using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum LoadingRequestKind
{
    Campanha,
    Tutorial,
    Save
}

/// <summary>
/// Tela de transicao persistente. Carrega a cena em segundo plano enquanto
/// exibe um conjunto curto de dicas aleatorias, sem alterar o estado da partida.
/// </summary>
[DefaultExecutionOrder(-8500)]
public sealed class LoadingScreenService : MonoBehaviour
{
    public static LoadingScreenService Instancia { get; private set; }

    [Header("Dicas")]
    [SerializeField] private int quantidadeDicasPorCarregamento = 4;
    [SerializeField] private float segundosPorDica = 4f;

    private Canvas canvas;
    private Font fontePadrao;
    private GameObject raiz;
    private RawImage imagemDica;
    private Text titulo;
    private Text textoDica;
    private Text progressoTexto;
    private Image progressoBarra;
    private AspectRatioFitter ajusteAspecto;
    private readonly List<Texture2D> dicas = new List<Texture2D>();
    private AsyncOperation carregamentoAtual;
    private Coroutine rotinaCarregamento;
    private bool aguardandoCena;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instancia != null)
        {
            return;
        }

        GameObject objeto = new GameObject("LoadingScreenService");
        objeto.AddComponent<LoadingScreenService>();
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += AoCarregarCena;
        CarregarCatalogoDeDicas();
        ConstruirInterface();
        Esconder();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        if (Instancia == this)
        {
            Instancia = null;
        }
    }

    public static void CarregarCena(string nomeCena, LoadingRequestKind tipo)
    {
        GarantirInstancia();
        if (Instancia == null)
        {
            SceneManager.LoadScene(nomeCena);
            return;
        }

        Instancia.IniciarCarregamento(nomeCena, tipo);
    }

    public static void RecarregarCatalogo()
    {
        if (Instancia != null)
        {
            Instancia.CarregarCatalogoDeDicas();
        }
    }

    private static void GarantirInstancia()
    {
        if (Instancia != null)
        {
            return;
        }

        LoadingScreenService encontrada = FindFirstObjectByType<LoadingScreenService>();
        if (encontrada != null)
        {
            Instancia = encontrada;
            return;
        }

        GameObject objeto = new GameObject("LoadingScreenService");
        Instancia = objeto.AddComponent<LoadingScreenService>();
    }

    private void IniciarCarregamento(string nomeCena, LoadingRequestKind tipo)
    {
        if (string.IsNullOrWhiteSpace(nomeCena))
        {
            Debug.LogError("[LoadingScreen] Cena vazia recebida.");
            return;
        }

        if (rotinaCarregamento != null)
        {
            StopCoroutine(rotinaCarregamento);
            rotinaCarregamento = null;
        }

        if (!ConfiguracaoCenasJogo.CenaExiste(nomeCena))
        {
            Debug.LogError("[LoadingScreen] Cena nao encontrada no Build Settings: " + nomeCena);
            Esconder();
            return;
        }

        CarregarCatalogoDeDicas();
        Mostrar(tipo);
        rotinaCarregamento = StartCoroutine(RotinaCarregar(nomeCena, tipo));
    }

    private IEnumerator RotinaCarregar(string nomeCena, LoadingRequestKind tipo)
    {
        carregamentoAtual = SceneManager.LoadSceneAsync(nomeCena);
        if (carregamentoAtual == null)
        {
            Debug.LogError("[LoadingScreen] Falha ao iniciar carregamento: " + nomeCena);
            Esconder();
            rotinaCarregamento = null;
            yield break;
        }

        carregamentoAtual.allowSceneActivation = false;
        aguardandoCena = true;

        int quantidade = Mathf.Min(Mathf.Max(0, quantidadeDicasPorCarregamento), dicas.Count);
        float intervalo = Mathf.Max(0.25f, segundosPorDica);

        if (quantidade == 0)
        {
            AtualizarProgresso();
            while (carregamentoAtual.progress < 0.9f)
            {
                AtualizarProgresso();
                yield return null;
            }
        }
        else
        {
            List<Texture2D> selecionadas = SelecionarDicasSemRepetir(quantidade);
            for (int i = 0; i < selecionadas.Count; i++)
            {
                MostrarDica(selecionadas[i], i + 1, selecionadas.Count, tipo);
                float terminaEm = Time.unscaledTime + intervalo;
                while (Time.unscaledTime < terminaEm)
                {
                    AtualizarProgresso();
                    yield return null;
                }
            }

            while (carregamentoAtual.progress < 0.9f)
            {
                MostrarAguardandoCena(tipo);
                AtualizarProgresso();
                yield return null;
            }
        }

        AtualizarProgressoFinal();
        carregamentoAtual.allowSceneActivation = true;
        rotinaCarregamento = null;
    }

    private List<Texture2D> SelecionarDicasSemRepetir(int quantidade)
    {
        List<Texture2D> embaralhadas = new List<Texture2D>(dicas);
        for (int i = embaralhadas.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Texture2D temporaria = embaralhadas[i];
            embaralhadas[i] = embaralhadas[j];
            embaralhadas[j] = temporaria;
        }

        if (embaralhadas.Count > quantidade)
        {
            embaralhadas.RemoveRange(quantidade, embaralhadas.Count - quantidade);
        }

        return embaralhadas;
    }

    private void CarregarCatalogoDeDicas()
    {
        dicas.Clear();
        Texture2D[] carregadas = Resources.LoadAll<Texture2D>("LoadingTips");
        if (carregadas == null)
        {
            return;
        }

        for (int i = 0; i < carregadas.Length; i++)
        {
            if (carregadas[i] != null)
            {
                dicas.Add(carregadas[i]);
            }
        }
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (!aguardandoCena)
        {
            return;
        }

        aguardandoCena = false;
        carregamentoAtual = null;
        Esconder();
    }

    private void Mostrar(LoadingRequestKind tipo)
    {
        if (raiz == null)
        {
            ConstruirInterface();
        }

        raiz.SetActive(true);
        titulo.text = tipo == LoadingRequestKind.Tutorial
            ? "TUTORIAL / PREPARANDO OPERACAO"
            : tipo == LoadingRequestKind.Save
                ? "CARREGANDO CAMPANHA"
                : "CAMPANHA / PREPARANDO OPERACAO";
        textoDica.text = dicas.Count > 0 ? "Dicas da operacao" : "Preparando partida...";
        progressoTexto.text = "CARREGANDO 0%";
        progressoBarra.fillAmount = 0f;
    }

    private void MostrarDica(Texture2D textura, int indice, int total, LoadingRequestKind tipo)
    {
        if (textura != null)
        {
            imagemDica.texture = textura;
        }

        // Os arquivos podem ter nomes tecnicos ou conter o nome da ferramenta
        // que gerou a arte. O jogador deve ver apenas a informacao do jogo.
        textoDica.text = "Dica de jogo";
        titulo.text = tipo == LoadingRequestKind.Tutorial
            ? "TUTORIAL / DICA " + indice + " DE " + total
            : "OPERACAO / DICA " + indice + " DE " + total;
    }

    private void MostrarAguardandoCena(LoadingRequestKind tipo)
    {
        titulo.text = tipo == LoadingRequestKind.Tutorial
            ? "TUTORIAL / FINALIZANDO"
            : "OPERACAO / FINALIZANDO";
        textoDica.text = "Finalizando a operacao...";
    }

    private void AtualizarProgresso()
    {
        if (carregamentoAtual == null || progressoBarra == null)
        {
            return;
        }

        float progresso = Mathf.Clamp01(carregamentoAtual.progress / 0.9f);
        progressoBarra.fillAmount = progresso;
        progressoTexto.text = "CARREGANDO " + Mathf.RoundToInt(progresso * 100f) + "%";
    }

    private void AtualizarProgressoFinal()
    {
        if (progressoBarra != null)
        {
            progressoBarra.fillAmount = 1f;
        }

        if (progressoTexto != null)
        {
            progressoTexto.text = "CARREGANDO 100%";
        }
    }

    private void Esconder()
    {
        if (raiz != null)
        {
            raiz.SetActive(false);
        }
    }

    private void ConstruirInterface()
    {
        if (canvas != null)
        {
            return;
        }

        fontePadrao = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        canvas = new GameObject("CanvasLoadingScreen").AddComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50000;
        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        raiz = new GameObject("LoadingScreenRoot");
        raiz.transform.SetParent(canvas.transform, false);
        RectTransform raizRect = raiz.AddComponent<RectTransform>();
        raizRect.anchorMin = Vector2.zero;
        raizRect.anchorMax = Vector2.one;
        raizRect.offsetMin = Vector2.zero;
        raizRect.offsetMax = Vector2.zero;

        Image fundo = raiz.AddComponent<Image>();
        fundo.color = new Color(0.005f, 0.012f, 0.018f, 1f);

        GameObject imagemObject = new GameObject("ImagemDica");
        imagemObject.transform.SetParent(raiz.transform, false);
        RectTransform imagemRect = imagemObject.AddComponent<RectTransform>();
        imagemRect.anchorMin = Vector2.zero;
        imagemRect.anchorMax = Vector2.one;
        imagemRect.offsetMin = Vector2.zero;
        imagemRect.offsetMax = Vector2.zero;
        imagemDica = imagemObject.AddComponent<RawImage>();
        imagemDica.color = Color.white;
        ajusteAspecto = imagemObject.AddComponent<AspectRatioFitter>();
        ajusteAspecto.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        ajusteAspecto.aspectRatio = 16f / 9f;

        // Faixa compacta: preserva a arte e nao cobre quase um terco da tela.
        Image sombra = CriarPainel("SombraInferior", raiz.transform, new Vector2(0f, 0f), new Vector2(1f, 0.14f), new Color(0.005f, 0.012f, 0.018f, 0.70f));
        sombra.transform.SetAsLastSibling();

        titulo = CriarTexto("Titulo", raiz.transform, "CARREGANDO", 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.98f, 1f, 1f), new Vector2(0.05f, 0.085f), new Vector2(0.70f, 0.135f));
        textoDica = CriarTexto("Dica", raiz.transform, "Preparando partida...", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.75f, 0.88f, 0.91f, 1f), new Vector2(0.05f, 0.035f), new Vector2(0.38f, 0.082f));
        progressoTexto = CriarTexto("ProgressoTexto", raiz.transform, "CARREGANDO 0%", 13, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.58f, 0.9f, 0.94f, 1f), new Vector2(0.75f, 0.085f), new Vector2(0.95f, 0.135f));

        Image barraFundo = CriarPainel("BarraFundo", raiz.transform, new Vector2(0.05f, 0.018f), new Vector2(0.72f, 0.032f), new Color(0.06f, 0.12f, 0.15f, 0.94f));
        Image barraPreenchida = CriarPainel("BarraPreenchida", barraFundo.transform, Vector2.zero, new Vector2(1f, 1f), new Color(0.23f, 0.83f, 0.9f, 0.95f));
        barraPreenchida.type = Image.Type.Filled;
        barraPreenchida.fillMethod = Image.FillMethod.Horizontal;
        barraPreenchida.fillOrigin = 0;
        barraPreenchida.fillAmount = 0f;
        progressoBarra = barraPreenchida;
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

    private Text CriarTexto(string nome, Transform pai, string conteudo, int tamanho, FontStyle estilo, TextAnchor alinhamento, Color cor, Vector2 ancoraMin, Vector2 ancoraMax)
    {
        GameObject objeto = new GameObject(nome);
        objeto.transform.SetParent(pai, false);
        RectTransform rect = objeto.AddComponent<RectTransform>();
        rect.anchorMin = ancoraMin;
        rect.anchorMax = ancoraMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text texto = objeto.AddComponent<Text>();
        texto.text = conteudo;
        texto.font = fontePadrao;
        texto.fontSize = tamanho;
        texto.fontStyle = estilo;
        texto.alignment = alinhamento;
        texto.color = cor;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Truncate;
        return texto;
    }
}
