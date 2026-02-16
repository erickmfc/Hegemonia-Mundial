using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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

    private float timer;
    private float timerRenda;

    void Start()
    {
        recebedor = GetComponent<RecebedorIA>();
        estrategista = GetComponent<Analista2>();
    }

    void Update()
    {
        // Renda Passiva da IA (Cheat para ela não depender de minas no inicio)
        timerRenda += Time.deltaTime;
        if (timerRenda > 1f)
        {
            recursosIA += rendaPassiva;
            timerRenda = 0;
            // Atualiza Analista 1 com o dinheiro real (se ele checasse)
        }

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            Pensar();
            timer = 4f; // Pensa a cada 4 segundos
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

    void ComprarEstrutura(string nomeParcial, PriorityLevel prioridade)
    {
        var item = EncontrarNoMenu(nomeParcial);
        if (item != null && recursosIA >= item.preco)
        {
            recursosIA -= item.preco; // Desconta da carteira da IA
            historicoConstrucoes.Add(item.nomeItem);
            
            recebedor.ReceberPedido(
                "Cerebro Estrategista",
                ActionType.ConstructBuilding,
                EncontrarLugarParaConstruir(item),
                item,
                prioridade
            );
        }
    }

    void ComprarUnidade(string nomeParcial, PriorityLevel prioridade)
    {
        var item = EncontrarNoMenu(nomeParcial);
        if (item != null && recursosIA >= item.preco)
        {
            recursosIA -= item.preco;
            historicoConstrucoes.Add(item.nomeItem);

            recebedor.ReceberPedido(
                "General IA",
                ActionType.RecruitUnit,
                Vector3.zero,
                item,
                prioridade
            );
        }
    }

    DadosConstrucao EncontrarNoMenu(string parteDoNome)
    {
        return MenuConstrucao.catalogoGlobal.FirstOrDefault(x => x.nomeItem.IndexOf(parteDoNome, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    int ContarMeusPredios(string nomeParcial)
    {
        // Conta no histórico OTIMISTA. 
        // No futuro verificar Physics.OverlapSphere ao redor da base da IA.
        return historicoConstrucoes.Count(x => x.IndexOf(nomeParcial, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    Vector3 EncontrarLugarParaConstruir(DadosConstrucao item)
    {
        // Espiral ao redor da base para não construir tudo um em cima do outro
        float angulo = Random.Range(0, 360);
        float dist = Random.Range(15, 60); // Distância segura
        Vector3 offset = new Vector3(Mathf.Cos(angulo)*dist, 0, Mathf.Sin(angulo)*dist);
        
        Vector3 tentativa = transform.position + offset;
        
        // Raycast para altura do terreno
        if (Physics.Raycast(tentativa + Vector3.up * 50, Vector3.down, out RaycastHit hit, 100))
        {
            return hit.point;
        }
        return tentativa;
    }
}
