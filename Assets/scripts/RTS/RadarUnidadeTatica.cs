using UnityEngine;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;

namespace Hegemonia.RTS
{
    /// <summary>
    /// Estado de emissão radar de uma unidade. Unidades do jogador são
    /// controladas pelo menu; unidades de equipes de IA usam decisão automática.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RadarUnidadeTatica : MonoBehaviour
    {
        [SerializeField, Min(50f)] private float alcanceRadar = 900f;
        [SerializeField] private bool radarLigado;

        private float proximaDecisaoIA;
        private float manterLigadoAte;
        private float proximaCobrancaEnergia;
        private float podeTentarLigarApos;
        private bool bloqueadoPorEnergia;

        private const float IntervaloCobrancaEnergia = 60f;
        private const float IntervaloNovaTentativaEnergia = 15f;

        public bool RadarLigado => radarLigado;
        public bool BloqueadoPorEnergia => bloqueadoPorEnergia;
        public float AlcanceRadar => Mathf.Max(50f, alcanceRadar);

        public void DefinirRadar(bool ligado)
        {
            if (!ligado)
            {
                radarLigado = false;
                return;
            }

            TentarLigarRadar(ObterTimeJogador());
        }

        public bool AlternarRadar()
        {
            if (radarLigado)
            {
                radarLigado = false;
                return false;
            }

            return TentarLigarRadar(ObterTimeJogador());
        }

        public bool TentarLigarRadar(int equipe)
        {
            if (radarLigado) return true;
            if (Time.unscaledTime < podeTentarLigarApos) return false;
            if (!TentarPagarEnergia(equipe))
            {
                radarLigado = false;
                bloqueadoPorEnergia = true;
                podeTentarLigarApos = Time.unscaledTime + IntervaloNovaTentativaEnergia;
                proximaCobrancaEnergia = podeTentarLigarApos;
                return false;
            }

            radarLigado = true;
            bloqueadoPorEnergia = false;
            proximaCobrancaEnergia = Time.unscaledTime + IntervaloCobrancaEnergia;
            return true;
        }

        public bool AtualizarCustoEnergia()
        {
            if (!radarLigado || Time.unscaledTime < proximaCobrancaEnergia)
                return radarLigado;

            int equipe = GetComponent<IdentidadeUnidade>() != null
                ? GetComponent<IdentidadeUnidade>().teamID
                : ObterTimeJogador();
            if (TentarPagarEnergia(equipe))
            {
                proximaCobrancaEnergia = Time.unscaledTime + IntervaloCobrancaEnergia;
                bloqueadoPorEnergia = false;
                return true;
            }

            radarLigado = false;
            bloqueadoPorEnergia = true;
            podeTentarLigarApos = Time.unscaledTime + IntervaloNovaTentativaEnergia;
            proximaCobrancaEnergia = podeTentarLigarApos;
            return false;
        }

        private static bool TentarPagarEnergia(int equipe)
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null) return true;

            // A cobrança por unidade é intencionalmente pequena: 1 unidade
            // ao ligar e outra por minuto enquanto o radar emitir.
            if (equipe == governo.teamJogador)
            {
                GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
                if (recursos != null)
                    return recursos.TentarGastar(custoEnergia: 1);
            }

            DadosPaisGoverno pais = governo.ObterPais(equipe);
            if (pais == null) return true;
            if (pais.energia <= 0) return false;
            pais.energia--;
            return true;
        }

        private static int ObterTimeJogador()
        {
            return SistemaGovernoMundial.Instancia != null
                ? Mathf.Max(1, SistemaGovernoMundial.Instancia.teamJogador)
                : 1;
        }

        public void AtualizarAlcance(IdentidadeUnidade identidade)
        {
            if (identidade == null) return;

            BoeingE3Reconhecimento e3 = GetComponent<BoeingE3Reconhecimento>();
            if (e3 != null)
            {
                alcanceRadar = Mathf.Max(500f, e3.alcanceReconhecimento);
                return;
            }

            IA_ConstructionMetadata metadata = GetComponent<IA_ConstructionMetadata>();
            if (metadata != null && metadata.IsRadar)
            {
                alcanceRadar = 1800f;
                return;
            }

            switch (identidade.tipoUnidade)
            {
                case TipoUnidade.Naval: alcanceRadar = 1500f; break;
                case TipoUnidade.Aereo: alcanceRadar = 1250f; break;
                case TipoUnidade.Veiculo: alcanceRadar = 700f; break;
                default: alcanceRadar = 550f; break;
            }

            if (GetComponent<ControleSubmarino>() != null)
                alcanceRadar = 450f;
        }

        /// <summary>
        /// A IA emite enquanto patrulha/reconhece ou quando detecta uma força
        /// hostil próxima. Após a ameaça sair do alcance, mantém a emissão por
        /// alguns segundos e volta ao silêncio.
        /// </summary>
        public void AtualizarDecisaoIA(IdentidadeUnidade identidade, IReadOnlyList<IdentidadeUnidade> unidades, int timeJogador)
        {
            if (identidade == null || identidade.teamID <= 0 || identidade.teamID == timeJogador
                || Time.unscaledTime < proximaDecisaoIA)
                return;

            proximaDecisaoIA = Time.unscaledTime + 1.5f;
            bool patrulhando = false;
            ControleUnidade controle = GetComponent<ControleUnidade>();
            if (controle != null)
                patrulhando = controle.OrdemAtual == OrdemControleUnidade.Patrulhando;

            BoeingE3Reconhecimento e3 = GetComponent<BoeingE3Reconhecimento>();
            ControleAviao aviao = GetComponent<ControleAviao>();
            if (e3 != null && e3.reconhecimentoAtivo && aviao != null
                && (aviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao
                    || aviao.estadoAtual == ControleAviao.EstadoAviao.Decolando))
                patrulhando = true;

            bool contatoHostil = false;
            float expiraContatoHostilConhecido = 0f;
            RTSVisibilityService visibilidade = RTSVisibilityService.Instancia;
            if (unidades != null)
            {
                for (int i = 0; i < unidades.Count; i++)
                {
                    IdentidadeUnidade alvo = unidades[i];
                    if (alvo == null || alvo == identidade || !alvo.gameObject.activeInHierarchy
                        || alvo.teamID <= 0 || !RTSVisibilityService.TeamsAtWar(identidade.teamID, alvo.teamID))
                        continue;

                    if (visibilidade != null && visibilidade.IsVisibleToTeam(identidade.teamID, alvo))
                    {
                        contatoHostil = true;
                        break;
                    }

                    // A IA também mantém o radar ligado ao agir sobre um
                    // contato recente compartilhado por aliados ou detectado
                    // antes de o alvo sair do alcance.
                    if (visibilidade != null
                        && visibilidade.TryGetContactForTeam(identidade.teamID, alvo, out RTSVisibilityContact contatoConhecido)
                        && contatoConhecido != null
                        && contatoConhecido.lastKnownExpiresAt > Time.unscaledTime)
                    {
                        expiraContatoHostilConhecido = Mathf.Max(
                            expiraContatoHostilConhecido,
                            contatoConhecido.lastKnownExpiresAt);
                    }
                }
            }

            if (patrulhando || contatoHostil)
                manterLigadoAte = Time.unscaledTime + 8f;
            else if (expiraContatoHostilConhecido > manterLigadoAte)
                manterLigadoAte = expiraContatoHostilConhecido;

            bool deveEmitir = Time.unscaledTime < manterLigadoAte;
            if (deveEmitir)
                TentarLigarRadar(identidade.teamID);
            else
                radarLigado = false;
        }
    }
}
