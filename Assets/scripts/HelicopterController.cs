using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class HelicopterController : MonoBehaviour
{
    [Header("Configurações de Voo")]
    public float altitudeDeVoo = 12f;
    public float velocidadeHelice = 1000f;
    public float alturaDoSolo = 0f; 
    public float antygaviti = 2f; 
    public float velocidadeNavegacao = 20f; 
    public float rotacaoSuavidade = 2.5f; // Suavidade da curva

    [Header("Transporte")]
    public float distanciaParaEmbarque = 22.5f; 
    public int capacidadeMaxima = 8;
    public List<GameObject> tropasEmbarcadas = new List<GameObject>();

    [Header("Referências")]
    public Transform helicePrincipal; 
    public Transform heliceTraseira; 
    public NavMeshAgent agent; 
    public Vector3 eixoRotacaoTraseira = Vector3.forward;
    
    // Variáveis de controle
    private bool estaNoChao = true;
    private bool selecionado = false;
    private bool preparandoDesembarque = false;
    private GameObject anelSelecao; 

    // Variáveis de Voo Manual
    private Vector3 destinoVoo;
    private bool temDestino = false;

    void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        
        // Ajustamos o agente para não interferir se estiver ligado
        if (agent != null) 
        {
            agent.updatePosition = false; // Importante: não travar a posição
            agent.updateRotation = false; // Importante: não travar a rotação
            agent.updateUpAxis = false;
        } 
        CriarVisualSelecao();
    }

    void Update()
    {
        // 1. Seleção
        if (Input.GetMouseButtonDown(0)) VerificarSelecao();

        // 2. Hélices
        if (helicePrincipal != null) helicePrincipal.Rotate(Vector3.up * velocidadeHelice * Time.deltaTime);
        if (heliceTraseira != null) heliceTraseira.Rotate(eixoRotacaoTraseira * velocidadeHelice * 1.5f * Time.deltaTime);

        // 3. Comandos
        if (selecionado)
        {
            if (Input.GetMouseButtonDown(1)) 
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                // Raycast para o mundo ignorando triggers ou UI se possível, mas padrão funciona
                if (Physics.Raycast(ray, out hit))
                {
                    DefinirDestino(hit.point);
                }
            }

            if (Input.GetKeyDown(KeyCode.O)) TentarEmbarcarTropas();
            if (Input.GetKeyDown(KeyCode.U)) IniciarProtocoloDesembarque();
            if (Input.GetKeyDown(KeyCode.K)) Pousar();
        }

        // --- MOVIMENTO E FÍSICA ---
        ProcessarVoo();

        // --- CONFIRMAÇÃO VISUAL ---
        if(anelSelecao != null) anelSelecao.SetActive(selecionado);
    }

    // --- NOVA LÓGICA DE VOO MANUAL (Ignora NavMesh/Água) ---
    public void DefinirDestino(Vector3 alvo)
    {
        destinoVoo = alvo;
        temDestino = true;
        Decolar();
    }

    public void Decolar() 
    { 
        estaNoChao = false; 
        preparandoDesembarque = false;
        if(agent) agent.enabled = false; // Desativa NavMesh para voar livre
    }
    
    public void Pousar() 
    { 
        estaNoChao = true; 
        temDestino = false; 
    }

    float GetAlturaDoSoloAtual()
    {
        // MELHORIA: Raycast do "Céu" (1000m) para baixo.
        // Garante que pegamos o terreno mesmo se o helicóptero estiver enterrado, na água ou muito alto.
        // Isso evita que ele "perca" o chão e vá para Y=0 se o terreno for alto.
        
        Vector3 origem = new Vector3(transform.position.x, 1000f, transform.position.z);
        
        // Raio de 2000m para cobrir de +1000 até -1000
        RaycastHit[] hits = Physics.RaycastAll(origem, Vector3.down, 2000f);
        
        float maiorY = -1000f; 
        bool achouChao = false;

        foreach (RaycastHit hit in hits)
        {
            // O MAIS IMPORTANTE: Ignora a si mesmo para não tentar pousar no próprio teto
            if (hit.collider.transform.root == transform.root) continue;
            
            // Ignora triggers invisíveis (zonas de detecção etc)
            if (hit.collider.isTrigger) continue;

            // Queremos o ponto mais alto SÓLIDO (Chão, Prédio, Água com colisor)
            if (hit.point.y > maiorY)
            {
                maiorY = hit.point.y;
                achouChao = true;
            }
        }

        return achouChao ? maiorY : 0f;
    }

    void ProcessarVoo()
    {
        float soloY = GetAlturaDoSoloAtual();
        float alturaAlvoRelativa = estaNoChao ? alturaDoSolo : altitudeDeVoo;
        float yFinal = soloY + alturaAlvoRelativa;

        Vector3 posAtual = transform.position;
        Vector3 novaPos = posAtual;

        // 1. Movimento Horizontal (Se tiver destino e não estiver no chão)
        // Só movemos horizontalmente se já estivermos decolando ou voando (ou seja, subindo para voar)
        // Pequena melhoria: só move horizontalmente se já tiver altura razoável? Não, helicóptero pode "taxiar" ou voar baixo.
        
        float velocidadeAtual = 0f;

        if (!estaNoChao && temDestino)
        {
            Vector3 vetorDirecao = (destinoVoo - posAtual);
            vetorDirecao.y = 0; // Ignora altura para calcular direção e distância

            if (vetorDirecao.magnitude > 1.0f) // Tolerância de chegada
            {
                Vector3 moveDir = vetorDirecao.normalized * velocidadeNavegacao * Time.deltaTime;
                novaPos += moveDir;
                velocidadeAtual = velocidadeNavegacao;

                // Rotação suave para o destino
                if (vetorDirecao != Vector3.zero)
                {
                    Quaternion rotAlvo = Quaternion.LookRotation(vetorDirecao);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, Time.deltaTime * rotacaoSuavidade);
                }
            }
            else
            {
                temDestino = false; // Chegou
            }
        }

        // 2. Movimento Vertical (Lerp suave de altura)
        novaPos.y = Mathf.Lerp(posAtual.y, yFinal, Time.deltaTime * antygaviti);

        // Aplica Movimento
        transform.position = novaPos;

        // 3. Atualiza script visual (VooHelicoptero)
        VooHelicoptero visual = GetComponent<VooHelicoptero>();
        if(visual != null) visual.SetVelocidadeAtual(velocidadeAtual);

        // --- GESTÃO DE DESEMBARQUE ---
        // Se estamos descendo (preparando) e chegamos perto do chão...
        if (preparandoDesembarque)
        {
            float alturaAtual = transform.position.y - soloY;
            if (alturaAtual < (alturaDoSolo + 1.0f)) // Margem de 1 metro
            {
                ForcarDesembarqueAgora();
                preparandoDesembarque = false;
            }
        }
    }
    
    void IniciarProtocoloDesembarque()
    {
        if (tropasEmbarcadas.Count == 0) return;
        Pousar(); 
        preparandoDesembarque = true; 
    }

    public void ForcarDesembarqueAgora()
    {
        Debug.Log("Helicóptero no solo. Liberando tropas...");
        foreach (GameObject soldado in tropasEmbarcadas)
        {
            if (soldado != null)
            {
                soldado.SetActive(true); 
                
                // Tenta achar lugar válido no NavMesh perto do heli
                Vector3 pontoSoltura = transform.position + (Random.insideUnitSphere * 4f);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(pontoSoltura, out hit, 15f, NavMesh.AllAreas))
                {
                    NavMeshAgent soldierAgent = soldado.GetComponent<NavMeshAgent>();
                    if (soldierAgent != null)
                    {
                        soldierAgent.Warp(hit.position);
                        soldierAgent.SetDestination(hit.position); // Para garantir que fique parado
                    }
                    else
                    {
                        soldado.transform.position = hit.position;
                    }
                }
            }
        }
        tropasEmbarcadas.Clear();
    }

    void TentarEmbarcarTropas()
    {
        float alturaDoHeli = transform.position.y - GetAlturaDoSoloAtual();
        if (alturaDoHeli > 4f) 
        {
            Pousar();
            return;
        }

        GameObject[] soldadosNoMapa = GameObject.FindGameObjectsWithTag("Player");
        int count = 0;

        foreach (GameObject soldado in soldadosNoMapa)
        {
            if (tropasEmbarcadas.Count >= capacidadeMaxima) break;

            Vector3 posHeli = transform.position; posHeli.y = 0;
            Vector3 posSoldado = soldado.transform.position; posSoldado.y = 0;

            if (Vector3.Distance(posHeli, posSoldado) <= distanciaParaEmbarque)
            {
                if (soldado.activeInHierarchy)
                {
                    tropasEmbarcadas.Add(soldado);
                    soldado.SetActive(false); 
                    count++;
                }
            }
        }
        if(count > 0) Debug.Log("Embarcados: " + count);
    }

    void VerificarSelecao()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            // Verifica se o objeto clicado é este helicóptero (ou parte dele)
            // Usa 'transform.root' para garantir que pegamos o pai se clicarmos no modelo visual
            if (hit.collider.transform.root == this.transform) selecionado = true;
            else selecionado = false;
        }
    }

    void CriarVisualSelecao()
    {
        anelSelecao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anelSelecao.name = "AnelSelecao"; 
        Destroy(anelSelecao.GetComponent<Collider>());
        anelSelecao.transform.SetParent(this.transform);
        anelSelecao.transform.localPosition = new Vector3(0, -1.1f, 0); // Ajuste visual conforme necessidade
        anelSelecao.transform.localScale = new Vector3(5f, 0.05f, 5f);
        
        Renderer rend = anelSelecao.GetComponent<Renderer>();
        if(rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = new Color(0, 1, 0, 0.045f);
        }
        anelSelecao.SetActive(false);
    }
}
