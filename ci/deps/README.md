# CI dependencies

Prebuilt copies of the libraries OP2Mapper references from a folder named
`other` next to the repository (`..\other`). A local build uses that folder;
the GitHub Actions build (`.github/workflows/build.yml`) copies this folder
there instead, so the project builds unchanged.

| File | Version | Source |
|---|---|---|
| `OP2UtilityDotNet.dll` | 1.4.1 | [OP2UtilityDotNet](https://github.com/leviathan400/OP2UtilityDotNet) |
| `OP2MapJsonTools.dll` | 0.2.6 | [OP2MapJsonToolsLibrary](https://github.com/leviathan400/OP2MapJsonToolsLibrary) |
| `Op2MapLib.Net.dll`, `op2maplib/x86/op2maplib.dll`, `op2maplib/x64/op2maplib.dll`, `op2maplib/op2.json` | 0.3.4 | [OP2MappingLib](https://github.com/OutpostUniverse/OP2MappingLib) (MIT licence; includes code ported from op2-codegen-editor by fefi-byte, MIT) |

When OP2Mapper moves to a newer version of one of these libraries, update the
copy here too, or the CI build tests against the old one.
