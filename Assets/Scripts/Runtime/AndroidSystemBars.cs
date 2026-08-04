using System.Collections;
using UnityEngine;

namespace DecisionDisc
{
    /// <summary>Keeps Android's clock/status bar visible and exposes its real insets.</summary>
    public sealed class AndroidSystemBars : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var host = new GameObject("Decision Disc Android System Bars");
            DontDestroyOnLoad(host);
            host.AddComponent<AndroidSystemBars>();
#endif
        }

        private void Awake() { Apply(); }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) StartCoroutine(ReapplyAfterFocus());
        }

        private IEnumerator ReapplyAfterFocus()
        {
            Apply();
            yield return new WaitForSecondsRealtime(.15f);
            Apply();
            yield return new WaitForSecondsRealtime(.5f);
            Apply();
        }

        private static void Apply()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try { ApplyOnUiThread(activity); }
                        catch (System.Exception exception) { Debug.LogWarning("无法更新 Android 系统栏：" + exception.Message); }
                        finally { activity.Dispose(); }
                    }));
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("无法更新 Android 系统栏：" + exception.Message);
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void ApplyOnUiThread(AndroidJavaObject activity)
        {
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
            using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
            {
                const int flagFullscreen = 0x00000400;
                const int flagDrawSystemBarBackgrounds = unchecked((int)0x80000000);
                const int hideNavigation = 0x00000002;
                const int fullscreen = 0x00000004;
                const int immersive = 0x00000800;
                const int immersiveSticky = 0x00001000;
                const int layoutStable = 0x00000100;
                const int layoutHideNavigation = 0x00000200;
                const int layoutFullscreen = 0x00000400;
                const int lightStatusBar = 0x00002000;
                const int lightNavigationBar = 0x00000010;

                window.Call("clearFlags", flagFullscreen);
                window.Call("addFlags", flagDrawSystemBarBackgrounds);
                // Use an opaque light surface so dark status-bar icons remain
                // readable even on devices that do not draw app content behind it.
                int systemBarColor = unchecked((int)0xFFEAF8FF);
                window.Call("setStatusBarColor", systemBarColor);
                window.Call("setNavigationBarColor", systemBarColor);
                int visibility = decor.Call<int>("getSystemUiVisibility");
                visibility &= ~(hideNavigation | fullscreen | immersive | immersiveSticky | layoutStable | layoutHideNavigation | layoutFullscreen);
                visibility |= lightStatusBar | lightNavigationBar;
                decor.Call("setSystemUiVisibility", visibility);

                int sdk;
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                if (sdk >= 30)
                {
                    window.Call("setDecorFitsSystemWindows", true);
                    using (var controller = window.Call<AndroidJavaObject>("getInsetsController"))
                    using (var insetType = new AndroidJavaClass("android.view.WindowInsets$Type"))
                    {
                        int systemBars = insetType.CallStatic<int>("systemBars");
                        controller.Call("show", systemBars);
                        const int lightAppearance = 8 | 16;
                        controller.Call("setSystemBarsAppearance", lightAppearance, lightAppearance);
                    }
                }
            }
        }
#endif

        public static Rect IncludeSystemInsets(Rect safeArea)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
                using (var rootInsets = decor.Call<AndroidJavaObject>("getRootWindowInsets"))
                using (var insetType = new AndroidJavaClass("android.view.WindowInsets$Type"))
                {
                    if (rootInsets == null) return safeArea;
                    int systemBars = insetType.CallStatic<int>("systemBars");
                    using (var insets = rootInsets.Call<AndroidJavaObject>("getInsets", systemBars))
                    {
                        int left = insets.Get<int>("left");
                        int top = insets.Get<int>("top");
                        int right = insets.Get<int>("right");
                        int bottom = insets.Get<int>("bottom");
                        float xMin = Mathf.Max(safeArea.xMin, left);
                        float yMin = Mathf.Max(safeArea.yMin, bottom);
                        float xMax = Mathf.Min(safeArea.xMax, Screen.width - right);
                        float yMax = Mathf.Min(safeArea.yMax, Screen.height - top);
                        return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
                    }
                }
            }
            catch { return safeArea; }
#else
            return safeArea;
#endif
        }
    }
}
