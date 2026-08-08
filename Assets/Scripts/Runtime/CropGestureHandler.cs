using UnityEngine;
using UnityEngine.EventSystems;

namespace DecisionDisc
{
    public sealed class CropGestureHandler : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public RectTransform Target;
        public RectTransform Viewport;
        public float Zoom { get; private set; } = 1f;
        public Vector2 NormalizedOffset { get; private set; }
        private Vector2 baseSize;
        private float lastPinchDistance;
        private float canvasScale = 1f;

        public void Configure(RectTransform target, RectTransform viewport, int textureWidth, int textureHeight)
        {
            Target = target; Viewport = viewport; Zoom = 1f; NormalizedOffset = Vector2.zero;
            Canvas canvas = GetComponentInParent<Canvas>(); canvasScale = canvas == null ? 1f : Mathf.Max(.01f, canvas.scaleFactor);
            Vector2 viewportSize = viewport.rect.size;
            viewportSize.x = Mathf.Max(1f, viewportSize.x);
            viewportSize.y = Mathf.Max(1f, viewportSize.y);
            float coverScale = Mathf.Max(viewportSize.x / Mathf.Max(1, textureWidth), viewportSize.y / Mathf.Max(1, textureHeight));
            baseSize = new Vector2(textureWidth * coverScale, textureHeight * coverScale);
            Target.sizeDelta = baseSize; Target.anchoredPosition = Vector2.zero; Target.localScale = Vector3.one;
            lastPinchDistance = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Input.touchCount > 1 || Target == null) return;
            Target.anchoredPosition += eventData.delta / canvasScale;
            ClampAndUpdate();
        }

        public void OnScroll(PointerEventData eventData) { SetZoom(Zoom + eventData.scrollDelta.y * .12f); }

        private void Update()
        {
            if (Target == null || Input.touchCount != 2) { lastPinchDistance = 0f; return; }
            Touch first = Input.GetTouch(0), second = Input.GetTouch(1);
            float distance = Vector2.Distance(first.position, second.position);
            if (lastPinchDistance > 0f) SetZoom(Zoom * distance / lastPinchDistance);
            lastPinchDistance = distance;
        }

        private void SetZoom(float value)
        {
            Zoom = Mathf.Clamp(value, 1f, 4f);
            Target.localScale = Vector3.one * Zoom;
            ClampAndUpdate();
        }

        private void ClampAndUpdate()
        {
            Vector2 viewportSize = Viewport.rect.size;
            Vector2 scaled = baseSize * Zoom;
            Vector2 maxPan = new Vector2(Mathf.Max(0f, (scaled.x - viewportSize.x) * .5f), Mathf.Max(0f, (scaled.y - viewportSize.y) * .5f));
            Vector2 position = Target.anchoredPosition;
            position.x = Mathf.Clamp(position.x, -maxPan.x, maxPan.x);
            position.y = Mathf.Clamp(position.y, -maxPan.y, maxPan.y);
            Target.anchoredPosition = position;
            NormalizedOffset = new Vector2(maxPan.x <= .01f ? 0f : position.x / maxPan.x, maxPan.y <= .01f ? 0f : position.y / maxPan.y);
        }
    }
}
