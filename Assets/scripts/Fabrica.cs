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
        
        // --- AUTOCORREÇÃO DE CONFIGURAÇÃO ---
        string meuNome = gameObject.name.ToLower();
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

        // Instancia
        GameObject unidade = Instantiate(prefab, spawn.position, spawn.rotation);

        // Configura Identidade (Time da Fábrica = Time da Unidade)
        var idFabrica = GetComponentInParent<IdentidadeUnidade>();
        var idUnidade = unidade.GetComponent<IdentidadeUnidade>();
        
        if (idFabrica != null && idUnidade != null)
        {
            idUnidade.teamID = idFabrica.teamID;
            idUnidade.nomeDoPais = idFabrica.nomeDoPais;
        }

        // Tenta mover para a saída
        var controle = unidade.GetComponent<ControleUnidade>();
        if(controle != null) controle.MoverParaPonto(saida.position);
        else 
        {
            var nav = unidade.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if(nav != null && nav.isOnNavMesh) nav.SetDestination(saida.position);
        }

        return unidade;
    }
}
