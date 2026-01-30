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
    // public NavMeshAgent agent; // REMOVIDO PARA USAR SISTEMA MANUAL
    public Vector3 eixoRotacaoTraseira = Vector3.forward;
    
    // Variáveis de controle
    private bool estaNoChao = true;
    private bool selecionado = false;
    private bool preparandoDesembarque = false;
    private GameObject anelSelecao; 

    // Variáveis de Voo Manual
    private Vector3 destinoVoo;
    private bool temDestino = false;

    // Referência opcional para sistema de armas
    private ControleTorreta sistemaArmas;

    void Start()
    {
        // Busca sistema de armas (ControleTorreta)
        sistemaArmas = GetComponent<ControleTorreta>();

        // CORREÇÃO CRÍTICA: Garante altura mínima para não nascer enterrado
        if(alturaDoSolo < 2.5f) 
        {
            alturaDoSolo = 3.0f; 
        }

        // NavMesh removido por solicitação. Voo 100% manual agora.
        CriarVisualSelecao();
    }
    
    // ... [MANTÉM UPDATE INALTERADO ATÉ LINHA 112] ...

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
        // if(agent) agent.enabled = false; // Removido

    }
    
    public void Pousar() 
    { 
        estaNoChao = true; 
        temDestino = false; 
    }

    float GetAlturaDoSoloAtual()
    {
        // Começa BEM ALTO (1000m) para achar o chão mesmo se estivermos voando
        Vector3 origem = new Vector3(transform.position.x, 1000f, transform.position.z);
        
        // Filtra camadas para ignorar o próprio heli, UI e Ignorados
        int layerMask = ~LayerMask.GetMask("Unidades", "Ignore Raycast", "UI");

        // Raio infinito (quase)
        RaycastHit[] hits = Physics.RaycastAll(origem, Vector3.down, 2000f, layerMask);
        
        float maiorY = -1000f; // Começa bem baixo
        bool achouChao = false;

        foreach (RaycastHit hit in hits)
        {
            // Ignora a si mesmo e filhos
            if (hit.collider.transform.root == transform.root) continue;
            // Ignora triggers
            if (hit.collider.isTrigger) continue;

            // É Chão sólido?
            if (hit.point.y > maiorY)
            {
                maiorY = hit.point.y;
                achouChao = true;
            }
        }
        
        // Debug para ver onde detectou o chão
        Debug.DrawLine(origem, new Vector3(transform.position.x, maiorY, transform.position.z), Color.yellow);

        // Se não achou chão nenhum (abismo?), retorna a altura atual corrigida
        if (!achouChao) return transform.position.y - alturaDoSolo; 

        return maiorY;
    }

    void ProcessarVoo()
    {
        float soloY = GetAlturaDoSoloAtual();
        float alturaAlvoRelativa = estaNoChao ? alturaDoSolo : altitudeDeVoo;
        
        // SEGURANÇA: Nunca pousa abaixo da altura do solo (evita enterrar)
        // Se alturaDoSolo configurada for 0, forçamos 3m para garantir
        float alturaMinimaSegura = Mathf.Max(alturaDoSolo, 2.5f);
        if (estaNoChao) alturaAlvoRelativa = alturaMinimaSegura;

        float yFinal = soloY + alturaAlvoRelativa;

        Vector3 posAtual = transform.position;
        Vector3 novaPos = posAtual;

        // ... [RESTO DA LÓGICA DE MOVIMENTO] ...
        // Só movemos horizontalmente se já estivermos decolando ou voando (ou seja, subindo para voar)
        // Pequena melhoria: só move horizontalmente se já tiver altura razoável? Não, helicóptero pode "taxiar" ou voar baixo.
        
        float velocidadeAtual = 0f;

        if (!estaNoChao && temDestino)
        {
            Vector3 vetorDirecao = (destinoVoo - posAtual);
            vetorDirecao.y = 0; // Ignora altura para calcular direção e distância

            if (vetorDirecao.magnitude > 1.0f) // Tolerância de chegada
            {
                // 1. PRIMEIRO: Rotação suave para o destino (ANTES de mover)
                if (vetorDirecao != Vector3.zero)
                {
                    Quaternion rotAlvo = Quaternion.LookRotation(vetorDirecao);
                    // Aumenta a velocidade de rotação para 3x (7.5 em vez de 2.5)
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, Time.deltaTime * (rotacaoSuavidade * 3f));
                }

                // 2. DEPOIS: Move na direção que está apontando (não na direção do alvo)
                // Isso garante que ele voe para frente e não de lado
                // Mas só move se estiver razoavelmente apontado para o alvo (evita voo de lado)
                float anguloErro = Vector3.Angle(transform.forward, vetorDirecao.normalized);
                
                if (anguloErro < 45f) // Só move se estiver apontando +/- na direção certa (dentro de 45°)
                {
                    Vector3 moveDir = transform.forward * velocidadeNavegacao * Time.deltaTime;
                    novaPos += moveDir;
                    velocidadeAtual = velocidadeNavegacao;
                }
                else
                {
                    // Se estiver muito desalinhado, só rotaciona sem mover (faz a curva parado)
                    velocidadeAtual = 0f;
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
        // Se clicar na UI, ignora
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // Filtra camadas para não pegar triggers invisíveis
        int layerMask = ~LayerMask.GetMask("Ignore Raycast", "UI");

        if (Physics.Raycast(ray, out hit, 5000f, layerMask))
        {
            // Tenta encontrar o controlador no objeto clicado ou nos pais dele
            HelicopterController clicado = hit.collider.GetComponentInParent<HelicopterController>();
            
            // Se encontrou ALGUÉM e esse alguém sou EU, então fui selecionado
            if (clicado != null && clicado == this)
            {
                selecionado = true;
                // Debug.Log($"[Falcon] Selecionado: {name}");
            }
            else
            {
                // Se clicou no chão ou em outra unidade, perde a seleção
                selecionado = false;
            }
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
            // Usa shader Unlit/Color que funciona melhor para UI 3D sem iluminação
            rend.material = new Material(Shader.Find("Unlit/Color"));
            rend.material.color = new Color(0, 1, 0, 0.15f); // Verde MUITO transparente (era 0.4, agora 0.15)
        }
        anelSelecao.SetActive(false);
    }
}
