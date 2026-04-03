using System.Collections.Generic;
using Hegemonia.Menus.Comandos;
using Hegemonia.Units;
using UnityEngine;
using UnityEngine.UI;

public class MenuComandoInteligente : MonoBehaviour
{
    [Header("Comandos Globais (Sempre aparecem)")]
    public List<ComandoMenu> comandosGlobais = new List<ComandoMenu>();

    private readonly List<ComandoMenu> comandosAtuais = new List<ComandoMenu>();
    private readonly List<Helicoptero> helicopterosCache = new List<Helicoptero>();

    private GameObject painelMestre;
    public List<GameObject> selecionados = new List<GameObject>();

    [Header("Configuração de Voo")]
    public float antygavitiComando = 5.0f;

    private int lastSelectionSignature = int.MinValue;
    private GerenciadorDePartida gerenciador;
    private GerenteSelecao gerenteSelecao;
    private float proximaBuscaHelicopteros = -1f;

    void Start()
    {
        gerenciador = Object.FindFirstObjectByType<GerenciadorDePartida>();
        gerenteSelecao = Object.FindFirstObjectByType<GerenteSelecao>();
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject(
                "EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        CriarPainelBase();
    }

    void Update()
    {
        DetectarSelecao();

        int assinaturaSelecaoAtual = CalcularAssinaturaSelecao();
        if (assinaturaSelecaoAtual != lastSelectionSignature)
        {
            lastSelectionSignature = assinaturaSelecaoAtual;
            AtualizarListaDeComandos();
            ReconstruirBotoes();
        }

        if (painelMestre != null)
        {
            bool deveExibir = selecionados.Count > 0 && comandosAtuais.Count > 0;
            painelMestre.SetActive(deveExibir);
        }
    }

    void DetectarSelecao()
    {
        selecionados.Clear();

        if (gerenteSelecao == null)
        {
            gerenteSelecao = Object.FindFirstObjectByType<GerenteSelecao>();
        }

        if (gerenteSelecao != null)
        {
            for (int i = 0; i < gerenteSelecao.unidadesSelecionadas.Count; i++)
            {
                ControleUnidade unidade = gerenteSelecao.unidadesSelecionadas[i];
                if (unidade != null)
                {
                    selecionados.Add(unidade.gameObject);
                }
            }
        }

        AtualizarCacheHelicopterosSeNecessario();
        for (int i = 0; i < helicopterosCache.Count; i++)
        {
            Helicoptero heli = helicopterosCache[i];
            if (heli == null || !heli.selecionado)
            {
                continue;
            }

            if (!selecionados.Contains(heli.gameObject))
            {
                selecionados.Add(heli.gameObject);
            }
        }
    }

    void AtualizarCacheHelicopterosSeNecessario()
    {
        if (Time.time < proximaBuscaHelicopteros)
        {
            return;
        }

        proximaBuscaHelicopteros = Time.time + 0.35f;
        helicopterosCache.Clear();

        Helicoptero[] todosHelis = FindObjectsByType<Helicoptero>(FindObjectsSortMode.None);
        for (int i = 0; i < todosHelis.Length; i++)
        {
            if (todosHelis[i] != null)
            {
                helicopterosCache.Add(todosHelis[i]);
            }
        }
    }

    int CalcularAssinaturaSelecao()
    {
        unchecked
        {
            int assinatura = 17;
            for (int i = 0; i < selecionados.Count; i++)
            {
                GameObject selecionado = selecionados[i];
                assinatura = (assinatura * 31) + (selecionado != null ? selecionado.GetInstanceID() : 0);
            }

            return assinatura;
        }
    }

    void AtualizarListaDeComandos()
    {
        comandosAtuais.Clear();
        comandosAtuais.AddRange(comandosGlobais);

        foreach (GameObject unit in selecionados)
        {
            UnidadeComandos cmds = unit.GetComponent<UnidadeComandos>();
            if (cmds == null)
            {
                continue;
            }

            foreach (ComandoMenu cmd in cmds.comandosDestaUnidade)
            {
                if (cmd != null && !comandosAtuais.Contains(cmd))
                {
                    comandosAtuais.Add(cmd);
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

        if (painelMestre != null)
        {
            Destroy(painelMestre);
        }

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
        if (painelMestre == null)
        {
            return;
        }

        foreach (Transform child in painelMestre.transform)
        {
            Destroy(child.gameObject);
        }

        string textoEstado = "ESTADO: --";
        if (selecionados.Count > 0)
        {
            LancadorMLRS mlrs = selecionados[0].GetComponent<LancadorMLRS>();
            if (mlrs != null)
            {
                textoEstado = mlrs.modoCombateAtivo ? "ESTADO: ATIVO" : "ESTADO: PASSIVO";
            }
        }
        CriarTextoAviso(textoEstado);

        int qtd = comandosAtuais.Count;
        RectTransform rt = painelMestre.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180, 50 + (qtd * 45));

        foreach (ComandoMenu comando in comandosAtuais)
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

        btnObj.AddComponent<Image>().color = new Color(0.2f, 0.35f, 0.65f);
        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Texto");
        txtObj.transform.SetParent(btnObj.transform);

        Text t = txtObj.AddComponent<Text>();
        t.text = comando.tituloBotao;

        Font fonte = null;
        try { fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (fonte == null) fonte = Font.CreateDynamicFontFromOSFont("Arial", 14);
        t.font = fonte;

        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.fontSize = 13;

        RectTransform rtTxt = t.rectTransform;
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = Vector2.zero;
        rtTxt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(() =>
        {
            if (selecionados.Count > 0)
            {
                comando.Executar(selecionados);
            }
        });
    }
}
