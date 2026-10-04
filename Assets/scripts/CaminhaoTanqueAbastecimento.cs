using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CombustivelUnidade))]
public class CaminhaoTanqueAbastecimento : MonoBehaviour
{
    public enum ModoOperacaoAbastecimento
    {
        Manual,
        Automatico
    }

    private enum EstadoOperacao
    {
        Parado,
        IndoAoAlvo,
        Abastecendo
    }

    private struct AlvoRadarInfo
    {
        public IdentidadeUnidade identidade;
        public CombustivelUnidade combustivel;
        public float distancia;
    }

    [Header("Carga")]
    public float capacidadeCarga = 600f;
    public float cargaAtual = 0f;

    [Header("Abastecimento")]
    public float raioAbastecimento = 30f;
    public float taxaTransferencia = 30f;
    public float intervaloBusca = 1.5f;
    public LayerMask camadasUnidades = ~0;
    public bool abastecerAutomaticamente = false;
    public ModoOperacaoAbastecimento modoOperacao = ModoOperacaoAbastecimento.Manual;
    [Min(1f)] public float raioRadar = 2599f;
    [Range(0.05f, 0.95f)] public float limiteCombustivelAutomatico = 0.60f;
    public KeyCode teclaRecarregarCarga = KeyCode.R;

    [Header("Indicador")]
    public bool mostrarIndicadorSelecionado = true;
    public Vector3 offsetIndicador = new Vector3(0f, 4.1f, 0f);

    private readonly List<IdentidadeUnidade> unidadesRegistradas = new List<IdentidadeUnidade>(128);
    private readonly List<AlvoRadarInfo> alvosRadar = new List<AlvoRadarInfo>(64);
    private ControleUnidade controle;
    private IdentidadeUnidade identidade;
    private bool identidadeResolvida;
    private CombustivelUnidade combustivelProprio;
    private UnityEngine.AI.NavMeshAgent agente;
    private Camera cameraCache;
    private EstadoOperacao estadoOperacao;
    private IdentidadeUnidade identidadeAlvoAtual;
    private CombustivelUnidade combustivelAlvoAtual;
    private float proximaBusca;
    private float proximaRevisaoRota;
    private float proximaAtualizacaoTexto;
    private float recargaManualAcumulada;
    private string idOrdemMovimento;
    private string mensagemStatus = string.Empty;
    private float mensagemStatusAte;
    private string textoCache = string.Empty;
    private Vector2 scrollRadar;
    private int sequenciaOrdem;

    public float CargaAtual => Mathf.Max(0f, cargaAtual);
    public float CapacidadeCarga => Mathf.Max(0f, capacidadeCarga);
    public float EspacoCarga => Mathf.Max(0f, CapacidadeCarga - CargaAtual);
    public float PercentualCarga => CapacidadeCarga > 0f ? Mathf.Clamp01(CargaAtual / CapacidadeCarga) : 0f;
    public bool ModoAutomaticoAtivo => modoOperacao == ModoOperacaoAbastecimento.Automatico;
    public string EstadoOperacaoAtual => estadoOperacao.ToString();

    private void Awake()
    {
        ResolverReferencias();

        // Prefabs e saves antigos só têm o bool legado; mantém o modo salvo.
        if (abastecerAutomaticamente)
        {
            modoOperacao = ModoOperacaoAbastecimento.Automatico;
        }
        abastecerAutomaticamente = ModoAutomaticoAtivo;

        if (combustivelProprio != null)
        {
            combustivelProprio.classe = ClasseCombustivelUnidade.Terrestre;
            combustivelProprio.ConfigurarSeNecessario(true);
        }

        cargaAtual = Mathf.Clamp(cargaAtual, 0f, capacidadeCarga);
    }

    private void OnDisable()
    {
        CancelarOrdemDeMovimento();
    }

    private void Update()
    {
        ResolverReferencias();
        if (Time.time >= proximaBusca)
        {
            proximaBusca = Time.time + Mathf.Max(0.25f, intervaloBusca);
            AtualizarRadar();

            if (ModoAutomaticoAtivo && estadoOperacao == EstadoOperacao.Parado && CargaAtual > 0.01f)
            {
                TentarEscolherAlvoAutomatico();
            }
        }

        if (EstaSelecionado() && Input.GetKey(teclaRecarregarCarga))
        {
            recargaManualAcumulada = Mathf.Min(
                EspacoCarga,
                recargaManualAcumulada + Mathf.Max(0f, taxaTransferencia) * Time.deltaTime);
            if (recargaManualAcumulada >= 1f || EspacoCarga <= recargaManualAcumulada)
            {
                ServicoAbastecimento.TentarCarregarCaminhao(this, recargaManualAcumulada, out float carregado);
                recargaManualAcumulada = Mathf.Max(0f, recargaManualAcumulada - carregado);
            }
        }
        else
        {
            recargaManualAcumulada = 0f;
        }

        if (estadoOperacao == EstadoOperacao.IndoAoAlvo || estadoOperacao == EstadoOperacao.Abastecendo)
        {
            AtualizarOperacao();
            return;
        }

    }

    public string AlternarModoOperacao()
    {
        modoOperacao = ModoAutomaticoAtivo
            ? ModoOperacaoAbastecimento.Manual
            : ModoOperacaoAbastecimento.Automatico;
        abastecerAutomaticamente = ModoAutomaticoAtivo;

        if (!ModoAutomaticoAtivo && estadoOperacao != EstadoOperacao.Parado)
        {
            CancelarOperacao("MODO MANUAL");
        }
        else
        {
            DefinirStatus("AUTOMÁTICO", 4f);
            proximaBusca = 0f;
        }

        return ModoAutomaticoAtivo ? "AUTOMÁTICO" : "MANUAL";
    }

    public float CarregarSemCusto(float quantidade)
    {
        if (quantidade <= 0f || CapacidadeCarga <= 0f)
        {
            return 0f;
        }

        float antes = cargaAtual;
        cargaAtual = Mathf.Clamp(cargaAtual + quantidade, 0f, capacidadeCarga);
        return cargaAtual - antes;
    }

    public float RemoverCarga(float quantidade)
    {
        if (quantidade <= 0f)
        {
            return 0f;
        }

        float retirado = Mathf.Min(quantidade, cargaAtual);
        cargaAtual -= retirado;
        return retirado;
    }

    private void AtualizarRadar()
    {
        alvosRadar.Clear();
        RegistroEntidadesJogo.FillUnidades(unidadesRegistradas);
        // O prefab Track legado não tinha IdentidadeUnidade; nesse caso seu
        // abastecimento continua restrito ao país do jogador (time 1).
        int meuTime = ResolverTeamId();
        float raioSqr = Mathf.Max(1f, raioRadar) * Mathf.Max(1f, raioRadar);

        for (int i = 0; i < unidadesRegistradas.Count; i++)
        {
            IdentidadeUnidade alvo = unidadesRegistradas[i];
            if (!AlvoTerrestreValido(alvo, meuTime, out CombustivelUnidade combustivel))
            {
                continue;
            }

            Vector3 delta = alvo.transform.position - transform.position;
            delta.y = 0f;
            float distanciaSqr = delta.sqrMagnitude;
            if (distanciaSqr > raioSqr || combustivel.Percentual >= 0.999f)
            {
                continue;
            }

            alvosRadar.Add(new AlvoRadarInfo
            {
                identidade = alvo,
                combustivel = combustivel,
                distancia = Mathf.Sqrt(distanciaSqr)
            });
        }

        alvosRadar.Sort((a, b) => a.distancia.CompareTo(b.distancia));
    }

    private bool AlvoTerrestreValido(IdentidadeUnidade alvo, int meuTime, out CombustivelUnidade combustivel)
    {
        combustivel = null;
        if (alvo == null || !alvo.gameObject.activeInHierarchy || alvo == identidade || alvo.transform.root == transform.root)
        {
            return false;
        }

        if (alvo.tipoUnidade != TipoUnidade.Veiculo && alvo.tipoUnidade != TipoUnidade.Infantaria)
        {
            return false;
        }

        if (meuTime > 0 && alvo.teamID != meuTime)
        {
            return false;
        }

        combustivel = alvo.GetComponent<CombustivelUnidade>()
            ?? alvo.GetComponentInChildren<CombustivelUnidade>(true);
        return combustivel != null
            && combustivel != combustivelProprio
            && combustivel.usaCombustivel
            && combustivel.classe == ClasseCombustivelUnidade.Terrestre
            && combustivel.Capacidade > 0f
            && combustivel.CombustivelAtual < combustivel.Capacidade - 0.01f;
    }

    private void TentarEscolherAlvoAutomatico()
    {
        AlvoRadarInfo? melhor = null;
        for (int i = 0; i < alvosRadar.Count; i++)
        {
            AlvoRadarInfo info = alvosRadar[i];
            if (info.identidade == null || info.combustivel == null
                || info.combustivel.Percentual > limiteCombustivelAutomatico)
            {
                continue;
            }

            if (!melhor.HasValue || info.distancia < melhor.Value.distancia)
            {
                melhor = info;
            }
        }

        if (melhor.HasValue)
        {
            IniciarOperacao(melhor.Value.identidade, melhor.Value.combustivel);
        }
    }

    private void IniciarOperacao(IdentidadeUnidade alvo, CombustivelUnidade combustivel)
    {
        if (estadoOperacao != EstadoOperacao.Parado || CargaAtual <= 0.01f
            || !AlvoTerrestreValido(alvo, ResolverTeamId(), out CombustivelUnidade validado))
        {
            DefinirStatus(CargaAtual <= 0.01f ? "CARGA VAZIA — pressione R para reabastecer" : "ALVO INDISPONÍVEL", 4f);
            return;
        }

        identidadeAlvoAtual = alvo;
        combustivelAlvoAtual = combustivel != null ? combustivel : validado;
        estadoOperacao = EstadoOperacao.IndoAoAlvo;
        sequenciaOrdem++;
        idOrdemMovimento = "abastecimento-terrestre:" + GetInstanceID() + ":" + alvo.GetInstanceID() + ":" + sequenciaOrdem;
        proximaRevisaoRota = 0f;
        DefinirStatus("INDO ATÉ " + alvo.name, 6f);

        if (DistanciaHorizontal(transform.position, alvo.transform.position) <= Mathf.Max(1f, raioAbastecimento))
        {
            PararOrdemPropria();
            estadoOperacao = EstadoOperacao.Abastecendo;
            return;
        }

        if (!EnviarOrdemAproximacao())
        {
            CancelarOperacao("ROTA TERRESTRE NÃO DISPONÍVEL");
        }
    }

    private void AtualizarOperacao()
    {
        if (identidadeAlvoAtual == null || combustivelAlvoAtual == null
            || !AlvoTerrestreValido(identidadeAlvoAtual, ResolverTeamId(), out _))
        {
            CancelarOperacao("ALVO INDISPONÍVEL");
            return;
        }

        if (estadoOperacao == EstadoOperacao.Abastecendo)
        {
            if (DistanciaHorizontal(transform.position, identidadeAlvoAtual.transform.position) > Mathf.Max(raioAbastecimento * 1.5f, raioAbastecimento + 10f))
            {
                estadoOperacao = EstadoOperacao.IndoAoAlvo;
                proximaRevisaoRota = 0f;
                if (!EnviarOrdemAproximacao()) CancelarOperacao("ALVO SAIU DO ALCANCE");
                return;
            }

            TransferirCombustivel();
            return;
        }

        OrdemMovimento ordem = controle != null ? controle.OrdemMovimentoAtual : null;
        if (ordem != null && !ordem.Terminada && !string.Equals(ordem.Dono, nameof(CaminhaoTanqueAbastecimento), System.StringComparison.Ordinal))
        {
            CancelarOperacao("ORDEM MANUAL ASSUMIU O CAMINHÃO");
            return;
        }

        if (agente != null && agente.enabled && agente.isOnNavMesh && !agente.pathPending
            && (agente.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid
                || agente.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathPartial && agente.hasPath))
        {
            CancelarOperacao("ROTA INCOMPLETA — selecione outro alvo");
            return;
        }

        if (DistanciaHorizontal(transform.position, identidadeAlvoAtual.transform.position) <= Mathf.Max(1f, raioAbastecimento))
        {
            PararOrdemPropria();
            estadoOperacao = EstadoOperacao.Abastecendo;
            DefinirStatus("ABASTECENDO " + identidadeAlvoAtual.name, 5f);
            return;
        }

        if (Time.time >= proximaRevisaoRota)
        {
            proximaRevisaoRota = Time.time + 1.5f;
            if (agente != null && agente.enabled && agente.isOnNavMesh && !agente.pathPending && !agente.hasPath)
            {
                // A unidade alvo pode ter se deslocado enquanto o caminhão vinha.
                sequenciaOrdem++;
                idOrdemMovimento = "abastecimento-terrestre:" + GetInstanceID() + ":" + identidadeAlvoAtual.GetInstanceID() + ":" + sequenciaOrdem;
                if (!EnviarOrdemAproximacao()) CancelarOperacao("ROTA TERRESTRE NÃO DISPONÍVEL");
            }
        }
    }

    private bool EnviarOrdemAproximacao()
    {
        if (identidadeAlvoAtual == null || controle == null)
        {
            return false;
        }

        Vector3 direcao = transform.position - identidadeAlvoAtual.transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.01f) direcao = -identidadeAlvoAtual.transform.forward;
        if (direcao.sqrMagnitude < 0.01f) direcao = Vector3.back;
        direcao.Normalize();

        Vector3 destino = identidadeAlvoAtual.transform.position + direcao * Mathf.Max(8f, raioAbastecimento * 0.65f);
        destino.y = identidadeAlvoAtual.transform.position.y;
        return controle.EmitirOrdemMovimento(
            destino,
            nameof(CaminhaoTanqueAbastecimento),
            TipoOrdemMovimento.Logistica,
            false,
            idOrdemMovimento);
    }

    private void TransferirCombustivel()
    {
        if (CargaAtual <= 0.01f)
        {
            CancelarOperacao("CARGA VAZIA — pressione R para reabastecer");
            return;
        }

        float espaco = combustivelAlvoAtual.Capacidade - combustivelAlvoAtual.CombustivelAtual;
        if (espaco <= 0.01f)
        {
            ConcluirOperacao("ABASTECIMENTO CONCLUÍDO");
            return;
        }

        float solicitado = Mathf.Min(Mathf.Max(0f, taxaTransferencia) * Time.deltaTime, CargaAtual, espaco);
        float aplicado = combustivelAlvoAtual.Abastecer(solicitado);
        RemoverCarga(aplicado);
        if (aplicado <= 0.001f)
        {
            CancelarOperacao("ABASTECIMENTO NÃO DISPONÍVEL");
            return;
        }

        DefinirStatus("ABASTECENDO " + identidadeAlvoAtual.name, 2f);
        if (CargaAtual <= 0.01f || combustivelAlvoAtual.CombustivelAtual >= combustivelAlvoAtual.Capacidade - 0.01f)
        {
            ConcluirOperacao(CargaAtual <= 0.01f ? "CARGA VAZIA" : "ABASTECIMENTO CONCLUÍDO");
        }
    }

    private void ConcluirOperacao(string mensagem)
    {
        PararOrdemPropria();
        LimparAlvo();
        DefinirStatus(mensagem, 5f);
        proximaBusca = Time.time + 0.5f;
    }

    private void CancelarOperacao(string mensagem)
    {
        PararOrdemPropria();
        LimparAlvo();
        DefinirStatus(mensagem, 5f);
    }

    private void PararOrdemPropria()
    {
        if (!string.IsNullOrEmpty(idOrdemMovimento) && controle != null)
        {
            controle.CancelarOrdemSePertenceA(nameof(CaminhaoTanqueAbastecimento), "abastecimento-terrestre");
        }
        idOrdemMovimento = null;
    }

    private void CancelarOrdemDeMovimento()
    {
        PararOrdemPropria();
        if (estadoOperacao == EstadoOperacao.IndoAoAlvo)
        {
            LimparAlvo();
        }
    }

    private void LimparAlvo()
    {
        estadoOperacao = EstadoOperacao.Parado;
        identidadeAlvoAtual = null;
        combustivelAlvoAtual = null;
    }

    private void OnGUI()
    {
        if (!mostrarIndicadorSelecionado || !EstaSelecionado())
        {
            return;
        }

        GUILayout.BeginArea(new Rect(20f, Screen.height - 360f, 310f, 340f), GUI.skin.box);
        GUIStyle titulo = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUILayout.Label("CAMINHÃO DE ABASTECIMENTO", titulo);
        GUILayout.Label("Modo: " + (ModoAutomaticoAtivo ? "AUTOMÁTICO" : "MANUAL") + "  |  Estado: " + estadoOperacao);
        GUILayout.Label("Carga: " + CargaAtual.ToString("F0") + " / " + CapacidadeCarga.ToString("F0") + "  (" + (PercentualCarga * 100f).ToString("F0") + "%)");
        GUILayout.Label("Radar terrestre: " + raioRadar.ToString("F0") + " m");

        if (GUILayout.Button(ModoAutomaticoAtivo ? "Mudar para MANUAL" : "Mudar para AUTOMÁTICO", GUILayout.Height(28f)))
        {
            AlternarModoOperacao();
        }

        if (!string.IsNullOrEmpty(mensagemStatus) && Time.time < mensagemStatusAte)
        {
            GUILayout.Label("Status: " + mensagemStatus, titulo);
        }

        if (estadoOperacao == EstadoOperacao.IndoAoAlvo || estadoOperacao == EstadoOperacao.Abastecendo)
        {
            GUILayout.Label("Alvo: " + (identidadeAlvoAtual != null ? identidadeAlvoAtual.name : "—"));
            if (GUILayout.Button("Cancelar operação", GUILayout.Height(26f)))
            {
                CancelarOperacao("CANCELADO");
            }
        }
        else if (!ModoAutomaticoAtivo)
        {
            GUILayout.Label("Unidades aliadas que precisam de combustível:", titulo);
            if (alvosRadar.Count == 0)
            {
                GUILayout.Label("Nenhuma unidade detectada no alcance.");
            }
            else
            {
                scrollRadar = GUILayout.BeginScrollView(scrollRadar, GUILayout.Height(125f));
                for (int i = 0; i < alvosRadar.Count; i++)
                {
                    AlvoRadarInfo info = alvosRadar[i];
                    if (info.identidade == null || info.combustivel == null) continue;
                    string texto = info.identidade.name + " — " + info.distancia.ToString("F0") + " m — " + (info.combustivel.Percentual * 100f).ToString("F0") + "%";
                    if (GUILayout.Button(texto, GUILayout.Height(26f)))
                    {
                        IniciarOperacao(info.identidade, info.combustivel);
                    }
                }
                GUILayout.EndScrollView();
            }
        }

        GUILayout.Label("Pressione e segure R para carregar combustível nacional.");
        GUILayout.EndArea();
    }

    private bool EstaSelecionado()
    {
        return controle != null && controle.selecionado;
    }

    private int ResolverTeamId()
    {
        return identidade != null ? identidade.teamID : 1;
    }

    private void ResolverReferencias()
    {
        if (controle == null) controle = GetComponent<ControleUnidade>();
        if (!identidadeResolvida)
        {
            identidade = GetComponent<IdentidadeUnidade>()
                ?? GetComponentInParent<IdentidadeUnidade>()
                ?? GetComponentInChildren<IdentidadeUnidade>(true);
            identidadeResolvida = true;
        }
        if (combustivelProprio == null) combustivelProprio = GetComponent<CombustivelUnidade>();
        if (agente == null) agente = GetComponent<UnityEngine.AI.NavMeshAgent>();
    }

    private void DefinirStatus(string texto, float duracao)
    {
        mensagemStatus = texto ?? string.Empty;
        mensagemStatusAte = Time.time + Mathf.Max(0.1f, duracao);
    }

    private static float DistanciaHorizontal(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
