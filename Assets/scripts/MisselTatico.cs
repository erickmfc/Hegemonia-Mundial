using UnityEngine;

public class MisselTatico : MonoBehaviour
{
    [Header("Motores")]
    public float velocidade = 40f; // Mais rápido que o ICBM
    public float velocidadeDeGiro = 60f; // Mais ágil
    
    [Header("Trajetória")]
    public float alturaDoArco = 20f; // Arco menor, mais tático
    public float atrasoParaVirar = 0.5f; // Sai do tubo e vira rápido
    public bool usarRotacaoInicialDoLancador = true;

    [Header("Explosão")]
    public GameObject efeitoExplosao;
    public AudioClip somExplosao;
    public float raioDeDano = 10f; // Explosão concentrada
    public int dano = 150; // Dano numérico direto
    public float escalaExplosao = 3.0f;
    public float forcaImpacto = 500f;

    // Internas
    private Vector3 alvo;
    private bool lancado = false;
    private float tempoDeVida = 0;
    private Quaternion rotacaoAlvo;
    private bool temAlvo = false;

    // Configura o alvo e inicia
    public void IniciarLancamento(Vector3 pontoAlvo)
    {
        alvo = pontoAlvo;
        lancado = true;
        temAlvo = true;
        tempoDeVida = 0;

        // Se NÃO usar rotação do lançador, força UP (estilo VLS)
        if (!usarRotacaoInicialDoLancador)
        {
            transform.rotation = Quaternion.LookRotation(Vector3.up);
        }

        PrepararFisica();
        
        // Destroi automaticamente se ficar voando pra sempre
        Destroy(gameObject, 15f); 
    }

    void PrepararFisica()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; 
            rb.useGravity = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            Invoke("ReativarColisao", 0.5f); // Reativa rápido
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

        // 1. MOVIMENTO (Sempre em frente)
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime);

        // 2. GUIAGEM
        if (tempoDeVida < atrasoParaVirar)
        {
            // Fase de Decolagem: Segue reto na direção que saiu do tubo
            // Não força rotação, apenas mantém a inércia direcional
        }
        else if (temAlvo)
        {
            // Fase de Cruzeiro/Mergulho
            
            // Calcula direção para o alvo
             Vector3 direcaoParaAlvo = (alvo - transform.position).normalized;
             float distancia = Vector3.Distance(transform.position, alvo);

            // Perfil de Voo
            Vector3 pontoDeMira = alvo;
            
            // Se ainda está longe, tenta virar para o alvo + arco
            // Se está muito perto, vai direto (Dive)
            if (distancia > 15f)
            {
                // Faz "Lead" ou curva suave parabólica
                // Adiciona um offset em Y baseado na distância para manter altura
                // Mas diferentemente do ICBM, o Tático voa mais "direto" depois do arco inicial
                // Vamos simplificar: Olha pro alvo, mas se estiver subindo, deixa subir um pouco mais.
            }

            Quaternion targetRot = Quaternion.LookRotation(direcaoParaAlvo);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, velocidadeDeGiro * Time.deltaTime);
        }

        // 3. DETECÇÃO DE IMPACTO (Manual por Distância se colisão falhar ou for muito rápido)
        if (tempoDeVida > 0.5f)
        {
            if (Vector3.Distance(transform.position, alvo) < 2.0f)
            {
                Explodir();
            }
        }
    }

    // Colisão Física (Caso bata em algo no caminho, tipo um prédio)
    void OnTriggerEnter(Collider other)
    {
        if (!lancado || tempoDeVida < 0.2f) return; // Ignora o próprio lançador
        Explodir();
    }

    void Explodir()
    {
        // Visual
        if (efeitoExplosao != null)
        {
            GameObject fx = Instantiate(efeitoExplosao, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * escalaExplosao;
            Destroy(fx, 3f);
        }

        // Som
        if (somExplosao != null)
        {
            AudioSource.PlayClipAtPoint(somExplosao, transform.position);
        }
        
        // Dano em Área
        Collider[] hits = Physics.OverlapSphere(transform.position, raioDeDano);
        foreach(var h in hits)
        {
            // Causa Dano
            SistemaDeDanos vida = h.GetComponent<SistemaDeDanos>();
            if (vida != null)
            {
                vida.ReceberDano(dano);
            }

            // Física
            Rigidbody rb = h.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(forcaImpacto, transform.position, raioDeDano);
            }
        }

        Destroy(gameObject);
    }
}
