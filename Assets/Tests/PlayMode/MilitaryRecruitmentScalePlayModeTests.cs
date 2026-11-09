using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The project scripts compile into Assembly-CSharp, which test asmdefs cannot
/// reference directly. Reflection keeps this test isolated from that assembly.
/// </summary>
public sealed class MilitaryRecruitmentScalePlayModeTests
{
    [UnityTest]
    public IEnumerator NationalRecruitmentBuildsFiveHundredOnePersistentSoldierRecords()
    {
        Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefaultAssemblyContaining("SistemaMilitar");
        Assert.That(gameAssembly, Is.Not.Null, "A assembly do jogo precisa estar carregada.");

        Type clockType = gameAssembly.GetType("GerenciadorTempo");
        Type countryType = gameAssembly.GetType("DadosPaisGoverno");
        Type militaryType = gameAssembly.GetType("SistemaMilitar");
        Assert.That(clockType, Is.Not.Null);
        Assert.That(countryType, Is.Not.Null);
        Assert.That(militaryType, Is.Not.Null);

        clockType.GetMethod("GarantirInstancia", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        object clock = clockType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        Assert.That(clock, Is.Not.Null, "O relogio nacional precisa estar disponivel para testar os ciclos de recrutamento.");
        FieldInfo dayField = clockType.GetField("totalDias", BindingFlags.Public | BindingFlags.Instance);
        int originalDay = (int)dayField.GetValue(clock);

        object country = Activator.CreateInstance(countryType);
        Set(countryType, country, "teamId", 987654);
        Set(countryType, country, "nomePais", "Nacao Teste Militar");
        Set(countryType, country, "populacaoCivil", 5000);
        Set(countryType, country, "populacaoMaxima", 20000);
        Set(countryType, country, "populacao", 10000);
        Set(countryType, country, "populacaoMilitarAtiva", 0);
        Set(countryType, country, "reservistas", 0);
        Set(countryType, country, "alistaveis", 5000);
        Set(countryType, country, "felicidade", 70f);
        Set(countryType, country, "metaEfetivoMilitar", 501);
        Set(countryType, country, "recrutamentoMilitarPorDia", 40);
        Set(countryType, country, "ultimoDiaRecrutamentoMilitar", 0);

        MethodInfo process = militaryType.GetMethod("Processar", BindingFlags.Public | BindingFlags.Static);
        try
        {
            for (int day = 1; day <= 14; day++)
            {
                dayField.SetValue(clock, day);
                process.Invoke(null, new[] { country, null });
            }

            Assert.That(Get<int>(countryType, country, "populacaoMilitarAtiva"), Is.EqualTo(501));
            Assert.That(Get<int>(countryType, country, "registrosMilitaresAtivos"), Is.EqualTo(501));
            IList records = (IList)Get(countryType, country, "efetivoMilitar");
            Assert.That(records.Count, Is.EqualTo(501));
            for (int i = 0; i < records.Count; i++)
            {
                object record = records[i];
                Assert.That(record, Is.Not.Null);
                Assert.That(Get<string>(record.GetType(), record, "id"), Is.Not.Empty);
            }
        }
        finally
        {
            dayField.SetValue(clock, originalDay);
        }

        yield return null;
    }

    private static void Set(Type type, object instance, string name, object value)
    {
        type.GetField(name, BindingFlags.Public | BindingFlags.Instance).SetValue(instance, value);
    }

    private static object Get(Type type, object instance, string name)
    {
        return type.GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
    }

    private static T Get<T>(Type type, object instance, string name)
    {
        return (T)Get(type, instance, name);
    }
}

internal static class AssemblyTestExtensions
{
    public static Assembly FirstOrDefaultAssemblyContaining(this Assembly[] assemblies, string typeName)
    {
        for (int i = 0; i < assemblies.Length; i++)
            if (assemblies[i].GetType(typeName) != null) return assemblies[i];
        return null;
    }
}
