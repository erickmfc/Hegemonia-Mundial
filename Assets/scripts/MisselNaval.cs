using System.Collections;
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

    private static readonly Collider[] bufferExplosao = new Collider[32];

    private Vector3 pontoAlvo;
    private Transform alvoTransform;
    private bool lancado = false;
    private bool emNavegacao = false;
    private float velocidadeAtual = 0f;
    private Rigidbody rb;
    private bool jaExplodiu = false;
    private float tempoExpirar;
    private bool alvoEhAereo = false;

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

        if (sistemaFumaca != null)
        {
            sistemaFumaca.Stop();
            var main = sistemaFumaca.main;
            main.startColor = corFumaca;
            main.startSize = tamanhoFumaca;
        }
    }

    public void IniciarAtaque(Vector3 alvo, Transform alvoT = null)
    {
        StopAllCoroutines();
        pontoAlvo = alvo;
        alvoTransform = alvoT;
        alvoEhAereo = DetectarAlvoAereo(alvoT, alvo);
        lancado = true;
        jaExplodiu = false;
        emNavegacao = false;
        tempoExpirar = Time.time + tempoMaximoVida;
        transform.rotation = Quaternion.LookRotation(Vector3.up);
        StartCoroutine(SequenciaDeVoo());
    }

    IEnumerator SequenciaDeVoo()
    {
        float tempo = 0f;
        velocidadeAtual = velocidadeEjecao;

        while (tempo < tempoEjecao)
        {
            rb.linearVelocity = Vector3.up * velocidadeAtual;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.up), Time.deltaTime * 5f);
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
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.up), Time.deltaTime * 5f);
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

        rb.linearVelocity = transform.forward * velocidadeAtual;

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

        Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, velocidadeGiro * Time.fixedDeltaTime);
    }

    void OnCollisionEnter(Collision collision)
    {
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
            source.volume = volumeSom;
            source.spatialBlend = 1f;
            source.minDistance = 10f;
            source.maxDistance = 600f;
            source.Play();
            Destroy(audioObj, somExplosao.length + 0.5f);
        }

        int atingidos = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, bufferExplosao);
        for (int i = 0; i < atingidos; i++)
        {
            Collider col = bufferExplosao[i];
            if (col == null)
            {
                continue;
            }

            SistemaDeDanos alvoVida = col.GetComponentInParent<SistemaDeDanos>();
            if (alvoVida != null)
            {
                alvoVida.ReceberDano(dano);
            }

            bufferExplosao[i] = null;
        }

        PoolDeObjetosCombate.Release(gameObject);
    }

    bool DeveIgnorarTrigger(Collider other)
    {
        if (other == null)
        {
            return true;
        }

        if (other.CompareTag("Player"))
        {
            return true;
        }

        if (other.isTrigger)
        {
            return true;
        }

        Transform raizOutro = other.transform.root != null ? other.transform.root : other.transform;
        Transform raizMinha = transform.root != null ? transform.root : transform;

        if (raizOutro == raizMinha)
        {
            return true;
        }

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
