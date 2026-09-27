#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        object consulta = Invocar(gerente, "ObterTerritorioNaPosicao", Vector3.zero);

        Assert.That(dono, Is.EqualTo(2), "O polígono político deve prevalecer sobre o quadrado legado da prefeitura.");
        Assert.That(GetProperty<object>(consulta, "estado").ToString(), Is.EqualTo("CountryOwned"));
        Assert.That(marcador, Is.Not.Null);
        RegistrarConsulta("CountryOwnedPolygon", Vector3.zero, consulta);
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
    public void LegacySquare_RemainsFallbackOutsidePoliticalCoverage()
    {
        Vector3 outsideCoverage = new Vector3(6000f, 0f, 0f);
        Component marcador = CriarMarcadorLegado(3, outsideCoverage, 250f);

        int dono = (int)Invocar(gerente, "ObterDonoDoPonto", outsideCoverage);

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
    public void InitialPoliticalPolygons_ResolveWithStructuredStateAndLogEveryRegion()
    {
        UnityEngine.Object asset = Resources.Load("MapaTerritorialInicial", ResolveType("DadosMapaTerritorial"));
        Assert.That(asset, Is.Not.Null);
        if (mapa != null) UnityEngine.Object.DestroyImmediate(mapa);
        mapa = UnityEngine.Object.Instantiate((ScriptableObject)asset);
        SetMember(gerente, "MapaPolitico", mapa);

        IList regioes = (IList)mapa.GetType().GetProperty("Regioes", InstanceFlags).GetValue(mapa, null);
        int terrasPrincipais = 0;
        int ilhas = 0;
        int aguasTerritoriais = 0;
        for (int i = 0; i < regioes.Count; i++)
        {
            object regiao = regioes[i];
            string id = GetField<string>(regiao, "territorioId");
            string tipo = GetField<object>(regiao, "tipo").ToString();
            Vector2 uv = EncontrarPontoInterior(regiao, mapa);
            Vector3 world = (Vector3)Invocar(gerente, "MapUvToWorld", uv);
            object consulta = Invocar(gerente, "ObterTerritorioNaPosicao", world);

            Assert.That(GetField<bool>(consulta, "encontrouRegiao"), Is.True, id);
            Assert.That(GetField<string>(consulta, "territorioId"), Is.EqualTo(id), id);
            Assert.That(GetField<object>(consulta, "fonte").ToString(), Is.EqualTo("PoligonoPolitico"), id);
            Assert.That(GetField<object>(consulta, "tipo").ToString(), Is.EqualTo(tipo), id);
            Assert.That(GetField<int>(consulta, "ownerCountryTeamId"), Is.EqualTo(GetField<int>(regiao, "ownerCountryTeamId")), id);
            Assert.That(GetField<bool>(consulta, "neutral"), Is.EqualTo(GetField<bool>(regiao, "neutral")), id);
            Assert.That(GetField<bool>(consulta, "capturable"), Is.EqualTo(GetField<bool>(regiao, "capturable")), id);
            if (id == "ilha-central")
            {
                Assert.That(GetField<int>(consulta, "ownerCountryTeamId"), Is.Zero);
                Assert.That(GetField<bool>(consulta, "neutral"), Is.True);
                Assert.That(GetField<bool>(consulta, "capturable"), Is.True);
            }

            string estado = GetProperty<object>(consulta, "estado").ToString();
            string estadoEsperado = GetField<bool>(regiao, "neutral") ? "NeutralTerritory"
                : GetField<int>(regiao, "ownerCountryTeamId") > 0 ? "CountryOwned" : "UnownedTerritory";
            Assert.That(estado, Is.EqualTo(estadoEsperado), id);
            TestContext.WriteLine(
                "TerritoryQuery|WorldPosition={0:R},{1:R},{2:R}|TerritoryId={3}|OwnerCountryTeamId={4}|RegionType={5}|Neutral={6}|Capturable={7}|ResolutionState={8}|Source={9}",
                world.x, world.y, world.z, id, GetField<int>(consulta, "ownerCountryTeamId"), tipo,
                GetField<bool>(consulta, "neutral"), GetField<bool>(consulta, "capturable"), estado,
                GetField<object>(consulta, "fonte"));

            if (tipo == "Terra" && id == "ilha-central") ilhas++;
            else if (tipo == "Terra") terrasPrincipais++;
            if (tipo == "AguasTerritoriais") aguasTerritoriais++;
        }

        Assert.That(terrasPrincipais, Is.EqualTo(6));
        Assert.That(ilhas, Is.EqualTo(1));
        Assert.That(aguasTerritoriais, Is.EqualTo(5));
    }

    [Test]
    public void PoliticalCoverageGap_BlocksLegacyAndWaterGapResolvesInternationally()
    {
        UnityEngine.Object asset = Resources.Load("MapaTerritorialInicial", ResolveType("DadosMapaTerritorial"));
        Assert.That(asset, Is.Not.Null);
        if (mapa != null) UnityEngine.Object.DestroyImmediate(mapa);
        mapa = UnityEngine.Object.Instantiate((ScriptableObject)asset);
        SetMember(gerente, "MapaPolitico", mapa);

        Vector2 gapUv = EncontrarLacunaPolitica(mapa);
        Vector3 gap = (Vector3)Invocar(gerente, "MapUvToWorld", gapUv);
        CriarMarcadorLegado(4, gap, 250f);

        object indefinido = Invocar(gerente, "ObterTerritorioNaPosicao", gap);
        int donoCompatibilidade = (int)Invocar(gerente, "ObterDonoDoPonto", gap);
        Assert.That(GetField<bool>(indefinido, "encontrouRegiao"), Is.False);
        Assert.That(GetProperty<object>(indefinido, "estado").ToString(), Is.EqualTo("Undefined"));
        Assert.That(GetField<int>(indefinido, "ownerCountryTeamId"), Is.EqualTo(-1));
        Assert.That(GetField<object>(indefinido, "fonte").ToString(), Is.EqualTo("Nenhuma"));
        Assert.That(donoCompatibilidade, Is.Zero);
        RegistrarConsulta("UndefinedGap", gap, indefinido);

        Vector3 mar = gap;
        mar.y = -5f;
        object internacional = Invocar(gerente, "ObterTerritorioNaPosicao", mar);
        Assert.That(GetField<bool>(internacional, "encontrouRegiao"), Is.True);
        Assert.That(GetField<bool>(internacional, "aguasInternacionais"), Is.True);
        Assert.That(GetField<string>(internacional, "territorioId"), Is.EqualTo("aguas-internacionais"));
        Assert.That(GetField<int>(internacional, "ownerCountryTeamId"), Is.EqualTo(-1));
        Assert.That(GetProperty<object>(internacional, "estado").ToString(), Is.EqualTo("InternationalWaters"));
        Assert.That(GetField<object>(internacional, "fonte").ToString(), Is.EqualTo("AguasInternacionais"));
        RegistrarConsulta("InternationalWaterGap", mar, internacional);
    }

    [Test]
    public void LegacyFallback_RemainsAvailableOutsidePoliticalCoverageButNotForWater()
    {
        Vector3 outsideLand = new Vector3(6000f, 0f, 0f);
        CriarMarcadorLegado(3, outsideLand, 250f);
        object legado = Invocar(gerente, "ObterTerritorioNaPosicao", outsideLand);
        Assert.That(GetField<bool>(legado, "encontrouRegiao"), Is.True);
        Assert.That(GetField<object>(legado, "fonte").ToString(), Is.EqualTo("Legado"));
        Assert.That(GetField<int>(legado, "ownerCountryTeamId"), Is.EqualTo(3));
        RegistrarConsulta("OutsideCoverageLegacy", outsideLand, legado);

        Vector3 outsideWater = new Vector3(6000f, -5f, 0f);
        object internacional = Invocar(gerente, "ObterTerritorioNaPosicao", outsideWater);
        Assert.That(GetField<string>(internacional, "territorioId"), Is.EqualTo("aguas-internacionais"));
        Assert.That(GetProperty<object>(internacional, "estado").ToString(), Is.EqualTo("InternationalWaters"));
        Assert.That(GetField<object>(internacional, "fonte").ToString(), Is.EqualTo("AguasInternacionais"));
        RegistrarConsulta("OutsideCoverageWater", outsideWater, internacional);
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
            object consultaCapturada = Invocar(gerente, "ObterTerritorioNaPosicao", pontoIlha);
            bool repetiuCaptura = (bool)Invocar(gerente, "TentarCapturarTerritorio", "ilha-central", 4);
            IList snapshot = (IList)Invocar(gerente, "CopiarProprietariosCapturados");
            Assert.That(snapshot.Count, Is.EqualTo(1));
            object estadoSalvo = snapshot[0];
            Assert.That(GetField<string>(estadoSalvo, "territorioId"), Is.EqualTo("ilha-central"));
            Assert.That(GetField<int>(estadoSalvo, "ownerCountryTeamId"), Is.EqualTo(4));
            Assert.That(GetField<bool>(estadoSalvo, "neutral"), Is.False);
            Assert.That(estadoSalvo.GetType().GetField("capturable", InstanceFlags), Is.Null,
                "Capturable é configuração estática do asset, não estado do save.");
            Invocar(gerente, "RestaurarProprietariosCapturados", snapshot);
            int ownerRestaurado = (int)Invocar(gerente, "ObterDonoDaRegiao", "ilha-central");
            object consultaRestaurada = Invocar(gerente, "ObterTerritorioNaPosicao", pontoIlha);

            Assert.That(capturou, Is.True);
            Assert.That(owner, Is.EqualTo(4));
            Assert.That(GetField<int>(consultaCapturada, "ownerCountryTeamId"), Is.EqualTo(4));
            Assert.That(GetField<bool>(consultaCapturada, "neutral"), Is.False);
            Assert.That(GetProperty<object>(consultaCapturada, "estado").ToString(), Is.EqualTo("CountryOwned"));
            Assert.That(repetiuCaptura, Is.False, "A mesma propriedade não deve disparar outra captura/evento.");
            Assert.That(ownerRestaurado, Is.EqualTo(4));
            Assert.That(GetField<int>(consultaRestaurada, "ownerCountryTeamId"), Is.EqualTo(4));
            Assert.That(GetField<bool>(consultaRestaurada, "neutral"), Is.False);
            Assert.That(GetProperty<object>(consultaRestaurada, "estado").ToString(), Is.EqualTo("CountryOwned"));

            // JsonUtility supplies false for an absent bool. A v16 snapshot
            // must ignore that default and leave the base asset's neutral true.
            object salvoLegado = JsonUtility.FromJson(
                "{\"territorioId\":\"ilha-central\",\"ownerCountryTeamId\":0}", estadoSalvo.GetType());
            IList snapshotLegado = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(estadoSalvo.GetType()));
            snapshotLegado.Add(salvoLegado);
            Invocar(gerente, "RestaurarEstadoPolitico", snapshotLegado, false);
            object consultaLegada = Invocar(gerente, "ObterTerritorioNaPosicao", pontoIlha);
            Assert.That(GetField<bool>(consultaLegada, "neutral"), Is.True,
                "O owner e neutral do asset devem sobreviver ao default false de bool ausente em v16.");
        }
        finally { UnityEngine.Object.DestroyImmediate(referencia); }
    }

    [Test, Explicit("Executar somente depois dos 13 testes políticos: fluxo normal de SaveGame")]
    public void SistemaSaveGame_NormalSaveLoadRestoresTerritoryAndKeepsUnitRecord()
    {
        ScriptableObject referencia = (ScriptableObject)ResolveType("DadosMapaTerritorial")
            .GetMethod("CriarModeloFase", StaticFlags).Invoke(null, new object[] { null });
        SetMember(gerente, "MapaPolitico", referencia);

        string caminhoTemporario = Path.Combine(Application.temporaryCachePath,
            "territory-save-test-" + Guid.NewGuid().ToString("N") + ".json");
        GameObject saveObject = new GameObject("Teste_SistemaSaveGame");
        objetosTeste.Add(saveObject);
        Component save = saveObject.AddComponent(ResolveType("SistemaSaveGame"));
        SetMember(save, "dadosAtuais", Activator.CreateInstance(ResolveType("DadosDoJogo")));
        SetMember(save, "caminhoDoArquivo", caminhoTemporario);

        GameObject unidade = new GameObject("Teste_UnidadePersistida");
        objetosTeste.Add(unidade);
        Component identidade = unidade.AddComponent(ResolveType("IdentidadeUnidade"));
        SetMember(identidade, "teamID", 3);
        Component saveable = unidade.AddComponent(ResolveType("SaveableEntity"));
        string uniqueId = GetProperty<string>(saveable, "UniqueId");
        Vector3 pontoIlha = (Vector3)Invocar(gerente, "MapUvToWorld", new Vector2(0.59f, 0.43f));

        try
        {
            object estadoInicial = Invocar(gerente, "ObterTerritorioNaPosicao", pontoIlha);
            Assert.That(GetProperty<object>(estadoInicial, "estado").ToString(), Is.EqualTo("NeutralTerritory"));
            Assert.That((bool)Invocar(gerente, "TentarCapturarTerritorio", "ilha-central", 4), Is.True);
            Assert.That((int)Invocar(gerente, "ObterDonoDaRegiao", "ilha-central"), Is.EqualTo(4));

            InvocarSemArgumentos(save, "SalvarJogo");
            Assert.That(File.Exists(caminhoTemporario), Is.True);
            string json = File.ReadAllText(caminhoTemporario);
            Assert.That(json, Does.Contain("\"territoriosCapturados\""));
            Assert.That(json, Does.Contain("\"saveVersion\": 17"));
            Assert.That(json, Does.Contain("\"neutral\": false"));
            Assert.That(json, Does.Not.Contain("\"capturable\""));
            Assert.That(json, Does.Contain("\"uniqueId\": \"" + uniqueId + "\""));

            Invocar(gerente, "RestaurarProprietariosCapturados", (object)null);
            Assert.That((int)Invocar(gerente, "ObterDonoDaRegiao", "ilha-central"), Is.Zero);
            InvocarSemArgumentos(save, "CarregarJogo");

            object consulta = Invocar(gerente, "ObterTerritorioNaPosicao", pontoIlha);
            Assert.That(GetField<int>(consulta, "ownerCountryTeamId"), Is.EqualTo(4));
            Assert.That(GetField<bool>(consulta, "neutral"), Is.False);
            Assert.That(GetField<bool>(consulta, "capturable"), Is.True);
            Assert.That(GetProperty<object>(consulta, "estado").ToString(), Is.EqualTo("CountryOwned"));

            IList entidades = (IList)GetField<object>(save, "dadosAtuais").GetType()
                .GetField("entidades", InstanceFlags).GetValue(GetField<object>(save, "dadosAtuais"));
            Assert.That(entidades.Count, Is.EqualTo(1));
            Assert.That(GetField<string>(entidades[0], "uniqueId"), Is.EqualTo(uniqueId));
            Assert.That(GetField<string>(entidades[0], "prefabKey"), Is.EqualTo("Teste_UnidadePersistida"));

            GameObject mapaObject = new GameObject("Teste_VisaoMapaTerritorial");
            objetosTeste.Add(mapaObject);
            Component mapaController = mapaObject.AddComponent(ResolveType("MapaGeralController"));
            object ilha = referencia.GetType().GetMethod("EncontrarRegiao", InstanceFlags)
                .Invoke(referencia, new object[] { "ilha-central" });
            string rotulo = (string)Invocar(mapaController, "TextoDonoTerritorial", gerente, ilha);
            Assert.That(rotulo, Is.EqualTo("PAÍS 4"));
        }
        finally
        {
            if (File.Exists(caminhoTemporario)) File.Delete(caminhoTemporario);
            UnityEngine.Object.DestroyImmediate(referencia);
        }
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
        Assert.That(GetField<int>(territorial, "ownerCountryTeamId"), Is.EqualTo(5));
        Assert.That(GetField<object>(territorial, "tipo").ToString(), Is.EqualTo("AguasTerritoriais"));
        Assert.That(GetProperty<object>(territorial, "estado").ToString(), Is.EqualTo("CountryOwned"));
        Assert.That(GetField<string>(internacional, "territorioId"), Is.EqualTo("aguas-internacionais"));
        Assert.That(GetField<bool>(internacional, "aguasInternacionais"), Is.True);
        Assert.That(GetProperty<object>(internacional, "estado").ToString(), Is.EqualTo("InternationalWaters"));
    }

    [Test]
    public void UnassignedLand_IsNotMistakenForNeutralTerritory()
    {
        AdicionarRegiao(mapa, "terra-sem-associacao", -1, false, false, "Terra", Retangulo(0.2f, 0.2f, 0.8f, 0.8f), Color.white);
        object territorio = Invocar(gerente, "ObterTerritorioNaPosicao", Vector3.zero);

        Assert.That(GetField<bool>(territorio, "encontrouRegiao"), Is.True);
        Assert.That(GetField<bool>(territorio, "neutral"), Is.False);
        Assert.That(GetField<int>(territorio, "ownerCountryTeamId"), Is.EqualTo(-1));
        Assert.That(GetProperty<object>(territorio, "estado").ToString(), Is.EqualTo("UnownedTerritory"));
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

    private Vector2 EncontrarPontoInterior(object regiao, ScriptableObject data)
    {
        IList vertices = (IList)GetField<object>(regiao, "vertices");
        string territorioEsperado = GetField<string>(regiao, "territorioId");
        MethodInfo dentro = ResolveType("DadosMapaTerritorial").GetMethod("PontoDentroOuNaBorda", StaticFlags);
        MethodInfo consultar = data.GetType().GetMethod("ConsultarUv", InstanceFlags);
        for (int y = 4; y < 252; y += 2)
        for (int x = 4; x < 508; x += 2)
        {
            Vector2 uv = new Vector2(x / 512f, y / 256f);
            if (!(bool)dentro.Invoke(null, new object[] { uv, vertices })) continue;
            object consulta = consultar.Invoke(data, new object[] { uv });
            if (GetField<bool>(consulta, "encontrouRegiao")
                && GetField<string>(consulta, "territorioId") == territorioEsperado) return uv;
        }
        Assert.Fail("Não foi encontrado ponto consultável interior para " + territorioEsperado);
        return Vector2.zero;
    }

    private Vector2 EncontrarLacunaPolitica(ScriptableObject data)
    {
        MethodInfo dentro = data.GetType().GetMethod("ConsultarUv", InstanceFlags);
        for (int y = 4; y < 252; y += 4)
        for (int x = 4; x < 508; x += 4)
        {
            Vector2 uv = new Vector2(x / 512f, y / 256f);
            object consulta = dentro.Invoke(data, new object[] { uv });
            if (!GetField<bool>(consulta, "encontrouRegiao")) return uv;
        }
        Assert.Fail("O asset não possui lacuna descobrível para a validação de Undefined.");
        return Vector2.zero;
    }

    private static void RegistrarConsulta(string caso, Vector3 world, object consulta)
    {
        TestContext.WriteLine(
            "TerritoryQuery|Case={0}|WorldPosition={1:R},{2:R},{3:R}|TerritoryId={4}|OwnerCountryTeamId={5}|RegionType={6}|Neutral={7}|Capturable={8}|ResolutionState={9}|Source={10}",
            caso, world.x, world.y, world.z, GetField<string>(consulta, "territorioId"),
            GetField<int>(consulta, "ownerCountryTeamId"), GetField<object>(consulta, "tipo"),
            GetField<bool>(consulta, "neutral"), GetField<bool>(consulta, "capturable"),
            GetProperty<object>(consulta, "estado"), GetField<object>(consulta, "fonte"));
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

    private static object InvocarSemArgumentos(object alvo, string nome)
    {
        MethodInfo metodo = alvo.GetType().GetMethod(nome, InstanceFlags | StaticFlags, null, Type.EmptyTypes, null);
        Assert.That(metodo, Is.Not.Null, alvo.GetType().FullName + "." + nome + "()");
        return metodo.Invoke(alvo, null);
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

    private static T GetProperty<T>(object alvo, string nome)
    {
        PropertyInfo propriedade = alvo.GetType().GetProperty(nome, InstanceFlags | StaticFlags);
        Assert.That(propriedade, Is.Not.Null, alvo.GetType().FullName + "." + nome);
        return (T)propriedade.GetValue(alvo, null);
    }
}
#endif
