using System;
using System.Collections.Generic;
using UnityEngine;

public enum TipoEstruturaCultura
{
    Estadio,
    Museu,
    TorreTuristica,
    Monumento,
    CentroCultural,
    Parque,
    Arena,
    CentroConvencoes
}

public enum NivelInvestimentoCultura
{
    Local,
    Regional,
    Nacional,
    Monumental
}

[DisallowMultipleComponent]
public sealed class EstruturaCulturaEntretenimento : MonoBehaviour
{
    public TipoEstruturaCultura tipo = TipoEstruturaCultura.Estadio;
    public NivelInvestimentoCultura nivel = NivelInvestimentoCultura.Local;
    public bool ocupacaoAutomatica = true;
    [Min(100)] public int capacidadeVisitantes = 5000;
    [Min(1)] public int empregosPermanentes = 80;
    [Min(0)] public int empregosTemporarios;
    [Min(1)] public float consumoEnergia = 20f;
    [Min(0)] public float manutencaoDiaria = 250f;
    [Min(0.01f)] public float precoIngresso = 25f;
    [Range(0f, 1f)] public float seguranca = 0.75f;
    [Range(0f, 1f)] public float acessoTransporte = 0.70f;
    [Range(0f, 1f)] public float qualidade = 0.80f;
    [Range(0f, 30f)] public float prestigioBase = 2f;

    [SerializeField] private bool funcionando;
    [SerializeField] private string motivoParada = "Aguardando analise";
    [SerializeField] private int visitantesAtuais;
    [SerializeField] private int turistasAtuais;
    [SerializeField] private float ocupacaoAtual;
    [SerializeField] private bool eventoEmAndamento;
    [SerializeField] private string proximoEvento = "Aguardando calendario";
    [SerializeField] private float receitaIngressos;
    [SerializeField] private float receitaIndireta;

    private EstruturaEconomica estruturaEconomica;
    public static EstruturaCulturaEntretenimento Selecionada { get; private set; }
    public bool Funcionando => funcionando;
    public string MotivoParada => motivoParada;
    public int VisitantesAtuais => visitantesAtuais;
    public int TuristasAtuais => turistasAtuais;
    public float OcupacaoAtual => ocupacaoAtual;
    public bool EventoEmAndamento => eventoEmAndamento;
    public string ProximoEvento => proximoEvento;
    public float ReceitaIngressos => receitaIngressos;
    public float ReceitaIndireta => receitaIndireta;
    public int TeamId => estruturaEconomica != null ? Mathf.Max(1, estruturaEconomica.teamId) : ResolverTeamId();

    private void Awake()
    {
        InferirPerfilPorNome();
        GarantirEstruturaEconomica();
    }

    private void OnEnable()
    {
        SistemaCulturaEntretenimento.GarantirInstancia();
        SistemaCulturaEntretenimento.Registrar(this);
    }

    private void OnDisable()
    {
        SistemaCulturaEntretenimento.Desregistrar(this);
        if (ReferenceEquals(Selecionada, this)) Selecionada = null;
    }

    private void OnMouseDown()
    {
        Selecionada = this;
    }

    private void OnValidate()
    {
        capacidadeVisitantes = Mathf.Max(100, capacidadeVisitantes);
        empregosPermanentes = Mathf.Max(1, empregosPermanentes);
        consumoEnergia = Mathf.Max(1f, consumoEnergia);
        manutencaoDiaria = Mathf.Max(0f, manutencaoDiaria);
        precoIngresso = Mathf.Max(0.01f, precoIngresso);
    }

    private int ResolverTeamId()
    {
        IdentidadeUnidade id = GetComponentInParent<IdentidadeUnidade>();
        if (id != null && id.teamID > 0) return id.teamID;
        IdentidadeIA ia = GetComponentInParent<IdentidadeIA>();
        if (ia != null && ia.teamID > 0) return ia.teamID;
        return SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.teamJogador : 1;
    }

    private void GarantirEstruturaEconomica()
    {
        estruturaEconomica = GetComponent<EstruturaEconomica>();
        // Cultura possui contabilidade propria. So sincronizamos uma estrutura
        // economica se o prefab ja tiver uma, evitando duplicar empregos e energia.
        if (estruturaEconomica == null) return;
        estruturaEconomica.teamId = ResolverTeamId();
        estruturaEconomica.tipo = tipo == TipoEstruturaCultura.Estadio || tipo == TipoEstruturaCultura.Arena
            ? TipoEstruturaEconomica.Shopping : TipoEstruturaEconomica.Comercio;
        estruturaEconomica.empregosGerados = empregosPermanentes;
        estruturaEconomica.energiaConsumida = consumoEnergia;
        estruturaEconomica.dinheiroGerado = 0f;
    }

    public void InferirPerfilPorNome()
    {
        string nome = gameObject.name.ToLowerInvariant();
        if (EhTorreTecnica(nome)) return;
        if (nome.Contains("estadio") || nome.Contains("football")) tipo = TipoEstruturaCultura.Estadio;
        else if (nome.Contains("museu")) tipo = TipoEstruturaCultura.Museu;
        else if (nome.Contains("torre") || nome.Contains("dubai") || nome.Contains("khalifa") || nome.Contains("eiffel")) tipo = TipoEstruturaCultura.TorreTuristica;
        else if (nome.Contains("piramide") || nome.Contains("monumento") || nome.Contains("maravilha")) tipo = TipoEstruturaCultura.Monumento;
        else if (nome.Contains("parque")) tipo = TipoEstruturaCultura.Parque;
        else if (nome.Contains("arena")) tipo = TipoEstruturaCultura.Arena;
        else if (nome.Contains("convenc") || nome.Contains("congresso")) tipo = TipoEstruturaCultura.CentroConvencoes;
        else if (nome.Contains("cultural") || nome.Contains("cultura")) tipo = TipoEstruturaCultura.CentroCultural;
        AplicarPerfil();
    }

    private void AplicarPerfil()
    {
        switch (tipo)
        {
            case TipoEstruturaCultura.Estadio: DefinirPerfil(NivelInvestimentoCultura.Nacional, 60000, 620, 180f, 1300f, 200f, 12f); break;
            case TipoEstruturaCultura.Museu: DefinirPerfil(NivelInvestimentoCultura.Local, 5000, 80, 20f, 250f, 50f, 4f); break;
            case TipoEstruturaCultura.TorreTuristica: DefinirPerfil(NivelInvestimentoCultura.Nacional, 20000, 250, 70f, 650f, 120f, 10f); break;
            case TipoEstruturaCultura.Monumento: DefinirPerfil(NivelInvestimentoCultura.Monumental, 100000, 400, 120f, 1800f, 80f, 25f); break;
            case TipoEstruturaCultura.CentroCultural: DefinirPerfil(NivelInvestimentoCultura.Local, 8000, 120, 25f, 300f, 30f, 5f); break;
            case TipoEstruturaCultura.Parque: DefinirPerfil(NivelInvestimentoCultura.Local, 12000, 90, 15f, 180f, 20f, 3f); break;
            case TipoEstruturaCultura.Arena: DefinirPerfil(NivelInvestimentoCultura.Regional, 30000, 300, 120f, 700f, 90f, 8f); break;
            case TipoEstruturaCultura.CentroConvencoes: DefinirPerfil(NivelInvestimentoCultura.Regional, 18000, 220, 90f, 600f, 150f, 9f); break;
        }
        if (estruturaEconomica != null) GarantirEstruturaEconomica();
    }

    private void DefinirPerfil(NivelInvestimentoCultura novoNivel, int visitantes, int empregos, float energia, float manutencao, float ingresso, float prestigio)
    {
        nivel = novoNivel;
        capacidadeVisitantes = visitantes;
        empregosPermanentes = empregos;
        consumoEnergia = energia;
        manutencaoDiaria = manutencao;
        precoIngresso = ingresso;
        prestigioBase = prestigio;
    }

    internal void AplicarResultado(bool ativo, string motivo, float ocupacao, int visitantes, int turistas, bool evento, string eventoNome, float bilheteria, float indireta, int temporarios)
    {
        funcionando = ativo;
        motivoParada = ativo ? "Funcionando" : motivo;
        ocupacaoAtual = Mathf.Clamp01(ocupacao);
        visitantesAtuais = Mathf.Max(0, visitantes);
        turistasAtuais = Mathf.Max(0, turistas);
        eventoEmAndamento = evento;
        proximoEvento = evento ? eventoNome : "Proximo evento em analise";
        receitaIngressos = Mathf.Max(0f, bilheteria);
        receitaIndireta = Mathf.Max(0f, indireta);
        empregosTemporarios = Mathf.Max(0, temporarios);
        if (estruturaEconomica != null)
        {
            estruturaEconomica.status = ativo ? StatusEstruturaEconomica.Ativa : StatusEstruturaEconomica.Inativa;
            estruturaEconomica.eficiencia = ativo ? Mathf.Max(0.1f, ocupacaoAtual) : 0f;
            estruturaEconomica.empregosGerados = empregosPermanentes + empregosTemporarios;
        }
    }

    public string GerarDetalhe()
    {
        return "Estrutura: " + tipo
            + "\nNivel: " + nivel
            + "\nFuncionamento: " + (ocupacaoAtual * 100f).ToString("0") + "%"
            + "\nVisitantes: " + visitantesAtuais.ToString("N0")
            + "\nTuristas: " + turistasAtuais.ToString("N0")
            + "\nEmpregos permanentes: " + empregosPermanentes.ToString("N0")
            + "\nEmpregos temporarios: " + empregosTemporarios.ToString("N0")
            + "\nManutencao diaria: " + manutencaoDiaria.ToString("N0")
            + "\nReceita direta: " + receitaIngressos.ToString("N0")
            + "\nReceita indireta: " + receitaIndireta.ToString("N0")
            + "\nSituacao: " + (funcionando ? "Sustentavel" : motivoParada)
            + "\nProximo evento: " + proximoEvento;
    }

    public static bool PareceCultura(GameObject objeto)
    {
        if (objeto == null) return false;
        string nome = (objeto.name + " " + objeto.tag).ToLowerInvariant();
        if (EhTorreTecnica(nome)) return false;
        return nome.Contains("estadio") || nome.Contains("football") || nome.Contains("museu")
            || nome.Contains("torre") || nome.Contains("dubai") || nome.Contains("khalifa")
            || nome.Contains("eiffel") || nome.Contains("piramide") || nome.Contains("monumento")
            || nome.Contains("parque") || nome.Contains("arena") || nome.Contains("convenc")
            || nome.Contains("congresso") || nome.Contains("cultural") || nome.Contains("cultura");
    }

    private static bool EhTorreTecnica(string nome)
    {
        return nome.Contains("radar") || nome.Contains("sentinela") || nome.Contains("torreta")
            || nome.Contains("defesa") || nome.Contains("controle") || nome.Contains("comunicacao");
    }

    public static void GarantirNaCena(GameObject objeto)
    {
        if (!PareceCultura(objeto) || objeto.GetComponent<EstruturaCulturaEntretenimento>() != null) return;
        objeto.AddComponent<EstruturaCulturaEntretenimento>();
    }
}

[Serializable]
public sealed class DadosCulturaNacional
{
    public int totalEstruturas;
    public int estadios;
    public int museus;
    public int torres;
    public int monumentos;
    public int centrosCulturais;
    public int parques;
    public int arenas;
    public int centrosConvencoes;
    public int estruturasAtivas;
    public int estruturasFechadas;
    public int capacidadeTotalVisitantes;
    public int visitantesAtuais;
    public int turistasNacionais;
    public int turistasInternacionais;
    public int empregosPermanentes;
    public int empregosTemporarios;
    public int eventosEmAndamento;
    public string proximoEvento = "Nenhum evento programado";
    public float receitaIngressos;
    public float receitaTuristicaIndireta;
    public float impostosGerados;
    public float custoManutencaoDiario;
    public float consumoEnergia;
    public float contribuicaoFelicidade;
    public float atratividadeTuristica;
    public float capacidadeAtracao;
    public float prestigioNacional;
    public int estruturasPrejuizo;
    public int obrasMonumentais;
    public string principalMotivoParada = "Nenhum";
}

[DefaultExecutionOrder(-40)]
public sealed class SistemaCulturaEntretenimento : MonoBehaviour
{
    // Atualiza automaticamente no ciclo nacional e no Menu Governo.
    public static SistemaCulturaEntretenimento Instancia { get; private set; }
    private static readonly HashSet<EstruturaCulturaEntretenimento> estruturas = new HashSet<EstruturaCulturaEntretenimento>();
    private readonly Dictionary<int, DadosCulturaNacional> relatorios = new Dictionary<int, DadosCulturaNacional>();
    private readonly Dictionary<int, HashSet<TipoEstruturaCultura>> variedades = new Dictionary<int, HashSet<TipoEstruturaCultura>>();
    private float proximoCiclo;
    private float proximaDescoberta;

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;
        GameObject go = new GameObject("SistemaCulturaEntretenimento");
        Instancia = go.AddComponent<SistemaCulturaEntretenimento>();
        DontDestroyOnLoad(go);
    }

    public static void Registrar(EstruturaCulturaEntretenimento estrutura) { if (estrutura != null) estruturas.Add(estrutura); }
    public static void Desregistrar(EstruturaCulturaEntretenimento estrutura) { if (estrutura != null) estruturas.Remove(estrutura); }

    public static DadosCulturaNacional ObterResumo(int teamId)
    {
        GarantirInstancia();
        DadosCulturaNacional resumo;
        return Instancia.relatorios.TryGetValue(Mathf.Max(1, teamId), out resumo) ? resumo : new DadosCulturaNacional();
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)) return;
        if (Time.unscaledTime >= proximaDescoberta)
        {
            proximaDescoberta = Time.unscaledTime + 5f;
            DescobrirEstruturas();
        }
        if (Time.unscaledTime < proximoCiclo) return;
        proximoCiclo = Time.unscaledTime + 1f;
        Recalcular();
    }

    private void DescobrirEstruturas()
    {
        Transform[] todos = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            Transform t = todos[i];
            if (t == null || t.parent != null || !t.gameObject.activeInHierarchy) continue;
            EstruturaCulturaEntretenimento.GarantirNaCena(t.gameObject);
        }
    }

    private void Recalcular()
    {
        relatorios.Clear();
        variedades.Clear();
        estruturas.RemoveWhere(e => e == null);
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        foreach (EstruturaCulturaEntretenimento estrutura in estruturas)
        {
            if (estrutura == null || !estrutura.isActiveAndEnabled) continue;
            int team = estrutura.TeamId;
            DadosPaisGoverno pais = gov != null ? gov.ObterPais(team) : null;
            int populacao = Mathf.Max(0, pais != null ? pais.populacaoCivil : 0);
            float poderCompra = pais != null ? pais.PoderDeCompra : 1f;
            float estabilidade = pais != null ? pais.estabilidade / 100f : 0.7f;
            float turistasBase = pais != null ? Mathf.Max(0f, pais.indiceAtratividade * 600f) : 0f;
            float demanda = Mathf.Clamp01(populacao / 50000f + turistasBase / 100000f);
            float mercadorias = pais != null
                ? Mathf.Clamp01(pais.comida / Mathf.Max(1f, populacao * 0.25f))
                : 1f;
            float fatores = Mathf.Clamp01(0.38f + poderCompra * 0.22f + estabilidade * 0.15f
                + estrutura.seguranca * 0.10f + estrutura.acessoTransporte * 0.08f
                + estrutura.qualidade * 0.10f + mercadorias * 0.07f);
            bool energia = TemEnergia(estrutura, pais);
            float ocupacao = populacao <= 0 && turistasBase <= 0f ? 0f : Mathf.Clamp(0.10f + demanda * fatores, 0.10f, 1f);
            int visitantes = Mathf.RoundToInt(estrutura.capacidadeVisitantes * ocupacao);
            bool evento = visitantes > 0 && (Time.unscaledTime + Mathf.Abs(estrutura.GetInstanceID() % 53)) % 60f > 35f;
            int temporarios = evento ? Mathf.RoundToInt(estrutura.empregosPermanentes * 0.60f * ocupacao) : 0;
            string eventoNome = NomeEvento(estrutura.tipo, estrutura.GetInstanceID());
            string motivo = !energia ? "Falta de energia"
                : visitantes <= 0 ? "Sem publico ou turistas"
                : estabilidade < 0.25f ? "Seguranca insuficiente"
                : estrutura.acessoTransporte < 0.10f ? "Acesso e transporte insuficientes"
                : poderCompra < 0.10f ? "Poder de compra insuficiente"
                : pais != null && populacao > 0 && pais.comida <= 0 ? "Falta de mercadorias"
                : null;
            bool ativo = motivo == null;
            if (!ativo) { ocupacao = 0f; visitantes = 0; temporarios = 0; }
            float bilheteria = ativo ? visitantes * estrutura.precoIngresso * 0.02f : 0f;
            float indireta = ativo ? visitantes * 0.012f : 0f;
            int turistas = Mathf.RoundToInt(visitantes * 0.35f);
            estrutura.AplicarResultado(ativo, motivo, ocupacao, visitantes, turistas, evento && ativo, eventoNome, bilheteria, indireta, temporarios);

            DadosCulturaNacional resumo;
            if (!relatorios.TryGetValue(team, out resumo)) { resumo = new DadosCulturaNacional(); relatorios.Add(team, resumo); variedades.Add(team, new HashSet<TipoEstruturaCultura>()); }
            resumo.totalEstruturas++;
            resumo.capacidadeTotalVisitantes += estrutura.capacidadeVisitantes;
            resumo.visitantesAtuais += visitantes;
            resumo.turistasInternacionais += turistas;
            resumo.turistasNacionais += Mathf.Max(0, visitantes - turistas);
            resumo.empregosPermanentes += Mathf.RoundToInt(estrutura.empregosPermanentes * ocupacao);
            resumo.empregosTemporarios += temporarios;
            resumo.receitaIngressos += bilheteria;
            resumo.receitaTuristicaIndireta += indireta;
            resumo.impostosGerados += (bilheteria + indireta) * (pais != null ? pais.impostoComercio / 100f : 0.12f);
            resumo.custoManutencaoDiario += estrutura.manutencaoDiaria;
            resumo.consumoEnergia += estrutura.consumoEnergia;
            resumo.prestigioNacional += estrutura.prestigioBase * ocupacao;
            if (ativo) resumo.estruturasAtivas++; else resumo.estruturasFechadas++;
            if (evento && ativo) { resumo.eventosEmAndamento++; resumo.proximoEvento = eventoNome; }
            if (!ativo) resumo.estruturasPrejuizo++;
            if (estrutura.nivel == NivelInvestimentoCultura.Monumental) resumo.obrasMonumentais++;
            variedades[team].Add(estrutura.tipo);
            IncrementarTipo(resumo, estrutura.tipo);
            if (!ativo && resumo.principalMotivoParada == "Nenhum") resumo.principalMotivoParada = motivo;
        }

        foreach (KeyValuePair<int, DadosCulturaNacional> par in relatorios)
        {
            DadosCulturaNacional r = par.Value;
            float atividade = r.totalEstruturas <= 0 ? 0f : r.estruturasAtivas / (float)r.totalEstruturas;
            float publico = r.capacidadeTotalVisitantes <= 0 ? 0f : Mathf.Clamp01(r.visitantesAtuais / (float)r.capacidadeTotalVisitantes);
            float variedade = Mathf.Clamp01(variedades[par.Key].Count / 5f);
            r.contribuicaoFelicidade = Mathf.Clamp(15f * atividade * Mathf.Lerp(0.55f, 1f, variedade) * Mathf.Lerp(0.4f, 1f, publico), 0f, 15f);
            r.atratividadeTuristica = Mathf.Clamp(20f * atividade * Mathf.Lerp(0.5f, 1f, variedade) * Mathf.Lerp(0.4f, 1f, publico), 0f, 20f);
            r.capacidadeAtracao = Mathf.Clamp(r.atratividadeTuristica + r.empregosPermanentes * 0.01f, 0f, 100f);
            r.prestigioNacional = Mathf.Clamp(r.prestigioNacional, 0f, 100f);
        }
    }

    private static bool TemEnergia(EstruturaCulturaEntretenimento estrutura, DadosPaisGoverno pais)
    {
        if (pais != null) return pais.energia >= estrutura.consumoEnergia;
        return true;
    }

    private static string NomeEvento(TipoEstruturaCultura tipo, int seed)
    {
        string[] nomes = tipo == TipoEstruturaCultura.Estadio || tipo == TipoEstruturaCultura.Arena
            ? new[] { "Campeonato nacional", "Show e festival", "Final internacional" }
            : tipo == TipoEstruturaCultura.Museu || tipo == TipoEstruturaCultura.CentroCultural
                ? new[] { "Exposicao cultural", "Feira de patrimonio", "Mostra historica" }
                : new[] { "Temporada de turismo", "Visita diplomatica", "Festival internacional" };
        return nomes[Mathf.Abs(seed) % nomes.Length];
    }

    private static void IncrementarTipo(DadosCulturaNacional r, TipoEstruturaCultura tipo)
    {
        switch (tipo)
        {
            case TipoEstruturaCultura.Estadio: r.estadios++; break;
            case TipoEstruturaCultura.Museu: r.museus++; break;
            case TipoEstruturaCultura.TorreTuristica: r.torres++; break;
            case TipoEstruturaCultura.Monumento: r.monumentos++; break;
            case TipoEstruturaCultura.CentroCultural: r.centrosCulturais++; break;
            case TipoEstruturaCultura.Parque: r.parques++; break;
            case TipoEstruturaCultura.Arena: r.arenas++; break;
            case TipoEstruturaCultura.CentroConvencoes: r.centrosConvencoes++; break;
        }
    }
}
