using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class IA03MissionQueuePlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator StrategicMissionQueueDeduplicatesByStableIdAndKeepsUrgentPriority()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolveType("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type conflictType = ResolveType("Hegemonia.AI.IA03.IA03NivelConflito");
        Type missionKindType = ResolveType("Hegemonia.AI.IA03.IA03TipoMissao");
        Type domainType = ResolveType("Hegemonia.AI.IA03.IA03DominioEstrategico");
        Assert.That(strategistType, Is.Not.Null);
        Assert.That(missionType, Is.Not.Null);

        var objects = new List<UnityEngine.Object>();
        GameObject host = new GameObject("IA03 mission queue isolated fixture");
        host.SetActive(false);
        objects.Add(host);
        var missions = new List<ScriptableObject>();

        try
        {
            Component strategist = host.AddComponent(strategistType);
            SetField(strategist, "nivelDeConflito", Enum.Parse(conflictType, "Tensao"));

            ScriptableObject routine = CreateMission(
                missionType, conflictType, missionKindType, domainType,
                "QUEUE-ROUTINE", "Patrulha rotineira", 20);
            ScriptableObject high = CreateMission(
                missionType, conflictType, missionKindType, domainType,
                "QUEUE-HIGH", "Defesa prioritaria", 80);
            ScriptableObject urgent = CreateMission(
                missionType, conflictType, missionKindType, domainType,
                "QUEUE-URGENT", "Defesa urgente", 1);
            ScriptableObject duplicateId = CreateMission(
                missionType, conflictType, missionKindType, domainType,
                "QUEUE-HIGH", "Mesmo pedido duplicado", 999);
            missions.Add(routine);
            missions.Add(high);
            missions.Add(urgent);
            missions.Add(duplicateId);

            MethodInfo request = strategistType.GetMethod("SolicitarMissaoEstrategica", Members);
            Assert.That(request, Is.Not.Null);

            Assert.That(Convert.ToBoolean(request.Invoke(
                strategist, new object[] { routine, "teste de fila", false })), Is.True);
            Assert.That(Convert.ToBoolean(request.Invoke(
                strategist, new object[] { high, "teste de fila", false })), Is.True);

            IList queue = (IList)GetField(strategist, "filaMissoes");
            Assert.That(queue, Is.Not.Null);
            Assert.That(queue.Count, Is.EqualTo(2),
                "Duas missões com chaves diferentes devem ocupar a fila uma vez cada.");
            Assert.That(queue[0], Is.SameAs(high),
                "A prioridade configurada deve ordenar a fila antes da missão rotineira.");

            bool duplicateAccepted = Convert.ToBoolean(request.Invoke(
                strategist, new object[] { duplicateId, "duplicata do mesmo IdMissao", false }));
            Assert.That(duplicateAccepted, Is.False,
                "Uma missão com o mesmo IdMissao não pode ser enfileirada novamente.");
            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue[0], Is.SameAs(high));

            bool urgentAccepted = Convert.ToBoolean(request.Invoke(
                strategist, new object[] { urgent, "estaleiro sob ataque", true }));
            Assert.That(urgentAccepted, Is.True);
            Assert.That(queue.Count, Is.EqualTo(3));
            Assert.That(queue[0], Is.SameAs(urgent),
                "Uma solicitação urgente deve passar à frente sem apagar as demais missões.");
            Assert.That(queue[1], Is.SameAs(high));
            Assert.That(queue[2], Is.SameAs(routine));
        }
        finally
        {
            if (host != null)
            {
                host.SetActive(false);
                UnityEngine.Object.Destroy(host);
            }

            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i] != null)
                {
                    UnityEngine.Object.Destroy(missions[i]);
                }
            }
        }

        yield return null;
    }

    private static ScriptableObject CreateMission(
        Type missionType,
        Type conflictType,
        Type missionKindType,
        Type domainType,
        string id,
        string name,
        int priority)
    {
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        SetField(mission, "idMissao", id);
        SetField(mission, "nomeMissao", name);
        SetField(mission, "nivelMenosGrave", Enum.Parse(conflictType, "Tensao"));
        SetField(mission, "nivelMaisGrave", Enum.Parse(conflictType, "GuerraTotal"));
        SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "Patrulha"));
        SetField(mission, "dominio", Enum.Parse(domainType, "Terrestre"));
        SetField(mission, "prioridade", priority);
        return mission;
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

    private static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo ausente: " + name);
        return field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo ausente: " + name);
        field.SetValue(target, value);
    }
}
