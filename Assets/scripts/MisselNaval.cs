using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.BrainMaster;
using UnityEngine;

public class MisselNaval : MonoBehaviour
{
    [Header("Fase 1: Ejecao (Lancamento Frio)")]
    public float velocidadeEjecao = 5f;
    public float tempoEjecao = 2.5f;

    [Header("Fase 2: Boost Vertical")]
    public float tempoBoostVertical = 4.0f;
    public float aceleracaoBoost = 35f;
    public ParticleSystem sistemaFumaca;

    [Header("Fase 3: Cruzeiro e Mergulho")]
    public float velocidadeCruzeiro = 90f;
    public float velocidadeMergulho = 180f;
    public float distanciaInicioMergulho = 120f;
    public float forcaRotacaoCruzeiro = 1.5f;
    public float forcaRotacaoMergulho = 5.0f;

    [Header("Engajamento Aéreo")]
    public float distanciaInicioPerseguicaoAerea = 260f;
    public float margemAltitudeAlvoAereo = 30f;
    public float raioDetonacaoProximidadeAerea = 18f;
    public float multiplicadorRotacaoAerea = 1.65f;
    public float multiplicadorVelocidadeAerea = 1.15f;

    [Header("Explosao")]
    public float dano = 200f;
    public float raioExplosao = 20f;
    public float escalaVisualExplosao = 1.0f;
    [Range(0f, 1f)] public float volumeSom = 1.0f;
    public GameObject efeitoExplosaoPrefab;
    public AudioClip somExplosao;
    public float tempoMaximoVida = 24f;

    [Header("Visual da Fumaca")]
    public Color corFumaca = new Color(0.8f, 0.8f, 0.8f, 0.5f);
    public float tamanhoFumaca = 1.5f;
    [Header("Orientação Visual")]
    [Tooltip("Ativa uma correção automática para prefabs cujo eixo visual foi importado separado do eixo da raiz. Deixe desligado quando o ponto de saída já estiver configurado reto.")]
    public bool corrigirEixoVisualAutomaticamente = false;
    [Tooltip("Referência opcional da malha do míssil. Se vazia, a primeira malha filha é usada quando a correção automática está ativa.")]
    public Transform referenciaVisual;

    private static readonly Collider[] bufferExplosao = new Collider[32];
    private static readonly HashSet<int> alvosProcessados = new HashSet<int>();

    private Vector3 pontoAlvo;
    private Transform alvoTransform;
    private bool lancado = false;
    private bool emNavegacao = false;
    private float velocidadeAtual = 0f;
    private Rigidbody rb;
    private bool jaExplodiu = false;
    private float tempoExpirar;
    private bool alvoEhAereo = false;
    private Quaternion rotacaoVisualLocal = Quaternion.identity;
    private Quaternion correcaoOrientacaoVisual = Quaternion.identity;

    void OnEnable()
    {
        IA_CombatTelemetry.RegisterMissile();
        ResetarEstado();
        tempoExpirar = Time.time + tempoMaximoVida;
    }

    void OnDisable()
    {
        IA_CombatTelemetry.UnregisterMissile();
        StopAllCoroutines();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (sistemaFumaca != null)
        {
            sistemaFumaca.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.freezeRotation = true;

        if (corrigirEixoVisualAutomaticamente)
        {
            ResolverOrientacaoVisual();
        }

        if (sistemaFumaca != null)
        {
            sistemaFumaca.Stop();
            var main = sistemaFumaca.main;
            main.startColor = corFumaca;
            main.startSize = tamanhoFumaca;
        }
    }

    private Transform lancador;

    public void IniciarAtaque(Vector3 alvo, Transform alvoT = null, Transform lancadorRef = null)
    {
        StopAllCoroutines();
        lancador = lancadorRef != null ? lancadorRef.root : null;
        pontoAlvo = alvo;
        alvoTransform = alvoT;
        alvoEhAereo = DetectarAlvoAereo(alvoT, alvo);
        lancado = true;
        jaExplodiu = false;
        emNavegacao = false;
        tempoExpirar = Time.time + tempoMaximoVida;
        transform.rotation = RotacaoParaDirecao(Vector3.up);
        StartCoroutine(SequenciaDeVoo());
    }

    IEnumerator SequenciaDeVoo()
    {
        float tempo = 0f;
        velocidadeAtual = velocidadeEjecao;

        while (tempo < tempoEjecao)
        {
            rb.linearVelocity = Vector3.up * velocidadeAtual;
            transform.rotation = Quaternion.Slerp(transform.rotation, RotacaoParaDirecao(Vector3.up), Time.deltaTime * 5f);
            tempo += Time.deltaTime;
            yield return null;
        }

        if (sistemaFumaca != null)
        {
            sistemaFumaca.Play();
        }

        tempo = 0f;
        while (tempo < tempoBoostVertical)
        {
            velocidadeAtual += aceleracaoBoost * Time.deltaTime;
            rb.linearVelocity = Vector3.up * velocidadeAtual;
            transform.rotation = Quaternion.Slerp(transform.rotation, RotacaoParaDirecao(Vector3.up), Time.deltaTime * 5f);
            tempo += Time.deltaTime;
            yield return null;
        }

        emNavegacao = true;
    }

    void FixedUpdate()
    {
        if (!lancado)
        {
            return;
        }

        if (Time.time >= tempoExpirar)
        {
            Explodir();
            return;
        }

        if (!emNavegacao)
        {
            return;
        }

        if (alvoTransform != null)
        {
            pontoAlvo = alvoTransform.position;
        }

        Vector3 vetorParaAlvo = pontoAlvo - transform.position;
        float distanciaHorizontal = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(pontoAlvo.x, pontoAlvo.z));
        float distanciaTotal = vetorParaAlvo.magnitude;
        bool alvoAlto = pontoAlvo.y > transform.position.y + margemAltitudeAlvoAereo;
        bool perseguirComoAereo = alvoEhAereo || alvoAlto;

        if (perseguirComoAereo && distanciaHorizontal > distanciaInicioMergulho)
        {
            Vector3 direcaoAerea = vetorParaAlvo.normalized;
            GirarPara(direcaoAerea, forcaRotacaoMergulho * multiplicadorRotacaoAerea);
            velocidadeAtual = Mathf.Lerp(
                velocidadeAtual,
                velocidadeMergulho * multiplicadorVelocidadeAerea,
                Time.fixedDeltaTime * 2.5f);
        }
        else if (distanciaHorizontal > Mathf.Max(distanciaInicioMergulho, distanciaInicioPerseguicaoAerea))
        {
            Vector3 direcaoHorizontal = new Vector3(vetorParaAlvo.x, 0f, vetorParaAlvo.z).normalized;
            Vector3 direcaoDesejada = direcaoHorizontal;

            if (transform.position.y > pontoAlvo.y + 100f)
            {
                direcaoDesejada = (direcaoHorizontal - Vector3.up * 0.1f).normalized;
            }

            GirarPara(direcaoDesejada, forcaRotacaoCruzeiro * 3f);
            velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeCruzeiro, Time.fixedDeltaTime * 0.8f);
        }
        else
        {
            GirarPara(vetorParaAlvo.normalized, forcaRotacaoMergulho);
            velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeMergulho, Time.fixedDeltaTime * 2f);
        }

        // A raiz e o ponto de saída formam o eixo de voo do míssil. A
        // correção visual fica opcional porque aplicá-la automaticamente em
        // um prefab que já foi orientado no Inspector cria uma segunda
        // rotação de 90 graus.
        Vector3 frenteVisual = ObterFrenteVisual();
        if (frenteVisual.sqrMagnitude < 0.001f)
        {
            frenteVisual = vetorParaAlvo.sqrMagnitude > 0.001f
                ? vetorParaAlvo.normalized
                : Vector3.forward;
        }
        rb.linearVelocity = frenteVisual.normalized * velocidadeAtual;

        float distanciaDetonacao = perseguirComoAereo
            ? Mathf.Max(10f, raioDetonacaoProximidadeAerea)
            : 10f;

        if (distanciaTotal < distanciaDetonacao)
        {
            Explodir();
        }
    }

    void GirarPara(Vector3 direcao, float velocidadeGiro)
    {
        if (direcao == Vector3.zero)
        {
            return;
        }

        Quaternion rotacaoAlvo = RotacaoParaDirecao(direcao);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, velocidadeGiro * Time.fixedDeltaTime);
    }

    private void ResolverOrientacaoVisual()
    {
        if (referenciaVisual == null)
        {
            MeshRenderer mesh = GetComponentInChildren<MeshRenderer>(true);
            if (mesh == null)
            {
                SkinnedMeshRenderer skinned = GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (skinned != null) referenciaVisual = skinned.transform;
            }
            else
            {
                referenciaVisual = mesh.transform;
            }
        }

        rotacaoVisualLocal = Quaternion.identity;
        if (referenciaVisual != null && referenciaVisual != transform)
        {
            Transform cursor = referenciaVisual;
            while (cursor != null && cursor != transform)
            {
                rotacaoVisualLocal = cursor.localRotation * rotacaoVisualLocal;
                cursor = cursor.parent;
            }
        }

        correcaoOrientacaoVisual = Quaternion.Inverse(rotacaoVisualLocal);
    }

    private Quaternion RotacaoParaDirecao(Vector3 direcao)
    {
        if (direcao.sqrMagnitude < 0.001f)
        {
            direcao = Vector3.forward;
        }

        direcao.Normalize();
        Vector3 eixoUp = Mathf.Abs(Vector3.Dot(direcao, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;
        Quaternion rotacaoBase = Quaternion.LookRotation(direcao, eixoUp);
        return corrigirEixoVisualAutomaticamente
            ? rotacaoBase * correcaoOrientacaoVisual
            : rotacaoBase;
    }

    private Vector3 ObterFrenteVisual()
    {
        if (!corrigirEixoVisualAutomaticamente)
        {
            return transform.forward;
        }

        Vector3 frente = transform.rotation * rotacaoVisualLocal * Vector3.forward;
        return frente.sqrMagnitude > 0.001f ? frente.normalized : transform.forward;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (DeveIgnorarTrigger(collision.collider)) return;
        Explodir();
    }

    void OnTriggerEnter(Collider other)
    {
        if (DeveIgnorarTrigger(other))
        {
            return;
        }

        Explodir();
    }

    void Explodir()
    {
        if (jaExplodiu)
        {
            return;
        }

        jaExplodiu = true;

        if (efeitoExplosaoPrefab != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(
                efeitoExplosaoPrefab,
                transform.position,
                Quaternion.identity,
                5f,
                Vector3.one * escalaVisualExplosao);
        }

        if (somExplosao != null)
        {
            GameObject audioObj = new GameObject("SomExplosaoMissil");
            audioObj.transform.position = transform.position;
            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.clip = somExplosao;
            source.volume = Mathf.Min(Mathf.Clamp01(volumeSom), 0.8f);
            source.spatialBlend = 1f;
            source.minDistance = 3f;
            source.maxDistance = 300f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(audioObj, somExplosao.length + 0.5f);
        }

        alvosProcessados.Clear();
        int atingidos = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, bufferExplosao);
        for (int i = 0; i < atingidos; i++)
        {
            Collider col = bufferExplosao[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            SistemaDeDanos alvoVida = col.GetComponentInParent<SistemaDeDanos>();
            if (alvoVida != null)
            {
                int idVida = alvoVida.GetInstanceID();
                if (!alvosProcessados.Add(idVida)) continue;
                alvoVida.ReceberDano(dano, lancador != null ? lancador.gameObject : null);
            }

            bufferExplosao[i] = null;
        }

        PoolDeObjetosCombate.Release(gameObject);
    }

    bool DeveIgnorarTrigger(Collider other)
    {
        if (other == null) return true;
        if (other.CompareTag("Player")) return true;
        if (other.isTrigger) return true;

        Transform raizOutro = other.transform.root != null ? other.transform.root : other.transform;
        Transform raizMinha = transform.root != null ? transform.root : transform;

        // 1. Evita atirar na própria pool
        if (raizOutro == raizMinha) return true;
        
        // 2. Evita explodir em quem atirou!
        if (lancador != null && raizOutro == lancador) return true;

        return false;
    }

    private void ResetarEstado()
    {
        pontoAlvo = Vector3.zero;
        alvoTransform = null;
        alvoEhAereo = false;
        lancado = false;
        emNavegacao = false;
        velocidadeAtual = 0f;
        jaExplodiu = false;
    }

    private bool DetectarAlvoAereo(Transform alvo, Vector3 pontoFallback)
    {
        if (alvo == null)
        {
            return pontoFallback.y > 35f;
        }

        IdentidadeUnidade identidade = alvo.GetComponentInParent<IdentidadeUnidade>();
        string nome = alvo.name.ToLowerInvariant();

        return alvo.position.y > 8f
               || alvo.GetComponentInParent<ControleAviao>() != null
               || alvo.GetComponentInParent<ControleAviaoCaca>() != null
               || alvo.GetComponentInParent<AviaoBombardeiro>() != null
               || alvo.GetComponentInParent<Helicoptero>() != null
               || (identidade != null && identidade.tipoUnidade == TipoUnidade.Aereo)
               || nome.Contains("aviao")
               || nome.Contains("heli")
               || nome.Contains("caca")
               || nome.Contains("bombard")
               || nome.Contains("bombardeiro")
               || nome.Contains("bomber");
    }
}
