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
        // Legado removido: este marcador agora é apenas visual.
        // Mantido para não quebrar cenas antigas que ainda contenham o componente.
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
