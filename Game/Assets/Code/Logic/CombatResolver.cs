using Mimic.Data;

namespace Mimic.Logic
{
    // Чистая логика боя: урон арифметически вычитается из здоровья (без модификаторов).
    public static class CombatResolver
    {
        public static void ApplyDamageToEnemy(CombatEnemy e, int dmg)
        {
            if (e == null || dmg <= 0) return;
            e.Hp = e.Hp - dmg;
            if (e.Hp < 0) e.Hp = 0;
        }

        public static int EnemyAttackDamage(CombatEnemy e) => e != null ? e.Attack : 0;

        public static bool IsEnemyDead(CombatEnemy e) => e == null || e.Hp <= 0;

        public static bool IsPlayerDead(int mimicHp) => mimicHp <= 0;

        // Урон предмета по врагу. 0 => предмет не атакует (не расходуется, возвращается на место).
        public static int ItemAttackDamage(LootData data) => data != null && data.Attack > 0 ? data.Attack : 0;
    }
}
