using System;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    [Serializable]
    public sealed class IA03RelatorioSnapshot
    {
        public int UnidadesPropriasDisponiveis;
        public int UnidadesInimigasConhecidas;
        public int UnidadesPropriasPerdidas;
        public int InimigosDestruidos;
        public int BatalhasVencidas;
        public int BatalhasPerdidas;
        public float PrejuizoEconomicoInimigo;
        public float PrejuizoEconomicoProprio;
        public float DanoEstruturalInimigo;
        public float DanoEstruturalProprio;
        public long SaldoNacional;
        public int EstoqueComida;
        public int EstoquePetroleo;
        public int EstoqueEnergia;
        public float DeficitComida;
        public float DeficitPetroleo;
        public float PressaoEconomicaNacional;
        public int ObjetivosCapturados;
        public int ObjetivosPerdidos;
        public float CapacidadeMilitarRestante;
        public float MomentoDoRelatorio;

        public int TotalDeBatalhas => BatalhasVencidas + BatalhasPerdidas;
        public float Dominio => TotalDeBatalhas > 0 ? BatalhasVencidas / (float)TotalDeBatalhas : 0f;
        public float PontuacaoDeGuerra => (BatalhasVencidas * 12f)
                                          + (ObjetivosCapturados * 20f)
                                          + (InimigosDestruidos * 2f)
                                          + (PrejuizoEconomicoInimigo / 1000f)
                                          - (PressaoEconomicaNacional * 18f)
                                          - (BatalhasPerdidas * 12f)
                                          - (ObjetivosPerdidos * 20f)
                                          - (UnidadesPropriasPerdidas * 2f)
                                          - (PrejuizoEconomicoProprio / 1000f);
    }

    /// <summary>
    /// Agrega eventos confirmados de combate. A lista de unidades disponível é
    /// fornecida pelo WorldState já registrado do BrainMaster, sem busca global.
    /// </summary>
    public sealed class IA03RelatorioConflito
    {
        private readonly IA03RelatorioSnapshot acumulado = new IA03RelatorioSnapshot();

        public IA03RelatorioSnapshot Acumulado => acumulado;

        public void Resetar()
        {
            acumulado.UnidadesPropriasDisponiveis = 0;
            acumulado.UnidadesInimigasConhecidas = 0;
            acumulado.UnidadesPropriasPerdidas = 0;
            acumulado.InimigosDestruidos = 0;
            acumulado.BatalhasVencidas = 0;
            acumulado.BatalhasPerdidas = 0;
            acumulado.PrejuizoEconomicoInimigo = 0f;
            acumulado.PrejuizoEconomicoProprio = 0f;
            acumulado.DanoEstruturalInimigo = 0f;
            acumulado.DanoEstruturalProprio = 0f;
            acumulado.SaldoNacional = 0L;
            acumulado.EstoqueComida = 0;
            acumulado.EstoquePetroleo = 0;
            acumulado.EstoqueEnergia = 0;
            acumulado.DeficitComida = 0f;
            acumulado.DeficitPetroleo = 0f;
            acumulado.PressaoEconomicaNacional = 0f;
            acumulado.ObjetivosCapturados = 0;
            acumulado.ObjetivosPerdidos = 0;
            acumulado.CapacidadeMilitarRestante = 0f;
            acumulado.MomentoDoRelatorio = 0f;
        }

        public void RegistrarEventoCombate(CartaCombateRegistro.EventoCombate evento, int teamId, int alvoTeamId)
        {
            if (evento == null || evento.tipo != "UNIDADE DESTRUÍDA")
            {
                return;
            }

            if (evento.equipeAlvo == teamId && evento.equipeAtacante == alvoTeamId)
            {
                acumulado.UnidadesPropriasPerdidas++;
            }
            else if (evento.equipeAlvo == alvoTeamId && evento.equipeAtacante == teamId)
            {
                acumulado.InimigosDestruidos++;
            }
        }

        public void RegistrarResultadoCombate(bool venceu)
        {
            if (venceu)
            {
                acumulado.BatalhasVencidas++;
            }
            else
            {
                acumulado.BatalhasPerdidas++;
            }
        }

        public void RegistrarPrejuizoEconomico(bool inimigo, float valor)
        {
            float prejuizo = Mathf.Max(0f, valor);
            if (inimigo)
            {
                acumulado.PrejuizoEconomicoInimigo += prejuizo;
            }
            else
            {
                acumulado.PrejuizoEconomicoProprio += prejuizo;
            }
        }

        /// <summary>
        /// Registra pontos de vida estrutural efetivamente removidos. Este
        /// indicador permanece separado de dinheiro/prejuízo econômico.
        /// </summary>
        public void RegistrarDanoEstrutural(bool inimigo, float dano)
        {
            float valor = Mathf.Max(0f, dano);
            if (inimigo)
            {
                acumulado.DanoEstruturalInimigo += valor;
            }
            else
            {
                acumulado.DanoEstruturalProprio += valor;
            }
        }

        public void RegistrarObjetivoCapturado(bool proprio)
        {
            if (proprio)
            {
                acumulado.ObjetivosCapturados++;
            }
            else
            {
                acumulado.ObjetivosPerdidos++;
            }
        }

        public void RegistrarEconomia(GestorEconomiaIA03.Resumo economia)
        {
            acumulado.SaldoNacional = economia.Saldo;
            acumulado.EstoqueComida = economia.Comida;
            acumulado.EstoquePetroleo = economia.Petroleo;
            acumulado.EstoqueEnergia = economia.Energia;
            acumulado.DeficitComida = economia.DeficitComida;
            acumulado.DeficitPetroleo = economia.DeficitPetroleo;
            acumulado.PressaoEconomicaNacional = Mathf.Clamp01(economia.PressaoEconomica);
        }

        public IA03RelatorioSnapshot CriarRelatorio(
            int unidadesPropriasDisponiveis,
            int unidadesInimigasConhecidas,
            int forcaPropriaInicial,
            float momento)
        {
            acumulado.UnidadesPropriasDisponiveis = Mathf.Max(0, unidadesPropriasDisponiveis);
            acumulado.UnidadesInimigasConhecidas = Mathf.Max(0, unidadesInimigasConhecidas);
            acumulado.CapacidadeMilitarRestante = forcaPropriaInicial > 0
                ? Mathf.Clamp01(unidadesPropriasDisponiveis / (float)forcaPropriaInicial)
                : 0f;
            acumulado.MomentoDoRelatorio = momento;

            return new IA03RelatorioSnapshot
            {
                UnidadesPropriasDisponiveis = acumulado.UnidadesPropriasDisponiveis,
                UnidadesInimigasConhecidas = acumulado.UnidadesInimigasConhecidas,
                UnidadesPropriasPerdidas = acumulado.UnidadesPropriasPerdidas,
                InimigosDestruidos = acumulado.InimigosDestruidos,
                BatalhasVencidas = acumulado.BatalhasVencidas,
                BatalhasPerdidas = acumulado.BatalhasPerdidas,
                PrejuizoEconomicoInimigo = acumulado.PrejuizoEconomicoInimigo,
                PrejuizoEconomicoProprio = acumulado.PrejuizoEconomicoProprio,
                DanoEstruturalInimigo = acumulado.DanoEstruturalInimigo,
                DanoEstruturalProprio = acumulado.DanoEstruturalProprio,
                SaldoNacional = acumulado.SaldoNacional,
                EstoqueComida = acumulado.EstoqueComida,
                EstoquePetroleo = acumulado.EstoquePetroleo,
                EstoqueEnergia = acumulado.EstoqueEnergia,
                DeficitComida = acumulado.DeficitComida,
                DeficitPetroleo = acumulado.DeficitPetroleo,
                PressaoEconomicaNacional = acumulado.PressaoEconomicaNacional,
                ObjetivosCapturados = acumulado.ObjetivosCapturados,
                ObjetivosPerdidos = acumulado.ObjetivosPerdidos,
                CapacidadeMilitarRestante = acumulado.CapacidadeMilitarRestante,
                MomentoDoRelatorio = acumulado.MomentoDoRelatorio
            };
        }
    }
}
