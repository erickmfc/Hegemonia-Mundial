using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA02
{
    [Serializable]
    public sealed class IA02ServiceDiagnosticsSnapshot
    {
        public bool HasGovernment;
        public bool HasMarket;
        public bool HasIndustrial;
        public bool HasSaveGame;
        public bool HasTimeService;
        public bool HasDifficultyService;
        public bool HasPerformanceLogger;
        public bool HasEntityRegistry;
        public bool HasInteractionLock;
        public bool HasGovernmentBridge;
        public string DifficultyCode = "normal";
        public string GovernmentSummary = string.Empty;
        public string Report = string.Empty;
        public List<string> AvailableServices = new List<string>(16);
        public List<string> MissingServices = new List<string>(16);
    }

    public sealed class IA02ServiceDiagnostics
    {
        private readonly IA02ServiceDiagnosticsSnapshot snapshot = new IA02ServiceDiagnosticsSnapshot();

        public IA02ServiceDiagnosticsSnapshot Snapshot
        {
            get { return snapshot; }
        }

        public void Refresh()
        {
            snapshot.AvailableServices.Clear();
            snapshot.MissingServices.Clear();

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            snapshot.HasGovernment = governo != null;
            if (snapshot.HasGovernment)
            {
                MarkAvailable("SistemaGovernoMundial");
                snapshot.GovernmentSummary = governo.Paises != null ? "Paises=" + governo.Paises.Count + ",Relacoes=" + governo.Relacoes.Count : "Governo sem dados";
            }
            else
            {
                MarkMissing("SistemaGovernoMundial");
            }

            snapshot.HasMarket = SistemaMercadoGlobal.Instancia != null;
            AddAvailability(snapshot.HasMarket, "SistemaMercadoGlobal");

            snapshot.HasIndustrial = SistemaIndustrialNacional.Instancia != null;
            AddAvailability(snapshot.HasIndustrial, "SistemaIndustrialNacional");

            snapshot.HasSaveGame = SistemaSaveGame.Instancia != null;
            AddAvailability(snapshot.HasSaveGame, "SistemaSaveGame");

            snapshot.HasTimeService = GerenciadorTempo.Instancia != null;
            AddAvailability(snapshot.HasTimeService, "GerenciadorTempo");

            // The project already owns the difficulty singleton. Polling it must not scan the scene.
            GameDifficultyManager difficulty = GameDifficultyManager.Instancia;
            snapshot.HasDifficultyService = difficulty != null;
            if (snapshot.HasDifficultyService)
            {
                MarkAvailable("GameDifficultyManager");
                snapshot.DifficultyCode = difficulty.ObterCodigoDificuldade();
            }
            else
            {
                MarkMissing("GameDifficultyManager");
                snapshot.DifficultyCode = "normal";
            }

            snapshot.HasPerformanceLogger = true;
            MarkAvailable("DiagnosticoDesempenhoJogo");

            snapshot.HasEntityRegistry = true;
            MarkAvailable("RegistroEntidadesJogo");

            snapshot.HasInteractionLock = true;
            MarkAvailable("InteractionModeService");

            snapshot.HasGovernmentBridge = true;
            MarkAvailable("ConectorGoverno");

            if (string.IsNullOrWhiteSpace(snapshot.GovernmentSummary))
            {
                snapshot.GovernmentSummary = snapshot.HasGovernment ? "Governo disponivel" : "Governo ausente";
            }

            snapshot.Report = BuildReport();
        }

        public string BuildReport()
        {
            List<string> parts = new List<string>(16)
            {
                "gov=" + BoolToken(snapshot.HasGovernment),
                "market=" + BoolToken(snapshot.HasMarket),
                "industrial=" + BoolToken(snapshot.HasIndustrial),
                "save=" + BoolToken(snapshot.HasSaveGame),
                "time=" + BoolToken(snapshot.HasTimeService),
                "difficulty=" + BoolToken(snapshot.HasDifficultyService),
                "perf=" + BoolToken(snapshot.HasPerformanceLogger),
                "registry=" + BoolToken(snapshot.HasEntityRegistry),
                "interaction=" + BoolToken(snapshot.HasInteractionLock),
                "bridge=" + BoolToken(snapshot.HasGovernmentBridge),
                "difficultyCode=" + snapshot.DifficultyCode,
                "summary=" + snapshot.GovernmentSummary
            };

            return string.Join(" | ", parts);
        }

        public bool HasRequiredCoreServices()
        {
            return snapshot.HasGovernment && snapshot.HasSaveGame && snapshot.HasEntityRegistry;
        }

        private void AddAvailability(bool available, string serviceName)
        {
            if (available)
            {
                MarkAvailable(serviceName);
                return;
            }

            MarkMissing(serviceName);
        }

        private void MarkAvailable(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return;
            }

            if (!snapshot.AvailableServices.Contains(serviceName))
            {
                snapshot.AvailableServices.Add(serviceName);
            }
        }

        private void MarkMissing(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return;
            }

            if (!snapshot.MissingServices.Contains(serviceName))
            {
                snapshot.MissingServices.Add(serviceName);
            }
        }

        private static string BoolToken(bool value)
        {
            return value ? "ok" : "missing";
        }
    }
}
