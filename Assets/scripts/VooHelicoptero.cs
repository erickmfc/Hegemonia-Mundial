using UnityEngine;
using UnityEngine.AI;

public class VooHelicoptero : MonoBehaviour
{
    [Header("Configuração")]
    public Transform modeloVisual; // O modelo 3D do helicóptero (Filho)
    public float alturaDeVoo = 6.0f; // Altura que ele vai subir
    public float suavidade = 2.0f;
    
    [Header("Animação")]
    public float forcaBalanco = 0.5f; // Quanto ele sobe e desce parado
    public float inclinacaoFrente = 15f; // Quanto ele inclina ao andar

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

    void Update()
    {
        if (modeloVisual == null) return;

        // 1. ALTREZA (Levanta o helicóptero do chão suavemente)
        // Efeito "Bobbing" (flutuar para cima e para baixo)
        float oscilacao = Mathf.Sin(Time.time * 2f) * forcaBalanco;
        float alturaAlvo = alturaDeVoo + oscilacao;

        Vector3 novaPosicao = modeloVisual.localPosition;
        novaPosicao.y = Mathf.Lerp(novaPosicao.y, alturaAlvo, Time.deltaTime * suavidade);
        modeloVisual.localPosition = novaPosicao;

        // 2. INCLINAÇÃO (Inclina o nariz quando voa para frente)
        if (agente != null)
        {
            float velocidade = agente.velocity.magnitude;
            float anguloAlvo = 0f;

            // Se estiver andando rápido (> 0.5f), inclina
            if (velocidade > 0.5f)
            {
                anguloAlvo = inclinacaoFrente;
            }

            // Aplica a rotação no eixo X (frente/trás) suavemente
            Quaternion rotacaoAtual = modeloVisual.localRotation;
            Quaternion rotacaoAlvo = Quaternion.Euler(anguloAlvo, rotacaoAtual.eulerAngles.y, 0);
            
            modeloVisual.localRotation = Quaternion.Lerp(rotacaoAtual, rotacaoAlvo, Time.deltaTime * suavidade);
        }
    }
}
