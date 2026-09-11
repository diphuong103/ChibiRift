using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Keeps the project's one device-reading exception from becoming a habit.
    /// </summary>
    /// <remarks>
    /// <para><b>The rule, held since P1 slice 1.</b> Gameplay never reads an input device. Input
    /// arrives through <c>IInputService</c>, backed by the bound action map in
    /// <c>ChibiRiftControls.inputactions</c>, so rebinding is data and the control scheme is
    /// testable. <c>Keyboard.current</c>, <c>Mouse.current</c> and <c>Gamepad.current</c> bypass all
    /// of that.</para>
    ///
    /// <para><b>The exception.</b> Development-only tooling — the F1 overlay and the F2/F3/F4 spawn
    /// keys — genuinely should not live in the shipping control scheme. Those may read a device
    /// directly, but only inside <c>#if UNITY_EDITOR</c> or <c>#if DEVELOPMENT_BUILD</c>, so the
    /// code cannot exist in a player build at all.</para>
    ///
    /// <para><b>Why a test and not a note.</b> A first exception is harmless. The danger is the
    /// second one, argued for on the grounds that the first exists. A note in OPEN_ISSUES does not
    /// stop that; a failing build does. See OI-29.</para>
    /// </remarks>
    [TestFixture]
    public sealed class DeviceInputSourceTests
    {
        private const string ScriptRoot = "Assets/_Project/Scripts";

        /// <summary>
        /// Runtime assemblies. EditorTools and the test assemblies are excluded: they are editor
        /// code by construction and cannot reach a player build.
        /// </summary>
        private static readonly string[] ScannedFolders =
        {
            "Core", "Data", "Gameplay", "Meta", "Save", "Telemetry", "UI"
        };

        private static readonly string[] DeviceAccessors =
        {
            "Keyboard.current",
            "Mouse.current",
            "Gamepad.current"
        };

        [Test]
        public void DeviceAccessIsOnlyEverInsideADevelopmentGuard()
        {
            var offenders = new List<string>();

            foreach (string folder in ScannedFolders)
            {
                string root = Path.Combine(ScriptRoot, folder);
                if (!Directory.Exists(root)) continue;

                foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    CollectUnguardedAccess(path, offenders);
                }
            }

            Assert.That(
                offenders,
                Is.Empty,
                "A device is read outside a development guard:\n  " + string.Join("\n  ", offenders) +
                "\n\nGameplay reads input through IInputService and the bound action map, never " +
                "through Keyboard/Mouse/Gamepad.current. The only permitted exceptions are " +
                "development tools — DebugSpawner and DebugOverlay — and only inside " +
                "#if UNITY_EDITOR || DEVELOPMENT_BUILD, so they cannot reach a player build.\n" +
                "If this is genuinely new development tooling, add the guard. If it is gameplay, " +
                "bind an action in ChibiRiftControls.inputactions instead.");
        }

        private static void CollectUnguardedAccess(string path, ICollection<string> offenders)
        {
            string[] lines = File.ReadAllLines(path);

            // One entry per open #if, holding whether that region is development-only. A region
            // nested inside a guarded one stays guarded, so the check is on the top of the stack.
            var regions = new Stack<bool>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string directive = line.TrimStart();

                if (directive.StartsWith("#if"))
                {
                    regions.Push(IsDevelopmentOnly(directive) || (regions.Count > 0 && regions.Peek()));
                    continue;
                }

                if (directive.StartsWith("#elif"))
                {
                    if (regions.Count > 0) regions.Pop();
                    regions.Push(IsDevelopmentOnly(directive));
                    continue;
                }

                if (directive.StartsWith("#else"))
                {
                    // The else branch of a development guard is the shipping branch, so it is not
                    // itself guarded however the #if was written.
                    if (regions.Count > 0) regions.Pop();
                    regions.Push(false);
                    continue;
                }

                if (directive.StartsWith("#endif"))
                {
                    if (regions.Count > 0) regions.Pop();
                    continue;
                }

                // Comments mentioning the accessor are documentation, not access.
                string code = StripComment(line);
                if (!ContainsDeviceAccess(code)) continue;

                bool guarded = regions.Count > 0 && regions.Peek();
                if (guarded) continue;

                offenders.Add($"{path.Replace('\\', '/')}:{i + 1}: {code.Trim()}");
            }
        }

        /// <summary>
        /// Whether a preprocessor condition restricts its region to editor or development builds.
        /// A negated symbol does the opposite, so it does not count.
        /// </summary>
        private static bool IsDevelopmentOnly(string directive)
        {
            string condition = directive.Replace(" ", string.Empty);

            if (condition.Contains("!UNITY_EDITOR") || condition.Contains("!DEVELOPMENT_BUILD")) return false;

            return condition.Contains("UNITY_EDITOR") || condition.Contains("DEVELOPMENT_BUILD");
        }

        private static bool ContainsDeviceAccess(string code)
        {
            foreach (string accessor in DeviceAccessors)
            {
                // The lookbehind excludes only an identifier character, not a dot. A fully
                // qualified UnityEngine.InputSystem.Keyboard.current is the same access and must be
                // caught; an unrelated FakeKeyboard.current must not be.
                if (Regex.IsMatch(code, $@"(?<!\w){Regex.Escape(accessor)}")) return true;
            }

            return false;
        }

        private static string StripComment(string line)
        {
            int comment = line.IndexOf("//", System.StringComparison.Ordinal);
            return comment >= 0 ? line.Substring(0, comment) : line;
        }
    }
}
