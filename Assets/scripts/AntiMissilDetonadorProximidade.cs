using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Fusivel de proximidade usado pelos interceptadores.
/// Quando entra no raio do alvo, tenta acionar a explosao real do missil inimigo
/// antes de remove-lo para evitar que ele simplesmente suma sem impacto visual.
/// </summary>
public class AntiMissilDetonadorProximidade : MonoBehaviour
{
    [Tooltip("Alvo que deve ser neutralizado pelo interceptador.")]
    public Transform alvo;

    [Tooltip("Distancia base para considerar que houve interceptacao.")]
    public float distanciaBaseIntercepcao = 10f;

    [Tooltip("Fator de seguranca para nao pular o alvo em alta velocidade.")]
    public float fatorSegurancaFrame = 2.2f;

    [Tooltip("Se marcado, destroi o alvo mesmo se nao parecer um missil.")]
    public bool forcarDestruicao = false;
    public bool autoDestruirSemAlvo = true;

    private Vector3 ultimaPosicao;
    private bool inicializado = false;
    private float velocidadeAproximada = 0f;
    private readonly List<MonoBehaviour> componentesBuffer = new List<MonoBehaviour>(16);
    private static readonly Dictionary<string, MethodInfo> cacheMetodosSemArgumentos = new Dictionary<string, MethodInfo>(128);

    void OnEnable()
    {
        ultimaPosicao = transform.position;
        inicializado = true;
    }

    void Update()
    {
        if (alvo == null || !alvo.gameObject.activeInHierarchy)
        {
            if (autoDestruirSemAlvo)
            {
                RemoverObjeto(gameObject);
            }
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
        else if (inicializado)
        {
            Vector3 delta = transform.position - ultimaPosicao;
            velocidadeAproximada = Mathf.Lerp(
                velocidadeAproximada,
                delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f),
                0.35f);
            velocidade = velocidadeAproximada;
        }

        ultimaPosicao = transform.position;

        float limiteFrame = velocidade * Time.deltaTime * fatorSegurancaFrame;
        return Mathf.Max(distanciaBaseIntercepcao, limiteFrame);
    }

    void NeutralizarAlvo()
    {
        if (alvo == null) return;

        Transform raiz = ResolverRaiz(alvo);
        if (raiz == null) return;

        if (!forcarDestruicao && !PareceMissil(raiz)) return;

        if (TentarDetonarMissil(raiz.gameObject, raiz.position))
        {
            return;
        }

        CriarEfeitoFallback(raiz.position, 1.1f);
        RemoverObjeto(raiz.gameObject);
    }

    bool PareceMissil(Transform tr)
    {
        if (tr == null) return false;

        string tagStr = tr.gameObject.tag;
        if (tagStr == "Missel" || tagStr == "Missil" || tagStr == "Missile" || tagStr == "Missil")
        {
            return true;
        }

        if (tr.GetComponentInParent<MisselNaval>() != null) return true;
        if (tr.GetComponentInParent<MisselCaca>() != null) return true;
        if (tr.GetComponentInParent<MisselSubmarino>() != null) return true;
        if (tr.GetComponentInParent<MisselICBM>() != null) return true;
        if (tr.GetComponentInParent<MisselTatico>() != null) return true;
        if (tr.GetComponentInParent<MisselLeopardAutomatico>() != null) return true;
        if (tr.GetComponentInParent<MissilTeleguiado>() != null) return true;
        if (tr.GetComponentInParent<InterceptMissile>() != null) return true;

        Projetil proj = tr.GetComponentInParent<Projetil>();
        return proj != null && (proj.curvaDePerseguicao > 0f || proj.raioDeExplosao > 0.01f);
    }

    void DetonarSelf()
    {
        if (TentarDetonarMissil(gameObject, transform.position))
        {
            return;
        }

        CriarEfeitoFallback(transform.position, 0.75f);
        RemoverObjeto(gameObject);
    }

    Transform ResolverRaiz(Transform origem)
    {
        if (origem == null) return null;

        Rigidbody rbAlvo = origem.GetComponentInParent<Rigidbody>();
        if (rbAlvo != null)
        {
            return rbAlvo.transform;
        }

        return origem.root != null ? origem.root : origem;
    }

    bool TentarDetonarMissil(GameObject objeto, Vector3 posicaoImpacto)
    {
        if (objeto == null) return false;

        if (TentarInvocarMetodoSemArgumentos<MisselNaval>(objeto, "Explodir")) return true;
        if (TentarInvocarMetodoSemArgumentos<MisselCaca>(objeto, "Explodir")) return true;
        if (TentarInvocarMetodoSemArgumentos<MisselSubmarino>(objeto, "Explodir")) return true;
        if (TentarInvocarMetodoSemArgumentos<MisselICBM>(objeto, "Explodir")) return true;
        if (TentarInvocarMetodoSemArgumentos<MisselTatico>(objeto, "Explodir")) return true;
        if (TentarInvocarMetodoSemArgumentos<MisselLeopardAutomatico>(objeto, "Explodir")) return true;

        if (objeto.GetComponentInParent<MissilTeleguiado>() != null)
        {
            CriarEfeitoFallback(posicaoImpacto, 0.9f);
            if (TentarInvocarMetodoSemArgumentos<MissilTeleguiado>(objeto, "Liberar"))
            {
                return true;
            }
        }

        Projetil projetil = objeto.GetComponentInParent<Projetil>();
        if (projetil != null)
        {
            CriarImpactoDeProjetil(projetil, posicaoImpacto);
            if (TentarInvocarMetodoSemArgumentos<Projetil>(objeto, "Liberar"))
            {
                return true;
            }
        }

        if (TentarInvocarMetodoSemArgumentosEmHierarquia(objeto, "Explodir")) return true;
        if (TentarInvocarMetodoSemArgumentosEmHierarquia(objeto, "DestroyMissile")) return true;
        if (TentarInvocarMetodoSemArgumentosEmHierarquia(objeto, "Liberar")) return true;

        return false;
    }

    void CriarImpactoDeProjetil(Projetil projetil, Vector3 posicaoImpacto)
    {
        if (projetil == null) return;

        if (projetil.efeitoImpacto != null)
        {
            float escala = projetil.raioDeExplosao > 0f ? Mathf.Max(1f, projetil.raioDeExplosao * 0.8f) : 1f;
            PoolDeObjetosCombate.SpawnTemporario(
                projetil.efeitoImpacto,
                posicaoImpacto,
                Quaternion.identity,
                2f,
                Vector3.one * escala);
            return;
        }

        CriarEfeitoFallback(posicaoImpacto, Mathf.Max(0.75f, projetil.raioDeExplosao * 0.5f));
    }

    bool TentarInvocarMetodoSemArgumentos<T>(GameObject objeto, string nomeMetodo) where T : Component
    {
        T componente = objeto.GetComponentInParent<T>();
        if (componente == null) return false;
        return TentarInvocarMetodoSemArgumentos(componente, nomeMetodo);
    }

    bool TentarInvocarMetodoSemArgumentosEmHierarquia(GameObject objeto, string nomeMetodo)
    {
        if (objeto == null) return false;

        componentesBuffer.Clear();
        objeto.GetComponentsInChildren<MonoBehaviour>(true, componentesBuffer);
        for (int i = 0; i < componentesBuffer.Count; i++)
        {
            if (TentarInvocarMetodoSemArgumentos(componentesBuffer[i], nomeMetodo))
            {
                componentesBuffer.Clear();
                return true;
            }
        }

        componentesBuffer.Clear();
        return false;
    }

    bool TentarInvocarMetodoSemArgumentos(Component componente, string nomeMetodo)
    {
        if (componente == null) return false;

        Type tipo = componente.GetType();
        string chaveCache = tipo.FullName + "::" + nomeMetodo;
        MethodInfo metodo;
        if (!cacheMetodosSemArgumentos.TryGetValue(chaveCache, out metodo))
        {
            metodo = tipo.GetMethod(
                nomeMetodo,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            cacheMetodosSemArgumentos[chaveCache] = metodo;
        }

        if (metodo == null) return false;

        try
        {
            metodo.Invoke(componente, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    void CriarEfeitoFallback(Vector3 posicao, float escala)
    {
        if (GerenciadorFXGlobal.Instancia != null)
        {
            GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", posicao, Mathf.Max(0.5f, escala));
        }
    }

    void RemoverObjeto(GameObject objeto)
    {
        if (objeto == null) return;

        PoolDeObjetosCombate.Release(objeto);
        if (objeto != null && objeto.activeInHierarchy)
        {
            Destroy(objeto);
        }
    }
}
