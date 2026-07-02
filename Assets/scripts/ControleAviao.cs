using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// O CÉREBRO COMPLETO DO AVIÃO.
/// Divide o movimento em 2 modos: Solo (rígido e guiado) e Voo (solto e realista).
/// </summary>
public class ControleAviao : MonoBehaviour
{
    public enum EstadoAviao { ReservaHangar, Taxiando, ProntoNoPatio, Decolando, EmMissao, Pousando, RetornandoPraVaga }
    
    [Header("=== ESTADO ATUAL ===")]
    public EstadoAviao estadoAtual = EstadoAviao.ReservaHangar;

    public void DefinirEstado(EstadoAviao novoEstado)
    {
        estadoAtual = novoEstado;
        bool motorLigado = novoEstado != EstadoAviao.ReservaHangar && novoEstado != EstadoAviao.ProntoNoPatio;
        AudioRuntime.DefinirMotorAereo(gameObject, motorLigado);
    }
    [HideInInspector] public GerenciadorAeroporto aeroportoOrigem;
    [HideInInspector] public Transform vagaRetorno;
    [HideInInspector] public bool aguardandoCliqueRadar = false;
    [Header("=== MISSÃO ===")]
    public bool ordemParaRetorno = false;
    [Range(0.08f, 0.45f)] public float reservaMinimaRetornoPercentual = 0.22f;
    [Range(0.15f, 0.60f)] public float reservaRetornoComDanosPercentual = 0.34f;

    [Header("=== FÍSICA E VELOCIDADES ===")]
    [Tooltip("Velocidade rígida no chão para não fazer zig-zag")]
    public float velocidadeSolo = 10f; 
    [Tooltip("Velocidade de voo nas nuvens")]
    public float velocidadeMaximaVoo = 180f; 
    public float taxaDeGiroLeme = 35f;    

    [Header("=== ÓRBITA DA MISSÃO ===")]
    [Tooltip("Raio horizontal da órbita em torno da área ordenada.")]
    public float raioOrbitaMissao = 350f;
    [Tooltip("Velocidade angular da órbita em torno do alvo/patrulha.")]
    public float velocidadeOrbitaMissao = 0.9f;
    [Tooltip("Distância para considerar que chegou ao centro inicial da missão.")]
    public float margemChegadaMissao = 65f;
    [Tooltip("Altitude de voo padrão da aeronave (em metros).")]
    public float altitudeVoo = 120f;

    [Header("=== ANIMAÇÃO VISUAL ===")]
    public Transform modeloMecanicoVisual; 
    public float asaBankingMaximo = 45f; 
    public float arfagemPitchMaxima = 20f; 

    [Header("=== TREM DE POUSO ===")]
    public List<Transform> rodas;
    private List<Quaternion> rotacoesOriginaisRodas = new List<Quaternion>();
    private bool rodasRecolhidas = false;

    // Cache por tipo de aeronave: evita varrer toda a hierarquia em TODO spawn.
    private static readonly Dictionary<string, string[]> CacheCaminhosRodasPorPrefab = new Dictionary<string, string[]>();

    [Tooltip("Ajuste extra de altura quando estacionado no convés/pátio (positivo = mais alto).")]
    public float ajusteAlturaEstacionado = 0f;
    private float _alturaEstacionamentoCache = -1f;

    // Variáveis internas
    public Vector3 alvoGPSVoo;
    public Vector3 centroDaPatrulha; 
    [HideInInspector] public bool emAtaqueMergulho = false;
    [HideInInspector] public Vector3 alvoDoMergulho;
    [HideInInspector] public bool alvoPrioritarioIA = false; 
    public Vector3 alvoEstrategico; // Armazena a coordenada real (com Y exato do chão)

    public bool estaEmModoVooFisico = false;
    protected float giroLateralRoll = 0f; 
    protected float empinadaPitch = 0f;   
    protected float giroLateralYInicial = 0f;
    protected float multiplicadorVelocidadeTurbo = 1f;
    protected float velocidadeVooAtual = 0f;
    public float aceleracaoVoo = 18f;
    public float desaceleracaoVoo = 25f;
    protected float tempoSegurandoTab = 0f;
    protected float anguloOrbitaAtual = 0f;
    protected int sentidoOrbita = 1;
    protected bool retornoAutomaticoAposChegadaCentro = false;
    protected readonly EstadoOtimizacaoTatica estadoOtimizacao = new EstadoOtimizacaoTatica();
    protected readonly List<Vector3> rotaPatrulhaSalva = new List<Vector3>();
    protected Vector3 ultimoObjetivoMissao = Vector3.zero;
    protected bool retomarMissaoAposAbastecer = false;
    protected string ultimoMotivoRetorno = string.Empty;
    protected Coroutine rotinaRetomadaMissao;
    protected int indiceRetanguloPatrulha = 0;
    private bool protecaoCombustivelCarrier = false;

    // --- CACHE DE COMPONENTES (evita GetComponent no Update) ---
    protected ControleUnidade _controleUnidade;
    protected SistemaDeDanos _sistemaDanos;
    protected LancadorMisselCaca _lancadorCaca;
    protected float _tempoUltimoDanoRecebido = -100f;

    protected virtual void Start()
    {
        if (modeloMecanicoVisual != null)
        {
            giroLateralYInicial = modeloMecanicoVisual.localEulerAngles.y;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true; 

        // Cache de componentes usados no Update
        _controleUnidade = GetComponent<ControleUnidade>();
        _sistemaDanos = GetComponent<SistemaDeDanos>();
        _lancadorCaca = GetComponent<LancadorMisselCaca>();

        if (_sistemaDanos != null)
        {
            _sistemaDanos.OnDano += RegistrarDanoRecebido;
        }

        if (rodas == null) rodas = new List<Transform>();
        if (rodas.Count == 0)
        {
            string chave = NormalizarChavePrefab(gameObject.name);
            if (!string.IsNullOrEmpty(chave)
                && CacheCaminhosRodasPorPrefab.TryGetValue(chave, out string[] caminhos)
                && caminhos != null
                && caminhos.Length > 0)
            {
                for (int i = 0; i < caminhos.Length; i++)
                {
                    if (string.IsNullOrEmpty(caminhos[i])) continue;
                    Transform roda = transform.Find(caminhos[i]);
                    if (roda != null) rodas.Add(roda);
                }
            }

            if (rodas.Count == 0)
            {
                Transform[] filhos = GetComponentsInChildren<Transform>(true);
                List<string> caminhosRodas = null;
                for (int i = 0, count = filhos.Length; i < count; i++)
                {
                    Transform f = filhos[i];
                    if (f == null) continue;
                    if (f == transform) continue;
                    string n = f.name;
                    if (ContemIgnoreCase(n, "wheel")
                        || ContemIgnoreCase(n, "roda")
                        || ContemIgnoreCase(n, "gear")
                        || ContemIgnoreCase(n, "pneu")
                        || ContemIgnoreCase(n, "tremdepouso"))
                    {
                        rodas.Add(f);
                        if (caminhosRodas == null) caminhosRodas = new List<string>(8);
                        caminhosRodas.Add(ObterCaminhoRelativo(f, transform));
                    }
                }

                if (!string.IsNullOrEmpty(chave) && caminhosRodas != null && caminhosRodas.Count > 0)
                {
                    CacheCaminhosRodasPorPrefab[chave] = caminhosRodas.ToArray();
                }
            }
        }

        rotacoesOriginaisRodas.Clear();
        for (int i = 0, count = rodas.Count; i < count; i++)
        {
            rotacoesOriginaisRodas.Add(rodas[i] != null ? rodas[i].localRotation : Quaternion.identity);
        }
        AbaixarRodas();

        // Cacheia altura de estacionamento para uso no pátio do porta-aviões
        _alturaEstacionamentoCache = ObterAlturaEstacionamento();
    }

    private void OnDestroy()
    {
        if (_sistemaDanos != null)
        {
            _sistemaDanos.OnDano -= RegistrarDanoRecebido;
        }
        RegistroEntidadesJogo.Unregister(this);
    }

    private void RegistrarDanoRecebido()
    {
        _tempoUltimoDanoRecebido = Time.time;
    }

    public float ObterAlturaEstacionamento()
    {
        if (_alturaEstacionamentoCache > 0f) return _alturaEstacionamentoCache;

        float alturaBase = Mathf.Max(0.05f, ajusteAlturaEstacionado);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return alturaBase;

        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!hasBounds) return alturaBase;

        float offset = transform.position.y - bounds.min.y + 0.05f;
        if (float.IsNaN(offset) || float.IsInfinity(offset)) return alturaBase;
        return Mathf.Clamp(offset, alturaBase, 5f); // Limita a 5m para evitar esferas de radar gigantes
    }

    private static string NormalizarChavePrefab(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return string.Empty;
        int idx = nome.IndexOf("(Clone)", StringComparison.Ordinal);
        if (idx >= 0) nome = nome.Substring(0, idx);
        return nome.Trim();
    }

    private static bool ContemIgnoreCase(string texto, string termo)
    {
        return !string.IsNullOrEmpty(texto)
               && !string.IsNullOrEmpty(termo)
               && texto.IndexOf(termo, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ObterCaminhoRelativo(Transform alvo, Transform raiz)
    {
        if (alvo == null || raiz == null || alvo == raiz) return string.Empty;

        List<string> partes = new List<string>(8);
        Transform atual = alvo;
        while (atual != null && atual != raiz)
        {
            partes.Add(atual.name);
            atual = atual.parent;
        }

        if (atual != raiz)
        {
            return alvo.name;
        }

        partes.Reverse();
        return string.Join("/", partes);
    }

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }



    protected virtual void Update()
    {
        if (!estaEmModoVooFisico) return;
        long inicioUpdate = InfraPerformanceGameplay.MarcarInicioMedicao();
        AtualizarEstadoOtimizacao();

        float intervaloLogica = InfraPerformanceGameplay.ResolverIntervalo(0.18f, estadoOtimizacao, true, true);
        bool executarLogica = InfraPerformanceGameplay.DeveExecutar(this, ref estadoOtimizacao.proximoTickLogica, intervaloLogica);

        if (executarLogica)
        {
            long inicioLogica = InfraPerformanceGameplay.MarcarInicioMedicao();
            AvaliarRetornoSeguroAutomatico();
            InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Logistica, inicioLogica);
        }

        if (!PodeIgnorarFaltaDeCombustivel() && !CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        float dt = Time.deltaTime;
        bool selecionado = (_controleUnidade != null && _controleUnidade.selecionado);

        float multiplicadorDanos = 1f;
        if (executarLogica && _sistemaDanos != null && _sistemaDanos.vidaMaxima > 0)
        {
            float pctVida = _sistemaDanos.vidaAtual / _sistemaDanos.vidaMaxima;
            if (pctVida < 0.25f)
            {
                multiplicadorDanos = 0.5f;
                // Drones Kamikaze não retornam à base por danos, eles continuam até o fim
                bool isKamikaze = GetComponent<KamikazeDrone>() != null;
                if (estadoAtual == EstadoAviao.EmMissao && !ordemParaRetorno && !isKamikaze)
                {
                    if (selecionado)
                    {
                        Debug.Log($"<color=red>[{gameObject.name}] DANOS CRÍTICOS ({Mathf.RoundToInt(pctVida*100)}%)! Retornando base.</color>");
                    }
                    PrepararRetornoSeguro(EncontrarMelhorBaseRetorno(), "danos criticos");
                }
            }
        }

        if (selecionado && Input.GetKey(KeyCode.Tab))
        {
            tempoSegurandoTab += dt;
            float alvo;
            if (tempoSegurandoTab >= 11f) alvo = 6f;
            else if (tempoSegurandoTab >= 5f) alvo = 4f;
            else if (tempoSegurandoTab >= 2f) alvo = 2f;
            else alvo = 1.5f;
            multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, alvo, dt);
        }
        else
        {
            tempoSegurandoTab = 0f; 
            multiplicadorVelocidadeTurbo = Mathf.Lerp(multiplicadorVelocidadeTurbo, 1f, dt * 2f);
        }

        ManobraVooRealista(multiplicadorDanos);
        InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Aereo, inicioUpdate);
    }

    private void AtualizarEstadoOtimizacao()
    {
        bool selecionado = (_controleUnidade != null && _controleUnidade.selecionado);
        bool engajado = estadoAtual == EstadoAviao.EmMissao
            || estadoAtual == EstadoAviao.Decolando
            || estadoAtual == EstadoAviao.Pousando
            || ordemParaRetorno;
        bool heroico = GetComponent<KamikazeDrone>() == null;
        InfraPerformanceGameplay.AtualizarEstadoBase(estadoOtimizacao, transform, selecionado, engajado, heroico, 180f, 420f);
    }

    protected virtual void ManobraVooRealista(float multDano = 1f)
    {
        float dt = Time.deltaTime;
        Vector3 retaAteAlvo = alvoGPSVoo - transform.position;
        float anguloPressaoLateralY = 0f;
        
        if (retaAteAlvo.sqrMagnitude > 0.1f)
        {
            Vector3 upRef = Mathf.Abs(Vector3.Dot(retaAteAlvo.normalized, Vector3.up)) > 0.99f ? transform.up : Vector3.up;
            Quaternion olharMundoDesejado = Quaternion.LookRotation(retaAteAlvo, upRef);
            anguloPressaoLateralY = Vector3.SignedAngle(transform.forward, retaAteAlvo, Vector3.up);
            
            // Slerp suaviza o voo do avião e evita viradas bruscas instantâneas
            transform.rotation = Quaternion.Slerp(transform.rotation, olharMundoDesejado, (taxaDeGiroLeme / 15f) * dt);
        }
        
        float multiplicadorPatrulha = 1f;
        if (estadoAtual == EstadoAviao.EmMissao && !alvoPrioritarioIA && !emAtaqueMergulho)
        {
            KamikazeDrone kdTemp = GetComponent<KamikazeDrone>();
            if (kdTemp == null || !kdTemp.kamikazeAtivo)
            {
                bool sobAtaqueOuEngajado = (_lancadorCaca != null && _lancadorCaca.TemInimigosDetectados) 
                                           || (Time.time - _tempoUltimoDanoRecebido < 8f);
                if (sobAtaqueOuEngajado)
                {
                    multiplicadorPatrulha = 1.2f; // Acelera a 120% no combate/ataque
                }
                else
                {
                    multiplicadorPatrulha = 1.0f; // Velocidade máxima na patrulha normal (100%)
                }
            }
        }
        else if (estadoAtual == EstadoAviao.Pousando)
        {
            bool pousoEmPortaAvioes = aeroportoOrigem is GerenciadorPortaAvioes;
            if (aeroportoOrigem != null && aeroportoOrigem.waypointsDecida != null && aeroportoOrigem.waypointsDecida.Count > 1)
            {
                float distToTouchdown = Vector3.Distance(transform.position, aeroportoOrigem.waypointsDecida[1].position);
                if (pousoEmPortaAvioes)
                {
                    multiplicadorPatrulha = distToTouchdown > 120f ? 0.95f : 0.75f;
                }
                else if (distToTouchdown > 120f)
                {
                    multiplicadorPatrulha = 0.7f; // Mantém 70% da velocidade na aproximação longa
                }
                else
                    multiplicadorPatrulha = Mathf.Lerp(multiplicadorPatrulha, 0.3f, Time.deltaTime * 2f); // Desacelera progressivamente para pousar lento (100 a chegar)
            }
            else
            {
                multiplicadorPatrulha = pousoEmPortaAvioes ? 0.8f : 0.4f; // Reduz velocidade na aproximação padrão
            }
        }

        float velFinal = (velocidadeMaximaVoo * multiplicadorVelocidadeTurbo * multiplicadorPatrulha) * multDano;
        float taxaVelocidade = velFinal >= velocidadeVooAtual ? aceleracaoVoo : desaceleracaoVoo;
        velocidadeVooAtual = Mathf.MoveTowards(velocidadeVooAtual, velFinal, taxaVelocidade * dt);
        Vector3 novaPos = transform.position + transform.forward * (velocidadeVooAtual * dt);

        bool isKamikazeDiving = false;
        KamikazeDrone kd = GetComponent<KamikazeDrone>();
        if (kd != null && kd.kamikazeAtivo) isKamikazeDiving = true;

        if (novaPos.y < 15f && !isKamikazeDiving)
        {
            novaPos.y = 15f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, transform.eulerAngles.y, 0), 30f * dt);
        }
        
        if (Mathf.Abs(novaPos.x) > 10000f || Mathf.Abs(novaPos.z) > 10000f)
        {
             Vector3 centroDoMap = new Vector3(0, novaPos.y, 0);
             alvoGPSVoo = centroDoMap;
             Quaternion freioDeOuro = Quaternion.LookRotation((centroDoMap - transform.position).normalized);
             transform.rotation = Quaternion.RotateTowards(transform.rotation, freioDeOuro, 100f * dt);
             novaPos = transform.position + transform.forward * (velocidadeVooAtual * dt);
        }

        transform.position = novaPos;

        if (modeloMecanicoVisual != null)
        {
            float inclinacaoAlvoZ = Mathf.Clamp(anguloPressaoLateralY * -2.5f, -asaBankingMaximo, asaBankingMaximo);
            float inclinacaoAlvoX = Mathf.Clamp(retaAteAlvo.y * -3.0f, -arfagemPitchMaxima, arfagemPitchMaxima);
            giroLateralRoll = Mathf.Lerp(giroLateralRoll, inclinacaoAlvoZ, dt * 5f);
            empinadaPitch = Mathf.Lerp(empinadaPitch, inclinacaoAlvoX, dt * 5f);
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, giroLateralYInicial, giroLateralRoll);
        }
    }

    public IEnumerator MoverInterpolado(Vector3 destinoFixo, float vel, bool pontoFinal = false, Transform alvoMovel = null, bool ignoreRotationSlowdown = false)
    {
        float raioDeAceitacao = pontoFinal ? 0.5f : 3.5f; // Aumentado para não engasgar em waypoints muito próximos
        float raioSqr = raioDeAceitacao * raioDeAceitacao;

        bool alvoMovelFornecido = (alvoMovel != null);
        Transform pai = transform.parent;

        while (true)
        {
            if (alvoMovelFornecido && alvoMovel == null) break; // O alvo (ex: vaga ou aeroporto) foi destruído no caminho

            if (!PodeIgnorarFaltaDeCombustivel() && !CombustivelUnidade.PodeOperarObjeto(gameObject))
            {
                PararPorFaltaDeCombustivel();
                yield break;
            }

            if (pai != null)
            {
                // Movimentação em espaço local do navio
                Vector3 posLocal = transform.localPosition;
                Vector3 destLocal = (alvoMovel != null) ? pai.InverseTransformPoint(alvoMovel.position) : pai.InverseTransformPoint(destinoFixo);
                
                Vector3 diffLocal = new Vector3(destLocal.x - posLocal.x, 0, destLocal.z - posLocal.z);
                if (diffLocal.sqrMagnitude <= raioSqr) break;

                Vector3 vetorAteDestinoLocal = destLocal - posLocal;
                Vector3 dirForwardLocal = transform.localRotation * Vector3.forward;
                if (vetorAteDestinoLocal.sqrMagnitude < 16f && Vector3.Dot(dirForwardLocal, vetorAteDestinoLocal.normalized) < 0f) break;

                Vector3 direcaoHorizonLocal = new Vector3(vetorAteDestinoLocal.x, 0, vetorAteDestinoLocal.z).normalized;
                if (direcaoHorizonLocal != Vector3.zero && vetorAteDestinoLocal.sqrMagnitude > 0.05f)
                {
                    Quaternion rotAlvoLocal = Quaternion.LookRotation(direcaoHorizonLocal);
                    transform.localRotation = Quaternion.RotateTowards(transform.localRotation, rotAlvoLocal, (ignoreRotationSlowdown ? 90f : 50f) * Time.deltaTime);

                    float fatorVelocidade = 1f;
                    if (!ignoreRotationSlowdown)
                    {
                        fatorVelocidade = Mathf.Clamp01(1.2f - (Quaternion.Angle(transform.localRotation, rotAlvoLocal) / 45f));
                        if (fatorVelocidade < 0.2f) fatorVelocidade = 0.2f;
                    }

                    Vector3 proximaPosLocal = posLocal + direcaoHorizonLocal * (vel * fatorVelocidade) * Time.deltaTime;
                    proximaPosLocal.y = destLocal.y; // Mantém a altura local alinhada com o convés
                    transform.localPosition = proximaPosLocal;
                }
            }
            else
            {
                // Movimentação global padrão
                Vector3 meuPos = transform.position;
                Vector3 diff;

                if (alvoMovel != null)
                {
                    diff = new Vector3(alvoMovel.position.x - meuPos.x, 0, alvoMovel.position.z - meuPos.z);
                }
                else
                {
                    diff = new Vector3(destinoFixo.x - meuPos.x, 0, destinoFixo.z - meuPos.z);
                }

                if (diff.sqrMagnitude <= raioSqr) break; 

                Vector3 vetorAteDestino = (alvoMovel != null) ? (alvoMovel.position - meuPos) : (destinoFixo - meuPos);
                if (vetorAteDestino.sqrMagnitude < 16f && Vector3.Dot(transform.forward, vetorAteDestino.normalized) < 0f) break; 

                Vector3 direcaoHorizon = new Vector3(vetorAteDestino.x, 0, vetorAteDestino.z).normalized;
                if (direcaoHorizon != Vector3.zero && vetorAteDestino.sqrMagnitude > 0.05f)
                {
                    Quaternion rotAlvo = Quaternion.LookRotation(direcaoHorizon);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, (ignoreRotationSlowdown ? 90f : 50f) * Time.deltaTime);
                    
                    float fatorVelocidade = 1f;
                    if (!ignoreRotationSlowdown)
                    {
                        fatorVelocidade = Mathf.Clamp01(1.2f - (Quaternion.Angle(transform.rotation, rotAlvo) / 45f));
                        if (fatorVelocidade < 0.2f) fatorVelocidade = 0.2f;
                    }
                    
                    transform.position += vetorAteDestino.normalized * (vel * fatorVelocidade) * Time.deltaTime;
                }
            }

            if (modeloMecanicoVisual != null) modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.Euler(0f, giroLateralYInicial, 0f), Time.deltaTime * 5f);
            yield return null;
        }

        if (pontoFinal)
        {
            if (pai != null)
            {
                Vector3 destLocal = (alvoMovel != null) ? pai.InverseTransformPoint(alvoMovel.position) : pai.InverseTransformPoint(destinoFixo);
                if ((transform.localPosition - destLocal).sqrMagnitude < 25f)
                    transform.localPosition = destLocal;
            }
            else
            {
                Vector3 destinoFinal = (alvoMovel != null) ? alvoMovel.position : destinoFixo;
                if ((transform.position - destinoFinal).sqrMagnitude < 25f) 
                    transform.position = destinoFinal;
            }
        }
    }

    public IEnumerator SeguirCaminhoDeWaypoints(List<Transform> caminho, float velInicial, float velFinal, bool aceleracaoGradativa = false, bool permitirPular = true)
    {
        int totalWaypoints = caminho.Count;
        bool carrierTakeoff = aceleracaoGradativa && aeroportoOrigem is GerenciadorPortaAvioes;
        bool carrierLanding = !aceleracaoGradativa && aeroportoOrigem is GerenciadorPortaAvioes;
        if (carrierLanding)
        {
            permitirPular = true;
        }

        // Otimização: Não volta pro waypoint [0] se o avião já estiver na frente (apenas se permitirPular for true)
        int indiceInicial = 0;
        if (permitirPular)
        {
            float menorDist = float.MaxValue;
            for (int i = 0; i < totalWaypoints; i++)
            {
                if (caminho[i] != null)
                {
                    float d = Vector3.Distance(transform.position, caminho[i].position);
                    if (d < menorDist)
                    {
                        menorDist = d;
                        indiceInicial = i;
                    }
                }
            }
            if (indiceInicial < totalWaypoints - 1 && menorDist < 10f) indiceInicial++;
        }

        // Aceleração gradativa começa apenas no final do percurso (Pista de decolagem)
        int indiceCorridaPista = indiceInicial;
        if (aceleracaoGradativa)
        {
            bool encontrouPreparaCarrier = false;
            if (carrierTakeoff)
            {
                for (int i = indiceInicial; i < totalWaypoints; i++)
                {
                    if (caminho[i] != null && caminho[i].name.IndexOf("prepara", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        indiceCorridaPista = i;
                        encontrouPreparaCarrier = true;
                        break;
                    }
                }
            }

            if (!encontrouPreparaCarrier)
            {
                bool encontrouAlinhamento = false;
                for (int i = totalWaypoints - 1; i >= indiceInicial; i--)
                {
                    if (caminho[i] != null && caminho[i].name.IndexOf("alinhamento", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        indiceCorridaPista = i;
                        encontrouAlinhamento = true;
                        break;
                    }
                }

                if (!encontrouAlinhamento)
                {
                    indiceCorridaPista = Mathf.Max(indiceInicial, totalWaypoints - 2);
                }
            }
        }

        float divisorPista = (totalWaypoints - indiceCorridaPista) > 1 ? (totalWaypoints - indiceCorridaPista - 1) : 1f;
        
        for (int i = indiceInicial; i < totalWaypoints; i++)
        {
            if (caminho[i] == null) continue;
            
            // --- CONTROLE DE FILA E DECOLAGEM DO PORTA-AVIÕES ---
            bool ehPrepara = string.Equals(caminho[i].name.Trim(), "prepara", StringComparison.OrdinalIgnoreCase);
            GerenciadorPortaAvioes carrier = aeroportoOrigem as GerenciadorPortaAvioes;
            
            if (ehPrepara && carrier != null)
            {
                // Aguarda até o ponto prepara ser liberado pelo avião anterior
                while (carrier.IsPreparaBusy(this))
                {
                    yield return new WaitForSeconds(0.5f);
                }
                // Reserva o ponto prepara
                carrier.ReservePrepara(this);
            }
            
            float velAtual = velInicial;
            if (carrierLanding)
            {
                velAtual = Mathf.Max(velInicial, velocidadeSolo * 2f);
            }
            else if (aceleracaoGradativa && i >= indiceCorridaPista)
            {
                velAtual = Mathf.Lerp(velInicial, velFinal, (i - indiceCorridaPista) / divisorPista);
            }
            
            // Segurança: O waypoint pode ser destruído durante o percurso
            bool ignoreRotationSlowdown = (aceleracaoGradativa && i >= indiceCorridaPista) || carrierLanding;
            yield return StartCoroutine(MoverInterpolado(Vector3.zero, velAtual, i == totalWaypoints - 1, caminho[i], ignoreRotationSlowdown));
            
            // Chegada no ponto prepara: sobe a rampa e aguarda 7 segundos
            if (caminho[i] != null && string.Equals(caminho[i].name.Trim(), "prepara", StringComparison.OrdinalIgnoreCase))
            {
                if (carrier != null)
                {
                    carrier.SubirRampa();
                    yield return new WaitForSeconds(7f);
                }
            }
            else if (caminho[i] != null && caminho[i].name.StartsWith("Prepara", StringComparison.OrdinalIgnoreCase))
            {
                yield return new WaitForSeconds(0.75f);
            }

            // Ao sair do prepara para o próximo ponto (ex: prepara 1): desce a rampa e libera a fila
            if (caminho[i] != null && string.Equals(caminho[i].name.Trim(), "prepara", StringComparison.OrdinalIgnoreCase))
            {
                if (carrier != null)
                {
                    carrier.ReleasePrepara(this);
                    carrier.DescerRampa();
                }
            }
            
            if (caminho[i] != null && caminho[i].name.IndexOf("alinhamento", StringComparison.OrdinalIgnoreCase) >= 0) 
            {
                // Parada exigida de forma realista antes de decolar (ou no pouso)
                yield return new WaitForSeconds(carrierLanding ? 0.15f : 2f);

                // Rotaciona para o próximo ponto antes de acelerar livremente
                if (i + 1 < totalWaypoints && caminho[i + 1] != null)
                {
                    Vector3 dir = caminho[i + 1].position - transform.position;
                    dir.y = 0;
                    if (dir.sqrMagnitude > 0.05f)
                    {
                        float rotationTimeout = 3f;
                        float rotTime = 0f;
                        while (rotTime < rotationTimeout)
                        {
                            Vector3 currentDir = caminho[i + 1].position - transform.position;
                            currentDir.y = 0;
                            if (currentDir.sqrMagnitude < 0.05f) break;
                            
                            Quaternion rotAlvo = Quaternion.LookRotation(currentDir.normalized);
                            if (Quaternion.Angle(transform.rotation, rotAlvo) <= 1.5f) break;

                            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, 120f * Time.deltaTime);
                            rotTime += Time.deltaTime;
                            yield return null;
                        }
                    }
                }
            }
        }
    }

    public void IniciarMissaoCompleta(Vector3 alvoFinalGPS)
    {
        if (!PodeIgnorarFaltaDeCombustivel() && !CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        if (estadoAtual == EstadoAviao.ProntoNoPatio)
        {
            bool emPatrulhaSalva = _controleUnidade != null && _controleUnidade.OrdemAtual == OrdemControleUnidade.Patrulhando && rotaPatrulhaSalva.Count > 1;
            if (!emPatrulhaSalva)
            {
                RegistrarMissaoManual(alvoFinalGPS);
            }
            else if (ultimoObjetivoMissao == Vector3.zero)
            {
                ultimoObjetivoMissao = alvoFinalGPS;
            }
            alvoEstrategico = alvoFinalGPS;
            alvoGPSVoo = alvoFinalGPS;
            Vector3 deslocamentoInicial = transform.position - alvoFinalGPS;
            deslocamentoInicial.y = 0f;
            if (deslocamentoInicial.sqrMagnitude > 1f)
            {
                anguloOrbitaAtual = Mathf.Atan2(deslocamentoInicial.z, deslocamentoInicial.x);
            }
            else
            {
                anguloOrbitaAtual = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            }

            sentidoOrbita = UnityEngine.Random.value >= 0.5f ? 1 : -1;
            StartCoroutine(SequenciaDeVooEPouso());
        }
    }

    public void ComandoRetornarBase()
    {
        if (estadoAtual == EstadoAviao.EmMissao || estadoAtual == EstadoAviao.Decolando)
        {
            ordemParaRetorno = true;
        }
    }

    public void PararPorFaltaDeCombustivel()
    {
        if (PodeIgnorarFaltaDeCombustivel())
        {
            return;
        }

        bool falhaDurantePousoOuDecolagem = (estadoAtual == EstadoAviao.Pousando || estadoAtual == EstadoAviao.Decolando || estadoAtual == EstadoAviao.EmMissao)
            && transform.position.y > 4f;

        if (!estaEmModoVooFisico && !falhaDurantePousoOuDecolagem)
        {
            ordemParaRetorno = false;
            aguardandoCliqueRadar = false;
            multiplicadorVelocidadeTurbo = 1f;

            if (modeloMecanicoVisual != null)
            {
                modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.Euler(0f, giroLateralYInicial, 0f), 0.45f);
            }
            return;
        }

        estaEmModoVooFisico = false;
        ordemParaRetorno = false;
        aguardandoCliqueRadar = false;
        multiplicadorVelocidadeTurbo = 1f;

        if (transform.position.y > 5f)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            FalhaAereaFisica.Ativar(gameObject, rb, velocidadeMaximaVoo * 0.4f, 5f, false, _sistemaDanos);
        }
    }

    public void DefinirBaseAlternativaEIniciarRetorno(GerenciadorAeroporto novaBase)
    {
        if (novaBase == null)
        {
            return;
        }

        aeroportoOrigem = novaBase;
        if (novaBase is GerenciadorPortaAvioes)
        {
            DefinirProtecaoCombustivelCarrier(true);
        }

        if (estadoAtual == EstadoAviao.EmMissao || estadoAtual == EstadoAviao.Pousando || estadoAtual == EstadoAviao.Decolando)
        {
            ComandoRetornarBase();
            return;
        }

        if (estadoAtual == EstadoAviao.ProntoNoPatio || estadoAtual == EstadoAviao.Taxiando || estadoAtual == EstadoAviao.ReservaHangar || estadoAtual == EstadoAviao.RetornandoPraVaga)
        {
            retornoAutomaticoAposChegadaCentro = true;
            Vector3 pontoAproximacaoBase = ObterPontoAproximacaoDaBase(novaBase);
            IniciarMissaoCompleta(pontoAproximacaoBase);
        }
    }

    public void RegistrarMissaoManual(Vector3 destino)
    {
        ultimoObjetivoMissao = destino;
        if (ultimoObjetivoMissao.y < 60f)
        {
            ultimoObjetivoMissao.y = 60f;
        }

        if (rotaPatrulhaSalva.Count > 0)
        {
            rotaPatrulhaSalva.Clear();
        }
    }

    public void RegistrarPatrulha(IList<Vector3> rota)
    {
        rotaPatrulhaSalva.Clear();
        if (rota == null || rota.Count == 0)
        {
            return;
        }

        for (int i = 0; i < rota.Count; i++)
        {
            Vector3 ponto = rota[i];
            if (ponto.y < 60f)
            {
                ponto.y = 60f;
            }
            rotaPatrulhaSalva.Add(ponto);
        }

        ultimoObjetivoMissao = rotaPatrulhaSalva[rotaPatrulhaSalva.Count - 1];

        // Atualiza a rota imediatamente se já estiver no ar
        if (estadoAtual == EstadoAviao.EmMissao)
        {
            AtualizarDestinoPatrulha(rotaPatrulhaSalva[0]);
        }
    }

    public void AtualizarDestinoPatrulha(Vector3 destino)
    {
        if (destino.y < 60f)
        {
            destino.y = 60f;
        }

        centroDaPatrulha = destino;
        alvoGPSVoo = destino;
        ultimoObjetivoMissao = destino;
        alvoPrioritarioIA = false;
    }

    private void AvaliarRetornoSeguroAutomatico()
    {
        CombustivelUnidade combustivel = GetComponent<CombustivelUnidade>();
        if (combustivel == null || !combustivel.usaCombustivel || estadoAtual != EstadoAviao.EmMissao || ordemParaRetorno)
        {
            return;
        }

        GerenciadorAeroporto baseSegura = EncontrarMelhorBaseRetorno();
        if (DeveRetornarAgora(combustivel, baseSegura))
        {
            PrepararRetornoSeguro(baseSegura, "combustivel");
        }
    }

    private bool DeveRetornarAgora(CombustivelUnidade combustivel, GerenciadorAeroporto baseSegura)
    {
        if (combustivel == null)
        {
            return false;
        }

        float percentualVida = 1f;
        if (_sistemaDanos != null && _sistemaDanos.vidaMaxima > 0f)
        {
            percentualVida = Mathf.Clamp01(_sistemaDanos.vidaAtual / _sistemaDanos.vidaMaxima);
        }

        if (percentualVida <= 0.22f)
        {
            return true;
        }

        float distancia = DistanciaAteBase(baseSegura);
        float consumoRetorno = combustivel.EstimarConsumoParaDistancia(distancia, Mathf.Max(60f, velocidadeMaximaVoo));
        float reservaPercentual = percentualVida < 0.55f ? reservaRetornoComDanosPercentual : reservaMinimaRetornoPercentual;
        float reservaCombustivel = Mathf.Max(combustivel.Capacidade * reservaPercentual, consumoRetorno * 0.45f);

        if (percentualVida < 0.40f)
        {
            reservaCombustivel += combustivel.Capacidade * 0.08f;
        }

        float minimoSeguro = consumoRetorno + reservaCombustivel;
        if (combustivel.CombustivelAtual <= minimoSeguro)
        {
            return true;
        }

        return combustivel.Percentual <= 0.12f;
    }

    private void PrepararRetornoSeguro(GerenciadorAeroporto baseSegura, string motivo)
    {
        ultimoMotivoRetorno = motivo;
        retomarMissaoAposAbastecer = motivo == "combustivel";

        if (baseSegura != null && baseSegura != aeroportoOrigem)
        {
            aeroportoOrigem = baseSegura;
        }

        if (baseSegura is GerenciadorPortaAvioes)
        {
            DefinirProtecaoCombustivelCarrier(true);
        }

        ComandoRetornarBase();
    }

    private GerenciadorAeroporto EncontrarMelhorBaseRetorno()
    {
        GerenciadorAeroporto melhorBase = aeroportoOrigem;
        float melhorDistancia = DistanciaAteBase(melhorBase);

        int meuTime = ObterTeamId();
        GerenciadorPortaAvioes[] carriers = FindObjectsByType<GerenciadorPortaAvioes>(FindObjectsSortMode.None);
        for (int i = 0; i < carriers.Length; i++)
        {
            GerenciadorPortaAvioes carrier = carriers[i];
            if (carrier == null || !PertenceAoMesmoTime(carrier, meuTime))
            {
                continue;
            }

            float distancia = DistanciaAteBase(carrier);
            if (distancia < melhorDistancia)
            {
                melhorDistancia = distancia;
                melhorBase = carrier;
            }
        }

        return melhorBase;
    }

    private float DistanciaAteBase(GerenciadorAeroporto baseDestino)
    {
        if (baseDestino == null)
        {
            return 999999f;
        }

        Vector3 alvoBase = ObterPontoAproximacaoDaBase(baseDestino);
        alvoBase.y = transform.position.y;
        return Vector3.Distance(transform.position, alvoBase);
    }

    private int ObterTeamId()
    {
        IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
        return identidade != null ? identidade.teamID : 0;
    }

    private bool PertenceAoMesmoTime(Component componente, int meuTime)
    {
        if (componente == null || meuTime <= 0)
        {
            return true;
        }

        IdentidadeUnidade identidade = componente.GetComponent<IdentidadeUnidade>();
        if (identidade == null)
        {
            identidade = componente.GetComponentInParent<IdentidadeUnidade>();
        }

        return identidade == null || identidade.teamID <= 0 || identidade.teamID == meuTime;
    }

    private bool EhCacaOperacional()
    {
        return GetComponent<LancadorMisselCaca>() != null
            || GetComponent<ControleAviaoCaca>() != null
            || gameObject.name.IndexOf("caca", StringComparison.OrdinalIgnoreCase) >= 0
            || gameObject.name.IndexOf("fighter", StringComparison.OrdinalIgnoreCase) >= 0
            || gameObject.name.IndexOf("jet", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ProcessarRetomadaAposReabastecimento()
    {
        if (!retomarMissaoAposAbastecer || estadoAtual != EstadoAviao.ProntoNoPatio)
        {
            return;
        }

        retomarMissaoAposAbastecer = false;
        if (rotinaRetomadaMissao != null)
        {
            StopCoroutine(rotinaRetomadaMissao);
        }
        rotinaRetomadaMissao = StartCoroutine(RotinaRetomarMissaoAposReabastecimento());
    }

    private IEnumerator RotinaRetomarMissaoAposReabastecimento()
    {
        yield return new WaitForSeconds(0.35f);

        if (estadoAtual != EstadoAviao.ProntoNoPatio)
        {
            rotinaRetomadaMissao = null;
            yield break;
        }

        if (_controleUnidade != null && rotaPatrulhaSalva.Count > 1)
        {
            _controleUnidade.EmitirOrdemPatrulha(new List<Vector3>(rotaPatrulhaSalva));
        }
        else if (ultimoObjetivoMissao != Vector3.zero)
        {
            IniciarMissaoCompleta(ultimoObjetivoMissao);
        }

        rotinaRetomadaMissao = null;
    }

    private static Vector3 ObterPontoAproximacaoDaBase(GerenciadorAeroporto baseDestino)
    {
        if (baseDestino == null)
        {
            return Vector3.zero;
        }

        if (baseDestino.waypointsDecida != null && baseDestino.waypointsDecida.Count > 0 && baseDestino.waypointsDecida[0] != null)
        {
            return baseDestino.waypointsDecida[0].position;
        }

        if (baseDestino.wpPronto != null)
        {
            return baseDestino.wpPronto.position;
        }

        return baseDestino.transform.position;
    }

    protected virtual List<Transform> ObterWaypointsDecolagem()
    {
        return aeroportoOrigem != null ? aeroportoOrigem.waypointsDecolagem : new List<Transform>();
    }

    protected virtual List<Transform> ObterWaypointsDecida()
    {
        return aeroportoOrigem != null ? aeroportoOrigem.waypointsDecida : new List<Transform>();
    }

    protected virtual List<Transform> ObterWaypointsTaxi()
    {
        return aeroportoOrigem != null ? aeroportoOrigem.waypointsTaxi : new List<Transform>();
    }

    protected virtual Transform ObterWpPreparacao()
    {
        return aeroportoOrigem != null ? aeroportoOrigem.wpPreparacao : null;
    }

    protected virtual Transform ObterWpPronto()
    {
        return aeroportoOrigem != null ? aeroportoOrigem.wpPronto : null;
    }

    protected virtual List<Transform> ObterWaypointsTaxiEntrada()
    {
        return new List<Transform>();
    }

    protected virtual IEnumerator SequenciaDeVooEPouso()
    {
        if (aeroportoOrigem == null) 
        {
            Destroy(gameObject); // Sem aeroporto, explode/se sacrifica
            yield break;
        }

        ordemParaRetorno = false;
        DefinirEstado(EstadoAviao.Decolando);
        if (vagaRetorno == null && transform.parent != null)
        {
            // Em porta-aviões, preserva a vaga de origem para retornar ao mesmo ponto.
            vagaRetorno = transform.parent;
        }

        // === [DECOLAGEM] ===
        var wpDecolagem = ObterWaypointsDecolagem();
        var wpsTaxiEntrada = ObterWaypointsTaxiEntrada();
        List<Transform> caminhoDecolagem = new List<Transform>();
        
        if (wpsTaxiEntrada != null)
        {
            for (int i = 0; i < wpsTaxiEntrada.Count; i++)
            {
                if (wpsTaxiEntrada[i] != null)
                    caminhoDecolagem.Add(wpsTaxiEntrada[i]);
            }
        }
        
        if (wpDecolagem != null)
        {
            for (int i = 0; i < wpDecolagem.Count; i++)
            {
                if (wpDecolagem[i] != null)
                    caminhoDecolagem.Add(wpDecolagem[i]);
            }
        }

        if (caminhoDecolagem.Count > 0)
            {
                // Giro realista: aponta suavemente para o início da pista antes de começar a andar
                Vector3 dirParaPista = caminhoDecolagem[0].position - transform.position;
                dirParaPista.y = 0f;
                if (dirParaPista.sqrMagnitude > 0.1f)
                {
                    Quaternion rotAlvo = Quaternion.LookRotation(dirParaPista.normalized);
                    while (Quaternion.Angle(transform.rotation, rotAlvo) > 5f)
                    {
                        Vector3 dir2 = caminhoDecolagem[0].position - transform.position;
                        dir2.y = 0f;
                        if (dir2.sqrMagnitude > 0.01f)
                            rotAlvo = Quaternion.LookRotation(dir2.normalized);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, 150f * Time.deltaTime);
                        yield return null;
                    }
                }

                // Táxi e decolagem usando waypoints (a velocidade baseia-se na de solo primeiro)
                float velocidadeSaidaConves = aeroportoOrigem is GerenciadorPortaAvioes
                    ? Mathf.Min(velocidadeMaximaVoo, 55f)
                    : velocidadeMaximaVoo;
                yield return StartCoroutine(SeguirCaminhoDeWaypoints(caminhoDecolagem, velocidadeSolo, velocidadeSaidaConves, true, false));
                velocidadeVooAtual = velocidadeSaidaConves;
            }
        else
        {
            yield return new WaitForSeconds(0.5f); // Pequena pausa fallback se não houver pista
        }

        transform.SetParent(null, true);
        estaEmModoVooFisico = true;
        DefinirEstado(EstadoAviao.EmMissao);
        if (alvoGPSVoo.y < altitudeVoo) alvoGPSVoo.y = altitudeVoo;
        centroDaPatrulha = alvoGPSVoo;
        StartCoroutine(RecolherRodas(1f));

        // Voa até o centro da patrulha
        float margemChegadaSqr = Mathf.Max(20f, margemChegadaMissao) * Mathf.Max(20f, margemChegadaMissao);
        while (true)
        {
            Vector3 diff = new Vector3(transform.position.x - centroDaPatrulha.x, 0, transform.position.z - centroDaPatrulha.z);
            if (diff.sqrMagnitude <= margemChegadaSqr) break;
            if (ordemParaRetorno) break;
            alvoGPSVoo = centroDaPatrulha; 
            yield return null;
        }

        if (retornoAutomaticoAposChegadaCentro)
        {
            retornoAutomaticoAposChegadaCentro = false;
            ordemParaRetorno = true;
        }

        // Loop de patrulha
        KamikazeDrone droneScript = GetComponent<KamikazeDrone>();
        
        Vector3[] pontosRetangulo = new Vector3[4];
        Vector3 ultimoCentroPatrulha = Vector3.zero;
        float ultimoRaio = -1f;
        
        float tempoUltimaTrocaCentro = 0f;
        Vector3 offsetPatrulha = Vector3.zero;

        while (!ordemParaRetorno)
        {
            if (droneScript != null)
            {
                // Drones kamikaze não fazem patrulha: miram 100% no alvo definido
                if (!droneScript.kamikazeAtivo) alvoGPSVoo = centroDaPatrulha;
                else alvoGPSVoo = alvoEstrategico;
            }
            else if (emAtaqueMergulho) 
            {
                alvoGPSVoo = alvoDoMergulho;
            }
            else if (!alvoPrioritarioIA)
            {
                if (Time.time - tempoUltimaTrocaCentro > 45f)
                {
                    tempoUltimaTrocaCentro = Time.time;
                    offsetPatrulha = new Vector3(UnityEngine.Random.Range(-100f, 100f), 0, UnityEngine.Random.Range(-100f, 100f));
                }

                Vector3 centroAtualizado = centroDaPatrulha + offsetPatrulha;
                float raio = Mathf.Max(280f, raioOrbitaMissao * 7f);
                float baseY = Mathf.Max(centroAtualizado.y, altitudeVoo);

                if (centroAtualizado != ultimoCentroPatrulha || raio != ultimoRaio)
                {
                    ultimoCentroPatrulha = centroAtualizado;
                    ultimoRaio = raio;

                    // Retângulo alongado: 2x maior na frente/trás do que pros lados
                    pontosRetangulo[0] = centroAtualizado + new Vector3(raio * 2f, 30f, raio);
                    pontosRetangulo[1] = centroAtualizado + new Vector3(-raio * 2f, -10f, raio);
                    pontosRetangulo[2] = centroAtualizado + new Vector3(-raio * 2f, 30f, -raio);
                    pontosRetangulo[3] = centroAtualizado + new Vector3(raio * 2f, -10f, -raio);

                    for (int i = 0; i < 4; i++) {
                        pontosRetangulo[i].y = Mathf.Max(baseY + ((i % 2 == 0) ? 30f : -15f), altitudeVoo);
                    }
                }

                Vector3 alvoDest = pontosRetangulo[indiceRetanguloPatrulha];
                // Suaviza muito mais a transição de alvo, gerando curva realista ampla
                alvoGPSVoo = Vector3.Lerp(alvoGPSVoo, alvoDest, Time.deltaTime * 0.2f);

                // Checa distância em relação ao canto real (alvoDest) em vez do alvo interpolado (alvoGPSVoo)
                float distSqr = (new Vector3(transform.position.x, 0, transform.position.z) - new Vector3(alvoDest.x, 0, alvoDest.z)).sqrMagnitude;
                if (distSqr < 40000f) // 200m de distância para trocar de ponto, fazendo a curva bem antes de chegar no vértice
                {
                    indiceRetanguloPatrulha = (indiceRetanguloPatrulha + 1) % 4;
                }

                Vector3 diffPatrulha = new Vector3(transform.position.x - centroAtualizado.x, 0, transform.position.z - centroAtualizado.z);
                float raioSeguranca = Mathf.Max(raio * 4f, 500f);
                if (diffPatrulha.sqrMagnitude > raioSeguranca * raioSeguranca) alvoGPSVoo = centroAtualizado;
            }
            yield return null;
        }

        // --- RETORNO À BASE ---
        ordemParaRetorno = false;
        retornoAutomaticoAposChegadaCentro = false;
        DefinirEstado(EstadoAviao.Pousando);
        if (aeroportoOrigem is GerenciadorPortaAvioes)
        {
            DefinirProtecaoCombustivelCarrier(true);
        }

        if (aeroportoOrigem == null || aeroportoOrigem.waypointsDecida == null || aeroportoOrigem.waypointsDecida.Count < 2)
        {
            var dmg = GetComponent<SistemaDeDanos>();
            if (dmg) dmg.ReceberDano(9999f); else Destroy(gameObject);
            yield break;
        }

        GerenciadorPortaAvioes carrier = aeroportoOrigem as GerenciadorPortaAvioes;
        if (vagaRetorno == null)
        {
            vagaRetorno = aeroportoOrigem.ObterPrimeiraVagaLivre();
        }
        if (vagaRetorno != null && !aeroportoOrigem.avioesNoPatio.Contains(this))
            aeroportoOrigem.avioesNoPatio.Add(this);

        // 2. Fase de aproximação no ar: o índice [0] é o ponto sintético 600m atrás do navio
        Vector3 pontoAproximacao = aeroportoOrigem.waypointsDecida[0].position;
        Vector3 pontoTouchdown   = aeroportoOrigem.waypointsDecida[1].position;

        alvoGPSVoo = pontoAproximacao;
        if (alvoGPSVoo.y < 50f) alvoGPSVoo.y = 50f;

        bool indoParaTouchdown = false;
        float velOriginalVoo = velocidadeMaximaVoo;

        while (true)
        {
            Vector3 alvoReal = indoParaTouchdown ? pontoTouchdown : pontoAproximacao;
            alvoGPSVoo = Vector3.Lerp(alvoGPSVoo, alvoReal, Time.deltaTime * 1.5f);

            Vector3 diffAprox = new Vector3(transform.position.x - pontoAproximacao.x, 0, transform.position.z - pontoAproximacao.z);
            Vector3 diffTD    = new Vector3(transform.position.x - pontoTouchdown.x,   0, transform.position.z - pontoTouchdown.z);

            if (!indoParaTouchdown)
            {
                if (diffAprox.sqrMagnitude <= 90000f) indoParaTouchdown = true; // 300 m
            }
            else
            {
                // Reduz a velocidade quando estiver a 100 metros (10000 = 100^2)
                if (diffTD.sqrMagnitude <= 10000f)
                {
                    velocidadeMaximaVoo = velocidadeSolo * 2.4f;
                }

                if (diffTD.sqrMagnitude <= 900f) break; // 30 m do 1º waypoint real
            }

            if (diffTD.sqrMagnitude < 250000f) AbaixarRodas();
            yield return null;
        }

        velocidadeMaximaVoo = velOriginalVoo; // Restaura a velocidade original para não quebrar o próximo voo

        // 3. Glideslope: segue os filhos do "Pouso" (índice 1 em diante, inclui "Parando")
        AbaixarRodas();
        estaEmModoVooFisico = false;

        List<Transform> caminhoPouso = new List<Transform>();
        for (int i = 1; i < aeroportoOrigem.waypointsDecida.Count; i++)
        {
            if (aeroportoOrigem.waypointsDecida[i] != null)
                caminhoPouso.Add(aeroportoOrigem.waypointsDecida[i]);
        }
        // Segue glideslope na velocidade de solo (lenta), permitindo pular waypoints já passados
        float velocidadeTaxiPouso = carrier != null ? Mathf.Max(velocidadeSolo * 2f, 18f) : velocidadeSolo * 2.4f;
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(caminhoPouso, velocidadeTaxiPouso, velocidadeTaxiPouso, false, false));

        // 4. Parentar ao convés do navio
        Transform paiConves = (caminhoPouso.Count > 0 && caminhoPouso[caminhoPouso.Count - 1] != null)
            ? (caminhoPouso[caminhoPouso.Count - 1].parent ?? aeroportoOrigem.transform)
            : aeroportoOrigem.transform;
        transform.SetParent(paiConves, true);

        // 5. Fase de Táxi ou Bypass pro Porta-Aviões
        DefinirEstado(EstadoAviao.RetornandoPraVaga);
        if (carrier != null)
        {
            while (carrier != null && vagaRetorno == null)
            {
                vagaRetorno = carrier.ObterPrimeiraVagaLivre();
                if (vagaRetorno == null) yield return new WaitForSeconds(1f);
            }
        }

        if (carrier == null && aeroportoOrigem != null)
        {
            // Aeroportos padrões usam o táxi normal
            if (aeroportoOrigem.waypointsTaxi != null && aeroportoOrigem.waypointsTaxi.Count > 0)
            {
                yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsTaxi, velocidadeSolo, velocidadeSolo, false, true));
                if (_sistemaDanos != null) _sistemaDanos.Reparar(_sistemaDanos.vidaMaxima);
            }
            else
            {
                if (aeroportoOrigem.wpAndadar != null)
                    yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, aeroportoOrigem.wpAndadar));
                
                if (aeroportoOrigem.wpAnalise != null)
                {
                    yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, aeroportoOrigem.wpAnalise));
                    if (_sistemaDanos != null) _sistemaDanos.Reparar(_sistemaDanos.vidaMaxima);
                }
            }
        }

        // 6. Busca da Vaga Final
        if (vagaRetorno != null)
        {
            if (carrier == null)
            {
                // Aeroporto padrão faz o giro realista e o táxi final
                Vector3 dirParaVaga = vagaRetorno.position - transform.position;
                dirParaVaga.y = 0f;
                if (dirParaVaga.sqrMagnitude > 0.1f)
                {
                    Quaternion rotAlvo = Quaternion.LookRotation(dirParaVaga.normalized);
                    while (Quaternion.Angle(transform.rotation, rotAlvo) > 5f)
                    {
                        Vector3 dir2 = vagaRetorno.position - transform.position;
                        dir2.y = 0f;
                        if (dir2.sqrMagnitude > 0.01f)
                            rotAlvo = Quaternion.LookRotation(dir2.normalized);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, 150f * Time.deltaTime);
                        yield return null;
                    }
                }

                // Táxi final lento para a vaga exata
                yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, vagaRetorno));
            }
            else
            {
                // Porta-aviões: pouso termina direto na vaga reservada, sem teleportar para outro ponto.
                yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, vagaRetorno));
            }

            // 6. Estaciona: parentar à própria vaga e alinhar
            transform.SetParent(vagaRetorno, true);
            float alturaOffset = carrier != null ? Mathf.Min(0.25f, ObterAlturaEstacionamento() * 0.1f) : ObterAlturaEstacionamento();
            transform.localPosition = new Vector3(0f, alturaOffset, 0f);

            // Giro final suave para ângulo zero local (alinhar com a vaga)
            while (Quaternion.Angle(transform.localRotation, Quaternion.identity) > 1f)
            {
                transform.localRotation = Quaternion.RotateTowards(
                    transform.localRotation, Quaternion.identity, 30f * Time.deltaTime);
                yield return null;
            }
            transform.localRotation = Quaternion.identity;
            transform.localPosition = new Vector3(0f, alturaOffset, 0f);

            // Garante que o voo físico NÃO seja reativado após o estacionamento
            estaEmModoVooFisico = false;
            DefinirEstado(EstadoAviao.ProntoNoPatio);
            ProcessarServicoDeBaseAposPouso();
            
            // Retoma patrulha automaticamente se o avião estava em missão e precisou abastecer
            ProcessarRetomadaAposReabastecimento();
        }
        else if (carrier == null)
        {
            // Pátio lotado → hangar
            ProcessarServicoDeBaseAposPouso();
            if (aeroportoOrigem != null) aeroportoOrigem.GuardarNoHangarAutomatico(this);
            ProcessarRetomadaAposReabastecimento();
        }
    }

    private void ProcessarServicoDeBaseAposPouso()
    {
        if (aeroportoOrigem is GerenciadorPortaAvioes)
        {
            GerenciadorPortaAvioes.ReabastecerAeronaveCarrier(this, false);
        }
        else
        {
            ReabastecerSeAbaixoDe(0.50f);
        }

        DefinirProtecaoCombustivelCarrier(false);
    }

    public void ReabastecerSeAbaixoDe(float percentualMinimo)
    {
        CombustivelUnidade combustivel = GetComponent<CombustivelUnidade>();
        if (combustivel == null || !combustivel.usaCombustivel || combustivel.Capacidade <= 0f)
        {
            return;
        }

        if (combustivel.Percentual <= Mathf.Clamp01(percentualMinimo))
        {
            combustivel.PreencherSemCusto();
        }
    }

    protected IEnumerator RecolherRodas(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (rodasRecolhidas) yield break;
        rodasRecolhidas = true;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime;
            for (int i = 0, count = rodas.Count; i < count; i++)
            {
                if (rodas[i] != null && i < rotacoesOriginaisRodas.Count)
                    rodas[i].localRotation = Quaternion.Slerp(rotacoesOriginaisRodas[i], rotacoesOriginaisRodas[i] * Quaternion.Euler(-50f, 0f, 0f), t);
            }
            yield return null;
        }
        for (int i = 0, count = rodas.Count; i < count; i++)
            if (rodas[i] != null) rodas[i].gameObject.SetActive(false);
    }

    protected void AbaixarRodas()
    {
        if (!rodasRecolhidas) return;
        rodasRecolhidas = false;
        for (int i = 0, count = rodas.Count; i < count; i++)
        {
            if (rodas[i] != null && i < rotacoesOriginaisRodas.Count) 
            { 
                rodas[i].gameObject.SetActive(true); 
                rodas[i].localRotation = rotacoesOriginaisRodas[i]; 
            }
        }
    }

    public void ForcarAtaqueMergulho(Vector3 direcaoRetoAtaque)
    {
        if (!emAtaqueMergulho) StartCoroutine(RotinaMergulho(direcaoRetoAtaque));
    }

    private IEnumerator RotinaMergulho(Vector3 pontoFinal)
    {
        emAtaqueMergulho = true;
        float velOriginal = velocidadeMaximaVoo;
        alvoDoMergulho = transform.position + transform.forward * 120f; 
        alvoDoMergulho.y = 150f;
        velocidadeMaximaVoo = velOriginal * 0.4f;
        yield return new WaitForSeconds(2.0f);
        alvoDoMergulho = pontoFinal;
        velocidadeMaximaVoo = velOriginal * 0.8f;
        yield return new WaitForSeconds(3.5f);
        velocidadeMaximaVoo = velOriginal;
        emAtaqueMergulho = false;
    }

    public bool PodeIgnorarFaltaDeCombustivel()
    {
        return protecaoCombustivelCarrier;
    }

    private void DefinirProtecaoCombustivelCarrier(bool ativa)
    {
        protecaoCombustivelCarrier = ativa;
    }
}
