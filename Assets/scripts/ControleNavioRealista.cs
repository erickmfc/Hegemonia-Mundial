using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
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

    [Header("Ajuste de Altura (Navio / Submarino)")]
    [Tooltip("Valor Positivo = Mais alto (flutuando). Negativo = Mais fundo (submarino).")]
    public float offsetAlturaAgua = 0.0f; 
    
    // Mantido para compatibilidade interna, mas agora somado ao offset
    [HideInInspector] public float profundidadeVisual = 0f;

    [Header("Manobra de Ré")]
    [Tooltip("Ângulo mínimo para entrar em manobra de ré quando o alvo está muito atrás.")]
    public float anguloEntradaManobraRe = 120f;
    [Tooltip("Ângulo em que a manobra de ré é encerrada e o navio volta a avançar.")]
    public float anguloSaidaManobraRe = 70f;
    [Tooltip("Tempo máximo da manobra de ré antes de forçar retorno à navegação frontal.")]
    public float duracaoMaximaManobraRe = 2.5f;
    [Tooltip("Potência aplicada durante a manobra de ré.")]
    public float potenciaManobraRe = -0.5f;

    [Header("Referências Visuais")]
    public ParticleSystem bigodeiraProa; // Espuma na frente
    public TrailRenderer rastroEsteira;  // Rastro longo
    public ParticleSystem turbulenciaPopa; // Cavitação atrás
    public Transform modelo3D; // O casco visual para rotacionar

    [Header("Áudio e Energia")]
    public ModoOperacao modoOperacao = ModoOperacao.Ativo;
    public AudioClip somMotorParado;
    public AudioClip somMotorMovimento;
    private AudioSource fonteAudio;
    private float tempoInatividade = 0f;
    private bool estaDesligado = false;

    public enum ModoOperacao
    {
        Ativo,   // Sempre ligado
        Passivo  // Desliga após 20s parado
    }

    // Estado Interno (Simulação)
    private NavMeshAgent agente;
    private Rigidbody rb;
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
    private IdentidadeNaval identidade;
    private bool ajusteInicialFlutuacaoVerificado = false;
    private float tempoAssistenciaSaida = 0f;
    private Vector3 destinoAssistenciaSaida = Vector3.zero;
    private bool manobraReAtiva = false;
    private float tempoRestanteManobraRe = 0f;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        identidade = GetComponent<IdentidadeNaval>();
        if (identidade == null)
            identidade = GetComponentInChildren<IdentidadeNaval>();

        // Correção de Robustez: Adiciona Rigidbody se faltar
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true; // Soft-Sim: Nós movemos via Transform
            Debug.LogWarning($"[ControleNavioRealista] Rigidbody adicionado automaticamente ao {name}.");
        }

        if (agente != null)
        {
            agente.updatePosition = false; // Nós controlamos a posição (Soft-Sim)
            agente.updateRotation = false; // Nós controlamos a rotação (Physics)
            agente.acceleration = 9999; // Agente "NavMesh" instantâneo, nós seguimos ele
        }
        else
        {
            Debug.LogError("[ControleNavioRealista] NavMeshAgent não encontrado!");
        }
        
        offsetOnda = Random.Range(0f, 100f);

        if (modelo3D == null && transform.childCount > 0)
            modelo3D = transform.GetChild(0);

        if (modelo3D != null)
            rotacaoInicialModelo = modelo3D.localRotation;

        // Configuração de Áudio
        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null)
            fonteAudio = gameObject.AddComponent<AudioSource>();
            
        fonteAudio.loop = true;
        fonteAudio.spatialBlend = 1.0f; // 3D Sound
        fonteAudio.playOnAwake = false;

        // Garante que os efeitos começam desligados
        if(bigodeiraProa) bigodeiraProa.Stop();
        if(turbulenciaPopa) turbulenciaPopa.Stop();
    }

    void Update()
    {
        if (agente == null) return;

        if (tempoAssistenciaSaida > 0f)
        {
            tempoAssistenciaSaida = Mathf.Max(0f, tempoAssistenciaSaida - Time.deltaTime);
            if (tempoAssistenciaSaida <= 0f)
            {
                destinoAssistenciaSaida = Vector3.zero;
            }
        }

        if (!AgenteProntoParaLeitura())
        {
            return;
        }
        
        // 0. VERIFICAÇÃO DE ATIVIDADE
        VerificarInatividade();
        
        // 1. INPUT (IA ou Player via NavMesh)
        CalcularInputNavegacao();

        // 2. SIMULAÇÃO DE MOTOR E HELICE
        SimularMotor();

        // 3. DIN MICA DE MOVIMENTO (Inércia e Drift)
        SimularFisicaMovimento();

        // 4. VISUAIS
        AtualizarEfeitosVisuais();

        // 5. AUDIO
        AtualizarAudio();
    }

    void VerificarInatividade()
    {
        // Se tem destino ou velocidade considerável (ou input de potencia), está ativo
        // Consideramos "Função" como ter um destino ou estar se movendo por propulsão
        if (temDestino || Mathf.Abs(potenciaAtual) > 0.05f || velocidadeVetorial.magnitude > 0.5f)
        {
            tempoInatividade = 0f;
            if (estaDesligado) estaDesligado = false; // Acorda instantaneamente se tiver atividade
        }
        else
        {
            tempoInatividade += Time.deltaTime;
        }

        // Regra de Desligamento no Modo Passivo
        if (modoOperacao == ModoOperacao.Passivo && tempoInatividade > 20.0f)
        {
            estaDesligado = true;
        }
    }

    void CalcularInputNavegacao()
    {
        if (!AgenteProntoParaLeitura())
        {
            potenciaAlvo = 0f;
            temDestino = false;
            manobraReAtiva = false;
            tempoRestanteManobraRe = 0f;
            anguloLemeAtual = Mathf.MoveTowards(anguloLemeAtual, 0f, Time.deltaTime * velocidadeLeme);
            return;
        }

        if (agente.pathPending)
        {
            temDestino = true;
            return;
        }

        if (!agente.hasPath && velocidadeVetorial.magnitude < 0.1f)
        {
            potenciaAlvo = 0f;
            temDestino = false;
            manobraReAtiva = false;
            tempoRestanteManobraRe = 0f;
            return;
        }

        float distancia = agente.remainingDistance;
        
        // Ponto de chegada sem freio brusco longe
        if (distancia < distanciaChegada || !agente.hasPath)
        {
            potenciaAlvo = 0f;
            temDestino = false;
            manobraReAtiva = false;
            tempoRestanteManobraRe = 0f;
            return;
        }

        temDestino = true;
        
        // Alvo no horizonte (Navigation Waypoint)
        Vector3 direcaoAlvo = agente.steeringTarget - transform.position;
        direcaoAlvo.y = 0f;
        if (direcaoAlvo.sqrMagnitude < 0.001f && destinoAtual != Vector3.zero)
        {
            direcaoAlvo = destinoAtual - transform.position;
            direcaoAlvo.y = 0f;
        }

        if (direcaoAlvo.sqrMagnitude > 0.001f)
        {
            direcaoAlvo.Normalize();
        }

        if (tempoAssistenciaSaida > 0f && destinoAssistenciaSaida != Vector3.zero)
        {
            Vector3 direcaoSaida = destinoAssistenciaSaida - transform.position;
            direcaoSaida.y = 0f;
            if (direcaoSaida.sqrMagnitude > 1f)
            {
                direcaoSaida.Normalize();
                direcaoAlvo = Vector3.Slerp(direcaoAlvo, direcaoSaida, 0.8f).normalized;
            }
        }

        float angulo = Vector3.SignedAngle(transform.forward, direcaoAlvo, Vector3.up);
        float velocidadeLongitudinal = Vector3.Dot(velocidadeVetorial, transform.forward);
        float velocidadeAbsoluta = Mathf.Abs(velocidadeLongitudinal);

        // O LEME vira proporcional ao angulo (30 graus é o suficiente pra dar leme máximo)
        float inputLeme = Mathf.Clamp(angulo / 30.0f, -1f, 1f);

        // NUNCA PODE PARAR PARA VIRAR. Se o jogador mandou ir, o navio acelera para frente e vai corrigindo o Leme!
        // Aceleração total a menos que esteja muito perto do destino (freio d'água)
        if (distancia < 40.0f)
        {
            potenciaAlvo = Mathf.Clamp01(distancia / 40.0f);
        }
        else
        {
            potenciaAlvo = 1.0f; // Força total em frente, desenhando o arco gigantesco característico de navios!
        }

        bool podeEntrarManobraRe = tempoAssistenciaSaida <= 0f
            && Mathf.Abs(angulo) >= anguloEntradaManobraRe
            && velocidadeAbsoluta < 1.0f
            && distancia > 20f;

        if (!manobraReAtiva && podeEntrarManobraRe)
        {
            manobraReAtiva = true;
            tempoRestanteManobraRe = Mathf.Max(0.5f, duracaoMaximaManobraRe);
        }

        if (manobraReAtiva)
        {
            tempoRestanteManobraRe = Mathf.Max(0f, tempoRestanteManobraRe - Time.deltaTime);
            inputLeme = -Mathf.Clamp(angulo / 30.0f, -1f, 1f);
            potenciaAlvo = potenciaManobraRe;

            bool podeSairDaManobraRe = Mathf.Abs(angulo) <= anguloSaidaManobraRe
                || distancia <= 20f
                || tempoRestanteManobraRe <= 0f
                || velocidadeLongitudinal > 1.5f;

            if (podeSairDaManobraRe)
            {
                manobraReAtiva = false;
                tempoRestanteManobraRe = 0f;
            }
        }

        // Movimento do leme hidráulico macio
        anguloLemeAtual = Mathf.MoveTowards(anguloLemeAtual, inputLeme, Time.deltaTime * velocidadeLeme);
    }

    bool AgenteProntoParaLeitura()
    {
        return agente != null
            && agente.enabled
            && agente.isActiveAndEnabled
            && agente.isOnNavMesh;
    }

    bool TentarPrepararAgenteParaNavegacao()
    {
        if (agente == null || !gameObject.activeInHierarchy)
        {
            return false;
        }

        try
        {
            if (!agente.enabled)
            {
                agente.enabled = true;
            }

            if (!agente.isOnNavMesh)
            {
                NavMeshHit hit;
                int areaMask = agente.areaMask;
                if (areaMask == 0)
                {
                    areaMask = NavMesh.AllAreas;
                }

                if (!NavMesh.SamplePosition(transform.position, out hit, 120f, areaMask))
                {
                    NavMesh.SamplePosition(transform.position, out hit, 120f, NavMesh.AllAreas);
                }

                if (hit.hit)
                {
                    agente.Warp(hit.position);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ControleNavioRealista] Falha ao preparar NavMeshAgent em {name}: {ex.Message}");
        }

        return AgenteProntoParaLeitura();
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
        if (!rb.isKinematic) rb.isKinematic = true;

        // VELOCIDADE PURA DE TRILHO (1 Eixo): Só existe movimento na mesma linha que a frente do navio aponta
        float velReal = velocidadeVetorial.magnitude;
        // Verifica se tá de ré (Dot Product < 0)
        if (Vector3.Dot(velocidadeVetorial, transform.forward) < 0f) velReal = -velReal;
        if (float.IsNaN(velReal)) velReal = 0f;

        // 1. Rotação (A curva arcada maravilhosa)
        // O navio precisa de velocidade fluindo no leme para girar.
        float fluxoAgua = Mathf.Abs(velReal) + (Mathf.Abs(potenciaAtual) * 2f); 
        float eficienciaLeme = Mathf.Clamp01(fluxoAgua / 2.0f); 
        
        float sentidoMotor = velReal >= -0.1f ? 1f : -1f; // Retrocede a curva se der ré
        float giro = anguloLemeAtual * curvaMaximaGraus * eficienciaLeme * Time.deltaTime * sentidoMotor;
        
        transform.Rotate(0, giro, 0);

        // 2. Aceleração (Sempre no eixo Z direcional)
        // Empurra para a potência desejada gradual
        float velocidadeAlvoReal = (potenciaAtual * velocidadeMaxima);
        
        // Aplica o acelerador hidráulico suave
        if (velReal < velocidadeAlvoReal)
            velReal += (velocidadeMaxima * Time.deltaTime / tempoAceleracao);
        else if (velReal > velocidadeAlvoReal)
            velReal -= (velocidadeMaxima * Time.deltaTime / tempoAceleracao);
            
        // Fricção para atrito e estacionamento
        float dragAtual = arrastoPassivo;
        if (!temDestino) dragAtual *= 4.0f; 
        velReal -= velReal * (Time.deltaTime * dragAtual); 
        
        // Limita a física
        velReal = Mathf.Clamp(velReal, -velocidadeMaxima * 0.4f, velocidadeMaxima);

        // O TRUQUE MESTRE: Sobrescreve TODO o vetor 3D forçando o navio a NUNCA patinar.
        // O navio vai rasgar a água feito um dardo, só vai pra onde o nariz rotacionar.
        velocidadeVetorial = transform.forward * velReal;

        agentNextPositionCheck(transform.position + velocidadeVetorial * Time.deltaTime);
    }
    
    void agentNextPositionCheck(Vector3 novaPos)
    {
        float nivelMar = NavalPlacementResolver.ResolveSeaLevel();

        // 1. Defina a posição visual do GameObject (Barco/Submarino) na profundidade desejada
        // Soma o offset (configurado no inspector) com a profundidade interna
        float alturaFinal = profundidadeVisual + offsetAlturaAgua;

        Vector3 posVisual = new Vector3(novaPos.x, nivelMar + alturaFinal, novaPos.z);
        transform.position = posVisual;

        if (!ajusteInicialFlutuacaoVerificado)
        {
            ajusteInicialFlutuacaoVerificado = true;
            if (TentarCorrigirFlutuacaoInicial(nivelMar))
            {
                alturaFinal = profundidadeVisual + offsetAlturaAgua;
                posVisual.y = nivelMar + alturaFinal;
                transform.position = posVisual;
            }
        }

        // 2. Informe ao NavMeshAgent que ele "virtualmente" está na superfície (NavMesh)
        Vector3 posSimulacao = new Vector3(novaPos.x, nivelMar, novaPos.z);
        agente.nextPosition = posSimulacao; 

        // 3. Ajuste o colisor (Cilindro do Agent) para que ele suba até a superfície e não fique afundado junto com o visual
        // Mantém o agente "virtual" no nível do mar mesmo com o casco visual abaixo/acima dele.
        agente.baseOffset = nivelMar - posVisual.y;
    }

    bool TentarCorrigirFlutuacaoInicial(float nivelMar)
    {
        if (identidade != null && identidade.categoriaNavio == IdentidadeNaval.CategoriaNavio.Submarino)
        {
            return false;
        }

        Bounds cascoBounds;
        if (!TryGetBoundsDoCascoPrincipal(out cascoBounds))
        {
            return false;
        }

        float topoMinimo = nivelMar + Mathf.Max(0.35f, cascoBounds.size.y * 0.08f);
        if (cascoBounds.max.y >= topoMinimo)
        {
            return false;
        }

        float ajuste = topoMinimo - cascoBounds.max.y;
        if (ajuste <= 0.01f)
        {
            return false;
        }

        profundidadeVisual += ajuste;
        Debug.Log($"[ControleNavioRealista] Ajuste automático de flutuação em {name}: +{ajuste:F2}m.");
        return true;
    }

    bool TryGetBoundsDoCascoPrincipal(out Bounds cascoBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        cascoBounds = new Bounds(transform.position, Vector3.zero);

        bool encontrou = false;
        float melhorScore = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererAtual = renderers[i];
            if (rendererAtual == null || !rendererAtual.enabled)
            {
                continue;
            }

            if (rendererAtual is ParticleSystemRenderer || rendererAtual is TrailRenderer)
            {
                continue;
            }

            Bounds boundsAtual = rendererAtual.bounds;
            float scoreAtual = boundsAtual.size.x * boundsAtual.size.y * boundsAtual.size.z;
            if (!encontrou || scoreAtual > melhorScore)
            {
                encontrou = true;
                melhorScore = scoreAtual;
                cascoBounds = boundsAtual;
            }
        }

        return encontrou;
    }

    void AtualizarEfeitosVisuais()
    {
        if (modelo3D == null) return;

        // Se estiver desligado (Economia de Energia / Stealth), corta efeitos
        if (estaDesligado)
        {
            if (bigodeiraProa && bigodeiraProa.isPlaying) bigodeiraProa.Stop();
            if (turbulenciaPopa && turbulenciaPopa.isPlaying) turbulenciaPopa.Stop();
            if (rastroEsteira) rastroEsteira.emitting = false;
            
            // Mantém apenas o balanço suave do mar (sem motor)
            float balancoMarOff = Mathf.Sin(Time.time * frequenciaOnda + offsetOnda) * 2.0f;
            float pitchOndaOff = Mathf.Cos(Time.time * (frequenciaOnda * 1.5f) + offsetOnda) * alturaOnda;
            
            Quaternion simulacaoOffsetOff = Quaternion.Euler(pitchOndaOff, 0, balancoMarOff);
            Quaternion rotacaoFinalOff = rotacaoInicialModelo * simulacaoOffsetOff;
            modelo3D.localRotation = Quaternion.Slerp(modelo3D.localRotation, rotacaoFinalOff, Time.deltaTime * 0.5f);
            return;
        }

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

    void AtualizarAudio()
    {
        if (fonteAudio == null) return;

        // Regra de Desligamento
        if (estaDesligado)
        {
            if (fonteAudio.isPlaying)
            {
                fonteAudio.Stop();
            }
            return;
        }

        // Verifica estado de movimento (Velocidade ou Potência do Motor)
        // Se a potência for significativa (motor ligado) ou velocidade > 1, consideramos "em movimento"
        bool emMovimento = velocidadeVetorial.magnitude > 1.0f || Mathf.Abs(potenciaAtual) > 0.1f;
        AudioClip clipDesejado = emMovimento ? somMotorMovimento : somMotorParado;

        if (clipDesejado != null)
        {
            if (fonteAudio.clip != clipDesejado || !fonteAudio.isPlaying)
            {
                fonteAudio.clip = clipDesejado;
                fonteAudio.Play();
            }
            
            // Ajuste de Pitch dinâmico
            if (emMovimento)
                fonteAudio.pitch = Mathf.Lerp(0.9f, 1.2f, velocidadeVetorial.magnitude / velocidadeMaxima);
            else
                fonteAudio.pitch = 1.0f;
        }
    }
    
    // Método para integração com ControleUnidade
    public void DefinirDestino(Vector3 destino)
    {
        if (TentarPrepararAgenteParaNavegacao())
        {
            // Guarda: se o destino é praticamente o mesmo e já temos um path ativo,
            // não interrompe a navegação (evita engasgamento durante patrulha).
            if (Vector3.Distance(destinoAtual, destino) < 2f
                && agente.isOnNavMesh
                && (agente.hasPath || agente.pathPending))
            {
                return;
            }

            destinoAtual = destino;
            agente.isStopped = false;
            manobraReAtiva = false;
            tempoRestanteManobraRe = 0f;
            bool destinoAceito = agente.SetDestination(destino);
            temDestino = destinoAceito;

            if (!destinoAceito)
            {
                NavMeshHit hitDestino;
                int areaMask = agente.areaMask == 0 ? NavMesh.AllAreas : agente.areaMask;
                if (!NavMesh.SamplePosition(destino, out hitDestino, 180f, areaMask))
                {
                    NavMesh.SamplePosition(destino, out hitDestino, 180f, NavMesh.AllAreas);
                }

                if (hitDestino.hit)
                {
                    destinoAceito = agente.SetDestination(hitDestino.position);
                    temDestino = destinoAceito;
                }
            }

            // Se receber uma ordem, acorda!
            estaDesligado = false;
            tempoInatividade = 0;
            // Se estiver em modo passivo, ele vai começar a contar o tempo de novo quando parar.
            if (!destinoAceito)
            {
                Debug.LogWarning($"[ControleNavioRealista] NavMesh rejeitou destino para {name} em {destino}.");
            }
            return;
        }

        Debug.LogWarning($"[ControleNavioRealista] Tentativa de navegar sem estar no NavMesh! ({name})");
    }

    public void PrepararSaidaInicial(Vector3 destinoSaida, float duracaoAssistencia = 8f)
    {
        destinoAssistenciaSaida = destinoSaida;
        tempoAssistenciaSaida = Mathf.Max(tempoAssistenciaSaida, Mathf.Max(1.5f, duracaoAssistencia));

        Vector3 direcaoSaida = destinoSaida - transform.position;
        direcaoSaida.y = 0f;
        if (direcaoSaida.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(direcaoSaida.normalized, Vector3.up);
        }

        estaDesligado = false;
        tempoInatividade = 0f;
    }
    
    // Gizmos para ver o vetor de movimento vs frente
    void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 5f);
        
        Gizmos.color = Color.yellow; // Vector Drift
        Gizmos.DrawLine(transform.position, transform.position + velocidadeVetorial);
        
        if (AgenteProntoParaLeitura() && agente.hasPath)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, agente.steeringTarget);
        }
    }
}
