# SfmKit

Shared helpers for **Secret Flasher Manaka** BepInEx 6 IL2CPP mods: plain-C# logic (`src/SfmKit/Pure`, unit-tested without
the game) and small Il2Cpp / game adapters (`src/SfmKit/Il2Cpp`). Details and rules: [src/SfmKit/README.md](src/SfmKit/README.md).

It is **source only**: a mod compiles its own copy into its DLL, so players never install SfmKit and a mod's build needs
nothing from here. The SFM mod repositories keep that copy in `lib/SfmKit` (refreshed from this repository by
sfm-modkit's `sync-repo`). All types are `internal`, so two mods' copies never clash.

Used by: [RestraintHub](https://github.com/FlanderDev-NSFW/SFM-RestraintHub),
[SpreaderBar](https://github.com/FlanderDev-NSFW/SFM-SpreaderBar), [WallRestraint](https://github.com/FlanderDev-NSFW/SFM-WallRestraint),
[Yoke](https://github.com/FlanderDev-NSFW/SFM-Yoke), [Binders](https://github.com/FlanderDev-NSFW/SFM-Binders),
[NipplePiercings](https://github.com/FlanderDev-NSFW/SFM-NipplePiercings), [NippleTether](https://github.com/FlanderDev-NSFW/SFM-NippleTether),
[RestraintTemplate](https://github.com/FlanderDev-NSFW/SFM-RestraintTemplate).

Tests: `dotnet run --project tests/SfmKit.Tests -c Release` (also on every push, `.github/workflows/test.yml`).

Code: MIT licence (`LICENSE`).
