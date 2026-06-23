using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Match3.View
{
    /// <summary>
    /// The match-3 HUD adapter (UI Toolkit). Driven by the view's cascade sequencer — not by core events —
    /// so the score counts UP in step with the animation and the combo callout fires on the right beat. Owns
    /// the shared win/lose/reward banner. Defensive: missing elements degrade to a console error, never a crash.
    /// </summary>
    public sealed class Match3Hud
    {
        private const float ComboHold = 0.9f;

        private readonly VisualElement _hud;
        private readonly Label _score;
        private readonly Label _goal;
        private readonly Label _moves;
        private readonly Label _combo;
        private readonly VisualElement _overlay;
        private readonly Label _bannerLabel;
        private readonly Label _bannerSub;
        private readonly Button _bannerButton;
        private readonly bool _valid;

        private int _displayedScore;
        private int _targetScore;
        private float _scorePop = 1f;
        private float _comboTimer;
        private float _comboPop = 1f;
        private string _goalName = "Goal";
        private int _goalTotal = 1;
        private Action _onBanner;

        public Match3Hud(UIDocument document)
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) { Debug.LogError("[Match3] HUD unavailable: UIDocument missing."); return; }

            _hud = root.Q("hud");
            _score = root.Q<Label>("hud-score");
            _goal = root.Q<Label>("hud-goal");
            _moves = root.Q<Label>("hud-moves");
            _combo = root.Q<Label>("hud-combo");
            _overlay = root.Q("overlay");
            _bannerLabel = root.Q<Label>("banner-label");
            _bannerSub = root.Q<Label>("banner-sub");
            _bannerButton = root.Q<Button>("banner-button");

            if (_hud == null || _score == null || _goal == null || _moves == null || _combo == null ||
                _overlay == null || _bannerLabel == null || _bannerButton == null)
            {
                Debug.LogError("[Match3] HUD unavailable: proto.uxml is missing match-3 HUD elements.");
                return;
            }
            _valid = true;
            _bannerButton.clicked += () => _onBanner?.Invoke();
            HideBanner();
        }

        /// <summary>Reset the HUD for a fresh level and show the board HUD.</summary>
        public void BeginLevel(string goalName, int goalTotal, int moves)
        {
            if (!_valid) return;
            _goalName = goalName;
            _goalTotal = Mathf.Max(1, goalTotal);
            _displayedScore = 0;
            _targetScore = 0;
            _score.text = "0";
            SetGoalRemaining(goalTotal);
            SetMoves(moves);
            _combo.style.opacity = 0f;
            _hud.style.display = DisplayStyle.Flex;
            HideBanner();
        }

        public void ShowHud(bool show)
        {
            if (_valid) _hud.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Target a new score; the displayed value counts up to it in <see cref="Tick"/>.</summary>
        public void SetScoreTarget(int score)
        {
            if (!_valid) return;
            _targetScore = score;
            _scorePop = 1.3f;
        }

        public void SetGoalRemaining(int remaining)
        {
            if (!_valid) return;
            int collected = Mathf.Clamp(_goalTotal - remaining, 0, _goalTotal);
            _goal.text = $"{_goalName} {collected}/{_goalTotal}";
        }

        public void SetMoves(int moves)
        {
            if (!_valid) return;
            _moves.text = moves.ToString();
            _moves.EnableInClassList("hud__moves--low", moves <= 3);
        }

        /// <summary>Punch in a centred callout ("Combo x3!", "Nice!"). Fades out over ~1s.</summary>
        public void ShowCombo(string text)
        {
            if (!_valid) return;
            _combo.text = text;
            _comboTimer = ComboHold;
            _comboPop = 1.5f;
            _combo.style.opacity = 1f;
        }

        public void Tick(float dt)
        {
            if (!_valid) return;

            if (_displayedScore != _targetScore)
            {
                int diff = _targetScore - _displayedScore;
                int step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(diff) * dt * 7f));
                _displayedScore += diff > 0 ? Mathf.Min(step, diff) : Mathf.Max(-step, diff);
                _score.text = _displayedScore.ToString();
            }
            _scorePop = Mathf.Lerp(_scorePop, 1f, 1f - Mathf.Exp(-14f * dt));
            _score.style.scale = new StyleScale(new Scale(new Vector3(_scorePop, _scorePop, 1f)));

            if (_comboTimer > 0f)
            {
                _comboTimer -= dt;
                _comboPop = Mathf.Lerp(_comboPop, 1f, 1f - Mathf.Exp(-12f * dt));
                _combo.style.scale = new StyleScale(new Scale(new Vector3(_comboPop, _comboPop, 1f)));
                _combo.style.opacity = Mathf.Clamp01(_comboTimer / 0.3f);
                if (_comboTimer <= 0f) _combo.style.opacity = 0f;
            }
        }

        public void ShowBanner(string title, string sub, string buttonText, Action onButton)
        {
            if (!_valid) return;
            _bannerLabel.text = title;
            if (_bannerSub != null)
            {
                _bannerSub.text = sub ?? string.Empty;
                _bannerSub.style.display = string.IsNullOrEmpty(sub) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            _bannerButton.text = buttonText;
            _onBanner = onButton;
            _overlay.style.display = DisplayStyle.Flex;
        }

        public void HideBanner()
        {
            if (_valid) _overlay.style.display = DisplayStyle.None;
        }
    }
}
