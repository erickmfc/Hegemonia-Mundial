using System;
using System.Collections.Generic;
using UnityEngine;

public enum IdiomaJogo
{
    PtBr,
    EnUs,
    ZhHans
}

[DefaultExecutionOrder(-10050)]
public sealed class LocalizationManager : MonoBehaviour
{
    private const string PlayerPrefsKey = "hegemonia.idioma";
    private static LocalizationManager instancia;
    private readonly Dictionary<string, string[]> textos = new Dictionary<string, string[]>(StringComparer.Ordinal);

    public static LocalizationManager Instancia
    {
        get
        {
            GarantirInstancia();
            return instancia;
        }
    }

    public static IdiomaJogo IdiomaAtual => Instancia.idiomaAtual;
    public static event Action IdiomaAlterado;

    [SerializeField] private IdiomaJogo idiomaAtual = IdiomaJogo.PtBr;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void GarantirInstancia()
    {
        if (instancia != null)
        {
            return;
        }

        LocalizationManager existente = FindFirstObjectByType<LocalizationManager>();
        if (existente != null)
        {
            instancia = existente;
            instancia.Inicializar();
            return;
        }

        GameObject obj = new GameObject("LocalizationManager");
        instancia = obj.AddComponent<LocalizationManager>();
        instancia.Inicializar();
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        Inicializar();
    }

    private void Inicializar()
    {
        DontDestroyOnLoad(gameObject);
        if (textos.Count == 0)
        {
            CarregarTextosPadrao();
        }

        AplicarCodigo(PlayerPrefs.GetString(PlayerPrefsKey, "pt-BR"), false);
    }

    public static string T(string chave, string fallback = null)
    {
        return Instancia.Traduzir(chave, fallback);
    }

    public string Traduzir(string chave, string fallback = null)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            return fallback ?? string.Empty;
        }

        if (textos.TryGetValue(chave, out string[] valores))
        {
            int idx = IndiceIdioma(idiomaAtual);
            if (idx >= 0 && idx < valores.Length && !string.IsNullOrEmpty(valores[idx]))
            {
                return valores[idx];
            }
        }

        return fallback ?? chave;
    }

    public string ObterCodigoIdioma()
    {
        switch (idiomaAtual)
        {
            case IdiomaJogo.EnUs: return "en-US";
            case IdiomaJogo.ZhHans: return "zh-Hans";
            default: return "pt-BR";
        }
    }

    public void AplicarCodigo(string codigo)
    {
        AplicarCodigo(codigo, true);
    }

    private void AplicarCodigo(string codigo, bool notificar)
    {
        IdiomaJogo novoIdioma = CodigoParaIdioma(codigo);
        if (idiomaAtual == novoIdioma && PlayerPrefs.GetString(PlayerPrefsKey, string.Empty) == ObterCodigoIdioma())
        {
            return;
        }

        idiomaAtual = novoIdioma;
        PlayerPrefs.SetString(PlayerPrefsKey, ObterCodigoIdioma());
        PlayerPrefs.Save();

        if (notificar)
        {
            IdiomaAlterado?.Invoke();
        }
    }

    public void ProximoIdioma()
    {
        switch (idiomaAtual)
        {
            case IdiomaJogo.PtBr:
                AplicarCodigo("en-US");
                break;
            case IdiomaJogo.EnUs:
                AplicarCodigo("zh-Hans");
                break;
            default:
                AplicarCodigo("pt-BR");
                break;
        }
    }

    public string NomeIdiomaAtual()
    {
        switch (idiomaAtual)
        {
            case IdiomaJogo.EnUs: return "English";
            case IdiomaJogo.ZhHans: return "简体中文";
            default: return "Português BR";
        }
    }

    private static IdiomaJogo CodigoParaIdioma(string codigo)
    {
        if (string.Equals(codigo, "en-US", StringComparison.OrdinalIgnoreCase) || string.Equals(codigo, "en", StringComparison.OrdinalIgnoreCase))
        {
            return IdiomaJogo.EnUs;
        }

        if (string.Equals(codigo, "zh-Hans", StringComparison.OrdinalIgnoreCase) || string.Equals(codigo, "zh", StringComparison.OrdinalIgnoreCase) || string.Equals(codigo, "cn", StringComparison.OrdinalIgnoreCase))
        {
            return IdiomaJogo.ZhHans;
        }

        return IdiomaJogo.PtBr;
    }

    private static int IndiceIdioma(IdiomaJogo idioma)
    {
        switch (idioma)
        {
            case IdiomaJogo.EnUs: return 1;
            case IdiomaJogo.ZhHans: return 2;
            default: return 0;
        }
    }

    private void Add(string chave, string ptBr, string enUs, string zhHans)
    {
        textos[chave] = new[] { ptBr, enUs, zhHans };
    }

    private void CarregarTextosPadrao()
    {
        Add("menu.main.subtitle", "Nova campanha e carregar jogo", "New campaign and load game", "新战役与载入游戏");
        Add("menu.main.ready", "Campanha pronta para iniciar.", "Campaign ready to start.", "战役已准备开始。");
        Add("menu.main.new", "Nova Campanha", "New Campaign", "新战役");
        Add("menu.main.tutorial", "Tutorial", "Tutorial", "教程");
        Add("menu.main.load", "Carregar Jogo", "Load Game", "载入游戏");
        Add("menu.main.exit", "Sair", "Exit", "退出");
        Add("menu.main.loading_new", "Iniciando campanha principal...", "Starting main campaign...", "正在开始主战役...");
        Add("menu.main.loading_tutorial", "Iniciando tutorial...", "Starting tutorial...", "正在开始教程...");
        Add("menu.main.no_save", "Nenhum save encontrado para carregar.", "No save found to load.", "没有可载入的存档。");
        Add("menu.main.loading_save", "Carregando campanha salva...", "Loading saved campaign...", "正在载入已保存战役...");
        Add("menu.main.language", "Idioma", "Language", "语言");
        Add("menu.main.difficulty", "Dificuldade", "Difficulty", "难度");
        Add("menu.main.difficulty_status", "Dificuldade: {0}", "Difficulty: {0}", "难度：{0}");

        Add("pause.header", "HEGEMONIA GLOBAL", "GLOBAL HEGEMONY", "全球霸权");
        Add("pause.title", "PAUSADO", "PAUSED", "已暂停");
        Add("pause.status", "Partida pausada.", "Game paused.", "游戏已暂停。");
        Add("pause.resume", "Retomar Jogo", "Resume Game", "继续游戏");
        Add("pause.settings", "Configuracoes", "Settings", "设置");
        Add("pause.load", "Carregar Jogo", "Load Game", "载入游戏");
        Add("pause.save", "Salvar Jogo", "Save Game", "保存游戏");
        Add("pause.restart", "Reiniciar Partida", "Restart Match", "重新开始");
        Add("pause.exit_menu", "Sair para Menu Principal", "Exit to Main Menu", "返回主菜单");
        Add("pause.saved", "Jogo salvo com sucesso.", "Game saved successfully.", "游戏保存成功。");
        Add("pause.no_save", "Nenhum save encontrado para carregar.", "No save found to load.", "没有可载入的存档。");
        Add("pause.settings_language", "Idioma: {0}", "Language: {0}", "语言：{0}");
        Add("pause.settings_difficulty", "Dificuldade: {0}", "Difficulty: {0}", "难度：{0}");
        Add("pause.footer", "ESC retoma a partida.", "ESC resumes the match.", "按 ESC 继续游戏。");

        Add("difficulty.easy", "Facil", "Easy", "简单");
        Add("difficulty.normal", "Normal", "Normal", "普通");
        Add("difficulty.hard", "Dificil", "Hard", "困难");
        Add("difficulty.imperial", "Imperial", "Imperial", "帝国");

        Add("build.invalid_item", "Item de construcao invalido.", "Invalid construction item.", "无效建造项目。");
        Add("build.no_manager", "Gerente de jogo nao encontrado. Nao foi possivel iniciar a construcao.", "Game manager not found. Could not start construction.", "找不到游戏管理器，无法开始建造。");
        Add("build.missing_prefab", "Prefab faltando para {0}.", "Missing prefab for {0}.", "{0} 缺少预制体。");
        Add("build.no_money", "Fundos insuficientes para comprar {0}.", "Insufficient funds to buy {0}.", "资金不足，无法购买 {0}。");
        Add("build.need_airport", "Bloqueado: voce precisa construir um AEROPORTO ou HELIPORTO primeiro para comprar aeronaves.", "Blocked: build an AIRPORT or HELIPAD first to buy aircraft.", "已阻止：先建造机场或直升机场才能购买飞机。");
        Add("build.no_airport", "Erro: nenhum aeroporto valido encontrado para entregar esta aeronave.", "Error: no valid airport found to deliver this aircraft.", "错误：没有可交付该飞机的有效机场。");
        Add("build.need_shipyard_big", "Bloqueado: construa um ESTALEIRO costeiro valido para produzir esse navio grande.", "Blocked: build a valid coastal SHIPYARD to produce this large ship.", "已阻止：建造有效的海岸造船厂以生产大型舰船。");
        Add("build.need_shipyard", "Bloqueado: construa um ESTALEIRO ou PIER costeiro valido para produzir navios.", "Blocked: build a valid coastal SHIPYARD or PIER to produce ships.", "已阻止：建造有效的海岸造船厂或码头以生产舰船。");
        Add("build.naval_fail", "Falha ao produzir '{0}' em estruturas navais validas.", "Failed to produce '{0}' in valid naval structures.", "无法在有效海军建筑中生产“{0}”。");
        Add("build.naval_none", "Nao foi possivel produzir {0}.", "Could not produce {0}.", "无法生产 {0}。");
        Add("build.no_constructor", "Construtor nao encontrado na cena. Impossivel posicionar a estrutura.", "Constructor not found in scene. Cannot place structure.", "场景中找不到建造器，无法放置建筑。");
    }
}
