using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(UIDocument))]
public class MenuFixadoController : MonoBehaviour
{
    private static MenuFixadoController _instance;

    private UIDocument uiDocument;
    private VisualElement root;

    // Resource UI Labels
    private Label lblCountryVal;
    private Label lblMoneyVal;
    private Label lblMoneyBonus;
    private Label lblCurrencyVal;
    private Label lblGoldVal;
    private Label lblPopVal;
    private Label lblOilVal;
    private Label lblOilBonus;
    private Label lblSteelVal;
    private Label lblSteelBonus;
    private Label lblFoodVal;
    private Label lblEnergyVal;
    private Label lblEnergyBonus;
    private Label lblStorageVal;
    private Label lblMilitaryVal;
    private Label lblMilitaryBonus;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoaded()
    {
        // Verifica se já existe um HUD do Menu Fixado na cena (usando FindObjectOfType para compatibilidade)
        if (FindObjectOfType<MenuFixadoController>() != null) return;

        // Carrega o layout UXML dos Resources
        VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("menu fixado/menufixado");
        if (uxml == null)
        {
            Debug.LogWarning("[MenuFixado] UXML 'menufixado' não encontrado na pasta Resources. Certifique-se de que os arquivos de UI estejam em Assets/Resources/.");
            return;
        }

        // Cria o GameObject e configura o UIDocument
        GameObject hudGo = new GameObject("[HUD_MenuFixado]");
        DontDestroyOnLoad(hudGo);
        
        UIDocument doc = hudGo.AddComponent<UIDocument>();
        doc.visualTreeAsset = uxml;
        
        // Carrega e adiciona o Stylesheet USS de forma programática
        StyleSheet uss = Resources.Load<StyleSheet>("menu fixado/menufixado");
        if (uss != null)
        {
            doc.rootVisualElement.styleSheets.Add(uss);
        }

        // Aplica configurações de renderização padrão
        PanelSettings panelSettings = Resources.Load<PanelSettings>("PanelSettings");
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.sortingOrder = 100; // Menu fixado por cima
        }
        doc.panelSettings = panelSettings;

        hudGo.AddComponent<MenuFixadoController>();
        Debug.Log("[MenuFixado] HUD auto-inicializado com sucesso!");
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (uiDocument == null) return;
        
        root = uiDocument.rootVisualElement;
        if (root == null) return;

        // Bind labels
        lblCountryVal = root.Q<Label>("lbl-country-val");
        lblMoneyVal = root.Q<Label>("lbl-money-val");
        lblMoneyBonus = root.Q<Label>("lbl-money-bonus");
        lblCurrencyVal = root.Q<Label>("lbl-currency-val");
        lblGoldVal = root.Q<Label>("lbl-gold-val");
        lblPopVal = root.Q<Label>("lbl-pop-val");
        lblOilVal = root.Q<Label>("lbl-oil-val");
        lblOilBonus = root.Q<Label>("lbl-oil-bonus");
        lblSteelVal = root.Q<Label>("lbl-steel-val");
        lblSteelBonus = root.Q<Label>("lbl-steel-bonus");
        lblFoodVal = root.Q<Label>("lbl-food-val");
        lblEnergyVal = root.Q<Label>("lbl-energy-val");
        lblEnergyBonus = root.Q<Label>("lbl-energy-bonus");
        lblStorageVal = root.Q<Label>("lbl-storage-val");
        lblMilitaryVal = root.Q<Label>("lbl-military-val");
        lblMilitaryBonus = root.Q<Label>("lbl-military-bonus");

        // Escuta eventos dos gerenciadores de recursos, censo militar e armazéns
        HookEvents(true);

        UpdateUI();
        
        // Registra mudança de cena para desativar em cenas de Menu Principal se houver
        SceneManager.sceneLoaded += OnSceneChanged;
    }

    private void OnDisable()
    {
        HookEvents(false);
        SceneManager.sceneLoaded -= OnSceneChanged;
    }

    private void HookEvents(bool register)
    {
        if (register)
        {
            if (GerenciadorRecursos.Instancia != null)
                GerenciadorRecursos.Instancia.OnRecursosAtualizados += UpdateUI;
            
            if (CensoImperial.Instancia != null)
                CensoImperial.Instancia.OnCensoAtualizado += UpdateUI;

            if (GerenciadorArmazens.Instancia != null)
                GerenciadorArmazens.Instancia.OnArmazensAtualizados += UpdateUI;
        }
        else
        {
            if (GerenciadorRecursos.Instancia != null)
                GerenciadorRecursos.Instancia.OnRecursosAtualizados -= UpdateUI;
            
            if (CensoImperial.Instancia != null)
                CensoImperial.Instancia.OnCensoAtualizado -= UpdateUI;

            if (GerenciadorArmazens.Instancia != null)
                GerenciadorArmazens.Instancia.OnArmazensAtualizados -= UpdateUI;
        }
    }

    private void Start()
    {
        // Tenta registrar novamente no Start caso os singletons não estivessem prontos no OnEnable
        HookEvents(false);
        HookEvents(true);
        UpdateUI();
    }

    private void Update()
    {
        // Fallback para caso algum singleton seja instanciado tardiamente
        if (GerenciadorRecursos.Instancia != null && CensoImperial.Instancia != null)
        {
            // Opcional: atualização a cada frame se necessário, ou mantém somente orientada a eventos.
            // Para garantir sincronia absoluta com o Painel Lateral, podemos atualizar aqui periodicamente.
        }
    }

    private void OnSceneChanged(Scene scene, LoadSceneMode mode)
    {
        string sceneName = scene.name.ToLower();
        if (sceneName.Contains("menu") && !sceneName.Contains("game") && !sceneName.Contains("fase") && !sceneName.Contains("mapa"))
        {
            if (uiDocument != null && uiDocument.rootVisualElement != null) 
                uiDocument.rootVisualElement.style.display = DisplayStyle.None;
        }
        else
        {
            if (uiDocument != null && uiDocument.rootVisualElement != null) 
                uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (root == null) return;

        if (GerenciadorRecursos.Instancia == null)
        {
            // Estado de simulação/mock offline (semelhante ao PainelRecursos.cs)
            if (lblCountryVal != null) lblCountryVal.text = "REPÚBLICA ATLAS";
            if (lblMoneyVal != null) lblMoneyVal.text = "10.320";
            if (lblMoneyBonus != null) lblMoneyBonus.text = "+11/s";
            if (lblCurrencyVal != null) lblCurrencyVal.text = "ATLAS 1.00X";
            if (lblGoldVal != null) lblGoldVal.text = "500";
            if (lblPopVal != null) lblPopVal.text = "12/200";
            if (lblOilVal != null) lblOilVal.text = "500";
            if (lblOilBonus != null) lblOilBonus.text = "+0/s";
            if (lblSteelVal != null) lblSteelVal.text = "325";
            if (lblSteelBonus != null) lblSteelBonus.text = "+5/s";
            if (lblFoodVal != null) lblFoodVal.text = "240";
            if (lblEnergyVal != null) lblEnergyVal.text = "0% USO";
            if (lblEnergyBonus != null) lblEnergyBonus.text = "0/0";
            if (lblStorageVal != null) lblStorageVal.text = "42%";
            if (lblMilitaryVal != null) lblMilitaryVal.text = "0";
            if (lblMilitaryBonus != null) lblMilitaryBonus.text = "+0";
            return;
        }

        var r = GerenciadorRecursos.Instancia;

        // País, Moeda e Ouro do Governo Mundial
        SistemaGovernoMundial.GarantirInstancia();
        DadosPaisGoverno paisJogador = SistemaGovernoMundial.Instancia != null
            ? SistemaGovernoMundial.Instancia.ObterPais(SistemaGovernoMundial.Instancia.teamJogador)
            : null;

        if (lblCountryVal != null)
            lblCountryVal.text = paisJogador != null ? paisJogador.nomePais.ToUpper() : "PAÍS 1";

        if (lblCurrencyVal != null)
            lblCurrencyVal.text = paisJogador != null ? $"{paisJogador.nomeMoeda.ToUpper()} {paisJogador.cambioComLider:0.00}X" : "$";

        if (lblGoldVal != null)
            lblGoldVal.text = paisJogador != null ? paisJogador.reservaOuro.ToString("N0") : "0";

        // Dinheiro
        if (lblMoneyVal != null) lblMoneyVal.text = r.dinheiro.ToString("N0");
        if (lblMoneyBonus != null)
        {
            lblMoneyBonus.text = r.dinheiroPorSegundo >= 0 
                ? $"+{r.dinheiroPorSegundo:N0}/s" 
                : $"{r.dinheiroPorSegundo:N0}/s";
            lblMoneyBonus.style.color = r.dinheiroPorSegundo >= 0 ? new Color(0.13f, 0.77f, 0.36f) : new Color(0.9f, 0.2f, 0.2f);
        }

        // Petróleo
        if (lblOilVal != null) lblOilVal.text = r.petroleo.ToString("N0");
        if (lblOilBonus != null)
        {
            lblOilBonus.text = r.petroleoPorSegundo >= 0 
                ? $"+{r.petroleoPorSegundo:N0}/s" 
                : $"{r.petroleoPorSegundo:N0}/s";
            lblOilBonus.style.color = r.petroleoPorSegundo >= 0 ? new Color(0.13f, 0.77f, 0.36f) : new Color(0.9f, 0.2f, 0.2f);
        }

        // Aço
        if (lblSteelVal != null) lblSteelVal.text = r.aco.ToString("N0");
        if (lblSteelBonus != null)
        {
            lblSteelBonus.text = r.acoPorSegundo >= 0 
                ? $"+{r.acoPorSegundo:N0}/s" 
                : $"{r.acoPorSegundo:N0}/s";
            lblSteelBonus.style.color = r.acoPorSegundo >= 0 ? new Color(0.13f, 0.77f, 0.36f) : new Color(0.9f, 0.2f, 0.2f);
        }

        // Comida
        if (lblFoodVal != null) lblFoodVal.text = r.comida.ToString("N0");

        // População
        if (lblPopVal != null) lblPopVal.text = $"{r.populacaoAtual:N0}/{r.populacaoMaxima:N0}";

        // Energia
        float energiaConsumida = paisJogador != null ? paisJogador.energiaConsumida : 0f;
        float energiaProduzida = paisJogador != null ? paisJogador.energiaProduzida : Mathf.Max(0f, r.energia);
        if (lblEnergyVal != null)
        {
            if (energiaProduzida > 0.01f)
            {
                float uso = Mathf.Clamp((energiaConsumida / energiaProduzida) * 100f, 0f, 999f);
                lblEnergyVal.text = uso >= 100f ? "DÉFICIT" : $"{uso:0}% USO";
                lblEnergyVal.style.color = uso >= 100f ? new Color(0.9f, 0.2f, 0.2f) : uso >= 90f ? new Color(1f, 0.7f, 0.2f) : new Color(0.95f, 0.95f, 0.95f);
            }
            else
            {
                lblEnergyVal.text = r.energia.ToString("N0");
                lblEnergyVal.style.color = new Color(0.95f, 0.95f, 0.95f);
            }
        }
        if (lblEnergyBonus != null)
        {
            if (energiaProduzida > 0.01f)
            {
                lblEnergyBonus.text = $"{energiaConsumida:0}/{energiaProduzida:0}";
                float uso = (energiaConsumida / energiaProduzida) * 100f;
                lblEnergyBonus.style.color = uso >= 100f ? new Color(0.9f, 0.2f, 0.2f) : uso >= 90f ? new Color(1f, 0.7f, 0.2f) : new Color(0.13f, 0.77f, 0.36f);
            }
            else
            {
                lblEnergyBonus.text = "+0/s";
                lblEnergyBonus.style.color = new Color(0.13f, 0.77f, 0.36f);
            }
        }

        // Armazém / Estoque
        if (lblStorageVal != null)
        {
            if (GerenciadorArmazens.Instancia != null && GerenciadorArmazens.Instancia.armazemRecursos != null)
            {
                float ocupacaoArmazem = GerenciadorArmazens.Instancia.armazemRecursos.PercentualOcupacao();
                lblStorageVal.text = ocupacaoArmazem >= 90f ? $"{ocupacaoArmazem:F0}% CHEIO" : $"{ocupacaoArmazem:F0}%";
                lblStorageVal.style.color = ocupacaoArmazem >= 90f ? new Color(0.9f, 0.2f, 0.2f) : ocupacaoArmazem >= 75f ? new Color(1f, 0.7f, 0.2f) : new Color(0.95f, 0.95f, 0.95f);
            }
            else
            {
                lblStorageVal.text = "OK";
                lblStorageVal.style.color = new Color(0.95f, 0.95f, 0.95f);
            }
        }

        // Militares (Censo)
        if (CensoImperial.Instancia != null)
        {
            if (lblMilitaryVal != null) lblMilitaryVal.text = CensoImperial.Instancia.totalUnidades.ToString("N0");
            int militarPesado = CensoImperial.Instancia.veiculos + CensoImperial.Instancia.naval + CensoImperial.Instancia.aereo;
            if (lblMilitaryBonus != null) lblMilitaryBonus.text = "+" + militarPesado.ToString("N0");
        }
    }
}
