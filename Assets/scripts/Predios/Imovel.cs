using UnityEngine;
using Hegemonia.AI.BrainMaster;

public class Imovel : MonoBehaviour
{
    [Header("🏠 Configuração do Imóvel")]
    public int capacidade = 10;

    [Header("🏘️ Conexão de Quarteirão (Grudar Imóveis)")]
    public float distanciaConexao = 8f;
    public Transform ladoEsquerdo;
    public Transform ladoDireito;

    [Header("🛣️ Conexão de Rua (Snaps)")]
    public Transform conexaoRua;
    public float distanciaFronteiraRua = 8f;
    public Transform conexaoRuaTras;
    public float distanciaFronteiraRuaTras = 8f;
    public bool gerarPavimentacaoConcreto = true;
    [HideInInspector] public GameObject pavimentoInstanciado;

    [Header("Debug")]
    public bool debugLogs = false;

    private int moradoresAtuais = 0;
    private float rendaTotal = 0f;
    private int qualidadeAtual = 50;

    private const int MORADORES_POR_CICLO = 2;
    private const float INTERVALO_CICLO = 5f;
    private const float RENDA_POR_MORADOR = 0.5f;
    private const int QUALIDADE_BASE = 50;
    private const int QUALIDADE_MINIMA = 20;

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

    public int MoradoresAtuais => moradoresAtuais;
    public int Capacidade => capacidade;
    public int VagasLivres => capacidade - moradoresAtuais;
    public bool Lotado => moradoresAtuais >= capacidade;
    public float TaxaOcupacao => capacidade > 0 ? (float)moradoresAtuais / capacidade : 0f;
    public float RendaAtual => rendaTotal;
    public int QualidadeAtual => qualidadeAtual;

    public struct Conector
    {
        public Vector3 posicao;
        public Vector3 direcaoSaida; 
    }

    private Vector3 CalcularDirecaoSaidaSegura(Transform conector, Vector3 fallbackDir)
    {
        if (conector == null) return fallbackDir;
        Vector3 dir = conector.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f) return dir.normalized;
        return conector.forward; 
    }

    public Conector ObterConectorFrente()
    {
        if (conexaoRua != null) return new Conector { posicao = conexaoRua.position, direcaoSaida = CalcularDirecaoSaidaSegura(conexaoRua, -transform.forward) };
        return new Conector { posicao = transform.position - transform.forward * distanciaFronteiraRua, direcaoSaida = -transform.forward };
    }

    public Conector ObterConectorTras()
    {
        if (conexaoRuaTras != null) return new Conector { posicao = conexaoRuaTras.position, direcaoSaida = CalcularDirecaoSaidaSegura(conexaoRuaTras, transform.forward) };
        return new Conector { posicao = transform.position + transform.forward * distanciaFronteiraRuaTras, direcaoSaida = transform.forward };
    }

    public Conector ObterConectorEsquerdo()
    {
        if (ladoEsquerdo != null) return new Conector { posicao = ladoEsquerdo.position, direcaoSaida = CalcularDirecaoSaidaSegura(ladoEsquerdo, -transform.right) };
        return new Conector { posicao = transform.position - transform.right * distanciaConexao, direcaoSaida = -transform.right };
    }

    public Conector ObterConectorDireito()
    {
        if (ladoDireito != null) return new Conector { posicao = ladoDireito.position, direcaoSaida = CalcularDirecaoSaidaSegura(ladoDireito, transform.right) };
        return new Conector { posicao = transform.position + transform.right * distanciaConexao, direcaoSaida = transform.right };
    }

    public void AtualizarPavimentacao(Vector3 posicaoRua)
    {
        if (!gerarPavimentacaoConcreto) return;

        if (pavimentoInstanciado != null) Destroy(pavimentoInstanciado);

        pavimentoInstanciado = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pavimentoInstanciado.name = "Pavimentacao_Concreto_" + name;
        Destroy(pavimentoInstanciado.GetComponent<Collider>());

        pavimentoInstanciado.transform.rotation = Quaternion.Euler(90f, transform.rotation.eulerAngles.y, 0f);

        Vector3 centroPavimento = (transform.position + posicaoRua) * 0.5f;
        centroPavimento.y = transform.position.y + 0.02f;
        pavimentoInstanciado.transform.position = centroPavimento;

        float larguraPavimento = Vector3.Distance(ObterConectorEsquerdo().posicao, ObterConectorDireito().posicao);
        float comprimentoPavimento = Vector3.Distance(transform.position, posicaoRua);

        pavimentoInstanciado.transform.localScale = new Vector3(larguraPavimento, comprimentoPavimento, 1f);

        Renderer rend = pavimentoInstanciado.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            rend.material = mat;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ObterConectorEsquerdo().posicao, 1f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(ObterConectorDireito().posicao, 1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ObterConectorFrente().posicao, 1.2f);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(ObterConectorTras().posicao, 1.2f);
    }

    private Transform EncontrarFilhoPeloNome(Transform raiz, string[] nomes)
    {
        Transform[] todosFilhos = raiz.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in todosFilhos)
        {
            if (t == raiz) continue; 
            foreach (string nome in nomes)
            {
                if (t.name.Equals(nome, System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
        }
        return null;
    }

    void Awake()
    {
        if (conexaoRua == null) conexaoRua = EncontrarFilhoPeloNome(transform, new string[] { "create", "frente", "conector" });
        if (conexaoRuaTras == null) conexaoRuaTras = EncontrarFilhoPeloNome(transform, new string[] { "create_tras", "atras" });
        if (ladoEsquerdo == null) ladoEsquerdo = EncontrarFilhoPeloNome(transform, new string[] { "esq", "create_esq" });
        if (ladoDireito == null) ladoDireito = EncontrarFilhoPeloNome(transform, new string[] { "dir", "direito", "create_dir" });
    }

    void Start()
    {
        qualidadeAtual = QUALIDADE_BASE;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        recursos.AumentarLimitePopulacao(capacidade);
        limitePopulacaoAdicionado = capacidade;
        registrado = true;
        timerCiclo = Random.Range(0f, INTERVALO_CICLO);
    }

    void OnEnable() { RegistroEntidadesJogo.Register(this); IA_BackendBridge.RegisterImovel(this); }
    void OnDisable() { RegistroEntidadesJogo.Unregister(this); IA_BackendBridge.UnregisterImovel(this); }

    void Update()
    {
        if (!registrado) return;
        timerCiclo += Time.deltaTime;
        
        if (semEnergia)
        {
            timerSaidaEnergia += Time.deltaTime;
            if (timerSaidaEnergia >= 15f) 
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

        if (moradoresAtuais > 0)
        {
            timerRenda += Time.deltaTime;
            if (timerRenda >= 1f) timerRenda = 0f;
        }
    }

    void ProcessarCicloMoradores()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        float atratividade = 50f;
        if (GerenciadorDivisaoTerritorial.Instancia != null) atratividade = GerenciadorDivisaoTerritorial.Instancia.ObterAtratividadeLocal(transform.position);

        float fatorAtratividade = atratividade / 50f; 

        if (qualidadeAtual >= QUALIDADE_MINIMA) ChegadaDeMoradores(recursos, fatorAtratividade);
        else SaidaDeMoradores(recursos, fatorAtratividade);
    }

    void ChegadaDeMoradores(GerenciadorRecursos recursos, float fatorAtratividade)
    {
        if (Lotado) return;
        int querVirBase = Mathf.Max(1, Mathf.RoundToInt(MORADORES_POR_CICLO * fatorAtratividade));
        int querVir = Mathf.Min(querVirBase, VagasLivres);
        int aceitos = 0;
        
        for (int i = 0; i < querVir; i++)
        {
            if (recursos.AdicionarPopulacao(1)) aceitos++;
            else break;
        }

        if (aceitos > 0)
        {
            moradoresAtuais += aceitos;
            AtualizarRenda();
        }
    }

    void SaidaDeMoradores(GerenciadorRecursos recursos, float fatorAtratividade)
    {
        if (moradoresAtuais <= 0) return;
        float fatorFuga = 1f - ((float)qualidadeAtual / QUALIDADE_MINIMA);
        if (fatorAtratividade < 1f) fatorFuga *= (2f - fatorAtratividade); 
        
        int querSair = Mathf.Max(1, Mathf.RoundToInt(MORADORES_POR_CICLO * fatorFuga));
        querSair = Mathf.Min(querSair, moradoresAtuais);
        moradoresAtuais -= querSair;
        recursos.RemoverPopulacao(querSair);
        AtualizarRenda();
    }

    void AtualizarRenda()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        if (rendaRegistradaNoSistema > 0) recursos.ModificarGanhos(multDinheiro: -rendaRegistradaNoSistema);

        rendaTotal = moradoresAtuais * RENDA_POR_MORADOR;

        if (rendaTotal > 0) recursos.ModificarGanhos(multDinheiro: rendaTotal);
        rendaRegistradaNoSistema = rendaTotal;
    }

    public void ModificarQualidade(int delta) { qualidadeAtual = Mathf.Clamp(qualidadeAtual + delta, 0, 100); }
    public void SetarSemEnergia(bool status) { semEnergia = status; }
    public void SetarQualidade(int novaQualidade) { qualidadeAtual = Mathf.Clamp(novaQualidade, 0, 100); }

    public void EvacuarTodos()
    {
        if (moradoresAtuais <= 0) return;
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null) recursos.RemoverPopulacao(moradoresAtuais);
        moradoresAtuais = 0;
        AtualizarRenda();
    }

    void OnMouseEnter() { mouseHover = true; }
    void OnMouseExit() { mouseHover = false; }

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

    void OnDestroy()
    {
        if (pavimentoInstanciado != null) Destroy(pavimentoInstanciado);
        RegistroEntidadesJogo.Unregister(this);
        if (!registrado) return;

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null) return;

        if (moradoresAtuais > 0) recursos.RemoverPopulacao(moradoresAtuais);
        if (rendaRegistradaNoSistema > 0) recursos.ModificarGanhos(multDinheiro: -rendaRegistradaNoSistema);
        if (limitePopulacaoAdicionado > 0) recursos.AumentarLimitePopulacao(-limitePopulacaoAdicionado);
    }
}