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

    [Header("Cidade abstrata")]
    public bool ehCidadeNaoMaterializada;
    public string nomePais = string.Empty;
    public Vector3 localizacao;
    [Range(0f, 100f)] public float desenvolvimento = 25f;
    [Range(0f, 100f)] public float importanciaEstrategica;
    [Range(0f, 100f)] public float estadoDano;
    public float producaoEconomicaMensal;
    public float producaoAgricola;
    public float taxaCrescimentoAnual = 0.015f;
    public string prefabCidade = "CidadeModerna";
    public int populacaoEstagio2 = 500000;
    public int populacaoEstagio3 = 1000000;
    public int populacaoEstagio4 = 2000000;

    // Referência somente de runtime. Cidades materializadas persistem pelo
    // SistemaSaveGame da própria entidade; o diretório nacional é reconstruído
    // quando CidadeComplexoUrbano inicia.
    [NonSerialized] public CidadeComplexoUrbano cidadeUrbana;

    public int EstagioUrbano => populacaoCivil >= Mathf.Max(populacaoEstagio3, populacaoEstagio4) ? 4
        : populacaoCivil >= Mathf.Max(1, populacaoEstagio3) ? 3
        : populacaoCivil >= Mathf.Max(1, populacaoEstagio2) ? 2
        : populacaoCivil > 0 ? 1 : 0;

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
    private readonly List<CidadeComplexoUrbano> bufferCidadesMigracao = new List<CidadeComplexoUrbano>(16);
    private readonly List<CidadeEstado> bufferRegioesMigracao = new List<CidadeEstado>(16);
    private readonly List<CidadeEstado> bufferCidadesAbstratas = new List<CidadeEstado>(16);
    private readonly List<int> bufferParcelasPopulacionais = new List<int>(16);
    private float nextRecalculateTime = 0f;
    private const float RecalculateInterval = 2.0f; // Recalcula a cada 2 segundos
    private const float DiferencaMinimaAtratividadeMigracao = 10f;
    private const float TaxaMigracaoPorCiclo = 0.00002f;
    private const float RaioConsultaCidadeAbstrata = 10000f;
    [SerializeField, Min(1)] private int populacaoMinimaMaterializacao = 500000;
    [SerializeField, Min(0f)] private float producaoMinimaMaterializacao = 250000f;
    [SerializeField, Range(0f, 100f)] private float desenvolvimentoMinimoMaterializacao = 70f;
    [SerializeField, Range(0f, 100f)] private float importanciaMinimaMaterializacao = 85f;

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
        if (Hegemonia.RTS.RTSSimulationClock.SimulacaoPausada) return;
        if (Time.unscaledTime >= nextRecalculateTime)
        {
            nextRecalculateTime = Time.unscaledTime + RecalculateInterval;
            ProcessarFluxoMigratorio();
            // A migração usa a atratividade calculada no ciclo anterior; a
            // nova população passa a aparecer na região neste recálculo.
            RecalcularDados();
            MaterializarCidadesQualificadas();
        }
    }

    /// <summary>
    /// Chamado automaticamente pelo MarcadorTerritorio ao iniciar
    /// </summary>
    public void RegistrarCidade(MarcadorTerritorio marcador, string nomePreferencial = null)
    {
        if (marcador == null || marcador.construirCidadeAoIniciar) return;

        string id = marcador.gameObject.GetInstanceID().ToString();
        CidadeComplexoUrbano cidadeVisual = marcador.GetComponent<CidadeComplexoUrbano>();
        CidadeEstado existente = cidades.FirstOrDefault(c => c != null && c.id == id);

        // Evita duplicados
        if (existente != null)
        {
            existente.marcador = marcador;
            existente.ehEstado = marcador.ehPrefeitura;
            if (cidadeVisual != null)
            {
                existente.cidadeUrbana = cidadeVisual;
                if (existente.nome != cidadeVisual.nomeCidade)
                {
                    nomesUsados.Remove(existente.nome);
                    existente.nome = cidadeVisual.nomeCidade;
                    nomesUsados.Add(existente.nome);
                }
                RecalcularCidadeUnica(existente);
            }
            else
            {
                RegistrarCidadeAbstrataDaPrefeitura(marcador, existente.nome);
            }
            OnDivisaoTerritorialAtualizada?.Invoke();
            return;
        }

        // Determina o nome a partir da equipe
        string nomeDaCidadeVisual = cidadeVisual != null ? cidadeVisual.nomeCidade : null;
        string nomeEscolhido = string.IsNullOrWhiteSpace(nomePreferencial)
            ? (string.IsNullOrWhiteSpace(nomeDaCidadeVisual)
                ? ObterNomeDisponivel(marcador.teamID)
                : nomeDaCidadeVisual.Trim())
            : nomePreferencial.Trim();
        if (string.IsNullOrWhiteSpace(nomeEscolhido))
            nomeEscolhido = ObterNomeDisponivel(marcador.teamID);
        if (nomesUsados.Contains(nomeEscolhido))
            nomeEscolhido = ObterNomeDisponivel(marcador.teamID);

        nomesUsados.Add(nomeEscolhido);

        CidadeEstado novaCidade = new CidadeEstado(id, nomeEscolhido, marcador.teamID, marcador.ehPrefeitura, marcador)
        {
            cidadeUrbana = cidadeVisual
        };
        cidades.Add(novaCidade);

        if (cidadeVisual != null)
            AtualizarDadosCidadeUrbana(novaCidade, cidadeVisual);
        else
        {
            RecalcularCidadeUnica(novaCidade);
            RegistrarCidadeAbstrataDaPrefeitura(marcador, novaCidade.nome);
        }
        OnDivisaoTerritorialAtualizada?.Invoke();
    }

    private void RegistrarCidadeAbstrataDaPrefeitura(MarcadorTerritorio marcador, string nomeCidade)
    {
        if (marcador == null || !marcador.ehPrefeitura
            || marcador.GetComponent<CidadeComplexoUrbano>() != null)
            return;

        // Uma cidade que já foi materializada ocupa a mesma posição da cidade
        // abstrata original. Ao restaurar, MarcadorTerritorio pode registrar
        // antes do visual urbano; não recrie os dados abstratos nesse caso.
        Vector3 posicao = marcador.transform.position;
        CidadeEstado cidadeMaterializada = cidades.FirstOrDefault(c => c != null
            && c.cidadeUrbana != null
            && c.teamID == marcador.teamID
            && (c.cidadeUrbana.transform.position - posicao).sqrMagnitude <= 1f);
        if (cidadeMaterializada != null) return;

        SaveableEntity entidade = SaveableEntity.Garantir(marcador.gameObject);
        string idPersistente = "cidade-capital-" + entidade.UniqueId;
        CidadeEstado cidade = RegistrarCidadeAbstrataInterno(
            nomeCidade,
            marcador.teamID,
            posicao,
            100000,
            2000000,
            idPersistente,
            notificarAtualizacao: false);
        if (cidade == null) return;

        // O id estável da cidade abstrata é salvo pelo DTO existente. Ele
        // religa os dados ao marcador sem alterar o formato do save.
        cidade.localizacao = posicao;
        cidade.teamID = marcador.teamID;
        IdentidadeUnidade identidade = marcador.GetComponent<IdentidadeUnidade>();
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(marcador.teamID)
            : null;
        cidade.nomePais = identidade != null && !string.IsNullOrWhiteSpace(identidade.nomeDoPais)
            ? identidade.nomeDoPais
            : pais != null ? pais.nomePais : cidade.nomePais;
    }

    /// <summary>
    /// Inclui uma cidade Nova Aurora materializada no mesmo diretório usado
    /// pela aba Cidades do Governo, sem transformá-la em reivindicação
    /// territorial. Se o objeto também possuir MarcadorTerritorio, ambos os
    /// caminhos compartilham o mesmo registro pelo instance ID.
    /// </summary>
    public CidadeEstado RegistrarCidadeUrbana(CidadeComplexoUrbano cidadeVisual)
    {
        if (cidadeVisual == null || !cidadeVisual.gameObject.scene.IsValid()) return null;

        string id = cidadeVisual.gameObject.GetInstanceID().ToString();
        CidadeEstado registro = cidades.FirstOrDefault(c => c != null && c.id == id);
        if (registro == null)
        {
            string nome = string.IsNullOrWhiteSpace(cidadeVisual.nomeCidade)
                ? cidadeVisual.gameObject.name
                : cidadeVisual.nomeCidade.Trim();
            if (string.IsNullOrWhiteSpace(nome)) nome = ObterNomeDisponivel(cidadeVisual.teamId);

            string nomeUnico = nome;
            int sufixo = 2;
            while (nomesUsados.Contains(nomeUnico))
                nomeUnico = nome + " " + sufixo++;
            if (nomeUnico != cidadeVisual.nomeCidade)
                cidadeVisual.nomeCidade = nomeUnico;

            registro = new CidadeEstado(
                id,
                nomeUnico,
                cidadeVisual.teamId,
                false,
                cidadeVisual.GetComponent<MarcadorTerritorio>());
            cidades.Add(registro);
            nomesUsados.Add(registro.nome);
        }
        else
        {
            nomesUsados.Remove(registro.nome);
            string nomeDesejado = string.IsNullOrWhiteSpace(cidadeVisual.nomeCidade)
                ? cidadeVisual.gameObject.name
                : cidadeVisual.nomeCidade.Trim();
            string nomeUnico = nomeDesejado;
            int sufixo = 2;
            while (nomesUsados.Contains(nomeUnico))
                nomeUnico = nomeDesejado + " " + sufixo++;
            cidadeVisual.nomeCidade = nomeUnico;
            registro.nome = nomeUnico;
            nomesUsados.Add(nomeUnico);
        }

        registro.cidadeUrbana = cidadeVisual;
        if (registro.marcador == null)
            AtualizarDadosCidadeUrbana(registro, cidadeVisual);
        else
            RecalcularCidadeUnica(registro);

        OnDivisaoTerritorialAtualizada?.Invoke();
        return registro;
    }

    public void RemoverCidadeUrbana(CidadeComplexoUrbano cidadeVisual)
    {
        if (cidadeVisual == null) return;

        string id = cidadeVisual.gameObject.GetInstanceID().ToString();
        CidadeEstado registro = cidades.FirstOrDefault(c => c != null && c.id == id);
        if (registro == null) return;

        registro.cidadeUrbana = null;
        if (registro.marcador == null && !registro.ehCidadeNaoMaterializada)
        {
            nomesUsados.Remove(registro.nome);
            cidades.Remove(registro);
        }
        OnDivisaoTerritorialAtualizada?.Invoke();
    }

    private static void AtualizarDadosCidadeUrbana(CidadeEstado registro, CidadeComplexoUrbano cidadeVisual)
    {
        if (registro == null || cidadeVisual == null) return;

        registro.cidadeUrbana = cidadeVisual;
        registro.teamID = cidadeVisual.teamId;
        registro.nome = string.IsNullOrWhiteSpace(cidadeVisual.nomeCidade)
            ? cidadeVisual.gameObject.name
            : cidadeVisual.nomeCidade.Trim();
        registro.localizacao = cidadeVisual.transform.position;
        registro.populacaoCivil = Mathf.Max(0, cidadeVisual.populacaoResidente);
        registro.capacidadeHabitacional = Mathf.Max(0, cidadeVisual.capacidadeHabitacional);
        registro.empregosTotais = Mathf.Max(0, cidadeVisual.CapacidadeComercial);
        registro.vagasDeEmpregoAbertas = Mathf.Max(0, cidadeVisual.VagasComerciaisLivres);
        registro.desenvolvimento = Mathf.Clamp(cidadeVisual.qualidadeVida, 0f, 100f);
        registro.atratividade = Mathf.Clamp(cidadeVisual.qualidadeVida, 0f, 100f);
        registro.scoreComercial = cidadeVisual.CapacidadeComercial;

        IdentidadeUnidade identidade = cidadeVisual.GetComponent<IdentidadeUnidade>()
            ?? cidadeVisual.GetComponentInParent<IdentidadeUnidade>();
        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(cidadeVisual.teamId)
            : null;
        registro.nomePais = identidade != null && !string.IsNullOrWhiteSpace(identidade.nomeDoPais)
            ? identidade.nomeDoPais
            : pais != null ? pais.nomePais : string.Empty;
    }

    /// <summary>
    /// Registra uma cidade sistêmica sem criar objetos visuais. A mesma lista
    /// que alimenta a aba Cidades do Governo mantém seus dados até a cidade
    /// atingir o limiar de materialização.
    /// </summary>
    public CidadeEstado RegistrarCidadeAbstrata(
        string nome,
        int teamId,
        Vector3 posicao,
        int populacaoInicial = 100000,
        int capacidadeHabitacionalInicial = 2000000)
    {
        return RegistrarCidadeAbstrataInterno(
            nome,
            teamId,
            posicao,
            populacaoInicial,
            capacidadeHabitacionalInicial,
            idPersistente: null,
            notificarAtualizacao: true);
    }

    private CidadeEstado RegistrarCidadeAbstrataInterno(
        string nome,
        int teamId,
        Vector3 posicao,
        int populacaoInicial,
        int capacidadeHabitacionalInicial,
        string idPersistente,
        bool notificarAtualizacao)
    {
        if (teamId < 0 || string.IsNullOrWhiteSpace(nome)) return null;

        string nomeNormalizado = nome.Trim();
        CidadeEstado existente = string.IsNullOrWhiteSpace(idPersistente)
            ? null
            : cidades.FirstOrDefault(c => c != null && c.ehCidadeNaoMaterializada
                && string.Equals(c.id, idPersistente, StringComparison.Ordinal));
        if (existente == null && string.IsNullOrWhiteSpace(idPersistente))
        {
            existente = cidades.FirstOrDefault(c => c != null
                && c.ehCidadeNaoMaterializada
                && c.teamID == teamId
                && string.Equals(c.nome, nomeNormalizado, StringComparison.OrdinalIgnoreCase));
        }
        if (existente != null) return existente;

        CidadeEstado nova = new CidadeEstado(
            string.IsNullOrWhiteSpace(idPersistente) ? "cidade-dados-" + Guid.NewGuid().ToString("N") : idPersistente,
            nomeNormalizado,
            teamId,
            false,
            null)
        {
            ehCidadeNaoMaterializada = true,
            localizacao = posicao,
            populacaoCivil = Mathf.Clamp(populacaoInicial, 0, Mathf.Max(0, capacidadeHabitacionalInicial)),
            capacidadeHabitacional = Mathf.Max(0, capacidadeHabitacionalInicial),
            empregosTotais = Mathf.Max(0, Mathf.RoundToInt(capacidadeHabitacionalInicial * 0.35f)),
            desenvolvimento = Mathf.Clamp(populacaoInicial / 10000f, 0f, 100f),
            importanciaEstrategica = 10f,
            producaoEconomicaMensal = Mathf.Max(0, populacaoInicial) * 0.01f,
            atratividade = 50f
        };

        cidades.Add(nova);
        nomesUsados.Add(nova.nome);
        AtualizarResumoCidadeAbstrata(nova);
        if (notificarAtualizacao) OnDivisaoTerritorialAtualizada?.Invoke();
        return nova;
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
            cidade.marcador = null;
            if (cidade.cidadeUrbana == null && !cidade.ehCidadeNaoMaterializada)
            {
                nomesUsados.Remove(cidade.nome);
                cidades.Remove(cidade);
            }
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
            if (cidade.cidadeUrbana != null)
                cidade.cidadeUrbana.nomeCidade = cidade.nome;
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
        cidades.RemoveAll(c => c == null
            || (c.marcador == null && !c.ehCidadeNaoMaterializada && c.cidadeUrbana == null));

        EstruturaEconomica[] estruturas = FindObjectsByType<EstruturaEconomica>(FindObjectsSortMode.None);

        foreach (var cidade in cidades)
        {
            if (cidade.ehCidadeNaoMaterializada)
                AtualizarResumoCidadeAbstrata(cidade);
            else if (cidade.marcador != null)
                CalcularIdentidadeEAtratividade(cidade, estruturas);
            else if (cidade.cidadeUrbana != null)
                AtualizarDadosCidadeUrbana(cidade, cidade.cidadeUrbana);
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
                else if (est.tipo == TipoEstruturaEconomica.LegacyReservedAgriculture)
                {
                    cidade.scoreAgricola += est.empregosGerados * 2f;
                }
                else if (est.tipo == TipoEstruturaEconomica.Energia || est.tipo == TipoEstruturaEconomica.UsinaHidreletrica || est.tipo == TipoEstruturaEconomica.UsinaSolar || est.tipo == TipoEstruturaEconomica.UsinaCarvao || est.tipo == TipoEstruturaEconomica.UsinaTermicaGrande || est.tipo == TipoEstruturaEconomica.UsinaTermicaPequena || est.tipo == TipoEstruturaEconomica.UsinaNuclear || est.tipo == TipoEstruturaEconomica.Refinaria || est.tipo == TipoEstruturaEconomica.Petroleo)
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
        if (Time.timeScale <= 0f) return;
        if (ProcessarFluxoMigratorioAbstrato()) return;

        CidadeComplexoUrbano.CopiarCidadesAtivas(bufferCidadesMigracao);
        if (bufferCidadesMigracao.Count < 2 || cidades.Count < 2) return;

        bufferRegioesMigracao.Clear();
        for (int i = 0; i < bufferCidadesMigracao.Count; i++)
            bufferRegioesMigracao.Add(ObterRegiaoDaCidade(bufferCidadesMigracao[i]));

        CidadeComplexoUrbano melhorOrigem = null;
        CidadeComplexoUrbano melhorDestino = null;
        float maiorDiferenca = DiferencaMinimaAtratividadeMigracao;

        for (int i = 0; i < bufferCidadesMigracao.Count; i++)
        {
            CidadeComplexoUrbano origem = bufferCidadesMigracao[i];
            if (origem == null || origem.populacaoResidente <= 0) continue;

            CidadeEstado regiaoOrigem = bufferRegioesMigracao[i];
            if (regiaoOrigem == null) continue;

            for (int j = 0; j < bufferCidadesMigracao.Count; j++)
            {
                CidadeComplexoUrbano destino = bufferCidadesMigracao[j];
                if (destino == null || destino == origem || destino.teamId != origem.teamId
                    || destino.VagasHabitacionaisLivres <= 0)
                    continue;

                CidadeEstado regiaoDestino = bufferRegioesMigracao[j];
                if (regiaoDestino == null || ReferenceEquals(regiaoOrigem, regiaoDestino)) continue;

                float diferenca = regiaoDestino.atratividade - regiaoOrigem.atratividade;
                if (diferenca > maiorDiferenca)
                {
                    maiorDiferenca = diferenca;
                    melhorOrigem = origem;
                    melhorDestino = destino;
                }
            }
        }

        if (melhorOrigem == null || melhorDestino == null) return;

        int quantidade = Mathf.RoundToInt(
            melhorOrigem.populacaoResidente * TaxaMigracaoPorCiclo
            * Mathf.Clamp01(maiorDiferenca / 100f) * Time.timeScale);
        if (quantidade <= 0) return;

        melhorOrigem.TransferirPopulacaoPara(melhorDestino, quantidade);
    }

    private bool ProcessarFluxoMigratorioAbstrato()
    {
        CopiarCidadesAbstratas(0, bufferCidadesAbstratas);
        if (bufferCidadesAbstratas.Count < 2) return false;

        CidadeEstado origemEscolhida = null;
        CidadeEstado destinoEscolhido = null;
        float maiorDiferenca = DiferencaMinimaAtratividadeMigracao;
        for (int i = 0; i < bufferCidadesAbstratas.Count; i++)
        {
            CidadeEstado origem = bufferCidadesAbstratas[i];
            if (origem.populacaoCivil <= 0) continue;

            for (int j = 0; j < bufferCidadesAbstratas.Count; j++)
            {
                CidadeEstado destino = bufferCidadesAbstratas[j];
                if (destino == origem || destino.teamID != origem.teamID
                    || destino.populacaoCivil >= destino.capacidadeHabitacional)
                    continue;

                float diferenca = destino.atratividade - origem.atratividade;
                if (diferenca <= maiorDiferenca) continue;
                maiorDiferenca = diferenca;
                origemEscolhida = origem;
                destinoEscolhido = destino;
            }
        }

        if (origemEscolhida == null || destinoEscolhido == null) return false;
        int solicitada = Mathf.RoundToInt(origemEscolhida.populacaoCivil * TaxaMigracaoPorCiclo
            * Mathf.Clamp01(maiorDiferenca / 100f) * Time.timeScale);
        int transferida = Mathf.Min(solicitada, Mathf.Min(origemEscolhida.populacaoCivil,
            destinoEscolhido.capacidadeHabitacional - destinoEscolhido.populacaoCivil));
        if (transferida <= 0) return false;

        origemEscolhida.populacaoCivil -= transferida;
        destinoEscolhido.populacaoCivil += transferida;
        AtualizarResumoCidadeAbstrata(origemEscolhida);
        AtualizarResumoCidadeAbstrata(destinoEscolhido);
        OnDivisaoTerritorialAtualizada?.Invoke();
        return true;
    }

    private static void AtualizarResumoCidadeAbstrata(CidadeEstado cidade)
    {
        if (cidade == null || !cidade.ehCidadeNaoMaterializada) return;

        cidade.populacaoCivil = Mathf.Clamp(cidade.populacaoCivil, 0, Mathf.Max(0, cidade.capacidadeHabitacional));
        cidade.empregosTotais = Mathf.Max(0, cidade.empregosTotais);
        cidade.vagasDeEmpregoAbertas = Mathf.Max(0, cidade.empregosTotais - cidade.populacaoCivil);
        cidade.atratividade = Mathf.Clamp(
            25f
            + cidade.desenvolvimento * 0.30f
            + Mathf.Clamp(cidade.vagasDeEmpregoAbertas / 10000f, 0f, 20f)
            + Mathf.Clamp(cidade.importanciaEstrategica * 0.10f, 0f, 10f)
            - cidade.estadoDano * 0.45f
            - (cidade.capacidadeHabitacional > 0
                ? Mathf.Clamp01((float)cidade.populacaoCivil / cidade.capacidadeHabitacional) * 15f
                : 30f),
            0f,
            100f);
        cidade.identidadePrincipal = !string.IsNullOrWhiteSpace(cidade.identidadePrincipal)
            ? cidade.identidadePrincipal
            : "Cidade em Desenvolvimento";
    }

    public void CopiarCidadesAbstratas(int teamId, List<CidadeEstado> destino)
    {
        if (destino == null) return;
        destino.Clear();
        for (int i = 0; i < cidades.Count; i++)
        {
            CidadeEstado cidade = cidades[i];
            if (cidade != null && cidade.ehCidadeNaoMaterializada
                && (teamId <= 0 || cidade.teamID == teamId))
                destino.Add(cidade);
        }
        destino.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
    }

    public void ObterTotaisCidadesAbstratas(int teamId, out long capacidade, out long populacao)
    {
        capacidade = 0;
        populacao = 0;
        for (int i = 0; i < cidades.Count; i++)
        {
            CidadeEstado cidade = cidades[i];
            if (cidade == null || !cidade.ehCidadeNaoMaterializada || cidade.teamID != teamId) continue;
            cidade.capacidadeHabitacional = Mathf.Max(0, cidade.capacidadeHabitacional);
            cidade.populacaoCivil = Mathf.Clamp(cidade.populacaoCivil, 0, cidade.capacidadeHabitacional);
            capacidade += cidade.capacidadeHabitacional;
            populacao += cidade.populacaoCivil;
        }
    }

    public void AplicarDeltaPopulacionalAbstrato(int teamId, long delta)
    {
        CopiarCidadesAbstratas(teamId, bufferCidadesAbstratas);
        if (bufferCidadesAbstratas.Count == 0 || delta == 0) return;

        bool adicionar = delta > 0;
        long quantidade = Math.Abs(delta);
        long pesoTotal = 0;
        for (int i = 0; i < bufferCidadesAbstratas.Count; i++)
        {
            CidadeEstado cidade = bufferCidadesAbstratas[i];
            pesoTotal += adicionar
                ? Mathf.Max(0, cidade.capacidadeHabitacional - cidade.populacaoCivil)
                : cidade.populacaoCivil;
        }
        if (pesoTotal <= 0) return;

        long distribuidos = 0;
        bufferParcelasPopulacionais.Clear();
        for (int i = 0; i < bufferCidadesAbstratas.Count; i++)
        {
            CidadeEstado cidade = bufferCidadesAbstratas[i];
            int peso = adicionar
                ? Mathf.Max(0, cidade.capacidadeHabitacional - cidade.populacaoCivil)
                : cidade.populacaoCivil;
            int parcela = (int)Math.Min(peso, quantidade * peso / pesoTotal);
            bufferParcelasPopulacionais.Add(parcela);
            distribuidos += parcela;
        }

        long restante = quantidade - distribuidos;
        for (int i = 0; i < bufferCidadesAbstratas.Count && restante > 0; i++)
        {
            CidadeEstado cidade = bufferCidadesAbstratas[i];
            int peso = adicionar
                ? Mathf.Max(0, cidade.capacidadeHabitacional - cidade.populacaoCivil)
                : cidade.populacaoCivil;
            if (bufferParcelasPopulacionais[i] >= peso) continue;
            bufferParcelasPopulacionais[i]++;
            restante--;
        }

        for (int i = 0; i < bufferCidadesAbstratas.Count; i++)
        {
            CidadeEstado cidade = bufferCidadesAbstratas[i];
            cidade.populacaoCivil = adicionar
                ? cidade.populacaoCivil + bufferParcelasPopulacionais[i]
                : cidade.populacaoCivil - bufferParcelasPopulacionais[i];
            AtualizarResumoCidadeAbstrata(cidade);
        }
    }

    public List<SaveCidadeAbstrataData> CapturarCidadesAbstratas()
    {
        List<SaveCidadeAbstrataData> resultado = new List<SaveCidadeAbstrataData>();
        for (int i = 0; i < cidades.Count; i++)
        {
            CidadeEstado cidade = cidades[i];
            if (cidade == null || !cidade.ehCidadeNaoMaterializada) continue;
            resultado.Add(new SaveCidadeAbstrataData
            {
                id = cidade.id,
                nome = cidade.nome,
                teamID = cidade.teamID,
                nomePais = cidade.nomePais,
                localizacao = new SaveVector3(cidade.localizacao),
                populacaoCivil = cidade.populacaoCivil,
                capacidadeHabitacional = cidade.capacidadeHabitacional,
                empregosTotais = cidade.empregosTotais,
                desenvolvimento = cidade.desenvolvimento,
                importanciaEstrategica = cidade.importanciaEstrategica,
                estadoDano = cidade.estadoDano,
                producaoEconomicaMensal = cidade.producaoEconomicaMensal,
                producaoAgricola = cidade.producaoAgricola,
                taxaCrescimentoAnual = cidade.taxaCrescimentoAnual,
                populacaoEstagio2 = cidade.populacaoEstagio2,
                populacaoEstagio3 = cidade.populacaoEstagio3,
                populacaoEstagio4 = cidade.populacaoEstagio4,
                scoreIndustrial = cidade.scoreIndustrial,
                scoreComercial = cidade.scoreComercial,
                scoreAgricola = cidade.scoreAgricola,
                scoreTurismo = cidade.scoreTurismo,
                scoreLogistica = cidade.scoreLogistica,
                scoreEnergia = cidade.scoreEnergia,
                identidadePrincipal = cidade.identidadePrincipal,
                atratividade = cidade.atratividade,
                temAeroporto = cidade.temAeroporto,
                temPorto = cidade.temPorto,
                prefabCidade = cidade.prefabCidade
            });
        }
        return resultado;
    }

    public void RestaurarCidadesAbstratas(IList<SaveCidadeAbstrataData> salvas)
    {
        if (salvas == null) return;

        for (int i = cidades.Count - 1; i >= 0; i--)
        {
            CidadeEstado existente = cidades[i];
            if (existente != null && existente.ehCidadeNaoMaterializada)
            {
                nomesUsados.Remove(existente.nome);
                cidades.RemoveAt(i);
            }
        }

        HashSet<string> idsRestaurados = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < salvas.Count; i++)
        {
            SaveCidadeAbstrataData salva = salvas[i];
            if (salva == null || string.IsNullOrWhiteSpace(salva.nome) || salva.teamID < 0) continue;
            string id = string.IsNullOrWhiteSpace(salva.id) ? "cidade-dados-" + Guid.NewGuid().ToString("N") : salva.id;
            if (!idsRestaurados.Add(id)) continue;

            CidadeEstado restaurada = new CidadeEstado(id, salva.nome.Trim(), salva.teamID, false, null)
            {
                ehCidadeNaoMaterializada = true,
                nomePais = salva.nomePais ?? string.Empty,
                localizacao = salva.localizacao.ToVector3(),
                populacaoCivil = Mathf.Max(0, salva.populacaoCivil),
                capacidadeHabitacional = Mathf.Max(0, salva.capacidadeHabitacional),
                empregosTotais = Mathf.Max(0, salva.empregosTotais),
                desenvolvimento = Mathf.Clamp(salva.desenvolvimento, 0f, 100f),
                importanciaEstrategica = Mathf.Clamp(salva.importanciaEstrategica, 0f, 100f),
                estadoDano = Mathf.Clamp(salva.estadoDano, 0f, 100f),
                producaoEconomicaMensal = Mathf.Max(0f, salva.producaoEconomicaMensal),
                producaoAgricola = Mathf.Max(0f, salva.producaoAgricola),
                taxaCrescimentoAnual = Mathf.Clamp(salva.taxaCrescimentoAnual, -1f, 1f),
                populacaoEstagio2 = Mathf.Max(1, salva.populacaoEstagio2),
                populacaoEstagio3 = Mathf.Max(1, salva.populacaoEstagio3),
                populacaoEstagio4 = Mathf.Max(1, salva.populacaoEstagio4),
                scoreIndustrial = Mathf.Max(0f, salva.scoreIndustrial),
                scoreComercial = Mathf.Max(0f, salva.scoreComercial),
                scoreAgricola = Mathf.Max(0f, salva.scoreAgricola),
                scoreTurismo = Mathf.Max(0f, salva.scoreTurismo),
                scoreLogistica = Mathf.Max(0f, salva.scoreLogistica),
                scoreEnergia = Mathf.Max(0f, salva.scoreEnergia),
                identidadePrincipal = salva.identidadePrincipal ?? "Cidade em Desenvolvimento",
                atratividade = Mathf.Clamp(salva.atratividade, 0f, 100f),
                temAeroporto = salva.temAeroporto,
                temPorto = salva.temPorto,
                prefabCidade = string.IsNullOrWhiteSpace(salva.prefabCidade) ? "CidadeModerna" : salva.prefabCidade
            };
            restaurada.populacaoCivil = Mathf.Clamp(restaurada.populacaoCivil, 0, restaurada.capacidadeHabitacional);
            cidades.Add(restaurada);
            nomesUsados.Add(restaurada.nome);
            AtualizarResumoCidadeAbstrata(restaurada);
        }

        OnDivisaoTerritorialAtualizada?.Invoke();
    }

    public bool TentarMaterializarCidadeAbstrata(string id)
    {
        CidadeEstado cidade = cidades.FirstOrDefault(c => c != null
            && c.ehCidadeNaoMaterializada
            && string.Equals(c.id, id, StringComparison.Ordinal));
        if (cidade == null) return false;

        bool atingiuLimiar = cidade.populacaoCivil >= populacaoMinimaMaterializacao
            || cidade.desenvolvimento >= desenvolvimentoMinimoMaterializacao
            || cidade.importanciaEstrategica >= importanciaMinimaMaterializacao
            || cidade.producaoEconomicaMensal >= producaoMinimaMaterializacao;
        if (!atingiuLimiar) return false;

        string chave = string.IsNullOrWhiteSpace(cidade.prefabCidade) ? "CidadeModerna" : cidade.prefabCidade.Trim();
        DadosConstrucao ficha = Resources.Load<DadosConstrucao>("Construcoes/" + chave);
        if (ficha == null || ficha.PrefabDaUnidade == null) return false;

        // A cidade urbana do prefab foi importada deitada no eixo X, e as
        // instâncias colocadas no mapa preservam essa correção de -90 graus.
        // A bandeira define o X/Z; a altura vem da superfície real para que
        // o complexo não fique suspenso quando o relevo muda.
        Vector3 posicaoCidade = cidade.localizacao;
        if (RegistroSuperficieMapa.TryGetAltura(posicaoCidade, TipoSuperficieMapa.Chao, out float alturaMarcada))
        {
            posicaoCidade.y = alturaMarcada;
        }
        else
        {
            Terrain[] terrenos = Terrain.activeTerrains;
            for (int i = 0; i < terrenos.Length; i++)
            {
                Terrain terreno = terrenos[i];
                if (terreno == null || terreno.terrainData == null) continue;
                Vector3 origem = terreno.transform.position;
                Vector3 tamanho = terreno.terrainData.size;
                if (posicaoCidade.x < origem.x || posicaoCidade.x > origem.x + tamanho.x
                    || posicaoCidade.z < origem.z || posicaoCidade.z > origem.z + tamanho.z) continue;
                posicaoCidade.y = terreno.SampleHeight(posicaoCidade) + origem.y;
                break;
            }
        }

        GameObject objeto = Instantiate(ficha.PrefabDaUnidade, posicaoCidade, Quaternion.Euler(-90f, 0f, 0f));
        if (objeto == null) return false;

        // Prefabs urbanos podem ter o pivô fora da base da malha. Encosta a
        // geometria no terreno mantendo exatamente o ponto X/Z da bandeira.
        Renderer[] renderizadoresCidade = objeto.GetComponentsInChildren<Renderer>(true);
        if (renderizadoresCidade.Length > 0)
        {
            float menorY = float.PositiveInfinity;
            for (int i = 0; i < renderizadoresCidade.Length; i++)
            {
                if (renderizadoresCidade[i] != null)
                    menorY = Mathf.Min(menorY, renderizadoresCidade[i].bounds.min.y);
            }
            if (!float.IsInfinity(menorY))
                objeto.transform.position += Vector3.up * (posicaoCidade.y - menorY);
        }

        CidadeComplexoUrbano cidadeVisual = objeto.GetComponent<CidadeComplexoUrbano>();
        if (cidadeVisual == null)
        {
            Destroy(objeto);
            return false;
        }

        cidadeVisual.nomeCidade = cidade.nome;
        cidadeVisual.teamId = cidade.teamID;
        cidadeVisual.capacidadeHabitacional = Mathf.Clamp(cidade.capacidadeHabitacional, 500000, 2000000);
        cidadeVisual.populacaoResidente = Mathf.Clamp(cidade.populacaoCivil, 0, cidadeVisual.capacidadeHabitacional);
        cidadeVisual.qualidadeVida = Mathf.Clamp(Mathf.RoundToInt(cidade.desenvolvimento), 0, 100);
        cidadeVisual.SincronizarEstruturaEconomica();

        IdentidadeUnidade identidade = objeto.GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = objeto.AddComponent<IdentidadeUnidade>();
        identidade.teamID = cidade.teamID;
        identidade.nomeDoPais = cidade.nomePais;
        SaveableEntity.Garantir(objeto, ficha.PrefabDaUnidade.name);

        MarcadorTerritorio marcador = objeto.GetComponent<MarcadorTerritorio>();
        if (marcador == null) marcador = objeto.AddComponent<MarcadorTerritorio>();
        marcador.mostrarBordasNoJogo = false;
        marcador.ConfigureOwnership(cidade.teamID, false, 0f);

        int indice = cidades.IndexOf(cidade);
        cidades.RemoveAt(indice);
        nomesUsados.Remove(cidade.nome);
        RegistrarCidade(marcador, cidade.nome);
        OnDivisaoTerritorialAtualizada?.Invoke();
        return true;
    }

    private void MaterializarCidadesQualificadas()
    {
        if (Time.timeScale <= 0f) return;
        for (int i = cidades.Count - 1; i >= 0; i--)
        {
            CidadeEstado cidade = cidades[i];
            if (cidade == null || !cidade.ehCidadeNaoMaterializada) continue;
            bool atingiuLimiar = cidade.populacaoCivil >= populacaoMinimaMaterializacao
                || cidade.desenvolvimento >= desenvolvimentoMinimoMaterializacao
                || cidade.importanciaEstrategica >= importanciaMinimaMaterializacao
                || cidade.producaoEconomicaMensal >= producaoMinimaMaterializacao;
            if (atingiuLimiar) TentarMaterializarCidadeAbstrata(cidade.id);
        }
    }

    private CidadeEstado ObterRegiaoDaCidade(CidadeComplexoUrbano cidade)
    {
        if (cidade == null) return null;

        CidadeEstado maisProxima = null;
        float menorDistanciaSqr = float.PositiveInfinity;
        Vector3 posicao = cidade.transform.position;

        for (int i = 0; i < cidades.Count; i++)
        {
            CidadeEstado regiao = cidades[i];
            if (regiao != null && regiao.cidadeUrbana == cidade && regiao.teamID == cidade.teamId)
                return regiao;

            MarcadorTerritorio marcador = regiao != null ? regiao.marcador : null;
            if (marcador == null || marcador.teamID != cidade.teamId) continue;

            float raio = Mathf.Max(0f, marcador.raioDeDominio);
            float distanciaSqr = (posicao - marcador.transform.position).sqrMagnitude;
            if (distanciaSqr > raio * raio || distanciaSqr >= menorDistanciaSqr) continue;

            menorDistanciaSqr = distanciaSqr;
            maisProxima = regiao;
        }

        return maisProxima;
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
            if (cid.ehCidadeNaoMaterializada
                && (posicao - cid.localizacao).sqrMagnitude <= RaioConsultaCidadeAbstrata * RaioConsultaCidadeAbstrata)
                return cid.atratividade;
        }
        return atratividadeMedia;
    }

    public float ObterEficienciaMaoDeObraLocal(Vector3 posicao)
    {
        foreach (var cid in cidades)
        {
            if (cid.marcador != null && Vector3.Distance(posicao, cid.marcador.transform.position) <= cid.marcador.raioDeDominio)
            {
                return CalcularEficienciaMaoDeObra(cid);
            }
            if (cid.ehCidadeNaoMaterializada
                && (posicao - cid.localizacao).sqrMagnitude <= RaioConsultaCidadeAbstrata * RaioConsultaCidadeAbstrata)
                return CalcularEficienciaMaoDeObra(cid);
        }
        return 1f;
    }

    private static float CalcularEficienciaMaoDeObra(CidadeEstado cidade)
    {
        if (cidade.empregosTotais == 0) return 1f;
        // Garante no mínimo 50% de eficiência mesmo sem população para que as primeiras construções funcionem.
        if (cidade.populacaoCivil == 0) return 0.5f;
        return Mathf.Clamp((float)cidade.populacaoCivil / cidade.empregosTotais, 0.1f, 1f);
    }

    public List<CidadeEstado> ObterCidadesComAeroporto(int teamID)
    {
        return cidades.Where(c => c.teamID == teamID && c.temAeroporto).ToList();
    }
}
