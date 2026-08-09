using System;
using UnityEngine;

namespace Hegemonia.RTS
{
    public enum RTSResourceType
    {
        Dinheiro,
        Petroleo,
        Aco,
        Energia,
        Comida,
        Populacao
    }

    [Serializable]
    public struct RTSResourceCost
    {
        public long dinheiro;
        public int petroleo;
        public int aco;
        public int energia;
        public int comida;

        public RTSResourceCost(long dinheiro = 0, int petroleo = 0, int aco = 0, int energia = 0, int comida = 0)
        {
            this.dinheiro = Math.Max(0L, dinheiro);
            this.petroleo = Mathf.Max(0, petroleo);
            this.aco = Mathf.Max(0, aco);
            this.energia = Mathf.Max(0, energia);
            this.comida = Mathf.Max(0, comida);
        }
    }

    [Serializable]
    public struct RTSResourceSnapshot
    {
        public long dinheiro;
        public int petroleo;
        public int aco;
        public int energia;
        public int comida;
        public int populacaoAtual;
        public int populacaoMaxima;
        public string source;

        public override string ToString()
        {
            return string.Format("money={0} oil={1} steel={2} energy={3} food={4} population={5}/{6}",
                dinheiro, petroleo, aco, energia, comida, populacaoAtual, populacaoMaxima);
        }
    }

    /// <summary>
    /// Fachada de transacao para o ledger legado. Ela evita que novas features
    /// alterem recursos campo a campo e permite migrar os sistemas existentes
    /// para uma autoridade unica sem quebrar cenas antigas.
    /// </summary>
    [DefaultExecutionOrder(-8000)]
    public sealed class RTSResourceLedgerService : MonoBehaviour
    {
        public static RTSResourceLedgerService Instancia { get; private set; }

        public event Action<RTSResourceSnapshot> OnChanged;
        public string LastTransaction { get; private set; } = string.Empty;
        public int TransactionCount { get; private set; }
        public bool IsApplyingTransaction { get; private set; }

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }

            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }

        public RTSResourceSnapshot GetPlayerSnapshot(string source = "ledger")
        {
            GerenciadorRecursos resources = GerenciadorRecursos.Instancia;
            if (resources == null)
            {
                return new RTSResourceSnapshot { source = source ?? string.Empty };
            }

            return new RTSResourceSnapshot
            {
                dinheiro = resources.dinheiro,
                petroleo = resources.petroleo,
                aco = resources.aco,
                energia = resources.energia,
                comida = resources.comida,
                populacaoAtual = resources.populacaoAtual,
                populacaoMaxima = resources.populacaoMaxima,
                source = source ?? string.Empty
            };
        }

        public bool TrySpendPlayer(RTSResourceCost cost, string reason = null)
        {
            GerenciadorRecursos resources = GerenciadorRecursos.Instancia;
            if (resources == null || !HasEnough(GetPlayerSnapshot(), cost))
            {
                return false;
            }

            IsApplyingTransaction = true;
            try
            {
                resources.dinheiro -= cost.dinheiro;
                resources.petroleo -= cost.petroleo;
                resources.aco -= cost.aco;
                resources.energia -= cost.energia;
                resources.comida -= cost.comida;
                resources.NotificarAtualizacao();
            }
            finally
            {
                IsApplyingTransaction = false;
            }

            RecordTransaction("spend player: " + (reason ?? "unspecified"));
            return true;
        }

        public bool TrySpendTeam(int teamId, RTSResourceCost cost, string reason = null)
        {
            if (teamId <= 0)
            {
                return false;
            }

            if (teamId == ResolvePlayerTeam())
            {
                return TrySpendPlayer(cost, reason);
            }

            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            if (government == null || cost.petroleo > 0 || cost.aco > 0 || cost.energia > 0 || cost.comida > 0)
            {
                return false;
            }

            bool paid = government.TentarPagar(teamId, cost.dinheiro);
            if (paid)
            {
                RecordTransaction("spend team " + teamId + ": " + (reason ?? "unspecified"));
            }

            return paid;
        }

        public void AddPlayer(RTSResourceCost amount, string reason = null)
        {
            GerenciadorRecursos resources = GerenciadorRecursos.Instancia;
            if (resources == null)
            {
                return;
            }

            IsApplyingTransaction = true;
            try
            {
                resources.dinheiro += amount.dinheiro;
                resources.petroleo += amount.petroleo;
                resources.aco += amount.aco;
                resources.energia += amount.energia;
                resources.comida += amount.comida;
                resources.NotificarAtualizacao();
            }
            finally
            {
                IsApplyingTransaction = false;
            }

            RecordTransaction("add player: " + (reason ?? "unspecified"));
        }

        public bool TryProtectFoundation(int teamId, long requiredFunds, out long availableFunds)
        {
            availableFunds = 0L;
            if (teamId <= 0 || requiredFunds <= 0)
            {
                return false;
            }

            SistemaGovernoMundial government = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno country = government != null ? government.ObterPais(teamId) : null;
            if (government == null || country == null)
            {
                return false;
            }

            government.DefinirReservaFundacao(teamId, requiredFunds);
            if (country.saldo < requiredFunds)
            {
                government.AdicionarSaldo(teamId, requiredFunds - country.saldo);
            }

            availableFunds = country.saldo;
            RecordTransaction("protect foundation team " + teamId);
            return availableFunds >= requiredFunds;
        }

        public void ReleaseFoundation(int teamId)
        {
            SistemaGovernoMundial.Instancia?.LiberarReservaFundacao(teamId);
        }

        private int ResolvePlayerTeam()
        {
            return GerenciadorDePartida.Instancia != null ? GerenciadorDePartida.Instancia.idJogador : 1;
        }

        private static bool HasEnough(RTSResourceSnapshot snapshot, RTSResourceCost cost)
        {
            return snapshot.dinheiro >= cost.dinheiro
                && snapshot.petroleo >= cost.petroleo
                && snapshot.aco >= cost.aco
                && snapshot.energia >= cost.energia
                && snapshot.comida >= cost.comida;
        }

        private void RecordTransaction(string description)
        {
            LastTransaction = description ?? string.Empty;
            TransactionCount++;
            OnChanged?.Invoke(GetPlayerSnapshot("transaction"));
        }
    }
}
