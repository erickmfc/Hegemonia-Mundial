using NUnit.Framework;
using System;
using System.Reflection;

public sealed class AgriculturaNacionalEditModeTests
{
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

    private static float Calcular(params float[] args)
    {
        Type tipo = Type.GetType("AgriculturaNacional, Assembly-CSharp");
        Assert.That(tipo, Is.Not.Null, "O sistema de agricultura deve compilar no Assembly-CSharp.");
        MethodInfo metodo = tipo.GetMethod("CalcularProducaoDiaria", BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);
        return (float)metodo.Invoke(null, new object[] { args[0], args[1], args[2], args[3], args[4], args[5], args[6] });
    }

    private static float InvocarFloat(string nome, params object[] args)
    {
        Type tipo = Type.GetType("AgriculturaNacional, Assembly-CSharp");
        Assert.That(tipo, Is.Not.Null);
        MethodInfo metodo = tipo.GetMethod(nome, BindingFlags.Public | BindingFlags.Static);
        Assert.That(metodo, Is.Not.Null);
        return (float)metodo.Invoke(null, args);
    }
}
