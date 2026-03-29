using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MenuInicialController : MonoBehaviour
{
    [Header("Configurações de Câmera")]
    [Tooltip("Câmera do menu diorama. Deixe vazio para usar a Main Camera.")]
    public Camera cameraDiorama;
    
    [Tooltip("Velocidade com que a câmera sobe e desce.")]
    public float velocidadeMovimento = 10f;
    
    [Tooltip("Altura mínima que a câmera pode chegar ao apertar CTRL.")]
    public float limiteAlturaMinima = 1f;

    [Tooltip("Altura máxima que a câmera pode chegar ao apertar ESPAÇO.")]
    public float limiteAlturaMaxima = 50f;

    [Header("Configuração de Cenas")]
    [Tooltip("Nome da cena principal do jogo a ser carregada (ex: Atualizacao).")]
    public string nomeCenaNovoJogo = "Atualizacao";

    [Header("Painéis UI (Opcional)")]
    public GameObject painelMenuPrincipal;
    public GameObject painelConfiguracoes;
    public GameObject painelCarregarJogo;
    public GameObject painelCreditos;

    private void Start()
    {
        if (cameraDiorama == null)
        {
            cameraDiorama = Camera.main;
        }

        // Garante que o cursor do mouse inicialize destravado e visível
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        MostrarPainelPrincipal();
    }

    private void Update()
    {
        ControlarMovimentoCamera();
    }

    /// <summary>
    /// Levanta a câmera com o ESPAÇO, e desce com o LEFT CTRL.
    /// </summary>
    private void ControlarMovimentoCamera()
    {
        if (cameraDiorama == null) return;

        Vector3 posAnterior = cameraDiorama.transform.position;
        float movimentoY = 0f;

        // Subir a tela
        if (Input.GetKey(KeyCode.Space))
        {
            movimentoY += velocidadeMovimento * Time.deltaTime;
        }

        // Descer a tela
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            movimentoY -= velocidadeMovimento * Time.deltaTime;
        }

        // Se houve intenção de movimento
        if (movimentoY != 0f)
        {
            posAnterior.y += movimentoY;
            
            // Limitadores para não deixar a câmera afundar ou sumir no céu
            posAnterior.y = Mathf.Clamp(posAnterior.y, limiteAlturaMinima, limiteAlturaMaxima);

            cameraDiorama.transform.position = posAnterior;
        }
    }

    // ============================================
    // MÉTODOS DOS BOTÕES (Ligar nos botões da UI)
    // ============================================

    public void Btn_NovoJogoSkirmish()
    {
        Debug.Log("Iniciando Novo Jogo...");
        
        // Aqui você faria a transição para a cena de jogo
        if (!string.IsNullOrEmpty(nomeCenaNovoJogo))
        {
            SceneManager.LoadScene(nomeCenaNovoJogo);
        }
        else
        {
            Debug.LogWarning("O nome da cena para o Novo Jogo está vazio no Inspector!");
        }
    }

    public void Btn_CarregarJogo()
    {
        Debug.Log("Abrindo Menu de Carregar Jogo (Placeholder)...");
        EsconderTodosPaineis();
        if (painelCarregarJogo != null) painelCarregarJogo.SetActive(true);
    }

    public void Btn_Configuracoes()
    {
        Debug.Log("Abrindo Configurações...");
        EsconderTodosPaineis();
        if (painelConfiguracoes != null) painelConfiguracoes.SetActive(true);
    }

    public void Btn_Creditos()
    {
        Debug.Log("Abrindo Créditos...");
        EsconderTodosPaineis();
        if (painelCreditos != null) painelCreditos.SetActive(true);
    }

    public void Btn_Sair()
    {
        Debug.Log("Saindo de Hegemonia Global...");
        
        #if UNITY_EDITOR
        EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    public void Btn_VoltarAoMenuPrincipal()
    {
        MostrarPainelPrincipal();
    }

    // ============================================
    // MÉTODOS AUXILIARES DE UI
    // ============================================

    private void EsconderTodosPaineis()
    {
        if (painelMenuPrincipal != null) painelMenuPrincipal.SetActive(false);
        if (painelConfiguracoes != null) painelConfiguracoes.SetActive(false);
        if (painelCarregarJogo != null) painelCarregarJogo.SetActive(false);
        if (painelCreditos != null) painelCreditos.SetActive(false);
    }

    private void MostrarPainelPrincipal()
    {
        EsconderTodosPaineis();
        if (painelMenuPrincipal != null) painelMenuPrincipal.SetActive(true);
    }
}
