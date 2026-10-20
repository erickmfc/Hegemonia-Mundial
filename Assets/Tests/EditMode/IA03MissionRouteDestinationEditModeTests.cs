#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA03MissionRouteDestinationEditModeTests
{
    [TestCase("InvasaoAnfibia", "ConflitoLimitado", "EmbarqueTransporte", "DesembarqueAnfibio", "exigeTransporteNaval")]
    [TestCase("VisitaPresidencial", "Tensao", "HelipontoDiplomatico", "RecepcaoPresidencial", "exigePresidente")]
    public void MissaoAceitaDestinoEncadeadoSemTransformaLoEmPontoInicialPermitido(
        string nomeMissao,
        string nivel,
        string tipoInicial,
        string tipoDestino,
        string requisito)
    {
        Type missionType = Resolve("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type missionKindType = Resolve("Hegemonia.AI.IA03.IA03TipoMissao");
        Type levelType = Resolve("Hegemonia.AI.IA03.IA03NivelConflito");
        Type creatyType = Resolve("Hegemonia.AI.IA03.IA03TipoCreaty");
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);

        try
        {
            Set(mission, "tipoMissao", Enum.Parse(missionKindType, nomeMissao));
            Set(mission, "nivelMenosGrave", Enum.Parse(levelType, nivel));
            Set(mission, "nivelMaisGrave", Enum.Parse(levelType, nomeMissao == "InvasaoAnfibia" ? "GuerraTotal" : "Tensao"));
            Set(mission, requisito, true);

            Type allowedListType = typeof(List<>).MakeGenericType(creatyType);
            IList allowedStartPoints = (IList)Activator.CreateInstance(allowedListType);
            object start = Enum.Parse(creatyType, tipoInicial);
            allowedStartPoints.Add(start);
            Set(mission, "creatysPermitidos", allowedStartPoints);

            MethodInfo accepts = missionType.GetMethod("Aceita", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(accepts, Is.Not.Null);
            Assert.That(accepts.Invoke(mission, new[] { Enum.Parse(levelType, nivel), start }), Is.EqualTo(true));
            Assert.That(
                accepts.Invoke(mission, new[] { Enum.Parse(levelType, nivel), Enum.Parse(creatyType, tipoDestino) }),
                Is.EqualTo(true),
                "O destino seguinte deve passar a validação sem ser adicionado à lista de pontos iniciais.");
            Assert.That(
                accepts.Invoke(mission, new[] { Enum.Parse(levelType, nivel), Enum.Parse(creatyType, "PontoGenerico") }),
                Is.EqualTo(false),
                "A exceção de rota deve aceitar somente o destino específico dessa categoria.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mission);
        }
    }

    private static Type Resolve(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
    }

    private static void Set(object target, string fieldName, object value)
    {
        Type type = target is UnityEngine.Object unityObject
            ? unityObject.GetType()
            : target.GetType();
        FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing serialized field: " + fieldName);
        field.SetValue(target, value);
    }
}
#endif
