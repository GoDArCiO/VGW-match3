# Gem Cascade — a polished match-3 vertical slice

> Swap, match, and chain explosive cascades — then spend the dice you earn on a Monopoly-style bonus
> board to unlock the next level. A small, juicy, **portfolio-grade** Unity 6 slice with a deliberately
> clean pure-logic / view split.

![Gameplay](docs/gameplay.gif)

---

## The pitch (30 seconds)

A 7×8 gem board. Tap a gem, tap a neighbour, and if it forms a line of 3+ it clears with a pop, a
particle burst and a floating score. Gravity drops the survivors, fresh gems rain in, and any **new**
matches resolve automatically — each cascade step louder than the last: rising pitch, a punchy combo
callout, screen shake and a hit-stop on the big ones. Hit the level's goal (e.g. *collect 25 blue gems
in 20 moves*) and you win **dice**. Spend them on a looped **bonus board**, hopping a token onto coins,
dice and stars. Collect enough stars and the next, harder level unlocks. Progress persists.

This slice exists to show two things: a **correct, unit-tested cascade engine**, and that match-3
retention lives in the **meta loop**, not just the board.

---

## Run it

Requires **Unity 6 (6000.3.11f1)** with the WebGL module (only for the optional web build).

**In the editor**
1. Open this folder as a project in Unity Hub.
2. Open `Assets/Scenes/Proto.unity` and press **Play**.
3. Set the Game view to a **portrait** aspect (e.g. 1080×1920) — the slice is composed for phone.

The scene is generated headlessly, so if it's ever missing or stale, regenerate it (editor closed):

```powershell
.\tools\unity.ps1 exec -Method Proto.EditorTools.SceneSetup.Build   # builds the scene + config assets
.\tools\unity.ps1 compile                                           # CS error gate -> COMPILE OK
.\tools\unity.ps1 test                                              # EditMode tests -> TESTS PASS
.\tools\unity.ps1 build-webgl                                       # optional WebGL build -> Builds\
```

**Controls:** tap a gem to select it, tap an adjacent gem to swap. On the bonus board, tap **Roll**.
The always-visible gear (bottom-right) opens Music/SFX volume and a Reset-progress button.

---

## Architecture

The headline decision: **all game rules are pure C# with no `UnityEngine` dependency**, and the view
only renders and animates what the rules hand it. This is enforced at compile time by the assembly graph.

```
Proto.Core   (engine-free)   Board · MatchFinder · CascadeResolver · Match3Game · meta models
   ▲      ▲      ▲      ▲
Proto.Game  Match3.Config  Match3.Meta  Match3.View      Proto.Editor (headless tools)   Proto.Tests
(juice      (ScriptableObj  (PlayerPrefs (board/meta views,                              (EditMode,
 toolkit)    config)         store + view) HUD, composition root)                         Core only)
```

> **Naming convention:** `Proto.*` assemblies are the reusable prototype **template** I build on (the
> engine-free core primitives, the juice toolkit, the headless editor tooling, the EditMode test harness);
> `Match3.*` assemblies are the game I authored on top (config, meta, view). The match-3 domain types live
> in namespace `Match3.Core` inside the shared engine-free core assembly. Each layer has its own `.asmdef`,
> so the dependency direction (`View → Meta → Config → Core`, and `Core` referencing nothing) is enforced
> by the compiler, not convention.

- **`Proto.Core`** — the domain. `Board` (a grid of color ids), `MatchFinder` (horizontal + vertical
  runs of 3+, with L/T junctions grouped as one connected component), and the star of the show, the
  **`CascadeResolver`**: a pure, deterministic, *synchronous* planner. Given a board, a swap and an
  injected gem source it returns the **entire cascade** — an ordered list of `CascadeStep`s
  (`clear → gravity → refill`, re-checked until stable) — plus the score and final state. No Unity, no
  coroutines, a hard `MaxCascade` guard so it always terminates. Scoring is an injected `ScoringConfig`
  (base points, the multiplier curve, bonuses) rather than baked constants — a deliberate live-ops seam
  (see below). The meta-progression model (`MetaBoard`, `MetaProgress`) lives here too, equally testable.
- **`Match3.View`** — the **only** sequencer. It replays the resolver's steps over time (the 3-phase
  per-step contract: clear, then gravity slides, then refills), keeping its own cell→GameObject map, and
  fires all the juice with the right timing. It never does grid math.
- **`Proto.Game`** — a reusable, game-agnostic juice toolkit (coroutine tweens, trauma-shake camera,
  hit-stop, particle bursts, URP post-fx pulses, audio, procedural shape sprites, UI-Toolkit floating
  text). Inherited from the ProtoGames template; the match-3 build *uses* it, doesn't reinvent it.
- **`Match3.Config`** — `ScriptableObject`s for gem types, level definitions and the meta-board layout,
  each converting to an engine-free Core POCO. Authored as real assets, generated headlessly.
- **`Match3.Meta`** — the bonus board view and the versioned `PlayerPrefs` persistence adapter.
- **One composition root** (`Match3Bootstrap`) news up the whole graph and owns the `Board ⇄ Meta`
  screen flow and level progression. No singletons, no `FindObjectOfType`, no service locator.

### Why a synchronous planner?

Because it makes the hardest part — cascade correctness — trivially testable. `Match3Game.TrySwap`
returns the full result up front, so a test asserts on data (step count, per-step scores, final board)
with zero scene, zero frames. The view owns animation timing and the "busy" lock; the core never blocks.

---

## Tests

EditMode tests (`Assets/Tests/EditMode`, `Proto.Tests` — references only `Proto.Core`) cover match
detection (runs, L/T junctions, parallel-run merges, gaps, edges, empties), gravity and refill
(compaction, order, deterministic per-column refill, constant fall distance), the resolver (illegal /
non-adjacent swaps, single match, **a 2-step cascade — gravity-driven and refill-driven**, two-matches-
in-one-swap, scoring/multiplier, structural invariants on generated boards), the session (goal/win/lose,
illegal-move and terminal-state guards), board generation + deadlock reshuffle, and the meta layer
(loop wrap, dice spend, single-shot star unlock, persistence round-trip + fresh defaults). Determinism is
pinned by `DeterministicReplayTests` (same seed + move list → identical score and board); the injectable
scoring economy and the `Clear()` reset seam are covered too.

---

## Continuous integration

`.github/workflows/ci.yml` runs the EditMode suite on every push and PR via **GameCI**
(`game-ci/unity-test-runner`) on a Linux container — the cloud equivalent of the local
`tools\unity.ps1 test` gate, caching `Library/` between runs and uploading results as an artifact. It
needs `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD` repo secrets to activate Unity (standard for
GameCI); the workflow is wired and ready, the secrets are the only remaining setup.

---

## Determinism & the live-ops seams

The slice is structured so the things a high-DAU live title iterates on are *seams*, not rewrites:

- **Deterministic by construction** — pure C# rules over a seeded `System.Random` (never
  `UnityEngine.Random`), no wall-clock. A fixed seed + a fixed move list reproduces an exact score and
  board — the property daily/shared boards, server-side replay validation (anti-cheat) and golden-master
  regression all build on. Pinned by `DeterministicReplayTests`.
- **Scoring is data, not constants** — base points, the combo-multiplier curve and long-match bonuses
  live in an injected `ScoringConfig` (Core POCO, defaulted to the original values). A remote-config
  provider or an A/B bucket can hand a different economy to `Match3Game` with **no client release**;
  `InjectedScoringConfig_RetunesTheEconomy_*` proves it, and the view reads each cleared group's score off
  the `CascadeStep` rather than re-deriving the formula, so the HUD can never drift from the banked score.
- **Persistence behind an interface** — `IMetaStore` (`Load` / `Save` / `Clear`) is the single swap
  point: `PlayerPrefs` today, a server-authoritative store tomorrow, call sites unchanged.

---

## Scope & production notes (what I'd build next)

A deliberately-bounded slice: a correct, tested core + a juicy view, **not** a shipped service. The
concerns below are intentionally out of scope here — I note them (and where each plugs into the existing
seams) rather than half-implementing them:

- **Analytics / telemetry** — no event surface yet, but the synchronous planner gives one clean emission
  point: an injected `IAnalytics` called from `Match3Game.TrySwap` (`MoveResult` already carries
  `MaxChain` / `ScoreAfter` / `MovesLeftAfter`), `MetaProgress.SpendDie` and `BeginLevel`. Same adapter
  shape as `IMetaStore`; the core stays engine-free.
- **Server authority** — for a real economy the server owns balances; the client sends *intents* (won a
  level, spent a die), the server validates and returns the new state, and `PlayerPrefs` becomes a cache.
  `IMetaStore` is already that seam.
- **Mobile hardening** (this builds to WebGL/desktop) — on device I'd inset the UI root by
  `Screen.safeArea`, switch `PanelSettings` to `ScaleWithScreenSize` against 1080×1920, set
  `Application.targetFrameRate` (lower on the idle meta screen for battery/thermals), lock portrait, and
  bind a gesture to a single touch id. The DPI-scaled swipe threshold in `PointerInputAdapter` is already
  in place.
- **GC at scale** — the core is allocation-aware (`int[,]` board, non-boxing `struct Cell`, pooled
  floating text). On a larger board the next steps are an allocation-free `HasMatch` (an early-out scan
  instead of a `HashSet` per legal-move probe) and pooling the gem `GameObject`s — mirroring the
  floating-text pool already here.
- **Special gems / new goals / new rewards** — a bomb/line-clear, a "clear the jelly" objective, or a new
  bonus-tile reward would each be a new strategy type (`IGemEffect` / `IObjective` / a reward effect)
  plugged into the resolver and `MetaProgress`, not edits spread across the model. The pure-core +
  composition-root structure is what keeps that a clean addition.

---

## Audio (drop-in)

The build is fully playable **silent**; audio is added by dropping clips in — no code change. SFX load
by hook name from `Assets/Resources/Sfx/<hook>.wav`; one looping clip in `Assets/Resources/Music/`
becomes the soundtrack. Hooks fired by the slice:

`game_start` · `swap` · `invalid_swap` · `match` *(pitch rises with cascade depth)* · `cascade` ·
`win` · `lose` · `restart` · `ui_click` · `dice_roll` · `token_hop` · `reward_coin` · `reward_star` ·
`level_unlock`

---

## Project layout

```
Assets/
  Scripts/
    Core/    pure domain (+ Core/Meta) — no UnityEngine
    Game/    reusable juice toolkit (tweens, shake, particles, post-fx, audio, sprites, floating text)
    Config/  ScriptableObject config + POCO converters
    Meta/    PlayerPrefs store + bonus-board view
    View/    board view, HUD, composition root
  Editor/Proto/   headless scene + asset generators, WebGL build
  Tests/EditMode/ pure-logic unit tests
  UI/             UXML + USS (HUD, banner, settings, meta HUD)
  Scenes/Proto.unity, Resources/Sfx, Resources/Music
tools/unity.ps1   headless batchmode runner
```
