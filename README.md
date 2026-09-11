# ChibiRift

Foundation skeleton for the 2D Action Roguelite RPG specified in
`SRS_2D_Action_Roguelite_RPG_Unity_v1.1.docx`.

The foundation is architecture only. On top of it, two P1 slices are implemented and playable:

- **Slice 1 — movement and camera.** The hero walks, jumps, double jumps, collides with the world
  and is followed by a confined Cinemachine camera.
- **Slice 2 — combat.** Mouse-aimed basic attack, a three hit combo, a single damage pipeline,
  health and death, and floating damage numbers. `Run_01` carries three training dummies to hit.
- **Slice 3 — enemy AI.** One melee archetype that notices the hero, chases, attacks on a
  telegraphed rhythm, gets stunned and knocked back, dies, and walks home when it loses interest.
  Knockback and post-hit invulnerability apply to the hero too.

Everything else is still skeleton — no dash, skills, boss, wave, elite, ranged or charger enemies,
animation, art, audio or level design. Placeholders are flat-colour geometric sprites.

---

## 1. Unity version

| Item | Value |
|---|---|
| Editor | **Unity 6000.3.23f1** (Unity 6 LTS stream) |
| Template | Universal 2D (URP 17.3.0) |
| Language | C# |
| Scripting backend | Mono / .NET Standard 2.1 |
| Target platform | Windows PC (SRS 3.1) |

SRS section 25 leaves the exact LTS version to be agreed at kickoff. 6000.3.23f1 was chosen
because it is the version already installed on the development machine; this is recorded in
`OPEN_ISSUES.md` as decision **OI-06**.

### Packages

Only first-party Unity packages are used. No paid Asset Store content.

| Package | Version | Why |
|---|---|---|
| `com.unity.inputsystem` | 1.20.0 | All input. The legacy Input Manager is **not** used anywhere. |
| `com.unity.cinemachine` | 3.1.7 | Camera follow, confiner and impulse shake (CAM-001..004). |
| `com.unity.render-pipelines.universal` | 17.3.0 | 2D renderer. |
| `com.unity.test-framework` | 1.6.0 | EditMode unit tests. |
| `com.unity.ugui` | 2.0.0 | UI. |

## 2. Technical constants

These six numbers fix the pixel-art pipeline. They are related, not independent: changing one
without the others makes sprites shimmer or the camera crop wrong. Confirmed for P1 slice 1.

| Constant | Value | Where it is set |
|---|---|---|
| Pixels per unit (PPU) | **32** | `PixelPerfectCamera.assetsPPU` in `Run_01`; sprite import setting |
| Reference resolution | **640 x 360** | `PixelPerfectCamera.refResolution` (16:9, upscales cleanly to 1280x720 and 1920x1080) |
| Camera orthographic size | **5.625** | Derived, not typed: `360 / 32 / 2 = 5.625`. The camera therefore shows exactly 20 x 11.25 world units |
| Animation frame rate | **12 fps** | Animation clip sample rate (no clips exist yet; P2) |
| Tile size | **16 x 16 px** | Tileset import (no tileset exists yet; P2) |
| Hero height | **64 px = 2 units** | `64 / 32 = 2`. `CapsuleCollider2D` on `Hero.prefab` is 0.8 x 1.8, deliberately inside the sprite so shoulders do not catch on ledges |

Sprite import settings that go with them: **Point (no filter)** filtering, **None** compression,
**mipmaps off**. Bilinear filtering or compression would blur a 32-PPU sprite; mipmaps are pointless
when the camera never scales the sprite. `Art/Placeholder/px_white.png` is generated with exactly
these settings by **ChibiRift → Setup → Generate Placeholder Art**.

**Never size a sprite with Transform scale.** Use `SpriteRenderer.size` with `drawMode = Sliced`.
These renderers sit on the actor root, so scaling the transform also scales the collider while
probe and hitbox offsets stay in unscaled units — which is how the hero ended up unable to jump
(OI-23). `SceneActorVisualTests` fails the build if an actor is scaled.

A tile is 16 px but PPU is 32, so one tile is **half a world unit**. That is intentional: it gives
level geometry twice the placement resolution of the movement grid without shrinking the hero.

## 3. Opening the project

```bash
# Via Unity Hub: Add project from disk, select this folder, open with 6000.3.23f1.
# Or from the command line:
~/Unity/Hub/Editor/6000.3.23f1/Editor/Unity -projectPath /home/dinhphuong/Linux/Game/ChibiRift
```

Press **F1** in Run_01 for the development overlay: velocity, grounded, jump count, coyote and
buffer timers, plus attack state, combo step, aim direction, enemy count and what the nearest enemy
is doing. Combat has no animation or sound yet, so a swing that simply missed looks identical to
broken input — the overlay is how you tell them apart (OI-25).

**Always press Play from `Assets/_Project/Scenes/Boot.unity`.** Boot builds the
`ServiceLocator`; entering any other scene directly leaves services unregistered and the
navigation buttons will log an error and do nothing.

The scene flow that works today:

```
Boot ──auto──▶ MainMenu ──[Play]──▶ Hub ──[Start Run]──▶ Run_01
                                     ▲                      │
                                     └──[Return to Hub]── PostRun ◀──[End Run]──┘
```

`Boot`, `MainMenu`, `Hub` and `PostRun` are still empty shells wired only for navigation.
`Run_01` is a playable test arena: ground with a 3-unit gap at x = -8, boundary walls at +/-20,
a platform at (6, 3), a spawn point, and a camera confiner. Press **F1** in Run_01 for the debug
overlay (velocity, grounded, jump count, coyote and buffer timers).

## 4. Folder structure

```
Assets/_Project/
├── Scripts/
│   ├── Core/         ChibiRift.Core         service locator, event bus, interfaces, pooling
│   ├── Data/         ChibiRift.Data         ScriptableObject definitions
│   ├── Gameplay/     ChibiRift.Gameplay     player, combat, enemy, wave, stage, boss, run
│   ├── Meta/         ChibiRift.Meta         hub, currency, meta progression
│   ├── Save/         ChibiRift.Save         save/load, serialization, settings
│   ├── Telemetry/    ChibiRift.Telemetry    local JSON Lines logging
│   ├── UI/           ChibiRift.UI           HUD, menus, panels
│   └── EditorTools/  ChibiRift.EditorTools  editor-only generators (excluded from builds)
├── Data/       ScriptableObject assets (one baseline per type)
├── Scenes/     Boot, MainMenu, Hub, Run_01, PostRun
├── Prefabs/    Hero.prefab, ENM_MeleeGrunt.prefab, DamageNumber.prefab
├── Settings/   ChibiRiftControls.inputactions
├── Art/        Placeholder/px_white.png (tinted and sized per renderer)
├── Audio/      (empty, P2)
├── UI/         (empty, P2)
└── Tests/
    ├── EditMode/  ChibiRift.Tests.Edit
    └── PlayMode/  ChibiRift.Tests.Play
```

`Assets/Scenes/SampleScene.unity` and `Assets/Settings/` come from the Universal 2D template and
are left untouched.

## 5. Assembly dependency direction

Each module has its own assembly definition. This shortens compile time and, more importantly,
makes the illegal directions **impossible to compile** rather than merely discouraged.

```
                    ┌──────────────────┐
                    │  ChibiRift.Core  │   ← references nothing of ours
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │  ChibiRift.Data  │
                    └────────┬─────────┘
          ┌──────────────────┼──────────────────┬─────────────────┐
          │                  │                  │                 │
   ┌──────▼──────┐   ┌───────▼──────┐   ┌───────▼──────┐  ┌───────▼───────┐
   │  Gameplay   │   │     Save     │   │      UI      │  │   Telemetry   │
   └─────────────┘   └───────┬──────┘   └──────────────┘  └───────────────┘
                             │            (Core + Data only,      (Core only)
                     ┌───────▼──────┐      strictly read-only)
                     │     Meta     │
                     └──────────────┘
```

| Assembly | References (ours) |
|---|---|
| `ChibiRift.Core` | *none* |
| `ChibiRift.Data` | Core |
| `ChibiRift.Gameplay` | Core, Data |
| `ChibiRift.Save` | Core, Data |
| `ChibiRift.Meta` | Core, Data, Save |
| `ChibiRift.Telemetry` | Core |
| `ChibiRift.UI` | Core, Data |
| `ChibiRift.EditorTools` | all of the above (Editor platform only) |
| `ChibiRift.Tests.Edit` | all runtime assemblies + EditorTools (Editor platform only) |
| `ChibiRift.Tests.Play` | Core, Data, Gameplay |

### Consequences worth knowing

- **Gameplay cannot call UI, and UI cannot call Gameplay.** They communicate one way, through
  `EventBus` in Core (SRS 26). `HudController` subscribes to `HealthChangedEvent`; it has no way
  to reach `PlayerStats`.
- **Core owns no game rules.** It holds interfaces, the event bus, pooling and utilities.
- **No singletons.** `ServiceLocator` is built once by `GameBootstrap` in the Boot scene and is
  the only static access point in the project. Modules outside Core register themselves through
  `ServiceInstaller`, which is how Core stays dependency-free while still having one root.

## 6. Naming conventions

### One runtime property, one owner

Each mutable property of a component has exactly **one** class allowed to write it. Anything else
that wants to affect it sends a request to the owner rather than writing directly.

| Property | Owner | How everyone else asks |
|---|---|---|
| `Time.timeScale` | `PauseManager` | `IPauseService.Pause` / `RequestHitStop` |
| `SpriteRenderer.color` and alpha | `SpriteFeedback` | raise `HealthComponent.Damaged`, or open an i-frame window |
| `SpriteRenderer.flipX` | `PlayerCombat` | `SetAimTarget` |
| `Rigidbody2D.linearVelocity` | `PlayerMotor` / `EnemyMotor` | `SetMoveIntent`, `ApplyKnockback`, `BeginDash` |
| `Transform.position` (hero) | `PlayerMotor` | `Teleport` |
| `GameObject.activeSelf` (pooled enemy) | `EnemySpawner` | `Spawn` / `Despawn`; `HealthComponent` only announces `CorpseExpired` |

**Why this is a rule and not a preference.** The project has hit the same failure three times, and
each time it was two writers rather than a logic error:

- Slice 2: `PlayerMotor` and `PlayerCombat` both wrote `flipX`, and the sprite flickered whenever
  the cursor and the movement direction disagreed (OI-20).
- Slice 4A, caught in planning: hit stop was specified as a second writer of `Time.timeScale`. A
  freeze expiring during a pause would have set the scale back to 1 and un-paused the game
  underneath the player.
- Slice 4A, same review: the white hit flash and the i-frame pulse both wanted
  `SpriteRenderer.color`, and they fire together by definition, because being hit is what opens the
  window.

- Slice 4B: `HealthComponent` deactivated a corpse on its own timer while the pool also owned the
  instance. Releasing something already inactive is how a pool hands the same object to two
  callers; `Test_Pool_DoubleDespawnIsSafe` covers it.

Update the table in the same commit that changes an owner.

### Two names that are easy to confuse

| Name | Meaning |
|---|---|
| `CombatSystem.DealDamage` | **The single entry point.** Anything wanting to hurt something calls this, and only this (HPS-003). |
| `IDamageable.ApplyDamage` | **The receiving end.** Applies an already-calculated result. Only `CombatSystem` calls it; `DamagePipelineSourceTests` fails the build if anything else does. |

They read alike and mean opposite ends of the same transaction. Calling `ApplyDamage` directly
skips the death guard, the i-frame guard, the crit roll and the damage event in one go.


| Kind | Convention | Example |
|---|---|---|
| Namespace | `ChibiRift.<Module>` | `ChibiRift.Gameplay` |
| Class / method / property | `PascalCase` | `DamageCalculator.Calculate` |
| Private field | `_camelCase` | `_balanceConfig` |
| Constant | `PascalCase` | `MaxBufferedRecords` |
| Event payload struct | `<Subject><Verb>Event` | `HealthChangedEvent` |
| Interface | `I<Name>` | `IDamageable` |
| Data asset file | `<TYPE>_<Name>.asset` | `ENM_Melee.asset`, `HERO_Knight.asset` |
| Scene | `PascalCase`, referenced only via `SceneNames` | `Run_01` |
| TODO marker | `// TODO(<REQ-ID>): <what>` | `// TODO(MOV-006): dash with i-frames` |

Every skeleton method body carries a `TODO(<requirement id>)` so the implementer can trace back to
the SRS clause without re-reading the document.

## 7. The three systems that are actually implemented

Everything else is a skeleton. These three are complete, pure, and unit-tested, because SRS
NFR-008 requires damage, XP and RNG to be independently testable.

| System | File | Requirements |
|---|---|---|
| `DamageCalculator` | `Scripts/Gameplay/Combat/DamageCalculator.cs` | SRS 9, HPS-003, HPS-009, HPS-010, COM-006 |
| `ExperienceCurve` | `Scripts/Gameplay/Progression/ExperienceCurve.cs` | SRS 10, EXP-003 |
| `UpgradeRoller` | `Scripts/Gameplay/Progression/UpgradeRoller.cs` | EXP-005, EXP-006, EXP-009, UPG-004, UPG-009, UPG-010, RNG-001, RNG-005 |

All three are `static`, take every parameter explicitly, touch no `MonoBehaviour` and no global
RNG. `UpgradeRoller` takes a seeded `DeterministicRandom` so a roll is reproducible (RNG-003).

The damage formula follows the **mandatory** order of SRS section 9:

```
1.  Raw        = BaseDamage × AttackModifiers
2.  if crit:  Raw = Raw × CriticalMultiplier
3.  AfterArmor = (Raw − Target.Defense) × (1 − clamp(Target.DamageReduction, 0, 0.8))
4.  Final      = max(AfterArmor, MinDamage)
```

`DamageResult` exposes each intermediate value so the order itself is asserted by tests, not just
the final number.

## 8. Running the tests

From the editor: **Window → General → Test Runner → Run All**, on both the EditMode and PlayMode
tabs.

Headless:

```bash
~/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -batchmode -nographics -projectPath /home/dinhphuong/Linux/Game/ChibiRift \
  -runTests -testPlatform EditMode \
  -testResults /tmp/edit.xml -logFile -

~/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -batchmode -nographics -projectPath /home/dinhphuong/Linux/Game/ChibiRift \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/play.xml -logFile -
```

Current status: **184 EditMode + 69 PlayMode, all passing.**

| Suite | Count | What it covers |
|---|---|---|
| `DamageCalculatorTests` | 14 | The four-step damage order, the MinDamage clamp on negative and zero results, the 0.8 damage-reduction ceiling |
| `ExperienceCurveTests` | 12 | The XP curve and its inverse |
| `UpgradeRollerTests` | 15 | Three-card rolls, the no-duplicate rule, Fallback Pool top-up, an exhausted pool, the max-stack filter, seed reproducibility |
| `InputActionsAssetTests` | 6 | The `.inputactions` asset itself: the map, all nine actions, the A/D composite and Space binding, and that W/S/F stay unbound (SRS 43 Q2) |
| `DataDefaultsConsistencyTests` | 122 | Every confirmed balance value survives a run of `SampleDataGenerator` — see below |
| `DamagePipelineSourceTests` | 2 | Health is only ever reduced through `CombatSystem` (HPS-003). A text scan, not a compiler guarantee — see OI-19 |
| `PrefabWiringTests` | 3 | Every serialized field on the hero and enemy prefabs is wired, or declared empty with a reason. Covers the third value path — prefab fields — which neither of the data suites can see (OI-26) |
| `SceneActorVisualTests` | 4 | Every actor in Run_01 draws something, its sprite is a real asset, and nothing is scaled through its Transform. Covers what the logic suites structurally cannot see — see OI-23 |
| `DeviceInputSourceTests` | 1 | `Keyboard/Mouse/Gamepad.current` appears only inside `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`. Development tools are the one exception to input going through `IInputService`, and this is the fence around it (OI-29) |
| `AssetReferenceIntegrityTests` | 5 | No wave, stage or hero points at a missing asset, and no two assets share an id. Renaming an asset is the classic way to leave a reference that Unity only complains about at runtime |
| `PlayerMovementTests` (PlayMode) | 8 | TC-MOV: top speed, jump peak height, double jump, coyote time, jump buffer, wall collision, world clamp, fall respawn |
| `PlayerCombatTests` (PlayMode) | 10 | TC-COM: the active window, one hit per target per swing, the three hit chain, both combo resets, mouse aim and sprite flip, damage to a corpse, death firing once, and step 3 out-damaging step 1 |
| `Run01SceneTests` (PlayMode) | 34 | **The only fixture that plays the real game.** Loads the real Boot scene so the real `InputReader` and `CombatSystem` are built, drives simulated mouse and keyboard, then plays Run_01 with the prefabs that ship in it: every wired action reaches its property, a real click damages a dummy, Space jumps, hero and enemies land, the nearest enemy actually closes distance, a landed hit produces a visible health bar and damage number, the dash/crit/hit-stop/shake feel systems, and — see section 14 — that one real allocation run stays under budget |
| `EnemyAiTests` (PlayMode) | 16 | TC-AI: idle, chase, aggro hysteresis, walking home, attack window and cooldown, damage through the pipeline, hero i-frames, combo reset on being hit, knockback out and back, hurt stun, terminal death, and stats following the asset |

### Writing a PlayMode test

**From P1 slice 4A every landed hit freezes time.** `Time.timeScale` goes to zero for up to 0.14s,
and `FixedUpdate` does not run at zero.

- **Never write `yield return new WaitForFixedUpdate()` in a test.** Use `TestTime.Steps(n)`,
  `TestTime.Seconds(s)` or `TestTime.RealSeconds(s)`. `Steps` waits each freeze out on frames
  before every physics step, because frames advance at a zero time scale and physics steps do not.
- Use `TestTime.RealSeconds` when the test itself is holding the freeze. A scaled wait there never
  returns.
- This applies to tests that never mention combat. An enemy landing a hit on the hero freezes time
  in the middle of a test about jumping just as effectively.

Two guards back the rule, and both were verified by deliberately breaking them:

| Guard | Fires when | What you see |
|---|---|---|
| `TestTime.Steps` | Time stays frozen past 2 real seconds | `TimeoutException: Time is frozen (timeScale=0) — did a hit stop leak, or is a WaitForFixedUpdate waiting on frozen physics?` |
| `[Timeout(20000)]` on every PlayMode fixture | A test runs past 20s for any other reason | `Timeout value of 20000 ms was exceeded.` |

20s is three times the slowest test today (6.40s, `Test_Flash_DoesNotLeakMaterials`). Raise it in
the same commit that makes a test legitimately slower.

**Why this is a rule rather than advice.** A hang is not a failure. The run does not stop and does
not report — it looks identical to "still in progress", and the first time it happened it cost a
full test cycle to work out that anything was wrong at all (OI-28).

### The three paths a value takes, and what watches each

| Route | Guarded by |
|---|---|
| C# field initialiser in `ChibiRift.Data` | `DataDefaultsConsistencyTests` |
| `SampleDataGenerator` writing a ScriptableObject | `DataDefaultsConsistencyTests` (it diffs real generator output) |
| Serialized field on a prefab | `PrefabWiringTests` |

All three have to hold. The hero once had the confirmed 0.8s invulnerability in its asset and in
its initialiser, and still had none in game, because the prefab field was 0 and only the first two
routes were checked (OI-26).

### Why `DataDefaultsConsistencyTests` matters

`SampleDataGenerator` deletes and recreates every data asset, so the generated values come from
the field initialisers in `ChibiRift.Data`, not from the `.asset` files. Confirming a number by
editing only the asset therefore looks right until the generator is re-run — at which point the
initialiser silently wins. That already happened once with `hurtIFrameDuration` (asset 0.8,
initialiser 0.5).

The test runs the generator into a scratch folder and diffs the result against the confirmed
values. **Every time a value is confirmed, add one row to `ConfirmedValues()`.** That list is the
only thing standing between a confirmed number and a silent revert.

## 9. Regenerating the project scaffolding

`ChibiRift.EditorTools` can rebuild layers, the collision matrix, the baseline data assets and the
five scenes from scratch. Menu: **ChibiRift → Setup → Run All**, or headless:

```bash
~/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -batchmode -quit -nographics -projectPath /home/dinhphuong/Linux/Game/ChibiRift \
  -executeMethod ChibiRift.EditorTools.ProjectSetup.RunAll -logFile -
```

This is destructive to `Assets/_Project/Data/*.asset` and `Assets/_Project/Scenes/*.unity`.

## 10. Physics2D layers and collision matrix

Nine layers occupy slots 6 to 14 (0–5 are Unity built-ins):

`Player`, `PlayerHitbox`, `Enemy`, `EnemyHitbox`, `Projectile_Player`, `Projectile_Enemy`,
`Ground`, `Boundary`, `Pickup`

Never write a raw layer index; use `ChibiRift.Core.GameLayers`.

|                    | Player | PlayerHitbox | Enemy | EnemyHitbox | Proj_Player | Proj_Enemy | Ground | Boundary | Pickup |
|--------------------|:------:|:------------:|:-----:|:-----------:|:-----------:|:----------:|:------:|:--------:|:------:|
| **Player**         |   ·    |      ·       |   ·   |      ✔      |      ·      |     ✔      |   ✔    |    ✔     |   ✔    |
| **PlayerHitbox**   |   ·    |      ·       |   ✔   |      ·      |      ·      |     ·      |   ·    |    ·     |   ·    |
| **Enemy**          |   ·    |      ✔       |   ✔   |      ·      |      ✔      |     ·      |   ✔    |    ✔     |   ·    |
| **EnemyHitbox**    |   ✔    |      ·       |   ·   |      ·      |      ·      |     ·      |   ·    |    ·     |   ·    |
| **Proj_Player**    |   ·    |      ·       |   ✔   |      ·      |      ·      |     ·      |   ✔    |    ✔     |   ·    |
| **Proj_Enemy**     |   ✔    |      ·       |   ·   |      ·      |      ·      |     ·      |   ✔    |    ✔     |   ·    |
| **Ground**         |   ✔    |      ·       |   ✔   |      ·      |      ✔      |     ✔      |   ·    |    ·     |   ✔    |
| **Boundary**       |   ✔    |      ·       |   ✔   |      ·      |      ✔      |     ✔      |   ·    |    ·     |   ✔    |
| **Pickup**         |   ✔    |      ·       |   ·   |      ·      |      ·      |     ·      |   ✔    |    ✔     |   ·    |

### Why each decision

- **Player ✗ Enemy.** Contact damage is delivered by `EnemyHitbox`, so making enemy bodies solid
  would only shove the hero around and let a crowd pin them against a wall. This directly serves
  the *Readable Chaos* pillar (SRS 5) and prevents the "died to physics, not to an attack"
  failure that pillar warns about.
- **Enemy ✔ Enemy.** Enemies do separate from each other, so a wave reads as a crowd instead of
  collapsing into a single overlapping silhouette.
- **Hitboxes touch only the opposing body.** `PlayerHitbox` sees `Enemy` and nothing else;
  `EnemyHitbox` sees `Player` and nothing else. Hitboxes never touch the world, pickups,
  projectiles or each other, so `COM-004` ("damage only during active frames") is enforced by the
  matrix rather than by filtering code in every collision callback.
- **Projectiles never collide with each other or with their owner.** A player projectile hits
  `Enemy`, `Ground` and `Boundary`; an enemy projectile hits `Player`, `Ground` and `Boundary`.
  This also keeps the projectile count in NFR-001 (≤ 80) cheap: no projectile-vs-projectile pairs.
- **Pickup ✔ Player / Ground / Boundary only.** Pickups fall, rest on the ground, stay inside the
  arena and are collected by the hero. They ignore enemies and projectiles so a busy fight cannot
  kick loot around.
- **Layers 0–5 left alone.** Interactions involving Unity's built-in layers are untouched, so
  nothing outside the project's own model is silently disabled.

## 11. Save and telemetry files

Both live under `Application.persistentDataPath`
(`~/.config/unity3d/DefaultCompany/ChibiRift/` on Linux):

| File | Purpose |
|---|---|
| `meta_save.json` | Progression and settings (SAVE-001, SAVE-002) |
| `meta_save.backup.json` | Previous good save, written automatically on each successful save |
| `telemetry.jsonl` | Local balance log, one JSON object per line (TEL-004) |

Saving is atomic: the payload is validated, written to a temp file, then swapped in with
`File.Replace`, which rolls the previous file into the backup. A partially written file therefore
cannot replace a good one (SAVE-003). Loading falls back main → backup → fresh defaults
(SAVE-004). Telemetry is buffered in memory and flushed in one batch at Run end, so it never
appears in the frame budget (TEL-005), and it can be switched off in Settings.

Nothing is ever sent over the network.

## 12. Git and LFS

`.gitattributes` routes `png/psd/wav/mp3/fbx` and other binaries through Git LFS.

> **`git-lfs` is not installed on this machine.** The filters are declared but inert until:
> ```bash
> sudo apt install git-lfs && git lfs install
> ```
> Commit binary art before doing this and it will be stored as a normal blob; `git lfs migrate`
> would then be needed. Install LFS **before** the first art commit.

The repository is committed and pushed to `https://github.com/diphuong103/ChibiRift.git`.

## 13. Sound effects

No audio ships yet. The wiring is complete and silent: every cue is connected, and a missing clip
is dropped without an error and reported once per session rather than once per hit.

**To add sound**, drop `.wav` files anywhere under `Assets/_Project/Audio/` and assign them on
`Assets/_Project/Data/SFX_Default.asset`:

| Field on `SfxLibrary` | Fires when |
|---|---|
| `Attack 1` | First combo step starts |
| `Attack 2` | Second combo step starts |
| `Attack 3` | Third combo step starts |
| `Hit` | Any non-critical hit lands |
| `Crit` | A critical hit lands (COM-006) |
| `Jump` | Jump or double jump leaves the ground |
| `Dash` | Dash starts (MOV-006) |
| `Hurt Hero` | The hero takes damage |
| `Enemy Death` | An enemy dies |

Attack cues fire as the swing starts, not as it connects, so a whiff still makes a sound.

The asset is regenerated by **ChibiRift → Setup**, which clears the clip assignments — assign them
after running Setup, or the next Setup run will empty them again.

## 14. Measuring frame time

`FrameTimeHarness` spawns the NFR-001 crowd, samples every frame for ten seconds and writes
`Logs/frametime-report.json`.

**Running it.** Open `Run_01`, press Play from `Boot`, then call `Run()` on the `FrameTimeHarness`
component on `DevTools` — or press **F3** a few times to fill the arena and watch the profiler
window directly. The console prints a one-line summary; the JSON has the full distribution.

**Reading it against the requirements:**

| Field | Requirement | Passing looks like |
|---|---|---|
| `MeanFps` | NFR-001, average FPS | ≥ 60 |
| `OnePercentLowFps` | NFR-001, 1% low | ≥ 50 |
| `P99Ms` | NFR-002, frame time ceiling | ≤ 33 |
| `FramesOver33Ms` | NFR-002 | as close to 0 as the run allows |
| `AllocatedKilobytesDelta` | NFR-002 | below `BalanceConfig.StressAllocationBudgetKilobytes` (8192); exact only when `GcCollectionsDuringSample` is 0, otherwise a floor |
| `GcCollectionsDuringSample` | NFR-002 | 0 for an exact allocation reading; above 0 means a collection ran and the figure undercounts |
| `StoppedBySafetyCap` | — | must be `false`; `true` means the sample did not reach the configured duration and nothing else here is comparable to a normal run |
| `TopAllocationSources` | NFR-002 | the five heaviest instrumented call sites, most bytes first — see below for what is and is not covered |

**What `TopAllocationSources` can and cannot see (OI-32).** `AllocationProfiler`
(`ChibiRift.Core`) brackets the hot paths most likely to matter — every enemy and hero
`Update`/`FixedUpdate`, and `CombatSystem.DealDamage`, which also captures everything a landed hit
triggers synchronously (hit stop, shake, damage numbers, SFX, particles, knockback), since
`EventBus.Publish` runs every subscriber on the same call stack. Across repeated 30-enemy/10s
runs, every one of these totalled under 60 KB, against a measured `AllocatedKilobytesDelta` of
2304-3456 KB. The remainder tracks the timing of enemies clustering around the hero rather than
anything traceable to a specific script, and is the best-supported read on it: Unity's own
Physics2D bookkeeping for a crowd of colliding, separating bodies — not a ChibiRift bug, and not
provably that either, since nothing at the script level can bracket the engine's own simulation
step. Full investigation notes, including two real bugs this harness shipped with and a hypothesis
that was tested and disproven (the F1 overlay), are in OI-32.

**What the measurement does not cover.** The harness casts no skill. The ultimate's cooldown is 15s
and the sample is 10s, so including it would make each run depend on whether a cast happened to
land inside the window. **The p99 therefore excludes the cost of an area skill sweeping its 3.5u
radius.** The F1 debug overlay and its enemy census are disabled for the duration of the run
(restored afterwards), so `AllocatedKilobytesDelta` is not measuring a diagnostic tool by accident.
All of this is written into the report file as well, so the numbers cannot be read without it.

**A batch-mode run measures nothing about frame-time performance, and cannot see anything tied to
rendering at all.** There is no renderer, so the frame times are not a player's frame times, and
`OnGUI`, Canvas rebuilds and sprite batching do not run here — verified for `OnGUI` directly, not
assumed. `Test_Profiler_HarnessProducesCompleteReport` asserts the harness produced every field and
never asserts an FPS threshold; `Test_Profiler_AllocationStaysUnderBudget` does assert a threshold,
because `AllocatedKilobytesDelta` does not depend on rendering to be meaningful. Only an editor run
on real hardware, with the overlay actually visible, answers NFR-001 and NFR-002 for frame time, or
can measure whatever this harness cannot see from here.

## 15. Related documents

- `TRACEABILITY.md` — every SRS requirement ID mapped to the file that serves it.
- `OPEN_ISSUES.md` — ambiguities found in the SRS and the decision taken for each.
