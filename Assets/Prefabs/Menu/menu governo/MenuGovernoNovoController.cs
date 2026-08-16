using System;
using System.Collections.Generic;
using System.Linq;
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
    private VisualElement recursos;
    private ScrollView navegacao;
    private ScrollView abas;
    private ScrollView conteudo;
    private ScrollView acoes;
    private Label breadcrumb;
    private string categoria = "Relacoes";
    private string abaAtual = "Resumo";
    // Nenhum alvo de diplomacia deve ser escolhido por acidente. O jogador e o
    // primeiro pais ativo da cena sao os unicos valores seguros no inicio.
    private int paisSelecionado = 1; // alvo inicial seguro; nunca apontar para uma IA inativa
    private float proximaAtualizacao;
    private bool aberturaPendente;
    private bool mercadoEstoquePrimeiro;
    // O mercado abre mostrando apenas o que pode ser transacionado agora;
    // itens indisponiveis continuam acessiveis pelo filtro manual.
    private bool mercadoSomenteDisponiveis = true;
    private string mercadoCategoria = "Todos";

    private static readonly string[] OrdemSecoes = new[]
    {
        "Relacoes",
        "Aliancas",
        "Sancoes",
        "Economia",
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
        { "Mercado", new[] { "Comprar", "Vender", "Precos", "Rotas" } },
        { "Interior", new[] { "Populacao", "Cidades", "Bem-estar", "Projetos" } },
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
    }

    public void Abrir(bool abrir)
    {
        InicializarSeNecessario();
        if (!Pronto || root == null)
        {
            aberturaPendente = abrir;
            StartCoroutine(AbrirQuandoPronto());
            return;
        }

        MenuGoverno.EstaAberto = abrir;
        root.style.display = abrir ? DisplayStyle.Flex : DisplayStyle.None;
        if (abrir)
        {
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

    private System.Collections.IEnumerator AbrirQuandoPronto()
    {
        for (int i = 0; i < 30 && !Pronto; i++)
        {
            yield return null;
            InicializarSeNecessario();
        }

        if (Pronto)
        {
            bool abrir = aberturaPendente;
            aberturaPendente = false;
            Abrir(abrir);
        }
    }

    private void InicializarSeNecessario()
    {
        if (Pronto)
        {
            return;
        }

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
        abaAtual = aba;
        foreach (Button botao in abas.Query<Button>().ToList())
            botao.EnableInClassList("active", botao.text.Equals(aba, StringComparison.OrdinalIgnoreCase));
        if (breadcrumb != null) breadcrumb.text = categoria + " / " + aba;

        conteudo.Clear();
        acoes.Clear();
        AdicionarTitulo(conteudo, Titulo(categoria, aba), Descricao(categoria));
        PreencherDadosBasicos();
        PreencherAcoes();
    }

    private void AtualizarCabecalho()
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(1) : null;
        if (countryName != null)
            countryName.text = pais != null && !string.IsNullOrWhiteSpace(pais.nomePais) ? pais.nomePais.ToUpperInvariant() : "REPUBLICA ATLAS";
        if (countryMeta != null)
            countryMeta.text = pais != null && !string.IsNullOrWhiteSpace(pais.nomePresidente) ? pais.nomePresidente.ToUpperInvariant() : "PRESIDENTE ATLAS";
        if (nationalStatus != null)
            nationalStatus.text = ObterStatusNacional(pais);
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
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(1) : null;
        if (pais == null) return;
        GerenciadorRecursos recursosReais = GerenciadorRecursos.Instancia;
        AdicionarRecurso("TESOURO", Moeda(recursosReais != null ? recursosReais.dinheiro : pais.saldo));
        // O cabeçalho deve ler o armazem vivo. O espelho econômico pode estar
        // atrasado um ciclo, especialmente depois de uma descarga de petroleiro.
        AdicionarRecurso("PETROLEO", EstoqueReal(RecursoMercado.Petroleo).ToString("N0"));
        AdicionarRecurso("ACO", EstoqueReal(RecursoMercado.Aco).ToString("N0"));
        AdicionarRecurso("ENERGIA", EstoqueReal(RecursoMercado.Energia).ToString("N0"));
        AdicionarRecurso("COMIDA", EstoqueReal(RecursoMercado.Comida).ToString("N0"));
        GerenciadorArmazens armazens = GerenciadorArmazens.Instancia;
        string agua = armazens != null && armazens.armazemRecursos != null
            ? Mathf.Max(0, armazens.armazemRecursos.agua).ToString("N0") + "/" + Mathf.Max(0, armazens.armazemRecursos.aguaMaximo).ToString("N0")
            : "0/0";
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
                if (identidade != null && identidade.estaAtivo && !identidade.eliminado && identidade.teamID > 1)
                    idsAtivos.Add(identidade.teamID);
            }
        }

        resultado.AddRange(governo.Paises.Where(p => p != null && idsAtivos.Contains(p.teamId)).OrderBy(p => p.teamId));
        return resultado;
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
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(1) : null;
        if (pais == null) { AdicionarCard(conteudo, "DADOS INDISPONIVEIS", "O sistema de governo ainda esta inicializando."); return; }
        switch (categoria)
        {
            case "Relacoes": ConstruirRelacoes(); break;
            case "Aliancas": ConstruirAliancas(pais); break;
            case "Sancoes": ConstruirSancoes(); break;
            case "Economia": ConstruirEconomia(pais); break;
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
                Acao("ROMPER ACORDO", () => Executar(() => SistemaGovernoMundial.Instancia.RomperAlianca(paisSelecionado), "Alianca encerrada."), "danger");
                break;
            case "Aliancas":
                Acao("CONVIDAR PAIS", ProporAliancaSelecionada);
                Acao("FIRMAR DEFESA MUTUA", ProporPactoSelecionado);
                Acao("NEGOCIAR CESSAR-FOGO", ProporCessarFogoSelecionado, "warning");
                Acao("ENVIAR RECURSOS", () => CriarProposta(TipoPropostaInternacional.Doacao, RecursoMercado.Comida, 100));
                break;
            case "Sancoes":
                Acao("IMPOR SANCAO", () => Executar(() => SistemaGovernoMundial.Instancia.AplicarSancao(paisSelecionado), "Sancao aplicada."), "warning");
                Acao("LEVANTAR SANCAO", () => Executar(() => SistemaGovernoMundial.Instancia.RemoverSancao(paisSelecionado), "Sancao removida."));
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
                    Acao("GERAR EMPREGOS", () => Executar(() => SistemaGovernoMundial.Instancia.AlterarEmprego(1, 3f), "Programa de empregos executado."));
                    Acao("INVESTIR EM MORADIA", MelhorarMoradia);
                    Acao("AJUSTAR IMPOSTOS +1", () => AjustarImposto(1));
                    Acao("AJUSTAR IMPOSTOS -1", () => AjustarImposto(-1));
                }
                else if (abaAtual == "Producao")
                {
                    Acao("INVESTIR EM INDUSTRIA", () => Investir("industria"));
                    Acao("INVESTIR EM ENERGIA", () => Investir("energia"));
                    Acao("ATUALIZAR DIAGNOSTICO", AtualizarDiagnosticoProducao);
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
            case "Mercado":
                Acao("ATUALIZAR COTACOES", AtualizarMercado);
                Acao("ABRIR PRECOS", () => MostrarPagina("Precos"));
                Acao("ABRIR VENDAS", () => MostrarPagina("Vender"));
                Acao("ABRIR ROTAS", () => MostrarPagina("Rotas"));
                break;
            case "Interior":
                Acao("MELHORAR MORADIA", MelhorarMoradia, "warning");
                Acao("MUTIRAO DE EMPREGOS", () => Executar(() => SistemaGovernoMundial.Instancia.AlterarEmprego(1, 3f), "Emprego nacional ampliado."));
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
                        DadosPaisGoverno pais = gov != null ? gov.ObterPais(1) : null;
                        if (pais != null)
                        {
                            if (pais.sateliteDefesa == null) pais.sateliteDefesa = new SateliteDefesaEstado();
                            gov.ConfigurarSatelite(1, !pais.sateliteDefesa.manutencaoAutomatica);
                            MostrarMensagem("Manutencao automatica do satelite atualizada.");
                            MostrarPagina(abaAtual);
                        }
                    });
                    Acao("APORTAR $1.200 NO SATELITE", () =>
                    {
                        string mensagem = "Programa satelital indisponivel.";
                        bool ok = SistemaGovernoMundial.Instancia != null
                            && SistemaGovernoMundial.Instancia.InvestirNoSatelite(1, 1200, out mensagem);
                        MostrarMensagem(ok ? mensagem : "Aporte recusado: " + mensagem);
                        MostrarPagina(abaAtual);
                    }, "warning");
                }
                break;
            case "Ciencia":
                if (abaAtual == "Pesquisa")
                {
                    Acao("PRIORIZAR INDUSTRIA", () => Investir("industria"));
                    Acao("PRIORIZAR ENERGIA", () => Investir("energia"));
                    Acao("ATUALIZAR RELATORIO", () => { MostrarMensagem("Relatorio cientifico atualizado."); MostrarPagina(abaAtual); });
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
                    Acao("ATUALIZAR FILA", () => { MostrarMensagem("Fila industrial atualizada."); MostrarPagina(abaAtual); });
                }
                else
                {
                    Acao("INVESTIR EM CIENCIA", () => Investir("ciencia"));
                    Acao("PRIORIZAR ENERGIA", () => Investir("energia"));
                    Acao("ABRIR PESQUISA", () => MostrarPagina("Pesquisa"));
                }
                break;
        }
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
            ok = SistemaFederacoesGlobais.Instancia.TrocarFederacao(1, destino, out mensagem);
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

    private void AtualizarGastosMilitares()
    {
        SistemaGastosMilitares.GarantirInstancia();
        MostrarMensagem("Historico de gastos militares sincronizado.");
        MostrarPagina(abaAtual);
    }

    private void AtualizarDiagnosticoProducao()
    {
        SistemaEconomiaImoveis.Instancia?.Recalcular();
        SistemaGovernoMundial.Instancia?.ProcessarEconomia();
        MostrarMensagem("Diagnostico de producao recalculado.");
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
        bool ok = SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.InvestirCapacidadeNacional(1, foco);
        MostrarMensagem(ok ? "Investimento em " + foco + " realizado." : "Recursos insuficientes para investir em " + foco + ".");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void MelhorarMoradia()
    {
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        bool ok = gov != null && gov.TentarPagar(1, 900);
        if (ok) gov.AlterarMoradia(1, 3f);
        MostrarMensagem(ok ? "Investimento em moradia realizado." : "Recursos insuficientes para moradia.");
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void AtualizarMercado()
    {
        if (SistemaMercadoGlobal.Instancia != null)
            SistemaMercadoGlobal.Instancia.SimularMercado();
        MostrarMensagem("Cotacoes do mercado atualizadas.");
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
        bool ok = SistemaFederacoesGlobais.Instancia != null && SistemaFederacoesGlobais.Instancia.QuitarEmprestimo(1, loanId, out mensagem);
        MostrarMensagem(ok ? "Emprestimo quitado." : "Nao foi possivel quitar: " + mensagem);
        MostrarPagina(abaAtual);
    }

    private void ConstruirMercado()
    {
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

        var itens = mercado.ItensOrdenados().ToList();
        int compras = mercado.historico.Count(t => t != null && t.compradorTeamId == 1);
        int vendas = mercado.historico.Count(t => t != null && t.vendedorTeamId == 1);
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
                ? "Mostra apenas itens com oferta global e compra habilitada."
                : "Mostra apenas itens que possuem estoque para venda.";
            somenteDisponiveis.RegisterValueChangedCallback(evt =>
            {
                mercadoSomenteDisponiveis = evt.newValue;
                MostrarPagina(abaAtual);
            });
            filtros.Add(somenteDisponiveis);

            VisualElement categorias = new VisualElement();
            categorias.AddToClassList("gov-market-category-filters");
            foreach (string nomeCategoria in new[] { "Todos", "Minerios", "Combustiveis", "Alimentos", "Tanques", "Navios", "Aeronaves", "Armas", "Municoes" })
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
            IEnumerable<DadosItemMercado> itensExibicao = itens;
            if (!string.Equals(mercadoCategoria, "Todos", StringComparison.OrdinalIgnoreCase))
                itensExibicao = itensExibicao.Where(item => CategoriaMercado(item) == mercadoCategoria);
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            int timeJogador = governo != null ? governo.teamJogador : 1;
            if (mercadoSomenteDisponiveis)
            {
                itensExibicao = itensExibicao.Where(item =>
                {
                    int estoque = EstoqueMercadoDoTime(item, timeJogador);
                    return comprar
                        ? item.podeComprar && item.precoAtual > 0f && item.estoqueGlobal > 0
                        : item.podeVender && item.precoAtual > 0f && estoque > 0;
                });
            }
            if (mercadoEstoquePrimeiro)
            {
                itensExibicao = itensExibicao
                    .OrderByDescending(item => EstoqueMercadoDoTime(item, timeJogador) > 0)
                    .ThenBy(item => item.NomeFormatado);
            }
            foreach (DadosItemMercado item in itensExibicao)
                AdicionarCardMercado(grade, item, comprar);
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
        foreach (DadosItemMercado item in itens.Take(12))
        {
            string status = item.precoAtual <= 0f ? "SEM PRECO" : (item.podeComprar ? "DISPONIVEL" : "LIMITADO");
            TabelaLinha(item.NomeFormatado, Moeda(item.precoAtual),
                item.variacaoPercentual.ToString("+0.0;-0.0;0.0") + "%", item.estoqueGlobal.ToString("N0"), status);
        }
    }

    private void AdicionarCardMercado(VisualElement grade, DadosItemMercado item, bool comprar)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        int timeJogador = governo != null ? governo.teamJogador : 1;
        int estoqueJogador = EstoqueMercadoDoTime(item, timeJogador);
        int quantidadeInicial = comprar
            ? Mathf.Max(1, item.CalcularQuantidadePadrao())
            : Mathf.Clamp(item.CalcularQuantidadePadrao(), 1, Mathf.Max(1, estoqueJogador));

        VisualElement card = new VisualElement();
        card.AddToClassList("gov-market-card");

        Label titulo = new Label(item.NomeFormatado.ToUpperInvariant());
        titulo.AddToClassList("gov-card-title");
        card.Add(titulo);

        string variacao = item.variacaoPercentual.ToString("+0.0;-0.0;0.0") + "%";
        Label detalhes = new Label(
            "Preco unitario: " + Moeda(item.precoAtual) +
            "\nVariacao: " + variacao +
            (comprar ? "\nOferta global: " + item.estoqueGlobal.ToString("N0") + (item.municaoMilitar ? " cart." : " t") : "\nSeu estoque: " + estoqueJogador.ToString("N0") + (item.municaoMilitar ? " cart." : " t")) +
            (item.municaoMilitar ? "\nCada disparo desconta um cartucho do estoque da unidade." : "\nLiquidacao no estoque agregado do armazem."));
        detalhes.AddToClassList("gov-market-details");
        card.Add(detalhes);

        IntegerField quantidade = new IntegerField(item.municaoMilitar ? "QUANTIDADE (CARTUCHOS)" : "QUANTIDADE (t)");
        quantidade.value = quantidadeInicial;
        quantidade.AddToClassList("gov-quantity");
        card.Add(quantidade);

        Label total = new Label();
        total.AddToClassList("gov-market-total");
        Action atualizarTotal = () =>
        {
            int valor = Mathf.Max(1, quantidade.value);
            total.text = (comprar ? "TOTAL: " : "RECEBER: ") + Moeda((long)valor * item.precoAtual) + "  |  " + valor.ToString("N0") + (item.municaoMilitar ? " cart." : " t");
        };
        Toggle repeticao = null;
        quantidade.RegisterValueChangedCallback(evt =>
        {
            int limite = comprar ? Mathf.Max(1, item.estoqueGlobal) : Mathf.Max(1, estoqueJogador);
            int ajustada = Mathf.Clamp(evt.newValue, 1, limite);
            if (ajustada != evt.newValue) quantidade.SetValueWithoutNotify(ajustada);
            atualizarTotal();
            if (repeticao != null && repeticao.value)
                SistemaLogisticaMercado.Instancia?.ConfigurarRepeticao(item.id, ajustada, comprar, true);
        });
        atualizarTotal();
        card.Add(total);

        SistemaLogisticaMercado.GarantirInstancia();
        repeticao = new Toggle("REPETIR A CADA 2 DIAS");
        repeticao.value = SistemaLogisticaMercado.Instancia != null && SistemaLogisticaMercado.Instancia.TemRepeticao(item.id, comprar);
        repeticao.AddToClassList("gov-market-repeat");
        repeticao.RegisterValueChangedCallback(evt =>
        {
            SistemaLogisticaMercado.Instancia?.ConfigurarRepeticao(item.id, Mathf.Max(1, quantidade.value), comprar, evt.newValue);
        });
        card.Add(repeticao);

        VisualElement botoes = new VisualElement();
        botoes.AddToClassList("gov-inline-actions");
        Button menos = new Button(() => quantidade.value = Mathf.Max(1, quantidade.value - quantidadeInicial)) { text = "-" + quantidadeInicial };
        Button mais = new Button(() => quantidade.value += quantidadeInicial) { text = "+" + quantidadeInicial };
        Button confirmar = new Button(() => Transacionar(item, Mathf.Max(1, quantidade.value), comprar))
        {
            text = comprar ? "COMPRAR" : "VENDER"
        };
        menos.AddToClassList("gov-mini-button");
        mais.AddToClassList("gov-mini-button");
        confirmar.AddToClassList("gov-mini-button");
        confirmar.AddToClassList(comprar ? "buy" : "warning");
        bool disponivel = comprar
            ? item.podeComprar && item.precoAtual > 0 && item.estoqueGlobal > 0
            : item.podeVender && item.precoAtual > 0 && estoqueJogador > 0;
        confirmar.SetEnabled(disponivel);
        botoes.Add(menos);
        botoes.Add(mais);
        botoes.Add(confirmar);
        card.Add(botoes);

        if (!disponivel)
        {
            Label aviso = new Label(comprar ? "INDISPONIVEL PARA COMPRA" : "SEM ESTOQUE PARA VENDA");
            aviso.AddToClassList("gov-market-warning");
            card.Add(aviso);
        }
        grade.Add(card);
    }

    private void AjustarImposto(int delta)
    {
        bool ok = SistemaGovernoMundial.Instancia != null;
        if (ok)
        {
            ok &= SistemaGovernoMundial.Instancia.AjustarImposto(1, "moradia", delta);
            ok &= SistemaGovernoMundial.Instancia.AjustarImposto(1, "industria", delta);
            ok &= SistemaGovernoMundial.Instancia.AjustarImposto(1, "comercio", delta);
        }
        MostrarMensagem(ok ? "Impostos nacionais ajustados." : "O imposto ja atingiu o limite permitido.");
        MostrarPagina(abaAtual);
    }

    private void Plano(string plano)
    {
        bool ok = SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.DefinirPlanoEstrategico(1, plano);
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

    private void Transacionar(DadosItemMercado item, int quantidade, bool comprar)
    {
        SistemaGovernoMundial g = SistemaGovernoMundial.Instancia;
        SistemaMercadoGlobal m = SistemaMercadoGlobal.Instancia;
        if (g == null || m == null) return;
        string mensagem = string.Empty;
        bool ok = false;
        if (comprar)
        {
            SistemaGastosMilitares.GarantirInstancia();
            DadosPaisGoverno vendedor = m.EncontrarMelhorFornecedor(g.teamJogador, item, quantidade);
            if (vendedor == null)
            {
                mensagem = "Nenhum fornecedor amigo possui estoque ou rota comercial ativa.";
            }
            else
            {
                ok = m.Comprar(1, vendedor.teamId, item.id, quantidade, out mensagem);
            }
        }
        else
        {
            bool recursoCivilReal = item.recurso == RecursoMercado.Comida
                || item.recurso == RecursoMercado.Petroleo
                || item.recurso == RecursoMercado.Energia
                || item.recurso == RecursoMercado.Aco;

            if (recursoCivilReal)
            {
                DadosPaisGoverno comprador = m.EncontrarMelhorComprador(g.teamJogador, item, quantidade);
                if (comprador == null)
                    mensagem = "Nenhum pais amigo demonstrou interesse ou possui saldo para esta operacao.";
                else
                    ok = m.Vender(g.teamJogador, comprador.teamId, item.id, quantidade, out mensagem);
            }
            else
            {
                DadosPaisGoverno comprador = m.EncontrarMelhorComprador(g.teamJogador, item, quantidade);
                if (comprador == null)
                    mensagem = "Nenhum pais amigo demonstrou interesse ou possui saldo para esta operacao.";
                else
                    ok = m.Vender(g.teamJogador, comprador.teamId, item.id, quantidade, out mensagem);
            }
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
        switch (item.recurso)
        {
            case RecursoMercado.Comida: return "Alimentos";
            case RecursoMercado.Petroleo: return "Combustiveis";
            case RecursoMercado.MinerioFerro:
            case RecursoMercado.MinerioCobre:
            case RecursoMercado.Bauxita:
            case RecursoMercado.MinerioTitanio:
            case RecursoMercado.Uranio: return "Minerios";
            default:
                string texto = ((item.categoria ?? string.Empty) + " " + (item.nome ?? string.Empty)).ToLowerInvariant();
                return texto.Contains("combust") || texto.Contains("gasolina") || texto.Contains("diesel") ? "Combustiveis" : "Outros";
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
        return teamId == 1 ? EstoqueReal(item != null ? item.recurso : RecursoMercado.Aco) : 0;
    }

    private int ContarRelacoes(Func<RelacaoPaisGoverno, bool> filtro)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return 0;
        HashSet<int> idsAtivos = new HashSet<int>(ObterNacoesAtivas(governo).Select(n => n.teamId));
        return governo.Relacoes.Count(r => r != null && r.Envolve(1, r.Outro(1)) && idsAtivos.Contains(r.Outro(1)) && filtro(r));
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
        foreach (string valor in new[] { a, b, c, d }) { Label label = new Label(valor); label.AddToClassList("gov-table-cell"); linha.Add(label); }
        if (clique == null) { Label label = new Label(e); label.AddToClassList("gov-table-cell"); linha.Add(label); }
        else { Button botao = new Button(clique) { text = e }; botao.AddToClassList("gov-table-cell"); botao.AddToClassList("gov-row-button"); linha.Add(botao); }
        conteudo.Add(linha);
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
        if (n.teamId != 1 && SistemaGovernoMundial.Instancia != null)
        {
            SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
            PropostaInternacional pedido = gov.Propostas.FirstOrDefault(p => p != null && p.EstaPendente
                && p.tipo == TipoPropostaInternacional.PedidoAjuda && p.origemTeamId == n.teamId && p.alvoTeamId == 1);
            if (pedido != null)
            {
                Label alerta = new Label("PEDIDO DE AJUDA: " + pedido.recurso.ToString().ToUpperInvariant() + " x" + pedido.quantidade.ToString("N0"));
                alerta.AddToClassList("state-warn");
                card.Add(alerta);
            }
            PosturaRelacaoPais postura = gov.ObterPostura(1, n.teamId);
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
                if (escolha == postura) botao.AddToClassList("selected");
                posturaBotoes.Add(botao);
            }
            card.Add(posturaBotoes);
        }
        Button selecionar = new Button(() => { paisSelecionado = n.teamId; MostrarMensagem(n.teamId == 1 ? "Este e o seu pais." : n.nomePais + " definido como alvo diplomatico."); MostrarPagina(abaAtual); }) { text = n.teamId == 1 ? "SEU PAIS" : "SELECIONAR ALVO" };
        selecionar.AddToClassList("gov-row-button");
        card.Add(selecionar);
        conteudo.Add(card);
    }

    private void DefinirPosturaMenu(int alvoTeamId, PosturaRelacaoPais postura)
    {
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        if (gov == null) return;
        string mensagem = "Relacao indisponivel.";
        bool ok = gov.DefinirPostura(1, alvoTeamId, postura, out mensagem);
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
            if (r == null || !r.Envolve(1, r.Outro(1))) continue;
            if (r.valor >= 25) positivas++;
            else if (r.valor < 0) negativas++;
        }

        if (abaAtual == "Resumo")
        {
            LinhaCards(new[] { ("INFLUENCIA", Mathf.Clamp(50 + positivas * 8 - negativas * 4, 0, 100) + "/100", "state-info"), ("TRATADOS ATIVOS", ContarRelacoes(r => r.tratadoComercial).ToString(), "state-good"), ("FOCOS DE TENSAO", negativas.ToString(), "state-bad"), ("PEDIDOS PENDENTES", g.ObterPropostasPendentesPara(1).Count().ToString(), "state-warn") });
            DadosPaisGoverno selecionado = g.ObterPais(paisSelecionado);
            if (selecionado != null)
                AdicionarCard(conteudo, "PAIS SELECIONADO", selecionado.nomePais + "\nFederacao: " + SistemaFederacoesGlobais.NomeFederacao(selecionado.federacaoGlobal) + "\nPresidente: " + selecionado.nomePresidente + "\nStatus: " + ObterStatusNacional(selecionado));
        }
        else if (abaAtual == "Tratados")
        {
            LinhaCards(new[] { ("TRATADOS", ContarRelacoes(r => r.tratadoComercial).ToString(), "state-good"), ("PACTOS", ContarRelacoes(r => r.pactoMilitar).ToString(), "state-info"), ("RELACOES POSITIVAS", positivas.ToString(), "state-good"), ("PEDIDOS PENDENTES", g.ObterPropostasPendentesPara(1).Count().ToString(), "state-warn") });
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
            LinhaCards(new[] { ("INFLUENCIA", Mathf.Clamp(50 + positivas * 8 - negativas * 4, 0, 100) + "/100", "state-info"), ("TRATADOS ATIVOS", ContarRelacoes(r => r.tratadoComercial).ToString(), "state-good"), ("FOCOS DE TENSAO", negativas.ToString(), "state-bad"), ("PEDIDOS PENDENTES", g.ObterPropostasPendentesPara(1).Count().ToString(), "state-warn") });
        }

        if (abaAtual == "Crises")
        {
            TabelaCabecalho("PAIS", "TIPO", "RELACAO", "SITUACAO", "ACAO");
            foreach (DadosPaisGoverno outro in nacoesAtivas)
            {
                if (outro.teamId == 1) continue;
                RelacaoPaisGoverno r = g.ObterRelacao(1, outro.teamId);
                if (r == null || (!r.guerraDeclarada && !r.sancaoAtiva && r.valor >= 0)) continue;
                string tipo = r.guerraDeclarada ? "GUERRA" : r.sancaoAtiva ? "SANCAO" : "TENSAO";
                TabelaLinha(outro.nomePais, outro.bloco, r.valor.ToString("+0;-0;0"), tipo, "SELECIONAR", () => { paisSelecionado = outro.teamId; MostrarMensagem(outro.nomePais + " selecionado."); MostrarPagina(abaAtual); });
            }
            AdicionarCard(conteudo, "HISTORICO RECENTE", UltimasNoticias(g, 5, "guerra", "sanc", "tens", "crise"));
            return;
        }

        if (abaAtual == "Tratados")
        {
            DadosPaisGoverno jogador = g.ObterPais(1);
            AdicionarCard(conteudo, "CONCORDIA GLOBAL", "Cooperacao, ajuda humanitaria e protecao diplomatica. Filiacao atual: " + (jogador != null && jogador.federacaoGlobal == SistemaFederacoesGlobais.TipoFederacao.CooperacaoGlobal.ToString() ? "SIM" : "NAO"));
            AdicionarCard(conteudo, "COALIZAO AEGIS", "Defesa mutua, padroes militares e credito de armamento. Filiacao atual: " + (jogador != null && jogador.federacaoGlobal == SistemaFederacoesGlobais.TipoFederacao.AliancaDefesa.ToString() ? "SIM" : "NAO"));
            if (jogador != null)
                AdicionarCard(conteudo, "DOCUMENTO DE FILIACAO", jogador.nomePais + " / " + jogador.nomePresidente + "\nFederacao: " + SistemaFederacoesGlobais.NomeFederacao(jogador.federacaoGlobal) + "\nLegitimidade: " + jogador.legitimidadeGlobal.ToString("0") + "%\nMoeda de liquidacao internacional: Dolar Hegemonico (DH)");
            TabelaCabecalho("PAIS", "TRATADO", "PACTO", "PEDIDO", "RELACAO");
            foreach (DadosPaisGoverno outro in nacoesAtivas)
            {
                if (outro.teamId == 1) continue;
                RelacaoPaisGoverno r = g.ObterRelacao(1, outro.teamId);
                TabelaLinha(outro.nomePais, r != null && r.tratadoComercial ? "SIM" : "NAO", r != null && r.pactoMilitar ? "SIM" : "NAO", r != null && r.pedidoPendente ? "SIM" : "NAO", r != null ? r.valor.ToString("+0;-0;0") : "0", () => { paisSelecionado = outro.teamId; MostrarMensagem(outro.nomePais + " selecionado."); MostrarPagina(abaAtual); });
            }
            return;
        }

        TabelaCabecalho("PAIS", "BLOCO", "RELACAO", "STATUS", "ACAO");
        foreach (DadosPaisGoverno outro in nacoesAtivas)
        {
            if (outro.teamId == 1) continue;
            RelacaoPaisGoverno r = g.ObterRelacao(1, outro.teamId);
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

        List<DadosPaisGoverno> aliados = new List<DadosPaisGoverno>(g.ObterAliados(1));
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
                    confianca = Mathf.RoundToInt((float)grupo.Average(p => Mathf.Clamp((g.ObterRelacao(1, p.teamId)?.valor ?? 0) + 50, 0, 100)))
                })
                .OrderByDescending(x => x.membros)
                .ToList();

            LinhaCards(new (string titulo, string valor, string estado)[] { ("FEDERACAO ATUAL", SistemaFederacoesGlobais.NomeFederacao(pais.federacaoGlobal), "state-warn"), ("BLOCOS ATIVOS", blocos.Count.ToString(), "state-info"), ("FORCA COMBINADA", Mathf.Clamp((float)(pais.nivelMilitar + aliados.Count * 6), 0f, 100f).ToString("0") + "%", "state-good"), ("RISCO DE RUPTURA", (100f - pais.legitimidadeGlobal).ToString("0") + "%", "state-bad") });
            TabelaCabecalho("BLOCO", "MEMBROS", "FORCA", "CONFIANCA", "ACAO");
            foreach (var bloco in blocos)
                TabelaLinha(bloco.nome, bloco.membros.ToString(), bloco.forca.ToString(), bloco.confianca.ToString() + "%", "SELECIONAR", () => MostrarMensagem("Bloco " + bloco.nome + " analisado."));
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
            List<PropostaInternacional> pendentes = g.ObterPropostasPendentesPara(1)
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
                    TabelaLinha(g.NomePais(proposta.origemTeamId), proposta.tipo.ToString(), proposta.recurso.ToString(), proposta.quantidade.ToString(), "ANALISAR", () => MostrarMensagem("Pedido de " + g.NomePais(proposta.origemTeamId) + " selecionado."));
            }
            return;
        }

        if (abaAtual == "Pactos")
        {
            LinhaCards(new[] { ("MEMBROS ALIADOS", aliados.Count.ToString(), "state-info"), ("PACTOS ATIVOS", ContarRelacoes(r => r.pactoMilitar).ToString(), "state-good"), ("TRATADOS", ContarRelacoes(r => r.tratadoComercial).ToString(), "state-good"), ("RISCO DE RUPTURA", (100f - pais.estabilidade).ToString("0") + "%", "state-bad") });
            TabelaCabecalho("PAIS", "TRATADO", "PACTO", "RELACAO", "ACAO");
            foreach (DadosPaisGoverno p in nacoesAtivas.Where(x => x != null && x.teamId != 1))
            {
                RelacaoPaisGoverno r = g.Relacoes.FirstOrDefault(rel => rel != null && rel.Envolve(1, p.teamId));
                if (r == null || (!r.pactoMilitar && !r.tratadoComercial)) continue;
                TabelaLinha(p.nomePais, r.tratadoComercial ? "SIM" : "NAO", r.pactoMilitar ? "SIM" : "NAO", r.valor.ToString("+0;-0;0"), "SELECIONAR", () => { paisSelecionado = p.teamId; MostrarPagina(abaAtual); });
            }
            return;
        }

        LinhaCards(new[] { ("BLOCO ATUAL", pais.bloco, "state-warn"), ("MEMBROS ALIADOS", aliados.Count.ToString(), "state-info"), ("FORCA COMBINADA", Mathf.Clamp(pais.nivelMilitar + aliados.Count * 6, 0, 100) + "%", "state-good"), ("RISCO DE RUPTURA", (100f - pais.estabilidade).ToString("0") + "%", "state-bad") });
        TabelaCabecalho("PAIS", "PRONTO PARA APOIO", "LEALDADE", "PACTO", "ACAO");
        foreach (DadosPaisGoverno p in aliados)
        {
            RelacaoPaisGoverno r = g.ObterRelacao(1, p.teamId);
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
        DadosPaisGoverno jogador = g.ObterPais(1);
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
            if (p.teamId == 1) continue;
            RelacaoPaisGoverno r = g.ObterRelacao(1, p.teamId);
            if (abaAtual == "Ativas" && (r == null || !r.sancaoAtiva)) continue;
            if (abaAtual == "Embargos" && (r == null || (!r.sancaoAtiva && r.valor >= 0))) continue;
            string embargoes = r != null && r.embargos != null && r.embargos.Count > 0 ? string.Join(",", r.embargos.Select(e => e.ToString())) : "NENHUM";
            TabelaLinha(p.nomePais, abaAtual == "Embargos" ? embargoes : (r != null && r.sancaoAtiva ? "ATIVA" : "INATIVA"), r != null ? r.valor.ToString() : "0", p.emGuerra ? "HOSTIL" : "ESTAVEL", "SELECIONAR", () => { paisSelecionado = p.teamId; MostrarPagina(abaAtual); });
        }
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
            RelatorioOrcamentoNacional rel = SistemaOrcamentoNacional.ObterOuCriar().GerarRelatorio(p != null ? p.teamId : 1);

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
                TabelaLinha(rec.nome, rec.baseCalculo, "+" + Moeda(rec.valorDiario) + "/dia", rec.tendencia, rec.status, () => MostrarMensagem(info));
            }

            AdicionarCard(conteudo, "BALANÇO GERAL DE DESPESAS",
                "Consolidação detalhada dos custos operacionais do país. Clique em qualquer linha para inspecionar os detalhes de cálculo.");
            TabelaCabecalho("DESPESA", "BASE DE CÁLCULO", "VALOR / DIA", "TENDÊNCIA", "STATUS");
            foreach (var desp in rel.despesas)
            {
                string info = desp.detalhamento;
                TabelaLinha(desp.nome, desp.baseCalculo, Moeda(desp.valorDiario) + "/dia", desp.tendencia, desp.status, () => MostrarMensagem(info));
            }
            return;
        }

        if (abaAtual == "Producao")
        {
            float comida = eco != null ? eco.comidaProduzida : p.comida;
            float petroleo = eco != null ? eco.petroleoProduzido : p.petroleo;
            float industria = eco != null ? eco.industriaProduzida : p.producao;
            float energia = eco != null ? eco.energiaProduzida : p.energia;
            float consumoEnergia = eco != null ? eco.energiaConsumida : p.gastosPorSegundo;
            float deficitComida = eco != null ? eco.deficitComida : p.deficitComida;
            float deficitEnergia = eco != null ? eco.deficitEnergia : p.deficitEnergia;
            float deficitPetroleo = eco != null ? eco.deficitPetroleo : p.deficitPetroleo;
            string principal = eco != null ? eco.ProducaoPrincipal : "N/D";

            LinhaCards(new[]
            {
                ("COMIDA", comida.ToString("0.0"), deficitComida <= 0f ? "state-good" : "state-warn"),
                ("PETROLEO", petroleo.ToString("0.0"), deficitPetroleo <= 0f ? "state-good" : "state-warn"),
                ("INDUSTRIA", industria.ToString("0.0"), "state-info"),
                ("ENERGIA", energia.ToString("0.0"), deficitEnergia <= 0f ? "state-good" : "state-bad"),
                ("CONSUMO", consumoEnergia.ToString("0.0"), "state-info"),
                ("PRINCIPAL", principal, "state-warn")
            });

            AdicionarCard(conteudo, "PRODUCAO NACIONAL",
                "A producao agora reflete o snapshot do motor economico.\n" +
                "Os deficits em comida, energia e petroleo afetam o saldo operacional, a qualidade de vida e a estabilidade.");
            TabelaCabecalho("RECURSO", "PRODUCAO", "CONSUMO", "DEFICIT", "STATUS");
            float consumoComida = eco != null ? eco.comidaConsumida : (p.populacaoCivil / 100f);
            TabelaLinha("COMIDA", comida.ToString("0.0"), consumoComida.ToString("0.0"), deficitComida.ToString("0.0"), deficitComida <= 0f ? "SUPRIDA" : "FALTA");
            TabelaLinha("ENERGIA", energia.ToString("0.0"), consumoEnergia.ToString("0.0"), deficitEnergia.ToString("0.0"), deficitEnergia <= 0f ? "SUPRIDA" : "FALTA");
            // O consumo exibido precisa vir do mesmo livro-caixa que atualiza a economia.
            // Nunca derive um valor fictício da produção industrial: combustivelConsumido
            // é acumulado pelo SistemaEconomiaImoveis a partir das estruturas/unidades ativas.
            float consumoPetroleo = eco != null ? eco.combustivelConsumido : 0f;
            TabelaLinha("PETROLEO", petroleo.ToString("0.0"), consumoPetroleo.ToString("0.0"), deficitPetroleo.ToString("0.0"), deficitPetroleo <= 0f ? "SUPRIDA" : "FALTA");
            TabelaLinha("INDUSTRIA", industria.ToString("0.0"), "-", p.nivelIndustrial.ToString("0"), p.nivelIndustrial >= 50 ? "FORTE" : "FRACA");
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
                TabelaLinha("Empresa " + (i + 1), empresa.tipo.ToString(), empresa.funcionariosContratados.ToString(), empresa.vagasAbertas.ToString(), empresa.estado, () => MostrarMensagem(motivoEmpresa));
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

    private void AjustarImpostoIndividual(string setor, int delta)
    {
        bool ok = SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.AjustarImposto(1, setor, delta);
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
                Moeda(registro.valorUnitario), Moeda(registro.valorTotal), () => MostrarMensagem(detalhe));
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
                () => MostrarMensagem(municao.descricao));
        }
    }

    private void ConstruirInterior(DadosPaisGoverno p)
    {
        GerenciadorDivisaoTerritorial.GarantirInstancia();
        List<CidadeEstado> cidades = GerenciadorDivisaoTerritorial.Instancia != null ? GerenciadorDivisaoTerritorial.Instancia.cidades.Where(c => c != null && c.teamID == 1).ToList() : new List<CidadeEstado>();

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
                string tipo = cidade.ehEstado ? "ESTADO" : "CIDADE";
                string aero = cidade.temAeroporto ? "SIM" : "NAO";
                string dominio = cidade.teamID == 1 ? "JOGADOR" : cidade.teamID > 1 ? "IA " + cidade.teamID : "NEUTRO";
                TabelaLinha(cidade.nome, tipo, cidade.populacaoCivil.ToString("N0"), aero, dominio, () => MostrarMensagem(cidade.nome + " selecionada."));
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
        AdicionarCard(conteudo, "PROJETOS INTERNOS", cidades.Count > 0
            ? string.Join("\n", cidades.OrderBy(c => c.atratividade).Take(4).Select(c => c.nome + " | atratividade " + c.atratividade.ToString("0") + "% | empregos " + c.empregosTotais + " | vagas " + c.vagasDeEmpregoAbertas))
            : "Nenhum projeto interno ativo.");
    }

    private void ConstruirDefesa(DadosPaisGoverno p)
    {
        SistemaGovernoMundial g = SistemaGovernoMundial.Instancia;
        if (g == null) { AdicionarCard(conteudo, "DADOS INDISPONIVEIS", "Defesa ainda nao foi inicializada."); return; }

        SistemaGastosMilitares.GarantirInstancia();
        SistemaGastosMilitares gastosMilitares = SistemaGastosMilitares.Instancia;
        DefinicaoMunicaoMilitar aresMunicao = gastosMilitares != null ? gastosMilitares.ObterMunicao("municao_ares_ar") : null;

        GerenciadorDivisaoTerritorial.GarantirInstancia();
        int aeroportos = GerenciadorDivisaoTerritorial.Instancia != null ? GerenciadorDivisaoTerritorial.Instancia.ObterCidadesComAeroporto(1).Count : 0;
        int portos = GerenciadorDivisaoTerritorial.Instancia != null ? GerenciadorDivisaoTerritorial.Instancia.cidades.Count(c => c != null && c.teamID == 1 && c.temPorto) : 0;

        if (abaAtual == "Comando")
        {
            LinhaCards(new[] { ("PRONTIDAO", p.nivelMilitar + "%", "state-good"), ("ARMAMENTOS", p.armamentos.ToString("N0"), "state-info"), ("URANIO", p.uranio.ToString("N0"), "state-warn"), ("PRESSAO DE GUERRA", g.PressaoGlobalGuerra().ToString("0") + "%", "state-bad") });
            AdicionarCard(conteudo, "PLANO DEFENSIVO ATUAL", $"Postura: {p.planoEstrategico}\nMilitares ativos: {p.populacaoMilitarAtiva:N0}\nReservistas: {p.reservistas:N0}\nAlistaveis: {p.alistaveis:N0}\nSituacao: {(p.emGuerra ? "EM GUERRA" : "PAZ")}");
            if (aresMunicao != null)
                AdicionarCard(conteudo, "MUNICAO ANTIAEREA", $"{aresMunicao.nome}\nValor por cartucho: {Moeda(aresMunicao.valorUnitario)}\nCarregador: {aresMunicao.capacidadeCartucho} cartuchos\nPausa de reabastecimento: {aresMunicao.tempoReabastecimento:0.0}s\nDisparos registrados: {aresMunicao.totalDisparado:N0}");
            TabelaCabecalho("NACAO", "STATUS", "NIVEL MILITAR", "RELACAO", "RISCO");
            foreach (DadosPaisGoverno outro in ObterNacoesAtivas(g))
            {
                if (outro.teamId == 1) continue; RelacaoPaisGoverno r = g.ObterRelacao(1, outro.teamId);
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
            LinhaCards(new[] { ("PORTOS", portos.ToString(), "state-info"), ("ARMAMENTOS", p.armamentos.ToString("N0"), "state-good"), ("PETROLEO", p.petroleo.ToString("N0"), "state-warn"), ("PRESSAO DE GUERRA", g.PressaoGlobalGuerra().ToString("0") + "%", "state-bad") });
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

            LinhaCards(new[] { ("ALERTAS", ObterNacoesAtivas(g).Count(x => x != null && x.emGuerra).ToString(), "state-bad"), ("ARMAMENTOS", p.armamentos.ToString("N0"), "state-info"), ("URANIO", p.uranio.ToString("N0"), "state-warn"), ("PRESSAO DE GUERRA", g.PressaoGlobalGuerra().ToString("0") + "%", "state-bad") });
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

                AdicionarCardCiencia(gradeProjetos,
                    receita.nivelIndustrialExigido.ToString(),
                    receita.nome.ToUpperInvariant(),
                    "Produto final: " + NomeIndustrial(receita.produtoFinalId),
                    detalhes,
                    estadoProjeto,
                    classeProjeto,
                    temFila ? "VER FILA" : "INICIAR LOTE",
                    () => CriarProjetoIndustrial(receita.id),
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
                permitirFabricacao && desbloqueada ? () => ProduzirMunicaoMilitar(municao.id, 10) : null,
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
            ok = SistemaGastosMilitares.Instancia.ProduzirMunicao(1, municaoId, quantidade, out mensagem);
        }
        MostrarMensagem(ok ? mensagem : "Fabricacao recusada: " + mensagem);
        AtualizarRecursos();
        MostrarPagina(abaAtual);
    }

    private void CriarProjetoIndustrial(string receitaId)
    {
        SistemaIndustrialNacional industrial = SistemaIndustrialNacional.Instancia;
        if (industrial == null)
        {
            MostrarMensagem("Sistema industrial indisponivel.");
            return;
        }

        int teamId = SistemaGovernoMundial.Instancia != null ? Mathf.Max(1, SistemaGovernoMundial.Instancia.teamJogador) : 1;
        OrdemRefinoIndustrial ordem = industrial.CriarOrdemRefino(teamId, receitaId);
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
        return ValoresDefinitivosHegemonia.FormatarDinheiro(valor);
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

