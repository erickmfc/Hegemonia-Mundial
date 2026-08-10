using UnityEngine;
using UnityEngine.AI;

public class RuaConectora : MonoBehaviour
{
    [Header("🛣️ Configurações da Via")]
    public float largura = 6f;
    public float comprimento = 10f;
    
    [Header("🔗 Pontos de Conectores (Snaps)")]
    public Transform pontoInicio;
    public Transform pontoFim;
    public Transform pontoLadoEsquerdo;
    public Transform pontoLadoDireito;
    public float distanciaConexao = 3f;
    [Tooltip("Quando desativado, residencias nao podem usar este trecho como frente de rua.")]
    public bool permiteCasas = true;
    [Tooltip("Usa a orientacao dos transforms de conector para definir as tangentes de entrada/saida.")]
    public bool usarTangentesDosConectores = false;
    
    [Header("🎨 Pavement (Calçada/Concreto Opcional)")]
    public GameObject prefabPapeamentoConcreto;

    private static bool navMeshCustosConfigurados = false;

    private Transform EncontrarFilhoPeloNome(Transform raiz, string[] nomes)
    {
        Transform[] todosFilhos = raiz.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in todosFilhos)
        {
            if (t == raiz) continue; 
            foreach (string nome in nomes)
            {
                if (t.name.Equals(nome, System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
        }

        // Os prefabs antigos usam nomes compostos, como "lado esq" e
        // "connector frente". Aceita esses nomes sem exigir a renomeacao
        // manual de cada prefab.
        foreach (Transform t in todosFilhos)
        {
            if (t == raiz) continue;
            string nomeFilho = t.name.ToLowerInvariant();
            foreach (string nome in nomes)
            {
                string termo = nome.ToLowerInvariant();
                if (nomeFilho.Contains(termo)) return t;
            }
        }
        return null;
    }

    void Awake()
    {
        if (pontoInicio == null) pontoInicio = EncontrarFilhoPeloNome(transform, new string[] { "create", "connector frente", "conector frente", "inicio" });
        if (pontoFim == null) pontoFim = EncontrarFilhoPeloNome(transform, new string[] { "create (1)", "create2", "connector tras", "conector tras", "fim" });
        if (pontoLadoEsquerdo == null) pontoLadoEsquerdo = EncontrarFilhoPeloNome(transform, new string[] { "lado esq", "esquerdo", "esq", "create_esq" });
        if (pontoLadoDireito == null) pontoLadoDireito = EncontrarFilhoPeloNome(transform, new string[] { "lado dir", "direito", "dir", "create_dir" });
    }

    void Start()
    {
        GarantirConfiguracaoCustosNavMesh();
        ConfigureNavMeshObject();
    }

    private void GarantirConfiguracaoCustosNavMesh()
    {
        if (navMeshCustosConfigurados) return;
        int roadAreaIndex = NavMesh.GetAreaFromName("Road");
        if (roadAreaIndex != -1) NavMesh.SetAreaCost(roadAreaIndex, 1.0f); 

        int walkableAreaIndex = NavMesh.GetAreaFromName("Walkable");
        if (walkableAreaIndex != -1) NavMesh.SetAreaCost(walkableAreaIndex, 4.0f); 

        navMeshCustosConfigurados = true;
    }

    private void ConfigureNavMeshObject()
    {
        var modifier = GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = gameObject.AddComponent<NavMeshModifier>();

        int roadAreaIndex = NavMesh.GetAreaFromName("Road");
        if (roadAreaIndex != -1)
        {
            modifier.overrideArea = true;
            modifier.area = roadAreaIndex;
        }
    }

    public struct Conector
    {
        public Vector3 posicao;
        public Vector3 direcaoSaida;
    }

    private Vector3 CalcularDirecaoSaidaSegura(Transform conector, Vector3 fallbackDir, bool inverterTangente = false)
    {
        if (conector == null) return fallbackDir;
        if (usarTangentesDosConectores)
        {
            Vector3 tangente = conector.forward * (inverterTangente ? -1f : 1f);
            tangente.y = 0f;
            if (tangente.sqrMagnitude > 0.001f) return tangente.normalized;
        }
        Vector3 dir = conector.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f) return dir.normalized;
        return conector.forward;
    }

    public Conector ObterConectorEsquerdo()
    {
        if (pontoLadoEsquerdo != null) return new Conector { posicao = pontoLadoEsquerdo.position, direcaoSaida = CalcularDirecaoSaidaSegura(pontoLadoEsquerdo, -transform.right) };
        return new Conector { posicao = transform.position - transform.right * (largura / 2f), direcaoSaida = -transform.right };
    }
    
    public Conector ObterConectorDireito()
    {
        if (pontoLadoDireito != null) return new Conector { posicao = pontoLadoDireito.position, direcaoSaida = CalcularDirecaoSaidaSegura(pontoLadoDireito, transform.right) };
        return new Conector { posicao = transform.position + transform.right * (largura / 2f), direcaoSaida = transform.right };
    }

    public Conector ObterConectorInicio()
    {
        if (pontoInicio != null) return new Conector { posicao = pontoInicio.position, direcaoSaida = CalcularDirecaoSaidaSegura(pontoInicio, -transform.forward, true) };
        return new Conector { posicao = transform.position - transform.forward * (comprimento / 2f), direcaoSaida = -transform.forward };
    }

    public Conector ObterConectorFim()
    {
        if (pontoFim != null) return new Conector { posicao = pontoFim.position, direcaoSaida = CalcularDirecaoSaidaSegura(pontoFim, transform.forward) };
        return new Conector { posicao = transform.position + transform.forward * (comprimento / 2f), direcaoSaida = transform.forward };
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ObterConectorInicio().posicao, 0.8f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(ObterConectorFim().posicao, 0.8f);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ObterConectorEsquerdo().posicao, 0.6f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(ObterConectorDireito().posicao, 0.6f);

        Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(largura, 0.1f, comprimento));
        Gizmos.matrix = originalMatrix;
    }
}
