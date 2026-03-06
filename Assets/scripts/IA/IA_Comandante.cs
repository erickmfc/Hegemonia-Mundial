using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CÉREBRO SUPREMO - COMANDANTE GLOBAL (RTS)
/// Gerencia a estratégia macro, expansão e logística de transporte do time inimigo.
/// </summary>
public class IA_Comandante : MonoBehaviour
{
    // --- CONFIGURAÇÕES ---
    [Header("Identidade")]
    public int TeamID = 3; // Inimigo Padrão
    public IdentidadeIA identidade; // Referência para compatibilidade com sistema de ID
    public string NomeComandante = "General Kaos";

    [Header("Recursos")]
    public float dinheiro = 5000f;
    public float rendaPorSegundo = 15f; // Renda passiva base

    [Header("Estado Mental")]
    public EstadoEstrategico estadoAtual = EstadoEstrategico.Paz_Desenvolvimento;
    public float intervaloDecisao = 2.0f; // Segundos entre pensamentos

    public enum EstadoEstrategico
    {
        Paz_Desenvolvimento, // Focada na economia, urbanismo e envio de turistas/imigrantes
        Expandir,            // Buscar novos recursos e construir bases logísticas
        Fortificar,          // Construir defesas nas bases existentes
        Acumular_Forcas,     // Criar tropas militares e reunir na base
        Ataque_Total         // Enviar TODO o exército para a base do jogador
    }

    // --- CONHECIMENTO GLOBAL ---
    [Header("Inteligência")]
    public List<GameObject> minhasUnidades = new List<GameObject>();
    public List<GameObject> minhasBases = new List<GameObject>();
    public List<GameObject> meusTransportes = new List<GameObject>();
    public List<GameObject> meusCivis = new List<GameObject>(); // Para turismo/imigração
    
    // Locais de interesse (Recursos neutros, bases inimigas descobertas)
    public List<Transform> pontosDeRecursoConhecidos = new List<Transform>();
    public Transform alvoAtaquePrincipal; // Base do Jogador
    public Vector3 pontoDeReuniao;        // Rally Point atual
    public Transform basePrincipal;       // QG ou primeira base

    // --- MÓDULOS PRO (Cérebros Especializados) ---
    public IA_General_Pro cerebroGeneral;
    public IA_Arquiteto_Pro cerebroArquiteto;
    public IA_Economia cerebroEconomico; // Mantido para compatibilidade
    public IA_Combate cerebroCombate;    // Mantido para compatibilidade

    // --- REFERÊNCIAS ---
    private GerenteDeJogo gerenteJogo;
    private MenuConstrucao menuConstrucao; // Acesso ao catálogo de prefabs

    void Awake()
    {
        // Garante componente de identidade
        if(identidade == null) identidade = GetComponent<IdentidadeIA>();
        // Fallback: se não tiver no objeto, adiciona
        if(identidade == null) identidade = gameObject.AddComponent<IdentidadeIA>();
        identidade.teamID = TeamID;
    }

    void Start()
    {
        gerenteJogo = FindFirstObjectByType<GerenteDeJogo>();
        menuConstrucao = FindFirstObjectByType<MenuConstrucao>(); // Para ler o catálogo

        // Inicializa Módulos
        cerebroGeneral = GetComponent<IA_General_Pro>();
        if (cerebroGeneral == null) cerebroGeneral = gameObject.AddComponent<IA_General_Pro>();
        cerebroGeneral.Inicializar(this);

        cerebroArquiteto = GetComponent<IA_Arquiteto_Pro>();
        if (cerebroArquiteto == null) cerebroArquiteto = gameObject.AddComponent<IA_Arquiteto_Pro>();
        cerebroArquiteto.Inicializar(this);

        // Inicializa ponto de reunião perto da primeira base ou do próprio comandante
        pontoDeReuniao = transform.position;
        if(minhasBases.Count > 0) basePrincipal = minhasBases[0].transform;

        // Inicia o Cérebro
        StartCoroutine(CicloDeDecisao());
        StartCoroutine(RendaPassiva());
    }

    // --- MÁQUINA DE ESTADOS (COROUTINE) ---
    IEnumerator CicloDeDecisao()
    {
        while (true)
        {
            yield return new WaitForSeconds(intervaloDecisao);

            AtualizarCenso(); // Conta unidades vivas e mortas
            EscanearMapa();   // Busca recursos novos

            switch (estadoAtual)
            {
                case EstadoEstrategico.Paz_Desenvolvimento:
                    LogicaPazEDesenvolvimento();
                    break;
                case EstadoEstrategico.Expandir:
                    LogicaExpandir();
                    break;
                case EstadoEstrategico.Fortificar:
                    LogicaFortificar();
                    break;
                case EstadoEstrategico.Acumular_Forcas:
                    LogicaAcumular();
                    break;
                case EstadoEstrategico.Ataque_Total:
                    LogicaAtaqueTotal();
                    break;
            }

            // Transição dinâmica do comportamento (Agressivo vs Pacífico)
            AvaliarMudancaDeEstado();
        }
    }

    void AvaliarMudancaDeEstado()
    {
        int totalSoldados = minhasUnidades.Count(u => u != null && !u.name.Contains("Construtor") && !u.name.Contains("Civil"));
        int totalCivis = meusCivis.Count(u => u != null);
        
        // A IA começa pacífica, juntando forças ou colonizando. 
        // Se a economia está forte, ela expande e investe em civis/turismo.
        
        // Se tem menos de 2 bases, foca em exploração
        if (minhasBases.Count < 2 && dinheiro > 1000) 
        {
            estadoAtual = EstadoEstrategico.Expandir;
            return;
        }

        // Se está incrivelmente rica e estruturada, ataca
        if (totalSoldados >= 15 && dinheiro > 2000)
        {
            estadoAtual = EstadoEstrategico.Ataque_Total;
            return;
        }
        
        // Se perdeu o exército, volta pra casa
        if (totalSoldados < 5 && estadoAtual == EstadoEstrategico.Ataque_Total)
        {
            estadoAtual = EstadoEstrategico.Acumular_Forcas;
            return;
        }

        // Se tem economia sobrando mas ainda não tem exército colossal, foca no Povo.
        if (dinheiro > 1000 && totalSoldados >= 5 && totalSoldados < 15)
        {
            estadoAtual = EstadoEstrategico.Paz_Desenvolvimento;
            return;
        }
        
        // Estado base se não caiu nas outras regras
        if (estadoAtual == EstadoEstrategico.Expandir && minhasBases.Count >= 2)
        {
            estadoAtual = EstadoEstrategico.Paz_Desenvolvimento;
        }
    }

    // --- LÓGICA DE CADA ESTADO ---

    void LogicaPazEDesenvolvimento()
    {
        // 1. O foco de paz é ganhar muito dinheiro, gerenciar turismo e construir cidades
        if (alvoAtaquePrincipal == null || !alvoAtaquePrincipal.gameObject.activeInHierarchy)
        {
            if (cerebroGeneral != null) alvoAtaquePrincipal = cerebroGeneral.BuscarAlvo();
        }

        // 2. Tentar enviar Embaixadores / Turistas civis até a prefeitura do jogador (Causando imigração)
        if (dinheiro > 500 && alvoAtaquePrincipal != null && meusCivis.Count < 5)
        {
            if (cerebroGeneral != null)
            {
                cerebroGeneral.RecrutarTurista();
            }
        }
    }

    void LogicaExpandir()
    {
        // 1. Encontrar recurso livre mais próximo
        Transform recursoAlvo = null;
        float menorDistancia = float.MaxValue;

        foreach (var ponto in pontosDeRecursoConhecidos)
        {
            // Verifica se já não tem uma base minha lá (distância < 20m)
            bool jaOcupado = minhasBases.Any(b => Vector3.Distance(b.transform.position, ponto.position) < 20f);
            
            if (!jaOcupado)
            {
                float d = Vector3.Distance(transform.position, ponto.position);
                if (d < menorDistancia)
                {
                    menorDistancia = d;
                    recursoAlvo = ponto;
                }
            }
        }

        if (recursoAlvo != null)
        {
            // Envia construtor
            MoverConstrutorPara(recursoAlvo.position);
        }
    }

    void LogicaFortificar()
    {
        // Verifica se cada base tem defesas (Torretas)
        // Se não, gasta dinheiro para construir
    }

    void LogicaAcumular()
    {
        // Define Ponto de Reunião seguro (ex: entre as bases)
        if (minhasBases.Count > 0)
        {
             pontoDeReuniao = minhasBases[0].transform.position + Vector3.forward * 10f;
             if(cerebroGeneral != null) 
             {
                 // Opcional: Avisar general para ser passivo? e.g. cerebroGeneral.minimoParaAtacar = 999; 
             }
        }
        
        // A compra de unidades agora é delegada 100% ao IA_General_Pro.
        // O Comandante apenas define a postura estratégica.
    }

    void LogicaAtaqueTotal()
    {
        // Atualiza ativamente o alvo caso o alvo principal tenha sido destruído!
        if (alvoAtaquePrincipal == null || !alvoAtaquePrincipal.gameObject.activeInHierarchy)
        {
            if (cerebroGeneral != null) alvoAtaquePrincipal = cerebroGeneral.BuscarAlvo();
            if (alvoAtaquePrincipal == null) return; // Inimigo totalmente aniquilado?
        }

        // Ordena TODAS as unidades militares a atacar
        foreach (var unidade in minhasUnidades)
        {
            if (unidade == null) continue;
            
            // Verifica se precisa de transporte para chegar no alvo
            MoverUnidadeComLogistica(unidade, alvoAtaquePrincipal.position);
        }
    }

    // --- SISTEMA DE LOGÍSTICA E TRANSPORTE (CRÍTICO) ---

    public void MoverUnidadeComLogistica(GameObject unidade, Vector3 destinoFinal)
    {
        NavMeshAgent agente = unidade.GetComponent<NavMeshAgent>();
        if (agente == null) return;

        // 1. Verifica Caminho Direto (Terra)
        NavMeshPath caminho = new NavMeshPath();
        agente.CalculatePath(destinoFinal, caminho);

        if (caminho.status == NavMeshPathStatus.PathComplete)
        {
            // Caminho livre! Vai andando.
            ControleUnidade controle = unidade.GetComponent<ControleUnidade>();
            if (controle) controle.MoverParaPonto(destinoFinal);
            else agente.SetDestination(destinoFinal);
        }
        else
        {
            // 2. Caminho Bloqueado ou Parcial (Provavel Água/Ilha)
            IniciarProtocoloTransporte(unidade, destinoFinal);
        }
    }

    void IniciarProtocoloTransporte(GameObject unidade, Vector3 destino)
    {
        // A. Verifica se temos transporte disponível
        GameObject transporte = ObterTransporteLivre();

        if (transporte == null)
        {
            // Se não tem, encomenda um! (Via general se possível)
            // if (dinheiro > 1000) ComprarUnidadeIA("Hovercraft"); 
            
            // Unidade espera no ponto de reunião por enquanto
            MoverPara(unidade, pontoDeReuniao);
            return;
        }

        // B. Lógica de Embarque (Placeholder)
        MoverPara(unidade, transporte.transform.position);
    }

    GameObject ObterTransporteLivre()
    {
        return meusTransportes.FirstOrDefault(t => t != null && Vector3.Distance(t.transform.position, transform.position) < 500); 
    }

    // --- UTILITÁRIOS ---

    void MoverPara(GameObject unidade, Vector3 pos)
    {
        var ctrl = unidade.GetComponent<ControleUnidade>();
        if (ctrl) ctrl.MoverParaPonto(pos);
    }

    void MoverConstrutorPara(Vector3 pos)
    {
        // Acha um construtor ocioso e manda ir construir base
        var construtor = minhasUnidades.FirstOrDefault(u => u.name.Contains("Construtor")); // Nome hipotético
        if (construtor != null) MoverPara(construtor, pos);
    }

    void EscanearMapa()
    {
        // Simulação de "Satélite" Global
        // Encontra todos os nodes de Petróleo/Recursos no mapa
        
        // Exemplo fictício: Adiciona objetos com tag "Recurso"
        // var recursos = GameObject.FindGameObjectsWithTag("Recurso"); 
        pontosDeRecursoConhecidos.Clear();
        // Caso exista script Recurso:
        // var recursos = FindObjectsByType<Recurso>(FindObjectsSortMode.None);
        // foreach (var r in recursos) pontosDeRecursoConhecidos.Add(r.transform);
    }

    void AtualizarCenso()
    {
        // Remove nulos (mortos)
        minhasUnidades.RemoveAll(u => u == null);
        minhasBases.RemoveAll(b => b == null);
        meusTransportes.RemoveAll(t => t == null);

        if(minhasBases.Count > 0 && basePrincipal == null) basePrincipal = minhasBases[0].transform;
    }

    // --- ECONOMIA E PRODUÇÃO ---

    IEnumerator RendaPassiva()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            dinheiro += rendaPorSegundo;
            // Ganho extra por base
            dinheiro += minhasBases.Count * 10f; 
        }
    }

    public Transform pontoDeSpawnPadrao()
    {
        if (minhasBases.Count > 0 && minhasBases[0] != null) return minhasBases[0].transform;
        return transform; // Fallback
    }

    public void RegistrarUnidade(GameObject unidade)
    {
        if(unidade == null) return;
        if (unidade.name.Contains("Transporte") || unidade.name.Contains("Hovercraft"))
        {
            meusTransportes.Add(unidade);
        }
        else if (unidade.GetComponent<NavMeshAgent>() != null)
        {
            minhasUnidades.Add(unidade);
        }
        else
        {
             // Assumimos que é prédio se não anda
             minhasBases.Add(unidade);
             if(basePrincipal == null) basePrincipal = unidade.transform;
        }

        // Configura Identidade
        var id = unidade.GetComponent<IdentidadeUnidade>();
        if(id != null && identidade != null)
        {
            id.teamID = identidade.teamID;
        }
    }
    
    // --- API FINANCEIRA (Para manter compatibilidade) ---
    public bool GastarDinheiro(float valor)
    {
        if (dinheiro >= valor)
        {
            dinheiro -= valor;
            return true;
        }
        return false;
    }

    public void AdicionarDinheiro(float valor)
    {
        dinheiro += valor;
    }
}
