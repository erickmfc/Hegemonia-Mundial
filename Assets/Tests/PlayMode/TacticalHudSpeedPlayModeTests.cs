using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public sealed class TacticalHudSpeedPlayModeTests
{
    [UnityTest]
    public IEnumerator GenericAircraftUsesOneHudSpeedMultiplierAndHonorsLimits()
    {
        GameObject unit = new GameObject("HUD speed test aircraft");
        try
        {
            System.Type movementType = System.Type.GetType("ControleUnidade, Assembly-CSharp");
            Assert.That(movementType, Is.Not.Null, "ControleUnidade must be present in the runtime assembly.");
            Component movement = unit.AddComponent(movementType);
            SetPrivateField(movement, movementType, "ehAereo", true);
            SetPrivateField(movement, movementType, "voando", true);
            movementType.GetField("velocidadeVoo", BindingFlags.Instance | BindingFlags.Public).SetValue(movement, 8f);

            Assert.That(Adjust(movement, movementType, 0.1f), Is.True);
            Assert.That(GetMultiplier(movement, movementType), Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(GetBaseSpeed(movement, movementType), Is.EqualTo(8f).Within(0.001f), "The base speed must not be scaled a second time.");
            Assert.That(GetActualSpeed(movement, movementType), Is.EqualTo(8.8f).Within(0.01f));

            Assert.That(Adjust(movement, movementType, 5f), Is.True);
            Assert.That(GetMultiplier(movement, movementType), Is.EqualTo(2f).Within(0.001f));
            Assert.That(Adjust(movement, movementType, 0.1f), Is.False);

            Assert.That(Adjust(movement, movementType, -5f), Is.True);
            Assert.That(GetMultiplier(movement, movementType), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(Adjust(movement, movementType, -0.1f), Is.False);
            Assert.That(GetActualSpeed(movement, movementType), Is.EqualTo(4f).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(unit);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator CustomGroundAndTankerMovementAcceptHudSpeedAdjustments()
    {
        GameObject ground = new GameObject("HUD speed test ground vehicle");
        GameObject tanker = new GameObject("HUD speed test tanker");
        try
        {
            System.Type unitType = System.Type.GetType("ControleUnidade, Assembly-CSharp");
            System.Type groundMoverType = System.Type.GetType("MovimentoRealTerrestre, Assembly-CSharp");
            System.Type tankerType = System.Type.GetType("NavioPetroleiro, Assembly-CSharp");
            Assert.That(unitType, Is.Not.Null);
            Assert.That(groundMoverType, Is.Not.Null);
            Assert.That(tankerType, Is.Not.Null);

            ground.AddComponent<NavMeshAgent>();
            Component groundMover = ground.AddComponent(groundMoverType);
            Component groundUnit = ground.AddComponent(unitType);
            SetPrivateObjectField(groundMover, groundMoverType, "controleUnidadeCache", groundUnit);
            Assert.That(GetProperty(groundUnit, unitType, "PossuiControleVelocidadeHud"), Is.True);
            Assert.That(Adjust(groundUnit, unitType, 0.1f), Is.True);

            MethodInfo groundSpeed = groundMoverType.GetMethod("ObterVelocidadeMaximaComandoHud", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(groundSpeed, Is.Not.Null);
            Assert.That((float)groundSpeed.Invoke(groundMover, null), Is.EqualTo(13.2f).Within(0.02f));

            tanker.AddComponent<NavMeshAgent>();
            Component tankerUnit = tanker.AddComponent(tankerType);
            Assert.That(GetProperty(tankerUnit, tankerType, "PossuiControleVelocidadeHud"), Is.True,
                "The tanker must remain adjustable while its logistics controller disables the NavMeshAgent.");
            Assert.That(Adjust(tankerUnit, tankerType, 0.1f), Is.True);

            MethodInfo tankerSpeed = tankerType.GetMethod("AplicarMultiplicadorComandoHud", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tankerSpeed, Is.Not.Null);
            Assert.That((float)tankerSpeed.Invoke(tankerUnit, new object[] { 4f }), Is.EqualTo(4.4f).Within(0.02f));
        }
        finally
        {
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(tanker);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator AirOperationsAndHudCardsRespondToRuntimeClicks()
    {
        GameObject selectionObject = null;
        GameObject unitObject = null;
        GameObject unit2Object = null;
        GameObject hudObject = null;
        try
        {
            System.Type unitType = System.Type.GetType("ControleUnidade, Assembly-CSharp");
            System.Type selectionType = System.Type.GetType("GerenteSelecao, Assembly-CSharp");
            System.Type controllerType = System.Type.GetType("MenuComandoController, Assembly-CSharp");
            System.Type combatMenuType = System.Type.GetType("MenuCombateNaval, Assembly-CSharp");
            System.Type commercialType = System.Type.GetType("ControleAviaoComercial, Assembly-CSharp");
            System.Type ac130Type = System.Type.GetType("ControleAviaoAC130, Assembly-CSharp");
            System.Type selectedAircraftType = System.Type.GetType("ControleAviao, Assembly-CSharp");
            System.Type groundMovementType = System.Type.GetType("MovimentoRealTerrestre, Assembly-CSharp");
            Assert.That(unitType, Is.Not.Null);
            Assert.That(selectionType, Is.Not.Null);
            Assert.That(controllerType, Is.Not.Null);
            Assert.That(combatMenuType, Is.Not.Null);
            Assert.That(commercialType, Is.Not.Null);
            Assert.That(ac130Type, Is.Not.Null);
            Assert.That(selectedAircraftType, Is.Not.Null);
            Assert.That(groundMovementType, Is.Not.Null);

            // Both specialized aircraft inherit the same command multiplier,
            // including their custom commercial and AC-130 movement paths.
            foreach (System.Type aircraftType in new[] { commercialType, ac130Type })
            {
                GameObject aircraft = new GameObject("HUD speed aircraft verification");
                Component aircraftControl = aircraft.AddComponent(aircraftType);
                Component unitControl = aircraft.AddComponent(unitType);
                Assert.That(Adjust(unitControl, unitType, 0.1f), Is.True);
                PropertyInfo multiplier = aircraftType.GetProperty(
                    "MultiplicadorVelocidadeComandoHud",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                Assert.That(multiplier, Is.Not.Null, aircraftType.Name);
                Assert.That((float)multiplier.GetValue(aircraftControl), Is.EqualTo(1.1f).Within(0.001f));
                Object.DestroyImmediate(aircraft);
            }

            selectionObject = new GameObject("HUD interaction test selection");
            Component selection = selectionObject.AddComponent(selectionType);
            unitObject = new GameObject("HUD interaction test unit");
            NavMeshAgent unitAgent = unitObject.AddComponent<NavMeshAgent>();
            unitObject.AddComponent(System.Type.GetType("IdentidadeUnidade, Assembly-CSharp"));
            Component unit = unitObject.AddComponent(unitType);
            System.Collections.IList selectedUnits = (System.Collections.IList)selectionType
                .GetField("unidadesSelecionadas", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(selection);
            selectedUnits.Add(unit);

            hudObject = new GameObject("HUD interaction test document");
            UIDocument document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("PanelSettings");
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("MenuComando/MenuComando");
            Assert.That(document.panelSettings, Is.Not.Null);
            Assert.That(document.visualTreeAsset, Is.Not.Null);
            Component controller = hudObject.AddComponent(controllerType);
            SetPrivateObjectField(controller, controllerType, "gerenteSelecao", selection);

            yield return null;

            VisualElement root = (VisualElement)controllerType
                .GetField("root", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.Q<Button>("hud-context-rtb").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "Return-to-base should stay hidden for a non-aircraft unit.");
            Assert.That(root.Q<Button>("formation-slot-1").enabledSelf, Is.False,
                "Formation slots should remain disabled for a single selected unit.");

            // Every bottom HUD card must open in the same bounded detail panel
            // and return to its original slot when the overlay is dismissed.
            VisualElement[] hudCards = root.Query<VisualElement>(className: "tactical-card").ToList().ToArray();
            Assert.That(hudCards.Length, Is.EqualTo(10), "All ten tactical cards should be present in the bottom HUD.");
            foreach (VisualElement card in hudCards)
            {
                VisualElement originalParent = card.parent;
                int originalIndex = originalParent.hierarchy.IndexOf(card);
                SimulateCardClick(card);
                yield return null;
                Assert.That(card.parent.name, Is.EqualTo("hud-card-detail-scroll"), card.name);
                VisualElement detailPanel = root.Q<VisualElement>("hud-card-detail-panel");
                Assert.That(detailPanel.resolvedStyle.left, Is.GreaterThan(0f), card.name);
                Assert.That(detailPanel.resolvedStyle.right, Is.GreaterThan(0f), card.name);
                Assert.That(detailPanel.resolvedStyle.top, Is.GreaterThan(0f), card.name);
                Assert.That(detailPanel.resolvedStyle.bottom, Is.GreaterThan(0f), card.name);
                SimulateButtonClick(root.Q<Button>("hud-card-detail-close"));
                Assert.That(card.parent, Is.SameAs(originalParent), card.name);
                Assert.That(originalParent.hierarchy.IndexOf(card), Is.EqualTo(originalIndex), card.name);
            }

            // Clicking the dimmed backdrop is also a working dismiss action.
            SimulateCardClick(hudCards[0]);
            VisualElement backdrop = root.Q<VisualElement>("hud-card-backdrop");
            ClickEvent backdropClick = ClickEvent.GetPooled();
            typeof(EventBase).GetProperty("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(backdropClick, backdrop);
            backdrop.SendEvent(backdropClick);
            Assert.That(hudCards[0].parent.ClassListContains("contexto-modulos"));

            SimulateButtonClick(root.Q<Button>("btn-hud-fechar"));
            Assert.That(root.Q<VisualElement>("barra-comando-contextual").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            SimulateButtonClick(root.Q<Button>("btn-hud-reabrir"));
            Assert.That(root.Q<VisualElement>("barra-comando-contextual").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            SimulateButtonClick(root.Q<Button>("btn-contexto-centro"));
            Assert.That(controllerType.GetProperty("MenuAberto", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(controller), Is.EqualTo(true), "The tactical center button should open its menu.");
            controllerType.GetMethod("FecharMenu", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(controllerType.GetProperty("MenuAberto", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(controller), Is.EqualTo(false));

            SimulateButtonClick(root.Q<Button>("hud-roe-free"));
            System.Type modeType = combatMenuType.GetNestedType("Modo", BindingFlags.Public);
            object automaticMode = System.Enum.Parse(modeType, "Automatico");
            object manualMode = System.Enum.Parse(modeType, "Manual");
            object passiveMode = System.Enum.Parse(modeType, "Passivo");
            System.Reflection.MethodInfo getCombatMode = combatMenuType.GetMethod("ObterModoCombateHud", BindingFlags.Public | BindingFlags.Static);
            Assert.That(getCombatMode.Invoke(null, new[] { unit }), Is.EqualTo(automaticMode),
                "The fire-free button should apply automatic engagement.");
            Assert.That(combatMenuType.GetMethod("ModoCombateHudLimitaAlvos", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { unit }), Is.EqualTo(false),
                "The fire-free button should remove target restrictions.");
            SimulateButtonClick(root.Q<Button>("hud-roe-hold"));
            Assert.That(getCombatMode.Invoke(null, new[] { unit }), Is.EqualTo(passiveMode),
                "The cease-fire button should set passive combat mode.");
            SimulateButtonClick(root.Q<Button>("hud-roe-defensive"));
            Assert.That(getCombatMode.Invoke(null, new[] { unit }), Is.EqualTo(manualMode),
                "The defensive button should set manual combat mode.");
            SimulateButtonClick(root.Q<Button>("hud-roe-tight"));
            Assert.That(getCombatMode.Invoke(null, new[] { unit }), Is.EqualTo(automaticMode));
            Assert.That(combatMenuType.GetMethod("ModoCombateHudLimitaAlvos", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { unit }), Is.EqualTo(true),
                "The restricted-fire button should restore target restrictions.");
            SimulateButtonClick(root.Q<Button>("hud-ai-assist"));
            Assert.That(getCombatMode.Invoke(null, new[] { unit }), Is.EqualTo(automaticMode),
                "The assistance button should apply automatic combat mode.");
            Assert.That(combatMenuType.GetMethod("ModoCombateHudLimitaAlvos", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { unit }), Is.EqualTo(true),
                "The assistance button should keep target restrictions enabled.");
            SimulateButtonClick(root.Q<Button>("hud-radar"));
            Component radar = unitObject.GetComponent("RadarUnidadeTatica");
            Assert.That(radar, Is.Not.Null, "The radar button should create or update a tactical radar for an eligible unit.");
            Assert.That(GetProperty(radar, radar.GetType(), "RadarLigado"), Is.EqualTo(true),
                "The radar button should turn radar emission on.");
            SimulateButtonClick(root.Q<Button>("formation-slot-1"));
            foreach (string indicator in new[] { "hud-sonar", "hud-esm", "hud-datalink" })
                Assert.That(root.Q<Label>(indicator), Is.Not.Null, indicator + " should remain a visible status indicator.");

            VisualElement speedCard = root.Query<VisualElement>(className: "speed-card").First();
            Assert.That(speedCard, Is.Not.Null);
            SimulateCardClick(speedCard);
            yield return null;
            Assert.That(speedCard.parent.name, Is.EqualTo("hud-card-detail-scroll"),
                "Clicking a data card should move it into the readable expanded panel.");

            SimulateButtonClick(root.Q<Button>("hud-speed-up"));
            Assert.That(GetMultiplier(unit, unitType), Is.EqualTo(1.1f).Within(0.001f),
                "The expanded speed button should adjust the selected unit without closing the card.");
            Assert.That(unitAgent.speed, Is.EqualTo(3.85f).Within(0.02f),
                "Speed up must update the selected unit's actual NavMesh movement speed.");
            Assert.That(root.Q<Label>("hud-speed-order").text, Does.Contain("110%"),
                "The order indicator must show the new commanded speed immediately.");
            Assert.That(speedCard.parent.name, Is.EqualTo("hud-card-detail-scroll"));

            SimulateButtonClick(root.Q<Button>("hud-speed-down"));
            Assert.That(GetMultiplier(unit, unitType), Is.EqualTo(1f).Within(0.001f),
                "Speed down must undo a previous 10% speed increase.");
            Assert.That(unitAgent.speed, Is.EqualTo(3.5f).Within(0.02f),
                "Speed down must restore the selected unit's actual NavMesh movement speed.");

            SimulateButtonClick(root.Q<Button>("hud-card-detail-close"));
            Assert.That(speedCard.parent.ClassListContains("contexto-modulos"));

            // A selected root can own its movement executor in a child object.
            // The speed command and group formation must still reach that unit.
            unit2Object = new GameObject("HUD interaction test unit with child movement");
            unit2Object.transform.position = new Vector3(24f, 0f, 0f);
            unit2Object.AddComponent(System.Type.GetType("IdentidadeUnidade, Assembly-CSharp"));
            Component childMovement = new GameObject("Nested movement executor").AddComponent<NavMeshAgent>();
            childMovement.transform.SetParent(unit2Object.transform, false);
            childMovement.gameObject.AddComponent(groundMovementType);
            childMovement.gameObject.AddComponent(selectedAircraftType);
            Component unit2 = unit2Object.AddComponent(unitType);
            selectedUnits.Add(unit2);
            yield return null;
            controllerType.GetMethod("SincronizarSelecaoComJogo", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            controllerType.GetMethod("AtualizarBarraComandoContextual", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(root.Q<Button>("hud-context-rtb").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                "Return-to-base should appear when an aircraft is in the selected group.");

            Button formationSlot2 = root.Q<Button>("formation-slot-2");
            Assert.That(formationSlot2.enabledSelf, Is.True,
                "Formation slots should connect once two units are selected.");
            VisualElement formationCard = root.Q<VisualElement>("formation-card");
            SimulateButtonClick(root.Q<Button>("quick-formation"));
            Assert.That(formationCard.ClassListContains("formation-editing"), Is.True,
                "The formation edit button should activate slot editing for a group.");
            SimulateButtonClick(formationSlot2);
            Assert.That(selectedUnits[0], Is.SameAs(unit2),
                "Choosing slot 2 must make that unit the selected group leader.");
            SimulateButtonClick(root.Q<Button>("quick-grid"));
            Component followLink = unitObject.GetComponent("ComportamentoSeguirUniversal");
            Assert.That(followLink, Is.Not.Null,
                "If a grid destination is rejected, the remaining unit must be connected to its leader.");
            Assert.That(GetProperty(followLink, followLink.GetType(), "AlvoSeguido"), Is.SameAs(unit2Object.transform),
                "The fallback formation connection must target the chosen leader.");
            Assert.That(root.Q<Label>("contexto-feedback").text, Is.Not.Empty,
                "The formation action should give the player feedback about what was applied.");

            // Exercise every remaining quick action and the matching patrol
            // button inside the waypoint card, checking its resulting feedback.
            Button feedbackButton = root.Q<Button>("quick-escort");
            SimulateButtonClick(feedbackButton);
            Assert.That(GetProperty(followLink, followLink.GetType(), "AlvoSeguido"), Is.SameAs(unit2Object.transform),
                "Escort must link each selected wing unit to the current group leader.");

            SimulateButtonClick(root.Q<Button>("quick-formation"));
            Assert.That(formationCard.ClassListContains("formation-editing"), Is.False,
                "The formation edit button should exit slot editing when clicked again.");
            SimulateButtonClick(root.Q<Button>("quick-formation"));
            Assert.That(formationCard.ClassListContains("formation-editing"), Is.True);

            foreach (string actionName in new[] { "btn-contexto-seguir", "quick-patrol", "btn-contexto-patrulhar", "quick-intercept" })
            {
                root.Q<Label>("contexto-feedback").text = string.Empty;
                SimulateButtonClick(root.Q<Button>(actionName));
                Assert.That(root.Q<Label>("contexto-feedback").text, Is.Not.Empty,
                    actionName + " should respond with a result or a clear unavailable-system message.");
            }

            SimulateButtonClick(root.Q<Button>("quick-damage"));
            Assert.That(root.Q<Label>("contexto-feedback").text, Does.Contain("CONTROLE DE DANOS"),
                "The damage action should report the selected unit's health data or its absence.");
            SimulateButtonClick(root.Q<Button>("quick-camera"));
            Assert.That(root.Q<Label>("contexto-feedback").text, Is.Not.Empty,
                "The camera action should focus the selection or report that no game camera is available.");
            SimulateButtonClick(root.Q<Button>("quick-camera-options"));
            Assert.That(root.Q<Label>("contexto-feedback").text, Is.Not.Empty,
                "The follow-camera action should switch modes or report that its camera is unavailable.");

            SimulateButtonClick(root.Q<Button>("hud-context-rtb"));
            Assert.That(((Label)controllerType.GetField("ordemFeedback", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller)).text, Does.Contain("RETORNANDO"),
                "The aircraft return-to-base action should process the selected aircraft.");

            SimulateButtonClick(root.Q<Button>("hud-roe-hold"));
            Assert.That(getCombatMode.Invoke(null, new[] { unit }), Is.EqualTo(passiveMode));
            Assert.That(getCombatMode.Invoke(null, new[] { unit2 }), Is.EqualTo(passiveMode),
                "Fire-control buttons should apply to every selected unit.");
            SimulateButtonClick(root.Q<Button>("hud-roe-defensive"));
            Assert.That(getCombatMode.Invoke(null, new[] { unit }), Is.EqualTo(manualMode));
            Assert.That(getCombatMode.Invoke(null, new[] { unit2 }), Is.EqualTo(manualMode));
            SimulateButtonClick(root.Q<Button>("hud-roe-tight"));
            Assert.That(getCombatMode.Invoke(null, new[] { unit }), Is.EqualTo(automaticMode));
            Assert.That(getCombatMode.Invoke(null, new[] { unit2 }), Is.EqualTo(automaticMode));


            SimulateCardClick(speedCard);
            yield return null;
            NavMeshAgent nestedAgent = childMovement as NavMeshAgent;
            SimulateButtonClick(root.Q<Button>("hud-speed-up"));
            yield return null;
            Assert.That(GetMultiplier(unit, unitType), Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(GetMultiplier(unit2, unitType), Is.EqualTo(1.1f).Within(0.001f),
                "Speed up must reach every selected unit in the group.");
            Assert.That(nestedAgent.speed, Is.EqualTo(13.2f).Within(0.05f),
                "A child movement executor must consume the selected root's speed multiplier.");
            SimulateButtonClick(root.Q<Button>("hud-speed-down"));
            yield return null;
            Assert.That(GetMultiplier(unit, unitType), Is.EqualTo(1f).Within(0.001f));
            Assert.That(GetMultiplier(unit2, unitType), Is.EqualTo(1f).Within(0.001f));

            SimulateButtonClick(root.Q<Button>("hud-ai-auto"));
            object actualMode = getCombatMode
                .Invoke(null, new[] { unit });
            Assert.That(actualMode, Is.EqualTo(automaticMode), "The automation strategy button should update the unit's combat mode.");
            Assert.That(combatMenuType.GetMethod("ObterModoCombateHud", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { unit2 }), Is.EqualTo(automaticMode),
                "Combat automation buttons must apply to every selected unit.");
        }
        finally
        {
            if (hudObject != null) Object.DestroyImmediate(hudObject);
            if (unit2Object != null) Object.DestroyImmediate(unit2Object);
            if (unitObject != null) Object.DestroyImmediate(unitObject);
            if (selectionObject != null) Object.DestroyImmediate(selectionObject);
        }

        yield return null;
    }

    private static void SetPrivateField(Component target, System.Type targetType, string fieldName, bool value)
    {
        FieldInfo field = targetType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }

    private static void SetPrivateObjectField(Component target, System.Type targetType, string fieldName, object value)
    {
        FieldInfo field = targetType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }

    private static void SimulateCardClick(VisualElement card)
    {
        ClickEvent click = ClickEvent.GetPooled();
        PropertyInfo targetProperty = typeof(EventBase).GetProperty(
            "target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(targetProperty, Is.Not.Null, "UI Toolkit should expose the event target for a targeted card click.");
        targetProperty.SetValue(click, card);
        card.SendEvent(click);
    }

    private static object GetProperty(Component target, System.Type targetType, string propertyName)
    {
        PropertyInfo property = targetType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, $"Expected property '{propertyName}' to exist.");
        return property.GetValue(target);
    }

    private static void SimulateButtonClick(Button button)
    {
        Assert.That(button, Is.Not.Null);
        object clickable = typeof(Button).GetProperty("clickable").GetValue(button);
        MethodInfo simulateClick = clickable.GetType().GetMethod(
            "SimulateSingleClick",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(EventBase), typeof(int) },
            null);
        Assert.That(simulateClick, Is.Not.Null, "UI Toolkit should provide its single-click simulation in Play Mode.");
        simulateClick.Invoke(clickable, new object[] { ClickEvent.GetPooled(), 100 });
    }

    private static bool Adjust(Component target, System.Type targetType, float delta)
    {
        MethodInfo method = targetType.GetMethod("AjustarVelocidadeComandoHud", BindingFlags.Instance | BindingFlags.Public);
        return (bool)method.Invoke(target, new object[] { delta });
    }

    private static float GetMultiplier(Component target, System.Type targetType)
    {
        PropertyInfo property = targetType.GetProperty("MultiplicadorVelocidadeComandoHud", BindingFlags.Instance | BindingFlags.Public);
        return (float)property.GetValue(target);
    }

    private static float GetBaseSpeed(Component target, System.Type targetType)
    {
        FieldInfo field = targetType.GetField("velocidadeVoo", BindingFlags.Instance | BindingFlags.Public);
        return (float)field.GetValue(target);
    }

    private static float GetActualSpeed(Component target, System.Type targetType)
    {
        MethodInfo method = targetType.GetMethod("ObterVelocidadeAtualReal", BindingFlags.Instance | BindingFlags.Public);
        return (float)method.Invoke(target, null);
    }
}
