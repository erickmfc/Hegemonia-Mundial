using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Reflection;

public class PortaAvioesUIController : MonoBehaviour
{
    private UIDocument uiDocument;
    private GerenciadorPortaAvioes gerenciador;

    // Elementos da UI
    private VisualElement bottomMenu;
    private ScrollView sectionsContainer;
    private Label lblShipName;
    private Label lblShipDesignation;
    
    // Bottom Menu elements
    private Label bmIcon, bmTitle, bmStatus, bmHpText, bmFuelText;
    private VisualElement bmHpBar, bmFuelBar, bmCommands;
    private VisualElement toastContainer;

    private int _selectedUnitId = -1;
    private ControleAviao _selectedPlane = null;
    private Helicoptero _selectedHeli = null;

    // Assinaturas de Estado para evitar repinturas constantes (flickering/piscado)
    private string _lastStateSignature = "";
    private string _lastSelectedUnitStateSignature = "";

    void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        gerenciador = GetComponent<GerenciadorPortaAvioes>();

        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var root = uiDocument.rootVisualElement;
        
        // Esconder toda a UI no inicio (aberta pelo 'O')
        root.style.display = DisplayStyle.None;

        sectionsContainer = root.Q<ScrollView>("sectionsContainer");
        bottomMenu = root.Q<VisualElement>("bottomMenu");
        lblShipName = root.Q<Label>("lblShipName");
        lblShipDesignation = root.Q<Label>("lblShipDesignation");

        // Bloqueia cliques no painel (não passa para objetos do jogo atrás)
        var carrierPanel = root.Q<VisualElement>("carrierPanel");
        if (carrierPanel != null) carrierPanel.pickingMode = PickingMode.Position;
        if (bottomMenu != null)  bottomMenu.pickingMode  = PickingMode.Position;
        
        bmIcon = root.Q<Label>("bmIcon");
        bmTitle = root.Q<Label>("bmTitle");
        bmStatus = root.Q<Label>("bmStatus");
        bmHpText = root.Q<Label>("bmHpText");
        bmFuelText = root.Q<Label>("bmFuelText");
        bmHpBar = root.Q<VisualElement>("bmHpBar");
        bmFuelBar = root.Q<VisualElement>("bmFuelBar");
        bmCommands = root.Q<VisualElement>("bmCommands");
        toastContainer = root.Q<VisualElement>("toastContainer");

        root.Q<Button>("btnClosePanel").clicked += () => ToggleMenu(false);
        root.Q<Button>("btnCloseBottom").clicked += CloseBottomMenu;

        if (lblShipName != null && gerenciador != null)
        {
            lblShipName.text = CleanName(gerenciador.gameObject.name).ToUpper();
        }
    }

    void Update()
    {
        if (uiDocument == null || uiDocument.rootVisualElement.style.display == DisplayStyle.None)
            return;

        // Atualiza a cada frame (ideal seria 1-2x por seg)
        if (Time.frameCount % 15 == 0)
        {
            var avioesNoAr = GetFieldValue<List<ControleAviao>>(gerenciador, "_avioesProximosNoAr");
            var helisNoAr = GetFieldValue<List<Helicoptero>>(gerenciador, "_helicopterosProximosNoAr");
            var patio = GetFieldValue<List<ControleAviao>>(gerenciador, "avioesNoPatio", typeof(GerenciadorAeroporto));
            var hangar = GetFieldValue<List<ControleAviao>>(gerenciador, "avioesNoHangar", typeof(GerenciadorAeroporto));

            string currentSig = GetStateSignature(avioesNoAr, helisNoAr, patio, hangar);
            if (currentSig != _lastStateSignature)
            {
                _lastStateSignature = currentSig;
                RenderPanel();
            }

            if (_selectedUnitId != -1)
            {
                RefreshSelectedUnit();
            }
        }
    }

    private string CleanName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "";
        // Remove sufixo (Clone) e espaçamentos/distributers adicionais
        string cleaned = rawName.Replace("(Clone)", "").Replace("3D", "").Trim();
        return cleaned;
    }

    private string GetStateSignature(List<ControleAviao> avioesNoAr, List<Helicoptero> helisNoAr, List<ControleAviao> patio, List<ControleAviao> hangar)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (avioesNoAr != null) 
        { 
            sb.Append("A:"); sb.Append(avioesNoAr.Count); 
            foreach(var x in avioesNoAr) { if (x != null) sb.Append(x.GetInstanceID()).Append((int)x.estadoAtual); } 
        }
        if (helisNoAr != null) 
        { 
            sb.Append("H:"); sb.Append(helisNoAr.Count); 
            foreach(var x in helisNoAr) { if (x != null) sb.Append(x.GetInstanceID()); } 
        }
        if (patio != null) 
        { 
            sb.Append("P:"); sb.Append(patio.Count); 
            foreach(var x in patio) { if (x != null) sb.Append(x.GetInstanceID()).Append((int)x.estadoAtual); } 
        }
        if (hangar != null) 
        { 
            sb.Append("G:"); sb.Append(hangar.Count); 
            foreach(var x in hangar) { if (x != null) sb.Append(x.GetInstanceID()).Append((int)x.estadoAtual); } 
        }
        return sb.ToString();
    }

    public void ToggleMenu(bool state)
    {
        if (uiDocument == null) return;
        uiDocument.rootVisualElement.style.display = state ? DisplayStyle.Flex : DisplayStyle.None;
        if (state)
        {
            _lastStateSignature = "";
            _lastSelectedUnitStateSignature = "";
            if (lblShipName != null && gerenciador != null)
            {
                lblShipName.text = CleanName(gerenciador.gameObject.name).ToUpper();
            }
            RenderPanel();
        }
        else
        {
            CloseBottomMenu();
        }
    }

    public bool IsMenuOpen()
    {
        return uiDocument != null && uiDocument.rootVisualElement.style.display == DisplayStyle.Flex;
    }

    private void RenderPanel()
    {
        if (sectionsContainer == null || gerenciador == null) return;
        sectionsContainer.Clear();

        // Usa Reflection para pegar as listas internas do GerenciadorPortaAvioes / GerenciadorAeroporto
        var avioesNoAr = GetFieldValue<List<ControleAviao>>(gerenciador, "_avioesProximosNoAr");
        var helisNoAr = GetFieldValue<List<Helicoptero>>(gerenciador, "_helicopterosProximosNoAr");
        var patio = GetFieldValue<List<ControleAviao>>(gerenciador, "avioesNoPatio", typeof(GerenciadorAeroporto));
        var hangar = GetFieldValue<List<ControleAviao>>(gerenciador, "avioesNoHangar", typeof(GerenciadorAeroporto));

        // 1. Radar Aviões
        if (avioesNoAr != null && avioesNoAr.Count > 0)
        {
            var section = CreateSection("RADAR // CONTATO AÉREO");
            foreach (var a in avioesNoAr)
            {
                if (a == null) continue;
                string cleanName = CleanName(a.gameObject.name);
                section.Add(CreateListItem("FLT", cleanName, "RTB POUSAR", "btn-critical", () => {
                    a.ordemParaRetorno = true;
                    a.aeroportoOrigem = gerenciador;
                    ShowToast($"CÓDIGO DE RETORNO ENVIADO. {cleanName} EM APROXIMAÇÃO.", "warn");
                }, () => SelectPlane(a)));
            }
            sectionsContainer.Add(section);
        }

        // 2. Radar Helicópteros
        if (helisNoAr != null && helisNoAr.Count > 0)
        {
            var section = CreateSection("RADAR // CONTATO HELO");
            foreach (var h in helisNoAr)
            {
                if (h == null) continue;
                string cleanName = CleanName(h.gameObject.name);
                section.Add(CreateListItem("HELO", cleanName, "REQUISITAR", "btn-critical", () => {
                    ShowToast($"CHAMANDO {cleanName} PARA O NAVIO.", "warn");
                }, () => SelectHeli(h)));
            }
            sectionsContainer.Add(section);
        }

        // 3. Convés
        if (patio != null)
        {
            var section = CreateSection($"CONVÉS SUPERIOR [{patio.Count}/4]");
            var deckControls = new VisualElement();
            deckControls.AddToClassList("deck-controls");
            var btnBuy = new Button(() => ShowToast("FUNÇÃO DE COMPRA NÃO IMPLEMENTADA", "warn"));
            btnBuy.text = "CONSTRUIR UCAV KAMIKAZE ($1500)";
            btnBuy.AddToClassList("btn-buy-drone");
            btnBuy.AddToClassList("btn-critical");
            deckControls.Add(btnBuy);
            section.Add(deckControls);

            foreach (var a in patio)
            {
                if (a == null) continue;
                var item = new VisualElement();
                item.AddToClassList("list-item");
                
                var nameContainer = new VisualElement();
                nameContainer.AddToClassList("unit-name-container");
                var desig = new Label(a.GetComponent<KamikazeDrone>() ? "UAV" : "FLT");
                desig.AddToClassList("unit-designation");
                string cleanName = CleanName(a.gameObject.name);
                var nameLabel = new Label(cleanName);
                nameLabel.AddToClassList("unit-name");
                
                nameContainer.Add(desig);
                nameContainer.Add(nameLabel);
                nameContainer.RegisterCallback<ClickEvent>(evt => SelectPlane(a));

                var status = new Label("PRONTO");
                status.AddToClassList("unit-status");
                
                item.Add(nameContainer);
                item.Add(status);
                section.Add(item);
            }
            sectionsContainer.Add(section);
        }

        // 4. Hangar
        if (hangar != null)
        {
            var section = CreateSection("HANGAR INTERNO");
            foreach (var a in hangar)
            {
                if (a == null) continue;
                string cleanName = CleanName(a.gameObject.name);
                section.Add(CreateListItem("FLT", cleanName, "ELEV. ACIMA", "btn-action", () => {
                    gerenciador.AcionarElevadorParaCima(a);
                    ShowToast("ELEVADOR ACIONADO.", "info");
                }, () => SelectPlane(a)));
            }
            sectionsContainer.Add(section);
        }
    }

    private void SelectPlane(ControleAviao plane)
    {
        if (plane == null) return;
        _selectedUnitId = plane.GetInstanceID();
        _selectedPlane = plane;
        _selectedHeli = null;
        bottomMenu.style.display = DisplayStyle.Flex;
        RefreshSelectedUnit();
    }

    private void SelectHeli(Helicoptero heli)
    {
        if (heli == null) return;
        _selectedUnitId = heli.GetInstanceID();
        _selectedHeli = heli;
        _selectedPlane = null;
        bottomMenu.style.display = DisplayStyle.Flex;
        RefreshSelectedUnit();
    }

    private void CloseBottomMenu()
    {
        _selectedUnitId = -1;
        _selectedPlane = null;
        _selectedHeli = null;
        if (bottomMenu != null) bottomMenu.style.display = DisplayStyle.None;
    }

    private void RefreshSelectedUnit()
    {
        if (_selectedPlane == null && _selectedHeli == null) return;

        string currentSig = "";
        if (_selectedPlane != null)
        {
            currentSig = $"{_selectedPlane.GetInstanceID()}:{_selectedPlane.estadoAtual}:{_selectedPlane.GetComponent<SistemaDeDanos>()?.vidaAtual}";
        }
        else if (_selectedHeli != null)
        {
            currentSig = $"{_selectedHeli.GetInstanceID()}:{_selectedHeli.GetComponent<SistemaDeDanos>()?.vidaAtual}";
        }

        bool stateChanged = currentSig != _lastSelectedUnitStateSignature;
        if (stateChanged)
        {
            _lastSelectedUnitStateSignature = currentSig;
        }

        if (_selectedPlane != null)
        {
            var dmg = _selectedPlane.GetComponent<SistemaDeDanos>();
            bool isDrone = _selectedPlane.GetComponent<KamikazeDrone>() != null;
            
            bmIcon.text = isDrone ? "[ UAV ]" : "[ FLT ]";
            bmTitle.text = CleanName(_selectedPlane.gameObject.name);
            bmStatus.text = _selectedPlane.estadoAtual == ControleAviao.EstadoAviao.ReservaHangar ? "STATUS: HANGAR" : 
                            (_selectedPlane.estadoAtual == ControleAviao.EstadoAviao.EmMissao ? "STATUS: EM VOO" : "STATUS: CONVÉS");

            if (dmg != null)
            {
                float hp = dmg.vidaAtual;
                float maxHp = dmg.vidaMaxima;
                bmHpText.text = $"{Mathf.Round(hp)}/{maxHp}";
                bmHpBar.style.width = Length.Percent((hp/maxHp)*100f);
                if (hp/maxHp > 0.35f) { bmHpBar.RemoveFromClassList("fill-hp-low"); bmHpBar.AddToClassList("fill-hp"); }
                else { bmHpBar.RemoveFromClassList("fill-hp"); bmHpBar.AddToClassList("fill-hp-low"); }
            }

            if (stateChanged)
            {
                bmCommands.Clear();
                if (_selectedPlane.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                {
                    if (!isDrone)
                    {
                        bmCommands.Add(CreateCmdButton("DECOLAR: MISSÃO DE ATAQUE", "btn-action", () => ShowToast("AGUARDANDO ALVO/MAPA...", "warn")));
                        bmCommands.Add(CreateCmdButton("DECOLAR: RECONHECIMENTO", "btn", () => ShowToast("AGUARDANDO COORDENADAS...", "warn")));
                        bmCommands.Add(CreateCmdButton("RECUAR UNIDADE PARA HANGAR", "btn-critical", () => {
                            gerenciador.MandarParaOHangar(_selectedPlane);
                            ShowToast("MANDANDO PARA O HANGAR", "info");
                            CloseBottomMenu();
                        }, true));
                    }
                }
            }
        }
        else if (_selectedHeli != null)
        {
            bmIcon.text = "[ HELO ]";
            bmTitle.text = CleanName(_selectedHeli.gameObject.name);
            bmStatus.text = "STATUS: EM VOO";
            
            var dmg = _selectedHeli.GetComponent<SistemaDeDanos>();
            if (dmg != null)
            {
                float hp = dmg.vidaAtual;
                float maxHp = dmg.vidaMaxima;
                bmHpText.text = $"{Mathf.Round(hp)}/{maxHp}";
                bmHpBar.style.width = Length.Percent((hp/maxHp)*100f);
            }

            if (stateChanged)
            {
                bmCommands.Clear();
                bmCommands.Add(CreateCmdButton("MISSÃO: RECON", "btn", () => ShowToast("RECONHECIMENTO INICIADO", "info")));
                bmCommands.Add(CreateCmdButton("ABORTAR E RETORNAR (RTB)", "btn-danger", () => ShowToast("HELICOPTERO RETORNANDO", "warn"), true));
            }
        }
    }

    private Button CreateCmdButton(string text, string cls, System.Action onClick, bool fullWidth = false)
    {
        var btn = new Button(onClick);
        btn.text = text;
        btn.AddToClassList("cmd-btn");
        if (cls != "btn") btn.AddToClassList(cls);
        if (fullWidth) btn.AddToClassList("full-width");
        return btn;
    }

    private VisualElement CreateSection(string title)
    {
        var section = new VisualElement();
        section.AddToClassList("section");
        var secTitle = new VisualElement();
        secTitle.AddToClassList("section-title");
        var lbl = new Label(title);
        secTitle.Add(lbl);
        section.Add(secTitle);
        return section;
    }

    private VisualElement CreateListItem(string designation, string name, string btnText, string btnClass, System.Action onBtnClick, System.Action onSelect)
    {
        var item = new VisualElement();
        item.AddToClassList("list-item");
        
        var nameContainer = new VisualElement();
        nameContainer.AddToClassList("unit-name-container");
        var desig = new Label(designation);
        desig.AddToClassList("unit-designation");
        var nameLabel = new Label(name);
        nameLabel.AddToClassList("unit-name");
        
        nameContainer.Add(desig);
        nameContainer.Add(nameLabel);
        nameContainer.RegisterCallback<ClickEvent>(evt => onSelect());

        var btn = new Button(onBtnClick);
        btn.text = btnText;
        btn.AddToClassList("btn");
        if (btnClass != "") btn.AddToClassList(btnClass);

        item.Add(nameContainer);
        item.Add(btn);
        return item;
    }

    public void ShowToast(string msg, string type)
    {
        if (toastContainer == null) return;
        var toast = new VisualElement();
        toast.AddToClassList("toast");
        toast.AddToClassList($"toast-{type}");

        string prefix = "[INFO]";
        if (type == "success") prefix = "[OK]";
        else if (type == "warn") prefix = "[AVISO]";
        else if (type == "error") prefix = "[CRÍTICO]";

        var lblPrefix = new Label(prefix);
        lblPrefix.style.unityFontStyleAndWeight = FontStyle.Bold;
        lblPrefix.style.marginRight = 10;
        
        var lblMsg = new Label(msg);

        toast.Add(lblPrefix);
        toast.Add(lblMsg);
        toastContainer.Add(toast);

        // Remove após 3s
        toast.schedule.Execute(() => {
            if (toastContainer.Contains(toast))
                toastContainer.Remove(toast);
        }).StartingIn(3000);
    }

    private T GetFieldValue<T>(object obj, string fieldName, System.Type type = null)
    {
        if (obj == null) return default;
        var t = type ?? obj.GetType();
        var field = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            return (T)field.GetValue(obj);
        }
        return default;
    }
}
