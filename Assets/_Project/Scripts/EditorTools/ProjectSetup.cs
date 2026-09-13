using UnityEditor;
using UnityEngine;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// One entry point that runs the whole foundation setup, so it can be replayed from a clean
    /// checkout or driven headlessly with
    /// <c>-batchmode -executeMethod ChibiRift.EditorTools.ProjectSetup.RunAll</c>.
    /// </summary>
    public static class ProjectSetup
    {
        /// <summary>Layers, collision matrix, baseline data assets, then the five scenes.</summary>
        [MenuItem("ChibiRift/Setup/Run All")]
        public static void RunAll()
        {
            Debug.Log("[Setup] === ChibiRift foundation setup ===");

            ProjectLayers.CreateLayers();
            ProjectLayers.ConfigureCollisionMatrix();
            SampleDataGenerator.Generate();
            SceneGenerator.GenerateAll();
            RunSceneBuilder.Build();
            SpritesheetSlicer.ReimportKnownSpritesheets();
            HeroAnimatorBuilder.Build();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Setup] === Complete ===");
        }
    }
}
