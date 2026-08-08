using UnityEngine;
using UnityEngine.EventSystems;

namespace DecisionDisc
{
    /// <summary>Gives tappable uGUI controls a gentle, tactile "soft rubber" press response.</summary>
    public sealed class SoftPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = .96f;
        [SerializeField] private float responseSpeed = 18f;

        private Vector3 restingScale;
        private bool pressed;

        private void Awake() { restingScale = transform.localScale; }
        private void OnEnable() { restingScale = transform.localScale; pressed = false; }
        private void OnDisable() { transform.localScale = restingScale; pressed = false; }
        private void Update()
        {
            Vector3 target = pressed ? restingScale * pressedScale : restingScale;
            transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-responseSpeed * Time.unscaledDeltaTime));
        }

        public void OnPointerDown(PointerEventData eventData) { pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }
        public void OnPointerExit(PointerEventData eventData) { pressed = false; }
    }
}
