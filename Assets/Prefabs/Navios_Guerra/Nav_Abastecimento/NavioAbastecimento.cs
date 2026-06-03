using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

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
    [Tooltip("Layer usada para identificar o que é um navio.")]
    public LayerMask layerNavios;

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

    [Header("Interface (UI Toolkit)")]
    public UIDocument uiDocument;
    
    // UI Elements
    private VisualElement painelPrincipal;
    private ScrollView listaNavios;
    private ProgressBar barraProgresso;
    private Label labelTempo;
    private Label labelCombustivelRestante;
    private Button btnFecharPainel;
    
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

    void Start()
    {
        alturaOriginalY = transform.position.y;
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            originalKinematic = rb.isKinematic;
        }

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

        ConfigurarUI();
        StartCoroutine(RotinaRadar());
    }

    void ConfigurarUI()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogWarning("UIDocument não atribuído no NavioAbastecimento e nenhum encontrado no mesmo GameObject.");
            return;
        }

        var root = uiDocument.rootVisualElement;
        
        // Elementos correspondentes ao arquivo .uxml
        painelPrincipal = root.Q<VisualElement>("supplyPanel");
        listaNavios = root.Q<ScrollView>("ListaNavios");
        barraProgresso = root.Q<ProgressBar>("BarraProgresso");
        labelTempo = root.Q<Label>("LabelTempo");
        labelCombustivelRestante = root.Q<Label>("LabelCombustivel");
        btnFecharPainel = root.Q<Button>("btnClosePanel");

        if (labelCombustivelRestante != null)
        {
            labelCombustivelRestante.text = $"{combustivelTotal:F0} L";
        }
        
        if (barraProgresso != null) barraProgresso.style.display = DisplayStyle.None;
        if (labelTempo != null) labelTempo.style.display = DisplayStyle.None;

        if (btnFecharPainel != null)
        {
            btnFecharPainel.clicked += () => {
                if (painelPrincipal != null)
                {
                    painelPrincipal.style.display = DisplayStyle.None;
                }
            };
        }
    }

    IEnumerator RotinaRadar()
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
        if (listaNavios == null) return;

        listaNavios.Clear();

        Collider[] hits = Physics.OverlapSphere(transform.position, raioRadar, layerNavios);
        bool algumNavioEncontrado = false;

        foreach (var hit in hits)
        {
            if (hit.transform != this.transform && hit.transform.root != this.transform.root)
            {
                algumNavioEncontrado = true;
                float distancia = Vector3.Distance(transform.position, hit.transform.position);
                
                // Criação do item de botão no padrão da UI do jogo
                Button btnNavio = new Button(() => IniciarProcessoAbastecimento(hit.transform));
                btnNavio.text = $"{hit.name} ({distancia:F0}m)";
                btnNavio.AddToClassList("btn");
                btnNavio.AddToClassList("btn-action");
                btnNavio.style.marginBottom = 6;
                btnNavio.style.height = 28;
                btnNavio.style.fontSize = 9;

                listaNavios.Add(btnNavio);
            }
        }

        if (!algumNavioEncontrado)
        {
            Label lblNenhum = new Label("Nenhum navio próximo");
            lblNenhum.style.color = new Color(0.3f, 0.4f, 0.5f);
            lblNenhum.style.fontSize = 9;
            lblNenhum.style.unityTextAlign = TextAnchor.MiddleCenter;
            lblNenhum.style.marginTop = 10;
            listaNavios.Add(lblNenhum);
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
        
        if (listaNavios != null) listaNavios.Clear();
        
        if (labelTempo != null)
        {
            labelTempo.style.display = DisplayStyle.Flex;
            labelTempo.text = "Aproximando...";
        }
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
            Quaternion rotacaoAlvo = Quaternion.LookRotation(alvoAtual.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, velocidadeRotacao * Time.deltaTime);
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

        if (barraProgresso != null)
        {
            barraProgresso.style.display = DisplayStyle.Flex;
            barraProgresso.value = 0f;
            barraProgresso.highValue = metaTransferenciaAtual;
        }

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

            AtualizarUIAbastecimento();

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

    void AtualizarUIAbastecimento()
    {
        if (labelCombustivelRestante != null)
        {
            labelCombustivelRestante.text = $"Total: {combustivelTotal:F0} L";
        }

        if (barraProgresso != null)
        {
            barraProgresso.value = combustivelTransferidoAlvo;
            barraProgresso.title = $"Transferido: {combustivelTransferidoAlvo:F0} / {metaTransferenciaAtual:F0} L";
        }

        if (labelTempo != null)
        {
            float tempoDecorrido = Time.time - tempoInicioAbastecimento;
            labelTempo.text = $"Tempo de Operação: {tempoDecorrido:F1}s";
        }
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

        if (cano != null)
        {
            cano.gameObject.SetActive(false);
        }

        if (barraProgresso != null)
        {
            barraProgresso.style.display = DisplayStyle.None;
        }

        if (labelTempo != null)
        {
            labelTempo.text = "ABASTECIMENTO COMPLETO";
            Invoke("EsconderLabelTempo", 3f);
        }
    }

    void EsconderLabelTempo()
    {
        if (labelTempo != null)
        {
            labelTempo.style.display = DisplayStyle.None;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, raioRadar);
    }
}
