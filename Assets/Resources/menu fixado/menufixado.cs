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
    private Label lblHappyVal, lblPopVal, lblDeadVal, lblJobsVal;
    private Label lblOilVal, lblOilBonus;
    private Label lblSteelVal, lblSteelBonus;
    private Label lblFoodVal, lblFoodBonus;
    private Label lblEnergyVal, lblEnergyBonus;
    private Label lblStorageVal;
    private Label lblMilitaryVal, lblMilitaryBonus;

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
        lblDeadVal = root.Q<Label>("lbl-dead-val");
        lblJobsVal = root.Q<Label>("lbl-jobs-val");
        
        lblOilVal = root.Q<Label>("lbl-oil-val");
        lblOilBonus = root.Q<Label>("lbl-oil-bonus");
        
        lblSteelVal = root.Q<Label>("lbl-steel-val");
        lblSteelBonus = root.Q<Label>("lbl-steel-bonus");
        
        lblFoodVal = root.Q<Label>("lbl-food-val");
        lblFoodBonus = root.Q<Label>("lbl-food-bonus");
        
        lblEnergyVal = root.Q<Label>("lbl-energy-val");
        lblEnergyBonus = root.Q<Label>("lbl-energy-bonus");
        
        lblStorageVal = root.Q<Label>("lbl-storage-val");
        
        lblMilitaryVal = root.Q<Label>("lbl-military-val");
        lblMilitaryBonus = root.Q<Label>("lbl-military-bonus");

        ConfigurarTooltips();
        uiPronta = true;
        CheckSceneVisibility(SceneManager.GetActiveScene());
        RegistrarEventos();
        UpdateUI();
    }

    private void ConfigurarTooltips()
    {
        if (lblMoneyVal != null) lblMoneyVal.tooltip = "Saldo atual do tesouro nacional.";
        if (lblCountryVal != null) lblCountryVal.tooltip = "Pais atualmente controlado.";
        if (lblCurrencyVal != null) lblCurrencyVal.tooltip = "Moeda nacional e relacao com a moeda lider.";
        if (lblGoldVal != null) lblGoldVal.tooltip = "Reserva de ouro estrategica.";
        if (lblHappyVal != null) lblHappyVal.tooltip = "Felicidade media da populacao.";
        if (lblPopVal != null) lblPopVal.tooltip = "Populacao civil atual versus capacidade habitacional.";
        if (lblDeadVal != null) lblDeadVal.tooltip = "Mortes acumuladas por guerra, fome e crises.";
        if (lblJobsVal != null) lblJobsVal.tooltip = "Empregos ocupados versus vagas disponiveis.";
        if (lblFoodVal != null) lblFoodVal.tooltip = "Estoque nacional de comida.";
        if (lblFoodBonus != null) lblFoodBonus.tooltip = "Saldo liquido de producao e consumo de comida.";
        if (lblEnergyVal != null) lblEnergyVal.tooltip = "Energia consumida versus energia disponivel.";
        if (lblEnergyBonus != null) lblEnergyBonus.tooltip = "Percentual de uso ou deficit energetico.";
        if (lblStorageVal != null) lblStorageVal.tooltip = "Ocupacao do armazem nacional.";
        if (lblMilitaryVal != null) lblMilitaryVal.tooltip = "Soldados ativos em servico.";
        if (lblMilitaryBonus != null) lblMilitaryBonus.tooltip = "Recrutaveis e reservistas disponiveis.";
    }

    private void RegistrarEventos()
    {
        DesregistrarEventos();
        GerenciadorTempo.GarantirInstancia();
        if (GerenciadorRecursos.Instancia != null) GerenciadorRecursos.Instancia.OnRecursosAtualizados += UpdateUI;
        if (CensoImperial.Instancia != null) CensoImperial.Instancia.OnCensoAtualizado += UpdateUI;
        if (GerenciadorArmazens.Instancia != null) GerenciadorArmazens.Instancia.OnArmazensAtualizados += UpdateUI;
        if (GerenciadorTempo.Instancia != null) GerenciadorTempo.Instancia.OnDataAlterada += UpdateUI;
    }

    private void DesregistrarEventos()
    {
        if (GerenciadorRecursos.Instancia != null) GerenciadorRecursos.Instancia.OnRecursosAtualizados -= UpdateUI;
        if (CensoImperial.Instancia != null) CensoImperial.Instancia.OnCensoAtualizado -= UpdateUI;
        if (GerenciadorArmazens.Instancia != null) GerenciadorArmazens.Instancia.OnArmazensAtualizados -= UpdateUI;
        if (GerenciadorTempo.Instancia != null) GerenciadorTempo.Instancia.OnDataAlterada -= UpdateUI;
    }

    private void Update()
    {
        if (!uiPronta || !_activeInScene) return;

        if (Time.frameCount % 30 == 0) RegistrarEventos();

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
        DadosEconomiaPais economia = null;
        if (SistemaEconomiaImoveis.Instancia != null)
        {
            economia = SistemaEconomiaImoveis.Instancia.ObterEconomia(SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.teamJogador : 1);
        }

        SetText(lblCountryVal, pais != null ? pais.nomePais.ToUpper() : "PAÍS");
        SetText(lblCurrencyVal, pais != null ? $"{pais.nomeMoeda.ToUpper()} {pais.cambioComLider:0.00}X" : "$");
        SetText(lblGoldVal, pais != null ? pais.reservaOuro.ToString("N0") : "0");
        AtualizarData();

        SetText(lblMoneyVal, ValoresDefinitivosHegemonia.FormatarDinheiro(r.dinheiro));
        AtualizarBonus(lblMoneyBonus, r.dinheiroPorSegundo, "/s", true);

        SetText(lblOilVal, r.petroleo.ToString("N0"));
        AtualizarBonus(lblOilBonus, r.petroleoPorSegundo, "/s");

        SetText(lblSteelVal, r.aco.ToString("N0"));
        AtualizarBonus(lblSteelBonus, r.acoPorSegundo, "/s");

        SetText(lblFoodVal, r.comida.ToString("N0"));
        if (economia != null)
        {
            float comidaLiquid = economia.comidaProduzida - economia.comidaConsumida;
            AtualizarBonus(lblFoodBonus, comidaLiquid, "/s");
        }
        else
        {
            AtualizarBonus(lblFoodBonus, 0f, "/s");
        }
        
        SetText(lblPopVal, pais != null ? $"{pais.populacaoCivil:N0}/{pais.populacaoMaxima:N0}" : $"{r.populacaoAtual:N0}/{r.populacaoMaxima:N0}");
        SetText(lblDeadVal, pais != null ? pais.mortosAcumulados.ToString("N0") : "0");
        SetColor(lblDeadVal, pais != null && pais.mortosAcumulados > 0 ? new Color(0.90f, 0.30f, 0.30f) : Color.white);
        if (lblPopVal != null && pais != null)
        {
            lblPopVal.tooltip = "Populacao civil: " + pais.populacaoCivil.ToString("N0")
                + "\nPopulacao total: " + pais.populacao.ToString("N0")
                + "\nCapacidade: " + pais.populacaoMaxima.ToString("N0");
        }
        if (lblDeadVal != null && pais != null)
        {
            lblDeadVal.tooltip = "Mortes acumuladas: " + pais.mortosAcumulados.ToString("N0")
                + "\nMortalidade atual: " + pais.mortalidade.ToString("0.0");
        }
        if (lblHappyVal != null && pais != null)
        {
            SetText(lblHappyVal, $"{pais.felicidade:F0}%");
            SetColor(lblHappyVal, pais.felicidade >= 70 ? Color.green : (pais.felicidade >= 40 ? Color.yellow : Color.red));
        }

        if (lblJobsVal != null && economia != null)
        {
            int disponiveis = Mathf.Max(0, economia.empregosDisponiveis);
            int ocupados = Mathf.Max(0, economia.empregosOcupados);
            lblJobsVal.text = $"{ocupados:N0}/{disponiveis:N0}";
            SetColor(lblJobsVal, economia.deficitEmprego > 0 ? Color.yellow : Color.white);
            lblJobsVal.tooltip = "Empregos ocupados: " + ocupados.ToString("N0")
                + "\nVagas disponiveis: " + disponiveis.ToString("N0")
                + "\nDeficit: " + economia.deficitEmprego.ToString("0");
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
            SetText(lblStorageVal, oc >= 90f ? $"{oc:F0}% CHEIO" : $"{oc:F0}% EST");
            SetColor(lblStorageVal, oc >= 90f ? Color.red : (oc >= 75f ? Color.yellow : Color.white));
            lblStorageVal.tooltip = "Ocupacao do armazem nacional: " + oc.ToString("0") + "%";
        }
        else
        {
            SetText(lblStorageVal, "OK EST");
            SetColor(lblStorageVal, Color.white);
        }

        if (CensoImperial.Instancia != null && pais != null)
        {
            SetText(lblMilitaryVal, pais.populacaoMilitarAtiva.ToString("N0"));
            SetText(lblMilitaryBonus, $"A {pais.alistaveis:N0} | R {pais.reservistas:N0}");
            lblMilitaryVal.tooltip = "Soldados ativos: " + pais.populacaoMilitarAtiva.ToString("N0");
            lblMilitaryBonus.tooltip = "Recrutaveis: " + pais.alistaveis.ToString("N0")
                + "\nReservistas: " + pais.reservistas.ToString("N0");
        }
    }

    private void AtualizarData()
    {
        if (lblDateVal == null)
        {
            return;
        }

        int dias = GerenciadorTempo.Instancia != null ? Mathf.Max(0, GerenciadorTempo.Instancia.totalDias - 1) : 0;
        System.DateTime data = new System.DateTime(2000, 1, 1).AddDays(dias);
        lblDateVal.text = data.ToString("dd/MM/yyyy");
    }

    private void SetText(Label lbl, string text)
    {
        if (lbl != null) lbl.text = text;
    }

    private void SetColor(Label lbl, Color color)
    {
        if (lbl != null) lbl.style.color = color;
    }

    private void AtualizarBonus(Label lbl, float valor, string sufixo, bool monetario = false)
    {
        if (lbl == null) return;
        string valorFormatado = monetario
            ? ValoresDefinitivosHegemonia.FormatarDinheiro(Mathf.RoundToInt(Mathf.Abs(valor)))
            : Mathf.Abs(valor).ToString("N0");
        lbl.text = valor >= 0 ? $"+{valorFormatado}{sufixo}" : $"-{valorFormatado}{sufixo}";
        lbl.style.color = valor >= 0 ? new Color(0.13f, 0.77f, 0.36f) : Color.red;
    }
}
