using System;
using System.Collections.Generic;
using UnityEngine;

public enum AreaInvestimentoAgricola
{
    Producao, Mecanizacao, Irrigacao, Fertilizacao, SementesBiotecnologia,
    Agrotoxicos, Pesquisa, Agroindustria, Logistica, Armazenamento,
    RecursosHidricos, Pecuaria
}

/// <summary>Calcula a produção agrícola nacional na passagem de cada dia de jogo.</summary>
[DisallowMultipleComponent]
public sealed class AgriculturaNacional : MonoBehaviour
{
    public const string PesquisaAgrotoxicosId = "pesquisa_tecnologia_agrotoxicos";
    public const int InvestimentoMinimoSemanal = 100; // $100M por semana
    private const float ProducaoBaseDiaria = 24f;
    private static readonly string[] Culturas = {
        "comida_milho", "comida_batata", "comida_feijao", "comida_trigo",
        "comida_arroz", "comida_cana", "comida_soja", "comida_cafe", "comida_cacau",
        "comida_mandioca", "comida_aveia", "comida_cevada", "comida_tomate",
        "comida_frutas", "comida_hortalicas"
    };

    [Header("Consumo configurável por tonelada produzida")]
    [Min(0f)] public float aguaPorTonelada = 2f;
    [Min(0f)] public float sementesPorTonelada = 0.03f;
    [Min(0f)] public float fertilizantePorTonelada = 0.02f;
    [Min(0f)] public float agrotoxicosPorTonelada = 0.005f;
    [Range(0f, 1f)] public float produtividadeSemFertilizante = 0.35f;
    [Range(0f, 1f)] public float produtividadeSemSementes = 0.15f;
    [Range(0f, 1f)] public float produtividadeSemAgrotoxico = 0.9f;
    [Min(0)] public int producaoBaseAgrotoxicoDiaria = 2;

    private SistemaGovernoMundial governo;
    private int ultimoDia;

    public static string NomeArea(AreaInvestimentoAgricola area)
    {
        switch (area)
        {
            case AreaInvestimentoAgricola.Producao: return "Produção agrícola";
            case AreaInvestimentoAgricola.Mecanizacao: return "Mecanização";
            case AreaInvestimentoAgricola.Irrigacao: return "Irrigação";
            case AreaInvestimentoAgricola.Fertilizacao: return "Fertilização";
            case AreaInvestimentoAgricola.SementesBiotecnologia: return "Sementes e biotecnologia";
            case AreaInvestimentoAgricola.Agrotoxicos: return "Agrotóxicos";
            case AreaInvestimentoAgricola.Pesquisa: return "Pesquisa agrícola";
            case AreaInvestimentoAgricola.Agroindustria: return "Agroindústria";
            case AreaInvestimentoAgricola.Logistica: return "Logística agrícola";
            case AreaInvestimentoAgricola.Armazenamento: return "Armazenamento";
            case AreaInvestimentoAgricola.RecursosHidricos: return "Recursos hídricos";
            default: return "Pecuária";
        }
    }

    private void Awake()
    {
        governo = GetComponent<SistemaGovernoMundial>();
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        GerenciadorTempo.GarantirInstancia();
        ultimoDia = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1;
        GarantirEstoqueHidricoInicial();
    }

    private void OnEnable()
    {
        GerenciadorTempo.GarantirInstancia();
        if (GerenciadorTempo.Instancia != null) GerenciadorTempo.Instancia.OnDataAlterada += ProcessarNovoDia;
    }

    private void OnDisable()
    {
        if (GerenciadorTempo.Instancia != null) GerenciadorTempo.Instancia.OnDataAlterada -= ProcessarNovoDia;
    }

    public bool DefinirInvestimentoSemanal(int teamId, int milhoes)
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        int valor = Mathf.Clamp(milhoes, InvestimentoMinimoSemanal, 10000);
        if (pais == null || !governo.TentarPagar(teamId, valor)) return false;
        pais.investimentoAgricolaSemanal = valor;
        governo.NotificarGovernoAtualizado();
        return true;
    }

    public bool InvestirArea(int teamId, AreaInvestimentoAgricola area)
    {
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null) return false;
        GarantirDadosArea(pais);
        int index = (int)area;
        if (index < 0 || index >= pais.niveisInvestimentoAgricola.Length || pais.niveisInvestimentoAgricola[index] >= 10) return false;
        int custo = 100 + pais.niveisInvestimentoAgricola[index] * 50;
        if (!governo.TentarPagar(teamId, custo)) return false;
        pais.niveisInvestimentoAgricola[index]++;
        if (area == AreaInvestimentoAgricola.RecursosHidricos)
        {
            pais.aguaMaxima += 500;
            if (teamId == governo.teamJogador && GerenciadorArmazens.Instancia != null && GerenciadorArmazens.Instancia.armazemRecursos != null)
                GerenciadorArmazens.Instancia.armazemRecursos.aguaMaximo += 500;
        }
        if (area == AreaInvestimentoAgricola.Armazenamento)
        {
            pais.capacidadeArmazenamentoAgricola += 1000;
        }
        governo.NotificarGovernoAtualizado();
        return true;
    }

    private static void GarantirDadosArea(DadosPaisGoverno pais)
    {
        if (pais.niveisInvestimentoAgricola == null || pais.niveisInvestimentoAgricola.Length != 12)
            Array.Resize(ref pais.niveisInvestimentoAgricola, 12);
    }

    private void GarantirEstoqueHidricoInicial()
    {
        if (governo == null || GerenciadorArmazens.Instancia == null || GerenciadorArmazens.Instancia.armazemRecursos == null) return;
        DadosPaisGoverno jogador = governo.ObterPais(governo.teamJogador);
        DadosArmazemRecursos armazem = GerenciadorArmazens.Instancia.armazemRecursos;
        if (jogador != null && !jogador.estoqueHidricoInicializado && armazem.agua <= 0 && jogador.agua > 0)
        {
            armazem.AdicionarRecurso(TipoRecurso.Agua, Mathf.Min(jogador.agua, armazem.aguaMaximo));
            jogador.agua = armazem.agua;
            GerenciadorArmazens.Instancia.NotificarAtualizacaoManual();
        }
        if (jogador != null) jogador.estoqueHidricoInicializado = true;
    }

    public static float CalcularProducaoDiaria(float capacidade, float eficiencia, float agua,
        float insumos, float tecnologia, float mecanizacao, float logistica)
    {
        return Mathf.Max(0f, capacidade) * Mathf.Clamp(eficiencia, 0f, 2f)
            * Mathf.Clamp01(agua) * Mathf.Clamp01(insumos) * Mathf.Clamp(tecnologia, 0f, 2f)
            * Mathf.Clamp(mecanizacao, 0f, 2f) * Mathf.Clamp01(logistica);
    }

    private void ProcessarNovoDia()
    {
        GarantirEstoqueHidricoInicial();
        if (governo == null) governo = SistemaGovernoMundial.Instancia;
        if (governo == null || GerenciadorTempo.Instancia == null) return;
        int dia = Mathf.Max(1, GerenciadorTempo.Instancia.totalDias);
        if (dia <= ultimoDia) return;
        ultimoDia = dia;
        IReadOnlyList<DadosPaisGoverno> paises = governo.Paises;
        for (int i = 0; i < paises.Count; i++)
        {
            DadosPaisGoverno pais = paises[i];
            if (pais == null) continue;
            GarantirDadosArea(pais);
            if ((dia - 1) % 7 == 0)
            {
                int custoSemanal = Mathf.Max(InvestimentoMinimoSemanal, pais.investimentoAgricolaSemanal);
                pais.investimentoAgricolaSemRecursos = !governo.TentarPagar(pais.teamId, custoSemanal);
            }
            Produzir(pais);
        }
        governo.NotificarGovernoAtualizado();
    }

    private void Produzir(DadosPaisGoverno pais)
    {
        pais.producaoAgricolaDiaria = 0;
        bool tecnologia = TecnologiaAgrotoxicosDesbloqueada(pais.teamId);
        pais.producaoAgrotoxicosDiaria = 0;
        if (tecnologia)
        {
            int producaoAgro = Mathf.Max(0, producaoBaseAgrotoxicoDiaria + pais.niveisInvestimentoAgricola[(int)AreaInvestimentoAgricola.Agrotoxicos] * 2);
            governo.AdicionarEstoque(pais.teamId, "agrotoxicos", producaoAgro);
            pais.producaoAgrotoxicosDiaria = producaoAgro;
            AtualizarOferta("agrotoxicos", producaoAgro);
        }

        int investimento = Mathf.Max(0, pais.investimentoAgricolaSemanal);
        float bonusInvestimento = pais.investimentoAgricolaSemRecursos ? 0f
            : 0.005f + 0.115f * (1f - Mathf.Exp(-Mathf.Max(0, investimento - InvestimentoMinimoSemanal) / 3200f));
        int[] niveis = pais.niveisInvestimentoAgricola;
        float eficiencia = Mathf.Clamp(pais.eficienciaAgricola + bonusInvestimento + niveis[(int)AreaInvestimentoAgricola.Pesquisa] * 0.015f, 0.5f, 1.8f);
        float capacidade = ProducaoBaseDiaria + pais.capacidadeAgricola + niveis[(int)AreaInvestimentoAgricola.Producao] * 5f
            + niveis[(int)AreaInvestimentoAgricola.Agroindustria] * 2f;
        float mecanizacao = 1f + pais.nivelMecanizacaoAgricola + niveis[(int)AreaInvestimentoAgricola.Mecanizacao] * 0.025f;
        float logistica = Mathf.Clamp01(pais.eficienciaLogisticaAgricola + niveis[(int)AreaInvestimentoAgricola.Logistica] * 0.01f);
        int aguaDisponivel = governo.ObterEstoque(pais.teamId, RecursoMercado.Agua);
        float demandaAgua = capacidade * aguaPorTonelada / (1f + niveis[(int)AreaInvestimentoAgricola.Irrigacao] * 0.1f);
        float fatorAgua = CalcularFatorAgua(aguaDisponivel, demandaAgua, pais.eficienciaHidrica);
        float sementes = CalcularFatorInsumo(governo.ObterEstoque(pais.teamId, "sementes"), capacidade * sementesPorTonelada,
            produtividadeSemSementes, niveis[(int)AreaInvestimentoAgricola.SementesBiotecnologia] * 0.01f);
        float fertilizante = CalcularFatorInsumo(governo.ObterEstoque(pais.teamId, "fertilizante_organico"), capacidade * fertilizantePorTonelada,
            produtividadeSemFertilizante, niveis[(int)AreaInvestimentoAgricola.Fertilizacao] * 0.02f);
        float pesticida = CalcularFatorInsumo(governo.ObterEstoque(pais.teamId, "agrotoxicos"), capacidade * agrotoxicosPorTonelada,
            produtividadeSemAgrotoxico, 0f);
        float diaria = CalcularProducaoDiaria(capacidade, eficiencia, fatorAgua,
            Mathf.Clamp01(sementes * fertilizante * pesticida), 1f + niveis[(int)AreaInvestimentoAgricola.Pesquisa] * 0.01f,
            mecanizacao, logistica);
        int quantidade = Mathf.Max(0, Mathf.RoundToInt(diaria));
        quantidade = Mathf.Min(quantidade, Mathf.Max(0, pais.capacidadeArmazenamentoAgricola - pais.comida));
        if (quantidade <= 0) return;

        Consumir(pais.teamId, "agua", Mathf.CeilToInt(quantidade * aguaPorTonelada / (1f + niveis[(int)AreaInvestimentoAgricola.Irrigacao] * 0.1f)));
        Consumir(pais.teamId, "sementes", Mathf.CeilToInt(quantidade * sementesPorTonelada));
        Consumir(pais.teamId, "fertilizante_organico", Mathf.CeilToInt(quantidade * fertilizantePorTonelada));
        Consumir(pais.teamId, "agrotoxicos", Mathf.CeilToInt(quantidade * agrotoxicosPorTonelada));
        DistribuirCulturas(pais, quantidade);
    }

    public static float CalcularFatorAgua(float disponivel, float demanda, float eficienciaHidrica)
    {
        float disponibilidade = demanda <= 0f ? 1f : Mathf.Sqrt(Mathf.Clamp01(disponivel / demanda));
        return disponibilidade * Mathf.Clamp01(eficienciaHidrica);
    }

    public static float CalcularFatorInsumo(float estoque, float demanda, float minimo, float bonus)
    {
        if (demanda <= 0f) return 1f + bonus;
        float proporcao = Mathf.Clamp01(estoque / demanda);
        return Mathf.Clamp(Mathf.Lerp(Mathf.Clamp01(minimo), 1f, Mathf.Sqrt(proporcao)) * (1f + bonus), 0f, 1.5f);
    }

    private void Consumir(int teamId, string recursoId, int quantidade)
    {
        if (quantidade <= 0) return;
        int disponivel = governo.ObterEstoque(teamId, recursoId);
        governo.RemoverEstoque(teamId, recursoId, Mathf.Min(disponivel, quantidade));
    }

    private void DistribuirCulturas(DadosPaisGoverno pais, int total)
    {
        if (pais.mixCulturaAgricola == null || pais.mixCulturaAgricola.Length != Culturas.Length)
            Array.Resize(ref pais.mixCulturaAgricola, Culturas.Length);
        if (pais.saldoFracionarioCulturas == null || pais.saldoFracionarioCulturas.Length != Culturas.Length)
            Array.Resize(ref pais.saldoFracionarioCulturas, Culturas.Length);
        float somaPesos = 0f;
        for (int i = 0; i < Culturas.Length; i++) somaPesos += Mathf.Max(0f, pais.mixCulturaAgricola[i]);
        bool pesosIguais = somaPesos <= 0f;
        if (pesosIguais) somaPesos = Culturas.Length;
        int[] partes = new int[Culturas.Length];
        int alocado = 0;
        for (int i = 0; i < Culturas.Length; i++)
        {
            float peso = pesosIguais ? 1f : Mathf.Max(0f, pais.mixCulturaAgricola[i]);
            pais.saldoFracionarioCulturas[i] += total * peso / somaPesos;
            partes[i] = Mathf.FloorToInt(Mathf.Max(0f, pais.saldoFracionarioCulturas[i]));
            pais.saldoFracionarioCulturas[i] -= partes[i];
            alocado += partes[i];
        }
        while (alocado < total)
        {
            int melhor = 0;
            for (int i = 1; i < Culturas.Length; i++)
                if (pais.saldoFracionarioCulturas[i] > pais.saldoFracionarioCulturas[melhor]) melhor = i;
            partes[melhor]++;
            pais.saldoFracionarioCulturas[melhor] -= 1f;
            alocado++;
        }
        for (int i = 0; i < Culturas.Length; i++)
        {
            if (partes[i] <= 0) continue;
            governo.AdicionarEstoque(pais.teamId, Culturas[i], partes[i]);
            AtualizarOferta(Culturas[i], partes[i]);
        }
        pais.producaoAgricolaDiaria = total;
    }

    private void AtualizarOferta(string id, int quantidade)
    {
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (mercado == null || quantidade <= 0) return;
        DadosItemMercado item = mercado.ObterItem(id);
        if (item == null) return;
        item.estoqueGlobal = Mathf.Clamp(item.estoqueGlobal + quantidade, 0, int.MaxValue);
        item.oferta = Mathf.Clamp(item.oferta + quantidade * 0.02f, 0f, 160f);
    }

    public bool TecnologiaAgrotoxicosDesbloqueada(int teamId)
    {
        DadosPaisGoverno pais = governo != null ? governo.ObterPais(teamId) : null;
        if (pais == null || pais.pesquisas == null) return false;
        for (int i = 0; i < pais.pesquisas.Count; i++)
        {
            PesquisaNacionalEstado pesquisa = pais.pesquisas[i];
            if (pesquisa != null && string.Equals(pesquisa.id, PesquisaAgrotoxicosId, StringComparison.OrdinalIgnoreCase))
                return pesquisa.concluida;
        }
        return false;
    }
}
