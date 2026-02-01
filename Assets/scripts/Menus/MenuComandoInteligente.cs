using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Hegemonia.Menus.Comandos; 
using Hegemonia.Units; // Para ler 'UnidadeComandos'

public class MenuComandoInteligente : MonoBehaviour
{
    // Lista Global (aparece sempre, opcional)
    [Header("Comandos Globais (Sempre aparecem)")]
    public List<ComandoMenu> comandosGlobais = new List<ComandoMenu>();

    // Lista dinâmica atual
    private List<ComandoMenu> comandosAtuais = new List<ComandoMenu>();

    private GameObject painelMestre;
    public List<GameObject> selecionados = new List<GameObject>();
    
    [Header("Configuração de Voo")]
    public float antygavitiComando = 5.0f; 

    private int lastSelectionCount = -1;
    private GerenciadorDePartida gerenciador; // Adicionado para suportar a nova linha

    void Start()
    {
        gerenciador = Object.FindFirstObjectByType<GerenciadorDePartida>();
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
        
        CriarPainelBase();
    }

    void Update()
    {
        DetectarSelecao();
        
        // Atualiza a interface
        if (selecionados.Count != lastSelectionCount)
        {
            lastSelectionCount = selecionados.Count;
            AtualizarListaDeComandos();
            ReconstruirBotoes();
        }

        if (painelMestre != null)
        {
            // Só mostra o painel se tiver unidades selecionadas E comandos disponíveis
            bool deveExibir = (selecionados.Count > 0 && comandosAtuais.Count > 0);
            painelMestre.SetActive(deveExibir);
        }
    }

    void DetectarSelecao()
    {
        selecionados.Clear();
        
        // 1. Busca por Helicópteros (Novo Sistema)
        Helicoptero[] todosHelis = FindObjectsByType<Helicoptero>(FindObjectsSortMode.None);
        foreach (var heli in todosHelis)
        {
            if (heli.selecionado)
            {
                selecionados.Add(heli.gameObject);
            }
        }

        // 2. Busca Unidades Genéricas (ControleUnidade) - NOVO SUPORTE
        ControleUnidade[] todasUnidades = FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None);
        foreach (var unit in todasUnidades)
        {
            // Evita duplicatas se a unidade tiver ambos os scripts
            if (!selecionados.Contains(unit.gameObject))
            {
                if (unit.selecionado)
                {
                    selecionados.Add(unit.gameObject);
                }
            }
        }
    }

    void AtualizarListaDeComandos()
    {
        comandosAtuais.Clear();
        
        // 1. Adiciona Globais
        comandosAtuais.AddRange(comandosGlobais);

        // 2. Adiciona os ESPECÍFICOS de cada unidade selecionada
        foreach (GameObject unit in selecionados)
        {
            UnidadeComandos cmds = unit.GetComponent<UnidadeComandos>();
            if (cmds != null)
            {
                foreach (ComandoMenu cmd in cmds.comandosDestaUnidade)
                {
                    if (!comandosAtuais.Contains(cmd) && cmd != null)
                    {
                        comandosAtuais.Add(cmd);
                    }
                }
            }
        }
    }

    void CriarPainelBase()
    {
        GameObject canvasObj = GameObject.Find("Canvas_Gerado_Automatico");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas_Gerado_Automatico");
            Canvas c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        if(painelMestre != null) Destroy(painelMestre); 

        painelMestre = new GameObject("PainelComandos", typeof(RectTransform), typeof(Image));
        painelMestre.transform.SetParent(canvasObj.transform);
        
        RectTransform rt = painelMestre.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -20);
        painelMestre.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);

        VerticalLayoutGroup vlg = painelMestre.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 5;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        painelMestre.SetActive(false);
    }

    void ReconstruirBotoes()
    {
        if (painelMestre == null) return;

        // Limpa botões antigos
        foreach (Transform child in painelMestre.transform)
        {
            Destroy(child.gameObject);
        }

        // Ajusta tamanho do painel (SEM cabeçalho de vida)
        int qtd = comandosAtuais.Count;
        RectTransform rt = painelMestre.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180, 20 + (qtd * 45)); // Reduzido de 50 para 20 (sem header)

        foreach (var comando in comandosAtuais)
        {
            CriarBotao(comando);
        }
    }

    void CriarTextoAviso(string msg)
    {
        GameObject txtObj = new GameObject("Texto_Aviso");
        txtObj.transform.SetParent(painelMestre.transform);
        
        LayoutElement le = txtObj.AddComponent<LayoutElement>();
        le.minHeight = 30;

        Text t = txtObj.AddComponent<Text>();
        t.text = msg;
        
        Font fonte = null;
        try { fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (fonte == null) fonte = Font.CreateDynamicFontFromOSFont("Arial", 12);
        t.font = fonte;

        t.alignment = TextAnchor.MiddleCenter; 
        t.color = Color.gray;
        t.fontSize = 12;
    }

    void CriarBotao(ComandoMenu comando)
    {
        GameObject btnObj = new GameObject("Btn_" + comando.tituloBotao);
        btnObj.transform.SetParent(painelMestre.transform);
        
        btnObj.AddComponent<RectTransform>();
        
        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 40; 
        le.preferredHeight = 40;
        le.flexibleWidth = 1;

        btnObj.AddComponent<Image>().color = new Color(0.2f, 0.35f, 0.65f); // Azul Tático
        Button btn = btnObj.AddComponent<Button>();
        
        GameObject txtObj = new GameObject("Texto");
        txtObj.transform.SetParent(btnObj.transform);
        
        Text t = txtObj.AddComponent<Text>();
        t.text = comando.tituloBotao;
        
        // CORREÇÃO FINAL: Não tenta mais carregar Arial.ttf builtin pois causa erro na Unity nova.
        Font fonte = null;
        try { fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        
        // Se falhar (ou se o builtin estiver quebrado), cria do sistema
        if (fonte == null) fonte = Font.CreateDynamicFontFromOSFont("Arial", 14);
        
        // Se AINDA falhar, o Unity usa o padrão (melhor que crashar)
        t.font = fonte;

        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.fontSize = 13;

        RectTransform rtTxt = t.rectTransform;
        rtTxt.anchorMin = Vector2.zero; rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = Vector2.zero; rtTxt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(() => {
            Debug.Log($">>> Executando: {comando.tituloBotao}");
            if (selecionados.Count > 0)
            {
                comando.Executar(selecionados);
            }
        });
    }
}
