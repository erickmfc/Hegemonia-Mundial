using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hegemonia.AI.IA01;
using Hegemonia.AI.Shared;

// O PENSADOR AUTÔNOMO
// Agora com Plano de Dominação (Build Order) e Economia Própria
public class CerebroIA : MonoBehaviour
{
    [Header("Conexões")]
    public RecebedorIA recebedor;
    public Analista2 estrategista;

    [Header("Economia da IA")]
    public float recursosIA = 10000f; // Começa rica para testar montagem rápida
    public float rendaPassiva = 50f;

    [Header("Plano de Jogo")]
    public bool baseMontada = false;
    public bool exercitoPronto = false;
    
    // Lista do que já pedi para construir para não repetir infinitamente
    private List<string> historicoConstrucoes = new List<string>();

    // Adicionado para identificação de time
    private IdentidadeIA identidade;
    public int teamID_Inimigo = 2; // ID padrão para o time inimigo

    private float timerRenda;

    void Start()
    {
        // Garante componentes essenciais
        if (identidade == null) identidade = GetComponent<IdentidadeIA>();
        if (identidade == null) identidade = gameObject.AddComponent<IdentidadeIA>();
        
        // Define o ID do time (Inimigo = 2 ou 3)
        identidade.teamID = teamID_Inimigo;

        // Inicia a espera pelo Catálogo
        StartCoroutine(InicializarCerebro());
    }

    IEnumerator InicializarCerebro()
    {
        // Atribuições de componentes que antes estavam no Start
        recebedor = GetComponent<RecebedorIA>();
        estrategista = GetComponent<Analista2>();

        Debug.Log($"[{name}] CerebroIA: Aguardando catálogo de construção...");
        
        // Espera até 5 segundos pelo catálogo
        float timeout = Time.time + 5.0f;
        while ((MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0) && Time.time < timeout)
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (MenuConstrucao.catalogoGlobal == null || MenuConstrucao.catalogoGlobal.Count == 0)
        {
            Debug.LogError($"[{name}] CerebroIA: Catálogo VAZIO ou MenuConstrucao não encontrado! IA pode falhar ao construir.");
        }
        else
        {
            Debug.Log($"[{name}] CerebroIA: Catálogo carregado com {MenuConstrucao.catalogoGlobal.Count} itens. Iniciando dominação.");
        }

        // Agora sim, pode começar
        // MontarBaseInicial(); // Esta chamada será feita dentro do CicloDePensamento

        // Inicia o ciclo de pensamento (Loop infinito)
        StartCoroutine(CicloDePensamento());

        // Inicia o fluxo de Recursos (Renda passiva da IA)
        StartCoroutine(GerarRendaIA());
    }

    void Update()
    {
        // O Update agora está vazio, pois a lógica de tempo e pensamento foi movida para Coroutines.
        // Se houver outras lógicas que precisam de execução por frame, elas podem ser adicionadas aqui.
    }

    IEnumerator GerarRendaIA()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // Gera renda a cada 1 segundo
            recursosIA += rendaPassiva;
            // Atualiza Analista 1 com o dinheiro real (se ele checasse)
        }
    }

    IEnumerator CicloDePensamento()
    {
        while (true)
        {
            yield return new WaitForSeconds(4f); // Pensa a cada 4 segundos
            Pensar();
        }
    }

    void Pensar()
    {
        if (MenuConstrucao.catalogoGlobal == null) return;

        // FASE 1: MONTAR BASE (Se ainda não montou)
        if (!baseMontada)
        {
            MontarBaseInicial();
        }
        // FASE 2: MONTAR EXÉRCITO (Se base ok, mas exército fraco)
        else if (!exercitoPronto)
        {
            RecrutarExercitoInvasor();
        }
        // FASE 3: ATAQUE TOTAL (Se tudo pronto)
        else
        {
            PlanejarAtaque();
        }
    }

    void MontarBaseInicial()
    {
        // Ordem de Construção: 1 Fábrica, 1 Quartel, 3 Tendas, 1 HangarVeiculo
        // Verifica se já construiu cada um pelo histórico ou checando a cena (melhor histórico + cena)
        
        // 1. Tendas (População) - Precisa de 3
        int qtdTendas = ContarMeusPredios("Tenda");
        if (qtdTendas < 3)
        {
            ComprarEstrutura("Tenda", PriorityLevel.High);
            return;
        }

        // 2. Quartel (Infantaria)
        if (ContarMeusPredios("Quartel") < 1 && ContarMeusPredios("Barraca") < 1)
        {
            ComprarEstrutura("Quartel", PriorityLevel.Critical); // Ou Barraca se for o nome no menu
            return;
        }

        // 3. Fábrica / Hangar (Veículos)
        if (ContarMeusPredios("Hangar") < 1 && ContarMeusPredios("Fabrica") < 1)
        {
            ComprarEstrutura("Hangar", PriorityLevel.Critical);
            return;
        }
        
        // 4. Defesa (Torres)
        if (ContarMeusPredios("Torre") < 2)
        {
            ComprarEstrutura("Torre", PriorityLevel.Medium);
            return;
        }

        // Se chegou aqui, temos o básico!
        baseMontada = true;
        Debug.Log("[CerebroIA] BASE INICIAL CONCLUÍDA! Iniciando Fase Militar.");
    }

    void RecrutarExercitoInvasor()
    {
        // Meta: 5 Soldados, 3 Tanques
        // Aqui precisaríamos saber s e as unidades morreram. Por simplificação, vamos contar pedidos.
        
        // Soldados (Barato e rápido)
        for(int i=0; i<2; i++) // Tenta comprar 2 por ciclo
        {
            if (recursosIA > 100)
            {
                ComprarUnidade("Soldado", PriorityLevel.High);
                ComprarUnidade("Fuzileiro", PriorityLevel.High);
            }
        }

        // Tanques (Caro)
        if (recursosIA > 800)
        {
            ComprarUnidade("Tank", PriorityLevel.High);
        }

        // Condição de saída simplificada: Se gastou muito dinheiro, assume que tem exército.
        // O ideal seria contar unidades vivas com tag 'Inimigo'.
        if (historicoConstrucoes.Count(x => x.Contains("Soldado") || x.Contains("Tank")) > 10)
        {
            exercitoPronto = true;
            Debug.Log("[CerebroIA] EXÉRCITO PRONTO! PREPARAR PARA ATAQUE.");
            
            // Avisa o Analista 2 que estamos em Guerra Ofensiva
            if (estrategista) estrategista.estadoPercebido = FactionState.War;
        }
    }

    void PlanejarAtaque()
    {
        // Aqui mandaria ordens de "MoveSquad" para a base do player.
        // Como ainda não temos o SquadSystem implementado na fase 4, vamos só manter o recrutamento de reforços.
        if (recursosIA > 500)
        {
            ComprarUnidade("Tank", PriorityLevel.Medium); // Reforços
        }
    }

    // --- HELPERS ---

    // --- HELPERS ---

    public float nivelDoMar = 0f; // Ajustar conforme o mapa (OceanAdvanced)

    void ComprarEstrutura(string nomeParcial, PriorityLevel prioridade)
    {
        var item = EncontrarNoMenu(nomeParcial);
        if (item != null && recursosIA >= item.preco && recebedor != null)
        {
            // Lógica Especial para NAVAL
            Vector3 alvo = Vector3.zero;
            Quaternion rotacao = Quaternion.identity;
            
        bool ehNaval = nomeParcial.ToLower().Contains("estaleiro") || nomeParcial.ToLower().Contains("naval") || item.NomeItem.ToLower().Contains("pier");
        bool ehAeroporto = nomeParcial.ToLower().Contains("aeroporto") || item.NomeItem.ToLower().Contains("aeroporto") || item.NomeItem.ToLower().Contains("hangar");

            if (ehNaval)
            {
            Debug.Log($"[CerebroIA] Planejando construção NAVAL: {item.NomeItem}");
                Vector3 centro = (baseMontada && ContarMeusPredios("Base") > 0) ? transform.position : transform.position; // Melhorar depois
                
                // Busca costa num raio grande (150m)
                Vector3 pontoCosta = EncontrarAgua(centro, 40f, 250f);
                
                if (pontoCosta != Vector3.zero)
                {
                    // Ponto costa é onde a terra encontra a água.
                    // O Estaleiro deve ficar "um pedaço na água".
                    // Vamos empurrar ele um pouco para dentro da água.
                    
                    // Direção Terra -> Mar?
                    // EncontrarAgua retorna o ponto exato da costa.
                    // Precisamos saber para onde é o mar.
                    // Hack: O mar está na direção de (pontoCosta - centro)? AS vezes sim.
                    
                    Vector3 direcaoMar = (pontoCosta - centro).normalized; // Assumindo que o centro da base é terra.
                    
                    alvo = pontoCosta + (direcaoMar * 20f); // 20m mar adentro
                    alvo.y = nivelDoMar; 
                    
                    // Rotação: "Virado ao contrário". 
                    // Geralmente Z-forward aponta para a água.
                    // Se o usuário disse "ao contrário", talvez o Z-forward deva apontar para a TERRA.
                    // Vamos tentar apontar para a terra.
                    rotacao = Quaternion.LookRotation(-direcaoMar); 
                    
                    Debug.Log($"[CerebroIA] Local Naval encontrado: {alvo}");
                }
                else
                {
                     Debug.LogWarning("[CerebroIA] Não encontrei água para o Estaleiro! Abortando.");
                     return;
                }
            }
            else if (ehAeroporto)
            {
            Debug.Log($"[CerebroIA] Planejando construção de AEROPORTO afastado: {item.NomeItem}");
                float angulo = Random.Range(0, 360) * Mathf.Deg2Rad;
                float dist = 650f; // Distância fixa para aeroportos conforme solicitado
                Vector3 offset = new Vector3(Mathf.Cos(angulo) * dist, 0, Mathf.Sin(angulo) * dist);
                alvo = transform.position + offset;
                
                if (Terrain.activeTerrain != null)
                {
                    alvo.y = Terrain.activeTerrain.SampleHeight(alvo);
                }
                else if (Physics.Raycast(alvo + Vector3.up * 100, Vector3.down, out RaycastHit hit, 200))
                {
                    alvo.y = hit.point.y;
                }
                
                rotacao = Quaternion.Euler(0, Random.Range(0, 360), 0);
            }
            else
            {
                alvo = EncontrarLugarParaConstruir(item);
                // Terrestres usam rotação padrão (identity ou aleatória?)
                // Vamos dar uma rotação aleatória leve para não ficar tudo quadrado
                rotacao = Quaternion.Euler(0, Random.Range(0, 360), 0);
            }

            recursosIA -= item.preco;
            historicoConstrucoes.Add(item.NomeItem);
            
            recebedor.ReceberPedido(
                "Cerebro Estrategista",
                ActionType.ConstructBuilding,
                alvo,
                item,
                prioridade,
                rotacao // Nova Assinatura
            );
        }
    }

    void ComprarUnidade(string nomeParcial, PriorityLevel prioridade)
    {
        var item = EncontrarNoMenu(nomeParcial);
        if (item != null && recursosIA >= item.preco)
        {
            IdentidadeIA identity = identidade != null ? identidade : GetComponent<IdentidadeIA>();
            int teamId = identity != null ? identity.teamID : 0;
            IA01MilitaryAssetKind kind = IA01MilitaryProductionGuard.Classify(item);
            string unitType = kind == IA01MilitaryAssetKind.Other ? item.GetStableId() : kind.ToString();
            int alive = CountOwnedForProduction(teamId, kind);
            if (!IAAutoProductionRegistry.TryReserveProduction(teamId, unitType, "ia3", alive + 1, alive, out string orderId, Time.time, 180f)) return;

            recursosIA -= item.preco;
            historicoConstrucoes.Add(item.NomeItem); // Bug fix: era o nome amigavel

            recebedor.ReceberPedido(
                "General IA",
                ActionType.RecruitUnit,
                Vector3.zero,
                item,
                prioridade,
                default,
                orderId
            );
        }
    }

    int CountOwnedForProduction(int teamId, IA01MilitaryAssetKind kind)
    {
        switch (kind)
        {
            case IA01MilitaryAssetKind.Infantry:
                return IA01MilitaryProductionGuard.CountOwnedUnique(teamId, TipoUnidade.Infantaria);
            case IA01MilitaryAssetKind.Tank:
            case IA01MilitaryAssetKind.AntiAir:
                return IA01MilitaryProductionGuard.CountOwnedUnique(teamId, TipoUnidade.Veiculo);
            case IA01MilitaryAssetKind.Fighter:
                return IA01MilitaryProductionGuard.CountOwnedUnique(teamId, TipoUnidade.Aereo);
            case IA01MilitaryAssetKind.Naval:
            case IA01MilitaryAssetKind.OilTanker:
                return IA01MilitaryProductionGuard.CountOwnedUnique(teamId, TipoUnidade.Naval);
            default:
                return 0;
        }
    }

    DadosConstrucao EncontrarNoMenu(string parteDoNome)
    {
        return MenuConstrucao.catalogoGlobal.FirstOrDefault(x => x.NomeItem.IndexOf(parteDoNome, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    int ContarMeusPredios(string nomeParcial)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 500f);
        HashSet<GameObject> unicos = new HashSet<GameObject>();
        int count = 0;
        
        foreach(var h in hits)
        {
            Transform raiz = h.transform.root;
            if (unicos.Contains(raiz.gameObject)) continue;
            unicos.Add(raiz.gameObject);

            var id = raiz.GetComponentInChildren<IdentidadeUnidade>();
            
            if (raiz.name.IndexOf(nomeParcial, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (id != null && id.teamID == GetComponent<IdentidadeIA>().teamID)
                {
                    count++;
                }
                else if (id == null)
                {
                   count++;
                }
            }
        }
        return count;
    }

    Vector3 EncontrarLugarParaConstruir(DadosConstrucao item)
    {
        for(int i=0; i<10; i++)
        {
            float angulo = Random.Range(0, 360);
            float dist = Random.Range(20, 80); 
            Vector3 offset = new Vector3(Mathf.Cos(angulo)*dist, 0, Mathf.Sin(angulo)*dist);
            Vector3 tentativa = transform.position + offset;
            
            if (Physics.Raycast(tentativa + Vector3.up * 50, Vector3.down, out RaycastHit hit, 100))
            {
                // Evitar construir na água se for terrestre
                if (hit.point.y > 0.5f) // Assumindo nível do mar 0
                {
                    return hit.point;
                }
            }
        }
        return transform.position + Vector3.right * 30; // Fallback
    }

    Vector3 EncontrarAgua(Vector3 centro, float raioMin, float raioMax)
    {
        int tentativas = 36; 
        float raioLimite = Mathf.Max(raioMax, 400f); 

        for (int i = 0; i < tentativas; i++)
        {
            float angulo = (360f / tentativas) * i * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angulo), 0, Mathf.Sin(angulo));
            bool estavaNaTerra = true;

            for (float dist = raioMin; dist < raioLimite; dist += 10f) 
            {
                Vector3 pontoTeste = centro + dir * dist;
                float altura = 0f;
                if (Terrain.activeTerrain != null) altura = Terrain.activeTerrain.SampleHeight(pontoTeste);
                else 
                {
                    // Sem terreno? Raycast.
                     if (Physics.Raycast(pontoTeste + Vector3.up * 100, Vector3.down, out RaycastHit hit, 200)) altura = hit.point.y;
                }

                bool estaNaAgua = (altura <= nivelDoMar - 0.5f); 

                if (estavaNaTerra && estaNaAgua)
                {
                    // Encontramos a borda!
                    return pontoTeste - (dir * 5f); 
                }
                estavaNaTerra = !estaNaAgua;
            }
        }
        return Vector3.zero;
    }
}
