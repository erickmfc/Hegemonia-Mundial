using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems; 
using System.Collections;
using System.Collections.Generic;

// [HELICÓPTERO TÁTICO V21.0 - SOLUÇÃO DEFINITIVA DE ROTAÇÃO E POUSO]
// - Fundido com a lógica de Navios/Porta-Aviões da outra IA (FinalizarPosicionamentoNaVagaAeroporto).
// - Rotação corrigida no MODELO VISUAL (filho) ao invés do pai, evitando conflito com a vaga do navio.
// - Trava de Altura no pouso: Impede que o helicóptero atravesse o casco do navio (nível do mar).

public class Helicoptero : MonoBehaviour
{
    public enum AjusteDoModelo3D
    {
        Nenhum_Modelo_Correto = 0,
        Girar_180_Graus_Costas = 180,
        Girar_90_Graus_Direita = 90,
        Girar_90_Graus_Esquerda = -90
    }

    public enum EixoRotacaoHelice
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    [Header("--- CORREÇÃO DO MODELO 3D ---")]
    [Tooltip("Se ele voar torto, troque as opções dessa lista até ele voar reto com o bico para frente!")]
    public AjusteDoModelo3D corrigirRotacao = AjusteDoModelo3D.Nenhum_Modelo_Correto;
    [Tooltip("Arraste o modelo visual (a carcaça 3D) aqui. Se deixar vazio, o script tenta achar sozinho.")]
    public Transform modeloVisual;

    [Header("--- DEBUG ---")]
    public bool debugLogs = false;

    [Header("--- CONTROLES ---")]
    public bool controleSempreAtivo = false; 

    [Header("--- DEBUG (Estado Atual) ---")]
    public bool selecionado = false;

    [Header("--- SENSIBILIDADE DO CLIQUE ---")]
    public float raioDoClique = 7.0f; 

    [Header("--- VOO ---")]
    public float altitudeDeVoo = 14f;       
    public float alturaPouso = 1.33f; 
    public float velocidadeHelice = 1200f;  
    public float velocidadeNavegacao = 20f; 
    public float velocidadePouso = 4f; 
    [Tooltip("Limita a velocidade vertical de subida para evitar efeito de 'disparo' para cima.")]
    public float velocidadeSubidaVertical = 6.5f;
    [Tooltip("Limita a velocidade vertical de descida durante pouso.")]
    public float velocidadeDescidaVertical = 5f;
    public float alturaSubidaInicial = 10f;
    public float ajusteAlturaEstacionado = 0f;
    [Range(0.12f, 0.55f)] public float reservaRetornoPercentual = 0.32f;
    [Tooltip("Distância horizontal em que o helicóptero começa a baixar de verdade para pouso.")]
    public float raioInicioDescida = 18f;
    
    [Header("--- TRANSPORTE (U / P) ---")]
    public float distanciaBusca = 50f; 
    public float distanciaEmbarque = 4.0f; 
    public int capacidadeMaxima = 8;
    public string tagAlvo = "Soldado"; 
    public List<GameObject> soldadosEmbarcados = new List<GameObject>();

    [Header("--- COMBATE & DEFESA (K / O) ---")]
    public bool modoCombateAtivo = false; 
    public float raioRadarMissil = 60f;
    public float cooldownFlares = 10f;
    public string tagMissil = "Missil";
    public string tagInimigo = "Inimigo"; 

    [Header("--- VISUAL E ÁUDIO ---")]
    public ParticleSystem[] flares;
    public Transform helicePrincipal;
    public Transform heliceTraseira;
    public EixoRotacaoHelice eixoRotacaoHeliceTraseira = EixoRotacaoHelice.X;
    [Tooltip("Use em helicópteros de duas hélices superiores, onde a traseira também deve girar no plano horizontal.")]
    public bool usarRotacaoHorizontalNaHeliceTraseira = false;
    public AudioSource audioMotor;
    public float pitchMinimo = 0.5f;
    public float pitchMaximo = 1.2f;
    public float volumeMaximo = 1.0f;
    public float tempoSpinUp = 4.0f; 
    public float tempoSpinDown = 6.0f; 

    [Header("--- DECOLAGEM ANIMADA ---")]
    public bool usarAnimacaoAntesDeVoar = false;
    public string nomeAnimacaoDecolagem = "Fly";
    public float tempoPreparacaoDecolagem = 1.1f;

    [Header("--- TRANSPORTE TATICO ---")]
    public bool helicopteroTransporte = false;
    public bool pousarNoDestinoTatico = false;
    public bool desembarcarAutomaticamenteAoPousar = true;
    public float raioTransferenciaNavio = 35f;
    [Tooltip("Mostra uma etiqueta acima do helicóptero com nome + ID único.")]
    public bool mostrarIdentificacaoFlutuante = true;

    // ESTADOS INTERNOS
    private float velocidadeAtualHelice = 0.0f; 
    public Vector3 destino;
    public bool estaVoando = false;
    private bool estaPousando = false;
    private bool motorLigado = false;
    private float timerInatividade = 0f;
    private float timerRecargaFlares = 0f;
    private Coroutine rotinaPousoAuto;
    private Coroutine rotinaPreparacaoDecolagem;
    private bool subidaInicialDecolagem = false;
    private Vector3 ancoraSubidaDecolagem;
    private bool preparandoDecolagem = false;
    private bool aplicarAltitudeCruzeiroNaDecolagem = true;
    private Animation animacaoDecolagem;
    private Transform alvoComandoAtaque;

    // Missao especial pausada durante o retorno para abastecimento. A rota e o
    // alvo ficam guardados para que o helicoptero volte ao mesmo trabalho depois
    // de pousar, em vez de ficar estacionado ou perder os pontos de patrulha.
    private readonly List<Vector3> rotaPatrulhaSalva = new List<Vector3>();
    private int indicePatrulhaSalva = 0;
    private bool retomarPatrulhaDepoisDeAbastecer = false;
    private Transform alvoSeguimentoSalvo;
    private float distanciaSeguimentoSalva = -1f;
    private bool retomarSeguimentoDepoisDeAbastecer = false;

    private ControleUnidade ObterControleUnidade()
    {
        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle == null) controle = GetComponentInParent<ControleUnidade>();
        if (controle == null) controle = GetComponentInChildren<ControleUnidade>(true);
        return controle;
    }

    private ComportamentoSeguirUniversal ObterComportamentoSeguir()
    {
        ComportamentoSeguirUniversal seguir = GetComponent<ComportamentoSeguirUniversal>();
        if (seguir == null) seguir = GetComponentInParent<ComportamentoSeguirUniversal>();
        if (seguir == null) seguir = GetComponentInChildren<ComportamentoSeguirUniversal>(true);
        return seguir;
    }

    // COMPATIBILIDADE EXTERNA
    [HideInInspector] public string nomeHelicoptero = "Falcão Negro"; 
    [HideInInspector] public int custoUpgrade = 800;  
    private bool disponivelParaPatrulha = true; 
    private IdentidadeUnidade identidade;
    private Rigidbody rb;
    private static int proximoIdExibicao = 1;
    private static readonly List<Helicoptero> _bufferHelicopterosConsulta = new List<Helicoptero>(32);
    [SerializeField, HideInInspector] private int idExibicao = 0;
    private readonly RaycastHit[] _bufferRaycastSolo = new RaycastHit[32];
    private float _cacheAlturaSolo = 0f;
    private float _proximaAtualizacaoAlturaSolo = 0f;
    private TextMesh _etiquetaFlutuante;
    private string _textoEtiquetaAtual = string.Empty;
    // Cache de renderers: evita GetComponentsInChildren toda chamada de ObterAlturaEstacionamentoTotal
    private Renderer[] _renderersCache;
    private bool _renderersCacheValido = false;
    private float _alturaEstacionamentoCache = -1f;
    private readonly EstadoOtimizacaoTatica estadoOtimizacao = new EstadoOtimizacaoTatica();

    void LogDebug(string msg) { if (debugLogs) Debug.Log(msg); }
    void OnEnable() { RegistroEntidadesJogo.Register(this); }
    void OnDisable() { RegistroEntidadesJogo.Unregister(this); }

    void Awake()
    {
        // O menu satelite e a trilha oficial de ordens usam ControleUnidade como
        // contrato comum. O proprio ControleUnidade detecta Helicoptero e evita NavMesh.
        if (GetComponent<ControleUnidade>() == null)
        {
            gameObject.AddComponent<ControleUnidade>();
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        selecionado = false;
        controleSempreAtivo = false; 
        
        if(flares != null)
        {
            foreach(var f in flares) if(f) { var m = f.main; m.playOnAwake = false; f.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        }

        GarantirIdentificacaoUnica();
        ConfigurarEtiquetaFlutuante();
    }

    void Start()
    {
        if (debugLogs) LogDebug($"🚁 SISTEMA DO HELICÓPTERO INICIADO: {name}");

        identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();

        destino = transform.position;
        
        // 1. ACHAR E CORRIGIR O MODELO VISUAL TORTO (Somente a carcaça)
        if (modeloVisual == null && transform.childCount > 0)
        {
            foreach (Transform child in transform)
            {
                string nomeFilho = child.name.ToLower();
                // Ignora barras de vida, canvas e as hélices
                if (!nomeFilho.Contains("canvas") && !nomeFilho.Contains("bar") && !nomeFilho.Contains("helice") && !nomeFilho.Contains("propel"))
                {
                    modeloVisual = child;
                    break;
                }
            }
        }

        if (modeloVisual != null && corrigirRotacao != AjusteDoModelo3D.Nenhum_Modelo_Correto)
        {
            // Gira o modelo visual uma única vez no início, para não brigar com a rotação do navio depois!
            modeloVisual.localRotation = modeloVisual.localRotation * Quaternion.Euler(0, (float)corrigirRotacao, 0);
        }

        // 2. Localiza as hélices automaticamente
        if(!helicePrincipal) helicePrincipal = transform.Find("helice_principal") ?? transform.Find("MainRotor") ?? EncontrarFilhoPorNomeParcial("front_rotor") ?? EncontrarFilhoPorNomeParcial("main_rotor") ?? EncontrarFilhoPorNomeParcial("propel") ?? EncontrarFilhoPorNomeParcial("helice");
        if(!heliceTraseira) heliceTraseira = transform.Find("helice_traseira") ?? transform.Find("TailRotor") ?? EncontrarFilhoPorNomeParcial("back_rotor") ?? EncontrarFilhoPorNomeParcial("tail_rotor") ?? EncontrarFilhoPorNomeParcial("tail");

        animacaoDecolagem = GetComponent<Animation>();
        if (!animacaoDecolagem) animacaoDecolagem = GetComponentInChildren<Animation>(true);
        if (animacaoDecolagem) animacaoDecolagem.playAutomatically = false;

        if(!audioMotor) audioMotor = GetComponent<AudioSource>();
        if(!audioMotor) audioMotor = GetComponentInChildren<AudioSource>(true);
        if(audioMotor)
        {
            audioMotor.loop = true;
            audioMotor.playOnAwake = false;
            audioMotor.spatialBlend = 1f;
            audioMotor.rolloffMode = AudioRolloffMode.Logarithmic;
            audioMotor.dopplerLevel = 0f;
            audioMotor.minDistance = 9f;
            audioMotor.maxDistance = 150f;
            audioMotor.rolloffMode = AudioRolloffMode.Linear;
            audioMotor.priority = Mathf.Min(audioMotor.priority, 48);
            audioMotor.volume = 0;
            audioMotor.pitch = pitchMinimo;
        }

        GarantirIdentificacaoUnica();
        ConfigurarEtiquetaFlutuante();

        // Cacheia altura de estacionamento no Start (helicóptero plano) para evitar oscilação quando o navio balança
        _renderersCache = GetComponentsInChildren<Renderer>(true);
        _renderersCacheValido = true;
        _alturaEstacionamentoCache = ObterAlturaEstacionamentoTotal();

        StartCoroutine(RadarDeAmeacas());
    }

    void Update()
    {
        long inicioUpdate = InfraPerformanceGameplay.MarcarInicioMedicao();
        AtualizarEstadoOtimizacao();
        if (timerRecargaFlares > 0) timerRecargaFlares -= Time.deltaTime;
        GestaoDeInput(); 
        float intervaloLogica = InfraPerformanceGameplay.ResolverIntervalo(0.20f, estadoOtimizacao, true, true);
        if (InfraPerformanceGameplay.DeveExecutar(this, ref estadoOtimizacao.proximoTickLogica, intervaloLogica))
        {
            long inicioLogica = InfraPerformanceGameplay.MarcarInicioMedicao();
            AvaliarRetornoSeguro();
            VerificarInatividade();
            AtualizarEtiquetaFlutuante();
            InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Logistica, inicioLogica);
        }
        if (estaVoando && !CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
        }
        AtualizarAlvoComandoAtaque();
        if (estaVoando) ProcessarMovimento();
        ControlarMotorEHelices();
        InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Aereo, inicioUpdate);
    }

    public void OrdenarAtaque(Transform alvo, Vector3 pontoFallback)
    {
        alvoComandoAtaque = alvo;
        modoCombateAtivo = true;
        Decolar(alvo != null ? alvo.position : pontoFallback);
    }

    /// <summary>
    /// Faz o helicoptero acompanhar uma estrutura ou unidade em movimento.
    /// O componente universal atualiza a posicao do alvo, portanto nao fica
    /// preso a uma coordenada antiga.
    /// </summary>
    public bool SeguirAlvoMovel(Transform alvo, float distancia = -1f)
    {
        if (alvo == null) return false;
        ControleUnidade controle = ObterControleUnidade();
        if (controle == null) return false;
        alvoComandoAtaque = alvo;
        modoCombateAtivo = true;
        return controle.EmitirOrdemSeguir(alvo, distancia);
    }

    private void AtualizarAlvoComandoAtaque()
    {
        if (alvoComandoAtaque == null)
        {
            return;
        }

        if (!alvoComandoAtaque.gameObject.activeInHierarchy)
        {
            alvoComandoAtaque = null;
            return;
        }

        destino = AjustarDestinoParaVoo(alvoComandoAtaque.position);
    }

    private void LateUpdate()
    {
        OrientarEtiquetaParaCamera();
    }

    void ControlarMotorEHelices()
    {
        float target = motorLigado ? 1.0f : 0.0f;
        float speed = motorLigado ? (1.0f / tempoSpinUp) : (1.0f / tempoSpinDown);
        
        velocidadeAtualHelice = Mathf.MoveTowards(velocidadeAtualHelice, target, speed * Time.deltaTime);

        float rotacao = velocidadeAtualHelice * velocidadeHelice * Time.deltaTime;
        if(helicePrincipal) helicePrincipal.Rotate(0, rotacao, 0);
        if (heliceTraseira)
        {
            if (usarRotacaoHorizontalNaHeliceTraseira)
            {
                heliceTraseira.Rotate(Vector3.up * rotacao, Space.World);
            }
            else
            {
                heliceTraseira.Rotate(ObterEixoRotacao(eixoRotacaoHeliceTraseira) * rotacao, Space.Self);
            }
        }

        if(audioMotor)
        {
            if(velocidadeAtualHelice > 0.01f)
            {
                if(audioMotor.enabled && audioMotor.gameObject.activeInHierarchy && !audioMotor.isPlaying) audioMotor.Play();
                audioMotor.volume = Mathf.Lerp(0, volumeMaximo, velocidadeAtualHelice);
                audioMotor.pitch = Mathf.Lerp(pitchMinimo, pitchMaximo, velocidadeAtualHelice);
            }
            else { if(audioMotor.isPlaying) audioMotor.Stop(); }
        }
    }

    void GestaoDeInput()
    {
        if (identidade != null && identidade.teamID != 1 && !controleSempreAtivo) return;
        if (Construtor.EmModoConstrucaoAtivo) return;
        bool capturaManualAtiva = CapturaCliqueOrdensManuais.EstaAtiva();

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (capturaManualAtiva) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    if (Vector3.Distance(hit.point, transform.position) <= raioDoClique) selecionado = true;
                    else selecionado = false;
                }
                else if (selecionado) selecionado = false;
            }
            else if (selecionado) selecionado = false;
        }

        if (capturaManualAtiva) return;
        if (!selecionado) return;

        if (Input.GetMouseButtonDown(1))
        {
            Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(r, out RaycastHit h, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) 
            {
                string rootName = h.transform.root.name.ToLower();
                string colName = h.collider.name.ToLower();
                
                bool clicouEmBase = h.collider.GetComponentInParent<Heliporto>() != null || 
                                    colName.Contains("pouso") || colName.Contains("vaga") || colName.Contains("deck") ||
                                    rootName.Contains("navio") || rootName.Contains("porta") || rootName.Contains("aeroporto");

                bool pousarNoCliqueDireto = clicouEmBase || (pousarNoDestinoTatico && EhHelicopteroTransporte());

                if (pousarNoCliqueDireto) VoarEPousar(h.point);
                else
                {
                    Decolar(h.point);
                    if (rotinaPousoAuto != null) StopCoroutine(rotinaPousoAuto);
                }
            }
        }

        if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;

        if (Input.GetKeyDown(KeyCode.I)) 
        {
            if (estaVoando) { estaPousando = true; destino = transform.position; if (rotinaPousoAuto != null) StopCoroutine(rotinaPousoAuto); }
            else ChamarReforcos();
        }
        
        if (Input.GetKeyDown(KeyCode.P)) OrdemPousoOuDesembarque();
        if (Input.GetKeyDown(KeyCode.O)) DispararFlaresManual(); 
    }

    private Transform EncontrarFilhoPorNomeParcial(string trechoNome)
    {
        if (string.IsNullOrEmpty(trechoNome)) return null;
        string trechoNormalizado = trechoNome.ToLowerInvariant();
        foreach (Transform filho in GetComponentsInChildren<Transform>(true))
        {
            if (filho == transform) continue;
            if (filho.name.ToLowerInvariant().Contains(trechoNormalizado)) return filho;
        }
        return null;
    }

    private void GarantirIdentificacaoUnica()
    {
        if (idExibicao != 0) return;
        idExibicao = proximoIdExibicao++;
    }

    public string ObterIdentificacaoCurta()
    {
        return $"#{Mathf.Max(1, idExibicao):00}";
    }

    public string ObterRotuloExibicao()
    {
        string baseNome = string.IsNullOrWhiteSpace(nomeHelicoptero) ? LimparNomeExibicao(name) : nomeHelicoptero.Trim();
        return $"{baseNome} {ObterIdentificacaoCurta()}";
    }

    private static string LimparNomeExibicao(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return texto.Replace("(Clone)", "").Trim();
    }

    private void ConfigurarEtiquetaFlutuante()
    {
        if (!mostrarIdentificacaoFlutuante) return;

        if (_etiquetaFlutuante == null)
        {
            Transform existente = transform.Find("IdentificacaoHelicoptero");
            if (existente != null)
            {
                _etiquetaFlutuante = existente.GetComponent<TextMesh>();
            }
        }

        if (_etiquetaFlutuante == null)
        {
            GameObject etiqueta = new GameObject("IdentificacaoHelicoptero");
            etiqueta.transform.SetParent(transform, false);
            etiqueta.transform.localPosition = new Vector3(0f, 5.6f, 0f);
            etiqueta.transform.localScale = Vector3.one * 0.1f;

            _etiquetaFlutuante = etiqueta.AddComponent<TextMesh>();
            _etiquetaFlutuante.anchor = TextAnchor.MiddleCenter;
            _etiquetaFlutuante.alignment = TextAlignment.Center;
            _etiquetaFlutuante.characterSize = 0.2f;
            _etiquetaFlutuante.fontSize = 48;
            _etiquetaFlutuante.color = Color.white;
            _etiquetaFlutuante.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Renderer rend = etiqueta.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Sprites/Default"));
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }
        else if (_etiquetaFlutuante.font == null)
        {
            _etiquetaFlutuante.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        AtualizarTextoEtiqueta();
    }

    private void AtualizarTextoEtiqueta()
    {
        if (_etiquetaFlutuante == null) return;

        string texto = ObterRotuloExibicao();
        if (_textoEtiquetaAtual == texto) return;
        _textoEtiquetaAtual = texto;
        _etiquetaFlutuante.text = texto;
    }

    private void AtualizarEtiquetaFlutuante()
    {
        if (!mostrarIdentificacaoFlutuante)
        {
            if (_etiquetaFlutuante != null)
            {
                _etiquetaFlutuante.gameObject.SetActive(false);
            }
            return;
        }

        if (_etiquetaFlutuante == null)
        {
            ConfigurarEtiquetaFlutuante();
            return;
        }

        if (!_etiquetaFlutuante.gameObject.activeSelf)
        {
            _etiquetaFlutuante.gameObject.SetActive(true);
        }

        AtualizarTextoEtiqueta();
    }

    private void OrientarEtiquetaParaCamera()
    {
        if (_etiquetaFlutuante == null || Camera.main == null) return;

        Vector3 direcao = _etiquetaFlutuante.transform.position - Camera.main.transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude > 0.0001f)
        {
            _etiquetaFlutuante.transform.rotation = Quaternion.LookRotation(direcao);
        }
    }

    IEnumerator RadarDeAmeacas()
    {
        Collider[] buffer = new Collider[48];
        while (true)
        {
            if (estaVoando && modoCombateAtivo && timerRecargaFlares <= 0)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(transform.position, raioRadarMissil, buffer, ~0, QueryTriggerInteraction.UseGlobal);
                for (int i = 0; i < hitCount; i++)
                {
                    if (buffer[i] != null && (SafeCompareTag(buffer[i], tagMissil) || buffer[i].name.IndexOf("missil", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        DispararFlaresManual();
                        break; 
                    }
                }
            }
            float espera = InfraPerformanceGameplay.ResolverIntervalo(0.50f, estadoOtimizacao, true, true);
            yield return new WaitForSeconds(espera);
        }
    }

    private void AtualizarEstadoOtimizacao()
    {
        bool engajado = estaVoando || preparandoDecolagem || estaPousando || missaoAtualAeroporto != 0;
        InfraPerformanceGameplay.AtualizarEstadoBase(estadoOtimizacao, transform, selecionado, engajado, true, 160f, 360f);
    }

    private static bool SafeCompareTag(Component component, string tagName)
    {
        try { return component != null && string.Equals(component.tag, tagName, System.StringComparison.Ordinal); }
        catch { return false; }
    }

    private static bool VetorValido(Vector3 valor)
    {
        return !(float.IsNaN(valor.x) || float.IsNaN(valor.y) || float.IsNaN(valor.z) ||
                 float.IsInfinity(valor.x) || float.IsInfinity(valor.y) || float.IsInfinity(valor.z));
    }

    private static Vector3 ObterEixoRotacao(EixoRotacaoHelice eixo)
    {
        switch (eixo)
        {
            case EixoRotacaoHelice.Y:
                return Vector3.up;
            case EixoRotacaoHelice.Z:
                return Vector3.forward;
            default:
                return Vector3.right;
        }
    }

    private bool ColliderEhSuperficiePreferencial(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (collider is TerrainCollider)
        {
            return true;
        }

        Transform alvo = collider.transform;
        return alvo.GetComponentInParent<Heliporto>() != null
            || alvo.GetComponentInParent<NavioTransporteTropas>() != null
            || alvo.GetComponentInParent<GerenciadorAeroporto>() != null
            || alvo.GetComponentInParent<GerenciadorPortaAvioes>() != null;
    }

    private bool ColliderContaComoSuperficie(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform raiz = collider.transform.root;
        if (raiz == transform.root)
        {
            return false;
        }

        bool superficiePreferencial = ColliderEhSuperficiePreferencial(collider);
        if (!superficiePreferencial)
        {
            if (raiz.GetComponentInParent<ControleUnidade>() != null)
            {
                return false;
            }

            if (raiz.GetComponentInParent<NavMeshAgent>() != null)
            {
                return false;
            }

            if (raiz.GetComponentInParent<IdentidadeUnidade>() != null)
            {
                return false;
            }
        }

        if (raiz.GetComponentInParent<Helicoptero>() != null)
        {
            return false;
        }

        if (raiz.GetComponentInParent<ControleAviao>() != null)
        {
            return false;
        }

        if (raiz.GetComponentInParent<C700TransporteAereo>() != null)
        {
            return false;
        }

        return true;
    }

    private bool DestinoCorrespondeAVagaAeroporto(Vector3 ponto)
    {
        if (vagaAeroporto == null)
        {
            return false;
        }

        Vector2 vagaXZ = new Vector2(vagaAeroporto.position.x, vagaAeroporto.position.z);
        Vector2 pontoXZ = new Vector2(ponto.x, ponto.z);
        return Vector2.Distance(vagaXZ, pontoXZ) <= 6f;
    }

    private float ObterAlturaSoloNoPonto(Vector3 ponto)
    {
        float altura = 0f;
        bool encontrou = false;

        Vector3 origem = new Vector3(ponto.x, 1200f, ponto.z);
        int hits = Physics.RaycastNonAlloc(
            origem,
            Vector3.down,
            _bufferRaycastSolo,
            2500f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits; i++)
        {
            RaycastHit hit = _bufferRaycastSolo[i];
            if (!ColliderContaComoSuperficie(hit.collider)) continue;

            if (!encontrou || hit.point.y > altura)
            {
                altura = hit.point.y;
                encontrou = true;
            }
        }

        return encontrou ? altura : 0f;
    }

    private Vector3 AjustarDestinoParaPouso(Vector3 alvo)
    {
        if (DestinoCorrespondeAVagaAeroporto(alvo))
        {
            return ObterPosicaoEstacionadaNaVaga(vagaAeroporto);
        }

        alvo.y = ObterAlturaSoloNoPonto(alvo);
        return alvo;
    }

    private Vector3 AjustarDestinoParaVoo(Vector3 alvo)
    {
        alvo.y = Mathf.Max(altitudeDeVoo, ObterAlturaSoloNoPonto(alvo) + altitudeDeVoo);
        return alvo;
    }

    private float ObterAlturaCruzeiroNoPonto(Vector3 ponto)
    {
        return Mathf.Max(altitudeDeVoo, ObterAlturaSoloNoPonto(ponto) + altitudeDeVoo);
    }

    private float ObterAlturaFinalDePouso(Vector3 ponto)
    {
        if (DestinoCorrespondeAVagaAeroporto(ponto))
        {
            return ObterPosicaoEstacionadaNaVaga(vagaAeroporto).y;
        }

        return ObterAlturaSoloNoPonto(ponto) + ObterAlturaEstacionamentoTotal();
    }

    public float ObterAlturaEstacionamentoTotal()
    {
        // Se já cacheamos a altura no Start, usa o valor fixo para evitar oscilação quando o navio balança
        if (_alturaEstacionamentoCache > 0f)
        {
            return _alturaEstacionamentoCache;
        }

        float alturaBase = Mathf.Max(0.05f, alturaPouso + Mathf.Max(0f, ajusteAlturaEstacionado));
        // OTIMIZAÇÃO: usa cache de renderers (calculado no Start) em vez de GetComponentsInChildren repetido
        if (!_renderersCacheValido)
        {
            _renderersCache = GetComponentsInChildren<Renderer>(true);
            _renderersCacheValido = true;
        }
        Renderer[] renderizadores = _renderersCache;
        if (renderizadores == null || renderizadores.Length == 0)
        {
            return alturaBase;
        }

        bool possuiBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        for (int i = 0; i < renderizadores.Length; i++)
        {
            Renderer renderizador = renderizadores[i];
            if (renderizador == null)
            {
                continue;
            }

            if (!possuiBounds)
            {
                bounds = renderizador.bounds;
                possuiBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderizador.bounds);
            }
        }

        if (!possuiBounds)
        {
            return alturaBase;
        }

        float deslocamentoBase = transform.position.y - bounds.min.y + 0.05f;
        if (float.IsNaN(deslocamentoBase) || float.IsInfinity(deslocamentoBase))
        {
            return alturaBase;
        }

        return Mathf.Max(alturaBase, deslocamentoBase);
    }

    public Vector3 ObterPosicaoEstacionadaNaVaga(Transform vaga)
    {
        if (vaga == null)
        {
            return transform.position;
        }

        return vaga.position + (vaga.up * ObterAlturaEstacionamentoTotal());
    }

    private bool DeveUsarPreparacaoAnimada()
    {
        return usarAnimacaoAntesDeVoar && animacaoDecolagem != null && tempoPreparacaoDecolagem > 0.01f;
    }

    private void TocarAnimacaoDecolagem()
    {
        if (animacaoDecolagem == null)
        {
            return;
        }

        animacaoDecolagem.Stop();

        AnimationClip clip = null;
        if (!string.IsNullOrWhiteSpace(nomeAnimacaoDecolagem))
        {
            clip = animacaoDecolagem.GetClip(nomeAnimacaoDecolagem);
        }
        if (clip == null)
        {
            clip = animacaoDecolagem.clip;
        }
        if (clip == null)
        {
            return;
        }

        AnimationState estado = animacaoDecolagem[clip.name];
        if (estado != null)
        {
            estado.wrapMode = WrapMode.Once;
            estado.time = 0f;
            estado.speed = 1f;
        }

        animacaoDecolagem.Play(clip.name, PlayMode.StopAll);
    }

    private void PararAnimacaoDecolagem()
    {
        if (animacaoDecolagem != null && animacaoDecolagem.isPlaying)
        {
            animacaoDecolagem.Stop();
        }
    }

    private void IniciarVooAposPreparacao()
    {
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        preparandoDecolagem = false;
        estaVoando = true;
        subidaInicialDecolagem = true;
        ancoraSubidaDecolagem = transform.position;

        if (aplicarAltitudeCruzeiroNaDecolagem && destino.y < altitudeDeVoo)
        {
            destino.y = altitudeDeVoo;
        }

        // Notificar heliportos sobre a decolagem
        Heliporto[] heliportos = Object.FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
        foreach (var h in heliportos)
        {
            if (h != null) h.HelicopteroDecolou(this);
        }
    }

    private IEnumerator RotinaPreparacaoDecolagem()
    {
        preparandoDecolagem = true;
        TocarAnimacaoDecolagem();

        float espera = Mathf.Max(0.05f, tempoPreparacaoDecolagem);
        float tempo = 0f;
        while (tempo < espera)
        {
            if (estaPousando)
            {
                preparandoDecolagem = false;
                rotinaPreparacaoDecolagem = null;
                PararAnimacaoDecolagem();
                yield break;
            }

            tempo += Time.deltaTime;
            yield return null;
        }

        rotinaPreparacaoDecolagem = null;
        PararAnimacaoDecolagem();
        IniciarVooAposPreparacao();
    }

    public void Decolar(Vector3 novoDestino)
    {
        Decolar(novoDestino, true);
    }

    public void Decolar(Vector3 novoDestino, bool limitarAltitudeCruzeiro)
    {
        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        bool estavaEstacionado = !estaVoando && !preparandoDecolagem;
        aplicarAltitudeCruzeiroNaDecolagem = limitarAltitudeCruzeiro;
        destino = novoDestino;
        estaPousando = false;
        motorLigado = true;
        timerInatividade = 0f;
        disponivelParaPatrulha = false;
        estacionadoNoAeroporto = false;
        if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (limitarAltitudeCruzeiro && destino.y < altitudeDeVoo)
        {
            destino.y = altitudeDeVoo;
        }

        if (preparandoDecolagem)
        {
            return;
        }

        if (estavaEstacionado)
        {
            if (DeveUsarPreparacaoAnimada())
            {
                if (rotinaPreparacaoDecolagem != null)
                {
                    StopCoroutine(rotinaPreparacaoDecolagem);
                }
                rotinaPreparacaoDecolagem = StartCoroutine(RotinaPreparacaoDecolagem());
                return;
            }

            IniciarVooAposPreparacao();
        }
    }

    public void VoarEPousar(Vector3 alvo)
    {
        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        Vector3 alvoPouso = AjustarDestinoParaPouso(alvo);
        Decolar(alvoPouso, false);
        if (rotinaPousoAuto != null) StopCoroutine(rotinaPousoAuto);
        rotinaPousoAuto = StartCoroutine(RotinaVoarEPousar(alvoPouso));
    }

    public void PararPorFaltaDeCombustivel()
    {
        if (rotinaPreparacaoDecolagem != null)
        {
            StopCoroutine(rotinaPreparacaoDecolagem);
            rotinaPreparacaoDecolagem = null;
        }

        if (rotinaPousoAuto != null)
        {
            StopCoroutine(rotinaPousoAuto);
            rotinaPousoAuto = null;
        }

        bool emFalhaNoAr = (estaVoando || preparandoDecolagem) && transform.position.y > Mathf.Max(alturaPouso + 1.5f, 4f);

        estaVoando = false;
        estaPousando = false;
        preparandoDecolagem = false;
        subidaInicialDecolagem = false;
        motorLigado = false;
        destino = transform.position;
        PararAnimacaoDecolagem();

        if (emFalhaNoAr)
        {
            FalhaAereaFisica.Ativar(gameObject, rb, Mathf.Max(velocidadeNavegacao, velocidadePouso) * 0.85f, 4f, true);
        }
    }

    private IEnumerator RotinaVoarEPousar(Vector3 alvo)
    {
        float distanciaParaIniciarDescida = Mathf.Max(2.5f, raioInicioDescida);
        while (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(alvo.x, alvo.z)) > distanciaParaIniciarDescida)
        {
            if (!estaVoando && !preparandoDecolagem) yield break;
            yield return null;
        }
        estaPousando = true;
        destino = alvo;
    }

    void ProcessarMovimento()
    {
        if (Time.time >= _proximaAtualizacaoAlturaSolo)
        {
            _cacheAlturaSolo = ObterAlturaSoloNoPonto(transform.position);
            _proximaAtualizacaoAlturaSolo = Time.time + 0.08f;
        }

        Vector3 posAtual = transform.position;
        Vector2 posAtualXZ = new Vector2(posAtual.x, posAtual.z);
        Vector2 destinoXZ = new Vector2(destino.x, destino.z);
        float distanciaHorizontal = Vector2.Distance(posAtualXZ, destinoXZ);

        float alturaCruzeiroAtual = Mathf.Max(altitudeDeVoo, _cacheAlturaSolo + altitudeDeVoo);
        float alturaPousoFinal = Mathf.Max(destino.y, ObterAlturaFinalDePouso(destino));
        float alturaDesejada = alturaCruzeiroAtual;

        if (!estaPousando && subidaInicialDecolagem)
        {
            float alturaSubidaMeta = Mathf.Max(alturaCruzeiroAtual, ancoraSubidaDecolagem.y + Mathf.Max(2f, alturaSubidaInicial));
            alturaDesejada = alturaSubidaMeta;

            if (posAtual.y >= alturaSubidaMeta - 0.15f || distanciaHorizontal > 5f)
            {
                subidaInicialDecolagem = false;
            }
        }
        else if (estaPousando)
        {
            float tDescida = Mathf.Clamp01(distanciaHorizontal / Mathf.Max(4f, raioInicioDescida));
            alturaDesejada = Mathf.Lerp(alturaPousoFinal, alturaCruzeiroAtual, tDescida);
        }

        float velocidadeHorizontal = estaPousando ? velocidadePouso : velocidadeNavegacao;

        Vector3 posHorizontalAtual = new Vector3(posAtual.x, 0f, posAtual.z);
        Vector3 posHorizontalMeta = new Vector3(destino.x, 0f, destino.z);
        Vector3 novoHorizontal = Vector3.MoveTowards(posHorizontalAtual, posHorizontalMeta, velocidadeHorizontal * Time.deltaTime);

        float velocidadeVertical = posAtual.y > alturaDesejada ? velocidadeDescidaVertical : velocidadeSubidaVertical;
        if (estaPousando && alturaDesejada <= posAtual.y)
        {
            velocidadeVertical = velocidadeDescidaVertical;
        }

        float novoY = Mathf.MoveTowards(posAtual.y, alturaDesejada, velocidadeVertical * Time.deltaTime);
        transform.position = new Vector3(novoHorizontal.x, novoY, novoHorizontal.z);

        Vector3 direcaoHorizontal = new Vector3(destino.x - transform.position.x, 0f, destino.z - transform.position.z);
        if (direcaoHorizontal.sqrMagnitude > 0.5f)
        {
            direcaoHorizontal.Normalize();
            Quaternion rotacaoFrente = Quaternion.LookRotation(direcaoHorizontal);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoFrente, Time.deltaTime * 5f);
        }

        float distanciaHorizontalFinal = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            destinoXZ
        );

        if (estaPousando && distanciaHorizontalFinal <= 1.35f && Mathf.Abs(transform.position.y - alturaPousoFinal) < 0.2f)
        {
            Vector3 pos = transform.position;
            pos.x = destino.x;
            pos.z = destino.z;
            pos.y = alturaPousoFinal;
            transform.position = pos;
            estaVoando = false;
            estaPousando = false;
            subidaInicialDecolagem = false;
            motorLigado = false; 

            // Se pousou perto de um Heliporto, registra nele
            Heliporto[] heliportos = Object.FindObjectsByType<Heliporto>(FindObjectsSortMode.None);
            foreach (var h in heliportos)
            {
                if (h != null && Vector3.Distance(new Vector3(h.ObterPontoDePousoMundial().x, 0, h.ObterPontoDePousoMundial().z), new Vector3(pos.x, 0, pos.z)) <= 6f)
                {
                    h.HelicopteroPousou(this);
                    break;
                }
            }

            bool pousouEmVagaRegistrada = DestinoCorrespondeAVagaAeroporto(destino);
            
            if (pousouEmVagaRegistrada && vagaAeroporto != null)
            {
                // Agora não exigimos ser um porta-aviões, qualquer vaga registrada (como de aeroporto/heliporto) serve.
                CombustivelUnidade comb = GetComponent<CombustivelUnidade>();
                if (comb != null) comb.PreencherSemCusto();

                SistemaDeDanos dano = GetComponent<SistemaDeDanos>();
                if (dano != null) dano.vidaAtual = dano.vidaMaxima;

                // Recarrega todos os sistemas de armas que possam estar acoplados no helicóptero
                foreach (var lancador in GetComponentsInChildren<LancadorMisseis>())
                {
                    lancador.municaoAtual = lancador.municaoMaxima;
                }
                foreach (var lancadorCaca in GetComponentsInChildren<LancadorMisselCaca>())
                {
                    lancadorCaca.RecarregarCompletoNaBase();
                }
                foreach (var torreta in GetComponentsInChildren<ControleTorretaModular>())
                {
                    foreach (var arma in torreta.armas)
                    {
                        if (arma != null) arma.municaoAtual = arma.tamanhoCartucho;
                    }
                }

                foreach (GameObject s in soldadosEmbarcados)
                {
                    if (s == null) continue;
                    SistemaDeDanos sd = s.GetComponent<SistemaDeDanos>();
                    if (sd != null) sd.vidaAtual = sd.vidaMaxima;
                    CombustivelUnidade sc = s.GetComponent<CombustivelUnidade>();
                    if (sc != null) sc.PreencherSemCusto();
                }
            }
            if(soldadosEmbarcados.Count > 0 && desembarcarAutomaticamenteAoPousar && !pousouEmVagaRegistrada) EjetarTodos();
            disponivelParaPatrulha = true; 
            if (pousouEmVagaRegistrada)
            {
                RetomarMissaoDepoisDoReabastecimento();
            }
        }

        // 4. LÓGICA DE PATRULHA
        if (!estaPousando && missaoAtualAeroporto == 3 && rotaPatrulhaAeroporto.Count > 1)
        {
            Vector3 alvoPatrulha = new Vector3(destino.x, transform.position.y, destino.z);
            Vector3 posPatrulhaAtual = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            if (Vector3.Distance(posPatrulhaAtual, alvoPatrulha) <= 2.5f)
            {
                indicePatrulhaAeroporto = (indicePatrulhaAeroporto + 1) % rotaPatrulhaAeroporto.Count;
                Vector3 proximoPonto = rotaPatrulhaAeroporto[indicePatrulhaAeroporto];
                proximoPonto.y = Mathf.Max(altitudeDeVoo, proximoPonto.y);
                destino = proximoPonto;
            }
        }
    }

    void VerificarInatividade() { if (!estaVoando && motorLigado) { timerInatividade += Time.deltaTime; if (timerInatividade > 10f) motorLigado = false; } }

    private List<GameObject> soldadosChamados = new List<GameObject>();
    public static bool SoldadoEstaEmbarcando(GameObject s)
    {
        if (s == null)
        {
            return false;
        }

        _bufferHelicopterosConsulta.Clear();
        RegistroEntidadesJogo.FillHelicopteros(_bufferHelicopterosConsulta);

        for (int i = 0; i < _bufferHelicopterosConsulta.Count; i++)
        {
            Helicoptero h = _bufferHelicopterosConsulta[i];
            if (h != null && h.soldadosChamados.Contains(s))
            {
                return true;
            }
        }

        return false;
    }

    public int ChamarReforcos()
    {
        if (estaVoando && !estaPousando) return 0;
        soldadosChamados.RemoveAll(s => s == null || !s.activeInHierarchy || soldadosEmbarcados.Contains(s));
        int espacoLivre = capacidadeMaxima - (soldadosEmbarcados.Count + soldadosChamados.Count);
        if(espacoLivre <= 0) return 0;

        Collider[] hits = Physics.OverlapSphere(transform.position, distanciaBusca * 3.0f);
        int chamados = 0;

        foreach(var h in hits)
        {
            var nav = h.GetComponentInParent<NavMeshAgent>();
            if (nav == null) continue;
            GameObject s = nav.gameObject;
            if(s == gameObject || soldadosEmbarcados.Contains(s) || soldadosChamados.Contains(s)) continue;

            IdentidadeUnidade idSoldado = s.GetComponent<IdentidadeUnidade>();
            if (idSoldado == null) idSoldado = s.GetComponentInChildren<IdentidadeUnidade>();
            if (idSoldado != null && identidade != null && idSoldado.teamID > 0 && identidade.teamID > 0 && idSoldado.teamID != identidade.teamID) continue; 

            bool tagCorreta = TagSafe.Matches(s, tagAlvo);
            string nm = s.name;
            if(!tagCorreta && (nm.IndexOf("soldado", System.StringComparison.OrdinalIgnoreCase) >= 0 || nm.IndexOf("infant", System.StringComparison.OrdinalIgnoreCase) >= 0)) tagCorreta = true;

            if(!tagCorreta) continue;

            soldadosChamados.Add(s);
            StartCoroutine(RotinaEmbarque(s, nav));
            espacoLivre--;
            chamados++;
            if (espacoLivre <= 0) break; 
        }

        return chamados;
    }

    IEnumerator RotinaEmbarque(GameObject s, NavMeshAgent nav)
    {
        if(s == null || nav == null) yield break;
        if (nav.isOnNavMesh) nav.isStopped = false; 
        nav.speed = 12f; 

        Vector3 destinoChao = new Vector3(transform.position.x, s.transform.position.y, transform.position.z);
        if (NavMesh.SamplePosition(destinoChao, out NavMeshHit hitM, 20f, NavMesh.AllAreas)) destinoChao = hitM.position;
        if (nav.isOnNavMesh) nav.SetDestination(destinoChao);

        float timeout = 25.0f; float timer = 0f; float proxAtualizacao = 0f;

        while(s != null && s.activeInHierarchy && timer < timeout)
        {
            if (estaVoando && !estaPousando) break;
            timer += Time.deltaTime;

            if (timer >= proxAtualizacao)
            {
                 destinoChao = new Vector3(transform.position.x, s.transform.position.y, transform.position.z);
                 if (NavMesh.SamplePosition(destinoChao, out NavMeshHit pNovo, 20f, NavMesh.AllAreas)) destinoChao = pNovo.position;
                 if (nav.isOnNavMesh) nav.SetDestination(destinoChao);
                 proxAtualizacao = timer + 1.0f;
            }

            float distHorizontal = Vector2.Distance(new Vector2(s.transform.position.x, s.transform.position.z), new Vector2(transform.position.x, transform.position.z));
            if(distHorizontal <= distanciaEmbarque || (distHorizontal < distanciaEmbarque * 2.0f && nav.velocity.sqrMagnitude < 0.1f && timer > 2.0f)) break;
            yield return null; 
        }

        if(s != null && soldadosEmbarcados.Count < capacidadeMaxima)
        {
             float distFinal = Vector2.Distance(new Vector2(s.transform.position.x, s.transform.position.z), new Vector2(transform.position.x, transform.position.z));
            if (distFinal <= 15f && soldadosEmbarcados.Count < capacidadeMaxima && (!estaVoando || estaPousando))
            {
                soldadosEmbarcados.Add(s);
                EsconderSoldado(s); 
            }
        }
        if (soldadosChamados.Contains(s)) soldadosChamados.Remove(s);
    }

    public void OrdemPousoOuDesembarque()
    {
        if(estaVoando) { estaPousando = true; destino = transform.position; if (rotinaPousoAuto != null) StopCoroutine(rotinaPousoAuto); }
        else if(soldadosEmbarcados.Count > 0) EjetarTodos();
    }

    private void EsconderSoldado(GameObject s)
    {
        if (s == null) return;
        if (!s.activeSelf) s.SetActive(true);
        NavMeshAgent nav = s.GetComponent<NavMeshAgent>();
        if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh) { nav.isStopped = true; nav.ResetPath(); }
        foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var c in s.GetComponentsInChildren<Collider>(true))  c.enabled = false;
        foreach (var mb in s.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) mb.enabled = false;
        s.transform.SetParent(transform, true);
    }

    private void MostrarSoldado(GameObject s, Vector3 posicao)
    {
        if (s == null) return;
        s.transform.SetParent(null, true);
        if (!s.activeSelf) s.SetActive(true);
        foreach (var mb in s.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) mb.enabled = true;
        foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        foreach (var c in s.GetComponentsInChildren<Collider>(true))  c.enabled = true;

        if (!VetorValido(posicao))
        {
            Vector3 fallback = VetorValido(transform.position) ? transform.position + transform.right * 6f : Vector3.zero;
            if (NavMesh.SamplePosition(fallback, out NavMeshHit hitFallback, 30f, NavMesh.AllAreas))
            {
                posicao = hitFallback.position;
            }
            else
            {
                posicao = fallback;
            }
        }

        NavMeshAgent nav = s.GetComponent<NavMeshAgent>();
        if (nav != null && nav.isActiveAndEnabled)
        {
            if (nav.isOnNavMesh) { nav.Warp(posicao); nav.isStopped = false; }
            else { s.transform.position = posicao; nav.isStopped = false; }
        }
        else s.transform.position = posicao;
    }

    void EjetarTodos()
    {
        int totalSoldados = soldadosEmbarcados.Count;
        for (int i = 0; i < totalSoldados; i++)
        {
            GameObject s = soldadosEmbarcados[i];
            if (s == null) continue;
            float angulo = i * (360f / Mathf.Max(1, totalSoldados));
            Vector3 posDesejada = transform.position + Quaternion.Euler(0, angulo, 0) * (transform.right * 6f);
            if (!VetorValido(posDesejada))
            {
                posDesejada = VetorValido(transform.position) ? transform.position : Vector3.zero;
            }
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(posDesejada, out hit, 20f, NavMesh.AllAreas))
            {
                posDesejada.y = Mathf.Max(0f, transform.position.y - alturaPouso);
                NavMesh.SamplePosition(posDesejada, out hit, 50f, NavMesh.AllAreas);
            }
            MostrarSoldado(s, hit.position != Vector3.zero ? hit.position : posDesejada);
        }
        soldadosEmbarcados.Clear();
    }

    public bool EmbarcarSoldadoTransferido(GameObject soldado)
    {
        if (soldado == null || soldadosEmbarcados.Contains(soldado) || soldadosEmbarcados.Count >= capacidadeMaxima)
        {
            return false;
        }

        soldadosChamados.Remove(soldado);
        EsconderSoldado(soldado);
        soldadosEmbarcados.Add(soldado);
        return true;
    }

    public int TransferirSoldadosParaNavio(NavioTransporteTropas navio, int quantidadeMax = int.MaxValue)
    {
        if (navio == null || quantidadeMax <= 0 || !PodeOperarTropasNoMenu())
        {
            return 0;
        }

        int transferidos = 0;
        for (int i = soldadosEmbarcados.Count - 1; i >= 0 && transferidos < quantidadeMax; i--)
        {
            GameObject soldado = soldadosEmbarcados[i];
            if (soldado == null)
            {
                soldadosEmbarcados.RemoveAt(i);
                continue;
            }

            if (!navio.EmbarcarSoldadoTransferidoDoHelicoptero(soldado))
            {
                continue;
            }

            soldadosEmbarcados.RemoveAt(i);
            transferidos++;
        }

        return transferidos;
    }

    private bool PertenceAoMesmoTime(Component componente)
    {
        if (componente == null || identidade == null || identidade.teamID <= 0)
        {
            return true;
        }

        IdentidadeUnidade identidadeOutro = componente.GetComponent<IdentidadeUnidade>();
        if (identidadeOutro == null) identidadeOutro = componente.GetComponentInParent<IdentidadeUnidade>();
        return identidadeOutro == null || identidadeOutro.teamID <= 0 || identidadeOutro.teamID == identidade.teamID;
    }

    private NavioTransporteTropas EncontrarNavioTransporteAliadoProximo()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, raioTransferenciaNavio, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        HashSet<int> vistos = new HashSet<int>();

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            NavioTransporteTropas navio = hits[i].GetComponentInParent<NavioTransporteTropas>();
            if (navio == null) continue;
            int id = navio.GetInstanceID();
            if (vistos.Contains(id)) continue;
            vistos.Add(id);

            if (!PertenceAoMesmoTime(navio)) continue;
            return navio;
        }

        return null;
    }

    public int RecolherTropasPeloMenu()
    {
        if (!PodeOperarTropasNoMenu())
        {
            return 0;
        }

        int transferidosDoNavio = 0;
        NavioTransporteTropas navio = EncontrarNavioTransporteAliadoProximo();
        if (navio != null && TemEspaco() > 0)
        {
            transferidosDoNavio = navio.TransferirSoldadosParaHelicoptero(this, TemEspaco());
        }

        int chamadosDoSolo = ChamarReforcos();
        return transferidosDoNavio + chamadosDoSolo;
    }

    public int DesembarcarTropasNoLocalAtual()
    {
        if (!PodeOperarTropasNoMenu() || soldadosEmbarcados.Count <= 0)
        {
            return 0;
        }

        int total = soldadosEmbarcados.Count;
        EjetarTodos();
        return total;
    }

    void DispararFlaresManual() { if(flares != null && flares.Length > 0) { timerRecargaFlares = cooldownFlares; foreach(var f in flares) if(f) f.Play(); Invoke("PararFlares", 4f); } }
    void PararFlares() { if(flares != null) foreach(var f in flares) if(f) f.Stop(); }
    void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, raioDoClique); }

    public bool EstaDisponivel() { return disponivelParaPatrulha && !estaVoando && !preparandoDecolagem; }
    public bool EstaNoSoloOperacional() { return !estaVoando && !estaPousando && !preparandoDecolagem; }
    public bool PodeOperarTropasNoMenu() { return !estaPousando && !preparandoDecolagem; }
    public bool EstaEmPreparacaoDecolagem() { return preparandoDecolagem; }
    public bool EhHelicopteroTransporte() { return helicopteroTransporte || capacidadeMaxima >= 20; }
    public string ObterDescricaoMenu() { return $"{nomeHelicoptero}\nLotação: {soldadosEmbarcados.Count}/{capacidadeMaxima}"; }
    public void MelhorarHelicoptero() { capacidadeMaxima += 4; nomeHelicoptero += "+"; }
    public int TemEspaco() { return capacidadeMaxima - soldadosEmbarcados.Count; }
    public bool TemSoldados() { return soldadosEmbarcados.Count > 0; }

    public void ChamarParaHeliporto(Transform t) { VoarEPousar(t.position); }
    public void ChamarParaHeliporto(Heliporto h) { VoarEPousar(h.transform.position); }
    public void ChamarParaHeliporto(GameObject g) { VoarEPousar(g.transform.position); }

    // --- MÉTODOS DE COMPATIBILIDADE DO AEROPORTO/NAVIO (MANTIDOS DA OUTRA IA) ---
    public bool controladoPeloAeroporto = false;
    public bool estacionadoNoAeroporto = false;
    public Transform vagaAeroporto;
    private Transform vagaOrigemAeroporto;
    private GerenciadorAeroporto aeroportoOrigem;
    private bool usandoVagaTemporariaNavio = false;
    public int missaoAtualAeroporto = 0; 
    private readonly List<Vector3> rotaPatrulhaAeroporto = new List<Vector3>();
    private int indicePatrulhaAeroporto = 0;

    public bool EstaSobControleDoAeroporto() { return controladoPeloAeroporto; }
    public string ObterEstadoOperacionalAeroporto()
    {
        if (estaPousando) return "Pousando";
        if (preparandoDecolagem) return "Decolando";
        if (usandoVagaTemporariaNavio && estaVoando) return "Entrando/Saindo do navio";
        if (usandoVagaTemporariaNavio && !estaVoando) return "No convés";
        if (estacionadoNoAeroporto) return "Estacionado";
        if (estaVoando && vagaAeroporto != null && !preparandoDecolagem) return "Aproximando";
        if (EstaNoSoloOperacional()) return TemSoldados() ? "Pousado c/ tropas" : "Pousado em campo";
        if (missaoAtualAeroporto == 4) return "Transporte";
        return missaoAtualAeroporto != 0 ? "Em Missão" : "Sobrevoando";
    }
    public bool EstaEstacionadoNoAeroporto() { return estacionadoNoAeroporto; }
    public Transform ObterVagaAeroporto() { return vagaAeroporto; }
    public Transform ObterVagaOrigemAeroporto() { return vagaOrigemAeroporto != null ? vagaOrigemAeroporto : vagaAeroporto; }
    public bool EstaAncoradoEmRaizMovel(Transform raizMovel)
    {
        if (raizMovel == null || transform.parent == null)
        {
            return false;
        }

        return transform.parent == raizMovel || transform.parent.IsChildOf(raizMovel);
    }

    public void DesancorarDeRaizMovel(Transform raizMovel)
    {
        if (transform.parent == null)
        {
            return;
        }

        if (raizMovel == null || transform.parent == raizMovel || transform.parent.IsChildOf(raizMovel))
        {
            transform.SetParent(null, true);
        }
    }

    public void IniciarPousoEmVagaMovel(Transform vaga)
    {
        if (vaga == null)
        {
            return;
        }

        vagaAeroporto = vaga;
        Vector3 alvo = ObterPosicaoEstacionadaNaVaga(vaga);

        if (rotinaPousoAuto != null)
        {
            StopCoroutine(rotinaPousoAuto);
            rotinaPousoAuto = null;
        }

        Decolar(alvo, false);
        destino = alvo;
    }

    public void AtualizarPousoEmVagaMovel(Transform vaga)
    {
        if (vaga == null)
        {
            return;
        }

        vagaAeroporto = vaga;
        Vector3 alvo = ObterPosicaoEstacionadaNaVaga(vaga);
        destino = alvo;

        if (!estaVoando || preparandoDecolagem)
        {
            return;
        }

        Vector2 atualXZ = new Vector2(transform.position.x, transform.position.z);
        Vector2 alvoXZ = new Vector2(alvo.x, alvo.z);
        float distanciaHorizontal = Vector2.Distance(atualXZ, alvoXZ);
        if (distanciaHorizontal <= Mathf.Max(raioInicioDescida, 8f))
        {
            estaPousando = true;
            subidaInicialDecolagem = false;
        }
    }

    public void FixarEmVagaMovel(Transform vaga, Transform raizMovel)
    {
        if (vaga == null || estaVoando || preparandoDecolagem)
        {
            return;
        }

        if (rotinaPousoAuto != null)
        {
            StopCoroutine(rotinaPousoAuto);
            rotinaPousoAuto = null;
        }

        estacionadoNoAeroporto = true;
        vagaAeroporto = vaga;
        destino = ObterPosicaoEstacionadaNaVaga(vaga);
        estaPousando = false;
        subidaInicialDecolagem = false;
        motorLigado = false;
        velocidadeAtualHelice = 0f;
        disponivelParaPatrulha = true;
        CancelarMissaoAeroporto();

        Transform paiMovel = vaga != transform ? vaga : raizMovel;
        if (paiMovel == null)
        {
            paiMovel = raizMovel;
        }

        if (paiMovel != null && transform.parent != paiMovel)
        {
            transform.SetParent(paiMovel, true);
        }

        transform.position = destino;
        transform.rotation = vaga.rotation;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (audioMotor) audioMotor.Stop();
        PararAnimacaoDecolagem();
    }

    public bool TemOrigemAeroportoRegistrada() { return aeroportoOrigem != null && vagaOrigemAeroporto != null; }
    public void IniciarPatrulhaAeroporto(List<Vector3> wp)
    {
        retomarPatrulhaDepoisDeAbastecer = false;
        rotaPatrulhaSalva.Clear();
        rotaPatrulhaAeroporto.Clear(); indicePatrulhaAeroporto = 0;
        if (wp == null || wp.Count == 0) { missaoAtualAeroporto = 0; return; }
        for (int i = 0; i < wp.Count; i++) { Vector3 ponto = AjustarDestinoParaVoo(wp[i]); rotaPatrulhaAeroporto.Add(ponto); }
        missaoAtualAeroporto = 3; Decolar(rotaPatrulhaAeroporto[0]);
    }
    public void CancelarMissaoAeroporto() { missaoAtualAeroporto = 0; rotaPatrulhaAeroporto.Clear(); indicePatrulhaAeroporto = 0; }
    public void IniciarReconhecimentoAeroporto(Vector3 wp) { CancelarMissaoAeroporto(); missaoAtualAeroporto = 1; Decolar(AjustarDestinoParaVoo(wp)); }
    public void IniciarAtaqueLocalAeroporto(Vector3 wp) { CancelarMissaoAeroporto(); missaoAtualAeroporto = 2; Decolar(AjustarDestinoParaVoo(wp)); }
    public void IniciarTransporteAeroporto(Vector3 wp) { CancelarMissaoAeroporto(); missaoAtualAeroporto = 4; VoarEPousar(AjustarDestinoParaPouso(wp)); }
    public void VincularAoAeroporto(GerenciadorAeroporto aeroporto, Transform vagaPreferencial)
    {
        aeroportoOrigem = aeroporto;
        vagaOrigemAeroporto = vagaPreferencial;
        vagaAeroporto = vagaPreferencial;
        controladoPeloAeroporto = true;
        usandoVagaTemporariaNavio = false;
    }

    public void VincularTemporariamenteAoNavio(Transform vagaTemporaria)
    {
        if (vagaOrigemAeroporto == null && vagaAeroporto != null)
        {
            vagaOrigemAeroporto = vagaAeroporto;
        }

        vagaAeroporto = vagaTemporaria;
        controladoPeloAeroporto = false;
        estacionadoNoAeroporto = false;
        usandoVagaTemporariaNavio = true;
    }

    public void TornarNavioBasePermanente(Transform vagaBase, GerenciadorAeroporto novaBase = null)
    {
        if (vagaBase == null)
        {
            return;
        }

        aeroportoOrigem = novaBase;
        vagaOrigemAeroporto = vagaBase;
        vagaAeroporto = vagaBase;
        controladoPeloAeroporto = novaBase != null;
        estacionadoNoAeroporto = false;
        usandoVagaTemporariaNavio = true;
    }

    public void DefinirModoCombateAtivo(bool ativo)
    {
        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle == null)
        {
            controle = GetComponentInParent<ControleUnidade>();
        }

        if (controle != null)
        {
            controle.DefinirModoCombate(ativo);
            return;
        }

        AplicarModoCombateDoMenu(ativo);
    }

    public void AplicarModoCombateDoMenu(bool ativo)
    {
        modoCombateAtivo = ativo;
        if (!ativo)
        {
            alvoComandoAtaque = null;
        }
    }

    public void RestaurarControleDoAeroportoOrigem()
    {
        if (vagaOrigemAeroporto != null)
        {
            vagaAeroporto = vagaOrigemAeroporto;
        }
        else if (usandoVagaTemporariaNavio)
        {
            vagaAeroporto = null;
        }

        usandoVagaTemporariaNavio = false;
        estacionadoNoAeroporto = false;
        controladoPeloAeroporto = aeroportoOrigem != null;

        if (aeroportoOrigem != null)
        {
            aeroportoOrigem.RegistrarHelicopteroControlado(this);
        }
    }
    
    private void FinalizarPosicionamentoNaVagaAeroporto(Transform vaga)
    {
        if (rotinaPousoAuto != null)
        {
            StopCoroutine(rotinaPousoAuto);
            rotinaPousoAuto = null;
        }

        if (rotinaPreparacaoDecolagem != null)
        {
            StopCoroutine(rotinaPreparacaoDecolagem);
            rotinaPreparacaoDecolagem = null;
        }

        estacionadoNoAeroporto = true;
        vagaAeroporto = vaga;
        transform.position = ObterPosicaoEstacionadaNaVaga(vaga);
        transform.rotation = vaga.rotation;
        preparandoDecolagem = false;
        estaVoando = false;
        estaPousando = false;
        subidaInicialDecolagem = false;
        motorLigado = false;
        velocidadeAtualHelice = 0f;
        if (rb != null) rb.interpolation = RigidbodyInterpolation.None;
        if (audioMotor) audioMotor.Stop();
        PararAnimacaoDecolagem();
        CancelarMissaoAeroporto();
    }

    public void PosicionarNaVagaAeroporto(Transform vaga) 
    { 
        if (vaga == null) return;
        if (Vector3.Distance(transform.position, vaga.position) > 15f) { vagaAeroporto = vaga; VoarEPousar(vaga.position); return; }
        FinalizarPosicionamentoNaVagaAeroporto(vaga);
    }

    public void PosicionarInstantaneamenteNaVagaAeroporto(Transform vaga)
    {
        if (vaga == null) return;
        FinalizarPosicionamentoNaVagaAeroporto(vaga);
    }

    private void SalvarMissaoAntesDoReabastecimento()
    {
        retomarPatrulhaDepoisDeAbastecer = missaoAtualAeroporto == 3 && rotaPatrulhaAeroporto.Count > 1;
        if (retomarPatrulhaDepoisDeAbastecer)
        {
            rotaPatrulhaSalva.Clear();
            rotaPatrulhaSalva.AddRange(rotaPatrulhaAeroporto);
            indicePatrulhaSalva = Mathf.Clamp(indicePatrulhaAeroporto, 0, rotaPatrulhaSalva.Count - 1);
            Debug.Log($"[Helicoptero] Missao de patrulha salva para reabastecimento: {name} pontos={rotaPatrulhaSalva.Count} indice={indicePatrulhaSalva}");
        }

        ComportamentoSeguirUniversal seguir = ObterComportamentoSeguir();
        retomarSeguimentoDepoisDeAbastecer = seguir != null && seguir.enabled && seguir.AlvoSeguido != null;
        if (retomarSeguimentoDepoisDeAbastecer)
        {
            alvoSeguimentoSalvo = seguir.AlvoSeguido;
            distanciaSeguimentoSalva = -1f;
            seguir.enabled = false;
            Debug.Log($"[Helicoptero] Seguimento salvo para reabastecimento: {name} alvo={alvoSeguimentoSalvo.name}");
        }
    }

    private void RetomarMissaoDepoisDoReabastecimento()
    {
        if (retomarSeguimentoDepoisDeAbastecer && alvoSeguimentoSalvo != null && alvoSeguimentoSalvo.gameObject.activeInHierarchy)
        {
            ControleUnidade controle = ObterControleUnidade();
            if (controle != null)
            {
                ComportamentoSeguirUniversal seguir = ObterComportamentoSeguir();
                if (seguir == null) seguir = gameObject.AddComponent<ComportamentoSeguirUniversal>();
                seguir.enabled = true;
                seguir.Configurar(alvoSeguimentoSalvo, distanciaSeguimentoSalva);
                controle.DefinirAlvoPrioritario(alvoSeguimentoSalvo);
                controle.DefinirModoCombate(modoCombateAtivo);
                Debug.Log($"[Helicoptero] Seguimento retomado apos reabastecimento: {name} alvo={alvoSeguimentoSalvo.name}");
            }
        }
        else if (retomarPatrulhaDepoisDeAbastecer && rotaPatrulhaSalva.Count > 1)
        {
            List<Vector3> rota = new List<Vector3>(rotaPatrulhaSalva.Count);
            for (int i = 0; i < rotaPatrulhaSalva.Count; i++)
            {
                rota.Add(rotaPatrulhaSalva[(indicePatrulhaSalva + i) % rotaPatrulhaSalva.Count]);
            }
            IniciarPatrulhaAeroporto(rota);
            Debug.Log($"[Helicoptero] Patrulha retomada apos reabastecimento: {name} pontos={rota.Count}");
        }

        retomarPatrulhaDepoisDeAbastecer = false;
        retomarSeguimentoDepoisDeAbastecer = false;
        alvoSeguimentoSalvo = null;
        rotaPatrulhaSalva.Clear();
    }

    public void RetornarParaVagaAeroporto()
    {
        SalvarMissaoAntesDoReabastecimento();
        Transform vagaRetorno = vagaOrigemAeroporto != null ? vagaOrigemAeroporto : vagaAeroporto;
        if (vagaRetorno != null)
        {
            vagaAeroporto = vagaRetorno;
            usandoVagaTemporariaNavio = false;
            controladoPeloAeroporto = aeroportoOrigem != null;
            CancelarMissaoAeroporto();
            VoarEPousar(vagaRetorno.position);
            estacionadoNoAeroporto = false;
        }
    }

    private void AvaliarRetornoSeguro()
    {
        if (!estaVoando || estaPousando)
        {
            return;
        }

        CombustivelUnidade combustivel = GetComponent<CombustivelUnidade>();
        if (combustivel == null || !combustivel.usaCombustivel)
        {
            return;
        }

        Transform vagaRetorno = vagaOrigemAeroporto != null ? vagaOrigemAeroporto : vagaAeroporto;
        if (vagaRetorno == null)
        {
            return;
        }

        float distancia = Vector3.Distance(transform.position, vagaRetorno.position);
        float consumoRetorno = combustivel.EstimarConsumoParaDistancia(distancia, Mathf.Max(8f, velocidadeNavegacao));
        float reserva = Mathf.Max(combustivel.Capacidade * reservaRetornoPercentual, consumoRetorno * 0.45f);

        if (combustivel.CombustivelAtual <= consumoRetorno + reserva)
        {
            RetornarParaVagaAeroporto();
        }
    }
}
