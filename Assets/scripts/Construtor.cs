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

    void Update()
    {
        if (!modoConstrucao || prefabSelecionado == null) return;

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
            Plane planoMar = new Plane(Vector3.up, new Vector3(0, alturaDoMar, 0));
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
                    // Queremos saber se bateu em TERRENO/CHÃO
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
                        // Sobe a plataforma para ficar "mais alta" como pedido
                        // +50% visualmente? Vamos testar um valor fixo ou relativo.
                        // Se o modelo tem pivô no centro, subir ajuda.
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
            else if (Physics.Raycast(raio, out toque, 1000f, mascaraGeral))
            {
                acertouChao = true;
                pontoMouse = toque.point;
                // Debug (Opcional)
            }
        }

        if (acertouChao)
        {
            // Não redeclara pontoMouse, usa o calculado acima
            
            bool ehMuro = prefabSelecionado.name.Contains("Muro") || prefabSelecionado.name.Contains("Fence");
            
            if (ehConstrucaoNaval && fantasmaUnico != null)
            {
                // Acha o componente para ler os offsets se possível (Estaleiro ou PierMarinha)
                float oFrente = 35f; float oTras = -15f;
                var pier = prefabSelecionado.GetComponent<PierMarinha>();
                var est = prefabSelecionado.GetComponent<Estaleiro>();
                if (pier) { oFrente = pier.offsetAguaFrente; oTras = pier.offsetTerraTras; }
                else if (est) { oFrente = est.offsetAguaFrente; oTras = est.offsetTerraTras; }

                Vector3 posFrente = fantasmaUnico.transform.position + fantasmaUnico.transform.forward * oFrente; 
                Vector3 posTras = fantasmaUnico.transform.position + fantasmaUnico.transform.forward * oTras; 

                int tFrente = VerTipoPonto(posFrente);
                int tTras = VerTipoPonto(posTras);

                // É totalmente terra, totalmente água, ou terra na frente e água atrás? Invalido!
                previewLocalInvalido = (tFrente == 2 && tTras == 2) || (tFrente == 1 && tTras == 1) || (tFrente == 2 && tTras == 1);
                if (previewLocalInvalido) motivoInvalido = "❌ LUGAR INVÁLIDO:\nAs pistas devem ir p/ Água e a base na Terra!";
            }
            else
            {
                previewLocalInvalido = false;
                motivoInvalido = "";
            }

            // --- SISTEMA DE TERRITÓRIO E SOBERANIA ---
            if (!previewLocalInvalido && GerenteDeTerritorio.Instancia != null)
            {
                int donoDoPonto = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(pontoMouse);
                int meuTime = 1; // O jogador sempre é 1

                bool ehPrefeitura = prefabSelecionado.GetComponent<ComplexoGovernamental>() != null || prefabSelecionado.name.ToLower().Contains("prefeitura") || prefabSelecionado.name.ToLower().Contains("complexo");
                bool ehBandeira = prefabSelecionado.name.ToLower().Contains("bandeira") || prefabSelecionado.name.ToLower().Contains("flag") || prefabSelecionado.GetComponent<MarcadorTerritorio>() != null;

                // 1. Construções Comuns: Exigem terra nacionalmente dominada
                if (!ehPrefeitura && !ehBandeira)
                {
                    if (donoDoPonto != meuTime) 
                    {
                        previewLocalInvalido = true;
                        motivoInvalido = "❌ TERRITÓRIO NÃO REIVINDICADO:\nPlante Bandeiras para expandir o espaço do seu País antes de construir estruturas mecânicas aqui.";
                    }
                }
                
                // 2. Prefeituras: Só podem em terra Neutra ou Sua. Proibido 2 na mesma ilha/terra (NavMesh test).
                if (ehPrefeitura)
                {
                    if (donoDoPonto != 0 && donoDoPonto != meuTime) 
                    {
                        previewLocalInvalido = true;
                        motivoInvalido = "❌ INVASÃO DIRETA:\nVocê não pode fundar a capital do Governo em um país inimigo.";
                    }
                    else if (!GerenteDeTerritorio.Instancia.PodeConstruirPrefeitura(pontoMouse))
                    {
                        previewLocalInvalido = true;
                        motivoInvalido = "❌ JÁ EXISTE LEI AQUI:\nEsta faixa de terra contígua já possui uma Prefeitura prefilada em alguma região. Você só pode possuir 1 governo por ilha.";
                    }
                }

                // 3. Bandeiras (Expansões): Proibido fincar totalmente no quintal inimigo. 
                // Se for 0, é ilha nova (ou beirada neutra da sua expansão). Se for meuTime, é sobreposição legal.
                if (ehBandeira)
                {
                    if (donoDoPonto != 0 && donoDoPonto != meuTime) 
                    {
                        previewLocalInvalido = true;
                        motivoInvalido = "❌ JURISDIÇÃO INIMIGA:\nA soberania desta área já foi assegurada por rivais. Cuidado.";
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
        // Atualiza o fantasma
        if (fantasmaUnico == null)
        {
            fantasmaUnico = Instantiate(prefabSelecionado, ponto, Quaternion.identity);
            RemoverColisoresEScripts(fantasmaUnico); // Limpa scripts e colisores
            SetLayerRecursively(fantasmaUnico, LayerMask.NameToLayer("Ignore Raycast"));
        }
        
        fantasmaUnico.transform.position = ponto;

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
                return; // Aborta
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
        // Desativa NavMeshAgent para não andar enquanto constrói
        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Desativa scripts de comportamento
        MonoBehaviour[] scripts = unidade.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts) script.enabled = false;
        
        // Mantém Collider ativo para cliques (Demolição futura)
    }

    void ReativarLogicaUnidade(GameObject unidade)
    {
        // Reativa Scripts
        MonoBehaviour[] scripts = unidade.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts) script.enabled = true;

        // Reativa NavMeshAgent com segurança
        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) 
        {
            agent.enabled = true;
        }
        
        // Garante layer correta
        unidade.layer = LayerMask.NameToLayer("Default");
    }

    // --- CONSTRUÇÃO DE MURO (Estilo RTS) ---
    void GerenciarConstrucaoMuro(Vector3 pontoAtual)
    {
        // ROTAÇÃO COM TECLA R (funciona em qualquer etapa)
        if (Input.GetKeyDown(KeyCode.R))
        {
            rotacaoExtra += 90f;
            if (rotacaoExtra >= 360f) rotacaoExtra = 0f;
            Debug.Log($"[Construtor] Muro rotacionado para {rotacaoExtra}°");
        }

        // ETAPA 1: Ainda não definiu o início
        if (!definindoMuro)
        {
            // Mostra 1 fantasma seguindo o mouse
            AtualizarFantasmas(1, pontoAtual, pontoAtual);

            if (Input.GetMouseButtonDown(0))
            {
                definindoMuro = true;
                pontoInicial = pontoAtual;
            }
        }
        // ETAPA 2: Já definiu o início, agora está esticando
        else
        {
            // Calcula direção e distância
            Vector3 direcao = pontoAtual - pontoInicial;
            float distancia = direcao.magnitude;
            int quantidadePecas = Mathf.Max(1, Mathf.RoundToInt(distancia / larguraDoMuro));
            
            // Calcula o ponto final "travado" na grade do tamanho do muro
            Vector3 pontoFinalAjustado = pontoInicial + (direcao.normalized * (quantidadePecas * larguraDoMuro));

            // Atualiza visualização (Fantasmas)
            AtualizarFantasmas(quantidadePecas, pontoInicial, pontoFinalAjustado);

            // CLIQUE FINAL: Constrói de verdade
            if (Input.GetMouseButtonDown(0))
            {
                ConstruirLinhaDeMuro(quantidadePecas, pontoInicial, pontoFinalAjustado);
                definindoMuro = false; // Reseta para começar outro trecho se quiser
                
                // Muros podem ser contínuos? Se sim, não cancela.
                // Mas cobramos o preço por UNIDADE? 
                // O MenuConstrucao cobra 1x o preço base. Se o muro gasta N peças, deveria cobrar N vezes.
                // Isso é complexo. Por enquanto, vamos assumir que o preço pago é pelo "pacote" de muro ou apenas 1 peça.
                // Para evitar exploit, vamos fechar também.
                CancelarConstrucao(false);
            }
        }
    }

    // Cria ou remove fantasmas para mostrar a prévia do muro
    void AtualizarFantasmas(int quantidade, Vector3 inicio, Vector3 fim)
    {
        // 1. Garante que temos fantasmas suficientes na lista
        while (fantasmasMuro.Count < quantidade)
        {
            GameObject g = Instantiate(prefabSelecionado);
            RemoverColisoresEScripts(g); // Fantasma não pode ter colisão e nem scripts
            SetLayerRecursively(g, LayerMask.NameToLayer("Ignore Raycast"));
            fantasmasMuro.Add(g);
        }
        
        // 2. Posiciona os fantasmas necessários
        Vector3 dir = (fim - inicio).normalized;
        if (dir == Vector3.zero) dir = Vector3.forward; // Evita erro se inicio == fim
        Quaternion rotacaoBase = Quaternion.LookRotation(dir);
        // Aplica a rotação extra (tecla R)
        Quaternion rotacaoFinal = rotacaoBase * Quaternion.Euler(0, rotacaoExtra, 0);

        for (int i = 0; i < quantidade; i++)
        {
            fantasmasMuro[i].SetActive(true);
            fantasmasMuro[i].transform.position = inicio + (dir * (i * larguraDoMuro)) + (dir * (larguraDoMuro/2)); 
            fantasmasMuro[i].transform.rotation = rotacaoFinal;
        }

        // 3. Esconde os fantasmas sobrando (se encolheu o muro)
        for (int i = quantidade; i < fantasmasMuro.Count; i++)
        {
            fantasmasMuro[i].SetActive(false);
        }
    }

    void ConstruirLinhaDeMuro(int quantidade, Vector3 inicio, Vector3 fim)
    {
        Vector3 dir = (fim - inicio).normalized;
        Quaternion rotacaoBase = Quaternion.LookRotation(dir);
        // Aplica a rotação extra (tecla R)
        Quaternion rotacaoFinal = rotacaoBase * Quaternion.Euler(0, rotacaoExtra, 0);

        for (int i = 0; i < quantidade; i++)
        {
            Vector3 pos = inicio + (dir * (i * larguraDoMuro)) + (dir * (larguraDoMuro/2));
            GameObject novoMuro = Instantiate(prefabSelecionado, pos, rotacaoFinal);
            
            // Força a reativação de tudo e redefine a Layer para o padrão (para as balas baterem)
            ReativarLogicaUnidade(novoMuro);
            EnsureCollider(novoMuro); 
        }
    }

    // API PARA INTELIGÊNCIA ARTIFICIAL (CPU)
    public GameObject ConstruirEstruturaIA(GameObject prefab, Vector3 posicao, Quaternion rotacao)
    {
        if (prefab == null) return null;

        // Instancia direto:
        GameObject novoPredio = Instantiate(prefab, posicao, rotacao);
        
        // CORREÇÃO: Garante que tem colisor, senão não toma dano
        EnsureCollider(novoPredio);

        Debug.Log($"[Construtor IA] Construiu {prefab.name} em {posicao}");
        return novoPredio;
    }
    
    // CHAMADO PELO SEU MENU
    public void SelecionarParaConstruir(GameObject prefab, int custo, DadosConstrucao.CategoriaItem categoria)
    {
        // Se já estava construindo algo, cancela o anterior (e reembolsa se não construiu)
        if (modoConstrucao) CancelarConstrucao(true);

        prefabSelecionado = prefab;
        custoAtual = custo; // Salva o custo para reembolso
        categoriaAtual = categoria;
        modoConstrucao = true;
        Debug.Log($"[Construtor] MODO CONSTRUÇÃO ATIVADO para: {prefab.name}. Custo: {custo}. Categoria: {categoria}");
    }

    public void CancelarConstrucao(bool reembolsar = true)
    {
        if (reembolsar && custoAtual > 0)
        {
            // Devolve o dinheiro
            if (GerenciadorRecursos.Instancia != null)
            {
                GerenciadorRecursos.Instancia.AdicionarRecursos(addDinheiro: custoAtual);
                Debug.Log($"[Construtor] Reembolsado ${custoAtual}");
            }
            else
            {
                GerenteDeJogo gerente = Object.FindFirstObjectByType<GerenteDeJogo>();
                if (gerente != null)
                {
                    gerente.dinheiroAtual += custoAtual;
                    Debug.Log($"[Construtor] Reembolsado ${custoAtual} (Gerente Antigo)");
                }
            }
        }

        modoConstrucao = false;
        definindoMuro = false;
        prefabSelecionado = null;
        custoAtual = 0; // Reseta custo
        rotacaoExtra = 0f; // Reseta a rotação

        // Limpa fantasmas
        if (fantasmaUnico != null) Destroy(fantasmaUnico);
        fantasmaUnico = null;
        
        foreach (var f in fantasmasMuro) 
        {
            if(f != null) Destroy(f);
        }
        fantasmasMuro.Clear();
    }

    // Utilitário para o "Ghost" não ter colisão física e atrapalhar o clique
    // E AGORA TAMBÉM remove scripts para evitar erros de lógica no fantasma
    void RemoverColisoresEScripts(GameObject obj)
    {
        // 1. Remove Colisores (Incluindo de filhos inativos)
        // IMPORTANTE: Desativar 'enabled' primeiro para garantir que o Raycast não bata neles
        // no mesmo frame em que são destruídos.
        Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols) 
        {
            c.enabled = false; 
            Destroy(c);
        }
        
        // 2. Remove NavMeshObstacles
        UnityEngine.AI.NavMeshObstacle[] navs = obj.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
        foreach (var n in navs) Destroy(n);

        // 3. Remove Scripts (MonoBehaviours) para o fantasma ser puramente visual
        // CUIDADO: Não remover componentes essenciais de renderização (MeshFilter, Renderer, etc)
        // Por padrão, GetComponentsInChildren<MonoBehaviour> pega scripts do usuário E componentes nativos que herdam de MB.
        // Vamos filtrar.
        MonoBehaviour[] scripts = obj.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var s in scripts)
        {
            // Ignora se for nulo (já destruído)
            if (s == null) continue;

            // NÃO DESTRUA O CONSTRUTOR SE ELE ESTIVER NO OBJETO! (Improvável, mas seguro)
            if (s == this) continue;

            // NÃO destruir componentes visuais ou de UI (Básico)
            // Mas scripts complexos como "Button", "Image" são MonoBehaviours. 
            // Se o estaleiro é um objeto 3D, ele pode ter scripts de lógica que não queremos.
            // Vamos arriscar destruir tudo que for MonoBehaviour
            // EXCETO os protegidos pelo Unity se forem MB? 
            // Transform não é MB. Renderer não é MB.
            
            // Vamos simplesmente destruir:
            Destroy(s);
        }
    }

    // CORREÇÃO: Verifica se o objeto tem colisor e adiciona um se faltar
    void EnsureCollider(GameObject obj)
    {
        if (obj.GetComponentInChildren<Collider>() == null)
        {
            // Debug.LogWarning($"[Construtor] O objeto '{obj.name}' não tinha colisor! Adicionando BoxCollider automático para receber danos.");
            
            // Adiciona Box Collider no pai ou no filho que tem MeshRenderer
            Renderer r = obj.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                // Se o renderer estiver num filho, adicionamos o collider LÁ
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
                // Fallback: adiciona no root
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
        // 1 = AGUA, 2 = TERRA, 0 = INCONCLUSIVO
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(ponto.x, 500f, ponto.z), Vector3.down, out hit, 1000f))
        {
            int l = hit.collider.gameObject.layer;
            string n = hit.collider.name.ToLower();
            
            if (l == 4 || n.Contains("water") || n.Contains("agua") || n.Contains("ocean") || n.Contains("mar") || n.Contains("sea"))
                return 1;
            
            if (l == LayerMask.NameToLayer("Ignore Raycast") || hit.collider.GetComponent<IdentidadeUnidade>())
                return 0;

            return 2;
        }
        
        if (Terrain.activeTerrain != null) 
        {
            if (Terrain.activeTerrain.SampleHeight(ponto) <= alturaDoMar + 0.5f) return 1;
            return 2;
        }

        return 0; // Desconhecido (Céu vazio?)
    }

    void OnGUI()
    {
        if (modoConstrucao && previewLocalInvalido && fantasmaUnico != null && !string.IsNullOrEmpty(motivoInvalido))
        {
             // Pega um estilo base bacana de caixa (Box) do próprio Unity para ter um fundo escuro que dá leitura
             GUIStyle stylePopUp = new GUIStyle(GUI.skin.box);
             stylePopUp.fontSize = 18;
             stylePopUp.normal.textColor = new Color(1f, 0.3f, 0.3f); // Vermelho pastel legível
             stylePopUp.fontStyle = FontStyle.Bold;
             stylePopUp.alignment = TextAnchor.MiddleCenter;
             stylePopUp.wordWrap = true;
             
             // Calcula a caixa fixa no centro e na porção inferior da tela, nunca vazando pras laterais
             float largura = 450f;
             float altura = 80f;
             Rect popupRect = new Rect((Screen.width - largura) / 2f, Screen.height - 180f, largura, altura);
             
             GUI.Box(popupRect, motivoInvalido, stylePopUp);
        }
    }

    // Muda a Layer recursivamente (para Ignore Raycast)
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
