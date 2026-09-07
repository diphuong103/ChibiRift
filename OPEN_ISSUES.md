# Open Issues

Ambiguities, gaps and contradictions found while reading
`SRS_2D_Action_Roguelite_RPG_Unity_v1.1.docx`, together with the decision taken so the
skeleton could be built. **No new requirements were invented.** Where the SRS is silent, the
value chosen is marked *unconfirmed* and is a designer decision to confirm, not a fact.

> **Status 2026-09-05:** OI-01 to OI-05 are **closed** — the project owner confirmed the five
> outstanding balance values and they are applied to the assets. OI-06 to OI-15 remain open.
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
