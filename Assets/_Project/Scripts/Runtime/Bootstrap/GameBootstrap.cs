using System;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Input;
using FarmGame.Save;
using FarmGame.UI;
using FarmGame.View;
using UnityEngine;

namespace FarmGame.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string ContentResourcePath = "Data/ContentDatabase";

        private const float AutosaveInterval = 30f;

        [SerializeField] private string saveFileName = "farm-save.json";
        [SerializeField] private bool loadSavedGame = true;

        private float _autosaveTimer;

        public static string SaveDirectoryOverride { get; set; }

        public GameSession Session { get; private set; }
        public bool IsInitialized { get; private set; }

        public string SaveFileName
        {
            get => saveFileName;
            set => saveFileName = value;
        }

        public bool LoadSavedGame
        {
            get => loadSavedGame;
            set => loadSavedGame = value;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var content = Resources.Load<ContentDatabase>(ContentResourcePath);
            if (content == null)
            {
                Debug.LogError(
                    $"Could not load {ContentResourcePath}. " +
                    "Run Tools > Farm Game > Rebuild Everything to generate placeholder content.");
                enabled = false;
                return;
            }

            var clock = new SystemClock();
            var directory = string.IsNullOrEmpty(SaveDirectoryOverride)
                ? Application.persistentDataPath
                : SaveDirectoryOverride;
            var saveService = new SaveService(directory, saveFileName);

            SaveDataV1 saveData = null;
            if (loadSavedGame)
            {
                saveData = saveService.TryLoad(out var loadError);
                if (!string.IsNullOrEmpty(loadError))
                {
                    Debug.LogWarning($"Save could not be loaded and was backed up: {loadError}");
                }
            }

            Session = new GameSession(content, clock, saveService, saveData) { DeferSaves = true };

            var camera = FindOrCreateCamera();
            var cameraRig = camera.GetComponent<IsoCameraRig>();
            if (cameraRig == null)
            {
                cameraRig = camera.gameObject.AddComponent<IsoCameraRig>();
            }

            var farmObject = new GameObject("Farm World");
            var farmView = farmObject.AddComponent<FarmView>();
            farmView.Initialize(Session);
            cameraRig.Initialize(camera, farmView.Grid.WorldBounds);

            var uiObject = new GameObject("Game UI");
            var ui = uiObject.AddComponent<GameUI>();
            ui.Initialize(Session, farmView, camera);

            var inputObject = new GameObject("Game Input");
            var input = inputObject.AddComponent<FarmInputController>();
            input.Initialize(Session, farmView, ui, camera, cameraRig);

            IsInitialized = true;
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer >= AutosaveInterval)
            {
                _autosaveTimer = 0f;
                Session?.SaveNow();
            }
        }

        private void LateUpdate()
        {
            // Everything the player did this frame is written in one save.
            if (IsInitialized)
            {
                Session?.FlushPendingSave();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Session?.SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            if (IsInitialized)
            {
                Session?.SaveNow();
            }
        }

        private void OnDestroy()
        {
            if (IsInitialized)
            {
                Session?.SaveNow();
            }
        }

        private static Camera FindOrCreateCamera()
        {
            var existing = Camera.main;
            if (existing != null)
            {
                return existing;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.68f, 0.85f, 0.6f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.AddComponent<AudioListener>();

            var urpCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, " +
                "Unity.RenderPipelines.Universal.Runtime");
            if (urpCameraDataType != null && cameraObject.GetComponent(urpCameraDataType) == null)
            {
                cameraObject.AddComponent(urpCameraDataType);
            }

            return camera;
        }
    }
}
