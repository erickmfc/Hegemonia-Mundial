using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IAConstructionAreaEditModeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string MarkerTypeName = "Hegemonia.AI.BrainMaster.IA_ManualBuildPoint";

    private static Type ResolveMarkerType()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(MarkerTypeName, false);
            if (type != null)
                return type;
        }

        Assert.Fail($"Tipo {MarkerTypeName} não foi carregado.");
        return null;
    }

    private static void SetMember(object instance, string name, object value)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name, Flags);
        if (property != null && property.CanWrite)
        {
            property.SetValue(instance, value, null);
            return;
        }

        FieldInfo field = type.GetField(name, Flags);
        Assert.That(field, Is.Not.Null, $"Membro {name} não encontrado em {type.FullName}.");
        field.SetValue(instance, value);
    }

    private static object GetMember(object instance, string name)
    {
        Type type = instance.GetType();
        PropertyInfo property = type.GetProperty(name, Flags);
        if (property != null)
            return property.GetValue(instance, null);

        FieldInfo field = type.GetField(name, Flags);
        Assert.That(field, Is.Not.Null, $"Membro {name} não encontrado em {type.FullName}.");
        return field.GetValue(instance);
    }

    private static object ParseRole(Type markerType, string roleName)
    {
        Type roleType = markerType.GetNestedType("OperationalRole", Flags);
        Assert.That(roleType, Is.Not.Null, "OperationalRole não foi encontrado.");
        return Enum.Parse(roleType, roleName);
    }

    private static Component CreateMarker(Type markerType)
    {
        GameObject gameObject = new GameObject("IA_ConstructionArea_Test");
        Component marker = (Component)gameObject.AddComponent(markerType);
        Assert.That(marker, Is.Not.Null);
        return marker;
    }

    [Test]
    public void ConstructionRolesArePlacementAreasByDefault()
    {
        Type markerType = ResolveMarkerType();
        Component marker = CreateMarker(markerType);
        try
        {
            string[] roles =
            {
                "ConstrucaoCidade",
                "ConstrucaoEnergia",
                "ConstrucaoPier",
                "ConstrucaoEstaleiro",
                "ConstrucaoPlataforma"
            };

            for (int i = 0; i < roles.Length; i++)
            {
                SetMember(marker, "ManualRole", ParseRole(markerType, roles[i]));
                SetMember(marker, "UsePlacementArea", false);
                Assert.That((bool)GetMember(marker, "UsesPlacementArea"), Is.True, roles[i]);
                string label = (string)markerType.GetMethod("GetDisplayLabel", Flags).Invoke(marker, null);
                Assert.That(label, Does.Contain("Construcao"));
                MethodInfo filtersMethod = markerType.GetMethod("GetDefaultFiltersForRole", Flags);
                Assert.That(filtersMethod, Is.Not.Null);
                IEnumerable filters = (IEnumerable)filtersMethod.Invoke(null, new[] { GetMember(marker, "ManualRole") });
                int filterCount = 0;
                foreach (object ignored in filters)
                    filterCount++;
                Assert.That(filterCount, Is.GreaterThan(0), roles[i]);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(marker.gameObject);
        }
    }

    [Test]
    public void LegacyManualPointRemainsExactUnlessAreaIsEnabled()
    {
        Type markerType = ResolveMarkerType();
        Component marker = CreateMarker(markerType);
        try
        {
            SetMember(marker, "ManualRole", ParseRole(markerType, "Nenhum"));
            SetMember(marker, "UsePlacementArea", false);
            Assert.That((bool)GetMember(marker, "UsesPlacementArea"), Is.False);

            SetMember(marker, "UsePlacementArea", true);
            Assert.That((bool)GetMember(marker, "UsesPlacementArea"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(marker.gameObject);
        }
    }
}
