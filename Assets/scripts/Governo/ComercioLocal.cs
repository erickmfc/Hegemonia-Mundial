using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente para prefabs de mercado, loja, restaurante, shopping e centro
/// comercial. O sistema nacional soma todos os componentes ativos e publica
/// emprego, consumo, lucro, imposto e atratividade no Menu Governo.
/// </summary>
[DisallowMultipleComponent]
public sealed class ComercioLocal : MonoBehaviour
{
    public enum Categoria { Mercado, Loja, Restaurante, Farmacia, Shopping, CentroComercial, Personalizado }

    public enum PortePredio { Pequeno, Medio, Grande }

    [Header("Perfil")]
    public Categoria categoria = Categoria.Mercado;
    public PortePredio porte = PortePredio.Pequeno;
    [Min(10)] public int capacidadeEmpresas;
    [Min(1)] public int limiteTrabalhadoresPredio;
    public bool ocupacaoAutomatica = true;
    [Range(0f, 1f)] public float segurancaLocal = 0.75f;
    [Range(0f, 1f)] public float acessoTransporte = 0.75f;
    [Range(0f, 1f)] public float concorrenciaLocal = 0.25f;
    [Range(0f, 1f)] public float qualidadeServicos = 0.70f;
    [Min(1)] public int vagasMaximas = 12;
    [Min(1)] public int clientesPorSegundo = 22;
    [Min(0.01f)] public float consumoEnergia = 1.5f;
    [Min(0.01f)] public float precoMedioProduto = 4f;
    [Min(0.01f)] public float salarioPorTrabalhador = 0.7f;
    [Range(0f, 1f)] public float custoMercadoriasPercentual = 0.35f;

    [Header("Mercadorias")]
    [Min(0)] public int estoqueMercadorias = 250;
    [Min(1)] public int estoqueMaximo = 500;
    public bool reposicaoAutomatica = true;
    [Min(0)] public int reposicaoPorCiclo = 30;

    [Header("Estado (somente leitura)")]
    [SerializeField] private bool emFuncionamento;
    [SerializeField] private string motivoParada = "Aguardando dados";
    [SerializeField] private int trabalhadoresContratados;
    [SerializeField] private int vagasDisponiveis;
    [SerializeField] private float vendasUltimoCiclo;
    [SerializeField] private float salariosUltimoCiclo;
    [SerializeField] private float lucroUltimoCiclo;
    [SerializeField] private int empresasAbertas;
    [SerializeField] private int empresasAbertasRecentemente;
    [SerializeField] private int empresasFechadasRecentemente;
    [SerializeField] private float ocupacaoAtual;
    [SerializeField] private string principalNecessidade = "Aguardando analise";
    [SerializeField] private List<EmpresaComercial> empresas = new List<EmpresaComercial>();

    public bool EmFuncionamento => emFuncionamento;
    public string MotivoParada => motivoParada;
    public int TrabalhadoresContratados => trabalhadoresContratados;
    public int VagasDisponiveis => vagasDisponiveis;
    public int CapacidadeEmpresas => capacidadeEmpresas;
    public int EmpresasAbertas => empresasAbertas;
    public int EmpresasFechadasRecentemente => empresasFechadasRecentemente;
    public int EmpresasAbertasRecentemente => empresasAbertasRecentemente;
    public float OcupacaoAtual => ocupacaoAtual;
    public string PrincipalNecessidade => principalNecessidade;
    public IReadOnlyList<EmpresaComercial> Empresas => empresas;
    public int CapacidadeTrabalhadoresAtual { get; private set; }
    public int EmpresasEmFuncionamento { get; private set; }
    public static ComercioLocal PredioSelecionado { get; private set; }

    private void OnMouseDown()
    {
        PredioSelecionado = this;
    }

    public string GerarDetalhePredio()
    {
        return "Porte: " + porte.ToString().ToLowerInvariant()
            + "\nCapacidade maxima: " + capacidadeEmpresas + " empresas"
            + "\nEmpresas abertas: " + empresasAbertas
            + "\nEspacos disponiveis: " + Mathf.Max(0, capacidadeEmpresas - empresasAbertas)
            + "\nOcupacao atual: " + (ocupacaoAtual * 100f).ToString("0") + "%"
            + "\nTrabalhadores contratados: " + trabalhadoresContratados
            + "\nVagas abertas: " + vagasDisponiveis
            + "\nEstado: " + (emFuncionamento ? "Em crescimento" : "Parado")
            + "\nPrincipal necessidade: " + principalNecessidade
            + "\nMotivo: " + motivoParada;
    }

    private EstruturaEconomica estrutura;

    private void Reset()
    {
        InferirPerfilPorNome();
        AplicarPerfil();
    }

    private void Awake() { GarantirEstrutura(); }
    private void OnEnable() { SistemaComercioNacional.GarantirInstancia(); SistemaComercioNacional.Registrar(this); }
    private void OnDisable() { SistemaComercioNacional.Desregistrar(this); }

    private void OnValidate()
    {
        vagasMaximas = Mathf.Max(1, vagasMaximas);
        estoqueMaximo = Mathf.Max(1, estoqueMaximo);
        estoqueMercadorias = Mathf.Clamp(estoqueMercadorias, 0, estoqueMaximo);
        InicializarPorteSeNecessario();
    }

    private void InicializarPorteSeNecessario()
    {
        if (capacidadeEmpresas > 0 && limiteTrabalhadoresPredio > 0) return;
        if (categoria == Categoria.Shopping || categoria == Categoria.CentroComercial)
            porte = categoria == Categoria.Shopping ? PortePredio.Grande : PortePredio.Medio;
        if (categoria == Categoria.Shopping)
        {
            capacidadeEmpresas = UnityEngine.Random.Range(40, 61);
            limiteTrabalhadoresPredio = UnityEngine.Random.Range(1000, 6001);
        }
        else if (porte == PortePredio.Grande)
        {
            capacidadeEmpresas = UnityEngine.Random.Range(40, 61);
            limiteTrabalhadoresPredio = capacidadeEmpresas * 80;
        }
        else if (porte == PortePredio.Medio)
        {
            capacidadeEmpresas = UnityEngine.Random.Range(16, 31);
            limiteTrabalhadoresPredio = capacidadeEmpresas * 75;
        }
        else
        {
            porte = PortePredio.Pequeno;
            capacidadeEmpresas = UnityEngine.Random.Range(10, 16);
            limiteTrabalhadoresPredio = capacidadeEmpresas * 70;
        }
    }

    internal void AtualizarComposicao(int populacao, float poderCompra, int trabalhadoresDisponiveis, bool energia, float felicidade, float imposto)
    {
        InicializarPorteSeNecessario();
        if (!ocupacaoAutomatica) return;
        float demanda = Mathf.Clamp01(populacao / Mathf.Max(1f, capacidadeEmpresas * 80f));
        float fator = 0.30f + poderCompra * 0.30f + Mathf.Clamp01(trabalhadoresDisponiveis / Mathf.Max(1f, populacao)) * 0.15f
            + (energia ? 0.10f : 0f) + segurancaLocal * 0.05f + felicidade / 100f * 0.10f
            + acessoTransporte * 0.08f + qualidadeServicos * 0.05f - concorrenciaLocal * 0.12f - Mathf.Max(0f, imposto - 15f) * 0.005f;
        float alvoOcupacao = populacao <= 0 ? 0f : Mathf.Clamp(demanda * fator, 0.10f, 1f);
        int alvoEmpresas = Mathf.Clamp(Mathf.RoundToInt(capacidadeEmpresas * alvoOcupacao), 0, capacidadeEmpresas);
        empresasAbertasRecentemente = Mathf.Max(0, alvoEmpresas - empresasAbertas);
        empresasFechadasRecentemente = Mathf.Max(0, empresasAbertas - alvoEmpresas);
        while (empresas.Count < alvoEmpresas) empresas.Add(CriarEmpresa(empresas.Count));
        while (empresas.Count > alvoEmpresas) empresas.RemoveAt(empresas.Count - 1);
        empresasAbertas = empresas.Count;
        ocupacaoAtual = capacidadeEmpresas <= 0 ? 0f : empresasAbertas / (float)capacidadeEmpresas;
        CapacidadeTrabalhadoresAtual = 0;
        for (int i = 0; i < empresas.Count; i++) CapacidadeTrabalhadoresAtual += empresas[i].capacidadeFuncionarios;
        if (limiteTrabalhadoresPredio > 0) CapacidadeTrabalhadoresAtual = Mathf.Min(CapacidadeTrabalhadoresAtual, limiteTrabalhadoresPredio);
        vagasMaximas = Mathf.Max(1, CapacidadeTrabalhadoresAtual);
        principalNecessidade = !energia ? "Mais energia" : trabalhadoresDisponiveis <= 0 ? "Mais trabalhadores" : poderCompra < 0.45f ? "Mais poder de compra" : "Mercadorias e acesso";
    }

    private EmpresaComercial CriarEmpresa(int indice)
    {
        TipoEmpresaComercial tipo;
        switch (indice % 8)
        {
            case 1: tipo = TipoEmpresaComercial.Restaurante; break;
            case 2: tipo = TipoEmpresaComercial.Loja; break;
            case 3: tipo = TipoEmpresaComercial.Farmacia; break;
            case 4: tipo = TipoEmpresaComercial.Servicos; break;
            case 5: tipo = TipoEmpresaComercial.Escritorio; break;
            case 6: tipo = TipoEmpresaComercial.Tecnologia; break;
            case 7: tipo = TipoEmpresaComercial.Manutencao; break;
            default: tipo = TipoEmpresaComercial.Mercado; break;
        }
        if (categoria == Categoria.Restaurante) tipo = TipoEmpresaComercial.Restaurante;
        if (categoria == Categoria.Farmacia) tipo = TipoEmpresaComercial.Farmacia;
        if (categoria == Categoria.Shopping) tipo = indice % 3 == 0 ? TipoEmpresaComercial.Restaurante : TipoEmpresaComercial.Loja;
        int capacidade;
        switch (tipo)
        {
            case TipoEmpresaComercial.Mercado: capacidade = UnityEngine.Random.Range(50, 101); break;
            case TipoEmpresaComercial.Restaurante: capacidade = UnityEngine.Random.Range(20, 81); break;
            case TipoEmpresaComercial.Loja: capacidade = UnityEngine.Random.Range(50, 81); break;
            case TipoEmpresaComercial.Farmacia: capacidade = UnityEngine.Random.Range(20, 61); break;
            case TipoEmpresaComercial.Servicos: capacidade = UnityEngine.Random.Range(30, 151); break;
            default: capacidade = UnityEngine.Random.Range(20, 81); break;
        }
        return new EmpresaComercial { tipo = tipo, capacidadeFuncionarios = capacidade, estado = "Aberta" };
    }

    internal void DistribuirFuncionarios(int total, bool ativo, string motivo)
    {
        int restantes = Mathf.Max(0, total);
        EmpresasEmFuncionamento = 0;
        for (int i = 0; i < empresas.Count; i++)
        {
            EmpresaComercial empresa = empresas[i];
            empresa.funcionariosContratados = ativo ? Mathf.Min(empresa.capacidadeFuncionarios, restantes) : 0;
            restantes -= empresa.funcionariosContratados;
            empresa.vagasAbertas = Mathf.Max(0, empresa.capacidadeFuncionarios - empresa.funcionariosContratados);
            empresa.funcionamento = empresa.capacidadeFuncionarios <= 0 ? 0f : empresa.funcionariosContratados / (float)empresa.capacidadeFuncionarios;
            empresa.estado = ativo ? (empresa.funcionamento >= 0.75f ? "Em crescimento" : "Funcionamento reduzido") : "Parada";
            empresa.motivo = ativo ? "" : motivo;
            if (ativo && empresa.funcionariosContratados > 0) EmpresasEmFuncionamento++;
        }
    }

    internal void AplicarResultado(bool ativo, string motivo, bool semEnergia, int trabalhadores, int estoque, float vendas, float salarios, float lucro)
    {
        emFuncionamento = ativo;
        motivoParada = ativo ? "Funcionando" : motivo;
        trabalhadoresContratados = Mathf.Clamp(trabalhadores, 0, vagasMaximas);
        vagasDisponiveis = vagasMaximas - trabalhadoresContratados;
        estoqueMercadorias = Mathf.Clamp(estoque, 0, estoqueMaximo);
        vendasUltimoCiclo = vendas;
        salariosUltimoCiclo = salarios;
        lucroUltimoCiclo = lucro;

        GarantirEstrutura();
        estrutura.tipo = categoria == Categoria.Shopping ? TipoEstruturaEconomica.Shopping : TipoEstruturaEconomica.Comercio;
        estrutura.empregosGerados = vagasMaximas;
        estrutura.energiaConsumida = consumoEnergia;
        estrutura.dinheiroGerado = 0f; // vendas reais sao processadas pelo comercio nacional.
        estrutura.status = ativo ? StatusEstruturaEconomica.Ativa : (semEnergia ? StatusEstruturaEconomica.SemEnergia : StatusEstruturaEconomica.Inativa);
        estrutura.eficiencia = ativo ? trabalhadoresContratados / (float)Mathf.Max(1, vagasMaximas) : 0f;
    }

    private void GarantirEstrutura()
    {
        if (estrutura == null) estrutura = GetComponent<EstruturaEconomica>();
        if (estrutura == null) estrutura = gameObject.AddComponent<EstruturaEconomica>();
        estrutura.InferirTeamId();
    }

    private void InferirPerfilPorNome()
    {
        string nome = gameObject.name.ToLowerInvariant();
        if (nome.Contains("shopping")) categoria = Categoria.Shopping;
        else if (nome.Contains("restaurante") || nome.Contains("restaurant")) categoria = Categoria.Restaurante;
        else if (nome.Contains("farmac")) categoria = Categoria.Farmacia;
        else if (nome.Contains("centro comercial") || nome.Contains("centro comercia") || nome.Contains("centro empresarial") || nome.Contains("galeria") || nome.Contains("mall")) categoria = Categoria.CentroComercial;
        else if (nome.Contains("loja") || nome.Contains("shop")) categoria = Categoria.Loja;
        else categoria = Categoria.Mercado;
    }

    public void AplicarPerfil()
    {
        switch (categoria)
        {
            case Categoria.Loja: vagasMaximas = 8; clientesPorSegundo = 12; consumoEnergia = 1f; estoqueMaximo = 300; precoMedioProduto = 3.5f; break;
            case Categoria.Restaurante: vagasMaximas = 15; clientesPorSegundo = 25; consumoEnergia = 2.5f; estoqueMaximo = 360; precoMedioProduto = 7f; break;
            case Categoria.Farmacia: vagasMaximas = 12; clientesPorSegundo = 18; consumoEnergia = 1.8f; estoqueMaximo = 420; precoMedioProduto = 6f; break;
            case Categoria.Shopping: vagasMaximas = 220; clientesPorSegundo = 420; consumoEnergia = 55f; estoqueMaximo = 3000; precoMedioProduto = 8.5f; break;
            case Categoria.CentroComercial: vagasMaximas = 80; clientesPorSegundo = 160; consumoEnergia = 20f; estoqueMaximo = 1500; precoMedioProduto = 6.5f; break;
            case Categoria.Mercado: vagasMaximas = 12; clientesPorSegundo = 22; consumoEnergia = 1.5f; estoqueMaximo = 500; precoMedioProduto = 4f; break;
        }
        estoqueMercadorias = Mathf.Min(Mathf.Max(estoqueMercadorias, estoqueMaximo / 2), estoqueMaximo);
    }

    public static bool PareceComercio(GameObject objeto)
    {
        if (objeto == null) return false;
        string chave = (objeto.name + " " + objeto.tag).ToLowerInvariant();
        return chave.Contains("mercado") || chave.Contains("comerc") || chave.Contains("loja") || chave.Contains("shop")
            || chave.Contains("restaurante") || chave.Contains("restaurant") || chave.Contains("farmac")
            || chave.Contains("centro comercial") || chave.Contains("centro comercia") || chave.Contains("centro empresarial") || chave.Contains("galeria") || chave.Contains("mall");
    }

    public static void GarantirNoPrefabInstanciado(GameObject objeto)
    {
        if (!PareceComercio(objeto)) return;
        ComercioLocal comercio = objeto.GetComponent<ComercioLocal>();
        if (comercio == null) comercio = objeto.AddComponent<ComercioLocal>();
        comercio.InferirPerfilPorNome();
        comercio.AplicarPerfil();
    }
}

public enum TipoEmpresaComercial
{
    Mercado, Restaurante, Loja, Farmacia, Servicos, Escritorio, Banco, Clinica, Academia,
    Lanchonete, Tecnologia, Manutencao, Distribuidora, Deposito
}

[Serializable]
public sealed class EmpresaComercial
{
    public TipoEmpresaComercial tipo;
    public int capacidadeFuncionarios;
    public int funcionariosContratados;
    public int vagasAbertas;
    [Range(0f, 1f)] public float funcionamento;
    public string estado;
    public string motivo;
}

[Serializable]
public sealed class DadosComercioNacional
{
    public int prediosComerciais;
    public int prediosPequenos;
    public int prediosMedios;
    public int prediosGrandes;
    public int capacidadeTotalEmpresas;
    public int espacosComerciaisVazios;
    public float taxaOcupacaoPredios;
    public int empresasAbertasRecentemente;
    public int empresasFechadasRecentemente;
    public int demandaNaoAtendida;
    public int regioesComExcessoComercio;
    public int estabelecimentosAtivos;
    public int estabelecimentosParados;
    public int empresasAtivas;
    public int empresasParadas;
    public int empregosCriados;
    public int vagasDisponiveis;
    public int trabalhadoresContratados;
    public int mercadoriasDisponiveis;
    public float salariosPagos;
    public float impostosArrecadados;
    public float consumoPopulacao;
    public float vendasBrutas;
    public float lucroTotal;
    public float contribuicaoFelicidade;
    public float capacidadeAtracao;
    public readonly Dictionary<string, int> empresasPorCategoria = new Dictionary<string, int>();
    public string principalMotivoParada = "Nenhum";
}

[DefaultExecutionOrder(-50)]
public sealed class SistemaComercioNacional : MonoBehaviour
{
    public static SistemaComercioNacional Instancia { get; private set; }
    private static readonly HashSet<ComercioLocal> comercios = new HashSet<ComercioLocal>();
    private readonly Dictionary<int, DadosComercioNacional> relatorios = new Dictionary<int, DadosComercioNacional>();
    private float proximoCiclo;

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;
        new GameObject("SistemaComercioNacional").AddComponent<SistemaComercioNacional>();
    }
    public static void Registrar(ComercioLocal comercio) { if (comercio != null) comercios.Add(comercio); }
    public static void Desregistrar(ComercioLocal comercio) { if (comercio != null) comercios.Remove(comercio); }
    public static DadosComercioNacional ObterResumo(int teamId)
    {
        GarantirInstancia();
        DadosComercioNacional resumo;
        return Instancia.relatorios.TryGetValue(Mathf.Max(1, teamId), out resumo) ? resumo : new DadosComercioNacional();
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Time.unscaledTime < proximoCiclo) return;
        proximoCiclo = Time.unscaledTime + 1f;
        Recalcular();
    }

    private void Recalcular()
    {
        relatorios.Clear();
        Dictionary<int, int> trabalhadoresRestantes = new Dictionary<int, int>();
        Dictionary<int, Dictionary<string, int>> paradas = new Dictionary<int, Dictionary<string, int>>();
        foreach (ComercioLocal comercio in comercios)
        {
            if (comercio == null || !comercio.isActiveAndEnabled) continue;
            EstruturaEconomica estrutura = comercio.GetComponent<EstruturaEconomica>();
            int team = Mathf.Max(1, estrutura != null ? estrutura.teamId : 1);
            DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(team) : null;
            DadosEconomiaPais economia = SistemaEconomiaImoveis.Instancia != null ? SistemaEconomiaImoveis.Instancia.ObterEconomia(team) : null;
            bool jogador = SistemaGovernoMundial.Instancia == null || team == SistemaGovernoMundial.Instancia.teamJogador;
            GerenciadorRecursos recursos = jogador ? GerenciadorRecursos.Instancia : null;
            int populacao = Mathf.Max(0, pais != null ? pais.populacaoCivil : (recursos != null ? recursos.populacaoAtual : 0));
            float poderCompra = pais != null ? pais.PoderDeCompra : 1f;
            if (!trabalhadoresRestantes.ContainsKey(team))
                trabalhadoresRestantes[team] = Mathf.Max(0, Mathf.RoundToInt(populacao * (1f - Mathf.Clamp01((pais != null ? pais.emprego : 0f) / 100f))));
            bool temEnergia = (economia != null && economia.energiaProduzida >= economia.energiaConsumida - 0.01f)
                || (recursos != null && recursos.energia > 0) || (pais != null && pais.energia > 0 && economia == null);
            comercio.AtualizarComposicao(
                populacao,
                poderCompra,
                trabalhadoresRestantes[team],
                temEnergia,
                pais != null ? pais.felicidade : 50f,
                pais != null ? pais.impostoComercio : 12f);
            int trabalhadores = Mathf.Min(comercio.vagasMaximas, trabalhadoresRestantes[team]);
            trabalhadoresRestantes[team] -= trabalhadores;
            int estoque = comercio.estoqueMercadorias;
            if (comercio.reposicaoAutomatica && estoque < comercio.estoqueMaximo / 3)
            {
                int reposicao = Mathf.Min(comercio.reposicaoPorCiclo, comercio.estoqueMaximo - estoque);
                if (recursos != null) { reposicao = Mathf.Min(reposicao, recursos.aco); if (reposicao > 0) recursos.RemoverRecurso("Aco", reposicao); }
                estoque += Mathf.Max(0, reposicao);
            }
            string motivo = null;
            if (!temEnergia) motivo = "Falta de energia";
            else if (populacao <= 0) motivo = "Sem clientes: regiao sem moradores";
            else if (trabalhadores <= 0) motivo = "Falta de trabalhadores";
            else if (estoque <= 0) motivo = "Falta de mercadorias";
            else if (poderCompra < 0.20f) motivo = "Poder de compra insuficiente";
            if (motivo != null)
            {
                // Um comercio parado nao retem funcionarios: eles voltam ao
                // mercado de trabalho e podem ser absorvidos por outro local.
                trabalhadoresRestantes[team] += trabalhadores;
                trabalhadores = 0;
            }
            float eficiencia = motivo == null ? trabalhadores / (float)Mathf.Max(1, comercio.vagasMaximas) : 0f;
            float unidades = motivo == null ? Mathf.Min(estoque, Mathf.Min(comercio.clientesPorSegundo, populacao * 0.08f * poderCompra) * eficiencia) : 0f;
            estoque -= Mathf.CeilToInt(unidades);
            float vendas = unidades * comercio.precoMedioProduto;
            float salarios = trabalhadores * comercio.salarioPorTrabalhador;
            float lucro = Mathf.Max(0f, vendas - salarios - vendas * comercio.custoMercadoriasPercentual - comercio.consumoEnergia * 0.15f);
            bool ativo = motivo == null && unidades > 0f;
            if (!ativo && motivo == null) motivo = "Sem demanda suficiente";
            comercio.DistribuirFuncionarios(trabalhadores, ativo, motivo);
            comercio.AplicarResultado(ativo, motivo, !temEnergia, trabalhadores, estoque, vendas, salarios, lucro);
            DadosComercioNacional resumo;
            if (!relatorios.TryGetValue(team, out resumo)) { resumo = new DadosComercioNacional(); relatorios.Add(team, resumo); paradas.Add(team, new Dictionary<string, int>()); }
            resumo.prediosComerciais++; resumo.empregosCriados += comercio.vagasMaximas; resumo.trabalhadoresContratados += trabalhadores;
            resumo.capacidadeTotalEmpresas += comercio.capacidadeEmpresas;
            resumo.espacosComerciaisVazios += Mathf.Max(0, comercio.capacidadeEmpresas - comercio.EmpresasAbertas);
            resumo.empresasAbertasRecentemente += comercio.EmpresasAbertasRecentemente;
            resumo.empresasFechadasRecentemente += comercio.EmpresasFechadasRecentemente;
            resumo.demandaNaoAtendida += Mathf.Max(0, Mathf.RoundToInt(populacao / 80f) - comercio.EmpresasAbertas);
            if (comercio.porte == ComercioLocal.PortePredio.Pequeno) resumo.prediosPequenos++;
            else if (comercio.porte == ComercioLocal.PortePredio.Medio) resumo.prediosMedios++;
            else resumo.prediosGrandes++;
            foreach (EmpresaComercial empresa in comercio.Empresas)
            {
                string nomeCategoria = empresa.tipo.ToString();
                resumo.empresasPorCategoria[nomeCategoria] = resumo.empresasPorCategoria.ContainsKey(nomeCategoria)
                    ? resumo.empresasPorCategoria[nomeCategoria] + 1 : 1;
            }
            resumo.vagasDisponiveis += comercio.vagasMaximas - trabalhadores; resumo.mercadoriasDisponiveis += estoque;
            resumo.salariosPagos += salarios; resumo.vendasBrutas += vendas; resumo.consumoPopulacao += vendas; resumo.lucroTotal += lucro;
            resumo.impostosArrecadados += vendas * (pais != null ? pais.impostoComercio / 100f : 0.12f);
            resumo.empresasAtivas += comercio.EmpresasEmFuncionamento;
            resumo.empresasParadas += Mathf.Max(0, comercio.EmpresasAbertas - comercio.EmpresasEmFuncionamento);
            if (ativo) resumo.estabelecimentosAtivos++; else { resumo.estabelecimentosParados++; Dictionary<string, int> motivos = paradas[team]; motivos[motivo] = motivos.ContainsKey(motivo) ? motivos[motivo] + 1 : 1; }
        }
        foreach (KeyValuePair<int, DadosComercioNacional> par in relatorios)
        {
            DadosComercioNacional r = par.Value;
            r.taxaOcupacaoPredios = r.capacidadeTotalEmpresas <= 0 ? 0f
                : (r.capacidadeTotalEmpresas - r.espacosComerciaisVazios) / (float)r.capacidadeTotalEmpresas;
            if (r.demandaNaoAtendida > 0 && r.estabelecimentosAtivos == 0) r.regioesComExcessoComercio++;
            float atividade = r.prediosComerciais == 0 ? 0f : r.estabelecimentosAtivos / (float)r.prediosComerciais;
            float emprego = r.empregosCriados == 0 ? 0f : r.trabalhadoresContratados / (float)r.empregosCriados;
            float estoque = Mathf.Clamp01(r.mercadoriasDisponiveis / (float)Mathf.Max(1, r.prediosComerciais * 100));
            r.contribuicaoFelicidade = Mathf.Clamp(10f * atividade * emprego * estoque, 0f, 10f);
            r.capacidadeAtracao = Mathf.Clamp(atividade * 45f + Mathf.Clamp01(r.empregosCriados / 200f) * 35f + estoque * 20f, 0f, 100f);
            Dictionary<string, int> motivos = paradas[par.Key];
            foreach (KeyValuePair<string, int> motivo in motivos) if (r.principalMotivoParada == "Nenhum" || motivo.Value > motivos[r.principalMotivoParada]) r.principalMotivoParada = motivo.Key;
        }
    }
}
