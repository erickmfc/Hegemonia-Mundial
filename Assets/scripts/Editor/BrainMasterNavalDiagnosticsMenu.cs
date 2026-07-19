using System.Text;
using Hegemonia.AI.BrainMaster;
using Hegemonia.AI.Shared;
using UnityEditor;
using UnityEngine;

public static class BrainMasterNavalDiagnosticsMenu
{
    [MenuItem("Tools/Diagnostics/BrainMaster/Dump Naval Report")]
    private static void DumpNavalReport()
    {
        IA_BrainMaster[] brains = IA_UnitySearch.FindAll<IA_BrainMaster>();
        int dumped = 0;
        for (int i = 0; i < brains.Length; i++)
        {
            IA_BrainMaster brain = brains[i];
            if (brain == null || !IA_NavalBuildDiagnostics.HasReport(brain))
            {
                continue;
            }

            dumped++;
            Debug.Log(IA_NavalBuildDiagnostics.BuildReport(brain), brain);
        }

        if (dumped == 0)
        {
            Debug.LogWarning("[IA_NavalDiagnostics] Nenhum relatorio naval ativo para despejar.");
        }
    }

    [MenuItem("Tools/Diagnostics/BrainMaster/Clear Naval Reports")]
    private static void ClearNavalReports()
    {
        IA_NavalBuildDiagnostics.ClearAll();
        Debug.Log("[IA_NavalDiagnostics] Relatorios navais limpos.");
    }

    [MenuItem("Tools/Diagnostics/BrainMaster/Test Shipyard At Selection")]
    private static void TestShipyardAtSelection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[IA_NavalDiagnostics] Entre em Play Mode para testar o estaleiro no ponto selecionado.");
            return;
        }

        Transform selected = Selection.activeTransform;
        if (selected == null)
        {
            Debug.LogWarning("[IA_NavalDiagnostics] Selecione um objeto ou marcador na cena para testar o estaleiro.");
            return;
        }

        IA_BrainMaster brain = ResolveBrain(selected);
        if (brain == null || brain.Context == null)
        {
            Debug.LogWarning("[IA_NavalDiagnostics] Nenhum IA_BrainMaster ativo encontrado para executar o teste.");
            return;
        }

        if (!TryResolveShipyard(brain, out string itemKey, out DadosConstrucao data))
        {
            Debug.LogWarning("[IA_NavalDiagnostics] Estaleiro naval nao encontrado no catalogo da BrainMaster.", brain);
            return;
        }

        Vector3 selectedPoint = selected.position;
        Vector3 resolvedPoint = selectedPoint;
        Quaternion resolvedRotation = Quaternion.identity;
        string poseReason = string.Empty;
        bool poseOk = false;
        NavalPlacementResolver.StructurePose pose;
        if (NavalPlacementResolver.TryResolveStructurePose(data.prefabDaUnidade, selectedPoint, resolvedRotation, out pose))
        {
            poseOk = true;
            resolvedPoint = pose.Position;
            resolvedRotation = pose.Rotation;
            poseReason = string.IsNullOrEmpty(pose.Reason) ? "ok" : pose.Reason;
        }
        else
        {
            poseReason = string.IsNullOrEmpty(pose.Reason) ? "costa invalida" : pose.Reason;
        }

        string territoryReason = string.Empty;
        bool territoryOk = poseOk && brain.Context.Backend.BuildService.ValidateTerritoryProbe(itemKey, resolvedPoint, out territoryReason);

        string placementReason = string.Empty;
        bool placementOk = poseOk
                           && territoryOk
                           && brain.Context.Backend.BuildService.ValidatePlacement(
                               itemKey,
                               resolvedPoint,
                               resolvedRotation,
                               IA_ZoneType.Naval,
                               brain.Context.WorldState,
                               brain.Context.MapAnalyzer,
                               brain.Context.ThreatAnalyzer,
                               out placementReason);
        if (!poseOk)
        {
            territoryReason = "pose falhou";
            placementReason = "pose falhou";
        }
        else if (!territoryOk)
        {
            placementReason = "territorio falhou";
        }

        int owner = GerenteDeTerritorio.Instancia != null ? GerenteDeTerritorio.Instancia.ObterDonoDoPonto(resolvedPoint) : -1;
        IA_MapCell cell = brain.Context.MapAnalyzer.SampleCell(selectedPoint);
        ClassificacaoSuperficieMapa surface;
        float markedHeight;
        bool hasSurface = RegistroSuperficieMapa.TryClassify(selectedPoint, out surface, out markedHeight);

        IA_NavalBuildDiagnostics.Begin(brain, "Teste manual de estaleiro", "selection=" + selected.name);
        IA_NavalBuildDiagnostics.AddPoint(brain, selectedPoint, "ponto selecionado", Color.yellow, 4.5f);
        if (poseOk)
        {
            IA_NavalBuildDiagnostics.AddPoint(brain, resolvedPoint, "pose resolvida", territoryOk && placementOk ? Color.green : Color.cyan, 4.2f);
        }
        else
        {
            IA_NavalBuildDiagnostics.AddPoint(brain, selectedPoint, "pose falhou: " + poseReason, Color.red, 5f);
        }

        IA_NavalBuildDiagnostics.AddLine(brain, "item=" + itemKey);
        IA_NavalBuildDiagnostics.AddLine(brain, "selecionado=" + selectedPoint);
        IA_NavalBuildDiagnostics.AddLine(brain, "pose=" + (poseOk ? "ok" : "falhou") + " | motivo=" + poseReason);
        if (poseOk)
        {
            IA_NavalBuildDiagnostics.AddLine(brain, "resolvido=" + resolvedPoint);
        }

        IA_NavalBuildDiagnostics.AddLine(
            brain,
            "superficie=" + (hasSurface ? surface.ToString() + "@" + markedHeight.ToString("0.0") : "sem marcador")
            + " | cell=" + (cell != null ? cell.Terrain.ToString() + " | buildable=" + cell.BuildableLand : "null"));
        IA_NavalBuildDiagnostics.AddLine(brain, "territorio=" + (territoryOk ? "ok" : "falhou") + " | owner=" + owner + " | motivo=" + territoryReason);
        IA_NavalBuildDiagnostics.AddLine(brain, "placement=" + (placementOk ? "ok" : "falhou") + " | motivo=" + placementReason);

        StringBuilder builder = new StringBuilder();
        builder.Append("[IA_NavalDiagnostics] Teste manual de estaleiro").AppendLine();
        builder.Append("Selecao: ").Append(selected.name).Append(" @ ").Append(selectedPoint.ToString("F2")).AppendLine();
        builder.Append("Item: ").Append(itemKey).AppendLine();
        builder.Append("Pose: ").Append(poseOk ? "OK" : "FALHOU").Append(" | ").Append(poseReason).AppendLine();
        if (poseOk)
        {
            builder.Append("Pose resolvida: ").Append(resolvedPoint.ToString("F2")).AppendLine();
        }

        builder.Append("Territorio: ").Append(territoryOk ? "OK" : "FALHOU").Append(" | owner=").Append(owner).Append(" | ").Append(territoryReason).AppendLine();
        builder.Append("Placement: ").Append(placementOk ? "OK" : "FALHOU").Append(" | ").Append(placementReason).AppendLine();
        builder.Append("Superficie: ").Append(hasSurface ? surface.ToString() + "@" + markedHeight.ToString("0.0") : "sem marcador").AppendLine();
        builder.Append("Cell: ").Append(cell != null ? cell.Terrain.ToString() + " | buildable=" + cell.BuildableLand : "null");

        Debug.Log(builder.ToString(), selected.gameObject);
        Debug.Log(IA_NavalBuildDiagnostics.BuildReport(brain), brain);
    }

    private static IA_BrainMaster ResolveBrain(Transform selected)
    {
        if (selected != null)
        {
            IA_BrainMaster inParents = selected.GetComponentInParent<IA_BrainMaster>();
            if (inParents != null)
            {
                return inParents;
            }
        }

        return IA_UnitySearch.FindFirst<IA_BrainMaster>();
    }

    private static bool TryResolveShipyard(IA_BrainMaster brain, out string itemKey, out DadosConstrucao data)
    {
        itemKey = string.Empty;
        data = null;
        if (brain == null || brain.Context == null || brain.Context.Backend == null)
        {
            return false;
        }

        string[] keys =
        {
            "Estaleiro Naval",
            "estaleiros navais",
            "estaleiro naval",
            "estaleiro"
        };

        for (int i = 0; i < keys.Length; i++)
        {
            if (brain.Context.Backend.TryResolveItem(keys[i], out data) && data != null)
            {
                itemKey = keys[i];
                return true;
            }
        }

        return false;
    }
}
