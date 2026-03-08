using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems; 
using System.Collections;
using System.Collections.Generic;

// [HELICÓPTERO TÁTICO V19.5 - DEBUG DE INICIALIZAÇÃO]
// - Adicionei um aviso no Start() para garantir que o script está vivo.

public class Helicoptero : MonoBehaviour
{
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

    // OTIMIZAÇÃO DE PERFORMANCE: Cache global dos helicópteros vivos
    private static List<Helicoptero> todosHelicopteros = new List<Helicoptero>();

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
        Debug.Log($"🚁 SISTEMA DO HELICÓPTERO INICIADO NO OBJETO: {name}");
        // -------------------

        identidade = GetComponent<IdentidadeUnidade>();
        if (identidade == null) identidade = GetComponentInParent<IdentidadeUnidade>();

        selecionado = false;
        controleSempreAtivo = false;
        destino = transform.position;
        
        if(!helicePrincipal) helicePrincipal = transform.Find("helice_principal") ?? transform.Find("MainRotor");
        if(!heliceTraseira) heliceTraseira = transform.Find("helice_traseira") ?? transform.Find("TailRotor");

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
                        Debug.Log($"✅ {name} SELECIONADO.");
                    }
                    else
                    {
                        selecionado = false;
                        Debug.Log($"🚫 Ignorado (Muito longe: {distanciaDoCentro:F1}m)");
                    }
                }
                else
                {
                    if(selecionado)
                    {
                        selecionado = false;
                        Debug.Log("🚫 Deselecionado.");
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
                Debug.Log($"🖱️ [CLIQUE DIREITO] Movendo para {h.point}");

                if (modoCombateAtivo)
                {
                    try 
                    {
                        if (h.collider.CompareTag(tagInimigo) || h.collider.name.ToLower().Contains("inimigo"))
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
                Debug.Log("⌨️ [TECLA I] Baixando aeronave para buscar tropas...");
                estaPousando = true; 
                destino = transform.position; 
            }
            else
            {
                Debug.Log("⌨️ [TECLA I] Chamando soldados para embarque...");
                ChamarReforcos();
            }
        }

        // TECLA P (Desembarque / Pousar para Desembarcar)
        if (Input.GetKeyDown(KeyCode.P)) 
        {
            Debug.Log("⌨️ [TECLA P] Ordem de Pouso/Desembarque...");
            OrdemPousoOuDesembarque();
        }

        // TECLA O (Flares)
        if (Input.GetKeyDown(KeyCode.O)) 
        {
            Debug.Log("⌨️ [TECLA O] Tentando disparar Flares...");
            DispararFlaresManual(); 
        }
    }

    // ... (Resto do código igual) ...

    IEnumerator RadarDeAmeacas()
    {
        while (true)
        {
            if (estaVoando && modoCombateAtivo && timerRecargaFlares <= 0)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, raioRadarMissil);
                foreach (var h in hits)
                {
                    if (h.CompareTag(tagMissil) || h.name.ToLower().Contains("missil"))
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
        float alturaAlvo = estaPousando ? alturaPouso : altitudeDeVoo;
        
        Vector3 meta = new Vector3(
            estaPousando ? transform.position.x : destino.x, 
            alturaAlvo, 
            estaPousando ? transform.position.z : destino.z
        );

        float vel = estaPousando ? velocidadePouso : velocidadeNavegacao;
        transform.position = Vector3.MoveTowards(transform.position, meta, vel * Time.deltaTime);

        if (!estaPousando && Vector3.Distance(transform.position, meta) > 2f)
        {
            Vector3 dir = (meta - transform.position).normalized;
            dir.y = 0; 
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);
        }

        if (estaPousando && Mathf.Abs(transform.position.y - alturaPouso) < 0.1f)
        {
            Vector3 pos = transform.position; 
            pos.y = alturaPouso; 
            transform.position = pos;
            estaVoando = false;
            estaPousando = false;
            Debug.Log("🚁 Helicóptero pousou.");
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
        // Limpa soldados chamados que foram destruídos ou já embarcaram ou desistiram
        soldadosChamados.RemoveAll(s => s == null || !s.activeInHierarchy || soldadosEmbarcados.Contains(s));

        int espacoLivre = capacidadeMaxima - (soldadosEmbarcados.Count + soldadosChamados.Count);

        if(espacoLivre <= 0) 
        {
            if(selecionado) Debug.Log("❌ Helicóptero cheio ou já com carga total a caminho!");
            return; // Já está cheio ou com gente suficiente a caminho
        }

        // GPS: Aumentado em 3x o raio de escaneamento para não deixar ninguém para trás na base gigante!
        Collider[] hits = Physics.OverlapSphere(transform.position, distanciaBusca * 3.0f);
        bool encontrouAlguem = false;

        if(selecionado) Debug.Log($"🔍 Procurando soldados em raio expansivo de {distanciaBusca * 3.0f}m...");

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
                    if (selecionado) Debug.Log($"❌ Rejeitado [{s.name}]: RG diz que é do time inimigo ({idSoldado.teamID}). Nosso time é {identidade.teamID}");
                    continue; 
                }
            }
            else if (identidade != null && idSoldado == null)
            {
                if (selecionado) Debug.Log($"⚠️ Atenção [{s.name}]: Não tem IdentidadeUnidade! Aceitando como neutro/nosso.");
            }

            bool tagCorreta = false;
            try { if(s.CompareTag(tagAlvo)) tagCorreta = true; } catch { }
            if(!tagCorreta && (s.name.ToLower().Contains("soldado") || s.name.ToLower().Contains("infant"))) tagCorreta = true;

            if(!tagCorreta)
            {
                if (selecionado && !s.name.ToLower().Contains("tanque") && !s.name.ToLower().Contains("heli") && !s.name.ToLower().Contains("carro")) 
                {
                    Debug.Log($"❌ Rejeitado [{s.name}]: Não tem a Tag '{tagAlvo}' nem nome de soldado.");
                }
                continue; // Pula os tanques
            }

            if(tagCorreta) 
            {
                encontrouAlguem = true;
                soldadosChamados.Add(s);
                Debug.Log($"🪖 [{s.name}] ACEITO! Ordenando correr para o helicóptero!");
                StartCoroutine(RotinaEmbarque(s, nav));

                espacoLivre--;
                if (espacoLivre <= 0) break; // Atingiu o limite de pessoas a chamar
            }
        }

        if(!encontrouAlguem && soldadosChamados.Count == 0 && selecionado) Debug.Log($"❌ Busca concluída: ZERO soldados livres e detectáveis no raio perto ({distanciaBusca}m).");
    }

    IEnumerator RotinaEmbarque(GameObject s, NavMeshAgent nav)
    {
        if(s == null || nav == null) yield break;

        Debug.Log($"[Helicoptero] Iniciando embarque de {s.name}...");
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
                Debug.Log($"[Helicoptero] {name} decolou! Cancelando embarque de {s.name}.");
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

            if (distFinal <= distanciaEmbarque * 3.0f && !estaVoando) // Tolerância final mais generosa
            {
                soldadosEmbarcados.Add(s);
                s.SetActive(false); 
                Debug.Log($"⬇️ {s.name} embarcou com sucesso! (Total: {soldadosEmbarcados.Count})");
            }
            else
            {
                Debug.Log($"❌ {s.name} falhou em embarcar (Longe demais: {distFinal:F1}m).");
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
            Debug.Log("📉 Iniciando sequência de pouso...");
        }
        else if(soldadosEmbarcados.Count > 0) 
        {
            Debug.Log("🚪 No chão. Ejetando soldados...");
            EjetarTodos();
        }
        else
        {
            Debug.Log("⚠️ Já está no chão e vazio. Nada a fazer.");
        }
    }

    void EjetarTodos()
    {
        int i = 0;
        foreach(var s in soldadosEmbarcados)
        {
            if(s)
            {
                s.SetActive(true);
                float angulo = i * (360f / Mathf.Max(1, soldadosEmbarcados.Count));
                
                Vector3 posDesejada = transform.position + Quaternion.Euler(0, angulo, 0) * (transform.right * 6f);
                Vector3 posFinal = posDesejada;
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(posDesejada, out hit, 3.0f, NavMesh.AllAreas)) posFinal = hit.position; 
                else posFinal.y = Mathf.Max(0, transform.position.y - alturaPouso + 0.1f); 

                s.transform.position = posFinal;
                if(s.GetComponent<NavMeshAgent>()) 
                {
                    s.GetComponent<NavMeshAgent>().Warp(posFinal); 
                    s.GetComponent<NavMeshAgent>().ResetPath(); 
                }
            }
            i++;
        }
        soldadosEmbarcados.Clear();
        Debug.Log("✅ Todos desembarcados.");
    }

    void DispararFlaresManual()
    {
        if(flares != null && flares.Length > 0)
        {
            timerRecargaFlares = cooldownFlares;
            foreach(var f in flares) if(f) f.Play();
            Debug.Log("✨ Flares disparados!");
            Invoke("PararFlares", 4f);
        }
        else
        {
            Debug.Log("⚠️ Erro: Nenhum Particle System de Flares atribuído no Inspector!");
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
}