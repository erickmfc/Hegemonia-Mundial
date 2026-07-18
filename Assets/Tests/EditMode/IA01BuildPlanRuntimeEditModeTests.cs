#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA01BuildPlanRuntimeEditModeTests
{
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(created[i]);
            }
        }

        created.Clear();
    }

    [Test]
    public void ExactCapitalStep_UsesPreparedSlot_AndRestoresReservation()
    {
        GameObject controllerObject = CreateGameObject("IA01_Test");
        object controller = controllerObject.AddComponent(ResolveType("Hegemonia.AI.IA01.IA01Controller"));
        object layout = controllerObject.AddComponent(ResolveType("Hegemonia.AI.IA01.IA01CityLayout"));
        object slot = controllerObject.AddComponent(ResolveType("Hegemonia.AI.IA01.IA01BuildSlot"));
        SetPrivate(slot, "slotId", "CapitalSlot");
        SetPrivate(slot, "allowedRole", EnumValue("Hegemonia.AI.IA01.IA01StrategicRole", "Capital"));
        SetPrivate(slot, "allowedDomain", EnumValue("Hegemonia.AI.IA01.IA01BuildDomain", "Land"));

        GameObject prefab = CreateGameObject("CapitalPrefab");
        ScriptableObject capital = ScriptableObject.CreateInstance(ResolveType("DadosConstrucao"));
        created.Add(capital);
        SetPrivate(capital, "itemId", "capital.test");
        SetPrivate(capital, "nomeItem", "Capital Test");
        SetPrivate(capital, "prefabDaUnidade", prefab);
        object capabilities = EnumValue("Hegemonia.AI.BrainMaster.IA_ConstructionCapability", "Structure");
        capabilities = OrEnum(capabilities, EnumValue("Hegemonia.AI.BrainMaster.IA_ConstructionCapability", "Land"));
        capabilities = OrEnum(capabilities, EnumValue("Hegemonia.AI.BrainMaster.IA_ConstructionCapability", "Core"));
        SetPrivate(capital, "capacidades", capabilities);
        SetPrivate(capital, "strategicRole", EnumValue("Hegemonia.AI.IA01.IA01StrategicRole", "Capital"));

        ScriptableObject plan = ScriptableObject.CreateInstance(ResolveType("Hegemonia.AI.IA01.IA01BuildPlan"));
        created.Add(plan);
        Type planStepType = ResolveType("Hegemonia.AI.IA01.IA01BuildPlanStep");
        IList steps = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(planStepType));
        object step = Activator.CreateInstance(planStepType);
        SetFieldOrProperty(step, "stepId", "capital.prefeitura");
        SetFieldOrProperty(step, "constructionData", capital);
        SetFieldOrProperty(step, "requiredRole", EnumValue("Hegemonia.AI.IA01.IA01StrategicRole", "Capital"));
        SetFieldOrProperty(step, "placementMode", EnumValue("Hegemonia.AI.IA01.IA01PlacementMode", "ExactSlot"));
        SetFieldOrProperty(step, "primarySlotId", "CapitalSlot");
        SetFieldOrProperty(step, "required", true);
        SetFieldOrProperty(step, "maximumCount", 1);
        SetFieldOrProperty(step, "failurePolicy", EnumValue("Hegemonia.AI.IA01.IA01FailurePolicy", "BlockMandatoryStep"));
        object condition = Activator.CreateInstance(ResolveType("Hegemonia.AI.IA01.IA01BuildCondition"));
        SetFieldOrProperty(condition, "type", EnumValue("Hegemonia.AI.IA01.IA01BuildConditionType", "Always"));
        SetFieldOrProperty(step, "condition", condition);
        steps.Add(step);
        SetPrivate(plan, "steps", steps);

        SetPrivate(controller, "buildPlan", plan);
        SetPrivate(controller, "cityLayout", layout);
        InvokeInstance(controller, "ConfigureIdentity", 70, 7, "Test Nation");
        InvokeInstance(layout, "ConfigureOwner", 7, 70);
        InvokeInstance(layout, "RegisterSlot", slot);

        GameObject managerObject = CreateGameObject("IA01_Manager_Test");
        object manager = managerObject.AddComponent(ResolveType("Hegemonia.AI.IA01.IA01Manager"));
        InvokeInstance(manager, "RegisterController", controller);
        object runtime = GetPrivate(controller, "nationRuntime");
        object runner = GetMemberValue(runtime, "BuildPlanRuntime");

        object intent = Activator.CreateInstance(ResolveType("Hegemonia.AI.IA01.IA01Intent"));
        SetFieldOrProperty(intent, "Type", EnumValue("Hegemonia.AI.IA01.IA01IntentType", "EstablishCapital"));
        SetFieldOrProperty(intent, "Approved", true);

        object[] selectArgs = { intent, 1f, null, null, null };
        Assert.That((bool)InvokeInstance(runner, "TrySelect", selectArgs), Is.True, selectArgs[4] as string);
        Assert.That((bool)selectArgs[3], Is.True);

        object selection = selectArgs[2];
        Assert.That(GetMemberValue(selection, "Slot"), Is.SameAs(slot));
        object lot = GetMemberValue(selection, "Lot");
        Assert.That(GetMemberValue(lot, "Position"), Is.EqualTo(((Component)slot).transform.position));

        object[] reserveArgs = { selection, "build:capital.test:slot:CapitalSlot", 1f, null };
        Assert.That((bool)InvokeInstance(runner, "TryReserve", reserveArgs), Is.True, reserveArgs[3] as string);
        Assert.That(GetMemberValue(slot, "State"), Is.EqualTo(EnumValue("Hegemonia.AI.IA01.IA01BuildSlotState", "Reserved")));

        object saved = InvokeInstance(runner, "CaptureSaveState");
        InvokeInstance(slot, "Release", string.Empty, false, "test");
        InvokeInstance(runner, "RestoreSaveState", saved);
        Assert.That(GetMemberValue(slot, "State"), Is.EqualTo(EnumValue("Hegemonia.AI.IA01.IA01BuildSlotState", "Reserved")));
        Assert.That(GetMemberValue(slot, "ReservedCommandId"), Is.EqualTo("build:capital.test:slot:CapitalSlot"));

        InvokeInstance(runner, "Confirm", selection, "build:capital.test:slot:CapitalSlot", false, "transient scene failure", 1f);
        object failedState = InvokeInstance(runner, "CaptureSaveState");
        Assert.That(((IList)GetMemberValue(failedState, "blockedSteps")).Count, Is.Zero, "Uma falha transitoria nao pode bloquear a prefeitura para sempre.");

        object[] cooldownArgs = { intent, 1.5f, null, null, null };
        Assert.That((bool)InvokeInstance(runner, "TrySelect", cooldownArgs), Is.False);
        Assert.That(cooldownArgs[4] as string, Is.EqualTo("cooldown do passo ativo"));

        object[] retryArgs = { intent, 2.1f, null, null, null };
        Assert.That((bool)InvokeInstance(runner, "TrySelect", retryArgs), Is.True, retryArgs[4] as string);

        ((IList)GetMemberValue(failedState, "blockedSteps")).Add("capital.prefeitura");
        InvokeInstance(runner, "RestoreSaveState", failedState);
        object[] migratedSaveArgs = { intent, 100000f, null, null, null };
        Assert.That((bool)InvokeInstance(runner, "TrySelect", migratedSaveArgs), Is.True, "Save antigo nao pode manter a prefeitura bloqueada.");
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject result = new GameObject(name);
        created.Add(result);
        return result;
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

    private static object OrEnum(object left, object right)
    {
        Type enumType = left.GetType();
        long combined = Convert.ToInt64(left) | Convert.ToInt64(right);
        return Enum.ToObject(enumType, combined);
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo ausente: " + fieldName);
        field.SetValue(target, value);
    }

    private static object GetPrivate(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo ausente: " + fieldName);
        return field.GetValue(target);
    }

    private static object GetMemberValue(object target, string memberName)
    {
        PropertyInfo property = target.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property != null)
        {
            return property.GetValue(target);
        }

        FieldInfo field = target.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "Campo ausente: " + memberName);
        return field.GetValue(target);
    }

    private static void SetFieldOrProperty(object target, string memberName, object value)
    {
        FieldInfo field = target.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo property = target.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, "Membro ausente: " + memberName);
        property.SetValue(target, value);
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
        MethodInfo[] candidates = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < candidates.Length; i++)
        {
            MethodInfo candidate = candidates[i];
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
                ParameterInfo parameter = parameters[p];
                Type parameterType = parameter.ParameterType;

                if (parameter.IsOut)
                {
                    continue;
                }

                if (argument == null)
                {
                    if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                    {
                        matches = false;
                        break;
                    }
                }
                else if (!parameterType.IsInstanceOfType(argument))
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
}
#endif
