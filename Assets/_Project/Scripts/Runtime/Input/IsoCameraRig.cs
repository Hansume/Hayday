using UnityEngine;

namespace FarmGame.Input
{
    public sealed class IsoCameraRig : MonoBehaviour
    {
        private const float MinZoom = 3.5f;

        /// <summary>The furthest the camera can zoom out: most of the 36 x 36 map on a wide screen.</summary>
        public const float MaxZoom = 16f;

        private const float DefaultZoom = 7f;

        private Camera _camera;
        private Bounds _worldBounds;
        private bool _initialized;

        public Camera Camera => _camera;

        public void Initialize(Camera targetCamera, Bounds worldBounds)
        {
            _camera = targetCamera != null ? targetCamera : GetComponent<Camera>();
            _worldBounds = worldBounds;
            _initialized = _camera != null;

            if (!_initialized)
            {
                return;
            }

            _camera.orthographic = true;
            _camera.orthographicSize = DefaultZoom;
            var position = _camera.transform.position;
            _camera.transform.position = new Vector3(_worldBounds.center.x, _worldBounds.center.y, position.z);
            ClampPosition();
        }

        /// <summary>Takes the camera to another area, which also becomes where it may pan.</summary>
        public void MoveTo(Bounds worldBounds, Vector3 center, float zoom)
        {
            if (!_initialized)
            {
                return;
            }

            _worldBounds = worldBounds;
            _camera.orthographicSize = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            _camera.transform.position = new Vector3(center.x, center.y, _camera.transform.position.z);
            ClampPosition();
        }

        public void PanScreen(Vector2 screenDelta)
        {
            if (!_initialized || screenDelta.sqrMagnitude <= 0f)
            {
                return;
            }

            var worldPerPixel = (_camera.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
            var position = _camera.transform.position;
            position -= new Vector3(screenDelta.x, screenDelta.y, 0f) * worldPerPixel;
            _camera.transform.position = position;
            ClampPosition();
        }

        public void ZoomWheel(float scrollY, Vector2 screenPosition)
        {
            if (!_initialized || Mathf.Abs(scrollY) < 0.01f)
            {
                return;
            }

            var factor = Mathf.Exp(-scrollY * 0.0012f);
            ZoomAtScreen(factor, screenPosition);
        }

        public void ZoomPinch(float previousDistance, float currentDistance, Vector2 screenMidpoint)
        {
            if (!_initialized || previousDistance <= 0.01f || currentDistance <= 0.01f)
            {
                return;
            }

            var factor = previousDistance / currentDistance;
            ZoomAtScreen(factor, screenMidpoint);
        }

        private void ZoomAtScreen(float factor, Vector2 screenPosition)
        {
            var before = ScreenToWorld(screenPosition);
            var size = Mathf.Clamp(_camera.orthographicSize * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(size, _camera.orthographicSize))
            {
                return;
            }

            _camera.orthographicSize = size;
            var after = ScreenToWorld(screenPosition);
            var delta = before - after;
            _camera.transform.position += new Vector3(delta.x, delta.y, 0f);
            ClampPosition();
        }

        private void LateUpdate()
        {
            ClampPosition();
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            var z = -_camera.transform.position.z;
            return _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, z));
        }

        private void ClampPosition()
        {
            if (!_initialized)
            {
                return;
            }

            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;
            var position = _camera.transform.position;

            if (halfWidth * 2f >= _worldBounds.size.x)
            {
                position.x = _worldBounds.center.x;
            }
            else
            {
                position.x = Mathf.Clamp(
                    position.x,
                    _worldBounds.min.x + halfWidth,
                    _worldBounds.max.x - halfWidth);
            }

            if (halfHeight * 2f >= _worldBounds.size.y)
            {
                position.y = _worldBounds.center.y;
            }
            else
            {
                position.y = Mathf.Clamp(
                    position.y,
                    _worldBounds.min.y + halfHeight,
                    _worldBounds.max.y - halfHeight);
            }

            _camera.transform.position = position;
        }
    }
}
