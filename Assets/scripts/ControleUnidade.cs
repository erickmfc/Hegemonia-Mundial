using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum DominioControleUnidade
{
    Terrestre,
    NavalSuperficie,
    NavalSubmerso,
    Aereo
}

public enum OrdemControleUnidade
{
    Ociosa,
    Movendo,
    Parada,
    Recuando,
    Patrulhando,
    Seguindo
}

[System.Serializable]
public struct EstadoControleUnidadeSnapshot
{
    public DominioControleUnidade dominio;
    public OrdemControleUnidade ordemAtual;
    public bool modoCombateAtivo;
    public string executorAtivo;
    public bool bloqueada;
    public string motivoBloqueio;
    public bool possuiDestinoOrdenado;
    public Vector3 ultimoDestino;
}

public class ControleUnidade : MonoBehaviour
{
    private static readonly int VelocidadeAnimatorHash = Animator.StringToHash("Velocidade");

    private NavMeshAgent agente;
    private Animator animator; // Referência para as animações
    private bool animatorPossuiVelocidade;
    
    public GameObject anelSelecao; 
    public bool selecionado = false;

    [Header("Imigração / Fronteira")]
    public bool aguardandoVisto = false;
    public bool vistoAprovado = false;
    private int ultimoDonoChao = -1;

    // --- CONTROLE AÉREO ---
    private VooHelicoptero scriptVoo;
    private bool ehAereo = false;
    private Vector3 destinoAereo;
    private bool voando = false;
    public float velocidadeVoo = 8.0f; // Velocidade base para helicópteros

    // --- DETECÇÃO DE CONFLITO ---
    private Helicoptero helicopteroExterno;
    private ControleAviao controleAviao;
    private ControleAviaoCaca controleAviaoCaca;
    private LancadorMisselCaca lancadorMisselCaca;
    private C700TransporteAereo c700TransporteAereo;
    private HovercraftTransporte hovercraftTransporte;
    private ControleNavioRealista controleNavioRealista;
    private NavegacaoInteligenteNaval navegacaoInteligenteNaval;
    private ControleSubmarino controleSubmarino;

    // --- SISTEMA DE VELOCIDADE DINÂMICA (Para Seguir) ---
    private float velocidadeOriginalSalva = -1f;
    private bool limiteVelocidadeAtivo = false;
    private Vector3 ultimoDestinoOrdenado = Vector3.zero;
    private bool possuiDestinoOrdenado = false;
    private readonly EstadoOtimizacaoTatica estadoOtimizacao = new EstadoOtimizacaoTatica();
    private Vector3 posicaoWatchdogAnterior;
    private float tempoSemProgressoOrdem;
    private float proximoWatchdogOrdem;
    private float proximoRelatorioWatchdogBloqueado;
    private int reemissoesWatchdogOrdem;
    private float proximoRefreshVisualCaminho;
    private float proximoReplanNavMesh;
    private Vector3 ultimoDestinoReplanNavMesh;
    private const float IntervaloWatchdogOrdem = 1.25f;
    private const float TempoMaximoSemProgresso = 7.5f;
    private const float IntervaloRelatorioWatchdogBloqueado = 8f;
    private const int MaxReemissoesWatchdogOrdem = 2;
    private bool cacheCombateSujo = true;
    private ControleTorreta[] cacheTorretas = System.Array.Empty<ControleTorreta>();
    private ControleTorretaModular[] cacheTorretasModulares = System.Array.Empty<ControleTorretaModular>();
    private SistemaDeTiro[] cacheSistemasDeTiro = System.Array.Empty<SistemaDeTiro>();
    private ControleNavioRealista[] cacheNaviosRealistas = System.Array.Empty<ControleNavioRealista>();

    [Header("Trilha Oficial")]
    [SerializeField] private DominioControleUnidade dominioControleAtual = DominioControleUnidade.Terrestre;
    [SerializeField] private OrdemControleUnidade ordemControleAtual = OrdemControleUnidade.Ociosa;
    [SerializeField] private bool modoCombateOficialAtivo = true;
    [SerializeField] private string executorControleAtual = "NavMeshAgent";
    [SerializeField] private bool bloqueioControleAtivo = false;
    [SerializeField] private string motivoBloqueioControle = string.Empty;

    protected virtual void Awake()
    {
        SanearBoxCollidersComEscalaNegativa();
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        animatorPossuiVelocidade = PossuiParametroFloat(animator, VelocidadeAnimatorHash);
        AudioRuntime.ConfigurarHierarquia(gameObject);
        
        // Verifica controladores externos
        helicopteroExterno = GetComponent<Helicoptero>();
        controleAviao = GetComponent<ControleAviao>();
        controleAviaoCaca = GetComponent<ControleAviaoCaca>();
        lancadorMisselCaca = GetComponent<LancadorMisselCaca>();
        if (lancadorMisselCaca == null)
        {
            lancadorMisselCaca = GetComponentInChildren<LancadorMisselCaca>(true);
        }
        c700TransporteAereo = GetComponent<C700TransporteAereo>();
        hovercraftTransporte = GetComponent<HovercraftTransporte>();
        controleNavioRealista = GetComponent<ControleNavioRealista>();
        navegacaoInteligenteNaval = GetComponent<NavegacaoInteligenteNaval>();
        controleSubmarino = GetComponent<ControleSubmarino>();

        if (navegacaoInteligenteNaval != null)
        {
            navegacaoInteligenteNaval.enabled = false;
        }
        
        // Verifica se é uma unidade aérea (GENÉRICA)
        scriptVoo = GetComponent<VooHelicoptero>();
        bool temScriptAviao = c700TransporteAereo == null && (controleAviao != null || controleAviaoCaca != null);

        if (scriptVoo != null || helicopteroExterno != null || temScriptAviao)
        {
            ehAereo = true;
            if(agente != null) 
            {
                agente.enabled = false;
                agente = null; 
            }
        }
        
        // CORREÇÃO: Impede que animações do modelo (Root Motion) decolem sozinhas e deixem a seleção p/ trás
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        AtualizarTrilhaOficial();
        ValidarConflitosDeControle();
        SincronizarModoCombateOficial();
        CombustivelUnidade.Garantir(gameObject);
    }

    void SanearBoxCollidersComEscalaNegativa()
    {
        BoxCollider[] boxes = GetComponentsInChildren<BoxCollider>(true);
        for (int i = 0; i < boxes.Length; i++)
        {
            BoxCollider box = boxes[i];
            if (box == null)
            {
                continue;
            }

            Vector3 scale = box.transform.lossyScale;
            if (scale.x >= 0f && scale.y >= 0f && scale.z >= 0f)
            {
                continue;
            }

            box.enabled = false;
            GameObject target = box.gameObject;
            Destroy(box);

            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider mesh = target.GetComponent<MeshCollider>();
            if (mesh == null)
            {
                mesh = target.AddComponent<MeshCollider>();
            }
            mesh.convex = true;
        }
    }

    protected virtual void Start()
    {
        CriarSelecaoVisual();
        if(anelSelecao != null) anelSelecao.SetActive(selecionado);
        posicaoWatchdogAnterior = transform.position;

        if (GetComponent<RenderizadorOrdensUnidade>() == null)
        {
            gameObject.AddComponent<RenderizadorOrdensUnidade>();
        }

        // --- CORREÇÃO DE RESPONSIVIDADE (SOLDADOS) ---
        // Se for uma unidade simples (sem scripts complexos de movimento), aplica configurações ágeis
        if (agente != null && !ehAereo &&
            !TryGetComponent<MovimentoRealTerrestre>(out _) && 
            !TryGetComponent<ControleNavioRealista>(out _) &&
            !TryGetComponent<ControleSubmarino>(out _) &&
            !TryGetComponent<NavioPetroleiro>(out _))
        {
            // É um soldado ou unidade terrestre com NavMesh simples
            agente.acceleration = 60.0f; // Aceleração instantânea
            agente.angularSpeed = 720.0f; // Giro instantâneo
            agente.autoBraking = true;
        }
    }

    protected virtual void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
        cacheCombateSujo = true;

        if (navegacaoInteligenteNaval != null)
        {
            navegacaoInteligenteNaval.enabled = false;
        }

        AtualizarTrilhaOficial();
        SincronizarModoCombateOficial();
    }

    protected virtual void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    protected virtual void OnDestroy()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    protected virtual void OnTransformChildrenChanged()
    {
        cacheCombateSujo = true;
    }

    public bool TemControleAviao => controleAviao != null && c700TransporteAereo == null;
    public bool TemControleAviaoCaca => controleAviaoCaca != null;
    public bool TemHelicopteroExterno => helicopteroExterno != null;
    public bool TemHovercraftTransporte => hovercraftTransporte != null;
    public bool TemC700TransporteAereo => c700TransporteAereo != null;
    public DominioControleUnidade DominioAtual => dominioControleAtual;
    public OrdemControleUnidade OrdemAtual => ordemControleAtual;
    public string ExecutorAtual => executorControleAtual;
    public bool ModoCombateAtivo => modoCombateOficialAtivo;

    [Header("Visual")]
    public float tamanhoSelecao = 0f; // 0 = Automatico
    public Color corSelecao = new Color(1f, 1f, 1f, 0.4f); // Branco semi-transparente (estilo padrão)

    void CriarSelecaoVisual()
    {
        if (anelSelecao != null) return;

        // Cria o anel
        anelSelecao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(anelSelecao.GetComponent<Collider>());
        anelSelecao.transform.SetParent(this.transform);

        // Posição: levemente acima do chão para evitar Z-Fighting
        anelSelecao.transform.localPosition = new Vector3(0, 0.05f, 0);

        // ─── CÁLCULO DE TAMANHO ───────────────────────────────────
        // IMPORTANTE: como o anel é filho do navio, o localScale é
        // multiplicado pela escala do pai. Precisamos compensar isso
        // dividindo pelo scale real do objeto pai para manter o disco
        // no tamanho correto na cena, independentemente de scale.
        float escalaParent = Mathf.Max(transform.lossyScale.x, 0.01f);

        float diametroMundo = 1.5f; // Tamanho padrão (soldado) em unidades de mundo

        if (tamanhoSelecao > 0)
        {
            diametroMundo = tamanhoSelecao;
        }
        else
        {
            // Usa raio do NavMeshAgent → já está em espaço de mundo
            if (agente != null)
            {
                diametroMundo = agente.radius * 2.5f;
            }
            // Ou bounding box do Collider (também em espaço de mundo)
            else
            {
                Collider col = GetComponent<Collider>();
                if (col != null) diametroMundo = col.bounds.size.x * 1.2f;
            }
        }

        // Converte tamanho de mundo → local (divide pela escala do pai)
        float diametroLocal = diametroMundo / escalaParent;

        // Aplica escala (Y achatado para parecer disco)
        anelSelecao.transform.localScale = new Vector3(diametroLocal, 0.02f / escalaParent, diametroLocal);

        // Material e Cor
        Renderer rend = anelSelecao.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = corSelecao;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void Update()
    {
        long inicioUpdate = InfraPerformanceGameplay.MarcarInicioMedicao();
        AtualizarEstadoOtimizacao();

        // 0. Desenha a linha de caminho (Rota) se estiver selecionado, para todos os tipos (Terra/Ar/Mar)
        if (selecionado || InfraPerformanceGameplay.DeveExecutar(this, ref proximoRefreshVisualCaminho, InfraPerformanceGameplay.ResolverIntervalo(0.10f, estadoOtimizacao, false, true)))
        {
            AtualizarVisualCaminho();
        }

        if (GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>() != null) return;

        // SE TIVER HELICOPTER CONTROLLER: NÃO FAZ NADA DE MOVIMENTO AQUI
        // Deixa o outro script cuidar de tudo, este fica só para Seleção/Identidade
        if (helicopteroExterno != null) return;

        float velocidadeAtual = 0f;

        // 1. Lógica Aérea (Movimento Reto)
        if (ehAereo && voando)
        {
            // Move em linha reta X/Z
            Vector3 direcao = destinoAereo - transform.position;
            direcao.y = 0; // Ignora altura no cálculo de direção (mantém altura atual ou ajusta depois)

            if (direcao.magnitude > 0.5f)
            {
                // Rotação
                Quaternion rotacaoAlvo = Quaternion.LookRotation(direcao);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, Time.deltaTime * 5f);

                // Movimento
                transform.position += transform.forward * velocidadeVoo * Time.deltaTime;
                velocidadeAtual = velocidadeVoo;
            }
            else
            {
                // Chegou
                voando = false;
                velocidadeAtual = 0f;
            }
            
            // Passa a velocidade para o script de voo (para inclinar)
            if(scriptVoo != null) scriptVoo.SetVelocidadeAtual(velocidadeAtual);
        }
        // 2. Lógica Terrestre (NavMesh)
        else if (agente != null && agente.enabled)
        {
            velocidadeAtual = agente.velocity.magnitude;

            // --- CORREDOR NULO (Alfândega e Imigração) ---
            if (GerenteDeTerritorio.Instancia != null)
            {
                // Busca a identidade, se não achar retorna 0.
                int teamMeu = GetComponent<IdentidadeIA>()?.teamID ?? GetComponent<IdentidadeUnidade>()?.teamID ?? 0;
                
                // Só processa checagem para times diferentes do Player (1)
                if (teamMeu != 0 && teamMeu != 1)
                {
                    int donoAtual = GerenteDeTerritorio.Instancia.ObterDonoDoPonto(transform.position);

                    // Acabou de entrar na terra do jogador
                    if (donoAtual == 1 && ultimoDonoChao != 1)
                    {
                        if (!vistoAprovado && !aguardandoVisto)
                        {
                            aguardandoVisto = true;
                            if (SistemaConsulado.Instancia != null)
                            {
                                SistemaConsulado.Instancia.SolicitarEntrada(this);
                            }
                        }
                    }
                    // Se saiu do país (ex. voltou pro próprio país ou área neutra)
                    else if (donoAtual != 1 && ultimoDonoChao == 1)
                    {
                        vistoAprovado = false; 
                        aguardandoVisto = false;
                    }

                    ultimoDonoChao = donoAtual;

                    // Bloqueia movimento se estiver na terra do player sem visto (e aguardando)
                    if (donoAtual == 1 && !vistoAprovado && aguardandoVisto)
                    {
                        if (agente.isOnNavMesh) agente.isStopped = true;
                    }
                    else
                    {
                        if (agente.isOnNavMesh) agente.isStopped = false;
                    }
                }
            }
        }

        if (possuiDestinoOrdenado)
        {
            float distanciaAoDestino = (transform.position - ultimoDestinoOrdenado).magnitude;
            float toleranciaChegada = 3f;

            if (agente != null && agente.enabled)
            {
                toleranciaChegada = Mathf.Max(toleranciaChegada, agente.stoppingDistance + 1.5f);
            }

            if (distanciaAoDestino <= toleranciaChegada)
            {
                LimparDestinoOrdenado();
            }
        }

        float intervaloWatchdog = InfraPerformanceGameplay.ResolverIntervalo(IntervaloWatchdogOrdem, estadoOtimizacao, false, true);
        if (InfraPerformanceGameplay.DeveExecutar(this, ref estadoOtimizacao.proximoTickWatchdog, intervaloWatchdog))
        {
            long inicioWatchdog = InfraPerformanceGameplay.MarcarInicioMedicao();
            AtualizarWatchdogOrdem();
            InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Formacao, inicioWatchdog);
        }

        // 3. Controle de Animação (Genérico)
        if (animatorPossuiVelocidade && animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat(VelocidadeAnimatorHash, velocidadeAtual);
        }

        // 4. Controle de Movimento pelo Mouse (se selecionado)
        // DESLIGADO AGORA: O GerenteSelecao controla o movimento em grupo!
        /*
        if (Input.GetMouseButtonDown(1) && selecionado)
        {
            MoverParaMouse();
        }
        */

        InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Terra, inicioUpdate);
    }

    private static bool PossuiParametroFloat(Animator alvo, int parametroHash)
    {
        if (alvo == null || alvo.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parametros = alvo.parameters;
        for (int i = 0; i < parametros.Length; i++)
        {
            AnimatorControllerParameter parametro = parametros[i];
            if (parametro.nameHash == parametroHash && parametro.type == AnimatorControllerParameterType.Float)
            {
                return true;
            }
        }

        return false;
    }

    void MoverParaMouse()
    {
        Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit pontoDeColisao;

        if (Physics.Raycast(raio, out pontoDeColisao))
        {
            MoverParaPonto(pontoDeColisao.point);
        }
    }

    // Sobrecarga para SendMessage da IA (que não suporta parâmetros opcionais)
    public void MoverParaPonto(Vector3 destino)
    {
        EmitirOrdemMover(destino, true);
    }

    // COMANDO AUTOMÁTICO (Usado pela fábrica e IA)
    public void MoverParaPonto(Vector3 destino, bool cancelarComportamentos = true)
    {
        EmitirOrdemMover(destino, cancelarComportamentos);
    }

    public bool EmitirOrdemMover(Vector3 destino, bool cancelarComportamentos = true)
    {
        if (GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>() != null)
        {
            Debug.LogWarning($"[C17] Movimento generico recusado para {name}; ordem deve vir do aeroporto.");
            return false;
        }
        AtualizarTrilhaOficial();
        AtualizarEstadoDeBloqueio();
        if (bloqueioControleAtivo)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("OrdemRecusada", $"{name}: mover bloqueado ({motivoBloqueioControle})");
            return false;
        }

        if (cancelarComportamentos || (ordemControleAtual != OrdemControleUnidade.Patrulhando && ordemControleAtual != OrdemControleUnidade.Seguindo))
        {
            ordemControleAtual = OrdemControleUnidade.Movendo;
            DefinirAlvoPrioritario(null);
        }

        ExecutarMoverParaPonto(destino, cancelarComportamentos);
        DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("orders_emitted");
        return true;
    }

    public bool EmitirOrdemParar()
    {
        AtualizarTrilhaOficial();
        AtualizarEstadoDeBloqueio();
        CancelarOrdemEspecial(false);
        DefinirAlvoPrioritario(null);

        bool alterouAlgo = false;

        if (ehAereo)
        {
            voando = false;
            alterouAlgo = true;
        }

        if (c700TransporteAereo != null)
        {
            c700TransporteAereo.CancelarModoAereo();
            alterouAlgo = true;
        }

        var c17Script = GetComponent<Hegemonia.Aeronaves.C17.C17TransporteController>();
        if (c17Script != null)
        {
            c17Script.CancelarOrdemExterna();
            alterouAlgo = true;
        }
        else if (controleAviao != null)
        {
            controleAviao.ordemParaRetorno = true;
            alterouAlgo = true;
        }

        if (helicopteroExterno != null)
        {
            helicopteroExterno.destino = transform.position;
            alterouAlgo = true;
        }

        if (controleAviaoCaca != null)
        {
            controleAviaoCaca.DefinirDestino(transform.position + transform.forward * 250f);
            alterouAlgo = true;
        }

        if (agente == null)
        {
            agente = GetComponent<NavMeshAgent>();
        }

        if (agente != null && agente.enabled)
        {
            if (agente.isOnNavMesh)
            {
                agente.ResetPath();
                agente.isStopped = true;
            }

            alterouAlgo = true;
        }

        LimparDestinoOrdenado();
        ordemControleAtual = OrdemControleUnidade.Parada;
        return alterouAlgo;
    }

    public bool EmitirOrdemRecuar(Vector3 origemAmeaca, float distancia = 40f, bool cancelarComportamentos = true)
    {
        Vector3 direcaoRecuo = transform.position - origemAmeaca;
        direcaoRecuo.y = 0f;

        if (direcaoRecuo.sqrMagnitude < 0.01f)
        {
            direcaoRecuo = -transform.forward;
            direcaoRecuo.y = 0f;
        }

        Vector3 destinoRecuo = transform.position + direcaoRecuo.normalized * Mathf.Max(5f, distancia);
        bool emitiu = EmitirOrdemMover(destinoRecuo, cancelarComportamentos);
        if (emitiu)
        {
            ordemControleAtual = OrdemControleUnidade.Recuando;
        }

        return emitiu;
    }

    public bool EmitirOrdemPatrulha(IList<Vector3> pontosPatrulha)
    {
        if (pontosPatrulha == null || pontosPatrulha.Count == 0)
        {
            return false;
        }

        AtualizarTrilhaOficial();

        List<Vector3> rotaFinal = new List<Vector3>(pontosPatrulha);
        if (rotaFinal.Count == 1)
        {
            rotaFinal.Insert(0, transform.position);
        }

        // O vinculo com aeroporto controla pouso e abastecimento, mas nao pode
        // impedir uma ordem manual enviada pelo menu satelite.
        if (helicopteroExterno != null)
        {
            CancelarOrdemEspecial(false);
            helicopteroExterno.IniciarPatrulhaAeroporto(rotaFinal);
            ordemControleAtual = OrdemControleUnidade.Patrulhando;
            DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("orders_emitted");
            return true;
        }

        AtualizarEstadoDeBloqueio();
        if (bloqueioControleAtivo)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("OrdemRecusada", $"{name}: patrulha bloqueada ({motivoBloqueioControle})");
            return false;
        }

        CancelarOrdemEspecial(false);

        ComportamentoPatrulhaUniversal patrulha = GetComponent<ComportamentoPatrulhaUniversal>();
        if (patrulha == null)
        {
            patrulha = gameObject.AddComponent<ComportamentoPatrulhaUniversal>();
        }

        patrulha.Configurar(rotaFinal);
        if (controleAviao != null)
        {
            controleAviao.RegistrarPatrulha(rotaFinal);
            if (controleAviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                controleAviao.IniciarMissaoCompleta(rotaFinal[0]);
            }
        }
        ordemControleAtual = OrdemControleUnidade.Patrulhando;
        DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("orders_emitted");
        return true;
    }

    public bool EmitirOrdemSeguir(Transform alvo)
    {
        return EmitirOrdemSeguir(alvo, -1f);
    }

    public bool EmitirOrdemSeguir(Transform alvo, float distanciaSeguimento)
    {
        if (alvo == null)
        {
            return false;
        }

        AtualizarTrilhaOficial();
        AtualizarEstadoDeBloqueio();
        if (bloqueioControleAtivo)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("OrdemRecusada", $"{name}: seguir bloqueado ({motivoBloqueioControle})");
            return false;
        }

        CancelarOrdemEspecial(false);

        ComportamentoSeguirUniversal seguir = GetComponent<ComportamentoSeguirUniversal>();
        if (seguir == null)
        {
            seguir = gameObject.AddComponent<ComportamentoSeguirUniversal>();
        }

        seguir.Configurar(alvo, distanciaSeguimento);
        ordemControleAtual = OrdemControleUnidade.Seguindo;
        DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("orders_emitted");
        return true;
    }

    public void DefinirAlvoPrioritario(Transform alvo)
    {
        // Limpa o cache se for a primeira vez
        if (cacheCombateSujo) GarantirCacheCombate();

        foreach (var tiro in cacheSistemasDeTiro)
        {
            if (tiro != null) tiro.alvoPrioritario = alvo;
        }
        foreach (var torreta in cacheTorretas)
        {
            if (torreta != null) torreta.alvoPrioritario = alvo;
        }
        foreach (var modular in cacheTorretasModulares)
        {
            if (modular != null) modular.alvoPrioritario = alvo;
        }
        
        // Também avisa ao avião/heli, se houver
        if (controleAviao != null && alvo != null) controleAviao.alvoPrioritarioIA = true;
    }

    public bool EmitirMissaoAereaOfensiva(Vector3 pontoAlvo, Transform alvoTransform)
    {
        AtualizarTrilhaOficial();
        AtualizarEstadoDeBloqueio();
        if (bloqueioControleAtivo)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("OrdemRecusada", $"{name}: missao aerea ofensiva bloqueada ({motivoBloqueioControle})");
            return false;
        }

        DefinirModoCombate(true);

        if (lancadorMisselCaca != null && alvoTransform != null)
        {
            lancadorMisselCaca.DefinirAlvoIA(alvoTransform, pontoAlvo, 6f);
        }

        if (controleAviao != null)
        {
            controleAviao.alvoEstrategico = pontoAlvo;
            controleAviao.alvoGPSVoo = pontoAlvo;
        }

        bool ordemEmitida = EmitirOrdemMover(pontoAlvo, true);
        if (ordemEmitida)
        {
            DefinirAlvoPrioritario(alvoTransform);
        }
        return ordemEmitida;
    }

    public bool EmitirMissaoNavalOfensiva(Vector3 pontoAlvo, Transform alvoTransform, bool manterFormacao, bool cancelarComportamentos)
    {
        AtualizarTrilhaOficial();
        AtualizarEstadoDeBloqueio();
        if (bloqueioControleAtivo)
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("OrdemRecusada", $"{name}: missao naval ofensiva bloqueada ({motivoBloqueioControle})");
            return false;
        }

        DefinirModoCombate(true);

        if (lancadorMisselCaca != null && alvoTransform != null)
        {
            lancadorMisselCaca.DefinirAlvoIA(alvoTransform, pontoAlvo, 6f);
        }

        if (controleNavioRealista != null && alvoTransform != null)
        {
            controleNavioRealista.DefinirDestinoAtaqueLateral(alvoTransform.position);
            DefinirAlvoPrioritario(alvoTransform);
            return true;
        }

        bool ordemEmitida = EmitirOrdemMover(pontoAlvo, cancelarComportamentos);
        if (ordemEmitida)
        {
            DefinirAlvoPrioritario(alvoTransform);
        }
        return ordemEmitida;
    }

    public void CancelarOrdemEspecial()
    {
        CancelarOrdemEspecial(true);
    }

    public EstadoControleUnidadeSnapshot ObterEstadoControle()
    {
        AtualizarTrilhaOficial();
        AtualizarEstadoDeBloqueio();
        return new EstadoControleUnidadeSnapshot
        {
            dominio = dominioControleAtual,
            ordemAtual = ordemControleAtual,
            modoCombateAtivo = modoCombateOficialAtivo,
            executorAtivo = executorControleAtual,
            bloqueada = bloqueioControleAtivo,
            motivoBloqueio = motivoBloqueioControle,
            possuiDestinoOrdenado = possuiDestinoOrdenado,
            ultimoDestino = ultimoDestinoOrdenado
        };
    }

    private void ExecutarMoverParaPonto(Vector3 destino, bool cancelarComportamentos = true)
    {
        if (helicopteroExterno != null && helicopteroExterno.EstaSobControleDoAeroporto())
        {
            return;
        }

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            CombustivelUnidade combustivel = GetComponent<CombustivelUnidade>();
            if (combustivel != null)
            {
                combustivel.PararPorFaltaDeCombustivel();
            }
            return;
        }

        // --- BLOQUEIO DE ÁGUA PARA UNIDADES TERRESTRES ---
        // Verifica se o destino é água e se a unidade tem permissão para entrar
        if (RegistroSuperficieMapa.TryClassify(destino, out ClassificacaoSuperficieMapa classe, out _))
        {
            bool ehTerrestre = !EhUnidadeNaval() && !ehAereo && hovercraftTransporte == null && c700TransporteAereo == null;
            if (ehTerrestre && classe == ClassificacaoSuperficieMapa.Agua)
            {
                // Debug.Log($"[Bloqueio] {name} recusou mover para Água profunda.");
                return; 
            }
        }

        if (c700TransporteAereo != null && c700TransporteAereo.EstaNoSolo && !c700TransporteAereo.AguardandoDestinoAereo)
        {
            c700TransporteAereo.ReceberOrdemMover(destino);
            return;
        }

        RegistrarDestinoOrdenado(destino);

        if (cancelarComportamentos)
        {
            CancelarOrdemEspecial(false);
        }

        // Debug.Log($"[ControleUnidade] {name} recebeu MoverParaPonto({destino})...");

        // Caça Militar Aéreo
        if (controleAviaoCaca != null)
        {
            controleAviaoCaca.DefinirDestino(destino);
            return;
        }

        // Avião de Passageiros / Cargueiro (Sistema de Aeroporto)
        if (c700TransporteAereo != null)
        {
            c700TransporteAereo.ReceberOrdemMover(destino);
            return;
        }

        // Avião de Passageiros / Cargueiro (Sistema de Aeroporto)
        if (controleAviao != null)
        {
            bool atualizacaoDePatrulhaAerea = ordemControleAtual == OrdemControleUnidade.Patrulhando && !cancelarComportamentos;
            if (atualizacaoDePatrulhaAerea)
            {
                controleAviao.AtualizarDestinoPatrulha(destino);
            }
            else
            {
                controleAviao.RegistrarMissaoManual(destino);
            }

            if (controleAviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                controleAviao.IniciarMissaoCompleta(destino);
            }
            else if (controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao || controleAviao.estadoAtual == ControleAviao.EstadoAviao.Decolando)
            {
                // Se já estiver voando, apenas muda a coordenada do GPS
                controleAviao.alvoGPSVoo = destino;
            }
            return;
        }

        if (helicopteroExterno != null)
        {
            helicopteroExterno.CancelarMissaoAeroporto();
            helicopteroExterno.Decolar(destino);
            return;
        }

        if (ehAereo)
        {
            destinoAereo = destino;
            voando = true;
            return;
        }

        if (hovercraftTransporte != null)
        {
            hovercraftTransporte.DefinirDestino(destino);

            if (agente != null && agente.enabled)
            {
                if (agente.isOnNavMesh) agente.ResetPath();
                agente.isStopped = true;
            }

            return;
        }

        if (controleNavioRealista != null)
        {
            controleNavioRealista.DefinirDestino(destino);
            return;
        }

        if (controleSubmarino != null)
        {
            controleSubmarino.DefinirDestino(destino);
            return;
        }

        // Segurança: Se o agente não foi pego no Awake (ex: adicionado depois), pega agora.
        if (agente == null) agente = GetComponent<NavMeshAgent>();

        if (agente != null)
        {
            if (agente.isOnNavMesh && agente.isActiveAndEnabled)
            {
                // ✨ SISTEMA DE NAVEGAÇÃO NAVAL REALISTA OU INTELIGENTE ✨
                if (controleNavioRealista != null)
                {
                    controleNavioRealista.DefinirDestino(destino);
                    // Debug.Log($"[Navegação] {name} usando Física Realista.");
                    return;
                }

                // Verifica se é Submarino
                if (controleSubmarino != null)
                {
                    controleSubmarino.DefinirDestino(destino);
                    return;
                }

                // Navegação normal (terrestre ou navio sem o sistema inteligente)
                AplicarDestinoNavMeshComCooldown(destino, false);
            }
            else
            {
                 // Agente fora do navmesh ou desativado - TENTA RECUPERAR!
                 if (!gameObject.activeInHierarchy) return; // Impede erros se o objeto estiver desligado (ex: em construção)
                 
                 try 
                 {
                     if (!agente.enabled) agente.enabled = true; // Força a ativação do componente

                     if (!agente.isOnNavMesh)
                     {
                         NavMeshHit hit;
                         if (NavMesh.SamplePosition(transform.position, out hit, 100f, NavMesh.AllAreas))
                         {
                             agente.Warp(hit.position);
                         }
                     }

                     // Só dá a ordem se a recuperação funcionou
                     if (agente.isOnNavMesh && agente.isActiveAndEnabled)
                     {
                         if (controleNavioRealista != null)
                         {
                             controleNavioRealista.DefinirDestino(destino);
                             return;
                         }

                        if (controleSubmarino != null)
                        {
                            controleSubmarino.DefinirDestino(destino);
                             return;
                         }

                         AplicarDestinoNavMeshComCooldown(destino, true);
                     }
                 }
                 catch (System.Exception ex)
                 {
                     Debug.LogWarning($"[ControleUnidade] Falha ao recuperar NavMeshAgent para {name}: {ex.Message}");
                 }
            }
        }
    }

    private void AtualizarEstadoOtimizacao()
    {
        bool engajada = possuiDestinoOrdenado
            || ordemControleAtual == OrdemControleUnidade.Movendo
            || ordemControleAtual == OrdemControleUnidade.Patrulhando
            || ordemControleAtual == OrdemControleUnidade.Seguindo
            || ordemControleAtual == OrdemControleUnidade.Recuando;

        bool heroica = ehAereo || EhUnidadeNaval();
        InfraPerformanceGameplay.AtualizarEstadoBase(estadoOtimizacao, transform, selecionado, engajada, heroica);
    }

    private void AplicarDestinoNavMeshComCooldown(Vector3 destino, bool forcar)
    {
        if (agente == null || !agente.enabled || !agente.isActiveAndEnabled || !agente.isOnNavMesh)
        {
            return;
        }

        float cooldownBase = EhUnidadeNaval() ? 1.10f : 0.80f;
        float cooldown = InfraPerformanceGameplay.ResolverIntervalo(cooldownBase, estadoOtimizacao, true, true);
        float tolerancia = EhUnidadeNaval() ? 8f : 4.5f;
        if (!InfraPerformanceGameplay.DeveAplicarReplan(destino, ref ultimoDestinoReplanNavMesh, ref proximoReplanNavMesh, cooldown, tolerancia, forcar))
        {
            if (agente.isOnNavMesh)
            {
                agente.isStopped = false;
            }
            return;
        }

        long inicioPath = InfraPerformanceGameplay.MarcarInicioMedicao();
        agente.SetDestination(destino);
        if (agente.isOnNavMesh)
        {
            agente.isStopped = false;
        }
        InfraPerformanceGameplay.RegistrarTempoDecorrido(CategoriaBudgetGameplay.Pathfinding, inicioPath);
    }

    public float ObterVelocidadeAtualReal()
    {
        if (agente != null && agente.enabled) return agente.velocity.magnitude;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) return rb.linearVelocity.magnitude;
        if (ehAereo && voando) return velocidadeVoo;
        return 0f;
    }

    public bool EhUnidadeNaval()
    {
        return GetComponent<IdentidadeNaval>() != null ||
               controleNavioRealista != null ||
               controleSubmarino != null ||
               TryGetComponent<NavioPetroleiro>(out _) ||
               TryGetComponent<NavioTransporteTropas>(out _) ||
               TryGetComponent<GerenciadorPortaAvioes>(out _);
    }

    private bool EhNavioSuperficie()
    {
        return controleSubmarino == null && (
               GetComponent<IdentidadeNaval>() != null ||
               controleNavioRealista != null ||
               TryGetComponent<NavioPetroleiro>(out _) ||
               TryGetComponent<NavioTransporteTropas>(out _) ||
               TryGetComponent<GerenciadorPortaAvioes>(out _));
    }

    public bool DefinirModoCombate(bool ativo)
    {
        GarantirCacheCombate();
        AtualizarTrilhaOficial();

        bool alterouAlgo = false;
        modoCombateOficialAtivo = ativo;

        if (helicopteroExterno != null)
        {
            helicopteroExterno.AplicarModoCombateDoMenu(ativo);
            alterouAlgo = true;
        }

        for (int i = 0; i < cacheTorretas.Length; i++)
        {
            ControleTorreta torreta = cacheTorretas[i];
            if (torreta == null) continue;
            torreta.DefinirModoAtivo(ativo);
            alterouAlgo = true;
        }

        for (int i = 0; i < cacheTorretasModulares.Length; i++)
        {
            ControleTorretaModular torreta = cacheTorretasModulares[i];
            if (torreta == null) continue;
            torreta.DefinirModoAtivo(ativo);
            alterouAlgo = true;
        }

        for (int i = 0; i < cacheSistemasDeTiro.Length; i++)
        {
            SistemaDeTiro sistema = cacheSistemasDeTiro[i];
            if (sistema == null) continue;
            bool usarSomenteRadarMissil = lancadorMisselCaca != null;
            sistema.DefinirModoPassivo(usarSomenteRadarMissil || !ativo);
            alterouAlgo = true;
        }

        for (int i = 0; i < cacheNaviosRealistas.Length; i++)
        {
            ControleNavioRealista navio = cacheNaviosRealistas[i];
            if (navio == null || !navio.TemSistemaTorpedosConfigurado()) continue;
            navio.DefinirModoCombateTorpedos(ativo);
            alterouAlgo = true;
        }

        return alterouAlgo;
    }

    public bool TryObterEstadoCombate(out bool passivo, out string descricao)
    {
        GarantirCacheCombate();
        AtualizarTrilhaOficial();

        bool encontrou = false;
        bool estadoInicial = false;
        bool misto = false;

        RegistrarEstadoCombate(cacheTorretas, ref encontrou, ref estadoInicial, ref misto, delegate(ControleTorreta t) { return t != null && t.modoPassivo; });
        RegistrarEstadoCombate(cacheTorretasModulares, ref encontrou, ref estadoInicial, ref misto, delegate(ControleTorretaModular t) { return t != null && t.modoPassivo; });
        RegistrarEstadoCombate(cacheSistemasDeTiro, ref encontrou, ref estadoInicial, ref misto, delegate(SistemaDeTiro t) { return t != null && t.modoPassivo; });
        RegistrarEstadoCombateNavios(cacheNaviosRealistas, ref encontrou, ref estadoInicial, ref misto);

        if (!encontrou)
        {
            passivo = !modoCombateOficialAtivo;
            descricao = modoCombateOficialAtivo ? "ATIVO" : "PASSIVO";
            return true;
        }

        if (misto)
        {
            passivo = false;
            descricao = "MISTO";
            return true;
        }

        passivo = estadoInicial;
        modoCombateOficialAtivo = !passivo;
        descricao = passivo ? "PASSIVO" : "ATIVO";
        return true;
    }

    public void AplicarLimiteVelocidade(float velocidadeAlvo)
    {
        // Salva a original apenas na primeira vez
        if (velocidadeOriginalSalva < 0f)
        {
            if (TryGetComponent<ControleAviao>(out var aviao)) velocidadeOriginalSalva = aviao.velocidadeMaximaVoo;
            else if (TryGetComponent<ControleAviaoCaca>(out var caca)) velocidadeOriginalSalva = caca.velocidadeCruzeiro;
            else if (TryGetComponent<Helicoptero>(out var helicoptero)) velocidadeOriginalSalva = helicoptero.velocidadeNavegacao;
            else if (TryGetComponent<ControleNavioRealista>(out var nav1)) velocidadeOriginalSalva = nav1.velocidadeMaxima;
            else if (TryGetComponent<NavMeshAgent>(out var nma)) velocidadeOriginalSalva = nma.speed;
            else velocidadeOriginalSalva = 0f;
        }

        limiteVelocidadeAtivo = true;
        ModificarVelocidadeInterna(velocidadeAlvo);
    }

    public void RestaurarVelocidadeOriginal()
    {
        if (limiteVelocidadeAtivo && velocidadeOriginalSalva > 0f)
        {
            ModificarVelocidadeInterna(velocidadeOriginalSalva);
            limiteVelocidadeAtivo = false;
        }
    }

    private void ModificarVelocidadeInterna(float v)
    {
        if (TryGetComponent<ControleAviao>(out var aviao)) aviao.velocidadeMaximaVoo = Mathf.Max(v, aviao.velocidadeSolo * 2.5f); // Avião não pode parar no ar
        else if (TryGetComponent<ControleAviaoCaca>(out var caca))
        {
            caca.velocidadeCruzeiro = Mathf.Max(v, caca.velocidadeTaxi * 2.5f);
            caca.velocidadeAtaque = Mathf.Max(caca.velocidadeCruzeiro, v * 1.4f);
        }
        else if (TryGetComponent<Helicoptero>(out var helicoptero)) helicoptero.velocidadeNavegacao = Mathf.Max(0.1f, v);
        else if (TryGetComponent<ControleNavioRealista>(out var nav1)) nav1.velocidadeMaxima = v;
        else if (TryGetComponent<NavMeshAgent>(out var nma)) { if(nma.enabled) nma.speed = v; }
    }

    private void RegistrarEstadoCombateValor(bool valido, bool passivoAtual, ref bool encontrou, ref bool estadoInicial, ref bool misto)
    {
        if (!valido || misto)
        {
            return;
        }

        if (!encontrou)
        {
            estadoInicial = passivoAtual;
            encontrou = true;
            return;
        }

        if (estadoInicial != passivoAtual)
        {
            misto = true;
        }
    }

    private static void RegistrarEstadoCombate<T>(T[] componentes, ref bool encontrou, ref bool estadoInicial, ref bool misto, System.Func<T, bool> leitor)
    {
        if (componentes == null) return;

        for (int i = 0; i < componentes.Length; i++)
        {
            T componente = componentes[i];
            if (componente == null) continue;

            bool estado = leitor(componente);
            if (!encontrou)
            {
                encontrou = true;
                estadoInicial = estado;
            }
            else if (estado != estadoInicial)
            {
                misto = true;
            }
        }
    }

    private static void RegistrarEstadoCombateNavios(ControleNavioRealista[] navios, ref bool encontrou, ref bool estadoInicial, ref bool misto)
    {
        if (navios == null) return;

        for (int i = 0; i < navios.Length; i++)
        {
            ControleNavioRealista navio = navios[i];
            if (navio == null || !navio.TemSistemaTorpedosConfigurado())
            {
                continue;
            }

            bool passivoAtual = !navio.ModoCombateTorpedosAtivo();
            if (!encontrou)
            {
                encontrou = true;
                estadoInicial = passivoAtual;
            }
            else if (estadoInicial != passivoAtual)
            {
                misto = true;
            }
        }
    }

    private void GarantirCacheCombate()
    {
        if (!cacheCombateSujo)
        {
            return;
        }

        cacheTorretas = GetComponentsInChildren<ControleTorreta>(true);
        cacheTorretasModulares = GetComponentsInChildren<ControleTorretaModular>(true);
        cacheSistemasDeTiro = GetComponentsInChildren<SistemaDeTiro>(true);
        cacheNaviosRealistas = GetComponentsInChildren<ControleNavioRealista>(true);
        cacheCombateSujo = false;
    }

    [Header("Visual de Alcance")]
    public LineRenderer linhaAlcance;
    public Color corAlcance = new Color(1f, 0f, 0f, 0.8f); // Vermelho
    public float larguraLinha = 0.15f;
    public int segmentosCirculo = 50;

    void CriarVisualAlcance()
    {
        if (linhaAlcance != null) return;

        // Tenta pegar o alcance da Torreta ou Sistema de Tiro
        float alcance = 0f;
        var torreta = GetComponent<ControleTorreta>();
        if (torreta != null) alcance = torreta.alcance;
        else 
        {
            var tiro = GetComponent<SistemaDeTiro>();
            if (tiro != null) alcance = tiro.alcanceTiro;
        }

        // Se não tem alcance de tiro, não desenha nada
        if (alcance <= 0) return;

        // Cria o LineRenderer
        GameObject objLinha = new GameObject("LinhaAlcance");
        objLinha.transform.SetParent(this.transform);
        objLinha.transform.localPosition = Vector3.zero;
        
        linhaAlcance = objLinha.AddComponent<LineRenderer>();
        linhaAlcance.useWorldSpace = false; // Relativo ao pai (se mover, move junto)
        linhaAlcance.startWidth = larguraLinha;
        linhaAlcance.endWidth = larguraLinha;
        linhaAlcance.positionCount = segmentosCirculo + 1;
        linhaAlcance.loop = true;
        
        // Material simples (unlit) para brilhar
        linhaAlcance.material = new Material(Shader.Find("Sprites/Default"));
        linhaAlcance.startColor = corAlcance;
        linhaAlcance.endColor = corAlcance;

        // Desenha o círculo
        float angulo = 0f;
        for (int i = 0; i < segmentosCirculo + 1; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angulo) * alcance;
            float z = Mathf.Cos(Mathf.Deg2Rad * angulo) * alcance;
            
            linhaAlcance.SetPosition(i, new Vector3(x, 0.5f, z)); // 0.5f de altura do chão
            
            angulo += (360f / segmentosCirculo);
        }
        
        linhaAlcance.gameObject.SetActive(false); // Começa invisível
    }

    [Header("Visual de Caminho")]
    public LineRenderer linhaCaminho;
    public Color corCaminho = new Color(0f, 1f, 0.5f, 0.4f);

    void CriarVisualCaminho()
    {
        if (linhaCaminho != null) return;
        
        GameObject objLinha = new GameObject("LinhaCaminho");
        objLinha.transform.SetParent(this.transform);
        objLinha.transform.localPosition = Vector3.zero;
        
        linhaCaminho = objLinha.AddComponent<LineRenderer>();
        linhaCaminho.useWorldSpace = true;
        linhaCaminho.startWidth = 0.5f;
        linhaCaminho.endWidth = 0.5f;
        // CORREÇÃO: começa com 0 pontos para não renderizar lixo antes de ser atualizado
        linhaCaminho.positionCount = 0;
        
        linhaCaminho.material = new Material(Shader.Find("Sprites/Default"));
        linhaCaminho.startColor = corCaminho;
        linhaCaminho.endColor = corCaminho;
        linhaCaminho.gameObject.SetActive(false);
        linhaCaminho.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void AtualizarVisualCaminho()
    {
        // Garante que a linha de caminho esteja sempre desativada e sem pontos
        if (linhaCaminho != null)
        {
            if (linhaCaminho.positionCount > 0)
                linhaCaminho.positionCount = 0;
            if (linhaCaminho.gameObject.activeSelf)
                linhaCaminho.gameObject.SetActive(false);
        }
        return;
#if false
        // Desenho da linha verde desativado conforme solicitado
        if (!selecionado || linhaCaminho == null) return;

        Vector3 metaLinha = Vector3.zero;
        bool tentarDesenharReta = false;

        // Tenta Aéreo Genérico
        if (ehAereo && voando)
        {
            metaLinha = destinoAereo;
            tentarDesenharReta = true;
        }
        else if (helicopteroExterno != null)
        {
            metaLinha = helicopteroExterno.destino; 
            if (helicopteroExterno.estaVoando && Vector3.Distance(transform.position, metaLinha) > 2f) 
                tentarDesenharReta = true;
        }

        if (c700TransporteAereo != null && c700TransporteAereo.TemDestinoVisual)
        {
            metaLinha = c700TransporteAereo.DestinoVisualAtual;
            if (Vector3.Distance(transform.position, metaLinha) > 2f)
            {
                tentarDesenharReta = true;
            }
        }

        ControleAviao aviao = GetComponent<ControleAviao>();
        if (aviao != null)
        {
            if (aviao.estaEmModoVooFisico || aviao.estadoAtual == ControleAviao.EstadoAviao.Pousando || aviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
            {
                metaLinha = aviao.alvoGPSVoo;
                if (Vector3.Distance(transform.position, metaLinha) > 2f && metaLinha != Vector3.zero) 
                    tentarDesenharReta = true;
            }
        }

        // Tenta ControleAviaoCaca (Caça Militar)
        ControleAviaoCaca caca = GetComponent<ControleAviaoCaca>();
        if (caca != null)
        {
            metaLinha = caca.DestinoAtual;
            if (Vector3.Distance(transform.position, metaLinha) > 2f)
                tentarDesenharReta = true;
        }

        if (tentarDesenharReta)
        {
            linhaCaminho.positionCount = 2;
            linhaCaminho.SetPosition(0, transform.position + Vector3.up * 1f);
            linhaCaminho.SetPosition(1, metaLinha + Vector3.up * 1f);
            if (!linhaCaminho.gameObject.activeSelf) linhaCaminho.gameObject.SetActive(true);
        }
        else if (agente != null && agente.enabled && agente.hasPath)
        {
            var caminho = agente.path;
            linhaCaminho.positionCount = caminho.corners.Length;
            for(int i = 0; i < caminho.corners.Length; i++)
            {
                linhaCaminho.SetPosition(i, caminho.corners[i] + Vector3.up * 0.5f);
            }
            if (!linhaCaminho.gameObject.activeSelf) linhaCaminho.gameObject.SetActive(true);
        }
        else if (TentarDesenharLinhaFallback())
        {
            if (!linhaCaminho.gameObject.activeSelf) linhaCaminho.gameObject.SetActive(true);
        }
        else
        {
            if (linhaCaminho.gameObject.activeSelf) linhaCaminho.gameObject.SetActive(false);
        }
#endif
    }

    void RegistrarDestinoOrdenado(Vector3 destino)
    {
        ultimoDestinoOrdenado = destino;
        possuiDestinoOrdenado = true;
        tempoSemProgressoOrdem = 0f;
        reemissoesWatchdogOrdem = 0;
        posicaoWatchdogAnterior = transform.position;
    }

    private void AtualizarWatchdogOrdem()
    {
        if (!Application.isPlaying || !possuiDestinoOrdenado)
        {
            posicaoWatchdogAnterior = transform.position;
            tempoSemProgressoOrdem = 0f;
            reemissoesWatchdogOrdem = 0;
            return;
        }

        if (Time.unscaledTime < proximoWatchdogOrdem)
        {
            return;
        }

        proximoWatchdogOrdem = Time.unscaledTime + IntervaloWatchdogOrdem;
        float distanciaMovida = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(posicaoWatchdogAnterior.x, 0f, posicaoWatchdogAnterior.z));
        posicaoWatchdogAnterior = transform.position;

        AtualizarEstadoDeBloqueio();
        if (distanciaMovida > 0.45f || ordemControleAtual == OrdemControleUnidade.Parada)
        {
            tempoSemProgressoOrdem = 0f;
            reemissoesWatchdogOrdem = 0;
            return;
        }

        if (bloqueioControleAtivo)
        {
            RegistrarWatchdogBloqueado(string.IsNullOrEmpty(motivoBloqueioControle) ? "Bloqueio de controle ativo" : motivoBloqueioControle);
            tempoSemProgressoOrdem = 0f;
            return;
        }

        tempoSemProgressoOrdem += IntervaloWatchdogOrdem;
        if (tempoSemProgressoOrdem < TempoMaximoSemProgresso)
        {
            return;
        }

        string causa = DiagnosticarCausaOrdemTravada();
        bool podeReemitir = PodeReemitirOrdemTravada(causa);
        if (!podeReemitir || reemissoesWatchdogOrdem >= MaxReemissoesWatchdogOrdem)
        {
            RegistrarWatchdogBloqueado(causa);
            tempoSemProgressoOrdem = Mathf.Max(0f, TempoMaximoSemProgresso - (IntervaloWatchdogOrdem * 2f));
            return;
        }

        tempoSemProgressoOrdem = 0f;
        reemissoesWatchdogOrdem++;
        DiagnosticoDesempenhoJogo.RegistrarEvento("OrdemTravada", $"{name}: reemitindo destino ativo ({ordemControleAtual}) tentativa {reemissoesWatchdogOrdem}/{MaxReemissoesWatchdogOrdem} causa={causa}");
        if (ordemControleAtual == OrdemControleUnidade.Movendo
            || ordemControleAtual == OrdemControleUnidade.Recuando
            || ordemControleAtual == OrdemControleUnidade.Patrulhando
            || ordemControleAtual == OrdemControleUnidade.Seguindo)
        {
            ExecutarMoverParaPonto(ultimoDestinoOrdenado, false);
        }
    }

    private string DiagnosticarCausaOrdemTravada()
    {
        if (bloqueioControleAtivo && !string.IsNullOrWhiteSpace(motivoBloqueioControle))
        {
            return motivoBloqueioControle;
        }

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            return "Sem combustivel";
        }

        if (helicopteroExterno != null && helicopteroExterno.EstaSobControleDoAeroporto())
        {
            return "Helicoptero sob controle do aeroporto";
        }

        if (agente != null)
        {
            if (!agente.enabled)
            {
                return "NavMeshAgent desativado";
            }

            if (!agente.isOnNavMesh)
            {
                return "NavMeshAgent fora do NavMesh";
            }

            if (agente.pathPending)
            {
                return "NavMeshAgent calculando caminho";
            }

            if (agente.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                return "NavMeshAgent com caminho invalido";
            }

            if (agente.pathStatus == NavMeshPathStatus.PathPartial)
            {
                return "NavMeshAgent com caminho parcial";
            }

            if (agente.isStopped)
            {
                return "NavMeshAgent parado";
            }

            if (!agente.hasPath && Vector3.Distance(transform.position, ultimoDestinoOrdenado) > 2.5f)
            {
                return "NavMeshAgent sem caminho";
            }
        }

        if (controleNavioRealista != null)
        {
            return "Executor naval sem progresso";
        }

        if (controleSubmarino != null)
        {
            return "Executor submarino sem progresso";
        }

        if (controleAviaoCaca != null || controleAviao != null || helicopteroExterno != null || c700TransporteAereo != null)
        {
            return "Executor aereo sem progresso";
        }

        return string.IsNullOrEmpty(executorControleAtual) ? "Sem executor de movimento" : "Sem progresso no executor " + executorControleAtual;
    }

    private bool PodeReemitirOrdemTravada(string causa)
    {
        if (string.IsNullOrEmpty(causa))
        {
            return true;
        }

        return !TextoContem(causa, "Sem combustivel")
               && !TextoContem(causa, "fora do NavMesh")
               && !TextoContem(causa, "desativado")
               && !TextoContem(causa, "aeroporto")
               && !TextoContem(causa, "Sem executor")
               && !TextoContem(causa, "caminho invalido");
    }

    private bool TextoContem(string texto, string trecho)
    {
        return !string.IsNullOrEmpty(texto)
               && !string.IsNullOrEmpty(trecho)
               && texto.IndexOf(trecho, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RegistrarWatchdogBloqueado(string causa)
    {
        if (Time.unscaledTime < proximoRelatorioWatchdogBloqueado)
        {
            return;
        }

        proximoRelatorioWatchdogBloqueado = Time.unscaledTime + IntervaloRelatorioWatchdogBloqueado;
        string motivo = string.IsNullOrEmpty(causa) ? "causa nao identificada" : causa;
        DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("orders_stuck_blocked");
        DiagnosticoDesempenhoJogo.RegistrarEvento("OrdemBloqueada", $"{name}: {ordemControleAtual} sem progresso; motivo={motivo}; tentativas={reemissoesWatchdogOrdem}; destino={ultimoDestinoOrdenado}");
    }

    void LimparDestinoOrdenado()
    {
        possuiDestinoOrdenado = false;
        ultimoDestinoOrdenado = Vector3.zero;
        tempoSemProgressoOrdem = 0f;
        reemissoesWatchdogOrdem = 0;
        if (ordemControleAtual == OrdemControleUnidade.Movendo || ordemControleAtual == OrdemControleUnidade.Recuando)
        {
            ordemControleAtual = OrdemControleUnidade.Ociosa;
        }
    }

    bool TentarDesenharLinhaFallback()
    {
        if (!possuiDestinoOrdenado)
        {
            return false;
        }

        float distancia = Vector3.Distance(transform.position, ultimoDestinoOrdenado);
        if (distancia <= 2.5f)
        {
            LimparDestinoOrdenado();
            return false;
        }

        linhaCaminho.positionCount = 2;
        linhaCaminho.SetPosition(0, transform.position + Vector3.up * 0.8f);
        linhaCaminho.SetPosition(1, ultimoDestinoOrdenado + Vector3.up * 0.8f);
        return true;
    }

    public void DefinirSelecao(bool estado)
    {
        selecionado = estado;
        
        if (anelSelecao != null) anelSelecao.SetActive(estado);
        
        if (estado)
        {
            if (linhaAlcance == null) CriarVisualAlcance();
            if (linhaCaminho == null) CriarVisualCaminho();
            
            if (linhaAlcance != null) linhaAlcance.gameObject.SetActive(true);
        }
        else
        {
            if (linhaAlcance != null) linhaAlcance.gameObject.SetActive(false);
            if (linhaCaminho != null) linhaCaminho.gameObject.SetActive(false);
        }
    }

    private void CancelarOrdemEspecial(bool redefinirEstado)
    {
        ComportamentoPatrulhaUniversal patrulhaUniversal = GetComponent<ComportamentoPatrulhaUniversal>();
        if (patrulhaUniversal != null)
        {
            patrulhaUniversal.enabled = false;
            Destroy(patrulhaUniversal);
        }

        ComportamentoSeguirUniversal seguirUniversal = GetComponent<ComportamentoSeguirUniversal>();
        if (seguirUniversal != null)
        {
            seguirUniversal.enabled = false;
            Destroy(seguirUniversal);
        }

        RestaurarVelocidadeOriginal();

        if (redefinirEstado && (ordemControleAtual == OrdemControleUnidade.Patrulhando || ordemControleAtual == OrdemControleUnidade.Seguindo))
        {
            ordemControleAtual = OrdemControleUnidade.Ociosa;
        }
    }

    private void AtualizarTrilhaOficial()
    {
        if (controleSubmarino != null)
        {
            dominioControleAtual = DominioControleUnidade.NavalSubmerso;
            executorControleAtual = nameof(ControleSubmarino);
            return;
        }

        if (helicopteroExterno != null || c700TransporteAereo != null || controleAviao != null || controleAviaoCaca != null || scriptVoo != null || ehAereo)
        {
            dominioControleAtual = DominioControleUnidade.Aereo;

            if (c700TransporteAereo != null) executorControleAtual = nameof(C700TransporteAereo);
            else if (helicopteroExterno != null) executorControleAtual = nameof(Helicoptero);
            else if (controleAviao != null) executorControleAtual = nameof(ControleAviao);
            else if (controleAviaoCaca != null) executorControleAtual = nameof(ControleAviaoCaca);
            else if (scriptVoo != null) executorControleAtual = nameof(VooHelicoptero);
            else executorControleAtual = "MovimentoAereoGenerico";

            return;
        }

        if (controleNavioRealista != null || GetComponent<IdentidadeNaval>() != null || TryGetComponent<NavioPetroleiro>(out _))
        {
            dominioControleAtual = DominioControleUnidade.NavalSuperficie;
            executorControleAtual = controleNavioRealista != null ? nameof(ControleNavioRealista) : "NavalAuxiliar";
            return;
        }

        dominioControleAtual = DominioControleUnidade.Terrestre;
        executorControleAtual = TryGetComponent<MovimentoRealTerrestre>(out _) ? nameof(MovimentoRealTerrestre) : nameof(NavMeshAgent);
    }

    private void AtualizarEstadoDeBloqueio()
    {
        bloqueioControleAtivo = false;
        motivoBloqueioControle = string.Empty;

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            bloqueioControleAtivo = true;
            motivoBloqueioControle = "Sem combustivel";
            return;
        }

        if (helicopteroExterno != null && helicopteroExterno.EstaSobControleDoAeroporto())
        {
            bloqueioControleAtivo = true;
            motivoBloqueioControle = "Helicoptero sob controle do aeroporto";
        }
    }

    private void ValidarConflitosDeControle()
    {
        if (controleNavioRealista != null && navegacaoInteligenteNaval != null)
        {
            navegacaoInteligenteNaval.enabled = false;
            Debug.LogWarning($"[ControleUnidade] {name} ainda possui NavegacaoInteligenteNaval, mas essa trilha foi desativada. Remova o componente legado do objeto.", this);
        }

        if (controleSubmarino != null && controleNavioRealista != null)
        {
            Debug.LogError($"[ControleUnidade] {name} mistura executor submarino com executor naval de superficie. Essa unidade precisa de uma unica autoridade naval.", this);
        }

        if (helicopteroExterno != null && (controleAviao != null || c700TransporteAereo != null))
        {
            Debug.LogError($"[ControleUnidade] {name} mistura Helicoptero com controladores aereos de aviao/transporte. Essa combinacao nao e suportada na trilha oficial.", this);
        }

        if (c700TransporteAereo != null && controleAviao == null)
        {
            Debug.LogWarning($"[ControleUnidade] {name} possui C700TransporteAereo sem ControleAviao. Revise o prefab antes da migracao final.", this);
        }
    }

    private void SincronizarModoCombateOficial()
    {
        bool passivo;
        string descricao;
        if (TryObterEstadoCombate(out passivo, out descricao))
        {
            modoCombateOficialAtivo = !passivo;
        }
    }
}
