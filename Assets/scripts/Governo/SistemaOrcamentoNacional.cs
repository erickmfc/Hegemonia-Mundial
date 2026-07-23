using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class LinhaOrcamento
{
    public string nome;
    public string baseCalculo;
    public decimal valorDiario;
    public decimal valorMensal;
    public string tendencia;
    public string status;
    public string detalhamento;
}

[Serializable]
public class RelatorioOrcamentoNacional
{
    public decimal receitaTotalDia;
    public decimal despesaTotalDia;
    public decimal saldoLiquidoDia;
    public decimal projecaoMensal;
    public float tesouroAtual;
    public float dividaTotal;
    public float inflacao;
    public float cargaFiscalMedia;

    public List<LinhaOrcamento> receitas = new List<LinhaOrcamento>();
    public List<LinhaOrcamento> despesas = new List<LinhaOrcamento>();
}

/// <summary>
/// Componente consolidador do Orçamento Nacional.
/// Apenas lê dados de sistemas existentes sem modificar lógicas de combate, produção ou população.
/// </summary>
public class SistemaOrcamentoNacional : MonoBehaviour
{
    public static SistemaOrcamentoNacional Instancia { get; private set; }

    private static readonly List<IdentidadeUnidade> bufferUnidades = new List<IdentidadeUnidade>();

    private void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
        }
    }

    public static SistemaOrcamentoNacional ObterOuCriar()
    {
        if (Instancia != null) return Instancia;

        SistemaOrcamentoNacional existente = FindObjectOfType<SistemaOrcamentoNacional>();
        if (existente != null)
        {
            Instancia = existente;
            return Instancia;
        }

        GameObject go = new GameObject("SistemaOrcamentoNacional");
        Instancia = go.AddComponent<SistemaOrcamentoNacional>();
        return Instancia;
    }

    public RelatorioOrcamentoNacional GerarRelatorio(int teamId = 1)
    {
        RelatorioOrcamentoNacional relatorio = new RelatorioOrcamentoNacional();

        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        DadosPaisGoverno p = gov != null ? gov.ObterPais(teamId) : null;
        if (p == null) return relatorio;

        SistemaEconomiaImoveis sistemaEconomia = SistemaEconomiaImoveis.Instancia;
        DadosEconomiaPais eco = sistemaEconomia != null ? sistemaEconomia.ObterEconomia(teamId) : null;

        // Atualizar Tesouro com sincronização real
        if (teamId == 1 && GerenciadorRecursos.Instancia != null)
        {
            p.saldo = GerenciadorRecursos.Instancia.dinheiro;
        }
        relatorio.tesouroAtual = p.saldo;
        relatorio.inflacao = p.inflacao;
        relatorio.cargaFiscalMedia = (p.impostoMoradia + p.impostoIndustria + p.impostoComercio) / 3f;

        // Leitura de unidades militares vivas
        bufferUnidades.Clear();
        RegistroEntidadesJogo.FillUnidades(bufferUnidades);
        int infantarias = 0, veiculos = 0, navais = 0, aereos = 0, estruturas = 0;
        for (int i = 0; i < bufferUnidades.Count; i++)
        {
            var u = bufferUnidades[i];
            if (u == null || u.teamID != teamId) continue;
            switch (u.tipoUnidade)
            {
                case TipoUnidade.Infantaria: infantarias++; break;
                case TipoUnidade.Veiculo: veiculos++; break;
                case TipoUnidade.Naval: navais++; break;
                case TipoUnidade.Aereo: aereos++; break;
                case TipoUnidade.Estrutura: estruturas++; break;
            }
        }

        // Cidades registradas
        int numCidades = 1;
        if (GerenciadorDivisaoTerritorial.Instancia != null && GerenciadorDivisaoTerritorial.Instancia.cidades != null)
        {
            int cidadesPais = GerenciadorDivisaoTerritorial.Instancia.cidades.Count(c => c != null && c.teamID == teamId);
            if (cidadesPais > 0) numCidades = cidadesPais;
        }

        // Empréstimos e dívidas
        float dividaEmprestimos = p.emprestimos != null ? p.emprestimos.Sum(e => e != null ? e.saldoDevedor : 0f) : 0f;
        relatorio.dividaTotal = p.divida + dividaEmprestimos;
        float jurosDia = p.emprestimos != null ? p.emprestimos.Sum(e => e != null ? e.saldoDevedor * e.jurosPorTick : 0f) : 0f;
        int emprestimosAtivos = p.emprestimos != null ? p.emprestimos.Count(e => e != null && e.saldoDevedor > 0f) : 0;

        // Pesquisas e Ciência
        int pesquisasAtivas = p.pesquisas != null ? p.pesquisas.Count(x => x != null && x.emAndamento) : 0;
        float custoPesquisasDia = p.pesquisas != null ? p.pesquisas.Where(x => x != null && x.emAndamento).Sum(x => (float)x.custoSaldo / Mathf.Max(1, x.duracaoDias)) : 0f;
        int labsAtivos = p.laboratorios != null ? p.laboratorios.Count(l => l != null && l.nivelAtual > 0) : 0;
        float custoLabsDia = p.laboratorios != null ? p.laboratorios.Where(l => l != null && l.nivelAtual > 0).Sum(l => l.custoSaldo * l.nivelAtual * 0.1f) : 0f;
        bool sateliteAtivo = p.sateliteDefesa != null && p.sateliteDefesa.desbloqueado;
        float custoSateliteDia = sateliteAtivo ? (p.sateliteDefesa.custoOperacionalDiario + p.sateliteDefesa.custoManutencaoDiaria) : 0f;

        // ==================== RECEITAS ====================

        // 1. Imposto de Renda
        float taxaEmprego = eco != null ? eco.TaxaEmprego : (p.emprego / 100f);
        float valorIR = eco != null ? eco.receitaMoradia : (p.populacaoCivil * 0.05f * (p.impostoMoradia / 100f) * p.PoderDeCompra);
        relatorio.receitas.Add(new LinhaOrcamento
        {
            nome = "Imposto de Renda",
            baseCalculo = $"{p.populacaoCivil} civis ({taxaEmprego * 100f:0}% emp.)",
            valorDiario = (decimal)valorIR,
            valorMensal = (decimal)(valorIR * 30f),
            tendencia = p.impostoMoradia >= 15 ? "+ ALTA" : "ESTÁVEL",
            status = "RECEBENDO",
            detalhamento = $"População civil: {p.populacaoCivil} hab. | Emprego: {taxaEmprego * 100f:0}% | Alíquota Moradia/Renda: {p.impostoMoradia}% | Poder de Compra: {p.PoderDeCompra:0.00}"
        });

        // 2. Imposto sobre Consumo
        float valorConsumo = eco != null ? eco.receitaComercio : (p.populacaoCivil * 0.04f * (p.impostoComercio / 100f) * p.PoderDeCompra);
        relatorio.receitas.Add(new LinhaOrcamento
        {
            nome = "Imposto sobre Consumo",
            baseCalculo = $"Mercado {p.populacaoCivil} hab. ({p.impostoComercio}%)",
            valorDiario = (decimal)valorConsumo,
            valorMensal = (decimal)(valorConsumo * 30f),
            tendencia = p.inflacao > 5f ? "- INFLAÇÃO" : "+ NORMAL",
            status = "RECEBENDO",
            detalhamento = $"Mercado consumidor: {p.populacaoCivil} habitantes | Inflação atual: {p.inflacao:0.0}% | Alíquota de Comércio/Consumo: {p.impostoComercio}% | Poder de Compra: {p.PoderDeCompra:0.00}"
        });

        // 3. Imposto Corporativo
        float ef = eco != null ? eco.eficienciaMedia : 1f;
        float indProd = eco != null ? eco.industriaProduzida : p.producao;
        float valorCorp = eco != null ? eco.receitaIndustria : (p.nivelIndustrial * 0.5f * (p.impostoIndustria / 100f));
        relatorio.receitas.Add(new LinhaOrcamento
        {
            nome = "Imposto Corporativo",
            baseCalculo = $"{indProd:0.0}t prod. (Ef: {ef * 100f:0}%)",
            valorDiario = (decimal)valorCorp,
            valorMensal = (decimal)(valorCorp * 30f),
            tendencia = ef >= 0.8f ? "+ ALTA" : "- QUEDA",
            status = "RECEBENDO",
            detalhamento = $"Setor produtivo/industrial ({indProd:0.0}t) | Eficiência média: {ef * 100f:0}% | Alíquota de Indústria: {p.impostoIndustria}% | Estruturas ativas"
        });

        // 4. Tarifas Comerciais
        float exp = eco != null ? eco.exportacaoTotal : 0f;
        float imp = eco != null ? eco.importacaoTotal : 0f;
        float valorTarifas = Mathf.Max(0.5f, (exp + imp) * 0.15f * (p.impostoComercio / 100f) + p.nivelDiplomatico * 0.05f);
        relatorio.receitas.Add(new LinhaOrcamento
        {
            nome = "Tarifas Comerciais",
            baseCalculo = $"Comércio ext. ({exp + imp:0.0}t)",
            valorDiario = (decimal)valorTarifas,
            valorMensal = (decimal)(valorTarifas * 30f),
            tendencia = p.sancionado ? "- EMBARGO" : "+ ATIVO",
            status = p.sancionado ? "SANCIONADO" : "RECEBENDO",
            detalhamento = $"Exportações: {exp:0.0}t | Importações: {imp:0.0}t | Nível Diplomático: {p.nivelDiplomatico} | Tarifa alfandegária sobre trocas internacionais"
        });

        // 5. Receita de Energia
        float energProd = eco != null ? eco.energiaProduzida : p.energiaProduzida;
        float energCons = eco != null ? eco.energiaConsumida : p.energiaConsumida;
        float valorEnergia = eco != null ? eco.receitaEnergia : (energProd * 0.15f);
        relatorio.receitas.Add(new LinhaOrcamento
        {
            nome = "Receita de Energia",
            baseCalculo = $"Ger: {energProd:0.0}MW / Cons: {energCons:0.0}MW",
            valorDiario = (decimal)valorEnergia,
            valorMensal = (decimal)(valorEnergia * 30f),
            tendencia = p.deficitEnergia <= 0f ? "+ SOBRA" : "- DEFICIT",
            status = p.deficitEnergia <= 0f ? "OPERANDO" : "SEM SOBRA",
            detalhamento = $"Energia gerada: {energProd:0.0} MW | Consumo nacional: {energCons:0.0} MW | Venda/Distribuição pública de excedente energético"
        });

        // 6. Recursos Públicos
        float petrProd = eco != null ? eco.petroleoProduzido : p.petroleo;
        float valorRecurso = petrProd * 1.2f + (p.uranio > 0 ? 2f : 0f);
        relatorio.receitas.Add(new LinhaOrcamento
        {
            nome = "Recursos Públicos",
            baseCalculo = $"Petróleo: {petrProd:0.0}t/dia",
            valorDiario = (decimal)valorRecurso,
            valorMensal = (decimal)(valorRecurso * 30f),
            tendencia = petrProd > 0f ? "+ ATIVO" : "INATIVO",
            status = petrProd > 0f ? "ARRECADANDO" : "SEM EXTRAÇÃO",
            detalhamento = $"Petróleo produzido: {petrProd:0.0}t/dia | Uranio em estoque: {p.uranio} | Royalties de extração mineral e energéticos estatais"
        });

        // 7. Outras Receitas
        float valorOutrasRec = Mathf.Max(0f, p.reservaOuro * 0.002f + (p.cambioComLider > 1f ? 1.5f : 0.5f));
        relatorio.receitas.Add(new LinhaOrcamento
        {
            nome = "Outras Receitas",
            baseCalculo = $"Reservas ($ {p.reservaOuro:N0})",
            valorDiario = (decimal)valorOutrasRec,
            valorMensal = (decimal)(valorOutrasRec * 30f),
            tendencia = p.reservaOuro > 300f ? "+ ESTÁVEL" : "BAIXA",
            status = "ATIVO",
            detalhamento = $"Reservas de Ouro: {p.reservaOuro:N0} | Câmbio de referência: 1 {p.nomeMoeda} = {p.cambioComLider:0.00} {p.moedaLiderReferencia} | Rendimentos financeiros"
        });

        // ==================== DESPESAS ====================

        // 1. Assistência Social
        float custoSoc = eco != null ? eco.custoSocial : 24f;
        float valorSoc = Mathf.Max(4f, custoSoc + (p.mortosAcumulados * 0.04f) + (p.pressaoHabitacional > 1f ? 8f : 0f));
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Assistência Social",
            baseCalculo = $"{p.populacaoCivil} civis (Hab: {p.pressaoHabitacional * 100f:0}%)",
            valorDiario = (decimal)valorSoc,
            valorMensal = (decimal)(valorSoc * 30f),
            tendencia = p.pressaoHabitacional > 1f ? "- ALTA DEMANDA" : "+ CONTROLADO",
            status = "PAGO",
            detalhamento = $"População civil amparada: {p.populacaoCivil} | Baixas de guerra acumuladas: {p.mortosAcumulados} | Pressão habitacional: {p.pressaoHabitacional * 100f:0}%"
        });

        // 2. Saúde
        float valorSaude = Mathf.Max(2f, p.populacaoCivil * 0.005f + (p.qualidadeVida < 50f ? 6f : 2f));
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Saúde",
            baseCalculo = $"Pop: {p.populacaoCivil} | QV: {p.qualidadeVida:0}%",
            valorDiario = (decimal)valorSaude,
            valorMensal = (decimal)(valorSaude * 30f),
            tendencia = p.qualidadeVida >= 70f ? "+ BOM" : "- DEMANDA",
            status = "PAGO",
            detalhamento = $"Rede pública de saúde preventiva e hospitalar | População coberta: {p.populacaoCivil} | Qualidade de vida atual: {p.qualidadeVida:0}%"
        });

        // 3. Educação
        float valorEdu = Mathf.Max(2f, p.populacaoCivil * 0.004f + p.nivelEconomico * 0.04f);
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Educação",
            baseCalculo = $"Pop: {p.populacaoCivil} | Nível: {p.nivelEconomico}",
            valorDiario = (decimal)valorEdu,
            valorMensal = (decimal)(valorEdu * 30f),
            tendencia = "ESTÁVEL",
            status = "PAGO",
            detalhamento = $"Manutenção de escolas e formação básica nacional | População atendida: {p.populacaoCivil} | Nível de desenvolvimento econômico: {p.nivelEconomico}"
        });

        // 4. Administração Pública
        float valorAdmin = Mathf.Max(4f, numCidades * 3.5f + (p.estabilidade < 50f ? 5f : 2f));
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Administração Pública",
            baseCalculo = $"{numCidades} cidade(s) registrada(s)",
            valorDiario = (decimal)valorAdmin,
            valorMensal = (decimal)(valorAdmin * 30f),
            tendencia = "ESTÁVEL",
            status = "PAGO",
            detalhamento = $"Gestão territorial e administrativa de {numCidades} cidade(s) | Manutenção de sedes governamentais e serviços públicos estatais"
        });

        // 5. Infraestrutura
        float custoInfra = eco != null ? eco.custoInfraestrutura : 2f;
        int semEnergia = eco != null ? eco.estruturasSemEnergia : p.estruturasSemEnergia;
        float valorInfra = Mathf.Max(2f, custoInfra + (semEnergia * 2.5f));
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Infraestrutura",
            baseCalculo = $"Malha de suporte ({semEnergia} s/ energ)",
            valorDiario = (decimal)valorInfra,
            valorMensal = (decimal)(valorInfra * 30f),
            tendencia = semEnergia > 0 ? "- PENALIDADE" : "+ REGULAR",
            status = "PAGO",
            detalhamento = $"Manutenção de malha elétrica, vias, portos e aeroportos | Estruturas sem abastecimento elétrico: {semEnergia} (Penalidade diária)"
        });

        // 6. Defesa Nacional
        float custoMil = eco != null ? eco.custoMilitar : 2f;
        float acrescimoGuerra = p.emGuerra ? (p.populacaoMilitarAtiva * 0.02f + 15f) : (p.populacaoMilitarAtiva * 0.012f);
        float valorDefesa = Mathf.Max(2f, custoMil + acrescimoGuerra + (p.armamentos * 0.0015f));
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Defesa Nacional",
            baseCalculo = $"{p.populacaoMilitarAtiva} tropas ({infantarias}Inf/{veiculos}Tq/{aereos}Aer/{navais}Nav)",
            valorDiario = (decimal)valorDefesa,
            valorMensal = (decimal)(valorDefesa * 30f),
            tendencia = p.emGuerra ? "- MOBILIZAÇÃO" : "+ EM PAZ",
            status = p.emGuerra ? "EM GUERRA" : "PRONTIDÃO",
            detalhamento = $"Manutenção de {p.populacaoMilitarAtiva} militares ativos | Unidades vivas: {infantarias} soldados, {veiculos} veículos/tanques, {aereos} aeronaves, {navais} embarcações | Status: {(p.emGuerra ? "EM GUERRA (Custo logístico elevado)" : "Paz")}"
        });

        // 7. Produção Pública
        float custoProd = eco != null ? eco.custoProducao : 2f;
        float valorProd = Mathf.Max(2f, custoProd);
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Produção Pública",
            baseCalculo = $"Parque estatal ({p.nivelIndustrial})",
            valorDiario = (decimal)valorProd,
            valorMensal = (decimal)(valorProd * 30f),
            tendencia = "ESTÁVEL",
            status = "PAGO",
            detalhamento = $"Custeio operacional de refinarias, poços e complexos industriais do Estado | Nível industrial nacional: {p.nivelIndustrial}"
        });

        // 8. Ciência e Tecnologia
        float valorCiencia = Mathf.Max(1f, custoPesquisasDia + custoLabsDia + custoSateliteDia);
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Ciência e Tecnologia",
            baseCalculo = $"{pesquisasAtivas} pesq. / {labsAtivos} labs",
            valorDiario = (decimal)valorCiencia,
            valorMensal = (decimal)(valorCiencia * 30f),
            tendencia = pesquisasAtivas > 0 ? "- EM PESQUISA" : "+ ESTÁVEL",
            status = "PAGO",
            detalhamento = $"Pesquisas ativas: {pesquisasAtivas} | Laboratórios operacionais: {labsAtivos} | Satélite de Defesa: {(sateliteAtivo ? "Ativo ($300/dia)" : "Inativo")}"
        });

        // 9. Juros da Dívida
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Juros da Dívida",
            baseCalculo = $"Dívida: $ {relatorio.dividaTotal:N0}",
            valorDiario = (decimal)jurosDia,
            valorMensal = (decimal)(jurosDia * 30f),
            tendencia = relatorio.dividaTotal > 0f ? "- COBRANÇA" : "+ QUITADO",
            status = relatorio.dividaTotal > 0f ? "ENCARGO" : "QUITADO",
            detalhamento = $"Dívida acumulada total: $ {relatorio.dividaTotal:N0} | Empréstimos federativos ativos: {emprestimosAtivos} | Serviço diário da dívida"
        });

        // 10. Outras Despesas
        float valorOutrasDesp = 1.5f;
        relatorio.despesas.Add(new LinhaOrcamento
        {
            nome = "Outras Despesas",
            baseCalculo = "Encargos diversos",
            valorDiario = (decimal)valorOutrasDesp,
            valorMensal = (decimal)(valorOutrasDesp * 30f),
            tendencia = "ESTÁVEL",
            status = "PAGO",
            detalhamento = "Despesas contingentes e encargos administrativos operacionais menores"
        });

        // Somas Totais
        relatorio.receitaTotalDia = relatorio.receitas.Sum(r => r.valorDiario);
        relatorio.despesaTotalDia = relatorio.despesas.Sum(d => d.valorDiario);
        relatorio.saldoLiquidoDia = relatorio.receitaTotalDia - relatorio.despesaTotalDia;
        relatorio.projecaoMensal = relatorio.saldoLiquidoDia * 30m;

        // Atualizar saldo operacional de governo sem sobrescrever o dinheiro principal
        p.rendaPorSegundo = (float)relatorio.receitaTotalDia;
        p.gastosPorSegundo = (float)relatorio.despesaTotalDia;
        p.saldoOperacional = (float)relatorio.saldoLiquidoDia;
        if (eco != null)
        {
            eco.saldoOperacional = p.saldoOperacional;
            eco.custoManutencao = p.gastosPorSegundo;
        }

        return relatorio;
    }
}
