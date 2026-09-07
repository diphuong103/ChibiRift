# Open Issues

Ambiguities, gaps and contradictions found while reading
`SRS_2D_Action_Roguelite_RPG_Unity_v1.1.docx`, together with the provisional decision taken so the
skeleton could be built. **No new requirements were invented.** Where the SRS is silent, the
value chosen is marked *provisional* and is a designer decision to confirm, not a fact.

Every provisional number lives in a ScriptableObject, so confirming or changing it is a data edit,
not a code change.

---

## OI-01 — Critical multiplier is given as a range, not a value

**SRS 35** lists `Crit Multiplier | 1.5x–2.0x`. A single number is needed to seed
`BalanceConfig` and `StatBlock`.

**Decision:** take **1.5**, the lower bound, as the baseline. Stored in
`BalanceConfig.DefaultCritMultiplier` and `StatBlock.PlayerBaseline.CritMultiplier`.

**Needs from design:** either a single baseline value, or confirmation that the range means
"per-hero" / "per-upgrade" variation, in which case the range belongs in `HeroData` rather than in
the global config.

---

## OI-02 — XP curve parameters have no numbers

**SRS 10** gives the shape `XPRequired(level) = BaseXP × GrowthFactor^(level-1)`, and **SRS 35**
lists `XP Growth | Configurable` with no figures.

**Decision:** provisional `BaseXP = 100`, `GrowthFactor = 1.15`, plus a `MaxHeroLevel = 50`
ceiling that the SRS does not mention but which is needed to stop `LevelForTotalExperience` from
looping without bound. All three are in `BalanceConfig`.

**Needs from design:** real values, and confirmation that a level cap is wanted at all.

---

## OI-03 — Dash distance and duration are unspecified

**SRS 35** gives `Dash Cooldown 1.5s` and `Dash I-Frame Duration 0.25s`, and **HER-006** requires
`distance, duration, i-frame, cooldown`. Distance and duration have no baseline anywhere.

**Decision:** provisional `Distance = 4` world units, `Duration = 0.18s`, in
`DashConfig.Baseline`. The two SRS values are copied verbatim.

**Needs from design:** dash distance and duration, ideally after the P1 movement prototype exists,
since these are feel values.

---

## OI-04 — Combo window has no baseline

**COM-003** requires the combo to reset after a timeout; **SRS 35** lists
`Combo Window | Configurable` with no number.

**Decision:** provisional `0.5s`, in `BalanceConfig.DefaultComboWindow` and
`HeroData.ComboWindow` (per-hero override).

---

## OI-05 — Post-hit i-frame duration is unspecified

**HPS-005** requires invulnerability frames in **two** situations: after taking a hit, and during
the dash window. **SRS 35** only gives a duration for the dash case (0.25s).

**Decision:** provisional `HeroData.HurtIFrameDuration = 0.5s`, kept as a separate field from
`DashConfig.IFrameDuration` because the two serve different purposes and should be tunable apart.

**Needs from design:** the post-hit value, and whether the two windows can overlap or the longer
one wins.

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
