using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Headless WebGL build — the shareable artifact for the pitch. Run via:
    ///   .\tools\unity.ps1 build-webgl   (which calls Proto.EditorTools.BuildScript.BuildWebGL)
    /// Tuned for a tiny, fast-loading slice: stripping High, no exceptions, no dev console, decompression
    /// fallback so the build opens locally. Requires the WebGL build module installed via Unity Hub.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>DEBUG build for diagnosing a WebGL-only crash: full exceptions + stack traces, Minimal managed
        /// stripping (High can strip a reflection-only type/method and silently abort), and a Development build —
        /// so the browser console prints the REAL C# exception + stack instead of the bare "undefined" the
        /// shipping build (exceptionSupport=None) shows. Run:
        ///   .\tools\unity.ps1 exec -Method Proto.EditorTools.BuildScript.BuildWebGLDebug
        /// Reproduce the crash, read the console: a managed throw names its site; a still-bare "undefined" with no
        /// managed frames means a native abort. Ship with BuildWebGL.</summary>
        public static void BuildWebGLDebug() => BuildWebGL(debug: true);

        public static void BuildWebGL() => BuildWebGL(debug: false);

        private static void BuildWebGL(bool debug)
        {
            string product = string.IsNullOrWhiteSpace(PlayerSettings.productName) ? "proto" : PlayerSettings.productName;
            string slug = product.Replace(' ', '-').ToLowerInvariant();
            string outDir = Path.Combine("Builds", $"{slug}-web");

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No scenes enabled in Build Settings — run Proto.EditorTools.SceneSetup.Build first.");

            // URP's internal blit/copy shaders. URP's "Strip Unused Variants" wrongly drops these on the WebGL
            // target — the runtime camera blit then hits a shader with no suitable subshader
            // ("Hidden/CoreSRP/CoreCopy is not supported on this GPU") and the native render loop HALTS the
            // program ("Uncaught exception from main loop → undefined", with NO managed stack even in a
            // FullWithStacktrace build). Force-including them by GUID (names are version-fragile) keeps the needed
            // variant so the slice opens. GUIDs are stable across URP package versions.
            AlwaysIncludeShaderByGuid("12dc59547ea167a4ab435097dd0f9add", "CoreCopy");
            AlwaysIncludeShaderByGuid("93446b5c5339d4f00b85c159e1159b7c", "CoreBlit");
            AlwaysIncludeShaderByGuid("d104b2fc1ca6445babb8e90b0758136b", "CoreBlitColorAndDepth");
            AlwaysIncludeShaderByGuid("8c3ee818f2efa514c889881ccb2e95a2", "StencilDitherMaskSeed");
            AlwaysIncludeShaderByGuid("a89bee29cffa951418fc1e2da94d1959", "BlitHDROverlay");

            // THE crash fix: MaterialFactory builds EVERY material via Shader.Find("Universal Render Pipeline/Lit").
            // No asset references it, so High stripping drops it from the build → Shader.Find returns null →
            // `new Material(null)` throws ArgumentNullException on the first particle burst (i.e. the first move):
            //   ParticleFactory.Burst → MaterialFactory.Get → new Material(shader:null).
            // Force-including it keeps it in the build so Shader.Find resolves.
            AlwaysIncludeShaderByName("Universal Render Pipeline/Lit");

            // Size/reliability tuning for a prototype slice. In DEBUG: Minimal stripping (High can strip a
            // reflection-only type/method → silent WebGL abort) and full exceptions + stack traces so the console
            // names the throw site.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, debug ? ManagedStrippingLevel.Minimal : ManagedStrippingLevel.High);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.WebGL.exceptionSupport = debug ? WebGLExceptionSupport.FullWithStacktrace : WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // opens from file:// without a server
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.SplashScreen.showUnityLogo = false; // if the license allows
            // Custom template: the DEFAULT template wires config.showBanner, which paints a permanent red "crash"
            // banner for benign engine error LOGS (e.g. URP's "Hidden/CoreSRP/CoreCopy not supported on this GPU"
            // blitter-init message) over a slice that is actually running — and the loader logo never hides. Our
            // Portrait template omits showBanner (errors go to console only) and hides the loader on load.
            PlayerSettings.WebGL.template = "PROJECT:Portrait";

            Directory.CreateDirectory(outDir);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = debug ? BuildOptions.Development : BuildOptions.None,
            });

            BuildSummary summary = report.summary;
            Debug.Log($"[BuildScript] Result: {summary.result}, errors: {summary.totalErrors}, size: {summary.totalSize / (1024 * 1024)} MB, output: {outDir}");

            if (summary.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw new Exception($"WebGL build failed: {summary.totalErrors} errors — see console/log.");
            }
        }

        /// <summary>Add a shader to GraphicsSettings' Always-Included list by its asset GUID so the build can't
        /// strip it. Used for URP's built-in Hidden blit/copy shaders, whose <c>Shader.Find</c> names are
        /// version-fragile but whose GUIDs are stable. Warns + skips if the GUID no longer resolves.</summary>
        private static void AlwaysIncludeShaderByGuid(string guid, string label)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var shader = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                Debug.LogWarning($"[BuildScript] Blit shader GUID {guid} ({label}) did not resolve — URP may have moved it; skipping.");
                return;
            }
            AddAlwaysIncluded(shader, label);
        }

        /// <summary>Add a shader to GraphicsSettings' Always-Included list by name (for shaders referenced only via
        /// <c>Shader.Find</c> at runtime — no asset points at them, so the build would otherwise strip them and
        /// <c>Shader.Find</c> would return null). Warns + skips if the name doesn't resolve.</summary>
        private static void AlwaysIncludeShaderByName(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[BuildScript] Shader not found, cannot always-include: {shaderName}");
                return;
            }
            AddAlwaysIncluded(shader, shaderName);
        }

        private static void AddAlwaysIncluded(Shader shader, string label)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            SerializedProperty arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;

            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return; // already present

            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            Debug.Log($"[BuildScript] Registered always-included shader: {label} ({shader.name})");
        }
    }
}
