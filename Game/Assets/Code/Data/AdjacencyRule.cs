using System;
using System.Collections.Generic;

namespace Mimic.Data
{
    // Один блок «таргеты | эффекты». Несколько блоков на предмете разделяются ';'.
    public struct AdjacencyRule
    {
        public string[] Targets;          // id соседей; пусто при Wildcard
        public bool Wildcard;             // таргет '*' — все НЕназванные соседи
        public AdjacencyEffect[] Effects; // эффекты блока (never null/empty в валидном правиле)

        // Грамматика поля:
        //   <block>(';'<block>)*, block := <targets>'|'<effects>
        //   targets через ',', effects через ',', '*' слева — вайлдкард, '*' в конце эффекта — стак.
        public static AdjacencyRule[] ParseRules(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<AdjacencyRule>();

            // Переносы строк (из многострочного оформления в Google Docs) — как пробел.
            raw = raw.Replace('\r', ' ').Replace('\n', ' ');

            var blocks = raw.Split(';');
            var rules = new List<AdjacencyRule>();
            foreach (var blockRaw in blocks)
            {
                var block = blockRaw.Trim();
                if (block.Length == 0) continue; // допускаем хвостовой ';'

                int bar = block.IndexOf('|');
                if (bar <= 0 || bar == block.Length - 1)
                    throw new FormatException($"Блок adjacency должен быть '<targets>|<effects>': '{block}'");

                string targetsPart = block.Substring(0, bar);
                string effectsPart = block.Substring(bar + 1);

                // --- таргеты ---
                var targets = new List<string>();
                bool wildcard = false;
                foreach (var tt in targetsPart.Split(','))
                {
                    var t = tt.Trim();
                    if (t.Length == 0) continue;
                    if (t == "*") wildcard = true;
                    else targets.Add(t);
                }
                if (wildcard && targets.Count > 0)
                    throw new FormatException($"Вайлдкард '*' нельзя смешивать с id: '{targetsPart}'");
                if (!wildcard && targets.Count == 0)
                    throw new FormatException($"Пустой список таргетов в блоке: '{block}'");

                // --- эффекты ---
                var effects = new List<AdjacencyEffect>();
                foreach (var et in effectsPart.Split(','))
                {
                    var e = et.Trim();
                    if (e.Length == 0) continue;
                    effects.Add(AdjacencyEffect.Parse(e));
                }
                if (effects.Count == 0)
                    throw new FormatException($"Пустой список эффектов в блоке: '{block}'");

                rules.Add(new AdjacencyRule
                {
                    Targets = targets.ToArray(),
                    Wildcard = wildcard,
                    Effects = effects.ToArray()
                });
            }
            return rules.ToArray();
        }
    }
}
