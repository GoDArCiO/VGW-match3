using System;
using Match3.Config;
using Match3.Core;
using Match3.Meta;
using Proto.Core;
using Proto.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Match3.View
{
    /// <summary>
    /// The single composition root (the only place that news up the object graph — no singletons / service
    /// locator / FindObjectOfType). Builds the persistent toolkit once, owns the Board↔Meta screen flow,
    /// level progression, and persistence. Serialized config is wired headlessly by the editor SceneSetup
    /// tool, never the inspector.
    /// </summary>
    public sealed class Match3Bootstrap : MonoBehaviour
    {
        private enum AppScreen { Board, Meta }

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private GemSet gemSet;
        [SerializeField] private LevelSet levelSet;
        [SerializeField] private MetaBoardLayout metaLayout;

        private readonly System.Random _rng = new System.Random();

        private VolumeSettings _volume;
        private SfxPlayer _sfx;
        private Match3Hud _hud;
        private SettingsController _settings;
        private BoardView _boardView;
        private MetaBoardView _metaView;
        private IMetaStore _store;
        private MetaProgress _progress;
        private Match3Game _game;
        private AppScreen _screen = AppScreen.Board;

        private void Start()
        {
            if (uiDocument == null || gemSet == null || levelSet == null || metaLayout == null)
            {
                Debug.LogError("[Match3] Bootstrap missing serialized config (run SceneSetup.Build).");
                return;
            }

            Camera cam = Camera.main;
            FitCamera(cam); // size the ortho view so the board AND the meta ring fit the portrait width

            // Persistent juice toolkit (built once, survives every level/screen).
            _volume = new VolumeSettings();
            var materials = new MaterialFactory();
            var particles = new ParticleFactory(materials);
            var sprites = new SpriteFactory();
            var tweens = gameObject.AddComponent<TweenRunner>();
            var hitStop = gameObject.AddComponent<HitStop>();
            var rig = (cam != null ? cam.GetComponent<CameraRig>() : null) ?? (cam != null ? cam.gameObject.AddComponent<CameraRig>() : gameObject.AddComponent<CameraRig>());
            PostFxJuice postFx = cam != null ? PostFxInstaller.Install(cam) : null;

            _sfx = gameObject.AddComponent<SfxPlayer>();
            _sfx.UseSettings(_volume);
            var music = gameObject.AddComponent<MusicPlayer>();
            music.Play(_volume);

            // One persistent juice event bus: the view raises beats with correct animation timing.
            var juice = new GameEvents();
            rig.SetBasePose();
            rig.Bind(juice);
            if (postFx != null) postFx.Bind(juice);

            VisualElement uiRoot = uiDocument.rootVisualElement;
            _hud = new Match3Hud(uiDocument);
            _settings = new SettingsController(uiDocument, _volume, ResetProgress, () => _sfx.PlayHook("ui_click"));
            var floaters = new FloatingTextLayer(uiRoot, cam, tweens);

            _store = new PlayerPrefsMetaStore();
            _progress = new MetaProgress(_store.Load(), metaLayout.StarsPerUnlock);
            var roller = new RandomDieRoller(_rng.Next());

            _boardView = new BoardView(gemSet, sprites, cam, transform, tweens, hitStop, rig, particles,
                                       juice, _sfx, floaters, _hud, OnBoardResolved);
            _metaView = new MetaBoardView(sprites, cam, transform, tweens, particles, _sfx, floaters,
                                          uiDocument, roller, _store, OnMetaProceed);

            // Input: the pointer adapter is the only device reader; route taps and swipes to the board only.
            var input = GetComponent<PointerInputAdapter>() ?? gameObject.AddComponent<PointerInputAdapter>();
            input.Init(cam, OnTap, null, () => _settings != null && _settings.IsOpen, OnSwipe);

            BeginLevel(_progress.LevelIndex);
        }

        private void Update()
        {
            _hud?.Tick(Time.unscaledDeltaTime);
        }

        // ---- Flow ---------------------------------------------------------------

        private void BeginLevel(int levelIndex)
        {
            LevelDefinition def = levelSet.At(levelIndex);
            if (def == null) { Debug.LogError("[Match3] LevelSet is empty."); return; }
            LevelConfig cfg = def.ToConfig();
            if (cfg.ColorCount > gemSet.Count)
                Debug.LogError($"[Match3] Level '{def.DisplayName}' wants {cfg.ColorCount} colors but the GemSet has only {gemSet.Count}; two color ids would share a sprite and the board would read ambiguously.");

            var genSource = new SeededGemSource(cfg.ColorCount, _rng.Next());
            var playSource = new SeededGemSource(cfg.ColorCount, _rng.Next());
            _game = Match3Game.NewGame(cfg, genSource, playSource, new System.Random(_rng.Next()));

            _hud.BeginLevel(GoalName(cfg.GoalColorId), cfg.GoalCount, cfg.MoveLimit);
            _hud.ShowHud(true);
            _boardView.Load(_game);
            _boardView.SetVisible(true);
            _metaView.SetVisible(false);
            _screen = AppScreen.Board;
            _sfx.PlayHook("game_start");
        }

        private void OnTap(Vector2 screenPosition)
        {
            if (_screen == AppScreen.Board) _boardView.OnTap(screenPosition);
        }

        private void OnSwipe(Vector2 startScreen, Vector2 screenDelta)
        {
            if (_screen == AppScreen.Board) _boardView.OnSwipe(startScreen, screenDelta);
        }

        private void OnBoardResolved(MoveResult result)
        {
            if (result.StatusAfter == GameStatus.Won)
            {
                int award = metaLayout.DicePerWin + Mathf.Clamp(result.MovesLeftAfter, 0, 6);
                _progress.AwardDice(award);
                _store.Save(_progress.ToState());
                _hud.ShowBanner("Level Complete!", "+" + award + " dice earned", "Bonus Board", GoToMeta);
            }
            else if (result.StatusAfter == GameStatus.Lost)
            {
                _hud.ShowBanner("Out of Moves", "So close — try again!", "Retry",
                    () => { _hud.HideBanner(); BeginLevel(_progress.LevelIndex); });
            }
        }

        private void GoToMeta()
        {
            _hud.HideBanner();
            _hud.ShowHud(false);
            _boardView.SetVisible(false);
            _metaView.Load(_progress, metaLayout.ToMetaBoard(), metaLayout);
            _metaView.SetVisible(true);
            _screen = AppScreen.Meta;
            _sfx.PlayHook("ui_click");
        }

        private void OnMetaProceed()
        {
            _metaView.SetVisible(false);
            BeginLevel(_progress.LevelIndex); // LevelIndex may have advanced via a star unlock
        }

        private void ResetProgress()
        {
            _store.Clear(); // through the interface — no downcast to the concrete store
            _progress = new MetaProgress(MetaState.Defaults, metaLayout.StarsPerUnlock);
            _sfx.PlayHook("restart");
            BeginLevel(_progress.LevelIndex);
        }

        private string GoalName(int colorId)
        {
            if (colorId >= 0 && colorId < gemSet.Count)
            {
                string n = gemSet.Get(colorId).Name;
                if (!string.IsNullOrEmpty(n)) return n;
            }
            return "Goal";
        }

        /// <summary>Sizes the orthographic view so both the 7×8 board and the meta ring fit the (portrait)
        /// width without clipping, taking the larger of the width- and height-constrained sizes for the
        /// actual screen aspect.</summary>
        private static void FitCamera(Camera cam)
        {
            if (cam == null || !cam.orthographic) return;
            const float cell = 1f, cols = 7f, rows = 8f, metaRadius = 2.9f;
            float halfW = Mathf.Max((cols - 1f) * 0.5f * cell, metaRadius) + 0.65f;
            float halfH = (rows - 1f) * 0.5f * cell + 1.1f;
            float aspect = cam.aspect > 0.01f ? cam.aspect : 0.5625f;
            cam.orthographicSize = Mathf.Max(halfH, halfW / aspect);
        }
    }
}
