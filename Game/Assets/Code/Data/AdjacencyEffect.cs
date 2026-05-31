using System;

namespace Mimic.Data
{
    public enum EffectType { Gold, Acid }

    public struct AdjacencyEffect
    {
        public EffectType Type;
        public float Multiplier; // +0.5 = +50%, -0.3 = -30%
        public bool Stackable;   // '*' суффикс: применять за каждый прилегающий инстанс

        // Парсит ОДИН эффект: '<type>:<sign?><n>%' с опциональным '*' в конце.
        // Знак опционален: 'gold:5%' == 'gold:+5%'.
        public static AdjacencyEffect Parse(string token)
        {
            token = token.Trim();

            bool stackable = false;
            if (token.EndsWith("*"))
            {
                stackable = true;
                token = token.Substring(0, token.Length - 1).Trim();
            }

            int colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
                throw new FormatException($"Эффект должен быть '<type>:<sign><n>%': '{token}'");

            string typeStr = token.Substring(0, colon).Trim().ToLowerInvariant();
            EffectType type = typeStr switch
            {
                "gold" => EffectType.Gold,
                "acid" => EffectType.Acid,
                _ => throw new FormatException($"Неизвестный тип эффекта '{typeStr}'")
            };

            string val = token.Substring(colon + 1).Trim();
            if (!val.EndsWith("%"))
                throw new FormatException($"Значение эффекта должно оканчиваться на %: '{val}'");
            val = val.Substring(0, val.Length - 1).Trim();

            // NumberStyles.Float допускает ведущий знак, поэтому '+50', '-50' и '50' все валидны.
            if (!float.TryParse(val, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float pct))
                throw new FormatException($"Значение эффекта не число: '{val}'");

            return new AdjacencyEffect { Type = type, Multiplier = pct / 100f, Stackable = stackable };
        }
    }
}
