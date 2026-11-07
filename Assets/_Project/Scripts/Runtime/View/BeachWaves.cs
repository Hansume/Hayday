using System.Collections.Generic;
using UnityEngine;

namespace FarmGame.View
{
    /// <summary>Keeps the water alive: the surf laps in and out, highlights drift and a boat bobs.</summary>
    public sealed class BeachWaves : MonoBehaviour
    {
        /// <summary>How far the surf slides. It lies under the sand, so only its edge moves.</summary>
        private const float LapDistance = 0.14f;
        private const float LapSpeed = 0.75f;
        private const float BobHeight = 0.05f;
        private const float BobSpeed = 1.3f;

        private readonly struct Drift
        {
            public readonly Transform Target;
            public readonly Vector3 Home;
            public readonly Vector3 Step;
            public readonly float Speed;

            public Drift(Transform target, Vector2 step, float speed)
            {
                Target = target;
                Home = target.localPosition;
                Step = step;
                Speed = speed;
            }
        }

        private readonly List<Drift> _drifts = new List<Drift>();
        private Transform _surf;
        private Vector3 _surfHome;
        private Vector3 _surfLap;
        private Transform _boat;
        private Vector3 _boatHome;

        /// <summary>The surf slides back and forth along <paramref name="lapDirection"/>, across the coast.</summary>
        public void SetSurf(Transform surf, Vector2 lapDirection)
        {
            _surf = surf;
            _surfHome = surf.localPosition;
            _surfLap = lapDirection.normalized * LapDistance;
        }

        public void SetBoat(Transform boat)
        {
            _boat = boat;
            _boatHome = boat.localPosition;
        }

        /// <summary>
        /// Slides a tiled layer along <paramref name="step"/>, one whole tile, then starts over: the
        /// layer looks the same at both ends, so the jump can't be seen.
        /// </summary>
        public void AddDrift(Transform target, Vector2 step, float speed)
        {
            _drifts.Add(new Drift(target, step, speed));
        }

        private void Update()
        {
            var time = Time.time;
            if (_surf != null)
            {
                _surf.localPosition = _surfHome + _surfLap * Mathf.Sin(time * LapSpeed);
            }

            if (_boat != null)
            {
                _boat.localPosition = _boatHome + Vector3.up * (Mathf.Sin(time * BobSpeed) * BobHeight);
            }

            for (var i = 0; i < _drifts.Count; i++)
            {
                var drift = _drifts[i];
                drift.Target.localPosition = drift.Home + drift.Step * (Mathf.Repeat(time * drift.Speed, 1f) - 0.5f);
            }
        }
    }
}
