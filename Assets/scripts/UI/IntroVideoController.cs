using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Reproduz o primeiro video encontrado em Resources/Intro antes do menu.
/// Sem video, permanece invisivel e nao interfere no fluxo atual.
/// </summary>
[DefaultExecutionOrder(-9000)]
public sealed class IntroVideoController : MonoBehaviour
{
    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private RenderTexture renderTexture;
    private GameObject painel;
    private Font fontePadrao;
    private bool ativo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (FindFirstObjectByType<IntroVideoController>() == null)
        {
            new GameObject("IntroVideoController").AddComponent<IntroVideoController>();
        }
    }

    private void Awake()
    {
        if (!ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += AoCarregarCena;
        fontePadrao = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void Start()
    {
        VideoClip[] clips = Resources.LoadAll<VideoClip>("Intro");
        if (clips == null || clips.Length == 0 || clips[0] == null)
        {
            return;
        }

        PrepararInterface(clips[0]);
    }

    private void Update()
    {
        if (!ativo)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
        {
            EncerrarVideo();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    private void PrepararInterface(VideoClip clip)
    {
        GameObject canvasObject = new GameObject("CanvasIntroVideo");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        painel = new GameObject("IntroVideoRoot");
        painel.transform.SetParent(canvas.transform, false);
        RectTransform painelRect = painel.AddComponent<RectTransform>();
        painelRect.anchorMin = Vector2.zero;
        painelRect.anchorMax = Vector2.one;
        painelRect.offsetMin = Vector2.zero;
        painelRect.offsetMax = Vector2.zero;
        painel.AddComponent<Image>().color = Color.black;

        renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        renderTexture.name = "IntroVideoRenderTexture";

        GameObject videoObject = new GameObject("VideoPlayer");
        videoObject.transform.SetParent(painel.transform, false);
        videoPlayer = videoObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.loopPointReached += AoTerminarVideo;

        audioSource = videoObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        AudioSettingsService.RegistrarFonte(audioSource, AudioChannel.Musica);
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.EnableAudioTrack(0, true);
        videoPlayer.SetTargetAudioSource(0, audioSource);

        RawImage imagem = videoObject.AddComponent<RawImage>();
        RectTransform imagemRect = imagem.rectTransform;
        imagemRect.anchorMin = Vector2.zero;
        imagemRect.anchorMax = Vector2.one;
        imagemRect.offsetMin = Vector2.zero;
        imagemRect.offsetMax = Vector2.zero;
        imagem.texture = renderTexture;
        imagem.raycastTarget = false;

        Image faixa = CriarPainel("Faixa", painel.transform, new Vector2(0f, 0f), new Vector2(1f, 0.12f), new Color(0f, 0f, 0f, 0.62f));
        CriarTexto("Aviso", faixa.transform, "VIDEO DE ABERTURA  |  ESC ou ESPACO para pular", 16, TextAnchor.MiddleCenter, new Color(0.88f, 0.96f, 1f, 1f));
        Button pular = CriarBotao("PULAR", painel.transform);
        pular.onClick.AddListener(EncerrarVideo);

        ativo = true;
        painel.SetActive(true);
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += AoPrepararVideo;
    }

    private void AoPrepararVideo(VideoPlayer player)
    {
        if (player == null || !ativo)
        {
            return;
        }

        player.Play();
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    private void AoTerminarVideo(VideoPlayer player)
    {
        EncerrarVideo();
    }

    private void EncerrarVideo()
    {
        if (!ativo)
        {
            return;
        }

        ativo = false;
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        if (painel != null)
        {
            painel.SetActive(false);
        }
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (!ConfiguracaoCenasJogo.EhCenaDeMenu(cena.name))
        {
            Destroy(gameObject);
        }
    }

    private Image CriarPainel(string nome, Transform pai, Vector2 ancoraMin, Vector2 ancoraMax, Color cor)
    {
        GameObject objeto = new GameObject(nome);
        objeto.transform.SetParent(pai, false);
        RectTransform rect = objeto.AddComponent<RectTransform>();
        rect.anchorMin = ancoraMin;
        rect.anchorMax = ancoraMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image imagem = objeto.AddComponent<Image>();
        imagem.color = cor;
        return imagem;
    }

    private Text CriarTexto(string nome, Transform pai, string conteudo, int tamanho, TextAnchor alinhamento, Color cor)
    {
        GameObject objeto = new GameObject(nome);
        objeto.transform.SetParent(pai, false);
        RectTransform rect = objeto.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text texto = objeto.AddComponent<Text>();
        texto.font = fontePadrao;
        texto.text = conteudo;
        texto.fontSize = tamanho;
        texto.alignment = alinhamento;
        texto.color = cor;
        return texto;
    }

    private Button CriarBotao(string nome, Transform pai)
    {
        GameObject objeto = new GameObject(nome);
        objeto.transform.SetParent(pai, false);
        RectTransform rect = objeto.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-28f, -28f);
        rect.sizeDelta = new Vector2(170f, 48f);
        Image imagem = objeto.AddComponent<Image>();
        imagem.color = new Color(0.04f, 0.18f, 0.23f, 0.96f);
        Button botao = objeto.AddComponent<Button>();
        botao.targetGraphic = imagem;
        CriarTexto("Texto", objeto.transform, "PULAR", 16, TextAnchor.MiddleCenter, Color.white);
        return botao;
    }
}
