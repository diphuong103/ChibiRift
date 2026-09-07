# ChibiRift

Foundation skeleton for the 2D Action Roguelite RPG specified in
`SRS_2D_Action_Roguelite_RPG_Unity_v1.1.docx`.

This repository contains **architecture only**. There is no gameplay content yet: no animation,
no enemy AI, no boss patterns, no level design, no VFX or SFX. The goal is that SRS phase **P1
(Core Prototype)** can begin writing gameplay logic without first having to make structural
decisions.

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

## 2. Opening the project

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

The scenes are intentionally empty shells. `Run_01` carries a placeholder "End Run" button purely
so this loop is walkable before the Run systems exist.

## 3. Folder structure

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
├── Prefabs/    (empty, P1)
├── Art/        (empty, P1)
├── Audio/      (empty, P1)
├── UI/         (empty, P1)
├── Settings/   (empty, P1)
└── Tests/
    └── EditMode/  ChibiRift.Tests.Edit
```

`Assets/Scenes/SampleScene.unity` and `Assets/Settings/` come from the Universal 2D template and
are left untouched.

## 4. Assembly dependency direction

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
| `ChibiRift.Tests.Edit` | all runtime assemblies (Editor platform only) |

### Consequences worth knowing

- **Gameplay cannot call UI, and UI cannot call Gameplay.** They communicate one way, through
  `EventBus` in Core (SRS 26). `HudController` subscribes to `HealthChangedEvent`; it has no way
  to reach `PlayerStats`.
- **Core owns no game rules.** It holds interfaces, the event bus, pooling and utilities.
- **No singletons.** `ServiceLocator` is built once by `GameBootstrap` in the Boot scene and is
  the only static access point in the project. Modules outside Core register themselves through
  `ServiceInstaller`, which is how Core stays dependency-free while still having one root.

## 5. Naming conventions

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

## 6. The three systems that are actually implemented

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

## 7. Running the tests

From the editor: **Window → General → Test Runner → EditMode → Run All**.

Headless:

```bash
~/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -batchmode -nographics -projectPath /home/dinhphuong/Linux/Game/ChibiRift \
  -runTests -testPlatform EditMode \
  -testResults /tmp/results.xml -logFile -
```

Current status: **41 tests, 41 passing**, covering the four-step damage order, the MinDamage
clamp on negative and zero results, the 0.8 damage-reduction ceiling, the XP curve and its
inverse, three-card rolls, the no-duplicate rule, Fallback Pool top-up, an exhausted pool, the
max-stack filter and seed reproducibility.

## 8. Regenerating the project scaffolding

`ChibiRift.EditorTools` can rebuild layers, the collision matrix, the baseline data assets and the
five scenes from scratch. Menu: **ChibiRift → Setup → Run All**, or headless:

```bash
~/Unity/Hub/Editor/6000.3.23f1/Editor/Unity \
  -batchmode -quit -nographics -projectPath /home/dinhphuong/Linux/Game/ChibiRift \
  -executeMethod ChibiRift.EditorTools.ProjectSetup.RunAll -logFile -
```

This is destructive to `Assets/_Project/Data/*.asset` and `Assets/_Project/Scenes/*.unity`.

## 9. Physics2D layers and collision matrix

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

## 10. Save and telemetry files

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

## 11. Git and LFS

`.gitattributes` routes `png/psd/wav/mp3/fbx` and other binaries through Git LFS.

> **`git-lfs` is not installed on this machine.** The filters are declared but inert until:
> ```bash
> sudo apt install git-lfs && git lfs install
> ```
> Commit binary art before doing this and it will be stored as a normal blob; `git lfs migrate`
> would then be needed. Install LFS **before** the first art commit.

The repository has been initialised (`git init`) but nothing has been staged or committed.

## 12. Related documents

- `TRACEABILITY.md` — every SRS requirement ID mapped to the file that serves it.
- `OPEN_ISSUES.md` — ambiguities found in the SRS and the provisional decision taken for each.
