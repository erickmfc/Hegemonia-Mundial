using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EquipeConvesEnterpriseEditModeTests
{
    [Test]
    public void IntervalosReservamSaidaSeparadaDaAeronaveAnterior()
    {
        Type equipeType = ResolveType("EquipeConvesEnterprise");
        MethodInfo calcular = equipeType.GetMethod("CalcularProximoInicio", BindingFlags.Public | BindingFlags.Static);
        Assert.That(calcular, Is.Not.Null);
        Assert.That(calcular.Invoke(null, new object[] { 10f, 0f, 3f }), Is.EqualTo(13f));
        Assert.That(calcular.Invoke(null, new object[] { 10f, 12f, 3f }), Is.EqualTo(15f));
        Assert.That(calcular.Invoke(null, new object[] { 10f, 12f, -1f }), Is.EqualTo(12f));
    }

    [Test]
    public void CompletaSoOsMarcosAusentesDaSegundaCatapulta()
    {
        var navio = new GameObject("EnterpriseTest");
        var layoutObject = new GameObject("Layout");
        var decolagem = new GameObject("Decolagem");
        var catapulta1 = new GameObject("Catapulta_01");
        var filaCatapulta1 = new GameObject("Fila");
        var equipeObject = new GameObject("Equipe");
        try
        {
            Type layoutType = ResolveType("LayoutConvesPortaAvioesV2");
            Type equipeType = ResolveType("EquipeConvesEnterprise");
            layoutObject.transform.SetParent(navio.transform);
            decolagem.transform.SetParent(layoutObject.transform);
            catapulta1.transform.SetParent(layoutObject.transform);
            filaCatapulta1.transform.SetParent(catapulta1.transform);
            Component layout = layoutObject.AddComponent(layoutType);
            layoutType.GetField("decolagem").SetValue(layout, decolagem.transform);
            Component equipe = equipeObject.AddComponent(equipeType);

            CriarPonto(decolagem.transform, "Fila", new Vector3(0f, 0f, 0f));
            CriarPonto(decolagem.transform, "Alinhamento", new Vector3(0f, 0f, 10f));
            CriarPonto(decolagem.transform, "Liberacao", new Vector3(0f, 0f, 20f));
            CriarPonto(decolagem.transform, "Subida_Inicial", new Vector3(0f, 5f, 40f));
            CriarPonto(decolagem.transform, "Saida_Voo", new Vector3(0f, 10f, 100f));
            CriarPonto(decolagem.transform, "Alinhamento (1)", new Vector3(12f, 0f, 10f));
            CriarPonto(decolagem.transform, "Liberacao (1)", new Vector3(12f, 0f, 20f));

            equipeType.GetMethod("GarantirRotaSegundaCatapulta").Invoke(equipe, new object[] { layout });

            Assert.That(decolagem.transform.Find("Fila (1)"), Is.Not.Null);
            Assert.That(decolagem.transform.Find("Subida_Inicial (1)"), Is.Not.Null);
            Assert.That(decolagem.transform.Find("Saida_Voo (1)"), Is.Not.Null);
            Assert.That(decolagem.transform.Find("Fila (1)").position.x, Is.EqualTo(12f).Within(0.001f));
            Assert.That(decolagem.transform.Find("Subida_Inicial (1)").position.z, Is.EqualTo(40f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(navio);
            UnityEngine.Object.DestroyImmediate(equipeObject);
        }
    }

    [Test]
    public void CorrigePontosLegadosDaEquipeParaOPlanoDoConves()
    {
        Type equipeType = ResolveType("EquipeConvesEnterprise");
        MethodInfo nivelamento = equipeType.GetMethod("NivelarPontosAoConves", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(nivelamento, Is.Not.Null);

        var navio = new GameObject("EnterpriseDeckTest");
        var layoutObject = new GameObject("Layout");
        var grupo = new GameObject("Trablhadores");
        var postoSinal = new GameObject("Sinal");
        var postoPatrulha = new GameObject("Patrulha");
        try
        {
            layoutObject.transform.SetParent(navio.transform);
            grupo.transform.SetParent(navio.transform);
            postoSinal.transform.SetParent(grupo.transform);
            postoPatrulha.transform.SetParent(grupo.transform);
            postoSinal.transform.position = new Vector3(1f, -81.8f, 2f);
            postoPatrulha.transform.position = new Vector3(3f, -40f, 4f);

            Type layoutType = ResolveType("LayoutConvesPortaAvioesV2");
            Component layout = layoutObject.AddComponent(layoutType);
            var referencia = new GameObject("ReferenciaConves").transform;
            referencia.SetParent(layoutObject.transform);
            referencia.position = new Vector3(0f, 20.18f, 0f);
            layoutType.GetField("referenciaConves").SetValue(layout, referencia);

            nivelamento.Invoke(null, new object[] { grupo.transform, layout });

            Assert.That(postoSinal.transform.position.y, Is.EqualTo(20.18f).Within(0.001f));
            Assert.That(postoPatrulha.transform.position.y, Is.EqualTo(20.18f).Within(0.001f));
            Assert.That(postoPatrulha.transform.position.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(postoPatrulha.transform.position.z, Is.EqualTo(4f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(navio);
            UnityEngine.Object.DestroyImmediate(layoutObject);
            UnityEngine.Object.DestroyImmediate(grupo);
        }
    }

    private static Transform CriarPonto(Transform pai, string nome, Vector3 posicao)
    {
        var ponto = new GameObject(nome).transform;
        ponto.SetParent(pai, false);
        ponto.localPosition = posicao;
        return ponto;
    }

    private static Type ResolveType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Tipo não encontrado: " + name);
        return type;
    }
}
