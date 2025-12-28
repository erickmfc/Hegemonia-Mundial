using UnityEngine;

public class AtributosPredio : MonoBehaviour
{
    public int vidaMaxima = 500;
    public int vidaAtual;

    void Start()
    {
        vidaAtual = vidaMaxima;
    }

    public void ReceberDano(int dano)
    {
        vidaAtual -= dano;
        if (vidaAtual <= 0)
        {
            DestruirPredio();
        }
    }

    void DestruirPredio()
    {
        // Aqui você pode colocar uma explosão ou som antes de sumir
        Destroy(gameObject);
    }
}
