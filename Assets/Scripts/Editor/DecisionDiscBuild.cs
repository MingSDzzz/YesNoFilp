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
        private const string AppIconPath = "Assets/Resources/Theme/app-launcher-icon.png";
        private const string SigningConfigPath = ".signing/signing.local.json";

        [Serializable]
        private sealed class SigningConfig
        {
            public string keystorePath;
            public string keystorePassword;
            public string alias;
            public string aliasPassword;
        }

        [MenuItem("Tools/Decision Disc/Setup Android")]
        public static void SetupAndroid()
        {
            PlayerSettings.productName = "决策勋章";
            PlayerSettings.companyName = "Personal";
            PlayerSettings.bundleVersion = "1.4.7";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.personal.decisiondisc");
            PlayerSettings.Android.bundleVersionCode = 18;
            Texture2D appIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (appIcon == null) throw new BuildFailedException("Android 启动图标缺失：" + AppIconPath);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { appIcon });
            // Unity 2022.3 Personal requires the Unity splash screen. Keep it static,
            // use the app's light background, and avoid claiming it can be disabled.
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.overlayOpacity = 0.5f;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.957f, 0.969f, 0.984f, 1f);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.startInFullscreen = false;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            // Runtime-created uGUI input modules are discovered dynamically. Keep
            // their engine-side classes so IL2CPP does not strip class ID 115.
            PlayerSettings.stripEngineCode = false;
            EditorUserBuildSettings.buildAppBundle = false;
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Decision Disc Android settings applied. Scene: " + ScenePath);
        }

        [MenuItem("Tools/Decision Disc/Build APK")]
        public static void BuildApk()
        {
            SetupAndroid();
            EnsureLocalSigning();
            ConfigureSigning(true);
            List<string> missing = MissingAndroidComponents();
            if (missing.Count > 0)
                throw new BuildFailedException("Android build cannot start. Missing: " + string.Join(", ", missing));

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Unity could not switch to the Android build target.");

            string apkRelativePath = "Builds/YesNoFilp-v" + PlayerSettings.bundleVersion + ".apk";
            string absoluteApk = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), apkRelativePath));
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

        // Used by automated emulator smoke tests when an older app with an
        // incompatible signature already occupies the production package name.
        public static void BuildEmulatorTestApk()
        {
            SetupAndroid();
            string productionName = PlayerSettings.productName;
            string productionIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            bool productionCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            try
            {
                PlayerSettings.productName = productionName + "（测试）";
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, productionIdentifier + ".uitest");
                PlayerSettings.Android.useCustomKeystore = false;
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new BuildFailedException("Unity could not switch to the Android build target.");
                string output = Path.GetFullPath("Builds/YesNoFilp-ui-test-v" + PlayerSettings.bundleVersion + ".apk");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Emulator test APK failed: " + report.summary.result);
                Debug.Log("DECISION_DISC_TEST_APK=" + output);
            }
            finally
            {
                PlayerSettings.productName = productionName;
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, productionIdentifier);
                PlayerSettings.Android.useCustomKeystore = productionCustomKeystore;
                AssetDatabase.SaveAssets();
            }
        }

        public static void ValidateProject()
        {
            SetupAndroid();
            if (!File.Exists(ScenePath)) throw new Exception("Main scene was not created.");
            if (AssetDatabase.FindAssets("t:MonoScript DecisionDiscApp").Length == 0) throw new Exception("Runtime app script is missing.");
            if (DecisionEngine.EffectiveYesProbability(0f, DecisionMode.StrengthInfluences, 1f) != 1f || DecisionEngine.EffectiveYesProbability(1f, DecisionMode.StrengthInfluences, 0f) != 0f)
                throw new Exception("徽章概率端点验证失败：0%/100% 必须保持绝对结果。");
            if (DecisionEngine.EffectiveYesProbability(0.5f, DecisionMode.Fair5050, 1f) != 0.5f)
                throw new Exception("公平模式验证失败：必须保持 50/50。");
            float weak = DecisionEngine.EffectiveYesProbability(0f, DecisionMode.StrengthInfluences, .5f);
            float strong = DecisionEngine.EffectiveYesProbability(1f, DecisionMode.StrengthInfluences, .5f);
            if (weak != .5f || strong != .5f)
                throw new Exception("力度不得影响概率：50% 徽章无论力度都必须保持 50%。");
            AuditRandomness();
            Debug.Log("DECISION_DISC_VALIDATION_OK");
            List<string> missing = MissingAndroidComponents();
            Debug.Log(missing.Count == 0 ? "ANDROID_TOOLCHAIN_OK" : "ANDROID_TOOLCHAIN_MISSING=" + string.Join(",", missing));
        }

        public static void AuditRandomness()
        {
            const int seriesCount = 10000;
            int yes = 0, allYes = 0, allNo = 0;
            for (int series = 0; series < seriesCount; series++)
            {
                int seriesYes = 0;
                for (int round = 0; round < 3; round++)
                {
                    if (DecisionEngine.Decide(.5f, DecisionMode.Fair5050)) { yes++; seriesYes++; }
                }
                if (seriesYes == 3) allYes++;
                if (seriesYes == 0) allNo++;
            }
            float yesRate = yes / (seriesCount * 3f);
            float allYesRate = allYes / (float)seriesCount;
            float allNoRate = allNo / (float)seriesCount;
            Debug.Log("DECISION_DISC_RANDOM_AUDIT tosses=" + (seriesCount * 3) + "; yes=" + (yesRate * 100f).ToString("0.00") + "%; 3:0=" + (allYesRate * 100f).ToString("0.00") + "%; 0:3=" + (allNoRate * 100f).ToString("0.00") + "%");
            if (yesRate < .47f || yesRate > .53f || allYesRate < .10f || allYesRate > .15f || allNoRate < .10f || allNoRate > .15f)
                throw new Exception("公平随机大样本审计失败，结果偏离理论分布。");
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

        private static void ConfigureSigning(bool required)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(projectRoot, SigningConfigPath);
            if (!File.Exists(configPath))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                if (required) throw new BuildFailedException("缺少本机签名配置：" + configPath + "。请创建项目专用签名后再构建。");
                return;
            }
            SigningConfig config = JsonUtility.FromJson<SigningConfig>(File.ReadAllText(configPath));
            if (config == null || string.IsNullOrEmpty(config.keystorePath) || string.IsNullOrEmpty(config.keystorePassword) || string.IsNullOrEmpty(config.alias) || string.IsNullOrEmpty(config.aliasPassword))
                throw new BuildFailedException("签名配置字段不完整：" + configPath);
            string keystore = Path.GetFullPath(Path.Combine(projectRoot, config.keystorePath));
            if (!File.Exists(keystore)) throw new BuildFailedException("找不到签名文件：" + keystore);
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = config.keystorePassword;
            PlayerSettings.Android.keyaliasName = config.alias;
            PlayerSettings.Android.keyaliasPass = config.aliasPassword;
            Debug.Log("DECISION_DISC_SIGNING=" + config.alias);
        }

        private static void EnsureLocalSigning()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(projectRoot, SigningConfigPath);
            if (File.Exists(configPath)) return;
            string keytool = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", "keytool.exe");
            if (!File.Exists(keytool)) throw new BuildFailedException("无法创建项目签名：缺少 OpenJDK keytool.exe");
            string directory = Path.Combine(projectRoot, ".signing");
            Directory.CreateDirectory(directory);
            string keystore = Path.Combine(directory, "YesNoFilp.keystore");
            string password = "Ynf" + Guid.NewGuid().ToString("N").Substring(0, 21);
            var start = new System.Diagnostics.ProcessStartInfo
            {
                FileName = keytool,
                Arguments = "-genkeypair -v -keystore \"" + keystore + "\" -alias yesnofilp -keyalg RSA -keysize 2048 -validity 10000 -storepass " + password + " -keypass " + password + " -dname \"CN=YesNoFilp Personal, OU=Personal, O=YesNoFilp, L=Shanghai, ST=Shanghai, C=CN\" -storetype PKCS12",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(start))
            {
                process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("创建项目签名失败：" + process.StandardError.ReadToEnd());
            }
            var config = new SigningConfig { keystorePath = ".signing/YesNoFilp.keystore", keystorePassword = password, alias = "yesnofilp", aliasPassword = password };
            File.WriteAllText(configPath, JsonUtility.ToJson(config, true));
            Debug.Log("已创建项目专用 Android 签名。请安全备份 .signing 目录。");
        }

        private static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (File.Exists(ScenePath))
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .09f, .15f); camera.orthographic = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
