using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class IA03ProductionAssetContractEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [TestCase("Assets/Resources/Construcoes/Estaleiro_Naval.asset", "Estaleiro", "estaleiro")]
    [TestCase("Assets/Resources/Construcoes/Pier_marinha.asset", "PierMarinha", "pier")]
    [TestCase("Assets/Resources/Construcoes/PLataforma.asset", "PlataformaOffshore", "plataforma")]
    [TestCase("Assets/Prefabs/Aeroporto/Aeroporto militar.asset", "GerenciadorAeroporto", "aeroporto militar")]
    [TestCase("Assets/Prefabs/Aeroporto/Aeroporto comercial/Aeroporto comercial.asset", "GerenciadorAeroportoComercial", "aeroporto comercial")]
    [TestCase("Assets/Resources/Construcoes/F16B.asset", "ControleAviao", "f16")]
    [TestCase("Assets/Resources/Construcoes/F200.asset", "ControleNavioRealista", "f200")]
    [TestCase("Assets/Resources/Construcoes/Ministral.asset", "NavioTransporteTropas", "navio transporte de tropas")]
    [TestCase("Assets/Prefabs/Marinha/Navio de carga.asset", "NavioCargaMercado", "cargueiro")]
    [TestCase("Assets/Prefabs/Marinha/Nac_Petroleo.asset", "NavioPetroleiro", "petroleiro")]
    public void IA03ProductionFichaReferencesItsRealPrefabAndRequiredComponent(
        string assetPath,
        string requiredComponentName,
        string lookupToken)
    {
        ScriptableObject ficha = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
        Assert.That(ficha, Is.Not.Null, "Ficha de produção ausente: " + assetPath);

        Type fichaType = ResolveType("DadosConstrucao");
        Assert.That(fichaType, Is.Not.Null, "O tipo DadosConstrucao precisa estar compilado.");
        Assert.That(fichaType.IsInstanceOfType(ficha), Is.True,
            "O asset precisa usar a ficha aceita pelo catálogo IA: " + assetPath);

        object[] prefabResult = { null };
        MethodInfo tryGetPrefab = fichaType.GetMethod("TryGetPrefab", Members);
        Assert.That(tryGetPrefab, Is.Not.Null);
        Assert.That((bool)tryGetPrefab.Invoke(ficha, prefabResult), Is.True,
            "A ficha precisa validar e devolver seu prefab: " + assetPath);

        GameObject prefab = prefabResult[0] as GameObject;
        Assert.That(prefab, Is.Not.Null, "A ficha não aponta para um prefab de unidade: " + assetPath);
        Type requiredComponentType = ResolveType(requiredComponentName);
        Assert.That(requiredComponentType, Is.Not.Null,
            "Componente esperado ausente no Assembly-CSharp: " + requiredComponentName);
        Assert.That(prefab.GetComponentInChildren(requiredComponentType, true), Is.Not.Null,
            "O prefab precisa conter o componente de gameplay " + requiredComponentName + ": " + assetPath);

        string catalogIdentity = string.Join(" ", new[]
        {
            ReadText(ficha, "NomeItem"),
            ReadText(ficha, "itemId"),
            ReadText(ficha, "aliases")
        });
        Assert.That(catalogIdentity.IndexOf(lookupToken, StringComparison.OrdinalIgnoreCase), Is.GreaterThanOrEqualTo(0),
            "O nome, ID ou alias precisa permitir que o catálogo IA encontre " + lookupToken + ": " + assetPath);

        Type backendType = ResolveType("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Assert.That(backendType, Is.Not.Null, "O resolvedor real do catálogo IA precisa estar compilado.");
        object backend = Activator.CreateInstance(backendType, new object[] { 2 });
        MethodInfo addCatalogItem = backendType.GetMethod("AddCatalogItem", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(addCatalogItem, Is.Not.Null, "O backend precisa indexar as fichas reais do catálogo.");
        addCatalogItem.Invoke(backend, new object[] { ficha });

        MethodInfo findFirstAvailable = backendType.GetMethod("FindFirstAvailable", Members);
        Assert.That(findFirstAvailable, Is.Not.Null, "A busca usada pela fila de produção precisa estar disponível.");
        object resolvedFicha = findFirstAvailable.Invoke(backend, new object[] { new[] { lookupToken } });
        Assert.That(resolvedFicha, Is.SameAs(ficha),
            "A busca real da IA precisa resolver " + lookupToken + " para a ficha esperada: " + assetPath);
    }

    [Test]
    public void IA03GenericPlatformAliasResolvesOffshorePlatformInsteadOfICBMLauncher()
    {
        ScriptableObject launcher = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
            "Assets/Prefabs/Missiles/ICBM/LAncador.asset");
        ScriptableObject offshorePlatform = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
            "Assets/Resources/Construcoes/PLataforma.asset");
        Assert.That(launcher, Is.Not.Null, "A ficha real do lançador ICBM precisa estar disponível.");
        Assert.That(offshorePlatform, Is.Not.Null, "A ficha real da plataforma offshore precisa estar disponível.");

        Type backendType = ResolveType("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        Assert.That(backendType, Is.Not.Null, "O resolvedor real do catálogo IA precisa estar compilado.");
        object backend = Activator.CreateInstance(backendType, new object[] { 2 });
        MethodInfo addCatalogItem = backendType.GetMethod("AddCatalogItem", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo findFirstAvailable = backendType.GetMethod("FindFirstAvailable", Members);
        Assert.That(addCatalogItem, Is.Not.Null);
        Assert.That(findFirstAvailable, Is.Not.Null);

        addCatalogItem.Invoke(backend, new object[] { launcher });
        addCatalogItem.Invoke(backend, new object[] { offshorePlatform });

        object resolvedFicha = findFirstAvailable.Invoke(backend, new object[] { new[] { "plataforma" } });
        Assert.That(resolvedFicha, Is.SameAs(offshorePlatform),
            "O alias plataforma deve identificar a ficha com PlataformaOffshore, mesmo quando a descrição do lançador menciona uma plataforma.");
    }

    private static Type ResolveType(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
    }

    private static string ReadText(object target, string memberName)
    {
        PropertyInfo property = target.GetType().GetProperty(memberName, Members);
        if (property != null)
            return Convert.ToString(property.GetValue(target, null)) ?? string.Empty;

        FieldInfo field = target.GetType().GetField(memberName, Members);
        return field != null ? Convert.ToString(field.GetValue(target)) ?? string.Empty : string.Empty;
    }
}
