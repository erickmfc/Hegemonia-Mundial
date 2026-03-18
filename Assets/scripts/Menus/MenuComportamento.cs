using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MenuComportamento : MonoBehaviour
{
    [Header("Configuração Visual")]
    public Color corFundo = new Color(0, 0, 0, 0.8f);
    public Color corBotaoAtivo = new Color(0.8f, 0, 0, 1f); // Vermelho Combate
    public Color corBotaoPassivo = new Color(0, 0.5f, 1f, 1f); // Azul Passivo
    
    // --- ADICIONADO: Novas cores para os novos botões ---
    public Color corBotaoPatrulha = new Color(0.8f, 0.5f, 0f, 1f); // Laranja
    public Color corBotaoSeguir = new Color(0.5f, 0f, 0.5f, 1f); // Roxo
    // ----------------------------------------------------

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

                LancadorMisselCaca caca = unidade.GetComponent<LancadorMisselCaca>();
                if (caca != null && torreta == null)
                {
                    txtEstadoAtual.text = caca.modoPassivo ? "ESTADO: <color=#88ffff>PASSIVO</color>" : "ESTADO: <color=#ff8888>PATRULHA/ATAQUE</color>";
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

            LancadorMisselCaca caca = unidade.GetComponent<LancadorMisselCaca>();
            if (caca != null)
            {
                caca.modoPassivo = passivo;
                contagem++;
            }
        }
        
        Debug.Log($"Ordem enviada: Modo {(passivo ? "PASSIVO" : "ATAQUE/PATRULHA")} aplicado a {contagem} unidades armadas.");
    }

    // --- ADICIONADO: Conexão com o novo sistema de linhas visuais ---
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
    // ----------------------------------------------------------------

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
        painelMenu = new GameObject("Painel_Comportamento", typeof(RectTransform), typeof(Image));
        painelMenu.transform.SetParent(canvasObj.transform, false);
        
        Image imgPanel = painelMenu.GetComponent<Image>();
        imgPanel.color = corFundo; 
        
        RectTransform rt = painelMenu.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1); 
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-20, -20); 
        
        // --- ALTERADO: Aumentei a altura de 120 para 200 para caber os 4 botões sem espremer ---
        rt.sizeDelta = new Vector2(150, 200); 

        // 3. Título / Estado
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
        txtRT.anchorMin = new Vector2(0, 1); txtRT.anchorMax = new Vector2(1, 1);
        txtRT.anchoredPosition = new Vector2(0, -15);
        txtRT.sizeDelta = new Vector2(0, 25);

        // 4. Botão PASSIVO
        CriarBotao("BTN_PASSIVO", "PASSIVO", corBotaoPassivo, new Vector2(0, -45), () => DefinirComportamento(true));

        // 5. Botão ATIVO
        CriarBotao("BTN_ATIVO", "ATIVO", corBotaoAtivo, new Vector2(0, -85), () => DefinirComportamento(false));

        // --- ADICIONADO: Novos botões embaixo dos antigos ---
        // 6. Botão PATRULHA
        CriarBotao("BTN_PATRULHA", "PATRULHA", corBotaoPatrulha, new Vector2(0, -125), () => AtivarModoPatrulha());

        // 7. Botão SEGUIR
        CriarBotao("BTN_SEGUIR", "SEGUIR", corBotaoSeguir, new Vector2(0, -165), () => AtivarModoSeguir());
        // ----------------------------------------------------
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
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
    }
}