using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(SistemaDeDanos))]
public class TorreDeControle : MonoBehaviour
{
    // ===================================
    // CONFIGURAÇÕES GERAIS
    // ===================================
    [Header("Radar e Detecção")]
    public float alcanceRadar = 2000f;
    public LayerMask camadaAvioes;
    [Tooltip("Tempo em segundos para varrer o radar novamente.")]
    public float intervaloScan = 2.0f;

    [Header("Pista de Pouso")]
    [Tooltip("Arraste o objeto vazio que indica onde os aviões devem tocar o chão.")]
    public Transform pontoDePouso;
    [Tooltip("Arraste o objeto vazio que indica o ponto final da pista (para alinhar o pouso).")]
    public Transform fimDaPista;

    [Header("Interface (Menu de Comando)")]
    public GameObject painelDeComando; // Prefab ou referência do Canvas UI
    public Transform containerListaAvioes; // Onde os botões serão criados (Grid Layout)
    public GameObject botaoAviaoPrefab;    // Prefab do botão da lista

    // ===================================
    // ESTADO INTERNO
    // ===================================
    private List<ControleAviaoCaca> avioesAliados = new List<ControleAviaoCaca>();
    private bool menuAberto = false;
    private IdentidadeUnidade identidade;

    // Singleton simplificado para fácil acesso (opcional)
    public static TorreDeControle Instancia;

    void Awake()
    {
        Instancia = this; // Assume que só tem uma torre principal por base por enquanto
    }

    void Start()
    {
        identidade = GetComponent<IdentidadeUnidade>();
        
        // Se não tiver menu, busca ou reclama
        if (painelDeComando == null)
            Debug.LogWarning("[TorreDeControle] Painel de Comando UI não atribuído!");
        else
            painelDeComando.SetActive(false);

        // Inicia escaneamento constante
        InvokeRepeating("EscanearAvioes", 1f, intervaloScan);
    }

    void Update()
    {
        // Detecta seleção da torre (Clique do Mouse)
        if (Input.GetMouseButtonDown(0))
        {
            DetectarCliqueNaTorre();
        }

        // Atalhos de teclado quando menu aberto
        if (menuAberto)
        {
            if (Input.GetMouseButtonDown(1)) // Botão direito fecha
            {
                FecharMenu();
            }
        }
    }

    void DetectarCliqueNaTorre()
    {
        // Raycast simples
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            // Se clicou em mim ou em um filho meu
            if (hit.transform.root == transform.root)
            {
                AbrirMenu();
            }
        }
    }

    // ===================================
    // LÓGICA DO RADAR
    // ===================================
    void EscanearAvioes()
    {
        avioesAliados.Clear();

        // Encontra todos os caças na cena (forma bruta mas eficaz para RTS pequeno)
        ControleAviaoCaca[] todosCacas = FindObjectsByType<ControleAviaoCaca>(FindObjectsSortMode.None);
        
        int meuTime = (identidade != null) ? identidade.teamID : 1;

        foreach (var aviao in todosCacas)
        {
            // Verifica distância
            float dist = Vector3.Distance(transform.position, aviao.transform.position);
            if (dist <= alcanceRadar)
            {
                // Verifica time
                var idAviao = aviao.GetComponent<IdentidadeUnidade>();
                if (idAviao != null && idAviao.teamID == meuTime)
                {
                    avioesAliados.Add(aviao);
                }
            }
        }
        
        // Se o menu estiver aberto, atualiza a lista visual em tempo real
        if (menuAberto)
        {
            AtualizarInterface();
        }
    }

    // ===================================
    // INTERFACE DE COMANDO
    // ===================================
    public void AbrirMenu()
    {
        if (painelDeComando == null) return;
        
        menuAberto = true;
        painelDeComando.SetActive(true);
        EscanearAvioes(); // Força scan atualizado
        AtualizarInterface();
    }

    public void FecharMenu()
    {
        if (painelDeComando == null) return;
        menuAberto = false;
        painelDeComando.SetActive(false);
    }

    void AtualizarInterface()
    {
        if (containerListaAvioes == null || botaoAviaoPrefab == null) return;

        // Limpa lista antiga
        foreach (Transform child in containerListaAvioes)
        {
            Destroy(child.gameObject);
        }

        // Cria novos botões
        foreach (var aviao in avioesAliados)
        {
            GameObject btnObj = Instantiate(botaoAviaoPrefab, containerListaAvioes);
            
            // Configura textos do botão (Nome, Status)
            Text txtInfo = btnObj.GetComponentInChildren<Text>();
            if (txtInfo != null)
            {
                txtInfo.text = $"{aviao.name}\nStatus: {aviao.ObterEstadoTexto()}"; // Precisa add getter no avião
            }

            // Configura ações dos botões
            Button btn = btnObj.GetComponent<Button>();
            
            // Botão Principal: Selecionar o avião na câmera (Focar)
            btn.onClick.AddListener(() => SelecionarAviao(aviao));

            // Botão Extra 1: Mandar Pousar (Precisa ter um sub-botão no prefab ou lógica de clique direito)
            // Para simplificar, vamos assumir que o prefab tem um script que chama "SolicitarPousoNaTorre"
            
            // Se tiver um componente de "Comando UI" no prefab, configuramos ele
            var cmdUI = btnObj.GetComponent<ComandoAviaoUI>();
            if(cmdUI != null)
            {
                cmdUI.Configurar(this, aviao);
            }
        }
    }

    // ===================================
    // COMANDOS
    // ===================================
    public void SelecionarAviao(ControleAviaoCaca aviao)
    {
        // Centraliza a câmera ou seleciona no RTS manager
        // Exemplo: GerenciadorSelecao.SelecionarUnico(aviao.gameObject);
        Debug.Log($"[Torre] Avião {aviao.name} selecionado.");
    }

    public void OrdenarPouso(ControleAviaoCaca aviao)
    {
        if (pontoDePouso == null)
        {
            Debug.LogError("[Torre] Ponto de pouso não definido!");
            return;
        }

        aviao.SolicitarPouso(pontoDePouso.position);
        Debug.Log($"[Torre] Ordenando pouso para {aviao.name}");
    }

    public void OrdenarPatrulha(ControleAviaoCaca aviao)
    {
        // Envia para um ponto aleatório longe
        Vector3 pontoAleatorio = transform.position + Random.insideUnitSphere * 500f;
        pontoAleatorio.y = aviao.altitudeCruzeiro;
        aviao.DefinirDestino(pontoAleatorio);
    }

    // Gizmos
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, alcanceRadar);

        if (pontoDePouso != null && fimDaPista != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pontoDePouso.position, fimDaPista.position);
            Gizmos.DrawSphere(pontoDePouso.position, 1f);
        }
    }
}
