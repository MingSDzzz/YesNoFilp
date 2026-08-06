using System.Collections;
using UnityEngine;

namespace DecisionDisc
{
    /// <summary>
    /// Keeps the two badge faces in a restrained head-to-head pose and turns that
    /// pose into a short launch cue before the physical disc animation begins.
    /// </summary>
    public sealed class BadgeCollisionMotion : MonoBehaviour
    {
        private RectTransform yesFace;
        private RectTransform noFace;
        private RectTransform impact;
        private CanvasGroup canvasGroup;
        private Vector2 yesPosition;
        private Vector2 noPosition;
        private float yesRotation;
        private float noRotation;
        private float idleOrigin;
        private bool releasing;

        public void Configure(RectTransform yes, RectTransform no, RectTransform impactMark)
        {
            yesFace = yes;
            noFace = no;
            impact = impactMark;
            yesPosition = yes == null ? Vector2.zero : yes.anchoredPosition;
            noPosition = no == null ? Vector2.zero : no.anchoredPosition;
            yesRotation = yes == null ? 0f : yes.localEulerAngles.z;
            noRotation = no == null ? 0f : no.localEulerAngles.z;
            if (yesRotation > 180f) yesRotation -= 360f;
            if (noRotation > 180f) noRotation -= 360f;
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            idleOrigin = Time.unscaledTime;
            releasing = false;
            ResetPose();
        }

        private void Update()
        {
            if (releasing || yesFace == null || noFace == null) return;
            float time = Time.unscaledTime - idleOrigin;
            float wave = Mathf.Sin(time * 1.35f);
            float counterWave = Mathf.Sin(time * 1.35f + Mathf.PI);
            yesFace.anchoredPosition = yesPosition + new Vector2(wave * 2f, wave * 4f);
            noFace.anchoredPosition = noPosition + new Vector2(counterWave * 2f, counterWave * 4f);
            yesFace.localRotation = Quaternion.Euler(0f, 0f, yesRotation + wave * .65f);
            noFace.localRotation = Quaternion.Euler(0f, 0f, noRotation + counterWave * .65f);
            yesFace.localScale = Vector3.one * (1f + wave * .006f);
            noFace.localScale = Vector3.one * (1f + counterWave * .006f);
            if (impact != null) impact.localScale = Vector3.one * (1f + Mathf.Sin(time * 2.1f) * .045f);
        }

        public IEnumerator PlayRelease()
        {
            if (yesFace == null || noFace == null) yield break;
            releasing = true;
            Vector2 yesStart = yesFace.anchoredPosition;
            Vector2 noStart = noFace.anchoredPosition;
            Quaternion yesStartRotation = yesFace.localRotation;
            Quaternion noStartRotation = noFace.localRotation;
            const float duration = .16f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                yesFace.anchoredPosition = yesStart + Vector2.left * (54f * eased);
                noFace.anchoredPosition = noStart + Vector2.right * (54f * eased);
                yesFace.localRotation = yesStartRotation * Quaternion.Euler(0f, 0f, -3f * eased);
                noFace.localRotation = noStartRotation * Quaternion.Euler(0f, 0f, 3f * eased);
                float faceScale = Mathf.Lerp(1f, .95f, eased);
                yesFace.localScale = noFace.localScale = Vector3.one * faceScale;
                if (impact != null) impact.localScale = Vector3.one * Mathf.Lerp(1f, 1.65f, eased);
                if (canvasGroup != null) canvasGroup.alpha = 1f - Mathf.SmoothStep(.55f, 1f, progress);
                yield return null;
            }
        }

        private void ResetPose()
        {
            if (yesFace != null)
            {
                yesFace.anchoredPosition = yesPosition;
                yesFace.localRotation = Quaternion.Euler(0f, 0f, yesRotation);
                yesFace.localScale = Vector3.one;
            }
            if (noFace != null)
            {
                noFace.anchoredPosition = noPosition;
                noFace.localRotation = Quaternion.Euler(0f, 0f, noRotation);
                noFace.localScale = Vector3.one;
            }
            if (impact != null) impact.localScale = Vector3.one;
        }
    }
}
