using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sokoban.UI
{
    // Keyboard focus must stay visible when navigating beyond the scroll viewport.
    public sealed class ScrollSelection : MonoBehaviour, ISelectHandler
    {
        public void OnSelect(BaseEventData eventData)
        {
            var scroll = GetComponentInParent<ScrollRect>();
            if (!scroll || !scroll.viewport || !scroll.content) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, transform);
            var viewport = scroll.viewport.rect;
            float offset = bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y :
                bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : 0;
            scroll.StopMovement();
            scroll.content.anchoredPosition += new Vector2(0, offset);
        }
    }
}
