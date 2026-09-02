using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remove vegetação que fica dentro do footprint de uma construção.
///
/// A limpeza é destrutiva apenas para o trecho ocupado: árvores e detalhes
/// do Terrain são copiados para um TerrainData de runtime antes de serem
/// removidos, e props independentes são apenas desativados. O componente
/// anexado à construção restaura props independentes caso a obra seja
/// demolida, sem gravar alterações nos assets originais.
/// </summary>
public static class LimpezaVegetacaoConstrucao
{
    private const float MargemEdificacao = 1.35f;
    private const float MargemRua = 0.65f;
    private const float MargemMuro = 0.45f;
    private const int LimitePropsPorConstrucao = 96;

    private static readonly Dictionary<int, TerrainData> DadosRuntimePorTerrain = new Dictionary<int, TerrainData>();
    private static readonly List<Transform> TransformesTemporarios = new List<Transform>(2048);

    public static void Aplicar(GameObject construcao)
    {
        if (construcao == null || !construcao.scene.IsValid())
        {
            return;
        }

        if (!TryCalcularBounds(construcao, out Bounds footprint))
        {
            return;
        }

        float margem = MargemEdificacao;
        string nome = construcao.name.ToLowerInvariant();
        if (nome.Contains("rua") || nome.Contains("road") || construcao.GetComponent<RuaConectora>() != null)
        {
            margem = MargemRua;
        }
        else if (nome.Contains("muro") || nome.Contains("wall") || nome.Contains("pared"))
        {
            margem = MargemMuro;
        }

        footprint.Expand(new Vector3(margem * 2f, 0f, margem * 2f));

        Terrain[] terrenos = UnityEngine.Object.FindObjectsByType<Terrain>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null || terreno.terrainData == null || !terreno.enabled)
            {
                // Os tiles Terrain_... da água antiga ficam desativados no
                // runtime. Eles continuam com collider para navegação, mas
                // não devem receber uma cópia de TerrainData por construção.
                continue;
            }

            Bounds limitesTerrain = ObterBoundsTerrain(terreno);
            if (!SobrepoeXZ(footprint, limitesTerrain))
            {
                continue;
            }

            LimparDadosTerrain(terreno, footprint);
        }

        DesativarPropsIndependentes(footprint, construcao);
    }

    private static void LimparDadosTerrain(Terrain terreno, Bounds footprint)
    {
        TerrainData dados = ObterDadosRuntime(terreno);
        if (dados == null)
        {
            return;
        }

        Vector3 origem = terreno.transform.position;
        Vector3 tamanho = dados.size;
        if (tamanho.x <= 0.01f || tamanho.z <= 0.01f)
        {
            return;
        }

        TreeInstance[] arvores = dados.treeInstances;
        if (arvores != null && arvores.Length > 0)
        {
            List<TreeInstance> mantidas = new List<TreeInstance>(arvores.Length);
            for (int i = 0; i < arvores.Length; i++)
            {
                TreeInstance arvore = arvores[i];
                Vector3 local = new Vector3(arvore.position.x * tamanho.x, arvore.position.y * tamanho.y, arvore.position.z * tamanho.z);
                Vector3 mundo = terreno.transform.TransformPoint(local);
                if (!PontoDentroXZ(mundo, footprint))
                {
                    mantidas.Add(arvore);
                }
            }

            if (mantidas.Count != arvores.Length)
            {
                dados.SetTreeInstances(mantidas.ToArray(), false);
            }
        }

        int larguraDetalhe = dados.detailWidth;
        int alturaDetalhe = dados.detailHeight;
        DetailPrototype[] prototipos = dados.detailPrototypes;
        if (larguraDetalhe <= 0 || alturaDetalhe <= 0 || prototipos == null || prototipos.Length == 0)
        {
            return;
        }

        float uMin = Mathf.Clamp01((footprint.min.x - origem.x) / tamanho.x);
        float uMax = Mathf.Clamp01((footprint.max.x - origem.x) / tamanho.x);
        float vMin = Mathf.Clamp01((footprint.min.z - origem.z) / tamanho.z);
        float vMax = Mathf.Clamp01((footprint.max.z - origem.z) / tamanho.z);
        int xMin = Mathf.Clamp(Mathf.FloorToInt(uMin * larguraDetalhe) - 1, 0, larguraDetalhe - 1);
        int xMax = Mathf.Clamp(Mathf.CeilToInt(uMax * larguraDetalhe) + 1, 0, larguraDetalhe);
        int yMin = Mathf.Clamp(Mathf.FloorToInt(vMin * alturaDetalhe) - 1, 0, alturaDetalhe - 1);
        int yMax = Mathf.Clamp(Mathf.CeilToInt(vMax * alturaDetalhe) + 1, 0, alturaDetalhe);
        int largura = Mathf.Max(0, xMax - xMin);
        int altura = Mathf.Max(0, yMax - yMin);
        if (largura == 0 || altura == 0)
        {
            return;
        }

        for (int camada = 0; camada < prototipos.Length; camada++)
        {
            int[,] detalhes = dados.GetDetailLayer(xMin, yMin, largura, altura, camada);
            bool mudou = false;
            for (int y = 0; y < altura; y++)
            {
                for (int x = 0; x < largura; x++)
                {
                    if (detalhes[y, x] == 0)
                    {
                        continue;
                    }

                    float u = (xMin + x + 0.5f) / larguraDetalhe;
                    float v = (yMin + y + 0.5f) / alturaDetalhe;
                    Vector3 mundo = terreno.transform.TransformPoint(new Vector3(u * tamanho.x, 0f, v * tamanho.z));
                    if (PontoDentroXZ(mundo, footprint))
                    {
                        detalhes[y, x] = 0;
                        mudou = true;
                    }
                }
            }

            if (mudou)
            {
                dados.SetDetailLayer(xMin, yMin, camada, detalhes);
            }
        }
    }

    private static TerrainData ObterDadosRuntime(Terrain terreno)
    {
        int id = terreno.GetInstanceID();
        if (DadosRuntimePorTerrain.TryGetValue(id, out TerrainData copia)
            && copia != null
            && terreno.terrainData == copia)
        {
            return copia;
        }

        TerrainData original = terreno.terrainData;
        if (original == null)
        {
            return null;
        }

        copia = UnityEngine.Object.Instantiate(original);
        copia.name = "[Runtime] Vegetação " + terreno.name;
        terreno.terrainData = copia;

        TerrainCollider collider = terreno.GetComponent<TerrainCollider>();
        if (collider != null)
        {
            collider.terrainData = copia;
        }

        DadosRuntimePorTerrain[id] = copia;
        return copia;
    }

    private static void DesativarPropsIndependentes(Bounds footprint, GameObject construcao)
    {
        TransformesTemporarios.Clear();
        Transform[] todos = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            Transform transform = todos[i];
            if (transform == null || transform.IsChildOf(construcao.transform) || !EhPropNatural(transform))
            {
                continue;
            }

            if (!TryCalcularBounds(transform.gameObject, out Bounds bounds) || !SobrepoeXZ(footprint, bounds))
            {
                continue;
            }

            TransformesTemporarios.Add(transform);
            if (TransformesTemporarios.Count >= LimitePropsPorConstrucao)
            {
                break;
            }
        }

        if (TransformesTemporarios.Count == 0)
        {
            return;
        }

        NaturezaOcultadaPorConstrucao registro = construcao.GetComponent<NaturezaOcultadaPorConstrucao>();
        if (registro == null)
        {
            registro = construcao.AddComponent<NaturezaOcultadaPorConstrucao>();
        }

        for (int i = 0; i < TransformesTemporarios.Count; i++)
        {
            Transform prop = TransformesTemporarios[i];
            if (prop != null && prop.gameObject.activeSelf)
            {
                prop.gameObject.SetActive(false);
                registro.Registrar(prop.gameObject);
            }
        }
    }

    private static bool EhPropNatural(Transform transform)
    {
        string nome = transform.name.ToLowerInvariant();
        if (nome.Contains("tree") || nome.Contains("arvore") || nome.Contains("árvore")
            || nome.Contains("rock") || nome.Contains("pedra") || nome.Contains("stone")
            || nome.Contains("boulder") || nome.Contains("veget") || nome.Contains("foliage")
            || nome.Contains("bush") || nome.Contains("arbusto") || nome.Contains("flora"))
        {
            return true;
        }

        // Não dependemos de tags opcionais: CompareTag lança erro quando a
        // tag não existe no TagManager do projeto. Os nomes dos props são
        // suficientes e deixam o sistema plug-and-play em qualquer cena.
        return false;
    }

    private static bool TryCalcularBounds(GameObject objeto, out Bounds bounds)
    {
        bounds = new Bounds(objeto.transform.position, Vector3.zero);
        bool iniciou = false;

        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!iniciou)
            {
                bounds = renderer.bounds;
                iniciou = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Collider[] colliders = objeto.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            if (!iniciou)
            {
                bounds = collider.bounds;
                iniciou = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (!iniciou)
        {
            bounds = new Bounds(objeto.transform.position, new Vector3(2f, 1f, 2f));
        }

        return bounds.size.x > 0.01f || bounds.size.z > 0.01f;
    }

    private static Bounds ObterBoundsTerrain(Terrain terreno)
    {
        Vector3 posicao = terreno.transform.position;
        Vector3 tamanho = terreno.terrainData.size;
        return new Bounds(posicao + tamanho * 0.5f, tamanho);
    }

    private static bool SobrepoeXZ(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x && a.max.x >= b.min.x
            && a.min.z <= b.max.z && a.max.z >= b.min.z;
    }

    private static bool PontoDentroXZ(Vector3 ponto, Bounds bounds)
    {
        return ponto.x >= bounds.min.x && ponto.x <= bounds.max.x
            && ponto.z >= bounds.min.z && ponto.z <= bounds.max.z;
    }
}

/// <summary>
/// Registro reversível para props de árvore/pedra que não pertencem ao
/// TerrainData. A demolição restaura apenas os objetos que esta construção
/// efetivamente ocultou.
/// </summary>
public sealed class NaturezaOcultadaPorConstrucao : MonoBehaviour
{
    private readonly List<GameObject> objetosOcultados = new List<GameObject>();

    public void Registrar(GameObject objeto)
    {
        if (objeto != null && !objetosOcultados.Contains(objeto))
        {
            objetosOcultados.Add(objeto);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < objetosOcultados.Count; i++)
        {
            GameObject objeto = objetosOcultados[i];
            if (objeto != null)
            {
                objeto.SetActive(true);
            }
        }
    }
}
