# CI dependencies

Prebuilt copies of libraries OP2Mapper references from a folder named `other`
next to the repository (`..\other`). A local build uses that folder; the
GitHub Actions build (`.github/workflows/build.yml`) copies these files there
instead, so the project builds unchanged.

| File | Version | Source |
|---|---|---|
| `OP2UtilityDotNet.dll` | 1.4.1 | [OP2UtilityDotNet](https://github.com/leviathan400/OP2UtilityDotNet) |
| `OP2MapJsonTools.dll` | 0.2.6 | [OP2MapJsonToolsLibrary](https://github.com/leviathan400/OP2MapJsonToolsLibrary) |

op2maplib isn't here: CI builds it from source from the `lib/OP2MappingLib`
submodule ([OP2MappingLib](https://github.com/OutpostUniverse/OP2MappingLib)),
at the commit the submodule is pinned to.

When OP2Mapper moves to a newer version of one of these libraries, update the
copy here too (and move the submodule pin for op2maplib), or the CI build
tests against the old one.
