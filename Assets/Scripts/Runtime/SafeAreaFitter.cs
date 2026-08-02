using UnityEngine;

namespace DecisionDisc
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;

        private void Awake() { rectTransform = GetComponent<RectTransform>(); Apply(); }
        private void Update()
        {
            if (lastSafeArea != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            Rect area = Screen.safeArea;
            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
            lastSafeArea = area;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
