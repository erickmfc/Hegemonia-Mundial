using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Script auxiliar para criar automaticamente o HUD de recursos.
/// Adicione este componente em um GameObject vazio e ele criará toda a UI necessária.
/// </summary>
[ExecuteInEditMode]
public class CriadorHUDRecursos : MonoBehaviour
{
    [Header("⚙️ Configuração")]
    [Tooltip("Clique neste botão no Inspector para criar o HUD automaticamente")]
    public bool criarHUD = false;

    [Header("🎨 Customização Visual")]
    public Color corFundo = new Color(0.1f, 0.1f, 0.15f, 0.9f); // Mais opaco
    public Color corTexto = new Color(0.9f, 0.9f, 0.95f, 1f);
    public int tamanhoFonte = 16; // Reduzido de 18 para 16
    public int tamanhoFonteGanho = 12; // Reduzido de 14 para 12

#if UNITY_EDITOR
    void Update()
    {
        if (criarHUD)
        {
            criarHUD = false;
            CriarHUDCompleto();
        }
    }

    void CriarHUDCompleto()
    {
        Debug.Log("🎨 Iniciando CRIAÇÃO DO HUD COMPACTO (LISTA VERTICAL)...");
        
        // 0. LIMPEZA: Remove HUD antigo
        PainelRecursos[] antigos = FindObjectsByType<PainelRecursos>(FindObjectsSortMode.None);
        foreach (var p in antigos)
            if (p != null && p.gameObject != null) DestroyImmediate(p.gameObject);
            
        var objAntigo = GameObject.Find("Painel_Recursos");
        if (objAntigo != null) DestroyImmediate(objAntigo);
        var objStatusAntigo = GameObject.Find("Barra_Status_Imperial");
        if (objStatusAntigo != null) DestroyImmediate(objStatusAntigo);

        // 0.1 RECUPERAÇÃO DE DEPENDÊNCIAS (Gerenciadores)
        if (FindFirstObjectByType<GerenciadorRecursos>() == null)
            new GameObject("GerenciadorRecursos").AddComponent<GerenciadorRecursos>();
            
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
             new GameObject("EventSystem").AddComponent<UnityEngine.EventSystems.EventSystem>()
                .gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        if (FindFirstObjectByType<CensoImperial>() == null)
             new GameObject("CensoImperial").AddComponent<CensoImperial>();

        // 1. Criar Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas_HUD");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. CRIAR PAINEL VERTICAL (Main Container)
        GameObject painelPrincipal = new GameObject("Painel_Recursos_Vertical");
        painelPrincipal.transform.SetParent(canvas.transform, false);
        
        RectTransform rtPanel = painelPrincipal.AddComponent<RectTransform>();
        // Ancorar no Topo Esquerdo (0, 1)
        rtPanel.anchorMin = new Vector2(0, 1);
        rtPanel.anchorMax = new Vector2(0, 1);
        rtPanel.pivot = new Vector2(0, 1);
        rtPanel.anchoredPosition = new Vector2(20, -20); // Margem de 20px
        
        // Fundo Semi-Transparente Compacto
        Image fundo = painelPrincipal.AddComponent<Image>();
        fundo.color = new Color(0.05f, 0.05f, 0.1f, 0.7f); // Preto azulado transparente

        // Layout Vertical
        VerticalLayoutGroup layout = painelPrincipal.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10); // Margem interna reduzida (era 15)
        layout.spacing = 5; // Espaçamento vertical reduzido (era 8)
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false; // IMPORTANTE: Não esticar largura!
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;

        // Content Size Fitter (Ajusta altura E largura ao conteúdo)
        ContentSizeFitter fitter = painelPrincipal.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; 
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Adiciona o script de lógica
        PainelRecursos painelScript = painelPrincipal.AddComponent<PainelRecursos>();

        // 3. CRIAR LINHAS DE RECURSO
        painelScript.textoDinheiro = CriarLinha(painelPrincipal.transform, "Dinheiro", "$", out painelScript.ganhoTextoDinheiro);
        painelScript.textoPetroleo = CriarLinha(painelPrincipal.transform, "Petróleo", "Oil", out painelScript.ganhoTextoPetroleo);
        painelScript.textoAco = CriarLinha(painelPrincipal.transform, "Aço", "Stl", out painelScript.ganhoTextoAco);
        painelScript.textoEnergia = CriarLinha(painelPrincipal.transform, "Energia", "Pwr", out painelScript.ganhoTextoEnergia);
        
        CriarSeparador(painelPrincipal.transform);

        painelScript.textoEstoque = CriarLinha(painelPrincipal.transform, "Estoque", "Cap", out var temp1);
        painelScript.textoPopulacao = CriarLinha(painelPrincipal.transform, "População", "Pop", out var temp2);
        painelScript.textoExercito = CriarLinha(painelPrincipal.transform, "Exército", "Uni", out var temp3);

        Debug.Log("✅ HUD Vertical 'Slim' (Magrinho) criado!");
        Selection.activeGameObject = painelPrincipal;
    }

    TextMeshProUGUI CriarLinha(Transform parent, string nome, string icone, out TextMeshProUGUI campoGanho)
    {
        GameObject linha = new GameObject("Linha_" + nome);
        linha.transform.SetParent(parent, false);
        
        // Linha Layout Horizontal
        HorizontalLayoutGroup hLayout = linha.AddComponent<HorizontalLayoutGroup>();
        hLayout.childControlWidth = true; 
        hLayout.childControlHeight = false;
        hLayout.childForceExpandWidth = false; // Não esticar
        hLayout.spacing = 8; // Espaço entre ícone e ganho
        hLayout.childAlignment = TextAnchor.MiddleLeft;

        // Ícone + Valor
        GameObject objTexto = new GameObject("Valor");
        objTexto.transform.SetParent(linha.transform, false);
        TextMeshProUGUI txtValor = objTexto.AddComponent<TextMeshProUGUI>();
        // Use espaço para separar label do valor
        txtValor.text = $"{icone}: 0";
        txtValor.fontSize = 16; // Fonte levemente menor (era 18)
        txtValor.color = Color.white;
        txtValor.alignment = TextAlignmentOptions.Left;
        txtValor.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        
        // Ganho (+10/s)
        GameObject objGanho = new GameObject("Ganho");
        objGanho.transform.SetParent(linha.transform, false);
        TextMeshProUGUI txtGanho = objGanho.AddComponent<TextMeshProUGUI>();
        txtGanho.text = "";
        txtGanho.fontSize = 12;
        txtGanho.color = new Color(0.7f, 1f, 0.7f); 
        txtGanho.alignment = TextAlignmentOptions.Left;
        
        // Ajustes de tamanho minímos para garantir legibilidade mas sem excesso
        // Preferrerd Width no texto cuida do resto
        objTexto.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        objGanho.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Remove LayoutElements fixos que estavam forçando largura
        // objTexto.AddComponent<LayoutElement>().minWidth = 80; <-- REMOVIDO
        // objGanho.AddComponent<LayoutElement>().minWidth = 50; <-- REMOVIDO
        
        // Adiciona LayoutElement com minSize pequeno só por segurança
        LayoutElement leTexto = objTexto.AddComponent<LayoutElement>();
        leTexto.minWidth = 20;
        
        campoGanho = txtGanho;
        return txtValor;
    }

    void CriarSeparador(Transform parent)
    {
        GameObject sep = new GameObject("Separador");
        sep.transform.SetParent(parent, false);
        Image img = sep.AddComponent<Image>();
        img.color = new Color(1,1,1,0.1f);
        LayoutElement le = sep.AddComponent<LayoutElement>();
        le.minHeight = 2;
        le.preferredHeight = 2;
        le.flexibleWidth = 1;
    }
#endif
}
