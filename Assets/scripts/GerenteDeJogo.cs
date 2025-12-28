using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class GerenteDeJogo : MonoBehaviour
{
    [Header("Economia")]
    public int dinheiroAtual = 5000;
    public TextMeshProUGUI mostradorDinheiro; 

    [Header("Logística do Hangar (Tanques)")]
    public Transform spawnInterno; // Onde nasce (dentro do hangar)
    public Transform pontoSaida;   // Para onde vai (na rua)

    [Header("Logística da Tenda (Soldados)")]
    public Transform spawnSoldado; // Onde nasce (dentro da tenda)
    public Transform saidaSoldado; // Para onde vai (na rua/frente da tenda)

    void Awake()
    {
        // Tenta achar sozinho se esquecer de arrastar
        if (spawnSoldado == null) 
        {
            var obj = GameObject.Find("Spawn_Soldado");
            if(obj != null) spawnSoldado = obj.transform;
        }
        
        if (saidaSoldado == null) 
        {
            var obj = GameObject.Find("Saida_Soldado");
            if(obj != null) saidaSoldado = obj.transform;
        }
    }

    void Start()
    {
        AtualizarPainel();
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
        bool ehSoldado = (nome.Contains("soldado") || nome.Contains("soldier") || nome.Contains("person") || nome.Contains("infantry"));

        Debug.Log($"INFO COMPRA: Unidade '{nome}' identificada como Soldado? {ehSoldado}");

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

        if (dinheiroAtual >= custoTotal)
        {
            dinheiroAtual -= custoTotal;
            AtualizarPainel();

            // 4. Adiciona na Fila (Um pedido para cada unidade)
            for (int i = 0; i < quantidade; i++)
            {
                PedidoDeProducao novoPedido = new PedidoDeProducao();
                novoPedido.nomeUnidade = unidadeParaConstruir.name;
                novoPedido.prefab = unidadeParaConstruir;
                novoPedido.ehSoldado = ehSoldado;
                
                // Tempo de Produção: 0s para Soldado (Instantâneo), 2s para Tanque
                novoPedido.tempoTotal = ehSoldado ? 0f : 2.0f; 
                novoPedido.tempoRestante = novoPedido.tempoTotal;

                filaProducao.Add(novoPedido);
            }

            Debug.Log($"Adicionado à fila: {quantidade}x {unidadeParaConstruir.name}");
        }
        else
        {
            Debug.LogError("Dinheiro Insuficiente!");
        }
    }

    void FinalizarProducao(PedidoDeProducao pedido)
    {
        Transform spawnAtual = pedido.ehSoldado ? spawnSoldado : spawnInterno;
        Transform destinoAtual = pedido.ehSoldado ? saidaSoldado : pontoSaida;

        if(spawnAtual != null) Debug.Log($"SPAWNANDO EM: {spawnAtual.name} (Parente: {spawnAtual.parent.name})");
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


        // CORREÇÃO DE ALTURA: Adiciona 0.5m para não nascer no chão
        posNascimento += Vector3.up * 0.5f;

        // NASCER
        GameObject novaUnidade = Instantiate(pedido.prefab, posNascimento, rotNascimento);
        
        if (novaUnidade == null)
        {
            Debug.LogError("ERRO: Instantiate falhou! O objeto não foi criado.");
            return;
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
            
            controle.MoverParaPonto(posDestino);
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

    void AtualizarPainel()
    {
        if(mostradorDinheiro != null)
            mostradorDinheiro.text = "$ " + dinheiroAtual.ToString();
    }

    // Mantido para compatibilidade com o Construtor.cs
    public bool TentarGastarDinheiro(int custo)
    {
        if (dinheiroAtual >= custo)
        {
            dinheiroAtual -= custo;
            AtualizarPainel();
            return true;
        }
        return false;
    }

    // --- MÉTODOS DE REGISTRO (Chamados pelo script Fabrica.cs dos prédios) ---
    public void AtualizarPontoQuartel(Transform nascimento, Transform saida)
    {
        spawnSoldado = nascimento;
        saidaSoldado = saida;
        Debug.Log("Logística Atualizada: Nova Tenda registrada como ativa!");
    }

    public void AtualizarPontoHangar(Transform nascimento, Transform saida)
    {
        spawnInterno = nascimento;
        pontoSaida = saida; // Nota: A variável original chamava pontoSaida
        Debug.Log("Logística Atualizada: Novo Hangar registrado como ativo!");
    }
}
