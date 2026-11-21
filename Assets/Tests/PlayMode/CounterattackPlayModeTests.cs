using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CounterattackPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private readonly List<UnityEngine.Object> createdAssets = new List<UnityEngine.Object>();
    private readonly List<Action> cleanupActions = new List<Action>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = cleanupActions.Count - 1; i >= 0; i--)
        {
            try { cleanupActions[i]?.Invoke(); } catch { }
        }
        cleanupActions.Clear();

        DestroySpawnedNamedComponents("MisselNaval", "TestCounter");
        DestroySpawnedNamedComponents("MisselSubmarino", "TestCounter");

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.Destroy(createdObjects[i]);
            }
        }
        createdObjects.Clear();

        for (int i = createdAssets.Count - 1; i >= 0; i--)
        {
            if (createdAssets[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdAssets[i]);
            }
        }
        createdAssets.Clear();

        yield return null;
    }

    [UnityTest]
    public IEnumerator NavalLauncherCounterattackRespectsManualAndPassiveModesAndLaunchesInAutomatic()
    {
        GameObject ship = Track(new GameObject("TestCounterShip"));
        Component identity = Add(ship, "IdentidadeUnidade");
        Set(identity, "teamID", 2);
        Component control = Add(ship, "ControleUnidade");
        Component launcher = Add(ship, "LancadorNaval");

        GameObject missileTemplate = CreateMissileTemplate("TestCounterNavalMissile", "MisselNaval");
        Set(launcher, "prefabMissel", missileTemplate);
        Set(launcher, "municaoTotal", 2);
        Set(launcher, "municaoMaxima", 2);

        yield return null;

        MethodInfo counterMethod = launcher.GetType().GetMethod("TentarLancarContraAtaqueIA", Members);
        Assert.That(counterMethod, Is.Not.Null, "TentarLancarContraAtaqueIA precisa existir em LancadorNaval.");

        SetEnum(launcher, "modoAtual", "Manual");
        object[] args = new object[] { new Vector3(1200f, 0f, 0f), null, 4500f, null };
        Assert.That((bool)counterMethod.Invoke(launcher, args), Is.False, "Modo Manual não pode ser sobrescrito por contra-ataque automático.");
        Assert.That((int)Read(launcher, "municaoTotal"), Is.EqualTo(2));

        SetEnum(launcher, "modoAtual", "Passivo");
        args[3] = null;
        Assert.That((bool)counterMethod.Invoke(launcher, args), Is.False, "Modo Passivo não pode disparar contra-ataque.");
        Assert.That((int)Read(launcher, "municaoTotal"), Is.EqualTo(2));

        SetEnum(launcher, "modoAtual", "Automatico");
        SetEnum(launcher, "armamentoSelecionadoHud", "Torpedo");
        args[3] = null;
        Assert.That((bool)counterMethod.Invoke(launcher, args), Is.False, "Seleção travada em torpedo não deve disparar míssil estratégico.");

        SetEnum(launcher, "armamentoSelecionadoHud", "Automatico");
        Invoke(control, "DefinirModoCombate", true);
        args[3] = null;
        bool fired = (bool)counterMethod.Invoke(launcher, args);
        Assert.That(fired, Is.True, "Lançador naval automático deveria disparar contra-ataque por coordenada: " + args[3]);
        Assert.That((int)Read(launcher, "municaoTotal"), Is.EqualTo(1));

        args[3] = null;
        Assert.That((bool)counterMethod.Invoke(launcher, args), Is.False, "Segundo disparo imediato deve respeitar recarga.");
    }

    [UnityTest]
    public IEnumerator OperationalCommercialAirportCanReceiveReturningAircraft()
    {
        Type aircraftType = ResolveType("ControleAviao");
        Type airportType = ResolveType("GerenciadorAeroportoComercial");
        MethodInfo validator = aircraftType.GetMethod(
            "BasePodeReceberRetorno", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(validator, Is.Not.Null);

        GameObject airportObject = new GameObject("TestCommercialReturnAirport");
        try
        {
            airportObject.SetActive(false);
            Component airport = airportObject.AddComponent(airportType);
            GameObject runwayObject = new GameObject("TestCommercialRunway");
            runwayObject.transform.SetParent(airportObject.transform, false);
            GameObject threshold = new GameObject("TestRunwayThreshold");
            threshold.transform.SetParent(runwayObject.transform, false);
            GameObject touchdown = new GameObject("TestRunwayTouchdown");
            touchdown.transform.SetParent(runwayObject.transform, false);
            touchdown.transform.localPosition = new Vector3(100f, 0f, 0f);

            object runwayData = airportType.GetField("pista1").GetValue(airport);
            runwayData.GetType().GetField("pista").SetValue(runwayData, runwayObject.transform);
            LogAssert.Expect(LogType.Error,
                "[Comercial] ERRO: prefabAviaoComercial NÃO ESTÁ ASSOCIADO no Inspector! Aviões não irão spawnar.");
            LogAssert.Expect(LogType.Error,
                "[Comercial] ERRO: Nenhuma vaga encontrada no patio! Associe o objeto com os creates de vagas no campo 'patio'.");
            airportObject.SetActive(true);
            yield return null;

            airportType.GetMethod("PossuiRotaPousoValida").Invoke(airport, null);

            Assert.That((bool)validator.Invoke(null, new object[] { airport }), Is.True,
                "An active, powered commercial airport with a configured runway must accept returning aircraft.");
            Set(airport, "semEnergia", true);
            Assert.That((bool)validator.Invoke(null, new object[] { airport }), Is.False,
                "A commercial airport without power cannot accept returning aircraft.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(airportObject);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator RebelCombatAnimationUsesNestedControllerAndUpdatesWalkBlend()
    {
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Library/Prefabs/IA rebelde/Animacoes/1920s_Gangster.controller");
        Assert.That(controller, Is.Not.Null, "The rebel character controller must be imported.");

        GameObject root = Track(new GameObject("TestRebelCharacterRoot"));
        root.AddComponent<Animator>(); // Mirrors the empty required root Animator in the character prefab.
        GameObject visual = Track(new GameObject("TestRebelCharacterVisual"));
        visual.transform.SetParent(root.transform, false);
        Animator visualAnimator = visual.AddComponent<Animator>();
        visualAnimator.runtimeAnimatorController = controller;
        Assert.That(Array.Exists(visualAnimator.parameters,
            parameter => parameter.name == "velocidade" && parameter.type == AnimatorControllerParameterType.Float), Is.True);
        Component driver = Add(root, "AnimacaoCombatenteRebelde");

        yield return null;

        FieldInfo animatorField = driver.GetType().GetField("animator", Members);
        Assert.That(animatorField.GetValue(driver), Is.SameAs(visualAnimator),
            "The rebel animation driver must skip the empty root Animator and bind to the child model controller.");

        root.transform.position += Vector3.right;
        driver.GetType().GetMethod("Update", Members).Invoke(driver, null);
        Assert.That(visualAnimator.GetFloat("velocidade"), Is.GreaterThan(0.05f),
            "Movement of the unit must drive the controller's locomotion blend parameter.");
    }

    [UnityTest]
    public IEnumerator SubmarineCounterattackRespectsManualAndPassiveModesAndLaunchesInAutomatic()
    {
        GameObject sub = Track(new GameObject("TestCounterSubmarine"));
        Component identity = Add(sub, "IdentidadeUnidade");
        Set(identity, "teamID", 2);
        Component control = Add(sub, "ControleUnidade");

        GameObject tube = new GameObject("TuboMissel_01");
        tube.transform.SetParent(sub.transform, false);
        tube.transform.localPosition = new Vector3(0f, 1f, 0f);

        GameObject subMissileTemplate = CreateMissileTemplate("TestCounterSubMissile", "MisselSubmarino");
        sub.SetActive(false);
        Component submarine = Add(sub, "ControleSubmarino");
        Set(submarine, "locaisDisparo", new Transform[] { tube.transform });
        Set(submarine, "prefabMisselSubmarino", subMissileTemplate);
        sub.SetActive(true);

        yield return null;

        Set(submarine, "misseisDisponiveis", 1);
        MethodInfo counterMethod = submarine.GetType().GetMethod("TentarLancarContraAtaqueIA", Members);
        Assert.That(counterMethod, Is.Not.Null, "TentarLancarContraAtaqueIA precisa existir em ControleSubmarino.");

        SetEnum(submarine, "modoAtual", "Manual");
        object[] args = new object[] { new Vector3(900f, 0f, 300f), null, 4500f, null };
        Assert.That((bool)counterMethod.Invoke(submarine, args), Is.False);
        Assert.That((int)Read(submarine, "misseisDisponiveis"), Is.EqualTo(1));

        SetEnum(submarine, "modoAtual", "Passivo");
        args[3] = null;
        Assert.That((bool)counterMethod.Invoke(submarine, args), Is.False);
        Assert.That((int)Read(submarine, "misseisDisponiveis"), Is.EqualTo(1));

        SetEnum(submarine, "modoAtual", "Automatico");
        SetEnum(submarine, "armamentoSelecionadoHud", "Automatico");
        Invoke(control, "DefinirModoCombate", true);
        args[3] = null;
        bool fired = (bool)counterMethod.Invoke(submarine, args);
        Assert.That(fired, Is.True, "Submarino automático deveria lançar míssil de contra-ataque: " + args[3]);
        Assert.That((int)Read(submarine, "misseisDisponiveis"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator VisibilityServiceNotificarAgressaoCountersWithNavalAndProtectsTeam1()
    {
        SetupWarBetweenTeams(1, 2);

        GameObject playerShip = Track(new GameObject("TestCounterPlayerShip"));
        Component playerId = Add(playerShip, "IdentidadeUnidade");
        Set(playerId, "teamID", 1);
        Add(playerShip, "ControleUnidade");
        Component playerLauncher = Add(playerShip, "LancadorNaval");
        GameObject missileTemplate = CreateMissileTemplate("TestCounterVisibilityMissile", "MisselNaval");
        Set(playerLauncher, "prefabMissel", missileTemplate);
        Set(playerLauncher, "municaoTotal", 2);
        Set(playerLauncher, "municaoMaxima", 2);
        SetEnum(playerLauncher, "modoAtual", "Automatico");

        GameObject aiShip = Track(new GameObject("TestCounterAIShip"));
        Component aiId = Add(aiShip, "IdentidadeUnidade");
        Set(aiId, "teamID", 2);
        Add(aiShip, "ControleUnidade");
        Component aiLauncher = Add(aiShip, "LancadorNaval");
        Set(aiLauncher, "prefabMissel", missileTemplate);
        Set(aiLauncher, "municaoTotal", 2);
        Set(aiLauncher, "municaoMaxima", 2);
        SetEnum(aiLauncher, "modoAtual", "Automatico");

        Component visibility = EnsureVisibilityService();
        yield return null;

        MethodInfo notify = visibility.GetType().GetMethod("NotificarAgressaoParaContraAtaque", Members);
        Assert.That(notify, Is.Not.Null);

        notify.Invoke(visibility, new object[] { 1, 2, null, new Vector3(1500f, 0f, 0f) });
        Assert.That((int)Read(playerLauncher, "municaoTotal"), Is.EqualTo(2), "Time 1 (jogador) nunca deve ser assumido por contra-ataque automático de IA.");

        notify.Invoke(visibility, new object[] { 2, 1, null, new Vector3(1200f, 0f, 0f) });
        Assert.That((int)Read(aiLauncher, "municaoTotal"), Is.EqualTo(1), "Time 2 sob ataque deveria contra-atacar via LancadorNaval.");
    }

    [UnityTest]
    public IEnumerator EmitirMissaoAereaOfensivaConfiguresBomberAndFighterOnCoordinateStrikes()
    {
        GameObject aircraft = Track(new GameObject("TestCounterBomber"));
        Component identity = Add(aircraft, "IdentidadeUnidade");
        Set(identity, "teamID", 2);
        SetEnum(identity, "tipoUnidade", "Aereo");

        aircraft.SetActive(false);
        Component control = Add(aircraft, "ControleUnidade");
        Component flight = Add(aircraft, "ControleAviao");
        Component bomber = Add(aircraft, "AviaoBombardeiro");
        Component airLauncher = Add(aircraft, "LancadorMisselCaca");
        aircraft.SetActive(true);

        yield return null;

        SetEnum(flight, "estadoAtual", "EmMissao");
        Type fuelType = ResolveType("CombustivelUnidade");
        Component fuel = aircraft.GetComponent(fuelType);
        if (fuel != null)
        {
            Set(fuel, "usaCombustivel", false);
        }

        Vector3 targetPoint = new Vector3(950f, 0f, -420f);
        bool accepted = (bool)Invoke(control, "EmitirMissaoAereaOfensiva", targetPoint, null);
        Assert.That(
            accepted,
            Is.True,
            "EmitirMissaoAereaOfensiva deve aceitar ataque aéreo por coordenada em voo. Motivo da recusa: "
            + ReadProperty(control, "UltimoMotivoRecusaOrdem"));
        Assert.That((bool)ReadProperty(control, "ModoCombateAtivo"), Is.True);
        Assert.That((Vector3)Read(flight, "alvoEstrategico"), Is.EqualTo(targetPoint));
        Assert.That((Vector3)Read(flight, "centroDaPatrulha"), Is.EqualTo(targetPoint));
        Assert.That((bool)Read(flight, "alvoPrioritarioIA"), Is.True);
        Assert.That((Vector3)Read(bomber, "alvoAreaSolo"), Is.EqualTo(targetPoint));
        Assert.That(Read(bomber, "modoDeAtaque").ToString(), Is.EqualTo("AtaqueAoSolo"));
        Assert.That((bool)Read(airLauncher, "modoPassivo"), Is.False);
        Assert.That((Vector3)Read(airLauncher, "_ultimoPontoAlvoIA"), Is.EqualTo(targetPoint));
    }

    [UnityTest]
    public IEnumerator IA03EstrategaNacionalDefensiveFallbackSelectsStrategicTargetWithoutPreferenciaMatch()
    {
        SetupWarBetweenTeams(2, 1);

        GameObject enemyShipyard = Track(new GameObject("Estaleiro_Jogador_CounterTest"));
        enemyShipyard.transform.position = new Vector3(450f, 0f, 450f);
        Component enemyId = Add(enemyShipyard, "IdentidadeUnidade");
        Set(enemyId, "teamID", 1);
        SetEnum(enemyId, "tipoUnidade", "Estrutura");

        GameObject brainGo = Track(new GameObject("TestCounterIA03Brain"));
        brainGo.SetActive(false);
        Component brain = Add(brainGo, "Hegemonia.AI.BrainMaster.IA_BrainMaster");
        Set(brain, "TeamId", 2);
        Assert.That((bool)ReadProperty(brain, "UsaCronogramaIA03"), Is.True);
        Component strategist = Add(brainGo, "Hegemonia.AI.IA03.IA03EstrategaNacional");
        brainGo.SetActive(true);

        yield return null;
        yield return null;

        object context = ReadProperty(brain, "Context");
        Assert.That(context, Is.Not.Null);
        object worldState = Read(context, "WorldState");
        Assert.That(worldState, Is.Not.Null);
        Invoke(worldState, "Tick", Time.time, Time.deltaTime);

        Set(strategist, "paisAlvoTeamId", 1);
        SetEnum(strategist, "nivelDeConflito", "GuerraTotal");

        Type prefEnum = ResolveType("Hegemonia.AI.IA03.IA03AlvoPreferencial");
        object airportPref = Enum.Parse(prefEnum, "Aeroporto");
        MethodInfo selectTarget = strategist.GetType().GetMethod("SelecionarAlvoObservado", Members);
        Assert.That(selectTarget, Is.Not.Null);

        object observed = selectTarget.Invoke(strategist, new object[] { airportPref, Vector3.zero });
        Assert.That(observed, Is.Not.Null, "Em guerra formal, SelecionarAlvoObservado não deve retornar null havendo alvos estratégicos do agressor.");
        Transform chosenTransform = Read(observed, "Transform") as Transform;
        Assert.That(chosenTransform, Is.EqualTo(enemyShipyard.transform));

        ScriptableObject defenseMission = CreateMissionAsset(
            "Defesa_Teste",
            "DefesaDeObjetivo",
            "Tensao",
            "GuerraTotal",
            "Aeroporto");
        object missionsList = Read(strategist, "missoesEstrategicas");
        missionsList.GetType().GetMethod("Add")?.Invoke(missionsList, new object[] { defenseMission });

        MethodInfo selectDefense = strategist.GetType().GetMethod("SelecionarMissaoDeDefesa", Members);
        Assert.That(selectDefense, Is.Not.Null);
        object fallbackDefense = selectDefense.Invoke(strategist, new object[] { "Corveta_Patrulha", true });
        Assert.That(fallbackDefense, Is.EqualTo(defenseMission), "Agressão contra unidade móvel deve permitir fallback para missão de defesa geral.");
    }

    private ScriptableObject CreateMissionAsset(
        string name,
        string missionType,
        string minLevel,
        string maxLevel,
        string preferredTarget)
    {
        Type missionTypeRef = ResolveType("Hegemonia.AI.IA03.MissaoEstrategicaSO");
        ScriptableObject asset = ScriptableObject.CreateInstance(missionTypeRef);
        asset.name = name;
        createdAssets.Add(asset);

        Set(asset, "nomeMissao", name);
        SetEnum(asset, "tipoMissao", missionType);
        SetEnum(asset, "nivelMenosGrave", minLevel);
        SetEnum(asset, "nivelMaisGrave", maxLevel);
        SetEnum(asset, "alvo", preferredTarget);
        return asset;
    }

    private GameObject CreateMissileTemplate(string objectName, string componentTypeName)
    {
        GameObject template = Track(new GameObject(objectName));
        template.SetActive(false);
        Add(template, componentTypeName);
        return template;
    }

    private Component EnsureVisibilityService()
    {
        Type visibilityType = ResolveType("Hegemonia.RTS.RTSVisibilityService");
        Component existing = visibilityType.GetProperty("Instancia", Members)?.GetValue(null) as Component;
        if (existing != null)
        {
            return existing;
        }

        GameObject go = Track(new GameObject("TestCounterVisibilityService"));
        return go.AddComponent(visibilityType);
    }

    private void SetupWarBetweenTeams(int teamA, int teamB)
    {
        Type govType = ResolveType("SistemaGovernoMundial");
        object preexisting = govType.GetProperty("Instancia", Members)?.GetValue(null);
        Component gov = preexisting as Component ?? (Component)InvokeStatic(govType, "GarantirInstancia");
        Assert.That(gov, Is.Not.Null);

        if (preexisting == null)
        {
            Track(gov.gameObject);
        }

        object rel = Invoke(gov, "ObterRelacao", teamA, teamB);
        if (rel != null)
        {
            bool prevWar = (bool)Read(rel, "guerraDeclarada");
            bool prevCease = (bool)Read(rel, "cessarFogoAtivo");
            int prevVal = (int)Read(rel, "valor");
            object prevPostA = Read(rel, "posturaAParaB");
            object prevPostB = Read(rel, "posturaBParaA");
            cleanupActions.Add(() =>
            {
                Set(rel, "guerraDeclarada", prevWar);
                Set(rel, "cessarFogoAtivo", prevCease);
                Set(rel, "valor", prevVal);
                Set(rel, "posturaAParaB", prevPostA);
                Set(rel, "posturaBParaA", prevPostB);
            });

            Invoke(gov, "DeclararGuerra", teamA, teamB);
            Set(rel, "guerraDeclarada", true);
            Set(rel, "cessarFogoAtivo", false);
            SetEnum(rel, "posturaAParaB", "Inimigo");
            SetEnum(rel, "posturaBParaA", "Inimigo");
        }
    }

    private void DestroySpawnedNamedComponents(string typeName, string namePrefix)
    {
        Type targetType = FindTypeOptional(typeName);
        if (targetType == null)
        {
            return;
        }

        UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(targetType, FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            Component comp = found[i] as Component;
            if (comp != null && comp.gameObject != null && comp.gameObject.name.StartsWith(namePrefix, StringComparison.Ordinal))
            {
                UnityEngine.Object.Destroy(comp.gameObject);
            }
        }
    }

    private GameObject Track(GameObject go)
    {
        createdObjects.Add(go);
        return go;
    }

    private static Component Add(GameObject target, string typeName)
    {
        return target.AddComponent(ResolveType(typeName));
    }

    private static Type FindTypeOptional(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(type => type != null);
    }

    private static Type ResolveType(string name)
    {
        Type found = FindTypeOptional(name);
        Assert.That(found, Is.Not.Null, "Tipo não encontrado para o teste: " + name);
        return found;
    }

    private static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + target.GetType().Name + "." + name);
        field.SetValue(target, value);
    }

    private static void SetEnum(object target, string fieldName, string enumValueName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, Members);
        Assert.That(field, Is.Not.Null, "Campo enum não encontrado: " + target.GetType().Name + "." + fieldName);
        field.SetValue(target, Enum.Parse(field.FieldType, enumValueName));
    }

    private static object Read(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + target.GetType().Name + "." + name);
        return field.GetValue(target);
    }

    private static object ReadProperty(object target, string name)
    {
        PropertyInfo prop = target.GetType().GetProperty(name, Members);
        Assert.That(prop, Is.Not.Null, "Propriedade não encontrada: " + target.GetType().Name + "." + name);
        return prop.GetValue(target, null);
    }

    private static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo[] methods = target.GetType().GetMethods(Members)
            .Where(m => m.Name == methodName && m.GetParameters().Length == args.Length)
            .ToArray();
        Assert.That(methods.Length, Is.GreaterThan(0), "Método não encontrado: " + target.GetType().Name + "." + methodName);
        return methods[0].Invoke(target, args);
    }

    private static object InvokeStatic(Type type, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethods(Members)
            .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length);
        Assert.That(method, Is.Not.Null, "Método estático não encontrado: " + type.Name + "." + methodName);
        return method.Invoke(null, args);
    }
}
