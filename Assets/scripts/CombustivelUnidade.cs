using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public enum ClasseCombustivelUnidade
{
    Nenhuma,
    Terrestre,
    Naval,
    Aerea
}

public class CombustivelUnidade : MonoBehaviour
{
    [Header("Config")]
    public bool usaCombustivel = true;
    public ClasseCombustivelUnidade classe = ClasseCombustivelUnidade.Nenhuma;
    public float capacidade = -1f;
    public float combustivelAtual = -1f;
    public float consumoPorSegundoMovendo = -1f;
    public float velocidadeMinimaParaConsumo = 0.05f;
    [Range(0.01f, 0.95f)] public float limiteBaixoPercentual = 0.25f;

    [Header("Spawn")]
    public bool preencherAoIniciar = true;
    public bool pararAoEsvaziar = true;

    [Header("Indicador")]
    public bool mostrarIndicadorMundo = true;
    public Vector3 offsetIndicador = new Vector3(0f, 3.2f, 0f);
    public float intervaloIndicador = 0.25f;

    [Header("Debug")]
    public bool debugLogs = false;

    private Vector3 ultimaPosicao;
    private bool configurado;
    private bool paradaAplicada;
    private bool parandoPorFalta;
    private float proximaAtualizacaoIndicador;
    private string textoIndicadorCache = "";
    private ControleUnidade controleCache;
    private Camera cameraCache;
    private static readonly StringBuilder textoBuilder = new StringBuilder(96);

    public float Percentual => capacidade > 0f ? Mathf.Clamp01(combustivelAtual / capacidade) : 1f;
    public float CombustivelAtual => Mathf.Max(0f, combustivelAtual);
    public float Capacidade => Mathf.Max(0f, capacidade);
    public bool EstaVazio => usaCombustivel && Capacidade > 0f && combustivelAtual <= 0.01f;
    public bool EstaBaixo => usaCombustivel && Capacidade > 0f && Percentual <= limiteBaixoPercentual;
    public bool PodeOperar => !usaCombustivel || !EstaVazio;

    private void Awake()
    {
        ConfigurarSeNecessario(false);
        ultimaPosicao = transform.position;
    }

    private void Start()
    {
        ConfigurarSeNecessario(preencherAoIniciar);
        ultimaPosicao = transform.position;
    }

    private float timerUpdate = 0f;

    private void Update()
    {
        if (!usaCombustivel || Capacidade <= 0f)
        {
            return;
        }

        timerUpdate += Time.deltaTime;
        if (timerUpdate < 0.25f) return;

        float dt = timerUpdate;
        timerUpdate = 0f;

        Vector3 posicaoAtual = transform.position;
        Vector3 deslocamento = posicaoAtual - ultimaPosicao;
        deslocamento.y = 0f;
        float velocidade = deslocamento.magnitude / dt;
        ultimaPosicao = posicaoAtual;

        if (velocidade > velocidadeMinimaParaConsumo && !EstaVazio)
        {
            bool sendoTransportado = transform.parent != null;
            if (sendoTransportado && transform.parent.GetComponentInParent<IdentidadeUnidade>() == null)
            {
                 sendoTransportado = false;
            }

            if (!sendoTransportado)
            {
                Consumir(consumoPorSegundoMovendo * dt);
            }
        }

        if (EstaVazio && pararAoEsvaziar && !PodeIgnorarFalhaDeCombustivel())
        {
            PararPorFaltaDeCombustivel();
        }
    }

    private void OnGUI()
    {
        if (ConfiguracaoCenasJogo.EhCenaDeMenu(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (!mostrarIndicadorMundo || !usaCombustivel || Capacidade <= 0f)
        {
            return;
        }

        if (!PodeMostrarIndicadorAoJogador())
        {
            return;
        }

        bool selecionado = EstaSelecionado();
        if (!selecionado && !EstaBaixo)
        {
            return;
        }

        if (cameraCache == null)
        {
            cameraCache = Camera.main;
        }

        if (cameraCache == null)
        {
            return;
        }

        Vector3 tela = cameraCache.WorldToScreenPoint(transform.position + offsetIndicador);
        if (tela.z < 0f)
        {
            return;
        }

        if (Time.unscaledTime >= proximaAtualizacaoIndicador)
        {
            proximaAtualizacaoIndicador = Time.unscaledTime + Mathf.Max(0.05f, intervaloIndicador);
            textoIndicadorCache = ConstruirTextoIndicador(selecionado);
        }

        int numLinhas = textoIndicadorCache.Split('\n').Length;
        bool expandido = selecionado || numLinhas > 1;
        
        float largura = expandido ? 160f : 108f;
        float altura = 20f + ((numLinhas - 1) * 16f); // Adiciona 16px para cada linha extra
        if (expandido && numLinhas == 1) altura = 30f; // Caso seja selecionado mas ainda tenha 1 linha
        
        float posX = tela.x + 18f;
        float posY = Screen.height - tela.y - altura * 0.55f;

        Rect fundo = new Rect(posX, posY, largura, altura);
        Rect barra = new Rect(fundo.x + 5f, fundo.y + fundo.height - 5f, (largura - 10f) * Percentual, 3f);

        Color corAntiga = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(fundo, Texture2D.whiteTexture);
        GUI.color = CorDoNivel();
        GUI.DrawTexture(barra, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(fundo.x + 6f, fundo.y + 2f, largura - 12f, altura - 4f), textoIndicadorCache);
        GUI.color = corAntiga;
    }

    public void ConfigurarSeNecessario(bool preencher)
    {
        if (configurado && !preencher)
        {
            return;
        }

        ClasseCombustivelUnidade classeDetectada = classe != ClasseCombustivelUnidade.Nenhuma
            ? classe
            : DetectarClasse();

        if (classeDetectada == ClasseCombustivelUnidade.Nenhuma)
        {
            usaCombustivel = false;
            configurado = true;
            return;
        }

        classe = classeDetectada;
        usaCombustivel = true;

        float capacidadeIdeal = CapacidadePadrao(classe);
        if ((classe == ClasseCombustivelUnidade.Aerea || classe == ClasseCombustivelUnidade.Naval) && capacidadeIdeal > 0f)
        {
            bool capacidadeAntigaPadrao = classe == ClasseCombustivelUnidade.Naval && capacidade > 0f && capacidade <= 300f;
            bool precisaAtualizarCapacidade = capacidade <= 0f || capacidade < capacidadeIdeal || capacidadeAntigaPadrao;
            if (precisaAtualizarCapacidade)
            {
                float percentualAnterior = capacidade > 0f ? Mathf.Clamp01(combustivelAtual / capacidade) : 1f;
                capacidade = capacidadeIdeal;
                if (combustivelAtual >= 0f)
                {
                    combustivelAtual = capacidade * percentualAnterior;
                }
            }
        }

        if (capacidade <= 0f)
        {
            capacidade = capacidadeIdeal;
        }

        if (consumoPorSegundoMovendo <= 0f
            || (classe == ClasseCombustivelUnidade.Aerea && Mathf.Approximately(consumoPorSegundoMovendo, 1.60f))
            || (classe == ClasseCombustivelUnidade.Terrestre && Mathf.Approximately(consumoPorSegundoMovendo, 0.30f)))
        {
            consumoPorSegundoMovendo = ConsumoPadrao(classe);
        }

        if (combustivelAtual < 0f)
        {
            combustivelAtual = preencher ? capacidade : 0f;
            paradaAplicada = combustivelAtual <= 0.01f;
        }
        else if (preencher)
        {
            PreencherSemCusto();
        }
        else
        {
            combustivelAtual = Mathf.Clamp(combustivelAtual, 0f, capacidade);
        }

        configurado = true;
    }

    public float Abastecer(float quantidade)
    {
        if (!usaCombustivel || Capacidade <= 0f || quantidade <= 0f)
        {
            return 0f;
        }

        float antes = combustivelAtual;
        combustivelAtual = Mathf.Clamp(combustivelAtual + quantidade, 0f, capacidade);
        float abastecido = combustivelAtual - antes;

        if (combustivelAtual > 0.01f)
        {
            paradaAplicada = false;
        }

        return abastecido;
    }

    public bool Consumir(float delta)
    {
        if (!usaCombustivel || Capacidade <= 0f || delta <= 0f)
        {
            return false;
        }

        if (EstaVazio)
        {
            return false;
        }

        combustivelAtual = Mathf.Max(0f, combustivelAtual - delta);
        if (EstaVazio && pararAoEsvaziar)
        {
            PararPorFaltaDeCombustivel();
        }

        return true;
    }

    public void PreencherSemCusto()
    {
        combustivelAtual = Mathf.Max(0f, capacidade);
        paradaAplicada = false;
    }

    public float EstimarConsumoParaDistancia(float distanciaMetros, float velocidadeMetrosSegundo)
    {
        if (!usaCombustivel || consumoPorSegundoMovendo <= 0f || distanciaMetros <= 0.01f)
        {
            return 0f;
        }

        float velocidade = Mathf.Max(1f, velocidadeMetrosSegundo);
        float tempo = distanciaMetros / velocidade;
        return consumoPorSegundoMovendo * tempo;
    }

    public void PararPorFaltaDeCombustivel()
    {
        if (PodeIgnorarFalhaDeCombustivel())
        {
            return;
        }

        if (paradaAplicada || parandoPorFalta)
        {
            return;
        }

        paradaAplicada = true;
        parandoPorFalta = true;

        if (TentarFalhaAereaAntesDePararTudo())
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[Combustivel] {name} entrou em falha aerea por falta de combustivel.", this);
            }

            parandoPorFalta = false;
            return;
        }

        ControleUnidade controle = ObterControle();
        if (controle != null)
        {
            controle.EmitirOrdemParar();
        }

        NavMeshAgent agente = GetComponent<NavMeshAgent>();
        if (agente != null && agente.enabled && agente.isOnNavMesh)
        {
            agente.ResetPath();
            agente.isStopped = true;
            agente.velocity = Vector3.zero;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ControleNavioRealista navio = GetComponent<ControleNavioRealista>();
        if (navio != null) navio.PararPorFaltaDeCombustivel();

        ControleSubmarino submarino = GetComponent<ControleSubmarino>();
        if (submarino != null) submarino.PararPorFaltaDeCombustivel();

        HovercraftTransporte hovercraft = GetComponent<HovercraftTransporte>();
        if (hovercraft != null) hovercraft.PararPorFaltaDeCombustivel();

        ControleAviao aviao = GetComponent<ControleAviao>();
        if (aviao != null) aviao.PararPorFaltaDeCombustivel();

        ControleAviaoCaca caca = GetComponent<ControleAviaoCaca>();
        if (caca != null) caca.PararPorFaltaDeCombustivel();

        C700TransporteAereo c700 = GetComponent<C700TransporteAereo>();
        if (c700 != null) c700.PararPorFaltaDeCombustivel();

        Helicoptero helicoptero = GetComponent<Helicoptero>();
        if (helicoptero != null) helicoptero.PararPorFaltaDeCombustivel();

        if (debugLogs)
        {
            Debug.LogWarning($"[Combustivel] {name} parou por falta de combustivel.", this);
        }

        parandoPorFalta = false;
    }

    public string TextoStatusCurto()
    {
        if (!usaCombustivel || Capacidade <= 0f)
        {
            return "";
        }

        return $"FUEL {Mathf.RoundToInt(Percentual * 100f)}% {Mathf.RoundToInt(CombustivelAtual)}/{Mathf.RoundToInt(Capacidade)}";
    }

    public static CombustivelUnidade Garantir(GameObject alvo, bool preencherSemCusto = true)
    {
        if (alvo == null || !DeveUsarCombustivel(alvo))
        {
            return null;
        }

        CombustivelUnidade combustivel = alvo.GetComponent<CombustivelUnidade>();
        if (combustivel == null)
        {
            combustivel = alvo.AddComponent<CombustivelUnidade>();
        }

        if (!preencherSemCusto)
        {
            combustivel.preencherAoIniciar = false;
        }

        combustivel.ConfigurarSeNecessario(preencherSemCusto);
        return combustivel;
    }

    public static bool PodeOperarObjeto(GameObject alvo)
    {
        if (alvo == null)
        {
            return true;
        }

        CombustivelUnidade combustivel = alvo.GetComponent<CombustivelUnidade>();
        return combustivel == null || combustivel.PodeOperar;
    }

    public static string TextoCurto(Component componente)
    {
        if (componente == null)
        {
            return "";
        }

        CombustivelUnidade combustivel = componente.GetComponent<CombustivelUnidade>();
        return combustivel != null ? combustivel.TextoStatusCurto() : "";
    }

    private bool PodeIgnorarFalhaDeCombustivel()
    {
        ControleAviao aviao = GetComponent<ControleAviao>();
        return aviao != null && aviao.PodeIgnorarFaltaDeCombustivel();
    }

    public static bool DeveUsarCombustivel(GameObject alvo)
    {
        if (alvo == null)
        {
            return false;
        }

        IdentidadeUnidade identidade = alvo.GetComponent<IdentidadeUnidade>();
        if (identidade != null)
        {
            if (identidade.tipoUnidade == TipoUnidade.Estrutura)
            {
                return false;
            }

            return identidade.tipoUnidade == TipoUnidade.Veiculo
                || identidade.tipoUnidade == TipoUnidade.Naval
                || identidade.tipoUnidade == TipoUnidade.Aereo
                || TemComponenteMotorizado(alvo);
        }

        return TemComponenteMotorizado(alvo);
    }

    private static bool TemComponenteMotorizado(GameObject alvo)
    {
        return alvo.GetComponent<MovimentoRealTerrestre>() != null
            || alvo.GetComponent<CaminhaoTanqueAbastecimento>() != null
            || alvo.GetComponent<ControleNavioRealista>() != null
            || alvo.GetComponent<ControleSubmarino>() != null
            || alvo.GetComponent<HovercraftTransporte>() != null
            || alvo.GetComponent<ControleAviao>() != null
            || alvo.GetComponent<ControleAviaoCaca>() != null
            || alvo.GetComponent<C700TransporteAereo>() != null
            || alvo.GetComponent<Helicoptero>() != null;
    }

    private ClasseCombustivelUnidade DetectarClasse()
    {
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        if (identidade != null)
        {
            if (identidade.tipoUnidade == TipoUnidade.Estrutura)
            {
                return ClasseCombustivelUnidade.Nenhuma;
            }

            if (identidade.tipoUnidade == TipoUnidade.Aereo) return ClasseCombustivelUnidade.Aerea;
            if (identidade.tipoUnidade == TipoUnidade.Naval) return ClasseCombustivelUnidade.Naval;
            if (identidade.tipoUnidade == TipoUnidade.Veiculo) return ClasseCombustivelUnidade.Terrestre;
            if (identidade.tipoUnidade == TipoUnidade.Infantaria && !TemComponenteMotorizado(gameObject))
            {
                return ClasseCombustivelUnidade.Nenhuma;
            }
        }

        if (GetComponent<ControleAviao>() != null
            || GetComponent<ControleAviaoCaca>() != null
            || GetComponent<C700TransporteAereo>() != null
            || GetComponent<Helicoptero>() != null)
        {
            return ClasseCombustivelUnidade.Aerea;
        }

        if (GetComponent<ControleNavioRealista>() != null
            || GetComponent<ControleSubmarino>() != null
            || GetComponent<HovercraftTransporte>() != null
            || GetComponent<IdentidadeNaval>() != null)
        {
            return ClasseCombustivelUnidade.Naval;
        }

        if (GetComponent<MovimentoRealTerrestre>() != null)
        {
            return ClasseCombustivelUnidade.Terrestre;
        }

        return ClasseCombustivelUnidade.Nenhuma;
    }

    private float CapacidadePadrao(ClasseCombustivelUnidade classeAlvo)
    {
        switch (classeAlvo)
        {
            case ClasseCombustivelUnidade.Naval:
                if (NomeContem("Sovereign")) return 50000f;
                if (NomeContem("Liberty")) return 15000f;
                if (NomeContem("Wall")) return 5800f;
                if (NomeContem("F200")) return 4800f;
                if (NomeContem("Fortaleza")) return 3800f;
                if (NomeContem("Leviathan")) return 25800f;
                if (NomeContem("abastecimento")) return 16800f;
                if (NomeContem("Petroleiro")) return 9800f;
                if (NomeContem("Ironclad")) return 6800f;
                
                // Os demais de grande porte mantêm a capacidade padrão de grande porte (9000f)
                if (EhGrandePorte())
                {
                    return 9000f;
                }
                
                // Os demais que não sejam de grande porte dobram a quantidade (3600f)
                return 3600f;

            case ClasseCombustivelUnidade.Aerea:
                return 300f * MultiplicadorCapacidadeAerea();
            case ClasseCombustivelUnidade.Terrestre:
                return 240f;
            default:
                return 0f;
        }
    }

    private float ConsumoPadrao(ClasseCombustivelUnidade classeAlvo)
    {
        switch (classeAlvo)
        {
            case ClasseCombustivelUnidade.Naval:
                if (NomeContem("Sovereign")) return 0.6f;
                if (NomeContem("Liberty")) return 0.6f;
                if (NomeContem("Wall")) return 0.5f;
                if (NomeContem("F200")) return 0.3f;
                if (NomeContem("Fortaleza")) return 0.5f;
                if (NomeContem("Leviathan")) return 0.5f;
                if (NomeContem("abastecimento")) return 0.5f;
                if (NomeContem("Petroleiro")) return 0.5f;
                if (NomeContem("Ironclad")) return 0.2f;
                return 0.50f;

            case ClasseCombustivelUnidade.Aerea:
                if (GetComponent<C700TransporteAereo>() != null) return 1.95f;
                if (GetComponent<AviaoBombardeiro>() != null) return 1.75f;
                if (GetComponent<Helicoptero>() != null) return 1.20f;
                return 1.45f;
            case ClasseCombustivelUnidade.Terrestre:
                return EhTerrestreMotorizado() ? 0.42f : 0.30f;
            default:
                return 0f;
        }
    }

    private bool EhGrandePorte()
    {
        if (GetComponent<GerenciadorPortaAvioes>() != null || NomeContem("porta"))
        {
            return true;
        }

        if (GetComponent<NavioTransporteTropas>() != null || GetComponent<NavioLiberty>() != null || NomeContem("transporte") || NomeContem("liberty"))
        {
            return true;
        }

        return false;
    }

    private float MultiplicadorCapacidadeAerea()
    {
        if (GetComponent<C700TransporteAereo>() != null || NomeContem("transporte") || NomeContem("cargo"))
        {
            return 4f;
        }

        if (GetComponent<AviaoBombardeiro>() != null || NomeContem("bomb"))
        {
            return 3f;
        }

        if (GetComponent<ControleDroneHasaf>() != null || NomeContem("hasaf"))
        {
            // Drone Hasaf: triplo de combustível (base 300f * 6 = 1800f em vez do padrão 300f * 2 = 600f)
            return 6f;
        }

        return 2f;
    }

    private float MultiplicadorCapacidadeNaval()
    {
        if (GetComponent<GerenciadorPortaAvioes>() != null || NomeContem("porta"))
        {
            return 5f;
        }

        if (GetComponent<NavioTransporteTropas>() != null || GetComponent<NavioLiberty>() != null || NomeContem("transporte") || NomeContem("liberty"))
        {
            return 5f;
        }

        return 1f;
    }

    private bool EstaSelecionado()
    {
        ControleUnidade controle = ObterControle();
        if (controle != null && controle.selecionado)
        {
            return true;
        }

        Helicoptero helicoptero = GetComponent<Helicoptero>();
        return helicoptero != null && helicoptero.selecionado;
    }

    private ControleUnidade ObterControle()
    {
        if (controleCache == null)
        {
            controleCache = GetComponent<ControleUnidade>();
        }

        return controleCache;
    }

    private Color CorDoNivel()
    {
        float pct = Percentual;
        if (pct <= 0.12f) return new Color(1f, 0.15f, 0.08f, 0.95f);
        if (pct <= limiteBaixoPercentual) return new Color(1f, 0.78f, 0.15f, 0.95f);
        return new Color(0.2f, 0.9f, 0.45f, 0.95f);
    }

    private bool EhTerrestreMotorizado()
    {
        if (GetComponent<MovimentoRealTerrestre>() != null || GetComponent<CaminhaoTanqueAbastecimento>() != null)
        {
            return true;
        }

        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        return identidade != null && identidade.tipoUnidade == TipoUnidade.Veiculo;
    }

    private bool NomeContem(string termo)
    {
        return !string.IsNullOrEmpty(termo)
            && name.IndexOf(termo, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string ConstruirTextoIndicador(bool expandido)
    {
        textoBuilder.Length = 0;
        textoBuilder.Append(TextoStatusCurto());

        string linhaSecundaria = ObterLinhaSecundariaIndicador();
        if (!string.IsNullOrEmpty(linhaSecundaria) && expandido)
        {
            textoBuilder.Append('\n');
            textoBuilder.Append(linhaSecundaria);
        }

        return textoBuilder.ToString();
    }

    private string ObterLinhaSecundariaIndicador()
    {
        string modo = ObterModoOperacaoIndicador();

        LancadorNaval lancadorNaval = GetComponent<LancadorNaval>();
        if (lancadorNaval == null) lancadorNaval = GetComponentInChildren<LancadorNaval>();
        if (lancadorNaval != null)
        {
            string armas = $"MSL {lancadorNaval.municaoTotal}/{lancadorNaval.municaoMaxima} TOR {lancadorNaval.torpedosTotal}/{lancadorNaval.torpedosMaximos}";
            return string.IsNullOrEmpty(modo) ? armas : $"{modo} {armas}";
        }

        LancadorMisselCaca lancadorCaca = GetComponent<LancadorMisselCaca>();
        if (lancadorCaca != null)
        {
            return $"MSL {lancadorCaca.municaoAtual}/{lancadorCaca.municaoMaxima}";
        }

        CaminhaoTanqueAbastecimento caminhao = GetComponent<CaminhaoTanqueAbastecimento>();
        if (caminhao != null)
        {
            return $"CARGA {Mathf.RoundToInt(caminhao.CargaAtual)}/{Mathf.RoundToInt(caminhao.CapacidadeCarga)}";
        }

        return modo;
    }

    private bool PodeMostrarIndicadorAoJogador()
    {
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();
        if (identidade == null)
        {
            return true;
        }

        int timeJogador = 1;
        if (SistemaGovernoMundial.Instancia != null && SistemaGovernoMundial.Instancia.teamJogador > 0)
        {
            timeJogador = SistemaGovernoMundial.Instancia.teamJogador;
        }

        return identidade.teamID == 0 || identidade.teamID == timeJogador;
    }

    private string ObterModoOperacaoIndicador()
    {
        ControleSubmarino submarino = GetComponent<ControleSubmarino>();
        if (submarino == null) submarino = GetComponentInChildren<ControleSubmarino>();
        if (submarino != null)
        {
            return $"[{submarino.modoAtual}]";
        }

        LancadorNaval lancadorNaval = GetComponent<LancadorNaval>();
        if (lancadorNaval == null) lancadorNaval = GetComponentInChildren<LancadorNaval>();
        if (lancadorNaval != null)
        {
            return $"[{lancadorNaval.modoAtual}]";
        }

        ControleUnidade controle = ObterControle();
        bool passivo;
        string descricao;
        if (controle != null && controle.TryObterEstadoCombate(out passivo, out descricao) && !string.IsNullOrEmpty(descricao))
        {
            return $"[{descricao}]";
        }

        ControleNavioRealista navio = GetComponent<ControleNavioRealista>();
        if (navio == null) navio = GetComponentInChildren<ControleNavioRealista>();
        if (navio != null)
        {
            return navio.ModoCombateTorpedosAtivo() ? "[ATIVO]" : "[PASSIVO]";
        }

        return string.Empty;
    }

    private bool TentarFalhaAereaAntesDePararTudo()
    {
        bool temControleAereo = GetComponent<ControleAviao>() != null
            || GetComponent<ControleAviaoCaca>() != null
            || GetComponent<C700TransporteAereo>() != null
            || GetComponent<Helicoptero>() != null;

        if (!temControleAereo || transform.position.y <= 4f)
        {
            return false;
        }

        ControleAviao aviao = GetComponent<ControleAviao>();
        if (aviao != null) aviao.PararPorFaltaDeCombustivel();

        ControleAviaoCaca caca = GetComponent<ControleAviaoCaca>();
        if (caca != null) caca.PararPorFaltaDeCombustivel();

        C700TransporteAereo c700 = GetComponent<C700TransporteAereo>();
        if (c700 != null) c700.PararPorFaltaDeCombustivel();

        Helicoptero helicoptero = GetComponent<Helicoptero>();
        if (helicoptero != null) helicoptero.PararPorFaltaDeCombustivel();

        return true;
    }
}
