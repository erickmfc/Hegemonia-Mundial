using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public sealed class IA03RuntimeAcquisitionAndProjectilePlayModeTests
{
    private const string ValidationScenePath = "Assets/Tests/PlayMode/IA03_WarValidation.unity";
    private const string BalaPrefabPath = "Assets/Prefabs/Soldado_Rifle/Bala.prefab";
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Vector3 TestOrigin = new Vector3(5000f, 0f, 5000f);

    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private readonly HashSet<int> projectileIdsBefore = new HashSet<int>();
    private readonly List<string> observedCombatLogs = new List<string>();
    private string originalScenePath;
    private object originalGovernment;
    private PropertyInfo governmentInstanceProperty;
    private Application.LogCallback logHandler;

    [UnitySetUp]
    public IEnumerator LoadIsolatedValidationScene()
    {
        Scene current = SceneManager.GetActiveScene();
        originalScenePath = current.IsValid() ? current.path : string.Empty;
#if UNITY_EDITOR
        SceneManager.sceneLoaded += OnValidationSceneLoaded;
        AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
            ValidationScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        Assert.That(load, Is.Not.Null, "Não foi possível carregar IA03_WarValidation em Play Mode.");
        while (!load.isDone) yield return null;
        SceneManager.sceneLoaded -= OnValidationSceneLoaded;

        Scene validationScene = SceneManager.GetActiveScene();
        Assert.That(validationScene.path, Is.EqualTo(ValidationScenePath));
        Assert.That(SceneManager.sceneCount, Is.EqualTo(1),
            "Este teste deve executar somente com IA03_WarValidation carregada.");
        DisableGameplayControllers(validationScene);
#else
        Assert.Fail("A validação exige Play Mode no Unity Editor.");
        yield break;
#endif
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator RestoreAfterTest()
    {
        if (logHandler != null) Application.logMessageReceived -= logHandler;
        logHandler = null;

        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null) UnityEngine.Object.Destroy(createdObjects[i]);
        createdObjects.Clear();
        yield return null;

        if (governmentInstanceProperty != null)
        {
            governmentInstanceProperty.GetSetMethod(true)?.Invoke(null, new[] { originalGovernment });
            governmentInstanceProperty = null;
            originalGovernment = null;
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(originalScenePath)
            && !string.Equals(originalScenePath, ValidationScenePath, StringComparison.OrdinalIgnoreCase))
        {
            AsyncOperation restore = EditorSceneManager.LoadSceneAsyncInPlayMode(
                originalScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            if (restore != null)
                while (!restore.isDone) yield return null;
        }
#endif
    }

    [UnityTest]
    public IEnumerator ControleTorreta_UpdateDoesNotAcquireTagOnlyTargetWithoutIdentity()
    {
        yield return VerifyTagOnlyTargetIsRejected("ControleTorreta");
    }

    [UnityTest]
    public IEnumerator ControleTorretaModular_UpdateDoesNotAcquireTagOnlyTargetWithoutIdentity()
    {
        yield return VerifyTagOnlyTargetIsRejected("ControleTorretaModular");
    }

    [UnityTest]
    public IEnumerator BalaPrefabFromSistemaDeTiroPreservesOwnerThroughDamage()
    {
#if UNITY_EDITOR
        GameObject balaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BalaPrefabPath);
#else
        GameObject balaPrefab = null;
#endif
        Assert.That(balaPrefab, Is.Not.Null, "Bala.prefab não foi carregado do asset real.");
        Type projectileType = ResolveType("Projetil");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type weaponType = ResolveType("SistemaDeTiro");
        Type damageType = ResolveType("SistemaDeDanos");
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Assert.That(projectileType, Is.Not.Null);
        Assert.That(identityType, Is.Not.Null);
        Assert.That(weaponType, Is.Not.Null);
        Assert.That(damageType, Is.Not.Null);

        if (governmentType != null)
        {
            governmentInstanceProperty = governmentType.GetProperty("Instancia", StaticMembers);
            if (governmentInstanceProperty != null)
            {
                originalGovernment = governmentInstanceProperty.GetValue(null, null);
                governmentInstanceProperty.GetSetMethod(true)?.Invoke(null, new object[] { null });
            }
        }

        GameObject shooter = Track(new GameObject("IA03 Bala authoring shooter Team 1"));
        shooter.SetActive(false);
        shooter.transform.position = TestOrigin;
        Component shooterIdentity = shooter.AddComponent(identityType);
        SetMember(shooterIdentity, "teamID", 1);
        Component weapon = shooter.AddComponent(weaponType);
        GameObject muzzleObject = Track(new GameObject("Bala authoring muzzle"));
        muzzleObject.transform.SetParent(shooter.transform, false);
        muzzleObject.transform.localPosition = new Vector3(0f, 1f, 0.5f);

        GameObject target = Track(new GameObject("IA03 Bala authoring target Team 3"));
        target.SetActive(false);
        target.transform.position = TestOrigin + new Vector3(0f, 0f, 12f);
        Component targetIdentity = target.AddComponent(identityType);
        SetMember(targetIdentity, "teamID", 3);
        BoxCollider targetCollider = target.AddComponent<BoxCollider>();
        targetCollider.size = new Vector3(3f, 4f, 2f);
        Component targetDamage = target.AddComponent(damageType);
        SetMember(targetDamage, "vidaMaxima", 100f);
        SetMember(targetDamage, "vidaAtual", 100f);

        SetMember(weapon, "prefabProjetil", balaPrefab);
        SetMember(weapon, "bocaDoCano", muzzleObject.transform);
        SetMember(weapon, "alvoAtual", null);
        SetMember(weapon, "modoPassivo", false);
        SetMember(weapon, "alcanceTiro", 100f);
        SetMember(weapon, "velocidadeDoTiro", 120f);
        SetMember(weapon, "intervaloEntreTiros", 10f);
        shooter.SetActive(true);
        target.SetActive(true);
        Physics.SyncTransforms();
        yield return null; // Start da arma/projetil é executado pelo runtime.

        float healthBefore = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        SetMember(weapon, "alvoAtual", target.transform);
        MethodInfo fire = weaponType.GetMethod("Atirar", InstanceMembers);
        Assert.That(fire, Is.Not.Null, "O método de disparo de SistemaDeTiro não foi encontrado.");
        fire.Invoke(weapon, null);

        Component shot = FindOwnedProjectile(projectileType, shooter);
        Assert.That(shot, Is.Not.Null,
            "SistemaDeTiro não deixou um Bala(Clone) ativo para capturar imediatamente após o disparo.");
        MethodInfo getOwner = projectileType.GetMethod("GetDono", InstanceMembers);
        Assert.That(getOwner, Is.Not.Null);
        Assert.That(getOwner.Invoke(shot, null), Is.SameAs(shooter),
            "O projétil deve receber a unidade atacante como dona logo após Spawn.");
        Assert.That(Convert.ToInt32(GetMember(shot, "TeamDono")), Is.EqualTo(1));

        float deadline = Time.realtimeSinceStartup + 2f;
        while (Convert.ToSingle(GetMember(targetDamage, "vidaAtual")) >= healthBefore
               && Time.realtimeSinceStartup < deadline)
            yield return null;

        float healthAfter = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        Assert.That(healthAfter, Is.LessThan(healthBefore), "Bala.prefab não causou dano no alvo real.");
        Assert.That(GetMember(targetDamage, "ultimoAgressor"), Is.SameAs(shooter),
            "SistemaDeDanos não preservou a unidade atacante como agressora.");
        Assert.That(Convert.ToInt32(GetMember(targetDamage, "ultimoAgressorTeamId")), Is.EqualTo(1),
            "SistemaDeDanos não registrou Team 1 como equipe agressora.");
    }

    private IEnumerator VerifyTagOnlyTargetIsRejected(string turretTypeName)
    {
#if UNITY_EDITOR
        GameObject balaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BalaPrefabPath);
#else
        GameObject balaPrefab = null;
#endif
        Assert.That(balaPrefab, Is.Not.Null);

        Type identityType = ResolveType("IdentidadeUnidade");
        Type damageType = ResolveType("SistemaDeDanos");
        Type turretType = ResolveType(turretTypeName);
        Type moduleType = ResolveType("ModuloArma");
        Type projectileType = ResolveType("Projetil");
        Assert.That(identityType, Is.Not.Null);
        Assert.That(damageType, Is.Not.Null);
        Assert.That(turretType, Is.Not.Null);
        Assert.That(projectileType, Is.Not.Null);
        if (turretTypeName == "ControleTorretaModular") Assert.That(moduleType, Is.Not.Null);

        GameObject shooter = Track(new GameObject("IA03 tag-only shooter " + turretTypeName));
        shooter.SetActive(false);
        shooter.transform.position = TestOrigin;
        Component shooterIdentity = shooter.AddComponent(identityType);
        SetMember(shooterIdentity, "teamID", 1);

        GameObject turretObject = Track(new GameObject("IA03 tag-only AA turret " + turretTypeName));
        turretObject.transform.SetParent(shooter.transform, false);
        turretObject.SetActive(false);
        Behaviour turret = turretObject.AddComponent(turretType) as Behaviour;
        Assert.That(turret, Is.Not.Null);

        GameObject pivot = Track(new GameObject("Turret pivot " + turretTypeName));
        pivot.transform.SetParent(turretObject.transform, false);
        GameObject muzzle = Track(new GameObject("Turret muzzle " + turretTypeName));
        muzzle.transform.SetParent(pivot.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        if (turretTypeName == "ControleTorreta")
        {
            SetMember(turret, "minhaIdentidade", shooterIdentity);
            SetMember(turret, "meuTime", 1);
            SetMember(turret, "alcance", 80f);
            SetMember(turret, "tamanhoCartucho", 12);
            SetMember(turret, "tempoEntreTiros", 0.1f);
            SetMember(turret, "municaoPrefab", balaPrefab);
            SetMember(turret, "locaisDoTiro", new[] { muzzle.transform });
            SetMember(turret, "descobrirCanosAutomaticamente", false);
            SetMember(turret, "pecaQueGira", pivot.transform);
            SetMember(turret, "velocidadeGiro", 360f);
            SetMember(turret, "etiquetaAlvo", "Aereo");
            SetMember(turret, "souAntiAereo", true);
            SetMember(turret, "modoPassivo", false);
        }
        else
        {
            SetMember(turret, "meuTime", 1);
            SetMember(turret, "minhaRaiz", shooter.transform);
            SetMember(turret, "alcanceRadar", 80f);
            SetMember(turret, "pecaQueGira", pivot.transform);
            SetMember(turret, "velocidadeGiro", 360f);
            SetMember(turret, "etiquetaAlvo", "Aereo");
            SetMember(turret, "modoPassivo", false);

            object module = Activator.CreateInstance(moduleType);
            SetMember(module, "municaoPrefab", balaPrefab);
            SetMember(module, "pontosDisparo", new[] { muzzle.transform });
            SetMember(module, "velocidadeProjetil", 120f);
            SetMember(module, "danoBase", 2f);
            SetMember(module, "intervaloTiro", 0.1f);
            SetMember(module, "tamanhoCartucho", 12);
            SetMember(module, "alcanceMaximo", 80f);
            // The PlayMode test assembly intentionally resolves gameplay types
            // by reflection because the runtime scripts live in the predefined
            // Assembly-CSharp. Build the concrete List<ModuloArma> without a
            // compile-time reference to that predefined assembly.
            Type listType = typeof(List<>).MakeGenericType(moduleType);
            IList modules = (IList)Activator.CreateInstance(listType);
            modules.Add(module);
            SetMember(turret, "armas", modules);
        }

        GameObject target = Track(new GameObject("IA03 TagOnly Aereo Without Identity"));
        target.SetActive(false);
        target.transform.position = TestOrigin + new Vector3(0f, 10f, 18f);
        target.tag = "Inimigo";
        BoxCollider collider = target.AddComponent<BoxCollider>();
        collider.size = new Vector3(4f, 4f, 2f);
        Component targetDamage = target.AddComponent(damageType);
        SetMember(targetDamage, "vidaMaxima", 100f);
        SetMember(targetDamage, "vidaAtual", 100f);

        // Nenhuma IdentidadeUnidade no alvo, em pais ou filhos; a tag sozinha não representa guerra.
        Assert.That(target.GetComponentsInChildren(identityType, true), Is.Empty);
        float healthBefore = Convert.ToSingle(GetMember(targetDamage, "vidaAtual"));
        IList configuredWeapons = null;
        int ammoBefore;
        if (turretTypeName == "ControleTorreta")
        {
            int magazine = Convert.ToInt32(GetMember(turret, "tamanhoCartucho"));
            SetMember(turret, "balasAtuais", magazine);
            ammoBefore = magazine;
        }
        else
        {
            configuredWeapons = (IList)GetMember(turret, "armas");
            int magazine = Convert.ToInt32(GetMember(configuredWeapons[0], "tamanhoCartucho"));
            SetMember(configuredWeapons[0], "municaoAtual", magazine);
            ammoBefore = magazine;
        }
        projectileIdsBefore.Clear();
        UnityEngine.Object[] existingProjectiles = UnityEngine.Object.FindObjectsByType(
            projectileType, FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < existingProjectiles.Length; i++)
            if (existingProjectiles[i] != null) projectileIdsBefore.Add(existingProjectiles[i].GetInstanceID());

        observedCombatLogs.Clear();
        logHandler = (condition, stack, type) =>
        {
            if (condition.IndexOf("DISPARAR_INVOCADO", StringComparison.Ordinal) >= 0
                || condition.IndexOf("[PROJECTILE_PROBE][SPAWN]", StringComparison.Ordinal) >= 0
                || condition.IndexOf("disparada!", StringComparison.OrdinalIgnoreCase) >= 0)
                observedCombatLogs.Add(condition);
        };
        Application.logMessageReceived += logHandler;

        shooter.SetActive(true);
        turretObject.SetActive(true);
        target.SetActive(true);
        Physics.SyncTransforms();

        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < deadline) yield return null;

        Application.logMessageReceived -= logHandler;
        logHandler = null;

        Assert.That(GetMember(turret, "alvoPrioritario"), Is.Null);
        Assert.That(GetMember(turret, "alvoAtual"), Is.Null,
            turretTypeName + " adquiriu automaticamente uma tag sem IdentidadeUnidade.");
        Assert.That(observedCombatLogs, Is.Empty,
            "O Update real chamou disparo ou criou projétil: " + string.Join(" | ", observedCombatLogs));
        Assert.That(Convert.ToSingle(GetMember(targetDamage, "vidaAtual")), Is.EqualTo(healthBefore).Within(0.001f),
            "O alvo tag-only recebeu dano.");

        if (turretTypeName == "ControleTorreta")
        {
            Assert.That(Convert.ToInt32(GetMember(turret, "MunicaoAtual")), Is.EqualTo(ammoBefore),
                "A torreta consumiu munição sem alvo autorizado.");
        }
        else
        {
            Assert.That(Convert.ToInt32(GetMember(configuredWeapons[0], "municaoAtual")), Is.EqualTo(ammoBefore),
                "O módulo consumiu munição sem alvo autorizado.");
        }

        UnityEngine.Object[] afterProjectiles = UnityEngine.Object.FindObjectsByType(
            projectileType, FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < afterProjectiles.Length; i++)
            Assert.That(projectileIdsBefore.Contains(afterProjectiles[i].GetInstanceID()), Is.True,
                "Uma instância nova de projétil foi criada pelo alvo tag-only.");
    }

    private GameObject Track(GameObject gameObject)
    {
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static Component FindOwnedProjectile(Type projectileType, GameObject owner)
    {
        MethodInfo getOwner = projectileType.GetMethod("GetDono", InstanceMembers);
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour candidate = behaviours[i];
            if (candidate == null || candidate.GetType() != projectileType) continue;
            if (candidate.gameObject.name.IndexOf("Bala", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (getOwner != null && ReferenceEquals(getOwner.Invoke(candidate, null), owner)) return candidate;
        }
        return null;
    }

    private static Type ResolveType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name, false);
            if (type != null) return type;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types; }
            for (int i = 0; i < types.Length; i++)
                if (types[i] != null && types[i].Name == name) return types[i];
        }
        return null;
    }

    private static object GetMember(object target, string name)
    {
        if (target == null) return null;
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null) return field.GetValue(target);
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null) return property.GetValue(target, null);
        throw new MissingMemberException(type.FullName, name);
    }

    private static void SetMember(object target, string name, object value)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, InstanceMembers);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }
        PropertyInfo property = type.GetProperty(name, InstanceMembers);
        if (property != null)
        {
            property.SetValue(target, value, null);
            return;
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private void OnValidationSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.Equals(scene.path, ValidationScenePath, StringComparison.OrdinalIgnoreCase))
            DisableGameplayControllers(scene);
    }

    private static void DisableGameplayControllers(Scene scene)
    {
        string[] disabledTypes =
        {
            "IA_BrainMaster", "IA03EstrategaNacional", "PatrulhaCombateSegura",
            "ControleTorreta", "ControleTorretaModular", "SistemaDeTiro", "Torreta", "TorretaAntiaerea"
        };
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MonoBehaviour[] behaviours = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                MonoBehaviour behaviour = behaviours[j];
                if (behaviour == null) continue;
                for (int k = 0; k < disabledTypes.Length; k++)
                {
                    if (!string.Equals(behaviour.GetType().Name, disabledTypes[k], StringComparison.Ordinal)) continue;
                    behaviour.enabled = false;
                    break;
                }
            }
        }
    }
}
