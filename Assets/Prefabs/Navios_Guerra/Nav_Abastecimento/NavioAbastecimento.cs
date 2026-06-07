using UnityEngine;
using System.Collections.Generic;

public class NavioAbastecimento : MonoBehaviour
{
    [Header("Configurações de Combustível")]
    [Tooltip("Capacidade total de combustível do navio de abastecimento.")]
    public float combustivelTotal = 10000f;
    [Tooltip("Quantidade de combustível transferida por segundo.")]
    public float taxaAbastecimento = 50f;
    [Tooltip("Quanto de combustível o alvo vai receber nesta operação.")]
    public float metaTransferenciaPorNavio = 500f;

    [Header("Configurações do Radar")]
    [Tooltip("Raio de varredura para encontrar navios.")]
    public float raioRadar = 500f;
    [Tooltip("Layer usada para identificar o que é um navio (deixe em Everything/~0 se não tiver certeza).")]
    public LayerMask layerNavios = ~0;

    [Header("Configurações do Cano (Mangueira)")]
    [Tooltip("O GameObject que representa o cano.")]
    public Transform cano;
    [Tooltip("O cano usa o pivot no centro (como o cilindro padrão da Unity) ou na base (false)?")]
    public bool pivotNoCentro = true;
    [Tooltip("O comprimento padrão do prefab do cano (Cilindro padrão da Unity é 2).")]
    public float comprimentoPadraoCano = 2f;
    [Tooltip("Escala de espessura (X e Y) do cano.")]
    public float espessuraCano = 0.4f;
    [Tooltip("O Create (ponto) do lado esquerdo do navio de abastecimento.")]
    public Transform pontoOrigemEsquerda;
    [Tooltip("O Create (ponto) do lado direito do navio de abastecimento.")]
    public Transform pontoOrigemDireita;
    [Tooltip("Opcional: Nome do GameObject filho no navio alvo onde o cano vai engatar.")]
    public string nomePontoEngateAlvo = "CreateEngate";

    [Header("Configurações de Movimento")]
    public float velocidadeAproximacao = 15f;
    public float distanciaIdealEmparelhamento = 30f;
    public float velocidadeRotacao = 3f;

    [Header("Ajustes de Modelo do Navio")]
    [Tooltip("Escala visual do navio de abastecimento (deixe em (1,1,1) para manter a original).")]
    public Vector3 escalaNavio = Vector3.one;
    [Tooltip("Offset vertical (Y) para ajustar a flutuação do navio (ex: -1 para baixar, 1 para subir).")]
    public float offsetAlturaY = 0f;

    [Header("Menu Simples (OnGUI)")]
    public bool mostrarPainelDebug = true;
    
    // Lista para OnGUI
    private struct NavioRadarInfo
    {
        public Transform raiz;
        public string nome;
        public float distancia;
    }
    private List<NavioRadarInfo> listaNaviosRadar = new List<NavioRadarInfo>();
    private ControleUnidade controleUnidade;
    private string mensagemStatusMenu = "";
    private float sumirMensagemTempo = 0f;
    private Vector2 scrollPosition;
    
    // Estado interno
    private bool estaAbastecendo = false;
    private bool estaAproximando = false;
    private Transform alvoAtual;
    private float combustivelTransferidoAlvo = 0f;
    private float tempoInicioAbastecimento;
    private float metaTransferenciaAtual = 500f;
    private CombustivelUnidade combustivelAlvoComp;
    private float alturaOriginalY;
    private Rigidbody rb;
    private bool originalKinematic = false;
    
    // Componentes de navegação a serem desativados durante acoplagem
    private UnityEngine.AI.NavMeshAgent agenteNav;
    private ControleNavioRealista controleNavio;
    private bool originalNavAgentState = false;
    private bool originalControleNavioState = false;

    void Start()
    {
        alturaOriginalY = transform.position.y;
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            originalKinematic = rb.isKinematic;
        }
        
        agenteNav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        controleNavio = GetComponent<ControleNavioRealista>();
        controleUnidade = GetComponentInParent<ControleUnidade>();

        // Ajusta a escala inicial se configurada
        if (escalaNavio != Vector3.one)
        {
            transform.localScale = escalaNavio;
        }

        // Aplica o offset de altura inicial
        if (offsetAlturaY != 0f)
        {
            Vector3 pos = transform.position;
            pos.y += offsetAlturaY;
            transform.position = pos;
            alturaOriginalY = transform.position.y;
        }

        // Ocultar o cano inicialmente
        if (cano != null)
        {
            cano.gameObject.SetActive(false);
        }

        StartCoroutine(RotinaRadar());
    }

    System.Collections.IEnumerator RotinaRadar()
    {
        while (true)
        {
            if (!estaAbastecendo && !estaAproximando)
            {
                AtualizarListaNaviosProximos();
            }
            yield return new WaitForSeconds(2f);
        }
    }

    void AtualizarListaNaviosProximos()
    {
        listaNaviosRadar.Clear();

        // Se layerNavios estiver vazia (0), forçamos buscar em tudo (~0) para evitar que o radar fique cego
        LayerMask mascaraDeBusca = layerNavios.value == 0 ? ~0 : layerNavios;

        Collider[] hits = Physics.OverlapSphere(transform.position, raioRadar, mascaraDeBusca);
        
        // Usar um HashSet para evitar adicionar o mesmo navio múltiplas vezes caso ele tenha múltiplos colididores
        HashSet<Transform> naviosAdicionados = new HashSet<Transform>();

        foreach (var hit in hits)
        {
            if (hit.transform != this.transform && hit.transform.root != this.transform.root)
            {
                // Encontra o componente de combustível correspondente no alvo
                CombustivelUnidade comb = hit.GetComponentInParent<CombustivelUnidade>();
                if (comb == null)
                {
                    comb = hit.GetComponentInChildren<CombustivelUnidade>();
                }

                if (comb != null)
                {
                    // Verifica se o alvo é realmente um navio ou submarino por tag ou classe de combustível
                    // Usa TagSafe.Matches para evitar exceção se a tag não estiver registrada no projeto
                    bool ehNavio = TagSafe.Matches(hit, "Navio") || TagSafe.Matches(hit, "Submarino") ||
                                   TagSafe.Matches(comb, "Navio") || TagSafe.Matches(comb, "Submarino") ||
                                   (hit.transform.parent != null && (TagSafe.Matches(hit.transform.parent, "Navio") || TagSafe.Matches(hit.transform.parent, "Submarino"))) ||
                                   comb.classe == ClasseCombustivelUnidade.Naval;

                    if (!ehNavio)
                    {
                        continue;
                    }

                    Transform navioRaiz = comb.transform;

                    if (naviosAdicionados.Contains(navioRaiz))
                    {
                        continue;
                    }
                    naviosAdicionados.Add(navioRaiz);

                    float distancia = Vector3.Distance(transform.position, navioRaiz.position);
                    listaNaviosRadar.Add(new NavioRadarInfo { raiz = navioRaiz, nome = navioRaiz.name, distancia = distancia });
                }
            }
        }
    }

    void IniciarProcessoAbastecimento(Transform alvo)
    {
        if (estaAbastecendo || estaAproximando) return;

        alvoAtual = alvo;
        estaAproximando = true;

        if (rb != null)
        {
            rb.isKinematic = true; // Evita colisões físicas e impulsos
        }
        
        if (agenteNav != null)
        {
            originalNavAgentState = agenteNav.enabled;
            agenteNav.enabled = false;
        }
        
        if (controleNavio != null)
        {
            originalControleNavioState = controleNavio.enabled;
            controleNavio.enabled = false;
        }
        
        listaNaviosRadar.Clear();
        mensagemStatusMenu = "Aproximando...";
        sumirMensagemTempo = Time.time + 10f;
    }

    void Update()
    {
        if (estaAproximando && alvoAtual != null)
        {
            RotinaAproximacao();
        }
        else if (estaAbastecendo && alvoAtual != null)
        {
            RotinaAbastecendo();
        }
    }

    void RotinaAproximacao()
    {
        // Calcula o lado mais próximo do alvo dinamicamente para evitar colisões ao cruzar caminho
        Vector3 ladoDireito = alvoAtual.position + alvoAtual.right * distanciaIdealEmparelhamento;
        Vector3 ladoEsquerdo = alvoAtual.position - alvoAtual.right * distanciaIdealEmparelhamento;
        Vector3 posicaoEmparelhamento = Vector3.Distance(transform.position, ladoDireito) < Vector3.Distance(transform.position, ladoEsquerdo) ? ladoDireito : ladoEsquerdo;
        
        // Mantém a altura original do reabastecedor
        posicaoEmparelhamento.y = alturaOriginalY;

        float distanciaAoPonto = Vector3.Distance(transform.position, posicaoEmparelhamento);

        if (distanciaAoPonto > 3f)
        {
            transform.position = Vector3.MoveTowards(transform.position, posicaoEmparelhamento, velocidadeAproximacao * Time.deltaTime);
            
            // Encara a direção para onde está navegando quando longe, e alinha com o alvo quando perto
            Quaternion rotacaoDesejada;
            if (distanciaAoPonto > 15f)
            {
                Vector3 direcaoMovimento = (posicaoEmparelhamento - transform.position).normalized;
                direcaoMovimento.y = 0f; // Evita inclinar o navio para cima ou para baixo
                if (direcaoMovimento.sqrMagnitude > 0.001f)
                {
                    rotacaoDesejada = Quaternion.LookRotation(direcaoMovimento);
                }
                else
                {
                    rotacaoDesejada = Quaternion.LookRotation(alvoAtual.forward);
                }
            }
            else
            {
                rotacaoDesejada = Quaternion.LookRotation(alvoAtual.forward);
            }
            
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoDesejada, velocidadeRotacao * Time.deltaTime);
        }
        else
        {
            estaAproximando = false;
            IniciarAbastecimento();
        }
    }

    void IniciarAbastecimento()
    {
        estaAbastecendo = true;
        combustivelTransferidoAlvo = 0f;
        tempoInicioAbastecimento = Time.time;

        // Busca o sistema de combustível do alvo
        combustivelAlvoComp = alvoAtual.GetComponent<CombustivelUnidade>();
        if (combustivelAlvoComp == null)
        {
            combustivelAlvoComp = alvoAtual.GetComponentInChildren<CombustivelUnidade>();
        }

        if (combustivelAlvoComp != null)
        {
            metaTransferenciaAtual = combustivelAlvoComp.capacidade - combustivelAlvoComp.combustivelAtual;
            
            // Se o alvo já estiver cheio, nem inicia o processo
            if (metaTransferenciaAtual <= 0f)
            {
                Debug.Log($"[Abastecimento] O navio alvo {alvoAtual.name} já está completamente abastecido.");
                FinalizarAbastecimento();
                return;
            }
        }
        else
        {
            if (metaTransferenciaPorNavio > 0f)
            {
                metaTransferenciaAtual = metaTransferenciaPorNavio;
            }
            else
            {
                Debug.LogWarning($"[Abastecimento] O navio alvo {alvoAtual.name} não possui o componente CombustivelUnidade e a Meta de Transferência no Inspector é 0.");
                FinalizarAbastecimento();
                return;
            }
        }

        mensagemStatusMenu = "Abastecendo...";
        sumirMensagemTempo = Time.time + 10f;

        float distEsq = Vector3.Distance(pontoOrigemEsquerda.position, alvoAtual.position);
        float distDir = Vector3.Distance(pontoOrigemDireita.position, alvoAtual.position);
        
        Transform origemCano = distEsq < distDir ? pontoOrigemEsquerda : pontoOrigemDireita;

        if (cano != null)
        {
            cano.gameObject.SetActive(true);
            AtualizarPosicaoCano(origemCano);
        }
    }

    void RotinaAbastecendo()
    {
        // Mantém o emparelhamento no lado mais próximo
        Vector3 ladoDireito = alvoAtual.position + alvoAtual.right * distanciaIdealEmparelhamento;
        Vector3 ladoEsquerdo = alvoAtual.position - alvoAtual.right * distanciaIdealEmparelhamento;
        Vector3 posicaoEmparelhamento = Vector3.Distance(transform.position, ladoDireito) < Vector3.Distance(transform.position, ladoEsquerdo) ? ladoDireito : ladoEsquerdo;
        
        posicaoEmparelhamento.y = alturaOriginalY;

        transform.position = Vector3.Lerp(transform.position, posicaoEmparelhamento, Time.deltaTime * velocidadeAproximacao);
        transform.rotation = Quaternion.Slerp(transform.rotation, alvoAtual.rotation, Time.deltaTime * velocidadeRotacao);

        float distEsq = Vector3.Distance(pontoOrigemEsquerda.position, alvoAtual.position);
        float distDir = Vector3.Distance(pontoOrigemDireita.position, alvoAtual.position);
        Transform origemCano = distEsq < distDir ? pontoOrigemEsquerda : pontoOrigemDireita;
        AtualizarPosicaoCano(origemCano);

        float combustivelA_Transferir = taxaAbastecimento * Time.deltaTime;
        
        if (combustivelTotal > 0 && combustivelTransferidoAlvo < metaTransferenciaAtual)
        {
            float transferenciaEfetiva = Mathf.Min(combustivelA_Transferir, combustivelTotal, metaTransferenciaAtual - combustivelTransferidoAlvo);
            
            combustivelTotal -= transferenciaEfetiva;
            combustivelTransferidoAlvo += transferenciaEfetiva;

            // Abastece efetivamente o componente do navio alvo
            if (combustivelAlvoComp != null)
            {
                combustivelAlvoComp.Abastecer(transferenciaEfetiva);
            }

            if (combustivelTransferidoAlvo >= metaTransferenciaAtual || combustivelTotal <= 0)
            {
                FinalizarAbastecimento();
            }
        }
        else
        {
            FinalizarAbastecimento();
        }
    }

    void AtualizarPosicaoCano(Transform origem)
    {
        if (cano == null || alvoAtual == null) return;

        Transform pontoDestino = alvoAtual.Find(nomePontoEngateAlvo);
        Vector3 posicaoDestino = pontoDestino != null ? pontoDestino.position : alvoAtual.position;

        float distancia = Vector3.Distance(origem.position, posicaoDestino);

        if (pivotNoCentro)
        {
            // Se o cilindro tem pivot no meio, posiciona no ponto central entre os dois navios
            cano.position = (origem.position + posicaoDestino) / 2f;
        }
        else
        {
            cano.position = origem.position;
        }

        cano.LookAt(posicaoDestino);
        
        // Define o comprimento proporcional e a espessura configurada
        Vector3 escala = cano.localScale;
        escala.x = espessuraCano;
        escala.y = espessuraCano;
        escala.z = distancia / comprimentoPadraoCano; 
        cano.localScale = escala;
    }

    void FinalizarAbastecimento()
    {
        estaAbastecendo = false;
        alvoAtual = null;
        combustivelAlvoComp = null;

        if (rb != null)
        {
            rb.isKinematic = originalKinematic; // Restaura a física normal do navio
        }

        if (agenteNav != null)
        {
            agenteNav.enabled = originalNavAgentState;
        }
        
        if (controleNavio != null)
        {
            controleNavio.enabled = originalControleNavioState;
        }

        if (cano != null)
        {
            cano.gameObject.SetActive(false);
        }

        mensagemStatusMenu = "ABASTECIMENTO COMPLETO";
        sumirMensagemTempo = Time.time + 3f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, raioRadar);
    }
    
    void OnGUI()
    {
        if (!mostrarPainelDebug) return;
        if (controleUnidade != null && !controleUnidade.selecionado) return;

        // Define a área do menu no canto inferior esquerdo
        GUILayout.BeginArea(new Rect(20, Screen.height - 280, 240, 260), GUI.skin.box);
        
        GUIStyle estiloTitulo = new GUIStyle(GUI.skin.label);
        estiloTitulo.fontStyle = FontStyle.Bold;
        estiloTitulo.alignment = TextAnchor.MiddleCenter;
        
        GUILayout.Label("NAVIO DE ABASTECIMENTO", estiloTitulo);
        GUILayout.Label($"Estoque: {combustivelTotal:F0} Litros");
        GUILayout.Space(5);
        
        if (estaAproximando || estaAbastecendo)
        {
            if (Time.time < sumirMensagemTempo)
            {
                GUILayout.Label($"Status: {mensagemStatusMenu}", estiloTitulo);
            }
            if (estaAbastecendo)
            {
                GUILayout.Label($"Transferido: {combustivelTransferidoAlvo:F0} / {metaTransferenciaAtual:F0} L");
                float tempoDecorrido = Time.time - tempoInicioAbastecimento;
                GUILayout.Label($"Tempo decorrido: {tempoDecorrido:F1}s");
                
                // Barra de progresso visual
                float progress = metaTransferenciaAtual > 0 ? (combustivelTransferidoAlvo / metaTransferenciaAtual) : 0;
                Rect r = GUILayoutUtility.GetRect(200, 15);
                GUI.Box(r, "");
                Rect fill = new Rect(r.x, r.y, r.width * progress, r.height);
                Texture2D tex = Texture2D.whiteTexture;
                Color old = GUI.color;
                GUI.color = Color.green;
                GUI.DrawTexture(fill, tex);
                GUI.color = old;
                GUILayout.Space(5);
            }
            
            if (GUILayout.Button("Cancelar Operação", GUILayout.Height(30)))
            {
                FinalizarAbastecimento();
            }
        }
        else
        {
            GUILayout.Label("Radar de Navios Próximos:", estiloTitulo);
            if (listaNaviosRadar.Count == 0)
            {
                GUILayout.Label("Nenhum navio detectado.", GUILayout.Height(30));
            }
            else
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                for (int i = 0; i < listaNaviosRadar.Count; i++)
                {
                    var info = listaNaviosRadar[i];
                    if (info.raiz == null) continue; 
                    
                    if (GUILayout.Button($"{info.nome}\n{info.distancia:F0}m de distância", GUILayout.Height(40)))
                    {
                        IniciarProcessoAbastecimento(info.raiz);
                    }
                }
                GUILayout.EndScrollView();
            }
        }
        GUILayout.EndArea();
    }
}
