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

    // ESTADOS INTERNOS
    private Vector3 destino;
    private bool estaVoando = false;
    private bool estaPousando = false;
    private bool motorLigado = false;
    private float timerInatividade = 0f;
    private float timerRecargaFlares = 0f;

    // COMPATIBILIDADE
    [HideInInspector] public string nomeHelicoptero = "Falcão Negro"; 
    [HideInInspector] public int custoUpgrade = 800;  
    private bool disponivelParaPatrulha = true; 

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

        selecionado = false;
        controleSempreAtivo = false;
        destino = transform.position;
        
        if(!helicePrincipal) helicePrincipal = transform.Find("helice_principal") ?? transform.Find("MainRotor");
        if(!heliceTraseira) heliceTraseira = transform.Find("helice_traseira") ?? transform.Find("TailRotor");

        StartCoroutine(RadarDeAmeacas());
    }

    void Update()
    {
        if (timerRecargaFlares > 0) timerRecargaFlares -= Time.deltaTime;

        GestaoDeInput(); 
        
        if (estaVoando) ProcessarMovimento();
        if (motorLigado) AnimarHelices();
        VerificarInatividade();
    }

    void GestaoDeInput()
    {
        // 1. CLIQUE ESQUERDO (Seleção)
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
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

        // TECLA K
        if (Input.GetKeyDown(KeyCode.K))
        {
            modoCombateAtivo = !modoCombateAtivo;
            Debug.Log($"⌨️ [TECLA K] Modo Combate: {(modoCombateAtivo ? "ATIVO" : "PASSIVO")}");
        }

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

        // TECLA O
        if (Input.GetKeyDown(KeyCode.O)) 
        {
            Debug.Log("⌨️ [TECLA O] Tentando disparar Flares...");
            DispararFlaresManual(); 
        }

        // TECLA U
        if (Input.GetKeyDown(KeyCode.U)) 
        {
            Debug.Log("⌨️ [TECLA U] Chamando reforços...");
            ChamarReforcos();
        }

        // TECLA P
        if (Input.GetKeyDown(KeyCode.P)) 
        {
            Debug.Log("⌨️ [TECLA P] Ordem de Pouso/Desembarque...");
            OrdemPousoOuDesembarque();
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

    void ChamarReforcos()
    {
        if(soldadosEmbarcados.Count >= capacidadeMaxima) 
        {
            Debug.Log("⚠️ Helicóptero Cheio! (Capacidade atingida)");
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, distanciaBusca);
        bool encontrouAlguem = false;

        foreach(var h in hits)
        {
            GameObject s = h.transform.root.gameObject;
            if(s == gameObject || soldadosEmbarcados.Contains(s)) continue;

            bool tagCorreta = false;
            try { if(s.CompareTag(tagAlvo)) tagCorreta = true; } catch { }
            if(!tagCorreta && (s.name.ToLower().Contains("soldado") || s.name.ToLower().Contains("infant"))) tagCorreta = true;

            var nav = s.GetComponent<NavMeshAgent>();
            if(tagCorreta && nav) 
            {
                encontrouAlguem = true;
                Debug.Log($"🪖 Soldado encontrado: {s.name}. Ordenando embarque!");
                StartCoroutine(RotinaEmbarque(s, nav));
            }
        }

        if(!encontrouAlguem) Debug.Log("❌ Nenhum soldado (Tag 'Soldado' ou NavMesh) encontrado por perto (50m).");
    }

    IEnumerator RotinaEmbarque(GameObject s, NavMeshAgent nav)
    {
        nav.SetDestination(transform.position); 
        nav.isStopped = false; 
        Vector3 ultimaPosHeli = transform.position;
        
        while(s && s.activeInHierarchy)
        {
            float distHorizontal = Vector2.Distance(new Vector2(s.transform.position.x, s.transform.position.z), new Vector2(transform.position.x, transform.position.z));
            if(distHorizontal <= distanciaEmbarque) break; 

            if(nav && nav.isActiveAndEnabled) 
            {
                if(Vector3.Distance(transform.position, ultimaPosHeli) > 1.0f)
                {
                    nav.SetDestination(transform.position);
                    ultimaPosHeli = transform.position;
                }
            }
            yield return new WaitForSeconds(0.2f);
        }

        if(s && soldadosEmbarcados.Count < capacidadeMaxima)
        {
            soldadosEmbarcados.Add(s);
            s.SetActive(false); 
            Debug.Log($"⬇️ {s.name} embarcou com sucesso!");
        }
    }

    void OrdemPousoOuDesembarque()
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

    void AnimarHelices()
    {
        if(helicePrincipal) helicePrincipal.Rotate(0, velocidadeHelice * Time.deltaTime, 0);
        if(heliceTraseira) heliceTraseira.Rotate(Vector3.right * velocidadeHelice * Time.deltaTime, Space.Self);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, raioDoClique);
    }

    public bool EstaDisponivel() { return disponivelParaPatrulha && !estaVoando; }
    public string ObterDescricaoMenu() { return $"{nomeHelicoptero}\nLotação: {soldadosEmbarcados.Count}/{capacidadeMaxima}"; }
    public void MelhorarHelicoptero() { capacidadeMaxima += 4; nomeHelicoptero += "+"; }
    
    public void ChamarParaHeliporto(Transform t) { Decolar(t.position); }
    public void ChamarParaHeliporto(Heliporto h) { Decolar(h.transform.position); }
    public void ChamarParaHeliporto(GameObject g) { Decolar(g.transform.position); }
}