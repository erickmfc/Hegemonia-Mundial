#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DestinoAeronaveEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
    private static object Read(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);

    [Test]
    public void DestinoManualEBaseEstacionadaSaoEstadosSeparados()
    {
        GameObject aviaoObject = new GameObject("AeronaveTeste");
        GameObject baseAtualObject = new GameObject("BaseAtual");
        GameObject destinoObject = new GameObject("BaseDestino");
        try
        {
            Type aviaoType = TypeOf("ControleAviao");
            Component aviao = aviaoObject.AddComponent(aviaoType);
            Component baseAtual = baseAtualObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            Component destino = destinoObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            aviaoType.GetMethod("RestaurarBasesSalvas").Invoke(aviao, new object[] { baseAtual, destino, true, false });
            Assert.AreSame(baseAtual, Read(aviao, "baseEstacionamentoAtual"));
            Assert.AreSame(destino, aviaoType.GetProperty("DestinoPousoEscolhido").GetValue(aviao));
            Assert.AreSame(baseAtual, Read(aviao, "aeroportoOrigem"));
            StringAssert.Contains("BaseAtual", (string)aviaoType.GetMethod("ObterResumoLocalizacao").Invoke(aviao, null));
            StringAssert.Contains("BaseDestino", (string)aviaoType.GetMethod("ObterResumoLocalizacao").Invoke(aviao, null));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(aviaoObject);
            UnityEngine.Object.DestroyImmediate(baseAtualObject);
            UnityEngine.Object.DestroyImmediate(destinoObject);
        }
    }

    [Test]
    public void DestinosManuaisRestringemEquipe()
    {
        GameObject aviaoObject = new GameObject("AeronaveTeste");
        GameObject aliadaObject = new GameObject("BaseAliada");
        GameObject inimigaObject = new GameObject("BaseInimiga");
        try
        {
            Component aviao = aviaoObject.AddComponent(TypeOf("ControleAviao"));
            Component idAviao = aviaoObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Component baseAliada = aliadaObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            Component idAliada = aliadaObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Component baseInimiga = inimigaObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            Component idInimiga = inimigaObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Type identidadeType = TypeOf("IdentidadeUnidade");
            identidadeType.GetField("teamID").SetValue(idAviao, 4);
            identidadeType.GetField("teamID").SetValue(idAliada, 4);
            identidadeType.GetField("teamID").SetValue(idInimiga, 5);
            Type aviaoType = TypeOf("ControleAviao");
            Assert.IsTrue((bool)Call(aviaoType, "BasesDaMesmaEquipe", aviao, baseAliada));
            Assert.IsFalse((bool)Call(aviaoType, "BasesDaMesmaEquipe", aviao, baseInimiga));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(aviaoObject);
            UnityEngine.Object.DestroyImmediate(aliadaObject);
            UnityEngine.Object.DestroyImmediate(inimigaObject);
        }
    }
}
#endif
