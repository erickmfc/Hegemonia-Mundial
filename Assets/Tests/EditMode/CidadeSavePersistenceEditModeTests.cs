#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CidadeSavePersistenceEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void CidadeMaterializadaPreservaEstadoNoDtoDoSaveEntity()
    {
        GameObject cidadeObject = new GameObject("Cidade_Save_RoundTrip");
        GameObject saveObject = new GameObject("Sistema_Save_RoundTrip");
        saveObject.SetActive(false);
        try
        {
            cidadeObject.AddComponent(ResolveType("IdentidadeUnidade"));
            cidadeObject.AddComponent(ResolveType("SistemaDeDanos"));
            Component cidade = cidadeObject.AddComponent(ResolveType("CidadeComplexoUrbano"));
            // EditMode does not invoke Awake for a newly added MonoBehaviour;
            // invoke its lifecycle entry point to test the runtime registration contract.
            cidade.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cidade, null);
            Assert.That(cidadeObject.GetComponent(ResolveType("SaveableEntity")), Is.Not.Null,
                "A cidade precisa entrar no mesmo ciclo de entidades que o SaveGame restaura.");

            Set(cidade, "tema", Enum.Parse(ResolveType("TemaCidade"), "Futurista"));
            Set(cidade, "nomeCidade", "Porto Horizonte");
            Set(cidade, "teamId", 4);
            Set(cidade, "capacidadeHospitalarInicial", 250000);
            Set(cidade, "possuiRedeSaudeBasica", true);
            Set(cidade, "capacidadeHabitacional", 800000);
            Set(cidade, "populacaoResidente", 321000);
            Set(cidade, "qualidadeVida", 66);
            Set(cidade, "proporcaoComercio", 0.5f);
            Set(cidade, "rendaPorTrabalhador", 0.00012f);
            Set(cidade, "consumoEnergiaPorMorador", 0.061f);
            Set(cidade, "consumoEnergiaPorEmprego", 0.031f);
            Set(cidade, "semEnergia", true);

            Type saveEntityType = ResolveType("SaveEntityData");
            Component saveSystem = saveObject.AddComponent(ResolveType("SistemaSaveGame"));
            MethodInfo capturarEntidade = ResolveType("SistemaSaveGame").GetMethod("CapturarEntidade", Members);
            Assert.That(capturarEntidade, Is.Not.Null);
            object saveEntity = capturarEntidade.Invoke(saveSystem, new object[] { cidadeObject });
            Assert.That(Read<int>(saveEntity, "teamID"), Is.EqualTo(4));
            Assert.That(Read<bool>(saveEntity, "possuiCidadeComplexoUrbano"), Is.True);

            string json = JsonUtility.ToJson(saveEntity);
            object roundTripped = JsonUtility.FromJson(json, saveEntityType);
            Assert.That(Read<bool>(roundTripped, "possuiCidadeComplexoUrbano"), Is.True);
            Assert.That(Read<Enum>(roundTripped, "cidadeTema").ToString(), Is.EqualTo("Futurista"));
            Assert.That(Read<int>(roundTripped, "cidadeCapacidadeHospitalarInicial"), Is.EqualTo(250000));
            Assert.That(Read<bool>(roundTripped, "cidadePossuiRedeSaudeBasica"), Is.True);
            Assert.That(Read<float>(roundTripped, "cidadeProporcaoComercio"), Is.EqualTo(0.5f));

            Set(cidade, "tema", Enum.Parse(ResolveType("TemaCidade"), "Moderna"));
            Set(cidade, "nomeCidade", "Estado alterado");
            Set(cidade, "teamId", 1);
            Set(cidade, "capacidadeHospitalarInicial", 0);
            Set(cidade, "possuiRedeSaudeBasica", false);
            Set(cidade, "capacidadeHabitacional", 500000);
            Set(cidade, "populacaoResidente", 100000);
            Set(cidade, "qualidadeVida", 10);
            Set(cidade, "rendaPorTrabalhador", 0f);
            Set(cidade, "consumoEnergiaPorMorador", 0f);
            Set(cidade, "consumoEnergiaPorEmprego", 0f);
            Set(cidade, "semEnergia", false);
            Method("AplicarDadosCidade", BindingFlags.Static).Invoke(null, new[] { cidadeObject, roundTripped });

            Assert.That(Read<string>(cidade, "nomeCidade"), Is.EqualTo("Porto Horizonte"));
            Assert.That(Read<int>(cidade, "teamId"), Is.EqualTo(4));
            Assert.That(Read<int>(cidade, "capacidadeHabitacional"), Is.EqualTo(800000));
            Assert.That(Read<int>(cidade, "populacaoResidente"), Is.EqualTo(321000));
            Assert.That(Read<int>(cidade, "qualidadeVida"), Is.EqualTo(66));
            Assert.That(Read<float>(cidade, "rendaPorTrabalhador"), Is.EqualTo(0.00012f).Within(0.000001f));
            Assert.That(Read<float>(cidade, "consumoEnergiaPorMorador"), Is.EqualTo(0.061f).Within(0.000001f));
            Assert.That(Read<float>(cidade, "consumoEnergiaPorEmprego"), Is.EqualTo(0.031f).Within(0.000001f));
            Assert.That(Read<bool>(cidade, "semEnergia"), Is.True);

            Component identidade = cidadeObject.GetComponent(ResolveType("IdentidadeUnidade"));
            Assert.That(Read<int>(identidade, "teamID"), Is.EqualTo(4));
            Component saude = cidadeObject.GetComponent(ResolveType("CidadeSaudePopulacional"));
            Assert.That(Read<int>(saude, "teamId"), Is.EqualTo(4));
            Assert.That(Read<int>(saude, "capacidadeBase"), Is.EqualTo(250000));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cidadeObject);
            UnityEngine.Object.DestroyImmediate(saveObject);
        }
    }

    [Test]
    public void CidadeMaterializadaRestauraEstadoDepoisDeCapturaJsonEInstanciacaoDoPrefab()
    {
        const string prefabPath = "Assets/Prefabs/Imobiliario/city AI/Cidade Nova Aurora.prefab";
        const string expectedUniqueId = "cidade-save-roundtrip-runtime";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, "Prefab-base Nova Aurora precisa estar disponível para a restauração.");

        GameObject original = null;
        GameObject restored = null;
        GameObject saveObject = new GameObject("Sistema_Save_Restaurar_Cidade");
        saveObject.SetActive(false);
        try
        {
            original = UnityEngine.Object.Instantiate(prefab);
            Component cidadeOriginal = original.GetComponent(ResolveType("CidadeComplexoUrbano"));
            Assert.That(cidadeOriginal, Is.Not.Null);
            Set(cidadeOriginal, "tema", Enum.Parse(ResolveType("TemaCidade"), "Futurista"));
            Set(cidadeOriginal, "nomeCidade", "Nova Aurora Persistida");
            Set(cidadeOriginal, "teamId", 4);
            Set(cidadeOriginal, "capacidadeHospitalarInicial", 275000);
            Set(cidadeOriginal, "possuiRedeSaudeBasica", true);
            Set(cidadeOriginal, "capacidadeHabitacional", 1500000);
            Set(cidadeOriginal, "populacaoResidente", 725000);
            Set(cidadeOriginal, "qualidadeVida", 83);
            Set(cidadeOriginal, "rendaPorTrabalhador", 0.00021f);
            Set(cidadeOriginal, "consumoEnergiaPorMorador", 0.052f);
            Set(cidadeOriginal, "consumoEnergiaPorEmprego", 0.033f);
            Set(cidadeOriginal, "semEnergia", true);

            Component identidadeOriginal = original.GetComponent(ResolveType("IdentidadeUnidade"));
            Assert.That(identidadeOriginal, Is.Not.Null);
            Set(identidadeOriginal, "teamID", 4);
            Set(identidadeOriginal, "nomeDoPais", "Valdoria");

            Component saveSystem = saveObject.AddComponent(ResolveType("SistemaSaveGame"));
            // Mantém o teste isolado do singleton/filesystem do Awake, mas
            // fornece o snapshot que a restauração real consulta.
            Set(saveSystem, "dadosAtuais", Activator.CreateInstance(ResolveType("DadosDoJogo")));
            MethodInfo capturar = Method("CapturarEntidade", BindingFlags.Instance);
            object captura = capturar.Invoke(saveSystem, new object[] { original });
            Set(captura, "uniqueId", expectedUniqueId);
            string json = JsonUtility.ToJson(captura);
            object dadosSalvos = JsonUtility.FromJson(json, ResolveType("SaveEntityData"));

            IDictionary prefabs = (IDictionary)Read<object>(saveSystem, "prefabsPorChaveSave");
            prefabs.Add(prefab.name, prefab);
            Method("InstanciarEntidadeSalva", BindingFlags.Instance).Invoke(saveSystem, new[] { dadosSalvos });

            IDictionary saveables = (IDictionary)Read<object>(saveSystem, "saveablesPorId");
            Component saveableRestaurado = (Component)saveables[expectedUniqueId];
            Assert.That(saveableRestaurado, Is.Not.Null);
            restored = saveableRestaurado.gameObject;

            Component cidadeRestaurada = restored.GetComponent(ResolveType("CidadeComplexoUrbano"));
            Assert.That(Read<Enum>(cidadeRestaurada, "tema").ToString(), Is.EqualTo("Futurista"));
            Assert.That(Read<string>(cidadeRestaurada, "nomeCidade"), Is.EqualTo("Nova Aurora Persistida"));
            Assert.That(Read<int>(cidadeRestaurada, "teamId"), Is.EqualTo(4));
            Assert.That(Read<int>(cidadeRestaurada, "capacidadeHospitalarInicial"), Is.EqualTo(275000));
            Assert.That(Read<int>(cidadeRestaurada, "capacidadeHabitacional"), Is.EqualTo(1500000));
            Assert.That(Read<int>(cidadeRestaurada, "populacaoResidente"), Is.EqualTo(725000));
            Assert.That(Read<int>(cidadeRestaurada, "qualidadeVida"), Is.EqualTo(83));
            Assert.That(Read<bool>(cidadeRestaurada, "semEnergia"), Is.True);
            Assert.That(Read<int>(restored.GetComponent(ResolveType("IdentidadeUnidade")), "teamID"), Is.EqualTo(4));
        }
        finally
        {
            if (restored != null) UnityEngine.Object.DestroyImmediate(restored);
            if (original != null) UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(saveObject);
        }
    }

    [Test]
    public void SincronizacaoNacionalPreservaDistribuicaoLocalEAjustaSomenteVariacaoDoTotal()
    {
        GameObject cidadeAObject = new GameObject("Cidade_Migracao_A");
        GameObject cidadeBObject = new GameObject("Cidade_Migracao_B");
        Component cidadeA = null;
        Component cidadeB = null;
        try
        {
            cidadeA = CriarCidadeParaSincronizacao(cidadeAObject, "Cidade A", 250000);
            cidadeB = CriarCidadeParaSincronizacao(cidadeBObject, "Cidade B", 750000);

            SincronizarPopulacaoNacional(4, 1000000);
            AssertPopulacoes(cidadeA, 250000, cidadeB, 750000);

            SincronizarPopulacaoNacional(4, 1200000);
            AssertPopulacoes(cidadeA, 400000, cidadeB, 800000);

            SincronizarPopulacaoNacional(4, 900000);
            AssertPopulacoes(cidadeA, 300000, cidadeB, 600000);

            Assert.That(Read<int>(cidadeA, "populacaoResidente"), Is.LessThanOrEqualTo(Read<int>(cidadeA, "capacidadeHabitacional")));
            Assert.That(Read<int>(cidadeB, "populacaoResidente"), Is.LessThanOrEqualTo(Read<int>(cidadeB, "capacidadeHabitacional")));
        }
        finally
        {
            if (cidadeA != null) InvokeLifecycle(cidadeA, "OnDisable");
            if (cidadeB != null) InvokeLifecycle(cidadeB, "OnDisable");
            UnityEngine.Object.DestroyImmediate(cidadeAObject);
            UnityEngine.Object.DestroyImmediate(cidadeBObject);
        }
    }

    [Test]
    public void FluxoMigratorioMoveMoradoresParaRegiaoMaisAtrativaSemAlterarTotalNacional()
    {
        float timeScaleAntes = Time.timeScale;
        Vector3 posicaoA = new Vector3(100000f, 0f, 100000f);
        Vector3 posicaoB = new Vector3(102000f, 0f, 100000f);
        GameObject cidadeAObject = new GameObject("Cidade_Migracao_Regiao_A");
        GameObject cidadeBObject = new GameObject("Cidade_Migracao_Regiao_B");
        GameObject marcadorAObject = new GameObject("Marcador_Migracao_A");
        GameObject marcadorBObject = new GameObject("Marcador_Migracao_B");
        GameObject gerenciadorObject = new GameObject("Gerenciador_Migracao_Teste");
        Component cidadeA = null;
        Component cidadeB = null;
        try
        {
            cidadeA = CriarCidadeParaSincronizacao(cidadeAObject, "Cidade A", 500000);
            cidadeB = CriarCidadeParaSincronizacao(cidadeBObject, "Cidade B", 400000);
            cidadeAObject.transform.position = posicaoA;
            cidadeBObject.transform.position = posicaoB;

            Component marcadorA = CriarMarcadorDeMigracao(marcadorAObject, posicaoA);
            Component marcadorB = CriarMarcadorDeMigracao(marcadorBObject, posicaoB);
            Component gerenciador = gerenciadorObject.AddComponent(ResolveType("GerenciadorDivisaoTerritorial"));

            IList regioes = (IList)Read<object>(gerenciador, "cidades");
            regioes.Add(CriarRegiaoDeMigracao("A", marcadorA, 20f));
            regioes.Add(CriarRegiaoDeMigracao("B", marcadorB, 90f));

            int totalAntes = Read<int>(cidadeA, "populacaoResidente") + Read<int>(cidadeB, "populacaoResidente");
            Time.timeScale = 0f;
            InvokeLifecycle(gerenciador, "ProcessarFluxoMigratorio");
            Assert.That(Read<int>(cidadeA, "populacaoResidente"), Is.EqualTo(500000), "A migração não avança durante a pausa.");
            Assert.That(Read<int>(cidadeB, "populacaoResidente"), Is.EqualTo(400000), "A migração não avança durante a pausa.");

            Time.timeScale = 1f;
            InvokeLifecycle(gerenciador, "ProcessarFluxoMigratorio");

            int populacaoA = Read<int>(cidadeA, "populacaoResidente");
            int populacaoB = Read<int>(cidadeB, "populacaoResidente");
            Assert.That(populacaoA, Is.LessThan(500000), "A cidade menos atrativa deve ceder moradores.");
            Assert.That(populacaoB, Is.GreaterThan(400000), "A cidade mais atrativa deve receber moradores.");
            Assert.That(populacaoA + populacaoB, Is.EqualTo(totalAntes), "Migração é zero-sum para a população nacional.");
            Assert.That(populacaoA, Is.LessThanOrEqualTo(Read<int>(cidadeA, "capacidadeHabitacional")));
            Assert.That(populacaoB, Is.LessThanOrEqualTo(Read<int>(cidadeB, "capacidadeHabitacional")));
        }
        finally
        {
            Time.timeScale = timeScaleAntes;
            if (cidadeA != null) InvokeLifecycle(cidadeA, "OnDisable");
            if (cidadeB != null) InvokeLifecycle(cidadeB, "OnDisable");
            UnityEngine.Object.DestroyImmediate(cidadeAObject);
            UnityEngine.Object.DestroyImmediate(cidadeBObject);
            UnityEngine.Object.DestroyImmediate(marcadorAObject);
            UnityEngine.Object.DestroyImmediate(marcadorBObject);
            UnityEngine.Object.DestroyImmediate(gerenciadorObject);
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public void PrefeituraCriaCidadeAbstrataUmaVezEReassociaAposSaveLoad(int teamId)
    {
        GameObject managerObject = new GameObject("Gerenciador_Capital_Abstrata_Save");
        managerObject.SetActive(false);
        Component manager = managerObject.AddComponent(ResolveType("GerenciadorDivisaoTerritorial"));
        PropertyInfo instancia = ResolveType("GerenciadorDivisaoTerritorial").GetProperty("Instancia", Members);
        object instanciaAnterior = instancia.GetValue(null);
        Component marcadorInicial = null;
        Component marcadorRestaurado = null;
        try
        {
            instancia.GetSetMethod(true).Invoke(null, new object[] { manager });
            string entityId = "capital-save-load-team-" + teamId;
            marcadorInicial = CriarMarcadorDePrefeitura(
                "Capital_Inicial_Team_" + teamId,
                teamId,
                new Vector3(125f, 0f, 275f),
                entityId);
            MethodInfo registrarCidade = ResolveType("GerenciadorDivisaoTerritorial").GetMethod("RegistrarCidade", Members);
            registrarCidade.Invoke(manager, new object[] { marcadorInicial, null });
            registrarCidade.Invoke(manager, new object[] { marcadorInicial, null });

            IList cidades = (IList)Read<object>(manager, "cidades");
            object[] abstratas = cidades.Cast<object>()
                .Where(c => Read<bool>(c, "ehCidadeNaoMaterializada"))
                .ToArray();
            Assert.That(abstratas.Length, Is.EqualTo(1), "Jogador e IA usam o mesmo registro idempotente de prefeitura.");
            object cidadeInicial = abstratas[0];
            Assert.That(Read<string>(cidadeInicial, "id"), Is.EqualTo("cidade-capital-" + entityId));
            Assert.That(Read<int>(cidadeInicial, "teamID"), Is.EqualTo(teamId));
            Assert.That(Read<int>(cidadeInicial, "populacaoCivil"), Is.EqualTo(100000));
            Assert.That(Read<int>(cidadeInicial, "capacidadeHabitacional"), Is.EqualTo(2000000));
            Assert.That(Read<Vector3>(cidadeInicial, "localizacao"), Is.EqualTo(new Vector3(125f, 0f, 275f)));
            Set(cidadeInicial, "populacaoCivil", 246000);

            object save = Activator.CreateInstance(ResolveType("DadosDoJogo"));
            Set(save, "saveVersion", 20);
            Set(save, "cidadesAbstratas", ResolveType("GerenciadorDivisaoTerritorial")
                .GetMethod("CapturarCidadesAbstratas", Members).Invoke(manager, null));
            object roundTrip = JsonUtility.FromJson(JsonUtility.ToJson(save), ResolveType("DadosDoJogo"));
            object cidadesSalvas = Read<object>(roundTrip, "cidadesAbstratas");

            UnityEngine.Object.DestroyImmediate(marcadorInicial.gameObject);
            marcadorInicial = null;
            ResolveType("GerenciadorDivisaoTerritorial").GetMethod("RestaurarCidadesAbstratas", Members)
                .Invoke(manager, new[] { cidadesSalvas });

            marcadorRestaurado = CriarMarcadorDePrefeitura(
                "Capital_Restaurada_Team_" + teamId,
                teamId,
                new Vector3(125f, 0f, 275f),
                entityId);
            registrarCidade.Invoke(manager, new object[] { marcadorRestaurado, null });

            cidades = (IList)Read<object>(manager, "cidades");
            abstratas = cidades.Cast<object>()
                .Where(c => Read<bool>(c, "ehCidadeNaoMaterializada"))
                .ToArray();
            Assert.That(abstratas.Length, Is.EqualTo(1), "O marcador restaurado deve se ligar ao registro salvo, sem semear uma segunda cidade.");
            Assert.That(Read<string>(abstratas[0], "id"), Is.EqualTo("cidade-capital-" + entityId));
            Assert.That(Read<int>(abstratas[0], "populacaoCivil"), Is.EqualTo(246000), "A população salva não deve voltar ao valor inicial.");
            Assert.That(Read<Vector3>(abstratas[0], "localizacao"), Is.EqualTo(new Vector3(125f, 0f, 275f)));
            Assert.That(cidades.Cast<object>().Count(c => Read<Component>(c, "marcador") != null), Is.EqualTo(1));
            Assert.That(cidades.Cast<object>().Any(c => Read<Component>(c, "marcador") == marcadorRestaurado), Is.True,
                "O registro territorial deve apontar para o marcador restaurado.");
        }
        finally
        {
            if (marcadorRestaurado != null) UnityEngine.Object.DestroyImmediate(marcadorRestaurado.gameObject);
            if (marcadorInicial != null) UnityEngine.Object.DestroyImmediate(marcadorInicial.gameObject);
            instancia.GetSetMethod(true).Invoke(null, new[] { instanciaAnterior });
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void PrefeituraNaoRessemeiaCidadeAbstrataJaMaterializadaNoMesmoLocal()
    {
        GameObject managerObject = new GameObject("Gerenciador_Capital_Materializada");
        managerObject.SetActive(false);
        Component manager = managerObject.AddComponent(ResolveType("GerenciadorDivisaoTerritorial"));
        PropertyInfo instancia = ResolveType("GerenciadorDivisaoTerritorial").GetProperty("Instancia", Members);
        object instanciaAnterior = instancia.GetValue(null);
        GameObject cidadeObject = new GameObject("Cidade_Materializada_Capital");
        Component marcador = null;
        try
        {
            instancia.GetSetMethod(true).Invoke(null, new object[] { manager });
            cidadeObject.transform.position = new Vector3(500f, 0f, 700f);
            Component cidadeVisual = cidadeObject.AddComponent(ResolveType("CidadeComplexoUrbano"));
            Set(cidadeVisual, "nomeCidade", "Capital Materializada");
            Set(cidadeVisual, "teamId", 3);
            ResolveType("GerenciadorDivisaoTerritorial").GetMethod("RegistrarCidadeUrbana", Members)
                .Invoke(manager, new[] { cidadeVisual });

            marcador = CriarMarcadorDePrefeitura("Capital_Existente", 3, cidadeObject.transform.position, "capital-visual-id");
            ResolveType("GerenciadorDivisaoTerritorial").GetMethod("RegistrarCidade", Members)
                .Invoke(manager, new object[] { marcador, null });

            IList cidades = (IList)Read<object>(manager, "cidades");
            Assert.That(cidades.Cast<object>().Count(c => Read<bool>(c, "ehCidadeNaoMaterializada")), Is.Zero);
            Assert.That(cidades.Cast<object>().Count(c => Read<object>(c, "cidadeUrbana") != null), Is.EqualTo(1));
        }
        finally
        {
            if (marcador != null) UnityEngine.Object.DestroyImmediate(marcador.gameObject);
            UnityEngine.Object.DestroyImmediate(cidadeObject);
            instancia.GetSetMethod(true).Invoke(null, new[] { instanciaAnterior });
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void CidadeAbstrataPersisteDadosEconomicosEEstagioUrbano()
    {
        GameObject managerObject = new GameObject("Gerenciador_Cidade_Abstrata_Save");
        managerObject.SetActive(false);
        Component manager = managerObject.AddComponent(ResolveType("GerenciadorDivisaoTerritorial"));
        try
        {
            MethodInfo registrar = ResolveType("GerenciadorDivisaoTerritorial").GetMethod("RegistrarCidadeAbstrata", Members);
            object cidadeCriada = registrar.Invoke(manager, new object[]
            {
                "Porto de Dados", 4, new Vector3(250f, 0f, 400f), 750000, 1500000
            });
            Set(cidadeCriada, "nomePais", "Valdoria");
            Set(cidadeCriada, "desenvolvimento", 68f);
            Set(cidadeCriada, "importanciaEstrategica", 45f);
            Set(cidadeCriada, "estadoDano", 12f);
            Set(cidadeCriada, "producaoEconomicaMensal", 123456f);
            Set(cidadeCriada, "producaoAgricola", 8765f);
            Set(cidadeCriada, "taxaCrescimentoAnual", 0.021f);
            Set(cidadeCriada, "empregosTotais", 600000);
            Set(cidadeCriada, "populacaoEstagio2", 400000);
            Set(cidadeCriada, "populacaoEstagio3", 900000);
            Set(cidadeCriada, "populacaoEstagio4", 1400000);

            MethodInfo capturar = ResolveType("GerenciadorDivisaoTerritorial").GetMethod("CapturarCidadesAbstratas", Members);
            object snapshot = capturar.Invoke(manager, null);
            object dadosDoJogo = Activator.CreateInstance(ResolveType("DadosDoJogo"));
            Set(dadosDoJogo, "saveVersion", 19);
            Set(dadosDoJogo, "cidadesAbstratas", snapshot);
            string json = JsonUtility.ToJson(dadosDoJogo);
            object roundTrip = JsonUtility.FromJson(json, ResolveType("DadosDoJogo"));
            object cidadesRoundTrip = Read<object>(roundTrip, "cidadesAbstratas");
            ResolveType("GerenciadorDivisaoTerritorial").GetMethod("RestaurarCidadesAbstratas", Members)
                .Invoke(manager, new[] { cidadesRoundTrip });

            IList cidades = (IList)Read<object>(manager, "cidades");
            Assert.That(cidades.Count, Is.EqualTo(1));
            object restaurada = cidades[0];
            Assert.That(Read<bool>(restaurada, "ehCidadeNaoMaterializada"), Is.True);
            Assert.That(Read<string>(restaurada, "nome"), Is.EqualTo("Porto de Dados"));
            Assert.That(Read<string>(restaurada, "nomePais"), Is.EqualTo("Valdoria"));
            Assert.That(Read<int>(restaurada, "teamID"), Is.EqualTo(4));
            Assert.That(Read<int>(restaurada, "populacaoCivil"), Is.EqualTo(750000));
            Assert.That(Read<int>(restaurada, "capacidadeHabitacional"), Is.EqualTo(1500000));
            Assert.That(Read<float>(restaurada, "producaoEconomicaMensal"), Is.EqualTo(123456f));
            Assert.That(Read<float>(restaurada, "producaoAgricola"), Is.EqualTo(8765f));
            Assert.That(Read<float>(restaurada, "estadoDano"), Is.EqualTo(12f));
            PropertyInfo estagio = restaurada.GetType().GetProperty("EstagioUrbano", Members);
            Assert.That(estagio.GetValue(restaurada), Is.EqualTo(2), "Os limiares de estágio salvos são configuráveis por cidade.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void PopulacaoNacionalIncluiCidadesAbstratasSemApagarDistribuicaoLocal()
    {
        GameObject managerObject = new GameObject("Gerenciador_Cidade_Abstrata_Populacao");
        managerObject.SetActive(false);
        Component manager = managerObject.AddComponent(ResolveType("GerenciadorDivisaoTerritorial"));
        PropertyInfo instancia = ResolveType("GerenciadorDivisaoTerritorial").GetProperty("Instancia", Members);
        object instanciaAnterior = instancia.GetValue(null);
        try
        {
            instancia.GetSetMethod(true).Invoke(null, new object[] { manager });
            MethodInfo registrar = ResolveType("GerenciadorDivisaoTerritorial").GetMethod("RegistrarCidadeAbstrata", Members);
            object cidadeA = registrar.Invoke(manager, new object[] { "Abstrata A", 4, Vector3.zero, 250000, 1000000 });
            object cidadeB = registrar.Invoke(manager, new object[] { "Abstrata B", 4, Vector3.one, 750000, 1000000 });
            MethodInfo sincronizar = ResolveType("CidadeComplexoUrbano").GetMethod(
                "SincronizarPopulacaoNacional", BindingFlags.Public | BindingFlags.Static);

            sincronizar.Invoke(null, new object[] { 4, 1000000 });
            Assert.That(Read<int>(cidadeA, "populacaoCivil"), Is.EqualTo(250000));
            Assert.That(Read<int>(cidadeB, "populacaoCivil"), Is.EqualTo(750000));

            sincronizar.Invoke(null, new object[] { 4, 1200000 });
            Assert.That(Read<int>(cidadeA, "populacaoCivil"), Is.EqualTo(400000));
            Assert.That(Read<int>(cidadeB, "populacaoCivil"), Is.EqualTo(800000));

            sincronizar.Invoke(null, new object[] { 4, 900000 });
            Assert.That(Read<int>(cidadeA, "populacaoCivil"), Is.EqualTo(300000));
            Assert.That(Read<int>(cidadeB, "populacaoCivil"), Is.EqualTo(600000));
        }
        finally
        {
            instancia.GetSetMethod(true).Invoke(null, new[] { instanciaAnterior });
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void EconomiaNacionalIncluiMoradiaPopulacaoEEmpregosDeCidadesAbstratas()
    {
        Type managerType = ResolveType("GerenciadorDivisaoTerritorial");
        PropertyInfo instanciaManager = managerType.GetProperty("Instancia", Members);
        object managerAnterior = instanciaManager.GetValue(null);
        GameObject managerObject = new GameObject("Gerenciador_Cidade_Abstrata_Economia");
        managerObject.SetActive(false);
        Component manager = managerObject.AddComponent(managerType);

        GameObject economyObject = new GameObject("Economia_Cidade_Abstrata_Test");
        economyObject.SetActive(false);
        Component economySystem = economyObject.AddComponent(ResolveType("SistemaEconomiaImoveis"));
        GameObject governmentObject = new GameObject("Governo_Cidade_Abstrata_Test");
        governmentObject.SetActive(false);
        Component government = governmentObject.AddComponent(ResolveType("SistemaGovernoMundial"));
        try
        {
            instanciaManager.GetSetMethod(true).Invoke(null, new object[] { manager });
            MethodInfo registrar = managerType.GetMethod("RegistrarCidadeAbstrata", Members);
            registrar.Invoke(manager, new object[] { "Abstrata Boreal A", 4, Vector3.zero, 350000, 1000000 });
            registrar.Invoke(manager, new object[] { "Abstrata Boreal B", 4, Vector3.one, 200000, 800000 });

            ResolveType("SistemaEconomiaImoveis").GetMethod("Recalcular", Members).Invoke(economySystem, null);
            object economia = ResolveType("SistemaEconomiaImoveis").GetMethod("ObterEconomia", Members)
                .Invoke(economySystem, new object[] { 4 });

            Assert.That(economia, Is.Not.Null);
            Assert.That(Read<int>(economia, "moradiaTotal"), Is.EqualTo(1800000),
                "A capacidade abstrata precisa entrar no limite de população nacional antes do clamp econômico.");
            Assert.That(Read<int>(economia, "populacaoTotal"), Is.EqualTo(550000));
            Assert.That(Read<int>(economia, "empregosDisponiveis"), Is.EqualTo(630000),
                "Empregos das cidades abstratas fazem parte do retrato econômico nacional.");
            Assert.That(Read<int>(economia, "estruturasContadas"), Is.EqualTo(2),
                "Uma cidade abstrata ativa deve impedir que o país seja tratado como economia sem atividade.");
            Assert.That(Read<float>(economia, "receitaComercio"), Is.EqualTo(13.75f).Within(0.001f),
                "Cidades abstratas devem gerar a mesma receita comercial por trabalhador definida no prefab CidadeModerna.");
            Assert.That(Read<float>(economia, "receitaIndustria"), Is.EqualTo(0f).Within(0.001f),
                "A receita comercial urbana não deve ser classificada como receita industrial.");
            Assert.That(Read<float>(economia, "receitaMoradia"), Is.EqualTo(44000f).Within(0.001f),
                "Cidades abstratas usam a mesma receita nacional por residente aplicada às cidades materializadas.");

            object pais = Activator.CreateInstance(ResolveType("DadosPaisGoverno"));
            Set(pais, "teamId", 4);
            Set(pais, "populacao", 550000);
            Set(pais, "populacaoCivil", 550000);
            ResolveType("SistemaGovernoMundial").GetMethod("AplicarEconomiaImoveis", Members)
                .Invoke(government, new[] { pais, economia, null });
            Assert.That(Read<int>(pais, "populacaoMaxima"), Is.EqualTo(1800000));
            Assert.That(Read<int>(pais, "populacaoCivil"), Is.EqualTo(550000),
                "O ciclo nacional não deve cortar a população das cidades abstratas pela capacidade residencial zero.");
        }
        finally
        {
            instanciaManager.GetSetMethod(true).Invoke(null, new[] { managerAnterior });
            UnityEngine.Object.DestroyImmediate(governmentObject);
            UnityEngine.Object.DestroyImmediate(economyObject);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void ReceitaDaCidadeMaterializadaEhComercialSemAlterarSeuTipoResidencial()
    {
        Type economyType = ResolveType("SistemaEconomiaImoveis");
        GameObject economyObject = new GameObject("Economia_Cidade_Materializada_Test");
        economyObject.SetActive(false);
        Component economySystem = economyObject.AddComponent(economyType);

        GameObject cityObject = new GameObject("Cidade_Materializada_Receita_Test");
        cityObject.SetActive(false);
        Component city = cityObject.AddComponent(ResolveType("CidadeComplexoUrbano"));
        ((Behaviour)city).enabled = false;
        Component structure = cityObject.AddComponent(ResolveType("EstruturaEconomica"));
        Set(structure, "teamId", 4);
        Set(structure, "tipo", Enum.Parse(ResolveType("TipoEstruturaEconomica"), "PredioResidencial"));
        Set(structure, "capacidadePopulacional", 1000);
        Set(structure, "populacaoAtual", 500);
        Set(structure, "empregosGerados", 250);
        Set(structure, "dinheiroGerado", 12f);
        Set(structure, "eficiencia", 1f);

        try
        {
            cityObject.SetActive(true);
            MethodInfo contabilizar = economyType.GetMethod("ContabilizarEstrutura", Members);
            Assert.That(contabilizar, Is.Not.Null);
            contabilizar.Invoke(economySystem, new object[] { structure });

            object economia = economyType.GetMethod("ObterEconomia", Members)
                .Invoke(economySystem, new object[] { 4 });
            Assert.That(economia, Is.Not.Null);
            Assert.That(Read<float>(economia, "receitaComercio"), Is.EqualTo(12f).Within(0.001f));
            Assert.That(Read<float>(economia, "receitaIndustria"), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Read<float>(economia, "receitaMoradia"), Is.EqualTo(40f).Within(0.001f));
            Assert.That(Read<Enum>(structure, "tipo").ToString(), Is.EqualTo("PredioResidencial"),
                "A cidade conserva o tipo residencial para capacidade e população; só sua receita é comercial.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cityObject);
            UnityEngine.Object.DestroyImmediate(economyObject);
        }
    }

    [Test]
    public void CatalogoUrbanoMantemCidadesCompletasEOcultaResidenciasERuasAvulsas()
    {
        Type dadosConstrucao = ResolveType("DadosConstrucao");
        UnityEngine.Object casaFicha = AssetDatabase.LoadAssetAtPath(
            "Assets/Prefabs/Imobiliario/casa/Casa.asset", dadosConstrucao);
        UnityEngine.Object ruaFicha = AssetDatabase.LoadAssetAtPath(
            "Assets/Prefabs/Imobiliario/ruas/rua reta.asset", dadosConstrucao);
        UnityEngine.Object cidadeFicha = AssetDatabase.LoadAssetAtPath(
            "Assets/Resources/Construcoes/CidadeModerna.asset", dadosConstrucao);
        GameObject casaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Imobiliario/casa/Casa.prefab");
        GameObject ruaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Imobiliario/ruas/Rua reta.prefab");
        GameObject cidadePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Imobiliario/city AI/Cidade Nova Aurora.prefab");
        Assert.That(casaFicha, Is.Not.Null);
        Assert.That(ruaFicha, Is.Not.Null);
        Assert.That(cidadeFicha, Is.Not.Null);
        Assert.That(casaPrefab, Is.Not.Null);
        Assert.That(ruaPrefab, Is.Not.Null);
        Assert.That(cidadePrefab, Is.Not.Null);

        MethodInfo classificar = ResolveType("MenuConstrucao").GetMethod(
            "EhConstrucaoUrbanaManual", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(classificar, Is.Not.Null);
        Assert.That((bool)classificar.Invoke(null, new object[] { casaFicha, casaPrefab }), Is.True,
            "Residencias isoladas nao devem ser uma ordem de construcao manual.");
        Assert.That((bool)classificar.Invoke(null, new object[] { ruaFicha, ruaPrefab }), Is.True,
            "Ruas isoladas nao devem ser uma ordem de construcao manual.");
        Assert.That((bool)classificar.Invoke(null, new object[] { cidadeFicha, cidadePrefab }), Is.False,
            "Nova Aurora permanece disponivel como expansao urbana completa.");
    }

    [Test]
    public void BrainMasterAdiaNovaAuroraDurantePreparacaoUrbanaAntesDeTentarPosicionar()
    {
        Type bridgeType = ResolveType("Hegemonia.AI.BrainMaster.IA_BackendBridge");
        object bridge = Activator.CreateInstance(bridgeType, new object[] { UnityEngine.Random.Range(10000, 100000000) });
        object buildService = bridgeType.GetProperty("BuildService", Members).GetValue(bridge, null);
        MethodInfo tryBuild = buildService.GetType().GetMethod("TryBuild", Members);
        Assert.That(tryBuild, Is.Not.Null);

        ParameterInfo[] parameters = tryBuild.GetParameters();
        object[] arguments =
        {
            "Cidade Nova Aurora",
            Vector3.zero,
            Quaternion.identity,
            Enum.ToObject(parameters[3].ParameterType, 0),
            false,
            string.Empty,
            null,
            null,
            null,
            null,
            null
        };

        bool construida = (bool)tryBuild.Invoke(buildService, arguments);
        Assert.That(construida, Is.False);
        Assert.That(arguments[9], Is.Null, "Nenhum prefab deve ser instanciado antes do fim da preparacao.");
        Assert.That((string)arguments[10], Does.Contain("preparacao urbana incompleta"));
    }

    [Test]
    public void CidadeAbstrataMaterializaPrefabNovaAuroraAoAtingirLimiarConfigurado()
    {
        GameObject managerObject = new GameObject("Gerenciador_Cidade_Abstrata_Materializacao");
        managerObject.SetActive(false);
        Component manager = managerObject.AddComponent(ResolveType("GerenciadorDivisaoTerritorial"));
        PropertyInfo instancia = ResolveType("GerenciadorDivisaoTerritorial").GetProperty("Instancia", Members);
        object instanciaAnterior = instancia.GetValue(null);
        GameObject materializada = null;
        GameObject terrenoObject = null;
        TerrainData terrenoDados = null;
        try
        {
            instancia.GetSetMethod(true).Invoke(null, new object[] { manager });
            terrenoDados = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(2000f, 100f, 2000f)
            };
            float[,] alturas = new float[33, 33];
            for (int z = 0; z < 33; z++)
                for (int x = 0; x < 33; x++) alturas[z, x] = 0.25f;
            terrenoDados.SetHeights(0, 0, alturas);
            terrenoObject = Terrain.CreateTerrainGameObject(terrenoDados);
            terrenoObject.name = "Terreno_Cidade_Materializacao_Teste";

            MethodInfo registrar = ResolveType("GerenciadorDivisaoTerritorial").GetMethod("RegistrarCidadeAbstrata", Members);
            object cidadeAbstrata = registrar.Invoke(manager, new object[]
            {
                "Nova Aurora Materializada Teste", 4, new Vector3(800f, 0f, 900f), 100000, 1200000
            });
            MethodInfo materializar = ResolveType("GerenciadorDivisaoTerritorial").GetMethod("TentarMaterializarCidadeAbstrata", Members);
            string id = Read<string>(cidadeAbstrata, "id");
            Assert.That((bool)materializar.Invoke(manager, new object[] { id }), Is.False,
                "A cidade inicial não deve criar geometria antes de um limiar.");

            Set(cidadeAbstrata, "desenvolvimento", 70f);
            Assert.That((bool)materializar.Invoke(manager, new object[] { id }), Is.True,
                "A ficha CidadeModerna de Resources deve materializar a cidade qualificada.");

            IList cidades = (IList)Read<object>(manager, "cidades");
            object registroMaterializado = cidades.Cast<object>().First(c => Read<string>(c, "nome") == "Nova Aurora Materializada Teste");
            Component marcador = (Component)Read<object>(registroMaterializado, "marcador");
            Assert.That(marcador, Is.Not.Null);
            materializada = marcador.gameObject;
            Component cidadeVisual = materializada.GetComponent(ResolveType("CidadeComplexoUrbano"));
            Assert.That(Read<string>(cidadeVisual, "nomeCidade"), Is.EqualTo("Nova Aurora Materializada Teste"));
            Assert.That(Read<int>(cidadeVisual, "teamId"), Is.EqualTo(4));
            Assert.That(Read<int>(cidadeVisual, "populacaoResidente"), Is.EqualTo(100000));
            Assert.That(materializada.transform.position.x, Is.EqualTo(800f).Within(0.01f),
                "A cidade deve manter o X definido pelo marcador da bandeira.");
            Assert.That(materializada.transform.position.z, Is.EqualTo(900f).Within(0.01f),
                "A cidade deve manter o Z definido pelo marcador da bandeira.");
            Renderer[] renderizadores = materializada.GetComponentsInChildren<Renderer>(true);
            float baseVisual = renderizadores.Min(renderer => renderer.bounds.min.y);
            Assert.That(baseVisual, Is.EqualTo(25f).Within(0.25f),
                "A base visual da cidade precisa encostar no relevo, não flutuar no Y salvo da bandeira.");
            Assert.That(materializada.GetComponent(ResolveType("SaveableEntity")), Is.Not.Null);
        }
        finally
        {
            instancia.GetSetMethod(true).Invoke(null, new[] { instanciaAnterior });
            if (materializada != null) UnityEngine.Object.DestroyImmediate(materializada);
            if (terrenoObject != null) UnityEngine.Object.DestroyImmediate(terrenoObject);
            if (terrenoDados != null) UnityEngine.Object.DestroyImmediate(terrenoDados);
            UnityEngine.Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void SaveEntityPreservaEfetivoMobilizadoEInfereMinimoEmSaveLegado()
    {
        const string prefabKey = "Unidade_Save_Efetivo";
        GameObject prefab = new GameObject(prefabKey);
        GameObject saveObject = new GameObject("Sistema_Save_Efetivo");
        saveObject.SetActive(false);
        Component identity = prefab.AddComponent(ResolveType("IdentidadeUnidade"));
        Component saveSystem = saveObject.AddComponent(ResolveType("SistemaSaveGame"));
        List<GameObject> restoredObjects = new List<GameObject>();
        try
        {
            Set(identity, "teamID", 1);
            Set(identity, "nomeDoPais", "Hegemonia");
            Set(identity, "tipoUnidade", Enum.Parse(ResolveType("TipoUnidade"), "Veiculo"));
            Set(identity, "militaresConsumidos", 17);
            Set(saveSystem, "dadosAtuais", Activator.CreateInstance(ResolveType("DadosDoJogo")));

            object captured = Method("CapturarEntidade", BindingFlags.Instance).Invoke(saveSystem, new object[] { prefab });
            Set(captured, "prefabKey", prefabKey);
            Set(captured, "uniqueId", "unidade-efetivo-save-atual");
            object currentSave = JsonUtility.FromJson(
                JsonUtility.ToJson(captured), ResolveType("SaveEntityData"));

            IDictionary prefabs = (IDictionary)Read<object>(saveSystem, "prefabsPorChaveSave");
            prefabs.Add(prefabKey, prefab);
            Method("InstanciarEntidadeSalva", BindingFlags.Instance).Invoke(saveSystem, new[] { currentSave });

            IDictionary saveables = (IDictionary)Read<object>(saveSystem, "saveablesPorId");
            Component restoredCurrentSave = (Component)saveables["unidade-efetivo-save-atual"];
            Assert.That(restoredCurrentSave, Is.Not.Null);
            restoredObjects.Add(restoredCurrentSave.gameObject);
            Assert.That(Read<int>(restoredCurrentSave.GetComponent(ResolveType("IdentidadeUnidade")), "militaresConsumidos"),
                Is.EqualTo(17), "Saves novos devem preservar o efetivo exato da unidade.");

            Set(identity, "militaresConsumidos", 0);
            string legacyJson = "{\"uniqueId\":\"unidade-efetivo-save-legado\","
                + "\"prefabKey\":\"" + prefabKey + "\",\"ativo\":true,\"teamID\":1,"
                + "\"nomeDoPais\":\"Hegemonia\",\"tipoUnidade\":1}";
            object legacySave = JsonUtility.FromJson(legacyJson, ResolveType("SaveEntityData"));
            Assert.That(Read<bool>(legacySave, "possuiMilitaresConsumidos"), Is.False,
                "Um JSON legado sem o marcador deve entrar no caminho de compatibilidade.");
            Method("InstanciarEntidadeSalva", BindingFlags.Instance).Invoke(saveSystem, new[] { legacySave });

            Component restoredLegacySave = (Component)saveables["unidade-efetivo-save-legado"];
            Assert.That(restoredLegacySave, Is.Not.Null);
            restoredObjects.Add(restoredLegacySave.gameObject);
            Assert.That(Read<int>(restoredLegacySave.GetComponent(ResolveType("IdentidadeUnidade")), "militaresConsumidos"),
                Is.EqualTo((int)ResolveType("SistemaGovernoMundial").GetMethod("ObterEfetivoNecessario", Members,
                    null, new[] { typeof(GameObject), ResolveType("TipoUnidade") }, null)
                    .Invoke(null, new[] { prefab, Enum.Parse(ResolveType("TipoUnidade"), "Veiculo") })),
                "Saves legados devem recuperar o mínimo de efetivo a partir do tipo da unidade.");
        }
        finally
        {
            Set(identity, "militaresConsumidos", 0);
            for (int i = 0; i < restoredObjects.Count; i++)
            {
                Component restoredIdentity = restoredObjects[i] != null
                    ? restoredObjects[i].GetComponent(ResolveType("IdentidadeUnidade"))
                    : null;
                if (restoredIdentity != null) Set(restoredIdentity, "militaresConsumidos", 0);
                if (restoredObjects[i] != null) UnityEngine.Object.DestroyImmediate(restoredObjects[i]);
            }
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(saveObject);
        }
    }

    private static Component CriarCidadeParaSincronizacao(GameObject cidadeObject, string nome, int populacao)
    {
        cidadeObject.AddComponent(ResolveType("IdentidadeUnidade"));
        cidadeObject.AddComponent(ResolveType("SistemaDeDanos"));
        Component cidade = cidadeObject.AddComponent(ResolveType("CidadeComplexoUrbano"));
        InvokeLifecycle(cidade, "Awake");
        Set(cidade, "nomeCidade", nome);
        Set(cidade, "teamId", 4);
        Set(cidade, "capacidadeHabitacional", 1000000);
        Set(cidade, "populacaoResidente", populacao);
        InvokeLifecycle(cidade, "OnEnable");
        return cidade;
    }

    private static void SincronizarPopulacaoNacional(int teamId, int populacao)
    {
        MethodInfo sincronizar = ResolveType("CidadeComplexoUrbano").GetMethod(
            "SincronizarPopulacaoNacional", BindingFlags.Static | BindingFlags.Public);
        Assert.That(sincronizar, Is.Not.Null, "CidadeComplexoUrbano.SincronizarPopulacaoNacional");
        sincronizar.Invoke(null, new object[] { teamId, populacao });
    }

    private static Component CriarMarcadorDeMigracao(GameObject marcadorObject, Vector3 posicao)
    {
        marcadorObject.transform.position = posicao;
        marcadorObject.AddComponent(ResolveType("IdentidadeUnidade"));
        Component marcador = marcadorObject.AddComponent(ResolveType("MarcadorTerritorio"));
        Set(marcador, "raioDeDominio", 100f);
        MethodInfo configurar = marcador.GetType().GetMethod("ConfigureOwnership", Members);
        Assert.That(configurar, Is.Not.Null);
        configurar.Invoke(marcador, new object[] { 4, false, 0f });
        return marcador;
    }

    private static Component CriarMarcadorDePrefeitura(string nome, int teamId, Vector3 posicao, string entityId)
    {
        GameObject objeto = new GameObject(nome);
        objeto.transform.position = posicao;
        Component identidade = objeto.AddComponent(ResolveType("IdentidadeUnidade"));
        Set(identidade, "teamID", teamId);
        Component entidadeSalva = objeto.AddComponent(ResolveType("SaveableEntity"));
        PropertyInfo uniqueId = entidadeSalva.GetType().GetProperty("UniqueId", Members);
        Assert.That(uniqueId, Is.Not.Null);
        uniqueId.SetValue(entidadeSalva, entityId);
        Component marcador = objeto.AddComponent(ResolveType("MarcadorTerritorio"));
        marcador.GetType().GetMethod("ConfigureOwnership", Members)
            .Invoke(marcador, new object[] { teamId, true, 300f });
        return marcador;
    }

    private static object CriarRegiaoDeMigracao(string id, Component marcador, float atratividade)
    {
        Type tipoRegiao = ResolveType("CidadeEstado");
        ConstructorInfo construtor = tipoRegiao.GetConstructor(new[]
        {
            typeof(string), typeof(string), typeof(int), typeof(bool), ResolveType("MarcadorTerritorio")
        });
        Assert.That(construtor, Is.Not.Null, "CidadeEstado(string,string,int,bool,MarcadorTerritorio)");
        object regiao = construtor.Invoke(new[] { (object)id, "Região " + id, 4, false, marcador });
        Set(regiao, "atratividade", atratividade);
        return regiao;
    }

    private static void AssertPopulacoes(Component cidadeA, int populacaoA, Component cidadeB, int populacaoB)
    {
        Assert.That(Read<int>(cidadeA, "populacaoResidente"), Is.EqualTo(populacaoA));
        Assert.That(Read<int>(cidadeB, "populacaoResidente"), Is.EqualTo(populacaoB));
    }

    private static void InvokeLifecycle(Component component, string methodName)
    {
        MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, component.GetType().Name + "." + methodName);
        method.Invoke(component, null);
    }

    private static Type ResolveType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Tipo não carregado: " + name);
        return type;
    }

    private static MethodInfo Method(string name, BindingFlags flags)
    {
        MethodInfo method = ResolveType("SistemaSaveGame").GetMethod(name, flags | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "SistemaSaveGame." + name);
        return method;
    }

    private static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + name);
        field.SetValue(target, value);
    }

    private static T Read<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, target.GetType().FullName + "." + name);
        return (T)field.GetValue(target);
    }
}
#endif
