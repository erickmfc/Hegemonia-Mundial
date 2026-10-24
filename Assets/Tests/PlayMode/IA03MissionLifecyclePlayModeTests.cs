using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class IA03MissionLifecyclePlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator ExpiredMissionReleasesCreatyReservationAndClearsActiveGroup()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type missionType = ResolveType("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type creatyType = ResolveType("Hegemonia.AI.IA03.CreatyEstrategico");
        Type conflictType = ResolveType("Hegemonia.AI.IA03.IA03NivelConflito");
        Type missionKindType = ResolveType("Hegemonia.AI.IA03.IA03TipoMissao");
        Type domainType = ResolveType("Hegemonia.AI.IA03.IA03DominioEstrategico");
        Type conditionType = ResolveType("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Assert.That(strategistType, Is.Not.Null);
        Assert.That(brainType, Is.Not.Null);
        Assert.That(missionType, Is.Not.Null);
        Assert.That(creatyType, Is.Not.Null);

        var objects = new List<GameObject>();
        var assets = new List<ScriptableObject>();
        GameObject host = new GameObject("IA03 expired mission fixture");
        host.SetActive(false);
        objects.Add(host);
        GameObject creatyObject = new GameObject("IA03 expired mission Creaty");
        creatyObject.SetActive(false);
        objects.Add(creatyObject);
        GameObject unit = new GameObject("IA03 expired mission unit");
        objects.Add(unit);

        try
        {
            Component brain = host.AddComponent(brainType);
            SetField(brain, "TeamId", 1);
            SetField(brain, "IntegrationMode", Enum.Parse(
                brainType.GetField("IntegrationMode").FieldType, "Hybrid"));
            Component strategist = host.AddComponent(strategistType);
            Component creaty = creatyObject.AddComponent(creatyType);
            SetField(creaty, "id", "IA03-EXPIRED-TEST-" + creatyObject.GetInstanceID());

            ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
            assets.Add(mission);
            SetField(mission, "idMissao", "EXPIRED-IA03-TEST");
            SetField(mission, "nomeMissao", "Missão expirada de teste");
            SetField(mission, "nivelMenosGrave", Enum.Parse(conflictType, "Tensao"));
            SetField(mission, "nivelMaisGrave", Enum.Parse(conflictType, "GuerraTotal"));
            SetField(mission, "tipoMissao", Enum.Parse(missionKindType, "Patrulha"));
            SetField(mission, "dominio", Enum.Parse(domainType, "Terrestre"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(conditionType, "ConfirmacaoExterna"));
            SetField(mission, "condicaoDeFracasso", Enum.Parse(conditionType, "ConfirmacaoExterna"));
            SetField(mission, "tempoMaximoSegundos", 1f);

            SetField(strategist, "brain", brain);
            SetField(strategist, "nivelDeConflito", Enum.Parse(conflictType, "ConflitoLimitado"));
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "equipeAlvoAtiva", 2);
            SetField(strategist, "unidadesReservadas", 1);
            SetField(strategist, "unidadesOriginaisNaMissao", 1);
            SetField(strategist, "inicioMissaoEm", Time.time - 2f);
            ((IList)GetField(strategist, "unidadesAtivasNaMissao")).Add(unit);

            creatyObject.SetActive(true);
            Assert.That(Convert.ToBoolean(creatyType.GetMethod("TentarReservar").Invoke(
                creaty, new object[] { 1, 2, 1 })), Is.True);

            MethodInfo process = strategistType.GetMethod("ProcessarMissaoAtiva", Members);
            Assert.That(process, Is.Not.Null);
            process.Invoke(strategist, new object[] { Time.time });

            Assert.That(GetProperty(strategist, strategistType, "EstadoDaMissao").ToString(),
                Is.EqualTo("Expirada"));
            Assert.That(GetField(strategist, "missaoAtiva"), Is.Null);
            Assert.That(GetField(strategist, "creatyAtivo"), Is.Null);
            Assert.That(Convert.ToInt32(GetField(strategist, "unidadesReservadas")), Is.EqualTo(0));
            Assert.That(Convert.ToInt32(creatyType.GetProperty("UnidadesReservadas").GetValue(creaty)),
                Is.EqualTo(0));
            Assert.That(((IList)GetField(strategist, "unidadesAtivasNaMissao")).Count, Is.EqualTo(0));
        }
        finally
        {
            if (host != null)
            {
                host.SetActive(false);
                UnityEngine.Object.Destroy(host);
            }

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.Destroy(objects[i]);
                }
            }

            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null)
                {
                    UnityEngine.Object.Destroy(assets[i]);
                }
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator ExternalMissionConfirmationRequiresMatchingIdAndConfiguredCondition()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolveType("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type conditionType = ResolveType("Hegemonia.AI.IA03.IA03CondicaoMissao");
        var objects = new List<GameObject>();
        var assets = new List<ScriptableObject>();
        GameObject host = new GameObject("IA03 external confirmation fixture");
        host.SetActive(false);
        objects.Add(host);

        try
        {
            Component strategist = host.AddComponent(strategistType);
            ScriptableObject external = ScriptableObject.CreateInstance(missionType);
            assets.Add(external);
            SetField(external, "idMissao", "EXTERNAL-IA03-TEST");
            SetField(external, "nomeMissao", "Confirmação externa de teste");
            SetField(external, "condicaoDeSucesso", Enum.Parse(conditionType, "ConfirmacaoExterna"));
            SetField(external, "condicaoDeFracasso", Enum.Parse(conditionType, "ConfirmacaoExterna"));
            SetField(strategist, "missaoAtiva", external);

            MethodInfo register = strategistType.GetMethod("RegistrarResultadoMissaoExterno", Members);
            Assert.That(register, Is.Not.Null);
            Assert.That(Convert.ToBoolean(register.Invoke(
                strategist, new object[] { "ID-INEXISTENTE", true, "id errado" })), Is.False);
            Assert.That(Convert.ToBoolean(register.Invoke(
                strategist, new object[] { "EXTERNAL-IA03-TEST", true, "radar destruído" })), Is.True);
            Assert.That(Convert.ToBoolean(GetField(strategist, "confirmacaoExternaDeSucesso")), Is.True);
            Assert.That(Convert.ToBoolean(GetField(strategist, "confirmacaoExternaDeFracasso")), Is.False);
            Assert.That(GetField(strategist, "motivoConfirmacaoExterna").ToString(),
                Is.EqualTo("radar destruído"));
        }
        finally
        {
            if (host != null)
            {
                host.SetActive(false);
                UnityEngine.Object.Destroy(host);
            }

            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null)
                {
                    UnityEngine.Object.Destroy(assets[i]);
                }
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator MissionCompletionIsPublishedToGovernmentNews()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type missionType = ResolveType("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type resultType = ResolveType("Hegemonia.AI.IA03.IA03ResultadoMissao");
        Assert.That(governmentType, Is.Not.Null);
        Assert.That(strategistType, Is.Not.Null);
        Assert.That(missionType, Is.Not.Null);
        Assert.That(resultType, Is.Not.Null);

        PropertyInfo instance = governmentType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(instance, Is.Not.Null);
        object previousGovernment = instance.GetValue(null, null);
        GameObject governmentObject = new GameObject("IA03 mission news government fixture");
        governmentObject.SetActive(false);
        GameObject strategistObject = new GameObject("IA03 mission news strategist fixture");
        strategistObject.SetActive(false);
        Component government = null;
        Component strategist = null;
        ScriptableObject mission = null;
        EventInfo newsEvent = null;
        Action<string> newsHandler = null;
        string publishedNews = null;

        try
        {
            government = governmentObject.AddComponent(governmentType);
            instance.GetSetMethod(true).Invoke(null, new[] { government });
            strategist = strategistObject.AddComponent(strategistType);
            mission = ScriptableObject.CreateInstance(missionType);
            SetField(mission, "nomeMissao", "Missão de comunicação IA03");
            SetField(strategist, "missaoAtiva", mission);

            newsEvent = governmentType.GetEvent("OnNoticia", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(newsEvent, Is.Not.Null);
            newsHandler = message => publishedNews = message;
            newsEvent.AddEventHandler(government, newsHandler);

            MethodInfo finalize = strategistType.GetMethod("FinalizarMissaoInterno", Members);
            Assert.That(finalize, Is.Not.Null);
            finalize.Invoke(strategist, new object[]
            {
                Enum.Parse(resultType, "Sucesso"), "objetivo validado", true
            });

            Assert.That(publishedNews, Is.Not.Null);
            Assert.That(publishedNews, Does.Contain("IA03"));
            Assert.That(publishedNews, Does.Contain("Missão de comunicação IA03"));
            Assert.That(publishedNews, Does.Contain("objetivo validado"));
        }
        finally
        {
            if (government != null && newsEvent != null && newsHandler != null)
                newsEvent.RemoveEventHandler(government, newsHandler);
            if (instance.GetSetMethod(true) != null)
                instance.GetSetMethod(true).Invoke(null, new[] { previousGovernment });
            if (strategistObject != null) UnityEngine.Object.Destroy(strategistObject);
            if (governmentObject != null) UnityEngine.Object.Destroy(governmentObject);
            if (mission != null) UnityEngine.Object.Destroy(mission);
        }

        yield return null;
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

    private static object GetProperty(object target, Type type, string name)
    {
        PropertyInfo property = type.GetProperty(name, Members);
        Assert.That(property, Is.Not.Null, "Propriedade ausente: " + name);
        return property.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo ausente: " + name);
        field.SetValue(target, value);
    }
}
