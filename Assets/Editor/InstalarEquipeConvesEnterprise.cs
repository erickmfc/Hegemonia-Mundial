#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class InstalarEquipeConvesEnterprise
{
    private const string PrefabEnterprise = "Assets/Prefabs/Navios_Guerra/Porta avioes/Uss Enterprise.prefab";
    private const string PrefabTrabalhador = "Assets/Prefabs/Navios_Guerra/Porta avioes/Trabalhador_Conves 1.prefab";
    private const string AnimacaoSinal = "Assets/Animations/TrabalhadorConves/Wave_Clear.anim";
    private static bool instalando;

    static InstalarEquipeConvesEnterprise()
    {
        EditorApplication.delayCall += InstalarSeNecessario;
        EditorApplication.playModeStateChanged += AoMudarModoDeJogo;
    }

    private static void AoMudarModoDeJogo(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += InstalarSeNecessario;
    }

    private static void InstalarSeNecessario()
    {
        if (instalando || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        GameObject trabalhador = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTrabalhador);
        AnimationClip sinal = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimacaoSinal);
        if (trabalhador == null || sinal == null || !System.IO.File.Exists(PrefabEnterprise))
            return;

        instalando = true;
        GameObject raiz = null;
        try
        {
            raiz = PrefabUtility.LoadPrefabContents(PrefabEnterprise);
            GerenciadorOperacoesPortaAvioesV2 gerenciador = raiz.GetComponentInChildren<GerenciadorOperacoesPortaAvioesV2>(true);
            if (gerenciador == null)
            {
                Debug.LogWarning("Enterprise sem GerenciadorOperacoesPortaAvioesV2; equipe não instalada.");
                return;
            }

            EquipeConvesEnterprise equipe = gerenciador.GetComponent<EquipeConvesEnterprise>();
            bool alterado = equipe == null;
            if (equipe == null) equipe = gerenciador.gameObject.AddComponent<EquipeConvesEnterprise>();
            SerializedObject serialized = new SerializedObject(equipe);
            SerializedProperty propriedadeTrabalhador = serialized.FindProperty("prefabTrabalhador");
            SerializedProperty propriedadeSinal = serialized.FindProperty("animacaoSinalLiberacao");
            if (propriedadeTrabalhador.objectReferenceValue != trabalhador)
            {
                propriedadeTrabalhador.objectReferenceValue = trabalhador;
                alterado = true;
            }
            if (propriedadeSinal.objectReferenceValue != sinal)
            {
                propriedadeSinal.objectReferenceValue = sinal;
                alterado = true;
            }
            if (!alterado) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(equipe);
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabEnterprise);
            Debug.Log("Equipe de convés e animação de sinal instaladas somente no USS Enterprise.");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (raiz != null) PrefabUtility.UnloadPrefabContents(raiz);
            instalando = false;
        }
    }
}
#endif
