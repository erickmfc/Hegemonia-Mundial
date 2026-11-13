using UnityEditor;
using UnityEngine;

namespace Hegemonia.EditorTools
{
    public static class HabilitadorReadWriteModelos
    {
        [MenuItem("Tools/Performance/Habilitar Read-Write em Modelos de Cidades e Navios")]
        public static void Executar()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model");
            int alterados = 0;

            string[] termos = new string[]
            {
                "city", "cidade", "building", "predio",
                "zumwalt", "type-45", "visakhapatnam", "steregushchiy",
                "navio", "petroleiro", "fragata", "corveta", "fortaleza", "nagoya", "cargueiro"
            };

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;

                string pathLower = path.ToLowerInvariant();
                bool corresponde = false;
                for (int t = 0; t < termos.Length; t++)
                {
                    if (pathLower.Contains(termos[t]))
                    {
                        corresponde = true;
                        break;
                    }
                }

                if (!corresponde) continue;

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    alterados++;
                    Debug.Log($"[HabilitadorReadWriteModelos] Read/Write habilitado em: {path}");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[HabilitadorReadWriteModelos] Processo concluído: {alterados} modelos 3D atualizados com Read/Write ativado.");
        }
    }
}
