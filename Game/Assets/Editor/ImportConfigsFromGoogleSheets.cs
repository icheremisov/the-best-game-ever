#if UNITY_EDITOR
using System.IO;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mimic.EditorTools
{
    // Downloads the game's CSV configs straight from the shared Google Sheet
    // and overwrites Assets/Resources/Configs/*.csv.txt.
    // Each sheet tab is exported via the public CSV export endpoint by its gid.
    public static class ImportConfigsFromGoogleSheets
    {
        private const string SpreadsheetId = "1EUfW2SPLCzWxCaqrYfPNz27JhQMIxda1Z9DLqmIEQE4";
        private const string ConfigsDir = "Assets/Resources/Configs";

        // gid -> output file name
        private static readonly (string gid, string file)[] Sheets =
        {
            ("0",          "day.csv.txt"),
            ("1296203013", "adventurers.csv.txt"),
            ("824198997",  "loot.csv.txt"),
        };

        [MenuItem("Mimic/Import Configs from Google Sheets")]
        private static void Import()
        {
            int ok = 0;
            try
            {
                for (int i = 0; i < Sheets.Length; i++)
                {
                    var (gid, file) = Sheets[i];
                    EditorUtility.DisplayProgressBar(
                        "Import Configs", file, (float)i / Sheets.Length);

                    string url =
                        $"https://docs.google.com/spreadsheets/d/{SpreadsheetId}/export?format=csv&gid={gid}";

                    if (TryDownload(url, file, out string error))
                        ok++;
                    else
                        Debug.LogError($"[ImportConfigs] {file} failed: {error}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ImportConfigs] Done: {ok}/{Sheets.Length} configs imported.");
        }

        private static bool TryDownload(string url, string file, out string error)
        {
            try
            {
                using (var client = new WebClient())
                {
                    // Google's export endpoint 302-redirects; WebClient follows it automatically.
                    // Decode bytes explicitly as UTF-8 — DownloadString mangles Cyrillic.
                    byte[] bytes = client.DownloadData(url);
                    string text = new UTF8Encoding(false).GetString(bytes)
                        .TrimStart('﻿')
                        .Replace("\r\n", "\n");

                    if (text.TrimStart().StartsWith("<"))
                    {
                        error = "got HTML instead of CSV — is the sheet shared as 'anyone with the link'?";
                        return false;
                    }

                    Directory.CreateDirectory(ConfigsDir);
                    File.WriteAllText(Path.Combine(ConfigsDir, file), text);
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
    }
}
#endif
