using UnityEngine;
using UnityEngine.AI;

public class ControleUnidade : MonoBehaviour
{
    private NavMeshAgent agente;
    private Animator animator; // Referência para as animações
    
    public GameObject anelSelecao; 
    public bool selecionado = false;

    // --- CONTROLE AÉREO ---
    private VooHelicoptero scriptVoo;
    private bool ehAereo = false;
    private Vector3 destinoAereo;
    private bool voando = false;
    public float velocidadeVoo = 8.0f; // Velocidade base para helicópteros

    // --- DETECÇÃO DE CONFLITO ---
    private HelicopterController helicopteroExterno;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>(); 
        
        // Verifica controladores externos
        helicopteroExterno = GetComponent<HelicopterController>();
        
        // Verifica se é uma unidade aérea (GENÉRICA)
        scriptVoo = GetComponent<VooHelicoptero>();
        if (scriptVoo != null || helicopteroExterno != null)
        {
            ehAereo = true;
            if(agente != null) 
            {
                agente.enabled = false;
                agente = null; 
            }
        }
    }

    void Start()
    {
        CriarSelecaoVisual();
        if(anelSelecao != null) anelSelecao.SetActive(selecionado);
    }

    [Header("Visual")]
    public float tamanhoSelecao = 0f; // 0 = Automatico
    public Color corSelecao = new Color(1f, 1f, 1f, 0.4f); // Branco semi-transparente (estilo padrão)

    void CriarSelecaoVisual()
    {
        if (anelSelecao != null) return;

        // Cria o anel
        anelSelecao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(anelSelecao.GetComponent<Collider>());
        anelSelecao.transform.SetParent(this.transform);
        
        // Posição: Levemente acima do chão para evitar Z-Fighting
        anelSelecao.transform.localPosition = new Vector3(0, 0.05f, 0);
        
        // --- CÁLCULO DE TAMANHO AUTOMÁTICO ---
        float diametroFinal = 1.5f; // Padrão soldado

        if (tamanhoSelecao > 0)
        {
            diametroFinal = tamanhoSelecao;
        }
        else
        {
            // Tenta adivinhar pelo NavMeshAgent
            if (agente != null) diametroFinal = agente.radius * 2.5f;
            // Ou pelo Collider
            else 
            {
                Collider col = GetComponent<Collider>();
                if (col != null) diametroFinal = col.bounds.size.x * 1.2f;
            }
        }
        
        // Aplica escala (Y baixinho para parecer disco)
        anelSelecao.transform.localScale = new Vector3(diametroFinal, 0.02f, diametroFinal);
        
        // Material e Cor
        Renderer rend = anelSelecao.GetComponent<Renderer>();
        if(rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = corSelecao;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void Update()
    {
        // SE TIVER HELICOPTER CONTROLLER: NÃO FAZ NADA DE MOVIMENTO AQUI
        // Deixa o outro script cuidar de tudo, este fica só para Seleção/Identidade
        if (helicopteroExterno != null) return;

        float velocidadeAtual = 0f;

        // 1. Lógica Aérea (Movimento Reto)
        if (ehAereo && voando)
        {
            // Move em linha reta X/Z
            Vector3 direcao = destinoAereo - transform.position;
            direcao.y = 0; // Ignora altura no cálculo de direção (mantém altura atual ou ajusta depois)

            if (direcao.magnitude > 0.5f)
            {
                // Rotação
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, Time.deltaTime * 5f);

                // Movimento
                transform.position += transform.forward * velocidadeVoo * Time.deltaTime;
                velocidadeAtual = velocidadeVoo;
            }
            else
            {
                // Chegou
                voando = false;
                velocidadeAtual = 0f;
            }
            
            // Passa a velocidade para o script de voo (para inclinar)
            if(scriptVoo != null) scriptVoo.SetVelocidadeAtual(velocidadeAtual);
        }
        // 2. Lógica Terrestre (NavMesh)
        else if (agente != null && agente.enabled)
        {
            velocidadeAtual = agente.velocity.magnitude;
        }

        // 3. Controle de Animação (Genérico)
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat("Velocidade", velocidadeAtual);
        }

        // 4. Controle de Movimento pelo Mouse (se selecionado)
        // DESLIGADO AGORA: O GerenteSelecao controla o movimento em grupo!
        /*
        if (Input.GetMouseButtonDown(1) && selecionado)
        {
            MoverParaMouse();
        }
        */
    }

    void MoverParaMouse()
    {
        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit pontoDeColisao;

        if (Physics.Raycast(raio, out pontoDeColisao))
        {
            MoverParaPonto(pontoDeColisao.point);
        }
    }

    // COMANDO AUTOMÁTICO (Usado pela fábrica)
    public void MoverParaPonto(Vector3 destino)
    {
        if (ehAereo)
        {
            destinoAereo = destino;
            voando = true;
            return;
        }

        // Segurança extra. Se o agente ainda não foi pego, pega agora.
        if (agente == null) agente = GetComponent<NavMeshAgent>();

        if (agente != null)
        {
            // CORREÇÃO CRÍTICA: O NavMeshAgent precisa estar ativo e NO CHÃO para receber ordens.
            // Se ele acabou de nascer no ar ou foi desativado, essa verificação evita o erro.
            if (agente.isOnNavMesh && agente.isActiveAndEnabled)
            {
                // ✨ SISTEMA DE NAVEGAÇÃO NAVAL INTELIGENTE ✨
                // Verifica se esta unidade tem navegação naval inteligente (marcha à ré automática)
                NavegacaoInteligenteNaval navegacaoNaval = GetComponent<NavegacaoInteligenteNaval>();
                
                if (navegacaoNaval != null)
                {
                    // Usa o sistema inteligente que decide automaticamente se vai de frente ou de ré
                    navegacaoNaval.DefinirDestino(destino);
                    Debug.Log($"[Navegação] {name} usando sistema naval inteligente para destino em {destino}");
                }
                else
                {
                    // Navegação normal (terrestre ou navio sem o sistema inteligente)
                    agente.SetDestination(destino);
                }
            }
            else
            {
                // Opcional: Tenta "Warp" para a posição atual para forçar conexão se estiver muito perto
                // mas geralmente é melhor apenas esperar o próximo frame.
                // Debug.LogWarning($"Unidade {name} ignorou movimento pois não está no NavMesh.");
            }
        }
    }

    [Header("Visual de Alcance")]
    public LineRenderer linhaAlcance;
    public Color corAlcance = new Color(1f, 0f, 0f, 0.8f); // Vermelho
    public float larguraLinha = 0.15f;
    public int segmentosCirculo = 50;

    void CriarVisualAlcance()
    {
        if (linhaAlcance != null) return;

        // Tenta pegar o alcance da Torreta ou Sistema de Tiro
        float alcance = 0f;
        var torreta = GetComponent<ControleTorreta>();
        if (torreta != null) alcance = torreta.alcance;
        else 
        {
            var tiro = GetComponent<SistemaDeTiro>();
            if (tiro != null) alcance = tiro.alcanceTiro;
        }

        // Se não tem alcance de tiro, não desenha nada
        if (alcance <= 0) return;

        // Cria o LineRenderer
        GameObject objLinha = new GameObject("LinhaAlcance");
        objLinha.transform.SetParent(this.transform);
        objLinha.transform.localPosition = Vector3.zero;
        
        linhaAlcance = objLinha.AddComponent<LineRenderer>();
        linhaAlcance.useWorldSpace = false; // Relativo ao pai (se mover, move junto)
        linhaAlcance.startWidth = larguraLinha;
        linhaAlcance.endWidth = larguraLinha;
        linhaAlcance.positionCount = segmentosCirculo + 1;
        linhaAlcance.loop = true;
        
        // Material simples (unlit) para brilhar
        linhaAlcance.material = new Material(Shader.Find("Sprites/Default"));
        linhaAlcance.startColor = corAlcance;
        linhaAlcance.endColor = corAlcance;

        // Desenha o círculo
        float angulo = 0f;
        for (int i = 0; i < segmentosCirculo + 1; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angulo) * alcance;
            float z = Mathf.Cos(Mathf.Deg2Rad * angulo) * alcance;
            
            linhaAlcance.SetPosition(i, new Vector3(x, 0.5f, z)); // 0.5f de altura do chão
            
            angulo += (360f / segmentosCirculo);
        }
        
        linhaAlcance.gameObject.SetActive(false); // Começa invisível
    }

    public void DefinirSelecao(bool estado)
    {
        selecionado = estado;
        
        // Círculo Verde (Seleção)
        if (anelSelecao != null) anelSelecao.SetActive(estado);
        
        // Círculo Vermelho (Alcance)
        if (estado)
        {
            // Tenta criar se não existir (lazy visualization)
            if (linhaAlcance == null) CriarVisualAlcance();
            
            if (linhaAlcance != null) linhaAlcance.gameObject.SetActive(true);
        }
        else
        {
            if (linhaAlcance != null) linhaAlcance.gameObject.SetActive(false);
        }
    }
}
