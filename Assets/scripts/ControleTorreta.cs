using System.Collections.Generic;
using UnityEngine;

public class ControleTorreta : MonoBehaviour
{
    [Header("Radar")]
    [Tooltip("Define qual tag a torreta vai procurar (Ex: 'Inimigo', 'Aereo').")]
    public string etiquetaAlvo = "Aereo"; 
    
    [Tooltip("DistÃ¢ncia mÃ¡xima que o radar consegue enxergar.")]
    public float alcance = 120f; 
    
    [Header("MecÃ¢nica & Recarga")]
    [Tooltip("Velocidade que a torreta gira para acompanhar o alvo.")]
    public float velocidadeGiro = 60f;
    
    [Header("Limites de RotaÃ§Ã£o (Anti-Clipping)")]
    public bool limitarRotacao = true;
    [Range(-180, 180)] public float anguloMinimo = -90f;
    [Range(-180, 180)] public float anguloMaximo = 90f;

    [Tooltip("Tempo em SEGUNDOS entre cada tiro (Quanto menor, mais rÃ¡pido).")]
    public float tempoEntreTiros = 0.08f; 

    [Tooltip("Quantidade de tiros atÃ© precisar carregar (Ex: 50 balas).")]
    public int tamanhoCartucho = 50; 

    [Tooltip("Tempo inativa recarregando (Segundos).")]
    public float tempoRecarga = 2.0f; 
    
    // VariÃ¡veis internas
    private float contadorTempo = 0f;
    private int balasAtuais;
    private bool estaRecarregando = false;
    
    // OTIMIZAÃ‡ÃƒO: Buffer reutilizÃ¡vel para evitar Garbage Collection â€” PRIVADO por instÃ¢ncia, sem compartilhamento
    private Collider[] bufferColisores = new Collider[40]; 

    [Header("PeÃ§as")]
    [Tooltip("A base que gira para os lados (Eixo Y).")]
    public Transform pecaQueGira; 
    [Tooltip("Opcional: A parte que levanta e abaixa (Eixo X). Deixe vazio para a base inclinar inteira.")]
    public Transform canosDaTorreta; 
    public Transform[] locaisDoTiro;  
    public GameObject municaoPrefab; 
    
    [Header("Limites de RotaÃ§Ã£o Cima/Baixo (Pitch)")]
    public bool limitarInclinacao = true;
    [Range(-90, 90)] public float elevacaoMinima = -10f;
    [Range(-90, 90)] public float elevacaoMaxima = 80f; 

    [Header("Efeitos")]
    public AudioClip somTiro;
    public AudioClip somRecarga; 
    public ParticleSystem fogoCano;
    // Cada torreta tem seu PRÃ“PRIO AudioSource â€” nunca compartilhado com o navio pai
    private AudioSource fonteAudio;

    private Transform alvoAtual;
    private int indiceBarrilAtual = 0; 
    
    private float rotacaoXOriginal;
    private float rotacaoYOriginal;
    private float rotacaoZOriginal;
    private float giroPitchAlvo = 0f;

    // --- RASTREIO DE VELOCIDADE PARA PREDIÃ‡ÃƒO ---
    private Transform alvoAnteriorParaCalculo;
    private Vector3 ultimaPosicaoAlvo;
    private Vector3 velocidadeCalculadaAlvo;

    // Visualizador de Alcance
    private LineRenderer linhaDeAlcance;
    private ControleUnidade meuControle;

    // Cache da identidade do navio dono â€” calculado UMA VEZ no Start para evitar
    // buscas repetidas em ProcurarAlvo (que roda 2.5x por segundo por torreta)
    private IdentidadeUnidade minhaIdentidade;
    private int meuTime = -1;

    // Flags de perfil calculadas UMA VEZ (evita string.Contains() todo frame)
    private bool souAntiAereo;
    private bool diagnosticoLocaisDoTiroEmitido;
    private bool bloquearRotacaoAutomatica;

    void Start()
    {
        // Busca o controle do navio pai
        meuControle = GetComponentInParent<ControleUnidade>();
        if (meuControle == null) meuControle = GetComponent<ControleUnidade>();

        // Cache da identidade â€” UMA vez, evita busca em ProcurarAlvo (roda 2.5x/s por torreta)
        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        meuTime = (minhaIdentidade != null) ? minhaIdentidade.teamID : 1;

        // Perfil anti-aÃ©reo determinado UMA vez
        souAntiAereo = DeterminarSouAntiAereo();

        balasAtuais = tamanhoCartucho;
        misseisAtuais = capacidadeMisseis;

        // AudioSource DESTA torreta (nÃ£o do navio pai) â€” AddComponent garante independÃªncia
        fonteAudio = GetComponent<AudioSource>();
        if (fonteAudio == null) fonteAudio = gameObject.AddComponent<AudioSource>();
        fonteAudio.spatialBlend = 1f;
        
        Helicoptero helicopteroPai = GetComponentInParent<Helicoptero>();
        if (helicopteroPai != null && pecaQueGira == null && canosDaTorreta == null)
        {
            bloquearRotacaoAutomatica = true;
        }

        if (pecaQueGira == null) pecaQueGira = transform;
        
        rotacaoXOriginal = pecaQueGira.localEulerAngles.x;
        rotacaoYOriginal = pecaQueGira.localEulerAngles.y;
        rotacaoZOriginal = pecaQueGira.localEulerAngles.z;

        // Offset aleatÃ³rio entre 0 e 0.5s â€” evita que todas as torretas disparem e busquem
        // alvos no mesmo frame exato, distribuindo a carga de CPU
        float inicioAleatorio = Random.Range(0f, 0.5f);
        InvokeRepeating(nameof(ProcurarAlvo), inicioAleatorio, 0.4f);

        CriarVisualizadorAlcance();

        GarantirLocaisDeTiro();
    }

    bool DeterminarSouAntiAereo()
    {
        string nomeBase = transform.root.name.ToLower();
        string nomeObj  = transform.name.ToLower();
        return etiquetaAlvo.Equals("Aereo", System.StringComparison.OrdinalIgnoreCase) ||
               etiquetaAlvo.Equals("Areo",  System.StringComparison.OrdinalIgnoreCase) ||
               nomeBase.Contains("ares")       || nomeBase.Contains("antiaerea") ||
               nomeBase.Contains("ciws")       || nomeBase.Contains("sam")  ||
               nomeObj.Contains("ares")        || nomeObj.Contains("antiaerea") ||
               nomeObj.Contains("ciws")        || nomeObj.Contains("sam");
    }

    Transform ResolverTransformAlvo(Transform alvo)
    {
        if (alvo == null) return null;

        SistemaDeDanos vida = alvo.GetComponentInParent<SistemaDeDanos>();
        if (vida != null) return vida.transform;

        ControleAviao aviao = alvo.GetComponentInParent<ControleAviao>();
        if (aviao != null) return aviao.transform;

        Helicoptero helicoptero = alvo.GetComponentInParent<Helicoptero>();
        if (helicoptero != null) return helicoptero.transform;

        IdentidadeUnidade identidade = alvo.GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null) return identidade.transform;

        return alvo.root != null ? alvo.root : alvo;
    }

    bool EhMissilReal(Transform alvo)
    {
        if (alvo == null) return false;
        if (TagSafe.Matches(alvo.gameObject, "Missil")) return true;

        if (alvo.GetComponentInParent<MissileThreatTracker>() != null) return true;
        if (alvo.GetComponentInParent<MisselCaca>() != null) return true;
        if (alvo.GetComponentInParent<MissilTeleguiado>() != null) return true;
        if (alvo.GetComponentInParent<MisselICBM>() != null) return true;
        if (alvo.GetComponentInParent<MisselNaval>() != null) return true;
        if (alvo.GetComponentInParent<MisselSubmarino>() != null) return true;
        if (alvo.GetComponentInParent<MisselTatico>() != null) return true;
        if (alvo.GetComponentInParent<MisselLeopardAutomatico>() != null) return true;
        return false;
    }

    Transform ResolverAtiradorAereoDeProjetil(Collider hit)
    {
        if (hit == null) return null;

        Projetil projetil = hit.GetComponentInParent<Projetil>();
        if (projetil == null) return null;
        if (EhMissilReal(projetil.transform)) return null;

        GameObject donoProjetil = projetil.GetDono();
        if (donoProjetil == null) return null;

        ControleAviao aviao = donoProjetil.GetComponentInParent<ControleAviao>();
        if (aviao != null) return aviao.transform;

        Helicoptero helicoptero = donoProjetil.GetComponentInParent<Helicoptero>();
        if (helicoptero != null) return helicoptero.transform;

        return null;
    }

    Vector3 CalcularVelocidadeInicialMissel(Transform saida, Vector3 posicaoAlvo)
    {
        if (saida == null) return transform.forward * 40f;

        Vector3 direcaoInicial = posicaoAlvo - saida.position;
        if (direcaoInicial.sqrMagnitude <= 0.001f)
            direcaoInicial = saida.forward.sqrMagnitude > 0.001f ? saida.forward : transform.forward;

        direcaoInicial.Normalize();

        Rigidbody rbLancador = transform.root.GetComponent<Rigidbody>();
        Vector3 velocidadeBase = (rbLancador != null && !rbLancador.isKinematic) ? rbLancador.linearVelocity : Vector3.zero;

        if (velocidadeBase.sqrMagnitude < 25f)
            velocidadeBase = direcaoInicial * 40f;
        else
            velocidadeBase += direcaoInicial * 25f;

        return velocidadeBase;
    }

    void ConfigurarProjetilComoMissel(GameObject projetilObj, Transform saida, Transform alvo, Vector3 posicaoAlvo)
    {
        if (projetilObj == null || saida == null) return;

        Projetil projetil = projetilObj.GetComponent<Projetil>();
        if (projetil == null) return;

        Vector3 direcao = posicaoAlvo - saida.position;
        if (direcao.sqrMagnitude <= 0.001f)
            direcao = saida.forward.sqrMagnitude > 0.001f ? saida.forward : transform.forward;

        projetil.SetDono(transform.root.gameObject);
        projetil.SetDirecao(direcao.normalized);

        if (alvo != null)
        {
            projetil.SetAlvo(alvo);
            if (projetil.curvaDePerseguicao <= 0f)
                projetil.curvaDePerseguicao = 90f;
        }
    }

    void GarantirLocaisDeTiro()
    {
        locaisDoTiro = FiltrarLocaisValidos(locaisDoTiro);
        if (locaisDoTiro != null && locaisDoTiro.Length > 0) return;

        locaisDoTiro = DescobrirLocaisDeTiroAutomaticos();
        if (locaisDoTiro != null && locaisDoTiro.Length > 0)
        {
            if (!diagnosticoLocaisDoTiroEmitido)
            {
                Debug.LogWarning($"[ControleTorreta] '{gameObject.name}' estava sem locaisDoTiro. Fallback automatico configurado com {locaisDoTiro.Length} ponto(s).", this);
                diagnosticoLocaisDoTiroEmitido = true;
            }
            return;
        }

        if (!diagnosticoLocaisDoTiroEmitido)
        {
            Debug.LogError($"[ControleTorreta] '{gameObject.name}' nao possui locais de tiro designados (locaisDoTiro) e o fallback automatico falhou. A torreta nao vai atirar!", this);
            diagnosticoLocaisDoTiroEmitido = true;
        }
    }

    Transform[] DescobrirLocaisDeTiroAutomaticos()
    {
        var encontrados = new List<Transform>();

        if (fogoCano != null)
        {
            encontrados.Add(fogoCano.transform);
        }

        Transform raizBusca = canosDaTorreta != null ? canosDaTorreta : (pecaQueGira != null ? pecaQueGira : transform);
        foreach (Transform filho in raizBusca.GetComponentsInChildren<Transform>(true))
        {
            if (filho == null || filho == raizBusca) continue;

            string nome = NormalizarNome(filho.name);
            if (nome.Contains("bocadetiro") ||
                nome.Contains("bocadefogo") ||
                nome.Contains("muzzle") ||
                nome.Contains("barrel") ||
                nome.Contains("cano") ||
                nome.Contains("tiro") ||
                nome.Contains("shot") ||
                nome.Contains("fire") ||
                nome.Contains("saida") ||
                nome.Contains("spawn"))
            {
                encontrados.Add(filho);
            }
        }

        Transform[] validos = FiltrarLocaisValidos(encontrados.ToArray());
        if (validos != null && validos.Length > 0) return validos;

        Transform referencia = canosDaTorreta != null ? canosDaTorreta : (pecaQueGira != null ? pecaQueGira : transform);
        Transform fallback = CriarLocalDeTiroFallback(referencia);
        return fallback != null ? new[] { fallback } : null;
    }

    Transform CriarLocalDeTiroFallback(Transform referencia)
    {
        if (referencia == null) referencia = transform;

        Transform existente = referencia.Find("_AutoLocalTiro");
        if (existente != null) return existente;

        GameObject marcador = new GameObject("_AutoLocalTiro");
        Transform ponto = marcador.transform;
        ponto.SetParent(referencia, false);
        ponto.localPosition = CalcularOffsetLocalDeTiro(referencia);
        ponto.localRotation = Quaternion.identity;
        return ponto;
    }

    Vector3 CalcularOffsetLocalDeTiro(Transform referencia)
    {
        Renderer render = referencia.GetComponentInChildren<Renderer>();
        if (render != null)
        {
            Bounds bounds = render.bounds;
            float frente = Mathf.Max(1.5f, bounds.extents.z + 0.5f);
            float altura = Mathf.Max(0.4f, bounds.extents.y * 0.5f);
            Vector3 mundo = referencia.position + referencia.forward * frente + referencia.up * altura;
            return referencia.InverseTransformPoint(mundo);
        }

        return new Vector3(0f, 0.5f, 1.5f);
    }

    static Transform[] FiltrarLocaisValidos(Transform[] origem)
    {
        if (origem == null || origem.Length == 0) return null;

        var validos = new List<Transform>(origem.Length);
        for (int i = 0; i < origem.Length; i++)
        {
            if (origem[i] != null) validos.Add(origem[i]);
        }

        return validos.Count > 0 ? validos.ToArray() : null;
    }

    static string NormalizarNome(string valor)
    {
        return string.IsNullOrEmpty(valor) ? string.Empty : valor.Replace(" ", string.Empty).ToLowerInvariant();
    }

    void CriarVisualizadorAlcance()
    {
        GameObject obj = new GameObject("Alcance_Torreta_UI");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        linhaDeAlcance = obj.AddComponent<LineRenderer>();
        linhaDeAlcance.useWorldSpace = true;
        
        Material mat = new Material(Shader.Find("Sprites/Default")); 
        Color corAmarela = Color.yellow; corAmarela.a = 0.5f; 
        linhaDeAlcance.material = mat;
        linhaDeAlcance.startColor = corAmarela; linhaDeAlcance.endColor = corAmarela;
        linhaDeAlcance.startWidth = 1.0f; linhaDeAlcance.endWidth = 1.0f;
        linhaDeAlcance.positionCount = 51;
        linhaDeAlcance.enabled = false;
    }

    void AtualizarVisualizadorAlcance()
    {
        if (linhaDeAlcance == null) return;
        
        bool deveMostrar = (meuControle != null && meuControle.selecionado);
        linhaDeAlcance.enabled = deveMostrar;
        
        if (deveMostrar)
        {
            float angulo = 0f;
            for (int i = 0; i <= 50; i++)
            {
                float x = Mathf.Sin(angulo) * alcance;
                float z = Mathf.Cos(angulo) * alcance;
                Vector3 pos = new Vector3(transform.position.x + x, transform.position.y + 0.5f, transform.position.z + z);
                linhaDeAlcance.SetPosition(i, pos);
                angulo += (2 * Mathf.PI) / 50;
            }
        }
    }

    [Tooltip("Se ativado, a torreta nÃ£o ataca automaticamente.")]
    public bool modoPassivo = false;

    [Header("Radar e Ociosidade")]
    [Tooltip("Se ativado, a torreta fica girando 360Âº quando nÃ£o tem alvos (estilo radar). Se desativado, ela volta para a frente.")]
    public bool modoRadar = false;
    
    [Header("Defesa Anti-MÃ­ssil")]
    [Tooltip("Pode interceptar mÃ­sseis inimigos no ar?")]
    public bool interceptarMisseis = false;
    [Tooltip("Se ativado, dispara um mÃ­ssil (Armamento SecundÃ¡rio) em vez de balas para abater a ameaÃ§a.")]
    public bool usarMisselParaInterceptar = true;

    void ProcurarAlvo()
    {
        if (modoPassivo) 
        {
            alvoAtual = null;
            return;
        }

        int quantidadeEncontrada = Physics.OverlapSphereNonAlloc(transform.position, alcance, bufferColisores, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        // Usa o meuTime cacheado no Start â€” sem GetComponentInParent a cada chamada
        for (int i = 0; i < quantidadeEncontrada; i++)
        {
            Collider hit = bufferColisores[i];
            if (hit == null) continue;

            Transform alvoTr = hit.transform;
            Transform alvoSubstitutoAereo = ResolverAtiradorAereoDeProjetil(hit);
            if (alvoSubstitutoAereo != null) alvoTr = alvoSubstitutoAereo;
            if (!ControleSubmarino.PodeSerAlvoConvencional(alvoTr)) continue;

            // Ignora qualquer coisa que seja parte do mesmo navio/veÃ­culo raiz
            if (alvoTr.root == transform.root) continue;

            bool ehMissil = EhMissilReal(alvoTr);

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
                    if (TagSafe.Matches(hit, etiquetaAlvo) || TagSafe.Matches(hit, "Inimigo"))
                        ehInimigo = true;
                }
            }

            if (ehInimigo)
            {
                IdentidadeUnidade idAlvo = alvoTr.GetComponentInParent<IdentidadeUnidade>();
                
                bool alvoAereo = ehMissil ||
                                 alvoTr.position.y > 6f ||
                                 (idAlvo != null && idAlvo.tipoUnidade == TipoUnidade.Aereo) ||
                                 TagSafe.Matches(alvoTr, "Aereo") || 
                                 TagSafe.Matches(alvoTr, "Areo") ||
                                 alvoTr.GetComponentInParent<ControleAviao>() != null ||
                                 alvoTr.GetComponentInParent<Helicoptero>() != null;

                if (!alvoAereo)
                {
                    // Evitar criar string .name repetidamente se possível, usando Contains ordinal para maior rapidez
                    string nm = alvoTr.name;
                    alvoAereo = nm.Contains("aviao", System.StringComparison.OrdinalIgnoreCase) || 
                                nm.Contains("heli", System.StringComparison.OrdinalIgnoreCase) || 
                                nm.Contains("caca", System.StringComparison.OrdinalIgnoreCase) ||
                                nm.Contains("drone", System.StringComparison.OrdinalIgnoreCase) ||
                                nm.Contains("vap", System.StringComparison.OrdinalIgnoreCase) ||
                                nm.Contains("bombard", System.StringComparison.OrdinalIgnoreCase);
                }
                
                if (souAntiAereo) { if (!alvoAereo) continue; }
                else              { if (alvoAereo)  continue; }

                Vector3 pontoMaisProximo = hit.ClosestPoint(transform.position);
                float dist = (transform.position - pontoMaisProximo).sqrMagnitude;
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    melhorAlvo = ResolverTransformAlvo(alvoTr);
                }
            }
        }

        // Limpa o buffer para nÃ£o vazar referÃªncias entre chamadas
        for (int i = 0; i < quantidadeEncontrada; i++) bufferColisores[i] = null;
        alvoAtual = melhorAlvo;
    }
    
    public void DefinirModoAtivo(bool ativo)
    {
        modoPassivo = !ativo; 
        if (modoPassivo) alvoAtual = null;
    }

    Vector3 ObterPosicaoPreditaAlvo(Transform alvoReferencia = null)
    {
        Transform alvoRef = alvoReferencia != null ? alvoReferencia : alvoAtual;
        if (alvoRef == null) return transform.position;
        Vector3 alvoPosicao = alvoRef.position;

        float velBala = 200f; 
        if (municaoPrefab != null)
        {
            Projetil proj = municaoPrefab.GetComponent<Projetil>();
            if (proj != null && proj.velocidade > 0f) velBala = proj.velocidade;
            
            if (municoesPorCano != null && indicacaoSeguraDeBala < municoesPorCano.Length)
            {
                 if (municoesPorCano[indicacaoSeguraDeBala] != null)
                 {
                     Projetil p2 = municoesPorCano[indicacaoSeguraDeBala].GetComponent<Projetil>();
                     if (p2 != null && p2.velocidade > 0f) velBala = p2.velocidade;
                 }
            }
        }

        Vector3 targetVel = velocidadeCalculadaAlvo;

        if (targetVel.magnitude < 0.1f)
        {
            Rigidbody rb = alvoRef.GetComponentInParent<Rigidbody>();
            if (rb != null && !rb.isKinematic) 
            {
                targetVel = rb.linearVelocity;
            }
        }

        if (targetVel.magnitude > 0.5f)
        {
            float dist1 = Vector3.Distance(pecaQueGira.position, alvoPosicao);
            float tempoAteAlvo1 = dist1 / velBala;
            
            Vector3 predicaoPrimaria = alvoPosicao + (targetVel * tempoAteAlvo1);
            
            float dist2 = Vector3.Distance(pecaQueGira.position, predicaoPrimaria);
            float tempoAteAlvo2 = dist2 / velBala;
            
            alvoPosicao = alvoPosicao + (targetVel * tempoAteAlvo2);
        }

        return alvoPosicao;
    }

    private int indicacaoSeguraDeBala = 0;

    void Update()
    {
        AtualizarVisualizadorAlcance();

        // --- Recarga de mÃ­sseis ---
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

        // --- Recarga de balas ---
        if (estaRecarregando)
        {
            contadorTempo -= Time.deltaTime;
            if (contadorTempo <= 0f)
            {
                estaRecarregando = false;
                balasAtuais = tamanhoCartucho;
                contadorTempo = 0f; 
            }
            return; // Durante recarga, nÃ£o mira nem atira
        }

        if (alvoAtual != null)
        {
            // Verifica se o alvo ainda existe (pode ter sido destruÃ­do entre frames)
            if (!alvoAtual.gameObject.activeInHierarchy || !ControleSubmarino.PodeSerAlvoConvencional(alvoAtual))
            {
                alvoAtual = null;
                return;
            }

            // --- CÃLCULO DA VELOCIDADE DO ALVO (para prediÃ§Ã£o de lead) ---
            if (alvoAtual != alvoAnteriorParaCalculo)
            {
                alvoAnteriorParaCalculo = alvoAtual;
                ultimaPosicaoAlvo = alvoAtual.position;
                velocidadeCalculadaAlvo = Vector3.zero;
            }
            else if (Time.deltaTime > 0f)
            {
                Vector3 velInst = (alvoAtual.position - ultimaPosicaoAlvo) / Time.deltaTime;
                velocidadeCalculadaAlvo = Vector3.Lerp(velocidadeCalculadaAlvo, velInst, Time.deltaTime * 15f);
                ultimaPosicaoAlvo = alvoAtual.position;
            }

            indicacaoSeguraDeBala = indiceBarrilAtual;

            // --- ROTAÃ‡ÃƒO DA TORRETA ---
            float anguloY = rotacaoYOriginal;
            if (pecaQueGira != null && !bloquearRotacaoAutomatica)
            {
                Vector3 alvoPosicao = ObterPosicaoPreditaAlvo();
                Vector3 direcao = alvoPosicao - pecaQueGira.position;

                // Determina o pai de referÃªncia: ou o pai da peÃ§a ou o navio raiz
                Transform referencia = (pecaQueGira.parent != null) ? pecaQueGira.parent : pecaQueGira;
                Vector3 localDir = referencia.InverseTransformDirection(direcao);

                // Yaw â€” espaÃ§o local do PAI da peÃ§a que gira (correto para mÃºltiplas torretas em posiÃ§Ãµes diferentes)
                anguloY = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                if (limitarRotacao) anguloY = Mathf.Clamp(anguloY, anguloMinimo, anguloMaximo);

                // Pitch
                float distanciaPlana = new Vector2(localDir.x, localDir.z).magnitude;
                giroPitchAlvo = -Mathf.Atan2(localDir.y, distanciaPlana) * Mathf.Rad2Deg;
                if (limitarInclinacao) giroPitchAlvo = Mathf.Clamp(giroPitchAlvo, -elevacaoMaxima, -elevacaoMinima);

                if (canosDaTorreta != null)
                {
                    // Base gira sÃ³ no Yaw
                    Quaternion rotacaoBase = Quaternion.Euler(0f, anguloY, 0f);
                    pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoBase, Time.deltaTime * velocidadeGiro);

                    // Cano gira sÃ³ no Pitch (mantÃ©m Y e Z locais zerados)
                    Quaternion rotacaoCanos = Quaternion.Euler(giroPitchAlvo, 0f, 0f);
                    canosDaTorreta.localRotation = Quaternion.Lerp(canosDaTorreta.localRotation, rotacaoCanos, Time.deltaTime * velocidadeGiro);
                }
                else
                {
                    // PeÃ§a Ãºnica: aplica Yaw + Pitch juntos
                    Quaternion rotacaoTotal = Quaternion.Euler(giroPitchAlvo, anguloY, rotacaoZOriginal);
                    pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoTotal, Time.deltaTime * velocidadeGiro);
                }
            }

            // --- DISPARO AUTÃ”NOMO ---
            contadorTempo -= Time.deltaTime;
            if (contadorTempo <= 0f)
            {
                // Verifica alinhamento em ESPAÃ‡O LOCAL do pai â€” mÃºltiplas torretas no
                // mesmo navio usam sua prÃ³pria frente local, nÃ£o se confundem entre si
                bool podeDisparar;

                if (pecaQueGira != null && pecaQueGira.parent != null)
                {
                    // DireÃ§Ã£o ao alvo no espaÃ§o local do pai da torreta
                    Vector3 alvoPosicao = ObterPosicaoPreditaAlvo();
                    Vector3 dirMundo    = (alvoPosicao - pecaQueGira.position);
                    Vector3 dirLocal    = pecaQueGira.parent.InverseTransformDirection(dirMundo);

                    // anguloY Ã© a rotaÃ§Ã£o desejada; compara com a rotaÃ§Ã£o atual da peÃ§a
                    float anguloAtualY  = pecaQueGira.localEulerAngles.y;
                    // Normaliza para [-180, 180]
                    if (anguloAtualY > 180f) anguloAtualY -= 360f;

                    float diff = Mathf.Abs(Mathf.DeltaAngle(anguloAtualY, anguloY));

                    // Para anti-aÃ©reo toleramos mais erro (alvo rÃ¡pido); para terrestre menos
                    float tolerancia = souAntiAereo ? 45f : 8f;
                    podeDisparar = (diff < tolerancia);
                }
                else
                {
                    // Fallback: compara frente mundial com direÃ§Ã£o mundial ao alvo
                    Vector3 alvoPosicao = ObterPosicaoPreditaAlvo();
                    Vector3 dirAlvo = (alvoPosicao - pecaQueGira.position);
                    dirAlvo.y = 0f;
                    Vector3 minhaFrente = pecaQueGira.forward;
                    minhaFrente.y = 0f;
                    float tolerancia = souAntiAereo ? 45f : 8f;
                    podeDisparar = Vector3.Angle(minhaFrente, dirAlvo) < tolerancia;
                }

                if (podeDisparar)
                {
                    Disparar();
                    if (!estaRecarregando) contadorTempo = tempoEntreTiros;
                }
            }
        }
        else
        {
            // Modo ocioso â€” gira como radar
            ModoOcioso();
        }
    }

    void ModoOcioso()
    {
        if (pecaQueGira == null || bloquearRotacaoAutomatica) return;

        if (modoRadar && !limitarRotacao)
        {
            // Comportamento de RADAR (Girando 360Âº)
            float anguloLivre = (Time.time * 20f) % 360f;
            pecaQueGira.localRotation = Quaternion.Euler(rotacaoXOriginal, anguloLivre, rotacaoZOriginal);
        }
        else
        {
            // Comportamento de REPOUSO (Retorna para a frente/posiÃ§Ã£o original)
            // Calculamos a rotaÃ§Ã£o de "descanso" usando os valores originais capturados no Start
            Quaternion rotacaoDescanso = Quaternion.Euler(rotacaoXOriginal, rotacaoYOriginal, rotacaoZOriginal);
            pecaQueGira.localRotation = Quaternion.Lerp(pecaQueGira.localRotation, rotacaoDescanso, Time.deltaTime * (velocidadeGiro * 0.5f));

            // Se tiver canos independentes, tambÃ©m volta eles para o horizonte
            if (canosDaTorreta != null)
            {
                canosDaTorreta.localRotation = Quaternion.Lerp(canosDaTorreta.localRotation, Quaternion.identity, Time.deltaTime * (velocidadeGiro * 0.5f));
            }
        }
    }

    [Header("Armamento SecundÃ¡rio (MÃ­sseis)")]
    [Tooltip("Se definido, usa este prefab para disparos especiais ou de longo alcance.")]
    public GameObject misselPrefab;
    public Transform[] locaisDoMissel; 
    public AudioClip somMissel;
    public float tempoEntreMisseis = 2.0f;
    
    [Tooltip("Quantidade mÃ¡xima de mÃ­sseis antes de precisar recarregar.")]
    public int capacidadeMisseis = 4;
    [Tooltip("Tempo em segundos para reabastecer os mÃ­sseis.")]
    public float tempoRecargaMisseis = 10f;
    
    private int misseisAtuais;
    private bool estaRecarregandoMisseis = false;
    private float contadorRecargaMissel = 0f;
    private float cooldownMissel = 0f;

    [Header("CustumizaÃ§Ã£o de Disparo")]
    [Tooltip("Se quiser muniÃ§Ãµes diferentes para canos diferentes, arraste aqui na ordem dos Locais Do Tiro.")]
    public GameObject[] municoesPorCano; 

    void Disparar()
    {
        bool alvoEhMissil = EhMissilReal(alvoAtual);

        // 1. DISPARO DE MÃSSIL (Arma Pesada ou Interceptador)
        if (misselPrefab != null && cooldownMissel <= 0f && !estaRecarregandoMisseis && misseisAtuais > 0 && alvoAtual != null)
        {
            if (!alvoEhMissil || (alvoEhMissil && usarMisselParaInterceptar))
            {
                DispararMisselCorrigido();
                cooldownMissel = tempoEntreMisseis;
                misseisAtuais--;

                if (misseisAtuais <= 0)
                {
                    estaRecarregandoMisseis = true;
                    contadorRecargaMissel = tempoRecargaMisseis;
                }
                
                if (alvoEhMissil && usarMisselParaInterceptar) return;
                if (!alvoEhMissil) return; 
            }
        }

        // 2. DISPARO PADRÃƒO (Metralhadora/CanhÃ£o/CIWS)
        GarantirLocaisDeTiro();
        if (locaisDoTiro != null && locaisDoTiro.Length > 0)
        {
            GameObject prefabParaUsar = municaoPrefab;
            
            if (municoesPorCano != null && indiceBarrilAtual < municoesPorCano.Length)
            {
                if (municoesPorCano[indiceBarrilAtual] != null)
                    prefabParaUsar = municoesPorCano[indiceBarrilAtual];
            }

            if (prefabParaUsar == null) return;
            if (indiceBarrilAtual >= locaisDoTiro.Length) indiceBarrilAtual = 0;

            Transform barrilDaVez = locaisDoTiro[indiceBarrilAtual];

            if (barrilDaVez == null)
            {
                Debug.LogWarning($"[ControleTorreta] O local de tiro no Ã­ndice {indiceBarrilAtual} estÃ¡ nulo em '{gameObject.name}'. Pulando disparo.", this);
                indiceBarrilAtual = (indiceBarrilAtual + 1) % locaisDoTiro.Length;
                return;
            }

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
                    if (scriptBala.velocidade == 0) scriptBala.velocidade = 200f; 
                }
            }

            if (somTiro != null && fonteAudio != null) fonteAudio.PlayOneShot(somTiro);

            indiceBarrilAtual++;
            if (indiceBarrilAtual >= locaisDoTiro.Length) indiceBarrilAtual = 0;

            balasAtuais--;
            if (balasAtuais <= 0) IniciarRecarga();
        }
    }

    void DispararMisselLegacy()
    {
        Transform[] saidas = (locaisDoMissel != null && locaisDoMissel.Length > 0) ? locaisDoMissel : locaisDoTiro;
        if (saidas == null || saidas.Length == 0) return;
        if (indiceBarrilAtual >= saidas.Length) indiceBarrilAtual = 0;

        Transform saida = saidas[indiceBarrilAtual % saidas.Length];
        if (saida == null)
        {
            Debug.LogWarning($"[ControleTorreta] Local de saida do missil nulo em '{gameObject.name}'.", this);
            return;
        }

        GameObject missel = PoolDeObjetosCombate.Spawn(misselPrefab, saida.position, saida.rotation);
        Transform alvoResolvido = ResolverTransformAlvo(alvoAtual);
        Vector3 posicaoPredita = ObterPosicaoPreditaAlvo(alvoResolvido);
        bool inicializado = false;

        MisselCaca misselCaca = missel.GetComponent<MisselCaca>();
        if (misselCaca != null)
        {
            misselCaca.IniciarAtaque(posicaoPredita, CalcularVelocidadeInicialMissel(saida, posicaoPredita), alvoResolvido);
            inicializado = true;
        }
        else
        {
            MisselNaval misselNaval = missel.GetComponent<MisselNaval>();
            if (misselNaval != null)
            {
                misselNaval.IniciarAtaque(posicaoPredita, alvoResolvido);
                inicializado = true;
            }
            else
            {
                MissilTeleguiado guiadoNovo = missel.GetComponent<MissilTeleguiado>();
                if (guiadoNovo != null)
                {
                    guiadoNovo.DefinirAlvo(alvoResolvido);
                    inicializado = true;
                }
                else
                {
                    MisselICBM icbmNovo = missel.GetComponent<MisselICBM>();
                    if (icbmNovo != null)
                    {
                        icbmNovo.IniciarLancamento(posicaoPredita);
                        inicializado = true;
                    }
                }
            }
        }

        if (!inicializado)
        {
            ConfigurarProjetilComoMissel(missel, saida, alvoResolvido, posicaoPredita);
        }

        if (alvoResolvido != null && EhMissilReal(alvoResolvido))
        {
            AntiMissilDetonadorProximidade detonador = missel.GetComponent<AntiMissilDetonadorProximidade>();
            if (detonador == null) detonador = missel.AddComponent<AntiMissilDetonadorProximidade>();
            detonador.alvo = alvoResolvido;
            detonador.forcarDestruicao = true;
            detonador.distanciaBaseIntercepcao = Mathf.Max(detonador.distanciaBaseIntercepcao, 8f);
        }

        MissileThreatTracker.RegistrarLancamento(missel, this, posicaoPredita, alvoResolvido, MissileThreatTracker.EstimarVelocidade(missel));

        if (somMissel != null && fonteAudio != null) fonteAudio.PlayOneShot(somMissel);
        Debug.Log("[ControleTorreta] Missil disparado.");
    }

    void DispararMisselCorrigido()
    {
        Transform[] saidas = (locaisDoMissel != null && locaisDoMissel.Length > 0) ? locaisDoMissel : locaisDoTiro;
        if (saidas == null || saidas.Length == 0) return;
        if (indiceBarrilAtual >= saidas.Length) indiceBarrilAtual = 0;

        Transform saida = saidas[indiceBarrilAtual % saidas.Length];
        if (saida == null)
        {
            Debug.LogWarning($"[ControleTorreta] Local de saida do missil nulo em '{gameObject.name}'.", this);
            return;
        }

        GameObject missel = PoolDeObjetosCombate.Spawn(misselPrefab, saida.position, saida.rotation);
        Transform alvoResolvido = ResolverTransformAlvo(alvoAtual);
        Vector3 posicaoPredita = ObterPosicaoPreditaAlvo(alvoResolvido);
        bool inicializado = false;

        MisselCaca misselCaca = missel.GetComponent<MisselCaca>();
        if (misselCaca != null)
        {
            misselCaca.IniciarAtaque(posicaoPredita, CalcularVelocidadeInicialMissel(saida, posicaoPredita), alvoResolvido);
            inicializado = true;
        }
        else
        {
            MisselNaval misselNaval = missel.GetComponent<MisselNaval>();
            if (misselNaval != null)
            {
                misselNaval.IniciarAtaque(posicaoPredita, alvoResolvido);
                inicializado = true;
            }
            else
            {
                MissilTeleguiado guiado = missel.GetComponent<MissilTeleguiado>();
                if (guiado != null)
                {
                    guiado.DefinirAlvo(alvoResolvido);
                    inicializado = true;
                }
                else
                {
                    MisselICBM icbm = missel.GetComponent<MisselICBM>();
                    if (icbm != null)
                    {
                        icbm.IniciarLancamento(posicaoPredita);
                        inicializado = true;
                    }
                }
            }
        }

        if (!inicializado)
            ConfigurarProjetilComoMissel(missel, saida, alvoResolvido, posicaoPredita);

        if (alvoResolvido != null && EhMissilReal(alvoResolvido))
        {
            AntiMissilDetonadorProximidade detonador = missel.GetComponent<AntiMissilDetonadorProximidade>();
            if (detonador == null) detonador = missel.AddComponent<AntiMissilDetonadorProximidade>();
            detonador.alvo = alvoResolvido;
            detonador.forcarDestruicao = true;
            detonador.distanciaBaseIntercepcao = Mathf.Max(detonador.distanciaBaseIntercepcao, 8f);
        }

        MissileThreatTracker.RegistrarLancamento(missel, this, posicaoPredita, alvoResolvido, MissileThreatTracker.EstimarVelocidade(missel));

        if (somMissel != null && fonteAudio != null) fonteAudio.PlayOneShot(somMissel);
        Debug.Log("Missil Disparado!");
    }

    void IniciarRecarga()
    {
        estaRecarregando = true;
        contadorTempo = tempoRecarga;
        if (somRecarga != null && fonteAudio != null) fonteAudio.PlayOneShot(somRecarga);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alcance);
    }
}

