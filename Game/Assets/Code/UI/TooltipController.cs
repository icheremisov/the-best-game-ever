using UnityEngine;

namespace Mimic.UI
{
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }
        public void Show(LootView item) { }
        public void Hide() { }
    }
}
