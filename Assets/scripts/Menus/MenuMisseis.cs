using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MenuMisseis : MonoBehaviour
{
    [Header("Arsenal (Mísseis Disponíveis)")]
    public List<GameObject> prefabsMisseis = new List<GameObject>(); // Arraste os prefabs dos mísseis aqui

    [Header("Configuração UI")]
    public float larguraBotao = 160f;
    public float alturaBotao = 40f;
    public Color corFundoMenu = new Color(0, 0, 0, 0.8f);

    // Variáveis Internas
    private GameObject painelMenu;
    private SiloNuclear siloSlecionado;
    private GameObject misselParaLancar; // Qual míssil foi escolhido
    private bool modoMira = false; // Se estamos esperando clicar no mapa
    public static bool EstaAberto = false;
    private Camera cameraPrincipal;
    private MenuConstrucao menuConstrucaoCache;
    private MenuPier menuPierCache;
    private MenuGoverno menuGovernoCache;

    void Start()
    {
        cameraPrincipal = Camera.main;
        CriarInterface();
        painelMenu.SetActive(false);
        EstaAberto = false;
    }

    void Update()
    {
        if (QuartelMenuUIController.EntradaGlobalBloqueada)
        {
            return;
        }

        AtualizarModoManualDeMira();

        // MODO MIRA: Se escolheu o míssil, espera clicar no chão ou apertar SPACE
        if (modoMira && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            LancarNoAlvo();
        }

        // Cancelar com Botão Direito ou ESC
        if (modoMira && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CancelarLancamento();
        }
    }

    // --- LÓGICA DE TIRO ---
    void LancarNoAlvo()
    {
        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
        if (cameraPrincipal == null) return;

        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        RaycastHit toque;

        // Tenta acertar o chão (use a Layer que você já configurou ou Default)
        if (Physics.Raycast(raio, out toque, 5000f))
        {
            if (siloSlecionado != null && misselParaLancar != null)
            {
                siloSlecionado.DispararMissel(misselParaLancar, toque.point);
                CancelarLancamento(); // Reseta tudo após o tiro
            }
        }
    }

    // --- LÓGICA DO MENU ---
    public void AbrirMenuParaSilo(SiloNuclear silo)
    {
        if (QuartelMenuUIController.EntradaGlobalBloqueada)
        {
            return;
        }

        if (modoMira) return; // Não abre menu se estiver mirando

        siloSlecionado = silo;
        
        // Posiciona o menu perto do mouse
        painelMenu.transform.position = Input.mousePosition + new Vector3(20, 20, 0);
        painelMenu.SetActive(true);
        EstaAberto = true;

        // Fecha os outros
        if (menuConstrucaoCache == null) menuConstrucaoCache = Object.FindFirstObjectByType<MenuConstrucao>();
        if (menuConstrucaoCache != null && MenuConstrucao.EstaAberto) menuConstrucaoCache.AlternarMenu(false);

        if (menuPierCache == null) menuPierCache = Object.FindFirstObjectByType<MenuPier>();
        if (menuPierCache != null && MenuPier.EstaAberto) menuPierCache.FecharMenu();

        if (menuGovernoCache == null) menuGovernoCache = Object.FindFirstObjectByType<MenuGoverno>();
        if (menuGovernoCache != null && MenuGoverno.EstaAberto) menuGovernoCache.AlternarMenu(false);
    }

    void SelecionarMissel(GameObject prefab)
    {
        misselParaLancar = prefab;
        painelMenu.SetActive(false); // Fecha o menu
        EstaAberto = false;
        modoMira = true;
        AtualizarModoManualDeMira();
        Debug.Log("Comandante, selecione o alvo no mapa!");
        // Dica: Aqui você mudaria o cursor para uma mira vermelha
    }

    public void CancelarLancamento()
    {
        modoMira = false;
        siloSlecionado = null;
        misselParaLancar = null;
        painelMenu.SetActive(false);
        EstaAberto = false;
        AtualizarModoManualDeMira();
    }

    void OnDisable()
    {
        InteractionModeService.Release(this, InteractionOwner.ManualFire);
    }

    void AtualizarModoManualDeMira()
    {
        if (modoMira)
        {
            if (!InteractionModeService.IsActive(this, InteractionOwner.ManualFire))
            {
                InteractionModeService.Request(
                    this,
                    InteractionOwner.ManualFire,
                    new InteractionPolicy
                    {
                        bloqueiaSelecao = true,
                        bloqueiaOrdemMundo = true,
                        bloqueiaRotacaoCamera = true,
                        consomeLMB = true,
                        consomeRMB = true
                    },
                    "Mira de míssil ativa");
            }
        }
        else
        {
            InteractionModeService.Release(this, InteractionOwner.ManualFire);
        }
    }

    // --- CRIAÇÃO DA UI VIA SCRIPT (Igual seu Construtor) ---
    void CriarInterface()
    {
        // Cria Canvas se não existir
        GameObject canvasObj = GameObject.Find("Canvas_Interface");
        if (canvasObj == null) canvasObj = new GameObject("Canvas_Interface", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Cria o Painel
        painelMenu = new GameObject("Painel_Misseis", typeof(RectTransform), typeof(Image));
        painelMenu.transform.SetParent(canvasObj.transform);
        painelMenu.GetComponent<Image>().color = corFundoMenu;
        
        RectTransform rt = painelMenu.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(larguraBotao + 20, (prefabsMisseis.Count * alturaBotao) + 20);
        rt.pivot = new Vector2(0, 0); // Pivô no canto inferior esquerdo

        // Cria os Botões
        for (int i = 0; i < prefabsMisseis.Count; i++)
        {
            GameObject prefabRef = prefabsMisseis[i]; // Referência local para o botão
            if (prefabRef == null) continue;
            GameObject btnObj = new GameObject("Botao_" + prefabRef.name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(painelMenu.transform);
            
            // Estilo do Botão
            btnObj.GetComponent<Image>().color = Color.red;
            RectTransform btnRT = btnObj.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(larguraBotao, alturaBotao);
            btnRT.anchoredPosition = new Vector2(0, -i * (alturaBotao + 5)); // Empilha para baixo
            // Centraliza no painel
            btnRT.anchorMin = new Vector2(0.5f, 1);
            btnRT.anchorMax = new Vector2(0.5f, 1);
            btnRT.pivot = new Vector2(0.5f, 1);
            btnRT.anchoredPosition = new Vector2(0, -(i * (alturaBotao + 5)) - 10);

            // Texto do Botão
            GameObject txtObj = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform);
            Text txt = txtObj.GetComponent<Text>();
            txt.text = prefabRef.name; // Nome do Prefab
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            RectTransform txtRT = txtObj.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one; txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;

            // Ação do Clique
            btnObj.GetComponent<Button>().onClick.AddListener(() => SelecionarMissel(prefabRef));
        }
    }
}
