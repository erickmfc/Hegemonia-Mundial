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

        // --- SISTEMA DE UI: MENU PIER (Tecla V) ---
        if (GetComponent<MenuPier>() == null)
        {
            gameObject.AddComponent<MenuPier>();
            Debug.Log("[Gerente] MenuPier adicionado automaticamente.");
        }

        // --- AUTOMATIZAÇÃO DE SPAWN POINTS ---
        // Tenta achar as referências sozinho se o usuário esqueceu de arrastar
        if (spawnSoldado == null) 
        {
            var obj = GameObject.Find("Spawn_Soldado");
            if(obj != null) spawnSoldado = obj.transform;
            else Debug.LogWarning("[Gerente] 'Spawn_Soldado' não encontrado na cena! Unidades nascerão na câmera.");
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
            else Debug.LogWarning("[Gerente] 'Spawn_Interno' não encontrado! Veículos nascerão na câmera.");
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
        // AtualizarPainel() removido - agora gerenciado pelo PainelRecursos
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
    }

    void Update()
    {
        ProcessarFila();
    }

    void ProcessarFila()
    {
        if (filaProducao.Count > 0)
        {
            // Pega o primeiro da fila
            PedidoDeProducao pedidoAtual = filaProducao[0];
            pedidoAtual.tempoRestante -= Time.deltaTime;

            if (pedidoAtual.tempoRestante <= 0)
            {
                // Ficou pronto!
                FinalizarProducao(pedidoAtual);
                filaProducao.RemoveAt(0);
            }
        }
    }

    // O Menu chama essa função
    public void ComprarUnidade(GameObject unidadeParaConstruir, int preco, int quantidade)
    {
        // 1. Identificar Tipo
        string nome = unidadeParaConstruir.name.ToLower();
        // REMOVIDO "variant" POIS CAUSAVA CONFUSÃO COM TANQUES
        // REMOVIDO "variant" POIS CAUSAVA CONFUSÃO COM TANQUES
        bool ehSoldado = (nome.Contains("soldado") || nome.Contains("soldier") || nome.Contains("person") || nome.Contains("infantry") || nome.Contains("fuzileiro"));
        bool ehHelicoptero = (nome.Contains("helicoptero") || nome.Contains("ray") || nome.Contains("viper") || nome.Contains("apache"));
        bool ehNavio = (nome.Contains("navio") || nome.Contains("corveta") || nome.Contains("fragata") || nome.Contains("submarino") || nome.Contains("destroier") || nome.Contains("porta") || nome.Contains("barco") || nome.Contains("lancha"));

        Debug.Log($"INFO COMPRA: '{nome}' -> Soldado? {ehSoldado}, Heli? {ehHelicoptero}, Navio? {ehNavio}");

        // 2. Verificar se a FÁBRICA existe
        // --- VERIFICAÇÃO DE FÁBRICA DESABILITADA (Spawn de Fallback será usado) ---
        /*
        if (ehSoldado && spawnSoldado == null)
        {
            Debug.LogWarning("⚠️ Você precisa construir uma TENDA antes de treinar soldados!");
            return; // Cancela compra
        }
        if (!ehSoldado && spawnInterno == null)
        {
            Debug.LogWarning("⚠️ Você precisa construir um HANGAR antes de fabricar tanques!");
            return; // Cancela compra
        }
        */

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
        Transform spawnAtual = null;
        Transform destinoAtual = null;

        if (pedido.ehHelicoptero)
        {
            // Lógica Exclusiva para HELICÓPTEROS
            Heliporto heliportoDestino = ObterProximoHeliporto();
            if (heliportoDestino != null)
            {
                // Cria um objeto temporário para representar o ponto de spawn exato
                // O heliporto calcula seu ponto mundial
                Vector3 pontoPouso = heliportoDestino.ObterPontoDePousoMundial();
                
                // Hack: Cria Transforms temporários apenas para passar para a lógica abaixo
                // O ideal seria refatorar para usar Vector3 direto, mas vamos manter a estrutura
                GameObject tempSpawn = new GameObject("TempSpawn_Heli");
                tempSpawn.transform.position = pontoPouso;
                tempSpawn.transform.rotation = heliportoDestino.transform.rotation;
                
                spawnAtual = tempSpawn.transform;
                destinoAtual = tempSpawn.transform; // Destino é o próprio ponto de pouso (hover)

                // Destruir depois de usar (será usado no Instantiate logo abaixo)
                Destroy(tempSpawn, 0.1f); 
            }
            else
            {
                Debug.LogWarning("⚠️ Nenhum HELIPORTO encontrado! Helicóptero nascerá no Hangar de Veículos com fallback.");
                 // Tenta Logística Normal se falhar
                PontoLogistico logistica = ObterProximoSpawn(false); // False = Hangar
                if (logistica != null && logistica.spawn != null)
                {
                    spawnAtual = logistica.spawn;
                    destinoAtual = logistica.saida;
                }
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
        else
        {
            posNascimento += Vector3.up * 0.5f;
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

        // DEBUG DE DESTINO
        Debug.Log($"DESTINO CALCULADO: {posDestino} (Alvo: {(destinoAtual != null ? destinoAtual.name : "Fallback")})");
        Debug.DrawLine(posNascimento, posDestino, Color.yellow, 10.0f); // Desenha linha amarela na Scene por 10s

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
                // Tenta encontrar o ponto válido mais próximo no NavMesh (Raio de 10m agora)
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(posNascimento, out hit, 10.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    Debug.Log($"NavMesh: Unidade posicionada no NavMesh em {hit.position}");
                }
                else
                {
                    Debug.LogWarning($"ALERTA: Não foi possível encontrar NavMesh próximo a {posNascimento}. Unidade pode ficar presa ou cair. Verifique se o mapa tem NavMesh baked.");
                    // Se não tiver NavMesh, desabilita o Agent senão ele trava a unidade no infinito
                    agent.enabled = false; 
                    novaUnidade.transform.position = posNascimento;
                }
            }
            else
            {
                // Sem agente, move transform direto
                novaUnidade.transform.position = posNascimento;
            }
            
            // Tenta mover. Se colidir, o NavMeshAgent lida com isso.
            // MODIFICAÇÃO: Randomizar levemente o destino para evitar fila indiana perfeita e colisão
            Vector3 destinoFinal = posDestino;
            
            // Adiciona variação aleatória de 3m ao redor do ponto de saída
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * 3.0f; 
            destinoFinal += new Vector3(randomCircle.x, 0, randomCircle.y);

            controle.MoverParaPonto(destinoFinal);
        }

        Debug.Log($"SUCESSO: Saiu da fábrica: {pedido.nomeUnidade} em {novaUnidade.transform.position}");
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
        listaHeliportos.RemoveAll(h => h == null);
        if (listaHeliportos.Count == 0) return null;

        indexHeliporto = (indexHeliporto + 1) % listaHeliportos.Count;
        return listaHeliportos[indexHeliporto];
    }
}
