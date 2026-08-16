using UnityEngine;

/// <summary>
/// Gerencia os sons das unidades (helicóptero, carro, tanque, avião, navio)
/// Coloque este script em todas as unidades que precisam de som.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SomUnidade : MonoBehaviour
{
    [Header("Tipo de Unidade")]
    public TipoSomUnidade tipoUnidade = TipoSomUnidade.Carro;
    
    [Header("Sons de Movimento")]
    public AudioClip somMotor; // Som principal do motor/movimento
    public AudioClip somParado; // Som quando a unidade está parada (idle)
    
    [Header("Sons de Ação")]
    public AudioClip somTiro; // Som ao atirar (se tiver arma)
    public AudioClip somExplosao; // Som ao explodir/morrer
    public AudioClip somDano; // Som ao receber dano
    
    [Header("Configurações de Som")]
    [Range(0f, 1f)]
    public float volumeMotor = 0.5f;
    [Range(0f, 1f)]
    public float volumeTiro = 0.8f;
    [Range(0f, 1f)]
    public float volumeExplosao = 1f;
    
    [Range(0.5f, 2f)]
    public float pitchMin = 0.8f; // Tom mínimo quando parado
    [Range(0.5f, 2f)]
    public float pitchMax = 1.5f; // Tom máximo quando em movimento rápido

    [Header("Diagnostico")]
    [Tooltip("Use somente para investigar audio. Logs de transicao geram stack traces no Editor.")]
    public bool registrarTransicoesDeAudio = false;
    
    [Header("Configurações Específicas")]
    public float velocidadeParaMaxPitch = 10f; // Velocidade para atingir pitch máximo
    public bool loopMotor = true; // Se o som do motor deve fazer loop
    
    private AudioSource audioSource;
    private AudioSource audioSourceSecundario; // Para sons adicionais (tiro, explosão)
    private ControleUnidade controleUnidade;
    private SistemaDeDanos sistemaDanos;
    private float velocidadeAtual = 0f;
    private bool estaMovendo = false;
    private bool somMotorTocando = false;
    private bool avisouClipAusente;

    // Cache de Componentes
    private UnityEngine.AI.NavMeshAgent agenteCached;
    private Rigidbody rbCached;

    void Awake()
    {
        // Pega ou cria AudioSource principal
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Cria AudioSource secundário
        GameObject audioSecundarioObj = new GameObject("AudioSecundario");
        audioSecundarioObj.transform.SetParent(transform);
        audioSecundarioObj.transform.localPosition = Vector3.zero;
        audioSourceSecundario = audioSecundarioObj.AddComponent<AudioSource>();
        
        // Configuração padrão
        audioSource.spatialBlend = 1f; 
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 8f;
        audioSource.maxDistance = tipoUnidade == TipoSomUnidade.Aviao || tipoUnidade == TipoSomUnidade.Helicoptero ? 150f : 50f;
        
        // Torna o som de Tiro e Explosão em 3D
        audioSourceSecundario.spatialBlend = 1f;
        audioSourceSecundario.minDistance = 3f;
        audioSourceSecundario.maxDistance = 50f;
        audioSourceSecundario.rolloffMode = AudioRolloffMode.Linear;
        
        // Cachear referências pesadas
        controleUnidade = GetComponent<ControleUnidade>();
        sistemaDanos = GetComponent<SistemaDeDanos>();
        agenteCached = GetComponent<UnityEngine.AI.NavMeshAgent>();
        rbCached = GetComponent<Rigidbody>();
        if (sistemaDanos != null)
        {
            sistemaDanos.OnMorte -= TocarSomExplosao;
            sistemaDanos.OnDano -= TocarSomDano;
            sistemaDanos.OnMorte += TocarSomExplosao;
            sistemaDanos.OnDano += TocarSomDano;
        }
        ConfigurarSonsPadrao();
        TentarCarregarClipsDoPrefab();
    }

    void Update()
    {
        DetectarVelocidade();
        AjustarSomMotor();
    }

    void DetectarVelocidade()
    {
        float deltaMovimento = 0f;
        if (lastPosition != Vector3.zero)
        {
            deltaMovimento = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }
        lastPosition = transform.position;

        // Usa cache em vez de GetComponent todo frame
        if (agenteCached != null && agenteCached.enabled && agenteCached.isOnNavMesh)
        {
            velocidadeAtual = agenteCached.velocity.magnitude;
        }
        else if (rbCached != null && !rbCached.isKinematic)
        {
            velocidadeAtual = rbCached.linearVelocity.magnitude;
        }
        else
        {
            velocidadeAtual = deltaMovimento;
        }

        if (float.IsNaN(velocidadeAtual) || float.IsInfinity(velocidadeAtual))
        {
            velocidadeAtual = 0f;
        }

        if (deltaMovimento > velocidadeAtual)
        {
            velocidadeAtual = deltaMovimento;
        }
        
        estaMovendo = velocidadeAtual > 0.1f;
    }
    
    private Vector3 lastPosition = Vector3.zero;

    void AjustarSomMotor()
    {
        if (audioSource == null) return;

        if (somMotor == null && somParado == null)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            return;
        }
        
        // Se está movendo e o som não está tocando, inicia
        if (estaMovendo && !somMotorTocando)
        {
            IniciarSomMotor(true);
        }
        // Se parou e está no modo de som parado
        else if (!estaMovendo && somMotorTocando && somParado != null)
        {
            IniciarSomMotor(false);
        }
        
        // Ajusta o pitch baseado na velocidade (efeito Doppler simulado)
        if (somMotorTocando)
        {
            float proporcaoVelocidade = Mathf.Clamp01(velocidadeAtual / Mathf.Max(0.01f, velocidadeParaMaxPitch));
            audioSource.pitch = Mathf.Lerp(pitchMin, pitchMax, proporcaoVelocidade);
            audioSource.volume = volumeMotor * Mathf.Lerp(0.7f, 1f, proporcaoVelocidade);
        }
    }

    void IniciarSomMotor(bool movimento)
    {
        if (audioSource == null || !audioSource.isActiveAndEnabled)
        {
            somMotorTocando = false;
            return;
        }

        AudioClip clipParaTocar = movimento ? somMotor : somParado;
        if (clipParaTocar == null)
        {
            clipParaTocar = somMotor != null ? somMotor : somParado;
        }
        
        if (clipParaTocar == null)
        {
            // Sem essa trava uma unidade sem clip emitia o mesmo aviso em todos
            // os frames, o que custa mais que o proprio audio no Unity Editor.
            if (!avisouClipAusente)
            {
                Debug.LogWarning($"[SomUnidade] Tentou tocar som mas clip é null! Movimento: {movimento}");
                avisouClipAusente = true;
            }
            return;
        }
        
        audioSource.clip = clipParaTocar;
        audioSource.loop = loopMotor;
        audioSource.volume = volumeMotor;
        audioSource.pitch = pitchMin;
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
        
        somMotorTocando = true;
        
        if (registrarTransicoesDeAudio)
        {
            Debug.Log($"[SomUnidade] 🔊 SOM TOCANDO: {clipParaTocar.name} | Volume: {volumeMotor} | Loop: {loopMotor} | isPlaying: {audioSource.isPlaying}");
        }
    }

    void ConfigurarSonsPadrao()
    {
        // Define distâncias de áudio específicas por tipo
        switch (tipoUnidade)
        {
            case TipoSomUnidade.Helicoptero:
                audioSource.maxDistance = 150f;
                velocidadeParaMaxPitch = 15f;
                pitchMin = 0.9f;
                pitchMax = 1.3f;
                break;
                
            case TipoSomUnidade.Aviao:
                audioSource.maxDistance = 150f;
                velocidadeParaMaxPitch = 50f;
                pitchMin = 0.8f;
                pitchMax = 1.8f;
                break;
                
            case TipoSomUnidade.Tank:
                audioSource.maxDistance = 50f;
                velocidadeParaMaxPitch = 8f;
                pitchMin = 0.7f;
                pitchMax = 1.2f;
                break;
                
            case TipoSomUnidade.Carro:
                audioSource.maxDistance = 50f;
                velocidadeParaMaxPitch = 12f;
                pitchMin = 0.8f;
                pitchMax = 1.5f;
                break;
                
            case TipoSomUnidade.Navio:
                audioSource.maxDistance = 50f;
                velocidadeParaMaxPitch = 5f;
                pitchMin = 0.6f;
                pitchMax = 1.0f;
                break;
        }
    }

    private void TentarCarregarClipsDoPrefab()
    {
        if (somMotor == null || somParado == null || somTiro == null || somExplosao == null || somDano == null)
        {
            AudioSource[] fontes = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < fontes.Length; i++)
            {
                AudioSource fonte = fontes[i];
                if (fonte == null || fonte.clip == null) continue;

                if (somMotor == null) somMotor = fonte.clip;
                else if (somParado == null) somParado = fonte.clip;
                else if (somTiro == null) somTiro = fonte.clip;
                else if (somExplosao == null) somExplosao = fonte.clip;
                else if (somDano == null) somDano = fonte.clip;
            }
        }
    }

    // === MÉTODOS PÚBLICOS PARA OUTROS SCRIPTS CHAMAREM ===
    
    /// <summary>
    /// Toca o som de tiro (chamado pelo SistemaDeTiro)
    /// </summary>
    public void TocarSomTiro()
    {
        if (somTiro != null && audioSourceSecundario != null)
        {
            audioSourceSecundario.pitch = Random.Range(0.9f, 1.1f); // Variação leve
            audioSourceSecundario.PlayOneShot(somTiro, volumeTiro);
        }
    }
    
    /// <summary>
    /// Toca o som de explosão (chamado quando morre)
    /// </summary>
    public void TocarSomExplosao()
    {
        if (somExplosao != null && audioSourceSecundario != null)
        {
            audioSourceSecundario.PlayOneShot(somExplosao, volumeExplosao);
        }
    }
    
    /// <summary>
    /// Toca o som de dano (chamado pelo SistemaDeDanos)
    /// </summary>
    public void TocarSomDano()
    {
        if (somDano != null && audioSourceSecundario != null)
        {
            audioSourceSecundario.pitch = Random.Range(0.8f, 1.2f);
            audioSourceSecundario.PlayOneShot(somDano, volumeMotor * 0.8f);
        }
    }

    void OnDestroy()
    {
        // Remove os listeners de eventos
        if (sistemaDanos != null)
        {
            sistemaDanos.OnMorte -= TocarSomExplosao;
            sistemaDanos.OnDano -= TocarSomDano;
        }
    }
}

/// <summary>
/// Enum para definir o tipo de som da unidade
/// </summary>
public enum TipoSomUnidade
{
    Helicoptero,
    Aviao,
    Tank,
    Carro,
    Navio
}
