# Open Issues

Ambiguities, gaps and contradictions found while reading
`SRS_2D_Action_Roguelite_RPG_Unity_v1.1.docx`, together with the decision taken so the
skeleton could be built. **No new requirements were invented.** Where the SRS is silent, the
value chosen is marked *unconfirmed* and is a designer decision to confirm, not a fact.

> **Status 2026-09-05:** OI-01 to OI-05 are **closed** — the project owner confirmed the five
> outstanding balance values and they are applied to the assets. OI-06 to OI-32 remain open.
> The five values are now consistent in all three places: the `.asset` files, the C# field
> initialisers (`StatBlock.PlayerBaseline`, `DashConfig.Baseline`, `BalanceConfig`) and
> `TRACEABILITY.md`. A newly created asset therefore starts from the confirmed numbers.

---

Every unconfirmed number lives in a ScriptableObject, so confirming or changing it is a data edit,
not a code change.

---

## OI-01 — Critical multiplier is given as a range, not a value  ✅ CLOSED 2026-09-05

**SRS 35** lists `Crit Multiplier | 1.5x–2.0x`. A single number is needed to seed
`BalanceConfig` and `HeroData`.

**Resolved by the project owner: 2.0**, the upper bound of the SRS range.

Applied to `Data/BalanceConfig.asset` `_defaultCritMultiplier` and
`Data/HERO_Knight.asset` `_baseStats.CritMultiplier`. Both now read `2`.

---

## OI-02 — XP curve parameters have no numbers  ✅ CLOSED 2026-09-05

**SRS 10** gives the shape `XPRequired(level) = BaseXP × GrowthFactor^(level-1)`, and **SRS 35**
lists `XP Growth | Configurable` with no figures.

**Resolved by the project owner: BaseXP = 100, GrowthFactor = 1.4.**

Applied to `Data/BalanceConfig.asset` `_baseExperience` (100) and `_experienceGrowthFactor` (1.4).

Note for balance: 1.4 is a steep curve. Level 10 costs 100 × 1.4⁹ ≈ 2066 XP and reaching level 10
costs ≈ 7156 XP cumulative, against ≈ 1519 under the earlier working value 1.15. Expect noticeably
fewer level-ups per Run; worth re-checking against telemetry (TEL-002) once a Run is playable.

`MaxHeroLevel = 50` remains a project-added ceiling with no SRS basis, kept so
`LevelForTotalExperience` cannot loop without bound.

---

## OI-03 — Dash distance and duration are unspecified  ✅ CLOSED 2026-09-05

**SRS 35** gives `Dash Cooldown 1.5s` and `Dash I-Frame Duration 0.25s`, and **HER-006** requires
`distance, duration, i-frame, cooldown`. Distance and duration had no baseline anywhere.

**Resolved by the project owner: Distance = 5.0 world units, Duration = 0.25 s.**

Applied to `Data/HERO_Knight.asset` `_dash.Distance` (5) and `_dash.Duration` (0.25).
`IFrameDuration` (0.25) and `Cooldown` (1.5) keep their SRS 35 values, untouched.

Note: duration now equals the i-frame window exactly, so the hero is invulnerable for the whole
dash rather than part of it. That is a legitimate design choice, and worth confirming it is
intended rather than coincidental.

---

## OI-04 — Combo window has no baseline  ✅ CLOSED 2026-09-05

**COM-003** requires the combo to reset after a timeout; **SRS 35** lists
`Combo Window | Configurable` with no number.

**Resolved by the project owner: 0.5 s**, confirming the value already in use.

`Data/BalanceConfig.asset` `_defaultComboWindow` and `Data/HERO_Knight.asset` `_comboWindow`
both read 0.5; no change was needed.

---

## OI-05 — Post-hit i-frame duration is unspecified  ✅ CLOSED 2026-09-05

**HPS-005** requires invulnerability frames in **two** situations: after taking a hit, and during
the dash window. **SRS 35** only gives a duration for the dash case (0.25 s).

**Resolved by the project owner: 0.8 s after taking a hit.**

Applied to `Data/HERO_Knight.asset` `_hurtIFrameDuration` (0.8). It stays a separate field from
`_dash.IFrameDuration` (0.25) because the two serve different purposes and tune apart.

Still open for design: whether the two windows may overlap, or the longer one wins when a hit
lands during a dash. `PlayerStats.BeginInvulnerability` currently has no stacking rule.

---

## OI-06 — Unity version left open by the SRS

**SRS 25** says `Engine | Unity 2D, phiên bản LTS được team thống nhất tại thời điểm kickoff`,
while the work order specifies Unity 6 LTS.

**Decision:** **Unity 6000.3.23f1**, the version already installed on the development machine.
Recorded in README section 1. Package versions are pinned in `Packages/manifest.json` as SRS 40
requires ("Unity project và package versions được khóa").

---

## OI-07 — Damage and health are floats, which the SRS does not state

The SRS gives `MinDamage 1` and integer-looking stats, but never says whether damage and HP are
integers or floats.

**Decision:** **float** throughout (`StatBlock`, `IDamageable`, `DamageRequest`,
`DamageResult`). Percentage modifiers, damage-over-time ticks and the `(1 − DamageReduction)`
multiply all produce fractions, and rounding at each step would make the mandatory four-step
order of SRS 9 non-reproducible, which NFR-008 forbids. Rounding is a display concern for the
damage number in HPS-008.

**Needs from design:** confirmation, plus a rounding rule for the displayed number.

---

## OI-08 — Telemetry default state is unspecified

**TEL-004** requires the telemetry toggle to exist in Settings but does not say what a fresh
install should default to.

**Decision:** default **on** (`SettingsSave.TelemetryEnabled = true`), since the whole point of
TEL-001..003 is to gather balance data during development and nothing leaves the machine.

**Needs from product:** if the shipped build should default off, this is a one-line change.

---

## OI-09 — Upgrade pool content is out of skeleton scope

**UPG-008** requires at least 12 valid upgrades, and **UPG-009** requires a Fallback Pool of
repeatable stat upgrades. The work order asks for exactly one sample asset per data type.

**Decision:** one `UpgradeData` asset (`UPG_AttackUp`) was created. The schema, the weighting, the
stack cap and the fallback mechanism are all implemented and unit-tested, but **the 12+ upgrades
and the Fallback Pool entries are not authored**. `UpgradeRoller` logs an error and returns a
short offer if asked to roll against a pool that cannot fill three cards, which is the SRS 30
behaviour.

**Action:** authoring the pool is P2 (Vertical Slice) content work. Until it exists, the Level Up
panel must not be opened.

---

## OI-10 — A tenth assembly was added beyond the eight requested

The work order lists eight assemblies. A ninth, **`ChibiRift.EditorTools`**, was added.

**Rationale:** it holds the generators that create the layers, the collision matrix, the baseline
data assets and the five scenes. It is marked `includePlatforms: ["Editor"]`, so it is excluded
from every player build and cannot affect runtime dependency direction. Without it, the scenes and
`.asset` files would have to be hand-written as raw YAML, which is fragile.

**Reversible:** deleting the folder loses only the ability to regenerate scaffolding.

---

## OI-11 — Enemy bodies do not collide with the player body

The SRS specifies the layer set implicitly but never states the collision matrix.

**Decision:** `Player` does **not** collide with `Enemy`. Contact damage is delivered through
`EnemyHitbox`, so solid enemy bodies would only shove the hero and let a crowd pin them against a
wall — a failure mode the *Readable Chaos* pillar (SRS 5) explicitly warns about. Full rationale
for every cell is in README section 9.

**Needs from design:** confirmation, especially for the Charger archetype (SRS 13), where a
physical body-check might be the intended feel.

---

## OI-12 — `git-lfs` is not installed on this machine

**SRS 25** asks for "Git + Git LFS nếu asset binary lớn".

**Status:** `.gitattributes` declares LFS filters for `png/psd/wav/mp3/fbx` and friends, but the
`git-lfs` binary is absent, so the filters do nothing. `git init` has been run; nothing is staged.

**Action required before the first art commit:**
```bash
sudo apt install git-lfs && git lfs install
```
Committing binaries first would store them as ordinary blobs and later require `git lfs migrate`.

---

## OI-13 — RunState is runtime-only, so an abandoned process loses the Run

**SAVE-006** forbids mid-run save and **SRS 23** marks `RunState` "Runtime-only: không persist ra
file ở MVP". **RUN-006** requires the currency haul to commit at Post-Run.

**Consequence, not a contradiction:** if the process is killed mid-Run (crash, alt-F4), the Run
and its collected Gold and Gems are lost, because they only ever existed in memory. This follows
from the SRS as written and is flagged only so it is not later reported as a bug. SAVE-007 marks
mid-run save as a post-MVP expansion.

---

## OI-14 — Scene count versus "multiple stages"

**SRS 27** lists a single `Run_01` scene, while **SRS 43 Q3** answers "nhiều Stage/room giúp cảm
giác tiến trình rõ hơn" and SRS 3.1 scopes MVP to one stage.

**Decision:** one `Run_01` scene, matching SRS 27 and the MVP scope. `StageData.SceneName` exists
so additional stages become data plus a scene, with no change to `StageManager`.

---

## OI-15 — NFR performance targets are not yet measurable

**NFR-001 to NFR-004** define FPS, frame time and load time budgets on a named Target Spec
(SRS 28.1).

**Status:** nothing to measure yet — the scenes are empty. The structural pieces that protect
these budgets are in place (`ObjectPool<T>`, AI think-throttling in `EnemyAI.ThinkInterval`,
spawn budget in `WaveData.SpawnBudget`, batched telemetry), but the numbers themselves cannot be
verified until P1/P2 content exists. Marked "chưa đo được" in `TRACEABILITY.md`.

---

## OI-16 — The short-hop formula in the spec cuts the jump the wrong way

The P1 slice 1 brief gives the released-jump gravity as `fallMultiplier x 0.5`. With the confirmed
values that is `1.6 x 0.5 = 0.8`, so gravity while rising with the key released would be
`40 x 0.8 = 32` — **less** than the 40 applied while the key is held. Releasing Space early would
make the hero jump *higher*, which is the opposite of a short hop.

**Decision:** use a separate `lowJumpMultiplier = 2.0`, giving gravity 80 while rising after
release. `MovementConfig.LowJumpMultiplier` carries `[Min(1f)]` so the inverted case cannot be
re-entered by editing the asset. Confirmed by the project owner on 2026-09-06.

`Test_Jump_PeakHeightInRange` pins the held-jump peak at `15.5^2 / (2 x 40) = 3.003u`; the
short hop is not yet covered by a test.

---

## OI-17 — Zero friction on the Hero collider is correct only while the world is flat

`PlayerMotor.Awake` assigns the hero collider a `PhysicsMaterial2D` with `friction = 0`. This is
not cosmetic: without it Unity applies contact friction *after* the motor writes
`Rigidbody2D.linearVelocity` in the same physics step, so the hero topped out at 6.68 u/s against a
configured `moveSpeed` of 7 — a bug that `Test_MoveRight_VelocityConvergesToMoveSpeed` caught.

**Correct for P1**, where every surface is flat and static.

**Breaks at P4**, when slopes and moving platforms arrive. With friction 0 the hero will slide down
any slope instead of standing on it, and will not be carried by a moving platform.

**What to do then:** do not simply turn friction back on — that reintroduces the speed loss above.
Separate the axes instead: keep horizontal velocity authored entirely by the motor, and add the
surface's own motion (slope normal, platform velocity) as a separate term the motor reads, rather
than letting the physics engine bleed it out of `linearVelocity`. Alternatively drive horizontal
motion through a `friction = 0` material but resolve slope support with an explicit ground-normal
projection in `PlayerMotor.ApplyHorizontal`.

---

## OI-18 — Hitbox timing uses frame data in seconds, not Animation Events

**SRS 21** and **COM-004** describe the attack hitbox being switched on and off by Animation
Events on the attack clips.

**P1 has no animation clips at all**, so there is no event to hang the toggle on. Building a
placeholder Animator only to carry two events would be more machinery than the thing it drives.

**Decision:** each combo step carries `activeStartTime` and `activeEndTime` in **seconds from the
start of the swing** (`AttackStep` in `Data/AttackData.cs`), and `PlayerCombat.FixedUpdate` opens
the hitbox inside that window. The observable behaviour is identical and the combo becomes
testable without an Animator, which is how `Test_Attack_HitboxOnlyActiveInWindow` can exist at
all. No `WaitForSeconds` and no `Invoke`: the elapsed time is accumulated from
`Time.fixedDeltaTime`, so it follows the physics clock and pauses when the game does.

`TODO(COM-004)` markers in `AttackData` and `PlayerCombat` record the migration. When real clips
arrive in P2 the frame data stays in the asset as the source of truth and the Animation Events
call into the same window, or the fields are read by the clip importer — either way the numbers
do not move into code.

---

## OI-19 — The single-damage-pipeline rule is guarded by a text scan, not by the compiler

**HPS-003** requires exactly one damage pipeline. `CombatSystem.DealDamage` is it: the death guard
(HPS-004), the i-frame guard (HPS-005), the crit roll and the `DamageAppliedEvent` all live there,
so a second path would silently skip all four.

**This cannot currently be enforced by the type system.** `CombatSystem` and `HealthComponent` are
in the same assembly, `ChibiRift.Gameplay`, so `internal` restricts nothing between them. A
capability token minted in `ChibiRift.Core` would not work either, because Core cannot reference
`ChibiRift.Gameplay` and so could never hand one to `CombatSystem`.

**Decision for P1:** `DamagePipelineSourceTests` scans every `.cs` under `Scripts/` and fails if
`ApplyDamage(` is called anywhere but `CombatSystem.cs`, or if `CurrentHealth` is written outside
`HealthComponent.cs`. Comments and string literals are stripped first. This was verified to fail
by injecting a second damage path deliberately.

**Be clear about its strength:** it is a check on source text. A determined caller can route
around it — through reflection, through a differently named wrapper, or by editing the allow-list
in the test.

**The real fix, deferred to P3:** move `HealthComponent` and `CombatSystem` into their own
assembly, `ChibiRift.Combat`, and make `IDamageable.ApplyDamage` internal to it. `ChibiRift.Gameplay`
would then reference `ChibiRift.Combat` and be unable to call `ApplyDamage` at all — a compile
error rather than a failing test. Not done now because it means moving `IDamageable` out of Core
and re-pointing every implementation, which is a larger change than this slice's scope allows.

---

## OI-20 — The hero moonwalks when the cursor and the movement direction disagree

From P1 slice 2 the sprite faces the **mouse cursor** (COM-009), not the direction of travel.
`PlayerCombat.SetAimTarget` is the only writer of `SpriteRenderer.flipX`; `PlayerMotor` stopped
writing it. Before this there were two writers and the sprite flickered whenever they disagreed.

**Consequence:** running left while the cursor is to the right shows the hero sliding backwards.

**This is intended, not a bug.** It is the standard behaviour for a mouse-aimed action game, and
COM-009 ("no auto-target") is the reason the cursor has to win: if facing followed velocity, the
hitbox and the sprite would point in different directions during any strafe.

**Needs a visual acceptance pass in slice 4**, once the attack animation exists and the effect is
actually visible. If it reads badly then, **plan B** is to make facing follow the cursor only
while attacking or holding aim, and follow velocity otherwise. That is a change inside
`PlayerCombat.SetAimTarget` alone; nothing else reads facing.

---

## OI-21 — Death suppresses the killing blow's own knockback

`HealthComponent.ApplyDamage` runs the death transition, which halts the enemy motor. Only then
does `CombatSystem` publish `DamageAppliedEvent`, which is what drives knockback. The push
therefore arrived **after** the halt and undid it: the corpse slid roughly 0.9 units away before
stopping. `Test_Enemy_DeathStopsAllAI` caught it.

**Decision:** `EnemyMotor` ignores a knockback aimed at a target that is already dead. AI-003 makes
Death terminal, and a corpse that keeps travelling is not terminal in any sense a player would
recognise.

**The alternative, deliberately not taken:** letting the killing blow throw the body is a common and
good-looking effect. It was rejected here because P1 slice 3 has no death animation, so a sliding
untextured rectangle reads as a bug rather than as impact. Worth revisiting once death animation
exists in P2 — at which point the fix is to gate on "has a death animation", not to remove the
guard.

---

## OI-22 — The banned-literal rule pushed twelve unlisted numbers into data

The slice 3 brief listed twenty-two enemy values and banned a set of literals from
`ChibiRift.Core` and `ChibiRift.Gameplay`. Writing the enemy needed twelve more numbers the brief
did not name: hitbox width, height and offset; gravity, terminal fall speed and the three ground
probe figures; the distance that counts as "home"; the length of an anti-stuck sidestep; and the
rate and floor alpha of the invulnerability flash.

Every one of them would have been a literal inside `ChibiRift.Gameplay`, which the audit correctly
refuses.

**Decision:** all twelve moved into `EnemyData` (via `EnemyAttackConfig` and the new
`EnemyPhysicsConfig`) or `BalanceConfig`, and all twelve are covered by
`DataDefaultsConsistencyTests`. Defaults are what would otherwise have been hard-coded, and mirror
the hero where the two should agree — enemy gravity and fall speed match `MovementConfig`, and the
enemy's reach matches the hero's chain, so trading blows at the edge of range is symmetrical.

**Worth noting for later slices:** the rule is doing real work. It is not a formatting preference;
it is what stopped an enemy's gravity from becoming a number only a programmer could find.

---

## OI-23 — Two shipped faults that 162 green tests could not see

Playtesting P1 slice 3 found the hero invisible and unable to jump, while the whole suite was
green. Both faults lived in `Hero.prefab`, and every existing test builds its actors in code.

**Fault 1 — the sprite was never saved.** The generators built their placeholder with
`new Texture2D` and `Sprite.Create` at edit time. Those are in-memory objects with no asset path. A
scene embeds such an object in its own `.unity` file, which is why boxes and dummies created
directly in the scene were visible; `PrefabUtility.SaveAsPrefabAsset` cannot serialise a reference
to something with no path, so **both** prefabs came out with `Sprite = None`. The hero and all
three enemies were invisible. Only the hero was reported, because the three visible red rectangles
at x = 3, 8 and 12 are the training dummies, not the enemies at x = 5, 10 and 15.

**Fault 2 — Transform scale.** The prefab carried scale (1, 2, 1) to stretch a 1x1 sprite to the
documented 64px height. That scaled the `CapsuleCollider2D` from 0.8 x 1.8 to 0.8 x 3.6 as well,
while `GroundCheckOffsetY` stayed in unscaled units, so the probe sat 0.9u inside the body.
`IsGrounded` was permanently false: Space did nothing, double jump was unreachable, and the combo
reset on leaving the ground could fire continuously.

**Fixes.** Placeholder art is now a real asset (`Art/Placeholder/px_white.png`, imported at the P1
constants: 32 PPU, Point, uncompressed, no mipmaps), tinted per renderer and sized through
`SpriteRenderer.size`. No generated object scales a Transform any more. Both ground probes also
multiply by `lossyScale`, so scaling an actor in future cannot silently reintroduce fault 2.

**The lesson worth keeping.** Every PlayMode fixture built its actors in code, which is right for
isolating logic but means none of them ever loaded the prefab that ships. `Run01SceneTests` now
plays the real scene, and `SceneActorVisualTests` checks in the editor that every actor draws
something and that nothing is scaled through its Transform. Both were verified by reintroducing the
original faults deliberately.

---

## OI-24 — Training dummies were walls

Dummies sit on the `Enemy` layer so the hero's attack sweep can find them. `Enemy` collides with
`Enemy`, so they were also solid bodies — and they sit at x = 3, 8 and 12, directly between the
hero's spawn and the enemies at x = 5, 10 and 15.

The enemy at x = 5 aggroed correctly, walked left, and wedged itself behind the dummy at x = 3. It
covered **0.6 units in six seconds**, which reads as "the enemy is not chasing".

**Decision:** dummy colliders are triggers. Attack sweeps include triggers, so a dummy still takes
hits; it just stops being terrain. A test prop should never be able to block pathing.

**Also fixed alongside it:** `EnemyAI.UpdateStuckDetection` accumulated its timer inside `TickChase`,
which runs at the think rate rather than the fixed rate, so the configured 0.5s stuck window
silently behaved as 2.5s. The timer now runs on the fixed clock in `TickTimers`. Even with that
corrected, an enemy whose only evasive move is a 0.35s sidestep cannot get around a solid obstacle,
which is why the obstacle had to stop existing.

---

## OI-25 — Two playtest reports that were not bugs, and why they looked like bugs

Alongside the two real faults above, the same playtest reported "Mouse Left does nothing" and "the
hero does not turn to face the cursor". Both systems were working. Measured in the real scene, with
the real `InputReader` driven by a simulated mouse: a click next to a dummy took it from 9999 to
9989 health, exactly the 10 damage the formula gives, and `AimDirection` tracked the cursor.

They looked broken because **nothing shows them**:

- The attack hitbox reaches about 1.4u. A click from further away is a clean miss, and a miss has
  no animation, no swing arc, no sound and no number. It is indistinguishable from dead input.
- Facing is `SpriteRenderer.flipX` on a **featureless solid rectangle**. Flipping it is a no-op to
  the eye. There is no art asymmetry for the flip to act on.

**Not "fixed", because nothing is broken.** What changed is that the F1 overlay now shows
`attackState`, `comboStep`, `aimDirection`, `enemyCount` and `nearestEnemyState`, so a swing that
merely missed can be told apart from input that never arrived.

**The real fix is art and juice**, and it is correctly scheduled: attack animation is P2, hit VFX
and sound are P2. Until then, expect combat to be legible only through the overlay and the damage
numbers.

---

## OI-26 — Three paths carry a value into the game; only two were watched

A number reaches the running game by one of three routes:

| # | Route | Watched by |
|---|---|---|
| 1 | A C# field initialiser in `ChibiRift.Data` | `DataDefaultsConsistencyTests` |
| 2 | `SampleDataGenerator` writing a ScriptableObject | the same tests, which diff the generator's actual output |
| 3 | **A serialized field on a prefab** | **nothing, until now** |

Route 3 is how `Hero.prefab` shipped with `_hurtIFrameDuration = 0`. Every part of route 1 and 2
was correct — `HeroData` carried the confirmed 0.8, the generator wrote it, the component
implemented HPS-005 properly — and the hero still had no post-hit invulnerability, because nothing
copied the value onto the prefab and nothing checked.

**`PrefabWiringTests` now covers route 3**, in three parts:

- Every numeric, boolean and object-reference field on `Hero.prefab` and `ENM_MeleeGrunt.prefab`
  must be non-zero and non-null, or appear in a declared exemption list with a written reason.
- Any field exempted as *"the scene assigns it"* is then checked on the actual instance in Run_01,
  so that promise cannot be made and quietly broken.
- Exemptions are checked against the current fields, so a renamed field cannot leave a stale entry
  silencing a real zero.

Verified by reintroducing both faults: setting `_hurtIFrameDuration` back to 0 and clearing
`_sceneContext` on the scene instance each fail with the offending field named.

**Four fields are legitimately empty** and are declared as such: `PlayerMotor._sceneContext` on the
prefab (a prefab cannot reference a scene object), `HealthComponent._sourceData` on the hero (that
is the enemy seeding path), and `_isPlayer` and `_hurtIFrameDuration` on the enemy (an enemy is not
the player, and enemies take hurt stun rather than invulnerability — i-frames would make them
immune for the rest of a combo and break COM-002).

---

## OI-27 — Two writers of a runtime property, three times running

The slice 4A brief specified a `TimeController` that would set `Time.timeScale` for hit stop.
`PauseManager` already documents itself as *"the only writer of `Time.timeScale` in the project"*,
and `VfxManager.RequestHitStop` already carried a TODO naming this exact hazard.

**The failure it would have caused:** the hero is hit, hit stop freezes time for 0.08s, the player
presses ESC inside that window. The freeze expires, sets the scale back to 1, and the game is
running again behind a pause menu that is still on screen.

That is the **third** time this project has hit the same shape of bug:

| Slice | Property | Two writers | Symptom |
|---|---|---|---|
| 2 | `SpriteRenderer.flipX` | `PlayerMotor` and `PlayerCombat` | Sprite flickered when cursor and movement disagreed (OI-20) |
| 4A | `Time.timeScale` | `PauseManager` and the specified `TimeController` | Pause silently lifted by an expiring freeze |
| 4A | `SpriteRenderer.color` | `HurtFlash` and the new hit flash | Whichever ran last won; they fire together by definition |

**Decision.** `PauseManager` stays the sole writer and gained `RequestHitStop`. Pause outranks hit
stop in both directions: a request made while paused is dropped, and a pause taken during a freeze
cancels it rather than queueing behind it. The freeze *length* is still chosen in
`Gameplay/Feel/HitStopService.cs`, because the durations live in `BalanceConfig` and Core cannot
reference Data. `HurtFlash` was merged into `Gameplay/Feel/SpriteFeedback.cs`, now the sole writer
of sprite colour.

**Rule adopted, written into README section 6:** one runtime property, one owner; everything else
sends a request. The README carries the current ownership table, updated in the same commit that
changes an owner. Three occurrences is a pattern, not an accident.

---

## OI-28 — Hit stop deadlocks any test that waits on a physics step

`FixedUpdate` does not run while `Time.timeScale` is zero, so `yield return new WaitForFixedUpdate()`
never returns during a freeze. Every PlayMode fixture waits that way, and from slice 4A **any**
landed hit freezes time — including one an enemy lands on the hero in the middle of a test about
something else entirely.

Found the hard way: the first run of the slice 4A suite hung indefinitely inside
`Test_Crit_AppliesMultiplier`, which deals damage and then waits two steps.

**Fixes, both kept:**

- The shared `Steps` helper waits out a freeze on frames before each fixed step, so it cannot
  deadlock. Frames advance at a zero time scale; physics steps do not.
- Tests that deliberately freeze time wait in real seconds instead, and the fixture forces
  `Time.timeScale = 1` both before a test boots and before it tears the composition root down —
  destroying the bootstrap mid-freeze would otherwise strand the scale at zero with nothing alive
  left to restore it.

**Worth remembering for later slices:** anything that can stop time turns every scaled wait in the
test suite into a potential hang, and a hang looks nothing like a failure — the run simply never
finishes.

**Hardened afterwards into two guards that apply to every PlayMode fixture:**

| Guard | Fires when | Verified by |
|---|---|---|
| `TestTime.Steps` throws `TimeoutException` | Time stays frozen past 2 real seconds | Leaking a freeze deliberately: fails in 3.5s with the message naming the cause |
| `[Timeout(20000)]` on all four fixtures | A test runs past 20s for any other reason | An outright `while (true) yield return null`: failed at exactly 20.01s |

The waiting primitives moved into one shared `TestTime` class and all four fixtures delegate to it,
so the guard cannot be present in some and missing from others. Twenty-nine bare
`WaitForFixedUpdate` calls across the four fixtures were replaced. 20000ms is three times the
slowest test measured (6.40s); raise it in the same commit that makes a test legitimately slower.

---

## OI-29 — The first exception to "gameplay never reads a device", and the fence around it

Since P1 slice 1 the rule has been that input reaches gameplay only through `IInputService`, backed
by the bound action map in `ChibiRiftControls.inputactions`. That is what makes rebinding a data
change and the control scheme testable, and `InputActionsAssetTests` checks the bindings directly.

**The exception.** Development tooling — the F1 overlay, and the F2/F3/F4 spawn keys coming in
slice 4B — should not appear in the shipping control scheme at all. Putting them in the action map
would mean a released build carries bindings for keys that do nothing. So they read
`Keyboard.current` directly, and only inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, which means
the code cannot exist in a player build.

**Found while fencing it:** `DebugOverlay` had already been reading `Keyboard.current` **unguarded**
since slice 1. The precedent was sitting there before anyone decided to make one. It is now guarded
at the method body rather than the whole class, so the component still exists in a build and the
scene reference to it does not break.

**Why this is a test and not this paragraph.** A first exception is harmless. The danger is the
second one, argued for on the grounds that the first exists — and a note in this file stops nobody.
`DeviceInputSourceTests` scans every runtime assembly (`Core`, `Data`, `Gameplay`, `Meta`, `Save`,
`Telemetry`, `UI`) for `Keyboard.current`, `Mouse.current` and `Gamepad.current`, tracks
preprocessor nesting including `#elif` and `#else`, and fails on any access that is not inside a
development guard. The failure message states the rule and names the permitted exceptions.

**A hole found in the test itself while proving it.** The first version used a lookbehind that
excluded a preceding dot, so a fully qualified `UnityEngine.InputSystem.Keyboard.current` slipped
straight through — which is exactly how someone adding a second exception without a `using` would
write it. Verified by injecting both forms: the guarded-and-then-unguarded `DebugOverlay`, and a
fully qualified access in `PlayerController`. Both are caught now.

**Limit worth knowing.** The scan is textual, like `DamagePipelineSourceTests` (OI-19). Reflection,
an alias, or a wrapper class would route around it.

---

## OI-30 — Pooled objects must not be parented to an actor

Found by `PrefabWiringTests` complaining that `ProjectileSkill._container` was unset. The default
would have been the component's own transform — which sits on the hero.

A projectile advances by reading its own world position each step and adding a step of travel. Made
a child of the hero, it inherits the hero's movement as well, so a shot fired while running drifts
with the shooter. `DashTrail` had the same arrangement from slice 4A: afterimages parented to the
hero travel with the hero, which defeats the one thing an afterimage is for.

**Decision.** Anything pooled that lives in world space is parented to a scene-level
`PooledObjects` root. Both components now fail loudly rather than defaulting to their own transform,
and the scene assigns the root on the instance, the way `PlayerMotor._sceneContext` already worked.

**Worth noting:** neither the trail nor the projectile was *visibly* broken in a way that would have
been reported as a bug. The trail simply looked wrong, and a projectile fired standing still behaved
correctly. The guard found it, not a playtest.

---

## OI-31 — Where P1 draws the line on the NFR measurements

`FrameTimeHarness` exists and produces the full distribution, but **P1 does not claim NFR-001 or
NFR-002 are met.**

- A batch-mode run has no renderer, so its frame times are not a player's frame times. The test
  asserts the report is complete and deliberately asserts no threshold; a pass here says the
  instrument works, not that the game is fast.
- The harness casts no skill. The ultimate's cooldown is 15s and the sample window is 10s, so
  including one would make each run depend on whether a cast landed inside the window. The p99
  therefore **excludes the cost of an area skill sweeping its 3.5u radius**, and that sentence is
  written into the report JSON so the numbers cannot be read without it.

**What closing these requires:** an editor run on the target machine, compared against the table in
README section 14. Until then both stay open, superseding the "nothing to profile yet" state
recorded in OI-15.

---

## OI-32 — Investigating a real allocation report: two harness bugs, one broken API, and an
unattributed remainder

The project owner ran the frame-time harness and reported `AllocatedKilobytesDelta = 12848 KB`
over `DurationSeconds = 6.08s` with 30 enemies — roughly 2 MB/s, and `DurationSeconds` not
matching the configured 10s. Investigating it found three real, fixed bugs and one honestly
unresolved finding.

**Bug 1 — the sample silently truncated at ~6s instead of 10s.** The sample buffer was a
fixed-size array sized from `duration * MaxPlausibleFps` with `MaxPlausibleFps = 2000`, treated as
a generous bound. A headless batch-mode run with no renderer runs far faster than that — the fixed
harness measured 34,000-41,000 frames in 10s, 1700-2000+ FPS — so the array filled and the loop
exited on `frames < samples.Length` failing, long before `elapsed` reached the configured
duration. **Fixed:** the sample buffer is a `List<float>` with no upper bound (only a defensive
`SafetyFrameCap` of 1,000,000 frames, purely against a runaway loop); a `StoppedBySafetyCap` field
on the report makes a future truncation impossible to miss silently, and
`Test_Profiler_HarnessProducesCompleteReport` asserts it is false.

**Bug 2 — `GC.GetAllocatedBytesForCurrentThread` is not implemented in this project's Unity/Mono
runtime.** The first fix attempt (before this entry) switched the measurement from
`GC.GetTotalMemory` to this API, on the textbook-correct reasoning that allocated-bytes cannot be
hidden by a mid-window collection the way a live-heap-size delta can. **Verified, not assumed:** a
direct test that allocated and held 10MB of real arrays between two calls measured a delta of
exactly zero from this API, and zero again for a single boxed `int`. `GC.GetTotalMemory` and
`UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong` both registered the same 10MB correctly in
the same test. **Fixed:** reverted to `GC.GetTotalMemory`, paired with `GC.CollectionCount(0)`
captured before and after — a `GcCollectionsDuringSample` field on the report says whether the
figure is exact (zero collections) or a floor (one or more; undercounts by whatever they
reclaimed). `AllocationProfiler`'s many small per-call-site samples use the same pairing and drop
(rather than add) any individual sample a collection landed inside, since losing one of thousands
costs nothing and keeping a known-wrong number is worse.

**A hypothesis tested and disproven: the F1 debug overlay was not the cause, at least not the way
it looked.** `DebugOverlay` is visible by default (`_visibleOnStart = true`) and draws ten
`GUILayout.Label` calls with string-interpolation format specifiers every `OnGUI` — a plausible-
looking source, and one with real problems independent of this investigation: it was completely
unguarded (`EnemyDebugCensus`, its enemy-count feed, was too), meaning both ran in every session
including a Release build, with nothing enforcing the "development only" their own doc comments
claimed. Both are now gated behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, matching the
`DebugSpawner` precedent from OI-29, and the frame-time harness additionally disables both for the
duration of its own run regardless of build symbols (restoring prior state after), since a
diagnostic overlay is not the game being measured.

That said: **`OnGUI` does not dispatch at all in `-nographics` batch mode** — verified with a
guaranteed `Debug.Log` placed inside it, which never printed across a full run. And when the exact
string-interpolation expressions from the overlay's ten label lines were run 10,000 times in
isolation (outside `OnGUI`, with a genuine data-dependent sink to defeat dead-code elimination),
they allocated zero bytes — this Unity/Roslyn version does not box interpolated strings with
alignment and format specifiers the way older versions did. Both halves of the original hypothesis
are therefore individually disproven for headless testing: the code that runs, doesn't allocate;
the code that might allocate (`GUILayout`'s own internal bookkeeping, independent of the strings
passed to it), doesn't run here. Whether that IMGUI-internal cost is real can only be checked in an
interactive Editor session with an actual Game view, which is almost certainly how the original
12848 KB figure was produced and is not an environment available to this investigation.

**What the instrumented gameplay code actually costs: negligible.** `AllocationProfiler` (new,
`ChibiRift.Core`, active only when a caller sets `Recording = true`) was wired into every
`Update`/`FixedUpdate` with a plausible claim to the total: `EnemyAI`, `EnemyMotor`,
`EnemyAttack`, `PlayerMotor`, `PlayerCombat`, `DashTrail`, `HealthComponent`, `SkillSystem`,
`EnemyDebugCensus`, and — cast the widest net available, since `EventBus.Publish` invokes every
subscriber synchronously on the same call stack — `CombatSystem.DealDamage`, which captures the
entire reaction cascade of a landed hit (hit stop, screen shake, damage numbers, SFX, particles,
knockback) in one label. Across three repeated 10-second/30-enemy runs, every one of these summed
to under 60 KB combined, against totals of 2304-3456 KB. Even bracketing whole frames around an
active hit-stop coroutine's iterations — as broad a net as a script-level bracket can cast — added
only ~56 KB.

**The honest conclusion: the remainder is not attributable to any of ChibiRift's own code, and it
is not clustering either — that part was tested, not just inferred (see the A/B experiment below).**
Logging the cumulative delta every 2000 frames showed a flat, near-zero rate for the first ~3.5
seconds, then a sustained ramp of roughly 115-165 bytes/frame for the rest of the run. The timing —
lining up with when enemies spawned across an 8-unit radius would first walk into melee range and
cluster around the hero, `(8 - 1.2) / 3 ≈ 2.3s` plus windup — first pointed at Physics2D's own
contact-generation bookkeeping for overlapping, separating bodies. The A/B experiment below rules
that out directly: enemies that never move and never overlap allocate the same amount as enemies
that chase and cluster. The remaining honest attribution is coarser than that: something scoped to
having 30 active enemy instances running `FixedUpdate`/`Update` each frame, independent of what
they do in it. `EnemyMotor`'s separation force and the Enemy-vs-Enemy collision matrix entry stay
untouched — both are documented, intentional choices from earlier slices (crowd readability, SRS
5), and this finding gives no reason to revisit either.

**Regression guard.** `BalanceConfig.StressAllocationBudgetKilobytes` (5000 KB) and
`Test_Profiler_AllocationStaysUnderBudget` exist so a genuinely new allocator — a P2 wave spawner
or elite modifier that allocates per tick — is caught immediately rather than discovered as a bad
p99 later. Set to ~1.4x the top of the measured range: wide enough to absorb run-to-run noise,
narrow enough that a system tripling the allocation rate cannot hide inside it. An earlier cut of
this guard used 8192 KB (~2.4x of the range known at the time), on the reasoning that the budget
only needed to be "generous"; tightened once the point of the guard was reframed as catching a P2
regression, not merely proving the harness runs (README section 8 — raise this only in the commit
that legitimately needs it, with the new range in the message). The measured range itself widened
from 2304-3456 KB to 1700-3640 KB once the clustering A/B experiment below added three more
single-condition runs; 5000 KB still clears the new top by the same ~1.4x, so the number did not
need to move again. Verified by temporarily lowering it below the measured range and confirming the
test fails with the actual figure named.

**Follow-up experiment: is it really Physics2D contact generation? No.** The clustering hypothesis
above was inference by elimination, not a measurement, so it was tested directly: spawn the same 30
enemies either (A) scattered across the full arena with aggro forced off (`EnemyAI.Target = null`,
so `DistanceToTarget()` reads `float.MaxValue` and `TickIdle` never crosses `DetectionRange` no
matter how close a spawn point lands to the hero — never moving, never overlapping each other), or
(B) spawned inside the hero's `DebugSpawnRadius` with aggro on, exactly like the harness does today
(closing in, clustering, attacking). Both conditions got their own fresh boot rather than running
back to back in one — an early combined version ran A then B in the same boot and measured B at
~12880 KB, nearly 4x every previously observed figure for that exact scenario; that gap tracked
with which stress phase ran second in the same process, not with which condition was which, so it
was an order effect from two heavy phases sharing one heap history, not a physics answer, and was
discarded.

Three independent, freshly-booted A/B pairs:

| Run | A — scattered, no aggro | B — clustered, aggro |
|---|---|---|
| 1 | 2984 KB | 1740 KB |
| 2 | 3016 KB | 2492 KB |
| 3 | 3640 KB | 2872 KB |

A is not lower than B in any run — if anything it runs slightly ahead. Enemies that stand still,
never touch each other, and never touch the hero allocate just as much as enemies that chase, pile
up, and land hits. **Physics2D contact generation for overlapping bodies is not the cause.** The
remaining honest attribution is coarser, as stated above: something proportional to having 30
active enemy instances ticking each frame, present whether they are idle or in combat. This is
diagnostic only — nothing here changes shipped behaviour, `EnemyMotor`'s separation force and the
Enemy-vs-Enemy collision matrix entry are untouched — but it sharpens what P2 should expect:
concurrent enemy *count* is the driver, not their behaviour state, which if anything makes NFR-001's
30-enemy cap a more load-bearing ceiling than the clustering theory implied, not less — a wave or
elite system adding bodies costs roughly this regardless of whether those bodies are aggroed.
