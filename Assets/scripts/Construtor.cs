using UnityEngine;
using System.Collections.Generic;

public class Construtor : MonoBehaviour
{
    [Header("Configurações")]
    public LayerMask layerChao; // O que é considerado chão? (Defina no Inspector)
    public float larguraDoMuro = 4.0f; // Tamanho do prefab do Muro (ajuste conforme seu modelo)

    [Header("Debug / Estado Atual")]
    public GameObject prefabSelecionado;
    public bool modoConstrucao = false;

    // Variáveis internas
    private int custoAtual = 0; // Custo do item sendo posicionado
    private DadosConstrucao.CategoriaItem categoriaAtual; // Categoria do item sendo posicionado
    private bool definindoMuro = false;
    private Vector3 pontoInicial;
    private List<GameObject> fantasmasMuro = new List<GameObject>(); 
    private GameObject fantasmaUnico; 
    private float rotacaoExtra = 0f;
    
    // Configurações Extras
    public float alturaDoMar = 0.0f; // Altura padrão da água

    private bool previewLocalInvalido = false;
    private string motivoInvalido = ""; // Para mensagens dinâmicas de erro de terreno/território
    
    // FIX: Variável para ignorar o primeiro frame após a seleção, evitando conflito com o clique do botão da UI
    private bool recemSelecionado = false;

    void Update()
    {
        if (!modoConstrucao || prefabSelecionado == null) return;
        
        // FIX: Se acabou de selecionar, ignora a checagem da UI neste frame para dar tempo do menu fechar
        if (recemSelecionado)
        {
            recemSelecionado = false;
            return;
        }

        // 1. Pausa o Construtor e oculta fantasmas se o mouse estiver sobre a Interface verdadeira (Menu, Minimapa)
        // PROTEÇÃO: Ignora a malha invisível de Barras de Vida (WorldSpace Canvas) das tropas da IA!
        if (IsMouseOverUI())
        {
            if (fantasmaUnico != null) fantasmaUnico.SetActive(false);
            foreach (var f in fantasmasMuro) { if (f != null) f.SetActive(false); }
            return; // Bloqueia a construção se o mouse estiver na UI
        }

        // 2. Garante que os fantasmas fiquem visíveis se o mouse não estiver na UI
        if (fantasmaUnico != null && !fantasmaUnico.activeSelf) fantasmaUnico.SetActive(true);

        // Cancelar com Botão Direito
        if (Input.GetMouseButtonDown(1)) 
        {
            CancelarConstrucao(true); 
            return;
        }

        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;
        bool acertouChao = false;
        Vector3 pontoMouse = Vector3.zero;

        // --- Lógica de Detecção de Terreno Aprimorada ---
        bool ehConstrucaoNaval = prefabSelecionado.name.ToLower().Contains("estaleiro") 
                              || prefabSelecionado.name.ToLower().Contains("pier")
                              || prefabSelecionado.name.ToLower().Contains("plataforma");

        int layerIgnore = LayerMask.NameToLayer("Ignore Raycast");
        int mascaraGeral = ~(1 << layerIgnore); // Tudo menos IgnoreRaycast

        if (ehConstrucaoNaval)
        {
            // NOVA LÓGICA ROBUSTA: Baseada em Plano Matemático (ignora colisor da água)
            // 1. Projeta o raio no Nível do Mar Teórico
            UnityEngine.Plane planoMar = new UnityEngine.Plane(Vector3.up, new Vector3(0, alturaDoMar, 0));
            float distancia;
            
            if (planoMar.Raycast(raio, out distancia))
            {
                Vector3 pontoNoMar = raio.GetPoint(distancia);
                
                // 2. Validação: Verifica se tem "Terra Firme" muito alta neste ponto (Ilha/Montanha)
                // Lança um raio do céu para baixo na coordenada X,Z encontrada
                RaycastHit infoTerreno;
                Vector3 origemCeu = new Vector3(pontoNoMar.x, alturaDoMar + 500f, pontoNoMar.z);
                
                bool temTerraEmbaixo = false;
                if (Physics.Raycast(origemCeu, Vector3.down, out infoTerreno, 1000f, mascaraGeral))
                {
                    // Ignora se o raio bateu na própria água (se tiver colisor) ou em coisas navais
                    bool bateuEmAguaOuNaval = infoTerreno.collider.name.ToLower().Contains("agua") || 
                                              infoTerreno.collider.name.ToLower().Contains("water") ||
                                              infoTerreno.collider.gameObject.layer == 4; // Water

                    if (!bateuEmAguaOuNaval)
                    {
                        // Bateu em algo sólido (provavelmente terreno)
                        if (infoTerreno.point.y > alturaDoMar + 1.0f) // Tolerância de 1m
                        {
                            temTerraEmbaixo = true;
                        }
                    }
                }

                // 3. Decisão Final: Aplica a todos (Plataforma, Estaleiro, Pier)
                // Se tiver terra firme muito alta (montanha) embaixo, bloqueia para não ficar "enterrado".
                if (temTerraEmbaixo)
                {
                    acertouChao = false;
                }
                else
                {
                    acertouChao = true;
                    pontoMouse = pontoNoMar;
                    
                    // Altura Base: Nível do Mar
                    pontoMouse.y = alturaDoMar;
                    
                    // AJUSTE DE ALTURA ESPECÍFICO
                    if (prefabSelecionado.name.ToLower().Contains("plataforma"))
                    {
                        pontoMouse.y = 30.0f; // Ajuste FIXO conforme referência da cena (Y ~30)
                    }
                }
            }
        }
        else 
        {
            // Estratégia Terrestre (Padrão)
            if (layerChao.value != 0 && Physics.Raycast(raio, out toque, 1000f, layerChao))
            {
                acertouChao = true;
                pontoMouse = toque.point;
            }
            else 
            {
                // =========================================================================
                // 🛡️ A GRANDE CORREÇÃO: PERFURAÇÃO DE COLISORES BUGADOS (RAYCAST ALL)
                // =========================================================================
                // Em vez de bater na primeira coisa (que pode ser a caixa gigante do dedo do soldado),
                // nós atravessamos todas as camadas até achar o chão verdadeiro.
                RaycastHit[] hits = Physics.RaycastAll(raio, 2000f, mascaraGeral);
                
                // Ordena os impactos do mais perto da câmera para o mais longe
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    
                    string n = h.collider.name.ToLower();
                    
                    // 1. Ignora os ossos bugados e cubos de colisão das tropas importadas
                    if (n.Contains("bip001") || n.Contains("bone") || n.Contains("finger") || n.Contains("cube"))
                        continue;

                    // 2. Ignora os próprios soldados/veículos inteiros para poder clicar no chão atrás deles
                    if (h.collider.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null || h.collider.GetComponentInParent<ControleUnidade>() != null)
                        continue;

                    // Se sobreviveu aos filtros, achou o chão real!
                    acertouChao = true;
                    pontoMouse = h.point;
                    break;
                }
            }
        }

        if (acertouChao)
        {
            bool ehMuro = prefabSelecionado.name.Contains("Muro") || prefabSelecionado.name.Contains("Fence");
            bool ehPlataforma = prefabSelecionado.name.ToLower().Contains("plataforma");

            if (ehConstrucaoNaval && fantasmaUnico != null && !ehPlataforma)
            {
                // RESTRIÇÕES REMOVIDAS: O usuário solicitou que Pier e Estaleiro possam ser construídos em qualquer lugar.
                previewLocalInvalido = false;
                motivoInvalido = "";
            }
            else
            {
                previewLocalInvalido = false;
                motivoInvalido = "";
            }

            // --- SISTEMA DE TERRITÓRIO E SOBERANIA ---
            if (!previewLocalInvalido)
            {
                if (GerenteDeTerritorio.Instancia == null)
                {
                    // Força a criação do Gerente para as regras funcionarem na cena
                    GameObject gerObj = new GameObject("GerenteDeTerritorio_Sistema");
                    gerObj.AddComponent<GerenteDeTerritorio>();
                }
                
                int donoDoPonto = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(pontoMouse);
                int meuTime = 1; // O jogador sempre é 1

                bool ehPrefeitura = prefabSelecionado.GetComponent<ComplexoGovernamental>() != null || prefabSelecionado.name.ToLower().Contains("prefeitura") || prefabSelecionado.name.ToLower().Contains("complexo");
                bool ehBandeira = prefabSelecionado.name.ToLower().Contains("bandeira") || prefabSelecionado.name.ToLower().Contains("flag") || prefabSelecionado.GetComponent<MarcadorTerritorio>() != null;

                // 1. Construções Comuns: Exigem terra nacionalmente dominada
                if (!ehPrefeitura && !ehBandeira && !ehPlataforma)
                {
                    if (donoDoPonto != meuTime) 
                    {
                        previewLocalInvalido = true;
                        motivoInvalido = "❌ TERRITÓRIO NÃO REIVINDICADO:\nConstrua dentro das linhas do seu País ou expanda plantando Bandeiras.";
                    }
                }
                
                // 2. Prefeituras: Só podem em terra Neutra ou Sua. Proibido 2 na mesma ilha/terra.
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

                // 3. Bandeiras (Expansões): Proibido fincar totalmente no quintal inimigo. 
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
    }

    // --- CONSTRUÇÃO NORMAL (Casas, Quartéis) ---
    void GerenciarConstrucaoNormal(Vector3 ponto)
    {
        // Atualiza o fantasma de forma segura (evita que Awake() cause erros antes de limparmos os scripts)
        if (fantasmaUnico == null)
        {
            GameObject containerSeguro = new GameObject("ContainerSeguro_Construtor");
            containerSeguro.SetActive(false); // Mantém inativo para segurar o Awake

            fantasmaUnico = Instantiate(prefabSelecionado, ponto, Quaternion.identity, containerSeguro.transform);
            
            RemoverColisoresEScripts(fantasmaUnico); // Limpa tudo com ele inativo
            SetLayerRecursively(fantasmaUnico, LayerMask.NameToLayer("Ignore Raycast"));
            
            fantasmaUnico.transform.SetParent(null); // Tira do container
            Destroy(containerSeguro); // Limpa o container
            
            fantasmaUnico.SetActive(true); // Agora ele acorda sem scripts perigosos
        }
        
        fantasmaUnico.transform.position = ponto;

        // --- SISTEMA DE COR: FANTASMA VERMELHO SE FOR INVÁLIDO ---
        AplicarCorNoFantasma(fantasmaUnico, previewLocalInvalido);

        // Rotacionar com R
        if (Input.GetKeyDown(KeyCode.R))
        {
            fantasmaUnico.transform.Rotate(0, 90, 0);
        }

        // Clica para construir
        if (Input.GetMouseButtonDown(0))
        {
            if (previewLocalInvalido)
            {
                Debug.LogWarning($"⚠️ [Construtor] Abortando: {motivoInvalido}");
                return; 
            }

            Vector3 posFinal = fantasmaUnico.transform.position;
            Quaternion rotFinal = fantasmaUnico.transform.rotation;
            
            Debug.Log($"[Construtor] Construção Instantânea de {prefabSelecionado.name} em {posFinal}");
            
             // Instancia o objeto real
            GameObject novo = Instantiate(prefabSelecionado, posFinal, rotFinal);
            
            // Garante que a lógica esteja ativa e com os colisores no lugar certo
            ReativarLogicaUnidade(novo);
            EnsureCollider(novo);
            
            // ANIMAR ESCALA NA CONSTRUÇÃO
            Vector3 escalaOriginal = novo.transform.localScale;
            novo.transform.localScale = Vector3.zero;
            AnimadorConstrucao anim = novo.AddComponent<AnimadorConstrucao>();
            anim.IniciarAnimacao(escalaOriginal, 1.5f); // 1.5 segundos para crescer
            
            // SUCESSO! Não reembolsa dinheiro, apenas limpa a seleção
            CancelarConstrucao(false); 
        }
    }

    // Helper para pintar o fantasma de vermelho
    void AplicarCorNoFantasma(GameObject fantasma, bool ehInvalido)
    {
        Renderer[] renders = fantasma.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renders)
        {
            foreach (Material mat in r.materials)
            {
                if (ehInvalido)
                {
                    mat.color = new Color(1f, 0.2f, 0.2f, 0.6f); // Vermelho Translúcido
                }
                else
                {
                    mat.color = new Color(0.2f, 1f, 0.2f, 0.6f); // Verde Translúcido Seguro
                }

                // Força o material a ser transparente para o fantasma
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

    // --- CLASSE AUXILIAR PARA ANIMAR ---
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
            
            // Easing simples: acelera rápido e freia no fim
            float curva = 1f - Mathf.Pow(1f - t, 3f);
            
            transform.localScale = Vector3.Lerp(Vector3.zero, alvoEscala, curva);

            if (tempo >= duracao)
            {
                transform.localScale = alvoEscala;
                Destroy(this); // Remove o script depois que termina
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
        foreach (var script in scripts) script.enabled = true;

        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) 
        {
            agent.enabled = true;
        }
        
        unidade.layer = LayerMask.NameToLayer("Default");
    }

    // --- CONSTRUÇÃO DE MURO (Estilo RTS) ---
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
            fantasmasMuro[i].transform.position = inicio + (dir * (i * larguraDoMuro)) + (dir * (larguraDoMuro/2)); 
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
            Vector3 pos = inicio + (dir * (i * larguraDoMuro)) + (dir * (larguraDoMuro/2));
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
        Debug.Log($"[Construtor IA] Construiu {prefab.name} em {posicao}");
        return novoPredio;
    }
    
    public void SelecionarParaConstruir(GameObject prefab, int custo, DadosConstrucao.CategoriaItem categoria)
    {
        if (modoConstrucao) CancelarConstrucao(true);

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

        if (fantasmaUnico != null) Destroy(fantasmaUnico);
        fantasmaUnico = null;
        
        foreach (var f in fantasmasMuro) 
        {
            if(f != null) Destroy(f);
        }
        fantasmasMuro.Clear();
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
        if (obj.GetComponentInChildren<Collider>() == null)
        {
            Renderer r = obj.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                if (r.gameObject != obj)
                {
                    r.gameObject.AddComponent<BoxCollider>();
                }
                else
                {
                    obj.AddComponent<BoxCollider>();
                }
            }
            else
            {
                obj.AddComponent<BoxCollider>();
            }
        }
    }

    public float ObterAlturaTerreno(Vector3 ponto)
    {
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
        int mascaraGeral = ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
        
        // CORREÇÃO: Aplica a mesma técnica de ignorar os ossos (Bip001) para a verificação de território da IA!
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(ponto.x, 500f, ponto.z), Vector3.down, 1000f, mascaraGeral);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            string n = hit.collider.name.ToLower();
            
            if (n.Contains("bip001") || n.Contains("bone") || n.Contains("cube") || n.Contains("finger")) continue;
            if (hit.collider.GetComponentInParent<IdentidadeUnidade>()) continue;

            int l = hit.collider.gameObject.layer;
            if (l == 4 || n.Contains("water") || n.Contains("agua") || n.Contains("ocean") || n.Contains("mar") || n.Contains("sea"))
                return 1; // Agua

            if (hit.point.y <= alturaDoMar + 1.0f) return 1; // Agua Funda
            
            return 2; // Terra Firme Real
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
            Canvas c = result.gameObject.GetComponentInParent<Canvas>();
            if (c != null && c.renderMode != RenderMode.WorldSpace)
            {
                return true; 
            }
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
}