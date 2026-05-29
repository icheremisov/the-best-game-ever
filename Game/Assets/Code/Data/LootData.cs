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
        public int CellsRestoredOnDigest;
        public string AdjacencyTarget;            // empty = no property
        public AdjacencyEffect[] AdjacencyEffects; // never null; empty if no property

        // --- голубой луп ---
        public LootCategory Category = LootCategory.Normal;
        public int AcidRestoreOnDigest;   // кислота/мизим: +ЖС при переваривании
        public int DamageOnDigest;        // гиря/клей/какашка: урон мимику при переваривании
        public bool CanReturnToBasket = true; // наказания = false
        public bool IsGlue;               // масса клея
        public bool IsFixture;            // сердце/желудок: нельзя двигать/переваривать
        public int NeighborGoldPct;       // какашка: -50 => соседям -50% золота
    }
}
