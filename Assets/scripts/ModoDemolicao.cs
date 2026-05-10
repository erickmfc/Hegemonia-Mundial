using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class ModoDemolicao : MonoBehaviour
{
    public static ModoDemolicao Instancia;
    public static bool TemModoAtivo => Instancia != null && Instancia.ativo;

    [Header("Configuração")]
    public Texture2D cursorDemolicao;           // Ícone opcional para o cursor
    public float duracaoDemolicao = 1.5f;       // Segundos para encolher até zero

    private bool ativo = false;
    private GameObject alvoAtual;
    public bool EstaAtivo => ativo;

    // Cursor animado
    private GameObject cursorAnimadoObj;
    private RectTransform cursorRect;

    // ─── Singleton ───────────────────────────────────────────────
    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        if (Instancia == null) Instancia = this;
    }

    void OnDisable()
    {
        if (Instancia == this) Instancia = null;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
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

        if (ativo)
        {
            InteractionModeService.Request(
                InteractionOwner.Demolition,
                new InteractionPolicy
                {
                    bloqueiaSelecao = true,
                    bloqueiaOrdemMundo = true,
                    bloqueiaRotacaoCamera = true,
                    consomeLMB = true,
                    consomeRMB = true
                },
                "Demolição ativa");
        }
        else
        {
            InteractionModeService.Release(InteractionOwner.Demolition);
        }

        if (cursorAnimadoObj != null)
            cursorAnimadoObj.SetActive(ativo);

        if (!ativo)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        Debug.Log($"[ModoDemolicao] Modo: {(ativo ? "ATIVO" : "DESLIGADO")}");
    }

    // ─── Update principal ─────────────────────────────────────────
    void Update()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null) return;

        if (Construtor.EmModoConstrucaoAtivo)
        {
            if (ativo)
            {
                AlternarModo(false);
            }

            return;
        }

        // Atalho: T ou Delete
        if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Delete))
            AlternarModo(!ativo);

        if (!ativo) return;
        if (!InteractionModeService.IsActive(InteractionOwner.Demolition))
        {
            return;
        }

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

            Camera cameraPrincipal = Camera.main;
            if (cameraPrincipal == null)
            {
                return;
            }

            Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
            RaycastHit toque;
            int mascara = ~(1 << 2); // tudo menos IgnoreRaycast

            if (Physics.Raycast(raio, out toque, 2000f, mascara))
            {
                GameObject obj = toque.collider.gameObject;

                // Ignora terreno e água
                if (obj.GetComponent<Terrain>()  != null) return;
                if (obj.name.ToLower().Contains("terreno")) return;
                if (obj.layer == LayerMask.NameToLayer("Water")) return;

                SistemaDeDanos alvoComVida;
                if (TryEncontrarSistemaDeDanos(obj, out alvoComVida))
                {
                    ExplodirPorDano(alvoComVida);
                    return;
                }

                GameObject raiz;
                if (TryEncontrarAlvoDemolivel(obj, out raiz) || TryEncontrarAlvoUnidade(obj, out raiz))
                {
                    ExplodirSemSistemaDeDanos(raiz);
                    return;
                }
            }
        }
    }

    bool TryEncontrarSistemaDeDanos(GameObject obj, out SistemaDeDanos alvo)
    {
        alvo = null;
        if (obj == null)
        {
            return false;
        }

        alvo = obj.GetComponent<SistemaDeDanos>();
        if (alvo != null)
        {
            return true;
        }

        alvo = obj.GetComponentInParent<SistemaDeDanos>();
        if (alvo != null)
        {
            return true;
        }

        Transform t = obj.transform;
        while (t != null)
        {
            alvo = t.GetComponentInChildren<SistemaDeDanos>(true);
            if (alvo != null)
            {
                return true;
            }

            t = t.parent;
        }

        return false;
    }

    void ExplodirPorDano(SistemaDeDanos alvo)
    {
        if (alvo == null)
        {
            return;
        }

        float danoFatal = Mathf.Max(alvo.vidaAtual, alvo.vidaMaxima, 1f) + 999999f;
        Debug.Log($"[Demolição] Explodindo por T: {alvo.gameObject.name}");
        DiagnosticoDesempenhoJogo.RegistrarEvento("Demolition", "Explodir T: " + alvo.gameObject.name);
        alvo.ReceberDano(danoFatal);
    }

    void ExplodirSemSistemaDeDanos(GameObject raiz)
    {
        if (raiz == null)
        {
            return;
        }

        Debug.Log($"[Demolição] Explodindo instantaneamente: {raiz.name}");
        DiagnosticoDesempenhoJogo.RegistrarEvento("Demolition", "Explodir T sem SistemaDeDanos: " + raiz.name);
        if (GerenciadorFXGlobal.Instancia != null)
        {
            GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", raiz.transform.position, 1.5f);
        }

        Destroy(raiz);
    }

    bool TryEncontrarAlvoUnidade(GameObject obj, out GameObject raiz)
    {
        raiz = null;
        Transform t = obj != null ? obj.transform : null;
        while (t != null)
        {
            GameObject atual = t.gameObject;
            if (atual.GetComponent<IdentidadeUnidade>() != null
                || atual.GetComponent<ControleUnidade>() != null
                || atual.GetComponent<ControleNavioRealista>() != null
                || atual.GetComponent<ControleSubmarino>() != null
                || atual.GetComponent<ControleAviao>() != null
                || atual.GetComponent<ControleAviaoCaca>() != null
                || atual.GetComponent<Helicoptero>() != null)
            {
                raiz = atual;
                return true;
            }

            t = t.parent;
        }

        return false;
    }

    // ─── Encontra uma estrutura válida que pode ser demolida ──────
    bool TryEncontrarAlvoDemolivel(GameObject obj, out GameObject raiz)
    {
        raiz = null;
        Transform t = obj.transform;
        while (t != null)
        {
            GameObject atual = t.gameObject;
            if (EhEstruturaDemolivel(atual))
            {
                raiz = atual;
                return true;
            }

            t = t.parent;
        }

        return false;
    }

    bool EhEstruturaDemolivel(GameObject obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (obj.CompareTag("Imovel"))
        {
            return true;
        }

        if (obj.GetComponent<AtributosPredio>() != null || obj.GetComponent<Edificio>() != null)
        {
            return true;
        }

        if (obj.GetComponent<Estaleiro>() != null
            || obj.GetComponent<PierMarinha>() != null
            || obj.GetComponent<Fabrica>() != null
            || obj.GetComponent<GerenciadorAeroporto>() != null
            || obj.GetComponent<GerenciadorPortaAvioes>() != null)
        {
            return true;
        }

        return false;
    }
}
