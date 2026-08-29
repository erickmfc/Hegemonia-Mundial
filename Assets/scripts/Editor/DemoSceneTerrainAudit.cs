#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auditoria não destrutiva da cobertura do mapa da demo. O relatório mostra
/// os limites reais de cada tile, os tiles inativos/sem TerrainData, câmeras e
/// controladores duplicados. A validação não altera objetos.
/// </summary>
public static class DemoSceneTerrainAudit
{
    private const string DemoScenePath = "Assets/_Recovery/demo1.unity";

    [MenuItem("Hegemonia/Demo/Validar cobertura dos terrenos e mapa", priority = 20)]
    public static void ValidarDemo()
    {
        Scene scene = AbrirDemo();
        Terrain[] terrenos = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MapaGeralController[] mapas = UnityEngine.Object.FindObjectsByType<MapaGeralController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Bounds cobertura = new Bounds();
        bool possuiCobertura = false;
        int validos = 0;
        int problemas = 0;
        List<string> faltas = new List<string>();

        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null) continue;

            if (terreno.terrainData == null)
            {
                problemas++;
                faltas.Add(terreno.name + " sem TerrainData");
                Debug.LogWarning("[DemoTerrain] " + terreno.name + " não possui TerrainData.", terreno);
                continue;
            }

            Vector3 escala = terreno.transform.lossyScale;
            Vector3 tamanho = Vector3.Scale(terreno.terrainData.size, new Vector3(
                Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z)));
            Bounds bounds = new Bounds(terreno.transform.position + tamanho * 0.5f, tamanho);
            bool terrenoAtivo = terreno.gameObject.activeInHierarchy && terreno.enabled;
            bool tamanhoValido = tamanho.x > 1f && tamanho.z > 1f;

            // A cobertura jogável deve refletir apenas os tiles que realmente
            // participam da cena. O demo mantém um Terrain legado inativo
            // como rascunho, portanto ele não pode ampliar artificialmente os
            // limites do mapa nem fazer os tiles laterais parecerem ausentes.
            if (terrenoAtivo && tamanhoValido)
            {
                if (!possuiCobertura)
                {
                    cobertura = bounds;
                    possuiCobertura = true;
                }
                else cobertura.Encapsulate(bounds);
            }

            if (!terrenoAtivo)
            {
                // The canonical demo keeps a legacy object named "Terrain"
                // disabled. It has no gameplay layers and is intentionally
                // excluded by InicializadorSuperficiesMapa; it is not a
                // missing side tile.
                if (string.Equals(terreno.name.Trim(), "Terrain", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[DemoTerrain] " + terreno.name + " | terreno auxiliar/rascunho inativo, excluído da cobertura jogável.", terreno);
                }
                else
                {
                    problemas++;
                    faltas.Add(terreno.name + " inativo");
                }
            }
            else if (!tamanhoValido)
            {
                problemas++;
                faltas.Add(terreno.name + " com tamanho inválido " + tamanho.ToString("F1"));
            }
            else validos++;

            Debug.Log(string.Format(
                "[DemoTerrain] {0} | ativo={1} enabled={2} origem={3} tamanho={4} limitesMin={5} limitesMax={6}",
                terreno.name, terreno.gameObject.activeInHierarchy, terreno.enabled,
                terreno.transform.position.ToString("F1"), tamanho.ToString("F1"),
                bounds.min.ToString("F1"), bounds.max.ToString("F1")), terreno);
        }

        Debug.Log(string.Format(
            "[DemoTerrain] RESUMO cena={0} tiles={1} válidos={2} problemas={3} cobertura={4} min={5} max={6}",
            scene.path, terrenos.Length, validos, problemas,
            possuiCobertura ? cobertura.size.ToString("F1") : "nenhuma",
            possuiCobertura ? cobertura.min.ToString("F1") : "-",
            possuiCobertura ? cobertura.max.ToString("F1") : "-"));

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null) continue;
            Debug.Log(string.Format("[DemoTerrain] Câmera {0} ativo={1} farClip={2:0} occlusion={3} máscara={4}",
                camera.name, camera.isActiveAndEnabled, camera.farClipPlane,
                camera.useOcclusionCulling, camera.cullingMask), camera);
        }

        for (int i = 0; i < mapas.Length; i++)
        {
            MapaGeralController mapa = mapas[i];
            if (mapa == null) continue;
            Debug.Log(string.Format("[DemoTerrain] MapaGeralController {0} ativo={1}",
                mapa.name, mapa.isActiveAndEnabled), mapa);
        }

        if (faltas.Count > 0)
        {
            Debug.LogWarning("[DemoTerrain] Problemas encontrados: " + string.Join("; ", faltas));
        }
        else
        {
            Debug.Log("[DemoTerrain] Cobertura dos terrenos sem problemas estruturais.");
        }
    }

    [MenuItem("Hegemonia/Demo/Selecionar objeto de performance", priority = 21)]
    public static void SelecionarPerformance()
    {
        AbrirDemo();
        NeblinaFrontalPerformance componente = UnityEngine.Object.FindFirstObjectByType<NeblinaFrontalPerformance>(FindObjectsInactive.Include);
        if (componente == null)
        {
            Debug.LogWarning("[DemoTerrain] O objeto de performance ainda não foi criado na demo.");
            return;
        }

        Selection.activeGameObject = componente.gameObject;
        EditorGUIUtility.PingObject(componente.gameObject);
    }

    private static Scene AbrirDemo()
    {
        Scene ativa = SceneManager.GetActiveScene();
        if (!string.Equals(ativa.path, DemoScenePath, StringComparison.OrdinalIgnoreCase))
        {
            ativa = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        }

        return ativa;
    }
}
#endif
