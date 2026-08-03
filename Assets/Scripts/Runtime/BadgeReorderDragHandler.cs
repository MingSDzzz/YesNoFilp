using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DecisionDisc
{
    public sealed class BadgeReorderDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform card;
        private RectTransform container;
        private LayoutElement layoutElement;
        private CanvasGroup canvasGroup;
        private Canvas canvas;
        private int originalIndex;
        private Action<int> dropped;

        public void Bind(RectTransform targetCard, Transform targetContainer, Action<int> onDropped)
        {
            card = targetCard;
            container = targetContainer as RectTransform;
            dropped = onDropped;
            layoutElement = card.GetComponent<LayoutElement>();
            canvasGroup = card.gameObject.GetComponent<CanvasGroup>() ?? card.gameObject.AddComponent<CanvasGroup>();
            canvas = card.GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (card == null || container == null) return;
            originalIndex = card.GetSiblingIndex();
            layoutElement.ignoreLayout = true;
            canvasGroup.alpha = .9f;
            canvasGroup.blocksRaycasts = false;
            card.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (card == null || !layoutElement.ignoreLayout) return;
            float scale = canvas == null ? 1f : canvas.scaleFactor;
            card.anchoredPosition += eventData.delta / Mathf.Max(.01f, scale);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (card == null || container == null || !layoutElement.ignoreLayout) return;
            int targetIndex = 0;
            for (int i = 0; i < container.childCount; i++)
            {
                RectTransform other = container.GetChild(i) as RectTransform;
                if (other == null || other == card) continue;
                float centerY = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, other.TransformPoint(other.rect.center)).y;
                if (eventData.position.y < centerY) targetIndex++;
            }

            layoutElement.ignoreLayout = false;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            card.SetSiblingIndex(Mathf.Clamp(targetIndex, 0, Mathf.Max(0, container.childCount - 1)));
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            if (targetIndex != originalIndex) dropped?.Invoke(targetIndex);
        }
    }
}
