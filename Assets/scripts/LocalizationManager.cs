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
        Add("difficulty.normal", "Medio", "Medium", "普通");
        Add("difficulty.hard", "Dificil", "Hard", "困难");
        Add("difficulty.imperial", "Imperial", "Imperial", "帝国");

        Add("build.invalid_item", "Item de construcao invalido.", "Invalid construction item.", "无效建造项目。");
        Add("build.no_manager", "Gerente de jogo nao encontrado. Nao foi possivel iniciar a construcao.", "Game manager not found. Could not start construction.", "找不到游戏管理器，无法开始建造。");
        Add("build.missing_prefab", "Prefab faltando para {0}.", "Missing prefab for {0}.", "{0} 缺少预制体。");
        Add("build.no_money", "Fundos insuficientes para comprar {0}.", "Insufficient funds to buy {0}.", "资金不足，无法购买 {0}。");
        Add("economy.no_money_action", "SEM DINHEIRO\nRecursos insuficientes para esta ação.", "NO MONEY\nInsufficient funds for this action.", "没有资金\n资金不足，无法执行此操作。");
        Add("economy.no_energy_action", "SEM ENERGIA\nEnergia insuficiente para esta ação.", "NO ENERGY\nInsufficient energy for this action.", "没有能源\n能源不足，无法执行此操作。");
        Add("build.need_airport", "Bloqueado: voce precisa construir um AEROPORTO ou HELIPORTO primeiro para comprar aeronaves.", "Blocked: build an AIRPORT or HELIPAD first to buy aircraft.", "已阻止：先建造机场或直升机场才能购买飞机。");
        Add("build.no_airport", "Erro: nenhum aeroporto valido encontrado para entregar esta aeronave.", "Error: no valid airport found to deliver this aircraft.", "错误：没有可交付该飞机的有效机场。");
        Add("build.need_shipyard_big", "Bloqueado: construa um ESTALEIRO costeiro valido para produzir esse navio grande.", "Blocked: build a valid coastal SHIPYARD to produce this large ship.", "已阻止：建造有效的海岸造船厂以生产大型舰船。");
        Add("build.need_shipyard", "Bloqueado: construa um ESTALEIRO ou PIER costeiro valido para produzir navios.", "Blocked: build a valid coastal SHIPYARD or PIER to produce ships.", "已阻止：建造有效的海岸造船厂或码头以生产舰船。");
        Add("build.naval_fail", "Falha ao produzir '{0}' em estruturas navais validas.", "Failed to produce '{0}' in valid naval structures.", "无法在有效海军建筑中生产“{0}”。");
        Add("build.naval_none", "Nao foi possivel produzir {0}.", "Could not produce {0}.", "无法生产 {0}。");
        Add("build.no_constructor", "Construtor nao encontrado na cena. Impossivel posicionar a estrutura.", "Constructor not found in scene. Cannot place structure.", "场景中找不到建造器，无法放置建筑。");

        Add("hud.identity.title", "COMANDO TÁTICO", "TACTICAL COMMAND", "战术指挥");
        Add("hud.identity.waiting", "UNIDADE / AGUARDANDO", "UNIT / STANDBY", "单位 / 待命");
        Add("hud.identity.none", "NENHUMA UNIDADE", "NO UNIT SELECTED", "未选择单位");
        Add("hud.identity.select", "SELECIONE UMA UNIDADE PARA VER O ESTADO", "SELECT A UNIT TO VIEW STATUS", "选择单位以查看状态");
        Add("hud.status.select", "SELECIONE UMA UNIDADE", "SELECT A UNIT", "选择单位");
        Add("hud.status.no_data", "SEM DADOS", "NO UNIT DATA", "无单位数据");
        Add("hud.status.no_telemetry", "SEM TELEMETRIA", "NO TELEMETRY", "无遥测数据");
        Add("hud.card.speed", "VELOCIDADE / RUMO", "SPEED / HEADING", "速度 / 航向");
        Add("hud.speed.short", "VEL", "SPD", "速度");
        Add("hud.speed.up", "AUMENTAR +10%", "INCREASE +10%", "速度增加 +10%");
        Add("hud.speed.down", "REDUZIR −10%", "REDUCE −10%", "速度降低 −10%");
        Add("hud.speed.order", "GRUPO {0}", "GROUP {0}", "编队 {0}");
        Add("hud.speed.mixed", "MISTO", "MIXED", "混合");
        Add("hud.speed.kmh", "km/h", "km/h", "公里/小时");
        Add("hud.speed.up.tooltip", "Aumenta em 10% a velocidade das unidades móveis selecionadas, até 200%.", "Increase selected mobile units' speed by 10%, up to 200%.", "将所选可移动单位的速度提高 10%，上限为 200%。");
        Add("hud.speed.down.tooltip", "Reduz em 10% a velocidade das unidades móveis selecionadas, até 50%.", "Reduce selected mobile units' speed by 10%, down to 50%.", "将所选可移动单位的速度降低 10%，下限为 50%。");
        Add("hud.speed.feedback", "VELOCIDADE {0}10% · {1}/{2} UNID. · {3} NO LIMITE · {4} SEM MOTOR", "SPEED {0}10% · {1}/{2} UNITS · {3} AT LIMIT · {4} IMMOBILE", "速度 {0}10% · {1}/{2} 个单位 · {3} 个已达上限 · {4} 个不可移动");
        Add("hud.speed.limit", "LIMITE DE VELOCIDADE ATINGIDO PARA A SELEÇÃO.", "THE SELECTION HAS REACHED ITS SPEED LIMIT.", "所选单位已达到速度限制。");
        Add("hud.speed.unavailable", "NENHUMA UNIDADE SELECIONADA POSSUI MOVIMENTO AJUSTÁVEL.", "NONE OF THE SELECTED UNITS HAS ADJUSTABLE MOVEMENT.", "所选单位均没有可调节的移动速度。");
        Add("hud.heading.short", "PROA", "CRS", "航向");
        Add("hud.heading.label", "RUMO", "HEADING", "航向");
        Add("hud.card.condition", "CONDIÇÃO DA UNIDADE", "UNIT CONDITION", "单位状态");
        Add("hud.condition.status", "ESTADO", "STATUS", "状态");
        Add("hud.condition.depth", "PROFUNDIDADE", "DEPTH", "深度");
        Add("hud.condition.altitude", "ALTITUDE", "ALTITUDE", "高度");
        Add("hud.condition.keel", "CALADO", "KEEL / DRAFT", "吃水");
        Add("hud.condition.terrain", "TERRENO / ORDEM", "TERRAIN / ORDER", "地形 / 命令");
        Add("hud.condition.surface", "SUPERFÍCIE", "SURFACE", "水面");
        Add("hud.condition.mode", "MODO", "MODE", "模式");
        Add("hud.condition.position", "POS {0}, {1}", "POS {0}, {1}", "位置 {0}, {1}");
        Add("hud.detail.submarine", "PROF. {0:F0} m · RUMO {1:F0}°", "DEPTH {0:F0} m · HEADING {1:F0}°", "深度 {0:F0} 米 · 航向 {1:F0}°");
        Add("hud.detail.naval", "RUMO {0:F0}° · VEL {1:F0} kt", "HEADING {0:F0}° · SPD {1:F0} kt", "航向 {0:F0}° · 速度 {1:F0} 节");
        Add("hud.detail.air", "ALT {0:F0} m · VEL {1:F0} kt · RUMO {2:F0}°", "ALT {0:F0} m · SPD {1:F0} kt · HDG {2:F0}°", "高度 {0:F0} 米 · 速度 {1:F0} 节 · 航向 {2:F0}°");
        Add("hud.detail.air_no_speed", "ALT {0:F0} m · RUMO {1:F0}°", "ALT {0:F0} m · HDG {1:F0}°", "高度 {0:F0} 米 · 航向 {1:F0}°");
        Add("hud.detail.position", "POS {0:F0}, {1:F0} · RUMO {2:F0}°", "POS {0:F0}, {1:F0} · HEADING {2:F0}°", "位置 {0:F0}, {1:F0} · 航向 {2:F0}°");
        Add("hud.card.formation", "FORMAÇÃO", "FORMATION", "编队");
        Add("hud.formation.selection", "SELEÇÃO TÁTICA", "TACTICAL SELECTION", "战术选择");
        Add("hud.formation.none", "SEM FORMAÇÃO", "NO FORMATION", "无编队");
        Add("hud.formation.select", "SELECIONE UNIDADES", "SELECT UNITS", "选择单位");
        Add("hud.formation.edit", "F3 EDITAR FORMAÇÃO", "F3 EDIT FORMATION", "F3 编辑编队");
        Add("hud.formation.group", "GRUPO TÁTICO ({0})", "TACTICAL GROUP ({0})", "战术小组（{0}）");
        Add("hud.formation.unit_leader", "UNIDADE / LÍDER", "UNIT / LEADER", "单位 / 指挥官");
        Add("hud.card.roe", "POSTURA / REGRAS DE FOGO", "POSTURE / RULES OF ENGAGEMENT", "姿态 / 交战规则");
        Add("hud.roe.hold", "CESSAR FOGO", "HOLD FIRE", "停止开火");
        Add("hud.roe.defensive", "DEFENSIVA", "DEFENSIVE", "防御");
        Add("hud.roe.tight", "FOGO RESTRITO", "WEAPONS TIGHT", "限制开火");
        Add("hud.roe.free", "FOGO LIVRE", "WEAPONS FREE", "自由开火");
        Add("hud.roe.hold.tooltip", "Desativa o armamento da unidade.", "Disables the unit's weapons.", "停用该单位的武器。");
        Add("hud.roe.defensive.tooltip", "Mantém a unidade sob comando manual; ela só dispara após uma ordem do jogador.", "Keeps the unit under manual control; it fires only after a player order.", "保持手动控制；只有收到玩家命令后才会开火。");
        Add("hud.roe.tight.tooltip", "Engaja automaticamente apenas os alvos autorizados.", "Automatically engages authorized targets only.", "仅自动攻击已授权目标。");
        Add("hud.roe.free.tooltip", "Engaja automaticamente qualquer alvo válido detectado.", "Automatically engages any valid detected target.", "自动攻击检测到的任何有效目标。");
        Add("hud.card.sensors", "SENSORES / EMCON", "SENSORS / EMCON", "传感器 / 电磁管控");
        Add("hud.sensor.radar", "RADAR", "RADAR", "雷达");
        Add("hud.sensor.sonar", "SONAR", "SONAR", "声呐");
        Add("hud.sensor.passive", "PASSIVO", "PASSIVE", "被动");
        Add("hud.sensor.esm", "ESM", "ESM", "电子支援");
        Add("hud.sensor.datalink", "LINK DE DADOS", "DATALINK", "数据链");
        Add("hud.sensor.row", "{0}  {1}", "{0}  {1}", "{0}  {1}");
        Add("hud.state.on", "LIGADO", "ON", "开启");
        Add("hud.state.off", "DESLIGADO", "OFF", "关闭");
        Add("hud.state.mixed", "MISTO", "MIXED", "混合");
        Add("hud.state.na", "N/D", "N/A", "不可用");
        Add("hud.card.weapons", "ARMAMENTO", "WEAPONS", "武器");
        Add("hud.weapon.naval", "MÍSSEIS NAVAIS", "NAVAL MISSILES", "舰载导弹");
        Add("hud.weapon.torpedoes", "TORPEDOS", "TORPEDOES", "鱼雷");
        Add("hud.weapon.air_to_air", "AR-AR", "AIR-TO-AIR", "空对空");
        Add("hud.weapon.air_missiles", "MÍSSEIS AÉREOS", "AIR MISSILES", "空射导弹");
        Add("hud.weapon.gun_ammo", "MUNIÇÃO DE CANHÃO", "GUN AMMO", "火炮弹药");
        Add("hud.weapon.launchers", "LANÇADORES", "LAUNCHERS", "发射器");
        Add("hud.card.countermeasures", "CONTRAMEDIDAS", "COUNTERMEASURES", "对抗措施");
        Add("hud.counter.chaff", "CHAFF", "CHAFF", "箔条");
        Add("hud.counter.noisemaker", "ISCA ACÚSTICA", "NOISEMAKER", "声学诱饵");
        Add("hud.counter.not_detected", "SISTEMAS NÃO DETECTADOS", "NO SYSTEMS DETECTED", "未检测到系统");
        Add("hud.card.waypoints", "PATRULHA / PONTOS DE ROTA", "PATROL / WAYPOINTS", "巡逻 / 航路点");
        Add("hud.route.undefined", "ROTA NÃO DEFINIDA", "ROUTE NOT SET", "未设置航线");
        Add("hud.route.no_order", "SEM DESTINO ORDENADO", "NO DESTINATION ORDERED", "未指定目的地");
        Add("hud.route.editing", "ROTA EM EDIÇÃO · {0} PONTOS", "ROUTE EDITING · {0} POINTS", "正在编辑航线 · {0} 个点");
        Add("hud.route.distance", "{0} · {1:F0} U", "{0} · {1:F0} U", "{0} · {1:F0} 单位");
        Add("hud.card.automation", "IA / AUTOMAÇÃO", "AI / AUTOMATION", "AI / 自动化");
        Add("hud.ai.manual", "MANUAL", "MANUAL", "手动");
        Add("hud.ai.assist", "ASSISTÊNCIA", "ASSIST", "辅助");
        Add("hud.ai.auto", "AUTOMÁTICO", "AUTO", "自动");
        Add("hud.ai.manual.tooltip", "Armamento aguarda uma ordem direta do jogador.", "Weapons wait for a direct player order.", "武器等待玩家直接命令。");
        Add("hud.ai.assist.tooltip", "Engajamento automático limitado aos alvos autorizados.", "Automatic engagement is limited to authorized targets.", "自动交战仅限已授权目标。");
        Add("hud.ai.auto.tooltip", "Engajamento automático de qualquer alvo válido detectado.", "Automatically engages any valid detected target.", "自动攻击检测到的任何有效目标。");
        Add("hud.ai.mode", "MODO {0}", "MODE {0}", "模式 {0}");
        Add("hud.ai.na", "MODO N/D", "MODE N/A", "模式不可用");
        Add("hud.ai.mixed", "MODO MISTO", "MIXED MODE", "混合模式");
        Add("hud.status.group", "GRUPO · {0} UNIDADES", "GROUP · {0} UNITS", "小组 · {0} 个单位");
        Add("hud.status.combat", "COMBATE ATIVO", "IN COMBAT", "正在交战");
        Add("hud.status.formation", "EM FORMAÇÃO", "IN FORMATION", "编队中");
        Add("hud.profile.naval", "NAVAL", "NAVAL", "海军");
        Add("hud.profile.submarine", "SUBMARINO", "SUBMARINE", "潜艇");
        Add("hud.profile.air", "AÉREA", "AIR", "空军");
        Add("hud.profile.ground", "TERRESTRE", "GROUND", "陆军");
        Add("hud.profile.structure", "ESTRUTURA", "STRUCTURE", "建筑");
        Add("hud.profile.vehicle", "VEÍCULO", "VEHICLE", "车辆");
        Add("hud.profile.infantry", "INFANTARIA", "INFANTRY", "步兵");
        Add("hud.profile.unit", "UNIDADE", "UNIT", "单位");
        Add("hud.group.title", "GRUPO DE COMANDO · {0} UNIDADES", "COMMAND GROUP · {0} UNITS", "指挥小组 · {0} 个单位");
        Add("hud.unit.title", "UNIDADE · {0}", "UNIT · {0}", "单位 · {0}");
        Add("hud.group.name", "GRUPO TÁTICO · {0}", "TACTICAL GROUP · {0}", "战术小组 · {0}");
        Add("hud.group.mix", "NAVAL {0} · SUB {1} · AÉREA {2} · TERRA {3}", "NAVAL {0} · SUB {1} · AIR {2} · GROUND {3}", "海军 {0} · 潜艇 {1} · 空军 {2} · 陆军 {3}");
        Add("hud.roe.mixed", "MODO MISTO", "MIXED MODE", "混合模式");
        Add("hud.integrity.status", "INTEGRIDADE  {0}", "INTEGRITY  {0}", "完整性  {0}");
        Add("hud.readiness.status", "PRONTIDÃO  {0}", "READINESS  {0}", "战备状态  {0}");
        Add("hud.speed.average", "MÉDIA", "AVG", "平均");
        Add("hud.unit.knots", "nós", "kt", "节");
        Add("hud.unit.metres", "m", "m", "米");
        Add("hud.unit.distance", "u", "u", "单位");
        Add("hud.quick.rtb", "BASE ↗", "RTB ↗", "返航 ↗");
        Add("hud.quick.patrol.short", "F4  PATRULHA", "F4  PATROL", "F4  巡逻");
        Add("hud.heading.north", "N", "N", "北");
        Add("hud.heading.south", "S", "S", "南");
        Add("hud.state.unavailable", "INDISPONÍVEL", "UNAVAILABLE", "不可用");
        Add("hud.mode.automatic", "AUTOMÁTICO", "AUTOMATIC", "自动");
        Add("hud.mode.passive", "PASSIVO", "PASSIVE", "被动");
        Add("hud.mode.manual", "MANUAL", "MANUAL", "手动");
        Add("hud.mode.patrolling", "PATRULHANDO", "PATROLLING", "巡逻中");
        Add("hud.mode.moving", "EM MOVIMENTO", "MOVING", "移动中");
        Add("hud.mode.following", "SEGUINDO", "FOLLOWING", "跟随中");
        Add("hud.mode.attacking", "ATACANDO", "ATTACKING", "攻击中");
        Add("hud.mode.waiting", "AGUARDANDO", "STANDING BY", "待命");
        Add("hud.mode.returning", "RETORNANDO", "RETURNING", "返航中");
        Add("hud.tooltip.flag", "Bandeira do país da unidade", "Unit country flag", "单位所属国家旗帜");
        Add("hud.tooltip.country_unknown", "País da unidade desconhecido", "Unit country unknown", "未知单位所属国家");
        Add("hud.tooltip.rtb", "Retornar aeronave à base.", "Return aircraft to base.", "让飞机返回基地。");
        Add("hud.tooltip.formation_slot", "Membro {0} — clique para torná-lo líder.", "Member {0} — click to make it leader.", "成员 {0} — 点击将其设为队长。");
        Add("hud.tooltip.formation_leader", "Líder da formação.", "Formation leader.", "编队队长。");
        Add("hud.tooltip.formation_move", "{0} · {1:F0} u do líder · clique para liderar ou arraste para reposicionar", "{0} · {1:F0} u from leader · click to lead or drag to reposition", "{0} · 距队长 {1:F0} 单位 · 点击设为队长或拖动调整位置");
        Add("hud.tooltip.empty_slot", "Sem unidade neste slot.", "No unit in this slot.", "此位置没有单位。");
        Add("hud.quick.follow", "⌘  SEGUIR   F1", "⌘  FOLLOW   F1", "⌘  跟随   F1");
        Add("hud.quick.escort", "♟  ESCOLTAR   F2", "♟  ESCORT   F2", "♟  护航   F2");
        Add("hud.quick.formation", "✥  EDITAR FORMAÇÃO   F3", "✥  FORMATION EDIT   F3", "✥  编辑编队   F3");
        Add("hud.quick.patrol", "⌖  PATRULHA   F4", "⌖  PATROL   F4", "⌖  巡逻   F4");
        Add("hud.quick.intercept", "◎  INTERCEPTAR   F5", "◎  INTERCEPT   F5", "◎  拦截   F5");
        Add("hud.quick.damage", "⚒  CONTROLE DE DANOS   F6", "⚒  DAMAGE CONTROL   F6", "⚒  损管   F6");
        Add("hud.quick.camera", "▣  CÂMERA   F7", "▣  CAMERA   F7", "▣  镜头   F7");
        Add("hud.quick.reopen", "☰  REABRIR HUD TÁTICO", "☰  RESTORE TACTICAL HUD", "☰  恢复战术界面");
        Add("hud.quick.follow.tooltip", "Escolher unidade para acompanhar.", "Choose a unit to follow.", "选择要跟随的单位。");
        Add("hud.quick.escort.tooltip", "Selecione duas ou mais unidades: as outras seguem a unidade em foco.", "Select at least two units; the others follow the focused unit.", "至少选择两个单位；其余单位会跟随当前焦点单位。");
        Add("hud.quick.formation.tooltip", "Ativar ou concluir a edição dos slots da formação.", "Start or finish editing formation slots.", "开始或结束编队位置编辑。");
        Add("hud.quick.patrol.tooltip", "Criar rota de patrulha.", "Create a patrol route.", "创建巡逻航线。");
        Add("hud.quick.intercept.tooltip", "Selecionar alvo para interceptar.", "Select a target to intercept.", "选择要拦截的目标。");
        Add("hud.quick.damage.tooltip", "Exibir o diagnóstico de integridade da unidade em foco.", "Show integrity diagnostics for the focused unit.", "显示当前单位的完整性诊断。");
        Add("hud.quick.camera.tooltip", "Centralizar câmera na seleção.", "Center the camera on the selection.", "将镜头移至当前选择。");
        Add("hud.card.expand.tooltip", "Clique para ampliar este painel e ler os detalhes.", "Click to expand this panel and read its details.", "点击展开面板以阅读详细信息。");
        Add("hud.card.close.tooltip", "Fechar o painel ampliado.", "Close the expanded panel.", "关闭展开面板。");
        Add("hud.cardinfo.identity", "Veja a unidade selecionada, o país, a integridade e a prontidão.", "View the selected unit, country, integrity, and readiness.", "查看所选单位、国家、完整性和战备状态。");
        Add("hud.cardinfo.speed", "Acompanhe a velocidade e o rumo. Use −/+10% para ajustar todas as unidades móveis selecionadas; os limites são 50% e 200%.", "Track speed and heading. Use −/+10% to adjust all selected mobile units; the limits are 50% and 200%.", "查看速度和航向。使用 −/+10% 调整所有已选可移动单位；范围为 50% 至 200%。");
        Add("hud.cardinfo.condition", "Confira o estado atual e retorne à base quando essa opção estiver disponível.", "Check the current condition and return to base when available.", "查看当前状态，并在可用时返回基地。");
        Add("hud.cardinfo.formation", "Escolha o líder e reorganize as unidades. F3 ativa a edição por arraste.", "Choose a leader and rearrange units. F3 enables drag editing.", "选择队长并重新编组。按 F3 可拖动调整。");
        Add("hud.cardinfo.roe", "Defina quando as armas podem disparar: passivo, sob ordem, contra alvos autorizados ou livre.", "Choose when weapons may fire: passive, by order, at authorized targets, or freely.", "设置武器开火规则：停火、按命令、攻击授权目标或自由开火。");
        Add("hud.cardinfo.sensors", "Alterne o radar. Radar ligado consome energia e pode ser detectado; outros sensores aparecem quando a unidade os possui.", "Toggle radar. It uses energy and may be detected while on; other sensors appear when installed.", "切换雷达。开启时会消耗能源且可能被发现；其他传感器仅在单位配备时显示。");
        Add("hud.cardinfo.weapons", "Consulte os tipos de arma e a munição disponível na unidade.", "Check the unit's weapon types and available ammunition.", "查看单位的武器类型和可用弹药。");
        Add("hud.cardinfo.countermeasures", "Confira chaff e iscas acústicas quando esses sistemas estiverem instalados.", "Check chaff and acoustic decoys when those systems are installed.", "查看箔条和声学诱饵状态（如已安装）。");
        Add("hud.cardinfo.waypoints", "Veja a ordem de rota e use Patrulha para marcar pontos no mapa.", "View route orders and use Patrol to mark points on the map.", "查看航线命令，并使用巡逻在地图上标记点位。");
        Add("hud.cardinfo.automation", "Manual aguarda ordens; Assistência engaja apenas alvos autorizados (em navios, pressione O para escolher contatos); Automático engaja qualquer alvo válido detectado.", "Manual waits for orders; Assist engages authorized targets only (on ships, press O to choose contacts); Auto engages any valid detected target.", "手动等待命令；辅助只攻击授权目标（舰船按 O 选择目标）；自动攻击检测到的任何有效目标。");
        Add("hud.feedback.escort.need_group", "Selecione um líder e pelo menos uma unidade para escoltá-lo.", "Select a leader and at least one unit to escort it.", "选择一名队长和至少一个护航单位。");
        Add("hud.feedback.escort.issued", "ESCOLTA · {0} UNIDADES SEGUINDO {1}", "ESCORT · {0} UNITS FOLLOWING {1}", "护航 · {0} 个单位正在跟随 {1}");
        Add("hud.feedback.camera.missing", "CÂMERA DE JOGO INDISPONÍVEL.", "GAME CAMERA UNAVAILABLE.", "游戏镜头不可用。");
        Add("hud.feedback.camera.centered", "CÂMERA CENTRALIZADA NA SELEÇÃO.", "CAMERA CENTERED ON THE SELECTION.", "镜头已移至所选单位。");
        Add("hud.feedback.dronecam.missing", "CÂMERA DE ACOMPANHAMENTO INDISPONÍVEL NESTA CENA.", "FOLLOW CAMERA IS UNAVAILABLE IN THIS SCENE.", "此场景中没有跟随镜头。");
        Add("hud.feedback.dronecam.on", "CÂMERA DE ACOMPANHAMENTO ATIVADA.", "FOLLOW CAMERA ENABLED.", "跟随镜头已启用。");
        Add("hud.feedback.dronecam.off", "CÂMERA ORBITAL ATIVADA.", "ORBIT CAMERA ENABLED.", "轨道镜头已启用。");
        Add("hud.quick.center.tooltip", "Abrir centro tático.", "Open tactical command center.", "打开战术指挥中心。");
        Add("hud.quick.grid.tooltip", "Aplicar formação em grade às unidades selecionadas.", "Apply a grid formation to selected units.", "为所选单位应用网格编队。");
        Add("hud.quick.camera_options.tooltip", "Alternar câmera de acompanhamento.", "Toggle follow camera.", "切换跟随镜头。");
        Add("hud.quick.minimize.tooltip", "Recolher a barra tática.", "Collapse the tactical bar.", "收起战术栏。");
        Add("hud.quick.restore.tooltip", "Reabrir a barra tática.", "Restore the tactical bar.", "展开战术栏。");
        Add("hud.quick.close", "×", "×", "×");
        Add("hud.feedback.select_ally", "Selecione uma unidade aliada.", "Select an allied unit.", "请选择一个友方单位。");
        Add("hud.feedback.center_unavailable", "O centro tático não pode abrir agora.", "The tactical command center cannot open right now.", "战术指挥中心现在无法打开。");
        Add("hud.feedback.need_two_grid", "Selecione pelo menos duas unidades para formar uma grade.", "Select at least two units to form a grid.", "至少选择两个单位以组成网格编队。");
        Add("hud.feedback.mode", "MODO {0} · {1}/{2} UNIDADES", "MODE {0} · {1}/{2} UNITS", "模式 {0} · {1}/{2} 个单位");
        Add("hud.feedback.grid_applied", "FORMAÇÃO EM GRADE · {0} UNIDADES", "GRID FORMATION · {0} UNITS", "网格编队 · {0} 个单位");
        Add("hud.feedback.need_two_formation", "Selecione pelo menos duas unidades para editar a formação.", "Select at least two units to edit the formation.", "至少选择两个单位以编辑编队。");
        Add("hud.feedback.formation_drag", "ARRASTE OS SLOTS · F3 CONCLUIR", "DRAG SLOTS · F3 FINISH", "拖动位置 · F3 完成");
        Add("hud.feedback.formation_active", "EDIÇÃO DE FORMAÇÃO ATIVA · ARRASTE UNIDADES ENTRE SLOTS", "FORMATION EDITING ACTIVE · DRAG UNITS BETWEEN SLOTS", "编队编辑已启用 · 在位置间拖动单位");
        Add("hud.feedback.formation_done", "EDIÇÃO DE FORMAÇÃO CONCLUÍDA", "FORMATION EDITING COMPLETE", "编队编辑完成");
        Add("hud.feedback.damage", "CONTROLE DE DANOS · {0:F0}/{1:F0} HP · {2:P0}", "DAMAGE CONTROL · {0:F0}/{1:F0} HP · {2:P0}", "损管 · {0:F0}/{1:F0} 生命值 · {2:P0}");
        Add("hud.feedback.damage_missing", "CONTROLE DE DANOS · DADOS DE INTEGRIDADE INDISPONÍVEIS", "DAMAGE CONTROL · INTEGRITY DATA UNAVAILABLE", "损管 · 完整性数据不可用");
    }
}
