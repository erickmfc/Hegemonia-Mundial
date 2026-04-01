// Revisão proposta para o Construtor.cs
// Foco: corrigir posicionamento costeiro de Estaleiro/Pier e evitar que a origem fique presa em terra.
// Também adiciona um ajuste para empurrar a estrutura em direção à água, o que tende a corrigir
// o ponto de spawn naval quando ele depende da orientação/posição final do prédio.

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

    private int custoAtual = 0;
    private DadosConstrucao.CategoriaItem categoriaAtual;
    private bool definindoMuro = false;
    private Vector3 pontoInicial;
    private List<GameObject> fantasmasMuro = new List<GameObject>();
    private GameObject fantasmaUnico;
    private float rotacaoExtra = 0f;

    public float alturaDoMar = 0.0f;

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
            foreach (var f in fantasmasMuro) if (f != null) f.SetActive(false);
            return;
        }

        if (fantasmaUnico != null && !fantasmaUnico.activeSelf) fantasmaUnico.SetActive(true);

        if (Input.GetMouseButtonDown(1))
        {
            CancelarConstrucao(true);
            return;
        }

        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;
        bool acertouChao = false;
        Vector3 pontoMouse = Vector3.zero;

        string nomePrefabSelecionado = prefabSelecionado.name.ToLower();
        bool ehEstruturaCosteira = nomePrefabSelecionado.Contains("estaleiro") || nomePrefabSelecionado.Contains("pier");
        bool ehConstrucaoNaval = ehEstruturaCosteira || nomePrefabSelecionado.Contains("plataforma");

        int layerIgnore = LayerMask.NameToLayer("Ignore Raycast");
        int mascaraGeral = ~(1 << layerIgnore);

        bool exigePlanoMarOffshore = ehConstrucaoNaval && !ehEstruturaCosteira;

        if (exigePlanoMarOffshore)
        {
            UnityEngine.Plane planoMar = new UnityEngine.Plane(Vector3.up, new Vector3(0, alturaDoMar, 0));
            float distancia;

            if (planoMar.Raycast(raio, out distancia))
            {
                Vector3 pontoNoMar = raio.GetPoint(distancia);
                RaycastHit infoTerreno;
                Vector3 origemCeu = new Vector3(pontoNoMar.x, alturaDoMar + 500f, pontoNoMar.z);

                bool temTerraEmbaixo = false;
                if (Physics.Raycast(origemCeu, Vector3.down, out infoTerreno, 1000f, mascaraGeral))
                {
                    bool bateuEmAguaOuNaval = infoTerreno.collider.name.ToLower().Contains("agua") ||
                                              infoTerreno.collider.name.ToLower().Contains("water") ||
                                              infoTerreno.collider.gameObject.layer == 4;

                    if (!bateuEmAguaOuNaval && infoTerreno.point.y > alturaDoMar + 1.0f)
                    {
                        temTerraEmbaixo = true;
                    }
                }

                if (!temTerraEmbaixo)
                {
                    acertouChao = true;
                    pontoMouse = pontoNoMar;
                    pontoMouse.y = alturaDoMar;

                    if (prefabSelecionado.name.ToLower().Contains("plataforma"))
                    {
                        pontoMouse.y = 30.0f;
                    }
                }
            }
        }
        else
        {
            if (layerChao.value != 0 && Physics.Raycast(raio, out toque, 1000f, layerChao))
            {
                acertouChao = true;
                pontoMouse = toque.point;
            }
            else
            {
                RaycastHit[] hits = Physics.RaycastAll(raio, 2000f, mascaraGeral);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    string n = h.collider.name.ToLower();

                    if (n.Contains("bip001") || n.Contains("bone") || n.Contains("finger") || n.Contains("cube"))
                        continue;

                    if (h.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null ||
                        h.collider.GetComponentInParent<ControleUnidade>() != null)
                        continue;

                    acertouChao = true;
                    pontoMouse = h.point;
                    break;
                }
            }
        }

        if (!acertouChao)
        {
            usarRotacaoPreviewNaval = false;
            usarPosicaoPreviewNaval = false;
            return;
        }

        bool ehMuro = prefabSelecionado.name.Contains("Muro") || prefabSelecionado.name.Contains("Fence");
        bool ehPlataforma = prefabSelecionado.name.ToLower().Contains("plataforma");

        if (ehEstruturaCosteira)
        {
            Quaternion rotacaoBase = fantasmaUnico != null ? fantasmaUnico.transform.rotation : prefabSelecionado.transform.rotation;
            NavalPlacementResolver.StructurePose poseCosteira;

            if (NavalPlacementResolver.TryResolveStructurePose(prefabSelecionado, pontoMouse, rotacaoBase, out poseCosteira))
            {
                pontoMouse = poseCosteira.Position;
                posicaoPreviewNaval = poseCosteira.Position;
                rotacaoPreviewNaval = poseCosteira.Rotation;
                usarRotacaoPreviewNaval = true;
                usarPosicaoPreviewNaval = true;
                previewLocalInvalido = false;
                motivoInvalido = "";
            }
            else if (TryResolverPoseCosteiraManual(prefabSelecionado, pontoMouse, rotacaoBase, out poseCosteira))
            {
                pontoMouse = poseCosteira.Position;
                posicaoPreviewNaval = poseCosteira.Position;
                rotacaoPreviewNaval = poseCosteira.Rotation;
                usarRotacaoPreviewNaval = true;
                usarPosicaoPreviewNaval = true;
                previewUsaColocacaoNavalManual = true;
                previewLocalInvalido = false;
                motivoInvalido = "";
            }
            else
            {
                usarRotacaoPreviewNaval = false;
                usarPosicaoPreviewNaval = false;
                previewLocalInvalido = true;
                motivoInvalido = $"❌ POSIÇÃO COSTEIRA INVÁLIDA:\n{poseCosteira.Reason}.";
            }
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
            if (GerenteDeTerritorio.Instancia == null)
            {
                GameObject gerObj = new GameObject("GerenteDeTerritorio_Sistema");
                gerObj.AddComponent<GerenteDeTerritorio>();
            }

            int donoDoPonto = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(usarPosicaoPreviewNaval ? posicaoPreviewNaval : pontoMouse);
            int meuTime = 1;

            bool ehPrefeitura = prefabSelecionado.GetComponent<ComplexoGovernamental>() != null || prefabSelecionado.name.ToLower().Contains("prefeitura") || prefabSelecionado.name.ToLower().Contains("complexo");
            bool ehBandeira = prefabSelecionado.name.ToLower().Contains("bandeira") || prefabSelecionado.name.ToLower().Contains("flag") || prefabSelecionado.GetComponent<MarcadorTerritorio>() != null;

            if (!ehPrefeitura && !ehBandeira && !ehPlataforma && !ehEstruturaCosteira)
            {
                if (donoDoPonto != meuTime)
                {
                    previewLocalInvalido = true;
                    motivoInvalido = "❌ TERRITÓRIO NÃO REIVINDICADO:\nConstrua dentro das linhas do seu País ou expanda plantando Bandeiras.";
                }
            }

            if (ehPrefeitura)
            {
                if (donoDoPonto != 0 && donoDoPonto != meuTime)
                {
                    previewLocalInvalido = true;
                    motivoInvalido = "❌ INVASÃO DIRETA:\nVocê não pode fundar a Prefeitura/Capital em um país inimigo.";
                }
                else if (!GerenteDeTerritorio.Instancia.PodeConstruirPrefeitura(pontoMouse))
                {
                    previewLocalInvalido = true;
                    motivoInvalido = "❌ JÁ EXISTE LEI AQUI:\nEsta ilha já possui uma Prefeitura.";
                }
            }

            if (ehBandeira)
            {
                if (donoDoPonto != 0 && donoDoPonto != meuTime)
                {
                    previewLocalInvalido = true;
                    motivoInvalido = "❌ JURISDIÇÃO INIMIGA:\nA soberania desta área já pertence a outra Nação.";
                }
            }
        }

        if (ehMuro) GerenciarConstrucaoMuro(pontoMouse);
        else GerenciarConstrucaoNormal(pontoMouse);
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
            fantasmaUnico.transform.Rotate(0, 90, 0);
        }

        if (Input.GetMouseButtonDown(0))
        {
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
                if (manualTag == null) manualTag = novo.AddComponent<IA_ManualPlacementTag>();
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
        if (fallbackForward.sqrMagnitude < 0.01f) fallbackForward = Vector3.forward;
        fallbackForward.Normalize();

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
        float raioBuscaAgua = Mathf.Max(320f, frenteAgua + 220f);
        bool encontrouDirecaoAgua = NavalPlacementResolver.TryResolveWaterDirection(
            pontoMouse,
            fallbackForward,
            8f,
            raioBuscaAgua,
            out waterForward,
            out waterPoint,
            out nivelDoMar);

        if (!encontrouDirecaoAgua)
        {
            Vector3 pontoAguaFallback;
            string motivoAgua;
            if (NavalPlacementResolver.TryResolveWaterSpawn(
                pontoMouse,
                fallbackForward,
                0f,
                Mathf.Max(700f, frenteAgua + 420f),
                out pontoAguaFallback,
                out nivelDoMar,
                out motivoAgua))
            {
                waterPoint = pontoAguaFallback;
                Vector3 direcaoFallback = pontoAguaFallback - new Vector3(pontoMouse.x, pontoAguaFallback.y, pontoMouse.z);
                direcaoFallback.y = 0f;
                waterForward = direcaoFallback.sqrMagnitude > 0.01f ? direcaoFallback.normalized : fallbackForward;
                encontrouDirecaoAgua = true;
            }
        }

        if (!encontrouDirecaoAgua)
        {
            Vector3 pontoAguaClassificado;
            Vector3 direcaoAguaClassificada;
            if (TryDetectarAguaPorClassificacao(
                pontoMouse,
                fallbackForward,
                Mathf.Max(1800f, frenteAgua + 900f),
                out pontoAguaClassificado,
                out direcaoAguaClassificada))
            {
                waterPoint = pontoAguaClassificado;
                waterForward = direcaoAguaClassificada;
                encontrouDirecaoAgua = true;
            }
        }

        if (!encontrouDirecaoAgua)
        {
            pose.Reason = "sem agua proxima";
            return false;
        }

        Vector3 posBase = new Vector3(pontoMouse.x, nivelDoMar, pontoMouse.z);
        Vector3 direcaoParaAgua = waterPoint - posBase;
        direcaoParaAgua.y = 0f;
        if (direcaoParaAgua.sqrMagnitude > 1f)
        {
            float aproximacao = Mathf.Clamp(direcaoParaAgua.magnitude * 0.35f, 0f, Mathf.Max(12f, frenteAgua * 0.55f));
            posBase += direcaoParaAgua.normalized * aproximacao;
        }

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

        float empurraoParaAgua = Mathf.Clamp(frenteAgua * 0.45f, 10f, 28f);
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

    bool TryDetectarAguaPorClassificacao(
        Vector3 centro,
        Vector3 fallbackForward,
        float raioMaximo,
        out Vector3 pontoAgua,
        out Vector3 direcaoAgua)
    {
        float nivelDoMar = NavalPlacementResolver.ResolveSeaLevel();
        pontoAgua = new Vector3(centro.x, nivelDoMar, centro.z);

        fallbackForward.y = 0f;
        if (fallbackForward.sqrMagnitude < 0.01f) fallbackForward = Vector3.forward;
        fallbackForward.Normalize();
        direcaoAgua = fallbackForward;

        float melhorScore = float.MinValue;
        bool encontrou = false;
        float inicio = 18f;
        float fim = Mathf.Max(inicio + 24f, raioMaximo);
        float passo = fim > 800f ? 36f : 18f;

        for (float raio = inicio; raio <= fim; raio += passo)
        {
            int amostras = raio < 220f ? 16 : 24;
            for (int i = 0; i < amostras; i++)
            {
                float angulo = (360f / amostras) * i;
                Vector3 direcao = Quaternion.AngleAxis(angulo, Vector3.up) * Vector3.forward;
                Vector3 probe = new Vector3(centro.x + (direcao.x * raio), nivelDoMar, centro.z + (direcao.z * raio));

                ClassificacaoSuperficieMapa classificacao;
                float altura;
                if (!RegistroSuperficieMapa.TryClassify(probe, out classificacao, out altura, 2.5f, 1.5f))
                {
                    continue;
                }

                if (classificacao != ClassificacaoSuperficieMapa.Agua && classificacao != ClassificacaoSuperficieMapa.Costa)
                {
                    continue;
                }

                float alinhamento = Vector3.Dot(fallbackForward, direcao);
                float score = (alinhamento * 0.45f) - (raio * 0.006f);
                if (!encontrou || score > melhorScore)
                {
                    encontrou = true;
                    melhorScore = score;
                    pontoAgua = probe;
                    direcaoAgua = direcao.normalized;
                }
            }
        }

        return encontrou;
    }

    void TentarFixarSpawnNaval(GameObject estrutura, Quaternion rotacao, bool logar)
    {
        if (estrutura == null) return;

        Transform[] filhos = estrutura.GetComponentsInChildren<Transform>(true);
        float nivelDoMar = NavalPlacementResolver.ResolveSeaLevel();
        Vector3 forward = rotacao * Vector3.forward;
        if (forward.sqrMagnitude < 0.01f) forward = estrutura.transform.forward;

        foreach (Transform t in filhos)
        {
            if (t == null) continue;
            string nome = t.name.ToLower();

            bool pareceSpawn = nome.Contains("spawn") || nome.Contains("saida") || nome.Contains("launch") || nome.Contains("navio");
            if (!pareceSpawn) continue;

            Vector3 pos = t.position;
            if (!NavalPlacementResolver.IsWaterAtPosition(pos, nivelDoMar))
            {
                Vector3 corrigido = estrutura.transform.position + (forward * 30f);
                corrigido.y = nivelDoMar;
                t.position = corrigido;
                if (logar) Debug.Log($"[Construtor] Spawn naval ajustado em {estrutura.name} -> {t.name} para {corrigido}");
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
        if (agent != null) agent.enabled = true;
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
        if (dir == Vector3.zero) dir = Vector3.forward;
        Quaternion rotacaoBase = Quaternion.LookRotation(dir);
        Quaternion rotacaoFinal = rotacaoBase * Quaternion.Euler(0, rotacaoExtra, 0);

        for (int i = 0; i < quantidade; i++)
        {
            fantasmasMuro[i].SetActive(true);
            fantasmasMuro[i].transform.position = inicio + (dir * (i * larguraDoMuro)) + (dir * (larguraDoMuro / 2));
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
        Quaternion rotacaoFinal = rotacaoBase * Quaternion.Euler(0, rotacaoExtra, 0);

        for (int i = 0; i < quantidade; i++)
        {
            Vector3 pos = inicio + (dir * (i * larguraDoMuro)) + (dir * (larguraDoMuro / 2));
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

        if (fantasmaUnico != null) Destroy(fantasmaUnico);
        fantasmaUnico = null;

        foreach (var f in fantasmasMuro) if (f != null) Destroy(f);
        fantasmasMuro.Clear();
    }

    private void SuspenderInteracoesConcorrentes()
    {
        MenuMisseis menuMisseis = Object.FindFirstObjectByType<MenuMisseis>();
        if (menuMisseis != null) menuMisseis.CancelarLancamento();

        GerenciadorAeroporto[] aeroportos = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        foreach (GerenciadorAeroporto aeroporto in aeroportos)
        {
            if (aeroporto != null) aeroporto.CancelarInteracaoPorConstrucao();
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
        foreach (var n in navs) Destroy(n);

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
                var mc = target.AddComponent<MeshCollider>();
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

        if (Terrain.activeTerrain != null) return Terrain.activeTerrain.SampleHeight(ponto);
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(ponto.x, 500f, ponto.z), Vector3.down, out hit, 1000f))
        {
            if (!hit.collider.name.ToLower().Contains("water")) return hit.point.y;
        }
        return 0f;
    }

    public int VerTipoPonto(Vector3 ponto)
    {
        ClassificacaoSuperficieMapa classificacaoMarcada;
        float alturaMarcada;
        if (RegistroSuperficieMapa.TryClassify(ponto, out classificacaoMarcada, out alturaMarcada))
        {
            if (classificacaoMarcada == ClassificacaoSuperficieMapa.Agua || classificacaoMarcada == ClassificacaoSuperficieMapa.Costa) return 1;
            if (classificacaoMarcada == ClassificacaoSuperficieMapa.Chao) return 2;
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
            if (l == 4 || n.Contains("water") || n.Contains("agua") || n.Contains("ocean") || n.Contains("mar") || n.Contains("sea")) return 1;
            if (hit.point.y <= alturaDoMar + 1.0f) return 1;
            return 2;
        }

        if (Terrain.activeTerrain != null)
        {
            if (Terrain.activeTerrain.SampleHeight(ponto) <= alturaDoMar + 1.0f) return 1;
            return 2;
        }

        return 0;
    }

    private bool IsMouseOverUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

        UnityEngine.EventSystems.PointerEventData eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        eventData.position = Input.mousePosition;

        List<UnityEngine.EventSystems.RaycastResult> results = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

        foreach (UnityEngine.EventSystems.RaycastResult result in results)
        {
            if (result.gameObject == null || !result.gameObject.activeInHierarchy) continue;
            Canvas c = result.gameObject.GetComponentInParent<Canvas>();
            if (c == null || c.renderMode == RenderMode.WorldSpace) continue;
            if (!UIEstaVisivelEInterativa(result.gameObject)) continue;
            return true;
        }
        return false;
    }

    private static bool UIEstaVisivelEInterativa(GameObject uiObject)
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
