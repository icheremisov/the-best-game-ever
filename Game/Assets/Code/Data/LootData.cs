namespace Mimic.Data
{
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
        public string AdjacencyTarget;       // empty = no property
        public AdjacencyEffect[] AdjacencyEffects; // never null; empty if no property
    }
}
