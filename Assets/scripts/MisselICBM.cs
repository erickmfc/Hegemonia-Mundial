using UnityEngine;

public class MisselICBM : MonoBehaviour
{
    [Header("Motores")]
    public float velocidade = 25f;
    public float velocidadeDeGiro = 30f; // Quanto menor, mais aberta a curva (Ex: 20 a 40)
    
    [Header("Trajetória")]
    public float alturaDoArco = 60f; // Altura máxima do voo
    public float atrasoParaVirar = 1.5f; // Tempo que sobe reto antes de mirar

    [Header("Explosão")]
    public GameObject efeitoExplosao;
    public AudioClip somExplosao; // Arraste o áudio aqui
    public float raioDeDano = 20f;
    public float escalaExplosao = 8.0f;
    public float distanciaSom = 500f; // Distância máxima para ouvir

    // Internas
    private Vector3 alvo;
    private bool lancado = false;
    private float tempoDeVida = 0;
    private Quaternion rotacaoAlvo;

    public void IniciarLancamento(Vector3 pontoAlvo)
    {
        alvo = pontoAlvo;
        lancado = true;
        tempoDeVida = 0;

        // 1. Aponta o míssil para CIMA imediatamente ao nascer
        transform.rotation = Quaternion.LookRotation(Vector3.up);

        // 2. Desativa a física para o script controlar o voo 100%
        PrepararFisica();
    }

    void PrepararFisica()
    {
        // Garante que a gravidade não puxe o míssil para baixo
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Desliga a simulação física, liga o modo "roteirizado"
            rb.useGravity = false;
        }

        // Garante que ele não bata nas paredes do silo ao sair
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false; // Desliga colisão na saída
            Invoke("ReativarColisao", 2.0f); // Liga de novo depois de 2 segundos (no ar)
        }
    }

    void ReativarColisao()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    void Update()
    {
        if (!lancado) return;

        tempoDeVida += Time.deltaTime;

        // --- MOVIMENTO ---
        // O míssil SEMPRE vai para onde o nariz (Azul) aponta
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime);

        // --- ROTAÇÃO (GUIAGEM) ---
        
        // Fase 1: Decolagem Vertical (Espera X segundos)
        if (tempoDeVida < atrasoParaVirar)
        {
            // Apenas sobe reto (já definimos a rotação inicial como UP)
            // Se ele estiver torto, força UP suavemente
            Quaternion subir = Quaternion.LookRotation(Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, subir, 180 * Time.deltaTime);
        }
        // Fase 2: Curva para o Alvo
        else
        {
            // Calcula direção para o alvo, mas adiciona altura para fazer o arco
            Vector3 direcaoFinal = (alvo - transform.position).normalized;
            
            // Truque da Parábola: Se estiver longe, olha mais pra cima. Se perto, olha pro chão.
            float distancia = Vector3.Distance(transform.position, alvo);
            
            // Ponto fictício no céu para onde ele deve olhar agora
            Vector3 pontoDeMira = alvo;
            if (distancia > 20f) // Se está longe
            {
                pontoDeMira.y += alturaDoArco * (distancia / 100f); // Mira alto
            }

            Vector3 direcaoGuia = (pontoDeMira - transform.position).normalized;
            rotacaoAlvo = Quaternion.LookRotation(direcaoGuia);

            // Gira suavemente em direção à mira
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoAlvo, velocidadeDeGiro * Time.deltaTime);
        }

        // --- DETECÇÃO DE IMPACTO ---
        // Se estiver caindo (pitch > 0) e perto do chão
        if (tempoDeVida > 2.0f && transform.position.y < alvo.y + 1f)
        {
            Explodir();
        }
    }

    void Explodir()
    {
        // 1. Cria o Visual
        if (efeitoExplosao != null)
        {
            GameObject fx = Instantiate(efeitoExplosao, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * escalaExplosao;
        }

        // 2. Cria o Som
        if (somExplosao != null)
        {
            GameObject audioObj = new GameObject("SomExplosaoICBM");
            audioObj.transform.position = transform.position;
            
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = somExplosao;
            source.volume = 1.0f;
            source.spatialBlend = 1.0f;
            source.minDistance = 10f;
            source.maxDistance = distanciaSom;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();

            Destroy(audioObj, somExplosao.length + 0.5f);
        }
        
        // 3. Aplica Dano e Física (O Melhor dos Dois Mundos)
        Collider[] hits = Physics.OverlapSphere(transform.position, raioDeDano);
        foreach(var h in hits)
        {
            // A. Dano no Sistema (Unidades/Prédios)
            h.GetComponent<SistemaDeDanos>()?.ReceberDano(9999);

            // B. Física de Explosão (Empurrar Destroços/Unidades)
            Rigidbody rb = h.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Força de 2000 (padrão) para jogar longe
                rb.AddExplosionForce(2000f, transform.position, raioDeDano, 3.0f);

                // C. Destruir objetos de cenário "soltos" que não tenham script de vida
                if (h.GetComponent<SistemaDeDanos>() == null)
                {
                    Destroy(h.gameObject, 0.5f); // Dá meio segundo para voar com o impacto antes de sumir
                }
            }
        }

        Destroy(gameObject);
    }
}
