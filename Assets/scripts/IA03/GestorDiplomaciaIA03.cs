using UnityEngine;

namespace Hegemonia.AI.IA03
{
    /// <summary>
    /// Adaptador estreito para o registro diplomático e as propostas já mantidas
    /// por SistemaGovernoMundial. Não mantém uma segunda cópia das relações.
    /// </summary>
    public sealed class GestorDiplomaciaIA03
    {
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
