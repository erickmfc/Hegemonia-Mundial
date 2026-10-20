using System.Collections;
using UnityEngine;

/// <summary>
/// Cenário descartável de demonstração do convés: posiciona dois F-16 nas
/// primeiras vagas e solicita as decolagens pelo fluxo V2 do porta-aviões.
/// Não é anexado a navios de jogo nem incluído nas cenas da campanha.
/// </summary>
public sealed class EnterpriseFlightDeckPreview : MonoBehaviour
{
    [SerializeField] private GerenciadorOperacoesPortaAvioesV2 gerenciador;
    [SerializeField] private GameObject prefabAeronave;
    [SerializeField] private float atrasoInicial = 1f;

    private IEnumerator Start()
    {
        if (gerenciador == null || prefabAeronave == null)
        {
            Debug.LogError("Preview do Enterprise sem gerente ou prefab de aeronave.", this);
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, atrasoInicial));
        LayoutConvesPortaAvioesV2 layout = gerenciador.layout;
        if (layout == null || layout.vagasConves == null || layout.vagasConves.Count < 2)
        {
            Debug.LogError("Preview do Enterprise precisa de duas vagas de convés.", this);
            yield break;
        }

        Transform destinoMarcado = layout.voo != null ? layout.voo.Find("Ponto_Missao") : null;
        Vector3 destino = destinoMarcado != null
            ? destinoMarcado.position
            : gerenciador.transform.position + gerenciador.transform.forward * 1200f + Vector3.up * 250f;

        for (int i = 0; i < 2; i++)
        {
            VagaPortaAvioesV2 vaga = layout.vagasConves[i];
            if (vaga == null) continue;
            GameObject objeto = Instantiate(prefabAeronave, vaga.transform.position, vaga.transform.rotation, vaga.transform);
            objeto.name = i == 0 ? "Preview_Aeronave_A" : "Preview_Aeronave_B";
            objeto.transform.localPosition = Vector3.zero;
            objeto.transform.localRotation = Quaternion.identity;
            ControleAviao controle = objeto.GetComponent<ControleAviao>();
            if (controle == null)
            {
                Debug.LogError("O F-16 de preview não possui ControleAviao.", objeto);
                continue;
            }

            gerenciador.PrepararAeronaveParaMenu(controle, false);
            bool solicitado = gerenciador.TrySolicitarDecolagem(controle, destino);
            Debug.Log(solicitado
                ? $"Enterprise: aeronave {(i == 0 ? "A" : "B")} autorizada; pista/cadência geridas pelo V2."
                : $"Enterprise: aeronave {(i == 0 ? "A" : "B")} não pôde iniciar o teste.", objeto);
            if (i == 0) yield return null;
        }
    }
}
