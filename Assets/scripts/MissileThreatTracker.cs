using System.Collections.Generic;
using UnityEngine;

public class MissileThreatTracker : MonoBehaviour
{
    private static readonly List<MissileThreatTracker> ameacasAtivas = new List<MissileThreatTracker>(64);
    private static int proximoId = 1;

    [SerializeField] private int missilId = -1;
    [SerializeField] private int teamOrigem = -1;
    [SerializeField] private bool interceptor = false;
    [SerializeField] private float velocidadeEstimada = 80f;
    [SerializeField] private Vector3 ultimoAlvoConhecido;
    [SerializeField] private float tempoDeVidaInicial = 10f;

    private Transform alvoTransform;
    private Transform raizMissil;
    private Rigidbody rb;
    private bool registrado = false;

    public int TeamOrigem => teamOrigem;
    public bool Interceptor => interceptor;
    public Transform RaizMissil => raizMissil != null ? raizMissil : (transform.root != null ? transform.root : transform);

    public Vector3 ObterVelocidadeAtual()
    {
        if (rb == null && raizMissil != null) rb = raizMissil.GetComponent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f) return rb.linearVelocity;

        Vector3 direcao = ObterDirecaoAtual();
        return direcao * Mathf.Max(velocidadeEstimada, 1f);
    }

    public static void RegistrarLancamento(GameObject missil, Component origem, Vector3 alvo, Transform alvoDinamico = null, float velocidade = 0f, bool ehInterceptador = false)
    {
        if (missil == null) return;

        MissileThreatTracker tracker = missil.GetComponent<MissileThreatTracker>();
        if (tracker == null) tracker = missil.AddComponent<MissileThreatTracker>();

        tracker.Configurar(origem, alvo, alvoDinamico, velocidade, ehInterceptador);
    }

    public static float EstimarVelocidade(GameObject missil)
    {
        if (missil == null) return 80f;

        Projetil projetil = missil.GetComponent<Projetil>();
        if (projetil != null && projetil.velocidade > 0f) return projetil.velocidade;

        MisselNaval misselNaval = missil.GetComponent<MisselNaval>();
        if (misselNaval != null) return Mathf.Max(misselNaval.velocidadeCruzeiro, misselNaval.velocidadeMergulho);

        MisselCaca misselCaca = missil.GetComponent<MisselCaca>();
        if (misselCaca != null) return misselCaca.velocidadeMaxima;

        MisselSubmarino misselSubmarino = missil.GetComponent<MisselSubmarino>();
        if (misselSubmarino != null) return Mathf.Max(misselSubmarino.velocidadeMaxima, misselSubmarino.velocidadeTurbo);

        MisselLeopardAutomatico misselLeopard = missil.GetComponent<MisselLeopardAutomatico>();
        if (misselLeopard != null) return Mathf.Max(misselLeopard.velocidadeMaxima, misselLeopard.velocidadeTurbo);

        MisselICBM misselIcbm = missil.GetComponent<MisselICBM>();
        if (misselIcbm != null) return misselIcbm.velocidade;

        MisselTatico misselTatico = missil.GetComponent<MisselTatico>();
        if (misselTatico != null) return misselTatico.velocidade;

        MissilTeleguiado missilGuiado = missil.GetComponent<MissilTeleguiado>();
        if (missilGuiado != null) return missilGuiado.velocidade;

        return 80f;
    }

    public static Transform EncontrarAmeacaMaisProxima(Vector3 centroDefesa, float alcanceDefesa, int teamDefesa, Transform raizIgnorada, float multiplicadorAntecipacao, float janelaAntecipacaoSegundos)
    {
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;
        float raioEfetivo = Mathf.Max(alcanceDefesa * multiplicadorAntecipacao, alcanceDefesa);

        for (int i = ameacasAtivas.Count - 1; i >= 0; i--)
        {
            MissileThreatTracker tracker = ameacasAtivas[i];
            if (tracker == null)
            {
                ameacasAtivas.RemoveAt(i);
                continue;
            }

            Transform raiz = tracker.RaizMissil;
            if (raiz == null || !raiz.gameObject.activeInHierarchy)
            {
                ameacasAtivas.RemoveAt(i);
                continue;
            }

            if (tracker.teamOrigem != -1 && teamDefesa != -1 && tracker.teamOrigem == teamDefesa) continue;
            if (raizIgnorada != null && raiz == raizIgnorada) continue;

            if (!tracker.PodeEntrarNaArea(centroDefesa, raioEfetivo, janelaAntecipacaoSegundos)) continue;

            float distancia = Vector3.Distance(centroDefesa, raiz.position);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                melhorAlvo = raiz;
            }
        }

        return melhorAlvo;
    }

    void OnDisable()
    {
        CancelInvoke(nameof(ExpirarMissil));
        RemoverRegistro();
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(ExpirarMissil));
        RemoverRegistro();
    }

    void Configurar(Component origem, Vector3 alvo, Transform alvoDinamico, float velocidade, bool ehInterceptador)
    {
        if (missilId < 0) missilId = proximoId++;

        raizMissil = transform.root != null ? transform.root : transform;
        rb = raizMissil.GetComponent<Rigidbody>();
        alvoTransform = alvoDinamico;
        ultimoAlvoConhecido = alvoDinamico != null ? alvoDinamico.position : alvo;
        interceptor = ehInterceptador;
        velocidadeEstimada = Mathf.Max(velocidade, EstimarVelocidade(raizMissil.gameObject), 1f);
        teamOrigem = ResolverTeam(origem, raizMissil);
        CancelInvoke(nameof(ExpirarMissil));
        Invoke(nameof(ExpirarMissil), tempoDeVidaInicial);

        if (teamOrigem != -1)
        {
            IdentidadeUnidade identidade = raizMissil.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = raizMissil.gameObject.AddComponent<IdentidadeUnidade>();
            identidade.teamID = teamOrigem;
        }

        if (!registrado)
        {
            ameacasAtivas.Add(this);
            registrado = true;
        }
    }

    bool PodeEntrarNaArea(Vector3 centroDefesa, float raioDefesa, float janelaAntecipacaoSegundos)
    {
        Transform raiz = RaizMissil;
        if (raiz == null) return false;

        Vector3 inicio = raiz.position;
        Vector3 fim = ObterPontoPrevisto(janelaAntecipacaoSegundos);
        float raioSqr = raioDefesa * raioDefesa;

        if ((inicio - centroDefesa).sqrMagnitude <= raioSqr) return true;
        return DistanciaSqrPontoSegmento(centroDefesa, inicio, fim) <= raioSqr;
    }

    Vector3 ObterPontoPrevisto(float janelaAntecipacaoSegundos)
    {
        Transform raiz = RaizMissil;
        Vector3 inicio = raiz != null ? raiz.position : transform.position;
        Vector3 alvoAtual = ObterAlvoAtual();
        Vector3 direcao = alvoAtual - inicio;

        if (direcao.sqrMagnitude <= 0.01f)
        {
            direcao = ObterDirecaoAtual();
        }
        else
        {
            direcao.Normalize();
        }

        float distanciaMaxima = Mathf.Max(velocidadeEstimada * Mathf.Max(janelaAntecipacaoSegundos, 0.5f), 50f);
        float distanciaAlvo = Vector3.Distance(inicio, alvoAtual);
        float distanciaPrevista = Mathf.Min(distanciaAlvo, distanciaMaxima);

        return inicio + (direcao * distanciaPrevista);
    }

    Vector3 ObterAlvoAtual()
    {
        if (alvoTransform != null)
        {
            ultimoAlvoConhecido = alvoTransform.position;
        }

        return ultimoAlvoConhecido;
    }

    Vector3 ObterDirecaoAtual()
    {
        if (rb == null && raizMissil != null) rb = raizMissil.GetComponent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f) return rb.linearVelocity.normalized;
        if (RaizMissil.forward.sqrMagnitude > 0.01f) return RaizMissil.forward.normalized;
        return Vector3.forward;
    }

    void RemoverRegistro()
    {
        if (!registrado) return;
        ameacasAtivas.Remove(this);
        registrado = false;
    }

    void ExpirarMissil()
    {
        Transform raiz = RaizMissil;
        if (raiz == null)
        {
            return;
        }

        if (raiz.GetComponent<PoolDeObjetoCombateLink>() != null)
        {
            PoolDeObjetosCombate.Release(raiz.gameObject);
            return;
        }

        Destroy(raiz.gameObject);
    }

    static int ResolverTeam(Component origem, Transform raizMissil)
    {
        IdentidadeUnidade identidade = null;

        if (origem != null)
        {
            identidade = origem.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = origem.GetComponentInParent<IdentidadeUnidade>();
        }

        if (identidade == null && raizMissil != null)
        {
            identidade = raizMissil.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = raizMissil.GetComponentInParent<IdentidadeUnidade>();
        }

        return identidade != null ? identidade.teamID : -1;
    }

    static float DistanciaSqrPontoSegmento(Vector3 ponto, Vector3 inicio, Vector3 fim)
    {
        Vector3 segmento = fim - inicio;
        float comprimentoSqr = segmento.sqrMagnitude;
        if (comprimentoSqr <= 0.0001f) return (ponto - inicio).sqrMagnitude;

        float t = Vector3.Dot(ponto - inicio, segmento) / comprimentoSqr;
        t = Mathf.Clamp01(t);
        Vector3 projecao = inicio + (segmento * t);
        return (ponto - projecao).sqrMagnitude;
    }
}
