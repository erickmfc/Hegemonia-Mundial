using UnityEngine;

/// <summary>
/// SINALIZADOR MANUAL DO COMANDANTE
/// Coloque este script em objetos vazios pelo mapa para guiar a IA.
/// </summary>
public class SinalizadorIA : MonoBehaviour
{
    [Header("O que este ponto marca?")]
    public bool istoE_Agua = false;
    public bool istoE_Terra = false;

    void Start()
    {
        // Assim que o jogo comeca, este objeto avisa as IAs estrategicas da cena.
        IA_Suprema ia = Object.FindFirstObjectByType<IA_Suprema>();
        IA_Dominadora iaDominadora = Object.FindFirstObjectByType<IA_Dominadora>();

        if (ia != null)
        {
            if (istoE_Agua)
                ia.ReceberSinalizador(this.transform.position, true);
            else if (istoE_Terra)
                ia.ReceberSinalizador(this.transform.position, false);
        }

        if (iaDominadora != null)
        {
            if (istoE_Agua)
                iaDominadora.ReceberSinalizador(this.transform.position, true);
            else if (istoE_Terra)
                iaDominadora.ReceberSinalizador(this.transform.position, false);
        }
    }

    // Desenha uma esfera colorida no Unity (só você vê, não aparece no jogo) para não perder o objeto de vista!
    void OnDrawGizmos()
    {
        if (istoE_Agua)
        {
            Gizmos.color = new Color(0, 0.5f, 1f, 0.5f); // Azul para a Água
            Gizmos.DrawSphere(transform.position, 15f);
        }
        else if (istoE_Terra)
        {
            Gizmos.color = new Color(0.5f, 0.25f, 0f, 0.5f); // Castanho para a Terra
            Gizmos.DrawSphere(transform.position, 15f);
        }
    }
}