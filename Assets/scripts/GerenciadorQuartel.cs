using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Hegemonia.RTS;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class GerenciadorQuartel : MonoBehaviour
{
    // Mantém o arquivo marcado para recompilação após alterações externas.
    [Serializable]
    public sealed class ContatoMilitarQuartelV2
    {
        public string id;
        public string idAlvoPersistente;
        public int idAlvo;
        public string nome;
        public string tipo;
        public int equipe;
        public string pais;
        public Vector3 posicao;
        public Vector3 direcao;
        public float velocidade;
        public string transmissor;
        public Vector3 posicaoTransmissor;
        public string horario;
        public float ultimaAtualizacao;
        public float validadeAte;
        public string estado;
        public bool inimigo;

        [NonSerialized] internal Transform transformAlvo;
        [NonSerialized] internal BoeingE3Reconhecimento.ContatoReconhecimento origemE3;
    }

    [Serializable]
    public sealed class UnidadeAbatidaQuartelV2
    {
        public string id;
        public string nome;
        public string tipo;
        public int equipe;
        public Vector3 posicao;
        public string horario;
        public string unidadeResponsavel;
        public string modoAtaque;
        public string resultado;
    }
    public enum ModoLancamentoCoordenadoV2
    {
        Manual,
        Automatico
    }

    [System.Serializable]
    public sealed class UnidadeLancamentoCoordenadoV2
    {
        public string id;
        public string nome;
        public string tipo;
        public string modoOperacional;
        public string sistemaLancamento;
        public Vector3 posicao;
        public float distanciaAoAlvo;
        public bool selecionada;
        public bool apta;
        public string estadoLancamento;
        public string motivo;

        [System.NonSerialized] internal ControleUnidade controle;
        [System.NonSerialized] internal IdentidadeUnidade identidade;
        [System.NonSerialized] internal LancadorNaval lancadorNaval;
        [System.NonSerialized] internal LancadorMisseis lancadorMisseis;
        [System.NonSerialized] internal ControleSubmarino submarino;
    }

    [System.Serializable]
    public sealed class AlvoLancamentoCoordenadoV2
    {
        public string id;
        public string nome;
        public string tipo;
        public int equipe;
        public Vector3 posicao;
        public float idadeSegundos;
        public string origem;
        public bool inimigo;
        public string pais;
        public string horario;
        public string estadoContato;
        public Vector3 direcao;
        public float velocidade;
        public float validadeAte;

        [System.NonSerialized] internal Transform transformAlvo;
    }

    [System.Serializable]
    public sealed class AvaliacaoLancamentoCoordenadoV2
    {
        public string unidadeId;
        public string unidadeNome;
        public bool selecionada;
        public bool apta;
        public string motivo;
        public float distanciaAoAlvo;
    }

    [System.Serializable]
    public sealed class TrilhaLancamentoCoordenadoV2
    {
        public string id;
        public string unidadeId;
        public string unidadeNome;
        public Vector3 pontoLancamento;
        public Vector3 pontoImpactoPrevisto;
        public string alvoId;
        public string modo;
        public string estado;
        public string missilId;
        public Vector3 pontoAtual;
        public float distanciaPercorrida;
        public float momento;

        [System.NonSerialized] internal Transform alvoDinamico;
    }

    [Header("Estrutura (Detectada Automaticamente)")]
    public List<Transform> dormitorios = new List<Transform>();
    public List<Transform> waypointsEntradaEstacionamento = new List<Transform>();
    public List<Transform> paradasEstacionamento = new List<Transform>();

    [Header("Unidades Armazenadas")]
    public List<ControleUnidade> soldadosNoDormitorio = new List<ControleUnidade>();
    public List<ControleUnidade> veiculosNoQuartel = new List<ControleUnidade>();
    
    private HashSet<Transform> vagasOcupadas = new HashSet<Transform>();

    [Header("Arsenal e Munição")]
    public int misseisArmazenados = 0;
    public int municaoArmazenada = 0;
    public long precoMissil = 5000000L;
    public long precoMunicao = 100000L;

    [Header("Chamada Automática (Limites de Área)")]
    public float raioDeCobertura = 2000f; 
    public bool recolhimentoAutomatico = false;
    public float tempoOciosoPermitido = 60f;
    private Dictionary<ControleUnidade, float> tempoOciosoUnidades = new Dictionary<ControleUnidade, float>();

    [Header("Recursos Extras (Inovação Tática)")]
    public bool treinamentoPassivo = true; 
    public bool modoDefensivoAtivo = false; 
    private float scanDefesaTimer = 0f;

    [Header("Quartel UI Toolkit V2")]
    [Tooltip("Mantem o novo painel UI Toolkit ativo. Desative para usar o IMGUI legado como fallback.")]
    public bool usarPainelQuartelUIToolkit = true;
    [Tooltip("Permite apenas o protocolo administrativo de recrutamento; a criacao de unidades continua no sistema de producao existente.")]
    public bool recrutamentoAutomatico = true;
    public bool treinamentoAutomatico = true;
    [Min(1)] public int metaEfetivo = 24;
    [Min(1f)] public float tempoFormacaoSegundos = 10f;
    [Min(1)] public int teamID = 1;
    [Tooltip("Somente para cenas de teste: abre o painel V2 ao iniciar o Play Mode sem alterar o prefab padrão.")]
    public bool abrirPainelAoIniciarNoPlayMode = false;

    private QuartelMenuUIController painelQuartelUI;
    private QuartelAdministracaoRuntime administracao;

    // UI Estilos
    // O estado do modal precisa pertencer a uma instância viva. Um bool
    // estático ficava preso em true quando a cena ou o GameObject do Quartel
    // era desativado, bloqueando seleção e ordens de movimento para sempre.
    private static GerenciadorQuartel interfaceAbertaAtual;
    private int frameAtalhoBConsumido = -1;
    public static bool InterfaceAberta
    {
        get
        {
            GerenciadorQuartel atual = interfaceAbertaAtual;
            if (atual == null || !atual.isActiveAndEnabled || !atual.menuAberto)
            {
                if (atual != null)
                {
                    interfaceAbertaAtual = null;
                }

                return false;
            }

            return true;
        }
    }
    private bool menuAberto = false;
    private Rect janelaRetangulo;
    private int abaAtual = 0; 
    private Vector2 scrollTropas;
    private Vector2 scrollInteligencia;
    private Vector2 scrollConvocar;
    private Vector2 scrollArsenal;
    private readonly List<ControleUnidade> soldadosAvulsosCache = new List<ControleUnidade>();
    private readonly List<ControleUnidade> veiculosAvulsosCache = new List<ControleUnidade>();
    private readonly HashSet<ControleUnidade> treinamentoPassivoAplicado = new HashSet<ControleUnidade>();
    private readonly HashSet<ControleUnidade> acolhimentosEmAndamento = new HashSet<ControleUnidade>();
    private float proximaAtualizacaoCacheCampo;

    [Header("Lançamento Coordenado do Quartel")]
    [Tooltip("Mantém a Carta Náutica como centro de autorização, sem mover navios ou submarinos.")]
    public bool habilitarLancamentoCoordenado = true;
    [Min(1f)] public float memoriaTrilhasLancamentoSegundos = 30f;
    private readonly List<IdentidadeUnidade> identidadesLancamentoCache = new List<IdentidadeUnidade>(256);
    private readonly List<BoeingE3Reconhecimento.ContatoReconhecimento> contatosE3Lancamento = new List<BoeingE3Reconhecimento.ContatoReconhecimento>(128);
    private readonly List<UnidadeLancamentoCoordenadoV2> unidadesLancamento = new List<UnidadeLancamentoCoordenadoV2>(64);
    private readonly List<AlvoLancamentoCoordenadoV2> alvosLancamento = new List<AlvoLancamentoCoordenadoV2>(64);
    private readonly List<AvaliacaoLancamentoCoordenadoV2> avaliacoesLancamento = new List<AvaliacaoLancamentoCoordenadoV2>(64);
    private readonly List<TrilhaLancamentoCoordenadoV2> trilhasLancamento = new List<TrilhaLancamentoCoordenadoV2>(64);
    private readonly List<ContatoMilitarQuartelV2> contatosMilitares = new List<ContatoMilitarQuartelV2>(128);
    private readonly Dictionary<string, ContatoMilitarQuartelV2> contatosPorId = new Dictionary<string, ContatoMilitarQuartelV2>(StringComparer.Ordinal);
    private readonly Dictionary<string, AlvoLancamentoCoordenadoV2> alvosPorId = new Dictionary<string, AlvoLancamentoCoordenadoV2>(StringComparer.Ordinal);
    private readonly List<UnidadeAbatidaQuartelV2> unidadesAbatidas = new List<UnidadeAbatidaQuartelV2>(64);
    private readonly HashSet<string> mortesRegistradas = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<SistemaDeDanos, Action> handlersMorte = new Dictionary<SistemaDeDanos, Action>();
    private readonly Dictionary<SistemaDeDanos, GameObject> ultimoAgressorPorVitima = new Dictionary<SistemaDeDanos, GameObject>();
    private readonly List<MissileThreatTracker> ameacasLancamentoCache = new List<MissileThreatTracker>(64);
    private readonly HashSet<string> unidadesSelecionadasLancamento = new HashSet<string>();
    private string alvoSelecionadoLancamentoId = string.Empty;
    private bool possuiPontoAlvoManual;
    private Vector3 pontoAlvoManual;
    private string origemPontoAlvoManual = string.Empty;
    private ModoLancamentoCoordenadoV2 modoLancamentoCoordenado = ModoLancamentoCoordenadoV2.Manual;
    private float proximaAtualizacaoLancamento;
    private int sequenciaLancamentoCoordenado;
    private string ultimoIdOperacaoLancamento = string.Empty;
    private string ultimoMotivoLancamento = string.Empty;

    public IReadOnlyList<UnidadeLancamentoCoordenadoV2> UnidadesLancamento => unidadesLancamento;
    public IReadOnlyList<AlvoLancamentoCoordenadoV2> AlvosLancamento => alvosLancamento;
    public IReadOnlyList<AvaliacaoLancamentoCoordenadoV2> AvaliacoesLancamento => avaliacoesLancamento;
    public IReadOnlyList<TrilhaLancamentoCoordenadoV2> TrilhasLancamento => trilhasLancamento;
    public IReadOnlyList<ContatoMilitarQuartelV2> ContatosMilitares => contatosMilitares;
    public IReadOnlyList<UnidadeAbatidaQuartelV2> UnidadesAbatidas => unidadesAbatidas;
    public string AlvoSelecionadoLancamentoId => alvoSelecionadoLancamentoId;
    public bool AlvoLancamentoSelecionadoValido => EncontrarAlvoLancamento(alvoSelecionadoLancamentoId) != null;
    public bool PossuiPontoAlvoManual => possuiPontoAlvoManual;
    public Vector3 PontoAlvoManual => pontoAlvoManual;
    public string OrigemPontoAlvoManual => origemPontoAlvoManual;
    public ModoLancamentoCoordenadoV2 ModoLancamentoCoordenado => modoLancamentoCoordenado;
    public string UltimoIdOperacaoLancamento => ultimoIdOperacaoLancamento;
    public string UltimoMotivoLancamento => ultimoMotivoLancamento;

    /// <summary>
    /// Usado apenas quando o atalho B criou este gerenciador no mesmo frame.
    /// Evita que o Update recém-adicionado veja o mesmo GetKeyDown e feche
    /// imediatamente o painel que acabou de abrir.
    /// </summary>
    public void MarcarAtalhoBConsumidoNesteFrame()
    {
        frameAtalhoBConsumido = Time.frameCount;
    }
    
    private GUIStyle estiloJanela;
    private GUIStyle estiloBotao;
    private GUIStyle estiloBotaoPerigo;
    private GUIStyle estiloBotaoSecundario;
    private GUIStyle estiloAba;
    private GUIStyle estiloAbaAtiva;
    private GUIStyle estiloTexto;
    private GUIStyle estiloTextoTitulo;
    private GUIStyle estiloTextoPequeno;
    private GUIStyle estiloCard;
    private GUIStyle estiloHeader;
    private bool estilosCriados = false;

    // Texturas reutilizáveis
    private static Texture2D _texFundoJanela;
    private static Texture2D _texBotao;
    private static Texture2D _texBotaoHover;
    private static Texture2D _texBotaoPerigo;
    private static Texture2D _texBotaoPerigHover;
    private static Texture2D _texBotaoSec;
    private static Texture2D _texBotaoSecHover;
    private static Texture2D _texAba;
    private static Texture2D _texAbaAtiva;
    private static Texture2D _texCard;
    private static Texture2D _texHeader;

    // Status Inteligência
    private class StatusInimigo {
        public string nomePais;
        public int infantaria;
        public int veiculos;
        public int navais;
        public int aereos;
        public int predios;
    }
    private Dictionary<int, StatusInimigo> infoInimigos = new Dictionary<int, StatusInimigo>();
    private float tagAtualizacaoIntel = 0f;

    void Awake()
    {
        precoMissil = 5000000L;
        precoMunicao = 100000L;
        MapearDormitorios();
        MapearEstacionamento();
        AtualizarRetanguloJanela(true);

        if (usarPainelQuartelUIToolkit)
        {
            painelQuartelUI = GetComponent<QuartelMenuUIController>();
            if (painelQuartelUI == null)
            {
                painelQuartelUI = gameObject.AddComponent<QuartelMenuUIController>();
            }
        }

        administracao = GetComponent<QuartelAdministracaoRuntime>();
        if (administracao == null)
        {
            administracao = gameObject.AddComponent<QuartelAdministracaoRuntime>();
        }
        administracao.teamID = Mathf.Max(1, teamID);
        administracao.tempoFormacaoPadraoSegundos = Mathf.Max(1f, tempoFormacaoSegundos);

        if (Application.isPlaying)
        {
            Debug.Log($"[Quartel] Awake: objeto={name}, cena={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, testeAuto={abrirPainelAoIniciarNoPlayMode}, uiToolkit={usarPainelQuartelUIToolkit}, painel={(painelQuartelUI != null)}", this);
        }
    }

    private void Start()
    {
        if (abrirPainelAoIniciarNoPlayMode)
        {
            Debug.Log($"[Quartel] instância de teste iniciou: objeto={name}, ativo={isActiveAndEnabled}, uiToolkit={usarPainelQuartelUIToolkit}, painel={(painelQuartelUI != null)}, administracao={(administracao != null)}, cena={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}", this);
        }

        if (Application.isPlaying && abrirPainelAoIniciarNoPlayMode)
        {
            Invoke(nameof(AbrirPainelQuartelAoIniciar), 0.75f);
        }
    }

    private void AbrirPainelQuartelAoIniciar()
    {
        Debug.Log($"[Quartel] tentativa de abertura automática: objeto={name}, ativo={isActiveAndEnabled}, menuAberto={menuAberto}, painel={(painelQuartelUI != null)}", this);
        if (this != null && isActiveAndEnabled && !menuAberto)
        {
            AlternarInterface();
            Debug.Log($"[Quartel] após AlternarInterface: menuAberto={menuAberto}, painelVisivel={(painelQuartelUI != null && painelQuartelUI.EstaVisivel)}", this);
        }
    }

    private void OnEnable()
    {
        BoeingE3Reconhecimento.OnContatoTransmitido -= AoReceberContatoDoE3;
        BoeingE3Reconhecimento.OnContatoTransmitido += AoReceberContatoDoE3;
        SistemaDeDanos.OnDanoGlobal -= AoDanoGlobal;
        SistemaDeDanos.OnDanoGlobal += AoDanoGlobal;
    }

    private void OnDestroy()
    {
        DesinscreverEventosOperacionais();
        if (painelQuartelUI != null)
        {
            painelQuartelUI.FecharInterno();
        }

        menuAberto = false;
        LimparInterfaceAberta();
    }

    private void OnDisable()
    {
        DesinscreverEventosOperacionais();
        // A troca de cena e a desativação do prefab passam por OnDisable, sem
        // necessariamente chegar a OnDestroy no mesmo frame. Limpar aqui
        // evita que o serviço de interação mantenha um modal órfão.
        if (painelQuartelUI != null)
        {
            painelQuartelUI.FecharInterno();
        }

        menuAberto = false;
        LimparInterfaceAberta();
    }

    private void DesinscreverEventosOperacionais()
    {
        BoeingE3Reconhecimento.OnContatoTransmitido -= AoReceberContatoDoE3;
        SistemaDeDanos.OnDanoGlobal -= AoDanoGlobal;
        foreach (KeyValuePair<SistemaDeDanos, Action> par in handlersMorte)
        {
            if (par.Key != null) par.Key.OnMorte -= par.Value;
        }
        handlersMorte.Clear();
        ultimoAgressorPorVitima.Clear();
    }

    private void AoDanoGlobal(SistemaDeDanos vitima, GameObject agressor, float dano)
    {
        if (vitima == null) return;
        ultimoAgressorPorVitima[vitima] = agressor;
    }

    private void AoReceberContatoDoE3(BoeingE3Reconhecimento.ContatoReconhecimento contato)
    {
        if (contato == null || contato.equipeObservadora != teamID) return;
        float distancia = Vector3.Distance(transform.position, contato.origemAeronavePosicao);
        float alcance = Mathf.Max(raioDeCobertura, contato.alcanceComunicacao);
        if (distancia > alcance + 0.01f) return;
        UpsertContatoMilitar(contato);
    }

    private void UpsertContatoMilitar(BoeingE3Reconhecimento.ContatoReconhecimento origem)
    {
        if (origem == null) return;
        string id = string.IsNullOrWhiteSpace(origem.idContato)
            ? "E3-" + origem.equipeObservadora + "-" + origem.idAlvo + "-" + origem.tipo
            : origem.idContato;
        ContatoMilitarQuartelV2 contato;
        if (!contatosPorId.TryGetValue(id, out contato) || contato == null)
        {
            contato = new ContatoMilitarQuartelV2 { id = id };
            contatosPorId[id] = contato;
            contatosMilitares.Add(contato);
        }

        contato.id = id;
        contato.idAlvo = origem.idAlvo;
        contato.idAlvoPersistente = origem.idAlvoPersistente;
        contato.nome = string.IsNullOrWhiteSpace(origem.nomeAlvo) ? "CONTATO " + origem.idAlvo : origem.nomeAlvo;
        contato.tipo = origem.tipo.ToString().ToUpperInvariant();
        contato.equipe = origem.equipeAlvo;
        contato.pais = string.IsNullOrWhiteSpace(origem.paisAlvo) ? "EQUIPE " + origem.equipeAlvo : origem.paisAlvo;
        contato.posicao = origem.ultimaPosicaoConhecida;
        contato.direcao = origem.direcao;
        contato.velocidade = origem.velocidade;
        contato.transmissor = string.IsNullOrWhiteSpace(origem.origemAeronave) ? origem.fonte : origem.origemAeronave;
        contato.posicaoTransmissor = origem.origemAeronavePosicao;
        contato.horario = string.IsNullOrWhiteSpace(origem.horarioDeteccao) ? DateTime.UtcNow.ToString("O") : origem.horarioDeteccao;
        contato.ultimaAtualizacao = origem.ultimaAtualizacao;
        contato.validadeAte = origem.validadeAte;
        contato.estado = string.IsNullOrWhiteSpace(origem.estado) ? "ATIVO" : origem.estado;
        contato.inimigo = origem.inimigo;
        contato.origemE3 = origem;
        proximaAtualizacaoLancamento = 0f;
    }

    private void AtualizarCacheContatosMilitares()
    {
        contatosE3Lancamento.Clear();
        BoeingE3Reconhecimento.CopiarContatosAtivos(teamID, contatosE3Lancamento);
        for (int i = 0; i < contatosE3Lancamento.Count; i++)
        {
            BoeingE3Reconhecimento.ContatoReconhecimento contato = contatosE3Lancamento[i];
            if (contato == null || !contato.inimigo) continue;
            UpsertContatoMilitar(contato);
        }

        float agora = Time.unscaledTime;
        for (int i = contatosMilitares.Count - 1; i >= 0; i--)
        {
            ContatoMilitarQuartelV2 contato = contatosMilitares[i];
            if (contato == null || (contato.validadeAte > 0f && agora > contato.validadeAte))
            {
                if (contato != null) contatosPorId.Remove(contato.id);
                contatosMilitares.RemoveAt(i);
            }
        }
    }

    private void RegistrarObservadorDeMorte(IdentidadeUnidade identidade)
    {
        if (identidade == null) return;
        SistemaDeDanos danos = identidade.GetComponent<SistemaDeDanos>();
        if (danos == null) danos = identidade.GetComponentInParent<SistemaDeDanos>();
        if (danos == null || handlersMorte.ContainsKey(danos)) return;

        IdentidadeUnidade identidadeCapturada = identidade;
        Action handler = () => RegistrarUnidadeAbatida(identidadeCapturada, danos);
        handlersMorte[danos] = handler;
        danos.OnMorte += handler;
    }

    private void RegistrarUnidadeAbatida(IdentidadeUnidade identidade, SistemaDeDanos danos)
    {
        if (identidade == null) return;
        string id = ObterIdLancamento(identidade.gameObject);
        if (!mortesRegistradas.Add(id)) return;

        GameObject agressor = null;
        ultimoAgressorPorVitima.TryGetValue(danos, out agressor);
        IdentidadeUnidade identidadeAgressora = SistemaDeDanos.ResolverIdentidade(agressor != null ? agressor.transform : null);
        unidadesAbatidas.Insert(0, new UnidadeAbatidaQuartelV2
        {
            id = id,
            nome = identidade.name,
            tipo = identidade.tipoUnidade.ToString(),
            equipe = identidade.teamID,
            posicao = identidade.transform.position,
            horario = DateTime.Now.ToString("HH:mm:ss"),
            unidadeResponsavel = identidadeAgressora != null ? identidadeAgressora.name : "DESCONHECIDA",
            modoAtaque = ResolverModoAtaque(agressor),
            resultado = "UNIDADE ABATIDA"
        });
        if (unidadesAbatidas.Count > 64) unidadesAbatidas.RemoveAt(unidadesAbatidas.Count - 1);
    }

    private static string ResolverModoAtaque(GameObject agressor)
    {
        if (agressor == null) return "DESCONHECIDO";

        ControleSubmarino submarino = agressor.GetComponent<ControleSubmarino>()
            ?? agressor.GetComponentInParent<ControleSubmarino>()
            ?? agressor.GetComponentInChildren<ControleSubmarino>(true);
        if (submarino != null) return submarino.modoAtual.ToString().ToUpperInvariant();

        LancadorNaval lancador = agressor.GetComponent<LancadorNaval>()
            ?? agressor.GetComponentInParent<LancadorNaval>()
            ?? agressor.GetComponentInChildren<LancadorNaval>(true);
        if (lancador != null) return lancador.modoAtual.ToString().ToUpperInvariant();

        return "ATAQUE EXTERNO";
    }

    private void RegistrarInterfaceAberta()
    {
        interfaceAbertaAtual = this;
    }

    private void LimparInterfaceAberta()
    {
        if (interfaceAbertaAtual == this)
        {
            interfaceAbertaAtual = null;
        }
    }

    private static bool CampoTextoQuartelEmEdicao()
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        var selecionado = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selecionado == null || !selecionado.activeInHierarchy)
            return false;

        var campo = selecionado.GetComponent<UnityEngine.UI.InputField>();
        return campo != null && campo.isFocused;
    }

    void Update()
    {
        bool atalhoQuartel = RTSInputBindings.GetKeyDown(RTSInputAction.Barracks)
            || Input.GetKeyDown(KeyCode.B);

        if (atalhoQuartel)
        {
            if (frameAtalhoBConsumido == Time.frameCount)
            {
                frameAtalhoBConsumido = -1;
                return;
            }

            if (!menuAberto && MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto)
            {
                MenuComandoController.Instancia.FecharMenu();
            }
            AlternarInterface();
            return;
        }

        if (CampoTextoQuartelEmEdicao()) return;

        if (recolhimentoAutomatico)
        {
            MonitorarUnidadesOciosas();
        }

        if (modoDefensivoAtivo && Time.time > scanDefesaTimer)
        {
            ChecarInvasaoEAcordarBase();
            scanDefesaTimer = Time.time + 4f;
        }

        if (menuAberto)
        {
            if (abaAtual == 0)
                AtualizarCacheUnidadesCampo(false);
            else if (abaAtual == 2)
            {
                if (Time.unscaledTime > tagAtualizacaoIntel)
                {
                    AtualizarDadosInimigos();
                    tagAtualizacaoIntel = Time.unscaledTime + 3f;
                }
            }
        }
    }

    public void AlternarInterface()
    {
        if (!menuAberto)
        {
            FecharOutrosMenus();
            menuAberto = true;
            RegistrarInterfaceAberta();
            AtualizarRetanguloJanela(true);
            if (usarPainelQuartelUIToolkit && painelQuartelUI != null)
            {
                painelQuartelUI.Abrir();
            }
        }
        else
        {
            menuAberto = false;
            LimparInterfaceAberta();
            if (painelQuartelUI != null)
            {
                painelQuartelUI.FecharInterno();
            }
        }
    }

    /// <summary>
    /// Fecha o painel sem que a UI precise conhecer o estado interno do
    /// gerenciador. O atalho B continua chamando AlternarInterface().
    /// </summary>
    public void FecharInterfacePorUI()
    {
        menuAberto = false;
        LimparInterfaceAberta();
        if (painelQuartelUI != null)
        {
            painelQuartelUI.FecharInterno();
        }
    }

    private void FecharOutrosMenus()
    {
        if (MenuGoverno.Instancia != null) MenuGoverno.Instancia.AlternarMenu(false);
        var construtor = Object.FindFirstObjectByType<MenuConstrucao>();
        if (construtor != null && MenuConstrucao.EstaAberto) construtor.AlternarMenu(false);

        MenuPier pier = Object.FindFirstObjectByType<MenuPier>();
        if (pier != null && MenuPier.EstaAberto) pier.FecharMenu();

        MenuMisseis misseis = Object.FindFirstObjectByType<MenuMisseis>();
        if (misseis != null && MenuMisseis.EstaAberto) misseis.CancelarLancamento();

        if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto)
        {
            MenuComandoController.Instancia.FecharMenu();
        }

        if (FazendaMenuController.EstaAberto && FazendaMenuController.Instancia != null)
        {
            FazendaMenuController.Instancia.FecharParaOutraInterface();
        }

        if (FabricaMineriosMenuController.EstaAberto && FabricaMineriosMenuController.Instancia != null)
        {
            FabricaMineriosMenuController.Instancia.FecharParaOutraInterface();
        }
    }

    private void AtualizarRetanguloJanela(bool centralizar)
    {
        float larguraMaxima = Mathf.Max(760f, Screen.width - 340f);
        float larguraMinima = Mathf.Min(1040f, larguraMaxima);
        float alturaMaxima = Mathf.Max(560f, Screen.height - 80f);
        float alturaMinima = Mathf.Min(660f, alturaMaxima);
        float largura = Mathf.Clamp(Screen.width * 0.66f, larguraMinima, larguraMaxima);
        float altura = Mathf.Clamp(Screen.height * 0.78f, alturaMinima, alturaMaxima);

        janelaRetangulo.width = largura;
        janelaRetangulo.height = altura;

        if (centralizar)
        {
            janelaRetangulo.x = Mathf.Max(280f, (Screen.width - largura) * 0.5f);
            janelaRetangulo.y = Mathf.Max(32f, (Screen.height - altura) * 0.5f);
        }
    }

    private void ChecarInvasaoEAcordarBase()
    {
        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        bool inimigoProximo = false;

        foreach (var id in todas)
        {
            if (id.teamID != 1 && Vector3.Distance(transform.position, id.transform.position) <= raioDeCobertura)
            {
                inimigoProximo = true;
                break;
            }
        }

        if (inimigoProximo)
        {
            if (soldadosNoDormitorio.Count > 0 || veiculosNoQuartel.Count > 0)
            {
                DesdobrarSoldados(soldadosNoDormitorio.Count);
                int totalV = veiculosNoQuartel.Count;
                for(int i = totalV - 1; i >= 0; i--) DesdobrarVeiculo(veiculosNoQuartel[i]);
            }
        }
    }

    private void MonitorarUnidadesOciosas()
    {
        if (Time.frameCount % 90 != 0) return;

        IdentidadeUnidade[] todasUnidades = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        
        foreach (var id in todasUnidades)
        {
            if (id.teamID != 1) continue;

            ControleUnidade u = id.GetComponent<ControleUnidade>();
            if (u == null || !u.gameObject.activeInHierarchy) continue;
            
            if (u.TemControleAviao || u.TemControleAviaoCaca || id.tipoUnidade == TipoUnidade.Naval || id.tipoUnidade == TipoUnidade.Aereo || id.tipoUnidade == TipoUnidade.Estrutura) 
                continue;

            if (Vector3.Distance(transform.position, u.transform.position) > raioDeCobertura) continue;

            if (u.ObterVelocidadeAtualReal() > 0.1f || u.selecionado || 
                veiculosNoQuartel.Contains(u) || soldadosNoDormitorio.Contains(u))
            {
                tempoOciosoUnidades[u] = Time.time;
            }
            else
            {
                if (!tempoOciosoUnidades.ContainsKey(u)) tempoOciosoUnidades[u] = Time.time;

                float tempoParado = Time.time - tempoOciosoUnidades[u];
                if (tempoParado > tempoOciosoPermitido)
                {
                    ReceberUnidade(u);
                    tempoOciosoUnidades.Remove(u);
                }
            }
        }
    }

    private Texture2D CriarTextura(Color cor)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, cor);
        tex.Apply();
        return tex;
    }

    private Texture2D CriarTexturaGradiente(Color topo, Color base_)
    {
        Texture2D tex = new Texture2D(1, 4);
        tex.SetPixel(0, 0, base_);
        tex.SetPixel(0, 1, Color.Lerp(base_, topo, 0.33f));
        tex.SetPixel(0, 2, Color.Lerp(base_, topo, 0.66f));
        tex.SetPixel(0, 3, topo);
        tex.Apply();
        return tex;
    }

    private void InicializarEstilos()
    {
        if (estilosCriados) return;

        // --- Paleta Principal ---
        // Fundo da janela: cinza escuro quase preto com leve tom azul-escuro
        if (_texFundoJanela == null) _texFundoJanela = CriarTexturaGradiente(
            new Color(0.10f, 0.12f, 0.16f, 0.99f),
            new Color(0.07f, 0.08f, 0.11f, 0.99f));

        // Botão primário: verde-oliva militar com hover âmbar
        if (_texBotao == null)     _texBotao     = CriarTexturaGradiente(new Color(0.18f, 0.30f, 0.15f, 1f), new Color(0.12f, 0.20f, 0.10f, 1f));
        if (_texBotaoHover == null) _texBotaoHover = CriarTexturaGradiente(new Color(0.75f, 0.55f, 0.05f, 1f), new Color(0.55f, 0.38f, 0.02f, 1f));

        // Botão perigo: vermelho
        if (_texBotaoPerigo == null)    _texBotaoPerigo    = CriarTextura(new Color(0.50f, 0.08f, 0.08f, 1f));
        if (_texBotaoPerigHover == null) _texBotaoPerigHover = CriarTextura(new Color(0.80f, 0.15f, 0.10f, 1f));

        // Botão secundário: azul-aço
        if (_texBotaoSec == null)     _texBotaoSec     = CriarTexturaGradiente(new Color(0.10f, 0.20f, 0.38f, 1f), new Color(0.07f, 0.13f, 0.26f, 1f));
        if (_texBotaoSecHover == null) _texBotaoSecHover = CriarTexturaGradiente(new Color(0.15f, 0.35f, 0.60f, 1f), new Color(0.10f, 0.22f, 0.45f, 1f));

        // Abas
        if (_texAba == null)      _texAba      = CriarTextura(new Color(0.13f, 0.16f, 0.20f, 1f));
        if (_texAbaAtiva == null) _texAbaAtiva = CriarTexturaGradiente(new Color(0.72f, 0.53f, 0.04f, 1f), new Color(0.50f, 0.35f, 0.01f, 1f));

        // Card de item
        if (_texCard == null)   _texCard   = CriarTextura(new Color(0.12f, 0.15f, 0.19f, 0.95f));
        if (_texHeader == null) _texHeader = CriarTexturaGradiente(new Color(0.65f, 0.48f, 0.03f, 0.30f), new Color(0.08f, 0.10f, 0.14f, 0.30f));

        // --- Janela ---
        estiloJanela = new GUIStyle(GUI.skin.window);
        estiloJanela.normal.background = _texFundoJanela;
        estiloJanela.normal.textColor = new Color(0.90f, 0.82f, 0.40f);
        estiloJanela.fontStyle = FontStyle.Bold;
        estiloJanela.fontSize = 18;
        estiloJanela.padding = new RectOffset(10, 10, 30, 10);

        // --- Botão primário ---
        estiloBotao = new GUIStyle(GUI.skin.button);
        estiloBotao.normal.background  = _texBotao;
        estiloBotao.hover.background   = _texBotaoHover;
        estiloBotao.normal.textColor   = new Color(0.85f, 0.95f, 0.75f);
        estiloBotao.hover.textColor    = new Color(0.10f, 0.06f, 0.02f);
        estiloBotao.active.background  = _texBotaoHover;
        estiloBotao.padding = new RectOffset(8, 8, 7, 7);
        estiloBotao.fontSize = 14;
        estiloBotao.fontStyle = FontStyle.Bold;
        estiloBotao.wordWrap = true;

        // --- Botão perigo ---
        estiloBotaoPerigo = new GUIStyle(estiloBotao);
        estiloBotaoPerigo.normal.background = _texBotaoPerigo;
        estiloBotaoPerigo.hover.background  = _texBotaoPerigHover;
        estiloBotaoPerigo.normal.textColor  = Color.white;
        estiloBotaoPerigo.hover.textColor   = Color.white;

        // --- Botão secundário ---
        estiloBotaoSecundario = new GUIStyle(estiloBotao);
        estiloBotaoSecundario.normal.background = _texBotaoSec;
        estiloBotaoSecundario.hover.background  = _texBotaoSecHover;
        estiloBotaoSecundario.normal.textColor  = new Color(0.70f, 0.88f, 1.0f);
        estiloBotaoSecundario.hover.textColor   = Color.white;

        // --- Abas ---
        estiloAba = new GUIStyle(estiloBotao);
        estiloAba.normal.background = _texAba;
        estiloAba.hover.background  = _texAbaAtiva;
        estiloAba.normal.textColor  = new Color(0.65f, 0.75f, 0.85f);
        estiloAba.hover.textColor   = new Color(0.10f, 0.06f, 0.02f);
        estiloAba.fontSize = 14;
        estiloAba.fontStyle = FontStyle.Bold;
        estiloAba.padding   = new RectOffset(12, 12, 10, 10);

        estiloAbaAtiva = new GUIStyle(estiloAba);
        estiloAbaAtiva.normal.background = _texAbaAtiva;
        estiloAbaAtiva.normal.textColor  = new Color(0.10f, 0.06f, 0.02f);

        // --- Textos ---
        estiloTexto = new GUIStyle(GUI.skin.label);
        estiloTexto.normal.textColor = new Color(0.78f, 0.90f, 0.78f);
        estiloTexto.fontSize = 13;
        estiloTexto.fontStyle = FontStyle.Normal;
        estiloTexto.wordWrap = true;

        estiloTextoTitulo = new GUIStyle(estiloTexto);
        estiloTextoTitulo.normal.textColor = new Color(0.90f, 0.82f, 0.40f);
        estiloTextoTitulo.fontSize = 14;
        estiloTextoTitulo.fontStyle = FontStyle.Bold;

        estiloTextoPequeno = new GUIStyle(estiloTexto);
        estiloTextoPequeno.fontSize = 12;
        estiloTextoPequeno.normal.textColor = new Color(0.55f, 0.70f, 0.55f);

        // --- Card (caixas de item) ---
        estiloCard = new GUIStyle(GUI.skin.box);
        estiloCard.normal.background = _texCard;
        estiloCard.padding = new RectOffset(8, 8, 6, 6);
        estiloCard.margin  = new RectOffset(0, 0, 3, 3);

        // --- Header de seção ---
        estiloHeader = new GUIStyle(GUI.skin.box);
        estiloHeader.normal.background = _texHeader;
        estiloHeader.normal.textColor  = new Color(0.90f, 0.82f, 0.40f);
        estiloHeader.fontSize  = 13;
        estiloHeader.fontStyle = FontStyle.Bold;
        estiloHeader.alignment = TextAnchor.MiddleLeft;
        estiloHeader.padding   = new RectOffset(10, 6, 5, 5);

        estilosCriados = true;
    }

    void OnGUI()
    {
        if (!menuAberto) return;
        if (usarPainelQuartelUIToolkit && painelQuartelUI != null && painelQuartelUI.EstaVisivel) return;
        InicializarEstilos();

        GUI.depth = -100;
        janelaRetangulo = GUI.Window(943, janelaRetangulo, DesenharJanela, "  ⚔  QUARTEL GENERAL  |  CENTRO DE COMANDO", estiloJanela);
    }

    void DesenharJanela(int windowID)
    {
        // --- Header de Status ---
        GUILayout.BeginHorizontal(estiloHeader, GUILayout.Height(36));
        GUILayout.Label($"🪖 Soldados: {soldadosNoDormitorio.Count}   🚗 Veículos: {veiculosNoQuartel.Count}   🚀 Mísseis: {misseisArmazenados}   💊 Munição: {municaoArmazenada}", estiloTextoTitulo);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Raio: {raioDeCobertura:F0}m", estiloTextoPequeno, GUILayout.Width(110));
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // --- Abas ---
        string[] nomesAbas   = { "🪖  TROPAS", "🔧  ARSENAL", "🛰  INTELIGÊNCIA" };
        GUILayout.BeginHorizontal();
        for (int i = 0; i < nomesAbas.Length; i++)
        {
            GUIStyle estilo = (abaAtual == i) ? estiloAbaAtiva : estiloAba;
            if (GUILayout.Button(nomesAbas[i], estilo, GUILayout.Height(40)))
                abaAtual = i;
        }
        GUILayout.EndHorizontal();

        // Linha separadora visual
        Rect linhaRect = GUILayoutUtility.GetLastRect();
        GUILayout.Space(6);

        if (abaAtual == 0) DesenharAbaTropas();
        else if (abaAtual == 1) DesenharAbaArsenal();
        else if (abaAtual == 2) DesenharAbaInteligencia();

        if (GUI.Button(new Rect(janelaRetangulo.width - 42, 4, 36, 26), "✕", estiloBotaoPerigo))
        {
            menuAberto = false;
            LimparInterfaceAberta();
        }

        GUI.DragWindow(new Rect(0, 0, janelaRetangulo.width, 30));
    }

    private void AtualizarCacheUnidadesCampo(bool forcar)
    {
        if (!forcar && Time.unscaledTime < proximaAtualizacaoCacheCampo)
        {
            return;
        }

        proximaAtualizacaoCacheCampo = Time.unscaledTime + 0.75f;
        soldadosAvulsosCache.Clear();
        veiculosAvulsosCache.Clear();

        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        float raioSqr = raioDeCobertura * raioDeCobertura;

        foreach (var id in todas)
        {
            if (id == null || id.teamID != 1) continue;

            ControleUnidade u = id.GetComponent<ControleUnidade>();
            if (u == null || !u.gameObject.activeInHierarchy) continue;
            if (u.TemControleAviao || u.TemControleAviaoCaca || id.tipoUnidade == TipoUnidade.Naval || id.tipoUnidade == TipoUnidade.Estrutura || id.tipoUnidade == TipoUnidade.Aereo) continue;
            if (veiculosNoQuartel.Contains(u) || soldadosNoDormitorio.Contains(u)) continue;
            if ((u.transform.position - transform.position).sqrMagnitude > raioSqr) continue;

            SistemaDeDanos dmg = u.GetComponent<SistemaDeDanos>();
            if (dmg != null && dmg.unidadeBiologica) soldadosAvulsosCache.Add(u);
            else veiculosAvulsosCache.Add(u);
        }
    }

    private void DesenharSeparador(string titulo)
    {
        GUILayout.Space(4);
        GUILayout.Label(titulo, estiloHeader, GUILayout.ExpandWidth(true), GUILayout.Height(24));
        GUILayout.Space(4);
    }

    void DesenharAbaTropas()
    {
        float colW = janelaRetangulo.width * 0.48f;
        GUILayout.BeginHorizontal();

        // =========== COLUNA ESQUERDA — RECOLHER DO CAMPO ===========
        GUILayout.BeginVertical(estiloCard, GUILayout.Width(colW));

        DesenharSeparador($"📡  EM CAMPO  —  Soldados: {soldadosAvulsosCache.Count}  |  Veículos: {veiculosAvulsosCache.Count}");

        if (GUILayout.Button("↩  CONVOCAR SELECIONADOS NO MAPA", estiloBotaoSecundario, GUILayout.Height(36)))
        {
            foreach (var u in Object.FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None))
                if (u.selecionado && u.GetComponent<IdentidadeUnidade>()?.teamID == 1)
                {
                    u.selecionado = false;
                    ReceberUnidade(u);
                }
        }
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"↩ Chamar Infantaria ({soldadosAvulsosCache.Count})", estiloBotao, GUILayout.Height(32)))
            foreach (var u in soldadosAvulsosCache) ReceberUnidade(u);
        if (GUILayout.Button($"↩ Chamar Veículos ({veiculosAvulsosCache.Count})", estiloBotao, GUILayout.Height(32)))
            foreach (var u in veiculosAvulsosCache) ReceberUnidade(u);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        scrollConvocar = GUILayout.BeginScrollView(scrollConvocar);

        if (soldadosAvulsosCache.Count > 0)
        {
            GUILayout.Label("  🪖 INFANTARIA LIVRE", estiloTextoTitulo);
            foreach (var s in soldadosAvulsosCache)
            {
                GUILayout.BeginHorizontal(estiloCard);
                GUILayout.Label($"· {s.name}", estiloTexto);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↩ Convocar", estiloBotao, GUILayout.Width(95), GUILayout.Height(26))) ReceberUnidade(s);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
        }
        if (veiculosAvulsosCache.Count > 0)
        {
            GUILayout.Label("  🚗 VEÍCULOS LIVRES", estiloTextoTitulo);
            foreach (var v in veiculosAvulsosCache)
            {
                GUILayout.BeginHorizontal(estiloCard);
                GUILayout.Label($"· {v.name}", estiloTexto);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↩ Convocar", estiloBotao, GUILayout.Width(95), GUILayout.Height(26))) ReceberUnidade(v);
                GUILayout.EndHorizontal();
            }
        }
        if (soldadosAvulsosCache.Count == 0 && veiculosAvulsosCache.Count == 0)
            GUILayout.Label("  ✅  Nenhuma unidade solta no raio do Quartel.", estiloTextoPequeno);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(8);

        // =========== COLUNA DIREITA — TROPAS ARMAZENADAS ===========
        GUILayout.BeginVertical(estiloCard, GUILayout.Width(colW));

        DesenharSeparador($"🏠  ARMAZENADAS  —  Soldados: {soldadosNoDormitorio.Count}  |  Veículos: {veiculosNoQuartel.Count}");

        // Soldados
        GUILayout.Label("  🪖 DORMITÓRIO", estiloTextoTitulo);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Desdobrar 1",  estiloBotao, GUILayout.Height(32))) DesdobrarSoldados(1);
        if (GUILayout.Button("Desdobrar 5",  estiloBotao, GUILayout.Height(32))) DesdobrarSoldados(5);
        if (GUILayout.Button("Esvaziar Tudo", estiloBotaoPerigo, GUILayout.Height(32))) DesdobrarSoldados(soldadosNoDormitorio.Count);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Veículos
        GUILayout.Label("  🚗 GARAGEM", estiloTextoTitulo);
        if (GUILayout.Button("🔑  LIGAR TODOS OS VEÍCULOS", estiloBotaoPerigo, GUILayout.Height(34)))
        {
            int totalV = veiculosNoQuartel.Count;
            for (int i = totalV - 1; i >= 0; i--) DesdobrarVeiculo(veiculosNoQuartel[i]);
        }

        GUILayout.Space(6);
        scrollTropas = GUILayout.BeginScrollView(scrollTropas);
        for (int i = 0; i < veiculosNoQuartel.Count; i++)
        {
            ControleUnidade v = veiculosNoQuartel[i];
            if (v == null) continue;
            GUILayout.BeginHorizontal(estiloCard);
            GUILayout.Label($"· {v.name}", estiloTexto);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔑 Ligar", estiloBotaoSecundario, GUILayout.Width(80), GUILayout.Height(26))) DesdobrarVeiculo(v);
            GUILayout.EndHorizontal();
        }
        if (veiculosNoQuartel.Count == 0)
            GUILayout.Label("  ✅  Nenhum veículo estacionado.", estiloTextoPequeno);
        GUILayout.EndScrollView();

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    void DesenharAbaArsenal()
    {
        scrollArsenal = GUILayout.BeginScrollView(scrollArsenal);

        // --- Protocolos ---
        DesenharSeparador("⚙  PROTOCOLOS DA BASE");
        GUILayout.BeginVertical(estiloCard);
        recolhimentoAutomatico = GUILayout.Toggle(recolhimentoAutomatico, "  📻  Recolhimento Automático  (chama unidades ociosas por rádio)", estiloTexto);
        if (recolhimentoAutomatico)
        {
            GUILayout.Label($"     Tempo ocioso antes de chamar: {Mathf.Round(tempoOciosoPermitido)}s", estiloTextoPequeno);
            tempoOciosoPermitido = GUILayout.HorizontalSlider(tempoOciosoPermitido, 10f, 300f);
        }
        GUILayout.Space(4);
        modoDefensivoAtivo = GUILayout.Toggle(modoDefensivoAtivo, "  🛡  Defesa Automática  (libera tudo se a base for invadida)", estiloTexto);
        GUILayout.Space(4);
        treinamentoPassivo = GUILayout.Toggle(treinamentoPassivo, "  💪  Treinamento Passivo  (bônus de HP para unidades em repouso)", estiloTexto);
        GUILayout.EndVertical();

        // --- Arsenal ---
        GUILayout.Space(6);
        DesenharSeparador("🚀  ARSENAL E MUNIÇÕES");
        GUILayout.BeginVertical(estiloCard);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"🚀  Mísseis Armazenados:", estiloTexto, GUILayout.Width(210));
        GUILayout.Label($"{misseisArmazenados}", estiloTextoTitulo);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"🔫  Pacotes de Munição:", estiloTexto, GUILayout.Width(210));
        GUILayout.Label($"{municaoArmazenada}", estiloTextoTitulo);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        if (GerenciadorRecursos.Instancia != null)
        {
            GUILayout.Label($"💰  Fundo Nacional: ${GerenciadorRecursos.Instancia.dinheiro}", estiloTextoTitulo);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"🚀  Encomendar Mísseis  (-${precoMissil})", estiloBotao, GUILayout.Height(42)))
                if (GerenciadorRecursos.Instancia.TentarGastarDinheiro(precoMissil)) misseisArmazenados += 10;
            if (GUILayout.Button($"🔫  Encomendar Munição  (-${precoMunicao})", estiloBotao, GUILayout.Height(42)))
                if (GerenciadorRecursos.Instancia.TentarGastarDinheiro(precoMunicao)) municaoArmazenada += 100;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        // --- Logística ---
        GUILayout.Space(6);
        DesenharSeparador("🚛  LOGÍSTICA DE ABASTECIMENTO");
        GUILayout.BeginVertical(estiloCard);
        CaminhaoCombustivel.AbastecimentoAutomaticoGlobal = GUILayout.Toggle(CaminhaoCombustivel.AbastecimentoAutomaticoGlobal, "  🔄  Abastecimento Automático  (Tracks buscam unidades com combustível baixo)", estiloTexto);
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("⛽  Carregar Tracks neste QG", estiloBotao, GUILayout.Height(36)))
            foreach (var c in Object.FindObjectsByType<CaminhaoCombustivel>(FindObjectsSortMode.None))
                if (c != null) c.ForcarRecarregarNoQuartel(this);
        if (GUILayout.Button("↩  Forçar Retorno à Base", estiloBotaoSecundario, GUILayout.Height(36)))
        {
            var caminhoes = Object.FindObjectsByType<CaminhaoCombustivel>(FindObjectsSortMode.None);
            foreach (var c in caminhoes)
                if (c != null) { c.DefinirQuartelPreferencial(this); c.ForcarRetornoBase(); }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("  ℹ  Tracks atendem somente a área do QG, recarregam abaixo de 20% e retornam para reabastecimento.", estiloTextoPequeno);
        GUILayout.EndVertical();

        GUILayout.EndScrollView();
    }

    void DesenharAbaInteligencia()
    {
        DesenharSeparador("🛰  VARREDURA SATELITAL — ESPIONAGEM CIBERNÉTICA");
        GUILayout.Label("  Monitoramento em tempo real dos países oponentes.", estiloTextoPequeno);
        GUILayout.Space(6);

        scrollInteligencia = GUILayout.BeginScrollView(scrollInteligencia);

        foreach (var kvp in infoInimigos)
        {
            if (kvp.Key == 1) continue;

            var status = kvp.Value;
            GUILayout.BeginVertical(estiloCard);

            // Cabeçalho do país
            GUILayout.BeginHorizontal(estiloHeader, GUILayout.Height(28));
            GUILayout.Label($"🔴  {status.nomePais.ToUpper()}", estiloTextoTitulo);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Time #{kvp.Key}", estiloTextoPequeno, GUILayout.Width(70));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            int max = Mathf.Max(1, status.infantaria + status.veiculos + status.aereos + status.navais);
            DesenharBarraForca("🪖 Infantaria",  status.infantaria, max, new Color(0.3f, 0.7f, 0.3f));
            DesenharBarraForca("🚗 Blindados",   status.veiculos,   max, new Color(0.6f, 0.5f, 0.2f));
            DesenharBarraForca("✈  Aéreos",     status.aereos,     max, new Color(0.2f, 0.5f, 0.9f));
            DesenharBarraForca("⚓ Naval",       status.navais,     max, new Color(0.1f, 0.6f, 0.8f));
            GUILayout.Label($"   🏛  Estruturas: {status.predios}", estiloTextoPequeno);
            GUILayout.Space(4);

            GUILayout.EndVertical();
            GUILayout.Space(8);
        }

        if (infoInimigos.Count <= 1)
            GUILayout.Label("  📡  Aguardando sinal... Nenhum inimigo monitorado.", estiloTextoPequeno);

        GUILayout.EndScrollView();
    }

    private void DesenharBarraForca(string label, int valor, int maximo, Color cor)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"   {label}:", estiloTexto, GUILayout.Width(130));
        GUILayout.Label($"{valor}", estiloTextoTitulo, GUILayout.Width(40));

        Rect baraBg = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(12));
        float fill = maximo > 0 ? Mathf.Clamp01((float)valor / maximo) : 0f;
        // Fundo
        Color oldColor = GUI.color;
        GUI.color = new Color(0.12f, 0.15f, 0.18f, 1f);
        GUI.DrawTexture(baraBg, Texture2D.whiteTexture);
        // Preenchimento
        Rect barFill = new Rect(baraBg.x, baraBg.y, baraBg.width * fill, baraBg.height);
        GUI.color = cor;
        GUI.DrawTexture(barFill, Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUILayout.Space(8);
        GUILayout.EndHorizontal();
    }

    void AtualizarDadosInimigos()
    {
        infoInimigos.Clear();
        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        
        foreach (var id in todas)
        {
            if (!infoInimigos.ContainsKey(id.teamID))
                infoInimigos[id.teamID] = new StatusInimigo { nomePais = id.nomeDoPais };
            
            var s = infoInimigos[id.teamID];
            if (id.tipoUnidade == TipoUnidade.Infantaria) s.infantaria++;
            else if (id.tipoUnidade == TipoUnidade.Veiculo) s.veiculos++;
            else if (id.tipoUnidade == TipoUnidade.Aereo) s.aereos++;
            else if (id.tipoUnidade == TipoUnidade.Naval) s.navais++;
            else if (id.tipoUnidade == TipoUnidade.Estrutura) s.predios++;
        }
    }

    private void MapearDormitorios()
    {
        Transform dom = ObterFilhoPorNome(transform, "dormitorio");
        if (dom != null)
            foreach (Transform filho in dom)
                dormitorios.Add(filho);
    }

    private void MapearEstacionamento()
    {
        Transform estac = ObterFilhoPorNome(transform, "estacionamento");
        if (estac != null)
        {
            Transform entrada = ObterFilhoPorNome(estac, "entrada");
            if (entrada != null)
                foreach (Transform filho in entrada)
                    waypointsEntradaEstacionamento.Add(filho);

            Transform paradas = ObterFilhoPorNome(estac, "paradas");
            if (paradas != null)
                foreach (Transform filho in paradas)
                    paradasEstacionamento.Add(filho);
        }
    }

    private Transform ObterFilhoPorNome(Transform pai, string nomeContido)
    {
        Transform[] todos = pai.GetComponentsInChildren<Transform>(true);
        foreach (Transform filho in todos)
            if (filho.name.ToLower().Contains(nomeContido.ToLower()))
                return filho;
        return null;
    }

    public void ReceberUnidade(ControleUnidade unidade)
    {
        if (unidade == null || !unidade.gameObject.activeInHierarchy) return;
        if (!acolhimentosEmAndamento.Add(unidade)) return;
        SistemaDeDanos sistemaDeDanos = unidade.GetComponent<SistemaDeDanos>();
        bool biologica = (sistemaDeDanos != null && sistemaDeDanos.unidadeBiologica);

        StartCoroutine(AcolherUnidadeSemDuplicacao(unidade, sistemaDeDanos, biologica));
    }

    private IEnumerator AcolherUnidadeSemDuplicacao(ControleUnidade unidade, SistemaDeDanos danos, bool biologica)
    {
        if (biologica)
            yield return StartCoroutine(AcolherSoldado(unidade, danos));
        else
            yield return StartCoroutine(AcolherVeiculo(unidade, danos));

        acolhimentosEmAndamento.Remove(unidade);
    }

    public void SolicitarConvocarSelecionados()
    {
        ControleUnidade[] unidades = Object.FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None);
        for (int i = 0; i < unidades.Length; i++)
        {
            ControleUnidade unidade = unidades[i];
            IdentidadeUnidade identidade = unidade != null ? unidade.GetComponent<IdentidadeUnidade>() : null;
            if (unidade == null || identidade == null || identidade.teamID != teamID || !unidade.selecionado) continue;
            unidade.selecionado = false;
            ReceberUnidade(unidade);
        }
    }

    public void SolicitarDesdobramentoSoldados(int quantidade)
    {
        DesdobrarSoldados(Mathf.Max(0, quantidade));
    }

    public void SolicitarDesdobramentoTodosVeiculos()
    {
        int total = veiculosNoQuartel != null ? veiculosNoQuartel.Count : 0;
        for (int i = total - 1; i >= 0; i--)
        {
            DesdobrarVeiculo(veiculosNoQuartel[i]);
        }
    }

    public void SolicitarReparosNoRaio()
    {
        IdentidadeUnidade[] identidades = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        float raioSqr = raioDeCobertura * raioDeCobertura;
        for (int i = 0; i < identidades.Length; i++)
        {
            IdentidadeUnidade identidade = identidades[i];
            if (identidade == null || identidade.teamID != teamID) continue;
            ControleUnidade unidade = identidade.GetComponent<ControleUnidade>();
            if (unidade == null || (unidade.transform.position - transform.position).sqrMagnitude > raioSqr) continue;
            SistemaDeDanos danos = unidade.GetComponent<SistemaDeDanos>();
            if (danos != null) danos.Reparar(9999f);
        }
    }

    public void SolicitarResgateManual()
    {
        if (administracao != null)
        {
            administracao.RegistrarResgateManual();
            return;
        }

        SolicitarReparosNoRaio();
    }

    /// <summary>
    /// Atualiza somente a leitura operacional usada pela Carta Náutica. A
    /// rotina não emite ordens, não chama EmitirOrdemMover e não toca no
    /// Transform das unidades.
    /// </summary>
    public void AtualizarDadosLancamento(bool forcar = false)
    {
        if (!habilitarLancamentoCoordenado) return;
        if (!forcar && Time.unscaledTime < proximaAtualizacaoLancamento) return;

        proximaAtualizacaoLancamento = Time.unscaledTime + 0.75f;
        identidadesLancamentoCache.Clear();
        RegistroEntidadesJogo.FillUnidades(identidadesLancamentoCache);
        if (identidadesLancamentoCache.Count == 0)
        {
            IdentidadeUnidade[] encontrados = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                if (encontrados[i] != null && !identidadesLancamentoCache.Contains(encontrados[i]))
                    identidadesLancamentoCache.Add(encontrados[i]);
            }
        }

        AtualizarCacheContatosMilitares();

        HashSet<string> idsPresentes = new HashSet<string>();
        for (int i = 0; i < identidadesLancamentoCache.Count; i++)
        {
            IdentidadeUnidade identidade = identidadesLancamentoCache[i];
            if (identidade == null || !identidade.gameObject.activeInHierarchy || identidade.teamID != teamID) continue;

            RegistrarObservadorDeMorte(identidade);

            ControleSubmarino submarino = ObterComponenteLancamento<ControleSubmarino>(identidade.gameObject);
            LancadorNaval lancador = ObterComponenteLancamento<LancadorNaval>(identidade.gameObject);
            LancadorMisseis lancadorEstrategico = ObterComponenteLancamento<LancadorMisseis>(identidade.gameObject);
            if (submarino == null && lancador == null && lancadorEstrategico == null) continue;

            string id = ObterIdLancamento(identidade.gameObject);
            idsPresentes.Add(id);
            UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamento(id);
            if (unidade == null)
            {
                unidade = new UnidadeLancamentoCoordenadoV2 { id = id };
                unidadesLancamento.Add(unidade);
            }

            unidade.identidade = identidade;
            unidade.controle = ObterComponenteLancamento<ControleUnidade>(identidade.gameObject);
            unidade.submarino = submarino;
            unidade.lancadorNaval = lancador;
            unidade.lancadorMisseis = lancadorEstrategico;
            unidade.nome = identidade.name;
            unidade.tipo = identidade.tipoUnidade.ToString().ToUpperInvariant();
            unidade.posicao = identidade.transform.position;
            unidade.selecionada = unidadesSelecionadasLancamento.Contains(id);
            unidade.sistemaLancamento = submarino != null ? "CONTROLE SUBMARINO" :
                lancador != null ? "LANÇADOR NAVAL" : "LANÇADOR DE MÍSSEIS";
            unidade.modoOperacional = submarino != null
                ? submarino.modoAtual.ToString().ToUpperInvariant()
                : lancador != null ? lancador.modoAtual.ToString().ToUpperInvariant() : "MANUAL";
            unidade.motivo = string.Empty;
            unidade.apta = false;
        }

        for (int i = unidadesLancamento.Count - 1; i >= 0; i--)
        {
            if (!idsPresentes.Contains(unidadesLancamento[i].id))
            {
                unidadesSelecionadasLancamento.Remove(unidadesLancamento[i].id);
                unidadesLancamento.RemoveAt(i);
            }
        }

        AtualizarAlvosLancamento();
        LimparTrilhasLancamento();
        AtualizarValidacaoLancamentoCoordenado();
    }

    public bool AlternarSelecaoLancamento(string unidadeId)
    {
        UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamento(unidadeId);
        if (unidade == null) return false;

        if (!unidadesSelecionadasLancamento.Add(unidadeId))
            unidadesSelecionadasLancamento.Remove(unidadeId);

        unidade.selecionada = unidadesSelecionadasLancamento.Contains(unidadeId);
        AtualizarValidacaoLancamentoCoordenado();
        return unidade.selecionada;
    }

    public bool AlternarSelecaoLancamento(string unidadeId, bool substituirSelecao)
    {
        UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamento(unidadeId);
        if (unidade == null) return false;
        if (substituirSelecao)
        {
            unidadesSelecionadasLancamento.Clear();
            for (int i = 0; i < unidadesLancamento.Count; i++)
                if (unidadesLancamento[i] != null) unidadesLancamento[i].selecionada = false;
        }
        return AlternarSelecaoLancamento(unidadeId);
    }

    /// <summary>
    /// Seleciona um executor sem alterna-lo. Serve para o modificador Shift da
    /// Carta: Ctrl alterna, Shift adiciona, e o clique simples substitui.
    /// </summary>
    public bool SelecionarUnidadeLancamento(string unidadeId, bool substituirSelecao = false)
    {
        UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamento(unidadeId);
        if (unidade == null) return false;

        if (substituirSelecao)
        {
            unidadesSelecionadasLancamento.Clear();
            for (int i = 0; i < unidadesLancamento.Count; i++)
            {
                if (unidadesLancamento[i] != null) unidadesLancamento[i].selecionada = false;
            }
        }

        unidadesSelecionadasLancamento.Add(unidadeId);
        unidade.selecionada = true;
        AtualizarValidacaoLancamentoCoordenado();
        return true;
    }

    public void LimparSelecaoLancamento()
    {
        unidadesSelecionadasLancamento.Clear();
        for (int i = 0; i < unidadesLancamento.Count; i++)
            if (unidadesLancamento[i] != null) unidadesLancamento[i].selecionada = false;
        AtualizarValidacaoLancamentoCoordenado();
    }

    public bool SelecionarAlvoLancamento(string alvoId)
    {
        AlvoLancamentoCoordenadoV2 alvo = EncontrarAlvoLancamento(alvoId);
        if (alvo == null) return false;
        alvoSelecionadoLancamentoId = alvo.id;
        AtualizarValidacaoLancamentoCoordenado();
        return true;
    }

    public bool DefinirPontoAlvoManual(Vector3 ponto, string origem = "COORDENADAS MANUAIS")
    {
        const string idManual = "quartel-ponto-manual";
        AlvoLancamentoCoordenadoV2 alvo = EncontrarAlvoLancamento(idManual);
        if (alvo == null)
        {
            alvo = new AlvoLancamentoCoordenadoV2 { id = idManual };
            alvosLancamento.Add(alvo);
            alvosPorId[idManual] = alvo;
        }
        alvo.nome = "PONTO MANUAL";
        alvo.tipo = "COORDENADA";
        alvo.equipe = -1;
        alvo.posicao = ponto;
        alvo.idadeSegundos = 0f;
        alvo.origem = origem;
        alvo.inimigo = false;
        alvo.pais = "N/A";
        alvo.horario = DateTime.Now.ToString("HH:mm:ss");
        alvo.estadoContato = "PONTO DEFINIDO PELO JOGADOR";
        alvo.validadeAte = float.PositiveInfinity;
        alvo.transformAlvo = null;
        possuiPontoAlvoManual = true;
        pontoAlvoManual = ponto;
        origemPontoAlvoManual = origem;
        alvoSelecionadoLancamentoId = idManual;
        AtualizarValidacaoLancamentoCoordenado();
        return true;
    }

    public bool UsarCoordenadasDoAlvo()
    {
        AlvoLancamentoCoordenadoV2 alvo = EncontrarAlvoLancamento(alvoSelecionadoLancamentoId);
        if (alvo == null || alvo.id == "quartel-ponto-manual") return false;
        possuiPontoAlvoManual = false;
        pontoAlvoManual = alvo.posicao;
        origemPontoAlvoManual = "CONTATO E-3";
        AtualizarValidacaoLancamentoCoordenado();
        return true;
    }

    public bool CancelarOperacaoLancamento()
    {
        LimparSelecaoLancamento();
        alvoSelecionadoLancamentoId = string.Empty;
        possuiPontoAlvoManual = false;
        pontoAlvoManual = Vector3.zero;
        origemPontoAlvoManual = string.Empty;
        avaliacoesLancamento.Clear();
        ultimoMotivoLancamento = "operacao de lancamento cancelada";
        return true;
    }

    public bool AlternarModoOperacionalLancador(string unidadeId)
    {
        UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamento(unidadeId);
        if (unidade == null) return false;
        if (unidade.lancadorNaval != null)
        {
            unidade.lancadorNaval.AlternarEstadoOperacional();
            AtualizarDadosLancamento(true);
            return true;
        }
        if (unidade.submarino != null)
        {
            unidade.submarino.AlternarEstadoOperacional();
            AtualizarDadosLancamento(true);
            return true;
        }
        // LancadorMisseis é o componente legado de lançamento estratégico e
        // não possui ciclo Passivo/Manual/Automático. Ele permanece manual.
        if (unidade.lancadorMisseis != null)
        {
            unidade.modoOperacional = "MANUAL";
            unidade.motivo = "lancador estrategico opera somente em modo manual";
            AtualizarValidacaoLancamentoCoordenado();
            return false;
        }
        if (unidade.controle != null)
        {
            unidade.controle.AlternarEstadoOperacional();
            AtualizarDadosLancamento(true);
            return true;
        }
        return false;
    }

    public string ObterMotivoBloqueioLancamento(string unidadeId)
    {
        UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamento(unidadeId);
        if (unidade == null) return "unidade nao encontrada";
        return unidade.apta ? "lancamento autorizado" :
            (string.IsNullOrWhiteSpace(unidade.motivo) ? "unidade nao validada" : unidade.motivo);
    }

    public void DefinirModoLancamentoCoordenado(ModoLancamentoCoordenadoV2 modo)
    {
        modoLancamentoCoordenado = modo;
        AtualizarValidacaoLancamentoCoordenado();
    }

    public void AtualizarValidacaoLancamentoCoordenado()
    {
        avaliacoesLancamento.Clear();
        AlvoLancamentoCoordenadoV2 alvo = EncontrarAlvoLancamento(alvoSelecionadoLancamentoId);
        Vector3 pontoAlvo = alvo != null ? ObterPontoAlvoLancamento(alvo) : Vector3.zero;

        for (int i = 0; i < unidadesLancamento.Count; i++)
        {
            UnidadeLancamentoCoordenadoV2 unidade = unidadesLancamento[i];
            if (unidade == null || !unidade.selecionada) continue;

            string motivo;
            bool apta = ValidarUnidadeLancamento(unidade, alvo, pontoAlvo, out motivo);
            unidade.distanciaAoAlvo = alvo != null ? Vector3.Distance(unidade.posicao, pontoAlvo) : 0f;
            unidade.apta = apta;
            unidade.motivo = motivo;
            avaliacoesLancamento.Add(new AvaliacaoLancamentoCoordenadoV2
            {
                unidadeId = unidade.id,
                unidadeNome = unidade.nome,
                selecionada = true,
                apta = apta,
                motivo = apta ? "lancamento autorizado" : motivo,
                distanciaAoAlvo = unidade.distanciaAoAlvo
            });
        }
    }

    /// <summary>
    /// Autoriza uma operação para as unidades selecionadas. Cada unidade é
    /// validada e dispara a partir do próprio Transform; uma unidade bloqueada
    /// não impede as demais. Nenhuma ordem de movimento é criada aqui.
    /// </summary>
    public bool TryExecutarLancamentoCoordenado(out string motivo)
    {
        motivo = string.Empty;
        if (!habilitarLancamentoCoordenado)
        {
            motivo = "lancamento coordenado desabilitado no Quartel";
            ultimoMotivoLancamento = motivo;
            return false;
        }

        AtualizarDadosLancamento(true);
        AlvoLancamentoCoordenadoV2 alvo = EncontrarAlvoLancamento(alvoSelecionadoLancamentoId);
        if (alvo == null)
        {
            motivo = "selecione um contato transmitido pelo E-3";
            ultimoMotivoLancamento = motivo;
            return false;
        }

        int selecionadas = 0;
        for (int i = 0; i < unidadesLancamento.Count; i++)
            if (unidadesLancamento[i] != null && unidadesLancamento[i].selecionada) selecionadas++;
        if (selecionadas == 0)
        {
            motivo = "selecione pelo menos um navio ou submarino compatível";
            ultimoMotivoLancamento = motivo;
            return false;
        }

        AtualizarValidacaoLancamentoCoordenado();
        ultimoIdOperacaoLancamento = "QG-LANC-" + (++sequenciaLancamentoCoordenado).ToString("0000");
        int autorizados = 0;
        int bloqueados = 0;
        Vector3 pontoAlvo = ObterPontoAlvoLancamento(alvo);

        for (int i = 0; i < avaliacoesLancamento.Count; i++)
        {
            AvaliacaoLancamentoCoordenadoV2 avaliacao = avaliacoesLancamento[i];
            UnidadeLancamentoCoordenadoV2 unidade = EncontrarUnidadeLancamento(avaliacao.unidadeId);
            if (unidade == null) continue;

            if (!avaliacao.apta)
            {
                bloqueados++;
                unidade.estadoLancamento = "BLOQUEADO";
                continue;
            }

            Transform alvoDinamico = alvo.transformAlvo != null && alvo.transformAlvo.gameObject.activeInHierarchy
                ? alvo.transformAlvo
                : null;
            string falha;
            bool lancou = false;
            if (unidade.submarino != null)
            {
                lancou = unidade.submarino.TentarLancarCoordenado(pontoAlvo, alvoDinamico, modoLancamentoCoordenado == ModoLancamentoCoordenadoV2.Automatico, out falha);
            }
            else if (unidade.lancadorNaval != null)
            {
                lancou = unidade.lancadorNaval.TentarLancarCoordenado(pontoAlvo, alvoDinamico, modoLancamentoCoordenado == ModoLancamentoCoordenadoV2.Automatico, out falha);
            }
            else if (unidade.lancadorMisseis != null)
            {
                lancou = unidade.lancadorMisseis.TentarLancarCoordenado(
                    pontoAlvo,
                    alvoDinamico,
                    modoLancamentoCoordenado == ModoLancamentoCoordenadoV2.Automatico,
                    out falha);
            }
            else
            {
                falha = "nenhum executor de lançamento compatível";
            }

            if (!lancou)
            {
                bloqueados++;
                unidade.apta = false;
                unidade.motivo = falha;
                unidade.estadoLancamento = "BLOQUEADO";
                continue;
            }

            autorizados++;
            unidade.estadoLancamento = "LANÇAMENTO AUTORIZADO";
            unidade.motivo = string.Empty;
            trilhasLancamento.Add(new TrilhaLancamentoCoordenadoV2
            {
                id = ultimoIdOperacaoLancamento + "-" + unidade.id,
                unidadeId = unidade.id,
                unidadeNome = unidade.nome,
                pontoLancamento = unidade.posicao,
                pontoImpactoPrevisto = pontoAlvo,
                alvoId = alvo.id,
                modo = modoLancamentoCoordenado == ModoLancamentoCoordenadoV2.Automatico ? "AUTOMATICO" : "MANUAL",
                estado = "LANÇAMENTO AUTORIZADO",
                pontoAtual = unidade.posicao,
                distanciaPercorrida = 0f,
                alvoDinamico = alvoDinamico,
                momento = Time.unscaledTime
            });
        }

        if (autorizados == 0)
        {
            motivo = "nenhuma unidade selecionada foi autorizada";
            ultimoMotivoLancamento = motivo;
            return false;
        }

        motivo = bloqueados > 0
            ? autorizados + " lançamento(s) autorizado(s); " + bloqueados + " bloqueado(s) individualmente"
            : autorizados + " lançamento(s) coordenado(s) autorizado(s)";
        ultimoMotivoLancamento = motivo;
        return true;
    }

    private void AtualizarAlvosLancamento()
    {
        HashSet<string> presentes = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < contatosMilitares.Count; i++)
        {
            ContatoMilitarQuartelV2 contato = contatosMilitares[i];
            if (contato == null || !contato.inimigo) continue;

            IdentidadeUnidade identidadeAlvo = null;
            for (int j = 0; j < identidadesLancamentoCache.Count; j++)
            {
                IdentidadeUnidade candidata = identidadesLancamentoCache[j];
                if (candidata != null
                    && (candidata.GetInstanceID() == contato.idAlvo
                        || (!string.IsNullOrWhiteSpace(contato.idAlvoPersistente)
                            && string.Equals(
                                ObterIdLancamento(candidata.gameObject),
                                contato.idAlvoPersistente,
                                StringComparison.Ordinal))))
                {
                    identidadeAlvo = candidata;
                    break;
                }
            }

            string id = contato.id;
            presentes.Add(id);
            AlvoLancamentoCoordenadoV2 alvo = EncontrarAlvoLancamento(id);
            if (alvo == null)
            {
                alvo = new AlvoLancamentoCoordenadoV2 { id = id };
                alvosLancamento.Add(alvo);
                alvosPorId[id] = alvo;
            }
            alvo.nome = contato.nome;
            alvo.tipo = contato.tipo;
            alvo.equipe = contato.equipe;
            alvo.posicao = contato.posicao;
            alvo.idadeSegundos = Mathf.Max(0f, Time.unscaledTime - contato.ultimaAtualizacao);
            alvo.origem = contato.transmissor;
            alvo.inimigo = contato.inimigo;
            alvo.pais = contato.pais;
            alvo.horario = contato.horario;
            alvo.estadoContato = contato.estado;
            alvo.direcao = contato.direcao;
            alvo.velocidade = contato.velocidade;
            alvo.validadeAte = contato.validadeAte;
            alvo.transformAlvo = identidadeAlvo != null ? identidadeAlvo.transform : null;
        }

        if (possuiPontoAlvoManual)
        {
            presentes.Add("quartel-ponto-manual");
        }

        for (int i = alvosLancamento.Count - 1; i >= 0; i--)
        {
            AlvoLancamentoCoordenadoV2 alvo = alvosLancamento[i];
            if (alvo == null || !presentes.Contains(alvo.id))
            {
                if (alvo != null) alvosPorId.Remove(alvo.id);
                alvosLancamento.RemoveAt(i);
            }
        }

        if (!string.IsNullOrWhiteSpace(alvoSelecionadoLancamentoId)
            && EncontrarAlvoLancamento(alvoSelecionadoLancamentoId) == null)
            alvoSelecionadoLancamentoId = string.Empty;
    }

    private bool ValidarUnidadeLancamento(UnidadeLancamentoCoordenadoV2 unidade, AlvoLancamentoCoordenadoV2 alvo, Vector3 pontoAlvo, out string motivo)
    {
        motivo = string.Empty;
        if (alvo == null)
        {
            motivo = "nenhum alvo transmitido selecionado";
            return false;
        }

        if (modoLancamentoCoordenado == ModoLancamentoCoordenadoV2.Automatico
            && (!alvo.inimigo || alvo.id == "quartel-ponto-manual"))
        {
            motivo = "modo automatico exige um contato inimigo valido";
            return false;
        }

        if (modoLancamentoCoordenado == ModoLancamentoCoordenadoV2.Automatico
            && alvo.validadeAte > 0f && Time.unscaledTime > alvo.validadeAte)
        {
            motivo = "contato transmitido expirado";
            return false;
        }

        if (modoLancamentoCoordenado == ModoLancamentoCoordenadoV2.Automatico
            && (string.IsNullOrWhiteSpace(alvo.origem)
                || string.Equals(alvo.estadoContato, "EXPIRADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(alvo.estadoContato, "PERDIDO", StringComparison.OrdinalIgnoreCase)))
        {
            motivo = "contato sem comunicacao valida";
            return false;
        }

        bool automatico = modoLancamentoCoordenado == ModoLancamentoCoordenadoV2.Automatico;
        Transform alvoDinamico = alvo.transformAlvo != null && alvo.transformAlvo.gameObject.activeInHierarchy ? alvo.transformAlvo : null;
        if (unidade.submarino != null)
            return unidade.submarino.PodeLancarCoordenado(pontoAlvo, alvoDinamico, automatico, out motivo);
        if (unidade.lancadorNaval != null)
            return unidade.lancadorNaval.PodeLancarCoordenado(pontoAlvo, alvoDinamico, automatico, out motivo);
        if (unidade.lancadorMisseis != null)
            return unidade.lancadorMisseis.PodeLancarCoordenado(pontoAlvo, automatico, out motivo);

        motivo = "nenhum executor de lançamento compatível";
        return false;
    }

    private static T ObterComponenteLancamento<T>(GameObject objeto) where T : Component
    {
        if (objeto == null) return null;
        T componente = objeto.GetComponent<T>();
        if (componente == null) componente = objeto.GetComponentInParent<T>();
        if (componente == null) componente = objeto.GetComponentInChildren<T>(true);
        return componente;
    }

    private static string ObterIdLancamento(GameObject objeto)
    {
        SaveableEntity saveable = objeto != null ? objeto.GetComponent<SaveableEntity>() : null;
        if (saveable == null && objeto != null) saveable = objeto.GetComponentInParent<SaveableEntity>();
        if (saveable == null && objeto != null) saveable = objeto.GetComponentInChildren<SaveableEntity>(true);
        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId)) return saveable.UniqueId;
        return objeto == null ? "unidade-sem-objeto" : "runtime-" + objeto.GetInstanceID();
    }

    private UnidadeLancamentoCoordenadoV2 EncontrarUnidadeLancamento(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < unidadesLancamento.Count; i++)
            if (unidadesLancamento[i] != null && unidadesLancamento[i].id == id) return unidadesLancamento[i];
        return null;
    }

    private AlvoLancamentoCoordenadoV2 EncontrarAlvoLancamento(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < alvosLancamento.Count; i++)
            if (alvosLancamento[i] != null && alvosLancamento[i].id == id) return alvosLancamento[i];
        return null;
    }

    private static Vector3 ObterPontoAlvoLancamento(AlvoLancamentoCoordenadoV2 alvo)
    {
        if (alvo != null && alvo.transformAlvo != null && alvo.transformAlvo.gameObject.activeInHierarchy)
            return alvo.transformAlvo.position;
        return alvo != null ? alvo.posicao : Vector3.zero;
    }

    private void LimparTrilhasLancamento()
    {
        float agora = Time.unscaledTime;
        MissileThreatTracker.CopiarAmeacasAtivas(ameacasLancamentoCache);
        for (int i = trilhasLancamento.Count - 1; i >= 0; i--)
        {
            TrilhaLancamentoCoordenadoV2 trilha = trilhasLancamento[i];
            if (trilha == null || agora - trilha.momento > Mathf.Max(1f, memoriaTrilhasLancamentoSegundos))
            {
                trilhasLancamento.RemoveAt(i);
                continue;
            }

            if (trilha.alvoDinamico != null && trilha.alvoDinamico.gameObject.activeInHierarchy)
                trilha.pontoImpactoPrevisto = trilha.alvoDinamico.position;

            MissileThreatTracker melhor = null;
            float menorDistancia = 25f * 25f;
            for (int j = 0; j < ameacasLancamentoCache.Count; j++)
            {
                MissileThreatTracker ameaca = ameacasLancamentoCache[j];
                if (ameaca == null || ameaca.NomeOrigem != trilha.unidadeNome) continue;
                float distancia = (ameaca.PontoLancamento - trilha.pontoLancamento).sqrMagnitude;
                if (distancia < menorDistancia)
                {
                    menorDistancia = distancia;
                    melhor = ameaca;
                }
            }

            if (melhor != null)
            {
                trilha.missilId = melhor.MissileId.ToString();
                trilha.pontoAtual = melhor.RaizMissil != null ? melhor.RaizMissil.position : trilha.pontoLancamento;
                trilha.distanciaPercorrida = Vector3.Distance(trilha.pontoLancamento, trilha.pontoAtual);
                trilha.estado = "EM VOO";
            }
        }
    }

    public QuartelAdministracaoRuntime ObterAdministracao()
    {
        if (administracao == null) administracao = GetComponent<QuartelAdministracaoRuntime>();
        return administracao;
    }

    public void SolicitarTracksNoQuartel()
    {
        CaminhaoCombustivel[] caminhoes = Object.FindObjectsByType<CaminhaoCombustivel>(FindObjectsSortMode.None);
        for (int i = 0; i < caminhoes.Length; i++)
        {
            if (caminhoes[i] != null) caminhoes[i].ForcarRecarregarNoQuartel(this);
        }
    }

    public void SolicitarRetornoTracks()
    {
        CaminhaoCombustivel[] caminhoes = Object.FindObjectsByType<CaminhaoCombustivel>(FindObjectsSortMode.None);
        for (int i = 0; i < caminhoes.Length; i++)
        {
            if (caminhoes[i] == null) continue;
            caminhoes[i].DefinirQuartelPreferencial(this);
            caminhoes[i].ForcarRetornoBase();
        }
    }

    public bool TentarEncomendarMisseis()
    {
        if (GerenciadorRecursos.Instancia == null || !GerenciadorRecursos.Instancia.TentarGastarDinheiro(precoMissil)) return false;
        misseisArmazenados += 10;
        return true;
    }

    public bool TentarEncomendarMunicao()
    {
        if (GerenciadorRecursos.Instancia == null || !GerenciadorRecursos.Instancia.TentarGastarDinheiro(precoMunicao)) return false;
        municaoArmazenada += 100;
        return true;
    }

    private IEnumerator AcolherSoldado(ControleUnidade soldado, SistemaDeDanos danos)
    {
        if (soldadosNoDormitorio.Contains(soldado)) yield break; // Evita loop de duplicação

        Transform destino = transform; 
        if (dormitorios.Count > 0) destino = dormitorios[Random.Range(0, dormitorios.Count)];

        soldado.EmitirOrdemMover(destino.position);

        while (soldado != null && soldado.gameObject.activeInHierarchy)
        {
            if (Vector3.Distance(soldado.transform.position, destino.position) < 4f) break;
            yield return null;
        }

        if (soldado != null)
        {
            if (danos != null) 
            {
                danos.Reparar(9999f);
                if (treinamentoPassivo && !treinamentoPassivoAplicado.Contains(soldado))
                {
                    danos.vidaMaxima *= 1.2f;
                    treinamentoPassivoAplicado.Add(soldado);
                }
            }
            soldado.gameObject.SetActive(false); 
            if (!soldadosNoDormitorio.Contains(soldado)) soldadosNoDormitorio.Add(soldado);
        }
    }

    private IEnumerator AcolherVeiculo(ControleUnidade veiculo, SistemaDeDanos danos)
    {
        if (veiculosNoQuartel.Contains(veiculo)) yield break; // Evita duplicação

        for (int i = 0; i < waypointsEntradaEstacionamento.Count; i++)
        {
            if (veiculo == null) yield break;
            Transform wp = waypointsEntradaEstacionamento[i];
            veiculo.EmitirOrdemMover(wp.position);
            while (veiculo != null)
            {
                if (Vector3.Distance(veiculo.transform.position, wp.position) < 5f) break;
                yield return null;
            }
        }

        if (veiculo == null) yield break;

        Transform vagaEscolhida = null;
        foreach (Transform vaga in paradasEstacionamento)
        {
            if (!vagasOcupadas.Contains(vaga))
            {
                vagaEscolhida = vaga;
                break;
            }
        }

        if (vagaEscolhida != null)
        {
            vagasOcupadas.Add(vagaEscolhida);
            veiculo.EmitirOrdemMover(vagaEscolhida.position);
            while (veiculo != null)
            {
                if (Vector3.Distance(veiculo.transform.position, vagaEscolhida.position) < 3.5f) break;
                yield return null;
            }

            if (veiculo != null)
            {
                if (danos != null) danos.Reparar(9999f);

                veiculo.transform.position = vagaEscolhida.position;
                veiculo.transform.rotation = vagaEscolhida.rotation;
                
                var agente = veiculo.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agente != null) agente.enabled = false;
                
                veiculo.DefinirModoCombate(false); 
                if (!veiculosNoQuartel.Contains(veiculo)) veiculosNoQuartel.Add(veiculo);
            }
        }
        else
        {
            if (danos != null) danos.Reparar(9999f);
            veiculo.gameObject.SetActive(false);
            if (!veiculosNoQuartel.Contains(veiculo)) veiculosNoQuartel.Add(veiculo);
        }
    }

    private void DesdobrarSoldados(int quantidade)
    {
        Vector3 pontoSaida = transform.position + (transform.forward * 15f);
        int liberados = 0;
        for (int i = soldadosNoDormitorio.Count - 1; i >= 0; i--)
        {
            if (liberados >= quantidade) break;
            ControleUnidade soldado = soldadosNoDormitorio[i];
            soldadosNoDormitorio.RemoveAt(i);
            
            if (soldado != null)
            {
                soldado.gameObject.SetActive(true);
                soldado.transform.position = transform.position + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                
                var danos = soldado.GetComponent<SistemaDeDanos>();
                if (danos != null) danos.Reparar(9999f); 

                soldado.EmitirOrdemMover(pontoSaida);
                if (administracao != null) administracao.RegistrarUnidadeDesdobrada(soldado);
                liberados++;
            }
        }
    }
    
    private void DesdobrarVeiculo(ControleUnidade veiculoEspecifico)
    {
        if (veiculoEspecifico != null && veiculosNoQuartel.Contains(veiculoEspecifico))
        {
            veiculosNoQuartel.Remove(veiculoEspecifico);
            veiculoEspecifico.gameObject.SetActive(true);
            
            var agente = veiculoEspecifico.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null)
            {
                agente.enabled = true;
                agente.Warp(veiculoEspecifico.transform.position);
            }
            
            veiculoEspecifico.DefinirModoCombate(true);
            
            foreach (Transform vaga in paradasEstacionamento)
            {
                if (Vector3.Distance(vaga.position, veiculoEspecifico.transform.position) < 2.5f)
                {
                    vagasOcupadas.Remove(vaga);
                    break;
                }
            }

            Vector3 pontoSaida = waypointsEntradaEstacionamento.Count > 0 ? waypointsEntradaEstacionamento[0].position : transform.position + (transform.forward * 20f);
            veiculoEspecifico.EmitirOrdemMover(pontoSaida);
            if (administracao != null) administracao.RegistrarUnidadeDesdobrada(veiculoEspecifico);
        }
    }
}
