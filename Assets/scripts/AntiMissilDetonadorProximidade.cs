using UnityEngine;

/// <summary>
/// Componente de segurança para interceptação:
/// - Quando o interceptador chega perto do alvo, destrói o míssil alvo mesmo que ele não tenha Collider/SistemaDeDanos.
/// - Em seguida tenta disparar a explosão do próprio interceptador (se existir) e destrói o GameObject.
/// </summary>
public class AntiMissilDetonadorProximidade : MonoBehaviour
{
    [Tooltip("Alvo que deve ser neutralizado pelo interceptador.")]
    public Transform alvo;

    [Tooltip("Distância base para considerar que houve interceptação.")]
    public float distanciaBaseIntercepcao = 10f;

    [Tooltip("Fator de segurança para não \"pular\" o alvo em alta velocidade (distância mínima = velocidade*deltaTime*fator).")]
    public float fatorSegurancaFrame = 2.2f;

    [Tooltip("Se marcado, destrói o alvo mesmo se não parecer um míssil.")]
    public bool forcarDestruicao = false;
    public bool autoDestruirSemAlvo = true;

    private Vector3 _ultimaPosicao;
    private bool _inicializado = false;
    private float _velocidadeAproximada = 0f;

    void OnEnable()
    {
        _ultimaPosicao = transform.position;
        _inicializado = true;
    }

    void Update()
    {
        if (alvo == null || !alvo.gameObject.activeInHierarchy)
        {
            if (autoDestruirSemAlvo) Destroy(gameObject);
            return;
        }

        float limite = CalcularLimiteDetonacao();
        float distancia = Vector3.Distance(transform.position, alvo.position);

        if (distancia <= limite)
        {
            NeutralizarAlvo();
            DetonarSelf();
        }
    }

    float CalcularLimiteDetonacao()
    {
        float velocidade = 0f;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            velocidade = rb.linearVelocity.magnitude;
        }
        else if (_inicializado)
        {
            Vector3 delta = (transform.position - _ultimaPosicao);
            _velocidadeAproximada = Mathf.Lerp(_velocidadeAproximada, delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f), 0.35f);
            velocidade = _velocidadeAproximada;
        }

        _ultimaPosicao = transform.position;

        float limiteFrame = velocidade * Time.deltaTime * fatorSegurancaFrame;
        return Mathf.Max(distanciaBaseIntercepcao, limiteFrame);
    }

    void NeutralizarAlvo()
    {
        if (alvo == null) return;

        Transform raiz = alvo;
        Rigidbody rbAlvo = alvo.GetComponentInParent<Rigidbody>();
        if (rbAlvo != null) raiz = rbAlvo.transform;
        else if (alvo.root != null) raiz = alvo.root;

        if (raiz == null) return;

        if (!forcarDestruicao && !PareceMissil(raiz)) return;

        Destroy(raiz.gameObject);
    }

    bool PareceMissil(Transform tr)
    {
        if (tr == null) return false;
        // TagManager do projeto usa "Missel". Mantemos "Missil" como compatibilidade.
        string tagStr = tr.gameObject.tag;
        if (tagStr == "Missel" || tagStr == "Missil" || tagStr == "Missile" || tagStr == "Míssil") return true;

        // Scripts de míssil / projétil guiado/explosivo
        if (tr.GetComponentInParent<MisselNaval>() != null) return true;
        if (tr.GetComponentInParent<MisselCaca>() != null) return true;
        if (tr.GetComponentInParent<MisselSubmarino>() != null) return true;
        if (tr.GetComponentInParent<MisselICBM>() != null) return true;
        if (tr.GetComponentInParent<MisselTatico>() != null) return true;
        if (tr.GetComponentInParent<MisselLeopardAutomatico>() != null) return true;
        if (tr.GetComponentInParent<MissilTeleguiado>() != null) return true;
        if (tr.GetComponentInParent<InterceptMissile>() != null) return true;

        Projetil proj = tr.GetComponentInParent<Projetil>();
        if (proj != null)
        {
            if (proj.curvaDePerseguicao > 0f || proj.raioDeExplosao > 0.01f) return true;
        }

        return false;
    }

    void DetonarSelf()
    {
        if (forcarDestruicao)
        {
            Destroy(gameObject);
            return;
        }

        // Tenta acionar métodos comuns de "explodir" (mesmo que sejam private).
        SendMessage("Explodir", SendMessageOptions.DontRequireReceiver);
        SendMessage("DestroyMissile", SendMessageOptions.DontRequireReceiver);

        // Fallback: remove o interceptador.
        Destroy(gameObject);
    }
}
