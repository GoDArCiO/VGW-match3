using UnityEditor;
using UnityEngine;
using Match3.Core;
using Match3.Config;

namespace Proto.EditorTools
{
    /// <summary>
    /// Headlessly creates the match-3 config ScriptableObjects (gem set, levels, meta board) under
    /// <c>Assets/Config</c>. Idempotent — loads any that already exist. Run via
    /// <c>unity.ps1 exec -Method Proto.EditorTools.Match3Assets.Generate</c>, and also called by
    /// <see cref="SceneSetup.Build"/> so a single scene-setup pass yields a fully wired, playable scene.
    /// "Assign in the inspector" is never a step in this project.
    /// </summary>
    public static class Match3Assets
    {
        private const string Dir = "Assets/Config";

        [MenuItem("Match3/Generate Config Assets")]
        public static void Generate()
        {
            EnsureAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Config assets ready under " + Dir);
        }

        public static (GemSet gems, LevelSet levels, MetaBoardLayout meta) EnsureAll()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets", "Config");

            var gems = EnsureGemSet();
            var meta = EnsureMetaLayout();
            var levels = EnsureLevelSet();
            AssetDatabase.SaveAssets();
            return (gems, levels, meta);
        }

        private static T LoadOrCreate<T>(string path, System.Func<T> create) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var obj = create();
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        private static GemSet EnsureGemSet()
        {
            return LoadOrCreate(Dir + "/GemSet.asset", () =>
            {
                var g = ScriptableObject.CreateInstance<GemSet>();
                g.SetGems(new[]
                {
                    new GemTypeDef { Name = "Red", Color = new Color(0.93f, 0.26f, 0.31f), Shape = GemShape.Circle },     // 0
                    new GemTypeDef { Name = "Blue", Color = new Color(0.22f, 0.56f, 0.96f), Shape = GemShape.Diamond },   // 1 (goal)
                    new GemTypeDef { Name = "Green", Color = new Color(0.40f, 0.80f, 0.38f), Shape = GemShape.Square },   // 2
                    new GemTypeDef { Name = "Yellow", Color = new Color(0.98f, 0.82f, 0.25f), Shape = GemShape.Triangle },// 3
                    new GemTypeDef { Name = "Purple", Color = new Color(0.66f, 0.42f, 0.92f), Shape = GemShape.Hexagon }, // 4
                    new GemTypeDef { Name = "Orange", Color = new Color(0.98f, 0.56f, 0.20f), Shape = GemShape.Star },    // 5
                });
                return g;
            });
        }

        private static MetaBoardLayout EnsureMetaLayout()
        {
            return LoadOrCreate(Dir + "/MetaBoardLayout.asset", () =>
            {
                var m = ScriptableObject.CreateInstance<MetaBoardLayout>();
                m.Configure(new[]
                {
                    new MetaTileDef { Kind = RewardKind.Nothing, Amount = 0 },  // 0 start
                    new MetaTileDef { Kind = RewardKind.Coins, Amount = 20 },
                    new MetaTileDef { Kind = RewardKind.Star, Amount = 1 },
                    new MetaTileDef { Kind = RewardKind.Coins, Amount = 30 },
                    new MetaTileDef { Kind = RewardKind.Dice, Amount = 1 },
                    new MetaTileDef { Kind = RewardKind.Coins, Amount = 25 },
                    new MetaTileDef { Kind = RewardKind.Star, Amount = 1 },
                    new MetaTileDef { Kind = RewardKind.Nothing, Amount = 0 },
                    new MetaTileDef { Kind = RewardKind.Coins, Amount = 40 },
                    new MetaTileDef { Kind = RewardKind.Dice, Amount = 2 },
                    new MetaTileDef { Kind = RewardKind.Star, Amount = 1 },
                    new MetaTileDef { Kind = RewardKind.Coins, Amount = 50 },
                }, starsPerUnlock: 3, dicePerWin: 4);
                return m;
            });
        }

        private static LevelSet EnsureLevelSet()
        {
            var l1 = EnsureLevel("Level_1", "Level 1", goalCount: 7, moveLimit: 22);
            var l2 = EnsureLevel("Level_2", "Level 2", goalCount: 9, moveLimit: 20);
            var l3 = EnsureLevel("Level_3", "Level 3", goalCount: 12, moveLimit: 18);

            var set = LoadOrCreate(Dir + "/LevelSet.asset", () => ScriptableObject.CreateInstance<LevelSet>());
            set.SetLevels(new[] { l1, l2, l3 });
            EditorUtility.SetDirty(set);
            return set;
        }

        private static LevelDefinition EnsureLevel(string assetName, string display, int goalCount, int moveLimit)
        {
            return LoadOrCreate(Dir + "/" + assetName + ".asset", () =>
            {
                var lvl = ScriptableObject.CreateInstance<LevelDefinition>();
                lvl.Configure(display, cols: 7, rows: 8, colorCount: 6, goalColorId: 1, goalCount: goalCount, moveLimit: moveLimit);
                return lvl;
            });
        }
    }
}
