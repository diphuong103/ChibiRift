using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// Thin logging facade. Exists so SRS 30 ("Missing ScriptableObject reference: log lỗi rõ
    /// ràng trong development") has one call site that can be stripped from release builds.
    /// </summary>
    public static class GameLog
    {
        private const string DevelopmentOnly = "UNITY_EDITOR";
        private const string DevelopmentBuild = "DEVELOPMENT_BUILD";

        /// <summary>Development-only informational message. Compiled out of release builds.</summary>
        [System.Diagnostics.Conditional(DevelopmentOnly)]
        [System.Diagnostics.Conditional(DevelopmentBuild)]
        public static void Info(string category, string message)
            => Debug.Log($"[{category}] {message}");

        /// <summary>Development-only warning. Compiled out of release builds.</summary>
        [System.Diagnostics.Conditional(DevelopmentOnly)]
        [System.Diagnostics.Conditional(DevelopmentBuild)]
        public static void Warn(string category, string message)
            => Debug.LogWarning($"[{category}] {message}");

        /// <summary>Error. Kept in every build: these block release when critical (SRS 30).</summary>
        public static void Error(string category, string message)
            => Debug.LogError($"[{category}] {message}");
    }
}
