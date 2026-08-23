using System.Collections.Generic;
using UnityEngine;

public class ControleTorreta : MonoBehaviour
{
    #region Variáveis Públicas (Inspetor)
    [Header("Radar")]
    [Tooltip("Define qual tag a torreta vai procurar (Ex: 'Inimigo', 'Aereo').")]
    public string etiquetaAlvo = "Aereo"; 
    
    [Tooltip("Distância máxima que o radar consegue enxergar.")]
    public float alcance = 120f; 
    
    [Header("Mecânica & Recarga")]
    [Tooltip("Velocidade que a torreta gira para acompanhar o alvo.")]
    public float velocidadeGiro = 60f;
    
    [Header("Limites de Rotação (Anti-Clipping)")]
    public bool limitarRotacao = true;
    [Range(-180, 180)] public float anguloMinimo = -90f;
    [Range(-180, 180)] public float anguloMaximo = 90f;

    [Tooltip("Tempo em SEGUNDOS entre cada tiro (Quanto menor, mais rápido).")]
    public float tempoEntreTiros = 0.08f; 

    [Tooltip("Quantidade de tiros até precisar carregar (Ex: 50 balas).")]
    public int tamanhoCartucho = 50; 

    [Tooltip("Tempo inativa recarregando (Segundos).")]
    public float tempoRecarga = 2.0f; 

    [Tooltip("Permite disparar enquanto o alvo está sendo acompanhado, mesmo fora do alinhamento perfeito. O projétil segue a direção atual do cano e pode errar.")]
    public bool dispararMesmoDesalinhado = false;
    [Tooltip("Quando ativado, o projétil usa a linha predita do alvo mesmo se a animação visual do cano ainda estiver alguns graus atrasada. Usado apenas em armas com assistência de mira explícita.")]
    public bool direcionarProjetilParaPredicao = false;
    
    [Header("Peças")]
    [Tooltip("A base que gira para os lados (Eixo Y).")]
    public Transform pecaQueGira; 
    [Tooltip("Cria um pivô seguro no centro da malha quando a peça foi importada com vértices longe da origem.")]
    public bool criarPivoSeguroDaMalha = false;
    [Tooltip("Quando não existe uma peça de canos separada, permite inclinar a própria peça que gira para acompanhar alvos acima/abaixo.")]
    public bool inclinarPecaSemCanos = false;
    [Tooltip("Opcional: A parte que levanta e abaixa (Eixo X). Deixe vazio para a base inclinar inteira.")]
    public Transform canosDaTorreta; 
    [Tooltip("Permite procurar automaticamente um pivô de elevação entre os filhos dos locais de tiro.")]
    public bool descobrirCanosAutomaticamente = true;
    public Transform[] locaisDoTiro;  
    public GameObject municaoPrefab; 
    
    [Header("Limites de Rotação Cima/Baixo (Pitch)")]
    public bool limitarInclinacao = true;
    [Range(-90, 90)] public float elevacaoMinima = -10f;
    [Range(-90, 90)] public float elevacaoMaxima = 80f; 

    [Header("Efeitos")]
    public AudioClip somTiro;
    public AudioClip somRecarga; 
    public ParticleSystem fogoCano;
    [Tooltip("Objeto/efeito de disparo já criado na hierarquia ou no prefab. Se definido, ele também é ativado no tiro.")]
    public GameObject fogoCanoObjeto;
    [Tooltip("Prefab visual opcional do clarão/fumaça de disparo. Pode ser um prefab leve da pasta FX.")]
    public GameObject efeitoCanoPrefab;
    [Tooltip("Ponto opcional para soltar o efeito do disparo. Se vazio, usa o primeiro cano/local de tiro encontrado.")]
    public Transform pontoEfeitoCano;
    [Tooltip("Tempo de vida do efeito de disparo instanciado.")]
    public float duracaoEfeitoCano = 1.5f;

    [Header("Recuo Realista")]
    [Tooltip("Ativa um pequeno recuo visual a cada disparo.")]
    public bool usarRecuoAoDisparar = true;
    [Tooltip("Peça que vai sofrer o recuo. Se vazio, usa os canos da torreta ou a base giratória.")]
    public Transform pecaRecuo;
    [Tooltip("Distância do recuo em metros locais por disparo.")]
    public float forcaRecuoDisparo = 0.12f;
    [Tooltip("Velocidade de retorno do recuo ao ponto original.")]
    public float velocidadeRetornoRecuo = 18f;

    [Header("Sistema de Desdobramento (MLRS/Lançador)")]
    [Tooltip("Se ativado, a torreta precisa 'abrir' ou 'levantar' antes de disparar.")]
    public bool usarSistemaDesdobramento = false;
    [Tooltip("A peça específica que será movida/rotacionada (ex: MissilePort).")]
    public Transform pecaParaDesdobrar;
    public float tempoParaDesdobrar = 2.0f;
    
    public Vector3 posRepouso = new Vector3(3.9434f, 1.26f, -1.36f);
    public Vector3 posDisparo = new Vector3(3.9434f, 3.21f, -4.29f);
    
    public Vector3 rotRepouso = new Vector3(0, 90, 0);
    public Vector3 rotDisparo = new Vector3(0, 90, -86.14f);

    [Tooltip("Se ativado, pega as coordenadas de Repouso de onde a peça parar no Start (ignorando os números de Repouso acima).")]
    public bool autoConfigurarRepousoNoStart = true;

    [Header("Busca de Pontos por Tag")]
    [Tooltip("Se definido, buscará objetos com esta TAG para usar como locais de lançamento de mísseis.")]
    public string tagPontoMissel = "";

    [Tooltip("Se ativado, a torreta não ataca automaticamente.")]
    public bool modoPassivo = false;

    [Header("Radar e Ociosidade")]
    [Tooltip("Se ativado, a torreta fica girando 360º quando não tem alvos (estilo radar). Se desativado, ela volta para a frente.")]
    public bool modoRadar = false;
    [Tooltip("Failsafe: impede qualquer rotacao automatica desta torreta.")]
    public bool bloquearMovimentoAutomatico = false;
    
    [Header("Defesa Anti-Míssil")]
    [Tooltip("Pode interceptar mísseis inimigos no ar?")]
    public bool interceptarMisseis = false;
    [Tooltip("Se true, torretas aéreas vão usar mísseis para interceptar outros mísseis.")]
    public bool usarMisselParaInterceptar = true;

    [Header("Defesa Anti-Torpedo")]
    [Tooltip("Pode detectar e interceptar torpedos inimigos?")]
    public bool interceptarTorpedos = false;
    [Tooltip("Se true, esta torreta pode identificar o submarino/navio de origem do torpedo.")]
    public bool identificarOrigemTorpedo = true;
    [Tooltip("Raio extra de detecção para torpedos (submarinos são mais difíceis de rastrear).")]
    public float raioDeteccaoTorpedo = 150f;
    [Tooltip("Prioridade de alvo: 0 = torpedos, 1 = mísseis, 2 = aeronaves, 3 = navios.")]
    public int prioridadeTorpedos = 0;

    [Header("Diagnóstico")]
    public bool debugRadar = false;

    [Header("Armamento Secundário (Mísseis)")]
    [Tooltip("Se definido, usa este prefab para disparos especiais ou de longo alcance.")]
    public GameObject misselPrefab;
    public Transform[] locaisDoMissel; 
    public AudioClip somMissel;
    public float tempoEntreMisseis = 2.0f;
    
    [Tooltip("Quantidade máxima de mísseis antes de precisar recarregar.")]
    public int capacidadeMisseis = 4;
    [Tooltip("Tempo em segundos para reabastecer os mísseis.")]
    public float tempoRecargaMisseis = 10f;

    [Header("Custumização de Disparo")]
    [Tooltip("Se quiser munições diferentes para canos diferentes, arraste aqui na ordem dos Locais Do Tiro.")]
    public GameObject[] municoesPorCano; 
    #endregion

    #region Variáveis Privadas
    private float contadorTempo = 0f;
    private int balasAtuais;
    private bool estaRecarregando = false;
    
    private int misseisAtuais;
    private bool estaRecarregandoMisseis = false;
    private float contadorRecargaMissel = 0f;
    private float cooldownMissel = 0f;

    // Buffer de colisores privado para não gerar lixo na memória
    private Collider[] bufferColisores = new Collider[40]; 

    private AudioSource fonteAudio;
    private Transform alvoAtual;
    [HideInInspector] public Transform alvoPrioritario;
    private Rigidbody alvoAtualRb; // CACHE: Otimização crucial para não usar GetComponent no Update
    private int indiceBarrilAtual = 0; 
    private int indicacaoSeguraDeBala = 0;
    
    private float rotacaoXOriginal, rotacaoYOriginal, rotacaoZOriginal, giroPitchAlvo = 0f;
    // Preserva a postura authored do prefab. Zerar X/Z ao mirar fazia algumas
    // torretas deitarem quando o alvo mudava de lado ou no momento do disparo.
    private Quaternion rotacaoInicialPecaQueGira = Quaternion.identity;
    private Quaternion rotacaoInicialCanos = Quaternion.identity;
    private bool rotacaoInicialCanosCapturada;
    private float progressoDesdobramento = 0f;
    private bool estaProntoParaAtirar = true;
    private Transform _alvoRecuoTransform;
    private Vector3 _recuoLocalOriginal;
    private bool _recuoInicializado;
    private float _recuoAtual;

    // Rastreio de Velocidade
    private Transform alvoAnteriorParaCalculo;
    private Vector3 ultimaPosicaoAlvo;
    private Vector3 velocidadeCalculadaAlvo;

    private LineRenderer linhaDeAlcance;
    private ControleUnidade meuControle;
    private IdentidadeUnidade minhaIdentidade;
    private int meuTime = -1;
    private readonly EstadoOtimizacaoTatica estadoOtimizacao = new EstadoOtimizacaoTatica();
    private float proximaBuscaAlvo;

    private bool souAntiAereo;
    private bool diagnosticoLocaisDoTiroEmitido;
    private bool bloquearRotacaoAutomatica;

    // Dicionários de Cache Estático para Radar Rápido (Limpados preventivamente)
    private static readonly Dictionary<Transform, IdentidadeUnidade> _idParentCache = new Dictionary<Transform, IdentidadeUnidade>();
    private static readonly Dictionary<Transform, bool> _aviaoParentCache = new Dictionary<Transform, bool>();
    private static readonly Dictionary<Transform, bool> _heliParentCache = new Dictionary<Transform, bool>();
    private static readonly Dictionary<Transform, bool> _c700ParentCache = new Dictionary<Transform, bool>();
    private static readonly Dictionary<Transform, bool> _missilParentCache = new Dictionary<Transform, bool>();
    private static readonly Dictionary<Transform, bool> _torpedoParentCache = new Dictionary<Transform, bool>();
    private static readonly List<IdentidadeUnidade> _unidadesRegistroRadar = new List<IdentidadeUnidade>(256);

    // Rastreamento de origem de torpedos
    private Transform lancadorDoTorpedoDetectado;
    private float tempoUltimaIdentificacao = -999f;
    #endregion

    #region Funções de Editor
    [ContextMenu("Salvar Atual como REPOUSO")]
    void SalvarAtualComoRepouso()
    {
        if (pecaParaDesdobrar != null)
        {
            posRepouso = pecaParaDesdobrar.localPosition;
            rotRepouso = pecaParaDesdobrar.localEulerAngles;
            Debug.Log("Posição e Rotação de REPOUSO salvas com sucesso!");
        }
    }

    [ContextMenu("Salvar Atual como DISPARO")]
    void SalvarAtualComoDisparo()
    {
        if (pecaParaDesdobrar != null)
        {
            posDisparo = pecaParaDesdobrar.localPosition;
            rotDisparo = pecaParaDesdobrar.localEulerAngles;
            Debug.Log("Posição e Rotação de DISPARO salvas com sucesso!");
        }
    }
    #endregion

    #region Inicialização
    void Start()
    {
        meuControle = GetComponentInParent<ControleUnidade>();
        if (meuControle == null) meuControle = GetComponent<ControleUnidade>();

        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        meuTime = (minhaIdentidade != null) ? minhaIdentidade.teamID : 1;

        souAntiAereo = DeterminarSouAntiAereo();

        balasAtuais = tamanhoCartucho;
        misseisAtuais = capacidadeMisseis;

        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null) fonteAudio = gameObject.AddComponent<AudioSource>();
        AudioRuntime.ConfigurarFonteDeTiro(fonteAudio);

        InicializarEfeitosDisparo();

        if (usarSistemaDesdobramento)
        {
            estaProntoParaAtirar = false;
            progressoDesdobramento = 0f;
            if (pecaParaDesdobrar != null)
            {
                if (autoConfigurarRepousoNoStart)
                {
                    posRepouso = pecaParaDesdobrar.localPosition;
                    rotRepouso = pecaParaDesdobrar.localEulerAngles;
                }
                else
                {
                    pecaParaDesdobrar.localPosition = posRepouso;
                    pecaParaDesdobrar.localRotation = Quaternion.Euler(rotRepouso);
                }
            }
        }
        else
        {
            estaProntoParaAtirar = true;
        }

        if (locaisDoMissel == null || locaisDoMissel.Length == 0)
        {
            if (!string.IsNullOrEmpty(tagPontoMissel))
            {
                List<Transform> encontrados = new List<Transform>();
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.CompareTag(tagPontoMissel)) encontrados.Add(t);
                }
                if (encontrados.Count > 0) locaisDoMissel = encontrados.ToArray();
            }
        }
        
        Helicoptero helicopteroPai = GetComponentInParent<Helicoptero>();
        if (helicopteroPai != null && pecaQueGira == null && canosDaTorreta == null)
            bloquearRotacaoAutomatica = true;

        GarantirPivoSeguroDaMalha();

        if (pecaQueGira == null)
        {
            // Nunca use o primeiro filho como pivô: em vários prefabs ele é
            // a malha inteira, o convés ou um marcador de saída. Girá-lo
            // durante o tiro altera a estrutura visual do prefab.
            DiagnosticoDesempenhoJogo.RegistrarEvento(
                "TurretAimFallback",
                name + ": pecaQueGira ausente; estrutura preservada");
        }
        
        if (pecaQueGira != null)
        {
            rotacaoXOriginal = pecaQueGira.localEulerAngles.x;
            rotacaoYOriginal = pecaQueGira.localEulerAngles.y;
            rotacaoZOriginal = pecaQueGira.localEulerAngles.z;
            rotacaoInicialPecaQueGira = pecaQueGira.localRotation;
        }

        if (canosDaTorreta != null)
        {
            rotacaoInicialCanos = canosDaTorreta.localRotation;
            rotacaoInicialCanosCapturada = true;
        }

        proximaBuscaAlvo = Time.unscaledTime + Random.Range(0f, 0.5f);

        CriarVisualizadorAlcance();
        GarantirLocaisDeTiro();
        GarantirCanosDaTorreta();
        if (canosDaTorreta != null && !rotacaoInicialCanosCapturada)
        {
            rotacaoInicialCanos = canosDaTorreta.localRotation;
            rotacaoInicialCanosCapturada = true;
        }

        if (municaoPrefab != null)
            PoolDeObjetosCombate.Prewarm(municaoPrefab, Mathf.Clamp(tamanhoCartucho / 5, 4, 12));
        if (municoesPorCano != null)
            for (int _pi = 0; _pi < municoesPorCano.Length; _pi++)
                if (municoesPorCano[_pi] != null && municoesPorCano[_pi] != municaoPrefab)
                    PoolDeObjetosCombate.Prewarm(municoesPorCano[_pi], 4);
    }
    #endregion

    void GarantirPivoSeguroDaMalha()
    {
        if (!criarPivoSeguroDaMalha || pecaQueGira == null || pecaQueGira.parent == null)
        {
            return;
        }

        MeshFilter filtro = pecaQueGira.GetComponent<MeshFilter>();
        MeshRenderer renderer = pecaQueGira.GetComponent<MeshRenderer>();
        if (filtro == null || filtro.sharedMesh == null || renderer == null)
        {
            return;
        }

        Vector3 centroLocal = filtro.sharedMesh.bounds.center;
        if (centroLocal.sqrMagnitude < 0.01f)
        {
            return;
        }

        Transform pecaOriginal = pecaQueGira;
        Transform paiOriginal = pecaOriginal.parent;
        Vector3 posicaoPivoMundo = pecaOriginal.TransformPoint(centroLocal);
        Quaternion rotacaoPivoMundo = pecaOriginal.rotation;

        GameObject objetoPivo = new GameObject(pecaOriginal.name + "_PivoSeguro");
        Transform pivo = objetoPivo.transform;
        pivo.SetParent(paiOriginal, true);
        pivo.position = posicaoPivoMundo;
        pivo.rotation = rotacaoPivoMundo;
        pivo.localScale = Vector3.one;

        // Preserva a posição de repouso da malha. A partir daqui, somente o
        // novo pivô gira, impedindo que a arma faça uma órbita em torno da
        // origem do avião ao procurar o alvo.
        pecaOriginal.SetParent(pivo, true);
        pecaQueGira = pivo;
    }

    #region Radar e Busca de Alvos
    void ProcurarAlvo()
    {
        if (modoPassivo || bloquearMovimentoAutomatico)
        {
            SetarAlvo(null);
            return;
        }

        if (alvoAtual != null && alvoAtual.gameObject.activeInHierarchy && !interceptarMisseis && !interceptarTorpedos)
        {
            return;
        }

        if (alvoPrioritario != null && alvoPrioritario.gameObject.activeInHierarchy && ControleSubmarino.PodeSerAlvoConvencional(alvoPrioritario))
        {
            Collider colPrioritario = alvoPrioritario.GetComponentInChildren<Collider>();
            Vector3 alvoPosRealPrioritario = (colPrioritario != null) ? colPrioritario.ClosestPoint(transform.position) : alvoPrioritario.position;
            float distSqrPrioritario = (transform.position - alvoPosRealPrioritario).sqrMagnitude;
            if (distSqrPrioritario <= alcance * alcance)
            {
                SetarAlvo(alvoPrioritario);
                return;
            }
        }

        if (DeveAdiarNovaBusca())
        {
            return;
        }

        int quantidadeEncontrada = Physics.OverlapSphereNonAlloc(transform.position, alcance, bufferColisores, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        if (debugRadar && quantidadeEncontrada > 0)
        {
            Debug.Log($"[ControleTorreta {name}] OverlapSphere encontrou {quantidadeEncontrada} colliders. MeuTime={meuTime}, AntiAereo={souAntiAereo}, Alcance={alcance}");
        }

        for (int i = 0; i < quantidadeEncontrada; i++)
        {
            Collider hit = bufferColisores[i];
            if (hit == null)
            {
                continue;
            }

            Transform alvoTr = hit.transform;
            Transform alvoSubstitutoAereo = ResolverAtiradorAereoDeProjetil(hit);
            if (alvoSubstitutoAereo != null)
            {
                alvoTr = alvoSubstitutoAereo;
            }

            if (!ControleSubmarino.PodeSerAlvoConvencional(alvoTr))
            {
                continue;
            }

            if (alvoTr.root == transform.root)
            {
                continue;
            }

            bool ehMissil = ObterEhMissilComCache(alvoTr);
            bool ehTorpedo = ObterEhTorpedoComCache(alvoTr);
            bool ehInimigo = false;

            if (interceptarMisseis && ehMissil)
            {
                Vector3 direcaoDoMissil = alvoTr.forward;
                Vector3 direcaoParaMim = (transform.position - alvoTr.position).normalized;
                if (Vector3.Dot(direcaoDoMissil, direcaoParaMim) > 0.2f)
                {
                    ehInimigo = true;
                }
                else
                {
                    continue;
                }
            }
            else if (interceptarTorpedos && ehTorpedo)
            {
                ehInimigo = true;

                if (identificarOrigemTorpedo)
                {
                    Torpedo torpedo = alvoTr.GetComponent<Torpedo>();
                    if (torpedo != null && torpedo.lancador != null)
                    {
                        IdentidadeUnidade idLancador = torpedo.lancador.GetComponent<IdentidadeUnidade>();
                        if (idLancador != null && idLancador.teamID != meuTime && idLancador.teamID != 0)
                        {
                            lancadorDoTorpedoDetectado = torpedo.lancador;
                            tempoUltimaIdentificacao = Time.time;
                        }
                    }
                }
            }
            else
            {
                IdentidadeUnidade idAlvo = ObterIdentidadeUnidadeComCache(alvoTr);
                if (idAlvo != null)
                {
                    if (idAlvo.teamID != meuTime && idAlvo.teamID != 0)
                    {
                        ehInimigo = true;
                    }
                }
                else if (TagSafe.Matches(hit, etiquetaAlvo) || TagSafe.Matches(hit, "Inimigo"))
                {
                    ehInimigo = true;
                }
            }

            if (!ehInimigo)
            {
                continue;
            }

            IdentidadeUnidade idAlvo2 = ObterIdentidadeUnidadeComCache(alvoTr);
            bool alvoAereo = ehMissil || ehTorpedo || alvoTr.position.y > 6f ||
                             (idAlvo2 != null && idAlvo2.tipoUnidade == TipoUnidade.Aereo) ||
                             TagSafe.Matches(alvoTr, "Aereo") || TagSafe.Matches(alvoTr, "Areo") ||
                             ObterEhAviaoComCache(alvoTr) || ObterEhHeliComCache(alvoTr) ||
                             ObterEhC700ComCache(alvoTr);

            if (!alvoAereo)
            {
                string nm = alvoTr.name;
                alvoAereo = nm.Contains("aviao", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("heli", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("caca", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("drone", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("c700", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("b260", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("bombardeiro", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("transporte", System.StringComparison.OrdinalIgnoreCase);
            }

            if (debugRadar)
            {
                string tipo = idAlvo2 != null ? idAlvo2.tipoUnidade.ToString() : "sem_id";
                int tid = idAlvo2 != null ? idAlvo2.teamID : -99;
                string tipoAlvo = ehTorpedo ? "TORPEDO" : (ehMissil ? "MISSIL" : (alvoAereo ? "AEREO" : "TERRESTRE"));
                Debug.Log($"[ControleTorreta {name}] Candidato: {alvoTr.root.name} | Tipo={tipoAlvo} | Inimigo={ehInimigo} | Aereo={alvoAereo} | Classe={tipo} | TeamID={tid} | Y={alvoTr.position.y:F1} | Dist={Vector3.Distance(transform.position, alvoTr.position):F1}");
            }

            if (souAntiAereo && !alvoAereo)
            {
                continue;
            }

            if (!souAntiAereo && alvoAereo)
            {
                continue;
            }

            float prioridade = 0f;
            if (ehTorpedo) prioridade = -1000f;
            else if (ehMissil) prioridade = -500f;

            Vector3 pontoMaisProximo = hit.ClosestPoint(transform.position);
            float dist = (transform.position - pontoMaisProximo).sqrMagnitude + prioridade;
            if (dist < menorDistancia)
            {
                menorDistancia = dist;
                melhorAlvo = ResolverTransformAlvo(alvoTr);
            }
        }

        if (melhorAlvo == null)
        {
            melhorAlvo = ProcurarAlvoNoRegistroGlobal();
        }

        if (debugRadar)
        {
            Debug.Log($"[ControleTorreta {name}] Alvo escolhido: {(melhorAlvo != null ? melhorAlvo.name : "NENHUM")}");
        }

        for (int i = 0; i < quantidadeEncontrada; i++)
        {
            bufferColisores[i] = null;
        }

        SetarAlvo(melhorAlvo);
    }

    private bool DeveAdiarNovaBusca()
    {
        if (!DiagnosticoDesempenhoJogo.RuntimeSobPressao() && !DiagnosticoDesempenhoJogo.RuntimeSaturado())
        {
            return false;
        }

        int divisor = DiagnosticoDesempenhoJogo.RuntimeSaturado() ? 4 : 2;
        return (Mathf.Abs(GetInstanceID()) + Time.frameCount) % divisor != 0;
    }

    public void DefinirModoAtivo(bool ativo)
    {
        modoPassivo = !ativo; 
        if (modoPassivo) SetarAlvo(null);
    }

    public void DefinirAlvo(Transform alvo)
    {
        SetarAlvo(alvo);
    }

    private Transform ProcurarAlvoNoRegistroGlobal()
    {
        RegistroEntidadesJogo.FillUnidades(_unidadesRegistroRadar);
        float alcanceSqr = alcance * alcance;
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        for (int i = 0; i < _unidadesRegistroRadar.Count; i++)
        {
            IdentidadeUnidade idAlvo = _unidadesRegistroRadar[i];
            if (idAlvo == null || !idAlvo.gameObject.activeInHierarchy) continue;
            if (idAlvo.teamID == 0 || idAlvo.teamID == meuTime) continue;

            Transform alvoTr = ResolverTransformAlvo(idAlvo.transform);
            if (alvoTr == null || alvoTr.root == transform.root) continue;
            if (!ControleSubmarino.PodeSerAlvoConvencional(alvoTr)) continue;

            float distSqr = (alvoTr.position - transform.position).sqrMagnitude;
            if (distSqr > alcanceSqr) continue;

            bool alvoAereo = alvoTr.position.y > 6f
                || idAlvo.tipoUnidade == TipoUnidade.Aereo
                || ObterEhAviaoComCache(alvoTr)
                || ObterEhHeliComCache(alvoTr)
                || ObterEhC700ComCache(alvoTr);

            if (!alvoAereo)
            {
                string nm = alvoTr.name;
                alvoAereo = nm.Contains("aviao", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("heli", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("caca", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("drone", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("c700", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("b260", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("bombardeiro", System.StringComparison.OrdinalIgnoreCase) ||
                            nm.Contains("transporte", System.StringComparison.OrdinalIgnoreCase);
            }

            if (souAntiAereo && !alvoAereo) continue;
            if (!souAntiAereo && alvoAereo) continue;

            if (distSqr < menorDistancia)
            {
                menorDistancia = distSqr;
                melhorAlvo = alvoTr;
            }
        }

        _unidadesRegistroRadar.Clear();
        return melhorAlvo;
    }

    private void SetarAlvo(Transform novoAlvo)
    {
        if (alvoAtual != novoAlvo)
        {
            alvoAtual = novoAlvo;
            // OTIMIZAÇÃO: Cache do Rigidbody para não buscar no Update()
            alvoAtualRb = alvoAtual != null ? alvoAtual.GetComponentInParent<Rigidbody>() : null;
            
            alvoAnteriorParaCalculo = alvoAtual;
            ultimaPosicaoAlvo = alvoAtual != null ? alvoAtual.position : Vector3.zero;
            velocidadeCalculadaAlvo = Vector3.zero;
        }
    }
    #endregion

    #region Update e Rotação
    void Update()
    {
        AtualizarAgendamentoBusca();
        if (bloquearMovimentoAutomatico)
        {
            SetarAlvo(null);
            return;
        }

        AtualizarVisualizadorAlcance();
        AtualizarRecuo();

        // Recarga de mísseis
        if (estaRecarregandoMisseis)
        {
            contadorRecargaMissel -= Time.deltaTime;
            if (contadorRecargaMissel <= 0f)
            {
                estaRecarregandoMisseis = false;
                misseisAtuais = capacidadeMisseis;
                contadorRecargaMissel = 0f;
            }
        }
        else if (cooldownMissel > 0f) cooldownMissel -= Time.deltaTime; 

        // Recarga de balas
        if (estaRecarregando)
        {
            contadorTempo -= Time.deltaTime;
            if (contadorTempo <= 0f)
            {
                estaRecarregando = false;
                balasAtuais = tamanhoCartucho;
                contadorTempo = 0f; 
            }
            return; 
        }

        if (alvoAtual != null)
        {
            // Lógica de Desdobramento
            if (usarSistemaDesdobramento && progressoDesdobramento < 1f)
            {
                progressoDesdobramento += Time.deltaTime / tempoParaDesdobrar;
                if (pecaParaDesdobrar != null)
                {
                    pecaParaDesdobrar.localPosition = Vector3.Lerp(posRepouso, posDisparo, progressoDesdobramento);
                    pecaParaDesdobrar.localRotation = Quaternion.Lerp(Quaternion.Euler(rotRepouso), Quaternion.Euler(rotDisparo), progressoDesdobramento);
                }
                if (progressoDesdobramento >= 1f) estaProntoParaAtirar = true;
            }

            if (!alvoAtual.gameObject.activeInHierarchy
                || !ControleSubmarino.PodeSerAlvoConvencional(alvoAtual)
                || (alvoAtual.position - transform.position).sqrMagnitude > alcance * alcance)
            {
                SetarAlvo(null);
                return;
            }

            // Cálculo da Velocidade para Predição
            if (Time.deltaTime > 0f)
            {
                Vector3 velInst = (alvoAtual.position - ultimaPosicaoAlvo) / Time.deltaTime;
                velocidadeCalculadaAlvo = Vector3.Lerp(velocidadeCalculadaAlvo, velInst, Time.deltaTime * 15f);
                ultimaPosicaoAlvo = alvoAtual.position;
            }

            indicacaoSeguraDeBala = indiceBarrilAtual;

            // Rotação
            float anguloY = rotacaoYOriginal;
            if (pecaQueGira != null && !bloquearRotacaoAutomatica)
            {
                Vector3 alvoPosicao = ObterPosicaoPreditaAlvo();
                Vector3 direcao = alvoPosicao - pecaQueGira.position;

                Transform referencia = (pecaQueGira.parent != null) ? pecaQueGira.parent : pecaQueGira;
                Vector3 localDir = referencia.InverseTransformDirection(direcao);

                anguloY = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                if (limitarRotacao) anguloY = Mathf.Clamp(anguloY, anguloMinimo, anguloMaximo);

                float distanciaPlana = new Vector2(localDir.x, localDir.z).magnitude;
                giroPitchAlvo = -Mathf.Atan2(localDir.y, distanciaPlana) * Mathf.Rad2Deg;
                if (limitarInclinacao) giroPitchAlvo = Mathf.Clamp(giroPitchAlvo, -elevacaoMaxima, -elevacaoMinima);

                if (canosDaTorreta != null && canosDaTorreta != pecaParaDesdobrar)
                {
                    // Altera somente o yaw: a inclinação/roll original do
                    // suporte continua intacta.
                    Quaternion rotacaoBase = Quaternion.Euler(rotacaoXOriginal, anguloY, rotacaoZOriginal);
                    pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoBase, Time.deltaTime * velocidadeGiro);

                    Quaternion rotacaoCanos = (rotacaoInicialCanosCapturada ? rotacaoInicialCanos : Quaternion.identity)
                        * Quaternion.Euler(giroPitchAlvo, 0f, 0f);
                    canosDaTorreta.localRotation = Quaternion.Lerp(canosDaTorreta.localRotation, rotacaoCanos, Time.deltaTime * velocidadeGiro);
                }
                else if (canosDaTorreta == null || canosDaTorreta == pecaParaDesdobrar)
                {
                    float pitchDaPeca = inclinarPecaSemCanos ? giroPitchAlvo : 0f;
                    Quaternion rotacaoTotal = Quaternion.Euler(
                        rotacaoXOriginal + pitchDaPeca,
                        anguloY,
                        rotacaoZOriginal);
                    pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoTotal, Time.deltaTime * velocidadeGiro);
                }
            }

            // Disparo Autônomo
            contadorTempo -= Time.deltaTime;
            if (contadorTempo <= 0f)
            {
                // A permissao de tiro normalmente exige que a frente REAL do
                // cano esteja alinhada. O AC-130 pode abrir fogo enquanto
                // acompanha o alvo; nesse caso o projétil mantém a direção
                // atual do cano e pode errar, mas a torre não fica silenciosa.
                bool podeDisparar = dispararMesmoDesalinhado || EstaAlinhadaParaDisparar();

                if (podeDisparar && estaProntoParaAtirar)
                {
                    Disparar();
                    if (!estaRecarregando) contadorTempo = tempoEntreTiros;
                }
            }
        }
        else
        {
            if (usarSistemaDesdobramento && progressoDesdobramento > 0f)
            {
                progressoDesdobramento -= Time.deltaTime / tempoParaDesdobrar;
                estaProntoParaAtirar = false;
                if (pecaParaDesdobrar != null)
                {
                    pecaParaDesdobrar.localPosition = Vector3.Lerp(posRepouso, posDisparo, progressoDesdobramento);
                    pecaParaDesdobrar.localRotation = Quaternion.Lerp(Quaternion.Euler(rotRepouso), Quaternion.Euler(rotDisparo), progressoDesdobramento);
                }
            }
            ModoOcioso();
        }
    }

    private void AtualizarAgendamentoBusca()
    {
        if (modoPassivo || bloquearMovimentoAutomatico) return;
        bool selecionada = meuControle != null && meuControle.selecionado;
        bool critica = interceptarMisseis || interceptarTorpedos;
        bool emCombate = critica || alvoAtual != null || alvoPrioritario != null;
        InfraPerformanceGameplay.AtualizarEstadoBase(estadoOtimizacao, transform, selecionada, emCombate, critica);
        float intervalo = InfraPerformanceGameplay.ResolverIntervalo(
            emCombate || selecionada ? 0.20f : 0.40f,
            estadoOtimizacao,
            true,
            true);
        if (InfraPerformanceGameplay.DeveExecutar(this, ref proximaBuscaAlvo, intervalo))
        {
            ProcurarAlvo();
        }
    }

    void ModoOcioso()
    {
        if (pecaQueGira == null || bloquearRotacaoAutomatica) return;

        if (modoRadar && !limitarRotacao)
        {
            float anguloLivre = (Time.time * 20f) % 360f;
            pecaQueGira.localRotation = Quaternion.Euler(rotacaoXOriginal, anguloLivre, rotacaoZOriginal);
        }
        else
        {
            Quaternion rotacaoDescanso = rotacaoInicialPecaQueGira;
            pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoDescanso, Time.deltaTime * (velocidadeGiro * 0.5f));

            if (canosDaTorreta != null)
            {
                Quaternion repousoCanos = rotacaoInicialCanosCapturada ? rotacaoInicialCanos : Quaternion.identity;
                canosDaTorreta.localRotation = Quaternion.Lerp(canosDaTorreta.localRotation, repousoCanos, Time.deltaTime * (velocidadeGiro * 0.5f));
            }
        }
    }
    #endregion

    #region Disparo e Predição
    Vector3 ObterPosicaoPreditaAlvo(Transform alvoReferencia = null)
    {
        Transform alvoRef = alvoReferencia != null ? alvoReferencia : alvoAtual;
        if (alvoRef == null) return transform.position;
        Vector3 alvoPosicao = alvoRef.position;

        float velBala = 200f; 
        if (municaoPrefab != null)
        {
            Projetil proj = municaoPrefab.GetComponent<Projetil>();
            if (proj != null && proj.velocidade > 0f) velBala = proj.velocidade;
            
            if (municoesPorCano != null && indicacaoSeguraDeBala < municoesPorCano.Length)
            {
                 if (municoesPorCano[indicacaoSeguraDeBala] != null)
                 {
                     Projetil p2 = municoesPorCano[indicacaoSeguraDeBala].GetComponent<Projetil>();
                     if (p2 != null && p2.velocidade > 0f) velBala = p2.velocidade;
                 }
            }
        }

        Vector3 targetVel = velocidadeCalculadaAlvo;

        // OTIMIZAÇÃO: Usa o Rigidbody cacheado em SetarAlvo() em vez de GetComponent no frame!
        if (targetVel.magnitude < 0.1f && alvoAtualRb != null && !alvoAtualRb.isKinematic)
        {
            targetVel = alvoAtualRb.linearVelocity;
        }

        if (targetVel.magnitude > 0.5f)
        {
            float dist1 = Vector3.Distance(pecaQueGira.position, alvoPosicao);
            float tempoAteAlvo1 = dist1 / velBala;
            
            Vector3 predicaoPrimaria = alvoPosicao + (targetVel * tempoAteAlvo1);
            
            float dist2 = Vector3.Distance(pecaQueGira.position, predicaoPrimaria);
            float tempoAteAlvo2 = dist2 / velBala;
            
            alvoPosicao = alvoPosicao + (targetVel * tempoAteAlvo2);
        }

        return alvoPosicao;
    }

    private bool EstaAlinhadaParaDisparar()
    {
        if (alvoAtual == null)
        {
            return false;
        }

        // So use o ponto do lancador quando ele realmente sera o proximo a
        // disparar. Depois de esgotar os misseis, a torre ainda pode usar o
        // canhao sem ficar presa ao alinhamento de um lancador vazio.
        bool proximoDisparoEhMissel = misselPrefab != null
            && misseisAtuais > 0
            && !estaRecarregandoMisseis
            && cooldownMissel <= 0f;
        Transform[] saidas = proximoDisparoEhMissel && locaisDoMissel != null && locaisDoMissel.Length > 0
            ? locaisDoMissel
            : locaisDoTiro;
        if (saidas == null || saidas.Length == 0)
        {
            GarantirLocaisDeTiro();
            saidas = proximoDisparoEhMissel && locaisDoMissel != null && locaisDoMissel.Length > 0
                ? locaisDoMissel
                : locaisDoTiro;
        }
        if (saidas == null || saidas.Length == 0)
        {
            return false;
        }

        int indice = Mathf.Clamp(indiceBarrilAtual, 0, saidas.Length - 1);
        Transform saida = saidas[indice];
        if (saida == null)
        {
            return false;
        }

        Vector3 direcaoAlvo = ObterPosicaoPreditaAlvo() - saida.position;
        if (direcaoAlvo.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        // Defesa aerea pode acompanhar alvos rapidos sem abrir um cone de
        // tiro exagerado; armas navais de superficie exigem mira mais fina.
        float tolerancia = souAntiAereo ? 12f : 5f;
        return Vector3.Angle(saida.forward, direcaoAlvo) <= tolerancia;
    }

    void Disparar()
    {
        bool alvoEhMissil = ObterEhMissilComCache(alvoAtual);
        bool alvoEhTorpedo = ObterEhTorpedoComCache(alvoAtual);

        // DISPARO DE MÍSSIL (inclui interceptação de torpedos)
        if (misselPrefab != null && cooldownMissel <= 0f && !estaRecarregandoMisseis && misseisAtuais > 0 && alvoAtual != null)
        {
            bool podeUsarMissil = !alvoEhMissil && !alvoEhTorpedo;
            bool interceptandoAmeaça = (alvoEhMissil && usarMisselParaInterceptar) || alvoEhTorpedo;

            if (podeUsarMissil || interceptandoAmeaça)
            {
                DispararMisselCorrigido();
                cooldownMissel = tempoEntreMisseis;
                misseisAtuais--;

                if (misseisAtuais <= 0)
                {
                    estaRecarregandoMisseis = true;
                    contadorRecargaMissel = tempoRecargaMisseis;
                }

                if (interceptandoAmeaça) return;
                if (podeUsarMissil) return;
            }
        }

        // DISPARO PADRÃO
        GarantirLocaisDeTiro();
        if (locaisDoTiro != null && locaisDoTiro.Length > 0)
        {
            GameObject prefabParaUsar = municaoPrefab;
            if (municoesPorCano != null && indiceBarrilAtual < municoesPorCano.Length && municoesPorCano[indiceBarrilAtual] != null)
            {
                prefabParaUsar = municoesPorCano[indiceBarrilAtual];
            }

            if (prefabParaUsar == null) return;
            if (indiceBarrilAtual >= locaisDoTiro.Length) indiceBarrilAtual = 0;

            Transform barrilDaVez = locaisDoTiro[indiceBarrilAtual];

            if (barrilDaVez == null)
            {
                indiceBarrilAtual = (indiceBarrilAtual + 1) % locaisDoTiro.Length;
                return;
            }

            // O projétil segue a frente visível do cano. Quando a exigência
            // de alinhamento estiver ativa, essa direção aponta para o alvo;
            // no AC-130 ela pode estar fora do alvo de propósito.
            Vector3 direcaoDisparo = barrilDaVez.forward;
            if (direcionarProjetilParaPredicao && alvoAtual != null)
            {
                Vector3 pontoPredito = ObterPosicaoPreditaAlvo();
                Vector3 direcaoPredita = pontoPredito - barrilDaVez.position;
                if (direcaoPredita.sqrMagnitude > 0.001f)
                {
                    direcaoDisparo = direcaoPredita.normalized;
                }
            }

            Quaternion rotacaoDisparo = Quaternion.LookRotation(direcaoDisparo, Vector3.up);
            GameObject bala = PoolDeObjetosCombate.Spawn(prefabParaUsar, barrilDaVez.position, rotacaoDisparo);
            Projetil scriptBala = bala != null ? bala.GetComponent<Projetil>() : null;
            
            if (scriptBala != null)
            {
                scriptBala.SetDono(transform.root.gameObject);
                scriptBala.SetDirecao(direcaoDisparo);
                if (scriptBala.velocidade == 0) scriptBala.velocidade = 200f;
            }

            if (somTiro != null && fonteAudio != null)
            {
                AudioRuntime.ConfigurarFonteDeTiro(fonteAudio);
                fonteAudio.PlayOneShot(somTiro);
            }
            TocarEfeitoDisparo();
            AplicarRecuoDisparo();

            indiceBarrilAtual = (indiceBarrilAtual + 1) % locaisDoTiro.Length;
            balasAtuais--;
            if (balasAtuais <= 0) IniciarRecarga();
        }
    }

    void DispararMisselCorrigido()
    {
        Transform[] saidas = (locaisDoMissel != null && locaisDoMissel.Length > 0) ? locaisDoMissel : locaisDoTiro;
        if (saidas == null || saidas.Length == 0) return;
        if (indiceBarrilAtual >= saidas.Length) indiceBarrilAtual = 0;

        Transform saida = saidas[indiceBarrilAtual % saidas.Length];
        if (saida == null) return;

        GameObject missel = PoolDeObjetosCombate.Spawn(misselPrefab, saida.position, saida.rotation);
        Transform alvoResolvido = ResolverTransformAlvo(alvoAtual);
        Vector3 posicaoPredita = ObterPosicaoPreditaAlvo(alvoResolvido);
        bool inicializado = false;
        bool alvoEhTorpedo = ObterEhTorpedoComCache(alvoResolvido);

        MisselCaca misselCaca = missel.GetComponent<MisselCaca>();
        if (misselCaca != null)
        {
            misselCaca.IniciarAtaque(posicaoPredita, CalcularVelocidadeInicialMissel(saida, posicaoPredita), alvoResolvido);
            inicializado = true;
        }
        else
        {
            MisselNaval misselNaval = missel.GetComponent<MisselNaval>();
            if (misselNaval != null)
            {
                misselNaval.IniciarAtaque(posicaoPredita, alvoResolvido, transform);
                inicializado = true;
            }
            else
            {
                MissilTeleguiado guiado = missel.GetComponent<MissilTeleguiado>();
                if (guiado != null)
                {
                    guiado.DefinirAlvo(alvoResolvido);
                    inicializado = true;
                }
                else
                {
                    MisselICBM icbm = missel.GetComponent<MisselICBM>();
                    if (icbm != null)
                    {
                        icbm.IniciarLancamento(posicaoPredita);
                        inicializado = true;
                    }
                }
            }
        }

        if (!inicializado)
            ConfigurarProjetilComoMissel(missel, saida, alvoResolvido, posicaoPredita);

        if (alvoResolvido != null && ObterEhMissilComCache(alvoResolvido))
        {
            AntiMissilDetonadorProximidade detonador = missel.GetComponent<AntiMissilDetonadorProximidade>();
            if (detonador == null) detonador = missel.AddComponent<AntiMissilDetonadorProximidade>();
            detonador.alvo = alvoResolvido;
            detonador.forcarDestruicao = true;
            detonador.distanciaBaseIntercepcao = Mathf.Max(detonador.distanciaBaseIntercepcao, 8f);
        }

        MissileThreatTracker.RegistrarLancamento(missel, this, posicaoPredita, alvoResolvido, MissileThreatTracker.EstimarVelocidade(missel));

        if (somMissel != null && fonteAudio != null)
        {
            AudioRuntime.ConfigurarFonteDeMissel(fonteAudio);
            fonteAudio.PlayOneShot(somMissel);
        }
        TocarEfeitoDisparo();
        AplicarRecuoDisparo();
    }

    void InicializarRecuo()
    {
        _alvoRecuoTransform = pecaRecuo != null
            ? pecaRecuo
            : pecaQueGira;

        if (_alvoRecuoTransform == null)
        {
            // Sem uma peca de recuo explicitamente segura, nao mova a raiz
            // nem o conjunto estrutural do prefab durante o disparo.
            _recuoInicializado = true;
            return;
        }

        _recuoLocalOriginal = _alvoRecuoTransform.localPosition;
        _recuoInicializado = true;
        _recuoAtual = 0f;
    }

    void AtualizarRecuo()
    {
        if (!usarRecuoAoDisparar)
        {
            return;
        }

        if (!_recuoInicializado || _alvoRecuoTransform == null)
        {
            InicializarRecuo();
        }
        if (_alvoRecuoTransform == null) return;

        float novoRecuo = Mathf.MoveTowards(_recuoAtual, 0f, Time.deltaTime * velocidadeRetornoRecuo);
        if (!Mathf.Approximately(novoRecuo, _recuoAtual))
        {
            _recuoAtual = novoRecuo;
            _alvoRecuoTransform.localPosition = _recuoLocalOriginal + Vector3.back * (forcaRecuoDisparo * _recuoAtual);
        }
    }

    void AplicarRecuoDisparo()
    {
        if (!usarRecuoAoDisparar)
        {
            return;
        }

        if (!_recuoInicializado || _alvoRecuoTransform == null)
        {
            InicializarRecuo();
        }
        if (_alvoRecuoTransform == null) return;

        _recuoAtual = 1f;
        _alvoRecuoTransform.localPosition = _recuoLocalOriginal + Vector3.back * forcaRecuoDisparo;
    }

    void TocarEfeitoDisparo()
    {
        Transform origem = ResolverPontoEfeitoDisparo();

        if (fogoCano != null)
        {
            fogoCano.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fogoCano.Play(true);
        }

        if (fogoCanoObjeto != null)
        {
            ParticleSystem[] sistemas = fogoCanoObjeto.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < sistemas.Length; i++)
            {
                ParticleSystem sistema = sistemas[i];
                if (sistema == null) continue;
                sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                sistema.Play(true);
            }
        }

        if (efeitoCanoPrefab != null && origem != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(
                efeitoCanoPrefab,
                origem.position,
                origem.rotation,
                Mathf.Max(0.25f, duracaoEfeitoCano));
        }
    }

    void InicializarEfeitosDisparo()
    {
        if (fogoCano == null)
        {
            if (fogoCanoObjeto != null)
            {
                fogoCano = fogoCanoObjeto.GetComponentInChildren<ParticleSystem>(true);
                if (fogoCano == null)
                {
                    fogoCano = fogoCanoObjeto.GetComponent<ParticleSystem>();
                }
            }

            ParticleSystem[] sistemas = GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem melhorCandidato = null;
            int melhorPontuacao = -1;

            for (int i = 0; i < sistemas.Length; i++)
            {
                ParticleSystem sistema = sistemas[i];
                if (sistema == null) continue;

                string nome = sistema.name != null ? sistema.name.Replace(" ", string.Empty).ToLowerInvariant() : string.Empty;
                int pontuacao = 0;

                if (nome.Contains("fire") || nome.Contains("fogo")) pontuacao += 8;
                if (nome.Contains("muzzle") || nome.Contains("boca") || nome.Contains("cano")) pontuacao += 6;
                if (nome.Contains("smoke") || nome.Contains("fumaca") || nome.Contains("smoke")) pontuacao += 3;
                if (sistema.main.playOnAwake) pontuacao += 1;

                if (pontuacao > melhorPontuacao)
                {
                    melhorPontuacao = pontuacao;
                    melhorCandidato = sistema;
                }
            }

            if (melhorCandidato != null)
            {
                fogoCano = melhorCandidato;
            }
        }

        if (fogoCano != null)
        {
            fogoCano.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    Transform ResolverPontoEfeitoDisparo()
    {
        if (pontoEfeitoCano != null)
        {
            return pontoEfeitoCano;
        }

        if (locaisDoTiro != null)
        {
            for (int i = 0; i < locaisDoTiro.Length; i++)
            {
                if (locaisDoTiro[i] != null)
                {
                    return locaisDoTiro[i];
                }
            }
        }

        if (canosDaTorreta != null)
        {
            return canosDaTorreta;
        }

        if (pecaQueGira != null)
        {
            return pecaQueGira;
        }

        return transform;
    }

    void IniciarRecarga()
    {
        estaRecarregando = true;
        contadorTempo = tempoRecarga;
        if (somRecarga != null && fonteAudio != null) fonteAudio.PlayOneShot(somRecarga);
    }
    #endregion

    #region Funções de Auxílio e UI Otimizada
    // OTIMIZAÇÃO: Círculo calculado LOCALMENTE uma única vez e guardado. Sem matemática no Update!
    void CriarVisualizadorAlcance()
    {
        GameObject obj = new GameObject("Alcance_Torreta_UI");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        linhaDeAlcance = obj.AddComponent<LineRenderer>();
        linhaDeAlcance.useWorldSpace = false; // MAGIA DA OTIMIZAÇÃO
        
        Material mat = new Material(Shader.Find("Sprites/Default")); 
        Color corAmarela = Color.yellow; corAmarela.a = 0.5f; 
        linhaDeAlcance.material = mat;
        linhaDeAlcance.startColor = corAmarela; linhaDeAlcance.endColor = corAmarela;
        linhaDeAlcance.startWidth = 1.0f; linhaDeAlcance.endWidth = 1.0f;
        linhaDeAlcance.positionCount = 51;
        linhaDeAlcance.enabled = false;

        float angulo = 0f;
        for (int i = 0; i <= 50; i++)
        {
            float x = Mathf.Sin(angulo) * alcance;
            float z = Mathf.Cos(angulo) * alcance;
            linhaDeAlcance.SetPosition(i, new Vector3(x, 0.5f, z));
            angulo += (2 * Mathf.PI) / 50;
        }
    }

    void AtualizarVisualizadorAlcance()
    {
        if (linhaDeAlcance == null) return;
        bool deveMostrar = (meuControle != null && meuControle.selecionado);
        
        if (linhaDeAlcance.enabled != deveMostrar)
            linhaDeAlcance.enabled = deveMostrar;
    }

    bool DeterminarSouAntiAereo()
    {
        string nomeBase = transform.root.name.ToLower();
        string nomeObj  = transform.name.ToLower();
        return etiquetaAlvo.Equals("Aereo", System.StringComparison.OrdinalIgnoreCase) ||
               etiquetaAlvo.Equals("Areo",  System.StringComparison.OrdinalIgnoreCase) ||
               nomeBase.Contains("ares")       || nomeBase.Contains("antiaerea") ||
               nomeBase.Contains("ciws")       || nomeBase.Contains("sam")  ||
               nomeObj.Contains("ares")        || nomeObj.Contains("antiaerea") ||
               nomeObj.Contains("ciws")        || nomeObj.Contains("sam");
    }

    Transform ResolverTransformAlvo(Transform alvo)
    {
        if (alvo == null) return null;
        if (alvo.GetComponentInParent<SistemaDeDanos>() != null) return alvo.GetComponentInParent<SistemaDeDanos>().transform;
        if (alvo.GetComponentInParent<ControleAviao>() != null) return alvo.GetComponentInParent<ControleAviao>().transform;
        if (alvo.GetComponentInParent<Helicoptero>() != null) return alvo.GetComponentInParent<Helicoptero>().transform;
        if (alvo.GetComponentInParent<C700TransporteAereo>() != null) return alvo.GetComponentInParent<C700TransporteAereo>().transform;
        if (alvo.GetComponentInParent<AviaoBombardeiro>() != null) return alvo.GetComponentInParent<AviaoBombardeiro>().transform;
        if (alvo.GetComponentInParent<IdentidadeUnidade>() != null) return alvo.GetComponentInParent<IdentidadeUnidade>().transform;
        return alvo.root != null ? alvo.root : alvo;
    }

    // OTIMIZAÇÃO: Caches para aliviar o peso no loop do radar
    private IdentidadeUnidade ObterIdentidadeUnidadeComCache(Transform t)
    {
        if (t == null) return null;
        if (_idParentCache.TryGetValue(t, out IdentidadeUnidade cached)) return cached;
        if (_idParentCache.Count > 2000) _idParentCache.Clear();
        cached = t.GetComponentInParent<IdentidadeUnidade>();
        _idParentCache[t] = cached;
        return cached;
    }

    private bool ObterEhAviaoComCache(Transform t)
    {
        if (t == null) return false;
        if (_aviaoParentCache.TryGetValue(t, out bool cached)) return cached;
        if (_aviaoParentCache.Count > 2000) _aviaoParentCache.Clear();
        cached = t.GetComponentInParent<ControleAviao>() != null;
        _aviaoParentCache[t] = cached;
        return cached;
    }

    private bool ObterEhHeliComCache(Transform t)
    {
        if (t == null) return false;
        if (_heliParentCache.TryGetValue(t, out bool cached)) return cached;
        if (_heliParentCache.Count > 2000) _heliParentCache.Clear();
        cached = t.GetComponentInParent<Helicoptero>() != null;
        _heliParentCache[t] = cached;
        return cached;
    }

    private bool ObterEhC700ComCache(Transform t)
    {
        if (t == null) return false;
        if (_c700ParentCache.TryGetValue(t, out bool cached)) return cached;
        if (_c700ParentCache.Count > 2000) _c700ParentCache.Clear();
        cached = t.GetComponentInParent<C700TransporteAereo>() != null ||
                 t.GetComponentInParent<AviaoBombardeiro>() != null;
        _c700ParentCache[t] = cached;
        return cached;
    }

    private bool ObterEhMissilComCache(Transform alvo)
    {
        if (alvo == null) return false;
        if (TagSafe.Matches(alvo.gameObject, "Missil")) return true;
        
        if (_missilParentCache.TryGetValue(alvo, out bool cached)) return cached;
        if (_missilParentCache.Count > 2000) _missilParentCache.Clear();

        cached = alvo.GetComponentInParent<MissileThreatTracker>() != null ||
                 alvo.GetComponentInParent<MisselCaca>() != null ||
                 alvo.GetComponentInParent<MissilTeleguiado>() != null ||
                 alvo.GetComponentInParent<MisselICBM>() != null ||
                 alvo.GetComponentInParent<MisselNaval>() != null ||
                 alvo.GetComponentInParent<MisselSubmarino>() != null ||
                 alvo.GetComponentInParent<MisselTatico>() != null ||
                 alvo.GetComponentInParent<MisselLeopardAutomatico>() != null;

        _missilParentCache[alvo] = cached;
        return cached;
    }

    private bool ObterEhTorpedoComCache(Transform alvo)
    {
        if (alvo == null) return false;
        if (TagSafe.Matches(alvo.gameObject, "Torpedo")) return true;

        if (_torpedoParentCache.TryGetValue(alvo, out bool cached)) return cached;
        if (_torpedoParentCache.Count > 2000) _torpedoParentCache.Clear();

        // Verificar se tem componente Torpedo
        cached = alvo.GetComponentInParent<Torpedo>() != null;

        // Verificar por nome se o componente não estiver no root
        if (!cached)
        {
            string nm = alvo.name.ToLower();
            cached = nm.Contains("torpedo") || nm.Contains("torpedo");
        }

        _torpedoParentCache[alvo] = cached;
        return cached;
    }

    // Retorna o submarino/navio que lançou o torpedo detectado (se identificável)
    public Transform ObterLancadorTorpedoDetectado()
    {
        if (Time.time - tempoUltimaIdentificacao < 10f)
            return lancadorDoTorpedoDetectado;
        return null;
    }

    Transform ResolverAtiradorAereoDeProjetil(Collider hit)
    {
        if (hit == null) return null;
        Projetil projetil = hit.GetComponentInParent<Projetil>();
        if (projetil == null) return null;
        if (ObterEhMissilComCache(projetil.transform)) return null;

        GameObject donoProjetil = projetil.GetDono();
        if (donoProjetil == null) return null;

        if (donoProjetil.GetComponentInParent<ControleAviao>() != null) return donoProjetil.GetComponentInParent<ControleAviao>().transform;
        if (donoProjetil.GetComponentInParent<Helicoptero>() != null) return donoProjetil.GetComponentInParent<Helicoptero>().transform;
        if (donoProjetil.GetComponentInParent<C700TransporteAereo>() != null) return donoProjetil.GetComponentInParent<C700TransporteAereo>().transform;
        if (donoProjetil.GetComponentInParent<AviaoBombardeiro>() != null) return donoProjetil.GetComponentInParent<AviaoBombardeiro>().transform;

        return null;
    }

    Vector3 CalcularVelocidadeInicialMissel(Transform saida, Vector3 posicaoAlvo)
    {
        if (saida == null) return transform.forward * 40f;

        Vector3 direcaoInicial = posicaoAlvo - saida.position;
        if (direcaoInicial.sqrMagnitude <= 0.001f)
            direcaoInicial = saida.forward.sqrMagnitude > 0.001f ? saida.forward : transform.forward;

        direcaoInicial.Normalize();

        Rigidbody rbLancador = transform.root.GetComponent<Rigidbody>();
        Vector3 velocidadeBase = (rbLancador != null && !rbLancador.isKinematic) ? rbLancador.linearVelocity : Vector3.zero;

        if (velocidadeBase.sqrMagnitude < 25f)
            velocidadeBase = direcaoInicial * 40f;
        else
            velocidadeBase += direcaoInicial * 25f;

        return velocidadeBase;
    }

    void ConfigurarProjetilComoMissel(GameObject projetilObj, Transform saida, Transform alvo, Vector3 posicaoAlvo)
    {
        if (projetilObj == null || saida == null) return;

        Projetil projetil = projetilObj.GetComponent<Projetil>();
        if (projetil == null) return;

        Vector3 direcao = posicaoAlvo - saida.position;
        if (direcao.sqrMagnitude <= 0.001f)
            direcao = saida.forward.sqrMagnitude > 0.001f ? saida.forward : transform.forward;

        projetil.SetDono(transform.root.gameObject);
        projetil.SetDirecao(direcao.normalized);

        if (alvo != null)
        {
            projetil.SetAlvo(alvo);
            if (projetil.curvaDePerseguicao <= 0f) projetil.curvaDePerseguicao = 90f;
        }
    }

    void GarantirLocaisDeTiro()
    {
        locaisDoTiro = FiltrarLocaisValidos(locaisDoTiro);
        if (locaisDoTiro != null && locaisDoTiro.Length > 0)
        {
            GarantirCanosDaTorreta();
            return;
        }

        locaisDoTiro = DescobrirLocaisDeTiroAutomaticos();
        if (locaisDoTiro != null && locaisDoTiro.Length > 0)
        {
            if (!diagnosticoLocaisDoTiroEmitido)
            {
                Debug.LogWarning($"[ControleTorreta] '{gameObject.name}' estava sem locaisDoTiro. Fallback automatico configurado.", this);
                diagnosticoLocaisDoTiroEmitido = true;
            }
            GarantirCanosDaTorreta();
            return;
        }
    }

    void GarantirCanosDaTorreta()
    {
        if (!descobrirCanosAutomaticamente)
        {
            return;
        }

        if (canosDaTorreta != null || locaisDoTiro == null || locaisDoTiro.Length == 0)
        {
            return;
        }

        Transform pontoPrincipal = locaisDoTiro[0];
        if (pontoPrincipal == null)
        {
            return;
        }

        Transform candidato = pontoPrincipal.parent;
        while (candidato != null && candidato != transform)
        {
            if (candidato != pecaQueGira)
            {
                canosDaTorreta = candidato;
                break;
            }

            candidato = candidato.parent;
        }
    }

    Transform[] DescobrirLocaisDeTiroAutomaticos()
    {
        var encontrados = new List<Transform>();
        if (fogoCano != null) encontrados.Add(fogoCano.transform);

        Transform raizBusca = canosDaTorreta != null ? canosDaTorreta : (pecaQueGira != null ? pecaQueGira : transform);
        foreach (Transform filho in raizBusca.GetComponentsInChildren<Transform>(true))
        {
            if (filho == null || filho == raizBusca) continue;
            string nome = string.IsNullOrEmpty(filho.name) ? "" : filho.name.Replace(" ", string.Empty).ToLowerInvariant();
            if (nome.Contains("bocadetiro") || nome.Contains("muzzle") || nome.Contains("barrel") || nome.Contains("cano"))
                encontrados.Add(filho);
        }

        Transform[] validos = FiltrarLocaisValidos(encontrados.ToArray());
        if (validos != null && validos.Length > 0) return validos;

        Transform fallback = CriarLocalDeTiroFallback(raizBusca);
        return fallback != null ? new[] { fallback } : null;
    }

    Transform CriarLocalDeTiroFallback(Transform referencia)
    {
        if (referencia == null) referencia = transform;
        Transform existente = referencia.Find("_AutoLocalTiro");
        if (existente != null) return existente;

        GameObject marcador = new GameObject("_AutoLocalTiro");
        Transform ponto = marcador.transform;
        ponto.SetParent(referencia, false);
        ponto.localPosition = new Vector3(0f, 0.5f, 1.5f);
        ponto.localRotation = Quaternion.identity;
        return ponto;
    }

    static Transform[] FiltrarLocaisValidos(Transform[] origem)
    {
        if (origem == null || origem.Length == 0) return null;
        var validos = new List<Transform>(origem.Length);
        for (int i = 0; i < origem.Length; i++)
            if (origem[i] != null) validos.Add(origem[i]);
        return validos.Count > 0 ? validos.ToArray() : null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alcance);
    }
    #endregion
}
