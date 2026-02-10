using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ControleNavioRealista : MonoBehaviour
{
    [Header("Configurações Físicas (Hardcore)")]
    [Tooltip("Tempo em segundos para ir de 0% a 100% de potência.")]
    public float tempoAceleracao = 8.0f; // Demorado, como pedido
    [Tooltip("Tempo em segundos para perder velocidade natural (sem motor). Coasting.")]
    public float tempoDesaceleracao = 15.0f; 
    [Tooltip("Tempo para parar usando 'Reversão Total'.")]
    public float tempoParadaEmergencia = 10.0f;
    [Tooltip("Velocidade máxima em nós (aproximado em unidades Unity).")]
    public float velocidadeMaxima = 12.0f;
    [Tooltip("Velocidade de resposta do leme (graus por segundo).")]
    public float velocidadeLeme = 5.0f; // Leme lento
    [Tooltip("Angulo máximo de curva por segundo em velocidade máxima.")]
    public float curvaMaximaGraus = 10.0f; // Raio largo
    
    [Header("Hidrodinâmica Visual")]
    [Tooltip("Inclinação lateral (Roll) ao virar. Positivo = tomba para fora (realista).")]
    public float coeficienteAderna = 2.5f; 
    [Tooltip("Altura das ondas para o balanço (Heave).")]
    public float alturaOnda = 0.5f;
    [Tooltip("Frequência do balanço passivo.")]
    public float frequenciaOnda = 0.2f;
    [Tooltip("Arrasto natural da água (fricção). Aumente se o navio desliza demais.")]
    public float arrastoPassivo = 0.5f;
    [Tooltip("Distância para considerar que chegou.")]
    public float distanciaChegada = 15.0f;

    [Header("Referências Visuais")]
    public ParticleSystem bigodeiraProa; // Espuma na frente
    public TrailRenderer rastroEsteira;  // Rastro longo
    public ParticleSystem turbulenciaPopa; // Cavitação atrás
    public Transform modelo3D; // O casco visual para rotacionar

    // Estado Interno (Simulação)
    private NavMeshAgent agente;
    private Vector3 velocidadeVetorial = Vector3.zero; // Vector de inércia real
    private float potenciaAlvo = 0f; // -1 a 1 (Input)
    private float potenciaAtual = 0f; // -1 a 1 (RPM do eixo)
    private float anguloLemeAtual = 0f; // -1 a 1 (Posição do Leme)
    private Vector3 destinoAtual;
    private bool temDestino = false;
    
    // Variáveis auxiliares visual
    private float tempoVibracao = 0f;
    private float offsetOnda;
    private Quaternion rotacaoInicialModelo;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        agente.updatePosition = false; // Nós controlamos a posição (Soft-Sim)
        agente.updateRotation = false; // Nós controlamos a rotação (Physics)
        agente.acceleration = 9999; // Agente "NavMesh" instantâneo, nós seguimos ele
        
        offsetOnda = Random.Range(0f, 100f);

        if (modelo3D == null && transform.childCount > 0)
            modelo3D = transform.GetChild(0);

        if (modelo3D != null)
            rotacaoInicialModelo = modelo3D.localRotation;

        // Garante que os efeitos começam desligados
        if(bigodeiraProa) bigodeiraProa.Stop();
        if(turbulenciaPopa) turbulenciaPopa.Stop();
    }

    void Update()
    {
        // 1. INPUT (IA ou Player via NavMesh)
        CalcularInputNavegacao();

        // 2. SIMULAÇÃO DE MOTOR E HELICE
        SimularMotor();

        // 3. DIN MICA DE MOVIMENTO (Inércia e Drift)
        SimularFisicaMovimento();

        // 4. VISUAIS
        AtualizarEfeitosVisuais();
    }

    void CalcularInputNavegacao()
    {
        // Se não tem caminho E já estamos parados, sai.
        if (!agente.hasPath && velocidadeVetorial.magnitude < 0.1f)
        {
            potenciaAlvo = 0f;
            temDestino = false;
            return;
        }

        float distancia = agente.remainingDistance;
        
        // --- LOGICA DE CHEGADA INTELIGENTE ---
        // Se estiver muito perto ou sem caminho, corta motor.
        // Aumentamos a tolerância para 15m (distanciaChegada) para evitar o loop de correcao infinita
        if (distancia < distanciaChegada || !agente.hasPath)
        {
            potenciaAlvo = 0f;
            temDestino = false;
            
            // Se ainda estiver muito rápido perto do destino, aplica reverso?
            // Por enquanto, apenas corta motor e deixa o "Parking Drag" atuar.
            return;
        }

        temDestino = true;
        
        // Direção desejada (Steering Target é o próximo corner do NavMesh)
        Vector3 direcaoAlvo = (agente.steeringTarget - transform.position).normalized;
        direcaoAlvo.y = 0;

        // Calculo do Leme (Dot Product para ver se está alinhado)
        float angulo = Vector3.SignedAngle(transform.forward, direcaoAlvo, Vector3.up);
        float inputLeme = Mathf.Clamp(angulo / 45.0f, -1f, 1f); // 45 graus para leme total

        // Leme tem inércia
        anguloLemeAtual = Mathf.MoveTowards(anguloLemeAtual, inputLeme, Time.deltaTime * (velocidadeLeme / 45.0f));

        // Controle de Potência (Throttle)
        // 1. Curva Fechada: Reduz motor
        if (Mathf.Abs(angulo) > 90f)
        {
            potenciaAlvo = 0.2f; 
        }
        else
        {
            // 2. Aproximação Suave
            // Se estiver chegando (< 50m), começa a reduzir proporcionalmente
            if (distancia < 50.0f)
            {
                potenciaAlvo = Mathf.Clamp01(distancia / 50.0f);
                if (potenciaAlvo < 0.1f) potenciaAlvo = 0f; // Corta final
            }
            else
            {
                potenciaAlvo = 1.0f; // Flank speed
            }
        }
    }

    void SimularMotor()
    {
        // Aceleração Logarítmica / Inércia
        // Se acelerando:
        float taxa = 0f;
        if (Mathf.Abs(potenciaAlvo) > Mathf.Abs(potenciaAtual))
            taxa = 1.0f / tempoAceleracao; // Acelerando
        else if (Mathf.Abs(potenciaAlvo) < Mathf.Abs(potenciaAtual) && potenciaAlvo != 0)
            taxa = 1.0f / tempoParadaEmergencia; // Freando/Revertendo ativo
        else
        {
            // Proteção contra divisão por zero se o usuário botar 0 no inspector
            if (tempoDesaceleracao <= 0.01f)
                taxa = 1000f; // Para "instantaneamente" (motor, não o navio)
            else
                taxa = 1.0f / tempoDesaceleracao; // Coasting
        }

        potenciaAtual = Mathf.MoveTowards(potenciaAtual, potenciaAlvo, Time.deltaTime * taxa);

        // Crash Stop Vibration (se mudou bruscamente de sentido)
        if (Mathf.Sign(potenciaAlvo) != Mathf.Sign(potenciaAtual) && Mathf.Abs(potenciaAlvo) > 0.5f)
        {
            tempoVibracao = 0.5f; // Vibra por um tempo
        }
        else
        {
            tempoVibracao -= Time.deltaTime;
        }
    }

    void SimularFisicaMovimento()
    {
        // 1. Rotação (Giro)
        // O navio gira baseado na velocidade da água passando pelo leme + propulsão
        // Se estiver parado, leme não funciona direito (a menos que tenha thruster, ignorado aqui para realismo clássico)
        float fluxoAgua = Mathf.Abs(velocidadeVetorial.magnitude); 
        float eficienciaLeme = Mathf.Clamp01(fluxoAgua / 2.0f); // Precisa de 2m/s para leme full
        
        float giro = anguloLemeAtual * curvaMaximaGraus * eficienciaLeme * Time.deltaTime;
        
        // Se estiver de ré, o leme inverte a lógica física visualmente, mas o NavMesh espera direção certa
        // Simplificação: Giro sempre aponta pro destino.
        transform.Rotate(0, giro, 0);

        // 2. Translação (Forces)
        // Força avante (Propulsão)
        Vector3 forcaMotor = transform.forward * potenciaAtual * (velocidadeMaxima / tempoAceleracao); 
        // Na verdade, a força deve equilibrar o arrasto na velocidade máxima.
        // Simplificando: Velocidade alvo é potencia * Max. MoveTowards simula força.
        
        // Vamos usar uma abordagem de Vetores de Velocidade para Drift (Sideslip)
        
        // Componente "Keel" (Quilha): Navio gosta de andar pra frente, não de lado.
        // Convertemos velocidade global para local
        Vector3 velLocal = transform.InverseTransformDirection(velocidadeVetorial);
        
        // Arrasto Longitudinal (Frente/Trás) - Baixo
        float dragLong = 0.5f; 
        // Arrasto Lateral (Lados) - Altíssimo (Quilha)
        float dragLat = 3.0f; 

        // Aplica input do motor na velocidade local Z
        float forcaZ = potenciaAtual * velocidadeMaxima * 2.0f; // Fator de força empírico
        
        // Integração Euler Simples para velocidade (não é física rigidbody real, é soft-sim)
        // É mais fácil controlar a velocidade "desejada" na direção frontal e permitir o slip na lateral.
        
        // Nova Abordagem Drift:
        // O vetor velocidade tenta alinhar com transform.forward, mas demora (drift).
        Vector3 velocidadeDesejada = transform.forward * (potenciaAtual * velocidadeMaxima);
        
        // Lerp vectoral diferente para eixos (Simulando drift)
        // O navio muda de direção de movimento LENTAMENTE
        
        if (velocidadeVetorial.magnitude < 0.1f && Mathf.Abs(potenciaAtual) < 0.01f)
        {
            velocidadeVetorial = Vector3.zero;
        }
        else
        {
            // Acelera na direção que o nariz aponta
            velocidadeVetorial += transform.forward * (potenciaAtual * velocidadeMaxima * Time.deltaTime / tempoAceleracao);
            
            // Drag (Resistência da Água)
            // Se chegou no destino (temDestino == false), aumenta o Drag (Freio de Mão Hidrodinamico)
            float dragAtual = arrastoPassivo;
            if (!temDestino) dragAtual *= 4.0f; // 4x mais arrasto para "estacionar"
            
            velocidadeVetorial -= velocidadeVetorial * (Time.deltaTime * dragAtual); 
            
            // "Kill Lateral Velocity" (A quilha matando o drift aos poucos)
            // Projeta velocidade na direita
            Vector3 velLateral = Vector3.Project(velocidadeVetorial, transform.right);
            velocidadeVetorial -= velLateral * (Time.deltaTime * 1.0f); // Corrige o drift lentamente (1.0f factor)
            
            // Limite
            velocidadeVetorial = Vector3.ClampMagnitude(velocidadeVetorial, velocidadeMaxima);
        }

        // Aplica posição
        // Atualiza a posição do NavMeshAgent para não perder sync
        agentNextPositionCheck(transform.position + velocidadeVetorial * Time.deltaTime);
    }
    
    void agentNextPositionCheck(Vector3 novaPos)
    {
        transform.position = novaPos;
        agente.nextPosition = transform.position; // Mantem o navmesh colado no visual
    }

    void AtualizarEfeitosVisuais()
    {
        if (modelo3D == null) return;

        float velocidadeReal = velocidadeVetorial.magnitude;
        float ratioVelocidade = velocidadeReal / velocidadeMaxima;

        // 1. ROLL (Adernamento)
        // Curva pra esquerda (Leme < 0) -> Joga corpo pra direita (Roll > 0 ??? Não, Roll < 0 é direita em Unity Z?)
        // Vamos verificar: Z+ é frente. Rotação Z é Roll. Sentido anti-horário.
        // +Z rot = Tomba pra Esquerda. -Z rot = Tomba pra Direita.
        // Queremos: Curva Esquerda (Leme -) -> Tomba Direita (Roll -)
        // Logo: Sinais iguais.
        float rollAlvo = anguloLemeAtual * coeficienteAderna * ratioVelocidade * 5.0f; // *5 para escala visual
        
        // Adiciona balanço do mar (Passive Roll)
        float balancoMar = Mathf.Sin(Time.time * frequenciaOnda + offsetOnda) * 2.0f; // +/- 2 graus sempre
        
        // PITCH (Arfagem - Nariz sobe com velocidade)
        float pitchAlvo = -ratioVelocidade * 2.0f; // Nariz levanta levemente (ou desce dependendo do design, aqui levanta)
        // Com ondas
        float pitchOnda = Mathf.Cos(Time.time * (frequenciaOnda * 1.5f) + offsetOnda) * alturaOnda;
        
        // Aplica Rotação Suave
        Quaternion simulacaoOffset = Quaternion.Euler(pitchAlvo + pitchOnda, 0, rollAlvo + balancoMar);
        // Aplica o offset SOBRE a rotação inicial (Initial * Offset) para respeitar se o modelo veio virado do Blender (-90 no X)
        Quaternion rotacaoFinal = rotacaoInicialModelo * simulacaoOffset;
        
        modelo3D.localRotation = Quaternion.Slerp(modelo3D.localRotation, rotacaoFinal, Time.deltaTime * 1.0f); // Lento e majestoso

        // 2. VIBRAÇÃO (Crash Stop)
        if (tempoVibracao > 0)
        {
            modelo3D.localPosition = Random.insideUnitSphere * 0.05f; // Shake leve
        }
        else
        {
            modelo3D.localPosition = Vector3.zero;
        }

        // 3. PARTICULAS
        // Rastro (Wake)
        if (rastroEsteira)
        {
            rastroEsteira.emitting = velocidadeReal > 2.0f; // Só emite se movendo bem
        }

        // Bigodeira (Bow Wave)
        if (bigodeiraProa)
        {
            var emission = bigodeiraProa.emission;
            if (velocidadeReal > 1.0f && potenciaAtual > 0)
            {
                emission.rateOverTime = ratioVelocidade * 50f; // Mais rápido = mais espuma
                if (!bigodeiraProa.isPlaying) bigodeiraProa.Play();
            }
            else
            {
                emission.rateOverTime = 0f;
                bigodeiraProa.Stop();
            }
        }

        // Turbulencia (Propeller)
        if (turbulenciaPopa)
        {
            var emission = turbulenciaPopa.emission;
            // Se acelerando forte, muita espuma
            bool acelerandoForte = (potenciaAtual > potenciaAlvo - 0.1f) && ratioVelocidade < 0.5f; 
            
            if (potenciaAtual != 0)
            {
                 emission.rateOverTime = (Mathf.Abs(potenciaAtual) * 30f) + (acelerandoForte ? 50f : 0f); // Cavitação extra na arrancada
                 if (!turbulenciaPopa.isPlaying) turbulenciaPopa.Play();
            }
            else
            {
                 emission.rateOverTime = 0f;
                 turbulenciaPopa.Stop();
            }
        }
    }
    
    // Método para integração com ControleUnidade
    public void DefinirDestino(Vector3 destino)
    {
        if(agente.isActiveAndEnabled)
            agente.SetDestination(destino);
    }
    
    // Gizmos para ver o vetor de movimento vs frente
    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 5f);
        
        Gizmos.color = Color.yellow; // Vector Drift
        Gizmos.DrawLine(transform.position, transform.position + velocidadeVetorial);
        
        if (agente != null && agente.hasPath)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, agente.steeringTarget);
        }
    }
}
