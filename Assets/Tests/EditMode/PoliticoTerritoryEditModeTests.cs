#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PoliticoTerritoryEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject gerenteObject;
    private Component gerente;
    private ScriptableObject mapa;
    private readonly List<GameObject> objetosTeste = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Type gerenteType = ResolveType("GerenteDeTerritorio");
        gerenteObject = new GameObject("Teste_GerenteTerritorioPolitico");
        gerenteObject.SetActive(false);
        gerente = gerenteObject.AddComponent(gerenteType);
        mapa = CriarMapa();
        SetMember(gerente, "MapaPolitico", mapa);
        gerenteObject.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < objetosTeste.Count; i++)
            if (objetosTeste[i] != null) UnityEngine.Object.DestroyImmediate(objetosTeste[i]);
        objetosTeste.Clear();
        if (mapa != null) UnityEngine.Object.DestroyImmediate(mapa);
        if (gerenteObject != null) UnityEngine.Object.DestroyImmediate(gerenteObject);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void PoliticoPolygon_IsSovereignOverLegacyCitySquares()
    {
        AdicionarRegiao(mapa, "zona-a", 2, false, false, "Terra", Retangulo(0.2f, 0.2f, 0.8f, 0.8f), Color.green);
        Component marcador = CriarMarcadorLegado(1, Vector3.zero, 250f);

        int dono = (int)Invocar(gerente, "ObterDonoDoPonto", Vector3.zero);

        Assert.That(dono, Is.EqualTo(2), "O polígono político deve prevalecer sobre o quadrado legado da prefeitura.");
        Assert.That(marcador, Is.Not.Null);
    }

    [Test]
    public void DefinedButUnassignedPolygon_StillBlocksLegacyOwnership()
    {
        AdicionarRegiao(mapa, "zona-sem-pais", -1, false, false, "Terra", Retangulo(0.2f, 0.2f, 0.8f, 0.8f), Color.white);
        CriarMarcadorLegado(1, Vector3.zero, 250f);

        int donoCompatibilidade = (int)Invocar(gerente, "ObterDonoDoPonto", Vector3.zero);
        object territorio = Invocar(gerente, "ObterTerritorioNaPosicao", Vector3.zero);

        Assert.That(donoCompatibilidade, Is.EqualTo(0), "Polígono sem associação não pode herdar o dono legado.");
        Assert.That(GetField<int>(territorio, "ownerCountryTeamId"), Is.EqualTo(-1));
        Assert.That(GetField<bool>(territorio, "neutral"), Is.False);
    }

    [Test]
    public void LegacySquare_RemainsFallbackOutsidePoliticalPolygons()
    {
        Component marcador = CriarMarcadorLegado(3, Vector3.zero, 250f);

        int dono = (int)Invocar(gerente, "ObterDonoDoPonto", Vector3.zero);

        Assert.That(dono, Is.EqualTo(3));
        Assert.That(marcador, Is.Not.Null);
    }

    [Test]
    public void SameBiomeColor_DoesNotMergeSeparatePoliticalRegions()
    {
        AdicionarRegiao(mapa, "regiao-oeste", 1, false, false, "Terra", Retangulo(0.1f, 0.1f, 0.4f, 0.9f), Color.green);
        AdicionarRegiao(mapa, "regiao-leste", 2, false, false, "Terra", Retangulo(0.6f, 0.1f, 0.9f, 0.9f), Color.green);
        Invocar(mapa, "InvalidarIndice");

        object oeste = Invocar(mapa, "ConsultarUv", new Vector2(0.25f, 0.5f));
        object leste = Invocar(mapa, "ConsultarUv", new Vector2(0.75f, 0.5f));

        Assert.That(GetField<string>(oeste, "territorioId"), Is.EqualTo("regiao-oeste"));
        Assert.That(GetField<int>(oeste, "ownerCountryTeamId"), Is.EqualTo(1));
        Assert.That(GetField<string>(leste, "territorioId"), Is.EqualTo("regiao-leste"));
        Assert.That(GetField<int>(leste, "ownerCountryTeamId"), Is.EqualTo(2));
    }

    [Test]
    public void PhaseReference_CentralIslandStartsNeutralAndCapturable()
    {
        ScriptableObject referencia = (ScriptableObject)ResolveType("DadosMapaTerritorial")
            .GetMethod("CriarModeloFase", StaticFlags).Invoke(null, new object[] { null });
        try
        {
            object ilha = Invocar(referencia, "EncontrarRegiao", "ilha-central");
            Assert.That(GetField<int>(ilha, "ownerCountryTeamId"), Is.EqualTo(0));
            Assert.That(GetField<bool>(ilha, "neutral"), Is.True);
            Assert.That(GetField<bool>(ilha, "capturable"), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(referencia); }
    }

    [Test]
    public void InitialTerritoryResource_LoadsScriptAndPreservesEditableOwnership()
    {
        UnityEngine.Object carregado = Resources.Load("MapaTerritorialInicial", ResolveType("DadosMapaTerritorial"));
        Assert.That(carregado, Is.Not.Null, "O asset Resources precisa carregar com a classe ScriptableObject correta.");

        PropertyInfo regioesProperty = carregado.GetType().GetProperty("Regioes", InstanceFlags);
        IList regioes = (IList)regioesProperty.GetValue(carregado, null);
        Assert.That(regioes.Count, Is.EqualTo(12));
        for (int i = 0; i < regioes.Count; i++)
        {
            object regiao = regioes[i];
            string id = GetField<string>(regiao, "territorioId");
            if (id == "ilha-central") continue;
            Assert.That(GetField<int>(regiao, "ownerCountryTeamId"), Is.EqualTo(-1), id);
            Assert.That(GetField<bool>(regiao, "neutral"), Is.False, id);
        }
    }

    [Test]
    public void CapturingNeutralIsland_UpdatesQueryAndCanBeSavedAndRestored()
    {
        ScriptableObject referencia = (ScriptableObject)ResolveType("DadosMapaTerritorial")
            .GetMethod("CriarModeloFase", StaticFlags).Invoke(null, new object[] { null });
        SetMember(gerente, "MapaPolitico", referencia);
        try
        {
            bool capturou = (bool)Invocar(gerente, "TentarCapturarTerritorio", "ilha-central", 4);
            Vector3 pontoIlha = (Vector3)Invocar(gerente, "MapUvToWorld", new Vector2(0.59f, 0.43f));
            int owner = (int)Invocar(gerente, "ObterDonoDoPonto", pontoIlha);
            IList snapshot = (IList)Invocar(gerente, "CopiarProprietariosCapturados");
            Invocar(gerente, "RestaurarProprietariosCapturados", snapshot);
            int ownerRestaurado = (int)Invocar(gerente, "ObterDonoDaRegiao", "ilha-central");

            Assert.That(capturou, Is.True);
            Assert.That(owner, Is.EqualTo(4));
            Assert.That(ownerRestaurado, Is.EqualTo(4));
        }
        finally { UnityEngine.Object.DestroyImmediate(referencia); }
    }

    [Test]
    public void TerritorialSeaAndInternationalWater_AreDifferentQueryResults()
    {
        AdicionarRegiao(mapa, "mar-controlado", 5, false, false, "AguasTerritoriais", Retangulo(0.4f, 0.4f, 0.6f, 0.6f), Color.magenta);

        object territorial = Invocar(gerente, "ObterTerritorioNaPosicao", Vector3.zero);
        object internacional = Invocar(gerente, "ObterTerritorioNaPosicao", new Vector3(0f, -5f, 4000f));

        Assert.That(GetField<string>(territorial, "territorioId"), Is.EqualTo("mar-controlado"));
        Assert.That(GetField<bool>(territorial, "encontrouRegiao"), Is.True);
        Assert.That(GetField<bool>(territorial, "aguasInternacionais"), Is.False);
        Assert.That(GetField<string>(internacional, "territorioId"), Is.EqualTo("aguas-internacionais"));
        Assert.That(GetField<bool>(internacional, "aguasInternacionais"), Is.True);
    }

    [Test]
    public void UnassignedLand_IsNotMistakenForNeutralTerritory()
    {
        AdicionarRegiao(mapa, "terra-sem-associacao", -1, false, false, "Terra", Retangulo(0.2f, 0.2f, 0.8f, 0.8f), Color.white);
        object territorio = Invocar(gerente, "ObterTerritorioNaPosicao", Vector3.zero);

        Assert.That(GetField<bool>(territorio, "encontrouRegiao"), Is.True);
        Assert.That(GetField<bool>(territorio, "neutral"), Is.False);
        Assert.That(GetField<int>(territorio, "ownerCountryTeamId"), Is.EqualTo(-1));
    }

    [Test]
    public void UnapprovedPassage_IsReportedButDoesNotBecomeAutomaticFireOrder()
    {
        AdicionarRegiao(mapa, "fronteira", 2, false, false, "Terra", Retangulo(0.2f, 0.2f, 0.8f, 0.8f), Color.white);
        GameObject unidadeObjeto = new GameObject("Unidade_Estrangeira");
        objetosTeste.Add(unidadeObjeto);
        Type identidadeType = ResolveType("IdentidadeUnidade");
        Component identidade = unidadeObjeto.AddComponent(identidadeType);
        SetMember(identidade, "teamID", 1);
        object contexto = ResolveType("ContextoTerritorialDiplomatico").GetMethod("AvaliarPresenca", StaticFlags)
            .Invoke(null, new object[] { identidade, Enum.Parse(ResolveType("MeioPassagemTerritorial"), "Terrestre"),
                Enum.Parse(ResolveType("ModoOperacionalTerritorial"), "Passivo"), true, false });

        Assert.That(GetField<bool>(contexto, "violacaoTerritorial"), Is.True);
        Assert.That(GetField<int>(contexto, "paisDoTerritorio"), Is.EqualTo(2));
        Assert.That(GetField<int>(contexto, "acao"), Is.EqualTo((int)Enum.Parse(ResolveType("AcaoRegrasEngajamento"), "Alertar")));
    }

    private Component CriarMarcadorLegado(int team, Vector3 position, float radius)
    {
        GameObject obj = new GameObject("Marcador_Legado_" + team);
        obj.transform.position = position;
        objetosTeste.Add(obj);
        obj.AddComponent(ResolveType("IdentidadeUnidade"));
        Component marker = obj.AddComponent(ResolveType("MarcadorTerritorio"));
        Invocar(marker, "ConfigureOwnership", team, true, radius);
        Invocar(gerente, "RegistrarMarcador", marker);
        return marker;
    }

    private static ScriptableObject CriarMapa()
    {
        return ScriptableObject.CreateInstance(ResolveType("DadosMapaTerritorial"));
    }

    private static void AdicionarRegiao(ScriptableObject data, string id, int owner, bool neutral, bool capturable, string tipo, List<Vector2> vertices, Color color)
    {
        Type regionType = ResolveType("RegiaoPolitica");
        object region = Activator.CreateInstance(regionType);
        SetMember(region, "territorioId", id);
        SetMember(region, "nome", id);
        SetMember(region, "ownerCountryTeamId", owner);
        SetMember(region, "neutral", neutral);
        SetMember(region, "capturable", capturable);
        SetMember(region, "tipo", Enum.Parse(ResolveType("TipoRegiaoPolitica"), tipo));
        SetMember(region, "vertices", vertices);
        SetMember(region, "corMapa", color);
        FieldInfo field = data.GetType().GetField("regioes", InstanceFlags);
        ((IList)field.GetValue(data)).Add(region);
        Invocar(data, "InvalidarIndice");
    }

    private static List<Vector2> Retangulo(float minX, float minY, float maxX, float maxY)
    {
        return new List<Vector2>
        {
            new Vector2(minX, minY), new Vector2(maxX, minY),
            new Vector2(maxX, maxY), new Vector2(minX, maxY)
        };
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }
        Assert.Fail("Tipo não encontrado: " + fullName);
        return null;
    }

    private static object Invocar(object alvo, string nome, params object[] argumentos)
    {
        MethodInfo metodo = alvo.GetType().GetMethod(nome, InstanceFlags | StaticFlags);
        Assert.That(metodo, Is.Not.Null, alvo.GetType().FullName + "." + nome);
        return metodo.Invoke(alvo, argumentos);
    }

    private static void SetMember(object alvo, string nome, object valor)
    {
        Type tipo = alvo.GetType();
        PropertyInfo propriedade = tipo.GetProperty(nome, InstanceFlags | StaticFlags);
        if (propriedade != null) { propriedade.SetValue(alvo, valor); return; }
        FieldInfo campo = tipo.GetField(nome, InstanceFlags | StaticFlags);
        Assert.That(campo, Is.Not.Null, tipo.FullName + "." + nome);
        campo.SetValue(alvo, valor);
    }

    private static T GetField<T>(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, InstanceFlags | StaticFlags);
        Assert.That(campo, Is.Not.Null, alvo.GetType().FullName + "." + nome);
        object valor = campo.GetValue(alvo);
        if (valor is T resultado) return resultado;
        return (T)Convert.ChangeType(valor, typeof(T));
    }
}
#endif
