using UnityEngine;
using UnityEngine.AI;

public class IdentidadeNaval : MonoBehaviour
{
    public enum CategoriaNavio
    {
        TransporteGrande, // Porta-aviões, Liberty, Navios Anfíbios
        Medio,            // Outros navios de combate
        Submarino         // Poseidon e outros submarinos
    }

    [Header("Identificação")]
    public string nomeDoNavio;
    public CategoriaNavio categoriaNavio;

    [Header("Estado")]
    [SerializeField] private bool estaAtracado = false;
    
    private NavMeshAgent agente;

    // Propriedade pública para leitura segura
    public bool EstaAtracado => estaAtracado;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        if (agente == null)
        {
            // Tenta buscar no pai ou filhos se não estiver no mesmo objeto, mas ideal é estar no root
            agente = GetComponentInChildren<NavMeshAgent>();
        }
    }

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    void OnDestroy()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    /// <summary>
    /// Chamado pelo Pier para ordenar que este navio atraque
    /// </summary>
    public void ReceberOrdemDeAtracagem(Transform destino)
    {
        if (destino == null)
        {
            return;
        }

        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle != null)
        {
            estaAtracado = true;
            manobrandoDeRe = false;
            controle.EmitirOrdemMover(destino.position);
            Debug.Log($"{nomeDoNavio} ({categoriaNavio}) indo atracar em {destino.name}");
            return;
        }

        if (agente != null)
        {
            estaAtracado = true;
            manobrandoDeRe = false; // Garante que não está fazendo ré
            agente.enabled = true;  // Reativa o navmesh se estiver desligado
            MovimentoFallbackTransicional.TrySetNavDestination(gameObject, destino.position);
            Debug.Log($"{nomeDoNavio} ({categoriaNavio}) indo atracar em {destino.name}");
        }
    }

    /// <summary>
    /// Ordena que o navio saia da doca em marcha ré até o ponto de saída especificado.
    /// </summary>
    public void SairDaDoca(Vector3 pontoDeSaida)
    {
        estaAtracado = false;
        if (agente != null)
        {
            // Desativa temporariamente o NavMeshAgent para movermos manualmente transform
            agente.enabled = false; 
        }
        
        targetSaida = pontoDeSaida;
        manobrandoDeRe = true;
        rotacaoReInicial = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Debug.Log($"{nomeDoNavio} saindo da doca em marcha ré.");
    }

    [Header("Configuração de Manobra")]
    public float velocidadeDeRe = 5.0f;
    public float velocidadeRotacaoRe = 2.0f;
    
    private bool manobrandoDeRe = false;
    private Vector3 targetSaida;
    private Quaternion rotacaoReInicial;

    void Update()
    {
        if (manobrandoDeRe)
        {
            ExecutarManobraDeRe();
            return;
        }

        // Opcional: Se o jogador mover o navio manualmente (clicando no mapa), 
        // precisamos detectar que ele não está mais "querendo atracar" ou "atracado".
        if (estaAtracado && agente != null && agente.isActiveAndEnabled && agente.isOnNavMesh && !agente.pathPending)
        {
            // Se já chegou (ou está muito perto)
            if (agente.remainingDistance <= agente.stoppingDistance)
            {
                // Lógica de "Parado na doca" (Ex: iniciar reparos)
            }
        }
    }

    private void ExecutarManobraDeRe()
    {
        // Distância até o ponto de saída
        float distancia = Vector3.Distance(transform.position, targetSaida);
        
        if (distancia < 2.0f) // Chegou no ponto de saída
        {
            manobrandoDeRe = false;
            // Reativa o navmesh
            if (agente != null)
            {
                agente.enabled = true;
                // Opcional: agente.SetDestination(...) para um ponto seguro ou fica idle
            }
            Debug.Log($"{nomeDoNavio} terminou a manobra de ré e está livre.");
            return;
        }

        // Mantem o casco alinhado durante a re; o controle normal volta depois da vaga.
        transform.position = Vector3.MoveTowards(transform.position, targetSaida, velocidadeDeRe * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoReInicial, velocidadeRotacaoRe * 45f * Time.deltaTime);
    }

    /// <summary>
    /// Comanda o navio a ir para um ponto específico (Mar aberto, etc)
    /// </summary>
    public void MoverPara(Vector3 destino)
    {
        NotificarMovimento();

        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle != null)
        {
            controle.EmitirOrdemMover(destino);
            return;
        }
        
        if (agente == null) agente = GetComponent<NavMeshAgent>();
        
        if (agente != null)
        {
            // Se o agente estiver desativado ou em um navmesh link etc, resetamos
            if (!agente.isOnNavMesh && agente.enabled) 
            {
                 // Tenta corrigir posição
                 NavMeshHit hit;
                 if(NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
                 {
                     agente.Warp(hit.position);
                 }
            }

            agente.enabled = true;
            if (agente.isActiveAndEnabled && agente.isOnNavMesh) agente.isStopped = false;
            MovimentoFallbackTransicional.TrySetNavDestination(gameObject, destino);
        }
        else
        {
            // Fallback sem navmesh (move simples)
            Debug.LogWarning("Navio sem NavMeshAgent tentando mover. Usando script simples?");
        }
    }

    /// <summary>
    /// Método chamado por controladores externos (ControleUnidade) para avisar que o navio vai se mover
    /// e deve abortar atracagem ou sair da doca.
    /// </summary>
    public void NotificarMovimento()
    {
        estaAtracado = false;
        manobrandoDeRe = false;
        targetSaida = Vector3.zero;
        
        if (agente != null)
        {
            agente.enabled = true;
            if (agente.isActiveAndEnabled && agente.isOnNavMesh) agente.isStopped = false;
        }
    }
}
