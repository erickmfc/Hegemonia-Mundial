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
    [SerializeField] private bool destruirMissilAoExpirar = true;

    private Vector3 pontoLancamento;
    private float momentoLancamento;
    private string nomeOrigem = string.Empty;
    private string alvoNome = string.Empty;
    private int alvoTeam = -1;

    private Transform alvoTransform;
    private Transform raizMissil;
    private Rigidbody rb;
    private bool registrado = false;

    public int TeamOrigem => teamOrigem;
    public bool Interceptor => interceptor;
    public Transform RaizMissil => raizMissil != null ? raizMissil : (transform.root != null ? transform.root : transform);
    public int MissileId => missilId;
    public Vector3 PontoLancamento => pontoLancamento;
    public Vector3 PontoAlvoConhecido => ObterAlvoAtual();
    public float TempoDesdeLancamento => Mathf.Max(0f, Time.time - momentoLancamento);
    public string NomeOrigem => nomeOrigem;
    public string AlvoNome => alvoNome;
    public int AlvoTeam => alvoTeam;
    public bool PossuiAlvoDinamico => alvoTransform != null;

    public static bool TryObterAtivo(int id, out MissileThreatTracker resultado)
    {
        resultado = null;
        for (int i = ameacasAtivas.Count - 1; i >= 0; i--)
        {
            MissileThreatTracker tracker = ameacasAtivas[i];
            if (tracker == null || tracker.RaizMissil == null || !tracker.RaizMissil.gameObject.activeInHierarchy)
            {
                ameacasAtivas.RemoveAt(i);
                continue;
            }

            if (tracker.MissileId == id)
            {
                resultado = tracker;
                return true;
            }
        }
        return false;
    }

    public static void CopiarAmeacasAtivas(List<MissileThreatTracker> destino)
    {
        if (destino == null) return;
        destino.Clear();

        for (int i = ameacasAtivas.Count - 1; i >= 0; i--)
        {
            MissileThreatTracker tracker = ameacasAtivas[i];
            if (tracker == null || tracker.RaizMissil == null || !tracker.RaizMissil.gameObject.activeInHierarchy)
            {
                ameacasAtivas.RemoveAt(i);
                continue;
            }

            destino.Add(tracker);
        }
    }

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
        pontoLancamento = raizMissil != null ? raizMissil.position : transform.position;
        momentoLancamento = Time.time;
        nomeOrigem = origem != null ? origem.name : "Origem desconhecida";
        alvoNome = alvoDinamico != null ? alvoDinamico.name : string.Empty;
        alvoTeam = ResolverTeam(alvoDinamico, alvoDinamico);
        interceptor = ehInterceptador;
        velocidadeEstimada = Mathf.Max(velocidade, EstimarVelocidade(raizMissil.gameObject), 1f);
        teamOrigem = ResolverTeam(origem, raizMissil);
        float tempoVidaRastreamento = ResolverTempoVidaRastreamento(
            raizMissil != null ? raizMissil.gameObject : gameObject,
            tempoDeVidaInicial,
            out destruirMissilAoExpirar);
        CancelInvoke(nameof(ExpirarMissil));
        Invoke(nameof(ExpirarMissil), tempoVidaRastreamento);

        if (!registrado)
        {
            ameacasAtivas.Add(this);
            registrado = true;
            CartaCombateRegistro.RegistrarLancamento(this);
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
        CartaCombateRegistro.RegistrarMissilEncerrado(this);
        ameacasAtivas.Remove(this);
        registrado = false;
    }

    void ExpirarMissil()
    {
        RemoverRegistro();

        if (!destruirMissilAoExpirar)
        {
            return;
        }

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

    static float ResolverTempoVidaRastreamento(GameObject missil, float fallback, out bool destruirAoExpirar)
    {
        destruirAoExpirar = true;
        float tempoFallback = Mathf.Max(0.5f, fallback);

        if (missil == null)
        {
            return tempoFallback;
        }

        MisselNaval misselNaval = missil.GetComponent<MisselNaval>();
        if (misselNaval != null)
        {
            destruirAoExpirar = false;
            return Mathf.Max(0.5f, misselNaval.tempoMaximoVida + 0.5f);
        }

        MisselCaca misselCaca = missil.GetComponent<MisselCaca>();
        if (misselCaca != null)
        {
            destruirAoExpirar = false;
            return Mathf.Max(0.5f, misselCaca.tempoMaximoVida + 0.5f);
        }

        MisselSubmarino misselSubmarino = missil.GetComponent<MisselSubmarino>();
        if (misselSubmarino != null)
        {
            destruirAoExpirar = false;
            return Mathf.Max(0.5f, misselSubmarino.tempoMaximoVida + 0.5f);
        }

        MisselBombardeiro misselBombardeiro = missil.GetComponent<MisselBombardeiro>();
        if (misselBombardeiro != null)
        {
            destruirAoExpirar = false;
            return Mathf.Max(0.5f, misselBombardeiro.tempoDeVida + 0.5f);
        }

        MissilTeleguiado missilTeleguiado = missil.GetComponent<MissilTeleguiado>();
        if (missilTeleguiado != null)
        {
            destruirAoExpirar = false;
            return Mathf.Max(0.5f, missilTeleguiado.tempoDeVida + 0.5f);
        }

        Projetil projetil = missil.GetComponent<Projetil>();
        if (projetil != null)
        {
            destruirAoExpirar = false;
            return Mathf.Max(0.5f, projetil.tempoDeVida + 0.5f);
        }

        MisselTatico misselTatico = missil.GetComponent<MisselTatico>();
        if (misselTatico != null)
        {
            destruirAoExpirar = false;
            return 15.5f;
        }

        MisselICBM misselIcbm = missil.GetComponent<MisselICBM>();
        if (misselIcbm != null)
        {
            return Mathf.Max(tempoFallback, 30f);
        }

        MisselLeopardAutomatico misselLeopard = missil.GetComponent<MisselLeopardAutomatico>();
        if (misselLeopard != null)
        {
            return Mathf.Max(tempoFallback, 20f);
        }

        return tempoFallback;
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
