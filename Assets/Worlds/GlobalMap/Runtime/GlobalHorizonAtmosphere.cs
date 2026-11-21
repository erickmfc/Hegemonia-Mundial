using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Estende visualmente o oceano ate o horizonte e aplica nevoa atmosferica adaptativa
/// por altitude (estilo Sea Power), sem colliders, NavMesh ou custo de simulacao.
/// </summary>
[DisallowMultipleComponent]
public class GlobalHorizonAtmosphere : MonoBehaviour
{
    [Header("Limites do Mapa Jogavel")]
    [SerializeField] private Vector2 playableWorldSize = new Vector2(512000f, 512000f);
    [SerializeField] private float seaLevel = 0f;
    [SerializeField] private float horizonExtension = 900000f;
    [SerializeField] private float seamOverlap = 600f;
    [SerializeField] private float verticalOffset = -0.15f;

    [Header("Material do Mar Distante")]
    [SerializeField] private Material horizonSeaMaterial;
    [SerializeField] private Color fallbackSeaColor = new Color(0.026275955f, 0.13437988f, 0.19852942f, 1f);
    [SerializeField] private Color reflectionSeaColor = new Color(0.48264486f, 0.5361673f, 0.5808823f, 1f);

    [Header("Skybox e Nevoa Atmosferica")]
    [SerializeField] private bool applyMaritimeProceduralSkybox = true;
    [SerializeField] private Color skyTint = new Color(0.24f, 0.45f, 0.68f, 1f);
    [SerializeField] private Color skyGroundColor = new Color(0.03f, 0.14f, 0.21f, 1f);
    [SerializeField] private Color horizonFogColor = new Color(0.62f, 0.75f, 0.85f, 1f);
    [SerializeField] private float baseFogStart = 18000f;
    [SerializeField] private float baseFogEnd = 160000f;
    [SerializeField] private float altitudeFogStartMultiplier = 2.0f;
    [SerializeField] private float altitudeFogEndMultiplier = 4.2f;

    private Camera _cachedCamera;
    private float _lastCameraY = float.NaN;
    private Material _runtimeSkyMaterial;
    private Material _activeSeaMaterial;
    private bool _ownsSeaMaterial;
    private bool _builtStrips;

    private void Start()
    {
        BuildHorizonSeaStrips();
        ConfigureSkybox();
        ApplyAdaptiveFog(force: true);
    }

    private void LateUpdate()
    {
        ApplyAdaptiveFog(force: false);
    }

    private void OnDestroy()
    {
        if (_runtimeSkyMaterial != null) Destroy(_runtimeSkyMaterial);
        if (_ownsSeaMaterial && _activeSeaMaterial != null) Destroy(_activeSeaMaterial);
    }

    private void BuildHorizonSeaStrips()
    {
        if (_builtStrips) return;
        _builtStrips = true;

        Material mat = GetOrCreateSeaMaterial();
        float halfX = playableWorldSize.x * 0.5f;
        float halfZ = playableWorldSize.y * 0.5f;
        float y = seaLevel + verticalOffset;
        float totalSpanX = playableWorldSize.x + horizonExtension * 2f;

        CreateStrip("HorizonteOceano_Norte", new Vector3(0f, y, halfZ - seamOverlap + horizonExtension * 0.5f), new Vector3(totalSpanX, horizonExtension + seamOverlap, 1f), mat);
        CreateStrip("HorizonteOceano_Sul", new Vector3(0f, y, -halfZ + seamOverlap - horizonExtension * 0.5f), new Vector3(totalSpanX, horizonExtension + seamOverlap, 1f), mat);
        CreateStrip("HorizonteOceano_Leste", new Vector3(halfX - seamOverlap + horizonExtension * 0.5f, y, 0f), new Vector3(horizonExtension + seamOverlap, playableWorldSize.y, 1f), mat);
        CreateStrip("HorizonteOceano_Oeste", new Vector3(-halfX + seamOverlap - horizonExtension * 0.5f, y, 0f), new Vector3(horizonExtension + seamOverlap, playableWorldSize.y, 1f), mat);
    }

    private void CreateStrip(string stripName, Vector3 localPosition, Vector3 localScale, Material mat)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = stripName;
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = localScale;
        quad.isStatic = true;

        Collider col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    private Material GetOrCreateSeaMaterial()
    {
        Shader customSeaShader = Shader.Find("GlobalMap/SeaPowerHorizonSea");
        if (horizonSeaMaterial != null && (customSeaShader == null || horizonSeaMaterial.shader == customSeaShader))
        {
            _activeSeaMaterial = horizonSeaMaterial;
            _ownsSeaMaterial = false;
        }
        else
        {
            Shader shader = customSeaShader ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _activeSeaMaterial = new Material(shader);
            _activeSeaMaterial.name = "GlobalMap_HorizonSea_Runtime";
            _ownsSeaMaterial = true;
        }

        SyncSeaMaterialProperties(baseFogStart, baseFogEnd);
        return _activeSeaMaterial;
    }

    private void SyncSeaMaterialProperties(float fogStart, float fogEnd)
    {
        if (_activeSeaMaterial == null) return;
        if (_activeSeaMaterial.HasProperty("_BaseColor")) _activeSeaMaterial.SetColor("_BaseColor", fallbackSeaColor);
        if (_activeSeaMaterial.HasProperty("_ReflectionColor")) _activeSeaMaterial.SetColor("_ReflectionColor", reflectionSeaColor);
        if (_activeSeaMaterial.HasProperty("_HorizonColor")) _activeSeaMaterial.SetColor("_HorizonColor", horizonFogColor);
        if (_activeSeaMaterial.HasProperty("_FogStart")) _activeSeaMaterial.SetFloat("_FogStart", fogStart);
        if (_activeSeaMaterial.HasProperty("_FogEnd")) _activeSeaMaterial.SetFloat("_FogEnd", fogEnd);
    }

    private void ConfigureSkybox()
    {
        if (!applyMaritimeProceduralSkybox) return;

        Shader skyShader = Shader.Find("GlobalMap/SeaPowerHorizonSkybox") ?? Shader.Find("Skybox/Procedural");
        if (skyShader == null) return;

        _runtimeSkyMaterial = new Material(skyShader);
        _runtimeSkyMaterial.name = "GlobalMap_HorizonSkybox_Runtime";
        _runtimeSkyMaterial.SetColor("_SkyTint", skyTint);
        if (_runtimeSkyMaterial.HasProperty("_HorizonColor")) _runtimeSkyMaterial.SetColor("_HorizonColor", horizonFogColor);
        _runtimeSkyMaterial.SetColor("_GroundColor", skyGroundColor);
        if (_runtimeSkyMaterial.HasProperty("_AtmosphereThickness")) _runtimeSkyMaterial.SetFloat("_AtmosphereThickness", 0.65f);
        _runtimeSkyMaterial.SetFloat("_Exposure", 1.0f);
        _runtimeSkyMaterial.SetFloat("_SunSize", 0.025f);
        RenderSettings.skybox = _runtimeSkyMaterial;
    }

    /// <summary>
    /// Atualiza as cores do ceu, chao distante e nevoa do horizonte em tempo real
    /// sem alocar novos materiais (usado pelo CicloDiaNoite).
    /// </summary>
    public void AplicarTemaAtmosferico(Color novoSkyTint, Color novoGroundColor, Color novoFogColor, float exposure)
    {
        skyTint = novoSkyTint;
        skyGroundColor = novoGroundColor;
        horizonFogColor = novoFogColor;

        if (_runtimeSkyMaterial != null)
        {
            _runtimeSkyMaterial.SetColor("_SkyTint", novoSkyTint);
            if (_runtimeSkyMaterial.HasProperty("_HorizonColor")) _runtimeSkyMaterial.SetColor("_HorizonColor", novoFogColor);
            _runtimeSkyMaterial.SetColor("_GroundColor", novoGroundColor);
            _runtimeSkyMaterial.SetFloat("_Exposure", exposure);
        }

        if (RenderSettings.fog)
        {
            RenderSettings.fogColor = novoFogColor;
        }

        SyncSeaMaterialProperties(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
    }

    private void ApplyAdaptiveFog(bool force)
    {
        if (_cachedCamera == null) _cachedCamera = Camera.main;
        if (_cachedCamera == null) return;

        float camY = _cachedCamera.transform.position.y;
        if (!force && Mathf.Abs(camY - _lastCameraY) < 5f) return;
        _lastCameraY = camY;

        float altitude = Mathf.Max(1f, camY - seaLevel);
        float fogStart = Mathf.Max(baseFogStart, altitude * altitudeFogStartMultiplier);
        float fogEnd = Mathf.Max(baseFogEnd, altitude * altitudeFogEndMultiplier);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = horizonFogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;

        SyncSeaMaterialProperties(fogStart, fogEnd);
    }
}
