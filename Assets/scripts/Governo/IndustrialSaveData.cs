using System;
using System.Collections.Generic;
using System.Linq;

public static class IndustriaIds
{
    public const string MinerioFerro = "minerio_ferro";
    public const string MinerioCobre = "minerio_cobre";
    public const string Bauxita = "bauxita";
    public const string MinerioTitanio = "minerio_titanio";
    public const string UranioBruto = "uranio_bruto";
    public const string MinerioLitio = "minerio_litio";
    public const string TerrasRaras = "terras_raras";
    public const string MinerioNiquel = "minerio_niquel";
    public const string MinerioManganes = "minerio_manganes";
    public const string Silica = "silica";
    public const string Calcario = "calcario";
    public const string AreiaIndustrial = "areia_industrial";
    public const string Fosfato = "fosfato";
    public const string CarvaoMineral = "carvao_mineral";
    public const string PetroleoBruto = "petroleo_bruto";
    public const string GasNatural = "gas_natural";

    public const string AcoEstrutural = "aco_estrutural";
    public const string CobreEletrolitico = "cobre_eletrolitico";
    public const string AluminioIndustrial = "aluminio_industrial";
    public const string Duraluminio = "duraluminio";
    public const string LigaTitanio = "liga_titanio";
    public const string AcoEspecial = "aco_especial";
    public const string CelulasLitio = "celulas_litio";
    public const string VidroIndustrial = "vidro_industrial";
    public const string Cimento = "cimento";
    public const string Fertilizante = "fertilizante";
    public const string PlasticoIndustrial = "plastico_industrial";
    public const string BorrachaSintetica = "borracha_sintetica";
    public const string ComponentesEletronicos = "componentes_eletronicos";
    public const string UranioEnriquecido = "uranio_enriquecido";
    public const string CabosEletricos = "cabos_eletricos";
    public const string CircuitosEletronicos = "circuitos_eletronicos";
    public const string Sensores = "sensores";
    public const string BateriaIndustrial = "bateria_industrial";
    public const string BateriaAltaCapacidade = "bateria_alta_capacidade";
    public const string MotorEletrico = "motor_eletrico";
    public const string MotorCombustao = "motor_combustao";
    public const string MotorDiesel = "motor_diesel";
    public const string MotorNaval = "motor_naval";
    public const string TurbinaAerea = "turbina_aerea";
    public const string TurbinaNaval = "turbina_naval";
    public const string PneusIndustriais = "pneus_industriais";
    public const string SistemaHidraulico = "sistema_hidraulico";
    public const string ChassiLeve = "chassi_leve";
    public const string ChassiPesado = "chassi_pesado";
    public const string Esteiras = "esteiras";
    public const string BlindagemLeve = "blindagem_leve";
    public const string BlindagemMedia = "blindagem_media";
    public const string BlindagemPesada = "blindagem_pesada";
    public const string Avionicos = "avionicos";
    public const string Radar = "radar";
    public const string Sonar = "sonar";
    public const string ModuloComunicacao = "modulo_comunicacao";
    public const string ModuloNavegacao = "modulo_navegacao";
    public const string EquipamentoLogistico = "equipamento_logistico";
    public const string MaquinasIndustriais = "maquinas_industriais";
    public const string GuindasteIndustrial = "guindaste_industrial";
    public const string Etanol = "etanol";
    public const string Biodiesel = "biodiesel";
    public const string Biogas = "biogas";
    public const string Gasolina = "gasolina";
    public const string Diesel = "diesel";
    public const string CombustivelAviacao = "combustivel_aviacao";
    public const string CombustivelNaval = "combustivel_naval";
    public const string LubrificanteIndustrial = "lubrificante_industrial";

    public static readonly string[] RecursosBrutos =
    {
        MinerioFerro,
        MinerioCobre,
        Bauxita,
        MinerioTitanio,
        UranioBruto,
        MinerioLitio,
        TerrasRaras,
        MinerioNiquel,
        MinerioManganes,
        Silica,
        Calcario,
        AreiaIndustrial,
        Fosfato,
        CarvaoMineral,
        PetroleoBruto,
        GasNatural
    };

    public static readonly string[] MateriaisRefinados =
    {
        AcoEstrutural,
        CobreEletrolitico,
        AluminioIndustrial,
        Duraluminio,
        LigaTitanio,
        AcoEspecial,
        CelulasLitio,
        VidroIndustrial,
        Cimento,
        Fertilizante,
        PlasticoIndustrial,
        BorrachaSintetica,
        ComponentesEletronicos,
        UranioEnriquecido
    };

    public static readonly string[] ComponentesIndustriais =
    {
        CabosEletricos,
        CircuitosEletronicos,
        Sensores,
        BateriaIndustrial,
        BateriaAltaCapacidade,
        MotorEletrico,
        MotorCombustao,
        MotorDiesel,
        MotorNaval,
        TurbinaAerea,
        TurbinaNaval,
        PneusIndustriais,
        SistemaHidraulico,
        ChassiLeve,
        ChassiPesado,
        Esteiras,
        BlindagemLeve,
        BlindagemMedia,
        BlindagemPesada,
        Avionicos,
        Radar,
        Sonar,
        ModuloComunicacao,
        ModuloNavegacao,
        EquipamentoLogistico,
        MaquinasIndustriais,
        GuindasteIndustrial
    };

    public static readonly string[] CombustiveisIndustriais =
    {
        Etanol,
        Biodiesel,
        Biogas,
        Gasolina,
        Diesel,
        CombustivelAviacao,
        CombustivelNaval,
        LubrificanteIndustrial
    };

    public static readonly string[] TodosOsMateriais = RecursosBrutos
        .Concat(MateriaisRefinados)
        .Concat(ComponentesIndustriais)
        .Concat(CombustiveisIndustriais)
        .ToArray();

    public static bool EhRecursoBruto(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId)) return false;
        return RecursosBrutos.Contains(recursoId, StringComparer.OrdinalIgnoreCase);
    }

    public static bool EhMaterialRefinado(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId)) return false;
        return MateriaisRefinados.Contains(recursoId, StringComparer.OrdinalIgnoreCase);
    }

    public static bool EhComponenteIndustrial(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId)) return false;
        return ComponentesIndustriais.Contains(recursoId, StringComparer.OrdinalIgnoreCase);
    }

    public static bool EhCombustivelIndustrial(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId)) return false;
        return CombustiveisIndustriais.Contains(recursoId, StringComparer.OrdinalIgnoreCase);
    }

    public static bool EhIndustrial(string recursoId)
    {
        if (string.IsNullOrWhiteSpace(recursoId)) return false;
        return TodosOsMateriais.Contains(recursoId, StringComparer.OrdinalIgnoreCase);
    }
}

[Serializable]
public class QuantidadeRecursoIndustrial
{
    public string recursoId;
    public double quantidade;

    public QuantidadeRecursoIndustrial()
    {
    }

    public QuantidadeRecursoIndustrial(string recursoId, double quantidade)
    {
        this.recursoId = recursoId;
        this.quantidade = quantidade;
    }
}

[Serializable]
public class SavePerfilMineralIndustrial
{
    public int teamId;
    public bool perfilGerado;
    public int ferro;
    public int cobre;
    public int bauxita;
    public int titanio;
    public int uranio;
    public float modificadorIndustrial = 1f;
    public bool extraindoFerro;
    public bool extraindoCobre;
    public bool extraindoBauxita;
    public bool extraindoTitanio;
    public bool extraindoUranio;
    public bool refinandoAco;
    public bool refinandoCobreEletrolitico;
    public bool refinandoDuraluminio;
    public bool refinandoLigaTitanio;
    public bool refinandoComponentes;
    public bool refinandoUranioEnriquecido;
}

[Serializable]
public class SaveEstoqueIndustrial
{
    public string paisId;
    public List<QuantidadeRecursoIndustrial> estoques = new List<QuantidadeRecursoIndustrial>();
    public List<QuantidadeRecursoIndustrial> reservas = new List<QuantidadeRecursoIndustrial>();
}

[Serializable]
public class SaveOrdemExtracaoIndustrial
{
    public string id;
    public int teamId;
    public string recursoId;
    public string nomeRecurso;
    public string estado;
    public bool continua;
    public int diasObjetivo;
    public int diasRestantes;
    public double quantidadeAlvo;
    public double quantidadeRestante;
    public double estoqueAlvo;
    public double totalProduzido;
    public float custoDinheiro;
    public float custoEnergia;
    public float producaoBase;
    public float producaoUltimoDia;
    public bool exigeAutorizacao;
    public bool autorizada;
    public string motivoBloqueio;
    public int ultimaDataProcessada;
}

[Serializable]
public class SaveOrdemRefinoIndustrial
{
    public string id;
    public int teamId;
    public string receitaId;
    public string produtoId;
    public string estado;
    public string linhaId;
    public float progresso;
    public int diasTotais;
    public int diasRestantes;
    public double quantidadeEntrada;
    public double quantidadeProduzida;
    public double dinheiroReservado;
    public double energiaReservada;
    public List<QuantidadeRecursoIndustrial> materiaisReservados = new List<QuantidadeRecursoIndustrial>();
    public int inicioDia;
    public int ultimaDataProcessada;
    public string pesquisaExigida;
    public int nivelIndustrialExigido;
}

[Serializable]
public class SaveLinhaIndustrial
{
    public string id;
    public int teamId;
    public int indice;
    public string estado;
    public string ordemRefinoId;
    public string receitaId;
    public float progresso;
    public int diasTotais;
    public int diasRestantes;
    public string motivoBloqueio;
}

[Serializable]
public class SaveImpactoPublicoIndustrial
{
    public string id;
    public int teamId;
    public string recursoId;
    public double quantidade;
    public float deltaFelicidade;
    public float deltaEstabilidade;
    public bool compra;
    public bool venda;
    public string mensagem;
    public int diaCriacao;
    public int diaAplicacao;
    public bool aplicado;
}

[Serializable]
public class SaveHistoricoIndustrial
{
    public int teamId;
    public string recursoId;
    public string categoria;
    public double quantidade;
    public double custoDinheiro;
    public double custoEnergia;
    public int dia;
    public string mensagem;
}

[Serializable]
public class IndustrialSaveData
{
    public int totalDias = 1;
    public List<SavePerfilMineralIndustrial> perfisMineral = new List<SavePerfilMineralIndustrial>();
    public List<SaveEstoqueIndustrial> estoques = new List<SaveEstoqueIndustrial>();
    public List<SaveOrdemExtracaoIndustrial> ordensExtracao = new List<SaveOrdemExtracaoIndustrial>();
    public List<SaveOrdemRefinoIndustrial> ordensRefino = new List<SaveOrdemRefinoIndustrial>();
    public List<SaveLinhaIndustrial> linhas = new List<SaveLinhaIndustrial>();
    public List<SaveImpactoPublicoIndustrial> impactosPendentes = new List<SaveImpactoPublicoIndustrial>();
    public List<SaveHistoricoIndustrial> historico = new List<SaveHistoricoIndustrial>();
}
