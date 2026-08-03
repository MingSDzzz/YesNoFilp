#if UNITY_ANDROID && !UNITY_EDITOR
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;

namespace DecisionDisc
{
    [Preserve]
    internal static class UnitySplashSkipper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void BeforeSplashScreen()
        {
            Task.Run(StopSplashScreen);
        }

        private static void StopSplashScreen()
        {
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
        }
    }
}
#endif
