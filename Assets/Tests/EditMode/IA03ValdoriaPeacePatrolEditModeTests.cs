#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA03ValdoriaPeacePatrolEditModeTests
{
    [Test]
    public void PazAceitaSomentePatrulhaExplicitamenteConfigurada()
    {
        Type missionType = ResolveType("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type levelType = ResolveType("Hegemonia.AI.IA03.IA03NivelConflito");
        Type missionKindType = ResolveType("Hegemonia.AI.IA03.IA03TipoMissao");
        Type orderType = ResolveType("Hegemonia.AI.IA03.IA03TipoOrdem");
        Type creatyType = ResolveType("Hegemonia.AI.IA03.IA03TipoCreaty");
        Assert.That(missionType, Is.Not.Null);
        Assert.That(levelType, Is.Not.Null);
        Assert.That(missionKindType, Is.Not.Null);
        Assert.That(orderType, Is.Not.Null);
        Assert.That(creatyType, Is.Not.Null);

        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        try
        {
            SetField(mission, "nivelMenosGrave", Enum.Parse(levelType, "Tensao"));
            SetField(mission, "nivelMaisGrave", Enum.Parse(levelType, "GuerraTotal"));
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "Patrulha"));
            SetField(mission, "tipoOrdem", Enum.Parse(orderType, "Patrulhar"));
            SetField(mission, "permitePatrulhaEmPaz", true);

            object airPatrol = Enum.Parse(creatyType, "PatrulhaAerea");
            Type listType = typeof(List<>).MakeGenericType(creatyType);
            var allowedCreatys = (System.Collections.IList)Activator.CreateInstance(listType);
            allowedCreatys.Add(airPatrol);
            SetField(mission, "creatysPermitidos", allowedCreatys);

            MethodInfo accepts = missionType.GetMethod("Aceita", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(accepts, Is.Not.Null);
            object peace = Enum.Parse(levelType, "Paz");
            Assert.That(accepts.Invoke(mission, new[] { peace, airPatrol }), Is.EqualTo(true));

            SetField(mission, "permitePatrulhaEmPaz", false);
            Assert.That(accepts.Invoke(mission, new[] { peace, airPatrol }), Is.EqualTo(false));

            SetField(mission, "permitePatrulhaEmPaz", true);
            SetField(mission, "tipoOrdem", Enum.Parse(orderType, "Atacar"));
            Assert.That(accepts.Invoke(mission, new[] { peace, airPatrol }), Is.EqualTo(false));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mission);
        }
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

        return null;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + name);
        field.SetValue(target, value);
    }
}
#endif
