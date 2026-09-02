using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Registro leve da área jogável da Md Historia.
/// Os bloqueios físicos são os BoxColliders gerados pelo utilitário Editor;
/// este componente apenas mantém os parâmetros e desenha a área no Editor.
/// </summary>
public sealed class MdHistoriaMapaRuntime : MonoBehaviour
{
    private const string PrefixoParede = "LimiteMdHistoria_";
    private const string PrefixoZonaExpansao = "ZonaExpansaoFronteira_";
    private const string PrefixoVisualTerritorio = "Visual_Fronteira_";
    private const string NomePisoTerritorio = "Piso_Holografico_Territorio";
    private const float IntervaloReconstrucaoCacheVisual = 0.75f;

    private readonly List<Renderer> renderersVisuaisBloqueados = new List<Renderer>(32);
    private readonly List<Terrain> terrenosLegados = new List<Terrain>(32);
    private readonly List<TerrainCollider> collidersTerrenosLegados = new List<TerrainCollider>(32);
    private bool cacheVisualInicializado;
    private float proximaReconstrucaoCacheVisual;

    [SerializeField] private Bounds mapaBounds;
    [SerializeField] private float nivelAgua;
    [SerializeField] private float alturaParedao;

    public Bounds MapaBounds => mapaBounds;
    public float NivelAgua => nivelAgua;
    public float AlturaParedao => alturaParedao;

    public void Configurar(Bounds bounds, float waterLevel, float wallHeight)
    {
        mapaBounds = bounds;
        nivelAgua = waterLevel;
        alturaParedao = wallHeight;
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += AoCarregarCena;
        ReconstruirCacheVisual(true);
        AplicarBloqueioVisual();
    }

    private void Start()
    {
        // Mantém a segunda aplicação existente para cobrir objetos habilitados
        // por outros inicializadores durante o primeiro ciclo da cena.
        ReconstruirCacheVisual(true);
        AplicarBloqueioVisual();
    }

    private void LateUpdate()
    {
        // Alguns fluxos de inicialização/reconstrução visual podem reativar
        // Renderers depois do Start. Reaplica o bloqueio em cada frame, mas
        // evita buscas globais e alocações no caminho quente.
        if (!cacheVisualInicializado || Time.unscaledTime >= proximaReconstrucaoCacheVisual)
        {
            ReconstruirCacheVisual(false);
        }

        AplicarBloqueioVisual();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        ReconstruirCacheVisual(true);
        AplicarBloqueioVisual();
    }

    private void ReconstruirCacheVisual(bool forcar)
    {
        if (!forcar && cacheVisualInicializado && Time.unscaledTime < proximaReconstrucaoCacheVisual)
        {
            return;
        }

        renderersVisuaisBloqueados.Clear();
        terrenosLegados.Clear();
        collidersTerrenosLegados.Clear();

        // Os quatro limites estão organizados dentro de "Locais terrenos"
        // na cena, e não como filhos diretos deste componente. Procurar a
        // hierarquia local deixava os MeshRenderers das travas escaparem em
        // runtime quando algum inicializador os reativava. Essa busca é feita
        // somente na construção/reconciliação do cache, nunca a cada frame.
        Transform[] todosOsObjetos = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < todosOsObjetos.Length; i++)
        {
            Transform filho = todosOsObjetos[i];
            if (filho == null)
            {
                continue;
            }

            bool ehParedeFisica = filho.name.StartsWith(PrefixoParede, System.StringComparison.Ordinal);
            bool ehZonaExpansao = filho.name.StartsWith(PrefixoZonaExpansao, System.StringComparison.Ordinal);
            bool ehVisualTerritorio = filho.name.StartsWith(PrefixoVisualTerritorio, System.StringComparison.OrdinalIgnoreCase)
                || filho.name.Equals(NomePisoTerritorio, System.StringComparison.OrdinalIgnoreCase);
            if (!ehParedeFisica && !ehZonaExpansao && !ehVisualTerritorio)
            {
                continue;
            }

            Renderer[] renderers = filho.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                {
                    continue;
                }

                if (!renderersVisuaisBloqueados.Contains(renderer))
                {
                    renderersVisuaisBloqueados.Add(renderer);
                }
            }
        }

        // A MD ainda traz alguns Terrain legados que eram usados como placas
        // de água/fronteira. Mesmo com o inicializador global, um Terrain pode
        // ser reativado por um fluxo de carga tardio e voltar a desenhar uma
        // faixa clara no horizonte. Desligue somente o renderer desses nomes
        // legados; o TerrainCollider permanece ativo para bloqueio e consultas
        // de superfície do roteador naval.
        Terrain[] terrenos = FindObjectsByType<Terrain>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null || !terreno.gameObject.scene.IsValid())
            {
                continue;
            }

            string nome = terreno.name ?? string.Empty;
            bool legadoVisual = nome.Equals("fronteira", System.StringComparison.OrdinalIgnoreCase)
                || nome.Equals("Terrain", System.StringComparison.OrdinalIgnoreCase)
                || nome.StartsWith("Terrain_", System.StringComparison.OrdinalIgnoreCase);
            if (!legadoVisual)
            {
                continue;
            }

            terrenosLegados.Add(terreno);
            TerrainCollider collider = terreno.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collidersTerrenosLegados.Add(collider);
            }
        }

        cacheVisualInicializado = true;
        proximaReconstrucaoCacheVisual = Time.unscaledTime + IntervaloReconstrucaoCacheVisual;
    }

    private void AplicarBloqueioVisual()
    {
        // A fronteira e as zonas de expansão continuam existindo para
        // colisão/trigger/estado territorial. Somente sua representação
        // visual é removida para não desenhar uma cinta artificial no
        // horizonte ou uma moldura colorida sobre o mar.
        for (int i = 0; i < renderersVisuaisBloqueados.Count; i++)
        {
            Renderer renderer = renderersVisuaisBloqueados[i];
            if (renderer == null)
            {
                continue;
            }

            if (renderer.enabled)
            {
                renderer.enabled = false;
            }

            if (!renderer.forceRenderingOff)
            {
                renderer.forceRenderingOff = true;
            }

            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (renderer.receiveShadows)
            {
                renderer.receiveShadows = false;
            }
        }

        // Alguns fluxos de carga podem reativar o componente Terrain ou
        // reconstruir sua renderização instanciada. Desligar explicitamente
        // todas as passagens visuais evita que a borda clara reapareça no
        // horizonte, sem tocar no TerrainCollider abaixo.
        for (int i = 0; i < terrenosLegados.Count; i++)
        {
            Terrain terreno = terrenosLegados[i];
            if (terreno == null)
            {
                continue;
            }

            if (terreno.drawHeightmap)
            {
                terreno.drawHeightmap = false;
            }

            if (terreno.drawTreesAndFoliage)
            {
                terreno.drawTreesAndFoliage = false;
            }

            if (terreno.drawInstanced)
            {
                terreno.drawInstanced = false;
            }

            if (terreno.enabled)
            {
                terreno.enabled = false;
            }
        }

        for (int i = 0; i < collidersTerrenosLegados.Count; i++)
        {
            TerrainCollider collider = collidersTerrenosLegados[i];
            if (collider != null && !collider.enabled)
            {
                collider.enabled = true;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.28f, 0.65f, 0.78f, 0.75f);
        Gizmos.DrawWireCube(mapaBounds.center, mapaBounds.size);
    }
}
