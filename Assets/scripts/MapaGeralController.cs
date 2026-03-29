using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MAPA GERAL TÁTICO - Pressione M para abrir/fechar.
/// - Camera ortográfica de cima com fundo pintado de azul oceano.
/// - Mostra ícones de PRÉDIOS aliados e UNIDADES aliadas.
/// - NUNCA revela unidades inimigas (Fog of War).
/// - Zoom com scroll do mouse. Pan com WASD/setas ou bordas da tela.
/// </summary>
public class MapaGeralController : MonoBehaviour
{
    private Camera cameraPrincipal;
    private Camera cameraMapa;
    private bool mapaAtivo = false;

    [Header("Configurações do Mapa")]
    public float velocidadeMover  = 120f;
    public float zoomVelocidade   = 1800f;
    public float zoomMinimo = 30f;
    public float zoomMaximo = 500f;

    [Header("Configurações de Exibição")]
    public int meuTeamID = 1; // ID do jogador (unidades aliadas a mostrar)
    public float nivelDoMar = 0f; // Heights abaixo disso = oceano azul

    // Cores do mapa
    private readonly Color corFundoMar      = new Color(0.10f, 0.25f, 0.55f, 1f);   // Azul oceano profundo
    private readonly Color corBordaMar      = new Color(0.18f, 0.40f, 0.70f, 1f);   // Borda da água
    private readonly Color corPredioProprio = new Color(0.90f, 0.90f, 0.90f, 1f);   // Branco
    private readonly Color corUnidadePropria= new Color(0.25f, 1.00f, 0.25f, 1f);   // Verde claro
    private readonly Color corUnidadeNeutro = new Color(0.70f, 0.70f, 0.70f, 1f);   // Cinza

    // Cache de objetos do mundo para não chamar Find() o tempo todo
    private List<IdentidadeUnidade> _cacheUnidades = new List<IdentidadeUnidade>();
    private float _tempoRefreshCache = 0f;

    void Start()
    {
        cameraPrincipal = Camera.main;

        GameObject camObj = new GameObject("Camera_MapaGeral");
        cameraMapa = camObj.AddComponent<Camera>();

        cameraMapa.orthographic     = true;
        cameraMapa.orthographicSize = 200f;
        cameraMapa.clearFlags       = CameraClearFlags.SolidColor;
        cameraMapa.backgroundColor  = corFundoMar; // Fundo azul oceano!
        cameraMapa.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cameraMapa.cullingMask      = ~0; // Vê tudo inicialmente
        cameraMapa.depth            = 100;
        cameraMapa.farClipPlane     = 2000f;
        cameraMapa.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            mapaAtivo = !mapaAtivo;
            cameraMapa.gameObject.SetActive(mapaAtivo);

            if (mapaAtivo && cameraPrincipal != null)
            {
                Vector3 p = cameraPrincipal.transform.position;
                cameraMapa.transform.position = new Vector3(p.x, 1200f, p.z);
                AudioListener.volume = 0.3f;
                RefreshCache();
            }
            else
            {
                AudioListener.volume = 1f;
            }
        }

        if (mapaAtivo && cameraMapa != null)
        {
            ControlarMapa();

            // Refresh do cache a cada 2s
            if (Time.time > _tempoRefreshCache)
            {
                RefreshCache();
                _tempoRefreshCache = Time.time + 2f;
            }
        }
    }

    void RefreshCache()
    {
        _cacheUnidades.Clear();
        var todos = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        foreach (var u in todos)
            if (u != null) _cacheUnidades.Add(u);
    }

    void ControlarMapa()
    {
        float movX = Input.GetAxisRaw("Horizontal");
        float movZ = Input.GetAxisRaw("Vertical");

        if (Input.mousePosition.x >= Screen.width  - 5) movX =  1;
        if (Input.mousePosition.x <= 5)                 movX = -1;
        if (Input.mousePosition.y >= Screen.height - 5) movZ =  1;
        if (Input.mousePosition.y <= 5)                 movZ = -1;

        if (movX != 0 || movZ != 0)
        {
            float mult = cameraMapa.orthographicSize / 50f;
            cameraMapa.transform.position += new Vector3(movX, 0, movZ).normalized
                * (velocidadeMover * mult) * Time.unscaledDeltaTime;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            cameraMapa.orthographicSize -= scroll * zoomVelocidade * Time.unscaledDeltaTime;
            cameraMapa.orthographicSize  = Mathf.Clamp(cameraMapa.orthographicSize, zoomMinimo, zoomMaximo);
        }
    }

    // ================================================================
    // OVERLAY DE INTERFACE DO MAPA (desenhado quando mapa está ativo)
    // ================================================================
    void OnGUI()
    {
        if (!mapaAtivo || cameraMapa == null) return;

        // --- Barra superior com info e botão de fechar ---
        float barH = 30f;
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize   = 15,
            fontStyle  = FontStyle.Bold,
            normal     = { textColor = Color.cyan }
        };
        GUI.Label(new Rect(0, 0, Screen.width, barH), "⬛ MAPA ESTRATÉGICO GLOBAL   [WASD / Scroll para navegar]   [M para fechar]", titleStyle);

        // --- Legenda no canto inferior esquerdo ---
        float legX = 12f, legY = Screen.height - 100f;
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(legX - 6, legY - 6, 175f, 90f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle legStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
        GUI.Label(new Rect(legX, legY,      170, 20), "■  Prédio Aliado",   legStyle);
        GUI.Label(new Rect(legX, legY + 22, 170, 20), "▲  Unidade Aliada",  legStyle);
        GUI.Label(new Rect(legX, legY + 44, 170, 20), "●  Unidade Neutra",  legStyle);
        GUI.Label(new Rect(legX, legY + 66, 170, 20), "🔵  Oceano",          legStyle);

        // --- Ícones das unidades e prédios no mapa ---
        DesenharIconesNoMapa();
    }

    void DesenharIconesNoMapa()
    {
        if (cameraMapa == null) return;

        foreach (var id in _cacheUnidades)
        {
            if (id == null || id.gameObject == null) continue;

            bool ehAliado  = (id.teamID == meuTeamID);
            bool ehNeutro  = (id.teamID == 0);
            bool ehInimigo = (!ehAliado && !ehNeutro);

            // !! FOG OF WAR: NUNCA mostrar unidades inimigas !!
            if (ehInimigo) continue;

            bool ehPredio = (id.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
                         && (id.GetComponent<UnityEngine.AI.NavMeshObstacle>() != null
                          || id.GetComponent<Rigidbody>() == null);

            // Converte posição 3D para coordenadas da tela relativa à cameraMapa
            Vector3 screenPos = cameraMapa.WorldToScreenPoint(id.transform.position);

            // Só mostra se estiver na frente da câmera (z > 0) e dentro da tela
            if (screenPos.z <= 0) continue;

            // Inverte Y (Unity GUI vs tela)
            float sx = screenPos.x;
            float sy = Screen.height - screenPos.y;

            if (sx < -20 || sx > Screen.width + 20 || sy < -20 || sy > Screen.height + 20) continue;

            if (ehAliado && ehPredio)
            {
                // Prédio aliado: quadrado branco com borda preta
                DesenharIcone(sx, sy, 10f, 10f, corPredioProprio);
            }
            else if (ehAliado)
            {
                // Unidade aliada: triângulo verde
                DesenharTriangulo(sx, sy, 8f, corUnidadePropria);
            }
            else if (ehNeutro)
            {
                // Unidade neutra: círculo cinza
                DesenharIcone(sx, sy, 7f, 7f, corUnidadeNeutro);
            }
        }
    }

    // Desenha um quadrado colorido
    void DesenharIcone(float cx, float cy, float w, float h, Color cor)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(cx - w/2f - 1, cy - h/2f - 1, w + 2, h + 2), Texture2D.whiteTexture);
        GUI.color = cor;
        GUI.DrawTexture(new Rect(cx - w/2f, cy - h/2f, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // Desenha triângulo (simulado com labels de símbolo)
    void DesenharTriangulo(float cx, float cy, float size, Color cor)
    {
        GUIStyle st = new GUIStyle()
        {
            fontSize  = (int)(size * 2),
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.black }
        };
        // Sombra
        GUI.Label(new Rect(cx - size + 1, cy - size + 1, size * 2, size * 2), "▲", st);
        // Cor real
        st.normal.textColor = cor;
        GUI.Label(new Rect(cx - size, cy - size, size * 2, size * 2), "▲", st);
    }
}
