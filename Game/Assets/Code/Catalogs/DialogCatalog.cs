using System.Collections.Generic;
using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class DialogCatalog
    {
        private static Dictionary<string, List<Mimic.Data.DialogLine>> _chains = new();

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Configs/dialogs.csv");
            _chains = ta != null
                ? Parse(ta.text)
                : new Dictionary<string, List<Mimic.Data.DialogLine>>();
        }

        // Колонки: trigger,text,icon. Непустой trigger начинает новую цепочку;
        // пустой trigger добавляет реплику в текущую цепочку. Шапка пропускается CsvLoader.
        public static Dictionary<string, List<Mimic.Data.DialogLine>> Parse(string csvText)
        {
            var rows = CsvLoader.ParseAll(csvText);
            var result = new Dictionary<string, List<Mimic.Data.DialogLine>>();
            List<Mimic.Data.DialogLine> current = null;
            foreach (var r in rows)
            {
                string trigger = r.Length > 0 ? r[0].Trim() : "";
                string text = r.Length > 1 ? r[1] : "";
                string icon = r.Length > 2 ? r[2].Trim() : "";
                if (!string.IsNullOrEmpty(trigger))
                {
                    current = new List<Mimic.Data.DialogLine>();
                    result[trigger] = current;
                }
                if (current == null) continue; // строки до первого триггера игнорируем
                current.Add(new Mimic.Data.DialogLine { Text = text, Icon = icon });
            }
            return result;
        }

        // Цепочка реплик для триггера или null, если такого триггера нет.
        public static IReadOnlyList<Mimic.Data.DialogLine> Get(string trigger)
            => _chains.TryGetValue(trigger, out var list) ? list : null;

        public static void SetForTest(Dictionary<string, List<Mimic.Data.DialogLine>> chains)
            => _chains = chains;
    }
}
