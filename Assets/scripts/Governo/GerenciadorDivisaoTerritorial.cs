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
    public bool temAeroporto;
    public MarcadorTerritorio marcador;

    public CidadeEstado(string id, string nome, int teamID, bool ehEstado, MarcadorTerritorio marcador)
    {
        this.id = id;
        this.nome = nome;
        this.teamID = teamID;
        this.ehEstado = ehEstado;
        this.marcador = marcador;
        this.populacaoCivil = 0;
        this.temAeroporto = false;
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
        string prefix = "";

        if (teamID == 1) // Jogador
        {
            pool = nomesJogador;
        }
        else if (teamID > 1) // IA
        {
            pool = nomesIA;
        }
        else // Neutro
        {
            pool = nomesNeutro;
        }

        // Tenta achar um nome que ainda não foi usado
        foreach (string nome in pool)
        {
            if (!nomesUsados.Contains(nome))
            {
                return nome;
            }
        }

        // Se todos os nomes estiverem usados, pega um aleatório e coloca um sufixo numérico
        string nomeBase = pool[UnityEngine.Random.Range(0, pool.Length)];
        int sufixo = 2;
        while (nomesUsados.Contains(nomeBase + " " + sufixo))
        {
            sufixo++;
        }
        return nomeBase + " " + sufixo;
    }

    public void RecalcularDados()
    {
        // Limpa referências nulas de marcadores que possam ter sido destruídos sem notificar
        cidades.RemoveAll(c => c.marcador == null);

        // Acha todas as casas e aeroportos uma vez para distribuir
        Imovel[] imoveis = FindObjectsByType<Imovel>(FindObjectsSortMode.None);
        GerenciadorAeroporto[] aeroportos = FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);

        foreach (var cidade in cidades)
        {
            if (cidade.marcador == null) continue;

            Vector3 centro = cidade.marcador.transform.position;
            float raio = cidade.marcador.raioDeDominio;

            // Recalcula população civil com base nas moradias dentro do limite
            int pop = 0;
            foreach (var imovel in imoveis)
            {
                if (imovel != null && Vector3.Distance(imovel.transform.position, centro) <= raio)
                {
                    pop += imovel.MoradoresAtuais;
                }
            }
            cidade.populacaoCivil = pop;

            // Verifica se possui aeroporto
            bool temAero = false;
            foreach (var aero in aeroportos)
            {
                if (aero != null && Vector3.Distance(aero.transform.position, centro) <= raio)
                {
                    temAero = true;
                    break;
                }
            }
            cidade.temAeroporto = temAero;
        }

        OnDivisaoTerritorialAtualizada?.Invoke();
    }

    private void RecalcularCidadeUnica(CidadeEstado cidade)
    {
        if (cidade.marcador == null) return;

        Vector3 centro = cidade.marcador.transform.position;
        float raio = cidade.marcador.raioDeDominio;

        Imovel[] imoveis = FindObjectsByType<Imovel>(FindObjectsSortMode.None);
        int pop = 0;
        foreach (var imovel in imoveis)
        {
            if (imovel != null && Vector3.Distance(imovel.transform.position, centro) <= raio)
            {
                pop += imovel.MoradoresAtuais;
            }
        }
        cidade.populacaoCivil = pop;

        GerenciadorAeroporto[] aeroportos = FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        bool temAero = false;
        foreach (var aero in aeroportos)
        {
            if (aero != null && Vector3.Distance(aero.transform.position, centro) <= raio)
            {
                temAero = true;
                break;
            }
        }
        cidade.temAeroporto = temAero;
    }

    /// <summary>
    /// API pública para o sistema de Aeroportos e Voos consultar quais cidades possuem infraestrutura
    /// </summary>
    public List<CidadeEstado> ObterCidadesComAeroporto(int teamID)
    {
        return cidades.Where(c => c.teamID == teamID && c.temAeroporto).ToList();
    }
}
