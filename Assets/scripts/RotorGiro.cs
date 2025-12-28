using UnityEngine;

public class RotorGiro : MonoBehaviour
{
    public float velocidade = 800f;
    public enum Eixo { Y, Z, X }
    public Eixo eixoGiro = Eixo.Y;

    void Update()
    {
        // Gira a peça constantemente
        if (eixoGiro == Eixo.Y)
            transform.Rotate(0, velocidade * Time.deltaTime, 0);
        else if (eixoGiro == Eixo.Z)
            transform.Rotate(0, 0, velocidade * Time.deltaTime);
        else
            transform.Rotate(velocidade * Time.deltaTime, 0, 0);
    }
}
