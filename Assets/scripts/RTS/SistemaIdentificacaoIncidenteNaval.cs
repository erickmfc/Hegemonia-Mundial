using System;
using UnityEngine;
using Hegemonia.RTS;

namespace Hegemonia.RTS
{
    public enum TipoAlvoComercialOuMilitar
    {
        Desconhecido,
        Petroleiro,
        Cargueiro,
        NavioCruzeiro,
        TransporteTropas,
        NavioGuerra
    }

    public enum RespostaInterrogacao
    {
        RespondeuLegitimo,
        SilencioRadio,
        FalsaIdentificacao,
        AmeacaOuHostilidade
    }

    /// <summary>
    /// Gerencia as sanções diplomáticas, perdas financeiras (indenização), perda de felicidade
    /// e a inteligência de resposta à interrogação baseada em diplomacia, tensão e lealdade.
    /// </summary>
    public sealed class SistemaIdentificacaoIncidenteNaval : MonoBehaviour
    {
        public static SistemaIdentificacaoIncidenteNaval Instancia { get; private set; }

        public static event Action<string, Color> OnIncidenteDiplomaticoRegistrado;

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(this);
                return;
            }
            Instancia = this;
        }

        /// <summary>
        /// Decide de forma inteligente como o navio/avião responde à chamada 'IDENTIFIQUE-SE':
        /// - Amigos e Neutros: alta chance de responder com bandeira real.
        /// - Países rivais/hostis: podem ignorar a chamada (silêncio rádio), responder com desafio ou tentar falsa identidade.
        /// </summary>
        public RespostaInterrogacao ProcessarInterrogacao(IdentidadeUnidade alvo, out string mensagemRetorno, out Color corStatus)
        {
            mensagemRetorno = string.Empty;
            corStatus = Color.white;

            if (alvo == null)
            {
                mensagemRetorno = "> Sem retorno de sinal. Contato perdido.";
                corStatus = Color.gray;
                return RespostaInterrogacao.SilencioRadio;
            }

            int timeJogador = 1;
            bool ehAmigo = (alvo.teamID == timeJogador);
            bool ehInimigoDeclarado = (alvo.teamID == 2);
            IdentificarSeEhComercial(alvo, out TipoAlvoComercialOuMilitar tipo);

            float roll = UnityEngine.Random.value;

            // 1. Unidades amigas (comerciais ou militares da nossa coalizão)
            if (ehAmigo)
            {
                mensagemRetorno = $"> Resposta [IFF Transponder]: {alvo.gameObject.name} (REPUBLICA ATLAS - ALIADO). Bandeira confirmada.";
                corStatus = new Color(0.2f, 0.85f, 0.3f); // Verde
                return RespostaInterrogacao.RespondeuLegitimo;
            }

            // 2. Unidades Comerciais Neutras (Petroleiros, Cargueiros de outros países)
            if (!ehInimigoDeclarado && tipo != TipoAlvoComercialOuMilitar.Desconhecido && tipo != TipoAlvoComercialOuMilitar.NavioGuerra)
            {
                if (roll < 0.85f)
                {
                    mensagemRetorno = $"> Transponder Civil: {alvo.gameObject.name} [NAVIO COMERCIAL REGISTRADO]. Destino e rota compartilhados.";
                    corStatus = new Color(0.35f, 0.75f, 1f); // Ciano azul
                    return RespostaInterrogacao.RespondeuLegitimo;
                }
                else
                {
                    mensagemRetorno = $"> Canal 16 silencioso. Embarcação não respondeu na frequência internacional. Alerta de SUSPEITO emitido!";
                    corStatus = new Color(1f, 0.75f, 0.2f); // Amarelo
                    return RespostaInterrogacao.SilencioRadio;
                }
            }

            // 3. Unidades de Nações Rivais / Inimigos / Beligerantes
            if (ehInimigoDeclarado)
            {
                if (roll < 0.50f)
                {
                    // Silêncio rádio para não entregar posição
                    mensagemRetorno = $"> Silêncio de rádio total (EMCON ativo). Embarcação recusa identificação!";
                    corStatus = new Color(1f, 0.4f, 0.2f); // Laranja alerta
                    return RespostaInterrogacao.SilencioRadio;
                }
                else if (roll < 0.80f)
                {
                    // Falsa identidade se for disfarçado
                    mensagemRetorno = $"> IFF Suspeito: Transponder transmite código civil inconsistente com o perfil radar!";
                    corStatus = new Color(1f, 0.8f, 0.2f); // Amarelo alerta
                    return RespostaInterrogacao.FalsaIdentificacao;
                }
                else
                {
                    // Desafio militar hostil
                    mensagemRetorno = $"> Resposta militar agressiva: 'Afaste-se de nossas águas de operação!' Alvo confirmado HOSTIL.";
                    corStatus = new Color(1f, 0.25f, 0.25f); // Vermelho
                    return RespostaInterrogacao.AmeacaOuHostilidade;
                }
            }

            // 4. Neutros com tensão intermediária
            mensagemRetorno = $"> Contato neutro confirmou identificação: {alvo.gameObject.name}.";
            corStatus = Color.cyan;
            return RespostaInterrogacao.RespondeuLegitimo;
        }

        /// <summary>
        /// Processa disparo ou abate de embarcação aliada/neutra/civil com impacto diplomático e estabilidade.
        /// </summary>
        public void RegistrarIncidenteDanoOuAbate(IdentidadeUnidade alvo, int timeAtacante, bool foiDestruido)
        {
            if (alvo == null) return;

            int timeJogador = 1;
            if (timeAtacante != timeJogador) return;

            bool ehComercial = IdentificarSeEhComercial(alvo, out TipoAlvoComercialOuMilitar tipo);
            bool ehNeutroOuAliado = (alvo.teamID != 2);

            if (ehNeutroOuAliado || ehComercial)
            {
                float valorIndenizacao = 0f;
                float perdaFelicidadePct = 0f;

                switch (tipo)
                {
                    case TipoAlvoComercialOuMilitar.NavioCruzeiro:
                        valorIndenizacao = foiDestruido ? 850000f : 200000f;
                        perdaFelicidadePct = foiDestruido ? 15f : 5f;
                        break;
                    case TipoAlvoComercialOuMilitar.Petroleiro:
                        valorIndenizacao = foiDestruido ? 500000f : 120000f;
                        perdaFelicidadePct = foiDestruido ? 8f : 3f;
                        break;
                    case TipoAlvoComercialOuMilitar.Cargueiro:
                    case TipoAlvoComercialOuMilitar.TransporteTropas:
                        valorIndenizacao = foiDestruido ? 350000f : 90000f;
                        perdaFelicidadePct = foiDestruido ? 6f : 2f;
                        break;
                    default:
                        if (ehNeutroOuAliado)
                        {
                            valorIndenizacao = foiDestruido ? 400000f : 100000f;
                            perdaFelicidadePct = foiDestruido ? 10f : 4f;
                        }
                        break;
                }

                AplicarPenalidades(alvo, valorIndenizacao, perdaFelicidadePct, foiDestruido);
            }
        }

        private bool IdentificarSeEhComercial(IdentidadeUnidade alvo, out TipoAlvoComercialOuMilitar tipo)
        {
            tipo = TipoAlvoComercialOuMilitar.Desconhecido;
            string nome = alvo.gameObject.name.ToLowerInvariant();

            if (nome.Contains("petr") || nome.Contains("oil") || nome.Contains("tanque"))
            {
                tipo = TipoAlvoComercialOuMilitar.Petroleiro;
                return true;
            }
            if (nome.Contains("carg") || nome.Contains("container") || nome.Contains("mercante"))
            {
                tipo = TipoAlvoComercialOuMilitar.Cargueiro;
                return true;
            }
            if (nome.Contains("cruzeiro") || nome.Contains("cruise") || nome.Contains("passageiro"))
            {
                tipo = TipoAlvoComercialOuMilitar.NavioCruzeiro;
                return true;
            }
            if (nome.Contains("transporte") || nome.Contains("tropas"))
            {
                tipo = TipoAlvoComercialOuMilitar.TransporteTropas;
                return true;
            }

            return false;
        }

        private void AplicarPenalidades(IdentidadeUnidade alvo, float valorIndenizacao, float perdaFelicidadePct, bool destruido)
        {
            if (GerenciadorRecursos.Instancia != null && valorIndenizacao > 0f)
            {
                GerenciadorRecursos.Instancia.dinheiro = (long)Mathf.Max(0f, GerenciadorRecursos.Instancia.dinheiro - valorIndenizacao);
            }

            if (SistemaGovernoMundial.Instancia != null && perdaFelicidadePct > 0f)
            {
                int timeJogador = SistemaGovernoMundial.Instancia.teamJogador > 0 ? SistemaGovernoMundial.Instancia.teamJogador : 1;
                DadosPaisGoverno pais = SistemaGovernoMundial.Instancia.ObterPais(timeJogador);
                if (pais != null)
                {
                    pais.estabilidade = Mathf.Clamp(pais.estabilidade - perdaFelicidadePct, 0f, 100f);
                }
            }

            string acaoTexto = destruido ? "DESTRUÍDO" : "ALVEJADO";
            string msg = $"[INCIDENTE DIPLOMÁTICO] Embarcação civil/aliada {alvo.gameObject.name} {acaoTexto}! Multa paga: -${valorIndenizacao:N0} | Estabilidade/Felicidade: -{perdaFelicidadePct:0}%";
            
            Debug.LogWarning(msg);
            OnIncidenteDiplomaticoRegistrado?.Invoke(msg, Color.red);
        }
    }
}
