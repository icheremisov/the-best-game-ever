using UnityEngine;

namespace Mimic.Game
{
    public class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }
        public void OnGridChanged() { }
    }
}
