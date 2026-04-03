using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using Hegemonia.AI.BrainMaster;

public class Construtor : MonoBehaviour
{
    public static Construtor Instancia { get; private set; }
    public static bool EmModoConstrucaoAtivo => Instancia != null && Instancia.modoConstrucao && Instancia.prefabSelecionado != null;

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

        if (Input.GetMouseButtonDown(1))
        {
            CancelarConstrucao(true);
            return;
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
            return;
        }

        bool ehMuro = prefabSelecionado.name.Contains("Muro") || prefabSelecionado.name.Contains("Fence");

        if (ehEstruturaCosteira)
        {
            LiberarPreviewCosteiroSemRestricao(pontoMouse);
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

        RaycastHit[] hits = Physics.RaycastAll(raio, 2000f, mascaraGeral);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
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

    void ValidarTerritorio(Vector3 ponto, bool ehPlataforma, bool ehEstruturaCosteira)
    {
        if (GerenteDeTerritorio.Instancia == null)
        {
            GameObject gerObj = new GameObject("GerenteDeTerritorio_Sistema");
            gerObj.AddComponent<GerenteDeTerritorio>();
        }

        int donoDoPonto = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(ponto);
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

            if (!GerenteDeTerritorio.Instancia.PodeConstruirPrefeitura(ponto))
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
            GameObject containerSeguro = new GameObject("ContainerSeguro_Construtor");
            containerSeguro.SetActive(false);

            fantasmaUnico = Instantiate(prefabSelecionado, ponto, Quaternion.identity, containerSeguro.transform);
            RemoverColisoresEScripts(fantasmaUnico);
            SetLayerRecursively(fantasmaUnico, LayerMask.NameToLayer("Ignore Raycast"));
            fantasmaUnico.transform.SetParent(null);
            Destroy(containerSeguro);
            fantasmaUnico.SetActive(true);
        }

        Vector3 posFinalPreview = usarPosicaoPreviewNaval ? posicaoPreviewNaval : ponto;
        fantasmaUnico.transform.position = posFinalPreview;

        if (usarRotacaoPreviewNaval)
        {
            fantasmaUnico.transform.rotation = rotacaoPreviewNaval;
        }

        AplicarCorNoFantasma(fantasmaUnico, previewLocalInvalido);

        if (Input.GetKeyDown(KeyCode.R) && !usarRotacaoPreviewNaval)
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

        Vector3 posFinal = fantasmaUnico.transform.position;
        Quaternion rotFinal = fantasmaUnico.transform.rotation;
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
        novo.transform.localScale = Vector3.zero;
        AnimadorConstrucao anim = novo.AddComponent<AnimadorConstrucao>();
        anim.IniciarAnimacao(escalaOriginal, 1.5f);

        CancelarConstrucao(false);
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

    void AplicarCorNoFantasma(GameObject fantasma, bool ehInvalido)
    {
        Renderer[] renders = fantasma.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renders)
        {
            foreach (Material mat in r.materials)
            {
                mat.color = ehInvalido ? new Color(1f, 0.2f, 0.2f, 0.6f) : new Color(0.2f, 1f, 0.2f, 0.6f);
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

        unidade.layer = LayerMask.NameToLayer("Default");
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

        if (!Application.isEditor)
        {
            Debug.Log($"[Construtor IA] Construiu {prefab.name} em {posicao}");
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

            CancelarConstrucao(true);
        }

        SuspenderInteracoesConcorrentes();
        prefabSelecionado = prefab;
        custoAtual = custo;
        categoriaAtual = categoria;
        modoConstrucao = true;
        recemSelecionado = true;

        Debug.Log($"[Construtor] MODO CONSTRUÇÃO ATIVADO para: {prefab.name}. Custo: {custo}. Categoria: {categoria}");
    }

    public void CancelarConstrucao(bool reembolsar = true)
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

        if (fantasmaUnico != null)
        {
            Destroy(fantasmaUnico);
        }
        fantasmaUnico = null;

        foreach (var f in fantasmasMuro)
        {
            if (f != null) Destroy(f);
        }
        fantasmasMuro.Clear();
    }

    void SuspenderInteracoesConcorrentes()
    {
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
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

        UnityEngine.EventSystems.PointerEventData eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        eventData.position = Input.mousePosition;

        List<UnityEngine.EventSystems.RaycastResult> results = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

        foreach (UnityEngine.EventSystems.RaycastResult result in results)
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

        return true;
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
}
