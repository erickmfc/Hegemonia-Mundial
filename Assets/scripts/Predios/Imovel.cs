using UnityEngine;
using Hegemonia.AI.BrainMaster;

/// <summary>
/// Sistema de Imóveis — Casas, Prédios e Apartamentos.
/// 
/// ╔═══════════════════════════════════════════════════════════════╗
/// ║  COMO FUNCIONA:                                              ║
/// ║  1. Cada imóvel tem uma CAPACIDADE de moradores              ║
/// ║  2. Moradores chegam gradualmente (não todos de uma vez)     ║
/// ║  3. Cada morador = +1 populacaoAtual no GerenciadorRecursos  ║
/// ║  4. Moradores geram renda (impostos) por segundo             ║
/// ║  5. Moradores podem ir embora se qualidade de vida cair      ║
/// ║  6. Ao destruir o imóvel, moradores são removidos            ║
/// ╚═══════════════════════════════════════════════════════════════╝
/// </summary>
public class Imovel : MonoBehaviour
{
    [Header("🏠 Configuração do Imóvel")]
    [Tooltip("Quantidade máxima de moradores que cabem neste imóvel")]
    public int capacidade = 10;

    [Header("🏘️ Conexão de Quarteirão (Grudar Imóveis)")]
    [Tooltip("Distância do centro até as laterais (usado para calcular a conexão)")]
    public float distanciaConexao = 8f;
    [Tooltip("Opcional: Ponto exato do lado esquerdo. Se nulo, usará a distânciaConexao.")]
    public Transform ladoEsquerdo;
    [Tooltip("Opcional: Ponto exato do lado direito. Se nulo, usará a distânciaConexao.")]
    public Transform ladoDireito;

    [Header("Debug")]
    public bool debugLogs = false;

    // ═══════════════════════════════════════════════════════════════
    // VALORES INTERNOS (o jogo calcula sozinho)
    // ═══════════════════════════════════════════════════════════════
    private int moradoresAtuais = 0;
    private float rendaTotal = 0f;
    private int qualidadeAtual = 50;

    // Constantes internas — o jogo controla
    private const int MORADORES_POR_CICLO = 2;
    private const float INTERVALO_CICLO = 5f;
    private const float RENDA_POR_MORADOR = 0.5f;
    private const int QUALIDADE_BASE = 50;
    private const int QUALIDADE_MINIMA = 20;

    // Controle interno
    private float timerCiclo = 0f;
    [Header("⚡ Energia")]
    public bool semEnergia = false;
    private float timerSaidaEnergia = 0f;
    
    private float timerRenda = 0f;
    private bool registrado = false;
    private int limitePopulacaoAdicionado = 0;
    private float rendaRegistradaNoSistema = 0f;
    private bool mouseHover = false;
    private Texture2D _texturaTooltip;

    // ═══════════════════════════════════════════════════════════════
    // PROPRIEDADES PÚBLICAS (para outros scripts lerem)
    // ═══════════════════════════════════════════════════════════════

    public int MoradoresAtuais => moradoresAtuais;
    public int Capacidade => capacidade;
    public int VagasLivres => capacidade - moradoresAtuais;
    public bool Lotado => moradoresAtuais >= capacidade;
    public float TaxaOcupacao => capacidade > 0 ? (float)moradoresAtuais / capacidade : 0f;
    public float RendaAtual => rendaTotal;
    public int QualidadeAtual => qualidadeAtual;

    public Vector3 ObterPontoEsquerdo()
    {
        if (ladoEsquerdo != null) return ladoEsquerdo.position;
        return transform.position - transform.right * distanciaConexao;
    }

    public Vector3 ObterPontoDireito()
    {
        if (ladoDireito != null) return ladoDireito.position;
        return transform.position + transform.right * distanciaConexao;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ObterPontoEsquerdo(), 1f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(ObterPontoDireito(), 1f);
    }

    // ═══════════════════════════════════════════════════════════════
    // INICIALIZAÇÃO
    // ═══════════════════════════════════════════════════════════════

    void Start()
    {
        qualidadeAtual = QUALIDADE_BASE;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null)
        {
            Debug.LogError($"[Imóvel] {name}: GerenciadorRecursos não encontrado!");
            return;
        }

        // Cada imóvel adiciona sua capacidade ao teto máximo de população
        recursos.AumentarLimitePopulacao(capacidade);
        limitePopulacaoAdicionado = capacidade;
        registrado = true;

        // Timer randômico para não sincronizar todos os imóveis
        timerCiclo = Random.Range(0f, INTERVALO_CICLO);

        if (debugLogs)
            Debug.Log($"[Imovel] {name} construido! Capacidade: {capacidade}");
    }

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
        IA_BackendBridge.RegisterImovel(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
        IA_BackendBridge.UnregisterImovel(this);
    }

    // ═══════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════

    void Update()
    {
        if (!registrado) return;

        // Ciclo de moradores
        timerCiclo += Time.deltaTime;
        
        if (semEnergia)
        {
            timerSaidaEnergia += Time.deltaTime;
            if (timerSaidaEnergia >= 15f) // Perde qualidade se sem energia
            {
                ModificarQualidade(-4);
                timerSaidaEnergia = 0f;
            }
        }

        if (timerCiclo >= INTERVALO_CICLO)
        {
            ProcessarCicloMoradores();
            timerCiclo = 0f;
        }

        // Renda (a cada segundo)
        if (moradoresAtuais > 0)
        {
            timerRenda += Time.deltaTime;
            if (timerRenda >= 1f)
            {
                timerRenda = 0f;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // LÓGICA DE MORADORES
    // ═══════════════════════════════════════════════════════════════

    void ProcessarCicloMoradores()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        if (qualidadeAtual >= QUALIDADE_MINIMA)
            ChegadaDeMoradores(recursos);
        else
            SaidaDeMoradores(recursos);
    }

    void ChegadaDeMoradores(GerenciadorRecursos recursos)
    {
        if (Lotado) return;

        int querVir = Mathf.Min(MORADORES_POR_CICLO, VagasLivres);
        int aceitos = 0;

        for (int i = 0; i < querVir; i++)
        {
            if (recursos.AdicionarPopulacao(1))
                aceitos++;
            else
                break;
        }

        if (aceitos > 0)
        {
            moradoresAtuais += aceitos;
            AtualizarRenda();
        }
    }

    void SaidaDeMoradores(GerenciadorRecursos recursos)
    {
        if (moradoresAtuais <= 0) return;

        float fatorFuga = 1f - ((float)qualidadeAtual / QUALIDADE_MINIMA);
        int querSair = Mathf.Max(1, Mathf.RoundToInt(MORADORES_POR_CICLO * fatorFuga));
        querSair = Mathf.Min(querSair, moradoresAtuais);

        moradoresAtuais -= querSair;
        recursos.RemoverPopulacao(querSair);
        AtualizarRenda();

        Debug.Log($"[Imovel] {name}: -{querSair} moradores fugiram! Qualidade: {qualidadeAtual} ({moradoresAtuais}/{capacidade})");
    }

    // ═══════════════════════════════════════════════════════════════
    // RENDA
    // ═══════════════════════════════════════════════════════════════

    void AtualizarRenda()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        // Remove renda antiga
        if (rendaRegistradaNoSistema > 0)
            recursos.ModificarGanhos(multDinheiro: -rendaRegistradaNoSistema);

        // Calcula e registra nova renda
        rendaTotal = moradoresAtuais * RENDA_POR_MORADOR;

        if (rendaTotal > 0)
            recursos.ModificarGanhos(multDinheiro: rendaTotal);

        rendaRegistradaNoSistema = rendaTotal;
    }

    // ═══════════════════════════════════════════════════════════════
    // QUALIDADE DE VIDA (API para futuro)
    // ═══════════════════════════════════════════════════════════════

    public void ModificarQualidade(int delta)
    {
        qualidadeAtual = Mathf.Clamp(qualidadeAtual + delta, 0, 100);
    }

    public void SetarSemEnergia(bool status)
    {
        if (semEnergia == status) return;
        semEnergia = status;
        if (semEnergia)
        {
            Debug.Log($"[ENERGIA] {name} está sem energia! Moradores começarão a sair em breve.");
        }
    }

    public void SetarQualidade(int novaQualidade)
    {
        qualidadeAtual = Mathf.Clamp(novaQualidade, 0, 100);
    }

    public void EvacuarTodos()
    {
        if (moradoresAtuais <= 0) return;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
            recursos.RemoverPopulacao(moradoresAtuais);

        moradoresAtuais = 0;
        AtualizarRenda();
    }

    // ═══════════════════════════════════════════════════════════════
    // ═══════════════════════════════════════════════════════════════
    // INTERACTION & TOOLTIP (Hover Connectivity Feedback)
    // ═══════════════════════════════════════════════════════════════

    void OnMouseEnter()
    {
        mouseHover = true;
    }

    void OnMouseExit()
    {
        mouseHover = false;
    }

    private Texture2D ObterTexturaTooltip()
    {
        if (_texturaTooltip == null)
        {
            _texturaTooltip = new Texture2D(1, 1);
            _texturaTooltip.SetPixel(0, 0, new Color(0.08f, 0.1f, 0.13f, 0.95f));
            _texturaTooltip.Apply();
        }
        return _texturaTooltip;
    }

    void OnGUI()
    {
        if (!mouseHover) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = ObterTexturaTooltip();
        boxStyle.padding = new RectOffset(10, 10, 10, 10);
        boxStyle.alignment = TextAnchor.MiddleLeft;

        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.richText = true;
        textStyle.fontSize = 13;
        textStyle.normal.textColor = Color.white;

        float baseConsumo = Mathf.Max(0.5f, moradoresAtuais * 0.05f);
        float consumo = baseConsumo * 1.5f;
        string statusEnergia = semEnergia ? "<color=#ff5555>⚡ SEM ENERGIA</color>" : "<color=#55ff55>⚡ COM ENERGIA</color>";
        string avisoBlackout = semEnergia ? "\n<color=orange>⚠️ Qualidade de vida caindo!</color>" : "";

        string content = $"<b>🏠 RESIDÊNCIA CIVIL ({name.Replace("(Clone)", "")})</b>\n\n" +
                         $"👥 Moradores: <b>{moradoresAtuais} / {capacidade}</b>\n" +
                         $"⚡ Consumo: <b>{consumo:F2} MW</b>\n" +
                         $"🔌 Conectividade: {statusEnergia}{avisoBlackout}";

        Vector2 size = textStyle.CalcSize(new GUIContent(content));
        float width = size.x + 20f;
        float height = size.y + 20f;

        Vector2 mousePos = Input.mousePosition;
        Rect rect = new Rect(mousePos.x + 15f, Screen.height - mousePos.y + 15f, width, height);

        if (rect.xMax > Screen.width) rect.x = mousePos.x - width - 15f;
        if (rect.yMax > Screen.height) rect.y = Screen.height - mousePos.y - height - 15f;

        GUI.Box(rect, "", boxStyle);
        GUI.Label(new Rect(rect.x + 10, rect.y + 10, size.x, size.y), content, textStyle);
    }

    // DESTRUIÇÃO
    // ═══════════════════════════════════════════════════════════════

    void OnDestroy()
    {
        RegistroEntidadesJogo.Unregister(this);
        if (!registrado) return;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        if (moradoresAtuais > 0)
            recursos.RemoverPopulacao(moradoresAtuais);

        if (rendaRegistradaNoSistema > 0)
            recursos.ModificarGanhos(multDinheiro: -rendaRegistradaNoSistema);

        if (limitePopulacaoAdicionado > 0)
            recursos.AumentarLimitePopulacao(-limitePopulacaoAdicionado);
    }
}
