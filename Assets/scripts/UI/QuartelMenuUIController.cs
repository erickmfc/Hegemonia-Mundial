using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Interface administrativa do Quartel em UI Toolkit, alimentada por estado de jogo.
///
/// Este componente nao assume o controle de unidades, ordens ou selecao. Ele
/// apenas apresenta os dados do GerenciadorQuartel e encaminha acoes para a
/// API publica do gerenciador. O IMGUI antigo continua disponivel quando
/// usarPainelQuartelUIToolkit estiver desativado.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuartelMenuUIController : MonoBehaviour
{
    private static QuartelMenuUIController painelAberto;

    private GerenciadorQuartel quartel;
    private QuartelAdministracaoRuntime administracao;
    private QuartelAdministracaoRuntime.Snapshot snapshot;
    private UIDocument documento;
    private PanelSettings panelSettingsRuntime;
    private VisualElement root;
    private VisualElement overlay;
    private VisualElement painel;
    private VisualElement conteudo;
    private VisualElement carta;
    private CartaTerrenoRenderer cartaTerrenoRenderer;
    private QuartelCartaTopograficaView cartaTopograficaView;
    private string cartaUnidadeSelecionadaId = string.Empty;
    private string cartaMissilSelecionadoId = string.Empty;
    private bool cartaVista3D;
    private Label titulo;
    private Label subtitulo;
    private Label status;
    private Label metricas;
    private readonly List<Button> botoesAbas = new List<Button>();
    private readonly string[] nomesAbas =
    {
        "TROPAS", "EFETIVO", "RECRUTAMENTO", "FOLHA MILITAR", "TRIPULACOES",
        "RESGATE", "COMUNICACOES", "CARTA NAUTICA", "ARSENAL"
    };

    private int abaAtual;
    private int quantidadeRecrutamentoUI = 1;
    private bool aberto;
    private bool pronto;
    private float proximaAtualizacao;
    private readonly List<IdentidadeUnidade> unidadesRegistradas = new List<IdentidadeUnidade>(256);
    private readonly List<ControleUnidade> controlesRegistrados = new List<ControleUnidade>(256);
    private readonly List<ControleAviao> avioesRegistrados = new List<ControleAviao>(64);

    private GUIStyle designerFundo;
    private GUIStyle designerPainel;
    private GUIStyle designerCabecalho;
    private GUIStyle designerBarraLateral;
    private GUIStyle designerMarca;
    private GUIStyle designerSubmarca;
    private GUIStyle designerTitulo;
    private GUIStyle designerSubtitulo;
    private GUIStyle designerRotulo;
    private GUIStyle designerValor;
    private GUIStyle designerSecao;
    private GUIStyle designerCartao;
    private GUIStyle designerNavegacao;
    private GUIStyle designerNavegacaoAtiva;
    private GUIStyle designerBotao;
    private GUIStyle designerBotaoAtivo;
    private GUIStyle designerMapa;
    private GUIStyle designerGrade;
    private GUIStyle designerStatus;
    private GUIStyle designerStatusAlerta;
    private GUIStyle designerPequeno;
    private Texture2D texturaFundo;
    private Texture2D texturaPainel;
    private Texture2D texturaCabecalho;
    private Texture2D texturaLateral;
    private Texture2D texturaCartao;
    private Texture2D texturaNavegacao;
    private Texture2D texturaNavegacaoAtiva;
    private Texture2D texturaBotao;
    private Texture2D texturaMapa;
    private Texture2D texturaGrade;
    private bool designerInicializado;

    private readonly string[] nomesNavegacaoDesigner =
    {
        "TROPAS", "EFETIVO MILITAR", "RECRUTAMENTO E FORMACAO", "FOLHA MILITAR",
        "TRIPULACOES", "RESGATES", "COMUNICACAO E INTERCEPTACAO", "CARTA NAUTICA", "ARSENAL"
    };

    private readonly string[] descricoesNavegacaoDesigner =
    {
        "Unidades armazenadas, aeronaves conectadas e acoes de desdobramento.",
        "Quadro nacional de efetivo e disponibilidade administrativa.",
        "Alistamento, treinamento e distribuicao para as forcas corretas.",
        "Folha, pagamento e custo real do pessoal administrado.",
        "Tripulacao minima, alocacao ativa e unidades inoperantes.",
        "Alertas de dano, perdas e protocolo de recuperacao.",
        "Contatos, sensores e retransmissao de informacoes.",
        "Leitura operacional da cobertura e dos contatos conhecidos.",
        "Misseis, municao e saldo do GerenciadorRecursos."
    };

    public bool EstaAberto => aberto;
    public bool EstaVisivel => aberto && root != null && root.style.display == DisplayStyle.Flex;

    private bool PrecisaFallbackIMGUI()
    {
        if (root == null) return true;
        float largura = root.resolvedStyle.width;
        float altura = root.resolvedStyle.height;
        return float.IsNaN(largura) || float.IsNaN(altura) || largura < 1f || altura < 1f;
    }

    private void OnGUI()
    {
        if (!aberto || !PrecisaFallbackIMGUI()) return;

        GarantirDesignerIMGUI();
        snapshot = administracao != null ? administracao.ObterSnapshot() : snapshot;
        GUI.depth = -1000;

        Rect tela = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.Box(tela, GUIContent.none, designerFundo);

        float margem = Mathf.Clamp(Screen.width * 0.012f, 8f, 24f);
        Rect frame = new Rect(margem, margem, Screen.width - margem * 2f, Screen.height - margem * 2f);
        GUI.BeginGroup(frame);
        GUI.Box(new Rect(0f, 0f, frame.width, frame.height), GUIContent.none, designerPainel);

        float lateral = Mathf.Clamp(frame.width * 0.185f, 220f, 285f);
        float cabecalho = Mathf.Clamp(frame.height * 0.105f, 66f, 86f);
        DesenharCabecalhoDesigner(frame.width, lateral, cabecalho);
        DesenharBarraLateralDesigner(lateral, frame.height, cabecalho);

        Rect conteudoDesigner = new Rect(lateral, cabecalho, frame.width - lateral, frame.height - cabecalho);
        DesenharConteudoDesigner(conteudoDesigner);
        GUI.EndGroup();
    }

    private Texture2D CriarTexturaDesigner(Color cor)
    {
        Texture2D textura = new Texture2D(1, 1);
        textura.SetPixel(0, 0, cor);
        textura.Apply();
        return textura;
    }

    private GUIStyle CriarEstiloDesigner(Texture2D fundo, Color texto, int tamanho, TextAnchor alinhamento, FontStyle fonte = FontStyle.Normal)
    {
        GUIStyle estilo = new GUIStyle(GUI.skin.label)
        {
            fontSize = tamanho,
            alignment = alinhamento,
            fontStyle = fonte,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
        estilo.normal.background = fundo;
        estilo.normal.textColor = texto;
        estilo.hover.background = fundo;
        estilo.hover.textColor = texto;
        estilo.padding = new RectOffset(10, 10, 6, 6);
        return estilo;
    }

    private void GarantirDesignerIMGUI()
    {
        if (designerInicializado) return;

        texturaFundo = CriarTexturaDesigner(new Color(0.005f, 0.014f, 0.024f, 0.90f));
        texturaPainel = CriarTexturaDesigner(new Color(0.012f, 0.035f, 0.052f, 0.98f));
        texturaCabecalho = CriarTexturaDesigner(new Color(0.018f, 0.070f, 0.100f, 1f));
        texturaLateral = CriarTexturaDesigner(new Color(0.008f, 0.028f, 0.045f, 1f));
        texturaCartao = CriarTexturaDesigner(new Color(0.018f, 0.075f, 0.105f, 0.98f));
        texturaNavegacao = CriarTexturaDesigner(new Color(0.010f, 0.042f, 0.065f, 1f));
        texturaNavegacaoAtiva = CriarTexturaDesigner(new Color(0.015f, 0.155f, 0.225f, 1f));
        texturaBotao = CriarTexturaDesigner(new Color(0.020f, 0.235f, 0.310f, 1f));
        texturaMapa = CriarTexturaDesigner(new Color(0.008f, 0.065f, 0.090f, 1f));
        texturaGrade = CriarTexturaDesigner(new Color(0.035f, 0.235f, 0.285f, 0.60f));

        designerFundo = CriarEstiloDesigner(texturaFundo, Color.white, 12, TextAnchor.MiddleCenter);
        designerPainel = CriarEstiloDesigner(texturaPainel, Color.white, 12, TextAnchor.UpperLeft);
        designerCabecalho = CriarEstiloDesigner(texturaCabecalho, Color.white, 12, TextAnchor.MiddleLeft);
        designerBarraLateral = CriarEstiloDesigner(texturaLateral, Color.white, 12, TextAnchor.UpperLeft);
        designerMarca = CriarEstiloDesigner(null, new Color(0.84f, 0.92f, 0.95f), 21, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerSubmarca = CriarEstiloDesigner(null, new Color(0.15f, 0.72f, 0.90f), 10, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerTitulo = CriarEstiloDesigner(null, new Color(0.86f, 0.93f, 0.96f), 23, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerSubtitulo = CriarEstiloDesigner(null, new Color(0.42f, 0.69f, 0.76f), 11, TextAnchor.MiddleLeft);
        designerRotulo = CriarEstiloDesigner(null, new Color(0.50f, 0.72f, 0.78f), 10, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerValor = CriarEstiloDesigner(null, new Color(0.90f, 0.96f, 0.98f), 22, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerSecao = CriarEstiloDesigner(null, new Color(0.16f, 0.79f, 0.98f), 14, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerCartao = CriarEstiloDesigner(texturaCartao, new Color(0.86f, 0.94f, 0.96f), 12, TextAnchor.UpperLeft);
        designerNavegacao = CriarEstiloDesigner(texturaNavegacao, new Color(0.71f, 0.82f, 0.86f), 12, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerNavegacaoAtiva = CriarEstiloDesigner(texturaNavegacaoAtiva, Color.white, 12, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerBotao = CriarEstiloDesigner(texturaBotao, Color.white, 12, TextAnchor.MiddleCenter, FontStyle.Bold);
        designerBotaoAtivo = CriarEstiloDesigner(texturaNavegacaoAtiva, Color.white, 12, TextAnchor.MiddleCenter, FontStyle.Bold);
        designerMapa = CriarEstiloDesigner(texturaMapa, Color.white, 12, TextAnchor.MiddleCenter);
        designerGrade = CriarEstiloDesigner(texturaGrade, Color.white, 1, TextAnchor.MiddleCenter);
        designerStatus = CriarEstiloDesigner(null, new Color(0.35f, 0.90f, 0.35f), 12, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerStatusAlerta = CriarEstiloDesigner(null, new Color(1f, 0.68f, 0.20f), 12, TextAnchor.MiddleLeft, FontStyle.Bold);
        designerPequeno = CriarEstiloDesigner(null, new Color(0.55f, 0.72f, 0.76f), 10, TextAnchor.MiddleLeft);

        designerMarca.padding = new RectOffset(0, 0, 0, 0);
        designerSubmarca.padding = new RectOffset(0, 0, 0, 0);
        designerTitulo.padding = new RectOffset(0, 0, 0, 0);
        designerSubtitulo.padding = new RectOffset(0, 0, 0, 0);
        designerRotulo.padding = new RectOffset(0, 0, 0, 0);
        designerValor.padding = new RectOffset(0, 0, 0, 0);
        designerSecao.padding = new RectOffset(0, 0, 0, 0);
        designerStatus.padding = new RectOffset(0, 0, 0, 0);
        designerStatusAlerta.padding = new RectOffset(0, 0, 0, 0);
        designerPequeno.padding = new RectOffset(0, 0, 0, 0);
        designerMarca.wordWrap = false;
        designerSubmarca.wordWrap = false;
        designerTitulo.wordWrap = false;
        designerSubtitulo.wordWrap = false;
        designerRotulo.wordWrap = false;
        designerValor.wordWrap = false;
        designerSecao.wordWrap = false;
        designerInicializado = true;
    }

    private void DesenharCabecalhoDesigner(float largura, float lateral, float altura)
    {
        GUI.Box(new Rect(lateral, 0f, largura - lateral, altura), GUIContent.none, designerCabecalho);
        string nome = quartel != null ? quartel.name.ToUpperInvariant() : "QUARTEL";
        GUI.Label(new Rect(lateral + 28f, 8f, largura - lateral - 370f, 31f), "QUARTEL  —  COMANDO MILITAR", designerTitulo);
        GUI.Label(new Rect(lateral + 30f, 42f, largura - lateral - 370f, 18f), "ESTADO-MAIOR NACIONAL  |  " + nome, designerSubtitulo);

        float direita = largura - 330f;
        GUI.Label(new Rect(direita, 14f, 100f, 16f), "DATA", designerRotulo);
        GUI.Label(new Rect(direita, 33f, 100f, 22f), DateTime.Now.ToString("dd MMM yyyy"), designerSubtitulo);
        GUI.Label(new Rect(direita + 112f, 14f, 100f, 16f), "HORA", designerRotulo);
        GUI.Label(new Rect(direita + 112f, 33f, 100f, 22f), DateTime.Now.ToString("HH:mm:ss"), designerSubtitulo);
        string prontidao = snapshot != null && snapshot.unidadesInoperantes > 0 ? "ATENCAO" : "OPERACIONAL";
        GUI.Label(new Rect(direita + 224f, 14f, 110f, 16f), "PRONTIDAO", designerRotulo);
        GUI.Label(new Rect(direita + 224f, 33f, 110f, 22f), prontidao, prontidao == "ATENCAO" ? designerStatusAlerta : designerStatus);
    }

    private void DesenharBarraLateralDesigner(float largura, float altura, float inicio)
    {
        GUI.Box(new Rect(0f, 0f, largura, altura), GUIContent.none, designerBarraLateral);
        GUI.Label(new Rect(24f, 16f, largura - 32f, 30f), "◈  QUARTEL", designerMarca);
        GUI.Label(new Rect(26f, 47f, largura - 32f, 18f), "COMANDO MILITAR", designerSubmarca);
        GUI.Box(new Rect(0f, inicio - 1f, largura, 1f), GUIContent.none, designerGrade);

        float y = inicio + 22f;
        for (int i = 0; i < nomesNavegacaoDesigner.Length; i++)
        {
            Rect botao = new Rect(10f, y, largura - 20f, 45f);
            GUIStyle estilo = i == abaAtual ? designerNavegacaoAtiva : designerNavegacao;
            if (GUI.Button(botao, "  " + SimboloNavegacao(i) + "   " + nomesNavegacaoDesigner[i], estilo))
            {
                abaAtual = i;
                AtualizarPainel();
            }
            y += 49f;
        }

        Rect pais = new Rect(14f, altura - 72f, largura - 28f, 56f);
        GUI.Box(pais, GUIContent.none, designerCartao);
        GUI.Label(new Rect(pais.x + 12f, pais.y + 8f, pais.width - 24f, 18f), "REPUBLICA ATLANTICA", designerSubtitulo);
        GUI.Label(new Rect(pais.x + 12f, pais.y + 29f, pais.width - 24f, 18f), "COMANDO CONJUNTO", designerPequeno);
    }

    private string SimboloNavegacao(int indice)
    {
        switch (indice)
        {
            case 0: return "✣";
            case 1: return "♙";
            case 2: return "↗";
            case 3: return "▤";
            case 4: return "⚓";
            case 5: return "◉";
            case 6: return "⌁";
            case 7: return "⌖";
            default: return "▣";
        }
    }

    private void DesenharConteudoDesigner(Rect area)
    {
        GUI.BeginGroup(area);
        GUI.Label(new Rect(28f, 18f, area.width - 56f, 30f), nomesNavegacaoDesigner[abaAtual], designerTitulo);
        GUI.Label(new Rect(30f, 49f, area.width - 60f, 28f), descricoesNavegacaoDesigner[abaAtual], designerSubtitulo);
        GUI.Box(new Rect(28f, 82f, area.width - 56f, 1f), GUIContent.none, designerGrade);
        Rect pagina = new Rect(28f, 96f, area.width - 56f, area.height - 108f);
        switch (abaAtual)
        {
            case 0: DesenharTropasDesigner(pagina); break;
            case 1: DesenharEfetivoDesigner(pagina); break;
            case 2: DesenharRecrutamentoDesigner(pagina); break;
            case 3: DesenharFolhaDesigner(pagina); break;
            case 4: DesenharTripulacoesDesigner(pagina); break;
            case 5: DesenharResgateDesigner(pagina); break;
            case 6: DesenharComunicacoesDesigner(pagina); break;
            case 7: DesenharCartaDesigner(pagina); break;
            case 8: DesenharArsenalDesigner(pagina); break;
        }
        GUI.EndGroup();
    }

    private void DesenharMetricasDesigner(Rect area, string[] rotulos, string[] valores)
    {
        int quantidade = Mathf.Min(rotulos.Length, valores.Length);
        float espacamento = 8f;
        float largura = (area.width - espacamento * (quantidade - 1)) / quantidade;
        for (int i = 0; i < quantidade; i++)
        {
            Rect cartao = new Rect(area.x + i * (largura + espacamento), area.y, largura, area.height);
            GUI.Box(cartao, GUIContent.none, designerCartao);
            GUI.Label(new Rect(cartao.x + 10f, cartao.y + 8f, cartao.width - 20f, 18f), rotulos[i], designerRotulo);
            GUI.Label(new Rect(cartao.x + 10f, cartao.y + 27f, cartao.width - 20f, 30f), valores[i], designerValor);
        }
    }

    private void DesenharLinhaInformacaoDesigner(Rect area, string rotulo, string valor, bool alerta = false)
    {
        GUI.Box(area, GUIContent.none, designerCartao);
        GUI.Label(new Rect(area.x + 12f, area.y + 5f, area.width * 0.48f, area.height - 10f), rotulo, designerPequeno);
        GUI.Label(new Rect(area.x + area.width * 0.48f, area.y + 5f, area.width * 0.48f - 12f, area.height - 10f), valor, alerta ? designerStatusAlerta : designerSubtitulo);
    }

    private void DesenharTropasDesigner(Rect area)
    {
        int soldados = quartel?.soldadosNoDormitorio?.Count ?? 0;
        int veiculos = quartel?.veiculosNoQuartel?.Count ?? 0;
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "DORMITORIO", "GARAGEM", "NA COBERTURA", "AERONAVES" },
            new[] { soldados.ToString("N0"), veiculos.ToString("N0"), (snapshot?.unidadesNoRaio ?? 0).ToString("N0"), (snapshot?.aeronavesNoRaio ?? 0).ToString("N0") });

        float y = 84f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "AERONAVES CONECTADAS AO QUARTEL", designerSecao);
        y += 28f;
        float listaAltura = Mathf.Max(105f, area.height - y - 58f);
        GUI.Box(new Rect(0f, y, area.width, listaAltura), GUIContent.none, designerCartao);
        if (snapshot?.aeronaves != null && snapshot.aeronaves.Length > 0)
        {
            float linhaY = y + 8f;
            for (int i = 0; i < snapshot.aeronaves.Length && i < 8; i++)
            {
                QuartelAeronaveSnapshotV2 aviao = snapshot.aeronaves[i];
                if (aviao == null) continue;
                string fuel = aviao.combustivelDisponivel ? $"FUEL {aviao.combustivelPercentual * 100f:0}%" : "FUEL N/R";
                string texto = $"{aviao.nome}  |  {aviao.estadoVoo}  |  {fuel}  |  {aviao.baseAtual}  |  {aviao.distanciaAoQuartel:0} m";
                GUI.Label(new Rect(12f, linhaY, area.width - 24f, 24f), texto, designerSubtitulo);
                linhaY += 26f;
            }
        }
        else
        {
            GUI.Label(new Rect(14f, y + 18f, area.width - 28f, 28f), "NENHUMA AERONAVE DO TIME DENTRO DO RAIO DE COMUNICACAO", designerPequeno);
            GUI.Label(new Rect(14f, y + 48f, area.width - 28f, 24f), "A leitura permanece ligada ao registro real e atualiza automaticamente.", designerPequeno);
        }
        if (GUI.Button(new Rect(0f, area.height - 42f, area.width * 0.48f, 34f), "RECONCILIAR AERONAVES", designerBotao))
            snapshot = administracao != null ? administracao.ObterSnapshot() : snapshot;
        if (GUI.Button(new Rect(area.width * 0.52f, area.height - 42f, area.width * 0.48f, 34f), "FECHAR  (ESC)", designerBotao))
            FecharInterfaceDesigner();
    }

    private void DesenharEfetivoDesigner(Rect area)
    {
        DadosPaisGoverno pais = ObterPaisJogador();
        int armazenados = (quartel?.soldadosNoDormitorio?.Count ?? 0) + (quartel?.veiculosNoQuartel?.Count ?? 0);
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "ATIVOS", "RESERVISTAS", "ALISTAVEIS", "ARMAZENADOS", "INOPERANTES" },
            new[] { (pais?.populacaoMilitarAtiva ?? 0).ToString("N0"), (pais?.reservistas ?? 0).ToString("N0"), (pais?.alistaveis ?? 0).ToString("N0"), armazenados.ToString("N0"), (snapshot?.unidadesInoperantes ?? 0).ToString("N0") });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "SITUACAO ADMINISTRATIVA", designerSecao);
        y += 28f;
        string ultimo = snapshot != null ? snapshot.ultimoEvento : "n/d";
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ESTADO DO EFETIVO", (pais?.populacaoMilitarAtiva ?? 0) > 0 ? "OPERACIONAL" : "SEM EFETIVO", (pais?.populacaoMilitarAtiva ?? 0) == 0); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "PESSOAL ALOCADO", (snapshot?.pessoalAlocado ?? 0).ToString("N0")); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "BAIXAS NACIONAIS", (pais?.mortosAcumulados ?? 0).ToString("N0")); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ULTIMO EVENTO", ultimo); y += 50f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "INTEGRIDADE DO REGISTRO", designerSecao);
        GUI.Label(new Rect(0f, y + 30f, area.width, 24f), "O quadro usa o governo nacional e nao duplica a contabilidade existente.", designerPequeno);
    }

    private void DesenharRecrutamentoDesigner(Rect area)
    {
        QuartelAdministracaoRuntime admin = administracao;
        DadosPaisGoverno pais = ObterPaisJogador();
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "ALISTAVEIS", "EM FORMACAO", "CONCLUIDOS", "TEMPO BASE" },
            new[] { (pais?.alistaveis ?? 0).ToString("N0"), (snapshot?.recrutasEmFormacao ?? 0).ToString("N0"), (snapshot?.recrutasConcluidos ?? 0).ToString("N0"), (quartel?.tempoFormacaoSegundos ?? 0f).ToString("0") + " s" });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "PROTOCOLOS DE PESSOAL", designerSecao); y += 30f;
        if (GUI.Button(new Rect(0f, y, area.width, 34f), quartel != null && quartel.recrutamentoAutomatico ? "✓  RECRUTAMENTO AUTOMATICO ATIVO" : "○  ATIVAR RECRUTAMENTO AUTOMATICO", quartel != null && quartel.recrutamentoAutomatico ? designerBotaoAtivo : designerBotao))
            if (quartel != null) quartel.recrutamentoAutomatico = !quartel.recrutamentoAutomatico;
        y += 43f;
        if (GUI.Button(new Rect(0f, y, area.width, 34f), "PROCESSAR RECRUTAMENTO DO DIA", designerBotao) && admin != null)
            admin.SolicitarRecrutamentoManual();
        y += 46f;
        GUI.Label(new Rect(0f, y, area.width * 0.32f, 24f), "QUANTIDADE POR ORDEM", designerRotulo);
        quantidadeRecrutamentoUI = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(area.width * 0.34f, y + 4f, area.width * 0.42f, 18f), quantidadeRecrutamentoUI, 1, 20));
        GUI.Label(new Rect(area.width * 0.80f, y, area.width * 0.20f, 24f), quantidadeRecrutamentoUI.ToString(), designerValor);
        y += 32f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "RECRUTAMENTO DIRECIONADO", designerSecao);
        y += 30f;
        QuartelForcaV2[] forcas = { QuartelForcaV2.Infantaria, QuartelForcaV2.Veiculos, QuartelForcaV2.Naval, QuartelForcaV2.Aerea };
        for (int i = 0; i < forcas.Length; i++)
        {
            float larguraBotao = (area.width - 12f) * 0.5f;
            float bx = (i % 2) * (larguraBotao + 12f);
            float by = y + (i / 2) * 40f;
            QuartelForcaV2 forca = forcas[i];
            if (GUI.Button(new Rect(bx, by, larguraBotao, 34f), "RECRUTAR " + NomeForca(forca), designerBotao) && admin != null)
                admin.SolicitarRecrutamentoManual(forca, quantidadeRecrutamentoUI);
        }
        y += 92f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "META DE EFETIVO LOCAL", (quartel?.metaEfetivo ?? 0).ToString("N0")); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "PROGRESSO", snapshot != null ? $"{snapshot.progressoTreinamento * 100f:0}%  |  {snapshot.segundosRestantesTreinamento:0.0} s" : "0%"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "FORCA DE DESTINO", snapshot?.forcaTreinamento ?? "Nenhuma");
    }

    private void DesenharFolhaDesigner(Rect area)
    {
        DadosPaisGoverno pais = ObterPaisJogador();
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "ATIVOS", "FOLHA DIARIA", "PERIODO", "PAGAMENTO" },
            new[] { (pais?.populacaoMilitarAtiva ?? 0).ToString("N0"), "$" + (snapshot?.custoFolhaDiario ?? 0).ToString("N0"), (snapshot?.periodoFolhaDias ?? 0) + " dias", snapshot != null && snapshot.ultimoPagamentoRealizado ? "DESCONTADO" : "PENDENTE" });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "CUSTOS E RESPONSABILIDADE FISCAL", designerSecao); y += 32f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "SALDO DO TESOURO", GerenciadorRecursos.Instancia != null ? "$" + GerenciadorRecursos.Instancia.dinheiro.ToString("N0") : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "FOLHA DO PERIODO", snapshot != null ? "$" + snapshot.custoFolhaCalculado.ToString("N0") : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "PROXIMO VENCIMENTO", snapshot != null ? "DIA " + snapshot.proximoDiaFolha : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "DIAS PENDENTES", (snapshot?.diasFolhaPendentes ?? 0).ToString("N0"), snapshot != null && snapshot.folhaPendente);
    }

    private void DesenharTripulacoesDesigner(Rect area)
    {
        QuartelForcaSnapshotV2 naval = ObterResumoForca(QuartelForcaV2.Naval);
        QuartelForcaSnapshotV2 aerea = ObterResumoForca(QuartelForcaV2.Aerea);
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "NAVIOS", "AERONAVES", "EXIGIDO", "ALOCADO", "INOPERANTES" },
            new[] { (naval?.unidades ?? 0).ToString("N0"), (aerea?.unidades ?? 0).ToString("N0"), (snapshot?.pessoalExigido ?? 0).ToString("N0"), (snapshot?.pessoalAlocado ?? 0).ToString("N0"), (snapshot?.unidadesInoperantes ?? 0).ToString("N0") });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "SITUACAO DE GUARNICAO", designerSecao); y += 32f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "RESERVA USADA COMO TRIPULACAO", "NAO"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "TRIPULACAO NAVAL", naval != null ? naval.pessoalExigido + " / " + naval.pessoalAlocado : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "TRIPULACAO AEREA", aerea != null ? aerea.pessoalExigido + " / " + aerea.pessoalAlocado : "n/d"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ESTADO", snapshot != null && snapshot.unidadesInoperantes > 0 ? "INOPERANTES" : "OPERACIONAIS", snapshot != null && snapshot.unidadesInoperantes > 0);
    }

    private void DesenharResgateDesigner(Rect area)
    {
        float mapaLargura = area.width * 0.61f;
        Rect mapa = new Rect(0f, 0f, mapaLargura, area.height - 4f);
        GUI.Box(mapa, GUIContent.none, designerMapa);
        for (int i = 1; i < 8; i++) GUI.Box(new Rect(mapa.x + mapa.width * i / 8f, mapa.y, 1f, mapa.height), GUIContent.none, designerGrade);
        for (int i = 1; i < 6; i++) GUI.Box(new Rect(mapa.x, mapa.y + mapa.height * i / 6f, mapa.width, 1f), GUIContent.none, designerGrade);
        GUI.Label(new Rect(mapa.x + 18f, mapa.y + 15f, 50f, 24f), "N", designerSecao);
        GUI.Label(new Rect(mapa.x + mapa.width * 0.44f, mapa.y + mapa.height * 0.47f, 80f, 24f), "QG", designerStatus);
        GUI.Label(new Rect(mapa.x + 18f, mapa.y + mapa.height - 36f, mapa.width - 36f, 24f), "COBERTURA OPERACIONAL  |  ALERTAS EM TEMPO REAL", designerPequeno);

        float x = mapaLargura + 18f;
        float w = area.width - x;
        GUI.Label(new Rect(x, 0f, w, 24f), "SINAL DE EMERGENCIA", designerSecao);
        DesenharLinhaInformacaoDesigner(new Rect(x, 32f, w, 32f), "UNIDADES AVALIADAS", (snapshot?.unidadesAvaliadasResgate ?? 0).ToString("N0"));
        DesenharLinhaInformacaoDesigner(new Rect(x, 70f, w, 32f), "ALERTAS ATIVOS", (snapshot?.unidadesComAlertaResgate ?? 0).ToString("N0"), snapshot != null && snapshot.unidadesComAlertaResgate > 0);
        DesenharLinhaInformacaoDesigner(new Rect(x, 108f, w, 32f), "PERDAS REGISTRADAS", (snapshot?.perdasRegistradas ?? 0).ToString("N0"));
        if (GUI.Button(new Rect(x, 158f, w, 34f), "RESGATE MANUAL", designerBotao)) quartel?.SolicitarResgateManual();
        if (GUI.Button(new Rect(x, 200f, w, 34f), "REPARAR NO RAIO", designerBotao)) quartel?.SolicitarReparosNoRaio();
        GUI.Label(new Rect(x, 250f, w, 48f), snapshot != null ? snapshot.ultimoAvisoResgate : "Nenhum aviso de resgate registrado.", designerPequeno);
    }

    private void DesenharComunicacoesDesigner(Rect area)
    {
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "AERONAVES", "INIMIGOS", "SUBMARINOS", "MISSEIS" },
            new[] { (snapshot?.aeronavesNoRaio ?? 0).ToString("N0"), (snapshot?.contatosInimigos ?? 0).ToString("N0"), (snapshot?.contatosSubmarinos ?? 0).ToString("N0"), (snapshot?.contatosMisseis ?? 0).ToString("N0") });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "REDE DE INFORMACOES", designerSecao); y += 34f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "REGISTRO DE AERONAVES", (snapshot?.aeronavesNoRaio ?? 0) > 0 ? "CONECTADO" : "AGUARDANDO CONTATO"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "RETRANSMISSAO", "ATIVA"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ULTIMO EVENTO", snapshot?.ultimoEvento ?? "n/d"); y += 38f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "ULTIMAS TRANSMISSOES", designerSecao); y += 30f;
        if (snapshot?.comunicacoes != null && snapshot.comunicacoes.Length > 0)
        {
            int limite = Mathf.Min(6, snapshot.comunicacoes.Length);
            for (int i = 0; i < limite; i++)
            {
                QuartelComunicacaoSnapshotV2 comunicacao = snapshot.comunicacoes[i];
                if (comunicacao == null) continue;
                string linha = "[" + comunicacao.horario + "] " + comunicacao.origem + " | " + comunicacao.tipo + " | " + comunicacao.mensagem;
                GUI.Label(new Rect(0f, y, area.width, 24f), linha, comunicacao.inimigo ? designerStatusAlerta : designerPequeno);
                y += 25f;
            }
        }
        else
        {
            GUI.Label(new Rect(0f, y, area.width, 30f), "Aguardando telemetria do E-3.", designerPequeno);
        }
    }

    private void DesenharCartaDesigner(Rect area)
    {
        GarantirCartaTopograficaView();
        float raio = cartaTopograficaView != null ? cartaTopograficaView.RaioMapa : ObterRaioCarta();
        float barraAltura = 36f;
        GUI.Box(new Rect(0f, 0f, area.width, barraAltura), GUIContent.none, designerCartao);
        GUI.Label(new Rect(12f, 7f, 220f, 22f), "CARTA NAUTICA OPERACIONAL", designerSecao);
        if (GUI.Button(new Rect(area.width - 330f, 5f, 150f, 27f), "2D TOPOGRAFICO", cartaVista3D ? designerNavegacao : designerBotaoAtivo))
        {
            cartaVista3D = false;
            AtualizarPainel();
        }
        if (GUI.Button(new Rect(area.width - 170f, 5f, 158f, 27f), "3D TOPOGRAFICO", cartaVista3D ? designerBotaoAtivo : designerNavegacao))
        {
            cartaVista3D = true;
            AtualizarPainel();
        }

        float painelLateral = Mathf.Clamp(area.width * 0.31f, 235f, 315f);
        Rect mapa = new Rect(0f, barraAltura + 8f, area.width - painelLateral - 8f, area.height - barraAltura - 46f);
        Rect telemetria = new Rect(area.width - painelLateral, barraAltura + 8f, painelLateral, area.height - barraAltura - 46f);
        Texture texturaTerreno = ObterTexturaTerrenoCarta(raio, mapa.width / Mathf.Max(1f, mapa.height));
        GUI.Box(mapa, GUIContent.none, designerMapa);
        if (texturaTerreno != null) GUI.DrawTexture(mapa, texturaTerreno, ScaleMode.StretchToFill, false);
        DesenharGradeCarta(mapa);
        DesenharCurvasNivelCarta(mapa, raio);
        DesenharRotaSelecionadaCarta(mapa, raio);

        int pontosUnidades = 0;
        int pontosMisseis = 0;
        if (cartaTopograficaView != null)
        {
            for (int i = 0; i < cartaTopograficaView.Unidades.Count; i++)
            {
                QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView.Unidades[i];
                if (unidade == null) continue;
                Vector2 ponto = PontoCarta(mapa, unidade.posicao, raio);
                Color cor = !unidade.aliada ? new Color(1f, 0.25f, 0.20f) : CorTipoCarta(unidade.tipo);
                DesenharPontoCarta(mapa, ponto, cor, unidade.id, unidade.nome, unidade.id == cartaUnidadeSelecionadaId, false);
                pontosUnidades++;
            }

            for (int i = 0; i < cartaTopograficaView.Misseis.Count; i++)
            {
                QuartelCartaTopograficaView.MissilTelemetria missil = cartaTopograficaView.Misseis[i];
                if (missil == null) continue;
                Vector2 ponto = PontoCarta(mapa, missil.posicao, raio);
                DesenharPontoCarta(mapa, ponto, missil.aliado ? new Color(0.35f, 0.72f, 1f) : new Color(1f, 0.22f, 0.18f), missil.id, missil.nome, missil.id == cartaMissilSelecionadoId, true);
                pontosMisseis++;
            }
        }

        Vector2 centro = PontoCarta(mapa, quartel != null ? quartel.transform.position : transform.position, raio);
        Color corAnterior = GUI.color;
        GUI.color = new Color(1f, 0.78f, 0.18f, 1f);
        GUI.DrawTexture(new Rect(centro.x - 6f, centro.y - 6f, 12f, 12f), Texture2D.whiteTexture);
        GUI.color = corAnterior;
        GUI.Label(new Rect(centro.x + 9f, centro.y - 12f, 140f, 22f), "QG  " + quartel.name, designerStatus);
        GUI.Label(new Rect(mapa.x + 12f, mapa.y + 10f, 180f, 24f), cartaVista3D ? "VISUALIZACAO INCLINADA" : "VISTA SUPERIOR", designerSubtitulo);
        GUI.Label(new Rect(mapa.x + 12f, mapa.y + mapa.height - 28f, mapa.width - 24f, 24f), "RAIO " + raio.ToString("0") + " m  |  UNIDADES " + pontosUnidades + "  |  MISSEIS " + pontosMisseis, designerPequeno);

        DesenharTelemetriaCartaDesigner(telemetria, raio);
        GUI.Label(new Rect(0f, area.height - 30f, area.width, 24f), "Carta topografica leve | curvas de nivel, rotas, contatos, telemetria e impactos; leitura somente.", designerPequeno);
    }

    private void DesenharGradeCarta(Rect mapa)
    {
        for (int i = 1; i < 10; i++) GUI.Box(new Rect(mapa.x + mapa.width * i / 10f, mapa.y, 1f, mapa.height), GUIContent.none, designerGrade);
        for (int i = 1; i < 8; i++) GUI.Box(new Rect(mapa.x, mapa.y + mapa.height * i / 8f, mapa.width, 1f), GUIContent.none, designerGrade);
        GUI.Label(new Rect(mapa.x + mapa.width - 32f, mapa.y + 8f, 24f, 22f), "N", designerStatus);
    }

    private void DesenharCurvasNivelCarta(Rect mapa, float raio)
    {
        if (cartaTopograficaView == null || quartel == null) return;
        float elevacaoCentro = cartaTopograficaView.ObterElevacaoTerreno(quartel.transform.position);
        for (int nivel = 1; nivel <= 6; nivel++)
        {
            float raioBase = raio * (0.15f + nivel * 0.115f);
            Vector2 anterior = Vector2.zero;
            bool possuiAnterior = false;
            for (int amostra = 0; amostra <= 24; amostra++)
            {
                float angulo = amostra / 24f * Mathf.PI * 2f;
                Vector3 deslocamentoLocal = new Vector3(Mathf.Cos(angulo) * raioBase, 0f, Mathf.Sin(angulo) * raioBase);
                Vector3 mundo = quartel.transform.TransformPoint(deslocamentoLocal);
                float elevacao = cartaTopograficaView.ObterElevacaoTerreno(mundo);
                float deformacao = Mathf.Clamp((elevacao - elevacaoCentro) / Mathf.Max(80f, raio * 0.4f), -0.16f, 0.16f);
                float raioDeformado = raioBase * (1f + deformacao);
                Vector3 pontoMundo = quartel.transform.TransformPoint(new Vector3(Mathf.Cos(angulo) * raioDeformado, 0f, Mathf.Sin(angulo) * raioDeformado));
                Vector2 ponto = PontoCarta(mapa, pontoMundo, raio);
                if (possuiAnterior) DesenharSegmentoCarta(anterior, ponto, new Color(0.22f, 0.83f, 0.72f, 0.24f), 1f);
                anterior = ponto;
                possuiAnterior = true;
            }
        }
    }

    private void DesenharRotaSelecionadaCarta(Rect mapa, float raio)
    {
        if (cartaTopograficaView == null) return;
        QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView.EncontrarUnidade(cartaUnidadeSelecionadaId);
        if (unidade == null) return;
        for (int i = 1; i < unidade.rotaPercorrida.Count; i++)
        {
            DesenharSegmentoCarta(PontoCarta(mapa, unidade.rotaPercorrida[i - 1], raio), PontoCarta(mapa, unidade.rotaPercorrida[i], raio), new Color(0.12f, 0.92f, 0.98f, 0.92f), 2f);
        }
        if (unidade.possuiDestino)
        {
            Vector2 atual = PontoCarta(mapa, unidade.posicao, raio);
            Vector2 destino = PontoCarta(mapa, unidade.destino, raio);
            float distancia = Vector2.Distance(atual, destino);
            int segmentos = Mathf.Max(1, Mathf.RoundToInt(distancia / 15f));
            for (int i = 0; i < segmentos; i += 2)
            {
                float t0 = i / (float)segmentos;
                float t1 = Mathf.Min(1f, (i + 1f) / segmentos);
                DesenharSegmentoCarta(Vector2.Lerp(atual, destino, t0), Vector2.Lerp(atual, destino, t1), new Color(0.16f, 0.84f, 1f, 0.95f), 2f);
            }
            GUI.Label(new Rect(destino.x + 6f, destino.y - 12f, 120f, 22f), "DESTINO", designerStatus);
        }
    }

    private void DesenharTelemetriaCartaDesigner(Rect area, float raio)
    {
        GUI.Box(area, GUIContent.none, designerCartao);
        GUI.Label(new Rect(area.x + 12f, area.y + 12f, area.width - 24f, 24f), "TELEMETRIA DA UNIDADE", designerSecao);
        QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView != null ? cartaTopograficaView.EncontrarUnidade(cartaUnidadeSelecionadaId) : null;
        QuartelCartaTopograficaView.MissilTelemetria missil = cartaTopograficaView != null ? cartaTopograficaView.EncontrarMissil(cartaMissilSelecionadoId) : null;
        float y = area.y + 42f;
        if (unidade != null)
        {
            GUI.Label(new Rect(area.x + 12f, y, area.width - 24f, 36f), unidade.nome + "\n" + unidade.tipo + " | EQUIPE " + unidade.equipe + " | " + unidade.situacao, designerTitulo);
            y += 44f;
            y = LinhaTelemetriaCarta(area, y, "ESTADO", unidade.estado);
            y = LinhaTelemetriaCarta(area, y, "MISSAO", unidade.missao);
            y = LinhaTelemetriaCarta(area, y, "POSICAO", FormatarPosicao(unidade.posicao));
            y = LinhaTelemetriaCarta(area, y, "ALTITUDE", unidade.altitudeAbsoluta.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "TERRENO", unidade.elevacaoTerreno.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "ACIMA DO SOLO", unidade.alturaAcimaDoSolo.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "VELOCIDADE", (unidade.velocidadeMetrosPorSegundo * 3.6f).ToString("0") + " km/h");
            y = LinhaTelemetriaCarta(area, y, "RUMO", unidade.rumo);
            y = LinhaTelemetriaCarta(area, y, "COMBUSTIVEL", unidade.combustivelCapacidade > 0f ? (unidade.combustivelPercentual * 100f).ToString("0") + "%" : "NAO DISPONIVEL");
            y = LinhaTelemetriaCarta(area, y, "PERCORRIDA", unidade.distanciaPercorrida.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "RESTANTE", unidade.possuiDestino ? unidade.distanciaRestante.ToString("0") + " m" : "SEM DESTINO");
            y = LinhaTelemetriaCarta(area, y, "CHEGADA", unidade.possuiDestino ? FormatarTempo(unidade.tempoEstimadoSegundos) : "N/A");
            y = LinhaTelemetriaCarta(area, y, "ARMAMENTO", unidade.armamento);
            if (!string.IsNullOrWhiteSpace(unidade.baseAtual)) y = LinhaTelemetriaCarta(area, y, "BASE", unidade.baseAtual);
            if (!string.IsNullOrWhiteSpace(unidade.vaga) && y < area.yMax - 46f) y = LinhaTelemetriaCarta(area, y, "VAGA", unidade.vaga);
        }
        else if (missil != null)
        {
            GUI.Label(new Rect(area.x + 12f, y, area.width - 24f, 36f), missil.nome + "\n" + missil.tipo + " | " + missil.estado, designerTitulo);
            y += 44f;
            y = LinhaTelemetriaCarta(area, y, "LANCADOR", missil.origem);
            y = LinhaTelemetriaCarta(area, y, "VELOCIDADE", (missil.velocidadeMetrosPorSegundo * 3.6f).ToString("0") + " km/h");
            y = LinhaTelemetriaCarta(area, y, "PERCORRIDA", missil.distanciaPercorrida.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "ATE O IMPACTO", missil.distanciaRestante.ToString("0") + " m");
            y = LinhaTelemetriaCarta(area, y, "PONTO DE IMPACTO", FormatarPosicao(missil.pontoProvavelImpacto));
            y = LinhaTelemetriaCarta(area, y, "TEMPO DE VOO", FormatarTempo(missil.tempoDesdeLancamento));
            if (missil.guiagemPerdida) y = LinhaTelemetriaCarta(area, y, "ALERTA", "GUIAGEM PERDIDA");
        }
        else
        {
            GUI.Label(new Rect(area.x + 12f, y, area.width - 24f, 54f), "Clique em uma unidade ou missil no mapa para abrir a telemetria completa.", designerSubtitulo);
            y += 70f;
            y = LinhaTelemetriaCarta(area, y, "UNIDADES", cartaTopograficaView != null ? cartaTopograficaView.Unidades.Count.ToString("N0") : "0");
            y = LinhaTelemetriaCarta(area, y, "MISSEIS EM VOO", cartaTopograficaView != null ? cartaTopograficaView.Misseis.Count.ToString("N0") : "0");
            y = LinhaTelemetriaCarta(area, y, "ALTITUDE", "selecione uma unidade aerea");
        }

        if (y < area.yMax - 78f)
        {
            GUI.Label(new Rect(area.x + 12f, area.yMax - 70f, area.width - 24f, 22f), "EVENTOS RECENTES", designerSecao);
            int eventos = 0;
            if (snapshot != null && snapshot.comunicacoes != null)
            {
                for (int i = 0; i < snapshot.comunicacoes.Length && eventos < 2; i++)
                {
                    QuartelComunicacaoSnapshotV2 comunicacao = snapshot.comunicacoes[i];
                    if (comunicacao == null) continue;
                    GUI.Label(new Rect(area.x + 12f, area.yMax - 48f + eventos * 22f, area.width - 24f, 20f), comunicacao.horario + "  " + comunicacao.tipo, designerPequeno);
                    eventos++;
                }
            }
            if (eventos == 0) GUI.Label(new Rect(area.x + 12f, area.yMax - 46f, area.width - 24f, 20f), "Sem eventos transmitidos.", designerPequeno);
        }
    }

    private float LinhaTelemetriaCarta(Rect area, float y, string nome, string valor)
    {
        if (y > area.yMax - 116f) return y;
        GUI.Label(new Rect(area.x + 12f, y, area.width * 0.42f, 20f), nome, designerPequeno);
        GUI.Label(new Rect(area.x + area.width * 0.40f, y, area.width * 0.56f, 20f), valor, designerSubtitulo);
        return y + 21f;
    }

    private void DesenharPontoCarta(Rect mapa, Vector2 ponto, Color cor, string id, string texto, bool selecionado, bool missil)
    {
        float tamanho = selecionado ? 12f : missil ? 8f : 9f;
        Color anterior = GUI.color;
        GUI.color = cor;
        GUI.DrawTexture(new Rect(ponto.x - tamanho * 0.5f, ponto.y - tamanho * 0.5f, tamanho, tamanho), Texture2D.whiteTexture);
        GUI.color = anterior;
        if (GUI.Button(new Rect(ponto.x - 13f, ponto.y - 13f, 26f, 26f), GUIContent.none, GUIStyle.none))
        {
            if (missil) cartaMissilSelecionadoId = id; else cartaUnidadeSelecionadaId = id;
            if (missil) cartaUnidadeSelecionadaId = string.Empty; else cartaMissilSelecionadoId = string.Empty;
        }
        GUI.Label(new Rect(ponto.x + 8f, ponto.y - 10f, 190f, 22f), texto, selecionado ? designerStatus : (cor.r > 0.8f ? designerStatusAlerta : designerPequeno));
    }

    private Vector2 PontoCarta(Rect mapa, Vector3 posicao, float raio)
    {
        Vector3 local = (quartel != null ? quartel.transform : transform).InverseTransformPoint(posicao);
        float x = mapa.x + Mathf.Clamp01(local.x / (raio * 2f) + 0.5f) * mapa.width;
        float y = mapa.y + (1f - Mathf.Clamp01(local.z / (raio * 2f) + 0.5f)) * mapa.height;
        return new Vector2(x, y);
    }

    private void DesenharSegmentoCarta(Vector2 inicio, Vector2 fim, Color cor, float espessura)
    {
        Vector2 delta = fim - inicio;
        float comprimento = delta.magnitude;
        if (comprimento < 0.5f) return;
        Matrix4x4 matrizAnterior = GUI.matrix;
        Color corAnterior = GUI.color;
        GUI.color = cor;
        float angulo = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angulo, inicio);
        GUI.DrawTexture(new Rect(inicio.x, inicio.y - espessura * 0.5f, comprimento, espessura), Texture2D.whiteTexture);
        GUI.matrix = matrizAnterior;
        GUI.color = corAnterior;
    }

    private static Color CorTipoCarta(string tipo)
    {
        if (tipo == "AEREO") return new Color(0.35f, 0.72f, 1f);
        if (tipo == "NAVAL") return new Color(0.20f, 0.92f, 0.90f);
        if (tipo == "VEICULO") return new Color(0.42f, 0.88f, 0.45f);
        return new Color(0.78f, 0.86f, 0.30f);
    }

    private static string FormatarPosicao(Vector3 posicao)
    {
        return "X " + posicao.x.ToString("0") + " | Y " + posicao.y.ToString("0") + " | Z " + posicao.z.ToString("0");
    }

    private static string FormatarTempo(float segundos)
    {
        if (segundos <= 0f) return "N/A";
        TimeSpan tempo = TimeSpan.FromSeconds(segundos);
        return tempo.TotalHours >= 1d ? tempo.ToString(@"hh\:mm\:ss") : tempo.ToString(@"mm\:ss");
    }

    private void DesenharArsenalDesigner(Rect area)
    {
        long dinheiro = GerenciadorRecursos.Instancia != null ? GerenciadorRecursos.Instancia.dinheiro : -1L;
        DesenharMetricasDesigner(new Rect(0f, 0f, area.width, 70f),
            new[] { "MISSEIS", "MUNICAO", "FUNDO", "FOLHA" },
            new[] { (quartel?.misseisArmazenados ?? 0).ToString("N0"), (quartel?.municaoArmazenada ?? 0).ToString("N0"), dinheiro >= 0 ? "$" + dinheiro.ToString("N0") : "n/d", "$" + (snapshot?.custoFolhaDiario ?? 0).ToString("N0") });
        float y = 88f;
        GUI.Label(new Rect(0f, y, area.width, 24f), "REABASTECER ARSENAL", designerSecao); y += 34f;
        if (GUI.Button(new Rect(0f, y, area.width, 36f), "ENCOMENDAR 10 MISSEIS  (-$" + (quartel?.precoMissil ?? 0).ToString("N0") + ")", designerBotao)) quartel?.TentarEncomendarMisseis();
        y += 44f;
        if (GUI.Button(new Rect(0f, y, area.width, 36f), "ENCOMENDAR 100 PACOTES  (-$" + (quartel?.precoMunicao ?? 0).ToString("N0") + ")", designerBotao)) quartel?.TentarEncomendarMunicao();
        y += 58f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "FONTE", "GerenciadorRecursos atual"); y += 38f;
        DesenharLinhaInformacaoDesigner(new Rect(0f, y, area.width, 32f), "ESTADO", "ESTOQUE PERSISTENTE");
    }

    private void FecharInterfaceDesigner()
    {
        if (quartel != null) quartel.FecharInterfacePorUI();
        else FecharInterno();
    }

    private void Awake()
    {
        quartel = GetComponent<GerenciadorQuartel>();
        administracao = quartel != null ? quartel.ObterAdministracao() : GetComponent<QuartelAdministracaoRuntime>();
        GarantirCartaTopograficaView();
        InicializarDocumento();
        if (Application.isPlaying)
        {
            Debug.Log($"[QuartelUI] Awake: objeto={name}, pronto={pronto}, root={(root != null)}, documento={(documento != null)}, administracao={(administracao != null)}", this);
        }
    }

    private void Update()
    {
        if (!aberto)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (quartel != null)
            {
                quartel.FecharInterfacePorUI();
            }
            else
            {
                FecharInterno();
            }
            return;
        }

        if (Time.unscaledTime < proximaAtualizacao)
        {
            return;
        }

        proximaAtualizacao = Time.unscaledTime + 0.75f;
        AtualizarPainel();
    }

    private void GarantirCartaTopograficaView()
    {
        if (cartaTopograficaView == null)
        {
            cartaTopograficaView = GetComponent<QuartelCartaTopograficaView>();
            if (cartaTopograficaView == null) cartaTopograficaView = gameObject.AddComponent<QuartelCartaTopograficaView>();
        }
    }

    private void OnDestroy()
    {
        if (painelAberto == this)
        {
            painelAberto = null;
        }

        if (panelSettingsRuntime != null)
        {
            Destroy(panelSettingsRuntime);
            panelSettingsRuntime = null;
        }
    }

    public void Abrir()
    {
        if (quartel == null)
        {
            quartel = GetComponent<GerenciadorQuartel>();
        }

        if (!pronto)
        {
            InicializarDocumento();
        }

        if (!pronto || root == null)
        {
            Debug.LogWarning($"[QuartelUI] abertura recusada: pronto={pronto}, root={(root != null)}, documento={(documento != null)}, painelSettings={(panelSettingsRuntime != null)}", this);
            return;
        }

        if (painelAberto != null && painelAberto != this)
        {
            QuartelMenuUIController anterior = painelAberto;
            GerenciadorQuartel quartelAnterior = anterior.quartel;
            anterior.FecharInterno();
            if (quartelAnterior != null && quartelAnterior != quartel)
            {
                quartelAnterior.FecharInterfacePorUI();
            }
        }

        painelAberto = this;
        aberto = true;
        documento.enabled = true;
        // Em alguns Game Views embutidos o PanelSettings nao calcula o
        // worldBound do root imediatamente. Fixar o viewport no momento da
        // abertura evita NaN e mantem o overlay visivel no editor e no jogo.
        root.style.width = Mathf.Max(1, Screen.width);
        root.style.height = Mathf.Max(1, Screen.height);
        root.style.display = DisplayStyle.Flex;
        proximaAtualizacao = 0f;
        AtualizarPainel();
        Debug.Log($"[QuartelUI] painel aberto: objeto={name}, root={root.name}, documentoAtivo={documento.enabled}, snapshotAeronaves={(snapshot != null ? snapshot.aeronavesNoRaio : 0)}, tamanho={root.resolvedStyle.width:0}x{root.resolvedStyle.height:0}, visibilidade={root.resolvedStyle.visibility}, opacidade={root.resolvedStyle.opacity:0.00}", this);
    }

    public void FecharInterno()
    {
        aberto = false;
        if (painelAberto == this)
        {
            painelAberto = null;
        }

        if (root != null)
        {
            root.style.display = DisplayStyle.None;
        }
    }

    private void InicializarDocumento()
    {
        if (pronto)
        {
            return;
        }

        documento = GetComponent<UIDocument>();
        if (documento == null)
        {
            documento = gameObject.AddComponent<UIDocument>();
        }

        PanelSettings baseSettings = Resources.Load<PanelSettings>("PanelSettings");
        if (baseSettings != null)
        {
            panelSettingsRuntime = Instantiate(baseSettings);
        }
        else
        {
            panelSettingsRuntime = ScriptableObject.CreateInstance<PanelSettings>();
        }

        panelSettingsRuntime.sortingOrder = 1200;
        documento.panelSettings = panelSettingsRuntime;
        documento.visualTreeAsset = null;
        documento.enabled = true;
        root = documento.rootVisualElement;
        if (root == null)
        {
            documento.enabled = false;
            return;
        }

        ConstruirLayout();
        root.style.display = DisplayStyle.None;
        documento.enabled = false;
        pronto = true;
        Debug.Log($"[QuartelUI] documento inicializado: objeto={name}, root={(root != null)}, panelSettings={(panelSettingsRuntime != null)}, sorting={(panelSettingsRuntime != null ? panelSettingsRuntime.sortingOrder : -1)}", this);
    }

    private void ConstruirLayout()
    {
        root.Clear();
        // O root do UIDocument ja recebe o tamanho do PanelSettings. Deixa-lo
        // como elemento absoluto pode zerar o worldBound em alguns modos de
        // renderizacao do Unity e fazer o menu existir sem aparecer.
        root.style.flexGrow = 1;

        overlay = new VisualElement { name = "quartel-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0.008f, 0.016f, 0.024f, 0.82f);
        overlay.style.paddingLeft = 18;
        overlay.style.paddingRight = 18;
        overlay.style.paddingTop = 18;
        overlay.style.paddingBottom = 18;
        root.Add(overlay);

        painel = new VisualElement { name = "quartel-painel" };
        painel.style.width = new Length(94f, LengthUnit.Percent);
        painel.style.maxWidth = 1500;
        painel.style.height = new Length(94f, LengthUnit.Percent);
        painel.style.maxHeight = 930;
        painel.style.flexDirection = FlexDirection.Column;
        painel.style.backgroundColor = new Color(0.025f, 0.055f, 0.075f, 0.99f);
        painel.style.borderTopWidth = 2;
        painel.style.borderBottomWidth = 2;
        painel.style.borderLeftWidth = 2;
        painel.style.borderRightWidth = 2;
        painel.style.borderTopColor = new Color(0.55f, 0.68f, 0.64f);
        painel.style.borderBottomColor = new Color(0.55f, 0.68f, 0.64f);
        painel.style.borderLeftColor = new Color(0.55f, 0.68f, 0.64f);
        painel.style.borderRightColor = new Color(0.55f, 0.68f, 0.64f);
        painel.style.borderTopLeftRadius = 6;
        painel.style.borderTopRightRadius = 6;
        painel.style.borderBottomLeftRadius = 6;
        painel.style.borderBottomRightRadius = 6;
        overlay.Add(painel);

        VisualElement cabecalho = Linha();
        cabecalho.style.paddingLeft = 16;
        cabecalho.style.paddingRight = 12;
        cabecalho.style.paddingTop = 10;
        cabecalho.style.paddingBottom = 10;
        cabecalho.style.backgroundColor = new Color(0.045f, 0.12f, 0.15f, 1f);

        VisualElement blocoTitulo = new VisualElement();
        blocoTitulo.style.flexGrow = 1;
        titulo = Texto("QUARTEL GERAL", 23, new Color(0.92f, 0.82f, 0.43f), FontStyle.Bold);
        subtitulo = Texto("CENTRO ADMINISTRATIVO E LOGISTICO", 12, new Color(0.68f, 0.82f, 0.82f), FontStyle.Normal);
        blocoTitulo.Add(titulo);
        blocoTitulo.Add(subtitulo);
        cabecalho.Add(blocoTitulo);

        status = Texto("SISTEMA PRONTO", 13, new Color(0.55f, 0.95f, 0.70f), FontStyle.Bold);
        status.style.marginRight = 18;
        cabecalho.Add(status);

        Button fechar = Botao("FECHAR  X", 106, 34, new Color(0.35f, 0.10f, 0.10f));
        fechar.clicked += () =>
        {
            if (quartel != null) quartel.FecharInterfacePorUI();
            else FecharInterno();
        };
        cabecalho.Add(fechar);
        painel.Add(cabecalho);

        metricas = Texto(string.Empty, 13, new Color(0.82f, 0.89f, 0.86f), FontStyle.Bold);
        metricas.style.paddingLeft = 16;
        metricas.style.paddingTop = 8;
        metricas.style.paddingBottom = 8;
        metricas.style.backgroundColor = new Color(0.02f, 0.08f, 0.10f, 1f);
        painel.Add(metricas);

        ScrollView abasScroll = new ScrollView(ScrollViewMode.Horizontal);
        abasScroll.style.flexShrink = 0;
        abasScroll.style.height = 46;
        VisualElement abas = Linha();
        abas.style.flexWrap = Wrap.NoWrap;
        abas.style.paddingLeft = 6;
        abas.style.paddingRight = 6;
        abasScroll.Add(abas);
        painel.Add(abasScroll);

        for (int i = 0; i < nomesAbas.Length; i++)
        {
            int indice = i;
            Button aba = Botao(nomesAbas[i], 132, 38, new Color(0.07f, 0.15f, 0.18f));
            aba.style.marginLeft = 2;
            aba.style.marginRight = 2;
            aba.clicked += () => SelecionarAba(indice);
            botoesAbas.Add(aba);
            abas.Add(aba);
        }

        conteudo = new ScrollView(ScrollViewMode.Vertical) { name = "quartel-conteudo" };
        conteudo.style.flexGrow = 1;
        conteudo.style.paddingLeft = 14;
        conteudo.style.paddingRight = 14;
        conteudo.style.paddingTop = 12;
        conteudo.style.paddingBottom = 12;
        painel.Add(conteudo);

        Label rodape = Texto("Acoes passam pelo GerenciadorQuartel. Ordens, selecao, camera e patrulhas permanecem nos controladores existentes.", 11, new Color(0.56f, 0.72f, 0.72f), FontStyle.Normal);
        rodape.style.paddingLeft = 14;
        rodape.style.paddingTop = 7;
        rodape.style.paddingBottom = 7;
        rodape.style.backgroundColor = new Color(0.02f, 0.07f, 0.09f, 1f);
        painel.Add(rodape);

        SelecionarAba(0);
    }

    private void SelecionarAba(int indice)
    {
        abaAtual = Mathf.Clamp(indice, 0, nomesAbas.Length - 1);
        for (int i = 0; i < botoesAbas.Count; i++)
        {
            bool ativo = i == abaAtual;
            botoesAbas[i].style.backgroundColor = ativo
                ? new Color(0.62f, 0.43f, 0.08f, 1f)
                : new Color(0.07f, 0.15f, 0.18f, 1f);
            botoesAbas[i].style.color = ativo ? new Color(0.08f, 0.05f, 0.02f) : Color.white;
        }
        AtualizarPainel();
    }

    private void AtualizarPainel()
    {
        if (!pronto || quartel == null || conteudo == null)
        {
            return;
        }

        administracao = quartel.ObterAdministracao();
        snapshot = administracao != null ? administracao.ObterSnapshot() : null;
        GarantirCartaTopograficaView();
        if (cartaTopograficaView != null)
        {
            float raioCarta = Mathf.Max(100f, quartel.raioDeCobertura);
            cartaTopograficaView.Modo = cartaVista3D
                ? QuartelCartaTopograficaView.ModoVisualizacao.Topografico3D
                : QuartelCartaTopograficaView.ModoVisualizacao.Topografico2D;
            cartaTopograficaView.Atualizar(quartel.transform, quartel.teamID, raioCarta, abaAtual == 7);
        }

        int soldados = quartel.soldadosNoDormitorio != null ? quartel.soldadosNoDormitorio.Count : 0;
        int veiculos = quartel.veiculosNoQuartel != null ? quartel.veiculosNoQuartel.Count : 0;
        metricas.text = $"RESERVA: {soldados} soldados  |  {veiculos} veiculos  |  ATIVOS NACIONAIS: {snapshot?.militaresAtivos ?? 0}  |  UNIDADES NA COBERTURA: {snapshot?.unidadesNoRaio ?? 0}  |  AERONAVES CONECTADAS: {snapshot?.aeronavesNoRaio ?? 0}  |  ARSENAL: {quartel.misseisArmazenados} misseis / {quartel.municaoArmazenada} pacotes  |  COBERTURA: {quartel.raioDeCobertura:0} m";
        titulo.text = "QUARTEL GERAL  |  " + quartel.name.ToUpperInvariant();
        subtitulo.text = "CENTRO ADMINISTRATIVO E LOGISTICO  |  UI TOOLKIT V2";
        status.text = quartel.modoDefensivoAtivo ? "DEFESA AUTOMATICA ATIVA" : "SISTEMA OPERACIONAL";
        status.style.color = quartel.modoDefensivoAtivo ? new Color(1f, 0.74f, 0.25f) : new Color(0.55f, 0.95f, 0.70f);

        conteudo.Clear();
        switch (abaAtual)
        {
            case 0: ConstruirAbaTropas(); break;
            case 1: ConstruirAbaEfetivo(); break;
            case 2: ConstruirAbaRecrutamento(); break;
            case 3: ConstruirAbaFolha(); break;
            case 4: ConstruirAbaTripulacoes(); break;
            case 5: ConstruirAbaResgate(); break;
            case 6: ConstruirAbaComunicacoes(); break;
            case 7: ConstruirAbaCartaNautica(); break;
            case 8: ConstruirAbaArsenal(); break;
        }
    }

    private void ConstruirAbaTropas()
    {
        AdicionarCabecalho("TROPAS", "Recolhimento e emprego das unidades mantidas pelo Quartel.");
        VisualElement resumo = Linha();
        resumo.Add(Cartao("DORMITORIO", (quartel.soldadosNoDormitorio?.Count ?? 0).ToString(), "soldados armazenados"));
        resumo.Add(Cartao("GARAGEM", (quartel.veiculosNoQuartel?.Count ?? 0).ToString(), "veiculos estacionados"));
        resumo.Add(Cartao("NA COBERTURA", snapshot != null ? snapshot.unidadesNoRaio.ToString("N0") : "n/d", "unidades registradas"));
        resumo.Add(Cartao("EM ATIVIDADE", snapshot != null ? snapshot.unidadesEmMissao.ToString("N0") : "n/d", "ordem ou missao ativa"));
        conteudo.Add(resumo);

        VisualElement acoes = Card("ACOES OPERACIONAIS");
        int selecionados = ContarSelecionadosJogador();
        int soldadosArmazenados = quartel.soldadosNoDormitorio?.Count ?? 0;
        int veiculosArmazenados = quartel.veiculosNoQuartel?.Count ?? 0;
        acoes.Add(BotaoAcao("CONVOCAR SELECIONADOS NO MAPA", quartel.SolicitarConvocarSelecionados, false, selecionados > 0, "Nenhuma unidade do jogador selecionada"));
        acoes.Add(BotaoAcao("DESDOBRAR 1 SOLDADO", () => quartel.SolicitarDesdobramentoSoldados(1), false, soldadosArmazenados >= 1, "Nao ha soldado armazenado"));
        acoes.Add(BotaoAcao("DESDOBRAR 5 SOLDADOS", () => quartel.SolicitarDesdobramentoSoldados(5), false, soldadosArmazenados >= 5, "Sao necessarios 5 soldados armazenados"));
        acoes.Add(BotaoAcao("ESVAZIAR DORMITORIO", () => quartel.SolicitarDesdobramentoSoldados(soldadosArmazenados), true, soldadosArmazenados > 0, "Dormitorio vazio"));
        acoes.Add(BotaoAcao("LIGAR TODOS OS VEICULOS", quartel.SolicitarDesdobramentoTodosVeiculos, true, veiculosArmazenados > 0, "Nao ha veiculos armazenados"));
        conteudo.Add(acoes);

        VisualElement lista = Card("UNIDADES ARMAZENADAS");
        int mostradas = 0;
        if (quartel.soldadosNoDormitorio != null)
        {
            for (int i = 0; i < quartel.soldadosNoDormitorio.Count && mostradas < 12; i++)
            {
                ControleUnidade unidade = quartel.soldadosNoDormitorio[i];
                if (unidade == null) continue;
                lista.Add(LinhaUnidade("INFANTARIA", unidade.name, "RESERVA"));
                mostradas++;
            }
        }
        if (quartel.veiculosNoQuartel != null)
        {
            for (int i = 0; i < quartel.veiculosNoQuartel.Count && mostradas < 20; i++)
            {
                ControleUnidade unidade = quartel.veiculosNoQuartel[i];
                if (unidade == null) continue;
                lista.Add(LinhaUnidade("VEICULO", unidade.name, "GARAGEM"));
                mostradas++;
            }
        }
        if (mostradas == 0) lista.Add(Texto("Nenhuma unidade esta armazenada no momento.", 13, new Color(0.68f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(lista);

        VisualElement avioes = Card("AERONAVES CONECTADAS AO QUARTEL");
        int aeronavesMostradas = 0;
        if (snapshot != null && snapshot.aeronaves != null)
        {
            for (int i = 0; i < snapshot.aeronaves.Length && aeronavesMostradas < 24; i++)
            {
                QuartelAeronaveSnapshotV2 aviao = snapshot.aeronaves[i];
                if (aviao == null) continue;
                string combustivel = aviao.combustivelDisponivel
                    ? "FUEL " + (aviao.combustivelPercentual * 100f).ToString("0") + "%"
                    : "FUEL N/R";
                string local = string.IsNullOrWhiteSpace(aviao.baseAtual) ? "sem base" : aviao.baseAtual;
                string vaga = string.IsNullOrWhiteSpace(aviao.vaga) ? string.Empty : " | vaga " + aviao.vaga;
                string estado = aviao.estadoVoo + " | " + combustivel + " | " + local + vaga;
                VisualElement linhaAviao = LinhaUnidade("AERONAVE", aviao.nome, estado);
                linhaAviao.tooltip = "ID: " + aviao.id
                    + "\nEstado porta-avioes V2: " + aviao.estadoPortaAvioes
                    + "\nOperacao: " + aviao.operacao
                    + "\nMissao: " + aviao.missao
                    + "\nIntegridade: " + (aviao.integridadePercentual * 100f).ToString("0") + "%"
                    + "\nDistancia ao Quartel: " + aviao.distanciaAoQuartel.ToString("0") + " m"
                    + (string.IsNullOrWhiteSpace(aviao.autoridadeMovimento) ? string.Empty : "\nAutoridade de movimento: " + aviao.autoridadeMovimento);
                avioes.Add(linhaAviao);
                aeronavesMostradas++;
            }
        }
        if (aeronavesMostradas == 0)
        {
            avioes.Add(Texto("Nenhuma aeronave do time esta dentro do raio de comunicacao do Quartel.", 13, new Color(0.68f, 0.78f, 0.78f), FontStyle.Normal));
        }
        avioes.Add(Texto("Leitura somente. Movimento, pouso, taxiamento, hangar e decolagem continuam sob ControleAviao/GerenciadorOperacoesPortaAvioesV2.", 11, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(avioes);
    }

    private void ConstruirAbaEfetivo()
    {
        AdicionarCabecalho("EFETIVO", "Quadro consolidado do pais e das unidades sob este Quartel.");
        DadosPaisGoverno pais = ObterPaisJogador();
        int ativos = pais != null ? pais.populacaoMilitarAtiva : 0;
        int reservistas = pais != null ? pais.reservistas : 0;
        int alistaveis = pais != null ? pais.alistaveis : 0;
        int armazenados = (quartel.soldadosNoDormitorio?.Count ?? 0) + (quartel.veiculosNoQuartel?.Count ?? 0);
        int inoperantes = snapshot != null ? snapshot.unidadesInoperantes : 0;

        VisualElement resumo = Linha();
        resumo.Add(Cartao("ATIVOS", ativos.ToString("N0"), "forca militar nacional"));
        resumo.Add(Cartao("RESERVISTAS", reservistas.ToString("N0"), "disponiveis para chamada"));
        resumo.Add(Cartao("ALISTAVEIS", alistaveis.ToString("N0"), "base de recrutamento"));
        resumo.Add(Cartao("ARMAZENADOS", armazenados.ToString("N0"), "unidades no Quartel"));
        resumo.Add(Cartao("INOPERANTES", inoperantes.ToString("N0"), "sem tripulacao ativa"));
        conteudo.Add(resumo);

        VisualElement quadro = Card("SITUACAO ADMINISTRATIVA");
        quadro.Add(LinhaInformacao("Estado do efetivo", ativos > 0 ? "OPERACIONAL" : "SEM EFETIVO ATIVO"));
        quadro.Add(LinhaInformacao("Reserva local", armazenados > 0 ? "COM UNIDADES" : "VAZIA"));
        quadro.Add(LinhaInformacao("Prontidao de defesa", quartel.modoDefensivoAtivo ? "PROTOCOLO ATIVO" : "SOB COMANDO"));
        quadro.Add(LinhaInformacao("Integracao com Censo Imperial", CensoImperial.Instancia != null ? "CONECTADA" : "AGUARDANDO CENSO"));
        quadro.Add(LinhaInformacao("Pessoal alocado neste Quartel", snapshot != null ? snapshot.pessoalAlocado.ToString("N0") : "n/d"));
        quadro.Add(LinhaInformacao("Baixas nacionais registradas", pais != null ? pais.mortosAcumulados.ToString("N0") : "n/d"));
        quadro.Add(LinhaInformacao("Feridos / temporarios / desaparecidos", "N/R — nao existe registro nacional dessas categorias"));
        quadro.Add(LinhaInformacao("Ultimo evento", snapshot != null ? snapshot.ultimoEvento : "n/d"));
        conteudo.Add(quadro);

        VisualElement forcas = Card("UNIDADES POR FORCA — DADOS DO REGISTRO ATUAL");
        AdicionarResumoForcas(forcas);
        conteudo.Add(forcas);

        VisualElement orientacao = Card("OBSERVACAO");
        orientacao.Add(Texto("O quadro nacional continua sendo calculado pelo sistema governamental existente. Este painel nao duplica a contabilidade; ele apenas organiza a leitura para o Quartel.", 13, new Color(0.70f, 0.82f, 0.82f), FontStyle.Normal));
        conteudo.Add(orientacao);
    }

    private void ConstruirAbaRecrutamento()
    {
        AdicionarCabecalho("RECRUTAMENTO", "Protocolos automaticos sem criar uma segunda autoridade de producao.");
        VisualElement protocolos = Card("PROTOCOLOS DE PESSOAL");
        Toggle toggleRecrutamento = new Toggle("Recrutamento automatico por disponibilidade nacional")
        {
            value = quartel.recrutamentoAutomatico
        };
        toggleRecrutamento.RegisterValueChangedCallback(e => quartel.recrutamentoAutomatico = e.newValue);
        protocolos.Add(toggleRecrutamento);
        QuartelAdministracaoRuntime admin = quartel.ObterAdministracao();
        if (admin != null)
        {
            IntegerField metaEfetivo = new IntegerField("META DE EFETIVO LOCAL")
            {
                value = quartel.metaEfetivo
            };
            metaEfetivo.RegisterValueChangedCallback(e => quartel.metaEfetivo = Mathf.Clamp(e.newValue, 1, 100000));
            protocolos.Add(metaEfetivo);

            IntegerField metaDiaria = new IntegerField("META DE RECRUTAMENTO DIARIO")
            {
                value = admin.recrutamentoPorDia
            };
            metaDiaria.RegisterValueChangedCallback(e => admin.recrutamentoPorDia = Mathf.Clamp(e.newValue, 1, 1000));
            protocolos.Add(metaDiaria);

            FloatField tempoFormacao = new FloatField("TEMPO BASE DE FORMACAO (s)")
            {
                value = quartel.tempoFormacaoSegundos
            };
            tempoFormacao.RegisterValueChangedCallback(e =>
            {
                quartel.tempoFormacaoSegundos = Mathf.Clamp(e.newValue, 1f, 86400f);
                admin.tempoFormacaoPadraoSegundos = quartel.tempoFormacaoSegundos;
            });
            protocolos.Add(tempoFormacao);

            DadosPaisGoverno paisParaRecrutamento = ObterPaisJogador();
            bool podeRecrutar = paisParaRecrutamento != null
                && paisParaRecrutamento.alistaveis > 0
                && paisParaRecrutamento.populacaoMilitarAtiva + (snapshot != null ? snapshot.recrutasEmFormacao : 0) < quartel.metaEfetivo;
            protocolos.Add(BotaoAcao("PROCESSAR RECRUTAMENTO DO DIA", admin.SolicitarRecrutamentoManual, false, podeRecrutar, podeRecrutar ? string.Empty : "Sem alistaveis ou meta de efetivo atingida"));

            IntegerField quantidade = new IntegerField("QUANTIDADE POR ORDEM")
            {
                value = quantidadeRecrutamentoUI
            };
            quantidade.style.marginTop = 8;
            quantidade.style.marginBottom = 6;
            quantidade.RegisterValueChangedCallback(e => quantidadeRecrutamentoUI = Mathf.Clamp(e.newValue, 1, 100));
            protocolos.Add(quantidade);

            VisualElement escolhas = Card("RECRUTAMENTO DIRECIONADO");
            escolhas.style.flexGrow = 1;
            escolhas.Add(Texto("Escolha a forca de destino; os recrutas entram na fila de formacao e nao criam clones de unidade.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
            VisualElement linhaForcas = Linha();
            QuartelForcaV2[] forcas = { QuartelForcaV2.Infantaria, QuartelForcaV2.Veiculos, QuartelForcaV2.Naval, QuartelForcaV2.Aerea };
            for (int i = 0; i < forcas.Length; i++)
            {
                QuartelForcaV2 forca = forcas[i];
                Button botaoForca = BotaoAcao("RECRUTAR " + NomeForca(forca), () => admin.SolicitarRecrutamentoManual(forca, quantidadeRecrutamentoUI), false, podeRecrutar, podeRecrutar ? string.Empty : "Sem alistaveis ou meta de efetivo atingida");
                botaoForca.style.minWidth = 150;
                linhaForcas.Add(botaoForca);
            }
            escolhas.Add(linhaForcas);
            conteudo.Add(escolhas);
        }
        Toggle toggleTreino = new Toggle("Treinamento automatico de unidades em repouso")
        {
            value = quartel.treinamentoAutomatico
        };
        toggleTreino.RegisterValueChangedCallback(e => quartel.treinamentoAutomatico = e.newValue);
        protocolos.Add(toggleTreino);
        protocolos.Add(LinhaInformacao("Modo atual", quartel.treinamentoAutomatico ? "AUTOMATICO" : "MANUAL"));
        protocolos.Add(LinhaInformacao("Treinamento passivo legado", quartel.treinamentoPassivo ? "ATIVO" : "DESATIVADO"));
        conteudo.Add(protocolos);

        VisualElement fila = Card("FILA E CRITERIOS");
        DadosPaisGoverno pais = ObterPaisJogador();
        fila.Add(LinhaInformacao("Alistaveis nacionais", pais != null ? pais.alistaveis.ToString("N0") : "n/d"));
        fila.Add(LinhaInformacao("Meta de efetivo local", quartel.metaEfetivo.ToString("N0")));
        fila.Add(LinhaInformacao("Tempo base de formacao", quartel.tempoFormacaoSegundos.ToString("0") + " s"));
        fila.Add(LinhaInformacao("Meta de recrutamento diario", admin != null ? admin.recrutamentoPorDia.ToString("N0") : "n/d"));
        fila.Add(LinhaInformacao("Recrutas no dia", snapshot != null ? snapshot.recrutasRecrutados.ToString("N0") : "0"));
        fila.Add(LinhaInformacao("Em formacao", snapshot != null ? snapshot.recrutasEmFormacao.ToString("N0") : "0"));
        fila.Add(LinhaInformacao("Progresso", snapshot != null ? (snapshot.progressoTreinamento * 100f).ToString("0") + "%  |  " + snapshot.segundosRestantesTreinamento.ToString("0.0") + " s" : "0%"));
        fila.Add(LinhaInformacao("Forca de destino", snapshot != null ? snapshot.forcaTreinamento : "Nenhuma"));
        if (snapshot != null)
        {
            fila.Add(LinhaInformacao("Distribuicao", "Inf " + snapshot.recrutasInfantaria + " | Veic " + snapshot.recrutasVeiculos + " | Naval " + snapshot.recrutasNaval + " | Aerea " + snapshot.recrutasAerea));
        }
        fila.Add(LinhaInformacao("Ultimo evento", snapshot != null ? snapshot.ultimoEvento : "n/d"));
        fila.Add(Texto("A fila usa os dados do SistemaGovernoMundial/SistemaMilitar. O Quartel nao instancia clones nem substitui a producao de unidades.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(fila);
    }

    private void ConstruirAbaFolha()
    {
        AdicionarCabecalho("FOLHA MILITAR", "Leitura de custo e responsabilidade fiscal do efetivo.");
        DadosPaisGoverno pais = ObterPaisJogador();
        VisualElement resumo = Card("CUSTOS ATUAIS");
        resumo.Add(LinhaInformacao("Custo de manutencao nacional", pais != null ? "$" + pais.custoManutencao.ToString("N2") : "n/d"));
        resumo.Add(LinhaInformacao("Militares ativos", pais != null ? pais.populacaoMilitarAtiva.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Reservistas", pais != null ? pais.reservistas.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Saldo do tesouro", GerenciadorRecursos.Instancia != null ? "$" + GerenciadorRecursos.Instancia.dinheiro.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Folha diaria do pessoal alocado", snapshot != null ? "$" + snapshot.custoFolhaDiario.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Folha do periodo", snapshot != null ? "$" + snapshot.custoFolhaCalculado.ToString("N0") + " / " + snapshot.periodoFolhaDias + " dias" : "n/d"));
        resumo.Add(LinhaInformacao("Proximo vencimento", snapshot != null ? "dia " + snapshot.proximoDiaFolha : "n/d"));
        resumo.Add(LinhaInformacao("Pagamento", snapshot != null ? (snapshot.ultimoPagamentoRealizado ? "DESCONTADO" : "PENDENTE") : "n/d"));
        resumo.Add(LinhaInformacao("Dias pendentes", snapshot != null ? snapshot.diasFolhaPendentes.ToString("N0") : "n/d"));
        resumo.Add(LinhaInformacao("Total pago pelo Quartel", snapshot != null ? "$" + snapshot.folhaPagaTotal.ToString("N0") : "n/d"));
        conteudo.Add(resumo);

        VisualElement nota = Card("REGRA DE FOLHA");
        nota.Add(Texto(snapshot != null && snapshot.folhaPendente
            ? "Pagamento pendente: " + snapshot.ultimoMotivoFolha
            : "A folha nacional continua no governo. Este quadro cobre apenas o pessoal alocado por este Quartel e cobra o periodo configurado; nao mistura reservistas com tripulacao.", 13, new Color(0.72f, 0.82f, 0.82f), FontStyle.Normal));
        conteudo.Add(nota);
    }

    private void ConstruirAbaTripulacoes()
    {
        AdicionarCabecalho("TRIPULACOES", "Controle administrativo de pessoal minimo para unidades navais.");
        QuartelForcaSnapshotV2 naval = ObterResumoForca(QuartelForcaV2.Naval);
        QuartelForcaSnapshotV2 aerea = ObterResumoForca(QuartelForcaV2.Aerea);
        int navais = naval != null ? naval.unidades : 0;
        int aereos = aerea != null ? aerea.unidades : 0;

        VisualElement resumo = Linha();
        resumo.Add(Cartao("NAVIOS", navais.ToString("N0"), "unidades navais reconhecidas"));
        resumo.Add(Cartao("AERONAVES", aereos.ToString("N0"), "unidades aereas reconhecidas"));
        resumo.Add(Cartao("EXIGIDO", snapshot != null ? snapshot.pessoalExigido.ToString("N0") : "0", "pessoal minimo"));
        resumo.Add(Cartao("ALOCADO", snapshot != null ? snapshot.pessoalAlocado.ToString("N0") : "0", "somente ativos"));
        resumo.Add(Cartao("INOPERANTES", snapshot != null ? snapshot.unidadesInoperantes.ToString("N0") : "0", "sem tripulacao"));
        conteudo.Add(resumo);

        VisualElement quadro = Card("SITUACAO DE GUARNICAO");
        quadro.Add(LinhaInformacao("Estado", navais == 0 ? "SEM NAVIOS NO CENSO" : "COBERTURA ADMINISTRATIVA ATIVA"));
        quadro.Add(LinhaInformacao("Reserva usada como tripulacao", "NAO"));
        quadro.Add(LinhaInformacao("Tripulacao naval exigida / alocada", naval != null ? naval.pessoalExigido + " / " + naval.pessoalAlocado : "n/d"));
        quadro.Add(LinhaInformacao("Tripulacao aerea exigida / alocada", aerea != null ? aerea.pessoalExigido + " / " + aerea.pessoalAlocado : "n/d"));
        quadro.Add(LinhaInformacao("Aeronaves conectadas ao Quartel", snapshot != null ? snapshot.aeronavesNoRaio.ToString("N0") : "n/d"));
        quadro.Add(LinhaInformacao("Unidades sem militares", snapshot != null && snapshot.unidadesInoperantes > 0 ? "INOPERANTES" : "OPERACIONAIS"));
        quadro.Add(Texto("Aba administrativa: nao emite ordem de movimento, patrulha ou combate. Quando faltam militares ativos, o bloqueio administrativo recusa novas ordens sem desativar o objeto.", 13, new Color(0.70f, 0.82f, 0.82f), FontStyle.Normal));
        conteudo.Add(quadro);
    }

    private void ConstruirAbaResgate()
    {
        AdicionarCabecalho("RESGATE", "Alertas de integridade e reparo das unidades do jogador dentro da cobertura.");
        int danificadas = snapshot != null ? snapshot.unidadesDanificadas : 0;
        int avaliadas = snapshot != null ? snapshot.unidadesAvaliadasResgate : 0;

        VisualElement resumo = Linha();
        resumo.Add(Cartao("AVALIADAS", avaliadas.ToString("N0"), "unidades na cobertura"));
        resumo.Add(Cartao("ALERTAS", danificadas.ToString("N0"), "unidades com dano"));
        resumo.Add(Cartao("PROTOCOLO", danificadas > 0 ? "ATENCAO" : "NORMAL", "estado de recuperacao"));
        resumo.Add(Cartao("PERDAS", snapshot != null ? snapshot.perdasRegistradas.ToString("N0") : "0", "historico registrado"));
        conteudo.Add(resumo);

        VisualElement acoes = Card("ACAO DE RECUPERACAO");
        acoes.Add(BotaoAcao("SOLICITAR RECUPERACAO MANUAL", quartel.SolicitarResgateManual, false, danificadas > 0, "Nenhuma unidade danificada na cobertura"));
        acoes.Add(LinhaInformacao("Aviso de resgate", snapshot != null && snapshot.unidadesComAlertaResgate > 0 ? snapshot.ultimoAvisoResgate : "Nenhum alerta"));
        acoes.Add(Texto("O reparo e encaminhado ao GerenciadorQuartel e nao reativa unidades, altera parentesco ou teletransporta objetos.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(acoes);
    }

    private void ConstruirAbaComunicacoes()
    {
        AdicionarCabecalho("COMUNICACOES", "Protocolos de radio, chamada automatica e defesa da base.");
        VisualElement protocolos = Card("REDE DE COMANDO");
        Toggle recolhimento = new Toggle("Recolhimento automatico de unidades ociosas")
        {
            value = quartel.recolhimentoAutomatico
        };
        recolhimento.RegisterValueChangedCallback(e => quartel.recolhimentoAutomatico = e.newValue);
        protocolos.Add(recolhimento);
        Toggle defesa = new Toggle("Defesa automatica e despertar da base")
        {
            value = quartel.modoDefensivoAtivo
        };
        defesa.RegisterValueChangedCallback(e => quartel.modoDefensivoAtivo = e.newValue);
        protocolos.Add(defesa);
        Toggle combustivel = new Toggle("Abastecimento automatico global de Tracks")
        {
            value = CaminhaoCombustivel.AbastecimentoAutomaticoGlobal
        };
        combustivel.RegisterValueChangedCallback(e => CaminhaoCombustivel.AbastecimentoAutomaticoGlobal = e.newValue);
        protocolos.Add(combustivel);
        protocolos.Add(LinhaInformacao("Raio de cobertura", quartel.raioDeCobertura.ToString("0") + " m"));
        protocolos.Add(LinhaInformacao("Contatos inimigos transmitidos", snapshot != null ? snapshot.contatosInimigos.ToString("N0") : "n/d"));
        protocolos.Add(LinhaInformacao("Submarinos na superficie", snapshot != null ? snapshot.contatosSubmarinos.ToString("N0") : "n/d"));
        protocolos.Add(LinhaInformacao("Ameacas de missil", snapshot != null ? snapshot.contatosMisseis.ToString("N0") : "n/d"));
        protocolos.Add(LinhaInformacao("Aeronaves com telemetria local", snapshot != null ? snapshot.aeronavesNoRaio.ToString("N0") : "n/d"));
        conteudo.Add(protocolos);

        VisualElement registro = Card("REGISTRO DE COMUNICACOES — QUARTEL / AERONAVE");
        if (snapshot != null && snapshot.comunicacoes != null && snapshot.comunicacoes.Length > 0)
        {
            int limite = Mathf.Min(12, snapshot.comunicacoes.Length);
            for (int i = 0; i < limite; i++)
            {
                QuartelComunicacaoSnapshotV2 comunicacao = snapshot.comunicacoes[i];
                if (comunicacao == null) continue;
                string tituloRegistro = "[" + comunicacao.horario + "] " + comunicacao.origem;
                string detalheRegistro = comunicacao.tipo + " | " + comunicacao.distanciaAoQuartel.ToString("0") + " m | " + comunicacao.mensagem;
                registro.Add(LinhaUnidade(comunicacao.inimigo ? "ALERTA" : "RADIO", tituloRegistro, detalheRegistro));
            }
        }
        else
        {
            registro.Add(Texto("Aguardando telemetria do E-3. O registro aparece quando o aviao estiver em missao e transmitir contatos.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        }
        conteudo.Add(registro);

        VisualElement acoes = Card("LOGISTICA");
        acoes.Add(BotaoAcao("CARREGAR TRACKS NESTE QG", quartel.SolicitarTracksNoQuartel));
        acoes.Add(BotaoAcao("FORCAR RETORNO A BASE", quartel.SolicitarRetornoTracks));
        conteudo.Add(acoes);
    }

    private void ConstruirAbaCartaNautica()
    {
        AdicionarCabecalho("CARTA NAUTICA", "Carta topografica operacional; leitura de telemetria sem substituir ordens ou FLIR.");
        VisualElement modos = Linha();
        modos.style.marginBottom = 8;
        Button modo2D = Botao("2D TOPOGRAFICO", 0, 36, cartaVista3D ? new Color(0.07f, 0.15f, 0.18f) : new Color(0.08f, 0.30f, 0.34f));
        Button modo3D = Botao("3D TOPOGRAFICO", 0, 36, cartaVista3D ? new Color(0.08f, 0.30f, 0.34f) : new Color(0.07f, 0.15f, 0.18f));
        modo2D.style.flexGrow = 1;
        modo3D.style.flexGrow = 1;
        modo2D.clicked += () => { cartaVista3D = false; AtualizarPainel(); };
        modo3D.clicked += () => { cartaVista3D = true; AtualizarPainel(); };
        modos.Add(modo2D);
        modos.Add(modo3D);
        conteudo.Add(modos);

        carta = new VisualElement { name = "quartel-carta" };
        carta.style.height = 470;
        carta.style.flexDirection = FlexDirection.Row;
        carta.style.position = Position.Relative;
        carta.style.backgroundColor = new Color(0.015f, 0.075f, 0.10f, 1f);
        carta.style.borderTopWidth = 1;
        carta.style.borderBottomWidth = 1;
        carta.style.borderLeftWidth = 1;
        carta.style.borderRightWidth = 1;
        carta.style.borderTopColor = new Color(0.22f, 0.55f, 0.58f);
        carta.style.borderBottomColor = new Color(0.22f, 0.55f, 0.58f);
        carta.style.borderLeftColor = new Color(0.22f, 0.55f, 0.58f);
        carta.style.borderRightColor = new Color(0.22f, 0.55f, 0.58f);
        DesenharCartaToolkitNovo();
        conteudo.Add(carta);

        VisualElement legenda = Card("LEGENDA");
        legenda.Add(LinhaInformacao("● QG", "posicao deste Quartel"));
        legenda.Add(LinhaInformacao("● Azul", "unidade aerea"));
        legenda.Add(LinhaInformacao("● Verde", "unidade terrestre"));
        legenda.Add(LinhaInformacao("● Ciano", "unidade naval"));
        legenda.Add(LinhaInformacao("Atualizacao", "a cada 0,75 s enquanto a aba esta aberta"));
        conteudo.Add(legenda);
    }

    private void DesenharCartaToolkitNovo()
    {
        if (carta == null || quartel == null) return;
        carta.Clear();
        carta.style.minHeight = 470;
        carta.style.overflow = Overflow.Hidden;

        float raio = Mathf.Max(100f, ObterRaioCarta());
        VisualElement mapa = new VisualElement { name = "quartel-carta-mapa" };
        mapa.style.width = new Length(68f, LengthUnit.Percent);
        mapa.style.minWidth = 430;
        mapa.style.position = Position.Relative;
        mapa.style.overflow = Overflow.Hidden;
        mapa.style.backgroundColor = new Color(0.008f, 0.065f, 0.09f, 1f);
        VisualElement telemetria = new VisualElement { name = "quartel-carta-telemetria" };
        telemetria.style.flexGrow = 1;
        telemetria.style.paddingLeft = 12;
        telemetria.style.paddingRight = 12;
        telemetria.style.paddingTop = 12;
        telemetria.style.paddingBottom = 10;
        telemetria.style.backgroundColor = new Color(0.018f, 0.075f, 0.095f, 1f);
        carta.Add(mapa);
        carta.Add(telemetria);

        Texture terreno = ObterTexturaTerrenoCarta(raio, 1.75f);
        if (terreno != null)
        {
            Image imagemTerreno = new Image { image = terreno, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            imagemTerreno.style.position = Position.Absolute;
            imagemTerreno.style.left = 0;
            imagemTerreno.style.right = 0;
            imagemTerreno.style.top = 0;
            imagemTerreno.style.bottom = 0;
            imagemTerreno.style.opacity = 0.86f;
            mapa.Add(imagemTerreno);
        }

        for (int i = 1; i < 10; i++)
        {
            VisualElement vertical = new VisualElement();
            vertical.style.position = Position.Absolute;
            vertical.style.left = new Length(i * 10f, LengthUnit.Percent);
            vertical.style.top = 0;
            vertical.style.bottom = 0;
            vertical.style.width = 1;
            vertical.style.backgroundColor = new Color(0.18f, 0.42f, 0.44f, 0.25f);
            mapa.Add(vertical);
            VisualElement horizontal = new VisualElement();
            horizontal.style.position = Position.Absolute;
            horizontal.style.top = new Length(i * 10f, LengthUnit.Percent);
            horizontal.style.left = 0;
            horizontal.style.right = 0;
            horizontal.style.height = 1;
            horizontal.style.backgroundColor = new Color(0.18f, 0.42f, 0.44f, 0.25f);
            mapa.Add(horizontal);
        }

        Label tituloMapa = Texto(cartaVista3D ? "VISUALIZACAO 3D INCLINADA" : "TOPOGRAFIA 2D  |  CURVAS DE NIVEL", 12, Color.white, FontStyle.Bold);
        tituloMapa.style.position = Position.Absolute;
        tituloMapa.style.left = 12;
        tituloMapa.style.top = 10;
        tituloMapa.pickingMode = PickingMode.Ignore;
        mapa.Add(tituloMapa);

        VisualElement centro = Marcador("QG", 50f, 50f, new Color(0.95f, 0.80f, 0.25f), 18);
        mapa.Add(centro);
        Label nomeQG = Texto("QG  " + quartel.name, 11, Color.white, FontStyle.Bold);
        nomeQG.style.position = Position.Absolute;
        nomeQG.style.left = new Length(50f, LengthUnit.Percent);
        nomeQG.style.top = new Length(50f, LengthUnit.Percent);
        nomeQG.style.marginLeft = 12;
        nomeQG.style.marginTop = -10;
        nomeQG.pickingMode = PickingMode.Ignore;
        mapa.Add(nomeQG);

        GarantirCartaTopograficaView();
        int total = 0;
        int totalMisseis = 0;
        if (cartaTopograficaView != null)
        {
            for (int i = 0; i < cartaTopograficaView.Unidades.Count; i++)
            {
                QuartelCartaTopograficaView.UnidadeTelemetria unidade = cartaTopograficaView.Unidades[i];
                if (unidade == null) continue;
                Vector3 local = quartel.transform.InverseTransformPoint(unidade.posicao);
                float x = Mathf.Clamp01(local.x / (raio * 2f) + 0.5f) * 100f;
                float y = (1f - Mathf.Clamp01(local.z / (raio * 2f) + 0.5f)) * 100f;
                Color cor = !unidade.aliada ? new Color(1f, 0.25f, 0.20f) : CorTipoCarta(unidade.tipo);
                Button marcador = CriarMarcadorCartaToolkit(mapa, unidade.nome + " | " + unidade.estado, x, y, cor, 16);
                string id = unidade.id;
                marcador.clicked += () => { cartaUnidadeSelecionadaId = id; cartaMissilSelecionadoId = string.Empty; AtualizarPainel(); };
                total++;
            }
            for (int i = 0; i < cartaTopograficaView.Misseis.Count; i++)
            {
                QuartelCartaTopograficaView.MissilTelemetria missil = cartaTopograficaView.Misseis[i];
                if (missil == null) continue;
                Vector3 local = quartel.transform.InverseTransformPoint(missil.posicao);
                float x = Mathf.Clamp01(local.x / (raio * 2f) + 0.5f) * 100f;
                float y = (1f - Mathf.Clamp01(local.z / (raio * 2f) + 0.5f)) * 100f;
                Color cor = missil.aliado ? new Color(0.35f, 0.72f, 1f) : new Color(1f, 0.22f, 0.18f);
                Button marcador = CriarMarcadorCartaToolkit(mapa, missil.nome + " | " + missil.estado, x, y, cor, 12);
                string id = missil.id;
                marcador.clicked += () => { cartaMissilSelecionadoId = id; cartaUnidadeSelecionadaId = string.Empty; AtualizarPainel(); };
                totalMisseis++;
            }
        }

        Label escala = Texto("RAIO " + raio.ToString("0") + " m  |  UNIDADES: " + total + "  |  MISSEIS: " + totalMisseis, 11, new Color(0.65f, 0.82f, 0.82f), FontStyle.Normal);
        escala.style.position = Position.Absolute;
        escala.style.left = 10;
        escala.style.bottom = 8;
        escala.pickingMode = PickingMode.Ignore;
        mapa.Add(escala);

        QuartelCartaTopograficaView.UnidadeTelemetria selecionada = cartaTopograficaView != null ? cartaTopograficaView.EncontrarUnidade(cartaUnidadeSelecionadaId) : null;
        QuartelCartaTopograficaView.MissilTelemetria missilSelecionado = cartaTopograficaView != null ? cartaTopograficaView.EncontrarMissil(cartaMissilSelecionadoId) : null;
        telemetria.Add(Texto("TELEMETRIA DA UNIDADE", 14, new Color(0.16f, 0.79f, 0.98f), FontStyle.Bold));
        if (selecionada != null)
        {
            telemetria.Add(Texto(selecionada.nome, 19, Color.white, FontStyle.Bold));
            telemetria.Add(Texto(selecionada.tipo + " | EQUIPE " + selecionada.equipe + " | " + selecionada.situacao, 11, new Color(0.55f, 0.85f, 0.86f), FontStyle.Bold));
            telemetria.Add(LinhaInformacao("Estado", selecionada.estado));
            telemetria.Add(LinhaInformacao("Missao", selecionada.missao));
            telemetria.Add(LinhaInformacao("Altitude absoluta", selecionada.altitudeAbsoluta.ToString("0") + " m"));
            telemetria.Add(LinhaInformacao("Elevacao do terreno", selecionada.elevacaoTerreno.ToString("0") + " m"));
            telemetria.Add(LinhaInformacao("Altura acima do solo", selecionada.alturaAcimaDoSolo.ToString("0") + " m"));
            telemetria.Add(LinhaInformacao("Velocidade", (selecionada.velocidadeMetrosPorSegundo * 3.6f).ToString("0") + " km/h"));
            telemetria.Add(LinhaInformacao("Rumo", selecionada.rumo));
            telemetria.Add(LinhaInformacao("Combustivel", selecionada.combustivelCapacidade > 0f ? (selecionada.combustivelPercentual * 100f).ToString("0") + "%" : "N/A"));
            telemetria.Add(LinhaInformacao("Percorrida", selecionada.distanciaPercorrida.ToString("0") + " m"));
            telemetria.Add(LinhaInformacao("Restante", selecionada.possuiDestino ? selecionada.distanciaRestante.ToString("0") + " m" : "Sem destino"));
            telemetria.Add(LinhaInformacao("Chegada", selecionada.possuiDestino ? FormatarTempo(selecionada.tempoEstimadoSegundos) : "N/A"));
            telemetria.Add(LinhaInformacao("Armamento", selecionada.armamento));
        }
        else if (missilSelecionado != null)
        {
            telemetria.Add(Texto(missilSelecionado.nome, 19, Color.white, FontStyle.Bold));
            telemetria.Add(Texto(missilSelecionado.tipo + " | " + missilSelecionado.estado, 11, new Color(0.55f, 0.85f, 0.86f), FontStyle.Bold));
            telemetria.Add(LinhaInformacao("Lancador", missilSelecionado.origem));
            telemetria.Add(LinhaInformacao("Percorrida", missilSelecionado.distanciaPercorrida.ToString("0") + " m"));
            telemetria.Add(LinhaInformacao("Ate o impacto", missilSelecionado.distanciaRestante.ToString("0") + " m"));
            telemetria.Add(LinhaInformacao("Velocidade", (missilSelecionado.velocidadeMetrosPorSegundo * 3.6f).ToString("0") + " km/h"));
            telemetria.Add(LinhaInformacao("Impacto provavel", "X " + missilSelecionado.pontoProvavelImpacto.x.ToString("0") + " | Z " + missilSelecionado.pontoProvavelImpacto.z.ToString("0")));
            telemetria.Add(LinhaInformacao("Tempo de voo", FormatarTempo(missilSelecionado.tempoDesdeLancamento)));
            if (missilSelecionado.guiagemPerdida) telemetria.Add(Texto("GUIAGEM PERDIDA | impacto recalculado", 11, new Color(1f, 0.68f, 0.20f), FontStyle.Bold));
        }
        else
        {
            telemetria.Add(Texto("Clique em uma unidade ou missil no mapa para abrir a telemetria.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
            telemetria.Add(LinhaInformacao("Unidades disponiveis", total.ToString("N0")));
            telemetria.Add(LinhaInformacao("Misseis em voo", totalMisseis.ToString("N0")));
            telemetria.Add(LinhaInformacao("Rota", "selecione uma unidade"));
        }
    }

    private Button CriarMarcadorCartaToolkit(VisualElement mapa, string tooltip, float x, float y, Color cor, float tamanho)
    {
        Button marcador = new Button { text = string.Empty, tooltip = tooltip };
        marcador.style.position = Position.Absolute;
        marcador.style.left = new Length(x, LengthUnit.Percent);
        marcador.style.top = new Length(y, LengthUnit.Percent);
        marcador.style.width = tamanho;
        marcador.style.height = tamanho;
        marcador.style.marginLeft = -tamanho * 0.5f;
        marcador.style.marginTop = -tamanho * 0.5f;
        marcador.style.backgroundColor = cor;
        marcador.style.borderTopLeftRadius = tamanho;
        marcador.style.borderTopRightRadius = tamanho;
        marcador.style.borderBottomLeftRadius = tamanho;
        marcador.style.borderBottomRightRadius = tamanho;
        marcador.style.paddingLeft = 0;
        marcador.style.paddingRight = 0;
        mapa.Add(marcador);
        return marcador;
    }

    private void DesenharCarta()
    {
        if (carta == null || quartel == null) return;
        carta.Clear();

        carta.style.minHeight = 420;
        carta.style.overflow = Overflow.Visible;

        float raio = ObterRaioCarta();
        Texture terreno = ObterTexturaTerrenoCarta(raio, ObterAspectoCarta());
        if (terreno != null)
        {
            Image imagemTerreno = new Image
            {
                image = terreno,
                scaleMode = ScaleMode.StretchToFill,
                pickingMode = PickingMode.Ignore
            };
            imagemTerreno.style.position = Position.Absolute;
            imagemTerreno.style.left = 0;
            imagemTerreno.style.right = 0;
            imagemTerreno.style.top = 0;
            imagemTerreno.style.bottom = 0;
            imagemTerreno.style.opacity = 0.92f;
            carta.Add(imagemTerreno);
        }

        for (int i = 1; i < 10; i++)
        {
            VisualElement vertical = new VisualElement();
            vertical.style.position = Position.Absolute;
            vertical.style.left = new Length(i * 10f, LengthUnit.Percent);
            vertical.style.top = 0;
            vertical.style.bottom = 0;
            vertical.style.width = 1;
            vertical.style.backgroundColor = new Color(0.18f, 0.42f, 0.44f, 0.25f);
            carta.Add(vertical);

            VisualElement horizontal = new VisualElement();
            horizontal.style.position = Position.Absolute;
            horizontal.style.top = new Length(i * 10f, LengthUnit.Percent);
            horizontal.style.left = 0;
            horizontal.style.right = 0;
            horizontal.style.height = 1;
            horizontal.style.backgroundColor = new Color(0.18f, 0.42f, 0.44f, 0.25f);
            carta.Add(horizontal);
        }

        Label norte = Texto("N", 16, new Color(0.96f, 0.82f, 0.35f), FontStyle.Bold);
        norte.style.position = Position.Absolute;
        norte.style.left = new Length(50f, LengthUnit.Percent);
        norte.style.top = 7;
        carta.Add(norte);

        VisualElement centro = Marcador("QG", 50f, 50f, new Color(0.95f, 0.80f, 0.25f), 18);
        carta.Add(centro);
        Label nomeQG = Texto("QG  " + quartel.name, 11, Color.white, FontStyle.Bold);
        nomeQG.style.position = Position.Absolute;
        nomeQG.style.left = new Length(50f, LengthUnit.Percent);
        nomeQG.style.top = new Length(50f, LengthUnit.Percent);
        nomeQG.style.marginLeft = 12;
        nomeQG.style.marginTop = -10;
        carta.Add(nomeQG);

        RegistroEntidadesJogo.FillUnidades(unidadesRegistradas);
        if (unidadesRegistradas.Count == 0)
        {
            IdentidadeUnidade[] encontrados = FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                if (encontrados[i] != null && !unidadesRegistradas.Contains(encontrados[i]))
                    unidadesRegistradas.Add(encontrados[i]);
            }
        }

        int total = 0;
        for (int i = 0; i < unidadesRegistradas.Count; i++)
        {
            IdentidadeUnidade identidade = unidadesRegistradas[i];
            if (identidade == null || identidade.teamID != quartel.teamID || identidade.transform == transform) continue;
            Vector3 local = transform.InverseTransformPoint(identidade.transform.position);
            if (Mathf.Abs(local.x) > raio || Mathf.Abs(local.z) > raio) continue;

            float x = Mathf.Clamp01(local.x / (raio * 2f) + 0.5f) * 100f;
            float y = (1f - Mathf.Clamp01(local.z / (raio * 2f) + 0.5f)) * 100f;
            BoeingE3Reconhecimento.ContatoReconhecimento contato;
            bool contatoInimigo = BoeingE3Reconhecimento.TryObterContato(quartel.teamID, identidade.GetInstanceID(), out contato)
                && contato != null && contato.inimigo;
            Color cor = contatoInimigo
                ? new Color(1f, 0.25f, 0.20f)
                : identidade.tipoUnidade == TipoUnidade.Naval
                ? new Color(0.20f, 0.90f, 0.95f)
                : identidade.tipoUnidade == TipoUnidade.Aereo
                    ? new Color(0.35f, 0.62f, 1f)
                    : new Color(0.35f, 0.90f, 0.48f);
            string estado = ObterEstadoCarta(identidade);
            carta.Add(Marcador(identidade.name + " | " + estado, x, y, cor, 10));
            total++;
            if (total >= 80) break;
        }

        // Contatos inimigos recebidos pelo E-3 também fazem parte da carta,
        // mesmo que a unidade ainda não esteja dentro do registro administrativo.
        for (int i = 0; i < unidadesRegistradas.Count && total < 100; i++)
        {
            IdentidadeUnidade identidade = unidadesRegistradas[i];
            if (identidade == null || identidade.teamID == quartel.teamID || identidade.teamID <= 0) continue;
            BoeingE3Reconhecimento.ContatoReconhecimento contato;
            if (!BoeingE3Reconhecimento.TryObterContato(quartel.teamID, identidade.GetInstanceID(), out contato) || contato == null) continue;

            Vector3 local = transform.InverseTransformPoint(contato.ultimaPosicaoConhecida);
            if (Mathf.Abs(local.x) > raio || Mathf.Abs(local.z) > raio) continue;
            float x = Mathf.Clamp01(local.x / (raio * 2f) + 0.5f) * 100f;
            float y = (1f - Mathf.Clamp01(local.z / (raio * 2f) + 0.5f)) * 100f;
            carta.Add(Marcador("CONTATO: " + contato.nomeAlvo + " | " + contato.tipo, x, y, new Color(1f, 0.25f, 0.20f), 10));
            total++;
        }

        Label escala = Texto("RAIO OPERACIONAL  " + raio.ToString("0") + " m  |  CONTATOS: " + total, 11, new Color(0.65f, 0.82f, 0.82f), FontStyle.Normal);
        escala.style.position = Position.Absolute;
        escala.style.left = 10;
        escala.style.bottom = 8;
        carta.Add(escala);
    }

    private float ObterAspectoCarta()
    {
        float largura = carta != null ? carta.resolvedStyle.width : 0f;
        float altura = carta != null ? carta.resolvedStyle.height : 0f;
        if (float.IsNaN(largura) || largura < 1f || float.IsNaN(altura) || altura < 1f)
        {
            return 1.9f;
        }

        return Mathf.Clamp(largura / altura, 1f, 4f);
    }

    private Texture ObterTexturaTerrenoCarta(float raio, float aspecto)
    {
        if (quartel == null)
        {
            return null;
        }

        if (cartaTerrenoRenderer == null)
        {
            cartaTerrenoRenderer = GetComponent<CartaTerrenoRenderer>();
            if (cartaTerrenoRenderer == null)
            {
                cartaTerrenoRenderer = gameObject.AddComponent<CartaTerrenoRenderer>();
            }
        }

        return cartaTerrenoRenderer.Renderizar(transform.position, raio, aspecto, cartaVista3D);
    }

    private float ObterRaioCarta()
    {
        float raio = Mathf.Max(1f, quartel != null ? quartel.raioDeCobertura : 2000f);
        RegistroEntidadesJogo.FillAvioes(avioesRegistrados);
        if (avioesRegistrados.Count == 0)
        {
            ControleAviao[] encontrados = FindObjectsByType<ControleAviao>(FindObjectsSortMode.None);
            for (int i = 0; i < encontrados.Length; i++)
            {
                if (encontrados[i] != null && !avioesRegistrados.Contains(encontrados[i]))
                    avioesRegistrados.Add(encontrados[i]);
            }
        }

        for (int i = 0; i < avioesRegistrados.Count; i++)
        {
            ControleAviao aviao = avioesRegistrados[i];
            if (aviao == null) continue;
            IdentidadeUnidade identidade = aviao.GetComponent<IdentidadeUnidade>();
            if (identidade == null || identidade.teamID != quartel.teamID) continue;
            BoeingE3Reconhecimento e3 = aviao.GetComponent<BoeingE3Reconhecimento>();
            if (e3 != null) raio = Mathf.Max(raio, e3.alcanceReconhecimento);
        }
        return raio;
    }

    private VisualElement Marcador(string texto, float xPercent, float yPercent, Color cor, int tamanho)
    {
        VisualElement marcador = new VisualElement();
        marcador.style.position = Position.Absolute;
        marcador.style.left = new Length(xPercent, LengthUnit.Percent);
        marcador.style.top = new Length(yPercent, LengthUnit.Percent);
        marcador.style.width = tamanho;
        marcador.style.height = tamanho;
        marcador.style.marginLeft = -tamanho * 0.5f;
        marcador.style.marginTop = -tamanho * 0.5f;
        marcador.style.backgroundColor = cor;
        marcador.style.borderTopLeftRadius = tamanho;
        marcador.style.borderTopRightRadius = tamanho;
        marcador.style.borderBottomLeftRadius = tamanho;
        marcador.style.borderBottomRightRadius = tamanho;
        marcador.tooltip = texto;
        return marcador;
    }

    private void ConstruirAbaArsenal()
    {
        AdicionarCabecalho("ARSENAL", "Estoque existente e compras encaminhadas ao sistema de recursos atual.");
        VisualElement estoque = Linha();
        estoque.Add(Cartao("MISSEIS", quartel.misseisArmazenados.ToString("N0"), "unidades armazenadas"));
        estoque.Add(Cartao("MUNICAO", quartel.municaoArmazenada.ToString("N0"), "pacotes armazenados"));
        estoque.Add(Cartao("FUNDO", GerenciadorRecursos.Instancia != null ? "$" + GerenciadorRecursos.Instancia.dinheiro.ToString("N0") : "n/d", "saldo real do GerenciadorRecursos"));
        conteudo.Add(estoque);

        VisualElement compras = Card("REABASTECER ARSENAL");
        long saldo = GerenciadorRecursos.Instancia != null ? GerenciadorRecursos.Instancia.dinheiro : -1L;
        compras.Add(BotaoAcao("ENCOMENDAR 10 MISSEIS  (-$" + quartel.precoMissil.ToString("N0") + ")", () => quartel.TentarEncomendarMisseis(), false, saldo >= quartel.precoMissil, "Saldo do GerenciadorRecursos insuficiente"));
        compras.Add(BotaoAcao("ENCOMENDAR 100 PACOTES  (-$" + quartel.precoMunicao.ToString("N0") + ")", () => quartel.TentarEncomendarMunicao(), false, saldo >= quartel.precoMunicao, "Saldo do GerenciadorRecursos insuficiente"));
        compras.Add(Texto("Os valores e o debito continuam sob responsabilidade de GerenciadorRecursos; o painel nao duplica a contabilidade.", 12, new Color(0.66f, 0.78f, 0.78f), FontStyle.Normal));
        conteudo.Add(compras);

        VisualElement logistica = Card("LOGISTICA DE COMBUSTIVEL");
        Toggle abastecimento = new Toggle("Abastecimento automatico de Tracks")
        {
            value = CaminhaoCombustivel.AbastecimentoAutomaticoGlobal
        };
        abastecimento.RegisterValueChangedCallback(e => CaminhaoCombustivel.AbastecimentoAutomaticoGlobal = e.newValue);
        logistica.Add(abastecimento);
        conteudo.Add(logistica);
    }

    private void AdicionarCabecalho(string nome, string descricao)
    {
        conteudo.Add(Texto(nome, 20, new Color(0.92f, 0.82f, 0.43f), FontStyle.Bold));
        conteudo.Add(Texto(descricao, 13, new Color(0.68f, 0.82f, 0.82f), FontStyle.Normal));
        VisualElement separador = new VisualElement();
        separador.style.height = 1;
        separador.style.marginTop = 8;
        separador.style.marginBottom = 10;
        separador.style.backgroundColor = new Color(0.28f, 0.52f, 0.53f, 0.65f);
        conteudo.Add(separador);
    }

    private VisualElement Card(string nome)
    {
        VisualElement card = new VisualElement();
        card.style.marginBottom = 10;
        card.style.paddingLeft = 12;
        card.style.paddingRight = 12;
        card.style.paddingTop = 10;
        card.style.paddingBottom = 10;
        card.style.backgroundColor = new Color(0.045f, 0.12f, 0.15f, 0.98f);
        card.style.borderTopWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderTopColor = new Color(0.18f, 0.37f, 0.40f);
        card.style.borderBottomColor = new Color(0.18f, 0.37f, 0.40f);
        card.style.borderLeftColor = new Color(0.18f, 0.37f, 0.40f);
        card.style.borderRightColor = new Color(0.18f, 0.37f, 0.40f);
        if (!string.IsNullOrWhiteSpace(nome))
        {
            card.Add(Texto(nome, 13, new Color(0.93f, 0.82f, 0.42f), FontStyle.Bold));
        }
        return card;
    }

    private VisualElement Cartao(string nome, string valor, string detalhe)
    {
        VisualElement cartao = Card(string.Empty);
        cartao.style.flexGrow = 1;
        cartao.style.minWidth = 150;
        cartao.style.marginRight = 8;
        cartao.Add(Texto(nome, 11, new Color(0.58f, 0.76f, 0.77f), FontStyle.Bold));
        cartao.Add(Texto(valor, 22, Color.white, FontStyle.Bold));
        cartao.Add(Texto(detalhe, 11, new Color(0.64f, 0.75f, 0.75f), FontStyle.Normal));
        return cartao;
    }

    private VisualElement LinhaUnidade(string tipo, string nome, string estado)
    {
        VisualElement linha = Linha();
        linha.style.marginTop = 3;
        linha.style.marginBottom = 3;
        linha.style.paddingTop = 6;
        linha.style.paddingBottom = 6;
        linha.style.paddingLeft = 8;
        linha.style.paddingRight = 8;
        linha.style.backgroundColor = new Color(0.025f, 0.08f, 0.10f, 1f);
        linha.Add(Texto(tipo, 11, new Color(0.55f, 0.85f, 0.86f), FontStyle.Bold));
        Label nomeLabel = Texto(nome, 13, Color.white, FontStyle.Normal);
        nomeLabel.style.flexGrow = 1;
        nomeLabel.style.marginLeft = 12;
        linha.Add(nomeLabel);
        linha.Add(Texto(estado, 11, new Color(0.93f, 0.78f, 0.34f), FontStyle.Bold));
        return linha;
    }

    private VisualElement LinhaInformacao(string nome, string valor)
    {
        VisualElement linha = Linha();
        linha.style.marginTop = 3;
        linha.style.marginBottom = 3;
        Label chave = Texto(nome, 12, new Color(0.66f, 0.80f, 0.80f), FontStyle.Normal);
        chave.style.flexGrow = 1;
        linha.Add(chave);
        linha.Add(Texto(valor, 13, Color.white, FontStyle.Bold));
        return linha;
    }

    private Button BotaoAcao(string label, Action acao, bool perigo = false)
    {
        return BotaoAcao(label, acao, perigo, true, string.Empty);
    }

    private Button BotaoAcao(string label, Action acao, bool perigo, bool habilitado, string motivo)
    {
        Button botao = Botao(label, 0, 38, perigo ? new Color(0.34f, 0.12f, 0.10f) : new Color(0.08f, 0.28f, 0.30f));
        botao.style.flexGrow = 1;
        botao.style.marginTop = 4;
        botao.style.marginBottom = 4;
        botao.SetEnabled(habilitado);
        if (!habilitado && !string.IsNullOrWhiteSpace(motivo)) botao.tooltip = motivo;
        if (habilitado) botao.clicked += () => acao?.Invoke();
        return botao;
    }

    private VisualElement Linha()
    {
        VisualElement linha = new VisualElement();
        linha.style.flexDirection = FlexDirection.Row;
        linha.style.alignItems = Align.Center;
        linha.style.flexWrap = Wrap.Wrap;
        return linha;
    }

    private Label Texto(string texto, int tamanho, Color cor, FontStyle estilo)
    {
        Label label = new Label(texto ?? string.Empty);
        label.style.fontSize = tamanho;
        label.style.color = cor;
        label.style.unityFontStyleAndWeight = estilo;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private Button Botao(string texto, float largura, float altura, Color cor)
    {
        Button botao = new Button { text = texto };
        if (largura > 0) botao.style.width = largura;
        botao.style.height = altura;
        botao.style.marginTop = 2;
        botao.style.marginBottom = 2;
        botao.style.backgroundColor = cor;
        botao.style.color = Color.white;
        botao.style.unityFontStyleAndWeight = FontStyle.Bold;
        botao.style.fontSize = 11;
        return botao;
    }

    private QuartelForcaSnapshotV2 ObterResumoForca(QuartelForcaV2 forca)
    {
        if (snapshot == null || snapshot.forcas == null) return null;
        for (int i = 0; i < snapshot.forcas.Length; i++)
        {
            QuartelForcaSnapshotV2 resumo = snapshot.forcas[i];
            if (resumo != null && resumo.forca == forca) return resumo;
        }
        return null;
    }

    private void AdicionarResumoForcas(VisualElement destino)
    {
        if (destino == null) return;
        for (int i = 0; i < 4; i++)
        {
            QuartelForcaSnapshotV2 resumo = ObterResumoForca((QuartelForcaV2)i);
            if (resumo == null) continue;
            destino.Add(LinhaInformacao(
                NomeForca(resumo.forca),
                "unidades " + resumo.unidades
                + " | missao " + resumo.unidadesEmMissao
                + " | dano " + resumo.unidadesDanificadas
                + " | pessoal " + resumo.pessoalAlocado + "/" + resumo.pessoalExigido));
        }
    }

    private string ObterEstadoCarta(IdentidadeUnidade identidade)
    {
        if (identidade == null) return "sem identidade";
        ControleUnidade controle = identidade.GetComponent<ControleUnidade>();
        if (controle == null) return "sem controlador";
        if (controle.BloqueioAdministrativoQuartelAtivo) return "inoperante: tripulacao";
        if (controle.PossuiOrdemMovimentoAtiva || controle.OrdemAtual == OrdemControleUnidade.Patrulhando) return "em atividade";
        SistemaDeDanos danos = controle.GetComponent<SistemaDeDanos>();
        if (danos != null && danos.vidaAtual + 0.01f < danos.vidaMaxima) return "danificada";
        return "disponivel";
    }

    private string NomeForca(QuartelForcaV2 forca)
    {
        switch (forca)
        {
            case QuartelForcaV2.Veiculos: return "VEICULOS";
            case QuartelForcaV2.Naval: return "NAVAL";
            case QuartelForcaV2.Aerea: return "AEREA";
            default: return "INFANTARIA";
        }
    }

    private int ContarSelecionadosJogador()
    {
        RegistroEntidadesJogo.FillControlesUnidade(controlesRegistrados);
        int total = 0;
        for (int i = 0; i < controlesRegistrados.Count; i++)
        {
            ControleUnidade controle = controlesRegistrados[i];
            IdentidadeUnidade identidade = controle != null ? controle.GetComponent<IdentidadeUnidade>() : null;
            if (controle != null && controle.selecionado && identidade != null && identidade.teamID == quartel.teamID) total++;
        }
        return total;
    }

    private DadosPaisGoverno ObterPaisJogador()
    {
        return SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(quartel != null ? quartel.teamID : SistemaGovernoMundial.Instancia.teamJogador)
            : null;
    }
}
