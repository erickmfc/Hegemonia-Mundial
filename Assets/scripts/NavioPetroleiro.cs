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
        // MODO PASSIVO: navio esperando infraestrutura
        AGUARDANDO_INFRAESTRUTURA,
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
    private IdentidadeUnidade identidadeCache;
    // A equipe de operação é definida pelo estaleiro/píer no momento do
    // nascimento. Não deve ser inferida novamente por controladores depois,
    // pois isso fazia o petroleiro do jogador entrar na logística da IA.
    private int equipeOperacaoFixa;
    private float timerEstado = 0f;
    private Vector3 destinoNavMeshAtual;
    private bool possuiDestinoNavMesh;
    private float proximoReplanejamento;
    private bool fallbackAquaticoAtivo;
    private bool avisoFallbackAquaticoEmitido;

    // Alvos
    public PlataformaOffshore plataformaAlvo;
    public PierMarinha pierAlvo;
    private Vector3? pontoDeSaidaEstaleiro = null;

    // Controle do modo passivo
    private float _timerVerificacao = 0f;
    private const float INTERVALO_VERIFICACAO = 5f; // Verifica a cada 5s se a infra está pronta

    protected override void Awake()
    {
        base.Awake();
        agenteNav = GetComponent<NavMeshAgent>();
        identidadeCache = GetComponent<IdentidadeUnidade>();
        if (identidadeCache == null)
        {
            identidadeCache = GetComponentInParent<IdentidadeUnidade>();
        }
    }

    protected override void Start()
    {
        base.Start();
        
        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;
        if (agenteNav != null)
        {
            agenteNav.enabled = false;
            agenteNav.stoppingDistance = 1.0f;
        }

        // 🚢 Começa no MODO PASSIVO: apenas aguarda a infraestrutura estar pronta!
        MudarEstado(EstadoPetroleiro.NASCENDO);
    }

    // Chamado pelo Estaleiro (Opcional)
    public void DefinirSaidaEstaleiro(Vector3 pontoSaida)
    {
        pontoDeSaidaEstaleiro = pontoSaida;
    }

    public void DefinirEquipeOperacao(int teamId)
    {
        equipeOperacaoFixa = Mathf.Max(1, teamId);
        if (identidadeCache == null)
        {
            identidadeCache = GetComponent<IdentidadeUnidade>() ?? GetComponentInParent<IdentidadeUnidade>();
        }

        if (identidadeCache == null)
        {
            identidadeCache = gameObject.AddComponent<IdentidadeUnidade>();
        }

        identidadeCache.teamID = equipeOperacaoFixa;
        identidadeCache.tipoUnidade = TipoUnidade.Naval;
    }

    void Update()
    {
        // Máquina de Estados
        switch (estadoAtual)
        {
            case EstadoPetroleiro.NASCENDO:
                // Vai para PASSIVO ao nascer (nunca inicia automaticamente)
                MudarEstado(EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA);
                break;

            case EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA:
                // ===================================================
                // MODO PASSIVO: Verifica a cada 5s se já há Plataforma E Pier na cena.
                // Quando encontrar ambos, liga o ciclo de trabalho automaticamente.
                // ===================================================
                _timerVerificacao += Time.deltaTime;
                if (_timerVerificacao >= INTERVALO_VERIFICACAO)
                {
                    _timerVerificacao = 0f;
                    VerificarEAtivarServico();
                }
                break;

            case EstadoPetroleiro.SAINDO_ESTALEIRO:
                ExecutarManobraRe(tempoSaidaEstaleiro, EstadoPetroleiro.INDO_PLATAFORMA, true, velocidadeSaidaEstaleiro);
                break;

            case EstadoPetroleiro.INDO_PLATAFORMA:
                ExecutarViagemNavMesh(() => MudarEstado(EstadoPetroleiro.ACOPLANDO_PLATAFORMA));
                break;

            case EstadoPetroleiro.ACOPLANDO_PLATAFORMA:
                if (plataformaAlvo != null && !plataformaAlvo.EhOcupante(this)
                    && !plataformaAlvo.TentarOcupar(this))
                {
                    statusDebug = "Fila da plataforma: aguardando vaga livre.";
                    return;
                }
                if(plataformaAlvo != null) 
                    ExecutarAcoplagemManual(plataformaAlvo.pontoAbastecer, EstadoPetroleiro.CARREGANDO);
                break;

            case EstadoPetroleiro.CARREGANDO:
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
                if (pierAlvo != null && !pierAlvo.EhOcupanteLogistica(this)
                    && !pierAlvo.TentarOcuparLogistica(this))
                {
                    statusDebug = "Fila do pier: aguardando vaga livre.";
                    return;
                }
                if(pierAlvo != null)
                    ExecutarAcoplagemManual(pierAlvo.Atraca_petro, EstadoPetroleiro.DESCARREGANDO);
                break;

            case EstadoPetroleiro.DESCARREGANDO:
                ExecutarOperacaoLogistica(false, EstadoPetroleiro.RE_DO_PIER);
                break;

            case EstadoPetroleiro.RE_DO_PIER:
                ExecutarReAteSaidaDoPier();
                break;
        }
    }

    // ===================================================
    // ATIVAÇÃO AUTOMÁTICA: Procura Plataforma + Pier
    // Só liga o navio quando a infraestrutura estiver montada!
    // ===================================================
    void VerificarEAtivarServico()
    {
        SelecionarAlvosLogisticos(true);

        // Só ativa se ambos existirem
        if (plataformaAlvo != null && pierAlvo != null)
        {
            statusDebug = "Infraestrutura do time encontrada! Iniciando serviço de logística.";
            Debug.Log("[Navio Petroleiro] Plataforma e Pier do proprio time detectados! Saindo para trabalhar.");
            MudarEstado(EstadoPetroleiro.SAINDO_ESTALEIRO);
        }
        else
        {
            string faltando = (plataformaAlvo == null && pierAlvo == null) ? "Plataforma e Pier" :
                              (plataformaAlvo == null) ? "Plataforma Offshore" : "Pier Marinha";
            statusDebug = $"Modo Passivo: Aguardando {faltando}...";
        }
    }

    // --- LÓGICA DE TRANSIÇÃO DE ESTADOS ---

    void MudarEstado(EstadoPetroleiro novo)
    {
        EstadoPetroleiro anterior = estadoAtual;
        estadoAtual = novo;
        timerEstado = 0f;
        statusDebug = "Iniciando: " + novo.ToString();
        Debug.Log($"[Navio] Mudando para: {novo}");

        switch (novo)
        {
            case EstadoPetroleiro.INDO_PLATAFORMA:
                LiberarReservas();
                if (!SelecionarAlvosLogisticos(true))
                {
                    statusDebug = "Sem rota logistica livre para o time " + TeamIdAtual;
                    MudarEstado(EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA);
                    break;
                }
                if (plataformaAlvo != null && plataformaAlvo.pontoChegada != null)
                {
                    ConfigurarNavMesh(plataformaAlvo.pontoChegada.position);
                }
                else
                {
                    MudarEstado(EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA);
                }
                break;

            case EstadoPetroleiro.INDO_PIER:
                if (plataformaAlvo != null)
                {
                    plataformaAlvo.Liberar(this);
                    plataformaAlvo.LiberarReserva(this);
                }

                if (pierAlvo == null || !PertenceAoMesmoTime(pierAlvo) || pierAlvo.EstaReservadoPorOutro(this))
                {
                    BuscarPier();
                }

                if (pierAlvo != null && pierAlvo.saida_petro != null)
                {
                    ConfigurarNavMesh(pierAlvo.saida_petro.position);
                }
                else
                {
                    statusDebug = "Pier do time ausente ou sem saida_petro.";
                    Debug.LogError("[Navio] ERRO CRITICO: Nao achei o Pier do time ou o ponto 'saida_petro' esta vazio!");
                    MudarEstado(EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA);
                }
                break;

            case EstadoPetroleiro.ACOPLANDO_PLATAFORMA:
                if (plataformaAlvo != null)
                {
                    plataformaAlvo.TentarOcupar(this);
                    plataformaAlvo.TentarReservar(this, 90f);
                }
                DesligarNavMesh();
                break;

            case EstadoPetroleiro.ACOPLANDO_PIER:
                if (pierAlvo != null)
                {
                    pierAlvo.TentarOcuparLogistica(this);
                    pierAlvo.TentarReservarLogistica(this, 90f);
                }
                DesligarNavMesh();
                break;

            case EstadoPetroleiro.SAINDO_ESTALEIRO:
            case EstadoPetroleiro.RE_DO_PIER:
            case EstadoPetroleiro.SAINDO_PLATAFORMA:
                DesligarNavMesh(); // Manobras finas são manuais
                break;
        }

        if (novo == EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA && anterior != EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA)
        {
            LiberarReservas();
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

    void ExecutarReAteSaidaDoPier()
    {
        timerEstado += Time.deltaTime;

        if (pierAlvo == null || pierAlvo.saida_petro == null)
        {
            ExecutarManobraRe(6.0f, EstadoPetroleiro.INDO_PLATAFORMA, false, velocidadeManobra);
            return;
        }

        Vector3 destino = pierAlvo.saida_petro.position;
        destino.y = transform.position.y;

        transform.position = Vector3.MoveTowards(transform.position, destino, velocidadeManobra * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        Debug.DrawLine(transform.position, destino, Color.yellow);

        float distH = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(destino.x, destino.z));

        if (distH < 2.5f)
        {
            MudarEstado(EstadoPetroleiro.INDO_PLATAFORMA);
        }
        else if (timerEstado > 45.0f)
        {
            Debug.LogWarning($"[Navio Petroleiro] Recuo do pier ainda distante ({distH:0.0} m); mantendo rota de ré.", this);
            timerEstado = 0f;
        }
    }

    void ExecutarViagemNavMesh(System.Action aoChegar)
    {
        if (agenteNav == null || !possuiDestinoNavMesh)
        {
            statusDebug = "Rota naval sem destino valido.";
            return;
        }

        // Petroleiro navega sobre água e não deve depender do NavMesh de
        // terra. A rota direta mantém a sequência plataforma -> píer sem
        // interferir nas patrulhas militares das corvetas.
        if (fallbackAquaticoAtivo)
        {
            ExecutarFallbackAquatico(aoChegar);
            return;
        }

        if (!agenteNav.enabled || !agenteNav.isOnNavMesh)
        {
            statusDebug = "Tentando reconectar NavMesh...";
            AtivarNavMeshNoLocal();
            if (agenteNav.enabled && agenteNav.isOnNavMesh)
            {
                fallbackAquaticoAtivo = false;
                agenteNav.SetDestination(destinoNavMeshAtual);
            }
            else
            {
                ExecutarFallbackAquatico(aoChegar);
            }
            return;
        }

        if (agenteNav.pathStatus == NavMeshPathStatus.PathInvalid
            || (!agenteNav.pathPending && !agenteNav.hasPath))
        {
            if (Time.time >= proximoReplanejamento)
            {
                proximoReplanejamento = Time.time + 1.5f;
                agenteNav.SetDestination(destinoNavMeshAtual);
            }
            statusDebug = "Replanejando rota naval...";

            // O NavMesh terrestre pode existir no mapa, mas não cobrir o
            // corredor de água até a plataforma/píer. Nesse caso, não deixe o
            // petroleiro congelado: usa o deslocamento aquático seguro abaixo.
            if (!agenteNav.pathPending && agenteNav.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                ExecutarFallbackAquatico(aoChegar);
            }
            return;
        }

        if(transform.rotation.x != 0 || transform.rotation.z != 0)
        {
             Vector3 euler = transform.rotation.eulerAngles;
             transform.rotation = Quaternion.Euler(0, euler.y, 0);
        }

        if (!agenteNav.pathPending
            && agenteNav.pathStatus == NavMeshPathStatus.PathComplete
            && !float.IsInfinity(agenteNav.remainingDistance)
            && agenteNav.remainingDistance <= agenteNav.stoppingDistance + 0.8f)
        {
            aoChegar.Invoke();
        }
    }

    void ExecutarFallbackAquatico(System.Action aoChegar)
    {
        if (agenteNav != null && agenteNav.enabled)
        {
            agenteNav.isStopped = true;
            agenteNav.enabled = false;
        }

        if (!fallbackAquaticoAtivo)
        {
            fallbackAquaticoAtivo = true;
        }
        if (!avisoFallbackAquaticoEmitido)
        {
            avisoFallbackAquaticoEmitido = true;
            Debug.Log("[Navio Petroleiro] Rota aquatica direta ativa para a logistica.", this);
        }

        Vector3 destino = destinoNavMeshAtual;
        destino.y = transform.position.y;
        Vector3 delta = destino - transform.position;
        delta.y = 0f;
        float distancia = delta.magnitude;
        if (distancia <= 3.5f)
        {
            fallbackAquaticoAtivo = false;
            avisoFallbackAquaticoEmitido = false;
            aoChegar.Invoke();
            return;
        }

        float velocidade = Mathf.Max(velocidadeManobra, 8f);
        transform.position = Vector3.MoveTowards(transform.position, destino, velocidade * Time.deltaTime);
        if (delta.sqrMagnitude > 0.01f)
        {
            Quaternion rotacao = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacao, 35f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
        }
        statusDebug = "Patrulha logistica aquatica: " + Mathf.RoundToInt(distancia) + " m restantes.";
    }

    void ExecutarAcoplagemManual(Transform alvo, EstadoPetroleiro proximo)
    {
        if (alvo == null) 
        {
            MudarEstado(proximo); 
            return; 
        }

        timerEstado += Time.deltaTime; 

        Vector3 destino = alvo.position;
        destino.y = transform.position.y;
        // A atracação acontece no plano da água. Não conte a altura do
        // marcador azul (que pode estar no deck da plataforma/pier), senão
        // o navio chega horizontalmente mas nunca conclui a etapa.
        float dist = Vector3.Distance(transform.position, destino);

        // Conclui a vaga azul antes do alinhamento fino da proa. Sem isso,
        // um casco a poucos metros podia ficar preso tentando girar.
        if (dist <= 3.5f)
        {
            transform.position = destino;
            transform.rotation = Quaternion.Euler(0, alvo.rotation.eulerAngles.y, 0);
            MudarEstado(proximo);
            return;
        }

        Vector3 dirInicial = destino - transform.position;
        dirInicial.y = 0f;
        if (dirInicial.sqrMagnitude > 4f)
        {
            Quaternion rotEntrada = Quaternion.LookRotation(dirInicial.normalized);
            float anguloEntrada = Quaternion.Angle(transform.rotation, rotEntrada);
            if (anguloEntrada > 10f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotEntrada, 60f * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
                Debug.DrawLine(transform.position, destino, Color.cyan);
                return;
            }
        }

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
        // Conclui somente ao chegar ao ponto azul. Nunca troca de alvo por
        // timeout distante, pois isso misturava pier/plataforma entre equipes.
        // O ponto azul marca a vaga, não o pivô exato do casco. Uma pequena
        // tolerância horizontal evita que colisores/boias deixem o navio
        // eternamente em aproximação quando já está dentro da vaga.
        if (timerEstado > 60.0f)
        {
            Debug.LogWarning($"[Navio Petroleiro] Atracagem ainda distante ({dist:0.0} m); mantendo aproximação ao ponto azul.", this);
            timerEstado = 0f;
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

        // A saída também precisa alcançar o ponto azul; não encerra a etapa
        // apenas porque passou tempo suficiente.
        if (distH < 2.5f)
        {
            MudarEstado(proximo);
        }
        else if (timerEstado > 45.0f)
        {
            Debug.LogWarning($"[Navio Petroleiro] Saída ainda distante ({distH:0.0} m); mantendo a rota.", this);
            timerEstado = 0f;
        }
    }

    void ExecutarOperacaoLogistica(bool carregando, EstadoPetroleiro proximo)
    {
        timerEstado += Time.deltaTime;
        
        int qtdPorFrame = Mathf.Max(1, Mathf.RoundToInt(taxaTransferencia * Time.deltaTime));
        
        // Define o tempo máximo de operação (usa a variável do inspector ou um mínimo de 15s se a do inspector for muito curta, para dar tempo de carregar)
        // Na verdade, vamos respeitar o Inspector E a capacidade.
        float tempoLimite = Mathf.Max(tempoDeCarregamento, 2.0f); // Pelo menos 2s
        
        if (carregando)
        {
            if (plataformaAlvo == null || !PertenceAoMesmoTime(plataformaAlvo))
            {
                statusDebug = "Plataforma perdida ou de outro time.";
                MudarEstado(EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA);
                return;
            }

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
            if (pierAlvo == null || !PertenceAoMesmoTime(pierAlvo))
            {
                statusDebug = "Pier perdido ou de outro time.";
                MudarEstado(EstadoPetroleiro.AGUARDANDO_INFRAESTRUTURA);
                return;
            }

            if (pierAlvo != null && petroleoCarregado > 0)
            {
                int aEntregar = Mathf.Min(qtdPorFrame, petroleoCarregado);
                pierAlvo.ReceberPetroleo(aEntregar);
                petroleoCarregado -= aEntregar;
            }
            statusDebug = $"Esvaziando: {petroleoCarregado}/{capacidadeMaxima} ({timerEstado:F1}s)";

            bool tanqueVazio = petroleoCarregado <= 0;
            // Nunca abandona o pier com petroleo ainda no porao.
            if (tanqueVazio)
            {
                MudarEstado(proximo);
            }
        }
    }

    // --- SISTEMAS DE NAVEGAÇÃO ---

    void ConfigurarNavMesh(Vector3 destino)
    {
        destinoNavMeshAtual = destino;
        possuiDestinoNavMesh = true;
        fallbackAquaticoAtivo = true;
        avisoFallbackAquaticoEmitido = false;
        proximoReplanejamento = 0f;
        if (agenteNav != null && agenteNav.enabled)
        {
            agenteNav.isStopped = true;
            agenteNav.enabled = false;
        }
    }

    void AtivarNavMeshNoLocal()
    {
        if (agenteNav == null) return;
        if (agenteNav.enabled && agenteNav.isOnNavMesh) return;

        NavMeshHit hit;
        // Procura chão navegável num raio de 10 metros
        if (NavMesh.SamplePosition(transform.position, out hit, 30.0f, NavMesh.AllAreas))
        {
            if (!agenteNav.enabled) agenteNav.enabled = true;
            agenteNav.Warp(hit.position);
        }
        else
        {
            Debug.LogError("[Navio] Não consegui achar NavMesh aqui! O navio vai travar.");
        }
    }

    void DesligarNavMesh()
    {
        if (agenteNav != null && agenteNav.enabled)
        {
            agenteNav.isStopped = true;
            agenteNav.enabled = false;
        }
        possuiDestinoNavMesh = false;
        fallbackAquaticoAtivo = false;
        avisoFallbackAquaticoEmitido = false;
    }

    // --- BUSCADORES ---

    void BuscarPlataforma()
    {
        plataformaAlvo = EncontrarMelhorPlataformaDoTime();
        if (plataformaAlvo == null) Debug.LogError("[Navio] Nenhuma PlataformaOffshore do time " + TeamIdAtual + " na cena!");
    }

    void BuscarPier()
    {
        pierAlvo = EncontrarMelhorPierDoTime();
        if (pierAlvo == null) Debug.LogError("[Navio] Nenhum PierMarinha do time " + TeamIdAtual + " na cena!");
    }

    private int TeamIdAtual
    {
        get
        {
            if (identidadeCache == null)
            {
                identidadeCache = GetComponent<IdentidadeUnidade>();
                if (identidadeCache == null)
                {
                    identidadeCache = GetComponentInParent<IdentidadeUnidade>();
                }
            }

            if (equipeOperacaoFixa > 0)
            {
                return equipeOperacaoFixa;
            }

            return identidadeCache != null && identidadeCache.teamID > 0
                ? identidadeCache.teamID
                : RecursosPorTime.ObterTeamId(this);
        }
    }

    private bool PertenceAoMesmoTime(Component componente)
    {
        return componente != null && RecursosPorTime.ObterTeamId(componente) == TeamIdAtual;
    }

    private bool SelecionarAlvosLogisticos(bool reservar)
    {
        PlataformaOffshore plataforma = EncontrarMelhorPlataformaDoTime();
        PierMarinha pier = EncontrarMelhorPierDoTime();
        if (plataforma == null || pier == null)
        {
            plataformaAlvo = plataforma;
            pierAlvo = pier;
            return false;
        }

        if (reservar)
        {
            if (!plataforma.TentarReservar(this, 90f))
            {
                return false;
            }

            if (!pier.TentarReservarLogistica(this, 90f))
            {
                plataforma.LiberarReserva(this);
                return false;
            }
        }

        plataformaAlvo = plataforma;
        pierAlvo = pier;
        return true;
    }

    private PlataformaOffshore EncontrarMelhorPlataformaDoTime()
    {
        PlataformaOffshore[] plataformas = Object.FindObjectsByType<PlataformaOffshore>(FindObjectsSortMode.None);
        PlataformaOffshore melhor = null;
        float melhorScore = float.MaxValue;
        int teamId = TeamIdAtual;

        for (int i = 0; i < plataformas.Length; i++)
        {
            PlataformaOffshore plataforma = plataformas[i];
            if (plataforma == null || !plataforma.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RecursosPorTime.ObterTeamId(plataforma) != teamId)
            {
                continue;
            }

            if (plataforma.pontoChegada == null || plataforma.pontoAbastecer == null || plataforma.pontoSaida == null)
            {
                DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("petroleiro_plataforma_invalida", plataforma.name);
                continue;
            }

            if (!plataforma.PodeReceberPetroleiro(this))
            {
                continue;
            }

            float distancia = Vector3.Distance(transform.position, plataforma.transform.position);
            float bonusEstoque = Mathf.Clamp(plataforma.petroleoArmazenado / 1000f, 0f, 40f);
            float score = distancia - bonusEstoque;
            if (score < melhorScore)
            {
                melhorScore = score;
                melhor = plataforma;
            }
        }

        return melhor;
    }

    private PierMarinha EncontrarMelhorPierDoTime()
    {
        PierMarinha[] piers = Object.FindObjectsByType<PierMarinha>(FindObjectsSortMode.None);
        PierMarinha melhor = null;
        float melhorScore = float.MaxValue;
        int teamId = TeamIdAtual;

        for (int i = 0; i < piers.Length; i++)
        {
            PierMarinha pier = piers[i];
            if (pier == null || !pier.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RecursosPorTime.ObterTeamId(pier) != teamId)
            {
                continue;
            }

            if (pier.saida_petro == null || pier.Atraca_petro == null)
            {
                DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("petroleiro_pier_invalido", pier.name);
                continue;
            }

            if (!pier.PodeReceberLogistica(this))
            {
                continue;
            }

            float score = Vector3.Distance(transform.position, pier.transform.position);
            if (score < melhorScore)
            {
                melhorScore = score;
                melhor = pier;
            }
        }

        return melhor;
    }

    private void LiberarReservas()
    {
        if (plataformaAlvo != null)
        {
            plataformaAlvo.Liberar(this);
            plataformaAlvo.LiberarReserva(this);
        }

        if (pierAlvo != null)
        {
            pierAlvo.LiberarLogistica(this);
            pierAlvo.LiberarReservaLogistica(this);
        }
    }

    protected override void OnDisable()
    {
        LiberarReservas();
        base.OnDisable();
    }
}
