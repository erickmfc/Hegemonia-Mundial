#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AISovereignWarEscalationEditModeTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private Type governmentType;
    private Type controllerType;
    private PropertyInfo governmentInstanceProperty;
    private object previousGovernmentInstance;
    private GameObject governmentObject;
    private GameObject controllerObject;
    private Component government;
    private Component controller;
    private object playerRelation;

    [SetUp]
    public void SetUp()
    {
        governmentType = ResolveType("SistemaGovernoMundial");
        controllerType = ResolveType("Hegemonia.AI.Sovereign.AISovereignController");
        governmentInstanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
        Assert.That(governmentInstanceProperty, Is.Not.Null);
        previousGovernmentInstance = governmentInstanceProperty.GetValue(null, null);

        governmentObject = new GameObject("Test_SovereignWar_Government");
        governmentObject.SetActive(false);
        government = governmentObject.AddComponent(governmentType);
        governmentInstanceProperty.GetSetMethod(true).Invoke(null, new[] { government });
        SetField(government, "teamJogador", 1);
        SetCountries(1, 2);

        Type relationType = ResolveType("RelacaoPaisGoverno");
        playerRelation = Activator.CreateInstance(relationType);
        SetField(playerRelation, "teamA", 1);
        SetField(playerRelation, "teamB", 2);
        SetField(playerRelation, "valor", -60);
        ((IList)Field(government, "relacoes")).Add(playerRelation);

        controllerObject = new GameObject("Test_SovereignWar_Controller");
        controllerObject.SetActive(false);
        controller = controllerObject.AddComponent(controllerType);
        SetField(controller, "teamId", 2);
        Invoke(controller, "EnsureRuntimeWiring");

        object blackboard = Field(controller, "_blackboard");
        SetField(blackboard, "PlayerPressure", 1f);
        SetField(blackboard, "UnderThreat", false);
        SetField(blackboard, "AtWar", false);
        SetField(blackboard, "EnemyAnchor", Vector3.zero);
    }

    [TearDown]
    public void TearDown()
    {
        if (controllerObject != null)
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
        if (governmentObject != null)
        {
            UnityEngine.Object.DestroyImmediate(governmentObject);
        }
        if (governmentInstanceProperty != null)
        {
            governmentInstanceProperty.GetSetMethod(true).Invoke(null, new[] { previousGovernmentInstance });
        }
    }

    [Test]
    public void PressaoVisualSemImpactosConfirmadosNaoEnfileiraDeclaracaoDeGuerra()
    {
        MethodInfo evaluateWar = controllerType.GetMethod("EvaluateWar", InstanceMembers);
        Assert.That(evaluateWar, Is.Not.Null);

        evaluateWar.Invoke(controller, new object[] { Time.unscaledTime });

        object orderBus = Field(controller, "_orders");
        int pendingOrders = (int)orderBus.GetType().GetProperty("PendingCount", InstanceMembers).GetValue(orderBus, null);
        Assert.That(pendingOrders, Is.Zero,
            "A pressão visual e a relação negativa não são impactos hostis confirmados.");
        Assert.That(Field(playerRelation, "guerraDeclarada"), Is.False,
            "A crise só pode virar guerra pelo fluxo de agressões confirmadas do Governo.");
    }

    private void SetCountries(params int[] teams)
    {
        Type countryType = ResolveType("DadosPaisGoverno");
        Type listType = typeof(List<>).MakeGenericType(countryType);
        IList countries = (IList)Activator.CreateInstance(listType);
        foreach (int teamId in teams)
        {
            object country = Activator.CreateInstance(countryType);
            SetField(country, "teamId", teamId);
            SetField(country, "nomePais", "País " + teamId);
            countries.Add(country);
        }
        SetField(government, "paises", countries);
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }
        Assert.Fail("Tipo não encontrado: " + fullName);
        return null;
    }

    private static object Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceMembers);
        Assert.That(method, Is.Not.Null, "Método não encontrado: " + methodName);
        return method.Invoke(target, null);
    }

    private static object Field(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + fieldName);
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceMembers);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + fieldName);
        field.SetValue(target, value);
    }
}
#endif
