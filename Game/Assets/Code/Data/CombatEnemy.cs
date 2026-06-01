namespace Mimic.Data
{
    // Лёгкое описание противника в бою. Собирается из приключенца или из дня (Хозяин).
    public class CombatEnemy
    {
        public string Name;
        public int MaxHp;
        public int Hp;
        public int Attack;

        public static CombatEnemy FromAdventurer(AdventurerData a) => new CombatEnemy
        {
            Name = a.Name,
            MaxHp = a.Hp,
            Hp = a.Hp,
            Attack = a.Attack,
        };

        public static CombatEnemy FromOverlord(DayData d) => new CombatEnemy
        {
            Name = "Властелин",
            MaxHp = d.OverlordHp,
            Hp = d.OverlordHp,
            Attack = d.OverlordAttack,
        };
    }
}
