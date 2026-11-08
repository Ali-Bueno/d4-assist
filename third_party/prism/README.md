# PRISM v0.18.3 (prebuilt)

From the [v0.18.3 release](https://github.com/ethindp/prism/releases/tag/v0.18.3), unchanged and never renamed
(the Wine bridges need both halves of the same build). MPL-2.0; `LICENSES/` and `NOTICE` come with the release.

| File | Release asset, path | SHA-256 |
|---|---|---|
| `prism.dll` | `prism-windows-x64.zip`, `dynamic/release/bin/` | `7c7d09c8c7306e8e1a0c46d603ccb2a391c194c77843400c11b746e7dd56a467` |
| `prism_orca_bridge.dll` | `prism-linux-x64.zip`, `dynamic/release/wine/placeholders/` | `b91d6a327396d9b0227b46a08ca6a39189565d54a2a8596fc292a0a9b56c8620` |
| `prism_speech_dispatcher_bridge.dll` | same | `83e761c674c2dc820ad8286dff528682ca266511055f3905cfc1d2522392fe26` |
| `prism_spiel_bridge.dll` | same | `a1e6844c49812c208aaf4a765682820d103be1d87ca18a4ab1f4979420a38d4c` |
| `wine/prism_orca_bridge.dll.so` | `prism-linux-x64.zip`, `dynamic/release/wine/` | `8cd778afb57f433a3a347bd7901e87fa22c7695fb35e4a2527be8a333c323824` |
| `wine/prism_speech_dispatcher_bridge.dll.so` | same | `8dcd2629052bd23d001ceb26f05e5d814bf134c77b15b3e3bd8077130f35b603` |
| `wine/prism_spiel_bridge.dll.so` | same | `3d9bd679eb9d267d4616f156a904d06bf4f2c0b2f10cf831355f8cc15310fcd2` |

`src/D4Assist/D4Assist.csproj` uses this `prism.dll` instead of the v0.12.2 one Prismatoid 0.3.0 bundles (that binding's
config version 2 is accepted by v0.18.3). Why, and how the bridges are found under Wine:
[prism-prebuilt](../../reference/screen-reader-integration/prism-prebuilt.md).

To upgrade: `gh release download <tag> -R ethindp/prism -p prism-windows-x64.zip -p prism-linux-x64.zip`, replace every
file above from the same tag, and check that Prismatoid's config version is still accepted (`source/prism.cpp`, `prism_init`).
