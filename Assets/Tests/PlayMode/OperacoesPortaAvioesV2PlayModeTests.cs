using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class OperacoesPortaAvioesV2PlayModeTests
{
    [UnityTest] public IEnumerator AeronaveMantemIdentidadeDuranteCicloDeRegistro()
    {
        var tipo = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AeronaveEmbarcadaV2")).FirstOrDefault(t => t != null); Assert.IsNotNull(tipo); var go = new GameObject("AeronaveV2Play"); var a = go.AddComponent(tipo); tipo.GetMethod("GarantirIdentidade").Invoke(a, null); var registro = tipo.GetProperty("Registro").GetValue(a, null); string id = (string)registro.GetType().GetField("id").GetValue(registro); yield return null; var atual = tipo.GetProperty("Registro").GetValue(a, null); Assert.AreEqual(id, (string)atual.GetType().GetField("id").GetValue(atual)); Assert.IsTrue(go.activeSelf); UnityEngine.Object.Destroy(go);
    }
}
