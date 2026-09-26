using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class OperacoesPortaAvioesV2PlayModeTests
{
    [UnityTest]
    public IEnumerator PortaAvioesRegistraAeronaveEmVooEConcluiPousoNoConves()
    {
        Type managerType = ResolveType("GerenciadorOperacoesPortaAvioesV2");
        Type layoutType = ResolveType("LayoutConvesPortaAvioesV2");
        Type vagaType = ResolveType("VagaPortaAvioesV2");
        Type aircraftType = ResolveType("ControleAviao");
        Type embeddedType = ResolveType("AeronaveEmbarcadaV2");
        Type stateType = ResolveType("ControleAviao+EstadoAviao");

        GameObject carrier = new GameObject("PortaAvioesV2EndToEnd");
        GameObject layoutObject = new GameObject("LayoutConvesV2EndToEnd");
        GameObject aircraftObject = new GameObject("AeronaveConvesV2EndToEnd");
        GameObject berthObject = new GameObject("VagaConvesV2EndToEnd");
        GameObject landingPoint = new GameObject("PontoPousoV2EndToEnd");

        try
        {
            Component layout = layoutObject.AddComponent(layoutType);
            Component berth = berthObject.AddComponent(vagaType);
            SetField(vagaType, berth, "id", "Conves_01");
            SetField(vagaType, berth, "tamanhoMaximo", 24f);
            berthObject.transform.position = Vector3.zero;
            landingPoint.transform.position = Vector3.zero;
            ((IList)ReadField(layoutType, layout, "pontosPouso")).Add(landingPoint.transform);
            ((IList)ReadField(layoutType, layout, "vagasConves")).Add(berth);

            Component aircraft = aircraftObject.AddComponent(aircraftType);
            aircraftObject.AddComponent(embeddedType);
            SetField(aircraftType, aircraft, "estadoAtual", Enum.Parse(stateType, "EmMissao"));
            SetField(aircraftType, aircraft, "estaEmModoVooFisico", true);
            yield return null;

            Component manager = carrier.AddComponent(managerType);
            SetField(managerType, manager, "layout", layout);
            SetField(managerType, manager, "usarSistemaOperacoesV2", true);
            SetField(managerType, manager, "velocidadeTaxi", 120f);
            SetField(managerType, manager, "velocidadeAproximacao", 120f);
            SetField(managerType, manager, "duracaoServicoAposPouso", 0f);
            yield return null;

            MethodInfo preparar = managerType.GetMethod("PrepararAeronaveParaMenu", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo solicitar = managerType.GetMethod("TrySolicitarPouso", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(preparar, Is.Not.Null);
            Assert.That(solicitar, Is.Not.Null);
            object aeronaveRegistro = preparar.Invoke(manager, new object[] { aircraft, false });
            Assert.That(aeronaveRegistro, Is.Not.Null);
            object registro = aeronaveRegistro.GetType().GetProperty("Registro").GetValue(aeronaveRegistro, null);
            Assert.That(registro, Is.Not.Null);
            Assert.That(solicitar.Invoke(manager, new object[] { aircraft }), Is.EqualTo(true));

            float limite = Time.time + 3f;
            while (Time.time < limite && ReadField(registro.GetType(), registro, "estado").ToString() != "ProntoNoConves")
                yield return null;

            Assert.That(ReadField(registro.GetType(), registro, "estado").ToString(), Is.EqualTo("ProntoNoConves"));
            Assert.That(ReadField(vagaType, berth, "estado").ToString(), Is.EqualTo("Ocupada"));
        }
        finally
        {
            UnityEngine.Object.Destroy(carrier);
            UnityEngine.Object.Destroy(layoutObject);
            UnityEngine.Object.Destroy(aircraftObject);
            UnityEngine.Object.Destroy(berthObject);
            UnityEngine.Object.Destroy(landingPoint);
        }
    }

    [UnityTest]
    public IEnumerator AeronaveMantemIdentidadeDuranteCicloDeRegistro()
    {
        Type tipo = ResolveType("AeronaveEmbarcadaV2");
        GameObject go = new GameObject("AeronaveV2Play");
        try
        {
            Component aeronave = go.AddComponent(tipo);
            tipo.GetMethod("GarantirIdentidade", BindingFlags.Instance | BindingFlags.Public).Invoke(aeronave, null);
            object registro = tipo.GetProperty("Registro").GetValue(aeronave, null);
            string id = (string)registro.GetType().GetField("id").GetValue(registro);
            yield return null;
            object atual = tipo.GetProperty("Registro").GetValue(aeronave, null);
            Assert.AreEqual(id, (string)atual.GetType().GetField("id").GetValue(atual));
            Assert.IsTrue(go.activeSelf);
        }
        finally
        {
            UnityEngine.Object.Destroy(go);
        }
    }

    private static Type ResolveType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Tipo não encontrado: " + name);
        return type;
    }

    private static object ReadField(Type type, object target, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + name);
        return field.GetValue(target);
    }

    private static void SetField(Type type, object target, string name, object value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + name);
        field.SetValue(target, value);
    }
}
