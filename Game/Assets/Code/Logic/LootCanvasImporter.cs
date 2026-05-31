using System;
using System.Collections.Generic;
using System.Text;

namespace Mimic.Logic
{
    // Converts the loot "canvas" Google Sheet into the runtime loot.csv the game reads.
    //
    // Source sheet layout:
    //   - Data columns are mapped BY HEADER NAME (order-independent): id, name,
    //     description, gold, acidCost, healOnDigest, cellsRestoredOnDigest,
    //     adjacencyEffect, category, acidRestoreOnDigest, damageOnDigest,
    //     canReturnToBasket, glue, neighborGoldPct. Missing columns use defaults.
    //   - The shape "canvas" is a block of checkbox columns whose FIRST column
    //     carries the header "shape"; the canvas spans from there to the last column.
    //   - Each item occupies a block of rows: the id-row plus following rows with an
    //     empty id. The union of checked cells (bounding box) becomes the "X|.." shape.
    //   - adjacencyEffect cells may be multi-line (RFC 4180 quoted) — read via ParseRecords.
    public static class LootCanvasImporter
    {
        // Runtime (output) columns, in order. 'shape' is generated from the canvas;
        // every other column is read from the sheet by header name (default if absent).
        internal static readonly string[] OutCols =
        {
            "id", "name", "description", "shape", "gold", "acidCost", "healOnDigest",
            "cellsRestoredOnDigest", "adjacencyEffect", "category", "acidRestoreOnDigest",
            "damageOnDigest", "canReturnToBasket", "glue", "neighborGoldPct"
        };

        private const int ShapeOutIndex = 3; // позиция shape в OutCols
        private const string CanvasHeader = "shape"; // заголовок первой колонки канваса

        // Дефолты для отсутствующих/пустых колонок (совпадают с LootCatalog).
        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { "gold", "0" }, { "acidCost", "0" }, { "healOnDigest", "0" },
            { "cellsRestoredOnDigest", "0" }, { "adjacencyEffect", "" }, { "category", "normal" },
            { "acidRestoreOnDigest", "0" }, { "damageOnDigest", "0" },
            { "canReturnToBasket", "1" }, { "glue", "0" }, { "neighborGoldPct", "0" },
        };

        public static string BuildLootCsv(string sheetCsv)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", OutCols)).Append('\n');

            var records = CsvLoader.ParseRecords(sheetCsv);
            if (records.Count == 0) return sb.ToString();

            // Заголовок → карта «имя колонки → индекс».
            var header = records[0];
            var colOf = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++)
            {
                string name = header[i].Trim();
                if (name.Length > 0 && !colOf.ContainsKey(name)) colOf[name] = i;
            }

            int idIdx = colOf.TryGetValue("id", out var ii) ? ii : 0;
            int canvasStart = colOf.TryGetValue(CanvasHeader, out var cs) ? cs : -1;
            int canvasWidth = canvasStart >= 0 ? header.Length - canvasStart : 0;

            string[] cur = null;
            var canvas = new List<bool[]>();

            void Flush()
            {
                if (cur == null) return;
                cur[ShapeOutIndex] = ResolveShape(canvas);
                sb.Append(JoinCsv(cur)).Append('\n');
            }

            for (int r = 1; r < records.Count; r++)
            {
                var rec = records[r];
                string id = idIdx < rec.Length ? rec[idIdx].Trim() : "";
                if (id.Length > 0)
                {
                    Flush();
                    cur = BuildRow(rec, colOf);
                    canvas = new List<bool[]> { CanvasRow(rec, canvasStart, canvasWidth) };
                }
                else if (cur != null)
                {
                    canvas.Add(CanvasRow(rec, canvasStart, canvasWidth));
                }
            }
            Flush();

            return sb.ToString();
        }

        // Собирает строку рантайма по именам колонок; shape заполняется позже.
        private static string[] BuildRow(string[] rec, Dictionary<string, int> colOf)
        {
            var row = new string[OutCols.Length];
            for (int i = 0; i < OutCols.Length; i++)
            {
                string name = OutCols[i];
                if (name == CanvasHeader) { row[i] = ""; continue; } // shape — из канваса

                string val = null;
                if (colOf.TryGetValue(name, out var idx) && idx < rec.Length) val = rec[idx];
                if (string.IsNullOrEmpty(val) && Defaults.TryGetValue(name, out var d)) val = d;
                row[i] = val ?? "";
            }
            return row;
        }

        private static bool[] CanvasRow(string[] rec, int start, int width)
        {
            var row = new bool[width > 0 ? width : 0];
            if (start < 0) return row;
            for (int c = 0; c < row.Length; c++)
            {
                int idx = start + c;
                row[c] = idx < rec.Length &&
                         rec[idx].Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            }
            return row;
        }

        // "X|.." из bounding box отмеченных клеток. Пустой канвас → "" (сигнал «нет формы»).
        private static string ResolveShape(List<bool[]> canvas)
        {
            int minR = int.MaxValue, maxR = -1, minC = int.MaxValue, maxC = -1;
            for (int r = 0; r < canvas.Count; r++)
                for (int c = 0; c < canvas[r].Length; c++)
                    if (canvas[r][c])
                    {
                        if (r < minR) minR = r;
                        if (r > maxR) maxR = r;
                        if (c < minC) minC = c;
                        if (c > maxC) maxC = c;
                    }

            if (maxR < 0) return ""; // ничего не отмечено

            var sb = new StringBuilder();
            for (int r = minR; r <= maxR; r++)
            {
                if (r > minR) sb.Append('|');
                for (int c = minC; c <= maxC; c++)
                {
                    bool on = c < canvas[r].Length && canvas[r][c];
                    sb.Append(on ? 'X' : '.');
                }
            }
            return sb.ToString();
        }

        // Минимальная CSV-сериализация: кавычим поля с , " или переводом строки.
        private static string JoinCsv(string[] cols)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cols.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string v = cols[i] ?? "";
                if (v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0)
                    sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
                else
                    sb.Append(v);
            }
            return sb.ToString();
        }
    }
}
