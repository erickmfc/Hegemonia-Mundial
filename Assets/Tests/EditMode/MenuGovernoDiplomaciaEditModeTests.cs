#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// This test assembly intentionally uses reflection: gameplay scripts remain in
// Unity's predefined Assembly-CSharp and cannot be referenced by an asmdef.
public sealed class MenuGovernoDiplomaciaEditModeTests
{
    private GameObject governoObject;
    private GameObject identidadeObject;
    private Type governoType;
    private Type dadosPaisType;
    private Type relacaoType;
    private Type identidadeType;
    private object governo;
    private object jogador;
    private object alvo;
    private IList relacoes;
    private IList noticias;
    private FieldInfo identidadesField;
    private IList listaIdentidadesOriginal;
    private object[] snapshotIdentidades;
    private Type registroNacoesType;
    private FieldInfo instanciaRegistroField;
    private object registroOriginal;
    private GameObject registroObject;

    [SetUp]
    public void SetUp()
    {
        governoType = FindGameplayType("SistemaGovernoMundial");
        dadosPaisType = FindGameplayType("DadosPaisGoverno");
        relacaoType = FindGameplayType("RelacaoPaisGoverno");
        identidadeType = FindGameplayType("IdentidadeIA");
        identidadesField = identidadeType.GetField("TodasIdentidades", BindingFlags.Public | BindingFlags.Static);
        registroNacoesType = FindGameplayType("RegistroNacoesGoverno");
        instanciaRegistroField = registroNacoesType.GetField("<Instancia>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        registroOriginal = instanciaRegistroField.GetValue(null);
        listaIdentidadesOriginal = (IList)identidadesField.GetValue(null);
        snapshotIdentidades = listaIdentidadesOriginal != null
            ? listaIdentidadesOriginal.Cast<object>().ToArray()
            : Array.Empty<object>();
        identidadesField.SetValue(null, CreateList(identidadeType));

        // Supply the runtime registry without invoking DontDestroyOnLoad from EditMode.
        registroObject = new GameObject("Teste_RegistroNacoes_MenuGoverno");
        registroObject.SetActive(false);
        object registro = registroObject.AddComponent(registroNacoesType);
        instanciaRegistroField.SetValue(null, registro);

        governoObject = new GameObject("Teste_MenuGoverno_Diplomacia");
        governoObject.SetActive(false);
        governo = governoObject.AddComponent(governoType);
        Set(governo, "teamJogador", 1);
        jogador = CreatePais(1, "Pais do Jogador");
        alvo = CreatePais(2, "Pais Alvo");
        IList paises = GetList(governo, "paises");
        paises.Clear();
        paises.Add(jogador);
        paises.Add(alvo);
        relacoes = GetList(governo, "relacoes");
        relacoes.Clear();
        noticias = GetList(governo, "noticias");
        noticias.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        if (identidadeObject != null) UnityEngine.Object.DestroyImmediate(identidadeObject);
        if (governoObject != null) UnityEngine.Object.DestroyImmediate(governoObject);
        instanciaRegistroField.SetValue(null, registroOriginal);
        if (registroObject != null) UnityEngine.Object.DestroyImmediate(registroObject);

        IList restored = CreateList(identidadeType);
        if (listaIdentidadesOriginal != null)
        {
            listaIdentidadesOriginal.Clear();
            foreach (object identidade in snapshotIdentidades) listaIdentidadesOriginal.Add(identidade);
            restored = listaIdentidadesOriginal;
        }
        identidadesField.SetValue(null, restored);
    }

    [Test]
    public void SancaoContraOProprioPaisERecusadaSemAlterarDados()
    {
        (bool aplicada, string mensagem) = CallWithMessage("TentarAplicarSancao", 1);

        Assert.That(aplicada, Is.False);
        Assert.That(mensagem, Is.Not.Empty);
        Assert.That(Get<bool>(jogador, "sancionado"), Is.False);
        Assert.That(Get<float>(jogador, "estabilidade"), Is.EqualTo(80f));
        Assert.That(relacoes, Is.Empty);
    }

    [Test]
    public void SancaoContraPaisInativoERecusadaSemCriarRelacao()
    {
        CriarIdentidadeAlvo(ativa: false);

        (bool aplicada, string mensagem) = CallWithMessage("TentarAplicarSancao", 2);

        Assert.That(aplicada, Is.False);
        Assert.That(mensagem, Is.Not.Empty);
        Assert.That(Get<bool>(alvo, "sancionado"), Is.False);
        Assert.That(Get<float>(alvo, "estabilidade"), Is.EqualTo(80f));
        Assert.That(relacoes, Is.Empty);
    }

    [Test]
    public void RomperAcordoSemRelacaoRegistradaNaoCriaRelacaoNemAplicaPenalidade()
    {
        CriarIdentidadeAlvo(ativa: true);

        (bool rompido, string mensagem) = CallWithMessage("TentarRomperAcordo", 2);

        Assert.That(rompido, Is.False);
        Assert.That(mensagem, Does.Contain("Nao existe"));
        Assert.That(relacoes, Is.Empty);
        Assert.That(noticias, Is.Empty);
    }

    [Test]
    public void RomperAcordoEncerraTratadosAtivosEAplicaPenalidadeUmaVez()
    {
        CriarIdentidadeAlvo(ativa: true);
        object relacao = Activator.CreateInstance(relacaoType);
        Set(relacao, "teamA", 1);
        Set(relacao, "teamB", 2);
        Set(relacao, "valor", 60);
        Set(relacao, "pactoMilitar", true);
        Set(relacao, "tratadoComercial", true);
        relacoes.Add(relacao);

        (bool rompido, string mensagem) = CallWithMessage("TentarRomperAcordo", 2);

        Assert.That(rompido, Is.True);
        Assert.That(Get<bool>(relacao, "pactoMilitar"), Is.False);
        Assert.That(Get<bool>(relacao, "tratadoComercial"), Is.False);
        Assert.That(Get<int>(relacao, "valor"), Is.EqualTo(38));
        Assert.That(mensagem, Does.Contain("Pais Alvo"));
        Assert.That(noticias, Has.Count.EqualTo(1));
    }

    [Test]
    public void LevantarSancaoInexistenteNaoCriaRelacao()
    {
        CriarIdentidadeAlvo(ativa: true);

        (bool removida, string mensagem) = CallWithMessage("TentarRemoverSancao", 2);

        Assert.That(removida, Is.False);
        Assert.That(mensagem, Does.Contain("Nao existe sancao"));
        Assert.That(relacoes, Is.Empty);
        Assert.That(Get<bool>(alvo, "sancionado"), Is.False);
    }

    [Test]
    public void LevantarMinhaSancaoPreservaSancaoDiplomaticaDeOutroPais()
    {
        CriarIdentidadeAlvo(ativa: true);
        relacoes.Add(CriarRelacao(1, 2, -20, true));
        relacoes.Add(CriarRelacao(3, 2, 0, true));
        Set(alvo, "sancionado", true);

        (bool removida, string mensagem) = CallWithMessage("TentarRemoverSancao", 2);

        Assert.That(removida, Is.True, mensagem);
        Assert.That(Get<bool>(relacoes[0], "sancaoAtiva"), Is.False);
        Assert.That(Get<bool>(relacoes[1], "sancaoAtiva"), Is.True);
        Assert.That(Get<bool>(alvo, "sancionado"), Is.True);
    }

    private object CreatePais(int teamId, string nome)
    {
        object pais = Activator.CreateInstance(dadosPaisType);
        Set(pais, "teamId", teamId);
        Set(pais, "nomePais", nome);
        Set(pais, "estabilidade", 80f);
        return pais;
    }

    private object CriarRelacao(int teamA, int teamB, int valor, bool sancao)
    {
        object relacao = Activator.CreateInstance(relacaoType);
        Set(relacao, "teamA", teamA);
        Set(relacao, "teamB", teamB);
        Set(relacao, "valor", valor);
        Set(relacao, "sancaoAtiva", sancao);
        return relacao;
    }

    private void CriarIdentidadeAlvo(bool ativa)
    {
        identidadeObject = new GameObject("Teste_Identidade_MenuGoverno");
        identidadeObject.SetActive(false);
        Component identidade = identidadeObject.AddComponent(identidadeType);
        Set(identidade, "teamID", 2);
        Set(identidade, "estaAtivo", ativa);
        Set(identidade, "eliminado", false);
        identidadeObject.SetActive(true);

        // EditMode tests do not consistently invoke MonoBehaviour.OnEnable, so
        // register the fixture explicitly in the same collection used by the
        // runtime diplomatic target validation.
        IList identidades = (IList)identidadesField.GetValue(null);
        if (!identidades.Contains(identidade)) identidades.Add(identidade);
        Assert.That(identidades.Cast<Component>().Any(item => item != null
            && Get<int>(item, "teamID") == 2
            && Get<bool>(item, "estaAtivo") == ativa
            && !Get<bool>(item, "eliminado")), Is.True,
            "The diplomatic test target must be registered with the requested active state.");
    }

    private (bool sucesso, string mensagem) CallWithMessage(string methodName, int teamId)
    {
        Assert.That(registroNacoesType.GetProperty("Instancia").GetValue(null), Is.Not.Null,
            "The edit-mode fixture must keep a national registry available.");
        object[] args = { teamId, null };
        MethodInfo method = governoType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        bool sucesso = (bool)method.Invoke(governo, args);
        return (sucesso, (string)args[1]);
    }

    private static Type FindGameplayType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, throwOnError: false))
            .First(type => type != null);
    }

    private static IList CreateList(Type elementType)
    {
        return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
    }

    private static IList GetList(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        object current = field.GetValue(target);
        if (current != null) return (IList)current;
        IList created = CreateList(field.FieldType.GetGenericArguments()[0]);
        field.SetValue(target, created);
        return created;
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);
    }

    private static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(target);
    }
}
#endif
