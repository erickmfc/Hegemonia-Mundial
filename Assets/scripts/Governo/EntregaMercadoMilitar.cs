using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Entrega visual de equipamentos comprados no mercado global.</summary>
public sealed class EntregaMercadoMilitar : MonoBehaviour
{
    private Vector3 destino;
    private float velocidade;
    private string tipoEntrega;
    private int compradorTeamId;
    private GerenciadorAeroporto aeroportoDestino;
    private Estaleiro estaleiroDestino;

    public static bool Enviar(DadosItemMercado item, int vendedorTeamId, int compradorTeamId, int quantidade, out string mensagem)
    {
        mensagem = string.Empty;
        if (item == null || string.IsNullOrWhiteSpace(item.prefabId)) { mensagem = "Equipamento sem ficha de entrega."; return false; }
        DadosConstrucao ficha = EncontrarFicha(item.prefabId);
        if (ficha == null || !ficha.TryGetPrefabBasico(out GameObject prefab)) { mensagem = "Prefab do equipamento nao encontrado."; return false; }

        string tipoEntrega = (item.tipoEntrega ?? string.Empty).ToLowerInvariant();
        GerenciadorAeroporto aeroportoDestino = tipoEntrega == "aeronave"
            ? EncontrarAeroporto(compradorTeamId)
            : null;
        Estaleiro estaleiroDestino = tipoEntrega == "navio"
            ? EncontrarEstaleiro(compradorTeamId)
            : null;

        Transform origem = EncontrarPonto(tipoEntrega, vendedorTeamId);
        Transform destino = EncontrarPonto(tipoEntrega, compradorTeamId);
        if (origem == null || destino == null) { mensagem = "Origem ou destino logistico indisponivel."; return false; }

        int lote = Mathf.Max(1, quantidade);
        TipoUnidade tipoUnidadeEntrega = tipoEntrega == "aeronave"
            ? TipoUnidade.Aereo
            : tipoEntrega == "navio" ? TipoUnidade.Naval : TipoUnidade.Veiculo;
        int efetivoNecessario = SistemaGovernoMundial.ObterEfetivoNecessario(prefab, tipoUnidadeEntrega);
        SistemaGovernoMundial governoEfetivo = efetivoNecessario > 0 ? SistemaGovernoMundial.Instancia : null;
        if (efetivoNecessario > 0
            && (governoEfetivo == null
                || !governoEfetivo.PodeProduzirUnidadeComEfetivo(compradorTeamId, prefab, lote, tipoUnidadeEntrega)))
        {
            mensagem = "Efetivo nacional insuficiente para receber o equipamento militar.";
            return false;
        }

        List<GameObject> unidadesCriadas = new List<GameObject>(lote);
        for (int i = 0; i < lote; i++)
        {
            GameObject unidade = UnityEngine.Object.Instantiate(prefab, origem.position + Vector3.up * (1.5f + i * 0.35f), origem.rotation);
            unidadesCriadas.Add(unidade);
            IdentidadeUnidade id = unidade.GetComponent<IdentidadeUnidade>() ?? unidade.AddComponent<IdentidadeUnidade>();
            id.teamID = compradorTeamId;
            id.tipoUnidade = tipoUnidadeEntrega;
            governoEfetivo = governoEfetivo != null ? governoEfetivo : SistemaGovernoMundial.Instancia;
            if (SistemaGovernoMundial.ObterEfetivoNecessario(unidade, tipoUnidadeEntrega) > 0
                && (governoEfetivo == null || !governoEfetivo.TentarMobilizarEfetivoDaUnidade(compradorTeamId, unidade)))
            {
                ReverterUnidadesCriadas(unidadesCriadas, compradorTeamId, governoEfetivo);
                mensagem = "Efetivo nacional indisponivel durante a entrega.";
                return false;
            }
            ServicoAbastecimento.AbastecerUnidadeProduzida(unidade);
            EntregaMercadoMilitar entrega = unidade.AddComponent<EntregaMercadoMilitar>();
            entrega.destino = destino.position + Vector3.right * (i * 1.5f);
            entrega.velocidade = tipoEntrega == "navio" ? 18f : 28f;
            entrega.tipoEntrega = tipoEntrega;
            entrega.compradorTeamId = compradorTeamId;
            entrega.aeroportoDestino = aeroportoDestino;
            entrega.estaleiroDestino = estaleiroDestino;
            entrega.StartCoroutine(entrega.IrAteDestino());
        }
        mensagem = "Entrega militar enviada para " + tipoEntrega + ".";
        return true;
    }

    private static void ReverterUnidadesCriadas(List<GameObject> unidades, int teamId, SistemaGovernoMundial governo)
    {
        if (unidades == null) return;
        for (int i = 0; i < unidades.Count; i++)
        {
            GameObject unidade = unidades[i];
            if (unidade == null) continue;
            governo?.LiberarEfetivoMobilizadoSemBaixa(teamId, unidade);
            UnityEngine.Object.Destroy(unidade);
        }
        unidades.Clear();
    }

    private IEnumerator IrAteDestino()
    {
        while (this != null && Vector3.Distance(transform.position, destino) > 2f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destino, velocidade * Time.deltaTime);
            Vector3 direcao = destino - transform.position;
            if (direcao.sqrMagnitude > 0.1f) transform.forward = Vector3.Lerp(transform.forward, direcao.normalized, 0.12f);
            yield return null;
        }
        transform.position = destino;
        IntegrarNoDestino();
        Destroy(this);
    }

    private void IntegrarNoDestino()
    {
        if (string.Equals(tipoEntrega, "aeronave", StringComparison.OrdinalIgnoreCase))
        {
            if (aeroportoDestino == null)
            {
                ReverterEntregaRecusada();
                return;
            }

            C700TransporteAereo c700 = GetComponentInChildren<C700TransporteAereo>(true);
            if (c700 != null)
            {
                if (!aeroportoDestino.RegistrarTransporteAereoRecebido(c700))
                    ReverterEntregaRecusada();
                return;
            }

            Helicoptero helicoptero = GetComponentInChildren<Helicoptero>(true);
            if (helicoptero != null)
            {
                if (!aeroportoDestino.RegistrarHelicopteroRecebido(helicoptero))
                    ReverterEntregaRecusada();
                return;
            }

            ControleAviao aviao = GetComponentInChildren<ControleAviao>(true);
            if (aviao != null)
            {
                if (!aeroportoDestino.RegistrarAeronaveRecebida(aviao))
                    ReverterEntregaRecusada();
                return;
            }
        }

        if (string.Equals(tipoEntrega, "navio", StringComparison.OrdinalIgnoreCase))
        {
            if (estaleiroDestino == null)
            {
                ReverterEntregaRecusada();
                return;
            }

            if (!estaleiroDestino.RegistrarNavioEntregue(gameObject, compradorTeamId))
                ReverterEntregaRecusada();
        }
    }

    private void ReverterEntregaRecusada()
    {
        SistemaGovernoMundial.Instancia?.LiberarEfetivoMobilizadoSemBaixa(compradorTeamId, gameObject);
        Destroy(gameObject);
    }

    private static DadosConstrucao EncontrarFicha(string id)
    {
        if (MenuConstrucao.catalogoGlobal != null)
            return MenuConstrucao.catalogoGlobal.FirstOrDefault(f => f != null && string.Equals(f.GetStableId(), id, StringComparison.OrdinalIgnoreCase));
        return null;
    }

    private static Transform EncontrarPonto(string tipo, int teamId)
    {
        tipo = (tipo ?? string.Empty).ToLowerInvariant();
        if (tipo == "aeronave")
        {
            GerenciadorAeroporto aeroporto = EncontrarAeroporto(teamId);
            return aeroporto != null && aeroporto.decolagem != null ? aeroporto.decolagem : aeroporto != null ? aeroporto.transform : null;
        }
        if (tipo == "navio")
        {
            Estaleiro estaleiro = EncontrarEstaleiro(teamId);
            return estaleiro != null && estaleiro.pontoDeSaida != null ? estaleiro.pontoDeSaida : estaleiro != null ? estaleiro.transform : null;
        }
        return UnityEngine.Object.FindObjectsByType<GerenciadorQuartel>(FindObjectsSortMode.None).Select(q => new { q, id = q.GetComponentInParent<IdentidadeUnidade>() }).Where(x => x.id != null && x.id.teamID == teamId).Select(x => x.q.transform).FirstOrDefault();
    }

    private static GerenciadorAeroporto EncontrarAeroporto(int teamId)
    {
        return UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None)
            .Select(a => new { a, id = a.GetComponentInParent<IdentidadeUnidade>() })
            .Where(x => x.id != null && x.id.teamID == teamId)
            .Select(x => x.a)
            .FirstOrDefault();
    }

    private static Estaleiro EncontrarEstaleiro(int teamId)
    {
        return UnityEngine.Object.FindObjectsByType<Estaleiro>(FindObjectsSortMode.None)
            .Where(e => e.OwnerTeamId == teamId)
            .FirstOrDefault();
    }
}
