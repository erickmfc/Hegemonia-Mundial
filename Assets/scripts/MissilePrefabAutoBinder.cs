using UnityEngine;

#if UNITY_EDITOR
using System;
using UnityEditor;
#endif

public static class MissilePrefabAutoBinder
{
#if UNITY_EDITOR
    private const string LogPrefix = "[AutoMissil]";

    public static bool BindLancadorMisseis(LancadorMisseis alvo, bool forcar = false)
    {
        return BindIfMissing(alvo, nameof(LancadorMisseis.missilPrefab), forcar,
            typeof(MisselICBM),
            "icbm", "ballistic", "balistic", "nuclear", "missil", "missile");
    }

    public static bool BindLancadorNaval(LancadorNaval alvo, bool forcar = false)
    {
        bool alterado = false;
        alterado |= BindIfMissing(alvo, nameof(LancadorNaval.prefabMissel), forcar,
            typeof(MisselNaval),
            "naval", "missil", "missile", "navalmissile");
        alterado |= BindIfMissing(alvo, nameof(LancadorNaval.prefabTorpedo), forcar,
            typeof(Torpedo),
            "torpedo", "sub", "underwater");
        return alterado;
    }

    public static bool BindLancadorMisselCaca(LancadorMisselCaca alvo, bool forcar = false)
    {
        return BindIfMissing(alvo, nameof(LancadorMisselCaca.missilCacaPrefab), forcar,
            typeof(MisselCaca),
            "caca", "fighter", "air", "missil", "missile");
    }

    public static bool BindControleDroneHasaf(ControleDroneHasaf alvo, bool forcar = false)
    {
        return BindIfMissing(alvo, nameof(ControleDroneHasaf.prefabMissil), forcar,
            typeof(MisselTatico),
            "tatico", "tactical", "drone", "missil", "missile");
    }

    public static bool BindAviaoBombardeiro(AviaoBombardeiro alvo, bool forcar = false)
    {
        bool alterado = false;
        alterado |= BindIfMissing(alvo, nameof(AviaoBombardeiro.projetilPrefab), forcar,
            typeof(MisselBombardeiro),
            "bombardeiro", "bomber", "missil", "missile", "bomba", "bomb");

        if (alvo != null && alvo.projetilPrefab == null)
        {
            alterado |= BindIfMissing(alvo, nameof(AviaoBombardeiro.projetilPrefab), true,
                typeof(BombaBombardeiro),
                "bomba", "bomb", "airstrike");
        }

        return alterado;
    }

    public static bool BindControleSubmarino(ControleSubmarino alvo, bool forcar = false)
    {
        bool alterado = false;
        alterado |= BindIfMissing(alvo, nameof(ControleSubmarino.prefabMisselSubmarino), forcar,
            typeof(MisselSubmarino),
            "submarino", "sub", "underwater", "missil", "missile");
        alterado |= BindIfMissing(alvo, nameof(ControleSubmarino.prefabTorpedo), forcar,
            typeof(Torpedo),
            "torpedo", "sub", "underwater");
        return alterado;
    }

    [MenuItem("Hegemonia/Misseis/Auto configurar selecionados")]
    private static void AutoConfigurarSelecionados()
    {
        AplicarNosSelecionados(false);
    }

    [MenuItem("Hegemonia/Misseis/Auto configurar selecionados (forcar)")]
    private static void AutoConfigurarSelecionadosForcado()
    {
        AplicarNosSelecionados(true);
    }

    private static void AplicarNosSelecionados(bool forcar)
    {
        GameObject[] selecionados = Selection.gameObjects;
        int totalAlterados = 0;

        for (int i = 0; i < selecionados.Length; i++)
        {
            GameObject go = selecionados[i];
            if (go == null) continue;

            totalAlterados += BindLancadorMisseis(go.GetComponentInChildren<LancadorMisseis>(true) ?? go.GetComponentInParent<LancadorMisseis>(), forcar) ? 1 : 0;
            totalAlterados += BindLancadorNaval(go.GetComponentInChildren<LancadorNaval>(true) ?? go.GetComponentInParent<LancadorNaval>(), forcar) ? 1 : 0;
            totalAlterados += BindLancadorMisselCaca(go.GetComponentInChildren<LancadorMisselCaca>(true) ?? go.GetComponentInParent<LancadorMisselCaca>(), forcar) ? 1 : 0;
            totalAlterados += BindControleDroneHasaf(go.GetComponentInChildren<ControleDroneHasaf>(true) ?? go.GetComponentInParent<ControleDroneHasaf>(), forcar) ? 1 : 0;
            totalAlterados += BindAviaoBombardeiro(go.GetComponentInChildren<AviaoBombardeiro>(true) ?? go.GetComponentInParent<AviaoBombardeiro>(), forcar) ? 1 : 0;
            totalAlterados += BindControleSubmarino(go.GetComponentInChildren<ControleSubmarino>(true) ?? go.GetComponentInParent<ControleSubmarino>(), forcar) ? 1 : 0;
        }

        Debug.Log($"{LogPrefix} Auto-config aplicado em {selecionados.Length} selecionado(s). Alteracoes: {totalAlterados}.");
    }

    private static bool BindIfMissing(UnityEngine.Object alvo, string nomeCampo, bool forcar, params object[] candidatos)
    {
        if (alvo == null) return false;

        SerializedObject serializedObject = new SerializedObject(alvo);
        SerializedProperty propriedade = serializedObject.FindProperty(nomeCampo);
        if (propriedade == null || propriedade.propertyType != SerializedPropertyType.ObjectReference)
        {
            return false;
        }

        if (!forcar && propriedade.objectReferenceValue != null)
        {
            return false;
        }

        // Se estamos dentro de OnValidate/Awake/CheckConsistency, diferir para o próximo frame
        // para evitar o erro "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate"
        // que ocorre quando LoadAssetAtPath carrega prefabs com SpriteRenderer.
        if (!Application.isPlaying)
        {
            UnityEngine.Object alvoCopia = alvo;
            string nomeCampoCopia = nomeCampo;
            bool forcarCopia = forcar;
            object[] candidatosCopia = candidatos;
            EditorApplication.delayCall += () =>
            {
                if (alvoCopia == null) return;
                BindIfMissingImediato(alvoCopia, nomeCampoCopia, forcarCopia, candidatosCopia);
            };
            return false;
        }

        return BindIfMissingImediato(alvo, nomeCampo, forcar, candidatos);
    }

    private static bool BindIfMissingImediato(UnityEngine.Object alvo, string nomeCampo, bool forcar, params object[] candidatos)
    {
        if (alvo == null) return false;

        SerializedObject serializedObject = new SerializedObject(alvo);
        SerializedProperty propriedade = serializedObject.FindProperty(nomeCampo);
        if (propriedade == null || propriedade.propertyType != SerializedPropertyType.ObjectReference)
        {
            return false;
        }

        if (!forcar && propriedade.objectReferenceValue != null)
        {
            return false;
        }

        GameObject prefab = EncontrarPrefab(candidatos);
        if (prefab == null)
        {
            return false;
        }

        propriedade.objectReferenceValue = prefab;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(alvo);
        Debug.Log($"{LogPrefix} {alvo.name}: '{nomeCampo}' configurado com '{prefab.name}'.", alvo);
        return true;
    }

    private static GameObject EncontrarPrefab(params object[] candidatos)
    {
        string[] guids = AssetDatabase.FindAssets("t:GameObject");
        GameObject melhor = null;
        int melhorScore = int.MinValue;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            int score = CalcularScore(prefab, path, candidatos);
            if (score > melhorScore)
            {
                melhorScore = score;
                melhor = prefab;
            }
        }

        return melhorScore > int.MinValue ? melhor : null;
    }

    private static int CalcularScore(GameObject prefab, string path, object[] candidatos)
    {
        int score = 0;
        bool encontrouCorrespondencia = false;
        string nome = prefab.name.ToLowerInvariant();
        string caminho = path.ToLowerInvariant();

        if (caminho.Contains("/prefabs/")) score += 25;
        if (caminho.Contains("/prefebs/")) score += 20;
        if (caminho.Contains("/example")) score -= 40;
        if (caminho.Contains("/demo")) score -= 20;

        for (int i = 0; i < candidatos.Length; i++)
        {
            if (candidatos[i] is Type tipo)
            {
                if (prefab.GetComponentInChildren(tipo, true) != null)
                {
                    score += 120;
                    encontrouCorrespondencia = true;
                }
                continue;
            }

            if (candidatos[i] is string texto && !string.IsNullOrWhiteSpace(texto))
            {
                string termo = texto.ToLowerInvariant().Replace(" ", string.Empty);
                if (nome.Contains(termo))
                {
                    score += 30;
                    encontrouCorrespondencia = true;
                }
                if (caminho.Contains(termo))
                {
                    score += 15;
                    encontrouCorrespondencia = true;
                }
            }
        }

        return encontrouCorrespondencia ? score : int.MinValue;
    }
#endif
}
