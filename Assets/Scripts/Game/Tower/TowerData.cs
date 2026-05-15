namespace Game
{
    public sealed class TowerData
    {
        public float AttackTimer;
        public Npc Target;

        public void Initialize()
        {
            AttackTimer = 0f;
            Target = null;
        }
    }
}