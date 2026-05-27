using UnityEngine;

public class ProxiResgateSobrevivente : MonoBehaviour
{
    public int teamId = 1;
    public float tempoRestante = 60f;
    public float raioResgate = 15f;

    private void Start()
    {
        // Add a visual capsule representing the survivor so they are visible in-game.
        if (GetComponentInChildren<Renderer>() == null)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.up;
            visual.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            Destroy(visual.GetComponent<Collider>()); // remove primitive collider
        }
    }

    private void Update()
    {
        tempoRestante -= Time.deltaTime;
        if (tempoRestante <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, raioResgate);
        foreach (var col in colliders)
        {
            if (col == null) continue;
            var id = col.GetComponent<IdentidadeUnidade>();
            if (id == null) id = col.GetComponentInParent<IdentidadeUnidade>();

            if (id != null && id.teamID == teamId && id.tipoUnidade != TipoUnidade.Estrutura)
            {
                Resgatar();
                break;
            }
        }
    }

    private void Resgatar()
    {
        DadosPaisGoverno pais = ConectorGoverno.ObterPais(teamId);
        if (pais == null && SistemaGovernoMundial.Instancia != null)
        {
            pais = SistemaGovernoMundial.Instancia.ObterPais(teamId);
        }

        if (pais != null)
        {
            pais.alistaveis += 1;
            SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
            Debug.Log($"[Resgate] Sobrevivente do time {teamId} resgatado e retornado ao pool!");
        }

        if (GerenciadorFXGlobal.Instancia != null)
        {
            GerenciadorFXGlobal.Instancia.TocarEfeito("FumacaLeve", transform.position, 1.5f);
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, raioResgate);
    }
}
