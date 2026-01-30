using UnityEngine;

/// <summary>
/// EXEMPLO: Como usar a Navegação Naval Inteligente via código
/// Este script demonstra como comandar navios com o sistema de marcha à ré
/// Útil para: IA, Cutscenes, Patrulhas automáticas, etc.
/// </summary>
public class ExemploUsoNavegacaoNaval : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject navio; // Arraste seu navio aqui
    
    [Header("Testes de Navegação")]
    public Transform pontoA; // Destino na frente
    public Transform pontoB; // Destino atrás (teste de ré)
    public Transform pontoC; // Destino longe atrás
    
    private NavegacaoInteligenteNaval navegacao;
    
    void Start()
    {
        // Pega a referência do sistema de navegação
        if (navio != null)
        {
            navegacao = navio.GetComponent<NavegacaoInteligenteNaval>();
            
            if (navegacao == null)
            {
                Debug.LogError("Navio não tem NavegacaoInteligenteNaval!");
            }
        }
    }
    
    void Update()
    {
        if (navegacao == null) return;
        
        // EXEMPLO 1: Comandos por teclado (para testes)
        if (Input.GetKeyDown(KeyCode.Alpha1)) IrParaPontoA();
        if (Input.GetKeyDown(KeyCode.Alpha2)) IrParaPontoB();
        if (Input.GetKeyDown(KeyCode.Alpha3)) IrParaPontoC();
        if (Input.GetKeyDown(KeyCode.Alpha4)) TestarMarchaReManual();
        
        // EXEMPLO 2: Mostra estado atual
        if (Input.GetKeyDown(KeyCode.Space))
        {
            MostrarEstadoAtual();
        }
    }
    
    /// <summary>
    /// EXEMPLO: Ir para ponto na frente (irá de frente)
    /// </summary>
    void IrParaPontoA()
    {
        if (pontoA == null) return;
        
        Debug.Log("🟢 TESTE 1: Indo para Ponto A (frente)");
        navegacao.DefinirDestino(pontoA.position);
    }
    
    /// <summary>
    /// EXEMPLO: Ir para ponto atrás próximo (deverá ir de ré)
    /// </summary>
    void IrParaPontoB()
    {
        if (pontoB == null) return;
        
        Debug.Log("🔴 TESTE 2: Indo para Ponto B (atrás/perto - deve ir de ré!)");
        navegacao.DefinirDestino(pontoB.position);
    }
    
    /// <summary>
    /// EXEMPLO: Ir para ponto atrás longe (irá de frente por estar longe)
    /// </summary>
    void IrParaPontoC()
    {
        if (pontoC == null) return;
        
        Debug.Log("🟢 TESTE 3: Indo para Ponto C (atrás/longe - vai de frente)");
        navegacao.DefinirDestino(pontoC.position);
    }
    
    /// <summary>
    /// EXEMPLO: Testar marcha ré forçada calculando posição atrás
    /// </summary>
    void TestarMarchaReManual()
    {
        // Calcula um ponto 10 metros atrás do navio
        Vector3 destinoAtras = navio.transform.position - navio.transform.forward * 10f;
        
        Debug.Log("🔴 TESTE 4: Indo 10m para trás (posição calculada - deve ir de ré!)");
        navegacao.DefinirDestino(destinoAtras);
    }
    
    /// <summary>
    /// Mostra informações do estado atual da navegação
    /// </summary>
    void MostrarEstadoAtual()
    {
        if (navegacao.EstaEmMarchaRe())
        {
            Debug.Log("⚠️ NAVIO EM MARCHA À RÉ!");
        }
        else
        {
            Debug.Log("✅ Navio em marcha à frente (normal)");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 📚 EXEMPLOS AVANÇADOS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// EXEMPLO AVANÇADO: Patrulha automática marítima
    /// </summary>
    [Header("Patrulha Automática")]
    public Transform[] pontosPatrulha;
    public float tempoEsperaNosPontos = 3f;
    
    private int pontoAtualPatrulha = 0;
    private float timerEspera = 0f;
    private bool iniciouPatrulha = false;
    
    public void IniciarPatrulha()
    {
        if (pontosPatrulha == null || pontosPatrulha.Length == 0)
        {
            Debug.LogWarning("Nenhum ponto de patrulha definido!");
            return;
        }
        
        iniciouPatrulha = true;
        pontoAtualPatrulha = 0;
        IrParaProximoPontoPatrulha();
    }
    
    void IrParaProximoPontoPatrulha()
    {
        if (pontosPatrulha[pontoAtualPatrulha] != null)
        {
            navegacao.DefinirDestino(pontosPatrulha[pontoAtualPatrulha].position);
            Debug.Log($"🚢 Patrulha: Indo para ponto {pontoAtualPatrulha + 1}/{pontosPatrulha.Length}");
        }
    }
    
    /// <summary>
    /// EXEMPLO AVANÇADO: Atracar no porto (aproximação final de ré)
    /// </summary>
    public Transform docaDoPorto;
    public float distanciaAproximacao = 30f; // Aproxima-se até aqui normalmente
    public float distanciaAtracao = 5f; // Últimos metros de ré
    
    public void AtracarNoPorto()
    {
        if (docaDoPorto == null) return;
        
        Vector3 posicaoNavio = navio.transform.position;
        Vector3 posicaoDoca = docaDoPorto.position;
        float distancia = Vector3.Distance(posicaoNavio, posicaoDoca);
        
        // Fase 1: Se está longe, aproxima normalmente
        if (distancia > distanciaAproximacao)
        {
            Debug.Log("🟢 Aproximando do porto...");
            navegacao.DefinirDestino(posicaoDoca);
        }
        // Fase 2: Chegou perto, faz manobra de atracação de ré
        else
        {
            Debug.Log("🔴 Iniciando manobra de atracação (marcha à ré)");
            
            // Calcula posição de ré ideal (um pouco afastado da doca)
            Vector3 direcaoDoca = (posicaoDoca - posicaoNavio).normalized;
            Vector3 posicaoAtracacao = posicaoDoca - direcaoDoca * distanciaAtracao;
            
            navegacao.DefinirDestino(posicaoAtracacao);
        }
    }
    
    /// <summary>
    /// EXEMPLO AVANÇADO: Evasão de projéteis (movimento lateral de ré)
    /// </summary>
    public void EvadirProjetil(Vector3 direcaoProjetil)
    {
        // Calcula direção perpendicular ao projétil
        Vector3 direcaoEvasao = Vector3.Cross(direcaoProjetil, Vector3.up).normalized;
        
        // Move para o lado E para trás ao mesmo tempo
        Vector3 destinoEvasao = navio.transform.position 
            + direcaoEvasao * 10f // 10m para o lado
            - navio.transform.forward * 8f; // 8m para trás
        
        Debug.Log("⚠️ MANOBRA EVASIVA! Movendo lateralmente de ré!");
        navegacao.DefinirDestino(destinoEvasao);
    }
    
    /// <summary>
    /// EXEMPLO AVANÇADO: Formação de esquadra (múltiplos navios)
    /// </summary>
    [Header("Formação de Esquadra")]
    public GameObject[] naviosDaEsquadra;
    public float espacamentoFormacao = 15f;
    
    public void FormarLinhaDeNavios(Vector3 centroFormacao)
    {
        if (naviosDaEsquadra == null || naviosDaEsquadra.Length == 0) return;
        
        int total = naviosDaEsquadra.Length;
        float larguraTotal = (total - 1) * espacamentoFormacao;
        Vector3 inicio = centroFormacao - Vector3.right * (larguraTotal / 2f);
        
        for (int i = 0; i < total; i++)
        {
            if (naviosDaEsquadra[i] == null) continue;
            
            NavegacaoInteligenteNaval nav = naviosDaEsquadra[i].GetComponent<NavegacaoInteligenteNaval>();
            if (nav == null) continue;
            
            Vector3 posicaoNaFormacao = inicio + Vector3.right * (i * espacamentoFormacao);
            nav.DefinirDestino(posicaoNaFormacao);
            
            Debug.Log($"🚢 Navio {i+1} indo para posição na formação");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 🎯 CALLBACKS E EVENTOS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// EXEMPLO: Detectar quando chegou no destino
    /// Útil para sequências de comandos
    /// </summary>
    bool ChegouNoDestino()
    {
        // Você precisaria adicionar este método público ao NavegacaoInteligenteNaval:
        // public bool ChegouNoDestino() { return !temDestino; }
        
        // Por enquanto, verificamos manualmente:
        UnityEngine.AI.NavMeshAgent agente = navio.GetComponent<UnityEngine.AI.NavMeshAgent>();
        
        if (agente != null && agente.hasPath)
        {
            return agente.remainingDistance <= agente.stoppingDistance;
        }
        
        return true;
    }
    
    /// <summary>
    /// EXEMPLO: Sequência de comandos automática
    /// </summary>
    public void ExecutarSequenciaComandos()
    {
        StartCoroutine(SequenciaDeManobras());
    }
    
    System.Collections.IEnumerator SequenciaDeManobras()
    {
        Debug.Log("🎬 Iniciando sequência de manobras automáticas...");
        
        // 1. Ir para frente
        if (pontoA != null)
        {
            navegacao.DefinirDestino(pontoA.position);
            Debug.Log("1️⃣ Indo para frente...");
            yield return new WaitUntil(() => ChegouNoDestino());
            yield return new WaitForSeconds(2f);
        }
        
        // 2. Dar ré
        if (pontoB != null)
        {
            navegacao.DefinirDestino(pontoB.position);
            Debug.Log("2️⃣ Dando marcha à ré...");
            yield return new WaitUntil(() => ChegouNoDestino());
            yield return new WaitForSeconds(2f);
        }
        
        // 3. Retornar à origem
        navegacao.DefinirDestino(navio.transform.position);
        Debug.Log("3️⃣ Retornando à posição inicial...");
        yield return new WaitUntil(() => ChegouNoDestino());
        
        Debug.Log("✅ Sequência completa!");
    }
}
