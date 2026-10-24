using NUnit.Framework;
using System;
using System.Reflection;

public sealed class AgriculturaNacionalEditModeTests
{
    [TestCase(100000000L, 1f, 1000000L)]
    [TestCase(10000000000L, 1f, 100000000L)]
    [TestCase(100L, 50f, 50L)]
    public void FoodBudgetUsesPercentageOfAvailableNationalCash(long saldo, float percentual, long esperado)
    {
        Assert.That(CalcularOrcamentoCompraAlimentar(saldo, percentual), Is.EqualTo(esperado));
    }

    [Test]
    public void FoodBudgetClampsInvalidPercentageAndNeverReturnsNegativeCash()
    {
        Assert.That(CalcularOrcamentoCompraAlimentar(100000000L, -5f), Is.Zero);
        Assert.That(CalcularOrcamentoCompraAlimentar(100000000L, 250f), Is.EqualTo(100000000L));
        Assert.That(CalcularOrcamentoCompraAlimentar(-1L, 1f), Is.Zero);
        Assert.That(CalcularOrcamentoCompraAlimentar(100000000L, float.NaN), Is.Zero);
    }

    [Test]
    public void CobrancaFixaConverteMilhoesParaDinheiroDoJogo()
    {
        Type tipo = Type.GetType("AgriculturaNacional, Assembly-CSharp");
        Assert.That(tipo, Is.Not.Null);
        MethodInfo metodo = tipo.GetMethod("CalcularCobrancaFixaSemanal", BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);
        Assert.That(Convert.ToInt64(metodo.Invoke(null, new object[] { 100 })), Is.EqualTo(100_000_000L));
        Assert.That(Convert.ToInt64(metodo.Invoke(null, new object[] { 250 })), Is.EqualTo(250_000_000L));
        Assert.That(Convert.ToInt64(metodo.Invoke(null, new object[] { -1 })), Is.EqualTo(100_000_000L));
    }

    [Test]
    public void BonusFixoRespeitaValorPagoEInterrompeQuandoCobrancaFalha()
    {
        Type tipo = Type.GetType("AgriculturaNacional, Assembly-CSharp");
        Assert.That(tipo, Is.Not.Null);
        MethodInfo estimar = tipo.GetMethod("CalcularBonusInvestimentoFixo", BindingFlags.Public | BindingFlags.Static);
        MethodInfo aplicar = tipo.GetMethod("CalcularBonusAplicadoInvestimentoFixo", BindingFlags.Public | BindingFlags.Static);
        Assert.That(estimar, Is.Not.Null);
        Assert.That(aplicar, Is.Not.Null);

        float bonusBaixo = Convert.ToSingle(estimar.Invoke(null, new object[] { 100 }));
        float bonusAlto = Convert.ToSingle(estimar.Invoke(null, new object[] { 1000 }));
        Assert.That(bonusBaixo, Is.GreaterThan(0f));
        Assert.That(bonusAlto, Is.GreaterThan(bonusBaixo));
        Assert.That(Convert.ToSingle(aplicar.Invoke(null, new object[] { 1000, 0L, false })), Is.Zero,
            "Não deve haver bônus antes de uma cobrança efetivamente paga.");
        Assert.That(Convert.ToSingle(aplicar.Invoke(null, new object[] { 1000, 1_000_000_000L, true })), Is.Zero,
            "Uma cobrança recusada não deve gerar bônus.");
        Assert.That(Convert.ToSingle(aplicar.Invoke(null, new object[] { 1000, 1_000_000_000L, false })), Is.EqualTo(bonusAlto).Within(0.0001f));
    }

    [Test]
    public void ProducaoDiariaDiminuiComEscassezHidrica()
    {
        float normal = Calcular(100f, 1f, 1f, 1f, 1f, 1f, 1f);
        float escassez = Calcular(100f, 1f, 0.4f, 1f, 1f, 1f, 1f);
        Assert.That(escassez, Is.EqualTo(normal * 0.4f).Within(0.001f));
    }

    [Test]
    public void InsumoOuLogisticaNulosNaoGeramComida()
    {
        Assert.That(Calcular(100f, 1f, 1f, 0f, 1f, 1f, 1f), Is.EqualTo(0f));
        Assert.That(Calcular(100f, 1f, 1f, 1f, 1f, 1f, 0f), Is.EqualTo(0f));
    }

    [Test]
    public void AguaSuficienteMantemFatorEAguaZeradaInterrompeProducao()
    {
        Assert.That(InvocarFloat("CalcularFatorAgua", 100f, 100f, 1f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(InvocarFloat("CalcularFatorAgua", 0f, 100f, 1f), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void EscassezDeSementesEFertilizanteReduzProdutividadeGradualmente()
    {
        float completa = InvocarFloat("CalcularFatorInsumo", 100f, 100f, 0.15f, 0f);
        float metade = InvocarFloat("CalcularFatorInsumo", 50f, 100f, 0.15f, 0f);
        float zerada = InvocarFloat("CalcularFatorInsumo", 0f, 100f, 0.15f, 0f);
        Assert.That(completa, Is.EqualTo(1f).Within(0.001f));
        Assert.That(metade, Is.GreaterThan(zerada).And.LessThan(completa));
        Assert.That(zerada, Is.EqualTo(0.15f).Within(0.001f));
    }

    [Test]
    public void SemAgrotoxicoHaPenalidadeLeveEEstoqueCompletoRestauraFator()
    {
        float semProduto = InvocarFloat("CalcularFatorInsumo", 0f, 10f, 0.9f, 0f);
        float estoqueCompleto = InvocarFloat("CalcularFatorInsumo", 10f, 10f, 0.9f, 0f);
        Assert.That(semProduto, Is.EqualTo(0.9f).Within(0.001f));
        Assert.That(estoqueCompleto, Is.EqualTo(1f).Within(0.001f));
    }

    [TestCase(44_000_000L, 2200)]
    [TestCase(22_000_000L, 1100)]
    [TestCase(19_999L, 0)]
    [TestCase(-1L, 0)]
    public void OrcamentoDiarioFinanciaCapacidadeAgricolaProporcional(long verba, int capacidadeEsperada)
    {
        Assert.That(InvocarInt("AgriculturaNacional", "CalcularCapacidadeFinanciadaPorOrcamentoDiario", verba),
            Is.EqualTo(capacidadeEsperada));
    }

    [Test]
    public void ReferenciaDeQuarentaEQuatroMilhoesAproximaCemMilCivisComSafraMinima()
    {
        int capacidadeFinanciada = InvocarInt("AgriculturaNacional",
            "CalcularCapacidadeFinanciadaPorOrcamentoDiario", 44_000_000L);
        float bonus = InvocarFloat("CalcularBonusInvestimentoPercentual", 44_000_000L * 7L);
        float producao = InvocarFloat("CalcularProducaoDiaria",
            54f + capacidadeFinanciada, 1f + bonus, 0.3f, 1f, 1f, 1f, 1f);
        int comidaArredondada = (int)Math.Round(producao, MidpointRounding.AwayFromZero);
        int civis = InvocarInt("SistemaPopulacao", "CalcularCivisAtendidosPorComida", comidaArredondada);
        Assert.That(capacidadeFinanciada, Is.EqualTo(2200));
        Assert.That(comidaArredondada, Is.EqualTo(1014));
        Assert.That(civis, Is.EqualTo(101400));
    }

    [TestCase(388f, 38800)]
    [TestCase(1000f, 100000)]
    [TestCase(0f, 0)]
    public void UnidadesDeComidaMostramCivisAtendidosNaEscalaDoConsumo(float comida, int civisEsperados)
    {
        Assert.That(InvocarInt("SistemaPopulacao", "CalcularCivisAtendidosPorComida", comida), Is.EqualTo(civisEsperados));
    }

    private static float Calcular(params float[] args)
    {
        Type tipo = Type.GetType("AgriculturaNacional, Assembly-CSharp");
        Assert.That(tipo, Is.Not.Null, "O sistema de agricultura deve compilar no Assembly-CSharp.");
        MethodInfo metodo = tipo.GetMethod("CalcularProducaoDiaria", BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);
        return (float)metodo.Invoke(null, new object[] { args[0], args[1], args[2], args[3], args[4], args[5], args[6] });
    }

    private static long CalcularOrcamentoCompraAlimentar(long saldo, float percentual)
    {
        Type tipo = Type.GetType("AgriculturaNacional, Assembly-CSharp");
        Assert.That(tipo, Is.Not.Null, "O sistema de agricultura deve compilar no Assembly-CSharp.");
        MethodInfo metodo = tipo.GetMethod("CalcularOrcamentoCompraAlimentar", BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);
        return Convert.ToInt64(metodo.Invoke(null, new object[] { saldo, percentual }));
    }

    private static float InvocarFloat(string nome, params object[] args)
    {
        Type tipo = Type.GetType("AgriculturaNacional, Assembly-CSharp");
        Assert.That(tipo, Is.Not.Null);
        MethodInfo metodo = tipo.GetMethod(nome, BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);
        return (float)metodo.Invoke(null, args);
    }

    private static int InvocarInt(string tipoNome, string nome, params object[] args)
    {
        Type tipo = Type.GetType(tipoNome + ", Assembly-CSharp");
        Assert.That(tipo, Is.Not.Null);
        MethodInfo metodo = tipo.GetMethod(nome, BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);
        return Convert.ToInt32(metodo.Invoke(null, args));
    }
}
