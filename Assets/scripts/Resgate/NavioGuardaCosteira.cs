using UnityEngine;

/// <summary>
/// Unidade de grande capacidade. Usa ControleNavioRealista e PierMarinha já
/// existentes; a Guarda Costeira não cria rota nem atracação próprias.
/// </summary>
[RequireComponent(typeof(ControleNavioRealista))]
[RequireComponent(typeof(IdentidadeNaval))]
[RequireComponent(typeof(SistemaDeDanos))]
[RequireComponent(typeof(IdentidadeUnidade))]
[RequireComponent(typeof(CombustivelUnidade))]
[RequireComponent(typeof(ControleUnidade))]
public sealed class NavioGuardaCosteira : GuardaCosteiraUnidade
{
    // Configurado no prefab; a movimentacao continua delegada ao sistema naval existente.
    [Header("Navio")]
    [SerializeField, Min(1)] private int capacidadeNavio = 250;
    [SerializeField, Min(1)] private int tripulacaoNavio = 15;
    [SerializeField, Min(1f)] private float tempoBuscaNavio = 120f;
    [SerializeField, Range(0.05f, 1f)] private float vidaMinimaParaContinuar = 0.50f;
    [SerializeField] private PierMarinha pierBase;

    private ControleNavioRealista controle;
    private IdentidadeNaval identidadeNaval;
    private PierMarinha.VagaDeAtracagem vagaRetorno;

    public int CapacidadeNavio => Mathf.Max(1, capacidadeNavio);

    protected override void Awake()
    {
        capacidadeResgate = 250;
        tripulacao = 15;
        tempoBuscaSegundos = 120f;
        areaBusca = 50f;
        base.Awake();
        controle = GetComponent<ControleNavioRealista>() ?? GetComponentInParent<ControleNavioRealista>();
        identidadeNaval = GetComponent<IdentidadeNaval>() ?? GetComponentInParent<IdentidadeNaval>();
        capacidadeResgate = Mathf.Max(1, capacidadeNavio);
        tripulacao = Mathf.Max(1, tripulacaoNavio);
        tempoBuscaSegundos = Mathf.Max(1f, tempoBuscaNavio);
        // A unidade de resgate permanece armada somente para autoprotecao por
        // canhao. Se o prefab reutilizado trouxer torpedos/misseis militares,
        // eles ficam desativados neste componente, sem alterar o sistema global.
        if (controle != null)
        {
            controle.possuiTorpedos = false;
            controle.torpedosDisponiveis = 0;
        }
        LancadorNaval[] lancadores = GetComponentsInChildren<LancadorNaval>(true);
        for (int i = 0; i < lancadores.Length; i++)
        {
            if (lancadores[i] != null) lancadores[i].enabled = false;
        }
        if (pierBase == null) pierBase = EncontrarPier();
    }

    protected override bool PodeOperarBase()
    {
        return controle != null && identidadeNaval != null && pierBase != null;
    }

    protected override bool NavegarAte(Vector3 destino)
    {
        if (controle == null || !CombustivelUnidade.PodeOperarObjeto(gameObject)) return false;
        if (!NavalPlacementResolver.IsWaterAtPosition(destino)
            && NavalPlacementResolver.TryResolveWaterSpawn(destino, transform.forward, 0f, 250f, out Vector3 agua, out _, out _))
            destino = agua;
        return controle.DefinirDestino(destino);
    }

    protected override void RetornarParaBase()
    {
        if (!PodeOperarBase()) return;
        if (vagaRetorno == null || !vagaRetorno.EstaLivre())
            vagaRetorno = EncontrarVagaLivre(pierBase);
        if (vagaRetorno != null) pierBase.AtribuirVaga(vagaRetorno, identidadeNaval);
        else GetComponent<ControleUnidade>()?.EmitirOrdemRetornarAoPontoInicial();
    }

    protected override bool EstaNaBase()
    {
        return vagaRetorno != null
            && vagaRetorno.atracagemCompleta
            && Vector3.Distance(transform.position, vagaRetorno.pontoDeAtracagem.position) <= 4f;
    }

    protected override bool DeveAbortarPorDano(SistemaDeDanos sistema)
    {
        return sistema != null && sistema.vidaMaxima > 0f && sistema.vidaAtual / sistema.vidaMaxima <= vidaMinimaParaContinuar;
    }

    private PierMarinha EncontrarPier()
    {
        PierMarinha[] piers = FindObjectsByType<PierMarinha>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PierMarinha melhor = null;
        float distancia = float.MaxValue;
        for (int i = 0; i < piers.Length; i++)
        {
            PierMarinha pier = piers[i];
            if (pier == null || (pier.OwnerTeamId > 0 && pier.OwnerTeamId != TeamId)) continue;
            float atual = (pier.transform.position - transform.position).sqrMagnitude;
            if (atual < distancia) { distancia = atual; melhor = pier; }
        }
        return melhor;
    }

    private static PierMarinha.VagaDeAtracagem EncontrarVagaLivre(PierMarinha pier)
    {
        if (pier == null || pier.vagasDisponiveis == null) return null;
        for (int i = 0; i < pier.vagasDisponiveis.Count; i++)
        {
            PierMarinha.VagaDeAtracagem vaga = pier.vagasDisponiveis[i];
            if (vaga != null && vaga.pontoDeAtracagem != null && vaga.EstaLivre()) return vaga;
        }
        return null;
    }
}
