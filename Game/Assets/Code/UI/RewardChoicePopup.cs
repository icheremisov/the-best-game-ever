using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Game;
using Mimic.Catalogs;
using Mimic.Data;
using Mimic.Logic;

namespace Mimic.UI
{
    public class RewardChoicePopup : MonoBehaviour
    {
        public GameObject Root;
        public Text TitleText;
        public Button[] OptionButtons = new Button[3];
        public Text[] OptionLabels = new Text[3];

        private Action onTaken;
        private Action onOvereat;
        private List<LootData> options;
        private int pickSeed = 1;

        private void Awake()
        {
            if (Root != null) Root.SetActive(false);
            for (int i = 0; i < OptionButtons.Length; i++)
            {
                int idx = i;
                if (OptionButtons[i] != null)
                    OptionButtons[i].onClick.AddListener(() => Take(idx));
            }
        }

        public void Show(bool win, Action onTaken, Action onOvereat)
        {
            this.onTaken = onTaken;
            this.onOvereat = onOvereat;

            var pool = LootCatalog.ByCategory(win ? LootCategory.Reward : LootCategory.Punish);
            options = Settlement.Pick3(pool, pickSeed++);

            if (Root != null) Root.SetActive(true);
            if (TitleText != null) TitleText.text = win ? "Награда — заберите 1" : "Наказание — заберите 1";
            for (int i = 0; i < OptionButtons.Length; i++)
            {
                bool has = i < options.Count;
                if (OptionButtons[i] != null) OptionButtons[i].gameObject.SetActive(has);
                if (has && OptionLabels[i] != null) OptionLabels[i].text = options[i].Name;
            }
        }

        private void Take(int idx)
        {
            if (options == null || idx >= options.Count) return;
            var ctx = GameContext.Instance;
            var view = ctx.SpawnLoot(options[idx], ctx.MimicGrid.CellsRoot);
            bool placed = GameFlow.TryPlaceFirstFit(ctx.MimicGrid, view);
            if (Root != null) Root.SetActive(false);
            if (!placed)
            {
                Destroy(view.gameObject);
                onOvereat?.Invoke();
                return;
            }
            ctx.OnGridChanged();
            onTaken?.Invoke();
        }
    }
}
