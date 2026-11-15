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

    [Test]
    public void PortaAvioesSemCreatyUsaVagaExternaPreferencialERespeitaTime()
    {
        GameObject carrier = new GameObject("CarrierSemCreaty");
        GameObject heliAliadoObj = new GameObject("HeliAliado");
        GameObject heliInimigoObj = new GameObject("HeliInimigo");
        try
        {
            System.Type identidadeType = ResolveType("IdentidadeUnidade");
            Component idCarrier = carrier.AddComponent(identidadeType);
            identidadeType.GetField("teamID", Members).SetValue(idCarrier, 1);

            GameObject grupoConves = new GameObject("VagasExternas");
            grupoConves.transform.SetParent(carrier.transform);
            GameObject grupoHangar = new GameObject("VagasInternas");
            grupoHangar.transform.SetParent(carrier.transform);

            GameObject vagaCaca = new GameObject("Vaga_Conves_01");
            vagaCaca.transform.SetParent(grupoConves.transform);
            vagaCaca.transform.position = new Vector3(0f, 20f, 0f);
            System.Type vagaV2Type = ResolveType("VagaPortaAvioesV2");
            Component compVagaCaca = vagaCaca.AddComponent(vagaV2Type);
            System.Type tipoV2Enum = ResolveType("TipoAeronavePortaAvioesV2");
            vagaV2Type.GetField("tipoPermitido", Members).SetValue(compVagaCaca, System.Enum.Parse(tipoV2Enum, "Caca"));

            GameObject vagaGrande = new GameObject("Vaga_Conves_14_Grande");
            vagaGrande.transform.SetParent(grupoConves.transform);
            vagaGrande.transform.position = new Vector3(40f, 20f, 0f);
            Component compVagaGrande = vagaGrande.AddComponent(vagaV2Type);
            vagaV2Type.GetField("tipoPermitido", Members).SetValue(compVagaGrande, System.Enum.Parse(tipoV2Enum, "Qualquer"));

            GameObject vagaHangar = new GameObject("Vaga_Hangar_01");
            vagaHangar.transform.SetParent(grupoHangar.transform);
            vagaHangar.transform.position = new Vector3(80f, 5f, 0f);

            System.Type managerType = ResolveType("GerenciadorPortaAvioes");
            Component manager = carrier.AddComponent(managerType);
            managerType.GetField("patio", Members).SetValue(manager, grupoConves.transform);
            managerType.GetField("hangarAviao", Members).SetValue(manager, grupoHangar.transform);

            System.Type heliType = ResolveType("Helicoptero");
            Component idAliado = heliAliadoObj.AddComponent(identidadeType);
            identidadeType.GetField("teamID", Members).SetValue(idAliado, 1);
            Component heliAliado = heliAliadoObj.AddComponent(heliType);

            Component idInimigo = heliInimigoObj.AddComponent(identidadeType);
            identidadeType.GetField("teamID", Members).SetValue(idInimigo, 2);
            Component heliInimigo = heliInimigoObj.AddComponent(heliType);

            Assert.That((bool)managerType.GetMethod("PodeReceberHelicoptero", Members).Invoke(manager, new object[] { heliAliado }), Is.True);
            Assert.That((bool)managerType.GetMethod("PodeReceberHelicoptero", Members).Invoke(manager, new object[] { heliInimigo }), Is.False);

            Transform escolhida = (Transform)managerType.GetMethod("ObterVagaHelicopteroCarrier", Members).Invoke(manager, new object[] { heliAliado });
            Assert.That(escolhida, Is.EqualTo(vagaGrande.transform));
            Assert.That((bool)managerType.GetMethod("PodeReservarVagaCreaty", Members).Invoke(manager, new object[] { vagaGrande.transform }), Is.True);
            Assert.That((bool)managerType.GetMethod("PodeReservarVagaCreaty", Members).Invoke(manager, new object[] { vagaHangar.transform }), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(carrier);
            Object.DestroyImmediate(heliAliadoObj);
            Object.DestroyImmediate(heliInimigoObj);
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
