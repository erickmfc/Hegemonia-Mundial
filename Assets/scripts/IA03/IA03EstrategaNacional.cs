using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    /// <summary>
    /// Decide a postura nacional em intervalos longos e envia missões usando a
    /// fila e os executores já pertencentes ao BrainMaster.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IA_BrainMaster))]
    public sealed class IA03EstrategaNacional : MonoBehaviour, IIAUpdateModule
    {
        private enum PapelUnidadeGrupo { PortaAvioes, Escolta, Aeronave, Submarino, Transporte, Terrestre, Presidente }

        private struct CandidatoGrupo
        {
            public GameObject Unidade;
            public bool PortaAvioes;
            public bool Escolta;
            public bool Aeronave;
            public bool Submarino;
            public bool Transporte;
            public bool Terrestre;
            public bool Presidente;
        }

        [Header("IA03 — Estratega Nacional")]
        [SerializeField] private bool ativo = true;
        [SerializeField] private PerfilPaisSO perfilPais;
        [SerializeField, Min(0)] private int paisAlvoTeamId;
        [SerializeField] private List<MissaoEstrategicaSO> missoesEstrategicas = new List<MissaoEstrategicaSO>();

        [Header("Relatório")]
        [SerializeField, Min(1f)] private float intervaloRelatorioSegundos = 300f;

        [Header("Debug")]
        [SerializeField] private bool debugIA03;

        [Header("Diagnóstico")]
        [SerializeField] private IA03NivelConflito nivelDeConflito = IA03NivelConflito.Paz;
        [SerializeField] private IA03EstadoNacional estadoNacional = IA03EstadoNacional.Paz;
        [SerializeField] private string ultimaDecisao = "aguardando perfil nacional";
        [SerializeField] private string ultimaMissao = string.Empty;

        private readonly IA03RelatorioConflito relatorio = new IA03RelatorioConflito();
        private readonly GestorDiplomaciaIA03 diplomacia = new GestorDiplomaciaIA03();
        private readonly GestorEconomiaIA03 economia = new GestorEconomiaIA03();
        private readonly List<CreatyEstrategico> candidatos = new List<CreatyEstrategico>(32);
        private readonly List<IA03TipoCreaty> tiposCreatyCandidatos = new List<IA03TipoCreaty>(8);
        private readonly List<GameObject> unidadesCandidatas = new List<GameObject>(48);
        private readonly List<GameObject> unidadesAtivasNaMissao = new List<GameObject>(32);
        private readonly List<CandidatoGrupo> candidatosGrupo = new List<CandidatoGrupo>(128);
        private readonly List<MissaoEstrategicaSO> filaMissoes = new List<MissaoEstrategicaSO>(32);
        private readonly HashSet<MissaoEstrategicaSO> missoesConcluidasNaCrise = new HashSet<MissaoEstrategicaSO>();
        private readonly HashSet<string> chavesMissoesConcluidasNaCrise = new HashSet<string>();
        private readonly HashSet<int> unidadesComOrdemConcluidaNaMissao = new HashSet<int>();
        private readonly HashSet<int> unidadesPerdidasNaMissao = new HashSet<int>();
        private GestorEconomiaIA03.Resumo economiaAtual;

        private IA_BrainMaster brain;
        private SistemaGovernoMundial governoAssinado;
        private GerenteDeTerritorio gerenteTerritorialAssinado;
        private MissaoEstrategicaSO missaoAtiva;
        private CreatyEstrategico creatyAtivo;
        private int equipeAlvoAtiva;
        private int unidadesReservadas;
        private int unidadesOriginaisNaMissao;
        private float inicioPermanenciaNoDestinoEm = -1f;
        private bool grupoChegouAoDestino;
        private bool grupoSaiuDoDestino;
        private bool grupoPerdeuUnidade;
        private bool alvoMissaoFoiDefinido;
        private bool alvoMissaoDestruido;
        private bool territorioDoObjetivoCapturado;
        private bool territorioDoObjetivoPerdido;
        private bool confirmacaoExternaDeSucesso;
        private bool confirmacaoExternaDeFracasso;
        private string motivoConfirmacaoExterna = string.Empty;
        private string idPersistenteAlvoMissao = string.Empty;
        private Transform alvoMissaoAtivo;
        private float inicioConflitoEm = -1f;
        private float inicioGuerraEm = -1f;
        private float inicioMissaoEm = -1f;
        private float proximoRelatorioEm;
        private float proximaDecisaoDeMissaoEm;
        private float proximaReanaliseAntecipadaEm;
        private long saldoAlvoNoInicio;
        private bool avaliouConflitoLimitado;
        private bool avaliouGuerraTotal;
        private bool perfilAplicado;
        private bool guerraObservada;
        private float ignorarTensaoAte;
        private float ultimaAnaliseEm = -1f;
        private string idOrdemAtivaDaMissao = string.Empty;
        private IA03ResultadoMissao estadoDaMissao = IA03ResultadoMissao.Pendente;
        private string ultimaLinhaDeDebug = string.Empty;
#if UNITY_EDITOR
        private bool nivelDebugForcado;
#endif

        public string Name => "IA03EstrategaNacional";
        public float Interval => missaoAtiva != null ? 5f : perfilPais != null ? Mathf.Max(5f, perfilPais.IntervaloDecisaoSegundos) : 60f;
        public float DelayInicialEscalonado => CalcularAtrasoInicialEscalonado(brain != null ? brain.TeamId : 0);
        public float BudgetMs => 0.35f;
        public bool TemPerfilConfigurado => ativo && perfilPais != null;
        public IA03NivelConflito NivelDeConflito => nivelDeConflito;
        public IA03EstadoNacional EstadoNacional => estadoNacional;
        public string UltimaDecisao => ultimaDecisao;
        public string UltimaMissao => ultimaMissao;
        public IA03ResultadoMissao EstadoDaMissao => estadoDaMissao;
        public float UltimaAnaliseEm => ultimaAnaliseEm;
        public float ProximaAnalisePrevistaEm => ultimaAnaliseEm < 0f ? Time.time + DelayInicialEscalonado : ultimaAnaliseEm + Interval;
        public bool DebugHabilitado => debugIA03;
        public IA03RelatorioSnapshot RelatorioAtual => relatorio.Acumulado;

        public static float CalcularAtrasoInicialEscalonado(int teamId)
        {
            int indice = Mathf.Max(0, teamId - 1) % 15;
            return indice * 0.5f;
        }

        public static int CalcularLimiteDeMobilizacao(
            int totalDeCombate,
            int capacidadeCreatyDisponivel,
            IA03NivelConflito nivel,
            float reservaDefesa,
            float contingenteMaximo)
        {
            float reserva = Mathf.Clamp(reservaDefesa, 0f, 0.49f);
            float teto = Mathf.Clamp(contingenteMaximo, 0f, 0.9f);
            float proporcao = nivel == IA03NivelConflito.GuerraTotal
                ? Mathf.Min(teto, 1f - reserva)
                : nivel == IA03NivelConflito.ConflitoLimitado ? 0.5f
                : nivel == IA03NivelConflito.AvancoMilitar ? 0.25f
                : nivel == IA03NivelConflito.Tensao ? 0.25f
                : 0f;
            int limite = Mathf.Min(
                Mathf.Max(0, capacidadeCreatyDisponivel),
                Mathf.FloorToInt(Mathf.Max(0, totalDeCombate) * Mathf.Clamp01(proporcao)));
            if (limite == 0 && totalDeCombate > 0 && capacidadeCreatyDisponivel > 0
                && nivel != IA03NivelConflito.GuerraTotal
                && nivel != IA03NivelConflito.Paz)
            {
                return 1;
            }

            return limite;
        }

        public static int CalcularLimitePorMissaoN1(
            int totalDeCombate,
            int capacidadeCreatyDisponivel,
            IA03NivelConflito nivel,
            float reservaDefesa,
            float contingenteMaximo,
            float contingenteMaximoPorMissao)
        {
            int limiteDeMobilizacao = CalcularLimiteDeMobilizacao(
                totalDeCombate,
                capacidadeCreatyDisponivel,
                nivel,
                reservaDefesa,
                contingenteMaximo);
            if (nivel != IA03NivelConflito.GuerraTotal || limiteDeMobilizacao <= 0)
            {
                return limiteDeMobilizacao;
            }

            float proporcaoPorMissao = Mathf.Clamp(contingenteMaximoPorMissao, 0f, 0.9f);
            if (proporcaoPorMissao <= 0f)
            {
                return 0;
            }

            int limiteDaMissao = Mathf.FloorToInt(Mathf.Max(0, totalDeCombate) * proporcaoPorMissao);
            if (limiteDaMissao == 0 && totalDeCombate > 0 && capacidadeCreatyDisponivel > 0)
            {
                limiteDaMissao = 1;
            }

            return Mathf.Min(limiteDeMobilizacao, limiteDaMissao);
        }

        private void Awake()
        {
            brain = GetComponent<IA_BrainMaster>();
            proximoRelatorioEm = Time.time + Mathf.Max(30f, intervaloRelatorioSegundos);
        }

        private void OnEnable()
        {
            CartaCombateRegistro.EventoRegistrado -= AoRegistrarCombate;
            CartaCombateRegistro.EventoRegistrado += AoRegistrarCombate;
            IA03MarcaPresidencial.PresidenteAbatido -= AoAbaterPresidente;
            IA03MarcaPresidencial.PresidenteAbatido += AoAbaterPresidente;
            SistemaDeDanos.OnDanoGlobal -= AoReceberDanoGlobal;
            SistemaDeDanos.OnDanoGlobal += AoReceberDanoGlobal;
            SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
            SistemaDeDanos.OnMorteGlobal += AoMorrerUnidade;
            OrquestradorGlobalOrdens.OrdemConcluida -= AoConcluirOrdemMovimento;
            OrquestradorGlobalOrdens.OrdemConcluida += AoConcluirOrdemMovimento;
            GarantirAssinaturaTerritorial();
        }

        private void OnDisable()
        {
            CartaCombateRegistro.EventoRegistrado -= AoRegistrarCombate;
            if (governoAssinado != null)
            {
                governoAssinado.OnGovernoAtualizado -= AoAtualizarGoverno;
                governoAssinado = null;
            }
            IA03MarcaPresidencial.PresidenteAbatido -= AoAbaterPresidente;
            SistemaDeDanos.OnDanoGlobal -= AoReceberDanoGlobal;
            SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
            OrquestradorGlobalOrdens.OrdemConcluida -= AoConcluirOrdemMovimento;
            if (gerenteTerritorialAssinado != null)
            {
                gerenteTerritorialAssinado.OnTerritoryOwnerChanged -= AoMudarDonoTerritorial;
                gerenteTerritorialAssinado = null;
            }
            EncerrarMissao("IA03 desativada");
        }

        public void Tick(float now, float deltaTime)
        {
            if (!ativo || perfilPais == null)
            {
                return;
            }

            if (brain == null)
            {
                brain = GetComponent<IA_BrainMaster>();
            }

            if (brain == null || brain.Context == null)
            {
                ultimaDecisao = "BrainMaster indisponível";
                return;
            }

            ultimaAnaliseEm = now;

            GarantirAssinaturaTerritorial();

            if (!perfilAplicado)
            {
                AplicarPerfilNacional();
            }

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            VincularEventosGoverno(governo);
            DadosPaisGoverno pais = governo != null ? governo.ObterPais(brain.TeamId) : null;
            if (paisAlvoTeamId <= 0 && pais != null && pais.rivalTeamId > 0)
            {
                paisAlvoTeamId = pais.rivalTeamId;
            }

#if UNITY_EDITOR
            if (!nivelDebugForcado)
#endif
            {
                AtualizarConflito(governo, pais, now);
            }
            economia.TentarAplicarPesos(
                governo,
                brain,
                perfilPais,
                brain.TeamId,
                nivelDeConflito <= IA03NivelConflito.ConflitoLimitado && nivelDeConflito != IA03NivelConflito.Paz,
                out economiaAtual);
            AtualizarPosturaBrainMaster();
            AtualizarRelatorio(governo, now);
            AvaliarPrazosDeGuerra(governo, now);

            if (missaoAtiva != null)
            {
                ProcessarMissaoAtiva(now);
            }
            else if (estadoNacional != IA03EstadoNacional.Negociando && now >= proximaDecisaoDeMissaoEm)
            {
                proximaDecisaoDeMissaoEm = now + Interval;
                TentarIniciarMissao(now);
            }

            RegistrarLogDebugSeNecessario();
        }

        public void DefinirPaisAlvo(int alvoTeamId, string motivo = "incidente diplomático")
        {
            if (alvoTeamId <= 0 || brain == null || alvoTeamId == brain.TeamId)
            {
                return;
            }

            paisAlvoTeamId = alvoTeamId;
            if (nivelDeConflito == IA03NivelConflito.Paz)
            {
                DefinirNivelConflito(IA03NivelConflito.Tensao, motivo);
            }
        }

        public void DefinirNivelConflito(IA03NivelConflito novoNivel, string motivo)
        {
            if (novoNivel == nivelDeConflito)
            {
                return;
            }

            IA03NivelConflito nivelAnterior = nivelDeConflito;
            nivelDeConflito = novoNivel;
            inicioConflitoEm = novoNivel == IA03NivelConflito.Paz ? -1f : Time.time;
            if (novoNivel == IA03NivelConflito.Paz)
            {
                inicioGuerraEm = -1f;
                guerraObservada = false;
                avaliouConflitoLimitado = false;
                avaliouGuerraTotal = false;
                ignorarTensaoAte = Time.time + 300f;
                EncerrarMissao("hostilidades suspensas");
                filaMissoes.Clear();
                missoesConcluidasNaCrise.Clear();
                chavesMissoesConcluidasNaCrise.Clear();
            }
            else if (nivelAnterior == IA03NivelConflito.Paz)
            {
                proximoRelatorioEm = Time.time + Mathf.Max(30f, intervaloRelatorioSegundos);
                filaMissoes.Clear();
                missoesConcluidasNaCrise.Clear();
                chavesMissoesConcluidasNaCrise.Clear();
                relatorio.Resetar();
            }

            if (novoNivel > IA03NivelConflito.Paz
                && novoNivel <= IA03NivelConflito.ConflitoLimitado
                && inicioGuerraEm < 0f)
            {
                inicioGuerraEm = Time.time;
                avaliouConflitoLimitado = false;
                avaliouGuerraTotal = false;
                saldoAlvoNoInicio = ObterSaldoAlvo();
            }

            estadoNacional = ConverterEstado(novoNivel);
            ultimaDecisao = "Nível " + (int)novoNivel + ": " + (motivo ?? string.Empty);
            SolicitarReanaliseAntecipada();
            RegistrarLogDebugSeNecessario();

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo != null && paisAlvoTeamId > 0)
            {
                governo.RegistrarNoticia(
                    "IA03 de " + governo.NomePais(brain.TeamId)
                    + " alterou a crise com " + governo.NomePais(paisAlvoTeamId)
                    + " para " + estadoNacional + ". " + (motivo ?? string.Empty));
            }
        }

        public void RegistrarResultadoCombate(bool venceu)
        {
            relatorio.RegistrarResultadoCombate(venceu);
        }

        public void RegistrarPrejuizoEconomico(bool inimigo, float valor)
        {
            relatorio.RegistrarPrejuizoEconomico(inimigo, valor);
        }

        public void RegistrarObjetivoCapturado(bool proprio)
        {
            relatorio.RegistrarObjetivoCapturado(proprio);
        }

        /// <summary>
        /// Integração para um sistema de missão que possua confirmação
        /// autoritativa. Só aceita o sinal que a condição ativa configurou.
        /// </summary>
        public bool RegistrarResultadoMissaoExterno(string idMissao, bool sucesso, string motivo)
        {
            if (missaoAtiva == null
                || string.IsNullOrWhiteSpace(idMissao)
                || !string.Equals(idMissao, missaoAtiva.IdMissao, System.StringComparison.Ordinal))
            {
                return false;
            }

            if (sucesso && missaoAtiva.CondicaoDeSucesso != IA03CondicaoMissao.ConfirmacaoExterna
                || !sucesso && missaoAtiva.CondicaoDeFracasso != IA03CondicaoMissao.ConfirmacaoExterna)
            {
                return false;
            }

            confirmacaoExternaDeSucesso = sucesso;
            confirmacaoExternaDeFracasso = !sucesso;
            motivoConfirmacaoExterna = string.IsNullOrWhiteSpace(motivo) ? "confirmação externa" : motivo.Trim();
            SolicitarReanaliseAntecipada();
            return true;
        }

        public bool SolicitarMissaoEstrategica(MissaoEstrategicaSO missao, string motivo, bool prioridadeUrgente = false)
        {
            if (missao == null || nivelDeConflito == IA03NivelConflito.Paz
                || !missao.Aceita(nivelDeConflito, MissaoDefaultCreaty(missao))
                || MissaoFoiConcluidaNaCrise(missao)
                || SaoMissoesEquivalentes(missaoAtiva, missao)
                || ContemMissaoEquivalenteNaFila(missao))
            {
                return false;
            }

            if (prioridadeUrgente && creatyAtivo != null)
            {
                MissaoEstrategicaSO interrompida = missaoAtiva;
                FinalizarMissao(IA03ResultadoMissao.Cancelada, "interrompida por missão urgente " + missao.NomeMissao);
                if (interrompida != null)
                {
                    InserirNaFila(interrompida, false);
                }
            }

            InserirNaFila(missao, prioridadeUrgente);
            proximaDecisaoDeMissaoEm = Mathf.Min(proximaDecisaoDeMissaoEm, Time.time);
            ultimaDecisao = "missão enfileirada" + (string.IsNullOrWhiteSpace(motivo) ? string.Empty : ": " + motivo.Trim());
            SolicitarReanaliseAntecipada();
            RegistrarLogDebugSeNecessario();
            return true;
        }

        private void AplicarPerfilNacional()
        {
            if (brain == null || perfilPais == null)
            {
                return;
            }

            brain.DiplomacyWeight = perfilPais.Diplomacia;
            brain.TradeWeight = Mathf.Clamp01((perfilPais.InteresseEmPetroleo + perfilPais.InteresseEmAlimentos + perfilPais.TendenciaDeExportacao) / 3f);
            brain.IndustryWeight = perfilPais.PrioridadeEconomica;
            brain.MilitarismWeight = perfilPais.PrioridadeMilitar;
            brain.AggressionWeight = perfilPais.Agressividade;

            AplicarMetasMilitaresDoPerfil();

            if (SistemaGovernoMundial.Instancia != null
                && !string.IsNullOrWhiteSpace(perfilPais.NomePais)
                && !string.IsNullOrWhiteSpace(perfilPais.NomePresidente))
            {
                DadosPaisGoverno dados = SistemaGovernoMundial.Instancia.ObterPais(brain.TeamId);
                if (dados != null)
                {
                    SistemaGovernoMundial.Instancia.AtualizarIdentidadeNacional(
                        brain.TeamId,
                        perfilPais.NomePais,
                        perfilPais.NomePresidente,
                        dados.nomeMoeda);
                }
            }

            perfilAplicado = true;
        }

        public void AplicarMetasMilitaresDoPerfil()
        {
            if (perfilPais == null)
            {
                return;
            }

            if (brain == null)
            {
                brain = GetComponent<IA_BrainMaster>();
            }

            if (brain == null)
            {
                return;
            }

            int fleetGoal;
            int airGoal;
            switch (perfilPais.NivelEconomico)
            {
                case IA03NivelEconomico.MuitoFraco:
                    fleetGoal = 1;
                    airGoal = 2;
                    break;
                case IA03NivelEconomico.Fraco:
                    fleetGoal = 2;
                    airGoal = 4;
                    break;
                case IA03NivelEconomico.Medio:
                    fleetGoal = 4;
                    airGoal = 8;
                    break;
                case IA03NivelEconomico.Forte:
                    fleetGoal = 7;
                    airGoal = 12;
                    break;
                default:
                    fleetGoal = 10;
                    airGoal = 18;
                    break;
            }

            brain.TargetFleet = fleetGoal;
            brain.TargetAircraft = airGoal;
            brain.TargetOilTankers = perfilPais.NivelEconomico >= IA03NivelEconomico.Forte ? 2 : 1;
            brain.TargetCoastalDefenseShips = Mathf.Max(1, perfilPais.MinimoNaviosEscolta);
        }

        private void AtualizarConflito(SistemaGovernoMundial governo, DadosPaisGoverno pais, float now)
        {
            if (governo == null || paisAlvoTeamId <= 0)
            {
                return;
            }

            RelacaoPaisGoverno relacao = governo.ObterRelacao(brain.TeamId, paisAlvoTeamId);
            if (relacao == null)
            {
                return;
            }

            if (relacao.guerraDeclarada)
            {
                guerraObservada = true;
                if (inicioGuerraEm < 0f)
                {
                    inicioGuerraEm = now;
                    saldoAlvoNoInicio = ObterSaldoAlvo();
                    avaliouConflitoLimitado = false;
                    avaliouGuerraTotal = false;
                }

                if (nivelDeConflito == IA03NivelConflito.Paz || nivelDeConflito > IA03NivelConflito.ConflitoLimitado)
                {
                    DefinirNivelConflito(IA03NivelConflito.ConflitoLimitado, "guerra confirmada pelo sistema diplomático");
                }
                return;
            }

            if (guerraObservada)
            {
                DefinirNivelConflito(IA03NivelConflito.Paz, "cessar-fogo registrado pelo sistema diplomático");
                estadoNacional = IA03EstadoNacional.CessarFogo;
                return;
            }

            if (nivelDeConflito == IA03NivelConflito.Paz && now >= ignorarTensaoAte && relacao.valor <= -55)
            {
                DefinirNivelConflito(IA03NivelConflito.Tensao, "relação diplomática deteriorada");
            }

            if (nivelDeConflito == IA03NivelConflito.Tensao
                && relacao.valor <= -75
                && inicioConflitoEm >= 0f
                && now - inicioConflitoEm >= 180f)
            {
                DefinirNivelConflito(IA03NivelConflito.AvancoMilitar, "tensão persistente; forças em alerta");
            }

            if (pais != null && pais.rivalTeamId == 0 && nivelDeConflito == IA03NivelConflito.Paz)
            {
                paisAlvoTeamId = 0;
            }
        }

        private void AtualizarPosturaBrainMaster()
        {
            if (brain == null)
            {
                return;
            }

            bool propostaCessarFogoPendente = estadoNacional == IA03EstadoNacional.Negociando
                                                && ExistePropostaCessarFogoPendente(SistemaGovernoMundial.Instancia);

            switch (nivelDeConflito)
            {
                case IA03NivelConflito.Tensao:
                    brain.WarPosture = IA_WarPosture.Cautious;
                    brain.StrategicPhase = IA_StrategicPhase.PressaoEconomica;
                    estadoNacional = IA03EstadoNacional.Tensao;
                    break;
                case IA03NivelConflito.AvancoMilitar:
                    brain.WarPosture = IA_WarPosture.Cautious;
                    brain.StrategicPhase = IA_StrategicPhase.DefesaCosteira;
                    estadoNacional = IA03EstadoNacional.Mobilizacao;
                    break;
                case IA03NivelConflito.ConflitoLimitado:
                    brain.WarPosture = IA_WarPosture.BalancedAggression;
                    brain.StrategicPhase = IA_StrategicPhase.Expansao;
                    estadoNacional = propostaCessarFogoPendente
                        ? IA03EstadoNacional.Negociando
                        : IA03EstadoNacional.ConflitoLimitado;
                    break;
                case IA03NivelConflito.GuerraTotal:
                    brain.WarPosture = IA_WarPosture.TotalPressure;
                    brain.StrategicPhase = IA_StrategicPhase.Dominacao;
                    estadoNacional = propostaCessarFogoPendente
                        ? IA03EstadoNacional.Negociando
                        : IA03EstadoNacional.GuerraTotal;
                    break;
                default:
                    if (estadoNacional != IA03EstadoNacional.CessarFogo || Time.time >= ignorarTensaoAte)
                    {
                        estadoNacional = IA03EstadoNacional.Paz;
                    }
                    break;
            }
        }

        private void AtualizarRelatorio(SistemaGovernoMundial governo, float now)
        {
            if (nivelDeConflito == IA03NivelConflito.Paz || now < proximoRelatorioEm)
            {
                return;
            }

            proximoRelatorioEm = now + Mathf.Max(60f, intervaloRelatorioSegundos);
            IA_Context contexto = brain != null ? brain.Context : null;
            IA_WorldState mundo = contexto != null ? contexto.WorldState : null;
            int unidadesProprias = mundo != null ? mundo.OwnCombatUnits.Count : 0;
            int inimigosConhecidos = ContarInimigosConhecidos(mundo);
            int forcaInicial = Mathf.Max(unidadesProprias, relatorio.Acumulado.UnidadesPropriasDisponiveis + relatorio.Acumulado.UnidadesPropriasPerdidas);

            relatorio.RegistrarEconomia(economiaAtual);
            Hegemonia.AI.BrainMaster.IA_ForceSnapshot forcaPropria = mundo != null ? mundo.ForceSnapshot : null;
            IA03RelatorioSnapshot snapshot = relatorio.CriarRelatorio(unidadesProprias, inimigosConhecidos, forcaInicial, now, forcaPropria);
            if (governo != null)
            {
                governo.RegistrarNoticia(
                    "Relatório IA03 " + governo.NomePais(brain.TeamId)
                    + ": próprias disponíveis=" + snapshot.UnidadesPropriasDisponiveis
                    + ", inimigos conhecidos=" + snapshot.UnidadesInimigasConhecidas
                    + ", próprias perdidas=" + snapshot.UnidadesPropriasPerdidas
                    + ", inimigos destruídos=" + snapshot.InimigosDestruidos
                    + ", estruturas inimigas destruídas=" + snapshot.EstruturasInimigasDestruidas
                    + ", forças próprias (inf/tan/avi/nav/sub/PA)=" + snapshot.InfantariaPropriaDisponivel
                    + "/" + snapshot.TanquesPropriosDisponiveis + "/" + snapshot.AvioesPropriosDisponiveis
                    + "/" + snapshot.NaviosPropriosDisponiveis + "/" + snapshot.SubmarinosPropriosDisponiveis
                    + "/" + snapshot.PortaAvioesPropriosDisponiveis
                    + ", reposição (quar/fáb/est/aero)=" + snapshot.QuarteisProprios
                    + "/" + snapshot.FabricasProprias + "/" + snapshot.EstaleirosProprios
                    + "/" + snapshot.AeroportosMilitaresProprios
                    + ", estruturas próprias destruídas=" + snapshot.EstruturasPropriasDestruidas
                    + ", dano estrutural (inimigo/próprio)=" + snapshot.DanoEstruturalInimigo
                    + "/" + snapshot.DanoEstruturalProprio
                    + ", prejuízo monetário estimado (inimigo/próprio)=" + snapshot.PrejuizoEconomicoInimigo
                    + "/" + snapshot.PrejuizoEconomicoProprio
                    + ", batalhas=" + snapshot.BatalhasVencidas + "/" + snapshot.TotalDeBatalhas
                    + ", objetivos=" + snapshot.ObjetivosCapturados + "/" + snapshot.ObjetivosPerdidos
                    + ", capacidade restante=" + Mathf.RoundToInt(snapshot.CapacidadeMilitarRestante * 100f) + "%"
                    + ", saldo=" + snapshot.SaldoNacional + ", comida=" + snapshot.EstoqueComida
                    + ", petróleo=" + snapshot.EstoquePetroleo + ", energia=" + snapshot.EstoqueEnergia + ".");
            }
        }

        private void AvaliarPrazosDeGuerra(SistemaGovernoMundial governo, float now)
        {
            if (governo == null || inicioGuerraEm < 0f || paisAlvoTeamId <= 0)
            {
                return;
            }

            float duracao = now - inicioGuerraEm;
            if (nivelDeConflito == IA03NivelConflito.ConflitoLimitado
                && !avaliouConflitoLimitado
                && duracao >= perfilPais.AvaliacaoConflitoLimitadoSegundos)
            {
                avaliouConflitoLimitado = true;
                IA03RelatorioSnapshot resultado = relatorio.Acumulado;
                bool venceuBatalhas = relatorio.AtingiuDominioMinimo(
                    perfilPais.MinimoDeBatalhasParaAvaliar,
                    perfilPais.DominioMinimo);
                bool causouPrejuizoAlto = resultado.PrejuizoEconomicoInimigo > 0f
                                          && CalcularPrejuizoEconomicoRelativo() >= perfilPais.PrejuizoEconomicoMinimoParaPaz;
                bool baixaToleranciaPressionada = CalcularProporcaoBaixas(resultado)
                                                  >= Mathf.Lerp(0.1f, 0.65f, perfilPais.ToleranciaABaixas);
                bool economiaSobPressao = economiaAtual.ReservaFinanceiraBaixa || economiaAtual.EstoqueEssencialBaixo;

                if (venceuBatalhas || causouPrejuizoAlto || baixaToleranciaPressionada || economiaSobPressao)
                {
                    string razao = venceuBatalhas
                        ? "o relatório indica domínio militar de " + Mathf.RoundToInt(resultado.Dominio * 100f) + "%"
                        : causouPrejuizoAlto
                            ? "o prejuízo econômico estimado alcançou o limite nacional"
                            : baixaToleranciaPressionada
                                ? "as baixas próprias ultrapassaram a tolerância nacional"
                                : "as reservas nacionais estão sob pressão";
                    if (perfilPais.DisposicaoParaNegociar < 0.25f && resultado.PontuacaoDeGuerra >= 0f)
                    {
                        ultimaDecisao = "Nível 2 avaliado; o perfil nacional prefere continuar o conflito.";
                    }
                    else
                    {
                        estadoNacional = IA03EstadoNacional.Negociando;
                        bool enviada = diplomacia.TentarProporCessarFogo(
                        governo,
                        brain.TeamId,
                        paisAlvoTeamId,
                        perfilPais.NomePresidente,
                        razao,
                        "ia03_n2_paz:" + brain.TeamId + ":" + paisAlvoTeamId,
                        out string mensagem);
                        ultimaDecisao = enviada ? mensagem : "Cessar-fogo não enviado: " + mensagem;
                        if (enviada && resultado.PontuacaoDeGuerra > 0f)
                        {
                            float fracaoIndenizacao = Mathf.Lerp(0.15f, 0.35f, Mathf.Clamp01(resultado.PontuacaoDeGuerra / 100f));
                            diplomacia.TentarProporIndenizacao(
                                governo,
                                brain.TeamId,
                                paisAlvoTeamId,
                                fracaoIndenizacao,
                                perfilPais.NomePresidente,
                                "termos de paz após o relatório de conflito limitado",
                                "ia03_n2_indenizacao:" + brain.TeamId + ":" + paisAlvoTeamId,
                                out _);
                        }
                    }
                }
                else
                {
                    ultimaDecisao = "Nível 2 avaliado aos 25 minutos; ainda sem evidência suficiente para negociar.";
                }
            }

            bool propostaPendente = ExistePropostaCessarFogoPendente(governo);
            if (nivelDeConflito == IA03NivelConflito.ConflitoLimitado
                && avaliouConflitoLimitado
                && duracao >= perfilPais.AvaliacaoConflitoLimitadoSegundos + 300f
                && !propostaPendente
                && perfilPais.Agressividade >= 0.55f)
            {
                DefinirNivelConflito(IA03NivelConflito.GuerraTotal, "as hostilidades continuaram após a avaliação do Nível 2");
            }

            if (nivelDeConflito == IA03NivelConflito.GuerraTotal
                && !avaliouGuerraTotal
                && duracao >= perfilPais.AvaliacaoGuerraTotalSegundos)
            {
                avaliouGuerraTotal = true;
                IA03RelatorioSnapshot resultado = relatorio.Acumulado;
                float pontuacao = resultado.PontuacaoDeGuerra;
                bool perdasAltas = CalcularProporcaoBaixas(relatorio.Acumulado)
                                   >= Mathf.Lerp(0.1f, 0.65f, perfilPais.ToleranciaABaixas);
                bool querNegociar = pontuacao < 0f || perdasAltas || perfilPais.DisposicaoParaNegociar >= 0.65f
                                    || economiaAtual.ReservaFinanceiraBaixa || economiaAtual.EstoqueEssencialBaixo;
                string decisao = querNegociar
                    ? (pontuacao >= 0f ? "oferecer cessar-fogo após avaliar a vantagem" : "buscar cessar-fogo para reduzir perdas")
                    : "continuar a guerra após a avaliação completa";
                if (querNegociar)
                {
                    diplomacia.TentarProporCessarFogo(
                        governo,
                        brain.TeamId,
                        paisAlvoTeamId,
                        perfilPais.NomePresidente,
                        decisao + "; pontuação de guerra " + pontuacao.ToString("0") + ".",
                        "ia03_n1_avaliacao:" + brain.TeamId + ":" + paisAlvoTeamId,
                        out _);
                    if (pontuacao >= 50f)
                    {
                        float fracaoIndenizacao = Mathf.Lerp(0.25f, 0.6f, Mathf.Clamp01(pontuacao / 250f));
                        diplomacia.TentarProporIndenizacao(
                            governo,
                            brain.TeamId,
                            paisAlvoTeamId,
                            fracaoIndenizacao,
                            perfilPais.NomePresidente,
                            "termos econômicos após a avaliação de uma hora",
                            "ia03_n1_indenizacao:" + brain.TeamId + ":" + paisAlvoTeamId,
                            out _);
                    }
                    if (pontuacao >= 250f)
                    {
                        bool vitoriaPraticamenteCompleta = pontuacao >= 1000f
                            && relatorio.AtingiuDominioMinimo(perfilPais.MinimoDeBatalhasParaAvaliar, 0.9f);
                        float fracaoTerritorial = vitoriaPraticamenteCompleta ? 1f
                            : pontuacao >= 500f ? 0.75f
                            : 0.5f;
                        diplomacia.TentarProporCessaoTerritorial(
                            governo,
                            brain.TeamId,
                            paisAlvoTeamId,
                            fracaoTerritorial,
                            perfilPais.NomePresidente,
                            "termos territoriais após a avaliação de uma hora",
                            "ia03_n1_territorio:" + brain.TeamId + ":" + paisAlvoTeamId,
                            out _);
                    }
                }
                ultimaDecisao = "Avaliação de uma hora: " + decisao + ".";
                governo.RegistrarNoticia("IA03: " + ultimaDecisao);
            }
        }

        private float CalcularPrejuizoEconomicoRelativo()
        {
            float prejuizo = relatorio.Acumulado.PrejuizoEconomicoInimigo;
            float baseEconomica = Mathf.Max(1f, saldoAlvoNoInicio);
            return Mathf.Clamp01(prejuizo / baseEconomica);
        }

        private static float CalcularProporcaoBaixas(IA03RelatorioSnapshot resultado)
        {
            if (resultado == null)
            {
                return 0f;
            }

            return resultado.UnidadesPropriasPerdidas
                   / (float)Mathf.Max(1, resultado.UnidadesPropriasDisponiveis + resultado.UnidadesPropriasPerdidas);
        }

        private bool ExistePropostaCessarFogoPendente(SistemaGovernoMundial governo)
        {
            if (governo == null)
            {
                return false;
            }

            IReadOnlyList<PropostaInternacional> propostas = governo.Propostas;
            for (int i = 0; i < propostas.Count; i++)
            {
                PropostaInternacional proposta = propostas[i];
                if (proposta != null
                    && proposta.origemTeamId == brain.TeamId
                    && proposta.alvoTeamId == paisAlvoTeamId
                    && proposta.tipo == TipoPropostaInternacional.CessarFogo
                    && proposta.EstaPendente)
                {
                    return true;
                }
            }

            return false;
        }

        private void TentarIniciarMissao(float now)
        {
            if (nivelDeConflito == IA03NivelConflito.Paz
                || brain == null
                || brain.Context == null
                || brain.Context.WorldState == null
                || brain.Context.CommandQueue == null
                || brain.IntegrationMode == IA_BrainMaster.IA_IntegrationMode.ShadowReadOnly)
            {
                return;
            }

            AtualizarFilaMissoes();
            MissaoEstrategicaSO selecionada = filaMissoes.Count > 0 ? filaMissoes[0] : null;
            IA03DominioEstrategico dominio = selecionada != null
                ? selecionada.Dominio
                : ResolverDominioPreferido();
            int minimoUnidades = selecionada != null ? selecionada.QuantidadeMinimaDeUnidades : 1;

            tiposCreatyCandidatos.Clear();
            if (selecionada != null && selecionada.CreatysPermitidos != null && selecionada.CreatysPermitidos.Count > 0)
            {
                for (int i = 0; i < selecionada.CreatysPermitidos.Count; i++)
                {
                    IA03TipoCreaty tipo = selecionada.CreatysPermitidos[i];
                    if (selecionada.Aceita(nivelDeConflito, tipo))
                    {
                        tiposCreatyCandidatos.Add(tipo);
                    }
                }
            }
            else
            {
                tiposCreatyCandidatos.Add(ResolverTipoCreaty(dominio, nivelDeConflito));
            }

            if (tiposCreatyCandidatos.Count == 0)
            {
                ultimaDecisao = "missão não permitida no nível estratégico atual";
                return;
            }

            CreatyEstrategico ponto = SelecionarCreaty(tiposCreatyCandidatos, dominio, minimoUnidades);
            if (ponto == null)
            {
                ultimaDecisao = "nenhum Creaty manual compatível está disponível para " + estadoNacional;
                return;
            }

            int capacidadeCreatyDisponivel = Mathf.Max(0, ponto.MaximoDeUnidades - ponto.UnidadesReservadas);
            if (!ConstruirGrupoUnidades(selecionada, dominio, minimoUnidades, capacidadeCreatyDisponivel))
            {
                ultimaDecisao = "unidades insuficientes para a missão " + (selecionada != null ? selecionada.NomeMissao : ponto.Tipo.ToString());
                return;
            }

            if (!ponto.TentarReservar(brain.TeamId, paisAlvoTeamId, unidadesCandidatas.Count))
            {
                ultimaDecisao = "Creaty ocupado ou em tempo de reutilização: " + ponto.Id;
                return;
            }

            ResetarRastreamentoMissao();
            if (!EnfileirarOrdem(selecionada, ponto, unidadesCandidatas, out string motivo))
            {
                ponto.LiberarReserva(unidadesCandidatas.Count);
                ultimaDecisao = "ordem não enfileirada: " + motivo;
                return;
            }

            missaoAtiva = selecionada;
            if (selecionada != null)
            {
                filaMissoes.Remove(selecionada);
            }
            creatyAtivo = ponto;
            equipeAlvoAtiva = paisAlvoTeamId;
            unidadesReservadas = unidadesCandidatas.Count;
            unidadesAtivasNaMissao.Clear();
            unidadesAtivasNaMissao.AddRange(unidadesCandidatas);
            unidadesOriginaisNaMissao = unidadesAtivasNaMissao.Count;
            inicioMissaoEm = now;
            estadoDaMissao = IA03ResultadoMissao.EmAndamento;
            ultimaMissao = selecionada != null ? selecionada.NomeMissao : ponto.Tipo.ToString();
            ultimaDecisao = "missão enviada: " + ultimaMissao + " -> " + ponto.Id;
            RegistrarLogDebugSeNecessario();
        }

        private void AtualizarFilaMissoes()
        {
            for (int i = filaMissoes.Count - 1; i >= 0; i--)
            {
                MissaoEstrategicaSO pendente = filaMissoes[i];
                if (pendente == null
                    || !pendente.Aceita(nivelDeConflito, MissaoDefaultCreaty(pendente))
                    || MissaoFoiConcluidaNaCrise(pendente))
                {
                    filaMissoes.RemoveAt(i);
                }
            }

            if (missoesEstrategicas == null)
            {
                return;
            }

            for (int i = 0; i < missoesEstrategicas.Count; i++)
            {
                MissaoEstrategicaSO missao = missoesEstrategicas[i];
                if (missao == null
                    || !missao.Aceita(nivelDeConflito, MissaoDefaultCreaty(missao))
                    || MissaoFoiConcluidaNaCrise(missao)
                    || ContemMissaoEquivalenteNaFila(missao)
                    || SaoMissoesEquivalentes(missaoAtiva, missao))
                {
                    continue;
                }

                InserirNaFila(missao, false);
            }
        }

        private void InserirNaFila(MissaoEstrategicaSO missao, bool prioridadeUrgente)
        {
            if (missao == null || ContemMissaoEquivalenteNaFila(missao))
            {
                return;
            }

            if (prioridadeUrgente)
            {
                filaMissoes.Insert(0, missao);
                return;
            }

            int index = 0;
            while (index < filaMissoes.Count && filaMissoes[index] != null
                   && filaMissoes[index].Prioridade >= missao.Prioridade)
            {
                index++;
            }
            filaMissoes.Insert(index, missao);
        }

        private CreatyEstrategico SelecionarCreaty(List<IA03TipoCreaty> tipos, IA03DominioEstrategico dominio, int minimoUnidades)
        {
            candidatos.Clear();
            for (int tipoIndex = 0; tipoIndex < tipos.Count; tipoIndex++)
            {
                RegistroCreatysEstrategicos.PreencherCandidatos(
                    candidatos,
                    brain.TeamId,
                    paisAlvoTeamId,
                    tipos[tipoIndex],
                    nivelDeConflito,
                    dominio);

                for (int i = candidatos.Count - 1; i >= 0; i--)
                {
                    CreatyEstrategico candidato = candidatos[i];
                    if (candidato.EstaEmRecarga
                        || candidato.UnidadesReservadas + minimoUnidades > candidato.MaximoDeUnidades)
                    {
                        candidatos.RemoveAt(i);
                    }
                }

                if (candidatos.Count > 0)
                {
                    return SelecionarPonderado(candidatos);
                }
            }

            return null;
        }

        private CreatyEstrategico SelecionarPonderado(List<CreatyEstrategico> lista)
        {
            float pesoTotal = 0f;
            for (int i = 0; i < lista.Count; i++)
            {
                CreatyEstrategico creaty = lista[i];
                pesoTotal += Mathf.Max(0.01f, creaty.PesoDeEscolha) * (0.25f + creaty.Prioridade / 100f);
            }

            float sorteio = Random.value * pesoTotal;
            for (int i = 0; i < lista.Count; i++)
            {
                CreatyEstrategico creaty = lista[i];
                sorteio -= Mathf.Max(0.01f, creaty.PesoDeEscolha) * (0.25f + creaty.Prioridade / 100f);
                if (sorteio <= 0f)
                {
                    return creaty;
                }
            }

            return lista[lista.Count - 1];
        }

        private bool ConstruirGrupoUnidades(
            MissaoEstrategicaSO missao,
            IA03DominioEstrategico dominio,
            int minimoUnidades,
            int capacidadeCreatyDisponivel)
        {
            unidadesCandidatas.Clear();
            candidatosGrupo.Clear();
            IA_WorldState mundo = brain.Context.WorldState;
            int totalDeCombate = Mathf.Max(0, mundo.OwnCombatUnits.Count);
            float reserva = perfilPais != null ? perfilPais.ReservaDefesaNacional : 0.1f;
            float tetoDeGuerra = perfilPais != null ? perfilPais.ContingenteMaximoDeGuerra : 0.9f;
            int limiteUnidades = CalcularLimitePorMissaoN1(
                totalDeCombate,
                capacidadeCreatyDisponivel,
                nivelDeConflito,
                reserva,
                tetoDeGuerra,
                perfilPais != null ? perfilPais.ContingenteMaximoPorMissaoN1 : 0.5f);

            bool exigePortaAvioes = missao != null && missao.ExigePortaAvioes;
            bool exigeSubmarino = missao != null && missao.ExigeSubmarino;
            bool exigeTransporte = missao != null && missao.ExigeTransporteNaval;
            bool exigePresidente = missao != null && missao.ExigePresidente;
            int minimoEscolta = Mathf.Max(perfilPais.MinimoNaviosEscolta, missao != null ? missao.MinimoNaviosEscolta : 0);
            int minimoAeronaves = Mathf.Max(perfilPais.MinimoAeronavesPortaAvioes, missao != null ? missao.MinimoAeronaves : 0);

            List<GameObject> fonte = mundo.OwnUnits;
            for (int i = 0; i < fonte.Count; i++)
            {
                GameObject unidade = fonte[i];
                if (unidade == null || !unidade.activeInHierarchy)
                {
                    continue;
                }

                IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
                if (identidade == null || identidade.teamID != brain.TeamId)
                {
                    continue;
                }

                bool ehPresidente = unidade.GetComponent<IA03MarcaPresidencial>() != null;
                if (ehPresidente && !exigePresidente)
                {
                    continue;
                }

                bool ehNavio = identidade.tipoUnidade == TipoUnidade.Naval;
                bool ehAeronave = identidade.tipoUnidade == TipoUnidade.Aereo;
                bool ehTerrestre = identidade.tipoUnidade == TipoUnidade.Infantaria || identidade.tipoUnidade == TipoUnidade.Veiculo;
                bool ehSubmarino = unidade.GetComponent<ControleSubmarino>() != null;
                bool ehTransporteNaval = unidade.GetComponent<NavioTransporteTropas>() != null;
                bool ehPortaAvioes = unidade.GetComponentInChildren<GerenciadorPortaAvioes>() != null
                                      || unidade.GetComponentInChildren<GerenciadorOperacoesPortaAvioesV2>() != null;

                bool elegivel = false;
                if (exigePortaAvioes)
                {
                    elegivel = ehPortaAvioes || ehNavio || ehAeronave;
                }
                else if (exigeSubmarino)
                {
                    elegivel = ehSubmarino;
                }
                else if (exigeTransporte)
                {
                    elegivel = ehTransporteNaval || ehTerrestre;
                }
                else if (ehPresidente || dominio == IA03DominioEstrategico.Combinado)
                {
                    elegivel = true;
                }
                else
                {
                    elegivel = dominio == IA03DominioEstrategico.Terrestre && ehTerrestre
                               || dominio == IA03DominioEstrategico.Naval && ehNavio
                               || dominio == IA03DominioEstrategico.Aereo && ehAeronave;
                }

                if (!elegivel)
                {
                    continue;
                }

                candidatosGrupo.Add(new CandidatoGrupo
                {
                    Unidade = unidade,
                    PortaAvioes = ehPortaAvioes,
                    Escolta = ehNavio && !ehPortaAvioes && !ehSubmarino && !ehTransporteNaval,
                    Aeronave = ehAeronave,
                    Submarino = ehSubmarino,
                    Transporte = ehTransporteNaval,
                    Terrestre = ehTerrestre,
                    Presidente = ehPresidente
                });
            }

            int minimoDeGrupo = Mathf.Max(minimoUnidades,
                (exigePortaAvioes ? 1 + minimoEscolta + minimoAeronaves : 0)
                + (exigeSubmarino ? 1 : 0)
                + (exigeTransporte ? 2 : 0)
                + (exigePresidente ? 1 : 0));
            if (limiteUnidades < minimoDeGrupo || candidatosGrupo.Count < minimoDeGrupo)
            {
                return false;
            }

            int portaAvioes = 0;
            int escoltas = 0;
            int aeronaves = 0;
            int submarinos = 0;
            int transportes = 0;
            int terrestres = 0;
            int presidentes = 0;
            if (exigePortaAvioes)
            {
                AdicionarPapelNecessario(PapelUnidadeGrupo.PortaAvioes, 1, limiteUnidades, ref portaAvioes);
                AdicionarPapelNecessario(PapelUnidadeGrupo.Escolta, minimoEscolta, limiteUnidades, ref escoltas);
                AdicionarPapelNecessario(PapelUnidadeGrupo.Aeronave, minimoAeronaves, limiteUnidades, ref aeronaves);
            }

            if (exigeSubmarino)
            {
                AdicionarPapelNecessario(PapelUnidadeGrupo.Submarino, 1, limiteUnidades, ref submarinos);
            }

            if (exigeTransporte)
            {
                AdicionarPapelNecessario(PapelUnidadeGrupo.Transporte, 1, limiteUnidades, ref transportes);
                AdicionarPapelNecessario(PapelUnidadeGrupo.Terrestre, 1, limiteUnidades, ref terrestres);
            }

            if (exigePresidente)
            {
                AdicionarPapelNecessario(PapelUnidadeGrupo.Presidente, 1, limiteUnidades, ref presidentes);
            }

            for (int i = 0; i < candidatosGrupo.Count && unidadesCandidatas.Count < limiteUnidades; i++)
            {
                GameObject unidade = candidatosGrupo[i].Unidade;
                if (unidade != null && !unidadesCandidatas.Contains(unidade))
                {
                    unidadesCandidatas.Add(unidade);
                }
            }

            if (unidadesCandidatas.Count < minimoUnidades
                || (exigePortaAvioes && (portaAvioes < 1 || escoltas < minimoEscolta || aeronaves < minimoAeronaves))
                || (exigeSubmarino && submarinos < 1)
                || (exigeTransporte && (transportes < 1 || terrestres < 1))
                || (exigePresidente && presidentes < 1))
            {
                unidadesCandidatas.Clear();
                return false;
            }

            return true;
        }

        private void AdicionarPapelNecessario(PapelUnidadeGrupo papel, int quantidade, int limite, ref int adicionados)
        {
            for (int i = 0; i < candidatosGrupo.Count && adicionados < quantidade && unidadesCandidatas.Count < limite; i++)
            {
                CandidatoGrupo candidato = candidatosGrupo[i];
                if (candidato.Unidade == null || unidadesCandidatas.Contains(candidato.Unidade) || !PossuiPapel(candidato, papel))
                {
                    continue;
                }

                unidadesCandidatas.Add(candidato.Unidade);
                adicionados++;
            }
        }

        private static bool PossuiPapel(CandidatoGrupo candidato, PapelUnidadeGrupo papel)
        {
            switch (papel)
            {
                case PapelUnidadeGrupo.PortaAvioes: return candidato.PortaAvioes;
                case PapelUnidadeGrupo.Escolta: return candidato.Escolta;
                case PapelUnidadeGrupo.Aeronave: return candidato.Aeronave;
                case PapelUnidadeGrupo.Submarino: return candidato.Submarino;
                case PapelUnidadeGrupo.Transporte: return candidato.Transporte;
                case PapelUnidadeGrupo.Terrestre: return candidato.Terrestre;
                case PapelUnidadeGrupo.Presidente: return candidato.Presidente;
                default: return false;
            }
        }

        private bool EnfileirarOrdem(MissaoEstrategicaSO missao, CreatyEstrategico ponto, List<GameObject> unidades, out string motivo)
        {
            motivo = string.Empty;
            IA_CommandType tipoComando = missao != null && missao.TipoOrdem == IA03TipoOrdem.Atacar
                ? IA_CommandType.Attack
                : missao != null && missao.TipoOrdem == IA03TipoOrdem.Patrulhar
                    ? IA_CommandType.Patrol
                    : IA_CommandType.Move;

            object payload;
            bool exigeDestruicaoDeAlvo = missao != null
                && (missao.CondicaoDeSucesso == IA03CondicaoMissao.DestruirAlvo
                    || missao.CondicaoDeFracasso == IA03CondicaoMissao.DestruirAlvo);
            IA_EnemyObservation alvoObservado = tipoComando == IA_CommandType.Attack || exigeDestruicaoDeAlvo
                ? SelecionarAlvoObservado(
                    missao != null ? missao.Alvo : ponto.AlvoPreferencial,
                    ponto.transform.position)
                : null;

            if (alvoObservado != null)
            {
                alvoMissaoAtivo = alvoObservado.Transform;
                alvoMissaoFoiDefinido = alvoMissaoAtivo != null;
                idPersistenteAlvoMissao = ObterIdPersistenteAlvo(alvoMissaoAtivo);
                alvoMissaoDestruido = false;
            }
            else if (missao != missaoAtiva)
            {
                alvoMissaoAtivo = null;
                alvoMissaoFoiDefinido = false;
                idPersistenteAlvoMissao = string.Empty;
                alvoMissaoDestruido = false;
            }

            if (tipoComando == IA_CommandType.Attack)
            {
                payload = new IA_AttackOrderData
                {
                    Units = new List<GameObject>(unidades),
                    Target = alvoObservado != null ? alvoObservado.Transform : null,
                    TargetPosition = alvoObservado != null ? alvoObservado.Position : ponto.transform.position
                };
            }
            else if (tipoComando == IA_CommandType.Patrol)
            {
                Vector3 pontoB = ponto.ProximoPonto != null ? ponto.ProximoPonto.transform.position : ponto.transform.position;
                payload = new IA_PatrolOrderData
                {
                    Units = new List<GameObject>(unidades),
                    PointA = ponto.transform.position,
                    PointB = pontoB
                };
            }
            else
            {
                payload = new IA_MoveOrderData
                {
                    Units = new List<GameObject>(unidades),
                    Destination = ponto.transform.position
                };
            }

            string seed = "ia03:" + brain.TeamId + ":" + paisAlvoTeamId + ":"
                          + (missao != null ? missao.IdMissao : ponto.Tipo.ToString()) + ":" + ponto.Id;
            IA_CommandRequest pedido = IA_CommandFactory.Create(
                tipoComando,
                "IA03EstrategaNacional",
                "estrategico",
                missao != null ? missao.NomeMissao : "ponto estratégico manual",
                missao != null ? Mathf.Clamp(missao.Prioridade, 1, 1000) : Mathf.Clamp(ponto.Prioridade, 1, 1000),
                tipoComando == IA_CommandType.Attack ? "tactical" : "tactical",
                seed,
                Mathf.Max(1f, ponto.TempoDeReutilizacaoSegundos),
                payload);

            if (!brain.Context.CommandQueue.Enqueue(pedido, Time.time, out motivo))
            {
                return false;
            }

            idOrdemAtivaDaMissao = pedido.Id;
            return true;
        }

        private void ProcessarMissaoAtiva(float now)
        {
            if (brain == null || brain.IntegrationMode == IA_BrainMaster.IA_IntegrationMode.ShadowReadOnly)
            {
                EncerrarMissao("modo somente leitura");
                return;
            }

            if (creatyAtivo == null)
            {
                FinalizarMissao(IA03ResultadoMissao.Fracasso, "Creaty indisponível");
                return;
            }

            int vivos = 0;
            int chegaram = 0;
            bool rastrearChegada = missaoAtiva == null
                                   || missaoAtiva.CondicaoDeSucesso == IA03CondicaoMissao.ChegarAoDestino
                                   || missaoAtiva.CondicaoDeSucesso == IA03CondicaoMissao.PermanecerNoDestino
                                   || missaoAtiva.CondicaoDeFracasso == IA03CondicaoMissao.PermanecerNoDestino;
            float raio = 30f;
            for (int i = unidadesAtivasNaMissao.Count - 1; i >= 0; i--)
            {
                GameObject unidade = unidadesAtivasNaMissao[i];
                if (unidade == null || !unidade.activeInHierarchy)
                {
                    grupoPerdeuUnidade = true;
                    unidadesAtivasNaMissao.RemoveAt(i);
                    continue;
                }

                int unidadeId = unidade.GetInstanceID();
                if (unidadesPerdidasNaMissao.Contains(unidadeId))
                {
                    grupoPerdeuUnidade = true;
                    continue;
                }

                vivos++;
                if (rastrearChegada
                    && (unidadesComOrdemConcluidaNaMissao.Contains(unidadeId)
                        || Vector3.SqrMagnitude(unidade.transform.position - creatyAtivo.transform.position) <= raio * raio))
                {
                    unidadesComOrdemConcluidaNaMissao.Add(unidadeId);
                    chegaram++;
                }
            }

            float tempoMaximo = missaoAtiva != null ? missaoAtiva.TempoMaximoSegundos : 600f;
            if (vivos == 0)
            {
                FinalizarMissao(IA03ResultadoMissao.Fracasso, "grupo indisponível");
                return;
            }

            bool todosChegaram = chegaram == vivos;
            if (todosChegaram)
            {
                grupoChegouAoDestino = true;
                if (inicioPermanenciaNoDestinoEm < 0f)
                {
                    inicioPermanenciaNoDestinoEm = now;
                }
            }
            else if (grupoChegouAoDestino)
            {
                grupoSaiuDoDestino = true;
            }

            if ((missaoAtiva != null &&
                 (missaoAtiva.CondicaoDeSucesso == IA03CondicaoMissao.CapturarTerritorio
                  || missaoAtiva.CondicaoDeFracasso == IA03CondicaoMissao.CapturarTerritorio))
                && GerenteDeTerritorio.Instancia != null)
            {
                ResultadoConsultaTerritorio objetivo = GerenteDeTerritorio.Instancia.ObterTerritorioNaPosicao(creatyAtivo.transform.position);
                if (objetivo.encontrouRegiao)
                {
                    territorioDoObjetivoCapturado = objetivo.ownerCountryTeamId == brain.TeamId;
                    territorioDoObjetivoPerdido = objetivo.ownerCountryTeamId == equipeAlvoAtiva
                                                   && !territorioDoObjetivoCapturado;
                }
            }

            bool prazoEncerrado = tempoMaximo > 0f && now - inicioMissaoEm >= tempoMaximo;
            float tempoPermanenciaMinimo = missaoAtiva != null
                ? Mathf.Max(0f, missaoAtiva.TempoMinimoDePermanenciaSegundos)
                : 0f;
            bool permaneceuNoDestino = todosChegaram
                                       && inicioPermanenciaNoDestinoEm >= 0f
                                       && now - inicioPermanenciaNoDestinoEm >= tempoPermanenciaMinimo;
            bool grupoSobreviveuAteOPrazo = prazoEncerrado
                                            && !grupoPerdeuUnidade
                                            && vivos == unidadesOriginaisNaMissao;
            bool alvoDestruido = alvoMissaoDestruido;
            IA03CondicaoMissao condicaoSucesso = missaoAtiva != null
                ? missaoAtiva.CondicaoDeSucesso
                : IA03CondicaoMissao.ChegarAoDestino;
            IA03CondicaoMissao condicaoFracasso = missaoAtiva != null
                ? missaoAtiva.CondicaoDeFracasso
                : IA03CondicaoMissao.SobreviverAteOPrazo;
            IA03ResultadoMissao resultado = IA03AvaliadorMissao.Avaliar(
                condicaoSucesso,
                condicaoFracasso,
                todosChegaram,
                permaneceuNoDestino,
                grupoSaiuDoDestino,
                alvoDestruido,
                territorioDoObjetivoCapturado,
                territorioDoObjetivoPerdido,
                grupoSobreviveuAteOPrazo,
                grupoPerdeuUnidade,
                prazoEncerrado,
                confirmacaoExternaDeSucesso,
                confirmacaoExternaDeFracasso);

            estadoDaMissao = resultado;
            if (resultado == IA03ResultadoMissao.Fracasso || resultado == IA03ResultadoMissao.Expirada)
            {
                string motivo = !string.IsNullOrWhiteSpace(motivoConfirmacaoExterna)
                    && confirmacaoExternaDeFracasso
                    ? motivoConfirmacaoExterna
                    : resultado == IA03ResultadoMissao.Expirada
                        ? "tempo máximo da missão encerrado sem cumprir o objetivo"
                        : "condição de fracasso atingida";
                FinalizarMissao(resultado, motivo);
                return;
            }

            if (resultado != IA03ResultadoMissao.Sucesso)
            {
                return;
            }

            bool sucessoPorPresenca = condicaoSucesso == IA03CondicaoMissao.ChegarAoDestino
                                      || condicaoSucesso == IA03CondicaoMissao.PermanecerNoDestino;
            CreatyEstrategico proximo = sucessoPorPresenca ? creatyAtivo.ProximoPonto : null;
            if (proximo != null && proximo.TentarReservar(brain.TeamId, equipeAlvoAtiva, unidadesAtivasNaMissao.Count))
            {
                if (EnfileirarOrdem(missaoAtiva, proximo, unidadesAtivasNaMissao, out string motivo))
                {
                    creatyAtivo.LiberarReserva(unidadesReservadas);
                    creatyAtivo = proximo;
                    unidadesReservadas = unidadesAtivasNaMissao.Count;
                    inicioMissaoEm = now;
                    inicioPermanenciaNoDestinoEm = -1f;
                    grupoChegouAoDestino = false;
                    grupoSaiuDoDestino = false;
                    territorioDoObjetivoCapturado = false;
                    territorioDoObjetivoPerdido = false;
                    ultimaDecisao = "rota estratégica avançou para " + proximo.Id;
                    return;
                }

                proximo.LiberarReserva(unidadesAtivasNaMissao.Count);
                ultimaDecisao = "ordem de rota não enfileirada: " + motivo;
            }

            FinalizarMissao(IA03ResultadoMissao.Sucesso, motivoConfirmacaoExterna.Length > 0
                ? motivoConfirmacaoExterna
                : "objetivo da missão alcançado");
        }

        private void EncerrarMissao(string motivo)
        {
            FinalizarMissao(IA03ResultadoMissao.Cancelada, motivo);
        }

        private void FinalizarMissao(IA03ResultadoMissao resultado, string motivo)
        {
            bool sucesso = resultado == IA03ResultadoMissao.Sucesso;
            bool final = resultado == IA03ResultadoMissao.Sucesso
                         || resultado == IA03ResultadoMissao.Fracasso
                         || resultado == IA03ResultadoMissao.Expirada;
            bool cancelarOrdens = resultado == IA03ResultadoMissao.Sucesso
                                  || resultado == IA03ResultadoMissao.Fracasso
                                  || resultado == IA03ResultadoMissao.Expirada
                                  || resultado == IA03ResultadoMissao.Cancelada;

            if (missaoAtiva != null && nivelDeConflito != IA03NivelConflito.Paz && final)
            {
                missoesConcluidasNaCrise.Add(missaoAtiva);
                chavesMissoesConcluidasNaCrise.Add(CriarChaveEquivalencia(missaoAtiva));
            }

            if (missaoAtiva != null && resultado != IA03ResultadoMissao.EmAndamento)
            {
                string verbo = resultado == IA03ResultadoMissao.Sucesso ? "missão concluída: "
                    : resultado == IA03ResultadoMissao.Cancelada ? "missão cancelada: "
                    : resultado == IA03ResultadoMissao.Expirada ? "missão expirada: "
                    : "missão falhou: ";
                ultimaDecisao = verbo
                                + missaoAtiva.NomeMissao + " — " + (motivo ?? string.Empty);
            }

            if (cancelarOrdens && brain != null && brain.Context != null && brain.Context.CommandQueue != null
                && !string.IsNullOrWhiteSpace(idOrdemAtivaDaMissao))
            {
                brain.Context.CommandQueue.CancelPending(idOrdemAtivaDaMissao, Time.time, motivo);
                for (int i = 0; i < unidadesAtivasNaMissao.Count; i++)
                {
                    GameObject unidade = unidadesAtivasNaMissao[i];
                    ControleUnidade controle = unidade != null ? unidade.GetComponent<ControleUnidade>() : null;
                    if (controle != null)
                    {
                        controle.CancelarOrdemSePertenceA("IA03EstrategaNacional", idOrdemAtivaDaMissao);
                    }
                }
            }

            if (creatyAtivo != null && unidadesReservadas > 0)
            {
                creatyAtivo.LiberarReserva(unidadesReservadas);
            }

            creatyAtivo = null;
            missaoAtiva = null;
            equipeAlvoAtiva = 0;
            unidadesReservadas = 0;
            unidadesAtivasNaMissao.Clear();
            inicioMissaoEm = -1f;
            idOrdemAtivaDaMissao = string.Empty;
            estadoDaMissao = resultado;
            ResetarRastreamentoMissao();
            if (resultado == IA03ResultadoMissao.Cancelada && !string.IsNullOrWhiteSpace(motivo) && ativo)
            {
                ultimaDecisao = "missão cancelada: " + motivo;
            }
            RegistrarLogDebugSeNecessario();
        }

        private void ResetarRastreamentoMissao()
        {
            unidadesOriginaisNaMissao = 0;
            inicioPermanenciaNoDestinoEm = -1f;
            grupoChegouAoDestino = false;
            grupoSaiuDoDestino = false;
            grupoPerdeuUnidade = false;
            alvoMissaoFoiDefinido = false;
            alvoMissaoDestruido = false;
            territorioDoObjetivoCapturado = false;
            territorioDoObjetivoPerdido = false;
            confirmacaoExternaDeSucesso = false;
            confirmacaoExternaDeFracasso = false;
            motivoConfirmacaoExterna = string.Empty;
            idPersistenteAlvoMissao = string.Empty;
            alvoMissaoAtivo = null;
            unidadesComOrdemConcluidaNaMissao.Clear();
            unidadesPerdidasNaMissao.Clear();
        }

        private void AoConcluirOrdemMovimento(OrquestradorGlobalOrdens.Registro registro)
        {
            if (registro == null
                || missaoAtiva == null
                || string.IsNullOrWhiteSpace(idOrdemAtivaDaMissao)
                || !string.Equals(registro.Dono, "IA03EstrategaNacional", System.StringComparison.Ordinal)
                || !registro.Id.StartsWith(idOrdemAtivaDaMissao + ":", System.StringComparison.Ordinal))
            {
                return;
            }

            for (int i = 0; i < unidadesAtivasNaMissao.Count; i++)
            {
                GameObject unidade = unidadesAtivasNaMissao[i];
                if (unidade == null || unidade.GetInstanceID() != registro.UnidadeInstanceId)
                {
                    continue;
                }

                unidadesComOrdemConcluidaNaMissao.Add(registro.UnidadeInstanceId);
                SolicitarReanaliseAntecipada();
                return;
            }
        }

        private void AoMorrerUnidade(SistemaDeDanos alvo, GameObject agressor)
        {
            if (alvo == null || alvo.ehEstrutura || missaoAtiva == null || brain == null)
            {
                return;
            }

            IdentidadeUnidade identidadeAlvo = SistemaDeDanos.ResolverIdentidade(alvo);
            if (identidadeAlvo == null || identidadeAlvo.teamID != brain.TeamId)
            {
                return;
            }

            for (int i = 0; i < unidadesAtivasNaMissao.Count; i++)
            {
                GameObject unidade = unidadesAtivasNaMissao[i];
                if (unidade == null)
                {
                    continue;
                }

                IdentidadeUnidade identidadeUnidade = SistemaDeDanos.ResolverIdentidade(unidade.transform);
                if (identidadeUnidade != identidadeAlvo)
                {
                    continue;
                }

                unidadesPerdidasNaMissao.Add(unidade.GetInstanceID());
                grupoPerdeuUnidade = true;
                SolicitarReanaliseAntecipada();
                return;
            }
        }

        private int ContarInimigosConhecidos(IA_WorldState mundo)
        {
            if (mundo == null)
            {
                return 0;
            }

            int contagem = 0;
            for (int i = 0; i < mundo.VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation observacao = mundo.VisibleEnemies[i];
                if (observacao != null
                    && observacao.Transform != null
                    && (paisAlvoTeamId <= 0 || observacao.Transform.GetComponentInParent<IdentidadeUnidade>()?.teamID == paisAlvoTeamId))
                {
                    contagem++;
                }
            }

            return contagem;
        }

        private IA_EnemyObservation SelecionarAlvoObservado(IA03AlvoPreferencial preferencia, Vector3 ponto)
        {
            IA_WorldState mundo = brain != null && brain.Context != null ? brain.Context.WorldState : null;
            if (mundo == null)
            {
                return null;
            }

            IA_EnemyObservation melhor = null;
            float melhorPontuacao = float.NegativeInfinity;
            for (int i = 0; i < mundo.VisibleEnemies.Count; i++)
            {
                IA_EnemyObservation observacao = mundo.VisibleEnemies[i];
                if (observacao == null)
                {
                    continue;
                }

                IdentidadeUnidade identidade = observacao.Transform != null
                    ? observacao.Transform.GetComponentInParent<IdentidadeUnidade>()
                    : null;
                if (paisAlvoTeamId > 0 && identidade != null && identidade.teamID != paisAlvoTeamId)
                {
                    continue;
                }

                if (preferencia != IA03AlvoPreferencial.Nenhum && !CorrespondeAlvoPreferencial(observacao, preferencia))
                {
                    continue;
                }

                float distancia = Vector3.Distance(ponto, observacao.Position);
                float pontuacao = observacao.ThreatScore - distancia * 0.005f;
                if (pontuacao > melhorPontuacao)
                {
                    melhorPontuacao = pontuacao;
                    melhor = observacao;
                }
            }

            return melhor;
        }

        private static bool CorrespondeAlvoPreferencial(IA_EnemyObservation observacao, IA03AlvoPreferencial preferencia)
        {
            string nome = observacao.UnitName ?? string.Empty;
            switch (preferencia)
            {
                case IA03AlvoPreferencial.Aeroporto:
                    return observacao.IsStructure && (nome.IndexOf("aero", System.StringComparison.OrdinalIgnoreCase) >= 0
                                                       || nome.IndexOf("airport", System.StringComparison.OrdinalIgnoreCase) >= 0);
                case IA03AlvoPreferencial.Estaleiro:
                    return observacao.IsStructure && (nome.IndexOf("estaleiro", System.StringComparison.OrdinalIgnoreCase) >= 0
                                                       || nome.IndexOf("shipyard", System.StringComparison.OrdinalIgnoreCase) >= 0);
                case IA03AlvoPreferencial.Radar:
                    return nome.IndexOf("radar", System.StringComparison.OrdinalIgnoreCase) >= 0;
                case IA03AlvoPreferencial.BaseMilitar:
                    return observacao.IsStructure && (nome.IndexOf("base", System.StringComparison.OrdinalIgnoreCase) >= 0
                                                       || nome.IndexOf("militar", System.StringComparison.OrdinalIgnoreCase) >= 0);
                case IA03AlvoPreferencial.Infraestrutura:
                    return observacao.IsStructure;
                case IA03AlvoPreferencial.Cidade:
                    return observacao.IsStructure && (nome.IndexOf("cidade", System.StringComparison.OrdinalIgnoreCase) >= 0
                                                       || nome.IndexOf("city", System.StringComparison.OrdinalIgnoreCase) >= 0);
                case IA03AlvoPreferencial.DefesaAerea:
                    return observacao.IsStructure && (nome.IndexOf("antia", System.StringComparison.OrdinalIgnoreCase) >= 0
                                                       || nome.IndexOf("ciws", System.StringComparison.OrdinalIgnoreCase) >= 0
                                                       || nome.IndexOf("defesa", System.StringComparison.OrdinalIgnoreCase) >= 0);
                case IA03AlvoPreferencial.Navios:
                    return observacao.Domain == IA_Domain.Naval;
                case IA03AlvoPreferencial.Tropas:
                    return observacao.Domain == IA_Domain.Land && !observacao.IsStructure;
                default:
                    return true;
            }
        }

        private void AoRegistrarCombate(CartaCombateRegistro.EventoCombate evento)
        {
            if (evento == null || brain == null)
            {
                return;
            }

            if (paisAlvoTeamId > 0)
            {
                relatorio.RegistrarEventoCombate(evento, brain.TeamId, paisAlvoTeamId);

                if (evento.tipo == "UNIDADE DESTRUÍDA"
                    && evento.custoReposicaoConhecido
                    && evento.custoReposicaoEstimado > 0L)
                {
                    bool inimigoPerdeu = evento.equipeAlvo == paisAlvoTeamId
                        && evento.equipeAtacante == brain.TeamId;
                    bool proprioPaisPerdeu = evento.equipeAlvo == brain.TeamId
                        && evento.equipeAtacante == paisAlvoTeamId;
                    if (inimigoPerdeu)
                    {
                        relatorio.RegistrarPrejuizoEconomico(true, evento.custoReposicaoEstimado);
                    }
                    else if (proprioPaisPerdeu)
                    {
                        relatorio.RegistrarPrejuizoEconomico(false, evento.custoReposicaoEstimado);
                    }
                }
            }

            bool exigeDestruicaoDeAlvo = missaoAtiva != null
                && (missaoAtiva.CondicaoDeSucesso == IA03CondicaoMissao.DestruirAlvo
                    || missaoAtiva.CondicaoDeFracasso == IA03CondicaoMissao.DestruirAlvo);
            if (exigeDestruicaoDeAlvo
                && evento.tipo == "UNIDADE DESTRUÍDA"
                && evento.equipeAtacante == brain.TeamId
                && evento.equipeAlvo == equipeAlvoAtiva
                && (string.Equals(evento.idAlvo, idPersistenteAlvoMissao, System.StringComparison.Ordinal)
                    || (string.IsNullOrWhiteSpace(evento.idAlvo)
                        && alvoMissaoFoiDefinido
                        && alvoMissaoAtivo != null
                        && string.Equals(evento.alvo, alvoMissaoAtivo.name, System.StringComparison.Ordinal))))
            {
                alvoMissaoDestruido = true;
                SolicitarReanaliseAntecipada();
            }
        }

        private void AoReceberDanoGlobal(SistemaDeDanos alvo, GameObject agressor, float dano)
        {
            if (alvo == null || !alvo.ehEstrutura || brain == null || dano <= 0f)
            {
                return;
            }

            IdentidadeUnidade identidadeAlvo = SistemaDeDanos.ResolverIdentidade(alvo);
            IdentidadeUnidade identidadeAgressor = agressor != null
                ? agressor.GetComponentInParent<IdentidadeUnidade>()
                : null;
            if (identidadeAlvo == null || identidadeAgressor == null
                || identidadeAgressor.teamID <= 0 || identidadeAgressor.teamID == identidadeAlvo.teamID)
            {
                return;
            }

            if (identidadeAgressor.teamID == brain.TeamId && identidadeAlvo.teamID > 0)
            {
                if (paisAlvoTeamId <= 0)
                {
                    paisAlvoTeamId = identidadeAlvo.teamID;
                }

                if (identidadeAlvo.teamID == paisAlvoTeamId)
                {
                    relatorio.RegistrarDanoEstrutural(true, dano);
                }
            }

            if (identidadeAlvo.teamID == brain.TeamId)
            {
                if (paisAlvoTeamId <= 0)
                {
                    paisAlvoTeamId = identidadeAgressor.teamID;
                }

                if (identidadeAgressor.teamID != paisAlvoTeamId)
                {
                    return;
                }

                relatorio.RegistrarDanoEstrutural(false, dano);
                if (nivelDeConflito == IA03NivelConflito.Paz)
                {
                    DefinirNivelConflito(IA03NivelConflito.Tensao, "estrutura nacional sob ataque");
                }

                MissaoEstrategicaSO defesa = SelecionarMissaoDeDefesa(alvo.name);
                if (defesa != null)
                {
                    SolicitarMissaoEstrategica(defesa, "ataque a " + alvo.name, true);
                }
            }

            SolicitarReanaliseAntecipada();
        }

        private void GarantirAssinaturaTerritorial()
        {
            GerenteDeTerritorio atual = GerenteDeTerritorio.Instancia;
            if (gerenteTerritorialAssinado == atual)
            {
                return;
            }

            if (gerenteTerritorialAssinado != null)
            {
                gerenteTerritorialAssinado.OnTerritoryOwnerChanged -= AoMudarDonoTerritorial;
            }

            gerenteTerritorialAssinado = atual;
            if (gerenteTerritorialAssinado != null)
            {
                gerenteTerritorialAssinado.OnTerritoryOwnerChanged += AoMudarDonoTerritorial;
            }
        }

        private void AoMudarDonoTerritorial(string territorioId, int donoAnterior, int novoDono)
        {
            if (brain == null || string.IsNullOrWhiteSpace(territorioId))
            {
                return;
            }

            if (paisAlvoTeamId > 0 && donoAnterior == paisAlvoTeamId && novoDono == brain.TeamId)
            {
                relatorio.RegistrarObjetivoCapturado(true);
            }
            else if (paisAlvoTeamId > 0 && donoAnterior == brain.TeamId && novoDono == paisAlvoTeamId)
            {
                relatorio.RegistrarObjetivoCapturado(false);
            }

            bool exigeCapturaTerritorial = missaoAtiva != null
                && (missaoAtiva.CondicaoDeSucesso == IA03CondicaoMissao.CapturarTerritorio
                    || missaoAtiva.CondicaoDeFracasso == IA03CondicaoMissao.CapturarTerritorio);
            if (!exigeCapturaTerritorial || creatyAtivo == null || gerenteTerritorialAssinado == null)
            {
                return;
            }

            ResultadoConsultaTerritorio objetivo = gerenteTerritorialAssinado.ObterTerritorioNaPosicao(creatyAtivo.transform.position);
            if (!objetivo.encontrouRegiao || !string.Equals(objetivo.territorioId, territorioId, System.StringComparison.Ordinal))
            {
                return;
            }

            if (donoAnterior == equipeAlvoAtiva && novoDono == brain.TeamId)
            {
                territorioDoObjetivoCapturado = true;
                territorioDoObjetivoPerdido = false;
            }
            else if (donoAnterior == brain.TeamId && novoDono == equipeAlvoAtiva)
            {
                territorioDoObjetivoCapturado = false;
                territorioDoObjetivoPerdido = true;
            }

            SolicitarReanaliseAntecipada();
        }

        private static string ObterIdPersistenteAlvo(Transform alvo)
        {
            if (alvo == null)
            {
                return string.Empty;
            }

            IdentidadeUnidade identidade = SistemaDeDanos.ResolverIdentidade(alvo);
            GameObject objeto = identidade != null ? identidade.gameObject : alvo.gameObject;
            SaveableEntity salvo = objeto.GetComponent<SaveableEntity>();
            return salvo != null && !string.IsNullOrWhiteSpace(salvo.UniqueId)
                ? salvo.UniqueId
                : "runtime-" + objeto.GetInstanceID();
        }

        private MissaoEstrategicaSO SelecionarMissaoDeDefesa(string nomeEstrutura)
        {
            MissaoEstrategicaSO melhor = null;
            if (missoesEstrategicas == null)
            {
                return null;
            }

            for (int i = 0; i < missoesEstrategicas.Count; i++)
            {
                MissaoEstrategicaSO missao = missoesEstrategicas[i];
                if (missao == null || missao.TipoMissao != IA03TipoMissao.DefesaDeObjetivo
                    || !missao.Aceita(nivelDeConflito, MissaoDefaultCreaty(missao))
                    || !NomeCompativelComAlvo(nomeEstrutura, missao.Alvo))
                {
                    continue;
                }

                if (melhor == null || missao.Prioridade > melhor.Prioridade)
                {
                    melhor = missao;
                }
            }

            return melhor;
        }

        private static bool NomeCompativelComAlvo(string nome, IA03AlvoPreferencial preferencia)
        {
            string valor = nome ?? string.Empty;
            switch (preferencia)
            {
                case IA03AlvoPreferencial.Aeroporto:
                    return valor.IndexOf("aero", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || valor.IndexOf("airport", System.StringComparison.OrdinalIgnoreCase) >= 0;
                case IA03AlvoPreferencial.Estaleiro:
                    return valor.IndexOf("estaleiro", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || valor.IndexOf("shipyard", System.StringComparison.OrdinalIgnoreCase) >= 0;
                case IA03AlvoPreferencial.Radar:
                    return valor.IndexOf("radar", System.StringComparison.OrdinalIgnoreCase) >= 0;
                case IA03AlvoPreferencial.BaseMilitar:
                    return valor.IndexOf("base", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || valor.IndexOf("militar", System.StringComparison.OrdinalIgnoreCase) >= 0;
                case IA03AlvoPreferencial.DefesaAerea:
                    return valor.IndexOf("antia", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || valor.IndexOf("ciws", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || valor.IndexOf("defesa", System.StringComparison.OrdinalIgnoreCase) >= 0;
                case IA03AlvoPreferencial.Nenhum:
                case IA03AlvoPreferencial.Infraestrutura:
                    return true;
                default:
                    return false;
            }
        }

        private void AoAtualizarGoverno()
        {
            // O BrainMaster já consome o serviço. O evento apenas deixa explícito
            // que a próxima fatia estratégica deve conferir a relação em cache.
            proximaDecisaoDeMissaoEm = Mathf.Min(proximaDecisaoDeMissaoEm, Time.time);
            SolicitarReanaliseAntecipada();
        }

        private void VincularEventosGoverno(SistemaGovernoMundial governo)
        {
            if (governoAssinado == governo)
            {
                return;
            }

            if (governoAssinado != null)
            {
                governoAssinado.OnGovernoAtualizado -= AoAtualizarGoverno;
            }

            governoAssinado = governo;
            if (governoAssinado != null)
            {
                governoAssinado.OnGovernoAtualizado += AoAtualizarGoverno;
            }
        }

        private void AoAbaterPresidente(int vitimaTeamId, int agressorTeamId, string nomePresidente)
        {
            if (brain == null || vitimaTeamId != brain.TeamId)
            {
                return;
            }

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            diplomacia.RegistrarPresidenteAbatido(governo, vitimaTeamId, agressorTeamId, nomePresidente);
            if (agressorTeamId > 0)
            {
                paisAlvoTeamId = agressorTeamId;
            }
            DefinirNivelConflito(perfilPais != null && perfilPais.Agressividade >= 0.75f
                ? IA03NivelConflito.GuerraTotal
                : IA03NivelConflito.ConflitoLimitado,
                "EventoPresidenteAbatido");
            SolicitarReanaliseAntecipada();
        }

        private long ObterSaldoAlvo()
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno alvo = governo != null ? governo.ObterPais(paisAlvoTeamId) : null;
            return alvo != null ? System.Math.Max(1L, alvo.saldo) : 1L;
        }

        private IA03DominioEstrategico ResolverDominioPreferido()
        {
            if (perfilPais.PreferenciaNaval >= perfilPais.PreferenciaAerea
                && perfilPais.PreferenciaNaval >= perfilPais.PreferenciaTerrestre)
            {
                return IA03DominioEstrategico.Naval;
            }
            if (perfilPais.PreferenciaAerea >= perfilPais.PreferenciaTerrestre)
            {
                return IA03DominioEstrategico.Aereo;
            }
            return IA03DominioEstrategico.Terrestre;
        }

        private IA03TipoCreaty ResolverTipoCreaty(IA03DominioEstrategico dominio, IA03NivelConflito nivel)
        {
            switch (nivel)
            {
                case IA03NivelConflito.Tensao:
                    return dominio == IA03DominioEstrategico.Naval ? IA03TipoCreaty.PatrulhaNaval
                        : dominio == IA03DominioEstrategico.Aereo ? IA03TipoCreaty.PatrulhaAerea
                        : IA03TipoCreaty.PatrulhaTerrestre;
                case IA03NivelConflito.AvancoMilitar: return IA03TipoCreaty.AvancoN3;
                case IA03NivelConflito.ConflitoLimitado: return IA03TipoCreaty.ConflitoN2;
                case IA03NivelConflito.GuerraTotal: return IA03TipoCreaty.GuerraN1;
                default: return IA03TipoCreaty.PontoGenerico;
            }
        }

        private IA03TipoCreaty MissaoDefaultCreaty(MissaoEstrategicaSO missao)
        {
            if (missao != null && missao.CreatysPermitidos != null && missao.CreatysPermitidos.Count > 0)
            {
                for (int i = 0; i < missao.CreatysPermitidos.Count; i++)
                {
                    IA03TipoCreaty tipo = missao.CreatysPermitidos[i];
                    if (missao.Aceita(nivelDeConflito, tipo))
                    {
                        return tipo;
                    }
                }

                return missao.CreatysPermitidos[0];
            }
            return ResolverTipoCreaty(missao != null ? missao.Dominio : ResolverDominioPreferido(), nivelDeConflito);
        }

        private bool MissaoFoiConcluidaNaCrise(MissaoEstrategicaSO missao)
        {
            return missao != null
                   && (missoesConcluidasNaCrise.Contains(missao)
                       || chavesMissoesConcluidasNaCrise.Contains(CriarChaveEquivalencia(missao)));
        }

        private bool ContemMissaoEquivalenteNaFila(MissaoEstrategicaSO missao)
        {
            if (missao == null)
            {
                return false;
            }

            for (int i = 0; i < filaMissoes.Count; i++)
            {
                if (SaoMissoesEquivalentes(filaMissoes[i], missao))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SaoMissoesEquivalentes(MissaoEstrategicaSO a, MissaoEstrategicaSO b)
        {
            return a != null && b != null
                   && string.Equals(CriarChaveEquivalencia(a), CriarChaveEquivalencia(b), System.StringComparison.Ordinal);
        }

        private static string CriarChaveEquivalencia(MissaoEstrategicaSO missao)
        {
            if (missao == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(missao.IdMissao))
            {
                return "id:" + missao.IdMissao.Trim();
            }

            return missao.TipoMissao + "|" + missao.Dominio + "|" + missao.Alvo + "|"
                   + missao.TipoOrdem + "|" + (missao.NomeMissao ?? string.Empty).Trim().ToUpperInvariant();
        }

        private void SolicitarReanaliseAntecipada()
        {
            if (Time.time < proximaReanaliseAntecipadaEm
                || brain == null || brain.Context == null || brain.Context.Scheduler == null)
            {
                return;
            }

            proximaReanaliseAntecipadaEm = Time.time + 0.5f;
            brain.Context.Scheduler.RequestEarlierTick(this, Time.time);
        }

        private void RegistrarLogDebugSeNecessario()
        {
            if (!debugIA03 || string.IsNullOrEmpty(ultimaDecisao)
                || string.Equals(ultimaLinhaDeDebug, ultimaDecisao, System.StringComparison.Ordinal))
            {
                return;
            }

            ultimaLinhaDeDebug = ultimaDecisao;
            string pais = brain != null && SistemaGovernoMundial.Instancia != null
                ? SistemaGovernoMundial.Instancia.NomePais(brain.TeamId)
                : brain != null ? "Team " + brain.TeamId : "país não configurado";
            Debug.Log("[IA03][" + pais + "] N" + (int)nivelDeConflito
                      + " | missão=" + (string.IsNullOrEmpty(ultimaMissao) ? "nenhuma" : ultimaMissao)
                      + " | grupo=" + unidadesAtivasNaMissao.Count
                      + " | " + ultimaDecisao, this);
        }

        public string DebugNomePais => brain != null && SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.NomePais(brain.TeamId)
            : brain != null ? "Team " + brain.TeamId : "não configurado";
        public int DebugTeamId => brain != null ? brain.TeamId : 0;
        public int DebugPaisAlvoTeamId => paisAlvoTeamId;
        public int DebugMissoesNaFila => filaMissoes.Count;
        public int DebugUnidadesRegistradas => brain != null && brain.Context != null && brain.Context.WorldState != null
            ? brain.Context.WorldState.OwnCombatUnits.Count
            : 0;
        public long DebugSaldo => economiaAtual.Saldo;

#if UNITY_EDITOR
        public void DebugForcarNivel(IA03NivelConflito nivel)
        {
            nivelDebugForcado = true;
            DefinirNivelConflito(nivel, "nível forçado pelo painel de debug");
            SolicitarReanaliseAntecipada();
        }

        public void DebugRetomarDiplomacia()
        {
            nivelDebugForcado = false;
            ultimaDecisao = "retomando o estado diplomático real";
            SolicitarReanaliseAntecipada();
            RegistrarLogDebugSeNecessario();
        }
#endif

        private static IA03EstadoNacional ConverterEstado(IA03NivelConflito nivel)
        {
            switch (nivel)
            {
                case IA03NivelConflito.Tensao: return IA03EstadoNacional.Tensao;
                case IA03NivelConflito.AvancoMilitar: return IA03EstadoNacional.Mobilizacao;
                case IA03NivelConflito.ConflitoLimitado: return IA03EstadoNacional.ConflitoLimitado;
                case IA03NivelConflito.GuerraTotal: return IA03EstadoNacional.GuerraTotal;
                default: return IA03EstadoNacional.Paz;
            }
        }
    }
}
