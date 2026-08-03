#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AplicarValoresDefinitivosEditor
{
    [MenuItem("Hegemonia/Economia/Aplicar valores definitivos")]
    public static void Aplicar()
    {
        string[] guids = AssetDatabase.FindAssets("t:DadosConstrucao");
        int atualizados = 0;
        int semCorrespondencia = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DadosConstrucao ficha = AssetDatabase.LoadAssetAtPath<DadosConstrucao>(path);
            if (ficha == null) continue;

            long valor;
            if (ValoresDefinitivosHegemonia.TryObterPreco(ficha.itemId, ficha.nomeItem, out valor))
            {
                if (ficha.precoDefinitivo != valor)
                {
                    ficha.precoDefinitivo = valor;
                    EditorUtility.SetDirty(ficha);
                    atualizados++;
                }
            }
            else
            {
                semCorrespondencia++;
                Debug.LogWarning("[ValoresDefinitivos] Sem correspondencia: " + path + " / " + ficha.nomeItem);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ValoresDefinitivos] Assets atualizados=" + atualizados + " | sem correspondencia=" + semCorrespondencia);
    }
}
#endif
