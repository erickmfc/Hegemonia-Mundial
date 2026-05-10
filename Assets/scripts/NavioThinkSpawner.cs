using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Helper script to spawn NavioThink with all necessary components.
/// Attach this to an empty GameObject in your scene and call SpawnNavioThink() to create the ship.
/// </summary>
public class NavioThinkSpawner : MonoBehaviour
{
    [Header("Configurações do Spawn")]
    public Vector3 posicaoSpawn = new Vector3(0, 0, 0);
    public Quaternion rotacaoSpawn = Quaternion.identity;
    
    [Header("Modelo Visual (Opcional)")]
    public GameObject modeloVisual;
    
    [Header("Referência para o Navio Criado")]
    public GameObject navioThinkCriado;

    /// <summary>
    /// Spawns the NavioThink ship with all necessary components
    /// </summary>
    public GameObject SpawnNavioThink()
    {
        // Create the main ship GameObject
        GameObject navio = new GameObject("NavioThink");
        navio.transform.position = posicaoSpawn;
        navio.transform.rotation = rotacaoSpawn;

        // Add required components
        NavMeshAgent agent = navio.AddComponent<NavMeshAgent>();
        Rigidbody rb = navio.AddComponent<Rigidbody>();
        IdentidadeNaval identidade = navio.AddComponent<IdentidadeNaval>();
        ControleNavioRealista controleRealista = navio.AddComponent<ControleNavioRealista>();
        NavioThink navioThink = navio.AddComponent<NavioThink>();
        ControleUnidade controleUnidade = navio.AddComponent<ControleUnidade>();
        IdentidadeUnidade identidadeUnidade = navio.AddComponent<IdentidadeUnidade>();

        // Configure NavMeshAgent
        agent.radius = 2f;
        agent.speed = 10f;
        agent.acceleration = 9999f;
        agent.angularSpeed = 120f;
        agent.stoppingDistance = 5f;
        agent.autoBraking = true;
        agent.updatePosition = false; // ControleNavioRealista controls position
        agent.updateRotation = false; // ControleNavioRealista controls rotation

        // Configure Rigidbody
        rb.mass = 1000f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezePositionY 
                       | RigidbodyConstraints.FreezeRotationX 
                       | RigidbodyConstraints.FreezeRotationZ;

        // Configure IdentidadeNaval
        identidade.nomeDoNavio = "Navio Think";
        identidade.categoriaNavio = IdentidadeNaval.CategoriaNavio.Medio;

        // Configure IdentidadeUnidade
        identidadeUnidade.teamID = 1; // Player team
        identidadeUnidade.nomeDoPais = "Player";

        // Add visual model if provided
        if (modeloVisual != null)
        {
            GameObject visual = Instantiate(modeloVisual, navio.transform);
            visual.name = "ModeloVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            
            // Set the modelo3D reference in ControleNavioRealista
            controleRealista.modelo3D = visual.transform;
        }
        else
        {
            // Create a simple cube as placeholder visual
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "ModeloVisual";
            visual.transform.SetParent(navio.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(2f, 1f, 8f);
            
            // Remove collider from visual (we want it on the parent)
            Destroy(visual.GetComponent<Collider>());
            
            // Set the modelo3D reference in ControleNavioRealista
            controleRealista.modelo3D = visual.transform;
        }

        // Store reference
        navioThinkCriado = navio;

        Debug.Log("[NavioThinkSpawner] NavioThink criado com sucesso em " + posicaoSpawn);
        
        return navio;
    }

    /// <summary>
    /// Spawns the NavioThink ship at a specific position
    /// </summary>
    public GameObject SpawnNavioThink(Vector3 posicao, Quaternion rotacao)
    {
        posicaoSpawn = posicao;
        rotacaoSpawn = rotacao;
        return SpawnNavioThink();
    }

    void Start()
    {
        // Auto-spawn on start if needed
        // Uncomment the line below to auto-spawn when the scene loads
        // SpawnNavioThink();
    }
}
