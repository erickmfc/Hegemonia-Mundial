using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Fazenda : MonoBehaviour
{
    public static Fazenda FazendaAtiva; // Para fechar outras ao abrir
    public static bool QualquerFazendaAberta = false; // Pro CameraController
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
        public long custoSemente;
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
    public long custoLote3 = 2000000L;
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
        
        // Janela compacta e centralizada para manter os dois painéis legíveis.
        janelaRetangulo = new Rect(Screen.width / 2f - 380f, Screen.height / 2f - 270f, 760f, 540f);

        PopularCatalogoSeNecessario();
        NormalizarCatalogoAgricola();
        ProducaoAutomaticaEdificio.Garantir(gameObject, ProducaoAutomaticaEdificio.TipoInstalacao.Fazenda);
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

        // Fazendas sem lavoura ativa nao precisam executar a rotina de
        // crescimento em todos os frames. Isso evita custo acumulado quando
        // existem muitas fazendas no mapa.
        if (!ExisteLavouraAtiva())
        {
            return;
        }

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
    }

    public static bool CliqueCapturadoPeloMenu()
    {
        if (!QualquerFazendaAberta || FazendaAtiva == null || !FazendaAtiva.menuAberto)
        {
            return false;
        }

        // UI Toolkit cobre a tela inteira e o InteractionModeService bloqueia
        // acoes no mundo. Isso tambem impede selecao atraves do painel.
        if (FazendaMenuController.EstaAberto)
        {
            return true;
        }

        Rect bounds = new Rect(
            FazendaAtiva.janelaRetangulo.x,
            Screen.height - FazendaAtiva.janelaRetangulo.y - FazendaAtiva.janelaRetangulo.height,
            FazendaAtiva.janelaRetangulo.width,
            FazendaAtiva.janelaRetangulo.height);
        return bounds.Contains(Input.mousePosition);
    }

    public void FecharMenu()
    {
        EncerrarEstadoDoMenu();
        FazendaMenuController.FecharSeAbertoPara(this);
    }

    internal void EncerrarEstadoDoMenu()
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
            if (cultivo == null || cultivo.tempoCrescimento <= 0f || float.IsNaN(cultivo.tempoCrescimento) || float.IsInfinity(cultivo.tempoCrescimento))
            {
                // Dados legados incompletos nao podem entrar em um ciclo de
                // replantio infinito nem gerar NaN/Infinity na economia.
                ocupado = false;
                progresso = 0f;
                sementeIndex = 0;
                return;
            }

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

    private bool ExisteLavouraAtiva()
    {
        return LoteValido(lote1Ocupado, lote1SementeIndex)
            || LoteValido(lote2Ocupado, lote2SementeIndex)
            || LoteValido(lote3Ocupado, lote3SementeIndex);
    }

    private bool LoteValido(bool ocupado, int sementeIndex)
    {
        if (!ocupado || catalogoAgricola == null || sementeIndex <= 0 || sementeIndex >= catalogoAgricola.Count)
        {
            return false;
        }

        RegistoColheita cultura = catalogoAgricola[sementeIndex];
        return cultura != null && cultura.tempoCrescimento > 0f && !float.IsNaN(cultura.tempoCrescimento);
    }

    private void NormalizarCatalogoAgricola()
    {
        if (catalogoAgricola == null)
        {
            catalogoAgricola = new List<RegistoColheita>();
        }

        for (int i = 0; i < catalogoAgricola.Count; i++)
        {
            RegistoColheita cultura = catalogoAgricola[i];
            if (cultura == null)
            {
                catalogoAgricola[i] = new RegistoColheita
                {
                    nome = "Cultura indisponivel",
                    semente = SementeAgricola.Nenhum,
                    tempoCrescimento = 120f,
                    custoSemente = 0L
                };
                continue;
            }

            if (cultura.tempoCrescimento <= 0f || float.IsNaN(cultura.tempoCrescimento) || float.IsInfinity(cultura.tempoCrescimento))
            {
                cultura.tempoCrescimento = 120f;
            }

            if (cultura.custoSemente < 0L)
            {
                cultura.custoSemente = 0L;
            }

            if (string.IsNullOrWhiteSpace(cultura.nome))
            {
                cultura.nome = cultura.semente == SementeAgricola.Nenhum ? "Cultura" : cultura.semente.ToString();
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
        // O menu legado IMGUI foi substituido pelo painel UI Toolkit.
        // O processamento agricola e as APIs abaixo continuam sendo usados.
        return;
        /*
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
        */
    }

    void InterfaceFazenda(int windowID)
    {
        GUILayout.Space(15);
        GUILayout.Label("Selecione culturas. Ao terminar o ciclo, ganhos vão ao País e plantam de novo sozinhos.", estiloStatus);
        
        GUILayout.BeginHorizontal();

        // ------------------------- LADO ESQUERDO (Status dos Lotes)
        GUILayout.BeginVertical("box", GUILayout.Width(300));
        GUILayout.Label("/// CAMPOS DE PRODUÇÃO ///", estiloSemente);
        GUILayout.Space(10);

        scrollLotes = GUILayout.BeginScrollView(scrollLotes);

        DesenharLoteUI(1, lote1Ocupado, lote1SementeIndex, lote1Progresso);
        GUILayout.Space(12);
        DesenharLoteUI(2, lote2Ocupado, lote2SementeIndex, lote2Progresso);
        GUILayout.Space(12);
        
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
            GUILayout.BeginVertical(GUILayout.Width(180));
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

    // API de apresentacao: o processamento original continua centralizado
    // acima, mas o painel UI Toolkit nao precisa acessar metodos privados.
    public bool MenuAberto => menuAberto;
    public string NomeFazendaExibicao => string.IsNullOrWhiteSpace(nomeFazenda) ? "Fazenda Nacional" : nomeFazenda;
    public float ComidaPorSegundoAtual => comidaPorSegundoAtual;
    public int CatalogoAgricolaCount => catalogoAgricola != null ? catalogoAgricola.Count : 0;

    public RegistoColheita ObterCultura(int indice)
    {
        if (catalogoAgricola == null || indice < 0 || indice >= catalogoAgricola.Count)
        {
            return null;
        }

        return catalogoAgricola[indice];
    }

    public bool ObterEstadoLote(int numeroLote, out bool ocupado, out int sementeIndex, out float progresso)
    {
        ocupado = false;
        sementeIndex = 0;
        progresso = 0f;

        switch (numeroLote)
        {
            case 1:
                ocupado = lote1Ocupado;
                sementeIndex = lote1SementeIndex;
                progresso = lote1Progresso;
                return true;
            case 2:
                ocupado = lote2Ocupado;
                sementeIndex = lote2SementeIndex;
                progresso = lote2Progresso;
                return true;
            case 3:
                ocupado = lote3Ocupado;
                sementeIndex = lote3SementeIndex;
                progresso = lote3Progresso;
                return lote3Comprado;
            default:
                return false;
        }
    }

    public bool PlantarSementePeloMenu(int loteDestino, int sementeId)
    {
        if (loteDestino == 3 && !lote3Comprado)
        {
            return false;
        }

        bool ocupado;
        int sementeAtual;
        float progresso;
        if (!ObterEstadoLote(loteDestino, out ocupado, out sementeAtual, out progresso) || ocupado)
        {
            return false;
        }

        RegistoColheita cultura = ObterCultura(sementeId);
        if (cultura == null || sementeId <= 0)
        {
            return false;
        }

        PlantarSemente(loteDestino, sementeId);
        ObterEstadoLote(loteDestino, out ocupado, out sementeAtual, out progresso);
        return ocupado && sementeAtual == sementeId;
    }

    public void LiberarLotePeloMenu(int numeroLote)
    {
        LimparLote(numeroLote);
    }

    public bool ComprarTerceiroLotePeloMenu()
    {
        if (lote3Comprado)
        {
            return true;
        }

        if (GerenciadorRecursos.Instancia == null ||
            !GerenciadorRecursos.Instancia.TentarGastarDinheiro(custoLote3))
        {
            LogFarm("Dinheiro insuficiente para desbloquear o terceiro lote.");
            return false;
        }

        lote3Comprado = true;
        LogFarm("Terceiro lote desbloqueado.");
        return true;
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
        catalogoAgricola.Add(new RegistoColheita { nome = "Milho", semente = SementeAgricola.Milho, lucroGerado = 300, tempoCrescimento = 90f, custoSemente = 50000L, diasParaSafra = 2 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Batata", semente = SementeAgricola.Batata, lucroGerado = 280, tempoCrescimento = 100f, custoSemente = 40000L, diasParaSafra = 2 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Feijao", semente = SementeAgricola.Feijao, lucroGerado = 250, tempoCrescimento = 120f, custoSemente = 35000L, diasParaSafra = 2 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Trigo", semente = SementeAgricola.Trigo, lucroGerado = 360, tempoCrescimento = 180f, custoSemente = 65000L, diasParaSafra = 3 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Arroz", semente = SementeAgricola.Arroz, lucroGerado = 400, tempoCrescimento = 210f, custoSemente = 70000L, diasParaSafra = 3 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Cana-de-Acucar", semente = SementeAgricola.CanaDeAcucar, lucroGerado = 550, tempoCrescimento = 300f, custoSemente = 100000L, diasParaSafra = 4 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Algodao", semente = SementeAgricola.Algodao, lucroGerado = 650, tempoCrescimento = 360f, custoSemente = 120000L, diasParaSafra = 4 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Soja", semente = SementeAgricola.Soja, lucroGerado = 800, tempoCrescimento = 420f, custoSemente = 150000L, diasParaSafra = 5 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Cafe", semente = SementeAgricola.Cafe, lucroGerado = 1100, tempoCrescimento = 600f, custoSemente = 250000L, diasParaSafra = 6 });
        catalogoAgricola.Add(new RegistoColheita { nome = "Cacau", semente = SementeAgricola.Cacau, lucroGerado = 1500, tempoCrescimento = 800f, custoSemente = 350000L, diasParaSafra = 7 });
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
