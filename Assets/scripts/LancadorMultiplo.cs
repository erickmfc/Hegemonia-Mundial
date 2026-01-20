using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class LancadorMultiplo : MonoBehaviour
{
    [Header("Configurações do Lançador")]
    [Tooltip("Arraste o Prefab do Míssil (Ex: Comar) aqui.")]
    public GameObject misselPrefab;

    [Tooltip("Componente ou objeto filho que vai girar 90 graus (A tampa/caixa de mísseis).")]
    public Transform compartimento;

    [Tooltip("Lista dos 12 pontos de saída dos mísseis.")]
    public Transform[] saidasDeMissel;

    [Header("Animação")]
    [Tooltip("Ângulo que o compartimento deve levantar (Eixo X local).")]
    public float anguloAberto = -90f; // Ajuste para +90 ou -90 conforme o eixo do modelo
    public float velocidadeAbrir = 45f; // Graus por segundo

    [Header("Tempos")]
    [Tooltip("Tempo que espera com a tampa aberta ANTES de começar a atirar.")]
    public float tempoPreparacao = 1.0f;
    [Tooltip("Tempo entre um míssil e outro na rajada.")]
    public float intervaloDisparo = 0.2f;
    
    [Header("Automático / Manual")]
    [Tooltip("Se TRUE, atira sozinho em inimigos quando parado.")]
    public bool modoAutomatico = true; // "Ativo" = Vermelho
    public bool lancamentoManual = false; // Flag ativada pelo botão ou tecla 'L'

    [Header("Radar")]
    public float alcanceRadar = 150f;
    public string[] tagsAlvo = { "Inimigo", "Destrutivel" }; // Pode atacar chão ou ar

    // Internas
    private bool estahAberto = false;
    private bool emDisparo = false;
    private NavMeshAgent agente;
    private ControleUnidade controle;
    private Transform alvoAtual;
    private IdentidadeUnidade meuID;
    private float anguloOriginalX; // Para saber onde voltar

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        controle = GetComponent<ControleUnidade>();
        meuID = GetComponent<IdentidadeUnidade>();

        if (compartimento != null)
        {
            anguloOriginalX = compartimento.localEulerAngles.x;
            // Normaliza para -180 a 180 para facilitar contas
            if (anguloOriginalX > 180) anguloOriginalX -= 360;
        }

        // Tenta achar as saídas se não estiverem preenchidas
        if (saidasDeMissel == null || saidasDeMissel.Length == 0)
        {
            // Busca filhos com nome "Saida"
            // (Isso é um helper caso o user esqueça de arrastar)
        }
    }

    void Update()
    {
        // 1. Verifica se está parado
        bool parado = VerdadeiramenteParado();

        // 2. Input Manual ('L') - REMOVIDO para evitar conflito com outras teclas.
        // O controle agora deve ser feito via botões na UI (ComandoMenu).
        /*
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (controle != null && controle.selecionado)
            {
                lancamentoManual = !lancamentoManual;
                if (parado) StartCoroutine(CicloDeDisparo(null));
                else Debug.Log("Pare o veículo para disparar!");
            }
        }
        */

        // 3. Lógica Automática
        if (parado && !emDisparo)
        {
            if (modoAutomatico)
            {
                // Busca alvo
                alvoAtual = BuscarAlvo();
                if (alvoAtual != null)
                {
                    // Se não estiver aberto, começa a abrir e atira
                    // Se já estiver aberto, só atira
                    if (!estahAberto) 
                    {
                        StartCoroutine(CicloDeDisparo(alvoAtual));
                    }
                    else
                    {
                        // Já está aberto por algum motivo, atira
                         StartCoroutine(CicloDeDisparo(alvoAtual));
                    }
                }
                else
                {
                    // Sem alvo. Se está aberto, deve fechar?
                    // Pode manter aberto por um tempo (histerese) ou fechar logo
                    if (estahAberto) StartCoroutine(FecharCompartimento());
                }
            }
        }
        else if (!parado && estahAberto && !emDisparo)
        {
            // Se começou a andar, fecha
            StartCoroutine(FecharCompartimento());
        }
    }

    bool VerdadeiramenteParado()
    {
        if (agente != null && agente.enabled)
        {
            return agente.velocity.magnitude < 0.1f && !agente.pathPending;
        }
        return true; // Se não tem NavMesh, assume parado ou controlado por física
    }

    // --- LÓGICA DE COMBATE ---
    
    Transform BuscarAlvo()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, alcanceRadar);
        float menorDist = Mathf.Infinity;
        Transform melhorAlvo = null;

        foreach (var hit in hits)
        {
            // Pula a mim mesmo
            if (hit.transform.root == transform.root) continue;

            bool ehInimigo = false;
            
            // Checa Tags
            foreach(string t in tagsAlvo) 
            {
                if (hit.CompareTag(t)) ehInimigo = true;
            }

            // Checa Identidade (IFF)
            if (meuID != null)
            {
                IdentidadeUnidade idAlvo = hit.GetComponent<IdentidadeUnidade>();
                if (idAlvo != null)
                {
                    if (idAlvo.teamID == meuID.teamID) ehInimigo = false; // Aliado
                    else ehInimigo = true; // Inimigo confirmado
                }
            }

            if (ehInimigo)
            {
                float d = Vector3.Distance(transform.position, hit.transform.position);
                if (d < menorDist)
                {
                    menorDist = d;
                    melhorAlvo = hit.transform;
                }
            }
        }
        return melhorAlvo;
    }

    IEnumerator CicloDeDisparo(Transform alvo)
    {
        if (emDisparo) yield break; // Já está fazendo algo
        emDisparo = true;

        // 1. ABRIR (Se necessário)
        if (!estahAberto)
        {
            yield return StartCoroutine(MoverCompartimento(anguloAberto));
            estahAberto = true;
        }

        // 2. TEMPO DE PREPARAÇÃO (User pediu: "demora um tempo para levantar e ai sim dispara")
        yield return new WaitForSeconds(tempoPreparacao);

        // 3. DISPARAR (Sequência de 12 ou até acabar alvos)
        for (int i = 0; i < saidasDeMissel.Length; i++)
        {
            if (saidasDeMissel[i] != null)
            {
                LancarMissel(saidasDeMissel[i], alvo);
                yield return new WaitForSeconds(intervaloDisparo);
            }
        }

        // 4. FECHAR (Opcional: Pode fechar logo depois ou esperar sair do modo combate)
        // Por enquanto, vamos manter aberto enquando tiver inimigos (Lógica do Update decide fechar se parar de achar alvos)
        // Mas para garantir ciclo completo se o user quiser "One Shot", podemos fechar.
        // O user disse: "compartimento abre... levata... dispara".
        
        emDisparo = false;
    }

    IEnumerator FecharCompartimento()
    {
        if (compartimento == null) yield break;
        yield return StartCoroutine(MoverCompartimento(anguloOriginalX));
        estahAberto = false;
    }

    IEnumerator MoverCompartimento(float anguloDestino)
    {
        if (compartimento == null) yield break;

        float anguloAtual = compartimento.localEulerAngles.x;
        // Ajuste para lidar com 0..360 do Unity
        if (anguloAtual > 180) anguloAtual -= 360;

        while (Mathf.Abs(anguloAtual - anguloDestino) > 1f)
        {
            anguloAtual = Mathf.MoveTowards(anguloAtual, anguloDestino, velocidadeAbrir * Time.deltaTime);
            compartimento.localEulerAngles = new Vector3(anguloAtual, 0, 0);
            yield return null;
        }
        
        // Garante valor final exato
        compartimento.localEulerAngles = new Vector3(anguloDestino, 0, 0);
    }

    void LancarMissel(Transform pontoSaida, Transform alvo)
    {
        if (misselPrefab == null) return;

        GameObject missel = Instantiate(misselPrefab, pontoSaida.position, pontoSaida.rotation);
        
        // Tenta achar script de míssil (ICBM, Tático ou genérico)
        MisselICBM scriptM = missel.GetComponent<MisselICBM>();
        MisselTatico scriptT = missel.GetComponent<MisselTatico>();
        
        // CONFIGURAÇÃO DO ALVO NO MÍSSEL
        if (scriptM != null)
        {
            // MisselICBM pede um Vector3 target.
            Vector3 destino = (alvo != null) ? alvo.position : (transform.position + transform.forward * 100f);
            scriptM.IniciarLancamento(destino);
        }
        else if (scriptT != null)
        {
            // MisselTatico (Comar)
            Vector3 destino = (alvo != null) ? alvo.position : (transform.position + transform.forward * 100f);
            scriptT.IniciarLancamento(destino);
        }
        else
        {
            // Tenta achar movimentação genérica do ControleTorreta
            var projetil = missel.GetComponent<Projetil>();
            if (projetil != null)
            {
                 projetil.SetDono(this.gameObject);
                 if (alvo != null) projetil.SetDirecao((alvo.position - pontoSaida.position).normalized);
            }
        }
    }

    // --- VISUAL (Gizmos & Feedback) ---
    void UpdateVisualFeedback()
    {
        if (controle != null && controle.anelSelecao != null)
        {
            Renderer r = controle.anelSelecao.GetComponent<Renderer>();
            if (r != null)
            {
                // Vermelho = Ativo (Automático), Verde = Passivo (Manual/Seguro)
                Color alvoAlvo = modoAutomatico ? Color.red : Color.green;
                r.material.color = Color.Lerp(r.material.color, alvoAlvo, Time.deltaTime * 5f);
            }
        }
    }

    void LateUpdate()
    {
        UpdateVisualFeedback();
    }

    void OnDrawGizmos()
    {
        // "se ficar vermelho ta ativo"
        if (modoAutomatico) 
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, alcanceRadar);
        }
        else
        {
            Gizmos.color = Color.green; // Passivo
             Gizmos.DrawWireSphere(transform.position, alcanceRadar * 0.5f);
        }
    }

    // Método para UI chamar
    public void AlternarModo()
    {
        modoAutomatico = !modoAutomatico;
    }

    // Método para disparo manual via Comando
    public void DispararManual()
    {
        // Só dispara se estiver parado (regra original) ou podemos forçar
        // O user disse "o compartimento abre... quando ele tiver parado".
        // Vamos respeitar a regra de estar parado.
        if (VerdadeiramenteParado())
        {
            StartCoroutine(CicloDeDisparo(null)); // Null = Dispara para frente
        }
        else
        {
            Debug.Log("Veículo precisa estar parado para lançar!");
        }
    }
}
