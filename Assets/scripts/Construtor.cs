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
    private Vector3? ultimoPontoConstruidoRua = null;
    private Collider ultimoColliderConstruidoRua = null;
    private readonly List<GameObject> fantasmasRua = new List<GameObject>();
    private bool definindoRua = false;

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
    private readonly List<GerenciadorAeroporto> bufferAeroportos = new List<GerenciadorAeroporto>(32);
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
                new InteractionPolicy { bloqueiaSelecao = true, bloqueiaOrdemMundo = true, bloqueiaRotacaoCamera = true, consomeLMB = true, consomeRMB = true },
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
            if (definindoRua)
            {
                definindoRua = false;
                ultimoPontoConstruidoRua = null;
                ultimoColliderConstruidoRua = null;
                foreach (var f in fantasmasRua) if (f != null) Destroy(f);
                fantasmasRua.Clear();
                InvalidarPreviewSnapshot();
            }
            else
            {
                CancelarConstrucao(false);
            }
            return;
        }

        if (IsMouseOverUI())
        {
            if (fantasmaUnico != null) fantasmaUnico.SetActive(false);
            foreach (var f in fantasmasMuro) if (f != null) f.SetActive(false);
            foreach (var f in fantasmasRua) if (f != null) f.SetActive(false);
            return;
        }

        if (fantasmaUnico != null) fantasmaUnico.SetActive(!definindoRua);

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
                if (ehPlataforma) pontoMouse.y = 30.0f;
                if (!ehEstruturaCosteira && ExisteTerraAltaNoPontoMaritimo(pontoMouse, mascaraGeral)) acertouChao = false;
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

        if (precisaRecalcularPreview)
        {
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

            if (!previewLocalInvalido) ValidarTerritorio(pontoMouse, ehPlataforma, ehEstruturaCosteira);

            SalvarPreviewSnapshot(pontoMouse);
        }
        else
        {
            RestaurarPreviewSnapshot();
            if (usarPosicaoPreviewNaval) pontoMouse = posicaoPreviewNaval;
        }

        if (ehMuro) GerenciarConstrucaoMuro(pontoMouse);
        else GerenciarConstrucaoNormal(pontoMouse);
    }

    bool TryObterPontoNoPlanoDoMar(Ray raio, out Vector3 ponto)
    {
        ponto = Vector3.zero;
        float nivelDoMar = NavalPlacementResolver.ResolveSeaLevel();
        float denominador = Vector3.Dot(raio.direction, Vector3.up);
        if (Mathf.Abs(denominador) < 0.0001f) return false;

        float distancia = (nivelDoMar - raio.origin.y) / denominador;
        if (distancia < 0f) return false;

        ponto = raio.origin + (raio.direction * distancia);
        ponto.y = nivelDoMar;
        return true;
    }

    bool ExisteTerraAltaNoPontoMaritimo(Vector3 pontoNoMar, int mascaraGeral)
    {
        float nivelDoMar = NavalPlacementResolver.ResolveSeaLevel();
        RaycastHit infoTerreno;
        Vector3 origemCeu = new Vector3(pontoNoMar.x, nivelDoMar + 500f, pontoNoMar.z);

        if (!Physics.Raycast(origemCeu, Vector3.down, out infoTerreno, 1000f, mascaraGeral)) return false;
        if (infoTerreno.collider == null) return false;

        string nomeCollider = infoTerreno.collider.name.ToLower();
        bool bateuEmAguaOuNaval = nomeCollider.Contains("agua") || nomeCollider.Contains("water") || infoTerreno.collider.gameObject.layer == 4;

        if (bateuEmAguaOuNaval) return false;
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
        System.Array.Sort(bufferHitsConstrucao, 0, quantidadeHits, ComparadorHitsConstrucao);

        for (int i = 0; i < quantidadeHits; i++)
        {
            RaycastHit h = bufferHitsConstrucao[i];
            if (h.collider == null) continue;
            string n = h.collider.name.ToLower();

            if (n.Contains("bip001") || n.Contains("bone") || n.Contains("finger") || n.Contains("cube")) continue;
            if (h.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) continue;
            if (h.collider.GetComponentInParent<ControleUnidade>() != null) continue;

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
        if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
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

        bool ehPrefeitura = prefabSelecionado.GetComponent<ComplexoGovernamental>() != null || prefabSelecionado.name.ToLower().Contains("prefeitura") || prefabSelecionado.name.ToLower().Contains("complexo");
        bool ehBandeira = prefabSelecionado.name.ToLower().Contains("bandeira") || prefabSelecionado.name.ToLower().Contains("flag") || prefabSelecionado.GetComponent<MarcadorTerritorio>() != null;

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

        if (ehBandeira && donoDoPonto != 0 && donoDoPonto != meuTime)
        {
            previewLocalInvalido = true;
            motivoInvalido = "❌ JURISDIÇÃO INIMIGA:\nA soberania desta área já pertence a outra Nação.";
            return;
        }

        previewLocalInvalido = false;
        motivoInvalido = "";
    }

    void GerenciarConstrucaoNormal(Vector3 ponto)
    {
        RuaConectora ruaPrefab = prefabSelecionado != null ? prefabSelecionado.GetComponent<RuaConectora>() : null;
        if (ruaPrefab != null && definindoRua)
        {
            GerenciarConstrucaoRuaContinua(ponto, ruaPrefab);
            return;
        }

        if (fantasmaUnico == null)
        {
            if (prefabSelecionado == null) return;

            GameObject containerSeguro = new GameObject("ContainerSeguro_Construtor");
            containerSeguro.SetActive(false);
            CriandoPreviewConstrucao = true;
            try { fantasmaUnico = Instantiate(prefabSelecionado, ponto, Quaternion.identity, containerSeguro.transform); }
            finally { CriandoPreviewConstrucao = false; }
            
            if (fantasmaUnico.transform.localScale.sqrMagnitude < 0.0001f) fantasmaUnico.transform.localScale = Vector3.one;

            Renderer[] todosRenderers = fantasmaUnico.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in todosRenderers)
            {
                if (r.transform.localScale.sqrMagnitude < 0.0001f) r.transform.localScale = Vector3.one;
                if (!r.gameObject.activeSelf) r.gameObject.SetActive(true);
            }

            RemoverColisoresEScripts(fantasmaUnico);
            SetLayerRecursively(fantasmaUnico, LayerMask.NameToLayer("Default"));
            fantasmaUnico.transform.SetParent(null);
            Destroy(containerSeguro);
            fantasmaUnico.SetActive(true);
            CacheMateriaisFantasma();
            corFantasmaAplicada = false;
        }

        Vector3 posFinalPreview = usarPosicaoPreviewNaval ? posicaoPreviewNaval : ponto;
        
        bool fezSnapImovel = false;
        bool fezSnapRua = false;
        Collider snapCollider = null;
        
        Imovel imovelPrefab = prefabSelecionado.GetComponent<Imovel>();
        
        if (imovelPrefab != null && !usarPosicaoPreviewNaval)
        {
            fantasmaUnico.transform.rotation = imovelPrefab.transform.rotation;

            // Raio maior para garantir snap dos dois lados da estrada
            float raioBuscaRua = Mathf.Max(imovelPrefab.distanciaFronteiraRua + 50f, 50f);
            RuaConectora ruaProxima = EncontrarRuaProxima(posFinalPreview, raioBuscaRua);
            if (ruaProxima != null)
            {
                Vector3 pInicio = GetPontoInicio(ruaProxima);
                Vector3 pFim = GetPontoFim(ruaProxima);
                
                Vector3 pInicioPlano = pInicio; pInicioPlano.y = 0f;
                Vector3 pFimPlano = pFim; pFimPlano.y = 0f;

                // Correção de Parallax: intercepta o raio da câmera com a altura real da rua
                Vector3 posPlana = posFinalPreview;
                Ray rayMouse = Camera.main.ScreenPointToRay(Input.mousePosition);
                UnityEngine.Plane planoRua = new UnityEngine.Plane(Vector3.up, new Vector3(0, pInicio.y, 0));
                if (planoRua.Raycast(rayMouse, out float enter)) {
                    posPlana = rayMouse.GetPoint(enter);
                }
                posPlana.y = 0f;
                
                Vector3 ruaDirPlana = (pFimPlano - pInicioPlano).normalized;
                if (ruaDirPlana == Vector3.zero) ruaDirPlana = Vector3.forward;

                // Projeta o mouse na linha da rua sem clamp, para detectar o lado correto
                // mesmo quando o mouse está além dos extremos da rua
                Vector3 v = posPlana - pInicioPlano;
                float d = Vector3.Dot(v, ruaDirPlana);
                // Para o posicionamento da casa, clampamos dentro do comprimento da rua
                float dClamp = Mathf.Clamp(d, 0, Vector3.Distance(pInicioPlano, pFimPlano));
                
                Vector3 pontoNaRuaPlano = pInicioPlano + ruaDirPlana * dClamp;
                Vector3 ruaRight = Vector3.Cross(Vector3.up, ruaDirPlana).normalized;
                
                // Usa o vetor do mouse sem clamp para detectar o lado correto (esquerdo ou direito da rua)
                Vector3 toMouseDoProj = posPlana - pontoNaRuaPlano;
                float dotRight = Vector3.Dot(toMouseDoProj, ruaRight);
                // Se o mouse estiver exatamente sobre a rua (dotRight muito próximo de zero),
                // usa a posição do mouse não-clampada em relação ao ponto projetado
                if (Mathf.Abs(dotRight) < 0.1f)
                {
                    dotRight = Vector3.Dot(toMouseDoProj, ruaRight);
                }
                
                Vector3 direcaoRuaParaCasa = (dotRight >= 0) ? ruaRight : -ruaRight;
                Vector3 direcaoFrenteCasa = -direcaoRuaParaCasa;
                
                // Força o prédio a ficar perfeitamente alinhado e RETO (Euler X e Z = 0)
                float alvoYaw = Quaternion.LookRotation(direcaoFrenteCasa, Vector3.up).eulerAngles.y;
                fantasmaUnico.transform.rotation = Quaternion.Euler(0, alvoYaw, 0);

                // Recupera a altura Y real da rua para aplicar na casa
                Vector3 pontoRealNaRua = pInicio + (pFim - pInicio).normalized * dClamp;
                
                posFinalPreview = pontoRealNaRua + direcaoRuaParaCasa * ((ruaProxima.largura / 2f) + imovelPrefab.distanciaFronteiraRua);
                posFinalPreview.y = pontoRealNaRua.y; 
                
                fezSnapImovel = true;
                snapCollider = ruaProxima.GetComponent<Collider>();
            }
            else
            {
                float raioBusca = imovelPrefab.distanciaConexao * 2f;
                int totalCols = Physics.OverlapSphereNonAlloc(posFinalPreview, raioBusca, bufferColisoresSnap, ~0, QueryTriggerInteraction.Ignore);
                Imovel imovelProximo = null;
                float menorDistancia = float.MaxValue;

                for (int i = 0; i < totalCols; i++)
                {
                    Collider col = bufferColisoresSnap[i];
                    if (col == null) continue;
                    Imovel viz = col.GetComponentInParent<Imovel>();
                    if (viz != null && viz.gameObject != fantasmaUnico)
                    {
                        float dist = Vector3.Distance(posFinalPreview, viz.transform.position);
                        if (dist < menorDistancia)
                        {
                            menorDistancia = dist;
                            imovelProximo = viz;
                        }
                    }
                }

                if (imovelProximo != null)
                {
                    Vector3 pEsqViz = GetPontoEsquerdo(imovelProximo);
                    Vector3 pDirViz = GetPontoDireito(imovelProximo);
                    
                    Vector3 melhorPontoViz = (Vector3.Distance(posFinalPreview, pEsqViz) < Vector3.Distance(posFinalPreview, pDirViz)) ? pEsqViz : pDirViz;
                    
                    float alvoYaw = imovelProximo.transform.eulerAngles.y;
                    fantasmaUnico.transform.rotation = Quaternion.Euler(0, alvoYaw, 0);
                    
                    Imovel imFantasma = fantasmaUnico.GetComponent<Imovel>();
                    Vector3 pEsqFantasma = GetPontoEsquerdo(imFantasma);
                    Vector3 pDirFantasma = GetPontoDireito(imFantasma);
                    
                    Vector3 melhorPontoFantasma = (Vector3.Distance(melhorPontoViz, pEsqFantasma) < Vector3.Distance(melhorPontoViz, pDirFantasma)) ? pEsqFantasma : pDirFantasma;
                    
                    Vector3 offsetLocal = melhorPontoFantasma - fantasmaUnico.transform.position;
                    posFinalPreview = melhorPontoViz - offsetLocal;
                    
                    fezSnapImovel = true;
                    snapCollider = imovelProximo.GetComponent<Collider>();
                }
            }
        }
        else
        {
            if (ruaPrefab != null)
            {
                fantasmaUnico.transform.rotation = ruaPrefab.transform.rotation;

                if (ultimoPontoConstruidoRua.HasValue)
                {
                    Vector3 anchor = ultimoPontoConstruidoRua.Value;
                    Vector3 dir = posFinalPreview - anchor;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f) dir.Normalize(); else dir = Vector3.forward;
                    
                    float alvoYaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
                    fantasmaUnico.transform.rotation = Quaternion.Euler(0, alvoYaw, 0);
                    
                    posFinalPreview = anchor + dir * (ruaPrefab.comprimento / 2f);
                    
                    fezSnapRua = true;
                    snapCollider = ultimoColliderConstruidoRua;
                }
                else
                {
                    RuaConectora ruaProximaOutra = EncontrarRuaProxima(posFinalPreview, ruaPrefab.distanciaConexao * 2.5f);
                    if (ruaProximaOutra != null && ruaProximaOutra.gameObject != fantasmaUnico)
                    {
                        Vector3 pInicio = GetPontoInicio(ruaProximaOutra);
                        Vector3 pFim = GetPontoFim(ruaProximaOutra);
                        
                        float distIni = Vector3.Distance(posFinalPreview, pInicio);
                        float distFim = Vector3.Distance(posFinalPreview, pFim);
                        
                        Vector3 snapPoint = (distIni < distFim) ? pInicio : pFim;
                        Vector3 originPoint = (distIni < distFim) ? pFim : pInicio;
                        
                        Vector3 snapDir = (snapPoint - originPoint);
                        snapDir.y = 0f; 
                        if (snapDir.sqrMagnitude > 0.001f) snapDir.Normalize(); else snapDir = Vector3.forward;
                        
                        float alvoYaw = Quaternion.LookRotation(snapDir, Vector3.up).eulerAngles.y;
                        fantasmaUnico.transform.rotation = Quaternion.Euler(0, alvoYaw, 0);
                        
                        posFinalPreview = snapPoint + snapDir * (ruaPrefab.comprimento / 2f);
                        posFinalPreview.y = snapPoint.y;
                        
                        fezSnapRua = true;
                        snapCollider = ruaProximaOutra.GetComponent<Collider>();
                    }
                }
            }
        }

        if (ruaPrefab != null && !fezSnapRua)
        {
            posFinalPreview.y += 0.05f; // Evita que a estrada fique enterrada sob o terreno (Z-fighting)
        }

        fantasmaUnico.transform.position = posFinalPreview;
        if (usarRotacaoPreviewNaval) fantasmaUnico.transform.rotation = rotacaoPreviewNaval;

        if (!previewLocalInvalido)
        {
            if (VerificarSobreposicao(posFinalPreview, fantasmaUnico.transform.rotation, prefabSelecionado, 0.5f, snapCollider))
            {
                previewLocalInvalido = true;
                motivoInvalido = LocalizationManager.T("build.overlap", "❌ SOBREPOSIÇÃO DE CONSTRUÇÃO:\nNão é permitido sobrepor prédios ou ruas.");
            }
        }

        AplicarCorNoFantasma(fantasmaUnico, previewLocalInvalido);

        if (Input.GetKeyDown(KeyCode.R) && !usarRotacaoPreviewNaval && !fezSnapImovel && !fezSnapRua)
        {
            fantasmaUnico.transform.Rotate(0f, 90f, 0f);
        }

        if (!Input.GetMouseButtonDown(0)) return;
        if (previewLocalInvalido)
        {
            Debug.LogWarning($"⚠️ [Construtor] Abortando: {motivoInvalido}");
            return;
        }

        RuaConectora ruaPrefabTemp = prefabSelecionado != null ? prefabSelecionado.GetComponent<RuaConectora>() : null;
        if (ruaPrefabTemp != null)
        {
            Vector3 direcaoRua = fantasmaUnico.transform.forward; 
            direcaoRua.y = 0f; direcaoRua.Normalize();
            ultimoPontoConstruidoRua = fantasmaUnico.transform.position + direcaoRua * (ruaPrefabTemp.comprimento / 2f);
            ultimoColliderConstruidoRua = snapCollider;
            definindoRua = true;
            InvalidarPreviewSnapshot();
            if (fantasmaUnico != null) fantasmaUnico.SetActive(false);
            return;
        }

        CommitConstrucaoUnica(ponto, posFinalPreview, fantasmaUnico.transform.rotation, snapCollider);
    }

    void CommitConstrucaoUnica(Vector3 pontoMouse, Vector3 posFinal, Quaternion rotFinal, Collider snapCollider)
    {
        if (EhEstruturaCosteiraPrefab(prefabSelecionado))
        {
            NavalPlacementResolver.StructurePose poseCommit;
            if (NavalPlacementResolver.TryResolveStructurePose(prefabSelecionado, pontoMouse, rotFinal, out poseCommit))
            {
                posFinal = poseCommit.Position;
                rotFinal = poseCommit.Rotation;
            }
        }

        if (!TentarCobrarConstrucao(custoAtual)) return;

        GameObject novo = Instantiate(prefabSelecionado, posFinal, rotFinal);

        Imovel imovelNovo = novo.GetComponent<Imovel>();
        if (imovelNovo != null)
        {
            RuaConectora ruaProxima = EncontrarRuaProxima(posFinal, Mathf.Max(imovelNovo.distanciaFronteiraRua * 2.5f, 20f));
            if (ruaProxima != null)
            {
                Vector3 startRua = GetPontoInicio(ruaProxima);
                Vector3 fimRua = GetPontoFim(ruaProxima);
                Vector3 vetorRua = fimRua - startRua;
                Vector3 dirRua = vetorRua.normalized;
                float proj = Vector3.Dot(posFinal - startRua, dirRua);
                proj = Mathf.Clamp(proj, 0f, vetorRua.magnitude);
                Vector3 pontoProjetadoNaRua = startRua + dirRua * proj;
                
                imovelNovo.AtualizarPavimentacao(pontoProjetadoNaRua);
            }
        }
        else
        {
            RuaConectora ruaNova = novo.GetComponent<RuaConectora>();
            if (ruaNova != null)
            {
                Imovel[] todosImoveis = Object.FindObjectsByType<Imovel>(FindObjectsSortMode.None);
                foreach (var imovel in todosImoveis)
                {
                    float dist = Vector3.Distance(imovel.transform.position, posFinal);
                    if (dist < imovel.distanciaFronteiraRua * 2.5f)
                    {
                        Vector3 startRua = GetPontoInicio(ruaNova);
                        Vector3 fimRua = GetPontoFim(ruaNova);
                        Vector3 vetorRua = fimRua - startRua;
                        Vector3 dirRua = vetorRua.normalized;
                        float proj = Vector3.Dot(imovel.transform.position - startRua, dirRua);
                        proj = Mathf.Clamp(proj, 0f, vetorRua.magnitude);
                        Vector3 pontoProjetadoNaRua = startRua + dirRua * proj;
                        
                        imovel.AtualizarPavimentacao(pontoProjetadoNaRua);
                    }
                }
            }
        }

        if (EhEstruturaCosteiraPrefab(prefabSelecionado))
        {
            IA_ManualPlacementTag manualTag = novo.GetComponent<IA_ManualPlacementTag>();
            if (manualTag == null) manualTag = novo.AddComponent<IA_ManualPlacementTag>();
            manualTag.SourceLabel = previewUsaColocacaoNavalManual ? "Construtor jogador (manual)" : "Construtor jogador";
        }

        ReativarLogicaUnidade(novo);
        EnsureCollider(novo);

        Estaleiro estaleiro = novo.GetComponent<Estaleiro>();
        if (estaleiro != null)
        {
            // Tudo que chega a este metodo veio do Construtor do jogador.
            // Alguns prefabs navais carregam identidade de teste/IA (time 2),
            // o que fazia os cacas tratarem o estaleiro comprado como inimigo
            // e bloqueava a fila naval do jogador. Normalize sempre a posse
            // antes de registrar slots e logistica; a IA usa seus executores
            // proprios e nao passa por este caminho.
            IdentidadeUnidade identidadeEstaleiro = novo.GetComponent<IdentidadeUnidade>();
            if (identidadeEstaleiro == null)
            {
                identidadeEstaleiro = novo.AddComponent<IdentidadeUnidade>();
            }

            identidadeEstaleiro.teamID = 1;
            identidadeEstaleiro.nomeDoPais = "Hegemonia";
            estaleiro.OwnerTeamId = 1;

            estaleiro.AtualizarReferenciasLitoraneas();
            TentarFixarSpawnNaval(estaleiro.gameObject, rotFinal, true);
        }

        PierMarinha pier = novo.GetComponent<PierMarinha>();
        if (pier != null)
        {
            // Mesma normalizacao do estaleiro: o pier comprado pelo jogador
            // nunca pode herdar a identidade de um prefab de IA.
            IdentidadeUnidade identidadePier = novo.GetComponent<IdentidadeUnidade>();
            if (identidadePier == null)
            {
                identidadePier = novo.AddComponent<IdentidadeUnidade>();
            }

            identidadePier.teamID = 1;
            identidadePier.nomeDoPais = "Hegemonia";
            pier.OwnerTeamId = 1;

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

        CancelarConstrucao(false);
    }

    private void GerenciarConstrucaoRuaContinua(Vector3 ponto, RuaConectora ruaPrefab)
    {
        if (!ultimoPontoConstruidoRua.HasValue) return;
        
        Vector3 anchor = ultimoPontoConstruidoRua.Value;
        Vector3 posFinalPreview = ponto;
        
        Collider snapColliderEnd = null;
        RuaConectora ruaProximaOutra = EncontrarRuaProxima(posFinalPreview, ruaPrefab.distanciaConexao * 1.5f);
        if (ruaProximaOutra != null)
        {
            Vector3 pInicio = GetPontoInicio(ruaProximaOutra);
            Vector3 pFim = GetPontoFim(ruaProximaOutra);
            float distIni = Vector3.Distance(posFinalPreview, pInicio);
            float distFim = Vector3.Distance(posFinalPreview, pFim);
            
            if (distIni < distFim && distIni < ruaPrefab.distanciaConexao * 1.5f) { posFinalPreview = pInicio; snapColliderEnd = ruaProximaOutra.GetComponent<Collider>(); }
            else if (distFim < ruaPrefab.distanciaConexao * 1.5f) { posFinalPreview = pFim; snapColliderEnd = ruaProximaOutra.GetComponent<Collider>(); }
        }
        else
        {
            Imovel imovelProx = null;
            Vector3 targetPoint = Vector3.zero;
            float menorDist = float.MaxValue;
            int totalCols = Physics.OverlapSphereNonAlloc(posFinalPreview, ruaPrefab.distanciaConexao * 2.5f, bufferColisoresSnap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < totalCols; i++)
            {
                Collider col = bufferColisoresSnap[i];
                if (col == null) continue;
                Imovel viz = col.GetComponentInParent<Imovel>();
                if (viz != null)
                {
                    Vector3 pFront = GetPontoConexaoRua(viz);
                    Vector3 pBack = GetPontoConexaoRuaTras(viz);
                    float distFront = Vector3.Distance(posFinalPreview, pFront);
                    float distBack = Vector3.Distance(posFinalPreview, pBack);
                    if (distFront < menorDist)
                    {
                        menorDist = distFront;
                        imovelProx = viz;
                        targetPoint = pFront;
                    }
                    if (distBack < menorDist)
                    {
                        menorDist = distBack;
                        imovelProx = viz;
                        targetPoint = pBack;
                    }
                }
            }

            if (imovelProx != null && menorDist < ruaPrefab.distanciaConexao * 1.5f)
            {
                posFinalPreview = targetPoint;
                snapColliderEnd = imovelProx.GetComponent<Collider>();
            }
        }
        
        Vector3 dir = posFinalPreview - anchor;
        dir.y = 0f;
        float dist = dir.magnitude;
        if (dir.sqrMagnitude > 0.001f) dir.Normalize(); else dir = Vector3.forward;
        
        int quantidade = Mathf.Max(1, Mathf.RoundToInt(dist / ruaPrefab.comprimento));
        
        while (fantasmasRua.Count < quantidade)
        {
            GameObject containerSeguro = new GameObject("ContainerSeguro_FantasmasRua");
            containerSeguro.SetActive(false);
            
            GameObject g = Instantiate(prefabSelecionado, containerSeguro.transform);
            RemoverColisoresEScripts(g);
            SetLayerRecursively(g, LayerMask.NameToLayer("Ignore Raycast"));
            g.transform.SetParent(null);
            Destroy(containerSeguro);
            fantasmasRua.Add(g);
        }
        
        float alvoYaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
        Quaternion rotSeg = Quaternion.Euler(0, alvoYaw, 0);
        
        Vector3 step = dir * ruaPrefab.comprimento;
        Vector3 centroBase = anchor + (dir * (ruaPrefab.comprimento / 2f));
        
        for (int i = 0; i < quantidade; i++)
        {
            fantasmasRua[i].SetActive(true);
            fantasmasRua[i].transform.rotation = rotSeg;
            fantasmasRua[i].transform.position = centroBase + (step * i);
        }
        
        for (int i = quantidade; i < fantasmasRua.Count; i++) fantasmasRua[i].SetActive(false);
        
        previewLocalInvalido = false;
        motivoInvalido = "";
        
        for (int i = 0; i < quantidade; i++)
        {
            Vector3 posSeg = fantasmasRua[i].transform.position;
            if (VerificarSobreposicao(posSeg, rotSeg, prefabSelecionado, 0.5f, ultimoColliderConstruidoRua, snapColliderEnd))
            {
                previewLocalInvalido = true;
                motivoInvalido = LocalizationManager.T("build.overlap", "❌ SOBREPOSIÇÃO DE CONSTRUÇÃO:\nNão é permitido sobrepor prédios ou ruas.");
                break;
            }
        }
        
        for (int i = 0; i < quantidade; i++) AplicarCorNoFantasmaGenerico(fantasmasRua[i], previewLocalInvalido);
        
        if (Input.GetMouseButtonDown(0))
        {
            if (previewLocalInvalido)
            {
                Debug.LogWarning($"⚠️ [Construtor] Abortando: {motivoInvalido}");
                return;
            }
            
            int custoTotal = custoAtual * quantidade;
            if (!TentarCobrarConstrucao(custoTotal)) return;
            
            Collider ultimoCol = null;
            
            for (int i = 0; i < quantidade; i++)
            {
                Vector3 posFinal = centroBase + (step * i);
                GameObject novo = Instantiate(prefabSelecionado, posFinal, rotSeg);
                RuaConectora rc = novo.GetComponent<RuaConectora>();
                
                ReativarLogicaUnidade(novo);
                EnsureCollider(novo);
                
                ultimoCol = novo.GetComponent<Collider>();
                
                Imovel[] todosImoveis = Object.FindObjectsByType<Imovel>(FindObjectsSortMode.None);
                foreach (var imovel in todosImoveis)
                {
                    float d = Vector3.Distance(imovel.transform.position, posFinal);
                    if (d < imovel.distanciaFronteiraRua * 2.5f)
                    {
                        Vector3 startRua = GetPontoInicio(rc);
                        Vector3 fimRua = GetPontoFim(rc);
                        Vector3 dirRuaLocal = (fimRua - startRua).normalized;
                        float distTotal = Vector3.Distance(startRua, fimRua);
                        
                        float proj = Vector3.Dot(imovel.transform.position - startRua, dirRuaLocal);
                        proj = Mathf.Clamp(proj, 0f, distTotal);
                        Vector3 pontoProjetadoNaRua = startRua + dirRuaLocal * proj;
                        
                        imovel.AtualizarPavimentacao(pontoProjetadoNaRua);
                    }
                }
                
                Vector3 escalaOriginal = novo.transform.localScale;
                if (escalaOriginal.sqrMagnitude < 0.0001f) { escalaOriginal = Vector3.one; novo.transform.localScale = Vector3.one; }
                novo.transform.localScale = Vector3.zero;
                AnimadorConstrucao anim = novo.AddComponent<AnimadorConstrucao>();
                anim.IniciarAnimacao(escalaOriginal, 1.5f);
            }
            
            ultimoPontoConstruidoRua = anchor + (dir * (quantidade * ruaPrefab.comprimento));
            ultimoColliderConstruidoRua = ultimoCol;
            InvalidarPreviewSnapshot();
            foreach (var f in fantasmasRua) if (f != null) f.SetActive(false);
        }
    }

    bool TentarCobrarConstrucao(int custo)
    {
        if (custo <= 0) return true;

        GerenteDeJogo gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
        if (gerente != null && gerente.TentarGastarDinheiro(custo)) return true;

        if (gerente == null && GerenciadorRecursos.Instancia != null && GerenciadorRecursos.Instancia.TentarGastar(custoDinheiro: custo)) return true;

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

    void TentarFixarSpawnNaval(GameObject estrutura, Quaternion rotacao, bool logar)
    {
        if (estrutura == null) return;

        Transform[] filhos = estrutura.GetComponentsInChildren<Transform>(true);
        Vector3 forward = rotacao * Vector3.forward;
        if (forward.sqrMagnitude < 0.01f) forward = estrutura.transform.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
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

            if (logar) Debug.Log($"[Construtor] Spawn naval forçado em {estrutura.name} -> {t.name} para {corrigido}");
        }
    }

    void CacheMateriaisFantasma()
    {
        materiaisFantasma.Clear();
        if (fantasmaUnico == null) return;

        Renderer[] renders = fantasmaUnico.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renders)
        {
            if (r == null) continue;
            Material[] materiais = r.materials;
            for (int i = 0; i < materiais.Length; i++)
            {
                if (materiais[i] != null) materiaisFantasma.Add(materiais[i]);
            }
        }
    }

    void AplicarCorNoFantasma(GameObject fantasma, bool ehInvalido)
    {
        if (fantasma == null) return;
        if (materiaisFantasma.Count == 0) CacheMateriaisFantasma();
        if (corFantasmaAplicada && corFantasmaInvalidaAplicada == ehInvalido) return;

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
        foreach (var script in scripts) script.enabled = false;
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
        GameObject novoPredio = Instantiate(prefab, posicao, rotacao);
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
        ultimoPontoConstruidoRua = null;
        ultimoColliderConstruidoRua = null;
        definindoRua = false;
        foreach (var f in fantasmasRua) if (f != null) Destroy(f);
        fantasmasRua.Clear();
        InvalidarPreviewSnapshot();
        ObterGerenteTerritorio(true);
        InteractionModeService.Request(
            InteractionOwner.Construction,
            new InteractionPolicy { bloqueiaSelecao = true, bloqueiaOrdemMundo = true, bloqueiaRotacaoCamera = true, consomeLMB = true, consomeRMB = true },
            "Construção ativa");
    }

    public void CancelarConstrucao(bool reembolsar = false)
    {
        if (reembolsar && custoAtual > 0)
        {
            GerenteDeJogo gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
            if (gerente != null) gerente.dinheiroAtual += custoAtual;
            else if (GerenciadorRecursos.Instancia != null) GerenciadorRecursos.Instancia.AdicionarRecursos(addDinheiro: custoAtual);
        }

        modoConstrucao = false;
        definindoMuro = false;
        definindoRua = false;
        prefabSelecionado = null;
        custoAtual = 0;
        rotacaoExtra = 0f;
        usarPosicaoPreviewNaval = false;
        usarRotacaoPreviewNaval = false;
        previewUsaColocacaoNavalManual = false;
        previewLocalInvalido = false;
        motivoInvalido = "";
        ultimoPontoConstruidoRua = null;
        ultimoColliderConstruidoRua = null;
        InvalidarPreviewSnapshot();
        InteractionModeService.Release(InteractionOwner.Construction);

        if (fantasmaUnico != null) Destroy(fantasmaUnico);
        fantasmaUnico = null;
        materiaisFantasma.Clear();
        corFantasmaAplicada = false;

        foreach (var f in fantasmasMuro) if (f != null) Destroy(f);
        fantasmasMuro.Clear();

        foreach (var f in fantasmasRua) if (f != null) Destroy(f);
        fantasmasRua.Clear();
    }

    void SuspenderInteracoesConcorrentes()
    {
        if (ModoDemolicao.TemModoAtivo) ModoDemolicao.Instancia.AlternarModo(false);

        InteractionModeService.Release(InteractionOwner.Patrol);
        InteractionModeService.Release(InteractionOwner.Follow);
        InteractionModeService.Release(InteractionOwner.AirportOrder);
        InteractionModeService.Release(InteractionOwner.CarrierOrder);
        InteractionModeService.Release(InteractionOwner.ManualFire);

        MenuMisseis menuMisseis = Object.FindFirstObjectByType<MenuMisseis>();
        if (menuMisseis != null) menuMisseis.CancelarLancamento();

        bufferAeroportos.Clear();
        RegistroEntidadesJogo.FillAeroportos(bufferAeroportos);
        foreach (GerenciadorAeroporto aeroporto in bufferAeroportos) if (aeroporto != null) aeroporto.CancelarInteracaoPorConstrucao();

        NavioTransporteTropas[] naviosTransporte = Object.FindObjectsByType<NavioTransporteTropas>(FindObjectsSortMode.None);
        foreach (NavioTransporteTropas navio in naviosTransporte) if (navio != null) navio.CancelarInteracaoPorConstrucao();
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
        foreach (var n in navs) Destroy(n);

        MonoBehaviour[] scripts = obj.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var s in scripts)
        {
            if (s == null || s == this) continue;
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
            if (result.gameObject == null || !result.gameObject.activeInHierarchy) continue;
            Canvas c = result.gameObject.GetComponentInParent<Canvas>();
            if (c == null || c.renderMode == RenderMode.WorldSpace) continue;
            if (!UIEstaVisivelEInterativa(result.gameObject)) continue;
            return true;
        }
        return false;
    }

    static bool UIEstaVisivelEInterativa(GameObject uiObject)
    {
        if (uiObject == null || !uiObject.activeInHierarchy) return false;
        Graphic graphic = uiObject.GetComponent<Graphic>();
        if (graphic != null && !graphic.raycastTarget) return false;

        CanvasGroup[] groups = uiObject.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            CanvasGroup group = groups[i];
            if (group == null) continue;
            if (!group.blocksRaycasts || group.alpha <= 0.05f) return false;
        }
        return UIObjetoBloqueiaCliqueMundo(uiObject);
    }

    static bool UIObjetoBloqueiaCliqueMundo(GameObject uiObject)
    {
        if (uiObject == null) return false;
        if (uiObject.GetComponentInParent<Selectable>() != null || uiObject.GetComponentInParent<ScrollRect>() != null || uiObject.GetComponentInParent<UnityEngine.EventSystems.EventTrigger>() != null) return true;

        Transform atual = uiObject.transform;
        while (atual != null)
        {
            string nome = atual.name;
            if (nome.Contains("Painel_Construcao") || nome.Contains("Painel_Governo") || nome.Contains("Menu") || nome.Contains("Popup") || nome.Contains("Modal")) return true;
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
        if (!possuiPreviewSnapshot || prefabSelecionado == null) return false;
        if (previewSnapshot.prefabId != prefabSelecionado.GetInstanceID()) return false;
        if (Application.isPlaying && (Time.unscaledTime - previewSnapshot.timestamp) > JanelaCachePreviewSegundos) return false;
        if (Mathf.Abs(previewSnapshot.rotationKey - ObterChaveRotacaoPreview()) > 0.01f) return false;
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
        return new Vector3(Mathf.Round(pontoMouse.x / passo) * passo, 0f, Mathf.Round(pontoMouse.z / passo) * passo);
    }

    float ObterChaveRotacaoPreview()
    {
        float angulo = rotacaoExtra;
        if (usarRotacaoPreviewNaval) angulo += rotacaoPreviewNaval.eulerAngles.y;
        else if (fantasmaUnico != null) angulo += fantasmaUnico.transform.eulerAngles.y;
        else if (prefabSelecionado != null) angulo += prefabSelecionado.transform.eulerAngles.y;
        return Mathf.Repeat(Mathf.Round(angulo * 10f) * 0.1f, 360f);
    }

    GerenteDeTerritorio ObterGerenteTerritorio(bool criarSeAusente)
    {
        if (gerenteTerritorioCache != null) return gerenteTerritorioCache;
        if (Time.unscaledTime < proximaBuscaGerenteTerritorio) return GerenteDeTerritorio.Instancia;

        gerenteTerritorioCache = GerenteDeTerritorio.Instancia != null ? GerenteDeTerritorio.Instancia : Object.FindFirstObjectByType<GerenteDeTerritorio>();
        if (gerenteTerritorioCache == null && criarSeAusente)
        {
            GameObject gerObj = new GameObject("GerenteDeTerritorio_Sistema");
            gerenteTerritorioCache = gerObj.AddComponent<GerenteDeTerritorio>();
        }

        proximaBuscaGerenteTerritorio = Time.unscaledTime + 1f;
        return gerenteTerritorioCache;
    }

    private RuaConectora EncontrarRuaProxima(Vector3 posicao, float raioBusca)
    {
        int totalCols = Physics.OverlapSphereNonAlloc(posicao, raioBusca, bufferColisoresSnap, ~0, QueryTriggerInteraction.Ignore);
        RuaConectora melhorRua = null;
        float menorDist = float.MaxValue;
        for (int i = 0; i < totalCols; i++)
        {
            Collider col = bufferColisoresSnap[i];
            if (col == null) continue;
            RuaConectora rua = col.GetComponentInParent<RuaConectora>();
            if (rua != null)
            {
                float dist = Vector3.Distance(posicao, rua.transform.position);
                if (dist < menorDist)
                {
                    menorDist = dist;
                    melhorRua = rua;
                }
            }
        }
        return melhorRua;
    }

    private bool VerificarSobreposicao(Vector3 posicao, Quaternion rotacao, GameObject prefab, float margemSeguranca = 0.5f, Collider ignorarCollider = null, Collider ignorarCollider2 = null)
    {
        Bounds boundsBase = new Bounds(prefab.transform.position, Vector3.zero);
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            boundsBase = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) boundsBase.Encapsulate(renderers[i].bounds);
        }
        
        // Reduz levemente a area de checagem para permitir encaixe por borda no grid.
        float fatorApertura = Mathf.Clamp01(1f - (margemSeguranca * 0.1f));
        Vector3 metadeTamanho = boundsBase.extents * fatorApertura;
        if (metadeTamanho.x < 0.2f) metadeTamanho.x = 0.2f;
        if (metadeTamanho.y < 0.2f) metadeTamanho.y = 0.2f;
        if (metadeTamanho.z < 0.2f) metadeTamanho.z = 0.2f;

        Collider[] cols = Physics.OverlapBox(posicao, metadeTamanho, rotacao, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            if (col == null) continue;
            
            if (ignorarCollider != null && (col == ignorarCollider || col.transform.IsChildOf(ignorarCollider.transform) || ignorarCollider.transform.IsChildOf(col.transform))) continue;
            if (ignorarCollider2 != null && (col == ignorarCollider2 || col.transform.IsChildOf(ignorarCollider2.transform) || ignorarCollider2.transform.IsChildOf(col.transform))) continue;

            string nomeCol = col.name.ToLower();
            if (nomeCol.Contains("terreno") || nomeCol.Contains("terrain") || nomeCol.Contains("agua") || nomeCol.Contains("water") || col.gameObject.layer == 4)
            {
                continue;
            }
            
            if (col.GetComponentInParent<Imovel>() != null || 
                col.GetComponentInParent<RuaConectora>() != null ||
                col.GetComponentInParent<Edificio>() != null ||
                nomeCol.Contains("complexo") || 
                nomeCol.Contains("quartel"))
            {
                return true;
            }
        }
        return false;
    }

    private void AplicarCorNoFantasmaGenerico(GameObject obj, bool ehInvalido)
    {
        if (obj == null) return;
        Color cor = ehInvalido ? new Color(1f, 0.2f, 0.2f, 0.6f) : new Color(0.2f, 1f, 0.2f, 0.6f);
        Renderer[] renders = obj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renders)
        {
            if (r == null) continue;
            Material[] materiais = r.materials;
            foreach (Material mat in materiais)
            {
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
        }
    }

    // -------------------------------------------------------------------------
    // Helpers para evitar erros de versão nos scripts Imovel e RuaConectora
    // Faz a matemática internamente sem exigir métodos atualizados nestas classes
    // -------------------------------------------------------------------------
    private Vector3 GetPontoInicio(RuaConectora rua) {
        return rua.transform.position - rua.transform.forward * (rua.comprimento / 2f);
    }
    
    private Vector3 GetPontoFim(RuaConectora rua) {
        return rua.transform.position + rua.transform.forward * (rua.comprimento / 2f);
    }
    
    private Vector3 GetPontoEsquerdo(Imovel imovel) {
        return imovel.transform.position - imovel.transform.right * imovel.distanciaConexao;
    }
    
    private Vector3 GetPontoDireito(Imovel imovel) {
        return imovel.transform.position + imovel.transform.right * imovel.distanciaConexao;
    }
    
    private Vector3 GetPontoConexaoRua(Imovel imovel) {
        return imovel.transform.position - imovel.transform.forward * imovel.distanciaFronteiraRua;
    }
    
    private Vector3 GetPontoConexaoRuaTras(Imovel imovel) {
        // Tenta pegar a variável "traseira" se você possuir um Imovel mais atualizado, caso contrário usa a frente.
        float dist = imovel.distanciaFronteiraRua;
        var field = imovel.GetType().GetField("distanciaFronteiraRuaTras");
        if (field != null) {
            dist = (float)field.GetValue(imovel);
        }
        return imovel.transform.position + imovel.transform.forward * dist;
    }
}
