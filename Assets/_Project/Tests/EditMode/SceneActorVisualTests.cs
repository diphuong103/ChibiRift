using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Checks the things about Run_01 that only a human looking at the Game view would otherwise
    /// catch: that every actor actually draws, and that nothing is scaled through its Transform.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists.</b> 162 tests were green while the hero and all three enemies were
    /// invisible and the hero could not jump. Both faults came from the same place and neither was
    /// reachable by the existing suites: the logic tests build their actors in code, with a default
    /// Transform and a sprite they never look at, so they never touch the prefab that ships in the
    /// scene.</para>
    ///
    /// <list type="number">
    ///   <item><b>Missing sprite.</b> Placeholder sprites used to be built in memory with
    ///   <c>Sprite.Create</c>. A scene embeds such an object in its own file, so objects created
    ///   directly in the scene looked fine — but a prefab cannot serialise a reference to something
    ///   with no asset path, so every prefab came out with Sprite = None.</item>
    ///
    ///   <item><b>Transform scale.</b> The hero prefab was left at scale (1, 2, 1) to stretch a
    ///   1x1 sprite. That scaled the capsule to 0.8 x 3.6 as well, while the ground probe offset
    ///   stayed in unscaled units — so the probe sat 0.9u inside the body, <c>IsGrounded</c> was
    ///   permanently false and Space did nothing.</item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public sealed class SceneActorVisualTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Run_01.unity";

        /// <summary>
        /// Objects expected to be visible. Named rather than discovered so a renamed or deleted
        /// actor fails loudly instead of quietly shrinking the test.
        /// </summary>
        private static readonly string[] ExpectedActors =
        {
            "Hero",
            "Enemy_1", "Enemy_2", "Enemy_3",
            "Dummy_A", "Dummy_B_Fragile", "Dummy_C"
        };

        private Scene _scene;

        [SetUp]
        public void OpenScene() => _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        [TearDown]
        public void CloseScene()
        {
            if (_scene.IsValid()) EditorSceneManager.CloseScene(_scene, true);
        }

        [Test]
        public void EveryActorIsPresentInTheScene()
        {
            var missing = new List<string>();

            foreach (string name in ExpectedActors)
            {
                if (Find(name) == null) missing.Add(name);
            }

            Assert.That(missing, Is.Empty,
                "Run_01 is missing actors:\n  " + string.Join("\n  ", missing) +
                "\nRe-run ChibiRift > Setup > Build Run_01 Arena.");
        }

        [Test]
        public void EveryActorDrawsSomething()
        {
            var offenders = new List<string>();

            foreach (string name in ExpectedActors)
            {
                GameObject actor = Find(name);
                if (actor == null) continue;

                var renderer = actor.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    offenders.Add($"{name}: no SpriteRenderer");
                    continue;
                }

                if (renderer.sprite == null)
                {
                    offenders.Add($"{name}: SpriteRenderer.sprite is None");
                    continue;
                }

                // A sprite built in memory has no asset path. That is exactly the state that
                // survives a scene save but is dropped on a prefab save.
                string path = AssetDatabase.GetAssetPath(renderer.sprite);
                if (string.IsNullOrEmpty(path))
                {
                    offenders.Add($"{name}: sprite '{renderer.sprite.name}' is not an asset on disk");
                }
            }

            Assert.That(offenders, Is.Empty,
                "Actors in Run_01 that render nothing:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void NoActorIsScaledThroughItsTransform()
        {
            var offenders = new List<string>();

            foreach (string name in ExpectedActors)
            {
                GameObject actor = Find(name);
                if (actor == null) continue;

                Vector3 scale = actor.transform.lossyScale;
                bool unscaled =
                    Mathf.Approximately(scale.x, 1f) &&
                    Mathf.Approximately(scale.y, 1f) &&
                    Mathf.Approximately(scale.z, 1f);

                if (!unscaled) offenders.Add($"{name}: lossyScale {scale}");
            }

            Assert.That(offenders, Is.Empty,
                "Actors scaled through their Transform:\n  " + string.Join("\n  ", offenders) +
                "\nTransform scale also scales the collider, while probe and hitbox offsets stay " +
                "in unscaled units. Size the sprite with SpriteRenderer.size instead.");
        }

        [Test]
        public void HeroGroundProbeSitsAtTheBottomOfItsCollider()
        {
            GameObject hero = Find("Hero");
            Assert.That(hero, Is.Not.Null, "Run_01 has no Hero.");

            var collider = hero.GetComponent<CapsuleCollider2D>();
            Assert.That(collider, Is.Not.Null, "Hero has no CapsuleCollider2D.");

            var motor = hero.GetComponent<ChibiRift.Gameplay.PlayerMotor>();
            Assert.That(motor, Is.Not.Null, "Hero has no PlayerMotor.");

            var heroData = SerializedFieldValue<ChibiRift.Data.HeroData>(motor, "_heroData");
            Assert.That(heroData, Is.Not.Null, "PlayerMotor has no HeroData assigned.");

            float probeY = heroData.Movement.GroundCheckOffsetY;
            float colliderBottom = collider.offset.y - collider.size.y / 2f;

            // The probe is a thin box centred on probeY; it has to straddle the collider's feet or
            // it can never see the ground the collider is resting on. Half the probe height of
            // tolerance is the widest that still guarantees an overlap.
            float tolerance = heroData.Movement.GroundCheckHeight;

            Assert.That(probeY, Is.EqualTo(colliderBottom).Within(tolerance),
                $"Ground probe sits at y={probeY} but the collider's feet are at y={colliderBottom}. " +
                "IsGrounded will be false while the hero is standing on the floor.");
        }

        private GameObject Find(string name)
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }

        private static T SerializedFieldValue<T>(Object target, string fieldName) where T : Object
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);

            Assert.That(property, Is.Not.Null, $"{target.GetType().Name} has no field '{fieldName}'.");
            return property.objectReferenceValue as T;
        }
    }
}
