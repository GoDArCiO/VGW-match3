using System;
using UnityEngine;
using Match3.Core;

namespace Match3.Config
{
    [Serializable]
    public struct MetaTileDef
    {
        public RewardKind Kind;
        public int Amount;
    }

    /// <summary>
    /// Authoring data for the looped meta board: the ring of reward tiles plus the unlock economy
    /// (<see cref="StarsPerUnlock"/>) and how many dice a level win grants. Converts to engine-free Core
    /// types (<see cref="MetaTile"/> / <see cref="MetaBoard"/>) so the Core never touches the engine.
    /// </summary>
    [CreateAssetMenu(fileName = "MetaBoardLayout", menuName = "Match3/Meta Board Layout")]
    public sealed class MetaBoardLayout : ScriptableObject
    {
        [SerializeField] private MetaTileDef[] tiles = Array.Empty<MetaTileDef>();
        [SerializeField, Min(1)] private int starsPerUnlock = 3;
        [SerializeField, Min(1)] private int dicePerWin = 4;

        public int Count => tiles.Length;
        public int StarsPerUnlock => starsPerUnlock;
        public int DicePerWin => dicePerWin;

        public MetaTileDef GetDef(int index) => tiles[index];

        public MetaTile[] ToTiles()
        {
            var arr = new MetaTile[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
                arr[i] = new MetaTile(tiles[i].Kind, tiles[i].Amount);
            return arr;
        }

        public MetaBoard ToMetaBoard() => new MetaBoard(ToTiles());

        public void Configure(MetaTileDef[] tiles, int starsPerUnlock, int dicePerWin)
        {
            this.tiles = tiles;
            this.starsPerUnlock = starsPerUnlock;
            this.dicePerWin = dicePerWin;
        }
    }
}
