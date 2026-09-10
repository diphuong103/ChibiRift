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

Current status: **132 EditMode + 37 PlayMode, all passing.**

| Suite | Count | What it covers |
|---|---|---|
| `DamageCalculatorTests` | 14 | The four-step damage order, the MinDamage clamp on negative and zero results, the 0.8 damage-reduction ceiling |
| `ExperienceCurveTests` | 12 | The XP curve and its inverse |
| `UpgradeRollerTests` | 15 | Three-card rolls, the no-duplicate rule, Fallback Pool top-up, an exhausted pool, the max-stack filter, seed reproducibility |
| `InputActionsAssetTests` | 6 | The `.inputactions` asset itself: the map, all nine actions, the A/D composite and Space binding, and that W/S/F stay unbound (SRS 43 Q2) |
| `DataDefaultsConsistencyTests` | 74 | Every confirmed balance value survives a run of `SampleDataGenerator` — see below |
| `DamagePipelineSourceTests` | 2 | Health is only ever reduced through `CombatSystem` (HPS-003). A text scan, not a compiler guarantee — see OI-19 |
| `SceneActorVisualTests` | 4 | Every actor in Run_01 draws something, its sprite is a real asset, and nothing is scaled through its Transform. Covers what the logic suites structurally cannot see — see OI-23 |
| `AssetReferenceIntegrityTests` | 5 | No wave, stage or hero points at a missing asset, and no two assets share an id. Renaming an asset is the classic way to leave a reference that Unity only complains about at runtime |
| `PlayerMovementTests` (PlayMode) | 8 | TC-MOV: top speed, jump peak height, double jump, coyote time, jump buffer, wall collision, world clamp, fall respawn |
| `PlayerCombatTests` (PlayMode) | 10 | TC-COM: the active window, one hit per target per swing, the three hit chain, both combo resets, mouse aim and sprite flip, damage to a corpse, death firing once, and step 3 out-damaging step 1 |
| `Run01SceneTests` (PlayMode) | 3 | Plays the real Run_01 with the prefabs that ship in it: hero and enemies land, hero can jump. The only fixture that loads a scene rather than building actors in code |
| `EnemyAiTests` (PlayMode) | 16 | TC-AI: idle, chase, aggro hysteresis, walking home, attack window and cooldown, damage through the pipeline, hero i-frames, combo reset on being hit, knockback out and back, hurt stun, terminal death, and stats following the asset |

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

## 13. Related documents

- `TRACEABILITY.md` — every SRS requirement ID mapped to the file that serves it.
- `OPEN_ISSUES.md` — ambiguities found in the SRS and the decision taken for each.
