#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChibiRift.Core;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Automatically boots core services if entering PlayMode directly in a non-Boot scene (e.g. Run_01).
    /// </summary>
    public static class EditorAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitServices()
        {
            if (ServiceLocator.Current != null) return;

            var bootScene = SceneManager.GetSceneByName("Boot");
            if (!bootScene.isLoaded)
            {
                Debug.Log("[EditorAutoBootstrap] Loading Boot scene additively for Editor PlayMode iteration.");
                SceneManager.LoadScene("Boot", LoadSceneMode.Additive);
            }
        }
    }
}
#endif
