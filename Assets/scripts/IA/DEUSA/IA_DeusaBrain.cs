using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;
using UnityEngine;

namespace Hegemonia.AI.DEUSA
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IA_BrainMaster))]
    public sealed class IA_DeusaBrain : MonoBehaviour, IIAUpdateModule
    {
        [Header("DEUSA 1.0")]
        public bool ativarDEUSA = true;
        public IA_DeusaConfig config = new IA_DeusaConfig();
        public IA_DeusaIdentidadeNacional identidade = new IA_DeusaIdentidadeNacional();
        public IA_DeusaPoliticaNacional politicaNacional = new IA_DeusaPoliticaNacional();
        public List<IA_DeusaPrioridade> prioridadesAtuais = new List<IA_DeusaPrioridade>();

        [Header("Debug")]
        [TextArea(3, 10)] public string resumoDeusa = "DEUSA aguardando bind";
        [TextArea(2, 8)] public string resumoEconomia = string.Empty;
        [TextArea(2, 8)] public string resumoMilitar = string.Empty;
        [TextArea(2, 8)] public string resumoEspionagem = string.Empty;
        [TextArea(2, 8)] public string resumoMapa = string.Empty;
        [TextArea(2, 10)] public string resumoPrioridades = string.Empty;

        private readonly IA_DeusaPerformance _performance = new IA_DeusaPerformance();
        private readonly IA_DeusaEstagios _estagios = new IA_DeusaEstagios();
        private readonly IA_DeusaMapaMemoria _mapa = new IA_DeusaMapaMemoria();
        private readonly IA_DeusaGovernoBridge _governoBridge = new IA_DeusaGovernoBridge();
        private readonly IA_DeusaComida _comida = new IA_DeusaComida();
        private readonly IA_DeusaHabitacao _habitacao = new IA_DeusaHabitacao();
        private readonly IA_DeusaPopulacao _populacao = new IA_DeusaPopulacao();
        private readonly IA_DeusaEconomia _economia = new IA_DeusaEconomia();
        private readonly IA_DeusaConstrucao _construcao = new IA_DeusaConstrucao();
        private readonly IA_DeusaEspionagem _espionagem = new IA_DeusaEspionagem();
        private readonly IA_DeusaMercado _mercado = new IA_DeusaMercado();
        private readonly IA_DeusaDiplomacia _diplomacia = new IA_DeusaDiplomacia();
        private readonly IA_DeusaTerrestre _terrestre = new IA_DeusaTerrestre();
        private readonly IA_DeusaAerea _aerea = new IA_DeusaAerea();
        private readonly IA_DeusaMarinha _marinha = new IA_DeusaMarinha();
        private readonly IA_DeusaDefesa _defesa = new IA_DeusaDefesa();
        private readonly IA_DeusaLogistica _logistica = new IA_DeusaLogistica();
        private readonly IA_DeusaMilitar _militar = new IA_DeusaMilitar();
        private readonly IA_DeusaAlvos _alvos = new IA_DeusaAlvos();
        private readonly IA_DeusaDebugPanel _debugPanel = new IA_DeusaDebugPanel();

        private IA_BrainMaster _brain;
        private IA_Context _context;
        private IA_DeusaPoliticaEstagio _politica = new IA_DeusaPoliticaEstagio();
        private bool _runtimeReady;
        private bool _vantagemAplicada;
        private int _baseMaxCommandsPerFrame = 4;
        private bool _logInicialEmitido;
        private bool? _ultimoModoObservadorLogado;
        private DeusaEstagio _ultimoEstagioLogado = (DeusaEstagio)(-1);
        private float _proximoLogResumoTime;

        public string Name
        {
            get { return "IA_DeusaBrain"; }
        }

        public float Interval
        {
            get { return 0.25f; }
        }

        public float BudgetMs
        {
            get { return 0.22f; }
        }

        public DeusaEstagio EstagioAtual
        {
            get { return identidade != null ? identidade.estagioAtual : DeusaEstagio.Inicializacao; }
        }

        public IA_DeusaPoliticaNacional PoliticaNacional
        {
            get { return politicaNacional; }
        }

        public IList<IA_DeusaPrioridade> PrioridadesAtuais
        {
            get { return prioridadesAtuais; }
        }

        public bool ModoObservadorAtivo
        {
            get { return config != null && config.modoObservadorDebug; }
        }

        public bool BloquearFilaBrainMasterEmObservador
        {
            get { return ModoObservadorAtivo && config != null && config.bloquearFilaBrainMasterEmObservador; }
        }

        public string EscopoObservador
        {
            get
            {
                if (!ModoObservadorAtivo)
                {
                    return "desligado";
                }

                return BloquearFilaBrainMasterEmObservador
                    ? "DEUSA + fila do BrainMaster"
                    : "apenas DEUSA";
            }
        }

        public void BindRuntime(IA_BrainMaster brain, IA_Context context)
        {
            _brain = brain;
            _context = context;
            if (config == null)
            {
                config = new IA_DeusaConfig();
            }

            if (identidade == null)
            {
                identidade = new IA_DeusaIdentidadeNacional();
            }

            if (politicaNacional == null)
            {
                politicaNacional = new IA_DeusaPoliticaNacional();
            }

            if (prioridadesAtuais == null)
            {
                prioridadesAtuais = new List<IA_DeusaPrioridade>();
            }

            identidade.GarantirDefaults(_brain != null ? _brain.TeamId : 1, config.personalidade, config.modoInicial);
            _baseMaxCommandsPerFrame = _brain != null ? Mathf.Max(1, _brain.MaxCommandsPerFrame) : 4;
            _performance.Reset(Time.time + (_brain != null ? _brain.TeamId * 0.43f : Random.value));
            _runtimeReady = true;
            EmitirLogInicialSeNecessario(true);
        }

        public void Tick(float now, float deltaTime)
        {
            if (!ativarDEUSA || _brain == null || _context == null)
            {
                return;
            }

            if (!IA_SharedRuntimeSupport.IsBrainMasterMode)
            {
                return;
            }

            if (!_runtimeReady)
            {
                BindRuntime(_brain, _context);
            }

            identidade.GarantirDefaults(_brain.TeamId, config.personalidade, config.modoInicial);
            bool modoObservador = ModoObservadorAtivo;
            DadosPaisGoverno pais = _governoBridge.SincronizarNacao(identidade, config);
            if (pais == null)
            {
                return;
            }

            AplicarIdentidadeAoBrainMaster(pais);
            if (!modoObservador)
            {
                AplicarVantagemInicial(pais);
                _performance.AplicarBudget(_brain, _baseMaxCommandsPerFrame);
            }

            if (_performance.DeveRodarMapa(now))
            {
                _mapa.Atualizar(_context, now, true);
            }

            DadosEconomiaPais economiaPais = _governoBridge.ObterEconomia(identidade.teamID);
            IA_ForceSnapshot snapshot = _context.ForceSnapshot ?? (_context.WorldState != null ? _context.WorldState.ForceSnapshot : null);
            if (snapshot == null)
            {
                return;
            }

            if (_performance.DeveRodarEconomia(now))
            {
                _politica = _estagios.Avaliar(config, identidade, pais, economiaPais, snapshot, _mapa);
                _comida.Atualizar(pais, economiaPais, _politica, pais.emGuerra);
                _habitacao.Atualizar(pais, economiaPais);
                _populacao.Atualizar(pais, economiaPais, identidade.estagioAtual);
                _economia.Atualizar(pais, economiaPais, _comida, _habitacao, _politica, modoObservador ? null : _context.NationalDecisionState);
            }

            if (_performance.DeveRodarEstrategia(now))
            {
                _politica = _estagios.Avaliar(config, identidade, pais, economiaPais, snapshot, _mapa);
                IA_DeusaEspionagemSnapshot intel = _espionagem.Atualizar(_context, config, identidade.estagioAtual);
                _terrestre.Atualizar(snapshot, intel, identidade.estagioAtual);
                _aerea.Atualizar(snapshot, intel, _politica);
                _marinha.Atualizar(snapshot, identidade.estagioAtual, _mapa);
                _defesa.Atualizar(snapshot, identidade.estagioAtual, intel);
                _logistica.Atualizar(pais, snapshot, _marinha);
                if (!modoObservador)
                {
                    _alvos.Atualizar(identidade, config, _politica);
                    _militar.Atualizar(_brain, config, snapshot, intel, _terrestre, _aerea, _marinha, _defesa, identidade.estagioAtual);
                    _brain.PlayerFleetEstimate = intel.EstimativaNaval;
                    _brain.PlayerAircraftEstimate = intel.EstimativaAerea;
                }
            }

            if (_performance.DeveRodarConstrucao(now))
            {
                _construcao.Atualizar(snapshot, _mapa, _comida, _habitacao, _economia, _politica);
                if (!modoObservador)
                {
                    ExecutarConstrucoes(now, snapshot);
                    ExecutarProducao(now, snapshot);
                }
            }

            if (!modoObservador && _performance.DeveRodarDiplomacia(now))
            {
                _mercado.Atualizar(_governoBridge, config, pais, _comida, _economia, _logistica, now);
                _diplomacia.Atualizar(_governoBridge, config, pais, identidade.estagioAtual, now);
            }

            AtualizarPoliticaNacional(pais, economiaPais, snapshot);
            AtualizarPrioridades();
            AtualizarResumos();
            EmitirLogsDiagnosticos(now);
        }

        public void AplicarEstadoSalvo(
            int personalidade,
            int modoInicial,
            int estagioAtual,
            bool travarEstagio,
            bool modoObservadorDebug,
            bool bloquearFilaBrainMasterEmObservador,
            bool usarEspionagemJusta,
            bool permitirComercioComJogador,
            bool permitirComercioComOutrasIAs,
            bool permitirSancoes,
            bool permitirGuerraTotal,
            string nomePais,
            string nomePresidente,
            string nomeMoeda,
            string resumo)
        {
            if (config == null)
            {
                config = new IA_DeusaConfig();
            }

            if (identidade == null)
            {
                identidade = new IA_DeusaIdentidadeNacional();
            }

            config.personalidade = (DeusaPersonalidade)Mathf.Clamp(personalidade, 0, (int)DeusaPersonalidade.Aleatoria);
            config.modoInicial = (DeusaModoInicial)Mathf.Clamp(modoInicial, 0, (int)DeusaModoInicial.Manual);
            config.travarEstagio = travarEstagio;
            config.modoObservadorDebug = modoObservadorDebug;
            config.bloquearFilaBrainMasterEmObservador = bloquearFilaBrainMasterEmObservador;
            config.usarEspionagemJusta = usarEspionagemJusta;
            config.permitirComercioComJogador = permitirComercioComJogador;
            config.permitirComercioComOutrasIAs = permitirComercioComOutrasIAs;
            config.permitirSancoes = permitirSancoes;
            config.permitirGuerraTotal = permitirGuerraTotal;

            identidade.nomePais = nomePais;
            identidade.nomePresidente = nomePresidente;
            identidade.nomeMoeda = nomeMoeda;
            identidade.estagioAtual = (DeusaEstagio)Mathf.Clamp(estagioAtual, 0, (int)DeusaEstagio.GuerraTotal);
            identidade.GarantirDefaults(_brain != null ? _brain.TeamId : identidade.teamID, config.personalidade, config.modoInicial);
            politicaNacional.estagioAtual = identidade.estagioAtual;
            politicaNacional.modoObservador = config.modoObservadorDebug;

            resumoDeusa = string.IsNullOrWhiteSpace(resumo) ? resumoDeusa : resumo;
            _runtimeReady = false;
            _logInicialEmitido = false;
            _ultimoModoObservadorLogado = null;
            _ultimoEstagioLogado = (DeusaEstagio)(-1);
        }

        public string ResumoSalvavel()
        {
            return !string.IsNullOrWhiteSpace(resumoDeusa)
                ? resumoDeusa
                : (politicaNacional != null ? politicaNacional.ResumoCurto() : string.Empty);
        }

        private void Awake()
        {
            _brain = GetComponent<IA_BrainMaster>();
            if (config == null)
            {
                config = new IA_DeusaConfig();
            }

            if (identidade == null)
            {
                identidade = new IA_DeusaIdentidadeNacional();
            }

            if (politicaNacional == null)
            {
                politicaNacional = new IA_DeusaPoliticaNacional();
            }

            if (prioridadesAtuais == null)
            {
                prioridadesAtuais = new List<IA_DeusaPrioridade>();
            }
        }

        private void AplicarIdentidadeAoBrainMaster(DadosPaisGoverno pais)
        {
            _brain.NationName = identidade.nomePais;
            _brain.CurrencyName = identidade.nomeMoeda;
            _brain.NationProfile = IA_DeusaGovernoBridge.MapearPerfil(identidade.personalidade);
            _brain.InitialNationMode = IA_DeusaGovernoBridge.MapearModoInicial(config.modoInicial);
            AplicarPesosPorPersonalidade(_brain, identidade.personalidade);

            pais.perfilIA = _brain.NationProfile;
            pais.modoInicialIA = _brain.InitialNationMode;
        }

        private void AtualizarPoliticaNacional(DadosPaisGoverno pais, DadosEconomiaPais economiaPais, IA_ForceSnapshot snapshot)
        {
            if (politicaNacional == null)
            {
                politicaNacional = new IA_DeusaPoliticaNacional();
            }

            politicaNacional.estagioAtual = identidade != null ? identidade.estagioAtual : DeusaEstagio.Inicializacao;
            politicaNacional.modoObservador = ModoObservadorAtivo;
            politicaNacional.focoEconomia = Mathf.Clamp01(0.25f
                + (_politica != null && _politica.PriorizarComida ? 0.20f : 0f)
                + (_politica != null && _politica.PriorizarMoradia ? 0.15f : 0f)
                + (_politica != null && _politica.PriorizarEnergia ? 0.20f : 0f)
                + (_economia != null && _economia.PrecisaIndustria ? 0.10f : 0f));
            politicaNacional.focoMilitar = Mathf.Clamp01(0.15f
                + (_politica != null && _politica.PriorizarDefesa ? 0.25f : 0f)
                + (_politica != null && (_politica.PriorizarNaval || _politica.PriorizarAereo) ? 0.15f : 0f)
                + (snapshot != null && snapshot.VisibleEnemies > 0 ? 0.25f : 0f));
            politicaNacional.focoExpansao = Mathf.Clamp01(_politica != null && _politica.PriorizarExpansao ? 0.75f : 0.20f);
            politicaNacional.focoDiplomacia = Mathf.Clamp01(config != null && config.modoInicial == DeusaModoInicial.Paz
                ? 0.85f
                : politicaNacional.estagioAtual >= DeusaEstagio.TensaoGeopolitica ? 0.25f : 0.50f);
            politicaNacional.focoDefesa = Mathf.Clamp01(0.25f
                + (_politica != null && _politica.PriorizarDefesa ? 0.30f : 0f)
                + (_defesa != null && (_defesa.PrecisaRadar || _defesa.PrecisaCiws) ? 0.20f : 0f)
                + (snapshot != null && snapshot.VisibleEnemies > 0 ? 0.20f : 0f));
            politicaNacional.focoAtaque = Mathf.Clamp01(_politica != null && _politica.PriorizarGuerraTotal
                ? 0.90f
                : politicaNacional.estagioAtual >= DeusaEstagio.TensaoGeopolitica ? 0.35f : 0.10f);
            politicaNacional.permitirGuerra = !ModoObservadorAtivo
                && config != null
                && config.permitirGuerraTotal
                && politicaNacional.estagioAtual >= DeusaEstagio.TensaoGeopolitica;
            politicaNacional.permitirSancoes = !ModoObservadorAtivo
                && config != null
                && config.permitirSancoes
                && politicaNacional.estagioAtual >= DeusaEstagio.TensaoGeopolitica;
            politicaNacional.permitirComercio = !ModoObservadorAtivo
                && config != null
                && (config.permitirComercioComJogador || config.permitirComercioComOutrasIAs);
            politicaNacional.permitirExpansao = !ModoObservadorAtivo
                && _politica != null
                && _politica.PriorizarExpansao;
            politicaNacional.metaMinimaSoldados = _terrestre != null ? _terrestre.MetaInfantaria : 0;
            politicaNacional.metaMinimaTanques = _terrestre != null ? _terrestre.MetaTanques : 0;
            politicaNacional.metaMinimaAvioes = _aerea != null ? _aerea.MetaAeronaves : 0;
            politicaNacional.metaMinimaNavios = _marinha != null ? _marinha.MetaNavios : 0;
            politicaNacional.alvoPrioritario = ResolverAlvoPrioritario();
            politicaNacional.proximaConstrucao = ResolverProximaConstrucaoDesejada();
            politicaNacional.origemDaDecisao = _politica != null
                ? _politica.Motivo
                : (_economia != null ? _economia.PlanoEconomico : "aguardando");

            if (pais != null && economiaPais != null && string.IsNullOrWhiteSpace(politicaNacional.proximaConstrucao))
            {
                politicaNacional.proximaConstrucao = economiaPais.deficitEnergia > 0.5f ? "Energia" : "Nenhuma";
            }
        }

        private void AtualizarPrioridades()
        {
            if (prioridadesAtuais == null)
            {
                prioridadesAtuais = new List<IA_DeusaPrioridade>();
            }

            prioridadesAtuais.Clear();
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaHQ, DeusaTipoPrioridade.ConstruirHQ, 100, "Garantir prefeitura/HQ operacional.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaEnergia, DeusaTipoPrioridade.ConstruirEnergia, 99, "Deficit energetico bloqueando crescimento.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaFazenda, DeusaTipoPrioridade.ConstruirFarm, 97, "Comida em risco ou reserva abaixo do ideal.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaCasas, DeusaTipoPrioridade.ConstruirCasa, 96, "Pressao populacional pede novas casas.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaQuartel, DeusaTipoPrioridade.ConstruirQuartel, 92, "Abrir quartel para defesa e espionagem.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaIndustria, DeusaTipoPrioridade.ConstruirIndustria, 90, "Base industrial ainda insuficiente.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaRadar, DeusaTipoPrioridade.ConstruirRadar, 88, "Cobertura de radar e defesa critica.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaAeroporto, DeusaTipoPrioridade.ConstruirAeroporto, 86, "Abrir projeção aerea em terra livre.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaEstaleiro, DeusaTipoPrioridade.ConstruirEstaleiro, 85, "Abrir producao naval perto da costa.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaPier, DeusaTipoPrioridade.ConstruirPier, 84, "Criar apoio logistico naval.");
            AdicionarPrioridade(_construcao != null && _construcao.PrecisaPlataforma, DeusaTipoPrioridade.ConstruirPlataforma, 83, "Explorar petroleo offshore.");
            AdicionarPrioridade(_logistica != null && _logistica.PrecisaPetroleiro, DeusaTipoPrioridade.CriarPetroleiro, 80, "Manter cadeia de combustivel abastecida.");
            AdicionarPrioridade(_marinha != null && _marinha.PrecisaNavio, DeusaTipoPrioridade.CriarEscoltaNaval, 78, "Escolta naval abaixo da meta.");
            AdicionarPrioridade(_aerea != null && _aerea.PrecisaAviacao, DeusaTipoPrioridade.CriarEsquadraoAereo, 76, "Formar esquadrao minimo antes de atacar.");
            AdicionarPrioridade(_terrestre != null && _terrestre.PrecisaInfantaria, DeusaTipoPrioridade.CriarInfantaria, 74, "Meta minima de infantaria nao atendida.");
            AdicionarPrioridade(_terrestre != null && _terrestre.PrecisaTanques, DeusaTipoPrioridade.CriarTanques, 73, "Blindados abaixo da meta.");
            AdicionarPrioridade(_politica != null && _politica.PriorizarEspionagem, DeusaTipoPrioridade.EspionarJogador, 67, "Inteligencia militar precisa melhorar.");
            AdicionarPrioridade(_logistica != null && _logistica.PrecisaTransporte, DeusaTipoPrioridade.PrepararDesembarque, 62, "Mobilidade anfibia ainda incompleta.");
            AdicionarPrioridade(politicaNacional != null && politicaNacional.focoDefesa >= 0.65f, DeusaTipoPrioridade.DefenderHQ, 61, "Postura defensiva reforcada ao redor do nucleo nacional.");

            if (politicaNacional != null && politicaNacional.estagioAtual >= DeusaEstagio.TensaoGeopolitica)
            {
                string alvo = politicaNacional.alvoPrioritario ?? string.Empty;
                AdicionarPrioridade(alvo.Contains("Radar"), DeusaTipoPrioridade.AtacarRadar, 60, "Abrir guerra derrubando vigilancia inimiga.");
                AdicionarPrioridade(alvo.Contains("Energia"), DeusaTipoPrioridade.AtacarEnergia, 59, "Enfraquecer a infraestrutura inimiga.");
                AdicionarPrioridade(alvo.Contains("Petroleo"), DeusaTipoPrioridade.AtacarPetroleo, 58, "Cortar combustivel e petroleo adversario.");
            }

            if (prioridadesAtuais.Count == 0)
            {
                prioridadesAtuais.Add(new IA_DeusaPrioridade(DeusaTipoPrioridade.MonitorarSituacao, 10, "Modo observador ou situacao estavel: apenas monitorar."));
            }

            prioridadesAtuais.Sort(CompararPrioridades);
        }

        private void AdicionarPrioridade(bool condicao, DeusaTipoPrioridade tipo, int peso, string detalhe)
        {
            if (!condicao || prioridadesAtuais == null)
            {
                return;
            }

            prioridadesAtuais.Add(new IA_DeusaPrioridade(tipo, peso, detalhe));
        }

        private void AplicarVantagemInicial(DadosPaisGoverno pais)
        {
            if (_vantagemAplicada || config == null || config.vantagemInicial <= 0)
            {
                return;
            }

            int bonus = Mathf.Max(0, config.vantagemInicial) * 40;
            _brain.Credits += bonus;
            pais.saldo += bonus;
            _vantagemAplicada = true;
        }

        private static void AplicarPesosPorPersonalidade(IA_BrainMaster brain, DeusaPersonalidade personalidade)
        {
            switch (personalidade)
            {
                case DeusaPersonalidade.Militarista:
                    brain.DiplomacyWeight = 0.18f;
                    brain.TradeWeight = 0.35f;
                    brain.IndustryWeight = 0.55f;
                    brain.MilitarismWeight = 0.86f;
                    brain.AggressionWeight = 0.78f;
                    break;
                case DeusaPersonalidade.Economica:
                    brain.DiplomacyWeight = 0.58f;
                    brain.TradeWeight = 0.84f;
                    brain.IndustryWeight = 0.80f;
                    brain.MilitarismWeight = 0.32f;
                    brain.AggressionWeight = 0.25f;
                    break;
                case DeusaPersonalidade.Naval:
                    brain.DiplomacyWeight = 0.40f;
                    brain.TradeWeight = 0.50f;
                    brain.IndustryWeight = 0.62f;
                    brain.MilitarismWeight = 0.72f;
                    brain.AggressionWeight = 0.56f;
                    break;
                case DeusaPersonalidade.Aerea:
                    brain.DiplomacyWeight = 0.35f;
                    brain.TradeWeight = 0.48f;
                    brain.IndustryWeight = 0.65f;
                    brain.MilitarismWeight = 0.74f;
                    brain.AggressionWeight = 0.60f;
                    break;
                case DeusaPersonalidade.Defensiva:
                    brain.DiplomacyWeight = 0.50f;
                    brain.TradeWeight = 0.45f;
                    brain.IndustryWeight = 0.52f;
                    brain.MilitarismWeight = 0.58f;
                    brain.AggressionWeight = 0.22f;
                    break;
                case DeusaPersonalidade.Diplomatica:
                    brain.DiplomacyWeight = 0.88f;
                    brain.TradeWeight = 0.76f;
                    brain.IndustryWeight = 0.48f;
                    brain.MilitarismWeight = 0.26f;
                    brain.AggressionWeight = 0.18f;
                    break;
                case DeusaPersonalidade.Expansionista:
                    brain.DiplomacyWeight = 0.32f;
                    brain.TradeWeight = 0.50f;
                    brain.IndustryWeight = 0.60f;
                    brain.MilitarismWeight = 0.74f;
                    brain.AggressionWeight = 0.82f;
                    break;
                default:
                    brain.DiplomacyWeight = 0.50f;
                    brain.TradeWeight = 0.55f;
                    brain.IndustryWeight = 0.50f;
                    brain.MilitarismWeight = 0.45f;
                    brain.AggressionWeight = 0.35f;
                    break;
            }
        }

        private void ExecutarConstrucoes(float now, IA_ForceSnapshot snapshot)
        {
            if (_context == null || _context.CommandQueue == null || _context.CommandQueue.PendingCount > 8)
            {
                return;
            }

            if (_construcao.PrecisaHQ && TentarEnfileirarConstrucao("deusa_hq", 99, 12f, IA_ZoneType.Core, "prefeitura", "hq", "governo"))
            {
                return;
            }

            if (_construcao.PrecisaEnergia && TentarEnfileirarConstrucao("deusa_energia", 98, 10f, IA_ZoneType.Economy, "energia", "usina", "solar", "gerador"))
            {
                return;
            }

            if (_construcao.PrecisaFazenda && TentarEnfileirarConstrucao("deusa_farm", 97, 10f, IA_ZoneType.Economy, "fazenda", "farm"))
            {
                return;
            }

            if (_construcao.PrecisaCasas && TentarEnfileirarConstrucao("deusa_casas", 96, 10f, IA_ZoneType.Core, "casa", "imovel", "predio"))
            {
                return;
            }

            if (_construcao.PrecisaQuartel && TentarEnfileirarConstrucao("deusa_quartel", 95, 14f, IA_ZoneType.Military, "quartel", "tenda militar", "barracks"))
            {
                return;
            }

            if (_construcao.PrecisaIndustria && TentarEnfileirarConstrucao("deusa_fabrica", 94, 15f, IA_ZoneType.Military, "fabrica", "construtor de veiculos", "factory"))
            {
                return;
            }

            if (_construcao.PrecisaRadar && TentarEnfileirarConstrucao("deusa_radar", 93, 18f, IA_ZoneType.Defense, "radar", "torre de radar"))
            {
                return;
            }

            if (_construcao.PrecisaAeroporto && TentarEnfileirarConstrucao("deusa_aeroporto", 92, 20f, IA_ZoneType.Air, "aeroporto militar", "base aerea militar", "military airport"))
            {
                return;
            }

            if (_construcao.PrecisaEstaleiro && TentarEnfileirarConstrucao("deusa_estaleiro", 91, 22f, IA_ZoneType.Naval, "estaleiro", "estaleiro naval"))
            {
                return;
            }

            if (_construcao.PrecisaPier && TentarEnfileirarConstrucao("deusa_pier", 90, 22f, IA_ZoneType.Naval, "pier", "pier naval"))
            {
                return;
            }

            if (_construcao.PrecisaPlataforma)
            {
                TentarEnfileirarConstrucao("deusa_plataforma", 89, 24f, IA_ZoneType.Naval, "plataforma", "plataforma offshore");
            }
        }

        private void ExecutarProducao(float now, IA_ForceSnapshot snapshot)
        {
            if (_context == null || _context.CommandQueue == null || _context.CommandQueue.PendingCount > 10)
            {
                return;
            }

            if (_terrestre.PrecisaInfantaria && _populacao.PodeRecrutarMilitar && snapshot.HasBarracks && TentarEnfileirarProducao("deusa_infantaria", 88, 6f, "soldado rifle", "soldado", "infantaria", "tropa navy"))
            {
                return;
            }

            if (_terrestre.PrecisaTanques && snapshot.HasFactory && TentarEnfileirarProducao("deusa_tanque", 87, 7f, "tanque", "arthur", "king", "leon"))
            {
                return;
            }

            if (_aerea.PrecisaAviacao && snapshot.HasMilitaryAirport && TentarEnfileirarProducao("deusa_aviao", 86, 7f, "b260", "fa1", "caca", "aviao"))
            {
                return;
            }

            if (_marinha.PrecisaNavio && snapshot.HasNavalBase && TentarEnfileirarProducao("deusa_navio", 85, 8f, "corveta", "destroyer", "ironclad", "navio"))
            {
                return;
            }

            if (_logistica.PrecisaPetroleiro && snapshot.HasNavalBase && TentarEnfileirarProducao("deusa_petroleiro", 84, 8f, "petroleiro", "navio petroleiro", "oil tanker"))
            {
                return;
            }

            if (_logistica.PrecisaTransporte && snapshot.HasNavalBase)
            {
                TentarEnfileirarProducao("deusa_transporte", 83, 8f, "hovercraft", "transporte", "navio de transporte");
            }
        }

        private bool TentarEnfileirarConstrucao(string dedup, int priority, float cooldown, IA_ZoneType zone, params string[] keys)
        {
            if (_context == null || _context.Backend == null || _context.CommandQueue == null)
            {
                return false;
            }

            DadosConstrucao data = _context.Backend.FindFirstAvailable(keys);
            if (data == null)
            {
                return false;
            }

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            IA_LotCandidate lot;
            string reason;
            if (_context.ConstructionPlanner != null && _context.ConstructionPlanner.TryPlanBuild(data.nomeItem, zone, out lot, out reason) && lot != null)
            {
                position = lot.Position;
                rotation = lot.Rotation;
            }
            else
            {
                position = ResolveFallbackBuildPoint(zone);
            }

            if (position == Vector3.zero)
            {
                return false;
            }

            IA_BuildOrderData payload = new IA_BuildOrderData
            {
                ItemKey = data.nomeItem,
                Position = position,
                Rotation = rotation,
                Zone = zone
            };

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Build,
                "IA_DeusaBrain",
                "build",
                "planejamento observador",
                priority,
                "build",
                dedup,
                cooldown,
                payload);

            string enqueueReason;
            return _context.CommandQueue.Enqueue(request, Time.time, out enqueueReason);
        }

        private bool TentarEnfileirarProducao(string dedup, int priority, float cooldown, params string[] keys)
        {
            if (_context == null || _context.Backend == null || _context.CommandQueue == null)
            {
                return false;
            }

            DadosConstrucao data = _context.Backend.FindFirstAvailable(keys);
            if (data == null)
            {
                return false;
            }

            IA_ProduceOrderData payload = new IA_ProduceOrderData
            {
                ItemKey = data.nomeItem,
                Quantity = 1
            };

            IA_CommandRequest request = IA_CommandFactory.Create(
                IA_CommandType.Produce,
                "IA_DeusaBrain",
                "production",
                "planejamento observador",
                priority,
                "production",
                dedup,
                cooldown,
                payload);

            string enqueueReason;
            return _context.CommandQueue.Enqueue(request, Time.time, out enqueueReason);
        }

        private Vector3 ResolveFallbackBuildPoint(IA_ZoneType zone)
        {
            switch (zone)
            {
                case IA_ZoneType.Air:
                    return _mapa.AncoraAerea;
                case IA_ZoneType.Naval:
                    return _mapa.AncoraNaval != Vector3.zero ? _mapa.AncoraNaval : _mapa.AncoraCosta;
                case IA_ZoneType.Defense:
                    return _mapa.AncoraTerraSegura;
                default:
                    return _mapa.AncoraTerraSegura != Vector3.zero ? _mapa.AncoraTerraSegura : _mapa.AncoraExpansao;
            }
        }

        private void AtualizarResumos()
        {
            resumoEconomia = _economia.UltimoResumo + "\n" + _comida.UltimoResumo + "\n" + _habitacao.UltimoResumo + "\n" + _mercado.UltimoResumo;
            resumoMilitar = _militar.UltimoResumo + "\n" + _terrestre.UltimoResumo + "\n" + _aerea.UltimoResumo + "\n" + _marinha.UltimoResumo + "\n" + _defesa.UltimoResumo;
            resumoEspionagem = _espionagem.UltimoResumo + "\n" + _diplomacia.UltimoResumo;
            resumoMapa = _mapa.UltimoResumo + "\n" + _construcao.UltimoResumo + "\n" + _performance.UltimoResumo;
            resumoPrioridades = ConstruirResumoPrioridades();

            _debugPanel.Atualizar(
                identidade,
                config,
                politicaNacional,
                prioridadesAtuais,
                resumoEconomia,
                resumoMilitar,
                resumoEspionagem,
                resumoMapa);

            resumoDeusa = _debugPanel.statusGeral
                          + "\nEstagio: " + identidade.estagioAtual + " (" + _politica.Motivo + ")"
                          + "\nObservador: " + EscopoObservador
                          + "\nPoliticaEstagio: " + _politica.Resumo()
                          + "\nPrioridades:\n" + resumoPrioridades
                          + "\nAlvos: " + _alvos.UltimoResumo
                          + "\nLogistica: " + _logistica.UltimoResumo;
        }

        private void EmitirLogInicialSeNecessario(bool forcar)
        {
            if (!forcar && _logInicialEmitido)
            {
                return;
            }

            if (_brain == null || identidade == null || config == null)
            {
                return;
            }

            Debug.Log(
                "[DEUSA][Team " + identidade.teamID + "] Runtime vinculado | pais=" + identidade.nomePais
                + " | presidente=" + identidade.nomePresidente
                + " | moeda=" + identidade.nomeMoeda
                + " | modo=" + config.modoInicial
                + " | personalidade=" + identidade.personalidade
                + " | observador=" + ModoObservadorAtivo
                + " | escopo=" + EscopoObservador,
                this);
            IA_RuntimeTextTrace.LogText(identidade.teamID, "DEUSA", "BIND", "Runtime vinculado | pais=" + identidade.nomePais + " | presidente=" + identidade.nomePresidente + " | moeda=" + identidade.nomeMoeda + " | modo=" + config.modoInicial + " | personalidade=" + identidade.personalidade + " | observador=" + ModoObservadorAtivo + " | escopo=" + EscopoObservador);

            _logInicialEmitido = true;
            _ultimoModoObservadorLogado = ModoObservadorAtivo;
            _ultimoEstagioLogado = identidade.estagioAtual;
            _proximoLogResumoTime = Time.unscaledTime + 20f;
        }

        private void EmitirLogsDiagnosticos(float now)
        {
            EmitirLogInicialSeNecessario(false);

            if (_brain == null || identidade == null || config == null)
            {
                return;
            }

            bool modoObservador = ModoObservadorAtivo;
            if (!_ultimoModoObservadorLogado.HasValue || _ultimoModoObservadorLogado.Value != modoObservador)
            {
                Debug.Log(
                    "[DEUSA][Team " + identidade.teamID + "] modoObservadorDebug=" + modoObservador
                    + " | escopo=" + EscopoObservador
                    + (modoObservador
                        ? (BloquearFilaBrainMasterEmObservador
                            ? " | a fila do BrainMaster sera bloqueada."
                            : " | o BrainMaster legado continua autorizado a agir.")
                        : " | execucao DEUSA liberada."),
                    this);
                IA_RuntimeTextTrace.LogText(identidade.teamID, "DEUSA", "OBSERVADOR", "modoObservadorDebug=" + modoObservador + " | escopo=" + EscopoObservador + (modoObservador ? (BloquearFilaBrainMasterEmObservador ? " | a fila do BrainMaster sera bloqueada." : " | o BrainMaster legado continua autorizado a agir.") : " | execucao DEUSA liberada."));
                _ultimoModoObservadorLogado = modoObservador;
            }

            if (_ultimoEstagioLogado != identidade.estagioAtual)
            {
                Debug.Log(
                    "[DEUSA][Team " + identidade.teamID + "] estagio -> " + identidade.estagioAtual
                    + " | motivo=" + (_politica != null ? _politica.Motivo : "n/d")
                    + " | politica=" + (politicaNacional != null ? politicaNacional.ResumoCurto() : "n/d"),
                    this);
                IA_RuntimeTextTrace.LogText(identidade.teamID, "DEUSA", "ESTAGIO", "estagio -> " + identidade.estagioAtual + " | motivo=" + (_politica != null ? _politica.Motivo : "n/d") + " | politica=" + (politicaNacional != null ? politicaNacional.ResumoCurto() : "n/d"));
                _ultimoEstagioLogado = identidade.estagioAtual;
            }

            if (Time.unscaledTime >= _proximoLogResumoTime)
            {
                Debug.Log(
                    "[DEUSA][Team " + identidade.teamID + "] resumo | observador=" + EscopoObservador
                    + " | proximaPrioridade=" + (prioridadesAtuais != null && prioridadesAtuais.Count > 0 ? prioridadesAtuais[0].ToString() : "Nenhuma")
                    + " | proximaConstrucao=" + (politicaNacional != null ? politicaNacional.proximaConstrucao : "Nenhuma")
                    + " | alvo=" + (politicaNacional != null ? politicaNacional.alvoPrioritario : "Nenhum"),
                    this);
                IA_RuntimeTextTrace.LogText(identidade.teamID, "DEUSA", "RESUMO", "observador=" + EscopoObservador + " | proximaPrioridade=" + (prioridadesAtuais != null && prioridadesAtuais.Count > 0 ? prioridadesAtuais[0].ToString() : "Nenhuma") + " | proximaConstrucao=" + (politicaNacional != null ? politicaNacional.proximaConstrucao : "Nenhuma") + " | alvo=" + (politicaNacional != null ? politicaNacional.alvoPrioritario : "Nenhum"));
                _proximoLogResumoTime = Time.unscaledTime + 20f;
            }
        }

        private string ResolverProximaConstrucaoDesejada()
        {
            if (_construcao == null)
            {
                return "Nenhuma";
            }

            if (_construcao.PrecisaHQ)
            {
                return "HQ/Prefeitura";
            }

            if (_construcao.PrecisaEnergia)
            {
                return "Energia";
            }

            if (_construcao.PrecisaFazenda)
            {
                return "Farm";
            }

            if (_construcao.PrecisaCasas)
            {
                return "Casas";
            }

            if (_construcao.PrecisaQuartel)
            {
                return "Quartel";
            }

            if (_construcao.PrecisaIndustria)
            {
                return "Industria";
            }

            if (_construcao.PrecisaRadar)
            {
                return "Radar";
            }

            if (_construcao.PrecisaAeroporto)
            {
                return "Aeroporto Militar";
            }

            if (_construcao.PrecisaEstaleiro)
            {
                return "Estaleiro";
            }

            if (_construcao.PrecisaPier)
            {
                return "Pier";
            }

            if (_construcao.PrecisaPlataforma)
            {
                return "Plataforma Offshore";
            }

            return "Nenhuma";
        }

        private string ResolverAlvoPrioritario()
        {
            if (identidade == null || identidade.estagioAtual < DeusaEstagio.TensaoGeopolitica)
            {
                return "Monitorar fronteira";
            }

            IA_DeusaEspionagemSnapshot intel = _espionagem != null ? _espionagem.UltimoSnapshot : null;
            if (intel != null && !intel.ConheceRadar)
            {
                return "Radar";
            }

            if (intel != null && intel.EstimativaEnergia > 0)
            {
                return "Energia";
            }

            if (intel != null && intel.EstimativaPetroleo > 0)
            {
                return "Petroleo";
            }

            if (intel != null && intel.ConheceAeroporto)
            {
                return "Aeroporto";
            }

            if (intel != null && intel.ConheceEstaleiro)
            {
                return "Estaleiro";
            }

            return identidade.estagioAtual >= DeusaEstagio.GuerraTotal ? "Defesas antes do HQ" : "Radar";
        }

        private string ConstruirResumoPrioridades()
        {
            if (prioridadesAtuais == null || prioridadesAtuais.Count == 0)
            {
                return "Nenhuma";
            }

            int limite = Mathf.Min(5, prioridadesAtuais.Count);
            string resumo = string.Empty;
            for (int i = 0; i < limite; i++)
            {
                if (i > 0)
                {
                    resumo += "\n";
                }

                resumo += (i + 1) + ". " + prioridadesAtuais[i];
            }

            return resumo;
        }

        private static int CompararPrioridades(IA_DeusaPrioridade a, IA_DeusaPrioridade b)
        {
            int pesoA = a != null ? a.peso : 0;
            int pesoB = b != null ? b.peso : 0;
            return pesoB.CompareTo(pesoA);
        }
    }
}
