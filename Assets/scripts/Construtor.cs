using UnityEngine;

public class Construtor : MonoBehaviour
{
    private GameObject fantasmaAtual; // O prédio transparente que segue o mouse
    private GameObject prefabReal;    // O prédio verdadeiro que será construído
    
    [Header("Configurações")]
    public LayerMask camadaChao; // Para o Raycast saber o que é chão

    void Update()
    {
        // Se temos um fantasma, ele deve seguir o mouse
        if (fantasmaAtual != null)
        {
            MoverFantasma();
            GerenciarRotacao();
            DetectarCliqueConstrucao();
            DetectarCancelamento();
        }
    }

    // --- FUNÇÃO CHAMADA PELOS BOTÕES DA UI ---
    public void SelecionarParaConstruir(GameObject item)
    {
        // Se já tem um fantasma, destrói o anterior
        if (fantasmaAtual != null) Destroy(fantasmaAtual);

        prefabReal = item;
        
        // Cria o fantasma (visual)
        fantasmaAtual = Instantiate(prefabReal);

        // --- SOLUÇÃO DE CAMADA (LAYER) VIA CÓDIGO ---
        // Muda para "Ignore Raycast" para o mouse atravessar o fantasma e acertar o chão.
        // Fazemos via código para não precisar estragar o Prefab original.
        SetLayerRecursively(fantasmaAtual, LayerMask.NameToLayer("Ignore Raycast"));
        
        // --- SOLUÇÃO DO EFEITO ESPELHO ---
        // Se o fantasma tiver colisor, o raycast do mouse acerta ELE MESMO em vez do chão.
        // O jogo detecta o toque no topo do fantasma e sobe a posição. 
        // No próximo frame, detecta o toque ainda mais alto, e ele voa.
        // Solução: Arrancamos qualquer física do fantasma.
        Collider[] colisores = fantasmaAtual.GetComponentsInChildren<Collider>();
        foreach (Collider c in colisores) Destroy(c);
        
        // Também desativamos obstáculos de navegação, se houver
        var obstaculo = fantasmaAtual.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstaculo != null) obstaculo.enabled = false;
    }

    void MoverFantasma()
    {
        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;

        // Tenta acertar a camadChao definida no Inspector. Se falhar, tenta acertar QUALQUER COISA (exceto UI)
        if (Physics.Raycast(raio, out toque, 1000f, camadaChao))
        {
            fantasmaAtual.transform.position = toque.point;
        }
        else if (Physics.Raycast(raio, out toque, 1000f)) // Fallback: Tenta pegar qualquer colisor
        {
             fantasmaAtual.transform.position = toque.point;
        }
    }

    void GerenciarRotacao()
    {
        // Se apertar "R", gira 90 graus
        if (Input.GetKeyDown(KeyCode.R))
        {
            fantasmaAtual.transform.Rotate(0, 90, 0);
        }
    }

    void DetectarCliqueConstrucao()
    {
        if (Input.GetMouseButtonDown(0)) // Clique Esquerdo
        {
            // Cria o prédio REAL na posição e rotação do fantasma
            GameObject novoPredio = Instantiate(prefabReal, fantasmaAtual.transform.position, fantasmaAtual.transform.rotation);
            
            // Opcional: Descontar dinheiro aqui (comunicar com GerenteDeJogo)
            Debug.Log("Construído: " + novoPredio.name);

            // NÃO destruímos o fantasma. Assim você pode continuar clicando e fazendo uma linha de muros!
        }
    }

    void DetectarCancelamento()
    {
        if (Input.GetMouseButtonDown(1)) // Clique Direito
        {
            Destroy(fantasmaAtual);
            fantasmaAtual = null;
            prefabReal = null;
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
