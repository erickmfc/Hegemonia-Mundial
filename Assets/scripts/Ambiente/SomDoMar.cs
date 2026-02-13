using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SomDoMar : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Arraste sua câmera aqui (ou deixe vazio para pegar a MainCamera)")]
    public Transform cameraPrincipal;

    [Tooltip("Arraste aqui o Áudio do Mar")]
    public AudioClip clipeDoMar;

    [Header("Ajustes de Altura")]
    public float nivelDaAgua = 0f; // Altura Y do mar (geralmente 0)
    public float alturaMinima = 5f; // Altura onde o som é MÁXIMO (bem perto da água)
    public float alturaMaxima = 80f; // Altura onde o som zera (câmera muito alta)
    
    [Range(0, 1f)] 
    public float volumeGeral = 0.8f;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Configurações automáticas do AudioSource
        audioSource.clip = clipeDoMar;
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.spatialBlend = 0f; // 2D (Vamos controlar o volume manualmente por script, melhor para ambiente)
        audioSource.dopplerLevel = 0f; // Sem efeito doppler
        
        if (cameraPrincipal == null && Camera.main != null)
            cameraPrincipal = Camera.main.transform;

        if (audioSource.clip != null)
            audioSource.Play();
    }

    void Update()
    {
        if (cameraPrincipal == null) return;

        // 1. O som segue a câmera no X e Z, mas fica preso na altura da água
        // Isso cria a ilusão de que o mar está "em todo lugar abaixo de você"
        transform.position = new Vector3(cameraPrincipal.position.x, nivelDaAgua, cameraPrincipal.position.z);

        // 2. Calcular Volume baseado na Altura da Câmera
        float alturaAtual = cameraPrincipal.position.y;
        
        // Fórmula de Interpolação (Lerp Inverso)
        // Quanto mais perto da alturaMinima, mais perto de 1.
        // Quanto mais perto da alturaMaxima, mais perto de 0.
        float t = Mathf.InverseLerp(alturaMaxima, alturaMinima, alturaAtual);
        
        // Aplica o volume com uma curva suave (quadrática) para ficar natural
        audioSource.volume = t * t * volumeGeral;
    }
}
