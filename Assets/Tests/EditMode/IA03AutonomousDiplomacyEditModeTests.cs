#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA03AutonomousDiplomacyEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> identityObjects = new List<GameObject>();
    private GameObject governmentObject;
    private Type governmentType;
    private Type countryType;
    private Type relationType;
    private Type proposalType;
    private Type identityType;
    private IList identities;
    private object[] previousIdentities;
    private object government;
    private IList countries;
    private IList relations;
    private IList proposals;

    [SetUp]
    public void SetUp()
    {
        governmentType = FindType("SistemaGovernoMundial");
        countryType = FindType("DadosPaisGoverno");
        relationType = FindType("RelacaoPaisGoverno");
        proposalType = FindType("PropostaInternacional");
        identityType = FindType("IdentidadeIA");
        FieldInfo identitiesField = identityType.GetField("TodasIdentidades", Members);
        identities = (IList)identitiesField.GetValue(null);
        previousIdentities = identities.Cast<object>().ToArray();
        identities.Clear();

        governmentObject = new GameObject("Teste_IA03_Diplomacia_Autonomica");
        governmentObject.SetActive(false);
        government = governmentObject.AddComponent(governmentType);
        Set(government, "teamJogador", 1);
        countries = GetList(government, "paises");
        relations = GetList(government, "relacoes");
        proposals = GetList(government, "propostas");
        countries.Clear();
        relations.Clear();
        proposals.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < identityObjects.Count; i++)
        {
            if (identityObjects[i] != null) UnityEngine.Object.DestroyImmediate(identityObjects[i]);
        }
        identityObjects.Clear();
        if (governmentObject != null) UnityEngine.Object.DestroyImmediate(governmentObject);
        identities.Clear();
        foreach (object identity in previousIdentities) identities.Add(identity);
    }

    [Test]
    public void CountriesNegotiateWithoutPlayerAndResolveDefenseRequestIndividually()
    {
        AddCountry(1, "Jogador");
        object valdoria = AddCountry(2, "Valdoria", "CooperacaoGlobal", military: 70, balance: 20000000L, stability: 80f, diplomacy: 60);
        object karsovia = AddCountry(3, "Karsovia", "AliancaDefesa", military: 50, balance: 20000000L, stability: 70f, diplomacy: 60);
        object militaryAlly = AddCountry(4, "Aliado militar", "AliancaDefesa", military: 80, balance: 20000000L, stability: 80f, diplomacy: 60);
        object sanctionAlly = AddCountry(5, "Aliado sancionador", "AliancaDefesa", military: 15, balance: 1000L, stability: 55f, diplomacy: 50);
        object refusingAlly = AddCountry(6, "Aliado neutro", "AliancaDefesa", military: 10, balance: 1000L, stability: 55f, diplomacy: 10);

        for (int teamId = 2; teamId <= 6; teamId++) AddIdentity(teamId);
        object defenderRelation = Relation(4, 3);
        Set(defenderRelation, "valor", 30);
        object sanctionRelation = Relation(5, 2);
        Set(sanctionRelation, "valor", -50);
        Relation(5, 3);
        Relation(6, 2);
        Relation(6, 3);

        MethodInfo recordAggression = governmentType.GetMethod("RegistrarAgressao", Members);
        recordAggression.Invoke(government, new object[] { 3, 2 });
        recordAggression.Invoke(government, new object[] { 3, 2 });
        recordAggression.Invoke(government, new object[] { 3, 2 });

        ProcessDiplomacy();

        object militaryRequest = FindSupportRequest(4);
        object sanctionsRequest = FindSupportRequest(5);
        object refusalRequest = FindSupportRequest(6);
        Assert.That(militaryRequest, Is.Not.Null);
        Assert.That(sanctionsRequest, Is.Not.Null);
        Assert.That(refusalRequest, Is.Not.Null);
        Assert.That(Get<object>(militaryRequest, "respostaApoio").ToString(), Is.EqualTo("ApoioMilitar"));
        Assert.That(Get<object>(militaryRequest, "status").ToString(), Is.EqualTo("Executada"));
        Assert.That(Get<object>(sanctionsRequest, "respostaApoio").ToString(), Is.EqualTo("Sancoes"));
        Assert.That(Get<object>(sanctionsRequest, "status").ToString(), Is.EqualTo("Executada"));
        Assert.That(Get<object>(refusalRequest, "status").ToString(), Is.EqualTo("Recusada"));
        object warWithAggressor = Relation(4, 2);
        Assert.That(Get<bool>(warWithAggressor, "guerraDeclarada"), Is.True,
            "O aliado militar deve entrar na guerra contra o agressor.");
        Assert.That(Get<int>(warWithAggressor, "agressorTeamId"), Is.EqualTo(4),
            "O aliado que presta apoio deve declarar guerra contra o agressor.");
        Assert.That(Get<int>(warWithAggressor, "defensorTeamId"), Is.EqualTo(2));
        Assert.That(Get<bool>(valdoria, "sancionado"), Is.True,
            "O aliado que não pode abrir outra frente deve sancionar o agressor.");
        Assert.That(Get<bool>(refusingAlly, "emGuerra"), Is.False);
        Assert.That(Get<bool>(valdoria, "emGuerra"), Is.True);
        Assert.That(Get<bool>(karsovia, "emGuerra"), Is.True);
        Assert.That(Get<bool>(militaryAlly, "emGuerra"), Is.True);
    }

    [Test]
    public void AutonomousAllianceProposalBetweenTwoAIsIsAcceptedWithoutPlayer()
    {
        AddCountry(1, "Jogador");
        AddCountry(2, "Valdoria", "CooperacaoGlobal", military: 60, balance: 10000000L, stability: 70f, diplomacy: 60);
        AddCountry(3, "Karsovia", "AliancaDefesa", military: 60, balance: 10000000L, stability: 70f, diplomacy: 60);
        AddIdentity(2);
        AddIdentity(3);
        Set(Relation(2, 3), "valor", 70);

        ProcessDiplomacy();
        Set(government, "proximaDiplomaciaIA", 0f);
        ProcessDiplomacy();

        object proposal = proposals.Cast<object>().FirstOrDefault(item => Get<object>(item, "tipo").ToString() == "Alianca");
        Assert.That(proposal, Is.Not.Null);
        Assert.That(Get<object>(proposal, "status").ToString(), Is.EqualTo("Executada"));
        Assert.That(Get<bool>(Relation(2, 3), "pactoMilitar"), Is.True);
    }

    [Test]
    public void AutonomousCeasefireBetweenTwoAIsEndsWarWithoutPlayer()
    {
        AddCountry(1, "Jogador");
        object exhaustedCountry = AddCountry(2, "Valdoria", military: 35, balance: 1000L, stability: 42f);
        object opponent = AddCountry(3, "Karsovia", military: 70, balance: 10000000L, stability: 70f);
        AddIdentity(2);
        AddIdentity(3);

        object relation = Relation(2, 3);
        Set(relation, "guerraDeclarada", true);
        Set(relation, "agressorTeamId", 2);
        Set(relation, "defensorTeamId", 3);
        Set(relation, "valor", -20);
        Set(exhaustedCountry, "emGuerra", true);
        Set(exhaustedCountry, "rivalTeamId", 3);
        Set(opponent, "emGuerra", true);
        Set(opponent, "rivalTeamId", 2);

        ProcessDiplomacy();

        object ceasefire = proposals.Cast<object>().FirstOrDefault(proposal =>
            Get<object>(proposal, "tipo").ToString() == "CessarFogo"
            && Get<int>(proposal, "origemTeamId") == 2
            && Get<int>(proposal, "alvoTeamId") == 3);
        Assert.That(ceasefire, Is.Not.Null, "O país exaurido deve pedir paz à outra IA.");
        Assert.That(Get<object>(ceasefire, "status").ToString(), Is.EqualTo("Pendente"));

        ProcessDiplomacy();

        Assert.That(Get<object>(ceasefire, "status").ToString(), Is.EqualTo("Executada"));
        Assert.That(Get<bool>(relation, "guerraDeclarada"), Is.False);
        Assert.That(Get<bool>(relation, "cessarFogoAtivo"), Is.True);
        Assert.That(Get<bool>(exhaustedCountry, "emGuerra"), Is.False);
        Assert.That(Get<bool>(opponent, "emGuerra"), Is.False);
    }

    [Test]
    public void RemoteUnconfiguredIslandIsNotClaimedMerelyForBeingAvailable()
    {
        Type mapType = FindType("DadosMapaTerritorial");
        Type regionType = FindType("RegiaoPolitica");
        Type strategistDiplomacyType = FindType("Hegemonia.AI.IA03.GestorDiplomaciaIA03");
        ScriptableObject map = (ScriptableObject)ScriptableObject.CreateInstance(mapType);
        try
        {
            IList regions = (IList)mapType.GetProperty("Regioes", Members).GetValue(map);
            object home = CreateRegion(regionType, "home", 2, false,
                new Vector2(0.08f, 0.08f), new Vector2(0.22f, 0.08f), new Vector2(0.22f, 0.22f), new Vector2(0.08f, 0.22f));
            object island = CreateRegion(regionType, "island", 0, true,
                new Vector2(0.72f, 0.72f), new Vector2(0.82f, 0.72f), new Vector2(0.82f, 0.82f), new Vector2(0.72f, 0.82f));
            Set(island, "capturable", true);
            regions.Add(home);
            regions.Add(island);

            MethodInfo score = strategistDiplomacyType.GetMethod("CalcularPontuacaoTerritorial", Members);
            float baseline = Convert.ToSingle(score.Invoke(null, new object[] { map, island, 2 }));
            float threshold = Convert.ToSingle(strategistDiplomacyType.GetField(
                "PontuacaoMinimaParaReivindicarTerritorio", Members).GetValue(null));
            Assert.That(baseline, Is.LessThan(threshold));

            Set(island, "valorRecursos", 100f);
            Set(island, "nivelInfraestrutura", 100f);
            Set(island, "capacidadeDefensiva", 100f);
            Set(island, "acessoMaritimo", true);
            float strategicValue = Convert.ToSingle(score.Invoke(null, new object[] { map, island, 2 }));
            Assert.That(strategicValue, Is.GreaterThanOrEqualTo(threshold));
            Assert.That(strategicValue, Is.GreaterThan(baseline));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(map);
        }
    }

    private object AddCountry(int teamId, string name, string federation = "", int military = 50,
        long balance = 10000000L, float stability = 70f, int diplomacy = 50)
    {
        object country = Activator.CreateInstance(countryType);
        Set(country, "teamId", teamId);
        Set(country, "nomePais", name);
        Set(country, "federacaoGlobal", federation);
        Set(country, "nivelMilitar", military);
        Set(country, "saldo", balance);
        Set(country, "estabilidade", stability);
        Set(country, "nivelDiplomatico", diplomacy);
        Set(country, "armamentos", 500);
        Set(country, "comida", 500);
        countries.Add(country);
        return country;
    }

    private object AddIdentity(int teamId)
    {
        GameObject go = new GameObject("Teste_Identidade_IA_" + teamId);
        identityObjects.Add(go);
        Component identity = go.AddComponent(identityType);
        Set(identity, "teamID", teamId);
        Set(identity, "estaAtivo", true);
        Set(identity, "eliminado", false);
        if (!identities.Contains(identity)) identities.Add(identity);
        return identity;
    }

    private object Relation(int teamA, int teamB)
    {
        MethodInfo method = governmentType.GetMethod("ObterRelacao", Members);
        return method.Invoke(government, new object[] { teamA, teamB });
    }

    private object FindSupportRequest(int allyTeamId)
    {
        return proposals.Cast<object>().FirstOrDefault(proposal =>
            Get<object>(proposal, "tipo").ToString() == "PedidoApoioMilitar"
            && Get<int>(proposal, "alvoTeamId") == allyTeamId);
    }

    private void ProcessDiplomacy()
    {
        Set(government, "proximaDiplomaciaIA", 0f);
        governmentType.GetMethod("ProcessarDiplomaciaIA", Members).Invoke(government, null);
    }

    private static object CreateRegion(Type regionType, string id, int owner, bool neutral, params Vector2[] vertices)
    {
        object region = Activator.CreateInstance(regionType);
        Set(region, "territorioId", id);
        Set(region, "ownerCountryTeamId", owner);
        Set(region, "neutral", neutral);
        Set(region, "tipo", Enum.Parse(FindType("TipoRegiaoPolitica"), "Terra"));
        IList vertexList = (IList)Activator.CreateInstance(regionType.GetField("vertices", Members).FieldType);
        foreach (Vector2 vertex in vertices) vertexList.Add(vertex);
        Set(region, "vertices", vertexList);
        return region;
    }

    private static IList GetList(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(name, Members);
        return field != null ? (IList)field.GetValue(instance) : (IList)instance.GetType().GetProperty(name, Members).GetValue(instance);
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Gameplay type not found: " + name);
        return type;
    }

    private static void Set(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name, Members);
        if (field != null) { field.SetValue(instance, value); return; }
        PropertyInfo property = instance.GetType().GetProperty(name, Members);
        Assert.That(property, Is.Not.Null, "Member not found: " + name);
        property.SetValue(instance, value);
    }

    private static T Get<T>(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(name, Members);
        object value = field != null ? field.GetValue(instance) : instance.GetType().GetProperty(name, Members).GetValue(instance);
        return (T)value;
    }
}
#endif
