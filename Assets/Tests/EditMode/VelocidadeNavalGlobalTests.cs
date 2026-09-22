using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class VelocidadeNavalGlobalTests
{
    [Test]
    public void AplicaMultiplicadorGlobalAoDeslocamentoNaval()
    {
        Assert.That(Aplicar(10f), Is.EqualTo(15f).Within(0.001f));
    }

    [Test]
    public void ImpedeVelocidadeNulaOuInvalida()
    {
        Assert.That(Aplicar(0f), Is.EqualTo(0.15f).Within(0.001f));
        Assert.That(Aplicar(float.NaN), Is.EqualTo(0.15f).Within(0.001f));
    }

    private static float Aplicar(float velocidadeBase)
    {
        Assembly runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
        Assert.That(runtimeAssembly, Is.Not.Null, "Assembly-CSharp nao foi carregada.");

        Type runtimeType = runtimeAssembly.GetType("VelocidadeNavalGlobal", false);
        Assert.That(runtimeType, Is.Not.Null, "Tipo VelocidadeNavalGlobal nao foi carregado.");

        MethodInfo aplicar = runtimeType.GetMethod(
            "Aplicar",
            BindingFlags.Static | BindingFlags.Public);
        Assert.That(aplicar, Is.Not.Null, "Metodo VelocidadeNavalGlobal.Aplicar nao foi encontrado.");

        return (float)aplicar.Invoke(null, new object[] { velocidadeBase });
    }
}
