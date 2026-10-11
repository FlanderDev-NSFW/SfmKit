# SfmKit — shared library (source-included)

Reusable helpers for SecretFlasherManaka BepInEx IL2CPP mods. Each mod compiles its own copy
(`<Import Project="$(RepoRoot)build\SfmKit.props" />`), so end users install nothing extra.

| Folder   | Contents | References | Tested by |
|----------|----------|------------|-----------|
| `Pure/`  | Logic with **no** Unity / Il2Cpp types (`System.Numerics`, plain C#) | none | `tests/SfmKit.Tests` (runs without the game) |
| `Il2Cpp/`| Helpers that touch Il2CppInterop / Unity | game interop | build + in-game acceptance |

Rules
- **Never put a `ClassInjector`-registered type here.** Two mods injecting the same full type name collide.
  Injected MonoBehaviours live in the mod's own namespace.
- Keep game-specific knowledge (ExposureUnnoticed2.*) out of `Pure/`; put it behind small adapters.
- Every public API gets an XML doc comment saying what was **verified how** (compiled / unit-tested / in-game).
- Add new code to `docs/BACKLOG.md` first, then implement via the `build-library-feature` prompt.

Provenance: `ReadinessGate` and the icall helpers are generalised from CollarAsset (RigReadinessGate,
LoaderAccess.NativeFlags, CursorLease, C3MovementHook), whose license permits free use and modification.
