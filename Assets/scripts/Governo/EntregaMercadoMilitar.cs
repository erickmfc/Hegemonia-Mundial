using System;
using System.Collections;
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

        for (int i = 0; i < Mathf.Max(1, quantidade); i++)
        {
            GameObject unidade = UnityEngine.Object.Instantiate(prefab, origem.position + Vector3.up * (1.5f + i * 0.35f), origem.rotation);
            IdentidadeUnidade id = unidade.GetComponent<IdentidadeUnidade>() ?? unidade.AddComponent<IdentidadeUnidade>();
            id.teamID = compradorTeamId;
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
        if (string.Equals(tipoEntrega, "aeronave", StringComparison.OrdinalIgnoreCase)
            && aeroportoDestino != null)
        {
            C700TransporteAereo c700 = GetComponentInChildren<C700TransporteAereo>(true);
            if (c700 != null)
            {
                aeroportoDestino.RegistrarTransporteAereoRecebido(c700);
                return;
            }

            Helicoptero helicoptero = GetComponentInChildren<Helicoptero>(true);
            if (helicoptero != null)
            {
                aeroportoDestino.RegistrarHelicopteroRecebido(helicoptero);
                return;
            }

            ControleAviao aviao = GetComponentInChildren<ControleAviao>(true);
            if (aviao != null)
            {
                aeroportoDestino.RegistrarAeronaveRecebida(aviao);
                return;
            }
        }

        if (string.Equals(tipoEntrega, "navio", StringComparison.OrdinalIgnoreCase)
            && estaleiroDestino != null)
        {
            estaleiroDestino.RegistrarNavioEntregue(gameObject, compradorTeamId);
        }
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
