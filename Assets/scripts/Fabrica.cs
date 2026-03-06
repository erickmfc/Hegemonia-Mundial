using UnityEngine;

public class Fabrica : MonoBehaviour
{
    [Header("Tipo de Fábrica")]
    public bool ehQuartel; // Marque TRUE se for Tenda/Soldado. Desmarque se for Hangar/Tanque.

    [Header("Pontos de Spawn (Arraste aqui os filhos)")]
    public Transform pontoNascimento;
    public Transform pontoSaida;

    void Start()
    {
        // Se eu tiver ID de IA, NÃO me registro no Gerente Global do Jogador
        var id = GetComponentInParent<IdentidadeUnidade>();
        if (id != null && id.teamID != 1) return; 

        // Lógica de Registro Global (Apenas para o Jogador Humano - Time 1)
        StartCoroutine(RegistrarNoGerente(0.1f));
    }

    System.Collections.IEnumerator RegistrarNoGerente(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        GerenteDeJogo gerente = FindFirstObjectByType<GerenteDeJogo>(); 
        
        string meuNome = gameObject.name.ToLower();

        // --- PROTEÇÃO CONTRA ESTALEIROS ---
        // Se for um prédio naval, NÃO se registra como fábrica terrestre!
        if (meuNome.Contains("naval") || meuNome.Contains("navio") || meuNome.Contains("estaleiro") || meuNome.Contains("pier"))
        {
            // Debug.Log($"[Fabrica] '{name}' ignorado pelo GerenteDeJogo pois parece ser Naval.");
            yield break;
        }

        // --- AUTOCORREÇÃO DE CONFIGURAÇÃO ---
        if(meuNome.Contains("hangar")) ehQuartel = false;
        if(meuNome.Contains("tenda") || meuNome.Contains("quartel")) ehQuartel = true;

        if (gerente != null)
        {
            if (ehQuartel) gerente.AtualizarPontoQuartel(pontoNascimento, pontoSaida);
            else gerente.AtualizarPontoHangar(pontoNascimento, pontoSaida);
        }
    }

    // --- NOVA FUNCIONALIDADE PARA IA ---
    public GameObject ProduzirUnidade(GameObject prefab)
    {
        if (prefab == null) return null;

        Transform spawn = (pontoNascimento != null) ? pontoNascimento : transform;
        Transform saida = (pontoSaida != null) ? pontoSaida : transform;

        Vector3 posFinal = spawn.position;
        Quaternion rotFinal = spawn.rotation;

        // --- CORREÇÃO DE NAVMESH ---
        // Antes de instanciar, verifica se o ponto está no NavMesh
        // Isso evita o erro "Failed to create agent because it is not close enough to the NavMesh"
        bool ehNaval = prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() == null;
        
        if (!ehNaval)
        {
            UnityEngine.AI.NavMeshHit hit;
            // Procura num raio de 5m
            if (UnityEngine.AI.NavMesh.SamplePosition(spawn.position, out hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                posFinal = hit.position;
            }
            else
            {
                Debug.LogWarning($"[Fabrica] '{name}' spawn Point fora do NavMesh! Tentando recuperar em raio maior (10m)...");
                if (UnityEngine.AI.NavMesh.SamplePosition(spawn.position, out hit, 10.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    posFinal = hit.position;
                }
            }
        }

        // Instancia na posição corrigida
        GameObject unidade = Instantiate(prefab, posFinal, rotFinal);

        // Configura Identidade (Time da Fábrica = Time da Unidade)
        var idFabrica = GetComponentInParent<IdentidadeUnidade>();
        var idUnidade = unidade.GetComponent<IdentidadeUnidade>();
        
        if (idFabrica != null && idUnidade != null)
        {
            idUnidade.teamID = idFabrica.teamID;
            idUnidade.nomeDoPais = idFabrica.nomeDoPais;
        }

        // --- CHECAGEM DE SOM (Silenciado) ---
        // if (unidade.GetComponent<SomUnidade>() == null)
        // {
        //     // Debug.LogWarning($"[Audio] Unidade '{unidade.name}' criada sem componente 'SomUnidade'! Adicione ao Prefab para ter áudio.");
        // }

        // Tenta mover para a saída
        var controle = unidade.GetComponent<ControleUnidade>();
        if(controle != null)
        {
             // Envia comando de movimento seguro
             controle.MoverParaPonto(saida.position);
        }
        else 
        {
            var nav = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if(nav != null && nav.isOnNavMesh) nav.SetDestination(saida.position);
        }

        return unidade;
    }
}
