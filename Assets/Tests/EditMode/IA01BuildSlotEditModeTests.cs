using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA01BuildSlotEditModeTests
{
    [Test]
    public void SlotGroup_SelectsOnlyCompatibleAvailableSlot()
    {
        GameObject root = new GameObject("Layout");
        try
        {
            object layout = root.AddComponent(ResolveType("Hegemonia.AI.IA01.IA01CityLayout"));
            object residential = CreateSlot(root.transform, "house-01", "Residential", "Residential", 4);
            object energy = CreateSlot(root.transform, "energy-01", "Residential", "EnergyProduction", 4);
            InvokeInstance(layout, "ConfigureOwner", 4, 40);
            InvokeInstance(layout, "RegisterSlot", residential);
            InvokeInstance(layout, "RegisterSlot", energy);

            object definition = CreateBuildDefinition("Residential", "Land", new Vector2(8f, 8f));
            object[] arguments = { "Residential", definition, null, null };

            Assert.That((bool)InvokeInstance(layout, "TryGetAvailableGroupSlot", arguments), Is.True, arguments[3] as string);
            object selected = arguments[2];
            Assert.That(selected, Is.SameAs(residential));

            object[] reserveArgs = { "build:house", 40, "house.basic", 1f, null };
            Assert.That((bool)InvokeInstance(selected, "TryReserve", reserveArgs), Is.True, reserveArgs[4] as string);

            object[] retryArgs = { "Residential", definition, null, null };
            Assert.That((bool)InvokeInstance(layout, "TryGetAvailableGroupSlot", retryArgs), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void SlotState_RoundTripsWithoutReleasingOccupiedSlot()
    {
        GameObject root = new GameObject("Layout");
        try
        {
            object layout = root.AddComponent(ResolveType("Hegemonia.AI.IA01.IA01CityLayout"));
            object slot = CreateSlot(root.transform, "capital", "Capital", "Capital", 6);
            InvokeInstance(layout, "ConfigureOwner", 6, 60);
            InvokeInstance(layout, "RegisterSlot", slot);

            object[] reserveArgs = { "build:capital", 60, "capital.prefeitura", 2f, null };
            Assert.That((bool)InvokeInstance(slot, "TryReserve", reserveArgs), Is.True, reserveArgs[4] as string);
            InvokeInstance(slot, "MarkOccupied", "build:capital", "capital.prefeitura");
            object state = InvokeInstance(slot, "CaptureSaveState");
            InvokeInstance(slot, "Release", string.Empty, false, "test");
            InvokeInstance(slot, "RestoreSaveState", state);

            Assert.That(GetMemberValue(slot, "State"), Is.EqualTo(EnumValue("Hegemonia.AI.IA01.IA01BuildSlotState", "Occupied")));
            Assert.That(GetMemberValue(slot, "ConstructedItemId"), Is.EqualTo("capital.prefeitura"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static object CreateSlot(Transform parent, string id, string group, string roleName, int owner)
    {
        GameObject go = new GameObject(id);
        go.transform.SetParent(parent);
        object slot = go.AddComponent(ResolveType("Hegemonia.AI.IA01.IA01BuildSlot"));
        SetField(slot, "slotId", id);
        SetField(slot, "slotGroupId", group);
        SetField(slot, "allowedRole", EnumValue("Hegemonia.AI.IA01.IA01StrategicRole", roleName));
        SetField(slot, "allowedDomain", EnumValue("Hegemonia.AI.IA01.IA01BuildDomain", "Land"));
        SetField(slot, "ownerTeamId", owner);
        return slot;
    }

    private static object CreateBuildDefinition(string roleName, string domainName, Vector2 footprint)
    {
        object definition = Activator.CreateInstance(ResolveType("Hegemonia.AI.IA01.IA01BuildDefinition"));
        SetProperty(definition, "StrategicRole", EnumValue("Hegemonia.AI.IA01.IA01StrategicRole", roleName));
        SetProperty(definition, "Domain", EnumValue("Hegemonia.AI.IA01.IA01BuildDomain", domainName));
        SetProperty(definition, "Footprint", footprint);
        return definition;
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidOperationException("Nao foi possivel resolver o tipo " + fullName + ".");
    }

    private static object EnumValue(string fullTypeName, string memberName)
    {
        return Enum.Parse(ResolveType(fullTypeName), memberName);
    }

    private static object InvokeInstance(object instance, string methodName, params object[] arguments)
    {
        Assert.That(instance, Is.Not.Null, "Instancia nula ao invocar " + methodName + ".");
        MethodInfo method = ResolveCompatibleMethod(instance.GetType(), methodName, arguments);
        Assert.That(method, Is.Not.Null, "Nao achei o metodo " + methodName + " em " + instance.GetType().Name + ".");
        return method.Invoke(instance, arguments);
    }

    private static MethodInfo ResolveCompatibleMethod(Type type, string methodName, object[] arguments)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo candidate = methods[i];
            if (candidate.Name != methodName)
            {
                continue;
            }

            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length != arguments.Length)
            {
                continue;
            }

            bool matches = true;
            for (int p = 0; p < parameters.Length; p++)
            {
                object argument = arguments[p];
                Type parameterType = parameters[p].ParameterType;
                if (argument == null)
                {
                    if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null && !parameters[p].IsOut)
                    {
                        matches = false;
                        break;
                    }
                }
                else if (!parameters[p].IsOut && !parameterType.IsInstanceOfType(argument))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return candidate;
            }
        }

        return null;
    }

    private static object GetMemberValue(object target, string memberName)
    {
        PropertyInfo property = target.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null)
        {
            return property.GetValue(target);
        }

        FieldInfo field = target.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "Campo nao encontrado: " + memberName);
        return field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo nao encontrado: " + name);
        field.SetValue(target, value);
    }

    private static void SetProperty(object target, string name, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, "Propriedade nao encontrada: " + name);
        property.SetValue(target, value);
    }
}
