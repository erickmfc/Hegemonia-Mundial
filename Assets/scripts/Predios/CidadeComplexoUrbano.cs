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
/// 1. Imobiliária (capacidade configurável; Nova Aurora suporta até 1 milhão de habitantes).
/// 2. Comércio e Empregos (Metade de cada lugar: 500k moradores -> até 250k comércio/empregos).
/// 3. Energia elétrica (Consumo idêntico ao que casas e prédios consumiriam para esse montante populacional).
/// Integra-se transparentemente com o SistemaEconomiaImoveis, GerenciadorRecursos e SistemaGovernoMundial.
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
public class CidadeComplexoUrbano : MonoBehaviour
{
    public const int CapacidadeMaximaNovaAurora = 1_000_000;
    public const int PopulacaoInicialNovaAurora = 5_000;

    private static readonly HashSet<CidadeComplexoUrbano> CidadesAtivas = new HashSet<CidadeComplexoUrbano>();
    private static readonly List<CidadeComplexoUrbano> BufferCidades = new List<CidadeComplexoUrbano>(16);
    private static readonly List<int> BufferDeltaPopulacional = new List<int>(16);

    [Header("🏛️ Identidade e Tema")]
    [Tooltip("Tema visual e cultural da cidade")]
    public TemaCidade tema = TemaCidade.Moderna;
    [Tooltip("Nome exibido para esta metrópole")]
    public string nomeCidade = "Nova Metrópole";
    [Tooltip("Identificador da nação ou time proprietário")]
    public int teamId = 1;

    [Header("Expansão de cidades no mapa global")]
    [Tooltip("Ative para usar esta cidade pré-posicionada como uma etapa da expansão urbana do mapa global.")]
    public bool participarProgressaoCidadesGlobais;
    [Min(0)] public int ordemProgressaoGlobal;

    [Header("🏥 Saúde da cidade")]
    [Tooltip("Capacidade hospitalar preventiva inicial do complexo. Hospitais adicionais usam HospitalSaude.")]
    public int capacidadeHospitalarInicial = 400000;
    [Tooltip("Indica ao sistema populacional que a cidade já possui uma rede básica de atendimento.")]
    public bool possuiRedeSaudeBasica = true;

    [Header("🏠 Imobiliária (Moradia)")]
    [Tooltip("Capacidade habitacional total suportada pela cidade (Nova Aurora: até 1.000.000 moradores)")]
    [Range(500000, 5000000)]
    public int capacidadeHabitacional = 5000000;

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
    private bool capacidadeHabitacionalNotificada;
    private float timerSincronizacao = 0f;
    private float timerBlackout = 0f;

    private void Awake()
    {
        // Cidades fazem parte das entidades persistidas, inclusive quando
        // foram colocadas diretamente na cena. O SaveGame usa este marcador
        // para substituir a instancia-base ao restaurar um snapshot.
        SaveableEntity.Garantir(gameObject);
        GarantirEstrutura();
        GarantirSaude();
        ValidarLimites();
    }

    private void Start()
    {
        ValidarLimites();
        AlinharBaseAoTerrenoDoMapaGlobal();
        if (participarProgressaoCidadesGlobais)
            MarcadorTerritorio.GarantirInicializacaoCidadesGlobais(gameObject.scene.name, this);
        SincronizarEstruturaEconomica();
        RegistrarPopulacaoInicialNaNacao();
        populacaoRegistrada = true;

        NotificarSeCapacidadeHabitacionalAtingida();
    }

    private void AlinharBaseAoTerrenoDoMapaGlobal()
    {
        if (!Application.isPlaying || !gameObject.scene.name.Equals("GlobalMapRTS", StringComparison.OrdinalIgnoreCase))
            return;

        GlobalTerrainStreamer streamer = FindFirstObjectByType<GlobalTerrainStreamer>();
        GlobalWorldDefinition world = streamer != null ? streamer.world : null;
        if (world == null)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        float baseY = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                baseY = Mathf.Min(baseY, renderers[i].bounds.min.y);
        }

        if (float.IsInfinity(baseY))
            return;

        float terrainY = world.HeightAtWorld(transform.position.x, transform.position.z);
        transform.position += Vector3.up * (terrainY - baseY);
    }

    private void OnEnable()
    {
        capacidadeHabitacionalNotificada = false;
        CidadesAtivas.Add(this);
        GarantirEstrutura();
        GarantirSaude();
        SincronizarEstruturaEconomica();
        if (Application.isPlaying)
        {
            GerenciadorDivisaoTerritorial.GarantirInstancia();
            GerenciadorDivisaoTerritorial.Instancia?.RegistrarCidadeUrbana(this);
        }
    }

    private void OnDisable()
    {
        CidadesAtivas.Remove(this);
        if (Application.isPlaying)
            GerenciadorDivisaoTerritorial.Instancia?.RemoverCidadeUrbana(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparRegistroRuntime()
    {
        CidadesAtivas.Clear();
        BufferCidades.Clear();
        BufferDeltaPopulacional.Clear();
    }

    /// <summary>
    /// Sincroniza a população civil nacional com as cidades materializadas.
    /// Mantém a distribuição local existente e aplica somente a variação do
    /// total nacional, para não apagar migrações entre cidades a cada tick.
    /// </summary>
    public static void SincronizarPopulacaoNacional(int timeId, int populacaoCivil)
    {
        PrepararBufferCidades(timeId);
        GerenciadorDivisaoTerritorial gerenciador = GerenciadorDivisaoTerritorial.Instancia;
        long capacidadeAbstrata = 0;
        long residentesAbstratos = 0;
        gerenciador?.ObterTotaisCidadesAbstratas(timeId, out capacidadeAbstrata, out residentesAbstratos);
        if (BufferCidades.Count == 0 && capacidadeAbstrata <= 0) return;

        long capacidadeTotal = 0;
        for (int i = 0; i < BufferCidades.Count; i++)
            capacidadeTotal += Mathf.Max(0, BufferCidades[i].capacidadeHabitacional);
        capacidadeTotal += capacidadeAbstrata;
        if (capacidadeTotal <= 0) return;

        int residentesAlvo = (int)Math.Min(Mathf.Max(0, populacaoCivil), capacidadeTotal);
        long residentesAtuais = 0;
        for (int i = 0; i < BufferCidades.Count; i++)
        {
            CidadeComplexoUrbano cidade = BufferCidades[i];
            int capacidade = Mathf.Max(0, cidade.capacidadeHabitacional);
            int residenteValido = Mathf.Clamp(cidade.populacaoResidente, 0, capacidade);
            if (cidade.populacaoResidente != residenteValido)
                DefinirPopulacaoSincronizada(cidade, residenteValido);
            residentesAtuais += residenteValido;
        }
        residentesAtuais += residentesAbstratos;

        long delta = (long)residentesAlvo - residentesAtuais;
        if (delta > 0)
        {
            long margemMaterializada = Math.Max(0L, capacidadeTotal - capacidadeAbstrata
                - (residentesAtuais - residentesAbstratos));
            long pesoTotal = Math.Max(0L, capacidadeTotal - residentesAtuais);
            long deltaMaterializado = pesoTotal > 0 ? delta * margemMaterializada / pesoTotal : 0;
            DistribuirDeltaPopulacional(deltaMaterializado, margemMaterializada, adicionar: true);
            gerenciador?.AplicarDeltaPopulacionalAbstrato(timeId, delta - deltaMaterializado);
        }
        else if (delta < 0)
        {
            long moradoresMaterializados = residentesAtuais - residentesAbstratos;
            long pesoTotal = residentesAtuais;
            long deltaMaterializado = pesoTotal > 0 ? -delta * moradoresMaterializados / pesoTotal : 0;
            DistribuirDeltaPopulacional(deltaMaterializado, moradoresMaterializados, adicionar: false);
            gerenciador?.AplicarDeltaPopulacionalAbstrato(timeId, delta + deltaMaterializado);
        }
    }

    internal static void CopiarCidadesAtivas(List<CidadeComplexoUrbano> destino)
    {
        if (destino == null) return;

        CidadesAtivas.RemoveWhere(cidade => cidade == null || !cidade.isActiveAndEnabled);
        destino.Clear();
        foreach (CidadeComplexoUrbano cidade in CidadesAtivas)
        {
            if (cidade != null && cidade.isActiveAndEnabled)
                destino.Add(cidade);
        }
        destino.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
    }

    /// <summary>
    /// Move moradores entre cidades do mesmo país sem alterar a população
    /// nacional. Crescimento e perdas nacionais continuam em seus próprios
    /// sistemas; esta operação só muda a distribuição urbana.
    /// </summary>
    internal int TransferirPopulacaoPara(CidadeComplexoUrbano destino, int quantidadeSolicitada)
    {
        if (destino == null || destino == this || teamId <= 0 || destino.teamId != teamId || quantidadeSolicitada <= 0)
            return 0;

        ValidarLimites();
        destino.ValidarLimites();

        int transferidos = Mathf.Min(quantidadeSolicitada, Mathf.Min(populacaoResidente, destino.VagasHabitacionaisLivres));
        if (transferidos <= 0) return 0;

        populacaoResidente -= transferidos;
        destino.populacaoResidente += transferidos;
        SincronizarEstruturaEconomica();
        destino.SincronizarEstruturaEconomica();
        return transferidos;
    }

    private static void DistribuirDeltaPopulacional(long quantidade, long pesoTotal, bool adicionar)
    {
        if (quantidade <= 0 || pesoTotal <= 0) return;

        BufferDeltaPopulacional.Clear();
        long distribuidos = 0;
        for (int i = 0; i < BufferCidades.Count; i++)
        {
            CidadeComplexoUrbano cidade = BufferCidades[i];
            int capacidade = Mathf.Max(0, cidade.capacidadeHabitacional);
            int residentes = Mathf.Clamp(cidade.populacaoResidente, 0, capacidade);
            int peso = adicionar ? capacidade - residentes : residentes;
            int parcela = (int)Math.Min(peso, quantidade * peso / pesoTotal);
            BufferDeltaPopulacional.Add(parcela);
            distribuidos += parcela;
        }

        long restante = quantidade - distribuidos;
        for (int i = 0; i < BufferCidades.Count && restante > 0; i++)
        {
            CidadeComplexoUrbano cidade = BufferCidades[i];
            int capacidade = Mathf.Max(0, cidade.capacidadeHabitacional);
            int residentes = Mathf.Clamp(cidade.populacaoResidente, 0, capacidade);
            int margemRestante = adicionar
                ? capacidade - residentes - BufferDeltaPopulacional[i]
                : residentes - BufferDeltaPopulacional[i];
            if (margemRestante <= 0) continue;

            BufferDeltaPopulacional[i]++;
            restante--;
        }

        for (int i = 0; i < BufferCidades.Count; i++)
        {
            int parcela = BufferDeltaPopulacional[i];
            if (parcela <= 0) continue;

            CidadeComplexoUrbano cidade = BufferCidades[i];
            int residentes = Mathf.Clamp(cidade.populacaoResidente, 0, Mathf.Max(0, cidade.capacidadeHabitacional));
            int novoTotal = adicionar ? residentes + parcela : residentes - parcela;
            DefinirPopulacaoSincronizada(cidade, novoTotal);
        }
    }

    private static void DefinirPopulacaoSincronizada(CidadeComplexoUrbano cidade, int residentes)
    {
        if (cidade == null || cidade.populacaoResidente == residentes) return;
        cidade.populacaoResidente = residentes;
        cidade.SincronizarEstruturaEconomica();
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
        capacidadeHabitacional = Mathf.Clamp(capacidadeHabitacional, 500000, 5000000);
        if (EhNovaAurora(nomeCidade))
            capacidadeHabitacional = CapacidadeMaximaNovaAurora;
        populacaoResidente = Mathf.Clamp(populacaoResidente, 0, capacidadeHabitacional);
        qualidadeVida = Mathf.Clamp(qualidadeVida, 0, 100);
        proporcaoComercio = 0.5f;
    }

    public static bool EhNovaAurora(string nome)
    {
        return !string.IsNullOrWhiteSpace(nome)
            && (string.Equals(nome.Trim(), "Nova Aurora", StringComparison.OrdinalIgnoreCase)
                || nome.Trim().StartsWith("Nova Aurora ", StringComparison.OrdinalIgnoreCase));
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
        if (populacaoRegistrada)
            NotificarSeCapacidadeHabitacionalAtingida();
    }

    private void NotificarSeCapacidadeHabitacionalAtingida()
    {
        if (capacidadeHabitacionalNotificada || !Application.isPlaying || !populacaoRegistrada
            || teamId <= 0 || populacaoResidente < capacidadeHabitacional)
            return;

        capacidadeHabitacionalNotificada = true;
        MarcadorTerritorio.NotificarCidadeAtingiuCapacidade(this);
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

    private void OnDestroy()
    {
        CidadesAtivas.Remove(this);
        MarcadorTerritorio.RemoverCidadeGuiaExpansao(this);
        if (Application.isPlaying)
            GerenciadorDivisaoTerritorial.Instancia?.RemoverCidadeUrbana(this);

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
