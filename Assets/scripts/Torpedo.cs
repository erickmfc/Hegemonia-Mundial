using UnityEngine;
using System.Collections;

/// <summary>
/// Torpedo submarino que navega debaixo d'água e sobe para explodir próximo ao casco do alvo.
/// </summary>
public class Torpedo : MonoBehaviour
{
    [Header("Movimento")]
    [Tooltip("Velocidade de deslocamento do torpedo.")]
    public float velocidade = 25f;
    
    [Tooltip("Profundidade de navegação (negativo = abaixo da água).")]
    public float profundidadeNavegacao = -8f;
    
    [Tooltip("Distância para começar a subida final em direção ao alvo.")]
    public float distanciaSubida = 15f;
    
    [Tooltip("Velocidade de subida ao aproximar do alvo.")]
    public float velocidadeSubida = 15f;

    [Header("Dano")]
    [Tooltip("Dano causado na explosão.")]
    public float danoExplosao = 800f;
    
    [Tooltip("Raio de explosão.")]
    public float raioExplosao = 8f;
    
    [Tooltip("Força de repulsão da explosão.")]
    public float forcaRepulsao = 500f;

    [Header("Detecção")]
    [Tooltip("Tag do alvo (inimigo).")]
    public string tagAlvo = "Navio";
    
    [Tooltip("LayerMask para detecção de colisão.")]
    public LayerMask layersDetectaveis;

    [Header("Efeitos")]
    public GameObject prefabExplosao;
    public ParticleSystem rastroBolas;
    public AudioClip somLancamento;
    public AudioClip somExplosao;
    [Tooltip("Cria um corpo/rastro simples caso o prefab nao tenha Renderer ou ParticleSystem configurado.")]
    public bool criarVisualFallback = true;
    public Color corVisualFallback = new Color(0.1f, 0.9f, 1f, 1f);

    [Header("Rastreamento")]
    [Tooltip("Se true, o torpedo segue o alvo. Se false, vai em linha reta.")]
    public bool rastrearAlvo = true;
    
    [Tooltip("Taxa de curva ao rastrear alvo (0-1).")]
    public float taxaCurva = 0.15f;

    [Header("Origem")]
    [Tooltip("Referência ao submarino/navio que lançou (para identificação).")]
    public Transform lancador;
    
    [Tooltip("Identidade do time do lançador.")]
    public int timeLancador = -1;

    // Estado interno
    private Transform alvoAtual;
    private Vector3 posicaoAlvoPerdido;
    private bool emSubida = false;
    private bool explodiu = false;
    private Vector3 direcaoAtual;
    private float distanciaPercorrida = 0f;
    private float profundidadeAtual;
    
    // Rastros de bolhas
    private float tempoProximoRastro = 0f;
    private float intervaloRastro = 0.3f;

    private float tempoUltimaBusca = 0f;
    private readonly Collider[] bufferDeteccao = new Collider[96];
    private readonly Collider[] bufferExplosao = new Collider[96];
    private readonly Collider[] bufferOnda = new Collider[160];

    private void OnEnable()
    {
        alvoAtual = null;
        emSubida = false;
        explodiu = false;
        direcaoAtual = transform.forward;
        distanciaPercorrida = 0f;
        profundidadeAtual = transform.position.y;
        tempoProximoRastro = 0f;
        tempoUltimaBusca = 0f;
    }

    void Start()
    {
        GarantirVisualVisivel();

        profundidadeAtual = transform.position.y;
        direcaoAtual = transform.forward;
        
        // Som de lançamento
        if (somLancamento != null)
            AudioSource.PlayClipAtPoint(somLancamento, transform.position, 0.8f);
        
        // Configurar layer de colisão para detectar todos os navios/submarinos em qualquer layer
        layersDetectaveis = ~0;
    }

    private void GarantirVisualVisivel()
    {
        if (!criarVisualFallback)
        {
            return;
        }

        Renderer rendererExistente = GetComponentInChildren<Renderer>();
        if (rendererExistente == null)
        {
            GameObject corpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            corpo.name = "Visual_Torpedo_Fallback";
            corpo.transform.SetParent(transform, false);
            corpo.transform.localPosition = Vector3.zero;
            corpo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            corpo.transform.localScale = new Vector3(0.55f, 1.8f, 0.55f);

            Collider col = corpo.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            Renderer rendererCorpo = corpo.GetComponent<Renderer>();
            if (rendererCorpo != null)
            {
                rendererCorpo.material = new Material(Shader.Find("Standard"));
                rendererCorpo.material.color = corVisualFallback;
                rendererCorpo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        TrailRenderer trilha = GetComponentInChildren<TrailRenderer>();
        if (trilha == null)
        {
            GameObject rastro = new GameObject("Rastro_Torpedo_Fallback");
            rastro.transform.SetParent(transform, false);
            rastro.transform.localPosition = Vector3.back * 0.8f;
            trilha = rastro.AddComponent<TrailRenderer>();
            trilha.time = 2.4f;
            trilha.startWidth = 0.35f;
            trilha.endWidth = 0.02f;
            trilha.material = new Material(Shader.Find("Sprites/Default"));
            trilha.startColor = new Color(corVisualFallback.r, corVisualFallback.g, corVisualFallback.b, 0.9f);
            trilha.endColor = new Color(1f, 1f, 1f, 0f);
            trilha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void Update()
    {
        if (explodiu) return;

        // Atualizar profundidade atual
        profundidadeAtual = transform.position.y;

        // Busca e aquisição de alvos / checagem de proximidade em tempo real
        tempoUltimaBusca += Time.deltaTime;
        if (tempoUltimaBusca >= 0.2f)
        {
            tempoUltimaBusca = 0f;
            
            // 1. Aquisição automática de alvos se não tiver nenhum ou se o alvo foi perdido
            if (rastrearAlvo && (alvoAtual == null || !alvoAtual.gameObject.activeInHierarchy))
            {
                AdquirirAlvoProximo();
            }

            // 2. Detecção de proximidade avançada (Backup para colliders que não se tocam verticalmente)
            ChecarProximidadeAvancada();
        }

        // Rastrear alvo se houver
        if (rastrearAlvo && alvoAtual != null && alvoAtual.gameObject.activeInHierarchy)
        {
            posicaoAlvoPerdido = alvoAtual.position;
            
            // Calcular direção horizontal até o alvo
            Vector3 posicaoHorizontal = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 posicaoAlvoHorizontal = new Vector3(alvoAtual.position.x, 0, alvoAtual.position.z);
            Vector3 direcaoParaAlvo = (posicaoAlvoHorizontal - posicaoHorizontal).normalized;
            
            // Interpolar direção para suavizar curva
            direcaoAtual = Vector3.Lerp(direcaoAtual, direcaoParaAlvo, taxaCurva).normalized;
            
            // Verificar distância para subida
            float distanciaHorizontal = Vector3.Distance(posicaoHorizontal, posicaoAlvoHorizontal);
            
            if (!emSubida && distanciaHorizontal <= distanciaSubida)
            {
                emSubida = true;
                // Efeito visual de subida
                if (rastroBolas != null)
                {
                    var em = rastroBolas.emission;
                    em.rateOverTime = em.rateOverTime.constant * 2f;
                }
            }
        }
        else if (alvoAtual != null && !alvoAtual.gameObject.activeInHierarchy)
        {
            // Alvo foi destruído, continuar na última posição conhecida
            alvoAtual = null;
        }

        // Movimento
        Vector3 movimento = direcaoAtual * velocidade * Time.deltaTime;
        
        if (emSubida && alvoAtual != null)
        {
            // Subir em direção ao alvo
            Vector3 direcaoSubida = (alvoAtual.position - transform.position).normalized;
            movimento = direcaoSubida * velocidadeSubida * Time.deltaTime;
        }
        else if (!emSubida)
        {
            // Manter profundidade de navegação
            float erroProfundidade = profundidadeNavegacao - profundidadeAtual;
            
            // Se está voando (lançado de um navio), aplica gravidade e aponta pra água
            if (profundidadeAtual > 2f && erroProfundidade < 0)
            {
                movimento.y = -35f * Time.deltaTime;
                direcaoAtual = Vector3.Lerp(direcaoAtual, (new Vector3(direcaoAtual.x, -1f, direcaoAtual.z)).normalized, Time.deltaTime * 4f);
            }
            else
            {
                movimento.y = erroProfundidade * 0.5f * Time.deltaTime;
            }
        }
        
        transform.position += movimento;
        transform.rotation = Quaternion.LookRotation(direcaoAtual);
        
        distanciaPercorrida += movimento.magnitude;

        // Rastros de bolhas
        tempoProximoRastro -= Time.deltaTime;
        if (tempoProximoRastro <= 0f)
        {
            SpawnRastroBolhas();
            tempoProximoRastro = intervaloRastro;
        }

        // Checagem de proximidade para explosão
        if (alvoAtual != null)
        {
            float distancia = Vector3.Distance(transform.position, alvoAtual.position);
            if (distancia <= 3f)
            {
                Explodir();
                return;
            }
        }

        // Autodestruição após distância máxima
        if (distanciaPercorrida > 2000f)
        {
            DestruirSemExplosao();
        }
    }

    private void AdquirirAlvoProximo()
    {
        int proximos = Physics.OverlapSphereNonAlloc(transform.position, 40f, bufferDeteccao, layersDetectaveis, QueryTriggerInteraction.Ignore);
        float menorDistancia = float.MaxValue;
        Transform melhorAlvo = null;

        for (int i = 0; i < proximos; i++)
        {
            Collider col = bufferDeteccao[i];
            if (col == null) continue;
            if (lancador != null && (col.transform == lancador || col.transform.IsChildOf(lancador))) continue;

            IdentidadeUnidade id = col.GetComponentInParent<IdentidadeUnidade>();
            if (id == null) id = col.GetComponentInChildren<IdentidadeUnidade>();

            if (id != null && timeLancador >= 0 && id.teamID == timeLancador) continue;

            ControleNavioRealista navio = col.GetComponentInParent<ControleNavioRealista>();
            if (navio == null) navio = col.GetComponentInChildren<ControleNavioRealista>();

            ControleSubmarino sub = col.GetComponentInParent<ControleSubmarino>();
            if (sub == null) sub = col.GetComponentInChildren<ControleSubmarino>();

            bool ehNavio = navio != null;
            bool ehSubmarino = sub != null;
            bool ehAlvoNaval = false;
            try { if (col.CompareTag(tagAlvo) || col.CompareTag("Navio") || col.CompareTag("Submarino")) ehAlvoNaval = true; } catch { }
            try { if (col.transform.parent != null && (col.transform.parent.CompareTag("Navio") || col.transform.parent.CompareTag("Submarino"))) ehAlvoNaval = true; } catch { }

            if (ehNavio || ehSubmarino || ehAlvoNaval || (id != null && id.teamID != timeLancador && id.teamID != 0))
            {
                Transform targetRoot = navio != null ? navio.transform : (sub != null ? sub.transform : col.transform);
                float dist = Vector3.Distance(transform.position, targetRoot.position);
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    melhorAlvo = targetRoot;
                }
            }
        }

        if (melhorAlvo != null)
        {
            DefinirAlvo(melhorAlvo);
        }
    }

    private void ChecarProximidadeAvancada()
    {
        int proximos = Physics.OverlapSphereNonAlloc(transform.position, 12f, bufferDeteccao, layersDetectaveis, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < proximos; i++)
        {
            Collider col = bufferDeteccao[i];
            if (col == null) continue;
            if (lancador != null && (col.transform == lancador || col.transform.IsChildOf(lancador))) continue;

            IdentidadeUnidade id = col.GetComponentInParent<IdentidadeUnidade>();
            if (id == null) id = col.GetComponentInChildren<IdentidadeUnidade>();

            if (id != null && timeLancador >= 0 && id.teamID == timeLancador) continue;

            ControleNavioRealista navio = col.GetComponentInParent<ControleNavioRealista>();
            if (navio == null) navio = col.GetComponentInChildren<ControleNavioRealista>();

            ControleSubmarino sub = col.GetComponentInParent<ControleSubmarino>();
            if (sub == null) sub = col.GetComponentInChildren<ControleSubmarino>();

            bool ehNavio = navio != null;
            bool ehSubmarino = sub != null;
            bool ehAlvoNaval = false;
            try { if (col.CompareTag(tagAlvo) || col.CompareTag("Navio") || col.CompareTag("Submarino")) ehAlvoNaval = true; } catch { }
            try { if (col.transform.parent != null && (col.transform.parent.CompareTag("Navio") || col.transform.parent.CompareTag("Submarino"))) ehAlvoNaval = true; } catch { }

            if (ehNavio || ehSubmarino || ehAlvoNaval || (id != null && id.teamID != timeLancador && id.teamID != 0))
            {
                if (!emSubida)
                {
                    emSubida = true;
                    alvoAtual = navio != null ? navio.transform : (sub != null ? sub.transform : col.transform);
                }
                else
                {
                    Explodir();
                    return;
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (explodiu) return;

        // Ignorar o próprio lançador inicialmente
        if (lancador != null && (other.transform == lancador || other.transform.IsChildOf(lancador)))
            return;

        // Extrair identidade
        IdentidadeUnidade id = other.GetComponentInParent<IdentidadeUnidade>();
        if (id == null) id = other.GetComponentInChildren<IdentidadeUnidade>();

        // Verificar identidade de time (não atacar aliados)
        if (id != null && timeLancador >= 0 && id.teamID == timeLancador)
            return;

        // Verificar se é um alvo válido (navio/submarino)
        ControleNavioRealista navio = other.GetComponentInParent<ControleNavioRealista>();
        if (navio == null) navio = other.GetComponentInChildren<ControleNavioRealista>();

        ControleSubmarino sub = other.GetComponentInParent<ControleSubmarino>();
        if (sub == null) sub = other.GetComponentInChildren<ControleSubmarino>();

        bool ehNavio = navio != null;
        bool ehSubmarino = sub != null;

        bool ehAlvoNaval = false;
        try { if (other.CompareTag(tagAlvo) || other.CompareTag("Navio") || other.CompareTag("Submarino")) ehAlvoNaval = true; } catch { }
        try { if (other.transform.parent != null && (other.transform.parent.CompareTag("Navio") || other.transform.parent.CompareTag("Submarino"))) ehAlvoNaval = true; } catch { }

        if (ehNavio || ehSubmarino || ehAlvoNaval || (id != null && id.teamID != timeLancador && id.teamID != 0))
        {
            // Se ainda não está em subida, iniciar subida
            if (!emSubida)
            {
                emSubida = true;
                alvoAtual = navio != null ? navio.transform : (sub != null ? sub.transform : other.transform);
                return;
            }
            
            Explodir();
        }
    }

    void SpawnRastroBolhas()
    {
        if (rastroBolas == null) return;
        
        // Criar bolhas na superfície da água
        Vector3 posSuperficie = new Vector3(transform.position.x, 0, transform.position.z);
        GameObject bolhasObj = PoolDeObjetosCombate.SpawnTemporario(rastroBolas.gameObject, posSuperficie, Quaternion.identity, 3f);
        ParticleSystem bolhas = bolhasObj != null ? bolhasObj.GetComponent<ParticleSystem>() : null;
        if (bolhas != null) bolhas.Play();
    }

    void Explodir()
    {
        if (explodiu) return;
        explodiu = true;

        // Som de explosão
        if (somExplosao != null)
            AudioSource.PlayClipAtPoint(somExplosao, transform.position, 1.0f);

        // Efeito visual
        if (prefabExplosao != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(prefabExplosao, transform.position, Quaternion.identity, 5f);
        }

        // Dano em área
        int atingidos = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, bufferExplosao, layersDetectaveis, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < atingidos; i++)
        {
            Collider col = bufferExplosao[i];
            if (col == null) continue;

            // Aplicar dano
            SistemaDeDanos danos = col.GetComponentInParent<SistemaDeDanos>();
            if (danos == null) danos = col.GetComponentInChildren<SistemaDeDanos>();
            if (danos != null)
            {
                // Dano extra em submarinos próximos à superfície
                float danoReal = danoExplosao;
                ControleSubmarino sub = col.GetComponentInParent<ControleSubmarino>();
                if (sub == null) sub = col.GetComponentInChildren<ControleSubmarino>();
                if (sub != null && !sub.EstaSubmerso())
                    danoReal *= 1.5f;
                
                danos.ReceberDano(danoReal);
            }

            // Empurrar objetos com rigidbody
            Rigidbody rb = col.GetComponentInParent<Rigidbody>();
            if (rb == null) rb = col.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                Vector3 direcaoRepulsao = (col.transform.position - transform.position).normalized;
                rb.AddForce(direcaoRepulsao * forcaRepulsao, ForceMode.Impulse);
            }
        }

        // Criar cratera na água (efeito visual)
        CriarOndaDeImpacto();

        PoolDeObjetosCombate.Release(gameObject);
    }

    void CriarOndaDeImpacto()
    {
        // Criar um efeito de ondulação na água
        int naAgua = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao * 2f, bufferOnda, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < naAgua; i++)
        {
            Collider col = bufferOnda[i];
            if (col == null) continue;

            ControleNavioRealista navio = col.GetComponentInParent<ControleNavioRealista>();
            if (navio == null) navio = col.GetComponentInChildren<ControleNavioRealista>();
            if (navio != null)
            {
                // Aplicar "tremor" ao navio atingido pela onda de choque subaquática
                Rigidbody rb = col.GetComponentInParent<Rigidbody>();
                if (rb == null) rb = col.GetComponentInChildren<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(Vector3.up * forcaRepulsao * 0.3f, ForceMode.Impulse);
                }
            }
        }
    }

    void DestruirSemExplosao()
    {
        if (explodiu) return;
        explodiu = true;
        PoolDeObjetosCombate.Release(gameObject);
    }

    public void DefinirAlvo(Transform alvo)
    {
        alvoAtual = alvo;
        if (alvo != null)
            posicaoAlvoPerdido = alvo.position;
            
        DeterminarProfundidadeAlvo(alvo);
    }

    private void DeterminarProfundidadeAlvo(Transform alvo)
    {
        if (alvo == null) return;
        
        bool ehSubmarino = alvo.GetComponentInParent<ControleSubmarino>() != null || 
                           alvo.GetComponentInChildren<ControleSubmarino>() != null || 
                           alvo.CompareTag("Submarino");
                           
        try { if (TagSafe.Matches(alvo, "Submarino")) ehSubmarino = true; } catch { }
        
        if (ehSubmarino)
        {
            profundidadeNavegacao = -8f; // Afunda caso seja submarino
        }
        else
        {
            profundidadeNavegacao = -0.5f; // Paira a agua se for navio
        }
    }

    public void DefinirLancador(Transform lanc, int time)
    {
        lancador = lanc;
        timeLancador = time;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, raioExplosao);
        
        if (alvoAtual != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, alvoAtual.position);
        }
    }
}
