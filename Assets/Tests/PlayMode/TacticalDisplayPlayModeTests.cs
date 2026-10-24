using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class TacticalDisplayPlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string PrivateOriginSentinel = "UI_PRIVACY_SENTINEL";
    private const int AnonymousContactBurst = 300;

    private readonly List<GameObject> cleanupObjects = new List<GameObject>();
    private Camera legacyMiniMapCamera;
    private bool legacyCameraWasEnabled;
    private RenderTexture legacyCameraTarget;

    [UnityTest]
    public IEnumerator DisplayIsolatesContactsHidesRadarAffiliationAndCleansItsOwnState()
    {
        CaptureLegacyCameraState();

        GameObject canvasObject = new GameObject("TacticalDisplayPlayModeTestCanvas",
            typeof(RectTransform), typeof(Canvas));
        cleanupObjects.Add(canvasObject);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        // Create the expected display root inside the fixture so MiniMapa does
        // not find, reparent, or replace a same-named root in the user's scene.
        GameObject isolatedDisplayRoot = new GameObject("Canvas_MiniMapa", typeof(RectTransform));
        isolatedDisplayRoot.transform.SetParent(canvasObject.transform, false);

        GameObject isolatedAdapterObject = new GameObject("TacticalDisplayPlayModeTestAdapter");
        isolatedAdapterObject.SetActive(false);
        cleanupObjects.Add(isolatedAdapterObject);

        GameObject host = new GameObject("TacticalDisplayPlayModeTestHost", typeof(RectTransform));
        host.transform.SetParent(canvasObject.transform, false);

        Type miniMapType = ResolveType("MiniMapa");
        Component miniMap = host.AddComponent(miniMapType);
        Component adapter = isolatedAdapterObject.AddComponent(
            ResolveType("Hegemonia.RTS.RTSTacticalContactAdapter"));

        yield return null;
        yield return null;

        Transform displayRoot = canvasObject.transform.Find("Canvas_MiniMapa");
        Assert.That(displayRoot, Is.Not.Null, "O Tactical Display deve permanecer na árvore do Canvas da fixture.");
        Assert.That(displayRoot.Find("MiniMapa_Container/Frame/Inner/MapViewport/ChartBase"), Is.Not.Null);
        Assert.That(displayRoot.Find("MiniMapa_Container/Frame/Inner/MapViewport/MapRecenterArea"), Is.Not.Null);
        Assert.That(displayRoot.Find("MiniMapa_Container/Frame/Inner/Controls/ZoomIn"), Is.Not.Null);
        Assert.That(displayRoot.Find("MiniMapa_Container/Frame/Inner/Controls/ZoomOut"), Is.Not.Null);
        Assert.That(displayRoot.gameObject.activeSelf, Is.False,
            "O minimapa começa oculto e aparece pelo ciclo compartilhado do atalho N.");
        Assert.That(displayRoot.GetComponentsInChildren<Camera>(true), Is.Empty,
            "A interface não deve criar câmera para renderizar unidades ocultas.");

        RectTransform panel = (RectTransform)miniMapType.GetField("_panelRect", Members).GetValue(miniMap);
        Assert.That(panel.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(panel.anchorMax, Is.EqualTo(Vector2.zero));
        Assert.That(panel.pivot, Is.EqualTo(Vector2.zero));
        Assert.That(panel.sizeDelta.x, Is.InRange(160f, 420f));
        Assert.That(panel.sizeDelta.y, Is.InRange(150f, 360f));

        InvokePrivate(miniMap, "DefinirVisibilidadePeloAtalho", true);
        Assert.That(displayRoot.gameObject.activeSelf, Is.True,
            "O ciclo de HUD deve conseguir exibir o minimapa.");
        Type menuGovernoType = ResolveType("MenuGoverno");
        FieldInfo estadoMenuGoverno = menuGovernoType.GetField("EstaAberto", StaticMembers);
        bool governoAbertoAntes = (bool)estadoMenuGoverno.GetValue(null);
        estadoMenuGoverno.SetValue(null, true);
        InvokePrivate(miniMap, "SuprimirPorMenuComando", false);
        Assert.That(displayRoot.gameObject.activeSelf, Is.True,
            "O Tactical Display pode permanecer aberto junto ao menu Governo.");
        estadoMenuGoverno.SetValue(null, governoAbertoAntes);
        InvokePrivate(miniMap, "SuprimirPorMenuComando", true);
        Assert.That(displayRoot.gameObject.activeSelf, Is.False,
            "O mapa deve parar de receber cliques quando outro menu modal estiver aberto.");
        InvokePrivate(miniMap, "SuprimirPorMenuComando", false);
        Assert.That(displayRoot.gameObject.activeSelf, Is.True,
            "Fechar o menu deve restaurar o mapa sem desativar o componente.");

        Transform zoomInTransform = displayRoot.Find("MiniMapa_Container/Frame/Inner/Controls/ZoomIn");
        Type buttonType = ResolveType("UnityEngine.UI.Button");
        Component zoomButton = zoomInTransform.GetComponent(buttonType);
        object clickEvent = buttonType.GetProperty("onClick").GetValue(zoomButton, null);
        float beforeZoom = (float)miniMapType.GetField("_viewWidthMeters", Members).GetValue(miniMap);
        clickEvent.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(clickEvent, null);
        float afterZoom = (float)miniMapType.GetField("_viewWidthMeters", Members).GetValue(miniMap);
        Assert.That(afterZoom, Is.LessThan(beforeZoom), "O controle + deve aproximar o Tactical Display.");

        // The adapter stays inactive and outside the singleton. Its public
        // report API and isolated reset handler can be exercised without
        // clearing the runtime adapter's contacts or subscribing global events.
        InvokePrivate(miniMap, "UnbindContactAdapter");
        miniMapType.GetField("_contactAdapter", Members).SetValue(miniMap, adapter);
        miniMapType.GetField("_nextAdapterResolveAt", Members)
            .SetValue(miniMap, Time.unscaledTime + 60f);
        SuppressGlobalRegistryRead(miniMap, miniMapType);

        int observerTeam = (int)miniMapType.GetField("_playerTeamId", Members).GetValue(miniMap);
        ReportVisibilityContact(adapter, observerTeam, 710001, observerTeam + 90, "Radar", new Vector3(400f, 0f, 600f));
        ReportVisibilityContact(adapter, observerTeam + 10, 710002, observerTeam + 11,
            "DirectVision", new Vector3(500f, 0f, 700f));

        object radarSnapshot = TryGetSnapshot(adapter, observerTeam, "TAC-000001");
        Assert.That(radarSnapshot, Is.Not.Null);
        AssertSnapshotField(radarSnapshot, "IdentityRevealed", false);
        AssertSnapshotField(radarSnapshot, "Relation", "Unknown");
        AssertSnapshotField(radarSnapshot, "ThreatState", "Unknown");
        AssertSnapshotField(radarSnapshot, "TargetInstanceId", 0);
        AssertSnapshotField(radarSnapshot, "TargetTeamId", 0);
        AssertSnapshotField(radarSnapshot, "IdentificationLabel", string.Empty);

        object identifiedSnapshot = TryGetSnapshot(adapter, observerTeam + 10, "TAC-000002");
        Assert.That(identifiedSnapshot, Is.Not.Null);
        AssertSnapshotField(identifiedSnapshot, "IdentityRevealed", true);
        AssertSnapshotField(identifiedSnapshot, "TargetInstanceId", 710002);
        AssertSnapshotField(identifiedSnapshot, "TargetTeamId", observerTeam + 11);

        ReportE3SensorInformation(adapter, observerTeam, 720003, observerTeam + 91);
        object sharedSensorSnapshot = TryGetSnapshot(adapter, observerTeam, "TAC-000003");
        Assert.That(sharedSensorSnapshot, Is.Not.Null);
        AssertSnapshotField(sharedSensorSnapshot, "IdentityRevealed", false);
        AssertSnapshotField(sharedSensorSnapshot, "Relation", "Unknown");
        AssertSnapshotField(sharedSensorSnapshot, "ThreatState", "Unknown");
        AssertSnapshotField(sharedSensorSnapshot, "TargetInstanceId", 0);
        AssertSnapshotField(sharedSensorSnapshot, "TargetTeamId", 0);
        AssertSnapshotField(sharedSensorSnapshot, "IdentificationLabel", string.Empty);
        Assert.That(ContainsText(displayRoot, "E3_PRIVATE_SENSOR_LABEL"), Is.False,
            "Uma etiqueta interna do relatório E-3 não pode aparecer para contato anônimo.");

        ReportAnonymousContact(adapter, observerTeam, 880001, new Vector3(100f, 0f, 200f),
            new Vector3(8f, 0f, 1f), 0.45f, 0.25f, 6f, PrivateOriginSentinel);
        string staleTrackId = "TAC-000004";
        ReportAnonymousContact(adapter, observerTeam, 880002, new Vector3(300f, 0f, 500f),
            Vector3.zero, 0.45f, 0.25f, 6f, PrivateOriginSentinel);
        string lastKnownTrackId = "TAC-000005";

        ReportAnonymousContact(adapter, observerTeam, 880003, new Vector3(650f, 0f, 750f),
            Vector3.zero, 0.7f, 0.25f, 6f, PrivateOriginSentinel,
            "Hostile", "Identified", "Suspected",
            affiliationConfirmedByIndependentEvidence: true);
        const string affiliationTrackId = "TAC-000006";
        object currentAffiliationSnapshot = TryGetSnapshot(adapter, observerTeam, affiliationTrackId);
        Assert.That(currentAffiliationSnapshot, Is.Not.Null);
        AssertSnapshotField(currentAffiliationSnapshot, "IdentityRevealed", false);
        AssertSnapshotField(currentAffiliationSnapshot, "Relation", "Hostile");

        yield return new WaitForSecondsRealtime(0.35f);
        ForceDisplayRefresh(miniMap, miniMapType);

        object staleSnapshot = TryGetSnapshot(adapter, observerTeam, staleTrackId);
        Assert.That(staleSnapshot, Is.Not.Null);
        AssertSnapshotField(staleSnapshot, "State", "Estimated");
        AssertSnapshotField(staleSnapshot, "PositionIsEstimated", true);
        AssertSnapshotField(staleSnapshot, "HasPosition", true);
        Vector3 estimatedPosition = (Vector3)staleSnapshot.GetType().GetField("Position", Members).GetValue(staleSnapshot);
        Assert.That(Vector3.Distance(estimatedPosition, new Vector3(100f, 0f, 200f)), Is.GreaterThan(0.1f),
            "Um contato vencido com velocidade deve usar extrapolação, não posição viva do alvo.");

        object lastKnownSnapshot = TryGetSnapshot(adapter, observerTeam, lastKnownTrackId);
        Assert.That(lastKnownSnapshot, Is.Not.Null);
        AssertSnapshotField(lastKnownSnapshot, "State", "LastKnown");
        AssertSnapshotField(lastKnownSnapshot, "HasLastKnownPosition", true);
        Vector3 lastKnownPosition = (Vector3)lastKnownSnapshot.GetType().GetField("Position", Members).GetValue(lastKnownSnapshot);
        Assert.That(lastKnownPosition, Is.EqualTo(new Vector3(300f, 0f, 500f)));

        object expiredAffiliationSnapshot = TryGetSnapshot(adapter, observerTeam, affiliationTrackId);
        Assert.That(expiredAffiliationSnapshot, Is.Not.Null);
        AssertSnapshotField(expiredAffiliationSnapshot, "State", "LastKnown");
        AssertSnapshotField(expiredAffiliationSnapshot, "Relation", "Unknown");
        AssertSnapshotField(expiredAffiliationSnapshot, "Identification", "Unknown");
        AssertSnapshotField(expiredAffiliationSnapshot, "ThreatState", "Unknown");
        ForceDisplayRefresh(miniMap, miniMapType);
        IList markerData = (IList)miniMapType.GetField("_markers", Members).GetValue(miniMap);
        int expiredMarkerIndex = FindMarkerIndex(markerData, affiliationTrackId);
        Assert.That(expiredMarkerIndex, Is.GreaterThanOrEqualTo(0));
        object expiredMarker = markerData[expiredMarkerIndex];
        object expiredMarkerSnapshot = expiredMarker.GetType().GetField("contact", Members)
            .GetValue(expiredMarker);
        AssertSnapshotField(expiredMarkerSnapshot, "Relation", "Unknown");
        InvokePrivate(miniMap, "SelectMarker", expiredMarkerIndex);
        ForceDisplayRefresh(miniMap, miniMapType);
        AssertMarkerUsesUnknownStyle(miniMap, miniMapType, expiredMarkerIndex);
        Assert.That(ContainsText(displayRoot, "HOSTIL"), Is.False,
            "O painel não deve manter hostilidade de um contato fora da validade.");

        ReportE3Contact(adapter, observerTeam, 730007, observerTeam + 90,
            "UnidadeInimiga", "E3_PRIVATE_CLASSIFIED_NAME", 5f);
        const string e3UnknownTrackId = "TAC-000007";
        object e3UnknownSnapshot = TryGetSnapshot(adapter, observerTeam, e3UnknownTrackId);
        Assert.That(e3UnknownSnapshot, Is.Not.Null);
        AssertSnapshotField(e3UnknownSnapshot, "State", "Current");
        AssertSnapshotField(e3UnknownSnapshot, "IdentityRevealed", false);
        AssertSnapshotField(e3UnknownSnapshot, "Relation", "Unknown");
        AssertSnapshotField(e3UnknownSnapshot, "Identification", "Probable");
        AssertSnapshotField(e3UnknownSnapshot, "ThreatState", "Unknown");
        AssertSnapshotField(e3UnknownSnapshot, "Category", "Unknown");
        AssertSnapshotField(e3UnknownSnapshot, "TargetInstanceId", 0);
        AssertSnapshotField(e3UnknownSnapshot, "TargetTeamId", 0);
        AssertSnapshotField(e3UnknownSnapshot, "IdentificationLabel", string.Empty);
        ForceDisplayRefresh(miniMap, miniMapType);
        IList e3MarkerData = (IList)miniMapType.GetField("_markers", Members).GetValue(miniMap);
        int e3UnknownMarkerIndex = FindMarkerIndex(e3MarkerData, e3UnknownTrackId);
        Assert.That(e3UnknownMarkerIndex, Is.GreaterThanOrEqualTo(0));
        AssertMarkerUsesUnknownStyle(miniMap, miniMapType, e3UnknownMarkerIndex);
        IList e3MarkerViews = (IList)miniMapType.GetField("_markerViews", Members).GetValue(miniMap);
        object e3MarkerView = e3MarkerViews[e3UnknownMarkerIndex];
        Component e3MarkerGlyph = (Component)e3MarkerView.GetType().GetField("glyph", Members)
            .GetValue(e3MarkerView);
        Assert.That(e3MarkerGlyph.GetType().GetProperty("text").GetValue(e3MarkerGlyph, null),
            Is.EqualTo("◇"), "A classificação interna UnidadeInimiga não deve criar símbolo de inimigo.");
        InvokePrivate(miniMap, "SelectMarker", e3UnknownMarkerIndex);
        ForceDisplayRefresh(miniMap, miniMapType);
        Assert.That(ContainsText(displayRoot, "E3_PRIVATE_CLASSIFIED_NAME"), Is.False);
        Assert.That(ContainsText(displayRoot, "HOSTIL"), Is.False);

        ReportE3Contact(adapter, observerTeam, 730008, observerTeam + 91,
            "LancamentoDeMissil", "E3_PRIVATE_MISSILE_ORIGIN", 5f);
        object e3MissileSnapshot = TryGetSnapshot(adapter, observerTeam, "TAC-000008");
        Assert.That(e3MissileSnapshot, Is.Not.Null);
        AssertSnapshotField(e3MissileSnapshot, "IdentityRevealed", false);
        AssertSnapshotField(e3MissileSnapshot, "Relation", "Unknown");
        AssertSnapshotField(e3MissileSnapshot, "ThreatState", "Confirmed");
        AssertSnapshotField(e3MissileSnapshot, "Category", "Missile");
        AssertSnapshotField(e3MissileSnapshot, "TargetInstanceId", 0);
        AssertSnapshotField(e3MissileSnapshot, "TargetTeamId", 0);
        AssertSnapshotField(e3MissileSnapshot, "IdentificationLabel", string.Empty);

        for (int i = 0; i < AnonymousContactBurst; i++)
            ReportAnonymousContact(adapter, observerTeam, 890000 + i, Vector3.zero,
                Vector3.zero, 0.45f, 5f, 5f, PrivateOriginSentinel);
        ReportAnonymousContact(adapter, observerTeam + 20, 990001, Vector3.zero,
            Vector3.zero, 0.45f, 5f, 5f, PrivateOriginSentinel);

        ForceDisplayRefresh(miniMap, miniMapType);
        IList contactSnapshots = (IList)miniMapType.GetField("_contactSnapshots", Members).GetValue(miniMap);
        Assert.That(contactSnapshots.Count, Is.EqualTo(AnonymousContactBurst + 7),
            "A UI deve copiar apenas contatos do time observador atual.");
        Assert.That(miniMapType.GetField("_contactAdapter", Members).GetValue(miniMap), Is.SameAs(adapter),
            "O minimapa deve continuar consumindo o adapter isolado, não o singleton global.");

        Transform mapViewport = displayRoot.Find("MiniMapa_Container/Frame/Inner/MapViewport");
        Assert.That(mapViewport.Find("ContactMarker_0"), Is.Not.Null,
            "O contato anônimo deve ser apresentado como símbolo tático.");
        IList markerPool = (IList)miniMapType.GetField("_markerViews", Members).GetValue(miniMap);
        Assert.That(markerPool.Count, Is.EqualTo(256),
            "O Tactical Display deve limitar o pool visual mesmo quando recebe mais contatos.");

        InvokePrivate(miniMap, "SelectMarker", 0);
        Assert.That((bool)miniMapType.GetField("_hasSelectedContact", Members).GetValue(miniMap), Is.True);
        Assert.That((bool)miniMapType.GetField("_hasSelectedUnit", Members).GetValue(miniMap), Is.False);
        object selectedSnapshot = miniMapType.GetField("_selectedContact", Members).GetValue(miniMap);
        AssertSnapshotField(selectedSnapshot, "IdentityRevealed", false);
        AssertSnapshotField(selectedSnapshot, "Relation", "Unknown");
        AssertSnapshotField(selectedSnapshot, "ThreatState", "Unknown");
        AssertSnapshotField(selectedSnapshot, "TargetInstanceId", 0);
        AssertSnapshotField(selectedSnapshot, "TargetTeamId", 0);

        Assert.That(ContainsText(displayRoot, PrivateOriginSentinel), Is.False,
            "A origem interna do sensor não pode aparecer na interface.");
        Assert.That(ContainsText(displayRoot, "710001"), Is.False,
            "A seleção de um track anônimo não pode mostrar o ID interno do alvo.");
        Assert.That(ContainsText(displayRoot, "HOSTIL"), Is.False,
            "A interface não pode inferir hostilidade a partir da equipe interna do radar.");

        // This calls the scene-reset handler only on the isolated, inactive
        // adapter. It does not claim to simulate Unity's sceneLoaded event.
        InvokeSingleSceneResetHandlerOnIsolatedAdapter(adapter);
        Assert.That((int)adapter.GetType().GetProperty("ContactCount", Members).GetValue(adapter, null), Is.Zero);
        ForceDisplayRefresh(miniMap, miniMapType);
        IList markersAfterReset = (IList)miniMapType.GetField("_markers", Members).GetValue(miniMap);
        Assert.That(markersAfterReset.Count, Is.Zero);
        Assert.That((bool)miniMapType.GetField("_hasSelectedContact", Members).GetValue(miniMap), Is.False);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int i = cleanupObjects.Count - 1; i >= 0; i--)
        {
            if (cleanupObjects[i] != null)
                UnityEngine.Object.Destroy(cleanupObjects[i]);
        }
        cleanupObjects.Clear();

        if (legacyMiniMapCamera != null)
        {
            legacyMiniMapCamera.targetTexture = legacyCameraTarget;
            legacyMiniMapCamera.enabled = legacyCameraWasEnabled;
        }
        legacyMiniMapCamera = null;
        legacyCameraTarget = null;
        yield return null;
    }

    private void CaptureLegacyCameraState()
    {
        GameObject legacyCameraObject = GameObject.Find("Cam_MiniMapa");
        legacyMiniMapCamera = legacyCameraObject != null ? legacyCameraObject.GetComponent<Camera>() : null;
        if (legacyMiniMapCamera == null) return;
        legacyCameraWasEnabled = legacyMiniMapCamera.enabled;
        legacyCameraTarget = legacyMiniMapCamera.targetTexture;
    }

    private static void SuppressGlobalRegistryRead(Component miniMap, Type miniMapType)
    {
        IList registeredUnits = (IList)miniMapType.GetField("_registeredUnits", Members).GetValue(miniMap);
        registeredUnits.Clear();
        Type registryType = ResolveType("RegistroEntidadesJogo");
        int registryVersion = (int)registryType.GetProperty("Version", StaticMembers).GetValue(null, null);
        miniMapType.GetField("_lastRegistryVersion", Members).SetValue(miniMap, registryVersion);
        miniMapType.GetField("_nextReconcileAt", Members).SetValue(miniMap, Time.unscaledTime + 60f);
    }

    private static void ForceDisplayRefresh(Component miniMap, Type miniMapType)
    {
        SuppressGlobalRegistryRead(miniMap, miniMapType);
        miniMapType.GetField("_nextRefreshAt", Members).SetValue(miniMap, 0f);
        InvokePrivate(miniMap, "LateUpdate");
    }

    private static void ReportVisibilityContact(Component adapter, int observerTeam,
        int targetInstanceId, int targetTeamId, string source, Vector3 position)
    {
        Type contactType = ResolveType("Hegemonia.RTS.RTSVisibilityContact");
        object contact = Activator.CreateInstance(contactType);
        float now = Time.unscaledTime;
        SetPublicField(contact, "observerTeamId", observerTeam);
        SetPublicField(contact, "targetInstanceId", targetInstanceId);
        SetPublicField(contact, "targetTeamId", targetTeamId);
        SetPublicField(contact, "targetSceneHandle", SceneManager.GetActiveScene().handle);
        SetPublicField(contact, "lastKnownPosition", position);
        SetPublicField(contact, "lastSeenAt", now);
        SetPublicField(contact, "expiresAt", now + 5f);
        SetPublicField(contact, "lastKnownExpiresAt", now + 30f);
        SetPublicField(contact, "source", Enum.Parse(ResolveType("Hegemonia.RTS.RTSDetectionSource"), source));
        SetPublicField(contact, "currentlyVisible", true);
        InvokePrivate(adapter, "AoReceberContatoVisibilidade", contact);
    }

    private static void ReportAnonymousContact(Component adapter, int observerTeam, int sensorContactId,
        Vector3 estimatedPosition, Vector3 estimatedVelocity, float confidence, float validForSeconds,
        float retentionSeconds, string origin, string relation = "Unknown",
        string identification = "Unknown", string threatState = "Unknown",
        bool affiliationConfirmedByIndependentEvidence = false)
    {
        Type categoryType = ResolveType("Hegemonia.RTS.RTSTacticalContactCategory");
        Type sourceType = ResolveType("Hegemonia.RTS.RTSDetectionSource");
        Type relationType = ResolveType("Hegemonia.RTS.RTSTacticalContactRelation");
        Type identificationType = ResolveType("Hegemonia.RTS.RTSTacticalContactIdentification");
        Type threatType = ResolveType("Hegemonia.RTS.RTSTacticalContactThreatState");
        Type verticalType = ResolveType("Hegemonia.RTS.RTSTacticalVerticalKind");
        MethodInfo report = adapter.GetType().GetMethod("ReportAnonymousContact", Members);
        Assert.That(report, Is.Not.Null);

        object[] arguments =
        {
            observerTeam,
            sensorContactId,
            Enum.Parse(categoryType, "Naval"),
            Enum.Parse(sourceType, "Radar"),
            estimatedPosition,
            estimatedVelocity,
            confidence,
            validForSeconds,
            retentionSeconds,
            160f,
            origin,
            Enum.Parse(relationType, relation),
            Enum.Parse(identificationType, identification),
            Enum.Parse(threatType, threatState),
            Enum.Parse(verticalType, "Unknown"),
            0f,
            true,
            affiliationConfirmedByIndependentEvidence
        };

        Assert.That((bool)report.Invoke(adapter, arguments), Is.True);
    }

    private static int FindMarkerIndex(IList markers, string contactId)
    {
        for (int i = 0; i < markers.Count; i++)
        {
            object marker = markers[i];
            object snapshot = marker.GetType().GetField("contact", Members).GetValue(marker);
            if (snapshot != null
                && string.Equals((string)snapshot.GetType().GetField("ContactId", Members).GetValue(snapshot),
                    contactId, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    private static void ReportE3SensorInformation(Component adapter, int observerTeam,
        int targetInstanceId, int targetTeamId)
    {
        ReportE3Contact(adapter, observerTeam, targetInstanceId, targetTeamId,
            "InformacaoDeSensor", "E3_PRIVATE_SENSOR_LABEL", 5f);
    }

    private static void ReportE3Contact(Component adapter, int observerTeam,
        int targetInstanceId, int targetTeamId, string contactKind, string label, float validForSeconds)
    {
        Type contactType = ResolveType("BoeingE3Reconhecimento+ContatoReconhecimento");
        Type contactKindType = ResolveType("BoeingE3Reconhecimento+TipoContatoReconhecimento");
        object contact = Activator.CreateInstance(contactType);
        float now = Time.unscaledTime;
        SetPublicField(contact, "idContato", "E3-TEST-SENSOR");
        SetPublicField(contact, "equipeObservadora", observerTeam);
        SetPublicField(contact, "idAlvo", targetInstanceId);
        SetPublicField(contact, "equipeAlvo", targetTeamId);
        SetPublicField(contact, "nomeAlvo", label);
        SetPublicField(contact, "tipo", Enum.Parse(contactKindType, contactKind));
        SetPublicField(contact, "ultimaPosicaoConhecida", new Vector3(800f, 0f, 900f));
        SetPublicField(contact, "ultimaAtualizacao", now);
        SetPublicField(contact, "validadeAte", now + validForSeconds);
        SetPublicField(contact, "retencaoAte", now + validForSeconds + 30f);
        SetPublicField(contact, "direcao", Vector3.zero);
        SetPublicField(contact, "velocidade", 0f);
        SetPublicField(contact, "fonte", PrivateOriginSentinel);
        InvokePrivate(adapter, "AoReceberContatoE3", contact);
    }

    private static void AssertMarkerUsesUnknownStyle(Component miniMap, Type miniMapType, int markerIndex)
    {
        IList markerViews = (IList)miniMapType.GetField("_markerViews", Members).GetValue(miniMap);
        object view = markerViews[markerIndex];
        Component background = (Component)view.GetType().GetField("background", Members).GetValue(view);
        Component glyph = (Component)view.GetType().GetField("glyph", Members).GetValue(view);
        Color actualBackground = (Color)background.GetType().GetProperty("color").GetValue(background, null);
        Color actualGlyph = (Color)glyph.GetType().GetProperty("color").GetValue(glyph, null);
        Color unknown = (Color)miniMapType.GetField("ColorUnknown", StaticMembers).GetValue(null);
        unknown.a = 0.62f;
        Color expectedBackground = new Color(unknown.r * 0.28f, unknown.g * 0.28f,
            unknown.b * 0.28f, 0.96f);
        Assert.That(actualGlyph, Is.EqualTo(unknown),
            "A view reutilizada precisa reaplicar a cor de contato desconhecido.");
        Assert.That(actualBackground, Is.EqualTo(expectedBackground),
            "O fundo da view reutilizada não pode conservar a cor hostil anterior.");
    }

    private static object TryGetSnapshot(Component adapter, int observerTeam, string contactId)
    {
        MethodInfo tryGet = adapter.GetType().GetMethod("TryObterContato", Members);
        object[] arguments = { observerTeam, contactId, null };
        bool found = (bool)tryGet.Invoke(adapter, arguments);
        return found ? arguments[2] : null;
    }

    private static void AssertSnapshotField(object snapshot, string fieldName, object expected)
    {
        object actual = snapshot.GetType().GetField(fieldName, Members).GetValue(snapshot);
        if (actual != null && actual.GetType().IsEnum && expected is string enumName)
            Assert.That(actual.ToString(), Is.EqualTo(enumName), fieldName);
        else
            Assert.That(actual, Is.EqualTo(expected), fieldName);
    }

    private static void InvokeSingleSceneResetHandlerOnIsolatedAdapter(Component adapter)
    {
        MethodInfo onSceneLoaded = adapter.GetType().GetMethod("OnSceneLoaded", Members);
        Assert.That(onSceneLoaded, Is.Not.Null);
        onSceneLoaded.Invoke(adapter, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
    }

    private static void SetPublicField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public).SetValue(target, value);
    }

    private static void InvokePrivate(Component target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, Members);
        Assert.That(method, Is.Not.Null, "Método ausente: " + methodName);
        method.Invoke(target, arguments);
    }

    private static bool ContainsText(Transform root, string value)
    {
        Type textType = ResolveType("UnityEngine.UI.Text");
        Component[] components = root.GetComponentsInChildren<Component>(true);
        PropertyInfo textProperty = textType.GetProperty("text");
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component.GetType() != textType)
                continue;
            string text = textProperty.GetValue(component, null) as string;
            if (!string.IsNullOrEmpty(text) && text.IndexOf(value, StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    private static Type ResolveType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null)
                return type;
        }
        throw new TypeLoadException("Não foi possível resolver " + fullName);
    }
}
