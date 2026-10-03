#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class IA03AvaliadorMissaoEditModeTests
{
    [Test]
    public void ChegadaAoDestinoConcluiAMissao()
    {
        Assert.That(Avaliar("ChegarAoDestino", chegou: true), Is.EqualTo("Sucesso"));
    }

    [Test]
    public void PrazoSemChegadaFalhaAMissao()
    {
        Assert.That(Avaliar("ChegarAoDestino", prazo: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void DestruicaoDoAlvoExigeConfirmacaoDoProdutorDeCombate()
    {
        Assert.That(Avaliar("DestruirAlvo", alvoDestruido: false), Is.EqualTo("Pendente"));
        Assert.That(Avaliar("DestruirAlvo", alvoDestruido: true), Is.EqualTo("Sucesso"));
    }

    [Test]
    public void CapturaDeTerritorioConcluiAMissao()
    {
        Assert.That(Avaliar("CapturarTerritorio", territorioCapturado: true), Is.EqualTo("Sucesso"));
        Assert.That(Avaliar("CapturarTerritorio", prazo: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void PermanenciaConcluiEAbandonoDoPontoFalha()
    {
        Assert.That(Avaliar("PermanecerNoDestino", permaneceu: true), Is.EqualTo("Sucesso"));
        Assert.That(Avaliar("ChegarAoDestino", falha: "PermanecerNoDestino", saiu: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void SobrevivenciaNoPrazoDependeDeTodoOGrupoOriginal()
    {
        Assert.That(Avaliar("SobreviverAteOPrazo", prazo: true, sobreviveu: true), Is.EqualTo("Sucesso"));
        Assert.That(Avaliar("SobreviverAteOPrazo", perdeuUnidade: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void ConfirmacaoExternaEsperaOHookConfigurado()
    {
        Assert.That(Avaliar("ConfirmacaoExterna"), Is.EqualTo("Pendente"));
        Assert.That(Avaliar("ConfirmacaoExterna", confirmouSucesso: true), Is.EqualTo("Sucesso"));
        Assert.That(Avaliar("ChegarAoDestino", confirmouFracasso: true), Is.EqualTo("Pendente"));
        Assert.That(Avaliar("ChegarAoDestino", falha: "ConfirmacaoExterna", confirmouFracasso: true), Is.EqualTo("Fracasso"));
    }

    [Test]
    public void TermosTemporariosPersistemNoJsonSemAlterarEnumAntigo()
    {
        Type propostaType = ResolverTipo("PropostaInternacional");
        object original = Activator.CreateInstance(propostaType);
        SetField(original, "tipo", Enum.Parse(ResolverTipo("TipoPropostaInternacional"), "CessaoTerritorialTemporaria"));
        SetField(original, "status", Enum.Parse(ResolverTipo("StatusPropostaInternacional"), "Executada"));
        SetField(original, "origemTeamId", 2);
        SetField(original, "alvoTeamId", 3);
        SetField(original, "duracaoDias", 30);
        SetField(original, "terminaEmDiaDeJogo", 41);
        SetField(original, "territoriosConcedidos", new List<string> { "regiao-teste" });

        string json = JsonUtility.ToJson(original);
        object restaurada = JsonUtility.FromJson(json, propostaType);

        Type tipoProposta = ResolverTipo("TipoPropostaInternacional");
        Assert.That(Convert.ToInt32(Enum.Parse(tipoProposta, "CessaoTerritorial")), Is.EqualTo(13));
        Assert.That(Field(restaurada, "tipo").ToString(), Is.EqualTo("CessaoTerritorialTemporaria"));
        Assert.That(Field(restaurada, "duracaoDias"), Is.EqualTo(30));
        Assert.That(Field(restaurada, "terminaEmDiaDeJogo"), Is.EqualTo(41));
        CollectionAssert.AreEqual((List<string>)Field(original, "territoriosConcedidos"), (List<string>)Field(restaurada, "territoriosConcedidos"));
    }

    private static string Avaliar(
        string sucesso,
        string falha = "SobreviverAteOPrazo",
        bool chegou = false,
        bool permaneceu = false,
        bool saiu = false,
        bool alvoDestruido = false,
        bool territorioCapturado = false,
        bool territorioPerdido = false,
        bool sobreviveu = false,
        bool perdeuUnidade = false,
        bool prazo = false,
        bool confirmouSucesso = false,
        bool confirmouFracasso = false)
    {
        Type condicaoType = ResolverTipo("Hegemonia.AI.IA03.IA03CondicaoMissao");
        Type resultadoType = ResolverTipo("Hegemonia.AI.IA03.IA03ResultadoMissao");
        Type avaliadorType = ResolverTipo("Hegemonia.AI.IA03.IA03AvaliadorMissao");
        MethodInfo metodo = avaliadorType.GetMethod("Avaliar", BindingFlags.Public | BindingFlags.Static);
        object[] argumentos =
        {
            Enum.Parse(condicaoType, sucesso),
            Enum.Parse(condicaoType, falha),
            chegou,
            permaneceu,
            saiu,
            alvoDestruido,
            territorioCapturado,
            territorioPerdido,
            sobreviveu,
            perdeuUnidade,
            prazo,
            confirmouSucesso,
            confirmouFracasso
        };

        Assert.That(metodo, Is.Not.Null);
        object resultado = metodo.Invoke(null, argumentos);
        Assert.That(resultado.GetType(), Is.EqualTo(resultadoType));
        return resultado.ToString();
    }

    private static Type ResolverTipo(string nome)
    {
        Type tipo = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(nome, false))
            .FirstOrDefault(candidato => candidato != null);
        Assert.That(tipo, Is.Not.Null, "Tipo não encontrado: " + nome);
        return tipo;
    }

    private static void SetField(object alvo, string nome, object valor)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        campo.SetValue(alvo, valor);
    }

    private static object Field(object alvo, string nome)
    {
        FieldInfo campo = alvo.GetType().GetField(nome, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(campo, Is.Not.Null, "Campo não encontrado: " + nome);
        return campo.GetValue(alvo);
    }
}
#endif
