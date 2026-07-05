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
    public ParticleSystem[] posQueimadores;
    public AudioSource somMotorJato;

    [Header("=== RADAR / ALVO ===")]
    public Vector3 alvoGPS;
    public bool motoresEmRota = false;
    
    private float velocidadeAtual = 0f;
    private float giroLateralRoll = 0f;
    private float empinadaPitch = 0f;
    private float _sensibilidadePitchCache;
    private Transform thisRoot;

    void Start()
    {
        // Garante que vai mover a base do avião e não apenas arrancar a malha visual fora do círculo
        thisRoot = transform.root;
        
        if (modeloMecanicoVisual == null)
        {
            if (transform.childCount > 0)
                modeloMecanicoVisual = transform.GetChild(0);
            else
                modeloMecanicoVisual = this.transform; // Se o script está na própria malha
        }
        
        _sensibilidadePitchCache = sensibilidadeAerodinamica * 0.8f;
        
        if (somMotorJato == null)
            somMotorJato = GetComponentInParent<AudioSource>();
        if (somMotorJato == null)
            somMotorJato = GetComponent<AudioSource>();
        if (somMotorJato == null)
            somMotorJato = gameObject.AddComponent<AudioSource>();

        somMotorJato.playOnAwake = false;
        somMotorJato.loop = true;
        somMotorJato.spatialBlend = 1f;
        somMotorJato.rolloffMode = AudioRolloffMode.Linear;
        somMotorJato.minDistance = 9f;
        somMotorJato.maxDistance = 150f;
    }

    void Update()
    {
        if (motoresEmRota)
        {
            ManobraEInterpolacaoCurva();
            SensoriamentoDoMotor();
            GarantirAudioMotor();
        }
        else
        {
            DesligarVFX();
        }
    }

    public void OrdenarAtaqueOuPatrulha(Vector3 novaCoordenadaAlvo)
    {
        alvoGPS = novaCoordenadaAlvo;
        motoresEmRota = true;
        
        if (somMotorJato != null)
        {
            somMotorJato.volume = 1f;
            if (!somMotorJato.isPlaying) somMotorJato.Play();
        }
        
        for (int i = 0, count = posQueimadores.Length; i < count; i++)
        {
            ParticleSystem fogo = posQueimadores[i];
            if (fogo != null && !fogo.isPlaying) fogo.Play();
        }
    }

    public void AbortarVoo()
    {
        motoresEmRota = false;
        velocidadeAtual = 0f;
        if (modeloMecanicoVisual != null) modeloMecanicoVisual.localRotation = Quaternion.identity; 
    }

    private void ManobraEInterpolacaoCurva()
    {
        float dt = Time.deltaTime;
        velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeMaxima, aceleracaoMotor * dt);

        Vector3 retaAteAlvo = alvoGPS - thisRoot.position;
        // sqrMagnitude evita sqrt — mais performático que magnitude
        if (retaAteAlvo.sqrMagnitude < 25f) return; // 5² = 25

        Vector3 upRef = Mathf.Abs(Vector3.Dot(retaAteAlvo.normalized, Vector3.up)) > 0.99f ? thisRoot.up : Vector3.up;
        Quaternion olharMundoDesejado = Quaternion.LookRotation(retaAteAlvo, upRef);
        float anguloPressaoLateralY = Vector3.SignedAngle(thisRoot.forward, retaAteAlvo, Vector3.up);
        thisRoot.rotation = Quaternion.RotateTowards(thisRoot.rotation, olharMundoDesejado, taxaDeGiroLeme * dt);
        thisRoot.position += thisRoot.forward * (velocidadeAtual * dt);

        if (modeloMecanicoVisual != null)
        {
            float inclinacaoAlvoZ = Mathf.Clamp(anguloPressaoLateralY * -1.8f, -asaBankingMaximo, asaBankingMaximo);
            float inclinacaoAlvoX = Mathf.Clamp(retaAteAlvo.y * -2.0f, -arfagemPitchMaxima, arfagemPitchMaxima);
            giroLateralRoll = Mathf.Lerp(giroLateralRoll, inclinacaoAlvoZ, dt * sensibilidadeAerodinamica);
            empinadaPitch = Mathf.Lerp(empinadaPitch, inclinacaoAlvoX, dt * _sensibilidadePitchCache);
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, 0f, giroLateralRoll);
        }
    }

    private void SensoriamentoDoMotor()
    {
        if (somMotorJato != null)
            somMotorJato.pitch = Mathf.Lerp(0.6f, 1.8f, velocidadeAtual / velocidadeMaxima);
    }

    private void GarantirAudioMotor()
    {
        if (somMotorJato == null)
        {
            return;
        }

        if (somMotorJato.clip == null)
        {
            AudioClip clip = BuscarAudioClipNoObjeto();
            if (clip != null)
            {
                somMotorJato.clip = clip;
            }
        }

        if (somMotorJato.clip != null && !somMotorJato.isPlaying)
        {
            somMotorJato.Play();
        }
    }

    private AudioClip BuscarAudioClipNoObjeto()
    {
        AudioSource[] fontes = GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < fontes.Length; i++)
        {
            AudioSource fonte = fontes[i];
            if (fonte != null && fonte.clip != null)
            {
                return fonte.clip;
            }
        }

        return null;
    }

    private void DesligarVFX()
    {
        if (somMotorJato != null && somMotorJato.isPlaying)
        {
            somMotorJato.volume = Mathf.Lerp(somMotorJato.volume, 0f, Time.deltaTime * 2f);
            if (somMotorJato.volume <= 0.05f) somMotorJato.Stop();
        }
        for (int i = 0, count = posQueimadores.Length; i < count; i++)
        {
            ParticleSystem fogo = posQueimadores[i];
            if (fogo != null && fogo.isPlaying) fogo.Stop();
        }
    }
}
