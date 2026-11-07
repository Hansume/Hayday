using UnityEngine;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// For a scrolling row: when the content is narrower than its viewport, keep it centred
    /// and switch scrolling off (so the ScrollRect doesn't pull it back to the left edge).
    /// Wider content scrolls as usual.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CenterWhenNarrow : MonoBehaviour
    {
        public RectTransform Viewport;
        public ScrollRect Scroll;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            if (Viewport == null)
            {
                return;
            }

            var spare = Viewport.rect.width - _rect.rect.width;
            var fits = spare >= 0f;
            if (Scroll != null && Scroll.horizontal == fits)
            {
                Scroll.horizontal = !fits;
                if (!fits)
                {
                    _rect.anchoredPosition = new Vector2(0f, _rect.anchoredPosition.y);
                }
            }

            if (fits)
            {
                _rect.anchoredPosition = new Vector2(spare * 0.5f, _rect.anchoredPosition.y);
            }
        }
    }
}
