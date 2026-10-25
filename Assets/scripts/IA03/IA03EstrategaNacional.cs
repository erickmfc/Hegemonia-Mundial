using System.Collections.Generic;
using System.Linq;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;
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

        [System.Serializable]
        private sealed class ReposicaoPendente
        {
            public string itemKey = string.Empty;
            public int quantidade;
            public string productionOrderId = string.Empty;
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
        [SerializeField] private List<ReposicaoPendente> reposicoesPendentes = new List<ReposicaoPendente>();

        private readonly IA03RelatorioConflito relatorio = new IA03RelatorioConflito();
        private readonly GestorDiplomaciaIA03 diplomacia = new GestorDiplomaciaIA03();
        private readonly GestorEconomiaIA03 economia = new GestorEconomiaIA03();
        private readonly List<CreatyEstrategico> candidatos = new List<CreatyEstrategico>(32);
        private readonly List<IA03TipoCreaty> tiposCreatyCandidatos = new List<IA03TipoCreaty>(8);
        private readonly List<GameObject> unidadesCandidatas = new List<GameObject>(48);
        private readonly List<GameObject> unidadesAtivasNaMissao = new List<GameObject>(32);
        private readonly List<IA_EnemyObservation> observacoesInimigasParaRelatorio = new List<IA_EnemyObservation>(128);
        private readonly List<CandidatoGrupo> candidatosGrupo = new List<CandidatoGrupo>(128);
        private readonly List<MissaoEstrategicaSO> filaMissoes = new List<MissaoEstrategicaSO>(32);
        private readonly HashSet<MissaoEstrategicaSO> missoesConcluidasNaCrise = new HashSet<MissaoEstrategicaSO>();
        private readonly HashSet<string> chavesMissoesConcluidasNaCrise = new HashSet<string>();
        private readonly HashSet<int> unidadesComOrdemConcluidaNaMissao = new HashSet<int>();
        private readonly HashSet<int> unidadesPerdidasNaMissao = new HashSet<int>();
        private bool reposicoesReconstruidasDoRegistro;
        private bool restauracaoProducaoPendente;
        private bool estadoMissaoSalvoAplicado;
        private bool ordemDaMissaoRestaurada;
        private GestorEconomiaIA03.Resumo economiaAtual;

        private IA_BrainMaster brain;
        private SistemaGovernoMundial governoAssinado;
        private SistemaGovernoMundial governoReservasEstrategicas;
        private int governoReservasEstrategicasTeamId;
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
        private float proximaAvaliacaoReivindicacaoTerritorialEm;
        private float proximaAvaliacaoVotosConselhoEm;
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

        public IA03RelatorioSaveData CapturarEstadoRelatorioSalvavel()
        {
            return relatorio.CapturarEstadoSalvavel();
        }

        public void RestaurarEstadoRelatorioSalvavel(IA03RelatorioSaveData estado)
        {
            relatorio.RestaurarEstadoSalvavel(estado);
        }

        public SaveIA03MissionStateData CapturarEstadoMissaoSalvavel()
        {
            if (brain == null)
            {
                brain = GetComponent<IA_BrainMaster>();
            }

            SaveIA03MissionStateData estado = new SaveIA03MissionStateData
            {
                teamID = brain != null ? brain.TeamId : 0,
                equipeAlvoAtiva = equipeAlvoAtiva,
                creatyAtivoId = creatyAtivo != null ? creatyAtivo.Id : string.Empty,
                alvoPersistenteId = idPersistenteAlvoMissao ?? string.Empty,
                idOrdemAtiva = idOrdemAtivaDaMissao ?? string.Empty,
                estadoMissao = (int)estadoDaMissao,
                tempoDecorridoMissao = inicioMissaoEm >= 0f ? Mathf.Max(0f, Time.time - inicioMissaoEm) : 0f,
                tempoPermanenciaNoDestino = inicioPermanenciaNoDestinoEm >= 0f
                    ? Mathf.Max(0f, Time.time - inicioPermanenciaNoDestinoEm)
                    : -1f,
                unidadesReservadas = Mathf.Max(0, unidadesReservadas),
                unidadesOriginaisNaMissao = Mathf.Max(0, unidadesOriginaisNaMissao),
                grupoChegouAoDestino = grupoChegouAoDestino,
                grupoSaiuDoDestino = grupoSaiuDoDestino,
                grupoPerdeuUnidade = grupoPerdeuUnidade,
                alvoMissaoFoiDefinido = alvoMissaoFoiDefinido,
                alvoMissaoDestruido = alvoMissaoDestruido,
                territorioDoObjetivoCapturado = territorioDoObjetivoCapturado,
                territorioDoObjetivoPerdido = territorioDoObjetivoPerdido,
                confirmacaoExternaDeSucesso = confirmacaoExternaDeSucesso,
                confirmacaoExternaDeFracasso = confirmacaoExternaDeFracasso,
                motivoConfirmacaoExterna = motivoConfirmacaoExterna ?? string.Empty
            };

            if (missaoAtiva != null)
            {
                estado.missaoAtivaId = missaoAtiva.IdMissao ?? string.Empty;
                estado.missaoAtivaKey = CriarChaveEquivalencia(missaoAtiva);
                estado.objetivoEstrategico = missaoAtiva.Objetivo ?? string.Empty;
                estado.nomeMissao = missaoAtiva.NomeMissao ?? string.Empty;
            }

            if (alvoMissaoAtivo != null)
            {
                estado.alvoPersistenteId = ObterIdPersistenteAlvo(alvoMissaoAtivo);
            }

            if (unidadesAtivasNaMissao != null)
            {
                for (int i = 0; i < unidadesAtivasNaMissao.Count; i++)
                {
                    GameObject unidade = unidadesAtivasNaMissao[i];
                    SaveableEntity saveable = unidade != null ? SaveableEntity.Garantir(unidade) : null;
                    if (saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId)
                        && !estado.unidadesAtivasIds.Contains(saveable.UniqueId))
                    {
                        estado.unidadesAtivasIds.Add(saveable.UniqueId);
                    }
                }
            }

            if (filaMissoes != null)
            {
                for (int i = 0; i < filaMissoes.Count; i++)
                {
                    string key = CriarChaveEquivalencia(filaMissoes[i]);
                    if (!string.IsNullOrWhiteSpace(key) && !estado.missoesPendentesKeys.Contains(key))
                    {
                        estado.missoesPendentesKeys.Add(key);
                    }
                }
            }

            foreach (string key in chavesMissoesConcluidasNaCrise)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    estado.missoesConcluidasKeys.Add(key);
                }
            }

            return estado;
        }

        public bool RestaurarEstadoMissaoSalvavel(SaveIA03MissionStateData estado)
        {
            if (estado == null || estadoMissaoSalvoAplicado)
            {
                return false;
            }

            if (brain == null)
            {
                brain = GetComponent<IA_BrainMaster>();
            }

            if (brain == null || estado.teamID <= 0 || brain.TeamId != estado.teamID)
            {
                return false;
            }

            // O método pode ser chamado novamente por fluxos de recuperação do save.
            // Aplicar o snapshot uma única vez impede reservas ou ordens duplicadas.
            estadoMissaoSalvoAplicado = true;
            equipeAlvoAtiva = Mathf.Max(0, estado.equipeAlvoAtiva);
            idOrdemAtivaDaMissao = estado.idOrdemAtiva ?? string.Empty;
            ordemDaMissaoRestaurada = !string.IsNullOrWhiteSpace(idOrdemAtivaDaMissao)
                && estado.estadoMissao == (int)IA03ResultadoMissao.EmAndamento;
            idPersistenteAlvoMissao = estado.alvoPersistenteId ?? string.Empty;
            estadoDaMissao = System.Enum.IsDefined(typeof(IA03ResultadoMissao), estado.estadoMissao)
                ? (IA03ResultadoMissao)estado.estadoMissao
                : IA03ResultadoMissao.Pendente;
            unidadesOriginaisNaMissao = Mathf.Max(0, estado.unidadesOriginaisNaMissao);
            grupoChegouAoDestino = estado.grupoChegouAoDestino;
            grupoSaiuDoDestino = estado.grupoSaiuDoDestino;
            grupoPerdeuUnidade = estado.grupoPerdeuUnidade;
            alvoMissaoFoiDefinido = estado.alvoMissaoFoiDefinido;
            alvoMissaoDestruido = estado.alvoMissaoDestruido;
            territorioDoObjetivoCapturado = estado.territorioDoObjetivoCapturado;
            territorioDoObjetivoPerdido = estado.territorioDoObjetivoPerdido;
            confirmacaoExternaDeSucesso = estado.confirmacaoExternaDeSucesso;
            confirmacaoExternaDeFracasso = estado.confirmacaoExternaDeFracasso;
            motivoConfirmacaoExterna = estado.motivoConfirmacaoExterna ?? string.Empty;
            inicioMissaoEm = estado.estadoMissao == (int)IA03ResultadoMissao.EmAndamento
                ? Time.time - Mathf.Max(0f, estado.tempoDecorridoMissao)
                : -1f;
            inicioPermanenciaNoDestinoEm = estado.tempoPermanenciaNoDestino >= 0f
                ? Time.time - estado.tempoPermanenciaNoDestino
                : -1f;

            missaoAtiva = ResolverMissaoPorChave(estado.missaoAtivaKey);
            if (!string.IsNullOrWhiteSpace(estado.missaoAtivaKey) && missaoAtiva == null)
            {
                Debug.LogWarning("[IA03] Missão salva não encontrada na configuração do team " + brain.TeamId
                    + ": " + estado.missaoAtivaKey + ". A ordem persistida não será reenviada.", this);
                idOrdemAtivaDaMissao = string.Empty;
            }

            filaMissoes.Clear();
            if (estado.missoesPendentesKeys != null)
            {
                for (int i = 0; i < estado.missoesPendentesKeys.Count; i++)
                {
                    MissaoEstrategicaSO pendente = ResolverMissaoPorChave(estado.missoesPendentesKeys[i]);
                    if (pendente != null && !filaMissoes.Contains(pendente)
                        && !SaoMissoesEquivalentes(pendente, missaoAtiva))
                    {
                        filaMissoes.Add(pendente);
                    }
                }
            }

            chavesMissoesConcluidasNaCrise.Clear();
            missoesConcluidasNaCrise.Clear();
            if (estado.missoesConcluidasKeys != null)
            {
                for (int i = 0; i < estado.missoesConcluidasKeys.Count; i++)
                {
                    string key = estado.missoesConcluidasKeys[i];
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    chavesMissoesConcluidasNaCrise.Add(key);
                    MissaoEstrategicaSO concluida = ResolverMissaoPorChave(key);
                    if (concluida != null) missoesConcluidasNaCrise.Add(concluida);
                }
            }

            unidadesAtivasNaMissao.Clear();
            Dictionary<string, SaveableEntity> saveables = new Dictionary<string, SaveableEntity>(System.StringComparer.Ordinal);
            SaveableEntity[] encontrados = IA_UnitySearch.FindAll<SaveableEntity>();
            for (int i = 0; encontrados != null && i < encontrados.Length; i++)
            {
                SaveableEntity candidato = encontrados[i];
                if (candidato != null && !string.IsNullOrWhiteSpace(candidato.UniqueId))
                {
                    saveables[candidato.UniqueId] = candidato;
                }
            }

            if (estado.unidadesAtivasIds != null)
            {
                for (int i = 0; i < estado.unidadesAtivasIds.Count; i++)
                {
                    string id = estado.unidadesAtivasIds[i];
                    if (!string.IsNullOrWhiteSpace(id) && saveables.TryGetValue(id, out SaveableEntity saveable)
                        && saveable != null && !unidadesAtivasNaMissao.Contains(saveable.gameObject))
                    {
                        unidadesAtivasNaMissao.Add(saveable.gameObject);
                    }
                }
            }

            alvoMissaoAtivo = null;
            if (!string.IsNullOrWhiteSpace(idPersistenteAlvoMissao)
                && saveables.TryGetValue(idPersistenteAlvoMissao, out SaveableEntity alvoSaveable)
                && alvoSaveable != null)
            {
                alvoMissaoAtivo = alvoSaveable.transform;
            }

            creatyAtivo = null;
            unidadesReservadas = 0;
            if (!string.IsNullOrWhiteSpace(estado.creatyAtivoId)
                && RegistroCreatysEstrategicos.TryGetPorId(estado.creatyAtivoId, out CreatyEstrategico creaty)
                && creaty != null)
            {
                creatyAtivo = creaty;
                int reserva = Mathf.Min(Mathf.Max(0, estado.unidadesReservadas), unidadesAtivasNaMissao.Count);
                if (reserva > 0 && creaty.TentarReservar(brain.TeamId, equipeAlvoAtiva, reserva))
                {
                    unidadesReservadas = reserva;
                }
            }

            if (missaoAtiva != null && estado.estadoMissao == (int)IA03ResultadoMissao.EmAndamento
                && creatyAtivo == null)
            {
                // A missão continua identificada, mas sem alvo operacional não se emite
                // uma ordem substituta que possa duplicar a operação salva.
                ultimaDecisao = "missão restaurada aguardando o Creaty " + estado.creatyAtivoId;
            }

            return true;
        }

        private MissaoEstrategicaSO ResolverMissaoPorChave(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || missoesEstrategicas == null)
            {
                return null;
            }

            for (int i = 0; i < missoesEstrategicas.Count; i++)
            {
                MissaoEstrategicaSO candidata = missoesEstrategicas[i];
                if (candidata != null && string.Equals(
                    CriarChaveEquivalencia(candidata), key, System.StringComparison.Ordinal))
                {
                    return candidata;
                }
            }

            return null;
        }

        public bool IsUnitReservedForActiveMission(GameObject unit)
        {
            if (!ativo || !isActiveAndEnabled || unit == null || missaoAtiva == null)
            {
                return false;
            }

            for (int i = 0; i < unidadesAtivasNaMissao.Count; i++)
            {
                if (unidadesAtivasNaMissao[i] == unit)
                {
                    return true;
                }
            }

            return false;
        }

        public static float CalcularAtrasoInicialEscalonado(int teamId)
        {
            int indice = Mathf.Max(0, teamId - 1) % 15;
            return indice * 0.5f;
        }

        public static void NotificarRestauracaoProducaoAutomatica()
        {
            IA03EstrategaNacional[] estrategistas =
                UnityEngine.Object.FindObjectsByType<IA03EstrategaNacional>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < estrategistas.Length; i++)
            {
                IA03EstrategaNacional estrategista = estrategistas[i];
                if (estrategista != null)
                {
                    estrategista.restauracaoProducaoPendente = true;
                    estrategista.ReconstruirReposicoesDoRegistroSeNecessario();
                }
            }
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
            LiberarReservasEstrategicas();
            if (governoAssinado != null)
            {
                governoAssinado.OnGovernoAtualizado -= AoAtualizarGoverno;
                governoAssinado.OnPropostaCriada -= AoCriarPropostaInternacional;
                governoAssinado = null;
            }
            IA03MarcaPresidencial.PresidenteAbatido -= AoAbaterPresidente;
            SistemaDeDanos.OnDanoGlobal -= AoReceberDanoGlobal;
            SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
            OrquestradorGlobalOrdens.OrdemConcluida -= AoConcluirOrdemMovimento;
            if (gerenteTerritorialAssinado != null)
            {
                gerenteTerritorialAssinado.OnTerritoryCapturedByOccupation -= AoCapturarTerritorioPorOcupacao;
                gerenteTerritorialAssinado.OnTerritoryOccupationChanged -= AoMudarOcupacaoTerritorial;
                gerenteTerritorialAssinado = null;
            }
            EncerrarMissao("IA03 desativada");
            CancelarReposicoesPendentes();
        }

        public void Tick(float now, float deltaTime)
        {
            if (SistemaSaveGame.RestauracaoDoMundoEmAndamento)
            {
                return;
            }

            if (!ativo || !isActiveAndEnabled)
            {
                if (missaoAtiva != null)
                {
                    EncerrarMissao("IA03 desativada");
                }

                // Alterar o toggle serializado nao dispara OnDisable. Limpe os
                // recursos retidos pela IA03 para nao bloquear economia/producao.
                CancelarReposicoesPendentes();
                LiberarReservasEstrategicas();

                return;
            }

            if (perfilPais == null)
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

            if (ordemDaMissaoRestaurada)
            {
                brain.Context.CommandQueue.RestoreInFlight(idOrdemAtivaDaMissao);
                ordemDaMissaoRestaurada = false;
            }

            if (restauracaoProducaoPendente)
            {
                ReconstruirReposicoesDoRegistroSeNecessario();
            }

            ultimaAnaliseEm = now;

            GarantirAssinaturaTerritorial();

            if (!perfilAplicado)
            {
                AplicarPerfilNacional();
            }

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            VincularEventosGoverno(governo);
            AvaliarVotosConselho(governo, now);
            DadosPaisGoverno pais = governo != null ? governo.ObterPais(brain.TeamId) : null;
            if (paisAlvoTeamId <= 0 && pais != null && pais.rivalTeamId > 0)
            {
                paisAlvoTeamId = pais.rivalTeamId;
            }

            AvaliarReivindicacoesTerritoriais(governo, pais, now);

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
            string motivoFinanciamento;
            if (economia.TentarSolicitarFinanciamento(
                    governo,
                    brain.TeamId,
                    now,
                    ref economiaAtual,
                    out motivoFinanciamento))
            {
                ultimaDecisao = motivoFinanciamento;
            }
            AtualizarPosturaBrainMaster();
            AvaliarPrazosDeGuerra(governo, now);
            ProcessarReposicoesPendentes(now);

            if (missaoAtiva != null)
            {
                ProcessarMissaoAtiva(now);
            }
            else if (estadoNacional != IA03EstadoNacional.Negociando && now >= proximaDecisaoDeMissaoEm)
            {
                proximaDecisaoDeMissaoEm = now + Interval;
                TentarIniciarMissao(now);
            }

            AtualizarRelatorio(governo, now);
            RegistrarLogDebugSeNecessario();
        }

        public bool GarantirReservasEstrategicas()
        {
            if (!ativo || perfilPais == null)
            {
                LiberarReservasEstrategicas();
                return false;
            }

            if (brain == null)
            {
                brain = GetComponent<IA_BrainMaster>();
            }

            if (brain == null)
            {
                return false;
            }

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null)
            {
                return false;
            }

            int teamId = brain.TeamId;
            bool mudouProprietario = governoReservasEstrategicas != governo
                                      || governoReservasEstrategicasTeamId != teamId;
            if (mudouProprietario)
            {
                LiberarReservasEstrategicas();
                governoReservasEstrategicas = governo;
                governoReservasEstrategicasTeamId = teamId;
            }

            DadosPaisGoverno pais = governo.ObterPais(teamId);
            if (mudouProprietario)
            {
                AplicarReservasEstrategicas(governo, pais, teamId);
            }
            else
            {
                AplicarReservaManpowerEstrategica(governo, pais, teamId);
            }
            return true;
        }

        private void AplicarReservasEstrategicas(
            SistemaGovernoMundial governo,
            DadosPaisGoverno pais,
            int teamId)
        {
            if (governo == null || perfilPais == null || teamId <= 0)
            {
                return;
            }

            governo.DefinirReservaFinanceiraEstrategica(
                teamId,
                Mathf.CeilToInt(Mathf.Max(0f, perfilPais.ReservaFinanceiraDesejada)));
            AplicarReservaManpowerEstrategica(governo, pais, teamId);
        }

        private void AplicarReservaManpowerEstrategica(
            SistemaGovernoMundial governo,
            DadosPaisGoverno pais,
            int teamId)
        {
            if (governo == null || perfilPais == null || teamId <= 0)
            {
                return;
            }

            long reservaDesejada = CalcularReservaManpowerEstrategica(
                pais,
                perfilPais.ReservaManpowerMinimaPercentualDaPopulacao);
            if (governo.ObterReservaManpowerEstrategica(teamId) != reservaDesejada)
            {
                governo.DefinirReservaManpowerEstrategica(teamId, reservaDesejada);
            }
        }

        private void LiberarReservasEstrategicas()
        {
            if (governoReservasEstrategicas == null || governoReservasEstrategicasTeamId <= 0)
            {
                return;
            }

            governoReservasEstrategicas.LiberarReservaFinanceiraEstrategica(governoReservasEstrategicasTeamId);
            governoReservasEstrategicas.LiberarReservaManpowerEstrategica(governoReservasEstrategicasTeamId);
            governoReservasEstrategicas = null;
            governoReservasEstrategicasTeamId = 0;
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
                CancelarReposicoesPendentes();
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

            // O perfil nacional define o piso da prontidão, mas não deve
            // apagar metas maiores já calculadas pela DEUSA, pelo déficit de
            // capacidade ou pela estimativa de ameaça do BrainMaster.
            brain.TargetFleet = Mathf.Max(brain.TargetFleet, fleetGoal);
            brain.TargetAircraft = Mathf.Max(brain.TargetAircraft, airGoal);
            brain.TargetOilTankers = Mathf.Max(
                brain.TargetOilTankers,
                perfilPais.NivelEconomico >= IA03NivelEconomico.Forte ? 2 : 1);
            brain.TargetCoastalDefenseShips = Mathf.Max(
                brain.TargetCoastalDefenseShips,
                Mathf.Max(1, perfilPais.MinimoNaviosEscolta));
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

            int afinidadeEstrategica = relacao.ValorDiplomaticoEstrategico;
            if (nivelDeConflito == IA03NivelConflito.Paz && now >= ignorarTensaoAte && afinidadeEstrategica <= -55)
            {
                DefinirNivelConflito(IA03NivelConflito.Tensao, "afinidade diplomática atual e histórica deteriorada");
            }

            if (nivelDeConflito == IA03NivelConflito.Tensao
                && afinidadeEstrategica <= -75
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
            // Força ainda viva + baixas próprias registradas representa o total
            // comprometido nesta crise. Somar as baixas ao contingente do
            // relatório anterior duplicava perdas em cada nova janela.
            int forcaInicial = Mathf.Max(0, unidadesProprias)
                               + relatorio.Acumulado.UnidadesPropriasPerdidas;

            relatorio.RegistrarEconomia(economiaAtual);
            Hegemonia.AI.BrainMaster.IA_ForceSnapshot forcaPropria = mundo != null ? mundo.ForceSnapshot : null;
            float duracaoConflito = inicioGuerraEm >= 0f ? Mathf.Max(0f, now - inicioGuerraEm) : 0f;
            IA03RelatorioSnapshot snapshot = relatorio.CriarRelatorio(
                unidadesProprias,
                inimigosConhecidos,
                forcaInicial,
                now,
                duracaoConflito,
                forcaPropria);
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
                    + ", reposição conhecida de ativos destruídos (inimigo/próprio)=" + snapshot.PrejuizoEconomicoInimigo
                    + "/" + snapshot.PrejuizoEconomicoProprio
                    + ", duração do conflito=" + Mathf.RoundToInt(snapshot.DuracaoConflitoSegundos) + "s"
                    + ", missões ofensivas vencidas/perdidas=" + snapshot.BatalhasVencidas + "/" + snapshot.BatalhasPerdidas
                    + ", objetivos=" + snapshot.ObjetivosCapturados + "/" + snapshot.ObjetivosPerdidos
                    + ", capacidade restante=" + Mathf.RoundToInt(snapshot.CapacidadeMilitarRestante * 100f) + "%"
                    + ", saldo=" + snapshot.SaldoNacional + ", comida=" + snapshot.EstoqueComida
                    + ", petróleo=" + snapshot.EstoquePetroleo + ", energia=" + snapshot.EstoqueEnergia
                    + ", prontidão=" + (string.IsNullOrEmpty(economiaAtual.ClassificacaoProntidao) ? "indisponível" : economiaAtual.ClassificacaoProntidao)
                    + ", saldo disponível/reserva=" + economiaAtual.SaldoDisponivel + "/" + economiaAtual.ReservaFinanceiraEstrategica
                    + ", manpower disponível/reserva/mobilizável=" + economiaAtual.ManpowerDisponivel + "/"
                    + economiaAtual.ReservaManpowerEstrategica + "/" + economiaAtual.ManpowerDisponivelAlemDaReserva
                    + ", combustível diesel/aéreo/naval=" + economiaAtual.CombustivelTerrestre + "/"
                    + economiaAtual.CombustivelAereo + "/" + economiaAtual.CombustivelNaval
                    + ", força operacional terrestre/aérea/naval=" + economiaAtual.UnidadesTerrestresOperacionais + "/"
                    + economiaAtual.UnidadesAereasOperacionais + "/" + economiaAtual.UnidadesNavaisOperacionais
                    + ", reposição operacional fáb/quartel/aero/heli/est=" + economiaAtual.FabricasOperacionais + "/"
                    + economiaAtual.QuarteisOperacionais + "/" + economiaAtual.AeroportosMilitaresOperacionais + "/"
                    + economiaAtual.HeliportosOperacionais + "/" + economiaAtual.EstaleirosOperacionais
                    + ", reposição do grupo="
                    + (economiaAtual.ReposicaoDoGrupoConhecida
                        ? economiaAtual.ItemMaisCaroDoGrupo + ":" + economiaAtual.CustoReposicaoDoGrupo
                            + " DH/manpower " + economiaAtual.ManpowerReposicaoDoGrupo
                        : "não avaliada")
                    + ", autonomia em movimento=" + (economiaAtual.AutonomiaOperacionalEstimadaHoras >= 0f
                        ? economiaAtual.AutonomiaOperacionalEstimadaHoras.ToString("0.0") + "h"
                        : "n/d")
                    + ", decisão IA03=" + ultimaDecisao
                    + (string.IsNullOrEmpty(economiaAtual.MotivoProntidao) ? "." : ", motivo=" + economiaAtual.MotivoProntidao + "."));
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
                if (PodeEscalarParaGuerraTotal(governo, out string motivoBloqueio))
                {
                    DefinirNivelConflito(
                        IA03NivelConflito.GuerraTotal,
                        "as hostilidades continuaram após a avaliação do Nível 2 e há prontidão nacional");
                }
                else
                {
                    ultimaDecisao = "Nível 1 adiado: " + motivoBloqueio;
                }
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

        private bool PodeEscalarParaGuerraTotal(SistemaGovernoMundial governo, out string motivoBloqueio)
        {
            if (!economiaAtual.DadosDisponiveis)
            {
                motivoBloqueio = "dados econômicos nacionais indisponíveis";
                return false;
            }

            if (economiaAtual.ReservaFinanceiraBaixa)
            {
                motivoBloqueio = "reserva financeira abaixo da meta nacional";
                return false;
            }

            if (economiaAtual.EstoqueEssencialBaixo)
            {
                motivoBloqueio = "estoque essencial baixo ou em déficit";
                return false;
            }

            if (governo == null || brain == null || governo.ObterPais(brain.TeamId) == null)
            {
                motivoBloqueio = "dados nacionais de mobilização indisponíveis";
                return false;
            }

            if (governo.ObterManpowerDisponivelParaMobilizacao(brain.TeamId) <= 0)
            {
                motivoBloqueio = "sem manpower civil ou alistável para reposição";
                return false;
            }

            if (governo.ObterManpowerDisponivelAlemDaReserva(brain.TeamId) <= 0)
            {
                motivoBloqueio = "manpower mobilizável abaixo da reserva estratégica do perfil";
                return false;
            }

            if (!governo.PodeMobilizarEfetivo(brain.TeamId, 1))
            {
                motivoBloqueio = "sem manpower civil ou alistável para reposição";
                return false;
            }

            IA_Context contexto = brain != null ? brain.Context : null;
            IA_ForceSnapshot forca = contexto != null
                ? contexto.ForceSnapshot ?? (contexto.WorldState != null ? contexto.WorldState.ForceSnapshot : null)
                : null;
            if (forca == null)
            {
                motivoBloqueio = "snapshot de forças indisponível";
                return false;
            }

            bool dominioTerrestreDominante = perfilPais.PreferenciaTerrestre >= 0.5f
                                             && perfilPais.PreferenciaTerrestre > perfilPais.PreferenciaNaval
                                             && perfilPais.PreferenciaTerrestre > perfilPais.PreferenciaAerea;
            bool dominioAereoDominante = perfilPais.PreferenciaAerea >= 0.5f
                                         && perfilPais.PreferenciaAerea > perfilPais.PreferenciaNaval
                                         && perfilPais.PreferenciaAerea > perfilPais.PreferenciaTerrestre;
            bool dominioNavalDominante = perfilPais.PreferenciaNaval >= 0.5f
                                         && perfilPais.PreferenciaNaval > perfilPais.PreferenciaAerea
                                         && perfilPais.PreferenciaNaval > perfilPais.PreferenciaTerrestre;

            // Doutrinas aérea e naval validam suas instalações próprias abaixo;
            // FactoryCount não deve exigir também uma fábrica terrestre nesses casos.
            if (forca.FactoryCount <= 0 && !dominioAereoDominante && !dominioNavalDominante)
            {
                motivoBloqueio = "sem fábrica para sustentar produção e reposição militar";
                return false;
            }

            if (dominioTerrestreDominante
                && (forca.InfantryUnits + forca.TankUnits + forca.ArtilleryUnits <= 0 || !forca.HasBarracks))
            {
                motivoBloqueio = "domínio terrestre prioritário sem tropas ou quartel";
                return false;
            }

            if (dominioAereoDominante
                && (forca.ReadyAircraft <= 0 || !forca.HasMilitaryAirport))
            {
                motivoBloqueio = "domínio aéreo prioritário sem aeronave ou aeroporto militar";
                return false;
            }

            if (dominioNavalDominante
                && (forca.NavalUnits < perfilPais.MinimoNaviosEscolta || !forca.HasOperationalNavalBase))
            {
                motivoBloqueio = "domínio naval prioritário sem escolta ou base naval";
                return false;
            }

            int limiteN1 = CalcularLimitePorMissaoN1(
                forca.TotalCombatUnits,
                forca.TotalCombatUnits,
                IA03NivelConflito.GuerraTotal,
                perfilPais.ReservaDefesaNacional,
                perfilPais.ContingenteMaximoDeGuerra,
                perfilPais.ContingenteMaximoPorMissaoN1);
            if (limiteN1 <= 0)
            {
                motivoBloqueio = "força de combate insuficiente para mobilizar o contingente mínimo N1";
                return false;
            }

            motivoBloqueio = string.Empty;
            return true;
        }

        private static long CalcularReservaManpowerEstrategica(
            DadosPaisGoverno pais,
            float percentualDaPopulacao)
        {
            if (pais == null)
            {
                return 0L;
            }

            long componentesPopulacionais = (long)Mathf.Max(0, pais.populacaoCivil)
                + Mathf.Max(0, pais.populacaoMilitarAtiva)
                + Mathf.Max(0, pais.reservistas)
                + Mathf.Max(0, pais.alistaveis);
            long populacaoTotal = System.Math.Max(Mathf.Max(0, pais.populacao), componentesPopulacionais);
            decimal percentual = System.Convert.ToDecimal(Mathf.Clamp(percentualDaPopulacao, 0f, 0.5f));
            return (long)System.Decimal.Ceiling(populacaoTotal * percentual);
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
            if ((nivelDeConflito == IA03NivelConflito.Paz && (brain == null || brain.TeamId != 2))
                || !PodeIniciarMissaoEstrategica())
            {
                return;
            }

            AtualizarFilaMissoes();
            if (filaMissoes.Count == 0)
            {
                if (!TentarIniciarMissao(null, now, out string motivoSemMissao))
                {
                    ultimaDecisao = motivoSemMissao;
                }
                return;
            }

            string ultimoMotivo = string.Empty;
            // Uma ofensiva sem autorização, alvo ou grupo disponível não pode
            // bloquear as patrulhas defensivas que estão atrás dela na fila.
            for (int i = 0; i < filaMissoes.Count; i++)
            {
                MissaoEstrategicaSO candidata = filaMissoes[i];
                if (TentarIniciarMissao(candidata, now, out string motivo))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(motivo))
                {
                    ultimoMotivo = motivo;
                }
            }

            ultimaDecisao = string.IsNullOrEmpty(ultimoMotivo)
                ? "nenhuma missão estratégica pôde ser iniciada"
                : ultimoMotivo;
        }

        private bool TentarIniciarMissao(MissaoEstrategicaSO selecionada, float now, out string motivo)
        {
            motivo = string.Empty;
            if (!PodeEmitirMissaoOfensiva(selecionada, out string motivoAutorizacaoOfensiva))
            {
                motivo = motivoAutorizacaoOfensiva;
                return false;
            }

            if (MissaoExigeAutorizacaoDeGuerraFormal(selecionada)
                && nivelDeConflito == IA03NivelConflito.ConflitoLimitado
                && !economiaAtual.PodeEscalarParaN2)
            {
                string motivoProntidao = string.IsNullOrEmpty(economiaAtual.MotivoProntidao)
                    ? "snapshot de prontidão indisponível"
                    : economiaAtual.MotivoProntidao;
                motivo = "Nível 2: missão ofensiva adiada até a prontidão melhorar — " + motivoProntidao;
                return false;
            }

            if (MissaoExigeAutorizacaoDeGuerraFormal(selecionada)
                && nivelDeConflito == IA03NivelConflito.GuerraTotal
                && !PodeEscalarParaGuerraTotal(SistemaGovernoMundial.Instancia, out string motivoN1))
            {
                motivo = "Nível 1: missão ofensiva adiada até a prontidão melhorar — " + motivoN1;
                return false;
            }

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
                motivo = "missão não permitida no nível estratégico atual";
                return false;
            }

            CreatyEstrategico ponto = SelecionarCreaty(tiposCreatyCandidatos, dominio, minimoUnidades);
            if (ponto == null)
            {
                motivo = "nenhum Creaty manual compatível está disponível para " + estadoNacional;
                return false;
            }

            bool buscaAlvoOfensivoObservado = MissaoExigeCapacidadeOfensiva(selecionada);
            IA_EnemyObservation alvoObservadoParaMissao = buscaAlvoOfensivoObservado
                ? SelecionarAlvoObservado(selecionada.Alvo, ponto.transform.position)
                : null;
            if (MissaoExigeDestruicaoDeAlvo(selecionada)
                && (alvoObservadoParaMissao == null || alvoObservadoParaMissao.Transform == null))
            {
                motivo = "alvo inimigo não observado";
                return false;
            }

            int capacidadeCreatyDisponivel = Mathf.Max(0, ponto.MaximoDeUnidades - ponto.UnidadesReservadas);
            if (!ConstruirGrupoUnidades(
                    selecionada,
                    dominio,
                    minimoUnidades,
                    capacidadeCreatyDisponivel,
                    alvoObservadoParaMissao))
            {
                motivo = "unidades insuficientes para a missão " + (selecionada != null ? selecionada.NomeMissao : ponto.Tipo.ToString());
                return false;
            }

            if (nivelDeConflito == IA03NivelConflito.ConflitoLimitado
                || nivelDeConflito == IA03NivelConflito.GuerraTotal)
            {
                string motivoReposicao;
                SistemaGovernoMundial governoReposicao = SistemaGovernoMundial.Instancia;
                bool reposicaoViavel = economia.TentarAvaliarReposicaoDoGrupo(
                    brain.Context.Backend,
                    governoReposicao,
                    unidadesCandidatas,
                    ref economiaAtual,
                    out motivoReposicao);

                if (!reposicaoViavel)
                {
                    economiaAtual.PodeEscalarParaN2 = false;
                    economiaAtual.ClassificacaoProntidao = "LIMITADA";
                    economiaAtual.MotivoProntidao = motivoReposicao;
                    motivo = (nivelDeConflito == IA03NivelConflito.ConflitoLimitado ? "Nível 2" : "Nível 1")
                        + ": missão ofensiva adiada — " + motivoReposicao;
                    return false;
                }
            }

            if (!ponto.TentarReservar(brain.TeamId, paisAlvoTeamId, unidadesCandidatas.Count))
            {
                motivo = "Creaty ocupado ou em tempo de reutilização: " + ponto.Id;
                return false;
            }

            ResetarRastreamentoMissao();
            if (!EnfileirarOrdem(selecionada, ponto, unidadesCandidatas, out string motivoOrdem, alvoObservadoParaMissao))
            {
                ponto.LiberarReserva(unidadesCandidatas.Count);
                motivo = "ordem não enfileirada: " + motivoOrdem;
                return false;
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
            RegistrarNoticiaMissao("iniciou a missão \"" + ultimaMissao + "\" contra "
                + NomePaisParaNoticia(paisAlvoTeamId) + ".");
            RegistrarLogDebugSeNecessario();
            return true;
        }

        private bool PodeIniciarMissaoEstrategica()
        {
            if (brain == null
                || brain.IntegrationMode == IA_BrainMaster.IA_IntegrationMode.ShadowReadOnly)
            {
                return false;
            }

            if (!brain.HasExecutionAuthority)
            {
                ultimaDecisao = "missão adiada: BrainMaster sem autoridade de execução";
                return false;
            }

            return brain.Context != null
                   && brain.Context.WorldState != null
                   && brain.Context.CommandQueue != null;
        }

        private bool PodeEmitirMissaoOfensiva(MissaoEstrategicaSO missao, out string motivo)
        {
            motivo = string.Empty;
            if (!MissaoExigeAutorizacaoDeGuerraFormal(missao))
            {
                return true;
            }

            if (nivelDeConflito == IA03NivelConflito.Paz)
            {
                motivo = "missão ofensiva bloqueada: país em paz";
                return false;
            }

            // Avanço militar e tensão podem preparar forças, mas não autorizam
            // uma ordem que efetivamente procura destruir um alvo. Só N2/N1
            // podem atravessar este gate; a relação ainda precisa confirmar a
            // guerra formal no instante da emissão.
            if (nivelDeConflito > IA03NivelConflito.ConflitoLimitado)
            {
                motivo = "missão ofensiva bloqueada: nível diplomático ainda não autoriza fogo";
                return false;
            }

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            RelacaoPaisGoverno relacao = governo != null && brain != null
                ? governo.ObterRelacao(brain.TeamId, paisAlvoTeamId)
                : null;
            if (relacao == null || !relacao.guerraDeclarada || relacao.cessarFogoAtivo)
            {
                motivo = "missão ofensiva bloqueada: guerra formal ou autorização diplomática ausente";
                return false;
            }

            return true;
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

        private bool ProximoPontoCompativel(MissaoEstrategicaSO missao, CreatyEstrategico ponto, int quantidade)
        {
            if (missao == null || ponto == null || brain == null
                || !missao.Aceita(nivelDeConflito, ponto.Tipo)
                || !ponto.PodeReservar(brain.TeamId, equipeAlvoAtiva, quantidade))
            {
                return false;
            }

            // Use o mesmo índice e os mesmos filtros da seleção inicial:
            // equipe, alvo permitido, nível, domínio, ativação e ID único.
            RegistroCreatysEstrategicos.PreencherCandidatos(
                candidatos,
                brain.TeamId,
                equipeAlvoAtiva,
                ponto.Tipo,
                nivelDeConflito,
                missao.Dominio);

            for (int i = 0; i < candidatos.Count; i++)
            {
                if (candidatos[i] == ponto)
                {
                    return true;
                }
            }

            return false;
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
            int capacidadeCreatyDisponivel,
            IA_EnemyObservation alvoObservado)
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

            bool patrulhaDeValdoriaEmPaz = brain != null
                && brain.TeamId == 2
                && nivelDeConflito == IA03NivelConflito.Paz
                && missao != null
                && missao.PermitePatrulhaEmPaz
                && missao.TipoOrdem == IA03TipoOrdem.Patrulhar;
            if (patrulhaDeValdoriaEmPaz)
            {
                limiteUnidades = Mathf.Min(
                    Mathf.Max(0, capacidadeCreatyDisponivel),
                    Mathf.Max(1, Mathf.CeilToInt(totalDeCombate / 6f)));
            }

            bool exigePortaAvioes = missao != null && missao.ExigePortaAvioes;
            bool exigeSubmarino = missao != null && missao.ExigeSubmarino;
            bool exigeTransporte = missao != null && missao.ExigeTransporteNaval;
            bool exigePresidente = missao != null && missao.ExigePresidente;
            bool exigeCapacidadeOfensiva = MissaoExigeCapacidadeOfensiva(missao);
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

                // The movement controllers already stop fuel-dependent units
                // when their tank is empty. Do not reserve those units for a
                // new strategic mission that they cannot physically execute.
                if (!CombustivelUnidade.PodeOperarObjeto(unidade))
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
                bool ehNavioLogistico = unidade.GetComponent<NavioCargaMercado>() != null
                                        || unidade.GetComponent<NavioPetroleiro>() != null;
                bool ehPortaAvioes = unidade.GetComponentInChildren<GerenciadorPortaAvioes>() != null
                                      || unidade.GetComponentInChildren<GerenciadorOperacoesPortaAvioesV2>() != null;

                // Cargueiros e petroleiros servem à logística e não são
                // consumidos como unidades de patrulha ou ataque da IA03.
                if (ehNavioLogistico)
                {
                    continue;
                }

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

                // Uma missão ofensiva não pode consumir a reserva estratégica
                // com uma plataforma que apenas se enquadra no domínio. A
                // exceção deliberada é o porta-aviões de uma missão que o
                // exige: ele pode ser a plataforma de apoio enquanto escoltas
                // e aeronaves carregam o armamento ofensivo.
                bool plataformaDeApoioPortaAvioes = exigePortaAvioes && ehPortaAvioes;
                if (exigeCapacidadeOfensiva
                    && !plataformaDeApoioPortaAvioes
                    && !PossuiCapacidadeOfensiva(
                        unidade,
                        alvoObservado != null && alvoObservado.Domain == IA_Domain.Air))
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

        private static bool MissaoExigeCapacidadeOfensiva(MissaoEstrategicaSO missao)
        {
            if (missao == null)
            {
                return false;
            }

            return missao.TipoOrdem == IA03TipoOrdem.Atacar
                   || missao.CondicaoDeSucesso == IA03CondicaoMissao.DestruirAlvo
                   || missao.CondicaoDeFracasso == IA03CondicaoMissao.DestruirAlvo;
        }

        private static bool MissaoExigeDestruicaoDeAlvo(MissaoEstrategicaSO missao)
        {
            return missao != null
                   && (missao.CondicaoDeSucesso == IA03CondicaoMissao.DestruirAlvo
                       || missao.CondicaoDeFracasso == IA03CondicaoMissao.DestruirAlvo);
        }

        private static bool MissaoExigeAutorizacaoDeGuerraFormal(MissaoEstrategicaSO missao)
        {
            if (missao == null)
            {
                return false;
            }

            return missao.TipoMissao == IA03TipoMissao.AtaqueLimitado
                   || missao.TipoMissao == IA03TipoMissao.GuerraTotal
                   || missao.TipoMissao == IA03TipoMissao.InvasaoAnfibia
                   || MissaoExigeCapacidadeOfensiva(missao);
        }

        private static bool PossuiCapacidadeOfensiva(GameObject unidade, bool alvoAereo)
        {
            if (unidade == null)
            {
                return false;
            }

            LancadorNaval[] lancadoresNavais = unidade.GetComponentsInChildren<LancadorNaval>(true);
            for (int i = 0; i < lancadoresNavais.Length; i++)
            {
                LancadorNaval lancador = lancadoresNavais[i];
                if (lancador != null
                    && ((lancador.municaoTotal > 0 && lancador.prefabMissel != null)
                        || (!alvoAereo && lancador.torpedosTotal > 0 && lancador.prefabTorpedo != null)))
                {
                    return true;
                }
            }

            ControleSubmarino[] submarinos = unidade.GetComponentsInChildren<ControleSubmarino>(true);
            for (int i = 0; i < submarinos.Length; i++)
            {
                ControleSubmarino submarino = submarinos[i];
                if (submarino != null
                    && ((submarino.prefabMisselSubmarino != null
                         && submarino.misseisDisponiveis > 0)
                        || (!alvoAereo && submarino.PossuiTorpedoOperacional)))
                {
                    return true;
                }
            }

            LancadorMisselCaca[] lancadoresCaca = unidade.GetComponentsInChildren<LancadorMisselCaca>(true);
            for (int i = 0; i < lancadoresCaca.Length; i++)
            {
                LancadorMisselCaca lancador = lancadoresCaca[i];
                if (lancador != null
                    && lancador.municaoAtual > 0
                    && lancador.missilCacaPrefab != null)
                {
                    return true;
                }
            }

            LancadorMisseis[] lancadoresEstrategicos = unidade.GetComponentsInChildren<LancadorMisseis>(true);
            for (int i = 0; i < lancadoresEstrategicos.Length; i++)
            {
                LancadorMisseis lancador = lancadoresEstrategicos[i];
                if (lancador != null
                    && lancador.municaoAtual > 0
                    && lancador.missilPrefab != null)
                {
                    return true;
                }
            }

            AviaoBombardeiro[] bombardeiros = unidade.GetComponentsInChildren<AviaoBombardeiro>(true);
            for (int i = 0; i < bombardeiros.Length; i++)
            {
                AviaoBombardeiro bombardeiro = bombardeiros[i];
                if (bombardeiro != null
                    && bombardeiro.projetilPrefab != null
                    && bombardeiro.comportasDeBomba != null
                    && bombardeiro.comportasDeBomba.Length > 0)
                {
                    return true;
                }
            }

            SistemaDeTiro[] armasDiretas = unidade.GetComponentsInChildren<SistemaDeTiro>(true);
            for (int i = 0; i < armasDiretas.Length; i++)
            {
                SistemaDeTiro arma = armasDiretas[i];
                if (arma != null && arma.PossuiCapacidadeDeDisparo && arma.prefabProjetil != null)
                {
                    return true;
                }
            }

            ControleTorreta[] torretas = unidade.GetComponentsInChildren<ControleTorreta>(true);
            for (int i = 0; i < torretas.Length; i++)
            {
                ControleTorreta torreta = torretas[i];
                if (torreta == null || (torreta.EhAntiAereo && !alvoAereo))
                {
                    continue;
                }

                bool temCanhao = torreta != null
                    && torreta.tamanhoCartucho > 0
                    && torreta.PossuiMunicaoCanhaoParaMissao
                    && torreta.municaoPrefab != null
                    && torreta.locaisDoTiro != null
                    && torreta.locaisDoTiro.Length > 0;
                bool temMissil = torreta != null
                    && torreta.capacidadeMisseis > 0
                    && torreta.PossuiMissilParaMissao
                    && torreta.misselPrefab != null
                    && torreta.locaisDoMissel != null
                    && torreta.locaisDoMissel.Length > 0;
                if (temCanhao || temMissil)
                {
                    return true;
                }
            }

            ControleTorretaModular[] torretasModulares = unidade.GetComponentsInChildren<ControleTorretaModular>(true);
            for (int i = 0; i < torretasModulares.Length; i++)
            {
                ControleTorretaModular torreta = torretasModulares[i];
                if (torreta == null || (torreta.EhAntiAereo && !alvoAereo) || torreta.armas == null)
                {
                    continue;
                }

                for (int j = 0; j < torreta.armas.Count; j++)
                {
                    ModuloArma arma = torreta.armas[j];
                    if (arma != null
                        && (arma.tamanhoCartucho <= 0 || arma.municaoAtual > 0 || arma.estaRecarregando)
                        && arma.municaoPrefab != null
                        && arma.pontosDisparo != null
                        && arma.pontosDisparo.Length > 0)
                    {
                        return true;
                    }
                }
            }

            SistemaArmamentoHelice[] armasDeHelice = unidade.GetComponentsInChildren<SistemaArmamentoHelice>(true);
            for (int i = 0; i < armasDeHelice.Length; i++)
            {
                SistemaArmamentoHelice arma = armasDeHelice[i];
                if (arma != null
                    && arma.cartuchoMaximo > 0
                    && arma.PossuiMunicaoParaMissao
                    && arma.prefabProjetilTrassante != null
                    && arma.canosDeTiro != null
                    && arma.canosDeTiro.Length > 0)
                {
                    return true;
                }
            }

            LancadorMLRS[] lancadoresMlrs = unidade.GetComponentsInChildren<LancadorMLRS>(true);
            for (int i = 0; i < lancadoresMlrs.Length; i++)
            {
                LancadorMLRS lancador = lancadoresMlrs[i];
                if (lancador != null
                    && lancador.missilPrefab != null
                    && lancador.pontosDeSaida != null
                    && lancador.pontosDeSaida.Length > 0)
                {
                    return true;
                }
            }

            LancadorSimples[] lancadoresSimples = unidade.GetComponentsInChildren<LancadorSimples>(true);
            for (int i = 0; i < lancadoresSimples.Length; i++)
            {
                LancadorSimples lancador = lancadoresSimples[i];
                if (lancador != null
                    && lancador.misselPrefab != null
                    && lancador.pontosDeSaida != null
                    && lancador.pontosDeSaida.Length > 0)
                {
                    return true;
                }
            }

            LancadorMultiplo[] lancadoresMultiplos = unidade.GetComponentsInChildren<LancadorMultiplo>(true);
            for (int i = 0; i < lancadoresMultiplos.Length; i++)
            {
                LancadorMultiplo lancador = lancadoresMultiplos[i];
                if (lancador != null
                    && lancador.misselPrefab != null
                    && lancador.saidasDeMissel != null
                    && lancador.saidasDeMissel.Length > 0)
                {
                    return true;
                }
            }

            if (alvoAereo)
            {
                TorretaAntiaerea[] torretasAntiaereas = unidade.GetComponentsInChildren<TorretaAntiaerea>(true);
                for (int i = 0; i < torretasAntiaereas.Length; i++)
                {
                    TorretaAntiaerea torreta = torretasAntiaereas[i];
                    if (torreta != null
                        && torreta.capacidadeCartucho > 0
                        && torreta.prefabProjetil != null
                        && torreta.pontosDeDisparo != null
                        && torreta.pontosDeDisparo.Length > 0)
                    {
                        return true;
                    }
                }
            }

            Torreta[] torretasLegadas = unidade.GetComponentsInChildren<Torreta>(true);
            for (int i = 0; i < torretasLegadas.Length; i++)
            {
                Torreta torreta = torretasLegadas[i];
                if (torreta != null && torreta.prefabProjetil != null && torreta.pontoTiro != null)
                {
                    return true;
                }
            }

            ControladorNavioVigilante[] vigilantes = unidade.GetComponentsInChildren<ControladorNavioVigilante>(true);
            for (int i = 0; i < vigilantes.Length; i++)
            {
                ControladorNavioVigilante vigilante = vigilantes[i];
                if (vigilante != null
                    && vigilante.projetilPrefab != null
                    && vigilante.pontosDisparo != null
                    && vigilante.pontosDisparo.Length > 0)
                {
                    return true;
                }
            }

            SiloLancadorEstrategico[] silos = unidade.GetComponentsInChildren<SiloLancadorEstrategico>(true);
            for (int i = 0; i < silos.Length; i++)
            {
                SiloLancadorEstrategico silo = silos[i];
                if (silo != null && silo.ProntoParaLancar && silo.prefabMisselEstrategico != null)
                {
                    return true;
                }
            }

            return false;
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

        private bool EnfileirarOrdem(
            MissaoEstrategicaSO missao,
            CreatyEstrategico ponto,
            List<GameObject> unidades,
            out string motivo,
            IA_EnemyObservation alvoObservadoPreSelecionado = null)
        {
            motivo = string.Empty;
            bool exigeDestruicaoDeAlvo = MissaoExigeDestruicaoDeAlvo(missao);
            IA_CommandType tipoComando = missao != null
                && (missao.TipoOrdem == IA03TipoOrdem.Atacar || exigeDestruicaoDeAlvo)
                ? IA_CommandType.Attack
                : missao != null && missao.TipoOrdem == IA03TipoOrdem.Patrulhar
                    ? IA_CommandType.Patrol
                    : IA_CommandType.Move;

            object payload;
            IA_EnemyObservation alvoObservado = tipoComando == IA_CommandType.Attack || exigeDestruicaoDeAlvo
                ? alvoObservadoPreSelecionado ?? SelecionarAlvoObservado(
                    missao != null ? missao.Alvo : ponto.AlvoPreferencial,
                    ponto.transform.position)
                : null;

            bool possuiAlvoObservadoAtivo = alvoObservado != null && alvoObservado.Transform != null;
            if (exigeDestruicaoDeAlvo
                && (alvoObservado == null || alvoObservado.Transform == null))
            {
                motivo = "alvo inimigo não observado";
                return false;
            }

            bool permiteAtaquePorCoordenada = tipoComando == IA_CommandType.Attack
                                                && !exigeDestruicaoDeAlvo
                                                && !possuiAlvoObservadoAtivo;
            int equipeAlvoDaOrdem = alvoObservado != null && alvoObservado.TeamId > 0
                ? alvoObservado.TeamId
                : (ponto.PaisAlvoTeamId > 0 ? ponto.PaisAlvoTeamId : paisAlvoTeamId);
            if (permiteAtaquePorCoordenada && equipeAlvoDaOrdem <= 0)
            {
                motivo = "missão ofensiva sem equipe rival identificada para validar o ataque por coordenadas";
                return false;
            }

            if (possuiAlvoObservadoAtivo)
            {
                alvoMissaoAtivo = alvoObservado.Transform;
                alvoMissaoFoiDefinido = true;
                idPersistenteAlvoMissao = ObterIdPersistenteAlvo(alvoMissaoAtivo);
                alvoMissaoDestruido = false;
            }
            else if (tipoComando == IA_CommandType.Attack || missao != missaoAtiva)
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
                    TargetPosition = alvoObservado != null
                        ? ResolverPontoDeAproximacaoAtaqueTerrestre(unidades, alvoObservado.Transform, alvoObservado.Position)
                        : ponto.transform.position,
                    RequiresDeclaredWar = missao != null && tipoComando == IA_CommandType.Attack,
                    TargetTeamId = alvoObservado != null && alvoObservado.TeamId > 0
                        ? alvoObservado.TeamId
                        : (permiteAtaquePorCoordenada ? equipeAlvoDaOrdem : 0),
                    AllowCoordinateAttackAgainstKnownEnemy = permiteAtaquePorCoordenada
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
            ordemDaMissaoRestaurada = false;
            return true;
        }

        private static Vector3 ResolverPontoDeAproximacaoAtaqueTerrestre(
            List<GameObject> unidades,
            Transform alvo,
            Vector3 posicaoAlvo)
        {
            if (alvo == null || unidades == null || unidades.Count == 0)
            {
                return posicaoAlvo;
            }

            float menorAlcanceDeTorreta = float.PositiveInfinity;
            Vector3 centroDoGrupo = Vector3.zero;
            int unidadesVeicularesArmadas = 0;
            for (int i = 0; i < unidades.Count; i++)
            {
                GameObject unidade = unidades[i];
                if (unidade == null) continue;

                IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
                if (identidade == null || identidade.tipoUnidade != TipoUnidade.Veiculo) continue;

                ControleTorreta[] torretas = unidade.GetComponentsInChildren<ControleTorreta>(true);
                bool possuiTorretaDeTiro = false;
                for (int j = 0; j < torretas.Length; j++)
                {
                    ControleTorreta torreta = torretas[j];
                    if (torreta == null || torreta.alcance <= 0f) continue;
                    possuiTorretaDeTiro = true;
                    menorAlcanceDeTorreta = Mathf.Min(menorAlcanceDeTorreta, torreta.alcance);
                }

                if (!possuiTorretaDeTiro) continue;
                centroDoGrupo += unidade.transform.position;
                unidadesVeicularesArmadas++;
            }

            if (unidadesVeicularesArmadas == 0 || float.IsInfinity(menorAlcanceDeTorreta))
            {
                return posicaoAlvo;
            }

            centroDoGrupo /= unidadesVeicularesArmadas;
            Vector3 direcaoDeRetirada = centroDoGrupo - alvo.position;
            direcaoDeRetirada.y = 0f;
            if (direcaoDeRetirada.sqrMagnitude < 0.01f)
            {
                direcaoDeRetirada = -alvo.forward;
                direcaoDeRetirada.y = 0f;
            }
            if (direcaoDeRetirada.sqrMagnitude < 0.01f)
            {
                return posicaoAlvo;
            }

            // Armas terrestres com torreta precisam abordar dentro do alcance,
            // sem levar o chassi até o centro do alvo e entrar na zona cega.
            float distanciaDeEngajamento = Mathf.Clamp(menorAlcanceDeTorreta * 0.35f, 30f, 40f);
            Vector3 pontoDeAproximacao = alvo.position + direcaoDeRetirada.normalized * distanciaDeEngajamento;
            pontoDeAproximacao.y = posicaoAlvo.y;
            return pontoDeAproximacao;
        }

        private void ProcessarMissaoAtiva(float now)
        {
            if (brain == null || brain.IntegrationMode == IA_BrainMaster.IA_IntegrationMode.ShadowReadOnly)
            {
                EncerrarMissao("modo somente leitura");
                return;
            }

            if (nivelDeConflito == IA03NivelConflito.Paz)
            {
                // AtualizarConflito pode observar o cessar-fogo no mesmo Tick
                // em que a ordem ainda está pendente. Cancela o canal de
                // comando antes que ele volte a emitir movimento/ataque.
                EncerrarMissao("cessar-fogo registrado antes da conclusão da missão");
                return;
            }

            if (EncerrarSeOrdemEstrategicaTerminouComFalha())
            {
                return;
            }

            if (creatyAtivo == null)
            {
                FinalizarMissaoPorFalhaDeDisponibilidade("Creaty indisponível");
                return;
            }

            int vivos = 0;
            int chegaram = 0;
            int noDestinoAgora = 0;
            int unidadesInoperaveisPorCombustivel = 0;
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
                if (!CombustivelUnidade.PodeOperarObjeto(unidade))
                {
                    unidadesInoperaveisPorCombustivel++;
                }

                if (rastrearChegada)
                {
                    ControleUnidade controleUnidadeMissao = unidade.GetComponent<ControleUnidade>();
                    float raioChegada = raio;
                    if (controleUnidadeMissao != null && controleUnidadeMissao.EhUnidadeNaval())
                    {
                        // O marcador pode estar na margem e o patrulhamento
                        // naval precisa permanecer em água navegável. A
                        // tolerância cobre o circuito local e a formação da
                        // frota, sem depender da altura/profundidade do casco.
                        raioChegada = 800f;
                    }
                    else if ((controleUnidadeMissao != null && controleUnidadeMissao.DominioAtual == DominioControleUnidade.Aereo)
                             || unidade.GetComponent<ControleAviao>() != null
                             || unidade.GetComponent<ControleAviaoCaca>() != null
                             || unidade.GetComponent<Helicoptero>() != null)
                    {
                        // Aeronaves voam acima da posição do Creaty e a
                        // patrulha local ocupa alguns centenas de metros.
                        raioChegada = 350f;
                    }

                    Vector3 deslocamentoHorizontal = unidade.transform.position - creatyAtivo.transform.position;
                    deslocamentoHorizontal.y = 0f;
                    bool estaNoDestino = deslocamentoHorizontal.sqrMagnitude <= raioChegada * raioChegada;
                    if (estaNoDestino)
                    {
                        noDestinoAgora++;
                        unidadesComOrdemConcluidaNaMissao.Add(unidadeId);
                    }

                    if (estaNoDestino || unidadesComOrdemConcluidaNaMissao.Contains(unidadeId))
                    {
                        chegaram++;
                    }
                }
            }

            float tempoMaximo = missaoAtiva != null ? missaoAtiva.TempoMaximoSegundos : 600f;
            bool objetivoOfensivoConcluido = missaoAtiva != null
                                             && missaoAtiva.CondicaoDeSucesso == IA03CondicaoMissao.DestruirAlvo
                                             && alvoMissaoDestruido;
            if (vivos == 0 && !objetivoOfensivoConcluido)
            {
                FinalizarMissaoPorFalhaDeDisponibilidade("grupo indisponível");
                return;
            }

            bool todosChegaram = chegaram == vivos;
            if (todosChegaram)
            {
                grupoChegouAoDestino = true;
            }

            bool todosNoDestinoAgora = rastrearChegada && noDestinoAgora == vivos;
            if (todosNoDestinoAgora)
            {
                if (inicioPermanenciaNoDestinoEm < 0f)
                {
                    inicioPermanenciaNoDestinoEm = now;
                }
            }
            else
            {
                inicioPermanenciaNoDestinoEm = -1f;
                if (grupoChegouAoDestino)
                {
                    grupoSaiuDoDestino = true;
                }
            }

            bool prazoEncerrado = tempoMaximo > 0f && now - inicioMissaoEm >= tempoMaximo;
            float tempoPermanenciaMinimo = missaoAtiva != null
                ? Mathf.Max(0f, missaoAtiva.TempoMinimoDePermanenciaSegundos)
                : 0f;
            bool permaneceuNoDestino = todosNoDestinoAgora
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

            if (resultado == IA03ResultadoMissao.EmAndamento
                && vivos > 0
                && unidadesInoperaveisPorCombustivel == vivos)
            {
                CancelarMissaoAguardandoReabastecimento(
                    "todas as unidades sobreviventes estão sem combustível; aguardando reabastecimento para reavaliar a missão");
                return;
            }

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
            bool proximoPontoCompativel = proximo != null
                                          && ProximoPontoCompativel(
                                              missaoAtiva,
                                              proximo,
                                              unidadesAtivasNaMissao.Count);
            if (proximoPontoCompativel && !brain.HasExecutionAuthority)
            {
                // A rota atual já foi emitida antes da perda de autoridade e
                // pode terminar normalmente. Só aguarde a autoridade quando
                // houver um próximo trecho válido; um destino incompatível não
                // deve impedir a conclusão da missão atual.
                estadoDaMissao = IA03ResultadoMissao.EmAndamento;
                ultimaDecisao = "rota estratégica aguardando autoridade de comando";
                return;
            }

            if (proximoPontoCompativel
                && proximo.TentarReservar(brain.TeamId, equipeAlvoAtiva, unidadesAtivasNaMissao.Count))
            {
                if (EnfileirarOrdem(missaoAtiva, proximo, unidadesAtivasNaMissao, out string motivo))
                {
                    creatyAtivo.LiberarReserva(unidadesReservadas);
                    creatyAtivo = proximo;
                    unidadesReservadas = unidadesAtivasNaMissao.Count;
                    inicioMissaoEm = now;
                    inicioPermanenciaNoDestinoEm = -1f;
                    unidadesComOrdemConcluidaNaMissao.Clear();
                    grupoChegouAoDestino = false;
                    grupoSaiuDoDestino = false;
                    territorioDoObjetivoCapturado = false;
                    territorioDoObjetivoPerdido = false;
                    estadoDaMissao = IA03ResultadoMissao.EmAndamento;
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

        private void CancelarMissaoAguardandoReabastecimento(string motivo)
        {
            // Missões provenientes de SolicitarMissaoEstrategica podem não estar
            // em missoesEstrategicas. Preserve-as na fila para que o próximo
            // ciclo de decisão possa tentar novamente após o reabastecimento.
            MissaoEstrategicaSO missaoParaReenfileirar = missaoAtiva;
            FinalizarMissao(IA03ResultadoMissao.Cancelada, motivo);

            if (missaoParaReenfileirar != null
                && nivelDeConflito != IA03NivelConflito.Paz
                && !MissaoFoiConcluidaNaCrise(missaoParaReenfileirar))
            {
                InserirNaFila(missaoParaReenfileirar, false);
            }
        }

        private bool EncerrarSeOrdemEstrategicaTerminouComFalha()
        {
            if (brain == null
                || brain.Context == null
                || brain.Context.CommandQueue == null
                || string.IsNullOrWhiteSpace(idOrdemAtivaDaMissao))
            {
                return false;
            }

            IA_CommandStatus status = brain.Context.CommandQueue.GetStatus(idOrdemAtivaDaMissao);
            if (status == IA_CommandStatus.Failed)
            {
                FinalizarMissao(
                    IA03ResultadoMissao.Fracasso,
                    "ordem estratégica recusada pelo executor");
                return true;
            }

            if (status == IA_CommandStatus.Cancelled)
            {
                FinalizarMissao(
                    IA03ResultadoMissao.Cancelada,
                    "ordem estratégica cancelada antes da execução");
                return true;
            }

            return false;
        }

        private void EncerrarMissao(string motivo)
        {
            FinalizarMissao(IA03ResultadoMissao.Cancelada, motivo);
        }

        private void FinalizarMissao(IA03ResultadoMissao resultado, string motivo)
        {
            FinalizarMissaoInterno(resultado, motivo, true);
        }

        private void FinalizarMissaoPorFalhaDeDisponibilidade(string motivo)
        {
            bool houveResultadoMilitar = grupoPerdeuUnidade
                                         || territorioDoObjetivoPerdido
                                         || confirmacaoExternaDeFracasso;
            FinalizarMissaoInterno(IA03ResultadoMissao.Fracasso, motivo, houveResultadoMilitar);
        }

        private void FinalizarMissaoInterno(
            IA03ResultadoMissao resultado,
            string motivo,
            bool contabilizarResultadoMilitar)
        {
            string nomeMissaoFinalizada = missaoAtiva != null ? missaoAtiva.NomeMissao : string.Empty;
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

            if (!string.IsNullOrWhiteSpace(nomeMissaoFinalizada)
                && resultado != IA03ResultadoMissao.EmAndamento
                && resultado != IA03ResultadoMissao.Pendente)
            {
                RegistrarNoticiaMissao(DescreverResultadoMissao(resultado) + " a missão \""
                    + nomeMissaoFinalizada + "\". "
                    + (string.IsNullOrWhiteSpace(motivo) ? string.Empty : motivo.Trim()));
            }

            RegistrarResultadoMissaoMilitar(missaoAtiva, resultado, contabilizarResultadoMilitar);

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
            ordemDaMissaoRestaurada = false;
            estadoDaMissao = resultado;
            ResetarRastreamentoMissao();
            if (resultado == IA03ResultadoMissao.Cancelada && !string.IsNullOrWhiteSpace(motivo) && ativo)
            {
                ultimaDecisao = "missão cancelada: " + motivo;
            }
            RegistrarLogDebugSeNecessario();
        }

        private void RegistrarNoticiaMissao(string mensagem)
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            if (governo == null || string.IsNullOrWhiteSpace(mensagem))
            {
                return;
            }

            string pais = brain != null && brain.TeamId > 0
                ? governo.NomePais(brain.TeamId)
                : "nação não identificada";
            governo.RegistrarNoticia("IA03 de " + pais + " " + mensagem);
        }

        private static string NomePaisParaNoticia(int teamId)
        {
            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            return governo != null && teamId > 0 ? governo.NomePais(teamId) : "alvo não identificado";
        }

        private static string DescreverResultadoMissao(IA03ResultadoMissao resultado)
        {
            switch (resultado)
            {
                case IA03ResultadoMissao.Sucesso: return "concluiu com sucesso";
                case IA03ResultadoMissao.Cancelada: return "cancelou";
                case IA03ResultadoMissao.Expirada: return "deixou expirar";
                case IA03ResultadoMissao.Fracasso: return "não conseguiu concluir";
                default: return "finalizou";
            }
        }

        private void RegistrarResultadoMissaoMilitar(
            MissaoEstrategicaSO missao,
            IA03ResultadoMissao resultado,
            bool contabilizarResultadoMilitar)
        {
            if (missao == null
                || (missao.TipoMissao != IA03TipoMissao.AtaqueLimitado
                    && missao.TipoMissao != IA03TipoMissao.GuerraTotal)
                || missao.CondicaoDeSucesso != IA03CondicaoMissao.DestruirAlvo
                || !alvoMissaoFoiDefinido)
            {
                return;
            }

            // Vitória vem do objetivo ofensivo concluído. Uma falha operacional por
            // indisponibilidade só conta como derrota quando há evidência de perda militar.
            // Timeout e cancelamento continuam inconclusivos.
            if (resultado == IA03ResultadoMissao.Sucesso && alvoMissaoDestruido)
            {
                RegistrarResultadoCombate(true);
            }
            else if (resultado == IA03ResultadoMissao.Fracasso && contabilizarResultadoMilitar)
            {
                RegistrarResultadoCombate(false);
            }
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

                int instanceId = unidade.GetInstanceID();
                if (unidadesPerdidasNaMissao.Contains(instanceId))
                {
                    return;
                }

                unidadesPerdidasNaMissao.Add(instanceId);
                grupoPerdeuUnidade = true;
                RegistrarReposicaoDaBaixa(unidade);
                SolicitarReanaliseAntecipada();
                return;
            }
        }

        private void RegistrarReposicaoDaBaixa(GameObject unidade)
        {
            if (unidade == null || brain == null || brain.Context == null || brain.Context.Backend == null)
            {
                return;
            }

            IA_ConstructionMetadata metadata = unidade.GetComponent<IA_ConstructionMetadata>()
                ?? unidade.GetComponentInChildren<IA_ConstructionMetadata>(true);
            DadosConstrucao ficha = null;
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.ItemId))
            {
                brain.Context.Backend.TryResolveItemExact(metadata.ItemId, out ficha);
            }

            // Unidades antigas sem metadados ainda podem ser repostas quando o
            // nome do prefab resolve exatamente uma ficha do catálogo.
            if (ficha == null)
            {
                const string cloneSuffix = "(Clone)";
                string prefabName = unidade.name;
                if (prefabName.EndsWith(cloneSuffix, System.StringComparison.Ordinal))
                {
                    prefabName = prefabName.Substring(0, prefabName.Length - cloneSuffix.Length).TrimEnd();
                }

                brain.Context.Backend.TryResolveItemExact(prefabName, out ficha);
            }

            if (ficha == null || ficha.PrefabDaUnidade == null || string.IsNullOrWhiteSpace(ficha.GetStableId()))
            {
                return;
            }

            string itemKey = ficha.GetStableId();
            ReposicaoPendente pendente = null;
            for (int i = 0; i < reposicoesPendentes.Count; i++)
            {
                ReposicaoPendente candidato = reposicoesPendentes[i];
                if (candidato != null
                    && string.Equals(candidato.itemKey, itemKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    pendente = candidato;
                    break;
                }
            }

            if (pendente == null)
            {
                pendente = new ReposicaoPendente { itemKey = itemKey };
                reposicoesPendentes.Add(pendente);
            }

            pendente.quantidade = Mathf.Max(0, pendente.quantidade) + 1;
        }

        private void ProcessarReposicoesPendentes(float now)
        {
            if (reposicoesPendentes == null
                || reposicoesPendentes.Count == 0
                || brain == null
                || brain.IntegrationMode == IA_BrainMaster.IA_IntegrationMode.ShadowReadOnly)
            {
                return;
            }

            if (nivelDeConflito == IA03NivelConflito.Paz)
            {
                CancelarReposicoesPendentes();
                return;
            }

            if (!brain.HasExecutionAuthority)
            {
                return;
            }

            for (int i = reposicoesPendentes.Count - 1; i >= 0; i--)
            {
                ReposicaoPendente pendente = reposicoesPendentes[i];
                if (pendente == null || string.IsNullOrWhiteSpace(pendente.itemKey))
                {
                    reposicoesPendentes.RemoveAt(i);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(pendente.productionOrderId))
                {
                    IAProductionOrderState estado;
                    if (IAAutoProductionRegistry.TryGetOrderState(pendente.productionOrderId, out estado))
                    {
                        if (IAAutoProductionRegistry.IsOwnedByProducer(pendente.productionOrderId)
                            || estado == IAProductionOrderState.Reserved
                            || estado == IAProductionOrderState.Constructing)
                        {
                            continue;
                        }

                        if (estado == IAProductionOrderState.Queued)
                        {
                            IA_CommandStatus status = brain.Context != null
                                && brain.Context.CommandQueue != null
                                ? brain.Context.CommandQueue.GetStatus(pendente.productionOrderId)
                                : IA_CommandStatus.Cancelled;
                            if (status != IA_CommandStatus.Failed && status != IA_CommandStatus.Cancelled)
                            {
                                continue;
                            }

                            IAAutoProductionRegistry.Release(pendente.productionOrderId, now);
                        }

                        if (estado != IAProductionOrderState.Delivered)
                        {
                            pendente.quantidade = Mathf.Max(0, pendente.quantidade) + 1;
                        }
                    }
                    else
                    {
                        pendente.quantidade = Mathf.Max(0, pendente.quantidade) + 1;
                    }

                    pendente.productionOrderId = string.Empty;
                }

                if (pendente.quantidade <= 0)
                {
                    reposicoesPendentes.RemoveAt(i);
                    continue;
                }

                int prioridade = missaoAtiva != null
                    ? Mathf.Clamp(missaoAtiva.Prioridade, 1, 1000)
                    : 500;
                if (brain.TryQueueProductionForIA03Replacement(
                        brain.TeamId,
                        pendente.itemKey,
                        prioridade,
                        out string productionOrderId))
                {
                    pendente.quantidade--;
                    pendente.productionOrderId = productionOrderId;
                    // Uma reposição por ciclo estratégico mantém o custo
                    // amortizado e evita rajadas de produção após uma batalha.
                    return;
                }
            }
        }

        private void ReconstruirReposicoesDoRegistroSeNecessario()
        {
            if (reposicoesReconstruidasDoRegistro
                || brain == null
                || brain.Context == null
                || brain.TeamId <= 0)
            {
                return;
            }

            List<IAProductionOrderSaveData> ordens =
                IAAutoProductionRegistry.GetActiveOrdersForTeamByPurposePrefix(
                    brain.TeamId,
                    "ia03-replacement:");
            if (ordens == null || ordens.Count == 0)
            {
                // O registro pode ser restaurado depois deste primeiro tick.
                // Tente novamente em ciclos estratégicos futuros sem alocar
                // estado persistente adicional nem alterar o formato do save.
                return;
            }

            if (reposicoesPendentes == null)
            {
                reposicoesPendentes = new List<ReposicaoPendente>();
            }

            const string prefixo = "ia03-replacement:";
            for (int i = 0; i < ordens.Count; i++)
            {
                IAProductionOrderSaveData ordem = ordens[i];
                if (ordem == null
                    || string.IsNullOrWhiteSpace(ordem.orderId)
                    || string.IsNullOrWhiteSpace(ordem.purpose)
                    || !ordem.purpose.StartsWith(prefixo, System.StringComparison.Ordinal))
                {
                    continue;
                }

                string itemKey = ordem.purpose.Substring(prefixo.Length).Trim();
                if (string.IsNullOrWhiteSpace(itemKey))
                {
                    continue;
                }

                bool jaRegistrada = false;
                for (int j = 0; j < reposicoesPendentes.Count; j++)
                {
                    ReposicaoPendente existente = reposicoesPendentes[j];
                    if (existente != null
                        && string.Equals(existente.productionOrderId, ordem.orderId, System.StringComparison.Ordinal))
                    {
                        jaRegistrada = true;
                        break;
                    }
                }

                if (jaRegistrada)
                {
                    continue;
                }

                // Mantenha cada ordem física separada: somá-las à quantidade
                // de uma única pendência poderia reservar novamente algo que
                // já está em produção e ainda possui um orderId ativo.
                reposicoesPendentes.Add(new ReposicaoPendente
                {
                    itemKey = itemKey,
                    productionOrderId = ordem.orderId,
                    quantidade = 0
                });
            }

            reposicoesReconstruidasDoRegistro = true;
            restauracaoProducaoPendente = false;
            SolicitarReanaliseAntecipada();
        }

        private void CancelarReposicoesPendentes()
        {
            if (reposicoesPendentes == null)
            {
                return;
            }

            for (int i = 0; i < reposicoesPendentes.Count; i++)
            {
                ReposicaoPendente pendente = reposicoesPendentes[i];
                if (pendente == null || string.IsNullOrWhiteSpace(pendente.productionOrderId))
                {
                    continue;
                }

                if (IAAutoProductionRegistry.IsOwnedByProducer(pendente.productionOrderId))
                {
                    // O produtor físico assumiu a ordem; preservar o ciclo
                    // interno do estaleiro/fábrica mesmo que a IA03 pare.
                    continue;
                }

                if (brain != null
                    && brain.Context != null
                    && brain.Context.CommandQueue != null
                    && brain.Context.CommandQueue.GetStatus(pendente.productionOrderId) == IA_CommandStatus.Queued)
                {
                    brain.Context.CommandQueue.CancelPending(
                        pendente.productionOrderId,
                        Time.time,
                        "reposição estratégica cancelada");
                }

                IAAutoProductionRegistry.Release(pendente.productionOrderId, Time.time);
            }

            reposicoesPendentes.Clear();
        }

        private int ContarInimigosConhecidos(IA_WorldState mundo)
        {
            if (mundo == null || paisAlvoTeamId <= 0)
            {
                return 0;
            }

            // O relatório descreve contatos ainda conhecidos pelo WorldState,
            // inclusive a última posição lembrada após perda de visão.
            mundo.FillEnemyMemory(observacoesInimigasParaRelatorio, 120f);
            int contagem = 0;
            for (int i = 0; i < observacoesInimigasParaRelatorio.Count; i++)
            {
                IA_EnemyObservation observacao = observacoesInimigasParaRelatorio[i];
                if (observacao != null
                    && observacao.TeamId == paisAlvoTeamId
                    && !observacao.IsStructure)
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

                if (observacao.TeamId <= 0
                    || (brain != null && observacao.TeamId == brain.TeamId))
                {
                    continue;
                }

                if (paisAlvoTeamId > 0 && observacao.TeamId != paisAlvoTeamId)
                {
                    continue;
                }

                IdentidadeUnidade identidade = observacao.Transform != null
                    ? observacao.Transform.GetComponentInParent<IdentidadeUnidade>()
                    : null;
                if (identidade != null && identidade.teamID != observacao.TeamId)
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
                bool eventoNovo = relatorio.RegistrarEventoCombate(evento, brain.TeamId, paisAlvoTeamId);

                if (eventoNovo
                    && evento.tipo == "UNIDADE DESTRUÍDA"
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
            if (alvo == null || brain == null || dano <= 0f)
            {
                return;
            }

            bool danoEstrutural = alvo.ehEstrutura;
            // O evento global chega depois de vidaAtual ser subtraída. Em
            // estruturas, contabiliza só o dano efetivamente aplicado. Dano a
            // unidades também gera resposta, mas não entra no relatório estrutural.
            float danoEfetivo = danoEstrutural
                ? CalcularDanoEstruturalEfetivo(dano, alvo.vidaAtual)
                : Mathf.Min(dano, Mathf.Max(0f, alvo.vidaAtual + dano));
            if (danoEfetivo <= 0f)
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
                    if (danoEstrutural) relatorio.RegistrarDanoEstrutural(true, danoEfetivo);
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
                    // O alvo estratégico pré-configurado não é necessariamente
                    // quem iniciou hostilidades nesta partida. Em paz, o primeiro
                    // dano confirmado define o agressor real para a resposta da IA03.
                    if (nivelDeConflito == IA03NivelConflito.Paz)
                    {
                        paisAlvoTeamId = identidadeAgressor.teamID;
                    }
                }

                if (danoEstrutural) relatorio.RegistrarDanoEstrutural(false, danoEfetivo);
                if (nivelDeConflito == IA03NivelConflito.Paz)
                {
                    DefinirNivelConflito(
                        IA03NivelConflito.Tensao,
                        danoEstrutural ? "estrutura nacional sob ataque" : "unidade nacional sob ataque");
                }

                if (danoEstrutural)
                {
                    MissaoEstrategicaSO defesa = SelecionarMissaoDeDefesa(alvo.name);
                    if (defesa != null)
                    {
                        SolicitarMissaoEstrategica(defesa, "ataque a " + alvo.name, true);
                    }
                }
            }

            SolicitarReanaliseAntecipada();
        }

        private static float CalcularDanoEstruturalEfetivo(float danoSolicitado, float vidaPosteriorAoImpacto)
        {
            if (danoSolicitado <= 0f)
            {
                return 0f;
            }

            float vidaAnteriorAoImpacto = Mathf.Max(0f, vidaPosteriorAoImpacto + danoSolicitado);
            return Mathf.Min(danoSolicitado, vidaAnteriorAoImpacto);
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
                gerenteTerritorialAssinado.OnTerritoryCapturedByOccupation -= AoCapturarTerritorioPorOcupacao;
                gerenteTerritorialAssinado.OnTerritoryOccupationChanged -= AoMudarOcupacaoTerritorial;
            }

            gerenteTerritorialAssinado = atual;
            if (gerenteTerritorialAssinado != null)
            {
                gerenteTerritorialAssinado.OnTerritoryCapturedByOccupation += AoCapturarTerritorioPorOcupacao;
                gerenteTerritorialAssinado.OnTerritoryOccupationChanged += AoMudarOcupacaoTerritorial;
            }
        }

        private void AoCapturarTerritorioPorOcupacao(string territorioId, int donoAnterior, int novoDono)
        {
            bool exigeCapturaTerritorial = missaoAtiva != null
                && (missaoAtiva.CondicaoDeSucesso == IA03CondicaoMissao.CapturarTerritorio
                    || missaoAtiva.CondicaoDeFracasso == IA03CondicaoMissao.CapturarTerritorio);
            if (!exigeCapturaTerritorial || brain == null || creatyAtivo == null
                || gerenteTerritorialAssinado == null || string.IsNullOrWhiteSpace(territorioId))
            {
                return;
            }

            ResultadoConsultaTerritorio objetivo = gerenteTerritorialAssinado.ObterTerritorioNaPosicao(creatyAtivo.transform.position);
            if (!objetivo.encontrouRegiao || !string.Equals(objetivo.territorioId, territorioId, System.StringComparison.Ordinal))
            {
                return;
            }

            if (novoDono == brain.TeamId)
            {
                if (!territorioDoObjetivoCapturado)
                {
                    relatorio.RegistrarObjetivoCapturado(true);
                }
                territorioDoObjetivoCapturado = true;
                territorioDoObjetivoPerdido = false;
                SolicitarReanaliseAntecipada();
            }
            else if (donoAnterior == brain.TeamId && novoDono == equipeAlvoAtiva)
            {
                if (!territorioDoObjetivoPerdido)
                {
                    relatorio.RegistrarObjetivoCapturado(false);
                }
                territorioDoObjetivoCapturado = false;
                territorioDoObjetivoPerdido = true;
                SolicitarReanaliseAntecipada();
            }
        }

        private void AoMudarOcupacaoTerritorial(string territorioId, int ocupanteAnterior, int novoOcupante)
        {
            bool exigeCapturaTerritorial = missaoAtiva != null
                && (missaoAtiva.CondicaoDeSucesso == IA03CondicaoMissao.CapturarTerritorio
                    || missaoAtiva.CondicaoDeFracasso == IA03CondicaoMissao.CapturarTerritorio);
            if (!exigeCapturaTerritorial || brain == null || creatyAtivo == null
                || gerenteTerritorialAssinado == null || string.IsNullOrWhiteSpace(territorioId))
            {
                return;
            }

            ResultadoConsultaTerritorio objetivo = gerenteTerritorialAssinado.ObterTerritorioNaPosicao(creatyAtivo.transform.position);
            if (!objetivo.encontrouRegiao || !string.Equals(objetivo.territorioId, territorioId, System.StringComparison.Ordinal))
            {
                return;
            }

            if (novoOcupante == brain.TeamId)
            {
                if (!territorioDoObjetivoCapturado) relatorio.RegistrarObjetivoCapturado(true);
                territorioDoObjetivoCapturado = true;
                territorioDoObjetivoPerdido = false;
                SolicitarReanaliseAntecipada();
            }
            else if (ocupanteAnterior == brain.TeamId)
            {
                if (!territorioDoObjetivoPerdido) relatorio.RegistrarObjetivoCapturado(false);
                territorioDoObjetivoCapturado = false;
                territorioDoObjetivoPerdido = true;
                SolicitarReanaliseAntecipada();
            }
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
                governoAssinado.OnPropostaCriada -= AoCriarPropostaInternacional;
            }

            governoAssinado = governo;
            if (governoAssinado != null)
            {
                governoAssinado.OnGovernoAtualizado += AoAtualizarGoverno;
                governoAssinado.OnPropostaCriada += AoCriarPropostaInternacional;
            }
        }

        private void AoCriarPropostaInternacional(PropostaInternacional proposta)
        {
            if (proposta == null || proposta.tipo != TipoPropostaInternacional.ReivindicacaoTerritorial
                || brain == null || proposta.origemTeamId == brain.TeamId)
            {
                return;
            }

            SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
            DadosPaisGoverno pais = governo != null ? governo.ObterPais(brain.TeamId) : null;
            if (governo == null || pais == null) return;

            RelacaoPaisGoverno relacao = governo.ObterRelacao(brain.TeamId, proposta.origemTeamId);
            DadosPaisGoverno requerente = governo.ObterPais(proposta.origemTeamId);
            VotoConselhoMundial voto;
            if (pais.emGuerra || (relacao != null && (relacao.guerraDeclarada || relacao.sancaoAtiva)))
            {
                voto = VotoConselhoMundial.Contra;
            }
            else if (requerente != null
                && !string.IsNullOrWhiteSpace(pais.federacaoGlobal)
                && string.Equals(pais.federacaoGlobal, requerente.federacaoGlobal, System.StringComparison.Ordinal))
            {
                voto = VotoConselhoMundial.AFavor;
            }
            else if (relacao != null && relacao.ValorDiplomaticoEstrategico >= 45)
            {
                voto = VotoConselhoMundial.AFavor;
            }
            else if (relacao != null && (relacao.ValorDiplomaticoEstrategico <= -30
                || relacao.PosturaDe(brain.TeamId) == PosturaRelacaoPais.Inimigo))
            {
                voto = VotoConselhoMundial.Contra;
            }
            else
            {
                voto = VotoConselhoMundial.Abster;
            }

            if (governo.TentarRegistrarVotoTerritorial(proposta.id, brain.TeamId, voto, out _))
            {
                ultimaDecisao = "voto " + voto + " na reivindicação de " + governo.NomePais(proposta.origemTeamId);
                RegistrarLogDebugSeNecessario();
            }
        }

        private void AvaliarVotosConselho(SistemaGovernoMundial governo, float now)
        {
            if (governo == null || brain == null || brain.TeamId <= 0
                || brain.TeamId == governo.teamJogador
                || now < proximaAvaliacaoVotosConselhoEm)
            {
                return;
            }

            proximaAvaliacaoVotosConselhoEm = now + 3f + (brain.TeamId % 5) * 0.25f;
            foreach (PropostaInternacional proposta in governo.ObterReivindicacoesTerritoriaisParaVoto(brain.TeamId))
            {
                VotoTerritorialConselho votoAtual = proposta.votosConselho != null
                    ? proposta.votosConselho.FirstOrDefault(voto => voto != null && voto.teamId == brain.TeamId)
                    : null;
                if (votoAtual == null || votoAtual.voto != VotoConselhoMundial.SemVoto) continue;
                AoCriarPropostaInternacional(proposta);
            }
        }

        private void AvaliarReivindicacoesTerritoriais(
            SistemaGovernoMundial governo,
            DadosPaisGoverno pais,
            float now)
        {
            if (governo == null || pais == null || perfilPais == null || brain == null
                || brain.TeamId <= 0 || brain.TeamId == governo.teamJogador
                || now < proximaAvaliacaoReivindicacaoTerritorialEm)
            {
                return;
            }

            proximaAvaliacaoReivindicacaoTerritorialEm = now + 90f + (brain.TeamId % 17);
            if (pais.emGuerra || pais.estabilidade < 35f || pais.legitimidadeGlobal < 35f
                || (pais.nivelDiplomatico < 38 && perfilPais.Diplomacia < 0.60f)
                || (perfilPais.Agressividade > 0.90f && perfilPais.Diplomacia < 0.30f))
            {
                return;
            }

            GerenteDeTerritorio gerenteTerritorial = GerenteDeTerritorio.Instancia;
            DadosMapaTerritorial mapaPolitico = gerenteTerritorial != null ? gerenteTerritorial.MapaPolitico : null;
            List<RegiaoPolitica> territoriosEstrategicos = governo.ObterTerritoriosElegiveisParaReivindicacao()
                .Where(regiao => regiao != null)
                .OrderByDescending(regiao => GestorDiplomaciaIA03.CalcularPontuacaoTerritorial(
                    mapaPolitico,
                    regiao,
                    brain.TeamId))
                .ThenBy(regiao => regiao.territorioId, System.StringComparer.Ordinal)
                .ToList();

            foreach (RegiaoPolitica regiao in territoriosEstrategicos)
            {
                if (regiao == null || string.IsNullOrWhiteSpace(regiao.territorioId)) continue;
                float pontuacaoTerritorial = GestorDiplomaciaIA03.CalcularPontuacaoTerritorial(
                    mapaPolitico,
                    regiao,
                    brain.TeamId);
                if (pontuacaoTerritorial < GestorDiplomaciaIA03.PontuacaoMinimaParaReivindicarTerritorio)
                {
                    continue;
                }

                bool jaReivindicou = governo.Propostas != null && governo.Propostas.Any(proposta => proposta != null
                    && proposta.EstaPendente
                    && proposta.tipo == TipoPropostaInternacional.ReivindicacaoTerritorial
                    && proposta.origemTeamId == brain.TeamId
                    && proposta.territoriosReivindicados != null
                    && proposta.territoriosReivindicados.Contains(regiao.territorioId));
                if (jaReivindicou) continue;

                if (governo.TentarCriarReivindicacaoTerritorial(brain.TeamId, regiao.territorioId, out _))
                {
                    ultimaDecisao = "solicitou reconhecimento internacional para "
                        + (string.IsNullOrWhiteSpace(regiao.nome) ? regiao.territorioId : regiao.nome);
                    RegistrarLogDebugSeNecessario();
                    break;
                }
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
                IA03TipoCreaty primeiroTipoConfigurado = missao.CreatysPermitidos[0];
                for (int i = 0; i < missao.CreatysPermitidos.Count; i++)
                {
                    IA03TipoCreaty tipo = missao.CreatysPermitidos[i];
                    if (missao.Aceita(nivelDeConflito, tipo))
                    {
                        return tipo;
                    }
                }

                // Mantém o primeiro tipo como fallback para que Aceita continue
                // bloqueando a missão quando nenhum tipo configurado é válido.
                return primeiroTipoConfigurado;
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
