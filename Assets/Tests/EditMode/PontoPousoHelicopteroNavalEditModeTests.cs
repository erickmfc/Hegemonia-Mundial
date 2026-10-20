using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PontoPousoHelicopteroNavalEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void PontoSoHabilitaQuandoCreatyEConvésPertencemAoMesmoNavioETime()
    {
        GameObject navio = new GameObject("NavioTeste");
        try
        {
            TestObjects types = CreateTypes(navio, 2);
            Assert.That((bool)MetodoConexao(types.Ponto, types.Navio.GetType()).Invoke(types.Ponto,
                new[] { types.Navio }), Is.True);

            Assert.That((bool)types.Navio.GetType().GetMethod("PodeReservarVagaCreaty").Invoke(types.Navio,
                new object[] { types.Ponto.transform }), Is.True);
            types.Creaty.GetType().GetField("paisProprietarioTeamId", Members).SetValue(types.Creaty, 3);
            Assert.That((bool)MetodoConexao(types.Ponto, types.Navio.GetType()).Invoke(types.Ponto,
                new[] { types.Navio }), Is.False);
            Assert.That((bool)types.Navio.GetType().GetMethod("PodeReservarVagaCreaty").Invoke(types.Navio,
                new object[] { types.Ponto.transform }), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(navio);
        }
    }

    [Test]
    public void CreatyForaDaHierarquiaDoNavioNaoAutorizaPouso()
    {
        GameObject navio = new GameObject("NavioSemCreatyLocal");
        GameObject creatyObject = new GameObject("CreatyExterno");
        try
        {
            TestObjects types = CreateTypes(navio, 1, creatyObject.transform);
            Assert.That((bool)MetodoConexao(types.Ponto, types.Navio.GetType()).Invoke(types.Ponto,
                new[] { types.Navio }), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(navio);
            Object.DestroyImmediate(creatyObject);
        }
    }

    [Test]
    public void ControleNavioRealistaAceitaSomentePontoECreatyDoMesmoTime()
    {
        GameObject navio = new GameObject("NavioRealistaTeste");
        try
        {
            System.Type identidadeType = ResolveType("IdentidadeUnidade");
            Component identidade = navio.AddComponent(identidadeType);
            identidadeType.GetField("teamID", Members).SetValue(identidade, 4);
            GameObject creatyObject = new GameObject("CreatyDoNavio");
            creatyObject.transform.SetParent(navio.transform);
            System.Type creatyType = ResolveType("Hegemonia.AI.IA03.CreatyEstrategico");
            Component creaty = creatyObject.AddComponent(creatyType);
            creatyType.GetField("paisProprietarioTeamId", Members).SetValue(creaty, 4);
            GameObject pontoObject = new GameObject("VagaDoConvés");
            pontoObject.transform.SetParent(navio.transform);
            System.Type pontoType = ResolveType("PontoPousoHelicopteroNaval");
            Component ponto = pontoObject.AddComponent(pontoType);
            pontoType.GetField("creatyConectado", Members).SetValue(ponto, creaty);
            System.Type navioType = ResolveType("ControleNavioRealista");
            Component controlador = navio.AddComponent(navioType);

            Assert.That((bool)MetodoConexao(ponto, navioType).Invoke(ponto, new[] { controlador }), Is.True);
            creatyType.GetField("paisProprietarioTeamId", Members).SetValue(creaty, 3);
            Assert.That((bool)MetodoConexao(ponto, navioType).Invoke(ponto, new[] { controlador }), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(navio);
        }
    }

    private static MethodInfo MetodoConexao(Component ponto, System.Type tipoNavio)
    {
        return ponto.GetType().GetMethod("EstaConectadoAoNavio", Members, null, new[] { tipoNavio }, null);
    }

    private static TestObjects CreateTypes(GameObject navio, int teamId, Transform creatyParent = null)
    {
        System.Type identidadeType = ResolveType("IdentidadeUnidade");
        System.Type creatyType = ResolveType("Hegemonia.AI.IA03.CreatyEstrategico");
        System.Type managerType = ResolveType("GerenciadorPortaAvioes");
        System.Type pontoType = ResolveType("PontoPousoHelicopteroNaval");

        Component identidade = navio.AddComponent(identidadeType);
        identidadeType.GetField("teamID", Members).SetValue(identidade, teamId);

        GameObject creatyObject = new GameObject("CreatyConvés");
        creatyObject.transform.SetParent(creatyParent != null ? creatyParent : navio.transform);
        Component creaty = creatyObject.AddComponent(creatyType);
        creatyType.GetField("paisProprietarioTeamId", Members).SetValue(creaty, teamId);

        GameObject pontoObject = new GameObject("PousoCreaty");
        pontoObject.transform.SetParent(navio.transform);
        Component ponto = pontoObject.AddComponent(pontoType);
        pontoType.GetField("creatyConectado", Members).SetValue(ponto, creaty);

        Component manager = navio.AddComponent(managerType);
        managerType.GetMethod("InicializarPontosPousoHelicopterosCreaty", Members).Invoke(manager, null);
        return new TestObjects(manager, ponto, creaty);
    }

    private static System.Type ResolveType(string fullName)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(fullName);
            if (type != null) return type;
        }

        Assert.Fail("Não foi possível localizar o tipo " + fullName);
        return null;
    }

    private sealed class TestObjects
    {
        public readonly Component Navio;
        public readonly Component Ponto;
        public readonly Component Creaty;

        public TestObjects(Component navio, Component ponto, Component creaty)
        {
            Navio = navio;
            Ponto = ponto;
            Creaty = creaty;
        }
    }
}
