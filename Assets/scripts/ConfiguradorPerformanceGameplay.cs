using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-950)]
public sealed class ConfiguradorPerformanceGameplay : MonoBehaviour
{
    public enum PerfilQualidade
    {
        Gameplay,
        Visual,
        Adaptativo
    }

    private struct EstadoTerrain
    {
        public float DetailDistance;
        public float TreeDistance;
        public float BillboardStart;
        public float BasemapDistance;
        public float PixelError;
        public bool DrawInstanced;
    }

    private struct EstadoVolume
    {
        public bool Enabled;
        public float Weight;
    }

    private struct EstadoBloom
    {
        public bool Active;
    }

    private struct EstadoRendererAgua
    {
        public bool ReceiveShadows;
        public ShadowCastingMode ShadowCasting;
        public ReflectionProbeUsage ReflectionProbeUsage;
    }

    [Header("Atalho")]
    // F10 fica reservado exclusivamente para fechar a ajuda do jogo.
    [SerializeField] private KeyCode teclaAlternarPerfil = KeyCode.F12;
    [SerializeField] private bool iniciarEmGameplay = true;

    [Header("Adaptativo")]
    [Tooltip("Mantem a qualidade original perto da camera e reduz somente alcance distante quando a CPU/GPU fica pressionada por varios segundos.")]
    [SerializeField] private bool usarPerfilAdaptativo = true;
    [SerializeField] private float limitePressaoFrameMs = 19f;
    [SerializeField] private float limitePressaoSeveraFrameMs = 25f;
    [SerializeField] private float segundosParaAdaptar = 2.5f;
    [SerializeField] private float segundosParaRestaurar = 5f;
    [SerializeField] private float shadowDistanceAdaptativoLeve = 56f;
    [SerializeField] private float shadowDistanceAdaptativoSevero = 40f;

    [Header("Gameplay")]
    [SerializeField] private bool desligarVolumesGlobaisExtras = true;
    [SerializeField] private bool bloquearBloomInstavelDuranteGameplay = true;
    [SerializeField] private bool simplificarTerrenos = true;
    [SerializeField] private bool simplificarAgua = true;
    [SerializeField] private bool desligarMedidoresFpsLegados = false;
    [SerializeField] private bool silenciarDiagnosticos = false;
    [SerializeField] private float shadowDistanceGameplay = 32f;
    [SerializeField] private int shadowCascadesGameplay = 2;
    [SerializeField] private float lodBiasGameplay = 1.35f;
    [SerializeField] private float terrainDetailDistanceGameplay = 60f;
    [SerializeField] private float terrainTreeDistanceGameplay = 1800f;
    [SerializeField] private float terrainBillboardStartGameplay = 80f;
    [SerializeField] private float terrainBasemapDistanceGameplay = 350f;
    [SerializeField] private float terrainPixelErrorGameplay = 4f;

    private readonly Dictionary<int, EstadoTerrain> _terrainsOriginais = new Dictionary<int, EstadoTerrain>();
    private readonly Dictionary<int, EstadoVolume> _volumesOriginais = new Dictionary<int, EstadoVolume>();
    private readonly Dictionary<int, EstadoBloom> _bloomsOriginais = new Dictionary<int, EstadoBloom>();
    private readonly Dictionary<int, EstadoRendererAgua> _renderersAguaOriginais = new Dictionary<int, EstadoRendererAgua>();
    private PerfilQualidade _perfilAtual;
    private float _shadowDistanceOriginal = -1f;
    private float _lodBiasOriginal = -1f;
    private int _shadowCascadesOriginal = -1;
    private float _mediaFrameMs;
    private float _tempoSobPressao;
    private float _tempoRecuperacao;
    private int _nivelAdaptativo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<ConfiguradorPerformanceGameplay>() != null)
        {
            return;
        }

        GameObject go = new GameObject("[ConfiguradorPerformanceGameplay]");
        DontDestroyOnLoad(go);
        go.AddComponent<ConfiguradorPerformanceGameplay>();
    }

    private void Awake()
    {
        _perfilAtual = iniciarEmGameplay
            ? (usarPerfilAdaptativo ? PerfilQualidade.Adaptativo : PerfilQualidade.Gameplay)
            : PerfilQualidade.Visual;
        CapturarQualidadeOriginalSeNecessario();
        SceneManager.sceneLoaded += OnSceneLoaded;
        AplicarPerfil(_perfilAtual);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (_perfilAtual == PerfilQualidade.Adaptativo)
        {
            AtualizarPerfilAdaptativo();
        }

        if (Input.GetKeyDown(teclaAlternarPerfil))
        {
            _perfilAtual = _perfilAtual == PerfilQualidade.Visual
                ? (usarPerfilAdaptativo ? PerfilQualidade.Adaptativo : PerfilQualidade.Gameplay)
                : PerfilQualidade.Visual;

            AplicarPerfil(_perfilAtual);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AplicarPerfil(_perfilAtual);
    }

    private void AplicarPerfil(PerfilQualidade perfil)
    {
        CapturarQualidadeOriginalSeNecessario();

        if (perfil == PerfilQualidade.Adaptativo)
        {
            _nivelAdaptativo = 0;
            RestaurarQualidadeVisual();
            return;
        }

        if (perfil == PerfilQualidade.Gameplay)
        {
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, shadowDistanceGameplay);
            QualitySettings.shadowCascades = Mathf.Min(QualitySettings.shadowCascades, shadowCascadesGameplay);
            QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, lodBiasGameplay);

            if (simplificarTerrenos)
            {
                AplicarTerrenosGameplay();
            }

            if (desligarVolumesGlobaisExtras)
            {
                AplicarVolumesGameplay();
            }

            if (bloquearBloomInstavelDuranteGameplay)
            {
                AplicarBloomSeguro();
            }

            if (simplificarAgua)
            {
                AplicarAguaGameplay();
            }

            if (silenciarDiagnosticos)
            {
                AplicarDiagnosticosGameplay();
            }
        }
        else
        {
            RestaurarQualidadeVisual();
        }
    }

    private void AtualizarPerfilAdaptativo()
    {
        float frameMs = Mathf.Clamp(Time.unscaledDeltaTime * 1000f, 0f, 200f);
        _mediaFrameMs = _mediaFrameMs <= 0f ? frameMs : Mathf.Lerp(_mediaFrameMs, frameMs, 0.08f);
        bool pressaoSevera = _mediaFrameMs >= limitePressaoSeveraFrameMs;
        bool sobPressao = pressaoSevera || _mediaFrameMs >= limitePressaoFrameMs;

        if (sobPressao)
        {
            _tempoSobPressao += Time.unscaledDeltaTime;
            _tempoRecuperacao = 0f;
            int alvo = pressaoSevera ? 2 : 1;
            if (_tempoSobPressao >= segundosParaAdaptar && alvo > _nivelAdaptativo)
            {
                _nivelAdaptativo = alvo;
                AplicarAdaptacaoDistante(_nivelAdaptativo);
            }
            return;
        }

        _tempoSobPressao = 0f;
        _tempoRecuperacao += Time.unscaledDeltaTime;
        if (_nivelAdaptativo > 0 && _tempoRecuperacao >= segundosParaRestaurar)
        {
            _nivelAdaptativo = 0;
            RestaurarQualidadeVisual();
        }
    }

    private void AplicarAdaptacaoDistante(int nivel)
    {
        CapturarQualidadeOriginalSeNecessario();
        bool severo = nivel >= 2;
        QualitySettings.shadowDistance = Mathf.Min(_shadowDistanceOriginal, severo ? shadowDistanceAdaptativoSevero : shadowDistanceAdaptativoLeve);
        QualitySettings.shadowCascades = severo ? Mathf.Min(_shadowCascadesOriginal, 2) : _shadowCascadesOriginal;
        QualitySettings.lodBias = Mathf.Min(_lodBiasOriginal, severo ? 1.35f : 1.60f);

        Terrain[] terrenos = Terrain.activeTerrains;
        float fator = severo ? 0.65f : 0.82f;
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null) continue;
            int key = terreno.GetInstanceID();
            if (!_terrainsOriginais.ContainsKey(key))
            {
                _terrainsOriginais[key] = new EstadoTerrain
                {
                    DetailDistance = terreno.detailObjectDistance,
                    TreeDistance = terreno.treeDistance,
                    BillboardStart = terreno.treeBillboardDistance,
                    BasemapDistance = terreno.basemapDistance,
                    PixelError = terreno.heightmapPixelError,
                    DrawInstanced = terreno.drawInstanced
                };
            }
            EstadoTerrain original = _terrainsOriginais[key];
            terreno.detailObjectDistance = original.DetailDistance * fator;
            terreno.treeDistance = original.TreeDistance * fator;
            terreno.treeBillboardDistance = original.BillboardStart * fator;
            terreno.basemapDistance = original.BasemapDistance * fator;
            terreno.heightmapPixelError = Mathf.Max(original.PixelError, severo ? original.PixelError * 1.35f : original.PixelError * 1.15f);
        }
    }

    private void CapturarQualidadeOriginalSeNecessario()
    {
        if (_shadowDistanceOriginal < 0f)
        {
            _shadowDistanceOriginal = QualitySettings.shadowDistance;
            _shadowCascadesOriginal = QualitySettings.shadowCascades;
            _lodBiasOriginal = QualitySettings.lodBias;
        }
    }

    private void AplicarTerrenosGameplay()
    {
        Terrain[] terrenos = Terrain.activeTerrains;
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null)
            {
                continue;
            }

            int key = terreno.GetInstanceID();
            if (!_terrainsOriginais.ContainsKey(key))
            {
                _terrainsOriginais[key] = new EstadoTerrain
                {
                    DetailDistance = terreno.detailObjectDistance,
                    TreeDistance = terreno.treeDistance,
                    BillboardStart = terreno.treeBillboardDistance,
                    BasemapDistance = terreno.basemapDistance,
                    PixelError = terreno.heightmapPixelError,
                    DrawInstanced = terreno.drawInstanced
                };
            }

            terreno.detailObjectDistance = Mathf.Min(terreno.detailObjectDistance, terrainDetailDistanceGameplay);
            terreno.treeDistance = Mathf.Min(terreno.treeDistance, terrainTreeDistanceGameplay);
            terreno.treeBillboardDistance = Mathf.Min(terreno.treeBillboardDistance, terrainBillboardStartGameplay);
            terreno.basemapDistance = Mathf.Min(terreno.basemapDistance, terrainBasemapDistanceGameplay);
            terreno.heightmapPixelError = Mathf.Max(terreno.heightmapPixelError, terrainPixelErrorGameplay);
            // O shader de Terrain usado nesta cena não fornece todos os
            // parâmetros de instancing exigidos pelo URP (unity_SHC/
            // unity_SHBb). Isso gerava avisos por frame e flashes de luz na
            // build. Desligamos apenas o caminho instanciado; o Terrain,
            // collider e navegação continuam ativos.
            terreno.drawInstanced = false;
        }
    }

    private void AplicarVolumesGameplay()
    {
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        Volume volumePrincipal = null;

        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || !volume.isGlobal)
            {
                continue;
            }

            int key = volume.GetInstanceID();
            if (!_volumesOriginais.ContainsKey(key))
            {
                _volumesOriginais[key] = new EstadoVolume
                {
                    Enabled = volume.enabled,
                    Weight = volume.weight
                };
            }

            if (volumePrincipal == null || volume.priority > volumePrincipal.priority ||
                (volume.priority == volumePrincipal.priority && volume.gameObject.activeInHierarchy && !volumePrincipal.gameObject.activeInHierarchy) ||
                (volume.priority == volumePrincipal.priority && volume.gameObject.activeInHierarchy == volumePrincipal.gameObject.activeInHierarchy &&
                 string.Equals(volume.name, "Global Volume", System.StringComparison.Ordinal) &&
                 !string.Equals(volumePrincipal.name, "Global Volume", System.StringComparison.Ordinal)))
            {
                volumePrincipal = volume;
            }
        }

        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || !volume.isGlobal)
            {
                continue;
            }

            if (volume == volumePrincipal)
            {
                volume.enabled = true;
                if (_volumesOriginais.TryGetValue(volume.GetInstanceID(), out EstadoVolume estado))
                {
                    volume.weight = estado.Weight;
                }
                continue;
            }

            volume.enabled = false;
        }
    }

    private void AplicarBloomSeguro()
    {
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || !volume.isGlobal || volume.sharedProfile == null)
            {
                continue;
            }

            Bloom bloom;
            if (!volume.sharedProfile.TryGet(out bloom) || bloom == null)
            {
                continue;
            }

            int key = bloom.GetInstanceID();
            if (!_bloomsOriginais.ContainsKey(key))
            {
                _bloomsOriginais[key] = new EstadoBloom { Active = bloom.active };
            }

            // Materiais HDR e partículas temporárias não podem transformar um
            // marcador fora da câmera em um clarão verde/ciano no gameplay.
            bloom.active = false;
        }
    }

    private void AplicarAguaGameplay()
    {
        OceanAdvanced[] oceanos = FindObjectsByType<OceanAdvanced>(FindObjectsSortMode.None);
        for (int i = 0; i < oceanos.Length; i++)
        {
            OceanAdvanced oceano = oceanos[i];
            if (oceano == null)
            {
                continue;
            }

            Renderer[] renderers = oceano.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                {
                    continue;
                }

                int key = renderer.GetInstanceID();
                if (!_renderersAguaOriginais.ContainsKey(key))
                {
                    _renderersAguaOriginais[key] = new EstadoRendererAgua
                    {
                        ReceiveShadows = renderer.receiveShadows,
                        ShadowCasting = renderer.shadowCastingMode,
                        ReflectionProbeUsage = renderer.reflectionProbeUsage
                    };
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        if (!desligarMedidoresFpsLegados)
        {
            return;
        }

        DesabilitarComponentesPorNome("guiFPS");
        DesabilitarComponentesPorNome("ui_suimonoFps");
    }

    private static void DesabilitarComponentesPorNome(string nomeTipo)
    {
        if (string.IsNullOrWhiteSpace(nomeTipo))
        {
            return;
        }

        MonoBehaviour[] comportamentos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < comportamentos.Length; i++)
        {
            MonoBehaviour comportamento = comportamentos[i];
            if (comportamento == null)
            {
                continue;
            }

            if (!string.Equals(comportamento.GetType().Name, nomeTipo, System.StringComparison.Ordinal))
            {
                continue;
            }

            comportamento.enabled = false;
        }
    }

    private void AplicarDiagnosticosGameplay()
    {
        DiagnosticoDesempenhoJogo diagnostico = Object.FindFirstObjectByType<DiagnosticoDesempenhoJogo>();
        if (diagnostico != null)
        {
            return;
        }

        DiagnosticoHUD[] diagnosticosHud = FindObjectsByType<DiagnosticoHUD>(FindObjectsSortMode.None);
        for (int i = 0; i < diagnosticosHud.Length; i++)
        {
            if (diagnosticosHud[i] != null)
            {
                diagnosticosHud[i].SetRuntimeVisible(false);
            }
        }
    }

    private void RestaurarQualidadeVisual()
    {
        if (_shadowDistanceOriginal >= 0f)
        {
            QualitySettings.shadowDistance = _shadowDistanceOriginal;
        }

        if (_shadowCascadesOriginal >= 0)
        {
            QualitySettings.shadowCascades = _shadowCascadesOriginal;
        }

        if (_lodBiasOriginal >= 0f)
        {
            QualitySettings.lodBias = _lodBiasOriginal;
        }

        Terrain[] terrenos = Terrain.activeTerrains;
        for (int i = 0; i < terrenos.Length; i++)
        {
            Terrain terreno = terrenos[i];
            if (terreno == null)
            {
                continue;
            }

            if (_terrainsOriginais.TryGetValue(terreno.GetInstanceID(), out EstadoTerrain estado))
            {
                terreno.detailObjectDistance = estado.DetailDistance;
                terreno.treeDistance = estado.TreeDistance;
                terreno.treeBillboardDistance = estado.BillboardStart;
                terreno.basemapDistance = estado.BasemapDistance;
                terreno.heightmapPixelError = estado.PixelError;
                terreno.drawInstanced = estado.DrawInstanced;
            }
        }

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || !volume.isGlobal)
            {
                continue;
            }

            if (_volumesOriginais.TryGetValue(volume.GetInstanceID(), out EstadoVolume estado))
            {
                volume.enabled = estado.Enabled;
                volume.weight = estado.Weight;
            }
        }

        if (bloquearBloomInstavelDuranteGameplay)
        {
            Volume[] volumesComBloom = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            for (int i = 0; i < volumesComBloom.Length; i++)
            {
                Volume volume = volumesComBloom[i];
                if (volume == null || volume.sharedProfile == null)
                {
                    continue;
                }

                Bloom bloom;
                if (!volume.sharedProfile.TryGet(out bloom) || bloom == null)
                {
                    continue;
                }

                if (_bloomsOriginais.TryGetValue(bloom.GetInstanceID(), out EstadoBloom estadoBloom))
                {
                    bloom.active = estadoBloom.Active;
                }
            }
        }

        OceanAdvanced[] oceanos = FindObjectsByType<OceanAdvanced>(FindObjectsSortMode.None);
        for (int i = 0; i < oceanos.Length; i++)
        {
            OceanAdvanced oceano = oceanos[i];
            if (oceano == null)
            {
                continue;
            }

            Renderer[] renderers = oceano.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                {
                    continue;
                }

                if (_renderersAguaOriginais.TryGetValue(renderer.GetInstanceID(), out EstadoRendererAgua estado))
                {
                    renderer.receiveShadows = estado.ReceiveShadows;
                    renderer.shadowCastingMode = estado.ShadowCasting;
                    renderer.reflectionProbeUsage = estado.ReflectionProbeUsage;
                }
            }
        }
    }
}
