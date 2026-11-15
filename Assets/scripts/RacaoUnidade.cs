using System;
using UnityEngine;
using UnityEngine.AI;

public enum ClasseRacaoUnidade
{
    Infantaria = 0,
    VeiculoTerrestre = 1,
    Naval = 2,
    Aerea = 3
}

/// <summary>
/// Sistema de racao / comida das unidades do jogador (Soldados, Tanques, Navios e Avioes).
/// Consome estoque individual durante operacao, cobra comida nacional no reabastecimento
/// e aplica dano por fome sem atribuir agressao inimiga quando a reserva zera.
/// </summary>
[DisallowMultipleComponent]
public class RacaoUnidade : MonoBehaviour
{
    private const float LimiarAlertaPercentual = 0.25f;
    private const float IntervaloChecagemBase = 2.0f;

    [Header("Configuracao de Racao")]
    public bool usaRacao = true;
    public ClasseRacaoUnidade classe = ClasseRacaoUnidade.Infantaria;
    [Min(1f)] public float capacidade = 420f;
    public float racaoAtual = -1f;

    [Header("Consumo (pontos por segundo)")]
    [Min(0f)] public float consumoBasePorSegundo = 1.0f;
    [Min(1f)] public float multiplicadorEmAtividade = 1.25f;
    [Min(0.0001f)] public float comidaNacionalPorPontoRacao = 0.0012f;

    [Header("Fome (ao zerar)")]
    [Min(1f)] public float atrasoParaFomeSegundos = 20f;
    [Range(0.005f, 0.25f)] public float danoFomePorSegundoPercentual = 0.04f;
    public float tempoSemRacao;

    [Header("Indicador Visual")]
    public bool mostrarBarraNoMundo = true;
    public float distanciaMaximaBarra = 900f;

    private bool configurado;
    private bool preenchimentoInicialSolicitado = true;
    private Vector3 ultimaPosicao;
    private IdentidadeUnidade identidadeCache;
    private ControleUnidade controleCache;
    private SistemaDeDanos danosCache;
    private CombustivelUnidade combustivelCache;
    private NavMeshAgent agenteCache;
    private ControleAviao aviaoCache;
    private ControleAviaoCaca cacaCache;
    private Helicoptero helicopteroCache;
    private C700TransporteAereo c700Cache;
    private NavioAbastecimento navioAbastecimentoCache;
    private TripulacaoUnidade tripulacaoCache;
    private Camera cameraCache;
    private float proximaTentativaAutoAbastecimento;

    private static GUIStyle estiloAlertaFome;
    private static GUIStyle estiloRotuloFome;

    public bool UsaRacao => usaRacao && capacidade > 0f && EhUnidadeDoJogador();
    public float Capacidade => Mathf.Max(0f, capacidade);
    public float RacaoAtual => Mathf.Clamp(racaoAtual < 0f ? Capacidade : racaoAtual, 0f, Capacidade);
    public float Percentual => Capacidade > 0.01f ? Mathf.Clamp01(RacaoAtual / Capacidade) : 1f;
    public bool EstaBaixo => UsaRacao && Percentual <= LimiarAlertaPercentual;
    public bool EstaVazio => UsaRacao && RacaoAtual <= 0.01f;
    public bool EstaFaminto => EstaVazio && tempoSemRacao >= atrasoParaFomeSegundos;

    private void Awake()
    {
        CachearReferencias();
        PrepararPerfilInicialSemZerarCarga();
    }

    private void Start()
    {
        CachearReferencias();
        ConfigurarSeNecessario(preenchimentoInicialSolicitado);
    }

    private void PrepararPerfilInicialSemZerarCarga()
    {
        if (configurado)
        {
            return;
        }

        if (identidadeCache == null)
        {
            identidadeCache = GetComponent<IdentidadeUnidade>()
                ?? GetComponentInParent<IdentidadeUnidade>()
                ?? GetComponentInChildren<IdentidadeUnidade>(true);
        }

        if (!DeveUsarRacao(gameObject, identidadeCache))
        {
            usaRacao = false;
            capacidade = 0f;
            racaoAtual = 0f;
            configurado = true;
            return;
        }

        DeterminarPerfilRacao();
        ultimaPosicao = transform.position;
        configurado = true;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ConfigurarSeNecessario(false);
        if (!UsaRacao)
        {
            return;
        }

        if (danosCache == null)
        {
            danosCache = GetComponent<SistemaDeDanos>();
        }

        if (danosCache != null && danosCache.Morreu)
        {
            return;
        }

        // O proprio navio de abastecimento alimenta sua tripulacao a partir da despensa de bordo enquanto houver carga.
        if (navioAbastecimentoCache != null && RacaoAtual < Capacidade * 0.5f && navioAbastecimentoCache.RacaoTotal > 0f)
        {
            float repor = Mathf.Min(Capacidade - RacaoAtual, navioAbastecimentoCache.RacaoTotal);
            if (repor > 0.01f)
            {
                float consumidoDoPorao = navioAbastecimentoCache.ConsumirRacaoParaUnidade(repor);
                Abastecer(consumidoDoPorao);
            }
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        if (classe == ClasseRacaoUnidade.Aerea)
        {
            AtualizarConsumoAereo(dt);
        }
        else
        {
            AtualizarConsumoContinuo(dt);
        }

        if (Time.time >= proximaTentativaAutoAbastecimento && RacaoAtual < Capacidade * 0.99f)
        {
            proximaTentativaAutoAbastecimento = Time.time + IntervaloChecagemBase;
            TentarReabastecerEmEstacaoProxima();
        }
    }

    private void AtualizarConsumoAereo(float dt)
    {
        if (!EstaEmVooAtivo())
        {
            ultimaPosicao = transform.position;
            return;
        }

        float consumo = consumoBasePorSegundo * multiplicadorEmAtividade * dt;
        AplicarConsumoEFome(consumo, dt);
    }

    private void AtualizarConsumoContinuo(float dt)
    {
        bool emMovimento = VerificarEmAtividade(dt);
        float mult = emMovimento ? multiplicadorEmAtividade : 1f;
        float consumo = consumoBasePorSegundo * mult * dt;
        AplicarConsumoEFome(consumo, dt);
    }

    private void AplicarConsumoEFome(float consumo, float dt)
    {
        if (tripulacaoCache == null)
        {
            tripulacaoCache = GetComponent<TripulacaoUnidade>() ?? TripulacaoUnidade.ObterParaObjeto(gameObject);
        }

        if (tripulacaoCache != null && tripulacaoCache.EstaSemTripulacao)
        {
            racaoAtual = 0f;
            tempoSemRacao = Mathf.Max(tempoSemRacao, atrasoParaFomeSegundos);
            return;
        }

        if (RacaoAtual > 0.01f)
        {
            racaoAtual = Mathf.Max(0f, RacaoAtual - consumo);
            if (racaoAtual > 0.01f)
            {
                tempoSemRacao = 0f;
                return;
            }
        }

        racaoAtual = 0f;
        tempoSemRacao += dt;
        if (tempoSemRacao < atrasoParaFomeSegundos)
        {
            return;
        }

        // Nunca destroi ou remove a unidade/estrutura fisica do mundo:
        // apenas aplica baixas graduais na tripulacao ate tornar a unidade inoperante.
        if (tripulacaoCache != null && tripulacaoCache.UsaTripulacao)
        {
            tripulacaoCache.AplicarBaixaPorFome(dt, danoFomePorSegundoPercentual);
        }
    }

    private bool VerificarEmAtividade(float dt)
    {
        Vector3 posAtual = transform.position;
        Vector3 delta = posAtual - ultimaPosicao;
        delta.y = 0f;
        ultimaPosicao = posAtual;

        if (dt > 0.0001f && delta.sqrMagnitude / (dt * dt) > 0.35f)
        {
            return true;
        }

        if (agenteCache != null && agenteCache.enabled && agenteCache.isOnNavMesh && agenteCache.velocity.sqrMagnitude > 0.25f)
        {
            return true;
        }

        if (controleCache != null && controleCache.TryObterEstadoCombate(out bool passivo, out _))
        {
            return !passivo;
        }

        return false;
    }

    private bool EstaEmVooAtivo()
    {
        if (transform.parent != null && (GetComponentInParent<NavioTransporteTropas>() != null || GetComponentInParent<GerenciadorPortaAvioes>() != null))
        {
            return false;
        }

        if (aviaoCache != null)
        {
            return aviaoCache.estadoAtual == ControleAviao.EstadoAviao.Decolando
                || aviaoCache.estadoAtual == ControleAviao.EstadoAviao.EmMissao
                || aviaoCache.estadoAtual == ControleAviao.EstadoAviao.Pousando
                || aviaoCache.estadoAtual == ControleAviao.EstadoAviao.RetornandoPraVaga;
        }

        if (cacaCache != null)
        {
            return !cacaCache.EstaNoChao;
        }

        if (helicopteroCache != null)
        {
            return !helicopteroCache.EstaNoSoloOperacional() && !helicopteroCache.EstaEstacionadoNoAeroporto();
        }

        if (c700Cache != null)
        {
            return !c700Cache.EstaNoSolo;
        }

        return Terrain.activeTerrain != null ? transform.position.y > 12f : true;
    }

    public void ConfigurarSeNecessario(bool preencherSeNovo = false)
    {
        CachearReferencias();

        if (!DeveUsarRacao(gameObject, identidadeCache))
        {
            usaRacao = false;
            capacidade = 0f;
            racaoAtual = 0f;
            configurado = true;
            return;
        }

        if (!configurado)
        {
            DeterminarPerfilRacao();
            ultimaPosicao = transform.position;
            configurado = true;
        }

        if (racaoAtual < 0f)
        {
            racaoAtual = preencherSeNovo ? capacidade : 0f;
        }
        else
        {
            racaoAtual = Mathf.Clamp(racaoAtual, 0f, capacidade);
        }
    }

    private void DeterminarPerfilRacao()
    {
        TripulacaoUnidade tripulacaoComp = TripulacaoUnidade.Garantir(gameObject);
        int tripulacao = tripulacaoComp != null ? Mathf.Max(1, tripulacaoComp.TripulacaoMaxima) : 1;

        if (identidadeCache != null && identidadeCache.tipoUnidade == TipoUnidade.Naval
            || GetComponent<IdentidadeNaval>() != null
            || GetComponent<ControleNavioRealista>() != null
            || GetComponent<ControleSubmarino>() != null
            || GetComponent<HovercraftTransporte>() != null
            || GetComponent<NavioAbastecimento>() != null
            || GetComponent<NavioPetroleiro>() != null
            || GetComponent<NavioTransporteTropas>() != null
            || GetComponent<GerenciadorPortaAvioes>() != null)
        {
            classe = ClasseRacaoUnidade.Naval;
            if (tripulacao >= 3500) capacidade = 1800f;      // Porta-avioes (~30 min fallback)
            else if (tripulacao >= 1500) capacidade = 1600f; // Transporte de tropas (~26 min fallback)
            else if (tripulacao >= 250) capacidade = 1400f;  // Destroyer / Fragata / Medio (~23 min fallback)
            else capacidade = 1100f;                         // Corveta / Escolta / Apoio (~18 min fallback)

            consumoBasePorSegundo = 1.0f;
            multiplicadorEmAtividade = 1.0f;
            atrasoParaFomeSegundos = 30f;
            danoFomePorSegundoPercentual = 0.02f;

            // Regra naval: a duracao das racoes equivale ao tempo de combustivel
            // em movimento acrescido de 10% de provisoes extras.
            if (combustivelCache == null)
            {
                combustivelCache = CombustivelUnidade.ObterParaObjeto(gameObject);
            }
            if (combustivelCache != null)
            {
                combustivelCache.ConfigurarSeNecessario(false);
                if (combustivelCache.usaCombustivel
                    && combustivelCache.Capacidade > 0f
                    && combustivelCache.consumoPorSegundoMovendo > 0f)
                {
                    float duracaoCombustivelSegundos = combustivelCache.Capacidade / combustivelCache.consumoPorSegundoMovendo;
                    capacidade = Mathf.Max(1f, Mathf.Round(duracaoCombustivelSegundos * 1.10f));
                }
            }

            // Custo nacional equilibrado: ~6 comida (corveta), ~18 (destroyer), ~35 (transporte), ~55 (porta-avioes)
            float comidaPorCargaCheia = tripulacao >= 3500 ? 55f
                : tripulacao >= 1500 ? 35f
                : tripulacao >= 250 ? 18f
                : 6f;
            comidaNacionalPorPontoRacao = comidaPorCargaCheia / capacidade;
            return;
        }

        if (identidadeCache != null && identidadeCache.tipoUnidade == TipoUnidade.Aereo
            || aviaoCache != null
            || cacaCache != null
            || helicopteroCache != null
            || c700Cache != null)
        {
            classe = ClasseRacaoUnidade.Aerea;
            capacidade = 900f; // 15 min em voo ativo
            consumoBasePorSegundo = 1.0f;
            multiplicadorEmAtividade = 1.0f;
            atrasoParaFomeSegundos = 20f;
            danoFomePorSegundoPercentual = 0.05f;
            comidaNacionalPorPontoRacao = (1.1f + 0.25f * Mathf.Clamp(tripulacao, 1, 2)) / capacidade;
            return;
        }

        if ((identidadeCache != null && identidadeCache.tipoUnidade == TipoUnidade.Veiculo)
            || GetComponent<CaminhaoTanqueAbastecimento>() != null
            || tripulacaoComp != null && tripulacaoComp.categoria == TripulacaoUnidade.CategoriaTripulacao.VeiculoTerrestre)
        {
            classe = ClasseRacaoUnidade.VeiculoTerrestre;
            capacidade = 660f; // ~11 min em campo
            consumoBasePorSegundo = 1.0f;
            multiplicadorEmAtividade = 1.15f;
            atrasoParaFomeSegundos = 25f;
            danoFomePorSegundoPercentual = 0.03f;
            comidaNacionalPorPontoRacao = (1.2f + 0.18f * Mathf.Clamp(tripulacao, 1, 6)) / capacidade;
            return;
        }

        classe = ClasseRacaoUnidade.Infantaria;
        capacidade = 420f; // ~7 min em campo
        consumoBasePorSegundo = 1.0f;
        multiplicadorEmAtividade = 1.25f;
        atrasoParaFomeSegundos = 20f;
        danoFomePorSegundoPercentual = 0.05f;
        comidaNacionalPorPontoRacao = (0.5f * Mathf.Max(1, tripulacao)) / capacidade;
    }

    public float Abastecer(float quantidade)
    {
        if (!usaRacao || Capacidade <= 0f || quantidade <= 0f)
        {
            return 0f;
        }

        ConfigurarSeNecessario(false);
        float antes = RacaoAtual;
        racaoAtual = Mathf.Clamp(antes + quantidade, 0f, capacidade);
        float aplicado = racaoAtual - antes;
        if (racaoAtual > 0.01f)
        {
            tempoSemRacao = 0f;
        }

        return aplicado;
    }

    public float AbastecerUsandoEstoqueNacional(float quantidadeDesejada, int teamIdOverride = -1)
    {
        if (!usaRacao || Capacidade <= 0f || quantidadeDesejada <= 0f)
        {
            return 0f;
        }

        ConfigurarSeNecessario(false);
        if (!EhUnidadeDoJogador())
        {
            return 0f;
        }

        float falta = Mathf.Max(0f, Capacidade - RacaoAtual);
        float solicitada = Mathf.Min(falta, quantidadeDesejada);
        if (solicitada <= 0.001f)
        {
            return 0f;
        }

        int teamId = teamIdOverride > 0 ? teamIdOverride : ResolverTeamId();
        float comidaNecessaria = solicitada * Mathf.Max(0.00001f, comidaNacionalPorPontoRacao);
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null || teamId <= 0)
        {
            return Abastecer(solicitada);
        }

        float razaoAtendida = governo.ConsumirComidaLogisticaFracionada(teamId, comidaNecessaria);
        if (razaoAtendida <= 0f)
        {
            return 0f;
        }

        return Abastecer(solicitada * razaoAtendida);
    }

    public void PreencherSemCusto()
    {
        ConfigurarSeNecessario(false);
        racaoAtual = Mathf.Max(0f, capacidade);
        tempoSemRacao = 0f;
    }

    internal void RestaurarEstadoPersistido(float capacidadeSalva, float racaoSalva, float tempoSemRacaoSalvo)
    {
        preenchimentoInicialSolicitado = false;
        ConfigurarSeNecessario(false);
        if (capacidadeSalva > 1f)
        {
            capacidade = capacidadeSalva;
        }

        racaoAtual = Mathf.Clamp(racaoSalva, 0f, capacidade);
        tempoSemRacao = Mathf.Max(0f, tempoSemRacaoSalvo);
        configurado = true;
    }

    public bool EhUnidadeDoJogador()
    {
        int teamUnidade = ResolverTeamId();
        if (teamUnidade <= 0)
        {
            return false;
        }

        int teamJogador = ObterTeamJogadorAtual();
        return teamUnidade == teamJogador;
    }

    public string TextoStatusCurto()
    {
        if (!UsaRacao || Capacidade <= 0f)
        {
            return string.Empty;
        }

        string sufixoFome = EstaFaminto ? " [FAMINTO]" : EstaVazio ? " [SEM RAÇÃO]" : string.Empty;
        return $"RAÇÃO {Mathf.RoundToInt(Percentual * 100f)}% ({Mathf.RoundToInt(RacaoAtual)}/{Mathf.RoundToInt(Capacidade)}){sufixoFome}";
    }

    private int ResolverTeamId()
    {
        if (identidadeCache == null)
        {
            identidadeCache = GetComponent<IdentidadeUnidade>()
                ?? GetComponentInParent<IdentidadeUnidade>()
                ?? GetComponentInChildren<IdentidadeUnidade>(true);
        }

        return identidadeCache != null ? identidadeCache.teamID : 0;
    }

    private void TentarReabastecerEmEstacaoProxima()
    {
        // Unidades aereas paradas no hangar/patio/porta-avioes recebem reabastecimento pelos gerenciadores;
        // aqui garantimos recarga terrestre proxima de Complexos/Quarteis/Fabricas aliadas e transportes embarcados.
        int meuTeam = ResolverTeamId();
        if (meuTeam <= 0)
        {
            return;
        }

        if (classe == ClasseRacaoUnidade.Infantaria || classe == ClasseRacaoUnidade.VeiculoTerrestre)
        {
            if (EstaPertoDeEstruturaLogisticaAliada(meuTeam, 280f))
            {
                AbastecerUsandoEstoqueNacional(Mathf.Max(Capacidade * 0.45f, Capacidade - RacaoAtual), meuTeam);
            }
        }
        else if (classe == ClasseRacaoUnidade.Aerea && !EstaEmVooAtivo())
        {
            if (transform.parent != null && GetComponentInParent<NavioTransporteTropas>() != null)
            {
                AbastecerUsandoEstoqueNacional(Capacidade * 0.45f, meuTeam);
            }
        }
    }

    private bool EstaPertoDeEstruturaLogisticaAliada(int meuTeam, float raio)
    {
        float raioSqr = raio * raio;
        Vector3 pos = transform.position;

        var complexos = BufferComplexos;
        RegistroEntidadesJogo.FillComplexos(complexos);
        for (int i = 0; i < complexos.Count; i++)
        {
            var c = complexos[i];
            if (c == null) continue;
            var id = c.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == meuTeam && HorizontalDistSqr(pos, c.transform.position) <= raioSqr)
            {
                return true;
            }
        }

        var fabricas = BufferFabricas;
        RegistroEntidadesJogo.FillFabricas(fabricas);
        for (int i = 0; i < fabricas.Count; i++)
        {
            var f = fabricas[i];
            if (f == null) continue;
            var id = f.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == meuTeam && HorizontalDistSqr(pos, f.transform.position) <= raioSqr)
            {
                return true;
            }
        }

        var aerop = BufferAeroportos;
        RegistroEntidadesJogo.FillAeroportos(aerop);
        for (int i = 0; i < aerop.Count; i++)
        {
            var a = aerop[i];
            if (a == null) continue;
            var id = a.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == meuTeam && HorizontalDistSqr(pos, a.transform.position) <= raioSqr)
            {
                return true;
            }
        }

        var piers = BufferPiers;
        RegistroEntidadesJogo.FillPiers(piers);
        for (int i = 0; i < piers.Count; i++)
        {
            var p = piers[i];
            if (p == null) continue;
            var id = p.GetComponent<IdentidadeUnidade>();
            if (id != null && id.teamID == meuTeam && HorizontalDistSqr(pos, p.transform.position) <= raioSqr * 1.4f)
            {
                return true;
            }
        }

        GerenciadorDivisaoTerritorial divisao = GerenciadorDivisaoTerritorial.Instancia;
        if (divisao != null && divisao.cidades != null)
        {
            float raioCidadeSqr = Mathf.Max(raio, 380f) * Mathf.Max(raio, 380f);
            for (int i = 0; i < divisao.cidades.Count; i++)
            {
                var c = divisao.cidades[i];
                if (c == null || c.teamID != meuTeam) continue;
                if (c.cidadeUrbana == null && c.marcador == null && c.localizacao == Vector3.zero) continue;
                Vector3 posCidade = c.cidadeUrbana != null
                    ? c.cidadeUrbana.transform.position
                    : (c.marcador != null ? c.marcador.transform.position : c.localizacao);
                if (HorizontalDistSqr(pos, posCidade) <= raioCidadeSqr)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float HorizontalDistSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private void CachearReferencias()
    {
        if (identidadeCache == null)
        {
            identidadeCache = GetComponent<IdentidadeUnidade>()
                ?? GetComponentInParent<IdentidadeUnidade>()
                ?? GetComponentInChildren<IdentidadeUnidade>(true);
        }
        if (controleCache == null) controleCache = GetComponent<ControleUnidade>();
        if (danosCache == null) danosCache = GetComponent<SistemaDeDanos>();
        if (combustivelCache == null) combustivelCache = CombustivelUnidade.ObterParaObjeto(gameObject);
        if (agenteCache == null) agenteCache = GetComponent<NavMeshAgent>();
        if (aviaoCache == null) aviaoCache = GetComponent<ControleAviao>();
        if (cacaCache == null) cacaCache = GetComponent<ControleAviaoCaca>();
        if (helicopteroCache == null) helicopteroCache = GetComponent<Helicoptero>();
        if (c700Cache == null) c700Cache = GetComponent<C700TransporteAereo>();
        if (navioAbastecimentoCache == null) navioAbastecimentoCache = GetComponent<NavioAbastecimento>();
    }

    private void OnGUI()
    {
        if (!mostrarBarraNoMundo || !UsaRacao || IndicadorUnidadeVisibilidade.ExisteMenuOuModoDeInterfaceAberto)
        {
            return;
        }

        if (combustivelCache == null)
        {
            combustivelCache = CombustivelUnidade.ObterParaObjeto(gameObject);
        }

        if (combustivelCache != null && combustivelCache.IndicadorUnificadoAtivo)
        {
            return;
        }

        if (tripulacaoCache == null)
        {
            tripulacaoCache = GetComponent<TripulacaoUnidade>() ?? TripulacaoUnidade.ObterParaObjeto(gameObject);
        }
        bool semTripulacao = tripulacaoCache != null && tripulacaoCache.EstaSemTripulacao;

        bool selecionado = controleCache != null && controleCache.selecionado;
        if (!selecionado && !EstaBaixo && !EstaFaminto && !semTripulacao)
        {
            return;
        }

        if (danosCache != null && danosCache.Morreu)
        {
            return;
        }

        if (cameraCache == null || !cameraCache.isActiveAndEnabled)
        {
            cameraCache = Camera.main;
            if (cameraCache == null)
            {
                return;
            }
        }

        Vector3 mundo = transform.position + Vector3.up * ObterAlturaIndicador();
        Vector3 deltaCam = mundo - cameraCache.transform.position;
        if (deltaCam.sqrMagnitude > distanciaMaximaBarra * distanciaMaximaBarra)
        {
            return;
        }

        Vector3 tela = cameraCache.WorldToScreenPoint(mundo);
        if (tela.z <= 0.1f)
        {
            return;
        }

        float largura = classe == ClasseRacaoUnidade.Naval ? 58f : 44f;
        float altura = 5f;
        float posX = tela.x - largura * 0.5f;
        float posY = Screen.height - tela.y + (combustivelCache != null && combustivelCache.usaCombustivel ? 10f : 2f);

        Color anterior = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(posX - 1f, posY - 1f, largura + 2f, altura + 2f), Texture2D.whiteTexture);

        float pct = Percentual;
        bool piscarCritico = EstaVazio && Mathf.FloorToInt(Time.unscaledTime * 4f) % 2 == 0;
        Color corPreenchimento = EstaVazio
            ? (piscarCritico ? new Color(1f, 0.22f, 0.22f, 0.98f) : new Color(0.65f, 0.08f, 0.08f, 0.95f))
            : pct <= LimiarAlertaPercentual
                ? new Color(1f, 0.58f, 0.12f, 0.96f)
                : new Color(0.32f, 0.85f, 0.38f, 0.94f);

        GUI.color = corPreenchimento;
        GUI.DrawTexture(new Rect(posX, posY, largura * Mathf.Max(EstaVazio ? 1f : 0.04f, pct), altura), Texture2D.whiteTexture);

        if (semTripulacao || EstaFaminto || EstaVazio)
        {
            InicializarEstilosGUI();
            GUI.color = Color.white;
            string aviso = semTripulacao ? "SEM TRIPULAÇÃO" : (EstaFaminto ? "FAMINTO" : "SEM RAÇÃO");
            GUI.Label(new Rect(posX - 32f, posY + 6f, largura + 64f, 16f), aviso, estiloAlertaFome);
        }
        else if (selecionado)
        {
            InicializarEstilosGUI();
            GUI.color = new Color(0.85f, 1f, 0.85f, 0.92f);
            GUI.Label(new Rect(posX - 18f, posY + 5f, largura + 36f, 14f), "RAÇÃO " + Mathf.RoundToInt(pct * 100f) + "%", estiloRotuloFome);
        }

        GUI.color = anterior;
    }

    private float ObterAlturaIndicador()
    {
        switch (classe)
        {
            case ClasseRacaoUnidade.Naval: return 14f;
            case ClasseRacaoUnidade.Aerea: return 7f;
            case ClasseRacaoUnidade.VeiculoTerrestre: return 4.5f;
            default: return 2.8f;
        }
    }

    private static void InicializarEstilosGUI()
    {
        if (estiloAlertaFome == null)
        {
            estiloAlertaFome = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            estiloAlertaFome.normal.textColor = new Color(1f, 0.35f, 0.25f, 1f);
        }

        if (estiloRotuloFome == null)
        {
            estiloRotuloFome = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 9,
                fontStyle = FontStyle.Bold
            };
        }
    }

    public static int ObterTeamJogadorAtual()
    {
        if (SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.teamJogador > 0)
        {
            return SistemaGovernoMundial.Instancia.teamJogador;
        }

        if (Hegemonia.RTS.RTSGameSession.Instancia != null && Hegemonia.RTS.RTSGameSession.Instancia.PlayerTeamId > 0)
        {
            return Hegemonia.RTS.RTSGameSession.Instancia.PlayerTeamId;
        }

        return 1;
    }

    public static bool DeveUsarRacao(GameObject alvo, IdentidadeUnidade identidade = null)
    {
        if (alvo == null)
        {
            return false;
        }

        if (alvo.GetComponent<ControleAviaoComercial>() != null
            || alvo.GetComponentInParent<ControleAviaoComercial>() != null)
        {
            return false;
        }

        if (TripulacaoUnidade.EhEstruturaImovel(alvo) || TripulacaoUnidade.EhDroneNaoTripulado(alvo))
        {
            return false;
        }

        if (identidade == null)
        {
            identidade = alvo.GetComponent<IdentidadeUnidade>()
                ?? alvo.GetComponentInParent<IdentidadeUnidade>()
                ?? alvo.GetComponentInChildren<IdentidadeUnidade>(true);
        }

        if (identidade == null || identidade.tipoUnidade == TipoUnidade.Estrutura || identidade.tipoUnidade == TipoUnidade.Aereo)
        {
            return false;
        }

        if (alvo.GetComponent<ControleAviao>() != null
            || alvo.GetComponent<ControleAviaoCaca>() != null
            || alvo.GetComponent<Helicoptero>() != null
            || alvo.GetComponent<C700TransporteAereo>() != null)
        {
            return false;
        }

        return TripulacaoUnidade.DeveUsarTripulacao(alvo, identidade);
    }

    public static RacaoUnidade Garantir(GameObject alvo, bool preencherSemCusto = true)
    {
        if (alvo == null || !DeveUsarRacao(alvo))
        {
            return null;
        }

        RacaoUnidade racao = alvo.GetComponent<RacaoUnidade>();
        if (racao == null)
        {
            racao = alvo.AddComponent<RacaoUnidade>();
        }

        if (!preencherSemCusto)
        {
            racao.preenchimentoInicialSolicitado = false;
        }

        racao.ConfigurarSeNecessario(preencherSemCusto);
        return racao;
    }

    public static RacaoUnidade ObterParaObjeto(GameObject alvo)
    {
        if (alvo == null) return null;
        RacaoUnidade r = alvo.GetComponent<RacaoUnidade>();
        if (r == null) r = alvo.GetComponentInParent<RacaoUnidade>(true);
        if (r == null) r = alvo.GetComponentInChildren<RacaoUnidade>(true);
        return r;
    }

    private static readonly System.Collections.Generic.List<ComplexoGovernamental> BufferComplexos = new System.Collections.Generic.List<ComplexoGovernamental>(16);
    private static readonly System.Collections.Generic.List<Fabrica> BufferFabricas = new System.Collections.Generic.List<Fabrica>(32);
    private static readonly System.Collections.Generic.List<GerenciadorAeroporto> BufferAeroportos = new System.Collections.Generic.List<GerenciadorAeroporto>(16);
    private static readonly System.Collections.Generic.List<PierMarinha> BufferPiers = new System.Collections.Generic.List<PierMarinha>(16);
}
