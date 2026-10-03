#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA03AcordosDiplomaticosEditModeTests
{
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private Type governoType;
    private Type gerenteType;
    private Type clockType;
    private GameObject governoObject;
    private GameObject gerenteObject;
    private GameObject clockObject;
    private Component governo;
    private Component gerente;
    private Component clock;
    private ScriptableObject mapa;
    private object gerenteAnterior;
    private object clockAnterior;

    [SetUp]
    public void SetUp()
    {
        governoType = ResolverTipo("SistemaGovernoMundial");
        gerenteType = ResolverTipo("GerenteDeTerritorio");
        clockType = ResolverTipo("GerenciadorTempo");
        gerenteAnterior = gerenteType.GetField("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        clockAnterior = clockType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);

        governoObject = new GameObject("Teste_IA03_Governo");
        governoObject.SetActive(false);
        governo = governoObject.AddComponent(governoType);

        gerenteObject = new GameObject("Teste_IA03_GerenteTerritorial");
        gerenteObject.SetActive(false);
        gerente = gerenteObject.AddComponent(gerenteType);
        mapa = ScriptableObject.CreateInstance(ResolverTipo("DadosMapaTerritorial"));
        gerenteType.GetProperty("MapaPolitico", PublicInstance).SetValue(gerente, mapa, null);
        gerenteType.GetField("Instancia", BindingFlags.Static | BindingFlags.Public).SetValue(null, gerente);

        clockObject = new GameObject("Teste_IA03_Relogio");
        clockObject.SetActive(false);
        clock = clockObject.AddComponent(clockType);
        clockType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetSetMethod(true).Invoke(null, new[] { clock });
        clockType.GetField("totalDias", PublicInstance).SetValue(clock, 40);
    }

    [TearDown]
    public void TearDown()
    {
        if (governoObject != null) UnityEngine.Object.DestroyImmediate(governoObject);
        if (gerenteObject != null) UnityEngine.Object.DestroyImmediate(gerenteObject);
        if (clockObject != null) UnityEngine.Object.DestroyImmediate(clockObject);
        if (mapa != null) UnityEngine.Object.DestroyImmediate(mapa);
        gerenteType.GetField("Instancia", BindingFlags.Static | BindingFlags.Public).SetValue(null, gerenteAnterior);
        clockType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public)
            .GetSetMethod(true).Invoke(null, new[] { clockAnterior });
    }

    [Test]
    public void DesmilitarizacaoVigoraApenasParaSignatariosEExpiraNoDiaConfigurado()
    {
        AdicionarRegiao("zona", 3, capturable: false);
        object proposta = CriarProposta("Desmilitarizacao", 2, 3, 41, new[] { "zona" }, true);
        DefinirPropostas(proposta);

        Assert.That(ConsultarZona(2, "zona"), Is.True);
        Assert.That(ConsultarZona(3, "zona"), Is.True);
        Assert.That(ConsultarZona(4, "zona"), Is.False);

        clockType.GetField("totalDias", PublicInstance).SetValue(clock, 41);
        Invocar(governo, "ProcessarAcordosTemporarios");

        Assert.That(ConsultarZona(2, "zona"), Is.False);
        Assert.That(Field(proposta, "status").ToString(), Is.EqualTo("Expirada"));
    }

    [Test]
    public void CessaoTemporariaDevolveRegiaoAoProprietarioQuandoExpira()
    {
        AdicionarRegiao("concessao", 3, capturable: true);
        Invocar(gerente, "TentarCapturarTerritorio", "concessao", 2);
        object proposta = CriarProposta("CessaoTerritorialTemporaria", 2, 3, 41, new[] { "concessao" }, false);
        DefinirPropostas(proposta);

        clockType.GetField("totalDias", PublicInstance).SetValue(clock, 41);
        Invocar(governo, "ProcessarAcordosTemporarios");

        object estado = Invocar(gerente, "ObterEstadoDaRegiao", "concessao");
        Assert.That(Field(estado, "ownerCountryTeamId"), Is.EqualTo(3));
        Assert.That(Field(proposta, "status").ToString(), Is.EqualTo("Expirada"));
    }

    [Test]
    public void ExpiracaoDaConcessaoNaoSobrescreveUmaConquistaPosterior()
    {
        AdicionarRegiao("concessao", 3, capturable: true);
        Invocar(gerente, "TentarCapturarTerritorio", "concessao", 2);
        object proposta = CriarProposta("CessaoTerritorialTemporaria", 2, 3, 41, new[] { "concessao" }, false);
        DefinirPropostas(proposta);
        Invocar(gerente, "TentarCapturarTerritorio", "concessao", 4);

        clockType.GetField("totalDias", PublicInstance).SetValue(clock, 41);
        Invocar(governo, "ProcessarAcordosTemporarios");

        object estado = Invocar(gerente, "ObterEstadoDaRegiao", "concessao");
        Assert.That(Field(estado, "ownerCountryTeamId"), Is.EqualTo(4));
        Assert.That(Field(proposta, "status").ToString(), Is.EqualTo("Expirada"));
    }

    private object CriarProposta(string tipo, int origem, int alvo, int terminaDia, IEnumerable<string> regioes, bool dmz)
    {
        Type propostaType = ResolverTipo("PropostaInternacional");
        object proposta = Activator.CreateInstance(propostaType);
        SetField(proposta, "tipo", Enum.Parse(ResolverTipo("TipoPropostaInternacional"), tipo));
        SetField(proposta, "status", Enum.Parse(ResolverTipo("StatusPropostaInternacional"), "Executada"));
        SetField(proposta, "origemTeamId", origem);
        SetField(proposta, "alvoTeamId", alvo);
        SetField(proposta, "duracaoDias", 1);
        SetField(proposta, "terminaEmDiaDeJogo", terminaDia);
        SetField(proposta, dmz ? "territoriosDesmilitarizados" : "territoriosConcedidos", new List<string>(regioes));
        return proposta;
    }

    private void DefinirPropostas(object proposta)
    {
        Type propostaType = ResolverTipo("PropostaInternacional");
        Type listaType = typeof(List<>).MakeGenericType(propostaType);
        IList lista = (IList)Activator.CreateInstance(listaType);
        lista.Add(proposta);
        governoType.GetField("propostas", PublicInstance).SetValue(governo, lista);
    }

    private bool ConsultarZona(int teamId, string id)
    {
        return (bool)Invocar(governo, "EstaRegiaoDesmilitarizada", teamId, id);
    }

    private void AdicionarRegiao(string id, int owner, bool capturable)
    {
        Type regiaoType = ResolverTipo("RegiaoPolitica");
        object regiao = Activator.CreateInstance(regiaoType);
        SetField(regiao, "territorioId", id);
        SetField(regiao, "ownerCountryTeamId", owner);
        SetField(regiao, "neutral", false);
        SetField(regiao, "capturable", capturable);
        SetField(regiao, "tipo", Enum.Parse(ResolverTipo("TipoRegiaoPolitica"), "Terra"));
        IList regioes = (IList)mapa.GetType().GetProperty("Regioes", PublicInstance).GetValue(mapa, null);
        regioes.Add(regiao);
    }

    private static Type ResolverTipo(string nome)
    {
        Type tipo = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(nome, false))
            .FirstOrDefault(candidato => candidato != null);
        Assert.That(tipo, Is.Not.Null, "Tipo não encontrado: " + nome);
        return tipo;
    }

    private static object Invocar(object alvo, string metodo, params object[] argumentos)
    {
        MethodInfo info = alvo.GetType().GetMethod(metodo, AllInstance);
        Assert.That(info, Is.Not.Null, "Método não encontrado: " + metodo);
        return info.Invoke(alvo, argumentos);
    }

    private static void SetField(object alvo, string nome, object valor)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, AllInstance);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        campo.SetValue(alvo, valor);
    }

    private static object Field(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, AllInstance);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        return campo.GetValue(alvo);
    }
}
#endif
