using System.Collections.Generic;
using Mimic.Data;
using Mimic.Logic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mimic.Input;

namespace Mimic.UI
{
    public class LootView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public LootData Data { get; private set; }
        public Shape Shape => Data?.Shape;
        public Rotation CurrentRotation = Rotation.Deg0;

        [Header("References")]
        public RectTransform CellsRoot;
        public GameObject CellPrefab;
        public Text Label;

        public void Bind(LootData data)
        {
            Data = data;
            Label.text = data.Name.Substring(0, System.Math.Min(2, data.Name.Length));
            BuildCells();
        }

        private void BuildCells()
        {
            for (int i = CellsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(CellsRoot.GetChild(i).gameObject);

            float size = 64f; // matches GridView.CellSize
            var cells = Data.Shape.GetRotatedCells(CurrentRotation);
            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!cells[r, c]) continue;
                    var go = Instantiate(CellPrefab, CellsRoot);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.sizeDelta = new Vector2(size, size);
                    rt.anchoredPosition = new Vector2(c * size, r * size);
                }
            }
        }

        public void Rotate(bool clockwise)
        {
            int v = (int)CurrentRotation + (clockwise ? 1 : 3);
            CurrentRotation = (Rotation)(v % 4);
            BuildCells();
        }

        public void OnPointerDown(PointerEventData ev)
        {
            if (ev.button == PointerEventData.InputButton.Left)
                DragController.Instance?.OnLootClicked(this);
            else if (ev.button == PointerEventData.InputButton.Right)
                DragController.Instance?.OnLootRightClicked(this);
        }

        public void OnPointerEnter(PointerEventData ev) => TooltipController.Instance?.Show(this);
        public void OnPointerExit(PointerEventData ev) => TooltipController.Instance?.Hide();
    }
}
