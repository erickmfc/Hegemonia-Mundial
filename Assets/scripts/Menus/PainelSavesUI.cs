using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PainelSavesUI : MonoBehaviour
{
    private SistemaSaveGame sistema;
    private bool modoSalvar;
    private Action<string> aoCarregar;
    private Action aoFechar;
    private RectTransform lista;
    private InputField nomeNovoSave;
    private Font fonte;

    public static PainelSavesUI Abrir(
        Transform parent,
        SistemaSaveGame sistemaSave,
        bool salvar,
        Action<string> carregar,
        Action fechar = null)
    {
        PainelSavesUI existente = parent != null ? parent.GetComponentInChildren<PainelSavesUI>(true) : null;
        if (existente != null) Destroy(existente.gameObject);

        GameObject objeto = new GameObject("PainelGerenciadorSaves");
        objeto.transform.SetParent(parent, false);
        PainelSavesUI painel = objeto.AddComponent<PainelSavesUI>();
        painel.sistema = sistemaSave;
        painel.modoSalvar = salvar;
        painel.aoCarregar = carregar;
        painel.aoFechar = fechar;
        painel.Construir();
        return painel;
    }

    private void Construir()
    {
        fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform raiz = gameObject.AddComponent<RectTransform>();
        raiz.anchorMin = Vector2.zero;
        raiz.anchorMax = Vector2.one;
        raiz.offsetMin = Vector2.zero;
        raiz.offsetMax = Vector2.zero;
        Image bloqueio = gameObject.AddComponent<Image>();
        bloqueio.color = new Color(0f, 0.02f, 0.03f, 0.88f);

        RectTransform painel = CriarPainel(transform, "JanelaSaves", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 720f), new Color(0.035f, 0.075f, 0.095f, 0.99f));
        Outline borda = painel.gameObject.AddComponent<Outline>();
        borda.effectColor = new Color(0.22f, 0.8f, 0.9f, 0.72f);
        borda.effectDistance = new Vector2(2f, -2f);

        CriarTexto(painel, modoSalvar ? "SALVAR PARTIDA" : "CARREGAR PARTIDA", 30, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -24f), new Vector2(-180f, 58f));
        CriarTexto(painel, "Escolha uma partida. Você pode renomear ou excluir cada slot.", 15, FontStyle.Normal,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -78f), new Vector2(-60f, 32f));
        CriarBotao(painel, "FECHAR", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(130f, 46f), Fechar, new Color(0.3f, 0.08f, 0.1f, 1f));

        float topoLista = -126f;
        if (modoSalvar)
        {
            RectTransform novo = CriarPainel(painel, "NovoSave", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -122f), new Vector2(-56f, 68f), new Color(0.05f, 0.14f, 0.17f, 1f));
            novo.anchoredPosition = new Vector2(0f, -122f);
            nomeNovoSave = CriarCampo(novo, "Nome da partida", new Vector2(18f, -12f), new Vector2(580f, 44f));
            CriarBotao(novo, "CRIAR NOVO SAVE", new Vector2(1f, 1f), new Vector2(-18f, -12f), new Vector2(270f, 44f), SalvarNovo, new Color(0.03f, 0.36f, 0.44f, 1f));
            topoLista = -208f;
        }

        RectTransform viewport = CriarPainel(painel, "Viewport", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 28f), new Vector2(-56f, topoLista - 28f), new Color(0.015f, 0.035f, 0.045f, 0.82f));
        viewport.anchoredPosition = Vector2.zero;
        viewport.sizeDelta = Vector2.zero;
        viewport.offsetMin = new Vector2(28f, 28f);
        viewport.offsetMax = new Vector2(-28f, topoLista);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 36f;

        GameObject contentObject = new GameObject("ListaSaves");
        contentObject.transform.SetParent(viewport, false);
        lista = contentObject.AddComponent<RectTransform>();
        lista.anchorMin = new Vector2(0f, 1f);
        lista.anchorMax = new Vector2(1f, 1f);
        lista.pivot = new Vector2(0.5f, 1f);
        lista.offsetMin = Vector2.zero;
        lista.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 10f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = lista;

        AtualizarLista();
    }

    private void AtualizarLista()
    {
        if (lista == null || sistema == null) return;
        for (int i = lista.childCount - 1; i >= 0; i--)
        {
            Transform filho = lista.GetChild(i);
            filho.SetParent(null, false);
            Destroy(filho.gameObject);
        }

        IReadOnlyList<SaveSlotInfo> saves = sistema.ListarSaves();
        if (saves.Count == 0)
        {
            Text vazio = CriarTexto(lista, "Nenhuma partida salva.", 18, FontStyle.Italic,
                new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 70f));
            vazio.alignment = TextAnchor.MiddleCenter;
            return;
        }

        for (int i = 0; i < saves.Count; i++) CriarLinhaSave(saves[i]);
    }

    private void CriarLinhaSave(SaveSlotInfo info)
    {
        RectTransform linha = CriarPainel(lista, "Save_" + info.nome, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 116f), new Color(0.055f, 0.12f, 0.145f, 1f));
        LayoutElement tamanho = linha.gameObject.AddComponent<LayoutElement>();
        tamanho.preferredHeight = 116f;
        tamanho.minHeight = 116f;

        InputField nome = CriarCampo(linha, info.nome, new Vector2(16f, -14f), new Vector2(390f, 42f));
        string data = FormatarData(info.salvoEmUtc);
        CriarTexto(linha, (string.IsNullOrWhiteSpace(info.mapa) ? "Mapa desconhecido" : info.mapa) + "  |  " + data,
            13, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 12f), new Vector2(430f, 28f));

        float x = -16f;
        Button excluir = CriarBotao(linha, "EXCLUIR", new Vector2(1f, 1f), new Vector2(x, -14f), new Vector2(120f, 42f), null, new Color(0.34f, 0.07f, 0.09f, 1f));
        x -= 130f;
        bool confirmarExclusao = false;
        excluir.onClick.AddListener(() =>
        {
            if (!confirmarExclusao)
            {
                confirmarExclusao = true;
                Text rotulo = excluir.GetComponentInChildren<Text>();
                if (rotulo != null) rotulo.text = "CONFIRMAR";
                return;
            }
            sistema.ExcluirSave(info.id);
            AtualizarLista();
        });

        CriarBotao(linha, "RENOMEAR", new Vector2(1f, 1f), new Vector2(x, -14f), new Vector2(130f, 42f), () =>
        {
            sistema.RenomearSave(info.id, nome.text);
            AtualizarLista();
        }, new Color(0.12f, 0.24f, 0.29f, 1f));
        x -= 140f;

        string acao = modoSalvar ? "SALVAR AQUI" : "CARREGAR";
        CriarBotao(linha, acao, new Vector2(1f, 1f), new Vector2(x, -14f), new Vector2(150f, 42f), () =>
        {
            if (modoSalvar)
            {
                sistema.SelecionarSave(info.id);
                sistema.SalvarJogo(nome.text);
                AtualizarLista();
            }
            else
            {
                aoCarregar?.Invoke(info.id);
            }
        }, new Color(0.03f, 0.36f, 0.44f, 1f));
    }

    private void SalvarNovo()
    {
        sistema.SalvarJogo(nomeNovoSave != null ? nomeNovoSave.text : "Nova partida");
        AtualizarLista();
        if (nomeNovoSave != null) nomeNovoSave.text = string.Empty;
    }

    private void Fechar()
    {
        aoFechar?.Invoke();
        Destroy(gameObject);
    }

    private RectTransform CriarPainel(Transform parent, string nome, Vector2 min, Vector2 max, Vector2 pos, Vector2 tamanho, Color cor)
    {
        GameObject objeto = new GameObject(nome);
        objeto.transform.SetParent(parent, false);
        RectTransform rect = objeto.AddComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(min.x == max.x ? min.x : 0.5f, min.y == max.y ? min.y : 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = tamanho;
        objeto.AddComponent<Image>().color = cor;
        return rect;
    }

    private Text CriarTexto(Transform parent, string texto, int tamanho, FontStyle estilo, Vector2 min, Vector2 max, Vector2 pos, Vector2 delta)
    {
        GameObject objeto = new GameObject("Texto");
        objeto.transform.SetParent(parent, false);
        RectTransform rect = objeto.AddComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(min.x == max.x ? min.x : 0.5f, min.y == max.y ? min.y : 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = delta;
        Text label = objeto.AddComponent<Text>();
        label.font = fonte;
        label.text = texto;
        label.fontSize = tamanho;
        label.fontStyle = estilo;
        label.color = new Color(0.9f, 0.97f, 1f, 1f);
        label.alignment = TextAnchor.MiddleLeft;
        return label;
    }

    private InputField CriarCampo(Transform parent, string valor, Vector2 pos, Vector2 tamanho)
    {
        RectTransform fundo = CriarPainel(parent, "CampoNome", new Vector2(0f, 1f), new Vector2(0f, 1f), pos, tamanho, new Color(0.015f, 0.035f, 0.045f, 1f));
        Text texto = CriarTexto(fundo, valor, 16, FontStyle.Normal, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-24f, 0f));
        InputField campo = fundo.gameObject.AddComponent<InputField>();
        campo.textComponent = texto;
        campo.text = valor;
        campo.characterLimit = 48;
        return campo;
    }

    private Button CriarBotao(Transform parent, string texto, Vector2 ancora, Vector2 pos, Vector2 tamanho, Action acao, Color cor)
    {
        RectTransform fundo = CriarPainel(parent, texto + "Button", ancora, ancora, pos, tamanho, cor);
        Text label = CriarTexto(fundo, texto, 14, FontStyle.Bold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        label.alignment = TextAnchor.MiddleCenter;
        Button botao = fundo.gameObject.AddComponent<Button>();
        if (acao != null) botao.onClick.AddListener(() => acao());
        return botao;
    }

    private static string FormatarData(string valor)
    {
        if (DateTime.TryParse(valor, out DateTime data)) return data.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        return "data desconhecida";
    }
}
