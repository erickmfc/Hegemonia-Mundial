using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using Hegemonia.AI.BrainMaster;

public class Construtor : MonoBehaviour
{
    private struct BuildPreviewSnapshot
    {
        public Vector3 mouseCell;
        public float timestamp;
        public Vector3 worldPoint;
        public float seaLevel;
        public NavalPlacementResolver.StructurePose pose;
        public bool isValid;
        public string reason;
        public bool usesNavalPosition;
        public bool usesNavalRotation;
        public bool usesManualPlacement;
        public int prefabId;
        public float rotationKey;
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }

    public static Construtor Instancia { get; private set; }
    public static bool EmModoConstrucaoAtivo => Instancia != null && Instancia.modoConstrucao && Instancia.prefabSelecionado != null;
    public static bool CriandoPreviewConstrucao { get; private set; }

    [Header("Configurações")]
    public LayerMask layerChao;
    public float larguraDoMuro = 4.0f;

    [Header("Debug / Estado Atual")]
    public GameObject prefabSelecionado;
    public bool modoConstrucao = false;

    [Header("Naval")]
    public float alturaDoMar = 0.0f;
    public float deslocamentoPadraoEstruturaCosteira = 18f;
    public float distanciaCorrecaoSpawnNaval = 30f;

    private int custoAtual = 0;
    private DadosConstrucao.CategoriaItem categoriaAtual;
    private bool definindoMuro = false;
    private Vector3 pontoInicial;
    private readonly List<GameObject> fantasmasMuro = new List<GameObject>();
    private GameObject fantasmaUnico;
    private float rotacaoExtra = 0f;

    private bool previewLocalInvalido = false;
    private string motivoInvalido = "";
    private bool recemSelecionado = false;
    private Camera cameraPrincipal;
    private Quaternion rotacaoPreviewNaval = Quaternion.identity;
    private bool usarRotacaoPreviewNaval = false;
    private bool previewUsaColocacaoNavalManual = false;
    private Vector3 posicaoPreviewNaval = Vector3.zero;
    private bool usarPosicaoPreviewNaval = false;
    private readonly RaycastHit[] bufferHitsConstrucao = new RaycastHit[96];
    private readonly Collider[] bufferColisoresSnap = new Collider[48];
    private readonly List<UnityEngine.EventSystems.RaycastResult> bufferRaycastUI = new List<UnityEngine.EventSystems.RaycastResult>(16);
    private readonly List<Material> materiaisFantasma = new List<Material>(32);
    private static readonly RaycastHitDistanceComparer ComparadorHitsConstrucao = new RaycastHitDistanceComparer();
    private const float DistanciaCachePreview = 0.75f;
    private const float JanelaCachePreviewSegundos = 0.10f;
    private UnityEngine.EventSystems.PointerEventData pointerEventDataUI;
    private UnityEngine.EventSystems.EventSystem eventSystemUI;
    private bool corFantasmaInvalidaAplicada;
    private bool corFantasmaAplicada;
    private BuildPreviewSnapshot previewSnapshot;
    private bool possuiPreviewSnapshot;
    private GerenteDeTerritorio gerenteTerritorioCache;
    private float proximaBuscaGerenteTerritorio;

    void Awake()
    {
        if (!enabled) return;
        if (Instancia == null) Instancia = this;
    }

    void OnEnable()
    {
        if (!enabled) return;
        if (Instancia == null) Instancia = this;
    }

    void OnDisable()
    {
        if (Instancia == this) Instancia = null;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    void Update()
    {
        if (!modoConstrucao || prefabSelecionado == null) return;
        if (!InteractionModeService.IsActive(InteractionOwner.Construction))
        {
            InteractionModeService.Request(
                InteractionOwner.Construction,
                new InteractionPolicy
                {
                    bloqueiaSelecao = true,
                    bloqueiaOrdemMundo = true,
                    bloqueiaRotacaoCamera = true,
                    consomeLMB = true,
                    consomeRMB = true
                },
                "Construção ativa");
        }

        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
        if (cameraPrincipal == null) return;

        usarRotacaoPreviewNaval = false;
        previewUsaColocacaoNavalManual = false;
        usarPosicaoPreviewNaval = false;

        if (recemSelecionado)
        {
            recemSelecionado = false;
            return;
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelarConstrucao(false);
            return;
        }

        if (IsMouseOverUI())
        {
            if (fantasmaUnico != null) fantasmaUnico.SetActive(false);
            foreach (var f in fantasmasMuro)
            {
                if (f != null) f.SetActive(false);
            }
            return;
        }

        if (fantasmaUnico != null && !fantasmaUnico.activeSelf)
        {
            fantasmaUnico.SetActive(true);
        }

        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        bool acertouChao = false;
        Vector3 pontoMouse = Vector3.zero;

        string nomePrefab = prefabSelecionado.name.ToLower();
        bool ehEstruturaCosteira = EhEstruturaCosteiraPrefab(prefabSelecionado);
        bool ehPlataforma = nomePrefab.Contains("plataforma");
        bool ehConstrucaoNaval = ehEstruturaCosteira || ehPlataforma;

        int layerIgnore = LayerMask.NameToLayer("Ignore Raycast");
        int mascaraGeral = ~(1 << layerIgnore);

        if (ehConstrucaoNaval)
        {
            acertouChao = TryObterPontoNoPlanoDoMar(raio, out pontoMouse);

            if (acertouChao)
            {
                pontoMouse.y = NavalPlacementResolver.ResolveSeaLevel();

                if (ehPlataforma)
                {
                    pontoMouse.y = 30.0f;
                }

                if (!ehEstruturaCosteira && ExisteTerraAltaNoPontoMaritimo(pontoMouse, mascaraGeral))
                {
                    acertouChao = false;
                }
            }
        }
        else
        {
            acertouChao = TryObterPontoDeConstrucaoTerrestre(raio, mascaraGeral, out pontoMouse);
        }

        if (!acertouChao)
        {
            usarRotacaoPreviewNaval = false;
            usarPosicaoPreviewNaval = false;
            InvalidarPreviewSnapshot();
            return;
        }

        bool ehMuro = prefabSelecionado.name.Contains("Muro") || prefabSelecionado.name.Contains("Fence");
        bool precisaRecalcularPreview = !PodeReutilizarPreview(pontoMouse);
        long previewStart = 0L;

        if (precisaRecalcularPreview)
        {
            previewStart = System.Diagnostics.Stopwatch.GetTimestamp();
            if (ehEstruturaCosteira)
            {
                AtualizarPreviewCosteiroLeve(pontoMouse);
                pontoMouse = usarPosicaoPreviewNaval ? posicaoPreviewNaval : pontoMouse;
            }
            else
            {
                usarRotacaoPreviewNaval = false;
                usarPosicaoPreviewNaval = false;
                previewLocalInvalido = false;
                motivoInvalido = "";
            }

            if (!previewLocalInvalido)
            {
                ValidarTerritorio(pontoMouse, ehPlataforma, ehEstruturaCosteira);
            }

            SalvarPreviewSnapshot(pontoMouse);
            string chavePreview = ehConstrucaoNaval ? "naval_preview_ms" : "constructor_preview_ms";
            float previewMs = MedirETrazerTempoDiagnostico(chavePreview, previewStart);
            if (previewMs >= 25f)
            {
                DiagnosticoDesempenhoJogo.RegistrarEvento(
                    "PreviewHitch",
                    string.Format("{0} levou {1:0.0}ms em {2}", chavePreview, previewMs, prefabSelecionado.name));
            }
        }
        else
        {
            RestaurarPreviewSnapshot();
            if (usarPosicaoPreviewNaval)
            {
                pontoMouse = posicaoPreviewNaval;
            }
        }

        if (ehMuro) GerenciarConstrucaoMuro(pontoMouse);
        else GerenciarConstrucaoNormal(pontoMouse);
    }

    bool TryObterPontoNoPlanoDoMar(Ray raio, out Vector3 ponto)
    {
        ponto = Vector3.zero;
        float nivelDoMar = NavalPlacementResolver.ResolveSeaLevel();

        float denominador = Vector3.Dot(raio.direction, Vector3.up);
        if (Mathf.Abs(denominador) < 0.0001f)
        {
            return false;
        }

        float distancia = (nivelDoMar - raio.origin.y) / denominador;
        if (distancia < 0f)
        {
            return false;
        }

        ponto = raio.origin + (raio.direction * distancia);
        ponto.y = nivelDoMar;
        return true;
    }

    bool ExisteTerraAltaNoPontoMaritimo(Vector3 pontoNoMar, int mascaraGeral)
    {
        float nivelDoMar = NavalPlacementResolver.ResolveSeaLevel();
        RaycastHit infoTerreno;
        Vector3 origemCeu = new Vector3(pontoNoMar.x, nivelDoMar + 500f, pontoNoMar.z);

        if (!Physics.Raycast(origemCeu, Vector3.down, out infoTerreno, 1000f, mascaraGeral))
        {
            return false;
        }

        if (infoTerreno.collider == null)
        {
            return false;
        }

        string nomeCollider = infoTerreno.collider.name.ToLower();
        bool bateuEmAguaOuNaval = nomeCollider.Contains("agua") ||
                                  nomeCollider.Contains("water") ||
                                  infoTerreno.collider.gameObject.layer == 4;

        if (bateuEmAguaOuNaval)
        {
            return false;
        }

        return infoTerreno.point.y > nivelDoMar + 1.0f;
    }

    bool TryObterPontoDeConstrucaoTerrestre(Ray raio, int mascaraGeral, out Vector3 pontoMouse)
    {
        pontoMouse = Vector3.zero;
        RaycastHit toque;

        if (layerChao.value != 0 && Physics.Raycast(raio, out toque, 1000f, layerChao))
        {
            pontoMouse = toque.point;
            return true;
        }

        int quantidadeHits = Physics.RaycastNonAlloc(raio, bufferHitsConstrucao, 2000f, mascaraGeral, QueryTriggerInteraction.Ignore);
        if (quantidadeHits >= bufferHitsConstrucao.Length)
        {
            DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("preview_overflow_count");
        }
        System.Array.Sort(bufferHitsConstrucao, 0, quantidadeHits, ComparadorHitsConstrucao);

        for (int i = 0; i < quantidadeHits; i++)
        {
            RaycastHit h = bufferHitsConstrucao[i];
            if (h.collider == null) continue;
            string n = h.collider.name.ToLower();

            if (n.Contains("bip001") || n.Contains("bone") || n.Contains("finger") || n.Contains("cube"))
                continue;

            if (h.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null)
                continue;

            if (h.collider.GetComponentInParent<ControleUnidade>() != null)
                continue;

            pontoMouse = h.point;
            return true;
        }

        return false;
    }

    void LiberarPreviewCosteiroSemRestricao(Vector3 pontoMouse)
    {
        float nivelDoMar = NavalPlacementResolver.ResolveSeaLevel();
        Quaternion rotacaoBase = fantasmaUnico != null ? fantasmaUnico.transform.rotation : prefabSelecionado.transform.rotation;

        posicaoPreviewNaval = pontoMouse;
        posicaoPreviewNaval.y = nivelDoMar;
        usarPosicaoPreviewNaval = true;
        previewLocalInvalido = false;
        motivoInvalido = "";
        previewUsaColocacaoNavalManual = true;

        Vector3 frente = rotacaoBase * Vector3.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.001f)
        {
            frente = Vector3.forward;
        }

        frente.Normalize();
        rotacaoPreviewNaval = Quaternion.LookRotation(frente, Vector3.up);
        usarRotacaoPreviewNaval = true;
    }

    void AtualizarPreviewCosteiroLeve(Vector3 pontoMouse)
    {
        Quaternion rotacaoBase = fantasmaUnico != null ? fantasmaUnico.transform.rotation : prefabSelecionado.transform.rotation;
        NavalPlacementResolver.PlacementContext contexto = NavalPlacementResolver.BuildPlacementContext(prefabSelecionado, rotacaoBase, true);
        NavalPlacementResolver.StructurePose pose;
        if (NavalPlacementResolver.TryResolvePreviewPose(prefabSelecionado, pontoMouse, contexto, out pose))
        {
            posicaoPreviewNaval = pose.Position;
            usarPosicaoPreviewNaval = true;
            rotacaoPreviewNaval = pose.Rotation;
            usarRotacaoPreviewNaval = true;
            previewLocalInvalido = false;
            motivoInvalido = string.Empty;
            previewUsaColocacaoNavalManual = true;
            return;
        }

        // Sem restrição — permite colocar livremente em qualquer lugar
        LiberarPreviewCosteiroSemRestricao(pontoMouse);
    }

    void ValidarTerritorio(Vector3 ponto, bool ehPlataforma, bool ehEstruturaCosteira)
    {
        GerenteDeTerritorio gerenteTerritorio = ObterGerenteTerritorio(true);
        if (gerenteTerritorio == null)
        {
            previewLocalInvalido = false;
            motivoInvalido = "";
            return;
        }

        int donoDoPonto = gerenteTerritorio.ObterDonoDoPonto(ponto);
        int meuTime = 1;

        bool ehPrefeitura = prefabSelecionado.GetComponent<ComplexoGovernamental>() != null ||
                            prefabSelecionado.name.ToLower().Contains("prefeitura") ||
                            prefabSelecionado.name.ToLower().Contains("complexo");

        bool ehBandeira = prefabSelecionado.name.ToLower().Contains("bandeira") ||
                          prefabSelecionado.name.ToLower().Contains("flag") ||
                          prefabSelecionado.GetComponent<MarcadorTerritorio>() != null;

        if (!ehPrefeitura && !ehBandeira && !ehPlataforma && !ehEstruturaCosteira)
        {
            if (donoDoPonto != meuTime)
            {
                previewLocalInvalido = true;
                motivoInvalido = "❌ TERRITÓRIO NÃO REIVINDICADO:\nConstrua dentro das linhas do seu País ou expanda plantando Bandeiras.";
                return;
            }
        }

        if (ehPrefeitura)
        {
            if (donoDoPonto != 0 && donoDoPonto != meuTime)
            {
                previewLocalInvalido = true;
                motivoInvalido = "❌ INVASÃO DIRETA:\nVocê não pode fundar a Prefeitura/Capital em um país inimigo.";
                return;
            }

            if (!gerenteTerritorio.PodeConstruirPrefeitura(ponto))
            {
                previewLocalInvalido = true;
                motivoInvalido = "❌ JÁ EXISTE LEI AQUI:\nEsta ilha já possui uma Prefeitura.";
                return;
            }
        }

        if (ehBandeira)
        {
            if (donoDoPonto != 0 && donoDoPonto != meuTime)
            {
                previewLocalInvalido = true;
                motivoInvalido = "❌ JURISDIÇÃO INIMIGA:\nA soberania desta área já pertence a outra Nação.";
                return;
            }
        }

        previewLocalInvalido = false;
        motivoInvalido = "";
    }

    void GerenciarConstrucaoNormal(Vector3 ponto)
    {
        if (fantasmaUnico == null)
        {
            if (prefabSelecionado == null)
            {
                Debug.LogError("[Construtor] Erro: prefabSelecionado é nulo ao tentar criar fantasma!");
                return;
            }

            GameObject containerSeguro = new GameObject("ContainerSeguro_Construtor");
            containerSeguro.SetActive(false);

            CriandoPreviewConstrucao = true;
            try
            {
                fantasmaUnico = Instantiate(prefabSelecionado, ponto, Quaternion.identity, containerSeguro.transform);
            }
            finally
            {
                CriandoPreviewConstrucao = false;
            }
            
            // Garantir que a escala não seja zero na raiz (comum em prefabs animados)
            if (fantasmaUnico.transform.localScale.sqrMagnitude < 0.0001f)
            {
                fantasmaUnico.transform.localScale = Vector3.one;
                Debug.LogWarning($"[Construtor] Fantasma de {prefabSelecionado.name} tinha escala zero na raiz. Forçando para 1,1,1.");
            }

            // Forçar todos os renderers a ficarem ativos e verificar escalas zeradas nos filhos
            Renderer[] todosRenderers = fantasmaUnico.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in todosRenderers)
            {
                if (r.transform.localScale.sqrMagnitude < 0.0001f)
                {
                    r.transform.localScale = Vector3.one;
                }
                
                // Ativar o GameObject do renderer caso venha desativado por padrão no prefab
                if (!r.gameObject.activeSelf)
                {
                    r.gameObject.SetActive(true);
                }
            }

            RemoverColisoresEScripts(fantasmaUnico);
            SetLayerRecursively(fantasmaUnico, LayerMask.NameToLayer("Default"));
            fantasmaUnico.transform.SetParent(null);
            Destroy(containerSeguro);
            fantasmaUnico.SetActive(true);
            CacheMateriaisFantasma();
            corFantasmaAplicada = false;

            Debug.Log($"[Construtor] Fantasma criado com sucesso para: {prefabSelecionado.name}. Renderers ativados: {todosRenderers.Length}");
        }

        Vector3 posFinalPreview = usarPosicaoPreviewNaval ? posicaoPreviewNaval : ponto;
        
        bool fezSnapImovel = false;
        Imovel imovelPrefab = prefabSelecionado.GetComponent<Imovel>();
        if (imovelPrefab != null && !usarPosicaoPreviewNaval)
        {
            float raioBusca = imovelPrefab.distanciaConexao * 2f;
            int totalCols = Physics.OverlapSphereNonAlloc(posFinalPreview, raioBusca, bufferColisoresSnap, ~0, QueryTriggerInteraction.Ignore);
            if (totalCols >= bufferColisoresSnap.Length)
            {
                DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("preview_overflow_count");
            }
            Imovel imovelProximo = null;
            float menorDistancia = float.MaxValue;

            for (int i = 0; i < totalCols; i++)
            {
                Collider col = bufferColisoresSnap[i];
                if (col == null) continue;
                Imovel vizinho = col.GetComponentInParent<Imovel>();
                if (vizinho != null && vizinho.gameObject != fantasmaUnico)
                {
                    float dist = Vector3.Distance(posFinalPreview, vizinho.transform.position);
                    if (dist < menorDistancia)
                    {
                        menorDistancia = dist;
                        imovelProximo = vizinho;
                    }
                }
            }

            if (imovelProximo != null)
            {
                Vector3 pEsq = imovelProximo.ObterPontoEsquerdo();
                Vector3 pDir = imovelProximo.ObterPontoDireito();
                
                float distEsq = Vector3.Distance(posFinalPreview, pEsq);
                float distDir = Vector3.Distance(posFinalPreview, pDir);
                
                if (distEsq < distDir && distEsq < imovelPrefab.distanciaConexao * 1.5f)
                {
                    fantasmaUnico.transform.rotation = imovelProximo.transform.rotation;
                    posFinalPreview = pEsq - (fantasmaUnico.transform.right * imovelPrefab.distanciaConexao);
                    fezSnapImovel = true;
                }
                else if (distDir <= distEsq && distDir < imovelPrefab.distanciaConexao * 1.5f)
                {
                    fantasmaUnico.transform.rotation = imovelProximo.transform.rotation;
                    posFinalPreview = pDir + (fantasmaUnico.transform.right * imovelPrefab.distanciaConexao);
                    fezSnapImovel = true;
                }
            }
        }

        fantasmaUnico.transform.position = posFinalPreview;

        if (usarRotacaoPreviewNaval)
        {
            fantasmaUnico.transform.rotation = rotacaoPreviewNaval;
        }

        AplicarCorNoFantasma(fantasmaUnico, previewLocalInvalido);

        if (Input.GetKeyDown(KeyCode.R) && !usarRotacaoPreviewNaval && !fezSnapImovel)
        {
            fantasmaUnico.transform.Rotate(0f, 90f, 0f);
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (previewLocalInvalido)
        {
            Debug.LogWarning($"⚠️ [Construtor] Abortando: {motivoInvalido}");
            return;
        }

        long confirmStart = System.Diagnostics.Stopwatch.GetTimestamp();
        Vector3 posFinal = fantasmaUnico.transform.position;
        Quaternion rotFinal = fantasmaUnico.transform.rotation;

        // Estruturas costeiras usam a posição do preview diretamente, sem revalidação
        if (EhEstruturaCosteiraPrefab(prefabSelecionado))
        {
            NavalPlacementResolver.StructurePose poseCommit;
            if (NavalPlacementResolver.TryResolveStructurePose(prefabSelecionado, ponto, rotFinal, out poseCommit))
            {
                posFinal = poseCommit.Position;
                rotFinal = poseCommit.Rotation;
            }
            // Se falhar, usa posFinal/rotFinal do fantasma (preview) sem bloquear
        }

        if (!TentarCobrarConstrucao(custoAtual))
        {
            return;
        }

        GameObject novo = Instantiate(prefabSelecionado, posFinal, rotFinal);

        if (EhEstruturaCosteiraPrefab(prefabSelecionado))
        {
            IA_ManualPlacementTag manualTag = novo.GetComponent<IA_ManualPlacementTag>();
            if (manualTag == null)
            {
                manualTag = novo.AddComponent<IA_ManualPlacementTag>();
            }
            manualTag.SourceLabel = previewUsaColocacaoNavalManual ? "Construtor jogador (manual)" : "Construtor jogador";
        }

        ReativarLogicaUnidade(novo);
        EnsureCollider(novo);

        Estaleiro estaleiro = novo.GetComponent<Estaleiro>();
        if (estaleiro != null)
        {
            estaleiro.AtualizarReferenciasLitoraneas();
            TentarFixarSpawnNaval(estaleiro.gameObject, rotFinal, true);
        }

        PierMarinha pier = novo.GetComponent<PierMarinha>();
        if (pier != null)
        {
            pier.RegistrarNoGerente();
            TentarFixarSpawnNaval(pier.gameObject, rotFinal, true);
        }

        Vector3 escalaOriginal = novo.transform.localScale;
        if (escalaOriginal.sqrMagnitude < 0.0001f)
        {
            escalaOriginal = Vector3.one;
            novo.transform.localScale = Vector3.one;
        }

        novo.transform.localScale = Vector3.zero;
        AnimadorConstrucao anim = novo.AddComponent<AnimadorConstrucao>();
        anim.IniciarAnimacao(escalaOriginal, 1.5f);

        float constructorConfirmMs = MedirETrazerTempoDiagnostico("constructor_confirm_ms", confirmStart);
        if (EhEstruturaCosteiraPrefab(prefabSelecionado))
        {
            float navalCommitMs = MedirETrazerTempoDiagnostico("naval_commit_ms", confirmStart);
            if (navalCommitMs >= 25f)
            {
                DiagnosticoDesempenhoJogo.RegistrarEvento(
                    "SpawnHitch",
                    string.Format("naval_commit_ms levou {0:0.0}ms em {1}", navalCommitMs, prefabSelecionado.name));
            }
        }
        if (constructorConfirmMs >= 50f)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento(
                constructorConfirmMs >= 600f ? "TrueFreeze" : "SpawnHitch",
                string.Format("constructor_confirm_ms levou {0:0.0}ms em {1}", constructorConfirmMs, prefabSelecionado.name));
        }

        CancelarConstrucao(false);
    }

    bool TentarCobrarConstrucao(int custo)
    {
        if (custo <= 0)
        {
            return true;
        }

        GerenteDeJogo gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        if (gerente != null && gerente.TentarGastarDinheiro(custo))
        {
            return true;
        }

        if (gerente == null && GerenciadorRecursos.Instancia != null && GerenciadorRecursos.Instancia.TentarGastar(custoDinheiro: custo))
        {
            return true;
        }

        string mensagem = $"Fundos insuficientes para construir. Custo: ${custo}.";
        Debug.LogWarning($"[Construtor] {mensagem}");
        HUDAjudaRTS.MostrarMensagemTemporaria(mensagem, 3.2f);
        return false;
    }

    bool EhEstruturaCosteiraPrefab(GameObject prefab)
    {
        if (prefab == null) return false;
        string nome = prefab.name.ToLower();
        return nome.Contains("estaleiro") || nome.Contains("pier");
    }

    bool TryResolverPoseCosteiraManual(GameObject prefab, Vector3 pontoMouse, Quaternion rotacaoBase, out NavalPlacementResolver.StructurePose pose)
    {
        float nivelDoMar = NavalPlacementResolver.ResolveSeaLevel();
        pose = new NavalPlacementResolver.StructurePose
        {
            Position = new Vector3(pontoMouse.x, nivelDoMar, pontoMouse.z),
            Rotation = rotacaoBase,
            SeaLevel = nivelDoMar,
            Reason = "sem costa valida"
        };

        Vector3 fallbackForward = rotacaoBase * Vector3.forward;
        if (fallbackForward.sqrMagnitude < 0.01f)
        {
            fallbackForward = Vector3.forward;
        }

        float frenteAgua = 35f;
        float trasTerra = 15f;

        Estaleiro estaleiro = prefab != null ? prefab.GetComponent<Estaleiro>() : null;
        if (estaleiro != null)
        {
            frenteAgua = Mathf.Max(frenteAgua, Mathf.Abs(estaleiro.offsetAguaFrente));
            trasTerra = Mathf.Max(trasTerra, Mathf.Abs(estaleiro.offsetTerraTras));
        }

        PierMarinha pier = prefab != null ? prefab.GetComponent<PierMarinha>() : null;
        if (pier != null)
        {
            frenteAgua = Mathf.Max(frenteAgua, Mathf.Abs(pier.offsetAguaFrente));
            trasTerra = Mathf.Max(trasTerra, Mathf.Abs(pier.offsetTerraTras));
        }

        Vector3 waterForward;
        Vector3 waterPoint;
        if (!NavalPlacementResolver.TryResolveWaterDirection(
            pontoMouse,
            fallbackForward,
            8f,
            Mathf.Max(180f, frenteAgua + 120f),
            out waterForward,
            out waterPoint,
            out nivelDoMar))
        {
            pose.Reason = "sem agua proxima";
            return false;
        }

        Vector3 posBase = new Vector3(pontoMouse.x, nivelDoMar, pontoMouse.z);
        Vector3 frente = posBase + (waterForward * Mathf.Max(18f, frenteAgua * 0.70f));
        Vector3 tras = posBase - (waterForward * Mathf.Max(12f, trasTerra));

        bool temAguaNaFrente = NavalPlacementResolver.IsWaterAtPosition(frente, nivelDoMar);
        bool temAguaAtras = NavalPlacementResolver.IsWaterAtPosition(tras, nivelDoMar);

        if (!temAguaNaFrente)
        {
            pose.Reason = "sem agua na frente";
            return false;
        }

        if (temAguaAtras)
        {
            pose.Reason = "sem terra atras";
            return false;
        }

        float empurraoParaAgua = Mathf.Clamp(frenteAgua * 0.45f, 10f, Mathf.Max(28f, deslocamentoPadraoEstruturaCosteira));
        Vector3 posFinal = posBase + (waterForward * empurraoParaAgua);
        posFinal.y = nivelDoMar;

        Vector3 checagemTerraAtras = posFinal - (waterForward * Mathf.Max(trasTerra, 10f));
        Vector3 checagemAguaFrente = posFinal + (waterForward * Mathf.Max(frenteAgua * 0.60f, 14f));

        bool validouTerraAtras = !NavalPlacementResolver.IsWaterAtPosition(checagemTerraAtras, nivelDoMar);
        bool validouAguaFrente = NavalPlacementResolver.IsWaterAtPosition(checagemAguaFrente, nivelDoMar);

        if (!validouTerraAtras)
        {
            pose.Reason = "pivot ficou avancado demais na agua";
            return false;
        }

        if (!validouAguaFrente)
        {
            pose.Reason = "saida naval continuou sem agua";
            return false;
        }

        pose.Position = posFinal;
        pose.Rotation = Quaternion.LookRotation(waterForward, Vector3.up);
        pose.SeaLevel = nivelDoMar;
        pose.Reason = string.Empty;
        return true;
    }

    void TentarFixarSpawnNaval(GameObject estrutura, Quaternion rotacao, bool logar)
    {
        if (estrutura == null) return;

        Transform[] filhos = estrutura.GetComponentsInChildren<Transform>(true);
        Vector3 forward = rotacao * Vector3.forward;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = estrutura.transform.forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        foreach (Transform t in filhos)
        {
            if (t == null) continue;

            string nome = t.name.ToLower();
            bool pareceSpawn = nome.Contains("spawn") || nome.Contains("saida") || nome.Contains("launch") || nome.Contains("navio");
            if (!pareceSpawn) continue;

            Vector3 corrigido = estrutura.transform.position + (forward * distanciaCorrecaoSpawnNaval);
            corrigido.y = alturaDoMar;
            t.position = corrigido;

            if (logar)
            {
                Debug.Log($"[Construtor] Spawn naval forçado em {estrutura.name} -> {t.name} para {corrigido}");
            }
        }
    }

    void CacheMateriaisFantasma()
    {
        materiaisFantasma.Clear();
        if (fantasmaUnico == null)
        {
            return;
        }

        Renderer[] renders = fantasmaUnico.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renders)
        {
            if (r == null) continue;

            Material[] materiais = r.materials;
            for (int i = 0; i < materiais.Length; i++)
            {
                if (materiais[i] != null)
                {
                    materiaisFantasma.Add(materiais[i]);
                }
            }
        }
    }

    void AplicarCorNoFantasma(GameObject fantasma, bool ehInvalido)
    {
        if (fantasma == null)
        {
            return;
        }

        if (materiaisFantasma.Count == 0)
        {
            CacheMateriaisFantasma();
        }

        if (corFantasmaAplicada && corFantasmaInvalidaAplicada == ehInvalido)
        {
            return;
        }

        Color cor = ehInvalido ? new Color(1f, 0.2f, 0.2f, 0.6f) : new Color(0.2f, 1f, 0.2f, 0.6f);
        for (int i = 0; i < materiaisFantasma.Count; i++)
        {
            Material mat = materiaisFantasma[i];
            if (mat == null) continue;

            mat.color = cor;
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        corFantasmaAplicada = true;
        corFantasmaInvalidaAplicada = ehInvalido;
    }

    public class AnimadorConstrucao : MonoBehaviour
    {
        private Vector3 alvoEscala;
        private float duracao;
        private float tempo;

        public void IniciarAnimacao(Vector3 escalaFinal, float tempoTotal)
        {
            alvoEscala = escalaFinal;
            duracao = tempoTotal;
            tempo = 0f;
        }

        void Update()
        {
            tempo += Time.deltaTime;
            float t = Mathf.Clamp01(tempo / duracao);
            float curva = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.Lerp(Vector3.zero, alvoEscala, curva);

            if (tempo >= duracao)
            {
                transform.localScale = alvoEscala;
                Destroy(this);
            }
        }
    }

    void DesativarLogicaUnidade(GameObject unidade)
    {
        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        MonoBehaviour[] scripts = unidade.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            script.enabled = false;
        }
    }

    void ReativarLogicaUnidade(GameObject unidade)
    {
        MonoBehaviour[] scripts = unidade.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;

            if (script is Construtor)
            {
                script.enabled = false;
                continue;
            }

            script.enabled = true;
        }

        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
        }

        SetLayerRecursively(unidade, LayerMask.NameToLayer("Default"));
    }

    void GerenciarConstrucaoMuro(Vector3 pontoAtual)
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            rotacaoExtra += 90f;
            if (rotacaoExtra >= 360f) rotacaoExtra = 0f;
        }

        if (!definindoMuro)
        {
            AtualizarFantasmas(1, pontoAtual, pontoAtual);

            if (Input.GetMouseButtonDown(0))
            {
                definindoMuro = true;
                pontoInicial = pontoAtual;
            }
        }
        else
        {
            Vector3 direcao = pontoAtual - pontoInicial;
            float distancia = direcao.magnitude;
            int quantidadePecas = Mathf.Max(1, Mathf.RoundToInt(distancia / larguraDoMuro));
            Vector3 pontoFinalAjustado = pontoInicial + (direcao.normalized * (quantidadePecas * larguraDoMuro));

            AtualizarFantasmas(quantidadePecas, pontoInicial, pontoFinalAjustado);

            if (Input.GetMouseButtonDown(0))
            {
                int custoTotal = Mathf.Max(0, custoAtual) * quantidadePecas;
                if (!TentarCobrarConstrucao(custoTotal))
                {
                    return;
                }

                ConstruirLinhaDeMuro(quantidadePecas, pontoInicial, pontoFinalAjustado);
                definindoMuro = false;
                CancelarConstrucao(false);
            }
        }
    }

    void AtualizarFantasmas(int quantidade, Vector3 inicio, Vector3 fim)
    {
        while (fantasmasMuro.Count < quantidade)
        {
            GameObject containerSeguro = new GameObject("ContainerSeguro_Muro");
            containerSeguro.SetActive(false);

            GameObject g = Instantiate(prefabSelecionado, containerSeguro.transform);
            RemoverColisoresEScripts(g);
            SetLayerRecursively(g, LayerMask.NameToLayer("Ignore Raycast"));
            g.transform.SetParent(null);
            Destroy(containerSeguro);
            fantasmasMuro.Add(g);
        }

        Vector3 dir = (fim - inicio).normalized;
        if (dir == Vector3.zero)
        {
            dir = Vector3.forward;
        }

        Quaternion rotacaoBase = Quaternion.LookRotation(dir);
        Quaternion rotacaoFinal = rotacaoBase * Quaternion.Euler(0f, rotacaoExtra, 0f);

        for (int i = 0; i < quantidade; i++)
        {
            fantasmasMuro[i].SetActive(true);
            fantasmasMuro[i].transform.position = inicio + (dir * (i * larguraDoMuro)) + (dir * (larguraDoMuro / 2f));
            fantasmasMuro[i].transform.rotation = rotacaoFinal;
        }

        for (int i = quantidade; i < fantasmasMuro.Count; i++)
        {
            fantasmasMuro[i].SetActive(false);
        }
    }

    void ConstruirLinhaDeMuro(int quantidade, Vector3 inicio, Vector3 fim)
    {
        Vector3 dir = (fim - inicio).normalized;
        Quaternion rotacaoBase = Quaternion.LookRotation(dir);
        Quaternion rotacaoFinal = rotacaoBase * Quaternion.Euler(0f, rotacaoExtra, 0f);

        for (int i = 0; i < quantidade; i++)
        {
            Vector3 pos = inicio + (dir * (i * larguraDoMuro)) + (dir * (larguraDoMuro / 2f));
            GameObject novoMuro = Instantiate(prefabSelecionado, pos, rotacaoFinal);
            ReativarLogicaUnidade(novoMuro);
            EnsureCollider(novoMuro);
        }
    }

    public GameObject ConstruirEstruturaIA(GameObject prefab, Vector3 posicao, Quaternion rotacao)
    {
        if (prefab == null) return null;
        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("spawn_prefab_name", prefab.name);

        long instantiateStart = System.Diagnostics.Stopwatch.GetTimestamp();
        GameObject novoPredio = Instantiate(prefab, posicao, rotacao);
        RegistrarTempoDiagnostico("spawn_structure_ms", instantiateStart);
        long initStart = System.Diagnostics.Stopwatch.GetTimestamp();
        EnsureCollider(novoPredio);

        Estaleiro estaleiro = novoPredio.GetComponent<Estaleiro>();
        if (estaleiro != null)
        {
            estaleiro.AtualizarReferenciasLitoraneas();
            TentarFixarSpawnNaval(estaleiro.gameObject, rotacao, false);
        }

        PierMarinha pier = novoPredio.GetComponent<PierMarinha>();
        if (pier != null)
        {
            pier.RegistrarNoGerente();
            TentarFixarSpawnNaval(pier.gameObject, rotacao, false);
        }

        if (!Application.isEditor)
        {
            Debug.Log($"[Construtor IA] Construiu {prefab.name} em {posicao}");
        }

        RegistrarTempoDiagnostico("prefab_init_ms", initStart);

        return novoPredio;
    }

    public void SelecionarParaConstruir(GameObject prefab, int custo, DadosConstrucao.CategoriaItem categoria)
    {
        if (modoConstrucao)
        {
            if (prefabSelecionado == prefab)
            {
                recemSelecionado = true;
                return;
            }

            CancelarConstrucao(false);
        }

        SuspenderInteracoesConcorrentes();
        prefabSelecionado = prefab;
        custoAtual = custo;
        categoriaAtual = categoria;
        modoConstrucao = true;
        recemSelecionado = true;
        InvalidarPreviewSnapshot();
        ObterGerenteTerritorio(true);
        InteractionModeService.Request(
            InteractionOwner.Construction,
            new InteractionPolicy
            {
                bloqueiaSelecao = true,
                bloqueiaOrdemMundo = true,
                bloqueiaRotacaoCamera = true,
                consomeLMB = true,
                consomeRMB = true
            },
            "Construção ativa");

        Debug.Log($"[Construtor] MODO CONSTRUÇÃO ATIVADO para: {prefab.name}. Custo: {custo}. Categoria: {categoria}");
    }

    public void CancelarConstrucao(bool reembolsar = false)
    {
        if (reembolsar && custoAtual > 0)
        {
            GerenteDeJogo gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
            if (gerente != null)
            {
                gerente.dinheiroAtual += custoAtual;
                Debug.Log($"[Construtor] Reembolsado ${custoAtual} (Gerente Antigo)");
            }
            else if (GerenciadorRecursos.Instancia != null)
            {
                GerenciadorRecursos.Instancia.AdicionarRecursos(addDinheiro: custoAtual);
                Debug.Log($"[Construtor] Reembolsado ${custoAtual}");
            }
        }

        modoConstrucao = false;
        definindoMuro = false;
        prefabSelecionado = null;
        custoAtual = 0;
        rotacaoExtra = 0f;
        usarPosicaoPreviewNaval = false;
        usarRotacaoPreviewNaval = false;
        previewUsaColocacaoNavalManual = false;
        previewLocalInvalido = false;
        motivoInvalido = "";
        InvalidarPreviewSnapshot();
        InteractionModeService.Release(InteractionOwner.Construction);

        if (fantasmaUnico != null)
        {
            Destroy(fantasmaUnico);
        }
        fantasmaUnico = null;
        materiaisFantasma.Clear();
        corFantasmaAplicada = false;

        foreach (var f in fantasmasMuro)
        {
            if (f != null) Destroy(f);
        }
        fantasmasMuro.Clear();
    }

    void SuspenderInteracoesConcorrentes()
    {
        if (ModoDemolicao.TemModoAtivo)
        {
            ModoDemolicao.Instancia.AlternarModo(false);
        }

        InteractionModeService.Release(InteractionOwner.Patrol);
        InteractionModeService.Release(InteractionOwner.Follow);
        InteractionModeService.Release(InteractionOwner.AirportOrder);
        InteractionModeService.Release(InteractionOwner.CarrierOrder);
        InteractionModeService.Release(InteractionOwner.ManualFire);

        MenuMisseis menuMisseis = Object.FindFirstObjectByType<MenuMisseis>();
        if (menuMisseis != null)
        {
            menuMisseis.CancelarLancamento();
        }

        GerenciadorAeroporto[] aeroportos = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        foreach (GerenciadorAeroporto aeroporto in aeroportos)
        {
            if (aeroporto != null)
            {
                aeroporto.CancelarInteracaoPorConstrucao();
            }
        }

        NavioTransporteTropas[] naviosTransporte = Object.FindObjectsByType<NavioTransporteTropas>(FindObjectsSortMode.None);
        foreach (NavioTransporteTropas navio in naviosTransporte)
        {
            if (navio != null)
            {
                navio.CancelarInteracaoPorConstrucao();
            }
        }
    }

    void RemoverColisoresEScripts(GameObject obj)
    {
        Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            c.enabled = false;
            Destroy(c);
        }

        UnityEngine.AI.NavMeshObstacle[] navs = obj.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
        foreach (var n in navs)
        {
            Destroy(n);
        }

        MonoBehaviour[] scripts = obj.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var s in scripts)
        {
            if (s == null) continue;
            if (s == this) continue;
            s.enabled = false;
        }
    }

    void EnsureCollider(GameObject obj)
    {
        if (obj.GetComponent<Fazenda>() != null)
        {
            if (obj.GetComponentInChildren<Collider>() == null)
            {
                Renderer r = obj.GetComponentInChildren<Renderer>();
                BoxCollider box = obj.AddComponent<BoxCollider>();
                if (r != null)
                {
                    Bounds b = r.bounds;
                    box.center = obj.transform.InverseTransformPoint(b.center);
                    Vector3 localSize = obj.transform.InverseTransformVector(b.size);
                    box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
                }
                else
                {
                    box.center = Vector3.up;
                    box.size = new Vector3(8f, 2f, 8f);
                }
            }
            return;
        }
        
        BoxCollider[] boxes = obj.GetComponentsInChildren<BoxCollider>(true);
        foreach (var box in boxes)
        {
            Vector3 scale = box.transform.lossyScale;
            if (scale.x < 0 || scale.y < 0 || scale.z < 0)
            {
                GameObject targetChild = box.gameObject;
                DestroyImmediate(box);
                targetChild.AddComponent<MeshCollider>().convex = true;
            }
        }

        if (obj.GetComponentInChildren<Collider>() == null)
        {
            Renderer r = obj.GetComponentInChildren<Renderer>();
            GameObject target = (r != null && r.gameObject != obj) ? r.gameObject : obj;
            Vector3 s = target.transform.lossyScale;

            if (s.x < 0 || s.y < 0 || s.z < 0)
            {
                MeshCollider mc = target.AddComponent<MeshCollider>();
                mc.convex = true;
            }
            else
            {
                target.AddComponent<BoxCollider>();
            }
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

    private static float MedirETrazerTempoDiagnostico(string chave, long inicio)
    {
        float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - inicio) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        if (elapsedMs > 0f)
        {
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo(chave, elapsedMs);
        }

        return elapsedMs;
    }

    public float ObterAlturaTerreno(Vector3 ponto)
    {
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryGetAltura(ponto, TipoSuperficieMapa.Chao, out alturaMarcada))
        {
            return alturaMarcada;
        }

        if (Terrain.activeTerrain != null)
        {
            return Terrain.activeTerrain.SampleHeight(ponto);
        }

        RaycastHit hit;
        if (Physics.Raycast(new Vector3(ponto.x, 500f, ponto.z), Vector3.down, out hit, 1000f))
        {
            if (!hit.collider.name.ToLower().Contains("water"))
            {
                return hit.point.y;
            }
        }

        return 0f;
    }

    public int VerTipoPonto(Vector3 ponto)
    {
        ClassificacaoSuperficieMapa classificacaoMarcada;
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryClassify(ponto, out classificacaoMarcada, out alturaMarcada))
        {
            if (classificacaoMarcada == ClassificacaoSuperficieMapa.Agua || classificacaoMarcada == ClassificacaoSuperficieMapa.Costa)
            {
                return 1;
            }

            if (classificacaoMarcada == ClassificacaoSuperficieMapa.Chao)
            {
                return 2;
            }
        }

        int mascaraGeral = ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(ponto.x, 500f, ponto.z), Vector3.down, 1000f, mascaraGeral);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            string n = hit.collider.name.ToLower();

            if (n.Contains("bip001") || n.Contains("bone") || n.Contains("cube") || n.Contains("finger")) continue;
            if (hit.collider.GetComponentInParent<IdentidadeUnidade>()) continue;

            MarcadorSuperficieMapa marcador = hit.collider.GetComponentInParent<MarcadorSuperficieMapa>();
            if (marcador != null)
            {
                return marcador.TipoSuperficie == TipoSuperficieMapa.Agua ? 1 : 2;
            }

            int l = hit.collider.gameObject.layer;
            if (l == 4 || n.Contains("water") || n.Contains("agua") || n.Contains("ocean") || n.Contains("mar") || n.Contains("sea"))
            {
                return 1;
            }

            if (hit.point.y <= alturaDoMar + 1.0f)
            {
                return 1;
            }

            return 2;
        }

        if (Terrain.activeTerrain != null)
        {
            if (Terrain.activeTerrain.SampleHeight(ponto) <= alturaDoMar + 1.0f)
            {
                return 1;
            }
            return 2;
        }

        return 0;
    }

    bool IsMouseOverUI()
    {
        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return false;

        if (pointerEventDataUI == null || eventSystemUI != eventSystem)
        {
            pointerEventDataUI = new UnityEngine.EventSystems.PointerEventData(eventSystem);
            eventSystemUI = eventSystem;
        }

        pointerEventDataUI.Reset();
        pointerEventDataUI.position = Input.mousePosition;
        bufferRaycastUI.Clear();
        eventSystem.RaycastAll(pointerEventDataUI, bufferRaycastUI);

        foreach (UnityEngine.EventSystems.RaycastResult result in bufferRaycastUI)
        {
            if (result.gameObject == null || !result.gameObject.activeInHierarchy)
            {
                continue;
            }

            Canvas c = result.gameObject.GetComponentInParent<Canvas>();
            if (c == null || c.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (!UIEstaVisivelEInterativa(result.gameObject))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    static bool UIEstaVisivelEInterativa(GameObject uiObject)
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
            if (group == null) continue;
            if (!group.blocksRaycasts || group.alpha <= 0.05f)
            {
                return false;
            }
        }

        return UIObjetoBloqueiaCliqueMundo(uiObject);
    }

    static bool UIObjetoBloqueiaCliqueMundo(GameObject uiObject)
    {
        if (uiObject == null)
        {
            return false;
        }

        if (uiObject.GetComponentInParent<Selectable>() != null
            || uiObject.GetComponentInParent<ScrollRect>() != null
            || uiObject.GetComponentInParent<UnityEngine.EventSystems.EventTrigger>() != null)
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
                || nome.Contains("Modal"))
            {
                return true;
            }

            atual = atual.parent;
        }

        return false;
    }

    void OnGUI()
    {
        if (modoConstrucao && previewLocalInvalido && fantasmaUnico != null && !string.IsNullOrEmpty(motivoInvalido))
        {
            GUIStyle stylePopUp = new GUIStyle(GUI.skin.box);
            stylePopUp.fontSize = 18;
            stylePopUp.normal.textColor = new Color(1f, 0.3f, 0.3f);
            stylePopUp.fontStyle = FontStyle.Bold;
            stylePopUp.alignment = TextAnchor.MiddleCenter;
            stylePopUp.wordWrap = true;

            float largura = 450f;
            float altura = 80f;
            Rect popupRect = new Rect((Screen.width - largura) / 2f, Screen.height - 180f, largura, altura);
            GUI.Box(popupRect, motivoInvalido, stylePopUp);
        }
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    bool PodeReutilizarPreview(Vector3 pontoMouse)
    {
        if (!possuiPreviewSnapshot || prefabSelecionado == null)
        {
            return false;
        }

        if (previewSnapshot.prefabId != prefabSelecionado.GetInstanceID())
        {
            return false;
        }

        if (Application.isPlaying && (Time.unscaledTime - previewSnapshot.timestamp) > JanelaCachePreviewSegundos)
        {
            return false;
        }

        if (Mathf.Abs(previewSnapshot.rotationKey - ObterChaveRotacaoPreview()) > 0.01f)
        {
            return false;
        }

        return previewSnapshot.mouseCell == QuantizarPontoPreview(pontoMouse);
    }

    void RestaurarPreviewSnapshot()
    {
        usarPosicaoPreviewNaval = previewSnapshot.usesNavalPosition;
        usarRotacaoPreviewNaval = previewSnapshot.usesNavalRotation;
        previewUsaColocacaoNavalManual = previewSnapshot.usesManualPlacement;
        previewLocalInvalido = !previewSnapshot.isValid;
        motivoInvalido = previewSnapshot.reason ?? string.Empty;
        posicaoPreviewNaval = previewSnapshot.pose.Position;
        rotacaoPreviewNaval = previewSnapshot.pose.Rotation;
    }

    void SalvarPreviewSnapshot(Vector3 pontoMouse)
    {
        previewSnapshot = new BuildPreviewSnapshot
        {
            mouseCell = QuantizarPontoPreview(pontoMouse),
            timestamp = Application.isPlaying ? Time.unscaledTime : 0f,
            worldPoint = pontoMouse,
            seaLevel = usarPosicaoPreviewNaval ? posicaoPreviewNaval.y : pontoMouse.y,
            pose = new NavalPlacementResolver.StructurePose
            {
                Position = usarPosicaoPreviewNaval ? posicaoPreviewNaval : pontoMouse,
                Rotation = usarRotacaoPreviewNaval ? rotacaoPreviewNaval : (fantasmaUnico != null ? fantasmaUnico.transform.rotation : prefabSelecionado.transform.rotation),
                SeaLevel = usarPosicaoPreviewNaval ? posicaoPreviewNaval.y : pontoMouse.y,
                Reason = motivoInvalido
            },
            isValid = !previewLocalInvalido,
            reason = motivoInvalido,
            usesNavalPosition = usarPosicaoPreviewNaval,
            usesNavalRotation = usarRotacaoPreviewNaval,
            usesManualPlacement = previewUsaColocacaoNavalManual,
            prefabId = prefabSelecionado != null ? prefabSelecionado.GetInstanceID() : 0,
            rotationKey = ObterChaveRotacaoPreview()
        };
        possuiPreviewSnapshot = true;
    }

    void InvalidarPreviewSnapshot()
    {
        possuiPreviewSnapshot = false;
        previewSnapshot = default(BuildPreviewSnapshot);
    }

    Vector3 QuantizarPontoPreview(Vector3 pontoMouse)
    {
        float passo = Mathf.Max(0.1f, DistanciaCachePreview);
        return new Vector3(
            Mathf.Round(pontoMouse.x / passo) * passo,
            0f,
            Mathf.Round(pontoMouse.z / passo) * passo);
    }

    float ObterChaveRotacaoPreview()
    {
        float angulo = rotacaoExtra;
        if (usarRotacaoPreviewNaval)
        {
            angulo += rotacaoPreviewNaval.eulerAngles.y;
        }
        else if (fantasmaUnico != null)
        {
            angulo += fantasmaUnico.transform.eulerAngles.y;
        }
        else if (prefabSelecionado != null)
        {
            angulo += prefabSelecionado.transform.eulerAngles.y;
        }

        return Mathf.Repeat(Mathf.Round(angulo * 10f) * 0.1f, 360f);
    }

    GerenteDeTerritorio ObterGerenteTerritorio(bool criarSeAusente)
    {
        if (gerenteTerritorioCache != null)
        {
            return gerenteTerritorioCache;
        }

        if (Time.unscaledTime < proximaBuscaGerenteTerritorio)
        {
            return GerenteDeTerritorio.Instancia;
        }

        gerenteTerritorioCache = GerenteDeTerritorio.Instancia != null
            ? GerenteDeTerritorio.Instancia
            : Object.FindFirstObjectByType<GerenteDeTerritorio>();

        if (gerenteTerritorioCache == null && criarSeAusente)
        {
            GameObject gerObj = new GameObject("GerenteDeTerritorio_Sistema");
            gerenteTerritorioCache = gerObj.AddComponent<GerenteDeTerritorio>();
        }

        proximaBuscaGerenteTerritorio = Time.unscaledTime + 1f;
        return gerenteTerritorioCache;
    }
}
