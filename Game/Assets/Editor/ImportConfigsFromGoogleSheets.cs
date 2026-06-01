#if UNITY_EDITOR
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
    // day, adventurers & dialogs are saved verbatim. loot is special: its sheet carries a
    // checkbox shape "canvas" and authoring-friendly columns. The actual
    // sheet→runtime conversion (shape from canvas, columns mapped by header name,
    // multi-line adjacencyEffect) lives in Mimic.Logic.LootCanvasImporter so it is
    // unit-tested; this file only fetches the CSV and writes the result.
    public static class ImportConfigsFromGoogleSheets
    {
        private const string SpreadsheetId = "1EUfW2SPLCzWxCaqrYfPNz27JhQMIxda1Z9DLqmIEQE4";
        private const string ConfigsDir = "Assets/Resources/Configs";

        private const string DayGid = "0";
        private const string AdventurersGid = "1296203013";
        private const string LootGid = "492897648"; // sheet with the shape canvas
        private const string DialogsGid = "915816125"; // trigger,text,icon — см. docs/dialogs.md

        [MenuItem("Mimic/Import Configs from Google Sheets")]
        private static void Import()
        {
            int ok = 0, total = 4;
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
                { WriteConfig("loot.csv.txt", LootCanvasImporter.BuildLootCsv(lootText)); ok++; }
                else Debug.LogError($"[ImportConfigs] loot failed: {err}");

                EditorUtility.DisplayProgressBar("Import Configs", "dialogs.csv.txt", 3f / total);
                if (TryDownload(DialogsGid, out string dialogsText, out err))
                { WriteConfig("dialogs.csv.txt", dialogsText); ok++; }
                else Debug.LogError($"[ImportConfigs] dialogs failed: {err}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ImportConfigs] Done: {ok}/{total} configs imported.");
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
