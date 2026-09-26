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
        GameObject hudObject = null;
        try
        {
            System.Type unitType = System.Type.GetType("ControleUnidade, Assembly-CSharp");
            System.Type selectionType = System.Type.GetType("GerenteSelecao, Assembly-CSharp");
            System.Type controllerType = System.Type.GetType("MenuComandoController, Assembly-CSharp");
            System.Type combatMenuType = System.Type.GetType("MenuCombateNaval, Assembly-CSharp");
            System.Type commercialType = System.Type.GetType("ControleAviaoComercial, Assembly-CSharp");
            System.Type ac130Type = System.Type.GetType("ControleAviaoAC130, Assembly-CSharp");
            Assert.That(unitType, Is.Not.Null);
            Assert.That(selectionType, Is.Not.Null);
            Assert.That(controllerType, Is.Not.Null);
            Assert.That(combatMenuType, Is.Not.Null);
            Assert.That(commercialType, Is.Not.Null);
            Assert.That(ac130Type, Is.Not.Null);

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
            unitObject.AddComponent<NavMeshAgent>();
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

            VisualElement speedCard = root.Query<VisualElement>(className: "speed-card").First();
            Assert.That(speedCard, Is.Not.Null);
            SimulateCardClick(speedCard);
            yield return null;
            Assert.That(speedCard.parent.name, Is.EqualTo("hud-card-detail-scroll"),
                "Clicking a data card should move it into the readable expanded panel.");

            SimulateButtonClick(root.Q<Button>("hud-speed-up"));
            Assert.That(GetMultiplier(unit, unitType), Is.EqualTo(1.1f).Within(0.001f),
                "The expanded speed button should adjust the selected unit without closing the card.");
            Assert.That(speedCard.parent.name, Is.EqualTo("hud-card-detail-scroll"));

            SimulateButtonClick(root.Q<Button>("hud-card-detail-close"));
            Assert.That(speedCard.parent.ClassListContains("contexto-modulos"));

            SimulateButtonClick(root.Q<Button>("hud-ai-auto"));
            System.Type modeType = combatMenuType.GetNestedType("Modo", BindingFlags.Public);
            object automaticMode = System.Enum.Parse(modeType, "Automatico");
            object actualMode = combatMenuType.GetMethod("ObterModoCombateHud", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { unit });
            Assert.That(actualMode, Is.EqualTo(automaticMode), "The automation strategy button should update the unit's combat mode.");
        }
        finally
        {
            if (hudObject != null) Object.DestroyImmediate(hudObject);
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
        MethodInfo simulateClick = clickable.GetType().GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(simulateClick, Is.Not.Null, "UI Toolkit should expose its normal single-click path in Play Mode.");
        simulateClick.Invoke(clickable, null);
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
