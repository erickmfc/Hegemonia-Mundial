using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class MenuComportamento : MonoBehaviour
{
    [Header("Configuracao Visual")]
    public Color corFundo = new Color(0, 0, 0, 0.8f);
    public Color corBotaoAtivo = new Color(0.8f, 0, 0, 1f);
    public Color corBotaoPassivo = new Color(0, 0.5f, 1f, 1f);
    public Color corBotaoPatrulha = new Color(0.8f, 0.5f, 0f, 1f);
    public Color corBotaoSeguir = new Color(0.5f, 0f, 0.5f, 1f);

    public Font fonteUI;

    private GameObject painelMenu;
    private Text txtEstadoAtual;
    private GerenteSelecao gerenteSelecao;

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "Menu cena" || SceneManager.GetActiveScene().name == "MenuPrincipal")
        {
            enabled = false;
            return;
        }

        gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();
        CriarInterface();
        painelMenu.SetActive(false);
    }

    void Update()
    {
        if (gerenteSelecao == null || painelMenu == null)
        {
            return;
        }

        bool temSelecao = gerenteSelecao.unidadesSelecionadas.Count > 0;
        if (temSelecao != painelMenu.activeSelf)
        {
            painelMenu.SetActive(temSelecao);
        }

        if (temSelecao)
        {
            AtualizarTextoEstado();
        }
    }

    void AtualizarTextoEstado()
    {
        if (gerenteSelecao.unidadesSelecionadas.Count == 0 || txtEstadoAtual == null)
        {
            return;
        }

        bool encontrou = false;
        bool estadoBase = false;
        bool misto = false;

        for (int i = 0; i < gerenteSelecao.unidadesSelecionadas.Count; i++)
        {
            ControleUnidade unidade = gerenteSelecao.unidadesSelecionadas[i];
            if (unidade == null)
            {
                continue;
            }

            bool passivoUnidade;
            string descricaoUnidade;
            if (!unidade.TryObterEstadoCombate(out passivoUnidade, out descricaoUnidade))
            {
                continue;
            }

            if (descricaoUnidade == "MISTO")
            {
                misto = true;
                break;
            }

            if (!encontrou)
            {
                encontrou = true;
                estadoBase = passivoUnidade;
                continue;
            }

            if (estadoBase != passivoUnidade)
            {
                misto = true;
                break;
            }
        }

        if (!encontrou)
        {
            txtEstadoAtual.text = "ESTADO: --";
        }
        else if (misto)
        {
            txtEstadoAtual.text = "ESTADO: <color=#ffd966>MISTO</color>";
        }
        else if (estadoBase)
        {
            txtEstadoAtual.text = "ESTADO: <color=#88ffff>PASSIVO</color>";
        }
        else
        {
            txtEstadoAtual.text = "ESTADO: <color=#ff8888>ATIVO</color>";
        }
    }

    public void DefinirComportamento(bool passivo)
    {
        if (gerenteSelecao == null)
        {
            return;
        }

        int contagem = 0;
        for (int i = 0; i < gerenteSelecao.unidadesSelecionadas.Count; i++)
        {
            ControleUnidade unidade = gerenteSelecao.unidadesSelecionadas[i];
            if (unidade == null)
            {
                continue;
            }

            if (unidade.DefinirModoCombate(!passivo))
            {
                contagem++;
            }
        }

        Debug.Log($"Ordem enviada: Modo {(passivo ? "PASSIVO" : "ATIVO")} aplicado a {contagem} unidades.");
    }

    public void AtivarModoPatrulha()
    {
        DesenharLinhasOrdem desenhador = FindFirstObjectByType<DesenharLinhasOrdem>();
        if (desenhador != null)
        {
            desenhador.IniciarModoPatrulha();
        }
        else
        {
            Debug.LogWarning("AVISO: Crie um objeto vazio na cena e adicione o script DesenharLinhasOrdem!");
        }
    }

    public void AtivarModoSeguir()
    {
        DesenharLinhasOrdem desenhador = FindFirstObjectByType<DesenharLinhasOrdem>();
        if (desenhador != null)
        {
            desenhador.IniciarModoSeguir();
        }
        else
        {
            Debug.LogWarning("AVISO: Crie um objeto vazio na cena e adicione o script DesenharLinhasOrdem!");
        }
    }

    void CriarInterface()
    {
        GameObject canvasObj = GameObject.Find("Canvas_Interface");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas_Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        painelMenu = new GameObject("Painel_Comportamento", typeof(RectTransform), typeof(Image));
        painelMenu.transform.SetParent(canvasObj.transform, false);

        Image imgPanel = painelMenu.GetComponent<Image>();
        imgPanel.color = corFundo;

        RectTransform rt = painelMenu.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -20);
        rt.sizeDelta = new Vector2(150, 200);

        GameObject txtObj = new GameObject("TextoEstado", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(painelMenu.transform, false);
        txtEstadoAtual = txtObj.GetComponent<Text>();
        txtEstadoAtual.font = fonteUI != null ? fonteUI : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txtEstadoAtual.text = "ESTADO: ...";
        txtEstadoAtual.alignment = TextAnchor.MiddleCenter;
        txtEstadoAtual.fontSize = 11;
        txtEstadoAtual.color = Color.white;
        txtEstadoAtual.supportRichText = true;

        RectTransform txtRT = txtObj.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0, 1);
        txtRT.anchorMax = new Vector2(1, 1);
        txtRT.anchoredPosition = new Vector2(0, -15);
        txtRT.sizeDelta = new Vector2(0, 25);

        CriarBotao("BTN_PASSIVO", "PASSIVO", corBotaoPassivo, new Vector2(0, -45), delegate { DefinirComportamento(true); });
        CriarBotao("BTN_ATIVO", "ATIVO", corBotaoAtivo, new Vector2(0, -85), delegate { DefinirComportamento(false); });
        CriarBotao("BTN_PATRULHA", "PATRULHA", corBotaoPatrulha, new Vector2(0, -125), AtivarModoPatrulha);
        CriarBotao("BTN_SEGUIR", "SEGUIR", corBotaoSeguir, new Vector2(0, -165), AtivarModoSeguir);
    }

    void CriarBotao(string nome, string texto, Color cor, Vector2 pos, UnityEngine.Events.UnityAction acao)
    {
        GameObject btnObj = new GameObject(nome, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(painelMenu.transform, false);

        Image img = btnObj.GetComponent<Image>();
        img.color = cor;

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(acao);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(135, 30);

        GameObject tObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
        tObj.transform.SetParent(btnObj.transform, false);
        Text t = tObj.GetComponent<Text>();
        t.text = texto;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.fontSize = 10;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontStyle = FontStyle.Bold;

        RectTransform trt = tObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }
}
