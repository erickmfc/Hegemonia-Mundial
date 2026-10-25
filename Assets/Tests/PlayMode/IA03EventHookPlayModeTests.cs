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
    public IEnumerator RealStructuralDamageUpdatesConflictReportFromParentIdentity()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type damageType = ResolveType("SistemaDeDanos");
        Type identityType = ResolveType("IdentidadeUnidade");
        var objects = new List<GameObject>();
        GameObject observer = new GameObject("IA03 structural damage listener test");
        observer.SetActive(false);
        objects.Add(observer);

        try
        {
            Component brain = observer.AddComponent(brainType);
            SetField(brain, "TeamId", 1);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            ((Behaviour)brain).enabled = false;
            Component strategist = observer.AddComponent(strategistType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 2);
            observer.SetActive(true);
            yield return null;

            GameObject attacker = CreateEntity(objects, identityType, damageType, 1, false, "Structural damage attacker");
            GameObject target = CreateEntity(objects, identityType, damageType, 2, true, "Structural damage target");
            GameObject attackerChild = new GameObject("attacker weapon child");
            attackerChild.transform.SetParent(attacker.transform);
            objects.Add(attackerChild);

            Component targetDamage = target.GetComponent(damageType);
            SetField(targetDamage, "vidaAtual", 100f);
            MethodInfo receiveDamage = damageType.GetMethod("ReceberDano", Members);
            Assert.That(receiveDamage, Is.Not.Null);

            receiveDamage.Invoke(targetDamage, new object[] { 25f, attackerChild });
            Assert.That(ReportFloat(strategist, strategistType, "DanoEstruturalInimigo"),
                Is.EqualTo(25f).Within(0.001f),
                "O hook global deve resolver a identidade do agressor ancestral e registrar o dano estrutural efetivo.");
            Assert.That(ReportFloat(strategist, strategistType, "DanoEstruturalProprio"),
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(ReportFloat(strategist, strategistType, "PrejuizoEconomicoInimigo"),
                Is.EqualTo(0f).Within(0.001f),
                "Dano estrutural parcial registra HP efetivamente aplicado, sem inventar prejuízo econômico.");
            Assert.That(ReportFloat(strategist, strategistType, "PrejuizoEconomicoProprio"),
                Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator FirstPlayerAttackRetargetsPeacefulIA03AndRaisesTension()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type damageType = ResolveType("SistemaDeDanos");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type governmentType = ResolveType("SistemaGovernoMundial");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo setGovernmentInstance = governmentInstance.GetSetMethod(true);
        object previousGovernment = governmentInstance.GetValue(null);
        var objects = new List<GameObject>();

        GameObject observer = new GameObject("IA03 player attack response test");
        observer.SetActive(false);
        objects.Add(observer);

        try
        {
            Component brain = observer.AddComponent(brainType);
            SetField(brain, "TeamId", 2);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            ((Behaviour)brain).enabled = false;

            Component strategist = observer.AddComponent(strategistType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 3);
            observer.SetActive(true);
            yield return null;

            GameObject attacker = CreateEntity(objects, identityType, damageType, 1, false, "Player team 1 attacker");
            GameObject target = CreateEntity(objects, identityType, damageType, 2, false, "Valdoria team 2 target");
            Component targetDamage = target.GetComponent(damageType);
            SetField(targetDamage, "vidaAtual", 100f);

            // Isola a resposta estratégica ao evento de dano; o governo e seus dados de partida
            // permanecem intocados durante o teste.
            setGovernmentInstance.Invoke(null, new object[] { null });
            MethodInfo receiveDamage = damageType.GetMethod("ReceberDano", Members);
            Assert.That(receiveDamage, Is.Not.Null);
            receiveDamage.Invoke(targetDamage, new object[] { 10f, attacker });

            Assert.That((int)ReadField(strategist, "paisAlvoTeamId"), Is.EqualTo(1),
                "Ao sofrer dano do jogador, a IA03 deve priorizar o agressor real em vez do rival fixo da cena.");
            Assert.That(strategistType.GetProperty("NivelDeConflito").GetValue(strategist).ToString(), Is.EqualTo("Tensao"),
                "O primeiro ataque deve abrir uma crise de tensão para a IA03.");
        }
        finally
        {
            setGovernmentInstance.Invoke(null, new[] { previousGovernment });
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
            }
        }

        yield return null;
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
        Type metadataType = ResolveType("Hegemonia.AI.BrainMaster.IA_ConstructionMetadata");
        Type constructionDataType = ResolveType("DadosConstrucao");
        var objects = new List<GameObject>();
        var constructionData = new List<ScriptableObject>();
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
            SetField(strategist, "nivelDeConflito", Enum.Parse(ResolveType("Hegemonia.AI.IA03.IA03NivelConflito"), "ConflitoLimitado"));
            observer.SetActive(true);
            yield return null;

            GameObject friendlyAttacker = CreateEntity(objects, identityType, damageType, 1, false, "Friendly attacker");
            GameObject enemyUnit = CreateEntity(objects, identityType, damageType, 2, false, "Enemy unit");
            GameObject enemyStructure = CreateEntity(objects, identityType, damageType, 2, true, "Enemy structure");
            GameObject enemyAttacker = CreateEntity(objects, identityType, damageType, 2, false, "Enemy attacker");
            GameObject friendlyUnit = CreateEntity(objects, identityType, damageType, 1, false, "Friendly unit");
            GameObject friendlyStructure = CreateEntity(objects, identityType, damageType, 1, true, "Friendly structure");
            AttachReplacementCost(constructionData, enemyUnit, constructionDataType, metadataType, 1200L);
            AttachReplacementCost(constructionData, enemyStructure, constructionDataType, metadataType, 8500L);
            AttachReplacementCost(constructionData, friendlyUnit, constructionDataType, metadataType, 1500L);
            AttachReplacementCost(constructionData, friendlyStructure, constructionDataType, metadataType, 2300L);
            GameObject missionUnit = new GameObject("Mission group unit");
            objects.Add(missionUnit);
            GameObject missionPoint = new GameObject("Mission Creaty");
            missionPoint.SetActive(false);
            objects.Add(missionPoint);
            Component creaty = missionPoint.AddComponent(creatyType);
            SetField(creaty, "id", "IA03-RELATORIO-TEST-" + missionPoint.GetInstanceID());

            SetField(mission, "tipoMissao", Enum.Parse(ResolveType("Hegemonia.AI.IA03.IA03TipoMissao"), "AtaqueLimitado"));
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
            Assert.That(ReportValue(strategist, strategistType, "BatalhasVencidas"), Is.EqualTo(1),
                "A destruição do alvo explicitamente definido deve registrar exatamente uma vitória ofensiva.");
            NotifyDeath(notifyDeath, enemyStructure, damageType, friendlyAttacker);
            NotifyDeath(notifyDeath, friendlyUnit, damageType, enemyAttacker);
            NotifyDeath(notifyDeath, friendlyStructure, damageType, enemyAttacker);

            Type combatRegistryType = ResolveType("CartaCombateRegistro");
            IEnumerable combatEvents = (IEnumerable)combatRegistryType.GetProperty("Eventos").GetValue(null);
            object enemyUnitEvent = combatEvents.Cast<object>().FirstOrDefault(entry =>
                string.Equals(Convert.ToString(ReadField(entry, "alvo")), enemyUnit.name)
                && Convert.ToInt32(ReadField(entry, "equipeAlvo")) == 2
                && Convert.ToInt32(ReadField(entry, "equipeAtacante")) == 1);
            Assert.That(enemyUnitEvent, Is.Not.Null, "A morte deve publicar um evento identificável para o relatório.");
            strategistType.GetMethod("AoRegistrarCombate", Members).Invoke(strategist, new[] { enemyUnitEvent });

            Assert.That(ReportValue(strategist, strategistType, "InimigosDestruidos"), Is.EqualTo(1));
            Assert.That(ReportValue(strategist, strategistType, "EstruturasInimigasDestruidas"), Is.EqualTo(1));
            Assert.That(ReportValue(strategist, strategistType, "UnidadesPropriasPerdidas"), Is.EqualTo(1));
            Assert.That(ReportValue(strategist, strategistType, "EstruturasPropriasDestruidas"), Is.EqualTo(1));
            Assert.That(ReportFloat(strategist, strategistType, "PrejuizoEconomicoInimigo"), Is.EqualTo(9700f));
            Assert.That(ReportFloat(strategist, strategistType, "PrejuizoEconomicoProprio"), Is.EqualTo(3800f));
            Assert.That(ReportValue(strategist, strategistType, "BatalhasPerdidas"), Is.EqualTo(0));

            GameObject enemyUnitWithoutCost = CreateEntity(objects, identityType, damageType, 2, false, "IA03 unknown cost target");
            NotifyDeath(notifyDeath, enemyUnitWithoutCost, damageType, friendlyAttacker);
            Assert.That(ReportValue(strategist, strategistType, "InimigosDestruidos"), Is.EqualTo(2));
            Assert.That(ReportFloat(strategist, strategistType, "PrejuizoEconomicoInimigo"), Is.EqualTo(9700f));

            observer.SetActive(false);
            yield return null;
            GameObject enemyAfterDisable = CreateEntity(objects, identityType, damageType, 2, false, "Enemy unit after disable");
            yield return null;
            NotifyDeath(notifyDeath, enemyAfterDisable, damageType, friendlyAttacker);
            Assert.That(ReportValue(strategist, strategistType, "InimigosDestruidos"), Is.EqualTo(2));
        }
        finally
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
            }
            for (int i = 0; i < constructionData.Count; i++)
            {
                if (constructionData[i] != null) UnityEngine.Object.Destroy(constructionData[i]);
            }
            if (mission != null) UnityEngine.Object.Destroy(mission);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator PeriodicConflictReportEmitsAfterFiveSimulatedMinutesThroughTick()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type governmentType = ResolveType("SistemaGovernoMundial");
        Type profileType = ResolveType("Hegemonia.AI.IA03.PerfilPaisSO");
        Type damageType = ResolveType("SistemaDeDanos");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type worldStateType = ResolveType("Hegemonia.AI.BrainMaster.IA_WorldState");
        PropertyInfo governmentInstance = governmentType.GetProperty(
            "Instancia", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object previousGovernment = governmentInstance.GetValue(null, null);
        GameObject governmentObject = new GameObject("IA03 periodic report government");
        governmentObject.SetActive(false);
        GameObject observer = new GameObject("IA03 periodic report tick test");
        observer.SetActive(false);
        var objects = new List<GameObject> { governmentObject, observer };
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        Component government = null;
        EventInfo newsEvent = null;
        Action<string> newsHandler = null;
        string reportNews = null;

        try
        {
            government = governmentObject.AddComponent(governmentType);
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { government });
            Component brain = observer.AddComponent(brainType);
            SetField(brain, "TeamId", 1);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            ((Behaviour)brain).enabled = false;
            object context = Activator.CreateInstance(ResolveType("Hegemonia.AI.BrainMaster.IA_Context"));
            object worldState = Activator.CreateInstance(worldStateType, new object[] { 1 });
            SetField(context, "WorldState", worldState);
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });

            Component strategist = observer.AddComponent(strategistType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "perfilPais", profile);
            SetField(strategist, "perfilAplicado", true);
            SetField(strategist, "paisAlvoTeamId", 2);
            SetField(strategist, "nivelDeConflito", Enum.Parse(ResolveType("Hegemonia.AI.IA03.IA03NivelConflito"), "ConflitoLimitado"));
            SetField(strategist, "estadoNacional", Enum.Parse(ResolveType("Hegemonia.AI.IA03.IA03EstadoNacional"), "ConflitoLimitado"));
            float conflictStart = Time.time + 1f;
            SetField(strategist, "inicioGuerraEm", conflictStart);
            SetField(strategist, "nivelDebugForcado", true);
            observer.SetActive(true);
            yield return null;

            float reportDueAt = Time.time + 300f;
            float simulatedNow = reportDueAt + 0.1f;
            SetField(strategist, "proximoRelatorioEm", reportDueAt);
            SetField(strategist, "proximaDecisaoDeMissaoEm", simulatedNow + 1000f);
            newsEvent = governmentType.GetEvent("OnNoticia", BindingFlags.Instance | BindingFlags.Public);
            newsHandler = message =>
            {
                if (message != null && message.StartsWith("Relatório IA03 ", StringComparison.Ordinal))
                {
                    reportNews = message;
                }
            };
            newsEvent.AddEventHandler(government, newsHandler);

            GameObject attacker = CreateEntity(objects, identityType, damageType, 1, false, "IA03 report attacker");
            GameObject enemy = CreateEntity(objects, identityType, damageType, 2, false, "IA03 report enemy");
            GameObject knownEnemyVisible = CreateEntity(objects, identityType, damageType, 2, false, "IA03 report visible contact");
            GameObject survivingUnitA = CreateEntity(objects, identityType, damageType, 1, false, "IA03 surviving force A");
            GameObject survivingUnitB = CreateEntity(objects, identityType, damageType, 1, false, "IA03 surviving force B");
            GameObject lostUnitA = CreateEntity(objects, identityType, damageType, 1, false, "IA03 lost force A");
            GameObject lostUnitB = CreateEntity(objects, identityType, damageType, 1, false, "IA03 lost force B");
            GameObject enemyAttacker = CreateEntity(objects, identityType, damageType, 2, false, "IA03 report enemy attacker");
            PropertyInfo ownCombatUnitsProperty = worldStateType.GetProperty("OwnCombatUnits", Members);
            Assert.That(ownCombatUnitsProperty, Is.Not.Null,
                "O WorldState deve expor OwnCombatUnits como propriedade para o relatório IA03.");
            IList ownCombatUnits = ownCombatUnitsProperty.GetValue(worldState, null) as IList;
            Assert.That(ownCombatUnits, Is.Not.Null,
                "OwnCombatUnits deve estar inicializada antes de acumular as unidades do relatório.");
            ownCombatUnits.Add(attacker);
            ownCombatUnits.Add(survivingUnitA);
            ownCombatUnits.Add(survivingUnitB);

            Type enemyObservationType = ResolveType("Hegemonia.AI.BrainMaster.IA_EnemyObservation");
            IDictionary enemyMemory = (IDictionary)worldStateType.GetField("_enemyMemoryById", Members).GetValue(worldState);
            float observedAt = Time.unscaledTime;
            AddEnemyMemoryObservation(7001, knownEnemyVisible.transform, 2, false, observedAt, enemyObservationType, enemyMemory);
            AddEnemyMemoryObservation(7002, null, 2, false, observedAt, enemyObservationType, enemyMemory);
            AddEnemyMemoryObservation(7003, null, 3, false, observedAt, enemyObservationType, enemyMemory);
            AddEnemyMemoryObservation(7004, null, 2, true, observedAt, enemyObservationType, enemyMemory);
            IList visibleEnemies = (IList)worldStateType.GetField("VisibleEnemies").GetValue(worldState);
            visibleEnemies.Add(enemyMemory[7001]);

            Type combatRegistryType = ResolveType("CartaCombateRegistro");
            MethodInfo registerDeath = combatRegistryType.GetMethod(
                "RegistrarUnidadeDestruida",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(registerDeath, Is.Not.Null);
            registerDeath.Invoke(null, new object[] { lostUnitA.GetComponent(damageType), enemyAttacker });
            registerDeath.Invoke(null, new object[] { lostUnitB.GetComponent(damageType), enemyAttacker });

            Component enemyDamage = enemy.GetComponent(damageType);
            SetField(enemyDamage, "vidaMaxima", 10f);
            SetField(enemyDamage, "vidaAtual", 10f);
            MethodInfo receiveDamage = damageType.GetMethod("ReceberDano", Members);
            receiveDamage.Invoke(enemyDamage, new object[] { 20f, attacker });

            MethodInfo tick = strategistType.GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tick, Is.Not.Null);
            tick.Invoke(strategist, new object[] { simulatedNow, 0.02f });

            object snapshot = strategistType.GetProperty("RelatorioAtual").GetValue(strategist);
            float expectedDuration = simulatedNow - conflictStart;
            Assert.That(reportNews, Is.Not.Null, "O relatório periódico deve ser publicado pelo caminho de runtime do Tick.");
            Assert.That(reportNews, Does.Contain("inimigos conhecidos=2"),
                "O relatório deve incluir o último contato lembrado do país-alvo sem somar outro país hostil.");
            Assert.That(reportNews, Does.Contain("inimigos destruídos=1"));
            Assert.That(reportNews, Does.Contain("reposição conhecida de ativos destruídos"));
            Assert.That(reportNews, Does.Contain("missões ofensivas vencidas/perdidas=0/0"));
            Assert.That(reportNews, Does.Contain("duração do conflito=" + Mathf.RoundToInt(expectedDuration) + "s"));
            Assert.That(Convert.ToSingle(ReadField(snapshot, "MomentoDoRelatorio")), Is.EqualTo(simulatedNow).Within(0.001f));
            Assert.That(Convert.ToSingle(ReadField(snapshot, "DuracaoConflitoSegundos")), Is.EqualTo(expectedDuration).Within(0.001f));
            Assert.That(ReportValue(strategist, strategistType, "InimigosDestruidos"), Is.EqualTo(1));
            Assert.That(ReportValue(strategist, strategistType, "UnidadesInimigasConhecidas"), Is.EqualTo(2));
            Assert.That(ReportValue(strategist, strategistType, "UnidadesPropriasPerdidas"), Is.EqualTo(2));
            Assert.That(ReportValue(strategist, strategistType, "UnidadesPropriasDisponiveis"), Is.EqualTo(3));
            Assert.That(ReportFloat(strategist, strategistType, "CapacidadeMilitarRestante"), Is.EqualTo(0.6f).Within(0.001f),
                "A primeira janela deve usar unidades sobreviventes + baixas registradas como força comprometida.");
            Assert.That(ReportFloat(strategist, strategistType, "PrejuizoEconomicoInimigo"), Is.EqualTo(0f),
                "Sem custo conhecido o relatório não deve inventar valor econômico.");
            Assert.That(ReadField(strategist, "missaoAtiva"), Is.Null);
            Assert.That(ReadField(strategist, "nivelDeConflito").ToString(), Is.EqualTo("ConflitoLimitado"));
            int newsCount = ((ICollection)ReadField(government, "noticias")).Count;

            tick.Invoke(strategist, new object[] { simulatedNow, 0.02f });
            Assert.That(((ICollection)ReadField(government, "noticias")).Count, Is.EqualTo(newsCount),
                "A mesma janela temporal não deve emitir nem acumular o mesmo relatório novamente.");
            Assert.That(ReportValue(strategist, strategistType, "InimigosDestruidos"), Is.EqualTo(1));

            GameObject lostUnitC = CreateEntity(objects, identityType, damageType, 1, false, "IA03 lost force C");
            registerDeath.Invoke(null, new object[] { lostUnitC.GetComponent(damageType), enemyAttacker });
            ownCombatUnits.Remove(survivingUnitB);
            survivingUnitB.SetActive(false);
            float secondReportDueAt = simulatedNow + 300f;
            float secondReportTime = secondReportDueAt + 0.1f;
            SetField(strategist, "proximoRelatorioEm", secondReportDueAt);
            tick.Invoke(strategist, new object[] { secondReportTime, 0.02f });

            object secondSnapshot = strategistType.GetProperty("RelatorioAtual").GetValue(strategist);
            Assert.That(((ICollection)ReadField(government, "noticias")).Count, Is.EqualTo(newsCount + 1));
            Assert.That(ReportValue(strategist, strategistType, "UnidadesPropriasDisponiveis"), Is.EqualTo(2));
            Assert.That(ReportValue(strategist, strategistType, "UnidadesPropriasPerdidas"), Is.EqualTo(3));
            Assert.That(Convert.ToSingle(ReadField(secondSnapshot, "CapacidadeMilitarRestante")),
                Is.EqualTo(0.4f).Within(0.001f),
                "A força comprometida não deve voltar a somar perdas já incluídas em relatórios anteriores.");
        }
        finally
        {
            if (newsEvent != null && newsHandler != null && government != null)
            {
                newsEvent.RemoveEventHandler(government, newsHandler);
            }
            governmentInstance.GetSetMethod(true).Invoke(null, new[] { previousGovernment });
            if (observer != null) observer.SetActive(false);
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
            }
            if (profile != null) UnityEngine.Object.Destroy(profile);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator GuidedMissileKillKeepsRegisteredLaunchTeamForConflictReport()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type damageType = ResolveType("SistemaDeDanos");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type missileType = ResolveType("MissilTeleguiado");
        Type trackerType = ResolveType("MissileThreatTracker");
        var objects = new List<GameObject>();
        GameObject observer = new GameObject("IA03 guided missile attribution test");
        observer.SetActive(false);
        objects.Add(observer);

        try
        {
            Component brain = observer.AddComponent(brainType);
            SetField(brain, "TeamId", 1);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            ((Behaviour)brain).enabled = false;
            Component strategist = observer.AddComponent(strategistType);
            SetField(strategist, "brain", brain);
            SetField(strategist, "paisAlvoTeamId", 2);
            observer.SetActive(true);
            yield return null;

            GameObject launcher = CreateEntity(objects, identityType, damageType, 1, false, "Guided missile launcher");
            launcher.transform.position = new Vector3(250f, 0f, 0f);
            GameObject target = CreateEntity(objects, identityType, damageType, 2, false, "Guided missile target");
            yield return null;
            Component targetDamage = target.GetComponent(damageType);
            SetField(targetDamage, "vidaMaxima", 10f);
            SetField(targetDamage, "vidaAtual", 10f);

            GameObject missile = new GameObject("Tracked guided missile");
            missile.transform.position = target.transform.position;
            objects.Add(missile);
            Component guided = missile.AddComponent(missileType);
            SetField(guided, "alvo", target.transform);

            MethodInfo registerMissile = trackerType.GetMethod("RegistrarLancamento", BindingFlags.Static | BindingFlags.Public);
            Assert.That(registerMissile, Is.Not.Null);
            registerMissile.Invoke(null, new object[]
            {
                missile,
                launcher.GetComponent(identityType),
                target.transform.position,
                target.transform,
                80f,
                false
            });

            MethodInfo applyDamage = missileType.GetMethod("AplicarDano", Members);
            Assert.That(applyDamage, Is.Not.Null);
            applyDamage.Invoke(guided, null);

            Assert.That(ReportValue(strategist, strategistType, "InimigosDestruidos"), Is.EqualTo(1),
                "A morte deve herdar o time registrado no tracker do míssil, mesmo sem atacante próximo ao alvo.");
        }
        finally
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
            }
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator RealReservedAttackerLossCountsOneOffensiveDefeatAndCleansMission()
    {
        Type strategistType = ResolveType("Hegemonia.AI.IA03.IA03EstrategaNacional");
        Type brainType = ResolveType("Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Type damageType = ResolveType("SistemaDeDanos");
        Type identityType = ResolveType("IdentidadeUnidade");
        Type missionType = ResolveType("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        Type creatyType = ResolveType("Hegemonia.AI.IA03.CreatyEstrategico");
        Type metadataType = ResolveType("Hegemonia.AI.BrainMaster.IA_ConstructionMetadata");
        Type constructionDataType = ResolveType("DadosConstrucao");
        var objects = new List<GameObject>();
        var constructionData = new List<ScriptableObject>();
        GameObject observer = new GameObject("IA03 real attacker loss listener test");
        observer.SetActive(false);
        objects.Add(observer);
        GameObject missionPoint = new GameObject("IA03 loss mission Creaty");
        missionPoint.SetActive(false);
        objects.Add(missionPoint);
        ScriptableObject mission = ScriptableObject.CreateInstance(missionType);
        GameObject attacker = null;
        try
        {
            Component brain = observer.AddComponent(brainType);
            SetField(brain, "TeamId", 1);
            FieldInfo integrationMode = brainType.GetField("IntegrationMode");
            SetField(brain, "IntegrationMode", Enum.Parse(integrationMode.FieldType, "Hybrid"));
            ((Behaviour)brain).enabled = false;
            Component strategist = observer.AddComponent(strategistType);
            Component creaty = missionPoint.AddComponent(creatyType);
            SetField(creaty, "id", "IA03-RELATORIO-TEST-" + missionPoint.GetInstanceID());
            object context = System.Activator.CreateInstance(ResolveType("Hegemonia.AI.BrainMaster.IA_Context"));
            brainType.GetProperty("Context").GetSetMethod(true).Invoke(brain, new[] { context });

            attacker = CreateEntity(objects, identityType, damageType, 1, false, "IA03 reserved attacker destroyed");
            GameObject enemyTarget = CreateEntity(objects, identityType, damageType, 2, false, "IA03 offensive target survives");
            GameObject enemyAttacker = CreateEntity(objects, identityType, damageType, 2, false, "IA03 enemy attacker");
            Component attackerDamage = attacker.GetComponent(damageType);
            SetField(attackerDamage, "vidaMaxima", 100f);
            SetField(attackerDamage, "vidaAtual", 100f);
            AttachReplacementCost(constructionData, attacker, constructionDataType, metadataType, 1500L);

            SetField(mission, "tipoMissao", Enum.Parse(ResolveType("Hegemonia.AI.IA03.IA03TipoMissao"), "AtaqueLimitado"));
            SetField(mission, "condicaoDeSucesso", Enum.Parse(ResolveType("Hegemonia.AI.IA03.IA03CondicaoMissao"), "DestruirAlvo"));
            SetField(strategist, "brain", brain);
            SetField(strategist, "nivelDeConflito", Enum.Parse(ResolveType("Hegemonia.AI.IA03.IA03NivelConflito"), "ConflitoLimitado"));
            SetField(strategist, "missaoAtiva", mission);
            SetField(strategist, "creatyAtivo", creaty);
            SetField(strategist, "paisAlvoTeamId", 2);
            SetField(strategist, "equipeAlvoAtiva", 2);
            SetField(strategist, "unidadesReservadas", 1);
            SetField(strategist, "unidadesOriginaisNaMissao", 1);
            SetField(strategist, "inicioMissaoEm", Time.time);
            SetField(strategist, "alvoMissaoFoiDefinido", true);
            SetField(strategist, "alvoMissaoAtivo", enemyTarget.transform);
            SetField(strategist, "idPersistenteAlvoMissao", "runtime-" + enemyTarget.GetInstanceID());
            ((List<GameObject>)ReadField(strategist, "unidadesAtivasNaMissao")).Add(attacker);
            missionPoint.SetActive(true);
            Assert.That(Convert.ToBoolean(creatyType.GetMethod("TentarReservar").Invoke(creaty, new object[] { 1, 2, 1 })), Is.True);
            observer.SetActive(true);
            yield return null;

            int ownLossesBefore = ReportValue(strategist, strategistType, "UnidadesPropriasPerdidas");
            int winsBefore = ReportValue(strategist, strategistType, "BatalhasVencidas");
            int lossesBefore = ReportValue(strategist, strategistType, "BatalhasPerdidas");
            float ownCostBefore = ReportFloat(strategist, strategistType, "PrejuizoEconomicoProprio");
            MethodInfo receiveDamage = damageType.GetMethod("ReceberDano", Members);
            Assert.That(receiveDamage, Is.Not.Null);
            receiveDamage.Invoke(attackerDamage, new object[] { 1000f, enemyAttacker });
            Assert.That(ReadField(strategist, "grupoPerdeuUnidade"), Is.True,
                "O evento real de morte da unidade reservada precisa marcar perda do grupo.");
            strategistType.GetMethod("ProcessarMissaoAtiva", Members).Invoke(strategist, new object[] { Time.time });

            Assert.That(strategistType.GetProperty("EstadoDaMissao").GetValue(strategist).ToString(), Is.EqualTo("Fracasso"));
            Assert.That(ReportValue(strategist, strategistType, "UnidadesPropriasPerdidas"), Is.EqualTo(ownLossesBefore + 1));
            Assert.That(ReportValue(strategist, strategistType, "BatalhasVencidas"), Is.EqualTo(winsBefore));
            Assert.That(ReportValue(strategist, strategistType, "BatalhasPerdidas"), Is.EqualTo(lossesBefore + 1));
            Assert.That(ReportFloat(strategist, strategistType, "PrejuizoEconomicoProprio"), Is.EqualTo(ownCostBefore + 1500f));
            Assert.That(ReadField(strategist, "missaoAtiva"), Is.Null);
            Assert.That(Convert.ToInt32(ReadField(strategist, "unidadesReservadas")), Is.EqualTo(0));
            Assert.That(ReadField(strategist, "creatyAtivo"), Is.Null);
            Assert.That(Convert.ToInt32(creatyType.GetProperty("UnidadesReservadas").GetValue(creaty)), Is.EqualTo(0));
            Assert.That(((List<GameObject>)ReadField(strategist, "unidadesAtivasNaMissao")).Count, Is.EqualTo(0));
        }
        finally
        {
            if (observer != null) observer.SetActive(false);
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null) UnityEngine.Object.Destroy(objects[i]);
            }
            for (int i = 0; i < constructionData.Count; i++)
            {
                if (constructionData[i] != null) UnityEngine.Object.Destroy(constructionData[i]);
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

    private static void AddEnemyMemoryObservation(
        int instanceId,
        Transform transform,
        int teamId,
        bool isStructure,
        float observedAt,
        Type observationType,
        IDictionary memory)
    {
        object observation = Activator.CreateInstance(observationType);
        SetField(observation, "InstanceId", instanceId);
        SetField(observation, "TeamId", teamId);
        SetField(observation, "Transform", transform);
        SetField(observation, "LastSeenTime", observedAt);
        SetField(observation, "ForgetAt", observedAt + 120f);
        SetField(observation, "IsStructure", isStructure);
        memory.Add(instanceId, observation);
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

    private static float ReportFloat(Component strategist, Type strategistType, string fieldName)
    {
        object snapshot = strategistType.GetProperty("RelatorioAtual").GetValue(strategist);
        return (float)snapshot.GetType().GetField(fieldName).GetValue(snapshot);
    }

    private static void AttachReplacementCost(
        List<ScriptableObject> constructionData,
        GameObject entity,
        Type constructionDataType,
        Type metadataType,
        long replacementCost)
    {
        ScriptableObject data = ScriptableObject.CreateInstance(constructionDataType);
        data.name = entity.name + " data";
        constructionData.Add(data);
        constructionDataType.GetProperty("NomeItem").SetValue(data, entity.name);
        constructionDataType.GetProperty("PrefabDaUnidade").SetValue(data, entity);
        SetField(data, "precoDefinitivo", replacementCost);
        Component metadata = entity.AddComponent(metadataType);
        metadataType.GetMethod("ApplyFrom", Members).Invoke(metadata, new[] { data });
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
