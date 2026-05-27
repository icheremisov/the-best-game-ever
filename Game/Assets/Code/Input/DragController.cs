using UnityEngine;
using Mimic.UI;

namespace Mimic.Input
{
    public class DragController : MonoBehaviour
    {
        public static DragController Instance { get; private set; }
        public void OnLootClicked(LootView item) { }
        public void OnLootRightClicked(LootView item) { }
    }
}
