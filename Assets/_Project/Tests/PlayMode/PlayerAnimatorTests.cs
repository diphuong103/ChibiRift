using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChibiRift.Core;
using ChibiRift.Data;
using ChibiRift.Gameplay;

namespace ChibiRift.Tests.Play
{
    /// <summary>
    /// A3/A4 (P2 slice 1): the Animator layer must only display gameplay state, never decide it,
    /// and the Animation-Event hitbox toggle must fall back cleanly when a clip has none (COM-004).
    /// </summary>
    /// <remarks>
    /// Same approach as <see cref="PlayerCombatTests"/>: the hero is built in code, private fields
    /// are set by reflection so this assembly needs no UnityEditor reference.
    /// </remarks>
    [Timeout(20000)]
    public sealed class PlayerAnimatorTests
    {
        private const float SpawnY = 1f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private ServiceLocator _locator;
        private EventBus _eventBus;
        private HeroData _hero;
        private AttackData _attack;
        private BalanceConfig _balance;
        private PlayerCombat _combat;

        [SetUp]
        public void SetUp()
        {
            _locator = new ServiceLocator();
            _eventBus = new EventBus();

            _balance = ScriptableObject.CreateInstance<BalanceConfig>();
            _hero = ScriptableObject.CreateInstance<HeroData>();
            _attack = ScriptableObject.CreateInstance<AttackData>();
            SetField(_attack, "_steps", AttackData.BaselineSteps());
            SetField(_hero, "_basicAttack", _attack);

            _locator.Register(_eventBus);
            _locator.Register(new CombatSystem(_eventBus, _balance, new DeterministicRandom(1)));
            ServiceLocator.SetCurrent(_locator);

            Track(CreateSolid("Ground", new Vector2(0f, -0.5f), new Vector2(40f, 1f), GameLayers.Ground));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null) Object.DestroyImmediate(spawned);
            }
            _spawned.Clear();

            if (_hero != null) Object.DestroyImmediate(_hero);
            if (_attack != null) Object.DestroyImmediate(_attack);
            if (_balance != null) Object.DestroyImmediate(_balance);

            _locator.Clear();
            ServiceLocator.ClearCurrent();
        }

        // ----- A3: the Animator layer is read-only ------------------------------------------

        /// <summary>
        /// Text-scan, in the spirit of <c>DamagePipelineSourceTests</c> (OI-19): the type system
        /// cannot express "this file may only read gameplay state", so this checks the source
        /// directly rather than trying to exercise every path through a live Animator, which this
        /// headless environment cannot render or drive meaningfully anyway.
        /// </summary>
        [Test]
        public void Test_Animator_DoesNotMutateGameplayState()
        {
            string driverPath = FindScriptFile("PlayerAnimatorDriver.cs");
            string driverSource = StripCommentsAndStrings(File.ReadAllText(driverPath));

            AssertNoWordBoundaryMatch(driverSource, driverPath, "PlayerMotor");
            AssertNoWordBoundaryMatch(driverSource, driverPath, "PlayerCombat");
            AssertNoWordBoundaryMatch(driverSource, driverPath, "HealthComponent");
            AssertNoGameplayMutation(driverSource, driverPath);

            string combatPath = FindScriptFile("PlayerCombat.cs");
            string combatSource = File.ReadAllText(combatPath);

            string startBody = ExtractMethodBody(combatSource, "OnAttackActiveStart");
            string endBody = ExtractMethodBody(combatSource, "OnAttackActiveEnd");

            Assert.That(startBody, Is.Not.Null, $"OnAttackActiveStart not found in {combatPath}.");
            Assert.That(endBody, Is.Not.Null, $"OnAttackActiveEnd not found in {combatPath}.");

            AssertNoGameplayMutation(
                StripCommentsAndStrings(startBody), $"{combatPath}::OnAttackActiveStart");
            AssertNoGameplayMutation(
                StripCommentsAndStrings(endBody), $"{combatPath}::OnAttackActiveEnd");
        }

        // ----- A4: Animation Event with a graceful fallback (COM-004) -----------------------

        [UnityTest]
        public IEnumerator Test_AttackEvent_FallsBackToTimerWhenClipHasNoEvent()
        {
            // A real clip is wired (unlike PlayerCombatTests' empty _attackClips, which is the
            // "no art at all yet" case) but nobody added the two hitbox events to it — the exact
            // mistake the brief named: a spritesheet swapped in without re-running the builder.
            var clipMissingEvents = new AnimationClip { name = "Attack1" };
            BuildHero(new[] { clipMissingEvents, null, null });

            // Settle on the ground first: PlayerCombat.Update drops the combo the instant it sees
            // the hero leave the ground (COM-003), which a hero spawned mid-air and still falling
            // reads as "just left the ground" on its very first frame.
            yield return TestTime.Steps(5);

            LogAssert.Expect(LogType.Warning, new Regex(
                "Attack1 has no OnAttackActiveStart/OnAttackActiveEnd"));

            AttackStep step = _attack.GetStep(0);
            _combat.RequestAttack();

            var samples = new List<(float Elapsed, bool Active)>();
            for (float t = 0f; t <= step.TotalDuration; t += Time.fixedDeltaTime)
            {
                yield return TestTime.Steps(1);
                samples.Add((t + Time.fixedDeltaTime, _combat.IsHitboxActive));
            }

            float slack = Time.fixedDeltaTime;
            foreach ((float elapsed, bool active) in samples)
            {
                if (elapsed < step.ActiveStartTime - slack)
                {
                    Assert.That(active, Is.False,
                        $"Hitbox was active at {elapsed:F3}s, before the fallback window opens at " +
                        $"{step.ActiveStartTime}s.");
                }
                else if (elapsed > step.ActiveEndTime + slack)
                {
                    Assert.That(active, Is.False,
                        $"Hitbox was still active at {elapsed:F3}s, after the fallback window " +
                        $"closes at {step.ActiveEndTime}s.");
                }
            }

            Assert.That(samples.Exists(s => s.Active), Is.True,
                "The hitbox never opened at all under the fallback timer, so the window assertions " +
                "proved nothing.");
        }

        // ----- helpers -------------------------------------------------------------------------

        private void BuildHero(AnimationClip[] attackClips)
        {
            GameObject heroObject = Track(new GameObject("Hero")
            {
                layer = LayerMask.NameToLayer(GameLayers.Player)
            });
            heroObject.transform.position = new Vector3(0f, SpawnY, 0f);

            // Inactive until every field is set: adding a component to an active object fires
            // Awake immediately, before _attackClips below would exist.
            heroObject.SetActive(false);

            var renderer = heroObject.AddComponent<SpriteRenderer>();

            var capsule = heroObject.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.8f, 1.8f);

            var motor = heroObject.AddComponent<PlayerMotor>();
            SetField(motor, "_heroData", _hero);

            var health = heroObject.AddComponent<HealthComponent>();
            SetField(health, "_isPlayer", true);
            SetField(health, "_bodyCollider", capsule);

            var stats = heroObject.AddComponent<PlayerStats>();
            SetField(stats, "_heroData", _hero);
            SetField(stats, "_balanceConfig", _balance);

            _combat = heroObject.AddComponent<PlayerCombat>();
            SetField(_combat, "_heroData", _hero);
            SetField(_combat, "_spriteRenderer", renderer);
            SetField(_combat, "_attackClips", attackClips);

            heroObject.SetActive(true);
        }

        private static string FindScriptFile(string fileName)
        {
            string[] matches = Directory.GetFiles("Assets/_Project/Scripts", fileName, SearchOption.AllDirectories);
            Assert.That(matches, Has.Length.EqualTo(1),
                $"Expected exactly one {fileName} under Assets/_Project/Scripts, found {matches.Length}.");
            return matches[0].Replace('\\', '/');
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            var match = Regex.Match(
                source,
                $@"\b{Regex.Escape(methodName)}\s*\([^)]*\)\s*\{{(.*?)\n        \}}",
                RegexOptions.Singleline);

            return match.Success ? match.Groups[1].Value : null;
        }

        private static void AssertNoWordBoundaryMatch(string strippedSource, string label, string word)
        {
            bool found = Regex.IsMatch(strippedSource, $@"\b{word}\b");
            Assert.That(found, Is.False,
                $"{label} references {word} directly — the Animator layer should only read " +
                "EventBus events, never hold a reference to another gameplay component.");
        }

        private static void AssertNoGameplayMutation(string strippedSource, string label)
        {
            (string Pattern, string Description)[] banned =
            {
                (@"\.\s*CurrentHealth\s*(=[^=]|[-+*/]=)", "writes .CurrentHealth"),
                (@"(?<![.\w])CurrentHealth\s*(=[^=]|[-+*/]=)", "writes CurrentHealth"),
                (@"\btransform\s*\.\s*position\s*=", "writes transform.position"),
                (@"\bInitialize\s*\(", "calls HealthComponent.Initialize"),
                (@"\bApplyDamage\s*\(", "calls ApplyDamage"),
                (@"\bState\s*=[^=]", "assigns an FSM State field")
            };

            foreach ((string pattern, string description) in banned)
            {
                bool found = Regex.IsMatch(strippedSource, pattern);
                Assert.That(found, Is.False,
                    $"{label} {description} — the Animator layer must only read gameplay state, " +
                    "never write it.");
            }
        }

        /// <summary>Removes comments and string literals, matching DamagePipelineSourceTests (OI-19).</summary>
        private static string StripCommentsAndStrings(string source)
        {
            source = Regex.Replace(
                source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"//.*", string.Empty);
            source = Regex.Replace(source, "\"(?:[^\"\\\\]|\\\\.)*\"", "\"\"");
            return source;
        }

        private GameObject Track(GameObject spawned)
        {
            _spawned.Add(spawned);
            return spawned;
        }

        private static GameObject CreateSolid(string name, Vector2 centre, Vector2 size, string layer)
        {
            var solid = new GameObject(name) { layer = LayerMask.NameToLayer(layer) };
            solid.transform.position = centre;
            solid.AddComponent<BoxCollider2D>().size = size;
            return solid;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
