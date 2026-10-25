using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StuntCar.EditorTools
{
    /// <summary>
    /// Builds the whole project from code, so CI can produce a playable WebGL build without
    /// anyone opening the editor: URP pipeline asset, materials, post-processing, the scene,
    /// player settings, then the build itself.
    /// Also available from the "Stunt Car" menu when the project is opened in the editor.
    /// </summary>
    public static class BuildScript
    {
        const string GeneratedDir = "Assets/Generated";
        const string SceneDir = "Assets/Scenes";
        const string ScenePath = SceneDir + "/Main.unity";

        [MenuItem("Stunt Car/Generate Project Assets")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(GeneratedDir);
            Directory.CreateDirectory(SceneDir);
            AssetDatabase.Refresh();

            ConfigurePlayer();
            EnsureUrp();
            var boot = CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[StuntCar] Project assets generated: " + (boot != null));
        }

        [MenuItem("Stunt Car/Build WebGL")]
        public static void BuildWebGL()
        {
            try
            {
                GenerateAll();
                string path = GetArg("-customBuildPath") ?? "build/WebGL/WebGL";
                Debug.Log("[StuntCar] Building WebGL to " + path);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = path,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                Debug.Log($"[StuntCar] Build {summary.result}: {summary.totalSize / (1024UL * 1024UL)} MB, {summary.totalErrors} errors, {summary.totalTime}");
                if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        // ------------------------------------------------------------ player settings

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "StuntCarRemake";
            PlayerSettings.productName = "Stunt Car Racer Remake";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; // works on any static host (GitHub Pages, itch.io)
            PlayerSettings.WebGL.template = "PROJECT:FullWindow";

            // Keyboard uses the classic Input Manager, gamepads use the Input System package:
            // run both ("activeInputHandler" 2).
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings != null && settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 2)
                {
                    handler.intValue = 2;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        // ------------------------------------------------------------ render pipeline

        static UniversalRenderPipelineAsset EnsureUrp()
        {
            string rendererPath = GeneratedDir + "/URP_Renderer.asset";
            string pipelinePath = GeneratedDir + "/URP_Pipeline.asset";

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                // Same as URP's own "Create URP Asset" menu: fill shader and post-process resources.
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);

                // Post-processing needs the renderer to point at URP's default PostProcessData.
                var postData = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                var rso = new SerializedObject(renderer);
                var ppProp = rso.FindProperty("postProcessData");
                if (ppProp != null && ppProp.objectReferenceValue == null && postData != null)
                {
                    ppProp.objectReferenceValue = postData;
                    rso.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(renderer);

                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            // Quality knobs, set by serialized name so a renamed field just gets skipped.
            var so = new SerializedObject(pipeline);
            SetFloat(so, "m_ShadowDistance", 220f);
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetBool(so, "m_SupportsHDR", true);
            SetInt(so, "m_MSAA", 4);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        static void SetFloat(SerializedObject so, string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; }
        static void SetInt(SerializedObject so, string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; }
        static void SetBool(SerializedObject so, string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; }

        // ------------------------------------------------------------ materials

        static Material Lit(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            string path = $"{GeneratedDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Color color)
        {
            string path = $"{GeneratedDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        // URP particle material: soft alpha-blended (smoke, dust) or additive and over-bright (sparks, for bloom).
        static Material Particle(string name, Color color, bool additive)
        {
            string path = $"{GeneratedDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);                  // transparent
            m.SetFloat("_Blend", additive ? 2f : 0f);    // additive : alpha
            BaseShaderGUI.SetMaterialKeywords(m);         // URP's own blend/keyword setup
            BaseShaderGUI.SetupMaterialBlendMode(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Sky()
        {
            string path = GeneratedDir + "/Sky.mat";
            var shader = Shader.Find("Skybox/Procedural");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetFloat("_SunSize", 0.035f);
            m.SetFloat("_SunSizeConvergence", 6f);
            m.SetFloat("_AtmosphereThickness", 0.85f);
            m.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.75f));
            m.SetColor("_GroundColor", new Color(0.36f, 0.4f, 0.36f));
            m.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static VolumeProfile PostProfile()
        {
            string path = GeneratedDir + "/PostProcessing.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(10f);
            color.saturation.Override(8f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);

            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ------------------------------------------------------------ scene

        static GameBootstrap CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var horizon = new Color(0.72f, 0.78f, 0.86f);

            // Camera with post-processing and SMAA.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.position = new Vector3(0, 60, -200);
            camGo.transform.LookAt(Vector3.zero);

            // Late-afternoon sun.
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(38f, -40f, 0f);

            var volGo = new GameObject("Post Processing");
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();

            RenderSettings.skybox = Sky();
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = 300f;
            RenderSettings.fogEndDistance = 2200f;

            var bootGo = new GameObject("Game");
            var boot = bootGo.AddComponent<GameBootstrap>();
            boot.asphalt = Lit("Asphalt", new Color(0.95f, 0.95f, 0.95f), 0.25f);
            boot.kerbRed = Lit("KerbRed", new Color(0.78f, 0.07f, 0.05f), 0.35f);
            boot.kerbWhite = Lit("KerbWhite", new Color(0.93f, 0.93f, 0.9f), 0.35f);
            boot.concrete = Lit("Concrete", new Color(0.56f, 0.56f, 0.53f), 0.15f);
            boot.scaffold = Lit("Scaffold", new Color(0.62f, 0.64f, 0.68f), 0.55f, 0.6f);
            boot.grass = Lit("Grass", new Color(0.33f, 0.47f, 0.2f), 0.08f);
            boot.playerPaint = Lit("PaintPlayer", new Color(0.78f, 0.04f, 0.03f), 0.85f, 0.25f);
            boot.opponentPaint = Lit("PaintRival", new Color(0.05f, 0.22f, 0.8f), 0.85f, 0.25f);
            boot.chrome = Lit("Chrome", new Color(0.86f, 0.86f, 0.9f), 0.92f, 0.85f);
            boot.darkMetal = Lit("DarkMetal", new Color(0.07f, 0.07f, 0.08f), 0.45f, 0.4f);
            boot.tyre = Lit("Tyre", new Color(0.035f, 0.035f, 0.035f), 0.2f);
            boot.helmet = Lit("Helmet", new Color(0.97f, 0.8f, 0.08f), 0.85f);
            boot.boostFlame = Lit("BoostFlame", new Color(1f, 0.45f, 0.1f), 0.5f, 0f, new Color(6f, 2.4f, 0.5f));
            boot.standSeats = Lit("StandSeats", new Color(0.18f, 0.28f, 0.55f), 0.3f);
            boot.floodlight = Lit("Floodlight", Color.white, 0.5f, 0f, new Color(4f, 4f, 3.6f));
            boot.cable = Unlit("Cable", new Color(0.08f, 0.08f, 0.08f));
            boot.tailLight = Lit("TailLight", new Color(0.9f, 0.05f, 0.05f), 0.6f, 0f, new Color(3f, 0.15f, 0.1f));
            boot.particleSmoke = Particle("ParticleSmoke", Color.white, false);
            boot.particleSpark = Particle("ParticleSpark", new Color(4f, 2.2f, 0.8f), true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return boot;
        }
    }
}
