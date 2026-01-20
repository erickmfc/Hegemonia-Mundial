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

    // O Awake roda NA HORA que o objeto nasce, antes de receber ordens.
    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>(); // Pega o Animator do próprio objeto
        
        // Verifica se é uma unidade aérea
        scriptVoo = GetComponent<VooHelicoptero>();
        if (scriptVoo != null)
        {
            ehAereo = true;
            // Desliga o NavMeshAgent para não prender no chão ou colidir com paredes invisíveis do Mesh
            if(agente != null) 
            {
                agente.enabled = false;
                agente = null; // Anula a referência para forçar lógica manual
            }
        }
    }

    void Start()
    {
        CriarSelecaoVisual();
        // Garante que comece deselecionado visualmente
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
        if (animator != null)
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
                agente.SetDestination(destino);
            }
            else
            {
                // Opcional: Tenta "Warp" para a posição atual para forçar conexão se estiver muito perto
                // mas geralmente é melhor apenas esperar o próximo frame.
                // Debug.LogWarning($"Unidade {name} ignorou movimento pois não está no NavMesh.");
            }
        }
    }

    public void DefinirSelecao(bool estado)
    {
        selecionado = estado;
        if (anelSelecao != null)
        {
            anelSelecao.SetActive(estado);
        }
    }
}
