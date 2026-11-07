using UnityEngine;

namespace FarmGame.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || screenSize != _lastScreenSize)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            var width = Mathf.Max(1f, Screen.width);
            var height = Mathf.Max(1f, Screen.height);
            var min = _lastSafeArea.position;
            var max = _lastSafeArea.position + _lastSafeArea.size;

            _rect.anchorMin = new Vector2(min.x / width, min.y / height);
            _rect.anchorMax = new Vector2(max.x / width, max.y / height);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
