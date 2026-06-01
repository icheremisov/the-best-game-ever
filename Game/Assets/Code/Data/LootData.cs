namespace Mimic.Data
{
    public enum LootCategory { Normal, Reward, Punish, Fixture }

    public class LootData
    {
        public string Id;
        public string Name;
        public string Description;
        public Shape Shape;
        public int Gold;
        public int AcidCost;
        public int HealOnDigest;
        public AdjacencyRule[] AdjacencyRules;    // never null; пустой = нет свойства

        // --- голубой луп ---
        public LootCategory Category = LootCategory.Normal;
        public int AcidRestoreOnDigest;   // кислота/мизим: +ЖС при переваривании
        public int DamageOnDigest;        // гиря/клей/какашка: урон мимику при переваривании
        public bool CanReturnToBasket = true; // наказания = false
        public bool IsGlue;               // масса клея
        public bool IsFixture;            // сердце/желудок: нельзя двигать/переваривать
        public int NeighborGoldPct;       // какашка: -50 => соседям -50% золота

        // --- бой ---
        public int Attack;          // >0 => предмет можно бросить во врага (урон), предмет исчезает
        public int AttackOnDigest;  // НИЗКИЙ ПРИОРИТЕТ: урон врагу при переваривании во время боя
    }
}
