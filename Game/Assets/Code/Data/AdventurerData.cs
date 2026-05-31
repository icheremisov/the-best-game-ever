namespace Mimic.Data
{
    public class AdventurerData
    {
        public string Id;
        public string Name;
        public string Phrase;
        public string[] LootIds;

        // --- бой ---
        public bool Battle;   // true => перед получением лута нужно сразиться
        public int Hp;        // здоровье в бою
        public int Attack;    // сила атаки в бою
    }
}
