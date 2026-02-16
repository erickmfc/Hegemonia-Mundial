using UnityEngine;

public class EstabilizadorNavio : MonoBehaviour {
    [Tooltip("Força de estabilização (Antygaviti). Aumente se o barco virar.")]
    public float antygaviti = 5.0f; // Esse é o valor do seu Inspector
    private Rigidbody rb;

    void Start() {
        rb = GetComponent<Rigidbody>();
    }

    // A mágica acontece no FixedUpdate (parte física)
    void FixedUpdate() {
        if (rb == null) return;

        // 1. O Antygaviti puxa o navio para cima para ele ficar reto
        // Ele compara a rotação do navio com a rotação "em pé" (Vector3.up)
        Vector3 torqueParaCorrigir = Vector3.Cross(transform.up, Vector3.up);

        // 2. Aplica a força usando o valor que você digitou no Inspector
        rb.AddTorque(torqueParaCorrigir * antygaviti * 10f);

        // 3. Freio de balanço (impede que ele pareça uma gelatina)
        rb.angularVelocity *= 0.95f; 
    }
}
