using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems; 
using System.Collections;
using System.Collections.Generic;

// [HELICÓPTERO TÁTICO V19.5 - DEBUG DE INICIALIZAÇÃO]
// - Adicionei um aviso no Start() para garantir que o script está vivo.

public class Helicoptero : MonoBehaviour
{
    [Header("--- DEBUG ---")]
    public bool debugLogs = false;

    [Header("--- CONTROLES ---")]
    public bool controleSempreAtivo = false; 

    [Header("--- DEBUG (Estado Atual) ---")]
    public bool selecionado = false;

    [Header("--- SENSIBILIDADE DO CLIQUE ---")]
    [Tooltip("Distância máxima do centro do helicóptero para aceitar o clique.")]
    public float raioDoClique = 7.0f; 

    [Header("--- VOO ---")]
    public float altitudeDeVoo = 14f;       
    public float alturaPouso = 1.33f; 
    public float velocidadeHelice = 1200f;  
    public float velocidadeNavegacao = 20f; 
    public float velocidadePouso = 4f; 
    
    [Header("--- TRANSPORTE (U / P) ---")]
    public float distanciaBusca = 50f; 
    public float distanciaEmbarque = 4.0f; 
    public int capacidadeMaxima = 8;
    public string tagAlvo = "Soldado"; 
    public List<GameObject> soldadosEmbarcados = new List<GameObject>();

    [Header("--- COMBATE & DEFESA (K / O) ---")]
    public bool modoCombateAtivo = false; 
    public float raioRadarMissil = 60f;
    public float cooldownFlares = 10f;
    public string tagMissil = "Missil";
    public string tagInimigo = "Inimigo"; 

    [Header("--- VISUAL ---")]
    public Transform modeloVisual;
    public float ajusteYawModelo = 0f;
    public ParticleSystem[] flares;
    public Transform helicePrincipal;
    public Transform heliceTraseira;

    [Header("--- ÁUDIO ---")]
    public AudioSource audioMotor;
    public float pitchMinimo = 0.5f;
    public float pitchMaximo = 1.2f;
    public float volumeMaximo = 1.0f;
    public float tempoSpinUp = 4.0f; // Tempo para ligar motor/hélices
    public float tempoSpinDown = 6.0f; // Tempo para parar

    // ESTADOS INTERNOS
    private float velocidadeAtualHelice = 0.0f; // Para lerp suave
    public Vector3 destino;
    public bool estaVoando = false;
    private bool estaPousando = false;
    private bool motorLigado = false;
    private float timerInatividade = 0f;
    private float timerRecargaFlares = 0f;

    // COMPATIBILIDADE
    [HideInInspector] public string nomeHelicoptero = "Falcão Negro"; 
    [HideInInspector] public int custoUpgrade = 800;  
    private bool disponivelParaPatrulha = true; 
    private IdentidadeUnidade identidade;
    private Quaternion rotacaoLocalModeloBase = Quaternion.identity;

    // OTIMIZAÇÃO DE PERFORMANCE: Cache global dos helicópteros vivos
    private static List<Helicoptero> todosHelicopteros = new List<Helicoptero>();

    void LogDebug(string msg)
    {
        if (debugLogs)
            Debug.Log(msg);
    }

    void OnEnable() { if(!todosHelicopteros.Contains(this)) todosHelicopteros.Add(this); }
    void OnDisable() { todosHelicopteros.Remove(this); }

    void Awake()
    {
        selecionado = false;
        controleSempreAtivo = false; 
        if(flares != null)
        {
            foreach(var f in flares)
            {
                if(f) { var m = f.main; m.playOnAwake = false; f.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            }
        }
    }

    void Start()
    {
        // --- LOG DE VIDA ---
        if (debugLogs)
            LogDebug($"🚁 SISTEMA DO HELICÓPTERO INICIADO NO OBJETO: {name}");
        // -------------------

        identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();

        selecionado = false;
        controleSempreAtivo = false;
        destino = transform.position;
        
        if(!helicePrincipal) helicePrincipal = transform.Find("helice_principal") ?? transform.Find("MainRotor") ?? transform.Find("helice") ?? EncontrarFilhoPorNomeParcial("mainpropel") ?? EncontrarFilhoPorNomeParcial("helice");
        if(!heliceTraseira) heliceTraseira = transform.Find("helice_traseira") ?? transform.Find("TailRotor") ?? transform.Find("helice_atras") ?? EncontrarFilhoPorNomeParcial("tail") ?? EncontrarFilhoPorNomeParcial("helice_atras");
        ConfigurarModeloVisual();

        if(!audioMotor) audioMotor = GetComponent<AudioSource>();
        if(audioMotor)
        {
            audioMotor.loop = true;
            audioMotor.playOnAwake = false;
            audioMotor.volume = 0;
            audioMotor.pitch = pitchMinimo;
        }

        StartCoroutine(RadarDeAmeacas());
    }

    void Update()
    {
        if (timerRecargaFlares > 0) timerRecargaFlares -= Time.deltaTime;

        GestaoDeInput(); 
        
        if (estaVoando) ProcessarMovimento();
        
        // Controle Suave de Motor e Hélices
        ControlarMotorEHelices();

        VerificarInatividade();
    }

    void ControlarMotorEHelices()
    {
        // Alvo de velocidade: Se motor ligado, 1 (100%). Se não, 0.
        float target = motorLigado ? 1.0f : 0.0f;
        float speed = motorLigado ? (1.0f / tempoSpinUp) : (1.0f / tempoSpinDown);
        
        velocidadeAtualHelice = Mathf.MoveTowards(velocidadeAtualHelice, target, speed * Time.deltaTime);

        // --- HÉLICES ---
        float rotacao = velocidadeAtualHelice * velocidadeHelice * Time.deltaTime;
        if(helicePrincipal) helicePrincipal.Rotate(0, rotacao, 0);
        if(heliceTraseira) heliceTraseira.Rotate(Vector3.right * rotacao, Space.Self);

        // --- ÁUDIO ---
        if(audioMotor)
        {
            if(velocidadeAtualHelice > 0.01f)
            {
                if(!audioMotor.isPlaying) audioMotor.Play();
                
                audioMotor.volume = Mathf.Lerp(0, volumeMaximo, velocidadeAtualHelice);
                audioMotor.pitch = Mathf.Lerp(pitchMinimo, pitchMaximo, velocidadeAtualHelice);
            }
            else
            {
                if(audioMotor.isPlaying) audioMotor.Stop();
            }
        }
    }

    void GestaoDeInput()
    {
        // Se for da IA, não permite controle do jogador
        if (identidade != null && identidade.teamID != 1 && !controleSempreAtivo) return;
        if (Construtor.EmModoConstrucaoAtivo) return;

        // 1. CLIQUE ESQUERDO (Seleção)
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Ignora triggers (como radares) para focar na física sólida
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    float distanciaDoCentro = Vector3.Distance(hit.point, transform.position);

                    if (distanciaDoCentro <= raioDoClique)
                    {
                        selecionado = true;
                        LogDebug($"✅ {name} SELECIONADO.");
                    }
                    else
                    {
                        selecionado = false;
                        LogDebug($"🚫 Ignorado (Muito longe: {distanciaDoCentro:F1}m)");
                    }
                }
                else
                {
                    if(selecionado)
                    {
                        selecionado = false;
                        LogDebug("🚫 Deselecionado.");
                    }
                }
            }
            else
            {
                if(selecionado) selecionado = false;
            }
        }

        // --- COMANDOS ---
        if (!selecionado) return;

        // CLIQUE DIREITO
        if (Input.GetMouseButtonDown(1))
        {
            Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(r, out RaycastHit h, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) 
            {
                Decolar(h.point);
                LogDebug($"🖱️ [CLIQUE DIREITO] Movendo para {h.point}");

                if (modoCombateAtivo)
                {
                    try 
                    {
                        if (SafeCompareTag(h.collider, tagInimigo) || h.collider.name.IndexOf("inimigo", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            TentativaDisparoAutomatico();
                        }
                    } catch {}
                }
            }
        }

        // TECLA I (Embarque / Pousar para Embarque)
        if (Input.GetKeyDown(KeyCode.I)) 
        {
            if (estaVoando) 
            {
                LogDebug("⌨️ [TECLA I] Baixando aeronave para buscar tropas...");
                estaPousando = true; 
                destino = transform.position; 
            }
            else
            {
                LogDebug("⌨️ [TECLA I] Chamando soldados para embarque...");
                ChamarReforcos();
            }
        }

        // TECLA P (Desembarque / Pousar para Desembarcar)
        if (Input.GetKeyDown(KeyCode.P)) 
        {
            LogDebug("⌨️ [TECLA P] Ordem de Pouso/Desembarque...");
            OrdemPousoOuDesembarque();
        }

        // TECLA O (Flares)
        if (Input.GetKeyDown(KeyCode.O)) 
        {
            LogDebug("⌨️ [TECLA O] Tentando disparar Flares...");
            DispararFlaresManual(); 
        }
    }

    // ... (Resto do código igual) ...

    private Transform EncontrarFilhoPorNomeParcial(string trechoNome)
    {
        if (string.IsNullOrEmpty(trechoNome)) return null;

        string trechoNormalizado = trechoNome.ToLowerInvariant();
        Transform[] filhos = GetComponentsInChildren<Transform>(true);
        foreach (Transform filho in filhos)
        {
            if (filho == null || filho == transform) continue;
            if (filho.name.ToLowerInvariant().Contains(trechoNormalizado))
            {
                return filho;
            }
        }

        return null;
    }

    private void ConfigurarModeloVisual()
    {
        if (modeloVisual == null)
        {
            modeloVisual = transform.Find("Chopper_01") ?? EncontrarFilhoPorNomeParcial("chopper_01");
        }

        if (modeloVisual == null) return;

        rotacaoLocalModeloBase = modeloVisual.localRotation;

        if (Mathf.Approximately(ajusteYawModelo, 0f))
        {
            string nomeHeli = name.ToLowerInvariant();
            string nomeModelo = modeloVisual.name.ToLowerInvariant();
            if (nomeHeli.Contains("vans") || nomeModelo.Contains("chopper"))
            {
                ajusteYawModelo = -90f;
            }
        }

        modeloVisual.localRotation = rotacaoLocalModeloBase * Quaternion.Euler(0f, ajusteYawModelo, 0f);
    }

    IEnumerator RadarDeAmeacas()
    {
        Collider[] buffer = new Collider[48];
        while (true)
        {
            if (estaVoando && modoCombateAtivo && timerRecargaFlares <= 0)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(transform.position, raioRadarMissil, buffer, ~0, QueryTriggerInteraction.UseGlobal);
                for (int i = 0; i < hitCount; i++)
                {
                    Collider h = buffer[i];
                    if (h == null) continue;
                    if (SafeCompareTag(h, tagMissil) || h.name.IndexOf("missil", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        TentativaDisparoAutomatico();
                        break; 
                    }
                }
            }
            yield return new WaitForSeconds(0.5f); 
        }
    }

    void TentativaDisparoAutomatico()
    {
        if (timerRecargaFlares <= 0) DispararFlaresManual();
    }

    private static bool SafeCompareTag(Component component, string tagName)
    {
        if (component == null || string.IsNullOrEmpty(tagName))
        {
            return false;
        }

        try
        {
            return string.Equals(component.tag, tagName, System.StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public void Decolar(Vector3 novoDestino)
    {
        destino = novoDestino;
        estaPousando = false;
        motorLigado = true;
        timerInatividade = 0f;
        disponivelParaPatrulha = false; 

        if (!estaVoando)
        {
            estaVoando = true;
            if(destino.y < altitudeDeVoo) destino.y = altitudeDeVoo;
        }
    }

    void ProcessarMovimento()
    {
        // 1. Descobrir a altura real do chão ou obstáculo (prédios) debaixo do helicóptero
        float alturaChaoTarget = 0f;
        
        // Dispara raios de cima pra baixo (sempre usa transform.position para evitar saltos bruscos se o destino mudar rápido)
        Vector3 pontoBuscaOrigem = transform.position;
        pontoBuscaOrigem.y = 800f; // Bem alto

        RaycastHit[] hits = Physics.RaycastAll(pontoBuscaOrigem, Vector3.down, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.transform.root == transform.root) continue; // Ignora o próprio helicóptero
            if (h.point.y > alturaChaoTarget) alturaChaoTarget = h.point.y;
        }

        // Se estiver voando ele voa sempre X metros ALÉM DO TETO. Se pousando, pouse no teto suavemente.
        float alturaAlvo = estaPousando ? (alturaChaoTarget + alturaPouso) : Mathf.Max(altitudeDeVoo, alturaChaoTarget + altitudeDeVoo);
        
        Vector3 meta = new Vector3(
            estaPousando ? transform.position.x : destino.x, 
            alturaAlvo, 
            estaPousando ? transform.position.z : destino.z
        );

        float vel = estaPousando ? velocidadePouso : velocidadeNavegacao;
        
        // Para subir/descer na vertical (Teto do prédio é dinâmico), suaviza só esse eixo Y se for a única diferença
        transform.position = Vector3.MoveTowards(transform.position, meta, vel * Time.deltaTime);

        if (!estaPousando && Vector3.Distance(transform.position, meta) > 2f)
        {
            Vector3 dir = (new Vector3(meta.x, transform.position.y, meta.z) - transform.position).normalized;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);
        }

        if (estaPousando && Mathf.Abs(transform.position.y - alturaAlvo) < 0.2f)
        {
            Vector3 pos = transform.position; 
            pos.y = alturaAlvo; 
            transform.position = pos;
            estaVoando = false;
            estaPousando = false;
            LogDebug("🚁 Helicóptero pousou taticamente.");
            if(soldadosEmbarcados.Count > 0) EjetarTodos();
            disponivelParaPatrulha = true; 
        }
    }

    void VerificarInatividade()
    {
        if (!estaVoando && motorLigado)
        {
            timerInatividade += Time.deltaTime;
            if (timerInatividade > 10f) motorLigado = false;
        }
    }

    // Lista de soldados que já receberam ordem de embarque e estão a caminho
    private List<GameObject> soldadosChamados = new List<GameObject>();

    // Método estático para outras classes saberem se devem ignorar comandos deste soldado
    // (Totalmente otimizado para não causar Spike de Lag/Queda de FPS ao varrer o mapa inteiro toda vez)
    public static bool SoldadoEstaEmbarcando(GameObject s)
    {
        if (s == null) return false;
        
        // Usa o cache em vez de FindObjectsByType (que trava o jogo se rodar em loops de movimentação do general)
        for (int i = 0; i < todosHelicopteros.Count; i++)
        {
            var h = todosHelicopteros[i];
            if (h != null && h.soldadosChamados.Contains(s)) return true;
        }
        return false;
    }

    public void ChamarReforcos()
    {
        // Se estiver voando e não for processo de pouso, não pode puxar ninguém!
        if (estaVoando && !estaPousando)
        {
            if(selecionado) LogDebug("❌ Helicóptero está voando e não pode embarcar ninguém agora!");
            return;
        }

        // Limpa soldados chamados que foram destruídos ou já embarcaram ou desistiram
        soldadosChamados.RemoveAll(s => s == null || !s.activeInHierarchy || soldadosEmbarcados.Contains(s));

        int espacoLivre = capacidadeMaxima - (soldadosEmbarcados.Count + soldadosChamados.Count);

        if(espacoLivre <= 0) 
        {
            if(selecionado) LogDebug("❌ Helicóptero cheio ou já com carga total a caminho!");
            return; // Já está cheio ou com gente suficiente a caminho
        }

        // GPS: Aumentado em 3x o raio de escaneamento para não deixar ninguém para trás na base gigante!
        Collider[] hits = Physics.OverlapSphere(transform.position, distanciaBusca * 3.0f);
        bool encontrouAlguem = false;

        if(selecionado) LogDebug($"🔍 Procurando soldados em raio expansivo de {distanciaBusca * 3.0f}m...");

        foreach(var h in hits)
        {
            // Abordagem SEGURA: Busca o NavMeshAgent subindo na hierarquia, ignora se não tiver
            var nav = h.GetComponentInParent<NavMeshAgent>();
            if (nav == null) 
            {
                // Não loga todas as pedras do chão sem navmesh pra não inundar o console.
                continue;
            }

            GameObject s = nav.gameObject;

            if(s == gameObject || soldadosEmbarcados.Contains(s) || soldadosChamados.Contains(s)) continue;

            // CHECAGEM DE TIME FLEXÍVEL
            IdentidadeUnidade idSoldado = s.GetComponent<IdentidadeUnidade>();
            if (idSoldado == null) idSoldado = s.GetComponentInChildren<IdentidadeUnidade>();
            
            if (idSoldado != null && identidade != null) 
            {
                // Só barra se ambos tiverem time configurado (>0) e forem de times diferentes. 
                // Isso evita que unidades recém-criadas do Player (Time 0) sejam ignoradas.
                if (idSoldado.teamID > 0 && identidade.teamID > 0 && idSoldado.teamID != identidade.teamID) 
                {
                    if (selecionado) LogDebug($"❌ Rejeitado [{s.name}]: RG diz que é do time inimigo ({idSoldado.teamID}). Nosso time é {identidade.teamID}");
                    continue; 
                }
            }
            else if (identidade != null && idSoldado == null)
            {
                if (selecionado) LogDebug($"⚠️ Atenção [{s.name}]: Não tem IdentidadeUnidade! Aceitando como neutro/nosso.");
            }

            bool tagCorreta = TagSafe.Matches(s, tagAlvo);
            string nm = s.name;
            if(!tagCorreta && (nm.IndexOf("soldado", System.StringComparison.OrdinalIgnoreCase) >= 0 || nm.IndexOf("infant", System.StringComparison.OrdinalIgnoreCase) >= 0)) tagCorreta = true;

            if(!tagCorreta)
            {
                if (selecionado && nm.IndexOf("tanque", System.StringComparison.OrdinalIgnoreCase) < 0 && nm.IndexOf("heli", System.StringComparison.OrdinalIgnoreCase) < 0 && nm.IndexOf("carro", System.StringComparison.OrdinalIgnoreCase) < 0) 
                {
                    LogDebug($"❌ Rejeitado [{s.name}]: Não tem a Tag '{tagAlvo}' nem nome de soldado.");
                }
                continue; // Pula os tanques
            }

            if(tagCorreta) 
            {
                encontrouAlguem = true;
                soldadosChamados.Add(s);
                LogDebug($"[Soldado] [{s.name}] ACEITO! Ordenando correr para o helicoptero!");
                StartCoroutine(RotinaEmbarque(s, nav));

                espacoLivre--;
                if (espacoLivre <= 0) break; // Atingiu o limite de pessoas a chamar
            }
        }

        if(!encontrouAlguem && soldadosChamados.Count == 0 && selecionado) LogDebug($"❌ Busca concluída: ZERO soldados livres e detectáveis no raio perto ({distanciaBusca}m).");
    }

    IEnumerator RotinaEmbarque(GameObject s, NavMeshAgent nav)
    {
        if(s == null || nav == null) yield break;

        LogDebug($"[Helicoptero] Iniciando embarque de {s.name}...");
        if (nav.isOnNavMesh) nav.isStopped = false; 
        nav.speed = 12f; // Acelera o soldado para correr até o heli (Opcional, mas ajuda no gameplay)

        // Busca o ponto real no chão abaixo do helicóptero para evitar bugs na IA
        Vector3 destinoChao = new Vector3(transform.position.x, s.transform.position.y, transform.position.z);
        if (NavMesh.SamplePosition(destinoChao, out NavMeshHit hitM, 20f, NavMesh.AllAreas)) 
            destinoChao = hitM.position;

        if (nav.isOnNavMesh) nav.SetDestination(destinoChao);

        float timeout = 25.0f; // Tempo máximo dilatado para tentar embarcar (o jogo tem colisões grossas)
        float timer = 0f;
        float proxAtualizacao = 0f;

        while(s != null && s.activeInHierarchy && timer < timeout)
        {
            if (estaVoando && !estaPousando) 
            {
                LogDebug($"[Helicoptero] {name} decolou! Cancelando embarque de {s.name}.");
                break;
            }

            timer += Time.deltaTime;

            // Atualiza destino periodicamente, mas de forma limpa! (1x por segundo)
            if (timer >= proxAtualizacao)
            {
                 destinoChao = new Vector3(transform.position.x, s.transform.position.y, transform.position.z);
                 if (NavMesh.SamplePosition(destinoChao, out NavMeshHit pNovo, 20f, NavMesh.AllAreas)) destinoChao = pNovo.position;

                 if (nav.isOnNavMesh) nav.SetDestination(destinoChao);
                 proxAtualizacao = timer + 1.0f;
            }

            // Distância Horizontal (Ignora altura)
            float distHorizontal = Vector2.Distance(
                new Vector2(s.transform.position.x, s.transform.position.z), 
                new Vector2(transform.position.x, transform.position.z)
            );

            if(distHorizontal <= distanciaEmbarque) 
            {
                break; // Chegou!
            }
            
            // Se estiver perto mas travado, considera embarcado (Aumentada a tolerância para distâncias falsas de colisão)
            if (distHorizontal < distanciaEmbarque * 2.0f && nav.velocity.sqrMagnitude < 0.1f && timer > 2.0f)
            {
                 break;
            }

            yield return null; 
        }

        if(s != null && soldadosEmbarcados.Count < capacidadeMaxima)
        {
            // Verifica novamente a distância final para não pegar gente de muito longe se o timeout estourou
             float distFinal = Vector2.Distance(
                new Vector2(s.transform.position.x, s.transform.position.z), 
                new Vector2(transform.position.x, transform.position.z)
            );

            // Proteção Final: Se o soldado chegou perto, ele entra, APENAS se o helicóptero ainda estiver no solo!
            bool pertoBastante = distFinal <= 15f; 

            if (pertoBastante && soldadosEmbarcados.Count < capacidadeMaxima && (!estaVoando || estaPousando))
            {
                soldadosEmbarcados.Add(s);
                EsconderSoldado(s); // Esconde sem desativar o GameObject (mantém NavMeshAgent no NavMesh)
                LogDebug($"[Helicoptero] {s.name} embarcou com sucesso! (Dist: {distFinal:F1}m)");
            }
            else
            {
                LogDebug($"[Helicoptero] {s.name} falhou em embarcar (Dist: {distFinal:F1}m). Perto o bastante: {pertoBastante}, Voando: {estaVoando}");
            }
        }
        
        // Sempre liberar a vaga da fila de chamadas quando a tentativa terminar (seja sucesso ou fracasso)
        if (soldadosChamados.Contains(s)) soldadosChamados.Remove(s);
    }

    public void OrdemPousoOuDesembarque()
    {
        if(estaVoando) 
        { 
            estaPousando = true; 
            destino = transform.position; 
            LogDebug("📉 Iniciando sequência de pouso...");
        }
        else if(soldadosEmbarcados.Count > 0) 
        {
            LogDebug("🚪 No chão. Ejetando soldados...");
            EjetarTodos();
        }
        else
        {
            LogDebug("⚠️ Já está no chão e vazio. Nada a fazer.");
        }
    }

    // -----------------------------------------------------------------------
    // EMBARQUE/DESEMBARQUE: Esconde o soldado SEM desativar o GameObject.
    // Isso mantém o NavMeshAgent sempre registrado no NavMesh, eliminando
    // a race-condition que causava o erro "ResetPath on inactive agent".
    // -----------------------------------------------------------------------

    /// <summary>Esconde o soldado dentro do helicóptero sem desativar o GameObject.</summary>
    private void EsconderSoldado(GameObject s)
    {
        if (s == null) return;

        // Para o agente no lugar (não precisa ir a lugar nenhum enquanto dentro do heli)
        NavMeshAgent nav = s.GetComponent<NavMeshAgent>();
        if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh)
        {
            nav.isStopped = true;
            nav.ResetPath();
        }

        // Desliga renderers e colliders para sumir visualmente
        foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var c in s.GetComponentsInChildren<Collider>(true))  c.enabled = false;

        // Desativa scripts de comportamento (IA, controle, etc.) MAS NÃO o NavMeshAgent
        foreach (var mb in s.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null)         continue;
            mb.enabled = false;
        }
    }

    /// <summary>Reposiciona e mostra o soldado usando Warp (método seguro do Unity).</summary>
    private void MostrarSoldado(GameObject s, Vector3 posicao)
    {
        if (s == null) return;

        // Reativa scripts de comportamento
        foreach (var mb in s.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            mb.enabled = true;
        }

        // Religa renderers e colliders
        foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        foreach (var c in s.GetComponentsInChildren<Collider>(true))  c.enabled = true;

        // Usa Warp — método OFICIAL do Unity para teletransportar NavMeshAgent com segurança.
        // Warp encontra o ponto mais próximo no NavMesh automaticamente e nunca lança exceção.
        NavMeshAgent nav = s.GetComponent<NavMeshAgent>();
        if (nav != null && nav.isActiveAndEnabled)
        {
            if (nav.isOnNavMesh)
            {
                nav.Warp(posicao);
                nav.isStopped = false;
            }
            else
            {
                // Agente perdeu o NavMesh de alguma forma: reposiciona via transform e retoma
                s.transform.position = posicao;
                nav.isStopped = false;
            }
        }
        else
        {
            s.transform.position = posicao;
        }

        LogDebug($"[Helicoptero] {s.name} desembarcou em {posicao}.");
    }

    void EjetarTodos()
    {
        int totalSoldados = soldadosEmbarcados.Count;
        for (int i = 0; i < totalSoldados; i++)
        {
            GameObject s = soldadosEmbarcados[i];
            if (s == null) continue;

            float angulo = i * (360f / Mathf.Max(1, totalSoldados));
            Vector3 posDesejada = transform.position + Quaternion.Euler(0, angulo, 0) * (transform.right * 6f);

            // Busca o ponto válido no NavMesh com raio generoso
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(posDesejada, out hit, 20f, NavMesh.AllAreas))
            {
                // Fallback: tenta direto abaixo do helicóptero com raio máximo
                posDesejada.y = Mathf.Max(0f, transform.position.y - alturaPouso);
                NavMesh.SamplePosition(posDesejada, out hit, 50f, NavMesh.AllAreas);
            }

            Vector3 posFinal = hit.position != Vector3.zero ? hit.position : posDesejada;

            MostrarSoldado(s, posFinal);
        }

        soldadosEmbarcados.Clear();
        LogDebug("✅ Todos desembarcados.");
    }




    void DispararFlaresManual()
    {
        if(flares != null && flares.Length > 0)
        {
            timerRecargaFlares = cooldownFlares;
            foreach(var f in flares) if(f) f.Play();
            LogDebug("✨ Flares disparados!");
            Invoke("PararFlares", 4f);
        }
        else
        {
            LogDebug("⚠️ Erro: Nenhum Particle System de Flares atribuído no Inspector!");
        }
    }

    void PararFlares()
    {
        if(flares != null) foreach(var f in flares) if(f) f.Stop();
    }

    // AnimarHelices removido - agora integrado no ControlarMotorEHelices para suavidade

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, raioDoClique);
    }

    public bool EstaDisponivel() { return disponivelParaPatrulha && !estaVoando; }
    public string ObterDescricaoMenu() { return $"{nomeHelicoptero}\nLotação: {soldadosEmbarcados.Count}/{capacidadeMaxima}"; }
    public void MelhorarHelicoptero() { capacidadeMaxima += 4; nomeHelicoptero += "+"; }
    
    public int TemEspaco() { return capacidadeMaxima - soldadosEmbarcados.Count; }
    public bool TemSoldados() { return soldadosEmbarcados.Count > 0; }

    public void ChamarParaHeliporto(Transform t) { Decolar(t.position); }
    public void ChamarParaHeliporto(Heliporto h) { Decolar(h.transform.position); }
    public void ChamarParaHeliporto(GameObject g) { Decolar(g.transform.position); }

    // --- MÉTODOS DE COMPATIBILIDADE DO AEROPORTO ---
    public bool controladoPeloAeroporto = false;
    public bool estacionadoNoAeroporto = false;
    public Transform vagaAeroporto;
    public int missaoAtualAeroporto = 0; // 0 = Nenhuma

    public bool EstaSobControleDoAeroporto() { return controladoPeloAeroporto; }
    public string ObterEstadoOperacionalAeroporto() { if (estacionadoNoAeroporto) return "Estacionado"; return missaoAtualAeroporto != 0 ? "Em Missão" : "Sobrevoando"; }
    public bool EstaEstacionadoNoAeroporto() { return estacionadoNoAeroporto; }
    public Transform ObterVagaAeroporto() { return vagaAeroporto; }
    public void IniciarPatrulhaAeroporto(List<Vector3> wp) { if(wp != null && wp.Count > 0) Decolar(wp[0]); missaoAtualAeroporto = 3; }
    public void CancelarMissaoAeroporto() { missaoAtualAeroporto = 0; }
    public void IniciarReconhecimentoAeroporto(Vector3 wp) { missaoAtualAeroporto = 1; Decolar(wp); }
    public void IniciarAtaqueLocalAeroporto(Vector3 wp) { missaoAtualAeroporto = 2; Decolar(wp); }
    public void VincularAoAeroporto(GerenciadorAeroporto aeroporto, Transform vagaPreferencial) { controladoPeloAeroporto = true; vagaAeroporto = vagaPreferencial; }
    public void PosicionarNaVagaAeroporto(Transform vaga) { estacionadoNoAeroporto = true; vagaAeroporto = vaga; transform.position = vaga.position; estaVoando = false; estaPousando = false; motorLigado = false; }
    public void RetornarParaVagaAeroporto() { if (vagaAeroporto != null) { Decolar(vagaAeroporto.position); estacionadoNoAeroporto = false; missaoAtualAeroporto = 0; } }
}
