using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema Industrial Central — responsável por:
/// 1. Gerar perfis geológicos dos países (1 vez, permanente)
/// 2. Processar extração virtual de minerais (1x por dia de jogo)
/// 3. Processar fila de refino (bruto → refinado)
/// 4. Sincronizar estoques minerais com DadosPaisGoverno
///
/// A "mina" não tem prefab, GameObject ou presença no mapa.
/// Existe apenas como dados pertencentes ao país.
/// </summary>
public class SistemaIndustrial : MonoBehaviour
{
    public static SistemaIndustrial Instancia { get; private set; }

    [Header("Configuração")]
    [Tooltip("Dias de jogo entre cada ciclo de refino (0 = todo dia)")]
    public int intervaloDiasRefino = 1;

    [Header("Debug")]
    public bool mostrarLogsDiarios = false;

    // ── Dados em memória ─────────────────────────────────────────────────
    private Dictionary<int, PerfilMineralPais> perfis   = new Dictionary<int, PerfilMineralPais>();
    private Dictionary<int, EstoqueMineral>    estoques = new Dictionary<int, EstoqueMineral>();

    // ── Eventos ──────────────────────────────────────────────────────────
    public event Action<int> OnCicloDiarioProcessado; // teamId

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (GerenciadorTempo.Instancia != null)
            GerenciadorTempo.Instancia.OnDataAlterada += OnDiaPassou;
    }

    private void OnDisable()
    {
        if (GerenciadorTempo.Instancia != null)
            GerenciadorTempo.Instancia.OnDataAlterada -= OnDiaPassou;
    }

    private void Start()
    {
        // Caso o GerenciadorTempo já exista mas OnEnable foi chamado antes dele:
        if (GerenciadorTempo.Instancia != null)
        {
            GerenciadorTempo.Instancia.OnDataAlterada -= OnDiaPassou;
            GerenciadorTempo.Instancia.OnDataAlterada += OnDiaPassou;
        }

        // Garante que todos os países já existentes têm perfil gerado
        if (SistemaGovernoMundial.Instancia != null)
        {
            foreach (DadosPaisGoverno pais in SistemaGovernoMundial.Instancia.Paises)
            {
                if (pais != null) GarantirPerfil(pais.teamId);
            }
        }
    }

    // ── API pública ──────────────────────────────────────────────────────

    /// <summary>Retorna (criando se necessário) o perfil mineral do país.</summary>
    public PerfilMineralPais ObterPerfil(int teamId)
    {
        if (!perfis.TryGetValue(teamId, out PerfilMineralPais perfil))
        {
            perfil = new PerfilMineralPais();
            perfil.GerarPerfil(teamId);
            perfis[teamId] = perfil;
        }
        return perfil;
    }

    /// <summary>Garante que o perfil existe (sem gerar se já existe). Útil ao carregar o save.</summary>
    public PerfilMineralPais GarantirPerfil(int teamId)
    {
        return ObterPerfil(teamId);
    }

    /// <summary>Carrega um perfil a partir do save (preserva o perfil existente).</summary>
    public void CarregarPerfil(PerfilMineralPais perfilSalvo)
    {
        if (perfilSalvo == null) return;
        perfis[perfilSalvo.teamId] = perfilSalvo;
    }

    /// <summary>Retorna (criando se necessário) o estoque mineral do país.</summary>
    public EstoqueMineral ObterEstoque(int teamId)
    {
        if (!estoques.TryGetValue(teamId, out EstoqueMineral estoque))
        {
            estoque = new EstoqueMineral { teamId = teamId };
            estoques[teamId] = estoque;
        }
        return estoque;
    }

    /// <summary>Carrega um estoque a partir do save.</summary>
    public void CarregarEstoque(EstoqueMineral estoqueSalvo)
    {
        if (estoqueSalvo == null) return;
        estoques[estoqueSalvo.teamId] = estoqueSalvo;
    }

    /// <summary>Retorna todos os perfis para serialização no save.</summary>
    public List<PerfilMineralPais> TodosOsPerfis() => new List<PerfilMineralPais>(perfis.Values);

    /// <summary>Retorna todos os estoques para serialização no save.</summary>
    public List<EstoqueMineral> TodosOsEstoques() => new List<EstoqueMineral>(estoques.Values);

    // ── Ciclo Diário ─────────────────────────────────────────────────────

    private void OnDiaPassou()
    {
        if (SistemaIndustrialNacional.Instancia != null) return;
        if (SistemaGovernoMundial.Instancia == null) return;

        int diaAtual = GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1;

        foreach (DadosPaisGoverno pais in SistemaGovernoMundial.Instancia.Paises)
        {
            if (pais == null) continue;
            ProcessarExtracao(pais, diaAtual);
            if (diaAtual % Mathf.Max(1, intervaloDiasRefino) == 0)
                ProcessarRefino(pais);
            SincronizarComPais(pais);
            OnCicloDiarioProcessado?.Invoke(pais.teamId);
        }
    }

    // ── Extração Virtual ─────────────────────────────────────────────────

    private void ProcessarExtracao(DadosPaisGoverno pais, int diaAtual)
    {
        PerfilMineralPais perfil  = ObterPerfil(pais.teamId);
        EstoqueMineral    estoque = ObterEstoque(pais.teamId);

        foreach (RecursoMineral recurso in System.Enum.GetValues(typeof(RecursoMineral)))
        {
            if (!perfil.EstaExtraindo(recurso)) continue;

            AbundanciaMineralNivel nivel = perfil.ObterAbundancia(recurso);
            if (nivel == AbundanciaMineralNivel.Inexistente) continue;

            float producaoBruta = CalcularProducaoDiaria(pais, recurso, nivel, diaAtual);
            if (producaoBruta <= 0f) continue;

            estoque.AdicionarBruto(recurso, producaoBruta);

            if (mostrarLogsDiarios)
            {
                Debug.Log($"[SistemaIndustrial] {pais.nomePais} extraiu {producaoBruta:N0}t de " +
                          $"{recurso} (nível: {nivel}) no dia {diaAtual}");
            }
        }
    }

    private float CalcularProducaoDiaria(DadosPaisGoverno pais, RecursoMineral recurso,
                                          AbundanciaMineralNivel nivel, int diaAtual)
    {
        float producaoBase   = TabelaProducaoMineral.ObterProducaoBase(nivel);
        if (producaoBase <= 0f) return 0f;

        // Multiplicadores
        float eficiencia   = Mathf.Clamp01(pais.nivelIndustrial / 100f);
        float energia      = Mathf.Clamp01(pais.energia / 200f);         // 200 unidades = 100% operacional
        float estabilidade = Mathf.Clamp01(pais.estabilidade / 100f);

        // Variação diária determinista (seed: teamId + recurso + dia)
        int seed = pais.teamId * 10000 + (int)recurso * 100 + diaAtual;
        System.Random rng = new System.Random(seed);
        float variacao = 0.75f + (float)rng.NextDouble() * 0.50f; // 75% a 125%

        float producaoFinal = producaoBase * eficiencia * energia * estabilidade * variacao;

        // Limites globais
        float limiteMax = TabelaProducaoMineral.LimiteMaximo(recurso, nivel);
        producaoFinal = Mathf.Clamp(producaoFinal, TabelaProducaoMineral.LimiteMinimo, limiteMax);

        return producaoFinal;
    }

    // ── Fila de Refino ───────────────────────────────────────────────────

    private void ProcessarRefino(DadosPaisGoverno pais)
    {
        PerfilMineralPais perfil  = ObterPerfil(pais.teamId);
        EstoqueMineral    estoque = ObterEstoque(pais.teamId);

        // Energia necessária para refino (cada lote de refino consome energia)
        float energiaParaRefino = pais.energia * 0.05f; // consome até 5% da energia disponível
        if (pais.energia < 10f) return; // sem energia, sem refino

        foreach (ReceitaRefino receita in TabelaRefino.Receitas)
        {
            if (!perfil.EstaRefinando(receita.resultado)) continue;

            // Verifica e consome matéria-prima A (bruta)
            if (receita.quantidadeA > 0f)
            {
                if (!estoque.ConsumirBruto(receita.materiaA, receita.quantidadeA))
                    continue; // sem matéria-prima suficiente
            }

            // Verifica e consome matéria-prima B (se necessário)
            if (receita.usaSegundaMateria && receita.quantidadeB > 0f)
            {
                if (receita.materiaBasRefinada)
                {
                    // B é um material refinado (ex: Cobre Eletrolítico para Eletrônicos)
                    if (!estoque.ConsumirRefinado(receita.materiaBRefinada, receita.quantidadeB))
                    {
                        // Devolve A se B não estava disponível
                        estoque.AdicionarBruto(receita.materiaA, receita.quantidadeA);
                        continue;
                    }
                }
                else
                {
                    if (!estoque.ConsumirBruto(receita.materiaA, receita.quantidadeB))
                    {
                        estoque.AdicionarBruto(receita.materiaA, receita.quantidadeA);
                        continue;
                    }
                }
            }

            // Produz o material refinado
            float producaoRefinada = receita.quantidadeA * receita.rendimento;
            estoque.AdicionarRefinado(receita.resultado, producaoRefinada);

            if (mostrarLogsDiarios)
            {
                Debug.Log($"[SistemaIndustrial] {pais.nomePais} refinou {producaoRefinada:N0}t de " +
                          $"{receita.resultado}");
            }
        }

        estoque.Validar();
    }

    // ── Sincronização com DadosPaisGoverno ───────────────────────────────

    private void SincronizarComPais(DadosPaisGoverno pais)
    {
        EstoqueMineral estoque = ObterEstoque(pais.teamId);

        // Copia os valores do estoque mineral para os campos do pais
        pais.minerioFerro               = estoque.minerioFerro;
        pais.minerioCobre               = estoque.minerioCobre;
        pais.bauxita                    = estoque.bauxita;
        pais.minerioTitanio             = estoque.minerioTitanio;
        pais.uranioBruto                = estoque.uranioBruto;
        pais.acoEstrutural              = estoque.acoEstrutural;
        pais.cobreEletrolitico          = estoque.cobreEletrolitico;
        pais.duraluminio                = estoque.duraluminio;
        pais.ligaTitanio                = estoque.ligaTitanio;
        pais.componentesEletronicos     = estoque.componentesEletronicos;
        pais.uranioEnriquecido          = estoque.uranioEnriquecido;
    }

    /// <summary>
    /// Sincroniza DadosPaisGoverno → EstoqueMineral (ao carregar save).
    /// </summary>
    public void SincronizarDoPais(DadosPaisGoverno pais)
    {
        EstoqueMineral estoque = ObterEstoque(pais.teamId);

        estoque.minerioFerro            = pais.minerioFerro;
        estoque.minerioCobre            = pais.minerioCobre;
        estoque.bauxita                 = pais.bauxita;
        estoque.minerioTitanio          = pais.minerioTitanio;
        estoque.uranioBruto             = pais.uranioBruto;
        estoque.acoEstrutural           = pais.acoEstrutural;
        estoque.cobreEletrolitico       = pais.cobreEletrolitico;
        estoque.duraluminio             = pais.duraluminio;
        estoque.ligaTitanio             = pais.ligaTitanio;
        estoque.componentesEletronicos  = pais.componentesEletronicos;
        estoque.uranioEnriquecido       = pais.uranioEnriquecido;
    }

    // ── Métodos de Controle (chamados pela UI ou pelo jogador) ───────────

    /// <summary>Ativa/desativa extração de um recurso para um país.</summary>
    public void SetExtracao(int teamId, RecursoMineral recurso, bool ativo)
    {
        ObterPerfil(teamId).SetExtracao(recurso, ativo);
    }

    /// <summary>Ativa/desativa refino de um material para um país.</summary>
    public void SetRefino(int teamId, MaterialRefinado material, bool ativo)
    {
        ObterPerfil(teamId).SetRefino(material, ativo);
    }

    /// <summary>Relatório de produção simulada para o dia informado (sem modificar o estoque).</summary>
    public float SimularProducao(int teamId, RecursoMineral recurso, int dia)
    {
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia?.ObterPais(teamId);
        if (pais == null) return 0f;
        PerfilMineralPais perfil = ObterPerfil(teamId);
        AbundanciaMineralNivel nivel = perfil.ObterAbundancia(recurso);
        if (nivel == AbundanciaMineralNivel.Inexistente) return 0f;
        return CalcularProducaoDiaria(pais, recurso, nivel, dia);
    }
}
