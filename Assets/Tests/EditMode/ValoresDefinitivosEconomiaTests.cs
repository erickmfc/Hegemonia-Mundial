#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class ValoresDefinitivosEconomiaTests
{
    private static readonly Type ValoresType = ResolveType("ValoresDefinitivosHegemonia");
    private static readonly Type DificuldadeType = ResolveType("DificuldadeJogo");

    private static Type ResolveType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(type => type != null);
    }

    private static object Dificuldade(string nome)
    {
        Assert.That(DificuldadeType, Is.Not.Null, "DificuldadeJogo nao foi carregado.");
        return Enum.Parse(DificuldadeType, nome);
    }

    private static long Longo(string metodo, string dificuldade)
    {
        Assert.That(ValoresType, Is.Not.Null, "ValoresDefinitivosHegemonia nao foi carregado.");
        MethodInfo info = ValoresType.GetMethod(metodo, BindingFlags.Public | BindingFlags.Static);
        return (long)info.Invoke(null, new[] { Dificuldade(dificuldade) });
    }

    private static float DecimalFloat(string metodo, string dificuldade)
    {
        Assert.That(ValoresType, Is.Not.Null, "ValoresDefinitivosHegemonia nao foi carregado.");
        MethodInfo info = ValoresType.GetMethod(metodo, BindingFlags.Public | BindingFlags.Static);
        return (float)info.Invoke(null, new[] { Dificuldade(dificuldade) });
    }

    [Test]
    public void Dificuldades_UsamCaixaInicialOficial()
    {
        Assert.That(Longo("DinheiroInicial", "Facil"), Is.EqualTo(120000000000L));
        Assert.That(Longo("DinheiroInicial", "Normal"), Is.EqualTo(70000000000L));
        Assert.That(Longo("DinheiroInicial", "Dificil"), Is.EqualTo(35000000000L));
    }

    [Test]
    public void Dificuldades_AplicamMultiplicadoresEconomicos()
    {
        Assert.That(DecimalFloat("MultiplicadorReceita", "Facil"), Is.EqualTo(1.25f));
        Assert.That(DecimalFloat("MultiplicadorReceita", "Normal"), Is.EqualTo(1f));
        Assert.That(DecimalFloat("MultiplicadorReceita", "Dificil"), Is.EqualTo(0.8f));
        Assert.That(DecimalFloat("MultiplicadorManutencao", "Facil"), Is.EqualTo(0.8f));
        Assert.That(DecimalFloat("MultiplicadorManutencao", "Normal"), Is.EqualTo(1f));
        Assert.That(DecimalFloat("MultiplicadorManutencao", "Dificil"), Is.EqualTo(1.2f));
    }

    [Test]
    public void Tabela_ResolveAliasesEValoresAcimaDeInt()
    {
        MethodInfo info = ValoresType.GetMethod("TryObterPreco", BindingFlags.Public | BindingFlags.Static);
        object[] navArgs = { "NAV_GLOBAL", "Navio Global", 0L };
        Assert.That((bool)info.Invoke(null, navArgs), Is.True);
        Assert.That((long)navArgs[2], Is.EqualTo(13000000000L));
        object[] casaArgs = { "", "Casa", 0L };
        Assert.That((bool)info.Invoke(null, casaArgs), Is.True);
        Assert.That((long)casaArgs[2], Is.EqualTo(250000L));
    }

    [Test]
    public void MercadoEAgricultura_UsamTabelaOficial()
    {
        MethodInfo mercado = ValoresType.GetMethod("TryObterPrecoMercado", BindingFlags.Public | BindingFlags.Static);
        object[] mercadoArgs = { "uranio", 0L };
        Assert.That((bool)mercado.Invoke(null, mercadoArgs), Is.True);
        Assert.That((long)mercadoArgs[1], Is.EqualTo(150000L));
        MethodInfo semente = ValoresType.GetMethod("ObterCustoSemente", BindingFlags.Public | BindingFlags.Static);
        Assert.That((long)semente.Invoke(null, new object[] { "cana-de-acucar", 1L }), Is.EqualTo(100000L));
        Assert.That((long)semente.Invoke(null, new object[] { "cacau", 1L }), Is.EqualTo(350000L));
    }

    [Test]
    public void Formatador_MantemValorInternoExato()
    {
        MethodInfo formatar = ValoresType.GetMethod("FormatarDinheiro", BindingFlags.Public | BindingFlags.Static);
        Assert.That((string)formatar.Invoke(null, new object[] { 1500L }), Is.EqualTo("$1.500"));
        Assert.That((string)formatar.Invoke(null, new object[] { 1500000L }), Is.EqualTo("$1,5 mi"));
        Assert.That((string)formatar.Invoke(null, new object[] { 70000000000L }), Is.EqualTo("$70 bi"));
    }
}
#endif
