/*
 * prism_bridge.c - Lua 5.4 C module exposing PRISM (screen reader) to UE4SS Lua.
 *
 * Approach (validated by AccessForge/SparkingZeroAccess's speech_bridge, and reused
 * on DBZ Kakarot): a Lua C module that STATICALLY links its own Lua 5.4.4 (the exact
 * version UE4SS bundles, so the lua_State ABI matches) and DYNAMICALLY loads prism.dll
 * at runtime.
 * UE4SS does `require("prism_bridge")`, which calls luaopen_prism_bridge with UE4SS's
 * lua_State; our statically-linked Lua manipulates that state. No UE4SS source build,
 * no UEPseudo, no Rust, no external process.
 *
 * Deploy prism_bridge.dll + prism.dll + tolk.dll into the mod's Scripts/ folder.
 *
 * Lua usage:
 *   local prism = require("prism_bridge")
 *   prism.say("Hello", true)   -- speak (interrupt previous); interrupt defaults true
 *   prism.say("queued", false) -- speak without interrupting
 *   prism.stop()
 *   prism.is_speaking()        -> bool
 *   prism.detect()             -> backend name (e.g. "NVDA") or nil
 *   prism.set_rate(v) / set_volume(v) / set_pitch(v)   -- v is a float
 */

#include <windows.h>
#include <string.h>

#include "lua.h"
#include "lauxlib.h"

/* PRISM types only; we resolve the functions ourselves, so suppress its dllimport decls. */
#define PRISM_STATIC
#include "prism.h"

/* ---- PRISM function pointer types (PRISM_CALL == __cdecl) ---- */
typedef PrismConfig   (PRISM_CALL *fn_config_init)(void);
typedef PrismContext* (PRISM_CALL *fn_init)(PrismConfig*);
typedef void          (PRISM_CALL *fn_shutdown)(PrismContext*);
typedef PrismBackend* (PRISM_CALL *fn_acquire_best)(PrismContext*);
typedef const char*   (PRISM_CALL *fn_backend_name)(PrismBackend*);
typedef PrismError    (PRISM_CALL *fn_backend_initialize)(PrismBackend*);
typedef PrismError    (PRISM_CALL *fn_speak)(PrismBackend*, const char*, bool);
typedef PrismError    (PRISM_CALL *fn_output)(PrismBackend*, const char*, bool);
typedef PrismError    (PRISM_CALL *fn_stop)(PrismBackend*);
typedef PrismError    (PRISM_CALL *fn_is_speaking)(PrismBackend*, bool*);
typedef PrismError    (PRISM_CALL *fn_set_float)(PrismBackend*, float);
typedef void          (PRISM_CALL *fn_backend_free)(PrismBackend*);

static HMODULE g_self = NULL;   /* our own module handle (set in DllMain) */
static HMODULE g_prism = NULL;  /* prism.dll */

static fn_config_init        p_config_init = NULL;
static fn_init               p_init = NULL;
static fn_shutdown           p_shutdown = NULL;
static fn_acquire_best       p_acquire_best = NULL;
static fn_backend_name       p_backend_name = NULL;
static fn_backend_initialize p_backend_initialize = NULL;
static fn_speak              p_speak = NULL;
static fn_output             p_output = NULL;
static fn_stop               p_stop = NULL;
static fn_is_speaking        p_is_speaking = NULL;
static fn_set_float          p_set_rate = NULL;
static fn_set_float          p_set_volume = NULL;
static fn_set_float          p_set_pitch = NULL;
static fn_backend_free       p_backend_free = NULL;

static PrismContext* g_ctx = NULL;
static PrismBackend* g_backend = NULL;
static int g_ready = 0;

/* ---- Lua-callable functions ---- */

static int l_say(lua_State *L) {
    const char* text = luaL_checkstring(L, 1);
    int interrupt = 1; /* default: interrupt */
    if (lua_gettop(L) >= 2) { interrupt = lua_toboolean(L, 2); }
    if (!g_ready) { lua_pushboolean(L, 0); return 1; }
    PrismError e = p_speak(g_backend, text, interrupt ? true : false);
    lua_pushboolean(L, e == PRISM_OK);
    return 1;
}

static int l_output(lua_State *L) {
    const char* text = luaL_checkstring(L, 1);
    int interrupt = 1;
    if (lua_gettop(L) >= 2) { interrupt = lua_toboolean(L, 2); }
    if (!g_ready) { lua_pushboolean(L, 0); return 1; }
    PrismError e = p_output(g_backend, text, interrupt ? true : false);
    lua_pushboolean(L, e == PRISM_OK);
    return 1;
}

static int l_stop(lua_State *L) {
    if (g_ready) { p_stop(g_backend); }
    return 0;
}

static int l_is_speaking(lua_State *L) {
    bool speaking = false;
    if (g_ready) { p_is_speaking(g_backend, &speaking); }
    lua_pushboolean(L, speaking ? 1 : 0);
    return 1;
}

static int l_detect(lua_State *L) {
    const char* name = (g_ready && p_backend_name) ? p_backend_name(g_backend) : NULL;
    if (name) { lua_pushstring(L, name); } else { lua_pushnil(L); }
    return 1;
}

static int l_set_rate(lua_State *L) {
    float v = (float)luaL_checknumber(L, 1);
    if (g_ready && p_set_rate) { lua_pushboolean(L, p_set_rate(g_backend, v) == PRISM_OK); }
    else { lua_pushboolean(L, 0); }
    return 1;
}

static int l_set_volume(lua_State *L) {
    float v = (float)luaL_checknumber(L, 1);
    if (g_ready && p_set_volume) { lua_pushboolean(L, p_set_volume(g_backend, v) == PRISM_OK); }
    else { lua_pushboolean(L, 0); }
    return 1;
}

static int l_set_pitch(lua_State *L) {
    float v = (float)luaL_checknumber(L, 1);
    if (g_ready && p_set_pitch) { lua_pushboolean(L, p_set_pitch(g_backend, v) == PRISM_OK); }
    else { lua_pushboolean(L, 0); }
    return 1;
}

static int l_is_ready(lua_State *L) {
    lua_pushboolean(L, g_ready);
    return 1;
}

static const luaL_Reg prism_funcs[] = {
    {"say", l_say},
    {"output", l_output},
    {"stop", l_stop},
    {"is_speaking", l_is_speaking},
    {"detect", l_detect},
    {"set_rate", l_set_rate},
    {"set_volume", l_set_volume},
    {"set_pitch", l_set_pitch},
    {"is_ready", l_is_ready},
    {NULL, NULL}
};

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved) {
    (void)lpvReserved;
    if (fdwReason == DLL_PROCESS_ATTACH) { g_self = hinstDLL; }
    return TRUE;
}

/* LoadLibrary a DLL sitting next to prism_bridge.dll. */
static HMODULE load_from_own_dir(const char* dllName) {
    char path[MAX_PATH];
    DWORD len = GetModuleFileNameA(g_self, path, MAX_PATH);
    char* lastSlash;
    if (len == 0 || len >= MAX_PATH) { return NULL; }
    lastSlash = strrchr(path, '\\');
    if (!lastSlash) { return NULL; }
    lastSlash[1] = '\0';
    if (strlen(path) + strlen(dllName) >= MAX_PATH) { return NULL; }
    strcat(path, dllName);
    return LoadLibraryA(path);
}

#define RESOLVE(var, name) do { \
    (var) = (void*)GetProcAddress(g_prism, (name)); \
    if (!(var)) { all = 0; } \
} while (0)

__declspec(dllexport) int luaopen_prism_bridge(lua_State *L) {
    char modDir[MAX_PATH];
    DWORD dirLen;
    int all = 1;
    PrismConfig cfg;
    PrismError err;

    /* Make our own directory the DLL search dir so prism.dll's own deps (tolk.dll) resolve. */
    dirLen = GetModuleFileNameA(g_self, modDir, MAX_PATH);
    if (dirLen > 0 && dirLen < MAX_PATH) {
        char* slash = strrchr(modDir, '\\');
        if (slash) { *slash = '\0'; }
        SetDllDirectoryA(modDir);
    }

    g_prism = load_from_own_dir("prism.dll");
    if (!g_prism) {
        luaL_error(L, "prism_bridge: failed to load prism.dll from mod dir (error %lu)", GetLastError());
        return 0;
    }

    RESOLVE(p_config_init, "prism_config_init");
    RESOLVE(p_init, "prism_init");
    RESOLVE(p_shutdown, "prism_shutdown");
    RESOLVE(p_acquire_best, "prism_registry_acquire_best");
    RESOLVE(p_backend_name, "prism_backend_name");
    RESOLVE(p_backend_initialize, "prism_backend_initialize");
    RESOLVE(p_speak, "prism_backend_speak");
    RESOLVE(p_output, "prism_backend_output");
    RESOLVE(p_stop, "prism_backend_stop");
    RESOLVE(p_is_speaking, "prism_backend_is_speaking");
    RESOLVE(p_set_rate, "prism_backend_set_rate");
    RESOLVE(p_set_volume, "prism_backend_set_volume");
    RESOLVE(p_set_pitch, "prism_backend_set_pitch");
    RESOLVE(p_backend_free, "prism_backend_free");
    if (!all) {
        luaL_error(L, "prism_bridge: prism.dll is missing expected exports");
        return 0;
    }

    cfg = p_config_init();
    g_ctx = p_init(&cfg);
    if (!g_ctx) { luaL_error(L, "prism_bridge: prism_init failed"); return 0; }

    g_backend = p_acquire_best(g_ctx);
    if (!g_backend) { luaL_error(L, "prism_bridge: no screen-reader backend available"); return 0; }

    /* acquire_best may already initialise the backend; "already initialized" is success. */
    err = p_backend_initialize(g_backend);
    if (err != PRISM_OK && err != PRISM_ERROR_ALREADY_INITIALIZED) {
        luaL_error(L, "prism_bridge: backend_initialize failed (%d)", (int)err);
        return 0;
    }

    g_ready = 1;
    luaL_newlib(L, prism_funcs);
    return 1;
}
