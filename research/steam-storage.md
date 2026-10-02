# Diablo IV — Steam install storage ("static CASC")

The Steam version (app 2344520) keeps the game files in a different on-disk layout from Battle.net. WiseOwl.Casc reads
only the Battle.net one, so `src/D4Data/Casc/Static` reads Steam's. **Nobody here has a Steam copy:** everything below
comes from code that reads real Steam installs, and the app's log reports each opening step so a Steam player's log
shows where it breaks.

Sources (read for format facts, no code copied):
- **rustydemon** (github.com/HoldMyBeer-gg/rustydemon, AGPL + Commons Clause), Steam backend "verified against a real
  Steam D4 install": `rustydemon-lib/src/{config,static_container,tvfs,handler}.rs`.
- **TACTLib** PR #38, commit 2fe45f5 "fenris: steam static vfs support" (MIT): `StaticContainerHandler{,_Fenris}.cs`,
  `VFSManifestReader.cs`, `ClientHandler.cs`, `ProductHandler_Fenris.cs`.
- **CascLib** PR #290 (MIT), D2R's Steam storage, same key and zlib scheme.

## 1. Detection

`<install>\Data\.build.config` with `key-layout-index-bits` and no `encoding` key (`StaticCascStorage.IsStaticInstall`).
Checked before `.build.info`: Steam's install has a root `.build.info` too, without the Battle.net columns. Archives sit in
`Data\000\`, `Data\001\`… (no `indices\`, `config\` or `data.NNN`). Found through Steam's uninstall key
`Steam App 2344520` or `appmanifest_2344520.acf` in a library (`SteamInstall`).

## 2. `.build.config`

`key = v1 v2 …` lines, `#` comments. Used keys:

| Key | Value |
|---|---|
| `key-layout-index-bits` | N: top bits of the location word that pick the layout |
| `key-layout-<i>` | `chunkBits archiveBits offsetBits [unit]`; unit 0/absent = bytes, 4096 seen for payloads |
| `vfs-root`, `vfs-<n>` | `<content key> <encoding key>`, 16-byte hex; the encoding key carries the location |
| `vfs-…-size` | `<content size> <encoded size>` |
| `vfs-…-espec` | `z` = raw zlib, no BLTE |

The build version comes from `build-name` (TACTLib reads it; rustydemon's notes say it's absent), else the root
`.build.info` `Version` column, else "unknown" (only the dump tool uses it). Only `vfs-root` and `vfs-<digits>` are
manifests; other `vfs-…` keys are ignored.

## 3. Locating a file from its key

Every key is 16 bytes: bytes 0–8 the hash, bytes 9–15 a big-endian 56-bit location word (byte 8 is ignored). From the top:
`layout` (N bits) → `key-layout-<layout>` → `chunk`, `archive`, `offset` (× unit) (`StaticLocation.Decode`). A layout
with 0 offset bits holds whole files (offset 0).

Archive names, under `Data\<chunk:D3>\` (`StaticLocation.ArchiveNames`; TACTLib's names first, rustydemon's guesses after):

| Layout | Name |
|---|---|
| 0 (and unknown) | `0x<archive:x4>-meta.dat` |
| 1 | `<archive>>16>-payload.dat` when `archive & 0xFFFF == 0`, else `<archive>>16>-<type-1:D4>.dat` |
| 2 | `<archive>-child.dat` |
| 3 | `0x<archive:x4>-payload.dat` |

No per-entry header at the offset (Battle.net's 30-byte envelope is absent). The encoded size comes from the TVFS
container-file table, or the build config for manifests; failing both, a BLTE block table or the end of a zlib stream.
Content is BLTE (by its magic), raw zlib (a `z` spec, or a zlib header when there is no spec) or plain bytes
(`StaticCascStorage.Decode`).

## 4. File table

The root manifest (raw zlib) and the nested `vfs-<n>` manifests are ordinary TVFS, with 16-byte keys. WiseOwl's
`TvfsManifest` keeps only a 9-byte key per path and drops the encoded size, so `TvfsFileTable` walks it again keeping each
span's full key, encoded size and spec (an ESpec table entry when header flag 2 is set). A span whose 9-byte key prefix
is a `vfs-*` key is a nested manifest, joined as `<name>/…`. Paths are hashed like WiseOwl's (upper case, backslashes).

A nested manifest that can't be read or isn't TVFS (e.g. a language not installed) is skipped and logged instead of
failing the storage. Whole files (layouts 1–2) are accepted only when their length equals the encoded size, since the two
naming schemes overlap (decimal `16-child.dat` vs hex archive 0x16); the log names the first file found per layout.

D2R's Steam manifests drop the separator inside folders (`meta123`); when CoreTOC or the first CoreTOC entries' metas
don't resolve, the table is rebuilt with separators ignored on both sides.

The Diablo IV paths are the Battle.net ones: `Base\CoreTOC.dat`, `Base\CoreTOCSharedPayloadsMapping.dat`,
`Base\<Meta|Payload|PayLow|PayMed|Child>\<id>` (`TvfsSnoArchive`, same shared-payload rule as WiseOwl's `Diablo4Storage`).

## 5. Verified here

- `TvfsFileTable` on the Battle.net install: every path resolves to WiseOwl's key; encoded sizes equal the local index's
  (minus the 30-byte envelope).
- `TvfsSnoArchive` over the Battle.net paths reads the same CoreTOC, metas, payloads and shared payloads as WiseOwl.
- A Steam-layout copy of real Battle.net files (`StaticInstallBuilder`, every layout and encoding, with and without
  separators, hex whole-file names behind decoys, an unreadable nested manifest) reads back byte for byte through `SnoStore`.
- An independent review against rustydemon, TACTLib and CascLib (2026-10-02) found no mismatch in paths, bits, names or
  decoding; its robustness points (skipped manifests, duplicate keys, name overlap, build-name, spec-driven zlib) are in.

## 6. Unknown until a Steam log

The real `key-layout-*` numbers; whether the whole-file names (layouts 1–2) are right; `PayMed` vs `paymid` (only
Battle.net's `PayMed` is used, and only `Meta`/`Payload` by the app); whether D4 drops separators; whether `Diablo IV.exe`
and `FenrisDebug.txt` are at the install root (the log's `Game install:` line says); whether the Steam build matches the
embedded `definitions.json`. The log lines start with `Steam storage:`.
