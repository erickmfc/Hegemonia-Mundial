using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GerenciadorQuartel : MonoBehaviour
{
    [Header("Estrutura (Detectada Automaticamente)")]
    public List<Transform> dormitorios = new List<Transform>();
    public List<Transform> waypointsEntradaEstacionamento = new List<Transform>();
    public List<Transform> paradasEstacionamento = new List<Transform>();

    [Header("Unidades Armazenadas")]
    public List<ControleUnidade> soldadosNoDormitorio = new List<ControleUnidade>();
    public List<ControleUnidade> veiculosNoQuartel = new List<ControleUnidade>();
    
    private HashSet<Transform> vagasOcupadas = new HashSet<Transform>();

    [Header("Arsenal e Munição")]
    public int misseisArmazenados = 0;
    public int municaoArmazenada = 0;
    public int precoMissil = 500;
    public int precoMunicao = 100;

    [Header("Chamada Automática (Limites de Área)")]
    public float raioDeCobertura = 2000f; 
    public bool recolhimentoAutomatico = false;
    public float tempoOciosoPermitido = 60f;
    private Dictionary<ControleUnidade, float> tempoOciosoUnidades = new Dictionary<ControleUnidade, float>();

    [Header("Recursos Extras (Inovação Tática)")]
    public bool treinamentoPassivo = true; 
    public bool modoDefensivoAtivo = false; 
    private float scanDefesaTimer = 0f;

    // UI Estilos
    public static bool InterfaceAberta = false;
    private bool menuAberto = false;
    private Rect janelaRetangulo;
    private int abaAtual = 0; 
    private Vector2 scrollTropas;
    private Vector2 scrollInteligencia;
    private Vector2 scrollConvocar;
    
    private GUIStyle estiloJanela;
    private GUIStyle estiloBotao;
    private GUIStyle estiloAba;
    private GUIStyle estiloTexto;
    private bool estilosCriados = false;

    // Status Inteligência
    private class StatusInimigo {
        public string nomePais;
        public int infantaria;
        public int veiculos;
        public int navais;
        public int aereos;
        public int predios;
    }
    private Dictionary<int, StatusInimigo> infoInimigos = new Dictionary<int, StatusInimigo>();
    private float tagAtualizacaoIntel = 0f;

    void Awake()
    {
        MapearDormitorios();
        MapearEstacionamento();
        janelaRetangulo = new Rect(Screen.width / 2f - 450f, Screen.height / 2f - 325f, 950f, 650f);
    }

    void Update()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null) return;
        
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (!menuAberto)
            {
                FecharOutrosMenus();
                InterfaceAberta = true;
                menuAberto = true;
                janelaRetangulo.position = new Vector2(Screen.width / 2f - 450f, Screen.height / 2f - 325f);
            }
            else
            {
                menuAberto = false;
                InterfaceAberta = false;
            }
        }

        if (recolhimentoAutomatico)
        {
            MonitorarUnidadesOciosas();
        }

        if (modoDefensivoAtivo && Time.time > scanDefesaTimer)
        {
            ChecarInvasaoEAcordarBase();
            scanDefesaTimer = Time.time + 4f;
        }
    }

    private void FecharOutrosMenus()
    {
        if (MenuGoverno.Instancia != null) MenuGoverno.Instancia.AlternarMenu(false);
        var construtor = Object.FindFirstObjectByType<MenuConstrucao>();
        if (construtor != null && MenuConstrucao.EstaAberto) construtor.AlternarMenu(false);
    }

    private void ChecarInvasaoEAcordarBase()
    {
        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        bool inimigoProximo = false;

        foreach (var id in todas)
        {
            if (id.teamID != 1 && Vector3.Distance(transform.position, id.transform.position) <= raioDeCobertura)
            {
                inimigoProximo = true;
                break;
            }
        }

        if (inimigoProximo)
        {
            if (soldadosNoDormitorio.Count > 0 || veiculosNoQuartel.Count > 0)
            {
                DesdobrarSoldados(soldadosNoDormitorio.Count);
                int totalV = veiculosNoQuartel.Count;
                for(int i = totalV - 1; i >= 0; i--) DesdobrarVeiculo(veiculosNoQuartel[i]);
            }
        }
    }

    private void MonitorarUnidadesOciosas()
    {
        if (Time.frameCount % 90 != 0) return;

        IdentidadeUnidade[] todasUnidades = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        
        foreach (var id in todasUnidades)
        {
            if (id.teamID != 1) continue;

            ControleUnidade u = id.GetComponent<ControleUnidade>();
            if (u == null || !u.gameObject.activeInHierarchy) continue;
            
            if (u.TemControleAviao || u.TemControleAviaoCaca || id.tipoUnidade == TipoUnidade.Naval || id.tipoUnidade == TipoUnidade.Aereo || id.tipoUnidade == TipoUnidade.Estrutura) 
                continue;

            if (Vector3.Distance(transform.position, u.transform.position) > raioDeCobertura) continue;

            if (u.ObterVelocidadeAtualReal() > 0.1f || u.selecionado || 
                veiculosNoQuartel.Contains(u) || soldadosNoDormitorio.Contains(u))
            {
                tempoOciosoUnidades[u] = Time.time;
            }
            else
            {
                if (!tempoOciosoUnidades.ContainsKey(u)) tempoOciosoUnidades[u] = Time.time;

                float tempoParado = Time.time - tempoOciosoUnidades[u];
                if (tempoParado > tempoOciosoPermitido)
                {
                    ReceberUnidade(u);
                    tempoOciosoUnidades.Remove(u);
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
        if (estilosCriados) return;

        estiloJanela = new GUIStyle(GUI.skin.window);
        estiloJanela.normal.background = CriarTextura(new Color(0.13f, 0.18f, 0.14f, 0.98f)); 
        estiloJanela.normal.textColor = new Color(0.9f, 1f, 0.9f);
        estiloJanela.fontStyle = FontStyle.Bold;
        estiloJanela.fontSize = 20;

        estiloBotao = new GUIStyle(GUI.skin.button);
        estiloBotao.normal.background = CriarTextura(new Color(0.2f, 0.3f, 0.2f, 0.9f));
        estiloBotao.hover.background = CriarTextura(new Color(0.3f, 0.45f, 0.25f, 1f));
        estiloBotao.normal.textColor = Color.white;
        estiloBotao.hover.textColor = new Color(1f, 0.8f, 0.2f);
        estiloBotao.padding = new RectOffset(6, 6, 6, 6);
        estiloBotao.fontSize = 15;
        estiloBotao.fontStyle = FontStyle.Bold;

        estiloAba = new GUIStyle(estiloBotao);
        estiloAba.fontSize = 16;
        estiloAba.padding = new RectOffset(10, 10, 10, 10);

        estiloTexto = new GUIStyle(GUI.skin.label);
        estiloTexto.normal.textColor = new Color(0.8f, 0.95f, 0.8f, 1f);
        estiloTexto.fontSize = 15;
        estiloTexto.fontStyle = FontStyle.Bold;

        estilosCriados = true;
    }

    void OnGUI()
    {
        if (!menuAberto) return;
        InicializarEstilos();

        GUI.depth = -100;
        janelaRetangulo = GUI.Window(943, janelaRetangulo, DesenharJanela, "QG COMANDO - QUARTEL GERAL", estiloJanela);
    }

    void DesenharJanela(int windowID)
    {
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SALA DE TROPAS", estiloAba, GUILayout.Height(45))) abaAtual = 0;
        if (GUILayout.Button("LOGÍSTICA & ARSENAL", estiloAba, GUILayout.Height(45))) abaAtual = 1;
        if (GUILayout.Button("INTELIGÊNCIA INIMIGA", estiloAba, GUILayout.Height(45))) abaAtual = 2;
        GUILayout.EndHorizontal();

        GUILayout.Space(15);

        if (abaAtual == 0) DesenharAbaTropas();
        else if (abaAtual == 1) DesenharAbaArsenal();
        else if (abaAtual == 2) DesenharAbaInteligencia();

        GUIStyle xStyle = new GUIStyle(estiloBotao);
        xStyle.normal.background = CriarTextura(new Color(0.6f, 0.1f, 0.1f, 1f));
        if (GUI.Button(new Rect(janelaRetangulo.width - 45, 5, 40, 30), "X", xStyle))
        {
            menuAberto = false;
            InterfaceAberta = false;
        }

        GUI.DragWindow();
    }

    void DesenharAbaTropas()
    {
        GUILayout.BeginHorizontal();

        // ======= COLUNA ESQUERDA (Recolher do Mapa) ======
        GUILayout.BeginVertical("box", GUILayout.Width(janelaRetangulo.width / 2f - 20));
        GUILayout.Label(">>> LISTA DE EFETIVOS EM CAMPO (Recolher)", estiloTexto);
        GUILayout.Space(5);

        // Varre quais unidades estão no mapa dando sopa
        List<ControleUnidade> soldadosAvulsos = new List<ControleUnidade>();
        List<ControleUnidade> veiculosAvulsos = new List<ControleUnidade>();

        foreach (var id in Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None))
        {
            if (id.teamID == 1)
            {
                ControleUnidade u = id.GetComponent<ControleUnidade>();
                if (u == null || !u.gameObject.activeInHierarchy) continue;
                if (u.TemControleAviao || u.TemControleAviaoCaca || id.tipoUnidade == TipoUnidade.Naval || id.tipoUnidade == TipoUnidade.Estrutura || id.tipoUnidade == TipoUnidade.Aereo) continue;

                // Não lista que já tá hibernando
                if (!veiculosNoQuartel.Contains(u) && !soldadosNoDormitorio.Contains(u))
                {
                    // Apenas se estiver dentro da área de rádio
                    if (Vector3.Distance(u.transform.position, transform.position) <= raioDeCobertura)
                    {
                        var dmg = u.GetComponent<SistemaDeDanos>();
                        if (dmg != null && dmg.unidadeBiologica) soldadosAvulsos.Add(u);
                        else veiculosAvulsos.Add(u);
                    }
                }
            }
        }

        if (GUILayout.Button("CONVOCAR: OS SELECIONADOS NO MAPA", estiloBotao, GUILayout.Height(40)))
        {
            foreach (var u in Object.FindObjectsByType<ControleUnidade>(FindObjectsSortMode.None))
                if (u.selecionado && u.GetComponent<IdentidadeUnidade>()?.teamID == 1)
                {
                    u.selecionado = false;
                    ReceberUnidade(u);
                }
        }
        
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"CHAMAR TODOS SOLDADOS ({soldadosAvulsos.Count})", estiloBotao, GUILayout.Height(35)))
        {
            foreach (var u in soldadosAvulsos) ReceberUnidade(u);
        }
        if (GUILayout.Button($"CHAMAR TODOS VEÍCULOS ({veiculosAvulsos.Count})", estiloBotao, GUILayout.Height(35)))
        {
            foreach (var u in veiculosAvulsos) ReceberUnidade(u);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        scrollConvocar = GUILayout.BeginScrollView(scrollConvocar, GUILayout.Height(380));
        
        // Soldados Espalhados
        if (soldadosAvulsos.Count > 0)
        {
            GUILayout.Label($"- INFANTARIA LIVRE ({soldadosAvulsos.Count}) -", estiloTexto);
            foreach (var s in soldadosAvulsos)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label(s.name, estiloTexto, GUILayout.Width(220));
                if (GUILayout.Button("Convocar", estiloBotao, GUILayout.Width(100))) ReceberUnidade(s);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(15);
        }

        // Veículos Espalhados
        if (veiculosAvulsos.Count > 0)
        {
            GUILayout.Label($"- VEÍCULOS LIVRES ({veiculosAvulsos.Count}) -", estiloTexto);
            foreach (var v in veiculosAvulsos)
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label(v.name, estiloTexto, GUILayout.Width(220));
                if (GUILayout.Button("Convocar", estiloBotao, GUILayout.Width(100))) ReceberUnidade(v);
                GUILayout.EndHorizontal();
            }
        }
        
        if (soldadosAvulsos.Count == 0 && veiculosAvulsos.Count == 0)
        {
            GUILayout.Label("Nenhuma unidade encontrada solta no Raio do Quartel.", estiloTexto);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();


        GUILayout.Space(10);
        // ======= COLUNA DIREITA (Desdobrar p/ Guerra) ======
        GUILayout.BeginVertical("box", GUILayout.Width(janelaRetangulo.width / 2f - 20));
        GUILayout.Label(">>> TROPAS ARMAZENADAS (Lançar p/ Guerra)", estiloTexto);
        GUILayout.Space(5);

        GUILayout.Label($"Soldados Dormindo: {soldadosNoDormitorio.Count}", estiloTexto);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Desdobrar 1", estiloBotao, GUILayout.Height(35))) DesdobrarSoldados(1);
        if (GUILayout.Button("Desdobrar 5", estiloBotao, GUILayout.Height(35))) DesdobrarSoldados(5);
        if (GUILayout.Button("Esvaziar Dormitório", estiloBotao, GUILayout.Height(35))) DesdobrarSoldados(soldadosNoDormitorio.Count);
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        GUILayout.Label($"Veículos Pesados: {veiculosNoQuartel.Count}", estiloTexto);

        if (GUILayout.Button("LIGAR TODOS VEÍCULOS (Retirar base inteira)", estiloBotao, GUILayout.Height(40)))
        {
            int totalV = veiculosNoQuartel.Count;
            for(int i = totalV - 1; i >= 0; i--) DesdobrarVeiculo(veiculosNoQuartel[i]);
        }
        
        GUILayout.Space(10);
        scrollTropas = GUILayout.BeginScrollView(scrollTropas, GUILayout.Height(310));
        
        for (int i = 0; i < veiculosNoQuartel.Count; i++)
        {
            ControleUnidade v = veiculosNoQuartel[i];
            if (v == null) continue;

            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"- {v.name}", estiloTexto, GUILayout.Width(220));
            if (GUILayout.Button("Ligar Motor", estiloBotao, GUILayout.Width(100))) DesdobrarVeiculo(v);
            GUILayout.EndHorizontal();
        }
        
        if (veiculosNoQuartel.Count == 0)
        {
            GUILayout.Label("Nenhum tanque/caminhão estacionado.", estiloTexto);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    void DesenharAbaArsenal()
    {
        GUILayout.Label(">>> PROTOCOLOS DA BASE", estiloTexto);
        recolhimentoAutomatico = GUILayout.Toggle(recolhimentoAutomatico, " RECOLHIMENTO AUTOMÁTICO (Chama por rádio as unidades)", estiloTexto);
        
        if (recolhimentoAutomatico)
        {
            GUILayout.Label($"Aguardar tempo ocioso para chamar: {Mathf.Round(tempoOciosoPermitido)}s", estiloTexto);
            tempoOciosoPermitido = GUILayout.HorizontalSlider(tempoOciosoPermitido, 10f, 300f);
        }

        GUILayout.Space(10);
        modoDefensivoAtivo = GUILayout.Toggle(modoDefensivoAtivo, " DEFESA AUTOMÁTICA (Libera geral se a base for invadida)", estiloTexto);
        treinamentoPassivo = GUILayout.Toggle(treinamentoPassivo, " TREINAMENTO PASSIVO (Bônus constante de HP para quem está hibernando)", estiloTexto);

        GUILayout.Space(25);
        GUILayout.Label(">>> ARSENAL E MUNIÇÕES DE RESERVA", estiloTexto);
        GUILayout.Label($"Mísseis Armazenados: {misseisArmazenados}", estiloTexto);
        GUILayout.Label($"Pacotes de Munição (Balas): {municaoArmazenada}", estiloTexto);

        GUILayout.Space(15);
        if (GerenciadorRecursos.Instancia != null)
        {
            GUILayout.Label($"Fundo Nacional Atual: ${GerenciadorRecursos.Instancia.dinheiro}", estiloTexto);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"ENCOMENDAR LOTE DE MÍSSIL (${precoMissil})", estiloBotao, GUILayout.Height(50)))
            {
                if (GerenciadorRecursos.Instancia.TentarGastarDinheiro(precoMissil)) misseisArmazenados += 10;
            }

            if (GUILayout.Button($"ENCOMENDAR MUNIÇÃO (${precoMunicao})", estiloBotao, GUILayout.Height(50)))
            {
                if (GerenciadorRecursos.Instancia.TentarGastarDinheiro(precoMunicao)) municaoArmazenada += 100;
            }
            GUILayout.EndHorizontal();
        }
    }

    void DesenharAbaInteligencia()
    {
        if (Time.time > tagAtualizacaoIntel)
        {
            AtualizarDadosInimigos();
            tagAtualizacaoIntel = Time.time + 3f; 
        }

        GUILayout.Label(">>> VARREDURA SATELITAL E ESPIONAGEM CIBERNÉTICA", estiloTexto);
        GUILayout.Label("Lista dos países oponentes encontrados e contagem militar:", estiloTexto);
        GUILayout.Space(10);

        scrollInteligencia = GUILayout.BeginScrollView(scrollInteligencia);

        foreach (var kvp in infoInimigos)
        {
            if (kvp.Key == 1) continue; 

            var status = kvp.Value;
            GUILayout.BeginVertical("box");
            GUILayout.Label($"🔴 PAÍS OPONENTE: {status.nomePais.ToUpper()}  [Time ID: {kvp.Key}]", estiloBotao); 
            GUILayout.Label($" - Força de Infantaria: {status.infantaria}", estiloTexto);
            GUILayout.Label($" - Força Blindada/Veículos: {status.veiculos}", estiloTexto);
            GUILayout.Label($" - Força Aérea (Aviões/Helis): {status.aereos}", estiloTexto);
            GUILayout.Label($" - Força Naval (Frota): {status.navais}", estiloTexto);
            GUILayout.Label($" - Infraestruturas e Prédios: {status.predios}", estiloTexto);
            GUILayout.EndVertical();
            GUILayout.Space(15);
        }

        if (infoInimigos.Count <= 1)
            GUILayout.Label("... Aguardando sinal. Nenhum inimigo monitorado ...", estiloTexto);

        GUILayout.EndScrollView();
    }

    void AtualizarDadosInimigos()
    {
        infoInimigos.Clear();
        IdentidadeUnidade[] todas = Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
        
        foreach (var id in todas)
        {
            if (!infoInimigos.ContainsKey(id.teamID))
                infoInimigos[id.teamID] = new StatusInimigo { nomePais = id.nomeDoPais };
            
            var s = infoInimigos[id.teamID];
            if (id.tipoUnidade == TipoUnidade.Infantaria) s.infantaria++;
            else if (id.tipoUnidade == TipoUnidade.Veiculo) s.veiculos++;
            else if (id.tipoUnidade == TipoUnidade.Aereo) s.aereos++;
            else if (id.tipoUnidade == TipoUnidade.Naval) s.navais++;
            else if (id.tipoUnidade == TipoUnidade.Estrutura) s.predios++;
        }
    }

    private void MapearDormitorios()
    {
        Transform dom = ObterFilhoPorNome(transform, "dormitorio");
        if (dom != null)
            foreach (Transform filho in dom)
                dormitorios.Add(filho);
    }

    private void MapearEstacionamento()
    {
        Transform estac = ObterFilhoPorNome(transform, "estacionamento");
        if (estac != null)
        {
            Transform entrada = ObterFilhoPorNome(estac, "entrada");
            if (entrada != null)
                foreach (Transform filho in entrada)
                    waypointsEntradaEstacionamento.Add(filho);

            Transform paradas = ObterFilhoPorNome(estac, "paradas");
            if (paradas != null)
                foreach (Transform filho in paradas)
                    paradasEstacionamento.Add(filho);
        }
    }

    private Transform ObterFilhoPorNome(Transform pai, string nomeContido)
    {
        Transform[] todos = pai.GetComponentsInChildren<Transform>(true);
        foreach (Transform filho in todos)
            if (filho.name.ToLower().Contains(nomeContido.ToLower()))
                return filho;
        return null;
    }

    public void ReceberUnidade(ControleUnidade unidade)
    {
        if (unidade == null || !unidade.gameObject.activeInHierarchy) return;
        SistemaDeDanos sistemaDeDanos = unidade.GetComponent<SistemaDeDanos>();
        bool biologica = (sistemaDeDanos != null && sistemaDeDanos.unidadeBiologica);

        if (biologica)
            StartCoroutine(AcolherSoldado(unidade, sistemaDeDanos));
        else
            StartCoroutine(AcolherVeiculo(unidade, sistemaDeDanos));
    }

    private IEnumerator AcolherSoldado(ControleUnidade soldado, SistemaDeDanos danos)
    {
        if (soldadosNoDormitorio.Contains(soldado)) yield break; // Evita loop de duplicação

        Transform destino = transform; 
        if (dormitorios.Count > 0) destino = dormitorios[Random.Range(0, dormitorios.Count)];

        soldado.MoverParaPonto(destino.position);

        while (soldado != null && soldado.gameObject.activeInHierarchy)
        {
            if (Vector3.Distance(soldado.transform.position, destino.position) < 4f) break;
            yield return null;
        }

        if (soldado != null)
        {
            if (danos != null) 
            {
                danos.Reparar(9999f);
                if (treinamentoPassivo) danos.vidaMaxima *= 1.2f; 
            }
            soldado.gameObject.SetActive(false); 
            if (!soldadosNoDormitorio.Contains(soldado)) soldadosNoDormitorio.Add(soldado);
        }
    }

    private IEnumerator AcolherVeiculo(ControleUnidade veiculo, SistemaDeDanos danos)
    {
        if (veiculosNoQuartel.Contains(veiculo)) yield break; // Evita duplicação

        for (int i = 0; i < waypointsEntradaEstacionamento.Count; i++)
        {
            if (veiculo == null) yield break;
            Transform wp = waypointsEntradaEstacionamento[i];
            veiculo.MoverParaPonto(wp.position);
            while (veiculo != null)
            {
                if (Vector3.Distance(veiculo.transform.position, wp.position) < 5f) break;
                yield return null;
            }
        }

        if (veiculo == null) yield break;

        Transform vagaEscolhida = null;
        foreach (Transform vaga in paradasEstacionamento)
        {
            if (!vagasOcupadas.Contains(vaga))
            {
                vagaEscolhida = vaga;
                break;
            }
        }

        if (vagaEscolhida != null)
        {
            vagasOcupadas.Add(vagaEscolhida);
            veiculo.MoverParaPonto(vagaEscolhida.position);
            while (veiculo != null)
            {
                if (Vector3.Distance(veiculo.transform.position, vagaEscolhida.position) < 3.5f) break;
                yield return null;
            }

            if (veiculo != null)
            {
                if (danos != null) danos.Reparar(9999f);

                veiculo.transform.position = vagaEscolhida.position;
                veiculo.transform.rotation = vagaEscolhida.rotation;
                
                var agente = veiculo.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agente != null) agente.enabled = false;
                
                veiculo.DefinirModoCombate(false); 
                if (!veiculosNoQuartel.Contains(veiculo)) veiculosNoQuartel.Add(veiculo);
            }
        }
        else
        {
            if (danos != null) danos.Reparar(9999f);
            veiculo.gameObject.SetActive(false);
            if (!veiculosNoQuartel.Contains(veiculo)) veiculosNoQuartel.Add(veiculo);
        }
    }

    private void DesdobrarSoldados(int quantidade)
    {
        Vector3 pontoSaida = transform.position + (transform.forward * 15f);
        int liberados = 0;
        for (int i = soldadosNoDormitorio.Count - 1; i >= 0; i--)
        {
            if (liberados >= quantidade) break;
            ControleUnidade soldado = soldadosNoDormitorio[i];
            soldadosNoDormitorio.RemoveAt(i);
            
            if (soldado != null)
            {
                soldado.gameObject.SetActive(true);
                soldado.transform.position = transform.position + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
                
                var danos = soldado.GetComponent<SistemaDeDanos>();
                if (danos != null) danos.Reparar(9999f); 

                soldado.MoverParaPonto(pontoSaida);
                liberados++;
            }
        }
    }
    
    private void DesdobrarVeiculo(ControleUnidade veiculoEspecifico)
    {
        if (veiculoEspecifico != null && veiculosNoQuartel.Contains(veiculoEspecifico))
        {
            veiculosNoQuartel.Remove(veiculoEspecifico);
            veiculoEspecifico.gameObject.SetActive(true);
            
            var agente = veiculoEspecifico.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agente != null)
            {
                agente.enabled = true;
                agente.Warp(veiculoEspecifico.transform.position);
            }
            
            veiculoEspecifico.DefinirModoCombate(true);
            
            foreach (Transform vaga in paradasEstacionamento)
            {
                if (Vector3.Distance(vaga.position, veiculoEspecifico.transform.position) < 2.5f)
                {
                    vagasOcupadas.Remove(vaga);
                    break;
                }
            }

            Vector3 pontoSaida = waypointsEntradaEstacionamento.Count > 0 ? waypointsEntradaEstacionamento[0].position : transform.position + (transform.forward * 20f);
            veiculoEspecifico.MoverParaPonto(pontoSaida);
        }
    }
}
