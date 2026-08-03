using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System;

public class GerenteDeJogo : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLogs = false;

    [Header("Economia - DEPRECATED: Use GerenciadorRecursos.Instancia")]
    [Tooltip("Deprecated: Este campo agora é gerenciado pelo GerenciadorRecursos")]
    public long dinheiroAtual
    { 
        get { return GerenciadorRecursos.Instancia != null ? GerenciadorRecursos.Instancia.dinheiro : 5000L; }
        set { if (GerenciadorRecursos.Instancia != null) GerenciadorRecursos.Instancia.dinheiro = value; }
    } 

    [Header("Logística do Hangar (Tanques)")]
    public Transform spawnInterno; // Onde nasce (dentro do hangar)
    public Transform pontoSaida;   // Para onde vai (na rua)

    [Header("Logística da Tenda (Soldados)")]
    public Transform spawnSoldado; // Onde nasce (dentro da tenda)
    public Transform saidaSoldado; // Para onde vai (na rua/frente da tenda)

    public static GerenteDeJogo Instancia;

    [Header("Jogadores na Partida")]
    public List<IdentidadeIA> comandantesIA = new List<IdentidadeIA>();

    void LogInfo(string msg)
    {
        if (debugLogs)
            Debug.Log(msg);
    }

    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);

        // --- SISTEMA DE UI: MENU PIER (Tecla V) E MENU GOVERNO (Tecla X) ---
        if (GetComponent<MenuPier>() == null)
        {
            gameObject.AddComponent<MenuPier>();
            LogInfo("[Gerente] MenuPier adicionado automaticamente.");
        }
        if (GetComponent<MenuGoverno>() == null)
        {
            MenuGoverno.GarantirInstancia();
            LogInfo("[Gerente] MenuGoverno garantido automaticamente.");
        }

        // --- AUTOMATIZAÇÃO DE SPAWN POINTS ---
        // Tenta achar as referências sozinho se o usuário esqueceu de arrastar
        if (spawnSoldado == null) 
        {
            var obj = GameObject.Find("Spawn_Soldado");
            if(obj != null) spawnSoldado = obj.transform;
            else LogInfo("[Gerente] 'Spawn_Soldado' não encontrado (Normal se não tiver base). Unidades nascerão na câmera.");
        }
        
        if (saidaSoldado == null) 
        {
            // Tenta achar saída específica ou usa o próprio spawn
            var obj = GameObject.Find("Saida_Soldado");
            if(obj != null) saidaSoldado = obj.transform;
            else if (spawnSoldado != null) saidaSoldado = spawnSoldado;
        }

        if (spawnInterno == null)
        {
            var obj = GameObject.Find("Spawn_Interno"); // Para veículos/tanques
            if(obj != null) spawnInterno = obj.transform;
            else LogInfo("[Gerente] 'Spawn_Interno' não encontrado (Normal se não tiver base). Veículos nascerão na câmera.");
        }

        if (pontoSaida == null)
        {
            var obj = GameObject.Find("Saida_Interno");
            if (obj != null) pontoSaida = obj.transform;
            else if (spawnInterno != null) pontoSaida = spawnInterno;
        }
    }

    public void RegistrarJogadorIA(IdentidadeIA ia)
    {
        if (!comandantesIA.Contains(ia))
        {
            comandantesIA.Add(ia);
            LogInfo($"[Gerente] Novo Comandante Registrado: {ia.nomeComandante} (Time {ia.teamID})");
            LogInfo($"[Gerente] Autonomia concedida para: {ia.nomeComandante}. A IA agora é reconhecida como Jogador.");
        }
    }

    void Start()
    {
        // Inicia o processamento da fila com uma Coroutine otimizada
        StartCoroutine(ProcessarFilaCoroutine());
    }

    [Header("Controle de Tempo")]
    private float _tempoApertandoTab = 0f;
    private readonly Dictionary<string, int> _slotsSaidaPorPonto = new Dictionary<string, int>();
    private const int SlotsPorPontoSaida = 9; // 3x3
    private const float IntervaloProcessamentoFilaProducao = 0.2f;
    private static readonly WaitForSeconds EsperaProcessamentoFilaProducao = new WaitForSeconds(IntervaloProcessamentoFilaProducao);
    private readonly List<GerenciadorAeroporto> _bufferAeroportosEntrega = new List<GerenciadorAeroporto>(16);

    void Update()
    {
        if (MenuPausaController.EstaPausado)
        {
            _tempoApertandoTab = 0f;
            return;
        }

        string motivoBloqueioTempo;
        if (GovernadorGameplayRTS.BloquearAceleracaoTempo(out motivoBloqueioTempo))
        {
            if (Time.timeScale > 1f)
            {
                Time.timeScale = 1f;
            }

            if (Input.GetKey(KeyCode.Tab))
            {
                _tempoApertandoTab = 0f;
                HUDAjudaRTS.MostrarMensagemTemporaria(motivoBloqueioTempo, 2.8f);
            }

            return;
        }

        // FAST-FORWARD: Acelera o tempo do jogo x2 se o usuário segurar o TAB por 2 segundos.
        if (Input.GetKey(KeyCode.Tab))
        {
            _tempoApertandoTab += Time.unscaledDeltaTime;
            if (_tempoApertandoTab >= 2.0f && Time.timeScale < 2.0f)
            {
                Time.timeScale = 2.0f;
            }
        }
        else
        {
            if (_tempoApertandoTab > 0)
            {
                _tempoApertandoTab = 0f;
                Time.timeScale = 1.0f; // Volta ao normal ao soltar!
            }
        }
    }

    [Header("Fila de Produção")]
    public List<PedidoDeProducao> filaProducao = new List<PedidoDeProducao>();

    [System.Serializable]
    public class PedidoDeProducao
    {
        public string nomeUnidade;
        public GameObject prefab;
        public float tempoTotal;
        public float tempoRestante;
        public bool ehSoldado;
        public bool ehHelicoptero;
        public bool ehNavio;
        public bool ehAviao;
        public bool ehCarrier;
    }

    private System.Collections.IEnumerator ProcessarFilaCoroutine()
    {
        while (true)
        {
            if (filaProducao.Count > 0)
            {
                // Pega o primeiro da fila
                PedidoDeProducao pedidoAtual = filaProducao[0];
                pedidoAtual.tempoRestante -= IntervaloProcessamentoFilaProducao; // Mantido em sincronia com a espera da coroutine

                if (pedidoAtual.tempoRestante <= 0)
                {
                    // Ficou pronto!
                    FinalizarProducao(pedidoAtual);
                    filaProducao.RemoveAt(0);
                }
            }
            
            // Aguarda 0.2 segundos antes de checar novamente
            yield return EsperaProcessamentoFilaProducao;
        }
    }

    // O Menu chama essa função
    public void ComprarUnidade(GameObject unidadeParaConstruir, long preco, int quantidade)
    {
        // 1. Identificar Tipo
        string nome = unidadeParaConstruir.name.ToLower();

        bool ehSoldado = EhSoldadoOuInfantaria(unidadeParaConstruir, nome);
        bool ehPredio = unidadeParaConstruir.CompareTag("Imovel") || nome.Contains("ares") || nome.Contains("torreta") || nome.Contains("missil") || nome.Contains("bunker") || nome.Contains("areas");
        bool ehNavio = (nome.Contains("navio") || nome.Contains("corveta") || nome.Contains("fragata") || nome.Contains("submarino") || nome.Contains("sub") || nome.Contains("destroier") || nome.Contains("barco") || nome.Contains("lancha") || nome.Contains("transporte") || nome.Contains("leviathan"));
        bool ehCarrier = nome.Contains("porta") || nome.Contains("carrier");

        bool ehHelicoptero = (unidadeParaConstruir.GetComponent<Helicoptero>() != null || 
                              unidadeParaConstruir.GetComponent("HelicopterController") != null ||
                              unidadeParaConstruir.GetComponent("VooHelicoptero") != null ||
                              nome.Contains("helicoptero") || nome.Contains("ray") || nome.Contains("viper") || nome.Contains("apache") || nome.Contains("heli"));

        bool ehAviao = (unidadeParaConstruir.GetComponent<ControleAviao>() != null || 
                        nome.Contains("aviao") || nome.Contains("caca") || nome.Contains("g15") || 
                        nome.Contains("jet") || nome.Contains("bomb") || nome.Contains("fighter") || nome.Contains("falcon") || nome.Contains("su11"));

        if (ehCarrier) ehNavio = true;

        LogInfo($"INFO COMPRA: '{nome}' -> Soldado? {ehSoldado}, Heli? {ehHelicoptero}, Navio? {ehNavio}, Avião? {ehAviao}");

        long custoTotal = preco * Math.Max(0, quantidade);

        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null)
        {
            Debug.LogError("❌ GerenciadorRecursos não encontrado! Crie um GameObject com este componente na cena.");
            return;
        }

        if (recursos.TentarGastar(custoDinheiro: custoTotal))
        {
            for (int i = 0; i < quantidade; i++)
            {
                PedidoDeProducao novoPedido = new PedidoDeProducao();
                novoPedido.nomeUnidade = unidadeParaConstruir.name;
                novoPedido.prefab = unidadeParaConstruir;
                novoPedido.ehSoldado = ehSoldado;
                novoPedido.ehHelicoptero = ehHelicoptero;
                novoPedido.ehNavio = ehNavio;
                novoPedido.ehAviao = ehAviao;
                novoPedido.ehCarrier = ehCarrier;
                
                float tempoBase = ehSoldado ? 0f : 2.0f;
                novoPedido.tempoTotal = tempoBase;
                novoPedido.tempoRestante = novoPedido.tempoTotal;

                filaProducao.Add(novoPedido);
            }

            LogInfo($"Adicionado à fila: {quantidade}x {unidadeParaConstruir.name}");
        }
        else
        {
            Debug.LogError($"❌ Dinheiro Insuficiente! Precisa: ${custoTotal}, Tem: ${recursos.dinheiro}");
        }
    }

    bool EhSoldadoOuInfantaria(GameObject unidadeParaConstruir, string nomeNormalizado = null)
    {
        if (unidadeParaConstruir == null)
        {
            return false;
        }

        string nome = string.IsNullOrEmpty(nomeNormalizado)
            ? unidadeParaConstruir.name.ToLowerInvariant()
            : nomeNormalizado;

        if (unidadeParaConstruir.CompareTag("Soldado"))
        {
            return true;
        }

        if (nome.Contains("soldado") ||
            nome.Contains("soldier") ||
            nome.Contains("person") ||
            nome.Contains("infantry") ||
            nome.Contains("fuzileiro") ||
            nome.Contains("rifle"))
        {
            return true;
        }

        if (unidadeParaConstruir.GetComponent<AnimacoesSoldado>() != null ||
            unidadeParaConstruir.GetComponentInChildren<AnimacoesSoldado>(true) != null)
        {
            return true;
        }

        SistemaDeDanos danos = unidadeParaConstruir.GetComponent<SistemaDeDanos>();
        if (danos == null)
        {
            danos = unidadeParaConstruir.GetComponentInChildren<SistemaDeDanos>(true);
        }

        if (danos != null && danos.unidadeBiologica)
        {
            return true;
        }

        IdentidadeUnidade identidade = unidadeParaConstruir.GetComponent<IdentidadeUnidade>();
        if (identidade == null)
        {
            identidade = unidadeParaConstruir.GetComponentInChildren<IdentidadeUnidade>(true);
        }

        return identidade != null && identidade.tipoUnidade == TipoUnidade.Infantaria;
    }

    void FinalizarProducao(PedidoDeProducao pedido)
    {
        if (pedido.ehAviao || pedido.ehHelicoptero)
        {
            // Busca o MELHOR aeroporto (um que não esteja lotado e seja do jogador)
            _bufferAeroportosEntrega.Clear();
            RegistroEntidadesJogo.FillAeroportos(_bufferAeroportosEntrega);
            GerenciadorAeroporto aeroEscolhido = null;

            for (int i = 0; i < _bufferAeroportosEntrega.Count; i++)
            {
                GerenciadorAeroporto a = _bufferAeroportosEntrega[i];
                if (a == null) continue;
                
                // EXCLUSÃO TOTAL: Aviões novos NUNCA vão para Navios (Porta-Aviões, Transportes, Hovercrafts)
                bool ehCarrier = (a is GerenciadorPortaAvioes) || (a.GetComponent<GerenciadorPortaAvioes>() != null);
                bool ehTransporte = (a.GetComponentInParent<TransporteAnfibio>() != null) || (a.GetComponentInParent<HovercraftTransporte>() != null);
                ControleUnidade controlePai = a.GetComponentInParent<ControleUnidade>();
                bool ehNaval = (controlePai != null && controlePai.EhUnidadeNaval())
                    || (a.GetComponentInParent<ControleNavioRealista>() != null)
                    || (a.GetComponentInParent<ControleSubmarino>() != null)
                    || (a.GetComponentInParent<IdentidadeNaval>() != null)
                    || (a.GetComponentInParent<NavioPetroleiro>() != null);
                bool ehCarrierNome = a.name.ToLower().Contains("carrier") || a.name.ToLower().Contains("porta") || a.name.ToLower().Contains("navio") || a.name.ToLower().Contains("ship");
                bool ehComercial = a is GerenciadorAeroportoComercial;
                
                if (ehCarrier || ehTransporte || ehNaval || ehCarrierNome || ehComercial) continue;

                bool temVagaAerea = pedido.ehHelicoptero
                    ? (a.ObterVagaHelicopteroPreferencial(false) != null)
                    : (a.ObterPrimeiraVagaLivre() != null);

                if (temVagaAerea)
                {
                    aeroEscolhido = a;
                    break;
                }
                if (!pedido.ehHelicoptero && aeroEscolhido == null) aeroEscolhido = a;
            }

            if (aeroEscolhido != null)
            {
                string tipoEntrega = pedido.ehHelicoptero ? "Helicóptero" : "Avião";
                LogInfo($"[Logística] {tipoEntrega} '{pedido.nomeUnidade}' entregue em: {aeroEscolhido.name}");
                aeroEscolhido.ComprarAviao(pedido.prefab);
            }
            else if (pedido.ehHelicoptero)
            {
                Debug.LogWarning($"[Logística] Helicóptero '{pedido.nomeUnidade}' sem vaga militar livre em aeroporto. Produção aguardando vaga.");
            }
            return; 
        }

        Transform spawnAtual = null;
        Transform destinoAtual = null;

        if (pedido.ehHelicoptero)
        {
            // Lógica Exclusiva para HELICÓPTEROS
            Heliporto heliportoDestino = ObterProximoHeliporto();
            if (heliportoDestino != null)
            {
                // ... (Lógica de Ponto Temp mantida) ...
                Vector3 pontoPouso = heliportoDestino.ObterPontoDePousoMundial();
                GameObject tempSpawn = new GameObject("TempSpawn_Heli");
                tempSpawn.transform.position = pontoPouso;
                tempSpawn.transform.rotation = heliportoDestino.transform.rotation;
                
                spawnAtual = tempSpawn.transform;
                destinoAtual = tempSpawn.transform; // Hover

                Destroy(tempSpawn, 0.1f); 
            }
            else
            {
                Debug.LogWarning("⚠️ Nenhum HELIPORTO encontrado! Helicóptero nascerá no fallback (fora do hangar).");
                 // MODIFICAÇÃO: Helicóptero NÃO deve nascer no hangar de carros.
                 spawnAtual = null; // Força fallback de Câmera/Céu
            }
        }
        else if (pedido.ehNavio)
        {
            // Lógica Exclusiva para NAVIOS (Estaleiros)
            PontoLogistico estaleiro = ObterProximoEstaleiro();
            if (estaleiro != null && estaleiro.spawn != null)
            {
                spawnAtual = estaleiro.spawn;
                destinoAtual = estaleiro.saida;
            }
            else
            {
                Debug.LogWarning("⚠️ Nenhum ESTALEIRO encontrado! Navio nascerá na água perto da câmera (fallback).");
                // Tenta achar água... ou usa fallback padrão
                if (Camera.main != null)
                {
                    spawnAtual = Camera.main.transform; // Só para não ser null
                }
            }
        }
        else
        {
            // Lógica Padrão (Soldado / Veículo Terrestre)
            PontoLogistico logistica = ObterProximoSpawn(pedido.ehSoldado);
        
            if (logistica != null && logistica.spawn != null)
            {
                spawnAtual = logistica.spawn;
                destinoAtual = logistica.saida;
            }
            else
            {
                // Fallback para variáveis legadas (Inspector ou último registrado)
                spawnAtual = pedido.ehSoldado ? spawnSoldado : spawnInterno;
                destinoAtual = pedido.ehSoldado ? saidaSoldado : pontoSaida;
            }
        }

        if(spawnAtual != null) LogInfo($"SPAWNANDO EM: {spawnAtual.name} (Parente: {spawnAtual.parent?.name ?? "World"})");
        else Debug.LogWarning("SPAWNANDO SEM FÁBRICA (NULL)");

        if (pedido.prefab == null)
        {
            Debug.LogError($"ERRO CRÍTICO: O prefab do pedido '{pedido.nomeUnidade}' está NULO! Verifique o ScriptableObject.");
            return;
        }

        // FALLBACK: Se não tiver fábrica, nasce no GerenteDeJogo + Offset
        // Ajuste: Y + 2.0f para garantir que não nasce enterrado
        Vector3 posNascimento;
        Quaternion rotNascimento;
        Vector3 posDestino;

        if(spawnAtual != null)
        {
            posNascimento = spawnAtual.position;
            rotNascimento = spawnAtual.rotation;
        }
        else
        {
            // FALLBACK MELHORADO: Tenta nascer em um ponto seguro do mapa se não houver prédios
            GameObject baseAres = GameObject.Find("Base_Ares");
            if (baseAres != null)
            {
                posNascimento = baseAres.transform.position + Vector3.up * 2f;
                rotNascimento = baseAres.transform.rotation;
            }
            else if (Camera.main != null)
            {
                // Nasce na frente da câmera, mas tenta fixar no chão
                posNascimento = Camera.main.transform.position + (Camera.main.transform.forward * 15f);
                posNascimento.y = 50f; 
                RaycastHit hitChao;
                if (Physics.Raycast(posNascimento, Vector3.down, out hitChao, 100f)) posNascimento = hitChao.point;
                rotNascimento = Quaternion.identity;
            }
            else
            {
                posNascimento = transform.position + new Vector3(3, 2, 0);
                rotNascimento = Quaternion.identity;
            }
            
            Debug.LogWarning($"[Logistica] Sem fábrica disponível para {pedido.nomeUnidade}. Usando fallback em {posNascimento}");
        }

        if(destinoAtual != null) posDestino = destinoAtual.position;
        else posDestino = posNascimento + new Vector3(2, 0, 2);

        if (pedido.ehSoldado && spawnAtual != null)
        {
            Vector3 frenteSaida = destinoAtual != null ? destinoAtual.forward : spawnAtual.forward;
            frenteSaida.y = 0f;
            if (frenteSaida.sqrMagnitude < 0.01f) frenteSaida = Vector3.forward;
            frenteSaida.Normalize();

            float distanciaSaida = Vector3.Distance(posNascimento, posDestino);
            if (distanciaSaida < 4f)
            {
                posDestino = posNascimento + (frenteSaida * 12f);
            }
        }


        // CORREÇÃO DE ALTURA (Spawn Height Check)
        if (pedido.ehHelicoptero)
        {
            // Raycast para cima do ponto de spawn para encontrar o chão real (evitar clipping no Heliporto)
            RaycastHit hitHeli;
            // Tenta raycast vindo bem de cima
            Vector3 pontoDeTeste = new Vector3(posNascimento.x, posNascimento.y + 20f, posNascimento.z);
            if (Physics.Raycast(pontoDeTeste, Vector3.down, out hitHeli, 50f))
            {
                posNascimento.y = hitHeli.point.y; // Ajusta para o chão encontrado
            }
            // Adiciona altura segura para garantir que não colida com o box do heliporto
            posNascimento += Vector3.up * 1.5f; 
        }
        else if (!pedido.ehNavio) // Terrestre
        {
            // --- CORREÇÃO DE NAVMESH PRÉ-INSTANTIATE ---
            // Verifica NavMesh ANTES de nascer para evitar erro do Unity
             UnityEngine.AI.NavMeshHit hitNav;
             if (UnityEngine.AI.NavMesh.SamplePosition(posNascimento, out hitNav, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
             {
                 posNascimento = hitNav.position; // Posição segura no NavMesh
                 posNascimento += Vector3.up * 0.1f; // Leve offset Y
             }
             else
             {
                 // Tenta raio maior
                 if (UnityEngine.AI.NavMesh.SamplePosition(posNascimento, out hitNav, 10.0f, UnityEngine.AI.NavMesh.AllAreas))
                 {
                     posNascimento = hitNav.position;
                 }
             }

             if (pedido.ehSoldado)
             {
                 UnityEngine.AI.NavMeshHit hitDestinoSoldado;
                 if (UnityEngine.AI.NavMesh.SamplePosition(posDestino, out hitDestinoSoldado, 10.0f, UnityEngine.AI.NavMesh.AllAreas))
                 {
                     posDestino = hitDestinoSoldado.position;
                 }
             }
        }
        else
        {
             // Navio: Só garante altura da água
             // posNascimento.y = 0; // Opcional
        }

        // NASCER
        GameObject novaUnidade = Instantiate(pedido.prefab, posNascimento, rotNascimento);
        
        if (novaUnidade == null)
        {
            Debug.LogError("ERRO: Instantiate falhou! O objeto não foi criado.");
            return;
        }

        // --- DEFINIR IDENTIDADE (O GerenteDeJogo do Jogador sempre cria unidades para o Time 1) ---
        IdentidadeUnidade identidade = novaUnidade.GetComponent<IdentidadeUnidade>();
        if (identidade == null)
        {
            identidade = novaUnidade.AddComponent<IdentidadeUnidade>();
            LogInfo($"[Gerente] Adicionei RG na marra em: {novaUnidade.name}");
        }
        
        // Garante que a unidade pertence ao jogador
        identidade.teamID = 1; 
        identidade.nomeDoPais = "Hegemonia";

        // Se tiver controle de unidade, garante que está configurado
        ControleUnidade controle = novaUnidade.GetComponent<ControleUnidade>();
        if (controle == null) controle = novaUnidade.AddComponent<ControleUnidade>();

        // Reativa NavMesh e logicamente
        UnityEngine.AI.NavMeshAgent agent = novaUnidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) 
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }
        
        // --- CORREÇÃO DE FÍSICA (IMPEDE VOAR) ---
        Rigidbody rb = novaUnidade.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Desliga a física de colisão bruta para o NavMesh funcionar em paz
            rb.useGravity = false; // NavMesh já cola no chão
        }

        // VERIFICAÇÃO VISUAL
        if(novaUnidade.GetComponentsInChildren<Renderer>().Length == 0)
        {
            Debug.LogWarning($"ALERTA: A unidade '{novaUnidade.name}' foi criada mas NÃO TEM RENDERERS (está invisível?). Verifique o Prefab.");
        }
        
        // --- CHECAGEM DE SOM ---
        SomUnidade somUnidade = novaUnidade.GetComponent<SomUnidade>();
        if (somUnidade == null)
        {
            somUnidade = novaUnidade.AddComponent<SomUnidade>();

            string nomeAudio = novaUnidade.name.ToLowerInvariant();
            if (nomeAudio.Contains("heli") || nomeAudio.Contains("ray") || nomeAudio.Contains("falcon"))
                somUnidade.tipoUnidade = TipoSomUnidade.Helicoptero;
            else if (nomeAudio.Contains("a_20") || nomeAudio.Contains("g_18") || nomeAudio.Contains("g15") || nomeAudio.Contains("tuk") || nomeAudio.Contains("aviao") || nomeAudio.Contains("su11"))
                somUnidade.tipoUnidade = TipoSomUnidade.Aviao;
            else if (nomeAudio.Contains("tank") || nomeAudio.Contains("tanque"))
                somUnidade.tipoUnidade = TipoSomUnidade.Tank;
            else if (nomeAudio.Contains("nav") || nomeAudio.Contains("destroyer") || nomeAudio.Contains("corveta") || nomeAudio.Contains("wall") || nomeAudio.Contains("uss"))
                somUnidade.tipoUnidade = TipoSomUnidade.Navio;
            else
                somUnidade.tipoUnidade = TipoSomUnidade.Carro;

            Debug.LogWarning($"[Audio] Unidade '{novaUnidade.name}' criada sem componente 'SomUnidade'. Fallback automatico aplicado.", novaUnidade);
        }

        // DEBUG DE DESTINO
        // LogInfo($"DESTINO CALCULADO: {posDestino} (Alvo: {(destinoAtual != null ? destinoAtual.name : "Fallback")})");
        // Debug.DrawLine(posNascimento, posDestino, Color.yellow, 10.0f); // Desenha linha amarela na Scene por 10s

        if(posDestino == Vector3.zero)
        {
             Debug.LogError("ERRO GRAVE: O Destino está (0,0,0)! Verifique se o 'Ponto_Saida' no Prefab do Hangar está na posição certa (fora da origem).");
        }

        // MOVER
        if (controle != null)
        {
            // Se tiver NavMeshAgent, usa lógica robusta de posicionamento
            if(agent != null) 
            {
                // Já posicionamos no NavMesh antes, mas garantir o Warp nunca é demais se o Instantiate tiver movido
                if (agent.isActiveAndEnabled && !agent.isOnNavMesh)
                {
                    UnityEngine.AI.NavMeshHit hitWarp;
                    if (UnityEngine.AI.NavMesh.SamplePosition(novaUnidade.transform.position, out hitWarp, 15.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agent.Warp(hitWarp.position);
                    }
                    else
                    {
                        // Fallback: Se não achar NavMesh perto do nascimento, warp para a saída
                        UnityEngine.AI.NavMeshHit hitDest;
                        if (UnityEngine.AI.NavMesh.SamplePosition(posDestino, out hitDest, 15.0f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            agent.Warp(hitDest.position);
                        }
                        else
                        {
                            agent.Warp(posDestino);
                        }
                    }
                }
            }
            
            // Tenta mover para um slot organizado na saida, evitando espalhamento aleatorio.
            // MODIFICAÇÃO: Randomizar levemente o destino para evitar fila indiana perfeita e colisão
            Vector3 destinoFinal = CalcularDestinoSaidaOrganizada(posDestino, destinoAtual, novaUnidade);
            
            // Adiciona variação aleatória de 3m ao redor do ponto de saída
            controle.EmitirOrdemMover(destinoFinal);
        }

        LogInfo($"SUCESSO: Saiu da fábrica: {pedido.nomeUnidade}");
    }

    Vector3 CalcularDestinoSaidaOrganizada(Vector3 destinoBase, Transform referenciaSaida, GameObject unidade)
    {
        string chave = ObterChaveSaida(referenciaSaida, destinoBase);
        int slotAtual = 0;
        if (_slotsSaidaPorPonto.ContainsKey(chave))
        {
            slotAtual = _slotsSaidaPorPonto[chave];
        }

        _slotsSaidaPorPonto[chave] = (slotAtual + 1) % SlotsPorPontoSaida;

        const int colunas = 3; // Grade 3x3
        int coluna = slotAtual % colunas;
        int linha = slotAtual / colunas;

        float largura;
        float profundidade;
        ObterPegadaUnidadeParaSaida(unidade, out largura, out profundidade);

        float passoX = Mathf.Max(2.5f, largura * 1.15f);
        float passoZ = Mathf.Max(2.5f, profundidade * 1.20f);

        float offsetX = (coluna - 1) * passoX;
        float offsetZ = (linha + 1) * passoZ;

        Vector3 direita = (referenciaSaida != null) ? referenciaSaida.right : Vector3.right;
        Vector3 frente = (referenciaSaida != null) ? referenciaSaida.forward : Vector3.forward;
        direita.y = 0f;
        frente.y = 0f;

        if (direita.sqrMagnitude < 0.01f) direita = Vector3.right;
        if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;

        direita.Normalize();
        frente.Normalize();

        bool ehHelicoptero = unidade != null && unidade.GetComponent<Helicoptero>() != null;
        if (ehHelicoptero)
        {
            offsetX = 0f;
            offsetZ = (slotAtual + 1) * passoZ;
        }

        Vector3 destino = destinoBase + (direita * offsetX) + (frente * offsetZ);

        if (EhUnidadeNavalParaSaida(unidade))
        {
            // Evita "snap" de destino naval para navmesh terrestre (topo do estaleiro/pier).
            destino.y = NavalPlacementResolver.ResolveSeaLevel();
            return destino;
        }

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(destino, out hit, 8f, UnityEngine.AI.NavMesh.AllAreas))
        {
            destino = hit.position;
        }

        return destino;
    }

    bool EhUnidadeNavalParaSaida(GameObject unidade)
    {
        if (unidade == null)
        {
            return false;
        }

        if (unidade.GetComponent<IdentidadeNaval>() != null
            || unidade.GetComponent<ControleNavioRealista>() != null
            || unidade.GetComponent<ControleSubmarino>() != null
            || unidade.GetComponent<NavioPetroleiro>() != null
            || unidade.GetComponent<HovercraftTransporte>() != null)
        {
            return true;
        }

        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
        return identidade != null && identidade.tipoUnidade == TipoUnidade.Naval;
    }

    string ObterChaveSaida(Transform referenciaSaida, Vector3 destinoBase)
    {
        if (referenciaSaida != null)
            return "saida_" + referenciaSaida.GetInstanceID();

        return "pos_" + Mathf.RoundToInt(destinoBase.x) + "_" + Mathf.RoundToInt(destinoBase.z);
    }

    void ObterPegadaUnidadeParaSaida(GameObject unidade, out float largura, out float profundidade)
    {
        largura = 2.5f;
        profundidade = 2.5f;
        if (unidade == null) return;

        bool temBounds = false;
        Bounds bounds = new Bounds(unidade.transform.position, Vector3.zero);

        Collider[] colliders = unidade.GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
        {
            if (c == null || !c.enabled || c.isTrigger) continue;

            if (!temBounds)
            {
                bounds = c.bounds;
                temBounds = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        if (temBounds)
        {
            largura = Mathf.Max(largura, bounds.size.x);
            profundidade = Mathf.Max(profundidade, bounds.size.z);
        }

        var agent = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            float diametro = Mathf.Max(2.5f, agent.radius * 2f);
            largura = Mathf.Max(largura, diametro);
            profundidade = Mathf.Max(profundidade, diametro);
        }

        largura = Mathf.Clamp(largura, 2.5f, 40f);
        profundidade = Mathf.Clamp(profundidade, 2.5f, 40f);
    }

    void OnDrawGizmos()
    {
        // Desenha uma bola vermelha onde seria o spawn de fallback
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawSphere(transform.position + new Vector3(3, 2, 0), 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + new Vector3(3, 2, 0));
    }

    // Mantido para compatibilidade com o Construtor.cs (Sobrecarga antiga)
    public void ComprarUnidade(GameObject unidade, long preco)
    {
        ComprarUnidade(unidade, preco, 1);
    }



    // Mantido para compatibilidade com o Construtor.cs
    public bool TentarGastarDinheiro(long custo)
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
        {
            return recursos.TentarGastar(custoDinheiro: custo);
        }
        
        // Fallback se o GerenciadorRecursos não existir
        Debug.LogWarning("⚠️ GerenciadorRecursos não encontrado! Aprovando transação em modo legado para evitar travamento da UI.");
        return true;
    }

    // --- MÉTODOS DE REGISTRO (Chamados pelo script Fabrica.cs dos prédios) ---
    // STRUCT INTERNA PARA GUARDAR OS PARES (SPAWN + SAIDA)
    [System.Serializable]
    public class PontoLogistico
    {
        public Transform spawn;
        public Transform saida;
    }

    // Listas para suportar múltiplos prédios
    public List<PontoLogistico> listaQuarteis = new List<PontoLogistico>();
    public List<PontoLogistico> listaHangares = new List<PontoLogistico>();
    public List<PontoLogistico> listaEstaleiros = new List<PontoLogistico>();
    public List<Heliporto> listaHeliportos = new List<Heliporto>();

    // Índices para Round-Robin
    private int indexQuartel = 0;
    private int indexHangar = 0;
    private int indexEstaleiro = 0;
    private int indexHeliporto = 0;

    public void AtualizarPontoQuartel(Transform nascimento, Transform saida)
    {
        // Mantém compatibilidade com variáveis antigas (aponta para o último)
        spawnSoldado = nascimento;
        saidaSoldado = saida;

        // Adiciona na lista se não estiver
        if (!ListaContem(listaQuarteis, nascimento))
        {
            listaQuarteis.Add(new PontoLogistico { spawn = nascimento, saida = saida });
            LogInfo($"Logística: Nova TENDA registrada (Total: {listaQuarteis.Count})");
        }
    }

    public void AtualizarPontoHangar(Transform nascimento, Transform saida)
    {
        // Mantém compatibilidade
        spawnInterno = nascimento;
        pontoSaida = saida;

        // Adiciona na lista
        if (!ListaContem(listaHangares, nascimento))
        {
            listaHangares.Add(new PontoLogistico { spawn = nascimento, saida = saida });
            LogInfo($"Logística: Novo HANGAR registrado (Total: {listaHangares.Count})");
        }
    }

    public void AtualizarPontoEstaleiro(Transform nascimento, Transform saida)
    {
        if (nascimento == null)
        {
            return;
        }

        PontoLogistico existente = listaEstaleiros.Find(x => x != null && x.spawn == nascimento);
        if (existente != null)
        {
            // Se o estaleiro jÃ¡ existia, atualiza a saida para nÃ£o ficar preso em valor antigo/null.
            if (saida != null)
            {
                existente.saida = saida;
            }
            return;
        }

        if (!ListaContem(listaEstaleiros, nascimento))
        {
            listaEstaleiros.Add(new PontoLogistico { spawn = nascimento, saida = saida });
            LogInfo($"Logística: Novo ESTALEIRO registrado (Total: {listaEstaleiros.Count})");
        }
    }

    public void RegistrarHeliporto(Heliporto heliporto)
    {
        IdentidadeUnidade id = heliporto.GetComponent<IdentidadeUnidade>();
        if (id == null) id = heliporto.GetComponentInParent<IdentidadeUnidade>();

        if (id != null && id.teamID != 1) return; // Não registra heliporto da IA no Gestor do Jogador

        if (!listaHeliportos.Contains(heliporto))
        {
            listaHeliportos.Add(heliporto);
            LogInfo($"Logística: Novo HELIPORTO registrado (Total: {listaHeliportos.Count})");
        }
    }

    bool ListaContem(List<PontoLogistico> lista, Transform t)
    {
        return lista.Exists(x => x.spawn == t);
    }

    PontoLogistico ObterProximoSpawn(bool ehSoldado)
    {
        List<PontoLogistico> lista = ehSoldado ? listaQuarteis : listaHangares;
        
        // Limpeza de Nulos (caso prédio tenha sido destruído)
        lista.RemoveAll(x => x.spawn == null);

        if (lista.Count == 0) return null;

        // Tenta encontrar um spawn válido no round-robin
        for (int i = 0; i < lista.Count; i++)
        {
            // Incrementa o índice
            if (ehSoldado) 
                indexQuartel = (indexQuartel + 1) % lista.Count;
            else 
                indexHangar = (indexHangar + 1) % lista.Count;

            PontoLogistico candidato = ehSoldado ? lista[indexQuartel] : lista[indexHangar];
            
            if (candidato != null && candidato.spawn != null)
            {
                // PROTEÇÃO EXTRA: Checa o nome do objeto PAI do spawn
                string nomePai = candidato.spawn.parent != null ? candidato.spawn.parent.name.ToLower() : candidato.spawn.name.ToLower();
                
                // Se for unidade terrestre e o spawn for naval, PULA
                if (!ehSoldado && (nomePai.Contains("naval") || nomePai.Contains("navio") || nomePai.Contains("estaleiro") || nomePai.Contains("pier") || nomePai.Contains("liberty")))
                {
                    Debug.LogWarning($"[Logistica] Ignorando spawn naval '{nomePai}' para unidade terrestre.");
                    continue; // Tenta o próximo
                }

                return candidato;
            }
        }

        Debug.LogWarning($"[Logistica] Fim da lista. Nenhum spawn válido encontrado ({lista.Count} total).");
        return null;
    }

    PontoLogistico ObterProximoEstaleiro()
    {
        listaEstaleiros.RemoveAll(x => x.spawn == null);
        if (listaEstaleiros.Count == 0) return null;

        indexEstaleiro = (indexEstaleiro + 1) % listaEstaleiros.Count;
        return listaEstaleiros[indexEstaleiro];
    }

    Heliporto ObterProximoHeliporto()
    {
        // 1. Limpeza de nulos (se o prédio foi destruído)
        listaHeliportos.RemoveAll(h => h == null);
        
        if (listaHeliportos.Count == 0) return null;

        // 2. Round Robin
        indexHeliporto = (indexHeliporto + 1) % listaHeliportos.Count;
        
        // 3. Retorna o próximo da fila
        return listaHeliportos[indexHeliporto];
    }
}
