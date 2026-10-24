using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    /// <summary>
    /// Adaptador estreito para o registro diplomático e as propostas já mantidas
    /// por SistemaGovernoMundial. Não mantém uma segunda cópia das relações.
    /// </summary>
    public sealed class GestorDiplomaciaIA03
    {
        public const float PontuacaoMinimaParaReivindicarTerritorio = 40f;

        /// <summary>
        /// Scores an eligible territory using authored regional value plus
        /// proximity and contiguity to the country's existing land.
        /// Missing resource/port/defense data remains neutral (zero).
        /// </summary>
        public static float CalcularPontuacaoTerritorial(
            DadosMapaTerritorial mapa,
            RegiaoPolitica candidata,
            int teamId)
        {
            if (mapa == null || candidata == null || candidata.tipo != TipoRegiaoPolitica.Terra || teamId <= 0)
            {
                return float.MinValue;
            }

            Vector2 centroCandidato = Centroide(candidata);
            float menorDistancia = float.MaxValue;
            bool contigua = false;
            float maiorArea = 0f;
            GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
            for (int i = 0; i < mapa.Regioes.Count; i++)
            {
                RegiaoPolitica regiao = mapa.Regioes[i];
                if (regiao == null || regiao.tipo != TipoRegiaoPolitica.Terra) continue;
                maiorArea = Mathf.Max(maiorArea, CalcularArea(regiao));
                ResultadoConsultaTerritorio estado = gerente != null
                    ? gerente.ObterEstadoDaRegiao(regiao.territorioId)
                    : ResultadoConsultaTerritorio.NaoDefinido;
                int proprietario = estado.encontrouRegiao ? estado.ownerCountryTeamId : regiao.ownerCountryTeamId;
                bool neutral = estado.encontrouRegiao ? estado.neutral : regiao.neutral;
                if (proprietario != teamId || neutral || regiao == candidata) continue;

                float distanciaCentro = Vector2.Distance(centroCandidato, Centroide(regiao));
                menorDistancia = Mathf.Min(menorDistancia, distanciaCentro);
                if (DistanciaEntreVertices(candidata, regiao) <= 0.018f)
                {
                    contigua = true;
                }
            }

            float area = CalcularArea(candidata);
            float valorArea = maiorArea > 0f ? Mathf.Clamp01(area / maiorArea) : 0f;
            float proximidade = menorDistancia < float.MaxValue
                ? 1f - Mathf.Clamp01(menorDistancia / 0.60f)
                : 0f;

            float pontuacao = 30f * proximidade
                + (contigua ? 30f : 0f)
                + 10f * Mathf.Sqrt(valorArea)
                + 15f * NormalizarValor(candidata.valorRecursos)
                + 12f * NormalizarValor(candidata.nivelInfraestrutura)
                + (candidata.acessoMaritimo ? 8f : 0f)
                + 12f * NormalizarValor(candidata.capacidadeDefensiva)
                - 15f * NormalizarValor(candidata.custoManutencao)
                + (candidata.capturable ? 4f : 0f);
            return Mathf.Clamp(pontuacao, 0f, 100f);
        }

        public bool ExisteGuerra(SistemaGovernoMundial governo, int origemTeamId, int alvoTeamId)
        {
            if (governo == null || origemTeamId <= 0 || alvoTeamId <= 0)
            {
                return false;
            }

            RelacaoPaisGoverno relacao = governo.ObterRelacao(origemTeamId, alvoTeamId);
            return relacao != null && relacao.guerraDeclarada;
        }

        public int ObterRelacao(SistemaGovernoMundial governo, int origemTeamId, int alvoTeamId)
        {
            if (governo == null || origemTeamId <= 0 || alvoTeamId <= 0)
            {
                return 0;
            }

            RelacaoPaisGoverno relacao = governo.ObterRelacao(origemTeamId, alvoTeamId);
            return relacao != null ? Mathf.Clamp(relacao.valor, -100, 100) : 0;
        }

        public bool TentarProporCessarFogo(
            SistemaGovernoMundial governo,
            int origemTeamId,
            int alvoTeamId,
            string nomePresidente,
            string motivo,
            string dedupKey,
            out string mensagem)
        {
            mensagem = string.Empty;
            if (governo == null || origemTeamId <= 0 || alvoTeamId <= 0 || origemTeamId == alvoTeamId)
            {
                mensagem = "Dados insuficientes para propor cessar-fogo.";
                return false;
            }

            foreach (PropostaInternacional propostaPendente in governo.ObterPropostasPendentesPara(alvoTeamId))
            {
                if (propostaPendente != null
                    && propostaPendente.origemTeamId == origemTeamId
                    && propostaPendente.tipo == TipoPropostaInternacional.CessarFogo)
                {
                    mensagem = "Já existe uma proposta de cessar-fogo pendente.";
                    return false;
                }
            }

            DadosPaisGoverno origem = governo.ObterPais(origemTeamId);
            string presidente = string.IsNullOrWhiteSpace(nomePresidente)
                ? origem != null ? origem.nomePresidente : governo.NomePais(origemTeamId)
                : nomePresidente.Trim();
            string texto = "Presidente " + presidente + " propõe um cessar-fogo: "
                           + (string.IsNullOrWhiteSpace(motivo) ? "suspensão imediata das hostilidades." : motivo.Trim());

            bool criada = governo.TentarCriarProposta(new PropostaInternacional
            {
                tipo = TipoPropostaInternacional.CessarFogo,
                origemTeamId = origemTeamId,
                alvoTeamId = alvoTeamId,
                quantidade = 1,
                prioridade = 80,
                motivo = texto,
                expiraEm = Time.unscaledTime + 180f,
                dedupKey = string.IsNullOrWhiteSpace(dedupKey)
                    ? "ia03_ceasefire:" + origemTeamId + ":" + alvoTeamId
                    : dedupKey
            });

            mensagem = criada ? texto : "A proposta de cessar-fogo já está registrada.";
            return criada;
        }

        public bool TentarProporIndenizacao(
            SistemaGovernoMundial governo,
            int paisCredorTeamId,
            int paisPagadorTeamId,
            float fracaoDoSaldo,
            string nomePresidente,
            string motivo,
            string dedupKey,
            out string mensagem)
        {
            mensagem = string.Empty;
            DadosPaisGoverno credor = governo != null ? governo.ObterPais(paisCredorTeamId) : null;
            DadosPaisGoverno pagador = governo != null ? governo.ObterPais(paisPagadorTeamId) : null;
            if (credor == null || pagador == null || paisCredorTeamId == paisPagadorTeamId || pagador.saldo <= 0)
            {
                mensagem = "Não há saldo pagador disponível para propor indenização.";
                return false;
            }

            float fracao = Mathf.Clamp(fracaoDoSaldo, 0.05f, 0.8f);
            long valorLongo = System.Math.Max(1L, (long)(pagador.saldo * fracao));
            int valor = (int)System.Math.Min(int.MaxValue, valorLongo);
            string presidente = string.IsNullOrWhiteSpace(nomePresidente) ? credor.nomePresidente : nomePresidente.Trim();
            string texto = "Presidente " + (string.IsNullOrWhiteSpace(presidente) ? governo.NomePais(paisCredorTeamId) : presidente)
                           + " solicita indenização de " + valor + " a " + governo.NomePais(paisPagadorTeamId)
                           + (string.IsNullOrWhiteSpace(motivo) ? "." : ": " + motivo.Trim());

            bool criada = governo.TentarCriarProposta(new PropostaInternacional
            {
                tipo = TipoPropostaInternacional.Indenizacao,
                origemTeamId = paisCredorTeamId,
                alvoTeamId = paisPagadorTeamId,
                recurso = RecursoMercado.Nenhum,
                quantidade = valor,
                precoUnitario = 1,
                prioridade = 85,
                motivo = texto,
                expiraEm = Time.unscaledTime + 180f,
                dedupKey = string.IsNullOrWhiteSpace(dedupKey)
                    ? "ia03_indenizacao:" + paisCredorTeamId + ":" + paisPagadorTeamId
                    : dedupKey
            });

            mensagem = criada ? texto : "Já existe uma proposta de indenização pendente.";
            return criada;
        }

        public bool TentarProporCessaoTerritorial(
            SistemaGovernoMundial governo,
            int paisCredorTeamId,
            int paisPagadorTeamId,
            float fracaoDeTerritorio,
            string nomePresidente,
            string motivo,
            string dedupKey,
            out string mensagem)
        {
            mensagem = string.Empty;
            if (governo == null || paisCredorTeamId <= 0 || paisPagadorTeamId <= 0 || paisCredorTeamId == paisPagadorTeamId)
            {
                mensagem = "Países inválidos para propor cessão territorial.";
                return false;
            }

            GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
            DadosMapaTerritorial mapa = gerente != null ? gerente.MapaPolitico : null;
            if (mapa == null)
            {
                mensagem = "O mapa político não está disponível para negociar territórios.";
                return false;
            }

            List<string> concedidos = SelecionarTerritoriosTerrestres(
                governo,
                paisPagadorTeamId,
                fracaoDeTerritorio,
                true,
                out mensagem);
            if (concedidos == null)
            {
                return false;
            }
            int quantidade = concedidos.Count;
            DadosPaisGoverno credor = governo.ObterPais(paisCredorTeamId);
            string presidente = !string.IsNullOrWhiteSpace(nomePresidente)
                ? nomePresidente.Trim()
                : credor != null ? credor.nomePresidente : governo.NomePais(paisCredorTeamId);
            string texto = "Presidente " + presidente + " solicita a cessão permanente de " + quantidade + " região(ões) de "
                           + governo.NomePais(paisPagadorTeamId)
                           + (string.IsNullOrWhiteSpace(motivo) ? "." : ": " + motivo.Trim());

            bool criada = governo.TentarCriarProposta(new PropostaInternacional
            {
                tipo = TipoPropostaInternacional.CessaoTerritorial,
                origemTeamId = paisCredorTeamId,
                alvoTeamId = paisPagadorTeamId,
                quantidade = quantidade,
                prioridade = 90,
                motivo = texto,
                expiraEm = Time.unscaledTime + 180f,
                dedupKey = string.IsNullOrWhiteSpace(dedupKey)
                    ? "ia03_territorio:" + paisCredorTeamId + ":" + paisPagadorTeamId
                    : dedupKey,
                territoriosConcedidos = concedidos
            });

            mensagem = criada ? texto : "Já existe uma proposta territorial pendente.";
            return criada;
        }

        public bool TentarProporCessaoTerritorialTemporaria(
            SistemaGovernoMundial governo,
            int paisCredorTeamId,
            int paisPagadorTeamId,
            float fracaoDeTerritorio,
            int duracaoDias,
            string nomePresidente,
            string motivo,
            string dedupKey,
            out string mensagem)
        {
            mensagem = string.Empty;
            if (!ValidarPartes(governo, paisCredorTeamId, paisPagadorTeamId, out mensagem))
            {
                return false;
            }

            List<string> concedidos = SelecionarTerritoriosTerrestres(
                governo,
                paisPagadorTeamId,
                fracaoDeTerritorio,
                true,
                out mensagem);
            if (concedidos == null)
            {
                return false;
            }

            int duracao = Mathf.Clamp(duracaoDias, 1, 3650);
            DadosPaisGoverno credor = governo.ObterPais(paisCredorTeamId);
            string presidente = ResolverPresidente(governo, credor, nomePresidente, paisCredorTeamId);
            string texto = "Presidente " + presidente + " propõe uma cessão territorial temporária de "
                           + concedidos.Count + " região(ões) por " + duracao + " dia(s) de jogo a "
                           + governo.NomePais(paisPagadorTeamId)
                           + (string.IsNullOrWhiteSpace(motivo) ? "." : ": " + motivo.Trim());
            bool criada = governo.TentarCriarProposta(new PropostaInternacional
            {
                tipo = TipoPropostaInternacional.CessaoTerritorialTemporaria,
                origemTeamId = paisCredorTeamId,
                alvoTeamId = paisPagadorTeamId,
                quantidade = concedidos.Count,
                duracaoDias = duracao,
                territoriosConcedidos = concedidos,
                prioridade = 88,
                motivo = texto,
                expiraEm = Time.unscaledTime + 180f,
                dedupKey = string.IsNullOrWhiteSpace(dedupKey)
                    ? "ia03_territorio_temporario:" + paisCredorTeamId + ":" + paisPagadorTeamId
                    : dedupKey
            });

            mensagem = criada ? texto : "Já existe uma proposta territorial temporária pendente.";
            return criada;
        }

        public bool TentarProporDesmilitarizacao(
            SistemaGovernoMundial governo,
            int paisProponenteTeamId,
            int outroPaisTeamId,
            float fracaoDeTerritorio,
            int duracaoDias,
            string nomePresidente,
            string motivo,
            string dedupKey,
            out string mensagem)
        {
            mensagem = string.Empty;
            if (!ValidarPartes(governo, paisProponenteTeamId, outroPaisTeamId, out mensagem))
            {
                return false;
            }

            List<string> zona = SelecionarTerritoriosTerrestres(
                governo,
                outroPaisTeamId,
                fracaoDeTerritorio,
                false,
                out mensagem);
            if (zona == null)
            {
                return false;
            }

            int duracao = Mathf.Clamp(duracaoDias, 1, 3650);
            DadosPaisGoverno proponente = governo.ObterPais(paisProponenteTeamId);
            string presidente = ResolverPresidente(governo, proponente, nomePresidente, paisProponenteTeamId);
            string texto = "Presidente " + presidente + " propõe desmilitarizar " + zona.Count
                           + " região(ões) de " + governo.NomePais(outroPaisTeamId) + " por "
                           + duracao + " dia(s) de jogo"
                           + (string.IsNullOrWhiteSpace(motivo) ? "." : ": " + motivo.Trim());
            bool criada = governo.TentarCriarProposta(new PropostaInternacional
            {
                tipo = TipoPropostaInternacional.Desmilitarizacao,
                origemTeamId = paisProponenteTeamId,
                alvoTeamId = outroPaisTeamId,
                quantidade = zona.Count,
                duracaoDias = duracao,
                territoriosDesmilitarizados = zona,
                prioridade = 86,
                motivo = texto,
                expiraEm = Time.unscaledTime + 180f,
                dedupKey = string.IsNullOrWhiteSpace(dedupKey)
                    ? "ia03_desmilitarizacao:" + paisProponenteTeamId + ":" + outroPaisTeamId
                    : dedupKey
            });

            mensagem = criada ? texto : "Já existe uma proposta de desmilitarização pendente.";
            return criada;
        }

        private static bool ValidarPartes(
            SistemaGovernoMundial governo,
            int origemTeamId,
            int alvoTeamId,
            out string mensagem)
        {
            mensagem = string.Empty;
            if (governo == null || origemTeamId <= 0 || alvoTeamId <= 0 || origemTeamId == alvoTeamId)
            {
                mensagem = "Países inválidos para propor um termo territorial.";
                return false;
            }

            if (GerenteDeTerritorio.Instancia == null || GerenteDeTerritorio.Instancia.MapaPolitico == null)
            {
                mensagem = "O mapa político não está disponível para negociar territórios.";
                return false;
            }

            return true;
        }

        private static List<string> SelecionarTerritoriosTerrestres(
            SistemaGovernoMundial governo,
            int proprietarioTeamId,
            float fracao,
            bool exigirCapturavel,
            out string mensagem)
        {
            mensagem = string.Empty;
            GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
            DadosMapaTerritorial mapa = gerente != null ? gerente.MapaPolitico : null;
            List<RegiaoPolitica> candidatos = new List<RegiaoPolitica>();
            IReadOnlyList<RegiaoPolitica> regioes = mapa != null ? mapa.Regioes : null;
            if (regioes != null)
            {
                for (int i = 0; i < regioes.Count; i++)
                {
                    RegiaoPolitica regiao = regioes[i];
                    if (regiao == null || regiao.tipo != TipoRegiaoPolitica.Terra
                        || (exigirCapturavel && !regiao.capturable))
                    {
                        continue;
                    }

                    ResultadoConsultaTerritorio estado = gerente.ObterEstadoDaRegiao(regiao.territorioId);
                    if (estado.ownerCountryTeamId == proprietarioTeamId && !estado.neutral)
                    {
                        candidatos.Add(regiao);
                    }
                }
            }

            if (candidatos.Count == 0)
            {
                mensagem = "O país não possui regiões terrestres adequadas para esse termo.";
                return null;
            }

            candidatos.Sort((a, b) =>
            {
                float pontuacaoA = CalcularPontuacaoTerritorial(mapa, a, proprietarioTeamId);
                float pontuacaoB = CalcularPontuacaoTerritorial(mapa, b, proprietarioTeamId);
                int comparacao = exigirCapturavel
                    ? pontuacaoA.CompareTo(pontuacaoB) // Concessions preserve the owner's most valuable regions.
                    : pontuacaoB.CompareTo(pontuacaoA); // Disarmament prioritizes strategically important areas.
                return comparacao != 0
                    ? comparacao
                    : System.StringComparer.Ordinal.Compare(a.territorioId, b.territorioId);
            });
            int quantidade = Mathf.Clamp(
                Mathf.CeilToInt(candidatos.Count * Mathf.Clamp(fracao, 0.05f, 1f)),
                1,
                candidatos.Count);
            List<string> escolhidos = new List<string>(quantidade);
            for (int i = 0; i < quantidade; i++) escolhidos.Add(candidatos[i].territorioId);
            return escolhidos;
        }

        private static Vector2 Centroide(RegiaoPolitica regiao)
        {
            if (regiao.vertices == null || regiao.vertices.Count == 0) return Vector2.zero;
            Vector2 soma = Vector2.zero;
            for (int i = 0; i < regiao.vertices.Count; i++) soma += regiao.vertices[i];
            return soma / regiao.vertices.Count;
        }

        private static float DistanciaEntreVertices(RegiaoPolitica a, RegiaoPolitica b)
        {
            if (a.vertices == null || b.vertices == null || a.vertices.Count == 0 || b.vertices.Count == 0)
                return float.MaxValue;
            float menor = float.MaxValue;
            for (int i = 0; i < a.vertices.Count; i++)
            {
                for (int j = 0; j < b.vertices.Count; j++)
                {
                    menor = Mathf.Min(menor, Vector2.Distance(a.vertices[i], b.vertices[j]));
                    if (menor <= 0.018f) return menor;
                }
            }
            return menor;
        }

        private static float CalcularArea(RegiaoPolitica regiao)
        {
            if (regiao.vertices == null || regiao.vertices.Count < 3) return 0f;
            float areaDobrada = 0f;
            for (int i = 0; i < regiao.vertices.Count; i++)
            {
                Vector2 atual = regiao.vertices[i];
                Vector2 proximo = regiao.vertices[(i + 1) % regiao.vertices.Count];
                areaDobrada += atual.x * proximo.y - proximo.x * atual.y;
            }
            return Mathf.Abs(areaDobrada) * 0.5f;
        }

        private static float NormalizarValor(float valor)
        {
            return Mathf.Clamp01(valor / 100f);
        }

        private static string ResolverPresidente(
            SistemaGovernoMundial governo,
            DadosPaisGoverno pais,
            string nomePresidente,
            int teamId)
        {
            if (!string.IsNullOrWhiteSpace(nomePresidente)) return nomePresidente.Trim();
            if (pais != null && !string.IsNullOrWhiteSpace(pais.nomePresidente)) return pais.nomePresidente;
            return governo.NomePais(teamId);
        }

        public void RegistrarPresidenteAbatido(
            SistemaGovernoMundial governo,
            int paisVitimaTeamId,
            int agressorTeamId,
            string nomePresidente)
        {
            if (governo == null || paisVitimaTeamId <= 0)
            {
                return;
            }

            DadosPaisGoverno pais = governo.ObterPais(paisVitimaTeamId);
            string presidente = string.IsNullOrWhiteSpace(nomePresidente)
                ? pais != null ? pais.nomePresidente : "Presidente"
                : nomePresidente.Trim();
            governo.RegistrarNoticia("EventoPresidenteAbatido: " + presidente + " foi abatido durante uma operação diplomática.");
            // SistemaDeDanos já registra a agressão quando identifica o ataque.
            // Este adaptador só publica o evento para não aplicar a sanção duas vezes.
        }
    }
}
