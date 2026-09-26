#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HegemoniaIntegrityTests
{
    [SetUp]
    public void SetUp()
    {
        InteractionModeService.Release(InteractionOwner.Construction);
        InteractionModeService.Release(InteractionOwner.SelectionBox);
        InteractionModeService.Release(InteractionOwner.Demolition);
        InteractionModeService.Release(InteractionOwner.Patrol);
        InteractionModeService.Release(InteractionOwner.Follow);
        InteractionModeService.Release(InteractionOwner.AirportOrder);
        InteractionModeService.Release(InteractionOwner.CarrierOrder);
        InteractionModeService.Release(InteractionOwner.ManualFire);
    }

    [Test]
    public void InteractionModeService_LastRequestWins_AndReleaseRestoresPrevious()
    {
        InteractionModeService.Request(InteractionOwner.Construction, new InteractionPolicy { bloqueiaSelecao = true });
        InteractionModeService.Request(InteractionOwner.Demolition, new InteractionPolicy { bloqueiaSelecao = true, consomeLMB = true });

        Assert.AreEqual(InteractionOwner.Demolition, InteractionModeService.CurrentSnapshot().Owner);
        Assert.IsTrue(InteractionModeService.CanConsumeLeft(InteractionOwner.Demolition));

        InteractionModeService.Release(InteractionOwner.Demolition);

        Assert.AreEqual(InteractionOwner.Construction, InteractionModeService.CurrentSnapshot().Owner);
    }

    [Test]
    public void InteractionModeService_SourceScopedRelease_DoesNotClearAnotherRequester()
    {
        GameObject aeroportoA = new GameObject("AeroportoA");
        GameObject aeroportoB = new GameObject("AeroportoB");

        try
        {
            InteractionModeService.Request(aeroportoA, InteractionOwner.AirportOrder, new InteractionPolicy { bloqueiaSelecao = true });
            InteractionModeService.Request(aeroportoB, InteractionOwner.AirportOrder, new InteractionPolicy { bloqueiaSelecao = true, consomeRMB = true });

            Assert.IsTrue(InteractionModeService.IsActive(aeroportoB, InteractionOwner.AirportOrder));

            InteractionModeService.Release(aeroportoB, InteractionOwner.AirportOrder);

            Assert.AreEqual(InteractionOwner.AirportOrder, InteractionModeService.CurrentSnapshot().Owner);
            Assert.IsTrue(InteractionModeService.IsActive(aeroportoA, InteractionOwner.AirportOrder));
            Assert.IsFalse(InteractionModeService.CanConsumeRight(InteractionOwner.AirportOrder));
        }
        finally
        {
            Object.DestroyImmediate(aeroportoA);
            Object.DestroyImmediate(aeroportoB);
            InteractionModeService.Release(InteractionOwner.AirportOrder);
        }
    }

    [Test]
    public void BuildSettings_EnabledScenesExistOnDisk()
    {
        int enabledScenes = 0;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled)
            {
                continue;
            }

            enabledScenes++;
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path),
                "Cena habilitada em Build Settings não existe: " + scene.path);
        }

        Assert.Greater(enabledScenes, 0, "O Build Settings precisa conter ao menos uma cena habilitada.");
    }

    [Test]
    public void MilitaryAirbasePrefabs_ProvideTheSu11ForPurchase()
    {
        GameObject su11 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Aeroporto/Su11/Su11.prefab");
        Assert.IsNotNull(su11, "Prefab do Su-11 não encontrado.");

        string[] airbasePrefabPaths =
        {
            "Assets/Prefabs/Aeroporto/Aeroporto militar.prefab",
            "Assets/Prefabs/Aeroporto/Aeroporto militar/Base Militar.prefab"
        };

        foreach (string path in airbasePrefabPaths)
        {
            GameObject airbase = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(airbase, "Prefab da base aérea não encontrado: " + path);

            GerenciadorAeroporto airport = airbase.GetComponentInChildren<GerenciadorAeroporto>(true);
            Assert.IsNotNull(airport, "Gerenciador de aeroporto ausente: " + path);
            Assert.AreSame(su11, airport.prefabSu11, "O botão Comprar Su-11 ficaria desativado: " + path);
            Assert.IsNotNull(airport.hangarAviao, "Saída do hangar ausente: " + path);
            Assert.IsNotNull(airport.decolagem, "Waypoints de decolagem ausentes: " + path);
            Assert.IsNotNull(airport.decida, "Waypoints de pouso ausentes: " + path);
        }
    }

    [Test]
    public void EstaleiroPrefab_ShouldExposeAtracagemSlots()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Estaleiro Marinho/Estaleiros navais.prefab");
        Assert.IsNotNull(prefab, "Prefab do estaleiro não encontrado.");

        Estaleiro estaleiro = prefab.GetComponent<Estaleiro>();
        Assert.IsNotNull(estaleiro, "Componente Estaleiro ausente no prefab.");
        Assert.IsNotNull(estaleiro.slots, "Slots do estaleiro ausentes.");
        Assert.GreaterOrEqual(estaleiro.slots.Length, 2, "Estaleiro precisa de ao menos dois slots.");
        Assert.AreEqual("Atracagem", estaleiro.slots[0].nomeSlot);
        Assert.AreEqual("Atracagem_Grande", estaleiro.slots[1].nomeSlot);
    }

    [Test]
    public void MainScenes_ShouldNotContainMissingScripts()
    {
        Assert.Zero(CountMissingScriptsInScene("Assets/Scenes/MenuPrincipal.unity"), "MenuPrincipal ainda possui Missing Scripts.");
    }

    [Test]
    public void EstaleiroPrefab_ShouldNotContainMissingScripts()
    {
        Assert.Zero(CountMissingScriptsInPrefab("Assets/Prefabs/Estaleiro Marinho/Estaleiros navais.prefab"), "Prefab do estaleiro ainda possui Missing Scripts.");
    }

    private static int CountMissingScriptsInScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        int missing = 0;

        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                missing += CountMissingScriptsRecursive(roots[i]);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        return missing;
    }

    private static int CountMissingScriptsInPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            return CountMissingScriptsRecursive(root);
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static int CountMissingScriptsRecursive(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int total = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child.gameObject == root)
            {
                continue;
            }

            total += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        }

        return total;
    }
}
#endif
