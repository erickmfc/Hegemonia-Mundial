using System.Collections.Generic;
using UnityEngine;

public class SistemaAntiMissil : MonoBehaviour
{
    [Header("Radar & Alcance")]
    [Tooltip("Raio de deteccao do radar.")]
    public float alcanceRadar = 180f;

    [Tooltip("Tempo em segundos entre cada varredura.")]
    public float tempoDeEscaneamento = 0.3f;

    [Header("Deteccao Simples")]
    [Tooltip("Velocidade minima para considerar um objeto como ameaca.")]
    public float velocidadeMinimaAmeaca = 5f;

    [Tooltip("Dot product minimo para considerar que o objeto esta vindo na nossa direcao.")]
    public float dotMinimoAmeaca = 0.4f;

    [Header("Antecipacao de Lancamento")]
    [Tooltip("Usa o rastreador global de misseis para antecipar ameacas antes de entrarem no radar fisico.")]
    public bool usarRastroLancamento = true;

    [Tooltip("Permite antecipar trajetorias um pouco antes do missil entrar no alcance real.")]
    public float multiplicadorAntecipacaoAlcance = 1.35f;

    [Tooltip("Janela maxima em segundos para prever se um missil vai cruzar a area defendida.")]
    public float janelaAntecipacaoSegundos = 6f;

    [Header("Mecanica da Torreta")]
    [Tooltip("Base que gira para os lados (Yaw).")]
    public Transform baseGiratoria;

    [Tooltip("Peca que vira para cima/baixo (Pitch).")]
    public Transform canoElevacao;

    public float velocidadeGiro = 60f;

    [Header("Sistema de Disparo")]
    [Tooltip("Prefab do missil que vai abater o outro missil.")]
    public GameObject prefabIntercepador;

    [Tooltip("Pontos de onde o interceptador sai.")]
    public Transform[] pontosDeSaida;

    [Tooltip("Cadencia de tiro entre interceptadores.")]
    public float tempoEntreTiros = 0.8f;

    [Tooltip("Quantidade de misseis prontos antes da recarga.")]
    public int capacidadeMisseis = 10;

    public float tempoRecargaMisseis = 5f;

    [Header("Efeitos & Sons")]
    public AudioClip somDisparo;

    [Header("Comportamento")]
    [Tooltip("Se ativado, o sistema nao intercepta misseis automaticamente.")]
    public bool modoPassivo = false;

    private AudioSource audioSource;
    private Transform alvoMissilAtual;
    private IdentidadeUnidade minhaIdentidade;
    private float cooldownDisparo = 0f;
    private int misseisAtuais;
    private bool recarregando = false;
    private int indexSaida = 0;
    private readonly Dictionary<Transform, Vector3> ultimasPosicoesAmeaca = new Dictionary<Transform, Vector3>();
    private readonly List<Transform> chavesAmeacaParaRemover = new List<Transform>();
    private static readonly Collider[] bufferAliados = new Collider[128];

    void Start()
    {
        misseisAtuais = capacidadeMisseis;

        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        if (minhaIdentidade == null)
        {
            minhaIdentidade = gameObject.AddComponent<IdentidadeUnidade>();
            minhaIdentidade.teamID = 1;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;

        InvokeRepeating(nameof(ProcurarAmeacaMisseis), Random.Range(0f, 0.5f), tempoDeEscaneamento);
    }

    void Update()
    {
        if (cooldownDisparo > 0f)
        {
            cooldownDisparo -= Time.deltaTime;
        }

        if (recarregando)
        {
            if (cooldownDisparo <= 0f)
            {
                misseisAtuais = capacidadeMisseis;
                recarregando = false;
            }
            return;
        }

        if (alvoMissilAtual != null)
        {
            if (!AlvoMissilAtualAindaEhValido())
            {
                alvoMissilAtual = null;
                return;
            }

            Mirar();

            if (cooldownDisparo <= 0f && MirouEmCheio() && misseisAtuais > 0)
            {
                AtirarInterceptador();
                misseisAtuais--;
                cooldownDisparo = tempoEntreTiros;

                if (misseisAtuais <= 0 && capacidadeMisseis > 0)
                {
                    recarregando = true;
                    cooldownDisparo = tempoRecargaMisseis;
                }
            }
        }
        else
        {
            ModoOcioso();
        }
    }

    void ModoOcioso()
    {
        if (baseGiratoria != null)
        {
            baseGiratoria.Rotate(0f, 30f * Time.deltaTime, 0f, Space.Self);
        }

        if (canoElevacao != null)
        {
            canoElevacao.localRotation = Quaternion.Lerp(canoElevacao.localRotation, Quaternion.identity, Time.deltaTime * 5f);
        }
    }

    void ProcurarAmeacaMisseis()
    {
        if (modoPassivo)
        {
            alvoMissilAtual = null;
            return;
        }

        LimparRastrosInvalidos();

        if (AlvoMissilAtualAindaEhValido()) return;

        alvoMissilAtual = null;
        Transform minhaRaiz = transform.root != null ? transform.root : transform;

        Transform melhorAlvoRegistrado = BuscarAmeacaRegistrada(minhaRaiz);
        if (melhorAlvoRegistrado != null)
        {
            alvoMissilAtual = melhorAlvoRegistrado;
            return;
        }

        Collider[] objetosNaArea = Physics.OverlapSphere(
            transform.position,
            alcanceRadar,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        foreach (Collider col in objetosNaArea)
        {
            if (col == null) continue;

            Transform candidato = ResolverTransformoRaiz(col.transform);
            if (candidato == null) continue;
            if (candidato == minhaRaiz) continue;
            if (candidato.gameObject == gameObject) continue;

            if (!EhAmeacaSimples(candidato)) continue;

            float distancia = Vector3.Distance(transform.position, candidato.position);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                melhorAlvo = candidato;
            }
        }

        alvoMissilAtual = melhorAlvo;
    }

    Transform BuscarAmeacaRegistrada(Transform minhaRaiz)
    {
        if (!usarRastroLancamento) return null;

        int meuTime = minhaIdentidade != null ? minhaIdentidade.teamID : -1;
        Transform candidato = MissileThreatTracker.EncontrarAmeacaMaisProxima(
            transform.position,
            alcanceRadar,
            meuTime,
            minhaRaiz,
            multiplicadorAntecipacaoAlcance,
            janelaAntecipacaoSegundos);

        if (candidato == null) return null;
        return EhMissilInimigo(candidato) ? candidato : null;
    }

    // ── NOVO: verifica se o candidato pertence a um time inimigo ──────────────
    bool EhMissilInimigo(Transform candidato)
    {
        // Se o próprio sistema não tem identidade, trata tudo como ameaça
        if (minhaIdentidade == null) return true;

        // Procura IdentidadeUnidade na hierarquia do candidato
        IdentidadeUnidade idCandidato = candidato.GetComponentInChildren<IdentidadeUnidade>();
        if (idCandidato == null)
            idCandidato = candidato.GetComponentInParent<IdentidadeUnidade>();

        // Sem identidade no míssil → considera ameaça por precaução
        if (idCandidato == null) return true;

        // Só é inimigo se o teamID for diferente
        return idCandidato.teamID != minhaIdentidade.teamID;
    }
    // ─────────────────────────────────────────────────────────────────────────

    bool EhAmeacaSimples(Transform candidato)
    {
        if (candidato == null) return false;

        // Precisa ter a tag de míssil
        if (!PossuiTagMisselNaHierarquia(candidato)) return false;

        // ── NOVO: só mira em mísseis inimigos ────────────────────────────────
        if (!EhMissilInimigo(candidato)) return false;
        // ─────────────────────────────────────────────────────────────────────

        Vector3 velocidade = ObterVelocidadeAmeaca(candidato);
        float moduloVelocidade = velocidade.magnitude;
        if (moduloVelocidade <= velocidadeMinimaAmeaca)
        {
            Vector3 fallbackDirecao = candidato.forward;
            if (fallbackDirecao.sqrMagnitude <= 0.0001f) return false;

            Vector3 vetorFallback = transform.position - candidato.position;
            if (vetorFallback.sqrMagnitude <= 0.0001f) return true;

            float dotFallback = Vector3.Dot(fallbackDirecao.normalized, vetorFallback.normalized);
            return dotFallback >= dotMinimoAmeaca;
        }

        Vector3 direcaoVoo = velocidade / moduloVelocidade;
        Vector3 vetorParaMim = transform.position - candidato.position;
        if (vetorParaMim.sqrMagnitude <= 0.0001f) return true;

        float dot = Vector3.Dot(direcaoVoo, vetorParaMim.normalized);
        return dot >= dotMinimoAmeaca;
    }

    Vector3 ObterVelocidadeAmeaca(Transform candidato)
    {
        if (candidato == null) return Vector3.zero;

        MissileThreatTracker tracker = candidato.GetComponentInParent<MissileThreatTracker>();
        if (tracker != null)
        {
            Vector3 velocidadeTracker = tracker.ObterVelocidadeAtual();
            if (velocidadeTracker.sqrMagnitude > 0.01f)
            {
                ultimasPosicoesAmeaca[candidato] = candidato.position;
                return velocidadeTracker;
            }
        }

        Rigidbody rb = candidato.GetComponentInParent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            ultimasPosicoesAmeaca[candidato] = candidato.position;
            return rb.linearVelocity;
        }

        Vector3 ultimaPosicao;
        if (ultimasPosicoesAmeaca.TryGetValue(candidato, out ultimaPosicao))
        {
            Vector3 velocidadeAproximada = (candidato.position - ultimaPosicao) / Mathf.Max(tempoDeEscaneamento, 0.02f);
            ultimasPosicoesAmeaca[candidato] = candidato.position;
            if (velocidadeAproximada.sqrMagnitude > 0.01f)
            {
                return velocidadeAproximada;
            }
        }
        else
        {
            ultimasPosicoesAmeaca[candidato] = candidato.position;
        }

        if (candidato.forward.sqrMagnitude > 0.01f)
        {
            return candidato.forward.normalized * Mathf.Max(velocidadeMinimaAmeaca + 1f, 10f);
        }

        return Vector3.zero;
    }

    bool PossuiTagMisselNaHierarquia(Transform referencia)
    {
        if (referencia == null) return false;
        if (PareceMissilPorComponente(referencia)) return true;

        Transform atual = referencia;
        while (atual != null)
        {
            string tagAtual = atual.gameObject.tag;
            if (tagAtual == "Missel" || tagAtual == "Missil")
            {
                return true;
            }

            atual = atual.parent;
        }

        Transform raiz = ResolverTransformoRaiz(referencia);
        if (raiz == null) return false;

        Transform[] filhos = raiz.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < filhos.Length; i++)
        {
            Transform filho = filhos[i];
            if (filho == null) continue;

            string tagFilho = filho.gameObject.tag;
            if (tagFilho == "Missel" || tagFilho == "Missil")
            {
                return true;
            }
        }

        return false;
    }

    bool PareceMissilPorComponente(Transform referencia)
    {
        if (referencia == null) return false;

        if (referencia.GetComponentInParent<MissileThreatTracker>() != null) return true;
        if (referencia.GetComponentInParent<MisselNaval>() != null) return true;
        if (referencia.GetComponentInParent<MisselCaca>() != null) return true;
        if (referencia.GetComponentInParent<MisselSubmarino>() != null) return true;
        if (referencia.GetComponentInParent<MisselICBM>() != null) return true;
        if (referencia.GetComponentInParent<MisselTatico>() != null) return true;
        if (referencia.GetComponentInParent<MisselLeopardAutomatico>() != null) return true;
        if (referencia.GetComponentInParent<MissilTeleguiado>() != null) return true;
        return false;
    }

    void LimparRastrosInvalidos()
    {
        chavesAmeacaParaRemover.Clear();

        foreach (KeyValuePair<Transform, Vector3> item in ultimasPosicoesAmeaca)
        {
            Transform chave = item.Key;
            if (chave == null || !chave.gameObject.activeInHierarchy)
            {
                chavesAmeacaParaRemover.Add(chave);
            }
        }

        for (int i = 0; i < chavesAmeacaParaRemover.Count; i++)
        {
            ultimasPosicoesAmeaca.Remove(chavesAmeacaParaRemover[i]);
        }
    }

    bool AlvoMissilAtualAindaEhValido()
    {
        if (alvoMissilAtual == null) return false;
        if (!alvoMissilAtual.gameObject.activeInHierarchy) return false;
        if (alvoMissilAtual.GetComponentInParent<MissileThreatTracker>() != null)
            return EhMissilInimigo(alvoMissilAtual);
        return EhAmeacaSimples(alvoMissilAtual);
    }

    Vector3 PreverPosicaoAlvoSuperSonia()
    {
        if (alvoMissilAtual == null) return transform.position;
        return ObterPosicaoPreditaIntercepcao(alvoMissilAtual, null);
    }

    void Mirar()
    {
        Vector3 posFutura = PreverPosicaoAlvoSuperSonia();

        if (baseGiratoria != null)
        {
            Vector3 dirBase = posFutura - baseGiratoria.position;
            dirBase.y = 0f;
            if (dirBase.sqrMagnitude > 0.0001f)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(dirBase);
                baseGiratoria.rotation = Quaternion.Slerp(baseGiratoria.rotation, rotAlvo, Time.deltaTime * velocidadeGiro);
            }
        }

        if (canoElevacao != null)
        {
            Vector3 dirCano = posFutura - canoElevacao.position;
            if (dirCano.sqrMagnitude > 0.0001f)
            {
                Quaternion rotCano = Quaternion.LookRotation(dirCano);
                canoElevacao.rotation = Quaternion.Slerp(canoElevacao.rotation, rotCano, Time.deltaTime * velocidadeGiro);
            }
        }
    }

    bool MirouEmCheio()
    {
        Vector3 posFutura = PreverPosicaoAlvoSuperSonia();

        if (baseGiratoria != null)
        {
            Vector3 dir = posFutura - baseGiratoria.position;
            dir.y = 0f;
            Vector3 frente = baseGiratoria.forward;
            frente.y = 0f;

            if (dir.sqrMagnitude > 0.001f && Vector3.Angle(frente, dir.normalized) > 40f)
            {
                return false;
            }
        }

        if (canoElevacao != null)
        {
            Vector3 dir = posFutura - canoElevacao.position;
            if (dir.sqrMagnitude > 0.001f && Vector3.Angle(canoElevacao.forward, dir.normalized) > 40f)
            {
                return false;
            }
        }

        return true;
    }

    void AtirarInterceptador()
    {
        if (prefabIntercepador == null || pontosDeSaida == null || pontosDeSaida.Length == 0) return;

        Transform saidaDaVez = pontosDeSaida[indexSaida];
        indexSaida = (indexSaida + 1) % pontosDeSaida.Length;

        if (saidaDaVez == null) return;

        GameObject missilGerado = Instantiate(prefabIntercepador, saidaDaVez.position, saidaDaVez.rotation);
        IgnorarColisaoComOrigem(missilGerado);
        IgnorarColisaoComAliados(missilGerado);

        // ── NOVO: herda o teamID do navio para não ser interceptado por aliados ──
        IdentidadeUnidade idInterceptador = missilGerado.GetComponent<IdentidadeUnidade>();
        if (idInterceptador == null)
            idInterceptador = missilGerado.AddComponent<IdentidadeUnidade>();
        if (minhaIdentidade != null)
            idInterceptador.teamID = minhaIdentidade.teamID;
        // ─────────────────────────────────────────────────────────────────────────

        Transform alvoResolvido = ResolverTransformAlvo(alvoMissilAtual);
        Vector3 posicaoPredita = ObterPosicaoPreditaIntercepcao(alvoResolvido, saidaDaVez);
        bool inicializado = false;

        Projetil projetil = missilGerado.GetComponent<Projetil>();
        if (projetil != null)
        {
            Transform minhaRaiz = transform.root != null ? transform.root : transform;
            projetil.SetDono(minhaRaiz.gameObject);
            projetil.SetAlvo(alvoResolvido);

            Vector3 direcaoInicial = posicaoPredita - saidaDaVez.position;
            if (direcaoInicial.sqrMagnitude > 0.001f)
            {
                projetil.SetDirecao(direcaoInicial.normalized);
            }

            if (projetil.curvaDePerseguicao < 90f) projetil.curvaDePerseguicao = 150f;
            if (projetil.velocidade < 100f) projetil.velocidade = 200f;
            inicializado = true;
        }
        else
        {
            MisselCaca misselCaca = missilGerado.GetComponent<MisselCaca>();
            if (misselCaca != null)
            {
                misselCaca.IniciarAtaque(posicaoPredita, CalcularVelocidadeInicialInterceptador(saidaDaVez, posicaoPredita), alvoResolvido);
                inicializado = true;
            }
            else
            {
                MisselNaval misselNaval = missilGerado.GetComponent<MisselNaval>();
                if (misselNaval != null)
                {
                    misselNaval.IniciarAtaque(posicaoPredita, alvoResolvido);
                    inicializado = true;
                }
                else
                {
                    MisselSubmarino misselSubmarino = missilGerado.GetComponent<MisselSubmarino>();
                    if (misselSubmarino != null)
                    {
                        bool nasceuSubmerso = missilGerado.transform.position.y < 0f;
                        misselSubmarino.IniciarLancamento(posicaoPredita, nasceuSubmerso);
                        inicializado = true;
                    }
                    else
                    {
                        MisselICBM misselIcbm = missilGerado.GetComponent<MisselICBM>();
                        if (misselIcbm != null)
                        {
                            misselIcbm.IniciarLancamento(posicaoPredita);
                            inicializado = true;
                        }
                        else
                        {
                            MissilTeleguiado missilGuiado = missilGerado.GetComponent<MissilTeleguiado>();
                            if (missilGuiado != null)
                            {
                                missilGuiado.DefinirAlvo(alvoResolvido);
                                inicializado = true;
                            }
                            else
                            {
                                MisselLeopardAutomatico misselLeopard = missilGerado.GetComponent<MisselLeopardAutomatico>();
                                if (misselLeopard != null)
                                {
                                    misselLeopard.DefinirAlvo(alvoResolvido);
                                    inicializado = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (!inicializado)
        {
            Vector3 direcaoFallback = posicaoPredita - saidaDaVez.position;
            if (direcaoFallback.sqrMagnitude > 0.001f)
            {
                missilGerado.transform.rotation = Quaternion.LookRotation(direcaoFallback.normalized);
            }
        }

        if (alvoResolvido != null)
        {
            MissileThreatTracker.RegistrarLancamento(
                missilGerado,
                this,
                posicaoPredita,
                alvoResolvido,
                ObterVelocidadeInterceptador(),
                true);

            AntiMissilDetonadorProximidade detonador = missilGerado.GetComponent<AntiMissilDetonadorProximidade>();
            if (detonador == null) detonador = missilGerado.AddComponent<AntiMissilDetonadorProximidade>();
            detonador.alvo = alvoResolvido;
            detonador.forcarDestruicao = true;
            detonador.distanciaBaseIntercepcao = Mathf.Max(detonador.distanciaBaseIntercepcao, 8f);
        }

        if (somDisparo != null && audioSource != null)
        {
            audioSource.PlayOneShot(somDisparo, 0.7f);
        }
    }

    Transform ResolverTransformoRaiz(Transform origem)
    {
        if (origem == null) return null;

        Rigidbody rb = origem.GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.transform;

        return origem.root != null ? origem.root : origem;
    }

    Transform ResolverTransformAlvo(Transform alvo)
    {
        if (alvo == null) return null;

        Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
        if (rb != null) return rb.transform;

        Projetil projetil = alvo.GetComponentInParent<Projetil>();
        if (projetil != null) return projetil.transform;

        return alvo.root != null ? alvo.root : alvo;
    }

    Vector3 ObterDirecaoMissil(Transform missil)
    {
        if (missil == null) return transform.forward;

        Rigidbody rb = missil.GetComponentInParent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 25f)
        {
            Vector3 velNorm = rb.linearVelocity.normalized;
            if (Mathf.Abs(velNorm.y) < 0.8f)
            {
                return velNorm;
            }
        }

        if (missil.forward.sqrMagnitude > 0.01f)
        {
            return missil.forward.normalized;
        }

        Vector3 fallback = transform.position - missil.position;
        return fallback.sqrMagnitude > 0.01f ? fallback.normalized : transform.forward;
    }

    float ObterVelocidadeInterceptador()
    {
        if (prefabIntercepador == null) return 150f;

        Projetil projetil = prefabIntercepador.GetComponent<Projetil>();
        if (projetil != null && projetil.velocidade > 0f) return projetil.velocidade;

        MisselNaval misselNaval = prefabIntercepador.GetComponent<MisselNaval>();
        if (misselNaval != null) return Mathf.Max(misselNaval.velocidadeCruzeiro, misselNaval.velocidadeMergulho);

        MisselCaca misselCaca = prefabIntercepador.GetComponent<MisselCaca>();
        if (misselCaca != null) return misselCaca.velocidadeMaxima;

        MisselSubmarino misselSubmarino = prefabIntercepador.GetComponent<MisselSubmarino>();
        if (misselSubmarino != null) return Mathf.Max(misselSubmarino.velocidadeMaxima, misselSubmarino.velocidadeTurbo);

        MisselLeopardAutomatico misselLeopard = prefabIntercepador.GetComponent<MisselLeopardAutomatico>();
        if (misselLeopard != null) return Mathf.Max(misselLeopard.velocidadeMaxima, misselLeopard.velocidadeTurbo);

        MisselICBM misselIcbm = prefabIntercepador.GetComponent<MisselICBM>();
        if (misselIcbm != null) return misselIcbm.velocidade;

        MissilTeleguiado missilGuiado = prefabIntercepador.GetComponent<MissilTeleguiado>();
        if (missilGuiado != null) return missilGuiado.velocidade;

        return 150f;
    }

    Vector3 ObterPosicaoPreditaIntercepcao(Transform alvo, Transform origemDisparo)
    {
        if (alvo == null) return transform.position;

        Vector3 origem = origemDisparo != null ? origemDisparo.position : transform.position;
        Vector3 velocidadeAlvo = ObterVelocidadeAmeaca(alvo);

        if (velocidadeAlvo.sqrMagnitude <= 0.01f)
        {
            velocidadeAlvo = ObterDirecaoMissil(alvo) * 80f;
        }

        float velocidadeInterceptador = Mathf.Max(ObterVelocidadeInterceptador(), 1f);
        Vector3 posicaoPrevista = alvo.position;

        for (int i = 0; i < 3; i++)
        {
            float distancia = Vector3.Distance(origem, posicaoPrevista);
            float tempoInterceptacao = distancia / velocidadeInterceptador;
            posicaoPrevista = alvo.position + (velocidadeAlvo * tempoInterceptacao);
        }

        return posicaoPrevista;
    }

    Vector3 CalcularVelocidadeInicialInterceptador(Transform saida, Vector3 posicaoAlvo)
    {
        if (saida == null) return transform.forward * 40f;

        Vector3 direcaoInicial = posicaoAlvo - saida.position;
        if (direcaoInicial.sqrMagnitude <= 0.001f)
        {
            direcaoInicial = saida.forward.sqrMagnitude > 0.001f ? saida.forward : transform.forward;
        }

        direcaoInicial.Normalize();

        Rigidbody rbOrigem = transform.root != null ? transform.root.GetComponent<Rigidbody>() : null;
        Vector3 velocidadeOrigem = rbOrigem != null ? rbOrigem.linearVelocity : Vector3.zero;
        float velocidadeBase = Mathf.Max(ObterVelocidadeInterceptador() * 0.6f, 40f);

        return velocidadeOrigem + (direcaoInicial * velocidadeBase);
    }

    void IgnorarColisaoComOrigem(GameObject missilGerado)
    {
        if (missilGerado == null) return;

        Transform minhaRaiz = transform.root != null ? transform.root : transform;
        Collider[] collidersOrigem = minhaRaiz.GetComponentsInChildren<Collider>();
        Collider[] collidersMissil = missilGerado.GetComponentsInChildren<Collider>();

        foreach (Collider colOrigem in collidersOrigem)
        {
            if (colOrigem == null) continue;

            foreach (Collider colMissil in collidersMissil)
            {
                if (colMissil == null) continue;
                Physics.IgnoreCollision(colOrigem, colMissil, true);
            }
        }
    }

    void IgnorarColisaoComAliados(GameObject missilGerado)
    {
        if (missilGerado == null || minhaIdentidade == null) return;

        Collider[] collidersMissil = missilGerado.GetComponentsInChildren<Collider>();
        if (collidersMissil == null || collidersMissil.Length == 0) return;

        int quantidade = Physics.OverlapSphereNonAlloc(transform.position, alcanceRadar, bufferAliados, Physics.AllLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < quantidade; i++)
        {
            Collider colAliado = bufferAliados[i];
            if (colAliado == null) continue;
            if (colAliado.transform.root == transform.root) continue;

            IdentidadeUnidade identidadeAliada = colAliado.GetComponentInParent<IdentidadeUnidade>();
            if (identidadeAliada == null || identidadeAliada.teamID != minhaIdentidade.teamID) continue;

            for (int j = 0; j < collidersMissil.Length; j++)
            {
                Collider colMissil = collidersMissil[j];
                if (colMissil == null) continue;
                Physics.IgnoreCollision(colAliado, colMissil, true);
            }
        }

        for (int i = 0; i < quantidade; i++) bufferAliados[i] = null;
    }

    public void DefinirModoAtivo(bool ativo)
    {
        modoPassivo = !ativo;
        if (modoPassivo) alvoMissilAtual = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, alcanceRadar);

        if (alvoMissilAtual != null)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawLine(transform.position, alvoMissilAtual.position);
            Gizmos.DrawSphere(alvoMissilAtual.position, 2.5f);
        }
    }
}
