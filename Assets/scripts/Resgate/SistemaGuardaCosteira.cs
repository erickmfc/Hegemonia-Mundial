using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Núcleo compartilhado da Guarda Costeira. Trabalha por TeamId para jogador,
/// IA01 e IA02 e delega qualquer movimento aos controladores existentes.
/// </summary>
[DefaultExecutionOrder(-620)]
public sealed class SistemaGuardaCosteira : MonoBehaviour
{
    public static SistemaGuardaCosteira Instancia { get; private set; }

    [Header("Operacao")]
    public bool criarIncidentesAutomaticamente = true;
    public bool despachoAutomatico = true;
    [Min(1f)] public float intervaloDespachoAutomatico = 2f;
    [Min(1f)] public float areaBuscaPadrao = 50f;
    [Min(1)] public int diasJanelaMaxima = 10;

    [Header("Estado")]
    [SerializeField] private List<RescueIncident> incidentes = new List<RescueIncident>();

    private readonly List<GuardaCosteiraUnidade> unidades = new List<GuardaCosteiraUnidade>(32);
    private readonly HashSet<int> perdasPessoaisAdiadas = new HashSet<int>();
    private readonly HashSet<int> mortesProcessadas = new HashSet<int>();
    private float proximoDespacho;
    private int sequenciaIncidente;
    private bool inscritoNoRelogio;
    private bool avisouPrefabBalsaAusente;

    public IReadOnlyList<RescueIncident> Incidentes => incidentes;
    public bool ModoAutomatico { get { return despachoAutomatico; } set { despachoAutomatico = value; } }

    public event Action<RescueIncident> IncidenteCriado;
    public event Action<RescueIncident> IncidenteAtualizado;

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;

#if UNITY_2023_1_OR_NEWER
        SistemaGuardaCosteira existente = FindFirstObjectByType<SistemaGuardaCosteira>();
#else
        SistemaGuardaCosteira existente = FindObjectOfType<SistemaGuardaCosteira>();
#endif
        if (existente != null)
        {
            Instancia = existente;
            return;
        }

        GameObject objeto = new GameObject("SistemaGuardaCosteira_Runtime");
        Instancia = objeto.AddComponent<SistemaGuardaCosteira>();
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
        SistemaDeDanos.OnMorteGlobal += AoMorrerUnidade;
        InscreverNoRelogio();
    }

    private void Start()
    {
        InscreverNoRelogio();
    }

    private void OnDisable()
    {
        SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
        if (inscritoNoRelogio && GerenciadorTempo.Instancia != null)
            GerenciadorTempo.Instancia.OnDataAlterada -= AoAvancarData;
        inscritoNoRelogio = false;
    }

    private void InscreverNoRelogio()
    {
        if (inscritoNoRelogio || GerenciadorTempo.Instancia == null) return;
        GerenciadorTempo.Instancia.OnDataAlterada += AoAvancarData;
        inscritoNoRelogio = true;
    }

    private void Update()
    {
        if (Hegemonia.RTS.RTSSimulationClock.SimulacaoPausada) return;
        AtualizarJanelas();
        if (despachoAutomatico && Time.unscaledTime >= proximoDespacho)
        {
            proximoDespacho = Time.unscaledTime + Mathf.Max(0.25f, intervaloDespachoAutomatico);
            DespacharAutomaticamente();
        }
    }

    private void AoAvancarData()
    {
        AtualizarJanelas();
        DespacharAutomaticamente();
    }

    public void RegistrarUnidade(GuardaCosteiraUnidade unidade)
    {
        if (unidade != null && !unidades.Contains(unidade)) unidades.Add(unidade);
    }

    public void RemoverUnidade(GuardaCosteiraUnidade unidade)
    {
        if (unidade != null) unidades.Remove(unidade);
    }

    public RescueIncident ObterIncidente(string incidentId)
    {
        if (string.IsNullOrWhiteSpace(incidentId)) return null;
        return incidentes.FirstOrDefault(x => x != null && x.IncidentId == incidentId);
    }

    public List<RescueIncident> CapturarEstadoSave()
    {
        List<RescueIncident> copia = new List<RescueIncident>(incidentes.Count);
        for (int i = 0; i < incidentes.Count; i++)
        {
            RescueIncident incidente = incidentes[i];
            if (incidente != null) copia.Add(ClonarIncidente(incidente));
        }
        return copia;
    }

    public void RestaurarEstadoSave(IReadOnlyList<RescueIncident> estadoSalvo)
    {
        for (int i = 0; i < incidentes.Count; i++)
        {
            RescueIncident existente = incidentes[i];
            if (existente != null) BalsaSalvamento.RemoverDoIncidente(existente.IncidentId);
        }
        incidentes.Clear();
        perdasPessoaisAdiadas.Clear();
        mortesProcessadas.Clear();
        sequenciaIncidente = 0;
        if (estadoSalvo == null) return;

        for (int i = 0; i < estadoSalvo.Count; i++)
        {
            RescueIncident salvo = estadoSalvo[i];
            if (salvo == null || string.IsNullOrWhiteSpace(salvo.IncidentId) || salvo.OwnerTeamId <= 0)
                continue;

            RescueIncident restaurado = ClonarIncidente(salvo);
            BalsaSalvamento.RemoverDoIncidente(restaurado.IncidentId);
            int pessoasEmTransito = Mathf.Clamp(restaurado.OnboardRescueUnits, 0,
                Mathf.Min(Mathf.Max(0, restaurado.RescuedPersonnel),
                    Mathf.Max(0, restaurado.OriginalPersonnel - restaurado.ReturnedPersonnel - restaurado.LostPersonnel)));

            // Unidades de resgate são recriadas pelo SaveGame sem a missão em
            // curso. Recoloca passageiros na ocorrência para novo despacho,
            // sem alterar o efetivo já salvo.
            if (pessoasEmTransito > 0 && restaurado.IsActiveAt(DiaAtual()))
            {
                restaurado.RescuedPersonnel = Mathf.Max(0, restaurado.RescuedPersonnel - pessoasEmTransito);
                restaurado.MissingPersonnel = Mathf.Max(0, restaurado.MissingPersonnel) + pessoasEmTransito;
            }
            else if (pessoasEmTransito > 0)
            {
                // Passageiros a bordo quando o sinal já expirou são tratados
                // como resgatados com segurança para não ficarem sem unidade.
                restaurado.ReturnedPersonnel += pessoasEmTransito;
                AdicionarAoEfetivo(restaurado.OwnerTeamId, pessoasEmTransito);
            }
            restaurado.UsaBalsasSalvamento = restaurado.UsaBalsasSalvamento
                || restaurado.IncidentType == RescueIncidentType.NavioAfundado;
            restaurado.ReservedForPickup = 0;
            restaurado.OnboardRescueUnits = 0;
            restaurado.AssignedUnits.Clear();
            if (restaurado.UsaBalsasSalvamento && restaurado.IsActiveAt(DiaAtual()))
                CriarBalsasParaIncidente(restaurado, restaurado.MissingPersonnel, null);
            incidentes.Add(restaurado);
            IncidenteAtualizado?.Invoke(restaurado);

            const string prefixo = "RESCUE-";
            if (restaurado.IncidentId.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(restaurado.IncidentId.Substring(prefixo.Length), out int sequencia))
            {
                sequenciaIncidente = Mathf.Max(sequenciaIncidente, sequencia);
            }
        }

        proximoDespacho = Time.unscaledTime;
    }

    public void LimparEstadoParaNovoJogo()
    {
        for (int i = 0; i < incidentes.Count; i++)
        {
            RescueIncident existente = incidentes[i];
            if (existente != null) BalsaSalvamento.RemoverDoIncidente(existente.IncidentId);
        }
        incidentes.Clear();
        perdasPessoaisAdiadas.Clear();
        mortesProcessadas.Clear();
        sequenciaIncidente = 0;
        proximoDespacho = Time.unscaledTime;
    }

    private static RescueIncident ClonarIncidente(RescueIncident origem)
    {
        return new RescueIncident
        {
            IncidentId = origem.IncidentId,
            OwnerTeamId = origem.OwnerTeamId,
            OwnerNationId = origem.OwnerNationId,
            Position = origem.Position,
            SearchAreaSize = origem.SearchAreaSize,
            OriginalPersonnel = origem.OriginalPersonnel,
            MissingPersonnel = origem.MissingPersonnel,
            RescuedPersonnel = origem.RescuedPersonnel,
            ReservedForPickup = origem.ReservedForPickup,
            OnboardRescueUnits = origem.OnboardRescueUnits,
            ReturnedPersonnel = origem.ReturnedPersonnel,
            LostPersonnel = origem.LostPersonnel,
            CreatedAtGameDay = origem.CreatedAtGameDay,
            CreatedAtRealtime = origem.CreatedAtRealtime,
            LastUpdatedGameDay = origem.LastUpdatedGameDay,
            IncidentType = origem.IncidentType,
            UsaBalsasSalvamento = origem.UsaBalsasSalvamento,
            ThreatLevel = origem.ThreatLevel,
            State = origem.State,
            IsSignalActive = origem.IsSignalActive,
            IsCompleted = origem.IsCompleted,
            SourceName = origem.SourceName,
            LastReason = origem.LastReason,
            AssignedUnits = origem.AssignedUnits != null
                ? new List<string>(origem.AssignedUnits)
                : new List<string>()
        };
    }

    public RescueIncident CriarIncidente(int ownerTeamId, int ownerNationId, Vector3 position,
        int personnel, RescueIncidentType type, string sourceName, int threatLevel = 0,
        float searchAreaSize = -1f)
    {
        bool usarBalsasSalvamento = type == RescueIncidentType.NavioAfundado;
        return CriarIncidenteInterno(ownerTeamId, ownerNationId, position, personnel, type,
            sourceName, threatLevel, searchAreaSize, true, usarBalsasSalvamento);
    }

    private RescueIncident CriarIncidenteInterno(int ownerTeamId, int ownerNationId, Vector3 position,
        int personnel, RescueIncidentType type, string sourceName, int threatLevel,
        float searchAreaSize, bool removerDoEfetivo, bool usarBalsasSalvamento = false,
        SistemaDeDanos danosOrigem = null)
    {
        personnel = Mathf.Max(0, personnel);
        if (personnel <= 0) return null;

        int day = DiaAtual();
        RescueIncident incidente = new RescueIncident
        {
            IncidentId = "RESCUE-" + (++sequenciaIncidente).ToString("000"),
            OwnerTeamId = Mathf.Max(1, ownerTeamId),
            OwnerNationId = Mathf.Max(0, ownerNationId),
            Position = position,
            SearchAreaSize = searchAreaSize > 0f ? searchAreaSize : areaBuscaPadrao,
            OriginalPersonnel = personnel,
            MissingPersonnel = personnel,
            CreatedAtGameDay = day,
            CreatedAtRealtime = Time.unscaledTime,
            LastUpdatedGameDay = day,
            IncidentType = type,
            UsaBalsasSalvamento = usarBalsasSalvamento,
            ThreatLevel = Mathf.Clamp(threatLevel, 0, 3),
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? "UNIDADE PERDIDA" : sourceName,
            LastReason = "INCIDENTE CRIADO"
        };
        incidentes.Add(incidente);
        if (removerDoEfetivo) RemoverDoEfetivo(incidente.OwnerTeamId, personnel);
        if (incidente.UsaBalsasSalvamento)
            CriarBalsasParaIncidente(incidente, personnel, danosOrigem);
        IncidenteCriado?.Invoke(incidente);
        return incidente;
    }

    public bool EnviarResgate(string incidentId, GuardaCosteiraUnidade unidade)
    {
        RescueIncident incidente = ObterIncidente(incidentId);
        if (incidente == null || unidade == null || !incidente.IsActiveAt(DiaAtual())) return false;
        if (unidade.TeamId != incidente.OwnerTeamId || !unidade.PodeSerDespachada) return false;
        if (incidente.AssignedUnits.Contains(unidade.RescueUnitId)) return true;
        if (incidente.AvailableForReservation(DiaAtual()) <= 0) return false;
        if (!unidade.DesignarResgate(incidente)) return false;
        incidente.AssignedUnits.Add(unidade.RescueUnitId);
        incidente.LastReason = "UNIDADE DESIGNADA";
        IncidenteAtualizado?.Invoke(incidente);
        return true;
    }

    public int ReservarEPegar(string incidentId, GuardaCosteiraUnidade unidade, int quantidade)
    {
        RescueIncident incidente = ObterIncidente(incidentId);
        if (incidente == null || unidade == null || quantidade <= 0 || !incidente.IsActiveAt(DiaAtual())) return 0;
        if (!incidente.AssignedUnits.Contains(unidade.RescueUnitId)) return 0;

        int reservado = Mathf.Min(Mathf.Max(0, quantidade), incidente.AvailableForReservation(DiaAtual()));
        if (incidente.UsaBalsasSalvamento && BalsaSalvamento.ExisteParaIncidente(incidentId))
            reservado = Mathf.Min(reservado, BalsaSalvamento.ContarSobreviventes(incidentId));
        if (reservado <= 0) return 0;
        if (incidente.UsaBalsasSalvamento && BalsaSalvamento.ExisteParaIncidente(incidentId))
        {
            int resgatadosFisicos = BalsaSalvamento.ResgatarSobreviventes(incidentId, reservado);
            reservado = Mathf.Min(reservado, resgatadosFisicos);
            if (reservado <= 0) return 0;
        }
        incidente.ReservedForPickup += reservado;
        incidente.ReservedForPickup -= reservado;
        incidente.MissingPersonnel = Mathf.Max(0, incidente.MissingPersonnel - reservado);
        incidente.RescuedPersonnel += reservado;
        incidente.OnboardRescueUnits += reservado;
        incidente.LastReason = "PESSOAS LOCALIZADAS";
        IncidenteAtualizado?.Invoke(incidente);
        return reservado;
    }

    public int Desembarcar(string incidentId, GuardaCosteiraUnidade unidade, int quantidade)
    {
        RescueIncident incidente = ObterIncidente(incidentId);
        if (incidente == null || unidade == null || quantidade <= 0) return 0;
        int entregue = Mathf.Min(quantidade, incidente.OnboardRescueUnits);
        if (entregue <= 0) return 0;
        incidente.OnboardRescueUnits -= entregue;
        incidente.ReturnedPersonnel += entregue;
        incidente.LastReason = "DESEMBARQUE CONCLUIDO";
        AdicionarAoEfetivo(incidente.OwnerTeamId, entregue);
        IncidenteAtualizado?.Invoke(incidente);
        return entregue;
    }

    public void LiberarUnidade(GuardaCosteiraUnidade unidade)
    {
        if (unidade == null) return;
        RescueIncident incidente = ObterIncidente(unidade.IncidentIdAtual);
        if (incidente != null) incidente.AssignedUnits.Remove(unidade.RescueUnitId);
        IncidenteAtualizado?.Invoke(incidente);
    }

    public void RegistrarPerdaDaUnidadeDeResgate(GuardaCosteiraUnidade unidade)
    {
        if (unidade == null || unidade.PessoasEmbarcadas <= 0) return;
        RescueIncident anterior = ObterIncidente(unidade.IncidentIdAtual);
        if (anterior != null)
        {
            anterior.OnboardRescueUnits = Mathf.Max(0, anterior.OnboardRescueUnits - unidade.PessoasEmbarcadas);
            anterior.RescuedPersonnel = Mathf.Max(0, anterior.RescuedPersonnel - unidade.PessoasEmbarcadas);
            anterior.LastReason = "UNIDADE DE RESGATE PERDIDA";
        }
        IdentidadeUnidade identidade = SistemaDeDanos.ResolverIdentidade(unidade);
        bool usarBalsa = identidade != null && identidade.tipoUnidade == TipoUnidade.Naval;
        CriarIncidenteInterno(unidade.TeamId, unidade.NationId, unidade.transform.position,
            unidade.PessoasEmbarcadas, RescueIncidentType.UnidadeDeResgateDestruida,
            unidade.name, 2, unidade.AreaBusca, false, usarBalsa,
            unidade.GetComponent<SistemaDeDanos>() ?? unidade.GetComponentInParent<SistemaDeDanos>());
        unidade.LimparPessoasEmbarcadas();
    }

    public bool DeveAdiarPerdaDePessoal(IdentidadeUnidade identidade)
    {
        if (identidade == null) return false;
        return perdasPessoaisAdiadas.Contains(identidade.GetInstanceID());
    }

    public void ConsumirPerdaAdiada(IdentidadeUnidade identidade)
    {
        if (identidade != null) perdasPessoaisAdiadas.Remove(identidade.GetInstanceID());
    }

    private void AoMorrerUnidade(SistemaDeDanos danos, GameObject agressor)
    {
        if (!criarIncidentesAutomaticamente || danos == null) return;
        IdentidadeUnidade identidade = SistemaDeDanos.ResolverIdentidade(danos);
        if (identidade == null || identidade.teamID <= 0 || identidade.tipoUnidade == TipoUnidade.Estrutura) return;
        if (!mortesProcessadas.Add(identidade.GetInstanceID())) return;

        GuardaCosteiraUnidade unidadeResgate = danos.GetComponent<GuardaCosteiraUnidade>()
            ?? danos.GetComponentInParent<GuardaCosteiraUnidade>()
            ?? danos.GetComponentInChildren<GuardaCosteiraUnidade>(true);
        if (unidadeResgate != null) RegistrarPerdaDaUnidadeDeResgate(unidadeResgate);

        int pessoal = PessoalDaUnidade(identidade);
        if (pessoal <= 0) return;
        perdasPessoaisAdiadas.Add(identidade.GetInstanceID());
        RescueIncidentType tipo = ResolverTipoIncidente(identidade, unidadeResgate != null);
        bool usarBalsa = identidade.tipoUnidade == TipoUnidade.Naval;
        CriarIncidenteInterno(identidade.teamID, identidade.teamID, identidade.transform.position,
            pessoal, tipo, identidade.name, 0, -1f, true, usarBalsa, danos);
    }

    private void AtualizarJanelas()
    {
        int dia = DiaAtual();
        for (int i = incidentes.Count - 1; i >= 0; i--)
        {
            RescueIncident incidente = incidentes[i];
            if (incidente == null) { incidentes.RemoveAt(i); continue; }
            if (incidente.State != RescueIncidentState.Ativo) continue;

            int maximo = incidente.MaxRecoverable(dia);
            int aindaRastreado = incidente.MissingPersonnel + incidente.ReservedForPickup;
            int limiteRestante = Mathf.Max(0, maximo - incidente.RescuedPersonnel);
            int perdaPorJanela = Mathf.Max(0, aindaRastreado - limiteRestante);
            if (dia > incidente.CreatedAtGameDay && perdaPorJanela > 0)
            {
                RegistrarPerdaDefinitiva(incidente, perdaPorJanela, "JANELA DE SOBREVIVENCIA REDUZIDA");
            }

            if (incidente.AgeDays(dia) > diasJanelaMaxima || incidente.AgeDays(dia) > 10)
                EncerrarPorSinalPerdido(incidente);
            else if (incidente.MissingPersonnel <= 0 && incidente.OnboardRescueUnits <= 0)
            {
                incidente.State = RescueIncidentState.Concluido;
                incidente.IsCompleted = true;
                incidente.IsSignalActive = false;
            }
            incidente.LastUpdatedGameDay = dia;
            IncidenteAtualizado?.Invoke(incidente);
        }
    }

    private void DespacharAutomaticamente()
    {
        int dia = DiaAtual();
        for (int i = 0; i < incidentes.Count; i++)
        {
            RescueIncident incidente = incidentes[i];
            if (incidente == null || !incidente.IsActiveAt(dia) || incidente.AvailableForReservation(dia) <= 0) continue;
            int capacidadeDespachada = 0;
            List<GuardaCosteiraUnidade> candidatas = unidades
                .Where(x => x != null && x.TeamId == incidente.OwnerTeamId && x.PodeSerDespachada)
                .OrderBy(x => Vector3.Distance(x.transform.position, incidente.Position))
                .ThenByDescending(x => x.CapacidadeResgate)
                .ToList();
            for (int u = 0; u < candidatas.Count; u++)
            {
                GuardaCosteiraUnidade unidade = candidatas[u];
                if (capacidadeDespachada >= incidente.AvailableForReservation(dia)) break;
                if (!EnviarResgate(incidente.IncidentId, unidade)) continue;
                capacidadeDespachada += unidade.CapacidadeResgate;
            }
        }
    }

    private void EncerrarPorSinalPerdido(RescueIncident incidente)
    {
        if (incidente == null || incidente.State != RescueIncidentState.Ativo) return;
        int restante = incidente.MissingPersonnel + incidente.ReservedForPickup;
        if (restante > 0) RegistrarPerdaDefinitiva(incidente, restante, "SINAL PERDIDO APOS 10 DIAS");
        incidente.ReservedForPickup = 0;
        incidente.IsSignalActive = false;
        incidente.State = RescueIncidentState.SinalPerdido;
        incidente.LastReason = "SINAL PERDIDO";
    }

    private void RegistrarPerdaDefinitiva(RescueIncident incidente, int quantidade, string motivo)
    {
        if (incidente == null || quantidade <= 0) return;
        int perdaReservada = Mathf.Min(incidente.ReservedForPickup, quantidade);
        incidente.ReservedForPickup -= perdaReservada;
        int perdaAusente = Mathf.Min(incidente.MissingPersonnel, quantidade - perdaReservada);
        incidente.MissingPersonnel -= perdaAusente;
        incidente.LostPersonnel += perdaReservada + perdaAusente;
        if (incidente.UsaBalsasSalvamento)
            BalsaSalvamento.PerderSobreviventes(incidente.IncidentId, perdaReservada + perdaAusente);
        incidente.LastReason = motivo;
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(incidente.OwnerTeamId) : null;
        if (pais != null) pais.mortosAcumulados += perdaReservada + perdaAusente;
    }

    private static int DiaAtual()
    {
        return GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
    }

    private void CriarBalsasParaIncidente(RescueIncident incidente, int sobreviventes, SistemaDeDanos danosOrigem)
    {
        if (incidente == null || sobreviventes <= 0) return;

        GameObject prefab = Resources.Load<GameObject>("Resgate/BalsaSalvamento");
        if (prefab == null)
        {
            if (!avisouPrefabBalsaAusente)
            {
                Debug.LogWarning("[SistemaGuardaCosteira] Prefab Resources/Resgate/BalsaSalvamento nao encontrado; o incidente SAR continuara sem a visualizacao fisica das balsas.", this);
                avisouPrefabBalsaAusente = true;
            }
            return;
        }

        BalsaSalvamento.RemoverDoIncidente(incidente.IncidentId);
        int capacidade = BalsaSalvamento.CapacidadePadrao;
        int quantidadeBalsas = Mathf.CeilToInt(sobreviventes / (float)capacidade);
        float raioBase = 8f;
        if (danosOrigem != null)
        {
            Renderer[] renderizadores = danosOrigem.GetComponentsInChildren<Renderer>(true);
            float raioCasco = 0f;
            for (int i = 0; i < renderizadores.Length; i++)
            {
                Renderer renderizador = renderizadores[i];
                if (renderizador == null || renderizador is ParticleSystemRenderer) continue;
                raioCasco = Mathf.Max(raioCasco,
                    Mathf.Max(renderizador.bounds.extents.x, renderizador.bounds.extents.z));
            }
            if (raioCasco > 0f) raioBase = Mathf.Clamp(raioCasco + 5f, 8f, 60f);
        }

        Vector3 centro = incidente.Position;
        if (RegistroSuperficieMapa.TryGetAltura(centro, TipoSuperficieMapa.Agua, out float alturaCentro))
            centro.y = alturaCentro + 0.04f;

        int restantes = sobreviventes;
        uint hash = 2166136261u;
        unchecked
        {
            for (int i = 0; i < incidente.IncidentId.Length; i++)
                hash = (hash ^ incidente.IncidentId[i]) * 16777619u;
        }
        float anguloInicial = (hash % 36000u) * (Mathf.PI / 18000f);
        const float passoAngular = 2.39996323f; // Angulo dourado: distribui balsas sem empilhar.
        const int balsasPorAnel = 12;

        for (int i = 0; i < quantidadeBalsas && restantes > 0; i++)
        {
            int anel = i / balsasPorAnel;
            float angulo = anguloInicial + i * passoAngular;
            float raio = raioBase + anel * 5.5f;
            Vector3 deslocamento = new Vector3(Mathf.Cos(angulo) * raio, 0f, Mathf.Sin(angulo) * raio);
            Vector3 posicao = centro + deslocamento;
            if (RegistroSuperficieMapa.TryGetAltura(posicao, TipoSuperficieMapa.Agua, out float alturaBalsa))
                posicao.y = alturaBalsa + 0.04f;

            GameObject objeto = Instantiate(prefab, posicao, Quaternion.Euler(0f, angulo * Mathf.Rad2Deg, 0f));
            BalsaSalvamento balsa = objeto.GetComponent<BalsaSalvamento>();
            if (balsa == null)
            {
                Debug.LogError("[SistemaGuardaCosteira] O prefab BalsaSalvamento esta sem o componente BalsaSalvamento.", objeto);
                Destroy(objeto);
                break;
            }

            int ocupantes = Mathf.Min(capacidade, restantes);
            balsa.Configurar(incidente.IncidentId, ocupantes);
            restantes -= ocupantes;
        }
    }

    private static int PessoalDaUnidade(IdentidadeUnidade identidade)
    {
        if (identidade == null) return 0;
        if (identidade.militaresConsumidos > 0) return identidade.militaresConsumidos;
        switch (identidade.tipoUnidade)
        {
            case TipoUnidade.Aereo: return 5;
            case TipoUnidade.Naval: return 15;
            case TipoUnidade.Veiculo: return 4;
            case TipoUnidade.Infantaria: return 1;
            default: return 0;
        }
    }

    private static RescueIncidentType ResolverTipoIncidente(IdentidadeUnidade identidade, bool unidadeResgate)
    {
        if (unidadeResgate) return RescueIncidentType.UnidadeDeResgateDestruida;
        if (identidade.tipoUnidade == TipoUnidade.Naval) return RescueIncidentType.NavioAfundado;
        if (identidade.tipoUnidade == TipoUnidade.Aereo)
        {
            return identidade.GetComponentInParent<Helicoptero>() != null
                ? RescueIncidentType.HelicopteroDestruido : RescueIncidentType.AeronaveAbatida;
        }
        return RescueIncidentType.VeiculoPerdido;
    }

    private static void RemoverDoEfetivo(int teamId, int quantidade)
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(teamId) : null;
        if (pais == null) return;
        pais.populacaoMilitarAtiva = Mathf.Max(0, pais.populacaoMilitarAtiva - quantidade);
        AtualizarPopulacao(pais);
    }

    private static void AdicionarAoEfetivo(int teamId, int quantidade)
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(teamId) : null;
        if (pais == null) return;
        pais.populacaoMilitarAtiva += Mathf.Max(0, quantidade);
        AtualizarPopulacao(pais);
    }

    private static void AtualizarPopulacao(DadosPaisGoverno pais)
    {
        pais.populacao = Mathf.Clamp(pais.populacaoCivil + pais.populacaoMilitarAtiva + pais.reservistas + pais.alistaveis, 0, pais.populacaoMaxima);
    }
}
