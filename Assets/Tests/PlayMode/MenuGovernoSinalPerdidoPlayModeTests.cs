using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public sealed class MenuGovernoSinalPerdidoPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject governmentObject;
    private GameObject missionObject;
    private GameObject menuObject;
    private GameObject airportObject;
    private GameObject destinationRunwayObject;
    private GameObject rebelControllerObject;
    private readonly List<GameObject> extraObjects = new List<GameObject>();
    private float previousTimeScale = 1f;
    private Component controller;
    private MethodInfo openMenu;
    private UIDocument menuDocument;
    private ScrollView menuNavigation;
    private ScrollView menuActions;

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator MenuRealRecusaOperacaoSemAeroportoComercial()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", Members);
        Assert.That(governmentInstance, Is.Not.Null);
        if (governmentInstance.GetValue(null) == null)
        {
            governmentObject = new GameObject("Teste Menu Governo - Sistema Nacional");
            governmentObject.AddComponent(governmentType);
        }
        Assert.That(governmentInstance.GetValue(null), Is.Not.Null,
            "A ação do menu precisa consultar a instância real do Governo.");

        Type missionType = ResolveType("MissaoOperacaoSinalPerdido");
        PropertyInfo missionState = missionType.GetProperty("Estado", Members);
        missionObject = new GameObject("Teste Menu Governo - Operação Sinal Perdido");
        Component mission = missionObject.AddComponent(missionType);
        Assert.That(missionState.GetValue(mission).ToString(), Is.EqualTo("Disponivel"));

        VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("MenuGoverno/MenuGovernoNovo");
        PanelSettings panelSettings = Resources.Load<PanelSettings>("PanelSettings");
        Assert.That(uxml, Is.Not.Null, "O UXML real do Governo deve estar disponível em Resources.");
        Assert.That(panelSettings, Is.Not.Null, "O PanelSettings real deve estar disponível.");

        menuObject = new GameObject("menu_governo");
        UIDocument document = menuObject.AddComponent<UIDocument>();
        document.visualTreeAsset = uxml;
        document.panelSettings = panelSettings;
        document.sortingOrder = 1000;
        Type controllerType = ResolveType("MenuGovernoNovoController");
        controller = menuObject.AddComponent(controllerType);
        PropertyInfo ready = controllerType.GetProperty("Pronto", Members);
        openMenu = controllerType.GetMethod("Abrir", Members, null, new[] { typeof(bool) }, null);
        FieldInfo navigationField = controllerType.GetField("navegacao", Members);
        FieldInfo actionsField = controllerType.GetField("acoes", Members);
        Assert.That(ready, Is.Not.Null);
        Assert.That(openMenu, Is.Not.Null);
        Assert.That(navigationField, Is.Not.Null);
        Assert.That(actionsField, Is.Not.Null);

        for (int frame = 0; frame < 120 && !(bool)ready.GetValue(controller); frame++)
            yield return null;
        Assert.That((bool)ready.GetValue(controller), Is.True,
            "O documento UI Toolkit deve vincular os painéis do menu real.");

        openMenu.Invoke(controller, new object[] { true });
        ScrollView navigation = navigationField.GetValue(controller) as ScrollView;
        Assert.That(navigation, Is.Not.Null);
        Button researchAndDevelopment = navigation.Query<Button>(className: "gov-nav-button")
            .ToList().Find(button => button.name == "nav-pesquisa e desenvolvimento");
        Assert.That(researchAndDevelopment, Is.Not.Null,
            "Pesquisa e Desenvolvimento deve estar visível na navegação real do Governo.");
        Click(researchAndDevelopment);

        ScrollView actions = actionsField.GetValue(controller) as ScrollView;
        Assert.That(actions, Is.Not.Null);
        Button finance = actions.Query<Button>().ToList()
            .Find(button => button.text == "FINANCIAR EXPEDIÇÃO CIENTÍFICA");
        Assert.That(finance, Is.Not.Null,
            "A Operação Sinal Perdido disponível deve oferecer o botão de financiamento.");
        Click(finance);
        yield return null;

        Label footer = document.rootVisualElement.Q<Label>("footer-message");
        Assert.That(footer, Is.Not.Null);
        Assert.That(footer.text, Does.Contain("aeroporto comercial operacional do jogador").IgnoreCase,
            "O callback do botão deve impedir a missão se o jogador não tiver aeroporto comercial.");
        Assert.That(missionState.GetValue(mission).ToString(), Is.EqualTo("Disponivel"),
            "O gate do aeroporto precisa preservar o estado da missão quando recusa o início.");
    }

    [UnityTest]
    public IEnumerator C700MantemDecolagemAceitaAntesDoStart()
    {
        GameObject runwayObject = new GameObject("Pista logística de teste");
        Type runwayType = ResolveType("MiniPistaLogistica");
        Component runway = runwayObject.AddComponent(runwayType);
        runwayType.GetField("teamId", Members).SetValue(runway, 1);
        GameObject aircraftObject = new GameObject("C700 da Operação Sinal Perdido");
        Type aircraftType = ResolveType("C700TransporteAereo");
        Component aircraft = aircraftObject.AddComponent(aircraftType);
        aircraftType.GetField("permitirPousoTerrestreSeco", Members).SetValue(aircraft, true);
        Type fuelType = ResolveType("CombustivelUnidade");
        Component fuel = aircraftObject.GetComponent(fuelType);
        if (fuel == null) fuel = aircraftObject.AddComponent(fuelType);
        fuelType.GetField("usaCombustivel", Members).SetValue(fuel, false);

        MethodInfo receiveOrder = aircraftType.GetMethod("TentarReceberOrdemMover", Members);
        Assert.That(receiveOrder, Is.Not.Null);
        object[] args = { runway.transform.position, null };
        Assert.That(receiveOrder.Invoke(aircraft, args), Is.True, args[1] as string);
        FieldInfo aircraftState = aircraftType.GetField("estadoAtual", Members);
        Assert.That(aircraftState.GetValue(aircraft).ToString(), Is.EqualTo("Decolando"),
            "A ordem do menu precisa colocar o transporte em decolagem pelo controlador que o prefab usa.");

        yield return null;

        Assert.That(aircraftState.GetValue(aircraft).ToString(), Is.EqualTo("Decolando"),
            "Start não deve apagar a ordem recebida no frame em que o C700 foi criado.");
        Assert.That(aircraft.transform.position.y, Is.GreaterThan(0f),
            "O transporte precisa começar a deixar o ponto de estacionamento do aeroporto.");

        UnityEngine.Object.Destroy(aircraftObject);
        UnityEngine.Object.Destroy(runwayObject);
        yield return null;
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator MenuFinanciaExpedicaoComAeroportoComercialEAAtivaFacao()
    {
        Type governmentType = ResolveType("SistemaGovernoMundial");
        PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", Members);
        if (governmentInstance.GetValue(null) == null)
        {
            governmentObject = new GameObject("Teste Menu Governo - Sistema Nacional com saldo");
            governmentObject.AddComponent(governmentType);
        }
        Component government = governmentInstance.GetValue(null) as Component;
        Assert.That(government, Is.Not.Null);
        int playerTeam = (int)governmentType.GetField("teamJogador", Members).GetValue(government);
        MethodInfo setBalance = governmentType.GetMethod("DefinirSaldoNacional", Members);
        Assert.That((bool)setBalance.Invoke(government, new object[] { playerTeam, 2000000L }), Is.True);
        MethodInfo getBalance = governmentType.GetMethod("ObterSaldoDisponivelParaOperacao", Members);
        long saldoAntes = (long)getBalance.Invoke(government, new object[] { playerTeam });
        Assert.That(saldoAntes, Is.GreaterThanOrEqualTo(250000L));

        airportObject = CriarAeroportoComercialDeTeste(ResolveType("GerenciadorAeroportoComercial"));
        CriarUsinaDeTeste(playerTeam);
        Component airport = airportObject.GetComponent(ResolveType("GerenciadorAeroportoComercial"));
        Assert.That((bool)airport.GetType().GetMethod("PossuiRotaPousoValida", Members).Invoke(airport, null), Is.True);

        destinationRunwayObject = new GameObject("Mini pista - destino Sinal Perdido");
        destinationRunwayObject.transform.position = Vector3.zero;
        destinationRunwayObject.AddComponent(ResolveType("MiniPistaLogistica"));

        rebelControllerObject = new GameObject("Controlador da Organização Rebelde - teste");
        Component rebelController = rebelControllerObject.AddComponent(ResolveType("RebeldeControlador"));

        Type constructionType = ResolveType("DadosConstrucao");
        UnityEngine.Object construction = Resources.Load("Construcoes/C400", constructionType);
        Assert.That(construction, Is.Not.Null, "O catálogo precisa fornecer o C-400 científico.");
        MethodInfo getPrefab = constructionType.GetMethod("TryGetPrefabBasico", Members);
        object[] prefabArguments = { null };
        Assert.That((bool)getPrefab.Invoke(construction, prefabArguments), Is.True);
        GameObject aircraftPrefab = prefabArguments[0] as GameObject;
        Assert.That(aircraftPrefab, Is.Not.Null);

        Type missionType = ResolveType("MissaoOperacaoSinalPerdido");
        PropertyInfo missionState = missionType.GetProperty("Estado", Members);
        missionObject = new GameObject("Teste positivo - Operação Sinal Perdido");
        Component mission = missionObject.AddComponent(missionType);
        SetField(mission, "prefabAeronaveCientifica", aircraftPrefab);
        SetField(mission, "aeroportoCivil", airport);
        SetField(mission, "pontoCriacaoAviao", airportObject.transform);
        SetField(mission, "destinoDaExpedicao", destinationRunwayObject.transform);
        SetField(mission, "controladorRebelde", rebelController);
        SetField(mission, "atrasoAutorizacaoDecolagem", 0f);
        SetField(mission, "tempoAteDesaparecimento", 10f);

        yield return PrepararMenuGoverno();
        Button finance = menuActions.Query<Button>().ToList()
            .Find(button => button.text == "FINANCIAR EXPEDIÇÃO CIENTÍFICA");
        Assert.That(finance, Is.Not.Null);
        Click(finance);
        yield return null;

        Assert.That(missionState.GetValue(mission).ToString(), Is.EqualTo("ExpedicaoEmAndamento"),
            "O botão do menu precisa aceitar a expedição quando há saldo, C400 e aeroporto comercial." );
        long saldoDepois = (long)getBalance.Invoke(government, new object[] { playerTeam });
        Assert.That(saldoDepois, Is.EqualTo(saldoAntes - 250000L));

        FieldInfo aircraftField = missionType.GetField("aeronaveDaExpedicao", Members);
        Component aircraft = aircraftField.GetValue(mission) as Component;
        Assert.That(aircraft, Is.Not.Null);
        Type c700Type = ResolveType("C700TransporteAereo");
        Component c700 = aircraft.GetComponent(c700Type);
        Assert.That(c700, Is.Not.Null);
        Assert.That(c700Type.GetField("estadoAtual", Members).GetValue(c700).ToString(), Is.EqualTo("Decolando"));
        IList airportTransports = airport.GetType().GetField("transportesC700NoPatio", Members).GetValue(airport) as IList;
        Assert.That(airportTransports, Is.Not.Null);
        Assert.That(airportTransports.Contains(c700), Is.True,
            "O C-400 da missão precisa estar registrado no pátio do aeroporto comercial de origem.");

        Component fuel = aircraft.GetComponent(ResolveType("CombustivelUnidade"));
        float capacity = (float)fuel.GetType().GetProperty("Capacidade", Members).GetValue(fuel);
        Assert.That(capacity, Is.GreaterThan(345f),
            "O C-400 dedicado deve receber autonomia para ida e retorno até o destino distante.");
        Vector3 positionAtTakeoff = aircraft.transform.position;
        yield return null;
        Assert.That((aircraft.transform.position - positionAtTakeoff).sqrMagnitude, Is.GreaterThan(0.001f),
            "O C-400 precisa começar a sair do aeroporto após o clique no menu.");

        // The Play Mode runner may advance many rendered frames with very little
        // scaled game time. Expire the mission clock deterministically, then let
        // the production coroutine perform the real disappearance transition.
        SetField(mission, "tempoDecorrido", 10f);
        yield return null;
        Assert.That(missionState.GetValue(mission).ToString(), Is.EqualTo("AeronaveDesaparecida"));
        Assert.That((bool)rebelController.GetType().GetProperty("Ativo", Members).GetValue(rebelController), Is.True,
            "O desaparecimento precisa ativar o controlador da facção rebelde.");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator GangsterExecutaLocomocaoEDisparoNoPlayMode()
    {
        const string prefabPath = "Assets/Library/Prefabs/IA rebelde/1920s_Gangster_Rebelde.prefab";
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, "O prefab do 1920s Gangster precisa estar importado no projeto.");

        GameObject gangster = UnityEngine.Object.Instantiate(prefab);
        GameObject alvo = new GameObject("Alvo temporário do teste de animação rebelde");
        try
        {
            UnityEngine.AI.NavMeshAgent agent = gangster.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
            if (agent != null) agent.enabled = false;

            Animator[] animadores = gangster.GetComponentsInChildren<Animator>(true);
            Animator animator = null;
            for (int i = 0; i < animadores.Length; i++)
                if (animadores[i] != null && animadores[i].runtimeAnimatorController != null)
                { animator = animadores[i]; break; }

            Assert.That(animator, Is.Not.Null, "O prefab deve encontrar um Animator com controlador atribuído.");
            Assert.That(animator.parameters.Any(p => p.name == "velocidade" && p.type == AnimatorControllerParameterType.Float), Is.True);
            Assert.That(animator.parameters.Any(p => p.name == "atirando" && p.type == AnimatorControllerParameterType.Bool), Is.True);

            Component driver = gangster.GetComponentInChildren(ResolveType("AnimacaoCombatenteRebelde"), true);
            Component shooter = gangster.GetComponentInChildren(ResolveType("SistemaDeTiro"), true);
            Assert.That(driver, Is.Not.Null);
            Assert.That(shooter, Is.Not.Null);
            Behaviour shooterBehaviour = shooter as Behaviour;
            Assert.That(shooterBehaviour, Is.Not.Null);
            animator.Play("Locomocao", 0, 0f);
            animator.Update(0.05f);

            gangster.transform.position += Vector3.forward * 0.5f;
            driver.GetType().GetMethod("Update", Members).Invoke(driver, null);
            Assert.That(animator.GetFloat(Animator.StringToHash("velocidade")), Is.GreaterThan(0f),
                "O driver deve converter o deslocamento do combatente em velocidade de animação.");

            shooterBehaviour.enabled = false;
            SetField(shooter, "modoPassivo", false);
            SetField(shooter, "alvoAtual", alvo.transform);
            driver.GetType().GetMethod("Update", Members).Invoke(driver, null);
            int fireHash = Animator.StringToHash("atirando");
            Assert.That(animator.GetBool(fireHash), Is.True,
                "O driver precisa propagar o alvo ativo do sistema de tiro ao parâmetro atirando.");
            animator.Update(0.5f);
            AnimatorStateInfo estadoAtual = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo proximoEstado = animator.GetNextAnimatorStateInfo(0);
            Assert.That(estadoAtual.IsName("Disparo")
                || (animator.IsInTransition(0) && proximoEstado.IsName("Disparo")), Is.True,
                "O parâmetro atirando precisa levar o Animator ao estado Disparo. Estado hash atual: "
                + estadoAtual.shortNameHash + "; próximo: " + proximoEstado.shortNameHash + ".");

            Component damage = gangster.GetComponentInChildren(ResolveType("SistemaDeDanos"), true);
            Assert.That(damage, Is.Not.Null, "O prefab do 1920s Gangster precisa possuir SistemaDeDanos.");
            MethodInfo receberDano = damage.GetType().GetMethod("ReceberDano", Members);
            Assert.That(receberDano, Is.Not.Null);
            float vidaAntes = (float)damage.GetType().GetField("vidaAtual", Members).GetValue(damage);
            receberDano.Invoke(damage, new object[] { 25f, alvo });
            float vidaDepois = (float)damage.GetType().GetField("vidaAtual", Members).GetValue(damage);
            Assert.That(vidaDepois, Is.LessThan(vidaAntes),
                "O Gangster precisa sofrer dano corretamente pelo SistemaDeDanos.");
            receberDano.Invoke(damage, new object[] { 5000f, alvo });
            float vidaFinal = (float)damage.GetType().GetField("vidaAtual", Members).GetValue(damage);
            Assert.That(vidaFinal, Is.LessThanOrEqualTo(0f),
                "Dano letal precisa zerar a vida do Gangster sem exceções.");
        }
        finally
        {
            UnityEngine.Object.Destroy(gangster);
            UnityEngine.Object.Destroy(alvo);
        }

        yield return null;
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator C400ExpedicaoAguardaNaBaiaSaiPeloTaxiEPistaComercial()
    {
        List<string> consoleErrors = new List<string>();
        Application.LogCallback capture = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert
                || (!string.IsNullOrEmpty(condition) && condition.IndexOf("Failed to create agent", StringComparison.OrdinalIgnoreCase) >= 0))
                consoleErrors.Add($"{type}: {condition}");
        };
        Application.logMessageReceived += capture;
        previousTimeScale = Time.timeScale;
        try
        {
            Type governmentType = ResolveType("SistemaGovernoMundial");
            PropertyInfo governmentInstance = governmentType.GetProperty("Instancia", Members);
            if (governmentInstance.GetValue(null) == null)
            {
                governmentObject = new GameObject("Teste Baia Pista - Governo");
                governmentObject.AddComponent(governmentType);
            }
            yield return null;
            object government = governmentInstance.GetValue(null);
            Assert.That(government, Is.Not.Null);
            int playerTeam = (int)governmentType.GetField("teamJogador", Members).GetValue(government);
            MethodInfo setBalance = governmentType.GetMethod("DefinirSaldoNacional", Members);
            Assert.That((bool)setBalance.Invoke(government, new object[] { playerTeam, 2000000L }), Is.True);

            GameObject commercialAirportPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Library/Prefabs/Aeroporto/Aeroporto comercial/Aeroporto comercial.prefab");
            Assert.That(commercialAirportPrefab, Is.Not.Null, "Prefab real Aeroporto comercial precisa existir.");
            airportObject = UnityEngine.Object.Instantiate(commercialAirportPrefab, new Vector3(2000f, 0f, 2000f), Quaternion.identity);
            Type commercialAirportType = ResolveType("GerenciadorAeroportoComercial");
            Component airport = airportObject.GetComponent(commercialAirportType);
            Assert.That(airport, Is.Not.Null);
            Component airportIdentity = airportObject.GetComponent(ResolveType("IdentidadeUnidade"));
            if (airportIdentity == null) airportIdentity = airportObject.AddComponent(ResolveType("IdentidadeUnidade"));
            SetField(airportIdentity, "teamID", playerTeam);
            SetField(airport, "semEnergia", false);
            CriarUsinaDeTeste(playerTeam);

            destinationRunwayObject = new GameObject("MiniPista Destino - Teste Baia");
            destinationRunwayObject.transform.position = new Vector3(8000f, 0f, 8000f);
            destinationRunwayObject.AddComponent(ResolveType("MiniPistaLogistica"));

            rebelControllerObject = new GameObject("Controlador Rebelde - Teste Baia");
            Component rebelController = rebelControllerObject.AddComponent(ResolveType("RebeldeControlador"));

            GameObject c400Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Library/Prefabs/Aeroporto/C400/C400.prefab");
            Assert.That(c400Prefab, Is.Not.Null, "Prefab real C400 precisa existir.");

            Type missionType = ResolveType("MissaoOperacaoSinalPerdido");
            missionObject = new GameObject("Missao Sinal Perdido - Teste Baia");
            Component mission = missionObject.AddComponent(missionType);
            SetField(mission, "prefabAeronaveCientifica", c400Prefab);
            SetField(mission, "aeroportoCivil", airport);
            SetField(mission, "pontoCriacaoAviao", airportObject.transform);
            SetField(mission, "destinoDaExpedicao", destinationRunwayObject.transform);
            SetField(mission, "controladorRebelde", rebelController);
            SetField(mission, "tempoAteDesaparecimento", 120f);

            float delayConfigured = (float)missionType.GetProperty("AtrasoAutorizacaoDecolagem", Members).GetValue(mission);
            Assert.That(delayConfigured, Is.EqualTo(5f).Within(0.01f),
                "O atraso padrão de autorização de táxi na baia deve iniciar em ~5s.");

            yield return PrepararMenuGoverno();
            Button finance = menuActions.Query<Button>().ToList()
                .Find(button => button.text == "FINANCIAR EXPEDIÇÃO CIENTÍFICA");
            Assert.That(finance, Is.Not.Null);
            Click(finance);
            yield return null;

            Assert.That(missionType.GetProperty("Estado", Members).GetValue(mission).ToString(), Is.EqualTo("ExpedicaoEmAndamento"));
            Assert.That((bool)missionType.GetProperty("DecolagemIniciada", Members).GetValue(mission), Is.False,
                "Logo após o financiamento, o C-400 deve aguardar a janela de ~5s na baia antes de taxiar.");
            Assert.That((float)missionType.GetProperty("TempoRestanteAteDecolagem", Members).GetValue(mission), Is.GreaterThan(0f));

            Component aircraft = missionType.GetField("aeronaveDaExpedicao", Members).GetValue(mission) as Component;
            Assert.That(aircraft, Is.Not.Null);
            IList patioBays = airport.GetType().GetField("waypointsPatio", Members).GetValue(airport) as IList;
            Assert.That(patioBays, Is.Not.Null.And.Not.Empty);
            bool parkedAtCommercialBay = patioBays.Cast<Transform>()
                .Any(b => b != null && Vector3.Distance(aircraft.transform.position, new Vector3(b.position.x, aircraft.transform.position.y, b.position.z)) < 5f);
            Assert.That(parkedAtCommercialBay, Is.True,
                "O C-400 científico precisa iniciar estacionado em uma baia livre do aeroporto comercial.");

            Type c700Type = ResolveType("C700TransporteAereo");
            Component c700 = aircraft.GetComponent(c700Type);
            Assert.That(c700, Is.Not.Null);
            FieldInfo c700StateField = c700Type.GetField("estadoAtual", Members);

            bool sawTaxi = false;
            bool sawTakeoff = false;
            bool sawAirborne = false;
            Time.timeScale = 5f;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline && !sawAirborne)
            {
                string c700State = c700StateField.GetValue(c700).ToString();
                if (c700State == "Taxiando") sawTaxi = true;
                if (c700State == "Decolando") sawTakeoff = true;
                if (c700State == "EmVoo") sawAirborne = true;
                yield return null;
            }

            Assert.That((bool)missionType.GetProperty("DecolagemIniciada", Members).GetValue(mission), Is.True,
                "Após ~5s de jogo na baia, a missão deve autorizar o C-400 a taxiar para a pista.");
            Assert.That(sawTaxi, Is.True, "O C-400 precisa sair da baia e percorrer a taxiway do aeroporto comercial.");
            Assert.That(sawTakeoff, Is.True, "O C-400 precisa alinhar e acelerar na pista comercial.");
            Assert.That(sawAirborne, Is.True, "Após a pista, o C-400 precisa decolar em voo rumo à área operacional.");
            Assert.That(consoleErrors, Is.Empty, "O ciclo baia->táxi->pista->voo não pode gerar erros no console.");
        }
        finally
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            Application.logMessageReceived -= capture;
        }
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator GlobalMapRTSContemOrganizacaoRebeldeLimpaEAtivaUnidadesSemErros()
    {
        List<string> consoleErrors = new List<string>();
        Application.LogCallback capture = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert
                || (!string.IsNullOrEmpty(condition) && condition.IndexOf("Failed to create agent", StringComparison.OrdinalIgnoreCase) >= 0))
                consoleErrors.Add($"{type}: {condition}");
        };
        Application.logMessageReceived += capture;
        Scene loadedMap = default;
        try
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("Assets/Scenes/GlobalMapRTS.unity", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "A cena GlobalMapRTS precisa estar habilitada em Build Settings.");
            while (!load.isDone)
                yield return null;

            loadedMap = SceneManager.GetSceneByPath("Assets/Scenes/GlobalMapRTS.unity");
            Assert.That(loadedMap.IsValid() && loadedMap.isLoaded, Is.True);
            SceneManager.SetActiveScene(loadedMap);

            yield return null;
            yield return null;

            Assert.That(GameObject.Find("AeroportoComercial_TesteTemporario"), Is.Null,
                "Nenhum AeroportoComercial_TesteTemporario deve permanecer na cena GlobalMapRTS.");
            Assert.That(GameObject.Find("SistemaGovernoMundial_TesteSinalPerdido"), Is.Null,
                "Nenhum SistemaGovernoMundial_TesteSinalPerdido deve permanecer na cena GlobalMapRTS.");

            Type missionType = ResolveType("MissaoOperacaoSinalPerdido");
            Component mission = UnityEngine.Object.FindFirstObjectByType(missionType) as Component;
            Assert.That(mission, Is.Not.Null, "Operacao Sinal Perdido precisa estar presente na cena GlobalMapRTS.");
            Assert.That((float)missionType.GetProperty("AtrasoAutorizacaoDecolagem", Members).GetValue(mission),
                Is.EqualTo(5f).Within(0.01f));
            GameObject c400Ref = missionType.GetField("prefabAeronaveCientifica", Members).GetValue(mission) as GameObject;
            Assert.That(c400Ref != null && c400Ref.name.Contains("C400"), Is.True,
                "MissaoOperacaoSinalPerdido precisa referenciar o prefab C400.");
            Assert.That(missionType.GetField("aeroportoCivil", Members).GetValue(mission), Is.Null,
                "A cena inicial não deve vincular aeroporto comercial temporário; usa o construído pelo jogador.");

            Component rebel = missionType.GetField("controladorRebelde", Members).GetValue(mission) as Component;
            Assert.That(rebel, Is.Not.Null, "Controlador Rebelde precisa estar conectado à missão.");
            Type rebelType = rebel.GetType();
            IList areas = rebelType.GetField("areasOperacionais", Members).GetValue(rebel) as IList;
            Assert.That(areas, Is.Not.Null.And.Count.GreaterThanOrEqualTo(3),
                "A IA Rebelde precisa ter as 3 áreas operacionais (Aérea, Terrestre e Naval).");
            Assert.That(rebelType.GetField("prefabCacaF5", Members).GetValue(rebel), Is.Not.Null);
            Assert.That(rebelType.GetField("prefabHelicopteroRebelde", Members).GetValue(rebel), Is.Not.Null);
            Assert.That(rebelType.GetField("prefabNavioArmado", Members).GetValue(rebel), Is.Not.Null);

            rebelType.GetMethod("Ativar", Members).Invoke(rebel, null);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(1.1f);

            GameObject basePrincipal = rebelType.GetField("basePrincipal", Members).GetValue(rebel) as GameObject;
            Assert.That(basePrincipal, Is.Not.Null);
            Component aeroportoMilitar = basePrincipal.GetComponent(ResolveType("GerenciadorAeroporto"));
            Assert.That(aeroportoMilitar, Is.Not.Null);
            Assert.That((bool)aeroportoMilitar.GetType().GetField("semEnergia", Members).GetValue(aeroportoMilitar), Is.False,
                "O Aeroporto Militar da facção rebelde não deve ser desligado por falta de energia estatal.");

            Assert.That((bool)rebelType.GetProperty("Ativo", Members).GetValue(rebel), Is.True);
            int rebelTeamId = (int)rebelType.GetProperty("IdentificacaoFacao", Members).GetValue(rebel);
            Assert.That(rebelTeamId, Is.GreaterThanOrEqualTo(4),
                "A IA Rebelde precisa possuir ID próprio distinto do jogador e das outras IAs.");

            IList aeronaves = rebelType.GetField("aeronavesRebeldes", Members).GetValue(rebel) as IList;
            IList helicopteros = rebelType.GetField("helicopterosRebeldes", Members).GetValue(rebel) as IList;
            IList navios = rebelType.GetField("naviosRebeldes", Members).GetValue(rebel) as IList;
            IList soldados = rebelType.GetField("soldadosReforcados", Members).GetValue(rebel) as IList;

            Assert.That(aeronaves, Is.Not.Null.And.Count.GreaterThan(0), "O F-5 rebelde precisa ser instanciado na ativação.");
            Assert.That(helicopteros, Is.Not.Null.And.Count.GreaterThan(0), "O helicóptero Kamov rebelde precisa ser instanciado na ativação.");
            Assert.That(navios, Is.Not.Null.And.Count.GreaterThan(0), "Os navios armados rebeldes precisam ser instanciados na área naval.");
            Assert.That(soldados, Is.Not.Null.And.Count.GreaterThan(0), "Os bandidos 1920s Gangster precisam surgir na área terrestre.");

            Type identityType = ResolveType("IdentidadeUnidade");
            foreach (object[] grupo in new[]
            {
                new object[] { aeronaves[0] as Component, "F-5" },
                new object[] { helicopteros[0] as Component, "Helicóptero" },
                new object[] { navios[0] as Component, "Navio" }
            })
            {
                Component comp = grupo[0] as Component;
                Assert.That(comp, Is.Not.Null);
                Component id = comp.GetComponent(identityType);
                Assert.That((int)identityType.GetField("teamID", Members).GetValue(id), Is.EqualTo(rebelTeamId),
                    grupo[1] + " rebelde precisa usar o teamID exclusivo da facção rebelde.");
            }
            GameObject primeiroGangster = soldados[0] as GameObject;
            Assert.That(primeiroGangster, Is.Not.Null);
            Component gangsterId = primeiroGangster.GetComponent(identityType);
            Assert.That((int)identityType.GetField("teamID", Members).GetValue(gangsterId), Is.EqualTo(rebelTeamId));
            Assert.That(primeiroGangster.GetComponent(ResolveType("SistemaDeDanos")), Is.Not.Null);

            Assert.That(consoleErrors, Is.Empty,
                "Carregar GlobalMapRTS e ativar toda a IA Rebelde não deve produzir erros no console: " + string.Join(" | ", consoleErrors));
        }
        finally
        {
            Application.logMessageReceived -= capture;
            Scene fallback = SceneManager.CreateScene("SinalPerdidoCleanup_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(fallback);
            if (loadedMap.IsValid() && loadedMap.isLoaded)
                SceneManager.UnloadSceneAsync(loadedMap);
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        previousTimeScale = 1f;
        if (controller != null && openMenu != null)
            openMenu.Invoke(controller, new object[] { false });
        if (menuObject != null) UnityEngine.Object.Destroy(menuObject);
        if (missionObject != null) UnityEngine.Object.Destroy(missionObject);
        if (airportObject != null) UnityEngine.Object.Destroy(airportObject);
        if (destinationRunwayObject != null) UnityEngine.Object.Destroy(destinationRunwayObject);
        if (rebelControllerObject != null) UnityEngine.Object.Destroy(rebelControllerObject);
        if (governmentObject != null) UnityEngine.Object.Destroy(governmentObject);
        for (int i = 0; i < extraObjects.Count; i++)
            if (extraObjects[i] != null) UnityEngine.Object.Destroy(extraObjects[i]);
        extraObjects.Clear();
        yield return null;
    }

    private IEnumerator PrepararMenuGoverno()
    {
        VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("MenuGoverno/MenuGovernoNovo");
        PanelSettings panelSettings = Resources.Load<PanelSettings>("PanelSettings");
        Assert.That(uxml, Is.Not.Null);
        Assert.That(panelSettings, Is.Not.Null);

        menuObject = new GameObject("menu_governo");
        menuDocument = menuObject.AddComponent<UIDocument>();
        menuDocument.visualTreeAsset = uxml;
        menuDocument.panelSettings = panelSettings;
        menuDocument.sortingOrder = 1000;
        Type controllerType = ResolveType("MenuGovernoNovoController");
        controller = menuObject.AddComponent(controllerType);
        PropertyInfo ready = controllerType.GetProperty("Pronto", Members);
        openMenu = controllerType.GetMethod("Abrir", Members, null, new[] { typeof(bool) }, null);
        FieldInfo navigationField = controllerType.GetField("navegacao", Members);
        FieldInfo actionsField = controllerType.GetField("acoes", Members);
        Assert.That(ready, Is.Not.Null);
        Assert.That(openMenu, Is.Not.Null);
        Assert.That(navigationField, Is.Not.Null);
        Assert.That(actionsField, Is.Not.Null);

        for (int frame = 0; frame < 120 && !(bool)ready.GetValue(controller); frame++)
            yield return null;
        Assert.That((bool)ready.GetValue(controller), Is.True);
        openMenu.Invoke(controller, new object[] { true });
        menuNavigation = navigationField.GetValue(controller) as ScrollView;
        menuActions = actionsField.GetValue(controller) as ScrollView;
        Assert.That(menuNavigation, Is.Not.Null);
        Assert.That(menuActions, Is.Not.Null);
        Button research = menuNavigation.Query<Button>(className: "gov-nav-button").ToList()
            .Find(button => button.name == "nav-pesquisa e desenvolvimento");
        Assert.That(research, Is.Not.Null);
        Click(research);
    }

    private GameObject CriarAeroportoComercialDeTeste(Type airportType)
    {
        GameObject root = new GameObject("Aeroporto comercial de teste");
        root.SetActive(false);
        Component identity = root.AddComponent(ResolveType("IdentidadeUnidade"));
        SetField(identity, "teamID", 1);

        GameObject patio = CriarFilho(root.transform, "Patio", Vector3.zero);
        CriarFilho(patio.transform, "Parada_Grande_Expedicao", Vector3.zero);
        GameObject hangar = CriarFilho(root.transform, "Hangar", Vector3.zero);
        CriarFilho(hangar.transform, "Preparacao", Vector3.zero);
        CriarFilho(hangar.transform, "Pronto", Vector3.zero);
        GameObject pista = CriarFilho(root.transform, "PistaComercial", Vector3.zero);
        CriarFilho(pista.transform, "PontoPista_01", new Vector3(0f, 0f, 100f));
        CriarFilho(pista.transform, "PontoPista_02", new Vector3(0f, 0f, 200f));
        GameObject decolagem = CriarFilho(root.transform, "Decolagem", Vector3.zero);
        CriarFilho(decolagem.transform, "Decolagem_01", Vector3.zero);
        CriarFilho(decolagem.transform, "Decolagem_02", new Vector3(0f, 0f, 50f));
        GameObject decida = CriarFilho(root.transform, "Decida", Vector3.zero);
        CriarFilho(decida.transform, "Decida_01", new Vector3(0f, 0f, 50f));
        CriarFilho(decida.transform, "Decida_02", new Vector3(0f, 0f, 100f));

        Component airport = root.AddComponent(airportType);
        SetField(airport, "patio", patio.transform);
        SetField(airport, "patioMilitar", patio.transform);
        SetField(airport, "hangarAviao", hangar.transform);
        SetField(airport, "decolagem", decolagem.transform);
        SetField(airport, "decida", decida.transform);
        SetField(airport, "prefabAviaoComercial", pista);
        object pistaConfigurada = airportType.GetField("pista1", Members).GetValue(airport);
        pistaConfigurada.GetType().GetField("pista", Members).SetValue(pistaConfigurada, pista.transform);
        airportType.GetField("pista1", Members).SetValue(airport, pistaConfigurada);
        root.transform.position = new Vector3(39700f, 0f, 24000f);
        root.SetActive(true);
        return root;
    }

    private void CriarUsinaDeTeste(int teamId)
    {
        GameObject usina = new GameObject("Usina Solar - Teste Sinal Perdido");
        Type economicType = ResolveType("EstruturaEconomica");
        Component estrutura = usina.AddComponent(economicType);
        Type enumType = ResolveType("TipoEstruturaEconomica");
        SetField(estrutura, "tipo", Enum.Parse(enumType, "UsinaSolar"));
        SetField(estrutura, "teamId", teamId);
        SetField(estrutura, "energiaProduzida", 500f);
        SetField(estrutura, "energiaConsumida", 0f);
        SetField(estrutura, "eficiencia", 1f);
        extraObjects.Add(usina);

        Type econSysType = ResolveType("SistemaEconomiaImoveis");
        object econSys = econSysType.GetProperty("Instancia", Members)?.GetValue(null);
        if (econSys != null)
            econSysType.GetMethod("Recalcular", Members)?.Invoke(econSys, null);
    }

    private static GameObject CriarFilho(Transform parent, string name, Vector3 localPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child;
    }

    private static void SetField(Component target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, Members);
        Assert.That(field, Is.Not.Null, "Campo não encontrado: " + target.GetType().Name + "." + fieldName);
        field.SetValue(target, value);
    }

    private static Type ResolveType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName, false);
            if (type != null) return type;
        }

        Assert.Fail("Tipo não encontrado: " + typeName);
        return null;
    }

    private static void Click(Button button)
    {
        PropertyInfo clickableProperty = typeof(Button).GetProperty("clickable", Members);
        Assert.That(clickableProperty, Is.Not.Null);
        object clickable = clickableProperty.GetValue(button);
        MethodInfo simulateClick = clickable.GetType().GetMethod(
            "SimulateSingleClick", Members, null, new[] { typeof(EventBase), typeof(int) }, null);
        Assert.That(simulateClick, Is.Not.Null,
            "O botão do UI Toolkit precisa expor o callback de clique real.");
        simulateClick.Invoke(clickable, new object[] { ClickEvent.GetPooled(), 100 });
    }
}
