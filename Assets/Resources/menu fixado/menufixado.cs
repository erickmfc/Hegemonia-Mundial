using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class MenuFixadoController : MonoBehaviour
{
    private static MenuFixadoController _instance;

    private UIDocument uiDocument;
    private VisualElement root;
    private bool uiPronta = false;

    // Labels
    private Label lblCountryVal, lblDateVal;
    private Label lblMoneyVal, lblMoneyBonus, lblCurrencyVal, lblGoldVal;
    private Label lblHappyVal, lblPopVal, lblDeadVal, lblJobsVal;
    private Label lblOilVal, lblOilBonus;
    private Label lblSteelVal, lblSteelBonus;
    private Label lblFoodVal, lblFoodBonus;
    private Label lblEnergyVal, lblEnergyBonus;
    private Label lblStorageVal;
    private Label lblMilitaryVal, lblMilitaryBonus;

    // Central de acontecimentos do pais
    private Button statusToggle, notificationClose;
    private Button tabToday, tabEvents, tabHelp;
    private VisualElement notificationPanel, notificationHelp;
    private ScrollView notificationList;
    private VisualElement decisionBanner;
    private Label decisionTitle, decisionMessage;
    private Button decisionAction, decisionClose;
    private StatusNotificacao decisionNotification;
    private StatusNotificacao alertaDecisaoFechado;
    private bool? ultimaPrefeituraOperacional;
    private bool? ultimaSituacaoComida;
    private bool? ultimaSituacaoEnergia;
    private bool? ultimaSituacaoCombustivel;
    private bool? ultimaSituacaoMunicao;
    private bool? ultimaSituacaoManutencao;
    private bool? ultimaSituacaoTerritorio;
    private int abaNotificacoes;
    private bool painelNotificacaoAberto;
    private bool statusTemNovidades;
    private bool statusPulsoAtivo;
    private bool ignorarNovidadesIniciais = true;
    private IVisualElementScheduledItem statusPulseSchedule;

    private bool _activeInScene = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (FindFirstObjectByType<MenuFixadoController>() != null) return;

        VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("menu fixado/menufixado");
        if (uxml == null) return;

        PanelSettings ps = Resources.Load<PanelSettings>("PanelSettings") ?? ScriptableObject.CreateInstance<PanelSettings>();
        ps.sortingOrder = 100;

        GameObject go = new GameObject("[HUD_MenuFixado]");
        DontDestroyOnLoad(go);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        doc.visualTreeAsset = uxml;
        go.AddComponent<MenuFixadoController>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        uiDocument = GetComponent<UIDocument>();
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckSceneVisibility(SceneManager.GetActiveScene());
        LimparDuplicadosNaCena();
        StartCoroutine(SetupUI());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        DesregistrarEventos();
        StatusNotificacaoFeed.OnAlterado -= AoAlterarFeedNotificacoes;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckSceneVisibility(scene);
        LimparDuplicadosNaCena();
        if (uiPronta)
        {
            DesregistrarEventos();
            RegistrarEventos();
            UpdateUI();
        }
    }

    private void LimparDuplicadosNaCena()
    {
        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var doc in docs)
        {
            if (doc != uiDocument && doc.visualTreeAsset != null && doc.visualTreeAsset.name.ToLower().Contains("menufixado"))
            {
                Destroy(doc.gameObject);
            }
        }
    }

    private void CheckSceneVisibility(Scene scene)
    {
        string nome = scene.name.ToLower();
        _activeInScene = !(nome.Contains("menu") && !nome.Contains("game") && !nome.Contains("fase") && !nome.Contains("mapa"));
        if (root != null) root.style.display = _activeInScene ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private IEnumerator SetupUI()
    {
        yield return new WaitUntil(() => uiDocument.rootVisualElement != null);
        yield return null;

        root = uiDocument.rootVisualElement;

        StyleSheet uss = Resources.Load<StyleSheet>("menu fixado/menufixado");
        if (uss != null) root.styleSheets.Add(uss);

        lblCountryVal = root.Q<Label>("lbl-country-val");
        lblDateVal = root.Q<Label>("lbl-date-val");
        
        lblMoneyVal = root.Q<Label>("lbl-money-val");
        lblMoneyBonus = root.Q<Label>("lbl-money-bonus");
        lblCurrencyVal = root.Q<Label>("lbl-currency-val");
        lblGoldVal = root.Q<Label>("lbl-gold-val");
        
        lblHappyVal = root.Q<Label>("lbl-happy-val");
        lblPopVal = root.Q<Label>("lbl-pop-val");
        lblDeadVal = root.Q<Label>("lbl-dead-val");
        lblJobsVal = root.Q<Label>("lbl-jobs-val");
        
        lblOilVal = root.Q<Label>("lbl-oil-val");
        lblOilBonus = root.Q<Label>("lbl-oil-bonus");
        
        lblSteelVal = root.Q<Label>("lbl-steel-val");
        lblSteelBonus = root.Q<Label>("lbl-steel-bonus");
        
        lblFoodVal = root.Q<Label>("lbl-food-val");
        lblFoodBonus = root.Q<Label>("lbl-food-bonus");
        
        lblEnergyVal = root.Q<Label>("lbl-energy-val");
        lblEnergyBonus = root.Q<Label>("lbl-energy-bonus");
        
        lblStorageVal = root.Q<Label>("lbl-storage-val");
        
        lblMilitaryVal = root.Q<Label>("lbl-military-val");
        lblMilitaryBonus = root.Q<Label>("lbl-military-bonus");

        ConfigurarPainelNotificacoes();

        ConfigurarTooltips();
        uiPronta = true;
        CheckSceneVisibility(SceneManager.GetActiveScene());
        RegistrarEventos();
        if (StatusNotificacaoFeed.Itens.Count == 0)
        {
            StatusNotificacaoFeed.Publicar(
                "MUNDO",
                "Boletim mundial ativo",
                "Compras militares, acordos e mudanças importantes das nações aparecerão aqui.",
                StatusNotificacaoSeveridade.Info);
        }
        UpdateUI();
        AtualizarAlertaPrincipal();
        ignorarNovidadesIniciais = false;
    }

    private void ConfigurarTooltips()
    {
        if (lblMoneyVal != null) lblMoneyVal.tooltip = "Saldo atual do tesouro nacional.";
        if (lblCountryVal != null) lblCountryVal.tooltip = "Pais atualmente controlado.";
        if (lblCurrencyVal != null) lblCurrencyVal.tooltip = "Moeda nacional e relacao com a moeda lider.";
        if (lblGoldVal != null) lblGoldVal.tooltip = "Reserva de ouro estrategica.";
        if (lblHappyVal != null) lblHappyVal.tooltip = "Felicidade media da populacao.";
        if (lblPopVal != null) lblPopVal.tooltip = "Populacao civil atual versus capacidade habitacional.";
        if (lblDeadVal != null) lblDeadVal.tooltip = "Mortes acumuladas por guerra, fome e crises.";
        if (lblJobsVal != null) lblJobsVal.tooltip = "Empregos ocupados versus vagas disponiveis.";
        if (lblFoodVal != null) lblFoodVal.tooltip = "Estoque nacional de comida.";
        if (lblFoodBonus != null) lblFoodBonus.tooltip = "Saldo liquido de producao e consumo de comida.";
        if (lblEnergyVal != null) lblEnergyVal.tooltip = "Energia consumida versus energia disponivel.";
        if (lblEnergyBonus != null) lblEnergyBonus.tooltip = "Percentual de uso ou deficit energetico.";
        if (lblStorageVal != null) lblStorageVal.tooltip = "Ocupacao do armazem nacional.";
        if (lblMilitaryVal != null) lblMilitaryVal.tooltip = "Soldados ativos em servico.";
        if (lblMilitaryBonus != null) lblMilitaryBonus.tooltip = "Recrutaveis e reservistas disponiveis.";
    }

    private void RegistrarEventos()
    {
        DesregistrarEventos();
        StatusNotificacaoFeed.OnAlterado += AoAlterarFeedNotificacoes;
        GerenciadorTempo.GarantirInstancia();
        if (GerenciadorRecursos.Instancia != null) GerenciadorRecursos.Instancia.OnRecursosAtualizados += UpdateUI;
        if (CensoImperial.Instancia != null) CensoImperial.Instancia.OnCensoAtualizado += UpdateUI;
        if (GerenciadorArmazens.Instancia != null) GerenciadorArmazens.Instancia.OnArmazensAtualizados += UpdateUI;
        if (GerenciadorTempo.Instancia != null) GerenciadorTempo.Instancia.OnDataAlterada += UpdateUI;
    }

    private void DesregistrarEventos()
    {
        StatusNotificacaoFeed.OnAlterado -= AoAlterarFeedNotificacoes;
        if (GerenciadorRecursos.Instancia != null) GerenciadorRecursos.Instancia.OnRecursosAtualizados -= UpdateUI;
        if (CensoImperial.Instancia != null) CensoImperial.Instancia.OnCensoAtualizado -= UpdateUI;
        if (GerenciadorArmazens.Instancia != null) GerenciadorArmazens.Instancia.OnArmazensAtualizados -= UpdateUI;
        if (GerenciadorTempo.Instancia != null) GerenciadorTempo.Instancia.OnDataAlterada -= UpdateUI;
    }

    private void ConfigurarPainelNotificacoes()
    {
        statusToggle = root.Q<Button>("status-toggle");
        notificationClose = root.Q<Button>("notification-close");
        tabToday = root.Q<Button>("tab-today");
        tabEvents = root.Q<Button>("tab-events");
        tabHelp = root.Q<Button>("tab-help");
        notificationPanel = root.Q<VisualElement>("notification-panel");
        notificationList = root.Q<ScrollView>("notification-list");
        decisionBanner = root.Q<VisualElement>("decision-banner");
        decisionTitle = root.Q<Label>("decision-title");
        decisionMessage = root.Q<Label>("decision-message");
        decisionAction = root.Q<Button>("decision-action");
        decisionClose = root.Q<Button>("decision-close");
        notificationHelp = root.Q<VisualElement>("notification-help");

        // O HUD usa um container superior deslocado para centralizar a barra.
        // O painel fica em camada propria, entao a ancoragem inline evita que
        // esse deslocamento empurre o feed para fora da tela.
        if (notificationPanel != null)
        {
            notificationPanel.style.alignSelf = Align.FlexStart;
            notificationPanel.style.left = 72f;
            notificationPanel.style.marginLeft = 0f;
            notificationPanel.style.top = 66f;
        }

        if (statusToggle != null) statusToggle.clicked += AlternarPainelNotificacoes;
        if (notificationClose != null) notificationClose.clicked += FecharPainelNotificacoes;
        if (tabToday != null) tabToday.clicked += () => SelecionarAbaNotificacoes(0);
        if (tabEvents != null) tabEvents.clicked += () => SelecionarAbaNotificacoes(1);
        if (tabHelp != null) tabHelp.clicked += () => SelecionarAbaNotificacoes(2);
        if (decisionAction != null) decisionAction.clicked += ExecutarAcaoDecisao;
        // UI Toolkit usa Action sem parametros; o metodo tambem aceita a
        // opcao de descartar internamente, entao adaptamos a assinatura aqui.
        if (decisionClose != null) decisionClose.clicked += () => FecharPainelFeebackDecisao();

        if (statusToggle != null)
        {
            statusPulseSchedule = statusToggle.schedule.Execute(AtualizarPulsoStatus).Every(450);
        }

        SelecionarAbaNotificacoes(0);
        FecharPainelNotificacoes();
    }

    private void AlternarPainelNotificacoes()
    {
        if (notificationPanel == null) return;

        bool fechado = notificationPanel.ClassListContains("is-hidden");
        notificationPanel.EnableInClassList("is-hidden", !fechado);
        if (fechado)
        {
            painelNotificacaoAberto = true;
            MarcarNotificacoesComoVistas();
            AtualizarNotificacoes();
        }
        else
        {
            painelNotificacaoAberto = false;
        }
    }

    private void FecharPainelNotificacoes()
    {
        painelNotificacaoAberto = false;
        if (notificationPanel != null) notificationPanel.AddToClassList("is-hidden");
    }

    private void AoAlterarFeedNotificacoes()
    {
        if (!ignorarNovidadesIniciais && !painelNotificacaoAberto && StatusNotificacaoFeed.PossuiNovidadeNaoDescartada)
        {
            statusTemNovidades = true;
            statusToggle?.AddToClassList("has-news");
        }
        else if (!StatusNotificacaoFeed.PossuiNovidadeNaoDescartada)
        {
            statusTemNovidades = false;
            statusToggle?.RemoveFromClassList("has-news");
            statusToggle?.RemoveFromClassList("status-pulse");
        }

        AtualizarNotificacoes();
        AtualizarAlertaPrincipal();
    }

    private void ExecutarAcaoDecisao()
    {
        if (decisionNotification == null || decisionNotification.Acao == null) return;

        // Fecha o aviso atual antes da acao, pois a acao pode publicar outro
        // estado imediatamente (e esse novo estado nao deve ser escondido).
        StatusNotificacao acao = decisionNotification;
        FecharPainelFeebackDecisao(false);
        alertaDecisaoFechado = null;
        acao.Acao.Invoke();
    }

    private void FecharPainelFeebackDecisao(bool descartar = true)
    {
        if (decisionNotification != null)
        {
            alertaDecisaoFechado = decisionNotification;
            // O fechamento vale para o estado inteiro do alerta, mesmo que o
            // sistema atualize a mensagem no proximo ciclo.
            if (descartar) StatusNotificacaoFeed.Descartar(decisionNotification);
        }
        if (decisionBanner != null) decisionBanner.AddToClassList("is-hidden");
        decisionNotification = null;
    }

    private void AtualizarAlertaPrincipal()
    {
        if (decisionBanner == null) return;

        StatusNotificacao escolhido = null;
        int melhorPeso = -1;
        // O feed guarda historico, mas o banner deve avaliar apenas o estado
        // mais recente de cada categoria. Sem isso, um alerta antigo de energia
        // continuava vencendo mesmo depois de a usina normalizar a rede.
        HashSet<string> categoriasVistas = new HashSet<string>();
        IList<StatusNotificacao> itens = StatusNotificacaoFeed.Itens;
        for (int i = 0; i < itens.Count; i++)
        {
            StatusNotificacao item = itens[i];
            if (item == null || !categoriasVistas.Add(item.Categoria)) continue;
            if (item.Descartada) continue;
            if (!item.TemAcao) continue;
            // O banner sobre a cena fica reservado para risco critico. Avisos
            // informativos e deficits cobertos pela reserva permanecem apenas
            // no historico da aba Status.
            if (item.Severidade != StatusNotificacaoSeveridade.Critical) continue;

            int peso = item.Severidade == StatusNotificacaoSeveridade.Critical ? 3
                : item.Severidade == StatusNotificacaoSeveridade.Warning ? 2 : 1;
            if (peso > melhorPeso)
            {
                escolhido = item;
                melhorPeso = peso;
            }
        }

        decisionNotification = escolhido;
        if (escolhido == null)
        {
            alertaDecisaoFechado = null;
            decisionBanner.AddToClassList("is-hidden");
            return;
        }

        // O X fecha apenas o alerta atual. Um novo estado publicado pelo jogo
        // continua podendo aparecer sem reabrir o mesmo aviso a cada frame.
        if (ReferenceEquals(escolhido, alertaDecisaoFechado))
        {
            decisionBanner.AddToClassList("is-hidden");
            return;
        }

        decisionTitle.text = escolhido.Titulo;
        decisionMessage.text = escolhido.Mensagem;
        decisionAction.text = escolhido.AcaoTexto;
        decisionBanner.RemoveFromClassList("is-hidden");
    }

    private void MarcarNotificacoesComoVistas()
    {
        statusTemNovidades = false;
        statusPulsoAtivo = false;
        if (statusToggle == null) return;

        statusToggle.RemoveFromClassList("has-news");
        statusToggle.RemoveFromClassList("status-pulse");
    }

    private void AtualizarPulsoStatus()
    {
        if (statusToggle == null) return;

        if (!statusTemNovidades)
        {
            statusPulsoAtivo = false;
            statusToggle.RemoveFromClassList("status-pulse");
            return;
        }

        statusPulsoAtivo = !statusPulsoAtivo;
        statusToggle.EnableInClassList("status-pulse", statusPulsoAtivo);
    }

    private void SelecionarAbaNotificacoes(int aba)
    {
        abaNotificacoes = Mathf.Clamp(aba, 0, 2);
        if (tabToday != null) tabToday.EnableInClassList("is-active", abaNotificacoes == 0);
        if (tabEvents != null) tabEvents.EnableInClassList("is-active", abaNotificacoes == 1);
        if (tabHelp != null) tabHelp.EnableInClassList("is-active", abaNotificacoes == 2);

        bool ajuda = abaNotificacoes == 2;
        if (notificationList != null) notificationList.style.display = ajuda ? DisplayStyle.None : DisplayStyle.Flex;
        if (notificationHelp != null) notificationHelp.EnableInClassList("is-hidden", !ajuda);
        if (!ajuda) AtualizarNotificacoes();
    }

    private void AtualizarNotificacoes()
    {
        if (notificationList == null || abaNotificacoes == 2) return;

        VisualElement content = notificationList.contentContainer;
        content.Clear();

        int itensExibidos = 0;
        IList<StatusNotificacao> itens = StatusNotificacaoFeed.Itens;
        for (int i = 0; i < itens.Count; i++)
        {
            StatusNotificacao item = itens[i];
            if (item == null) continue;

            bool eventoMundial = item.Categoria == "MUNDO"
                || item.Categoria == "AEROPORTO"
                || item.Categoria == "DIPLOMACIA";
            if (abaNotificacoes == 1 && !eventoMundial) continue;

            content.Add(CriarLinhaNotificacao(item));
            itensExibidos++;
        }

        if (itensExibidos == 0)
        {
            Label vazio = new Label(abaNotificacoes == 1
                ? "Nenhum evento mundial registrado."
                : "Nenhuma notificacao pendente.");
            vazio.AddToClassList("notification-empty");
            content.Add(vazio);
        }
    }

    private VisualElement CriarLinhaNotificacao(StatusNotificacao item)
    {
        VisualElement linha = new VisualElement();
        linha.AddToClassList("notification-item");

        Label severidade = new Label(ObterSimboloNotificacao(item.Severidade));
        severidade.AddToClassList("notification-severity");
        severidade.AddToClassList(item.Severidade.ToString().ToLowerInvariant());
        linha.Add(severidade);

        VisualElement copia = new VisualElement();
        copia.AddToClassList("notification-item-copy");

        Label titulo = new Label("[" + item.Categoria + "] " + item.Titulo);
        titulo.AddToClassList("notification-item-title");
        copia.Add(titulo);

        Label mensagem = new Label(item.Mensagem);
        mensagem.AddToClassList("notification-item-message");
        copia.Add(mensagem);
        linha.Add(copia);

        if (item.TemAcao)
        {
            Button acao = new Button(item.Acao)
            {
                text = item.AcaoTexto,
                tooltip = "Executar a ação recomendada"
            };
            acao.AddToClassList("notification-action");
            linha.Add(acao);
        }

        Label horario = new Label(item.Horario);
        horario.AddToClassList("notification-item-time");
        linha.Add(horario);
        return linha;
    }

    private string ObterSimboloNotificacao(StatusNotificacaoSeveridade severidade)
    {
        switch (severidade)
        {
            case StatusNotificacaoSeveridade.Warning: return "!";
            case StatusNotificacaoSeveridade.Success: return "✓";
            case StatusNotificacaoSeveridade.Critical: return "×";
            default: return "i";
        }
    }

    private void AtualizarAlertasAutomaticos(GerenciadorRecursos recursos, DadosEconomiaPais economia)
    {
        bool prefeituraOperacional = false;
        ComplexoGovernamental[] complexos = FindObjectsByType<ComplexoGovernamental>(FindObjectsSortMode.None);
        for (int i = 0; i < complexos.Length; i++)
        {
            ComplexoGovernamental complexo = complexos[i];
            if (complexo == null) continue;

            IdentidadeUnidade identidade = complexo.GetComponent<IdentidadeUnidade>();
            if (complexo.ehDoJogador || (identidade != null && identidade.teamID == 1))
            {
                prefeituraOperacional = true;
                break;
            }
        }

        if (!ultimaPrefeituraOperacional.HasValue || ultimaPrefeituraOperacional.Value != prefeituraOperacional)
        {
            ultimaPrefeituraOperacional = prefeituraOperacional;
            StatusNotificacaoFeed.Publicar(
                "GOVERNO",
                prefeituraOperacional ? "Prefeitura operacional" : "Prefeitura necessaria",
                prefeituraOperacional
                    ? "A sede do governo esta ativa e pronta para administrar a nação."
                    : "Construa uma Prefeitura para centralizar o governo da nação.",
                prefeituraOperacional ? StatusNotificacaoSeveridade.Success : StatusNotificacaoSeveridade.Warning,
                prefeituraOperacional ? string.Empty : "ABRIR CONSTRUCAO",
                prefeituraOperacional ? null : () => AbrirConstrucao(DadosConstrucao.CategoriaItem.Urbana, "Selecione uma Prefeitura para iniciar a fundacao."));
        }

        // A Prefeitura e a fundacao da partida. Enquanto ela nao existe,
        // mantenha apenas esta orientacao: energia, comida e territorio so
        // fazem sentido depois que o governo foi estabelecido.
        if (!prefeituraOperacional)
        {
            return;
        }

        float consumoComida = economia != null ? Mathf.Max(1f, economia.comidaConsumida) : 1f;
        float reservaCriticaComida = Mathf.Max(10f, consumoComida * 2f);
        // Uma queda de producao nao interrompe o jogador enquanto ainda ha
        // reserva para alguns ciclos. So sinaliza quando a reserva esta perto
        // de acabar, ou ja acabou.
        bool comidaEstavel = recursos != null && recursos.comida > reservaCriticaComida;
        if (!ultimaSituacaoComida.HasValue || ultimaSituacaoComida.Value != comidaEstavel)
        {
            ultimaSituacaoComida = comidaEstavel;
            StatusNotificacaoFeed.Publicar(
                "ECONOMIA",
                comidaEstavel ? "Abastecimento normalizado" : "Atenção ao estoque de comida",
                comidaEstavel
                    ? "A produção atual cobre o consumo nacional."
                    : "A nação precisa de comida: aumente a produção ou providencie importações.",
                comidaEstavel ? StatusNotificacaoSeveridade.Success : StatusNotificacaoSeveridade.Critical,
                comidaEstavel ? string.Empty : "CONSTRUIR FAZENDA",
                comidaEstavel ? null : () => AbrirConstrucao(DadosConstrucao.CategoriaItem.Urbana, "Construa uma Fazenda para recuperar a producao de comida."));
        }

        float deficitEnergia = economia != null ? Mathf.Max(0f, economia.deficitEnergia) : 0f;
        int estruturasSemEnergia = economia != null ? Mathf.Max(0, economia.estruturasSemEnergia) : 0;
        // O retrato economico pode chegar um ciclo atrasado quando uma usina
        // termina de ser construida. Se ja existe reserva positiva e nenhuma
        // estrutura esta marcada sem energia, um residuo de ate 1 MW nao deve
        // manter o alerta critico na tela.
        if (recursos != null && recursos.energia > 0 && estruturasSemEnergia == 0 && deficitEnergia <= 1f)
        {
            deficitEnergia = 0f;
        }
        bool energiaEstavel = deficitEnergia <= 0.5f && estruturasSemEnergia <= 0;
        float consumoEnergiaAtual = economia != null ? Mathf.Max(0f, economia.energiaConsumida) : 0f;
        float producaoEnergiaAtual = economia != null ? Mathf.Max(0f, economia.energiaProduzida) : 0f;
        if (!ultimaSituacaoEnergia.HasValue || ultimaSituacaoEnergia.Value != energiaEstavel)
        {
            ultimaSituacaoEnergia = energiaEstavel;
            StatusNotificacaoFeed.Publicar(
                "ENERGIA",
                energiaEstavel ? "Energia estabilizada" : "Energia insuficiente",
                energiaEstavel
                    ? "A producao atual atende o consumo das estruturas."
                    : "Consumo " + Mathf.CeilToInt(consumoEnergiaAtual) + " MW / producao "
                        + Mathf.CeilToInt(producaoEnergiaAtual) + " MW. Deficit de "
                        + Mathf.CeilToInt(deficitEnergia) + " MW; " + estruturasSemEnergia
                        + " estrutura(s) sem energia.",
                energiaEstavel ? StatusNotificacaoSeveridade.Success : StatusNotificacaoSeveridade.Critical,
                energiaEstavel ? string.Empty : "CONSTRUIR USINA",
                energiaEstavel ? null : () => AbrirConstrucao(DadosConstrucao.CategoriaItem.Energia, "Escolha uma usina para ampliar a geracao."));
        }

        float deficitPetroleo = economia != null ? economia.deficitPetroleo : 0f;
        bool unidadeComCombustivelBaixo = false;
        CombustivelUnidade[] unidadesComCombustivel = FindObjectsByType<CombustivelUnidade>(FindObjectsSortMode.None);
        int teamJogadorCombustivel = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.teamJogador : 1;
        for (int i = 0; i < unidadesComCombustivel.Length; i++)
        {
            CombustivelUnidade combustivel = unidadesComCombustivel[i];
            if (combustivel == null || !combustivel.usaCombustivel || !combustivel.EstaBaixo) continue;
            IdentidadeUnidade identidade = combustivel.GetComponent<IdentidadeUnidade>() ?? combustivel.GetComponentInParent<IdentidadeUnidade>();
            if (identidade == null || identidade.teamID != teamJogadorCombustivel) continue;
            unidadeComCombustivelBaixo = true;
            break;
        }
        bool combustivelEstavel = recursos.petroleo > 0 && deficitPetroleo <= 0.5f && !unidadeComCombustivelBaixo;
        if (!ultimaSituacaoCombustivel.HasValue || ultimaSituacaoCombustivel.Value != combustivelEstavel)
        {
            ultimaSituacaoCombustivel = combustivelEstavel;
            StatusNotificacaoFeed.Publicar(
                "LOGISTICA",
                combustivelEstavel ? "Combustivel disponivel" : "Sem combustivel",
                combustivelEstavel
                    ? "A reserva de petroleo cobre o consumo atual."
                    : unidadeComCombustivelBaixo
                        ? "Uma ou mais unidades estao com combustivel baixo; reabasteca antes de continuar."
                        : "Estoque: " + Mathf.Max(0, recursos.petroleo) + " L; a operacao esta bloqueada por falta de combustivel.",
                combustivelEstavel ? StatusNotificacaoSeveridade.Success : StatusNotificacaoSeveridade.Critical,
                combustivelEstavel ? string.Empty : "ABRIR INFRAESTRUTURA",
                combustivelEstavel ? null : () => AbrirConstrucao(DadosConstrucao.CategoriaItem.Infraestrutura, "Construa producao ou armazenamento de combustivel."));
        }

        SistemaGastosMilitares.GarantirInstancia();
        int municaoDisponivel = 0;
        if (SistemaGastosMilitares.Instancia != null && SistemaGastosMilitares.Instancia.estoques != null)
        {
            int team = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.teamJogador : 1;
            for (int i = 0; i < SistemaGastosMilitares.Instancia.estoques.Count; i++)
            {
                EstoqueMunicaoMilitar estoque = SistemaGastosMilitares.Instancia.estoques[i];
                if (estoque != null && estoque.teamId == team) municaoDisponivel += Mathf.Max(0, estoque.quantidade);
            }
        }
        bool precisaMunicao = economia != null && economia.militaresNecessarios > 0;
        bool municaoEstavel = !precisaMunicao || municaoDisponivel > 0;
        if (!ultimaSituacaoMunicao.HasValue || ultimaSituacaoMunicao.Value != municaoEstavel)
        {
            ultimaSituacaoMunicao = municaoEstavel;
            StatusNotificacaoFeed.Publicar(
                "DEFESA",
                municaoEstavel ? "Municao disponivel" : "Municao insuficiente",
                municaoEstavel
                    ? "O estoque militar permite manter as unidades em operacao."
                    : "Estoque atual: 0 cartuchos; fabrique municao antes de iniciar a proxima operacao.",
                municaoEstavel ? StatusNotificacaoSeveridade.Success : StatusNotificacaoSeveridade.Warning,
                municaoEstavel ? string.Empty : "ABRIR DEFESA",
                municaoEstavel ? null : () => AbrirGoverno("Abra Defesa > Alertas para fabricar municao."));
        }

        bool manutencaoEstavel = economia == null || economia.estruturasContadas <= 0 || economia.saldoOperacional >= 0f;
        if (!ultimaSituacaoManutencao.HasValue || ultimaSituacaoManutencao.Value != manutencaoEstavel)
        {
            ultimaSituacaoManutencao = manutencaoEstavel;
            StatusNotificacaoFeed.Publicar(
                "ORCAMENTO",
                manutencaoEstavel ? "Manutencao coberta" : "Manutencao pressionando o tesouro",
                manutencaoEstavel
                    ? "A receita operacional cobre os custos atuais."
                    : "Custo de manutencao: $" + Mathf.CeilToInt(Mathf.Max(0f, economia.custoManutencao)) + "; o saldo operacional esta negativo.",
                manutencaoEstavel ? StatusNotificacaoSeveridade.Success : StatusNotificacaoSeveridade.Warning,
                manutencaoEstavel ? string.Empty : "ABRIR ORCAMENTO",
                manutencaoEstavel ? null : () => AbrirGoverno("Abra Economia > Orcamento para revisar a manutencao."));
        }

        int marcadoresProprios = 0;
        MarcadorTerritorio[] marcadores = FindObjectsByType<MarcadorTerritorio>(FindObjectsSortMode.None);
        for (int i = 0; i < marcadores.Length; i++)
        {
            MarcadorTerritorio marcador = marcadores[i];
            if (marcador != null && marcador.teamID == 1) marcadoresProprios++;
        }
        bool possuiEstrutura = false;
        IdentidadeUnidade[] identidades = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        for (int i = 0; i < identidades.Length; i++)
        {
            if (identidades[i] != null && identidades[i].teamID == 1 && identidades[i].tipoUnidade == TipoUnidade.Estrutura)
            {
                possuiEstrutura = true;
                break;
            }
        }
        bool territorioEstavel = !possuiEstrutura || marcadoresProprios > 0;
        if (!ultimaSituacaoTerritorio.HasValue || ultimaSituacaoTerritorio.Value != territorioEstavel)
        {
            ultimaSituacaoTerritorio = territorioEstavel;
            StatusNotificacaoFeed.Publicar(
                "TERRITORIO",
                territorioEstavel ? "Territorio sob controle" : "Territorio nao reivindicado",
                territorioEstavel
                    ? "As estruturas nacionais estao dentro de uma area sob sua soberania."
                    : "A construcao esta fora de uma area reivindicada; expanda a fronteira antes de continuar.",
                territorioEstavel ? StatusNotificacaoSeveridade.Success : StatusNotificacaoSeveridade.Warning,
                territorioEstavel ? string.Empty : "ABRIR MAPA",
                territorioEstavel ? null : AbrirMapaEstrategico);
        }
    }

    private void AbrirConstrucao(DadosConstrucao.CategoriaItem categoria, string mensagem)
    {
        if (MenuConstrucao.AbrirCategoria(categoria))
        {
            return;
        }

        HUDAjudaRTS.MostrarMensagemTemporaria(mensagem, 4f);
    }

    private void AbrirGoverno(string mensagem)
    {
        if (MenuGovernoNovoController.GarantirInstancia()
            && MenuGovernoNovoController.Instancia != null)
        {
            MenuGovernoNovoController.Instancia.Abrir(true);
        }
        else
        {
            HUDAjudaRTS.MostrarMensagemTemporaria(mensagem, 4f);
        }
    }

    private void AbrirMapaEstrategico()
    {
        MapaGeralController mapa = FindFirstObjectByType<MapaGeralController>();
        if (mapa != null)
        {
            mapa.AbrirMapaEstrategico();
        }
        else
        {
            HUDAjudaRTS.MostrarMensagemTemporaria("Abra o mapa estrategico com M para localizar uma area sob sua soberania.", 4f);
        }
    }

    private void Update()
    {
        if (!uiPronta || !_activeInScene) return;

        if (Time.frameCount % 30 == 0) RegistrarEventos();

        if (Time.frameCount % 60 == 0) UpdateUI();
    }

    private void UpdateUI()
    {
        if (!uiPronta) return;

        var r = GerenciadorRecursos.Instancia;
        if (r == null) return;

        SistemaGovernoMundial.GarantirInstancia();
        if (SistemaGovernoMundial.Instancia != null)
        {
            SistemaGovernoMundial.Instancia.SincronizarJogador();
        }

        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(SistemaGovernoMundial.Instancia.teamJogador) : null;
        DadosEconomiaPais economia = null;
        if (SistemaEconomiaImoveis.Instancia != null)
        {
            economia = SistemaEconomiaImoveis.Instancia.ObterEconomia(SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.teamJogador : 1);
        }

        SetText(lblCountryVal, pais != null ? pais.nomePais.ToUpper() : "PAÍS");
        SetText(lblCurrencyVal, pais != null ? $"{pais.nomeMoeda.ToUpper()} {pais.cambioComLider:0.00}X" : "$");
        SetText(lblGoldVal, pais != null ? pais.reservaOuro.ToString("N0") : "0");
        AtualizarData();

        SetText(lblMoneyVal, ValoresDefinitivosHegemonia.FormatarDinheiro(r.dinheiro));
        AtualizarBonus(lblMoneyBonus, r.dinheiroPorSegundo, "/s", true);

        SetText(lblOilVal, r.petroleo.ToString("N0"));
        AtualizarBonus(lblOilBonus, r.petroleoPorSegundo, "/s");

        SetText(lblSteelVal, r.aco.ToString("N0"));
        AtualizarBonus(lblSteelBonus, r.acoPorSegundo, "/s");

        SetText(lblFoodVal, r.comida.ToString("N0"));
        if (economia != null)
        {
            float comidaLiquid = economia.comidaProduzida - economia.comidaConsumida;
            AtualizarBonus(lblFoodBonus, comidaLiquid, "/s");
        }
        else
        {
            AtualizarBonus(lblFoodBonus, 0f, "/s");
        }
        
        SetText(lblPopVal, pais != null ? $"{pais.populacaoCivil:N0}/{pais.populacaoMaxima:N0}" : $"{r.populacaoAtual:N0}/{r.populacaoMaxima:N0}");
        SetText(lblDeadVal, pais != null ? pais.mortosAcumulados.ToString("N0") : "0");
        SetColor(lblDeadVal, pais != null && pais.mortosAcumulados > 0 ? new Color(0.90f, 0.30f, 0.30f) : Color.white);
        if (lblPopVal != null && pais != null)
        {
            lblPopVal.tooltip = "Populacao civil: " + pais.populacaoCivil.ToString("N0")
                + "\nPopulacao total: " + pais.populacao.ToString("N0")
                + "\nCapacidade: " + pais.populacaoMaxima.ToString("N0");
        }
        if (lblDeadVal != null && pais != null)
        {
            lblDeadVal.tooltip = "Mortes acumuladas: " + pais.mortosAcumulados.ToString("N0")
                + "\nMortalidade atual: " + pais.mortalidade.ToString("0.0");
        }
        if (lblHappyVal != null && pais != null)
        {
            SetText(lblHappyVal, $"{pais.felicidade:F0}%");
            SetColor(lblHappyVal, pais.felicidade >= 70 ? Color.green : (pais.felicidade >= 40 ? Color.yellow : Color.red));
        }

        if (lblJobsVal != null && economia != null)
        {
            int disponiveis = Mathf.Max(0, economia.empregosDisponiveis);
            int ocupados = Mathf.Max(0, economia.empregosOcupados);
            lblJobsVal.text = $"{ocupados:N0}/{disponiveis:N0}";
            SetColor(lblJobsVal, economia.deficitEmprego > 0 ? Color.yellow : Color.white);
            lblJobsVal.tooltip = "Empregos ocupados: " + ocupados.ToString("N0")
                + "\nVagas disponiveis: " + disponiveis.ToString("N0")
                + "\nDeficit: " + economia.deficitEmprego.ToString("0");
        }

        float consumida = Mathf.Max(0f, pais?.energiaConsumida ?? economia?.energiaConsumida ?? 0f);
        // Estoque acumulado nao e capacidade da rede. O HUD mostra consumo
        // versus producao, os mesmos valores usados para decidir o alerta.
        float produzida = Mathf.Max(0f, economia?.energiaProduzida ?? 0f);
        if (produzida <= 0.01f && pais != null) produzida = Mathf.Max(0f, pais.energiaProduzida);

        SetText(lblEnergyVal, $"{consumida:0}/{produzida:0} MW");
        if (lblEnergyVal != null)
        {
            lblEnergyVal.tooltip = "Energia: consumo atual / producao disponivel da rede (MW). Estoque: "
                + r.energia.ToString("N0") + " MW.";
        }

        if (produzida > 0.01f)
        {
            float uso = Mathf.Clamp((consumida / produzida) * 100f, 0f, 999f);
            SetColor(lblEnergyVal, uso > 100f ? Color.red : (uso >= 90f ? Color.yellow : Color.white));
            SetText(lblEnergyBonus, uso > 100f ? "DÉFICIT" : $"{uso:0}% USO");
            SetColor(lblEnergyBonus, uso > 100f ? Color.red : (uso >= 90f ? Color.yellow : Color.green));
            if (lblEnergyBonus != null)
                lblEnergyBonus.tooltip = "Percentual da capacidade da rede que esta sendo consumido.";
        }
        else
        {
            SetColor(lblEnergyVal, consumida > 0 ? Color.red : Color.white);
            SetText(lblEnergyBonus, consumida > 0 ? "DÉFICIT" : "+0/s");
            SetColor(lblEnergyBonus, consumida > 0 ? Color.red : Color.green);
            if (lblEnergyBonus != null)
                lblEnergyBonus.tooltip = "Nenhuma producao de energia foi registrada na rede.";
        }

        if (GerenciadorArmazens.Instancia?.armazemRecursos != null)
        {
            float oc = GerenciadorArmazens.Instancia.armazemRecursos.PercentualOcupacao();
            SetText(lblStorageVal, oc >= 90f ? $"{oc:F0}% CHEIO" : $"{oc:F0}% EST");
            SetColor(lblStorageVal, oc >= 90f ? Color.red : (oc >= 75f ? Color.yellow : Color.white));
            lblStorageVal.tooltip = "Ocupacao do armazem nacional: " + oc.ToString("0") + "%";
        }
        else
        {
            SetText(lblStorageVal, "OK EST");
            SetColor(lblStorageVal, Color.white);
        }

        if (CensoImperial.Instancia != null && pais != null)
        {
            SetText(lblMilitaryVal, pais.populacaoMilitarAtiva.ToString("N0"));
            SetText(lblMilitaryBonus, $"A {pais.alistaveis:N0} | R {pais.reservistas:N0}");
            lblMilitaryVal.tooltip = "Soldados ativos: " + pais.populacaoMilitarAtiva.ToString("N0");
            lblMilitaryBonus.tooltip = "Recrutaveis: " + pais.alistaveis.ToString("N0")
                + "\nReservistas: " + pais.reservistas.ToString("N0");
        }

        AtualizarAlertasAutomaticos(r, economia);
        AtualizarNotificacoes();
        AtualizarAlertaPrincipal();
    }

    private void AtualizarData()
    {
        if (lblDateVal == null)
        {
            return;
        }

        int dias = GerenciadorTempo.Instancia != null ? Mathf.Max(0, GerenciadorTempo.Instancia.totalDias - 1) : 0;
        System.DateTime data = new System.DateTime(2000, 1, 1).AddDays(dias);
        lblDateVal.text = data.ToString("dd/MM/yyyy");
    }

    private void SetText(Label lbl, string text)
    {
        if (lbl != null) lbl.text = text;
    }

    private void SetColor(Label lbl, Color color)
    {
        if (lbl != null) lbl.style.color = color;
    }

    private void AtualizarBonus(Label lbl, float valor, string sufixo, bool monetario = false)
    {
        if (lbl == null) return;
        string valorFormatado = monetario
            ? ValoresDefinitivosHegemonia.FormatarDinheiro(Mathf.RoundToInt(Mathf.Abs(valor)))
            : Mathf.Abs(valor).ToString("N0");
        lbl.text = valor >= 0 ? $"+{valorFormatado}{sufixo}" : $"-{valorFormatado}{sufixo}";
        lbl.style.color = valor >= 0 ? new Color(0.13f, 0.77f, 0.36f) : Color.red;
    }
}
