using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// High-level application state above a single Run (SRS 26). Tracks which part of the loop
    /// of SRS 7 the player is in and exposes the navigation the menus need, so UI never calls
    /// <see cref="ISceneFlowService"/> with a raw scene name of its own invention.
    /// </summary>
    public sealed class GameManager : IGameService
    {
        private readonly ISceneFlowService _sceneFlow;
        private readonly IPauseService _pause;

        /// <summary>Where the player currently is in the loop of SRS 7.</summary>
        public GamePhase Phase { get; private set; } = GamePhase.Boot;

        public GameManager(ISceneFlowService sceneFlow, IPauseService pause)
        {
            _sceneFlow = sceneFlow;
            _pause = pause;
        }

        /// <summary>Main Menu "Play" (SRS 19.1). There is deliberately no Continue (SAVE-006).</summary>
        public void GoToHub()
        {
            Phase = GamePhase.Hub;
            _sceneFlow.LoadScene(SceneNames.Hub);
        }

        /// <summary>Hub "Start Run" (HUB-002). Begins the Run lifecycle (RUN-001).</summary>
        public void StartRun()
        {
            Phase = GamePhase.Run;
            _sceneFlow.LoadScene(SceneNames.Run01);
        }

        /// <summary>Every Run ending routes here: Victory, Death and Abandon alike (RUN-002/003/008).</summary>
        public void GoToPostRun()
        {
            Phase = GamePhase.PostRun;
            _sceneFlow.LoadScene(SceneNames.PostRun);
        }

        /// <summary>Post-Run "Return to Hub" (RUN-007).</summary>
        public void ReturnToHub()
        {
            Phase = GamePhase.Hub;
            _sceneFlow.LoadScene(SceneNames.Hub);
        }

        /// <summary>Pause Menu "Quit to Main Menu" (PAU-002), after the confirm step (PAU-003).</summary>
        public void QuitToMainMenu()
        {
            Phase = GamePhase.MainMenu;
            _sceneFlow.LoadScene(SceneNames.MainMenu);
        }

        /// <summary>Main Menu "Quit" (SRS 19.1).</summary>
        public void QuitGame()
        {
            // TODO(SAVE-001): flush the meta save before the process exits.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>Coarse application phase, matching the loop of SRS 7.</summary>
    public enum GamePhase
    {
        Boot = 0,
        MainMenu = 1,
        Hub = 2,
        Run = 3,
        PostRun = 4
    }
}
