using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Navio civil usado pelo mercado internacional. A movimentacao e direta sobre
/// a agua para nao depender do NavMesh terrestre e para manter o custo baixo.
/// O sistema de mercado controla as pernas da viagem; este componente controla
/// apenas a locomocao visual e o estado do casco.
/// </summary>
public sealed class NavioCargaMercado : MonoBehaviour
{
    public const float CapacidadePadrao = 5000f;

    [Header("Identidade")]
    [SerializeField] private int ownerTeamId;
    [SerializeField] private bool fretado;

    [Header("Operacao")]
    public float capacidadeCarga = CapacidadePadrao;
    public float velocidadeCruzeiro = 12f;
    public float distanciaChegada = 4f;
    [Tooltip("Deslocamento vertical visual em relacao ao nivel do mar. Valores negativos afundam o casco.")]
    public float offsetAlturaAgua = -1.5f;

    private bool emViagem;
    private Vector3 destino;
    private Action aoChegar;
    private float nivelAgua;
    private bool inicializado;
    private float proximaSincronizacaoIdentidade;

    public int OwnerTeamId => ownerTeamId;
    public bool Fretado => fretado;
    public bool EmViagem => emViagem;
    public bool Disponivel => isActiveAndEnabled && !emViagem && !fretado;
    public Vector3 DestinoAtual => destino;

    private void Awake()
    {
        // O cargueiro usa locomocao aquatica direta. Deixar o NavMeshAgent do
        // prefab ativo faz o Unity tentar criar um agente sobre a agua e pode
        // impedir a viagem ou produzir avisos repetidos na build.
        NavMeshAgent agente = GetComponent<NavMeshAgent>();
        if (agente != null) agente.enabled = false;
    }

    private void OnEnable()
    {
        SistemaLogisticaMercado.Instancia?.RegistrarNavio(this);
    }

    private void Start()
    {
        if (!inicializado)
        {
            IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>()
                ?? GetComponentInParent<IdentidadeUnidade>()
                ?? GetComponentInChildren<IdentidadeUnidade>(true);
            if (identidade != null) ownerTeamId = Mathf.Max(0, identidade.teamID);
        }

        SistemaLogisticaMercado.Instancia?.RegistrarNavio(this);
    }

    private void OnDisable()
    {
        SistemaLogisticaMercado.Instancia?.DesregistrarNavio(this);
    }

    private void Update()
    {
        if (!fretado && ownerTeamId <= 0 && Time.unscaledTime >= proximaSincronizacaoIdentidade)
        {
            proximaSincronizacaoIdentidade = Time.unscaledTime + 1f;
            SincronizarEquipeDaIdentidade();
        }

        if (!emViagem) return;

        Vector3 atual = transform.position;
        Vector3 alvo = destino;
        alvo.y = nivelAgua + offsetAlturaAgua;
        Vector3 delta = alvo - atual;
        delta.y = 0f;

        if (delta.sqrMagnitude <= distanciaChegada * distanciaChegada)
        {
            transform.position = new Vector3(alvo.x, nivelAgua + offsetAlturaAgua, alvo.z);
            emViagem = false;
            Action callback = aoChegar;
            aoChegar = null;
            callback?.Invoke();
            return;
        }

        Vector3 direcao = delta.normalized;
        transform.position = Vector3.MoveTowards(atual, new Vector3(alvo.x, nivelAgua + offsetAlturaAgua, alvo.z), velocidadeCruzeiro * Time.deltaTime);
        if (direcao.sqrMagnitude > 0.001f)
        {
            Quaternion rotacao = Quaternion.LookRotation(direcao, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacao, 35f * Time.deltaTime);
        }
    }

    public void SincronizarEquipeDaIdentidade()
    {
        if (fretado || ownerTeamId > 0) return;
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>()
            ?? GetComponentInParent<IdentidadeUnidade>()
            ?? GetComponentInChildren<IdentidadeUnidade>(true);
        if (identidade != null && identidade.teamID > 0) ownerTeamId = identidade.teamID;
    }

    public void Inicializar(int teamId, bool isFretado)
    {
        ownerTeamId = Mathf.Max(0, teamId);
        fretado = isFretado;
        inicializado = true;

        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = gameObject.AddComponent<IdentidadeUnidade>();
        identidade.teamID = ownerTeamId;
        identidade.tipoUnidade = TipoUnidade.Naval;

        IdentidadeNaval identidadeNaval = GetComponent<IdentidadeNaval>();
        if (identidadeNaval == null) identidadeNaval = gameObject.AddComponent<IdentidadeNaval>();
        identidadeNaval.nomeDoNavio = fretado ? "Frete Maritimo" : "Navio de Carga";
        identidadeNaval.categoriaNavio = IdentidadeNaval.CategoriaNavio.TransporteGrande;

        nivelAgua = NavalPlacementResolver.ResolveSeaLevel();
        Vector3 posicao = transform.position;
        transform.position = new Vector3(posicao.x, nivelAgua + offsetAlturaAgua, posicao.z);
        SistemaLogisticaMercado.Instancia?.RegistrarNavio(this);
    }

    public bool Despachar(Vector3 novoDestino, Action chegada)
    {
        if (!isActiveAndEnabled) return false;
        destino = novoDestino;
        nivelAgua = NavalPlacementResolver.ResolveSeaLevel();
        emViagem = true;
        aoChegar = chegada;
        return true;
    }

    public void PararNoPonto(Vector3 ponto)
    {
        emViagem = false;
        aoChegar = null;
        nivelAgua = NavalPlacementResolver.ResolveSeaLevel();
        transform.position = new Vector3(ponto.x, nivelAgua + offsetAlturaAgua, ponto.z);
    }
}
