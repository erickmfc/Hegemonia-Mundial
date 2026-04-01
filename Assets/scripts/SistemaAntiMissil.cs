using UnityEngine;
using System.Collections.Generic;

public class SistemaAntiMissil : MonoBehaviour
{
    [Header("Radar & Alcance (Defesa de Area)")]
    [Tooltip("Raio de deteccao do radar para defender o navio e todos os aliados proximos.")]
    public float alcanceRadar = 180f;
    [Tooltip("Tempo em segundos entre cada checagem do radar (ex: 0.3s)")]
    public float tempoDeEscaneamento = 0.3f;

    [Header("Mecanica da Torreta")]
    [Tooltip("Base que gira para os lados (Yaw)")]
    public Transform baseGiratoria;
    [Tooltip("Peca que vira para cima/baixo (Pitch)")]
    public Transform canoElevacao;
    public float velocidadeGiro = 60f;

    [Header("Sistema de Disparo")]
    [Tooltip("Prefab do missil que vai abater o outro missil (Interceptador).")]
    public GameObject prefabIntercepador;
    [Tooltip("Zonas de onde o missil interceptador vai sair.")]
    public Transform[] pontosDeSaida;
    [Tooltip("Cadencia de tiro. Tempo entre disparar um interceptador e outro.")]
    public float tempoEntreTiros = 0.8f;
    [Tooltip("Capacidade de misseis prontos. Quantidade antes de iniciar a recarga cheia.")]
    public int capacidadeMisseis = 10;
    public float tempoRecargaMisseis = 5f;

    [Header("Efeitos & Sons")]
    public AudioClip somDisparo;
    private AudioSource audioSource;

    [Header("Comportamento")]
    [Tooltip("Se ativado, o sistema nao intercepta misseis automaticamente (modo Ocioso).")]
    public bool modoPassivo = false;

    private Transform alvoMissilAtual;
    private IdentidadeUnidade minhaIdentidade;
    private float cooldownDisparo = 0f;
    private int misseisAtuais;
    private bool recarregando = false;
    private int indexSaida = 0;
    private readonly HashSet<Transform> bufferMisseisUnicos = new HashSet<Transform>();
    private readonly HashSet<Transform> bufferAliadosUnicos = new HashSet<Transform>();
    private const float DOT_APROXIMACAO_AMIGOS = 0.2f;
    private const float DOT_AFASTANDO_AMIGO = -0.15f;
    private const float RAIO_IGNORAR_LANCAMENTO_AMIGO = 80f;
    private const float RAIO_AUTODEFESA = 140f;

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
        if (cooldownDisparo > 0f) cooldownDisparo -= Time.deltaTime;

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

            if (cooldownDisparo <= 0f && MirouEmCheio())
            {
                if (misseisAtuais > 0)
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

        if (AlvoMissilAtualAindaEhValido()) return;

        alvoMissilAtual = null;

        Collider[] objetosNaArea = Physics.OverlapSphere(transform.position, alcanceRadar);

        List<Transform> misseisDetectados = new List<Transform>();
        List<Transform> aliadosNaArea = new List<Transform>();
        bufferMisseisUnicos.Clear();
        bufferAliadosUnicos.Clear();

        Transform minhaRaiz = transform.root != null ? transform.root : transform;
        aliadosNaArea.Add(minhaRaiz);
        bufferAliadosUnicos.Add(minhaRaiz);

        foreach (Collider col in objetosNaArea)
        {
            if (col == null) continue;

            Transform tr = ResolverTransformoRaiz(col.transform);
            if (tr == null) continue;

            if (EhMissil(tr))
            {
                if (bufferMisseisUnicos.Add(tr))
                {
                    misseisDetectados.Add(tr);
                }
                continue;
            }

            if (col.isTrigger) continue;

            IdentidadeUnidade id = tr.GetComponentInParent<IdentidadeUnidade>();
            if (id == null) continue;

            if (id.teamID == minhaIdentidade.teamID || minhaIdentidade.teamID == 0)
            {
                Transform raizAliada = id.transform.root != null ? id.transform.root : id.transform;
                if (bufferAliadosUnicos.Add(raizAliada))
                {
                    aliadosNaArea.Add(raizAliada);
                }
            }
        }

        float menorDistancia = Mathf.Infinity;
        Transform melhorMissilAlvo = null;

        foreach (Transform missil in misseisDetectados)
        {
            if (missil == null) continue;

            Vector3 posMissil = missil.position;
            Vector3 dirMissil = ObterDirecaoMissil(missil);

            bool ignorarPorSerAliado = false;
            foreach (Transform aliado in aliadosNaArea)
            {
                if (aliado == null) continue;

                Vector3 dirProAliado = aliado.position - posMissil;
                float distAteAmigo = dirProAliado.magnitude;

                if (distAteAmigo < RAIO_IGNORAR_LANCAMENTO_AMIGO && dirProAliado.sqrMagnitude > 0.001f)
                {
                    if (Vector3.Dot(dirMissil, dirProAliado.normalized) < DOT_AFASTANDO_AMIGO)
                    {
                        ignorarPorSerAliado = true;
                        break;
                    }
                }
            }

            if (ignorarPorSerAliado) continue;

            bool ehAmeaca = false;
            foreach (Transform aliado in aliadosNaArea)
            {
                if (aliado == null) continue;

                Vector3 vetorParaAliado = aliado.position - posMissil;
                if (vetorParaAliado.sqrMagnitude <= 0.001f)
                {
                    ehAmeaca = true;
                    break;
                }

                Vector3 dirProAliado = vetorParaAliado.normalized;
                if (Vector3.Dot(dirMissil, dirProAliado) > DOT_APROXIMACAO_AMIGOS)
                {
                    ehAmeaca = true;
                    break;
                }
            }

            // Fallback de autodefesa: só considera ameaça se estiver realmente se aproximando de nós
            if (!ehAmeaca)
            {
                float distAteMim = Vector3.Distance(transform.position, posMissil);
                if (distAteMim < RAIO_AUTODEFESA)
                {
                    Vector3 vetorParaMim = transform.position - posMissil;
                    if (vetorParaMim.sqrMagnitude > 0.001f && Vector3.Dot(dirMissil, vetorParaMim.normalized) > DOT_APROXIMACAO_AMIGOS)
                    {
                        ehAmeaca = true;
                    }
                }
            }

            if (ehAmeaca)
            {
                float distancia = Vector3.Distance(transform.position, posMissil);
                if (distancia < menorDistancia)
                {
                    menorDistancia = distancia;
                    melhorMissilAlvo = missil;
                }
            }
        }

        alvoMissilAtual = melhorMissilAlvo;
    }

    Vector3 PreverPosicaoAlvoSuperSonia()
    {
        if (alvoMissilAtual == null) return transform.position;
        return ObterPosicaoPreditaIntercepcao(alvoMissilAtual, null);
    }

    void Mirar()
    {
        Vector3 posFuturaMortal = PreverPosicaoAlvoSuperSonia();

        if (baseGiratoria != null)
        {
            Vector3 dirBase = posFuturaMortal - baseGiratoria.position;
            dirBase.y = 0f;
            if (dirBase != Vector3.zero)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(dirBase);
                baseGiratoria.rotation = Quaternion.Slerp(baseGiratoria.rotation, rotAlvo, Time.deltaTime * velocidadeGiro);
            }
        }

        if (canoElevacao != null)
        {
            Vector3 dirCano = posFuturaMortal - canoElevacao.position;
            if (dirCano != Vector3.zero)
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

        // Garantia de "abate": mesmo que o alvo não tenha SistemaDeDanos/Collider, remove o míssil inimigo por proximidade.
        if (alvoResolvido != null)
        {
            AntiMissilDetonadorProximidade detonador = missilGerado.GetComponent<AntiMissilDetonadorProximidade>();
            if (detonador == null) detonador = missilGerado.AddComponent<AntiMissilDetonadorProximidade>();
            detonador.alvo = alvoResolvido;
        }

        if (somDisparo != null && audioSource != null)
        {
            audioSource.PlayOneShot(somDisparo, 0.7f);
        }
    }

    bool AlvoMissilAtualAindaEhValido()
    {
        if (alvoMissilAtual == null) return false;
        if (!alvoMissilAtual.gameObject.activeInHierarchy) return false;
        if (!EhMissil(alvoMissilAtual)) return false;
        return Vector3.Distance(transform.position, alvoMissilAtual.position) <= (alcanceRadar * 1.5f);
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

    bool EhMissil(Transform tr)
    {
        if (tr == null) return false;

        // Pedido: só considera míssil se estiver com a TAG correta.
        // OBS: o projeto usa "Missel" no TagManager. Mantemos "Missil" como compatibilidade.
        string tagStr = tr.gameObject.tag;
        return tagStr == "Missel" || tagStr == "Missil";
    }

    bool PossuiScriptDeMissil(Transform tr)
    {
        Projetil proj = tr.GetComponentInParent<Projetil>();
        if (proj != null)
        {
            // Evita "atirar no nada": balas comuns (sem explosão e sem homing) não são consideradas mísseis.
            if (proj.curvaDePerseguicao > 0f || proj.raioDeExplosao > 0.01f) return true;
        }

        return tr.GetComponentInParent<MisselNaval>() != null ||
               tr.GetComponentInParent<MisselCaca>() != null ||
               tr.GetComponentInParent<MisselSubmarino>() != null ||
               tr.GetComponentInParent<MisselICBM>() != null ||
               tr.GetComponentInParent<MisselTatico>() != null ||
               tr.GetComponentInParent<MisselLeopardAutomatico>() != null ||
               tr.GetComponentInParent<MissilTeleguiado>() != null ||
               tr.GetComponentInParent<InterceptMissile>() != null;
    }

    Vector3 ObterDirecaoMissil(Transform missil)
    {
        if (missil == null) return transform.forward;

        Rigidbody rb = missil.GetComponentInParent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            return rb.linearVelocity.normalized;
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
        Vector3 velocidadeAlvo;

        Rigidbody rb = alvo.GetComponentInParent<Rigidbody>();
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            velocidadeAlvo = rb.linearVelocity;
        }
        else
        {
            velocidadeAlvo = ObterDirecaoMissil(alvo) * 80f;
        }

        float distancia = Vector3.Distance(origem, alvo.position);
        float velocidadeInterceptador = Mathf.Max(ObterVelocidadeInterceptador(), 1f);
        float tempoInterceptacao = distancia / velocidadeInterceptador;

        return alvo.position + (velocidadeAlvo * tempoInterceptacao);
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
