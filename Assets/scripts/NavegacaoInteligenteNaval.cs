using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))] // ⚠️ OBRIGATÓRIO: Garante que o navio tem física!
public class NavegacaoInteligenteNaval : MonoBehaviour
{
    [Header("Motores e Física (Rigidbody)")]
    [Tooltip("A força que o motor faz para empurrar o navio para a frente.")]
    public float forcaMotor = 1000f;
    [Tooltip("O tempo (em segundos) que o motor leva para chegar na sua potência máxima (Aceleração gradual).")]
    public float tempoAceleracaoMotor = 8f;
    [Tooltip("A força do leme para rodar a traseira do navio.")]
    public float forcaLeme = 800f;
    [Tooltip("Limite máximo de velocidade na água.")]
    public float velocidadeMaxima = 15f;
    
    [Header("Dinâmica da Água (O Segredo do Drift)")]
    [Tooltip("Resistência da água contra a frente do navio (inércia/freio).")]
    public float arrastoFrontal = 1.5f;
    [Tooltip("Quanto o navio resiste a andar de lado. Valor baixo = derrapa mais (drift). Valor alto = anda nos trilhos.")]
    public float aderenciaAguaLateral = 8f; 
    
    [Header("Marcha à Ré")]
    public float distanciaMaximaRe = 200f;
    public float multiplicadorForcaRe = 0.6f;

    [Header("Efeitos Visuais")]
    public TrailRenderer rastroAgua;
    public Transform modelo3D;
    public float forcaInclinacao = 15f;

    private NavMeshAgent agente;
    private Rigidbody rb;
    private bool emMarchaRe = false;
    private bool modoAncorado = false;
    private float potenciaMotorAtual = 0f; // Vai de 0 a 1 para aceleração gradual

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // 1. Configurar o GPS (NavMeshAgent) para NÃO interferir na física
        agente.updatePosition = false;
        agente.updateRotation = false;
        agente.autoBraking = false;

        // 2. Configurar o Corpo Físico (Rigidbody) automaticamente
        rb.useGravity = false; // Desligamos a gravidade para ele não afundar no nada (assumindo que flutua)
        rb.linearDamping = arrastoFrontal; // Resistência da água
        rb.angularDamping = 2f; // Resistência ao giro (para não girar para sempre)
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Movimento suave para a câmara
        
        // Tranca a rotação para o navio não virar de cabeça para baixo (Capsize)
        // e tranca o Y para não voar nem afundar
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
        
        if (rastroAgua == null) 
            rastroAgua = GetComponentInChildren<TrailRenderer>();
    }

    void Update()
    {
        // COMANDOS DE JOGADOR DEVEM FICAR NO UPDATE
        if (Input.GetKeyDown(KeyCode.I))
        {
            var controle = GetComponent<ControleUnidade>();
            if (controle != null && controle.selecionado)
            {
                modoAncorado = !modoAncorado;
                if (modoAncorado) agente.ResetPath();
                Debug.Log(modoAncorado ? "[Navio] Âncora lançada!" : "[Navio] Âncora recolhida!");
            }
        }

        AtualizarVisuais();
    }

    // ⚠️ ATENÇÃO: Toda a física tem de ser calculada no FixedUpdate, não no Update!
    void FixedUpdate()
    {
        if (modoAncorado)
        {
            FrearFisicamente();
            return;
        }

        if (agente.hasPath && agente.remainingDistance > 5f)
        {
            AplicarFisicaNaval();
        }
        else
        {
            FrearFisicamente();
        }

        // Sincroniza o GPS invisível com o corpo físico de metal
        agente.nextPosition = transform.position;
    }

    private void AplicarFisicaNaval()
    {
        Vector3 alvoGPS = agente.steeringTarget;
        Vector3 direcaoParaAlvo = (alvoGPS - transform.position).normalized;
        direcaoParaAlvo.y = 0;

        // Calcula a velocidade de avanço real (se está a ir para a frente ou para trás)
        float velocidadeAvanco = transform.InverseTransformDirection(rb.linearVelocity).z;

        // DECIDIR SE VAI DE FRENTE OU DE RÉ
        if (Mathf.Abs(velocidadeAvanco) < 2f) // Só decide mudar de marcha se estiver devagar
        {
            float anguloParaAlvo = Vector3.Angle(transform.forward, direcaoParaAlvo);
            emMarchaRe = (anguloParaAlvo > 130f && Vector3.Distance(transform.position, alvoGPS) < distanciaMaximaRe);
        }

        Vector3 direcaoQueQueremosOlhar = emMarchaRe ? -direcaoParaAlvo : direcaoParaAlvo;

        // === 1. O LEME (ROTAÇÃO FÍSICA) ===
        float diferencaAngulo = Vector3.SignedAngle(transform.forward, direcaoQueQueremosOlhar, Vector3.up);
        float inputLeme = Mathf.Clamp(diferencaAngulo / 45f, -1f, 1f); // O quanto o piloto rodou o volante
        
        // A MÁGICA DO LEME: O leme só funciona se tiver água a passar por ele!
        float eficienciaLeme = Mathf.Clamp01(Mathf.Abs(velocidadeAvanco) / (velocidadeMaxima * 0.3f));
        
        // Aplica torque (força de rotação)
        rb.AddTorque(transform.up * inputLeme * forcaLeme * eficienciaLeme * Time.fixedDeltaTime, ForceMode.VelocityChange);

        // === 2. O MOTOR (PROPULSÃO FÍSICA) ===
        // O motor acelera de forma gradual (não vai do 0 ao 100 direto)
        potenciaMotorAtual = Mathf.MoveTowards(potenciaMotorAtual, 1f, Time.fixedDeltaTime / tempoAceleracaoMotor);

        // Empurra na direção que o bico (ou a traseira) está a apontar
        Vector3 direcaoMotor = emMarchaRe ? -transform.forward : transform.forward;
        float forcaFinalMotor = emMarchaRe ? forcaMotor * multiplicadorForcaRe : forcaMotor;
        
        rb.AddForce(direcaoMotor * forcaFinalMotor * potenciaMotorAtual * Time.fixedDeltaTime, ForceMode.Acceleration);

        // Limita a velocidade máxima
        if (rb.linearVelocity.magnitude > velocidadeMaxima)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * velocidadeMaxima;
        }

        // === 3. A QUILHA (O SEGREDO DO DRIFT E DA ADERÊNCIA) ===
        // Sem isto, o navio escorrega de lado como se estivesse no gelo.
        // Com isto, ele corta a água, mas ainda deixa a traseira deslizar um pouco (Drift).
        Vector3 velocidadeLocal = transform.InverseTransformDirection(rb.linearVelocity);
        float velocidadeLateral = velocidadeLocal.x; // Quão rápido ele está a deslizar de lado
        
        // Aplica uma força contrária ao deslizamento lateral
        rb.AddRelativeForce(Vector3.left * velocidadeLateral * aderenciaAguaLateral, ForceMode.Acceleration);
    }

    private void FrearFisicamente()
    {
        // Corta a potência do motor de forma rápida (freio a motor)
        potenciaMotorAtual = Mathf.MoveTowards(potenciaMotorAtual, 0f, Time.fixedDeltaTime / 2f);

        // Para frear, basta deixar a resistência da água (rb.drag) atuar naturalmente,
        // mas podemos ajudar a matar o deslizamento lateral mais depressa.
        Vector3 velocidadeLocal = transform.InverseTransformDirection(rb.linearVelocity);
        rb.AddRelativeForce(Vector3.left * velocidadeLocal.x * aderenciaAguaLateral, ForceMode.Acceleration);
    }

    private void AtualizarVisuais()
    {
        if (rastroAgua != null) 
            rastroAgua.emitting = (rb.linearVelocity.magnitude > 2f);

        if (modelo3D != null)
        {
            // A inclinação agora é baseada na força centrífuga real!
            // Quanto mais rápido ele roda (angularVelocity.y), mais ele inclina.
            float inclinacaoAlvo = -rb.angularVelocity.y * forcaInclinacao;
            inclinacaoAlvo = Mathf.Clamp(inclinacaoAlvo, -20f, 20f);
            
            Quaternion rotacaoInclinada = Quaternion.Euler(0, 0, inclinacaoAlvo);
            modelo3D.localRotation = Quaternion.Slerp(modelo3D.localRotation, rotacaoInclinada, Time.deltaTime * 3f);
        }
    }

    public void DefinirDestino(Vector3 novoDestino)
    {
        if (modoAncorado) return;

        if (agente != null && agente.enabled)
        {
            agente.SetDestination(novoDestino);
        }
    }

    public bool EstaEmMarchaRe()
    {
        return emMarchaRe;
    }
}