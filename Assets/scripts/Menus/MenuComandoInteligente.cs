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
    private readonly List<BotaoSlot> botoes = new List<BotaoSlot>(16);
    private readonly List<int> bufferAssinatura = new List<int>(64);

    private GameObject painelMestre;
    private Text textoEstado;
    public List<GameObject> selecionados = new List<GameObject>();

    [Header("Configuração de Voo")]
    public float antygavitiComando = 5.0f;

    private int lastSelectionSignature = int.MinValue;
    private GerenciadorDePartida gerenciador;
    private GerenteSelecao gerenteSelecao;
    private bool rebuildPendente;
    private int assinaturaPendente;

    public enum ComandoInterno
    {
        Nenhum,
        Passivo,
        Ativo,
        Patrulhar,
        Seguir
    }

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

        if (GerenciadorHelicopteros.Instancia == null)
        {
            new GameObject("GerenciadorHelicopteros_Auto", typeof(GerenciadorHelicopteros));
        }

        CriarPainelBase();
    }

    void Update()
    {
        DetectarSelecao();

        int assinaturaSelecaoAtual = CalcularAssinaturaSelecaoEstavel();
        if (assinaturaSelecaoAtual != lastSelectionSignature)
        {
            assinaturaPendente = assinaturaSelecaoAtual;
            rebuildPendente = true;
        }

        if (rebuildPendente && !DeveAdiarRebuildUi())
        {
            rebuildPendente = false;
            lastSelectionSignature = assinaturaPendente;
            AtualizarListaDeComandos();
            ReconstruirBotoes();
        }

        if (painelMestre != null)
        {
            bool deveExibir = selecionados.Count > 0 && (comandosAtuais.Count > 0 || TemComandosInternos());
            painelMestre.SetActive(deveExibir);
        }
    }

    bool DeveAdiarRebuildUi()
    {
        if (!Input.GetMouseButton(0))
        {
            return false;
        }

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            return false;
        }

        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
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

        GerenciadorHelicopteros gerenciadorHelicopteros = GerenciadorHelicopteros.Instancia;
        if (gerenciadorHelicopteros == null)
        {
            return;
        }

        List<Helicoptero> helis = gerenciadorHelicopteros.helicopterosRegistrados;
        for (int i = 0; i < helis.Count; i++)
        {
            Helicoptero heli = helis[i];
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

    int CalcularAssinaturaSelecaoEstavel()
    {
        bufferAssinatura.Clear();
        for (int i = 0; i < selecionados.Count; i++)
        {
            GameObject selecionado = selecionados[i];
            if (selecionado != null)
            {
                bufferAssinatura.Add(selecionado.GetInstanceID());
            }
        }

        bufferAssinatura.Sort();

        unchecked
        {
            int assinatura = 17;
            for (int i = 0; i < bufferAssinatura.Count; i++)
            {
                assinatura = (assinatura * 31) + bufferAssinatura[i];
            }

            return assinatura;
        }
    }

    void AtualizarListaDeComandos()
    {
        comandosAtuais.Clear();
        for (int i = 0; i < comandosGlobais.Count; i++)
        {
            ComandoMenu comandoGlobal = comandosGlobais[i];
            if (comandoGlobal != null && !ComandoEhCobertoPorInterno(comandoGlobal))
            {
                comandosAtuais.Add(comandoGlobal);
            }
        }

        for (int i = 0; i < selecionados.Count; i++)
        {
            GameObject unit = selecionados[i];
            if (unit == null)
            {
                continue;
            }

            UnidadeComandos cmds = unit.GetComponent<UnidadeComandos>();
            if (cmds == null)
            {
                continue;
            }

            for (int j = 0; j < cmds.comandosDestaUnidade.Count; j++)
            {
                ComandoMenu cmd = cmds.comandosDestaUnidade[j];
                if (cmd != null && !comandosAtuais.Contains(cmd) && !ComandoEhCobertoPorInterno(cmd))
                {
                    comandosAtuais.Add(cmd);
                }
            }
        }
    }

    bool TemComandosInternos()
    {
        return selecionados.Count > 0;
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
        GarantirTextoEstado();
    }

    void ReconstruirBotoes()
    {
        if (painelMestre == null)
        {
            return;
        }

        float inicio = Time.realtimeSinceStartup;

        string textoEstadoValor = ResolverTextoEstado();
        if (textoEstado != null)
        {
            textoEstado.text = textoEstadoValor;
        }

        int qtd = TemComandosInternos() ? 4 : 0;
        for (int i = 0; i < comandosAtuais.Count; i++)
        {
            if (comandosAtuais[i] != null)
            {
                qtd++;
            }
        }

        RectTransform rt = painelMestre.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(190, 50 + (qtd * 45));

        int slotIndex = 0;
        if (TemComandosInternos())
        {
            GarantirSlot(slotIndex);
            AtualizarSlotInterno(botoes[slotIndex], ComandoInterno.Passivo, "PASSIVO", new Color(0f, 0.5f, 1f, 1f));
            slotIndex++;

            GarantirSlot(slotIndex);
            AtualizarSlotInterno(botoes[slotIndex], ComandoInterno.Ativo, "ATIVO", new Color(0.8f, 0f, 0f, 1f));
            slotIndex++;

            GarantirSlot(slotIndex);
            AtualizarSlotInterno(botoes[slotIndex], ComandoInterno.Patrulhar, "PATRULHAR", new Color(0.8f, 0.5f, 0f, 1f));
            slotIndex++;

            GarantirSlot(slotIndex);
            AtualizarSlotInterno(botoes[slotIndex], ComandoInterno.Seguir, "SEGUIR", new Color(0.5f, 0f, 0.5f, 1f));
            slotIndex++;
        }

        for (int i = 0; i < comandosAtuais.Count; i++)
        {
            ComandoMenu comando = comandosAtuais[i];
            if (comando == null)
            {
                continue;
            }

            GarantirSlot(slotIndex);
            AtualizarSlot(botoes[slotIndex], comando);
            slotIndex++;
        }

        for (int i = slotIndex; i < botoes.Count; i++)
        {
            if (botoes[i].Raiz != null)
            {
                botoes[i].Raiz.SetActive(false);
            }
        }

        DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("ui_rebuild_ms", (Time.realtimeSinceStartup - inicio) * 1000f);
    }

    void GarantirTextoEstado()
    {
        if (painelMestre == null || textoEstado != null)
        {
            return;
        }

        GameObject txtObj = new GameObject("Texto_Aviso");
        txtObj.transform.SetParent(painelMestre.transform, false);

        LayoutElement le = txtObj.AddComponent<LayoutElement>();
        le.minHeight = 30;

        textoEstado = txtObj.AddComponent<Text>();

        Font fonte = null;
        try { fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (fonte == null) fonte = Font.CreateDynamicFontFromOSFont("Arial", 12);
        textoEstado.font = fonte;

        textoEstado.alignment = TextAnchor.MiddleCenter;
        textoEstado.color = Color.gray;
        textoEstado.fontSize = 12;
    }

    void GarantirSlot(int index)
    {
        while (botoes.Count <= index)
        {
            botoes.Add(CriarSlot());
        }
    }

    BotaoSlot CriarSlot()
    {
        GameObject btnObj = new GameObject("Btn_Comando");
        btnObj.transform.SetParent(painelMestre.transform, false);

        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.minHeight = 40;
        le.preferredHeight = 40;
        le.flexibleWidth = 1;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.35f, 0.65f);

        Button btn = btnObj.AddComponent<Button>();
        MenuComandoInteligenteBotaoBinding binding = btnObj.AddComponent<MenuComandoInteligenteBotaoBinding>();
        binding.menu = this;
        btn.onClick.AddListener(binding.Executar);

        GameObject txtObj = new GameObject("Texto");
        txtObj.transform.SetParent(btnObj.transform, false);

        Text t = txtObj.AddComponent<Text>();

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

        return new BotaoSlot
        {
            Raiz = btnObj,
            Botao = btn,
            Imagem = img,
            Texto = t,
            Binding = binding
        };
    }

    void AtualizarSlot(BotaoSlot slot, ComandoMenu comando)
    {
        if (slot == null || slot.Raiz == null)
        {
            return;
        }

        slot.Raiz.SetActive(true);
        slot.Binding.comando = comando;
        slot.Binding.comandoInterno = ComandoInterno.Nenhum;
        slot.Binding.comandoLabel = ResolverTextoComando(comando);
        slot.Texto.text = slot.Binding.comandoLabel;
        slot.Raiz.name = "Btn_" + slot.Binding.comandoLabel;
        if (slot.Imagem != null)
        {
            slot.Imagem.color = new Color(0.2f, 0.35f, 0.65f);
        }
    }

    void AtualizarSlotInterno(BotaoSlot slot, ComandoInterno comandoInterno, string label, Color cor)
    {
        if (slot == null || slot.Raiz == null)
        {
            return;
        }

        slot.Raiz.SetActive(true);
        slot.Binding.comando = null;
        slot.Binding.comandoInterno = comandoInterno;
        slot.Binding.comandoLabel = label;
        slot.Texto.text = label;
        slot.Raiz.name = "Btn_" + label;
        if (slot.Imagem != null)
        {
            slot.Imagem.color = cor;
        }
    }

    string ResolverTextoComando(ComandoMenu comando)
    {
        if (comando == null)
        {
            return "--";
        }

        string titulo = comando.tituloBotao;
        if (string.IsNullOrWhiteSpace(titulo) || titulo.Trim() == "Novo Comando")
        {
            return comando.name;
        }

        return titulo;
    }

    string ResolverTextoEstado()
    {
        bool encontrou = false;
        bool estadoBasePassivo = false;
        bool misto = false;

        for (int i = 0; i < selecionados.Count; i++)
        {
            GameObject selecionado = selecionados[i];
            if (selecionado == null)
            {
                continue;
            }

            ControleUnidade controle = selecionado.GetComponent<ControleUnidade>();
            bool passivo;
            string descricao;
            if (controle != null && controle.TryObterEstadoCombate(out passivo, out descricao))
            {
                if (descricao == "MISTO")
                {
                    misto = true;
                    break;
                }

                if (!encontrou)
                {
                    encontrou = true;
                    estadoBasePassivo = passivo;
                    continue;
                }

                if (estadoBasePassivo != passivo)
                {
                    misto = true;
                    break;
                }
            }
        }

        if (!encontrou)
        {
            return "ESTADO: --";
        }

        if (misto)
        {
            return "ESTADO: MISTO";
        }

        return estadoBasePassivo ? "ESTADO: PASSIVO" : "ESTADO: ATIVO";
    }

    bool ComandoEhCobertoPorInterno(ComandoMenu comando)
    {
        if (comando == null)
        {
            return false;
        }

        string texto = (" " + ResolverTextoComando(comando) + " " + comando.name + " ")
            .ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ");
        return texto.Contains("passivo")
            || texto.Contains(" ativo ")
            || texto.Contains("comandoativo")
            || texto.Contains("patrul")
            || texto.Contains("seguir");
    }

    internal void ExecutarComandoInterno(ComandoInterno comandoInterno, string label, List<GameObject> snapshot)
    {
        if (snapshot == null || snapshot.Count == 0)
        {
            return;
        }

        DiagnosticoDesempenhoJogo.RegistrarEvento("UI", "Click comando: " + label);

        switch (comandoInterno)
        {
            case ComandoInterno.Passivo:
                DefinirModoCombate(snapshot, false);
                break;
            case ComandoInterno.Ativo:
                DefinirModoCombate(snapshot, true);
                break;
            case ComandoInterno.Patrulhar:
                IniciarModoPatrulha(snapshot);
                break;
            case ComandoInterno.Seguir:
                IniciarModoSeguir(snapshot);
                break;
        }
    }

    void DefinirModoCombate(List<GameObject> snapshot, bool ativo)
    {
        int aplicados = 0;
        for (int i = 0; i < snapshot.Count; i++)
        {
            GameObject unidade = snapshot[i];
            if (unidade == null)
            {
                continue;
            }

            ControleUnidade controle = unidade.GetComponent<ControleUnidade>();
            if (controle != null && controle.DefinirModoCombate(ativo))
            {
                aplicados++;
                continue;
            }

            Helicoptero helicoptero = unidade.GetComponent<Helicoptero>();
            if (helicoptero != null)
            {
                helicoptero.modoCombateAtivo = ativo;
                aplicados++;
            }
        }

        Debug.Log($"[MenuComandoInteligente] Modo {(ativo ? "ATIVO" : "PASSIVO")} aplicado a {aplicados} unidades.");
    }

    void IniciarModoPatrulha(List<GameObject> snapshot)
    {
        DesenharLinhasOrdem desenhador = Object.FindFirstObjectByType<DesenharLinhasOrdem>();
        if (desenhador == null)
        {
            Debug.LogWarning("DesenharLinhasOrdem nao encontrado na cena. Nao foi possivel entrar no modo patrulha.");
            return;
        }

        desenhador.IniciarModoPatrulha(snapshot);
    }

    void IniciarModoSeguir(List<GameObject> snapshot)
    {
        DesenharLinhasOrdem desenhador = Object.FindFirstObjectByType<DesenharLinhasOrdem>();
        if (desenhador == null)
        {
            Debug.LogWarning("DesenharLinhasOrdem nao encontrado na cena. Nao foi possivel entrar no modo seguir.");
            return;
        }

        desenhador.IniciarModoSeguir(snapshot);
    }

    internal List<GameObject> CriarSnapshotSelecao()
    {
        List<GameObject> snapshot = new List<GameObject>(selecionados.Count);
        for (int i = 0; i < selecionados.Count; i++)
        {
            GameObject go = selecionados[i];
            if (go != null)
            {
                snapshot.Add(go);
            }
        }

        return snapshot;
    }
}

public sealed class MenuComandoInteligenteBotaoBinding : MonoBehaviour
{
    public MenuComandoInteligente menu;
    public ComandoMenu comando;
    public MenuComandoInteligente.ComandoInterno comandoInterno;
    public string comandoLabel;

    public void Executar()
    {
        if (menu == null)
        {
            return;
        }

        List<GameObject> snapshot = menu.CriarSnapshotSelecao();
        if (snapshot.Count == 0)
        {
            return;
        }

        if (comando != null)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("UI", "Click comando: " + (string.IsNullOrWhiteSpace(comandoLabel) ? comando.name : comandoLabel));
            comando.Executar(snapshot);
            return;
        }

        menu.ExecutarComandoInterno(comandoInterno, comandoLabel, snapshot);
    }
}

internal sealed class BotaoSlot
{
    public GameObject Raiz;
    public Button Botao;
    public Image Imagem;
    public Text Texto;
    public MenuComandoInteligenteBotaoBinding Binding;
}
