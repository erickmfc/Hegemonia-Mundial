using UnityEngine;
using System.Collections;

/// <summary>
/// Controle do Submarino USS Leviathan com sistema de mísseis selecionável.
/// </summary>
public class ControleSubmarino : MonoBehaviour
{
    [Header("Configuração de Profundidade")]
    [Tooltip("Profundidade quando submerso (valor negativo em Y)")]
    public float profundidadeSubmersao = -15f;
    
    [Tooltip("Altura quando na superfície (valor em Y)")]
    public float alturaSuperificie = 0f;
    
    [Tooltip("Velocidade de subida/descida")]
    public float velocidadeMovimento = 2f;
    
    [Header("Sistema de Mísseis")]
    [Tooltip("Locais de disparo de mísseis (preencha todos no Editor — nulos são ignorados automaticamente)")]
    public Transform[] locaisDisparo = new Transform[22];
    
    [Tooltip("Prefab do míssil submarino")]
    public GameObject prefabMisselSubmarino;
    
    [Header("Alcance de Ataque")]
    [Tooltip("Alcance máximo dos mísseis em unidades")]
    public float alcanceMisseis = 500f;

    [Header("Status")]
    public bool estaSubmerso = true;
    public int misseisDisponiveis = 22;
    
    // Estado interno
    private bool emMovimento = false;
    private float ultimoMovimento = -4f;
    private bool[] misseisUsados;          // Tamanho dinâmico — inicializado no Start()
    private int totalLocaisValidos = 0;    // Quantidade de slots não-nulos descoberta no Start()
    private bool modoMira = false;
    private Vector3 pontoAlvoAtual;

    // Cache do ControleUnidade — evita GetComponent todo frame (era o bug #1)
    private ControleUnidade meuControle;
    
    // --- VARIÁVEIS DE NAVEGAÇÃO REALISTA ---
    [Header("Física de Navegação")]
    [Tooltip("Velocidade máxima de rotação do leme (graus por segundo).")]
    public float velocidadeGiroMax = 15f; 
    [Tooltip("Quanto tempo o submarino demora para acelerar totalmente (inércia).")]
    public float aceleracao = 1.5f;
    [Tooltip("Inclinação visual nas curvas")]
    public float forcaInclinacao = 3.0f;
    public Transform modelo3D;
    public TrailRenderer rastroAgua;

    private UnityEngine.AI.NavMeshAgent agente;
    private float velocidadeOriginal;
    private float velocidadeAtualSimulada = 0f;
    private float lemeAtual = 0f;
    private Camera cameraPrincipal;
    
    void Start()
    {
        cameraPrincipal = Camera.main;
        // FIX #1 — Cache do ControleUnidade, nunca mais GetComponent no Update
        meuControle = GetComponent<ControleUnidade>();

        agente = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agente != null)
        {
            velocidadeOriginal = agente.speed;
            agente.updateRotation = false; 
            agente.acceleration = 9999;
        }
        
        if (rastroAgua == null) rastroAgua = GetComponentInChildren<TrailRenderer>();

        // FIX #3 & #4 — Conta slots válidos e cria array de status com tamanho correto
        totalLocaisValidos = 0;
        for (int i = 0; i < locaisDisparo.Length; i++)
            if (locaisDisparo[i] != null) totalLocaisValidos++;

        misseisUsados = new bool[locaisDisparo.Length]; // Tamanho real do array, sem IndexOutOfRange
        misseisDisponiveis = totalLocaisValidos;        // Inicia com a contagem real de slots
        
        Debug.Log($"[USS Leviathan] {totalLocaisValidos} locais de lançamento detectados.", this);

        // Lógica de Inicialização Inteligente (Compatível com Estaleiro)
        float navMeshY = transform.position.y - (agente != null ? agente.baseOffset : 0f);

        if (transform.position.y > -5f)
        {
            estaSubmerso = false;
            if (agente != null) agente.baseOffset = alturaSuperificie - navMeshY;
        }
        else
        {
            estaSubmerso = true;
            if (agente != null) agente.baseOffset = profundidadeSubmersao - navMeshY;
        }
    }
    
    void Update()
    {
        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
        // FIX #1 — Usa o cache; nunca busca no mid-frame
        if (meuControle == null || !meuControle.selecionado)
        {
            // Se perdeu seleção durante o modo mira, cancela
            if (modoMira)
            {
                modoMira = false;
                DesativarCursorMira();
            }
            return; // FIX #5 — teclas NÃO processadas se não selecionado
        }
        
        float tempoDesdeUltimoMovimento = Time.time - ultimoMovimento;

        // --- MOVIMENTO REALISTA ---
        if (agente != null && agente.enabled)
        {
            if (modoMira)
            {
                velocidadeAtualSimulada = 0f;
                agente.velocity = Vector3.zero;
                agente.ResetPath();
            }
            else if (agente.hasPath && agente.remainingDistance > agente.stoppingDistance)
            {
                ExecutarMarchaFrenteRealista();
            }
            else
            {
                velocidadeAtualSimulada = Mathf.Lerp(velocidadeAtualSimulada, 0f, Time.deltaTime * 0.5f);
                agente.velocity = transform.forward * velocidadeAtualSimulada;
            }
            
            AtualizarInclinacaoNavio();
            AtualizarRastroAgua();
        }
        
        // FIX #5 — Teclas de controle SÓ respondem para o submarino SELECIONADO
        // (o early-return acima já garante isso, mas fica explícito aqui)

        // U - Subir
        if (Input.GetKeyDown(KeyCode.U))
        {
            if (tempoDesdeUltimoMovimento >= 4f && !emMovimento)
            {
                if (estaSubmerso)
                    StartCoroutine(Subir());
                else
                    Debug.Log("[USS Leviathan] Já está na superfície!");
            }
            else
            {
                Debug.Log($"[USS Leviathan] Aguarde {(4f - tempoDesdeUltimoMovimento):F1}s antes de mover novamente.");
            }
        }
        
        // P - Descer
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (tempoDesdeUltimoMovimento >= 4f && !emMovimento)
            {
                if (!estaSubmerso)
                    StartCoroutine(Descer());
                else
                    Debug.Log("[USS Leviathan] Já está submerso!");
            }
            else
            {
                Debug.Log($"[USS Leviathan] Aguarde {(4f - tempoDesdeUltimoMovimento):F1}s antes de mover novamente.");
            }
        }
        
        // O - Mostrar status de ogivas
        if (Input.GetKeyDown(KeyCode.O))
            MostrarOgivasDisponiveis();
        
        // I - Toggle Modo Mira
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (!modoMira && misseisDisponiveis > 0)
                IniciarModoMira();
            else if (modoMira)
                CancelarModoMira();
            else if (misseisDisponiveis <= 0)
                Debug.Log("[USS Leviathan] Sem mísseis disponíveis!");
        }
        
        if (modoMira)
            ProcessarMira();
    }
    
    void MostrarOgivasDisponiveis()
    {
        Debug.Log($"╔══════════════════════════════════════╗");
        Debug.Log($"║  USS LEVIATHAN - STATUS ARMAMENTO   ║");
        Debug.Log($"╠══════════════════════════════════════╣");
        Debug.Log($"║  Ogivas Disponíveis: {misseisDisponiveis}/{totalLocaisValidos}              ║");
        Debug.Log($"║  Status: {(estaSubmerso ? "SUBMERSO   " : "SUPERFÍCIE ")}                 ║");
        Debug.Log($"╚══════════════════════════════════════╝");
    }
    
    void IniciarModoMira()
    {
        modoMira = true;
        
        if (agente != null)
        {
            agente.ResetPath();
            agente.velocity = Vector3.zero;
        }
        velocidadeAtualSimulada = 0f;
        lemeAtual = 0f;
        
        Debug.Log("[USS Leviathan] 🎯 MODO MIRA ATIVADO — Clique DIREITO para disparar. 'I' para cancelar.");
        AtivarCursorMira();
    }
    
    void CancelarModoMira()
    {
        modoMira = false;
        Debug.Log("[USS Leviathan] ❌ Modo mira cancelado.");
        DesativarCursorMira();
    }
    
    void AtivarCursorMira()
    {
        if (VisualMiraSubmarino.Instancia != null)
            VisualMiraSubmarino.Instancia.AtivarMira();
    }
    
    void DesativarCursorMira()
    {
        if (VisualMiraSubmarino.Instancia != null)
            VisualMiraSubmarino.Instancia.DesativarMira();
    }
    
    void ProcessarMira()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (cameraPrincipal == null) return;
            Ray ray = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                // FIX #6 — Valida alcance antes de disparar
                float distancia = Vector3.Distance(transform.position, hit.point);
                if (distancia > alcanceMisseis)
                {
                    Debug.Log($"[USS Leviathan] ⚠️ Alvo fora de alcance! ({distancia:F0}m / máx {alcanceMisseis:F0}m)");
                    return; // Não sai do modo mira — deixa o jogador escolher outro ponto
                }

                pontoAlvoAtual = hit.point;
                DispararMissel(pontoAlvoAtual);
                CancelarModoMira();
            }
        }
    }
    
    void DispararMissel(Vector3 alvo)
    {
        if (misseisDisponiveis <= 0)
        {
            Debug.Log("[USS Leviathan] Sem mísseis disponíveis!");
            CancelarModoMira();
            return;
        }
        
        if (prefabMisselSubmarino == null)
        {
            Debug.LogError("[USS Leviathan] Prefab do míssil não está configurado!", this);
            return;
        }

        // FIX #3 — Itera apenas sobre slots válidos (não-nulos)
        for (int i = 0; i < locaisDisparo.Length; i++)
        {
            // Pula slots nulos sem errar — FIX #3
            if (locaisDisparo[i] == null) continue;
            if (misseisUsados[i]) continue;

            GameObject missel = Instantiate(prefabMisselSubmarino, locaisDisparo[i].position, locaisDisparo[i].rotation);
            
            MisselSubmarino scriptMissel = missel.GetComponent<MisselSubmarino>();
            if (scriptMissel != null)
            {
                scriptMissel.IniciarLancamento(alvo, estaSubmerso);
                MissileThreatTracker.RegistrarLancamento(missel, this, alvo, null, MissileThreatTracker.EstimarVelocidade(missel));
            }
            
            misseisUsados[i] = true;
            misseisDisponiveis--;
            
            Debug.Log($"[USS Leviathan] 🚀 Míssil do slot {i + 1} disparado! ({misseisDisponiveis} restantes) → Alvo: {alvo}");
            return;
        }

        Debug.LogWarning("[USS Leviathan] Nenhum slot de míssil disponível foi encontrado.", this);
    }
    
    IEnumerator Subir()
    {
        emMovimento = true;
        ultimoMovimento = Time.time;
        
        Debug.Log("[USS Leviathan] ⬆️ Subindo para superfície...");
        
        float navMeshY = transform.position.y - (agente != null ? agente.baseOffset : 0f);
        float offsetDesejado = alturaSuperificie - navMeshY;
        float offsetInicial = agente != null ? agente.baseOffset : 0f;
        
        float distancia = Mathf.Abs(offsetDesejado - offsetInicial);
        float duracao = distancia / velocidadeMovimento;
        float tempoDecorrido = 0f;
        
        if (duracao > 0.1f)
        {
            while (tempoDecorrido < duracao)
            {
                tempoDecorrido += Time.deltaTime;
                if (agente != null)
                    agente.baseOffset = Mathf.Lerp(offsetInicial, offsetDesejado, tempoDecorrido / duracao);
                yield return null;
            }
        }
        
        if (agente != null) agente.baseOffset = offsetDesejado;
        estaSubmerso = false;
        emMovimento = false;
        
        Debug.Log("[USS Leviathan] 🌊 Na superfície!");
    }
    
    IEnumerator Descer()
    {
        emMovimento = true;
        ultimoMovimento = Time.time;
        
        Debug.Log("[USS Leviathan] ⬇️ Descendo...");
        
        float navMeshY = transform.position.y - (agente != null ? agente.baseOffset : 0f);
        float offsetDesejado = profundidadeSubmersao - navMeshY;
        float offsetInicial = agente != null ? agente.baseOffset : 0f;
        
        float distancia = Mathf.Abs(offsetDesejado - offsetInicial);
        float duracao = distancia / velocidadeMovimento;
        float tempoDecorrido = 0f;
        
        if (duracao > 0.1f)
        {
            while (tempoDecorrido < duracao)
            {
                tempoDecorrido += Time.deltaTime;
                if (agente != null)
                    agente.baseOffset = Mathf.Lerp(offsetInicial, offsetDesejado, tempoDecorrido / duracao);
                yield return null;
            }
        }
        
        if (agente != null) agente.baseOffset = offsetDesejado;
        estaSubmerso = true;
        emMovimento = false;
        
        Debug.Log("[USS Leviathan] 🔵 Submerso!");
    }
    
    // --- API PÚBLICA ---

    public int GetMisseisDisponiveis() => misseisDisponiveis;
    public bool EstaSubmerso()         => estaSubmerso;

    [ContextMenu("Recarregar Todos os Mísseis")]
    public void RecarregarMisseis()
    {
        for (int i = 0; i < misseisUsados.Length; i++)
            misseisUsados[i] = false;
        misseisDisponiveis = totalLocaisValidos;
        Debug.Log($"[USS Leviathan] ✅ Todos os {totalLocaisValidos} mísseis recarregados!");
    }

    /// <summary>Recebe destino via clique do jogador ou IA.</summary>
    public void DefinirDestino(Vector3 destino)
    {
        if (agente != null && agente.enabled && agente.isOnNavMesh)
        {
            agente.SetDestination(destino);
            agente.isStopped = false;
        }
    }

    // --- NAVEGAÇÃO REALISTA ---

    void ExecutarMarchaFrenteRealista()
    {
        Vector3 direcaoDesejada = (agente.steeringTarget - transform.position).normalized;
        direcaoDesejada.y = 0;

        float angulo = Vector3.SignedAngle(transform.forward, direcaoDesejada, Vector3.up);
        float lemeAlvo = Mathf.Clamp(angulo / 30.0f, -1f, 1f);

        lemeAtual = Mathf.MoveTowards(lemeAtual, lemeAlvo, Time.deltaTime * 2.0f);

        velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, velocidadeOriginal, Time.deltaTime * aceleracao);
        
        float fluxoAgua = Mathf.Abs(velocidadeAtualSimulada) + 2f; 
        float eficienciaLeme = Mathf.Clamp01(fluxoAgua / 2.0f); 

        float giroReal = lemeAtual * velocidadeGiroMax * Time.deltaTime * eficienciaLeme;
        transform.Rotate(0, giroReal, 0);

        agente.velocity = transform.forward * velocidadeAtualSimulada;
    }

    void AtualizarInclinacaoNavio()
    {
        if (modelo3D == null) return;
        
        float giroFrame = lemeAtual * velocidadeGiroMax; 
        float anguloAlvo = -giroFrame * (forcaInclinacao / 10f); 
        anguloAlvo = Mathf.Clamp(anguloAlvo, -10f, 10f);
        
        Vector3 rotAtual = modelo3D.localEulerAngles;
        float zAtual = rotAtual.z > 180 ? rotAtual.z - 360f : rotAtual.z;
        float zNovo = Mathf.Lerp(zAtual, anguloAlvo, Time.deltaTime * 2.0f);
        modelo3D.localEulerAngles = new Vector3(rotAtual.x, rotAtual.y, zNovo);
    }
    
    void AtualizarRastroAgua()
    {
        if (rastroAgua == null) return;
        rastroAgua.emitting = !estaSubmerso && velocidadeAtualSimulada > 1.0f;
    }

    // --- GIZMOS ---

    void OnDrawGizmosSelected()
    {
        // Posição submersa
        Gizmos.color = Color.blue;
        Vector3 posSubmersa = new Vector3(transform.position.x, profundidadeSubmersao, transform.position.z);
        Gizmos.DrawWireSphere(posSubmersa, 2f);
        
        // Posição de superfície
        Gizmos.color = Color.cyan;
        Vector3 posSuperficie = new Vector3(transform.position.x, alturaSuperificie, transform.position.z);
        Gizmos.DrawWireSphere(posSuperficie, 2f);
        Gizmos.DrawLine(posSubmersa, posSuperficie);
        
        // Alcance de ataque
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        DrawCircle(transform.position, alcanceMisseis, 100);
        
        // Locais de disparo
        if (locaisDisparo != null)
        {
            for (int i = 0; i < locaisDisparo.Length; i++)
            {
                if (locaisDisparo[i] == null) continue;
                bool usado = (misseisUsados != null && i < misseisUsados.Length) ? misseisUsados[i] : false;
                Gizmos.color = usado ? Color.red : Color.green;
                Gizmos.DrawWireSphere(locaisDisparo[i].position, 0.5f);
            }
        }
        
        // Linha de mira ativa no editor
        if (modoMira && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                bool dentroDoAlcance = Vector3.Distance(transform.position, hit.point) <= alcanceMisseis;
                Gizmos.color = dentroDoAlcance ? Color.yellow : Color.red;
                Gizmos.DrawLine(transform.position, hit.point);
                Gizmos.DrawWireSphere(hit.point, dentroDoAlcance ? 5f : 8f);
            }
        }
    }
    
    void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
