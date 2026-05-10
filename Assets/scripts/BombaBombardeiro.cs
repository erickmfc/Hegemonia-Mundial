using UnityEngine;

/// <summary>
/// BOMBA BALÍSTICA DO BOMBARDEIRO
/// ────────────────────────────────
/// Cai com gravidade real após ser soltada pelo AviaoBombardeiro.
/// Pode ter guiagem terminal opcional (bomba inteligente / laser-guided).
/// Totalmente editável no Inspector do prefab.
/// </summary>
public class BombaBombardeiro : MonoBehaviour
{
    // =========================================================
    //  SEÇÃO 1: FÍSICA DE QUEDA
    // =========================================================
    [Header("── Física de Queda ─────────────────────")]
    [Tooltip("Velocidade horizontal herdada do avião ao ser soltada (m/s). O bombardeiro vai preencher isso automaticamente — você pode ajustar no prefab para testes.")]
    public float velocidadeHorizontalInicial = 180f;

    [Tooltip("Múltiplo da gravidade do Unity. 1 = gravidade real. 2 = cai mais rápido (mais dramatismo).")]
    public float multiplicadorGravidade = 1.5f;

    [Tooltip("Velocidade máxima de queda, para a bomba não ficar absurda.")]
    public float velocidadeTerminalQueda = 280f;

    // =========================================================
    //  SEÇÃO 2: GUIAGEM TERMINAL (BOMBA INTELIGENTE)
    // =========================================================
    [Header("── Guiagem Terminal (opcional) ─────────")]
    [Tooltip("Se verdadeiro, ativa as aletas de guiagem: a bomba corrige o curso para o alvo enquanto cai.")]
    public bool guiadaTerminal = false;

    [Tooltip("Velocidade de rotação das aletas (graus/s). Só ativo se 'guiadaTerminal' estiver marcado.")]
    public float velocidadeAletas = 90f;

    [Tooltip("Distância vertical acima do alvo em que a guiagem terminal ativa (metros). A bomba corrige só na reta final.")]
    public float alturaInicioGuiagem = 80f;

    // =========================================================
    //  SEÇÃO 3: DETONAÇÃO
    // =========================================================
    [Header("── Detonação ───────────────────────────")]
    [Tooltip("Raio da explosão em metros.")]
    public float raioExplosao = 25f;

    [Tooltip("Dano total no epicentro da explosão.")]
    public int danoMaximo = 350;

    [Tooltip("Se verdadeiro, o dano diminui com a distância até o epicentro.")]
    public bool danoComFalloff = true;

    [Tooltip("Tipo de detonação: 'Impacto' = explode ao tocar algo. 'Penetrante' = penetra X metros antes de explodir.")]
    public TipoDetonacao tipoDetonacao = TipoDetonacao.Impacto;

    [Tooltip("(Só para Penetrante) Profundidade de penetração antes de explodir (metros).")]
    public float profundidadePenetracao = 5f;

    // =========================================================
    //  SEÇÃO 4: EFEITOS
    // =========================================================
    [Header("── Efeitos ─────────────────────────────")]
    [Tooltip("Prefab de explosão. (Deixe vazio para usar o FX global do jogo)")]
    public GameObject efeitoExplosao;

    [Tooltip("Som de explosão.")]
    public AudioClip somExplosao;

    [Tooltip("Escala do efeito de explosão.")]
    public float escalaVisualExplosao = 3f;

    [Tooltip("Emite uma nuvem de fumaça de queda? Se quiser rastro visual enquanto cai.")]
    public ParticleSystem fumaçaDeQueda;

    // =========================================================
    //  ESTADO INTERNO
    // =========================================================
    public enum TipoDetonacao { Impacto, Penetrante }

    private Vector3 velocidade;          // Vetor de velocidade 3D acumulado
    private Vector3 alvoFinal;           // O ponto GPS do alvo (preenchido pelo AviaoBombardeiro)
    private bool ativa = false;
    private bool explodiu = false;
    private GameObject dono;
    private float penetracaoPercorrida = 0f;
    private bool emPenetracao = false;
    private readonly Collider[] bufferExplosao = new Collider[96];

    // ──────────────────────────────────────────────────────────
    //  API PÚBLICA
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Chamado pelo AviaoBombardeiro no momento de soltar a bomba.
    /// velHorizontal = velocidade do avião no instante do lançamento (vetor XZ).
    /// pontoAlvo     = coordenada GPS calculada para o impacto (pode ser Vector3.zero se for bomba burra).
    /// </summary>
    public void IniciarQueda(Vector3 velHorizontal, Vector3 pontoAlvo, GameObject quemSoltou = null)
    {
        // Herda a velocidade horizontal do avião
        velocidade = velHorizontal;
        velocidade.y = 0f; // No momento do soltar, velocidade vertical é zero

        alvoFinal = pontoAlvo;
        dono      = quemSoltou;
        ativa     = true;
        explodiu  = false;
        emPenetracao = false;
        penetracaoPercorrida = 0f;
        CancelInvoke(nameof(AtivarColisao));
        CancelInvoke(nameof(AutodestruirPorTempo));

        // Separa do pai (o avião) para cair livremente
        transform.SetParent(null);

        if (fumaçaDeQueda != null) fumaçaDeQueda.Play();

        // Desativa colisão por meio segundo para não bater no próprio avião
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        Invoke(nameof(AtivarColisao), 0.5f);

        // Autodestruição de segurança: se cair no mar ou falhar, some em 30s
        Invoke(nameof(AutodestruirPorTempo), 30f);
    }

    /// <summary>
    /// Versão simplificada: usa a velocidade horizontal padrão configurada no prefab.
    /// Útil quando o AviaoBombardeiro não passa a velocidade exata.
    /// </summary>
    public void IniciarLancamento(Vector3 pontoAlvo, GameObject quemSoltou = null)
    {
        Vector3 velHoriz = transform.forward * velocidadeHorizontalInicial;
        IniciarQueda(velHoriz, pontoAlvo, quemSoltou);
    }

    public void SetDono(GameObject quem) { dono = quem; }

    // ──────────────────────────────────────────────────────────

    void AtivarColisao()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    void Update()
    {
        if (!ativa || explodiu) return;

        float dt = Time.deltaTime;

        // ─── FÍSICA: GRAVIDADE REAL ───────────────────────────
        velocidade.y -= Physics.gravity.magnitude * multiplicadorGravidade * dt;

        // Limita velocidade de queda (terminal velocity)
        if (velocidade.y < -velocidadeTerminalQueda)
            velocidade.y = -velocidadeTerminalQueda;

        // ─── GUIAGEM TERMINAL (aletas) ────────────────────────
        if (guiadaTerminal && alvoFinal != Vector3.zero)
        {
            float alturaAtual   = transform.position.y;
            float alturaAlvo    = alvoFinal.y;
            float alturaRelativa = alturaAtual - alturaAlvo;

            if (alturaRelativa <= alturaInicioGuiagem)
            {
                Vector3 dir = (alvoFinal - transform.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion rotAlvo = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, velocidadeAletas * dt);

                    // Redireciona a velocidade ligeiramente na direção correta
                    Vector3 velDesejada = dir * velocidade.magnitude;
                    velocidade = Vector3.Lerp(velocidade, velDesejada, velocidadeAletas * 0.005f * dt);
                }
            }
        }

        // Orientação visual: aponta na direção do movimento SOMENTE se NÃO estiver com guiagem terminal ativa
        // (quando guiagem está ativa, a rotação já é controlada pelas aletas acima)
        bool guiagemAtivaAgora = guiadaTerminal && alvoFinal != Vector3.zero && 
                                  (transform.position.y - alvoFinal.y) <= alturaInicioGuiagem;
        if (!guiagemAtivaAgora && velocidade.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.LookRotation(velocidade.normalized);

        // Movimento
        transform.position += velocidade * dt;

        // ─── MODO PENETRANTE ──────────────────────────────────
        if (emPenetracao)
        {
            penetracaoPercorrida += velocidade.magnitude * dt;
            if (penetracaoPercorrida >= profundidadePenetracao)
                Explodir();
        }
    }

    // ─── COLISÃO ─────────────────────────────────────────────

    void OnCollisionEnter(Collision col)
    {
        if (!ativa || explodiu) return;
        if (dono != null && col.gameObject.transform.IsChildOf(dono.transform)) return;

        if (tipoDetonacao == TipoDetonacao.Impacto)
        {
            Explodir();
        }
        else // Penetrante: continua se movendo por mais X metros antes de explodir
        {
            // Desativa colisão para penetrar (atravessa fisicamente)
            Collider c = GetComponent<Collider>();
            if (c != null) c.enabled = false;
            emPenetracao = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!ativa || explodiu) return;
        if (dono != null && other.gameObject.transform.IsChildOf(dono.transform)) return;
        if (tipoDetonacao == TipoDetonacao.Impacto) Explodir();
    }

    // ─── EXPLOSÃO ────────────────────────────────────────────

    private void Explodir()
    {
        if (explodiu) return;
        explodiu = true;
        ativa    = false;
        CancelInvoke(nameof(AutodestruirPorTempo));

        if (fumaçaDeQueda != null) fumaçaDeQueda.Stop();

        // Efeito Visual
        if (efeitoExplosao != null)
        {
            PoolDeObjetosCombate.SpawnTemporario(
                efeitoExplosao,
                transform.position,
                Quaternion.identity,
                5f,
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
        int hits = Physics.OverlapSphereNonAlloc(transform.position, raioExplosao, bufferExplosao, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Collider h = bufferExplosao[i];
            if (h == null || h.isTrigger) continue;
            if (dono != null && h.transform.IsChildOf(dono.transform)) continue;

            SistemaDeDanos vida = h.GetComponent<SistemaDeDanos>() ?? h.GetComponentInParent<SistemaDeDanos>();
            if (vida != null)
            {
                int danoFinal = danoMaximo;
                if (danoComFalloff)
                {
                    float dist  = Vector3.Distance(transform.position, h.transform.position);
                    float fator = Mathf.Clamp01(1f - (dist / raioExplosao));
                    danoFinal   = Mathf.RoundToInt(danoMaximo * fator);
                }
                vida.ReceberDano(danoFinal);
            }

            Rigidbody rb = h.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
                rb.AddExplosionForce(danoMaximo * 3f, transform.position, raioExplosao, 2f);
        }

        PoolDeObjetosCombate.Release(gameObject);
    }

    private void AutodestruirPorTempo()
    {
        if (explodiu) return;
        explodiu = true;
        ativa = false;
        if (fumaçaDeQueda != null) fumaçaDeQueda.Stop();
        PoolDeObjetosCombate.Release(gameObject);
    }
}
