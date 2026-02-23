using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class ModoDemolicao : MonoBehaviour
{
    public static ModoDemolicao Instancia;

    [Header("Configuração")]
    public Texture2D cursorDemolicao;           // Ícone opcional para o cursor
    public float duracaoDemolicao = 1.5f;       // Segundos para encolher até zero

    private bool ativo = false;
    private GameObject alvoAtual;

    // Cursor animado
    private GameObject cursorAnimadoObj;
    private RectTransform cursorRect;

    // ─── Singleton ───────────────────────────────────────────────
    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        CriarCursorAnimado();
    }

    // ─── Cursor Visual ───────────────────────────────────────────
    void CriarCursorAnimado()
    {
        GameObject canvasObj = GameObject.Find("Canvas_Cursor_Demolicao");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas_Cursor_Demolicao");
            Canvas c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 9999;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        cursorAnimadoObj = new GameObject("Cursor_Demolicao_Icone");
        cursorAnimadoObj.transform.SetParent(canvasObj.transform, false);

        var img = cursorAnimadoObj.AddComponent<Image>();
        img.raycastTarget = false;

        cursorRect = cursorAnimadoObj.GetComponent<RectTransform>();
        cursorRect.sizeDelta = new Vector2(40, 40);
        cursorRect.anchorMin = Vector2.zero;
        cursorRect.anchorMax = Vector2.zero;
        cursorRect.pivot    = new Vector2(0.5f, 0.5f);

        if (cursorDemolicao != null)
        {
            Rect rec = new Rect(0, 0, cursorDemolicao.width, cursorDemolicao.height);
            img.sprite = Sprite.Create(cursorDemolicao, rec, new Vector2(0.5f, 0.5f));
            img.color  = Color.white;
        }
        else
        {
            img.color = new Color(1f, 0.15f, 0.15f, 0.85f); // Vermelho sem textura
        }

        cursorAnimadoObj.SetActive(false);
    }

    // ─── Ativar / Desativar modo ────────────────────────────────-
    public void AlternarModo(bool estado)
    {
        ativo = estado;

        if (cursorAnimadoObj != null)
            cursorAnimadoObj.SetActive(ativo);

        if (!ativo)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        Debug.Log($"[ModoDemolicao] Modo: {(ativo ? "ATIVO" : "DESLIGADO")}");
    }

    // ─── Update principal ─────────────────────────────────────────
    void Update()
    {
        // Atalho: T ou Delete
        if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Delete))
            AlternarModo(!ativo);

        if (!ativo) return;

        // Anima cursor
        if (cursorRect != null)
        {
            cursorRect.position = Input.mousePosition;
            float s = 1f + Mathf.PingPong(Time.time * 5f, 0.3f);
            cursorRect.localScale = new Vector3(s, s, 1f);
        }

        // Cancelar
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            AlternarModo(false);
            return;
        }

        // ─── Clique para demolir ─────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            // Evita clicar em UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit toque;
            int mascara = ~(1 << 2); // tudo menos IgnoreRaycast

            if (Physics.Raycast(raio, out toque, 2000f, mascara))
            {
                GameObject obj = toque.collider.gameObject;

                // Ignora terreno e água
                if (obj.GetComponent<Terrain>()  != null) return;
                if (obj.name.ToLower().Contains("terreno")) return;
                if (obj.layer == LayerMask.NameToLayer("Water")) return;

                // Descobre a raiz lógica do objeto
                GameObject raiz = EncontrarRaiz(obj);

                Debug.Log($"[Demolição] Demolindo instantaneamente: {raiz.name}");
                Destroy(raiz);
            }
        }
    }

    // ─── Encontra o objeto "raiz" que deve ser demolido ───────────
    GameObject EncontrarRaiz(GameObject obj)
    {
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.GetComponent<IdentidadeUnidade>() != null) return t.gameObject;
            if (t.GetComponent<AtributosPredio>()   != null) return t.gameObject;
            if (t.GetComponent<Edificio>()           != null) return t.gameObject;
            t = t.parent;
        }
        // Fallback: pai imediato ou o próprio objeto
        return obj.transform.parent != null ? obj.transform.parent.gameObject : obj;
    }
}
