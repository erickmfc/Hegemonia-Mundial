using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class GerenteDeJogo : MonoBehaviour
{
    [Header("Economia - DEPRECATED: Use GerenciadorRecursos.Instancia")]
    [Tooltip("Deprecated: Este campo agora é gerenciado pelo GerenciadorRecursos")]
    public int dinheiroAtual 
    { 
        get { return GerenciadorRecursos.Instancia != null ? GerenciadorRecursos.Instancia.dinheiro : 5000; }
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

    void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);

        // --- SISTEMA DE UI: MENU PIER (Tecla V) E MENU GOVERNO (Tecla X) ---
        if (GetComponent<MenuPier>() == null)
        {
            gameObject.AddComponent<MenuPier>();
            Debug.Log("[Gerente] MenuPier adicionado automaticamente.");
        }
        if (GetComponent<MenuGoverno>() == null)
        {
            gameObject.AddComponent<MenuGoverno>();
            Debug.Log("[Gerente] MenuGoverno adicionado automaticamente.");
        }

        // --- AUTOMATIZAÇÃO DE SPAWN POINTS ---
        // Tenta achar as referências sozinho se o usuário esqueceu de arrastar
        if (spawnSoldado == null) 
        {
            var obj = GameObject.Find("Spawn_Soldado");
            if(obj != null) spawnSoldado = obj.transform;
            else Debug.Log("[Gerente] 'Spawn_Soldado' não encontrado (Normal se não tiver base). Unidades nascerão na câmera.");
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
            else Debug.Log("[Gerente] 'Spawn_Interno' não encontrado (Normal se não tiver base). Veículos nascerão na câmera.");
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
            Debug.Log($"[Gerente] Novo Comandante Registrado: {ia.nomeComandante} (Time {ia.teamID})");
            Debug.Log($"[Gerente] Autonomia concedida para: {ia.nomeComandante}. A IA agora é reconhecida como Jogador.");
        }
    }

    void Start()
    {
        // Inicia o processamento da fila com uma Coroutine otimizada
        StartCoroutine(ProcessarFilaCoroutine());
    }

    [Header("Controle de Tempo")]
    private float _tempoApertandoTab = 0f;

    void Update()
    {
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
    }

    private System.Collections.IEnumerator ProcessarFilaCoroutine()
    {
        while (true)
        {
            if (filaProducao.Count > 0)
            {
                // Pega o primeiro da fila
                PedidoDeProducao pedidoAtual = filaProducao[0];
                pedidoAtual.tempoRestante -= 0.2f; // Subtrai o intervalo da coroutine

                if (pedidoAtual.tempoRestante <= 0)
                {
                    // Ficou pronto!
                    FinalizarProducao(pedidoAtual);
                    filaProducao.RemoveAt(0);
                }
            }
            
            // Aguarda 0.2 segundos antes de checar novamente
            yield return new WaitForSeconds(0.2f);
        }
    }

    // O Menu chama essa função
    public void ComprarUnidade(GameObject unidadeParaConstruir, int preco, int quantidade)
    {
        // 1. Identificar Tipo
        string nome = unidadeParaConstruir.name.ToLower();
        
        bool ehSoldado = (nome.Contains("soldado") || nome.Contains("soldier") || nome.Contains("person") || nome.Contains("infantry") || nome.Contains("fuzileiro"));
        bool ehNavio = (nome.Contains("navio") || nome.Contains("corveta") || nome.Contains("fragata") || nome.Contains("submarino") || nome.Contains("destroier") || nome.Contains("porta") || nome.Contains("barco") || nome.Contains("lancha"));

        bool ehHelicoptero = (unidadeParaConstruir.GetComponent<Helicoptero>() != null || 
                              unidadeParaConstruir.GetComponent("HelicopterController") != null ||
                              unidadeParaConstruir.GetComponent("VooHelicoptero") != null ||
                              nome.Contains("helicoptero") || nome.Contains("ray") || nome.Contains("viper") || nome.Contains("apache") || nome.Contains("heli"));

        bool ehAviao = (unidadeParaConstruir.GetComponent<ControleAviao>() != null || 
                        nome.Contains("aviao") || nome.Contains("caca") || nome.Contains("g15") || 
                        nome.Contains("jet") || nome.Contains("bomb") || nome.Contains("fighter") || nome.Contains("falcon"));

        Debug.Log($"INFO COMPRA: '{nome}' -> Soldado? {ehSoldado}, Heli? {ehHelicoptero}, Navio? {ehNavio}, Avião? {ehAviao}");

        // 2. Verificar se a FÁBRICA existe (Impedir aparecer na câmera do nada!)
        if (ehSoldado)
        {
            listaQuarteis.RemoveAll(q => q.spawn == null);
            if (listaQuarteis.Count == 0 && spawnSoldado == null)
            {
                Debug.LogWarning("PROIBIDO: Você precisa construir uma TENDA/QUARTEL antes de treinar soldados!");
                return; // Cancela compra e não gasta o dinheiro
            }
        }
        else if (ehNavio)
        {
            listaEstaleiros.RemoveAll(e => e.spawn == null);
            if (listaEstaleiros.Count == 0)
            {
                Debug.LogWarning("PROIBIDO: Você precisa construir um ESTALEIRO NAVAL antes de fabricar navios!");
                return; // Cancela compra
            }
        }
        else if (!ehHelicoptero && !ehAviao) // Fica sendo Veículo Terrestre
        {
            listaHangares.RemoveAll(h => h.spawn == null);
            if (listaHangares.Count == 0 && spawnInterno == null)
            {
                Debug.LogWarning("PROIBIDO: Você precisa construir um HANGAR/FÁBRICA antes de fabricar blindados ou veículos pesados!");
                return; // Cancela compra
            }
        }

        // VERIFICAÇÃO ESTRITA: Helicópteros SÓ no Heliporto
        if (ehHelicoptero)
        {
            listaHeliportos.RemoveAll(h => h == null);
            if (listaHeliportos.Count == 0)
            {
                Debug.LogWarning("PROIBIDO: Você precisa construir um HELIPORTO antes de fabricar Helicópteros!");
                // Opcional: Aqui poderíamos chamar algum UI Error na tela.
                return; // Cancela antes de gastar dinheiro ou entrar na fila
            }
        }

        if (ehAviao && FindFirstObjectByType<GerenciadorAeroporto>() == null)
        {
            Debug.LogWarning("PROIBIDO: Você precisa possuir o AEROPORTO NA CENA antes de comprar aviões ou caças táticos!");
            return;
        }

        // 3. Verifica Dinheiro Total
        int custoTotal = preco * quantidade;

        // Usa o novo sistema de recursos
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos == null)
        {
            Debug.LogError("❌ GerenciadorRecursos não encontrado! Crie um GameObject com este componente na cena.");
            return;
        }

        if (recursos.TentarGastar(custoDinheiro: custoTotal))
        {
            // 4. Adiciona na Fila (Um pedido para cada unidade)
            for (int i = 0; i < quantidade; i++)
            {
                PedidoDeProducao novoPedido = new PedidoDeProducao();
                novoPedido.nomeUnidade = unidadeParaConstruir.name;
                novoPedido.prefab = unidadeParaConstruir;
                novoPedido.ehSoldado = ehSoldado;
                
                novoPedido.ehSoldado = ehSoldado;
                novoPedido.ehHelicoptero = ehHelicoptero;
                novoPedido.ehNavio = ehNavio;
                novoPedido.ehAviao = ehAviao;
                
                // Tempo de Produção: 0s para Soldado (Instantâneo), 2s para Tanque/Heli
                float tempoBase = ehSoldado ? 0f : 2.0f;
                // Se for helicóptero, pode ter um tempo diferente se quiser
                
                novoPedido.tempoTotal = tempoBase;
                novoPedido.tempoRestante = novoPedido.tempoTotal;

                filaProducao.Add(novoPedido);
            }

            Debug.Log($"Adicionado à fila: {quantidade}x {unidadeParaConstruir.name}");
        }
        else
        {
            Debug.LogError($"❌ Dinheiro Insuficiente! Precisa: ${custoTotal}, Tem: ${recursos.dinheiro}");
        }
    }

    void FinalizarProducao(PedidoDeProducao pedido)
    {
        if (pedido.ehAviao)
        {
            GerenciadorAeroporto aero = FindFirstObjectByType<GerenciadorAeroporto>();
            if (aero != null)
            {
                aero.ComprarAviao(pedido.prefab);
            }
            else
            {
                Debug.LogError($"ERRO: O Avião '{pedido.nomeUnidade}' terminou a produção mas o Aeroporto sumiu!");
            }
            return; // Corta aqui a verificação padrão de fábricas terrestres. O próprio Aeroporto Instancia.
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

        if(spawnAtual != null) Debug.Log($"SPAWNANDO EM: {spawnAtual.name} (Parente: {spawnAtual.parent?.name ?? "World"})");
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
            // FALLBACK MELHORADO: Nascer na frente da Câmera para o jogador VER que funcionou
            if (Camera.main != null)
            {
                posNascimento = Camera.main.transform.position + (Camera.main.transform.forward * 10f);
                posNascimento.y = 10f; // Força altura para cair
                // Raycast para achar o chão
                RaycastHit hitChao;
                if (Physics.Raycast(posNascimento + Vector3.up * 50, Vector3.down, out hitChao, 100f))
                {
                    posNascimento = hitChao.point;
                }
            }
            else
            {
                posNascimento = transform.position + new Vector3(3, 2, 0);
            }

            rotNascimento = Quaternion.identity;
            Debug.LogWarning($"Usando Spawn de Fallback (Frente da Câmera) para: {pedido.nomeUnidade}. Motivo: Fábrica não encontrada (spawnSoldado/spawnInterno é null).");
        }

        if(destinoAtual != null) posDestino = destinoAtual.position;
        else posDestino = posNascimento + new Vector3(2, 0, 2);


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

        // --- DEFINIR IDENTIDADE (TeamID 1 = Jogador) ---
        IdentidadeUnidade identidade = novaUnidade.GetComponent<IdentidadeUnidade>();
        if (identidade == null)
        {
            // Se não tiver, adiciona na hora
            identidade = novaUnidade.AddComponent<IdentidadeUnidade>();
            Debug.Log($"[Gerente] Adicionei RG na marra em: {novaUnidade.name}");
        }
        
        identidade.teamID = 1;
        identidade.nomeDoPais = "Minha Nação";
        
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
        if (novaUnidade.GetComponent<SomUnidade>() == null)
        {
            Debug.LogWarning($"[Audio] Unidade '{novaUnidade.name}' criada sem componente 'SomUnidade' (Gerente Player)! Adicione ao Prefab.");
        }

        // DEBUG DE DESTINO
        // Debug.Log($"DESTINO CALCULADO: {posDestino} (Alvo: {(destinoAtual != null ? destinoAtual.name : "Fallback")})");
        // Debug.DrawLine(posNascimento, posDestino, Color.yellow, 10.0f); // Desenha linha amarela na Scene por 10s

        if(posDestino == Vector3.zero)
        {
             Debug.LogError("ERRO GRAVE: O Destino está (0,0,0)! Verifique se o 'Ponto_Saida' no Prefab do Hangar está na posição certa (fora da origem).");
        }

        // MOVER
        ControleUnidade controle = novaUnidade.GetComponent<ControleUnidade>();
        if (controle != null)
        {
            // Se tiver NavMeshAgent, usa lógica robusta de posicionamento
            UnityEngine.AI.NavMeshAgent agent = novaUnidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if(agent != null) 
            {
                // Já posicionamos no NavMesh antes, mas garantir o Warp nunca é demais se o Instantiate tiver movido
                if (agent.isActiveAndEnabled && !agent.isOnNavMesh)
                {
                    UnityEngine.AI.NavMeshHit hitWarp;
                    if (UnityEngine.AI.NavMesh.SamplePosition(novaUnidade.transform.position, out hitWarp, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agent.Warp(hitWarp.position);
                    }
                }
            }
            
            // Tenta mover. Se colidir, o NavMeshAgent lida com isso.
            // MODIFICAÇÃO: Randomizar levemente o destino para evitar fila indiana perfeita e colisão
            Vector3 destinoFinal = posDestino;
            
            // Adiciona variação aleatória de 3m ao redor do ponto de saída
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * 3.0f; 
            destinoFinal += new Vector3(randomCircle.x, 0, randomCircle.y);

            controle.MoverParaPonto(destinoFinal);
        }

        Debug.Log($"SUCESSO: Saiu da fábrica: {pedido.nomeUnidade}");
    }

    void OnDrawGizmos()
    {
        // Desenha uma bola vermelha onde seria o spawn de fallback
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawSphere(transform.position + new Vector3(3, 2, 0), 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + new Vector3(3, 2, 0));
    }

    // Mantido para compatibilidade com o Construtor.cs (Sobrecarga antiga)
    public void ComprarUnidade(GameObject unidade, int preco)
    {
        ComprarUnidade(unidade, preco, 1);
    }



    // Mantido para compatibilidade com o Construtor.cs
    public bool TentarGastarDinheiro(int custo)
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
        {
            return recursos.TentarGastar(custoDinheiro: custo);
        }
        
        // Fallback se o GerenciadorRecursos não existir
        Debug.LogWarning("⚠️ GerenciadorRecursos não encontrado! Usando sistema legado.");
        return false;
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
            Debug.Log($"Logística: Nova TENDA registrada (Total: {listaQuarteis.Count})");
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
            Debug.Log($"Logística: Novo HANGAR registrado (Total: {listaHangares.Count})");
        }
    }

    public void AtualizarPontoEstaleiro(Transform nascimento, Transform saida)
    {
        if (!ListaContem(listaEstaleiros, nascimento))
        {
            listaEstaleiros.Add(new PontoLogistico { spawn = nascimento, saida = saida });
            Debug.Log($"Logística: Novo ESTALEIRO registrado (Total: {listaEstaleiros.Count})");
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
            Debug.Log($"Logística: Novo HELIPORTO registrado (Total: {listaHeliportos.Count})");
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
