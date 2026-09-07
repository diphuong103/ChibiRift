using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChibiRift.Core
{
    /// <summary>
    /// Single owner of scene transitions across the five scenes of SRS 27
    /// (Boot to MainMenu to Hub to Run_01 to PostRun and back to Hub).
    /// Loads asynchronously so the 5 second budget of NFR-004 is measurable, and refuses
    /// overlapping requests so a double-clicked button cannot corrupt the flow (SRS 30).
    /// </summary>
    public sealed class SceneFlowManager : ISceneFlowService
    {
        private const string LogCategory = "SceneFlow";

        private readonly ICoroutineRunner _runner;
        private readonly EventBus _eventBus;

        /// <inheritdoc />
        public string CurrentSceneName { get; private set; }

        /// <inheritdoc />
        public bool IsTransitioning { get; private set; }

        /// <inheritdoc />
        public event Action<float> LoadProgressChanged;

        /// <inheritdoc />
        public event Action<string> SceneLoaded;

        public SceneFlowManager(ICoroutineRunner runner, EventBus eventBus)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            CurrentSceneName = SceneManager.GetActiveScene().name;
        }

        /// <inheritdoc />
        public void LoadScene(string sceneName, Action onComplete = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                GameLog.Error(LogCategory, "LoadScene called with an empty scene name.");
                return;
            }

            if (IsTransitioning)
            {
                GameLog.Warn(LogCategory, $"Ignoring request for '{sceneName}': '{CurrentSceneName}' is still loading.");
                return;
            }

            _runner.Run(LoadRoutine(sceneName, onComplete));
        }

        private IEnumerator LoadRoutine(string sceneName, Action onComplete)
        {
            IsTransitioning = true;
            LoadProgressChanged?.Invoke(0f);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                // SRS 30: a failed transition must log and must not lose confirmed meta data.
                GameLog.Error(LogCategory, $"Scene '{sceneName}' is missing from Build Settings.");
                IsTransitioning = false;
                yield break;
            }

            while (!operation.isDone)
            {
                LoadProgressChanged?.Invoke(operation.progress);
                yield return null;
            }

            CurrentSceneName = sceneName;
            IsTransitioning = false;

            LoadProgressChanged?.Invoke(1f);
            SceneLoaded?.Invoke(sceneName);
            GameLog.Info(LogCategory, $"Entered scene '{sceneName}'.");

            onComplete?.Invoke();
        }
    }
}
