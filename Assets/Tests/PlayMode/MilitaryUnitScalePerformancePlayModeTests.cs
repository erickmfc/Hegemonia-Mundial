using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Mede a renderização dos prefabs militares reais em 4 e 101 instâncias.
/// O teste mantém scripts e agentes desligados para isolar chamadas de desenho;
/// o custo de simulação é coberto pelas verificações de movimentação existentes.
/// </summary>
public sealed class MilitaryUnitScalePerformancePlayModeTests
{
    private const string TankPrefabPath = "Assets/Prefabs/Veiculos/Tank_Ubu/Ubu.prefab";
    private const int TankCountForScale = 101;
    private const int WarmupFrames = 12;
    private const int SampleFrames = 45;
    private const int TestLayer = 31;

    [UnityTest]
    public IEnumerator TankPrefabMeasuresFourAndOneHundredOneInstancesWithAndWithoutInstancing()
    {
#if !UNITY_EDITOR
        Assert.Ignore("O benchmark precisa do AssetDatabase para carregar os prefabs do projeto.");
        yield break;
#else
        if (!SystemInfo.supportsInstancing)
        {
            Assert.Ignore("A plataforma atual não suporta GPU instancing.");
            yield break;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabPath);
        Assert.That(prefab, Is.Not.Null, "O prefab de tanque real precisa existir para medir a escala.");

        var instancias = new List<GameObject>(TankCountForScale);
        var materiais = new List<Material>(64);
        var estadoInstancingOriginal = new List<bool>(64);
        var materiaisVistos = new HashSet<Material>();
        var frameTimesMs = new List<float>(SampleFrames);
        var camerasParaRestaurar = new List<Camera>();
        var estadosOriginaisDasCameras = new List<bool>();
        var root = new GameObject("MilitaryScale_RenderBenchmark_Temporary");
        root.SetActive(false);
        Camera[] existingCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < existingCameras.Length; i++)
        {
            Camera existingCamera = existingCameras[i];
            if (existingCamera == null) continue;
            camerasParaRestaurar.Add(existingCamera);
            estadosOriginaisDasCameras.Add(existingCamera.enabled);
        }

        var cameraObject = new GameObject("MilitaryScale_RenderBenchmarkCamera_Temporary");
        Camera camera = cameraObject.AddComponent<Camera>();
        var target = new RenderTexture(640, 480, 16, RenderTextureFormat.ARGB32);
        ProfilerRecorder batches = default;
        ProfilerRecorder drawCalls = default;
        ProfilerRecorder triangles = default;
        ProfilerRecorder mainThread = default;

        camera.targetTexture = target;
        camera.orthographic = true;
        camera.orthographicSize = 85f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1000f;
        camera.cullingMask = 1 << TestLayer;
        camera.transform.position = new Vector3(0f, 150f, -140f);
        camera.transform.LookAt(new Vector3(0f, 0f, 0f));
        camera.enabled = true;

        try
        {
            for (int i = 0; i < camerasParaRestaurar.Count; i++)
            {
                if (camerasParaRestaurar[i] != null)
                    camerasParaRestaurar[i].enabled = false;
            }

            for (int i = 0; i < TankCountForScale; i++)
            {
                int column = i % 11;
                int row = i / 11;
                Vector3 position = new Vector3((column - 5) * 14f, 0f, (row - 4) * 14f);
                GameObject tank = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity, root.transform);
                tank.name = "MilitaryScale_Tank_" + i;
                DefinirLayerRecursivo(tank.transform, TestLayer);

                // Isola o custo gráfico sem adicionar agentes fora de NavMesh nem
                // executar os sistemas da cena no mesmo ciclo de teste.
                Behaviour[] behaviours = tank.GetComponentsInChildren<Behaviour>(true);
                for (int componentIndex = 0; componentIndex < behaviours.Length; componentIndex++)
                {
                    if (behaviours[componentIndex] != null)
                        behaviours[componentIndex].enabled = false;
                }

                Renderer[] renderers = tank.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null) continue;
                    renderer.enabled = true;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Material[] shared = renderer.sharedMaterials;
                    for (int materialIndex = 0; materialIndex < shared.Length; materialIndex++)
                    {
                        Material material = shared[materialIndex];
                        if (material == null || !materiaisVistos.Add(material)) continue;
                        materiais.Add(material);
                        estadoInstancingOriginal.Add(material.enableInstancing);
                    }
                }

                instancias.Add(tank);
            }

            // Desliga os scripts enquanto a hierarquia ainda está inativa:
            // nenhum NavMeshAgent começa a operar fora de uma malha durante o teste.
            root.SetActive(true);

            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 64);
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 64);
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 64);
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 64);
            Assert.That(batches.Valid, Is.True, "O ProfilerRecorder precisa expor Batches Count no Unity 6.");
            Assert.That(drawCalls.Valid, Is.True, "O ProfilerRecorder precisa expor Draw Calls Count no Unity 6.");
            Assert.That(triangles.Valid, Is.True, "O ProfilerRecorder precisa expor Triangles Count no Unity 6.");
            Assert.That(mainThread.Valid, Is.True, "O ProfilerRecorder precisa expor o tempo da thread principal.");

            for (int ativo = 4; ativo <= TankCountForScale; ativo = ativo == 4 ? TankCountForScale : TankCountForScale + 1)
            {
                for (int i = 0; i < instancias.Count; i++)
                    instancias[i].SetActive(i < ativo);

                for (int instancingLigado = 0; instancingLigado <= 1; instancingLigado++)
                {
                    for (int materialIndex = 0; materialIndex < materiais.Count; materialIndex++)
                        materiais[materialIndex].enableInstancing = instancingLigado != 0;

                    for (int frame = 0; frame < WarmupFrames; frame++) yield return null;

                    long somaBatches = 0L;
                    long somaDrawCalls = 0L;
                    long somaTriangles = 0L;
                    long somaMainThreadNs = 0L;
                    int amostrasBatches = 0;
                    int amostrasMainThread = 0;
                    for (int frame = 0; frame < SampleFrames; frame++)
                    {
                        yield return null;
                        float frameMs = Time.unscaledDeltaTime * 1000f;
                        if (frameMs > 0f)
                            frameTimesMs.Add(frameMs);
                        somaBatches += batches.LastValue;
                        somaDrawCalls += drawCalls.LastValue;
                        somaTriangles += triangles.LastValue;
                        somaMainThreadNs += mainThread.LastValue;
                        amostrasBatches++;
                        amostrasMainThread++;
                    }

                    Assert.That(amostrasBatches, Is.GreaterThan(0), "A captura precisa registrar frames renderizados.");
                    Assert.That(somaDrawCalls, Is.GreaterThan(0L), "A câmera de benchmark precisa desenhar os tanques ativos.");
                    Assert.That(somaTriangles, Is.GreaterThan(0L), "A câmera de benchmark precisa processar triângulos dos tanques.");
                    Assert.That(somaBatches, Is.GreaterThan(0L), "A câmera de benchmark precisa processar batches dos tanques.");
                    Assert.That(frameTimesMs.Count, Is.GreaterThan(30), "A captura precisa registrar frame time suficiente.");

                    double frameMsAverage = frameTimesMs.Average();
                    float frameMsP95 = Percentile95(frameTimesMs);
                    double batchesAverage = somaBatches / (double)amostrasBatches;
                    double drawCallsAverage = somaDrawCalls / (double)amostrasBatches;
                    double trianglesAverage = somaTriangles / (double)amostrasBatches;
                    int renderersAtivos = ContarRenderers(instancias, ativo);
                    Assert.That(renderersAtivos, Is.GreaterThan(0), "A escala medida precisa conter renderers ativos.");
                    string mainThreadMsText = (somaMainThreadNs / (double)amostrasMainThread / 1000000d).ToString("F3");
                    Debug.Log("[MILITARY_SCALE_RENDER] tanks=" + ativo
                              + "; instancing=" + (instancingLigado != 0)
                              + "; renderers=" + renderersAtivos
                              + "; materials=" + materiais.Count
                              + "; fpsAvg=" + (frameMsAverage > 0d ? (1000d / frameMsAverage).ToString("F1") : "n/d")
                              + "; frameAvgMs=" + frameMsAverage.ToString("F2")
                              + "; frameP95Ms=" + frameMsP95.ToString("F2")
                              + "; drawCallsAvg=" + drawCallsAverage.ToString("F1")
                              + "; trianglesAvg=" + trianglesAverage.ToString("F1")
                              + "; batchesAvg=" + batchesAverage.ToString("F1")
                              + "; mainThreadAvgMs=" + mainThreadMsText);
                    frameTimesMs.Clear();
                }
            }

            Assert.That(instancias.Count, Is.EqualTo(TankCountForScale));
            Assert.That(materiais.Count, Is.GreaterThan(0));
            Assert.That(ContarRenderers(instancias, TankCountForScale), Is.GreaterThan(0));
        }
        finally
        {
            for (int i = 0; i < materiais.Count; i++)
            {
                if (materiais[i] != null)
                    materiais[i].enableInstancing = estadoInstancingOriginal[i];
            }
            if (batches.Valid) batches.Dispose();
            if (drawCalls.Valid) drawCalls.Dispose();
            if (triangles.Valid) triangles.Dispose();
            if (mainThread.Valid) mainThread.Dispose();
            if (camera != null) camera.enabled = false;
            for (int i = 0; i < camerasParaRestaurar.Count; i++)
            {
                if (camerasParaRestaurar[i] != null)
                    camerasParaRestaurar[i].enabled = estadosOriginaisDasCameras[i];
            }
            if (root != null) UnityEngine.Object.Destroy(root);
            if (cameraObject != null) UnityEngine.Object.Destroy(cameraObject);
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }

        yield return null;
#endif
    }

    private static int ContarRenderers(List<GameObject> instancias, int quantidade)
    {
        int total = 0;
        for (int i = 0; i < quantidade && i < instancias.Count; i++)
        {
            if (instancias[i] != null)
                total += instancias[i].GetComponentsInChildren<Renderer>(true).Length;
        }
        return total;
    }

    private static float Percentile95(List<float> values)
    {
        values.Sort();
        int index = Mathf.Clamp(Mathf.CeilToInt(values.Count * 0.95f) - 1, 0, values.Count - 1);
        return values[index];
    }

    private static void DefinirLayerRecursivo(Transform transform, int layer)
    {
        transform.gameObject.layer = layer;
        for (int i = 0; i < transform.childCount; i++)
            DefinirLayerRecursivo(transform.GetChild(i), layer);
    }
}
