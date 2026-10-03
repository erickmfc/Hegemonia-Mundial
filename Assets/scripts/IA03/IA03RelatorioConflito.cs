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
        public int EstruturasInimigasDestruidas;
        public int EstruturasPropriasDestruidas;
        public int InfantariaPropriaDisponivel;
        public int TanquesPropriosDisponiveis;
        public int AvioesPropriosDisponiveis;
        public int NaviosPropriosDisponiveis;
        public int SubmarinosPropriosDisponiveis;
        public int PortaAvioesPropriosDisponiveis;
        public int QuarteisProprios;
        public int FabricasProprias;
        public int EstaleirosProprios;
        public int AeroportosMilitaresProprios;
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
            acumulado.EstruturasInimigasDestruidas = 0;
            acumulado.EstruturasPropriasDestruidas = 0;
            acumulado.InfantariaPropriaDisponivel = 0;
            acumulado.TanquesPropriosDisponiveis = 0;
            acumulado.AvioesPropriosDisponiveis = 0;
            acumulado.NaviosPropriosDisponiveis = 0;
            acumulado.SubmarinosPropriosDisponiveis = 0;
            acumulado.PortaAvioesPropriosDisponiveis = 0;
            acumulado.QuarteisProprios = 0;
            acumulado.FabricasProprias = 0;
            acumulado.EstaleirosProprios = 0;
            acumulado.AeroportosMilitaresProprios = 0;
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

            if (evento.alvoEhEstrutura)
            {
                if (evento.equipeAlvo == teamId && evento.equipeAtacante == alvoTeamId)
                {
                    acumulado.EstruturasPropriasDestruidas++;
                }
                else if (evento.equipeAlvo == alvoTeamId && evento.equipeAtacante == teamId)
                {
                    acumulado.EstruturasInimigasDestruidas++;
                }
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

        public bool AtingiuDominioMinimo(int minimoDeBatalhas, float dominioMinimo)
        {
            return acumulado.TotalDeBatalhas >= Mathf.Max(1, minimoDeBatalhas)
                   && acumulado.Dominio >= Mathf.Clamp01(dominioMinimo);
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

        public void RegistrarForcasProprias(Hegemonia.AI.BrainMaster.IA_ForceSnapshot forca)
        {
            if (forca == null)
            {
                return;
            }

            acumulado.InfantariaPropriaDisponivel = Mathf.Max(0, forca.InfantryUnits);
            acumulado.TanquesPropriosDisponiveis = Mathf.Max(0, forca.TankUnits);
            acumulado.AvioesPropriosDisponiveis = Mathf.Max(0, forca.FixedWingAircraft + forca.Helicopters);
            acumulado.PortaAvioesPropriosDisponiveis = Mathf.Max(0, forca.AircraftCarriers);
            acumulado.NaviosPropriosDisponiveis = Mathf.Max(0, forca.NavalUnits - forca.AircraftCarriers);
            acumulado.SubmarinosPropriosDisponiveis = Mathf.Max(0, forca.Submarines);
            acumulado.QuarteisProprios = Mathf.Max(0, forca.BarracksCount);
            acumulado.FabricasProprias = Mathf.Max(0, forca.FactoryCount);
            acumulado.EstaleirosProprios = Mathf.Max(0, forca.ShipyardCount);
            acumulado.AeroportosMilitaresProprios = Mathf.Max(0, forca.MilitaryAirportCount);
        }

        public IA03RelatorioSnapshot CriarRelatorio(
            int unidadesPropriasDisponiveis,
            int unidadesInimigasConhecidas,
            int forcaPropriaInicial,
            float momento,
            Hegemonia.AI.BrainMaster.IA_ForceSnapshot forcaPropria)
        {
            acumulado.UnidadesPropriasDisponiveis = Mathf.Max(0, unidadesPropriasDisponiveis);
            acumulado.UnidadesInimigasConhecidas = Mathf.Max(0, unidadesInimigasConhecidas);
            acumulado.CapacidadeMilitarRestante = forcaPropriaInicial > 0
                ? Mathf.Clamp01(unidadesPropriasDisponiveis / (float)forcaPropriaInicial)
                : 0f;
            acumulado.MomentoDoRelatorio = momento;
            RegistrarForcasProprias(forcaPropria);

            return new IA03RelatorioSnapshot
            {
                UnidadesPropriasDisponiveis = acumulado.UnidadesPropriasDisponiveis,
                UnidadesInimigasConhecidas = acumulado.UnidadesInimigasConhecidas,
                UnidadesPropriasPerdidas = acumulado.UnidadesPropriasPerdidas,
                InimigosDestruidos = acumulado.InimigosDestruidos,
                EstruturasInimigasDestruidas = acumulado.EstruturasInimigasDestruidas,
                EstruturasPropriasDestruidas = acumulado.EstruturasPropriasDestruidas,
                InfantariaPropriaDisponivel = acumulado.InfantariaPropriaDisponivel,
                TanquesPropriosDisponiveis = acumulado.TanquesPropriosDisponiveis,
                AvioesPropriosDisponiveis = acumulado.AvioesPropriosDisponiveis,
                NaviosPropriosDisponiveis = acumulado.NaviosPropriosDisponiveis,
                SubmarinosPropriosDisponiveis = acumulado.SubmarinosPropriosDisponiveis,
                PortaAvioesPropriosDisponiveis = acumulado.PortaAvioesPropriosDisponiveis,
                QuarteisProprios = acumulado.QuarteisProprios,
                FabricasProprias = acumulado.FabricasProprias,
                EstaleirosProprios = acumulado.EstaleirosProprios,
                AeroportosMilitaresProprios = acumulado.AeroportosMilitaresProprios,
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
