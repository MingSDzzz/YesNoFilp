using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DecisionDisc
{
    [RequireComponent(typeof(Image))]
    public sealed class ChargeThrowButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public Action<float, string, float> Released;
        public Text Label;
        public Image Fill;
        private bool charging;
        private float downTime;
        private float maxPressure;
        private float maxRadius;
        private float motion;
        private Vector2 lastPosition;

        public void OnPointerDown(PointerEventData eventData)
        {
            charging = true;
            downTime = Time.unscaledTime;
            maxPressure = ReadPressure(eventData);
            maxRadius = ReadRadius(eventData);
            motion = 0f;
            lastPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!charging) return;
            maxPressure = Mathf.Max(maxPressure, ReadPressure(eventData));
            maxRadius = Mathf.Max(maxRadius, ReadRadius(eventData));
            motion += Vector2.Distance(lastPosition, eventData.position);
            lastPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!charging) return;
            charging = false;
            float heldSeconds = Mathf.Clamp(Time.unscaledTime - downTime, 0f, 3f);
            float duration = Mathf.Clamp01(heldSeconds / 3f);
            maxPressure = Mathf.Max(maxPressure, ReadPressure(eventData));
            float strength;
            string source;
            if (maxPressure > 0.01f && Mathf.Abs(maxPressure - 1f) > 0.01f)
            {
                strength = Mathf.Clamp01(maxPressure);
                source = "pressure";
            }
            else
            {
                float area = Mathf.Clamp01(maxRadius / 80f);
                float releaseMotion = Mathf.Clamp01(motion / Mathf.Max(200f, Screen.dpi * 1.5f));
                strength = Mathf.Clamp01(duration * 0.65f + area * 0.2f + releaseMotion * 0.15f);
                source = maxRadius > 0.01f ? "hold+area+release" : "hold+release";
            }
            SetVisual(0f);
            Released?.Invoke(strength, source, heldSeconds);
        }

        private void Update()
        {
            if (!charging) return;
            SetVisual(Mathf.Clamp01((Time.unscaledTime - downTime) / 3f));
        }

        private void SetVisual(float value)
        {
            if (Fill != null) Fill.fillAmount = value;
            if (Label != null) Label.text = value > 0f ? "蓄力中  " + Mathf.RoundToInt(value * 100f) + "%" : "按住蓄力，松开投掷";
        }

        private static float ReadPressure(PointerEventData eventData)
        {
            if (eventData.pointerId >= 0 && Input.touchSupported)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId == eventData.pointerId && touch.maximumPossiblePressure > 0f)
                        return touch.pressure / touch.maximumPossiblePressure;
                }
            }
            return eventData.pressure;
        }

        private static float ReadRadius(PointerEventData eventData)
        {
            if (eventData.pointerId >= 0 && Input.touchSupported)
            {
                for (int i = 0; i < Input.touchCount; i++)
                    if (Input.GetTouch(i).fingerId == eventData.pointerId) return Input.GetTouch(i).radius;
            }
            return 0f;
        }
    }
}
