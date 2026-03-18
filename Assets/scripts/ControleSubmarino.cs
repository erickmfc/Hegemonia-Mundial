using UnityEngine;
using System.Collections;

/// <summary>
/// Controle do Submarino com sistema de mísseis selecionável
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
    [Tooltip("22 locais de disparo de mísseis")]
    public Transform[] locaisDisparo = new Transform[22];
    
    [Tooltip("Prefab do míssil submarino")]
    public GameObject prefabMisselSubmarino;
    
    [Header("Status")]
    public bool estaSubmerso = true;
    public int misseisDisponiveis = 22;
    
    // Estado interno
    private bool emMovimento = false;
    private float ultimoMovimento = -4f;
    private bool[] misseisUsados = new bool[22];
    private bool modoMira = false;
    private Vector3 pontoAlvoAtual;
    
    // --- VARIÁVEIS DE NAVEGAÇÃO REALISTA ---
    [Header("Física de Navegação")]
    [Tooltip("Velocidade máxima de rotação do leme (graus por segundo).")]
    public float velocidadeGiroMax = 15f; 
    [Tooltip("Quanto tempo o submarino demora para acelerar totalmente (inércia).")]
    public float aceleracao = 1.5f;
    [Tooltip("Inclinação visual nas curvas")]
    public float forcaInclinacao = 3.0f;
    public Transform modelo3D; // Arraste o visual aqui
    public TrailRenderer rastroAgua;

    private UnityEngine.AI.NavMeshAgent agente;
    private float velocidadeOriginal;
    private float velocidadeAtualSimulada = 0f;
    private float lemeAtual = 0f;
    
    void Start()
    {
        agente = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agente != null)
        {
            velocidadeOriginal = agente.speed;
            // Configura para movimento manual
            agente.updateRotation = false; 
            agente.acceleration = 9999;
        }
        
        if (rastroAgua == null) rastroAgua = GetComponentInChildren<TrailRenderer>();

        // Lógica de Inicialização Inteligente (Compatível com Estaleiro)
        // Se nascer perto da superfície (Y > -5), começa como superfície.
        // Se nascer fundo, começa submerso.
        // Se nascer fundo, começa submerso.
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
        
        // Todos os mísseis disponíveis
        for (int i = 0; i < 22; i++)
        {
            misseisUsados[i] = false;
        }
    }
    
    void Update()
    {
        // Verifica se ESTE GameObject está selecionado (não outros)
        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle == null || !controle.selecionado) 
        {
            // Se não está selecionado e estava no modo mira, cancela
            if (modoMira)
            {
                modoMira = false;
                DesativarCursorMira();
            }
            return;
        }
        
        float tempoDesdeUltimoMovimento = Time.time - ultimoMovimento;

        // --- MOVIMENTO REALISTA (Estilo Liberty) ---
        if (agente != null && agente.enabled)
        {
            // Se está em modo mira, força parada total
            if (modoMira)
            {
                velocidadeAtualSimulada = 0f;
                agente.velocity = Vector3.zero;
                agente.ResetPath();
            }
            else if (agente.hasPath && agente.remainingDistance > agente.stoppingDistance)
            {
                // Movimento com física de leme
                ExecutarMarchaFrenteRealista();
            }
            else
            {
                // Freio suave (inércia na água)
                velocidadeAtualSimulada = Mathf.Lerp(velocidadeAtualSimulada, 0f, Time.deltaTime * 0.5f);
                agente.velocity = transform.forward * velocidadeAtualSimulada;
            }
            
            // Visual
            AtualizarInclinacaoNavio();
            AtualizarRastroAgua();
        }
        
        // Tecla U - Subir
        if (Input.GetKeyDown(KeyCode.U))
        {
            if (tempoDesdeUltimoMovimento >= 4f && !emMovimento)
            {
                if (estaSubmerso)
                {
                    StartCoroutine(Subir());
                }
                else
                {
                    Debug.Log("Submarino já está na superfície!");
                }
            }
            else
            {
                Debug.Log($"[Submarino] Aguarde {4f - tempoDesdeUltimoMovimento:F1}s antes de mover novamente.");
            }
        }
        
        // Tecla P - Descer
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (tempoDesdeUltimoMovimento >= 4f && !emMovimento)
            {
                if (!estaSubmerso)
                {
                    StartCoroutine(Descer());
                }
                else
                {
                    Debug.Log("Submarino já está submerso!");
                }
            }
            else
            {
                Debug.Log($"[Submarino] Aguarde {4f - tempoDesdeUltimoMovimento:F1}s antes de mover novamente.");
            }
        }
        
        // Tecla O - Mostrar ogivas disponíveis
        if (Input.GetKeyDown(KeyCode.O))
        {
            MostrarOgivasDisponiveis();
        }
        
        // Tecla I - TOGGLE Modo Mira (Liga/Desliga)
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (!modoMira && misseisDisponiveis > 0)
            {
                IniciarModoMira();
            }
            else if (modoMira)
            {
                CancelarModoMira();
            }
            else if (misseisDisponiveis <= 0)
            {
                Debug.Log("[Submarino] Sem mísseis disponíveis!");
            }
        }
        
        // Se está no modo mira, processa
        if (modoMira)
        {
            ProcessarMira();
        }
    }
    
    void MostrarOgivasDisponiveis()
    {
        Debug.Log($"╔══════════════════════════════════════╗");
        Debug.Log($"║  SUBMARINO - STATUS ARMAMENTO       ║");
        Debug.Log($"╠══════════════════════════════════════╣");
        Debug.Log($"║  Ogivas Disponíveis: {misseisDisponiveis}/22          ║");
        Debug.Log($"║  Status: {(estaSubmerso ? "SUBMERSO" : "SUPERFÍCIE")}              ║");
        Debug.Log($"╚══════════════════════════════════════╝");
        
        // Também pode mostrar UI se tiver
        // Exemplo: UIManager.Instance.MostrarInfoSubmarino(misseisDisponiveis, estaSubmerso);
    }
    
    void IniciarModoMira()
    {
        modoMira = true;
        
        // Para o submarino imediatamente
        if (agente != null)
        {
            agente.ResetPath();
            agente.velocity = Vector3.zero;
        }
        velocidadeAtualSimulada = 0f;
        lemeAtual = 0f;
        
        Debug.Log("[Submarino] 🎯 MODO MIRA ATIVADO - Clique BOTÃO DIREITO para disparar. Aperte 'I' novamente para cancelar.");
        AtivarCursorMira();
    }
    
    void CancelarModoMira()
    {
        modoMira = false;
        Debug.Log("[Submarino] ❌ Modo mira cancelado. Submarino livre para navegar.");
        DesativarCursorMira();
    }
    
    void AtivarCursorMira()
    {
        // Ativa visual de mira
        if (VisualMiraSubmarino.Instancia != null)
        {
            VisualMiraSubmarino.Instancia.AtivarMira();
        }
    }
    
    void DesativarCursorMira()
    {
        // Desativa visual de mira
        if (VisualMiraSubmarino.Instancia != null)
        {
            VisualMiraSubmarino.Instancia.DesativarMira();
        }
    }
    
    void ProcessarMira()
    {
        // Detecta clique do BOTÃO DIREITO do mouse (disparo)
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                pontoAlvoAtual = hit.point;
                DispararMissel(pontoAlvoAtual);
                
                // Após disparar, sai do modo mira automaticamente
                CancelarModoMira();
            }
        }
    }
    
    void DispararMissel(Vector3 alvo)
    {
        if (misseisDisponiveis <= 0)
        {
            Debug.Log("[Submarino] Sem mísseis disponíveis!");
            CancelarModoMira();
            return;
        }
        
        // Encontra o primeiro míssil disponível
        for (int i = 0; i < 22; i++)
        {
            if (!misseisUsados[i] && locaisDisparo[i] != null)
            {
                // Cria o míssil
                GameObject missel = Instantiate(prefabMisselSubmarino, locaisDisparo[i].position, locaisDisparo[i].rotation);
                
                // Configura o míssil
                MisselSubmarino scriptMissel = missel.GetComponent<MisselSubmarino>();
                if (scriptMissel != null)
                {
                    scriptMissel.IniciarLancamento(alvo, estaSubmerso);
                }
                
                // Marca como usado
                misseisUsados[i] = true;
                misseisDisponiveis--;
                
                Debug.Log($"[Submarino] 🚀 Míssil {i + 1} disparado! ({misseisDisponiveis} restantes)");
                Debug.Log($"[Submarino] 🎯 Alvo: {alvo}");
                return;
            }
        }
    }
    
    IEnumerator Subir()
    {
        emMovimento = true;
        ultimoMovimento = Time.time;
        
        Debug.Log("[Submarino] Subindo para superfície...");
        
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
                float progresso = tempoDecorrido / duracao;
                
                if (agente != null) agente.baseOffset = Mathf.Lerp(offsetInicial, offsetDesejado, progresso);
                yield return null;
            }
        }
        
        if (agente != null) agente.baseOffset = offsetDesejado;
        estaSubmerso = false;
        emMovimento = false;
        
        Debug.Log("[Submarino] Na superfície!");
    }
    
    IEnumerator Descer()
    {
        emMovimento = true;
        ultimoMovimento = Time.time;
        
        Debug.Log("[Submarino] Descendo...");
        
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
                float progresso = tempoDecorrido / duracao;
                
                if (agente != null) agente.baseOffset = Mathf.Lerp(offsetInicial, offsetDesejado, progresso);
                yield return null;
            }
        }
        
        if (agente != null) agente.baseOffset = offsetDesejado;
        estaSubmerso = true;
        emMovimento = false;
        
        Debug.Log("[Submarino] Submerso!");
    }
    
    // Método público para UI ou outros scripts verificarem
    public int GetMisseisDisponiveis()
    {
        return misseisDisponiveis;
    }
    
    // Recarregar mísseis (para testes ou quando voltar à base)
    [ContextMenu("Recarregar Todos os Mísseis")]
    public void RecarregarMisseis()
    {
        for (int i = 0; i < 22; i++)
        {
            misseisUsados[i] = false;
        }
        misseisDisponiveis = 22;
        Debug.Log("[Submarino] Todos os 22 mísseis recarregados!");
    }
    
    [Header("Alcance de Ataque")]
    public float alcanceMisseis = 500f; // Alcance máximo dos mísseis
    
    // Desenha informações no editor
    void OnDrawGizmosSelected()
    {
        // Desenha profundidade
        Gizmos.color = Color.blue;
        Vector3 posSubmersa = new Vector3(transform.position.x, profundidadeSubmersao, transform.position.z);
        Gizmos.DrawWireSphere(posSubmersa, 2f);
        
        // Desenha superfície
        Gizmos.color = Color.cyan;
        Vector3 posSuperficie = new Vector3(transform.position.x, alturaSuperificie, transform.position.z);
        Gizmos.DrawWireSphere(posSuperficie, 2f);
        
        // Linha conectando
        Gizmos.DrawLine(posSubmersa, posSuperficie);
        
        // Desenha alcance de ataque (círculo vermelho)
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        DrawCircle(transform.position, alcanceMisseis, 100);
        
        // Desenha locais de disparo
        if (locaisDisparo != null)
        {
            for (int i = 0; i < locaisDisparo.Length; i++)
            {
                if (locaisDisparo[i] != null)
                {
                    Gizmos.color = misseisUsados[i] ? Color.red : Color.green;
                    Gizmos.DrawWireSphere(locaisDisparo[i].position, 0.5f);
                }
            }
        }
        
        // Se está em modo mira, desenha linha até o mouse
        if (modoMira && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, hit.point);
                Gizmos.DrawWireSphere(hit.point, 5f);
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
    // --- MÉTODOS DE NAVEGAÇÃO REALISTA ---

    void ExecutarMarchaFrenteRealista()
    {
        Vector3 direcaoDesejada = (agente.steeringTarget - transform.position).normalized;
        direcaoDesejada.y = 0;

        // O SEGREDO DO "TRILHO" -> Mesma lógica mágica do cruzador
        float angulo = Vector3.SignedAngle(transform.forward, direcaoDesejada, Vector3.up);
        float lemeAlvo = Mathf.Clamp(angulo / 30.0f, -1f, 1f);

        // Giro imediato com maciez
        lemeAtual = Mathf.MoveTowards(lemeAtual, lemeAlvo, Time.deltaTime * 2.0f);

        // Aceleração forçada trancada
        velocidadeAtualSimulada = Mathf.MoveTowards(velocidadeAtualSimulada, velocidadeOriginal, Time.deltaTime * aceleracao);
        
        // Mantém a inércia do leme
        float fluxoAgua = Mathf.Abs(velocidadeAtualSimulada) + 2f; 
        float eficienciaLeme = Mathf.Clamp01(fluxoAgua / 2.0f); 

        // Gira o Submarino (Rotação Pura)
        float giroReal = lemeAtual * velocidadeGiroMax * Time.deltaTime * eficienciaLeme;
        transform.Rotate(0, giroReal, 0);

        // Empurra para a frente (A mágica que evita que ele deslize para as laterais)
        agente.velocity = transform.forward * velocidadeAtualSimulada;
    }

    void AtualizarInclinacaoNavio()
    {
        if (modelo3D == null) return;
        
        // Inclinação nas curvas (Banking)
        float giroFrame = lemeAtual * velocidadeGiroMax; 
        float anguloAlvo = -giroFrame * (forcaInclinacao / 10f); 
        anguloAlvo = Mathf.Clamp(anguloAlvo, -10f, 10f);
        
        // Mantém a rotação local original do modelo, só mexendo no Z
        Vector3 rotAtual = modelo3D.localEulerAngles;
        // Ajuste para lidar com 0-360
        float zAtual = rotAtual.z;
        if (zAtual > 180) zAtual -= 360;
        
        float zNovo = Mathf.Lerp(zAtual, anguloAlvo, Time.deltaTime * 2.0f);
        modelo3D.localEulerAngles = new Vector3(rotAtual.x, rotAtual.y, zNovo);
    }
    
    void AtualizarRastroAgua()
    {
        if (rastroAgua == null) return;
        // Só emite rastro se estiver na superfície e andando
        bool naSuperficie = !estaSubmerso; 
        bool andando = velocidadeAtualSimulada > 1.0f;
        
        rastroAgua.emitting = naSuperficie && andando;
    }

    /// <summary>
    /// Método público para receber comando de movimento (via clique/IA)
    /// </summary>
    public void DefinirDestino(Vector3 destino)
    {
        if (agente != null && agente.enabled)
        {
            if (agente.isOnNavMesh)
            {
                agente.SetDestination(destino);
                agente.isStopped = false;
            }
        }
    }
}
