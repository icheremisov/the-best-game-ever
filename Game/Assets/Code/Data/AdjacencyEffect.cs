using System;
using System.Collections.Generic;

namespace Mimic.Data
{
    public enum EffectType { Gold, Acid }

    public struct AdjacencyEffect
    {
        public EffectType Type;
        public float Multiplier; // +0.5 = +50%, -0.3 = -30%

        public static AdjacencyEffect[] ParseList(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Array.Empty<AdjacencyEffect>();
            var parts = raw.Split(';');
            var result = new List<AdjacencyEffect>(parts.Length);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;
                result.Add(ParseOne(trimmed));
            }
            return result.ToArray();
        }

        private static AdjacencyEffect ParseOne(string token)
        {
            int colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
                throw new FormatException($"Effect must be '<type>:<sign><n>%': got '{token}'");

            string typeStr = token.Substring(0, colon).Trim().ToLowerInvariant();
            EffectType type = typeStr switch
            {
                "gold" => EffectType.Gold,
                "acid" => EffectType.Acid,
                _ => throw new FormatException($"Unknown effect type '{typeStr}'")
            };

            string val = token.Substring(colon + 1).Trim();
            if (!val.EndsWith("%"))
                throw new FormatException($"Effect value must end with %: '{val}'");
            val = val.Substring(0, val.Length - 1);
            if (!val.StartsWith("+") && !val.StartsWith("-"))
                throw new FormatException($"Effect value must start with + or -: '{val}'");

            if (!float.TryParse(val, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float pct))
                throw new FormatException($"Effect value not a number: '{val}'");

            return new AdjacencyEffect { Type = type, Multiplier = pct / 100f };
        }
    }
}
