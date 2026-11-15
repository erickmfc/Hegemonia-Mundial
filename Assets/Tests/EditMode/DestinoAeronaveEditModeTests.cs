#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DestinoAeronaveEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
    private static object Read(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);

    [Test]
    public void DestinoManualEBaseEstacionadaSaoEstadosSeparados()
    {
        GameObject aviaoObject = new GameObject("AeronaveTeste");
        GameObject baseAtualObject = new GameObject("BaseAtual");
        GameObject destinoObject = new GameObject("BaseDestino");
        try
        {
            Type aviaoType = TypeOf("ControleAviao");
            Component aviao = aviaoObject.AddComponent(aviaoType);
            Component baseAtual = baseAtualObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            Component destino = destinoObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            aviaoType.GetMethod("RestaurarBasesSalvas").Invoke(aviao, new object[] { baseAtual, destino, true, false });
            Assert.AreSame(baseAtual, Read(aviao, "baseEstacionamentoAtual"));
            Assert.AreSame(destino, aviaoType.GetProperty("DestinoPousoEscolhido").GetValue(aviao));
            Assert.AreSame(baseAtual, Read(aviao, "aeroportoOrigem"));
            StringAssert.Contains("BaseAtual", (string)aviaoType.GetMethod("ObterResumoLocalizacao").Invoke(aviao, null));
            StringAssert.Contains("BaseDestino", (string)aviaoType.GetMethod("ObterResumoLocalizacao").Invoke(aviao, null));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(aviaoObject);
            UnityEngine.Object.DestroyImmediate(baseAtualObject);
            UnityEngine.Object.DestroyImmediate(destinoObject);
        }
    }

    [Test]
    public void DestinosManuaisRestringemEquipe()
    {
        GameObject aviaoObject = new GameObject("AeronaveTeste");
        GameObject aliadaObject = new GameObject("BaseAliada");
        GameObject inimigaObject = new GameObject("BaseInimiga");
        try
        {
            Component aviao = aviaoObject.AddComponent(TypeOf("ControleAviao"));
            Component idAviao = aviaoObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Component baseAliada = aliadaObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            Component idAliada = aliadaObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Component baseInimiga = inimigaObject.AddComponent(TypeOf("GerenciadorAeroporto"));
            Component idInimiga = inimigaObject.AddComponent(TypeOf("IdentidadeUnidade"));
            Type identidadeType = TypeOf("IdentidadeUnidade");
            identidadeType.GetField("teamID").SetValue(idAviao, 4);
            identidadeType.GetField("teamID").SetValue(idAliada, 4);
            identidadeType.GetField("teamID").SetValue(idInimiga, 5);
            Type aviaoType = TypeOf("ControleAviao");
            Assert.IsTrue((bool)Call(aviaoType, "BasesDaMesmaEquipe", aviao, baseAliada));
            Assert.IsFalse((bool)Call(aviaoType, "BasesDaMesmaEquipe", aviao, baseInimiga));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(aviaoObject);
            UnityEngine.Object.DestroyImmediate(aliadaObject);
            UnityEngine.Object.DestroyImmediate(inimigaObject);
        }
    }

    [Test]
    public void ApoioAereoAliadoPermitePousoTemporarioEPreservaBaseOriginal()
    {
        Type aviaoType = TypeOf("ControleAviao");
        Type aeroportoType = TypeOf("GerenciadorAeroporto");
        Type identidadeType = TypeOf("IdentidadeUnidade");
        Type governoType = TypeOf("SistemaGovernoMundial");
        Type paisType = TypeOf("DadosPaisGoverno");
        Type relacaoType = TypeOf("RelacaoPaisGoverno");
        Type contextoType = TypeOf("ContextoTerritorialDiplomatico");
        Type iaType = TypeOf("IdentidadeIA");

        PropertyInfo instanciaGovProp = governoType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        object governoOriginal = instanciaGovProp.GetValue(null);
        FieldInfo todasIAsField = iaType.GetField("TodasIdentidades", BindingFlags.Public | BindingFlags.Static);
        System.Collections.IList listaIAsOriginal = (System.Collections.IList)todasIAsField.GetValue(null);
        object[] snapshotIAs = listaIAsOriginal != null ? listaIAsOriginal.Cast<object>().ToArray() : Array.Empty<object>();

        GameObject govObj = new GameObject("Teste_Governo_ApoioAliado");
        govObj.SetActive(false);
        GameObject iaObj = new GameObject("Teste_IAAliada");
        iaObj.SetActive(false);
        GameObject aviaoObj = new GameObject("CacaJogador");
        GameObject basePropriaObj = new GameObject("BaseJogador");
        GameObject baseAliadaObj = new GameObject("AeroportoAliado");
        GameObject wpDecida1 = new GameObject("Decida1");
        GameObject wpDecida2 = new GameObject("Decida2");
        GameObject wpPatio1 = new GameObject("Vaga1");
        GameObject wpPropDec1 = new GameObject("PropDec1");
        GameObject wpPropDec2 = new GameObject("PropDec2");

        try
        {
            object gov = govObj.AddComponent(governoType);
            instanciaGovProp.SetValue(null, gov);
            governoType.GetField("teamJogador", Members).SetValue(gov, 1);

            System.Collections.IList paises = (System.Collections.IList)governoType.GetField("paises", Members).GetValue(gov);
            object pais1 = Activator.CreateInstance(paisType);
            paisType.GetField("teamId", Members).SetValue(pais1, 1);
            paisType.GetField("nomePais", Members).SetValue(pais1, "Brasil");
            object pais2 = Activator.CreateInstance(paisType);
            paisType.GetField("teamId", Members).SetValue(pais2, 2);
            paisType.GetField("nomePais", Members).SetValue(pais2, "Nacao Aliada");
            paises.Add(pais1);
            paises.Add(pais2);

            System.Collections.IList relacoes = (System.Collections.IList)governoType.GetField("relacoes", Members).GetValue(gov);
            object relacao = Activator.CreateInstance(relacaoType);
            relacaoType.GetField("teamA", Members).SetValue(relacao, 1);
            relacaoType.GetField("teamB", Members).SetValue(relacao, 2);
            relacaoType.GetField("pactoMilitar", Members).SetValue(relacao, true);
            relacaoType.GetField("valor", Members).SetValue(relacao, 75);
            relacoes.Add(relacao);

            Component iaAliada = iaObj.AddComponent(iaType);
            iaType.GetField("teamID", Members).SetValue(iaAliada, 2);
            iaType.GetField("estaAtivo", Members).SetValue(iaAliada, true);
            iaType.GetField("eliminado", Members).SetValue(iaAliada, false);
            if (listaIAsOriginal != null && !listaIAsOriginal.Contains(iaAliada)) listaIAsOriginal.Add(iaAliada);

            wpPropDec1.transform.SetParent(basePropriaObj.transform);
            wpPropDec2.transform.SetParent(basePropriaObj.transform);
            wpDecida1.transform.SetParent(baseAliadaObj.transform);
            wpDecida2.transform.SetParent(baseAliadaObj.transform);
            wpPatio1.transform.SetParent(baseAliadaObj.transform);

            Component idBasePropria = basePropriaObj.AddComponent(identidadeType);
            identidadeType.GetField("teamID").SetValue(idBasePropria, 1);
            Component basePropria = basePropriaObj.AddComponent(aeroportoType);
            var decProp = (System.Collections.IList)aeroportoType.GetField("waypointsDecida", Members).GetValue(basePropria);
            decProp.Add(wpPropDec1.transform);
            decProp.Add(wpPropDec2.transform);

            Component idBaseAliada = baseAliadaObj.AddComponent(identidadeType);
            identidadeType.GetField("teamID").SetValue(idBaseAliada, 2);
            Component baseAliada = baseAliadaObj.AddComponent(aeroportoType);
            var decAliada = (System.Collections.IList)aeroportoType.GetField("waypointsDecida", Members).GetValue(baseAliada);
            decAliada.Add(wpDecida1.transform);
            decAliada.Add(wpDecida2.transform);
            var patioAliada = (System.Collections.IList)aeroportoType.GetField("waypointsPatio", Members).GetValue(baseAliada);
            patioAliada.Add(wpPatio1.transform);

            Component idAviao = aviaoObj.AddComponent(identidadeType);
            identidadeType.GetField("teamID").SetValue(idAviao, 1);
            Component aviao = aviaoObj.AddComponent(aviaoType);
            aviaoType.GetField("aeroportoOrigem", Members).SetValue(aviao, basePropria);
            Type estadoEnum = aviaoType.GetNestedType("EstadoAviao", BindingFlags.Public);
            aviaoType.GetField("estadoAtual", Members).SetValue(aviao, Enum.Parse(estadoEnum, "EmMissao"));

            // Strict ownership check stays false between team 1 and team 2
            Assert.IsFalse((bool)Call(aviaoType, "BasesDaMesmaEquipe", aviao, baseAliada));
            // Pre-authorization: aliado exists (exigirAutorizacaoAtiva = false), not yet authorized (true)
            Assert.IsTrue((bool)Call(aviaoType, "PodeUsarBaseAliadaParaApoio", aviao, baseAliada, false));
            Assert.IsFalse((bool)Call(aviaoType, "PodeUsarBaseAliadaParaApoio", aviao, baseAliada, true));

            object[] argsPedido = { 2, null, 15 };
            bool concedido = (bool)governoType.GetMethod("SolicitarApoioReabastecimentoAliadoJogador", Members).Invoke(gov, argsPedido);
            Assert.IsTrue(concedido, "Pedido aliado com pacto militar deve ser aprovado.");
            Assert.IsTrue((bool)Call(contextoType, "ReabastecimentoAliadoAutorizado", 1, 2));
            Assert.IsTrue((bool)Call(aviaoType, "PodeUsarBaseAliadaParaApoio", aviao, baseAliada, true));
            Assert.IsFalse((bool)Call(aviaoType, "BasesDaMesmaEquipe", aviao, baseAliada));

            bool desviou = (bool)aviaoType.GetMethod("SolicitarPousoReabastecimentoAliado", Members).Invoke(aviao, new object[] { baseAliada });
            Assert.IsTrue(desviou);
            Assert.AreEqual(1, (int)identidadeType.GetField("teamID").GetValue(idAviao));
            Assert.AreSame(basePropria, Read(aviao, "baseOrigemAnteriorAoApoioAliado"));
            Assert.AreSame(baseAliada, Read(aviao, "aeroportoOrigem"));
            Assert.IsTrue((bool)Read(aviao, "retomarMissaoAposAbastecer"));

            // Simulate post-refuel launch exiting the allied airport back to home base
            var patioList = (System.Collections.IList)aeroportoType.GetField("avioesNoPatio", Members).GetValue(baseAliada);
            patioList.Add(aviao);
            aviaoType.GetMethod("AplicarBaseDestinoRetornoPendente", Members).Invoke(aviao, null);
            Assert.IsFalse(patioList.Contains(aviao));
            Assert.AreSame(basePropria, Read(aviao, "aeroportoOrigem"));
            Assert.IsNull(Read(aviao, "baseOrigemAnteriorAoApoioAliado"));
        }
        finally
        {
            instanciaGovProp.SetValue(null, governoOriginal);
            if (listaIAsOriginal != null)
            {
                listaIAsOriginal.Clear();
                foreach (object item in snapshotIAs) listaIAsOriginal.Add(item);
            }
            UnityEngine.Object.DestroyImmediate(aviaoObj);
            UnityEngine.Object.DestroyImmediate(basePropriaObj);
            UnityEngine.Object.DestroyImmediate(baseAliadaObj);
            UnityEngine.Object.DestroyImmediate(iaObj);
            UnityEngine.Object.DestroyImmediate(govObj);
        }
    }

    [Test]
    public void ApoioAereoAliadoRecusaNacaoEmGuerra()
    {
        Type aviaoType = TypeOf("ControleAviao");
        Type aeroportoType = TypeOf("GerenciadorAeroporto");
        Type identidadeType = TypeOf("IdentidadeUnidade");
        Type governoType = TypeOf("SistemaGovernoMundial");
        Type paisType = TypeOf("DadosPaisGoverno");
        Type relacaoType = TypeOf("RelacaoPaisGoverno");
        Type iaType = TypeOf("IdentidadeIA");

        PropertyInfo instanciaGovProp = governoType.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
        object governoOriginal = instanciaGovProp.GetValue(null);
        FieldInfo todasIAsField = iaType.GetField("TodasIdentidades", BindingFlags.Public | BindingFlags.Static);
        System.Collections.IList listaIAsOriginal = (System.Collections.IList)todasIAsField.GetValue(null);
        object[] snapshotIAs = listaIAsOriginal != null ? listaIAsOriginal.Cast<object>().ToArray() : Array.Empty<object>();

        GameObject govObj = new GameObject("Teste_Governo_Guerra");
        govObj.SetActive(false);
        GameObject iaObj = new GameObject("Teste_IAInimiga");
        iaObj.SetActive(false);
        GameObject aviaoObj = new GameObject("CacaJogador");
        GameObject baseInimigaObj = new GameObject("AeroportoInimigo");
        GameObject wpDec1 = new GameObject("Dec1");
        GameObject wpDec2 = new GameObject("Dec2");
        GameObject wpVaga = new GameObject("Vaga1");

        try
        {
            object gov = govObj.AddComponent(governoType);
            instanciaGovProp.SetValue(null, gov);
            governoType.GetField("teamJogador", Members).SetValue(gov, 1);

            System.Collections.IList paises = (System.Collections.IList)governoType.GetField("paises", Members).GetValue(gov);
            object pais1 = Activator.CreateInstance(paisType);
            paisType.GetField("teamId", Members).SetValue(pais1, 1);
            object pais3 = Activator.CreateInstance(paisType);
            paisType.GetField("teamId", Members).SetValue(pais3, 3);
            paisType.GetField("nomePais", Members).SetValue(pais3, "Nacao Inimiga");
            paises.Add(pais1);
            paises.Add(pais3);

            System.Collections.IList relacoes = (System.Collections.IList)governoType.GetField("relacoes", Members).GetValue(gov);
            object relacao = Activator.CreateInstance(relacaoType);
            relacaoType.GetField("teamA", Members).SetValue(relacao, 1);
            relacaoType.GetField("teamB", Members).SetValue(relacao, 3);
            relacaoType.GetField("guerraDeclarada", Members).SetValue(relacao, true);
            relacaoType.GetField("valor", Members).SetValue(relacao, -80);
            relacoes.Add(relacao);

            Component iaInimiga = iaObj.AddComponent(iaType);
            iaType.GetField("teamID", Members).SetValue(iaInimiga, 3);
            iaType.GetField("estaAtivo", Members).SetValue(iaInimiga, true);
            iaType.GetField("eliminado", Members).SetValue(iaInimiga, false);
            if (listaIAsOriginal != null && !listaIAsOriginal.Contains(iaInimiga)) listaIAsOriginal.Add(iaInimiga);

            wpDec1.transform.SetParent(baseInimigaObj.transform);
            wpDec2.transform.SetParent(baseInimigaObj.transform);
            wpVaga.transform.SetParent(baseInimigaObj.transform);

            Component idBaseInimiga = baseInimigaObj.AddComponent(identidadeType);
            identidadeType.GetField("teamID").SetValue(idBaseInimiga, 3);
            Component baseInimiga = baseInimigaObj.AddComponent(aeroportoType);
            var decList = (System.Collections.IList)aeroportoType.GetField("waypointsDecida", Members).GetValue(baseInimiga);
            decList.Add(wpDec1.transform);
            decList.Add(wpDec2.transform);
            var patioList = (System.Collections.IList)aeroportoType.GetField("waypointsPatio", Members).GetValue(baseInimiga);
            patioList.Add(wpVaga.transform);

            Component idAviao = aviaoObj.AddComponent(identidadeType);
            identidadeType.GetField("teamID").SetValue(idAviao, 1);
            Component aviao = aviaoObj.AddComponent(aviaoType);

            object[] argsPedido = { 3, null, 15 };
            bool concedido = (bool)governoType.GetMethod("SolicitarApoioReabastecimentoAliadoJogador", Members).Invoke(gov, argsPedido);
            Assert.IsFalse(concedido);
            Assert.IsFalse((bool)Call(aviaoType, "PodeUsarBaseAliadaParaApoio", aviao, baseInimiga, false));
            Assert.IsFalse((bool)Call(aviaoType, "PodeUsarBaseAliadaParaApoio", aviao, baseInimiga, true));
        }
        finally
        {
            instanciaGovProp.SetValue(null, governoOriginal);
            if (listaIAsOriginal != null)
            {
                listaIAsOriginal.Clear();
                foreach (object item in snapshotIAs) listaIAsOriginal.Add(item);
            }
            UnityEngine.Object.DestroyImmediate(aviaoObj);
            UnityEngine.Object.DestroyImmediate(baseInimigaObj);
            UnityEngine.Object.DestroyImmediate(iaObj);
            UnityEngine.Object.DestroyImmediate(govObj);
        }
    }
}
#endif
