#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LayoutConvesPortaAvioesV2))]
public sealed class LayoutConvesPortaAvioesV2Editor : Editor
{
    public override void OnInspectorGUI()
    {
        RepararScriptsDasVagas((LayoutConvesPortaAvioesV2)target);
        DrawDefaultInspector();
        var layout = (LayoutConvesPortaAvioesV2)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Criar estrutura padrão")) { Undo.RecordObject(layout, "Criar estrutura V2"); layout.CriarEstruturaPadrao(); EditorUtility.SetDirty(layout); }
        if (GUILayout.Button("Criar 15 vagas de convés + 60 de hangar")) { Undo.RecordObject(layout, "Criar estacionamento V2"); layout.CriarVagasPadrao(); RepararScriptsDasVagas(layout); EditorUtility.SetDirty(layout); }
        if (GUILayout.Button("Reparar scripts das vagas")) RepararScriptsDasVagas(layout);
        if (GUILayout.Button("Validar layout")) { bool ok = layout.ValidarLayout(); Debug.Log(ok ? "[PortaAvioes V2] Layout válido." : "[PortaAvioes V2] Layout possui problemas.", layout); if (!ok && layout.UltimosErros != null) foreach (string erro in layout.UltimosErros) Debug.LogWarning("[PortaAvioes V2] " + erro, layout); }
        if (GUILayout.Button("Desenhar rotas")) layout.DesenharRotas();
        if (GUILayout.Button("Testar reserva de vagas")) layout.TestarReservaDeVagas();
        if (GUILayout.Button("Verificar sobreposição")) layout.VerificarSobreposicao();
        if (GUILayout.Button("Listar pontos ausentes")) layout.ListarPontosAusentes();
        if (GUILayout.Button("Medir bounds do modelo")) MedirBoundsDoModelo(layout);
        if (layout.UltimosErros != null && layout.UltimosErros.Length > 0) foreach (string erro in layout.UltimosErros) EditorGUILayout.HelpBox(erro, MessageType.Warning);
    }

    private static void MedirBoundsDoModelo(LayoutConvesPortaAvioesV2 layout)
    {
        if (layout == null) return;
        Renderer[] renderers = layout.transform.root.GetComponentsInChildren<Renderer>(true);
        Debug.Log("[PortaAvioes V2] Bounds do modelo USS Enterprise:", layout);
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            if (bounds.size.x >= 20f && bounds.size.z >= 20f && bounds.size.y <= 8f)
                Debug.Log($"[PortaAvioes V2] candidato convés: {renderer.name} centerY={bounds.center.y:F3} minY={bounds.min.y:F3} maxY={bounds.max.y:F3} size={bounds.size}", renderer);
        }
    }

    private static void RepararScriptsDasVagas(LayoutConvesPortaAvioesV2 layout)
    {
        if (layout == null) return;
        int reparadas = 0;
        Transform[] grupos = { layout.vagasExternas, layout.vagasInternas };
        for (int g = 0; g < grupos.Length; g++)
        {
            Transform grupo = grupos[g];
            if (grupo == null) continue;
            foreach (Transform filho in grupo)
            {
                if (filho == null) continue;
                LayoutConvesPortaAvioesV2 layoutNaVaga = filho.GetComponent<LayoutConvesPortaAvioesV2>();
                if (layoutNaVaga != null)
                {
                    Object.DestroyImmediate(layoutNaVaga, true);
                    reparadas++;
                }
                VagaPortaAvioesV2 vaga = filho.GetComponent<VagaPortaAvioesV2>();
                if (vaga == null)
                {
                    vaga = filho.gameObject.AddComponent<VagaPortaAvioesV2>();
                    reparadas++;
                }
                SerializedObject serialized = new SerializedObject(vaga);
                SerializedProperty script = serialized.FindProperty("m_Script");
                MonoScript scriptCorreto = MonoScript.FromMonoBehaviour(vaga);
                if (script != null && script.objectReferenceValue != scriptCorreto)
                {
                    script.objectReferenceValue = scriptCorreto;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    reparadas++;
                }
                EditorUtility.SetDirty(vaga);
            }
        }
        EditorUtility.SetDirty(layout);
        Debug.Log($"[PortaAvioes V2] Referências de script reparadas: {reparadas}.", layout);
    }

    [MenuItem("Tools/Porta-aviões V2/Configurar USS Enterprise")]
    public static void ConfigurarUssEnterprise()
    {
        const string caminho = "Assets/Prefabs/Navios_Guerra/Porta avioes/Uss Enterprise.prefab";
        GameObject raiz = PrefabUtility.LoadPrefabContents(caminho);
        try
        {
            LayoutConvesPortaAvioesV2 layout = raiz.GetComponentInChildren<LayoutConvesPortaAvioesV2>(true);
            if (layout == null) throw new System.InvalidOperationException("O USS Enterprise não possui LayoutConvesPortaAvioesV2.");

            layout.CriarVagasPadrao();
            Transform defesa = layout.transform.Find("DefesaAntiaerea") ?? new GameObject("DefesaAntiaerea").transform;
            if (defesa.parent != layout.transform) defesa.SetParent(layout.transform, false);
            defesa.localPosition = new Vector3(0f, 15.81f, 0f);
            defesa.localRotation = Quaternion.identity;
            defesa.localScale = Vector3.one;

            const string nomeTorreta = "Torreta_AA_Sovereign";
            if (layout.transform.Find(nomeTorreta) == null && defesa.Find(nomeTorreta) == null)
            {
                const string caminhoTorreta = "Assets/Prefabs/Navios_Guerra/Porta avioes/torreta esquerda.prefab";
                GameObject prefabTorreta = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoTorreta);
                if (prefabTorreta == null) throw new System.InvalidOperationException("Prefab da torreta do Sovereign não encontrado.");
                GameObject torreta = (GameObject)PrefabUtility.InstantiatePrefab(prefabTorreta, defesa);
                torreta.name = nomeTorreta;
                torreta.transform.localPosition = new Vector3(-30f, 0.8f, 30f);
                torreta.transform.localRotation = Quaternion.Euler(-13.37f, 0f, 0f);
                torreta.transform.localScale = Vector3.one * 0.01f;
            }

            layout.AtualizarListas();
            if (!layout.ValidarLayout())
                foreach (string erro in layout.UltimosErros) Debug.LogWarning("[PortaAvioes V2] " + erro, layout);
            EditorUtility.SetDirty(layout);
            PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            AssetDatabase.SaveAssets();
            Debug.Log("[PortaAvioes V2] USS Enterprise configurado: 15 vagas externas, 60 internas e torreta AA do Sovereign.", raiz);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
#endif
