using UnityEngine;

public class ControleTorreta : MonoBehaviour
{
    [Header("Radar")]
    [Tooltip("Define qual tag a torreta vai procurar (Ex: 'Inimigo', 'Aereo').")]
    public string etiquetaAlvo = "Aereo"; 
    
    [Tooltip("Distância máxima que o radar consegue enxergar.")]
    public float alcance = 120f; 
    
    [Header("Mecânica & Recarga")]
    [Tooltip("Velocidade que a torreta gira para acompanhar o alvo.")]
    public float velocidadeGiro = 60f; 

    [Tooltip("Tempo em SEGUNDOS entre cada tiro (Quanto menor, mais rápido).")]
    public float tempoEntreTiros = 0.08f; 

    [Tooltip("Quantidade de tiros até precisar carregar (Ex: 50 balas).")]
    public int tamanhoCartucho = 50; 

    [Tooltip("Tempo inativa recarregando (Segundos).")]
    public float tempoRecarga = 2.0f; 
    
    // Variáveis internas
    private float contadorTempo = 0f;
    private int balasAtuais;
    private bool estaRecarregando = false;

    [Header("Peças")]
    public Transform pecaQueGira; 
    public Transform[] locaisDoTiro;  
    public GameObject municaoPrefab; 

    [Header("Efeitos")]
    public AudioClip somTiro;
    public AudioClip somRecarga; 
    public ParticleSystem fogoCano;
    private AudioSource fonteAudio;

    private Transform alvoAtual;
    private int indiceBarrilAtual = 0; 

    void Start()
    {
        balasAtuais = tamanhoCartucho; // Começa com munição cheia
        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null) fonteAudio = gameObject.AddComponent<AudioSource>();
        fonteAudio.spatialBlend = 1f;
        
        InvokeRepeating("ProcurarAlvo", 0f, 0.2f);
    }

    void ProcurarAlvo()
    {
        GameObject[] inimigos = GameObject.FindGameObjectsWithTag(etiquetaAlvo);
        float menorDistancia = Mathf.Infinity;
        GameObject inimigoMaisPerto = null;

        foreach (GameObject inimigo in inimigos)
        {
            float distancia = Vector3.Distance(transform.position, inimigo.transform.position);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                inimigoMaisPerto = inimigo;
            }
        }

        if (inimigoMaisPerto != null && menorDistancia <= alcance)
        {
            alvoAtual = inimigoMaisPerto.transform;
        }
        else
        {
            alvoAtual = null;
        }
    }

    void Update()
    {
        // 1. SISTEMA DE RECARGA
        if (estaRecarregando)
        {
            contadorTempo -= Time.deltaTime;
            if (contadorTempo <= 0f)
            {
                estaRecarregando = false;
                balasAtuais = tamanhoCartucho;
                contadorTempo = 0f; 
            }
            return; // Se estiver recarregando, não faz mais nada
        }

        // 2. COMPORTAMENTO DE MIRA
        if (alvoAtual != null)
        {
            // MODO COMBATE: Olha para o inimigo
            Vector3 direcao = alvoAtual.position - transform.position;
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
            pecaQueGira.rotation = Quaternion.Lerp(pecaQueGira.rotation, rotacaoAlvo, Time.deltaTime * velocidadeGiro);

            // MODO TIRO: Atira se der o tempo
            if (contadorTempo <= 0f)
            {
                Disparar();
                // CORREÇÃO: Só define o tempo do próximo tiro se NÃO entrou em recarga
                if (!estaRecarregando)
                {
                    contadorTempo = tempoEntreTiros;
                }
            }
            contadorTempo -= Time.deltaTime;
        }
        else
        {
            // MODO OCIOSO (Extra): Gira devagarinho como um radar varrendo a área
            ModoOcioso();
        }
    }

    void ModoOcioso()
    {
        // Gira suavemente no eixo Y (procurando)
        // Se a sua torreta for montada diferente, talvez precise ser eixo Z ou X
        if (pecaQueGira != null)
        {
            pecaQueGira.Rotate(0, 10f * Time.deltaTime, 0);
        }
    }

    void Disparar()
    {
        if (municaoPrefab != null && locaisDoTiro != null && locaisDoTiro.Length > 0)
        {
            // Pega o cano da vez
            Transform barrilDaVez = locaisDoTiro[indiceBarrilAtual];
            
            // Cria a bala
            GameObject bala = Instantiate(municaoPrefab, barrilDaVez.position, barrilDaVez.rotation);
            MissilTeleguiado scriptBala = bala.GetComponent<MissilTeleguiado>();
            
            if (scriptBala != null)
            {
                scriptBala.DefinirAlvo(alvoAtual);
                scriptBala.velocidade = 130f; 
            }

            if (somTiro != null) fonteAudio.PlayOneShot(somTiro);

            // Cicla para o próximo cano
            indiceBarrilAtual++;
            if (indiceBarrilAtual >= locaisDoTiro.Length)
            {
                indiceBarrilAtual = 0;
            }

            // Gasta bala
            balasAtuais--;
            if (balasAtuais <= 0)
            {
                IniciarRecarga();
            }
        }
    }

    void IniciarRecarga()
    {
        estaRecarregando = true;
        contadorTempo = tempoRecarga;
        if (somRecarga != null) fonteAudio.PlayOneShot(somRecarga);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alcance);
    }
}
