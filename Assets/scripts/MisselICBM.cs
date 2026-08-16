using UnityEngine;
using System.Collections.Generic;

public class MisselICBM : MonoBehaviour
{
    [Header("Motores")]
    public float velocidade = 25f;
    public float velocidadeDeGiro = 30f; // Quanto menor, mais aberta a curva (Ex: 20 a 40)
    
    [Header("Trajetória")]
    public float alturaDoArco = 60f; // Altura máxima do voo
    public float atrasoParaVirar = 1.5f; // Tempo que sobe reto antes de mirar
    [Tooltip("Distancia usada para confirmar o impacto no ponto de destino.")]
    public float distanciaDeImpacto = 12f;

    [Header("Explosão")]
    public GameObject efeitoExplosao;
    public AudioClip somExplosao; // Arraste o áudio aqui
    public float raioDeDano = 20f;
    public float escalaExplosao = 8.0f;
    public float distanciaSom = 50f; // Distância máxima para ouvir

    // Internas
    private Vector3 alvo;
    private bool lancado = false;
    private bool explodiu = false;
    private float tempoDeVida = 0;
    private Quaternion rotacaoAlvo;
    private bool iniciouDescida;
    private Vector3 pontoDePartida;
    private float alturaDoApex;
    private readonly Collider[] bufferExplosao = new Collider[160];
    private static readonly HashSet<int> alvosProcessados = new HashSet<int>();

    public void IniciarLancamento(Vector3 pontoAlvo)
    {
        alvo = pontoAlvo;
        lancado = true;
        explodiu = false;
        tempoDeVida = 0;
        iniciouDescida = false;
        pontoDePartida = transform.position;
        alturaDoApex = Mathf.Max(pontoDePartida.y, alvo.y) + Mathf.Max(alturaDoArco, 10f);
        CancelInvoke(nameof(ReativarColisao));

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

        // Trajetoria robusta: sobe ate um apex fixo e inicia a descida
        // obrigatoriamente, evitando a mira dinamica que mantinha o missil no ceu.
        Vector3 posicaoAnteriorCorrigida = transform.position;
        if (!iniciouDescida && transform.position.y < alturaDoApex - 2f)
        {
            Quaternion subirCorrigido = Quaternion.LookRotation(Vector3.up);
            float giroSubidaCorrigido = tempoDeVida < atrasoParaVirar ? 180f : velocidadeDeGiro;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, subirCorrigido, giroSubidaCorrigido * Time.deltaTime);
        }
        else
        {
            iniciouDescida = true;
            Vector3 direcaoParaAlvoCorrigida = alvo - transform.position;
            if (direcaoParaAlvoCorrigida.sqrMagnitude > 0.001f)
            {
                Quaternion mirarAlvoCorrigido = Quaternion.LookRotation(direcaoParaAlvoCorrigida.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, mirarAlvoCorrigido, velocidadeDeGiro * Time.deltaTime);
            }
        }
        transform.position += transform.forward * (velocidade * Time.deltaTime);
        Vector3 segmentoCorrigido = transform.position - posicaoAnteriorCorrigida;
        float comprimentoCorrigido = segmentoCorrigido.sqrMagnitude;
        float tCorrigido = comprimentoCorrigido > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(alvo - posicaoAnteriorCorrigida, segmentoCorrigido) / comprimentoCorrigido)
            : 0f;
        Vector3 pontoMaisProximoCorrigido = posicaoAnteriorCorrigida + segmentoCorrigido * tCorrigido;
        bool cruzouAlvoCorrigido = iniciouDescida && Vector3.Distance(pontoMaisProximoCorrigido, alvo) <= Mathf.Max(4f, distanciaDeImpacto);
        bool chegouAoChaoCorrigido = iniciouDescida && transform.position.y <= alvo.y + 2f;
        if (cruzouAlvoCorrigido || chegouAoChaoCorrigido)
        {
            transform.position = alvo;
            Explodir();
        }
        return;
    }
#if false

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

#endif
    void Explodir()
    {
        if (explodiu)
        {
            return;
        }

        explodiu = true;
        lancado = false;

        // 1. Cria o Visual
        if (efeitoExplosao != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(
                efeitoExplosao,
                transform.position,
                Quaternion.identity,
                8f,
                Vector3.one * escalaExplosao);
        }

        // 2. Cria o Som
        if (somExplosao != null)
        {
            GameObject audioObj = new GameObject("SomExplosaoICBM");
            audioObj.transform.position = transform.position;
            
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = somExplosao;
            source.volume = 0.8f;
            source.spatialBlend = 1.0f;
            source.minDistance = 3f;
            source.maxDistance = Mathf.Max(300f, distanciaSom);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();

            Destroy(audioObj, somExplosao.length + 0.5f);
        }
        
        // 3. Aplica Dano e Física (O Melhor dos Dois Mundos)
        alvosProcessados.Clear();
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raioDeDano, bufferExplosao, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider h = bufferExplosao[i];
            if (h == null)
            {
                continue;
            }

            // A. Dano no Sistema (Unidades/Prédios)
            SistemaDeDanos vida = h.GetComponent<SistemaDeDanos>() ?? h.GetComponentInParent<SistemaDeDanos>();
            if (vida != null)
            {
                int idVida = vida.GetInstanceID();
                if (alvosProcessados.Add(idVida))
                {
                    vida.ReceberDano(Mathf.RoundToInt(Mathf.Max(300f, raioDeDano * 50f)));
                }
            }

            // B. Física de Explosão (Empurrar Destroços/Unidades)
            Rigidbody rb = h.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Força de 2000 (padrão) para jogar longe
                rb.AddExplosionForce(2000f, transform.position, raioDeDano, 3.0f);

                // C. Destruir objetos de cenário "soltos" que não tenham script de vida
                if (vida == null)
                {
                    Destroy(h.gameObject, 0.5f); // Dá meio segundo para voar com o impacto antes de sumir
                }
            }
        }

        PoolDeObjetosCombate.Release(gameObject);
    }
}
