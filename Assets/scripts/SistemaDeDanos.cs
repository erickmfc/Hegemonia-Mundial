using UnityEngine;

public class SistemaDeDanos : MonoBehaviour
{
    [Header("Configuração da Unidade")]
    public int vidaMaxima = 100; // Cada unidade pode ter um valor diferente
    private int vidaAtual;

    [Header("Efeitos Visuais (Arraste os GameObjects aqui)")]
    public GameObject fxFumacaLeve;   // Dano Leve (70%)
    public GameObject fxFumacaEscura; // Dano Médio (40%)
    public GameObject fxFogoCritico;  // Dano Crítico (15%)
    
    [Header("Morte")]
    public GameObject fxExplosaoFinal; // O boom quando morre

    void Start()
    {
        vidaAtual = vidaMaxima;
        
        // Garante que os efeitos comecem desligados para não ter erro
        DesativarEfeitos();
    }

    public void ReceberDano(int dano)
    {
        vidaAtual -= dano;
        
        // Cálculo matemático da porcentagem (0.0 a 1.0)
        float porcentagemVida = (float)vidaAtual / vidaMaxima;

        Debug.Log(gameObject.name + " Vida: " + vidaAtual + " (" + (porcentagemVida*100) + "%)");

        VerificarDanos(porcentagemVida);

        if (vidaAtual <= 0)
        {
            Morrer();
        }
    }

    void VerificarDanos(float porcentagem)
    {
        // Lógica de Escala de Dano
        
        // Se vida menor que 70%, liga fumaça leve
        if (porcentagem <= 0.7f && fxFumacaLeve != null)
        {
            fxFumacaLeve.SetActive(true);
        }

        // Se vida menor que 40%, liga fumaça escura (e pode desligar a leve se quiser)
        if (porcentagem <= 0.4f && fxFumacaEscura != null)
        {
            if(fxFumacaLeve) fxFumacaLeve.SetActive(false); // Troca a leve pela escura
            fxFumacaEscura.SetActive(true);
        }

        // Se vida menor que 15%, pega FOGO!
        if (porcentagem <= 0.15f && fxFogoCritico != null)
        {
            fxFogoCritico.SetActive(true); // Fogo soma com a fumaça escura
        }
    }

    void Morrer()
    {
        // Cria a explosão final
        if (fxExplosaoFinal != null)
        {
            Instantiate(fxExplosaoFinal, transform.position, transform.rotation);
        }

        // Destroi a unidade
        Destroy(gameObject);
    }

    void DesativarEfeitos()
    {
        if (fxFumacaLeve) fxFumacaLeve.SetActive(false);
        if (fxFumacaEscura) fxFumacaEscura.SetActive(false);
        if (fxFogoCritico) fxFogoCritico.SetActive(false);
    }
}
