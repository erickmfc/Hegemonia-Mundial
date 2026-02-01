using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MovimentoRealTerrestre : MonoBehaviour
{
    [Header("Configuração do Veículo")]
    public float velocidadeMaxima = 8.0f;
    public float aceleracao = 5.0f;
    public float desaceleracao = 10.0f;
    
    [Tooltip("Capacidade de curva (Graus por segundo na velocidade máxima)")]
    public float potenciaCurva = 60.0f; 
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
    
    // Controle de rotação das rodas (para não bugar o Euler)
    private float giroAcomuladoRodas = 0f; 

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
        if (agente == null) return;

        // 1. Sincronia Agente <> Veículo
        // Mantém o agente preso ao carro para calcular caminhos a partir da posição real
        agente.nextPosition = transform.position;

        // 2. Cálculo de Destino
        Vector3 proximoPonto = agente.steeringTarget;
        Vector3 vetorDirecao = (proximoPonto - transform.position);
        vetorDirecao.y = 0; // Ignora altura (terreno plano)

        float distanciaAteAlvo = vetorDirecao.magnitude;
        
        // Verifica se realmente tem que andar
        bool temCaminho = agente.hasPath && distanciaAteAlvo > distanciaParada;
        
        // Correção de bug do NavMesh (às vezes ele acha que tem caminho mas já está lá)
        // PROTEÇÃO: Só verifica remainingDistance se estiver no NavMesh e Ativo
        if (agente.isOnNavMesh && agente.isActiveAndEnabled)
        {
            if (agente.remainingDistance <= distanciaParada && !agente.pathPending)
            {
                temCaminho = false; 
            }
        }
        else
        {
            // Se perdeu o NavMesh, aborta movimento para evitar erros
            temCaminho = false;
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
            float fatorGiro = Mathf.Clamp(velocidadeAtual / velocidadeMaxima, 0.35f, 1.0f);
            
            // Em ângulos extremos e baixa velocidade, aumentamos a potência para evitar o "loop da morte" (Rodinha)
            if (fatorCurva > 0.8f && velocidadeAtual < velocidadeMaxima * 0.5f)
            {
                fatorGiro = 1.0f; // Força giro máximo se estiver lento e precisando virar muito
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
}
