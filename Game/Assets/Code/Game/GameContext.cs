using UnityEngine;
using Mimic.Logic;
using Mimic.UI;

namespace Mimic.Game
{
    // Stub — full implementation in Task 4.8.
    public class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }
        public AdjacencyResult<LootView> LastResolved { get; protected set; }
        public GameResources Resources { get; protected set; } = new GameResources();
        public GridView MimicGrid;
        public GridView AdventurerGrid;
        public void OnGridChanged() { }
        public void Digest(LootView item) { }
    }
}
