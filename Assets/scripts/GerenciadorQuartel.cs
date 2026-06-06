using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GerenciadorQuartel : MonoBehaviour
{
    [Header("Estrutura (Detectada Automaticamente)")]
    public List<Transform> dormitorios = new List<Transform>();
    public List<Transform> waypointsEntradaEstacionamento = new List<Transform>();
    public List<Transform> paradasEstacionamento = new List<Transform>();

    [Header("Unidades Armazenadas")]
    public List<ControleUnidade> soldadosNoDormitorio = new List<ControleUnidade>();
    public List<ControleUnidade> veiculosNoQuartel = new List<ControleUnidade>();
    
    private HashSet<Transform> vagasOcupadas = new HashSet<Transform>();

    [Header("Arsenal e Munição")]
    public int misseisArmazenados = 0;
    public int municaoArmazenada = 0;
    public int precoMissil = 500;
    public int precoMunicao = 100;

    [Header("Chamada Automática (Limites de Área)")]
    public float raioDeCobertura = 2000f; 
    public bool recolhimentoAutomatico = false;
    public float tempoOciosoPermitido = 60f;
    private Dictionary<ControleUnidade, float> tempoOciosoUnidades = new Dictionary<ControleUnidade, float>();

    [Header("Recursos Extras (Inovação Tática)")]
    public bool treinamentoPassivo = true; 
    public bool modoDefensivoAtivo = false; 
    private float scanDefesaTimer = 0f;

    // UI Estilos
    public static bool InterfaceAberta = false;
    private bool menuAberto = false;
    private Rect janelaRetangulo;
    private int abaAtual = 0; 
    private Vector2 scrollTropas;
    private Vector2 scrollInteligencia;
    private Vector2 scrollConvocar;
    private Vector2 scrollArsenal;
    private readonly List<ControleUnidade> soldadosAvulsosCache = new List<ControleUnidade>();
    private readonly List<ControleUnidade> veiculosAvulsosCache = new List<ControleUnidade>();
    private float proximaAtualizacaoCacheCampo;
    
    private GUIStyle estiloJanela;
    private GUIStyle estiloBotao;
    private GUIStyle estiloBotaoPerigo;
    private GUIStyle estiloBotaoSecundario;
    private GUIStyle estiloAba;
    private GUIStyle estiloAbaAtiva;
    private GUIStyle estiloTexto;
    private GUIStyle estiloTextoTitulo;
    private GUIStyle estiloTextoPequeno;
    private GUIStyle estiloCard;
    private GUIStyle estiloHeader;
    private bool estilosCriados = false;

    // Texturas reutilizáveis
    private static Texture2D _texFundoJanela;
    private static Texture2D _texBotao;
    private static Texture2D _texBotaoHover;
    private static Texture2D _texBotaoPerigo;
    private static Texture2D _texBotaoPerigHover;
    private static Texture2D _texBotaoSec;
    private static Texture2D _texBotaoSecHover;
    private static Texture2D _texAba;
    private static Texture2D _texAbaAtiva;
    private static Texture2D _texCard;
    private static Texture2D _texHeader;

    // Status Inteligência
    private class StatusInimigo {
        public string nomePais;
        public int infantaria;
        public int veiculos;
        public int navais;
        public int aereos;
        public int predios;
    }
    private Dictionary<int, StatusInimigo> infoInimigos = new Dictionary<int, StatusInimigo>();
    private float tagAtualizacaoIntel = 0f;

    void Awake()
    {
        MapearDormitorios();
        MapearEstacionamento();
        AtualizarRetanguloJanela(true);
    }

    void Update()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null) return;
        
        if (Input.GetKeyDown(KeyCode.B) && (MenuComandoController.Instancia == null || !MenuComandoController.Instancia.MenuAberto))
        {
            if (!menuAberto)
            {
                FecharOutrosMenus();
                InterfaceAberta = true;
                menuAberto = true;
                AtualizarRetanguloJanela(true);
            }
            else
            {
                menuAberto = false;
                InterfaceAberta = false;
            }
        }

        if (recolhimentoAutomatico)
        {
            MonitorarUnidadesOciosas();
        }

        if (modoDefensivoAtivo && Time.time > scanDefesaTimer)
        {
            ChecarInvasaoEAcordarBase();
            scanDefesaTimer = Time.time + 4f;
        }

        if (menuAberto)
        {
            if (abaAtual == 0)
                AtualizarCacheUnidadesCampo(false);
            else if (abaAtual == 2)
            {
                if (Time.unscaledTime > tagAtualizacaoIntel)
                {
                    AtualizarDadosInimigos();
                    tagAtualizacaoIntel = Time.unscaledTime + 3f;
                }
            }
        }
    }

    private void FecharOutrosMenus()
    {
        if (MenuGoverno.Instancia != null) MenuGoverno.Instancia.AlternarMenu(false);
        var construtor = Object.FindFirstObjectByType<MenuConstrucao>();
        if (construtor != null && MenuConstrucao.EstaAberto) construtor.AlternarMenu(false);
    }

    private void AtualizarRetanguloJanela(bool centralizar)
    {
        float larguraMaxima = Mathf.Max(760f, Screen.width - 340f);
        float larguraMinima = Mathf.Min(1040f, larguraMaxima);
        float alturaMaxima = Mathf.Max(560f, Screen.height - 80f);
        float alturaMinima = Mathf.Min(660f, alturaMaxima);
        float largura = Mathf.Clamp(Screen.width * 0.66f, larguraMinima, larguraMaxima);
        float altura = Mathf.Clamp(Screen.height * 0.78f, alturaMinima, alturaMaxima);

        janelaRetangulo.width = largura;
        janelaRetangulo.height = altura;

        if (centralizar)
        {
            janelaRetangulo.x = Mathf.Max(280f, (Screen.width - largura) * 0.5f);
            janelaRetangulo.y = Mathf.Max(32f, (Screen.height - altura) * 0.5f);
        }
    }

    private void ChecarInvasaoEAcordarBase()
    {
        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        bool inimigoProximo = false;

        foreach (var id in todas)
        {
            if (id.teamID != 1 && Vector3.Distance(transform.position, id.transform.position) <= raioDeCobertura)
            {
                inimigoProximo = true;
                break;
            }
        }

        if (inimigoProximo)
        {
            if (soldadosNoDormitorio.Count > 0 || veiculosNoQuartel.Count > 0)
            {
                DesdobrarSoldados(soldadosNoDormitorio.Count);
                int totalV = veiculosNoQuartel.Count;
                for(int i = totalV - 1; i >= 0; i--) DesdobrarVeiculo(veiculosNoQuartel[i]);
            }
        }
    }

    private void MonitorarUnidadesOciosas()
    {
        if (Time.frameCount % 90 != 0) return;

        IdentidadeUnidade[] todasUnidades = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        
        foreach (var id in todasUnidades)
        {
            if (id.teamID != 1) continue;

            ControleUnidade u = id.GetComponent<ControleUnidade>();
            if (u == null || !u.gameObject.activeInHierarchy) continue;
            
            if (u.TemControleAviao || u.TemControleAviaoCaca || id.tipoUnidade == TipoUnidade.Naval || id.tipoUnidade == TipoUnidade.Aereo || id.tipoUnidade == TipoUnidade.Estrutura) 
                continue;

            if (Vector3.Distance(transform.position, u.transform.position) > raioDeCobertura) continue;

            if (u.ObterVelocidadeAtualReal() > 0.1f || u.selecionado || 
                veiculosNoQuartel.Contains(u) || soldadosNoDormitorio.Contains(u))
            {
                tempoOciosoUnidades[u] = Time.time;
            }
            else
            {
                if (!tempoOciosoUnidades.ContainsKey(u)) tempoOciosoUnidades[u] = Time.time;

                float tempoParado = Time.time - tempoOciosoUnidades[u];
                if (tempoParado > tempoOciosoPermitido)
                {
                    ReceberUnidade(u);
                    tempoOciosoUnidades.Remove(u);
                }
            }
        }
    }

    private Texture2D CriarTextura(Color cor)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, cor);
        tex.Apply();
        return tex;
    }

    private Texture2D CriarTexturaGradiente(Color topo, Color base_)
    {
        Texture2D tex = new Texture2D(1, 4);
        tex.SetPixel(0, 0, base_);
        tex.SetPixel(0, 1, Color.Lerp(base_, topo, 0.33f));
        tex.SetPixel(0, 2, Color.Lerp(base_, topo, 0.66f));
        tex.SetPixel(0, 3, topo);
        tex.Apply();
        return tex;
    }

    private void InicializarEstilos()
    {
        if (estilosCriados) return;

        // --- Paleta Principal ---
        // Fundo da janela: cinza escuro quase preto com leve tom azul-escuro
        if (_texFundoJanela == null) _texFundoJanela = CriarTexturaGradiente(
            new Color(0.10f, 0.12f, 0.16f, 0.99f),
            new Color(0.07f, 0.08f, 0.11f, 0.99f));

        // Botão primário: verde-oliva militar com hover âmbar
        if (_texBotao == null)     _texBotao     = CriarTexturaGradiente(new Color(0.18f, 0.30f, 0.15f, 1f), new Color(0.12f, 0.20f, 0.10f, 1f));
        if (_texBotaoHover == null) _texBotaoHover = CriarTexturaGradiente(new Color(0.75f, 0.55f, 0.05f, 1f), new Color(0.55f, 0.38f, 0.02f, 1f));

        // Botão perigo: vermelho
        if (_texBotaoPerigo == null)    _texBotaoPerigo    = CriarTextura(new Color(0.50f, 0.08f, 0.08f, 1f));
        if (_texBotaoPerigHover == null) _texBotaoPerigHover = CriarTextura(new Color(0.80f, 0.15f, 0.10f, 1f));

        // Botão secundário: azul-aço
        if (_texBotaoSec == null)     _texBotaoSec     = CriarTexturaGradiente(new Color(0.10f, 0.20f, 0.38f, 1f), new Color(0.07f, 0.13f, 0.26f, 1f));
        if (_texBotaoSecHover == null) _texBotaoSecHover = CriarTexturaGradiente(new Color(0.15f, 0.35f, 0.60f, 1f), new Color(0.10f, 0.22f, 0.45f, 1f));

        // Abas
        if (_texAba == null)      _texAba      = CriarTextura(new Color(0.13f, 0.16f, 0.20f, 1f));
        if (_texAbaAtiva == null) _texAbaAtiva = CriarTexturaGradiente(new Color(0.72f, 0.53f, 0.04f, 1f), new Color(0.50f, 0.35f, 0.01f, 1f));

        // Card de item
        if (_texCard == null)   _texCard   = CriarTextura(new Color(0.12f, 0.15f, 0.19f, 0.95f));
        if (_texHeader == null) _texHeader = CriarTexturaGradiente(new Color(0.65f, 0.48f, 0.03f, 0.30f), new Color(0.08f, 0.10f, 0.14f, 0.30f));

        // --- Janela ---
        estiloJanela = new GUIStyle(GUI.skin.window);
        estiloJanela.normal.background = _texFundoJanela;
        estiloJanela.normal.textColor = new Color(0.90f, 0.82f, 0.40f);
        estiloJanela.fontStyle = FontStyle.Bold;
        estiloJanela.fontSize = 18;
        estiloJanela.padding = new RectOffset(10, 10, 30, 10);

        // --- Botão primário ---
        estiloBotao = new GUIStyle(GUI.skin.button);
        estiloBotao.normal.background  = _texBotao;
        estiloBotao.hover.background   = _texBotaoHover;
        estiloBotao.normal.textColor   = new Color(0.85f, 0.95f, 0.75f);
        estiloBotao.hover.textColor    = new Color(0.10f, 0.06f, 0.02f);
        estiloBotao.active.background  = _texBotaoHover;
        estiloBotao.padding = new RectOffset(8, 8, 7, 7);
        estiloBotao.fontSize = 14;
        estiloBotao.fontStyle = FontStyle.Bold;
        estiloBotao.wordWrap = true;

        // --- Botão perigo ---
        estiloBotaoPerigo = new GUIStyle(estiloBotao);
        estiloBotaoPerigo.normal.background = _texBotaoPerigo;
        estiloBotaoPerigo.hover.background  = _texBotaoPerigHover;
        estiloBotaoPerigo.normal.textColor  = Color.white;
        estiloBotaoPerigo.hover.textColor   = Color.white;

        // --- Botão secundário ---
        estiloBotaoSecundario = new GUIStyle(estiloBotao);
        estiloBotaoSecundario.normal.background = _texBotaoSec;
        estiloBotaoSecundario.hover.background  = _texBotaoSecHover;
        estiloBotaoSecundario.normal.textColor  = new Color(0.70f, 0.88f, 1.0f);
        estiloBotaoSecundario.hover.textColor   = Color.white;

        // --- Abas ---
        estiloAba = new GUIStyle(estiloBotao);
        estiloAba.normal.background = _texAba;
        estiloAba.hover.background  = _texAbaAtiva;
        estiloAba.normal.textColor  = new Color(0.65f, 0.75f, 0.85f);
        estiloAba.hover.textColor   = new Color(0.10f, 0.06f, 0.02f);
        estiloAba.fontSize = 14;
        estiloAba.fontStyle = FontStyle.Bold;
        estiloAba.padding   = new RectOffset(12, 12, 10, 10);

        estiloAbaAtiva = new GUIStyle(estiloAba);
        estiloAbaAtiva.normal.background = _texAbaAtiva;
        estiloAbaAtiva.normal.textColor  = new Color(0.10f, 0.06f, 0.02f);

        // --- Textos ---
        estiloTexto = new GUIStyle(GUI.skin.label);
        estiloTexto.normal.textColor = new Color(0.78f, 0.90f, 0.78f);
        estiloTexto.fontSize = 13;
        estiloTexto.fontStyle = FontStyle.Normal;
        estiloTexto.wordWrap = true;

        estiloTextoTitulo = new GUIStyle(estiloTexto);
        estiloTextoTitulo.normal.textColor = new Color(0.90f, 0.82f, 0.40f);
        estiloTextoTitulo.fontSize = 14;
        estiloTextoTitulo.fontStyle = FontStyle.Bold;

        estiloTextoPequeno = new GUIStyle(estiloTexto);
        estiloTextoPequeno.fontSize = 12;
        estiloTextoPequeno.normal.textColor = new Color(0.55f, 0.70f, 0.55f);

        // --- Card (caixas de item) ---
        estiloCard = new GUIStyle(GUI.skin.box);
        estiloCard.normal.background = _texCard;
        estiloCard.padding = new RectOffset(8, 8, 6, 6);
        estiloCard.margin  = new RectOffset(0, 0, 3, 3);

        // --- Header de seção ---
        estiloHeader = new GUIStyle(GUI.skin.box);
        estiloHeader.normal.background = _texHeader;
        estiloHeader.normal.textColor  = new Color(0.90f, 0.82f, 0.40f);
        estiloHeader.fontSize  = 13;
        estiloHeader.fontStyle = FontStyle.Bold;
        estiloHeader.alignment = TextAnchor.MiddleLeft;
        estiloHeader.padding   = new RectOffset(10, 6, 5, 5);

        estilosCriados = true;
    }

    void OnGUI()
    {
        if (!menuAberto) return;
        InicializarEstilos();

        GUI.depth = -100;
        janelaRetangulo = GUI.Window(943, janelaRetangulo, DesenharJanela, "  ⚔  QUARTEL GENERAL  |  CENTRO DE COMANDO", estiloJanela);
    }

    void DesenharJanela(int windowID)
    {
        // --- Header de Status ---
        GUILayout.BeginHorizontal(estiloHeader, GUILayout.Height(36));
        GUILayout.Label($"🪖 Soldados: {soldadosNoDormitorio.Count}   🚗 Veículos: {veiculosNoQuartel.Count}   🚀 Mísseis: {misseisArmazenados}   💊 Munição: {municaoArmazenada}", estiloTextoTitulo);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Raio: {raioDeCobertura:F0}m", estiloTextoPequeno, GUILayout.Width(110));
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // --- Abas ---
        string[] nomesAbas   = { "🪖  TROPAS", "🔧  ARSENAL", "🛰  INTELIGÊNCIA" };
        GUILayout.BeginHorizontal();
        for (int i = 0; i < nomesAbas.Length; i++)
        {
            GUIStyle estilo = (abaAtual == i) ? estiloAbaAtiva : estiloAba;
            if (GUILayout.Button(nomesAbas[i], estilo, GUILayout.Height(40)))
                abaAtual = i;
        }
        GUILayout.EndHorizontal();

        // Linha separadora visual
        Rect linhaRect = GUILayoutUtility.GetLastRect();
        GUILayout.Space(6);

        if (abaAtual == 0) DesenharAbaTropas();
        else if (abaAtual == 1) DesenharAbaArsenal();
        else if (abaAtual == 2) DesenharAbaInteligencia();

        if (GUI.Button(new Rect(janelaRetangulo.width - 42, 4, 36, 26), "✕", estiloBotaoPerigo))
        {
            menuAberto = false;
            InterfaceAberta = false;
        }

        GUI.DragWindow(new Rect(0, 0, janelaRetangulo.width, 30));
    }

    private void AtualizarCacheUnidadesCampo(bool forcar)
    {
        if (!forcar && Time.unscaledTime < proximaAtualizacaoCacheCampo)
        {
            return;
        }

        proximaAtualizacaoCacheCampo = Time.unscaledTime + 0.75f;
        soldadosAvulsosCache.Clear();
        veiculosAvulsosCache.Clear();

        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        float raioSqr = raioDeCobertura * raioDeCobertura;

        foreach (var id in todas)
        {
            if (id == null || id.teamID != 1) continue;

            ControleUnidade u = id.GetComponent<ControleUnidade>();
            if (u == null || !u.gameObject.activeInHierarchy) continue;
            if (u.TemControleAviao || u.TemControleAviaoCaca || id.tipoUnidade == TipoUnidade.Naval || id.tipoUnidade == TipoUnidade.Estrutura || id.tipoUnidade == TipoUnidade.Aereo) continue;
            if (veiculosNoQuartel.Contains(u) || soldadosNoDormitorio.Contains(u)) continue;
            if ((u.transform.position - transform.position).sqrMagnitude > raioSqr) continue;

            SistemaDeDanos dmg = u.GetComponent<SistemaDeDanos>();
            if (dmg != null && dmg.unidadeBiologica) soldadosAvulsosCache.Add(u);
            else veiculosAvulsosCache.Add(u);
        }
    }

    private void DesenharSeparador(string titulo)
    {
        GUILayout.Space(4);
        GUILayout.Label(titulo, estiloHeader, GUILayout.ExpandWidth(true), GUILayout.Height(24));
        GUILayout.Space(4);
    }

    void DesenharAbaTropas()
    {
        float colW = janelaRetangulo.width * 0.48f;
        GUILayout.BeginHorizontal();

        // =========== COLUNA ESQUERDA — RECOLHER DO CAMPO ===========
        GUILayout.BeginVertical(estiloCard, GUILayout.Width(colW));

        DesenharSeparador($"📡  EM CAMPO  —  Soldados: {soldadosAvulsosCache.Count}  |  Veículos: {veiculosAvulsosCache.Count}");

        if (GUILayout.Button("↩  CONVOCAR SELECIONADOS NO MAPA", estiloBotaoSecundario, GUILayout.Height(36)))
        {
            foreach (var u in Object.FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None))
                if (u.selecionado && u.GetComponent<IdentidadeUnidade>()?.teamID == 1)
                {
                    u.selecionado = false;
                    ReceberUnidade(u);
                }
        }
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"↩ Chamar Infantaria ({soldadosAvulsosCache.Count})", estiloBotao, GUILayout.Height(32)))
            foreach (var u in soldadosAvulsosCache) ReceberUnidade(u);
        if (GUILayout.Button($"↩ Chamar Veículos ({veiculosAvulsosCache.Count})", estiloBotao, GUILayout.Height(32)))
            foreach (var u in veiculosAvulsosCache) ReceberUnidade(u);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        scrollConvocar = GUILayout.BeginScrollView(scrollConvocar);

        if (soldadosAvulsosCache.Count > 0)
        {
            GUILayout.Label("  🪖 INFANTARIA LIVRE", estiloTextoTitulo);
            foreach (var s in soldadosAvulsosCache)
            {
                GUILayout.BeginHorizontal(estiloCard);
                GUILayout.Label($"· {s.name}", estiloTexto);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↩ Convocar", estiloBotao, GUILayout.Width(95), GUILayout.Height(26))) ReceberUnidade(s);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
        }
        if (veiculosAvulsosCache.Count > 0)
        {
            GUILayout.Label("  🚗 VEÍCULOS LIVRES", estiloTextoTitulo);
            foreach (var v in veiculosAvulsosCache)
            {
                GUILayout.BeginHorizontal(estiloCard);
                GUILayout.Label($"· {v.name}", estiloTexto);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↩ Convocar", estiloBotao, GUILayout.Width(95), GUILayout.Height(26))) ReceberUnidade(v);
                GUILayout.EndHorizontal();
            }
        }
        if (soldadosAvulsosCache.Count == 0 && veiculosAvulsosCache.Count == 0)
            GUILayout.Label("  ✅  Nenhuma unidade solta no raio do Quartel.", estiloTextoPequeno);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(8);

        // =========== COLUNA DIREITA — TROPAS ARMAZENADAS ===========
        GUILayout.BeginVertical(estiloCard, GUILayout.Width(colW));

        DesenharSeparador($"🏠  ARMAZENADAS  —  Soldados: {soldadosNoDormitorio.Count}  |  Veículos: {veiculosNoQuartel.Count}");

        // Soldados
        GUILayout.Label("  🪖 DORMITÓRIO", estiloTextoTitulo);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Desdobrar 1",  estiloBotao, GUILayout.Height(32))) DesdobrarSoldados(1);
        if (GUILayout.Button("Desdobrar 5",  estiloBotao, GUILayout.Height(32))) DesdobrarSoldados(5);
        if (GUILayout.Button("Esvaziar Tudo", estiloBotaoPerigo, GUILayout.Height(32))) DesdobrarSoldados(soldadosNoDormitorio.Count);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Veículos
        GUILayout.Label("  🚗 GARAGEM", estiloTextoTitulo);
        if (GUILayout.Button("🔑  LIGAR TODOS OS VEÍCULOS", estiloBotaoPerigo, GUILayout.Height(34)))
        {
            int totalV = veiculosNoQuartel.Count;
            for (int i = totalV - 1; i >= 0; i--) DesdobrarVeiculo(veiculosNoQuartel[i]);
        }

        GUILayout.Space(6);
        scrollTropas = GUILayout.BeginScrollView(scrollTropas);
        for (int i = 0; i < veiculosNoQuartel.Count; i++)
        {
            ControleUnidade v = veiculosNoQuartel[i];
            if (v == null) continue;
            GUILayout.BeginHorizontal(estiloCard);
            GUILayout.Label($"· {v.name}", estiloTexto);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🔑 Ligar", estiloBotaoSecundario, GUILayout.Width(80), GUILayout.Height(26))) DesdobrarVeiculo(v);
            GUILayout.EndHorizontal();
        }
        if (veiculosNoQuartel.Count == 0)
            GUILayout.Label("  ✅  Nenhum veículo estacionado.", estiloTextoPequeno);
        GUILayout.EndScrollView();

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    void DesenharAbaArsenal()
    {
        scrollArsenal = GUILayout.BeginScrollView(scrollArsenal);

        // --- Protocolos ---
        DesenharSeparador("⚙  PROTOCOLOS DA BASE");
        GUILayout.BeginVertical(estiloCard);
        recolhimentoAutomatico = GUILayout.Toggle(recolhimentoAutomatico, "  📻  Recolhimento Automático  (chama unidades ociosas por rádio)", estiloTexto);
        if (recolhimentoAutomatico)
        {
            GUILayout.Label($"     Tempo ocioso antes de chamar: {Mathf.Round(tempoOciosoPermitido)}s", estiloTextoPequeno);
            tempoOciosoPermitido = GUILayout.HorizontalSlider(tempoOciosoPermitido, 10f, 300f);
        }
        GUILayout.Space(4);
        modoDefensivoAtivo = GUILayout.Toggle(modoDefensivoAtivo, "  🛡  Defesa Automática  (libera tudo se a base for invadida)", estiloTexto);
        GUILayout.Space(4);
        treinamentoPassivo = GUILayout.Toggle(treinamentoPassivo, "  💪  Treinamento Passivo  (bônus de HP para unidades em repouso)", estiloTexto);
        GUILayout.EndVertical();

        // --- Arsenal ---
        GUILayout.Space(6);
        DesenharSeparador("🚀  ARSENAL E MUNIÇÕES");
        GUILayout.BeginVertical(estiloCard);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"🚀  Mísseis Armazenados:", estiloTexto, GUILayout.Width(210));
        GUILayout.Label($"{misseisArmazenados}", estiloTextoTitulo);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label($"🔫  Pacotes de Munição:", estiloTexto, GUILayout.Width(210));
        GUILayout.Label($"{municaoArmazenada}", estiloTextoTitulo);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        if (GerenciadorRecursos.Instancia != null)
        {
            GUILayout.Label($"💰  Fundo Nacional: ${GerenciadorRecursos.Instancia.dinheiro}", estiloTextoTitulo);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"🚀  Encomendar Mísseis  (-${precoMissil})", estiloBotao, GUILayout.Height(42)))
                if (GerenciadorRecursos.Instancia.TentarGastarDinheiro(precoMissil)) misseisArmazenados += 10;
            if (GUILayout.Button($"🔫  Encomendar Munição  (-${precoMunicao})", estiloBotao, GUILayout.Height(42)))
                if (GerenciadorRecursos.Instancia.TentarGastarDinheiro(precoMunicao)) municaoArmazenada += 100;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        // --- Logística ---
        GUILayout.Space(6);
        DesenharSeparador("🚛  LOGÍSTICA DE ABASTECIMENTO");
        GUILayout.BeginVertical(estiloCard);
        CaminhaoCombustivel.AbastecimentoAutomaticoGlobal = GUILayout.Toggle(CaminhaoCombustivel.AbastecimentoAutomaticoGlobal, "  🔄  Abastecimento Automático  (Tracks buscam unidades com combustível baixo)", estiloTexto);
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("⛽  Carregar Tracks neste QG", estiloBotao, GUILayout.Height(36)))
            foreach (var c in Object.FindObjectsByType<CaminhaoCombustivel>(FindObjectsSortMode.None))
                if (c != null) c.ForcarRecarregarNoQuartel(this);
        if (GUILayout.Button("↩  Forçar Retorno à Base", estiloBotaoSecundario, GUILayout.Height(36)))
        {
            var caminhoes = Object.FindObjectsByType<CaminhaoCombustivel>(FindObjectsSortMode.None);
            foreach (var c in caminhoes)
                if (c != null) { c.DefinirQuartelPreferencial(this); c.ForcarRetornoBase(); }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("  ℹ  Tracks atendem somente a área do QG, recarregam abaixo de 20% e retornam para reabastecimento.", estiloTextoPequeno);
        GUILayout.EndVertical();

        GUILayout.EndScrollView();
    }

    void DesenharAbaInteligencia()
    {
        DesenharSeparador("🛰  VARREDURA SATELITAL — ESPIONAGEM CIBERNÉTICA");
        GUILayout.Label("  Monitoramento em tempo real dos países oponentes.", estiloTextoPequeno);
        GUILayout.Space(6);

        scrollInteligencia = GUILayout.BeginScrollView(scrollInteligencia);

        foreach (var kvp in infoInimigos)
        {
            if (kvp.Key == 1) continue;

            var status = kvp.Value;
            GUILayout.BeginVertical(estiloCard);

            // Cabeçalho do país
            GUILayout.BeginHorizontal(estiloHeader, GUILayout.Height(28));
            GUILayout.Label($"🔴  {status.nomePais.ToUpper()}", estiloTextoTitulo);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Time #{kvp.Key}", estiloTextoPequeno, GUILayout.Width(70));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            int max = Mathf.Max(1, status.infantaria + status.veiculos + status.aereos + status.navais);
            DesenharBarraForca("🪖 Infantaria",  status.infantaria, max, new Color(0.3f, 0.7f, 0.3f));
            DesenharBarraForca("🚗 Blindados",   status.veiculos,   max, new Color(0.6f, 0.5f, 0.2f));
            DesenharBarraForca("✈  Aéreos",     status.aereos,     max, new Color(0.2f, 0.5f, 0.9f));
            DesenharBarraForca("⚓ Naval",       status.navais,     max, new Color(0.1f, 0.6f, 0.8f));
            GUILayout.Label($"   🏛  Estruturas: {status.predios}", estiloTextoPequeno);
            GUILayout.Space(4);

            GUILayout.EndVertical();
            GUILayout.Space(8);
        }

        if (infoInimigos.Count <= 1)
            GUILayout.Label("  📡  Aguardando sinal... Nenhum inimigo monitorado.", estiloTextoPequeno);

        GUILayout.EndScrollView();
    }

    private void DesenharBarraForca(string label, int valor, int maximo, Color cor)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"   {label}:", estiloTexto, GUILayout.Width(130));
        GUILayout.Label($"{valor}", estiloTextoTitulo, GUILayout.Width(40));

        Rect baraBg = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(12));
        float fill = maximo > 0 ? Mathf.Clamp01((float)valor / maximo) : 0f;
        // Fundo
        Color oldColor = GUI.color;
        GUI.color = new Color(0.12f, 0.15f, 0.18f, 1f);
        GUI.DrawTexture(baraBg, Texture2D.whiteTexture);
        // Preenchimento
        Rect barFill = new Rect(baraBg.x, baraBg.y, baraBg.width * fill, baraBg.height);
        GUI.color = cor;
        GUI.DrawTexture(barFill, Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUILayout.Space(8);
        GUILayout.EndHorizontal();
    }

    void AtualizarDadosInimigos()
    {
        infoInimigos.Clear();
        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        
        foreach (var id in todas)
        {
            if (!infoInimigos.ContainsKey(id.teamID))
                infoInimigos[id.teamID] = new StatusInimigo { nomePais = id.nomeDoPais };
            
            var s = infoInimigos[id.teamID];
            if (id.tipoUnidade == TipoUnidade.Infantaria) s.infantaria++;
            else if (id.tipoUnidade == TipoUnidade.Veiculo) s.veiculos++;
            else if (id.tipoUnidade == TipoUnidade.Aereo) s.aereos++;
            else if (id.tipoUnidade == TipoUnidade.Naval) s.navais++;
            else if (id.tipoUnidade == TipoUnidade.Estrutura) s.predios++;
        }
    }

    private void MapearDormitorios()
    {
        Transform dom = ObterFilhoPorNome(transform, "dormitorio");
        if (dom != null)
            foreach (Transform filho in dom)
                dormitorios.Add(filho);
    }

    private void MapearEstacionamento()
    {
        Transform estac = ObterFilhoPorNome(transform, "estacionamento");
        if (estac != null)
        {
            Transform entrada = ObterFilhoPorNome(estac, "entrada");
            if (entrada != null)
                foreach (Transform filho in entrada)
                    waypointsEntradaEstacionamento.Add(filho);

            Transform paradas = ObterFilhoPorNome(estac, "paradas");
            if (paradas != null)
                foreach (Transform filho in paradas)
                    paradasEstacionamento.Add(filho);
        }
    }

    private Transform ObterFilhoPorNome(Transform pai, string nomeContido)
    {
        Transform[] todos = pai.GetComponentsInChildren<Transform>(true);
        foreach (Transform filho in todos)
            if (filho.name.ToLower().Contains(nomeContido.ToLower()))
                return filho;
        return null;
    }

    public void ReceberUnidade(ControleUnidade unidade)
    {
        if (unidade == null || !unidade.gameObject.activeInHierarchy) return;
        SistemaDeDanos sistemaDeDanos = unidade.GetComponent<SistemaDeDanos>();
        bool biologica = (sistemaDeDanos != null && sistemaDeDanos.unidadeBiologica);

        if (biologica)
            StartCoroutine(AcolherSoldado(unidade, sistemaDeDanos));
        else
            StartCoroutine(AcolherVeiculo(unidade, sistemaDeDanos));
    }

    private IEnumerator AcolherSoldado(ControleUnidade soldado, SistemaDeDanos danos)
    {
        if (soldadosNoDormitorio.Contains(soldado)) yield break; // Evita loop de duplicação

        Transform destino = transform; 
        if (dormitorios.Count > 0) destino = dormitorios[Random.Range(0, dormitorios.Count)];

        soldado.EmitirOrdemMover(destino.position);

        while (soldado != null && soldado.gameObject.activeInHierarchy)
        {
            if (Vector3.Distance(soldado.transform.position, destino.position) < 4f) break;
            yield return null;
        }

        if (soldado != null)
        {
            if (danos != null) 
            {
                danos.Reparar(9999f);
                if (treinamentoPassivo) danos.vidaMaxima *= 1.2f; 
            }
            soldado.gameObject.SetActive(false); 
            if (!soldadosNoDormitorio.Contains(soldado)) soldadosNoDormitorio.Add(soldado);
        }
    }

    private IEnumerator AcolherVeiculo(ControleUnidade veiculo, SistemaDeDanos danos)
    {
        if (veiculosNoQuartel.Contains(veiculo)) yield break; // Evita duplicação

        for (int i = 0; i < waypointsEntradaEstacionamento.Count; i++)
        {
            if (veiculo == null) yield break;
            Transform wp = waypointsEntradaEstacionamento[i];
            veiculo.EmitirOrdemMover(wp.position);
            while (veiculo != null)
            {
                if (Vector3.Distance(veiculo.transform.position, wp.position) < 5f) break;
                yield return null;
            }
        }

        if (veiculo == null) yield break;

        Transform vagaEscolhida = null;
        foreach (Transform vaga in paradasEstacionamento)
        {
            if (!vagasOcupadas.Contains(vaga))
            {
                vagaEscolhida = vaga;
                break;
            }
        }

        if (vagaEscolhida != null)
        {
            vagasOcupadas.Add(vagaEscolhida);
            veiculo.EmitirOrdemMover(vagaEscolhida.position);
            while (veiculo != null)
            {
                if (Vector3.Distance(veiculo.transform.position, vagaEscolhida.position) < 3.5f) break;
                yield return null;
            }

            if (veiculo != null)
            {
                if (danos != null) danos.Reparar(9999f);

                veiculo.transform.position = vagaEscolhida.position;
                veiculo.transform.rotation = vagaEscolhida.rotation;
                
                var agente = veiculo.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agente != null) agente.enabled = false;
                
                veiculo.DefinirModoCombate(false); 
                if (!veiculosNoQuartel.Contains(veiculo)) veiculosNoQuartel.Add(veiculo);
            }
        }
        else
        {
            if (danos != null) danos.Reparar(9999f);
            veiculo.gameObject.SetActive(false);
            if (!veiculosNoQuartel.Contains(veiculo)) veiculosNoQuartel.Add(veiculo);
        }
    }

    private void DesdobrarSoldados(int quantidade)
    {
        Vector3 pontoSaida = transform.position + (transform.forward * 15f);
        int liberados = 0;
        for (int i = soldadosNoDormitorio.Count - 1; i >= 0; i--)
        {
            if (liberados >= quantidade) break;
            ControleUnidade soldado = soldadosNoDormitorio[i];
            soldadosNoDormitorio.RemoveAt(i);
            
            if (soldado != null)
            {
                soldado.gameObject.SetActive(true);
                soldado.transform.position = transform.position + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                
                var danos = soldado.GetComponent<SistemaDeDanos>();
                if (danos != null) danos.Reparar(9999f); 

                soldado.EmitirOrdemMover(pontoSaida);
                liberados++;
            }
        }
    }
    
    private void DesdobrarVeiculo(ControleUnidade veiculoEspecifico)
    {
        if (veiculoEspecifico != null && veiculosNoQuartel.Contains(veiculoEspecifico))
        {
            veiculosNoQuartel.Remove(veiculoEspecifico);
            veiculoEspecifico.gameObject.SetActive(true);
            
            var agente = veiculoEspecifico.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null)
            {
                agente.enabled = true;
                agente.Warp(veiculoEspecifico.transform.position);
            }
            
            veiculoEspecifico.DefinirModoCombate(true);
            
            foreach (Transform vaga in paradasEstacionamento)
            {
                if (Vector3.Distance(vaga.position, veiculoEspecifico.transform.position) < 2.5f)
                {
                    vagasOcupadas.Remove(vaga);
                    break;
                }
            }

            Vector3 pontoSaida = waypointsEntradaEstacionamento.Count > 0 ? waypointsEntradaEstacionamento[0].position : transform.position + (transform.forward * 20f);
            veiculoEspecifico.EmitirOrdemMover(pontoSaida);
        }
    }
}
