using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaEspionagemSnapshot
    {
        public int Nivel;
        public int EstimativaTerrestre;
        public int EstimativaNaval;
        public int EstimativaAerea;
        public int EstimativaPetroleo;
        public int EstimativaEnergia;
        public bool ConheceAeroporto;
        public bool ConheceEstaleiro;
        public bool ConheceRadar;
        public float ConfiancaMilitar;
        public float ConfiancaEconomica;
        public string Resumo = "sem inteligencia";
    }

    public sealed class IA_DeusaEspionagem
    {
        private readonly List<IA_EnemyObservation> _memoria = new List<IA_EnemyObservation>(64);
        private readonly List<IdentidadeUnidade> _unidades = new List<IdentidadeUnidade>(256);

        public IA_DeusaEspionagemSnapshot UltimoSnapshot { get; } = new IA_DeusaEspionagemSnapshot();
        public string UltimoResumo => UltimoSnapshot.Resumo;

        public IA_DeusaEspionagemSnapshot Atualizar(IA_Context context, IA_DeusaConfig config, DeusaEstagio estagio)
        {
            if (context == null || context.WorldState == null)
            {
                return UltimoSnapshot;
            }

            int nivel = ResolverNivelEfetivo(config, estagio);
            UltimoSnapshot.Nivel = nivel;
            UltimoSnapshot.ConfiancaMilitar = Mathf.Clamp01(0.20f + nivel * 0.18f);
            UltimoSnapshot.ConfiancaEconomica = Mathf.Clamp01(0.15f + nivel * 0.18f);
            UltimoSnapshot.EstimativaTerrestre = 0;
            UltimoSnapshot.EstimativaNaval = 0;
            UltimoSnapshot.EstimativaAerea = 0;
            UltimoSnapshot.EstimativaPetroleo = 0;
            UltimoSnapshot.EstimativaEnergia = 0;
            UltimoSnapshot.ConheceAeroporto = false;
            UltimoSnapshot.ConheceEstaleiro = false;
            UltimoSnapshot.ConheceRadar = false;

            context.WorldState.FillEnemyMemory(_memoria, 300f);
            for (int i = 0; i < _memoria.Count; i++)
            {
                IA_EnemyObservation obs = _memoria[i];
                if (obs == null)
                {
                    continue;
                }

                string nome = IA_Text.Normalize(obs.UnitName);
                if (obs.Domain == IA_Domain.Naval)
                {
                    UltimoSnapshot.EstimativaNaval++;
                }
                else if (obs.Domain == IA_Domain.Air)
                {
                    UltimoSnapshot.EstimativaAerea++;
                }
                else
                {
                    UltimoSnapshot.EstimativaTerrestre++;
                }

                UltimoSnapshot.ConheceAeroporto |= nome.Contains("aeroporto") || nome.Contains("airport");
                UltimoSnapshot.ConheceEstaleiro |= nome.Contains("estaleiro");
                UltimoSnapshot.ConheceRadar |= nome.Contains("radar");
            }

            if (nivel >= 3 || (config != null && !config.usarEspionagemJusta && nivel >= 2))
            {
                RegistroEntidadesJogo.FillUnidades(_unidades);
                int realTerrestre = 0;
                int realNaval = 0;
                int realAereo = 0;

                for (int i = 0; i < _unidades.Count; i++)
                {
                    IdentidadeUnidade unidade = _unidades[i];
                    if (unidade == null || unidade.teamID != 1 || !unidade.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    switch (unidade.tipoUnidade)
                    {
                        case TipoUnidade.Naval:
                            if (!IA_Text.Normalize(unidade.name).Contains("petroleiro"))
                            {
                                realNaval++;
                            }
                            break;
                        case TipoUnidade.Aereo:
                            realAereo++;
                            break;
                        case TipoUnidade.Infantaria:
                        case TipoUnidade.Veiculo:
                            realTerrestre++;
                            break;
                    }
                }

                int bucket = nivel >= 4 ? 1 : 2;
                UltimoSnapshot.EstimativaTerrestre = Aproximar(realTerrestre, bucket);
                UltimoSnapshot.EstimativaNaval = Aproximar(realNaval, bucket);
                UltimoSnapshot.EstimativaAerea = Aproximar(realAereo, bucket);
            }

            DadosPaisGoverno jogador = SistemaGovernoMundial.Instancia != null
                ? SistemaGovernoMundial.Instancia.ObterPais(1)
                : null;
            if (jogador != null && nivel >= 2)
            {
                UltimoSnapshot.EstimativaPetroleo = Aproximar(jogador.petroleo, nivel >= 4 ? 25 : 100);
                UltimoSnapshot.EstimativaEnergia = Aproximar(Mathf.RoundToInt(jogador.energiaProduzida), nivel >= 4 ? 10 : 25);
            }

            UltimoSnapshot.Resumo = "nivel=" + nivel
                                    + " | terra=" + UltimoSnapshot.EstimativaTerrestre
                                    + " | mar=" + UltimoSnapshot.EstimativaNaval
                                    + " | ar=" + UltimoSnapshot.EstimativaAerea
                                    + " | conf=" + UltimoSnapshot.ConfiancaMilitar.ToString("0.00");
            return UltimoSnapshot;
        }

        private static int ResolverNivelEfetivo(IA_DeusaConfig config, DeusaEstagio estagio)
        {
            DeusaNivelEspionagem baseNivel = config == null
                ? DeusaNivelEspionagem.Justa
                : (config.usarEspionagemJusta ? DeusaNivelEspionagem.Justa : DeusaNivelEspionagem.Avancada);

            if (baseNivel == DeusaNivelEspionagem.Desligada)
            {
                return 0;
            }

            int nivel = estagio >= DeusaEstagio.GuerraTotal ? 4
                : estagio >= DeusaEstagio.TensaoGeopolitica ? 3
                : estagio >= DeusaEstagio.ProjecaoRegional ? 2
                : 1;
            if (config != null)
            {
                nivel += config.BonusIntel();
            }

            if (baseNivel == DeusaNivelEspionagem.Justa)
            {
                nivel = Mathf.Min(nivel, 4);
            }
            else
            {
                nivel = Mathf.Clamp(nivel + 1, 1, 4);
            }

            return Mathf.Clamp(nivel, 0, 4);
        }

        private static int Aproximar(int valor, int bucket)
        {
            if (bucket <= 1)
            {
                return Mathf.Max(0, valor);
            }

            return Mathf.Max(0, Mathf.RoundToInt(valor / (float)bucket) * bucket);
        }
    }
}
