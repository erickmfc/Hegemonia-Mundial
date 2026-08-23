using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class OperacoesPortaAvioesV2EditModeTests
{
    private static Type Tipo(string nome) { return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(nome)).FirstOrDefault(t => t != null); }
    private static object Enumero(string nome, string valor) { return Enum.Parse(Tipo(nome), valor); }
    private static object Campo(object alvo, string nome) { return alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(alvo); }
    private static object Propriedade(object alvo, string nome) { return alvo.GetType().GetProperty(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(alvo, null); }
    private static object Chamar(object alvo, string nome, params object[] args) { return alvo.GetType().GetMethod(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(alvo, args); }

    [Test] public void IdentidadePermaneceEstavelETransicaoInvalidaFalha()
    {
        var tipo = Tipo("AeronaveEmbarcadaV2"); Assert.IsNotNull(tipo); var go = new GameObject("AeronaveV2"); var a = go.AddComponent(tipo); Chamar(a, "GarantirIdentidade"); string id = (string)Campo(Propriedade(a, "Registro"), "id");
        Assert.IsTrue((bool)Chamar(a, "TentarTransicionar", Enumero("EstadoOperacaoPortaAvioesV2", "SolicitandoPouso"), 1f, "")); Assert.IsFalse((bool)Chamar(a, "TentarTransicionar", Enumero("EstadoOperacaoPortaAvioesV2", "ArmazenadoNoHangar"), 2f, "")); Assert.AreEqual(id, (string)Campo(Propriedade(a, "Registro"), "id")); UnityEngine.Object.DestroyImmediate(go);
    }

    [Test] public void ReservaDeVagaEAtomicaELibera()
    {
        var tipo = Tipo("VagaPortaAvioesV2"); Assert.IsNotNull(tipo); var go = new GameObject("Vaga"); var vaga = go.AddComponent(tipo); tipo.GetField("id").SetValue(vaga, "V-01"); Assert.IsTrue((bool)Chamar(vaga, "Reservar", "A")); Assert.IsFalse((bool)Chamar(vaga, "Reservar", "B")); Assert.IsFalse((bool)Chamar(vaga, "Ocupar", "B")); Assert.IsTrue((bool)Chamar(vaga, "Ocupar", "A")); Chamar(vaga, "Liberar", "A"); Assert.AreEqual(Enumero("EstadoVagaPortaAvioesV2", "Livre"), tipo.GetField("estado").GetValue(vaga)); UnityEngine.Object.DestroyImmediate(go);
    }

    [Test] public void EstacionamentoNaoDesativaAeronave()
    {
        var tipo = Tipo("AeronaveEmbarcadaV2"); Assert.IsNotNull(tipo); var go = new GameObject("AeronaveVisivel"); go.AddComponent(tipo); Assert.IsTrue(go.activeSelf); UnityEngine.Object.DestroyImmediate(go);
    }
}
