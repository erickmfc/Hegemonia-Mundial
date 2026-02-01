using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// [TECNOLOGIA ANTYGAVITI - MÓDULO DE PROPULSÃO NAVAL V14 - DESEMBARQUE EM LEQUE]
// - Física de Curva Realista
// - Modo Líder (Soldados ocultos)
// - Desembarque Organizado (Sem amontoamento)
public class HovercraftTransporte : MonoBehaviour
{
    [Header("🔧 Movimentação")]
    public float velocidade = 1500f; 
    public float velocidadeRotacao = 1.5f; 
    public float alturaDoChao = 1.5f; 
    public LayerMask camadasChao; 
    
    [Header("🎮 Seleção")]
    public bool isSelecionado = false;

    [Header("🚪 Rampa")]
    public Transform portaRampa;
    public float anguloAberto = 110f;
    private bool rampaAberta = false;
    public float delayEntreEmbarques = 1.2f; 

    [Header("💨 Hélices")]
    public Transform[] helices; 

    [Header("📦 SLOTS DE CARGA")]
    public Transform[] slotsDeCarga; 

    [Header("⚙️ Configurações de Carga")]
    public float distanciaParaEmbarque = 40f; 
    public float distanciaDescarga = 15.0f; // Base inicial
    public int capacidadeSoldadosPorSlot = 12; 

    // ESTADO
    private Rigidbody rb;
    private Vector3 destinoAtual;
    private bool temDestino = false;
    private bool processoEmbarqueAtivo = false;

    [System.Serializable]
    public class SlotInfo 
    {
        public Transform pontoAncora;
        public GameObject veiculoOcupante; 
        public List<GameObject> soldadosOcupantes = new List<GameObject>(); 
        public bool EstaVazio => veiculoOcupante == null && soldadosOcupantes.Count == 0;
        public bool TemEspacoSoldado(int max) => veiculoOcupante == null && soldadosOcupantes.Count < max;
    }

    private List<SlotInfo> slotsLogicos = new List<SlotInfo>();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; rb.isKinematic = false; 
        rb.linearDamping = 1f; 
        rb.angularDamping = 2f; 
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (helices == null || helices.Length == 0)
        {
            var l = new List<Transform>();
            foreach(Transform t in GetComponentsInChildren<Transform>()) 
                if(t.name.ToLower().Contains("helice")) l.Add(t);
            helices = l.ToArray();
        }

        AtualizarSlots();
    }

    void AtualizarSlots()
    {
        slotsLogicos.Clear();
        if (slotsDeCarga != null)
        {
            foreach(var t in slotsDeCarga)
            {
                if (t != null) slotsLogicos.Add(new SlotInfo { pontoAncora = t });
            }
        }
    }

    void Update()
    {
        VerificarSelecao();

        if (isSelecionado)
        {
            if (Input.GetKeyDown(KeyCode.U)) 
            { 
                rampaAberta = true; 
                if (!processoEmbarqueAtivo) StartCoroutine(RotinaEmbarqueSequencial()); 
            }
            
            if (Input.GetKeyDown(KeyCode.P)) 
            { 
                rampaAberta = !rampaAberta; 
                if (rampaAberta) StartCoroutine(RotinaDesembarqueSequencial()); 
            }
            
            if (Input.GetMouseButtonDown(1))
            {
                Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(r, out RaycastHit h, 1000f, camadasChao)) 
                { 
                    destinoAtual = h.point; 
                    temDestino = true; 
                }
            }
        }

        AnimarRampa();
        AnimarHelices();
        FixarUnidadesEmbarcadas(); 
    }

    void FixedUpdate() 
    { 
        ManterFlutuacao(); 
        MoverParaDestino(); 
    }

    void MoverParaDestino() 
    { 
        if(!temDestino) return;

        Vector3 dir = destinoAtual - transform.position; dir.y=0;
        if(dir.magnitude < 8f) { temDestino=false; return; }

        Quaternion rotAlvo = Quaternion.LookRotation(dir);
        rb.MoveRotation(Quaternion.Slerp(transform.rotation, rotAlvo, velocidadeRotacao * Time.fixedDeltaTime));
        rb.AddForce(transform.forward * velocidade * Time.fixedDeltaTime, ForceMode.Acceleration);

        Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 forcaContraDrift = transform.right * -velLocal.x * 800f * Time.fixedDeltaTime;
        rb.AddForce(forcaContraDrift, ForceMode.Acceleration);
    }

    void ManterFlutuacao()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit h, alturaDoChao + 10f, camadasChao))
        {
            float f = (alturaDoChao - h.distance) * 40f; 
            if(f>0) rb.AddForce(Vector3.up * f, ForceMode.VelocityChange);
            else rb.AddForce(Vector3.down * 5f, ForceMode.Acceleration);
        }
    }
    
    // ===================================
    // LÓGICA DE CARGA E FIXAÇÃO
    // ===================================

    void FixarUnidadesEmbarcadas()
    {
        foreach(var slot in slotsLogicos)
        {
            if (slot.pontoAncora == null) continue;

            if (slot.veiculoOcupante != null)
                ManterUnidadeColada(slot.veiculoOcupante, slot.pontoAncora.position, slot.pontoAncora.rotation);

            for(int i = 0; i < slot.soldadosOcupantes.Count; i++)
            {
                var s = slot.soldadosOcupantes[i];
                if(s == null) continue;

                if (i == 0) // LÍDER VISÍVEL
                {
                    if(!s.activeSelf) s.SetActive(true);
                    Quaternion rotLider = slot.pontoAncora.rotation * Quaternion.Euler(0, 180, 0);
                    ManterUnidadeColada(s, slot.pontoAncora.position, rotLider);
                }
                else // PELOTÃO OCULTO
                {
                    if(s.activeSelf) s.SetActive(false);
                    s.transform.position = slot.pontoAncora.position;
                }
            }
        }
    }

    void ManterUnidadeColada(GameObject u, Vector3 posMundo, Quaternion rotMundo)
    {
        var rbU = u.GetComponent<Rigidbody>();
        if(rbU && !rbU.isKinematic) rbU.isKinematic = true;

        if (Vector3.Distance(u.transform.position, posMundo) > 4.0f)
            u.transform.position = posMundo;
        else
            u.transform.position = Vector3.Lerp(u.transform.position, posMundo, Time.deltaTime * 20f);
            
        u.transform.rotation = Quaternion.Lerp(u.transform.rotation, rotMundo, Time.deltaTime * 15f);
    }

    // ===================================
    // ROTINA DE EMBARQUE
    // ===================================

    IEnumerator RotinaEmbarqueSequencial()
    {
        processoEmbarqueAtivo = true;
        if (slotsLogicos.Count == 0 || slotsLogicos[0].pontoAncora == null) CriarSlotsPadrao();
        
        Debug.Log($"⏳ Iniciando embarque...");
        List<GameObject> fila = new List<GameObject>();
        Collider[] hits = Physics.OverlapSphere(transform.position, distanciaParaEmbarque);

        foreach (var hit in hits)
        {
            GameObject u = ResolverUnidade(hit.gameObject);
            if (u != null && EhViavelParaEmbarque(u) && !fila.Contains(u)) fila.Add(u);
        }

        foreach (GameObject u in fila)
        {
            if (u == null || !u.activeInHierarchy) continue;
            if (TentarAlocar(u))
            {
                DesativarLogicaUnidade(u); 
                yield return new WaitForSeconds(delayEntreEmbarques);
            }
        }

        processoEmbarqueAtivo = false;
        Debug.Log("✅ Embarque finalizado.");
    }

    bool TentarAlocar(GameObject u)
    {
        bool soldado = EhSoldado(u);
        foreach(var slot in slotsLogicos)
        {
            if (soldado)
            {
                if (slot.veiculoOcupante == null && slot.soldadosOcupantes.Count > 0 && slot.soldadosOcupantes.Count < capacidadeSoldadosPorSlot)
                {
                    slot.soldadosOcupantes.Add(u);
                    return true;
                }
            }
            if (slot.EstaVazio)
            {
                if (soldado) slot.soldadosOcupantes.Add(u);
                else slot.veiculoOcupante = u;
                return true;
            }
        }
        return false;
    }

    void DesativarLogicaUnidade(GameObject u)
    {
        var nav = u.GetComponent<NavMeshAgent>();
        if(nav) { nav.velocity = Vector3.zero; nav.isStopped = true; nav.enabled = false; }
        
        var rbU = u.GetComponent<Rigidbody>();
        if(rbU) { rbU.isKinematic = true; rbU.detectCollisions = false; }

        foreach(var c in u.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach(var s in u.GetComponents<MonoBehaviour>()) 
        {
            if(s.GetType().Name.Contains("Controle") || s.GetType().Name.Contains("IA_")) s.enabled = false;
        }
    }

    // ===================================
    // ROTINA DE DESEMBARQUE (COM FREIO E FORMAÇÃO)
    // ===================================

    IEnumerator RotinaDesembarqueSequencial()
    {
        // 1. FREIA
        temDestino = false; 
        Debug.Log("🛑 Freando...");
        while (rb.linearVelocity.magnitude > 0.5f)
        {
            rb.linearDamping = 5.0f; 
            yield return null;
        }
        rb.linearVelocity = Vector3.zero; 
        rb.linearDamping = 1.0f; 

        // 2. PREPARA LISTA
        List<GameObject> saindo = new List<GameObject>();
        foreach(var slot in slotsLogicos) 
        {
            if(slot.veiculoOcupante) saindo.Add(slot.veiculoOcupante);
            saindo.AddRange(slot.soldadosOcupantes);
            
            slot.veiculoOcupante = null;
            slot.soldadosOcupantes.Clear();
        }

        // 3. EJETA EM FORMAÇÃO
        int indiceFormacao = 0;
        foreach(var u in saindo)
        {
            if(u)
            {
                Ejetar(u, indiceFormacao);
                indiceFormacao++;
                yield return new WaitForSeconds(0.6f); // Tempo para o anterior andar um pouco
            }
        }
        
        yield return new WaitForSeconds(3f);
        rampaAberta = false;
    }

    void Ejetar(GameObject u, int idx)
    {
        u.SetActive(true); 
        u.transform.SetParent(null);
        
        // CÁLCULO DE FORMAÇÃO EM LEQUE/V
        // Base: 25m à frente
        Vector3 pontoBase = transform.position + transform.forward * (distanciaDescarga + 10f);

        // Alterna lados: 0 (centro), 1 (dir), 2 (esq), 3 (dir), 4 (esq)...
        // Aumenta o espaçamento lateral a cada par (8m, 16m, 24m...)
        float lateralMultiplier = Mathf.CeilToInt((idx + 1) / 2.0f); 
        float lado = (idx % 2 == 0) ? 1f : -1f;
        if(idx == 0) lado = 0; // Primeiro no centro

        Vector3 offsetLateral = transform.right * (lado * lateralMultiplier * 8.0f);
        Vector3 offsetProfundidade = transform.forward * (indiceAleatorio(idx) * 2f); // Pequena variação frente/tras

        Vector3 destinoFinal = pontoBase + offsetLateral + offsetProfundidade;

        // Ajusta altura com Navmesh
        NavMeshHit hit;
        if(NavMesh.SamplePosition(destinoFinal, out hit, 15f, NavMesh.AllAreas))
            destinoFinal = hit.position;
        else
            destinoFinal.y = transform.position.y; // Chão plano se falhar

        u.transform.position = destinoFinal;
        u.transform.rotation = transform.rotation;
        
        // REATIVA
        foreach(var c in u.GetComponentsInChildren<Collider>()) c.enabled = true;
        var nav = u.GetComponent<NavMeshAgent>();
        if(nav) { nav.enabled = true; nav.Warp(destinoFinal); }
        var rbU = u.GetComponent<Rigidbody>();
        if(rbU) { rbU.isKinematic = false; rbU.detectCollisions = true; }
        
        foreach(var s in u.GetComponents<MonoBehaviour>()) 
        {
            string n = s.GetType().Name;
            if(n.Contains("Controle") || n.Contains("IA_")) s.enabled = true;
        }
    }
    
    float indiceAleatorio(int i) { return (i % 3) - 1.0f; } // -1, 0, 1

    // ===================================
    // HELPER
    // ===================================
    
    GameObject ResolverUnidade(GameObject hit)
    {
        var ctrl = hit.GetComponentInParent<ControleUnidade>();
        if(ctrl) return ctrl.gameObject;
        if(hit.transform.root) return hit.transform.root.gameObject;
        return hit;
    }

    bool EhViavelParaEmbarque(GameObject u)
    {
        if (u == gameObject) return false;
        if (JaEmbarcado(u)) return false;
        if (u.name.ToLower().Contains("uss")) return false;
        if (u.GetComponent<HovercraftTransporte>()) return false;
        
        var id = u.GetComponent<IdentidadeUnidade>();
        var meuId = GetComponent<IdentidadeUnidade>();
        if (id && meuId && id.teamID != meuId.teamID) return false;

        return EhSoldado(u) || EhVeiculo(u);
    }

    bool JaEmbarcado(GameObject u)
    {
        foreach(var s in slotsLogicos)
            if (s.veiculoOcupante == u || s.soldadosOcupantes.Contains(u)) return true;
        return false;
    }

    bool EhSoldado(GameObject obj)
    {
        var sd = obj.GetComponent<SistemaDeDanos>();
        string n = obj.name.ToLower();
        return (sd && sd.unidadeBiologica) || n.Contains("soldado") || n.Contains("sniper") || n.Contains("caoc") || n.Contains("infant");
    }

    bool EhVeiculo(GameObject obj)
    {
        var nav = obj.GetComponent<NavMeshAgent>();
        string n = obj.name.ToLower();
        return (nav != null) || n.Contains("tank") || n.Contains("truck") || n.Contains("caminhao") || n.Contains("jeep") || n.Contains("vehicle");
    }

    void AnimarRampa() 
    {
        if(portaRampa) portaRampa.localRotation = Quaternion.Slerp(portaRampa.localRotation, Quaternion.Euler(rampaAberta?anguloAberto:0,0,0), Time.deltaTime*2);
    }
    void AnimarHelices()
    {
        if(helices==null) return;
        float spe = 200 + rb.linearVelocity.magnitude * 50;
        foreach(var h in helices) if(h) h.Rotate(Vector3.forward, spe*Time.deltaTime);
    }
    void VerificarSelecao()
    {
        if(Input.GetMouseButtonDown(0))
        {
            Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
            if(Physics.Raycast(r, out RaycastHit h, 1000f)) isSelecionado = (h.transform==transform || h.transform.IsChildOf(transform));
        }
    }
    void OnDrawGizmos()
    {
        if(slotsDeCarga!=null) {
            Gizmos.color = Color.cyan;
            foreach(var t in slotsDeCarga) if(t) Gizmos.DrawWireCube(t.position, new Vector3(2.5f, 0.1f, 4f));
        }
    }
    void CriarSlotsPadrao()
    {
        var lista = new List<Transform>();
        GameObject container = new GameObject("Slots_AutoGerados");
        container.transform.SetParent(transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        for (int i = 0; i < 6; i++) {
            GameObject slot = new GameObject($"Slot_Auto_{i}");
            slot.transform.SetParent(container.transform);
            float x = (i % 2 == 0) ? -2.5f : 2.5f; 
            float z = 3f - (i / 2) * 5.5f; 
            float y = 1.2f; 
            slot.transform.localPosition = new Vector3(x, y, z);
            slot.transform.localRotation = Quaternion.identity;
            lista.Add(slot.transform);
        }
        slotsDeCarga = lista.ToArray();
        AtualizarSlots();
    }
}
