using UnityEngine;

namespace DecisionDisc
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastRawSafeArea;
        private Vector2Int lastScreen;
        private float nextInsetRefresh;

        private void Awake() { rectTransform = GetComponent<RectTransform>(); Apply(); }
        private void Update()
        {
            if (lastRawSafeArea != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height || Time.unscaledTime >= nextInsetRefresh) Apply();
        }

        private void Apply()
        {
            Rect rawArea = Screen.safeArea;
            Rect area = AndroidSystemBars.IncludeSystemInsets(rawArea);
            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
            lastRawSafeArea = rawArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            nextInsetRefresh = Time.unscaledTime + 1f;
        }
    }
}
