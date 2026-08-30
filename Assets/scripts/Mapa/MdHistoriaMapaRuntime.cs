using UnityEngine;

/// <summary>
/// Registro leve da área jogável da Md Historia.
/// Os bloqueios físicos são os BoxColliders gerados pelo utilitário Editor;
/// este componente apenas mantém os parâmetros e desenha a área no Editor.
/// </summary>
public sealed class MdHistoriaMapaRuntime : MonoBehaviour
{
    private const string PrefixoParede = "LimiteMdHistoria_";

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
        OcultarParedoesSemRemoverColisao();
    }

    private void Start()
    {
        OcultarParedoesSemRemoverColisao();
    }

    private void LateUpdate()
    {
        // Alguns fluxos de inicialização/reconstrução visual podem reativar
        // Renderers depois do Start. Reaplica somente a parte visual do
        // bloqueio, sem tocar nos BoxColliders que seguram as unidades.
        OcultarParedoesSemRemoverColisao();
    }

    private void OcultarParedoesSemRemoverColisao()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform filho = transform.GetChild(i);
            if (filho == null || !filho.name.StartsWith(PrefixoParede, System.StringComparison.Ordinal))
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

                renderer.enabled = false;
                renderer.forceRenderingOff = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.28f, 0.65f, 0.78f, 0.75f);
        Gizmos.DrawWireCube(mapaBounds.center, mapaBounds.size);
    }
}
