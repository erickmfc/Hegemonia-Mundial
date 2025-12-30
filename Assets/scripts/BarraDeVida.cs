using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida visual que fica em cima da cabeça das unidades (World Space UI)
/// </summary>
public class BarraDeVida : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("O componente Vida da unidade")]
    public Vida scriptVida;
    
    [Tooltip("A imagem de preenchimento da barra (Image com Fill Amount)")]
    public Image imagemPreenchimento;
    
    [Tooltip("Canvas que contém a barra")]
    public Canvas canvasBarra;
    
    [Header("Configuração Visual")]
    [Tooltip("Cor quando a vida está cheia")]
    public Color corVidaCheia = Color.green;
    
    [Tooltip("Cor quando a vida está no meio")]
    public Color corVidaMedia = Color.yellow;
    
    [Tooltip("Cor quando a vida está baixa")]
    public Color corVidaBaixa = Color.red;
    
    [Header("Comportamento")]
    [Tooltip("Altura da barra acima da unidade (em metros)")]
    public float alturaAcimaDaUnidade = 2.5f;
    
    [Tooltip("Esconder a barra quando a vida estiver cheia?")]
    public bool esconderSeVidaCheia = true;
    
    [Tooltip("Esconder quando a unidade morrer?")]
    public bool esconderAoMorrer = true;
    
    [Tooltip("A barra sempre olha para a câmera?")]
    public bool olharParaCamera = true;

    private Camera cameraJogo;
    private Transform targetTransform; // Transform da unidade

    void Start()
    {
        // Busca a câmera principal
        cameraJogo = Camera.main;
        
        // Se não definiu o script de vida, tenta pegar do pai
        if (scriptVida == null)
        {
            scriptVida = GetComponentInParent<Vida>();
            
            if (scriptVida == null)
            {
                Debug.LogError($"⚠️ BarraDeVida em {gameObject.name} não encontrou componente Vida!");
                enabled = false;
                return;
            }
        }
        
        // Armazena o transform da unidade (geralmente o pai da barra)
        targetTransform = scriptVida.transform;
        
        // Configuração automática do Canvas se foi criado via código
        ConfigurarCanvas();
        
        // Busca a imagem de preenchimento se não foi definida
        if (imagemPreenchimento == null)
        {
            imagemPreenchimento = GetComponentInChildren<Image>();
            
            if (imagemPreenchimento == null)
            {
                Debug.LogError($"⚠️ BarraDeVida não encontrou componente Image!");
                enabled = false;
                return;
            }
        }
        
        // Configura o tipo de preenchimento da imagem
        imagemPreenchimento.type = Image.Type.Filled;
        imagemPreenchimento.fillMethod = Image.FillMethod.Horizontal;
        
        Debug.Log($"✅ Barra de vida configurada para {scriptVida.gameObject.name}");
    }

    void ConfigurarCanvas()
    {
        if (canvasBarra == null)
        {
            canvasBarra = GetComponent<Canvas>();
        }
        
        if (canvasBarra != null)
        {
            canvasBarra.renderMode = RenderMode.WorldSpace;
            
            // Ajusta a escala do canvas para ficar mais natural
            RectTransform rectTransform = canvasBarra.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(100, 10); // Tamanho padrão
                rectTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f); // Escala pequena
            }
        }
    }

    void Update()
    {
        if (scriptVida == null || imagemPreenchimento == null) return;
        
        // Atualiza a posição da barra acima da unidade
        AtualizarPosicao();
        
        // Faz a barra olhar sempre para a câmera
        if (olharParaCamera && cameraJogo != null)
        {
            transform.LookAt(transform.position + cameraJogo.transform.rotation * Vector3.forward,
                             cameraJogo.transform.rotation * Vector3.up);
        }
        
        // Atualiza o visual da barra
        AtualizarBarra();
    }

    void AtualizarPosicao()
    {
        if (targetTransform != null)
        {
            // Posiciona a barra acima da unidade
            transform.position = targetTransform.position + Vector3.up * alturaAcimaDaUnidade;
        }
    }

    void AtualizarBarra()
    {
        // Calcula a porcentagem de vida
        float porcentagem = scriptVida.PorcentagemVida();
        
        // Atualiza o preenchimento
        imagemPreenchimento.fillAmount = porcentagem;
        
        // Muda a cor baseado na porcentagem
        if (porcentagem > 0.6f)
        {
            imagemPreenchimento.color = corVidaCheia;
        }
        else if (porcentagem > 0.3f)
        {
            imagemPreenchimento.color = corVidaMedia;
        }
        else
        {
            imagemPreenchimento.color = corVidaBaixa;
        }
        
        // Esconde a barra se a vida estiver cheia (opcional)
        if (esconderSeVidaCheia && canvasBarra != null)
        {
            canvasBarra.enabled = porcentagem < 1f;
        }
        
        // Esconde a barra se a unidade morreu
        if (esconderAoMorrer && canvasBarra != null)
        {
            if (!scriptVida.EstaVivo())
            {
                canvasBarra.enabled = false;
            }
        }
    }
}
