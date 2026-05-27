using System.Collections.Generic;
using System.Text;

namespace Mimic.Logic
{
    public static class CsvLoader
    {
        // Parses a single CSV line with double-quote escaping.
        // Does NOT handle multi-line quoted fields (kept simple for jam).
        public static string[] ParseLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else sb.Append(ch);
                }
                else
                {
                    if (ch == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else if (ch == '"' && sb.Length == 0) inQuotes = true;
                    else sb.Append(ch);
                }
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }

        // Parses full CSV (LF or CRLF), skips header (first non-blank line), skips blank lines.
        public static List<string[]> ParseAll(string text)
        {
            var rows = new List<string[]>();
            var lines = text.Replace("\r\n", "\n").Split('\n');
            bool headerSkipped = false;
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }
                rows.Add(ParseLine(raw));
            }
            return rows;
        }
    }
}
