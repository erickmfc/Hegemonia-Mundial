using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class TransporteTerrestre : MonoBehaviour
{
    [Header("Configurações de Transporte")]
    public int capacidadeMaxima = 4;
    public float distanciaParaEmbarque = 10f; // Raio para puxar soldados
    public float distanciaDescarga = 3.0f;    // Distância exata pedida (3 metros)

    [Header("Assentos Visíveis (Atiradores)")]
    [Tooltip("Arraste os Transforms 'Lugar' aqui. Soldados nestes pontos continuarão ativos e visíveis.")]
    public Transform[] assentosVisiveis;

    // Estado Interno
    private List<GameObject> soldadosInternos = new List<GameObject>(); // Os que ficam escondidos/desativados
    private GameObject[] soldadosNosAssentos; // Os que ficam visíveis

    private bool selecionado = false;

    void Start()
    {
        // Inicializa array de assentos baseados nos slots disponíveis
        if (assentosVisiveis != null)
        {
            soldadosNosAssentos = new GameObject[assentosVisiveis.Length];
        }
        else
        {
            soldadosNosAssentos = new GameObject[0];
        }

        // Tenta auto-configurar se esqueceu de arrastar
        if (assentosVisiveis == null || assentosVisiveis.Length == 0)
        {
            var lugares = new List<Transform>();
            foreach(Transform child in transform)
            {
                if(child.name.Contains("Lugar")) lugares.Add(child);
            }
            if (lugares.Count > 0)
            {
                assentosVisiveis = lugares.ToArray();
                soldadosNosAssentos = new GameObject[assentosVisiveis.Length];
            }
        }
    }

    void Update()
    {
        VerificarSelecao();

        if (selecionado)
        {
            // O -> OPEN / Entrar
            if (Input.GetKeyDown(KeyCode.O)) 
            {
                TentarEmbarcar();
            }
            // U -> UNLOAD / Sair
            if (Input.GetKeyDown(KeyCode.U))
            {
                DesembarcarTudo();
            }
        }
    }

    void VerificarSelecao()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                // Se clicou em mim ou num filho meu
                if (hit.collider.transform.root == transform.root)
                {
                    selecionado = true;
                    // Debug.Log("Transporte Selecionado");
                }
                else
                {
                    selecionado = false;
                }
            }
        }
    }

    public void TentarEmbarcar()
    {
        // Conta total atual
        int totalEmbarcados = soldadosInternos.Count + ContarVisiveis();
        if (totalEmbarcados >= capacidadeMaxima)
        {
            Debug.Log("Transporte Cheio!");
            return;
        }

        // Busca soldados próximos (Assumindo tag "Player" ou similar, ajustável conforme seu jogo)
        // Se usar IdentidadeUnidade, seria melhor filtrar pelo time ID, mas vamos simplificar como no Heli
        GameObject[] possiveisSoldados = GameObject.FindGameObjectsWithTag("Player"); 
        
        foreach (var soldado in possiveisSoldados)
        {
            if (totalEmbarcados >= capacidadeMaxima) break;

            // Não embarca a si mesmo se por acaso tiver a tag
            if (soldado == gameObject) continue;
            if (!soldado.activeInHierarchy) continue; // Ignora desativados

            float dist = Vector3.Distance(transform.position, soldado.transform.position);
            if (dist <= distanciaParaEmbarque)
            {
                // Verifica se é soldado (tem NavMeshAgent ou script de controle geralmente)
                if (soldado.GetComponent<NavMeshAgent>() != null || soldado.name.ToLower().Contains("soldado"))
                {
                    EmbarcarUnidade(soldado);
                    totalEmbarcados++;
                }
            }
        }
    }

    void EmbarcarUnidade(GameObject soldado)
    {
        // 1. Tenta colocar num assento visível primeiro
        for (int i = 0; i < assentosVisiveis.Length; i++)
        {
            if (soldadosNosAssentos[i] == null)
            {
                // Coloca no assento
                soldadosNosAssentos[i] = soldado;
                
                // Lógica de fixação
                DesabilitarMovimento(soldado);
                
                soldado.transform.SetParent(assentosVisiveis[i]);
                soldado.transform.localPosition = Vector3.zero;
                soldado.transform.localRotation = Quaternion.identity;
                
                Debug.Log($"Soldado {soldado.name} ocupou o assento visível {i}.");
                return;
            }
        }

        // 2. Se não tem assento, vai para o compartimento interno (escondido)
        soldadosInternos.Add(soldado);
        soldado.SetActive(false); // Some com ele
        soldado.transform.SetParent(transform); // Leva junto como filho para não perder
        Debug.Log($"Soldado {soldado.name} embarcou no interior.");
    }

    void DesembarcarTudo()
    {
        // 1. Desembarca os Visíveis
        for (int i = 0; i < soldadosNosAssentos.Length; i++)
        {
            if (soldadosNosAssentos[i] != null)
            {
                EjetarUnidade(soldadosNosAssentos[i]);
                soldadosNosAssentos[i] = null;
            }
        }

        // 2. Desembarca os Internos
        // Nota: Iteramos de trás para frente para poder remover da lista
        for (int i = soldadosInternos.Count - 1; i >= 0; i--)
        {
            GameObject s = soldadosInternos[i];
            if (s != null)
            {
                s.SetActive(true); // Reaparece
                EjetarUnidade(s);
            }
            soldadosInternos.RemoveAt(i);
        }
    }

    void EjetarUnidade(GameObject unidade)
    {
        unidade.transform.SetParent(null); // Solta do carro
        
        // --- CÁLCULO DA POSIÇÃO DE SAÍDA ---
        // Pega uma posição aleatória num círculo de raio "distanciaDescarga"
        // Ou fixo nas laterais? O user pediu "saiam 3 metros do carro".
        
        Vector3 direcaoAleatoria = Random.onUnitSphere;
        direcaoAleatoria.y = 0; // Mantém no plano
        direcaoAleatoria.Normalize();

        Vector3 pontoAlvo = transform.position + (direcaoAleatoria * distanciaDescarga);

        // Garante que seja no NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pontoAlvo, out hit, 2.0f, NavMesh.AllAreas))
        {
            unidade.transform.position = hit.position;
            HabilitarMovimento(unidade, hit.position);
        }
        else
        {
            // Se falhar o NavMesh, bota na posição bruta mesmo definindo Y do chão
            pontoAlvo.y = transform.position.y; 
            unidade.transform.position = pontoAlvo;
            HabilitarMovimento(unidade, pontoAlvo);
        }
    }

    // --- Utilitários de Controle de Agente ---

    void DesabilitarMovimento(GameObject unidade)
    {
        NavMeshAgent agent = unidade.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false; // Desliga o NavMeshAgent
        }
        
        Rigidbody rb = unidade.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Trava física
        }

        // DESLIGA O CONTROLE PARA NÃO SER SELECIONADO
        ControleUnidade ctrl = unidade.GetComponent<ControleUnidade>();
        if (ctrl != null)
        {
             ctrl.DefinirSelecao(false); // Garante que deseleciona visualmente antes
             ctrl.enabled = false; 
        }

        // DESLIGA O COLLIDER PARA CLIQUE PASSAR DIRETO PRO CARRO
        // (Nota: Isso impede que levem tiros diretos. Se quiser que levem tiro, teria que tratar no GerenteSelecao)
        Collider[] cols = unidade.GetComponentsInChildren<Collider>();
        foreach(var c in cols) c.enabled = false;
    }

    void HabilitarMovimento(GameObject unidade, Vector3 posicaoInicial)
    {
        // REATIVA COLLIDERS
        Collider[] cols = unidade.GetComponentsInChildren<Collider>();
        foreach(var c in cols) c.enabled = true;

        // REATIVA CONTROLE
        ControleUnidade ctrl = unidade.GetComponent<ControleUnidade>();
        if (ctrl != null) ctrl.enabled = true;

        NavMeshAgent agent = unidade.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(posicaoInicial); // Reseta o agente na nova posição
            agent.enabled = true;
            agent.SetDestination(posicaoInicial); // Manda ficar parado onde caiu
        }
         
        Rigidbody rb = unidade.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // (Ou true se usar NavMesh sempre)
        }
    }
    
    int ContarVisiveis()
    {
        int c = 0;
        foreach(var s in soldadosNosAssentos) if(s != null) c++;
        return c;
    }
}
