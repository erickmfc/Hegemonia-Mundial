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

    [Header("🏖️ Costa / Anti-Encalhe")]
    public float distanciaSondaFrontal = 7f;
    public float larguraSonda = 2.5f;
    public float alturaOrigemSonda = 6f;
    public float impulsoSubidaPraia = 10f;
    public float velocidadeMinimaNaCosta = 9f;
    public float tempoParaDesatolar = 0.75f;
    
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
    private bool processoDesembarqueAtivo = false;
    private Vector3 ultimaPosicaoProgresso;
    private float tempoSemProgresso = 0f;
    private bool monitorDeProgressoInicializado = false;

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
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        ConfigurarCamadasAnfibias();

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
                IniciarEmbarque();
            }
            
            if (Input.GetKeyDown(KeyCode.P)) 
            { 
                IniciarDesembarque();
            }
            
            if (Input.GetMouseButtonDown(1))
            {
                Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(r, out RaycastHit h, 1000f, camadasChao)) 
                { 
                    DefinirDestino(h.point);
                }
                else if (Physics.Raycast(r, out RaycastHit hitLivre, 1000f))
                {
                    DefinirDestino(hitLivre.point);
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
        AtualizarAntiEncalhe();
    }

    public void DefinirDestino(Vector3 destino)
    {
        destinoAtual = destino;
        temDestino = true;
        ultimaPosicaoProgresso = PlanoXZ(transform.position);
        tempoSemProgresso = 0f;
        monitorDeProgressoInicializado = true;
    }

    void MoverParaDestino() 
    { 
        if(!temDestino) return;

        Vector3 dir = destinoAtual - transform.position;
        dir.y = 0f;

        float distancia = dir.magnitude;
        if (distancia < 8f)
        {
            temDestino = false;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 0.35f);
            return;
        }

        Vector3 direcaoDesejada = dir.normalized;
        float anguloErro = Vector3.SignedAngle(transform.forward, direcaoDesejada, Vector3.up);
        bool subindoPraia = DetectarSubidaPraia(out float subidaPraia);

        float fatorRotacao = Mathf.Clamp01(Mathf.Abs(anguloErro) / 90f);
        float velocidadeRotacaoReal = Mathf.Max(velocidadeRotacao, 2f) * Mathf.Lerp(0.75f, 2.2f, fatorRotacao);
        Quaternion rotAlvo = Quaternion.LookRotation(direcaoDesejada, Vector3.up);
        Quaternion novaRotacao = Quaternion.RotateTowards(transform.rotation, rotAlvo, velocidadeRotacaoReal * Time.fixedDeltaTime);
        rb.MoveRotation(novaRotacao);

        Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
        velLocal.x = Mathf.Lerp(velLocal.x, 0f, 0.22f);

        float alinhamento = Mathf.Clamp01(Vector3.Dot(transform.forward, direcaoDesejada));
        float freioPorCurva = Mathf.Clamp01(1f - (Mathf.Abs(anguloErro) / 135f));
        float freioPorDistancia = Mathf.Clamp01(distancia / 25f);
        float velocidadeBase = Mathf.Clamp(velocidade * 0.018f, 11f, 30f);
        float velocidadeAlvo = velocidadeBase
            * Mathf.Lerp(0.28f, 1f, alinhamento)
            * Mathf.Lerp(0.30f, 1f, freioPorCurva)
            * Mathf.Lerp(0.35f, 1f, freioPorDistancia);

        if (subindoPraia && distancia > 14f)
        {
            float bonusCosta = Mathf.Clamp(subidaPraia * 3.5f, 0f, velocidadeMinimaNaCosta);
            velocidadeAlvo = Mathf.Max(velocidadeAlvo, velocidadeMinimaNaCosta + bonusCosta);
        }

        float taxaAceleracao = Mathf.Clamp(velocidade * 0.08f, 18f, 85f);
        if (subindoPraia)
        {
            taxaAceleracao *= 1.35f;
        }

        velLocal.z = Mathf.MoveTowards(velLocal.z, velocidadeAlvo, taxaAceleracao * Time.fixedDeltaTime);
        if (alinhamento < 0.2f)
        {
            velLocal.z = Mathf.MoveTowards(velLocal.z, 0f, taxaAceleracao * 1.6f * Time.fixedDeltaTime);
        }

        Vector3 velocidadeMundo = transform.TransformDirection(velLocal);
        velocidadeMundo.y = rb.linearVelocity.y;
        rb.linearVelocity = velocidadeMundo;
    }

    void ManterFlutuacao()
    {
        if (TryGetHoverPlaneHeight(out float alturaBase, out bool subindoPraia, out float subidaPraia))
        {
            float alturaDesejada = alturaBase + alturaDoChao;
            float erro = alturaDesejada - transform.position.y;
            float amortecimentoVertical = rb.linearVelocity.y * 0.65f;
            float forcaVertical = (erro * 28f) - (amortecimentoVertical * 5.5f);

            if (subindoPraia)
            {
                forcaVertical += impulsoSubidaPraia + Mathf.Clamp(subidaPraia * 8f, 0f, impulsoSubidaPraia);
            }

            rb.AddForce(Vector3.up * forcaVertical, ForceMode.Acceleration);
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
    
    public void IniciarEmbarque()
    {
        rampaAberta = true; 
        if (!processoEmbarqueAtivo) StartCoroutine(RotinaEmbarqueSequencial());
    }

    public void IniciarDesembarque()
    {
        if (processoDesembarqueAtivo) return;

        if (!TemCarga())
        {
            rampaAberta = !rampaAberta;
            return;
        }

        rampaAberta = true; 
        StartCoroutine(RotinaDesembarqueSequencial());
    }

    public void DesembarcarTudo()
    {
        IniciarDesembarque();
    }

    public bool TemEspacoLivre()
    {
        foreach(var slot in slotsLogicos) if (slot.EstaVazio || (slot.veiculoOcupante == null && slot.soldadosOcupantes.Count < capacidadeSoldadosPorSlot)) return true;
        return false;
    }

    public bool TemCarga()
    {
        foreach(var slot in slotsLogicos) if (!slot.EstaVazio) return true;
        return false;
    }

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
        processoDesembarqueAtivo = true;

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
        processoDesembarqueAtivo = false;
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

    void ConfigurarCamadasAnfibias()
    {
        camadasChao = AdicionarLayerSeExistir(camadasChao, "Chao");
        camadasChao = AdicionarLayerSeExistir(camadasChao, "Terrain");
        camadasChao = AdicionarLayerSeExistir(camadasChao, "Terra");
        camadasChao = AdicionarLayerSeExistir(camadasChao, "Water");
        camadasChao = AdicionarLayerSeExistir(camadasChao, "Agua");
        camadasChao = AdicionarLayerSeExistir(camadasChao, "Mar");
        camadasChao = AdicionarLayerSeExistir(camadasChao, "Sea");

        if (camadasChao.value == 0)
        {
            camadasChao = Physics.DefaultRaycastLayers;
        }
    }

    LayerMask AdicionarLayerSeExistir(LayerMask mascara, string nomeLayer)
    {
        int indice = LayerMask.NameToLayer(nomeLayer);
        if (indice < 0) return mascara;

        return mascara | (1 << indice);
    }

    bool TryGetHoverPlaneHeight(out float alturaBase, out bool subindoPraia, out float subidaPraia)
    {
        alturaBase = transform.position.y - alturaDoChao;
        subindoPraia = false;
        subidaPraia = 0f;

        Vector3 frente = transform.forward * distanciaSondaFrontal;
        Vector3 direita = transform.right * larguraSonda;
        Vector3[] offsets =
        {
            Vector3.zero,
            frente,
            frente + direita,
            frente - direita,
            -frente * 0.45f
        };

        float maiorAltura = float.MinValue;
        float alturaCentro = float.MinValue;
        float alturaFrontal = float.MinValue;
        bool achouAlgo = false;

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 origem = transform.position + offsets[i] + (Vector3.up * alturaOrigemSonda);
            if (!Physics.Raycast(origem, Vector3.down, out RaycastHit hit, alturaOrigemSonda + alturaDoChao + 25f, camadasChao, QueryTriggerInteraction.Ignore))
                continue;

            achouAlgo = true;
            maiorAltura = Mathf.Max(maiorAltura, hit.point.y);

            if (i == 0) alturaCentro = hit.point.y;
            if (i >= 1 && i <= 3) alturaFrontal = Mathf.Max(alturaFrontal, hit.point.y);
        }

        if (!achouAlgo)
            return false;

        alturaBase = maiorAltura;

        if (alturaCentro > float.MinValue && alturaFrontal > float.MinValue)
        {
            subidaPraia = Mathf.Max(0f, alturaFrontal - alturaCentro);
            subindoPraia = subidaPraia > 0.25f;
        }

        return true;
    }

    bool DetectarSubidaPraia(out float subidaPraia)
    {
        if (TryGetHoverPlaneHeight(out _, out bool subindoPraia, out subidaPraia))
            return subindoPraia;

        subidaPraia = 0f;
        return false;
    }

    void AtualizarAntiEncalhe()
    {
        if (!temDestino)
        {
            tempoSemProgresso = 0f;
            monitorDeProgressoInicializado = false;
            return;
        }

        if (Vector3.Distance(PlanoXZ(transform.position), PlanoXZ(destinoAtual)) < 12f)
        {
            tempoSemProgresso = 0f;
            ultimaPosicaoProgresso = PlanoXZ(transform.position);
            return;
        }

        Vector3 posicaoAtual = PlanoXZ(transform.position);
        if (!monitorDeProgressoInicializado)
        {
            ultimaPosicaoProgresso = posicaoAtual;
            monitorDeProgressoInicializado = true;
            return;
        }

        float deslocamento = Vector3.Distance(posicaoAtual, ultimaPosicaoProgresso);
        Vector3 velocidadeHorizontal = PlanoXZ(rb.linearVelocity);
        if (deslocamento > 0.4f || velocidadeHorizontal.magnitude > 2.5f)
        {
            ultimaPosicaoProgresso = posicaoAtual;
            tempoSemProgresso = 0f;
            return;
        }

        tempoSemProgresso += Time.fixedDeltaTime;
        if (tempoSemProgresso < tempoParaDesatolar)
            return;

        Vector3 direcao = destinoAtual - transform.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.1f)
        {
            tempoSemProgresso = 0f;
            return;
        }

        Vector3 impulso = (direcao.normalized * 4f) + (Vector3.up * 1.8f);
        rb.AddForce(impulso, ForceMode.VelocityChange);
        tempoSemProgresso = tempoParaDesatolar * 0.35f;
    }

    Vector3 PlanoXZ(Vector3 valor)
    {
        valor.y = 0f;
        return valor;
    }
}
