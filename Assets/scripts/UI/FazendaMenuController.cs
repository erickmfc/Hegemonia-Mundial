using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Painel operacional da fazenda. A interface foi separada do componente de
/// simulacao para que trocar IMGUI por UI Toolkit nao altere crescimento,
/// replantio, custo ou contabilidade da producao.
/// </summary>
[DisallowMultipleComponent]
public sealed class FazendaMenuController : MonoBehaviour
{
    public static FazendaMenuController Instancia { get; private set; }

    private UIDocument documento;
    private VisualElement root;
    private VisualElement overlay;
    private VisualElement panel;
    private VisualElement lotesContainer;
    private VisualElement catalogoContainer;
    private Label titleLabel;
    private Label subtitleLabel;
    private Label statusLabel;
    private Label resumoComidaLabel;
    private Label resumoLotesLabel;
    private Label resumoSaldoLabel;
    private Label resumoSementesLabel;
    private Button fecharButton;

    private Fazenda fazendaAtual;
    private bool aberto;
    private float proximaAtualizacao;

    public static bool EstaAberto => Instancia != null && Instancia.aberto;

    public static bool AbrirPara(Fazenda fazenda)
    {
        if (QuartelMenuUIController.EntradaGlobalBloqueada)
        {
            return false;
        }

        if (fazenda == null)
        {
            return false;
        }

        if (Instancia == null)
        {
            CriarInstanciaRuntime();
        }

        if (Instancia == null)
        {
            return false;
        }

        Instancia.AbrirInterno(fazenda);
        return true;
    }

    public static void FecharSeAbertoPara(Fazenda fazenda)
    {
        if (Instancia != null && Instancia.fazendaAtual == fazenda)
        {
            Instancia.FecharInterno(false);
        }
    }

    private static void CriarInstanciaRuntime()
    {
        if (Instancia != null)
        {
            return;
        }

        GameObject go = new GameObject("FazendaMenuController");
        DontDestroyOnLoad(go);
        Instancia = go.AddComponent<FazendaMenuController>();
        Instancia.InicializarDocumento();
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        InicializarDocumento();
    }

    private void Update()
    {
        if (!aberto)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Fechar();
            return;
        }

        if (Time.unscaledTime < proximaAtualizacao)
        {
            return;
        }

        proximaAtualizacao = Time.unscaledTime + 0.4f;
        AtualizarPainel();
    }

    private void OnDestroy()
    {
        InteractionModeService.Release(this, InteractionOwner.FarmPanel);
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void InicializarDocumento()
    {
        if (documento != null && root != null)
        {
            return;
        }

        documento = GetComponent<UIDocument>();
        if (documento == null)
        {
            documento = gameObject.AddComponent<UIDocument>();
        }

        documento.panelSettings = ResolverPanelSettings();
        documento.enabled = true;
        root = documento.rootVisualElement;
        if (root == null)
        {
            return;
        }

        ConstruirLayout();
        root.style.display = DisplayStyle.None;
        documento.enabled = false;
    }

    private void ConstruirLayout()
    {
        root.Clear();
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;

        overlay = new VisualElement { name = "farm-menu-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0.015f, 0.025f, 0.02f, 0.90f);
        overlay.style.paddingLeft = 22;
        overlay.style.paddingRight = 22;
        overlay.style.paddingTop = 22;
        overlay.style.paddingBottom = 22;
        root.Add(overlay);

        panel = new VisualElement { name = "farm-menu-panel" };
        panel.style.width = new Length(92, LengthUnit.Percent);
        panel.style.maxWidth = 1220;
        panel.style.height = new Length(90, LengthUnit.Percent);
        panel.style.maxHeight = 860;
        panel.style.paddingLeft = 24;
        panel.style.paddingRight = 24;
        panel.style.paddingTop = 20;
        panel.style.paddingBottom = 20;
        panel.style.backgroundColor = new Color(0.045f, 0.075f, 0.055f, 0.99f);
        AplicarBorda(panel, new Color(0.42f, 0.66f, 0.42f, 0.95f), 2);
        overlay.Add(panel);

        VisualElement header = Linha();
        VisualElement titles = new VisualElement();
        titles.style.flexGrow = 1;
        titleLabel = Label("CENTRO AGRICOLA", 24, Color.white, FontStyle.Bold);
        subtitleLabel = Label(string.Empty, 13, new Color(0.70f, 0.84f, 0.70f), FontStyle.Normal);
        titles.Add(titleLabel);
        titles.Add(subtitleLabel);
        header.Add(titles);

        fecharButton = Botao("FECHAR  X", 120, 40, new Color(0.32f, 0.12f, 0.10f));
        fecharButton.name = "farm-menu-close";
        fecharButton.clicked += Fechar;
        header.Add(fecharButton);
        panel.Add(header);

        statusLabel = Label(string.Empty, 13, new Color(0.94f, 0.78f, 0.38f), FontStyle.Bold);
        statusLabel.style.marginTop = 8;
        statusLabel.style.marginBottom = 12;
        panel.Add(statusLabel);

        VisualElement resumo = Linha();
        resumo.style.flexWrap = Wrap.Wrap;
        resumoComidaLabel = Resumo(resumo, "COMIDA");
        resumoLotesLabel = Resumo(resumo, "LOTES");
        resumoSaldoLabel = Resumo(resumo, "CAIXA");
        resumoSementesLabel = Resumo(resumo, "CATALOGO");
        panel.Add(resumo);

        VisualElement corpo = Linha();
        corpo.style.alignItems = Align.Stretch;
        corpo.style.justifyContent = Justify.FlexStart;
        corpo.style.flexGrow = 1;
        corpo.style.marginTop = 14;

        VisualElement colunaLotes = new VisualElement();
        colunaLotes.style.width = new Length(38, LengthUnit.Percent);
        colunaLotes.style.minWidth = 300;
        colunaLotes.style.marginRight = 12;
        Label loteTitulo = Label("CAMPOS DE PRODUCAO", 15, new Color(0.80f, 0.94f, 0.80f), FontStyle.Bold);
        colunaLotes.Add(loteTitulo);
        lotesContainer = new ScrollView(ScrollViewMode.Vertical) { name = "farm-lots-list" };
        lotesContainer.style.flexGrow = 1;
        lotesContainer.style.marginTop = 8;
        colunaLotes.Add(lotesContainer);
        corpo.Add(colunaLotes);

        VisualElement colunaCatalogo = new VisualElement();
        colunaCatalogo.style.flexGrow = 1;
        Label catalogoTitulo = Label("CATALOGO DE CULTURAS", 15, new Color(0.80f, 0.94f, 0.80f), FontStyle.Bold);
        colunaCatalogo.Add(catalogoTitulo);
        catalogoContainer = new ScrollView(ScrollViewMode.Vertical) { name = "farm-crops-list" };
        catalogoContainer.style.flexGrow = 1;
        catalogoContainer.style.marginTop = 8;
        colunaCatalogo.Add(catalogoContainer);
        corpo.Add(colunaCatalogo);
        panel.Add(corpo);

        overlay.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.target == overlay)
            {
                Fechar();
                evt.StopPropagation();
            }
        });
        panel.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
    }

    private void AbrirInterno(Fazenda fazenda)
    {
        InicializarDocumento();
        if (root == null)
        {
            return;
        }

        if (FabricaMineriosMenuController.EstaAberto)
        {
            FabricaMineriosMenuController.Instancia.FecharParaOutraInterface();
        }

        MenuConstrucao menuConstrucao = FindFirstObjectByType<MenuConstrucao>();
        if (menuConstrucao != null && MenuConstrucao.EstaAberto)
        {
            menuConstrucao.AlternarMenu(false);
        }

        fazendaAtual = fazenda;
        aberto = true;
        proximaAtualizacao = 0f;
        documento.enabled = true;
        root.style.display = DisplayStyle.Flex;
        InteractionModeService.Request(
            this,
            InteractionOwner.FarmPanel,
            new InteractionPolicy
            {
                bloqueiaSelecao = true,
                bloqueiaOrdemMundo = true,
                bloqueiaRotacaoCamera = false,
                consomeLMB = true,
                consomeRMB = true
            },
            "Painel da fazenda aberto");
        AtualizarPainel();
    }

    private void Fechar()
    {
        FecharInterno(true);
    }

    // Usado quando um modal exclusivo, como o Quartel, assume o foco.
    // Fecha apenas a interface e preserva o estado operacional da fazenda.
    public void FecharParaOutraInterface()
    {
        FecharInterno(false);
    }

    private void FecharInterno(bool encerrarEstado)
    {
        if (encerrarEstado && fazendaAtual != null)
        {
            fazendaAtual.EncerrarEstadoDoMenu();
        }

        aberto = false;
        fazendaAtual = null;
        if (root != null)
        {
            root.style.display = DisplayStyle.None;
        }
        if (documento != null)
        {
            documento.enabled = false;
        }
        InteractionModeService.Release(this, InteractionOwner.FarmPanel);
    }

    private void AtualizarPainel()
    {
        if (fazendaAtual == null)
        {
            FecharInterno(false);
            return;
        }

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        int lotesOcupados = 0;
        if (fazendaAtual.lote1Ocupado) lotesOcupados++;
        if (fazendaAtual.lote2Ocupado) lotesOcupados++;
        if (fazendaAtual.lote3Comprado && fazendaAtual.lote3Ocupado) lotesOcupados++;

        SetTexto(titleLabel, "CENTRO AGRICOLA | " + fazendaAtual.NomeFazendaExibicao.ToUpperInvariant());
        SetTexto(subtitleLabel, "producao alimentar, sementes e lotes | operacao em tempo real");
        SetTexto(statusLabel, "Ciclos continuam ativos com o painel aberto | atualizacao operacional a cada 0,4 s");
        SetTexto(resumoComidaLabel, "Comida  " + (recursos != null ? recursos.comida.ToString("N0") : "-") +
            "  |  +" + fazendaAtual.ComidaPorSegundoAtual.ToString("N1") + "/s");
        SetTexto(resumoLotesLabel, "Lotes  " + lotesOcupados + "/" + (fazendaAtual.lote3Comprado ? 3 : 2) +
            (fazendaAtual.lote3Comprado ? "  |  expansao liberada" : "  |  lote 3 bloqueado"));
        SetTexto(resumoSaldoLabel, "Caixa  " + FormatarDinheiro(recursos != null ? recursos.dinheiro : 0L));
        SetTexto(resumoSementesLabel, "Culturas  " + Mathf.Max(0, fazendaAtual.CatalogoAgricolaCount - 1));

        RenderizarLotes();
        RenderizarCatalogo();
    }

    private void RenderizarLotes()
    {
        if (lotesContainer == null)
        {
            return;
        }

        lotesContainer.Clear();
        for (int lote = 1; lote <= 3; lote++)
        {
            if (lote == 3 && !fazendaAtual.lote3Comprado)
            {
                VisualElement bloqueado = Cartao();
                bloqueado.Add(Label("LOTE 3 | EXPANSAO BLOQUEADA", 16, Color.white, FontStyle.Bold));
                bloqueado.Add(Label("Prepare um novo campo para aumentar a capacidade agricola.", 12,
                    new Color(0.72f, 0.78f, 0.72f), FontStyle.Normal));
                Button comprar = Botao("DESBLOQUEAR  " + FormatarDinheiro(fazendaAtual.custoLote3), 0, 38,
                    new Color(0.17f, 0.36f, 0.18f));
                comprar.style.marginTop = 10;
                comprar.clicked += () =>
                {
                    if (fazendaAtual != null && fazendaAtual.ComprarTerceiroLotePeloMenu())
                    {
                        AtualizarPainel();
                    }
                };
                bloqueado.Add(comprar);
                lotesContainer.Add(bloqueado);
                continue;
            }

            bool ocupado;
            int semente;
            float progresso;
            fazendaAtual.ObterEstadoLote(lote, out ocupado, out semente, out progresso);
            VisualElement card = Cartao();
            VisualElement cabecalho = Linha();
            cabecalho.Add(Label("LOTE " + lote, 16, Color.white, FontStyle.Bold));
            Label estado = Label(ocupado ? "EM PRODUCAO" : "DISPONIVEL", 11,
                ocupado ? new Color(0.46f, 0.92f, 0.58f) : new Color(0.82f, 0.86f, 0.72f), FontStyle.Bold);
            cabecalho.Add(estado);
            card.Add(cabecalho);

            Fazenda.RegistoColheita cultura = fazendaAtual.ObterCultura(semente);
            if (!ocupado || cultura == null)
            {
                card.Add(Label("Terreno aravel vazio. Escolha uma cultura no catalogo ao lado.", 12,
                    new Color(0.70f, 0.78f, 0.70f), FontStyle.Normal));
            }
            else
            {
                float percentual = cultura.tempoCrescimento > 0f
                    ? Mathf.Clamp01(progresso / cultura.tempoCrescimento)
                    : 0f;
                card.Add(Label(cultura.nome + "  |  +" + cultura.lucroGerado + " comida por ciclo", 13,
                    Color.white, FontStyle.Bold));
                card.Add(Label("Crescimento " + (percentual * 100f).ToString("F1") + "%  |  " +
                    cultura.tempoCrescimento.ToString("F0") + " s por ciclo", 12,
                    new Color(0.84f, 0.88f, 0.82f), FontStyle.Normal));
                card.Add(BarraProgresso(percentual));
                Button liberar = Botao("LIBERAR LOTE", 0, 32, new Color(0.34f, 0.18f, 0.12f));
                liberar.style.marginTop = 8;
                int loteCapturado = lote;
                liberar.clicked += () =>
                {
                    if (fazendaAtual != null)
                    {
                        fazendaAtual.LiberarLotePeloMenu(loteCapturado);
                        AtualizarPainel();
                    }
                };
                card.Add(liberar);
            }

            lotesContainer.Add(card);
        }
    }

    private void RenderizarCatalogo()
    {
        if (catalogoContainer == null)
        {
            return;
        }

        catalogoContainer.Clear();
        for (int indice = 1; indice < fazendaAtual.CatalogoAgricolaCount; indice++)
        {
            Fazenda.RegistoColheita cultura = fazendaAtual.ObterCultura(indice);
            if (cultura == null)
            {
                continue;
            }

            VisualElement card = Cartao();
            VisualElement topo = Linha();
            topo.Add(Label(cultura.nome, 16, Color.white, FontStyle.Bold));
            topo.Add(Label("SEMENTE", 11, new Color(0.78f, 0.90f, 0.72f), FontStyle.Bold));
            card.Add(topo);
            card.Add(Label("Custo " + FormatarDinheiro(cultura.custoSemente) +
                "  |  ciclo " + cultura.tempoCrescimento.ToString("F0") + " s  |  " +
                cultura.diasParaSafra + " dias de safra", 12,
                new Color(0.82f, 0.87f, 0.80f), FontStyle.Normal));
            float porSegundo = cultura.tempoCrescimento > 0f ? cultura.lucroGerado / cultura.tempoCrescimento : 0f;
            card.Add(Label("Producao " + cultura.lucroGerado + " comida/ciclo  |  ritmo " +
                porSegundo.ToString("F2") + "/s", 12, new Color(0.93f, 0.78f, 0.40f), FontStyle.Normal));

            VisualElement acoes = new VisualElement();
            acoes.style.flexDirection = FlexDirection.Row;
            acoes.style.flexWrap = Wrap.Wrap;
            for (int lote = 1; lote <= 3; lote++)
            {
                bool ocupado;
                int semente;
                float progresso;
                bool loteDisponivel = fazendaAtual.ObterEstadoLote(lote, out ocupado, out semente, out progresso) && !ocupado;
                if (lote == 3 && !fazendaAtual.lote3Comprado)
                {
                    loteDisponivel = false;
                }

                int loteCapturado = lote;
                Button plantar = Botao("PLANTAR LOTE " + lote, 142, 32, new Color(0.16f, 0.36f, 0.20f));
                plantar.style.marginTop = 8;
                plantar.style.marginRight = 6;
                plantar.SetEnabled(loteDisponivel);
                plantar.clicked += () =>
                {
                    if (fazendaAtual != null && fazendaAtual.PlantarSementePeloMenu(loteCapturado, indice))
                    {
                        AtualizarPainel();
                    }
                };
                acoes.Add(plantar);
            }
            card.Add(acoes);
            catalogoContainer.Add(card);
        }
    }

    private static VisualElement BarraProgresso(float percentual)
    {
        VisualElement trilho = new VisualElement();
        trilho.style.height = 10;
        trilho.style.marginTop = 8;
        trilho.style.backgroundColor = new Color(0.10f, 0.16f, 0.11f);
        VisualElement preenchimento = new VisualElement();
        preenchimento.style.height = 10;
        preenchimento.style.width = new Length(Mathf.Clamp01(percentual) * 100f, LengthUnit.Percent);
        preenchimento.style.backgroundColor = new Color(0.32f, 0.76f, 0.38f);
        trilho.Add(preenchimento);
        return trilho;
    }

    private static VisualElement Cartao()
    {
        VisualElement card = new VisualElement();
        card.style.paddingLeft = 12;
        card.style.paddingRight = 12;
        card.style.paddingTop = 11;
        card.style.paddingBottom = 11;
        card.style.marginBottom = 8;
        card.style.backgroundColor = new Color(0.075f, 0.13f, 0.085f, 0.96f);
        AplicarBorda(card, new Color(0.20f, 0.34f, 0.22f, 0.85f), 1);
        return card;
    }

    private static VisualElement Linha()
    {
        VisualElement linha = new VisualElement();
        linha.style.flexDirection = FlexDirection.Row;
        linha.style.alignItems = Align.Center;
        linha.style.justifyContent = Justify.SpaceBetween;
        return linha;
    }

    private static Label Resumo(VisualElement pai, string texto)
    {
        Label label = Label(texto, 12, new Color(0.84f, 0.91f, 0.84f), FontStyle.Bold);
        label.style.flexGrow = 1;
        label.style.minWidth = 190;
        label.style.marginRight = 7;
        label.style.marginBottom = 6;
        label.style.paddingLeft = 10;
        label.style.paddingRight = 10;
        label.style.paddingTop = 9;
        label.style.paddingBottom = 9;
        label.style.backgroundColor = new Color(0.075f, 0.12f, 0.08f);
        pai.Add(label);
        return label;
    }

    private static Label Label(string texto, int tamanho, Color cor, FontStyle estilo)
    {
        Label label = new Label(texto ?? string.Empty);
        label.style.fontSize = tamanho;
        label.style.color = cor;
        label.style.unityFontStyleAndWeight = estilo;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static Button Botao(string texto, float largura, float altura, Color cor)
    {
        Button botao = new Button { text = texto };
        if (largura > 0f)
        {
            botao.style.width = largura;
        }
        botao.style.flexGrow = largura > 0f ? 0 : 1;
        botao.style.height = altura;
        botao.style.backgroundColor = cor;
        botao.style.color = Color.white;
        botao.style.unityFontStyleAndWeight = FontStyle.Bold;
        return botao;
    }

    private static void AplicarBorda(VisualElement elemento, Color cor, float espessura)
    {
        elemento.style.borderTopWidth = espessura;
        elemento.style.borderBottomWidth = espessura;
        elemento.style.borderLeftWidth = espessura;
        elemento.style.borderRightWidth = espessura;
        elemento.style.borderTopColor = cor;
        elemento.style.borderBottomColor = cor;
        elemento.style.borderLeftColor = cor;
        elemento.style.borderRightColor = cor;
    }

    private static void SetTexto(Label label, string texto)
    {
        if (label != null)
        {
            label.text = texto ?? string.Empty;
        }
    }

    private static string FormatarDinheiro(long valor)
    {
        return ValoresDefinitivosHegemonia.FormatarDinheiro(valor);
    }

    private PanelSettings ResolverPanelSettings()
    {
        UIDocument[] documentos = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < documentos.Length; i++)
        {
            UIDocument outro = documentos[i];
            if (outro == null || outro == documento || outro.panelSettings == null)
            {
                continue;
            }

            return outro.panelSettings;
        }

        return ScriptableObject.CreateInstance<PanelSettings>();
    }
}
