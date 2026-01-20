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
    
    [Header("Limites de Rotação (Anti-Clipping)")]
    public bool limitarRotacao = true;
    [Range(-180, 180)] public float anguloMinimo = -90f;
    [Range(-180, 180)] public float anguloMaximo = 90f;

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
        
        // Garante que a referência exista
        if (pecaQueGira == null) pecaQueGira = transform;

        InvokeRepeating("ProcurarAlvo", 0f, 0.2f);
    }

    [Header("Comportamento")]
    [Tooltip("Se ativado, a torreta não ataca automaticamente.")]
    public bool modoPassivo = false;

    void ProcurarAlvo()
    {
        if (modoPassivo) 
        {
            alvoAtual = null;
            return;
        }

        GameObject[] inimigos = GameObject.FindGameObjectsWithTag(etiquetaAlvo);
        float menorDistancia = Mathf.Infinity;
        GameObject inimigoMaisPerto = null;

        // Busca meu próprio ID
        IdentidadeUnidade meuID = GetComponentInParent<IdentidadeUnidade>();
        
        foreach (GameObject inimigo in inimigos)
        {
            // Verificação de Amigo ou Inimigo (IFF)
            IdentidadeUnidade idAlvo = inimigo.GetComponent<IdentidadeUnidade>();
            
            // Regra:
            // 1. Se eu não tenho ID ou o alvo não tem ID, ataca (comportamento padrão antigo).
            // 2. Se ambos têm ID e são IGUAIS, ignora (Aliado).
            if (meuID != null && idAlvo != null)
            {
                if (meuID.teamID == idAlvo.teamID) continue; // Pula aliados
            }

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
            if (pecaQueGira != null)
            {
                Vector3 direcao = alvoAtual.position - pecaQueGira.position;
                
                if (limitarRotacao && pecaQueGira.parent != null)
                {
                    // Lógica Local Clamp (Respeita a rotação do barco pai)
                    Vector3 localDir = pecaQueGira.parent.InverseTransformDirection(direcao);
                    float anguloY = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                    float anguloTravado = Mathf.Clamp(anguloY, anguloMinimo, anguloMaximo);
                    
                    Quaternion rotacaoAlvo = Quaternion.Euler(0, anguloTravado, 0);
                    pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoAlvo, Time.deltaTime * velocidadeGiro);
                }
                else
                {
                    // Lógica Global (Sem limites ou sem pai)
                    Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                    // Trava X e Z para não tombar
                    rotacaoAlvo = Quaternion.Euler(0, rotacaoAlvo.eulerAngles.y, 0);
                    pecaQueGira.rotation = Quaternion.Lerp(pecaQueGira.rotation, rotacaoAlvo, Time.deltaTime * velocidadeGiro);
                }
            }

            // MODO TIRO: Atira se der o tempo
            if (contadorTempo <= 0f)
            {
                // Verifica se o ângulo permite atirar (Se a arma não está apontando para o alvo, não atira)
                // Isso evita atirar "através" do barco enquanto gira
                Vector3 dirAlvo = (alvoAtual.position - pecaQueGira.position).normalized;
                if(Vector3.Angle(pecaQueGira.forward, dirAlvo) < 10f) // Só atira se < 10 graus de erro
                {
                    Disparar();
                    if (!estaRecarregando) contadorTempo = tempoEntreTiros;
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
        if (pecaQueGira != null)
        {
            if (limitarRotacao)
            {
                // Se tem limite, volta para o centro (0 graus)
                pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, Quaternion.identity, Time.deltaTime * 2f);
            }
            else
            {
                // Radar girando 360
                pecaQueGira.Rotate(0, 10f * Time.deltaTime, 0);
            }
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
            
            // CORREÇÃO: Usa Projetil ao invés de MissilTeleguiado (tiro em linha reta)
            Projetil scriptBala = bala.GetComponent<Projetil>();
            
            if (scriptBala != null)
            {
                // Define quem atirou (para não se auto-atacar)
                scriptBala.SetDono(transform.root.gameObject);
                
                if (alvoAtual != null)
                {
                    // Calcula direção FIXA do tiro (balístico, não rastreador)
                    Vector3 direcao = (alvoAtual.position - barrilDaVez.position).normalized;
                    scriptBala.SetDirecao(direcao);
                    scriptBala.velocidade = 200f; // Tiro de metralhadora é rápido
                }
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
