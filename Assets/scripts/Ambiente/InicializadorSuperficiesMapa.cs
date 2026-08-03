using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Garante que todos os terrenos jogáveis da cena sejam reconhecidos como chão.
/// Terrains auxiliares do mapa inimigo continuam invisíveis no mundo 3D.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class InicializadorSuperficiesMapa : MonoBehaviour
{
    private const string NomeObjeto = "[InicializadorSuperficiesMapa]";
    private const string MaterialTerrainResource = "CodexCampaignTerrainURP";
    private const float MargemRecorteCamera = 500f;
    private const float RecorteMaximoSeguro = 14000f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<InicializadorSuperficiesMapa>() != null)
        {
            return;
        }

        GameObject go = new GameObject(NomeObjeto);
        DontDestroyOnLoad(go);
        go.AddComponent<InicializadorSuperficiesMapa>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CorrigirCenaAtual();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CorrigirCenaAtual();
    }

    private void CorrigirCenaAtual()
    {
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int terrenosJogaveis = 0;
        int terrenosReativados = 0;
        int marcadoresCriados = 0;
        int materiaisCorrigidos = 0;
        int instancingDesativado = 0;

        int camadaChao = LayerMask.NameToLayer("Chao");
        Material materialTerrain = Resources.Load<Material>(MaterialTerrainResource);

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || !terrain.gameObject.scene.IsValid())
            {
                continue;
            }

            if (EhTerrenoAuxiliarInimigo(terrain))
            {
                // O mapa auxiliar continua disponível para colisão/navegação,
                // mas não pode aparecer como uma segunda superfície no mundo.
                terrain.enabled = false;
                continue;
            }

            terrenosJogaveis++;
            if (!terrain.gameObject.activeSelf)
            {
                terrain.gameObject.SetActive(true);
                terrenosReativados++;
            }

            terrain.enabled = true;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.enabled = true;
            }

            // Toda superfície Terrain da partida usa o mesmo material URP
            // validado. Isso cobre também Terrains adicionados na cena e
            // evita que o fallback padrão fique invisível na build.
            if (materialTerrain != null && terrain.materialTemplate != materialTerrain)
            {
                terrain.materialTemplate = materialTerrain;
                materiaisCorrigidos++;
            }

            if (terrain.drawInstanced)
            {
                terrain.drawInstanced = false;
                instancingDesativado++;
            }

            // Preserve o material já configurado no asset. Nesta configuração
            // URP, remover o override faz o Terrain cair em um shader padrão
            // ausente e a superfície fica magenta. A correção de material deve
            // ser feita no asset pelo Editor quando houver um shader Terrain
            // compatível; o runtime não substitui a aparência funcional.

            if (camadaChao >= 0 && terrain.gameObject.layer != camadaChao)
            {
                terrain.gameObject.layer = camadaChao;
            }

            MarcadorSuperficieMapa marcador = terrain.GetComponent<MarcadorSuperficieMapa>();
            if (marcador == null)
            {
                marcador = terrain.gameObject.AddComponent<MarcadorSuperficieMapa>();
                marcadoresCriados++;
            }

            marcador.DefinirTipo(TipoSuperficieMapa.Chao);
        }

        AjustarRecorteDasCameras(terrains);

        if (terrenosReativados > 0 || marcadoresCriados > 0 || materiaisCorrigidos > 0 || instancingDesativado > 0)
        {
            Debug.Log($"[Mapa] superfícies corrigidas: terrenos={terrenosJogaveis}, reativados={terrenosReativados}, marcadores={marcadoresCriados}");
        }
    }

    private static bool EhTerrenoAuxiliarInimigo(Terrain terrain)
    {
        string nome = terrain.name.ToLowerInvariant();
        return nome.Contains("mapa inimigo") || nome.Contains("mapa_inimigo") || nome.Contains("enemy map");
    }

    private static void AjustarRecorteDasCameras(Terrain[] terrains)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int c = 0; c < cameras.Length; c++)
        {
            Camera camera = cameras[c];
            if (camera == null || !camera.enabled || !camera.gameObject.scene.IsValid())
            {
                continue;
            }

            float distanciaNecessaria = camera.farClipPlane;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || !terrain.enabled || !terrain.gameObject.activeInHierarchy ||
                    EhTerrenoAuxiliarInimigo(terrain) || terrain.terrainData == null)
                {
                    continue;
                }

                Vector3 escala = terrain.transform.lossyScale;
                Vector3 tamanho = Vector3.Scale(terrain.terrainData.size, new Vector3(
                    Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z)));
                Vector3 centro = terrain.GetPosition() + tamanho * 0.5f;
                float raio = tamanho.magnitude * 0.5f;
                distanciaNecessaria = Mathf.Max(distanciaNecessaria,
                    Vector3.Distance(camera.transform.position, centro) + raio + MargemRecorteCamera);
            }

            camera.farClipPlane = Mathf.Clamp(distanciaNecessaria, camera.farClipPlane, RecorteMaximoSeguro);
        }
    }
}
