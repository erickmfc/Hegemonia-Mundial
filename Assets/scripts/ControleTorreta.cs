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
    [Tooltip("A base que gira para os lados (Eixo Y).")]
    public Transform pecaQueGira; 
    [Tooltip("Opcional: A parte que levanta e abaixa (Eixo X). Deixe vazio para a base inclinar inteira.")]
    public Transform canosDaTorreta; 
    public Transform[] locaisDoTiro;  
    public GameObject municaoPrefab; 
    
    [Header("Limites de Rotação Cima/Baixo (Pitch)")]
    public bool limitarInclinacao = true;
    [Range(-90, 90)] public float elevacaoMinima = -10f; // Abaixo
    [Range(-90, 90)] public float elevacaoMaxima = 80f;  // Acima

    [Header("Efeitos")]
    public AudioClip somTiro;
    public AudioClip somRecarga; 
    public ParticleSystem fogoCano;
    private AudioSource fonteAudio;

    private Transform alvoAtual;
    private int indiceBarrilAtual = 0; 
    
    private float rotacaoXOriginal;
    private float rotacaoYOriginal;
    private float rotacaoZOriginal;
    private float giroPitchAlvo = 0f;

    void Start()
    {
        balasAtuais = tamanhoCartucho; // Começa com munição cheia
        misseisAtuais = capacidadeMisseis;
        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null) fonteAudio = gameObject.AddComponent<AudioSource>();
        fonteAudio.spatialBlend = 1f;
        
        // Garante que a referência exista
        if (pecaQueGira == null) pecaQueGira = transform;
        
        rotacaoXOriginal = pecaQueGira.localEulerAngles.x;
        rotacaoYOriginal = pecaQueGira.localEulerAngles.y;
        rotacaoZOriginal = pecaQueGira.localEulerAngles.z;

        float inicioAleatorio = Random.Range(0f, 0.5f);
        InvokeRepeating("ProcurarAlvo", inicioAleatorio, 0.4f);
    }

    [Header("Comportamento")]
    [Tooltip("Se ativado, a torreta não ataca automaticamente.")]
    public bool modoPassivo = false;
    
    [Header("Defesa Anti-Míssil")]
    [Tooltip("Pode interceptar mísseis inimigos no ar?")]
    public bool interceptarMisseis = false;
    [Tooltip("Se ativado, dispara um míssil (Armamento Secundário) em vez de balas para abater a ameaça.")]
    public bool usarMisselParaInterceptar = true;

    void ProcurarAlvo()
    {
        if (modoPassivo) 
        {
            alvoAtual = null;
            return;
        }

        int quantidadeEncontrada = Physics.OverlapSphereNonAlloc(transform.position, alcance, bufferColisores);
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        IdentidadeUnidade meuID = GetComponentInParent<IdentidadeUnidade>();
        int meuTime = (meuID != null) ? meuID.teamID : 1; 

        for (int i = 0; i < quantidadeEncontrada; i++)
        {
            Collider hit = bufferColisores[i];
            if (hit == null) continue;

            Transform alvoTr = hit.transform;
            if (alvoTr.root == transform.root) continue;

            bool ehMissil = alvoTr.GetComponentInParent<MisselCaca>() != null || 
                            alvoTr.GetComponentInParent<MissilTeleguiado>() != null || 
                            alvoTr.GetComponentInParent<MisselICBM>() != null || 
                            hit.tag == "Missil";

            bool ehInimigo = false;

            if (interceptarMisseis && ehMissil)
            {
                Vector3 direcaoDoMissil = alvoTr.forward;
                Vector3 direcaoParaMim = (transform.position - alvoTr.position).normalized;
                
                if (Vector3.Dot(direcaoDoMissil, direcaoParaMim) > 0.2f)
                    ehInimigo = true;
                else
                    continue; 
            }
            else
            {
                IdentidadeUnidade idAlvo = alvoTr.GetComponentInParent<IdentidadeUnidade>();
                if (idAlvo != null)
                {
                    if (idAlvo.teamID != meuTime && idAlvo.teamID != 0)
                        ehInimigo = true;
                }
                else 
                {
                    if ((hit.tag == etiquetaAlvo) || (hit.tag == "Inimigo"))
                        ehInimigo = true;
                }
            }

            if (ehInimigo)
            {
                string nomeBase = transform.root.name.ToLower();
                string nomeObj = transform.name.ToLower();
                bool souAntiAereo = etiquetaAlvo.Equals("Aereo", System.StringComparison.OrdinalIgnoreCase) || 
                                    etiquetaAlvo.Equals("Areo", System.StringComparison.OrdinalIgnoreCase) ||
                                    nomeBase.Contains("ares") || nomeBase.Contains("antiaerea") || 
                                    nomeBase.Contains("ciws") || nomeBase.Contains("sam") || 
                                    nomeObj.Contains("ares") || nomeObj.Contains("antiaerea") || 
                                    nomeObj.Contains("ciws") || nomeObj.Contains("sam");

                bool alvoAereo = ehMissil ||
                                 alvoTr.position.y > 6f ||
                                 alvoTr.GetComponentInParent<ControleAviao>() != null ||
                                 alvoTr.GetComponentInParent<Helicoptero>() != null ||
                                 alvoTr.name.ToLower().Contains("aviao") || 
                                 alvoTr.name.ToLower().Contains("heli") || 
                                 alvoTr.name.ToLower().Contains("caca") ||
                                 alvoTr.tag == "Areo" || 
                                 alvoTr.tag == "Aereo";
                
                if (souAntiAereo) { if (!alvoAereo) continue; }
                else { if (alvoAereo) continue; }

                Vector3 pontoMaisProximo = hit.ClosestPoint(transform.position);
                float dist = Vector3.Distance(transform.position, pontoMaisProximo);
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    melhorAlvo = alvoTr;
                }
            }
        }

        for(int i=0; i<quantidadeEncontrada; i++) bufferColisores[i] = null;
        alvoAtual = melhorAlvo;
    }
    
    public void DefinirModoAtivo(bool ativo)
    {
        modoPassivo = !ativo; 
        if (modoPassivo) alvoAtual = null;
    }

    Vector3 ObterPosicaoPreditaAlvo()
    {
        if (alvoAtual == null) return transform.position;
        Vector3 alvoPosicao = alvoAtual.position;

        float velBala = 200f; 
        if (municaoPrefab != null)
        {
            Projetil proj = municaoPrefab.GetComponent<Projetil>();
            if (proj != null && proj.velocidade > 0f) velBala = proj.velocidade;
        }

        Vector3 targetVel = Vector3.zero;
        Rigidbody rb = alvoAtual.GetComponentInParent<Rigidbody>();
        if (rb != null && !rb.isKinematic) 
        {
            targetVel = rb.linearVelocity;
        }
        else
        {
            ControleUnidade cu = alvoAtual.GetComponentInParent<ControleUnidade>();
            if (cu != null)
            {
                float speed = cu.ObterVelocidadeAtualReal();
                targetVel = alvoAtual.forward * speed;
            }
        }

        if (targetVel.magnitude > 0.5f)
        {
            float dist = Vector3.Distance(pecaQueGira.position, alvoPosicao);
            float tempoAteAlvo = dist / velBala;
            alvoPosicao = alvoPosicao + (targetVel * tempoAteAlvo);
        }

        return alvoPosicao;
    }

    void Update()
    {
        if (estaRecarregandoMisseis)
        {
            contadorRecargaMissel -= Time.deltaTime;
            if (contadorRecargaMissel <= 0f)
            {
                estaRecarregandoMisseis = false;
                misseisAtuais = capacidadeMisseis;
                contadorRecargaMissel = 0f;
            }
        }
        else if (cooldownMissel > 0f) cooldownMissel -= Time.deltaTime; 

        if (estaRecarregando)
        {
            contadorTempo -= Time.deltaTime;
            if (contadorTempo <= 0f)
            {
                estaRecarregando = false;
                balasAtuais = tamanhoCartucho;
                contadorTempo = 0f; 
            }
            return;
        }

        if (alvoAtual != null)
        {
            if (pecaQueGira != null)
            {
                Vector3 alvoPosicao = ObterPosicaoPreditaAlvo();
                Vector3 direcao = alvoPosicao - pecaQueGira.position;
                float anguloY = pecaQueGira.localEulerAngles.y;
                float anguloX = rotacaoXOriginal;

                if (pecaQueGira.parent != null)
                {
                    Vector3 localDir = pecaQueGira.parent.InverseTransformDirection(direcao);
                    anguloY = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                    
                    if (limitarRotacao) anguloY = Mathf.Clamp(anguloY, anguloMinimo, anguloMaximo);

                    // --- CÁLCULO PITCH (Esquema de Elevação) ---
                    float distanciaPlana = new Vector2(localDir.x, localDir.z).magnitude;
                    giroPitchAlvo = -Mathf.Atan2(localDir.y, distanciaPlana) * Mathf.Rad2Deg;
                }
                else
                {
                    Quaternion olhar = Quaternion.LookRotation(direcao);
                    anguloY = olhar.eulerAngles.y;
                    
                    Vector3 localDir = pecaQueGira.InverseTransformDirection(direcao); 
                    float distanciaPlana = new Vector2(localDir.x, localDir.z).magnitude;
                    giroPitchAlvo = -Mathf.Atan2(localDir.y, distanciaPlana) * Mathf.Rad2Deg;
                }

                if (limitarInclinacao) giroPitchAlvo = Mathf.Clamp(giroPitchAlvo, -elevacaoMaxima, -elevacaoMinima); // Invertido pois -X é para cima na maioria dos modelos

                if (canosDaTorreta != null) // Peças separadas (Gira Base = Yaw, Gira Cano = Pitch)
                {
                    Quaternion rotacaoBase = Quaternion.Euler(rotacaoXOriginal, anguloY, rotacaoZOriginal);
                    pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoBase, Time.deltaTime * velocidadeGiro);

                    Quaternion rotacaoCanos = Quaternion.Euler(giroPitchAlvo, canosDaTorreta.localEulerAngles.y, canosDaTorreta.localEulerAngles.z);
                    canosDaTorreta.localRotation = Quaternion.Lerp(canosDaTorreta.localRotation, rotacaoCanos, Time.deltaTime * velocidadeGiro);
                }
                else // Peça única faz os dois movimentos (Base inclina inteira)
                {
                    Quaternion rotacaoTotal = Quaternion.Euler(giroPitchAlvo, anguloY, rotacaoZOriginal);
                    pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoTotal, Time.deltaTime * velocidadeGiro);
                }
            }

            // MODO TIRO: Atira se der o tempo
            if (contadorTempo <= 0f)
            {
                // Verifica se o ângulo permite atirar (Ignorando a altura "Y" porque tanques/navios não levantam o cano nas configurações básicas)
                // Se não ignorar o Y, o ângulo para a base da Prefeitura sempre seria > 5 e nunca atiraria.
                Vector3 alvoPosicao = ObterPosicaoPreditaAlvo(); // Re-obter para garantir que é a mais recente
                Vector3 dirAlvo = (alvoPosicao - pecaQueGira.position);
                dirAlvo.y = 0;
                Vector3 minhaFrente = pecaQueGira.forward;
                minhaFrente.y = 0;
                
                // Tolera o erro de engasgo se for antiaereo contra alvo rapido
                string nomeB = transform.root.name.ToLower();
                string nomeO = transform.name.ToLower();
                bool antiAereo = etiquetaAlvo.Equals("Aereo", System.StringComparison.OrdinalIgnoreCase) || 
                                 etiquetaAlvo.Equals("Areo", System.StringComparison.OrdinalIgnoreCase) ||
                                 nomeB.Contains("ares") || nomeB.Contains("antiaerea") || 
                                 nomeB.Contains("ciws") || nomeB.Contains("sam") || 
                                 nomeO.Contains("ares") || nomeO.Contains("antiaerea") || 
                                 nomeO.Contains("ciws") || nomeO.Contains("sam");
                float anguloMaximo = antiAereo ? 45f : 8f;

                if(Vector3.Angle(minhaFrente, dirAlvo) < anguloMaximo) // 8 graus para terrestre, 45 graus para aereo
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
                // Se tem limite, volta para o centro mantendo a inclinação original do modelo 3D
                Quaternion centro = Quaternion.Euler(rotacaoXOriginal, 0, rotacaoZOriginal);
                pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, centro, Time.deltaTime * 2f);
            }
            else
            {
                // Radar girando 360 usando angulo livre protegido
                float anguloLivre = (Time.time * 20f) % 360f;
                pecaQueGira.localRotation = Quaternion.Euler(rotacaoXOriginal, anguloLivre, rotacaoZOriginal);
            }
        }
    }

    [Header("Armamento Secundário (Mísseis)")]
    [Tooltip("Se definido, usa este prefab para disparos especiais ou de longo alcance.")]
    public GameObject misselPrefab;
    public Transform[] locaisDoMissel; 
    public AudioClip somMissel;
    public float tempoEntreMisseis = 2.0f;
    
    [Tooltip("Quantidade máxima de mísseis antes de precisar recarregar.")]
    public int capacidadeMisseis = 4;
    [Tooltip("Tempo em segundos para reabastecer os mísseis.")]
    public float tempoRecargaMisseis = 10f;
    
    private int misseisAtuais;
    private bool estaRecarregandoMisseis = false;
    private float contadorRecargaMissel = 0f;
    private float cooldownMissel = 0f;

    [Header("Custumização de Disparo")]
    [Tooltip("Se quiser munições diferentes para canos diferentes, arraste aqui na ordem dos Locais Do Tiro.")]
    public GameObject[] municoesPorCano; 

    void Disparar()
    {
        bool alvoEhMissil = alvoAtual != null && (alvoAtual.GetComponentInParent<MisselCaca>() != null || alvoAtual.GetComponentInParent<MissilTeleguiado>() != null || alvoAtual.GetComponentInParent<MisselICBM>() != null || alvoAtual.tag == "Missil");

        // 1. DISPARO DE MÍSSIL (Arma Pesada ou Interceptador)
        if (misselPrefab != null && cooldownMissel <= 0f && !estaRecarregandoMisseis && misseisAtuais > 0 && alvoAtual != null)
        {
            // Se o alvo for míssil, só atira de míssil se 'usarMisselParaInterceptar' for true
            if (!alvoEhMissil || (alvoEhMissil && usarMisselParaInterceptar))
            {
                DispararMissel();
                cooldownMissel = tempoEntreMisseis;
                misseisAtuais--;

                if (misseisAtuais <= 0)
                {
                    estaRecarregandoMisseis = true;
                    contadorRecargaMissel = tempoRecargaMisseis;
                    // opcional: som de empty ou recarregando
                }
                
                // Se é um míssil e pedimos pra usar míssil neles, não desperdiça bala agora
                if (alvoEhMissil && usarMisselParaInterceptar) return;
                
                // Se não era míssil inimigo, o return normal do sistema de arma pesada
                if (!alvoEhMissil) return; 
            }
        }

        // Se o alvo for míssil, e nós NÃO temos missel (ou usarMisselParaInterceptar = false), vamos fuzilar ele com balas!
        // 2. DISPARO PADRÃO (Metralhadora/Canhão/CIWS)
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
                    Vector3 alvoPosicao = ObterPosicaoPreditaAlvo();
                    Vector3 direcao = (alvoPosicao - barrilDaVez.position).normalized;
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
                 icbm.IniciarLancamento(ObterPosicaoPreditaAlvo());
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
