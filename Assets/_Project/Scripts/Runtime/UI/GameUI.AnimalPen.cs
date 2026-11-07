using System.Collections;
using System.Collections.Generic;
using FarmGame.Buildings;
using FarmGame.Content;
using FarmGame.Core;
using FarmGame.Farming;
using FarmGame.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FarmGame.UI
{
    /// <summary>
    /// The animal pen window: the animated scene, and adding animals. Tapping a hungry animal pops up
    /// the feed on it, with how much is in storage; tapping the feed feeds that animal and closes it. A fed animal shows a timer above it, then its
    /// product when ready (a grown fish shows its price instead). Tapping the animal or that button collects.
    /// </summary>
    public sealed partial class GameUI
    {
        private static readonly Vector2 AnimalIconButtonSize = new Vector2(76f, 76f);
        private static readonly Vector2 AnimalTextBubbleSize = new Vector2(132f, 52f);
        private static readonly Vector2 AnimalFeedPopupSize = new Vector2(120f, 120f);

        private void BuildAnimalPen()
        {
            var panel = UIFactory.CreateWindow(
                _canvasRect,
                "AnimalPenOverlay",
                "AnimalPenPanel",
                "Animal Home",
                new Vector2(1100f, 860f),
                CloseAnimalPen,
                out _animalPenTitle);
            _animalPenOverlay = panel.parent.gameObject;

            var countPill = UIFactory.CreateDecor("AnimalPenCountPill", panel, UIFactory.PillSprite, Color.white);
            UIFactory.Place(countPill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(300f, 56f));
            _animalPenCountText = UIFactory.CreateTitleText("AnimalPenCount", countPill.transform, string.Empty, 28);
            UIFactory.Stretch(_animalPenCountText.rectTransform, 12f, 2f, 12f, 2f);

            // A wooden frame around the scene where the animals live.
            var frame = UIFactory.CreateDecor("StageFrame", panel, UIFactory.HeaderSprite, Color.white);
            UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 500f));

            _animalPenStage = UIFactory.CreateRect("AnimalPenStage", panel);
            UIFactory.Place(_animalPenStage, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 460f));
            _animalPenStage.gameObject.AddComponent<RectMask2D>();

            _animalPenBackground = UIFactory.CreateImage(
                "PenBackground",
                _animalPenStage,
                null,
                Color.white);
            _animalPenBackground.raycastTarget = false;
            UIFactory.Stretch(_animalPenBackground.rectTransform);

            _animalPenAnimals = UIFactory.CreateRect("AnimalPenAnimals", _animalPenStage);
            UIFactory.Stretch(_animalPenAnimals);

            // Bubbles sit in their own layer above the animals and follow them each frame.
            // They aren't children of the animals because fish flip to face the way they swim,
            // which would mirror the bubble's text.
            _animalPenStatusRoot = UIFactory.CreateRect("AnimalStatusBubbles", _animalPenStage);
            UIFactory.Stretch(_animalPenStatusRoot);

            BuildAnimalFeedTray(_animalPenStage);

            _animalPenAddButton = UIFactory.CreateButton("AddAnimalButton", panel, "Add Animal", new Vector2(420f, 82f), 32, ButtonStyle.Primary);
            UIFactory.Place(_animalPenAddButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(420f, 82f));
            _animalPenAddButton.onClick.AddListener(AddAnimalToActivePen);
            _animalPenAddLabel = _animalPenAddButton.GetComponentInChildren<Text>();

            // Fish ponds pick which fish to buy from a list over the pen.
            BuildFishPicker(panel);
        }

        /// <summary>The feed's icon and stock, popped up on the tapped animal. Hidden until one is tapped.</summary>
        private void BuildAnimalFeedTray(Transform stage)
        {
            var tray = UIFactory.CreateImage("AnimalFeedTray", stage, UIFactory.CardSprite, Color.white);
            tray.raycastTarget = true;
            UIFactory.Place(tray.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, AnimalFeedPopupSize);
            var tap = tray.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(FeedTappedAnimal);
            tray.gameObject.AddComponent<UIPopIn>().Target = tray.rectTransform;

            _animalFeedIcon = UIFactory.CreateIcon("Icon", tray.transform, null, new Vector2(104f, 104f));
            UIFactory.Place(_animalFeedIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 104f));

            _animalFeedCount = UIFactory.CreatePill("CountPill", tray.transform, "0", 28, new Vector2(84f, 42f), out var countPill);
            UIFactory.Place(countPill.rectTransform, new Vector2(1f, 0f), new Vector2(12f, -10f), new Vector2(84f, 42f));

            _animalFeedTray = tray.gameObject;
            _animalFeedTray.SetActive(false);
        }

        private void ShowAnimalFeedTray(AnimalVisual animal)
        {
            var feed = _session.GetFeedFor(_activeAnimalBuilding);
            if (_animalFeedTray == null || feed == null || animal == null)
            {
                return;
            }

            // Moving to another animal pops it up again there.
            if (_animalFeedTarget != animal)
            {
                _animalFeedTray.SetActive(false);
            }

            _animalFeedTarget = animal;
            _animalFeedIcon.sprite = LoadFeedIcon(feed);
            _animalFeedTray.SetActive(true);
            UpdateAnimalStatusBubble(animal);
            UpdateAnimalFeedTray();
        }

        private void HideAnimalFeedTray()
        {
            _animalFeedTarget = null;
            if (_animalFeedTray != null)
            {
                _animalFeedTray.SetActive(false);
            }
        }

        private void UpdateAnimalFeedTray()
        {
            if (_animalFeedTray == null || !_animalFeedTray.activeSelf)
            {
                return;
            }

            var feed = _session.GetFeedFor(_activeAnimalBuilding);
            var count = feed != null ? _session.Inventory.GetCount(feed.Id) : 0;
            var text = UIFactory.FormatNumber(count);
            if (_animalFeedCount.text != text)
            {
                _animalFeedCount.text = text;
            }

            _animalFeedIcon.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);

            if (_session.GetAnimalStatus(_activeAnimalBuilding, _animalFeedTarget.Index, out _) != AnimalStatus.Hungry)
            {
                HideAnimalFeedTray();
                return;
            }

            PositionAnimalFeedTray();
        }

        /// <summary>
        /// Just above the animal's head.
        /// Follows swimming fish, but ignores the small bobs of the others so it doesn't jiggle.
        /// </summary>
        private void PositionAnimalFeedTray()
        {
            if (_animalFeedTarget == null || _animalFeedTarget.Rect == null)
            {
                return;
            }

            var animal = _animalFeedTarget.Kind == AnimalKind.Fish
                ? _animalFeedTarget.Rect.anchoredPosition
                : _animalFeedTarget.Home;
            var animalHalf = _animalFeedTarget.Size * 0.5f * _animalFeedTarget.Scale;
            var half = AnimalFeedPopupSize * 0.5f;
            var extents = GetAnimalPenHalfExtents(half.x + 6f, half.y + 6f);

            ((RectTransform)_animalFeedTray.transform).anchoredPosition = new Vector2(
                Mathf.Clamp(animal.x, -extents.x, extents.x),
                Mathf.Clamp(animal.y + animalHalf.y + half.y, -extents.y, extents.y));
        }

        /// <summary>Feeds the animal the feed popped up on. Closes the popup if that worked; out of feed, it stays and says so.</summary>
        private void FeedTappedAnimal()
        {
            if (_animalFeedTarget == null || _activeAnimalBuilding == null)
            {
                return;
            }

            _session.TryFeedAnimal(_activeAnimalBuilding, _animalFeedTarget.Index, out var message);
            ShowToast(message);
            UpdateAnimalFeedTray();
        }

        public bool IsAnimalPenVisible =>
            _animalPenOverlay != null && _animalPenOverlay.activeSelf;

        public void ShowAnimalPen(PlacedBuilding building)
        {
            if (_animalPenOverlay == null || building == null || building.Definition == null)
            {
                return;
            }

            if (!building.Definition.CanHouseAnimals)
            {
                ShowToast($"{building.Definition.DisplayName} does not house animals.");
                return;
            }

            CloseBuildingShop();
            CloseInventory();
            CloseFeedGrinder();
            HideCropShop();
            HideScythe();
            if (IsPlacementActive)
            {
                CancelBuildingPlacement();
            }

            _activeAnimalBuilding = building;
            _animalPenTime = 0f;
            CloseFishPicker();
            HideAnimalFeedTray();
            if (_animalPenBackground != null)
            {
                _animalPenBackground.sprite = LoadPenBackground(building.Definition.AnimalKind);
                _animalPenBackground.color = Color.white;
            }

            _animalPenOverlay.SetActive(true);
            RebuildAnimalPenVisuals();
            UpdateAnimalPenControls();
        }

        public void CloseAnimalPen()
        {
            if (_animalPenOverlay != null)
            {
                _animalPenOverlay.SetActive(false);
            }

            HideAnimalFeedTray();
            _activeAnimalBuilding = null;
            CloseFishPicker();
            ClearAnimalPenVisuals();
        }

        private void RebuildAnimalPenVisuals()
        {
            ClearAnimalPenVisuals();
            if (_activeAnimalBuilding == null || _animalPenAnimals == null)
            {
                return;
            }

            var definition = _activeAnimalBuilding.Definition;
            var kindSprite = LoadAnimalSprite(definition.AnimalKind);
            var count = _activeAnimalBuilding.AnimalCount;
            // Spaced so their status bubbles don't overlap. Chickens stand in two rows of three.
            var chickenSlots = new[]
            {
                new Vector2(-240f, -140f), new Vector2(0f, -140f), new Vector2(240f, -140f),
                new Vector2(-240f, 60f), new Vector2(0f, 60f), new Vector2(240f, 60f)
            };
            var cowSlots = new[] { -290f, 0f, 290f };
            var goatSlots = new[] { -240f, 0f, 240f };

            for (var i = 0; i < count; i++)
            {
                // Each fish shows its own kind; other animals all look alike.
                var fish = _session.GetFishAt(_activeAnimalBuilding, i);
                var sprite = fish != null && fish.Sprite != null ? fish.Sprite : kindSprite;
                if (sprite == null)
                {
                    continue;
                }

                var animalId = GetAnimalId(definition.AnimalKind);
                var image = UIFactory.CreateImage(
                    $"Animal_{animalId}_{i}",
                    _animalPenAnimals,
                    sprite,
                    Color.white);
                image.preserveAspect = true;
                image.raycastTarget = true;

                var visual = new AnimalVisual
                {
                    Rect = image.rectTransform,
                    Kind = definition.AnimalKind,
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    Index = i,
                    Size = GetAnimalSize(definition.AnimalKind)
                };

                // Tap an animal to show the feed or collect its product. Reads the index when tapped,
                // because selling a fish moves the ones after it up a place.
                var tap = image.gameObject.AddComponent<Button>();
                tap.transition = Selectable.Transition.None;
                tap.onClick.AddListener(() => OnAnimalTapped(visual.Index));

                switch (definition.AnimalKind)
                {
                    case AnimalKind.Fish:
                        visual.Home = new Vector2(
                            Random.Range(-300f, 300f),
                            Random.Range(-60f, 80f));
                        var direction = Random.insideUnitCircle;
                        if (direction.sqrMagnitude < 0.01f)
                        {
                            direction = Vector2.right;
                        }

                        visual.Velocity = direction.normalized * Random.Range(55f, 105f);
                        visual.Scale = Random.Range(0.85f, 1.15f);
                        visual.TurnTimer = Random.Range(1.4f, 3.5f);
                        break;
                    case AnimalKind.Chicken:
                        visual.Home = chickenSlots[Mathf.Min(i, chickenSlots.Length - 1)];
                        break;
                    case AnimalKind.Cow:
                        visual.Home = new Vector2(
                            cowSlots[Mathf.Min(i, cowSlots.Length - 1)],
                            -80f);
                        break;
                    case AnimalKind.Goat:
                        visual.Home = new Vector2(
                            goatSlots[Mathf.Min(i, goatSlots.Length - 1)],
                            -60f);
                        break;
                }

                UIFactory.SetAnchored(
                    image.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    visual.Home,
                    visual.Size);
                image.rectTransform.localScale = new Vector3(
                    visual.Scale,
                    visual.Scale,
                    1f);

                CreateAnimalStatusBubble(visual);
                _animalPenVisuals.Add(visual);
                UpdateAnimalStatusBubble(visual);
            }
        }

        private void CreateAnimalStatusBubble(AnimalVisual visual)
        {
            var root = UIFactory.CreateRect($"AnimalStatus_{visual.Index}", _animalPenStatusRoot);
            UIFactory.Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, AnimalTextBubbleSize);

            // The bubble is a button, like the animal under it. It collects the animal's product
            // or says how long is left.
            var background = UIFactory.CreateImage("Background", root, UIFactory.PillSprite, Color.white);
            background.raycastTarget = true;
            UIFactory.Stretch(background.rectTransform);
            var tap = background.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => OnAnimalTapped(visual.Index));
            background.gameObject.AddComponent<UIPressFeedback>();

            // Icon and label sit on the background so they squash with it when pressed.
            var icon = UIFactory.CreateIcon("Icon", background.transform, null, new Vector2(50f, 50f));
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 2f), new Vector2(50f, 50f));

            var label = UIFactory.CreateTitleText("Label", background.transform, string.Empty, 20, TextAnchor.MiddleLeft);
            UIFactory.Stretch(label.rectTransform, 56f, 2f, 8f, 2f);

            visual.StatusRoot = root;
            visual.StatusBackground = background;
            visual.StatusIcon = icon;
            visual.StatusLabel = label;
        }

        private void UpdateAnimalStatusBubble(AnimalVisual visual)
        {
            if (visual.StatusRoot == null || visual.Rect == null || _activeAnimalBuilding == null)
            {
                return;
            }

            var status = _session.GetAnimalStatus(_activeAnimalBuilding, visual.Index, out var remaining);

            // Hungry animals show nothing: tapping one brings up the feed.
            var hasBubble = status != AnimalStatus.Hungry;
            if (visual.StatusRoot.gameObject.activeSelf != hasBubble)
            {
                visual.StatusRoot.gameObject.SetActive(hasBubble);
            }

            if (!hasBubble)
            {
                return;
            }

            switch (status)
            {
                case AnimalStatus.Producing:
                    SetAnimalBubbleLayout(visual, false);
                    visual.StatusBackground.sprite = UIFactory.PillSprite;
                    visual.StatusBackground.color = new Color(1f, 1f, 1f, 0.85f);
                    visual.StatusIcon.sprite = UIFactory.Icon("icon_clock");
                    SetCountdownText(visual.StatusLabel, remaining);
                    visual.StatusLabel.color = UIFactory.Cream;
                    visual.StatusRoot.localScale = Vector3.one;
                    break;
                default:
                    // Ready: a product to collect. Fish have no product, so a grown fish shows
                    // what it sells for.
                    var product = _session.GetProductFor(_activeAnimalBuilding);
                    var grownFish = _session.GetFishAt(_activeAnimalBuilding, visual.Index);
                    SetAnimalBubbleLayout(visual, grownFish == null);
                    visual.StatusBackground.sprite = UIFactory.ButtonSprite;
                    visual.StatusBackground.color = UIFactory.Gold;
                    visual.StatusIcon.sprite = grownFish != null
                        ? UIFactory.Icon("icon_coin")
                        : product != null ? product.Icon : null;
                    SetPlainText(
                        visual.StatusLabel,
                        grownFish != null ? $"+{UIFactory.FormatNumber(grownFish.SellPrice)}" : string.Empty);
                    visual.StatusLabel.color = UIFactory.Cream;
                    var bounce = 1f + 0.08f * Mathf.Abs(Mathf.Sin(_animalPenTime * 5f + visual.Phase));
                    visual.StatusRoot.localScale = new Vector3(bounce, bounce, 1f);
                    break;
            }

            visual.StatusIcon.enabled = visual.StatusIcon.sprite != null;

            // Follow the animal, sitting just above its head.
            var scale = Mathf.Abs(visual.Rect.localScale.y);
            visual.StatusRoot.anchoredPosition = visual.Rect.anchoredPosition +
                new Vector2(0f, visual.Size.y * 0.5f * scale + visual.StatusRoot.sizeDelta.y * 0.5f);
        }

        /// <summary>A square button with a big icon, or a pill with a small icon and text.</summary>
        private static void SetAnimalBubbleLayout(AnimalVisual visual, bool iconOnly)
        {
            var size = iconOnly ? AnimalIconButtonSize : AnimalTextBubbleSize;
            if (visual.StatusRoot.sizeDelta == size)
            {
                return;
            }

            visual.StatusRoot.sizeDelta = size;
            if (iconOnly)
            {
                UIFactory.Place(visual.StatusIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            }
            else
            {
                UIFactory.Place(visual.StatusIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 2f), new Vector2(50f, 50f));
            }
        }

        private void ClearAnimalPenVisuals()
        {
            HideAnimalFeedTray();
            _animalPenVisuals.Clear();
            DestroyChildren(_animalPenAnimals);
            DestroyChildren(_animalPenStatusRoot);
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void AddAnimalToActivePen()
        {
            if (_activeAnimalBuilding == null || _session == null)
            {
                return;
            }

            if (_activeAnimalBuilding.Definition.SellsAnimals)
            {
                OpenFishPicker();
                return;
            }

            if (_session.TryAddAnimal(_activeAnimalBuilding, out var message))
            {
                RebuildAnimalPenVisuals();
            }

            UpdateAnimalPenControls();
            ShowToast(message);
        }

        private void OnAnimalTapped(int index)
        {
            if (_activeAnimalBuilding == null)
            {
                return;
            }

            var countBefore = _activeAnimalBuilding.AnimalCount;
            if (_session.GetAnimalStatus(_activeAnimalBuilding, index, out _) != AnimalStatus.Hungry)
            {
                // Ready: collect the product, or sell the grown fish. Not ready yet: the
                // session explains how long is left.
                _session.TryCollectAnimalProduct(_activeAnimalBuilding, index, out var message);
                ShowToast(message);
            }

            // Selling a fish takes it out of the pond.
            if (_activeAnimalBuilding.AnimalCount != countBefore)
            {
                RemoveAnimalVisual(index);
                UpdateAnimalPenControls();
                return;
            }

            // Only a hungry animal needs the feed: one still producing just explains how long is left.
            if (_session.GetAnimalStatus(_activeAnimalBuilding, index, out _) != AnimalStatus.Hungry)
            {
                return;
            }

            for (var i = 0; i < _animalPenVisuals.Count; i++)
            {
                if (_animalPenVisuals[i].Index == index)
                {
                    ShowAnimalFeedTray(_animalPenVisuals[i]);
                    break;
                }
            }
        }

        /// <summary>The sold fish and its bubble go; the rest keep swimming from where they are.</summary>
        private void RemoveAnimalVisual(int sold)
        {
            for (var i = _animalPenVisuals.Count - 1; i >= 0; i--)
            {
                var visual = _animalPenVisuals[i];
                if (visual.Index == sold)
                {
                    if (_animalFeedTarget == visual)
                    {
                        HideAnimalFeedTray();
                    }

                    _animalPenVisuals.RemoveAt(i);
                    DestroyVisualObject(visual.Rect);
                    DestroyVisualObject(visual.StatusRoot);
                }
                else if (visual.Index > sold)
                {
                    // As in the building, the animals after it move up a place.
                    visual.Index--;
                    visual.Rect.name = $"Animal_{GetAnimalId(visual.Kind)}_{visual.Index}";
                    visual.StatusRoot.name = $"AnimalStatus_{visual.Index}";
                }
            }
        }

        private static void DestroyVisualObject(Component visualObject)
        {
            visualObject.gameObject.SetActive(false);
            Destroy(visualObject.gameObject);
        }

        private void UpdateAnimalPenControls()
        {
            if (_activeAnimalBuilding == null || _animalPenTitle == null)
            {
                return;
            }

            var definition = _activeAnimalBuilding.Definition;
            var count = _activeAnimalBuilding.AnimalCount;
            var capacity = definition.AnimalCapacity;
            var full = count >= capacity;

            _animalPenTitle.text = definition.DisplayName;
            if (_animalPenCountText != null)
            {
                _animalPenCountText.text =
                    $"{definition.AnimalDisplayName} {count} / {capacity}";
            }

            if (_animalPenAddButton != null && definition.SellsAnimals)
            {
                // Prices differ per fish, so the button just opens the list of fish.
                _animalPenAddButton.interactable = !full;
                if (_animalPenAddLabel != null)
                {
                    _animalPenAddLabel.text = full ? "Full" : "Buy Fish";
                }

                UIFactory.SetButtonStyle(_animalPenAddButton, full ? ButtonStyle.Muted : ButtonStyle.Primary);
            }
            else if (_animalPenAddButton != null)
            {
                _animalPenAddButton.interactable = !full;
                var affordable = _session.Wallet.Coins >= definition.AnimalCost;
                if (_animalPenAddLabel != null)
                {
                    _animalPenAddLabel.text = full
                        ? "Full"
                        : affordable
                            ? $"Add {definition.AnimalDisplayName} ({definition.AnimalCost})"
                            : $"Need {definition.AnimalCost}";
                }

                UIFactory.SetButtonStyle(
                    _animalPenAddButton,
                    full ? ButtonStyle.Muted : affordable ? ButtonStyle.Primary : ButtonStyle.Danger);
            }
        }

        private void UpdateAnimalPen()
        {
            if (_animalPenOverlay == null || !_animalPenOverlay.activeSelf)
            {
                return;
            }

            var deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _animalPenTime += deltaTime;
            var fishExtents = GetAnimalPenHalfExtents(75f, 150f);

            for (var i = 0; i < _animalPenVisuals.Count; i++)
            {
                var visual = _animalPenVisuals[i];
                if (visual.Rect == null)
                {
                    continue;
                }

                switch (visual.Kind)
                {
                    case AnimalKind.Fish:
                        UpdateFishVisual(visual, fishExtents, deltaTime);
                        break;
                    case AnimalKind.Chicken:
                        UpdateChickenVisual(visual);
                        break;
                    case AnimalKind.Cow:
                        UpdateCowVisual(visual);
                        break;
                    case AnimalKind.Goat:
                        UpdateGoatVisual(visual);
                        break;
                }

                UpdateAnimalStatusBubble(visual);
            }

            UpdateAnimalFeedTray();
        }

        private void UpdateFishVisual(
            AnimalVisual visual,
            Vector2 halfExtents,
            float deltaTime)
        {
            var velocity = visual.Velocity;
            if (velocity.sqrMagnitude < 0.001f)
            {
                velocity = Vector2.right * 60f;
            }

            visual.TurnTimer -= deltaTime;
            if (visual.TurnTimer <= 0f)
            {
                var angle = Random.Range(-0.75f, 0.75f) * Mathf.Deg2Rad;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                velocity = new Vector2(
                    velocity.x * cos - velocity.y * sin,
                    velocity.x * sin + velocity.y * cos);
                velocity = velocity.normalized * Random.Range(55f, 105f);
                visual.TurnTimer = Random.Range(1.4f, 3.5f);
            }

            var position = visual.Rect.anchoredPosition + velocity * deltaTime;
            if (position.x < -halfExtents.x)
            {
                position.x = -halfExtents.x;
                velocity.x = Mathf.Abs(velocity.x);
            }
            else if (position.x > halfExtents.x)
            {
                position.x = halfExtents.x;
                velocity.x = -Mathf.Abs(velocity.x);
            }

            if (position.y < -halfExtents.y)
            {
                position.y = -halfExtents.y;
                velocity.y = Mathf.Abs(velocity.y);
            }
            else if (position.y > halfExtents.y)
            {
                position.y = halfExtents.y;
                velocity.y = -Mathf.Abs(velocity.y);
            }

            visual.Velocity = velocity;
            visual.Rect.anchoredPosition = position;
            var facing = velocity.x >= 0f ? 1f : -1f;
            visual.Rect.localScale = new Vector3(
                facing * visual.Scale,
                visual.Scale,
                1f);
            var tilt = Mathf.Clamp(velocity.y * 0.12f, -10f, 10f);
            visual.Rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void UpdateChickenVisual(AnimalVisual visual)
        {
            var bob = Mathf.Sin(_animalPenTime * 2.1f + visual.Phase) * 2.5f;
            visual.Rect.anchoredPosition = visual.Home + new Vector2(0f, bob);
            var sway = Mathf.Sin(_animalPenTime * 0.8f + visual.Phase) * 1.2f;
            visual.Rect.localRotation = Quaternion.Euler(0f, 0f, sway);
        }

        private void UpdateCowVisual(AnimalVisual visual)
        {
            var breathe = 1f + Mathf.Sin(_animalPenTime * 1.6f + visual.Phase) * 0.015f;
            visual.Rect.localScale = new Vector3(
                visual.Scale,
                visual.Scale * breathe,
                1f);
            var bob = Mathf.Sin(_animalPenTime * 1.1f + visual.Phase) * 1.5f;
            visual.Rect.anchoredPosition = visual.Home + new Vector2(0f, bob);
        }

        /// <summary>Goats are lively: every few seconds each one does a little hop.</summary>
        private void UpdateGoatVisual(AnimalVisual visual)
        {
            var cycle = Mathf.Repeat(_animalPenTime * 0.45f + visual.Phase / (Mathf.PI * 2f), 1f);
            var hop = cycle < 0.18f ? Mathf.Sin(cycle / 0.18f * Mathf.PI) * 26f : 0f;
            var bob = Mathf.Sin(_animalPenTime * 1.8f + visual.Phase) * 1.5f;
            visual.Rect.anchoredPosition = visual.Home + new Vector2(0f, hop + bob);
            var tilt = cycle < 0.18f ? Mathf.Sin(cycle / 0.18f * Mathf.PI) * -6f : 0f;
            visual.Rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private Vector2 GetAnimalPenHalfExtents(float marginX, float marginY)
        {
            var width = _animalPenStage != null && _animalPenStage.rect.width > 1f
                ? _animalPenStage.rect.width
                : 900f;
            var height = _animalPenStage != null && _animalPenStage.rect.height > 1f
                ? _animalPenStage.rect.height
                : 460f;
            return new Vector2(width * 0.5f - marginX, height * 0.5f - marginY);
        }

        private static Vector2 GetAnimalSize(AnimalKind kind)
        {
            switch (kind)
            {
                case AnimalKind.Fish:
                    return new Vector2(130f, 100f);
                case AnimalKind.Chicken:
                    return new Vector2(120f, 120f);
                case AnimalKind.Cow:
                    return new Vector2(190f, 190f);
                case AnimalKind.Goat:
                    return new Vector2(160f, 160f);
                default:
                    return new Vector2(100f, 100f);
            }
        }

        private static string GetAnimalId(AnimalKind kind)
        {
            switch (kind)
            {
                case AnimalKind.Fish:
                    return "fish";
                case AnimalKind.Chicken:
                    return "chicken";
                case AnimalKind.Cow:
                    return "cow";
                case AnimalKind.Goat:
                    return "goat";
                default:
                    return "animal";
            }
        }

        private static Sprite LoadAnimalSprite(AnimalKind kind)
        {
            switch (kind)
            {
                // UIFactory.Icon prefers painted art (NewAssets/Animals) over the placeholder.
                case AnimalKind.Fish:
                    return UIFactory.Icon("animal_fish");
                case AnimalKind.Chicken:
                    return UIFactory.Icon("animal_chicken");
                case AnimalKind.Cow:
                    return UIFactory.Icon("animal_cow");
                case AnimalKind.Goat:
                    return UIFactory.Icon("animal_goat");
                default:
                    return null;
            }
        }

        private static Sprite LoadPenBackground(AnimalKind kind)
        {
            switch (kind)
            {
                case AnimalKind.Fish:
                    return Resources.Load<Sprite>(FishPenBackgroundPath);
                case AnimalKind.Chicken:
                    return Resources.Load<Sprite>(CoopPenBackgroundPath);
                case AnimalKind.Cow:
                    return Resources.Load<Sprite>(StablePenBackgroundPath);
                case AnimalKind.Goat:
                    return Resources.Load<Sprite>(GoatPenBackgroundPath);
                default:
                    return null;
            }
        }
    }
}
