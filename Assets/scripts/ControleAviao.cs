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
    public float taxaDeGiroLeme = 120f;    

    [Header("=== ÓRBITA DA MISSÃO ===")]
    [Tooltip("Raio horizontal da órbita em torno da área ordenada.")]
    public float raioOrbitaMissao = 85f;
    [Tooltip("Velocidade angular da órbita em torno do alvo/patrulha.")]
    public float velocidadeOrbitaMissao = 0.9f;
    [Tooltip("Distância para considerar que chegou ao centro inicial da missão.")]
    public float margemChegadaMissao = 65f;

    [Header("=== ANIMAÇÃO VISUAL ===")]
    public Transform modeloMecanicoVisual; 
    public float asaBankingMaximo = 75f; 
    public float arfagemPitchMaxima = 30f; 

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
    private float giroLateralRoll = 0f; 
    private float empinadaPitch = 0f;   
    private float multiplicadorVelocidadeTurbo = 1f;
    private float tempoSegurandoTab = 0f;
    private float anguloOrbitaAtual = 0f;
    private int sentidoOrbita = 1;
    private bool retornoAutomaticoAposChegadaCentro = false;
    private readonly List<Vector3> rotaPatrulhaSalva = new List<Vector3>();
    private Vector3 ultimoObjetivoMissao = Vector3.zero;
    private bool retomarMissaoAposAbastecer = false;
    private string ultimoMotivoRetorno = string.Empty;
    private Coroutine rotinaRetomadaMissao;

    // --- CACHE DE COMPONENTES (evita GetComponent no Update) ---
    private ControleUnidade _controleUnidade;
    private SistemaDeDanos _sistemaDanos;

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true; 

        // Cache de componentes usados no Update
        _controleUnidade = GetComponent<ControleUnidade>();
        _sistemaDanos = GetComponent<SistemaDeDanos>();

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
        return Mathf.Max(alturaBase, offset);
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

    void OnDestroy()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    void Update()
    {
        if (!estaEmModoVooFisico) return;

        AvaliarRetornoSeguroAutomatico();

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        float dt = Time.deltaTime;
        bool selecionado = (_controleUnidade != null && _controleUnidade.selecionado);

        float multiplicadorDanos = 1f;
        if (_sistemaDanos != null && _sistemaDanos.vidaMaxima > 0)
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
    }

    private void ManobraVooRealista(float multDano = 1f)
    {
        float dt = Time.deltaTime;
        Vector3 retaAteAlvo = alvoGPSVoo - transform.position;
        Quaternion olharMundoDesejado = Quaternion.LookRotation(retaAteAlvo);
        float anguloPressaoLateralY = Vector3.SignedAngle(transform.forward, retaAteAlvo, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, olharMundoDesejado, taxaDeGiroLeme * dt);
        
        float velFinal = (velocidadeMaximaVoo * multiplicadorVelocidadeTurbo) * multDano;
        Vector3 novaPos = transform.position + transform.forward * (velFinal * dt);

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
             novaPos = transform.position + transform.forward * (velocidadeMaximaVoo * dt);
        }

        transform.position = novaPos;

        if (modeloMecanicoVisual != null)
        {
            float inclinacaoAlvoZ = Mathf.Clamp(anguloPressaoLateralY * -2.5f, -asaBankingMaximo, asaBankingMaximo);
            float inclinacaoAlvoX = Mathf.Clamp(retaAteAlvo.y * -3.0f, -arfagemPitchMaxima, arfagemPitchMaxima);
            giroLateralRoll = Mathf.Lerp(giroLateralRoll, inclinacaoAlvoZ, dt * 5f);
            empinadaPitch = Mathf.Lerp(empinadaPitch, inclinacaoAlvoX, dt * 5f);
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, 0f, giroLateralRoll);
        }
    }

    public IEnumerator MoverInterpolado(Vector3 destinoFixo, float vel, bool pontoFinal = false, Transform alvoMovel = null, bool ignoreRotationSlowdown = false)
    {
        float raioDeAceitacao = pontoFinal ? 0.5f : 3.5f; // Aumentado para não engasgar em waypoints muito próximos
        float raioSqr = raioDeAceitacao * raioDeAceitacao;

        bool alvoMovelFornecido = (alvoMovel != null);

        while (true)
        {
            if (alvoMovelFornecido && alvoMovel == null) break; // O alvo (ex: vaga ou aeroporto) foi destruído no caminho

            if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
            {
                PararPorFaltaDeCombustivel();
                yield break;
            }

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
            if (direcaoHorizon != Vector3.zero)
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
            if (modeloMecanicoVisual != null) modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.identity, Time.deltaTime * 5f);
            yield return null;
        }

        Vector3 destinoFinal = (alvoMovel != null) ? alvoMovel.position : destinoFixo;
        if (pontoFinal && (transform.position - destinoFinal).sqrMagnitude < 25f) 
            transform.position = destinoFinal;
    }

    public IEnumerator SeguirCaminhoDeWaypoints(List<Transform> caminho, float velInicial, float velFinal, bool aceleracaoGradativa = false)
    {
        int totalWaypoints = caminho.Count;

        // Otimização: Não volta pro waypoint [0] se o avião já estiver na frente (ex: decolando do meio da pista)
        int indiceInicial = 0;
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

        // Aceleração gradativa começa apenas no final do percurso (Pista de decolagem)
        int indiceCorridaPista = indiceInicial;
        if (aceleracaoGradativa)
        {
            for (int i = totalWaypoints - 1; i >= indiceInicial; i--)
            {
                if (caminho[i] != null && caminho[i].name.IndexOf("alinhamento", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    indiceCorridaPista = i;
                    break;
                }
            }
        }

        float divisorPista = (totalWaypoints - indiceCorridaPista) > 1 ? (totalWaypoints - indiceCorridaPista - 1) : 1f;
        
        for (int i = indiceInicial; i < totalWaypoints; i++)
        {
            if (caminho[i] == null) continue;
            
            float velAtual = velInicial;
            if (aceleracaoGradativa && i >= indiceCorridaPista)
            {
                velAtual = Mathf.Lerp(velInicial, velFinal, (i - indiceCorridaPista) / divisorPista);
            }
            
            // Segurança: O waypoint pode ser destruído durante o percurso
            yield return StartCoroutine(MoverInterpolado(Vector3.zero, velAtual, i == totalWaypoints - 1, caminho[i], aceleracaoGradativa && i >= indiceCorridaPista));
            
            if (caminho[i] != null && caminho[i].name.IndexOf("alinhamento", StringComparison.OrdinalIgnoreCase) >= 0) 
            {
                // Parada exigida de forma realista antes de decolar (ou no pouso)
                yield return new WaitForSeconds(2f);

                // Rotaciona para o próximo ponto antes de acelerar livremente
                if (i + 1 < totalWaypoints && caminho[i + 1] != null)
                {
                    Vector3 dir = caminho[i + 1].position - transform.position;
                    dir.y = 0;
                    if (dir.sqrMagnitude > 0.05f)
                    {
                        Quaternion rotAlvo = Quaternion.LookRotation(dir.normalized);
                        while (Quaternion.Angle(transform.rotation, rotAlvo) > 1.5f)
                        {
                            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAlvo, 90f * Time.deltaTime);
                            yield return null;
                        }
                    }
                }
            }
        }
    }

    public void IniciarMissaoCompleta(Vector3 alvoFinalGPS)
    {
        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
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
        bool falhaDurantePousoOuDecolagem = (estadoAtual == EstadoAviao.Pousando || estadoAtual == EstadoAviao.Decolando || estadoAtual == EstadoAviao.EmMissao)
            && transform.position.y > 4f;

        if (!estaEmModoVooFisico && !falhaDurantePousoOuDecolagem)
        {
            ordemParaRetorno = false;
            aguardandoCliqueRadar = false;
            multiplicadorVelocidadeTurbo = 1f;

            if (modeloMecanicoVisual != null)
            {
                modeloMecanicoVisual.localRotation = Quaternion.Lerp(modeloMecanicoVisual.localRotation, Quaternion.identity, 0.45f);
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
        retomarMissaoAposAbastecer = EhCacaOperacional() && motivo == "combustivel";

        if (baseSegura != null && baseSegura != aeroportoOrigem)
        {
            aeroportoOrigem = baseSegura;
        }

        ComandoRetornarBase();
    }

    private GerenciadorAeroporto EncontrarMelhorBaseRetorno()
    {
        GerenciadorAeroporto melhorBase = aeroportoOrigem;
        float melhorDistancia = DistanciaAteBase(melhorBase);

        if (!EhCacaOperacional())
        {
            return melhorBase;
        }

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

    private IEnumerator SequenciaDeVooEPouso()
    {
        if (aeroportoOrigem == null) 
        {
            Destroy(gameObject); // Sem aeroporto, explode/se sacrifica
            yield break;
        }

        ordemParaRetorno = false;
        estadoAtual = EstadoAviao.Decolando;
        vagaRetorno = null; 
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsDecolagem, velocidadeSolo, velocidadeMaximaVoo, true));

        estaEmModoVooFisico = true;
        estadoAtual = EstadoAviao.EmMissao;
        if (alvoGPSVoo.y < 60f) alvoGPSVoo.y = 60f;
        centroDaPatrulha = alvoGPSVoo;
        StartCoroutine(RecolherRodas(3f));

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
                anguloOrbitaAtual += Time.deltaTime * velocidadeOrbitaMissao * sentidoOrbita;
                Vector3 offsetOrbita = new Vector3(
                    Mathf.Cos(anguloOrbitaAtual),
                    0f,
                    Mathf.Sin(anguloOrbitaAtual)) * Mathf.Max(25f, raioOrbitaMissao);

                alvoGPSVoo = centroDaPatrulha + offsetOrbita;
                alvoGPSVoo.y = Mathf.Max(centroDaPatrulha.y, 60f);

                Vector3 diffPatrulha = new Vector3(transform.position.x - centroDaPatrulha.x, 0, transform.position.z - centroDaPatrulha.z);
                float raioSeguranca = Mathf.Max(raioOrbitaMissao * 2.5f, 160f);
                if (diffPatrulha.sqrMagnitude > raioSeguranca * raioSeguranca) alvoGPSVoo = centroDaPatrulha;
            }
            yield return null;
        }

        // --- RETORNO À BASE ---
        ordemParaRetorno = false;
        retornoAutomaticoAposChegadaCentro = false;
        estadoAtual = EstadoAviao.Pousando;

        if (aeroportoOrigem == null || aeroportoOrigem.waypointsDecida == null || aeroportoOrigem.waypointsDecida.Count == 0)
        {
            // O Aeroporto sumiu ou os dados do aeroporto foram destruídos enquanto estávamos no céu!
            Debug.LogWarning($"[{gameObject.name}] Meu Aeroporto original foi DESTRUÍDO. Realizando Pouso Forçado/Ejeção.");
            var dmg = GetComponent<SistemaDeDanos>();
            if (dmg) dmg.ReceberDano(9999f); else Destroy(gameObject);
            yield break;
        }

        Vector3 pontoFreiada = aeroportoOrigem.waypointsDecida[0].position;
        alvoGPSVoo = pontoFreiada;
        if (alvoGPSVoo.y < 40f) alvoGPSVoo.y = 40f; 
        
        while (true)
        {
            Vector3 diff2D = new Vector3(transform.position.x - alvoGPSVoo.x, 0, transform.position.z - alvoGPSVoo.z);
            float distSqr = diff2D.sqrMagnitude;
            
            // Aumentado o raio de aceitação para 120m para garantir transição suave 
            // no ponto de aproximação distante do Porta-Aviões
            if (distSqr <= 14400f) break; // 120² = 14400
            
            if (distSqr < 250000f) AbaixarRodas(); // 500² = 250000
            yield return null;
        }

        AbaixarRodas();
        estaEmModoVooFisico = false;
        yield return StartCoroutine(SeguirCaminhoDeWaypoints(aeroportoOrigem.waypointsDecida, velocidadeMaximaVoo * 0.5f, velocidadeSolo, true));

        if (aeroportoOrigem == null) 
        {
            if (_sistemaDanos) _sistemaDanos.ReceberDano(9999f); else Destroy(gameObject);
            yield break;
        }

        estadoAtual = EstadoAviao.RetornandoPraVaga;
        if (aeroportoOrigem != null && aeroportoOrigem.wpAndadar != null) yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, aeroportoOrigem.wpAndadar));
        if (aeroportoOrigem != null && aeroportoOrigem.wpAnalise != null)
        {
            yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, aeroportoOrigem.wpAnalise));
            
            // REPARO AO CHEGAR: Restaura 100% da vida no ponto de análise
            if (_sistemaDanos != null) _sistemaDanos.Reparar(_sistemaDanos.vidaMaxima);
            ProcessarServicoDeBaseAposPouso();
            
            estadoAtual = EstadoAviao.ProntoNoPatio; 
            yield return new WaitForSeconds(3f);
            if (estadoAtual != EstadoAviao.ProntoNoPatio) yield break; 
        }

        if (aeroportoOrigem == null) yield break;

        Transform vagaSegura = aeroportoOrigem.ObterPrimeiraVagaLivre();
        if (vagaSegura != null)
        {
             vagaRetorno = vagaSegura;
             // Registro imediato para evitar que outros aviões pousem na mesma vaga
             if (!aeroportoOrigem.avioesNoPatio.Contains(this)) aeroportoOrigem.avioesNoPatio.Add(this);
              yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, vagaRetorno));
             
             // Segurança: A vaga ou o aeroporto podem ter sido destruídos durante o taxiamento (yield acima)
             if (vagaRetorno == null) yield break;

             estadoAtual = EstadoAviao.ProntoNoPatio;
             // Offset de altura para o avião ficar em cima do convés, não dentro da mesh
             float alturaOffset = ObterAlturaEstacionamento();
             transform.position = vagaRetorno.position + (vagaRetorno.up * alturaOffset);
             transform.rotation = vagaRetorno.rotation; 
             ProcessarServicoDeBaseAposPouso();
             ProcessarRetomadaAposReabastecimento();
        }
        else
        {
             if (aeroportoOrigem.wpPronto != null)
             {
                 yield return StartCoroutine(MoverInterpolado(Vector3.zero, velocidadeSolo, true, aeroportoOrigem.wpPronto));
             }
             ProcessarServicoDeBaseAposPouso();
             if (aeroportoOrigem != null) aeroportoOrigem.GuardarNoHangarAutomatico(this);
        }
    }

    private void ProcessarServicoDeBaseAposPouso()
    {
        if (aeroportoOrigem is GerenciadorPortaAvioes)
        {
            GerenciadorPortaAvioes.ReabastecerAeronaveCarrier(this, false);
            return;
        }

        ReabastecerSeAbaixoDe(0.50f);
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

    private IEnumerator RecolherRodas(float delay)
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

    private void AbaixarRodas()
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
}
