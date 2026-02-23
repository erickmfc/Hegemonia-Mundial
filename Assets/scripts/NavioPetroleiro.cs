using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class NavioPetroleiro : ControleUnidade
{
    [Header("Configurações do Ciclo")]
    public float tempoSaidaEstaleiro = 8.0f;
    public float velocidadeSaidaEstaleiro = 12.0f; // Velocidade maior para sair do estaleiro
    public float velocidadeManobra = 4.0f;
    public float velocidadeAcoplagem = 12.0f; // Velocidade rápida para acoplagem/desacoplagem
    public float tempoDeCarregamento = 5.0f;

    [Header("Carga")]
    public int petroleoCarregado = 0;
    public int capacidadeMaxima = 5000;
    public int taxaTransferencia = 1000; // Por segundo

    public enum EstadoPetroleiro
    {
        NASCENDO,
        SAINDO_ESTALEIRO,
        INDO_PLATAFORMA,
        ACOPLANDO_PLATAFORMA,
        CARREGANDO,
        SAINDO_PLATAFORMA,
        INDO_PIER,
        ACOPLANDO_PIER,
        DESCARREGANDO,
        RE_DO_PIER
    }

    [Header("Monitoramento")]
    public EstadoPetroleiro estadoAtual;
    public string statusDebug; // Leia isso no Inspector para saber o que ele pensa

    // Componentes
    private NavMeshAgent agenteNav;
    private float timerEstado = 0f;

    // Alvos
    public PlataformaOffshore plataformaAlvo;
    public PierMarinha pierAlvo;
    private Vector3? pontoDeSaidaEstaleiro = null;

    protected override void Awake()
    {
        base.Awake();
        agenteNav = GetComponent<NavMeshAgent>();
    }

    protected override void Start()
    {
        base.Start();
        
        // Configuração inicial de física e navegação
        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;
        if (agenteNav != null)
        {
            agenteNav.enabled = false; // Começa desligado para manobra manual
            agenteNav.stoppingDistance = 1.0f;
        }

        MudarEstado(EstadoPetroleiro.NASCENDO);
    }

    // Chamado pelo Estaleiro (Opcional)
    public void DefinirSaidaEstaleiro(Vector3 pontoSaida)
    {
        pontoDeSaidaEstaleiro = pontoSaida;
    }

    void Update()
    {
        // Máquina de Estados
        switch (estadoAtual)
        {
            case EstadoPetroleiro.NASCENDO:
                MudarEstado(EstadoPetroleiro.SAINDO_ESTALEIRO);
                break;

            case EstadoPetroleiro.SAINDO_ESTALEIRO:
                ExecutarManobraRe(tempoSaidaEstaleiro, EstadoPetroleiro.INDO_PLATAFORMA, true, velocidadeSaidaEstaleiro);
                break;

            case EstadoPetroleiro.INDO_PLATAFORMA:
                ExecutarViagemNavMesh(() => MudarEstado(EstadoPetroleiro.ACOPLANDO_PLATAFORMA));
                break;

            case EstadoPetroleiro.ACOPLANDO_PLATAFORMA:
                if(plataformaAlvo != null) 
                    ExecutarAcoplagemManual(plataformaAlvo.pontoAbastecer, EstadoPetroleiro.CARREGANDO);
                break;

            case EstadoPetroleiro.CARREGANDO:
                // Agora carrega de verdade!
                ExecutarOperacaoLogistica(true, EstadoPetroleiro.SAINDO_PLATAFORMA);
                break;

            case EstadoPetroleiro.SAINDO_PLATAFORMA:
                if(plataformaAlvo != null)
                    ExecutarSaidaManual(plataformaAlvo.pontoSaida, EstadoPetroleiro.INDO_PIER);
                break;

            case EstadoPetroleiro.INDO_PIER:
                ExecutarViagemNavMesh(() => MudarEstado(EstadoPetroleiro.ACOPLANDO_PIER));
                break;

            case EstadoPetroleiro.ACOPLANDO_PIER:
                if(pierAlvo != null)
                    ExecutarAcoplagemManual(pierAlvo.Atraca_petro, EstadoPetroleiro.DESCARREGANDO);
                break;

            case EstadoPetroleiro.DESCARREGANDO:
                ExecutarOperacaoLogistica(false, EstadoPetroleiro.RE_DO_PIER);
                break;

            case EstadoPetroleiro.RE_DO_PIER:
                ExecutarManobraRe(6.0f, EstadoPetroleiro.INDO_PLATAFORMA, false, velocidadeManobra);
                break;
        }
    }

    // --- LÓGICA DE TRANSIÇÃO DE ESTADOS ---

    void MudarEstado(EstadoPetroleiro novo)
    {
        estadoAtual = novo;
        timerEstado = 0f;
        statusDebug = "Iniciando: " + novo.ToString();
        Debug.Log($"[Navio] Mudando para: {novo}");

        switch (novo)
        {
            case EstadoPetroleiro.INDO_PLATAFORMA:
                BuscarPlataforma();
                if (plataformaAlvo != null && plataformaAlvo.pontoChegada != null)
                    ConfigurarNavMesh(plataformaAlvo.pontoChegada.position);
                break;

            case EstadoPetroleiro.INDO_PIER:
                BuscarPier(); 
                if (pierAlvo != null && pierAlvo.saida_petro != null)
                    ConfigurarNavMesh(pierAlvo.saida_petro.position);
                else
                    Debug.LogError("[Navio] ERRO CRÍTICO: Não achei o Pier ou o ponto 'saida_petro' está vazio!");
                break;

            case EstadoPetroleiro.ACOPLANDO_PLATAFORMA:
            case EstadoPetroleiro.ACOPLANDO_PIER:
            case EstadoPetroleiro.SAINDO_ESTALEIRO:
            case EstadoPetroleiro.RE_DO_PIER:
            case EstadoPetroleiro.SAINDO_PLATAFORMA:
                DesligarNavMesh(); // Manobras finas são manuais
                break;
        }
    }

    // --- COMPORTAMENTOS (ACTIONS) ---

    // --- COMPORTAMENTOS (ACTIONS) ---

    void ExecutarManobraRe(float tempo, EstadoPetroleiro proximoEstado, bool usarSaidaEstaleiro, float velocidade)
    {
        timerEstado += Time.deltaTime;
        
        // Move para trás ou para o ponto de saída do estaleiro
        if (usarSaidaEstaleiro && pontoDeSaidaEstaleiro.HasValue)
        {
             // Mantém altura fixa (Y do navio) para o movimento
             Vector3 destino = pontoDeSaidaEstaleiro.Value;
             destino.y = transform.position.y;
             
             transform.position = Vector3.MoveTowards(transform.position, destino, velocidade * Time.deltaTime);
             
             Vector3 dir = pontoDeSaidaEstaleiro.Value - transform.position;
             dir.y = 0; 
             if(dir != Vector3.zero) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 20f * Time.deltaTime);
        }
        else
        {
            transform.Translate(Vector3.back * velocidade * Time.deltaTime);
        }

        Vector3 euler = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0, euler.y, 0);

        if (timerEstado >= tempo)
        {
            MudarEstado(proximoEstado);
        }
    }

    void ExecutarViagemNavMesh(System.Action aoChegar)
    {
        if (!agenteNav.enabled || !agenteNav.isOnNavMesh)
        {
            statusDebug = "Tentando reconectar NavMesh...";
            AtivarNavMeshNoLocal();
            return;
        }

        if(transform.rotation.x != 0 || transform.rotation.z != 0)
        {
             Vector3 euler = transform.rotation.eulerAngles;
             transform.rotation = Quaternion.Euler(0, euler.y, 0);
        }

        if (!agenteNav.pathPending && agenteNav.remainingDistance <= agenteNav.stoppingDistance + 0.5f)
        {
            aoChegar.Invoke();
        }
    }

    void ExecutarAcoplagemManual(Transform alvo, EstadoPetroleiro proximo)
    {
        if (alvo == null) 
        {
            MudarEstado(proximo); 
            return; 
        }

        timerEstado += Time.deltaTime; 

        float dist = Vector3.Distance(transform.position, alvo.position);
        
        Vector3 destino = alvo.position;
        destino.y = transform.position.y;

        // VELOCIDADE RÁPIDA: Usa a velocidade de acoplagem específica (12.0f)
        float velAtual = velocidadeAcoplagem; 
        // Desacelera apenas quando MUITO perto (últimos 8 metros) para não bater
        if(dist < 8.0f) velAtual = Mathf.Lerp(velAtual, 2.0f, 1 - (dist / 8.0f));
        // Garante mínimo de movimento
        velAtual = Mathf.Max(velAtual, 1.0f);

        transform.position = Vector3.MoveTowards(transform.position, destino, velAtual * Time.deltaTime);
        
        // ROTAÇÃO SUAVE E INTELIGENTE
        // Longe (> 15m): Olha para o alvo
        // Perto (<= 15m): Começa a alinhar com a rotação final do alvo
        Quaternion rotDestino;
        if(dist > 15.0f)
        {
            Vector3 dir = destino - transform.position;
            dir.y = 0;
            if(dir != Vector3.zero) rotDestino = Quaternion.LookRotation(dir);
            else rotDestino = transform.rotation;
        }
        else
        {
             rotDestino = Quaternion.Euler(0, alvo.rotation.eulerAngles.y, 0);
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotDestino, 60f * Time.deltaTime);

        // Debug
        Debug.DrawLine(transform.position, destino, Color.cyan);

        // Condição de chegada ou Timeout de segurança (40s para garantir que não teletransporta visualmente)
        if (dist < 0.8f || timerEstado > 40.0f) 
        {
            if(timerEstado > 40.0f) Debug.LogWarning("[Navio] Timeout Acoplagem! Forçando transição.");
            
            // Snap final apenas se estiver perto o suficiente para não "pular" na tela
            if(dist < 5.0f) {
                transform.position = destino;
                transform.rotation = Quaternion.Euler(0, alvo.rotation.eulerAngles.y, 0);
            }
            
            MudarEstado(proximo);
        }
    }

    void ExecutarSaidaManual(Transform alvoSaida, EstadoPetroleiro proximo)
    {
         timerEstado += Time.deltaTime;

         if (alvoSaida == null) { MudarEstado(proximo); return; }

         Vector3 destino = alvoSaida.position;
         destino.y = transform.position.y;
         
         // Usa a mesma velocidade rápida para sair
         transform.position = Vector3.MoveTowards(transform.position, destino, velocidadeAcoplagem * Time.deltaTime);
         
         Vector3 dir = alvoSaida.position - transform.position;
         dir.y = 0; 
         if(dir != Vector3.zero) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 60f * Time.deltaTime);

         transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

         float distH = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(alvoSaida.position.x, alvoSaida.position.z));

         // Tolerância maior na saída
         if (distH < 2.5f || timerEstado > 20.0f) 
         {
             MudarEstado(proximo);
         }
    }

    void ExecutarOperacaoLogistica(bool carregando, EstadoPetroleiro proximo)
    {
        timerEstado += Time.deltaTime;
        
        int qtdPorFrame = Mathf.RoundToInt(taxaTransferencia * Time.deltaTime);
        
        // Define o tempo máximo de operação (usa a variável do inspector ou um mínimo de 15s se a do inspector for muito curta, para dar tempo de carregar)
        // Na verdade, vamos respeitar o Inspector E a capacidade.
        float tempoLimite = Mathf.Max(tempoDeCarregamento, 2.0f); // Pelo menos 2s
        
        if (carregando)
        {
            if (plataformaAlvo != null)
            {
                // Só carrega se couber no navio E se a plataforma tiver
                int espacoLivre = capacidadeMaxima - petroleoCarregado;
                int aPedir = Mathf.Min(qtdPorFrame, espacoLivre);
                
                // Verifica também se a plataforma tem petroleo (plataforma pode estar vazia)
                if (plataformaAlvo.petroleoArmazenado <= 0)
                {
                     statusDebug = "Plataforma Vazia! Aguardando...";
                }
                
                if (aPedir > 0)
                {
                    int recebido = plataformaAlvo.DrenarPetroleo(aPedir);
                    petroleoCarregado += recebido;
                }
            }
            statusDebug = $"Enchendo: {petroleoCarregado}/{capacidadeMaxima} ({timerEstado:F1}s)";
            
            // CONDIÇÃO DE SAÍDA:
            // 1. Tanque Cheio
            // 2. OU Tempo Limite excedido (para não ficar eternamente se a plataforma estiver vazia)
            // Mas se a plataforma estiver vazia, talvez devêssemos esperar um pouco mais? 
            // O usuário disse: "depois do tempo ele deve sair sozinho". Então usamos o tempo.
            
            bool tanqueCheio = petroleoCarregado >= capacidadeMaxima;
            bool tempoEsgotado = timerEstado > tempoLimite;

            if (tanqueCheio || tempoEsgotado)
            {
                Debug.Log($"[Navio] Carregamento concluído. Cheio: {tanqueCheio}, Tempo: {tempoEsgotado}");
                MudarEstado(proximo);
            }
        }
        else
        {
            // DESCARREGANDO
            if (pierAlvo != null && petroleoCarregado > 0)
            {
                int aEntregar = Mathf.Min(qtdPorFrame, petroleoCarregado);
                pierAlvo.ReceberPetroleo(aEntregar);
                petroleoCarregado -= aEntregar;
            }
            statusDebug = $"Esvaziando: {petroleoCarregado}/{capacidadeMaxima} ({timerEstado:F1}s)";

            bool tanqueVazio = petroleoCarregado <= 0;
            bool tempoEsgotado = timerEstado > tempoLimite;

            if (tanqueVazio || tempoEsgotado)
            {
                MudarEstado(proximo);
            }
        }
    }

    // --- SISTEMAS DE NAVEGAÇÃO ---

    void ConfigurarNavMesh(Vector3 destino)
    {
        AtivarNavMeshNoLocal();
        if (agenteNav.enabled)
        {
            agenteNav.SetDestination(destino);
            agenteNav.isStopped = false;
        }
    }

    void AtivarNavMeshNoLocal()
    {
        if (agenteNav.enabled) return;

        NavMeshHit hit;
        // Procura chão navegável num raio de 10 metros
        if (NavMesh.SamplePosition(transform.position, out hit, 10.0f, NavMesh.AllAreas))
        {
            agenteNav.Warp(hit.position);
            agenteNav.enabled = true;
        }
        else
        {
            Debug.LogError("[Navio] Não consegui achar NavMesh aqui! O navio vai travar.");
        }
    }

    void DesligarNavMesh()
    {
        if (agenteNav.enabled)
        {
            agenteNav.isStopped = true;
            agenteNav.enabled = false;
        }
    }

    // --- BUSCADORES ---

    void BuscarPlataforma()
    {
        plataformaAlvo = FindObjectOfType<PlataformaOffshore>(); // Pega a primeira que achar
        if (plataformaAlvo == null) Debug.LogError("[Navio] Nenhuma PlataformaOffshore na cena!");
    }

    void BuscarPier()
    {
        pierAlvo = FindObjectOfType<PierMarinha>(); // Pega o primeiro que achar
        if (pierAlvo == null) Debug.LogError("[Navio] Nenhum PierMarinha na cena!");
    }
}
