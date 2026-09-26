using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tema visual e cultural da cidade pré-montada.
/// </summary>
public enum TemaCidade
{
    Egito,
    Moderna,
    Futurista,
    Classica,
    Personalizada
}

/// <summary>
/// Script completo para cidades pré-montadas temáticas (Egito, Moderna, etc.).
/// Engloba simultaneamente:
/// 1. Imobiliária (Moradia de 500 mil a 1,5 milhão de habitantes).
/// 2. Comércio e Empregos (Metade de cada lugar: 500k moradores -> até 250k comércio/empregos).
/// 3. Energia elétrica (Consumo idêntico ao que casas e prédios consumiriam para esse montante populacional).
/// Integra-se transparentemente com o SistemaEconomiaImoveis, GerenciadorRecursos e SistemaGovernoMundial.
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
public class CidadeComplexoUrbano : MonoBehaviour
{
    private static readonly HashSet<CidadeComplexoUrbano> CidadesAtivas = new HashSet<CidadeComplexoUrbano>();
    private static readonly List<CidadeComplexoUrbano> BufferCidades = new List<CidadeComplexoUrbano>(16);

    [Header("🏛️ Identidade e Tema")]
    [Tooltip("Tema visual e cultural da cidade")]
    public TemaCidade tema = TemaCidade.Moderna;
    [Tooltip("Nome exibido para esta metrópole")]
    public string nomeCidade = "Nova Metrópole";
    [Tooltip("Identificador da nação ou time proprietário")]
    public int teamId = 1;

    [Header("🏥 Saúde da cidade")]
    [Tooltip("Capacidade hospitalar preventiva inicial do complexo. Hospitais adicionais usam HospitalSaude.")]
    public int capacidadeHospitalarInicial = 400000;
    [Tooltip("Indica ao sistema populacional que a cidade já possui uma rede básica de atendimento.")]
    public bool possuiRedeSaudeBasica = true;

    [Header("🏠 Imobiliária (Moradia)")]
    [Tooltip("Capacidade habitacional total suportada pela cidade (500.000 a 2.000.000 moradores)")]
    [Range(500000, 2000000)]
    public int capacidadeHabitacional = 2000000;

    [Tooltip("População atualmente residente na cidade")]
    public int populacaoResidente = 100000;

    [Tooltip("Índice de qualidade de vida e atratividade residencial (0 a 100)")]
    [Range(0, 100)]
    public int qualidadeVida = 75;

    [Header("🏪 Comércio e Empregos (Metade da Capacidade)")]
    [Tooltip("Porcentagem fixa de comércio em relação aos moradores (sempre 50%, conforme especificação)")]
    [Range(0.1f, 1f)]
    public float proporcaoComercio = 0.5f;

    [Tooltip("Fator de receita gerada por trabalhador comercial ativo por segundo")]
    public float rendaPorTrabalhador = 0.00005f;

    [Header("⚡ Consumo de Energia")]
    [Tooltip("Consumo por morador idêntico ao cálculo de Imovel.cs (0.05 * 1.5 = 0.075 MW por pessoa)")]
    public float consumoEnergiaPorMorador = 0.075f;

    [Tooltip("Consumo de energia por posto comercial ativo (MW)")]
    public float consumoEnergiaPorEmprego = 0.025f;

    [Tooltip("Indica se a cidade está sofrendo com falta de energia")]
    public bool semEnergia = false;

    // Métricas calculadas públicas
    public int CapacidadeComercial => Mathf.RoundToInt(capacidadeHabitacional * Mathf.Clamp01(proporcaoComercio));
    public int EmpregosOcupados => Mathf.Min(CapacidadeComercial, Mathf.RoundToInt(populacaoResidente * Mathf.Clamp01(proporcaoComercio)));
    public int VagasComerciaisLivres => Mathf.Max(0, CapacidadeComercial - EmpregosOcupados);
    public int VagasHabitacionaisLivres => Mathf.Max(0, capacidadeHabitacional - populacaoResidente);
    public float TaxaOcupacaoHabitacional => capacidadeHabitacional > 0 ? (float)populacaoResidente / capacidadeHabitacional : 0f;
    public float TaxaOcupacaoComercial => CapacidadeComercial > 0 ? (float)EmpregosOcupados / CapacidadeComercial : 0f;

    public float ConsumoEnergiaResidencial => populacaoResidente * consumoEnergiaPorMorador;
    public float ConsumoEnergiaComercial => (semEnergia ? 0f : EmpregosOcupados * consumoEnergiaPorEmprego);
    public float ConsumoEnergiaTotal => ConsumoEnergiaResidencial + ConsumoEnergiaComercial;
    public float ReceitaComercialPorSegundo => semEnergia ? 0f : EmpregosOcupados * rendaPorTrabalhador;

    // Componentes internos de ponte econômica
    private EstruturaEconomica estrutura;
    private int limitePopulacaoAdicionadoRecursos;
    private bool populacaoRegistrada;
    private float timerSincronizacao = 0f;
    private float timerBlackout = 0f;
    private bool mouseHover = false;
    private Texture2D _texturaTooltip;

    private void Awake()
    {
        GarantirEstrutura();
        GarantirSaude();
        ValidarLimites();
    }

    private void Start()
    {
        ValidarLimites();
        SincronizarEstruturaEconomica();
        RegistrarPopulacaoInicialNaNacao();
        populacaoRegistrada = true;
    }

    private void OnEnable()
    {
        CidadesAtivas.Add(this);
        GarantirEstrutura();
        GarantirSaude();
        SincronizarEstruturaEconomica();
    }

    private void OnDisable()
    {
        CidadesAtivas.Remove(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparRegistroRuntime()
    {
        CidadesAtivas.Clear();
        BufferCidades.Clear();
    }

    /// <summary>
    /// Distribui a população civil nacional pelas cidades do time, segundo a
    /// capacidade de cada uma. O crescimento populacional nacional continua
    /// sendo calculado uma única vez por SistemaPopulacao.
    /// </summary>
    public static void SincronizarPopulacaoNacional(int timeId, int populacaoCivil)
    {
        PrepararBufferCidades(timeId);
        if (BufferCidades.Count == 0) return;

        long capacidadeTotal = 0;
        for (int i = 0; i < BufferCidades.Count; i++)
            capacidadeTotal += Mathf.Max(0, BufferCidades[i].capacidadeHabitacional);
        if (capacidadeTotal <= 0) return;

        int residentesParaDistribuir = (int)Math.Min(Mathf.Max(0, populacaoCivil), capacidadeTotal);
        int distribuidos = 0;
        for (int i = 0; i < BufferCidades.Count; i++)
        {
            CidadeComplexoUrbano cidade = BufferCidades[i];
            int alvo = i == BufferCidades.Count - 1
                ? residentesParaDistribuir - distribuidos
                : (int)((long)residentesParaDistribuir * Mathf.Max(0, cidade.capacidadeHabitacional) / capacidadeTotal);
            alvo = Mathf.Clamp(alvo, 0, Mathf.Max(0, cidade.capacidadeHabitacional));
            distribuidos += alvo;
            if (cidade.populacaoResidente == alvo) continue;
            cidade.populacaoResidente = alvo;
            cidade.SincronizarEstruturaEconomica();
        }
    }

    private static void PrepararBufferCidades(int timeId)
    {
        CidadesAtivas.RemoveWhere(cidade => cidade == null || !cidade.isActiveAndEnabled);
        BufferCidades.Clear();
        foreach (CidadeComplexoUrbano cidade in CidadesAtivas)
        {
            if (cidade != null && cidade.isActiveAndEnabled && cidade.teamId == timeId)
                BufferCidades.Add(cidade);
        }
        BufferCidades.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
    }

    private static void ObterTotaisCidades(int timeId, out int capacidade, out int moradores)
    {
        PrepararBufferCidades(timeId);
        long capacidadeTotal = 0;
        long moradoresTotal = 0;
        for (int i = 0; i < BufferCidades.Count; i++)
        {
            capacidadeTotal += Mathf.Max(0, BufferCidades[i].capacidadeHabitacional);
            moradoresTotal += Mathf.Clamp(BufferCidades[i].populacaoResidente, 0, BufferCidades[i].capacidadeHabitacional);
        }
        capacidade = (int)Math.Min(int.MaxValue, capacidadeTotal);
        moradores = (int)Math.Min(int.MaxValue, moradoresTotal);
    }

    private void RegistrarPopulacaoInicialNaNacao()
    {
        ObterTotaisCidades(teamId, out int capacidadeCidades, out int moradoresConfigurados);
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais != null)
        {
            pais.populacaoMaxima = Mathf.Max(pais.populacaoMaxima, capacidadeCidades);
            int naoCivil = Mathf.Max(0, pais.populacaoMilitarAtiva + pais.reservistas + pais.alistaveis);
            int maximoCivil = Mathf.Max(0, pais.populacaoMaxima - naoCivil);
            pais.populacaoCivil = Mathf.Clamp(Mathf.Max(pais.populacaoCivil, moradoresConfigurados), 0, maximoCivil);
            pais.populacao = pais.populacaoCivil + naoCivil;
            SincronizarRecursosDoJogador(pais);
            SincronizarPopulacaoNacional(teamId, pais.populacaoCivil);
            return;
        }

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        bool eJogador = governo == null || teamId == governo.teamJogador;
        if (!eJogador || recursos == null) return;

        recursos.AumentarLimitePopulacao(capacidadeHabitacional);
        limitePopulacaoAdicionadoRecursos = capacidadeHabitacional;
        recursos.populacaoAtual = Mathf.Clamp(Mathf.Max(recursos.populacaoAtual, moradoresConfigurados), 0, recursos.populacaoMaxima);
        SincronizarPopulacaoNacional(teamId, recursos.populacaoAtual);
    }

    private static void SincronizarRecursosDoJogador(DadosPaisGoverno pais)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (governo == null || recursos == null || pais == null || pais.teamId != governo.teamJogador) return;
        recursos.populacaoAtual = pais.populacao;
        recursos.populacaoMaxima = pais.populacaoMaxima;
        recursos.NotificarAtualizacao();
    }

    private int AplicarVariacaoPopulacionalNacional(int delta)
    {
        if (delta == 0) return 0;
        ObterTotaisCidades(teamId, out int capacidadeCidades, out _);
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais != null)
        {
            pais.populacaoMaxima = Mathf.Max(pais.populacaoMaxima, capacidadeCidades);
            int naoCivil = Mathf.Max(0, pais.populacaoMilitarAtiva + pais.reservistas + pais.alistaveis);
            int antes = Mathf.Max(0, pais.populacaoCivil);
            pais.populacaoCivil = Mathf.Clamp(antes + delta, 0, Mathf.Max(0, pais.populacaoMaxima - naoCivil));
            pais.populacao = pais.populacaoCivil + naoCivil;
            SincronizarRecursosDoJogador(pais);
            return pais.populacaoCivil - antes;
        }

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        bool eJogador = governo == null || teamId == governo.teamJogador;
        if (eJogador && recursos != null)
        {
            int antes = recursos.populacaoAtual;
            recursos.populacaoAtual = Mathf.Clamp(antes + delta, 0, recursos.populacaoMaxima);
            recursos.NotificarAtualizacao();
            return recursos.populacaoAtual - antes;
        }
        return delta;
    }

    private void GarantirSaude()
    {
        if (GetComponent<CidadeSaudePopulacional>() == null)
        {
            CidadeSaudePopulacional saude = gameObject.AddComponent<CidadeSaudePopulacional>();
            if (!possuiRedeSaudeBasica) saude.capacidadeBase = 0;
            else saude.capacidadeBase = Mathf.Max(0, capacidadeHospitalarInicial);
        }
    }

    private void Update()
    {
        timerSincronizacao += Time.deltaTime;
        if (timerSincronizacao >= 1.5f)
        {
            timerSincronizacao = 0f;
            SincronizarEstruturaEconomica();
        }

        if (semEnergia)
        {
            timerBlackout += Time.deltaTime;
            if (timerBlackout >= 10f)
            {
                timerBlackout = 0f;
                qualidadeVida = Mathf.Max(10, qualidadeVida - 2);
            }
        }
        else
        {
            timerBlackout = 0f;
        }
    }

    private void OnValidate()
    {
        ValidarLimites();
        proporcaoComercio = 0.5f; // Mantém fixo em 50% conforme especificação
    }

    private void ValidarLimites()
    {
        capacidadeHabitacional = Mathf.Clamp(capacidadeHabitacional, 500000, 2000000);
        populacaoResidente = Mathf.Clamp(populacaoResidente, 0, capacidadeHabitacional);
        qualidadeVida = Mathf.Clamp(qualidadeVida, 0, 100);
        proporcaoComercio = 0.5f;
    }

    /// <summary>
    /// Sincroniza a metrópole com o módulo EstruturaEconomica existente no projeto.
    /// Isso permite que o SistemaEconomiaImoveis calcule automaticamente os dados da cidade
    /// junto ao orçamento e matriz energética da nação sem alterar o código do sistema existente.
    /// </summary>
    public void SincronizarEstruturaEconomica()
    {
        GarantirEstrutura();
        if (estrutura == null) return;

        ValidarLimites();

        estrutura.teamId = teamId;
        estrutura.tipo = TipoEstruturaEconomica.PredioResidencial;
        estrutura.capacidadePopulacional = capacidadeHabitacional;
        estrutura.populacaoAtual = populacaoResidente;
        estrutura.empregosGerados = CapacidadeComercial;
        estrutura.energiaConsumida = ConsumoEnergiaTotal;
        estrutura.dinheiroGerado = ReceitaComercialPorSegundo;

        // Se o sistema marcou como SemEnergia, refletimos no script
        semEnergia = (estrutura.status == StatusEstruturaEconomica.SemEnergia);
    }

    private void GarantirEstrutura()
    {
        if (estrutura == null)
        {
            estrutura = GetComponent<EstruturaEconomica>();
        }
        if (estrutura == null)
        {
            estrutura = gameObject.AddComponent<EstruturaEconomica>();
        }

        if (teamId <= 0)
        {
            estrutura.InferirTeamId();
            teamId = estrutura.teamId;
        }
        else
        {
            estrutura.teamId = teamId;
        }
    }

    /// <summary>
    /// Altera o contingente de moradores residentes da cidade.
    /// Atualiza proporcionalmente os empregos e consumo de energia.
    /// </summary>
    public void AlterarPopulacaoResidente(int delta)
    {
        int antes = populacaoResidente;
        int solicitada = Mathf.Clamp(populacaoResidente + delta, 0, capacidadeHabitacional) - antes;
        if (solicitada != 0)
        {
            int variacaoAplicada = AplicarVariacaoPopulacionalNacional(solicitada);
            populacaoResidente = Mathf.Clamp(antes + variacaoAplicada, 0, capacidadeHabitacional);
        }

        SincronizarEstruturaEconomica();
    }

    public void DefinirStatusEnergia(bool semLuz)
    {
        semEnergia = semLuz;
        if (estrutura != null)
        {
            estrutura.status = semLuz ? StatusEstruturaEconomica.SemEnergia : StatusEstruturaEconomica.Ativa;
        }
    }

    private void OnMouseEnter() { mouseHover = true; }
    private void OnMouseExit() { mouseHover = false; }

    private Texture2D ObterTexturaTooltip()
    {
        if (_texturaTooltip == null)
        {
            _texturaTooltip = new Texture2D(1, 1);
            _texturaTooltip.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.12f, 0.94f));
            _texturaTooltip.Apply();
        }
        return _texturaTooltip;
    }

    private void OnGUI()
    {
        if (!mouseHover) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = ObterTexturaTooltip();
        boxStyle.padding = new RectOffset(12, 12, 12, 12);
        boxStyle.alignment = TextAnchor.MiddleLeft;

        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.richText = true;
        textStyle.fontSize = 13;
        textStyle.normal.textColor = Color.white;

        string iconeTema = (tema == TemaCidade.Egito) ? "🏺" : "🏙️";
        string statusEnergia = semEnergia
            ? "<color=#ff5555>⚡ SEM ENERGIA (Blecaute)</color>"
            : "<color=#55ff55>⚡ REDE CONECTADA</color>";

        string content = $"<b>{iconeTema} CIDADE METRÓPOLE [{tema.ToString().ToUpperInvariant()}]</b>\n" +
                         $"<i>{nomeCidade}</i> (Time {teamId})\n\n" +
                         $"🏠 <b>Imobiliária (Moradia):</b>\n" +
                         $"   👥 Residentes: <b>{populacaoResidente:N0}</b> / {capacidadeHabitacional:N0} ({(TaxaOcupacaoHabitacional * 100f):F1}%)\n" +
                         $"   🌿 Qualidade de Vida: <b>{qualidadeVida}%</b>\n\n" +
                         $"🏪 <b>Comércio & Empregos (50% da Moradia):</b>\n" +
                         $"   💼 Postos de Trabalho: <b>{EmpregosOcupados:N0}</b> / {CapacidadeComercial:N0}\n" +
                         $"   💰 Receita Comercial: <b>+${ReceitaComercialPorSegundo:F2}/s</b>\n\n" +
                         $"⚡ <b>Energia Elétrica:</b>\n" +
                         $"   🔌 Consumo: <b>{ConsumoEnergiaTotal:N1} MW</b>\n" +
                         $"      (Habitação: {ConsumoEnergiaResidencial:N1} MW | Comércio: {ConsumoEnergiaComercial:N1} MW)\n" +
                         $"   Status: {statusEnergia}";

        Vector2 size = textStyle.CalcSize(new GUIContent(content));
        float width = size.x + 24f;
        float height = size.y + 24f;

        Vector2 mousePos = Input.mousePosition;
        Rect rect = new Rect(mousePos.x + 15f, Screen.height - mousePos.y + 15f, width, height);

        if (rect.xMax > Screen.width) rect.x = mousePos.x - width - 15f;
        if (rect.yMax > Screen.height) rect.y = Screen.height - mousePos.y - height - 15f;

        GUI.Box(rect, "", boxStyle);
        GUI.Label(new Rect(rect.x + 12, rect.y + 12, size.x, size.y), content, textStyle);
    }

    private void OnDestroy()
    {
        CidadesAtivas.Remove(this);
        if (populacaoRegistrada && populacaoResidente > 0)
        {
            AplicarVariacaoPopulacionalNacional(-populacaoResidente);
            populacaoRegistrada = false;
        }

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (limitePopulacaoAdicionadoRecursos > 0 && recursos != null)
            recursos.AumentarLimitePopulacao(-limitePopulacaoAdicionadoRecursos);
    }
}
