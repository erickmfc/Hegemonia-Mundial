using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public sealed class FabricaMineriosMenuController : MonoBehaviour
{
    private const int MaxHistoricoConsultadoNoPainel = 96;
    private const string UxmlResourcePath = "FactoryIndustryMenu/FactoryIndustryMenu";
    private const string UssResourcePath = "FactoryIndustryMenu/FactoryIndustryMenu";

    private static readonly TipoRecursoExtracao[] RecursosBase =
    {
        TipoRecursoExtracao.Ferro,
        TipoRecursoExtracao.Cobre,
        TipoRecursoExtracao.Bauxita,
        TipoRecursoExtracao.Titanio,
        TipoRecursoExtracao.Uranio
    };

    public static FabricaMineriosMenuController Instancia { get; private set; }

    private UIDocument documento;
    private VisualElement root;
    private VisualElement overlay;
    private VisualElement panel;
    private Label titleLabel;
    private Label subtitleLabel;
    private Label statusLabel;
    private Label resumoDiaLabel;
    private Label resumoSemanaLabel;
    private Label resumoMesLabel;
    private Label resumoEstoqueLabel;
    private Label resumoValorLabel;
    private VisualElement destaqueMinerio;
    private Label destaqueNomeLabel;
    private Label destaqueEstadoLabel;
    private Label destaqueResumoLabel;
    private Label destaqueFluxoLabel;
    private VisualElement destaqueMetricas;
    private ScrollView listaMinerios;
    private Button fecharButton;

    private Fabrica fabricaAtual;
    private GerenciadorExtracoes gerenciadorAtual;
    private TipoRecursoExtracao? mineralSelecionado;
    private bool aberto;
    private float proximaAtualizacao;
    private string ultimaAssinaturaLista = string.Empty;

    public static bool EstaAberto
    {
        get { return Instancia != null && Instancia.aberto; }
    }

    public static bool AbrirPara(Fabrica fabrica)
    {
        if (fabrica == null || !fabrica.PossuiPainelIndustrial)
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

        Instancia.AbrirInterno(fabrica);
        return true;
    }

    private static void CriarInstanciaRuntime()
    {
        if (Instancia != null)
        {
            return;
        }

        GameObject go = new GameObject("FabricaMineriosMenu");
        DontDestroyOnLoad(go);
        Instancia = go.AddComponent<FabricaMineriosMenuController>();
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

        proximaAtualizacao = Time.unscaledTime + 1f;
        AtualizarPainel();
    }

    private void OnDestroy()
    {
        if (Instancia == this)
        {
            InteractionModeService.Release(this, InteractionOwner.FactoryIndustryPanel);
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

        documento.enabled = true;
        documento.panelSettings = ResolverPanelSettings();
        documento.visualTreeAsset = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
        root = documento.rootVisualElement;
        if (root == null)
        {
            return;
        }

        if (documento.visualTreeAsset == null)
        {
            Debug.LogWarning("[FabricaMineriosMenu] Layout em Resources nao encontrado; usando painel runtime.");
            ConstruirLayoutRuntime();
        }
        else
        {
            StyleSheet folha = Resources.Load<StyleSheet>(UssResourcePath);
            if (folha != null && !root.styleSheets.Contains(folha))
            {
                root.styleSheets.Add(folha);
            }

            overlay = root.Q<VisualElement>("factory-menu-overlay");
            panel = root.Q<VisualElement>("factory-menu-panel");
            titleLabel = root.Q<Label>("factory-menu-title");
            subtitleLabel = root.Q<Label>("factory-menu-subtitle");
            statusLabel = root.Q<Label>("factory-menu-status");
            resumoDiaLabel = root.Q<Label>("factory-summary-day");
            resumoSemanaLabel = root.Q<Label>("factory-summary-week");
            resumoMesLabel = root.Q<Label>("factory-summary-month");
            resumoEstoqueLabel = root.Q<Label>("factory-summary-stock");
            resumoValorLabel = root.Q<Label>("factory-summary-value");
            destaqueMinerio = root.Q<VisualElement>("factory-mineral-highlight");
            destaqueNomeLabel = root.Q<Label>("factory-highlight-name");
            destaqueEstadoLabel = root.Q<Label>("factory-highlight-state");
            destaqueResumoLabel = root.Q<Label>("factory-highlight-summary");
            destaqueFluxoLabel = root.Q<Label>("factory-highlight-flow");
            destaqueMetricas = root.Q<VisualElement>("factory-highlight-metrics");
            listaMinerios = root.Q<ScrollView>("factory-minerals-list");
            fecharButton = root.Q<Button>("factory-menu-close");
        }

        if (overlay != null)
        {
            overlay.RegisterCallback<MouseDownEvent>(OnOverlayMouseDown);
        }

        if (panel != null)
        {
            panel.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
        }

        if (fecharButton != null)
        {
            fecharButton.clicked += Fechar;
        }

        root.style.display = DisplayStyle.None;
        documento.enabled = false;
    }

    private void ConstruirLayoutRuntime()
    {
        root.Clear();
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;

        overlay = new VisualElement { name = "factory-menu-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0.015f, 0.025f, 0.035f, 0.90f);
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.paddingLeft = 24;
        overlay.style.paddingRight = 24;
        overlay.style.paddingTop = 24;
        overlay.style.paddingBottom = 24;
        root.Add(overlay);

        panel = new VisualElement { name = "factory-menu-panel" };
        panel.style.width = new Length(92, LengthUnit.Percent);
        panel.style.maxWidth = 1180;
        panel.style.height = new Length(90, LengthUnit.Percent);
        panel.style.maxHeight = 840;
        panel.style.paddingLeft = 26;
        panel.style.paddingRight = 26;
        panel.style.paddingTop = 22;
        panel.style.paddingBottom = 22;
        panel.style.backgroundColor = new Color(0.055f, 0.075f, 0.09f, 0.99f);
        panel.style.borderTopWidth = 2;
        panel.style.borderBottomWidth = 2;
        panel.style.borderLeftWidth = 2;
        panel.style.borderRightWidth = 2;
        Color borda = new Color(0.52f, 0.64f, 0.68f, 0.9f);
        panel.style.borderTopColor = borda;
        panel.style.borderBottomColor = borda;
        panel.style.borderLeftColor = borda;
        panel.style.borderRightColor = borda;
        overlay.Add(panel);

        VisualElement cabecalho = NovaLinha();
        VisualElement titulos = new VisualElement();
        titulos.style.flexGrow = 1;
        titleLabel = NovaLabel("COMPLEXO INDUSTRIAL DE EXTRAÇÃO", 24, Color.white, FontStyle.Bold);
        subtitleLabel = NovaLabel(string.Empty, 13, new Color(0.66f, 0.76f, 0.78f), FontStyle.Normal);
        titulos.Add(titleLabel);
        titulos.Add(subtitleLabel);
        fecharButton = new Button { text = "FECHAR  ×", name = "factory-menu-close" };
        fecharButton.style.width = 126;
        fecharButton.style.height = 42;
        fecharButton.style.backgroundColor = new Color(0.38f, 0.12f, 0.10f);
        fecharButton.style.color = Color.white;
        fecharButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        cabecalho.Add(titulos);
        cabecalho.Add(fecharButton);
        panel.Add(cabecalho);

        statusLabel = NovaLabel(string.Empty, 13, new Color(0.93f, 0.72f, 0.36f), FontStyle.Bold);
        statusLabel.style.marginTop = 8;
        statusLabel.style.marginBottom = 12;
        panel.Add(statusLabel);

        VisualElement resumo = NovaLinha();
        resumo.style.flexWrap = Wrap.Wrap;
        resumoDiaLabel = AdicionarResumo(resumo, "SAÍDA DO DIA");
        resumoSemanaLabel = AdicionarResumo(resumo, "RITMO SEMANAL");
        resumoMesLabel = AdicionarResumo(resumo, "ACUMULADO MENSAL");
        resumoEstoqueLabel = AdicionarResumo(resumo, "ESTOQUE MINERAL");
        resumoValorLabel = AdicionarResumo(resumo, "VALOR PATRIMONIAL");
        panel.Add(resumo);

        destaqueMinerio = new VisualElement { name = "factory-mineral-highlight" };
        destaqueMinerio.style.marginTop = 14;
        destaqueMinerio.style.marginBottom = 12;
        destaqueMinerio.style.paddingLeft = 16;
        destaqueMinerio.style.paddingRight = 16;
        destaqueMinerio.style.paddingTop = 12;
        destaqueMinerio.style.paddingBottom = 12;
        destaqueMinerio.style.backgroundColor = new Color(0.085f, 0.12f, 0.135f);
        VisualElement destaqueTopo = NovaLinha();
        destaqueNomeLabel = NovaLabel(string.Empty, 21, Color.white, FontStyle.Bold);
        destaqueEstadoLabel = NovaLabel(string.Empty, 13, new Color(0.46f, 0.86f, 0.64f), FontStyle.Bold);
        destaqueTopo.Add(destaqueNomeLabel);
        destaqueTopo.Add(destaqueEstadoLabel);
        destaqueResumoLabel = NovaLabel(string.Empty, 13, new Color(0.78f, 0.84f, 0.85f), FontStyle.Normal);
        destaqueFluxoLabel = NovaLabel(string.Empty, 13, new Color(0.93f, 0.72f, 0.36f), FontStyle.Normal);
        destaqueMetricas = NovaLinha();
        destaqueMetricas.style.flexWrap = Wrap.Wrap;
        destaqueMinerio.Add(destaqueTopo);
        destaqueMinerio.Add(destaqueResumoLabel);
        destaqueMinerio.Add(destaqueFluxoLabel);
        destaqueMinerio.Add(destaqueMetricas);
        panel.Add(destaqueMinerio);

        listaMinerios = new ScrollView(ScrollViewMode.Vertical) { name = "factory-minerals-list" };
        listaMinerios.style.flexGrow = 1;
        listaMinerios.style.backgroundColor = new Color(0.025f, 0.04f, 0.05f, 0.65f);
        listaMinerios.style.paddingLeft = 8;
        listaMinerios.style.paddingRight = 8;
        listaMinerios.style.paddingTop = 8;
        listaMinerios.style.paddingBottom = 8;
        panel.Add(listaMinerios);
    }

    private static VisualElement NovaLinha()
    {
        VisualElement linha = new VisualElement();
        linha.style.flexDirection = FlexDirection.Row;
        linha.style.alignItems = Align.Center;
        linha.style.justifyContent = Justify.SpaceBetween;
        return linha;
    }

    private static Label NovaLabel(string texto, int tamanho, Color cor, FontStyle estilo)
    {
        Label label = new Label(texto);
        label.style.fontSize = tamanho;
        label.style.color = cor;
        label.style.unityFontStyleAndWeight = estilo;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static Label AdicionarResumo(VisualElement pai, string textoInicial)
    {
        Label label = NovaLabel(textoInicial, 12, new Color(0.82f, 0.87f, 0.88f), FontStyle.Bold);
        label.style.minWidth = 180;
        label.style.flexGrow = 1;
        label.style.marginRight = 8;
        label.style.marginBottom = 6;
        label.style.paddingLeft = 10;
        label.style.paddingRight = 10;
        label.style.paddingTop = 9;
        label.style.paddingBottom = 9;
        label.style.backgroundColor = new Color(0.09f, 0.115f, 0.125f);
        pai.Add(label);
        return label;
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

    private void OnOverlayMouseDown(MouseDownEvent evt)
    {
        if (evt.target == overlay)
        {
            Fechar();
            evt.StopPropagation();
        }
    }

    private void AbrirInterno(Fabrica fabrica)
    {
        if (fabrica == null)
        {
            return;
        }

        InicializarDocumento();
        if (root == null)
        {
            return;
        }

        fabricaAtual = fabrica;
        gerenciadorAtual = fabrica.GerenciadorExtracoesLocal;
        aberto = true;
        proximaAtualizacao = 0f;
        ultimaAssinaturaLista = string.Empty;
        mineralSelecionado = mineralSelecionado.HasValue ? mineralSelecionado : TipoRecursoExtracao.Ferro;
        documento.enabled = true;
        root.style.display = DisplayStyle.Flex;

        MenuConstrucao menuConstrucao = FindFirstObjectByType<MenuConstrucao>();
        if (menuConstrucao != null && MenuConstrucao.EstaAberto)
        {
            menuConstrucao.AlternarMenu(false);
        }

        InteractionModeService.Request(
            this,
            InteractionOwner.FactoryIndustryPanel,
            new InteractionPolicy
            {
                bloqueiaSelecao = true,
                bloqueiaOrdemMundo = true,
                bloqueiaRotacaoCamera = false,
                consomeLMB = true,
                consomeRMB = true
            },
            "Painel da fabrica industrial aberto");

        AtualizarPainel();
    }

    private void Fechar()
    {
        aberto = false;
        fabricaAtual = null;
        gerenciadorAtual = null;

        if (root != null)
        {
            root.style.display = DisplayStyle.None;
        }

        if (documento != null)
        {
            documento.enabled = false;
        }

        InteractionModeService.Release(this, InteractionOwner.FactoryIndustryPanel);
    }

    // Usado quando outro painel operacional precisa assumir o foco da UI.
    public void FecharParaOutraInterface()
    {
        Fechar();
    }

    private void AtualizarPainel()
    {
        if (root == null)
        {
            return;
        }

        if (fabricaAtual == null || gerenciadorAtual == null)
        {
            SetTexto(statusLabel, "Fabrica sem gerenciador de extracao.");
            LimparLista();
            return;
        }

        List<MineralSnapshot> snapshots = ConstruirSnapshots();
        float totalDia = snapshots.Sum(s => s.extraidoDia);
        float totalSemana = snapshots.Sum(s => s.extraidoSemana);
        float totalMes = snapshots.Sum(s => s.extraidoMes);
        float totalEstoque = snapshots.Sum(s => s.estoqueToneladas);
        double totalValorEstoque = snapshots.Sum(s => s.valorEstoqueTotal);
        int ordensAtivas = gerenciadorAtual.ordens.Count(o =>
            o != null && (o.Estado == EstadoOrdem.Ativa || o.Estado == EstadoOrdem.Aguardando || o.Estado == EstadoOrdem.ConcluindoCiclo));
        MineralSnapshot destaque = ObterSnapshotDestaque(snapshots);

        SetTexto(titleLabel, "CENTRAL INDUSTRIAL");
        SetTexto(subtitleLabel, fabricaAtual.name + "  |  extracao, energia e estoque em uma tela");
        SetTexto(statusLabel, "Ordens ativas " + ordensAtivas +
            "  |  proximo fechamento em " + Mathf.CeilToInt(gerenciadorAtual.TempoAteProximoDia) + "s");
        SetTexto(resumoDiaLabel, "Saida do dia  " + FormatarToneladas(totalDia));
        SetTexto(resumoSemanaLabel, "Ritmo semanal  " + FormatarToneladas(totalSemana));
        SetTexto(resumoMesLabel, "Acumulado mensal  " + FormatarToneladas(totalMes));
        SetTexto(resumoEstoqueLabel, "Estoque mineral  " + FormatarToneladas(totalEstoque));
        SetTexto(resumoValorLabel, "Valor patrimonial  " + FormatarMoeda(totalValorEstoque));

        RenderizarDestaque(destaque);
        string assinatura = ConstruirAssinaturaLista(snapshots);
        if (!string.Equals(assinatura, ultimaAssinaturaLista, StringComparison.Ordinal))
        {
            ultimaAssinaturaLista = assinatura;
            RenderizarLista(snapshots);
        }
    }

    private string ConstruirAssinaturaLista(List<MineralSnapshot> snapshots)
    {
        string assinatura = mineralSelecionado.HasValue ? mineralSelecionado.Value.ToString() : "-";
        for (int i = 0; i < snapshots.Count; i++)
        {
            MineralSnapshot s = snapshots[i];
            assinatura += "|" + s.tipo + ":" + s.estado + ":" + s.ordensConfiguradas + ":" +
                s.estoqueToneladas.ToString("F1", CultureInfo.InvariantCulture) + ":" +
                s.extraidoDia.ToString("F1", CultureInfo.InvariantCulture) + ":" +
                s.extraidoSemana.ToString("F1", CultureInfo.InvariantCulture) + ":" +
                s.extraidoMes.ToString("F1", CultureInfo.InvariantCulture);
        }

        return assinatura;
    }

    private MineralSnapshot ObterSnapshotDestaque(List<MineralSnapshot> snapshots)
    {
        if (snapshots == null || snapshots.Count == 0)
        {
            return default;
        }

        if (mineralSelecionado.HasValue)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].tipo == mineralSelecionado.Value)
                {
                    return snapshots[i];
                }
            }
        }

        MineralSnapshot melhor = snapshots
            .OrderByDescending(s => s.extraidoMes)
            .ThenByDescending(s => s.estoqueToneladas)
            .First();
        mineralSelecionado = melhor.tipo;
        return melhor;
    }

    private List<MineralSnapshot> ConstruirSnapshots()
    {
        Dictionary<TipoRecursoExtracao, List<OrdemExtracao>> ordensPorTipo = new Dictionary<TipoRecursoExtracao, List<OrdemExtracao>>();
        for (int i = 0; i < RecursosBase.Length; i++)
        {
            ordensPorTipo[RecursosBase[i]] = new List<OrdemExtracao>();
        }

        if (gerenciadorAtual != null && gerenciadorAtual.ordens != null)
        {
            for (int i = 0; i < gerenciadorAtual.ordens.Count; i++)
            {
                OrdemExtracao ordem = gerenciadorAtual.ordens[i];
                if (ordem == null || ordem.dados == null)
                {
                    continue;
                }

                if (!ordensPorTipo.ContainsKey(ordem.dados.tipoExtracao))
                {
                    ordensPorTipo[ordem.dados.tipoExtracao] = new List<OrdemExtracao>();
                }

                ordensPorTipo[ordem.dados.tipoExtracao].Add(ordem);
            }
        }

        List<MineralSnapshot> resultado = new List<MineralSnapshot>(ordensPorTipo.Count);
        foreach (KeyValuePair<TipoRecursoExtracao, List<OrdemExtracao>> par in ordensPorTipo)
        {
            resultado.Add(CriarSnapshot(par.Key, par.Value));
        }

        return resultado.OrderBy(s => s.ordemExibicao).ToList();
    }

    private MineralSnapshot CriarSnapshot(TipoRecursoExtracao tipo, List<OrdemExtracao> ordens)
    {
        DadosTipoMinerio dados = ordens.FirstOrDefault(o => o != null && o.dados != null)?.dados;
        float estoque = ConsultarEstoque(tipo);
        int diaAtual = gerenciadorAtual != null ? gerenciadorAtual.DiaAtual : 0;
        float valorPorKg = ObterValorKg(tipo);
        float valorPorTon = valorPorKg * 1000f;

        float extraidoDia = 0f;
        float extraidoSemana = 0f;
        float extraidoMes = 0f;
        float mediaCiclo = 0f;
        float mediaPorDia = 0f;
        int ciclos = 0;
        int diasPorCiclo = 0;
        int custoDinheiroCiclo = 0;
        int custoEnergiaCiclo = 0;
        string estado = "SEM ORDEM";
        string proximaEntrega = "-";

        for (int i = 0; i < ordens.Count; i++)
        {
            OrdemExtracao ordem = ordens[i];
            if (ordem == null || ordem.dados == null)
            {
                continue;
            }

            diasPorCiclo = Mathf.Max(diasPorCiclo, ordem.dados.duracaoEmDias);
            custoDinheiroCiclo += ordem.dados.custoDinheiro;
            custoEnergiaCiclo += ordem.dados.custoEnergia;

            if (estado == "SEM ORDEM")
            {
                estado = ordem.Estado.ToString().ToUpperInvariant();
                proximaEntrega = ordem.ProximaEntregaFormatada();
            }

            IReadOnlyList<RegistroExtracao> historico = ordem.Historico;
            int primeiroHistorico = Mathf.Max(0, historico.Count - MaxHistoricoConsultadoNoPainel);
            for (int h = primeiroHistorico; h < historico.Count; h++)
            {
                RegistroExtracao registro = historico[h];
                mediaCiclo += registro.quantidadeProduzida;
                ciclos++;

                if (registro.dia == diaAtual)
                {
                    extraidoDia += registro.quantidadeProduzida;
                }

                if (registro.dia >= diaAtual - 6)
                {
                    extraidoSemana += registro.quantidadeProduzida;
                }

                if (registro.dia >= diaAtual - 29)
                {
                    extraidoMes += registro.quantidadeProduzida;
                }
            }
        }

        if (ciclos > 0)
        {
            mediaCiclo /= ciclos;
        }
        else if (dados != null)
        {
            mediaCiclo = (dados.producaoMinima + dados.producaoMaxima) * 0.5f;
        }

        if (diasPorCiclo <= 0 && dados != null)
        {
            diasPorCiclo = Mathf.Max(1, dados.duracaoEmDias);
        }

        if (diasPorCiclo > 0)
        {
            mediaPorDia = mediaCiclo / diasPorCiclo;
        }

        MineralSnapshot snapshot = new MineralSnapshot();
        snapshot.tipo = tipo;
        snapshot.nome = dados != null && !string.IsNullOrWhiteSpace(dados.nomeRecurso) ? dados.nomeRecurso : NomeTipo(tipo);
        snapshot.ordemExibicao = (int)tipo;
        snapshot.estoqueToneladas = estoque;
        snapshot.extraidoDia = extraidoDia;
        snapshot.extraidoSemana = extraidoSemana;
        snapshot.extraidoMes = extraidoMes;
        snapshot.mediaCiclo = mediaCiclo;
        snapshot.mediaPorDia = mediaPorDia;
        snapshot.custoDinheiroCiclo = custoDinheiroCiclo;
        snapshot.custoEnergiaCiclo = custoEnergiaCiclo;
        snapshot.valorKg = valorPorKg;
        snapshot.valorTonelada = valorPorTon;
        snapshot.valorEstoqueTotal = estoque * valorPorTon;
        snapshot.diasCiclo = diasPorCiclo;
        snapshot.estado = estado;
        snapshot.proximaEntrega = proximaEntrega;
        snapshot.kgEstoque = estoque * 1000f;
        snapshot.ordensConfiguradas = ordens.Count;
        return snapshot;
    }

    private float ConsultarEstoque(TipoRecursoExtracao tipo)
    {
        if (GerenciadorArmazens.Instancia == null || GerenciadorArmazens.Instancia.armazemRecursos == null)
        {
            return 0f;
        }

        return GerenciadorArmazens.Instancia.armazemRecursos.ConsultarMinerio(tipo);
    }

    private float ObterValorKg(TipoRecursoExtracao tipo)
    {
        switch (tipo)
        {
            case TipoRecursoExtracao.Ferro: return 0.35f;
            case TipoRecursoExtracao.Cobre: return 0.92f;
            case TipoRecursoExtracao.Bauxita: return 0.28f;
            case TipoRecursoExtracao.Titanio: return 4.80f;
            case TipoRecursoExtracao.Uranio: return 18.00f;
            default: return 1f;
        }
    }

    private void RenderizarLista(List<MineralSnapshot> snapshots)
    {
        if (listaMinerios == null)
        {
            return;
        }

        listaMinerios.Clear();
        for (int i = 0; i < snapshots.Count; i++)
        {
            listaMinerios.Add(CriarCardMinerio(snapshots[i], snapshots[i].tipo == mineralSelecionado));
        }
    }

    private void RenderizarDestaque(MineralSnapshot snapshot)
    {
        if (destaqueMinerio == null)
        {
            return;
        }

        destaqueMinerio.EnableInClassList("is-idle", snapshot.ordensConfiguradas <= 0);
        SetTexto(destaqueNomeLabel, snapshot.nome);
        SetTexto(destaqueEstadoLabel, snapshot.estado);
        SetTexto(destaqueResumoLabel,
            "Estoque " + FormatarToneladas(snapshot.estoqueToneladas) +
            "  |  " + snapshot.kgEstoque.ToString("N0", CultureInfo.InvariantCulture) + " kg armazenados  |  " +
            "ordens " + snapshot.ordensConfiguradas);
        SetTexto(destaqueFluxoLabel,
            "Hoje " + FormatarToneladas(snapshot.extraidoDia) +
            "  |  Semana " + FormatarToneladas(snapshot.extraidoSemana) +
            "  |  Mes " + FormatarToneladas(snapshot.extraidoMes) +
            "  |  proxima entrega " + snapshot.proximaEntrega);

        if (destaqueMetricas != null)
        {
            destaqueMetricas.Clear();
            destaqueMetricas.Add(CriarMetrica("Valor por kg", FormatarMoeda(snapshot.valorKg), "accent-copper"));
            destaqueMetricas.Add(CriarMetrica("Valor por tonelada", FormatarMoeda(snapshot.valorTonelada), "accent-gold"));
            destaqueMetricas.Add(CriarMetrica("Valor do estoque", FormatarMoeda(snapshot.valorEstoqueTotal), "accent-steel"));
            destaqueMetricas.Add(CriarMetrica("Media por ciclo", FormatarToneladas(snapshot.mediaCiclo), "accent-smoke"));
            destaqueMetricas.Add(CriarMetrica("Media por dia", FormatarToneladas(snapshot.mediaPorDia), "accent-smoke"));
            destaqueMetricas.Add(CriarMetrica("Dias por ciclo", snapshot.diasCiclo > 0 ? snapshot.diasCiclo + " dias" : "-", "accent-smoke"));
            destaqueMetricas.Add(CriarMetrica("Custo por ciclo", FormatarMoeda(snapshot.custoDinheiroCiclo), "accent-copper"));
            destaqueMetricas.Add(CriarMetrica("Energia por ciclo", snapshot.custoEnergiaCiclo.ToString("N0", CultureInfo.InvariantCulture), "accent-gold"));
            AdicionarAcoesOrdem(destaqueMetricas, snapshot, false);
        }
    }

    private VisualElement CriarCardMinerio(MineralSnapshot snapshot, bool selecionado)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("factory-mineral-card");
        card.EnableInClassList("is-selected", selecionado);

        VisualElement topo = new VisualElement();
        topo.AddToClassList("factory-mineral-top");

        Label nome = new Label(snapshot.nome);
        nome.AddToClassList("factory-mineral-name");

        Label estado = new Label(snapshot.estado);
        estado.AddToClassList("factory-mineral-state");
        if (snapshot.estado.Contains("ATIVA") || snapshot.estado.Contains("CONCLUINDO"))
        {
            estado.AddToClassList("is-good");
        }
        else if (snapshot.estado.Contains("SEM") || snapshot.estado.Contains("BLOQUE"))
        {
            estado.AddToClassList("is-bad");
        }
        else
        {
            estado.AddToClassList("is-warn");
        }

        topo.Add(nome);
        topo.Add(estado);

        Label descricao = new Label("Estoque " + FormatarToneladas(snapshot.estoqueToneladas) +
            "  |  entrega " + snapshot.proximaEntrega +
            "  |  ordens " + snapshot.ordensConfiguradas);
        descricao.AddToClassList("factory-mineral-subtitle");

        VisualElement grid = new VisualElement();
        grid.AddToClassList("factory-mineral-grid");
        grid.Add(CriarMetrica("Hoje", FormatarToneladas(snapshot.extraidoDia), "compact"));
        grid.Add(CriarMetrica("Semana", FormatarToneladas(snapshot.extraidoSemana), "compact"));
        grid.Add(CriarMetrica("Mes", FormatarToneladas(snapshot.extraidoMes), "compact"));
        grid.Add(CriarMetrica("Valor / kg", FormatarMoeda(snapshot.valorKg), "compact"));

        VisualElement acoes = new VisualElement();
        acoes.style.flexDirection = FlexDirection.Row;
        acoes.style.flexWrap = Wrap.Wrap;
        acoes.style.marginTop = 4;
        AdicionarAcoesOrdem(acoes, snapshot, true);

        card.Add(topo);
        card.Add(descricao);
        card.Add(grid);
        card.Add(acoes);
        card.style.marginBottom = 8;
        card.style.paddingLeft = 14;
        card.style.paddingRight = 14;
        card.style.paddingTop = 11;
        card.style.paddingBottom = 11;
        card.style.backgroundColor = selecionado
            ? new Color(0.15f, 0.22f, 0.24f)
            : new Color(0.075f, 0.10f, 0.115f);
        card.RegisterCallback<ClickEvent>(_ =>
        {
            mineralSelecionado = snapshot.tipo;
            ultimaAssinaturaLista = string.Empty;
            AtualizarPainel();
        });
        return card;
    }

    private void AdicionarAcoesOrdem(VisualElement destino, MineralSnapshot snapshot, bool compacto)
    {
        if (destino == null || gerenciadorAtual == null || gerenciadorAtual.ordens == null)
        {
            return;
        }

        int encontradas = 0;
        for (int indice = 0; indice < gerenciadorAtual.ordens.Count; indice++)
        {
            OrdemExtracao ordem = gerenciadorAtual.ordens[indice];
            if (ordem == null || ordem.dados == null || ordem.dados.tipoExtracao != snapshot.tipo)
            {
                continue;
            }

            encontradas++;
            EstadoOrdem estado = ordem.Estado;
            string texto;
            bool habilitado = true;
            Color cor;
            if (estado == EstadoOrdem.Bloqueada)
            {
                texto = compacto ? "AUTORIZAR" : "AUTORIZAR ORDEM";
                cor = new Color(0.42f, 0.30f, 0.10f);
            }
            else if (estado == EstadoOrdem.Pausada)
            {
                texto = compacto ? "RETOMAR" : "RETOMAR ORDEM";
                cor = new Color(0.14f, 0.38f, 0.20f);
            }
            else if (estado == EstadoOrdem.Aguardando || estado == EstadoOrdem.Ativa)
            {
                texto = compacto ? "PAUSAR" : "PAUSAR ORDEM";
                cor = new Color(0.35f, 0.22f, 0.10f);
            }
            else
            {
                texto = compacto ? "AGUARDAR" : "AGUARDAR RECURSOS";
                cor = new Color(0.20f, 0.22f, 0.23f);
                habilitado = false;
            }

            int indiceCapturado = indice;
            Button acao = new Button { text = texto };
            acao.style.height = compacto ? 28 : 34;
            acao.style.minWidth = compacto ? 108 : 142;
            acao.style.marginRight = 6;
            acao.style.marginTop = 6;
            acao.style.backgroundColor = cor;
            acao.style.color = Color.white;
            acao.style.unityFontStyleAndWeight = FontStyle.Bold;
            acao.SetEnabled(habilitado);
            acao.clicked += () => ExecutarAcaoOrdem(indiceCapturado);
            destino.Add(acao);
        }

        if (encontradas == 0)
        {
            Label semOrdem = new Label(compacto ? "sem ordem" : "Nenhuma ordem configurada para este mineral.");
            semOrdem.style.fontSize = compacto ? 10 : 12;
            semOrdem.style.color = new Color(0.62f, 0.68f, 0.68f);
            semOrdem.style.marginTop = 8;
            destino.Add(semOrdem);
        }
    }

    private void ExecutarAcaoOrdem(int indice)
    {
        if (gerenciadorAtual == null || gerenciadorAtual.ordens == null ||
            indice < 0 || indice >= gerenciadorAtual.ordens.Count)
        {
            return;
        }

        OrdemExtracao ordem = gerenciadorAtual.ordens[indice];
        if (ordem == null)
        {
            return;
        }

        switch (ordem.Estado)
        {
            case EstadoOrdem.Bloqueada:
                gerenciadorAtual.AutorizarOrdem(indice);
                break;
            case EstadoOrdem.Pausada:
                gerenciadorAtual.RetomarOrdem(indice);
                break;
            case EstadoOrdem.Aguardando:
            case EstadoOrdem.Ativa:
                gerenciadorAtual.PausarOrdem(indice);
                break;
        }

        ultimaAssinaturaLista = string.Empty;
        AtualizarPainel();
    }

    private static VisualElement CriarMetrica(string rotulo, string valor, string variantClass = null)
    {
        VisualElement box = new VisualElement();
        box.AddToClassList("factory-metric");
        if (!string.IsNullOrWhiteSpace(variantClass))
        {
            box.AddToClassList(variantClass);
        }

        Label labelRotulo = new Label(rotulo);
        labelRotulo.AddToClassList("factory-metric-label");

        Label labelValor = new Label(valor);
        labelValor.AddToClassList("factory-metric-value");

        box.Add(labelRotulo);
        box.Add(labelValor);
        box.style.minWidth = 128;
        box.style.flexGrow = 1;
        box.style.marginRight = 6;
        box.style.marginTop = 6;
        box.style.paddingLeft = 8;
        box.style.paddingRight = 8;
        box.style.paddingTop = 6;
        box.style.paddingBottom = 6;
        box.style.backgroundColor = new Color(0.045f, 0.065f, 0.075f);
        labelRotulo.style.fontSize = 10;
        labelRotulo.style.color = new Color(0.60f, 0.69f, 0.71f);
        labelValor.style.fontSize = 13;
        labelValor.style.color = Color.white;
        labelValor.style.unityFontStyleAndWeight = FontStyle.Bold;
        return box;
    }

    private void LimparLista()
    {
        if (listaMinerios != null)
        {
            listaMinerios.Clear();
        }
    }

    private static void SetTexto(Label label, string texto)
    {
        if (label != null)
        {
            label.text = texto ?? string.Empty;
        }
    }

    private static string FormatarToneladas(float valor)
    {
        return valor.ToString("N0", CultureInfo.InvariantCulture) + " t";
    }

    private static string FormatarMoeda(double valor)
    {
        return "$ " + valor.ToString("N2", CultureInfo.InvariantCulture);
    }

    private static string NomeTipo(TipoRecursoExtracao tipo)
    {
        switch (tipo)
        {
            case TipoRecursoExtracao.Ferro: return "Ferro";
            case TipoRecursoExtracao.Cobre: return "Cobre";
            case TipoRecursoExtracao.Bauxita: return "Bauxita";
            case TipoRecursoExtracao.Titanio: return "Titanio";
            case TipoRecursoExtracao.Uranio: return "Uranio";
            default: return tipo.ToString();
        }
    }

    private struct MineralSnapshot
    {
        public TipoRecursoExtracao tipo;
        public string nome;
        public int ordemExibicao;
        public float estoqueToneladas;
        public float kgEstoque;
        public float extraidoDia;
        public float extraidoSemana;
        public float extraidoMes;
        public float mediaCiclo;
        public float mediaPorDia;
        public int custoDinheiroCiclo;
        public int custoEnergiaCiclo;
        public float valorKg;
        public float valorTonelada;
        public double valorEstoqueTotal;
        public int diasCiclo;
        public string estado;
        public string proximaEntrega;
        public int ordensConfiguradas;
    }
}
