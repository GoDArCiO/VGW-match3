using System.IO;
using Match3.Config;
using Match3.View;
using Proto.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Proto.EditorTools
{
    /// <summary>
    /// Builds the single playable match-3 scene headlessly — NO human editor work (the headless-automation
    /// pillar). Run via:
    ///   .\tools\unity.ps1 exec -Method Proto.EditorTools.SceneSetup.Build
    /// Creates an orthographic 2D camera (+ CameraRig + AudioListener), a directional light (so the lit
    /// particle bursts read in colour), and the Game object carrying the UIDocument + PointerInputAdapter +
    /// Match3Bootstrap. Ensures the config assets exist and wires every serialized reference via
    /// SerializedObject. Saves Assets/Scenes/Proto.unity and registers it as the sole build scene. Idempotent.
    /// </summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Proto.unity";
        private const string PanelSettingsPath = "Assets/UI/ProtoPanelSettings.asset";
        private const string ThemePath = "Assets/UI/GameTheme.tss";
        private const string UxmlPath = "Assets/UI/proto.uxml";
        private const string GemSetPath = "Assets/Config/GemSet.asset";
        private const string LevelSetPath = "Assets/Config/LevelSet.asset";
        private const string MetaLayoutPath = "Assets/Config/MetaBoardLayout.asset";

        public static void Build()
        {
            // Make sure the gem/level/meta config assets exist before wiring the bootstrap to them.
            // We deliberately ignore the handles EnsureAll() returns: in batchmode those are unpersisted
            // in-memory instances with no asset path, so wiring them produces {fileID: 0} (a null reference
            // the bootstrap only discovers at Play time). We reload strictly by path below instead.
            Match3Assets.EnsureAll();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Orthographic camera looking down +Z at the XY board, centred on the origin, portrait framing.
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.7f;        // safe fallback; Match3Bootstrap.FitCamera refines for the live aspect
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.10f, 0.14f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CameraRig>();
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            PanelSettings panelSettings = CreateOrLoadPanelSettings();
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null) throw new FileNotFoundException($"Missing {UxmlPath}");

            var gameGo = new GameObject("Game");
            var uiDocument = gameGo.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = uxml;
            gameGo.AddComponent<PointerInputAdapter>();

            // Load the config assets fresh by path before wiring. In batchmode the references returned
            // straight out of EnsureAll() are unpersisted in-memory instances (no asset path), so wiring
            // them serializes as {fileID: 0}. Forcing the import and loading by path yields properly-backed
            // handles whose cross-asset references survive SaveScene.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var gemSet = LoadOrThrow<GemSet>(GemSetPath);
            var levelSet = LoadOrThrow<LevelSet>(LevelSetPath);
            var metaLayout = LoadOrThrow<MetaBoardLayout>(MetaLayoutPath);

            var bootstrap = gameGo.AddComponent<Match3Bootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("uiDocument").objectReferenceValue = uiDocument;
            serialized.FindProperty("gemSet").objectReferenceValue = gemSet;
            serialized.FindProperty("levelSet").objectReferenceValue = levelSet;
            serialized.FindProperty("metaLayout").objectReferenceValue = metaLayout;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException($"Failed to save {ScenePath}");

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[SceneSetup] Built {ScenePath} (orthographic match-3) and registered it as the sole build scene.");
        }

        /// <summary>
        /// Imports then loads a config asset by path so the handle is backed by the persisted asset (a
        /// freshly created/dirtied SO doesn't serialize as a cross-asset reference in batchmode). Throws
        /// rather than wiring a null the bootstrap would only discover at Play time.
        /// </summary>
        private static T LoadOrThrow<T>(string path) where T : Object
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<T>(path);
            if (loaded == null)
                throw new FileNotFoundException($"Failed to load {typeof(T).Name} at {path} — cannot wire it into the scene.");
            return loaded;
        }

        private static PanelSettings CreateOrLoadPanelSettings()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) throw new FileNotFoundException($"Missing {ThemePath}");

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1080, 1920);
            panelSettings.match = 0.5f;
            EditorUtility.SetDirty(panelSettings);
            AssetDatabase.SaveAssets();
            return panelSettings;
        }
    }
}
