using UnityEngine;
using System.Collections.Generic;

public class SistemaAntiMissil : MonoBehaviour
{
    [Header("Radar & Alcance (Defesa de Área)")]
    [Tooltip("Raio de detecção do radar para defender o navio e todos os aliados próximos.")]
    public float alcanceRadar = 180f;
    [Tooltip("Tempo em segundos entre cada checagem do radar (ex: 0.3s)")]
    public float tempoDeEscaneamento = 0.3f;

    [Header("Mecânica da Torreta")]
    [Tooltip("Base que gira para os lados (Yaw)")]
    public Transform baseGiratoria; 
    [Tooltip("Peça que vira para cima/baixo (Pitch)")]
    public Transform canoElevacao;  
    public float velocidadeGiro = 60f;

    [Header("Sistema de Disparo")]
    [Tooltip("Prefab do míssil que vai abater o outro míssil (Interceptador).")]
    public GameObject prefabIntercepador;
    [Tooltip("Zonas de onde o míssil interceptador vai sair.")]
    public Transform[] pontosDeSaida;
    [Tooltip("Cadência de tiro. Tempo entre disparar um interceptador e outro.")]
    public float tempoEntreTiros = 0.8f;
    [Tooltip("Capacidade de Mísseis prontos. Quantidade antes de iniciar a recarga cheia.")]
    public int capacidadeMisseis = 10;
    public float tempoRecargaMisseis = 5f;

    [Header("Efeitos & Sons")]
    public AudioClip somDisparo;
    private AudioSource audioSource;

    [Header("Comportamento")]
    [Tooltip("Se ativado, o sistema não intercepta mísseis automaticamente (modo Ocioso).")]
    public bool modoPassivo = false;

    // Variáveis Internas
    private Transform alvoMissilAtual;
    private IdentidadeUnidade minhaIdentidade;
    private float cooldownDisparo = 0f;
    private int misseisAtuais;
    private bool recarregando = false;
    private int indexSaida = 0;

    void Start()
    {
        misseisAtuais = capacidadeMisseis;

        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        if (minhaIdentidade == null)
        {
            // Cria uma identidade se a torreta não pertencer a ninguém
            minhaIdentidade = gameObject.AddComponent<IdentidadeUnidade>();
            minhaIdentidade.teamID = 1;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;

        // Começa a varredura do radar de forma otimizada para não travar o jogo
        InvokeRepeating(nameof(ProcurarAmeacaMisseis), Random.Range(0f, 0.5f), tempoDeEscaneamento);
    }

    void Update()
    {
        // Lógica de Recarga
        if (cooldownDisparo > 0f) cooldownDisparo -= Time.deltaTime;

        if (recarregando)
        {
            if (cooldownDisparo <= 0f)
            {
                misseisAtuais = capacidadeMisseis;
                recarregando = false;
            }
            return; // Espera terminar a recarga sem atirar
        }

        if (alvoMissilAtual != null)
        {
            // O alvo explodiu, sumiu ou saiu muito da área? Descarta!
            if (!alvoMissilAtual.gameObject.activeInHierarchy || 
                Vector3.Distance(transform.position, alvoMissilAtual.position) > (alcanceRadar * 1.5f))
            {
                alvoMissilAtual = null;
                return;
            }

            // Gira no alvo
            Mirar();

            // Atirar se estiver alinhado e arma carregada
            if (cooldownDisparo <= 0f && MirouEmCheio())
            {
                if (misseisAtuais > 0)
                {
                    AtirarInterceptador();
                    misseisAtuais--;
                    cooldownDisparo = tempoEntreTiros;

                    if (misseisAtuais <= 0 && capacidadeMisseis > 0)
                    {
                        recarregando = true;
                        cooldownDisparo = tempoRecargaMisseis;
                    }
                }
            }
        }
        else
        {
            // Ficar vigiando (Radar)
            ModoOcioso();
        }
    }

    void ModoOcioso()
    {
        if (baseGiratoria != null)
        {
            baseGiratoria.Rotate(0, 30f * Time.deltaTime, 0, Space.Self);
        }
        
        if (canoElevacao != null)
        {
            canoElevacao.localRotation = Quaternion.Lerp(canoElevacao.localRotation, Quaternion.identity, Time.deltaTime * 5f);
        }
    }

    /// <summary>
    /// Escaneia a área procurando qualquer objeto que seja um míssil ameaçando a nós ou aos aliados na área de suporte.
    /// </summary>
    void ProcurarAmeacaMisseis()
    {
        if (modoPassivo)
        {
            alvoMissilAtual = null;
            return;
        }

        // Se a ameaça já for válida, nem gasta CPU procurando mais
        if (alvoMissilAtual != null && alvoMissilAtual.gameObject.activeInHierarchy) return;

        Collider[] objetosNaArea = Physics.OverlapSphere(transform.position, alcanceRadar);
        
        List<Transform> misseisDetectados = new List<Transform>();
        List<Transform> aliadosNaArea = new List<Transform>();
        
        // Garante que a gente também proteja o próprio navio/estrutura
        aliadosNaArea.Add(transform.root); 

        // 1. Separar o que é míssil e o que é alidado protegido
        foreach (Collider col in objetosNaArea)
        {
            if (col.isTrigger) continue;

            Transform tr = col.transform;

            string tagStr = col.tag;
            string nomeLC = tr.name.ToLower();

            bool ehMissil = tagStr == "Missil" || 
                            nomeLC.Contains("missil") || 
                            nomeLC.Contains("projetil") || 
                            tr.GetComponent<Projetil>() != null || 
                            tr.GetComponentInParent<Projetil>() != null;

            if (ehMissil)
            {
                misseisDetectados.Add(tr);
            }
            else
            {
                IdentidadeUnidade id = tr.GetComponentInParent<IdentidadeUnidade>();
                if (id != null && (id.teamID == minhaIdentidade.teamID || minhaIdentidade.teamID == 0))
                {
                    if (!aliadosNaArea.Contains(id.transform.root)) 
                        aliadosNaArea.Add(id.transform.root);
                }
            }
        }

        float menorDistancia = Mathf.Infinity;
        Transform melhorMissilAlvo = null;

        // 2. Verificar cada míssil detectado se é perigoso (Ameaça)
        foreach (Transform missil in misseisDetectados)
        {
            Vector3 posMissil = missil.position;
            Vector3 dirMissil = missil.forward;

            // (A) Ignorar mísseis "amigos" (Ex: um navio aliado que atirou um míssil e ele ainda tá perto)
            bool interceptarIgnorarPorSerAliado = false;
            foreach (Transform aliado in aliadosNaArea)
            {
                if (aliado == null) continue;
                Vector3 dirProAliado = aliado.position - posMissil; 
                float distAteAmigo = dirProAliado.magnitude;
                
                if (distAteAmigo < 25f) // Nasceu no amigo agora
                {
                    // Se o míssil foca em afastar do amigo (foi disparado) a gente não deve abater ele
                    if (Vector3.Dot(dirMissil, dirProAliado.normalized) < -0.3f) 
                    {
                        interceptarIgnorarPorSerAliado = true;
                        break;
                    }
                }
            }

            if (interceptarIgnorarPorSerAliado) continue; // Pula este míssil, ele é "dos nossos"

            // (B) Este míssil é uma ameaça para a zona de suporte aliados?
            bool ehAmeaça = false;
            foreach (Transform aliado in aliadosNaArea)
            {
                if (aliado == null) continue;
                
                Vector3 dirProAliado = (aliado.position - posMissil).normalized;
                
                // Míssil apontando na direção de algum aliado de perto ou de longe (ângulo de 90°)
                if (Vector3.Dot(dirMissil, dirProAliado) > 0.4f)
                {
                    ehAmeaça = true;
                    break;
                }
            }

            // Mísseis soltos varrendo a área super próximos também são perigo iminente (raio crítico de 45m)
            if (!ehAmeaça && Vector3.Distance(transform.position, posMissil) < 45f) ehAmeaça = true;

            if (ehAmeaça)
            {
                float d = Vector3.Distance(transform.position, posMissil);
                // Dá prioridade pro míssil inimigo mais próximo de mim pra abater logo!
                if (d < menorDistancia)
                {
                    menorDistancia = d;
                    melhorMissilAlvo = missil;
                }
            }
        }

        alvoMissilAtual = melhorMissilAlvo;
    }

    /// <summary>
    /// Calcula o ponto imaginário para a torreta já ir virando antes da hora para compensar a velocidade supersônica do míssil
    /// </summary>
    Vector3 PreverPosicaoAlvoSuperSonia()
    {
        if (alvoMissilAtual == null) return transform.position;

        Rigidbody rb = alvoMissilAtual.GetComponentInParent<Rigidbody>();
        
        // Pega velocidade física ou deduz uma bruta (ex 80 m/s)
        Vector3 velLinear = (rb != null && !rb.isKinematic) ? rb.linearVelocity : (alvoMissilAtual.forward * 80f);

        float d = Vector3.Distance(transform.position, alvoMissilAtual.position);
        
        // Simula o tempo que a nossa bala ia levar para acertar a frente
        float velInterceptador = 150f;
        if (prefabIntercepador != null)
        {
            Projetil proj = prefabIntercepador.GetComponent<Projetil>();
            if (proj != null && proj.velocidade > 0f) velInterceptador = proj.velocidade;
        }

        float tempo = d / velInterceptador;
        return alvoMissilAtual.position + (velLinear * tempo);
    }

    void Mirar()
    {
        Vector3 posFuturaMortal = PreverPosicaoAlvoSuperSonia();

        if (baseGiratoria != null)
        {
            Vector3 dirBase = posFuturaMortal - baseGiratoria.position;
            dirBase.y = 0; // Trava o eixo Y nela
            if (dirBase != Vector3.zero)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(dirBase);
                baseGiratoria.rotation = Quaternion.Slerp(baseGiratoria.rotation, rotAlvo, Time.deltaTime * velocidadeGiro);
            }
        }

        if (canoElevacao != null)
        {
            Vector3 dirCano = posFuturaMortal - canoElevacao.position;
            if (dirCano != Vector3.zero)
            {
                Quaternion rotCano = Quaternion.LookRotation(dirCano);
                canoElevacao.rotation = Quaternion.Slerp(canoElevacao.rotation, rotCano, Time.deltaTime * velocidadeGiro);
            }
        }
    }

    bool MirouEmCheio()
    {
        Vector3 posFutura = PreverPosicaoAlvoSuperSonia();

        // Tolerâncias altas (ex: 40 graus) pro tiro sair fácil. Mísseis são muito velozes pra torreta cravar no milímetro
        if (baseGiratoria != null)
        {
            Vector3 dir = (posFutura - baseGiratoria.position);
            dir.y = 0;
            Vector3 frente = baseGiratoria.forward;
            frente.y = 0;
            if (Vector3.Angle(frente, dir.normalized) > 40f) return false;
        }

        if (canoElevacao != null)
        {
            Vector3 dir = (posFutura - canoElevacao.position).normalized;
            if (Vector3.Angle(canoElevacao.forward, dir) > 40f) return false;
        }

        return true;
    }

    void AtirarInterceptador()
    {
        if (prefabIntercepador == null || pontosDeSaida == null || pontosDeSaida.Length == 0) return;

        Transform saidaDaVez = pontosDeSaida[indexSaida];
        indexSaida = (indexSaida + 1) % pontosDeSaida.Length;

        if (saidaDaVez == null) return;

        GameObject missilGerado = Instantiate(prefabIntercepador, saidaDaVez.position, saidaDaVez.rotation);
        
        Projetil p = missilGerado.GetComponent<Projetil>();
        if (p != null)
        {
            p.SetDono(transform.root.gameObject); // Pra não estourar a gente no ato
            
            // Foco 100% no míssil inimigo capturado (Míssil cassando Míssil!)
            p.SetAlvo(alvoMissilAtual);

            // Um míssil interceptador precisa curvar violento e ser rápido para derrubar outro míssil caindo
            if (p.curvaDePerseguicao < 90f) p.curvaDePerseguicao = 150f; 
            if (p.velocidade < 100f) p.velocidade = 200f; // Muito Supersônico
        }

        if (somDisparo != null && audioSource != null)
        {
            audioSource.PlayOneShot(somDisparo, 0.7f);
        }
    }

    public void DefinirModoAtivo(bool ativo)
    {
        modoPassivo = !ativo; 
        if (modoPassivo) alvoMissilAtual = null;
    }

    void OnDrawGizmosSelected()
    {
        // Visualizador no Unity Editor pro Level Designer ver o tamanho da "Bolha de Defesa"
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, alcanceRadar);
    }
}
