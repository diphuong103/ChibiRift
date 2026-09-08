using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Asserts the shipped <c>ChibiRiftControls.inputactions</c> really contains the control map
    /// fixed in SRS 8.3 (P1-01).
    /// </summary>
    /// <remarks>
    /// The PlayMode movement tests substitute a fake <c>IInputService</c>, so they never touch a
    /// binding. Without this file, renaming an action or dropping a key binding would break the
    /// game while every other test stayed green.
    /// </remarks>
    public sealed class InputActionsAssetTests
    {
        private const string AssetPath = "Assets/_Project/Settings/ChibiRiftControls.inputactions";
        private const string GameplayMap = "Gameplay";

        private static readonly string[] RequiredActions =
        {
            "Move", "Jump", "Dash", "Attack", "Skill1", "Skill2", "Skill3", "Aim", "Pause"
        };

        private InputActionAsset _asset;

        [SetUp]
        public void LoadAsset()
        {
            Assert.That(File.Exists(AssetPath), Is.True, $"Missing input asset at {AssetPath}");

            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _asset.LoadFromJson(File.ReadAllText(AssetPath));
        }

        [TearDown]
        public void DestroyAsset()
        {
            if (_asset != null) Object.DestroyImmediate(_asset);
        }

        [Test]
        public void GameplayActionMapExists()
        {
            Assert.That(_asset.FindActionMap(GameplayMap), Is.Not.Null,
                $"Action map '{GameplayMap}' is missing.");
        }

        [Test]
        public void AllNineActionsArePresentAndNamedCorrectly()
        {
            InputActionMap map = _asset.FindActionMap(GameplayMap);
            Assert.That(map, Is.Not.Null);

            foreach (string action in RequiredActions)
            {
                Assert.That(map.FindAction(action), Is.Not.Null, $"Action '{action}' is missing (SRS 8.3).");
            }

            Assert.That(map.actions.Count, Is.EqualTo(RequiredActions.Length),
                "The Gameplay map gained or lost an action; SRS 8.3 fixes the list at nine.");
        }

        [Test]
        public void JumpIsBoundToSpace()
        {
            InputAction jump = _asset.FindActionMap(GameplayMap).FindAction("Jump");
            Assert.That(BindingPaths(jump), Contains.Item("<Keyboard>/space"), "MOV-002 requires Space.");
        }

        [Test]
        public void MoveIsAOneDimensionalAxisOverAAndD()
        {
            InputAction move = _asset.FindActionMap(GameplayMap).FindAction("Move");

            bool hasComposite = false;
            foreach (InputBinding binding in move.bindings)
            {
                if (binding.isComposite) hasComposite = true;
            }

            Assert.That(hasComposite, Is.True, "Move must use a composite, not two separate bindings.");
            Assert.That(BindingPaths(move), Contains.Item("<Keyboard>/a"), "MOV-001 requires A for left.");
            Assert.That(BindingPaths(move), Contains.Item("<Keyboard>/d"), "MOV-001 requires D for right.");
        }

        [Test]
        public void RemainingActionsKeepTheSrsControlMap()
        {
            InputActionMap map = _asset.FindActionMap(GameplayMap);

            Assert.That(BindingPaths(map.FindAction("Dash")), Contains.Item("<Keyboard>/leftShift"), "MOV-006");
            Assert.That(BindingPaths(map.FindAction("Attack")), Contains.Item("<Mouse>/leftButton"), "COM-001");
            Assert.That(BindingPaths(map.FindAction("Skill1")), Contains.Item("<Keyboard>/q"), "COM-007");
            Assert.That(BindingPaths(map.FindAction("Skill2")), Contains.Item("<Keyboard>/e"), "COM-007");
            Assert.That(BindingPaths(map.FindAction("Skill3")), Contains.Item("<Keyboard>/r"), "COM-007");
            Assert.That(BindingPaths(map.FindAction("Aim")), Contains.Item("<Mouse>/position"), "COM-009");
            Assert.That(BindingPaths(map.FindAction("Pause")), Contains.Item("<Keyboard>/escape"), "PAU-001");
        }

        [Test]
        public void WasdKeysBeyondAAndDStayUnbound()
        {
            // SRS 43 Q2: W, S and F are deliberately out of the MVP control map.
            foreach (InputAction action in _asset.FindActionMap(GameplayMap).actions)
            {
                foreach (string path in BindingPaths(action))
                {
                    Assert.That(path, Is.Not.EqualTo("<Keyboard>/w"));
                    Assert.That(path, Is.Not.EqualTo("<Keyboard>/s"));
                    Assert.That(path, Is.Not.EqualTo("<Keyboard>/f"));
                }
            }
        }

        private static string[] BindingPaths(InputAction action)
        {
            Assert.That(action, Is.Not.Null);

            var paths = new string[action.bindings.Count];
            for (int i = 0; i < action.bindings.Count; i++) paths[i] = action.bindings[i].path;
            return paths;
        }
    }
}
