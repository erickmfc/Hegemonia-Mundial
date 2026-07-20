using System;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>Entrega visual de equipamentos comprados no mercado global.</summary>
public sealed class EntregaMercadoMilitar : MonoBehaviour
{
    private Vector3 destino;
    private float velocidade;

    public static bool Enviar(DadosItemMercado item, int vendedorTeamId, int compradorTeamId, int quantidade, out string mensagem)
    {
        mensagem = string.Empty;
        if (item == null || string.IsNullOrWhiteSpace(item.prefabId)) { mensagem = "Equipamento sem ficha de entrega."; return false; }
        DadosConstrucao ficha = EncontrarFicha(item.prefabId);
        if (ficha == null || !ficha.TryGetPrefabBasico(out GameObject prefab)) { mensagem = "Prefab do equipamento nao encontrado."; return false; }

        Transform origem = EncontrarPonto(item.tipoEntrega, vendedorTeamId);
        Transform destino = EncontrarPonto(item.tipoEntrega, compradorTeamId);
        if (origem == null || destino == null) { mensagem = "Origem ou destino logistico indisponivel."; return false; }

        for (int i = 0; i < Mathf.Max(1, quantidade); i++)
        {
            GameObject unidade = UnityEngine.Object.Instantiate(prefab, origem.position + Vector3.up * (1.5f + i * 0.35f), origem.rotation);
            IdentidadeUnidade id = unidade.GetComponent<IdentidadeUnidade>() ?? unidade.AddComponent<IdentidadeUnidade>();
            id.teamID = compradorTeamId;
            EntregaMercadoMilitar entrega = unidade.AddComponent<EntregaMercadoMilitar>();
            entrega.destino = destino.position + Vector3.right * (i * 1.5f);
            entrega.velocidade = item.tipoEntrega == "navio" ? 18f : 28f;
            entrega.StartCoroutine(entrega.IrAteDestino());
        }
        mensagem = "Entrega militar enviada para " + item.tipoEntrega + ".";
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
        Destroy(this);
    }

    private static DadosConstrucao EncontrarFicha(string id)
    {
        if (MenuConstrucao.catalogoGlobal != null)
            return MenuConstrucao.catalogoGlobal.FirstOrDefault(f => f != null && string.Equals(f.GetStableId(), id, StringComparison.OrdinalIgnoreCase));
        return Resources.LoadAll<DadosConstrucao>(string.Empty).FirstOrDefault(f => f != null && string.Equals(f.GetStableId(), id, StringComparison.OrdinalIgnoreCase));
    }

    private static Transform EncontrarPonto(string tipo, int teamId)
    {
        if (tipo == "aeronave")
            return UnityEngine.Object.FindObjectsOfType<GerenciadorAeroporto>().Select(a => new { a, id = a.GetComponentInParent<IdentidadeUnidade>() }).Where(x => x.id != null && x.id.teamID == teamId).Select(x => x.a.decolagem != null ? x.a.decolagem : x.a.transform).FirstOrDefault();
        if (tipo == "navio")
            return UnityEngine.Object.FindObjectsOfType<Estaleiro>().Where(e => e.OwnerTeamId == teamId).Select(e => e.pontoDeSaida != null ? e.pontoDeSaida : e.transform).FirstOrDefault();
        return UnityEngine.Object.FindObjectsOfType<GerenciadorQuartel>().Select(q => new { q, id = q.GetComponentInParent<IdentidadeUnidade>() }).Where(x => x.id != null && x.id.teamID == teamId).Select(x => x.q.transform).FirstOrDefault();
    }
}
