# CI dependencies

Prebuilt copies of libraries OP2Mapper references from a folder named `other`
next to the repository (`..\other`). A local build uses that folder; the
GitHub Actions build (`.github/workflows/build.yml`) copies these files there
instead, so the project builds unchanged.

| File | Version | Source |
|---|---|---|
| `OP2MapJsonTools.dll` | 0.2.6 | [OP2MapJsonToolsLibrary](https://github.com/leviathan400/OP2MapJsonToolsLibrary) |

op2maplib and OP2UtilityDotNet aren't here: CI builds them from source from
the `lib/OP2MappingLib` and `lib/OP2UtilityDotNet` submodules, at the commits
the submodules are pinned to.

When OP2Mapper moves to a newer version of a library, update the copy here
(OP2MapJsonTools) or move its submodule pin, or the CI build tests against
the old one.