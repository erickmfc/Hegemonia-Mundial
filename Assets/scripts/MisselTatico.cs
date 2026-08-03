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
    private bool explodiu = false;
    private readonly Collider[] bufferExplosao = new Collider[96];

    // Configura o alvo e inicia
    public void IniciarLancamento(Vector3 pontoAlvo)
    {
        alvo = pontoAlvo;
        lancado = true;
        temAlvo = true;
        explodiu = false;
        tempoDeVida = 0;
        CancelInvoke(nameof(ReativarColisao));
        CancelInvoke(nameof(AutodestruirPorTempo));

        // Se usar rotação inicial, NÃO mexemos na rotação (confia no Instantiate do Lançador).
        // Se NÃO usar (ex: silo vertical), força pra cima.
        if (!usarRotacaoInicialDoLancador)
        {
            transform.rotation = Quaternion.LookRotation(Vector3.up);
        }
        
        // DEBUG: Garantir que saia alinhado com o tubo
        // O instantiate já faz isso, mas aqui reforçamos a lógica de não sobrescrever.

        PrepararFisica();
        
        // Destroi automaticamente se ficar voando pra sempre
        Invoke(nameof(AutodestruirPorTempo), 15f);
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

        // 1. MOVIMENTO (Sempre em frente no eixo local Z)
        transform.Translate(Vector3.forward * velocidade * Time.deltaTime);

        // 2. GUIAGEM
        if (tempoDeVida < atrasoParaVirar)
        {
            // Fase de Decolagem: Segue reto na direção que saiu do tubo
            // Não faz NADA aqui, apenas mantém a rotação inicial que veio do Instantiate
        }
        else if (temAlvo)
        {
            // Fase de Cruzeiro/Mergulho (Só começa depois do atraso)
            
            // Calcula direção para o alvo
             Vector3 direcaoParaAlvo = (alvo - transform.position).normalized;
             
            // Faz curva suave em direção ao alvo
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
        if (explodiu)
        {
            return;
        }

        explodiu = true;
        lancado = false;
        CancelInvoke(nameof(AutodestruirPorTempo));

        // Visual
        if (efeitoExplosao != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(
                efeitoExplosao,
                transform.position,
                Quaternion.identity,
                3f,
                Vector3.one * escalaExplosao);
        }

        // Som
        if (somExplosao != null)
        {
            AudioRuntime.PlayClipAtPoint(somExplosao, transform.position);
        }
        
        // Dano em Área
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raioDeDano, bufferExplosao, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider h = bufferExplosao[i];
            if (h == null)
            {
                continue;
            }

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

        PoolDeObjetosCombate.Release(gameObject);
    }

    private void AutodestruirPorTempo()
    {
        if (explodiu)
        {
            return;
        }

        explodiu = true;
        lancado = false;
        PoolDeObjetosCombate.Release(gameObject);
    }
}
