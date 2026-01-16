using UnityEngine;
using UnityEngine.AI;

public class VooHelicoptero : MonoBehaviour
{
    [Header("Configuração")]
    public Transform modeloVisual; // O modelo 3D do helicóptero (Filho)
    public float alturaVisualExtra = 0.0f; // Ajuste fino apenas (Default 0 agora que voa fisicamente)
    public float suavidade = 2.0f;
    
    [Header("Animação")]
    public float forcaBalanco = 0.2f; // Balanço leve
    public float inclinacaoFrente = 15f; 

    private NavMeshAgent agente;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        
        // Se não arrastou nada, tenta achar o primeiro filho
        if (modeloVisual == null && transform.childCount > 0)
        {
            modeloVisual = transform.GetChild(0);
        }
    }

    // Variável para receber velocidade do ControleUnidade 
    private float velocidadeExterna = 0f;

    public void SetVelocidadeAtual(float v)
    {
        velocidadeExterna = v;
    }

    void Update()
    {
        if (modeloVisual == null) return;

        // 1. ANIMAÇÃO DE FLUTUAÇÃO (Bobbing)
        // Agora somamos a 0 (ou ajuste fino), pois o Pai já está na altura de voo real
        float oscilacao = Mathf.Sin(Time.time * 2f) * forcaBalanco;
        float alturaLocalAlvo = alturaVisualExtra + oscilacao;

        Vector3 novaPosicao = modeloVisual.localPosition;
        novaPosicao.y = Mathf.Lerp(novaPosicao.y, alturaLocalAlvo, Time.deltaTime * suavidade);
        modeloVisual.localPosition = novaPosicao;

        // 2. INCLINAÇÃO
        float velocidade = velocidadeExterna;
        if (agente != null && agente.isActiveAndEnabled)
        {
            velocidade = agente.velocity.magnitude;
        }

        float anguloAlvo = 0f;
        if (velocidade > 0.5f)
        {
            anguloAlvo = inclinacaoFrente;
        }

        // Aplica a rotação (Mantendo a rotação Y original do modelo se houver, mas geralmente local é 0)
        // Cuidado: Quaternion.Euler(x, y, z) substitui tudo.
        // Vamos manter o Y local atual caso ele tenha animações girando, mas geralmente o Pai gira.
        Quaternion rotacaoAlvo = Quaternion.Euler(anguloAlvo, 0, 0); // Local rotation assumindo frente Z
        
        modeloVisual.localRotation = Quaternion.Slerp(modeloVisual.localRotation, rotacaoAlvo, Time.deltaTime * suavidade);
    }
}
