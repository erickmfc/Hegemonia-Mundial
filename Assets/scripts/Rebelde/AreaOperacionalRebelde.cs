using System.Collections.Generic;
using UnityEngine;

public enum TipoAreaOperacionalRebelde
{
    Aerea,
    Naval,
    Terrestre
}

/// <summary>Zona local de operação. O volume e os pontos são definidos no Inspector.</summary>
[DisallowMultipleComponent]
public sealed class AreaOperacionalRebelde : MonoBehaviour
{
    [Header("Identificação")]
    [SerializeField] private string identificacao = "AreaPatrulhaAerea";
    [SerializeField] private TipoAreaOperacionalRebelde tipo = TipoAreaOperacionalRebelde.Aerea;
    [Header("Limites e vínculo")]
    [SerializeField] private Collider limiteDaArea;
    [SerializeField] private GameObject baseResponsavel;
    [SerializeField] private Transform pontoRetornoBase;
    [SerializeField] private RebeldeControlador controlador;
    [Header("Operação")]
    [SerializeField] private List<Transform> pontosCriacaoAutorizados = new List<Transform>();
    [SerializeField] private List<Transform> rotaPatrulha = new List<Transform>();
    [SerializeField] private List<GameObject> unidadesVinculadas = new List<GameObject>();
    [SerializeField, Min(0f)] private float tempoEntrePatrulhas = 30f;
    [SerializeField, Min(0)] private int quantidadeMaximaUnidades = 2;
    [Header("Visualização")]
    [SerializeField] private Color corDaArea = new Color(0.9f, 0.2f, 0.15f, 0.25f);

    public string Identificacao => identificacao;
    public TipoAreaOperacionalRebelde Tipo => tipo;
    public Collider LimiteDaArea => limiteDaArea;
    public GameObject BaseResponsavel => baseResponsavel;
    public Transform PontoRetornoBase => pontoRetornoBase;
    public IReadOnlyList<Transform> PontosCriacaoAutorizados => pontosCriacaoAutorizados;
    public IReadOnlyList<Transform> RotaPatrulha => rotaPatrulha;
    public IReadOnlyList<GameObject> UnidadesVinculadas => unidadesVinculadas;
    public float TempoEntrePatrulhas => tempoEntrePatrulhas;
    public int QuantidadeMaximaUnidades => quantidadeMaximaUnidades;

    public bool RegistrarUnidade(GameObject unidade)
    {
        if (unidade == null || !Contem(unidade.transform.position)) return false;
        if (!unidadesVinculadas.Contains(unidade)) unidadesVinculadas.Add(unidade);
        return true;
    }

    public bool Contem(Vector3 posicao)
    {
        if (limiteDaArea == null || !limiteDaArea.enabled) return false;
        return limiteDaArea.bounds.Contains(posicao);
    }

    public bool RotaValida()
    {
        if (limiteDaArea == null || rotaPatrulha == null || rotaPatrulha.Count == 0) return false;
        for (int i = 0; i < rotaPatrulha.Count; i++)
            if (rotaPatrulha[i] == null || !Contem(rotaPatrulha[i].position)) return false;
        return true;
    }

    public Transform ObterPontoCriacaoValido()
    {
        for (int i = 0; i < pontosCriacaoAutorizados.Count; i++)
        {
            Transform ponto = pontosCriacaoAutorizados[i];
            if (ponto != null && Contem(ponto.position)) return ponto;
        }
        return null;
    }

    private void OnTriggerEnter(Collider contato)
    {
        if (controlador == null || contato == null) return;
        IdentidadeUnidade identidade = contato.GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null) controlador.RegistrarContato(this, identidade);
    }

    private void OnTriggerExit(Collider contato)
    {
        if (controlador == null || contato == null) return;
        IdentidadeUnidade identidade = contato.GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null) controlador.RemoverContato(identidade);
    }

    private void OnDrawGizmos()
    {
        if (limiteDaArea != null)
        {
            Gizmos.color = corDaArea;
            Gizmos.DrawCube(limiteDaArea.bounds.center, limiteDaArea.bounds.size);
            Gizmos.color = new Color(corDaArea.r, corDaArea.g, corDaArea.b, 1f);
            Gizmos.DrawWireCube(limiteDaArea.bounds.center, limiteDaArea.bounds.size);
        }
        if (rotaPatrulha == null) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < rotaPatrulha.Count; i++)
        {
            Transform ponto = rotaPatrulha[i];
            if (ponto == null) continue;
            Gizmos.DrawSphere(ponto.position, 12f);
            if (i + 1 < rotaPatrulha.Count && rotaPatrulha[i + 1] != null)
                Gizmos.DrawLine(ponto.position, rotaPatrulha[i + 1].position);
        }
        Gizmos.color = Color.green;
        for (int i = 0; i < pontosCriacaoAutorizados.Count; i++)
            if (pontosCriacaoAutorizados[i] != null) Gizmos.DrawCube(pontosCriacaoAutorizados[i].position, Vector3.one * 18f);
        if (pontoRetornoBase != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pontoRetornoBase.position, 30f);
        }
    }
}
