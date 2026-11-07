using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>Fades an overlay in and springs its window up from slightly smaller whenever it opens.</summary>
    public sealed class UIPopIn : MonoBehaviour
    {
        private const float Duration = 0.22f;
        private const float StartScale = 0.86f;

        public RectTransform Target;

        private CanvasGroup _group;
        private float _elapsed;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (_elapsed >= Duration)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(_elapsed / Duration));
        }

        private void Apply(float progress)
        {
            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01(progress * 2f);
            }

            if (Target != null)
            {
                var scale = Mathf.LerpUnclamped(StartScale, 1f, EaseOutBack(progress));
                Target.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.6f;
            var x = t - 1f;
            return 1f + x * x * ((overshoot + 1f) * x + overshoot);
        }
    }

    /// <summary>Squashes a button slightly while it is held down.</summary>
    public sealed class UIPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.93f;
        private const float Speed = 3f;

        private Selectable _selectable;
        private float _scale = 1f;
        private float _target = 1f;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable == null || _selectable.IsInteractable())
            {
                _target = PressedScale;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _target = 1f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _target = 1f;
        }

        private void Update()
        {
            if (Mathf.Approximately(_scale, _target))
            {
                return;
            }

            _scale = Mathf.MoveTowards(_scale, _target, Time.unscaledDeltaTime * Speed);
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }

        private void OnDisable()
        {
            _scale = 1f;
            _target = 1f;
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>A quick "pop" scale bounce, e.g. on the coin counter when coins change.</summary>
    public sealed class UIPunch : MonoBehaviour
    {
        private const float Duration = 0.25f;
        private const float Amount = 0.14f;

        private float _elapsed = Duration;

        public void Play()
        {
            _elapsed = 0f;
        }

        private void Update()
        {
            if (_elapsed >= Duration)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / Duration);
            var scale = 1f + Amount * Mathf.Sin(t * Mathf.PI) * (1f - t);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
