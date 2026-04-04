namespace Hegemonia.AI.BrainMaster
{
    public static class IA_CombatTelemetry
    {
        private static int _activeMissiles;
        private static int _activeProjectiles;

        public static int ActiveMissiles
        {
            get { return _activeMissiles; }
        }

        public static int ActiveProjectiles
        {
            get { return _activeProjectiles; }
        }

        public static void RegisterMissile()
        {
            _activeMissiles++;
        }

        public static void UnregisterMissile()
        {
            if (_activeMissiles > 0)
            {
                _activeMissiles--;
            }
        }

        public static void RegisterProjectile()
        {
            _activeProjectiles++;
        }

        public static void UnregisterProjectile()
        {
            if (_activeProjectiles > 0)
            {
                _activeProjectiles--;
            }
        }
    }
}
