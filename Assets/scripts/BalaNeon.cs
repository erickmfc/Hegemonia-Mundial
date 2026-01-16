using UnityEngine;

public class BalaNeon : MonoBehaviour
{
    public float velocidade = 50f; // Velocidade da bala
    public float tempoDeVida = 3f; // Para a bala não voar eternamente e pesar o jogo

    void Start()
    {
        // Destrói a bala automaticamente após 3 segundos para economizar memória
        Destroy(gameObject, tempoDeVida);
    }

    void Update()
    {
        // Move a bala para frente (na direção que ela foi instanciada)
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime);
    }
    
    // Opcional: Se a bala bater em algo, ela some
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Inimigo"))
        {
            // Aqui você pode adicionar lógica de dano depois
            Destroy(gameObject);
        }
    }
}
