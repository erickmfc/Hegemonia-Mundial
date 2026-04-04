using UnityEngine;
using UnityEngine.AI;

public class ControleUnidade : MonoBehaviour
{
    private NavMeshAgent agente;
    private Animator animator; // Referência para as animações
    
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
    private bool cacheCombateSujo = true;
    private ControleTorreta[] cacheTorretas = System.Array.Empty<ControleTorreta>();
    private ControleTorretaModular[] cacheTorretasModulares = System.Array.Empty<ControleTorretaModular>();
    private SistemaAntiMissil[] cacheSistemasAntiMissil = System.Array.Empty<SistemaAntiMissil>();
    private SistemaDeTiro[] cacheSistemasDeTiro = System.Array.Empty<SistemaDeTiro>();
    private LancadorMultiplo[] cacheLancadoresMultiplos = System.Array.Empty<LancadorMultiplo>();
    private LancadorMisselCaca[] cacheLancadoresCaca = System.Array.Empty<LancadorMisselCaca>();

    protected virtual void Awake()
    {
        SanearBoxCollidersComEscalaNegativa();
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>(); 
        
        // Verifica controladores externos
        helicopteroExterno = GetComponent<Helicoptero>();
        controleAviao = GetComponent<ControleAviao>();
        controleAviaoCaca = GetComponent<ControleAviaoCaca>();
        c700TransporteAereo = GetComponent<C700TransporteAereo>();
        hovercraftTransporte = GetComponent<HovercraftTransporte>();
        controleNavioRealista = GetComponent<ControleNavioRealista>();
        navegacaoInteligenteNaval = GetComponent<NavegacaoInteligenteNaval>();
        controleSubmarino = GetComponent<ControleSubmarino>();
        
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
        // 0. Desenha a linha de caminho (Rota) se estiver selecionado, para todos os tipos (Terra/Ar/Mar)
        AtualizarVisualCaminho();

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
            float distanciaAoDestino = Vector3.Distance(transform.position, ultimoDestinoOrdenado);
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

        // 3. Controle de Animação (Genérico)
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat("Velocidade", velocidadeAtual);
        }

        // 4. Controle de Movimento pelo Mouse (se selecionado)
        // DESLIGADO AGORA: O GerenteSelecao controla o movimento em grupo!
        /*
        if (Input.GetMouseButtonDown(1) && selecionado)
        {
            MoverParaMouse();
        }
        */
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
        MoverParaPonto(destino, true);
    }

    // COMANDO AUTOMÁTICO (Usado pela fábrica e IA)
    public void MoverParaPonto(Vector3 destino, bool cancelarComportamentos = true)
    {
        RegistrarDestinoOrdenado(destino);

        if (cancelarComportamentos)
        {
            // Interrompe comportamentos especiais se receber ordem DIRETA do jogador (clique direito)
            var patCaminho = GetComponent<ComportamentoPatrulhaCaminho>();
            if (patCaminho != null) Destroy(patCaminho);
            
            var seg = GetComponent<ComportamentoSeguir>();
            if (seg != null) Destroy(seg);
            
            var patUniv = GetComponent<ComportamentoPatrulhaUniversal>();
            if (patUniv != null) Destroy(patUniv);
            
            var segUniv = GetComponent<ComportamentoSeguirUniversal>();
            if (segUniv != null) 
            {
                Destroy(segUniv);
                RestaurarVelocidadeOriginal();
            }
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

                // Verifica se esta unidade tem navegação naval inteligente (marcha à ré automática)
                if (navegacaoInteligenteNaval != null)
                {
                    // Usa o sistema inteligente que decide automaticamente se vai de frente ou de ré
                    navegacaoInteligenteNaval.DefinirDestino(destino);
                    // Debug.Log($"[Navegação] {name} usando sistema naval inteligente.");
                    return;
                }

                // Verifica se é Submarino
                if (controleSubmarino != null)
                {
                    controleSubmarino.DefinirDestino(destino);
                    return;
                }

                // Navegação normal (terrestre ou navio sem o sistema inteligente)
                agente.SetDestination(destino);
                if (agente.isOnNavMesh) agente.isStopped = false;
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

                         if (navegacaoInteligenteNaval != null)
                         {
                             navegacaoInteligenteNaval.DefinirDestino(destino);
                             return;
                         }

                         if (controleSubmarino != null)
                         {
                             controleSubmarino.DefinirDestino(destino);
                             return;
                         }

                         agente.SetDestination(destino);
                         if (agente.isOnNavMesh) agente.isStopped = false;
                     }
                 }
                 catch (System.Exception ex)
                 {
                     Debug.LogWarning($"[ControleUnidade] Falha ao recuperar NavMeshAgent para {name}: {ex.Message}");
                 }
            }
        }
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
               navegacaoInteligenteNaval != null ||
               controleSubmarino != null ||
               TryGetComponent<NavioPetroleiro>(out _);
    }

    public bool DefinirModoCombate(bool ativo)
    {
        GarantirCacheCombate();
        bool alterouAlgo = false;

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

        for (int i = 0; i < cacheSistemasAntiMissil.Length; i++)
        {
            SistemaAntiMissil sistema = cacheSistemasAntiMissil[i];
            if (sistema == null) continue;
            sistema.DefinirModoAtivo(ativo);
            alterouAlgo = true;
        }

        for (int i = 0; i < cacheSistemasDeTiro.Length; i++)
        {
            SistemaDeTiro sistema = cacheSistemasDeTiro[i];
            if (sistema == null) continue;
            sistema.DefinirModoPassivo(!ativo);
            alterouAlgo = true;
        }

        for (int i = 0; i < cacheLancadoresMultiplos.Length; i++)
        {
            LancadorMultiplo lancador = cacheLancadoresMultiplos[i];
            if (lancador == null) continue;
            lancador.modoAutomatico = ativo;
            alterouAlgo = true;
        }

        for (int i = 0; i < cacheLancadoresCaca.Length; i++)
        {
            LancadorMisselCaca lancador = cacheLancadoresCaca[i];
            if (lancador == null) continue;
            lancador.modoPassivo = !ativo;
            alterouAlgo = true;
        }

        return alterouAlgo;
    }

    public bool TryObterEstadoCombate(out bool passivo, out string descricao)
    {
        GarantirCacheCombate();
        bool encontrou = false;
        bool estadoInicial = false;
        bool misto = false;

        RegistrarEstadoCombate(cacheTorretas, ref encontrou, ref estadoInicial, ref misto, delegate(ControleTorreta t) { return t != null && t.modoPassivo; });
        RegistrarEstadoCombate(cacheTorretasModulares, ref encontrou, ref estadoInicial, ref misto, delegate(ControleTorretaModular t) { return t != null && t.modoPassivo; });
        RegistrarEstadoCombate(cacheSistemasAntiMissil, ref encontrou, ref estadoInicial, ref misto, delegate(SistemaAntiMissil t) { return t != null && t.modoPassivo; });
        RegistrarEstadoCombate(cacheSistemasDeTiro, ref encontrou, ref estadoInicial, ref misto, delegate(SistemaDeTiro t) { return t != null && t.modoPassivo; });
        RegistrarEstadoCombate(cacheLancadoresCaca, ref encontrou, ref estadoInicial, ref misto, delegate(LancadorMisselCaca t) { return t != null && t.modoPassivo; });
        RegistrarEstadoCombate(cacheLancadoresMultiplos, ref encontrou, ref estadoInicial, ref misto, delegate(LancadorMultiplo t) { return t != null && !t.modoAutomatico; });

        if (!encontrou)
        {
            passivo = false;
            descricao = "--";
            return false;
        }

        if (misto)
        {
            passivo = false;
            descricao = "MISTO";
            return true;
        }

        passivo = estadoInicial;
        descricao = passivo ? "PASSIVO" : "ATIVO";
        return true;
    }

    public void AplicarLimiteVelocidade(float velocidadeAlvo)
    {
        // Salva a original apenas na primeira vez
        if (velocidadeOriginalSalva < 0f)
        {
            if (TryGetComponent<ControleAviao>(out var aviao)) velocidadeOriginalSalva = aviao.velocidadeMaximaVoo;
            else if (TryGetComponent<ControleNavioRealista>(out var nav1)) velocidadeOriginalSalva = nav1.velocidadeMaxima;
            else if (TryGetComponent<NavegacaoInteligenteNaval>(out var nav2)) velocidadeOriginalSalva = nav2.velocidadeMaxima;
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
        else if (TryGetComponent<ControleNavioRealista>(out var nav1)) nav1.velocidadeMaxima = v;
        else if (TryGetComponent<NavegacaoInteligenteNaval>(out var nav2)) nav2.velocidadeMaxima = v;
        else if (TryGetComponent<NavMeshAgent>(out var nma)) { if(nma.enabled) nma.speed = v; }
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

    private void GarantirCacheCombate()
    {
        if (!cacheCombateSujo)
        {
            return;
        }

        cacheTorretas = GetComponentsInChildren<ControleTorreta>(true);
        cacheTorretasModulares = GetComponentsInChildren<ControleTorretaModular>(true);
        cacheSistemasAntiMissil = GetComponentsInChildren<SistemaAntiMissil>(true);
        cacheSistemasDeTiro = GetComponentsInChildren<SistemaDeTiro>(true);
        cacheLancadoresMultiplos = GetComponentsInChildren<LancadorMultiplo>(true);
        cacheLancadoresCaca = GetComponentsInChildren<LancadorMisselCaca>(true);
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
        
        linhaCaminho.material = new Material(Shader.Find("Sprites/Default"));
        linhaCaminho.startColor = corCaminho;
        linhaCaminho.endColor = corCaminho;
        linhaCaminho.gameObject.SetActive(false);
        linhaCaminho.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void AtualizarVisualCaminho()
    {
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
    }

    void RegistrarDestinoOrdenado(Vector3 destino)
    {
        ultimoDestinoOrdenado = destino;
        possuiDestinoOrdenado = true;
    }

    void LimparDestinoOrdenado()
    {
        possuiDestinoOrdenado = false;
        ultimoDestinoOrdenado = Vector3.zero;
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
}
