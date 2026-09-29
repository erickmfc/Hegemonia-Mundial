#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class FundacaoTerritorialEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private Scene previewScene;
    private GameObject gerenteObject;
    private GameObject expansaoObject;
    private GameObject edificacaoObject;
    private Component gerente;
    private ScriptableObject mapa;

    [SetUp]
    public void SetUp()
    {
        Type gerenteType = ResolveType("GerenteDeTerritorio");
        if (GetStaticMemberValue(gerenteType, "Instancia") != null)
            Assert.Ignore("Há um GerenteDeTerritorio ativo; o teste não vai interferir no estado da cena atual.");

        previewScene = EditorSceneManager.NewPreviewScene();
        gerenteObject = new GameObject("Teste_FundacaoTerritorial");
        gerenteObject.SetActive(false);
        SceneManager.MoveGameObjectToScene(gerenteObject, previewScene);
        gerente = gerenteObject.AddComponent(gerenteType);
        gerenteObject.SetActive(true);

        mapa = GetPropertyValue<ScriptableObject>(gerente, "MapaPolitico");
        Assert.That(mapa, Is.Not.Null, "O mapa político de referência deve estar disponível em Resources.");
    }

    [TearDown]
    public void TearDown()
    {
        if (expansaoObject != null) UnityEngine.Object.DestroyImmediate(expansaoObject);
        if (edificacaoObject != null) UnityEngine.Object.DestroyImmediate(edificacaoObject);
        if (gerenteObject != null) UnityEngine.Object.DestroyImmediate(gerenteObject);
        if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
    }

    [Test]
    public void FundarTerritorioNeutro_AtualizaConsultaSemAlterarAsset()
    {
        Vector2 uvNeutra = EncontrarUv(territorio =>
            GetFieldValue<string>(territorio, "territorioId") == "ilha-central"
            && GetFieldValue<int>(territorio, "ownerCountryTeamId") == 0
            && GetFieldValue<bool>(territorio, "neutral")
            && GetFieldValue<object>(territorio, "tipo").ToString() == "Terra");
        Vector3 ponto = Invocar<Vector3>(gerente, "MapUvToWorld", uvNeutra);
        object consultaAntes = Invocar<object>(gerente, "ObterTerritorioNaPosicao", ponto);
        string territorioId = GetFieldValue<string>(consultaAntes, "territorioId");

        Assert.That(territorioId, Is.EqualTo("ilha-central"));
        Assert.That(Invocar<bool>(gerente, "TentarFundarTerritorio", ponto, 3), Is.True);
        Assert.That(Invocar<int>(gerente, "ObterDonoDoPonto", ponto), Is.EqualTo(3));

        object consultaDepois = Invocar<object>(gerente, "ObterTerritorioNaPosicao", ponto);
        object regiaoAsset = Invocar<object>(mapa, "EncontrarRegiao", territorioId);
        Assert.That(GetFieldValue<int>(consultaDepois, "ownerCountryTeamId"), Is.EqualTo(3));
        Assert.That(GetFieldValue<bool>(consultaDepois, "neutral"), Is.False);
        Assert.That(GetFieldValue<int>(regiaoAsset, "ownerCountryTeamId"), Is.EqualTo(0));
        Assert.That(GetFieldValue<bool>(regiaoAsset, "neutral"), Is.True);
    }

    [Test]
    public void FundarTerritorio_NaoTomaRegiaoDeOutroTimeNemAgua()
    {
        Vector2 uvInimiga = EncontrarUv(territorio =>
            GetFieldValue<int>(territorio, "ownerCountryTeamId") > 0
            && GetFieldValue<object>(territorio, "tipo").ToString() == "Terra");
        Vector2 uvAgua = EncontrarUv(territorio =>
            GetFieldValue<object>(territorio, "tipo").ToString() == "AguasTerritoriais");
        Vector3 pontoInimigo = Invocar<Vector3>(gerente, "MapUvToWorld", uvInimiga);
        Vector3 pontoAgua = Invocar<Vector3>(gerente, "MapUvToWorld", uvAgua);

        Assert.That(Invocar<bool>(gerente, "TentarFundarTerritorio", pontoInimigo, 3), Is.False);
        Assert.That(Invocar<bool>(gerente, "TentarFundarTerritorio", pontoAgua, 3), Is.False);
        Assert.That(Invocar<int>(gerente, "ObterDonoDoPonto", pontoInimigo), Is.Not.EqualTo(3));
    }

    [Test]
    public void FundarTerritorio_RejeitaTimeInvalido()
    {
        Vector2 uvNeutra = EncontrarUv(territorio =>
            GetFieldValue<string>(territorio, "territorioId") == "ilha-central");
        Vector3 ponto = Invocar<Vector3>(gerente, "MapUvToWorld", uvNeutra);

        Assert.That(Invocar<bool>(gerente, "TentarFundarTerritorio", ponto, 0), Is.False);
        Assert.That(Invocar<int>(gerente, "ObterDonoDoPonto", ponto), Is.EqualTo(0));
    }

    [Test]
    public void NotificarConstrucao_UsaTimeJogadorMesmoSePrefabTiverOutroTime()
    {
        Type expansaoType = ResolveType("GerenciadorExpansaoFronteira");
        if (GetStaticMemberValue(expansaoType, "Instancia") != null)
            Assert.Ignore("Há um GerenciadorExpansaoFronteira ativo; o teste não vai interferir no estado da cena atual.");

        expansaoObject = new GameObject("Teste_ExpansaoFronteira");
        expansaoObject.SetActive(false);
        SceneManager.MoveGameObjectToScene(expansaoObject, previewScene);

        GameObject zonaObject = new GameObject("Teste_ZonaFronteira");
        zonaObject.SetActive(false);
        SceneManager.MoveGameObjectToScene(zonaObject, previewScene);
        zonaObject.transform.SetParent(expansaoObject.transform, false);
        zonaObject.AddComponent(ResolveType("ZonaFronteiraExpansionavel"));

        edificacaoObject = new GameObject("Prefeitura teste");
        edificacaoObject.SetActive(false);
        SceneManager.MoveGameObjectToScene(edificacaoObject, previewScene);
        Component identidade = edificacaoObject.AddComponent(ResolveType("IdentidadeUnidade"));
        SetFieldValue(identidade, "teamID", 1);
        edificacaoObject.SetActive(true);

        Component expansao = expansaoObject.AddComponent(expansaoType);
        expansaoObject.SetActive(true);

        MethodInfo notificar = expansaoType.GetMethod(
            "NotificarConstrucao",
            InstanceFlags,
            null,
            new[] { typeof(GameObject), typeof(Vector3), typeof(int) },
            null);
        Assert.That(notificar, Is.Not.Null);
        Assert.That((bool)notificar.Invoke(expansao, new object[] { edificacaoObject, Vector3.zero, 3 }), Is.True);
        Assert.That(GetPropertyValue<int>(zonaObject.GetComponent(ResolveType("ZonaFronteiraExpansionavel")), "TeamDono"), Is.EqualTo(3));
    }

    private Vector2 EncontrarUv(Predicate<object> corresponde)
    {
        MethodInfo consultarUv = mapa.GetType().GetMethod("ConsultarUv", InstanceFlags);
        Assert.That(consultarUv, Is.Not.Null);

        for (int y = 1; y < 128; y++)
        {
            for (int x = 1; x < 256; x++)
            {
                Vector2 uv = new Vector2(x / 256f, y / 128f);
                object territorio = consultarUv.Invoke(mapa, new object[] { uv });
                if (corresponde(territorio)) return uv;
            }
        }

        Assert.Fail("Não foi possível encontrar uma amostra para a região territorial solicitada.");
        return Vector2.zero;
    }

    private static Type ResolveType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName, false);
            if (type != null) return type;
        }

        Assert.Fail("Tipo não encontrado: " + fullName);
        return null;
    }

    private static object GetStaticMemberValue(Type type, string memberName)
    {
        FieldInfo field = type.GetField(memberName, StaticFlags);
        if (field != null) return field.GetValue(null);
        PropertyInfo property = type.GetProperty(memberName, StaticFlags);
        return property != null ? property.GetValue(null) : null;
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, target.GetType().FullName + "." + propertyName);
        return (T)property.GetValue(target);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + fieldName);
        return (T)field.GetValue(target);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + fieldName);
        field.SetValue(target, value);
    }

    private static T Invocar<T>(object alvo, string nome, params object[] argumentos)
    {
        MethodInfo metodo = alvo.GetType().GetMethod(nome, InstanceFlags);
        Assert.That(metodo, Is.Not.Null, alvo.GetType().FullName + "." + nome);
        return (T)metodo.Invoke(alvo, argumentos);
    }
}
#endif
