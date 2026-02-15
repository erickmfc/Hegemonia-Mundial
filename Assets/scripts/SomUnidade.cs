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
        audioSource.maxDistance = 50f;
        
        // Cachear referências pesadas
        controleUnidade = GetComponent<ControleUnidade>();
        sistemaDanos = GetComponent<SistemaDeDanos>();
        agenteCached = GetComponent<UnityEngine.AI.NavMeshAgent>();
        rbCached = GetComponent<Rigidbody>();
    }

    void DetectarVelocidade()
    {
        // Usa cache em vez de GetComponent todo frame
        if (agenteCached != null && agenteCached.enabled && agenteCached.isOnNavMesh)
        {
            velocidadeAtual = agenteCached.velocity.magnitude;
        }
        // Para unidades com Rigidbody (física)
        else if (rbCached != null)
        {
            velocidadeAtual = rbCached.linearVelocity.magnitude;
        }
        // Para unidades aéreas manuais
        else if (controleUnidade != null)
        {
            if (lastPosition != Vector3.zero)
            {
                velocidadeAtual = (transform.position - lastPosition).magnitude / Time.deltaTime;
            }
            lastPosition = transform.position;
        }
        
        estaMovendo = velocidadeAtual > 0.1f;
    }
    
    private Vector3 lastPosition = Vector3.zero;

    void AjustarSomMotor()
    {
        if (somMotor == null) return;
        
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
            float proporcaoVelocidade = Mathf.Clamp01(velocidadeAtual / velocidadeParaMaxPitch);
            audioSource.pitch = Mathf.Lerp(pitchMin, pitchMax, proporcaoVelocidade);
            audioSource.volume = volumeMotor * Mathf.Lerp(0.7f, 1f, proporcaoVelocidade);
        }
    }

    void IniciarSomMotor(bool movimento)
    {
        AudioClip clipParaTocar = movimento ? somMotor : somParado;
        
        if (clipParaTocar == null)
        {
            Debug.LogWarning($"[SomUnidade] Tentou tocar som mas clip é null! Movimento: {movimento}");
            return;
        }
        
        audioSource.clip = clipParaTocar;
        audioSource.loop = loopMotor;
        audioSource.volume = volumeMotor;
        audioSource.pitch = pitchMin;
        audioSource.Play();
        
        somMotorTocando = true;
        
        Debug.Log($"[SomUnidade] 🔊 SOM TOCANDO: {clipParaTocar.name} | Volume: {volumeMotor} | Loop: {loopMotor} | isPlaying: {audioSource.isPlaying}");
    }

    void ConfigurarSonsPadrao()
    {
        // Define distâncias de áudio específicas por tipo
        switch (tipoUnidade)
        {
            case TipoSomUnidade.Helicoptero:
                audioSource.maxDistance = 80f;
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
                audioSource.maxDistance = 60f;
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
                audioSource.maxDistance = 100f;
                velocidadeParaMaxPitch = 5f;
                pitchMin = 0.6f;
                pitchMax = 1.0f;
                break;
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
