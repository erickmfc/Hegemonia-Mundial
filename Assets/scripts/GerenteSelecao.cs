using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class GerenteSelecao : MonoBehaviour
{
    private struct PegadaCache
    {
        public float largura;
        public float profundidade;
        public int frameAtualizacao;
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }

    [Header("Configurações Visuais")]
    public RectTransform caixaSelecaoVisual; // Sua imagem verde
    public RectTransform canvasRect;         // O Pai de todos (Interface/Canvas)
    public GameObject prefabMarcadorDestino;     // Prefab do marcador normal
    public GameObject prefabMarcadorBombardeiro; // Prefab Marker 7 (Para bombardeiros)
    public GameObject prefabMarcadorPatrulha;    // Prefab do marcador de patrulha (ex: Marker 2 Pointer Loop)

    [Header("Controle")]
    public float espacamento = 2.5f; // Distância entre soldados na formação
    public List<ControleUnidade> unidadesSelecionadas = new List<ControleUnidade>();
    private Camera cameraPrincipal;
    private Construtor construtorCache;
    private DesenharLinhasOrdem desenhadorOrdensCache;
    private readonly RaycastHit[] bufferHitsClique = new RaycastHit[64];
    private readonly List<RaycastResult> bufferRaycastUI = new List<RaycastResult>(16);
    private readonly Dictionary<int, PegadaCache> cachePegadas = new Dictionary<int, PegadaCache>();
    private readonly List<ControleUnidade> bufferControlesSelecionaveis = new List<ControleUnidade>(256);
    private readonly List<ControleUnidade> bufferInfantaria = new List<ControleUnidade>(64);
    private readonly List<ControleUnidade> bufferVeiculos = new List<ControleUnidade>(32);
    private readonly List<ControleUnidade> bufferOrdemFormacao = new List<ControleUnidade>(96);
    private readonly List<SlotFormacao> bufferSlotsFormacao = new List<SlotFormacao>(96);
    private static readonly RaycastHitDistanceComparer ComparadorHits = new RaycastHitDistanceComparer();
    private PointerEventData pointerEventDataUI;
    private EventSystem eventSystemUI;
    private const int FramesCachePegada = 1800;
    private const int LimiteGrupoGrandeParaAmostragemLeve = 12;

    private Vector2 inicioMouseScreen; // Posição pura do mouse na tela
    private bool arrastando = false;

    void Start()
    {
        cameraPrincipal = Camera.main;
        // Começa desligado e zerado
        if (caixaSelecaoVisual != null)
        {
            caixaSelecaoVisual.gameObject.SetActive(false);
            caixaSelecaoVisual.sizeDelta = Vector2.zero;
        }
    }

    void Update()
    {
        if (cameraPrincipal == null || !cameraPrincipal.gameObject.activeInHierarchy)
        {
            cameraPrincipal = ObterCameraForte();
        }
        Construtor construtorObj = ObterConstrutor();
        if (Construtor.EmModoConstrucaoAtivo)
        {
            arrastando = false;
            LiberarModoCaixaSelecao();
            if (caixaSelecaoVisual != null)
            {
                caixaSelecaoVisual.gameObject.SetActive(false);
                caixaSelecaoVisual.sizeDelta = Vector2.zero;
            }
            return;
        }
        // 1. CLICOU (Marca onde começou)
        if (Input.GetMouseButtonDown(0))
        {
            // Se clicar em cima de botões da UI, não começa seleção nem limpa a seleção atual.
            if (IsMouseOverInteractiveUI())
            {
                return;
            }

            // Clique esquerdo CANCELA o modo patrulha/seguir (sai do modo ao invés de ignorar o clique) PRIMEIRAMENTE
            DesenharLinhasOrdem desenhadorAtivo = ObterDesenhadorOrdens();
            if (desenhadorAtivo != null && (desenhadorAtivo.modoPatrulhaAtivo || desenhadorAtivo.modoSeguirAtivo))
            {
                desenhadorAtivo.CancelarModo();
                return;
            }

            if (CapturaCliqueOrdensManuais.EstaAtiva())
            {
                return;
            }

            if (SelecaoBloqueadaPorModoAtual("Seleção bloqueada por modo ativo"))
            {
                return;
            }

            // ===== CORREÇÃO DEFINITIVA DO BUG DO CONSTRUTOR ABRINDO O MENU SOZINHO =====
            // Se estou segurando um prédio para colocar no chão, o Gerente ignora esse clique (Não arma o arrasto)
            Construtor construtorModoClique = ObterConstrutor();
            if (construtorModoClique != null && construtorModoClique.modoConstrucao)
            {
                return;
            }
            // ==============================================================================

            arrastando = true;
            inicioMouseScreen = Input.mousePosition; 
            AtivarModoCaixaSelecao();
        }

        // 2. ARRASTANDO (Desenha a caixa)
        if (Input.GetMouseButton(0) && arrastando)
        {
            if (CapturaCliqueOrdensManuais.EstaAtiva())
            {
                arrastando = false;
                LiberarModoCaixaSelecao();
                if (caixaSelecaoVisual != null)
                {
                    caixaSelecaoVisual.gameObject.SetActive(false);
                    caixaSelecaoVisual.sizeDelta = Vector2.zero;
                }
                return;
            }

            if (SelecaoBloqueadaPorModoAtual("Arrasto interrompido por modo ativo"))
            {
                arrastando = false;
                LiberarModoCaixaSelecao();
                if (caixaSelecaoVisual != null)
                {
                    caixaSelecaoVisual.gameObject.SetActive(false);
                    caixaSelecaoVisual.sizeDelta = Vector2.zero;
                }
                return;
            }

            if (caixaSelecaoVisual == null) GarantirCaixaVisual();

            if(caixaSelecaoVisual != null && Vector2.Distance(inicioMouseScreen, Input.mousePosition) > 10f)
            {
                caixaSelecaoVisual.gameObject.SetActive(true);
            }
            
            if (caixaSelecaoVisual != null && caixaSelecaoVisual.gameObject.activeSelf)
                AtualizarDesenhoCaixa();
        }

        // 3. SOLTOU (Calcula quem pegou)
        if (Input.GetMouseButtonUp(0))
        {
            if (CapturaCliqueOrdensManuais.EstaAtiva())
            {
                arrastando = false;
                LiberarModoCaixaSelecao();
                if(caixaSelecaoVisual != null)
                    caixaSelecaoVisual.gameObject.SetActive(false);
                return;
            }

            if (!arrastando)
            {
                LiberarModoCaixaSelecao();
                return;
            }

            bool arrastouBastante = Vector2.Distance(inicioMouseScreen, Input.mousePosition) > 10f;

            if (arrastouBastante)
            {
                DeselecionarTudo();
                SelecionarUnidadesMatematica();
            }
            else
            {
                // Clique Simples (Sem arrastar)
                CliqueSimples();
            }

            // Limpeza
            arrastando = false;
            LiberarModoCaixaSelecao();
            if(caixaSelecaoVisual != null)
                caixaSelecaoVisual.gameObject.SetActive(false);
        }

        // 4. MOVIMENTO EM GRUPO (Botão Direito)
        if (Input.GetMouseButtonDown(1))
        {
            if (CapturaCliqueOrdensManuais.EstaAtiva())
            {
                return;
            }

            if (OrdemMundoBloqueadaPorModoAtual("Ordem de movimento bloqueada por modo ativo"))
            {
                return;
            }

            // --- CONEXÃO COM SISTEMA DE ORDENS (PATRULHA/SEGUIR) ---
            DesenharLinhasOrdem desenhador = ObterDesenhadorOrdens();
            if (desenhador != null && (desenhador.modoPatrulhaAtivo || desenhador.modoSeguirAtivo))
            {
                return; // Ignora o movimento padrão se estiver gravando patrulha ou seguir
            }
            // -----------------------------------------------------

            if (AlgumaUnidadeSelecionadaEmModoManualDeDisparo())
            {
                return;
            }

            if(unidadesSelecionadas.Count > 0)
            {
                // Usa LayerMask para ignorar Triggers, UI, IgnoreRaycast (2) etc.
                // Default (0), Water (4), Terrain (8) etc.
                // Mas queremos ignorar IgnoreRaycast (2).
                int layerMaskMove = ~(1 << 2); 

                Camera cam = cameraPrincipal != null ? cameraPrincipal : ObterCameraForte();
                if (cam == null) return;
                Ray raio = cam.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                Vector3 destino = Vector3.zero;
                bool encontrouDestino = TryResolverDestinoClique(raio, layerMaskMove, out hit, out destino);

                if (encontrouDestino)
                {
                    MostrarMarcadorDestino(destino);

                    // VERIFICA SE CLICOU EM UM AEROPORTO PARA OS AVIÕES POUSAREM (Abastecimento Manual)
                    TorreDeControle torre = null;
                    GerenciadorPortaAvioes portaAvioes = null;
                    if (hit.collider != null)
                    {
                         torre = hit.collider.GetComponentInParent<TorreDeControle>();
                         portaAvioes = hit.collider.GetComponentInParent<GerenciadorPortaAvioes>();
                    }

                    MoverUnidadesEmGrupo(destino, torre, portaAvioes);
                }
            }
        }
    }

    Construtor ObterConstrutor()
    {
        if (construtorCache == null)
        {
            construtorCache = Object.FindFirstObjectByType<Construtor>();
        }

        return construtorCache;
    }

    Camera ObterCameraForte()
    {
        if (cameraPrincipal != null && cameraPrincipal.gameObject.activeInHierarchy) return cameraPrincipal;
        cameraPrincipal = Camera.main;
        if (cameraPrincipal != null) return cameraPrincipal;
        cameraPrincipal = Object.FindFirstObjectByType<Camera>();
        return cameraPrincipal;
    }

    bool TryResolverDestinoClique(Ray raio, int layerMaskMove, out RaycastHit hitFinal, out Vector3 destino)
    {
        hitFinal = new RaycastHit();
        destino = Vector3.zero;

        int quantidadeHits = Physics.RaycastNonAlloc(
            raio,
            bufferHitsClique,
            Mathf.Infinity,
            layerMaskMove,
            QueryTriggerInteraction.Ignore);

        RaycastHit[] hitsExtras = null;
        if (quantidadeHits >= bufferHitsClique.Length)
        {
            hitsExtras = Physics.RaycastAll(raio, Mathf.Infinity, layerMaskMove, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hitsExtras, ComparadorHits);
            quantidadeHits = hitsExtras.Length;
        }
        else
        {
            System.Array.Sort(bufferHitsClique, 0, quantidadeHits, ComparadorHits);
        }

        for (int i = 0; i < quantidadeHits; i++)
        {
            RaycastHit hit = hitsExtras != null ? hitsExtras[i] : bufferHitsClique[i];
            if (hit.collider == null)
            {
                continue;
            }

            string nomeCollider = hit.collider.name.ToLowerInvariant();
            if (nomeCollider.Contains("bip001") || nomeCollider.Contains("bone") || nomeCollider.Contains("finger") || nomeCollider.Contains("cube"))
            {
                continue;
            }

            bool ehPortaAvioes = hit.collider.GetComponentInParent<GerenciadorPortaAvioes>() != null;
            bool temUnidadeOuNavAgent = hit.collider.GetComponentInParent<ControleUnidade>() != null ||
                                        hit.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null;
            if (temUnidadeOuNavAgent && !ehPortaAvioes)
            {
                continue;
            }

            bool ehEstrutura = hit.collider.GetComponentInParent<Estaleiro>() != null
                               || hit.collider.GetComponentInParent<PierMarinha>() != null
                               || hit.collider.GetComponentInParent<Fabrica>() != null
                               || hit.collider.GetComponentInParent<AtributosPredio>() != null
                               || hit.collider.GetComponentInParent<Edificio>() != null;
            if (ehEstrutura)
            {
                continue;
            }

            hitFinal = hit;
            destino = hit.point;
            return true;
        }

        UnityEngine.Plane planoAgua = new UnityEngine.Plane(Vector3.up, Vector3.zero);
        float distancia;
        if (planoAgua.Raycast(raio, out distancia))
        {
            destino = raio.GetPoint(distancia);
            return true;
        }

        return false;
    }

    void AtivarModoCaixaSelecao()
    {
        InteractionModeService.Request(
            InteractionOwner.SelectionBox,
            new InteractionPolicy
            {
                bloqueiaSelecao = false,
                bloqueiaOrdemMundo = true,
                bloqueiaRotacaoCamera = true,
                consomeLMB = true,
                consomeRMB = false
            },
            "Caixa de seleção ativa");
    }

    void LiberarModoCaixaSelecao()
    {
        InteractionModeService.Release(InteractionOwner.SelectionBox);
    }

    bool SelecaoBloqueadaPorModoAtual(string descricao)
    {
        InteractionModeSnapshot snapshot = InteractionModeService.CurrentSnapshot();
        if (snapshot.Owner == InteractionOwner.None || snapshot.Owner == InteractionOwner.SelectionBox)
        {
            return false;
        }

        if (!snapshot.Policy.bloqueiaSelecao)
        {
            return false;
        }

        string detalhe = string.IsNullOrWhiteSpace(snapshot.Reason) ? snapshot.Owner.ToString() : snapshot.Reason;
        InteractionModeService.ReportBlockedInput(descricao + ": " + detalhe);
        return true;
    }

    bool OrdemMundoBloqueadaPorModoAtual(string descricao)
    {
        InteractionModeSnapshot snapshot = InteractionModeService.CurrentSnapshot();
        if (snapshot.Owner == InteractionOwner.None || snapshot.Owner == InteractionOwner.SelectionBox)
        {
            return false;
        }

        if (!snapshot.Policy.bloqueiaOrdemMundo)
        {
            return false;
        }

        string detalhe = string.IsNullOrWhiteSpace(snapshot.Reason) ? snapshot.Owner.ToString() : snapshot.Reason;
        InteractionModeService.ReportBlockedInput(descricao + ": " + detalhe);
        return true;
    }

    DesenharLinhasOrdem ObterDesenhadorOrdens()
    {
        if (desenhadorOrdensCache == null)
        {
            desenhadorOrdensCache = FindFirstObjectByType<DesenharLinhasOrdem>();
        }

        return desenhadorOrdensCache;
    }

    bool IsMouseOverInteractiveUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (pointerEventDataUI == null || eventSystemUI != eventSystem)
        {
            pointerEventDataUI = new PointerEventData(eventSystem);
            eventSystemUI = eventSystem;
        }

        pointerEventDataUI.Reset();
        pointerEventDataUI.position = Input.mousePosition;
        bufferRaycastUI.Clear();
        eventSystem.RaycastAll(pointerEventDataUI, bufferRaycastUI);

        for (int i = 0; i < bufferRaycastUI.Count; i++)
        {
            GameObject uiObject = bufferRaycastUI[i].gameObject;
            if (uiObject == null || !uiObject.activeInHierarchy)
            {
                continue;
            }

            Canvas canvas = uiObject.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (!UIEstaVisivelEInterativa(uiObject))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    bool UIEstaVisivelEInterativa(GameObject uiObject)
    {
        if (uiObject == null || !uiObject.activeInHierarchy)
        {
            return false;
        }

        Graphic graphic = uiObject.GetComponent<Graphic>();
        if (graphic != null && !graphic.raycastTarget)
        {
            return false;
        }

        CanvasGroup[] groups = uiObject.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            CanvasGroup group = groups[i];
            if (group == null)
            {
                continue;
            }

            if (!group.blocksRaycasts || group.alpha <= 0.05f)
            {
                return false;
            }
        }

        return UIObjetoBloqueiaCliqueMundo(uiObject);
    }

    bool UIObjetoBloqueiaCliqueMundo(GameObject uiObject)
    {
        if (uiObject == null)
        {
            return false;
        }

        if (uiObject.GetComponentInParent<Selectable>() != null
            || uiObject.GetComponentInParent<ScrollRect>() != null
            || uiObject.GetComponentInParent<EventTrigger>() != null)
        {
            return true;
        }

        Transform atual = uiObject.transform;
        while (atual != null)
        {
            string nome = atual.name;
            if (nome.Contains("Painel_Construcao")
                || nome.Contains("Painel_Governo")
                || nome.Contains("Menu")
                || nome.Contains("Popup")
                || nome.Contains("Modal")
                || nome.Contains("[HUD_MenuComando]")
                || nome.Contains("PainelComandoStatus")) // ADDED
            {
                return true;
            }

            atual = atual.parent;
        }

        // ADICIONAR: Verifica colisão usando EventSystem (RaycastAll) para UI Toolkit / Canvas genéricos
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        return false;
    }

    // --- MARCADOR VISUAL DO CLIQUE ---
    void MostrarMarcadorDestino(Vector3 pos)
    {
        bool temBombardeiro = false;
        foreach(var u in unidadesSelecionadas) 
        {
            if (u != null && u.GetComponent<AviaoBombardeiro>() != null) 
            { 
                temBombardeiro = true; 
                break; 
            }
        }

        if (temBombardeiro && prefabMarcadorBombardeiro != null)
        {
            GameObject marcador = Instantiate(prefabMarcadorBombardeiro, pos + Vector3.up * 0.1f, Quaternion.identity);
            marcador.transform.localScale = new Vector3(40f, 40f, 40f);
            Destroy(marcador, 4f);
        }
        else if (prefabMarcadorDestino != null)
        {
            // Instancia o novo efeito de partícula (Marker 1 arrows Loop)
            GameObject marcador = Instantiate(prefabMarcadorDestino, pos + Vector3.up * 0.1f, Quaternion.identity);
            
            // Aumenta o tamanho da animação para 10 vezes maior
            marcador.transform.localScale = new Vector3(10f, 10f, 10f);
            
            // Como é um loop, vamos destruí-lo após 2 segundos para não ficar no mapa para sempre
            Destroy(marcador, 2f); 
        }
        else
        {
            // Fallback caso não tenha prefab associado
            GameObject marcador = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(marcador.GetComponent<Collider>());
            marcador.transform.position = pos + Vector3.up * 0.1f;
            marcador.transform.localScale = new Vector3(2f, 0.05f, 2f);
            
            Renderer r = marcador.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.material.color = new Color(0f, 1f, 0.5f, 0.6f); // Verde neon
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            
            marcador.AddComponent<AnimadorMarcador>();
        }
    }

    public class AnimadorMarcador : MonoBehaviour
    {
        float tempo = 0;
        void Update()
        {
            tempo += Time.deltaTime * 3f;
            transform.localScale = Vector3.Lerp(new Vector3(2f, 0.05f, 2f), Vector3.zero, tempo);
            if (tempo >= 1f) Destroy(gameObject);
        }
    }

    // --- NOVA LÓGICA DE FORMAÇÃO TÁTICA (MISTA) ---
    void MoverUnidadesEmGrupo_OLD(Vector3 destinoCentral, TorreDeControle torreDestino = null)
    {
        unidadesSelecionadas.RemoveAll(u => u == null);
        int totalOriginal = unidadesSelecionadas.Count;
        if (totalOriginal == 0) return;

        // 1. Classifica o esquadrão taticamente
        bool ehGrupoNaval = false;
        bool temVeiculo = false;

        bufferInfantaria.Clear();
        bufferVeiculos.Clear();
        bufferOrdemFormacao.Clear();
        bufferSlotsFormacao.Clear();
        List<ControleUnidade> infantaria = bufferInfantaria;
        List<ControleUnidade> veiculos = bufferVeiculos;

        foreach (var u in unidadesSelecionadas)
        {
            // Checagem Aérea (Não entra na grade limitante de contato físico)
            ControleAviao aviao = u.GetComponent<ControleAviao>();
            Helicoptero heli = u.GetComponent<Helicoptero>();
            
            if (aviao != null)
            {
                if (torreDestino != null)
                {
                    aviao.ComandoRetornarBase();
                    Debug.Log($"[GerenteSelecao] Selecionou Retornar pra Base via RMB! ({u.name})");
                }
                else
                {
                    u.EmitirOrdemMover(destinoCentral);
                }
                continue; // Avião resolvido
            }
            if (heli != null)
            {
                // Helicópteros ganham pequenos offsets individuais no ar para não se amalgamarem
                Vector3 deslocHeli = new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
                heli.Decolar(destinoCentral + deslocHeli);
                continue; // Helicóptero resolvido
            }

            // Checagem Naval
            if (u.EhUnidadeNaval())
            {
                ehGrupoNaval = true;
            }

            // Checagem Terrestre (Tanque x Soldado)
            string n = u.name.ToLower();
            if (n.Contains("tank") || n.Contains("tanque") || n.Contains("blindado") || n.Contains("hammer") || n.Contains("humvee") || n.Contains("lancador"))
            {
                temVeiculo = true;
                veiculos.Add(u);
            }
            else
            {
                infantaria.Add(u);
            }
        }

        // Define espaçamento dinâmico: Se tiver mista (1 tanque e 10 soldados), exige grade larga pra evitar atrito
        float espacamentoReal = ehGrupoNaval ? 30.0f : (temVeiculo ? 7.0f : espacamento); 

        // 2. Ordena o pelotão: Tanques e suporte atrás, infantaria na linha de frente
        List<ControleUnidade> gridUnidades = new List<ControleUnidade>();
        gridUnidades.AddRange(veiculos);     // Índice Baixo da grade (Trás)
        gridUnidades.AddRange(infantaria);   // Índice Alto da grade (Frente)

        int totalGrade = gridUnidades.Count;
        if (totalGrade == 0) return;

        // 3. Calcula centro do grupo para direção tática de virada
        Vector3 centroGrupo = Vector3.zero;
        foreach (var u in gridUnidades) centroGrupo += u.transform.position;
        centroGrupo /= totalGrade;

        Vector3 direcaoMovimento = (destinoCentral - centroGrupo).normalized;
        if (direcaoMovimento == Vector3.zero) direcaoMovimento = Vector3.forward;
        Quaternion rotacaoFormacao = Quaternion.LookRotation(direcaoMovimento);

        // 4. Desenha Formação Geométrica
        int colunas = Mathf.CeilToInt(Mathf.Sqrt(totalGrade));
        float larguraTotal = (colunas - 1) * espacamentoReal;
        float profundidadeTotal = (Mathf.CeilToInt((float)totalGrade / colunas) - 1) * espacamentoReal;
        Vector3 offsetCentral = new Vector3(-larguraTotal / 2f, 0, -profundidadeTotal / 2f);

        for (int i = 0; i < totalGrade; i++)
        {
            ControleUnidade alvoCtrl = gridUnidades[i];

            int x = i % colunas;
            int z = i / colunas; // z é a linha (z alto = frente da base)

            Vector3 posLocalGrade = offsetCentral + new Vector3(x * espacamentoReal, 0, z * espacamentoReal);
            Vector3 offsetRodado = rotacaoFormacao * posLocalGrade;

            // O ponto alvo final individual
            Vector3 posAlvo = destinoCentral + offsetRodado;

            // BLOQUEIO MANUAL
            if (UnidadeBloqueiaMovimentoManual(alvoCtrl))
                continue; 

            // NAVMESH PREDICTION: Ajuda navios em terreno acidentado / margens
            if (ehGrupoNaval)
            {
                 UnityEngine.AI.NavMeshHit hit;
                 if (UnityEngine.AI.NavMesh.SamplePosition(posAlvo, out hit, 15f, UnityEngine.AI.NavMesh.AllAreas))
                 {
                     posAlvo = hit.position;
                 }
            }

            // Envia Comando
            alvoCtrl.EmitirOrdemMover(posAlvo);
        }
    }

    private struct SlotFormacao
    {
        public ControleUnidade unidade;
        public float largura;
        public float profundidade;
    }

    // Formacao considerando tamanho real (BoxCollider/NavMeshAgent)
    void MoverUnidadesEmGrupo(Vector3 destinoCentral, TorreDeControle torreDestino = null, GerenciadorPortaAvioes portaAvioesDestino = null)
    {
        unidadesSelecionadas.RemoveAll(u => u == null);
        if (unidadesSelecionadas.Count == 0) return;

        bool ehGrupoNaval = false;
        bool temVeiculo = false;

        bufferInfantaria.Clear();
        bufferVeiculos.Clear();
        bufferOrdemFormacao.Clear();
        bufferSlotsFormacao.Clear();

        foreach (var unidade in unidadesSelecionadas)
        {
            if (unidade == null) continue;

            if (unidade.TemC700TransporteAereo)
            {
                unidade.EmitirOrdemMover(destinoCentral);
                continue;
            }

            if (unidade.TemControleAviao)
            {
                ControleAviao aviao = unidade.GetComponent<ControleAviao>();
                if (aviao == null)
                {
                    unidade.EmitirOrdemMover(destinoCentral);
                    continue;
                }

                if (portaAvioesDestino != null)
                {
                    aviao.DefinirBaseAlternativaEIniciarRetorno(portaAvioesDestino);
                    Debug.Log($"[GerenteSelecao] {unidade.name} redirecionado para pousar no porta-avioes {portaAvioesDestino.name}.");
                }
                else if (torreDestino != null)
                {
                    aviao.ComandoRetornarBase();
                    Debug.Log($"[GerenteSelecao] Selecionou Retornar pra Base via RMB! ({unidade.name})");
                }
                else
                {
                    unidade.EmitirOrdemMover(destinoCentral);
                }
                continue;
            }

            if (unidade.TemHelicopteroExterno)
            {
                Vector3 deslocHeli = new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                Helicoptero heli = unidade.GetComponent<Helicoptero>();
                if (heli != null)
                {
                    if (heli.EstaSobControleDoAeroporto())
                    {
                        continue;
                    }
                    heli.Decolar(destinoCentral + deslocHeli);
                }
                continue;
            }

            if (unidade.EhUnidadeNaval())
            {
                ehGrupoNaval = true;
            }

            string nome = unidade.name.ToLower();
            bool eVeiculo = nome.Contains("tank") || nome.Contains("tanque") || nome.Contains("blindado") ||
                            nome.Contains("hammer") || nome.Contains("humvee") || nome.Contains("lancador");

            if (eVeiculo)
            {
                temVeiculo = true;
                bufferVeiculos.Add(unidade);
            }
            else
            {
                bufferInfantaria.Add(unidade);
            }
        }

        bufferOrdemFormacao.AddRange(bufferVeiculos);   // Traseira
        bufferOrdemFormacao.AddRange(bufferInfantaria); // Frente

        int total = bufferOrdemFormacao.Count;
        if (total == 0) return;

        Vector3 centroGrupo = Vector3.zero;
        foreach (var u in bufferOrdemFormacao) centroGrupo += u.transform.position;
        centroGrupo /= total;

        Vector3 direcaoMovimento = (destinoCentral - centroGrupo).normalized;
        if (direcaoMovimento == Vector3.zero) direcaoMovimento = Vector3.forward;
        Quaternion rotacaoFormacao = Quaternion.LookRotation(direcaoMovimento);

        int colunas = Mathf.CeilToInt(Mathf.Sqrt(total));
        if (temVeiculo) colunas = Mathf.Clamp(colunas, 2, 6);
        int linhas = Mathf.CeilToInt((float)total / colunas);

        float somaLargura = 0f;
        float somaProfundidade = 0f;

        for (int i = 0; i < total; i++)
        {
            float largura;
            float profundidade;
            ObterPegadaUnidade(bufferOrdemFormacao[i], out largura, out profundidade);

            if (ehGrupoNaval)
            {
                largura *= 1.25f;
                profundidade *= 1.25f;
            }

            bufferSlotsFormacao.Add(new SlotFormacao
            {
                unidade = bufferOrdemFormacao[i],
                largura = largura,
                profundidade = profundidade
            });

            somaLargura += largura;
            somaProfundidade += profundidade;
        }

        float mediaLargura = Mathf.Max(1f, somaLargura / total);
        float mediaProfundidade = Mathf.Max(1f, somaProfundidade / total);

        float gapX = ehGrupoNaval ? Mathf.Max(6f, mediaLargura * 0.30f) : Mathf.Max(temVeiculo ? 1.7f : 1.0f, mediaLargura * 0.18f);
        float gapZ = ehGrupoNaval ? Mathf.Max(8f, mediaProfundidade * 0.35f) : Mathf.Max(temVeiculo ? 2.2f : 1.2f, mediaProfundidade * 0.22f);

        float[] larguraColuna = new float[colunas];
        float[] profundidadeLinha = new float[linhas];

        for (int i = 0; i < bufferSlotsFormacao.Count; i++)
        {
            int coluna = i % colunas;
            int linha = i / colunas;
            larguraColuna[coluna] = Mathf.Max(larguraColuna[coluna], bufferSlotsFormacao[i].largura);
            profundidadeLinha[linha] = Mathf.Max(profundidadeLinha[linha], bufferSlotsFormacao[i].profundidade);
        }

        float larguraTotal = 0f;
        for (int c = 0; c < colunas; c++) larguraTotal += larguraColuna[c];
        larguraTotal += Mathf.Max(0, colunas - 1) * gapX;

        float profundidadeTotal = 0f;
        for (int l = 0; l < linhas; l++) profundidadeTotal += profundidadeLinha[l];
        profundidadeTotal += Mathf.Max(0, linhas - 1) * gapZ;

        float[] centroColuna = new float[colunas];
        float[] centroLinha = new float[linhas];

        float cursorX = -larguraTotal * 0.5f;
        for (int c = 0; c < colunas; c++)
        {
            centroColuna[c] = cursorX + (larguraColuna[c] * 0.5f);
            cursorX += larguraColuna[c] + gapX;
        }

        float cursorZ = -profundidadeTotal * 0.5f;
        for (int l = 0; l < linhas; l++)
        {
            centroLinha[l] = cursorZ + (profundidadeLinha[l] * 0.5f);
            cursorZ += profundidadeLinha[l] + gapZ;
        }

        float raioAmostraNavMesh = ehGrupoNaval ? 20f : (temVeiculo ? 8f : 4f);
        bool usarAmostragemLeve = !ehGrupoNaval && total >= LimiteGrupoGrandeParaAmostragemLeve;

        for (int i = 0; i < bufferSlotsFormacao.Count; i++)
        {
            ControleUnidade alvoCtrl = bufferSlotsFormacao[i].unidade;
            if (alvoCtrl == null) continue;

            if (UnidadeBloqueiaMovimentoManual(alvoCtrl))
                continue;

            int coluna = i % colunas;
            int linha = i / colunas;

            Vector3 posLocal = new Vector3(centroColuna[coluna], 0f, centroLinha[linha]);
            Vector3 posAlvo = destinoCentral + (rotacaoFormacao * posLocal);

            bool unidadeAnfibia = alvoCtrl.TemHovercraftTransporte;

            UnityEngine.AI.NavMeshHit hit;
            if (!unidadeAnfibia && !usarAmostragemLeve && UnityEngine.AI.NavMesh.SamplePosition(posAlvo, out hit, raioAmostraNavMesh, UnityEngine.AI.NavMesh.AllAreas))
            {
                posAlvo = hit.position;
            }

            alvoCtrl.EmitirOrdemMover(posAlvo);
        }
    }

    void ObterPegadaUnidade(ControleUnidade unidade, out float largura, out float profundidade)
    {
        float minimo = Mathf.Max(1.0f, espacamento * 0.55f);
        largura = minimo;
        profundidade = minimo;

        if (unidade == null) return;

        int idUnidade = unidade.GetInstanceID();
        PegadaCache cache;
        if (cachePegadas.TryGetValue(idUnidade, out cache) && Time.frameCount - cache.frameAtualizacao <= FramesCachePegada)
        {
            largura = cache.largura;
            profundidade = cache.profundidade;
            return;
        }

        bool temBounds = false;
        Bounds bounds = new Bounds(unidade.transform.position, Vector3.zero);

        Collider[] colliders = unidade.GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c == null || !c.enabled || c.isTrigger) continue;

            if (!temBounds)
            {
                bounds = c.bounds;
                temBounds = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        if (temBounds)
        {
            largura = Mathf.Max(largura, bounds.size.x);
            profundidade = Mathf.Max(profundidade, bounds.size.z);
        }
        else
        {
            Renderer[] renderers = unidade.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;

                if (!temBounds)
                {
                    bounds = r.bounds;
                    temBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (temBounds)
            {
                largura = Mathf.Max(largura, bounds.size.x * 0.8f);
                profundidade = Mathf.Max(profundidade, bounds.size.z * 0.8f);
            }
        }

        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            float diametro = Mathf.Max(minimo, agent.radius * 2f);
            largura = Mathf.Max(largura, diametro);
            profundidade = Mathf.Max(profundidade, diametro);
        }

        largura = Mathf.Clamp(largura, minimo, 45f);
        profundidade = Mathf.Clamp(profundidade, minimo, 45f);

        cachePegadas[idUnidade] = new PegadaCache
        {
            largura = largura,
            profundidade = profundidade,
            frameAtualizacao = Time.frameCount
        };
    }

    bool AlgumaUnidadeSelecionadaEmModoManualDeDisparo()
    {
        for (int i = 0; i < unidadesSelecionadas.Count; i++)
        {
            ControleUnidade unidade = unidadesSelecionadas[i];
            if (unidade == null)
            {
                continue;
            }

            if (UnidadeBloqueiaMovimentoManual(unidade))
            {
                return true;
            }
        }

        return false;
    }

    bool UnidadeBloqueiaMovimentoManual(ControleUnidade unidade)
    {
        if (unidade == null)
        {
            return false;
        }

        LancadorNaval lancador = unidade.GetComponentInChildren<LancadorNaval>(true);
        if (lancador != null && lancador.modoAtual == LancadorNaval.ModoOperacao.Manual)
        {
            return true;
        }

        ControleSubmarino submarino = unidade.GetComponent<ControleSubmarino>();
        return submarino != null && submarino.EmModoManualDisparo();
    }

    void GarantirCaixaVisual()
    {
        if (caixaSelecaoVisual != null && canvasRect != null) return;

        GameObject existente = GameObject.Find("CaixaSelecao");
        if (existente != null)
        {
            caixaSelecaoVisual = existente.GetComponent<RectTransform>();
            Canvas c = existente.GetComponentInParent<Canvas>();
            if (c != null) canvasRect = c.GetComponent<RectTransform>();
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
            GameObject novaCaixa = new GameObject("CaixaSelecao");
            novaCaixa.transform.SetParent(canvas.transform, false);
            caixaSelecaoVisual = novaCaixa.AddComponent<RectTransform>();
            novaCaixa.transform.SetAsFirstSibling(); 
            
            Image img = novaCaixa.AddComponent<Image>();
            img.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            img.raycastTarget = false;
            
            Outline outline = novaCaixa.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 1f, 0.1f, 0.8f);
            outline.effectDistance = new Vector2(2, 2);

            caixaSelecaoVisual.pivot = new Vector2(0, 0); 
            caixaSelecaoVisual.anchorMin = new Vector2(0.5f, 0.5f);
            caixaSelecaoVisual.anchorMax = new Vector2(0.5f, 0.5f);
            
            caixaSelecaoVisual.gameObject.SetActive(false);
        }
    }

    void AtualizarDesenhoCaixa()
    {
        if (canvasRect == null || caixaSelecaoVisual == null) return;

        Vector2 mouseAtualScreen = Input.mousePosition;

        // --- TRADUÇÃO MOUSE -> CANVAS ---
        Vector2 localInicio;
        Vector2 localAtual;

        // Converte o ponto inicial e o atual para dentro do Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, inicioMouseScreen, null, out localInicio);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouseAtualScreen, null, out localAtual);

        // Calcula o tamanho e posição no Canvas
        Vector2 tamanho = localAtual - localInicio;
        
        caixaSelecaoVisual.sizeDelta = new Vector2(Mathf.Abs(tamanho.x), Mathf.Abs(tamanho.y));
        
        // Ajusta a posição para que a caixa cresça para qualquer lado (cima/baixo/esq/dir)
        float posX = (tamanho.x < 0) ? localAtual.x : localInicio.x;
        float posY = (tamanho.y < 0) ? localAtual.y : localInicio.y;

        caixaSelecaoVisual.anchoredPosition = new Vector2(posX, posY);
    }

    void SelecionarUnidadesMatematica()
    {
        // Aqui usamos a posição REAL da tela, ignorando o desenho da caixa
        Vector2 mouseFinal = Input.mousePosition;

        float minX = Mathf.Min(inicioMouseScreen.x, mouseFinal.x);
        float maxX = Mathf.Max(inicioMouseScreen.x, mouseFinal.x);
        float minY = Mathf.Min(inicioMouseScreen.y, mouseFinal.y);
        float maxY = Mathf.Max(inicioMouseScreen.y, mouseFinal.y);

        Camera cam = cameraPrincipal != null ? cameraPrincipal : ObterCameraForte();
        if (cam == null) return;

        RegistroEntidadesJogo.FillControlesUnidade(bufferControlesSelecionaveis);

        foreach (var unidade in bufferControlesSelecionaveis)
        {
            if (unidade == null || !unidade.enabled) continue; // Ignora unidades desativadas (como soldados dentro de caminhões)

            // Onde o tanque está na tela?
            Vector3 posTela = cam.WorldToScreenPoint(unidade.transform.position);

            if (posTela.x > minX && posTela.x < maxX && 
                posTela.y > minY && posTela.y < maxY)
            {
                AdicionarSelecao(unidade);
            }
        }
    }

    ControleUnidade ResolverControleSelecionavel(Transform origem)
    {
        if (origem == null)
        {
            return null;
        }

        ControleUnidade unidade = origem.GetComponentInParent<ControleUnidade>();
        if (unidade != null)
        {
            return unidade;
        }

        IdentidadeNaval identidadeNaval = origem.GetComponentInParent<IdentidadeNaval>();
        if (identidadeNaval == null)
        {
            return null;
        }

        unidade = identidadeNaval.GetComponent<ControleUnidade>();
        if (unidade == null)
        {
            unidade = identidadeNaval.gameObject.AddComponent<ControleUnidade>();
        }

        if (!unidade.enabled)
        {
            unidade.enabled = true;
        }

        return unidade;
    }

    void CliqueSimples()
    {
        // Se usar ~0 (Tudo), pega até triggers que não deveria.
        // Vamos tentar pegar tudo exceto a Ignore Raycast (2).
        int layerMask = ~(1 << 2); 

        Camera cam = cameraPrincipal != null ? cameraPrincipal : ObterCameraForte();
        if (cam == null) return;
        Ray raio = cam.ScreenPointToRay(Input.mousePosition);
        int quantidadeHits = Physics.RaycastNonAlloc(raio, bufferHitsClique, Mathf.Infinity, layerMask, QueryTriggerInteraction.Ignore);
        RaycastHit[] hitsExtras = null;
        if (quantidadeHits >= bufferHitsClique.Length)
        {
            hitsExtras = Physics.RaycastAll(raio, Mathf.Infinity, layerMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hitsExtras, ComparadorHits);
            quantidadeHits = hitsExtras.Length;
        }
        else
        {
            System.Array.Sort(bufferHitsClique, 0, quantidadeHits, ComparadorHits);
        }

        for (int i = 0; i < quantidadeHits; i++)
        {
            RaycastHit toque = hitsExtras != null ? hitsExtras[i] : bufferHitsClique[i];
            if (toque.collider == null) continue;

            ControleUnidade unidade = ResolverControleSelecionavel(toque.transform);
            if (unidade == null) continue;

            if (!unidade.enabled)
            {
                var transportePai = unidade.transform.parent?.GetComponentInParent<ControleUnidade>();
                if (transportePai != null && transportePai.enabled) unidade = transportePai;
                else continue;
            }

            DeselecionarTudo();
            AdicionarSelecao(unidade);
            return;
        }

        for (int i = 0; i < quantidadeHits; i++)
        {
            RaycastHit toque = hitsExtras != null ? hitsExtras[i] : bufferHitsClique[i];
            if (toque.collider == null) continue;

            // === NOVO: VERIFICA SE O JOGADOR CLICOU NA FÁBRICA / CONSTRUTOR DE VEÍCULOS ===
            var fabrica = toque.transform.GetComponentInParent<Fabrica>();
            if (fabrica != null)
            {
                var id = fabrica.GetComponentInParent<IdentidadeUnidade>();
                // Certifica se a fábrica pertence ao jogador (TeamID 1)
                if (id == null || id.teamID == 1) 
                {
                    MenuConstrucao menu = Object.FindFirstObjectByType<MenuConstrucao>();
                    if (menu != null)
                    {
                        // Abre o menu na aba do Exército
                        if (!MenuConstrucao.EstaAberto) menu.AlternarMenu(true);
                        menu.FiltrarPorCategoria(DadosConstrucao.CategoriaItem.Exercito);
                        
                        return; // Paralisa o código para não selecionar a fábrica como "tropa"
                    }
                }
            }
            // ==============================================================================

            // === VERIFICA SE O JOGADOR CLICOU NO ESTALEIRO / PIER ===
            var estaleiro = toque.transform.GetComponentInParent<Estaleiro>();
            if (estaleiro != null)
            {
                var idEstaleiro = estaleiro.GetComponentInParent<IdentidadeUnidade>();
                if (idEstaleiro == null || idEstaleiro.teamID == 1)
                {
                    MenuConstrucao menu = Object.FindFirstObjectByType<MenuConstrucao>();
                    if (menu != null)
                    {
                        if (!MenuConstrucao.EstaAberto) menu.AlternarMenu(true);
                        menu.FiltrarPorCategoria(DadosConstrucao.CategoriaItem.Marinha);

                        return;
                    }
                }
            }
            // ==============================================================================
        }
    }

    public void AdicionarSelecao(ControleUnidade unidade)
    {
        if (unidade == null || unidadesSelecionadas.Contains(unidade))
        {
            return;
        }

        // VERIFICA SE É DO MEU TIME
        int teamIdRecuperado = -1;
        
        IdentidadeUnidade idU = unidade.GetComponent<IdentidadeUnidade>();
        if (idU != null) teamIdRecuperado = idU.teamID;
        else 
        {
            IdentidadeIA idIA = unidade.GetComponent<IdentidadeIA>();
            if (idIA != null) teamIdRecuperado = idIA.teamID;
        }
        
        if (teamIdRecuperado != -1)
        {
            // Tem uma identidade definida. Se não for 1, ignora.
            if (teamIdRecuperado != 1) return;
        }
        else
        {
            if (unidade.GetComponent<GerenciadorPortaAvioes>() != null)
            {
                return;
            }

            // --- CORREÇÃO AUTOMÁTICA (APENAS SE NÃO TIVER NENHUM SCRIPT DE IDENTIDADE) ---
            idU = unidade.gameObject.AddComponent<IdentidadeUnidade>();
            idU.teamID = 1; // Registra como Aliado
            idU.nomeDoPais = "Minha Nação";
            idU.tipoUnidade = unidade.EhUnidadeNaval()
                ? TipoUnidade.Naval
                : (((!unidade.TemC700TransporteAereo) && (
                    unidade.GetComponent<ControleAviao>() != null
                    || unidade.GetComponent<Helicoptero>() != null
                    || unidade.GetComponent<VooHelicoptero>() != null))
                    ? TipoUnidade.Aereo
                    : TipoUnidade.Veiculo);
        }

        unidadesSelecionadas.Add(unidade);
        unidade.DefinirSelecao(true);
    }

    public void DeselecionarTudo()
    {
        foreach (var u in unidadesSelecionadas)
        {
            if (u)
            {
                u.DefinirSelecao(false);
            }
        }
        unidadesSelecionadas.Clear();
    }
}
