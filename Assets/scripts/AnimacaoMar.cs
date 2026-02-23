using UnityEngine;

public class AnimacaoMar : MonoBehaviour
{
    [Header("Configurações de Movimento")]
    [Tooltip("Velocidade que a água se move para os lados")]
    public float velocidadeX = 0.1f;
    
    [Tooltip("Velocidade que a água se move para frente/trás")]
    public float velocidadeY = 0.05f;

    private Renderer renderizadorDaAgua;

    void Start()
    {
        // Pega o material visual do mar assim que o jogo começa
        renderizadorDaAgua = GetComponent<Renderer>();
    }

    void Update()
    {
        // Faz o cálculo contínuo para mover a textura da água baseando-se no tempo do jogo
        float movimentoX = Time.time * velocidadeX;
        float movimentoY = Time.time * velocidadeY;

        // Aplica o "deslizamento" na imagem do mar, criando a ilusão de ondas se movendo
        renderizadorDaAgua.material.mainTextureOffset = new Vector2(movimentoX, movimentoY);
    }
}
