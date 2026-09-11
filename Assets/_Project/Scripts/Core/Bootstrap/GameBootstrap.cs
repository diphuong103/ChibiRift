using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChibiRift.Core
{
    /// <summary>
    /// The composition root (SRS 26). Lives on one object in the Boot scene, survives every
    /// scene load, builds the <see cref="ServiceLocator"/>, runs each <see cref="ServiceInstaller"/>
    /// found on its hierarchy, then hands control to MainMenu (SRS 27).
    /// No other type in the project may create a service or declare a singleton.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour, ICoroutineRunner
    {
        private const string LogCategory = "Bootstrap";

        [Header("Boot flow")]
        [Tooltip("Scene to enter once services are up. SRS 27: Boot hands over to MainMenu.")]
        [SerializeField] private string _firstScene = SceneNames.MainMenu;

        [Tooltip("Off lets a developer press Play directly inside Hub or Run_01 while iterating.")]
        [SerializeField] private bool _loadFirstSceneOnStart = true;

        [Header("Input (P1-01)")]
        [Tooltip("ChibiRiftControls.inputactions. Its Gameplay map holds the SRS 8.3 control scheme.")]
        [SerializeField] private InputActionAsset _controls;

        private ServiceLocator _locator;
        private EventBus _eventBus;
        private InputReader _input;

        private void Awake()
        {
            // Re-entering Boot must not build a second root.
            if (ServiceLocator.Current != null)
            {
                GameLog.Warn(LogCategory, "A ServiceLocator already exists; destroying the duplicate bootstrap.");
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            _locator = new ServiceLocator();
            _eventBus = new EventBus();

            InstallCoreServices();
            InstallModuleServices();

            ServiceLocator.SetCurrent(_locator);
            GameLog.Info(LogCategory, "Composition root ready.");
        }

        private void Start()
        {
            if (!_loadFirstSceneOnStart) return;
            if (_locator == null) return;

            _locator.Get<ISceneFlowService>().LoadScene(_firstScene);
        }

        private void OnDestroy()
        {
            if (_locator == null) return;

            _locator.Clear();
            _eventBus.Clear();
            if (ServiceLocator.Current == _locator) ServiceLocator.ClearCurrent();
        }

        private void InstallCoreServices()
        {
            _locator.Register(_eventBus);
            _locator.Register<ICoroutineRunner>(this);

            _input = new InputReader(_controls);
            _locator.Register<IInputService>(_input);
            _locator.Register(_input);

            _locator.Register<ISceneFlowService>(new SceneFlowManager(this, _eventBus));
            _locator.Register<IPauseService>(new PauseManager(_eventBus, _input, this));
            _locator.Register<IAudioService>(new AudioManager());
            _locator.Register(new VfxManager(_eventBus));
        }

        /// <summary>
        /// Runs the installers supplied by Save, Telemetry and Meta. Core stays unaware of those
        /// assemblies; the Boot scene wires them by placing their components under this object.
        /// </summary>
        private void InstallModuleServices()
        {
            var installers = new List<ServiceInstaller>(GetComponentsInChildren<ServiceInstaller>(true));
            installers.Sort((a, b) => a.Order.CompareTo(b.Order));

            for (int i = 0; i < installers.Count; i++)
            {
                installers[i].Install(_locator, _eventBus);
                GameLog.Info(LogCategory, $"Installed {installers[i].GetType().Name}.");
            }
        }

        /// <inheritdoc />
        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

        /// <inheritdoc />
        public void Stop(Coroutine coroutine)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
    }
}
