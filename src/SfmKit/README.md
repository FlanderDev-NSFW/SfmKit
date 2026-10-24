# SfmKit — shared library (source-included)

Reusable helpers for SecretFlasherManaka BepInEx IL2CPP mods. Each mod compiles its own copy
(`<Import Project="$(RepoRoot)build\SfmKit.props" />`), so end users install nothing extra.

| Folder   | Contents | References | Tested by |
|----------|----------|------------|-----------|
| `Pure/`  | Logic with **no** Unity / Il2Cpp types (`System.Numerics`, plain C#) | none | `tests/SfmKit.Tests` (runs without the game) |
| `Il2Cpp/`| Helpers that touch Il2CppInterop / Unity | game interop | build + in-game acceptance |
| `Il2Cpp/Game/` | Game-specific helpers (`ExposureUnnoticed2.*`): `RingEntry` (a mod's ring menu entry + number shortcuts), `GameUi` (panel open, interaction area, sound, message bar), `PlusMinusKeys` (the printed + / - keys with repeat) | game interop | in game via WallRestraint (L3 2026-10-24) |

`Pure/LockMachine` holds the lock / struggle-free rules shared by SpreaderBar and WallRestraint, `Pure/LimbGirth` a limb's
thickness from a cut through baked meshes (SpreaderBar's cuff fit); both unit-tested in SpreaderBar.Tests.

Rules
- **Never put a `ClassInjector`-registered type here.** Two mods injecting the same full type name collide.
  Injected MonoBehaviours live in the mod's own namespace.
- Keep game-specific knowledge (ExposureUnnoticed2.*) out of `Pure/`; put it behind small adapters.
- Every public API gets an XML doc comment saying what was **verified how** (compiled / unit-tested / in-game).
- Add new code to `docs/BACKLOG.md` first, then implement via the `build-library-feature` prompt.

Provenance: `ReadinessGate` and the icall helpers are generalised from CollarAsset (RigReadinessGate,
LoaderAccess.NativeFlags, CursorLease, C3MovementHook), whose license permits free use and modification.
