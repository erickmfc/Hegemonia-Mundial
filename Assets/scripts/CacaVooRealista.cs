using UnityEngine;

/// <summary>
/// MÓDULO MILITAR: MOTOR DE VOO REALISTA PARA CAÇAS
/// Simula aerodinâmica base, inclinação de asas (Banking), inércia de motor e curvas em arco.
/// </summary>
public class CacaVooRealista : MonoBehaviour
{
    [Header("=== FÍSICA E TURBINAS ===")]
    public float velocidadeMaxima = 150f;
    [Tooltip("Quão rápido ele atinge a velocidade máxima (G-Force de arrancada)")]
    public float aceleracaoMotor = 45f;
    [Tooltip("Velocidade do giro do Leme (Menos valor = arcos mais abertos e reais)")]
    public float taxaDeGiroLeme = 60f;

    [Header("=== AERODINÂMICA E ANIMAÇÃO VISUAL ===")]
    [Tooltip("ARRASTE AQUI O 'CORPO' DO AVIÃO! (O modelo 3D que é filho do objeto principal)")]
    public Transform modeloMecanicoVisual; 
    
    [Tooltip("Até quantos graus as asas deitam quando ele faz uma curva aguda? (Ex: 75º)")]
    public float asaBankingMaximo = 75f; 
    
    [Tooltip("Até quantos graus o bico do avião empina para cima ou mergulha ao mudar de altura?")]
    public float arfagemPitchMaxima = 30f; 

    [Tooltip("Velocidade com que a asa volta ao eixo perfeito após a curva")]
    public float sensibilidadeAerodinamica = 3.5f;

    [Header("=== EFEITOS (VFX / SFX) ===")]
    public ParticleSystem[] posQueimadores; // O fogo das duas saídas do jato
    public AudioSource somMotorJato;

    [Header("=== RADAR / ALVO ===")]
    public Vector3 alvoGPS;
    public bool motoresEmRota = false;
    
    // Matemática Oculta
    private float velocidadeAtual = 0f;
    private float giroLateralRoll = 0f; // Eixo Z da malha (Asas)
    private float empinadaPitch = 0f;   // Eixo X da malha (Nariz)

    void Start()
    {
        // Se o designer esquecer de arrastar o modelo 3D no Inspector, o script acha o primeiro filho automaticamente.
        if (modeloMecanicoVisual == null && transform.childCount > 0)
        {
            modeloMecanicoVisual = transform.GetChild(0);
        }
    }

    void Update()
    {
        // A física só atua enquanto tivermos uma missão/destino engatado
        if (motoresEmRota)
        {
            ManobraEInterpolacaoCurva();
            SensoriamentoDoMotor();
        }
        else
        {
            // Corta os pós-queimadores quando pousar ou terminar missão
            DesligarVFX();
        }
    }

    /// <summary>
    /// Chama esta função de qualquer outro script (Menu, Aeroporto, Radar) para fazer o caça voar para X lugar!
    /// </summary>
    public void OrdenarAtaqueOuPatrulha(Vector3 novaCoordenadaAlvo)
    {
        alvoGPS = novaCoordenadaAlvo;
        motoresEmRota = true;

        if (somMotorJato != null && !somMotorJato.isPlaying) 
            somMotorJato.Play();

        foreach (var fogo in posQueimadores) 
            if (fogo != null && !fogo.isPlaying) fogo.Play();
    }

    /// <summary>
    /// Força ele a aterrissar de emergência ou cancelar rotação
    /// </summary>
    public void AbortarVoo()
    {
        motoresEmRota = false;
        velocidadeAtual = 0f; // Corta motor
        // Nivela assas automaticamente de volta ao eixo zero
        if(modeloMecanicoVisual != null) modeloMecanicoVisual.localRotation = Quaternion.identity; 
    }

    // O NÚCLEO DA FÍSICA QUE GERA A ILUSÃO MILITAR PERFEITA
    // Não usa e não precisa de NavMesh. Corta o ar em 3D.
    private void ManobraEInterpolacaoCurva()
    {
        // 1. INÉRCIA: O jato nunca decola a 300km/h no frame 1. Ele espreme a velocidade aos poucos.
        velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeMaxima, aceleracaoMotor * Time.deltaTime);

        // Geometria de distância
        Vector3 retaAteAlvo = alvoGPS - transform.position;
        float distanciaProAlvo = retaAteAlvo.magnitude;

        if (distanciaProAlvo < 5f)
        {
            // Encostou no alvo (Ação terminada ou em espera do próximo código)
            // Aqui você pode acoplar soltar bombas, se quiser.
            return;
        }

        // 2. TIMÃO / LEME (Muda o nariz pra mirar). 
        // Os aviões não "drifitam" pros lados perfeitamente. Eles são girados no seu eixo raiz.
        Quaternion olharMundoDesejado = Quaternion.LookRotation(retaAteAlvo);
        
        // Matemágica: Lê a diferença entre a FRENTE do avião atual com o LUGAR pra onde eu QUERO apontar
        float anguloPressaoLateralY = Vector3.SignedAngle(transform.forward, retaAteAlvo, Vector3.up);

        // Vira a CAIXA RAIZ (o GameObject master) estritamente nessa direção na curva predefinida.
        transform.rotation = Quaternion.RotateTowards(transform.rotation, olharMundoDesejado, taxaDeGiroLeme * Time.deltaTime);
        
        // 3. PROPULSÃO FÍSICA: Ao forçar o "master" pra frente o tempo todo, ele fará um grande 
        // arco no céu, igual um caça deitando nas nuvens para virar para trás.
        transform.position += transform.forward * velocidadeAtual * Time.deltaTime;

        // 4. EFEITO DE BANKING (A magica aerodinâmica)
        if (modeloMecanicoVisual != null)
        {
            // Roll (Z): Depende estritamente do ângulo Y (Taxa de guinada). Curva pra direita = Asa tomba pra direita.
            // O multiplicador negativo inverte o sentido pro eixo correto da Unity (Esquerda levanta, afunda a Direita)
            float inclinacaoAlvoZ = Mathf.Clamp(anguloPressaoLateralY * -1.8f, -asaBankingMaximo, asaBankingMaximo);
            
            // Pitch (X): Depende da altura do alvo. Quer subir = Bico levanta. Quer Descer = Bico mergulha.
            // Usamos uma conta leve na distância Y dividida, e limitamos pra não dar loop backflip.
            float inclinacaoAlvoX = Mathf.Clamp(retaAteAlvo.y * -2.0f, -arfagemPitchMaxima, arfagemPitchMaxima);

            // Transições amanteigadas 
            giroLateralRoll = Mathf.Lerp(giroLateralRoll, inclinacaoAlvoZ, Time.deltaTime * sensibilidadeAerodinamica);
            empinadaPitch = Mathf.Lerp(empinadaPitch, inclinacaoAlvoX, Time.deltaTime * (sensibilidadeAerodinamica * 0.8f));

            // Aplica APENAS na malha visual. A caixa master contínua reta para evitar giros bizarros da física global.
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, 0f, giroLateralRoll);
        }
    }

    private void SensoriamentoDoMotor()
    {
        if (somMotorJato != null)
        {
            // Quanto mais rápido o avião, o pitch de áudio grita mais agudo (efeito Doppler de turbina)
            somMotorJato.pitch = Mathf.Lerp(0.6f, 1.8f, velocidadeAtual / velocidadeMaxima);
        }
    }

    private void DesligarVFX()
    {
        if (somMotorJato != null && somMotorJato.isPlaying)
        {
            somMotorJato.volume = Mathf.Lerp(somMotorJato.volume, 0f, Time.deltaTime * 2f);
            if (somMotorJato.volume <= 0.05f) somMotorJato.Stop();
        }

        foreach (var fogo in posQueimadores)
        {
            if (fogo != null && fogo.isPlaying) fogo.Stop();
        }
    }
}
