#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LayoutConvesPortaAvioesV2))]
public sealed class LayoutConvesPortaAvioesV2Editor : Editor
{
    public override void OnInspectorGUI()
    {
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
        if (GUILayout.Button("Calibrar pontos no convés real"))
        {
            Undo.RecordObject(layout, "Calibrar pontos do convés");
            CalibrarLayoutAoConves(layout);
            EditorUtility.SetDirty(layout);
        }
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

    private static void CalibrarLayoutAoConves(LayoutConvesPortaAvioesV2 layout)
    {
        if (layout == null) return;
        Bounds convés;
        if (!TryObterBoundsDoConves(layout, out convés))
        {
            Debug.LogWarning("[PortaAvioes V2] Não foi possível localizar o convés real; pontos não foram movidos.", layout);
            return;
        }

        Undo.RecordObject(layout, "Calibrar layout do convés");
        layout.layoutCalibradoManualmente = true;
        layout.eixoComprimentoEhX = convés.size.x >= convés.size.z;
        float comprimento = layout.eixoComprimentoEhX ? convés.size.x : convés.size.z;
        float largura = layout.eixoComprimentoEhX ? convés.size.z : convés.size.x;
        float centroComprimento = layout.eixoComprimentoEhX ? convés.center.x : convés.center.z;
        float centroLateral = layout.eixoComprimentoEhX ? convés.center.z : convés.center.x;
        float convésY = convés.max.y + 0.2f;
        float meioComprimento = Mathf.Max(20f, comprimento * 0.5f - 10f);
        float meioLargura = Mathf.Max(8f, largura * 0.5f - 5f);
        float lateralVaga = Mathf.Max(4f, meioLargura - 2f);
        float faixaComprimento = Mathf.Max(30f, comprimento * 0.36f);
        float inicioVagas = centroComprimento - meioComprimento + 12f;
        // Reserva o setor dianteiro para as três vagas grandes. Sem essa
        // folga a última vaga de caça ficava colada às vagas de transporte.
        float reservaVagasGrandes = Mathf.Max(48f, comprimento * .18f);
        float fimVagas = centroComprimento + meioComprimento - reservaVagasGrandes;
        if (fimVagas <= inicioVagas) fimVagas = inicioVagas + Mathf.Max(12f, comprimento * 0.25f);

        layout.referenciaConves.localPosition = Local(layout, centroComprimento, centroLateral, convésY);
        layout.referenciaConves.localRotation = Quaternion.identity;

        // A pista ocupa o eixo longitudinal real. A aproximação fica fora do
        // casco, mas todos os pontos de toque e frenagem ficam sobre o convés.
        Set(layout.pouso, "Espera_01", Local(layout, centroComprimento - meioComprimento - Mathf.Max(35f, comprimento * .18f), centroLateral, convésY + Mathf.Max(24f, largura * .7f)), Forward(layout, 1f, 0f));
        Set(layout.pouso, "Aproximacao_Longa", Local(layout, centroComprimento - meioComprimento - Mathf.Max(15f, comprimento * .08f), centroLateral, convésY + Mathf.Max(16f, largura * .45f)), Forward(layout, 1f, 0f));
        Set(layout.pouso, "Aproximacao_Media", Local(layout, centroComprimento - meioComprimento + Mathf.Max(2f, comprimento * .015f), centroLateral, convésY + Mathf.Max(9f, largura * .25f)), Forward(layout, 1f, 0f));
        Set(layout.pouso, "Aproximacao_Final", Local(layout, centroComprimento - meioComprimento + Mathf.Max(10f, comprimento * .07f), centroLateral, convésY + 3f), Forward(layout, 1f, 0f));
        Set(layout.pouso, "Toque", Local(layout, centroComprimento - meioComprimento + Mathf.Max(18f, comprimento * .13f), centroLateral, convésY), Forward(layout, 1f, 0f));
        Set(layout.pouso, "Fim_Frenagem", Local(layout, centroComprimento + comprimento * .18f, centroLateral, convésY), Forward(layout, 1f, 0f));
        Set(layout.pouso, "Saida_Pista", Local(layout, centroComprimento + comprimento * .34f, centroLateral + lateralVaga, convésY), Forward(layout, 0f, 1f));

        Set(layout.taxi, "Acesso_Vagas_Esquerda", Local(layout, centroComprimento - comprimento * .18f, centroLateral + lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.taxi, "Acesso_Vagas_Direita", Local(layout, centroComprimento - comprimento * .18f, centroLateral - lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.taxi, "Cruzamento_Esquerda", Local(layout, centroComprimento - comprimento * .42f, centroLateral + lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.taxi, "Cruzamento_Direita", Local(layout, centroComprimento - comprimento * .42f, centroLateral - lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.taxi, "Taxi_01", Local(layout, centroComprimento - comprimento * .05f, centroLateral + lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.taxi, "Taxi_02", Local(layout, centroComprimento + comprimento * .18f, centroLateral - lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.taxi, "Cruzamento_01", Local(layout, centroComprimento, centroLateral, convésY), Forward(layout, 1f, 0f));
        Set(layout.taxi, "Acesso_Vagas", Local(layout, centroComprimento - comprimento * .18f, centroLateral, convésY), Forward(layout, 1f, 0f));

        for (int i = 0; i < layout.vagasConves.Count; i++)
        {
            VagaPortaAvioesV2 vaga = layout.vagasConves[i];
            if (vaga == null) continue;
            bool grande = vaga.tamanhoMaximo >= 20f || vaga.name.IndexOf("Grande", System.StringComparison.OrdinalIgnoreCase) >= 0;
            float lado = grande ? (i % 2 == 0 ? lateralVaga : -lateralVaga) : (i < 6 ? lateralVaga : -lateralVaga);
            float longo;
            if (grande)
            {
                int grandeIndex = Mathf.Max(0, i - 12);
                longo = centroComprimento + meioComprimento - 22f - grandeIndex * Mathf.Max(28f, comprimento * .08f);
            }
            else
            {
                int indice = i < 6 ? i : i - 6;
                longo = Mathf.Lerp(inicioVagas, fimVagas, indice / 5f);
            }
            Set(vaga.transform, Local(layout, longo, centroLateral + lado, convésY), Forward(layout, lado >= 0f ? 1f : -1f, 0f));
            SetChild(vaga.transform, "Entrada", new Vector3(0f, 0f, 8f));
            SetChild(vaga.transform, "Parada", Vector3.zero);
        }

        // Pontos de catapulta, decolagem e circuito aéreo acompanham o mesmo
        // eixo do modelo, evitando objetos fora do navio ou rotas de lado.
        for (int i = 0; i < layout.catapultasLista.Count; i++)
        {
            Transform cat = layout.catapultasLista[i];
            if (cat == null) continue;
            cat.localPosition = Local(layout, centroComprimento + meioComprimento * .62f - i * 12f, centroLateral + (i % 2 == 0 ? -lateralVaga : lateralVaga), convésY);
            cat.localRotation = Quaternion.LookRotation(LocalDirection(layout, 1f, 0f), Vector3.up);
            SetChild(cat, "Fila", LocalDirection(layout, -1f, 0f) * 22f);
            SetChild(cat, "Inicio", LocalDirection(layout, -1f, 0f) * 6f);
            SetChild(cat, "Liberacao", LocalDirection(layout, 1f, 0f) * 8f);
            SetChild(cat, "Subida", LocalDirection(layout, 1f, 0f) * 8f + Vector3.up * 14f);
        }
        Set(layout.decolagem, "Fila", Local(layout, centroComprimento + meioComprimento * .45f, centroLateral - lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.decolagem, "Alinhamento", Local(layout, centroComprimento + meioComprimento * .68f, centroLateral - lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.decolagem, "Liberacao", Local(layout, centroComprimento + meioComprimento * .86f, centroLateral - lateralVaga, convésY), Forward(layout, 1f, 0f));
        Set(layout.decolagem, "Subida_Inicial", Local(layout, centroComprimento + meioComprimento * .86f, centroLateral - lateralVaga, convésY + 14f), Forward(layout, 1f, 0f));
        Set(layout.decolagem, "Saida_Voo", Local(layout, centroComprimento + meioComprimento + 38f, centroLateral - lateralVaga, convésY + 22f), Forward(layout, 1f, 0f));
        Set(layout.voo, "Circuito_01", Local(layout, centroComprimento + meioComprimento + 70f, centroLateral, convésY + 55f), Forward(layout, -1f, 0f));
        Set(layout.voo, "Afastamento_01", Local(layout, centroComprimento + meioComprimento + 110f, centroLateral - largura, convésY + 75f), Forward(layout, -1f, 0f));
        Set(layout.voo, "Subida_Inicial", Local(layout, centroComprimento + meioComprimento + 42f, centroLateral - lateralVaga, convésY + 35f), Forward(layout, 1f, 0f));
        Set(layout.voo, "Ponto_Missao", Local(layout, centroComprimento + meioComprimento + 150f, centroLateral, convésY + 100f), Forward(layout, 1f, 0f));

        // Alinha as referências dos elevadores ao mesmo convés calibrado.
        // O mesh permanece intacto; só os pontos operacionais acompanham a
        // posição real da pista e do hangar.
        for (int i = 0; i < layout.elevadoresLista.Count; i++)
        {
            Transform elevador = layout.elevadoresLista[i];
            if (elevador == null) continue;
            float longo = centroComprimento - meioComprimento * .15f + i * 24f;
            float lateralElevador = i % 2 == 0 ? -meioLargura * .55f : meioLargura * .55f;
            elevador.localPosition = Local(layout, longo, centroLateral + lateralElevador, convésY);
            elevador.localRotation = Quaternion.identity;
            SetChild(elevador, "Plataforma", Vector3.zero);
            SetChild(elevador, "Posicao_Conves", Vector3.zero);
            SetChild(elevador, "Posicao_Baixa", Vector3.down * Mathf.Max(8f, convésY - 8f));
            SetChild(elevador, "Saida_Hangar", LocalDirection(layout, -1f, 0f) * 10f + Vector3.down * 2f);
        }

        layout.AtualizarListas();
        // AtualizarListas garante a lista persistente, mas também reconstrói a
        // grade padrão do hangar. A calibração precisa ser a última operação
        // para que as coordenadas corrigidas permaneçam no prefab.
        CalibrarGradeHangar(layout, centroComprimento, centroLateral, meioComprimento, meioLargura);
        Debug.Log($"[PortaAvioes V2] Convés calibrado: eixo={(layout.eixoComprimentoEhX ? "X" : "Z")}, bounds locais={convés}, vagas de convés={layout.vagasConves.Count}, hangar={layout.vagasHangar.Count}.", layout);
    }

    private static void CalibrarGradeHangar(LayoutConvesPortaAvioesV2 layout, float centroComprimento, float centroLateral, float meioComprimento, float meioLargura)
    {
        if (layout == null || layout.vagasHangar == null) return;
        float hangarInicio = centroComprimento - meioComprimento + 14f;
        float hangarFim = centroComprimento + meioComprimento - 14f;
        float hangarLateral = Mathf.Max(5f, meioLargura - 8f);
        for (int i = 0; i < layout.vagasHangar.Count; i++)
        {
            VagaPortaAvioesV2 vaga = layout.vagasHangar[i];
            if (vaga == null) continue;
            int coluna = i % 12;
            int linha = i / 12;
            float longo = Mathf.Lerp(hangarInicio, hangarFim, coluna / 11f);
            float lateral = Mathf.Lerp(centroLateral - hangarLateral, centroLateral + hangarLateral, linha / 4f);
            float altura = vaga.transform.localPosition.y;
            Set(vaga.transform, Local(layout, longo, lateral, altura), Forward(layout, coluna < 6 ? -1f : 1f, 0f));
            SetChild(vaga.transform, "Entrada", new Vector3(0f, 0f, 8f));
            SetChild(vaga.transform, "Parada", Vector3.zero);
        }
    }

    private static bool TryObterBoundsDoConves(LayoutConvesPortaAvioesV2 layout, out Bounds resultado)
    {
        resultado = new Bounds();
        bool encontrado = false;
        float melhorArea = 0f;
        Renderer[] renderers = layout.transform.root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.transform == layout.transform || renderer.transform.IsChildOf(layout.transform)) continue;
            Bounds local = BoundsEmLocal(renderer.bounds, layout.transform);
            float area = local.size.x * local.size.z;
            if (area < 400f || local.size.y > 12f) continue;
            if (!encontrado || area > melhorArea)
            {
                resultado = local;
                melhorArea = area;
                encontrado = true;
            }
        }
        if (encontrado) return true;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.transform == layout.transform || renderer.transform.IsChildOf(layout.transform)) continue;
            Bounds local = BoundsEmLocal(renderer.bounds, layout.transform);
            if (!encontrado)
            {
                resultado = local;
                encontrado = true;
            }
            else resultado.Encapsulate(local.min); resultado.Encapsulate(local.max);
        }
        return encontrado;
    }

    private static Bounds BoundsEmLocal(Bounds mundo, Transform referencia)
    {
        Vector3 min = mundo.min;
        Vector3 max = mundo.max;
        Vector3[] pontos =
        {
            new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
        };
        Bounds local = new Bounds(referencia.InverseTransformPoint(pontos[0]), Vector3.zero);
        for (int i = 1; i < pontos.Length; i++) local.Encapsulate(referencia.InverseTransformPoint(pontos[i]));
        return local;
    }

    private static Vector3 Local(LayoutConvesPortaAvioesV2 layout, float comprimento, float lateral, float altura)
    {
        return layout.eixoComprimentoEhX ? new Vector3(comprimento, altura, lateral) : new Vector3(lateral, altura, comprimento);
    }

    private static Vector3 LocalDirection(LayoutConvesPortaAvioesV2 layout, float comprimento, float lateral)
    {
        return layout.eixoComprimentoEhX ? new Vector3(comprimento, 0f, lateral).normalized : new Vector3(lateral, 0f, comprimento).normalized;
    }

    private static Quaternion Forward(LayoutConvesPortaAvioesV2 layout, float comprimento, float lateral)
    {
        return Quaternion.LookRotation(LocalDirection(layout, comprimento, lateral), Vector3.up);
    }

    private static void Set(Transform grupo, string nome, Vector3 posicao, Quaternion rotacao)
    {
        if (grupo == null) return;
        Transform ponto = grupo.Find(nome);
        if (ponto == null) return;
        ponto.localPosition = posicao;
        ponto.localRotation = rotacao;
    }

    private static void Set(Transform ponto, Vector3 posicao, Quaternion rotacao)
    {
        if (ponto == null) return;
        ponto.localPosition = posicao;
        ponto.localRotation = rotacao;
    }

    private static void SetChild(Transform pai, string nome, Vector3 posicao)
    {
        if (pai == null) return;
        Transform filho = pai.Find(nome);
        if (filho == null) return;
        filho.localPosition = posicao;
        filho.localRotation = Quaternion.identity;
    }

    private static void RepararScriptsDasVagas(LayoutConvesPortaAvioesV2 layout)
    {
        if (layout == null) return;
        int reparadas = 0;
        MonoScript scriptV2 = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/scripts/PortaAvioesV2/LayoutConvesPortaAvioesV2.cs");
        if (scriptV2 == null) throw new System.InvalidOperationException("Script V2 das vagas não encontrado.");
        Transform[] grupos = { layout.vagasExternas, layout.vagasInternas };
        for (int g = 0; g < grupos.Length; g++)
        {
            Transform grupo = grupos[g];
            if (grupo == null) continue;
            foreach (Transform filho in grupo)
            {
                if (filho == null) continue;
                // O Unity não permite salvar um prefab que ainda contenha
                // componentes Missing Script. Remova apenas essas entradas
                // órfãs; os componentes válidos e os dados da vaga ficam
                // preservados.
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(filho.gameObject);
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
                // A classe VagaPortaAvioesV2 compartilha o arquivo com o
                // Layout. Em prefab contents, FromMonoBehaviour pode retornar
                // nulo; use o asset do script diretamente para garantir uma
                // referência serializada válida.
                SerializedObject serialized = new SerializedObject(vaga);
                SerializedProperty script = serialized.FindProperty("m_Script");
                if (script != null && script.objectReferenceValue != scriptV2)
                {
                    script.objectReferenceValue = scriptV2;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    reparadas++;
                }
                // A importação/reconstrução do prefab pode invalidar a
                // referência nativa entre a leitura do componente e este
                // ponto. Não tente marcar um objeto destruído como dirty.
                if (vaga != null)
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
            // As vagas antigas deste prefab foram geradas com o tipo de
            // componente serializado incorreto. Repare antes de atualizar
            // as listas para que o manager veja uma única Layout e 75
            // componentes VagaPortaAvioesV2 reais.
            RepararScriptsDasVagas(layout);
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
