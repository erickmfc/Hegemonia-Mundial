using UnityEngine;

public class Projetil : MonoBehaviour
{
    [Header("Configuração da Bala")]
    public float velocidade = 50f;
    public int dano = 10;
    public float tempoDeVida = 3f;
    public GameObject efeitoExplosao; // O visual do impacto (se tiver)

    private Vector3 direcao; // Direção fixa ao ser disparado
    private bool usaDirecaoCustom = false;
    private GameObject dono; // Quem atirou essa bala (para não se auto-atacar)
    private float tempoSeguranca = 0.1f; // Tempo antes de poder causar dano (evita auto-hit)
    private float tempoNascimento;

    /// <summary>
    /// Define a direção fixa que o projétil vai seguir (usado pelas torretas)
    /// </summary>
    public void SetDirecao(Vector3 novaDirecao)
    {
        direcao = novaDirecao.normalized;
        transform.forward = direcao;
        usaDirecaoCustom = true;
    }

    /// <summary>
    /// Define quem atirou essa bala (para não causar dano em si mesmo)
    /// </summary>
    public void SetDono(GameObject quemAtirou)
    {
        dono = quemAtirou;
    }

    void Start()
    {
        tempoNascimento = Time.time;
        
        // Destrói a bala depois de X segundos para não travar o jogo
        Destroy(gameObject, tempoDeVida);
    }

    void Update()
    {
        Vector3 moveDirection = usaDirecaoCustom ? direcao : transform.forward;
        
        // Move a bala
        transform.position += moveDirection * velocidade * Time.deltaTime;

        // --- SISTEMA DE DETECÇÃO (RAYCAST) ---
        // Lança um raio invisível para frente antes de andar
        float distanciaFrame = velocidade * Time.deltaTime;
        RaycastHit toque;

        if (Physics.Raycast(transform.position, moveDirection, out toque, distanciaFrame))
        {
            // Bateu em algo! Vamos ver o que é.
            VerificarImpacto(toque.collider.gameObject);
        }
    }

    // Caso o Raycast falhe e a física normal detecte (Segurança Extra)
    void OnTriggerEnter(Collider other)
    {
        VerificarImpacto(other.gameObject);
    }

    void VerificarImpacto(GameObject alvo)
    {
        // PROTEÇÃO 1: Tempo de segurança (nos primeiros 0.1s não causa dano)
        if (Time.time - tempoNascimento < tempoSeguranca)
        {
            return; // Ignora colisões muito cedo
        }

        // PROTEÇÃO 2: Não ataca o próprio dono
        if (dono != null)
        {
            // Verifica se é o próprio dono ou um filho/parte do dono
            if (alvo == dono || alvo.transform.IsChildOf(dono.transform) || 
                (alvo.transform.root == dono.transform.root))
            {
                return; // Ignora - é o próprio atirador!
            }
        }

        // PROTEÇÃO 3: Ignora outros projéteis
        if (alvo.GetComponent<Projetil>() != null)
        {
            return;
        }

        // Tenta achar o script de Vida no objeto que bateu (ou no pai)
        SistemaDeDanos vidaAlvo = alvo.GetComponent<SistemaDeDanos>();
        if (vidaAlvo == null)
        {
            vidaAlvo = alvo.GetComponentInParent<SistemaDeDanos>();
        }

        // Se o alvo tiver vida...
        if (vidaAlvo != null)
        {
            // PROTEÇÃO 4: Verifica se não é o dono pelo SistemaDeDanos
            if (dono != null && vidaAlvo.gameObject == dono)
            {
                return;
            }

            vidaAlvo.ReceberDano(dano); // Causa o dano!
            Debug.Log("Bala acertou e causou dano em: " + vidaAlvo.gameObject.name);
        }

        // Cria explosão (se tiver configurado)
        if (efeitoExplosao != null)
        {
            Instantiate(efeitoExplosao, transform.position, Quaternion.identity);
        }

        // Destrói a bala (ela cumpriu sua missão)
        Destroy(gameObject);
    }
}
