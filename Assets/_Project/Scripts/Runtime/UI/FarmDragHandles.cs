using UnityEngine;
using UnityEngine.EventSystems;

namespace FarmGame.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class CropDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameUI _owner;
        private string _cropId;

        public void Initialize(GameUI owner, string cropId)
        {
            _owner = owner;
            _cropId = cropId;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_owner != null && !string.IsNullOrEmpty(_cropId))
            {
                _owner.BeginCropDrag(_cropId, eventData, gameObject);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_owner != null)
            {
                _owner.DragCrop(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_owner != null)
            {
                _owner.EndCropDrag(eventData);
            }
        }
    }

    [RequireComponent(typeof(RectTransform))]
    public sealed class ScytheDragHandle : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler
    {
        private GameUI _owner;

        public void Initialize(GameUI owner)
        {
            _owner = owner;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_owner != null)
            {
                _owner.BeginScytheDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_owner != null)
            {
                _owner.DragScythe(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_owner != null)
            {
                _owner.EndScytheDrag(eventData);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner != null)
            {
                _owner.NotifyScytheTapped();
            }
        }
    }
}
