using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA01
{
    public enum IA01EconomicState
    {
        Prosperity,
        Stable,
        Alert,
        Crisis,
        Collapse
    }

    public enum IA01EconomicAction
    {
        BuildMilitary,
        BuildEconomy,
        ReduceSpending,
        SellAssets,
        RequestLoan,
        InvestResearch,
        AdjustTaxes,
        DiplomaticAgreement
    }

    [Serializable]
    public sealed class IA01EconomicDecision
    {
        public IA01EconomicAction action;
        public float utility;
        public string reason = string.Empty;
    }

    /// <summary>Estado economico local de uma instancia. Nenhum campo e statico.</summary>
    public sealed class IA01EconomicModel
    {
        private readonly IA01NationProfile profile;
        private readonly Queue<string> history = new Queue<string>(8);
        public IA01EconomicState State { get; private set; } = IA01EconomicState.Stable;
        public IA01EconomicDecision LastDecision { get; private set; }
        public IReadOnlyCollection<string> History => history;
        public string Status => State + " | " + (LastDecision != null ? LastDecision.action.ToString() : "aguardando");

        public IA01EconomicModel(IA01NationProfile profile) { this.profile = profile; }

        public void Refresh(DadosPaisGoverno country)
        {
            if (country == null) return;
            float food = country.populacao > 0 ? country.comida / (float)Mathf.Max(1, country.populacao) : country.comida;
            int emergencyReserve = 2500;
            if (country.estabilidade <= 0f && country.saldo <= 0 && country.divida > Mathf.Max(1000f, country.saldo * 2f)) State = IA01EconomicState.Collapse;
            else if (country.divida > Mathf.Max(1000f, Mathf.Max(1f, (float)country.saldo) * 1.5f) || country.comida < 0 || food < 0.02f) State = IA01EconomicState.Crisis;
            else if (country.saldo < Mathf.Max(500, emergencyReserve) || country.divida > Mathf.Max(500f, country.saldo * 0.65f) || food < 0.08f) State = IA01EconomicState.Alert;
            else if (country.saldo >= Mathf.Max(12000, (profile != null ? profile.InitialTreasury : 10000) * 2) && country.divida < country.saldo * 0.15f && country.comida > Mathf.Max(1000, country.populacao)) State = IA01EconomicState.Prosperity;
            else State = IA01EconomicState.Stable;
            LastDecision = Choose(country);
            AddHistory(State + ":" + LastDecision.action);
        }

        public float CalculateUtility(IA01EconomicAction action, DadosPaisGoverno country)
        {
            if (country == null) return 0f;
            float military = profile != null ? profile.MilitaryPriority : 0.33f;
            float economy = profile != null ? profile.EconomyPriority : 0.34f;
            float diplomacy = profile != null ? profile.DiplomacyPriority : 0.33f;
            float risk = profile != null ? profile.RiskTolerance : 50f;
            bool defenseFederation = country.federacaoGlobal == SistemaFederacoesGlobais.TipoFederacao.AliancaDefesa.ToString();
            bool hasAdjustmentLoan = country.emprestimos != null && country.emprestimos.Exists(x => x != null && x.ajusteEstrutural && !x.inadimplente);
            if (defenseFederation) military += 0.18f;
            else economy += 0.12f + diplomacy * 0.08f;
            if (hasAdjustmentLoan) military *= 0.55f;
            float crisis = State == IA01EconomicState.Crisis || State == IA01EconomicState.Collapse ? 1f : 0f;
            switch (action)
            {
                case IA01EconomicAction.BuildMilitary: return military * (1f + (profile != null ? profile.MilitaryPriority : 0f)) - crisis * (1f - risk / 100f);
                case IA01EconomicAction.BuildEconomy: return economy * (1f + crisis * 2f);
                case IA01EconomicAction.ReduceSpending: return crisis * 2f + (country.divida > country.saldo ? 1f : 0f);
                case IA01EconomicAction.SellAssets: return country.saldo < 0 ? 2f : crisis * 0.6f;
                case IA01EconomicAction.RequestLoan: return crisis * risk / 100f;
                case IA01EconomicAction.InvestResearch: return economy * 0.6f + military * 0.2f;
                case IA01EconomicAction.AdjustTaxes: return country.estabilidade < 45f ? 1.2f : economy * 0.4f;
                default: return diplomacy;
            }
        }

        private IA01EconomicDecision Choose(DadosPaisGoverno country)
        {
            IA01EconomicAction best = IA01EconomicAction.BuildEconomy;
            float score = float.MinValue;
            foreach (IA01EconomicAction action in Enum.GetValues(typeof(IA01EconomicAction)))
            {
                float value = CalculateUtility(action, country);
                if (value > score) { score = value; best = action; }
            }
            return new IA01EconomicDecision { action = best, utility = score, reason = State.ToString() };
        }

        private void AddHistory(string entry)
        {
            history.Enqueue(entry);
            while (history.Count > 8) history.Dequeue();
        }
    }
}
