using System.Collections.Generic;
using FarmGame.Content;
using FarmGame.Visitors;
using UnityEngine;

namespace FarmGame.View
{
    /// <summary>
    /// Visitors on the farm. They walk in along the front-left edge, wait at their spot just
    /// off the field, and walk back out when they're served, sent away or tired of waiting.
    /// The farm view follows the visitor list; it never changes it.
    /// </summary>
    public sealed partial class FarmView
    {
        private const string VisitorSpritePrefix = "Art/Placeholders/visitor_";

        /// <summary>Painted visitors (Tools > Farm Game > Rebuild Painted Art), used over the placeholders.</summary>
        private const string PaintedVisitorSpritePrefix = "Art/Icons/visitor_";
        private const float VisitorWalkSpeed = 1.8f;

        /// <summary>How far past the front-left edge visitors stand, in cells.</summary>
        private const float VisitorEdgeOffset = 0.35f;

        /// <summary>How far apart neighbouring visitor spots are, in cells.</summary>
        private const float VisitorSpotSpacing = 1.5f;

        /// <summary>How far visitors walk along their row to reach the middle of it, in cells.</summary>
        private const float VisitorEntranceDistance = 8f;

        private enum VisitorPhase
        {
            Arriving,
            Waiting,
            Leaving
        }

        private sealed class VisitorActor
        {
            public int Id;
            public Transform Root;
            public SpriteRenderer Renderer;
            public Vector3 Spot;
            public Vector3 Entrance;
            public VisitorPhase Phase;
            public float Time;
            public float Phase0;
        }

        private readonly List<VisitorActor> _visitorActors = new List<VisitorActor>();
        private readonly HashSet<int> _liveVisitorIds = new HashSet<int>();
        private readonly Sprite[] _visitorSprites = new Sprite[VisitorService.LookCount];
        private VisitorService _visitorService;
        private Transform _visitorsRoot;

        private void InitializeVisitors(VisitorService visitors)
        {
            _visitorService = visitors;
            _visitorsRoot = new GameObject("Visitors").transform;
            _visitorsRoot.SetParent(transform, false);

            for (var i = 0; i < _visitorSprites.Length; i++)
            {
                var painted = Resources.Load<Sprite>(PaintedVisitorSpritePrefix + i);
                _visitorSprites[i] = painted != null ? painted : Resources.Load<Sprite>(VisitorSpritePrefix + i);
            }

            // Visitors already waiting when the farm loads are simply there.
            if (_visitorService != null)
            {
                for (var i = 0; i < _visitorService.Visitors.Count; i++)
                {
                    SpawnVisitor(_visitorService.Visitors[i], alreadyThere: true);
                }
            }
        }

        /// <summary>Where a visitor spot is: a close row centred on the front-left edge.</summary>
        public Vector3 GetVisitorSpotWorldPosition(int spot)
        {
            if (Grid == null)
            {
                return Vector3.zero;
            }

            var along = VisitorRowMiddle + (spot - (VisitorBalance.MaxSpots - 1) * 0.5f) * VisitorSpotSpacing;
            return Grid.CellToWorld(new Vector2(along, VisitorRow));
        }

        /// <summary>Where visitors come from and go back to: well to the left of their row.</summary>
        private Vector3 GetVisitorEntrance()
        {
            return Grid.CellToWorld(new Vector2(VisitorRowMiddle - VisitorEntranceDistance, VisitorRow));
        }

        /// <summary>The middle of the visitors' row: in line with the middle of the start area.</summary>
        private float VisitorRowMiddle => _layout.StartArea.x + (_layout.StartArea.width - 1) * 0.5f;

        /// <summary>
        /// Just past the owned land in front of the start area, so visitors never stand on land
        /// that can be built on. It moves forward as that land is bought.
        /// </summary>
        private float VisitorRow
        {
            get
            {
                // Every column the row covers counts: one bought section can be narrower than the row.
                var reach = (VisitorBalance.MaxSpots - 1) * 0.5f * VisitorSpotSpacing;
                var front = 0;
                for (var x = Mathf.FloorToInt(VisitorRowMiddle - reach); x <= Mathf.CeilToInt(VisitorRowMiddle + reach); x++)
                {
                    front = Mathf.Max(front, _session.Land.GetFrontEdge(x));
                }

                return front - 0.5f + VisitorEdgeOffset;
            }
        }

        /// <summary>Land was bought: visitors walk to the new edge, and leave and arrive from there.</summary>
        private void MoveVisitorsToFrontEdge()
        {
            var entrance = GetVisitorEntrance();
            for (var i = 0; i < _visitorActors.Count; i++)
            {
                var actor = _visitorActors[i];
                var visitor = _visitorService != null ? _visitorService.Find(actor.Id) : null;
                actor.Entrance = entrance;
                if (visitor == null)
                {
                    continue;
                }

                actor.Spot = GetVisitorSpotWorldPosition(visitor.Spot);
                if (actor.Phase == VisitorPhase.Waiting && actor.Root.position != actor.Spot)
                {
                    actor.Phase = VisitorPhase.Arriving;
                }
            }
        }

        /// <summary>The visitor under a tap, if they've arrived and aren't leaving.</summary>
        public bool TryGetVisitorAtWorld(Vector3 worldPosition, out int visitorId)
        {
            visitorId = -1;
            var bestY = float.MaxValue;
            for (var i = 0; i < _visitorActors.Count; i++)
            {
                var actor = _visitorActors[i];
                if (actor.Phase == VisitorPhase.Leaving || actor.Renderer == null)
                {
                    continue;
                }

                var bounds = actor.Renderer.bounds;
                var point = new Vector3(worldPosition.x, worldPosition.y, bounds.center.z);

                // The frontmost (lowest on screen) visitor wins when two overlap.
                if (bounds.Contains(point) && actor.Root.position.y < bestY)
                {
                    bestY = actor.Root.position.y;
                    visitorId = actor.Id;
                }
            }

            return visitorId >= 0;
        }

        /// <summary>Just above a waiting visitor's head, for their speech bubble.</summary>
        public bool TryGetVisitorBubblePosition(int visitorId, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            for (var i = 0; i < _visitorActors.Count; i++)
            {
                var actor = _visitorActors[i];
                if (actor.Id == visitorId && actor.Phase == VisitorPhase.Waiting && actor.Renderer != null)
                {
                    var bounds = actor.Renderer.bounds;
                    worldPosition = new Vector3(bounds.center.x, bounds.max.y + 0.15f, 0f);
                    return true;
                }
            }

            return false;
        }

        private void UpdateVisitors(float deltaTime)
        {
            if (_visitorService == null || Grid == null)
            {
                return;
            }

            // New visitors walk in; ones no longer on the list walk out.
            _liveVisitorIds.Clear();
            for (var i = 0; i < _visitorService.Visitors.Count; i++)
            {
                var visitor = _visitorService.Visitors[i];
                _liveVisitorIds.Add(visitor.Id);
                if (FindActor(visitor.Id) == null)
                {
                    SpawnVisitor(visitor, alreadyThere: false);
                }
            }

            for (var i = _visitorActors.Count - 1; i >= 0; i--)
            {
                var actor = _visitorActors[i];
                if (actor.Phase != VisitorPhase.Leaving && !_liveVisitorIds.Contains(actor.Id))
                {
                    actor.Phase = VisitorPhase.Leaving;
                }

                if (AnimateVisitor(actor, deltaTime))
                {
                    Destroy(actor.Root.gameObject);
                    _visitorActors.RemoveAt(i);
                }
            }
        }

        private VisitorActor FindActor(int id)
        {
            for (var i = 0; i < _visitorActors.Count; i++)
            {
                if (_visitorActors[i].Id == id && _visitorActors[i].Phase != VisitorPhase.Leaving)
                {
                    return _visitorActors[i];
                }
            }

            return null;
        }

        private void SpawnVisitor(Visitor visitor, bool alreadyThere)
        {
            // The root tracks where the visitor stands; the picture is a child so it can hop
            // and breathe without moving them.
            var root = new GameObject($"Visitor_{visitor.Id}_{visitor.Name}").transform;
            root.SetParent(_visitorsRoot, false);
            var picture = new GameObject("Sprite").transform;
            picture.SetParent(root, false);

            var renderer = picture.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _visitorSprites[Mathf.Clamp(visitor.Look, 0, _visitorSprites.Length - 1)];
            renderer.sortingLayerName = EntitySortingLayer;

            var actor = new VisitorActor
            {
                Id = visitor.Id,
                Root = root,
                Renderer = renderer,
                Spot = GetVisitorSpotWorldPosition(visitor.Spot),
                Entrance = GetVisitorEntrance(),
                Phase = alreadyThere ? VisitorPhase.Waiting : VisitorPhase.Arriving,
                Phase0 = visitor.Id * 1.7f
            };

            root.position = alreadyThere ? actor.Spot : actor.Entrance;
            if (!alreadyThere)
            {
                renderer.color = new Color(1f, 1f, 1f, 0f);
            }

            _visitorActors.Add(actor);
            UpdateVisitorSorting(actor);
        }

        /// <summary>Moves one visitor along. Returns true once a leaving visitor is gone.</summary>
        private bool AnimateVisitor(VisitorActor actor, float deltaTime)
        {
            actor.Time += deltaTime;
            var position = actor.Root.position;
            Vector3 target;
            switch (actor.Phase)
            {
                case VisitorPhase.Arriving:
                    target = actor.Spot;
                    break;
                case VisitorPhase.Leaving:
                    target = actor.Entrance;
                    break;
                default:
                    target = actor.Spot;
                    break;
            }

            var walking = actor.Phase != VisitorPhase.Waiting;
            if (walking)
            {
                position = Vector3.MoveTowards(position, target, VisitorWalkSpeed * deltaTime);
                actor.Root.position = position;

                // Fade in at the start of the walk in, out at the end of the walk out.
                var fromEntrance = Vector3.Distance(position, actor.Entrance);
                var alpha = Mathf.Clamp01(fromEntrance / 1.2f);
                actor.Renderer.color = new Color(1f, 1f, 1f, alpha);

                if (actor.Phase == VisitorPhase.Arriving && position == target)
                {
                    actor.Phase = VisitorPhase.Waiting;
                    actor.Renderer.color = Color.white;
                }
            }

            // A little hop while walking; gentle breathing while waiting.
            var sprite = actor.Renderer.transform;
            if (walking)
            {
                var hop = Mathf.Abs(Mathf.Sin(actor.Time * 9f)) * 0.08f;
                sprite.localScale = Vector3.one;
                sprite.localPosition = new Vector3(0f, hop, 0f);
            }
            else
            {
                var breathe = 1f + 0.025f * Mathf.Sin(actor.Time * 2.4f + actor.Phase0);
                sprite.localScale = new Vector3(1f, breathe, 1f);
                sprite.localPosition = Vector3.zero;
            }

            UpdateVisitorSorting(actor);
            return actor.Phase == VisitorPhase.Leaving && position == target;
        }

        /// <summary>Visitors sort with buildings by how far forward they stand.</summary>
        private void UpdateVisitorSorting(VisitorActor actor)
        {
            var cell = Grid.WorldToCell(actor.Root.position);
            actor.Renderer.sortingOrder = 50 + (cell.x + cell.y) * 10 + 8;
        }
    }
}
