using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class AudioSettingsPanelUI
{
    private sealed class LinhaAudio
    {
        public AudioChannel Canal;
        public Slider Slider;
        public Button BotaoMute;
    }

    private static GameObject painelAtual;
    private static Font fonte;
    private static readonly Color CorPainel = new Color(0.025f, 0.07f, 0.09f, 0.98f);
    private static readonly Color CorBotao = new Color(0.055f, 0.15f, 0.18f, 0.98f);
    private static readonly Color CorDestaque = new Color(0.08f, 0.38f, 0.45f, 0.98f);
    private static readonly Color CorTexto = new Color(0.9f, 0.98f, 1f, 1f);
    private static readonly Color CorTextoSuave = new Color(0.65f, 0.84f, 0.88f, 1f);

    public static bool EstaAberto => painelAtual != null;

    public static void Abrir(Transform pai)
    {
        if (pai == null)
        {
            return;
        }

        if (painelAtual != null)
        {
            return;
        }

        fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        painelAtual = new GameObject("PainelConfiguracoesAudio");
        painelAtual.transform.SetParent(pai, false);
        RectTransform raizRect = painelAtual.AddComponent<RectTransform>();
        raizRect.anchorMin = Vector2.zero;
        raizRect.anchorMax = Vector2.one;
        raizRect.offsetMin = Vector2.zero;
        raizRect.offsetMax = Vector2.zero;
        Image overlay = painelAtual.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);

        RectTransform painel = CriarPainel("PainelCentral", painelAtual.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 760f), CorPainel);
        Outline contorno = painel.gameObject.AddComponent<Outline>();
        contorno.effectColor = new Color(0.3f, 0.86f, 0.94f, 0.46f);
        contorno.effectDistance = new Vector2(2f, -2f);

        CriarTexto("Titulo", painel, "CONFIGURACOES DE AUDIO", 28, FontStyle.Bold, TextAnchor.UpperCenter, CorTexto, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(-64f, 44f));
        CriarTexto("Descricao", painel, "Ajuste cada categoria sem alterar a jogabilidade.", 14, FontStyle.Normal, TextAnchor.UpperCenter, CorTextoSuave, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -82f), new Vector2(-64f, 28f));

        AudioChannel[] canais = (AudioChannel[])Enum.GetValues(typeof(AudioChannel));
        List<LinhaAudio> linhas = new List<LinhaAudio>();
        for (int i = 0; i < canais.Length; i++)
        {
            float y = -142f - (i * 86f);
            LinhaAudio linha = CriarLinha(painel, canais[i], y);
            linhas.Add(linha);
        }

        Button padrao = CriarBotao(painel, "RESTAURAR PADROES", new Vector2(-122f, 48f), new Vector2(250f, 54f), CorBotao, () =>
        {
            AudioSettingsService.RestaurarPadroes();
            AtualizarLinhas(linhas);
        });
        Button fechar = CriarBotao(painel, "FECHAR", new Vector2(122f, 48f), new Vector2(180f, 54f), CorDestaque, Fechar);
    }

    public static void Fechar()
    {
        if (painelAtual == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(painelAtual);
        painelAtual = null;
    }

    private static LinhaAudio CriarLinha(Transform pai, AudioChannel canal, float y)
    {
        string nome = NomeCanal(canal);
        CriarTexto("Label_" + canal, pai, nome, 18, FontStyle.Bold, TextAnchor.MiddleLeft, CorTexto, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, y), new Vector2(180f, 28f));

        RectTransform sliderRect = CriarRect("Slider_" + canal, pai, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(280f, y), new Vector2(280f, 24f));
        Image fundo = sliderRect.gameObject.AddComponent<Image>();
        fundo.color = new Color(0.1f, 0.2f, 0.23f, 1f);
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = AudioSettingsService.ObterVolume(canal);
        slider.direction = Slider.Direction.LeftToRight;

        RectTransform preenchimento = CriarRect("Fill", sliderRect, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        Image preenchimentoImagem = preenchimento.gameObject.AddComponent<Image>();
        preenchimentoImagem.color = new Color(0.23f, 0.82f, 0.9f, 0.96f);
        slider.fillRect = preenchimento;

        RectTransform alca = CriarRect("Handle", sliderRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24f, 34f));
        Image alcaImagem = alca.gameObject.AddComponent<Image>();
        alcaImagem.color = Color.white;
        slider.handleRect = alca;
        slider.targetGraphic = alcaImagem;

        slider.onValueChanged.AddListener(valor => AudioSettingsService.DefinirVolume(canal, valor));

        // O botao usa ancora centralizada; 250 px deixa o centro dele na
        // extremidade direita do slider sem sair do painel de 720 px.
        Button mute = CriarBotao(pai, "", new Vector2(250f, y), new Vector2(82f, 34f), CorBotao, null, true);
        mute.onClick.AddListener(() =>
        {
            AudioSettingsService.DefinirSilenciado(canal, !AudioSettingsService.EstaSilenciado(canal));
            AtualizarBotaoMute(mute, canal);
        });
        AtualizarBotaoMute(mute, canal);

        return new LinhaAudio { Canal = canal, Slider = slider, BotaoMute = mute };
    }

    private static void AtualizarLinhas(List<LinhaAudio> linhas)
    {
        for (int i = 0; i < linhas.Count; i++)
        {
            LinhaAudio linha = linhas[i];
            linha.Slider.value = AudioSettingsService.ObterVolume(linha.Canal);
            AtualizarBotaoMute(linha.BotaoMute, linha.Canal);
        }
    }

    private static void AtualizarBotaoMute(Button botao, AudioChannel canal)
    {
        if (botao == null)
        {
            return;
        }

        Text texto = botao.GetComponentInChildren<Text>();
        if (texto != null)
        {
            texto.text = AudioSettingsService.EstaSilenciado(canal) ? "MUDO" : "ATIVO";
        }
    }

    private static string NomeCanal(AudioChannel canal)
    {
        switch (canal)
        {
            case AudioChannel.Geral: return "GERAL";
            case AudioChannel.Musica: return "MUSICA";
            case AudioChannel.Efeitos: return "EFEITOS";
            case AudioChannel.Ambiente: return "AMBIENTE";
            case AudioChannel.Voz: return "VOZ";
            default: return canal.ToString().ToUpperInvariant();
        }
    }

    private static RectTransform CriarRect(string nome, Transform pai, Vector2 ancoraMin, Vector2 ancoraMax, Vector2 posicao, Vector2 tamanho)
    {
        GameObject objeto = new GameObject(nome);
        objeto.transform.SetParent(pai, false);
        RectTransform rect = objeto.AddComponent<RectTransform>();
        rect.anchorMin = ancoraMin;
        rect.anchorMax = ancoraMax;
        rect.pivot = new Vector2(Mathf.Approximately(ancoraMin.x, ancoraMax.x) ? ancoraMin.x : 0.5f, Mathf.Approximately(ancoraMin.y, ancoraMax.y) ? ancoraMin.y : 0.5f);
        rect.anchoredPosition = posicao;
        rect.sizeDelta = tamanho;
        return rect;
    }

    private static RectTransform CriarPainel(string nome, Transform pai, Vector2 ancoraMin, Vector2 ancoraMax, Vector2 posicao, Vector2 tamanho, Color cor)
    {
        RectTransform rect = CriarRect(nome, pai, ancoraMin, ancoraMax, posicao, tamanho);
        rect.gameObject.AddComponent<Image>().color = cor;
        return rect;
    }

    private static Text CriarTexto(string nome, Transform pai, string conteudo, int tamanho, FontStyle estilo, TextAnchor alinhamento, Color cor, Vector2 ancoraMin, Vector2 ancoraMax, Vector2 posicao, Vector2 dimensao)
    {
        RectTransform rect = CriarRect(nome, pai, ancoraMin, ancoraMax, posicao, dimensao);
        Text texto = rect.gameObject.AddComponent<Text>();
        texto.font = fonte;
        texto.text = conteudo;
        texto.fontSize = tamanho;
        texto.fontStyle = estilo;
        texto.alignment = alinhamento;
        texto.color = cor;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Truncate;
        return texto;
    }

    private static Button CriarBotao(Transform pai, string texto, Vector2 posicao, Vector2 tamanho, Color cor, UnityEngine.Events.UnityAction acao, bool ancorarNoTopo = false)
    {
        float ancoraY = ancorarNoTopo ? 1f : 0f;
        RectTransform rect = CriarRect("BotaoAudio_" + Guid.NewGuid().ToString("N"), pai, new Vector2(0.5f, ancoraY), new Vector2(0.5f, ancoraY), posicao, tamanho);
        Image imagem = rect.gameObject.AddComponent<Image>();
        imagem.color = cor;
        Button botao = rect.gameObject.AddComponent<Button>();
        botao.targetGraphic = imagem;
        ColorBlock cores = botao.colors;
        cores.normalColor = cor;
        cores.highlightedColor = new Color(Mathf.Min(1f, cor.r + 0.08f), Mathf.Min(1f, cor.g + 0.12f), Mathf.Min(1f, cor.b + 0.12f), cor.a);
        cores.pressedColor = new Color(cor.r * 0.82f, cor.g * 0.82f, cor.b * 0.82f, cor.a);
        botao.colors = cores;
        if (acao != null)
        {
            botao.onClick.AddListener(acao);
        }

        CriarTexto("Texto", rect, texto, 14, FontStyle.Bold, TextAnchor.MiddleCenter, CorTexto, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return botao;
    }
}
