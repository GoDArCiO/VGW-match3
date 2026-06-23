using System;
using System.Collections;
using System.Collections.Generic;
using Match3.Config;
using Match3.Core;
using Proto.Core;
using Proto.Game;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// Renders the match-3 board and animates it. The board MODEL is authoritative and synchronous
    /// (<see cref="Match3Game.TrySwap"/> returns the entire <see cref="MoveResult"/> up front); this view is
    /// the sole sequencer that replays it over time, owning a cell→GameObject map and the "busy" lock.
    /// It never does grid math — only rendering, input, and juice.
    ///
    /// Per-step replay follows the <see cref="CascadeStep"/> contract: clear (pre-gravity cells) → movements
    /// (post-gravity targets) → spawns (from off-board), each phase finishing before the next, steps strictly
    /// in order. All waits/tweens are UNSCALED so a hit-stop never stalls the sequence.
    /// </summary>
    public sealed class BoardView
    {
        private const float SwapDur = 0.16f;
        private const float RevertDur = 0.14f;
        private const float ClearDur = 0.24f;
        private const float FallDur = 0.28f;
        private const float GemScale = 0.9f;

        // Persistent dependencies.
        private readonly GemSet _gems;
        private readonly SpriteFactory _sprites;
        private readonly Camera _camera;
        private readonly Transform _root;
        private readonly TweenRunner _tweens;
        private readonly HitStop _hitStop;
        private readonly CameraRig _rig;
        private readonly ParticleFactory _particles;
        private readonly IGameEvents _juice;       // raised for camera + post-fx timing
        private readonly SfxPlayer _sfx;
        private readonly FloatingTextLayer _floaters;
        private readonly Match3Hud _hud;
        private readonly Action<MoveResult> _onResolved;

        // Per-level state.
        private Match3Game _game;
        private GameObject[,] _objects;
        private SpriteRenderer _selectionMarker;
        private float _cell = 1f;
        private Vector3 _origin;
        private Cell? _selected;
        private bool _busy;

        public bool Busy => _busy;

        public BoardView(GemSet gems, SpriteFactory sprites, Camera camera, Transform parent,
                         TweenRunner tweens, HitStop hitStop, CameraRig rig, ParticleFactory particles,
                         IGameEvents juice, SfxPlayer sfx, FloatingTextLayer floaters, Match3Hud hud,
                         Action<MoveResult> onResolved)
        {
            _gems = gems;
            _sprites = sprites;
            _camera = camera;
            _tweens = tweens;
            _hitStop = hitStop;
            _rig = rig;
            _particles = particles;
            _juice = juice;
            _sfx = sfx;
            _floaters = floaters;
            _hud = hud;
            _onResolved = onResolved;

            var rootGo = new GameObject("BoardRoot");
            rootGo.transform.SetParent(parent, false);
            _root = rootGo.transform;

            var markerGo = new GameObject("Selection");
            markerGo.transform.SetParent(_root, false);
            _selectionMarker = markerGo.AddComponent<SpriteRenderer>();
            _selectionMarker.sprite = _sprites.Get(SpriteShape.RoundedSquare);
            _selectionMarker.color = new Color(1f, 1f, 1f, 0.35f);
            _selectionMarker.sortingOrder = 0;
            markerGo.SetActive(false);
        }

        /// <summary>(Re)build the gem objects for a new game and reset interaction state.</summary>
        public void Load(Match3Game game)
        {
            ClearObjects();
            _game = game;
            _busy = false;
            _selected = null;
            _selectionMarker.gameObject.SetActive(false);

            int cols = game.Board.Cols, rows = game.Board.Rows;
            _origin = new Vector3(-(cols - 1) * _cell * 0.5f, -(rows - 1) * _cell * 0.5f, 0f);
            _objects = new GameObject[cols, rows];

            for (int x = 0; x < cols; x++)
                for (int y = 0; y < rows; y++)
                    _objects[x, y] = CreateGem(game.Board.Get(x, y), new Cell(x, y));
        }

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        /// <summary>Routed from the pointer adapter. Tap a gem to select, tap an adjacent gem to swap.</summary>
        public void OnTap(Vector2 screenPosition)
        {
            if (_busy || _game == null || _game.IsOver) return;
            if (!TryScreenToCell(screenPosition, out Cell cell)) { Deselect(); return; }

            if (_selected == null) { Select(cell); return; }
            if (_selected.Value == cell) { Deselect(); return; }

            if (_selected.Value.IsAdjacent(cell))
            {
                Cell a = _selected.Value;
                Deselect();
                BeginSwap(a, cell);
            }
            else
            {
                Select(cell); // re-target
            }
        }

        /// <summary>
        /// Routed from the pointer adapter. Grab the gem under <paramref name="startScreen"/> and swap it with
        /// its neighbor in the dominant swipe direction — the classic match-3 drag. Screen up maps to board up
        /// (world Y), matching <see cref="WorldOf(int,int)"/>.
        /// </summary>
        public void OnSwipe(Vector2 startScreen, Vector2 screenDelta)
        {
            if (_busy || _game == null || _game.IsOver) return;
            if (!TryScreenToCell(startScreen, out Cell from)) { Deselect(); return; }

            // Snap the drag to a single cardinal step (one cell), choosing the larger screen axis.
            int dx = 0, dy = 0;
            if (Mathf.Abs(screenDelta.x) >= Mathf.Abs(screenDelta.y)) dx = screenDelta.x > 0f ? 1 : -1;
            else dy = screenDelta.y > 0f ? 1 : -1;

            var to = new Cell(from.X + dx, from.Y + dy);
            if (to.X < 0 || to.X >= _game.Board.Cols || to.Y < 0 || to.Y >= _game.Board.Rows)
            { Deselect(); return; }

            Deselect();
            BeginSwap(from, to); // TrySwap validates adjacency + match; an illegal drag just bounces back
        }

        // ---- Input helpers ------------------------------------------------------

        private void Select(Cell cell)
        {
            var go = _objects[cell.X, cell.Y];
            if (go == null) return; // keep _selected and the marker in sync — never select an empty cell
            _selected = cell;
            _selectionMarker.transform.position = go.transform.position;
            _selectionMarker.transform.localScale = Vector3.one * _cell;
            _selectionMarker.gameObject.SetActive(true);
            _tweens.PunchScale(go.transform, 1.18f, 0.18f);
        }

        private void Deselect()
        {
            _selected = null;
            _selectionMarker.gameObject.SetActive(false);
        }

        private void BeginSwap(Cell a, Cell b)
        {
            MoveResult result = _game.TrySwap(a, b);
            _busy = true;
            if (!result.Valid)
                _tweens.Run(AnimateIllegal(a, b));
            else
                _tweens.Run(AnimateMove(a, b, result));
        }

        // ---- Animation sequence -------------------------------------------------

        private IEnumerator AnimateIllegal(Cell a, Cell b)
        {
            _sfx.PlayHook("invalid_swap");
            var ga = _objects[a.X, a.Y];
            var gb = _objects[b.X, b.Y];
            Vector3 pa = WorldOf(a), pb = WorldOf(b);
            yield return MoveBoth(ga, gb, pb, pa, RevertDur);   // show the swap...
            yield return MoveBoth(ga, gb, pa, pb, RevertDur);   // ...then bounce back
            if (ga != null) _tweens.ShakePosition(ga.transform, 0.12f, 0.18f);
            _busy = false;
        }

        private IEnumerator AnimateMove(Cell a, Cell b, MoveResult result)
        {
            _sfx.PlayHook("swap");
            _hud.SetMoves(result.MovesLeftAfter);

            // Swap the two gem objects to match the model.
            var ga = _objects[a.X, a.Y];
            var gb = _objects[b.X, b.Y];
            _objects[a.X, a.Y] = gb;
            _objects[b.X, b.Y] = ga;
            yield return MoveBoth(ga, gb, WorldOf(b), WorldOf(a), SwapDur);

            int scoreBefore = result.ScoreAfter - result.TotalScore;
            int running = scoreBefore;

            foreach (var step in result.Steps)
            {
                running += step.StepScore;
                yield return PlayStep(step, running);
            }

            _hud.SetScoreTarget(result.ScoreAfter);
            _hud.SetGoalRemaining(result.GoalRemainingAfter);
            _hud.SetMoves(result.MovesLeftAfter);

            if (result.Reshuffled)
                yield return RebuildBoardObjects();

            if (result.StatusAfter == GameStatus.Won) { _juice.Won(result.ScoreAfter); _sfx.PlayHook("win"); }
            else if (result.StatusAfter == GameStatus.Lost) { _juice.Lost(result.ScoreAfter); _sfx.PlayHook("lose"); }

            _busy = false;
            _onResolved?.Invoke(result);
        }

        private IEnumerator PlayStep(CascadeStep step, int runningScore)
        {
            int chain = step.Chain;

            // Audio: one pitched "match" cue per step (never per gem — avoids batch clipping), rising by chain.
            _sfx.PlayHook("match", 1f + 0.12f * (chain - 1));
            if (chain >= 2)
            {
                _hud.ShowCombo(ComboWord(chain));
                _sfx.PlayHook("cascade", 1f + 0.1f * (chain - 1));
            }

            // Phase 1 — clear each group: pop + burst + floating score, then the gem shrinks out.
            for (int gi = 0; gi < step.Cleared.Count; gi++)
            {
                MatchGroup group = step.Cleared[gi];
                Vector3 center = GroupCenter(group);
                Color color = ColorOf(group.Color);

                _juice.Collected(0, new Vec2(center.x, center.y)); // camera trauma + post-fx swell
                _particles.Burst(center, color, 8 + group.Size * 3);

                // Score comes from the core (CascadeStep.GroupScores) — the view never re-derives the scoring
                // formula, so an A/B-tuned ScoringConfig can't desync the floaters from the banked score.
                int groupScore = step.GroupScores[gi];
                _floaters.Spawn("+" + groupScore, center, Color.Lerp(color, Color.white, 0.45f), 44f + group.Size * 4f);

                foreach (var cell in group.Cells)
                {
                    var go = _objects[cell.X, cell.Y];
                    _objects[cell.X, cell.Y] = null;
                    if (go != null) _tweens.Run(ClearGem(go.transform));
                }
            }

            // Escalating feel: bigger clears / deeper chains hit harder.
            _rig.AddTrauma(Mathf.Min(0.55f, 0.06f + 0.04f * step.ClearedCount + 0.05f * chain));
            if (chain >= 3 || step.ClearedCount >= 6) _hitStop.Trigger(0.07f);

            _hud.SetScoreTarget(runningScore);
            yield return WaitUnscaled(ClearDur);

            // Phase 2 — gravity slides (read all sources before writing so no in-flight clobber).
            var movers = new List<(Movement m, GameObject go)>(step.Movements.Count);
            foreach (var m in step.Movements) movers.Add((m, _objects[m.From.X, m.From.Y]));
            foreach (var mv in movers) _objects[mv.m.From.X, mv.m.From.Y] = null;
            foreach (var mv in movers)
            {
                _objects[mv.m.To.X, mv.m.To.Y] = mv.go;
                if (mv.go != null) _tweens.Run(FallTo(mv.go.transform, WorldOf(mv.m.To)));
            }

            // Phase 3 — refills drop in from above their column.
            foreach (var spawn in step.Spawns)
            {
                Vector3 from = WorldOf(spawn.To.X, spawn.FromRowVirtual);
                var go = CreateGem(spawn.Color, spawn.To);
                _objects[spawn.To.X, spawn.To.Y] = go; // track the refill so later cascade steps can move it
                go.transform.position = from;
                _tweens.Run(FallTo(go.transform, WorldOf(spawn.To)));
            }

            if (step.Movements.Count > 0 || step.Spawns.Count > 0)
                yield return WaitUnscaled(FallDur + 0.04f);
        }

        private IEnumerator ClearGem(Transform t)
        {
            Vector3 baseScale = t.localScale;
            yield return TweenRunner.Animate(ClearDur, t, k =>
            {
                float s = k < 0.4f ? Mathf.Lerp(1f, 1.35f, k / 0.4f) : Mathf.Lerp(1.35f, 0f, (k - 0.4f) / 0.6f);
                t.localScale = baseScale * s;
            });
            if (t != null) UnityEngine.Object.Destroy(t.gameObject);
        }

        private IEnumerator FallTo(Transform t, Vector3 target)
        {
            Vector3 start = t.position;
            yield return TweenRunner.Animate(FallDur, t, k =>
            {
                t.position = Vector3.LerpUnclamped(start, target, Ease.OutCubic(k));
            });
            if (t != null)
            {
                t.position = target;
                _tweens.PunchScale(t, 1.12f, 0.12f); // a little squash-pop on landing
            }
        }

        private IEnumerator MoveBoth(GameObject ga, GameObject gb, Vector3 toA, Vector3 toB, float dur)
        {
            Vector3 fromA = ga != null ? ga.transform.position : toA;
            Vector3 fromB = gb != null ? gb.transform.position : toB;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease.OutCubic(Mathf.Clamp01(t / dur));
                if (ga != null) ga.transform.position = Vector3.LerpUnclamped(fromA, toA, k);
                if (gb != null) gb.transform.position = Vector3.LerpUnclamped(fromB, toB, k);
                yield return null;
            }
            if (ga != null) ga.transform.position = toA;
            if (gb != null) gb.transform.position = toB;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        // ---- Board object management -------------------------------------------

        /// <summary>Destroys and rebuilds every gem object from the model (used after a deadlock reshuffle).</summary>
        private IEnumerator RebuildBoardObjects()
        {
            ClearObjects();
            int cols = _game.Board.Cols, rows = _game.Board.Rows;
            _objects = new GameObject[cols, rows];
            for (int x = 0; x < cols; x++)
                for (int y = 0; y < rows; y++)
                {
                    var go = CreateGem(_game.Board.Get(x, y), new Cell(x, y));
                    _tweens.PunchScale(go.transform, 1.15f, 0.2f);
                    _objects[x, y] = go;
                }
            yield return WaitUnscaled(0.2f);
        }

        private GameObject CreateGem(int colorId, Cell cell)
        {
            var go = new GameObject($"Gem_{cell.X}_{cell.Y}");
            go.transform.SetParent(_root, false);
            go.transform.position = WorldOf(cell);
            go.transform.localScale = Vector3.one * GemScale;

            var sr = go.AddComponent<SpriteRenderer>();
            int id = Mathf.Clamp(colorId, 0, Mathf.Max(0, _gems.Count - 1));
            sr.sprite = _sprites.Get(ShapeFor(id));
            sr.color = _gems.Count > 0 ? _gems.Get(id).Color : Color.magenta;
            sr.sortingOrder = 1;
            return go;
        }

        private void ClearObjects()
        {
            if (_objects == null) return;
            foreach (var go in _objects)
                if (go != null) UnityEngine.Object.Destroy(go);
            _objects = null;
        }

        // ---- Mapping & lookups --------------------------------------------------

        private Vector3 WorldOf(Cell c) => WorldOf(c.X, c.Y);
        private Vector3 WorldOf(int x, int y) => _origin + new Vector3(x * _cell, y * _cell, 0f);

        private Vector3 GroupCenter(MatchGroup group)
        {
            Vector3 sum = Vector3.zero;
            foreach (var c in group.Cells) sum += WorldOf(c);
            return sum / Mathf.Max(1, group.Cells.Length);
        }

        private Color ColorOf(int colorId)
        {
            if (_gems.Count == 0) return Color.magenta;
            return _gems.Get(Mathf.Clamp(colorId, 0, _gems.Count - 1)).Color;
        }

        private SpriteShape ShapeFor(int colorId)
        {
            GemShape shape = _gems.Get(colorId).Shape;
            switch (shape)
            {
                case GemShape.Circle: return SpriteShape.Circle;
                case GemShape.Square: return SpriteShape.RoundedSquare;
                case GemShape.Diamond: return SpriteShape.Diamond;
                case GemShape.Triangle: return SpriteShape.Triangle;
                case GemShape.Hexagon: return SpriteShape.Hexagon;
                case GemShape.Star: return SpriteShape.Star;
                default: return SpriteShape.Circle;
            }
        }

        private bool TryScreenToCell(Vector2 screen, out Cell cell)
        {
            cell = default;
            if (_camera == null || _game == null) return false;
            float dist = Mathf.Abs(_camera.transform.position.z);
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, dist));
            int x = Mathf.RoundToInt((world.x - _origin.x) / _cell);
            int y = Mathf.RoundToInt((world.y - _origin.y) / _cell);
            if (x < 0 || x >= _game.Board.Cols || y < 0 || y >= _game.Board.Rows) return false;
            cell = new Cell(x, y);
            return true;
        }

        private static string ComboWord(int chain)
        {
            switch (chain)
            {
                case 2: return "Nice!  x2";
                case 3: return "Great!  x3";
                case 4: return "Awesome!  x4";
                case 5: return "Amazing!  x5";
                default: return "Unreal!  x" + chain;
            }
        }
    }
}
