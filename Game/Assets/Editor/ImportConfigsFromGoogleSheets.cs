#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Mimic.Logic;
using UnityEditor;
using UnityEngine;

namespace Mimic.EditorTools
{
    // Downloads the game's CSV configs straight from the shared Google Sheet
    // and overwrites Assets/Resources/Configs/*.csv.txt.
    //
    // day & adventurers are saved verbatim. loot is special: its sheet has a
    // checkbox "canvas" (columns K..P) where each item occupies a block of rows
    // (from its id-row until the next id-row). We turn that canvas into the
    // game's "X|.." shape string and write a clean 10-column loot.csv.txt that
    // the runtime loader already understands — no game-side changes needed.
    public static class ImportConfigsFromGoogleSheets
    {
        private const string SpreadsheetId = "1EUfW2SPLCzWxCaqrYfPNz27JhQMIxda1Z9DLqmIEQE4";
        private const string ConfigsDir = "Assets/Resources/Configs";

        private const string DayGid = "0";
        private const string AdventurersGid = "1296203013";
        private const string LootGid = "492897648"; // sheet with the shape canvas

        // loot.csv schema the game expects (canvas columns are dropped on import).
        private const int LootDataCols = 10;   // id..adjacencyEffect
        private const int ShapeCol = 3;         // "shape" lives in column D
        private const int CanvasStartCol = 10;  // checkbox canvas begins at column K
        private const int CanvasMaxCols = 6;    // K..P

        [MenuItem("Mimic/Import Configs from Google Sheets")]
        private static void Import()
        {
            int ok = 0, total = 3;
            try
            {
                EditorUtility.DisplayProgressBar("Import Configs", "day.csv.txt", 0f / total);
                if (TryDownload(DayGid, out string dayText, out string err))
                { WriteConfig("day.csv.txt", dayText); ok++; }
                else Debug.LogError($"[ImportConfigs] day failed: {err}");

                EditorUtility.DisplayProgressBar("Import Configs", "adventurers.csv.txt", 1f / total);
                if (TryDownload(AdventurersGid, out string advText, out err))
                { WriteConfig("adventurers.csv.txt", advText); ok++; }
                else Debug.LogError($"[ImportConfigs] adventurers failed: {err}");

                EditorUtility.DisplayProgressBar("Import Configs", "loot.csv.txt", 2f / total);
                if (TryDownload(LootGid, out string lootText, out err))
                { WriteConfig("loot.csv.txt", BuildLootCsv(lootText)); ok++; }
                else Debug.LogError($"[ImportConfigs] loot failed: {err}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ImportConfigs] Done: {ok}/{total} configs imported.");
        }

        // Converts the loot sheet (data columns + checkbox canvas) into a clean
        // 10-column CSV with the shape string generated from the canvas.
        private static string BuildLootCsv(string raw)
        {
            var lines = raw.Split('\n');
            var sb = new StringBuilder();

            // Header: keep only the 10 data columns.
            sb.Append("id,name,description,shape,gold,acidCost,healOnDigest,")
              .Append("cellsRestoredOnDigest,adjacencyTarget,adjacencyEffect\n");

            string[] itemCols = null;             // the current item's 10 data columns
            var canvasRows = new List<bool[]>();  // checkbox rows accumulated for that item

            void Flush()
            {
                if (itemCols == null) return;
                itemCols[ShapeCol] = ResolveShape(canvasRows, itemCols[ShapeCol]);
                sb.Append(JoinCsv(itemCols)).Append('\n');
            }

            bool headerSkipped = false;
            foreach (var rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var cells = CsvLoader.ParseLine(rawLine);
                string id = cells.Length > 0 ? cells[0].Trim() : "";

                if (id.Length > 0)
                {
                    Flush();
                    itemCols = ExtractDataCols(cells);
                    canvasRows = new List<bool[]> { ExtractCanvasRow(cells) };
                }
                else if (itemCols != null)
                {
                    canvasRows.Add(ExtractCanvasRow(cells));
                }
            }
            Flush();

            return sb.ToString();
        }

        // First 10 columns, padded if the sheet trimmed trailing empties.
        private static string[] ExtractDataCols(string[] cells)
        {
            var cols = new string[LootDataCols];
            for (int i = 0; i < LootDataCols; i++)
                cols[i] = i < cells.Length ? cells[i] : "";
            return cols;
        }

        private static bool[] ExtractCanvasRow(string[] cells)
        {
            var row = new bool[CanvasMaxCols];
            for (int c = 0; c < CanvasMaxCols; c++)
            {
                int idx = CanvasStartCol + c;
                row[c] = idx < cells.Length &&
                         cells[idx].Trim().Equals("TRUE", System.StringComparison.OrdinalIgnoreCase);
            }
            return row;
        }

        // Builds the "X|.." shape from the canvas bounding box.
        // Falls back to the text cell if the canvas is empty.
        private static string ResolveShape(List<bool[]> canvas, string fallback)
        {
            int minR = int.MaxValue, maxR = -1, minC = int.MaxValue, maxC = -1;
            for (int r = 0; r < canvas.Count; r++)
                for (int c = 0; c < CanvasMaxCols; c++)
                    if (canvas[r][c])
                    {
                        if (r < minR) minR = r;
                        if (r > maxR) maxR = r;
                        if (c < minC) minC = c;
                        if (c > maxC) maxC = c;
                    }

            if (maxR < 0) return fallback; // nothing checked

            var sb = new StringBuilder();
            for (int r = minR; r <= maxR; r++)
            {
                if (r > minR) sb.Append('|');
                for (int c = minC; c <= maxC; c++)
                    sb.Append(canvas[r][c] ? 'X' : '.');
            }
            return sb.ToString();
        }

        // Minimal CSV serialization: quote fields containing , " or newline.
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

        private static bool TryDownload(string gid, out string text, out string error)
        {
            text = null;
            string url =
                $"https://docs.google.com/spreadsheets/d/{SpreadsheetId}/export?format=csv&gid={gid}";
            try
            {
                using (var client = new WebClient())
                {
                    // Google's export endpoint 302-redirects; WebClient follows it automatically.
                    // Decode bytes explicitly as UTF-8 — DownloadString mangles Cyrillic.
                    byte[] bytes = client.DownloadData(url);
                    text = new UTF8Encoding(false).GetString(bytes)
                        .TrimStart('﻿')
                        .Replace("\r\n", "\n");

                    if (text.TrimStart().StartsWith("<"))
                    {
                        error = "got HTML instead of CSV — is the sheet shared as 'anyone with the link'?";
                        return false;
                    }
                    error = null;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private static void WriteConfig(string file, string text)
        {
            Directory.CreateDirectory(ConfigsDir);
            File.WriteAllText(Path.Combine(ConfigsDir, file), text);
        }
    }
}
#endif
