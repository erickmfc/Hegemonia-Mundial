using UnityEngine;

public class BombaICNU : MonoBehaviour
{
    [Header("Configurações da Explosão")]
    public float raioDaExplosao = 50f; // Tamanho da área afetada
    public float forcaDoVento = 2000f; // A força do impacto "physics"
    public GameObject efeitoVisualExplosao; // Aqui colocaremos a partícula (War FX)

    // Quando a bomba toca em algo (Chão ou Inimigo)
    void OnCollisionEnter(Collision collision)
    {
        Explodir();
    }

    void Explodir()
    {
        // 1. Criar o efeito visual (Fogo/Fumaça) no local da batida
        if (efeitoVisualExplosao != null)
        {
            Instantiate(efeitoVisualExplosao, transform.position, transform.rotation);
        }

        // 2. Detectar tudo o que está dentro do raio da esfera de explosão
        Collider[] objetosAtingidos = Physics.OverlapSphere(transform.position, raioDaExplosao);

        foreach (Collider objeto in objetosAtingidos)
        {
            // A. EMPURRAR (O Vento do Impacto)
            Rigidbody rb = objeto.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Adiciona força de explosão: Força, Posição da Bomba, Raio, Empurrão pra cima
                rb.AddExplosionForce(forcaDoVento, transform.position, raioDaExplosao, 3.0f);
            }

            // B. DESTRUIR (Dano Infinito)
            // Se o objeto tiver uma tag de inimigo ou destrutível, ele some.
            // CUIDADO: Isso destrói tudo, até suas unidades se estiverem perto!
            if (objeto.CompareTag("Inimigo") || objeto.CompareTag("Destrutivel"))
            {
                Destroy(objeto.gameObject, 0.5f); // Destrói após 0.5 segundos para dar tempo de voar com o vento
            }
        }

        // 3. Destruir a própria bomba para ela não ficar no cenário
        Destroy(gameObject);
    }
}
