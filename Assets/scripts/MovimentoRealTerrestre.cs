using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MovimentoRealTerrestre : MonoBehaviour
{
    [Header("Configuração do Veículo")]
    public float velocidadeMaxima = 12.0f; // Aumentado
    public float aceleracao = 15.0f;       // Aumentado (Start rápido)
    public float desaceleracao = 30.0f;    // Aumentado (Freio rápido)
    
    [Tooltip("Capacidade de curva (Graus por segundo na velocidade máxima)")]
    public float potenciaCurva = 180.0f;   // Aumentado drasticamente (Giro rápido)
    public float distanciaParada = 1.0f;

    [Header("Configuração das Rodas")]
    public Transform rodaFrenteEsq;
    public Transform rodaFrenteDir;
    
    [Tooltip("Pode ser uma roda única ou eixo traseiro se o caminhão tiver peça única")]
    public Transform rodaTrasEsq; 
    public Transform rodaTrasDir;
    
    [Tooltip("Ângulo máximo que as rodas da frente viram")]
    public float anguloMaximoVolante = 35f;

    // Estado Interno
    private NavMeshAgent agente;
    private float velocidadeAtual = 0f;
    private float anguloVolanteAtual = 0f;
    private readonly EstadoOtimizacaoTatica estadoOtimizacao = new EstadoOtimizacaoTatica();
    
    // Controle de rotação das rodas (para não bugar o Euler)
    private float giroAcomuladoRodas = 0f; 
    private Vector3 steeringTargetCache;
    private float remainingDistanceCache;
    private bool hasPathCache;
    private bool pathPendingCache;
    private bool agenteLeituraValidaCache;
    private readonly Collider[] bufferColisores = new Collider[8];

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        
        // Desacopla o Agente: Nós controlamos a física
        agente.updateRotation = false;
        agente.updatePosition = false;
        
        // Configurações do Agente
        agente.speed = velocidadeMaxima;
        agente.acceleration = aceleracao * 2; 
        agente.angularSpeed = 0; // Importante: Desliga giro do agente

        // Tenta achar rodas se estiverem vazias
        if (rodaFrenteEsq == null) TentarAcharRodas();
    }

    void Update()
    {
        long inicioUpdate = InfraPerformanceGameplay.MarcarInicioMedicao();
        if (agente == null) return;
        AtualizarEstadoOtimizacao();
        AtualizarCacheNavMeshSeNecessario();

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            if (agente.enabled && agente.isOnNavMesh)
            {
                agente.ResetPath();
                agente.isStopped = true;
            }

            velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, 0f, desaceleracao * Time.deltaTime);
            AnimarRodas(0f);
            return;
        }

        // 1. Sincronia Agente <> Veículo
        // Mantém o agente preso ao carro para calcular caminhos a partir da posição real
        agente.nextPosition = transform.position;

        // 2. Cálculo de Destino com preferência de faixa (mão de ida e volta) se estiver na rua
        Vector3 proximoPonto = (agente.enabled && agente.isOnNavMesh) ? agente.steeringTarget : transform.position;
        
        RuaConectora ruaProxima = EncontrarRuaProxima(transform.position, 8f);
        if (ruaProxima != null)
        {
            Vector3 dirRua = ruaProxima.transform.forward;
            Vector3 dirMovimento = (proximoPonto - transform.position).normalized;
            float dot = Vector3.Dot(dirMovimento, dirRua);
            
            // Deslocamento da faixa (afasta-se 25% da largura total do asfalto)
            float shiftDistance = ruaProxima.largura * 0.25f;
            
            if (dot >= 0f)
            {
                // Sentido favorável da rua: mantém-se na faixa da direita
                proximoPonto += ruaProxima.transform.right * shiftDistance;
            }
            else
            {
                // Sentido contrário: mantém-se na faixa da esquerda (direita invertida)
                proximoPonto -= ruaProxima.transform.right * shiftDistance;
            }
        }

        Vector3 vetorDirecao = (proximoPonto - transform.position);
        vetorDirecao.y = 0; // Ignora altura (terreno plano)

        float distanciaAteAlvo = vetorDirecao.magnitude;
        
        // Verifica se realmente tem que andar usando o estado em tempo real do agente
        bool temCaminho = false;
        if (agente.enabled && agente.isOnNavMesh)
        {
            if (!agente.isStopped && (agente.hasPath || agente.pathPending))
            {
                if (agente.pathPending || agente.remainingDistance > distanciaParada)
                {
                    temCaminho = true;
                }
                else if (agente.hasPath)
                {
                    // Chegou ao destino: limpa o caminho para evitar overshoot / oscilação
                    agente.ResetPath();
                }
            }
        }

        // --- LÓGICA DE MOVIMENTO FÍSICO ---

        if (temCaminho)
        {
            Quaternion rotacaoAlvo = Quaternion.LookRotation(vetorDirecao);
            
            // Calcula o ângulo relativo do alvo (Ex: o alvo está 30 graus à direita)
            float anguloParaAlvo = Vector3.SignedAngle(transform.forward, vetorDirecao, Vector3.up);

            // --- LÓGICA DE FREIO EM CURVA ---
            // Se o ângulo for agudo (> 10 graus), reduz a velocidade alvo para fazer a curva mais fechada
            float fatorCurva = Mathf.Clamp01(Mathf.Abs(anguloParaAlvo) / 45.0f); // 0 = Reto, 1 = Curva Fechada (>45)
            float velocidadeAlvo = Mathf.Lerp(velocidadeMaxima, velocidadeMaxima * 0.2f, fatorCurva);

            // A. Acelera / Freia para atingir a velocidade ideal da curva
            velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeAlvo, aceleracao * Time.deltaTime);

            // B. Gira o chassi
            // TRUQUE: Garante que mesmo lento, o carro consiga girar (Simulação de Pivot/Skid-Steer)
            // Se estivermos muito lentos, fingimos que estamos mais rápidos para o cálculo de rotação,
            // ou simplesmente impomos um giro mínimo.
            
            // CORREÇÃO: Fator mínimo de 0.8f para garantir giro rápido mesmo parado
            float fatorGiro = Mathf.Clamp(velocidadeAtual / velocidadeMaxima, 0.8f, 1.2f);
            
            // Em ângulos extremos e baixa velocidade, aumentamos a potência para evitar o "loop da morte" (Rodinha)
            if (Mathf.Abs(anguloParaAlvo) > 45f && velocidadeAtual < velocidadeMaxima * 0.5f)
            {
                fatorGiro = 2.0f; // Força giro x2 se estiver lento e precisando virar muito (Pivot Turn)
            }

            float passoGiro = (potenciaCurva * fatorGiro) * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, passoGiro);

            // Visual: O VOLANTE vira parado (isso é normal), mas o chassi não.
            float anguloDesejadoVolante = Mathf.Clamp(anguloParaAlvo, -anguloMaximoVolante, anguloMaximoVolante);
            anguloVolanteAtual = Mathf.Lerp(anguloVolanteAtual, anguloDesejadoVolante, Time.deltaTime * 5f);
        }
        else
        {
            // Parar o carro
            velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, 0, desaceleracao * Time.deltaTime);
            
            // Volta o volante para o centro
            anguloVolanteAtual = Mathf.Lerp(anguloVolanteAtual, 0, Time.deltaTime * 5f);
        }

        // Aplica o movimento para frente (Sempre na direção que o chassi está olhando)
        transform.position += transform.forward * velocidadeAtual * Time.deltaTime;

        // --- ANIMAÇÃO DAS RODAS ---
        AnimarRodas(velocidadeAtual);
        InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Terra, inicioUpdate);
    }

    private void AtualizarEstadoOtimizacao()
    {
        ControleUnidade controle = GetComponent<ControleUnidade>();
        bool selecionado = controle != null && controle.selecionado;
        bool engajado = agente != null && agente.enabled && (agente.hasPath || agente.velocity.sqrMagnitude > 0.1f);
        InfraPerformanceGameplay.AtualizarEstadoBase(estadoOtimizacao, transform, selecionado, engajado, false, 140f, 320f);
    }

    private void AtualizarCacheNavMeshSeNecessario()
    {
        float intervalo = InfraPerformanceGameplay.ResolverIntervalo(0.12f, estadoOtimizacao, true, true);
        if (!InfraPerformanceGameplay.DeveExecutar(this, ref estadoOtimizacao.proximoTickPath, intervalo) && agenteLeituraValidaCache)
        {
            return;
        }

        long inicioPath = InfraPerformanceGameplay.MarcarInicioMedicao();
        agenteLeituraValidaCache = agente != null && agente.enabled && agente.isActiveAndEnabled && agente.isOnNavMesh;
        if (agenteLeituraValidaCache)
        {
            steeringTargetCache = agente.steeringTarget;
            remainingDistanceCache = agente.remainingDistance;
            hasPathCache = agente.hasPath;
            pathPendingCache = agente.pathPending;
        }
        else
        {
            steeringTargetCache = transform.position;
            remainingDistanceCache = 0f;
            hasPathCache = false;
            pathPendingCache = false;
        }

        InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Pathfinding, inicioPath);
    }

    void AnimarRodas(float velocidade)
    {
        // 1. Calcula o quanto as rodas giraram neste frame (Rolagem)
        // Multiplicador arbitrário (150f) ajustável pelo tamanho do pneu
        float passoGiro = velocidade * 150f * Time.deltaTime; 
        giroAcomuladoRodas += passoGiro;
        
        // Garante loop seguro de 360 graus para não estourar float
        giroAcomuladoRodas %= 360f;

        // 2. Aplica Rotação (X = Rolagem, Y = Direção)
        
        // Frente (Gira + Vira) - Vira baseado no anguloVolanteAtual
        AplicarRotacao(rodaFrenteEsq, giroAcomuladoRodas, anguloVolanteAtual);
        AplicarRotacao(rodaFrenteDir, giroAcomuladoRodas, anguloVolanteAtual);

        // Trás (Só Gira)
        AplicarRotacao(rodaTrasEsq, giroAcomuladoRodas, 0);
        if (rodaTrasDir != rodaTrasEsq) AplicarRotacao(rodaTrasDir, giroAcomuladoRodas, 0);
    }

    void AplicarRotacao(Transform roda, float rolagemX, float direcaoY)
    {
        if (roda != null)
        {
            // Cria a rotação combinada:
            // - Eixo X: Rolagem (andar para frente)
            // - Eixo Y: Direção (curvar)
            // - Eixo Z: 0 (sem camber/inclinação lateral)
            roda.localRotation = Quaternion.Euler(rolagemX, direcaoY, 0);
        }
    }

    // Auto-detectar rodas (Auxiliar)
    public void TentarAcharRodas()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            string nome = child.name.ToLower();
            if (!nome.Contains("wheel") && !nome.Contains("roda") && !nome.Contains("pneu")) continue;
            
            Vector3 posLocal = transform.InverseTransformPoint(child.position);
            
            if (posLocal.z > 0) // Frente
            {
                if (posLocal.x < 0 && rodaFrenteEsq == null) rodaFrenteEsq = child;
                else if (posLocal.x > 0 && rodaFrenteDir == null) rodaFrenteDir = child;
            }
            else // Trás
            {
                // Se estiver no meio (X ~ 0), pode ser eixo único
                if (Mathf.Abs(posLocal.x) < 0.1f)
                {
                    if (rodaTrasEsq == null) rodaTrasEsq = child; 
                }
                else
                {
                    if (posLocal.x < 0 && rodaTrasEsq == null) rodaTrasEsq = child;
                    else if (posLocal.x > 0 && rodaTrasDir == null) rodaTrasDir = child;
                }
            }
        }
    }

    private RuaConectora EncontrarRuaProxima(Vector3 posicao, float raioBusca)
    {
        int totalCols = Physics.OverlapSphereNonAlloc(posicao, raioBusca, bufferColisores, ~0, QueryTriggerInteraction.Ignore);
        RuaConectora melhorRua = null;
        float menorDist = float.MaxValue;
        for (int i = 0; i < totalCols; i++)
        {
            Collider col = bufferColisores[i];
            if (col == null) continue;
            RuaConectora rua = col.GetComponentInParent<RuaConectora>();
            if (rua != null)
            {
                float dist = Vector3.Distance(posicao, rua.transform.position);
                if (dist < menorDist)
                {
                    menorDist = dist;
                    melhorRua = rua;
                }
            }
        }
        return melhorRua;
    }
}
