using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Script auxiliar que cria automaticamente uma barra de vida visual
/// para qualquer GameObject que tenha o componente Vida
/// </summary>
[RequireComponent(typeof(Vida))]
public class CriarBarraDeVida : MonoBehaviour
{
    [Header("Configuração Automática")]
    [Tooltip("Altura da barra acima da unidade")]
    public float altura = 3.5f; // Aumentado para melhor visualização
    
    [Tooltip("Criar a barra automaticamente no Start?")]
    public bool criarAutomaticamente = true;
    
    [Tooltip("Prefab personalizado da barra (opcional)")]
    public GameObject prefabBarraPersonalizada;

    void Start()
    {
        if (criarAutomaticamente)
        {
            CriarBarra();
        }
    }

    /// <summary>
    /// Cria a barra de vida completa via código
    /// </summary>
    public void CriarBarra()
    {
        // Verifica se já existe uma barra de vida
        BarraDeVida barraExistente = GetComponentInChildren<BarraDeVida>();
        if (barraExistente != null)
        {
            Debug.Log($"ℹ️ {gameObject.name} já tem uma barra de vida!");
            return;
        }

        GameObject barraPrefab;
        
        // Se tem um prefab personalizado, usa ele
        if (prefabBarraPersonalizada != null)
        {
            barraPrefab = Instantiate(prefabBarraPersonalizada, transform);
        }
        else
        {
            // Cria a barra via código
            barraPrefab = CriarBarraPorCodigo();
        }
        
        // Configura a barra
        BarraDeVida scriptBarra = barraPrefab.GetComponent<BarraDeVida>();
        if (scriptBarra != null)
        {
            scriptBarra.scriptVida = GetComponent<Vida>();
            scriptBarra.alturaAcimaDaUnidade = altura;
        }
        
        Debug.Log($"✅ Barra de vida criada para {gameObject.name}!");
    }

    GameObject CriarBarraPorCodigo()
    {
        // 1. Cria o GameObject principal com Canvas
        GameObject barraObj = new GameObject("BarraDeVida");
        barraObj.transform.SetParent(transform);
        barraObj.transform.localPosition = Vector3.up * altura;
        
        // 2. Adiciona Canvas (World Space)
        Canvas canvas = barraObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        // 3. Adiciona CanvasScaler para manter qualidade
        CanvasScaler scaler = barraObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        
        // 4. Configura o RectTransform do Canvas (MAIOR PARA MELHOR VISUALIZAÇÃO)
        RectTransform canvasRect = barraObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(150, 20); // Aumentado de 100x10 para 150x20
        canvasRect.localScale = new Vector3(0.02f, 0.02f, 0.02f); // Aumentado de 0.01 para 0.02 (2x maior)
        
        // 5. Cria o fundo da barra (Background)
        GameObject fundoObj = new GameObject("Fundo");
        fundoObj.transform.SetParent(barraObj.transform);
        
        Image fundoImage = fundoObj.AddComponent<Image>();
        fundoImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Cinza escuro semi-transparente
        
        RectTransform fundoRect = fundoObj.GetComponent<RectTransform>();
        fundoRect.anchorMin = Vector2.zero;
        fundoRect.anchorMax = Vector2.one;
        fundoRect.offsetMin = Vector2.zero;
        fundoRect.offsetMax = Vector2.zero;
        
        // 6. Cria a barra de preenchimento (Fill)
        GameObject fillObj = new GameObject("Preenchimento");
        fillObj.transform.SetParent(fundoObj.transform);
        
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = Color.green;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 1f;
        
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        
        // Adiciona uma margem interna de 2 pixels
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);
        
        // 7. Cria o texto de vida (acima da barra) - MAIOR E MAIS VISÍVEL
        GameObject textoObj = new GameObject("TextoVida");
        textoObj.transform.SetParent(barraObj.transform);
        
        Text textoVida = textoObj.AddComponent<Text>();
        textoVida.text = "100/100";
        textoVida.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textoVida.fontSize = 18; // Aumentado de 14 para 18
        textoVida.alignment = TextAnchor.MiddleCenter;
        textoVida.color = Color.white;
        textoVida.fontStyle = FontStyle.Bold; // Negrito para melhor leitura
        
        // Adiciona sombra para melhor leitura
        Shadow sombra = textoObj.AddComponent<Shadow>();
        sombra.effectColor = Color.black;
        sombra.effectDistance = new Vector2(1, -1);
        
        RectTransform textoRect = textoObj.GetComponent<RectTransform>();
        textoRect.anchorMin = new Vector2(0, 0.5f);
        textoRect.anchorMax = new Vector2(1, 1.5f);
        textoRect.offsetMin = Vector2.zero;
        textoRect.offsetMax = Vector2.zero;
        
        // 8. Adiciona o script BarraDeVida
        BarraDeVida scriptBarra = barraObj.AddComponent<BarraDeVida>();
        scriptBarra.canvasBarra = canvas;
        scriptBarra.imagemPreenchimento = fillImage;
        scriptBarra.textoVidaLegacy = textoVida; // Usa Text padrão
        scriptBarra.scriptVida = GetComponent<Vida>();
        
        return barraObj;
    }

    /// <summary>
    /// Remove a barra de vida se existir
    /// </summary>
    public void RemoverBarra()
    {
        BarraDeVida barra = GetComponentInChildren<BarraDeVida>();
        if (barra != null)
        {
            Destroy(barra.gameObject);
            Debug.Log($"🗑️ Barra de vida removida de {gameObject.name}");
        }
    }
}
