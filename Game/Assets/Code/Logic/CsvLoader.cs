using System.Collections.Generic;
using System.Text;

namespace Mimic.Logic
{
    public static class CsvLoader
    {
        // Parses a single CSV line with double-quote escaping.
        // For whole files (incl. multi-line quoted fields) use ParseAll.
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

        // Parses full CSV (RFC 4180): handles quoted fields that span multiple lines,
        // embedded commas, and escaped quotes (""). Skips the header (first non-blank
        // record) and blank records. LF and CRLF are normalised to LF (also inside fields).
        public static List<string[]> ParseAll(string text)
        {
            var rows = new List<string[]>();
            bool headerSkipped = false;
            foreach (var fields in ParseRecords(text))
            {
                if (IsBlankRecord(fields)) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }
                rows.Add(fields);
            }
            return rows;
        }

        // Splits the whole text into records of fields in a single quote-aware pass.
        // A newline outside quotes ends a record; inside quotes it is part of the field.
        // Public so tools (e.g. the loot importer) can access the header and raw records.
        public static List<string[]> ParseRecords(string text)
        {
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            var records = new List<string[]>();
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            bool fieldStart = true; // a quote opens a field only at its very start

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(ch);
                }
                else if (ch == '"' && fieldStart)
                {
                    inQuotes = true;
                    fieldStart = false;
                }
                else if (ch == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                    fieldStart = true;
                }
                else if (ch == '\n')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                    records.Add(fields.ToArray());
                    fields = new List<string>();
                    fieldStart = true;
                }
                else
                {
                    sb.Append(ch);
                    fieldStart = false;
                }
            }

            // Flush the final field/record (covers files without a trailing newline).
            fields.Add(sb.ToString());
            records.Add(fields.ToArray());
            return records;
        }

        private static bool IsBlankRecord(string[] fields)
            => fields.Length == 0 || (fields.Length == 1 && string.IsNullOrWhiteSpace(fields[0]));
    }
}
