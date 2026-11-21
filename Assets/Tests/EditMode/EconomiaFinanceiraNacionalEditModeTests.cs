#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class EconomiaFinanceiraNacionalEditModeTests
{
    private static Type Resolve(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(type => type != null);

    [TestCase(950L, "950")]
    [TestCase(950000L, "950K")]
    [TestCase(125000000L, "125M")]
    [TestCase(1500000000L, "1,5B")]
    [TestCase(1200000000000L, "1,2T")]
    public void FormatarCompacto_UsaSufixoESemEspacos(long valor, string esperado)
    {
        Type economia = Resolve("EconomiaFinanceiraNacional");
        Assert.That(economia, Is.Not.Null);
        MethodInfo metodo = economia.GetMethod("FormatarCompacto", BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo.Invoke(null, new object[] { valor }), Is.EqualTo(esperado));
    }

    [Test]
    public void Projetar_LimitaHorizonteEfeitoSemMutarPais()
    {
        Type paisType = Resolve("DadosPaisGoverno");
        Type economia = Resolve("EconomiaFinanceiraNacional");
        Assert.That(paisType, Is.Not.Null);
        object pais = Activator.CreateInstance(paisType);
        paisType.GetField("saldo").SetValue(pais, 1000L);
        paisType.GetField("receitaDiariaTesouro").SetValue(pais, 250L);
        paisType.GetField("despesaDiariaTesouro").SetValue(pais, 50L);
        MethodInfo projetar = economia.GetMethod("Projetar", BindingFlags.Public | BindingFlags.Static);
        object resultado = projetar.Invoke(null, new object[] { pais, 30, 0L, 0L });

        Assert.That((int)resultado.GetType().GetField("dias").GetValue(resultado), Is.EqualTo(7));
        Assert.That((long)resultado.GetType().GetField("tesouroProjetado").GetValue(resultado), Is.EqualTo(2400L));
        Assert.That((long)paisType.GetField("saldo").GetValue(pais), Is.EqualTo(1000L));
    }

    [Test]
    public void RateioOrcamento_SomaCemEAjustaServicosSemMudarTotal()
    {
        Type paisType = Resolve("DadosPaisGoverno");
        Type rateio = Resolve("OrcamentoInvestimentoNacional");
        object pais = Activator.CreateInstance(paisType);
        rateio.GetMethod("NormalizarPercentuais").Invoke(null, new[] { pais });
        Assert.That(Soma(paisType, pais), Is.EqualTo(100));
        rateio.GetMethod("AjustarPercentualServico").Invoke(null, new object[] { pais, 0, 40 });
        Assert.That(Soma(paisType, pais), Is.EqualTo(100));
        Assert.That((int)paisType.GetField("orcamentoPctServicos").GetValue(pais), Is.EqualTo(40));
    }

    private static int Soma(Type tipo, object pais)
    {
        string[] campos = { "orcamentoPctAgricultura", "orcamentoPctEnergia", "orcamentoPctPetroleo",
            "orcamentoPctIndustria", "orcamentoPctCiencia", "orcamentoPctMilitar", "orcamentoPctServicos",
            "orcamentoPctInfraestrutura", "orcamentoPctSeguranca" };
        return campos.Sum(campo => (int)tipo.GetField(campo).GetValue(pais));
    }
}
#endif
