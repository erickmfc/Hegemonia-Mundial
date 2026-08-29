using System.Collections;
using System.Collections.Generic;
using Hegemonia.AI.IA01;
using UnityEngine;

/// <summary>
/// Míssil estratégico de longo alcance lançado por silo fixo.
/// A trajetória é dividida em impulso, cruzeiro e terminal para permitir
/// ajuste visual e de balanceamento no Inspector.
/// </summary>
public class MisselEstrategicoLongoAlcance : MonoBehaviour
{
    // Guiagem terminal corrigida para não perder o ponto de impacto.
    public enum FaseVoo { Preparacao, Impulso, Cruzeiro, Terminal, Impacto, Explosao }

    [Header("Desempenho")]
    public float velocidadeMaxima = 180f;
    public float aceleracao = 45f;
    public float velocidadeGiro = 32f;
    public float alturaCruzeiro = 850f;
    public float alturaTerminal = 120f;
    [Tooltip("Distância horizontal a partir da qual o míssil inicia a descida terminal.")]
    public float distanciaTerminalHorizontal = 1200f;
    public float tempoImpulso = 8f;
    public float tempoSeparacaoEstagios = 3.5f;
    public float tempoMaximoDeVoo = 180f;

    [Header("Guiagem e impacto")]
    [Tooltip("Distância máxima para considerar que o míssil atingiu o alvo. Também cobre o caso em que ele passa do alvo entre dois frames.")]
    public float distanciaImpactoTerminal = 24f;

    [Header("Carga")]
    public bool cargaNuclear = false;
    public float raioExplosaoConvencional = 35f;
    public float raioExplosaoNuclear = 220f;
    public int danoConvencional = 1200;
    public int danoNuclear = 12000;
    public float atrasoExplosaoImpacto = 0f;
    public float duracaoEfeitoExplosao = 12f;
    public float escalaEfeitoConvencional = 8f;
    public float escalaEfeitoNuclear = 28f;
    public GameObject efeitoExplosaoConvencional;
    public GameObject efeitoExplosaoNuclear;
    [Tooltip("Efeito usado quando a cena não possui GerenciadorFXGlobal configurado.")]
    public GameObject efeitoExplosaoFallback;
    public AudioClip somExplosao;

    [Header("Estágios visuais")]
    [Tooltip("Partes que serão ocultadas progressivamente durante a separação.")]
    public GameObject[] estagios = new GameObject[0];

    public FaseVoo faseAtual = FaseVoo.Preparacao;
    public Vector3 Alvo => alvo;
    public float TempoDeVoo => tempoDeVoo;
    public bool CargaNuclear => cargaNuclear;

    private Vector3 alvo;
    private Transform alvoTransform;
    private Component origem;
    private bool lancado;
    private bool explodiu;
    private float tempoDeVoo;
    private float velocidadeAtual;
    private readonly Collider[] bufferExplosao = new Collider[256];
    private static readonly HashSet<int> alvosProcessados = new HashSet<int>();

    public void IniciarLancamento(Vector3 pontoAlvo, bool nuclear, Component lancador = null)
    {
        IniciarLancamento(pontoAlvo, nuclear, lancador, null);
    }

    /// <summary>
    /// Mantém o alvo móvel como referência viva. A antecipação serve apenas
    /// para orientar a trajetória; a explosão continua condicionada à
    /// posição real que o míssil percorreu.
    /// </summary>
    public void IniciarLancamento(
        Vector3 pontoAlvo,
        bool nuclear,
        Component lancador,
        Transform alvoMovel)
    {
        // Evita que um clique em uma posição inválida deixe o míssil sem destino.
        alvo = SanitizarPontoAlvo(pontoAlvo);
        alvoTransform = alvoMovel;
        origem = lancador;
        cargaNuclear = nuclear;
        lancado = true;
        explodiu = false;
        tempoDeVoo = 0f;
        velocidadeAtual = 0f;
        faseAtual = FaseVoo.Impulso;

        // O ponto de saída pode ter qualquer rotação. Começar apontando para
        // cima evita a guinada errática nos primeiros frames do lançamento.
        transform.rotation = RotacaoSegura(Vector3.up);

        PrepararFisica();
        ConfigurarTagMissil();
        MissileThreatTracker.RegistrarLancamento(gameObject, origem, alvo, alvoTransform, velocidadeMaxima);
    }

    private void PrepararFisica()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    private void ConfigurarTagMissil()
    {
        try { gameObject.tag = "Missel"; } catch (UnityException) { }
    }

    private void Update()
    {
        if (!lancado || explodiu) return;

        float delta = Mathf.Max(0.0001f, Time.deltaTime);
        tempoDeVoo += delta;
        if (alvoTransform != null && alvoTransform.gameObject.activeInHierarchy)
        {
            alvo = SanitizarPontoAlvo(alvoTransform.position);
        }
        Vector3 posicaoAnterior = transform.position;
        Vector3 direcao = ResolverDirecaoDaFase();
        if (direcao.sqrMagnitude < 0.001f) direcao = transform.forward;

        velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeMaxima, aceleracao * delta);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            RotacaoSegura(direcao),
            velocidadeGiro * delta);

        Vector3 posicaoSeguinte = posicaoAnterior + transform.forward * (velocidadeAtual * delta);
        bool cruzouAlvo = faseAtual == FaseVoo.Terminal && SegmentoAtingeAlvo(posicaoAnterior, posicaoSeguinte);

        // O alvo só é usado como ponto de impacto quando o segmento realmente
        // o alcançou. Expirar não pode teletransportar o míssil nem fabricar
        // uma explosão em coordenada diferente da trajetória percorrida.
        if (cruzouAlvo)
        {
            // O segmento já confirmou o impacto; preservar a posição real da
            // trajetória evita um salto visual até uma coordenada antiga.
            IniciarImpacto();
            return;
        }

        if (tempoDeVoo >= tempoMaximoDeVoo)
        {
            // Expirar longe do alvo é uma falha controlada, nunca um impacto
            // falso no ponto em que o míssil conseguiu chegar.
            EncerrarSemImpacto();
            return;
        }

        transform.position = posicaoSeguinte;

        if (faseAtual == FaseVoo.Terminal && Vector3.Distance(transform.position, alvo) <= distanciaImpactoTerminal)
        {
            IniciarImpacto();
        }
    }

    private Vector3 ResolverDirecaoDaFase()
    {
        if (tempoDeVoo < tempoImpulso)
        {
            faseAtual = FaseVoo.Impulso;
            return Vector3.Lerp(Vector3.up, (alvo - transform.position).normalized, tempoDeVoo / Mathf.Max(tempoImpulso, 0.1f) * 0.2f);
        }

        if (tempoDeVoo < tempoImpulso + tempoSeparacaoEstagios)
        {
            faseAtual = FaseVoo.Cruzeiro;
            SepararEstagio(0);
        }

        Vector3 pontoCruzeiro = alvo;
        pontoCruzeiro.y += alturaCruzeiro;
        float distancia = Vector3.Distance(transform.position, alvo);
        float distanciaHorizontal = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(alvo.x, 0f, alvo.z));
        float alturaCruzeiroAlvo = alvo.y + alturaCruzeiro;
        bool atingiuAltitudeDeCruzeiro = transform.position.y >= alturaCruzeiroAlvo - Mathf.Max(alturaCruzeiro * 0.12f, 25f);
        bool chegouAoPontoDeCruzeiro = Vector3.Distance(transform.position, pontoCruzeiro) <= Mathf.Max(alturaCruzeiro * 0.35f, 100f);
        bool prontoParaDescida = atingiuAltitudeDeCruzeiro &&
            distanciaHorizontal <= Mathf.Max(distanciaTerminalHorizontal, 250f);

        // A descida não depende somente da distância 3D ao alvo. Isso evita
        // que o míssil passe do topo, fique corrigindo a rota no alto e nunca
        // entre na fase terminal.
        if (prontoParaDescida || chegouAoPontoDeCruzeiro || distancia < Mathf.Max(alturaCruzeiro * 1.2f, 250f))
        {
            faseAtual = FaseVoo.Terminal;
            Vector3 pontoTerminal = alvo;
            pontoTerminal.y += Mathf.Clamp(distanciaHorizontal * 0.15f, 8f, alturaTerminal);
            return pontoTerminal - transform.position;
        }

        faseAtual = FaseVoo.Cruzeiro;
        return pontoCruzeiro - transform.position;
    }

    private Vector3 SanitizarPontoAlvo(Vector3 ponto)
    {
        if (float.IsNaN(ponto.x) || float.IsNaN(ponto.y) || float.IsNaN(ponto.z) ||
            float.IsInfinity(ponto.x) || float.IsInfinity(ponto.y) || float.IsInfinity(ponto.z))
        {
            return transform.position + transform.forward * 100f;
        }
        return ponto;
    }

    private Quaternion RotacaoSegura(Vector3 direcao)
    {
        Vector3 frente = direcao.sqrMagnitude > 0.0001f ? direcao.normalized : Vector3.forward;
        // LookRotation não aceita frente e up paralelos; isso acontecia durante
        // a subida vertical e gerava giros/rolls imprevisíveis.
        Vector3 referenciaUp = Mathf.Abs(Vector3.Dot(frente, Vector3.up)) > 0.97f
            ? Vector3.forward
            : Vector3.up;
        return Quaternion.LookRotation(frente, referenciaUp);
    }

    private bool SegmentoAtingeAlvo(Vector3 inicio, Vector3 fim)
    {
        Vector3 segmento = fim - inicio;
        float comprimentoSqr = segmento.sqrMagnitude;
        if (comprimentoSqr < 0.0001f)
        {
            return Vector3.Distance(inicio, alvo) <= distanciaImpactoTerminal;
        }

        float t = Mathf.Clamp01(Vector3.Dot(alvo - inicio, segmento) / comprimentoSqr);
        Vector3 pontoMaisProximo = inicio + segmento * t;
        return Vector3.Distance(pontoMaisProximo, alvo) <= Mathf.Max(8f, distanciaImpactoTerminal);
    }

    private void SepararEstagio(int indice)
    {
        if (estagios == null || indice < 0 || indice >= estagios.Length) return;
        if (estagios[indice] != null) estagios[indice].SetActive(false);
    }

    private void IniciarImpacto()
    {
        if (explodiu) return;
        lancado = false;
        faseAtual = FaseVoo.Impacto;
        StartCoroutine(ExplodirDepoisDoAtraso());
    }

    private void EncerrarSemImpacto()
    {
        if (explodiu) return;
        lancado = false;
        faseAtual = FaseVoo.Impacto;
        Debug.LogWarning($"[MisselEstrategico] voo expirado sem impacto: pos={transform.position} alvo={alvo}", this);
        PoolDeObjetosCombate.Release(gameObject);
    }

    private IEnumerator ExplodirDepoisDoAtraso()
    {
        if (atrasoExplosaoImpacto > 0f) yield return new WaitForSeconds(atrasoExplosaoImpacto);
        Explodir();
    }

    private void Explodir()
    {
        if (explodiu) return;
        explodiu = true;
        faseAtual = FaseVoo.Explosao;

        float raio = cargaNuclear ? raioExplosaoNuclear : raioExplosaoConvencional;
        int dano = cargaNuclear ? danoNuclear : danoConvencional;
        GameObject efeito = cargaNuclear ? efeitoExplosaoNuclear : efeitoExplosaoConvencional;
        float escala = cargaNuclear ? escalaEfeitoNuclear : escalaEfeitoConvencional;

        if (efeito == null) efeito = efeitoExplosaoFallback;

        if (efeito != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(efeito, transform.position, Quaternion.identity,
                duracaoEfeitoExplosao, Vector3.one * escala);
        }
        else if (GerenciadorFXGlobal.Instancia != null)
        {
            // Os prefabs antigos não tinham o campo de FX preenchido. Use o
            // efeito global como fallback para que todo impacto seja visível.
            GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", transform.position, escala);
        }

        if (somExplosao != null)
        {
            GameObject audioObj = new GameObject("SomExplosaoMissilEstrategico");
            audioObj.transform.position = transform.position;
            AudioSource audio = audioObj.AddComponent<AudioSource>();
            audio.clip = somExplosao;
            audio.volume = 0.8f;
            audio.spatialBlend = 1f;
            audio.maxDistance = 500f; // permanece acima do requisito mÃ­nimo de 300 m
            audio.Play();
            Destroy(audioObj, somExplosao.length + 0.5f);
        }

        alvosProcessados.Clear();
        HashSet<int> paisesNotificados = new HashSet<int>();
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raio, bufferExplosao, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider col = bufferExplosao[i];
            if (col == null) continue;
            SistemaDeDanos vida = col.GetComponent<SistemaDeDanos>() ?? col.GetComponentInParent<SistemaDeDanos>();
            IdentidadeUnidade identidadeVitima = vida != null ? SistemaDeDanos.ResolverIdentidade(vida) : null;
            if (identidadeVitima != null && paisesNotificados.Add(identidadeVitima.teamID))
            {
                NotificarImpactoEstrategico(identidadeVitima.teamID, transform.position, dano);
            }
            if (vida != null && alvosProcessados.Add(vida.GetInstanceID()))
            {
                vida.ReceberDano(dano, origem != null ? origem.gameObject : gameObject);
            }

            Rigidbody rb = col.attachedRigidbody;
            if (rb != null) rb.AddExplosionForce(cargaNuclear ? 6500f : 2200f, transform.position, raio, 4f);
        }

        // Algumas estruturas importadas (GLB) não têm collider, embora tenham
        // SistemaDeDanos. Incluí-las aqui evita explosão visual sem dano.
        SistemaDeDanos[] danosSemCollider = FindObjectsByType<SistemaDeDanos>(FindObjectsSortMode.None);
        for (int i = 0; i < danosSemCollider.Length; i++)
        {
            SistemaDeDanos vida = danosSemCollider[i];
            if (vida == null || !vida.isActiveAndEnabled || alvosProcessados.Contains(vida.GetInstanceID())) continue;
            if (Vector3.Distance(vida.transform.position, transform.position) > raio) continue;

            IdentidadeUnidade identidadeVitima = SistemaDeDanos.ResolverIdentidade(vida);
            if (identidadeVitima != null && paisesNotificados.Add(identidadeVitima.teamID))
            {
                NotificarImpactoEstrategico(identidadeVitima.teamID, transform.position, dano);
            }
            alvosProcessados.Add(vida.GetInstanceID());
            vida.ReceberDano(dano, origem != null ? origem.gameObject : gameObject);
        }

        Debug.Log($"[MisselEstrategico] impacto confirmado pos={transform.position} alvo={alvo} raio={raio:F0} dano={dano} colliders={hits}", this);

        PoolDeObjetosCombate.Release(gameObject);
    }

    private void NotificarImpactoEstrategico(int paisVitima, Vector3 impacto, float dano)
    {
        IA01Manager manager = IA01Manager.Instancia;
        if (manager == null) return;

        MissileThreatTracker tracker = GetComponent<MissileThreatTracker>();
        int paisSuspeito = tracker != null ? tracker.TeamOrigem : 0;
        Vector3 areaProvavel = origem != null ? origem.transform.position : impacto;
        for (int i = 0; i < manager.Controllers.Count; i++)
        {
            IA01Controller controller = manager.Controllers[i];
            if (controller == null || controller.TeamId != paisVitima || !controller.StrategicOptions.BallisticEnabled) continue;
            controller.RegisterBallisticImpact(
                impacto,
                impacto,
                (impacto - areaProvavel).normalized,
                areaProvavel,
                cargaNuclear ? IA01BallisticMissileType.Nuclear : IA01BallisticMissileType.Conventional,
                cargaNuclear ? IA01BallisticWarheadType.Nuclear : IA01BallisticWarheadType.Conventional,
                1,
                dano,
                "infraestrutura atingida",
                paisSuspeito,
                0,
                Vector3.zero,
                false,
                0.2f);
        }
    }
}
