using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Fazenda : MonoBehaviour
{
    public static Fazenda FazendaAtiva; // Para fechar outras ao abrir
    public static bool QualquerFazendaAberta = false; // Pro CameraController
    private static int frameCliqueProcessado = -1;
    private static bool cliqueSobreUIProcessado;
    private static bool houveHitCliqueProcessado;
    private static RaycastHit hitCliqueProcessado;
    private static Texture2D texturaBotaoNormal;
    private static Texture2D texturaBotaoHover;
    private static Texture2D texturaJanela;
    private static Texture2D texturaBotaoFechar;
    private static GUIStyle estiloSementeCompartilhado;
    private static GUIStyle estiloStatusCompartilhado;
    private static GUIStyle estiloBotaoCompartilhado;
    private static GUIStyle estiloAvisoCompartilhado;
    private static GUIStyle estiloJanelaAgricolaCompartilhado;
    private static GUIStyle estiloXBotaoCompartilhado;

    public enum SementeAgricola { Nenhum, Milho, Trigo, Soja, CanaDeAcucar, Feijao, Arroz, Algodao, Cafe, Batata, Cacau }

    [System.Serializable]
    public class RegistoColheita
    {
        public string nome;
        public SementeAgricola semente;
        public int lucroGerado;
        public float tempoCrescimento; // tempo real em segundos
        public int custoSemente;
        public int diasParaSafra = 2;
    }

    [Header("Configurações da Fazenda")]
    public List<RegistoColheita> catalogoAgricola = new List<RegistoColheita>();
    public string nomeFazenda = "Fazenda Nacional";
    public bool mostrarLogs = false;
    public float intervaloProducaoSegundos = 1f;

    [Header("Lote de Produção 1")]
    public bool lote1Ocupado = false;
    public int lote1SementeIndex = 0;
    public float lote1Progresso = 0f;

    [Header("Lote de Produção 2")]
    public bool lote2Ocupado = false;
    public int lote2SementeIndex = 0;
    public float lote2Progresso = 0f;

    [Header("Lote de Produção 3 (Adquirível)")]
    public bool lote3Comprado = false;
    public int custoLote3 = 2000;
    public bool lote3Ocupado = false;
    public int lote3SementeIndex = 0;
    public float lote3Progresso = 0f;

    // Interface
    private bool menuAberto = false;
    private Rect janelaRetangulo;
    private IdentidadeUnidade identidade;
    private Vector2 scrollSementes;
    private Vector2 scrollLotes;

    private GUIStyle estiloSemente;
    private GUIStyle estiloStatus;
    private GUIStyle estiloBotao;
    private GUIStyle estiloAviso;
    private GUIStyle estiloJanelaAgricola;
    private GUIStyle estiloXBotao;
    private bool estilosProntos = false;
    private WaitForSeconds esperaProducao;
    private float comidaPorSegundoAtual = 0f;

    void Awake()
    {
        if (Construtor.CriandoPreviewConstrucao)
        {
            enabled = false;
            return;
        }

        identidade = GetComponent<IdentidadeUnidade>();
        GarantirEstruturaEconomica();
        esperaProducao = new WaitForSeconds(Mathf.Max(1.5f, intervaloProducaoSegundos));
        
        // Menu 20% maior (840x600)
        janelaRetangulo = new Rect(Screen.width / 2f - 420f, Screen.height / 2f - 300f, 840f, 600f);

        PopularCatalogoSeNecessario();
    }

    private void Start()
    {
    }

    void OnDestroy()
    {
        if (GerenciadorRecursos.Instancia != null && comidaPorSegundoAtual > 0f)
        {
            GerenciadorRecursos.Instancia.ModificarGanhos(0, 0, 0, 0, -comidaPorSegundoAtual);
        }
        if (FazendaAtiva == this)
        {
            FazendaAtiva = null;
            QualquerFazendaAberta = false;
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        float novaComidaPorSegundo = 0f;
        
        ProcessarCrescimentoContinuo(1, ref lote1Ocupado, ref lote1Progresso, ref lote1SementeIndex, ref novaComidaPorSegundo);
        ProcessarCrescimentoContinuo(2, ref lote2Ocupado, ref lote2Progresso, ref lote2SementeIndex, ref novaComidaPorSegundo);
        ProcessarCrescimentoContinuo(3, ref lote3Ocupado, ref lote3Progresso, ref lote3SementeIndex, ref novaComidaPorSegundo);

        if (Mathf.Abs(novaComidaPorSegundo - comidaPorSegundoAtual) > 0.01f)
        {
            if (GerenciadorRecursos.Instancia != null)
            {
                GerenciadorRecursos.Instancia.ModificarGanhos(0, 0, 0, 0, novaComidaPorSegundo - comidaPorSegundoAtual);
            }
            comidaPorSegundoAtual = novaComidaPorSegundo;
        }

        // Interação de Clique na Fazenda
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        ProcessarCliqueCompartilhado();

        if (cliqueSobreUIProcessado)
        {
            return;
        }

        if (houveHitCliqueProcessado)
        {
            Transform alvoClique = hitCliqueProcessado.transform;
            if (alvoClique == transform || alvoClique.IsChildOf(transform))
            {
                // Apenas dono do time abrem o menu (0 ou 1 costuma ser Player)
                if (identidade == null || identidade.teamID == 1 || identidade.teamID == 0)
                {
                    if (MenuGoverno.Instancia != null) MenuGoverno.Instancia.AlternarMenu(false);

                    // Fecha outra fazenda que estiver aberta
                    if (FazendaAtiva != null && FazendaAtiva != this) FazendaAtiva.FecharMenu();

                    menuAberto = true;
                    FazendaAtiva = this;
                    QualquerFazendaAberta = true;
                    LogFarm("Terminal da fazenda aberto.");
                }
                else
                {
                    LogFarm("Clique ignorado: fazenda pertence ao time " + identidade.teamID + ".");
                }
            }
            else
            {
                FecharSeCliqueFora();
            }
        }
        else
        {
            FecharSeCliqueFora();
        }
    }

    private static void ProcessarCliqueCompartilhado()
    {
        if (frameCliqueProcessado == Time.frameCount)
        {
            return;
        }

        frameCliqueProcessado = Time.frameCount;
        cliqueSobreUIProcessado = UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        houveHitCliqueProcessado = false;
        if (cliqueSobreUIProcessado || Camera.main == null)
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        houveHitCliqueProcessado = Physics.Raycast(ray, out hitCliqueProcessado, 5000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private void FecharSeCliqueFora()
    {
        Rect bounds = new Rect(janelaRetangulo.x, Screen.height - janelaRetangulo.y - janelaRetangulo.height, janelaRetangulo.width, janelaRetangulo.height);
        if (menuAberto && !bounds.Contains(Input.mousePosition))
        {
            FecharMenu();
        }
    }

    public void FecharMenu()
    {
        menuAberto = false;
        if (FazendaAtiva == this)
        {
            FazendaAtiva = null;
            QualquerFazendaAberta = false;
        }
    }

    private void ProcessarCrescimentoContinuo(int numeroLote, ref bool ocupado, ref float progresso, ref int sementeIndex, ref float ganhoComida)
    {
        if (ocupado && sementeIndex > 0 && sementeIndex < catalogoAgricola.Count)
        {
            RegistoColheita cultivo = catalogoAgricola[sementeIndex];
            progresso += Time.deltaTime;
            ganhoComida += (float)cultivo.lucroGerado / cultivo.tempoCrescimento;

            if (progresso >= cultivo.tempoCrescimento)
            {
                if (GerenciadorRecursos.Instancia != null)
                {
                    if (GerenciadorRecursos.Instancia.TentarGastarDinheiro(cultivo.custoSemente))
                    {
                        progresso = 0f;
                        LogFarm($"Replantio automatico de {cultivo.nome}. Custo: -${cultivo.custoSemente}");
                    }
                    else
                    {
                        // Sem dinheiro para sementes -> aguarda
                        progresso = cultivo.tempoCrescimento - 0.1f;
                        ganhoComida -= (float)cultivo.lucroGerado / cultivo.tempoCrescimento;
                    }
                }
                else
                {
                    progresso = 0f;
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

    private void InicializarEstilos()
    {
        if (estilosProntos) return;

        if (texturaBotaoNormal == null) texturaBotaoNormal = CriarTextura(new Color(0.2f, 0.4f, 0.2f, 0.95f));
        if (texturaBotaoHover == null) texturaBotaoHover = CriarTextura(new Color(0.3f, 0.6f, 0.3f, 1f));
        if (texturaJanela == null) texturaJanela = CriarTextura(new Color(0.18f, 0.15f, 0.12f, 0.98f));
        if (texturaBotaoFechar == null) texturaBotaoFechar = CriarTextura(new Color(0.8f, 0.2f, 0.2f, 0.9f));

        if (estiloBotaoCompartilhado == null)
        {
            estiloBotaoCompartilhado = new GUIStyle(GUI.skin.button);
            estiloBotaoCompartilhado.normal.background = texturaBotaoNormal;
            estiloBotaoCompartilhado.hover.background = texturaBotaoHover;
            estiloBotaoCompartilhado.normal.textColor = Color.white;
            estiloBotaoCompartilhado.hover.textColor = Color.yellow;
            estiloBotaoCompartilhado.fontStyle = FontStyle.Bold;
            estiloBotaoCompartilhado.fontSize = 16;
        }

        if (estiloSementeCompartilhado == null)
        {
            estiloSementeCompartilhado = new GUIStyle(GUI.skin.label);
            estiloSementeCompartilhado.normal.textColor = new Color(0.9f, 0.9f, 0.7f, 1f);
            estiloSementeCompartilhado.fontStyle = FontStyle.Bold;
            estiloSementeCompartilhado.fontSize = 16;
        }

        if (estiloStatusCompartilhado == null)
        {
            estiloStatusCompartilhado = new GUIStyle(GUI.skin.label);
            estiloStatusCompartilhado.normal.textColor = new Color(0.8f, 1f, 0.8f, 1f);
            estiloStatusCompartilhado.fontSize = 15;
        }

        if (estiloAvisoCompartilhado == null)
        {
            estiloAvisoCompartilhado = new GUIStyle(GUI.skin.label);
            estiloAvisoCompartilhado.normal.textColor = new Color(1f, 0.6f, 0.6f, 1f);
            estiloAvisoCompartilhado.fontStyle = FontStyle.Italic;
            estiloAvisoCompartilhado.fontSize = 14;
        }

        if (estiloJanelaAgricolaCompartilhado == null)
        {
            estiloJanelaAgricolaCompartilhado = new GUIStyle(GUI.skin.window);
            estiloJanelaAgricolaCompartilhado.normal.background = texturaJanela;
            estiloJanelaAgricolaCompartilhado.normal.textColor = new Color(0.9f, 0.9f, 0.8f);
            estiloJanelaAgricolaCompartilhado.fontStyle = FontStyle.Bold;
            estiloJanelaAgricolaCompartilhado.fontSize = 16;
        }

        if (estiloXBotaoCompartilhado == null)
        {
            estiloXBotaoCompartilhado = new GUIStyle(estiloBotaoCompartilhado);
            estiloXBotaoCompartilhado.normal.background = texturaBotaoFechar;
        }

        estiloBotao = estiloBotaoCompartilhado;
        estiloSemente = estiloSementeCompartilhado;
        estiloStatus = estiloStatusCompartilhado;
        estiloAviso = estiloAvisoCompartilhado;
        estiloJanelaAgricola = estiloJanelaAgricolaCompartilhado;
        estiloXBotao = estiloXBotaoCompartilhado;

        estilosProntos = true;
    }

    void OnGUI()
    {
        if (!menuAberto) return;
        InicializarEstilos();
        GUI.depth = -110; 

        // Usa o estilo já inicializado no InicializarEstilos() para não vazar memória
        janelaRetangulo = GUI.Window(88901, janelaRetangulo, InterfaceFazenda, "🚜 TERMINAL DE PRODUÇÃO - " + nomeFazenda.ToUpper(), estiloJanelaAgricola);

        // Bloqueia Scroll do Mouse para não mexer a câmera enquanto o mouse estiver sobre o retângulo do Menu
        Rect bounds = new Rect(janelaRetangulo.x, janelaRetangulo.y, janelaRetangulo.width, janelaRetangulo.height);
        Vector2 mouseInv = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (bounds.Contains(mouseInv))
        {
            // Set flag on globally so CameraController can see it if we updated it
            QualquerFazendaAberta = true; 
        }
    }

    void InterfaceFazenda(int windowID)
    {
        GUILayout.Space(15);
        GUILayout.Label("Selecione culturas. Ao terminar o ciclo, ganhos vão ao País e plantam de novo sozinhos.", estiloStatus);
        
        GUILayout.BeginHorizontal();

        // ------------------------- LADO ESQUERDO (Status dos Lotes)
        GUILayout.BeginVertical("box", GUILayout.Width(350)); // Maior
        GUILayout.Label("/// CAMPOS DE PRODUÇÃO ///", estiloSemente);
        GUILayout.Space(15);

        scrollLotes = GUILayout.BeginScrollView(scrollLotes);

        DesenharLoteUI(1, lote1Ocupado, lote1SementeIndex, lote1Progresso);
        GUILayout.Space(25);
        DesenharLoteUI(2, lote2Ocupado, lote2SementeIndex, lote2Progresso);
        GUILayout.Space(25);
        
        if (!lote3Comprado) {
            GUILayout.BeginVertical("box");
            GUILayout.Label("LOTE DE TERRA 3 (BLOQUEADO)", estiloAviso);
            GUILayout.Space(5);
            GUILayout.Label($"Expansão de terras de cultivo. Requer liberação governamental para preparar o solo.", estiloStatus);
            GUILayout.Space(10);
            if (GUILayout.Button($"Desbloquear Terceiro Lote\nCusto: -${custoLote3}", estiloBotao, GUILayout.Height(50))) {
                if (GerenciadorRecursos.Instancia != null && GerenciadorRecursos.Instancia.TentarGastarDinheiro(custoLote3)) {
                    lote3Comprado = true;
                }
            }
            GUILayout.EndVertical();
        } else {
            DesenharLoteUI(3, lote3Ocupado, lote3SementeIndex, lote3Progresso);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // ------------------------- LADO DIREITO (Catálogo de Culturas do País)
        GUILayout.BeginVertical("box");
        GUILayout.Label("/// CATÁLOGO DE SEMENTES ///", estiloSemente);
        GUILayout.Space(10);

        scrollSementes = GUILayout.BeginScrollView(scrollSementes);
        
        for (int i = 1; i < catalogoAgricola.Count; i++) // Ignora index 0 ("Nenhum")
        {
            RegistoColheita cultura = catalogoAgricola[i];
            
            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical(GUILayout.Width(220));
            GUILayout.Label($"🌱 {cultura.nome}", estiloSemente);
            GUILayout.Label($"Cresce em: {cultura.tempoCrescimento}s", estiloStatus);
            GUILayout.Label($"Produção: +{cultura.lucroGerado} Comida", estiloStatus);
            GUILayout.Label($"Semente Custa: -${cultura.custoSemente}", estiloAviso);
            GUILayout.EndVertical();
            
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            if (GUILayout.Button("PLANTAR NO\nLOTE 1", estiloBotao, GUILayout.Height(35))) PlantarSemente(1, i);
            GUILayout.Space(5);
            if (GUILayout.Button("PLANTAR NO\nLOTE 2", estiloBotao, GUILayout.Height(35))) PlantarSemente(2, i);
            
            if (lote3Comprado) {
                GUILayout.Space(5);
                if (GUILayout.Button("PLANTAR NO\nLOTE 3", estiloBotao, GUILayout.Height(35))) PlantarSemente(3, i);
            }
            
            GUILayout.EndVertical();
            
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }
        
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        if (GUI.Button(new Rect(janelaRetangulo.width - 40, 5, 35, 30), "X", estiloXBotao)) FecharMenu();

        GUI.DragWindow();
    }

    void DesenharLoteUI(int numeroLote, bool ocupado, int sementeIndex, float progressoAtual)
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label($"LOTE DE TERRA {numeroLote}", estiloSemente);
        GUILayout.Space(5);
        
        if (ocupado && sementeIndex > 0)
        {
            RegistoColheita colheita = catalogoAgricola[sementeIndex];
            float porcentagem = (progressoAtual / colheita.tempoCrescimento) * 100f;
            
            GUILayout.Label($"Cultura: {colheita.nome}", estiloStatus);
            GUILayout.Label($"Produção: +{colheita.lucroGerado} Comida", estiloStatus);
            GUILayout.Label($"Amadurecendo: {porcentagem:F1}%", estiloAviso);
            GUILayout.Space(5);

            if (GUILayout.Button("Destruir Lavouras (Desocupar)", estiloBotao, GUILayout.Height(35)))
            {
                LimparLote(numeroLote);
            }
        }
        else
        {
            GUILayout.Label("Estado: TERRENO ARÁVEL VAZIO", estiloStatus);
            GUILayout.Label("Aguardando Operário...", estiloAviso);
            GUILayout.Space(38);
        }
        GUILayout.EndVertical();
    }

    void PlantarSemente(int loteDestino, int sementeId)
    {
        if (GerenciadorRecursos.Instancia == null) return;
        
        RegistoColheita novaSemente = catalogoAgricola[sementeId];
        
        // Verifica dinheiro pra semente e planta
        if (GerenciadorRecursos.Instancia.TentarGastarDinheiro(novaSemente.custoSemente))
        {
            if (loteDestino == 1)
            {
                lote1Ocupado = true;
                lote1SementeIndex = sementeId;
                lote1Progresso = 0f;
            }
            else if (loteDestino == 2)
            {
                lote2Ocupado = true;
                lote2SementeIndex = sementeId;
                lote2Progresso = 0f;
            }
            else
            {
                lote3Ocupado = true;
                lote3SementeIndex = sementeId;
                lote3Progresso = 0f;
            }
            LogFarm($"Trabalhadores plantaram sementes de {novaSemente.nome}.");
        }
        else
        {
            LogFarm("Dinheiro insuficiente para comprar sementes de " + novaSemente.nome);
        }
    }

    void LimparLote(int loteId)
    {
        if (loteId == 1)
        {
            lote1Ocupado = false;
            lote1SementeIndex = 0;
            lote1Progresso = 0f;
        }
        else if (loteId == 2)
        {
            lote2Ocupado = false;
            lote2SementeIndex = 0;
            lote2Progresso = 0f;
        }
        else
        {
            lote3Ocupado = false;
            lote3SementeIndex = 0;
            lote3Progresso = 0f;
        }
    }

    // Salvar Dados Básicos (Se tiver sistema de save unificado, aqui ficam as propriedades para ele varrer)
    // As propriedades lote1Ocupado e afins já são públicas, sendo fácil de serializar no JSON/PlayerPrefs global!
    private void GarantirEstruturaEconomica()
    {
        EstruturaEconomica estrutura = GetComponent<EstruturaEconomica>();
        if (estrutura == null)
            estrutura = gameObject.AddComponent<EstruturaEconomica>();

        estrutura.tipo = TipoEstruturaEconomica.Farm;
        estrutura.InferirTeamId();
        estrutura.AplicarPadraoPorTipo();
    }

    private void PopularCatalogoSeNecessario()
    {
        if (catalogoAgricola == null)
            catalogoAgricola = new List<RegistoColheita>();

        if (catalogoAgricola.Count > 0)
            return;

        catalogoAgricola.Add(new RegistoColheita { nome = "-", semente = SementeAgricola.Nenhum });
        catalogoAgricola.Add(new RegistoColheita { nome = "Milho", semente = SementeAgricola.Milho, lucroGerado = 300, tempoCrescimento = 90f, custoSemente = 50, diasParaSafra = 2 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Batata", semente = SementeAgricola.Batata, lucroGerado = 280, tempoCrescimento = 100f, custoSemente = 40, diasParaSafra = 2 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Feijao", semente = SementeAgricola.Feijao, lucroGerado = 250, tempoCrescimento = 120f, custoSemente = 35, diasParaSafra = 2 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Trigo", semente = SementeAgricola.Trigo, lucroGerado = 360, tempoCrescimento = 180f, custoSemente = 65, diasParaSafra = 3 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Arroz", semente = SementeAgricola.Arroz, lucroGerado = 400, tempoCrescimento = 210f, custoSemente = 70, diasParaSafra = 3 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Cana-de-Acucar", semente = SementeAgricola.CanaDeAcucar, lucroGerado = 550, tempoCrescimento = 300f, custoSemente = 100, diasParaSafra = 4 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Algodao", semente = SementeAgricola.Algodao, lucroGerado = 650, tempoCrescimento = 360f, custoSemente = 120, diasParaSafra = 4 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Soja", semente = SementeAgricola.Soja, lucroGerado = 800, tempoCrescimento = 420f, custoSemente = 150, diasParaSafra = 5 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Cafe", semente = SementeAgricola.Cafe, lucroGerado = 1100, tempoCrescimento = 600f, custoSemente = 250, diasParaSafra = 6 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Cacau", semente = SementeAgricola.Cacau, lucroGerado = 1500, tempoCrescimento = 800f, custoSemente = 350, diasParaSafra = 7 });
    }



    private void LogFarm(string mensagem)
    {
        if (!mostrarLogs)
        {
            return;
        }

        Debug.Log("[FAZENDA] " + mensagem, this);
    }
}
