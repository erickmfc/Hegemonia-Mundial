using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MenuComportamento : MonoBehaviour
{
    [Header("Configuração Visual")]
    public Color corFundo = new Color(0, 0, 0, 0.8f);
    public Color corBotaoAtivo = new Color(0.8f, 0, 0, 1f); // Vermelho Combate
    public Color corBotaoPassivo = new Color(0, 0.5f, 1f, 1f); // Azul Passivo
    public Font fonteUI;

    private GameObject painelMenu;
    private Text txtEstadoAtual;
    private GerenteSelecao gerenteSelecao;

    void Start()
    {
        gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();
        CriarInterface();
        painelMenu.SetActive(false);
    }

    void Update()
    {
        if (gerenteSelecao != null)
        {
            // Mostra o menu apenas se tiver unidades selecionadas
            bool temSelecao = gerenteSelecao.unidadesSelecionadas.Count > 0;
            
            if(temSelecao != painelMenu.activeSelf)
            {
                painelMenu.SetActive(temSelecao);
            }

            if (temSelecao)
            {
                AtualizarTextoEstado();
            }
        }
    }

    void AtualizarTextoEstado()
    {
        // Verifica o estado da primeira unidade para atualizar o texto do display
        if(gerenteSelecao.unidadesSelecionadas.Count > 0 && txtEstadoAtual != null)
        {
            var unidade = gerenteSelecao.unidadesSelecionadas[0];
            if(unidade != null)
            {
                ControleTorreta torreta = unidade.GetComponentInChildren<ControleTorreta>();
                if(torreta != null)
                {
                    txtEstadoAtual.text = torreta.modoPassivo ? "ESTADO: <color=#88ffff>PASSIVO</color>" : "ESTADO: <color=#ff8888>ATAQUE</color>";
                }
                else
                {
                    txtEstadoAtual.text = "ESTADO: --";
                }
            }
        }
    }

    public void DefinirComportamento(bool passivo)
    {
        if (gerenteSelecao == null) return;

        int contagem = 0;
        foreach (var unidade in gerenteSelecao.unidadesSelecionadas)
        {
            if (unidade == null) continue;
            
            // Procura o controle da torreta nos filhos (onde geralmente fica a arma) ou no próprio objeto
            ControleTorreta[] torretas = unidade.GetComponentsInChildren<ControleTorreta>();
            foreach(var t in torretas)
            {
                t.modoPassivo = passivo;
                contagem++;
            }
            // Fallback: Procura no pai/raiz se não achou
            if(torretas.Length == 0)
            {
                 ControleTorreta t = unidade.GetComponent<ControleTorreta>();
                 if(t != null) { t.modoPassivo = passivo; contagem++; }
            }

            // --- Suporte para LANCADOR MULTIPLO (Leopard) ---
            // Passivo (true) = Automatico desligado (false)
            // Ativo (false) = Automatico ligado (true)
            LancadorMultiplo[] lancadores = unidade.GetComponentsInChildren<LancadorMultiplo>();
            foreach(var l in lancadores)
            {
                l.modoAutomatico = !passivo; 
            }
            if(lancadores.Length == 0)
            {
                LancadorMultiplo l = unidade.GetComponent<LancadorMultiplo>();
                if(l != null) l.modoAutomatico = !passivo;
            }
        }
        
        Debug.Log($"Ordem enviada: Modo {(passivo ? "PASSIVO" : "ATAQUE")} aplicado a {contagem} torretas.");
    }

    void CriarInterface()
    {
        // 1. Canvas Check
        GameObject canvasObj = GameObject.Find("Canvas_Interface");
        if (canvasObj == null) 
        {
            canvasObj = new GameObject("Canvas_Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // 2. Painel Principal (Canto Superior Direito)
        // REDUÇÃO DE 25%: Antes 200x160 -> Agora 150x120
        painelMenu = new GameObject("Painel_Comportamento", typeof(RectTransform), typeof(Image));
        painelMenu.transform.SetParent(canvasObj.transform, false);
        
        Image imgPanel = painelMenu.GetComponent<Image>();
        imgPanel.color = corFundo; 
        
        RectTransform rt = painelMenu.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1); 
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -20); 
        rt.sizeDelta = new Vector2(150, 120); // Reduzido

        // 3. Título / Estado
        GameObject txtObj = new GameObject("TextoEstado", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(painelMenu.transform, false);
        txtEstadoAtual = txtObj.GetComponent<Text>();
        txtEstadoAtual.font = fonteUI != null ? fonteUI : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txtEstadoAtual.text = "ESTADO: ...";
        txtEstadoAtual.alignment = TextAnchor.MiddleCenter;
        txtEstadoAtual.fontSize = 11; // Reduzido de 14
        txtEstadoAtual.color = Color.white;
        txtEstadoAtual.supportRichText = true;
        
        RectTransform txtRT = txtObj.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0, 1); txtRT.anchorMax = new Vector2(1, 1);
        txtRT.anchoredPosition = new Vector2(0, -15); // Antes -20
        txtRT.sizeDelta = new Vector2(0, 25);

        // 4. Botão PASSIVO
        // Antes Y -60 -> Agora -45
        CriarBotao("BTN_PASSIVO", "PASSIVO", corBotaoPassivo, new Vector2(0, -45), () => DefinirComportamento(true));

        // 5. Botão ATIVO
        // Antes Y -110 -> Agora -85
        CriarBotao("BTN_ATIVO", "ATIVO", corBotaoAtivo, new Vector2(0, -85), () => DefinirComportamento(false));
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
        rt.sizeDelta = new Vector2(135, 30); // Reduzido de 180x40

        // Texto do Botão
        GameObject tObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
        tObj.transform.SetParent(btnObj.transform, false);
        Text t = tObj.GetComponent<Text>();
        t.text = texto;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.fontSize = 10; // Fonte menor (padrao era ~14)
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontStyle = FontStyle.Bold;
        
        RectTransform trt = tObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
    }
}
