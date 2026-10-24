using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class MenuGovernoNovoController : MonoBehaviour
{
    // Dados de cada aba sao reconstruidos a partir do estado vivo da partida.
    public static MenuGovernoNovoController Instancia { get; private set; }
    public bool Pronto { get; private set; }

    private UIDocument documento;
    private VisualElement root;
    private Label countryName;
    private Label countryMeta;
    private Label nationalStatus;
    private VisualElement flagPreview;
    private VisualElement recursos;
    private ScrollView navegacao;
    private ScrollView abas;
    private ScrollView conteudo;
    private ScrollView acoes;
    private Label breadcrumb;
    private VisualElement detalheAtual;
    private string categoria = "Relacoes";
    private string abaAtual = "Resumo";
    private string cidadeSelecionadaId = string.Empty;
    // A selecao inicial escolhe um alvo estrangeiro ativo; selecoes invalidas sao
    // substituidas antes de renderizar ou executar acoes diplomaticas.
    private int paisSelecionado = -1;
    private float proximaAtualizacao;
    private bool aberturaPendente;
    private bool tentandoAbrirQuandoPronto;
    private bool mercadoEstoquePrimeiro;
    private bool? modoPercentualAgricolaSelecionadoNoMenu;
    private int diaProducaoRenderizado = -1;
    private int diaProducaoPendente = -1;
    private float horarioAtualizacaoProducao;
    // Mostra o catalogo negociavel ao abrir; o jogador pode filtrar ofertas ativas.
    private bool mercadoSomenteDisponiveis;
    private string mercadoCategoria = "Todos";
    private readonly List<SliderInt> slidersOrcamentoSetores = new List<SliderInt>();
    private readonly List<SliderInt> slidersOrcamentoForcas = new List<SliderInt>();
    private readonly List<Label> valoresOrcamentoSetores = new List<Label>();
    private readonly List<Label> valoresOrcamentoForcas = new List<Label>();
    private Label resumoOrcamentoDiario;
    private Label statusOrcamento;
    private string assinaturaBandeira;

    private int TeamJogador => ResolverTeamJogador(SistemaGovernoMundial.Instancia);

    private static int ResolverTeamJogador(SistemaGovernoMundial governo)
    {
        return governo != null ? Mathf.Max(1, governo.teamJogador) : 1;
    }

    private static readonly string[] OrdemSecoes = new[]
    {
        "Relacoes",
        "Aliancas",
        "Sancoes",
        "Economia",
        "Pais",
        "Mercado",
        "Interior",
        "Defesa",
        "Ciencia",
    };

    private static readonly Dictionary<string, string[]> Secoes = new Dictionary<string, string[]>
    {
        { "Relacoes", new[] { "Resumo", "Nacoes", "Tratados", "Crises" } },
        { "Aliancas", new[] { "Blocos", "Membros", "Pactos", "Pedidos" } },
        { "Sancoes", new[] { "Ativas", "Embargos", "Pressao", "Historico", "Legitimidade", "Emprestimos" } },
        { "Economia", new[] { "Tesouro", "Orcamento", "Gastos", "Producao", "Impostos" } },
        { "Pais", new[] { "Identidade" } },
        { "Mercado", new[] { "Comprar", "Vender", "Precos", "Rotas" } },
        { "Interior", new[] { "Populacao", "Cidades", "Bem-estar", "Prioridades" } },
        { "Defesa", new[] { "Comando", "Exercito", "Marinha", "Aerea", "Alertas" } },
        { "Ciencia", new[] { "Pesquisa", "Tecnologias", "Projetos", "Laboratorios" } },
    };

    public static bool GarantirInstancia()
    {
        if (Instancia != null)
        {
            Instancia.InicializarSeNecessario();
            // The UIDocument can finish binding one frame after the hotkey is
            // pressed.  Keep the new menu as the owner and let Abrir retry;
            // falling back to the legacy menu in that window created two
            // overlapping Government panels.
            return true;
        }

        UIDocument[] documentos = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UIDocument doc in documentos)
        {
            if (doc == null || !doc.gameObject.name.Equals("menu_governo", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            MenuGovernoNovoController controller = doc.GetComponent<MenuGovernoNovoController>();
            if (controller == null)
            {
                controller = doc.gameObject.AddComponent<MenuGovernoNovoController>();
            }

            controller.InicializarSeNecessario();
            return true;
        }

        return false;
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(this);
            return;
        }

        Instancia = this;
        documento = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        InicializarSeNecessario();
        if (Pronto)
            AtualizarTudo(false);
        else
            StartCoroutine(InicializarNoProximoFrame());
    }

    private System.Collections.IEnumerator InicializarNoProximoFrame()
    {
        yield return null;
        InicializarSeNecessario();
        if (Pronto)
            AtualizarTudo(true);
    }

    private void OnDestroy()
    {
        InteractionModeService.Release(this, InteractionOwner.GovernmentMenu);
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    private void Update()
    {
        if (!Pronto || root == null || root.panel == null || root.resolvedStyle.display == DisplayStyle.None)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Abrir(false);
            return;
        }

        if (Time.unscaledTime < proximaAtualizacao)
        {
            return;
        }

        proximaAtualizacao = Time.unscaledTime + 1.25f;
        AtualizarCabecalho();
        AtualizarRecursos();
        AtualizarProducaoSeNovoDia();
    }

    public void Abrir(bool abrir)
    {
        InicializarSeNecessario();
        if (!Pronto || root == null)
        {
            aberturaPendente = abrir;
            if (!tentandoAbrirQuandoPronto)
            {
                tentandoAbrirQuandoPronto = true;
                StartCoroutine(AbrirQuandoPronto());
            }
            return;
        }

        MenuGoverno.EstaAberto = abrir;
        root.style.display = abrir ? DisplayStyle.Flex : DisplayStyle.None;
        if (abrir)
        {
            modoPercentualAgricolaSelecionadoNoMenu = null;
            // O Governo novo usa UI Toolkit e o menu de Construção usa
            // Canvas. Fechar somente o MenuGoverno antigo deixava o Canvas
            // marcado como dono da interação; depois o atalho C parecia não
            // funcionar. Sempre encerra a construção antes de assumir o foco.
            MenuConstrucao construcao = FindFirstObjectByType<MenuConstrucao>();
            if (construcao != null && MenuConstrucao.EstaAberto)
            {
                construcao.AlternarMenu(false);
            }

            InteractionModeService.Request(
                this,
                InteractionOwner.GovernmentMenu,
                new InteractionPolicy
                {
                    bloqueiaSelecao = true,
                    bloqueiaOrdemMundo = true,
                    bloqueiaRotacaoCamera = false,
                    consomeLMB = true,
                    consomeRMB = true
                },
                "Menu Governo aberto");
            AtualizarTudo(true);
        }
        else
        {
            InteractionModeService.Release(this, InteractionOwner.GovernmentMenu);
        }
    }

    public bool AbrirResumoRegional(ComplexoGovernamental centro)
    {
        if (centro == null) return false;

        MarcadorTerritorio marcador = centro.GetComponent<MarcadorTerritorio>();
        if (marcador == null) return false;

        GerenciadorDivisaoTerritorial.GarantirInstancia();
        GerenciadorDivisaoTerritorial divisao = GerenciadorDivisaoTerritorial.Instancia;
        if (divisao == null) return false;

        divisao.RegistrarCidade(marcador);
        CidadeEstado regiao = divisao.cidades.FirstOrDefault(c => c != null && c.marcador == marcador);
        if (regiao == null) return false;

        categoria = "Interior";
        abaAtual = "Cidades";
        cidadeSelecionadaId = regiao.id;
        Abrir(true);
        return true;
    }

    private System.Collections.IEnumerator AbrirQuandoPronto()
    {
        for (int i = 0; i < 30 && (!Pronto || root == null); i++)
        {
            yield return null;
            InicializarSeNecessario();
        }

        tentandoAbrirQuandoPronto = false;
        if (Pronto && root != null)
        {
            bool abrir = aberturaPendente;
            aberturaPendente = false;
            Abrir(abrir);
        }
        else
        {
            aberturaPendente = false;
        }
    }

    private void InicializarSeNecessario()
    {
        if (Pronto && root != null)
        {
            return;
        }

        Pronto = false;

        if (documento == null)
        {
            documento = GetComponent<UIDocument>();
        }

        if (documento == null || documento.rootVisualElement == null)
        {
            return;
        }

        root = documento.rootVisualElement.Q<VisualElement>("menu-governo") ?? documento.rootVisualElement;
        // O painel cobre a tela inteira e consome o apontador; nenhuma
        // estrutura atrás do menu pode receber seleção ou ordem de mundo.
        root.pickingMode = PickingMode.Position;
        countryName = root.Q<Label>("country-name");
        countryMeta = root.Q<Label>("country-meta");
        nationalStatus = root.Q<Label>("national-status");
        flagPreview = root.Q<VisualElement>("flag-preview");
        recursos = root.Q<VisualElement>("resource-bar");
        navegacao = root.Q<ScrollView>("navigation");
        abas = root.Q<ScrollView>("section-tabs");
        conteudo = root.Q<ScrollView>("content-panel");
        acoes = root.Q<ScrollView>("action-panel");
        breadcrumb = root.Q<Label>("breadcrumb");

        ConstruirNavegacao();
        Pronto = root != null && countryName != null && countryMeta != null && nationalStatus != null
            && recursos != null && navegacao != null && abas != null && conteudo != null && acoes != null;

        if (root != null)
        {
            root.style.display = DisplayStyle.None;
        }

        if (!Pronto)
        {
            return;
        }

        AtualizarTudo(true);
    }

    private void AtualizarTudo(bool reconstruir)
    {
        if (!Pronto)
        {
            return;
        }

        GarantirPaisSelecionado();
        AtualizarCabecalho();
        AtualizarRecursos();

        if (!Secoes.TryGetValue(categoria, out string[] abasCategoria) || abasCategoria == null || abasCategoria.Length == 0)
        {
            categoria = OrdemSecoes[0];
            abasCategoria = Secoes[categoria];
        }

        if (reconstruir || !abasCategoria.Contains(abaAtual))
        {
            MostrarSecao(categoria);
            return;
        }

        MostrarPagina(abaAtual);
    }

    private void ConstruirNavegacao()
    {
        if (navegacao == null)
        {
            return;
        }

        navegacao.Clear();
        foreach (string nome in OrdemSecoes)
        {
            string nomeSecao = nome;
            Button botao = new Button(() => MostrarSecao(nomeSecao))
            {
                text = nomeSecao.ToUpperInvariant(),
                name = "nav-" + nomeSecao.ToLowerInvariant()
            };
            botao.AddToClassList("gov-nav-button");
            navegacao.Add(botao);
        }
    }

    private void MostrarSecao(string nome)
    {
        if (!Pronto || string.IsNullOrWhiteSpace(nome) || !Secoes.ContainsKey(nome))
        {
            return;
        }

        categoria = nome;
        foreach (Button botao in navegacao.Query<Button>().ToList())
        {
            botao.EnableInClassList("active", botao.name == "nav-" + nome.ToLowerInvariant());
        }

        abas.Clear();
        string[] nomesAbas = Secoes[nome];
        for (int i = 0; i < nomesAbas.Length; i++)
        {
            string aba = nomesAbas[i];
            Button botao = new Button(() => MostrarPagina(aba)) { text = aba.ToUpperInvariant() };
            botao.AddToClassList("gov-tab-button");
            abas.Add(botao);
        }

        string abaInicial = nomesAbas.Contains(abaAtual) ? abaAtual : nomesAbas[0];
        MostrarPagina(abaInicial);
    }

    private void MostrarPagina(string aba)
    {
        if (!Pronto || abas == null || conteudo == null || acoes == null)
            return;
        GarantirPaisSelecionado();
        abaAtual = aba;
        if (categoria == "Economia" && abaAtual == "Producao")
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            governo?.ReconciliarEstoqueComidaCulturas(TeamJogador);
            AtualizarRecursos();
            diaProducaoRenderizado = GerenciadorTempo.Instancia != null
                ? GerenciadorTempo.Instancia.totalDias : -1;
            diaProducaoPendente = -1;
        }
        foreach (Button botao in abas.Query<Button>().ToList())
            botao.EnableInClassList("active", botao.text.Equals(aba, StringComparison.OrdinalIgnoreCase));
        if (breadcrumb != null) breadcrumb.text = categoria + " / " + aba;

        detalheAtual = null;
        conteudo.Clear();
        acoes.Clear();
        AdicionarTitulo(conteudo, Titulo(categoria, aba), Descricao(categoria));
        PreencherDadosBasicos();
        PreencherAcoes();
    }

    private void AtualizarProducaoSeNovoDia()
    {
        if (categoria != "Economia" || abaAtual != "Producao")
        {
            diaProducaoPendente = -1;
            return;
        }

        GerenciadorTempo tempo = GerenciadorTempo.Instancia;
        int diaAtual = tempo != null ? tempo.totalDias : -1;
        if (diaAtual < 0 || diaAtual == diaProducaoRenderizado) return;

        if (diaProducaoPendente != diaAtual)
        {
            diaProducaoPendente = diaAtual;
            // Deixa os sistemas diários terminarem antes de reconstruir o resumo.
            horarioAtualizacaoProducao = Time.unscaledTime + 0.75f;
            return;
        }

        if (Time.unscaledTime < horarioAtualizacaoProducao) return;

        Vector2 scrollConteudo = conteudo != null ? conteudo.scrollOffset : Vector2.zero;
        Vector2 scrollAcoes = acoes != null ? acoes.scrollOffset : Vector2.zero;
        MostrarPagina(abaAtual);
        if (conteudo != null)
            conteudo.schedule.Execute(() => { if (conteudo != null) conteudo.scrollOffset = scrollConteudo; });
        if (acoes != null)
            acoes.schedule.Execute(() => { if (acoes != null) acoes.scrollOffset = scrollAcoes; });
    }

    private void AtualizarCabecalho()
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(TeamJogador) : null;
        if (countryName != null)
            countryName.text = pais != null && !string.IsNullOrWhiteSpace(pais.nomePais) ? pais.nomePais.ToUpperInvariant() : "PAÍS SEM NOME";
        if (countryMeta != null)
            countryMeta.text = pais != null && !string.IsNullOrWhiteSpace(pais.nomePresidente) ? pais.nomePresidente.ToUpperInvariant() : "CONFIGURE SEU PAÍS";
        if (nationalStatus != null)
            nationalStatus.text = ObterStatusNacional(pais);
        if (flagPreview != null)
        {
            string novaAssinatura = pais != null
                ? pais.modeloBandeira + "|" + pais.corBandeiraPrimaria + "|" + pais.corBandeiraSecundaria + "|" + pais.corBandeiraDestaque
                : string.Empty;
            if (!string.Equals(assinaturaBandeira, novaAssinatura, StringComparison.Ordinal))
            {
                assinaturaBandeira = novaAssinatura;
                PreencherBandeira(flagPreview, pais);
            }
        }
    }

    private static string ObterStatusNacional(DadosPaisGoverno pais)
    {
        if (pais == null) return "OFFLINE";
        if (pais.emGuerra) return "GUERRA";
        if (pais.sancionado) return "SANCOES";
        if (pais.estabilidade < 40f) return "CRISE";
        if (pais.estabilidade < 70f) return "TENSAO";
        return "PAZ";
    }

    private void AtualizarRecursos()
    {
        if (recursos == null) return;
        recursos.Clear();
        SistemaGovernoMundial governoAtual = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governoAtual != null ? governoAtual.ObterPais(TeamJogador) : null;
        if (pais == null) return;
        AgriculturaNacional.GarantirCapacidadeArmazenamento(pais, true);
        governoAtual.ReconciliarEstoqueComidaCulturas(TeamJogador);
        GerenciadorRecursos recursosReais = GerenciadorRecursos.Instancia;
        AdicionarRecurso("TESOURO", Moeda(recursosReais != null ? recursosReais.dinheiro : pais.saldo));
        // O cabeçalho deve ler o armazem vivo. O espelho econômico pode estar
        // atrasado um ciclo, especialmente depois de uma descarga de petroleiro.
        AdicionarRecurso("PETROLEO", EstoqueReal(RecursoMercado.Petroleo).ToString("N0"));
        AdicionarRecurso("ACO", EstoqueReal(RecursoMercado.Aco).ToString("N0"));
        AdicionarRecurso("ENERGIA", EstoqueReal(RecursoMercado.Energia).ToString("N0"));
        AdicionarRecurso("COMIDA", EstoqueReal(RecursoMercado.Comida).ToString("N0"));
        GerenciadorArmazens armazens = GerenciadorArmazens.Instancia;
        DadosArmazemRecursos armazem = armazens != null ? armazens.armazemRecursos : null;
        int aguaAtual = governoAtual.ObterEstoque(TeamJogador, RecursoMercado.Agua);
        int capacidadeAgua = armazem != null && armazem.aguaMaximo > 0
            ? armazem.aguaMaximo
            : Mathf.Max(0, pais.aguaMaxima);
        string agua = Mathf.Max(0, aguaAtual).ToString("N0") + "/" + capacidadeAgua.ToString("N0");
        AdicionarRecurso("AGUA", agua);
        AdicionarRecurso("POPULACAO", pais.populacaoCivil.ToString("N0"));
        AdicionarRecurso("ESTABILIDADE", pais.estabilidade.ToString("0") + "%");
    }

    private static List<DadosPaisGoverno> ObterNacoesAtivas(SistemaGovernoMundial governo)
    {
        List<DadosPaisGoverno> resultado = new List<DadosPaisGoverno>();
        if (governo == null) return resultado;

        HashSet<int> idsAtivos = new HashSet<int> { governo.teamJogador };
        if (IdentidadeIA.TodasIdentidades != null)
        {
            foreach (IdentidadeIA identidade in IdentidadeIA.TodasIdentidades.ToArray())
            {
                if (identidade != null && identidade.estaAtivo && !identidade.eliminado
                    && identidade.teamID > 0 && identidade.teamID != governo.teamJogador)
                    idsAtivos.Add(identidade.teamID);
            }
        }

        resultado.AddRange(governo.Paises.Where(p => p != null && idsAtivos.Contains(p.teamId)).OrderBy(p => p.teamId));
        return resultado;
    }

    private void GarantirPaisSelecionado()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null)
            return;

        List<DadosPaisGoverno> nacoesAtivas = ObterNacoesAtivas(governo);
        bool alvoValido = paisSelecionado > 0
            && paisSelecionado != TeamJogador
            && nacoesAtivas.Any(p => p != null && p.teamId == paisSelecionado);
        if (!alvoValido)
        {
            paisSelecionado = nacoesAtivas
                .Where(p => p != null && p.teamId != TeamJogador)
                .Select(p => p.teamId)
                .FirstOrDefault();
        }
    }

    private bool TemAlvoDiplomatico()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        return governo != null && paisSelecionado != governo.teamJogador && ObterNacoesAtivas(governo).Any(p => p.teamId == paisSelecionado);
    }

    private void ProporAliancaSelecionada()
    {
        string mensagem = "Selecione uma nacao ativa.";
        bool ok = TemAlvoDiplomatico() && SistemaGovernoMundial.Instancia.CriarPropostaAliancaJogador(paisSelecionado, out mensagem);
        MostrarMensagem(ok ? "Alianca enviada para analise." : mensagem);
        MostrarPagina(abaAtual);
    }

    private void ProporPactoSelecionado()
    {
        string mensagem = "Selecione uma nacao ativa.";
        bool ok = TemAlvoDiplomatico() && SistemaGovernoMundial.Instancia.CriarPropostaPactoJogador(paisSelecionado, out mensagem);
        MostrarMensagem(ok ? "Pacto defensivo enviado para analise." : mensagem);
        MostrarPagina(abaAtual);
    }

    private void ProporCessarFogoSelecionado()
    {
        string mensagem = "Selecione uma nacao ativa.";
        bool ok = TemAlvoDiplomatico() && SistemaGovernoMundial.Instancia.CriarPropostaCessarFogoJogador(paisSelecionado, out mensagem);
        MostrarMensagem(ok ? "Cessar-fogo enviado para analise." : mensagem);
        MostrarPagina(abaAtual);
    }

    private static int EstoqueReal(RecursoMercado recurso)
    {
        if (recurso == RecursoMercado.Comida)
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo != null)
            {
                DadosPaisGoverno pais = governo.ObterPais(governo.teamJogador);
                AgriculturaNacional.GarantirCapacidadeArmazenamento(pais, true);
                governo.ReconciliarEstoqueComidaCulturas(governo.teamJogador);
                return Mathf.Max(0, governo.ObterEstoque(governo.teamJogador, RecursoMercado.Comida));
            }
        }

        GerenciadorRecursos gr = GerenciadorRecursos.Instancia;
        if (gr == null) return 0;
        switch (recurso)
        {
            case RecursoMercado.Petroleo: return Mathf.Max(0, gr.petroleo);
            case RecursoMercado.Aco: return Mathf.Max(0, gr.aco);
            case RecursoMercado.Energia: return Mathf.Max(0, gr.energia);
            case RecursoMercado.Comida: return Mathf.Max(0, gr.comida);
            default: return 0;
        }
    }

    private static string DescreverCicloAgricola(
        DadosPaisGoverno pais, AgriculturaNacional agricultura,
        int capacidadeLivre, int colheitaPrevista)
    {
        GerenciadorTempo relogio = GerenciadorTempo.Instancia;
        int diaAtual = relogio != null ? Mathf.Max(1, relogio.totalDias) : -1;
        int diaProcessado = agricultura != null ? agricultura.UltimoDiaProcessado : -1;
        int ultimaColheita = pais != null ? Mathf.Max(0, pais.producaoAgricolaDiaria) : 0;
        string resumo = "Dia atual: " + (diaAtual >= 0 ? diaAtual.ToString("N0") : "indisponível")
            + " | último ciclo processado: " + (diaProcessado >= 0 ? diaProcessado.ToString("N0") : "indisponível")
            + " | última colheita: " + ultimaColheita.ToString("N0") + " t/dia.";

        if (SistemaGovernoMundial.Instancia == null)
            return resumo + "\nBloqueio: sistema de governo indisponível.";
        if (agricultura == null)
            return resumo + "\nBloqueio: Agricultura Nacional não está inicializada.";
        if (relogio == null || !agricultura.RelogioConectado)
            return resumo + "\nBloqueio: relógio diário não conectado à agricultura.";
        if (MenuPausaController.EstaPausado || Time.timeScale <= 0f)
            return resumo + "\nSimulação pausada; a safra roda quando o jogo avançar.";
        if (SistemaIndustrialNacional.Instancia == null)
            return resumo + "\nBloqueio: armazém nacional de culturas indisponível.";
        if (capacidadeLivre <= 0)
            return resumo + "\nBloqueio: armazém de alimentos cheio; a produção volta após liberar espaço.";
        if (diaAtual > diaProcessado)
            return resumo + "\nVirada do dia detectada; a produção está processando os dias pendentes.";
        if (ultimaColheita > 0)
            return resumo + "\nProdução diária confirmada e ativa.";
        if (colheitaPrevista > 0 && diaAtual <= 1)
            return resumo + "\nAguardando a primeira virada do calendário para creditar a previsão de "
                + colheitaPrevista.ToString("N0") + " t/dia.";
        if (colheitaPrevista > 0)
            return resumo + "\nNenhuma colheita foi registrada na última virada. Previsão atual: "
                + colheitaPrevista.ToString("N0") + " t/dia; confira o Console do Unity.";
        return resumo + "\nA previsão atual é 0 t/dia. Verifique água, insumos, rendimento e espaço do armazém.";
    }

    private void AdicionarRecurso(string rotulo, string valor)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("gov-resource-card");
        card.Add(new Label(rotulo) { name = "resource-label" });
        Label valorLabel = new Label(valor) { name = "resource-value" };
        valorLabel.AddToClassList("state-info");
        card.Add(valorLabel);
        recursos.Add(card);
    }

    private void PreencherDadosBasicos()
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(TeamJogador) : null;
        if (pais == null) { AdicionarCard(conteudo, "DADOS INDISPONIVEIS", "O sistema de governo ainda esta inicializando."); return; }
        switch (categoria)
        {
            case "Relacoes": ConstruirRelacoes(); break;
            case "Aliancas": ConstruirAliancas(pais); break;
            case "Sancoes": ConstruirSancoes(); break;
            case "Economia": ConstruirEconomia(pais); break;
            case "Pais": ConstruirIdentidade(pais); break;
            case "Mercado": ConstruirMercado(); break;
            case "Interior": ConstruirInterior(pais); break;
            case "Defesa": ConstruirDefesa(pais); break;
            case "Ciencia": ConstruirCiencia(pais); break;
        }
    }

    private void PreencherAcoes()
    {
        Label titulo = new Label("ACOES");
        titulo.AddToClassList("gov-heading");
        acoes.Add(titulo);
        switch (categoria)
        {
            case "Relacoes":
                if (abaAtual == "Tratados")
                {
                    Acao("ASSINAR CONCORDIA GLOBAL", () => TrocarFederacao(SistemaFederacoesGlobais.TipoFederacao.CooperacaoGlobal.ToString()));
                    Acao("ASSINAR COALIZAO AEGIS", () => TrocarFederacao(SistemaFederacoesGlobais.TipoFederacao.AliancaDefesa.ToString()), "warning");
                }
                Acao("PROPOR ALIANCA", ProporAliancaSelecionada);
                Acao("PACTO DEFENSIVO", ProporPactoSelecionado);
                Acao("PROPOR CESSAR-FOGO", ProporCessarFogoSelecionado, "warning");
                Acao("ENVIAR AJUDA", () => CriarProposta(TipoPropostaInternacional.Doacao, RecursoMercado.Comida, 100));
                Acao("ROMPER ACORDO", RomperAcordoSelecionado, "danger");
                break;
            case "Aliancas":
                Acao("CONVIDAR PAIS", ProporAliancaSelecionada);
                Acao("FIRMAR DEFESA MUTUA", ProporPactoSelecionado);
                Acao("NEGOCIAR CESSAR-FOGO", ProporCessarFogoSelecionado, "warning");
                Acao("ENVIAR RECURSOS", () => CriarProposta(TipoPropostaInternacional.Doacao, RecursoMercado.Comida, 100));
                break;
            case "Sancoes":
                Acao("IMPOR SANCAO", () => AlterarSancaoSelecionada(true), "warning");
                Acao("LEVANTAR SANCAO", () => AlterarSancaoSelecionada(false));
                if (abaAtual == "Embargos")
                {
                    Acao("EMBARGAR COMIDA", () => AlterarEmbargo(RecursoMercado.Comida, true), "warning");
                    Acao("LIBERAR COMIDA", () => AlterarEmbargo(RecursoMercado.Comida, false));
                    Acao("EMBARGAR PETROLEO", () => AlterarEmbargo(RecursoMercado.Petroleo, true), "warning");
                    Acao("LIBERAR PETROLEO", () => AlterarEmbargo(RecursoMercado.Petroleo, false));
                }
                if (abaAtual == "Emprestimos")
                {
                    Acao("PEDIR AJUSTE ECONOMICO", () => SolicitarEmprestimo(false));
                    Acao("PEDIR CREDITO MILITAR", () => SolicitarEmprestimo(true), "warning");
                }
                Acao("REVER IMPACTO", ReverImpactoSancoes);
                break;
            case "Economia":
                if (abaAtual == "Gastos")
                {
                    Acao("ATUALIZAR GASTOS MILITARES", AtualizarGastosMilitares);
                    Acao("ABRIR DEFESA", () => { categoria = "Defesa"; MostrarSecao("Defesa"); });
                }
                else if (abaAtual == "Tesouro" || abaAtual == "Orcamento")
                {
                    Acao("GERAR EMPREGOS", () => Executar(() => SistemaGovernoMundial.Instancia.AlterarEmprego(TeamJogador, 3f), "Programa de empregos executado."));
                    Acao("INVESTIR EM MORADIA", MelhorarMoradia);
                    Acao("AJUSTAR IMPOSTOS +1", () => AjustarImposto(1));
                    Acao("AJUSTAR IMPOSTOS -1", () => AjustarImposto(-1));
                }
                else if (abaAtual == "Producao")
                {
                    AdicionarAcoesRapidasDeComida();
                    AdicionarAcoesInvestimentoAgricola();
                    VisualElement outrosSetores = AdicionarSecaoAcoes("OUTROS SETORES", "Expanda a produção industrial e a oferta de energia.");
                    VisualElement gradeOutrosSetores = AdicionarGradeAcoes(outrosSetores);
                    AdicionarBotaoGrade(gradeOutrosSetores, "INDÚSTRIA\nINVESTIR", () => Investir("industria"), "gov-action-option");
                    AdicionarBotaoGrade(gradeOutrosSetores, "ENERGIA\nINVESTIR", () => Investir("energia"), "gov-action-option");
                    AdicionarBotaoAcao(outrosSetores, "ATUALIZAR DIAGNÓSTICO", AtualizarDiagnosticoProducao, "gov-action-wide");
                }
                else if (abaAtual == "Impostos")
                {
                    Acao("SUBIR IMPOSTOS", () => AjustarImposto(1));
                    Acao("REDUZIR IMPOSTOS", () => AjustarImposto(-1));
                    Acao("INVESTIR EM MORADIA", MelhorarMoradia);
                }
                else
                {
                    Acao("INVESTIR EM INDUSTRIA", () => Investir("industria"));
                    Acao("INVESTIR EM ENERGIA", () => Investir("energia"));
                    Acao("AJUSTAR IMPOSTOS +1", () => AjustarImposto(1));
                }
                break;
            case "Pais":
                Acao("FINALIZAR CONFIGURACAO DO PAIS", SalvarIdentidadeDoPais, "warning");
                Acao("ABRIR ORCAMENTO NACIONAL", () => { categoria = "Economia"; MostrarSecao("Economia"); MostrarPagina("Orcamento"); });
                break;
            case "Mercado":
                Acao("ATUALIZAR COTACOES", AtualizarMercado);
                Acao("ABRIR PRECOS", () => MostrarPagina("Precos"));
                Acao("ABRIR VENDAS", () => MostrarPagina("Vender"));
                Acao("ABRIR ROTAS", () => MostrarPagina("Rotas"));
                break;
            case "Interior":
                Acao("MELHORAR MORADIA", MelhorarMoradia, "warning");
                Acao("MUTIRAO DE EMPREGOS", () => Executar(() => SistemaGovernoMundial.Instancia.AlterarEmprego(TeamJogador, 3f), "Emprego nacional ampliado."));
                Acao("EXPANDIR SERVICOS", () => Investir("economia"));
                break;
            case "Defesa":
                Acao("DEFESA ATIVA", () => Plano("Defesa Ativa"), "danger");
                Acao("MOBILIZACAO", () => Plano("Mobilizacao"), "warning");
                Acao("EQUILIBRIO", () => Plano("Equilibrio"));
                Acao("INVESTIR EM DEFESA", () => Investir("defesa"));
                if (abaAtual == "Aerea")
                {
                    Acao("ALTERNAR MANUTENCAO DO SATELITE", () =>
                    {
                        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
                        DadosPaisGoverno pais = gov != null ? gov.ObterPais(TeamJogador) : null;
                        if (pais != null)
                        {
                            if (pais.sateliteDefesa == null) pais.sateliteDefesa = new SateliteDefesaEstado();
                            gov.ConfigurarSatelite(TeamJogador, !pais.sateliteDefesa.manutencaoAutomatica);
                            MostrarMensagem("Manutencao automatica do satelite atualizada.");
                            MostrarPagina(abaAtual);
                        }
                    });
                    Acao("APORTAR $1.200 NO SATELITE", () =>
                    {
                        string mensagem = "Programa satelital indisponivel.";
                        bool ok = SistemaGovernoMundial.Instancia != null
                            && SistemaGovernoMundial.Instancia.InvestirNoSatelite(TeamJogador, 1200, out mensagem);
                        MostrarMensagem(ok ? mensagem : "Aporte recusado: " + mensagem);
                        MostrarPagina(abaAtual);
                    }, "warning");
                }
                break;
            case "Ciencia":
                if (abaAtual == "Pesquisa")
                {
                    Acao("INVESTIR EM INDUSTRIA", () => Investir("industria"));
                    Acao("INVESTIR EM ENERGIA", () => Investir("energia"));
                    Acao("ATUALIZAR RELATORIO", AtualizarRelatorioCientifico);
                }
                else if (abaAtual == "Tecnologias")
                {
                    Acao("APLICAR A INDUSTRIA", () => Investir("industria"));
                    Acao("ABRIR PROJETOS", () => MostrarPagina("Projetos"));
                    Acao("VER LABORATORIOS", () => MostrarPagina("Laboratorios"));
                }
                else if (abaAtual == "Projetos")
                {
                    Acao("CRIAR LOTE DE ACO", () => CriarProjetoIndustrial(IndustriaIds.AcoEstrutural));
                    Acao("CRIAR ELETRONICOS", () => CriarProjetoIndustrial(IndustriaIds.ComponentesEletronicos));
                    Acao("ATUALIZAR FILA", AtualizarFilaIndustrial);
                }
                else
                {
                    Acao("INVESTIR EM CIENCIA", () => Investir("ciencia"));
                    Acao("PRIORIZAR ENERGIA", () => Investir("energia"));
                    Acao("ABRIR PESQUISA", () => MostrarPagina("Pesquisa"));
                }
                break;
        }

        AtualizarDisponibilidadeAcoesDiplomaticas();
    }

    private void AtualizarDisponibilidadeAcoesDiplomaticas()
    {
        if (categoria != "Relacoes" && categoria != "Aliancas" && categoria != "Sancoes") return;

        List<string> motivos = new List<string>();
        if (!TemAlvoDiplomatico())
        {
            string motivo = "Selecione uma nacao estrangeira ativa para usar estas acoes.";
            foreach (string acao in new[]
            {
                "PROPOR ALIANCA", "PACTO DEFENSIVO", "PROPOR CESSAR-FOGO", "ENVIAR AJUDA",
                "CONVIDAR PAIS", "FIRMAR DEFESA MUTUA", "NEGOCIAR CESSAR-FOGO", "ENVIAR RECURSOS",
                "ROMPER ACORDO", "IMPOR SANCAO", "LEVANTAR SANCAO", "EMBARGAR COMIDA",
                "LIBERAR COMIDA", "EMBARGAR PETROLEO", "LIBERAR PETROLEO",
                "PEDIR AJUSTE ECONOMICO", "PEDIR CREDITO MILITAR"
            })
            {
                DefinirDisponibilidadeAcao(acao, false, motivo, motivos);
            }
        }
        else
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            RelacaoPaisGoverno relacao = governo != null
                ? governo.Relacoes.FirstOrDefault(r => r != null && r.Envolve(TeamJogador, paisSelecionado))
                : null;
            if (categoria == "Relacoes")
            {
                bool existeAcordo = relacao != null && (relacao.pactoMilitar || relacao.tratadoComercial);
                DefinirDisponibilidadeAcao("ROMPER ACORDO", existeAcordo,
                    "Nao ha tratado comercial ou pacto defensivo ativo com o alvo.", motivos);
            }

            if (categoria == "Sancoes")
            {
                bool sancaoAtiva = relacao != null && relacao.sancaoAtiva;
                DefinirDisponibilidadeAcao("IMPOR SANCAO", !sancaoAtiva,
                    "O alvo ja esta sob sancao deste governo.", motivos);
                DefinirDisponibilidadeAcao("LEVANTAR SANCAO", sancaoAtiva,
                    "Nao existe sancao ativa deste governo contra o alvo.", motivos);

                if (abaAtual == "Embargos")
                {
                    bool comidaEmbargada = relacao != null && relacao.embargos != null && relacao.embargos.Contains(RecursoMercado.Comida);
                    bool petroleoEmbargado = relacao != null && relacao.embargos != null && relacao.embargos.Contains(RecursoMercado.Petroleo);
                    DefinirDisponibilidadeAcao("EMBARGAR COMIDA", !comidaEmbargada,
                        "Comida ja esta embargada para o alvo.", motivos);
                    DefinirDisponibilidadeAcao("LIBERAR COMIDA", comidaEmbargada,
                        "Nao ha embargo de comida ativo para o alvo.", motivos);
                    DefinirDisponibilidadeAcao("EMBARGAR PETROLEO", !petroleoEmbargado,
                        "Petroleo ja esta embargado para o alvo.", motivos);
                    DefinirDisponibilidadeAcao("LIBERAR PETROLEO", petroleoEmbargado,
                        "Nao ha embargo de petroleo ativo para o alvo.", motivos);
                }
            }
        }

        if (motivos.Count > 0)
        {
            Label aviso = new Label(string.Join("\n", motivos));
            aviso.AddToClassList("gov-subheading");
            aviso.AddToClassList("state-warn");
            aviso.style.whiteSpace = WhiteSpace.Normal;
            acoes.Add(aviso);
        }
    }

    private void DefinirDisponibilidadeAcao(string texto, bool habilitada, string motivo, List<string> motivos)
    {
        Button botao = acoes.Query<Button>().ToList()
            .FirstOrDefault(b => b != null && string.Equals(b.text, texto, StringComparison.OrdinalIgnoreCase));
        if (botao == null) return;

        botao.SetEnabled(habilitada);
        if (!habilitada && !motivos.Contains(motivo)) motivos.Add(motivo);
        if (!habilitada) botao.tooltip = motivo;
    }

    private void TrocarFederacao(string destino)
    {
        SistemaFederacoesGlobais.GarantirInstancia();
        string mensagem = "federacao indisponivel";
        bool ok;
        if (SistemaFederacoesGlobais.Instancia == null)
        {
            ok = false;
            mensagem = "sistema de federacoes indisponivel";
        }
        else
        {
            ok = SistemaFederacoesGlobais.Instancia.TrocarFederacao(TeamJogador, destino, out mensagem);
        }
        MostrarMensagem((ok ? "Filiacao confirmada: " : "Filiacao recusada: ") + mensagem);
        if (ok) SistemaGovernoMundial.Instancia?.ProcessarEconomia();
        MostrarPagina(abaAtual);
    }

    private void ReverImpactoSancoes()
    {
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        SistemaMercadoGlobal.Instancia?.SimularMercado();
        gov?.ProcessarEconomia();
        int afetados = gov != null ? ContarRelacoes(r => r.sancaoAtiva) : 0;
        MostrarMensagem("Impacto recalculado: " + afetados + " relacao(oes) sob sancao.");
        MostrarPagina(abaAtual);
    }

    private void AlterarEmbargo(RecursoMercado recurso, bool ativar)
    {
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        string mensagem = "Selecione uma nacao ativa.";
        bool ok = false;
        if (TemAlvoDiplomatico() && gov != null)
        {
            ok = ativar
                ? gov.AplicarEmbargo(paisSelecionado, recurso, out mensagem)
                : gov.RemoverEmbargo(paisSelecionado, recurso, out mensagem);
        }
        MostrarMensagem(ok ? mensagem : "Embargo recusado: " + mensagem);
        MostrarPagina(abaAtual);
    }

    private void RomperAcordoSelecionado()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        string mensagem = "Selecione uma nacao estrangeira ativa.";
        bool ok = TemAlvoDiplomatico() && governo != null
            && governo.TentarRomperAcordo(paisSelecionado, out mensagem);
        MostrarMensagem(ok ? mensagem : "Acordo nao rompido: " + mensagem);
        MostrarPagina(abaAtual);
    }

    private void AlterarSancaoSelecionada(bool aplicar)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        string mensagem = "Selecione uma nacao estrangeira ativa.";
        bool ok = false;
        if (TemAlvoDiplomatico() && governo != null)
        {
            ok = aplicar
                ? governo.TentarAplicarSancao(paisSelecionado, out mensagem)
                : governo.TentarRemoverSancao(paisSelecionado, out mensagem);
        }

        MostrarMensagem(ok ? mensagem : "Acao recusada: " + mensagem);
        MostrarPagina(abaAtual);
    }

    private void AtualizarGastosMilitares()
    {
        SistemaGastosMilitares.GarantirInstancia();
        SistemaGastosMilitares gastos = SistemaGastosMilitares.Instancia;
        List<RegistroGastoMilitar> registros = gastos != null
            ? gastos.ObterRegistrosDoTime(TeamJogador).ToList()
            : new List<RegistroGastoMilitar>();
        long total = registros.Sum(x => Math.Max(0L, x.valorTotal));
        MostrarPagina(abaAtual);
        MostrarMensagem(gastos != null
            ? "Historico recarregado: " + registros.Count + " registro(s), total de " + Moeda(total) + "."
            : "Historico de gastos militares indisponivel.");
    }

    private void AtualizarRelatorioCientifico()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(TeamJogador) : null;
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        int ordensPendentes = industrial != null
            ? industrial.OrdensRefino.Count(o => o != null && o.teamId == TeamJogador
                && o.estado != EstadoOrdemRefinoIndustrial.Cancelada
                && o.estado != EstadoOrdemRefinoIndustrial.Concluida)
            : 0;

        MostrarPagina(abaAtual);
        if (pais != null)
        {
            AdicionarCard(conteudo, "RELATORIO CIENTIFICO RECARREGADO",
                "Nivel industrial atual: " + pais.nivelIndustrial.ToString("0")
                    + "\nOrdens de refino pendentes: " + ordensPendentes
                    + "\nSaldo nacional consultado: " + Moeda(pais.saldo));
            MostrarMensagem("Relatorio cientifico recarregado com os dados atuais do pais e da fila industrial.");
        }
        else
        {
            AdicionarCard(conteudo, "RELATORIO INDISPONIVEL", "Os dados nacionais ainda nao foram carregados.");
            MostrarMensagem("Relatorio cientifico indisponivel: dados nacionais nao carregados.");
        }
    }

    private void AtualizarFilaIndustrial()
    {
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            MostrarPagina(abaAtual);
            MostrarMensagem("Fila industrial indisponivel: sistema nao carregado.");
            return;
        }

        int ordensPendentes = industrial.OrdensRefino.Count(o => o != null && o.teamId == TeamJogador
            && o.estado != EstadoOrdemRefinoIndustrial.Cancelada
            && o.estado != EstadoOrdemRefinoIndustrial.Concluida);
        MostrarPagina(abaAtual);
        MostrarFilaIndustrial(null);
        MostrarMensagem("Fila industrial relida: " + ordensPendentes + " ordem(ns) pendente(s). Nenhuma ordem foi iniciada ou alterada.");
    }

    private void AtualizarDiagnosticoProducao()
    {
        SistemaEconomiaImoveis economiaImoveis = SistemaEconomiaImoveis.Instancia;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (economiaImoveis == null && governo == null)
        {
            MostrarMensagem("Diagnostico indisponivel: sistemas economico e governamental nao carregados.");
            return;
        }

        economiaImoveis?.Recalcular();
        governo?.ProcessarEconomia();
        string resultado = economiaImoveis != null && governo != null
            ? "Diagnostico imobiliario e economia nacional recalculados."
            : economiaImoveis != null
                ? "Diagnostico imobiliario recalculado; sistema governamental indisponivel."
                : "Economia nacional processada; recalculo imobiliario indisponivel.";
        MostrarMensagem(resultado);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void MostrarMensagem(string acao)
    {
        Label rodape = root.Q<Label>("footer-message");
        if (rodape != null) rodape.text = "GOVERNO: " + acao;
    }

    private void Acao(string texto, Action callback, string variante = null)
    {
        Button botao = new Button(callback) { text = texto };
        botao.AddToClassList("gov-action-button");
        if (!string.IsNullOrEmpty(variante)) botao.AddToClassList(variante);
        acoes.Add(botao);
    }

    private void Executar(Action acao, string mensagem)
    {
        if (SistemaGovernoMundial.Instancia == null) return;
        acao();
        MostrarMensagem(mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void Investir(string foco)
    {
        bool ok = SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.InvestirCapacidadeNacional(TeamJogador, foco);
        MostrarMensagem(ok ? "Investimento em " + foco + " realizado." : "Recursos insuficientes para investir em " + foco + ".");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void InvestirProducaoAgricola()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        AgriculturaNacional agricultura = governo != null ? governo.GetComponent<AgriculturaNacional>() : null;
        bool investido = agricultura != null
            && agricultura.InvestirArea(TeamJogador, AreaInvestimentoAgricola.Producao);
        MostrarMensagem(investido
            ? "Capacidade nominal agricola ampliada em 5 t/dia; a colheita sera recalculada na proxima virada do dia."
            : "Investimento agricola recusado: sistema indisponivel, saldo insuficiente ou capacidade no nivel maximo.");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void AdicionarAcoesRapidasDeComida()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(TeamJogador) : null;
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        DadosItemMercado item = mercado != null ? mercado.ObterItem("comida") : null;
        int estoque = governo != null ? governo.ObterEstoque(TeamJogador, RecursoMercado.Comida) : 0;
        int capacidadeLivre = CapacidadeLivreComida(pais, estoque);
        long saldoDisponivel = governo != null ? governo.ObterSaldoDisponivelParaOperacao(TeamJogador) : 0L;
        VisualElement secao = AdicionarSecaoAcoes("ESTOQUE ALIMENTAR", "Compra imediata para repor o estoque nacional.");
        int capacidade = pais != null ? Mathf.Max(0, pais.capacidadeArmazenamentoAgricola) : 0;
        AdicionarTextoAcao(secao,
            "Armazenado: " + estoque.ToString("N0") + "/" + capacidade.ToString("N0")
            + " t | espaço livre: " + capacidadeLivre.ToString("N0") + " t | saldo: " + Moeda(saldoDisponivel));
        AdicionarControleCompraAlimentar(secao);

        if (governo == null || pais == null)
        {
            AdicionarAvisoAcao(secao, "Estoque nacional indisponível.");
            return;
        }

        if (item == null || mercado == null)
        {
            AdicionarAvisoAcao(secao, "Mercado global ainda não inicializado.");
        }
        else if (capacidadeLivre <= 0)
        {
            AdicionarAvisoAcao(secao, "Armazém cheio. Amplie o armazenamento agrícola antes de comprar.");
        }
        else
        {
            VisualElement grade = AdicionarGradeAcoes(secao);
            foreach (int loteSolicitado in new[] { 100, 500, 1000 })
            {
                int lote = Mathf.Min(loteSolicitado, capacidadeLivre);
                int precoUnitario = Mathf.Max(1, item.precoAtual);
                long subtotal = (long)lote * precoUnitario;
                long estimativa = subtotal + Mathf.CeilToInt(subtotal * SistemaLogisticaMercado.FretePercentual);
                Button botao = AdicionarBotaoGrade(grade,
                    "COMPRAR " + lote.ToString("N0") + " t\n" + Moeda(estimativa),
                    () => ComprarComidaRapida(lote), "gov-action-option buy");
                botao.SetEnabled(lote > 0 && saldoDisponivel >= estimativa);
                botao.tooltip = saldoDisponivel < estimativa
                    ? "Saldo insuficiente. Custo estimado com frete: " + Moeda(estimativa)
                    : "Compra imediata estimada com frete: " + Moeda(estimativa);
            }
        }
    }

    private void AdicionarControleCompraAlimentar(VisualElement pai)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(TeamJogador) : null;
        AgriculturaNacional agricultura = governo != null ? governo.GetComponent<AgriculturaNacional>() : null;
        if (pai == null || pais == null || agricultura == null) return;

        if (pais.participacaoCompraAlimentar == null
            || pais.participacaoCompraAlimentar.Length != AgriculturaNacional.QuantidadeCulturas)
            pais.participacaoCompraAlimentar = Enumerable.Repeat(1f, AgriculturaNacional.QuantidadeCulturas).ToArray();

        VisualElement controle = new VisualElement();
        controle.AddToClassList("card");
        controle.AddToClassList("gov-culture-control");
        Label titulo = new Label("ORCAMENTO DIARIO DE ALIMENTOS");
        titulo.AddToClassList("gov-card-title");
        controle.Add(titulo);
        Label explicacao = new Label("A compra usa agora a porcentagem do caixa disponivel. Com pais e rota ativos, a verba e reservada e o cargueiro entrega; sem fornecedor/rota, o Mercado automatico entrega na hora.");
        explicacao.AddToClassList("gov-action-section-copy");
        controle.Add(explicacao);

        SliderInt percentualCaixa = new SliderInt("Caixa por dia (%)", 0, 100)
        {
            value = Mathf.RoundToInt(Mathf.Clamp(pais.compraAlimentarPercentualCaixaDiario, 0f, 100f))
        };
        percentualCaixa.AddToClassList("gov-share-slider");
        Label resumoOrcamento = new Label();
        resumoOrcamento.AddToClassList("gov-share-value");
        Action atualizarResumo = () =>
        {
            long caixa = governo.ObterSaldoDisponivelParaOperacao(TeamJogador);
            long verba = AgriculturaNacional.CalcularOrcamentoCompraAlimentar(caixa, percentualCaixa.value);
            resumoOrcamento.text = percentualCaixa.value + "% do caixa = " + Moeda(verba) + " por dia";
        };
        percentualCaixa.RegisterValueChangedCallback(evt =>
        {
            pais.compraAlimentarPercentualCaixaDiario = evt.newValue;
            atualizarResumo();
        });
        atualizarResumo();
        controle.Add(percentualCaixa);
        controle.Add(resumoOrcamento);

        List<string> culturas = new List<string>();
        for (int i = 0; i < AgriculturaNacional.QuantidadeCulturas; i++)
            culturas.Add(AgriculturaNacional.ObterNomeCultura(AgriculturaNacional.ObterIdCultura(i)));
        int indiceInicial = 0;
        for (int i = 1; i < pais.participacaoCompraAlimentar.Length; i++)
            if (pais.participacaoCompraAlimentar[i] > pais.participacaoCompraAlimentar[indiceInicial]) indiceInicial = i;
        DropdownField cultura = new DropdownField("Alimento", culturas, indiceInicial);
        cultura.AddToClassList("gov-culture-select");
        SliderInt percentualCultura = new SliderInt("Participacao da verba (%)", 0, 100);
        percentualCultura.AddToClassList("gov-share-slider");
        Label resumoCultura = new Label();
        resumoCultura.AddToClassList("gov-share-value");
        Action atualizarParticipacao = () =>
        {
            float soma = pais.participacaoCompraAlimentar.Sum(x => Mathf.Max(0f, x));
            int indice = Mathf.Clamp(culturas.IndexOf(cultura.value), 0, culturas.Count - 1);
            float peso = Mathf.Max(0f, pais.participacaoCompraAlimentar[indice]);
            int percentual = soma > 0f ? Mathf.RoundToInt(peso / soma * 100f) : 0;
            percentualCultura.SetValueWithoutNotify(percentual);
            resumoCultura.text = percentual + "% da verba nesta cultura";
        };
        cultura.RegisterValueChangedCallback(_ => atualizarParticipacao());
        percentualCultura.RegisterValueChangedCallback(evt => resumoCultura.text = evt.newValue + "% da verba nesta cultura");
        atualizarParticipacao();
        controle.Add(cultura);
        controle.Add(percentualCultura);

        VisualElement botoes = new VisualElement();
        botoes.AddToClassList("gov-inline-actions");
        Button aplicarMix = new Button(() =>
        {
            int indice = Mathf.Max(0, culturas.IndexOf(cultura.value));
            bool ok = agricultura.DefinirParticipacaoCompraAlimentar(TeamJogador, indice, percentualCultura.value);
            MostrarMensagem(ok ? "Mix alimentar salvo para " + culturas[indice] + "." : "Nao foi possivel salvar o mix alimentar.");
            MostrarPagina(abaAtual);
        }) { text = "APLICAR MIX" };
        aplicarMix.AddToClassList("gov-mini-button");
        botoes.Add(aplicarMix);

        Button comprarAgora = new Button(() =>
        {
            agricultura.DefinirPercentualCompraAlimentar(TeamJogador, percentualCaixa.value);
            bool ok = agricultura.ExecutarCompraAlimentarPercentualCaixa(TeamJogador, out string mensagem, out _);
            MostrarMensagem(ok ? mensagem : "Compra nao executada: " + mensagem);
            if (pais.compraAlimentarDiariaAtiva && GerenciadorTempo.Instancia != null)
                pais.ultimoDiaCompraAlimentarProcessado = Mathf.Max(1, GerenciadorTempo.Instancia.totalDias);
            AtualizarRecursos();
            MostrarPagina(abaAtual);
        }) { text = "COMPRAR HOJE" };
        comprarAgora.AddToClassList("gov-mini-button");
        comprarAgora.AddToClassList("buy");
        botoes.Add(comprarAgora);

        Button automatizar = new Button(() =>
        {
            agricultura.DefinirPercentualCompraAlimentar(TeamJogador, percentualCaixa.value);
            bool ativa = !pais.compraAlimentarDiariaAtiva;
            bool ok = agricultura.DefinirCompraAlimentarDiaria(TeamJogador, ativa, out string mensagem);
            MostrarMensagem(ok ? mensagem : "Rotina diaria atualizada com aviso: " + mensagem);
            AtualizarRecursos();
            MostrarPagina(abaAtual);
        }) { text = pais.compraAlimentarDiariaAtiva ? "PAUSAR ROTINA" : "ATIVAR + COMPRAR HOJE" };
        automatizar.AddToClassList("gov-mini-button");
        automatizar.AddToClassList(pais.compraAlimentarDiariaAtiva ? "warning" : "buy");
        botoes.Add(automatizar);
        controle.Add(botoes);
        pai.Add(controle);
    }

    private void AdicionarAcoesInvestimentoAgricola()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(TeamJogador) : null;
        AgriculturaNacional agricultura = governo != null ? governo.GetComponent<AgriculturaNacional>() : null;
        if (pais == null || agricultura == null || governo == null)
        {
            VisualElement indisponivel = AdicionarSecaoAcoes("INVESTIMENTO AGRÍCOLA", "O sistema nacional ainda está inicializando.");
            AdicionarAvisoAcao(indisponivel, "Investimentos temporariamente indisponíveis.");
            return;
        }

        int nivel = pais.niveisInvestimentoAgricola != null
            && pais.niveisInvestimentoAgricola.Length > (int)AreaInvestimentoAgricola.Producao
            ? Mathf.Clamp(pais.niveisInvestimentoAgricola[(int)AreaInvestimentoAgricola.Producao], 0, 10) : 0;
        long custoProximoNivel = AgriculturaNacional.CalcularCobrancaFixaSemanal(100 + nivel * 50);
        float capacidadeNominal = 24f + Mathf.Max(0, pais.capacidadeAgricola)
            + nivel * 5f;
        if (pais.niveisInvestimentoAgricola != null
            && pais.niveisInvestimentoAgricola.Length > (int)AreaInvestimentoAgricola.Agroindustria)
            capacidadeNominal += pais.niveisInvestimentoAgricola[(int)AreaInvestimentoAgricola.Agroindustria] * 2f;

        VisualElement expansao = AdicionarSecaoAcoes("CAPACIDADE AGRÍCOLA", "A ampliação adiciona capacidade nominal; a colheita real é atualizada no próximo dia.");
        AdicionarTextoAcao(expansao, "Atual: " + capacidadeNominal.ToString("N0") + " t/dia | nível " + nivel + "/10");
        Button ampliar = AdicionarBotaoAcao(expansao,
            nivel >= 10 ? "CAPACIDADE NO MÁXIMO" : "AMPLIAR +5 t/dia\n" + Moeda(custoProximoNivel),
            InvestirProducaoAgricola, "gov-action-wide buy");
        ampliar.SetEnabled(nivel < 10 && governo.ObterSaldoDisponivelParaOperacao(TeamJogador) >= custoProximoNivel);
        if (nivel < 10 && governo.ObterSaldoDisponivelParaOperacao(TeamJogador) < custoProximoNivel)
            ampliar.tooltip = "Saldo insuficiente. Custo do próximo nível: " + Moeda(custoProximoNivel);

        long saldo = governo.ObterSaldoDisponivelParaOperacao(TeamJogador);
        string investimentoAtual = pais.investimentoAgricolaPercentualAtivo
            ? pais.investimentoAgricolaPercentualSemanal.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"))
                + "% semanal • próxima cobrança estimada: "
                + Moeda(AgriculturaNacional.CalcularCobrancaSemanal(saldo, pais.investimentoAgricolaPercentualSemanal))
            : "modo fixo • " + Moeda(AgriculturaNacional.CalcularCobrancaFixaSemanal(pais.investimentoAgricolaSemanal)) + "/semana";
        VisualElement recorrencia = AdicionarSecaoAcoes("INVESTIMENTO RECORRENTE", "Escolha o valor fixo semanal ou um percentual do saldo. A seleção apenas agenda a cobrança; a primeira cobrança ocorre na próxima virada semanal.");
        AdicionarTextoAcao(recorrencia, "Configuração atual: " + investimentoAtual);
        AdicionarTextoAcao(recorrencia, "Saldo disponível para operação: " + Moeda(saldo) + ". Nenhuma quantia é debitada ao clicar.");
        bool mostrarPercentual = modoPercentualAgricolaSelecionadoNoMenu ?? pais.investimentoAgricolaPercentualAtivo;
        VisualElement gradeModos = AdicionarGradeAcoes(recorrencia);
        Button modoFixo = AdicionarBotaoGrade(gradeModos, "VALOR FIXO\nPOR SEMANA",
            () => SelecionarModoInvestimentoAgricola(false), "gov-investment-mode");
        Button modoPercentual = AdicionarBotaoGrade(gradeModos, "% DO SALDO\nPOR SEMANA",
            () => SelecionarModoInvestimentoAgricola(true), "gov-investment-mode");
        modoFixo.EnableInClassList("selected", !mostrarPercentual);
        modoPercentual.EnableInClassList("selected", mostrarPercentual);

        if (mostrarPercentual)
        {
            VisualElement gradePercentuais = AdicionarGradeAcoes(recorrencia);
            float[] percentuais = { 0f, 0.01f, 0.025f, 0.05f, 0.1f, 0.25f };
            int producaoSemInvestimento = agricultura.EstimarProducaoDiaria(pais, 0f);
            for (int i = 0; i < percentuais.Length; i++)
            {
                float percentual = percentuais[i];
                long estimativa = AgriculturaNacional.CalcularCobrancaSemanal(saldo, percentual);
                float bonus = AgriculturaNacional.CalcularBonusInvestimentoPercentual(estimativa);
                int producaoEstimada = agricultura.EstimarProducaoDiaria(pais, bonus);
                int ganhoEstimado = Mathf.Max(0, producaoEstimada - producaoSemInvestimento);
                string rotulo = percentual <= 0f
                    ? "PAUSAR\n" + Moeda(estimativa) + "/sem\n" + producaoEstimada.ToString("N0") + " t/dia"
                    : percentual.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR")) + "% DO SALDO\n"
                        + Moeda(estimativa) + "/sem\n" + producaoEstimada.ToString("N0") + " t/dia | +"
                        + ganhoEstimado.ToString("N0") + " t";
                Button botao = AdicionarBotaoGrade(gradePercentuais, rotulo,
                    () => DefinirInvestimentoAgricola(percentual),
                    percentual <= 0f ? "gov-action-option warning" : "gov-action-option buy");
                bool selecionado = pais.investimentoAgricolaPercentualAtivo
                    && Mathf.Abs(pais.investimentoAgricolaPercentualSemanal - percentual) < 0.0001f;
                botao.EnableInClassList("selected", selecionado);
                botao.tooltip = percentual <= 0f
                    ? "Desativa a cobrança recorrente. A safra não recebe bônus por investimento."
                    : "Debita " + percentual.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"))
                        + "% do saldo operacional na próxima virada semanal: " + Moeda(estimativa)
                        + ". Após a cobrança, a previsão passa a " + producaoEstimada.ToString("N0")
                        + " t/dia (ganho de " + ganhoEstimado.ToString("N0") + " t/dia sobre a safra sem investimento).";
            }
        }
        else
        {
            VisualElement gradeFixos = AdicionarGradeAcoes(recorrencia);
            int[] investimentos = { 100, 250, 500, 1000, 2500, 5000, 10000 };
            int producaoSemInvestimento = agricultura.EstimarProducaoDiaria(pais, 0f);
            for (int i = 0; i < investimentos.Length; i++)
            {
                int milhoes = investimentos[i];
                long cobranca = AgriculturaNacional.CalcularCobrancaFixaSemanal(milhoes);
                float bonus = AgriculturaNacional.CalcularBonusInvestimentoFixo(milhoes);
                int producaoEstimada = agricultura.EstimarProducaoDiaria(pais, bonus);
                int ganhoEstimado = Mathf.Max(0, producaoEstimada - producaoSemInvestimento);
                Button botao = AdicionarBotaoGrade(gradeFixos,
                    Moeda(cobranca) + "/SEM\n" + producaoEstimada.ToString("N0") + " t/dia | +"
                        + ganhoEstimado.ToString("N0") + " t",
                    () => DefinirInvestimentoAgricolaFixo(milhoes), "gov-action-option buy");
                botao.EnableInClassList("selected", !pais.investimentoAgricolaPercentualAtivo
                    && pais.investimentoAgricolaSemanal == milhoes);
                botao.tooltip = "Débito fixo de " + Moeda(cobranca) + " a cada semana. O clique não cobra agora; a primeira cobrança ocorre na próxima virada semanal. Saldo disponível: "
                    + Moeda(saldo) + ". Produção estimada após pagamento: " + producaoEstimada.ToString("N0")
                    + " t/dia (ganho de " + ganhoEstimado.ToString("N0") + " t/dia sobre a safra sem investimento).";
            }
        }

        if (pais.investimentoAgricolaSemRecursos)
            AdicionarAvisoAcao(recorrencia, "A cobrança mais recente não foi paga. O investimento não está aumentando a produção; reduza o valor ou recupere o saldo.");

        int sementes = governo.ObterEstoque(TeamJogador, "sementes");
        int fertilizante = governo.ObterEstoque(TeamJogador, "fertilizante_organico");
        int agua = governo.ObterEstoque(TeamJogador, RecursoMercado.Agua);
        VisualElement insumos = AdicionarSecaoAcoes("INSUMOS DA LAVOURA",
            "Sementes " + sementes.ToString("N0") + " t • fertilizante " + fertilizante.ToString("N0")
            + " t • água " + agua.ToString("N0") + " t. Sem eles, a lavoura mantém a safra de sequeiro.");
        VisualElement gradeInsumos = AdicionarGradeAcoes(insumos);
        Button mercadoInsumos = AdicionarBotaoGrade(gradeInsumos, "SEMENTES E\nFERTILIZANTE",
            () => AbrirMercadoDeInsumos("Insumos"), "gov-action-option");
        mercadoInsumos.tooltip = "Abre Mercado > Comprar com a categoria Insumos selecionada.";
        Button mercadoAgua = AdicionarBotaoGrade(gradeInsumos, "ÁGUA\nIRRIGAÇÃO",
            () => AbrirMercadoDeInsumos("Agua"), "gov-action-option");
        mercadoAgua.tooltip = "Abre Mercado > Comprar com a categoria Água selecionada.";
    }

    private void AbrirMercadoDeInsumos(string categoriaMercado)
    {
        mercadoCategoria = categoriaMercado;
        mercadoSomenteDisponiveis = false;
        abaAtual = "Comprar";
        MostrarSecao("Mercado");
    }

    private VisualElement AdicionarSecaoAcoes(string titulo, string descricao)
    {
        VisualElement secao = new VisualElement();
        secao.AddToClassList("gov-action-section");
        Label tituloLabel = new Label(titulo);
        tituloLabel.AddToClassList("gov-action-section-title");
        secao.Add(tituloLabel);
        if (!string.IsNullOrWhiteSpace(descricao))
        {
            Label descricaoLabel = new Label(descricao);
            descricaoLabel.AddToClassList("gov-action-section-copy");
            secao.Add(descricaoLabel);
        }
        acoes.Add(secao);
        return secao;
    }

    private static VisualElement AdicionarGradeAcoes(VisualElement secao)
    {
        VisualElement grade = new VisualElement();
        grade.AddToClassList("gov-action-grid");
        secao.Add(grade);
        return grade;
    }

    private static Button AdicionarBotaoGrade(VisualElement grade, string texto, Action callback, string classes)
    {
        Button botao = new Button(callback) { text = texto };
        botao.AddToClassList("gov-action-button");
        foreach (string classe in classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            botao.AddToClassList(classe);
        grade.Add(botao);
        return botao;
    }

    private static Button AdicionarBotaoAcao(VisualElement pai, string texto, Action callback, string classes)
    {
        Button botao = new Button(callback) { text = texto };
        botao.AddToClassList("gov-action-button");
        foreach (string classe in classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            botao.AddToClassList(classe);
        pai.Add(botao);
        return botao;
    }

    private static void AdicionarTextoAcao(VisualElement secao, string texto)
    {
        Label label = new Label(texto);
        label.AddToClassList("gov-action-section-copy");
        secao.Add(label);
    }

    private static void AdicionarAvisoAcao(VisualElement secao, string texto)
    {
        Label label = new Label(texto);
        label.AddToClassList("gov-action-notice");
        secao.Add(label);
    }

    private void DefinirInvestimentoAgricola(float percentual)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        AgriculturaNacional agricultura = governo != null ? governo.GetComponent<AgriculturaNacional>() : null;
        bool configurado = agricultura != null && agricultura.DefinirInvestimentoPercentualSemanal(TeamJogador, percentual);
        if (configurado) modoPercentualAgricolaSelecionadoNoMenu = true;
        MostrarMensagem(configurado
            ? (percentual <= 0f ? "Investimento agricola pausado. Nenhuma cobranca recorrente sera feita."
                : "Investimento configurado em " + percentual.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR")) + "% do saldo operacional por semana. A primeira cobranca ocorre na proxima semana do jogo.")
            : "Nao foi possivel configurar o investimento agricola.");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void SelecionarModoInvestimentoAgricola(bool percentual)
    {
        modoPercentualAgricolaSelecionadoNoMenu = percentual;
        MostrarPagina(abaAtual);
    }

    private void DefinirInvestimentoAgricolaFixo(int milhoes)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        AgriculturaNacional agricultura = governo != null ? governo.GetComponent<AgriculturaNacional>() : null;
        bool configurado = agricultura != null && agricultura.DefinirInvestimentoSemanal(TeamJogador, milhoes);
        if (configurado) modoPercentualAgricolaSelecionadoNoMenu = false;
        long cobranca = AgriculturaNacional.CalcularCobrancaFixaSemanal(milhoes);
        MostrarMensagem(configurado
            ? "Investimento fixo configurado em " + Moeda(cobranca) + " por semana. Nenhum valor foi descontado agora; a primeira cobrança será na próxima virada semanal."
            : "Não foi possível configurar o investimento agrícola fixo.");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private int CapacidadeLivreComida(DadosPaisGoverno pais, int estoqueAtual)
    {
        if (pais == null) return 0;
        AgriculturaNacional.GarantirCapacidadeArmazenamento(pais, pais.teamId == TeamJogador);

        int capacidadeMaxima = Mathf.Max(0, pais.capacidadeArmazenamentoAgricola);
        GerenciadorArmazens armazens = GerenciadorArmazens.Instancia;
        DadosArmazemRecursos armazem = armazens != null ? armazens.armazemRecursos : null;
        if (armazem != null && armazem.alimentosMaximo > 0)
            capacidadeMaxima = capacidadeMaxima > 0
                ? Mathf.Min(capacidadeMaxima, armazem.alimentosMaximo)
                : armazem.alimentosMaximo;

        if (capacidadeMaxima <= 0) return 0;
        int livreNacional = Mathf.Max(0, capacidadeMaxima - Mathf.Max(estoqueAtual, pais.comida));
        if (armazem == null) return livreNacional;
        return Mathf.Min(livreNacional, Mathf.Max(0, armazem.alimentosMaximo - armazem.alimentos));
    }

    private void ComprarComidaRapida(int quantidadeSolicitada)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(TeamJogador) : null;
        DadosItemMercado item = mercado != null ? mercado.ObterItem("comida") : null;
        if (governo == null || mercado == null || pais == null || item == null)
        {
            MostrarMensagem("A compra de alimentos esta indisponivel porque governo ou mercado nao foi inicializado.");
            return;
        }

        int estoque = EstoqueReal(RecursoMercado.Comida);
        int quantidade = Mathf.Min(Mathf.Max(0, quantidadeSolicitada), CapacidadeLivreComida(pais, estoque));
        if (quantidade <= 0)
        {
            MostrarMensagem("Nao ha espaco livre no armazenamento nacional de alimentos.");
            return;
        }

        SistemaGastosMilitares.GarantirInstancia();
        bool comprou = mercado.ComprarAlimentoComFallback(TeamJogador, item.id, quantidade, out string mensagem);
        MostrarMensagem(comprou ? "Compra de alimentos iniciada: " + mensagem : "Compra recusada: " + mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void MelhorarMoradia()
    {
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        bool ok = gov != null && gov.TentarPagar(TeamJogador, 900);
        if (ok) gov.AlterarMoradia(TeamJogador, 3f);
        MostrarMensagem(ok ? "Investimento em moradia realizado." : "Recursos insuficientes para moradia.");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void AtualizarMercado()
    {
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (mercado == null)
        {
            MostrarPagina(abaAtual);
            MostrarMensagem("Cotacoes indisponiveis: sistema de mercado nao carregado.");
            return;
        }

        mercado.SimularMercado();
        MostrarMensagem("Cotacoes do mercado atualizadas a partir do sistema global.");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void SolicitarEmprestimo(bool militar)
    {
        string mensagem = "Sistema federativo indisponivel.";
        if (!TemAlvoDiplomatico())
        {
            MostrarMensagem("Selecione um credor ativo na aba Nacoes antes de pedir o emprestimo.");
            MostrarPagina(abaAtual);
            return;
        }
        bool ok = SistemaFederacoesGlobais.Instancia != null && SistemaFederacoesGlobais.Instancia.SolicitarEmprestimo(paisSelecionado, 1, 2500f, militar, out mensagem);
        MostrarMensagem(ok ? "Emprestimo aprovado: " + mensagem : "Emprestimo recusado: " + mensagem);
        MostrarPagina(abaAtual);
    }

    private void QuitarEmprestimo(string loanId)
    {
        string mensagem = "Sistema federativo indisponivel.";
        bool ok = SistemaFederacoesGlobais.Instancia != null && SistemaFederacoesGlobais.Instancia.QuitarEmprestimo(TeamJogador, loanId, out mensagem);
        MostrarMensagem(ok ? "Emprestimo quitado." : "Nao foi possivel quitar: " + mensagem);
        MostrarPagina(abaAtual);
    }

    private void ConstruirMercado()
    {
        SistemaGovernoMundial.GarantirInstancia();
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (mercado == null)
        {
            AdicionarCard(conteudo, "MERCADO INDISPONIVEL", "O sistema de mercado ainda nao foi inicializado.");
            return;
        }

        // O catalogo de fichas pode carregar depois do singleton do mercado.
        // Sincronizar aqui garante que os equipamentos militares aparecam no
        // primeiro acesso ao menu.
        mercado.SincronizarCatalogoConstrucao();

        // O catalogo compartilhado tambem recebe construcoes que nao sao
        // negociaveis. Elas nao devem ocupar a lista do mercado.
        var itens = mercado.ItensOrdenados()
            .Where(item => item.podeComprar || item.podeVender)
            .ToList();
        int compras = mercado.historico.Count(t => t != null && t.compradorTeamId == TeamJogador);
        int vendas = mercado.historico.Count(t => t != null && t.vendedorTeamId == TeamJogador);
        float tendencia = itens.Count > 0 ? itens.Average(i => i.variacaoPercentual) : 0f;

        LinhaCards(new[]
        {
            ("ITENS", itens.Count.ToString(), "state-info"),
            ("COMPRAS", compras.ToString(), "state-good"),
            ("VENDAS", vendas.ToString(), "state-warn"),
            ("TENDENCIA", tendencia.ToString("0.0") + "%", "state-info")
        });

        SistemaGovernoMundial governoConectado = SistemaGovernoMundial.Instancia;
        List<string> parceirosComerciais = new List<string>();
        if (governoConectado != null)
        {
            foreach (DadosPaisGoverno pais in ObterNacoesAtivas(governoConectado))
            {
                if (pais == null || pais.teamId == governoConectado.teamJogador) continue;
                RelacaoPaisGoverno relacao = governoConectado.ObterRelacao(governoConectado.teamJogador, pais.teamId);
                if (relacao != null && !relacao.guerraDeclarada && !relacao.sancaoAtiva && relacao.valor >= 40
                    && (relacao.tratadoComercial || relacao.valor >= 60))
                    parceirosComerciais.Add(pais.nomePais + " (amizade " + relacao.valor + ")");
            }
        }
        AdicionarCard(conteudo, "PARCEIROS COMERCIAIS CONECTADOS",
            parceirosComerciais.Count > 0
                ? string.Join(", ", parceirosComerciais) + ". As vendas priorizam esses paises conforme a demanda e o saldo."
                : "Nenhum pais amigo esta conectado para comprar automaticamente. Melhore as relacoes diplomaticas ou remova sancoes.");

        if (abaAtual == "Comprar" || abaAtual == "Vender")
        {
            bool comprar = abaAtual == "Comprar";
            AdicionarCard(conteudo, comprar ? "COMPRAR RECURSOS" : "VENDER RECURSOS",
                comprar
                    ? "Escolha a quantidade em cada card. O valor total e calculado antes da confirmacao."
                    : "Venda recursos do estoque nacional para compradores internacionais disponiveis.");
            if (comprar) AdicionarControleCompraAlimentar(conteudo);

            VisualElement filtros = new VisualElement();
            filtros.AddToClassList("gov-market-filters");
            Label filtroTitulo = new Label("FILTROS DO MERCADO");
            filtroTitulo.AddToClassList("gov-market-filter-title");
            filtros.Add(filtroTitulo);

            Toggle estoquePrimeiro = new Toggle("MEU ESTOQUE PRIMEIRO")
            {
                value = mercadoEstoquePrimeiro
            };
            estoquePrimeiro.AddToClassList("gov-market-filter");
            estoquePrimeiro.tooltip = "Coloca no topo os recursos que ja existem no seu armazenamento.";
            estoquePrimeiro.RegisterValueChangedCallback(evt =>
            {
                mercadoEstoquePrimeiro = evt.newValue;
                MostrarPagina(abaAtual);
            });
            filtros.Add(estoquePrimeiro);

            Toggle somenteDisponiveis = new Toggle("SOMENTE DISPONIVEIS")
            {
                value = mercadoSomenteDisponiveis
            };
            somenteDisponiveis.AddToClassList("gov-market-filter");
            somenteDisponiveis.tooltip = comprar
                ? "Mostra apenas ofertas com estoque e fornecedor comercial aliado."
                : "Mostra apenas itens com estoque nacional e comprador aliado com saldo.";
            somenteDisponiveis.RegisterValueChangedCallback(evt =>
            {
                mercadoSomenteDisponiveis = evt.newValue;
                MostrarPagina(abaAtual);
            });
            filtros.Add(somenteDisponiveis);

            VisualElement categorias = new VisualElement();
            categorias.AddToClassList("gov-market-category-filters");
            string[] categoriasDisponiveis = new[]
                {
                    "Minerios", "Combustiveis", "Alimentos", "Agua", "Insumos", "Industrial",
                    "Tanques", "Navios", "Aeronaves", "Armas", "Municoes", "Outros"
                }
                .Concat(itens.Select(CategoriaMercado))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(nome => itens.Any(item => string.Equals(CategoriaMercado(item), nome, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            foreach (string nomeCategoria in new[] { "Todos" }.Concat(categoriasDisponiveis))
            {
                string categoriaBotao = nomeCategoria;
                Button botaoCategoria = new Button(() =>
                {
                    mercadoCategoria = categoriaBotao;
                    MostrarPagina(abaAtual);
                }) { text = categoriaBotao.ToUpperInvariant() };
                botaoCategoria.AddToClassList("gov-market-filter");
                if (categoriaBotao == mercadoCategoria) botaoCategoria.AddToClassList("active");
                categorias.Add(botaoCategoria);
            }
            filtros.Add(categorias);
            conteudo.Add(filtros);

            VisualElement grade = new VisualElement();
            grade.AddToClassList("gov-market-grid");
            IEnumerable<DadosItemMercado> itensExibicao = itens.Where(item => comprar ? item.podeComprar : item.podeVender);
            if (!string.Equals(mercadoCategoria, "Todos", StringComparison.OrdinalIgnoreCase))
                itensExibicao = itensExibicao.Where(item => CategoriaMercado(item) == mercadoCategoria);
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            int timeJogador = governo != null ? governo.teamJogador : 1;
            var itensComParceiro = itensExibicao
                .Select(item => new
                {
                    Item = item,
                    Parceiro = EncontrarParceiroMercado(mercado, item, timeJogador, comprar)
                })
                .ToList();
            if (mercadoSomenteDisponiveis)
            {
                itensComParceiro = itensComParceiro.Where(registro =>
                {
                    DadosItemMercado item = registro.Item;
                    int estoque = EstoqueMercadoDoTime(item, timeJogador);
                    return comprar
                        ? item.precoAtual > 0 && item.estoqueGlobal > 0 && registro.Parceiro != null
                        : item.precoAtual > 0 && estoque > 0 && registro.Parceiro != null;
                }).ToList();
            }
            if (mercadoEstoquePrimeiro)
            {
                itensComParceiro = itensComParceiro
                    .OrderByDescending(registro => EstoqueMercadoDoTime(registro.Item, timeJogador) > 0)
                    .ThenBy(registro => registro.Item.NomeFormatado)
                    .ToList();
            }
            foreach (var registro in itensComParceiro)
                AdicionarCardMercado(grade, registro.Item, comprar, registro.Parceiro);
            if (grade.childCount == 0)
            {
                AdicionarCard(conteudo, "NENHUM ITEM NESTE FILTRO",
                    "Retire o filtro ou aguarde a atualização do estoque e das ofertas.");
            }
            conteudo.Add(grade);
            return;
        }

        if (abaAtual == "Rotas")
        {
            AdicionarCard(conteudo, "ROTAS E OPERACOES RECENTES",
                "As transacoes abaixo mostram a origem, o destino e o valor movimentado pelo mercado global.");
            TabelaCabecalho("RECURSO", "ORIGEM", "DESTINO", "QUANTIDADE", "TOTAL");
            foreach (TransacaoMercado transacao in mercado.historico.Where(t => t != null).Reverse<TransacaoMercado>().Take(20))
            {
                DadosPaisGoverno origem = SistemaGovernoMundial.Instancia.ObterPais(transacao.vendedorTeamId);
                DadosPaisGoverno destino = SistemaGovernoMundial.Instancia.ObterPais(transacao.compradorTeamId);
                DadosItemMercado item = mercado.ObterItem(transacao.itemId);
                TabelaLinha(item != null ? item.NomeFormatado : transacao.itemId,
                    origem != null ? origem.nomePais : "Mercado",
                    destino != null ? destino.nomePais : "Mercado",
            transacao.quantidade.ToString("N0"), Moeda(transacao.total));
            }
            if (!mercado.historico.Any(t => t != null))
                AdicionarCard(conteudo, "SEM OPERACOES", "Ainda nao existem rotas comerciais registradas.");
            return;
        }

        AdicionarCard(conteudo, "COTACOES DO MERCADO",
            "Compare precos, variacoes, oferta e disponibilidade antes de negociar.");
        TabelaCabecalho("ITEM", "PRECO", "VARIACAO", "ESTOQUE GLOBAL", "STATUS");
        foreach (DadosItemMercado item in itens)
        {
            string status = item.precoAtual <= 0
                ? "SEM PRECO"
                : item.podeComprar && item.estoqueGlobal > 0
                    ? "OFERTA DISPONIVEL"
                    : item.podeVender
                        ? "VENDA NACIONAL"
                        : "SEM OFERTA";
            TabelaLinha(item.NomeFormatado, Moeda(item.precoAtual),
                item.variacaoPercentual.ToString("+0.0;-0.0;0.0") + "%", item.estoqueGlobal.ToString("N0"), status);
        }
    }

    private DadosPaisGoverno EncontrarParceiroMercado(SistemaMercadoGlobal mercado, DadosItemMercado item, int teamJogador, bool comprar)
    {
        if (mercado == null || item == null || item.precoAtual <= 0) return null;
        int estoqueReferencia = comprar ? item.estoqueGlobal : EstoqueMercadoDoTime(item, teamJogador);
        if (estoqueReferencia <= 0) return null;

        int quantidadePadrao = Mathf.Max(1, item.CalcularQuantidadePadrao());
        int quantidadePreferida = Mathf.Min(quantidadePadrao, estoqueReferencia);
        DadosPaisGoverno parceiro = comprar
            ? mercado.EncontrarMelhorFornecedor(teamJogador, item, quantidadePreferida)
            : mercado.EncontrarMelhorComprador(teamJogador, item, quantidadePreferida);
        if (parceiro == null && quantidadePreferida > 1)
        {
            parceiro = comprar
                ? mercado.EncontrarMelhorFornecedor(teamJogador, item, 1)
                : mercado.EncontrarMelhorComprador(teamJogador, item, 1);
        }
        return parceiro;
    }

    private void AdicionarCardMercado(VisualElement grade, DadosItemMercado item, bool comprar, DadosPaisGoverno parceiro)
    {
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        int timeJogador = governo != null ? governo.teamJogador : 1;
        int estoqueJogador = EstoqueMercadoDoTime(item, timeJogador);
        int estoqueFornecedor = !comprar
            ? 0
            : parceiro == null
                ? Mathf.Max(0, item.estoqueGlobal)
                : item.equipamentoMilitar ? item.estoqueGlobal : EstoqueMercadoDoTime(item, parceiro.teamId);
        int limiteEstoque = comprar
            ? Mathf.Min(Mathf.Max(0, item.estoqueGlobal), Mathf.Max(0, estoqueFornecedor))
            : Mathf.Max(0, estoqueJogador);
        int limiteQuantidade = mercado != null
            ? mercado.QuantidadeMaximaTransacionavel(item, comprar, limiteEstoque)
            : Mathf.Min(limiteEstoque, int.MaxValue / Mathf.Max(1, item.precoAtual));
        if (parceiro != null && mercado != null && (!comprar || item.equipamentoMilitar))
        {
            long saldoParceiro = governo != null ? governo.ObterSaldoDisponivelParaOperacao(parceiro.teamId) : parceiro.saldo;
            limiteQuantidade = Mathf.Min(limiteQuantidade,
                mercado.QuantidadeMaximaPorSaldo(item, !comprar, saldoParceiro, limiteQuantidade));
        }
        if (comprar && governo != null && mercado != null)
        {
            long saldoJogador = governo.ObterSaldoDisponivelParaOperacao(timeJogador);
            limiteQuantidade = Mathf.Min(limiteQuantidade,
                mercado.QuantidadeMaximaPorSaldo(item, true, saldoJogador, limiteQuantidade));
        }
        int quantidadePadrao = Mathf.Max(1, item.CalcularQuantidadePadrao());
        int passoQuantidade = limiteQuantidade > 0 ? Mathf.Min(quantidadePadrao, limiteQuantidade) : quantidadePadrao;
        int quantidadeInicial = limiteQuantidade > 0 ? Mathf.Clamp(quantidadePadrao, 1, limiteQuantidade) : 1;

        VisualElement card = new VisualElement();
        card.AddToClassList("gov-market-card");

        Label titulo = new Label(item.NomeFormatado.ToUpperInvariant());
        titulo.AddToClassList("gov-card-title");
        card.Add(titulo);

        string variacao = item.variacaoPercentual.ToString("+0.0;-0.0;0.0") + "%";
        string unidadeLabel = item.equipamentoMilitar ? " un." : item.municaoMilitar ? " cart." : " t";
        string liquidacao = item.equipamentoMilitar
            ? "\nA unidade sera transferida diretamente ao comprador."
            : item.municaoMilitar
                ? "\nEstoque de municao nacional, creditado apos a entrega."
                : "\nLiquidacao no estoque agregado do armazem.";
        string limiteLabel = limiteQuantidade > 0
            ? "\nLimite desta operacao: " + limiteQuantidade.ToString("N0") + unidadeLabel
            : string.Empty;
        Label detalhes = new Label(
            "Preco unitario: " + Moeda(item.precoAtual) +
            "\nVariacao: " + variacao +
            (comprar
                ? "\nOferta global: " + item.estoqueGlobal.ToString("N0") + unidadeLabel
                    + (parceiro != null ? "\nPais vendedor: " + parceiro.nomePais
                        + (item.equipamentoMilitar ? string.Empty : " (estoque: " + estoqueFornecedor.ToString("N0") + unidadeLabel + ")") : string.Empty)
                : "\nSeu estoque: " + estoqueJogador.ToString("N0") + unidadeLabel
                    + (parceiro != null ? "\nComprador: " + parceiro.nomePais : string.Empty)) + limiteLabel + liquidacao);
        detalhes.AddToClassList("gov-market-details");
        card.Add(detalhes);

        IntegerField quantidade = new IntegerField(item.equipamentoMilitar ? "QUANTIDADE (UNIDADES)" : item.municaoMilitar ? "QUANTIDADE (CARTUCHOS)" : "QUANTIDADE (t)");
        quantidade.value = quantidadeInicial;
        quantidade.AddToClassList("gov-quantity");
        card.Add(quantidade);

        Label total = new Label();
        total.AddToClassList("gov-market-total");
        Action atualizarTotal = () =>
        {
            int valor = Mathf.Clamp(quantidade.value, 1, Mathf.Max(1, limiteQuantidade));
            if (item.precoAtual <= 0)
            {
                total.text = "SEM PRECO DISPONIVEL";
                return;
            }

            long subtotal = (long)valor * Mathf.Max(1, item.precoAtual);
            int frete = mercado != null ? mercado.CalcularFreteMercado(item, comprar, valor) : 0;
            total.text = comprar
                ? "ITENS: " + Moeda(subtotal) + " | FRETE: " + Moeda(frete)
                    + "\nTOTAL A PAGAR: " + Moeda(subtotal + frete) + " | " + valor.ToString("N0") + unidadeLabel
                : "VALOR DA VENDA: " + Moeda(subtotal) + " | FRETE DO COMPRADOR: " + Moeda(frete)
                    + "\nVOCE RECEBE: " + Moeda(subtotal) + " | " + valor.ToString("N0") + unidadeLabel;
        };
        Toggle repeticao = null;
        quantidade.RegisterValueChangedCallback(evt =>
        {
            int ajustada = Mathf.Clamp(evt.newValue, 1, Mathf.Max(1, limiteQuantidade));
            if (ajustada != evt.newValue) quantidade.SetValueWithoutNotify(ajustada);
            atualizarTotal();
            if (repeticao != null && repeticao.value)
                SistemaLogisticaMercado.Instancia?.ConfigurarRepeticao(timeJogador, item.id, ajustada, comprar, true);
        });
        atualizarTotal();
        card.Add(total);

        SistemaLogisticaMercado.GarantirInstancia();
        repeticao = new Toggle("REPETIR A CADA 2 DIAS");
        repeticao.value = SistemaLogisticaMercado.Instancia != null && SistemaLogisticaMercado.Instancia.TemRepeticao(timeJogador, item.id, comprar);
        repeticao.AddToClassList("gov-market-repeat");
        repeticao.RegisterValueChangedCallback(evt =>
        {
            SistemaLogisticaMercado.Instancia?.ConfigurarRepeticao(timeJogador, item.id, Mathf.Max(1, quantidade.value), comprar, evt.newValue);
        });
        card.Add(repeticao);
        quantidade.SetEnabled(limiteQuantidade > 0);
        repeticao.SetEnabled(item.precoAtual > 0);

        VisualElement botoes = new VisualElement();
        botoes.AddToClassList("gov-inline-actions");
        Button menos = new Button(() => quantidade.value = Mathf.Max(1, quantidade.value - passoQuantidade)) { text = "-" + passoQuantidade };
        Button mais = new Button(() => quantidade.value = (int)Math.Min(limiteQuantidade, (long)quantidade.value + passoQuantidade)) { text = "+" + passoQuantidade };
        Button confirmar = new Button(() => Transacionar(item, Mathf.Max(1, quantidade.value), comprar, parceiro != null ? parceiro.teamId : 0))
        {
            text = comprar ? "COMPRAR" : "VENDER"
        };
        menos.AddToClassList("gov-mini-button");
        mais.AddToClassList("gov-mini-button");
        confirmar.AddToClassList("gov-mini-button");
        confirmar.AddToClassList(comprar ? "buy" : "warning");
        bool disponivel = item.precoAtual > 0 && limiteQuantidade > 0 && parceiro != null && (comprar
            ? item.podeComprar && item.estoqueGlobal > 0 && estoqueFornecedor > 0
            : item.podeVender && estoqueJogador > 0);
        confirmar.SetEnabled(disponivel);
        menos.SetEnabled(limiteQuantidade > 0);
        mais.SetEnabled(limiteQuantidade > 0);
        botoes.Add(menos);
        botoes.Add(mais);
        botoes.Add(confirmar);
        card.Add(botoes);

        if (!disponivel)
        {
            string motivoIndisponibilidade = item.precoAtual <= 0
                ? "SEM PRECO"
                : comprar && item.estoqueGlobal <= 0
                    ? "OFERTA GLOBAL ESGOTADA"
                    : !comprar && estoqueJogador <= 0
                        ? "SEM ESTOQUE PARA VENDA"
                        : parceiro == null
                            ? (comprar ? "SEM FORNECEDOR ALIADO (TRATADO COMERCIAL OU AMIZADE 60+)" : "SEM COMPRADOR ALIADO COM SALDO")
                            : comprar && estoqueFornecedor <= 0
                                ? "FORNECEDOR SEM ESTOQUE"
                                : limiteQuantidade <= 0
                                    ? (comprar ? "SALDO INSUFICIENTE PARA ITEM E FRETE" : "COMPRADOR SEM SALDO PARA ESTA QUANTIDADE")
                                : "OPERACAO INDISPONIVEL";
            Label aviso = new Label(motivoIndisponibilidade);
            aviso.AddToClassList("gov-market-warning");
            card.Add(aviso);
        }
        grade.Add(card);
    }

    private void AjustarImposto(int delta)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        List<string> ajustados = new List<string>();
        List<string> noLimite = new List<string>();
        if (governo != null)
        {
            foreach (string setor in new[] { "moradia", "industria", "comercio" })
            {
                if (governo.AjustarImposto(TeamJogador, setor, delta)) ajustados.Add(setor);
                else noLimite.Add(setor);
            }
        }

        string mensagem;
        if (governo == null)
            mensagem = "Sistema de impostos indisponivel.";
        else if (noLimite.Count == 0)
            mensagem = "Impostos ajustados nos setores: " + string.Join(", ", ajustados) + ".";
        else if (ajustados.Count == 0)
            mensagem = "Nenhum imposto alterado; os setores ja estao no limite: " + string.Join(", ", noLimite) + ".";
        else
            mensagem = "Ajuste parcial. Alterados: " + string.Join(", ", ajustados)
                + ". No limite: " + string.Join(", ", noLimite) + ".";

        MostrarMensagem(mensagem);
        MostrarPagina(abaAtual);
    }

    private void Plano(string plano)
    {
        bool ok = SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.DefinirPlanoEstrategico(TeamJogador, plano);
        MostrarMensagem(ok ? "Plano estrategico definido: " + plano + "." : "Nao foi possivel alterar o plano.");
        MostrarPagina(abaAtual);
    }

    private void CriarProposta(TipoPropostaInternacional tipo, RecursoMercado recurso, int quantidade)
    {
        if (!TemAlvoDiplomatico())
        {
            MostrarMensagem("Selecione uma nacao ativa antes de enviar recursos.");
            return;
        }
        bool ok = SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.CriarPropostaJogador(paisSelecionado, tipo, recurso, quantidade, 0, "Ordem do menu Governo");
        MostrarMensagem(ok ? "Proposta internacional registrada." : "A proposta nao pode ser criada agora.");
        MostrarPagina(abaAtual);
    }

    private void Transacionar(DadosItemMercado item, int quantidade, bool comprar, int parceiroTeamId)
    {
        SistemaGovernoMundial g = SistemaGovernoMundial.Instancia;
        SistemaMercadoGlobal m = SistemaMercadoGlobal.Instancia;
        if (g == null || m == null)
        {
            MostrarMensagem("Governo ou mercado indisponivel. Aguarde a inicializacao e tente novamente.");
            return;
        }
        string mensagem = string.Empty;
        bool ok = false;
        if (comprar)
        {
            SistemaGastosMilitares.GarantirInstancia();
            DadosPaisGoverno vendedor = parceiroTeamId > 0
                ? g.ObterPais(parceiroTeamId)
                : m.EncontrarMelhorFornecedor(g.teamJogador, item, quantidade);
            if (vendedor == null)
            {
                mensagem = "Nenhum fornecedor amigo possui estoque ou rota comercial ativa.";
            }
            else
            {
                ok = m.Comprar(TeamJogador, vendedor.teamId, item.id, quantidade, out mensagem);
            }
        }
        else
        {
            DadosPaisGoverno comprador = parceiroTeamId > 0
                ? g.ObterPais(parceiroTeamId)
                : m.EncontrarMelhorComprador(g.teamJogador, item, quantidade);
            if (comprador == null)
                mensagem = "Nenhum pais amigo demonstrou interesse ou possui saldo para esta operacao.";
            else
                ok = m.Vender(g.teamJogador, comprador.teamId, item.id, quantidade, out mensagem);
        }
        MostrarMensagem((ok ? "Operacao concluida: " : "Operacao recusada: ") + mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private static string CategoriaMercado(DadosItemMercado item)
    {
        if (item == null) return "Outros";
        if (item.municaoMilitar) return "Municoes";
        if (item.equipamentoMilitar)
        {
            string tipo = (item.tipoEntrega ?? string.Empty).ToLowerInvariant();
            if (tipo.Contains("aeronave")) return "Aeronaves";
            if (tipo.Contains("navio")) return "Navios";
            string texto = ((item.nome ?? string.Empty) + " " + (item.id ?? string.Empty)).ToLowerInvariant();
            return texto.Contains("tanque") || texto.Contains("tank") || texto.Contains("blindad") ? "Tanques" : "Armas";
        }
        string id = ((item.id ?? string.Empty) + " " + (item.recursoId ?? string.Empty)).ToLowerInvariant();
        string categoria = (item.categoria ?? string.Empty).ToLowerInvariant();
        if (item.recurso == RecursoMercado.Comida || id.Contains("comida_")) return "Alimentos";
        if (item.recurso == RecursoMercado.Agua || id.Contains("agua")) return "Agua";
        if (categoria.Contains("insumo agricola")) return "Insumos";

        switch (item.recurso)
        {
            case RecursoMercado.Comida: return "Alimentos";
            case RecursoMercado.Petroleo:
            case RecursoMercado.Diesel:
            case RecursoMercado.CombustivelAviacao:
            case RecursoMercado.CombustivelNaval: return "Combustiveis";
            case RecursoMercado.MinerioFerro:
            case RecursoMercado.MinerioCobre:
            case RecursoMercado.Bauxita:
            case RecursoMercado.MinerioTitanio:
            case RecursoMercado.Uranio: return "Minerios";
            case RecursoMercado.Armamentos: return "Armas";
            case RecursoMercado.Aco:
            case RecursoMercado.CobreEletrolitico:
            case RecursoMercado.Duraluminio:
            case RecursoMercado.LigaTitanio:
            case RecursoMercado.ComponentesEletronicos:
            case RecursoMercado.UranioEnriquecido: return "Industrial";
            default:
                string texto = (categoria + " " + (item.nome ?? string.Empty)).ToLowerInvariant();
                if (texto.Contains("combust") || texto.Contains("gasolina") || texto.Contains("diesel")) return "Combustiveis";
                if (IndustriaIds.EhIndustrial(item.RecursoIdEfetivo)) return "Industrial";
                return "Outros";
        }
    }

    private int EstoqueMercadoDoTime(DadosItemMercado item, int teamId)
    {
        if (item != null && item.municaoMilitar)
        {
            SistemaGastosMilitares.GarantirInstancia();
            return SistemaGastosMilitares.Instancia != null
                ? SistemaGastosMilitares.Instancia.ObterEstoqueMunicao(teamId, item.idMunicaoMilitar)
                : 0;
        }
        if (item != null && item.equipamentoMilitar)
        {
            SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
            return mercado != null ? mercado.ObterEstoqueEquipamento(item, teamId) : 0;
        }
        if (item == null) return 0;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (teamId == TeamJogador && GerenciadorRecursos.Instancia != null
            && (item.recurso == RecursoMercado.Comida || item.recurso == RecursoMercado.Petroleo
                || item.recurso == RecursoMercado.Energia || item.recurso == RecursoMercado.Aco))
            return EstoqueReal(item.recurso);
        return governo != null ? Mathf.Max(0, governo.ObterEstoque(teamId, item.RecursoIdEfetivo)) : 0;
    }

    private int ContarRelacoes(Func<RelacaoPaisGoverno, bool> filtro)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return 0;
        HashSet<int> idsAtivos = new HashSet<int>(ObterNacoesAtivas(governo).Select(n => n.teamId));
        int teamJogador = TeamJogador;
        return governo.Relacoes.Count(r => r != null && r.Envolve(teamJogador, r.Outro(teamJogador)) && idsAtivos.Contains(r.Outro(teamJogador)) && filtro(r));
    }

    private void LinhaCards((string titulo, string valor, string estado)[] dados)
    {
        VisualElement linha = new VisualElement(); linha.AddToClassList("gov-stat-grid");
        foreach (var dado in dados)
        {
            VisualElement card = new VisualElement(); card.AddToClassList("gov-stat-card");
            Label titulo = new Label(dado.titulo); titulo.AddToClassList("gov-stat-label"); card.Add(titulo);
            Label valor = new Label(dado.valor); valor.AddToClassList("gov-stat-value"); valor.AddToClassList(dado.estado); card.Add(valor);
            linha.Add(card);
        }
        conteudo.Add(linha);
    }

    private void TabelaCabecalho(params string[] colunas)
    {
        VisualElement linha = new VisualElement(); linha.AddToClassList("gov-table-row"); linha.AddToClassList("header");
        foreach (string coluna in colunas) { Label label = new Label(coluna); label.AddToClassList("gov-table-cell"); linha.Add(label); }
        conteudo.Add(linha);
    }

    private void TabelaLinha(string a, string b, string c, string d, string e, Action clique = null)
    {
        VisualElement linha = new VisualElement(); linha.AddToClassList("gov-table-row");
        foreach (string valor in new[] { a, b, c, d })
        {
            Label label = new Label(valor);
            label.AddToClassList("gov-table-cell");
            if (clique != null)
            {
                label.pickingMode = PickingMode.Position;
                label.AddToClassList("gov-row-action-cell");
                label.AddManipulator(new Clickable(clique));
            }
            linha.Add(label);
        }
        if (clique == null) { Label label = new Label(e); label.AddToClassList("gov-table-cell"); linha.Add(label); }
        else { Button botao = new Button(clique) { text = e }; botao.AddToClassList("gov-table-cell"); botao.AddToClassList("gov-row-button"); linha.Add(botao); }
        conteudo.Add(linha);
    }

    private void AbrirDetalheLinha(string titulo, string detalhes)
    {
        if (conteudo == null) return;

        if (detalheAtual != null)
            detalheAtual.RemoveFromHierarchy();

        VisualElement card = new VisualElement();
        card.AddToClassList("gov-card");
        Label cabecalho = new Label(titulo);
        cabecalho.AddToClassList("gov-card-title");
        Label corpo = new Label(string.IsNullOrWhiteSpace(detalhes) ? "Detalhamento indisponivel." : detalhes);
        corpo.style.whiteSpace = WhiteSpace.Normal;
        card.Add(cabecalho);
        card.Add(corpo);
        conteudo.Add(card);
        detalheAtual = card;
    }

    private void TabelaPedidoAjuda(SistemaGovernoMundial gov, PropostaInternacional proposta)
    {
        VisualElement linha = new VisualElement();
        linha.AddToClassList("gov-table-row");
        foreach (string valor in new[]
        {
            gov.NomePais(proposta.origemTeamId),
            "MANTIMENTO",
            proposta.recurso.ToString(),
            proposta.quantidade.ToString("N0")
        })
        {
            Label label = new Label(valor);
            label.AddToClassList("gov-table-cell");
            linha.Add(label);
        }

        VisualElement botoes = new VisualElement();
        botoes.AddToClassList("gov-table-cell");
        botoes.style.flexDirection = FlexDirection.Row;
        string id = proposta.id;
        Button aceitar = new Button(() => ResolverPedidoAjuda(id, StatusPropostaInternacional.Aceita)) { text = "ACEITAR" };
        aceitar.AddToClassList("gov-row-button");
        Button recusar = new Button(() => ResolverPedidoAjuda(id, StatusPropostaInternacional.Recusada)) { text = "RECUSAR" };
        recusar.AddToClassList("gov-row-button");
        botoes.Add(aceitar);
        botoes.Add(recusar);
        linha.Add(botoes);
        conteudo.Add(linha);
    }

    private void TabelaPropostaDiplomatica(SistemaGovernoMundial gov, PropostaInternacional proposta)
    {
        VisualElement linha = new VisualElement();
        linha.AddToClassList("gov-table-row");
        foreach (string valor in new[]
        {
            gov.NomePais(proposta.origemTeamId),
            proposta.tipo == TipoPropostaInternacional.CessarFogo ? "CESSAR-FOGO" : proposta.tipo.ToString().ToUpperInvariant(),
            "DIPLOMACIA",
            "ANALISE"
        })
        {
            Label label = new Label(valor);
            label.AddToClassList("gov-table-cell");
            linha.Add(label);
        }

        VisualElement botoes = new VisualElement();
        botoes.AddToClassList("gov-table-cell");
        botoes.style.flexDirection = FlexDirection.Row;
        string id = proposta.id;
        Button aceitar = new Button(() => ResolverPedidoAjuda(id, StatusPropostaInternacional.Aceita)) { text = "ACEITAR" };
        aceitar.AddToClassList("gov-row-button");
        Button recusar = new Button(() => ResolverPedidoAjuda(id, StatusPropostaInternacional.Recusada)) { text = "RECUSAR" };
        recusar.AddToClassList("gov-row-button");
        botoes.Add(aceitar);
        botoes.Add(recusar);
        linha.Add(botoes);
        conteudo.Add(linha);
    }

    private void ResolverPedidoAjuda(string propostaId, StatusPropostaInternacional status)
    {
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        if (gov == null) return;
        string mensagem = "Operacao indisponivel.";
        bool ok = gov.ResolverProposta(propostaId, status, out mensagem);
        MostrarMensagem((ok ? "Pedido de ajuda: " : "Pedido de ajuda recusado: ") + mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void TabelaLinhaDuplaAcao(string a, string b, string c, string d, Action acaoMais, Action acaoMenos)
    {
        VisualElement linha = new VisualElement(); linha.AddToClassList("gov-table-row");
        foreach (string valor in new[] { a, b, c, d }) { Label label = new Label(valor); label.AddToClassList("gov-table-cell"); linha.Add(label); }
        
        VisualElement botoes = new VisualElement(); botoes.AddToClassList("gov-table-cell"); botoes.style.flexDirection = FlexDirection.Row;
        Button btnMenos = new Button(acaoMenos) { text = "-1%" }; btnMenos.AddToClassList("gov-row-button"); btnMenos.style.flexGrow = 1; botoes.Add(btnMenos);
        Button btnMais = new Button(acaoMais) { text = "+1%" }; btnMais.AddToClassList("gov-row-button"); btnMais.style.flexGrow = 1; botoes.Add(btnMais);
        
        linha.Add(botoes);
        conteudo.Add(linha);
    }

    private void CartaoNacao(DadosPaisGoverno n)
    {
        if (n == null) return;
        VisualElement card = new VisualElement();
        card.AddToClassList("gov-stat-card");
        card.Add(new Label(n.nomePais + "  |  " + n.nomePresidente) { name = "nation-title" });
        card.Add(new Label("Populacao: " + n.populacao.ToString("N0") + " | Militar: " + n.populacaoMilitarAtiva.ToString("N0")));
            card.Add(new Label("Caixa: " + Moeda(n.saldo) + " | Divida: " + Moeda(n.divida) + " | Estado: " + ObterStatusNacional(n)));
        card.Add(new Label("Moeda: " + n.nomeMoeda + " | 1 " + n.nomeMoeda + " = " + n.cambioComLider.ToString("0.00") + " DH"));
        card.Add(new Label("Federacao: " + SistemaFederacoesGlobais.NomeFederacao(n.federacaoGlobal)));
        if (n.teamId != TeamJogador && SistemaGovernoMundial.Instancia != null)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            PropostaInternacional pedido = gov.Propostas.FirstOrDefault(p => p != null && p.EstaPendente
                && p.tipo == TipoPropostaInternacional.PedidoAjuda && p.origemTeamId == n.teamId && p.alvoTeamId == TeamJogador);
            if (pedido != null)
            {
                Label alerta = new Label("PEDIDO DE AJUDA: " + pedido.recurso.ToString().ToUpperInvariant() + " x" + pedido.quantidade.ToString("N0"));
                alerta.AddToClassList("state-warn");
                card.Add(alerta);
            }
            PosturaRelacaoPais postura = gov.ObterPostura(TeamJogador, n.teamId);
            Label relacao = new Label("Postura bilateral: " + postura.ToString().ToUpperInvariant());
            relacao.AddToClassList("gov-section-title");
            card.Add(relacao);
            VisualElement posturaBotoes = new VisualElement();
            posturaBotoes.style.flexDirection = FlexDirection.Row;
            foreach (PosturaRelacaoPais opcao in new[] { PosturaRelacaoPais.Amigo, PosturaRelacaoPais.Neutro, PosturaRelacaoPais.Inimigo })
            {
                PosturaRelacaoPais escolha = opcao;
                Button botao = new Button(() => DefinirPosturaMenu(n.teamId, escolha)) { text = escolha.ToString().ToUpperInvariant() };
                botao.AddToClassList("gov-row-button");
                botao.SetEnabled(n.teamId != TeamJogador);
                if (escolha == postura) botao.AddToClassList("selected");
                posturaBotoes.Add(botao);
            }
            card.Add(posturaBotoes);
        }
        Button selecionar = new Button(() => { paisSelecionado = n.teamId; MostrarMensagem(n.teamId == TeamJogador ? "Este e o seu pais." : n.nomePais + " definido como alvo diplomatico."); MostrarPagina(abaAtual); }) { text = n.teamId == TeamJogador ? "SEU PAIS" : "SELECIONAR ALVO" };
        selecionar.AddToClassList("gov-row-button");
        selecionar.SetEnabled(n.teamId != TeamJogador);
        card.Add(selecionar);
        conteudo.Add(card);
    }

    private void DefinirPosturaMenu(int alvoTeamId, PosturaRelacaoPais postura)
    {
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        if (gov == null) return;
        string mensagem = "Relacao indisponivel.";
        bool ok = gov.DefinirPostura(TeamJogador, alvoTeamId, postura, out mensagem);
        MostrarMensagem((ok ? "Relacao atualizada: " : "Relacao recusada: ") + mensagem);
        MostrarPagina(abaAtual);
    }

    private void ConstruirRelacoes()
    {
        SistemaGovernoMundial g = SistemaGovernoMundial.Instancia;
        if (g == null) { AdicionarCard(conteudo, "DADOS INDISPONIVEIS", "Relacionamentos ainda nao foram inicializados."); return; }
        List<DadosPaisGoverno> nacoesAtivas = ObterNacoesAtivas(g);

        int positivas = 0, negativas = 0;
        foreach (RelacaoPaisGoverno r in g.Relacoes)
        {
            if (r == null || !r.Envolve(TeamJogador, r.Outro(TeamJogador))) continue;
            if (r.valor >= 25) positivas++;
            else if (r.valor < 0) negativas++;
        }

        if (abaAtual == "Resumo")
        {
            LinhaCards(new[] { ("INFLUENCIA", Mathf.Clamp(50 + positivas * 8 - negativas * 4, 0, 100) + "/100", "state-info"), ("TRATADOS ATIVOS", ContarRelacoes(r => r.tratadoComercial).ToString(), "state-good"), ("FOCOS DE TENSAO", negativas.ToString(), "state-bad"), ("PEDIDOS PENDENTES", g.ObterPropostasPendentesPara(TeamJogador).Count().ToString(), "state-warn") });
            DadosPaisGoverno selecionado = g.ObterPais(paisSelecionado);
            if (selecionado != null)
                AdicionarCard(conteudo, "PAIS SELECIONADO", selecionado.nomePais + "\nFederacao: " + SistemaFederacoesGlobais.NomeFederacao(selecionado.federacaoGlobal) + "\nPresidente: " + selecionado.nomePresidente + "\nStatus: " + ObterStatusNacional(selecionado));
        }
        else if (abaAtual == "Tratados")
        {
            LinhaCards(new[] { ("TRATADOS", ContarRelacoes(r => r.tratadoComercial).ToString(), "state-good"), ("PACTOS", ContarRelacoes(r => r.pactoMilitar).ToString(), "state-info"), ("RELACOES POSITIVAS", positivas.ToString(), "state-good"), ("PEDIDOS PENDENTES", g.ObterPropostasPendentesPara(TeamJogador).Count().ToString(), "state-warn") });
        }
        else if (abaAtual == "Crises")
        {
            LinhaCards(new[] { ("GUERRAS", nacoesAtivas.Count(p => p != null && p.emGuerra).ToString(), "state-bad"), ("SANCOES", nacoesAtivas.Count(p => p != null && p.sancionado).ToString(), "state-warn"), ("TENSAO", negativas.ToString(), "state-bad"), ("NOTICIAS", g.noticias.Count.ToString(), "state-info") });
        }
        else if (abaAtual == "Nacoes")
        {
            IReadOnlyList<DadosPaisGoverno> registro = nacoesAtivas;
            LinhaCards(new[] { ("NACOES REGISTRADAS", registro.Count.ToString(), "state-info"), ("POPULACAO", registro.Where(p => p != null).Sum(p => p.populacao).ToString("N0"), "state-good"), ("FEDERACOES", registro.Where(p => p != null && !string.IsNullOrWhiteSpace(p.federacaoGlobal)).Select(p => p.federacaoGlobal).Distinct().Count().ToString(), "state-info"), ("EM GUERRA", registro.Count(p => p != null && p.emGuerra).ToString(), "state-bad") });
            foreach (DadosPaisGoverno nacao in registro.Where(p => p != null).OrderBy(p => p.teamId))
                CartaoNacao(nacao);
            return;
        }
        else
        {
            LinhaCards(new[] { ("INFLUENCIA", Mathf.Clamp(50 + positivas * 8 - negativas * 4, 0, 100) + "/100", "state-info"), ("TRATADOS ATIVOS", ContarRelacoes(r => r.tratadoComercial).ToString(), "state-good"), ("FOCOS DE TENSAO", negativas.ToString(), "state-bad"), ("PEDIDOS PENDENTES", g.ObterPropostasPendentesPara(TeamJogador).Count().ToString(), "state-warn") });
        }

        if (abaAtual == "Crises")
        {
            TabelaCabecalho("PAIS", "TIPO", "RELACAO", "SITUACAO", "ACAO");
            foreach (DadosPaisGoverno outro in nacoesAtivas)
            {
                if (outro.teamId == TeamJogador) continue;
                RelacaoPaisGoverno r = g.ObterRelacao(TeamJogador, outro.teamId);
                if (r == null || (!r.guerraDeclarada && !r.sancaoAtiva && r.valor >= 0)) continue;
                string tipo = r.guerraDeclarada ? "GUERRA" : r.sancaoAtiva ? "SANCAO" : "TENSAO";
                TabelaLinha(outro.nomePais, outro.bloco, r.valor.ToString("+0;-0;0"), tipo, "SELECIONAR", () => { paisSelecionado = outro.teamId; MostrarMensagem(outro.nomePais + " selecionado."); MostrarPagina(abaAtual); });
            }
            AdicionarCard(conteudo, "HISTORICO RECENTE", UltimasNoticias(g, 5, "guerra", "sanc", "tens", "crise"));
            return;
        }

        if (abaAtual == "Tratados")
        {
            DadosPaisGoverno jogador = g.ObterPais(TeamJogador);
            AdicionarCard(conteudo, "CONCORDIA GLOBAL", "Cooperacao, ajuda humanitaria e protecao diplomatica. Filiacao atual: " + (jogador != null && jogador.federacaoGlobal == SistemaFederacoesGlobais.TipoFederacao.CooperacaoGlobal.ToString() ? "SIM" : "NAO"));
            AdicionarCard(conteudo, "COALIZAO AEGIS", "Defesa mutua, padroes militares e credito de armamento. Filiacao atual: " + (jogador != null && jogador.federacaoGlobal == SistemaFederacoesGlobais.TipoFederacao.AliancaDefesa.ToString() ? "SIM" : "NAO"));
            if (jogador != null)
                AdicionarCard(conteudo, "DOCUMENTO DE FILIACAO", jogador.nomePais + " / " + jogador.nomePresidente + "\nFederacao: " + SistemaFederacoesGlobais.NomeFederacao(jogador.federacaoGlobal) + "\nLegitimidade: " + jogador.legitimidadeGlobal.ToString("0") + "%\nMoeda de liquidacao internacional: Dolar Hegemonico (DH)");
            TabelaCabecalho("PAIS", "TRATADO", "PACTO", "PEDIDO", "RELACAO");
            foreach (DadosPaisGoverno outro in nacoesAtivas)
            {
                if (outro.teamId == TeamJogador) continue;
                RelacaoPaisGoverno r = g.ObterRelacao(TeamJogador, outro.teamId);
                TabelaLinha(outro.nomePais, r != null && r.tratadoComercial ? "SIM" : "NAO", r != null && r.pactoMilitar ? "SIM" : "NAO", r != null && r.pedidoPendente ? "SIM" : "NAO", r != null ? r.valor.ToString("+0;-0;0") : "0", () => { paisSelecionado = outro.teamId; MostrarMensagem(outro.nomePais + " selecionado."); MostrarPagina(abaAtual); });
            }
            return;
        }

        TabelaCabecalho("PAIS", "BLOCO", "RELACAO", "STATUS", "ACAO");
        foreach (DadosPaisGoverno outro in nacoesAtivas)
        {
            if (outro.teamId == TeamJogador) continue;
            RelacaoPaisGoverno r = g.ObterRelacao(TeamJogador, outro.teamId);
            string status = r != null && r.guerraDeclarada ? "GUERRA" : r != null && r.sancaoAtiva ? "SANCAO" : "PAZ";
            TabelaLinha(outro.nomePais, outro.bloco, r != null ? r.valor.ToString("+0;-0;0") : "0", status, "SELECIONAR", () => { paisSelecionado = outro.teamId; MostrarMensagem(outro.nomePais + " selecionado."); MostrarPagina(abaAtual); });
        }
    }

    private void ConstruirAliancas(DadosPaisGoverno pais)
    {
        SistemaGovernoMundial g = SistemaGovernoMundial.Instancia;
        if (g == null) { AdicionarCard(conteudo, "DADOS INDISPONIVEIS", "Aliancas ainda nao foram inicializadas."); return; }
        List<DadosPaisGoverno> nacoesAtivas = ObterNacoesAtivas(g);
        SistemaFederacoesGlobais.GarantirInstancia();

        List<DadosPaisGoverno> aliados = new List<DadosPaisGoverno>(g.ObterAliados(TeamJogador));
        if (abaAtual == "Blocos")
        {
            var blocos = nacoesAtivas
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.federacaoGlobal))
                .GroupBy(p => p.federacaoGlobal)
                .Select(grupo => new
                {
                    nome = grupo.Key,
                    membros = grupo.Count(),
                    forca = Mathf.RoundToInt((float)grupo.Average(p => p.nivelMilitar)),
                    confianca = Mathf.RoundToInt((float)grupo.Average(p => Mathf.Clamp((g.ObterRelacao(TeamJogador, p.teamId)?.valor ?? 0) + 50, 0, 100)))
                })
                .OrderByDescending(x => x.membros)
                .ToList();

            LinhaCards(new (string titulo, string valor, string estado)[] { ("FEDERACAO ATUAL", SistemaFederacoesGlobais.NomeFederacao(pais.federacaoGlobal), "state-warn"), ("BLOCOS ATIVOS", blocos.Count.ToString(), "state-info"), ("FORCA COMBINADA", Mathf.Clamp((float)(pais.nivelMilitar + aliados.Count * 6), 0f, 100f).ToString("0") + "%", "state-good"), ("RISCO DE RUPTURA", (100f - pais.legitimidadeGlobal).ToString("0") + "%", "state-bad") });
            TabelaCabecalho("BLOCO", "MEMBROS", "FORCA", "CONFIANCA", "ACAO");
            foreach (var bloco in blocos)
                TabelaLinha(bloco.nome, bloco.membros.ToString(), bloco.forca.ToString(), bloco.confianca.ToString() + "%", "SELECIONAR",
                    () => AbrirDetalheLinha("BLOCO: " + bloco.nome,
                        "Membros: " + bloco.membros + "\nForca conjunta: " + bloco.forca + "\nConfianca: " + bloco.confianca + "%"));
            return;
        }

        if (abaAtual == "Membros")
        {
            string federacao = pais.federacaoGlobal;
            List<DadosPaisGoverno> membros = nacoesAtivas.Where(p => p != null && p.federacaoGlobal == federacao).OrderBy(p => p.teamId).ToList();
            LinhaCards(new[] { ("FEDERACAO", SistemaFederacoesGlobais.NomeFederacao(federacao), "state-info"), ("MEMBROS", membros.Count.ToString(), "state-good"), ("LEGITIMIDADE", pais.legitimidadeGlobal.ToString("0") + "%", "state-warn") });
            TabelaCabecalho("PAIS", "PRESIDENTE", "LEGIT.", "MILITAR", "STATUS");
            foreach (DadosPaisGoverno membro in membros)
                TabelaLinha(membro.nomePais, membro.nomePresidente, membro.legitimidadeGlobal.ToString("0") + "%", membro.nivelMilitar.ToString("0"), ObterStatusNacional(membro));
            return;
        }

        if (abaAtual == "Pedidos")
        {
            HashSet<int> idsAtivos = new HashSet<int>(nacoesAtivas.Select(n => n.teamId));
            List<PropostaInternacional> pendentes = g.ObterPropostasPendentesPara(TeamJogador)
                .Where(proposta => proposta != null && idsAtivos.Contains(proposta.origemTeamId))
                .ToList();
            LinhaCards(new[] { ("PEDIDOS", pendentes.Count.ToString(), "state-info"), ("ALIANCAS", aliados.Count.ToString(), "state-good"), ("FORCA COMBINADA", Mathf.Clamp(pais.nivelMilitar + aliados.Count * 6, 0, 100) + "%", "state-good"), ("RISCO DE RUPTURA", (100f - pais.estabilidade).ToString("0") + "%", "state-bad") });
            TabelaCabecalho("ORIGEM", "TIPO", "RECURSO", "QTD", "STATUS");
            if (pendentes.Count == 0)
            {
                AdicionarCard(conteudo, "SEM PEDIDOS", "Nao ha propostas aguardando resposta neste momento.");
                return;
            }
            foreach (PropostaInternacional proposta in pendentes)
            {
                if (proposta.tipo == TipoPropostaInternacional.PedidoAjuda)
                    TabelaPedidoAjuda(g, proposta);
                else if (proposta.tipo == TipoPropostaInternacional.Alianca || proposta.tipo == TipoPropostaInternacional.PactoDefensivo || proposta.tipo == TipoPropostaInternacional.CessarFogo)
                    TabelaPropostaDiplomatica(g, proposta);
                else
                    TabelaLinha(g.NomePais(proposta.origemTeamId), proposta.tipo.ToString(), proposta.recurso.ToString(), proposta.quantidade.ToString(), "ANALISAR",
                        () => AbrirDetalheLinha("PEDIDO DE " + g.NomePais(proposta.origemTeamId),
                            "Tipo: " + proposta.tipo + "\nRecurso: " + proposta.recurso + "\nQuantidade: " + proposta.quantidade.ToString("N0")
                                + "\nPrioridade: " + proposta.prioridade + "\nMotivo: " + (string.IsNullOrWhiteSpace(proposta.motivo) ? "Nao informado." : proposta.motivo)));
            }
            return;
        }

        if (abaAtual == "Pactos")
        {
            LinhaCards(new[] { ("MEMBROS ALIADOS", aliados.Count.ToString(), "state-info"), ("PACTOS ATIVOS", ContarRelacoes(r => r.pactoMilitar).ToString(), "state-good"), ("TRATADOS", ContarRelacoes(r => r.tratadoComercial).ToString(), "state-good"), ("RISCO DE RUPTURA", (100f - pais.estabilidade).ToString("0") + "%", "state-bad") });
            TabelaCabecalho("PAIS", "TRATADO", "PACTO", "RELACAO", "ACAO");
            foreach (DadosPaisGoverno p in nacoesAtivas.Where(x => x != null && x.teamId != TeamJogador))
            {
                RelacaoPaisGoverno r = g.Relacoes.FirstOrDefault(rel => rel != null && rel.Envolve(TeamJogador, p.teamId));
                if (r == null || (!r.pactoMilitar && !r.tratadoComercial)) continue;
                TabelaLinha(p.nomePais, r.tratadoComercial ? "SIM" : "NAO", r.pactoMilitar ? "SIM" : "NAO", r.valor.ToString("+0;-0;0"), "SELECIONAR", () => { paisSelecionado = p.teamId; MostrarPagina(abaAtual); });
            }
            return;
        }

        LinhaCards(new[] { ("BLOCO ATUAL", pais.bloco, "state-warn"), ("MEMBROS ALIADOS", aliados.Count.ToString(), "state-info"), ("FORCA COMBINADA", Mathf.Clamp(pais.nivelMilitar + aliados.Count * 6, 0, 100) + "%", "state-good"), ("RISCO DE RUPTURA", (100f - pais.estabilidade).ToString("0") + "%", "state-bad") });
        TabelaCabecalho("PAIS", "PRONTO PARA APOIO", "LEALDADE", "PACTO", "ACAO");
        foreach (DadosPaisGoverno p in aliados)
        {
            RelacaoPaisGoverno r = g.ObterRelacao(TeamJogador, p.teamId);
            TabelaLinha(p.nomePais, p.nivelMilitar > 60 ? "ALTO" : "MEDIO", Mathf.Clamp((r != null ? r.valor : 0) + 50, 0, 100) + "%", r != null && r.pactoMilitar ? "ATIVO" : "NAO", "SELECIONAR", () => { paisSelecionado = p.teamId; MostrarPagina(abaAtual); });
        }
        if (aliados.Count == 0) AdicionarCard(conteudo, "SEM ALIANCA FORMAL", "Selecione uma nacao em Relacoes e envie uma proposta de alianca.");
    }

    private void ConstruirSancoes()
    {
        SistemaGovernoMundial g = SistemaGovernoMundial.Instancia;
        if (g == null) { AdicionarCard(conteudo, "DADOS INDISPONIVEIS", "Sancoes ainda nao foram inicializadas."); return; }
        List<DadosPaisGoverno> nacoesAtivas = ObterNacoesAtivas(g);

        SistemaFederacoesGlobais.GarantirInstancia();
        DadosPaisGoverno jogador = g.ObterPais(TeamJogador);
        int ativas = ContarRelacoes(r => r.sancaoAtiva);
            float pressaoAtiva = nacoesAtivas.Count > 1 ? ativas / (float)(nacoesAtivas.Count - 1) : 0f;
            LinhaCards(new[] { ("PAISES AFETADOS", ativas.ToString(), "state-info"), ("PRESSAO GLOBAL", (pressaoAtiva * 100f).ToString("0") + "%", "state-bad"), ("RISCO DE RETALIACAO", ativas > 1 ? "ALTO" : "BAIXO", "state-warn"), ("APOIO INTERNACIONAL", Mathf.Clamp(100 - ativas * 15, 0, 100) + "%", "state-good") });

        if (abaAtual == "Legitimidade")
        {
            LinhaCards(new[] { ("LEGITIMIDADE", jogador != null ? jogador.legitimidadeGlobal.ToString("0") + "%" : "N/D", jogador != null && jogador.legitimidadeGlobal >= 60f ? "state-good" : "state-bad"), ("SANCOES", ativas.ToString(), "state-warn"), ("FEDERACAO", jogador != null ? SistemaFederacoesGlobais.NomeFederacao(jogador.federacaoGlobal) : "N/D", "state-info") });
            AdicionarCard(conteudo, "CREDIBILIDADE GLOBAL", "A legitimidade e reduzida por sancoes, inadimplencia e troca de federacao. Ela influencia a permanencia dos membros e a estabilidade interna.");
            return;
        }
        if (abaAtual == "Emprestimos")
        {
            List<EmprestimoFederativoEstado> loans = jogador != null && jogador.emprestimos != null ? jogador.emprestimos.Where(x => x != null).ToList() : new List<EmprestimoFederativoEstado>();
            LinhaCards(new[] { ("EMPRESTIMOS", loans.Count.ToString(), loans.Any(x => !x.inadimplente) ? "state-warn" : "state-info"), ("DIVIDA", loans.Sum(x => x.saldoDevedor).ToString("N0"), loans.Any(x => x.inadimplente) ? "state-bad" : "state-warn") });
            foreach (EmprestimoFederativoEstado loan in loans)
                TabelaLinha(loan.id, "Credor " + loan.credorTeamId, loan.saldoDevedor.ToString("N0"), loan.inadimplente ? "INADIMPLENTE" : "ATIVO", "QUITAR", () => QuitarEmprestimo(loan.id));
            if (loans.Count == 0) AdicionarCard(conteudo, "SEM DIVIDA ATIVA", "Nenhum empréstimo federativo registrado para esta nação.");
            return;
        }
        if (abaAtual == "Historico")
        {
            var noticias = g.noticias.Where(n => n != null && (n.ToLowerInvariant().Contains("sanc") || n.ToLowerInvariant().Contains("bloque") || n.ToLowerInvariant().Contains("embarg") || n.ToLowerInvariant().Contains("guerra"))).Take(6).ToList();
            AdicionarCard(conteudo, "HISTORICO RECENTE", noticias.Count > 0 ? string.Join("\n", noticias) : "Nenhuma noticia relevante sobre sancoes.");
            return;
        }

        if (abaAtual == "Pressao")
        {
            AdicionarCard(conteudo, "PRESSAO GLOBAL", "Pais afetados: " + ativas + "\nIndice de pressao: " + (pressaoAtiva * 100f).ToString("0") + "%\nRisco de retaliacao: " + (ativas > 1 ? "ALTO" : "BAIXO") + "\nApoio internacional estimado: " + Mathf.Clamp(100 - ativas * 15, 0, 100) + "%");
        }
        else if (abaAtual == "Embargos")
        {
            AdicionarCard(conteudo, "EMBARGOS POR RECURSO", "Selecione um alvo ativo e use as acoes ao lado para bloquear ou liberar comida e petroleo. O mercado recusa a rota embargada e atualiza as cotacoes.");
        }

        TabelaCabecalho("PAIS", "SANCAO", "RELACAO", "RESPOSTA", "ACAO");
        foreach (DadosPaisGoverno p in nacoesAtivas)
        {
            if (p.teamId == TeamJogador) continue;
            RelacaoPaisGoverno r = g.ObterRelacao(TeamJogador, p.teamId);
            if (abaAtual == "Ativas" && (r == null || !r.sancaoAtiva)) continue;
            if (abaAtual == "Embargos" && (r == null || (!r.sancaoAtiva && r.valor >= 0))) continue;
            string embargoes = r != null && r.embargos != null && r.embargos.Count > 0 ? string.Join(",", r.embargos.Select(e => e.ToString())) : "NENHUM";
            TabelaLinha(p.nomePais, abaAtual == "Embargos" ? embargoes : (r != null && r.sancaoAtiva ? "ATIVA" : "INATIVA"), r != null ? r.valor.ToString() : "0", p.emGuerra ? "HOSTIL" : "ESTAVEL", "SELECIONAR", () => { paisSelecionado = p.teamId; MostrarPagina(abaAtual); });
        }
    }

    private void ConstruirOrcamentoInvestimento(DadosPaisGoverno pais)
    {
        OrcamentoInvestimentoNacional.NormalizarPercentuais(pais);
        slidersOrcamentoSetores.Clear();
        slidersOrcamentoForcas.Clear();
        valoresOrcamentoSetores.Clear();
        valoresOrcamentoForcas.Clear();

        VisualElement configuracao = new VisualElement();
        configuracao.AddToClassList("gov-card");
        configuracao.AddToClassList("gov-budget-card");
        Label titulo = new Label("ORÇAMENTO DIÁRIO DO PAÍS");
        titulo.AddToClassList("gov-card-title");
        configuracao.Add(titulo);
        Label explicacao = new Label("Defina o teto diário e distribua 100% entre os setores. O débito ocorre uma vez por dia e respeita o saldo operacional disponível.");
        explicacao.AddToClassList("gov-budget-description");
        configuracao.Add(explicacao);

        Toggle ativo = new Toggle("ATIVAR RATEIO E INVESTIMENTO DIÁRIO")
        {
            value = pais.orcamentoInvestimentoAtivo
        };
        ativo.AddToClassList("gov-budget-toggle");
        ativo.RegisterValueChangedCallback(evt =>
        {
            pais.orcamentoInvestimentoAtivo = evt.newValue;
            AtualizarResumoOrcamento(pais);
            SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
        });
        configuracao.Add(ativo);

        IntegerField totalMilhoes = new IntegerField("TETO DIÁRIO (MILHÕES)")
        {
            value = (int)Math.Min(int.MaxValue, Math.Max(0L, pais.orcamentoInvestimentoDiario) / 1_000_000L)
        };
        totalMilhoes.AddToClassList("gov-budget-total-field");
        totalMilhoes.RegisterValueChangedCallback(evt =>
        {
            long milhoes = Math.Max(0, evt.newValue);
            pais.orcamentoInvestimentoDiario = milhoes > long.MaxValue / 1_000_000L
                ? long.MaxValue : milhoes * 1_000_000L;
            SincronizarControlesOrcamento(pais);
            SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
        });
        configuracao.Add(totalMilhoes);

        resumoOrcamentoDiario = new Label();
        resumoOrcamentoDiario.AddToClassList("gov-budget-summary");
        configuracao.Add(resumoOrcamentoDiario);
        statusOrcamento = new Label();
        statusOrcamento.AddToClassList("gov-budget-status");
        configuracao.Add(statusOrcamento);
        conteudo.Add(configuracao);

        VisualElement setores = new VisualElement();
        setores.AddToClassList("gov-card");
        setores.AddToClassList("gov-budget-card");
        Label tituloSetores = new Label("DISTRIBUIÇÃO NACIONAL · 100%");
        tituloSetores.AddToClassList("gov-card-title");
        setores.Add(tituloSetores);
        string[] nomesSetores = { "AGRICULTURA E ALIMENTOS", "ENERGIA", "PETRÓLEO E MANUTENÇÃO", "INDÚSTRIA E RECURSOS", "PESQUISA E TECNOLOGIA", "MILITAR" };
        for (int i = 0; i < nomesSetores.Length; i++)
        {
            int indice = i;
            VisualElement linha = new VisualElement();
            linha.AddToClassList("gov-budget-row");
            Label nome = new Label(nomesSetores[i]);
            nome.AddToClassList("gov-budget-name");
            Label valor = new Label();
            valor.AddToClassList("gov-budget-value");
            SliderInt slider = new SliderInt { lowValue = 0, highValue = 100, value = OrcamentoInvestimentoNacional.ObterPercentualSetor(pais, i) };
            slider.AddToClassList("gov-budget-slider");
            slider.RegisterValueChangedCallback(evt =>
            {
                OrcamentoInvestimentoNacional.AjustarPercentualSetor(pais, indice, evt.newValue);
                SincronizarControlesOrcamento(pais);
                SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
            });
            slidersOrcamentoSetores.Add(slider);
            valoresOrcamentoSetores.Add(valor);
            linha.Add(nome);
            linha.Add(slider);
            linha.Add(valor);
            setores.Add(linha);
        }
        conteudo.Add(setores);

        VisualElement forcas = new VisualElement();
        forcas.AddToClassList("gov-card");
        forcas.AddToClassList("gov-budget-card");
        Label tituloForcas = new Label("DISTRIBUIÇÃO MILITAR · 100% DA VERBA MILITAR");
        tituloForcas.AddToClassList("gov-card-title");
        forcas.Add(tituloForcas);
        string[] nomesForcas = { "EXÉRCITO", "MARINHA", "AERONÁUTICA" };
        int[] percentuaisForcas = { pais.orcamentoPctExercito, pais.orcamentoPctMarinha, pais.orcamentoPctAerea };
        for (int i = 0; i < nomesForcas.Length; i++)
        {
            int indice = i;
            VisualElement linha = new VisualElement();
            linha.AddToClassList("gov-budget-row");
            Label nome = new Label(nomesForcas[i]);
            nome.AddToClassList("gov-budget-name");
            Label valor = new Label();
            valor.AddToClassList("gov-budget-value");
            SliderInt slider = new SliderInt { lowValue = 0, highValue = 100, value = percentuaisForcas[i] };
            slider.AddToClassList("gov-budget-slider");
            slider.RegisterValueChangedCallback(evt =>
            {
                OrcamentoInvestimentoNacional.AjustarPercentualForca(pais, indice, evt.newValue);
                SincronizarControlesOrcamento(pais);
                SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
            });
            slidersOrcamentoForcas.Add(slider);
            valoresOrcamentoForcas.Add(valor);
            linha.Add(nome);
            linha.Add(slider);
            linha.Add(valor);
            forcas.Add(linha);
        }
        conteudo.Add(forcas);
        AtualizarResumoOrcamento(pais);
        SincronizarControlesOrcamento(pais);
    }

    private void SincronizarControlesOrcamento(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        for (int i = 0; i < slidersOrcamentoSetores.Count; i++)
        {
            slidersOrcamentoSetores[i].SetValueWithoutNotify(OrcamentoInvestimentoNacional.ObterPercentualSetor(pais, i));
            valoresOrcamentoSetores[i].text = OrcamentoInvestimentoNacional.ObterPercentualSetor(pais, i)
                + "%  ·  " + Moeda(OrcamentoInvestimentoNacional.ObterValorSetor(pais, i)) + "/dia";
        }
        int[] percentuaisForcas = { pais.orcamentoPctExercito, pais.orcamentoPctMarinha, pais.orcamentoPctAerea };
        long[] valoresForcas =
        {
            (long)Math.Floor(pais.orcamentoInvestimentoDiario * (double)pais.orcamentoPctMilitar / 100d * pais.orcamentoPctExercito / 100d),
            (long)Math.Floor(pais.orcamentoInvestimentoDiario * (double)pais.orcamentoPctMilitar / 100d * pais.orcamentoPctMarinha / 100d),
            (long)Math.Floor(pais.orcamentoInvestimentoDiario * (double)pais.orcamentoPctMilitar / 100d * pais.orcamentoPctAerea / 100d)
        };
        for (int i = 0; i < slidersOrcamentoForcas.Count; i++)
        {
            slidersOrcamentoForcas[i].SetValueWithoutNotify(percentuaisForcas[i]);
            valoresOrcamentoForcas[i].text = percentuaisForcas[i] + "%  ·  " + Moeda(valoresForcas[i]) + "/dia";
        }
        AtualizarResumoOrcamento(pais);
    }

    private void AtualizarResumoOrcamento(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        if (resumoOrcamentoDiario != null)
            resumoOrcamentoDiario.text = "TETO CONFIGURADO: " + Moeda(pais.orcamentoInvestimentoDiario) + " / dia";
        if (statusOrcamento != null)
        {
            string estado = pais.orcamentoInvestimentoAtivo ? "ATIVO" : "PAUSADO";
            string gasto = pais.orcamentoGastoHoje > 0L
                ? " · debitado hoje: " + Moeda(pais.orcamentoGastoHoje)
                : " · próximo débito na virada do dia";
            statusOrcamento.text = estado + gasto;
        }
    }

    private void ConstruirIdentidade(DadosPaisGoverno pais)
    {
        AdicionarCard(conteudo, "CONFIGURE SEU PAÍS", "Esses dados identificam sua nação no Governo e na interface de recursos; a configuração entra no save da campanha.");
        VisualElement identidade = new VisualElement();
        identidade.AddToClassList("gov-card");
        identidade.AddToClassList("gov-identity-card");

        TextField nomePais = new TextField("NOME DO PAÍS") { value = pais.nomePais ?? string.Empty, maxLength = 48 };
        nomePais.AddToClassList("gov-identity-field");
        nomePais.RegisterValueChangedCallback(evt =>
        {
            pais.nomePais = evt.newValue;
            AtualizarCabecalho();
            SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
        });
        identidade.Add(nomePais);

        TextField nomeMoeda = new TextField("NOME DA MOEDA") { value = pais.nomeMoeda ?? string.Empty, maxLength = 32 };
        nomeMoeda.AddToClassList("gov-identity-field");
        nomeMoeda.RegisterValueChangedCallback(evt =>
        {
            pais.nomeMoeda = evt.newValue;
            AtualizarRecursos();
            SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
        });
        identidade.Add(nomeMoeda);

        TextField simboloMoeda = new TextField("SÍMBOLO MONETÁRIO") { value = pais.simboloMoeda ?? string.Empty, maxLength = 8 };
        simboloMoeda.AddToClassList("gov-identity-field");
        simboloMoeda.RegisterValueChangedCallback(evt =>
        {
            pais.simboloMoeda = evt.newValue;
            AtualizarRecursos();
            SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
        });
        identidade.Add(simboloMoeda);

        string[] modelos = { "Faixas horizontais", "Faixas verticais", "Faixa central", "Cruz central" };
        DropdownField modelo = new DropdownField("FORMATO DA BANDEIRA", new List<string>(modelos), Mathf.Clamp(pais.modeloBandeira, 0, modelos.Length - 1));
        modelo.AddToClassList("gov-identity-field");
        modelo.RegisterValueChangedCallback(evt =>
        {
            pais.modeloBandeira = Mathf.Max(0, modelo.choices.IndexOf(evt.newValue));
            PreencherBandeira(flagPreview, pais);
            AtualizarAssinaturaBandeira(pais);
            SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
        });
        identidade.Add(modelo);

        VisualElement bandeira = new VisualElement();
        bandeira.AddToClassList("gov-flag-large");
        PreencherBandeira(bandeira, pais);
        identidade.Add(bandeira);

        identidade.Add(CriarSeletorCorBandeira("COR PRINCIPAL", pais.corBandeiraPrimaria,
            cor => AtualizarCorBandeira(pais, bandeira, 0, cor)));
        identidade.Add(CriarSeletorCorBandeira("COR SECUNDÁRIA", pais.corBandeiraSecundaria,
            cor => AtualizarCorBandeira(pais, bandeira, 1, cor)));
        identidade.Add(CriarSeletorCorBandeira("COR DE DESTAQUE", pais.corBandeiraDestaque,
            cor => AtualizarCorBandeira(pais, bandeira, 2, cor)));
        conteudo.Add(identidade);
    }

    private DropdownField CriarSeletorCorBandeira(string rotulo, string corAtual, Action<Color> aoSelecionar)
    {
        string[] nomes = { "Azul oceano", "Azul profundo", "Verde floresta", "Vermelho", "Dourado", "Branco", "Grafite", "Violeta", "Coral" };
        string[] cores = { "#117C87", "#173B63", "#2F7D50", "#B23939", "#F5C451", "#F4F6F7", "#27323A", "#7651A8", "#E17852" };
        List<string> escolhas = new List<string>(nomes);
        List<string> valores = new List<string>(cores);
        string atual = string.IsNullOrWhiteSpace(corAtual) ? cores[0] : corAtual.Trim().ToUpperInvariant();
        int selecionado = valores.FindIndex(cor => string.Equals(cor, atual, StringComparison.OrdinalIgnoreCase));
        if (selecionado < 0)
        {
            escolhas.Add("Personalizada (" + atual + ")");
            valores.Add(atual);
            selecionado = valores.Count - 1;
        }

        DropdownField seletor = new DropdownField(rotulo, escolhas, selecionado);
        seletor.AddToClassList("gov-identity-color");
        seletor.RegisterValueChangedCallback(evt =>
        {
            int indice = seletor.choices.IndexOf(evt.newValue);
            if (indice < 0 || indice >= valores.Count) return;
            if (ColorUtility.TryParseHtmlString(valores[indice], out Color cor))
                aoSelecionar?.Invoke(cor);
        });
        return seletor;
    }

    private void AtualizarCorBandeira(DadosPaisGoverno pais, VisualElement preview, int indice, Color cor)
    {
        string hex = "#" + ColorUtility.ToHtmlStringRGB(cor);
        if (indice == 0) pais.corBandeiraPrimaria = hex;
        else if (indice == 1) pais.corBandeiraSecundaria = hex;
        else pais.corBandeiraDestaque = hex;
        PreencherBandeira(preview, pais);
        AtualizarCabecalho();
        SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
    }

    private void AtualizarAssinaturaBandeira(DadosPaisGoverno pais)
    {
        assinaturaBandeira = pais == null ? string.Empty
            : pais.modeloBandeira + "|" + pais.corBandeiraPrimaria + "|" + pais.corBandeiraSecundaria + "|" + pais.corBandeiraDestaque;
    }

    private void SalvarIdentidadeDoPais()
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(TeamJogador) : null;
        if (pais == null) return;
        pais.nomePais = string.IsNullOrWhiteSpace(pais.nomePais) ? string.Empty : pais.nomePais.Trim();
        pais.nomeMoeda = string.IsNullOrWhiteSpace(pais.nomeMoeda) ? string.Empty : pais.nomeMoeda.Trim();
        pais.simboloMoeda = string.IsNullOrWhiteSpace(pais.simboloMoeda) ? string.Empty : pais.simboloMoeda.Trim();
        if (pais.nomeMoeda.Length == 0 || pais.simboloMoeda.Length == 0)
        {
            MostrarMensagem("Informe o nome da moeda e seu símbolo antes de finalizar.");
            return;
        }
        pais.configuracaoInicialConcluida = true;
        governo.NotificarGovernoAtualizado();
        AtualizarCabecalho();
        AtualizarRecursos();
        MostrarMensagem("Identidade nacional salva na campanha.");
    }

    private static Color LerCorBandeira(string valor, Color padrao)
    {
        return !string.IsNullOrWhiteSpace(valor) && ColorUtility.TryParseHtmlString(valor, out Color cor)
            ? cor : padrao;
    }

    private static void PreencherBandeira(VisualElement alvo, DadosPaisGoverno pais)
    {
        if (alvo == null) return;
        alvo.Clear();
        Color primaria = LerCorBandeira(pais != null ? pais.corBandeiraPrimaria : null, new Color(0.07f, 0.49f, 0.53f));
        Color secundaria = LerCorBandeira(pais != null ? pais.corBandeiraSecundaria : null, new Color(0.96f, 0.77f, 0.31f));
        Color destaque = LerCorBandeira(pais != null ? pais.corBandeiraDestaque : null, new Color(0.96f, 0.97f, 0.98f));
        int modelo = pais != null ? Mathf.Clamp(pais.modeloBandeira, 0, 3) : 0;
        alvo.style.backgroundColor = primaria;
        alvo.style.flexDirection = modelo == 1 ? FlexDirection.Row : FlexDirection.Column;
        if (modelo == 0 || modelo == 1)
        {
            foreach (Color cor in new[] { primaria, secundaria, destaque })
            {
                VisualElement faixa = new VisualElement();
                faixa.style.flexGrow = 1f;
                faixa.style.backgroundColor = cor;
                alvo.Add(faixa);
            }
            return;
        }

        if (modelo == 2)
        {
            VisualElement faixa = new VisualElement();
            faixa.style.position = Position.Absolute;
            faixa.style.left = 0;
            faixa.style.right = 0;
            faixa.style.top = Length.Percent(36f);
            faixa.style.height = Length.Percent(28f);
            faixa.style.backgroundColor = secundaria;
            alvo.Add(faixa);
            Label emblema = new Label("✦");
            emblema.style.position = Position.Absolute;
            emblema.style.left = Length.Percent(40f);
            emblema.style.top = Length.Percent(18f);
            emblema.style.width = Length.Percent(20f);
            emblema.style.height = Length.Percent(64f);
            emblema.style.unityTextAlign = TextAnchor.MiddleCenter;
            emblema.style.color = destaque;
            emblema.style.fontSize = 22;
            alvo.Add(emblema);
            return;
        }

        VisualElement coluna = new VisualElement();
        coluna.style.position = Position.Absolute;
        coluna.style.left = Length.Percent(42f);
        coluna.style.top = 0;
        coluna.style.width = Length.Percent(16f);
        coluna.style.bottom = 0;
        coluna.style.backgroundColor = secundaria;
        alvo.Add(coluna);
        VisualElement barra = new VisualElement();
        barra.style.position = Position.Absolute;
        barra.style.left = 0;
        barra.style.right = 0;
        barra.style.top = Length.Percent(40f);
        barra.style.height = Length.Percent(20f);
        barra.style.backgroundColor = destaque;
        alvo.Add(barra);
    }

    private void ConstruirEconomia(DadosPaisGoverno p)
    {
        SistemaEconomiaImoveis sistemaEconomia = SistemaEconomiaImoveis.Instancia;
        DadosEconomiaPais eco = sistemaEconomia != null ? sistemaEconomia.ObterEconomia(p.teamId) : null;

        if (abaAtual == "Gastos")
        {
            ConstruirGastosMilitares(p);
            return;
        }

        if (abaAtual == "Tesouro")
        {
            LinhaCards(new[]
            {
                ("SALDO", Moeda(p.saldo), "state-good"),
                ("RECEITA", Moeda(p.rendaPorSegundo) + "/s", "state-good"),
                ("GASTOS", Moeda(p.custoManutencao) + "/s", "state-bad"),
                ("RESERVA", Moeda(p.reservaOuro), "state-info"),
                ("PODER DE COMPRA", p.PoderDeCompra.ToString("0.00"), "state-info"),
                ("INFLACAO", p.inflacao.ToString("0.0") + "%", p.inflacao >= 8f ? "state-warn" : "state-good")
            });

            AdicionarCard(conteudo, "TESOURO NACIONAL",
                "Saldo atual: " + Moeda(p.saldo) + "\n" +
                "Receita recorrente: " + Moeda(p.rendaPorSegundo) + "/s\n" +
                "Gasto recorrente: " + Moeda(p.custoManutencao) + "/s\n" +
                "Saldo operacional: " + Moeda(p.saldoOperacional) + "/s\n" +
                "Reserva de ouro: " + Moeda(p.reservaOuro) + "\n" +
                "Moeda de referencia: " + p.moedaLiderReferencia + "\n" +
                "Cambio com lider: " + p.cambioComLider.ToString("0.00"));
            TabelaCabecalho("INDICADOR", "VALOR", "BASE", "TENDENCIA", "STATUS");
            TabelaLinha("CAIXA", Moeda(p.saldo), "Liquidez imediata", p.saldoOperacional >= 0f ? "SUPERAVIT" : "DEFICIT", p.saldo > 1000 ? "SAUDAVEL" : "RISCO");
            TabelaLinha("PODER", p.PoderDeCompra.ToString("0.00"), "Indice real", "Inflacao " + p.inflacao.ToString("0.0") + "%", p.PoderDeCompra > 0.75f ? "BOM" : "FRACO");
            TabelaLinha("RESERVA", p.reservaOuro.ToString("N0"), "Ativo financeiro", "Cambio " + p.cambioComLider.ToString("0.00"), p.reservaOuro > 300f ? "OK" : "BAIXA");
            return;
        }

        if (abaAtual == "Orcamento")
        {
            ConstruirOrcamentoInvestimento(p);
            RelatorioOrcamentoNacional rel = SistemaOrcamentoNacional.ObterOuCriar().GerarRelatorio(p != null ? p.teamId : TeamJogador);

            LinhaCards(new[]
            {
                ("RECEITA TOTAL/DIA", Moeda(rel.receitaTotalDia), "state-good"),
                ("DESPESA TOTAL/DIA", Moeda(rel.despesaTotalDia), "state-bad"),
                ("SALDO LÍQUIDO/DIA", Moeda(rel.saldoLiquidoDia), rel.saldoLiquidoDia >= 0m ? "state-good" : "state-bad"),
                ("PROJEÇÃO MENSAL", Moeda(rel.projecaoMensal), rel.projecaoMensal >= 0m ? "state-good" : "state-bad"),
                ("TESOURO ATUAL", Moeda(rel.tesouroAtual), rel.tesouroAtual > 1000m ? "state-good" : "state-warn"),
                ("DÍVIDA TOTAL", Moeda(rel.dividaTotal), rel.dividaTotal > 0f ? "state-bad" : "state-good"),
                ("INFLAÇÃO", rel.inflacao.ToString("0.0") + "%", rel.inflacao <= 5f ? "state-good" : "state-warn"),
                ("CARGA FISCAL", rel.cargaFiscalMedia.ToString("0.0") + "%", rel.cargaFiscalMedia <= 18f ? "state-good" : "state-warn")
            });

            AdicionarCard(conteudo, "BALANÇO GERAL DE RECEITAS",
                "Consolidação completa de receitas estatais. Clique em qualquer linha para inspecionar a fórmula e origem dos dados.");
            TabelaCabecalho("RECEITA", "BASE DE CÁLCULO", "VALOR / DIA", "TENDÊNCIA", "STATUS");
            foreach (var rec in rel.receitas)
            {
                string info = rec.detalhamento;
                TabelaLinha(rec.nome, rec.baseCalculo, "+" + Moeda(rec.valorDiario) + "/dia", rec.tendencia, rec.status,
                    () => AbrirDetalheLinha("RECEITA: " + rec.nome, info));
            }

            AdicionarCard(conteudo, "BALANÇO GERAL DE DESPESAS",
                "Consolidação detalhada dos custos operacionais do país. Clique em qualquer linha para inspecionar os detalhes de cálculo.");
            TabelaCabecalho("DESPESA", "BASE DE CÁLCULO", "VALOR / DIA", "TENDÊNCIA", "STATUS");
            foreach (var desp in rel.despesas)
            {
                string info = desp.detalhamento;
                TabelaLinha(desp.nome, desp.baseCalculo, Moeda(desp.valorDiario) + "/dia", desp.tendencia, desp.status,
                    () => AbrirDetalheLinha("DESPESA: " + desp.nome, info));
            }
            return;
        }

        if (abaAtual == "Producao")
        {
            float comidaAgricolaAtual = Mathf.Max(0f, p.producaoAgricolaDiaria);
            float comidaNoSnapshot = eco != null ? Mathf.Max(0f, eco.comidaProduzida) : 0f;
            float comida = Mathf.Max(comidaAgricolaAtual, comidaNoSnapshot);
            SistemaGovernoMundial governoAtual = SistemaGovernoMundial.Instancia;
            AgriculturaNacional sistemaAgricola = governoAtual != null
                ? governoAtual.GetComponent<AgriculturaNacional>() : null;
            float investimentoAplicadoAtual = p.investimentoAgricolaPercentualAtivo
                ? AgriculturaNacional.CalcularBonusInvestimentoPercentual(p.ultimaCobrancaAgricola)
                : AgriculturaNacional.CalcularBonusAplicadoInvestimentoFixo(
                    p.investimentoAgricolaSemanal, p.ultimaCobrancaAgricola, p.investimentoAgricolaSemRecursos);
            int colheitaPrevista = sistemaAgricola != null
                ? sistemaAgricola.EstimarProducaoDiaria(p, investimentoAplicadoAtual)
                : 0;
            float necessidadeComidaNacional = Mathf.Max(0, p.populacaoCivil) / 100f
                + Mathf.Max(0, p.populacaoMilitarAtiva) / 100f * 2f;
            float consumoComida = Mathf.Max(necessidadeComidaNacional,
                eco != null ? Mathf.Max(0f, eco.comidaConsumida) : 0f);
            float saldoLiquidoComidaDiario = comida - consumoComida;
            string saldoLiquidoComidaTexto = (saldoLiquidoComidaDiario > 0f ? "+" : string.Empty)
                + saldoLiquidoComidaDiario.ToString("N1") + " t/dia";
            int estoqueComidaCabecalho = EstoqueReal(RecursoMercado.Comida);
            int capacidadeLivreComida = CapacidadeLivreComida(p, estoqueComidaCabecalho);
            float petroleo = eco != null ? eco.petroleoProduzido : p.petroleo;
            float industria = eco != null ? eco.industriaProduzida : p.producao;
            float energia = eco != null ? eco.energiaProduzida : p.energia;
            float consumoEnergia = eco != null ? eco.energiaConsumida : p.gastosPorSegundo;
            float deficitComida = Mathf.Max(0f, consumoComida - comida);
            float deficitEnergia = eco != null ? eco.deficitEnergia : p.deficitEnergia;
            float deficitPetroleo = eco != null ? eco.deficitPetroleo : p.deficitPetroleo;
            string principal = eco != null ? eco.ProducaoPrincipal : "N/D";

            LinhaCards(new[]
            {
                ("ESTOQUE COMIDA", estoqueComidaCabecalho.ToString("N0") + " t", estoqueComidaCabecalho > 0 ? "state-good" : "state-warn"),
                ("PRODUÇÃO ALIMENTAR/DIA", comida.ToString("N0") + " t", comida > 0f ? "state-good" : "state-warn"),
                ("COLHEITA PREVISTA/DIA", colheitaPrevista.ToString("N0") + " t", colheitaPrevista > 0 ? "state-good" : "state-warn"),
                ("CONSUMO ALIMENTAR/DIA", consumoComida.ToString("N0") + " t", deficitComida <= 0f ? "state-good" : "state-warn"),
                ("SALDO LÍQUIDO/DIA", saldoLiquidoComidaTexto, saldoLiquidoComidaDiario >= 0f ? "state-good" : "state-bad"),
                ("PETROLEO", petroleo.ToString("0.0"), deficitPetroleo <= 0f ? "state-good" : "state-warn"),
                ("ENERGIA", energia.ToString("0.0"), deficitEnergia <= 0f ? "state-good" : "state-bad"),
                ("INDUSTRIA", industria.ToString("0.0"), "state-info"),
                ("PRINCIPAL", principal, "state-warn")
            });

            AdicionarCard(conteudo, "CICLO DIÁRIO DA SAFRA",
                DescreverCicloAgricola(p, sistemaAgricola, capacidadeLivreComida, colheitaPrevista));

            AdicionarCard(conteudo, "PRODUCAO NACIONAL",
                "A producao agora reflete o snapshot do motor economico.\n" +
                "Os deficits em comida, energia e petroleo afetam o saldo operacional, a qualidade de vida e a estabilidade.");
            int nivelProducaoAlimentos = p.niveisInvestimentoAgricola != null
                && p.niveisInvestimentoAgricola.Length > (int)AreaInvestimentoAgricola.Producao
                ? p.niveisInvestimentoAgricola[(int)AreaInvestimentoAgricola.Producao]
                : 0;
            long custoProximoInvestimento = AgriculturaNacional.CalcularCobrancaFixaSemanal(100 + nivelProducaoAlimentos * 50);
            int estoqueComidaReal = EstoqueReal(RecursoMercado.Comida);
            SistemaMercadoGlobal mercadoAtual = SistemaMercadoGlobal.Instancia;
            DadosItemMercado itemComida = mercadoAtual != null
                ? mercadoAtual.ObterItem("comida")
                : null;
            int aguaAgricola = governoAtual != null
                ? governoAtual.ObterEstoque(p.teamId, RecursoMercado.Agua)
                : 0;
            int sementesAgricolas = governoAtual != null ? governoAtual.ObterEstoque(p.teamId, "sementes") : 0;
            int fertilizanteAgricola = governoAtual != null ? governoAtual.ObterEstoque(p.teamId, "fertilizante_organico") : 0;
            int agrotoxicosAgricolas = governoAtual != null ? governoAtual.ObterEstoque(p.teamId, "agrotoxicos") : 0;
            float industriaAlimentarDiaria = eco != null
                ? Mathf.Max(0f, comidaNoSnapshot - comidaAgricolaAtual)
                : 0f;
            float investimentoPercentual = p.investimentoAgricolaPercentualAtivo
                ? p.investimentoAgricolaPercentualSemanal : 0f;
            long saldoOperacionalAgricola = governoAtual != null
                ? governoAtual.ObterSaldoDisponivelParaOperacao(p.teamId) : 0L;
            long cobrancaEstimada = p.investimentoAgricolaPercentualAtivo
                ? AgriculturaNacional.CalcularCobrancaSemanal(saldoOperacionalAgricola, investimentoPercentual)
                : AgriculturaNacional.CalcularCobrancaFixaSemanal(p.investimentoAgricolaSemanal);
            List<string> limitadoresAgricolas = new List<string>();
            if (aguaAgricola <= 0) limitadoresAgricolas.Add("água zerada");
            if (sementesAgricolas <= 0) limitadoresAgricolas.Add("sementes zeradas");
            if (fertilizanteAgricola <= 0) limitadoresAgricolas.Add("fertilizante zerado");
            if (agrotoxicosAgricolas <= 0) limitadoresAgricolas.Add("agrotóxicos zerados");
            if (p.investimentoAgricolaSemRecursos) limitadoresAgricolas.Add("saldo insuficiente para a cobrança semanal");
            if (capacidadeLivreComida <= 0) limitadoresAgricolas.Add("armazém de alimentos cheio");
            string situacaoAgricola = capacidadeLivreComida <= 0
                ? "Colheita bloqueada: o armazém está sem espaço."
                : limitadoresAgricolas.Count > 0
                    ? "Safra diária ativa em modo de sequeiro; limitações: " + string.Join(", ", limitadoresAgricolas) + "."
                    : "Safra diária ativa; água e insumos disponíveis para aumentar o rendimento.";
            AdicionarCard(conteudo, "AGRICULTURA E ALIMENTOS",
                "Estoque atual: " + estoqueComidaReal.ToString("N0") + " t | espaco livre: " + capacidadeLivreComida.ToString("N0") + " t\n" +
                "Producao agricola: " + p.producaoAgricolaDiaria.ToString("N0") + " t/dia | fabricas alimenticias: " + industriaAlimentarDiaria.ToString("N0") + " t/dia\n" +
                (p.producaoAgricolaDiaria <= 0 && colheitaPrevista > 0
                    ? "Previsao para a proxima colheita: " + colheitaPrevista.ToString("N0") + " t/dia. A producao confirmada atualiza na virada do dia.\n"
                    : string.Empty) +
                "Saldo diário após consumo: " + saldoLiquidoComidaTexto + (saldoLiquidoComidaDiario < 0f ? " (o estoque diminui)" : " (o estoque cresce ou se mantém)") + "\n" +
                "Situação da safra: " + situacaoAgricola + "\n" +
                "Insumos: água " + aguaAgricola.ToString("N0") + "/" + Mathf.Max(0, p.aguaMaxima).ToString("N0") + " t | sementes " + sementesAgricolas.ToString("N0") + " t | fertilizante " + fertilizanteAgricola.ToString("N0") + " t | agrotóxicos " + agrotoxicosAgricolas.ToString("N0") + " t\n" +
                "Nivel de capacidade: " + nivelProducaoAlimentos + "/10\n" +
                "Proxima ampliacao: " + Moeda(custoProximoInvestimento) + " (+5 t/dia nominais)\n" +
                "Investimento recorrente: " + (p.investimentoAgricolaPercentualAtivo
                    ? investimentoPercentual.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR")) + "% semanal (~" + Moeda(cobrancaEstimada) + "/semana) | ultima cobranca: " + Moeda(p.ultimaCobrancaAgricola)
                    : Moeda(cobrancaEstimada) + " semanal (modo fixo)") + (p.investimentoAgricolaSemRecursos ? " | saldo insuficiente na cobranca" : "") + "\n" +
                "Preco atual: " + (itemComida != null ? Moeda(itemComida.precoAtual) + "/t, mais frete estimado de 5%" : "indisponivel") +
                "\nA produção ocorre automaticamente em cada virada diária. Mesmo sem água e insumos, a lavoura mantém 30% de rendimento de sequeiro; recursos e investimento elevam a safra até a capacidade disponível.");
            TabelaCabecalho("RECURSO", "PRODUCAO", "CONSUMO", "DEFICIT", "STATUS");
            TabelaLinha("COMIDA", comida.ToString("0.0"), consumoComida.ToString("0.0"), deficitComida.ToString("0.0"), deficitComida <= 0f ? "SUPRIDA" : "FALTA");
            TabelaLinha("ENERGIA", energia.ToString("0.0"), consumoEnergia.ToString("0.0"), deficitEnergia.ToString("0.0"), deficitEnergia <= 0f ? "SUPRIDA" : "FALTA");
            // O consumo exibido precisa vir do mesmo livro-caixa que atualiza a economia.
            // Nunca derive um valor fictício da produção industrial: combustivelConsumido
            // é acumulado pelo SistemaEconomiaImoveis a partir das estruturas/unidades ativas.
            float consumoPetroleo = eco != null ? eco.combustivelConsumido : 0f;
            TabelaLinha("PETROLEO", petroleo.ToString("0.0"), consumoPetroleo.ToString("0.0"), deficitPetroleo.ToString("0.0"), deficitPetroleo <= 0f ? "SUPRIDA" : "FALTA");
            TabelaLinha("INDUSTRIA", industria.ToString("0.0"), "-", p.nivelIndustrial.ToString("0"), p.nivelIndustrial >= 50 ? "FORTE" : "FRACA");
            ConstruirControleCulturas(p, sistemaAgricola);
            return;
        }

        LinhaCards(new[]
        {
            ("MORADIA", p.impostoMoradia + "%", p.impostoMoradia <= 12 ? "state-good" : "state-warn"),
            ("INDUSTRIA", p.impostoIndustria + "%", p.impostoIndustria <= 15 ? "state-good" : "state-warn"),
            ("COMERCIO", p.impostoComercio + "%", p.impostoComercio <= 12 ? "state-good" : "state-warn"),
            ("CUSTO DE VIDA", p.inflacao.ToString("0.0") + "%", p.inflacao < 6f ? "state-good" : "state-warn"),
            ("PODER DE COMPRA", p.PoderDeCompra.ToString("0.00"), "state-info"),
            ("SALDO", Moeda(p.saldo), "state-info")
        });
        DadosComercioNacional comercio = SistemaComercioNacional.ObterResumo(p.teamId);
        LinhaCards(new[]
        {
            ("PREDIOS COMERCIAIS", comercio.prediosComerciais.ToString(), "state-info"),
            ("ATIVOS / PARADOS", comercio.estabelecimentosAtivos + " / " + comercio.estabelecimentosParados, comercio.estabelecimentosParados == 0 ? "state-good" : "state-warn"),
            ("EMPREGOS", comercio.trabalhadoresContratados + "/" + comercio.empregosCriados, "state-good"),
            ("FELICIDADE", "+" + comercio.contribuicaoFelicidade.ToString("0.0") + "/10", "state-good"),
            ("ATRATIVIDADE", comercio.capacidadeAtracao.ToString("0") + "%", "state-info")
        });
        AdicionarCard(conteudo, "POLITICA FISCAL",
            "Impostos mais altos elevam a arrecadacao, mas tambem pressionam moradia, industria e comercio.\n" +
            "O ideal e equilibrar receita, estabilidade e poder de compra sem travar a producao.");
        AdicionarCard(conteudo, "COMERCIO E EMPREGOS",
            "Predios: " + comercio.prediosComerciais + " | Ativos: " + comercio.estabelecimentosAtivos + " | Parados: " + comercio.estabelecimentosParados + "\n" +
            "Porte: pequenos " + comercio.prediosPequenos + " | medios " + comercio.prediosMedios + " | grandes " + comercio.prediosGrandes + "\n" +
            "Capacidade: " + comercio.capacidadeTotalEmpresas + " empresas | Abertas: " + comercio.empresasAtivas + " | Vazios: " + comercio.espacosComerciaisVazios + " | Ocupacao: " + (comercio.taxaOcupacaoPredios * 100f).ToString("0") + "%\n" +
            "Abertas recentemente: " + comercio.empresasAbertasRecentemente + " | Fechadas recentemente: " + comercio.empresasFechadasRecentemente + "\n" +
            "Demanda nao atendida: " + comercio.demandaNaoAtendida + " | Excesso de comercio: " + comercio.regioesComExcessoComercio + "\n" +
            "Empregos criados: " + comercio.empregosCriados + " | Vagas: " + comercio.vagasDisponiveis + " | Contratados: " + comercio.trabalhadoresContratados + "\n" +
            "Salarios: " + Moeda(comercio.salariosPagos) + "/s | Impostos: " + Moeda(comercio.impostosArrecadados) + "/s\n" +
            "Mercadorias: " + comercio.mercadoriasDisponiveis + " | Consumo: " + Moeda(comercio.consumoPopulacao) + "/s | Lucro: " + Moeda(comercio.lucroTotal) + "/s\n" +
            "Felicidade: +" + comercio.contribuicaoFelicidade.ToString("0.0") + "/10 | Atracao de moradores: " + comercio.capacidadeAtracao.ToString("0") + "%\n" +
            "Empresas por categoria: " + string.Join(", ", comercio.empresasPorCategoria.Keys) + "\n" +
            (comercio.estabelecimentosParados > 0 ? "Principal motivo de parada: " + comercio.principalMotivoParada : "Todos os estabelecimentos estao funcionando."));
        if (ComercioLocal.PredioSelecionado != null)
        {
            ComercioLocal predio = ComercioLocal.PredioSelecionado;
            AdicionarCard(conteudo, "PREDIO COMERCIAL SELECIONADO", predio.GerarDetalhePredio());
            TabelaCabecalho("EMPRESA", "CATEGORIA", "FUNCIONARIOS", "VAGAS", "ESTADO");
            int limiteEmpresasVisiveis = Mathf.Min(12, predio.Empresas.Count);
            for (int i = 0; i < limiteEmpresasVisiveis; i++)
            {
                EmpresaComercial empresa = predio.Empresas[i];
                string motivoEmpresa = string.IsNullOrEmpty(empresa.motivo) ? "Funcionamento normal" : empresa.motivo;
                TabelaLinha("Empresa " + (i + 1), empresa.tipo.ToString(), empresa.funcionariosContratados.ToString(), empresa.vagasAbertas.ToString(), empresa.estado,
                    () => AbrirDetalheLinha("EMPRESA " + (i + 1), "Tipo: " + empresa.tipo + "\nEstado: " + empresa.estado
                        + "\nFuncionarios: " + empresa.funcionariosContratados + "\nVagas abertas: " + empresa.vagasAbertas
                        + "\nDiagnostico: " + motivoEmpresa));
            }
        }
        TabelaCabecalho("IMPOSTO", "ATUAL", "EFEITO", "AJUSTE", "AÇÕES");
        
        TabelaLinhaDuplaAcao("MORADIA", p.impostoMoradia + "%", "Descontentamento: " + p.descontentamentoMoradia.ToString("0") + "%", p.impostoMoradia < 20 ? "LEVE" : "ALTO",
            () => AjustarImpostoIndividual("Moradia", 1), () => AjustarImpostoIndividual("Moradia", -1));
            
        TabelaLinhaDuplaAcao("INDUSTRIA", p.impostoIndustria + "%", "Descontentamento: " + p.descontentamentoIndustria.ToString("0") + "%", p.impostoIndustria < 20 ? "LEVE" : "ALTO",
            () => AjustarImpostoIndividual("Industria", 1), () => AjustarImpostoIndividual("Industria", -1));
            
        TabelaLinhaDuplaAcao("COMERCIO", p.impostoComercio + "%", "Descontentamento: " + p.descontentamentoComercio.ToString("0") + "%", p.impostoComercio < 18 ? "LEVE" : "ALTO",
            () => AjustarImpostoIndividual("Comercio", 1), () => AjustarImpostoIndividual("Comercio", -1));
    }

    private void ConstruirControleCulturas(DadosPaisGoverno pais, AgriculturaNacional agricultura)
    {
        if (pais == null) return;
        AdicionarCard(conteudo, "CULTURAS E ESTOQUE POR ALIMENTO",
            "A divisao configurada afeta as proximas colheitas. Priorizar arroz, milho, feijao ou outra cultura redistribui a participacao restante proporcionalmente.");
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        List<string> nomes = new List<string>();
        for (int i = 0; i < AgriculturaNacional.QuantidadeCulturas; i++)
            nomes.Add(AgriculturaNacional.ObterNomeCultura(AgriculturaNacional.ObterIdCultura(i)));

        VisualElement controle = new VisualElement();
        controle.AddToClassList("card");
        controle.AddToClassList("gov-culture-control");
        int indiceInicial = 0;
        if (pais.mixCulturaAgricola != null)
            for (int i = 1; i < Mathf.Min(pais.mixCulturaAgricola.Length, nomes.Count); i++)
                if (pais.mixCulturaAgricola[i] > pais.mixCulturaAgricola[indiceInicial]) indiceInicial = i;
        DropdownField cultura = new DropdownField("Cultura", nomes, indiceInicial);
        cultura.AddToClassList("gov-culture-select");
        SliderInt percentual = new SliderInt("Participação da cultura (%)", 0, 100);
        percentual.AddToClassList("gov-share-slider");
        percentual.value = pais.mixCulturaAgricola != null && pais.mixCulturaAgricola.Length > indiceInicial
            ? Mathf.RoundToInt(pais.mixCulturaAgricola[indiceInicial] * 100f) : 7;
        Label valorPercentual = new Label(percentual.value + "%");
        valorPercentual.AddToClassList("gov-share-value");
        VisualElement linhaPercentual = new VisualElement();
        linhaPercentual.AddToClassList("gov-share-row");
        linhaPercentual.Add(percentual);
        linhaPercentual.Add(valorPercentual);
        percentual.RegisterValueChangedCallback(evt => valorPercentual.text = evt.newValue + "%");
        cultura.RegisterValueChangedCallback(evt =>
        {
            int selecionada = Mathf.Max(0, nomes.IndexOf(evt.newValue));
            percentual.SetValueWithoutNotify(pais.mixCulturaAgricola != null && pais.mixCulturaAgricola.Length > selecionada
                ? Mathf.RoundToInt(pais.mixCulturaAgricola[selecionada] * 100f) : 0);
            valorPercentual.text = percentual.value + "%";
        });
        VisualElement predefinicoes = new VisualElement();
        predefinicoes.AddToClassList("gov-share-presets");
        foreach (int valorPresetInicial in new[] { 25, 50, 75, 100 })
        {
            int valorPreset = valorPresetInicial;
            Button atalho = new Button(() => percentual.value = valorPreset) { text = valorPreset + "%" };
            atalho.AddToClassList("gov-share-preset");
            atalho.SetEnabled(agricultura != null);
            predefinicoes.Add(atalho);
        }
        Button aplicar = new Button(() =>
        {
            int indice = Mathf.Max(0, nomes.IndexOf(cultura.value));
            bool ok = agricultura != null && agricultura.DefinirParticipacaoCultura(pais.teamId, indice, percentual.value);
            MostrarMensagem(ok ? "Participacao de " + nomes[indice] + " atualizada para " + percentual.value + "%." : "Nao foi possivel atualizar o mix de culturas.");
            MostrarPagina(abaAtual);
        }) { text = "APLICAR PARTICIPAÇÃO" };
        aplicar.AddToClassList("gov-share-apply");
        aplicar.SetEnabled(agricultura != null);
        percentual.SetEnabled(agricultura != null);
        controle.Add(cultura);
        controle.Add(linhaPercentual);
        controle.Add(predefinicoes);
        controle.Add(aplicar);
        conteudo.Add(controle);

        TabelaCabecalho("CULTURA", "MIX DE PRODUCAO", "ESTOQUE NACIONAL", "UNIDADE", "STATUS");
        float soma = 0f;
        if (pais.mixCulturaAgricola != null)
            for (int i = 0; i < pais.mixCulturaAgricola.Length; i++) soma += Mathf.Max(0f, pais.mixCulturaAgricola[i]);
        for (int i = 0; i < AgriculturaNacional.QuantidadeCulturas; i++)
        {
            string id = AgriculturaNacional.ObterIdCultura(i);
            float mix = pais.mixCulturaAgricola != null && pais.mixCulturaAgricola.Length > i && soma > 0f
                ? Mathf.Max(0f, pais.mixCulturaAgricola[i]) / soma * 100f : 100f / AgriculturaNacional.QuantidadeCulturas;
            int estoque = governo != null ? governo.ObterEstoque(pais.teamId, id) : 0;
            TabelaLinha(AgriculturaNacional.ObterNomeCultura(id), mix.ToString("0.0") + "%", estoque.ToString("N0"), "t", estoque > 0 ? "ESTOCADO" : "SEM ESTOQUE");
        }
    }

    private void AjustarImpostoIndividual(string setor, int delta)
    {
        bool ok = SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.AjustarImposto(TeamJogador, setor, delta);
        MostrarMensagem(ok ? $"Imposto de {setor} ajustado." : "O imposto ja atingiu o limite permitido.");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }
    private void ConstruirGastosMilitares(DadosPaisGoverno p)
    {
        SistemaGastosMilitares.GarantirInstancia();
        SistemaGastosMilitares gastos = SistemaGastosMilitares.Instancia;
        if (gastos == null)
        {
            AdicionarCard(conteudo, "GASTOS MILITARES INDISPONIVEIS", "O registro financeiro ainda esta inicializando.");
            return;
        }

        List<RegistroGastoMilitar> registros = gastos.ObterRegistrosDoTime(p.teamId).ToList();
        long total = registros.Sum(x => Math.Max(0L, x.valorTotal));
        int disparos = registros.Where(x => x.tipo == TipoGastoMilitar.Disparo).Sum(x => x.quantidade);
        int compras = registros.Where(x => x.tipo == TipoGastoMilitar.CompraMunicao || x.tipo == TipoGastoMilitar.CompraUnidade).Sum(x => x.quantidade);
        int fabricados = registros.Where(x => x.tipo == TipoGastoMilitar.FabricacaoMunicao).Sum(x => x.quantidade);

        LinhaCards(new[]
        {
            ("GASTO REGISTRADO", Moeda(total), total > 0 ? "state-warn" : "state-info"),
            ("DISPAROS", disparos.ToString("N0"), disparos > 0 ? "state-bad" : "state-good"),
            ("COMPRAS MILITARES", compras.ToString("N0"), "state-info"),
            ("FABRICACAO", fabricados.ToString("N0"), "state-good")
        });

        AdicionarCard(conteudo, "CONTROLE DE GASTOS MILITARES",
            "Cada disparo do Ares_Ar aparece como compra de um cartucho. Compras de unidades, pesquisas militares e lotes fabricados tambem entram neste historico.\nSaldo atual: " + Moeda(p.saldo));

        TabelaCabecalho("TIPO", "ITEM", "QTD", "VALOR UNIT.", "TOTAL");
        foreach (RegistroGastoMilitar registro in registros.Take(30))
        {
            if (registro == null) continue;
            string detalhe = (registro.data ?? string.Empty) + " | " + (registro.origem ?? string.Empty);
            TabelaLinha(registro.tipo.ToString(), registro.itemNome, registro.quantidade.ToString("N0") + " " + registro.unidade,
                Moeda(registro.valorUnitario), Moeda(registro.valorTotal),
                () => AbrirDetalheLinha("GASTO: " + registro.itemNome,
                    detalhe + "\nQuantidade: " + registro.quantidade.ToString("N0") + " " + registro.unidade
                        + "\nValor unitario: " + Moeda(registro.valorUnitario) + "\nValor total: " + Moeda(registro.valorTotal)));
        }
        if (registros.Count == 0)
            AdicionarCard(conteudo, "SEM GASTOS MILITARES REGISTRADOS", "O historico sera preenchido quando uma unidade for comprada, fabricada, pesquisada ou disparar.");

        AdicionarTitulo(conteudo, "ESTOQUE DE MUNICAO", "Quantidade armazenada por tipo e valor atual de referencia no mercado.");
        TabelaCabecalho("MUNICAO", "CATEGORIA", "ARMAZENADO", "VALOR", "DISPAROS");
        foreach (DefinicaoMunicaoMilitar municao in gastos.ObterMunicoesAtivas())
        {
            TabelaLinha(municao.nome, municao.categoria,
                gastos.ObterEstoqueMunicao(p.teamId, municao.id).ToString("N0") + " cart.",
                Moeda(municao.valorUnitario), municao.totalDisparado.ToString("N0"),
                () => AbrirDetalheLinha("MUNICAO: " + municao.nome,
                    municao.descricao + "\nEstoque: " + gastos.ObterEstoqueMunicao(p.teamId, municao.id).ToString("N0")
                        + " cartuchos\nDisparos registrados: " + municao.totalDisparado.ToString("N0")));
        }
    }

    private void ConstruirInterior(DadosPaisGoverno p)
    {
        GerenciadorDivisaoTerritorial.GarantirInstancia();
        List<CidadeEstado> cidades = GerenciadorDivisaoTerritorial.Instancia != null ? GerenciadorDivisaoTerritorial.Instancia.cidades.Where(c => c != null && c.teamID == TeamJogador).ToList() : new List<CidadeEstado>();

        if (abaAtual == "Populacao")
        {
            LinhaCards(new[] { ("POPULACAO", p.populacaoCivil.ToString("N0"), "state-info"), ("EMPREGO", p.emprego.ToString("0") + "%", "state-good"), ("MORADIA", p.moradia.ToString("0") + "%", p.moradia >= 70 ? "state-good" : "state-warn"), ("ALIMENTACAO", (p.deficitComida <= 0 ? "SUPRIDA" : "DEFICIT"), p.deficitComida <= 0 ? "state-good" : "state-bad"), ("QUALIDADE DE VIDA", p.qualidadeVida.ToString("0") + "%", "state-info"), ("SEGURANCA", p.estabilidade.ToString("0") + "%", "state-good") });
            AdicionarCard(conteudo, "SITUACAO INTERNA", $"Capacidade populacional: {p.populacaoMaxima:N0}\nFelicidade: {p.felicidade:0}%\nSatisfacao dos servicos: {p.indiceSatisfacaoServicos:0}%\nPressao habitacional: {p.pressaoHabitacional * 100f:0}%\nMigracao liquida: {p.taxaMigracao:+0.0;-0.0;0}");
            return;
        }

        if (abaAtual == "Cidades")
        {
            LinhaCards(new[] { ("CIDADES", cidades.Count.ToString(), "state-info"), ("COM AEROPORTO", cidades.Count(c => c.temAeroporto).ToString(), "state-good"), ("COM PORTO", cidades.Count(c => c.temPorto).ToString(), "state-info"), ("ATRATIVIDADE MEDIA", (cidades.Count > 0 ? cidades.Average(c => c.atratividade) : 0f).ToString("0") + "%", "state-warn") });
            TabelaCabecalho("CIDADE", "TIPO", "POP CIVIL", "AEROPORTO", "DOMINIO");
            foreach (CidadeEstado cidade in cidades)
            {
                string tipo = cidade.marcador != null && cidade.marcador.GetComponent<ComplexoGovernamental>() != null
                    ? "CENTRO ADMINISTRATIVO REGIONAL"
                    : cidade.ehEstado ? "ESTADO" : "CIDADE";
                string aero = cidade.temAeroporto ? "SIM" : "NAO";
                string dominio = cidade.teamID == TeamJogador ? "JOGADOR" : cidade.teamID > 0 ? "IA " + cidade.teamID : "NEUTRO";
                TabelaLinha(cidade.nome, tipo, cidade.populacaoCivil.ToString("N0"), aero, dominio, () =>
                {
                    cidadeSelecionadaId = cidade.id;
                    MostrarPagina("Cidades");
                });
            }
            CidadeEstado selecionada = cidades.FirstOrDefault(c => c != null && c.id == cidadeSelecionadaId);
            if (selecionada != null)
            {
                string tipo = selecionada.marcador != null && selecionada.marcador.GetComponent<ComplexoGovernamental>() != null
                    ? "Centro Administrativo Regional"
                    : selecionada.ehEstado ? "Estado" : "Cidade";
                string resumo = "Região: " + selecionada.nome
                    + "\nTipo: " + tipo
                    + "\nPopulação: " + selecionada.populacaoCivil.ToString("N0")
                    + "\nEmpregos: " + selecionada.empregosTotais.ToString("N0")
                    + " | Vagas abertas: " + selecionada.vagasDeEmpregoAbertas.ToString("N0")
                    + "\nCapacidade habitacional: " + selecionada.capacidadeHabitacional.ToString("N0")
                    + "\nPerfil produtivo local (indicadores): indústria " + selecionada.scoreIndustrial.ToString("0")
                    + ", comércio " + selecionada.scoreComercial.ToString("0")
                    + ", agricultura " + selecionada.scoreAgricola.ToString("0")
                    + ", energia " + selecionada.scoreEnergia.ToString("0")
                    + "\nInfraestrutura: aeroporto " + (selecionada.temAeroporto ? "sim" : "não")
                    + ", porto " + (selecionada.temPorto ? "sim" : "não")
                    + ", logística " + selecionada.scoreLogistica.ToString("0")
                    + "\nNecessidades: moradia " + Mathf.Max(0, selecionada.populacaoCivil - selecionada.capacidadeHabitacional).ToString("N0")
                    + " vagas; empregos " + Mathf.Max(0, selecionada.populacaoCivil - selecionada.empregosTotais).ToString("N0")
                    + " necessários"
                    + "\nConsumo e impostos seguem consolidados no nível nacional; não há valores regionais existentes.";
                AdicionarCard(conteudo, "RESUMO REGIONAL", resumo);

                TextField nomeRegiao = new TextField("Nome da região") { value = selecionada.nome };
                nomeRegiao.style.marginTop = 4f;
                nomeRegiao.style.marginBottom = 8f;
                conteudo.Add(nomeRegiao);
                Button salvarNome = new Button(() =>
                {
                    if (string.IsNullOrWhiteSpace(nomeRegiao.value))
                    {
                        MostrarMensagem("Informe um nome para a região.");
                        return;
                    }

                    GerenciadorDivisaoTerritorial.Instancia.RenomearCidade(selecionada.id, nomeRegiao.value);
                    MostrarMensagem("Região renomeada para " + nomeRegiao.value.Trim() + ".");
                    MostrarPagina("Cidades");
                })
                {
                    text = "SALVAR NOME DA REGIÃO"
                };
                salvarNome.AddToClassList("gov-action-button");
                conteudo.Add(salvarNome);
            }
            if (cidades.Count == 0) AdicionarCard(conteudo, "SEM CIDADES CADASTRADAS", "Crie ou registre marcadores territoriais para popular esta lista.");
            return;
        }

        if (abaAtual == "Bem-estar")
        {
            LinhaCards(new[] { ("QUALIDADE DE VIDA", p.qualidadeVida.ToString("0") + "%", "state-info"), ("FELICIDADE", p.felicidade.ToString("0") + "%", p.felicidade >= 70f ? "state-good" : "state-warn"), ("SERVICOS", p.indiceSatisfacaoServicos.ToString("0") + "%", "state-good"), ("PRESSAO HABITACIONAL", (p.pressaoHabitacional * 100f).ToString("0") + "%", p.pressaoHabitacional > 1f ? "state-bad" : "state-info") });
            string deficitPrincipal = p.deficitComida > 0 ? "Comida" : p.deficitEnergia > 0 ? "Energia" : p.deficitPetroleo > 0 ? "Petroleo" : "Nenhum";
            AdicionarCard(conteudo, "BEM-ESTAR", "Deficit principal: " + deficitPrincipal + "\nMoradia: " + p.moradia.ToString("0") + "%\nEmprego: " + p.emprego.ToString("0") + "%\nEstabilidade: " + p.estabilidade.ToString("0") + "%\nMigracao liquida: " + p.taxaMigracao.ToString("+0.0;-0.0;0"));
            return;
        }

        LinhaCards(new[] { ("POPULACAO", p.populacaoCivil.ToString("N0"), "state-info"), ("EMPREGO", p.emprego.ToString("0") + "%", "state-good"), ("MORADIA", p.moradia.ToString("0") + "%", p.moradia >= 70 ? "state-good" : "state-warn"), ("ALIMENTACAO", (p.deficitComida <= 0 ? "SUPRIDA" : "DEFICIT"), p.deficitComida <= 0 ? "state-good" : "state-bad"), ("QUALIDADE DE VIDA", p.qualidadeVida.ToString("0") + "%", "state-info"), ("SEGURANCA", p.estabilidade.ToString("0") + "%", "state-good") });
        AdicionarCard(conteudo, "REGIOES PRIORITARIAS", cidades.Count > 0
            ? string.Join("\n", cidades.OrderBy(c => c.atratividade).Take(4).Select(c => c.nome + " | atratividade " + c.atratividade.ToString("0") + "% | empregos " + c.empregosTotais + " | vagas " + c.vagasDeEmpregoAbertas))
            : "Nenhuma cidade cadastrada para priorizacao.");
    }

    private void ConstruirDefesa(DadosPaisGoverno p)
    {
        SistemaGovernoMundial g = SistemaGovernoMundial.Instancia;
        if (g == null) { AdicionarCard(conteudo, "DADOS INDISPONIVEIS", "Defesa ainda nao foi inicializada."); return; }

        SistemaGastosMilitares.GarantirInstancia();
        SistemaGastosMilitares gastosMilitares = SistemaGastosMilitares.Instancia;
        DefinicaoMunicaoMilitar aresMunicao = gastosMilitares != null ? gastosMilitares.ObterMunicao("municao_ares_ar") : null;

        GerenciadorDivisaoTerritorial.GarantirInstancia();
        int aeroportos = GerenciadorDivisaoTerritorial.Instancia != null ? GerenciadorDivisaoTerritorial.Instancia.ObterCidadesComAeroporto(TeamJogador).Count : 0;
        int portos = GerenciadorDivisaoTerritorial.Instancia != null ? GerenciadorDivisaoTerritorial.Instancia.cidades.Count(c => c != null && c.teamID == TeamJogador && c.temPorto) : 0;

        if (abaAtual == "Comando")
        {
            LinhaCards(new[] { ("PRONTIDAO", p.nivelMilitar + "%", "state-good"), ("ARMAMENTOS", p.armamentos.ToString("N0"), "state-info"), ("URANIO", p.uranio.ToString("N0"), "state-warn"), ("PRESSAO DE GUERRA", (g.PressaoGlobalGuerra() * 100f).ToString("0") + "%", "state-bad") });
            AdicionarCard(conteudo, "PLANO DEFENSIVO ATUAL", $"Postura: {p.planoEstrategico}\nMilitares ativos: {p.populacaoMilitarAtiva:N0}\nReservistas: {p.reservistas:N0}\nAlistaveis: {p.alistaveis:N0}\nSituacao: {(p.emGuerra ? "EM GUERRA" : "PAZ")}");
            if (aresMunicao != null)
                AdicionarCard(conteudo, "MUNICAO ANTIAEREA", $"{aresMunicao.nome}\nValor por cartucho: {Moeda(aresMunicao.valorUnitario)}\nCarregador: {aresMunicao.capacidadeCartucho} cartuchos\nPausa de reabastecimento: {aresMunicao.tempoReabastecimento:0.0}s\nDisparos registrados: {aresMunicao.totalDisparado:N0}");
            TabelaCabecalho("NACAO", "STATUS", "NIVEL MILITAR", "RELACAO", "RISCO");
            foreach (DadosPaisGoverno outro in ObterNacoesAtivas(g))
            {
                if (outro.teamId == TeamJogador) continue; RelacaoPaisGoverno r = g.ObterRelacao(TeamJogador, outro.teamId);
                TabelaLinha(outro.nomePais, outro.emGuerra ? "HOSTIL" : "OBSERVADO", outro.nivelMilitar.ToString(), r != null ? r.valor.ToString() : "0", outro.emGuerra ? "ALTO" : "MODERADO");
            }
            return;
        }

        if (abaAtual == "Exercito")
        {
            LinhaCards(new[] { ("FORCA TERRESTRE", p.populacaoMilitarAtiva.ToString("N0"), "state-good"), ("RESERVAS", p.reservistas.ToString("N0"), "state-info"), ("ALISTAVEIS", p.alistaveis.ToString("N0"), "state-warn"), ("ARMAMENTOS", p.armamentos.ToString("N0"), "state-info") });
            AdicionarCard(conteudo, "EXERCITO", $"Prontidao: {p.nivelMilitar:0}%\nPlano atual: {p.planoEstrategico}\nPressao de guerra: {g.PressaoGlobalGuerra() * 100f:0}%");
            return;
        }

        if (abaAtual == "Marinha")
        {
            LinhaCards(new[] { ("PORTOS", portos.ToString(), "state-info"), ("ARMAMENTOS", p.armamentos.ToString("N0"), "state-good"), ("PETROLEO", p.petroleo.ToString("N0"), "state-warn"), ("PRESSAO DE GUERRA", (g.PressaoGlobalGuerra() * 100f).ToString("0") + "%", "state-bad") });
            AdicionarCard(conteudo, "MARINHA", $"Portos ativos: {portos}\nAbastecimento de petroleo: {p.petroleo:N0}\nEstoque de armamentos: {p.armamentos:N0}");
            return;
        }

        if (abaAtual == "Aerea")
        {
            LinhaCards(new[] { ("AEROPORTOS", aeroportos.ToString(), "state-info"), ("ESTOQUE", p.armamentos.ToString("N0"), "state-good"), ("URANIO", p.uranio.ToString("N0"), "state-warn"), ("PRONTIDAO", p.nivelMilitar + "%", "state-good") });
            string aresTexto = aresMunicao != null
                ? $"\n\nAres_Ar: {aresMunicao.totalDisparado:N0} disparos | {Moeda(aresMunicao.valorUnitario)}/cartucho | carregador {aresMunicao.capacidadeCartucho} | reabastecimento {aresMunicao.tempoReabastecimento:0.0}s"
                : string.Empty;
            AdicionarCard(conteudo, "FORCA AEREA", $"Aeroportos operacionais: {aeroportos}\nPlano atual: {p.planoEstrategico}\nPressao de guerra: {g.PressaoGlobalGuerra() * 100f:0}%" + aresTexto);
            if (p.sateliteDefesa == null)
            {
                p.sateliteDefesa = new SateliteDefesaEstado();
            }
            SateliteDefesaEstado satelite = p.sateliteDefesa;
            string statusSatelite = satelite.desbloqueado ? "ATIVO" : "BLOQUEADO";
            string prontidaoSatelite = satelite.integridade >= 75f && satelite.desempenho >= 70f
                ? "OPERACIONAL"
                : satelite.integridade >= 45f && satelite.desempenho >= 45f ? "ATENCAO" : "CRITICO";
            AdicionarCard(conteudo, "SATELITE NACIONAL",
                $"Status: {statusSatelite}\nProntidao: {prontidaoSatelite}\nDesempenho: {satelite.desempenho:0}%\nIntegridade: {satelite.integridade:0}%\n" +
                $"Custo operacao: {Moeda(satelite.custoOperacionalDiario)}/dia\nManutencao automatica: {(satelite.manutencaoAutomatica ? "SIM" : "NAO")}");
            return;
        }

            LinhaCards(new[] { ("ALERTAS", ObterNacoesAtivas(g).Count(x => x != null && x.emGuerra).ToString(), "state-bad"), ("ARMAMENTOS", p.armamentos.ToString("N0"), "state-info"), ("URANIO", p.uranio.ToString("N0"), "state-warn"), ("PRESSAO DE GUERRA", (g.PressaoGlobalGuerra() * 100f).ToString("0") + "%", "state-bad") });
        AdicionarCard(conteudo, "ALERTAS MILITARES", UltimasNoticias(g, 6, "guerra", "alerta", "fronteira", "defesa"));
    }

    private void ConstruirCiencia(DadosPaisGoverno p)
    {
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        EstadoIndustrialPais estado = industrial != null ? industrial.ObterEstadoPais(p.teamId) : null;
        PerfilMineralPais perfil = industrial != null ? industrial.GarantirPerfil(p.teamId) : null;
        List<LinhaIndustrial> linhas = industrial != null ? industrial.ObterLinhasPais(p.teamId).Where(l => l != null).ToList() : new List<LinhaIndustrial>();
        List<OrdemExtracaoIndustrial> ordensExtracaoPais = industrial != null
            ? industrial.OrdensExtracao.Where(o => o != null && o.teamId == p.teamId).ToList()
            : new List<OrdemExtracaoIndustrial>();
        List<OrdemRefinoIndustrial> ordensRefinoPais = industrial != null
            ? industrial.OrdensRefino.Where(o => o != null && o.teamId == p.teamId).ToList()
            : new List<OrdemRefinoIndustrial>();

        int teamId = p.teamId;
        float eficiencia = estado != null ? estado.eficienciaIndustrial * 100f : Mathf.Clamp(p.nivelIndustrial, 0f, 100f);
        float energiaDisponivel = estado != null ? estado.energiaDisponivel * 100f : Mathf.Clamp(p.energia / 2f, 0f, 100f);
        int extracoesAtivas = ordensExtracaoPais.Count(o => o.estado == EstadoOrdemExtracaoIndustrial.Ativa || o.estado == EstadoOrdemExtracaoIndustrial.Aguardando || o.estado == EstadoOrdemExtracaoIndustrial.ConcluindoCiclo);
        int refinosAtivos = ordensRefinoPais.Count(o => o.estado != EstadoOrdemRefinoIndustrial.Cancelada && o.estado != EstadoOrdemRefinoIndustrial.Concluida);
        int reservasAtivas = industrial != null ? RecursosIndustriaisBase().Count(id => industrial.ObterQuantidadeReserva(teamId, id) > 0.1d) : 0;
        int laboratoriosAtivos = CalcularLaboratoriosAtivos(p, perfil);
        int laboratoriosTotais = 6;
        string perfilTexto = perfil != null ? perfil.DescreverPerfil() : "Perfil mineral ainda nao gerado.";

        if (abaAtual == "Pesquisa")
        {
            LinhaCards(new[]
            {
                ("LABS ATIVOS", laboratoriosAtivos + "/" + laboratoriosTotais, laboratoriosAtivos >= 4 ? "state-good" : "state-warn"),
                ("EXTRACOES", extracoesAtivas.ToString(), extracoesAtivas >= 3 ? "state-good" : "state-warn"),
                ("PROD. DIARIA", estado != null ? estado.producaoDiariaTotal.ToString("N0") + " t" : "N/D", "state-info"),
                ("ENERGIA", energiaDisponivel.ToString("0") + "%", energiaDisponivel >= 70f ? "state-good" : "state-warn"),
                ("ESTABILIDADE", p.estabilidade.ToString("0") + "%", p.estabilidade >= 65f ? "state-good" : "state-warn")
            });

            AdicionarCard(conteudo, "PERFIL GEOLOGICO NACIONAL",
                perfilTexto + "\nBase industrial: " + p.nivelIndustrial + "/100\n" +
                "Esta aba organiza a cadeia de pesquisa do pais em torno dos minerais, materiais refinados e futuras municoes.");

            AdicionarTitulo(conteudo, "LINHAS DE PESQUISA INDUSTRIAL", "Cards baseados nos recursos brutos e nos conteudos que ja existem no projeto.");
            VisualElement gradePesquisa = CriarGradeCiencia();

            OrdemExtracaoIndustrial ferro = ordensExtracaoPais.FirstOrDefault(o => string.Equals(o.recursoId, IndustriaIds.MinerioFerro, StringComparison.OrdinalIgnoreCase));
            OrdemExtracaoIndustrial cobre = ordensExtracaoPais.FirstOrDefault(o => string.Equals(o.recursoId, IndustriaIds.MinerioCobre, StringComparison.OrdinalIgnoreCase));
            OrdemExtracaoIndustrial bauxita = ordensExtracaoPais.FirstOrDefault(o => string.Equals(o.recursoId, IndustriaIds.Bauxita, StringComparison.OrdinalIgnoreCase));
            OrdemExtracaoIndustrial titanio = ordensExtracaoPais.FirstOrDefault(o => string.Equals(o.recursoId, IndustriaIds.MinerioTitanio, StringComparison.OrdinalIgnoreCase));
            OrdemExtracaoIndustrial uranio = ordensExtracaoPais.FirstOrDefault(o => string.Equals(o.recursoId, IndustriaIds.UranioBruto, StringComparison.OrdinalIgnoreCase));

            AdicionarCardCiencia(gradePesquisa, "1", "EXTRACAO DE MINERIO DE FERRO",
                "Abundancia: " + ObterAbundanciaTexto(perfil, RecursoMineral.MinerioFerro),
                "Estoque atual: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.MinerioFerro) +
                "\nUso futuro: aco estrutural, blindagens, projeteis e construcoes pesadas.",
                DescreverEstadoExtracao(ferro), ClasseEstadoExtracao(ferro));

            AdicionarCardCiencia(gradePesquisa, "2", "EXTRACAO DE MINERIO DE COBRE",
                "Abundancia: " + ObterAbundanciaTexto(perfil, RecursoMineral.MinerioCobre),
                "Estoque atual: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.MinerioCobre) +
                "\nUso futuro: cobre eletrolitico, cabos, radares, municoes e eletronicos.",
                DescreverEstadoExtracao(cobre), ClasseEstadoExtracao(cobre));

            AdicionarCardCiencia(gradePesquisa, "3", "EXTRACAO DE BAUXITA",
                "Abundancia: " + ObterAbundanciaTexto(perfil, RecursoMineral.Bauxita),
                "Estoque atual: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.Bauxita) +
                "\nUso futuro: duraluminio, fuselagens, drones, bombas leves e misseis leves.",
                DescreverEstadoExtracao(bauxita), ClasseEstadoExtracao(bauxita));

            AdicionarCardCiencia(gradePesquisa, "4", "EXTRACAO DE TITANIO",
                "Abundancia: " + ObterAbundanciaTexto(perfil, RecursoMineral.MinerioTitanio),
                "Estoque atual: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.MinerioTitanio) +
                "\nUso futuro: liga de titanio, blindagem pesada, submarinos e misseis estrategicos.",
                DescreverEstadoExtracao(titanio), ClasseEstadoExtracao(titanio));

            AdicionarCardCiencia(gradePesquisa, "5", "EXTRACAO DE URANIO BRUTO",
                "Abundancia: " + ObterAbundanciaTexto(perfil, RecursoMineral.UranioBruto),
                "Estoque atual: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.UranioBruto) +
                "\nUso futuro: pesquisa nuclear, laboratorio nuclear e carga de uranio enriquecido.",
                DescreverEstadoExtracao(uranio), ClasseEstadoExtracao(uranio));

            AdicionarCardCiencia(gradePesquisa, "6", "PADRONIZACAO DE MUNICOES",
                "Bala | Bala_30 | Bala_Nav | Tank_Bala",
                "Fase seguinte do projeto. A cadeia ja esta sendo preparada para reabastecimento nacional das unidades.",
                p.nivelIndustrial >= 45 ? "PLANEJAVEL" : "BLOQUEADA", p.nivelIndustrial >= 45 ? "state-warn" : "state-bad");

            AdicionarCardCiencia(gradePesquisa, "7", "MISSILARIA GUIADA",
                "homing_missile | Missil_05 | Intercept_Missile | SS_Missile",
                "Depende de componentes eletronicos, ligas leves e reserva energetica constante.",
                p.nivelIndustrial >= 60 ? "EM PREPARACAO" : "BLOQUEADA", p.nivelIndustrial >= 60 ? "state-info" : "state-bad");

            AdicionarCardCiencia(gradePesquisa, "8", "PESQUISA NUCLEAR",
                "uranio_bruto -> uranio_enriquecido -> ICNU",
                "A linha nuclear existe como conteudo estrategico futuro e so deve liberar quando houver energia, estabilidade e laboratorio adequados.",
                p.tecnologiaExtracaoConcluida && p.nivelIndustrial >= 70 && p.estabilidade >= 70f ? "PRONTA PARA DESBLOQUEIO" : "BLOQUEADA",
                p.tecnologiaExtracaoConcluida && p.nivelIndustrial >= 70 && p.estabilidade >= 70f ? "state-warn" : "state-bad");
            ConstruirCatalogoPesquisasNacionais(p);
            ConstruirCatalogoMunicoesCiencia(false, p);
            return;
        }

        if (abaAtual == "Tecnologias")
        {
            LinhaCards(new[]
            {
                ("INDUSTRIA", p.nivelIndustrial + "/100", "state-info"),
                ("LINHAS", linhas.Count.ToString(), linhas.Count >= 3 ? "state-good" : "state-warn"),
                ("RESERVAS", reservasAtivas.ToString() + " tipos", reservasAtivas > 0 ? "state-good" : "state-warn"),
                ("DEFESA TEC.", p.nivelMilitar + "/100", p.nivelMilitar >= 50 ? "state-good" : "state-warn")
            });

            AdicionarCard(conteudo, "TECNOLOGIAS INDUSTRIAIS",
                "Estas tecnologias traduzem o que o backend industrial ja sabe fazer hoje e o que o projeto ja esta preparando para as proximas fases.");

            VisualElement gradeTecnologias = CriarGradeCiencia();
            AdicionarCardCiencia(gradeTecnologias, "1", "EXTRACAO CONTINUA", "Reinicio automatico de ciclos diarios.",
                "Mantem a extracao rodando a cada mudanca de data sem depender de minas fisicas no mapa.", "ATIVA", "state-good");
            AdicionarCardCiencia(gradeTecnologias, "2", "ESTOQUE-ALVO", "Extrair ate atingir reserva planejada.",
                "Permite parar cadeias automaticamente quando o Armazem Nacional alcanca a quantidade definida.", "ATIVA", "state-good");
            AdicionarCardCiencia(gradeTecnologias, "3", "RESERVA IMEDIATA", "Bloqueio de materiais no inicio do refino.",
                "Minerais e materiais reservados nao podem ser vendidos ou consumidos por outras filas.", reservasAtivas > 0 ? "EM USO" : "DISPONIVEL", reservasAtivas > 0 ? "state-info" : "state-good");
            AdicionarCardCiencia(gradeTecnologias, "4", "LINHAS INDUSTRIAIS", "Capacidade limitada por nivel da fabrica.",
                "Hoje o pais opera " + linhas.Count + " linhas industriais para refino paralelo.", linhas.Count >= 3 ? "OPERACIONAL" : "LIMITADA", linhas.Count >= 3 ? "state-good" : "state-warn");
            AdicionarCardCiencia(gradeTecnologias, "5", "ELETRONICA INDUSTRIAL", "Cobre eletrolitico + duraluminio -> componentes.",
                "Desbloqueia radares, guiagem, drones e a base de misseis inteligentes.", p.nivelIndustrial >= 55 ? "PRONTA" : "BLOQUEADA", p.nivelIndustrial >= 55 ? "state-info" : "state-bad");
            AdicionarCardCiencia(gradeTecnologias, "6", "LIGAS ESTRATEGICAS", "Titaneo bruto + aco estrutural.",
                "Base para blindagem pesada, cascos especiais e misseis de longo alcance.", p.nivelIndustrial >= 50 ? "PRONTA" : "LIMITADA", p.nivelIndustrial >= 50 ? "state-info" : "state-warn");
            AdicionarCardCiencia(gradeTecnologias, "7", "PADRONIZACAO BALISTICA", "Bala | Bala_30 | Bala_Nav | Tank_Bala",
                "Prepara a futura fabricacao de municoes por calibre a partir do Armazem Nacional.", p.nivelIndustrial >= 45 ? "EM PREPARACAO" : "BLOQUEADA", p.nivelIndustrial >= 45 ? "state-warn" : "state-bad");
            AdicionarCardCiencia(gradeTecnologias, "8", "PROGRAMA NUCLEAR", "uranio_enriquecido | ICNU",
                "Tecnologia estrategica de maior custo energetico do jogo. Nunca deve ser instantanea.", p.nivelIndustrial >= 70 && p.estabilidade >= 70f ? "PESQUISAVEL" : "BLOQUEADA", p.nivelIndustrial >= 70 && p.estabilidade >= 70f ? "state-warn" : "state-bad");
            ConstruirCatalogoTecnologiasNacionais(p);
            return;
        }

        if (abaAtual == "Projetos")
        {
            LinhaCards(new[]
            {
                ("LINHAS", linhas.Count(l => l.EstaOcupada) + "/" + Mathf.Max(1, linhas.Count), linhas.Any(l => l.EstaLivre) ? "state-good" : "state-warn"),
                ("REFINOS", refinosAtivos.ToString(), refinosAtivos > 0 ? "state-info" : "state-warn"),
                ("RESERVADO", reservasAtivas.ToString() + " tipos", reservasAtivas > 0 ? "state-good" : "state-warn"),
                ("SALDO", Moeda(p.saldo), p.saldo >= 2500 ? "state-good" : "state-warn")
            });

            if (industrial == null)
            {
                AdicionarCard(conteudo, "SISTEMA INDUSTRIAL INDISPONIVEL", "O painel de ciencia encontrou o menu, mas o backend industrial ainda nao foi carregado.");
                return;
            }

            AdicionarTitulo(conteudo, "PROJETOS INDUSTRIAIS DISPONIVEIS", "Receitas reais do sistema industrial, prontas para virar fila de refino.");
            VisualElement gradeProjetos = CriarGradeCiencia();
            foreach (ReceitaIndustrialSO receita in industrial.ReceitasCatalogo.OrderBy(r => r.nivelIndustrialExigido).ThenBy(r => r.diasNecessarios))
            {
                bool possuiMateriais = receita.materiaisNecessarios.All(m => industrial.ObterQuantidadePais(teamId, m.recursoId) >= m.quantidade);
                bool possuiSaldo = p.saldo >= receita.dinheiroNecessario;
                bool nivelOk = p.nivelIndustrial >= receita.nivelIndustrialExigido;
                bool temFila = ordensRefinoPais.Any(o => string.Equals(o.receitaId, receita.id, StringComparison.OrdinalIgnoreCase)
                    && o.estado != EstadoOrdemRefinoIndustrial.Cancelada
                    && o.estado != EstadoOrdemRefinoIndustrial.Concluida);

                string estadoProjeto = temFila ? "EM FILA" : (possuiMateriais && possuiSaldo && nivelOk ? "PRONTO" : "REQUISITOS");
                string classeProjeto = temFila ? "state-info" : (possuiMateriais && possuiSaldo && nivelOk ? "state-good" : "state-warn");
                string materiais = string.Join("\n", receita.materiaisNecessarios.Select(m =>
                    NomeIndustrial(m.recursoId) + ": " +
                    (industrial.ObterQuantidadePais(teamId, m.recursoId)).ToString("N0") + "/" +
                    m.quantidade.ToString("N0")));

                string detalhes = materiais +
                    "\nCusto: " + Moeda(receita.dinheiroNecessario) +
                    " | Energia: " + receita.energiaNecessaria.ToString("N0") +
                    "\nDuracao: " + receita.diasNecessarios + " dias (" + (receita.diasNecessarios * 2) + " min)" +
                    "\nSaida: " + receita.quantidadeProduzida.ToString("N0") + " " + receita.unidadeResultado;

                Action acaoProjeto = temFila
                    ? (Action)(() => MostrarFilaIndustrial(receita.id))
                    : () => CriarProjetoIndustrial(receita.id);

                AdicionarCardCiencia(gradeProjetos,
                    receita.nivelIndustrialExigido.ToString(),
                    receita.nome.ToUpperInvariant(),
                    "Produto final: " + NomeIndustrial(receita.produtoFinalId),
                    detalhes,
                    estadoProjeto,
                    classeProjeto,
                    temFila ? "VER FILA" : "INICIAR LOTE",
                    acaoProjeto,
                    temFila ? null : "buy");
            }

            AdicionarTitulo(conteudo, "FABRICACAO MILITAR FUTURA", "Conteudos previstos para a segunda fase, usando os materiais que o Armazem Nacional ja suporta.");
            VisualElement gradeArsenal = CriarGradeCiencia();
            AdicionarCardCiencia(gradeArsenal, "A", "MUNICAO DE FUZIL", "Bala", "Base esperada: aco estrutural + cobre eletrolitico.\nReabastecimento automatico de unidades terrestres.", "FUTURO", "state-info");
            AdicionarCardCiencia(gradeArsenal, "B", "MUNICOES PESADAS", "Bala_30 | Bala_Nav | Tank_Bala", "Base esperada: aco estrutural reforcado, cobre e ligas estrategicas.", "FUTURO", "state-info");
            AdicionarCardCiencia(gradeArsenal, "C", "BOMBAS AEREAS", "Bomb_01_Prefeb | Bomb_02_Prefeb | Bomb_03_Prefeb", "Base esperada: duraluminio, aco estrutural e componentes de detoncao.", "FUTURO", "state-info");
            AdicionarCardCiencia(gradeArsenal, "D", "MISSEIS GUIADOS", "homing_missile | Missil_05 | Intercept_Missile", "Base esperada: componentes eletronicos, duraluminio e cobre refinado.", "FUTURO", "state-info");
            AdicionarCardCiencia(gradeArsenal, "E", "MISSEIS NAVAIS", "missel_sub | Missel_navTomy", "Base esperada: liga de titanio, eletronicos e casco estrategico.", "FUTURO", "state-info");
            AdicionarCardCiencia(gradeArsenal, "F", "DISSUASAO NUCLEAR", "ICNU", "Base esperada: uranio enriquecido, componentes eletronicos e cadeia nuclear completa.", "FUTURO ESTRATEGICO", "state-bad");
            ConstruirCatalogoMunicoesCiencia(true, p);
            return;
        }

        LinhaCards(new[]
        {
            ("LABS", laboratoriosAtivos + "/" + laboratoriosTotais, laboratoriosAtivos >= 4 ? "state-good" : "state-warn"),
            ("EFICIENCIA", eficiencia.ToString("0") + "%", eficiencia >= 70f ? "state-good" : "state-warn"),
            ("LINHAS", linhas.Count.ToString(), linhas.Count >= 3 ? "state-good" : "state-warn"),
            ("ESTABILIDADE", p.estabilidade.ToString("0") + "%", p.estabilidade >= 70f ? "state-good" : "state-warn")
        });

        AdicionarCard(conteudo, "LABORATORIOS NACIONAIS",
            "Os laboratorios a seguir representam a malha cientifica e industrial que alimenta pesquisa, projetos, refino e a futura fabricacao de municoes.");

        VisualElement gradeLaboratorios = CriarGradeCiencia();
        AdicionarCardCiencia(gradeLaboratorios, "1", "LAB. DE MATERIAIS FERROSOS", "Minerio de ferro -> aco estrutural",
            "Estoque: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.AcoEstrutural) + "\nEspecializacao em blindagens, trilhos, chassis e projeteis.", "ATIVO", "state-good");
        AdicionarCardCiencia(gradeLaboratorios, "2", "LAB. DE COBRE INDUSTRIAL", "Minerio de cobre -> cobre eletrolitico",
            "Estoque: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.CobreEletrolitico) + "\nEspecializacao em cabos, espoletas, radares e circuitos.", "ATIVO", "state-good");
        AdicionarCardCiencia(gradeLaboratorios, "3", "LAB. DE MATERIAIS LEVES", "Bauxita -> duraluminio",
            "Estoque: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.Duraluminio) + "\nEspecializacao em aviacao, drones, bombas e fuselagens.", p.nivelIndustrial >= 35 ? "ATIVO" : "LIMITADO", p.nivelIndustrial >= 35 ? "state-good" : "state-warn");
        AdicionarCardCiencia(gradeLaboratorios, "4", "LAB. DE LIGAS ESTRATEGICAS", "Titanio + aco estrutural",
            "Estoque: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.LigaTitanio) + "\nEspecializacao em blindagem pesada, submarinos e misseis.", p.nivelIndustrial >= 50 ? "ATIVO" : "LIMITADO", p.nivelIndustrial >= 50 ? "state-good" : "state-warn");
        AdicionarCardCiencia(gradeLaboratorios, "5", "LAB. DE ELETRONICA INDUSTRIAL", "Cobre eletrolitico + duraluminio",
            "Estoque: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.ComponentesEletronicos) + "\nEspecializacao em guiagem, radares, comunicacao e defesa AA.", p.nivelIndustrial >= 55 ? "DISPONIVEL" : "BLOQUEADO", p.nivelIndustrial >= 55 ? "state-info" : "state-bad");
        AdicionarCardCiencia(gradeLaboratorios, "6", "LAB. NUCLEAR", "Uranio bruto -> uranio enriquecido",
            "Estoque: " + FormatarQuantidadeIndustrial(industrial, teamId, IndustriaIds.UranioEnriquecido, "cargas") + "\nExige alta energia, estabilidade e linha estrategica dedicada.", p.tecnologiaExtracaoConcluida && p.nivelIndustrial >= 70 && p.estabilidade >= 70f ? "PRONTO PARA DESBLOQUEIO" : "BLOQUEADO", p.tecnologiaExtracaoConcluida && p.nivelIndustrial >= 70 && p.estabilidade >= 70f ? "state-warn" : "state-bad");

        ConstruirCatalogoLaboratoriosNacionais(p);

        string linhasTexto = linhas.Count == 0
            ? "Nenhuma linha industrial registrada."
            : string.Join("\n", linhas.Select(l =>
                "Linha " + (l.indice + 1) + ": " + DescreverEstadoLinha(l) +
                (string.IsNullOrWhiteSpace(l.receitaId) ? string.Empty : " | " + NomeIndustrial(l.receitaId))));
        AdicionarCard(conteudo, "LINHAS INDUSTRIAIS ATUAIS", linhasTexto);
    }

    private void ConstruirCatalogoMunicoesCiencia(bool permitirFabricacao, DadosPaisGoverno pais)
    {
        SistemaGastosMilitares.GarantirInstancia();
        SistemaGastosMilitares gastos = SistemaGastosMilitares.Instancia;
        if (gastos == null) return;

        AdicionarTitulo(conteudo, permitirFabricacao ? "FABRICACAO DE MUNICOES ATIVAS" : "MUNICOES E SISTEMAS EM USO",
            "Somente os armamentos registrados como ativos aparecem aqui, com pesquisa, valor de mercado e quantidade.");
        VisualElement grade = CriarGradeCiencia();
        foreach (DefinicaoMunicaoMilitar municao in gastos.ObterMunicoesAtivas())
        {
            PesquisaNacionalEstado pesquisa = pais != null && pais.pesquisas != null
                ? pais.pesquisas.FirstOrDefault(x => x != null && string.Equals(x.id, municao.pesquisaId, StringComparison.OrdinalIgnoreCase))
                : null;
            bool desbloqueada = pesquisa == null || pesquisa.concluida;
            string status = pesquisa == null ? "CATALOGADA" : pesquisa.concluida ? "DESBLOQUEADA" : pesquisa.emAndamento ? "EM PESQUISA" : "BLOQUEADA";
            string classe = desbloqueada ? "state-good" : pesquisa != null && pesquisa.emAndamento ? "state-info" : "state-bad";
            string corpo = municao.descricao
                + "\nValor de mercado: " + Moeda(municao.valorUnitario)
                + " por cartucho\nCarregador: " + municao.capacidadeCartucho
                + " | Reabastecimento: " + municao.tempoReabastecimento.ToString("0.0") + "s"
                + "\nFabricados: " + municao.totalFabricado.ToString("N0")
                + " | Disparados: " + municao.totalDisparado.ToString("N0");
            AdicionarCardCiencia(grade, "AA", municao.nome.ToUpperInvariant(), municao.categoria, corpo, status, classe,
                permitirFabricacao && desbloqueada ? "FABRICAR 10" : null,
                permitirFabricacao && desbloqueada ? (Action)(() => ProduzirMunicaoMilitar(municao.id, 10)) : null,
                "buy");
        }
    }

    private void ProduzirMunicaoMilitar(string municaoId, int quantidade)
    {
        SistemaGastosMilitares.GarantirInstancia();
        string mensagem = "Sistema de gastos militares indisponivel.";
        bool ok = false;
        if (SistemaGastosMilitares.Instancia != null)
        {
            ok = SistemaGastosMilitares.Instancia.ProduzirMunicao(TeamJogador, municaoId, quantidade, out mensagem);
        }
        MostrarMensagem(ok ? mensagem : "Fabricacao recusada: " + mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void ConstruirCatalogoPesquisasNacionais(DadosPaisGoverno pais)
    {
        AdicionarTitulo(conteudo, "PESQUISAS NACIONAIS", "Pesquisas com custo, energia, dependencias e prazo controlados pelo governo.");
        List<PesquisaNacionalEstado> pesquisas = pais != null && pais.pesquisas != null
            ? pais.pesquisas.Where(p => p != null).ToList()
            : new List<PesquisaNacionalEstado>();
        if (pesquisas.Count == 0)
        {
            AdicionarCard(conteudo, "CATALOGO INDISPONIVEL", "O catalogo nacional de pesquisas ainda nao foi carregado.");
            return;
        }

        VisualElement grade = CriarGradeCiencia();
        foreach (PesquisaNacionalEstado pesquisa in pesquisas)
        {
            bool dependenciasOk = DependenciasCientificasAtendidas(pais, pesquisa.dependencias);
            string status = pesquisa.concluida ? "CONCLUIDA"
                : pesquisa.emAndamento ? "EM ANDAMENTO"
                : dependenciasOk ? "DISPONIVEL PARA INICIAR" : "BLOQUEADA: DEPENDENCIAS PENDENTES";
            string corpo = pesquisa.descricao
                + "\nCategoria: " + pesquisa.categoria
                + "\nRequisitos: " + pesquisa.requisitosVisuais
                + "\nDesbloqueia: " + pesquisa.desbloqueia
                + "\nCusto: " + Moeda(pesquisa.custoSaldo) + " | Energia: " + pesquisa.custoEnergia.ToString("N0")
                + "\nDuracao: " + pesquisa.duracaoDias.ToString("N0") + " dia(s)"
                + (pesquisa.emAndamento ? "\nPrazo restante: " + DiasRestantes(pesquisa.diaInicio, pesquisa.duracaoDias).ToString("N0") + " dia(s)" : string.Empty);
            bool podeIniciar = !pesquisa.concluida && !pesquisa.emAndamento && dependenciasOk;
            AdicionarCardCiencia(grade, "P", pesquisa.nome.ToUpperInvariant(), pesquisa.categoria, corpo, status,
                pesquisa.concluida ? "state-good" : pesquisa.emAndamento ? "state-info" : dependenciasOk ? "state-warn" : "state-bad",
                podeIniciar ? "INICIAR PESQUISA" : null,
                podeIniciar ? (Action)(() => IniciarPesquisaNacional(pesquisa.id)) : null,
                "buy");
        }
    }

    private void ConstruirCatalogoTecnologiasNacionais(DadosPaisGoverno pais)
    {
        AdicionarTitulo(conteudo, "TECNOLOGIAS NACIONAIS", "Niveis tecnologicos usam o custo progressivo e os requisitos do sistema de governo.");
        List<TecnologiaNacionalEstado> tecnologias = pais != null && pais.tecnologias != null
            ? pais.tecnologias.Where(t => t != null).ToList()
            : new List<TecnologiaNacionalEstado>();
        if (tecnologias.Count == 0)
        {
            AdicionarCard(conteudo, "CATALOGO INDISPONIVEL", "O catalogo nacional de tecnologias ainda nao foi carregado.");
            return;
        }

        VisualElement grade = CriarGradeCiencia();
        foreach (TecnologiaNacionalEstado tecnologia in tecnologias)
        {
            int proximoNivel = Mathf.Min(tecnologia.nivelMaximo, tecnologia.nivelAtual + 1);
            int multiplicador = Mathf.Max(1, tecnologia.nivelAtual + 1);
            bool dependenciasOk = DependenciasCientificasAtendidas(pais, tecnologia.dependencias);
            bool noMaximo = tecnologia.nivelAtual >= tecnologia.nivelMaximo;
            string status = noMaximo ? "NIVEL MAXIMO"
                : tecnologia.emAndamento ? "EM DESENVOLVIMENTO"
                : dependenciasOk ? "DISPONIVEL PARA INVESTIMENTO" : "BLOQUEADA: DEPENDENCIAS PENDENTES";
            string corpo = tecnologia.descricao
                + "\nCategoria: " + tecnologia.categoria
                + "\nEfeito: " + tecnologia.efeito
                + "\nProximo nivel: " + proximoNivel + "/" + tecnologia.nivelMaximo
                + "\nCusto: " + Moeda(tecnologia.custoSaldo * multiplicador)
                + " | Energia: " + (tecnologia.custoEnergia * multiplicador).ToString("N0")
                + "\nDuracao: " + tecnologia.duracaoDias.ToString("N0") + " dia(s)"
                + (tecnologia.emAndamento ? "\nPrazo restante: " + DiasRestantes(tecnologia.diaInicio, tecnologia.duracaoDias).ToString("N0") + " dia(s)" : string.Empty);
            bool podeIniciar = !noMaximo && !tecnologia.emAndamento && dependenciasOk;
            AdicionarCardCiencia(grade, proximoNivel.ToString(), tecnologia.nome.ToUpperInvariant(), tecnologia.categoria, corpo, status,
                noMaximo ? "state-good" : tecnologia.emAndamento ? "state-info" : dependenciasOk ? "state-warn" : "state-bad",
                podeIniciar ? "INVESTIR NIVEL " + proximoNivel : null,
                podeIniciar ? (Action)(() => IniciarTecnologiaNacional(tecnologia.id)) : null,
                "buy");
        }
    }

    private void ConstruirCatalogoLaboratoriosNacionais(DadosPaisGoverno pais)
    {
        AdicionarTitulo(conteudo, "LABORATORIOS NACIONAIS EXPANSIVEIS", "A expansao cobra saldo e energia e respeita dependencias, teto e prazo do backend.");
        List<LaboratorioNacionalEstado> laboratorios = pais != null && pais.laboratorios != null
            ? pais.laboratorios.Where(l => l != null).ToList()
            : new List<LaboratorioNacionalEstado>();
        if (laboratorios.Count == 0)
        {
            AdicionarCard(conteudo, "CATALOGO INDISPONIVEL", "O catalogo nacional de laboratorios ainda nao foi carregado.");
            return;
        }

        VisualElement grade = CriarGradeCiencia();
        foreach (LaboratorioNacionalEstado laboratorio in laboratorios)
        {
            int proximoNivel = Mathf.Min(laboratorio.nivelMaximo, laboratorio.nivelAtual + 1);
            int multiplicador = Mathf.Max(1, laboratorio.nivelAtual + 1);
            bool dependenciasOk = DependenciasCientificasAtendidas(pais, laboratorio.dependencias);
            bool noMaximo = laboratorio.nivelAtual >= laboratorio.nivelMaximo;
            string status = noMaximo ? "NIVEL MAXIMO"
                : laboratorio.emExpansao ? "EXPANSAO EM ANDAMENTO"
                : dependenciasOk ? "PRONTO PARA EXPANDIR" : "BLOQUEADO: DEPENDENCIAS PENDENTES";
            string corpo = laboratorio.especializacao
                + "\n" + laboratorio.descricao
                + "\nNivel: " + laboratorio.nivelAtual + "/" + laboratorio.nivelMaximo
                + "\nCusto: " + Moeda(laboratorio.custoSaldo * multiplicador)
                + " | Energia: " + (laboratorio.custoEnergia * multiplicador).ToString("N0")
                + "\nDuracao: " + laboratorio.duracaoDias.ToString("N0") + " dia(s)"
                + (laboratorio.emExpansao ? "\nPrazo restante: " + DiasRestantes(laboratorio.diaInicio, laboratorio.duracaoDias).ToString("N0") + " dia(s)" : string.Empty);
            bool podeExpandir = !noMaximo && !laboratorio.emExpansao && dependenciasOk;
            AdicionarCardCiencia(grade, proximoNivel.ToString(), laboratorio.nome.ToUpperInvariant(), laboratorio.especializacao,
                corpo, status, noMaximo ? "state-good" : laboratorio.emExpansao ? "state-info" : dependenciasOk ? "state-warn" : "state-bad",
                podeExpandir ? "EXPANDIR LABORATORIO" : null,
                podeExpandir ? (Action)(() => ExpandirLaboratorioNacional(laboratorio.id)) : null,
                "buy");
        }
    }

    private static bool DependenciasCientificasAtendidas(DadosPaisGoverno pais, string dependencias)
    {
        if (pais == null || string.IsNullOrWhiteSpace(dependencias)) return true;

        foreach (string parte in dependencias.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string dependencia = parte.Trim();
            if (dependencia.StartsWith("lab_", StringComparison.OrdinalIgnoreCase))
            {
                LaboratorioNacionalEstado laboratorio = pais.laboratorios != null
                    ? pais.laboratorios.FirstOrDefault(l => l != null && l.id == dependencia)
                    : null;
                if (laboratorio == null || laboratorio.nivelAtual <= 0) return false;
            }
            else
            {
                PesquisaNacionalEstado pesquisa = pais.pesquisas != null
                    ? pais.pesquisas.FirstOrDefault(p => p != null && p.id == dependencia)
                    : null;
                if (pesquisa == null || !pesquisa.concluida) return false;
            }
        }

        return true;
    }

    private static int DiasRestantes(int diaInicio, int duracaoDias)
    {
        int diaAtual = GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
        return Mathf.Max(0, diaInicio + Mathf.Max(1, duracaoDias) - diaAtual);
    }

    private void IniciarPesquisaNacional(string pesquisaId)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        string mensagem = "Sistema nacional de pesquisas indisponivel.";
        bool ok = governo != null && governo.IniciarPesquisaNacional(TeamJogador, pesquisaId, out mensagem);
        MostrarMensagem(ok ? mensagem : "Pesquisa recusada: " + mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void IniciarTecnologiaNacional(string tecnologiaId)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        string mensagem = "Sistema nacional de tecnologias indisponivel.";
        bool ok = governo != null && governo.IniciarTecnologiaNacional(TeamJogador, tecnologiaId, out mensagem);
        MostrarMensagem(ok ? mensagem : "Tecnologia recusada: " + mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void ExpandirLaboratorioNacional(string laboratorioId)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        string mensagem = "Sistema nacional de laboratorios indisponivel.";
        bool ok = governo != null && governo.ExpandirLaboratorio(TeamJogador, laboratorioId, out mensagem);
        MostrarMensagem(ok ? mensagem : "Expansao recusada: " + mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void MostrarFilaIndustrial(string receitaId)
    {
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            MostrarMensagem("Fila industrial indisponivel: sistema nao carregado.");
            return;
        }

        List<OrdemRefinoIndustrial> fila = industrial.OrdensRefino
            .Where(o => o != null && o.teamId == TeamJogador
                && (string.IsNullOrWhiteSpace(receitaId)
                    || string.Equals(o.receitaId, receitaId, StringComparison.OrdinalIgnoreCase))
                && o.estado != EstadoOrdemRefinoIndustrial.Cancelada
                && o.estado != EstadoOrdemRefinoIndustrial.Concluida)
            .OrderBy(o => o.diasRestantes)
            .ThenBy(o => o.receitaId)
            .ToList();

        string escopoFila = string.IsNullOrWhiteSpace(receitaId)
            ? "todas as ordens desta nacao"
            : "ordens da receita " + NomeIndustrial(receitaId);
        if (detalheAtual != null)
            detalheAtual.RemoveFromHierarchy();

        VisualElement painelFila = new VisualElement();
        painelFila.AddToClassList("gov-card");
        Label tituloFila = new Label("FILA DE REFINO ATUAL");
        tituloFila.AddToClassList("gov-card-title");
        Label descricaoFila = new Label("Consulta somente leitura de " + escopoFila
            + ". Saldo, materiais e ordens nao foram alterados.");
        descricaoFila.AddToClassList("gov-subheading");
        descricaoFila.style.whiteSpace = WhiteSpace.Normal;
        painelFila.Add(tituloFila);
        painelFila.Add(descricaoFila);
        if (fila.Count == 0)
        {
            Label vazia = new Label("FILA SEM ORDENS ATIVAS\nNao ha ordens ativas para esta nacao.");
            vazia.style.whiteSpace = WhiteSpace.Normal;
            painelFila.Add(vazia);
            conteudo.Add(painelFila);
            detalheAtual = painelFila;
            return;
        }

        string[] cabecalhos = { "PRODUTO", "QUANTIDADE PREVISTA", "ESTADO", "PRAZO", "DETALHE" };
        VisualElement cabecalho = new VisualElement();
        cabecalho.AddToClassList("gov-table-row");
        cabecalho.AddToClassList("header");
        foreach (string texto in cabecalhos)
        {
            Label celula = new Label(texto);
            celula.AddToClassList("gov-table-cell");
            cabecalho.Add(celula);
        }
        painelFila.Add(cabecalho);

        foreach (OrdemRefinoIndustrial ordem in fila)
        {
            ReceitaIndustrialSO receita = industrial.ReceitasCatalogo.FirstOrDefault(r => r != null
                && string.Equals(r.id, ordem.receitaId, StringComparison.OrdinalIgnoreCase));
            string unidade = receita != null && !string.IsNullOrWhiteSpace(receita.unidadeResultado)
                ? " " + receita.unidadeResultado
                : string.Empty;
            string prazo = Mathf.Max(0, ordem.diasRestantes).ToString("N0") + " dia(s) restante(s)";
            string detalhe = string.IsNullOrWhiteSpace(ordem.motivoBloqueio)
                ? "Linha " + (string.IsNullOrWhiteSpace(ordem.linhaId) ? "pendente" : ordem.linhaId)
                : ordem.motivoBloqueio;
            VisualElement linha = new VisualElement();
            linha.AddToClassList("gov-table-row");
            foreach (string texto in new[]
            {
                NomeIndustrial(ordem.produtoId),
                ordem.quantidadeResultadoPrevista.ToString("N0") + unidade,
                ordem.estado.ToString().ToUpperInvariant(),
                prazo,
                detalhe
            })
            {
                Label celula = new Label(texto);
                celula.AddToClassList("gov-table-cell");
                linha.Add(celula);
            }
            painelFila.Add(linha);
        }

        conteudo.Add(painelFila);
        detalheAtual = painelFila;
    }

    private void CriarProjetoIndustrial(string receitaId)
    {
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            MostrarMensagem("Sistema industrial indisponivel.");
            return;
        }

        OrdemRefinoIndustrial ordemPendente = industrial.OrdensRefino.FirstOrDefault(o => o != null
            && o.teamId == TeamJogador
            && string.Equals(o.receitaId, receitaId, StringComparison.OrdinalIgnoreCase)
            && o.estado != EstadoOrdemRefinoIndustrial.Cancelada
            && o.estado != EstadoOrdemRefinoIndustrial.Concluida);
        if (ordemPendente != null)
        {
            MostrarFilaIndustrial(receitaId);
            MostrarMensagem("Projeto ja possui uma ordem pendente; nenhuma nova reserva ou cobranca foi feita.");
            return;
        }

        OrdemRefinoIndustrial ordem = industrial.CriarOrdemRefino(TeamJogador, receitaId);
        if (ordem == null)
        {
            MostrarMensagem("Nao foi possivel criar o projeto industrial.");
            return;
        }

        string mensagem = ordem.estado == EstadoOrdemRefinoIndustrial.Aguardando || ordem.estado == EstadoOrdemRefinoIndustrial.Produzindo
            ? "Projeto industrial iniciado: " + NomeIndustrial(ordem.produtoId) + "."
            : "Projeto industrial pendente: " + (string.IsNullOrWhiteSpace(ordem.motivoBloqueio) ? ordem.estado.ToString() : ordem.motivoBloqueio);

        MostrarMensagem(mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private VisualElement CriarGradeCiencia()
    {
        VisualElement grade = new VisualElement();
        grade.AddToClassList("gov-science-grid");
        conteudo.Add(grade);
        return grade;
    }

    private void AdicionarCardCiencia(
        VisualElement pai,
        string indice,
        string titulo,
        string subtitulo,
        string corpo,
        string status,
        string statusClasse,
        string textoAcao = null,
        Action acao = null,
        string classeAcao = null)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("gov-science-card");

        VisualElement topo = new VisualElement();
        topo.AddToClassList("gov-science-top");

        Label selo = new Label(indice);
        selo.AddToClassList("gov-science-index");
        topo.Add(selo);

        VisualElement header = new VisualElement();
        header.style.flexGrow = 1f;

        Label tituloLabel = new Label(titulo);
        tituloLabel.AddToClassList("gov-card-title");
        header.Add(tituloLabel);

        if (!string.IsNullOrWhiteSpace(subtitulo))
        {
            Label subtituloLabel = new Label(subtitulo);
            subtituloLabel.AddToClassList("gov-science-subtitle");
            header.Add(subtituloLabel);
        }

        topo.Add(header);
        card.Add(topo);

        Label corpoLabel = new Label(corpo);
        corpoLabel.AddToClassList("gov-science-body");
        card.Add(corpoLabel);

        Label statusLabel = new Label(status);
        statusLabel.AddToClassList("gov-science-status");
        if (!string.IsNullOrWhiteSpace(statusClasse))
        {
            statusLabel.AddToClassList(statusClasse);
        }
        card.Add(statusLabel);

        if (!string.IsNullOrWhiteSpace(textoAcao) && acao != null)
        {
            Button botao = new Button(acao) { text = textoAcao };
            botao.AddToClassList("gov-mini-button");
            if (!string.IsNullOrWhiteSpace(classeAcao))
            {
                botao.AddToClassList(classeAcao);
            }
            card.Add(botao);
        }

        pai.Add(card);
    }

    private static IEnumerable<string> RecursosIndustriaisBase()
    {
        yield return IndustriaIds.MinerioFerro;
        yield return IndustriaIds.MinerioCobre;
        yield return IndustriaIds.Bauxita;
        yield return IndustriaIds.MinerioTitanio;
        yield return IndustriaIds.UranioBruto;
        yield return IndustriaIds.AcoEstrutural;
        yield return IndustriaIds.CobreEletrolitico;
        yield return IndustriaIds.Duraluminio;
        yield return IndustriaIds.LigaTitanio;
        yield return IndustriaIds.ComponentesEletronicos;
        yield return IndustriaIds.UranioEnriquecido;
    }

    private static string FormatarQuantidadeIndustrial(SistemaIndustrialNacional industrial, int teamId, string recursoId, string unidade = "t")
    {
        if (industrial == null)
        {
            return "N/D";
        }

        double quantidade = industrial.ObterQuantidadePais(teamId, recursoId);
        return quantidade.ToString("N0") + " " + unidade;
    }

    private static string NomeIndustrial(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId))
        {
            return "Item";
        }

        switch (recursoId.Trim().ToLowerInvariant())
        {
            case "minerio_ferro": return "Minerio de Ferro";
            case "minerio_cobre": return "Minerio de Cobre";
            case "bauxita": return "Bauxita";
            case "minerio_titanio": return "Minerio de Titanio";
            case "uranio_bruto": return "Uranio Bruto";
            case "aco_estrutural": return "Aco Estrutural";
            case "cobre_eletrolitico": return "Cobre Eletrolitico";
            case "duraluminio": return "Duraluminio";
            case "liga_titanio": return "Liga de Titanio";
            case "componentes_eletronicos": return "Componentes Eletronicos";
            case "uranio_enriquecido": return "Uranio Enriquecido";
            default:
                string texto = recursoId.Replace('_', ' ').Trim();
                return texto.Length > 0 ? texto.ToUpperInvariant() : recursoId;
        }
    }

    private static string ObterAbundanciaTexto(PerfilMineralPais perfil, RecursoMineral recurso)
    {
        return perfil != null ? perfil.ObterAbundancia(recurso).ToString().Replace("MuitoEscasso", "Muito Escasso") : "Indefinida";
    }

    private static string DescreverEstadoExtracao(OrdemExtracaoIndustrial ordem)
    {
        if (ordem == null)
        {
            return "SEM ORDEM";
        }

        switch (ordem.estado)
        {
            case EstadoOrdemExtracaoIndustrial.Ativa: return "ATIVA";
            case EstadoOrdemExtracaoIndustrial.Aguardando: return "AGUARDANDO";
            case EstadoOrdemExtracaoIndustrial.Pausada: return "PAUSADA";
            case EstadoOrdemExtracaoIndustrial.SemEnergia: return "SEM ENERGIA";
            case EstadoOrdemExtracaoIndustrial.SemVerba: return "SEM VERBA";
            case EstadoOrdemExtracaoIndustrial.ConcluindoCiclo: return "CONCLUINDO CICLO";
            case EstadoOrdemExtracaoIndustrial.Bloqueada: return "BLOQUEADA";
            default: return ordem.estado.ToString().ToUpperInvariant();
        }
    }

    private static string ClasseEstadoExtracao(OrdemExtracaoIndustrial ordem)
    {
        if (ordem == null)
        {
            return "state-warn";
        }

        switch (ordem.estado)
        {
            case EstadoOrdemExtracaoIndustrial.Ativa:
            case EstadoOrdemExtracaoIndustrial.ConcluindoCiclo:
                return "state-good";
            case EstadoOrdemExtracaoIndustrial.Aguardando:
            case EstadoOrdemExtracaoIndustrial.Pausada:
                return "state-info";
            case EstadoOrdemExtracaoIndustrial.SemEnergia:
            case EstadoOrdemExtracaoIndustrial.SemVerba:
                return "state-warn";
            case EstadoOrdemExtracaoIndustrial.Bloqueada:
                return "state-bad";
            default:
                return "state-info";
        }
    }

    private static int CalcularLaboratoriosAtivos(DadosPaisGoverno pais, PerfilMineralPais perfil)
    {
        int ativos = 0;
        if (perfil == null)
        {
            return ativos;
        }

        if (perfil.ferro > AbundanciaMineralNivel.Inexistente) ativos++;
        if (perfil.cobre > AbundanciaMineralNivel.Inexistente) ativos++;
        if (perfil.bauxita > AbundanciaMineralNivel.Inexistente) ativos++;
        if (perfil.titanio > AbundanciaMineralNivel.Baixo || pais.nivelIndustrial >= 50) ativos++;
        if (pais.nivelIndustrial >= 55) ativos++;
        if (pais.tecnologiaExtracaoConcluida && pais.nivelIndustrial >= 70 && pais.estabilidade >= 70f) ativos++;
        return Mathf.Clamp(ativos, 0, 6);
    }

    private static string DescreverEstadoLinha(LinhaIndustrial linha)
    {
        if (linha == null)
        {
            return "Livre";
        }

        switch (linha.estado)
        {
            case EstadoLinhaIndustrial.Livre: return "Livre";
            case EstadoLinhaIndustrial.ReservandoRecursos: return "Reservando recursos";
            case EstadoLinhaIndustrial.Produzindo: return "Produzindo";
            case EstadoLinhaIndustrial.PausadaSemEnergia: return "Pausada sem energia";
            case EstadoLinhaIndustrial.PausadaSemVerba: return "Pausada sem verba";
            case EstadoLinhaIndustrial.Concluida: return "Concluida";
            case EstadoLinhaIndustrial.Cancelada: return "Cancelada";
            default: return linha.estado.ToString();
        }
    }

    private static string UltimasNoticias(SistemaGovernoMundial gov, int limite, params string[] filtros)
    {
        if (gov == null || gov.noticias == null || limite <= 0) return "Sem noticias relevantes.";
        IEnumerable<string> itens = gov.noticias.Where(n => !string.IsNullOrWhiteSpace(n));
        if (filtros != null && filtros.Length > 0)
        {
            itens = itens.Where(n =>
            {
                string lower = n.ToLowerInvariant();
                return filtros.Any(f => !string.IsNullOrWhiteSpace(f) && lower.Contains(f.ToLowerInvariant()));
            });
        }

        List<string> linhas = itens.Take(limite).ToList();
        return linhas.Count > 0 ? string.Join("\n", linhas) : "Sem noticias relevantes.";
    }

    private static void AdicionarTitulo(VisualElement pai, string titulo, string descricao)
    {
        Label h = new Label(titulo); h.AddToClassList("gov-heading"); pai.Add(h);
        Label p = new Label(descricao); p.AddToClassList("gov-subheading"); pai.Add(p);
    }

    private static void AdicionarCard(VisualElement pai, string titulo, string texto)
    {
        VisualElement card = new VisualElement(); card.AddToClassList("gov-card");
        Label h = new Label(titulo); h.AddToClassList("state-info"); card.Add(h);
        Label p = new Label(texto); p.style.marginTop = 10; p.style.whiteSpace = WhiteSpace.Normal; card.Add(p);
        pai.Add(card);
    }

    private static string Moeda(long valor)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(governo.teamJogador) : null;
        string simbolo = pais != null && !string.IsNullOrWhiteSpace(pais.simboloMoeda)
            ? pais.simboloMoeda.Trim() : "$";
        return simbolo + " " + valor.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static string Moeda(int valor)
    {
        return Moeda((long)valor);
    }

    private static string Moeda(float valor)
    {
        return Moeda((long)Math.Round(valor, MidpointRounding.AwayFromZero));
    }

    private static string Moeda(decimal valor)
    {
        if (valor > long.MaxValue) return Moeda(long.MaxValue);
        if (valor < long.MinValue) return Moeda(long.MinValue);
        return Moeda((long)Math.Round(valor, MidpointRounding.AwayFromZero));
    }

    private static string Titulo(string secao, string aba) => secao.ToUpperInvariant() + " - " + aba.ToUpperInvariant();
    private static string Descricao(string secao) => "Painel nacional de " + secao.ToLowerInvariant() + ". Informacoes organizadas para leitura rapida e tomada de decisao.";
}

