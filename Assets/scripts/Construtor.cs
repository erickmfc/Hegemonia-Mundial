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

    // Variáveis internas do Muro
    private bool definindoMuro = false; // Se já clicou o primeiro ponto
    private Vector3 pontoInicial;
    private List<GameObject> fantasmasMuro = new List<GameObject>(); // Previews visuais
    private GameObject fantasmaUnico; // Preview para construções normais
    private float rotacaoExtra = 0f; // Rotação extra para o muro (tecla R)

    void Update()
    {
        if (!modoConstrucao || prefabSelecionado == null) return;

        // Cancelar com Botão Direito
        if (Input.GetMouseButtonDown(1)) 
        {
            CancelarConstrucao();
            return;
        }

        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;
        bool acertouChao = false;

        // TENTATIVA 1: Usar a Layer configurada (mais preciso)
        if (layerChao.value != 0 && Physics.Raycast(raio, out toque, 1000f, layerChao))
        {
            acertouChao = true;
        }
        // FALLBACK: Se não tiver layer configurada, tenta acertar QUALQUER colisor
        // MAS ignora a layer "Ignore Raycast" (onde ficam os fantasmas)
        else
        {
            int layerIgnore = LayerMask.NameToLayer("Ignore Raycast");
            int mascara = ~(1 << layerIgnore); // Tudo MENOS Ignore Raycast
            
            if (Physics.Raycast(raio, out toque, 1000f, mascara))
            {
                acertouChao = true;
                // Debug para saber onde está batendo (só mostra 1x por segundo para não floodar)
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[Construtor] FALLBACK: Batendo em '{toque.collider.name}' na Layer '{LayerMask.LayerToName(toque.collider.gameObject.layer)}'");
                }
            }
        }

        if (acertouChao)
        {
            Vector3 pontoMouse = toque.point;
            
            bool ehMuro = prefabSelecionado.name.Contains("Muro") || prefabSelecionado.name.Contains("Fence");

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
            RemoverColisores(fantasmaUnico); // Para não bater nele mesmo
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
            Instantiate(prefabSelecionado, ponto, fantasmaUnico.transform.rotation);
            // Opcional: CancelarConstrucao(); // Se quiser fechar após construir um
        }
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
                // CancelarConstrucao(); // Descomente se quiser sair do modo muro ao terminar
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
            RemoverColisores(g); // Fantasma não pode ter colisão
            SetLayerRecursively(g, LayerMask.NameToLayer("Ignore Raycast"));
            // Opcional: Mudar material para transparente/verde aqui
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
            Instantiate(prefabSelecionado, pos, rotacaoFinal);
        }
    }

    // --- API PARA INTELIGÊNCIA ARTIFICIAL (CPU) ---
    public GameObject ConstruirEstruturaIA(GameObject prefab, Vector3 posicao, Quaternion rotacao)
    {
        if (prefab == null) return null;

        // Instancia direto:
        GameObject novoPredio = Instantiate(prefab, posicao, rotacao);
        
        Debug.Log($"[Construtor IA] Construiu {prefab.name} em {posicao}");
        return novoPredio;
    }
    
    // CHAMADO PELO SEU MENU
    public void SelecionarParaConstruir(GameObject prefab)
    {
        CancelarConstrucao(); // Limpa seleção anterior
        prefabSelecionado = prefab;
        modoConstrucao = true;
        Debug.Log($"[Construtor] MODO CONSTRUÇÃO ATIVADO para: {prefab.name}");
    }

    public void CancelarConstrucao()
    {
        modoConstrucao = false;
        definindoMuro = false;
        prefabSelecionado = null;
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
    void RemoverColisores(GameObject obj)
    {
        Collider[] cols = obj.GetComponentsInChildren<Collider>();
        foreach (var c in cols) Destroy(c);
        // Se tiver NavMeshObstacle, remove também
        UnityEngine.AI.NavMeshObstacle[] navs = obj.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();
        foreach (var n in navs) Destroy(n);
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
