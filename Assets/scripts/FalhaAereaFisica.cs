using UnityEngine;

public class FalhaAereaFisica : MonoBehaviour
{
    private Rigidbody corpo;
    private SistemaDeDanos sistemaDanos;
    private bool perfilHelicoptero;
    private bool impactoAplicado;
    private float velocidadeCruzeiro;
    private float empuxoDescida;
    private float arrastoLinear;
    private float giroRoll;
    private float giroPitch;
    private Vector3 direcaoFrente;

    public static void Ativar(GameObject alvo, Rigidbody rb, float velocidadeFrente, float descidaInicial, bool usarPerfilHelicoptero, SistemaDeDanos danos = null)
    {
        if (alvo == null)
        {
            return;
        }

        FalhaAereaFisica queda = alvo.GetComponent<FalhaAereaFisica>();
        if (queda == null)
        {
            queda = alvo.AddComponent<FalhaAereaFisica>();
        }

        queda.Configurar(rb, velocidadeFrente, descidaInicial, usarPerfilHelicoptero, danos);
    }

    private void Configurar(Rigidbody rb, float velocidadeFrente, float descidaInicial, bool usarPerfilHelicoptero, SistemaDeDanos danos)
    {
        corpo = rb != null ? rb : GetComponent<Rigidbody>();
        if (corpo == null)
        {
            corpo = gameObject.AddComponent<Rigidbody>();
        }

        sistemaDanos = danos;
        perfilHelicoptero = usarPerfilHelicoptero;
        impactoAplicado = false;
        velocidadeCruzeiro = Mathf.Max(12f, velocidadeFrente);
        empuxoDescida = Mathf.Max(4f, descidaInicial);
        arrastoLinear = perfilHelicoptero ? 0.35f : 0.16f;
        giroRoll = perfilHelicoptero ? 55f : 90f;
        giroPitch = perfilHelicoptero ? 22f : 40f;

        corpo.isKinematic = false;
        corpo.useGravity = true;
        corpo.linearDamping = arrastoLinear;
        corpo.angularDamping = 0.35f;
        corpo.constraints = RigidbodyConstraints.None;

        Vector3 velocidadeAtual = corpo.linearVelocity;
        Vector3 frente = transform.forward * velocidadeCruzeiro;

        if (velocidadeAtual.sqrMagnitude < 4f)
        {
            velocidadeAtual = frente;
        }
        else
        {
            Vector3 horizontalAtual = Vector3.ProjectOnPlane(velocidadeAtual, Vector3.up);
            if (horizontalAtual.sqrMagnitude < frente.sqrMagnitude * 0.15f)
            {
                velocidadeAtual = horizontalAtual + frente * 0.6f + Vector3.up * velocidadeAtual.y;
            }
        }

        velocidadeAtual += Vector3.down * empuxoDescida;
        direcaoFrente = Vector3.ProjectOnPlane(velocidadeAtual, Vector3.up);
        if (direcaoFrente.sqrMagnitude < 0.01f)
        {
            direcaoFrente = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        direcaoFrente = direcaoFrente.sqrMagnitude > 0.01f ? direcaoFrente.normalized : Vector3.forward;
        corpo.linearVelocity = velocidadeAtual;
        corpo.angularVelocity = new Vector3(0.45f, 0.2f, perfilHelicoptero ? 0.35f : 0.7f);

        enabled = true;
    }

    private void FixedUpdate()
    {
        if (corpo == null)
        {
            corpo = GetComponent<Rigidbody>();
            if (corpo == null)
            {
                AplicarImpactoFinal();
                return;
            }
        }

        if (!impactoAplicado)
        {
            float velocidadeMinimaFrente = Mathf.Max(
                perfilHelicoptero ? velocidadeCruzeiro * 0.45f : velocidadeCruzeiro * 0.70f,
                perfilHelicoptero ? 12f : 28f);

            Vector3 velocidade = corpo.linearVelocity;
            Vector3 velocidadeHorizontal = Vector3.ProjectOnPlane(velocidade, Vector3.up);
            float velocidadeNaFrente = Vector3.Dot(velocidadeHorizontal, direcaoFrente);
            if (velocidadeNaFrente < velocidadeMinimaFrente)
            {
                velocidadeHorizontal += direcaoFrente * (velocidadeMinimaFrente - velocidadeNaFrente);
                velocidade = velocidadeHorizontal + Vector3.up * velocidade.y;
                corpo.linearVelocity = velocidade;
            }

            corpo.AddForce(direcaoFrente * (perfilHelicoptero ? 3.5f : 6f), ForceMode.Acceleration);
            transform.Rotate(Vector3.forward, giroRoll * Time.fixedDeltaTime, Space.Self);
            transform.Rotate(Vector3.right, giroPitch * Time.fixedDeltaTime, Space.Self);
        }

        if (transform.position.y <= 1.5f)
        {
            AplicarImpactoFinal();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        if (collision.relativeVelocity.sqrMagnitude >= 9f || transform.position.y <= 3f)
        {
            AplicarImpactoFinal();
        }
    }

    private void AplicarImpactoFinal()
    {
        if (impactoAplicado)
        {
            return;
        }

        impactoAplicado = true;

        if (sistemaDanos != null)
        {
            sistemaDanos.ReceberDano(Mathf.Max(9999f, sistemaDanos.vidaMaxima * 2f));
        }
        else
        {
            Destroy(gameObject, 0.05f);
        }

        enabled = false;
    }
}
