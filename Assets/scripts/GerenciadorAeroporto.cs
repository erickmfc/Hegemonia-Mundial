using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GerenciadorAeroporto : MonoBehaviour
{
    // Estruturas comerciais removidas e transferidas para GerenciadorAeroportoComercial.cs
    
    private enum ModoOrdemHelicoptero
    {
        Nenhum,
        Reconhecimento,
        Patrulha,
        AtaqueLocal,
        Transporte
    }

    [Header("Hierarquia do Aeroporto (Vincular do Inspector)")]
    [Tooltip("Grupo pai contendo as marcações 'Parada' a 'Parada 4'")]
    public Transform patio;

    [Tooltip("Grupo adicional de vagas extras, como Patio_Militar.")]
    public Transform patioMilitar;
    
    [Tooltip("Grupo contendo 'Preparacao' e 'Pronto'")]
    public Transform hangarAviao;
    
    [Tooltip("Grupo de waypoints de decolagem: alinhamento -> decolagem -> voo...")]
    public Transform decolagem;
    
    [Tooltip("Grupo de waypoints de pouso OBRIGATÓRIOS (Decida)")]
    public Transform decida;

    [Header("Gestão de Frota e Status")]
    public List<ControleAviao> avioesNoPatio = new List<ControleAviao>();
    public List<ControleAviao> avioesNoHangar = new List<ControleAviao>();
    public List<C700TransporteAereo> transportesC700NoPatio = new List<C700TransporteAereo>();
    public List<Helicoptero> helicopterosDoAeroporto = new List<Helicoptero>();

    [Header("Drone Kamikaze")]
    public GameObject prefabDroneKamikaze;
    public int precoDroneKamikaze = 1500;

    [Header("Su-11")]
    public GameObject prefabSu11;
    public int precoSu11 = 2500;

    [Header("Marcadores")]
    public GameObject prefabMarcadorPatrulhaAviao; // Marker 5 Circle Loop
    public GameObject prefabMarcadorBombardeiro; // Marker 7 Danger zone Loop

    [Header("Interface (UI)")]
    public GameObject menuAeroportoUI;
    
    private bool menuAtivo = false;
    private int abaAtual = 0; 
    private Vector2 scrollPosFrota;
    private Vector2 scrollPosHangar;
    private Vector2 scrollPosC700;
    private Vector2 scrollPosAbaMilitar;
    [HideInInspector] public ControleAviao aviaoSelecionadoParaMissao;
    [HideInInspector] public C700TransporteAereo c700SelecionadoParaMissao;
    [HideInInspector] public Helicoptero helicopteroSelecionadoParaMissao;

    // Listas internas de Waypoints lidas no Awake
    [HideInInspector] public List<Transform> waypointsPatio = new List<Transform>();
    [HideInInspector] public Transform wpPreparacao;
    [HideInInspector] public Transform wpPronto;
    [HideInInspector] public List<Transform> waypointsDecolagem = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsTaxi = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsDecida = new List<Transform>();
    
    [HideInInspector] public Transform wpAndadar;
    [HideInInspector] public Transform wpAnalise;

    [HideInInspector] public bool esperandoCliqueMassa = false;
    [HideInInspector] public int qtdMassaDrone = 1;
    [HideInInspector] public bool esperandoCliquePatrulhaGrupo = false;
    [HideInInspector] public int qtdPatrulhaGrupo = 1;

    // --- CACHE DE COMPONENTES (Evita GetComponent repetido) ---
    protected IdentidadeUnidade _identidadeCacheada;
    protected bool _identidadeVerificada = false;

    // --- CACHE PARA OnGUI (Evita alocações repetidas) ---
    private readonly HashSet<Transform> _vagasOcupadas = new HashSet<Transform>();
    private readonly List<Vector3> _rotaPatrulhaHelicoptero = new List<Vector3>();
    private readonly List<ControleAviao> _bufferSortidaAereaIA = new List<ControleAviao>(24);
    private Camera cameraPrincipal;
    private ModoOrdemHelicoptero _modoOrdemHelicoptero = ModoOrdemHelicoptero.Nenhum;
    private string _modeloPatrulhaGrupo = string.Empty;
    private string _ultimoModeloPainelPatrulha = string.Empty;
    private bool _usarMarcadorPatrulhaAviaoNoProximoClique = false;
    private float _proximaSortidaIA = -999f;
    private float _proximoReporPatioTime = -999f;

    [Header("⚡ Energia")]
    public bool semEnergia = false;
    private bool mouseHover = false;
    private Texture2D _texturaTooltip;

    public void SetarSemEnergia(bool status)
    {
        if (semEnergia == status) return;
        semEnergia = status;
        if (semEnergia)
        {
            Debug.Log($"[ENERGIA] Aeroporto {name} está sem energia! Operações e compras bloqueadas.");
        }
    }

    [Header("Spawn IA (Antitravada)")]
    [Tooltip("Quando ativo, compras de aeronaves da IA entram em fila e sao instanciadas aos poucos para evitar queda brutal de FPS/GC.")]
    [SerializeField] private bool usarFilaSpawnAereoIA = true;
    [SerializeField] private float intervaloSpawnIaSaudavel = 0.55f;
    [SerializeField] private float intervaloSpawnIaPressao = 1.7f;
    [SerializeField] private float intervaloSpawnIaCritico = 3.5f;
    private readonly Queue<GameObject> _filaSpawnAeronavesIA = new Queue<GameObject>();
    private float _proximoSpawnAeronaveIA = -999f;
    private bool _interacaoManualSolicitada;

    protected static int RemoveNulls<T>(List<T> lista) where T : class
    {
        if (lista == null)
        {
            return 0;
        }

        int removidos = 0;
        for (int i = lista.Count - 1; i >= 0; i--)
        {
            if (lista[i] == null)
            {
                lista.RemoveAt(i);
                removidos++;
            }
        }

        return removidos;
    }

    protected virtual void Awake()
    {
#if UNITY_EDITOR
        GarantirPrefabsMarcadoresNoEditor();
#endif

        if (prefabMarcadorPatrulhaAviao != null)
        {
            PoolDeObjetosCombate.Prewarm(prefabMarcadorPatrulhaAviao, 2);
        }

        if (prefabMarcadorBombardeiro != null)
        {
            PoolDeObjetosCombate.Prewarm(prefabMarcadorBombardeiro, 2);
        }

        // Cache da identidade do aeroporto
        _identidadeCacheada = GetComponent<IdentidadeUnidade>();
        _identidadeVerificada = true;

        if (patio != null)
        {
            RegistrarWaypointsPatio(patio);
        }

        if (patioMilitar == null)
        {
            patioMilitar = EncontrarGrupoPatioMilitar();
        }

        if (patioMilitar != null && patioMilitar != patio)
        {
            RegistrarWaypointsPatio(patioMilitar);
        }

        if (hangarAviao != null)
        {
            wpPreparacao = hangarAviao.Find("Preparacao");
            wpPronto = hangarAviao.Find("Pronto");
        }

        if (decolagem != null)
        {
            foreach (Transform filho in decolagem) waypointsDecolagem.Add(filho);
        }

        // --- SISTEMA DE EMERGÊNCIA: AUTO-GERAÇÃO DE VAGAS ---
        if (waypointsPatio.Count < 24)
        {
            int vagasFaltantes = 24 - waypointsPatio.Count;
            float anguloStep = 360f / vagasFaltantes * Mathf.Deg2Rad;
            for (int i = 0; i < vagasFaltantes; i++)
            {
                GameObject vagaAuto = new GameObject($"Vaga_Auto_{i}");
                vagaAuto.transform.SetParent(patio != null ? patio : this.transform);
                float ang = i * anguloStep;
                vagaAuto.transform.localPosition = new Vector3(Mathf.Cos(ang) * 65f, 0, Mathf.Sin(ang) * 65f);
                waypointsPatio.Add(vagaAuto.transform);
            }
        }

        if (decida != null)
        {
            foreach (Transform filho in decida) waypointsDecida.Add(filho);
            // Como o objeto no Unity está do inicio (Freiada) ao fim (Alinhando)
            // e o avião entra pelo Alinhando, invertemos a lista inteira!
            waypointsDecida.Reverse();
        }

        // Tenta achar Andadar e Analise (em qualquer lugar dentro do Aeroporto)
        Transform[] todasAsTags = GetComponentsInChildren<Transform>(true);
        for (int i = 0, count = todasAsTags.Length; i < count; i++)
        {
            string nome = todasAsTags[i].name.ToLower();
            if (nome == "andadar") wpAndadar = todasAsTags[i];
            else if (nome == "analise") wpAnalise = todasAsTags[i];
            // Sai mais cedo se já encontrou ambos
            if (wpAndadar != null && wpAnalise != null) break;
        }
    }

    protected virtual void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    protected virtual void OnDisable()
    {
        GestorMenusExclusivos.Fechar(this);
        if (_interacaoManualSolicitada)
        {
            InteractionModeService.Release(this, ObterDonoInteracaoManual());
            _interacaoManualSolicitada = false;
        }
        RegistroEntidadesJogo.Unregister(this);
    }

    protected virtual void OnDestroy()
    {
        GestorMenusExclusivos.Fechar(this);
        if (_interacaoManualSolicitada)
        {
            InteractionModeService.Release(this, ObterDonoInteracaoManual());
            _interacaoManualSolicitada = false;
        }
        RegistroEntidadesJogo.Unregister(this);
    }

    protected virtual void Start()
    {
        // Inicia o serviço de reparação automática
        StartCoroutine(ManutencaoDeFrota());
    }

    private IEnumerator ManutencaoDeFrota()
    {
        WaitForSeconds espera = new WaitForSeconds(2.0f); // Reutiliza o objeto de espera
        while (true)
        {
            yield return espera;

            // Repara quem está no pátio
            for (int i = avioesNoPatio.Count - 1; i >= 0; i--)
            {
                ControleAviao a = avioesNoPatio[i];
                if (a == null) { avioesNoPatio.RemoveAt(i); continue; }
                if (a.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) continue;

                SistemaDeDanos sd = a.GetComponent<SistemaDeDanos>();
                if (sd != null && sd.vidaAtual < sd.vidaMaxima) 
                    sd.Reparar(sd.vidaMaxima * 0.05f); // 5% de vida no pátio

                ReabastecerAeronave(a, 2f);
            }

            // No hangar o reparo é prioritário
            for (int i = avioesNoHangar.Count - 1; i >= 0; i--)
            {
                ControleAviao h = avioesNoHangar[i];
                if (h == null) { avioesNoHangar.RemoveAt(i); continue; }

                SistemaDeDanos sd = h.GetComponent<SistemaDeDanos>();
                if (sd != null && sd.vidaAtual < sd.vidaMaxima) 
                    sd.Reparar(sd.vidaMaxima * 0.10f); // 10% de vida no hangar

                ReabastecerAeronave(h, 2f);
            }

            for (int i = transportesC700NoPatio.Count - 1; i >= 0; i--)
            {
                C700TransporteAereo c700 = transportesC700NoPatio[i];
                if (c700 == null) { transportesC700NoPatio.RemoveAt(i); continue; }
                if (c700.EstaNoSolo)
                {
                    ReabastecerAeronave(c700, 2f);
                }
            }

            for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
            {
                Helicoptero helicoptero = helicopterosDoAeroporto[i];
                if (helicoptero == null) { helicopterosDoAeroporto.RemoveAt(i); continue; }
                if (helicoptero.EstaEstacionadoNoAeroporto())
                {
                    ReabastecerAeronave(helicoptero, 2f);
                }
            }
        }
    }

    private void ReabastecerAeronave(Component aeronave, float deltaServico)
    {
        if (aeronave == null)
        {
            return;
        }

        CombustivelUnidade combustivel = aeronave.GetComponent<CombustivelUnidade>();
        if (combustivel == null)
        {
            combustivel = CombustivelUnidade.Garantir(aeronave.gameObject, false);
        }

        if (combustivel == null || combustivel.CombustivelAtual >= combustivel.Capacidade)
        {
            return;
        }

        ServicoAbastecimento.TentarAbastecer(combustivel, 45f * Mathf.Max(0.1f, deltaServico), out _);
    }

    private void ProcessarFilaCompraAeronavesIA()
    {
        if (!usarFilaSpawnAereoIA) return;
        if (_filaSpawnAeronavesIA == null || _filaSpawnAeronavesIA.Count == 0) return;
        if (Time.unscaledTime < _proximoSpawnAeronaveIA) return;

        if (DiagnosticoDesempenhoJogo.RuntimeSaturado())
        {
            _proximoSpawnAeronaveIA = Time.unscaledTime + Mathf.Max(0.2f, intervaloSpawnIaCritico);
            return;
        }

        GameObject prefab = _filaSpawnAeronavesIA.Dequeue();
        if (prefab != null) ComprarAviaoImediato(prefab);

        float cooldown = intervaloSpawnIaSaudavel;
        if (DiagnosticoDesempenhoJogo.RuntimeSaturado()) cooldown = intervaloSpawnIaCritico;
        else if (DiagnosticoDesempenhoJogo.RuntimeSobPressao()) cooldown = intervaloSpawnIaPressao;

        _proximoSpawnAeronaveIA = Time.unscaledTime + Mathf.Max(0.05f, cooldown);
    }

    protected virtual void Update()
    {
        ProcessarFilaCompraAeronavesIA();
        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
        RemoveNulls(helicopterosDoAeroporto);
        LimparHelicopterosTransferidos();

        // Replenish patio from hangar periodically if space is available
        if (avioesNoHangar.Count > 0 && Time.time >= _proximoReporPatioTime)
        {
            _proximoReporPatioTime = Time.time + 2.0f;
            ReporPatioComAvioesDoHangar();
        }

        if (Construtor.EmModoConstrucaoAtivo)
        {
            if (menuAtivo || aviaoSelecionadoParaMissao != null || c700SelecionadoParaMissao != null || helicopteroSelecionadoParaMissao != null)
            {
                CancelarInteracaoPorConstrucao();
            }
            AtualizarModoInteracaoManualAeroporto();
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null) return;

        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Alpha7))
        {
            if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;
            if (!_identidadeVerificada) { _identidadeCacheada = GetComponent<IdentidadeUnidade>(); _identidadeVerificada = true; }
            if (_identidadeCacheada != null && _identidadeCacheada.teamID != 1 && _identidadeCacheada.teamID != 0) return;

            bool novoEstadoMenu = !menuAtivo;
            if (novoEstadoMenu)
            {
                GestorMenusExclusivos.Abrir(this);
            }
            else
            {
                GestorMenusExclusivos.Fechar(this);
            }

            menuAtivo = novoEstadoMenu;
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(menuAtivo);
            Debug.Log("[Aeroporto] Centro de Controle " + (menuAtivo ? "ABERTO" : "FECHADO"));
        }

        if (menuAtivo && !GestorMenusExclusivos.EstaAtivo(this))
        {
            menuAtivo = false;
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
        }

        if (c700SelecionadoParaMissao != null)
        {
            if (c700SelecionadoParaMissao.AguardandoDestinoAereo && Input.GetMouseButtonDown(1))
            {
                if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;

                if (cameraPrincipal == null) return;
                Ray rC700 = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
                Vector3 pontoAlvoC700 = Vector3.zero;

                if (Physics.Raycast(rC700, out RaycastHit hitC700, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    pontoAlvoC700 = hitC700.point;
                }
                else
                {
                    UnityEngine.Plane plano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
                    if (plano.Raycast(rC700, out float distC700))
                    {
                        pontoAlvoC700 = rC700.GetPoint(distC700);
                    }
                }

                if (pontoAlvoC700 != Vector3.zero)
                {
                    c700SelecionadoParaMissao.ReceberOrdemMover(pontoAlvoC700);
                    CriarSinalizador(pontoAlvoC700, c700SelecionadoParaMissao);
                    c700SelecionadoParaMissao = null;
                }
            }
        }

        if (_modoOrdemHelicoptero != ModoOrdemHelicoptero.Nenhum && helicopteroSelecionadoParaMissao != null)
        {
            ProcessarOrdemHelicoptero();
        }

        if (aviaoSelecionadoParaMissao == null)
        {
            AtualizarModoInteracaoManualAeroporto();
            return;
        }

        bool esperandoAutorizacao = aviaoSelecionadoParaMissao.aguardandoCliqueRadar;
        bool emVooConstante = (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.EmMissao);

        if (!esperandoAutorizacao && !emVooConstante)
        {
            AtualizarModoInteracaoManualAeroporto();
            return;
        }
        if (!Input.GetMouseButtonDown(1)) return;
        if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;

        if (cameraPrincipal == null) return;
        Ray r = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        Vector3 pontoAlvo = Vector3.zero;

        if (Physics.Raycast(r, out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            pontoAlvo = hit.point;
        }
        else
        {
            UnityEngine.Plane marPlano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
            float distanciaIntersecao;
            if (marPlano.Raycast(r, out distanciaIntersecao))
            {
                pontoAlvo = r.GetPoint(distanciaIntersecao);
            }
            else
            {
                return;
            }
        }

        bool usarMarcadorPatrulhaNoClique = _usarMarcadorPatrulhaAviaoNoProximoClique;

        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;

        if (esperandoCliqueMassa)
        {
            int quantidadeMassa = qtdMassaDrone;
            LimparModoMassaAereo();
            StartCoroutine(RotinaLancarMissaoEmMassa(pontoAlvo, quantidadeMassa));
        }
        else if (esperandoCliquePatrulhaGrupo)
        {
            int quantidadeGrupo = qtdPatrulhaGrupo;
            string modeloGrupo = _modeloPatrulhaGrupo;
            LimparModoMassaAereo();
            StartCoroutine(RotinaLancarPatrulhaMesmoModelo(pontoAlvo, modeloGrupo, quantidadeGrupo));
        }
        else if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
        {
            aviaoSelecionadoParaMissao.IniciarMissaoCompleta(pontoAlvo);
            Debug.Log($"[Aeroporto] Coordenadas recebidas! {aviaoSelecionadoParaMissao.gameObject.name} decolando para: {pontoAlvo}");
        }
        else
        {
            aviaoSelecionadoParaMissao.centroDaPatrulha = pontoAlvo;
            aviaoSelecionadoParaMissao.alvoGPSVoo = pontoAlvo;
            CacaVooRealista cv = aviaoSelecionadoParaMissao.GetComponent<CacaVooRealista>();
            if (cv != null) cv.alvoGPS = pontoAlvo;
            Debug.Log($"[Aeroporto] Rota Alterada! {aviaoSelecionadoParaMissao.gameObject.name} mudando curso para: {pontoAlvo}");
        }

        CriarSinalizadorAereoNoAlvo(pontoAlvo, aviaoSelecionadoParaMissao, usarMarcadorPatrulhaNoClique);
        aviaoSelecionadoParaMissao = null;
        AtualizarModoInteracaoManualAeroporto();
    }

    public void CancelarInteracaoPorConstrucao()
    {
        if (aviaoSelecionadoParaMissao != null)
        {
            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
        }

        if (helicopteroSelecionadoParaMissao != null)
        {
            CancelarModoHelicoptero();
        }

        if (c700SelecionadoParaMissao != null)
        {
            c700SelecionadoParaMissao.CancelarModoAereo();
        }

        aviaoSelecionadoParaMissao = null;
        c700SelecionadoParaMissao = null;
        helicopteroSelecionadoParaMissao = null;
        _modoOrdemHelicoptero = ModoOrdemHelicoptero.Nenhum;
        _rotaPatrulhaHelicoptero.Clear();
        menuAtivo = false;
        LimparModoMassaAereo();

        if (menuAeroportoUI != null)
        {
            menuAeroportoUI.SetActive(false);
        }

        AtualizarModoInteracaoManualAeroporto();
    }

    public virtual bool PossuiOrdemManualAtiva()
    {
        bool aviaoAguardandoClique = aviaoSelecionadoParaMissao != null && aviaoSelecionadoParaMissao.aguardandoCliqueRadar;
        bool c700AguardandoClique = c700SelecionadoParaMissao != null && c700SelecionadoParaMissao.AguardandoDestinoAereo;
        bool helicopteroAguardandoClique = _modoOrdemHelicoptero != ModoOrdemHelicoptero.Nenhum && helicopteroSelecionadoParaMissao != null;
        return aviaoAguardandoClique || c700AguardandoClique || helicopteroAguardandoClique || esperandoCliqueMassa || esperandoCliquePatrulhaGrupo;
    }

    protected virtual InteractionOwner ObterDonoInteracaoManual()
    {
        return InteractionOwner.AirportOrder;
    }

    protected void AtualizarModoInteracaoManualAeroporto()
    {
        InteractionOwner owner = ObterDonoInteracaoManual();
        if (PossuiOrdemManualAtiva())
        {
            if (!_interacaoManualSolicitada || !InteractionModeService.IsActive(this, owner))
            {
                InteractionModeService.Request(
                    this,
                    owner,
                    new InteractionPolicy
                    {
                        bloqueiaSelecao = true,
                        bloqueiaOrdemMundo = true,
                        bloqueiaRotacaoCamera = true,
                        consomeLMB = false,
                        consomeRMB = true
                    },
                    "Ordem manual aerea aguardando clique");
            }
            _interacaoManualSolicitada = true;
        }
        else
        {
            if (_interacaoManualSolicitada)
            {
                InteractionModeService.Release(this, owner);
                _interacaoManualSolicitada = false;
            }
        }
    }

    private void ProcessarOrdemHelicoptero()
    {
        if (cameraPrincipal == null || helicopteroSelecionadoParaMissao == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelarModoHelicoptero();
            return;
        }

        if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Patrulha)
        {
            if (Input.GetMouseButtonDown(0))
            {
                CancelarModoHelicoptero();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                EncerrarModoHelicoptero();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace) && _rotaPatrulhaHelicoptero.Count > 0)
            {
                _rotaPatrulhaHelicoptero.RemoveAt(_rotaPatrulhaHelicoptero.Count - 1);
                if (_rotaPatrulhaHelicoptero.Count > 0)
                {
                    helicopteroSelecionadoParaMissao.IniciarPatrulhaAeroporto(_rotaPatrulhaHelicoptero);
                }
                else
                {
                    helicopteroSelecionadoParaMissao.CancelarMissaoAeroporto();
                }
                return;
            }
        }

        if (!Input.GetMouseButtonDown(1)) return;
        if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;

        if (!TryResolverPontoMapa(out Vector3 pontoAlvo))
        {
            return;
        }

        if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Patrulha)
        {
            _rotaPatrulhaHelicoptero.Add(pontoAlvo);
            helicopteroSelecionadoParaMissao.IniciarPatrulhaAeroporto(_rotaPatrulhaHelicoptero);
            CriarSinalizador(pontoAlvo, helicopteroSelecionadoParaMissao);
            Debug.Log($"[Aeroporto] Patrulha atualizada com {_rotaPatrulhaHelicoptero.Count} ponto(s). Clique direito adiciona mais, ENTER encerra edição.");
            return;
        }

        if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Reconhecimento)
        {
            helicopteroSelecionadoParaMissao.IniciarReconhecimentoAeroporto(pontoAlvo);
        }
        else if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.AtaqueLocal)
        {
            helicopteroSelecionadoParaMissao.IniciarAtaqueLocalAeroporto(pontoAlvo);
        }
        else if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Transporte)
        {
            helicopteroSelecionadoParaMissao.IniciarTransporteAeroporto(pontoAlvo);
        }

        CriarSinalizador(pontoAlvo, helicopteroSelecionadoParaMissao);
        EncerrarModoHelicoptero();
    }

    private bool TryResolverPontoMapa(out Vector3 pontoAlvo)
    {
        pontoAlvo = Vector3.zero;
        if (cameraPrincipal == null)
        {
            return false;
        }

        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(raio, out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            pontoAlvo = hit.point;
            return true;
        }

        UnityEngine.Plane plano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
        if (plano.Raycast(raio, out float distancia))
        {
            pontoAlvo = raio.GetPoint(distancia);
            return true;
        }

        return false;
    }

    private void IniciarModoHelicoptero(Helicoptero helicoptero, ModoOrdemHelicoptero modo)
    {
        if (helicoptero == null)
        {
            return;
        }

        helicopteroSelecionadoParaMissao = helicoptero;
        _modoOrdemHelicoptero = modo;
        _rotaPatrulhaHelicoptero.Clear();
        aviaoSelecionadoParaMissao = null;
        c700SelecionadoParaMissao = null;

        if (modo == ModoOrdemHelicoptero.Patrulha)
        {
            Debug.Log("[Aeroporto] Patrulha armada. O primeiro clique direito já inicia; cliques seguintes expandem a rota. ENTER encerra edição.");
        }
        else
        {
            string textoModo = "Ataque local";
            if (modo == ModoOrdemHelicoptero.Reconhecimento) textoModo = "Reconhecimento";
            else if (modo == ModoOrdemHelicoptero.Transporte) textoModo = "Transporte tático";
            Debug.Log($"[Aeroporto] {textoModo} armado para o helicóptero. Clique com o botão direito no destino.");
        }

        menuAtivo = false;
        if (menuAeroportoUI != null)
        {
            menuAeroportoUI.SetActive(false);
        }

        AtualizarModoInteracaoManualAeroporto();
    }

    private void CancelarModoHelicoptero()
    {
        _rotaPatrulhaHelicoptero.Clear();
        _modoOrdemHelicoptero = ModoOrdemHelicoptero.Nenhum;
        helicopteroSelecionadoParaMissao = null;
        AtualizarModoInteracaoManualAeroporto();
    }

    private void EncerrarModoHelicoptero()
    {
        _rotaPatrulhaHelicoptero.Clear();
        _modoOrdemHelicoptero = ModoOrdemHelicoptero.Nenhum;
        helicopteroSelecionadoParaMissao = null;
        AtualizarModoInteracaoManualAeroporto();
    }

    protected void CriarSinalizador(Vector3 pos, Component aviao)
    {
        bool ehBombardeiroOuKamikaze = false;
        if (aviao != null) 
        {
            ehBombardeiroOuKamikaze = (aviao.GetComponent<AviaoBombardeiro>() != null || aviao.GetComponent<KamikazeDrone>() != null);
        }

        if (ehBombardeiroOuKamikaze && prefabMarcadorBombardeiro != null)
        {
            GameObject marcadorBombardeiro = InstanciarMarcadorSeguro(prefabMarcadorBombardeiro, pos, Quaternion.identity);
            if (marcadorBombardeiro != null)
            {
                marcadorBombardeiro.transform.localScale = new Vector3(40f, 40f, 40f);
                return;
            }
        }

        // Cria um feixe/pilar de cristal alto indicando o ponto ordenado
        GameObject sinal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(sinal.GetComponent<Collider>()); // Remover colisão para não ferrar física
        sinal.transform.position = pos + new Vector3(0, 50f, 0); 
        sinal.transform.localScale = new Vector3(4f, 100f, 4f); // Cilindro gigante visível de longe
        
        // Pinta da cor do esquadrão ou de Turquesa 
        Color c = new Color(0, 1, 1, 0.4f);
        if (aviao != null)
        {
            ControleAviaoCaca cc = aviao.GetComponent<ControleAviaoCaca>();
            if (cc != null) c = new Color(cc.corIdentificacao.r, cc.corIdentificacao.g, cc.corIdentificacao.b, 0.4f);
        }
        
        Renderer rend = sinal.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = c;
        }

        // Animação e Fade suave
        StartCoroutine(AnimarSinalizador(sinal, c));
    }

    private void CriarSinalizadorAereoNoAlvo(Vector3 pos, Component aviao, bool forcarMarcadorPatrulha = false)
    {
        bool usarMarcadorPatrulha = forcarMarcadorPatrulha || _usarMarcadorPatrulhaAviaoNoProximoClique;
        _usarMarcadorPatrulhaAviaoNoProximoClique = false;

        if (usarMarcadorPatrulha && prefabMarcadorPatrulhaAviao != null)
        {
            GameObject marcadorPatrulha = InstanciarMarcadorSeguro(prefabMarcadorPatrulhaAviao, pos + Vector3.up * 0.1f, Quaternion.identity);
            if (marcadorPatrulha != null)
            {
                return;
            }
        }

        CriarSinalizador(pos, aviao);
    }

    private GameObject InstanciarMarcadorSeguro(UnityEngine.Object prefab, Vector3 posicao, Quaternion rotacao)
    {
        if (prefab == null)
        {
            return null;
        }

        if (prefab is GameObject prefabGo)
        {
            return PoolDeObjetosCombate.SpawnTemporario(prefabGo, posicao, rotacao, 4f);
        }

        UnityEngine.Object instancia = Instantiate(prefab, posicao, rotacao);
        if (instancia is GameObject go)
        {
            return go;
        }

        if (instancia is Component componente)
        {
            return componente.gameObject;
        }

        Debug.LogWarning($"[Aeroporto] Marcador instanciado com tipo inesperado: {instancia.GetType().Name}");
        return null;
    }

    private IEnumerator AnimarSinalizador(GameObject sinal, Color baseColor)
    {
        Renderer rend = sinal.GetComponent<Renderer>();
        float t = 0;
        const float duracao = 3.5f;
        while (t < duracao)
        {
            if (sinal == null) break;
            t += Time.deltaTime;
            sinal.transform.Rotate(0, 180f * Time.deltaTime, 0); // Gira o pilar loucamente
            
            // Pisca e apaga usando Fade no shader default alpha
            if (rend != null && rend.material != null)
            {
                baseColor.a = Mathf.Lerp(0.5f, 0f, t / duracao);
                rend.material.color = baseColor;
            }
            yield return null;
        }
        if (sinal != null) Destroy(sinal);
    }

    public void ComprarAviao(GameObject prefabDeAeronave)
    {
        if (prefabDeAeronave == null)
        {
            return;
        }

        if (semEnergia)
        {
            Debug.LogWarning($"[Aeroporto] {name} está sem energia! Compra de aeronaves bloqueada (Ignorado pelo patch).");
            // return;
        }

        // --- SISTEMA DE IDENTIDADE (HERANÇA DO AEROPORTO) ---
        if (!_identidadeVerificada)
        {
            _identidadeCacheada = GetComponent<IdentidadeUnidade>();
            _identidadeVerificada = true;
        }

        bool aeroportoEhIA = _identidadeCacheada != null && _identidadeCacheada.teamID > 1;

        // Para IA, enfileira para não spawnar vários aviões no mesmo segundo e travar o jogo.
        if (aeroportoEhIA && usarFilaSpawnAereoIA)
        {
            _filaSpawnAeronavesIA.Enqueue(prefabDeAeronave);
            ProcessarFilaCompraAeronavesIA();
            return;
        }

        ComprarAviaoImediato(prefabDeAeronave);
    }


    private void ComprarAviaoImediato(GameObject prefabDeAeronave)
    {
        if (prefabDeAeronave == null)
        {
            return;
        }

        long spawnStart = System.Diagnostics.Stopwatch.GetTimestamp();

        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("spawn_prefab_name", prefabDeAeronave.name);
        Vector3 posSpawn = (wpPreparacao != null) ? wpPreparacao.position : transform.position;
        GameObject aeronaveNascente = Instantiate(prefabDeAeronave, posSpawn, Quaternion.identity);

        // Mede init pós-instantiate (o custo total do spawn fica em spawn_air_ms).
        long initStart = System.Diagnostics.Stopwatch.GetTimestamp();

        // --- SISTEMA DE IDENTIDADE (HERANÇA DO AEROPORTO) ---
        if (!_identidadeVerificada)
        {
            _identidadeCacheada = GetComponent<IdentidadeUnidade>();
            _identidadeVerificada = true;
        }

        IdentidadeUnidade idAviao = aeronaveNascente != null ? aeronaveNascente.GetComponent<IdentidadeUnidade>() : null;
        if (aeronaveNascente != null && idAviao == null) idAviao = aeronaveNascente.AddComponent<IdentidadeUnidade>();

        if (_identidadeCacheada != null && idAviao != null)
        {
            idAviao.teamID = _identidadeCacheada.teamID;
            idAviao.nomeDoPais = _identidadeCacheada.nomeDoPais;
            idAviao.tipoUnidade = TipoUnidade.Aereo;

            // Se pertencer à IA (Time 2 ou maior), empurra o avião pra mente dela
            if (idAviao.teamID > 1)
            {
                // Busca o general correto que comanda este time específico
                IA_General_Pro gen = IA_ComandanteRegistry.GetGeneralByTeam(idAviao.teamID);
                if (gen != null)
                {
                    gen.RegistrarUnidade(aeronaveNascente);
                }

                DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("spawn_registrations");
            }
        }
        else if (idAviao != null)
        {
            idAviao.tipoUnidade = TipoUnidade.Aereo;
        }

        CombustivelUnidade.Garantir(aeronaveNascente, true);

        C700TransporteAereo c700 = aeronaveNascente != null ? aeronaveNascente.GetComponent<C700TransporteAereo>() : null;
        if (c700 != null)
        {
            c700.DefinirAeroportoOrigem(this);
            StartCoroutine(RotinaRecebimentoC700(c700));
            RegistrarTempoDiagnostico("prefab_init_ms", initStart);
            RegistrarTempoDiagnostico("spawn_air_ms", spawnStart);
            return;
        }

        Helicoptero helicoptero = aeronaveNascente != null ? aeronaveNascente.GetComponent<Helicoptero>() : null;
        if (helicoptero != null)
        {
            StartCoroutine(RotinaRecebimentoHelicoptero(helicoptero));
            RegistrarTempoDiagnostico("prefab_init_ms", initStart);
            RegistrarTempoDiagnostico("spawn_air_ms", spawnStart);
            return;
        }

        ControleAviao controleDaNave = aeronaveNascente != null ? aeronaveNascente.GetComponent<ControleAviao>() : null;
        if (aeronaveNascente != null && controleDaNave == null) controleDaNave = aeronaveNascente.AddComponent<ControleAviao>();

        if (controleDaNave != null)
        {
            controleDaNave.aeroportoOrigem = this;
            Transform vaga = ObterPrimeiraVagaLivre();
            if (vaga != null)
            {
                controleDaNave.vagaRetorno = vaga;
                if (!avioesNoPatio.Contains(controleDaNave)) avioesNoPatio.Add(controleDaNave);
            }
            StartCoroutine(RotinaRecebimento(controleDaNave));
        }

        RegistrarTempoDiagnostico("prefab_init_ms", initStart);
        RegistrarTempoDiagnostico("spawn_air_ms", spawnStart);
    }

    public int ExecutarSortidaIA(Vector3 alvoReconhecimento, Vector3 alvoPatrulha, Vector3 alvoAtaque, int quantidadeMaxima = 5)
    {
        if (Time.time < _proximaSortidaIA)
        {
            return 0;
        }

        if (quantidadeMaxima <= 0)
        {
            quantidadeMaxima = 1;
        }

        ReporPatioComAvioesDoHangar();
        _bufferSortidaAereaIA.Clear();
        ColetarAeronavesProntas(_bufferSortidaAereaIA);
        if (_bufferSortidaAereaIA.Count == 0)
        {
            _proximaSortidaIA = Time.time + 20f;
            return 0;
        }

        const int reservaMinimaNoAeroporto = 1;
        int disponiveisParaLancar = Mathf.Max(0, _bufferSortidaAereaIA.Count - reservaMinimaNoAeroporto);
        if (disponiveisParaLancar <= 0)
        {
            _proximaSortidaIA = Time.time + 12f;
            return 0;
        }

        int limiteLote = Mathf.Min(Mathf.Max(1, quantidadeMaxima), disponiveisParaLancar);
        int lote = 1;
        if (limiteLote >= 2)
        {
            bool ataqueEmGrupo = Random.value < 0.45f;
            lote = ataqueEmGrupo ? Random.Range(2, Mathf.Min(4, limiteLote) + 1) : 1;
        }

        int lancados = 0;

        for (int i = 0; i < lote && _bufferSortidaAereaIA.Count > 0; i++)
        {
            int indice = Random.Range(0, _bufferSortidaAereaIA.Count);
            ControleAviao aviao = _bufferSortidaAereaIA[indice];
            _bufferSortidaAereaIA.RemoveAt(indice);
            if (LancarAeronaveIA(aviao, alvoReconhecimento, alvoPatrulha, alvoAtaque))
            {
                lancados++;
            }
        }

        if (lancados > 0)
        {
            _proximaSortidaIA = Time.time + (lancados > 1 ? Random.Range(24f, 36f) : Random.Range(18f, 28f));
            ReporPatioComAvioesDoHangar();
        }
        else
        {
            _proximaSortidaIA = Time.time + 10f;
        }

        return lancados;
    }

    private static void RegistrarTempoDiagnostico(string chave, long inicio)
    {
        float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - inicio) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (elapsedMs > 0f)
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(chave, elapsedMs);
        }
    }

    protected IEnumerator RotinaRecebimento(ControleAviao aviao)
    {
        if (aviao == null) yield break;

        Transform vagaDesignada = aviao.vagaRetorno != null ? aviao.vagaRetorno : ObterPrimeiraVagaLivre();
        
        if (vagaDesignada == null)
        {
            if (avioesNoPatio.Contains(aviao)) avioesNoPatio.Remove(aviao);
            if (!avioesNoHangar.Contains(aviao)) avioesNoHangar.Add(aviao);
            aviao.estadoAtual = ControleAviao.EstadoAviao.ReservaHangar;
            aviao.gameObject.SetActive(false); 
            yield break;
        }

        aviao.vagaRetorno = vagaDesignada;
        if (!avioesNoPatio.Contains(aviao)) avioesNoPatio.Add(aviao);

        // Vai devagarzinho do Hangar até a frente do Hangar
        if (wpPronto != null)
        {
            yield return StartCoroutine(aviao.MoverInterpolado(Vector3.zero, aviao.velocidadeSolo, false, wpPronto));
        }

        if (aviao == null) yield break;
        
        // Vai devagarzinho pra Vaga do Pátio
        yield return StartCoroutine(aviao.MoverInterpolado(Vector3.zero, aviao.velocidadeSolo, false, vagaDesignada));
        
        if (aviao != null) aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
    }

    private IEnumerator RotinaRecebimentoHelicoptero(Helicoptero helicoptero)
    {
        if (helicoptero == null) yield break;

        Transform vagaHelicoptero = ObterVagaHelicopteroPreferencial(false);
        if (vagaHelicoptero == null)
        {
            // Sem vaga para o helicóptero, oculta ele imediatamente no hangar
            helicoptero.gameObject.SetActive(false);
            RegistrarHelicopteroControlado(helicoptero);
            yield break;
        }

        helicoptero.VincularAoAeroporto(this, vagaHelicoptero);
        helicoptero.PosicionarInstantaneamenteNaVagaAeroporto(vagaHelicoptero);

        GerenciadorPortaAvioes carrier = this as GerenciadorPortaAvioes;
        if (carrier != null)
        {
            helicoptero.FixarEmVagaMovel(vagaHelicoptero, carrier.transform);
        }

        RegistrarHelicopteroControlado(helicoptero);
    }

    public virtual void RegistrarHelicopteroControlado(Helicoptero helicoptero)
    {
        if (helicoptero == null)
        {
            return;
        }

        if (!helicopterosDoAeroporto.Contains(helicoptero))
        {
            helicopterosDoAeroporto.Add(helicoptero);
        }
    }

    private IEnumerator RotinaRecebimentoC700(C700TransporteAereo aviao)
    {
        if (aviao == null) yield break;

        Transform paradaGrande = ObterParadaGrandePreferencial(false);
        if (paradaGrande == null)
        {
            paradaGrande = ObterPrimeiraVagaLivre();
        }
        if (paradaGrande == null)
        {
            paradaGrande = ObterParadaGrandePreferencial(true);
        }

        if (paradaGrande == null)
        {
            // Sem vaga para C700, manda para a reserva (invisível)
            aviao.gameObject.SetActive(false);
            yield break;
        }

        if (wpPronto != null)
        {
            yield return StartCoroutine(aviao.TaxiarAteTransform(wpPronto));
        }

        if (aviao == null) yield break;

        aviao.RegistrarPontoEstacionamento(paradaGrande);
        if (!transportesC700NoPatio.Contains(aviao)) transportesC700NoPatio.Add(aviao);
        yield return StartCoroutine(aviao.TaxiarAteTransform(paradaGrande));
        aviao.FinalizarPosicionamentoNoPatio(paradaGrande);
    }

    public IEnumerator RotinaLancarMissaoEmMassa(Vector3 alvo, int quantidade)
    {
        int lancados = 0;
        quantidade = Mathf.Max(1, quantidade);
        
        while (lancados < quantidade)
        {
            ControleAviao proximo = avioesNoPatio.Find(a => a != null && a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio && a.GetComponent<KamikazeDrone>() != null);
            
            if (proximo == null)
            {
                proximo = avioesNoHangar.Find(a => a != null && a.GetComponent<KamikazeDrone>() != null);
                if (proximo != null)
                {
                    PrepararAviaoReservaParaLancamento(proximo);
                }
            }

            if (proximo == null)
            {
                Debug.LogWarning("[Ataque Massa] Sem kamikazes suficientes na reserva ou pátio!");
                break;
            }

            proximo.IniciarMissaoCompleta(alvo);
            lancados++;
            
            if (lancados < quantidade) yield return new WaitForSeconds(5f);
        }
    }

    private IEnumerator RotinaLancarPatrulhaMesmoModelo(Vector3 alvo, string modelo, int quantidade)
    {
        if (string.IsNullOrEmpty(modelo))
        {
            yield break;
        }

        int lancados = 0;
        quantidade = Mathf.Max(1, quantidade);

        while (lancados < quantidade)
        {
            ControleAviao proximo = avioesNoPatio.Find(a =>
                a != null &&
                a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio &&
                EhMesmoModelo(a, modelo));

            if (proximo == null)
            {
                proximo = avioesNoHangar.Find(a => a != null && EhMesmoModelo(a, modelo));
                if (proximo != null)
                {
                    PrepararAviaoReservaParaLancamento(proximo);
                }
            }

            if (proximo == null)
            {
                Debug.LogWarning($"[Patrulha Grupo] Sem aeronaves suficientes do modelo {modelo} no pátio ou hangar.");
                yield break;
            }

            ConfigurarAviaoParaPatrulhaEmGrupo(proximo);
            proximo.IniciarMissaoCompleta(alvo);
            lancados++;

            if (lancados < quantidade)
            {
                yield return new WaitForSeconds(5f);
            }
        }
    }

    public Transform ObterPrimeiraVagaLivre()
    {
        if (waypointsPatio == null || waypointsPatio.Count == 0) return null;

        _vagasOcupadas.Clear();
        for (int i = avioesNoPatio.Count - 1; i >= 0; i--)
        {
            ControleAviao av = avioesNoPatio[i];
            if (av == null) { avioesNoPatio.RemoveAt(i); continue; }
            if (av.vagaRetorno != null) _vagasOcupadas.Add(av.vagaRetorno);
        }

        for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null)
            {
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            Transform vagaHeli = heli.ObterVagaAeroporto();
            if (vagaHeli != null && heli.EstaEstacionadoNoAeroporto())
            {
                _vagasOcupadas.Add(vagaHeli);
            }
        }

        for (int i = 0, count = waypointsPatio.Count; i < count; i++)
        {
            Transform wp = waypointsPatio[i];
            if (wp == null || _vagasOcupadas.Contains(wp)) continue;

            bool ocupadoPorC700 = false;
            Collider[] hits = Physics.OverlapSphere(wp.position, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h] == null) continue;

                C700TransporteAereo transporte = hits[h].GetComponentInParent<C700TransporteAereo>();
                if (transporte != null)
                {
                    ocupadoPorC700 = true;
                    break;
                }
            }

            if (!ocupadoPorC700) return wp;
        }
        return null;
    }

    public Transform ObterVagaHelicopteroPreferencial(bool aceitarOcupada = false)
    {
        Transform vagaEncontrada = ProcurarVagaHelicopteroEmRaiz(patio != null ? patio : transform, aceitarOcupada);
        if (vagaEncontrada == null && patio != transform)
        {
            vagaEncontrada = ProcurarVagaHelicopteroEmRaiz(transform, aceitarOcupada);
        }

        return vagaEncontrada;
    }

    protected bool HelicopteroPertenceAEstaBase(Helicoptero heli)
    {
        if (heli == null)
        {
            return false;
        }

        Transform vaga = heli.ObterVagaAeroporto();
        return vaga != null && (vaga == transform || vaga.IsChildOf(transform));
    }

    protected void LimparHelicopterosTransferidos()
    {
        for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null)
            {
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            if (HelicopteroPertenceAEstaBase(heli))
            {
                continue;
            }

            if (helicopteroSelecionadoParaMissao == heli)
            {
                helicopteroSelecionadoParaMissao = null;
            }

            helicopterosDoAeroporto.RemoveAt(i);
        }
    }

    private Transform ProcurarVagaHelicopteroEmRaiz(Transform raizBusca, bool aceitarOcupada)
    {
        if (raizBusca == null)
        {
            return null;
        }

        Transform[] filhos = raizBusca.GetComponentsInChildren<Transform>(true);
        List<Transform> vagasPatioMilitar = new List<Transform>();
        List<Transform> vagasGerais = new List<Transform>();

        for (int i = 0; i < filhos.Length; i++)
        {
            Transform candidato = filhos[i];
            if (candidato == null) continue;

            string nome = candidato.name.ToLowerInvariant();
            if (!NomeEhVagaHelicopteroMilitar(nome)) continue;

            Transform pai = candidato.parent;
            if (pai != null && pai.name.ToLowerInvariant().Contains("patio_militar"))
            {
                vagasPatioMilitar.Add(candidato);
            }
            else
            {
                vagasGerais.Add(candidato);
            }
        }

        vagasPatioMilitar.Sort(CompararVagasHelicopteroMilitar);
        vagasGerais.Sort(CompararVagasHelicopteroMilitar);

        Transform vagaLivrePatio = EncontrarPrimeiraVagaHelicoptero(vagasPatioMilitar, aceitarOcupada);
        if (vagaLivrePatio != null)
        {
            return vagaLivrePatio;
        }

        return EncontrarPrimeiraVagaHelicoptero(vagasGerais, aceitarOcupada);
    }

    private static bool NomeEhVagaHelicopteroMilitar(string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            return false;
        }

        return nome == "h" || nome == "i" || nome == "j" || nome == "k" || nome == "l" || nome == "q"
            || nome == "vaga_h" || nome == "vaga_i" || nome == "vaga_j" || nome == "vaga_k" || nome == "vaga_l" || nome == "vaga_q"
            || nome.StartsWith("vaga_h_") || nome.StartsWith("vaga_i_") || nome.StartsWith("vaga_j_")
            || nome.StartsWith("vaga_k_") || nome.StartsWith("vaga_l_") || nome.StartsWith("vaga_q_");
    }

    private Transform EncontrarPrimeiraVagaHelicoptero(List<Transform> vagas, bool aceitarOcupada)
    {
        if (vagas == null || vagas.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < vagas.Count; i++)
        {
            Transform vaga = vagas[i];
            if (vaga == null)
            {
                continue;
            }

            if (aceitarOcupada || !VagaHelicopteroOcupada(vaga))
            {
                return vaga;
            }
        }

        return null;
    }

    private static int CompararVagasHelicopteroMilitar(Transform a, Transform b)
    {
        int ordemA = OrdemVagaHelicopteroMilitar(a != null ? a.name : string.Empty);
        int ordemB = OrdemVagaHelicopteroMilitar(b != null ? b.name : string.Empty);
        if (ordemA != ordemB)
        {
            return ordemA.CompareTo(ordemB);
        }

        string nomeA = a != null ? a.name : string.Empty;
        string nomeB = b != null ? b.name : string.Empty;
        return string.Compare(nomeA, nomeB, System.StringComparison.OrdinalIgnoreCase);
    }

    private static int OrdemVagaHelicopteroMilitar(string nomeOriginal)
    {
        string nome = string.IsNullOrEmpty(nomeOriginal) ? string.Empty : nomeOriginal.ToLowerInvariant();
        switch (nome)
        {
            case "h":
            case "vaga_h":
                return 0;
            case "i":
            case "vaga_i":
                return 1;
            case "j":
            case "vaga_j":
                return 2;
            case "k":
            case "vaga_k":
                return 3;
            case "l":
            case "vaga_l":
                return 4;
            case "q":
            case "vaga_q":
                return 5;
            default:
                if (nome.StartsWith("vaga_h_")) return 0;
                if (nome.StartsWith("vaga_i_")) return 1;
                if (nome.StartsWith("vaga_j_")) return 2;
                if (nome.StartsWith("vaga_k_")) return 3;
                if (nome.StartsWith("vaga_l_")) return 4;
                if (nome.StartsWith("vaga_q_")) return 5;
                return 99;
        }
    }

    private bool VagaHelicopteroOcupada(Transform vaga)
    {
        if (vaga == null)
        {
            return false;
        }

        for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null)
            {
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            if (!heli.EstaEstacionadoNoAeroporto())
            {
                continue;
            }

            if (heli.ObterVagaAeroporto() == vaga)
            {
                return true;
            }

            Vector3 diff = heli.transform.position - vaga.position;
            diff.y = 0f;
            if (diff.sqrMagnitude <= 36f)
            {
                return true;
            }
        }

        return false;
    }

    private void RegistrarWaypointsPatio(Transform grupo)
    {
        if (grupo == null)
        {
            return;
        }

        foreach (Transform filho in grupo)
        {
            if (filho == null)
            {
                continue;
            }

            string nome = filho.name.ToLowerInvariant();
            if (!NomeEhVagaAviaoExtra(nome))
            {
                continue;
            }

            if (!waypointsPatio.Contains(filho))
            {
                waypointsPatio.Add(filho);
            }
        }
    }

    private Transform EncontrarGrupoPatioMilitar()
    {
        Transform[] todos = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < todos.Length; i++)
        {
            Transform candidato = todos[i];
            if (candidato == null)
            {
                continue;
            }

            string nome = candidato.name.ToLowerInvariant().Replace(" ", string.Empty);
            if (nome.Contains("patio_militar") || nome.Contains("patiomilitar"))
            {
                return candidato;
            }
        }

        return null;
    }

    private static bool NomeEhVagaAviaoExtra(string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            return false;
        }

        if (nome.Contains("parada"))
        {
            return true;
        }

        return nome == "h" || nome == "i" || nome == "j" || nome == "k" || nome == "l" || nome == "q"
            || nome == "vaga_h" || nome == "vaga_i" || nome == "vaga_j" || nome == "vaga_k" || nome == "vaga_l" || nome == "vaga_q"
            || nome.StartsWith("vaga_h_") || nome.StartsWith("vaga_i_") || nome.StartsWith("vaga_j_")
            || nome.StartsWith("vaga_k_") || nome.StartsWith("vaga_l_") || nome.StartsWith("vaga_q_");
    }

    public Transform ObterParadaGrandePreferencial(bool aceitarOcupada = false)
    {
        Transform encontrada = null;

        if (waypointsPatio != null)
        {
            for (int i = 0; i < waypointsPatio.Count; i++)
            {
                Transform wp = waypointsPatio[i];
                if (wp != null && wp.name.ToLowerInvariant().Contains("parada_grande"))
                {
                    encontrada = wp;
                    break;
                }
            }
        }

        if (encontrada == null)
        {
            Transform[] filhos = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < filhos.Length; i++)
            {
                if (filhos[i] != null && filhos[i].name.ToLowerInvariant().Contains("parada_grande"))
                {
                    encontrada = filhos[i];
                    break;
                }
            }
        }

        if (encontrada == null)
        {
            return null;
        }

        if (aceitarOcupada)
        {
            return encontrada;
        }

        Collider[] hits = Physics.OverlapSphere(encontrada.position, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            C700TransporteAereo transporte = hits[i].GetComponentInParent<C700TransporteAereo>();
            if (transporte != null)
            {
                return null;
            }
        }

        return encontrada;
    }

    // --- HELPER: Extrai texto formatado de um avião para OnGUI (evita código repetido) ---
    private string ObterInfoAviao(Component aviao, out string corCristal, out string vidaStr)
    {
        corCristal = "white";
        vidaStr = "";
        
        if (aviao == null) { return ""; }

        ControleAviaoCaca cacaScript = aviao.GetComponent<ControleAviaoCaca>();
        if (cacaScript != null) corCristal = "#" + ColorUtility.ToHtmlStringRGB(cacaScript.corIdentificacao);
        
        string nomeLimpo = aviao.gameObject.name.Replace("(Clone)", "").Trim();

        SistemaDeDanos danos = aviao.GetComponent<SistemaDeDanos>();
        if (danos != null && danos.vidaMaxima > 0)
        {
            int pct = Mathf.RoundToInt((danos.vidaAtual / danos.vidaMaxima) * 100f);
            string corVid = (pct > 50) ? "white" : (pct > 25 ? "yellow" : "red");
            vidaStr = $" (<color={corVid}>{pct}%</color>)";
        }

        CombustivelUnidade combustivel = aviao.GetComponent<CombustivelUnidade>();
        if (combustivel != null && combustivel.usaCombustivel)
        {
            int pctComb = Mathf.RoundToInt(combustivel.Percentual * 100f);
            string corComb = pctComb > 50 ? "cyan" : (pctComb > 25 ? "yellow" : "red");
            vidaStr += $" <color={corComb}>Comb {pctComb}%</color>";
        }

        return nomeLimpo;
    }

    private string ObterChaveModeloAviao(ControleAviao aviao)
    {
        if (aviao == null)
        {
            return string.Empty;
        }

        return aviao.gameObject.name.Replace("(Clone)", "").Trim();
    }

    private bool EhMesmoModelo(ControleAviao aviao, string modelo)
    {
        return aviao != null && !string.IsNullOrEmpty(modelo) && ObterChaveModeloAviao(aviao) == modelo;
    }

    private int ContarAvioesDisponiveisMesmoModelo(ControleAviao aviaoBase)
    {
        return ContarAvioesDisponiveisMesmoModelo(ObterChaveModeloAviao(aviaoBase));
    }

    private int ContarAvioesDisponiveisMesmoModelo(string modelo)
    {
        if (string.IsNullOrEmpty(modelo))
        {
            return 0;
        }

        int total = 0;

        for (int i = 0; i < avioesNoPatio.Count; i++)
        {
            ControleAviao aviao = avioesNoPatio[i];
            if (aviao == null || aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                continue;
            }

            if (EhMesmoModelo(aviao, modelo))
            {
                total++;
            }
        }

        for (int i = 0; i < avioesNoHangar.Count; i++)
        {
            ControleAviao aviao = avioesNoHangar[i];
            if (EhMesmoModelo(aviao, modelo))
            {
                total++;
            }
        }

        return total;
    }

    private void PrepararAviaoReservaParaLancamento(ControleAviao aviao)
    {
        if (aviao == null)
        {
            return;
        }

        avioesNoHangar.Remove(aviao);
        if (!avioesNoPatio.Contains(aviao))
        {
            avioesNoPatio.Add(aviao);
        }

        aviao.gameObject.SetActive(true);

        if (wpPronto != null)
        {
            aviao.transform.position = wpPronto.position;
            aviao.transform.rotation = wpPronto.rotation;
        }
        else
        {
            aviao.transform.position = transform.position;
            aviao.transform.rotation = transform.rotation;
        }

        aviao.vagaRetorno = null;
        aviao.aguardandoCliqueRadar = false;
        aviao.ordemParaRetorno = false;
        aviao.estaEmModoVooFisico = false;
        aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
    }

    private void ConfigurarAviaoParaPatrulhaEmGrupo(ControleAviao aviao)
    {
        if (aviao == null)
        {
            return;
        }

        LancadorMisselCaca missilScript = aviao.GetComponent<LancadorMisselCaca>();
        if (missilScript != null)
        {
            missilScript.modoPassivo = false;
        }

        AviaoBombardeiro bombardeiro = aviao.GetComponent<AviaoBombardeiro>();
        if (bombardeiro != null)
        {
            bombardeiro.modoDeAtaque = AviaoBombardeiro.ModoAtaque.Patrulha;
        }
    }

    private void PrepararPatrulhaEmGrupo()
    {
        if (aviaoSelecionadoParaMissao == null)
        {
            return;
        }

        LimparModoMassaAereo();
        _modeloPatrulhaGrupo = ObterChaveModeloAviao(aviaoSelecionadoParaMissao);
        esperandoCliquePatrulhaGrupo = !string.IsNullOrEmpty(_modeloPatrulhaGrupo);
        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = esperandoCliquePatrulhaGrupo;
        _usarMarcadorPatrulhaAviaoNoProximoClique = esperandoCliquePatrulhaGrupo;
    }

    private void LimparModoMassaAereo()
    {
        esperandoCliqueMassa = false;
        esperandoCliquePatrulhaGrupo = false;
        _modeloPatrulhaGrupo = string.Empty;
        _usarMarcadorPatrulhaAviaoNoProximoClique = false;
    }

#if UNITY_EDITOR
    private void GarantirPrefabsMarcadoresNoEditor()
    {
        if (prefabMarcadorPatrulhaAviao == null)
        {
            prefabMarcadorPatrulhaAviao = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/animacao click/Map track markers VFX/Prefabs/Marker 5 Circle Loop.prefab");
        }

        if (prefabMarcadorBombardeiro == null)
        {
            prefabMarcadorBombardeiro = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/animacao click/Map track markers VFX/Prefabs/Marker 7 Danger zone Loop.prefab");
        }
    }
#endif

    void OnMouseEnter()
    {
        mouseHover = true;
    }

    void OnMouseExit()
    {
        mouseHover = false;
    }

    protected virtual void OnGUI()
    {
        if (Construtor.EmModoConstrucaoAtivo) return;

        if (mouseHover && !(this is GerenciadorPortaAvioes))
        {
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            if (_texturaTooltip == null)
            {
                _texturaTooltip = new Texture2D(1, 1);
                _texturaTooltip.SetPixel(0, 0, new Color(0.08f, 0.1f, 0.13f, 0.95f));
                _texturaTooltip.Apply();
            }
            boxStyle.normal.background = _texturaTooltip;
            boxStyle.padding = new RectOffset(10, 10, 10, 10);
            boxStyle.alignment = TextAnchor.MiddleLeft;

            GUIStyle textStyle = new GUIStyle(GUI.skin.label);
            textStyle.richText = true;
            textStyle.fontSize = 13;
            textStyle.normal.textColor = Color.white;

            float baseConsumo = 15.0f;
            int totalAvioes = avioesNoPatio.Count + avioesNoHangar.Count;
            int totalHelis = helicopterosDoAeroporto.Count;
            int totalHeavy = transportesC700NoPatio.Count;
            float consumo = baseConsumo + (totalAvioes * 2.0f) + (totalHelis * 1.5f) + (totalHeavy * 5.0f);
            
            string statusEnergia = semEnergia ? "<color=#ff5555>⚡ APAGÃO (SEM ENERGIA)</color>" : "<color=#55ff55>⚡ OPERACIONAL</color>";
            string avisoBlackout = semEnergia ? "\n<color=orange>⚠️ Lançamentos e compras bloqueados!</color>" : "";

            string content = $"<b>✈️ AEROPORTO MILITAR ({name.Replace("(Clone)", "")})</b>\n\n" +
                             $"🛸 Frota no Pátio: <b>{avioesNoPatio.Count + transportesC700NoPatio.Count}</b>\n" +
                             $"🔒 Frota no Hangar: <b>{avioesNoHangar.Count}</b>\n" +
                             $"🚁 Helicópteros: <b>{helicopterosDoAeroporto.Count}</b>\n" +
                             $"⚡ Consumo: <b>{consumo:F2} MW</b>\n" +
                             $"🔌 Conectividade: {statusEnergia}{avisoBlackout}";

            Vector2 size = textStyle.CalcSize(new GUIContent(content));
            float width = size.x + 20f;
            float height = size.y + 20f;

            Vector2 mousePos = Input.mousePosition;
            Rect rect = new Rect(mousePos.x + 15f, Screen.height - mousePos.y + 15f, width, height);

            if (rect.xMax > Screen.width) rect.x = mousePos.x - width - 15f;
            if (rect.yMax > Screen.height) rect.y = Screen.height - mousePos.y - height - 15f;

            GUI.Box(rect, "", boxStyle);
            GUI.Label(new Rect(rect.x + 10, rect.y + 10, size.x, size.y), content, textStyle);
        }

        if (!menuAtivo) return;
        if (!GestorMenusExclusivos.EstaAtivo(this))
        {
            menuAtivo = false;
            return;
        }
        if (menuAeroportoUI != null && menuAeroportoUI.activeInHierarchy) return;

        // --- SISTEMA DE FAXINA (Fix para os fantasmas no pátio) ---
        RemoveNulls(avioesNoPatio);
        RemoveNulls(avioesNoHangar);
        RemoveNulls(transportesC700NoPatio);
        RemoveNulls(helicopterosDoAeroporto);

        float widthMenu = abaAtual == 0 ? Mathf.Max(800f, Screen.width * 0.52f) : Mathf.Max(800f, Screen.width * 0.42f);
        float xMenu = 30f;
        float yMenu = 65f;
        float heightMenu = Screen.height - 85f;
        
        Rect telaDeMenu = new Rect(xMenu, yMenu, widthMenu, heightMenu);
        GestorMenusExclusivos.RegistrarAreaBloqueio(this, telaDeMenu);

        Color oldColor = GUI.backgroundColor;
        if (abaAtual == 0) GUI.backgroundColor = new Color(0.1f, 0.35f, 0.7f, 1f);
        GUI.Box(telaDeMenu, "CENTRO DE CONTROLE TÁTICO & AEROPORTO");
        GUI.backgroundColor = oldColor;

        // --- BOTÃO DE FECHAR (X) ---
        if (GUI.Button(new Rect(telaDeMenu.xMax - 35, telaDeMenu.y + 5, 30, 25), "<b>X</b>"))
        {
            menuAtivo = false;
            GestorMenusExclusivos.Fechar(this);
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
        }

        GUILayout.BeginArea(new Rect(telaDeMenu.x + 15, telaDeMenu.y + 35, telaDeMenu.width - 30, telaDeMenu.height - 45));
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🎖️ Frota Militar", GUILayout.Height(35))) { abaAtual = 1; aviaoSelecionadoParaMissao = null; c700SelecionadoParaMissao = null; helicopteroSelecionadoParaMissao = null; }
        GUILayout.EndHorizontal();

        GUILayout.Space(25);

        scrollPosAbaMilitar = GUILayout.BeginScrollView(scrollPosAbaMilitar);
        DesenharAbaMilitar();
        GUILayout.EndScrollView();
        
        GUILayout.EndArea();
    }

    // Aba Comercial removida

    private void DesenharAbaMilitar()
    {
        GUILayout.Label("<size=18><b>FROTA AÉREA E TÁTICA</b></size>");
        
        // Botão de compra para o Drone Kamikaze
        if (prefabDroneKamikaze != null)
        {
            if (semEnergia) GUI.enabled = false;
            if (GUILayout.Button($"🧨 COMPRAR DRONE KAMIKAZE (${precoDroneKamikaze})", GUILayout.Height(40)))
            {
                if (GerenciadorRecursos.Instancia != null && GerenciadorRecursos.Instancia.dinheiro >= precoDroneKamikaze)
                {
                    GerenciadorRecursos.Instancia.dinheiro -= precoDroneKamikaze;
                    ComprarAviao(prefabDroneKamikaze);
                }
                else
                {
                    Debug.LogWarning("[Aeroporto] Dinheiro insuficiente para Drone Kamikaze!");
                }
            }
            if (semEnergia) GUI.enabled = true;
        }
        else
        {
            GUI.enabled = false;
            GUILayout.Button("🧨 DRONE KAMIKAZE (Prefab não vinculado)", GUILayout.Height(40));
            GUI.enabled = true;
        }

        // Botão de compra para o Su-11
        if (prefabSu11 != null)
        {
            if (semEnergia) GUI.enabled = false;
            if (GUILayout.Button($"✈️ COMPRAR SU-11 (${precoSu11})", GUILayout.Height(40)))
            {
                if (GerenciadorRecursos.Instancia != null && GerenciadorRecursos.Instancia.dinheiro >= precoSu11)
                {
                    GerenciadorRecursos.Instancia.dinheiro -= precoSu11;
                    ComprarAviao(prefabSu11);
                }
                else
                {
                    Debug.LogWarning("[Aeroporto] Dinheiro insuficiente para Su-11!");
                }
            }
            if (semEnergia) GUI.enabled = true;
        }
        else
        {
            GUI.enabled = false;
            GUILayout.Button("✈️ SU-11 (Prefab não vinculado)", GUILayout.Height(40));
            GUI.enabled = true;
        }

        GUILayout.BeginHorizontal();

        // === COLUNA ESQUERDA: FROTA ATIVA ===
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        GUILayout.Label($"<b>FROTA ATIVA ({avioesNoPatio.Count + transportesC700NoPatio.Count + helicopterosDoAeroporto.Count})</b>");
        
        scrollPosFrota = GUILayout.BeginScrollView(scrollPosFrota, GUILayout.Height(280));
        for (int i = 0, count = avioesNoPatio.Count; i < count; i++)
        {
            ControleAviao a = avioesNoPatio[i];
            if (a == null) continue;
            
            string nomeLimpo = ObterInfoAviao(a, out string corCristal, out string vidaStr);
            string corEst = (a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio) ? "green" : "red";

            if (GUILayout.Button($"<color={corCristal}>■</color> ✈️ {nomeLimpo}{vidaStr} [<color={corEst}>{a.estadoAtual}</color>]", GUILayout.Height(30)))
            {
                aviaoSelecionadoParaMissao = a;
                c700SelecionadoParaMissao = null;
                helicopteroSelecionadoParaMissao = null;
            }
        }

        for (int i = 0; i < helicopterosDoAeroporto.Count; i++)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null) continue;

            string nomeHeli = heli.ObterRotuloExibicao();
            string estadoHeli = heli.ObterEstadoOperacionalAeroporto();
            string corEstadoHeli = heli.EstaEstacionadoNoAeroporto() ? "green" : "orange";
            string combustivelHeli = CombustivelUnidade.TextoCurto(heli);
            if (!string.IsNullOrEmpty(combustivelHeli))
            {
                combustivelHeli = $" <color=cyan>{combustivelHeli}</color>";
            }

            if (GUILayout.Button($"🚁 {nomeHeli}{combustivelHeli} [<color={corEstadoHeli}>{estadoHeli}</color>]", GUILayout.Height(30)))
            {
                helicopteroSelecionadoParaMissao = heli;
                aviaoSelecionadoParaMissao = null;
                c700SelecionadoParaMissao = null;
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // === COLUNA DIREITA: HANGAR ===
        GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>HANGAR ({avioesNoHangar.Count})</b>");
        if (GUILayout.Button("Lib. Todos", GUILayout.Width(75), GUILayout.Height(20)))
        {
            LiberarTodosDoHangar();
        }
        GUILayout.EndHorizontal();

        scrollPosHangar = GUILayout.BeginScrollView(scrollPosHangar, GUILayout.Height(280));
        for (int i = avioesNoHangar.Count - 1; i >= 0; i--)
        {
            ControleAviao h = avioesNoHangar[i];
            if (h == null) continue;
            
            string nomeLimpo = ObterInfoAviao(h, out string corCristal, out string vidaStr);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={corCristal}>■</color> 🔒 {nomeLimpo}{vidaStr}", GUILayout.Width(170));
            
            if (aviaoSelecionadoParaMissao != null && avioesNoPatio.Contains(aviaoSelecionadoParaMissao))
            {
                if (GUILayout.Button("⮂ TROCAR", GUILayout.Height(25)))
                {
                    TrocarAvioesLogicaGeral(h, aviaoSelecionadoParaMissao);
                    aviaoSelecionadoParaMissao = null; 
                    GUILayout.EndHorizontal();
                    break; 
                }
            }
            else
            {
                if (ObterPrimeiraVagaLivre() != null)
                {
                    if (GUILayout.Button("▶ LIBERAR", GUILayout.Height(25)))
                    {
                        LiberarAviaoParaPatio(h);
                        GUILayout.EndHorizontal();
                        break;
                    }
                }
                else 
                {
                    GUI.enabled = false;
                    GUILayout.Button("Pátio L.(X)", GUILayout.Height(25));
                    GUI.enabled = true;
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(20);
        
        // === PAINEL DE ORDENS DO AVIÃO SELECIONADO ===
        if (aviaoSelecionadoParaMissao != null && avioesNoPatio.Contains(aviaoSelecionadoParaMissao))
        {
            string nomeLimpo = ObterInfoAviao(aviaoSelecionadoParaMissao, out string corCristal, out string vidaStr);
            GUILayout.Label($"<b>PAINEL DE ORDENS: <color={corCristal}>■</color> {nomeLimpo}{vidaStr}</b>");
            if (semEnergia) GUI.enabled = false;
            
            if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                if (aviaoSelecionadoParaMissao.aeroportoOrigem != this && aviaoSelecionadoParaMissao.aeroportoOrigem != null)
                {
                    GUILayout.Label($"<color=orange>✈️ Estacionado em outra base/navio: {aviaoSelecionadoParaMissao.aeroportoOrigem.name.Replace("(Clone)","")}</color>");
                    if (GUILayout.Button("🔙 REQUISITAR RETORNO IMEDIATO", GUILayout.Height(50)))
                    {
                        aviaoSelecionadoParaMissao.aeroportoOrigem = this;
                        aviaoSelecionadoParaMissao.IniciarMissaoCompleta(transform.position);
                        aviaoSelecionadoParaMissao = null;
                        menuAtivo = false;
                        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                    }
                }
                else if (aviaoSelecionadoParaMissao.aguardandoCliqueRadar)
                {
                    GUILayout.Label("<color=yellow>⚠️ MODO ALVO ATIVO! Feche o Menu e Clique no mapa com o Botão Direito.</color>");
                    if (Input.GetMouseButtonDown(1)) // Botão direito do mouse
                    {
                        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
                        if (cameraPrincipal == null) return;
                        Ray r = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
                        Vector3 pontoAlvo = Vector3.zero;

                        // Tenta Raycast físico (para pegar unidades ou terra)
                        if (Physics.Raycast(r, out RaycastHit hit))
                        {
                            pontoAlvo = hit.point;
                        }
                        else
                        {
                            UnityEngine.Plane marPlano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
                            float dist;
                            if (marPlano.Raycast(r, out dist)) pontoAlvo = r.GetPoint(dist);
                        }

                        if (pontoAlvo != Vector3.zero)
                        {
                            bool usarMarcadorPatrulhaNoClique = _usarMarcadorPatrulhaAviaoNoProximoClique;
                            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
                            
                            if (esperandoCliqueMassa)
                            {
                                int quantidadeMassa = qtdMassaDrone;
                                LimparModoMassaAereo();
                                StartCoroutine(RotinaLancarMissaoEmMassa(pontoAlvo, quantidadeMassa));
                            }
                            else if (esperandoCliquePatrulhaGrupo)
                            {
                                int quantidadeGrupo = qtdPatrulhaGrupo;
                                string modeloGrupo = _modeloPatrulhaGrupo;
                                LimparModoMassaAereo();
                                StartCoroutine(RotinaLancarPatrulhaMesmoModelo(pontoAlvo, modeloGrupo, quantidadeGrupo));
                            }
                            else
                            {
                                aviaoSelecionadoParaMissao.IniciarMissaoCompleta(pontoAlvo);
                            }

                            CriarSinalizadorAereoNoAlvo(pontoAlvo, aviaoSelecionadoParaMissao, usarMarcadorPatrulhaNoClique);
                            
                            menuAtivo = false; 
                            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                        }
                    }
                    if (GUILayout.Button("❌ Cancelar Ordem", GUILayout.Height(30)))
                    {
                        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
                        LimparModoMassaAereo();
                    }
                }
                    bool isKamikaze = aviaoSelecionadoParaMissao.GetComponent<KamikazeDrone>() != null;
                    bool isBombardeiro = aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>() != null;
                    int totalMesmoModelo = 0;

                    if (!isKamikaze)
                    {
                        string modeloPainel = ObterChaveModeloAviao(aviaoSelecionadoParaMissao);
                        if (_ultimoModeloPainelPatrulha != modeloPainel)
                        {
                            _ultimoModeloPainelPatrulha = modeloPainel;
                            qtdPatrulhaGrupo = 1;
                        }

                        totalMesmoModelo = ContarAvioesDisponiveisMesmoModelo(aviaoSelecionadoParaMissao);
                        if (totalMesmoModelo < 3)
                        {
                            qtdPatrulhaGrupo = 1;
                        }
                        else
                        {
                            qtdPatrulhaGrupo = Mathf.Clamp(qtdPatrulhaGrupo, 1, totalMesmoModelo);
                            GUILayout.BeginHorizontal();
                            GUILayout.Label($"<b>Qtd. P/ Patrulha:</b> {qtdPatrulhaGrupo}/{totalMesmoModelo}");
                            if (GUILayout.Button("-", GUILayout.Width(35), GUILayout.Height(30))) qtdPatrulhaGrupo = Mathf.Max(1, qtdPatrulhaGrupo - 1);
                            if (GUILayout.Button("+", GUILayout.Width(35), GUILayout.Height(30))) qtdPatrulhaGrupo = Mathf.Min(totalMesmoModelo, qtdPatrulhaGrupo + 1);
                            if (GUILayout.Button("Todos", GUILayout.Width(60), GUILayout.Height(30))) qtdPatrulhaGrupo = totalMesmoModelo;
                            GUILayout.EndHorizontal();
                        }
                    }

                    if (isKamikaze)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"<b>Qtd. P/ Ataque:</b> {qtdMassaDrone}");
                        if (GUILayout.Button("-", GUILayout.Width(35), GUILayout.Height(30))) qtdMassaDrone = Mathf.Max(1, qtdMassaDrone - 1);
                        if (GUILayout.Button("+", GUILayout.Width(35), GUILayout.Height(30))) qtdMassaDrone++;
                        if (GUILayout.Button("Todos", GUILayout.Width(60), GUILayout.Height(30))) 
                        {
                            int totais = 0;
                            foreach(var a in avioesNoPatio) if (a != null && a.GetComponent<KamikazeDrone>() != null) totais++;
                            foreach(var a in avioesNoHangar) if (a != null && a.GetComponent<KamikazeDrone>() != null) totais++;
                            qtdMassaDrone = totais;
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("🚀 ATAQUE EM MASSA", GUILayout.Height(40))) 
                        {
                            LimparModoMassaAereo();
                            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
                            esperandoCliqueMassa = true;
                        }
                        if (GUILayout.Button("💣 Ataque Solo", GUILayout.Height(40))) 
                        {
                            ExecutarModoRadar(false);
                            esperandoCliqueMassa = false;
                        }
                        GUILayout.EndHorizontal();
                    }
                    else if (isBombardeiro)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("💣 Ataque Área (Tapete)", GUILayout.Height(40))) 
                        {
                            aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.AtaqueAoSolo;
                            ExecutarModoRadar(false);
                        }
                        string textoPatrulhaBombardeiro = (totalMesmoModelo >= 3 && qtdPatrulhaGrupo > 1)
                            ? $"🛡️ Radar (Móvel) x{qtdPatrulhaGrupo}"
                            : "🛡️ Radar (Móvel)";
                        if (GUILayout.Button(textoPatrulhaBombardeiro, GUILayout.Height(40))) 
                        {
                            aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.Patrulha;
                            if (totalMesmoModelo >= 3 && qtdPatrulhaGrupo > 1)
                            {
                                PrepararPatrulhaEmGrupo();
                            }
                            else
                            {
                                ExecutarModoRadar(false, true);
                            }
                        }
                        if (GUILayout.Button("🚀 Ataque em Massa", GUILayout.Height(40))) 
                        {
                            aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.AtaqueEmMassa;
                            ExecutarModoRadar(false);
                        }
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("👁️ Reconhecimento", GUILayout.Height(40))) ExecutarModoRadar(true);
                        string textoPatrulhaGrupo = (totalMesmoModelo >= 3 && qtdPatrulhaGrupo > 1)
                            ? $"🛡️ Patrulha Aérea x{qtdPatrulhaGrupo}"
                            : "🛡️ Patrulha Aérea";
                        if (GUILayout.Button(textoPatrulhaGrupo, GUILayout.Height(40)))
                        {
                            if (totalMesmoModelo >= 3 && qtdPatrulhaGrupo > 1)
                            {
                                PrepararPatrulhaEmGrupo();
                            }
                            else
                            {
                                ExecutarModoRadar(false, true);
                            }
                        }
                        if (GUILayout.Button("💣 Ataque Solo", GUILayout.Height(40))) ExecutarModoRadar(false);
                        GUILayout.EndHorizontal();
                    }
            }
            else if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
            {
                GUILayout.Label("<color=cyan>Aeronave civil/militar operando no espaço aéreo.</color>");
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🎯 ALTERAR ALVO/DESTINO", GUILayout.Height(50))) 
                {
                    ExecutarModoRadar(false);
                }

                if (GUILayout.Button("🔙 ABORTAR E RETORNAR À BASE", GUILayout.Height(50)))
                {
                    aviaoSelecionadoParaMissao.ComandoRetornarBase();
                    aviaoSelecionadoParaMissao = null;
                    menuAtivo = false;
                    if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                 GUILayout.Label($"<color=orange>Aeronave em trânsito: {aviaoSelecionadoParaMissao.estadoAtual}...</color>");
                 GUI.enabled = false;
                 GUILayout.Button("Aguarde a manobra de pista...", GUILayout.Height(40));
                 GUI.enabled = true;
            }
            if (semEnergia) GUI.enabled = true;
        }

        DesenharPainelHelicoptero();

        DesenharPainelC700();
    }

    private void DesenharPainelHelicoptero()
    {
        if (helicopteroSelecionadoParaMissao == null || !helicopterosDoAeroporto.Contains(helicopteroSelecionadoParaMissao))
        {
            return;
        }

        GUILayout.Space(12);
        GUILayout.BeginVertical("box");
        if (semEnergia) GUI.enabled = false;
        string nomeHeliSelecionado = helicopteroSelecionadoParaMissao.ObterRotuloExibicao();
        GUILayout.Label($"<b>PAINEL DE ORDENS: 🚁 {nomeHeliSelecionado}</b>");
        GUILayout.Label($"<color=cyan>{helicopteroSelecionadoParaMissao.ObterEstadoOperacionalAeroporto()}</color>");
        string combustivelHeliSelecionado = CombustivelUnidade.TextoCurto(helicopteroSelecionadoParaMissao);
        if (!string.IsNullOrEmpty(combustivelHeliSelecionado))
        {
            GUILayout.Label($"<color=cyan>{combustivelHeliSelecionado}</color>");
        }

        if (_modoOrdemHelicoptero != ModoOrdemHelicoptero.Nenhum)
        {
            if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Patrulha)
            {
                GUILayout.Label($"<color=yellow>PATRULHA ATIVA: clique direito adiciona e já aplica ponto ({_rotaPatrulhaHelicoptero.Count}). ENTER encerra edição, BACKSPACE desfaz, ESC cancela.</color>");
            }
            else
            {
                string modoTexto = "ATAQUE LOCAL";
                if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Reconhecimento) modoTexto = "RECONHECIMENTO";
                else if (_modoOrdemHelicoptero == ModoOrdemHelicoptero.Transporte) modoTexto = "TRANSPORTE";
                GUILayout.Label($"<color=yellow>{modoTexto} ATIVO: clique com botão direito no mapa. ESC cancela.</color>");
            }

            if (GUILayout.Button("❌ Cancelar Ordem Helicóptero", GUILayout.Height(30)))
            {
                CancelarModoHelicoptero();
            }
        }
        else
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("👁️ Reconhecimento", GUILayout.Height(38)))
            {
                IniciarModoHelicoptero(helicopteroSelecionadoParaMissao, ModoOrdemHelicoptero.Reconhecimento);
            }
            if (GUILayout.Button("🛡️ Patrulha", GUILayout.Height(38)))
            {
                IniciarModoHelicoptero(helicopteroSelecionadoParaMissao, ModoOrdemHelicoptero.Patrulha);
            }
            if (GUILayout.Button("💥 Ataque local", GUILayout.Height(38)))
            {
                IniciarModoHelicoptero(helicopteroSelecionadoParaMissao, ModoOrdemHelicoptero.AtaqueLocal);
            }
            GUILayout.EndHorizontal();

            if (helicopteroSelecionadoParaMissao.EhHelicopteroTransporte())
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🛬 Transporte", GUILayout.Height(34)))
                {
                    IniciarModoHelicoptero(helicopteroSelecionadoParaMissao, ModoOrdemHelicoptero.Transporte);
                }
                GUILayout.Label($"Tropas: {helicopteroSelecionadoParaMissao.soldadosEmbarcados.Count}/{helicopteroSelecionadoParaMissao.capacidadeMaxima}", GUILayout.Width(150));
                GUILayout.EndHorizontal();

                bool podeOperarTropas = helicopteroSelecionadoParaMissao.PodeOperarTropasNoMenu();
                bool pousadoForaDaBase = podeOperarTropas && !helicopteroSelecionadoParaMissao.EstaEstacionadoNoAeroporto();
                if (podeOperarTropas && (pousadoForaDaBase || helicopteroSelecionadoParaMissao.TemSoldados()))
                {
                    GUILayout.BeginHorizontal();
                    GUI.enabled = pousadoForaDaBase && helicopteroSelecionadoParaMissao.TemEspaco() > 0;
                    if (GUILayout.Button("📥 Recolher tropas", GUILayout.Height(32)))
                    {
                        int recolhidos = helicopteroSelecionadoParaMissao.RecolherTropasPeloMenu();
                        Debug.Log($"[Aeroporto] Helicóptero recolheu {recolhidos} tropa(s).");
                    }
                    GUI.enabled = podeOperarTropas && helicopteroSelecionadoParaMissao.TemSoldados();
                    if (GUILayout.Button("📤 Desembarcar", GUILayout.Height(32)))
                    {
                        int desembarcados = helicopteroSelecionadoParaMissao.DesembarcarTropasNoLocalAtual();
                        Debug.Log($"[Aeroporto] Helicóptero desembarcou {desembarcados} tropa(s).");
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }

            if (!helicopteroSelecionadoParaMissao.EstaEstacionadoNoAeroporto())
            {
                if (GUILayout.Button("🔙 Retornar para vaga H", GUILayout.Height(32)))
                {
                    helicopteroSelecionadoParaMissao.RetornarParaVagaAeroporto();
                }
            }
        }

        if (semEnergia) GUI.enabled = true;
        GUILayout.EndVertical();
    }

    private void DesenharPainelC700()
    {
        if (transportesC700NoPatio.Count == 0)
        {
            return;
        }

        if (c700SelecionadoParaMissao == null && transportesC700NoPatio.Count == 1)
        {
            c700SelecionadoParaMissao = transportesC700NoPatio[0];
        }

        GUILayout.Space(12);
        GUILayout.BeginVertical("box");
        GUILayout.Label("<b>TRANSPORTE C700</b>");
        scrollPosC700 = GUILayout.BeginScrollView(scrollPosC700, GUILayout.Height(380));

        for (int i = 0; i < transportesC700NoPatio.Count; i++)
        {
            C700TransporteAereo transporte = transportesC700NoPatio[i];
            if (transporte == null) continue;

            string nomeLimpo = ObterInfoAviao(transporte, out string corCristal, out string vidaStr);
            string corEstado = transporte.EstaNoSolo ? "green" : "orange";
            if (GUILayout.Button($"<color={corCristal}>■</color> {nomeLimpo}{vidaStr} [<color={corEstado}>{transporte.estadoAtual}</color>]", GUILayout.Height(28)))
            {
                c700SelecionadoParaMissao = transporte;
                aviaoSelecionadoParaMissao = null;
                helicopteroSelecionadoParaMissao = null;
            }
        }

        if (c700SelecionadoParaMissao != null && transportesC700NoPatio.Contains(c700SelecionadoParaMissao))
        {
            GUILayout.Space(8);
            string nomeSelecionado = ObterInfoAviao(c700SelecionadoParaMissao, out string corCristalSelecionado, out string vidaSelecionada);
            GUILayout.Label($"<b>SELECIONADO:</b> <color={corCristalSelecionado}>■</color> {nomeSelecionado}{vidaSelecionada}");
            if (semEnergia) GUI.enabled = false;

            GUILayout.Label($"Estado: {c700SelecionadoParaMissao.estadoAtual}");
            GUILayout.Label($"Carga real: {c700SelecionadoParaMissao.QuantidadeCargaAtual}/{c700SelecionadoParaMissao.CapacidadeCargaAtual} | Manifesto: {c700SelecionadoParaMissao.QuantidadeManifestoTotal}");

            if (c700SelecionadoParaMissao.TemDestinoVisual)
            {
                Vector3 destinoAtual = c700SelecionadoParaMissao.DestinoVisualAtual;
                GUILayout.Label($"Destino: X {destinoAtual.x:0} / Z {destinoAtual.z:0}");
            }

            GUI.enabled = c700SelecionadoParaMissao.EstaNoSolo;
            if (GUILayout.Button("Puxar tropas", GUILayout.Height(30)))
            {
                c700SelecionadoParaMissao.PuxarUnidadesProximas();
            }
            GUI.enabled = true;

            if (c700SelecionadoParaMissao.EstaNoSolo)
            {
                if (c700SelecionadoParaMissao.AguardandoDestinoAereo)
                {
                    GUILayout.Label("<color=yellow>MODO AEREO ATIVO. FECHE O MENU E CLIQUE COM O BOTAO DIREITO NO MAPA.</color>");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Cancelar ordem", GUILayout.Height(34)))
                    {
                        c700SelecionadoParaMissao.CancelarModoAereo();
                        c700SelecionadoParaMissao = null;
                    }
                    if (GUILayout.Button("Fechar menu", GUILayout.Height(34)))
                    {
                        menuAtivo = false;
                        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Preparar decolagem / destino", GUILayout.Height(40)))
                    {
                        c700SelecionadoParaMissao.PrepararMissaoAerea();
                        menuAtivo = false;
                        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                    }
                    if (GUILayout.Button("Voltar para aeroporto", GUILayout.Height(40)))
                    {
                        c700SelecionadoParaMissao.OrdenarRetornoAoAeroporto();
                        c700SelecionadoParaMissao = null;
                        menuAtivo = false;
                        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                    }
                    GUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Desembarcar carga", GUILayout.Height(34)))
                {
                    c700SelecionadoParaMissao.DesembarcarTudo();
                }
            }
            else
            {
                if (GUILayout.Button("Retornar a base", GUILayout.Height(40)))
                {
                    c700SelecionadoParaMissao.OrdenarRetornoAoAeroporto();
                    c700SelecionadoParaMissao = null;
                    menuAtivo = false;
                    if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                }
            }

        if (semEnergia) GUI.enabled = true;
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void SelecionarTransporteNoMapa(C700TransporteAereo transporte)
    {
        if (transporte == null)
        {
            return;
        }

        ControleUnidade controle = transporte.GetComponent<ControleUnidade>();
        if (controle == null)
        {
            return;
        }

        GerenteSelecao gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();
        if (gerenteSelecao != null)
        {
            gerenteSelecao.DeselecionarTudo();
            if (!gerenteSelecao.unidadesSelecionadas.Contains(controle))
            {
                gerenteSelecao.unidadesSelecionadas.Add(controle);
            }
        }

        controle.DefinirSelecao(true);
    }

    private void ExecutarModoRadar(bool deveSerPassivo, bool usarMarcadorPatrulhaAviao = false)
    {
        if (aviaoSelecionadoParaMissao == null) return;
        LimparModoMassaAereo();
        _usarMarcadorPatrulhaAviaoNoProximoClique = usarMarcadorPatrulhaAviao;
        
        // Tenta forçar o modo no script de Missil, caso exista
        LancadorMisselCaca missilScript = aviaoSelecionadoParaMissao.GetComponent<LancadorMisselCaca>();
        if (missilScript != null)
        {
            missilScript.modoPassivo = deveSerPassivo;
            Debug.Log($"[Aeroporto] Modo Passivo definido como: {deveSerPassivo}");
        }

        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
        menuAtivo = false;
        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
        Debug.Log($"[Aeroporto] Modo Missão Ativado. Fechando painel. Dê a ordem com o clique Direito!");
    }

    private void TrocarAvioesLogicaGeral(ControleAviao modeloSubsaturado, ControleAviao hangarASeAfastar)
    {
        if (hangarASeAfastar.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) return;

        Transform vagaOcupadaLivre = hangarASeAfastar.vagaRetorno;
        Vector3 xyzVaga = hangarASeAfastar.transform.position;
        Quaternion anguloVaga = hangarASeAfastar.transform.rotation;

        avioesNoPatio.Remove(hangarASeAfastar);
        avioesNoHangar.Remove(modeloSubsaturado);

        GuardarAviaoNoHangarInstantaneo(hangarASeAfastar, false);

        modeloSubsaturado.gameObject.SetActive(true);
        modeloSubsaturado.transform.position = xyzVaga;
        modeloSubsaturado.transform.rotation = anguloVaga;
        modeloSubsaturado.vagaRetorno = vagaOcupadaLivre; 
        modeloSubsaturado.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        avioesNoPatio.Add(modeloSubsaturado);
    }

    private void LiberarAviaoParaPatio(ControleAviao aviaoDoHangar)
    {
        Transform vagaDesignada = ObterPrimeiraVagaLivre();
        if (vagaDesignada == null) return;
        
        ColocarAviaoInstantaneamenteNoPatio(aviaoDoHangar, vagaDesignada, true);
    }
    
    public void LiberarTodosDoHangar()
    {
        // Copia a lista para evitar modificação simultânea no foreach
        List<ControleAviao> copiaHangar = new List<ControleAviao>(avioesNoHangar);
        for (int i = 0, count = copiaHangar.Count; i < count; i++)
        {
            if (ObterPrimeiraVagaLivre() == null) break; // Pátio lotado
            if (copiaHangar[i] != null) LiberarAviaoParaPatio(copiaHangar[i]);
        }
    }
    
    private IEnumerator TrazerAviaoParaPatio(ControleAviao aviao, Transform vaga)
    {
        if (aviao == null) yield break;
        ColocarAviaoInstantaneamenteNoPatio(aviao, vaga, true);
        yield break;
    }

    public virtual void GuardarNoHangarAutomatico(ControleAviao aviao)
    {
        GuardarAviaoNoHangarInstantaneo(aviao, true);
    }

    protected void GuardarAviaoNoHangarInstantaneo(ControleAviao aviao, bool removerDoPatio)
    {
        if (aviao == null) return;

        if (removerDoPatio)
        {
            avioesNoPatio.Remove(aviao);
        }

        if (!avioesNoHangar.Contains(aviao)) avioesNoHangar.Add(aviao);

        aviao.aguardandoCliqueRadar = false;
        aviao.ordemParaRetorno = false;
        aviao.estaEmModoVooFisico = false;
        aviao.estadoAtual = ControleAviao.EstadoAviao.ReservaHangar;
        aviao.vagaRetorno = null;
        aviao.transform.SetParent(transform, true);

        if (wpPreparacao != null)
        {
            aviao.transform.position = wpPreparacao.position;
            aviao.transform.rotation = wpPreparacao.rotation;
        }
        else if (hangarAviao != null)
        {
            aviao.transform.position = hangarAviao.position;
            aviao.transform.rotation = hangarAviao.rotation;
        }

        aviao.gameObject.SetActive(false);
    }

    protected bool ColocarAviaoInstantaneamenteNoPatio(ControleAviao aviao, Transform vaga, bool removerDoHangar)
    {
        if (aviao == null || vaga == null) return false;

        if (removerDoHangar)
        {
            avioesNoHangar.Remove(aviao);
        }

        aviao.gameObject.SetActive(true);
        aviao.transform.SetParent(transform, true);
        // Offset de altura para evitar que o avião fique dentro da mesh do convés
        float alturaOffset = aviao.ObterAlturaEstacionamento();
        aviao.transform.position = vaga.position + (vaga.up * alturaOffset);
        aviao.transform.rotation = vaga.rotation;
        aviao.vagaRetorno = vaga;
        aviao.aguardandoCliqueRadar = false;
        aviao.ordemParaRetorno = false;
        aviao.estaEmModoVooFisico = false;
        aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;

        if (!avioesNoPatio.Contains(aviao)) avioesNoPatio.Add(aviao);
        return true;
    }

    protected void ReporPatioComAvioesDoHangar()
    {
        for (int i = avioesNoHangar.Count - 1; i >= 0 && ObterPrimeiraVagaLivre() != null; i--)
        {
            ControleAviao aviaoDoHangar = avioesNoHangar[i];
            if (aviaoDoHangar == null)
            {
                avioesNoHangar.RemoveAt(i);
                continue;
            }

            LiberarAviaoParaPatio(aviaoDoHangar);
        }
    }

    private void ColetarAeronavesProntas(List<ControleAviao> destino)
    {
        destino.Clear();
        for (int i = 0; i < avioesNoPatio.Count; i++)
        {
            ControleAviao aviao = avioesNoPatio[i];
            if (aviao != null && aviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                destino.Add(aviao);
            }
        }
    }

    private bool LancarAeronaveIA(ControleAviao aviao, Vector3 alvoReconhecimento, Vector3 alvoPatrulha, Vector3 alvoAtaque)
    {
        if (aviao == null || aviao.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio)
        {
            return false;
        }

        Vector3 alvo;
        int missao = Random.Range(0, 3);
        switch (missao)
        {
            case 0:
                alvo = alvoReconhecimento != Vector3.zero ? alvoReconhecimento : transform.position + transform.forward * 700f;
                break;
            case 1:
                alvo = alvoPatrulha != Vector3.zero ? alvoPatrulha : transform.position + transform.right * 350f;
                break;
            default:
                alvo = alvoAtaque != Vector3.zero ? alvoAtaque : transform.position + transform.forward * 1100f;
                break;
        }

        Vector3 alvoEstrategico = alvo;
        Vector3 alvoVoo = alvo;
        alvoVoo.y = Mathf.Max(alvoVoo.y, 60f);
        aviao.aguardandoCliqueRadar = false;
        aviao.alvoPrioritarioIA = missao == 2;
        aviao.alvoEstrategico = alvoEstrategico;
        aviao.centroDaPatrulha = alvoVoo;
        aviao.alvoGPSVoo = alvoVoo;

        AviaoBombardeiro bombardeiro = aviao.GetComponent<AviaoBombardeiro>();
        if (bombardeiro != null)
        {
            bombardeiro.alvoAreaSolo = alvoEstrategico;
            bombardeiro.alvoMassa1 = alvoEstrategico + new Vector3(-35f, 0f, -10f);
            bombardeiro.alvoMassa2 = alvoEstrategico + new Vector3(35f, 0f, 10f);
            bombardeiro.modoDeAtaque = missao == 2
                ? (Random.value > 0.45f ? AviaoBombardeiro.ModoAtaque.AtaqueAoSolo : AviaoBombardeiro.ModoAtaque.AtaqueEmMassa)
                : AviaoBombardeiro.ModoAtaque.Patrulha;
        }

        CacaVooRealista vooRealista = aviao.GetComponent<CacaVooRealista>();
        if (vooRealista != null)
        {
            vooRealista.alvoGPS = alvoVoo;
        }

        LancadorMisselCaca lancadorCaca = aviao.GetComponent<LancadorMisselCaca>();
        if (lancadorCaca != null)
        {
            lancadorCaca.modoPassivo = missao != 2;
        }

        aviao.IniciarMissaoCompleta(alvoEstrategico);
        return true;
    }
}
