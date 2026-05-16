using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaPerformance
    {
        private float _nextQuick;
        private float _nextEconomy;
        private float _nextBuild;
        private float _nextExpansion;
        private float _nextDiplomacy;
        private float _nextStrategy;
        private float _nextMap;

        public string UltimoResumo { get; private set; } = "ciclos DEUSA aguardando";

        public void Reset(float now)
        {
            _nextQuick = now;
            _nextEconomy = now;
            _nextBuild = now;
            _nextExpansion = now;
            _nextDiplomacy = now;
            _nextStrategy = now;
            _nextMap = now;
        }

        public bool DeveRodarAlertas(float now)
        {
            return Consumir(ref _nextQuick, now, 0.25f);
        }

        public bool DeveRodarEconomia(float now)
        {
            return Consumir(ref _nextEconomy, now, 1f);
        }

        public bool DeveRodarConstrucao(float now)
        {
            return Consumir(ref _nextBuild, now, 2f);
        }

        public bool DeveRodarExpansao(float now)
        {
            return Consumir(ref _nextExpansion, now, 3f);
        }

        public bool DeveRodarDiplomacia(float now)
        {
            return Consumir(ref _nextDiplomacy, now, 5f);
        }

        public bool DeveRodarEstrategia(float now)
        {
            return Consumir(ref _nextStrategy, now, 8f);
        }

        public bool DeveRodarMapa(float now)
        {
            return Consumir(ref _nextMap, now, 10f);
        }

        public void AplicarBudget(Hegemonia.AI.BrainMaster.IA_BrainMaster brain, int baseline)
        {
            if (brain == null)
            {
                return;
            }

            int target = baseline;
            if (DiagnosticoDesempenhoJogo.RuntimeSaturado())
            {
                target = Mathf.Min(target, 2);
            }
            else if (DiagnosticoDesempenhoJogo.RuntimeSobPressao())
            {
                target = Mathf.Min(target, 3);
            }

            brain.MaxCommandsPerFrame = Mathf.Max(1, target);
            UltimoResumo = "cmd/frame=" + brain.MaxCommandsPerFrame
                           + " | saturado=" + DiagnosticoDesempenhoJogo.RuntimeSaturado()
                           + " | pressao=" + DiagnosticoDesempenhoJogo.RuntimeSobPressao();
        }

        private static bool Consumir(ref float nextTime, float now, float interval)
        {
            if (now < nextTime)
            {
                return false;
            }

            nextTime = now + Mathf.Max(0.15f, interval);
            return true;
        }
    }
}
