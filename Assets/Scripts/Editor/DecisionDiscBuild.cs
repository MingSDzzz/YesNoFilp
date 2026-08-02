using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DecisionDisc.Editor
{
    public static class DecisionDiscBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ApkRelativePath = "Builds/DecisionDisc.apk";

        [MenuItem("Tools/Decision Disc/Setup Android")]
        public static void SetupAndroid()
        {
            PlayerSettings.productName = "Decision Disc";
            PlayerSettings.companyName = "Personal";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.personal.decisiondisc");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Decision Disc Android settings applied. Scene: " + ScenePath);
        }

        [MenuItem("Tools/Decision Disc/Build APK")]
        public static void BuildApk()
        {
            SetupAndroid();
            List<string> missing = MissingAndroidComponents();
            if (missing.Count > 0)
                throw new BuildFailedException("Android build cannot start. Missing: " + string.Join(", ", missing));

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Unity could not switch to the Android build target.");

            string absoluteApk = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ApkRelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteApk));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = absoluteApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Android build failed: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
            if (!File.Exists(absoluteApk) || new FileInfo(absoluteApk).Length == 0)
                throw new BuildFailedException("Unity reported success but no non-empty APK exists at " + absoluteApk);
            Debug.Log("DECISION_DISC_APK=" + absoluteApk);
        }

        public static void ValidateProject()
        {
            SetupAndroid();
            if (!File.Exists(ScenePath)) throw new Exception("Main scene was not created.");
            if (AssetDatabase.FindAssets("t:MonoScript DecisionDiscApp").Length == 0) throw new Exception("Runtime app script is missing.");
            Debug.Log("DECISION_DISC_VALIDATION_OK");
            List<string> missing = MissingAndroidComponents();
            Debug.Log(missing.Count == 0 ? "ANDROID_TOOLCHAIN_OK" : "ANDROID_TOOLCHAIN_MISSING=" + string.Join(",", missing));
        }

        public static List<string> MissingAndroidComponents()
        {
            string root = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");
            var missing = new List<string>();
            if (!Directory.Exists(root)) missing.Add("Android Build Support");
            if (!Directory.Exists(Path.Combine(root, "SDK"))) missing.Add("Android SDK");
            if (!Directory.Exists(Path.Combine(root, "NDK"))) missing.Add("Android NDK");
            if (!Directory.Exists(Path.Combine(root, "OpenJDK"))) missing.Add("OpenJDK");
            return missing;
        }

        private static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .09f, .15f); camera.orthographic = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
