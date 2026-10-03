using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    /// <summary>
    /// Lê o retrato econômico central e ajusta preferências dos diretores existentes.
    /// Não altera saldos, estoques nem executa compras por conta própria.
    /// </summary>
    public sealed class GestorEconomiaIA03
    {
        public struct Resumo
        {
            public long Saldo;
            public int Comida;
            public int Petroleo;
            public int Energia;
            public float DeficitComida;
            public float DeficitPetroleo;
            public float PressaoEconomica;
            public bool ReservaFinanceiraBaixa;
            public bool EstoqueEssencialBaixo;
        }

        public bool TentarAplicarPesos(
            SistemaGovernoMundial governo,
            IA_BrainMaster brain,
            PerfilPaisSO perfil,
            int teamId,
            bool emGuerra,
            out Resumo resumo)
        {
            resumo = default;
            if (governo == null || brain == null || perfil == null)
            {
                return false;
            }

            DadosPaisGoverno pais = governo.ObterPais(teamId);
            if (pais == null)
            {
                return false;
            }

            resumo.Saldo = pais.saldo;
            resumo.Comida = pais.comida;
            resumo.Petroleo = pais.petroleo;
            resumo.Energia = pais.energia;
            resumo.DeficitComida = pais.deficitComida;
            resumo.DeficitPetroleo = pais.deficitPetroleo;
            resumo.ReservaFinanceiraBaixa = pais.saldo < perfil.ReservaFinanceiraDesejada;
            resumo.EstoqueEssencialBaixo = pais.comida < 150
                                           || pais.petroleo < 150
                                           || pais.energia < 100
                                           || pais.deficitComida > 0f
                                           || pais.deficitPetroleo > 0f;

            float pressao = Mathf.Clamp01((resumo.ReservaFinanceiraBaixa ? 0.55f : 0f)
                                          + (resumo.EstoqueEssencialBaixo ? 0.45f : 0f));
            resumo.PressaoEconomica = pressao;
            brain.TradeWeight = Mathf.Clamp01(
                (perfil.InteresseEmPetroleo + perfil.InteresseEmAlimentos + perfil.TendenciaDeExportacao) / 3f
                + (resumo.EstoqueEssencialBaixo ? 0.12f : 0f)
                - (emGuerra ? 0.10f : 0f));
            brain.StockControlWeight = Mathf.Clamp01(Mathf.Lerp(0.48f, 0.95f, Mathf.Max(pressao, emGuerra ? 0.75f : 0f)));
            brain.SelfSufficiencyWeight = Mathf.Clamp01(Mathf.Lerp(0.40f, 0.90f, pressao));
            brain.ExternalDependencyWeight = Mathf.Clamp01(Mathf.Lerp(0.55f, 0.85f, resumo.EstoqueEssencialBaixo ? 1f : 0f));
            brain.EconomicRiskWeight = Mathf.Clamp01(Mathf.Lerp(0.35f, 0.90f, pressao));
            return true;
        }
    }
}
