using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class IA03EventHookPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    // Keep the test assembly independent from Assembly-CSharp; this name matches IA_BrainMaster's existing guard.
    private const string BrainMasterGuardSceneName = "cena19)";
    private Scene sceneAtEntry;
    private Scene sceneWithBrainInitializationGuard;
    private bool createdBrainInitializationGuardScene;

    [UnitySetUp]
    public IEnumerator UseLightweightSceneForBrainMasterFixture()
    {
        sceneAtEntry = SceneManager.GetActiveScene();
        sceneWithBrainInitializationGuard = SceneManager.GetSceneByName(BrainMasterGuardSceneName);
        createdBrainInitializationGuardScene = !sceneWithBrainInitializationGuard.IsValid()
                                               || !sceneWithBrainInitializationGuard.isLoaded;
        if (createdBrainInitializationGuardScene)
        {
            sceneWithBrainInitializationGuard = SceneManager.CreateScene(BrainMasterGuardSceneName);
        }

        Assert.That(SceneManager.SetActiveScene(sceneWithBrainInitializationGuard), Is.True);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator RestoreSceneAfterBrainMasterFixture()
    {
        if (sceneAtEntry.IsValid() && sceneAtEntry.isLoaded)
        {
            SceneManager.SetActiveScene(sceneAtEntry);
        }

        if (createdBrainInitializationGuardScene
            && sceneWithBrainInitializationGuard.IsValid()
            && sceneWithBrainInitializationGuard.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(sceneWithBrainInitializationGuard);
            if (unload != null)
            {
                yield return unload;
            }
        }

        sceneAtEntry = default;
        sceneWithBrainInitializationGuard = default;
        createdBrainInitializationGuardScene = false;
    }

    [UnityTest]
    public IEnumerator RealUnitDeathUpdatesConflictReportAndDisableUnsubscribes()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type damageType = ResolveType("SistemaDeDanos");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type missionType = ResolveType("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type creatyType = ResolveType("Hegemonia.AI.IA03.CreatyEstrategico");
        var objects = new List<GameObject>();
        GameObject observer = new GameObject("IA03 event listener PlayMode test");
        observer.SetActive(false);
        objects.Add(observer);

        Component strategist = null;
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        try
        {
            Component brain = observer.AddComponent(brainType);
            SetField(brain, "TeamId", 1);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            ((Behaviour)brain).enabled = false;
            strategist = observer.AddComponent(strategistType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 2);
            observer.SetActive(true);
            yield return null;

            GameObject friendlyAttacker = CreateEntity(objects, identityType, damageType, 1, false, "Friendly attacker");
            GameObject enemyUnit = CreateEntity(objects, identityType, damageType, 2, false, "Enemy unit");
            GameObject enemyStructure = CreateEntity(objects, identityType, damageType, 2, true, "Enemy structure");
            GameObject enemyAttacker = CreateEntity(objects, identityType, damageType, 2, false, "Enemy attacker");
            GameObject friendlyUnit = CreateEntity(objects, identityType, damageType, 1, false, "Friendly unit");
            GameObject missionUnit = new GameObject("Mission group unit");
            objects.Add(missionUnit);
            GameObject missionPoint = new GameObject("Mission Creaty");
            missionPoint.SetActive(false);
            objects.Add(missionPoint);
            Component creaty = missionPoint.AddComponent(creatyType);

            SetField(mission, "condicaoDeSucesso", Enum.Parse(ResolveType("Hegemonia.AI.IA03.IA03CondicaoMissao"), "DestruirAlvo"));
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "equipeAlvoAtiva", 2);
            SetField(strategist, "inicioMissaoEm", Time.time);
            SetField(strategist, "unidadesOriginaisNaMissao", 1);
            SetField(strategist, "alvoMissaoFoiDefinido", true);
            SetField(strategist, "alvoMissaoAtivo", enemyUnit.transform);
            SetField(strategist, "idPersistenteAlvoMissao", "runtime-" + enemyUnit.GetInstanceID());
            ((List<GameObject>)ReadField(strategist, "unidadesAtivasNaMissao")).Add(missionUnit);
            yield return null;

            MethodInfo notifyDeath = damageType.GetMethod("NotificarMorte", Members);
            Assert.That(notifyDeath, Is.Not.Null);
            NotifyDeath(notifyDeath, enemyUnit, damageType, friendlyAttacker);
            Assert.That(ReadField(strategist, "alvoMissaoDestruido"), Is.EqualTo(true));
            strategistType.GetMethod("ProcessarMissaoAtiva", Members).Invoke(strategist, new object[] { Time.time });
            Assert.That(strategistType.GetProperty("EstadoDaMissao").GetValue(strategist).ToString(), Is.EqualTo("Sucesso"));
            NotifyDeath(notifyDeath, enemyStructure, damageType, friendlyAttacker);
            NotifyDeath(notifyDeath, friendlyUnit, damageType, enemyAttacker);

            Assert.That(ReportValue(strategist, strategistType, "InimigosDestruidos"), Is.EqualTo(1));
            Assert.That(ReportValue(strategist, strategistType, "EstruturasInimigasDestruidas"), Is.EqualTo(1));
            Assert.That(ReportValue(strategist, strategistType, "UnidadesPropriasPerdidas"), Is.EqualTo(1));

            observer.SetActive(false);
            yield return null;
            GameObject enemyAfterDisable = CreateEntity(objects, identityType, damageType, 2, false, "Enemy unit after disable");
            yield return null;
            NotifyDeath(notifyDeath, enemyAfterDisable, damageType, friendlyAttacker);
            Assert.That(ReportValue(strategist, strategistType, "InimigosDestruidos"), Is.EqualTo(1));
        }
        finally
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
            }
            if (mission != null) UnityEngine.Object.Destroy(mission);
        }

        yield return null;
    }

    private static GameObject CreateEntity(
        List<GameObject> objects,
        Type identityType,
        Type damageType,
        int teamId,
        bool isStructure,
        string name)
    {
        var entity = new GameObject(name);
        entity.SetActive(false);
        objects.Add(entity);
        Component identity = entity.AddComponent(identityType);
        SetField(identity, "teamID", teamId);
        Component damage = entity.AddComponent(damageType);
        SetField(damage, "ehEstrutura", isStructure);
        entity.SetActive(true);
        return entity;
    }

    private static void NotifyDeath(MethodInfo notifyDeath, GameObject victim, Type damageType, GameObject aggressor)
    {
        Component damage = victim.GetComponent(damageType);
        SetField(damage, "ultimoAgressor", aggressor);
        notifyDeath.Invoke(damage, null);
    }

    private static int ReportValue(Component strategist, Type strategistType, string fieldName)
    {
        object snapshot = strategistType.GetProperty("RelatorioAtual").GetValue(strategist);
        return (int)snapshot.GetType().GetField(fieldName).GetValue(snapshot);
    }

    private static object ReadField(object target, string name)
    {
        return target.GetType().GetField(name, Members).GetValue(target);
    }

    private static Type ResolveType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .First(type => type != null);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, Members).SetValue(target, value);
    }
}
