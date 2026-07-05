using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// MÍSSIL AR-TERRA DO BOMBARDEIRO
/// ──────────────────────────────
/// Lançado pelo AviaoBombardeiro, pode perseguir um alvo ESTÁTICO ou MÓVEL.
/// Voa em arco para sair da sombra do avião e depois mergulha no alvo com precisão.
/// Totalmente editável no Inspector do prefab.
/// </summary>
public class MisselBombardeiro : MonoBehaviour
{
    // =========================================================
    //  SEÇÃO 1: MOTOR / VOO
    // =========================================================
    [Header("── Motor / Velocidade ──────────────────")]
    [Tooltip("Velocidade inicial ao sair da barriga do bombardeiro (m/s).")]
    public float velocidadeInicial = 80f;

    [Tooltip("Velocidade máxima no mergulho final (m/s). Quanto maior, mais difícil de interceptar.")]
    public float velocidadeMaxima = 350f;

    [Tooltip("Aceleração após o boost inicial (m/s²).")]
    public float aceleracao = 120f;

    // =========================================================
    //  SEÇÃO 2: GUIAGEM
    // =========================================================
    [Header("── Guiagem ─────────────────────────────")]
    [Tooltip("Velocidade de rotação do míssil em graus por segundo (curva). Valores baixos = míssil lerdo de curvar; altos = teleguiado agressivo.")]
    public float velocidadeDeGiro = 180f;

    [Tooltip("Altura máxima do arco de saída antes de mergulhar no alvo (metros acima do ponto de lançamento).")]
    public float alturaDoArco = 60f;

    [Tooltip("Distância horizontal a partir do qual ele para de subir e começa a mergulhar no alvo.")]
    public float distanciaMergulho = 200f;

    [Tooltip("Tempo em segundos antes da guiagem ativar. Evita que acerte o próprio avião.")]
    public float atrasoGuiagem = 0.6f;

    [Tooltip("Se verdadeiro, o míssil lê a posição atual do alvo a cada frame (para alvos em movimento).")]
    public bool rastrearAlvoMovel = true;

    // =========================================================
    //  SEÇÃO 3: TRIAGEM / PROXIMIDADE
    // =========================================================
    [Header("── Detonação ───────────────────────────")]
    [Tooltip("Detonará automaticamente quando estiver a esta distância do alvo (metros). Previne passagem pelo alvo em alta velocidade.")]
    public float fusilProximidade = 8f;

    [Tooltip("Tempo máximo de vida do míssil até se autodestruir (segundos).")]
    public float tempoDeVida = 20f;

    // =========================================================
    //  SEÇÃO 4: DANO E EXPLOSÃO
    // =========================================================
    [Header("── Explosão / Dano ──────────────────────")]
    [Tooltip("Raio da explosão em metros. Tudo dentro desse raio recebe dano.")]
    public float raioExplosao = 18f;

    [Tooltip("Dano total no centro da explosão.")]
    public int danoMaximo = 400;

    [Tooltip("Se verdadeiro, o dano diminui com a distância. Se falso, tudo no raio recebe dano total.")]
    public bool danoComFalloff = true;

    [Tooltip("Prefab de efeito de explosão a instanciar no impacto. (Deixe vazio para usar o FX global do jogo)")]
    public GameObject efeitoExplosao;

    [Tooltip("Som de explosão a tocar no impacto.")]
    public AudioClip somExplosao;

    [Tooltip("Escala do efeito visual de explosão.")]
    public float escalaVisualExplosao = 2.5f;

    // =========================================================
    //  SEÇÃO 5: TRAIL / FUMAÇA VISUAL
    // =========================================================
    [Header("── Visual / Rastro ──────────────────────")]
    [Tooltip("TrailRenderer para o rastro de fumaça/fogo do motor (opcional).")]
    public TrailRenderer rastroFumo;

    // =========================================================
    //  ESTADO INTERNO (não editar)
    // =========================================================
    private Vector3 alvoFixo;
    private Transform alvoMovel;
    private bool lancado = false;
    private float tempoVivo = 0f;
    private float velocidadeAtual;
    private bool emArco = true;    // Fase 1: subir no arco
    private GameObject dono;
    private readonly Collider[] bufferExplosao = new Collider[96];
    private static readonly HashSet<int> alvosProcessados = new HashSet<int>();

    // ──────────────────────────────────────────────────────────
    //  API PÚBLICA (chamada pelo AviaoBombardeiro)
    // ──────────────────────────────────────────────────────────

    /// <summary>Lança contra um ponto estático no mundo (ex: coordenada clicada no mapa).</summary>
    public void IniciarLancamento(Vector3 pontoAlvo, GameObject quemLancou = null)
    {
        alvoFixo  = pontoAlvo;
        alvoMovel = null;
        dono      = quemLancou;
        IniciarVoo();
    }

    /// <summary>Lança contra uma unidade móvel (o míssil vai corrigir a rota a cada frame).</summary>
    public void IniciarLancamentoRastreado(Transform alvomovendo, GameObject quemLancou = null)
    {
        alvoMovel = alvomovendo;
        alvoFixo  = alvomovendo != null ? alvomovendo.position : transform.position + transform.forward * 300f;
        dono      = quemLancou;
        IniciarVoo();
    }

    public void SetDono(GameObject quem) { dono = quem; }

    // ──────────────────────────────────────────────────────────

    private void IniciarVoo()
    {
        StopAllCoroutines();
        CancelInvoke(nameof(AutodestruirPorTempo));
        velocidadeAtual = velocidadeInicial;
        lancado         = true;
        tempoVivo       = 0f;
        emArco          = true;

        // Desativa colisão por um momento para não bater no próprio avião
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            StartCoroutine(ReativarColisao(atrasoGuiagem));
        }

        // Registra no tracker de ameaças do jogo base (se existir)
        MissileThreatTracker.RegistrarLancamento(gameObject, this, alvoFixo, alvoMovel, velocidadeMaxima);

        Invoke(nameof(AutodestruirPorTempo), tempoDeVida);
    }

    private IEnumerator ReativarColisao(float delay)
    {
        yield return new WaitForSeconds(delay);
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    void Update()
    {
        if (!lancado) return;
        tempoVivo += Time.deltaTime;
        if (tempoVivo < atrasoGuiagem) 
        {
            // Fase de saída: apenas empurra reto pra frente (sai da sombra do avião)
            transform.position += transform.forward * velocidadeAtual * Time.deltaTime;
            return;
        }

        // Atualiza posição do alvo móvel a cada frame
        Vector3 pontoAlvoAtual = (rastrearAlvoMovel && alvoMovel != null) ? alvoMovel.position : alvoFixo;

        // ─── FASE 1: ARCO DE SUBIDA ──────────────────────────
        if (emArco)
        {
            // Aponta para o ponto alto acima do alvo
            Vector3 pontoAlto = new Vector3(pontoAlvoAtual.x, pontoAlvoAtual.y + alturaDoArco, pontoAlvoAtual.z);
            float distHoriz = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(pontoAlvoAtual.x, 0, pontoAlvoAtual.z)
            );

            // Transição: quando fica perto o suficiente ou subiu alto o suficiente, mergulha
            bool subioSuficiente  = transform.position.y >= pontoAlvoAtual.y + alturaDoArco * 0.7f;
            bool pertoDemais      = distHoriz < distanciaMergulho;

            if (subioSuficiente || pertoDemais)
            {
                emArco = false;
            }
            else
            {
                GirarPara(pontoAlto);
            }
        }
        // ─── FASE 2: MERGULHO PRECISO NO ALVO ────────────────
        else
        {
            GirarPara(pontoAlvoAtual);

            // Fusil de proximidade: detona antes de mais nada
            float distAlvo = Vector3.Distance(transform.position, pontoAlvoAtual);
            if (distAlvo < fusilProximidade)
            {
                Detonar();
                return;
            }
        }

        // Acelera progressivamente
        velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeMaxima, aceleracao * Time.deltaTime);
        transform.position += transform.forward * velocidadeAtual * Time.deltaTime;
    }

    private void GirarPara(Vector3 destino)
    {
        Vector3 dir = (destino - transform.position);
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion rotAlvo = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, velocidadeDeGiro * Time.deltaTime);
    }

    void OnCollisionEnter(Collision col)
    {
        if (!lancado || tempoVivo < atrasoGuiagem) return;
        if (dono != null && col.gameObject.transform.IsChildOf(dono.transform)) return;
        Detonar();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!lancado || tempoVivo < atrasoGuiagem) return;
        if (dono != null && other.gameObject.transform.IsChildOf(dono.transform)) return;
        Detonar();
    }

    private void Detonar()
    {
        if (!lancado) return;
        lancado = false;
        CancelInvoke(nameof(AutodestruirPorTempo));

        // Efeito visual
        if (efeitoExplosao != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(
                efeitoExplosao,
                transform.position,
                Quaternion.identity,
                4f,
                Vector3.one * escalaVisualExplosao);
        }
        else if (GerenciadorFXGlobal.Instancia != null)
        {
            GerenciadorFXGlobal.Instancia.TocarEfeito("Explosao", transform.position, escalaVisualExplosao);
        }

        // Som
        if (somExplosao != null)
            AudioSource.PlayClipAtPoint(somExplosao, transform.position);

        // Dano em área
        alvosProcessados.Clear();
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, bufferExplosao, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider h = bufferExplosao[i];
            if (h == null || h.isTrigger) continue;
            if (dono != null && h.transform.IsChildOf(dono.transform)) continue;

            SistemaDeDanos vida = h.GetComponent<SistemaDeDanos>() ?? h.GetComponentInParent<SistemaDeDanos>();
            if (vida != null)
            {
                int idVida = vida.GetInstanceID();
                if (!alvosProcessados.Add(idVida)) continue;
                int danoFinal = danoMaximo;
                if (danoComFalloff)
                {
                    float dist    = Vector3.Distance(transform.position, h.transform.position);
                    float fator   = Mathf.Clamp01(1f - (dist / raioExplosao));
                    danoFinal     = Mathf.RoundToInt(danoMaximo * fator);
                }
                vida.ReceberDano(danoFinal);
            }

            // Empurrão físico (opcional, se tiver RigidBody)
            Rigidbody rb = h.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
                rb.AddExplosionForce(danoMaximo * 2f, transform.position, raioExplosao, 1f);
        }

        PoolDeObjetosCombate.Release(gameObject);
    }

    private void AutodestruirPorTempo()
    {
        if (!lancado) return;
        lancado = false;
        PoolDeObjetosCombate.Release(gameObject);
    }
}
