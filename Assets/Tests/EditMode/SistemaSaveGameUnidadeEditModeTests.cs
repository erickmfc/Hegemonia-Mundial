#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SistemaSaveGameUnidadeEditModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string C17PrefabPath = "Assets/Prefabs/Aeroporto/C700/C17.prefab";

    [Test]
    public void UnidadeSemSaveablePreservaIdentidadeVidaECombustivelAposCapturaJsonERestauracao()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(C17PrefabPath);
        Assert.That(prefab, Is.Not.Null, "O teste precisa usar o prefab militar real C17.");
        Assert.That(prefab.GetComponent(ResolveType("SaveableEntity")), Is.Null,
            "O caso deve cobrir ficha militar que entra no save pelo componente operacional.");
        Assert.That(prefab.GetComponent(ResolveType("IdentidadeUnidade")), Is.Not.Null);
        Assert.That(prefab.GetComponent(ResolveType("SistemaDeDanos")), Is.Not.Null);
        Assert.That(prefab.GetComponent(ResolveType("CombustivelUnidade")), Is.Not.Null);
        Assert.That(prefab.GetComponent(ResolveType("ControleUnidade")), Is.Not.Null);

        GameObject saveObject = new GameObject("Sistema_Save_Unidade_RoundTrip");
        saveObject.SetActive(false);
        GameObject origem = null;
        GameObject restaurada = null;

        try
        {
            Component saveSystem = saveObject.AddComponent(ResolveType("SistemaSaveGame"));
            origem = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(origem, Is.Not.Null);
            origem.name = prefab.name + "(Clone)";
            origem.SetActive(false);
            origem.transform.SetPositionAndRotation(new Vector3(17f, 3f, -42f), Quaternion.Euler(0f, 63f, 0f));

            Component identidade = origem.GetComponent(ResolveType("IdentidadeUnidade"));
            Set(identidade, "teamID", 1);
            Set(identidade, "nomeDoPais", "Hegemonia Teste");
            Set(identidade, "militaresConsumidos", 87);

            Component danos = origem.GetComponent(ResolveType("SistemaDeDanos"));
            Set(danos, "vidaMaxima", 920f);
            Set(danos, "vidaAtual", 517f);

            Component combustivel = origem.GetComponent(ResolveType("CombustivelUnidade"));
            Set(combustivel, "capacidade", 16000f);
            Set(combustivel, "combustivelAtual", 7312.5f);

            object capturado = Method("CapturarEntidade").Invoke(saveSystem, new object[] { origem });
            Assert.That(capturado, Is.Not.Null);
            Assert.That(origem.GetComponent(ResolveType("SaveableEntity")), Is.Not.Null,
                "CapturarEntidade deve registrar o ID estável no objeto capturado.");

            string json = JsonUtility.ToJson(capturado);
            Type saveDataType = ResolveType("SaveEntityData");
            object carregado = JsonUtility.FromJson(json, saveDataType);

            Assert.That(Read<string>(carregado, "uniqueId"), Is.Not.Null.And.Not.Empty);
            Assert.That(Read<string>(carregado, "prefabKey"), Is.EqualTo("C17"));
            Assert.That(Read<int>(carregado, "teamID"), Is.EqualTo(1));
            Assert.That(Read<string>(carregado, "nomeDoPais"), Is.EqualTo("Hegemonia Teste"));
            Assert.That(Read<int>(carregado, "militaresConsumidos"), Is.EqualTo(87));
            Assert.That(Read<bool>(carregado, "possuiVida"), Is.True);
            Assert.That(Read<float>(carregado, "vidaAtual"), Is.EqualTo(517f));
            Assert.That(Read<float>(carregado, "vidaMaxima"), Is.EqualTo(920f));
            Assert.That(Read<bool>(carregado, "possuiCombustivel"), Is.True);
            Assert.That(Read<float>(carregado, "combustivelAtual"), Is.EqualTo(7312.5f));
            Assert.That(Read<float>(carregado, "capacidadeCombustivel"), Is.EqualTo(16000f));

            IDictionary prefabs = (IDictionary)ResolveType("SistemaSaveGame")
                .GetField("prefabsPorChaveSave", Members).GetValue(saveSystem);
            prefabs.Add(Read<string>(carregado, "prefabKey"), prefab);

            Method("InstanciarEntidadeSalva").Invoke(saveSystem, new[] { carregado });

            IDictionary saveables = (IDictionary)ResolveType("SistemaSaveGame")
                .GetField("saveablesPorId", Members).GetValue(saveSystem);
            Component saveableRestaurada = (Component)saveables[Read<string>(carregado, "uniqueId")];
            Assert.That(saveableRestaurada, Is.Not.Null, "A restauração deve registrar a entidade pelo mesmo ID.");
            restaurada = saveableRestaurada.gameObject;

            Component identidadeRestaurada = restaurada.GetComponent(ResolveType("IdentidadeUnidade"));
            Assert.That(Read<int>(identidadeRestaurada, "teamID"), Is.EqualTo(1));
            Assert.That(Read<string>(identidadeRestaurada, "nomeDoPais"), Is.EqualTo("Hegemonia Teste"));
            Assert.That(Read<int>(identidadeRestaurada, "militaresConsumidos"), Is.EqualTo(87));

            Component danosRestaurados = restaurada.GetComponent(ResolveType("SistemaDeDanos"));
            Assert.That(Read<float>(danosRestaurados, "vidaAtual"), Is.EqualTo(517f));
            Assert.That(Read<float>(danosRestaurados, "vidaMaxima"), Is.EqualTo(920f));

            Component combustivelRestaurado = restaurada.GetComponent(ResolveType("CombustivelUnidade"));
            Assert.That(Read<float>(combustivelRestaurado, "combustivelAtual"), Is.EqualTo(7312.5f));
            Assert.That(Read<float>(combustivelRestaurado, "capacidade"), Is.EqualTo(16000f));
            Assert.That(restaurada.transform.position, Is.EqualTo(new Vector3(17f, 3f, -42f)));
            Assert.That(Quaternion.Angle(restaurada.transform.rotation, Quaternion.Euler(0f, 63f, 0f)), Is.LessThan(0.01f));
            Component saveableComponenteRestaurado = restaurada.GetComponent(ResolveType("SaveableEntity"));
            PropertyInfo uniqueId = saveableComponenteRestaurado.GetType().GetProperty("UniqueId", Members);
            Assert.That(uniqueId, Is.Not.Null);
            Assert.That(uniqueId.GetValue(saveableComponenteRestaurado), Is.EqualTo(Read<string>(carregado, "uniqueId")));
        }
        finally
        {
            if (restaurada != null) UnityEngine.Object.DestroyImmediate(restaurada);
            if (origem != null) UnityEngine.Object.DestroyImmediate(origem);
            if (saveObject != null) UnityEngine.Object.DestroyImmediate(saveObject);
        }
    }

    [Test]
    public void SaveSemEntidadesAgendaRestauracaoParaEstadoNacionalAdiado()
    {
        Type saveType = ResolveType("DadosDoJogo");
        object dados = Activator.CreateInstance(saveType);
        MethodInfo possuiEstado = ResolveType("SistemaSaveGame").GetMethod(
            "PossuiEstadoRestauravel",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(possuiEstado, Is.Not.Null);

        Assert.That(possuiEstado.Invoke(null, new[] { dados }), Is.False,
            "Um save vazio não deve forçar a substituição do mundo carregado.");

        Set(dados, "governoMundial", Activator.CreateInstance(ResolveType("SaveGovernoMundialData")));
        Assert.That(possuiEstado.Invoke(null, new[] { dados }), Is.True,
            "Um snapshot de governo precisa chegar à restauração posterior à cena mesmo sem entidades.");
        MethodInfo substituirEntidades = ResolveType("SistemaSaveGame").GetMethod(
            "DeveSubstituirEntidadesDaCena",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(substituirEntidades, Is.Not.Null);
        Assert.That(substituirEntidades.Invoke(null, new[] { dados }), Is.False,
            "O estado econômico isolado não autoriza apagar as entidades existentes na cena.");

        Set(dados, "governoMundial", null);
        ((IList)Read<object>(dados, "filaProducao")).Add(Activator.CreateInstance(ResolveType("SaveProductionOrderData")));
        Assert.That(possuiEstado.Invoke(null, new[] { dados }), Is.True,
            "Uma fila de produção salva também depende da restauração posterior à cena.");

        ((IList)Read<object>(dados, "filaProducao")).Clear();
        object industria = Read<object>(dados, "industria");
        ((IList)Read<object>(industria, "historico")).Add(Activator.CreateInstance(ResolveType("SaveHistoricoIndustrial")));
        Assert.That(possuiEstado.Invoke(null, new[] { dados }), Is.True,
            "Dados industriais salvos precisam sobreviver mesmo quando o mundo não contém unidades.");

        Set(dados, "industria", Activator.CreateInstance(ResolveType("IndustrialSaveData")));
        object producaoAutomatica = Activator.CreateInstance(ResolveType("Hegemonia.AI.Shared.IAAutoProductionSaveData"));
        IList ordensAutomaticas = (IList)Read<object>(producaoAutomatica, "orders");
        ordensAutomaticas.Add(Activator.CreateInstance(ResolveType("Hegemonia.AI.Shared.IAProductionOrderSaveData")));
        Set(dados, "producaoAutomaticaIA", producaoAutomatica);
        Assert.That(possuiEstado.Invoke(null, new[] { dados }), Is.True,
            "Uma reserva ativa de produção automática também precisa acionar a restauração tardia.");
    }

    private static Type ResolveType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Tipo não carregado: " + name);
        return type;
    }

    private static MethodInfo Method(string name)
    {
        MethodInfo method = ResolveType("SistemaSaveGame").GetMethod(name, Members);
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
