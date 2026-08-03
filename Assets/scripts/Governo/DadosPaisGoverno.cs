using System;
using System.Collections.Generic;
using UnityEngine;

public enum PerfilPaisIA
{
    Pequeno,
    Industrial,
    ProdutorPetroleo,
    Militarista,
    Neutro,
    Rival,
    Aliado
}

public enum ModoInicialPaisIA
{
    Paz,
    Ocioso,
    Comercial,
    Crescimento,
    Crise,
    GuerraFria,
    Mobilizacao,
    GuerraTotal,
    AgressivoContraJogador
}

public enum TipoPropostaInternacional
{
    Compra,
    Venda,
    Anuncio,
    Emprestimo,
    Doacao,
    Contraoferta,
    Bloqueio,
    Sancao,
    PedidoAjuda,
    PactoDefensivo
}

public enum StatusPropostaInternacional
{
    Pendente,
    Aceita,
    Recusada,
    Negociando,
    Expirada,
    Executada
}

public enum PosturaRelacaoPais
{
    Neutro,
    Amigo,
    Inimigo
}

[Serializable]
public class PesquisaNacionalEstado
{
    public string id;
    public string nome;
    public string categoria;
    public string descricao;
    public string requisitosVisuais;
    public string desbloqueia;
    public string dependencias;
    public int custoSaldo;
    public int custoEnergia;
    public int duracaoDias = 1;
    public int diaInicio = -1;
    public int nivelAtual;
    public int nivelMaximo = 1;
    public bool emAndamento;
    public bool concluida;
}

[Serializable]
public class TecnologiaNacionalEstado
{
    public string id;
    public string nome;
    public string categoria;
    public string descricao;
    public string efeito;
    public string dependencias;
    public int custoSaldo;
    public int custoEnergia;
    public int duracaoDias = 1;
    public int diaInicio = -1;
    public int nivelAtual;
    public int nivelMaximo = 1;
    public bool emAndamento;
}

[Serializable]
public class LaboratorioNacionalEstado
{
    public string id;
    public string nome;
    public string especializacao;
    public string descricao;
    public string dependencias;
    public int custoSaldo;
    public int custoEnergia;
    public int duracaoDias = 1;
    public int diaInicio = -1;
    public int nivelAtual;
    public int nivelMaximo = 3;
    public bool emExpansao;
}

[Serializable]
public class SateliteDefesaEstado
{
    public bool desbloqueado;
    public bool manutencaoAutomatica = true;
    public float integridade = 100f;
    public float desempenho = 72f;
    public int custoOperacionalDiario = 180;
    public int custoManutencaoDiaria = 120;
    public int ultimoDiaProcessado;
}

[Serializable]
public class DadosPaisGoverno
{
    public int teamId = 1;
    public string nomePais = "Republica Atlas";
    public string nomePresidente = "Presidente Atlas";
    public string nomeMoeda = "Atlas";
    public string simboloMoeda = "AT$";

    [Header("Economia")]
    public float valorMoeda = 1f;
    public float variacaoMoeda = 0f;
    public float reservaOuro = 500f;
    public float cambioComLider = 1f;
    public string moedaLiderReferencia = "Atlas";
    public float inflacao = 3f;
    public float emprego = 78f;
    public float moradia = 70f;
    public float estabilidade = 70f;
    public float producao = 70f;
    
    [Header("Demografia")]
    public int populacao = 3200;
    public int populacaoMaxima = 3200;
    public int populacaoCivil = 3200;
    public int populacaoMilitarAtiva = 0;
    public int reservistas = 0;
    public int alistaveis = 0;
    public int mortosAcumulados = 0;
    [Range(0f, 100f)] public float felicidade = 70f;
    public float mortalidade = 1f;
    public float natalidade = 1.2f;

    // ─── Dinâmica Populacional Avançada ───────────────────────────────────
    /// <summary>Migração líquida por tick. Positivo = imigração, Negativo = emigração.</summary>
    public float taxaMigracao = 0f;
    /// <summary>Razão população/capacidade habitacional (0 = vazio, 1 = lotado, >1 = superpopulação).</summary>
    public float pressaoHabitacional = 0f;
    /// <summary>Índice composto de satisfação (0-100) com serviços públicos e qualidade de vida.</summary>
    public float indiceSatisfacaoServicos = 50f;
    /// <summary>Índice de atratividade nacional para crescimento populacional (0-1).</summary>
    public float indiceAtratividade = 0.5f;
    // ─────────────────────────────────────────────────────────────────────


    public long saldo = 5000L;
    public float rendaPorSegundo = 10f;
    public float gastosPorSegundo = 4f;
    public float divida = 0f;
    [Range(0, 35)] public int impostoMoradia = 10;
    [Range(0, 35)] public int impostoIndustria = 15;
    [Range(0, 35)] public int impostoComercio = 12;
    public float receitaMoradia;
    public float receitaIndustria;
    public float receitaComercio;
    public float receitaEnergia;
    public float custoManutencao;
    public float saldoOperacional;

    [Header("Economia Viva")]
    public float qualidadeVida = 55f;
    public float energiaProduzida;
    public float energiaConsumida;
    public float deficitComida;
    public float deficitEnergia;
    public float deficitPetroleo;
    public int estruturasSemEnergia;
    public float exportacaoTotal;
    public float importacaoTotal;

    [Header("Diplomacia")]
    public string bloco = "Nenhum";
    public string federacaoGlobal = string.Empty;
    [Range(0f, 100f)] public float legitimidadeGlobal = 70f;
    public List<EmprestimoFederativoEstado> emprestimos = new List<EmprestimoFederativoEstado>();
    public bool emGuerra;
    public bool sancionado;
    public int aliadoPrioritarioTeamId = -1;
    public int rivalTeamId = -1;

    [Header("IA Nacional")]
    public PerfilPaisIA perfilIA = PerfilPaisIA.Neutro;
    public ModoInicialPaisIA modoInicialIA = ModoInicialPaisIA.Paz;
    [Range(0, 100)] public int nivelEconomico = 50;
    [Range(0, 100)] public int nivelIndustrial = 50;
    [Range(0, 100)] public int nivelMilitar = 50;
    [Range(0, 100)] public int nivelDiplomatico = 50;
    [Range(0f, 1f)] public float pesoDiplomacia = 0.50f;
    [Range(0f, 1f)] public float pesoComercio = 0.55f;
    [Range(0f, 1f)] public float pesoIndustria = 0.50f;
    [Range(0f, 1f)] public float pesoMilitarismo = 0.45f;
    [Range(0f, 1f)] public float pesoAgressividade = 0.35f;
    [Range(0f, 1f)] public float pesoDependenciaExterna = 0.45f;
    [Range(0f, 1f)] public float pesoAutossuficiencia = 0.45f;
    [Range(0f, 1f)] public float pesoRiscoEconomico = 0.35f;
    [Range(0f, 1f)] public float pesoControleEstoque = 0.55f;
    [Range(0f, 1f)] public float pesoLealdadeAliados = 0.55f;
    [Range(0f, 1f)] public float pesoOdioRivais = 0.45f;
    public string planoEstrategico = "Equilibrio";
    public bool tecnologiaExtracaoConcluida = false;
    public List<PesquisaNacionalEstado> pesquisas = new List<PesquisaNacionalEstado>();
    public List<TecnologiaNacionalEstado> tecnologias = new List<TecnologiaNacionalEstado>();
    public List<LaboratorioNacionalEstado> laboratorios = new List<LaboratorioNacionalEstado>();
    public SateliteDefesaEstado sateliteDefesa = new SateliteDefesaEstado();

    [Header("Estoque")]
    public int comida = 500;
    public int petroleo = 500;
    public int energia = 200;
    public int aco = 300;
    public int armamentos = 500;
    public int uranio;

    // ─── Estoque Mineral (gerido pelo SistemaIndustrial) ─────────────────
    // Matéria-Prima Bruta (toneladas)
    [Header("Estoque Mineral — Bruto (t)")]
    public float minerioFerro       = 0f;
    public float minerioCobre       = 0f;
    public float bauxita            = 0f;
    public float minerioTitanio     = 0f;
    public float uranioBruto        = 0f;

    // Materiais Refinados (toneladas)
    [Header("Estoque Mineral — Refinado (t)")]
    public float acoEstrutural              = 0f;
    public float cobreEletrolitico          = 0f;
    public float duraluminio               = 0f;
    public float ligaTitanio               = 0f;
    public float componentesEletronicos    = 0f;
    public float uranioEnriquecido         = 0f;
    // ─────────────────────────────────────────────────────────────────────

    public float PoderDeCompra
    {
        get
        {
            float inflacaoPeso = Mathf.Clamp01(1f - inflacao / 30f);
            return Mathf.Clamp(valorMoeda * inflacaoPeso * (0.65f + estabilidade / 200f), 0.05f, 3f);
        }
    }

    public float PontuacaoEconomica()
    {
        float estoqueEssencial = 0f;
        estoqueEssencial += comida > 150 ? 8f : -10f;
        estoqueEssencial += petroleo > 150 ? 8f : -8f;
        estoqueEssencial += energia > 120 ? 6f : -6f;
        estoqueEssencial += aco > 120 ? 6f : -6f;

        float score = 50f;
        score += (emprego - 50f) * 0.35f;
        score += (moradia - 50f) * 0.25f;
        score += (estabilidade - 50f) * 0.30f;
        score += (producao - 50f) * 0.22f;
        score += (qualidadeVida - 50f) * 0.18f;
        score += estoqueEssencial;
        score -= Mathf.Clamp(deficitComida + deficitEnergia + deficitPetroleo, 0f, 30f) * 0.45f;
        if (emGuerra) score -= 18f;
        if (sancionado) score -= 14f;
        return Mathf.Clamp(score, 0f, 100f);
    }
}

[Serializable]
public class PropostaInternacional
{
    public string id;
    public TipoPropostaInternacional tipo = TipoPropostaInternacional.Compra;
    public StatusPropostaInternacional status = StatusPropostaInternacional.Pendente;
    public int origemTeamId;
    public int alvoTeamId;
    public RecursoMercado recurso = RecursoMercado.Nenhum;
    public int quantidade;
    public int precoUnitario;
    public int prioridade = 50;
    public float criadaEm;
    public float expiraEm;
    public string motivo;
    public string dedupKey;

    public int Total
    {
        get { return Mathf.Max(0, quantidade) * Mathf.Max(0, precoUnitario); }
    }

    public bool EstaPendente
    {
        get { return status == StatusPropostaInternacional.Pendente || status == StatusPropostaInternacional.Negociando; }
    }
}

[Serializable]
public class RelacaoPaisGoverno
{
    public int teamA;
    public int teamB;
    [Range(-100, 100)] public int valor;
    public bool tratadoComercial = true;
    public bool pactoMilitar;
    public bool pedidoPendente;
    public bool sancaoAtiva;
    public bool guerraDeclarada;
    public PosturaRelacaoPais posturaAParaB = PosturaRelacaoPais.Neutro;
    public PosturaRelacaoPais posturaBParaA = PosturaRelacaoPais.Neutro;

    public bool Envolve(int a, int b)
    {
        return (teamA == a && teamB == b) || (teamA == b && teamB == a);
    }

    public int Outro(int teamId)
    {
        return teamA == teamId ? teamB : teamA;
    }

    public PosturaRelacaoPais PosturaDe(int teamId)
    {
        return teamA == teamId ? posturaAParaB : posturaBParaA;
    }

    public void DefinirPostura(int teamId, PosturaRelacaoPais postura)
    {
        if (teamA == teamId) posturaAParaB = postura;
        else if (teamB == teamId) posturaBParaA = postura;
    }
}
