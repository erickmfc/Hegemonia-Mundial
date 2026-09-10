using System;
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
    [Tooltip("Capacidade habitacional total suportada pela cidade (500.000 a 1.500.000 moradores)")]
    [Range(500000, 1500000)]
    public int capacidadeHabitacional = 1000000;

    [Tooltip("População atualmente residente na cidade")]
    public int populacaoResidente = 500000;

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
    private int populacaoAdicionadaRecursos = 0;
    private int limitePopulacaoAdicionado = 0;
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

        // Integração com o GerenciadorRecursos se pertencer ao time do jogador
        bool eJogador = SistemaGovernoMundial.Instancia == null || teamId == SistemaGovernoMundial.Instancia.teamJogador;
        if (eJogador && GerenciadorRecursos.Instancia != null)
        {
            GerenciadorRecursos.Instancia.AumentarLimitePopulacao(capacidadeHabitacional);
            limitePopulacaoAdicionado = capacidadeHabitacional;

            int adicionar = Mathf.Min(populacaoResidente, capacidadeHabitacional);
            if (adicionar > 0)
            {
                GerenciadorRecursos.Instancia.AdicionarPopulacao(adicionar);
                populacaoAdicionadaRecursos = adicionar;
            }
        }
    }

    private void OnEnable()
    {
        GarantirEstrutura();
        GarantirSaude();
        SincronizarEstruturaEconomica();
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
        capacidadeHabitacional = Mathf.Clamp(capacidadeHabitacional, 500000, 1500000);
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
        populacaoResidente = Mathf.Clamp(populacaoResidente + delta, 0, capacidadeHabitacional);
        int variacao = populacaoResidente - antes;

        bool eJogador = SistemaGovernoMundial.Instancia == null || teamId == SistemaGovernoMundial.Instancia.teamJogador;
        if (eJogador && GerenciadorRecursos.Instancia != null && variacao != 0)
        {
            if (variacao > 0)
            {
                GerenciadorRecursos.Instancia.AdicionarPopulacao(variacao);
                populacaoAdicionadaRecursos += variacao;
            }
            else
            {
                GerenciadorRecursos.Instancia.RemoverPopulacao(-variacao);
                populacaoAdicionadaRecursos = Mathf.Max(0, populacaoAdicionadaRecursos + variacao);
            }
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
        bool eJogador = SistemaGovernoMundial.Instancia == null || teamId == SistemaGovernoMundial.Instancia.teamJogador;
        if (eJogador && GerenciadorRecursos.Instancia != null)
        {
            if (limitePopulacaoAdicionado > 0)
            {
                GerenciadorRecursos.Instancia.AumentarLimitePopulacao(-limitePopulacaoAdicionado);
            }
            if (populacaoAdicionadaRecursos > 0)
            {
                GerenciadorRecursos.Instancia.RemoverPopulacao(populacaoAdicionadaRecursos);
            }
        }
    }
}
