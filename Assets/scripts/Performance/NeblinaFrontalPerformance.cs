using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Configuração opcional, por cena, para reduzir o custo de renderização à
/// frente da câmera. A câmera continua sendo a autoridade de visibilidade:
/// frustum culling, occlusion culling e layer culling trabalham juntos; a
/// neblina só suaviza a transição visual dos objetos distantes.
///
/// O componente não desativa GameObjects, Renderers ou Terrains e não cria
/// outra câmera. Ao ser desativado, restaura as configurações que encontrou.
/// </summary>
[DisallowMultipleComponent]
public sealed class NeblinaFrontalPerformance : MonoBehaviour
{
    [Header("Câmera")]
    [SerializeField] private Camera cameraAlvo;
    [SerializeField] private bool usarOcclusionCulling = true;
    [SerializeField, Min(100f)] private float distanciaCulling = 12000f;
    [SerializeField] private LayerMask camadasSemCulling;
    [SerializeField] private bool preservarFarClipDaCamera = true;

    [Header("Neblina visual")]
    [SerializeField] private bool aplicarNeblinaDaCena = true;
    [SerializeField] private bool preservarCorDaCena = true;
    [SerializeField] private Color corNeblina = new Color(0.60f, 0.67f, 0.73f, 1f);
    [SerializeField] private FogMode modoNeblina = FogMode.Linear;
    [SerializeField, Min(0f)] private float inicioNeblina = 6500f;
    [SerializeField, Min(1f)] private float fimNeblina = 14000f;
    [SerializeField, Min(0.01f)] private float densidadeNeblina = 0.00004f;
    [SerializeField] private bool restaurarAoDesativar = true;

    private bool estadoCapturado;
    private bool cameraOcclusionOriginal;
    private float cameraFarClipOriginal;
    private float[] cullingDistancesOriginais;
    private bool cullingSphericalOriginal;
    private bool fogOriginal;
    private Color fogColorOriginal;
    private FogMode fogModeOriginal;
    private float fogDensityOriginal;
    private float fogStartOriginal;
    private float fogEndOriginal;

    public Camera CameraAlvo => cameraAlvo;
    public float DistanciaCulling => distanciaCulling;
    public bool NeblinaAplicada => aplicarNeblinaDaCena;

    private void Awake()
    {
        ResolverCamera();
        CapturarEstadoOriginal();
        AplicarConfiguracao();
    }

    private void Start()
    {
        // O CameraController pode inicializar o far clip no Start. Reaplicar
        // apenas uma vez depois dele evita disputa por frame sem mexer na
        // posição ou na projeção da câmera.
        ResolverCamera();
        AplicarConfiguracao();
    }

    private void OnDisable()
    {
        if (restaurarAoDesativar) RestaurarEstadoOriginal();
    }

    private void OnDestroy()
    {
        if (restaurarAoDesativar) RestaurarEstadoOriginal();
    }

    public void ReaplicarConfiguracao()
    {
        ResolverCamera();
        if (!estadoCapturado) CapturarEstadoOriginal();
        AplicarConfiguracao();
    }

    private void ResolverCamera()
    {
        if (cameraAlvo != null) return;
        cameraAlvo = Camera.main;
        if (cameraAlvo == null) cameraAlvo = FindFirstObjectByType<Camera>();
    }

    private void CapturarEstadoOriginal()
    {
        if (estadoCapturado) return;

        if (cameraAlvo != null)
        {
            cameraOcclusionOriginal = cameraAlvo.useOcclusionCulling;
            cameraFarClipOriginal = cameraAlvo.farClipPlane;
            cullingDistancesOriginais = cameraAlvo.layerCullDistances;
            cullingSphericalOriginal = cameraAlvo.layerCullSpherical;
        }

        fogOriginal = RenderSettings.fog;
        fogColorOriginal = RenderSettings.fogColor;
        fogModeOriginal = RenderSettings.fogMode;
        fogDensityOriginal = RenderSettings.fogDensity;
        fogStartOriginal = RenderSettings.fogStartDistance;
        fogEndOriginal = RenderSettings.fogEndDistance;
        estadoCapturado = true;
    }

    private void AplicarConfiguracao()
    {
        if (cameraAlvo != null)
        {
            cameraAlvo.useOcclusionCulling = usarOcclusionCulling;

            // Layer culling é uma redução de custo reversível e não remove
            // objetos da cena. A máscara permite preservar camadas críticas.
            float distancia = Mathf.Max(100f, distanciaCulling);
            float[] distancias = new float[32];
            for (int i = 0; i < distancias.Length; i++)
            {
                distancias[i] = (camadasSemCulling.value & (1 << i)) != 0 ? 0f : distancia;
            }

            cameraAlvo.layerCullDistances = distancias;
            // O projeto usa um Scriptable Render Pipeline. Nesse modo o Unity
            // não suporta layerCullSpherical e emite aviso; o culling por
            // distância e o frustum continuam ativos sem essa opção.
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                cameraAlvo.layerCullSpherical = true;
            }

            // A cena demo já possui CameraController, que calcula um far clip
            // adequado aos tiles. Não o substituímos quando esta opção está
            // ligada; o culling por layer e o frustum continuam ativos.
            if (!preservarFarClipDaCamera)
            {
                cameraAlvo.farClipPlane = Mathf.Max(cameraAlvo.nearClipPlane + 1f, distancia);
            }
        }

        if (!aplicarNeblinaDaCena) return;

        RenderSettings.fog = true;
        if (!preservarCorDaCena) RenderSettings.fogColor = corNeblina;
        RenderSettings.fogMode = modoNeblina;
        RenderSettings.fogDensity = Mathf.Max(0.00001f, densidadeNeblina);
        RenderSettings.fogStartDistance = Mathf.Max(0f, inicioNeblina);
        RenderSettings.fogEndDistance = Mathf.Max(RenderSettings.fogStartDistance + 1f, fimNeblina);
    }

    private void RestaurarEstadoOriginal()
    {
        if (!estadoCapturado) return;

        if (cameraAlvo != null)
        {
            cameraAlvo.useOcclusionCulling = cameraOcclusionOriginal;
            cameraAlvo.farClipPlane = cameraFarClipOriginal;
            cameraAlvo.layerCullDistances = cullingDistancesOriginais;
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                cameraAlvo.layerCullSpherical = cullingSphericalOriginal;
            }
        }

        RenderSettings.fog = fogOriginal;
        RenderSettings.fogColor = fogColorOriginal;
        RenderSettings.fogMode = fogModeOriginal;
        RenderSettings.fogDensity = fogDensityOriginal;
        RenderSettings.fogStartDistance = fogStartOriginal;
        RenderSettings.fogEndDistance = fogEndOriginal;
        estadoCapturado = false;
    }
}
