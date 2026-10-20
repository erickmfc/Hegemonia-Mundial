using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MiniMapAttackRangePrivacyPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> cleanupObjects = new List<GameObject>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = cleanupObjects.Count - 1; i >= 0; i--)
            if (cleanupObjects[i] != null)
                UnityEngine.Object.Destroy(cleanupObjects[i]);

        cleanupObjects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator SelectedAndGlobalFriendlyRangesNeverRevealDetectedEnemyRange()
    {
        GameObject canvasObject = new GameObject("MiniMapRangePrivacyTestCanvas", typeof(RectTransform), typeof(Canvas));
        cleanupObjects.Add(canvasObject);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject displayRoot = new GameObject("Canvas_MiniMapa", typeof(RectTransform));
        displayRoot.transform.SetParent(canvasObject.transform, false);

        GameObject host = new GameObject("MiniMapRangePrivacyTestHost", typeof(RectTransform));
        host.transform.SetParent(canvasObject.transform, false);
        Type miniMapType = ResolveType("MiniMapa");
        Component miniMap = host.AddComponent(miniMapType);

        GameObject friendly = new GameObject("FriendlyRangeTestUnit");
        cleanupObjects.Add(friendly);
        friendly.SetActive(false);
        Component identity = friendly.AddComponent(ResolveType("IdentidadeUnidade"));
        Set(identity, "teamID", 1);
        Component control = friendly.AddComponent(ResolveType("ControleUnidade"));
        friendly.AddComponent(ResolveType("SistemaDeTiro"));
        Set(friendly.GetComponent(ResolveType("SistemaDeTiro")), "alcanceTiro", 1250f);
        friendly.SetActive(true);

        GameObject selectionObject = new GameObject("MiniMapRangePrivacyTestSelection");
        cleanupObjects.Add(selectionObject);
        Component selection = selectionObject.AddComponent(ResolveType("GerenteSelecao"));
        IList selectedUnits = (IList)Get(selection, "unidadesSelecionadas");

        yield return null;
        yield return null;

        Type markerType = miniMapType.GetNestedType("MarkerData", BindingFlags.NonPublic);
        Type categoryType = ResolveType("Hegemonia.RTS.RTSTacticalContactCategory");
        Type relationType = ResolveType("Hegemonia.RTS.RTSTacticalContactRelation");
        Type snapshotType = ResolveType("Hegemonia.RTS.RTSTacticalContactSnapshot");
        object ownMarker = CreateMarker(markerType, categoryType, relationType, snapshotType,
            true, identity, control, "Naval", "Friendly", Vector3.zero);
        object detectedEnemyMarker = CreateMarker(markerType, categoryType, relationType, snapshotType,
            false, null, null, "Naval", "Hostile", Vector3.zero);
        IList markers = (IList)Get(miniMap, "_markers");
        markers.Clear();
        markers.Add(ownMarker);
        markers.Add(detectedEnemyMarker);

        Set(miniMap, "_playerTeamId", 1);
        Set(miniMap, "_selectionManager", selection);
        Invoke(miniMap, "UpdateMapViewport");
        Invoke(miniMap, "UpdateMarkerViews");

        IList markerViews = (IList)Get(miniMap, "_markerViews");
        Assert.That(markerViews.Count, Is.GreaterThanOrEqualTo(2));
        AssertMarkerVisible(markerViews[0], true);
        AssertMarkerVisible(markerViews[1], true,
            "Contato inimigo detectado continua no minimapa.");
        for (int i = 2; i < markerViews.Count; i++)
        {
            AssertMarkerVisible(markerViews[i], false,
                "A reutilização do pool não deve deixar contatos antigos visíveis.");
            AssertRangeVisible(markerViews[i], false,
                "Um marcador antigo no pool não pode revelar alcance algum.");
        }
        AssertRangeVisible(markerViews[0], false,
            "Unidade própria não selecionada começa sem raio quando o filtro global está desligado.");
        AssertRangeVisible(markerViews[1], false,
            "Contato inimigo nunca revela seu alcance de ataque.");

        selectedUnits.Add(control);
        Invoke(miniMap, "UpdateMarkerViews");
        AssertRangeVisible(markerViews[0], true,
            "Selecionar unidade militar própria mostra o alcance individual.");
        AssertRangeVisible(markerViews[1], false,
            "A seleção própria não deve revelar o raio do inimigo.");

        Invoke(miniMap, "ToggleFriendlyRanges");
        Invoke(miniMap, "UpdateMarkerViews");
        AssertRangeVisible(markerViews[0], true,
            "O filtro RAIOS mantém visíveis os alcances das unidades próprias visíveis.");
        AssertRangeVisible(markerViews[1], false,
            "O filtro global inclui somente unidades próprias.");

        selectedUnits.Clear();
        Invoke(miniMap, "UpdateMarkerViews");
        AssertRangeVisible(markerViews[0], true,
            "Com RAIOS ligado, o raio próprio permanece visível após desmarcar a unidade.");
        Invoke(miniMap, "ToggleFriendlyRanges");
        Invoke(miniMap, "UpdateMarkerViews");
        AssertRangeVisible(markerViews[0], false,
            "Desligar RAIOS oculta o raio global da unidade não selecionada.");
        AssertRangeVisible(markerViews[1], false);
    }

    private static object CreateMarker(Type markerType, Type categoryType, Type relationType,
        Type snapshotType, bool friendly, Component identity, Component control,
        string category, string relation, Vector3 position)
    {
        object marker = Activator.CreateInstance(markerType);
        Set(marker, "isFriendlyUnit", friendly);
        Set(marker, "friendlyIdentity", identity);
        Set(marker, "friendlyControl", control);
        Set(marker, "contact", Activator.CreateInstance(snapshotType));
        Set(marker, "position", position);
        Set(marker, "category", Enum.Parse(categoryType, category));
        Set(marker, "relation", Enum.Parse(relationType, relation));
        Set(marker, "isEstimated", false);
        return marker;
    }

    private static void AssertMarkerVisible(object view, bool expected, string message = null)
    {
        RectTransform rect = (RectTransform)Get(view, "rect");
        Assert.That(rect.gameObject.activeSelf, Is.EqualTo(expected), message);
    }

    private static void AssertRangeVisible(object view, bool expected, string message = null)
    {
        RectTransform range = (RectTransform)Get(view, "rangeRect");
        Assert.That(range.gameObject.activeSelf, Is.EqualTo(expected), message);
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null) return type;
        }

        throw new AssertionException("Tipo de gameplay não encontrado: " + fullName);
    }

    private static object Get(object target, string name)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, Members);
        if (field != null) return field.GetValue(target);
        PropertyInfo property = type.GetProperty(name, Members);
        if (property != null) return property.GetValue(target, null);
        throw new AssertionException("Membro ausente: " + type.FullName + "." + name);
    }

    private static void Set(object target, string name, object value)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(name, Members);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo property = type.GetProperty(name, Members);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
            return;
        }

        throw new AssertionException("Membro gravável ausente: " + type.FullName + "." + name);
    }

    private static object Invoke(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name, Members);
        if (method == null) throw new AssertionException("Método ausente: " + target.GetType().FullName + "." + name);
        return method.Invoke(target, null);
    }
}
