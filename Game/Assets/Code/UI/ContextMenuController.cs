using UnityEngine;

namespace Mimic.UI
{
    public class ContextMenuController : MonoBehaviour
    {
        public static ContextMenuController Instance { get; private set; }
        public void Open(LootView item) { }
        public void Close() { }
    }
}
