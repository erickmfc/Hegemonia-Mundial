using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ItemMercado
{
    public string nome = "Novo Item";
    public int preco = 100;
    public Sprite icone; 
    public GameObject prefabParaConstruir;
    
    [HideInInspector] public int quantidadeSelecionada = 0; // Controle interno
}

public class GerenteMercadoUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform janelaMercado; // O Painel principal
    public Construtor scriptConstrutor;
    public Font fonteUI; // Fonte para textos

    [Header("Configuração Visual")]
    public Color corFundoLinha = new Color(0.1f, 0.15f, 0.2f, 0.8f);
    public Color corTexto = Color.white;
    public Color corBotaoComprar = new Color(0.2f, 0.5f, 0.2f); // Verde escuro
    public int alturaLinha = 60;

    [Header("Itens")]
    public List<ItemMercado> itens = new List<ItemMercado>();

    // Variaveis internas de UI
    private Text textoTotalGeral;
    private List<GameObject> linhasCriadas = new List<GameObject>();

    void Start()
    {
        if (janelaMercado == null) return;
        AtualizarInterface();
    }

    // Chama isso para redesenhar tudo (útil se mudar lista)
    [ContextMenu("Atualizar Interface")]
    public void AtualizarInterface()
    {
        // 1. Limpa tudo que existe na janela
        foreach (Transform filho in janelaMercado) Destroy(filho.gameObject);
        linhasCriadas.Clear();

        // 2. Configura Layout Vertical na Janela
        VerticalLayoutGroup layout = janelaMercado.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = janelaMercado.gameObject.AddComponent<VerticalLayoutGroup>();
        
        layout.childControlHeight = false; // Nós definimos altura das linhas
        layout.childForceExpandHeight = false;
        layout.spacing = 5;
        layout.padding = new RectOffset(10, 10, 10, 60); // Padding Bottom 60 para sobrar espaço pro botão comprar

        // 3. Cria as Linhas dos Itens
        foreach (var item in itens)
        {
            item.quantidadeSelecionada = 0; // Reseta qtd
            CriarLinhaItem(item);
        }

        // 4. Cria o Rodapé (Total e Botão Comprar)
        CriarRodape();
    }

    void CriarLinhaItem(ItemMercado item)
    {
        // Container da Linha
        GameObject linhaObj = new GameObject("Linha_" + item.nome);
        linhaObj.transform.SetParent(janelaMercado, false);
        
        // Fundo
        Image fundo = linhaObj.AddComponent<Image>();
        fundo.color = corFundoLinha;

        // Tamanho
        LayoutElement le = linhaObj.AddComponent<LayoutElement>();
        le.minHeight = alturaLinha;
        le.preferredHeight = alturaLinha;
        le.flexibleWidth = 1;

        RectTransform rtLinha = linhaObj.GetComponent<RectTransform>();
        
        // --- A. Ícone (Esquerda) ---
        if (item.icone != null)
        {
            GameObject iconObj = new GameObject("Icone");
            iconObj.transform.SetParent(linhaObj.transform, false);
            Image img = iconObj.AddComponent<Image>();
            img.sprite = item.icone;
            img.preserveAspect = true;

            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(10, 0); // Margem esq
            rt.sizeDelta = new Vector2(alturaLinha - 10, 0); // Quadrado
        }

        // --- B. Nome e Preço (Centro-Esq) ---
        CriarTexto(linhaObj, item.nome + "\n<size=12><color=#88ff88>$" + item.preco + "</color></size>", 
            TextAnchor.MiddleLeft, new Vector2(70, 0), new Vector2(-150, 0));

        // --- C. Controles de Quantidade (Direita) ---
        // Texto Qtd
        Text txtQtd = CriarTexto(linhaObj, "0", TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        RectTransform rtQtd = txtQtd.rectTransform;
        rtQtd.anchorMin = new Vector2(1, 0.5f); rtQtd.anchorMax = new Vector2(1, 0.5f);
        rtQtd.anchoredPosition = new Vector2(-70, 0);
        rtQtd.sizeDelta = new Vector2(40, 30);

        // Botão Menos
        CriarBotaoSimples(linhaObj, "-", new Vector2(-110, 0), Color.red, () => {
             if(item.quantidadeSelecionada > 0) item.quantidadeSelecionada--;
             txtQtd.text = item.quantidadeSelecionada.ToString();
             RecalcularTotal();
        });

        // Botão Mais
        CriarBotaoSimples(linhaObj, "+", new Vector2(-30, 0), Color.green, () => {
             item.quantidadeSelecionada++;
             txtQtd.text = item.quantidadeSelecionada.ToString();
             RecalcularTotal();
        });
    }

    void CriarRodape()
    {
        // Container Flutuante no final da janela (ignorando layout vertical para ficar fixo embaixo se quisesse, 
        // mas aqui vamos por como último item da lista para simplificar)
        
        GameObject rodapeObj = new GameObject("Rodape_Total");
        rodapeObj.transform.SetParent(janelaMercado, false);
        
        LayoutElement le = rodapeObj.AddComponent<LayoutElement>();
        le.minHeight = 50;
        le.flexibleWidth = 1;

        // Texto Total
        textoTotalGeral = CriarTexto(rodapeObj, "Total: $0", TextAnchor.MiddleLeft, new Vector2(20, 0), new Vector2(-120, 0));
        textoTotalGeral.fontSize = 20;

        // Botão COMPRAR
        GameObject btnObj = new GameObject("BtnComprar");
        btnObj.transform.SetParent(rodapeObj.transform, false);
        
        Image img = btnObj.AddComponent<Image>();
        img.color = corBotaoComprar;
        
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(FinalizarCompra);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 0.5f);
        rt.anchoredPosition = new Vector2(-10, 0);
        rt.sizeDelta = new Vector2(100, 0);

        // Texto do Botão
        Text txtBtn = CriarTexto(btnObj, "COMPRAR", TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        txtBtn.color = Color.white;
        txtBtn.fontStyle = FontStyle.Bold;
    }

    Text CriarTexto(GameObject pai, string conteudo, TextAnchor alinhamento, Vector2 pos, Vector2 sizeDeltaOffset)
    {
        GameObject obj = new GameObject("Txt");
        obj.transform.SetParent(pai.transform, false);
        Text txt = obj.AddComponent<Text>();
        txt.text = conteudo;
        txt.font = fonteUI != null ? fonteUI : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.color = corTexto;
        txt.alignment = alinhamento;
        txt.supportRichText = true;
        
        RectTransform rt = obj.GetComponent<RectTransform>();
        if(sizeDeltaOffset != Vector2.zero) 
        {
             // Modo esticado simples ou custom
             rt.sizeDelta = new Vector2(100, 30); // Base
        }
        else
        {
             rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
             rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        
        // Ajuste fino manual se passado pos (assumindo ancora e pivot manuais depois se precisar, 
        // mas aqui vamos simplificar usando RectTransform padrão esticado ou ancorado)
        
        if (pos != Vector2.zero || sizeDeltaOffset.x < 0) // Gambiarra para identificar posicionamento custom
        {
             rt.anchorMin = new Vector2(0,0); rt.anchorMax = new Vector2(1,1); // Esticado
             if(alinhamento == TextAnchor.MiddleLeft) { rt.offsetMin = new Vector2(pos.x, 0); rt.offsetMax = new Vector2(sizeDeltaOffset.x, 0); }
        }

        return txt;
    }

    void CriarBotaoSimples(GameObject pai, string simbolo, Vector2 posAncoraDir, Color cor, UnityEngine.Events.UnityAction acao)
    {
        GameObject btnObj = new GameObject("Btn" + simbolo);
        btnObj.transform.SetParent(pai.transform, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = cor;
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(acao);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
        rt.anchoredPosition = posAncoraDir;
        rt.sizeDelta = new Vector2(30, 30);

        CriarTexto(btnObj, simbolo, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero).fontStyle = FontStyle.Bold;
    }

    void RecalcularTotal()
    {
        int total = 0;
        foreach (var item in itens)
        {
            total += item.preco * item.quantidadeSelecionada;
        }
        if(textoTotalGeral) textoTotalGeral.text = "Total: $" + total;
    }

    void FinalizarCompra()
    {
        Debug.Log("--- INICIANDO COMPRA ---");
        // Aqui você implementa a lógica real (deduzir dinheiro, etc)
        
        foreach (var item in itens)
        {
            if (item.quantidadeSelecionada > 0)
            {
                Debug.Log($"Comprando {item.quantidadeSelecionada}x {item.nome}");
                
                // Exemplo: Se comprou 1 item de construção, inicia o Construtor
                // OBS: O Construtor atual só suporta 1 fantasma.
                // Se comprar vários, teria que ter uma lógica de fila ou spawn direto.
                // Vamos ativar o construtor para o ULTIMO item selecionado como exemplo
                if (scriptConstrutor != null && item.prefabParaConstruir != null)
                {
                    scriptConstrutor.SelecionarParaConstruir(item.prefabParaConstruir);
                }
            }
        }
        
        // Opcional: Zerar carrinho após compra?
        // foreach(var i in itens) i.quantidadeSelecionada = 0;
        // AtualizarInterface();
    }
}
