using UnityEngine;

// 4. O EXECUTOR: "A MÃO DA IA"
// Pega o pedido APROVADO pelos dois analistas e chama o comando real.
public class AnalistaExecutivo : MonoBehaviour
{
    // Não depende mais de FactionOverlord nem SquadSystem
    
    public void ExecutarFinal(TaskRequest pedido)
    {
        Debug.Log($"[Executor] AGORA VAI! Executando {pedido.type} no alvo {pedido.targetPosition}.");

        // DISPATCHER DE COMANDOS (CENTRAL DE ORDENS)
        switch (pedido.type)
        {
            case ActionType.ConstructBuilding:
                ExecutarConstrucao(pedido);
                break;
            case ActionType.RecruitUnit:
                ExecutarRecrutamento(pedido);
                break;
            case ActionType.AttackTarget:
                ExecutarAtaque(pedido);
                break;
            case ActionType.MoveSquad:
                ExecutarMovimento(pedido);
                break;
        }
    }

    // --- SUB-COMANDOS REAIS (MODO IA - SEM GASTAR DO PLAYER) ---

    // Cache do ID da IA
    private int teamID = -1;

    void Start()
    {
        // Tenta pegar o ID do script IdentidadeIA no mesmo objeto
        var idScript = GetComponent<IdentidadeIA>();
        if (idScript != null)
        {
            teamID = idScript.teamID; // Supondo que a variavel se chama 'teamID'
            Debug.Log($"[Executor] Team ID definido como: {teamID}");
        }
        else
        {
            Debug.LogWarning("[Executor] Não encontrei IdentidadeIA! Usando Team ID padrão (-1).");
        }
    }

    void ExecutarConstrucao(TaskRequest r)
    {
        // O dinheiro JA FOI GASTO no CerebroIA (Economia interna).
        // Aqui apenas materializamos o prédio.

        Construtor construtor = Object.FindFirstObjectByType<Construtor>();
        if (construtor != null)
        {
            GameObject predio = construtor.ConstruirEstruturaIA(r.targetObject, r.targetPosition, r.targetRotation);
            if (predio != null)
            {
                ConfigurarTime(predio);
            }
            Debug.Log($"[Executor] CONSTRUÇÃO IA: {r.targetObject.name}");
        }
    }

    void ExecutarRecrutamento(TaskRequest r)
    {
        // Precisamos achar um local de spawn (Quartel/Fábrica)
        // Como o item tem "Categoria", podemos tentar achar o prédio certo.
        
        Vector3 spawnPoint = transform.position; // Fallback: Spawna no Overlord
        
        // Tenta achar prédio compatível
        if (r.menuItem != null)
        {
            if (r.menuItem.categoria == DadosConstrucao.CategoriaItem.Exercito)
            {
                 var quartel = EncontrarPredioDeSpawn("Quartel"); 
                 if (quartel) spawnPoint = quartel.transform.position + Vector3.forward * 5;
            }
            else if (r.menuItem.categoria == DadosConstrucao.CategoriaItem.Aeronautica)
            {
                 var heliporto = EncontrarPredioDeSpawn("Heliporto"); 
                 if (heliporto) spawnPoint = heliporto.transform.position + Vector3.up * 2;
            }
        }

        // Spawn FREE (Pois a IA já pagou internamente)
        GameObject novaUnidade = Instantiate(r.targetObject, spawnPoint, Quaternion.identity);
        
        // Garante que é INIMIGO (Team ID diferente)
        ConfigurarTime(novaUnidade);
        
        Debug.Log($"[Executor] RECRUTAMENTO IA: {r.targetObject.name} em {spawnPoint} (Time: {teamID})");
    }

    void ConfigurarTime(GameObject obj)
    {
        if (teamID == -1) return;

        // Tenta achar IdentidadeUnidade (Inclusive nos filhos)
        var idUnits = obj.GetComponentsInChildren<IdentidadeUnidade>(true);
        
        if (idUnits.Length > 0)
        {
            // Já tem identidade, só atualiza
            foreach(var idScript in idUnits)
            {
                idScript.teamID = teamID;
            }
        }
        else
        {
            // --- CORREÇÃO CRÍTICA ---
            // Se não tem identidade (ex: Tenda simples), ADICIONA o componente!
            // Senão o Player ignora e não ataca.
            
            Debug.Log($"[Executor] Objeto {obj.name} sem IdentidadeUnidade. Adicionando automaticamente...");
            
            // Adiciona no root
            IdentidadeUnidade novaId = obj.AddComponent<IdentidadeUnidade>();
            novaId.teamID = teamID;
            novaId.nomeDoPais = "Inimigo IA";
            novaId.tipoUnidade = TipoUnidade.Estrutura; // Assume estrutura se não tinha script
        }
        
        // Remove NavMeshAgent se for prédio estático para não pesar? 
        // Não, deixa quieto. O importante é a identidade.
    }

    GameObject EncontrarPredioDeSpawn(string nomeParcial)
    {
        // Procura predios próximos ao Overlord (usando colisão ou tag seria melhor, mas vamos por nome na cena)
        // Isso é perigoso se pegar prédio do player.
        // O ideal é a IA ter uma lista de "Meus Prédios".
        
        // Solução Provisória: OverlapSphere em volta do Overlord
        Collider[] hits = Physics.OverlapSphere(transform.position, 100f);
        foreach(var h in hits)
        {
            // Verifica se o prédio é MEU (mesmo time)
            var id = h.GetComponent<IdentidadeUnidade>(); // Prédios as vezes usam IdentidadeUnidade também?
            // Se tiver tag, checar tag.
            
            if (h.name.Contains(nomeParcial)) return h.gameObject;
        }
        return null;
    }


    void ExecutarAtaque(TaskRequest r)
    {
        // Placeholder até termos o sistema de Squads real
        Debug.Log($"[Executor] (Simulação) ATAQUE EM MASSA contra {r.targetPosition}!");
    }

    void ExecutarMovimento(TaskRequest r)
    {
        Debug.Log($"[Executor] (Simulação) MOVIMENTO para {r.targetPosition}.");
    }
}
