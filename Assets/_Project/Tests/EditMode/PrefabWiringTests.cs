using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Guards the third path a value can take into the running game: a serialized field on a prefab.
    /// </summary>
    /// <remarks>
    /// <para>Three routes feed runtime values, and until now only two were watched:</para>
    /// <list type="number">
    ///   <item>A C# field initialiser in <c>ChibiRift.Data</c> — watched by
    ///   <see cref="DataDefaultsConsistencyTests"/>.</item>
    ///   <item><c>SampleDataGenerator</c> writing a ScriptableObject — watched by the same tests,
    ///   since they diff what the generator actually produces.</item>
    ///   <item><b>A serialized field on a prefab</b> — watched by nothing, which is how
    ///   <c>Hero.prefab</c> shipped with <c>_hurtIFrameDuration = 0</c>. The component supported
    ///   post-hit invulnerability, the asset carried the confirmed 0.8, and every test passed,
    ///   because no test ever looked at the prefab (OI-23, OI-26).</item>
    /// </list>
    ///
    /// <para>A zero or a null on a prefab is not automatically wrong — several are deliberate. The
    /// rule enforced here is that each one is <b>declared</b>, with a reason, in
    /// <see cref="Exemptions"/>. Anything else fails.</para>
    /// </remarks>
    [TestFixture]
    public sealed class PrefabWiringTests
    {
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const string RunScenePath = "Assets/_Project/Scenes/Run_01.unity";

        /// <summary>Prefabs whose wiring is checked. Add a prefab here when it gains behaviour.</summary>
        private static readonly string[] CheckedPrefabs =
        {
            "Hero.prefab",
            "ENM_MeleeGrunt.prefab"
        };

        /// <summary>Why a field is allowed to be zero or null on the prefab.</summary>
        private enum Why
        {
            /// <summary>Zero or false is the correct value for this component.</summary>
            MeaningfulZero,

            /// <summary>Belongs to a code path this component does not use.</summary>
            NotApplicable,

            /// <summary>
            /// A prefab cannot hold it; the scene assigns it on the instance. Verified separately
            /// by <see cref="EveryPerSceneFieldIsAssignedOnTheRunSceneInstance"/>.
            /// </summary>
            AssignedPerScene
        }

        private readonly struct Exemption
        {
            public readonly string Prefab;
            public readonly string Component;
            public readonly string Field;
            public readonly Why Reason;
            public readonly string Explanation;

            public Exemption(string prefab, string component, string field, Why reason, string explanation)
            {
                Prefab = prefab;
                Component = component;
                Field = field;
                Reason = reason;
                Explanation = explanation;
            }

            public bool Matches(string prefab, string component, string field)
                => Prefab == prefab && Component == component && Field == field;
        }

        /// <summary>
        /// Every zero or null that is deliberate. One line per field, with the reason it is safe.
        /// Adding a line here is the only way to silence a failure, which keeps the reasoning
        /// visible rather than lost in an inspector.
        /// </summary>
        private static readonly Exemption[] Exemptions =
        {
            new Exemption(
                "Hero.prefab", "PlayerMotor", "_sceneContext", Why.AssignedPerScene,
                "World bounds and the respawn point belong to a scene, and a prefab cannot " +
                "reference a scene object. RunSceneBuilder.BuildHero assigns it on the instance."),

            new Exemption(
                "Hero.prefab", "ProjectileSkill", "_container", Why.AssignedPerScene,
                "Pooled projectiles hang off a scene root, not off the hero: one parented to a " +
                "moving actor is carried along by it mid-flight. A prefab cannot hold a scene " +
                "reference, so RunSceneBuilder.BuildHero assigns it on the instance."),

            new Exemption(
                "Hero.prefab", "DashTrail", "_container", Why.AssignedPerScene,
                "Same reason: an afterimage parented to the hero travels with them and marks " +
                "nothing, which is the one thing it exists to do."),

            new Exemption(
                "Hero.prefab", "HealthComponent", "_sourceData", Why.NotApplicable,
                "That field seeds health from an EnemyData asset. The hero is seeded by " +
                "PlayerStats.ApplyStats from HeroData instead, so it must stay empty."),

            new Exemption(
                "ENM_MeleeGrunt.prefab", "HealthComponent", "_isPlayer", Why.MeaningfulZero,
                "False is correct: this is not the hero. UI keys the player health bar off it."),

            new Exemption(
                "ENM_MeleeGrunt.prefab", "HealthComponent", "_hurtIFrameDuration", Why.MeaningfulZero,
                "Enemies take hurt stun, not invulnerability. Giving an enemy i-frames would make " +
                "it immune for the rest of a combo and break the three hit chain of COM-002.")
        };

        [Test]
        public void EveryNumericAndReferenceFieldOnCheckedPrefabsIsWiredOrDeclared()
        {
            var offenders = new List<string>();

            foreach (string prefabName in CheckedPrefabs)
            {
                GameObject prefab = LoadPrefab(prefabName);

                foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;

                    // Only this project's components: package scripts have their own defaults and
                    // are not ours to police.
                    if (!component.GetType().Namespace?.StartsWith("ChibiRift") ?? true) continue;

                    string componentName = component.GetType().Name;

                    foreach (SerializedProperty property in AuthoredProperties(component))
                    {
                        if (!IsUnset(property)) continue;
                        if (IsExempt(prefabName, componentName, property.name)) continue;

                        offenders.Add(
                            $"{prefabName} > {componentName}.{property.name} = {Describe(property)}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "Prefab fields left at zero or null:\n  " + string.Join("\n  ", offenders) +
                "\n\nEither wire the field, or add it to PrefabWiringTests.Exemptions with the " +
                "reason it is deliberately empty. This is the path that let Hero.prefab ship with " +
                "no post-hit invulnerability while every other test passed.");
        }

        [Test]
        public void EveryPerSceneFieldIsAssignedOnTheRunSceneInstance()
        {
            Scene scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Additive);

            try
            {
                var offenders = new List<string>();

                foreach (Exemption exemption in Exemptions)
                {
                    if (exemption.Reason != Why.AssignedPerScene) continue;

                    // "The scene will assign it" is a promise. This is where it is collected.
                    string actorName = System.IO.Path.GetFileNameWithoutExtension(exemption.Prefab);
                    GameObject actor = FindRoot(scene, actorName);

                    if (actor == null)
                    {
                        offenders.Add($"{actorName}: not present in Run_01");
                        continue;
                    }

                    Component component = FindComponent(actor, exemption.Component);
                    if (component == null)
                    {
                        offenders.Add($"{actorName}: has no {exemption.Component}");
                        continue;
                    }

                    var so = new SerializedObject(component);
                    SerializedProperty property = so.FindProperty(exemption.Field);

                    if (property == null)
                    {
                        offenders.Add($"{actorName}.{exemption.Component} has no field {exemption.Field}");
                        continue;
                    }

                    if (IsUnset(property))
                    {
                        offenders.Add(
                            $"{actorName}.{exemption.Component}.{exemption.Field} is still unset in Run_01");
                    }
                }

                Assert.That(offenders, Is.Empty,
                    "Fields exempted as \"the scene assigns it\" are not actually assigned:\n  " +
                    string.Join("\n  ", offenders));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NoExemptionIsStale()
        {
            var offenders = new List<string>();

            foreach (Exemption exemption in Exemptions)
            {
                GameObject prefab = LoadPrefab(exemption.Prefab);
                Component component = FindComponent(prefab, exemption.Component);

                if (component == null)
                {
                    offenders.Add($"{exemption.Prefab} no longer has {exemption.Component}");
                    continue;
                }

                var so = new SerializedObject(component);
                if (so.FindProperty(exemption.Field) == null)
                {
                    offenders.Add($"{exemption.Component} no longer has {exemption.Field}");
                }
            }

            // Without this the list quietly rots: a renamed field leaves an exemption that silences
            // nothing, and a real zero elsewhere could hide behind a stale entry.
            Assert.That(offenders, Is.Empty,
                "Exemptions that no longer match anything:\n  " + string.Join("\n  ", offenders));
        }

        // ----- helpers ---------------------------------------------------------------------

        /// <summary>
        /// The named component anywhere under <paramref name="root"/>. Children count: a prefab
        /// puts its pooling and trail components on child objects, and a check that only looked at
        /// the root would silently pass them by.
        /// </summary>
        private static Component FindComponent(GameObject root, string typeName)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == typeName) return component;
            }

            return null;
        }

        private static GameObject LoadPrefab(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{prefabName}");
            Assert.That(prefab, Is.Not.Null, $"{PrefabRoot}/{prefabName} is missing.");
            return prefab;
        }

        /// <summary>The component's own serialized fields, skipping Unity's built-in ones.</summary>
        private static IEnumerable<SerializedProperty> AuthoredProperties(Object component)
        {
            var so = new SerializedObject(component);
            SerializedProperty iterator = so.GetIterator();

            iterator.NextVisible(true);
            while (iterator.NextVisible(false))
            {
                if (iterator.name.StartsWith("m_")) continue;
                yield return iterator.Copy();
            }
        }

        /// <summary>True when the field carries no value: zero, false, or a null reference.</summary>
        private static bool IsUnset(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: return Mathf.Approximately(property.floatValue, 0f);
                case SerializedPropertyType.Integer: return property.intValue == 0;
                case SerializedPropertyType.Boolean: return !property.boolValue;
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue == null;
                default: return false;
            }
        }

        private static string Describe(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: return property.floatValue.ToString("F3");
                case SerializedPropertyType.Integer: return property.intValue.ToString();
                case SerializedPropertyType.Boolean: return "false";
                default: return "null";
            }
        }

        private static bool IsExempt(string prefab, string component, string field)
        {
            foreach (Exemption exemption in Exemptions)
            {
                if (exemption.Matches(prefab, component, field)) return true;
            }

            return false;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }
    }
}
