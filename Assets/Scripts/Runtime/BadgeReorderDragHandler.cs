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
        private GameObject placeholder;
        private int originalIndex;
        private float lockedLocalX;
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
            placeholder = new GameObject("BadgeDropPlaceholder", typeof(RectTransform), typeof(LayoutElement));
            placeholder.transform.SetParent(container, false);
            LayoutElement placeholderLayout = placeholder.GetComponent<LayoutElement>();
            placeholderLayout.preferredHeight = layoutElement.preferredHeight;
            placeholderLayout.minHeight = layoutElement.minHeight;
            placeholderLayout.flexibleWidth = 1f;
            placeholder.transform.SetSiblingIndex(originalIndex);

            Vector3 worldPosition = card.position;
            layoutElement.ignoreLayout = true;
            canvasGroup.alpha = .9f;
            canvasGroup.blocksRaycasts = false;
            card.SetAsLastSibling();
            card.position = worldPosition;
            lockedLocalX = card.localPosition.x;
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (card == null || !layoutElement.ignoreLayout) return;
            float scale = canvas == null ? 1f : canvas.scaleFactor;
            Vector3 local = card.localPosition;
            local.y += eventData.delta.y / Mathf.Max(.01f, scale);
            local.x = lockedLocalX;
            float minY = container.rect.yMin + card.rect.height * card.pivot.y;
            float maxY = container.rect.yMax - card.rect.height * (1f - card.pivot.y);
            if (maxY >= minY) local.y = Mathf.Clamp(local.y, minY, maxY);
            card.localPosition = local;

            int previewIndex = TargetIndex(eventData);
            if (placeholder != null && placeholder.transform.GetSiblingIndex() != previewIndex)
            {
                placeholder.transform.SetSiblingIndex(previewIndex);
                LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (card == null || container == null || !layoutElement.ignoreLayout) return;
            int targetIndex = placeholder == null ? originalIndex : placeholder.transform.GetSiblingIndex();
            if (placeholder != null)
            {
                placeholder.transform.SetParent(null, false);
                Destroy(placeholder);
            }
            placeholder = null;
            card.SetSiblingIndex(Mathf.Clamp(targetIndex, 0, Mathf.Max(0, container.childCount - 1)));
            layoutElement.ignoreLayout = false;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            if (targetIndex != originalIndex) dropped?.Invoke(targetIndex);
        }

        private int TargetIndex(PointerEventData eventData)
        {
            int targetIndex = 0;
            for (int i = 0; i < container.childCount; i++)
            {
                RectTransform other = container.GetChild(i) as RectTransform;
                if (other == null || other == card || (placeholder != null && other.gameObject == placeholder)) continue;
                float centerY = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, other.TransformPoint(other.rect.center)).y;
                if (eventData.position.y < centerY) targetIndex++;
            }
            return Mathf.Clamp(targetIndex, 0, Mathf.Max(0, container.childCount - 2));
        }
    }
}
