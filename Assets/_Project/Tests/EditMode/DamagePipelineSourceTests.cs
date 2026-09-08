using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ChibiRift.Tests.Edit
{
    /// <summary>
    /// Asserts that health is only ever reduced through <c>CombatSystem</c> (HPS-003).
    /// </summary>
    /// <remarks>
    /// <para><b>This is a textual guard, not a compiler guarantee.</b> The type system cannot
    /// express it here: <c>CombatSystem</c> and <c>HealthComponent</c> live in the same assembly,
    /// so <c>internal</c> restricts nothing between them, and a capability token minted in
    /// ChibiRift.Core would be unusable because Core cannot reference ChibiRift.Gameplay. A
    /// determined caller can still route around this test. See OI-19 for the real fix, which is to
    /// split the pair into their own assembly in P3.</para>
    ///
    /// <para>What it does catch is the realistic failure: someone adding a second damage path in a
    /// later slice and not noticing that it skips the death guard (HPS-004), the i-frame guard
    /// (HPS-005), the crit roll and the telemetry event, all of which live in the pipeline.</para>
    /// </remarks>
    [TestFixture]
    public sealed class DamagePipelineSourceTests
    {
        private const string ScriptRoot = "Assets/_Project/Scripts";

        /// <summary>
        /// Files allowed to mention the call. <c>CombatSystem</c> is the pipeline;
        /// <c>CoreInterfaces</c> declares the member and <c>HealthComponent</c> implements it.
        /// </summary>
        private static readonly string[] AllowedFiles =
        {
            "CombatSystem.cs",
            "CoreInterfaces.cs",
            "HealthComponent.cs"
        };

        [Test]
        public void ApplyDamageIsCalledOnlyByCombatSystem()
        {
            var offenders = new List<string>();

            foreach (string path in Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(path);
                if (System.Array.IndexOf(AllowedFiles, fileName) >= 0) continue;

                string source = StripCommentsAndStrings(File.ReadAllText(path));

                // A call, not the declaration: "something.ApplyDamage(" or "ApplyDamage(" as a
                // bare invocation. The declaration only appears in the allowed files above.
                if (!Regex.IsMatch(source, @"\bApplyDamage\s*\(")) continue;

                offenders.Add(path.Replace('\\', '/'));
            }

            Assert.That(
                offenders,
                Is.Empty,
                "HPS-003 requires a single damage pipeline, but ApplyDamage is reached outside " +
                "CombatSystem in:\n  " + string.Join("\n  ", offenders) +
                "\nRoute the hit through CombatSystem.DealDamage instead; it owns the death guard, " +
                "the i-frame guard, the crit roll and the damage event.");
        }

        [Test]
        public void HealthIsWrittenOnlyInsideHealthComponent()
        {
            var offenders = new List<string>();

            foreach (string path in Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path) == "HealthComponent.cs") continue;

                string source = StripCommentsAndStrings(File.ReadAllText(path));

                // Two shapes count as writing someone's health:
                //   qualified   - "target.CurrentHealth = ..." through a reference, and
                //   unqualified - "CurrentHealth = ..." inside a type that is itself damageable.
                // The unqualified form has to be narrowed that way because event payloads such as
                // HealthChangedEvent carry a readonly field of the same name and seed it in their
                // constructor, which is not health state and must not be reported.
                bool qualifiedWrite = Regex.IsMatch(source, @"\.\s*CurrentHealth\s*(=[^=]|[-+*/]=)");
                bool unqualifiedWrite =
                    source.Contains("IDamageable")
                    && Regex.IsMatch(source, @"(?<![.\w])CurrentHealth\s*(=[^=]|[-+*/]=)");

                if (!qualifiedWrite && !unqualifiedWrite) continue;

                offenders.Add(path.Replace('\\', '/'));
            }

            Assert.That(
                offenders,
                Is.Empty,
                "CurrentHealth is written outside HealthComponent in:\n  " +
                string.Join("\n  ", offenders));
        }

        /// <summary>
        /// Removes comments and string literals so a mention inside documentation or a log message
        /// is not reported as a call. Without this the XML docs on the interface would fail the test.
        /// </summary>
        private static string StripCommentsAndStrings(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            source = Regex.Replace(source, @"//.*", string.Empty);
            source = Regex.Replace(source, "\"(?:[^\"\\\\]|\\\\.)*\"", "\"\"");
            return source;
        }
    }
}
