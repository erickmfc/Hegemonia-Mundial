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
    
    // OTIMIZAÇÃO: Buffer reutilizável para evitar Garbage Collection (Lixo de Memória)
    private Collider[] bufferColisores = new Collider[40]; 

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

        // OTIMIZAÇÃO: Distribui a carga de processamento. 
        // Em vez de todas as torretas procurarem no mesmo frame, cada uma tem um "offset" aleatório.
        // Aumentei o intervalo de 0.2s para 0.4s (ainda é muito rápido, mas metade do custo).
        float inicioAleatorio = Random.Range(0f, 0.5f);
        InvokeRepeating("ProcurarAlvo", inicioAleatorio, 0.4f);
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

        // --- SISTEMA DE DETECÇÃO OTIMIZADO (V3 - NonAlloc) ---
        // Usa buffer fixo para não gerar lixo na memória a cada varredura.
        // Retorna quantos objetos encontrou (até o limite do buffer, ex: 40).
        int quantidadeEncontrada = Physics.OverlapSphereNonAlloc(transform.position, alcance, bufferColisores);
        
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        // Busca meu próprio ID
        IdentidadeUnidade meuID = GetComponentInParent<IdentidadeUnidade>();
        int meuTime = (meuID != null) ? meuID.teamID : 1; // Se não tiver, assume Jogador (1)

        for (int i = 0; i < quantidadeEncontrada; i++)
        {
            Collider hit = bufferColisores[i];
            if (hit == null) continue;

            Transform alvoTr = hit.transform;
            
            // Ignora a mim mesmo e meus filhos
            if (alvoTr.root == transform.root) continue;

            // 1. TENTA POR IDENTIDADE (Padrão Ouro)
            IdentidadeUnidade idAlvo = alvoTr.GetComponentInParent<IdentidadeUnidade>();
            bool ehInimigo = false;

            if (idAlvo != null)
            {
                if (idAlvo.teamID != meuTime && idAlvo.teamID != 0) // Diferente de mim e Não é Neutro
                {
                    ehInimigo = true;
                }
            }
            // 2. FALLBACK POR TAG (Para objetos simples sem script)
            else 
            {
                // Verifica a tag do objeto e da raiz (fallback seguro com string compare para evitar erro de tag inexistente)
                if ((hit.tag == etiquetaAlvo) || (hit.tag == "Inimigo"))
                {
                    ehInimigo = true;
                }
            }

            if (ehInimigo)
            {
                float dist = Vector3.Distance(transform.position, alvoTr.position);
                if (dist < menorDistancia)
                {
                    // Verifica linha de visão (Opcional - Pode pesar se tiver muitos muros)
                    // if (!Physics.Linecast(transform.position + Vector3.up, alvoTr.position + Vector3.up))
                    {
                        menorDistancia = dist;
                        melhorAlvo = alvoTr;
                    }
                }
            }
        }

        // Limpa referências do buffer para não prender objetos na memória (opcional, mas bom)
        for(int i=0; i<quantidadeEncontrada; i++) bufferColisores[i] = null;

        alvoAtual = melhorAlvo;
    }
    
    /// <summary>
    /// Liga/Desliga o modo automático de ataque
    /// </summary>
    public void DefinirModoAtivo(bool ativo)
    {
        modoPassivo = !ativo; // Se ativo = true, modoPassivo = false
        
        // CORREÇÃO IMPORTANTE: Se está sendo desativado, limpa o alvo imediatamente
        if (modoPassivo)
        {
            alvoAtual = null;
            Debug.Log($"[ControleTorreta] Modo passivo ativado - Alvo limpo");
        }
        else
        {
            Debug.Log($"[ControleTorreta] Modo ativo - Procurando alvos");
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
                
                if (pecaQueGira.parent != null)
                {
                    // Lógica UNIFICADA (Respeita a rotação do barco pai/convés)
                    // Converte a direção do alvo para o espaço local do pai da peça
                    Vector3 localDir = pecaQueGira.parent.InverseTransformDirection(direcao);
                    
                    // Calcula o ângulo Yaw no plano do convés (XZ local)
                    float anguloY = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                    
                    // Só aplica limite se necessário
                    if (limitarRotacao)
                    {
                        anguloY = Mathf.Clamp(anguloY, anguloMinimo, anguloMaximo);
                    }
                    
                    // Aplica rotação APENAS no eixo Y local (Gira no convés, sem tombar/subir)
                    Quaternion rotacaoAlvo = Quaternion.Euler(0, anguloY, 0);
                    pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoAlvo, Time.deltaTime * velocidadeGiro);
                }
                else
                {
                    // Lógica Global (Sem limites ou sem pai - ex: Torreta no chão)
                    Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                    // Trava X e Z globais apenas se não tiver pai
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
                if(Vector3.Angle(pecaQueGira.forward, dirAlvo) < 5f) // Só atira se < 5 graus de erro (mais preciso)
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

    [Header("Armamento Secundário (Mísseis)")]
    [Tooltip("Se definido, usa este prefab para disparos especiais ou de longo alcance.")]
    public GameObject misselPrefab;
    public Transform[] locaisDoMissel; 
    public AudioClip somMissel;
    public float tempoEntreMisseis = 2.0f;
    private float cooldownMissel = 0f;

    [Header("Custumização de Disparo")]
    [Tooltip("Se quiser munições diferentes para canos diferentes, arraste aqui na ordem dos Locais Do Tiro.")]
    public GameObject[] municoesPorCano; 

    void Disparar()
    {
        // 1. DISPARO DE MÍSSIL (Arma Pesada)
        if (misselPrefab != null && cooldownMissel <= 0f && alvoAtual != null)
        {
            DispararMissel();
            cooldownMissel = tempoEntreMisseis;
            return;
        }

        // 2. DISPARO PADRÃO (Metralhadora/Canhão)
        if (locaisDoTiro != null && locaisDoTiro.Length > 0)
        {
            // Define qual prefab usar (Padrão ou Específico do Cano)
            GameObject prefabParaUsar = municaoPrefab;
            
            // Verifica se tem munição específica para este cano (Override)
            if (municoesPorCano != null && indiceBarrilAtual < municoesPorCano.Length)
            {
                if (municoesPorCano[indiceBarrilAtual] != null)
                {
                    prefabParaUsar = municoesPorCano[indiceBarrilAtual];
                }
            }

            if (prefabParaUsar == null) return; // Segurança básica

            Transform barrilDaVez = locaisDoTiro[indiceBarrilAtual];
            GameObject bala = Instantiate(prefabParaUsar, barrilDaVez.position, barrilDaVez.rotation);
            Projetil scriptBala = bala.GetComponent<Projetil>();
            
            if (scriptBala != null)
            {
                scriptBala.SetDono(transform.root.gameObject);
                if (alvoAtual != null)
                {
                    Vector3 direcao = (alvoAtual.position - barrilDaVez.position).normalized;
                    scriptBala.SetDirecao(direcao);
                    // Se o script da bala não tiver velocidade própria, aplicamos uma padrão
                    if (scriptBala.velocidade == 0) scriptBala.velocidade = 200f; 
                }
            }

            if (somTiro != null) fonteAudio.PlayOneShot(somTiro);

            indiceBarrilAtual++;
            if (indiceBarrilAtual >= locaisDoTiro.Length) indiceBarrilAtual = 0;

            balasAtuais--;
            if (balasAtuais <= 0) IniciarRecarga();
        }

        if(cooldownMissel > 0) cooldownMissel -= Time.deltaTime;
    }

    void DispararMissel()
    {
        // Usa locais específicos se tiver, senão usa os da metralhadora
        Transform[] saidas = (locaisDoMissel != null && locaisDoMissel.Length > 0) ? locaisDoMissel : locaisDoTiro;
        if(saidas.Length == 0) return;

        // Pega um aleatório ou sequencial (vou usar sequencial do indiceBarril pra variar)
        Transform saida = saidas[indiceBarrilAtual % saidas.Length]; 

        GameObject missel = Instantiate(misselPrefab, saida.position, saida.rotation);
        
        // Tenta configurar guiagem (Suporta MissilTeleguiado E MisselICBM)
        MissilTeleguiado guiado = missel.GetComponent<MissilTeleguiado>();
        if(guiado != null)
        {
            // Usa o método público DefinirAlvo
            guiado.DefinirAlvo(alvoAtual);
        }
        else
        {
            // Tenta ICBM (que usa IniciarLancamento em vez de IniciarSequencia)
            MisselICBM icbm = missel.GetComponent<MisselICBM>();
            if(icbm != null)
            {
                 icbm.IniciarLancamento(alvoAtual.position);
            }
        }

        if (somMissel != null) fonteAudio.PlayOneShot(somMissel);
        Debug.Log("🚀 Míssil Disparado!");
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
