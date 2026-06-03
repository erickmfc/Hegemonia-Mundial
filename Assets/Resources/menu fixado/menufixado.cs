using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(UIDocument))]
public class MenuFixadoController : MonoBehaviour
{
    private static MenuFixadoController _instance;

    private UIDocument uiDocument;
    private VisualElement root;
    private bool uiPronta = false;

    // Labels
    private Label lblCountryVal, lblDateVal;
    private Label lblMoneyVal, lblMoneyBonus, lblCurrencyVal, lblGoldVal;
    private Label lblHappyVal, lblPopVal;
    private Label lblOilVal, lblOilBonus;
    private Label lblSteelVal, lblSteelBonus;
    private Label lblFoodVal;
    private Label lblEnergyVal, lblEnergyBonus;
    private Label lblStorageVal;
    private Label lblMilitaryVal, lblMilitaryBonus;

    // Data
    private float timeAccumulator = 0f;
    private int elapsedDays = 0;
    private bool _activeInScene = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (FindFirstObjectByType<MenuFixadoController>() != null) return;

        VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("menu fixado/menufixado");
        if (uxml == null) return;

        PanelSettings ps = Resources.Load<PanelSettings>("PanelSettings") ?? ScriptableObject.CreateInstance<PanelSettings>();
        ps.sortingOrder = 100;

        GameObject go = new GameObject("[HUD_MenuFixado]");
        DontDestroyOnLoad(go);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        doc.visualTreeAsset = uxml;
        go.AddComponent<MenuFixadoController>();
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

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckSceneVisibility(SceneManager.GetActiveScene());
        LimparDuplicadosNaCena();
        StartCoroutine(SetupUI());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        DesregistrarEventos();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckSceneVisibility(scene);
        LimparDuplicadosNaCena();
        if (uiPronta)
        {
            DesregistrarEventos();
            RegistrarEventos();
            UpdateUI();
        }
    }

    private void LimparDuplicadosNaCena()
    {
        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var doc in docs)
        {
            if (doc != uiDocument && doc.visualTreeAsset != null && doc.visualTreeAsset.name.ToLower().Contains("menufixado"))
            {
                Destroy(doc.gameObject);
            }
        }
    }

    private void CheckSceneVisibility(Scene scene)
    {
        string nome = scene.name.ToLower();
        _activeInScene = !(nome.Contains("menu") && !nome.Contains("game") && !nome.Contains("fase") && !nome.Contains("mapa"));
        if (root != null) root.style.display = _activeInScene ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private IEnumerator SetupUI()
    {
        yield return new WaitUntil(() => uiDocument.rootVisualElement != null);
        yield return null;

        root = uiDocument.rootVisualElement;

        StyleSheet uss = Resources.Load<StyleSheet>("menu fixado/menufixado");
        if (uss != null) root.styleSheets.Add(uss);

        lblCountryVal = root.Q<Label>("lbl-country-val");
        lblDateVal = root.Q<Label>("lbl-date-val");
        
        lblMoneyVal = root.Q<Label>("lbl-money-val");
        lblMoneyBonus = root.Q<Label>("lbl-money-bonus");
        lblCurrencyVal = root.Q<Label>("lbl-currency-val");
        lblGoldVal = root.Q<Label>("lbl-gold-val");
        
        lblHappyVal = root.Q<Label>("lbl-happy-val");
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

        uiPronta = true;
        CheckSceneVisibility(SceneManager.GetActiveScene());
        RegistrarEventos();
        UpdateUI();
    }

    private void RegistrarEventos()
    {
        DesregistrarEventos();
        if (GerenciadorRecursos.Instancia != null) GerenciadorRecursos.Instancia.OnRecursosAtualizados += UpdateUI;
        if (CensoImperial.Instancia != null) CensoImperial.Instancia.OnCensoAtualizado += UpdateUI;
        if (GerenciadorArmazens.Instancia != null) GerenciadorArmazens.Instancia.OnArmazensAtualizados += UpdateUI;
    }

    private void DesregistrarEventos()
    {
        if (GerenciadorRecursos.Instancia != null) GerenciadorRecursos.Instancia.OnRecursosAtualizados -= UpdateUI;
        if (CensoImperial.Instancia != null) CensoImperial.Instancia.OnCensoAtualizado -= UpdateUI;
        if (GerenciadorArmazens.Instancia != null) GerenciadorArmazens.Instancia.OnArmazensAtualizados -= UpdateUI;
    }

    private void Update()
    {
        if (!uiPronta || !_activeInScene) return;

        if (Time.frameCount % 30 == 0) RegistrarEventos();

        timeAccumulator += Time.deltaTime;
        if (timeAccumulator >= 120f)
        {
            int dias = Mathf.FloorToInt(timeAccumulator / 120f);
            elapsedDays += dias;
            timeAccumulator -= dias * 120f;
            if (lblDateVal != null)
            {
                System.DateTime data = new System.DateTime(2000, 1, 1).AddDays(elapsedDays);
                lblDateVal.text = data.ToString("dd/MM/yyyy");
            }
        }

        if (Time.frameCount % 60 == 0) UpdateUI();
    }

    private void UpdateUI()
    {
        if (!uiPronta) return;

        var r = GerenciadorRecursos.Instancia;
        if (r == null) return;

        SistemaGovernoMundial.GarantirInstancia();
        if (SistemaGovernoMundial.Instancia != null)
        {
            SistemaGovernoMundial.Instancia.SincronizarJogador();
        }

        DadosPaisGoverno pais = SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(SistemaGovernoMundial.Instancia.teamJogador) : null;

        SetText(lblCountryVal, pais != null ? pais.nomePais.ToUpper() : "PAÍS");
        SetText(lblCurrencyVal, pais != null ? $"{pais.nomeMoeda.ToUpper()} {pais.cambioComLider:0.00}X" : "$");
        SetText(lblGoldVal, pais != null ? pais.reservaOuro.ToString("N0") : "0");

        SetText(lblMoneyVal, r.dinheiro.ToString("N0"));
        AtualizarBonus(lblMoneyBonus, r.dinheiroPorSegundo, "/s");

        SetText(lblOilVal, r.petroleo.ToString("N0"));
        AtualizarBonus(lblOilBonus, r.petroleoPorSegundo, "/s");

        SetText(lblSteelVal, r.aco.ToString("N0"));
        AtualizarBonus(lblSteelBonus, r.acoPorSegundo, "/s");

        SetText(lblFoodVal, r.comida.ToString("N0"));
        
        SetText(lblPopVal, pais != null ? $"{pais.populacaoCivil:N0}/{pais.populacaoMaxima:N0}" : $"{r.populacaoAtual:N0}/{r.populacaoMaxima:N0}");
        if (lblHappyVal != null && pais != null)
        {
            SetText(lblHappyVal, $"{pais.felicidade:F0}%");
            SetColor(lblHappyVal, pais.felicidade >= 70 ? Color.green : (pais.felicidade >= 40 ? Color.yellow : Color.red));
        }

        float consumida = pais?.energiaConsumida ?? 0f;
        float produzida = pais != null ? Mathf.Max(pais.energiaProduzida, r.energia) : Mathf.Max(0f, r.energia);

        SetText(lblEnergyVal, $"{consumida:0}/{produzida:0}");

        if (produzida > 0.01f)
        {
            float uso = Mathf.Clamp((consumida / produzida) * 100f, 0f, 999f);
            SetColor(lblEnergyVal, uso > 100f ? Color.red : (uso >= 90f ? Color.yellow : Color.white));
            SetText(lblEnergyBonus, uso > 100f ? "DÉFICIT" : $"{uso:0}% USO");
            SetColor(lblEnergyBonus, uso > 100f ? Color.red : (uso >= 90f ? Color.yellow : Color.green));
        }
        else
        {
            SetColor(lblEnergyVal, consumida > 0 ? Color.red : Color.white);
            SetText(lblEnergyBonus, consumida > 0 ? "DÉFICIT" : "+0/s");
            SetColor(lblEnergyBonus, consumida > 0 ? Color.red : Color.green);
        }

        if (GerenciadorArmazens.Instancia?.armazemRecursos != null)
        {
            float oc = GerenciadorArmazens.Instancia.armazemRecursos.PercentualOcupacao();
            SetText(lblStorageVal, oc >= 90f ? $"{oc:F0}% CHEIO" : $"{oc:F0}%");
            SetColor(lblStorageVal, oc >= 90f ? Color.red : (oc >= 75f ? Color.yellow : Color.white));
        }
        else
        {
            SetText(lblStorageVal, "OK");
            SetColor(lblStorageVal, Color.white);
        }

        if (CensoImperial.Instancia != null && pais != null)
        {
            SetText(lblMilitaryVal, pais.populacaoMilitarAtiva.ToString("N0"));
            SetText(lblMilitaryBonus, $"+{pais.alistaveis:N0} RES");
        }
    }

    private void SetText(Label lbl, string text)
    {
        if (lbl != null) lbl.text = text;
    }

    private void SetColor(Label lbl, Color color)
    {
        if (lbl != null) lbl.style.color = color;
    }

    private void AtualizarBonus(Label lbl, float valor, string sufixo)
    {
        if (lbl == null) return;
        lbl.text = valor >= 0 ? $"+{valor:N0}{sufixo}" : $"{valor:N0}{sufixo}";
        lbl.style.color = valor >= 0 ? new Color(0.13f, 0.77f, 0.36f) : Color.red;
    }
}
