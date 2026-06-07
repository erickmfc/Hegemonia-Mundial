using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class CidadeEstado
{
    public string id;
    public string nome;
    public int teamID;
    public bool ehEstado; // Se for baseado em uma Prefeitura
    public int populacaoCivil;
    public int capacidadeHabitacional;
    
    [Header("Infraestrutura")]
    public bool temAeroporto;
    public bool temPorto;
    
    [Header("Identidade Regional")]
    public float scoreIndustrial;
    public float scoreComercial;
    public float scoreAgricola;
    public float scoreTurismo;
    public float scoreLogistica;
    public float scoreEnergia;
    public string identidadePrincipal = "Região em Desenvolvimento";
    
    [Header("Atratividade e Economia")]
    [Range(0f, 100f)] public float atratividade = 50f;
    public int empregosTotais;
    public int vagasDeEmpregoAbertas;

    public MarcadorTerritorio marcador;

    public CidadeEstado(string id, string nome, int teamID, bool ehEstado, MarcadorTerritorio marcador)
    {
        this.id = id;
        this.nome = nome;
        this.teamID = teamID;
        this.ehEstado = ehEstado;
        this.marcador = marcador;
        this.populacaoCivil = 0;
        this.capacidadeHabitacional = 0;
        this.temAeroporto = false;
        this.temPorto = false;
    }
}

public class GerenciadorDivisaoTerritorial : MonoBehaviour
{
    public static GerenciadorDivisaoTerritorial Instancia { get; private set; }

    public List<CidadeEstado> cidades = new List<CidadeEstado>();

    public event Action OnDivisaoTerritorialAtualizada;

    private HashSet<string> nomesUsados = new HashSet<string>();
    private float nextRecalculateTime = 0f;
    private const float RecalculateInterval = 2.0f; // Recalcula a cada 2 segundos

    #region Pools de Nomes (100+ para Jogador, 100+ para IA, 10 para Neutro)

    private static readonly string[] nomesJogador = new string[]
    {
        "Porto Real", "Forte Alvorada", "Cidadela de Aco", "Nova Esperanca", "Metropole de Atlas", 
        "Vale Verde", "Sanctum", "Pico da Aguia", "Baia da Vitoria", "Nova Alexandria", 
        "Porto Seguro", "Alto do Sol", "Vanguardia", "Santuario", "Bastiao", 
        "Monte Olimpo", "Vale da Paz", "Bela Vista", "Campina Grande", "Rio Bravo", 
        "Nova Yorkia", "Soberania", "Ilha da Gloria", "Ponta de Lanca", "Catedral", 
        "Aurora", "Fronteira Oeste", "Nirvana", "Grand Vista", "Horizonte", 
        "Miramar", "Sol Nascente", "Eldorado", "Arcadia", "Fenix", 
        "Mar Aberto", "Cabo Frio", "Ribeirao", "Belo Monte", "Luz Divina", 
        "Porto Fino", "Serra Negra", "Planalto", "Terras Altas", "Nova Tripoli", 
        "Oasis", "Cresta de Ferro", "Nova Cartago", "Fortaleza", "Guarda Real", 
        "Ponte Alta", "Colina Verde", "Bastiao Leste", "Vigilia", "Ninho do Falcao", 
        "Cupula do Ceu", "Passagem Real", "Rio Doce", "Nova Esparta", "Mare Alta", 
        "Baia Azul", "Cachoeira Alta", "Pico Negro", "Fronteira Sul", "Nova Atenas", 
        "Forte Cristal", "Refugio do Rei", "Campo Belo", "Alvorada Nova", "Mirante Real", 
        "Vale do Luar", "Ponta Negra", "Nova Roma", "Porto Dourado", "Serra do Sol", 
        "Nova Bizancio", "Terra Nova", "Vila Rica", "Belo Vale", "Pedra Branca", 
        "Canto da Sereia", "Guarda Norte", "Nova Tebas", "Forte Nobre", "Vale de Ferro", 
        "Metropole Celeste", "Porto da Uniao", "Bela Patria", "Rio Claro", "Serra do Mar", 
        "Baia Grande", "Fortaleza do Vale", "Nova Veneza", "Porto Esperanca", "Nova Viena", 
        "Pico Gelado", "Bastiao Sul", "Vila Progresso", "Grand Metropole", "Portal de Atlas"
    };

    private static readonly string[] nomesIA = new string[]
    {
        "Boreal Primus", "Carmesim Central", "Solaris Peak", "Valerian Ridge", "Aurora Station", 
        "Cidadela Carmesim", "Fortaleza Valeriana", "Posto Avancado Boreal", "Base Carmesim 9", "Setor Boreal B", 
        "Solaris V", "Valeriana Primus", "Forte Carmesim", "Estacao Boreal 4", "Bastiao Solaris", 
        "Setor Valeriano Z", "Boreal Secundus", "Carmesim Sul", "Valeriana Nova", "Solaris Alpha", 
        "Base Boreal", "Forte Solaris", "Posto Valeriano 3", "Divisao Carmesim", "Boreal Tertius", 
        "Estacao Solaris 7", "Carmesim Alpha", "Bastiao Valeriano", "Setor Boreal 1", "Solaris Beta", 
        "Base Carmesim 12", "Forte Valeriano 5", "Boreal Norte", "Estacao Carmesim 6", "Bastiao Boreal", 
        "Solaris Delta", "Valeriana Secundus", "Carmesim Leste", "Base Solaris 2", "Forte Carmesim 8", 
        "Boreal Leste", "Estacao Valeriana 1", "Bastiao Carmesim", "Solaris Gamma", "Valeriana Tertius", 
        "Carmesim Oeste", "Base Boreal 5", "Forte Solaris 3", "Boreal Sul", "Estacao Solaris 12", 
        "Bastiao Solaris II", "Valeriana Norte", "Carmesim Norte", "Base Valeriana 8", "Forte Carmesim 3", 
        "Boreal Oeste", "Estacao Carmesim II", "Bastiao Boreal II", "Solaris Sigma", "Valeriana Leste", 
        "Carmesim Nova", "Base Solaris V", "Forte Valeriano X", "Boreal Nova", "Estacao Valeriana IX", 
        "Bastiao Valeriano II", "Solaris Omega", "Valeriana Oeste", "Carmesim Central II", "Base Carmesim III", 
        "Forte Boreal IV", "Boreal Central", "Estacao Boreal VII", "Bastiao Carmesim II", "Solaris Prime", 
        "Valeriana Central", "Carmesim Prime", "Base Valeriana IV", "Forte Solaris IX", "Boreal Prime", 
        "Estacao Solaris IV", "Bastiao Solaris III", "Solaris Nova", "Valeriana Nova II", "Carmesim Nova II", 
        "Base Boreal X", "Forte Carmesim IV", "Boreal Leste II", "Estacao Carmesim X", "Bastiao Boreal III", 
        "Solaris Lux", "Valeriana Lux", "Carmesim Lux", "Base Solaris IX", "Forte Valeriano IV", 
        "Boreal Lux", "Estacao Valeriana IV", "Bastiao Valeriano III", "Solaris Nova III", "Valeriana Apex"
    };

    private static readonly string[] nomesNeutro = new string[]
    {
        "Posto de Troca", "Entreposto", "Terra de Ninguem", "Fronteira Livre", "Oasis Neutro", 
        "Canyon Seco", "Ilha Esquecida", "Valadares", "Colonia Livre", "Porto Franco"
    };

    #endregion

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;

        GerenciadorDivisaoTerritorial existente = FindFirstObjectByType<GerenciadorDivisaoTerritorial>();
        if (existente != null)
        {
            Instancia = existente;
            return;
        }

        GameObject go = new GameObject("GerenciadorDivisaoTerritorial_Runtime");
        Instancia = go.AddComponent<GerenciadorDivisaoTerritorial>();
        DontDestroyOnLoad(go);
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

    private void Update()
    {
        if (Time.unscaledTime >= nextRecalculateTime)
        {
            nextRecalculateTime = Time.unscaledTime + RecalculateInterval;
            RecalcularDados();
            ProcessarFluxoMigratorio();
        }
    }

    /// <summary>
    /// Chamado automaticamente pelo MarcadorTerritorio ao iniciar
    /// </summary>
    public void RegistrarCidade(MarcadorTerritorio marcador)
    {
        if (marcador == null) return;

        string id = marcador.gameObject.GetInstanceID().ToString();

        // Evita duplicados
        if (cidades.Any(c => c.id == id)) return;

        // Determina o nome a partir da equipe
        string nomeEscolhido = ObterNomeDisponivel(marcador.teamID);
        nomesUsados.Add(nomeEscolhido);

        CidadeEstado novaCidade = new CidadeEstado(id, nomeEscolhido, marcador.teamID, marcador.ehPrefeitura, marcador);
        cidades.Add(novaCidade);

        RecalcularCidadeUnica(novaCidade);
        OnDivisaoTerritorialAtualizada?.Invoke();
    }

    /// <summary>
    /// Chamado pelo MarcadorTerritorio ao ser destruído
    /// </summary>
    public void RemoverCidade(MarcadorTerritorio marcador)
    {
        if (marcador == null) return;
        string id = marcador.gameObject.GetInstanceID().ToString();

        CidadeEstado cidade = cidades.FirstOrDefault(c => c.id == id);
        if (cidade != null)
        {
            nomesUsados.Remove(cidade.nome);
            cidades.Remove(cidade);
            OnDivisaoTerritorialAtualizada?.Invoke();
        }
    }

    public void RenomearCidade(string id, string novoNome)
    {
        if (string.IsNullOrWhiteSpace(novoNome)) return;
        CidadeEstado cidade = cidades.FirstOrDefault(c => c.id == id);
        if (cidade != null)
        {
            nomesUsados.Remove(cidade.nome);
            cidade.nome = novoNome.Trim();
            nomesUsados.Add(cidade.nome);
            OnDivisaoTerritorialAtualizada?.Invoke();
        }
    }

    private string ObterNomeDisponivel(int teamID)
    {
        string[] pool;

        if (teamID == 1) // Jogador
        {
            pool = nomesJogador;
            foreach (string nome in pool)
            {
                if (!nomesUsados.Contains(nome))
                {
                    return nome;
                }
            }
            string nomeBase = pool[UnityEngine.Random.Range(0, pool.Length)];
            int sufixo = 2;
            while (nomesUsados.Contains(nomeBase + " " + sufixo))
            {
                sufixo++;
            }
            return nomeBase + " " + sufixo;
        }
        else if (teamID > 1) // IA
        {
            // A IA agora gera nomes dinamicamente parecidos com estados baseados no país
            string nomeGerado = "Estado de " + GeradorNomesBatismo.GerarNome();
            int tentativas = 0;
            while (nomesUsados.Contains(nomeGerado) && tentativas < 20)
            {
                nomeGerado = "Estado de " + GeradorNomesBatismo.GerarNome();
                tentativas++;
            }
            
            if (nomesUsados.Contains(nomeGerado))
            {
                int sufixoIA = 2;
                while (nomesUsados.Contains(nomeGerado + " " + sufixoIA))
                {
                    sufixoIA++;
                }
                nomeGerado = nomeGerado + " " + sufixoIA;
            }
            return nomeGerado;
        }
        else // Neutro
        {
            pool = nomesNeutro;
            foreach (string nome in pool)
            {
                if (!nomesUsados.Contains(nome))
                {
                    return nome;
                }
            }
            string nomeBaseN = pool[UnityEngine.Random.Range(0, pool.Length)];
            int sufixoN = 2;
            while (nomesUsados.Contains(nomeBaseN + " " + sufixoN))
            {
                sufixoN++;
            }
            return nomeBaseN + " " + sufixoN;
        }
    }

    public void RecalcularDados()
    {
        cidades.RemoveAll(c => c.marcador == null);

        EstruturaEconomica[] estruturas = FindObjectsByType<EstruturaEconomica>(FindObjectsSortMode.None);

        foreach (var cidade in cidades)
        {
            CalcularIdentidadeEAtratividade(cidade, estruturas);
        }

        OnDivisaoTerritorialAtualizada?.Invoke();
    }

    private void RecalcularCidadeUnica(CidadeEstado cidade)
    {
        EstruturaEconomica[] estruturas = FindObjectsByType<EstruturaEconomica>(FindObjectsSortMode.None);
        CalcularIdentidadeEAtratividade(cidade, estruturas);
    }

    private void CalcularIdentidadeEAtratividade(CidadeEstado cidade, EstruturaEconomica[] todasEstruturas)
    {
        if (cidade.marcador == null) return;

        Vector3 centro = cidade.marcador.transform.position;
        float raio = cidade.marcador.raioDeDominio;

        cidade.populacaoCivil = 0;
        cidade.capacidadeHabitacional = 0;
        cidade.empregosTotais = 0;
        cidade.vagasDeEmpregoAbertas = 0;
        cidade.temAeroporto = false;
        cidade.temPorto = false;

        cidade.scoreIndustrial = 0f;
        cidade.scoreComercial = 0f;
        cidade.scoreAgricola = 0f;
        cidade.scoreTurismo = 0f;
        cidade.scoreLogistica = 0f;
        cidade.scoreEnergia = 0f;

        foreach (var est in todasEstruturas)
        {
            if (est == null || !est.gameObject.activeInHierarchy) continue;
            
            if (Vector3.Distance(est.transform.position, centro) <= raio)
            {
                cidade.populacaoCivil += est.populacaoAtual;
                cidade.capacidadeHabitacional += est.capacidadePopulacional;
                cidade.empregosTotais += est.empregosGerados;

                if (est.tipo == TipoEstruturaEconomica.Casa || est.tipo == TipoEstruturaEconomica.CasaPopular || est.tipo == TipoEstruturaEconomica.PredioResidencial)
                {
                    // Moradias
                }
                else if (est.tipo == TipoEstruturaEconomica.Industria || est.tipo == TipoEstruturaEconomica.IndustriaLeve || est.tipo == TipoEstruturaEconomica.IndustriaPesada)
                {
                    cidade.scoreIndustrial += est.empregosGerados;
                }
                else if (est.tipo == TipoEstruturaEconomica.Comercio || est.tipo == TipoEstruturaEconomica.ComercioPequeno || est.tipo == TipoEstruturaEconomica.Shopping)
                {
                    cidade.scoreComercial += est.empregosGerados;
                    cidade.scoreTurismo += est.empregosGerados * 0.2f;
                }
                else if (est.tipo == TipoEstruturaEconomica.Farm)
                {
                    cidade.scoreAgricola += est.empregosGerados * 2f;
                }
                else if (est.tipo == TipoEstruturaEconomica.Energia || est.tipo == TipoEstruturaEconomica.UsinaHidreletrica || est.tipo == TipoEstruturaEconomica.UsinaSolar || est.tipo == TipoEstruturaEconomica.UsinaTermicaGrande || est.tipo == TipoEstruturaEconomica.UsinaTermicaPequena || est.tipo == TipoEstruturaEconomica.UsinaNuclear || est.tipo == TipoEstruturaEconomica.Refinaria || est.tipo == TipoEstruturaEconomica.Petroleo)
                {
                    cidade.scoreEnergia += est.energiaProduzida > 0 ? est.energiaProduzida : est.empregosGerados;
                }
                else if (est.tipo == TipoEstruturaEconomica.AeroportoCivil)
                {
                    cidade.temAeroporto = true;
                    cidade.scoreLogistica += 1000f;
                    cidade.scoreTurismo += 500f;
                }
                else if (est.tipo == TipoEstruturaEconomica.PortoComercial)
                {
                    cidade.temPorto = true;
                    cidade.scoreLogistica += 1200f;
                    cidade.scoreIndustrial += 400f;
                }
            }
        }

        cidade.vagasDeEmpregoAbertas = Mathf.Max(0, cidade.empregosTotais - cidade.populacaoCivil);

        // Definir Identidade
        float maxScore = Mathf.Max(cidade.scoreIndustrial, cidade.scoreComercial, cidade.scoreAgricola, cidade.scoreTurismo, cidade.scoreLogistica, cidade.scoreEnergia);
        
        if (maxScore < 50f) cidade.identidadePrincipal = "Região em Desenvolvimento";
        else if (maxScore == cidade.scoreIndustrial) cidade.identidadePrincipal = "Polo Industrial";
        else if (maxScore == cidade.scoreComercial) cidade.identidadePrincipal = "Centro Comercial";
        else if (maxScore == cidade.scoreTurismo) cidade.identidadePrincipal = "Destino Turístico";
        else if (maxScore == cidade.scoreAgricola) cidade.identidadePrincipal = "Polo Agrícola";
        else if (maxScore == cidade.scoreLogistica) cidade.identidadePrincipal = "Hub Logístico";
        else if (maxScore == cidade.scoreEnergia) cidade.identidadePrincipal = "Centro Energético";

        // Calcular Atratividade (0 a 100)
        float baseAtratividade = 20f;
        float bonusEmprego = Mathf.Clamp((cidade.vagasDeEmpregoAbertas / 100f) * 10f, 0f, 40f); // Até 40 pontos por empregos sobrando
        float bonusAero = cidade.temAeroporto ? 15f : 0f;
        float bonusPorto = cidade.temPorto ? 15f : 0f;
        float malusLotacao = (cidade.capacidadeHabitacional > 0 && cidade.populacaoCivil >= cidade.capacidadeHabitacional) ? -30f : 0f; // Fica menos atrativo se não tem casa
        
        cidade.atratividade = Mathf.Clamp(baseAtratividade + bonusEmprego + bonusAero + bonusPorto + malusLotacao, 0f, 100f);
    }

    private void ProcessarFluxoMigratorio()
    {
        // Migração ocorre movendo "pontos de população" das cidades menos atrativas para as mais atrativas.
        // A lógica de imigração do Imovel.cs agora dependerá dessa atratividade.
        // O Gerenciador de Recursos global (populacaoAtual) é distribuído.
    }

    public float ObterAtratividadeLocal(Vector3 posicao)
    {
        float atratividadeMedia = 50f; 
        foreach(var cid in cidades)
        {
            if (cid.marcador != null && Vector3.Distance(posicao, cid.marcador.transform.position) <= cid.marcador.raioDeDominio)
            {
                return cid.atratividade;
            }
        }
        return atratividadeMedia;
    }

    public float ObterEficienciaMaoDeObraLocal(Vector3 posicao)
    {
        foreach (var cid in cidades)
        {
            if (cid.marcador != null && Vector3.Distance(posicao, cid.marcador.transform.position) <= cid.marcador.raioDeDominio)
            {
                if (cid.empregosTotais == 0) return 1f;
                // Garante no mínimo 50% de eficiência mesmo sem população para que as primeiras construções funcionem
                if (cid.populacaoCivil == 0) return 0.5f; 
                
                // Se tem mais empregos do que população, a eficiência cai proporcionalmente
                float prop = (float)cid.populacaoCivil / cid.empregosTotais;
                return Mathf.Clamp(prop, 0.1f, 1f); // Garante no mínimo 10%
            }
        }
        return 1f;
    }

    public List<CidadeEstado> ObterCidadesComAeroporto(int teamID)
    {
        return cidades.Where(c => c.teamID == teamID && c.temAeroporto).ToList();
    }
}
