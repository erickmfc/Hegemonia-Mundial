using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI; // Necessário para a UI
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;

public class Estaleiro : MonoBehaviour
{
    private Vector3 _ultimoForwardAgua = Vector3.forward;

    [Header("Proprietario da estrutura")]
    [Tooltip("Use 0 para ler a IdentidadeUnidade da propria estrutura. Nunca e inferido pela distancia a outra IA.")]
    [SerializeField] private int ownerTeamId;

    public int OwnerTeamId
    {
        get { return ResolverOwnerTeamId(); }
        set
        {
            ownerTeamId = Mathf.Max(0, value);

            // A estrutura pode vir de um prefab que carregava a identidade de
            // outro país. Ao definir explicitamente o dono, mantenha também a
            // identidade usada pelos navios produzidos e pelo menu do jogador.
            if (ownerTeamId > 0)
            {
                IdentidadeUnidade id = GetComponent<IdentidadeUnidade>();
                if (id == null) id = GetComponentInParent<IdentidadeUnidade>();
                if (id != null) id.teamID = ownerTeamId;
            }
        }
    }

    [System.Serializable]
    public class SlotConstrucao
    {
        public string nomeSlot = "Slot";
        public Transform pontoDeConstrucao; // Onde o navio fica sendo "montado"
        public bool estaOcupado = false;
        
        [HideInInspector] public GameObject visualAtual;
        [HideInInspector] public GameObject prefabAtual;
        [HideInInspector] public float progresso; // 0 a 100
        [HideInInspector] public Vector3 escalaOriginal; // Para lembrar o tamanho correto
        
        // UI de Progresso Interna
        [HideInInspector] public GameObject barCanvasObj;
        [HideInInspector] public Image barFillImage;
        [HideInInspector] public Text textProgresso;
        [HideInInspector] public string productionOrderId = string.Empty;
    }

    private sealed class PedidoNavalAutomatico
    {
        public GameObject Prefab;
        public string OrderId;
    }

    [Header("Estrutura e Vagas")]
    public Transform pontoDeSaida; // Para onde o navio vai depois de pronto
    public SlotConstrucao[] slots; // Configure 2 slots no Inspector

    [Header("Configuração de Construção")]
    public float tempoDeConstrucao = 5.0f; // Tempo em segundos para construir
    public bool usarAnimacaoEscala = false; // DESATIVADO TEMPORARIAMENTE A PEDIDO

    [Header("Fila Naval")]
    [Tooltip("Quantidade maxima de navios aguardando alem dos dois slots ativos.")]
    [SerializeField] private int limiteFilaNaval = 10;
    [Tooltip("Tempo de carregamento entre lotes de ate dois navios.")]
    [SerializeField] private float intervaloEntreLotes = 10f;

    private readonly Queue<PedidoNavalAutomatico> filaNaval = new Queue<PedidoNavalAutomatico>(10);
    private int naviosIniciadosNoLote;
    private int naviosConcluidosNoLote;
    private float liberarProximoLoteEm = -1f;

    public int NaviosNaFila { get { return filaNaval.Count; } }
    public int LimiteFilaNaval { get { return Mathf.Max(0, limiteFilaNaval); } }
    public bool TemCapacidadeDeProducao
    {
        get
        {
            if (liberarProximoLoteEm >= 0f)
            {
                return filaNaval.Count < LimiteFilaNaval;
            }

            return ObterSlotLivre() != null || filaNaval.Count < LimiteFilaNaval;
        }
    }

    [Header("Visual da Barra de Progresso")]
    public GameObject prefabBarraProgresso; // Opcional: Prefab customizado
    public Vector3 offsetBarra = new Vector3(0, 10f, 0); // Altura da barra sobre o navio
    public Vector2 tamanhoBarra = new Vector2(4, 0.5f); // Tamanho se gerada via código

    [Header("Ajustes de Altura")]
    public bool forcarNivelDaAgua = true; 
    public float nivelDaAgua = 0f; 
    public float offsetAltura = 0f; 

    [Header("Efeitos Visuais")]
    public ParticleSystem efeitoConclusao; 

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    void OnDestroy()
    {
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !string.IsNullOrEmpty(slots[i].productionOrderId))
                    IAAutoProductionRegistry.Release(slots[i].productionOrderId, Time.time);
            }
        }
        while (filaNaval.Count > 0)
        {
            PedidoNavalAutomatico pedido = filaNaval.Dequeue();
            if (pedido != null && !string.IsNullOrEmpty(pedido.OrderId))
                IAAutoProductionRegistry.Release(pedido.OrderId, Time.time);
        }
    }
    
    void Start()
    {
        NormalizarProprietarioDoJogador();
        SincronizarPerfilCosteiro();
        AutoDetectarFilhosDaCena();
        NormalizarSlots();
        CorrigirPoseCosteiraSeNecessario();
        GarantirSlotsExistentes();
        AtualizarReferenciasLitoraneas();

        if (GetComponent<SistemaDeDanos>() == null)
        {
            var dano = gameObject.AddComponent<SistemaDeDanos>();
            dano.vidaMaxima = 3000f;
            dano.vidaAtual = 3000f;
        }

        RegistrarNoGerente();
    }

    /// <summary>
    /// Corrige estaleiros de saves/prefabs antigos que chegaram com a
    /// IdentidadeUnidade de uma IA. O marcador e escrito pelo Construtor do
    /// jogador; estaleiros criados pelos executores da IA usam outro rotulo e
    /// continuam com o proprio time.
    /// </summary>
    void NormalizarProprietarioDoJogador()
    {
        IA_ManualPlacementTag manualTag = GetComponent<IA_ManualPlacementTag>();
        if (manualTag == null) manualTag = GetComponentInParent<IA_ManualPlacementTag>();
        if (manualTag == null
            || string.IsNullOrEmpty(manualTag.SourceLabel)
            || !manualTag.SourceLabel.StartsWith("Construtor jogador", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null)
        {
            identidade = gameObject.AddComponent<IdentidadeUnidade>();
        }

        identidade.teamID = 1;
        identidade.nomeDoPais = "Hegemonia";
        OwnerTeamId = 1;
    }

    /// <summary>
    /// Busca filhos existentes na hierarquia ("saida", "Atracagem", "Atracagem_Grande")
    /// e vincula-os ao pontoDeSaida e aos slots, respeitando posições manuais da cena.
    /// </summary>
    void AutoDetectarFilhosDaCena()
    {
        // --- Detectar ponto de saída ---
        if (pontoDeSaida == null)
        {
            string[] nomesSaida = { "saida", "Saida", "PontoDeSaida", "ponto_saida", "exit" };
            foreach (string nome in nomesSaida)
            {
                Transform encontrado = transform.Find(nome);
                if (encontrado != null)
                {
                    pontoDeSaida = encontrado;
                    Debug.Log($"[Estaleiro] Auto-detectado ponto de saída: {encontrado.name}");
                    break;
                }
            }
        }

        // --- Detectar pontos de atracagem ---
        Transform atracagem = transform.Find("Atracagem");
        Transform atracagemGrande = transform.Find("Atracagem_Grande");

        // Se slots estão vazios ou nulos, criar a partir dos filhos encontrados
        if (slots == null || slots.Length == 0)
        {
            var listaSlots = new List<SlotConstrucao>();

            if (atracagemGrande != null)
            {
                listaSlots.Add(new SlotConstrucao
                {
                    nomeSlot = "Atracagem_Grande",
                    pontoDeConstrucao = atracagemGrande,
                    estaOcupado = false
                });
            }

            if (atracagem != null)
            {
                listaSlots.Add(new SlotConstrucao
                {
                    nomeSlot = "Atracagem",
                    pontoDeConstrucao = atracagem,
                    estaOcupado = false
                });
            }

            if (listaSlots.Count > 0)
            {
                slots = listaSlots.ToArray();
                Debug.Log($"[Estaleiro] Auto-detectados {listaSlots.Count} slots de atracagem da cena.");
            }
        }
        else
        {
            // Vincular filhos existentes a slots que não têm pontoDeConstrucao
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                if (slots[i].pontoDeConstrucao != null) continue;

                string nomeSlot = slots[i].nomeSlot;
                if (string.IsNullOrEmpty(nomeSlot)) continue;

                Transform encontrado = transform.Find(nomeSlot);
                if (encontrado != null)
                {
                    slots[i].pontoDeConstrucao = encontrado;
                    Debug.Log($"[Estaleiro] Slot '{nomeSlot}' vinculado ao filho existente.");
                }
            }
        }
    }

    void OnValidate()
    {
        if (!gameObject.scene.IsValid())
        {
            return;
        }

        SincronizarPerfilCosteiro();
        NormalizarSlots();
        CorrigirPoseCosteiraSeNecessario();
        AtualizarReferenciasLitoraneas();
    }

    void RegistrarNoGerente()
    {
        GerenteDeJogo gerente = GerenteDeJogo.Instancia;
        if (gerente == null) gerente = Object.FindFirstObjectByType<GerenteDeJogo>();

        if (gerente != null)
        {
            if (ResolverOwnerTeamId() == 1)
            {
                // Registra o primeiro slot como spawn se possível
                Transform spawn = (slots != null && slots.Length > 0) ? slots[0].pontoDeConstrucao : transform;
                gerente.AtualizarPontoEstaleiro(spawn, pontoDeSaida);
            }
        }
    }

    bool EstruturaDoJogadorHumano()
    {
        return ResolverOwnerTeamId() == 1;
    }

    private int ResolverOwnerTeamId()
    {
        // A marca aplicada pelo Construtor identifica uma estrutura colocada
        // manualmente pelo jogador. Ela tem precedência sobre uma identidade
        // antiga gravada no prefab, evitando que o estaleiro seja atribuído à
        // IA mais próxima ou a um país de teste.
        IA_ManualPlacementTag manualTag = GetComponent<IA_ManualPlacementTag>();
        if (manualTag == null) manualTag = GetComponentInParent<IA_ManualPlacementTag>();
        if (manualTag != null
            && !string.IsNullOrEmpty(manualTag.SourceLabel)
            && manualTag.SourceLabel.StartsWith("Construtor jogador", System.StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        IdentidadeUnidade id = GetComponent<IdentidadeUnidade>();
        if (id == null) id = GetComponentInParent<IdentidadeUnidade>();
        if (id != null && id.teamID > 0) return id.teamID;
        if (ownerTeamId > 0) return ownerTeamId;
        // Estruturas colocadas pelo jogador recebem esta marca antes de
        // qualquer unidade ser criada. Ela e um fallback seguro para o time 1.
        if (manualTag != null)
        {
            return 1;
        }
        // Prefabs legados do jogador ja usavam team 1 por padrao. O ponto
        // importante e nunca escolher uma IA pela proximidade.
        return 1;
    }

    bool IgnorarRegrasCosteirasManuais()
    {
        // IA já tem pontos manuais predefinidos, não precisa de validação costeira
        if (!EstruturaDoJogadorHumano())
        {
            return true;
        }

        return GetComponent<IA_ManualPlacementTag>() != null;
    }

    void CorrigirPoseCosteiraSeNecessario()
    {
        if (EstruturaDoJogadorHumano() || IgnorarRegrasCosteirasManuais())
        {
            return;
        }

        string validacao;
        if (NavalPlacementResolver.IsCurrentStructurePoseValid(gameObject, out validacao))
        {
            return;
        }

        NavalPlacementResolver.StructurePose pose;
        if (!NavalPlacementResolver.TryResolveStructurePose(gameObject, transform.position, transform.rotation, out pose))
        {
            return;
        }

        transform.SetPositionAndRotation(pose.Position, pose.Rotation);
    }

    public void GarantirSlotsExistentes()
    {
        // Validação básica: auto-criação de slots se estiver nulo
        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("[Estaleiro] Nenhum slot de construção configurado no Inspector! Criando 3 slots básicos automaticamente.");
            slots = new SlotConstrucao[3];
            Vector3 autoWaterForward = ObterForwardAgua();
            Vector3 autoLateralAxis = Quaternion.LookRotation(autoWaterForward, Vector3.up) * Vector3.right;
            for (int i = 0; i < 3; i++)
            {
                GameObject novoPonto = new GameObject($"Ponto_Auto_Estaleiro_{i}");
                novoPonto.transform.SetParent(this.transform);
                novoPonto.transform.position = transform.position + (autoWaterForward * offsetAguaFrente) + (autoLateralAxis * (i * 20f - 20f));
                novoPonto.transform.rotation = Quaternion.LookRotation(autoWaterForward, Vector3.up);
                
                slots[i] = new SlotConstrucao
                {
                    nomeSlot = (i == 0) ? "Atracagem_Grande" : $"Slot_{i}",
                    pontoDeConstrucao = novoPonto.transform,
                    estaOcupado = false
                };
            }
        }

        NormalizarSlots();

        Vector3 waterForward = ObterForwardAgua();
        Vector3 lateralAxis = Quaternion.LookRotation(waterForward, Vector3.up) * Vector3.right;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].pontoDeConstrucao != null)
            {
                continue;
            }

            GameObject novoPonto = new GameObject(slots[i].nomeSlot);
            novoPonto.transform.SetParent(this.transform);
            novoPonto.transform.position = transform.position + (waterForward * offsetAguaFrente) + (lateralAxis * (i * 20f - 20f));
            novoPonto.transform.rotation = Quaternion.LookRotation(waterForward, Vector3.up);
            slots[i].pontoDeConstrucao = novoPonto.transform;
        }
    }

    void Update()
    {
        if (slots == null) return;
        ProcessarProximoLoteSePronto();
        // Processa a construção em cada slot ocupado
        foreach (var slot in slots)
        {
            if (slot.estaOcupado)
            {
                ProcessarConstrucao(slot);
            }
        }
    }

    public bool TemVaga
    {
        // Nome legado usado pelo menu: agora inclui a fila pendente.
        get { return TemCapacidadeDeProducao; }
    }

    public bool ConstruirUnidade(GameObject prefabDoNavio, string productionOrderId = "")
    {
        if (prefabDoNavio != null)
        {
            DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("spawn_prefab_name", prefabDoNavio.name);
        }

        GarantirSlotsExistentes();
        AtualizarReferenciasLitoraneas();

        string validacaoNaval;
        float nivelAguaAtual = NavalPlacementResolver.ResolveSeaLevel();
        bool poseValida = NavalPlacementResolver.IsCurrentStructurePoseValid(gameObject, out validacaoNaval);
        if (!IgnorarRegrasCosteirasManuais() && !poseValida && !EstaNaConstrucaoValida(nivelAguaAtual))
        {
            Debug.LogWarning("[Estaleiro] Construção naval bloqueada: " + validacaoNaval);
            return false;
        }

        if (liberarProximoLoteEm >= 0f)
        {
            int limiteEspera = LimiteFilaNaval;
            if (filaNaval.Count >= limiteEspera)
            {
                Debug.LogWarning($"[Estaleiro] Fila naval cheia ({limiteEspera}). Pedido recusado.");
                return false;
            }

            filaNaval.Enqueue(new PedidoNavalAutomatico { Prefab = prefabDoNavio, OrderId = productionOrderId });
            Debug.Log($"[Estaleiro] Navio enfileirado para o proximo lote: {prefabDoNavio.name} | fila={filaNaval.Count}/{limiteEspera}");
            return true;
        }

        if (naviosIniciadosNoLote >= 2)
        {
            int limiteLote = LimiteFilaNaval;
            if (filaNaval.Count >= limiteLote)
            {
                Debug.LogWarning($"[Estaleiro] Fila naval cheia ({limiteLote}). Pedido recusado.");
                return false;
            }

            filaNaval.Enqueue(new PedidoNavalAutomatico { Prefab = prefabDoNavio, OrderId = productionOrderId });
            Debug.Log($"[Estaleiro] Navio aguardando o fim do lote: {prefabDoNavio.name} | fila={filaNaval.Count}/{limiteLote}");
            return true;
        }

        SlotConstrucao slotLivre = ObterSlotLivre();

        if (slotLivre == null)
        {
            int limite = LimiteFilaNaval;
            if (filaNaval.Count >= limite)
            {
                Debug.LogWarning($"[Estaleiro] Fila naval cheia ({limite}). Pedido recusado.");
                return false;
            }

            filaNaval.Enqueue(new PedidoNavalAutomatico { Prefab = prefabDoNavio, OrderId = productionOrderId });
            Debug.Log($"[Estaleiro] Navio enfileirado: {prefabDoNavio.name} | fila={filaNaval.Count}/{limite}");
            return true;
        }

        if (slotLivre != null)
        {
            long initStart = System.Diagnostics.Stopwatch.GetTimestamp();
            IniciarConstrucao(slotLivre, prefabDoNavio, productionOrderId);
            RegistrarTempoDiagnostico("prefab_init_ms", initStart);
            return true;
        }
        else
        {
            Debug.LogWarning("[Estaleiro] Todos os slots estão ocupados!");
            return false;
        }
    }

    private static void RegistrarTempoDiagnostico(string chave, long inicio)
    {
        float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - inicio) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (elapsedMs > 0f)
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(chave, elapsedMs);
        }
    }

    bool EhNavioGrande(GameObject prefabDoNavio)
    {
        if (prefabDoNavio == null)
        {
            return false;
        }

        IdentidadeNaval identidadeNaval = prefabDoNavio.GetComponent<IdentidadeNaval>();
        if (identidadeNaval == null)
        {
            identidadeNaval = prefabDoNavio.GetComponentInChildren<IdentidadeNaval>();
        }

        if (identidadeNaval != null && identidadeNaval.categoriaNavio == IdentidadeNaval.CategoriaNavio.TransporteGrande)
        {
            return true;
        }

        return prefabDoNavio.GetComponent<NavioPetroleiro>() != null
            || prefabDoNavio.GetComponent<NavioCargaMercado>() != null
            || prefabDoNavio.name.IndexOf("navio de carga", System.StringComparison.OrdinalIgnoreCase) >= 0
            || prefabDoNavio.GetComponent<TransporteAnfibio>() != null
            || prefabDoNavio.GetComponent<NavioLiberty>() != null;
    }

    SlotConstrucao ObterSlotEspecificoLivre(string nomeAlvo)
    {
        if (slots == null) return null;
        foreach (var slot in slots)
        {
            // Verifica o nome configurado no Inspector ou o nome do objeto Transform
            bool nomeBate = (slot.nomeSlot == nomeAlvo) || 
                            (slot.pontoDeConstrucao != null && slot.pontoDeConstrucao.name == nomeAlvo);

            if (nomeBate && !slot.estaOcupado)
            {
                return slot;
            }
        }
        return null;
    }

    SlotConstrucao ObterSlotLivre()
    {
        if (slots == null) return null;
        foreach (var slot in slots)
        {
            if (!slot.estaOcupado) return slot;
        }
        return null;
    }

    void IniciarConstrucao(SlotConstrucao slot, GameObject prefab, string productionOrderId = "")
    {
        slot.estaOcupado = true;
        slot.prefabAtual = prefab;
        slot.productionOrderId = productionOrderId ?? string.Empty;
        slot.progresso = 0f;
        naviosIniciadosNoLote++;
        if (!string.IsNullOrEmpty(slot.productionOrderId))
            IAAutoProductionRegistry.ConfirmConstructionStarted(slot.productionOrderId, GetInstanceID(), Time.time);

        // --- CRIAR BARRA DE PROGRESSO ---
        CriarBarraProgresso(slot);

        Debug.Log($"[Estaleiro] Iniciando construção de {prefab.name} no {slot.nomeSlot}. Aguardando conclusão...");
    }

    void CriarBarraProgresso(SlotConstrucao slot)
    {
        if (slot.prefabAtual == null) return;

        GameObject canvasObj = new GameObject("CanvasBarra_" + slot.nomeSlot);
        canvasObj.transform.position = slot.pontoDeConstrucao.position + offsetBarra;
        canvasObj.transform.SetParent(this.transform); // Parente é o estaleiro para organização

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(800, 150); 
        rt.localScale = new Vector3(0.003f, 0.003f, 0.003f); // 70% menor

        // Texto Informativo com Porcentagem
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(canvasObj.transform, false);
        Text txt = txtObj.AddComponent<Text>();
        txt.text = "PREPARANDO... 0%";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // CORRIGIDO: Arial.ttf removido
        txt.fontSize = 80; 
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0f, 0.8f, 1f, 1f); // Azul Neon no texto
        
        Shadow ts = txtObj.AddComponent<Shadow>();
        ts.effectColor = Color.black; 
        ts.effectDistance = new Vector2(4, -4);

        RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;
        rtTxt.sizeDelta = Vector2.zero;

        // Texto plano sem seguir a câmera, com redução e virado 180 graus
        canvasObj.transform.localRotation = Quaternion.Euler(90f, 0f, 180f);

        slot.barCanvasObj = canvasObj;
        slot.barFillImage = null; // Removido linhas visíveis
        slot.textProgresso = txt;
    }

    void ProcessarConstrucao(SlotConstrucao slot)
    {
        // Incrementa progresso
        float incremento = (Time.deltaTime / tempoDeConstrucao) * 100f;
        slot.progresso += incremento;

        // Atualiza Barra de Progresso
        if (slot.barFillImage != null)
        {
            slot.barFillImage.fillAmount = slot.progresso / 100f;
        }
        if (slot.textProgresso != null)
        {
            slot.textProgresso.text = $"PREPARANDO... {Mathf.FloorToInt(slot.progresso)}%";
        }

        // Verifica Conclusão
        if (slot.progresso >= 100f)
        {
            FinalizarConstrucao(slot);
        }
    }

    void FinalizarConstrucao(SlotConstrucao slot)
    {
        NormalizarSlots();
        Debug.Log($"[Estaleiro] Construção finalizada no {slot.nomeSlot}! Nascendo no ponto de atracagem.");

        bool ehNavioGrande = EhNavioGrande(slot.prefabAtual);
        Vector3 waterForward = ObterForwardAgua();
        float nivelAguaAtual = NavalPlacementResolver.ResolveSeaLevel();

        // ======================================================
        // SPAWN DIRETO NO PONTO DE ATRACAGEM (Atracagem / Atracagem_Grande)
        // Os pontos de atracação já estão posicionados na água junto ao estaleiro.
        // Não buscar água aberta - isso causava spawn em cantos distantes do mapa.
        // ======================================================
        Vector3 posFinal;
        if (slot.pontoDeConstrucao != null)
        {
            posFinal = slot.pontoDeConstrucao.position;
        }
        else
        {
            // Fallback: frente do estaleiro na direção da água
            posFinal = transform.position + (waterForward * Mathf.Max(20f, offsetAguaFrente));
        }

        // Ajusta a altura para o nível da água
        posFinal.y = nivelAguaAtual + offsetAltura;

        Quaternion rotacaoNaval = Quaternion.LookRotation(-waterForward, Vector3.up);

        // 1. INSTANCIA O PREFAB CRU E INTACTO!
        // Guardamos a escala antes do Instantiate porque os componentes do
        // navio executam Awake durante a criação. Prefabs navais antigos ou
        // scripts adicionados em runtime podem alterar a raiz nesse momento;
        // a escala configurada no prefab deve ser a autoridade visual final.
        Vector3 escalaPrefab = slot.prefabAtual != null
            ? slot.prefabAtual.transform.localScale
            : Vector3.one;
        long instantiateStart = System.Diagnostics.Stopwatch.GetTimestamp();
        GameObject navioPronto = Instantiate(slot.prefabAtual, posFinal, rotacaoNaval);
        RegistrarTempoDiagnostico("naval_instantiate_ms", instantiateStart);
        long initStart = System.Diagnostics.Stopwatch.GetTimestamp();
        if (slot.prefabAtual != null)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("Spawn", "Navio criado: " + slot.prefabAtual.name);
        }
        navioPronto.transform.SetParent(null);

        // Reaplica a escala do asset depois do Awake/OnEnable dos scripts do
        // navio. Isso faz o aumento feito no Inspector aparecer também no
        // navio recém-liberado pelo estaleiro e não altera sua escala durante
        // a navegação.
        if (navioPronto != null && escalaPrefab.sqrMagnitude > 0.0001f)
        {
            navioPronto.transform.localScale = escalaPrefab;
        }
        if (!string.IsNullOrEmpty(slot.productionOrderId))
            IAAutoProductionRegistry.Complete(slot.productionOrderId, Time.time);

        // Destroi a barra
        if (slot.barCanvasObj != null)
        {
            Destroy(slot.barCanvasObj);
            slot.barCanvasObj = null;
            slot.barFillImage = null;
        }

        // --- LÓGICA DE IDENTIDADE (somente a propria estrutura) ---
        navioPronto.layer = LayerMask.NameToLayer("Default");
        IdentidadeUnidade idEstaleiro = GetComponentInParent<IdentidadeUnidade>();
        IdentidadeUnidade idNavio = navioPronto.GetComponent<IdentidadeUnidade>();
        if (idNavio == null) idNavio = navioPronto.AddComponent<IdentidadeUnidade>();

        int ownerTeam = Mathf.Max(1, OwnerTeamId);
        idNavio.teamID = ownerTeam;
        idNavio.nomeDoPais = idEstaleiro != null
            && idEstaleiro.teamID == ownerTeam
            && !string.IsNullOrEmpty(idEstaleiro.nomeDoPais)
            ? idEstaleiro.nomeDoPais
            : (ownerTeam == 1 ? "Hegemonia" : "Nacao " + ownerTeam);

        idNavio.tipoUnidade = TipoUnidade.Naval;
        CombustivelUnidade.Garantir(navioPronto, true);

        var ctrl = navioPronto.GetComponent<ControleUnidade>();
        if (ctrl == null)
        {
            ctrl = navioPronto.AddComponent<ControleUnidade>();
        }
        else if (!ctrl.enabled)
        {
            ctrl.enabled = true;
        }

        // Calcula destino de saída: usa o pontoDeSaida do estaleiro ou avança na direção da água
        Vector3 destinoSaida;
        if (pontoDeSaida != null)
        {
            destinoSaida = pontoDeSaida.position;
            destinoSaida.y = nivelAguaAtual;
        }
        else
        {
            float distanciaSaida = ehNavioGrande
                ? Mathf.Max(110f, offsetAguaFrente + 70f)
                : Mathf.Max(60f, offsetAguaFrente + 25f);
            destinoSaida = posFinal + (waterForward * distanciaSaida);
            destinoSaida.y = nivelAguaAtual;
        }

        // Remove a movimentação e rotação automáticas em direção ao estaleiro/saída para que fiquem parados na atracagem.
        var agenteNovo = navioPronto.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agenteNovo != null)
        {
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(navioPronto.transform.position, out hit, 20f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agenteNovo.Warp(hit.position);
            }
        }

        var controleSubmarino = navioPronto.GetComponent<ControleSubmarino>();
        if (controleSubmarino != null)
        {
            controleSubmarino.ForcarEstadoSuperficieImediato();
        }

        // Registrar no General se for IA
        if (idNavio.teamID != 1)
        {
            DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("spawn_registrations");
        }

        // --- LÓGICA ESPECÍFICA PARA PETROLEIRO ---
        NavioPetroleiro petroleiro = navioPronto.GetComponent<NavioPetroleiro>();
        if (petroleiro != null)
        {
            petroleiro.DefinirEquipeOperacao(ownerTeam);
            petroleiro.DefinirSaidaEstaleiro(destinoSaida);
        }

        bool ehCargueiro = slot.prefabAtual != null &&
            (slot.prefabAtual.GetComponent<NavioCargaMercado>() != null ||
             slot.prefabAtual.name.IndexOf("navio de carga", System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (ehCargueiro)
        {
            NavioCargaMercado cargueiro = navioPronto.GetComponent<NavioCargaMercado>();
            if (cargueiro == null) cargueiro = navioPronto.AddComponent<NavioCargaMercado>();
            cargueiro.Inicializar(ownerTeam, false);
            // O cargueiro termina no mesmo ponto de atracagem dos demais
            // navios; a logistica pode despacha-lo depois.
            cargueiro.PararNoPonto(posFinal);
        }

        // Efeitos
        if (efeitoConclusao != null)
        {
            Instantiate(efeitoConclusao, slot.pontoDeConstrucao != null ? slot.pontoDeConstrucao.position : posFinal, Quaternion.identity);
        }

        Debug.Log($"[Estaleiro] Navio liberado: {navioPronto.name} | atracagem={slot.nomeSlot} | spawn={navioPronto.transform.position} | saida={destinoSaida}");
        RegistrarTempoDiagnostico("naval_spawn_init_ms", initStart);

        // Libera o slot
        LiberarSlot(slot);
        naviosConcluidosNoLote++;
        ProcessarFimDoLote();
    }

    void ProcessarFimDoLote()
    {
        if (naviosIniciadosNoLote <= 0 || naviosConcluidosNoLote < naviosIniciadosNoLote)
        {
            return;
        }

        if (filaNaval.Count == 0)
        {
            naviosIniciadosNoLote = 0;
            naviosConcluidosNoLote = 0;
            liberarProximoLoteEm = -1f;
            return;
        }

        liberarProximoLoteEm = Time.time + Mathf.Max(0f, intervaloEntreLotes);
    }

    void ProcessarProximoLoteSePronto()
    {
        if (liberarProximoLoteEm < 0f || Time.time < liberarProximoLoteEm)
        {
            return;
        }

        liberarProximoLoteEm = -1f;
        naviosIniciadosNoLote = 0;
        naviosConcluidosNoLote = 0;

        int quantidade = Mathf.Min(2, filaNaval.Count);
        for (int i = 0; i < quantidade; i++)
        {
            PedidoNavalAutomatico pedido = filaNaval.Dequeue();
            SlotConstrucao slot = ObterSlotLivre();
            if (slot == null)
            {
                filaNaval.Enqueue(pedido);
                break;
            }

            IniciarConstrucao(slot, pedido.Prefab, pedido.OrderId);
        }
    }

    void NormalizarSlots()
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = new SlotConstrucao();
            }

            if (string.IsNullOrWhiteSpace(slots[i].nomeSlot))
            {
                slots[i].nomeSlot = i == 0 ? "Atracagem_Grande" : $"Slot_{i}";
            }

            if (slots[i].pontoDeConstrucao != null && string.IsNullOrWhiteSpace(slots[i].pontoDeConstrucao.name))
            {
                slots[i].pontoDeConstrucao.name = slots[i].nomeSlot;
            }
        }
    }

    bool TryResolveLaunchWaterPoint(Vector3 anchor, bool ehNavioGrande, out Vector3 pontoAgua, out float nivelAgua, out string reason)
    {
        pontoAgua = anchor;
        nivelAgua = NavalPlacementResolver.ResolveSeaLevel();
        reason = "sem ponto de agua";

        Vector3 waterForward = ObterForwardAgua();
        var anchors = new List<Vector3>();
        AddLaunchAnchor(anchors, anchor);
        if (pontoDeSaida != null)
        {
            AddLaunchAnchor(anchors, pontoDeSaida.position);
        }

        AddLaunchAnchor(anchors, transform.position + (waterForward * Mathf.Max(20f, offsetAguaFrente)));
        AddLaunchAnchor(anchors, transform.position);

        float raioMinimo = ehNavioGrande ? 20f : 0f;
        float[] raiosMaximos = ehNavioGrande
            ? new[] { Mathf.Max(180f, offsetAguaFrente + 110f), 280f, 380f, 520f }
            : new[] { Mathf.Max(110f, offsetAguaFrente + 40f), 180f, 260f, 360f };

        for (int a = 0; a < anchors.Count; a++)
        {
            for (int r = 0; r < raiosMaximos.Length; r++)
            {
                string tentativaReason;
                Vector3 tentativaPonto;
                float tentativaNivel;
                if (NavalPlacementResolver.TryResolveWaterSpawn(
                    anchors[a],
                    waterForward,
                    raioMinimo,
                    raiosMaximos[r],
                    out tentativaPonto,
                    out tentativaNivel,
                    out tentativaReason))
                {
                    pontoAgua = tentativaPonto;
                    nivelAgua = tentativaNivel;
                    reason = string.Empty;
                    return true;
                }

                reason = tentativaReason;
            }
        }

        return false;
    }

    void AddLaunchAnchor(List<Vector3> anchors, Vector3 candidate)
    {
        if (anchors == null || candidate == Vector3.zero)
        {
            return;
        }

        Vector3 flatCandidate = new Vector3(candidate.x, 0f, candidate.z);
        for (int i = 0; i < anchors.Count; i++)
        {
            Vector3 flatCurrent = new Vector3(anchors[i].x, 0f, anchors[i].z);
            if ((flatCurrent - flatCandidate).sqrMagnitude <= 4f)
            {
                return;
            }
        }

        anchors.Add(candidate);
    }

    void LiberarSlot(SlotConstrucao slot)
    {
        if (slot == null)
        {
            return;
        }

        slot.estaOcupado = false;
        slot.visualAtual = null;
        slot.prefabAtual = null;
        slot.productionOrderId = string.Empty;
        slot.progresso = 0f;
        slot.barCanvasObj = null;
        slot.barFillImage = null;
        slot.textProgresso = null;
    }

    [Header("Indicadores Litorâneos (Terra/Água)")]
    public bool autoAlinharComAgua = true;
    public CoastalPlacementProfile perfilColocacaoCosteira = new CoastalPlacementProfile();
    public float offsetAguaFrente = 35f; 
    public float offsetTerraTras = -15f; 

    void SincronizarPerfilCosteiro()
    {
        if (perfilColocacaoCosteira == null)
        {
            perfilColocacaoCosteira = new CoastalPlacementProfile();
        }

        perfilColocacaoCosteira.offsetAguaFrente = Mathf.Abs(offsetAguaFrente);
        perfilColocacaoCosteira.offsetTerraTras = offsetTerraTras;
    }

    public bool EstaNaConstrucaoValida(float nivelAgua = 0f)
    {
        Vector3 waterForward = ObterForwardAgua();
        Vector3 posFrente = transform.position + waterForward * offsetAguaFrente;
        Vector3 posTras = transform.position + waterForward * offsetTerraTras;

        ClassificacaoSuperficieMapa frenteClassificada;
        ClassificacaoSuperficieMapa trasClassificada;
        float frenteAltura;
        float trasAltura;
        bool frenteMarcada = RegistroSuperficieMapa.TryClassify(posFrente, out frenteClassificada, out frenteAltura);
        bool trasMarcada = RegistroSuperficieMapa.TryClassify(posTras, out trasClassificada, out trasAltura);

        if (frenteMarcada || trasMarcada)
        {
            bool frenteEhAgua = frenteClassificada == ClassificacaoSuperficieMapa.Agua || frenteClassificada == ClassificacaoSuperficieMapa.Costa;
            bool trasEhTerra = trasClassificada == ClassificacaoSuperficieMapa.Chao || trasClassificada == ClassificacaoSuperficieMapa.Costa;
            return frenteEhAgua && trasEhTerra;
        }

        float hFrente = 0f;
        float hTras = 0f;
        
        if (TrySampleTerrainHeight(posFrente, out hFrente))
        {
            TrySampleTerrainHeight(posTras, out hTras);
        }

        // Deve prever a frente perto d'água e traseira em solo alto
        return (hFrente <= nivelAgua + 1f) && (hTras > nivelAgua);
    }

    private static bool TrySampleTerrainHeight(Vector3 position, out float height)
    {
        height = 0f;
        Terrain[] terrains = Terrain.activeTerrains;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null || !terrain.enabled) continue;
            Vector3 minimum = terrain.transform.position;
            Vector3 size = Vector3.Scale(terrain.terrainData.size, terrain.transform.lossyScale);
            if (position.x < minimum.x || position.x > minimum.x + size.x
                || position.z < minimum.z || position.z > minimum.z + size.z) continue;
            height = terrain.SampleHeight(position) + terrain.transform.position.y;
            return true;
        }
        return false;
    }

    void OnDrawGizmos()
    {
        // GIZMO DE COLOCAÇÃO CORRETA (Frente Azul = Água, Atrás Marrom = Terra)
        Vector3 waterForward = ObterForwardAgua();
        Vector3 posAgua = transform.position + waterForward * offsetAguaFrente;
        Vector3 posTerra = transform.position + waterForward * offsetTerraTras;

        Gizmos.color = new Color(0f, 0.4f, 1f, 0.7f); // AZUL = ÁGUA
        Gizmos.DrawSphere(posAgua, 3.5f);
        Gizmos.DrawLine(posAgua, transform.position);

        Gizmos.color = new Color(0.6f, 0.3f, 0f, 0.7f); // MARROM = TERRA FIRME
        Gizmos.DrawSphere(posTerra, 3.5f);
        Gizmos.DrawLine(transform.position, posTerra);
        
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot.pontoDeConstrucao != null)
            {
                Gizmos.color = slot.estaOcupado ? Color.red : Color.green;
                Gizmos.DrawWireCube(slot.pontoDeConstrucao.position, Vector3.one * 5f);
            }
        }
        
        if(pontoDeSaida != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(pontoDeSaida.position, 2f);
        }
    }

    public void AtualizarReferenciasLitoraneas()
    {
        NormalizarSlots();
        if (!autoAlinharComAgua)
        {
            return;
        }

        GarantirSlotsExistentes();

        Vector3 waterForward;
        Vector3 waterPoint;
        float seaLevel;
        if (!NavalPlacementResolver.TryResolveWaterDirection(
            transform.position,
            transform.forward,
            Mathf.Max(10f, Mathf.Abs(offsetAguaFrente) * 0.35f),
            Mathf.Max(140f, Mathf.Abs(offsetAguaFrente) + 80f),
            out waterForward,
            out waterPoint,
            out seaLevel))
        {
            return;
        }

        _ultimoForwardAgua = waterForward;
        Quaternion waterRotation = Quaternion.LookRotation(waterForward, Vector3.up);

        // ======================================================
        // SLOTS: só posicionar automaticamente os que NÃO existem na cena.
        // Filhos já colocados manualmente (Atracagem, Atracagem_Grande) são preservados.
        // ======================================================
        if (slots != null && slots.Length > 0)
        {
            float distanciaFrontalSlots = Mathf.Max(28f, offsetAguaFrente + (EstruturaDoJogadorHumano() ? 12f : 0f));
            Vector3 origemSlots = new Vector3(
                transform.position.x + (waterForward.x * distanciaFrontalSlots),
                seaLevel,
                transform.position.z + (waterForward.z * distanciaFrontalSlots));

            float lateralSpacing = 20f;
            float lateralStart = -((slots.Length - 1) * lateralSpacing) * 0.5f;
            Vector3 lateralAxis = waterRotation * Vector3.right;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(slots[i].nomeSlot))
                {
                    slots[i].nomeSlot = (i == 0) ? "Atracagem_Grande" : $"Slot_{i}";
                }

                if (slots[i].pontoDeConstrucao == null)
                {
                    // Só cria pontos novos para slots que não têm Transform
                    GameObject novoPonto = new GameObject(string.IsNullOrEmpty(slots[i].nomeSlot) ? ("Ponto_Estaleiro_" + i) : slots[i].nomeSlot);
                    novoPonto.transform.SetParent(this.transform);
                    novoPonto.transform.position = origemSlots + (lateralAxis * (lateralStart + (i * lateralSpacing)));
                    novoPonto.transform.rotation = waterRotation;
                    slots[i].pontoDeConstrucao = novoPonto.transform;
                }
                // NÃO sobrescrever posição de slots que já existem na cena!
            }
        }

        // ======================================================
        // PONTO DE SAÍDA: só criar se não existir. NUNCA sobrescrever posição manual.
        // O objeto "saida" colocado na cena é a referência correta (aponta para o mar).
        // ======================================================
        if (pontoDeSaida == null)
        {
            GameObject goSaida = new GameObject("PontoDeSaida_Auto");
            goSaida.transform.SetParent(this.transform);
            pontoDeSaida = goSaida.transform;

            // Só posicionar automaticamente ponto de saída que foi CRIADO por código
            float distanciaFrontalSlots = Mathf.Max(28f, offsetAguaFrente);
            Vector3 origemSlots = new Vector3(
                transform.position.x + (waterForward.x * distanciaFrontalSlots),
                seaLevel,
                transform.position.z + (waterForward.z * distanciaFrontalSlots));

            pontoDeSaida.position = new Vector3(
                origemSlots.x + (waterForward.x * Mathf.Max(45f, offsetAguaFrente + 35f)),
                seaLevel,
                origemSlots.z + (waterForward.z * Mathf.Max(45f, offsetAguaFrente + 35f)));
            pontoDeSaida.rotation = waterRotation;
        }

        RegistrarNoGerente();
    }

    private Vector3 ObterForwardAgua()
    {
        Vector3 waterForward;
        Vector3 waterPoint;
        float seaLevel;
        if (NavalPlacementResolver.TryResolveWaterDirection(
            transform.position,
            _ultimoForwardAgua.sqrMagnitude > 0.01f ? _ultimoForwardAgua : transform.forward,
            Mathf.Max(10f, Mathf.Abs(offsetAguaFrente) * 0.35f),
            Mathf.Max(140f, Mathf.Abs(offsetAguaFrente) + 80f),
            out waterForward,
            out waterPoint,
            out seaLevel))
        {
            _ultimoForwardAgua = waterForward;
            return waterForward;
        }

        Vector3 fallback = _ultimoForwardAgua.sqrMagnitude > 0.01f ? _ultimoForwardAgua : transform.forward;
        fallback.y = 0f;
        if (fallback.sqrMagnitude < 0.01f)
        {
            fallback = Vector3.forward;
        }

        return fallback.normalized;
    }
}
