using System;
using System.Collections;
using Match3.Config;
using Match3.Core;
using Proto.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Match3.Meta
{
    /// <summary>
    /// The Monopoly-Match-style bonus board: a looped ring of reward tiles a token hops around as the player
    /// spends dice earned from match-3 wins. Lands grant coins / stars / dice; enough stars unlock the next
    /// level. Persists after every roll. Shares the one scene with the board (toggled by the bootstrap).
    /// </summary>
    public sealed class MetaBoardView
    {
        private const float Radius = 2.9f;
        private const float HopDur = 0.22f;
        private const float TileScale = 0.95f;

        private readonly SpriteFactory _sprites;
        private readonly Camera _camera;
        private readonly Transform _root;
        private readonly TweenRunner _tweens;
        private readonly ParticleFactory _particles;
        private readonly SfxPlayer _sfx;
        private readonly FloatingTextLayer _floaters;
        private readonly IDieRoller _roller;
        private readonly IMetaStore _store;
        private readonly Action _onProceed;

        private readonly VisualElement _metaHud;
        private readonly Label _coins, _stars, _dice, _info;
        private readonly Button _roll, _next;
        private readonly bool _valid;

        private MetaProgress _progress;
        private MetaBoard _board;
        private MetaBoardLayout _layout;
        private GameObject[] _tileObjects;
        private Vector3[] _tilePositions;
        private GameObject _token;
        private bool _busy;

        public MetaBoardView(SpriteFactory sprites, Camera camera, Transform parent, TweenRunner tweens,
                             ParticleFactory particles, SfxPlayer sfx, FloatingTextLayer floaters,
                             UIDocument document, IDieRoller roller, IMetaStore store, Action onProceed)
        {
            _sprites = sprites;
            _camera = camera;
            _tweens = tweens;
            _particles = particles;
            _sfx = sfx;
            _floaters = floaters;
            _roller = roller;
            _store = store;
            _onProceed = onProceed;

            var rootGo = new GameObject("MetaRoot");
            rootGo.transform.SetParent(parent, false);
            _root = rootGo.transform;
            _root.gameObject.SetActive(false);

            VisualElement uiRoot = document != null ? document.rootVisualElement : null;
            if (uiRoot != null)
            {
                _metaHud = uiRoot.Q("meta-hud");
                _coins = uiRoot.Q<Label>("meta-coins");
                _stars = uiRoot.Q<Label>("meta-stars");
                _dice = uiRoot.Q<Label>("meta-dice");
                _info = uiRoot.Q<Label>("meta-info");
                _roll = uiRoot.Q<Button>("meta-roll");
                _next = uiRoot.Q<Button>("meta-next");
            }
            _valid = _metaHud != null && _coins != null && _stars != null && _dice != null && _roll != null && _next != null;
            if (!_valid) { Debug.LogError("[Match3] Meta HUD unavailable: proto.uxml is missing meta elements."); return; }

            _roll.clicked += Roll;
            _next.clicked += () => { _sfx.PlayHook("ui_click"); _onProceed?.Invoke(); };
        }

        public void Load(MetaProgress progress, MetaBoard board, MetaBoardLayout layout)
        {
            _progress = progress;
            _board = board;
            _layout = layout;
            _busy = false;
            BuildRing();
            RefreshStats();
            RefreshButtons();
            if (_valid) _info.text = progress.Dice > 0 ? "Spend dice to move the token!" : "Out of dice.";
        }

        public void SetVisible(bool visible)
        {
            _root.gameObject.SetActive(visible);
            if (_valid) _metaHud.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---- Interaction --------------------------------------------------------

        private void Roll()
        {
            if (!_valid || _busy || _progress == null || _progress.Dice <= 0) return;
            RollOutcome outcome = _progress.SpendDie(_board, _roller);
            if (!outcome.Rolled) return;
            _store.Save(_progress.ToState()); // persist immediately — the spend is atomic
            _busy = true;
            _sfx.PlayHook("ui_click");
            _tweens.Run(AnimateRoll(outcome));
        }

        private IEnumerator AnimateRoll(RollOutcome outcome)
        {
            _sfx.PlayHook("dice_roll");
            _dice.text = "Dice " + _progress.Dice; // already decremented
            _info.text = "Rolled " + outcome.Face + "!";

            int index = outcome.FromIndex;
            for (int hop = 0; hop < outcome.Face; hop++)
            {
                index = (index + 1) % _board.Count;
                Vector3 from = _token.transform.position;
                Vector3 to = _tilePositions[index];
                _tweens.ArcMove(_token.transform, from, to, 0.6f, HopDur);
                _sfx.PlayHook("token_hop", 1f + 0.04f * hop);
                yield return WaitUnscaled(HopDur);
                _tweens.PunchScale(_token.transform, 1.12f, 0.1f);
            }

            yield return WaitUnscaled(0.08f);
            yield return ApplyReward(outcome);

            RefreshButtons();
            _busy = false;
        }

        private IEnumerator ApplyReward(RollOutcome outcome)
        {
            Vector3 at = _tilePositions[outcome.ToIndex];
            var tile = _tileObjects[outcome.ToIndex];
            if (tile != null) _tweens.PunchScale(tile.transform, 1.3f, 0.3f);

            switch (outcome.Landed.Kind)
            {
                case RewardKind.Coins:
                    _particles.Burst(at, TileColor(RewardKind.Coins), 16);
                    _floaters.Spawn("+" + outcome.CoinsGained, at, new Color(1f, 0.85f, 0.3f), 54f);
                    _sfx.PlayHook("reward_coin");
                    yield return CountUp(_coins, "Coins ", _progress.Coins - outcome.CoinsGained, _progress.Coins);
                    break;
                case RewardKind.Star:
                    _particles.Confetti(at, TileColor(RewardKind.Star), 26);
                    _floaters.Spawn("+" + outcome.StarsGained + " Star", at, new Color(0.5f, 0.8f, 1f), 50f);
                    _sfx.PlayHook("reward_star");
                    RefreshStats();
                    break;
                case RewardKind.Dice:
                    _particles.Burst(at, TileColor(RewardKind.Dice), 16);
                    _floaters.Spawn("+" + outcome.DiceGained + " Dice", at, new Color(0.6f, 0.95f, 0.6f), 50f);
                    _sfx.PlayHook("reward_coin");
                    break;
                default:
                    _floaters.Spawn("—", at, new Color(0.8f, 0.8f, 0.85f), 44f);
                    break;
            }

            RefreshStats();

            if (outcome.Unlocked)
            {
                _sfx.PlayHook("level_unlock");
                _info.text = "Level " + (outcome.LevelAfter + 1) + " unlocked!";
                if (_token != null) _tweens.PunchScale(_token.transform, 1.4f, 0.4f);
            }
            else if (_progress.Dice <= 0)
            {
                _info.text = "Out of dice — keep going!";
            }
        }

        private IEnumerator CountUp(Label label, string prefix, int from, int to)
        {
            float t = 0f, dur = 0.45f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                int v = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.Clamp01(t / dur)));
                label.text = prefix + v;
                yield return null;
            }
            label.text = prefix + to;
        }

        // ---- Build & refresh ----------------------------------------------------

        private void BuildRing()
        {
            if (_tileObjects != null)
                foreach (var t in _tileObjects) if (t != null) UnityEngine.Object.Destroy(t);
            if (_token != null) UnityEngine.Object.Destroy(_token);

            int count = _board.Count;
            _tileObjects = new GameObject[count];
            _tilePositions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                float ang = (90f - i * 360f / count) * Mathf.Deg2Rad; // start at top, clockwise
                _tilePositions[i] = new Vector3(Mathf.Cos(ang) * Radius, Mathf.Sin(ang) * Radius, 0f);

                MetaTileDef def = _layout.GetDef(i);
                var go = new GameObject("Tile_" + i);
                go.transform.SetParent(_root, false);
                go.transform.position = _tilePositions[i];
                go.transform.localScale = Vector3.one * TileScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprites.Get(ShapeFor(def.Kind));
                sr.color = TileColor(def.Kind);
                sr.sortingOrder = 1;
                _tileObjects[i] = go;
            }

            _token = new GameObject("Token");
            _token.transform.SetParent(_root, false);
            _token.transform.localScale = Vector3.one * 0.6f;
            var tsr = _token.AddComponent<SpriteRenderer>();
            tsr.sprite = _sprites.Get(SpriteShape.Circle);
            tsr.color = new Color(0.98f, 0.97f, 0.95f);
            tsr.sortingOrder = 3;
            int tokenIndex = ((_progress.TokenIndex % count) + count) % count;
            _token.transform.position = _tilePositions[tokenIndex];
        }

        private void RefreshStats()
        {
            if (!_valid) return;
            _coins.text = "Coins " + _progress.Coins;
            _stars.text = "Stars " + _progress.Stars + "/" + _progress.StarsPerUnlock;
            _dice.text = "Dice " + _progress.Dice;
        }

        private void RefreshButtons()
        {
            if (!_valid) return;
            bool canRoll = _progress.Dice > 0;
            _roll.style.display = canRoll ? DisplayStyle.Flex : DisplayStyle.None;
            _next.style.display = canRoll ? DisplayStyle.None : DisplayStyle.Flex;
            _next.text = _progress.LevelIndex > 0 ? "Next Level" : "Play Again";
        }

        private static SpriteShape ShapeFor(RewardKind kind)
        {
            switch (kind)
            {
                case RewardKind.Coins: return SpriteShape.Circle;
                case RewardKind.Star: return SpriteShape.Star;
                case RewardKind.Dice: return SpriteShape.Hexagon;
                default: return SpriteShape.RoundedSquare;
            }
        }

        private static Color TileColor(RewardKind kind)
        {
            switch (kind)
            {
                case RewardKind.Coins: return new Color(0.96f, 0.80f, 0.28f);
                case RewardKind.Star: return new Color(0.34f, 0.62f, 0.96f);
                case RewardKind.Dice: return new Color(0.42f, 0.82f, 0.45f);
                default: return new Color(0.40f, 0.43f, 0.52f);
            }
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}
