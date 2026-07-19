using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

/// <summary>
/// GerenciadorPortaAvioes - Especializado para navios porta-aviões.
/// Herda de GerenciadorAeroporto para manter compatibilidade com ControleAviao,
/// mas substitui a interface e lógica de pátio por um sistema naval dinâmico.
/// </summary>
public class GerenciadorPortaAvioes : GerenciadorAeroporto
{
    [Header("=== EXCLUSIVO PORTA-AVIÕES ===")]
    public Transform antenaRotativa;
    public float velGiroAntena = 45f;
    
    [Header("=== ANIMAÇÃO DA RAMPA DE DECOLAGEM ===")]
    public Transform rampaDecolagem;
    private Vector3 _posicaoDefaultRampa;
    private Quaternion _rotacaoDefaultRampa;
    private ControleAviao _aviaoNoPrepara = null;
    private Coroutine _rotinaRampa = null;
    
    [Header("=== SISTEMA DE ELEVADOR ===")]
    public Transform plataformaElevador;
    public Transform nivelHangar;   // Ponto onde o elevador fica no fundo
    public Transform nivelConves;   // Ponto onde o elevador fica no topo
    public float tempoElevador = 4f;

    [Header("=== POSIÇÕES LOCAIS DO ELEVADOR (CONFIG) ===")]
    public Vector3 localHangarInterno = new Vector3(1.52f, -0.78f, -2.07f); 
    public Vector3 localHangarFrente  = new Vector3(1.18f, -0.78f, 8.66f);  
    public Vector3 localConvesEntrada = new Vector3(1.18f, 6.13f, 8.66f);   
    
    [Header("=== HIERARQUIA DE PISTAS (OPCIONAL) ===")]
    public Transform grupoPistaLanding;
    [Tooltip("Auto-detect: transform 'Descer h' usado para alinhar helicópteros antes das paradas do convés.")]
    public Transform pontoAlinhamentoHelicoptero;

    [Header("=== PONTOS DE TAXI EXCLUSIVOS ===")]
    public Transform waypointSaidaPista;
    public Transform waypointEntradaElevador;
    public Transform grupoParadas; // NOVO: Grupo com os pontos "parada" do convés
    
    [Header("=== DEBUG ===")]
    public bool debugLogs = false;
    [Tooltip("Se true, clicar no casco abre o menu. Se false, somente a tecla O controla o menu.")]
    public bool abrirMenuAoCliqueNoNavio = false;
    
    private bool _menuCarrierAtivo = false;
    private bool _elevadorOcupado = false;
    private ControleAviao _selecionadoCarrier;
    private int _modoOrdemAviao = 0; // 0=Ataque/Recon, 1=Patrulha, 2=Seguir
    private Vector2 _scrollCarrier;
    private Vector2 _scrollMenuCarrierGeral;
    private IdentidadeUnidade _idCarrier;
    private ControleUnidade _controleUnidade;
    private Camera _cameraPrincipal;

    [Header("=== RADAR DE CONTROLE AÉREO ===")]
    public float raioRadarResgate = 1500f; // Aumentei um pouco o alcance para facilitar
    private List<ControleAviao> _avioesProximosNoAr = new List<ControleAviao>();
    private readonly List<Helicoptero> _helicopterosProximosNoAr = new List<Helicoptero>();
    private readonly List<ControleAviao> _bufferScanAvioes = new List<ControleAviao>(48);
    private readonly List<Helicoptero> _bufferScanHelicopteros = new List<Helicoptero>(32);
    private float _tempoProximoScan = 0f;
    private Vector2 _scrollHelisCarrier;
    private readonly List<Vector3> _rotaPatrulhaAviaoCarrier = new List<Vector3>();
    private readonly List<Vector3> _rotaPatrulhaHelicopteroCarrier = new List<Vector3>();
    private readonly Dictionary<int, Coroutine> _rotinasRecebimentoHeliCarrier = new Dictionary<int, Coroutine>();
    private LineRenderer _linhaPatrulhaAviaoCarrier;

    // ======================================================
    // Cache UI (IMGUI): evita queda grande de FPS ao listar muitas unidades por frame
    // ======================================================
    [Header("=== OTIMIZAÇÃO DA UI ===")]
    [Tooltip("Mantém listas resumidas por padrão no menu do porta-aviões. A lista completa continua disponível pelo botão do menu.")]
    public bool modoDesempenhoUI = true;
    private bool _uiMostrarListaCompleta = false;
    private const int UI_MAX_ITENS_LISTA_RESUMIDA = 10;
    private GUISkin _skinCache;
    private GUIStyle _uiLinhaCompacta;
    private GUIStyle _uiLabelCompacta;
    private GUIStyle _uiLabelWrap;
    private readonly HashSet<int> _helicopterosVistosScan = new HashSet<int>(64);

    private enum ModoOrdemHelicopteroCarrier
    {
        Nenhum,
        Reconhecimento,
        Patrulha,
        AtaqueLocal
    }

    private ModoOrdemHelicopteroCarrier _modoOrdemHelicopteroCarrier = ModoOrdemHelicopteroCarrier.Nenhum;

    void LogDebug(string msg)
    {
        if (debugLogs)
            Debug.Log(msg);
    }

    private static string CompactarTextoMenu(string texto, int maxChars)
    {
        if (string.IsNullOrEmpty(texto) || maxChars <= 0 || texto.Length <= maxChars)
        {
            return texto;
        }

        if (maxChars <= 3)
        {
            return texto.Substring(0, maxChars);
        }

        return texto.Substring(0, maxChars - 3) + "...";
    }

    private string NormalizarNomeHierarquia(string nome)
    {
        return string.IsNullOrEmpty(nome)
            ? string.Empty
            : nome.Replace(" ", string.Empty).ToLowerInvariant();
    }

    private Transform EncontrarTransformPorNomeExato(string nome)
    {
        string alvo = NormalizarNomeHierarquia(nome);
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (NormalizarNomeHierarquia(t.name) == alvo)
            {
                return t;
            }
        }
        return null;
    }

    private Transform EncontrarTransformPorTrechos(params string[] trechos)
    {
        if (trechos == null || trechos.Length == 0)
        {
            return null;
        }

        string[] termos = trechos
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => NormalizarNomeHierarquia(t))
            .ToArray();

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            string nome = NormalizarNomeHierarquia(t.name);
            bool bate = true;
            for (int i = 0; i < termos.Length; i++)
            {
                if (!nome.Contains(termos[i]))
                {
                    bate = false;
                    break;
                }
            }

            if (bate)
            {
                return t;
            }
        }

        return null;
    }

    private void AutoDetectarHierarquiaSovereign()
    {
        if (grupoParadas == null)
        {
            grupoParadas = EncontrarTransformPorNomeExato("Patio aberto") ?? EncontrarTransformPorTrechos("patio", "aberto");
        }

        if (patio == null && grupoParadas != null)
        {
            patio = grupoParadas;
        }

        if (decolagem == null)
        {
            decolagem = EncontrarTransformPorNomeExato("Decolagem") ?? EncontrarTransformPorTrechos("decolagem");
        }

        if (grupoPistaLanding == null)
        {
            grupoPistaLanding = EncontrarTransformPorNomeExato("pouso") ?? EncontrarTransformPorTrechos("pouso");
        }

        if (grupoPistaLanding == null)
        {
            Transform pistas = EncontrarTransformPorNomeExato("Pistas") ?? EncontrarTransformPorTrechos("pistas");
            if (pistas != null)
            {
                foreach (Transform filho in pistas.GetComponentsInChildren<Transform>(true))
                {
                    if (filho == null || filho == pistas) continue;
                    string nome = NormalizarNomeHierarquia(filho.name);
                    if (nome.Contains("pouso"))
                    {
                        grupoPistaLanding = filho;
                        break;
                    }
                }
            }
        }

        if (decida == null && grupoPistaLanding != null)
        {
            decida = grupoPistaLanding;
        }

        if (waypointSaidaPista == null && grupoPistaLanding != null)
        {
            foreach (Transform t in grupoPistaLanding)
            {
                if (t == null) continue;
                string nome = NormalizarNomeHierarquia(t.name);
                if (nome.Contains("parada") || nome.Contains("stop"))
                {
                    waypointSaidaPista = t;
                    break;
                }
            }
        }

        if (pontoAlinhamentoHelicoptero == null)
        {
            pontoAlinhamentoHelicoptero = EncontrarTransformPorNomeExato("Descer h") ?? EncontrarTransformPorTrechos("descer", "h");
        }
    }

    private Vector3 ObterDestinoAlinhamentoHelicoptero()
    {
        if (pontoAlinhamentoHelicoptero == null)
        {
            return transform.position + Vector3.up * 12f;
        }

        Vector3 destino = pontoAlinhamentoHelicoptero.position;
        destino.y = Mathf.Max(destino.y + 2.5f, 12f);
        return destino;
    }

    private IEnumerator EsperarHelicopteroChegarAoPonto(Helicoptero heli, Vector3 ponto, float timeout, float distanciaAceita)
    {
        float tempo = 0f;
        float distanciaLimite = Mathf.Max(1.5f, distanciaAceita);

        while (tempo < timeout && heli != null)
        {
            tempo += Time.deltaTime;
            Vector2 heliXZ = new Vector2(heli.transform.position.x, heli.transform.position.z);
            Vector2 pontoXZ = new Vector2(ponto.x, ponto.z);
            if (Vector2.Distance(heliXZ, pontoXZ) <= distanciaLimite)
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator EsperarHelicopteroChegarAoPontoMovel(Helicoptero heli, Transform pontoMovel, float timeout, float distanciaAceita)
    {
        float tempo = 0f;
        float distanciaLimite = Mathf.Max(1.5f, distanciaAceita);

        while (tempo < timeout && heli != null && pontoMovel != null)
        {
            tempo += Time.deltaTime;
            Vector3 ponto = pontoMovel.position;
            ponto.y = Mathf.Max(ponto.y + 2.5f, 12f);
            heli.destino = ponto;

            Vector2 heliXZ = new Vector2(heli.transform.position.x, heli.transform.position.z);
            Vector2 pontoXZ = new Vector2(ponto.x, ponto.z);
            if (Vector2.Distance(heliXZ, pontoXZ) <= distanciaLimite)
            {
                yield break;
            }

            yield return null;
        }
    }

    private bool HelicopteroEstaEmRecebimentoCarrier(Helicoptero heli)
    {
        if (heli == null)
        {
            return false;
        }

        return _rotinasRecebimentoHeliCarrier.ContainsKey(heli.GetInstanceID());
    }

    protected override void Awake()
    {
        AutoDetectarHierarquiaSovereign();
        base.Awake(); // Inicializa lógica base do Aeroporto

        if (rampaDecolagem == null)
        {
            rampaDecolagem = transform.Find("Rampa") ?? transform.Find("rampa");
            if (rampaDecolagem == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child.name.ToLower().Contains("rampa") && !child.name.ToLower().Contains("landing"))
                    {
                        rampaDecolagem = child;
                        break;
                    }
                }
            }
        }
        
        if (rampaDecolagem != null)
        {
            _posicaoDefaultRampa = rampaDecolagem.localPosition;
            _rotacaoDefaultRampa = rampaDecolagem.localRotation;

            if (!EhCenaDeMenuAtiva())
            {
                // Em jogo, a rampa inicia recolhida para a rotina de taxi/decolagem.
                Vector3 pos = _posicaoDefaultRampa;
                pos.y = -4.4f;
                rampaDecolagem.localPosition = pos;

                Vector3 rot = _rotacaoDefaultRampa.eulerAngles;
                rot.x = 20f;
                rampaDecolagem.localRotation = Quaternion.Euler(rot);
            }
        }
        
        _idCarrier = GetComponent<IdentidadeUnidade>();
        if (_idCarrier == null)
        {
            IdentidadeIA identidadeIA = GetComponent<IdentidadeIA>();
            if (identidadeIA != null)
            {
                _idCarrier = gameObject.AddComponent<IdentidadeUnidade>();
                _idCarrier.teamID = identidadeIA.teamID;
                _idCarrier.nomeDoPais = identidadeIA.nomeComandante;
            }
        }

        _controleUnidade = GetComponent<ControleUnidade>();
        
        // 1. Mapeia o Pátio Aberto (Prioriza o grupoParadas se existir)
        Transform grupoParaPatio = (grupoParadas != null) ? grupoParadas : patio;
        if (grupoParaPatio != null)
        {
            waypointsPatio.Clear(); // Evita duplicata se base.Awake já preencheu usando 'patio'
            foreach (Transform t in grupoParaPatio) 
            {
                if (grupoParadas != null) 
                {
                    waypointsPatio.Add(t); // Tira a restrição de nome se tiver um grupo específico!
                }
                else 
                {
                    string nm = t.name.ToLower();
                    if (nm.Contains("parada") || nm.Contains("vaga") || nm.Contains("ponto") || nm.Contains("deck")) waypointsPatio.Add(t);
                }
            }
        }
        
        // 2. Mapeia Decolagem
        if (decolagem != null)
        {
            waypointsDecolagem.Clear(); // Evita duplicata
            foreach (Transform t in decolagem) waypointsDecolagem.Add(t);
            LogDebug($"[Porta-Aviões] Sequência de Decolagem: {string.Join(" -> ", waypointsDecolagem.Where(w => w != null).Select(w => w.name))}");
        }

        // 3. Mapeia Pouso (Apenas filhos diretos do grupo "Pouso", mantendo a ordem exata da hierarquia)
        Transform pistaParaUsar = (grupoPistaLanding != null) ? grupoPistaLanding : decida;
        if (pistaParaUsar != null)
        {
            List<Transform> listaTemp = new List<Transform>();
            foreach (Transform t in pistaParaUsar)
            {
                if (t == null) continue;
                // Previne que o próprio objeto "Pouso" ou qualquer pai vazado entre na lista
                if (t == pistaParaUsar) continue;
                string nm = t.name.Trim().ToLowerInvariant();
                if (nm == "pouso") continue;

                listaTemp.Add(t);
            }
            
            // O Segredo para não pular: MANTER A ORDEM EXATA DA HIERARQUIA DO UNITY!
            // Ele vai ler exatamente na ordem: 1 -> 1 (1) -> 1 (2) -> ... -> Parando
            waypointsDecida = listaTemp.ToList();
            
            LogDebug($"[Porta-Aviões] Sequência de Pouso (Glideslope): {string.Join(" -> ", waypointsDecida.Select(w => w.name))}");

            // --- PONTO DE APROXIMAÇÃO REALISTA ---
            // Cria um ponto 600m atrás do navio para o avião alinhar antes de iniciar a descida
            if (waypointsDecida.Count > 0)
            {
                GameObject approach = new GameObject("Ponto_Aproximacao_Navio");
                approach.transform.SetParent(this.transform);
                
                // Calcula direção da pista baseada nos dois primeiros pontos da hierarquia (ex: 1 e 1(1))
                Vector3 direcaoPista = (waypointsDecida.Count > 1) ? 
                    (waypointsDecida[0].position - waypointsDecida[1].position).normalized : transform.forward;
                
                // Coloca o ponto de entrada 600 metros atrás para aproximação em arco suave
                approach.transform.position = waypointsDecida[0].position + direcaoPista * 600f + Vector3.up * 50f;
                waypointsDecida.Insert(0, approach.transform);
            }
        }
        
        // 4. Mapeia a Pista de Táxi ("Trabalho")
        Transform grupoTrabalho = EncontrarTransformPorNomeExato("Trabalho") ?? EncontrarTransformPorTrechos("trabalho");
        if (grupoTrabalho != null)
        {
            waypointsTaxi.Clear();
            foreach (Transform t in grupoTrabalho)
            {
                if (t != null && t != grupoTrabalho)
                {
                    waypointsTaxi.Add(t);
                }
            }
        }

        // 5. Ajusta os waypoints clássicos (wpAndadar e wpAnalise) para compatibilidade com a máquina de estados base
        if (waypointsTaxi.Count > 0)
        {
            // wpAndadar é o início do táxi. Se a lista existir, é o primeiro ponto do Trabalho.
            if (wpAndadar == null) wpAndadar = waypointsTaxi[0];
            
            // wpAnalise (Busca) é o fim do táxi, onde o avião aguarda/alinha antes de ir pra vaga.
            if (wpAnalise == null) wpAnalise = waypointsTaxi[waypointsTaxi.Count - 1];
        }
        else
        {
            // Fallbacks padrão caso não haja pasta Trabalho (mantém compatibilidade)
            if (wpAndadar == null)
            {
                if (waypointsDecida != null && waypointsDecida.Count > 0)
                    wpAndadar = waypointsDecida.Last();
                else if (waypointSaidaPista != null)
                    wpAndadar = waypointSaidaPista;
                else
                    wpAndadar = transform;
            }
            if (wpAnalise == null)
            {
                wpAnalise = wpAndadar;
            }
        }
    }

    protected override void Update()
    {
        // 1. Rotação da Antena
        if (antenaRotativa != null)
            antenaRotativa.Rotate(Vector3.up * velGiroAntena * Time.deltaTime);

        LimparHelicopterosTransferidos();

        // ==========================================
        // 2. SISTEMA DA TECLA 'O' COM TRAVA DE SEGURANÇA (CORRIGIDO)
        // ==========================================
        if (Input.GetKeyDown(KeyCode.O))
        {
            if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto) return;

            // SÓ ABRE SE O NAVIO ESTIVER SELECIONADO PELO JOGADOR
            if (_controleUnidade != null && !_controleUnidade.selecionado)
            {
                return; // Ignora se não estiver selecionado
            }

            LogDebug("[Porta-Aviões] Você apertou a tecla O no navio selecionado!");
            
            // Tenta achar a Identidade caso tenha falhado no Awake
            if (_idCarrier == null) _idCarrier = GetComponent<IdentidadeUnidade>();

            if (_idCarrier == null || _idCarrier.teamID != 1)
            {
                _menuCarrierAtivo = false;
                GestorMenusExclusivos.Fechar(this);
                return;
            }

            bool novoEstado = !_menuCarrierAtivo;
            if (novoEstado) GestorMenusExclusivos.Abrir(this);
            else GestorMenusExclusivos.Fechar(this);
            _menuCarrierAtivo = novoEstado;

        }

        // ==========================================
        // 3. SISTEMA DE CLIQUE NO NAVIO (OPCIONAL)
        // ==========================================
        if (abrirMenuAoCliqueNoNavio && Input.GetMouseButtonDown(0)) 
        {
            if (!GestorMenusExclusivos.CliqueBloqueadoPelaUI())
            {
                if (_cameraPrincipal == null) _cameraPrincipal = Camera.main;
                if (_cameraPrincipal == null) return;
                Ray raioCamera = _cameraPrincipal.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(raioCamera, out RaycastHit hit))
                {
                    if (hit.transform == this.transform || hit.transform.IsChildOf(this.transform))
                    {
                        if (!_identidadeVerificada) { _identidadeCacheada = GetComponent<IdentidadeUnidade>(); _identidadeVerificada = true; }
                        if (_identidadeCacheada == null || _identidadeCacheada.teamID != 1)
                        {
                            return; 
                        }

                        LogDebug("[Porta-Aviões] Você clicou no navio!");
                        GestorMenusExclusivos.Abrir(this);
                        _menuCarrierAtivo = true; // Abre o menu ao clicar
                    }
                }
            }
        }

        if (_menuCarrierAtivo && !GestorMenusExclusivos.EstaAtivo(this))
        {
            _menuCarrierAtivo = false;
        }

        // 4. Scan de Aviões Próximos no Céu
        if (_menuCarrierAtivo && Time.time > _tempoProximoScan)
        {
            EscanearAvioesNoAr();
            EscanearHelicopterosNoAr();
            _tempoProximoScan = Time.time + 2f;
        }

        if (_modoOrdemHelicopteroCarrier != ModoOrdemHelicopteroCarrier.Nenhum && helicopteroSelecionadoParaMissao != null)
        {
            ProcessarOrdemHelicopteroCarrier();
        }

        // 5. Radar de Destino (Para mandar atacar) com Mouse Direito
        if (_selecionadoCarrier != null && _selecionadoCarrier.aguardandoCliqueRadar && !_menuCarrierAtivo)
        {
            ProcessarOrdemAviaoCarrier();
        }

        // 6. SISTEMA DE "CONVÈS ADERENTE" (Parenting)
        // Garante que aviões no navio se movam JUNTO com o navio
        GerenciarParentescoAeronaves();
        AtualizarModoInteracaoManualAeroporto();
    }

    void GerenciarParentescoAeronaves()
    {
        // Limpa listas de nulos primeiro
        RemoveNulls(avioesNoPatio);
        RemoveNulls(avioesNoHangar);

        // Processa aviões no pátio
        foreach (var av in avioesNoPatio)
        {
            if (av == null) continue;
            // Se estiver no chão/pátio ou decolando, gruda no navio
            if (av.transform.parent != this.transform && 
                (av.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio || 
                 av.estadoAtual == ControleAviao.EstadoAviao.Taxiando ||
                 av.estadoAtual == ControleAviao.EstadoAviao.Decolando ||
                 av.estadoAtual == ControleAviao.EstadoAviao.RetornandoPraVaga))
            {
                av.transform.SetParent(this.transform, true);
            }
            // Se começou a voar, solta do navio
            else if (av.transform.parent == this.transform && 
                    (av.estadoAtual == ControleAviao.EstadoAviao.EmMissao))
            {
                av.transform.SetParent(null, true);
            }
        }

        // Processa aviões no hangar (se não estiverem no elevador)
        if (!_elevadorOcupado)
        {
            foreach (var av in avioesNoHangar)
            {
                if (av != null && av.transform.parent != this.transform)
                {
                    av.transform.SetParent(this.transform, true);
                }
            }
        }

        for (int i = helicopterosDoAeroporto.Count - 1; i >= 0; i--)
        {
            Helicoptero heli = helicopterosDoAeroporto[i];
            if (heli == null)
            {
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            if (!HelicopteroPertenceAEstaBase(heli))
            {
                _rotinasRecebimentoHeliCarrier.Remove(heli.GetInstanceID());
                heli.DesancorarDeRaizMovel(transform);
                helicopterosDoAeroporto.RemoveAt(i);
                continue;
            }

            if (heli.estaVoando)
            {
                heli.DesancorarDeRaizMovel(transform);
                continue;
            }

            if (heli.EstaEmPreparacaoDecolagem())
            {
                continue;
            }

            Transform vagaHeli = heli.ObterVagaAeroporto();
            if (vagaHeli != null && (vagaHeli == transform || vagaHeli.IsChildOf(transform)))
            {
                heli.FixarEmVagaMovel(vagaHeli, transform);
            }
            else if (!heli.EstaAncoradoEmRaizMovel(transform))
            {
                heli.transform.SetParent(transform, true);
            }
        }
    }

    void EscanearAvioesNoAr()
    {
        _avioesProximosNoAr.Clear();
        _bufferScanAvioes.Clear();
        RegistroEntidadesJogo.FillAvioes(_bufferScanAvioes);
        
        if (_idCarrier == null) _idCarrier = GetComponent<IdentidadeUnidade>();
        int meuTime = (_idCarrier != null) ? _idCarrier.teamID : 1;

        foreach (var av in _bufferScanAvioes)
        {
            if (av == null || av.aeroportoOrigem == this) continue;
            
            // --- BLOQUEIA AVIÕES INIMIGOS E NEUTROS ---
            int aviaoTime = -1;
            IdentidadeUnidade idU = av.GetComponent<IdentidadeUnidade>();
            if (idU != null) aviaoTime = idU.teamID;
            else 
            {
                var idIA = av.GetComponent<IdentidadeIA>();
                if (idIA != null) aviaoTime = idIA.teamID;
            }
            
            if (aviaoTime != meuTime) continue; // Pula se for de time diferente ou se não tiver time definido (-1)
            
            if (av.estadoAtual == ControleAviao.EstadoAviao.EmMissao || av.estadoAtual == ControleAviao.EstadoAviao.Decolando)
            {
                float distSqr = (av.transform.position - transform.position).sqrMagnitude;
                if (distSqr < raioRadarResgate * raioRadarResgate) _avioesProximosNoAr.Add(av);
            }
        }
    }

    void EscanearHelicopterosNoAr()
    {
        _helicopterosProximosNoAr.Clear();
        _bufferScanHelicopteros.Clear();
        RegistroEntidadesJogo.FillHelicopteros(_bufferScanHelicopteros);
        _helicopterosVistosScan.Clear();

        if (_idCarrier == null) _idCarrier = GetComponent<IdentidadeUnidade>();
        int meuTime = (_idCarrier != null) ? _idCarrier.teamID : 1;

        foreach (var heli in _bufferScanHelicopteros)
        {
            if (heli == null) continue;
            int heliId = heli.GetInstanceID();
            if (!_helicopterosVistosScan.Add(heliId)) continue;
            if (HelicopteroPertenceAEstaBase(heli)) continue;
            if (!heli.estaVoando && !heli.EstaEmPreparacaoDecolagem()) continue;

            int heliTime = -1;
            IdentidadeUnidade idU = heli.GetComponent<IdentidadeUnidade>();
            if (idU == null) idU = heli.GetComponentInParent<IdentidadeUnidade>();
            if (idU != null) heliTime = idU.teamID;
            else
            {
                var idIA = heli.GetComponent<IdentidadeIA>();
                if (idIA == null) idIA = heli.GetComponentInParent<IdentidadeIA>();
                if (idIA != null) heliTime = idIA.teamID;
            }

            if (heliTime > 0 && heliTime != meuTime) continue;

            float distSqr = (heli.transform.position - transform.position).sqrMagnitude;
            if (distSqr < raioRadarResgate * raioRadarResgate)
            {
                _helicopterosProximosNoAr.Add(heli);
            }
        }

        _helicopterosProximosNoAr.Sort((a, b) =>
            (a.transform.position - transform.position).sqrMagnitude.CompareTo(
            (b.transform.position - transform.position).sqrMagnitude));
    }

    protected override void OnGUI()
    {
        if (!_menuCarrierAtivo) return;
        if (!GestorMenusExclusivos.EstaAtivo(this))
        {
            _menuCarrierAtivo = false;
            return;
        }

        int oldLabelFont = GUI.skin.label.fontSize;
        int oldButtonFont = GUI.skin.button.fontSize;
        int oldBoxFont = GUI.skin.box.fontSize;
        bool oldLabelRichText = GUI.skin.label.richText;
        bool oldBoxRichText = GUI.skin.box.richText;
        bool oldButtonRichText = GUI.skin.button.richText;
        GUI.skin.label.richText = true;
        GUI.skin.box.richText = true;
        GUI.skin.button.richText = true;
        GUI.skin.label.fontSize = 11;
        GUI.skin.button.fontSize = 10;
        GUI.skin.box.fontSize = 12;

        PrepararEstilosUISeNecessario();

        float menuWidth = Mathf.Clamp(Screen.width * 0.31f, 430f, 540f);
        float menuHeight = Mathf.Min(Screen.height - 20f, 960f);
        Rect areaMenu = new Rect(Screen.width - menuWidth - 16f, 10f, menuWidth, menuHeight);
        GestorMenusExclusivos.RegistrarAreaBloqueio(this, areaMenu);
        
        GUI.Box(areaMenu, "<b>⚓ COMANDO DE OPERAÇÕES NAVAIS</b>");

        GUILayout.BeginArea(new Rect(areaMenu.x + 10, areaMenu.y + 25, areaMenu.width - 20, areaMenu.height - 35));

        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>⚓ Status do Navio:</b> <color=cyan>{(_idCarrier != null ? _idCarrier.nomeDoPais : "USS-Carrier")}</color>", GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Fechar", GUILayout.Width(72f), GUILayout.Height(22f)))
        {
            _menuCarrierAtivo = false;
            GestorMenusExclusivos.Fechar(this);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        float alturaScrollMenu = Mathf.Max(280f, areaMenu.height - 70f);
        _scrollMenuCarrierGeral = GUILayout.BeginScrollView(_scrollMenuCarrierGeral, false, true, GUILayout.Height(alturaScrollMenu));

        GUILayout.BeginHorizontal("box");
        GUILayout.Label("<b>Lista:</b>", GUILayout.Width(52f));
        _uiMostrarListaCompleta = GUILayout.Toggle(_uiMostrarListaCompleta, "mostrar completa (pode pesar)");
        GUILayout.EndHorizontal();
        GUILayout.Space(2);

        if (_avioesProximosNoAr.Count > 0)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<color=cyan><b>📡 RADAR: AVIÕES NO CÉU (ALCANCE)</b></color>");

            int totalRadarAvioes = _avioesProximosNoAr.Count;
            int limiteRadarAvioes = ObterLimiteListaUI(totalRadarAvioes, 6);
            for (int i = 0; i < limiteRadarAvioes; i++)
            {
                var av = _avioesProximosNoAr[i];
                if (av == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"✈️ {CompactarTextoMenu(LimparClone(av.name), 22)}", _uiLabelCompacta, GUILayout.Width(180));
                
                if (GUILayout.Button("⬇️ Autorizar pouso", GUILayout.Width(140), GUILayout.Height(22)))
                {
                    av.DefinirBaseAlternativaEIniciarRetorno(this);
                    _avioesProximosNoAr.RemoveAt(i);
                    break;
                }
                GUILayout.EndHorizontal();
            }
            if (totalRadarAvioes > limiteRadarAvioes)
            {
                GUILayout.Label($"<color=grey>+{totalRadarAvioes - limiteRadarAvioes} avião(ões) no alcance.</color>", _uiLabelCompacta);
            }
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        if (_helicopterosProximosNoAr.Count > 0)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<color=cyan><b>📡 RADAR: HELICÓPTEROS ALIADOS</b></color>");

            int totalRadarHelis = _helicopterosProximosNoAr.Count;
            int limiteRadarHelis = ObterLimiteListaUI(totalRadarHelis, 6);
            for (int i = 0; i < limiteRadarHelis; i++)
            {
                var heli = _helicopterosProximosNoAr[i];
                if (heli == null) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"🚁 {CompactarTextoMenu(heli.ObterRotuloExibicao(), 24)}", _uiLabelCompacta, GUILayout.Width(180));

                GUI.enabled = !HelicopteroEstaEmRecebimentoCarrier(heli);
                if (GUILayout.Button("🛬 Chamar", GUILayout.Width(95), GUILayout.Height(22)))
                {
                    ReceberHelicopteroNoCarrier(heli);
                    break;
                }
                GUI.enabled = true;

                GUILayout.EndHorizontal();
            }
            if (totalRadarHelis > limiteRadarHelis)
            {
                GUILayout.Label($"<color=grey>+{totalRadarHelis - limiteRadarHelis} helicóptero(s) no alcance.</color>", _uiLabelCompacta);
            }
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

            GUILayout.Label("<color=lime><b>📦 CONVÉS (PRONTO PARA DECOLAR)</b></color>");
            
            // NOVO: COMPRA DE DRONE KAMIKAZE NO PORTA-AVIÕES
            if (prefabDroneKamikaze != null)
            {
                if (GUILayout.Button($"🧨 COMPRAR DRONE KAMIKAZE (${precoDroneKamikaze})", GUILayout.Height(26)))
                {
                    if (GerenciadorRecursos.Instancia != null && GerenciadorRecursos.Instancia.dinheiro >= precoDroneKamikaze)
                    {
                        GerenciadorRecursos.Instancia.dinheiro -= precoDroneKamikaze;
                        ComprarAviao(prefabDroneKamikaze);
                    }
                    else
                    {
                        Debug.LogWarning("[Porta-Aviões] Dinheiro insuficiente para Drone Kamikaze!");
                    }
                }
            }
            
            _scrollCarrier = GUILayout.BeginScrollView(_scrollCarrier, GUILayout.Height(100));

            int totalPatio = avioesNoPatio.Count;
            int limitePatio = _uiMostrarListaCompleta ? totalPatio : Mathf.Min(UI_MAX_ITENS_LISTA_RESUMIDA, totalPatio);
            for (int i = 0; i < limitePatio; i++)
            {
                var av = avioesNoPatio[i];
                if (av == null) continue;
                string pfx = (av == _selecionadoCarrier) ? "► " : "";
                if (GUILayout.Button($"{pfx}✈️ {CompactarTextoMenu(LimparClone(av.name), 30)}", _uiLinhaCompacta, GUILayout.Height(22)))
                {
                    _selecionadoCarrier = av;
                    helicopteroSelecionadoParaMissao = null;
                    _modoOrdemHelicopteroCarrier = ModoOrdemHelicopteroCarrier.Nenhum;
                    _rotaPatrulhaHelicopteroCarrier.Clear();
                }
            }
            if (!_uiMostrarListaCompleta && totalPatio > limitePatio)
            {
                GUILayout.Label($"<color=grey>+{totalPatio - limitePatio} aeronave(s) ocultas (ative lista completa).</color>", _uiLabelCompacta);
            }
            GUILayout.EndScrollView();

            if (helicopterosDoAeroporto.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("<color=orange><b>🚁 HELICÓPTEROS DO NAVIO</b></color>");
                _scrollHelisCarrier = GUILayout.BeginScrollView(_scrollHelisCarrier, GUILayout.Height(110));

                int totalHelis = helicopterosDoAeroporto.Count;
                int limiteHelis = _uiMostrarListaCompleta ? totalHelis : Mathf.Min(UI_MAX_ITENS_LISTA_RESUMIDA, totalHelis);
                for (int i = 0; i < limiteHelis; i++)
                {
                    Helicoptero heli = helicopterosDoAeroporto[i];
                    if (heli == null || !HelicopteroPertenceAEstaBase(heli)) continue;

                    string nomeHeli = CompactarTextoMenu(heli.ObterRotuloExibicao(), 24);
                    string statusHeli = HelicopteroEstaEmRecebimentoCarrier(heli)
                        ? "Descer h"
                        : heli.EstaEstacionadoNoAeroporto()
                        ? CompactarTextoMenu(heli.ObterVagaAeroporto() != null ? heli.ObterVagaAeroporto().name : "convés", 12)
                        : "em voo";
                    string prefixo = helicopteroSelecionadoParaMissao == heli ? "► " : string.Empty;

                    GUILayout.BeginHorizontal("box");
                    if (GUILayout.Button($"{prefixo}🚁 {nomeHeli}", _uiLinhaCompacta, GUILayout.Width(185), GUILayout.Height(22)))
                    {
                        helicopteroSelecionadoParaMissao = heli;
                        _selecionadoCarrier = null;
                    }
                    GUILayout.Label(statusHeli, _uiLabelCompacta, GUILayout.Width(95));

                    if (!heli.EstaEstacionadoNoAeroporto())
                    {
                        GUI.enabled = !HelicopteroEstaEmRecebimentoCarrier(heli);
                        if (GUILayout.Button(HelicopteroEstaEmRecebimentoCarrier(heli) ? "Aproximando" : "🛬 Chamar", GUILayout.Width(95), GUILayout.Height(22)))
                        {
                            ReceberHelicopteroNoCarrier(heli);
                            break;
                        }
                        GUI.enabled = true;
                    }
                    else
                    {
                        GUI.enabled = false;
                        GUILayout.Button("Convés", GUILayout.Width(95), GUILayout.Height(22));
                        GUI.enabled = true;
                    }

                    GUILayout.EndHorizontal();
                }

                if (!_uiMostrarListaCompleta && totalHelis > limiteHelis)
                {
                    GUILayout.Label($"<color=grey>+{totalHelis - limiteHelis} helicóptero(s) ocultos (ative lista completa).</color>", _uiLabelCompacta);
                }
                GUILayout.EndScrollView();
            }

            DesenharPainelHelicopteroCarrier();

            GUILayout.Space(10);
            GUILayout.Label("<color=orange><b>🛠️ HANGAR INTERNO (RESERVAS)</b></color>");
            
            bool vagaDisponivel = ObterPrimeiraVagaLivre() != null;
            
            int totalHangar = avioesNoHangar.Count;
            int limiteHangar = _uiMostrarListaCompleta ? totalHangar : Mathf.Min(UI_MAX_ITENS_LISTA_RESUMIDA, totalHangar);
            for (int i = 0; i < limiteHangar; i++)
            {
                var av = avioesNoHangar[i];
                if (av == null) continue;
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"🔒 {CompactarTextoMenu(LimparClone(av.name), 24)}", _uiLabelCompacta, GUILayout.Width(220));
                
                if (vagaDisponivel)
                {
                    GUI.enabled = !_elevadorOcupado;
                    if (GUILayout.Button("⬆️ Elevador", GUILayout.Width(95), GUILayout.Height(22))) StartCoroutine(RotinaElevadorSequencial(av, true));
                    GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button("Pátio lotado", GUILayout.Width(95), GUILayout.Height(22));
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            if (!_uiMostrarListaCompleta && totalHangar > limiteHangar)
            {
                GUILayout.Label($"<color=grey>+{totalHangar - limiteHangar} aeronave(s) ocultas (ative lista completa).</color>", _uiLabelCompacta);
            }

        GUILayout.Space(10);

        if (_selecionadoCarrier != null)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>AERONAVE:</b> <color=yellow>{LimparClone(_selecionadoCarrier.name)}</color>");
            
            SistemaDeDanos vidaAviao = _selecionadoCarrier.GetComponent<SistemaDeDanos>();
            if (vidaAviao != null)
            {
                float hpAtual = vidaAviao.vidaAtual;
                float hpMax = vidaAviao.vidaMaxima;
                string corHp = (hpAtual < (hpMax / 2f)) ? "red" : "lime";
                
                GUILayout.Label($"🛡️ <b>Integridade:</b> <color={corHp}>{hpAtual.ToString("F0")} / {hpMax.ToString("F0")}</color>");
                GUILayout.Label($"⛽ <b>Combustivel/Municao:</b> <color=lime>{ObterResumoCombustivelMunicao(_selecionadoCarrier)}</color>");

                bool noConvesOuHangar = avioesNoPatio.Contains(_selecionadoCarrier) || avioesNoHangar.Contains(_selecionadoCarrier);
                if (noConvesOuHangar)
                {
                    if (GUILayout.Button("🔧 REPARAR E REABASTECER", GUILayout.Height(24)))
                    {
                        vidaAviao.vidaAtual = vidaAviao.vidaMaxima;
                        ReabastecerAeronaveCarrier(_selecionadoCarrier, true);
                        LogDebug("[Porta-Aviões] Avião totalmente reparado!");
                    }
                }
            }

            GUILayout.Space(5);

            if (_selecionadoCarrier.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                if (_selecionadoCarrier.aguardandoCliqueRadar)
                {
                    string infoModo = _modoOrdemAviao == 0 ? "ALVO (ATAQUE/RECON)" : (_modoOrdemAviao == 1 ? "PATRULHA MULTIPONTO" : "SEGUIR (CLIQUE NUM ALIADO)");
                    string ajudaModo = _modoOrdemAviao == 1
                        ? $"<color=yellow>⚠️ MODO {infoModo} ATIVO! Clique direito adiciona pontos ({Mathf.Max(0, _rotaPatrulhaAviaoCarrier.Count - 1)}). ENTER encerra, BACKSPACE desfaz, ESC cancela.</color>"
                        : $"<color=yellow>⚠️ MODO {infoModo} ATIVO! Clique no mapa com o Botão Direito.</color>";
                    GUILayout.Label(ajudaModo, _uiLabelWrap);

                    if (GUILayout.Button("❌ CANCELAR ORDEM", GUILayout.Height(24))) 
                    {
                        CancelarModoAviaoCarrier();
                    }
                }
                else
                {
                    bool isKamikaze = _selecionadoCarrier.GetComponent<KamikazeDrone>() != null;
                    if (isKamikaze)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"<b>Qtd. P/ Ataque:</b> {qtdMassaDrone}");
                        if (GUILayout.Button("-", GUILayout.Width(32), GUILayout.Height(24))) qtdMassaDrone = Mathf.Max(1, qtdMassaDrone - 1);
                        if (GUILayout.Button("+", GUILayout.Width(32), GUILayout.Height(24))) qtdMassaDrone++;
                        if (GUILayout.Button("Todos", GUILayout.Width(56), GUILayout.Height(24))) 
                        {
                            int totais = 0;
                            foreach(var a in avioesNoPatio) if (a != null && a.GetComponent<KamikazeDrone>() != null) totais++;
                            foreach(var a in avioesNoHangar) if (a != null && a.GetComponent<KamikazeDrone>() != null) totais++;
                            qtdMassaDrone = totais;
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("🚀 ATAQUE EM MASSA", GUILayout.Height(28))) 
                        {
                            IniciarRadar(0);
                            esperandoCliqueMassa = true;
                        }
                        if (GUILayout.Button("💣 Ataque Solo", GUILayout.Height(28))) 
                        {
                            IniciarRadar(0);
                            esperandoCliqueMassa = false;
                        }
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("🛫 DECOLAR AO ATAQUE", GUILayout.Height(28))) IniciarRadar(0);
                        if (GUILayout.Button("📡 DECOLAR RECON.", GUILayout.Height(28))) IniciarRadar(0); // Opcionalmente, pode forçar radar passivo
                        GUILayout.EndHorizontal();
                        
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("🛡️ DECOLAR PATRULHA", GUILayout.Height(26))) IniciarRadar(1);
                        if (GUILayout.Button("👥 DECOLAR SEGUIR", GUILayout.Height(26))) IniciarRadar(2);
                        GUILayout.EndHorizontal();
                    }
                }
            }
            else if (_selecionadoCarrier.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
            {
                if (GUILayout.Button("🔙 ABORTAR E POUSAR", GUILayout.Height(28))) 
                { 
                    _selecionadoCarrier.ComandoRetornarBase(); 
                    _selecionadoCarrier = null; 
                }
            }
            
            if (avioesNoPatio.Contains(_selecionadoCarrier))
            {
                if (!_elevadorOcupado)
                {
                    if (GUILayout.Button("⬇️ DESCER PARA HANGAR", GUILayout.Height(24))) 
                    {
                        StartCoroutine(RotinaElevadorSequencial(_selecionadoCarrier, false));
                        _selecionadoCarrier = null;
                    }
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button("ELEVADOR EM USO", GUILayout.Height(24));
                    GUI.enabled = true;
                }
            }
            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();

        GUILayout.Space(6);
        if (GUILayout.Button("Fechar (O)", GUILayout.Height(24)))
        {
            _menuCarrierAtivo = false;
            GestorMenusExclusivos.Fechar(this);
        }
        GUILayout.EndArea();

        GUI.skin.label.fontSize = oldLabelFont;
        GUI.skin.button.fontSize = oldButtonFont;
        GUI.skin.box.fontSize = oldBoxFont;
        GUI.skin.label.richText = oldLabelRichText;
        GUI.skin.box.richText = oldBoxRichText;
        GUI.skin.button.richText = oldButtonRichText;
    }

    private int ObterLimiteListaUI(int total, int limiteDesempenho)
    {
        if (_uiMostrarListaCompleta)
        {
            return total;
        }

        int limite = modoDesempenhoUI ? limiteDesempenho : UI_MAX_ITENS_LISTA_RESUMIDA;
        return Mathf.Min(Mathf.Max(1, limite), total);
    }

    private static string LimparClone(string nome)
    {
        return string.IsNullOrEmpty(nome) ? string.Empty : nome.Replace("(Clone)", string.Empty).Trim();
    }

    private static string ObterResumoCombustivelMunicao(ControleAviao aviao)
    {
        if (aviao == null)
        {
            return "-";
        }

        CombustivelUnidade combustivel = aviao.GetComponent<CombustivelUnidade>();
        LancadorMisselCaca misseis = aviao.GetComponent<LancadorMisselCaca>();

        string fuel = combustivel != null && combustivel.usaCombustivel
            ? $"Fuel {Mathf.RoundToInt(combustivel.Percentual * 100f)}%"
            : "Fuel -";
        string ammo = misseis != null
            ? $"MSL {misseis.municaoAtual}/{misseis.municaoMaxima}"
            : "MSL -";

        return fuel + " | " + ammo;
    }

    public static void ReabastecerAeronaveCarrier(ControleAviao aviao, bool forcarCompleto)
    {
        if (aviao == null)
        {
            return;
        }

        CombustivelUnidade combustivel = aviao.GetComponent<CombustivelUnidade>();
        if (combustivel != null && combustivel.usaCombustivel && (forcarCompleto || combustivel.Percentual <= 0.50f))
        {
            combustivel.PreencherSemCusto();
        }

        LancadorMisselCaca misseis = aviao.GetComponent<LancadorMisselCaca>();
        if (misseis != null)
        {
            bool estavaComPoucaMunicao = misseis.municaoAtual < misseis.municaoMaxima;
            misseis.RecarregarCompletoNaBase();
            if (estavaComPoucaMunicao && combustivel != null && combustivel.Percentual <= 0.50f)
            {
                combustivel.PreencherSemCusto();
            }
        }
    }

    private void ProcessarCliqueOrdemRadar()
    {
        if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;
        
        if (_cameraPrincipal == null) _cameraPrincipal = Camera.main;
        if (_cameraPrincipal == null) return;
        Ray r = _cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        Vector3 pontoAlvo = Vector3.zero;
        
        if (Physics.Raycast(r, out RaycastHit hit))
        {
            pontoAlvo = hit.point;
            
            // Lógica Especial SEGUIR
            if (_modoOrdemAviao == 2)
            {
                ControleUnidade alvoSeguir = hit.collider.GetComponentInParent<ControleUnidade>();
                if (alvoSeguir != null)
                {
                    ControleUnidade controleSelecionado = _selecionadoCarrier.GetComponent<ControleUnidade>();
                    if (controleSelecionado == null || !controleSelecionado.EmitirOrdemSeguir(alvoSeguir.transform))
                    {
                        Debug.LogWarning("Modo SEGUIR: a aeronave selecionada nao possui ControleUnidade pronto para seguir.");
                        return;
                    }

                    LogDebug("🎯 Avião designado para Escolta/Seguir!");
                }
                else
                {
                    Debug.LogWarning("Modo SEGUIR: Você não clicou em uma unidade.");
                    return; // Cancela e continua esperando
                }
            }
        }
        else
        {
            if (_modoOrdemAviao == 2) return; // Precisa clicar num objeto pra seguir
            
            UnityEngine.Plane marPlano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
            float dist;
            if (marPlano.Raycast(r, out dist)) pontoAlvo = r.GetPoint(dist);
            else return;
        }

        // Lógica Especial PATRULHA
        if (_modoOrdemAviao == 1)
        {
            Vector3 pontoPatrulha = pontoAlvo;
            pontoPatrulha.y = 80f;

            if (_rotaPatrulhaAviaoCarrier.Count == 0)
            {
                Vector3 pontoInicial = _selecionadoCarrier.transform.position;
                pontoInicial.y = 80f;
                _rotaPatrulhaAviaoCarrier.Add(pontoInicial);
            }

            _rotaPatrulhaAviaoCarrier.Add(pontoPatrulha);

            List<Vector3> pts = new List<Vector3>(_rotaPatrulhaAviaoCarrier);
            ControleUnidade controleSelecionado = _selecionadoCarrier.GetComponent<ControleUnidade>();
            if (controleSelecionado == null || !controleSelecionado.EmitirOrdemPatrulha(pts))
            {
                Debug.LogWarning("Modo PATRULHA: a aeronave selecionada nao possui ControleUnidade pronto para patrulhar.");
                return;
            }
            
            DesenharLinhasOrdem linhas = FindFirstObjectByType<DesenharLinhasOrdem>();
            if (linhas != null)
            {
                linhas.lineRenderer.positionCount = pts.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    linhas.lineRenderer.SetPosition(i, pts[i]);
                }
            }
            AtualizarLinhaPatrulhaAviaoCarrier(pts);
            
            CriarSinalizador(pontoPatrulha, _selecionadoCarrier);
            LogDebug("🛡️ Avião designado para rota de Patrulha multiponto.");
            return;
        }

        _selecionadoCarrier.aguardandoCliqueRadar = false;
        
        // Dispara o avião pro ar
        if (pontoAlvo != Vector3.zero)
        {
            CriarSinalizador(pontoAlvo, _selecionadoCarrier);

            if (esperandoCliqueMassa)
            {
                esperandoCliqueMassa = false;
                StartCoroutine(RotinaLancarMissaoEmMassa(pontoAlvo, qtdMassaDrone));
            }
            else
            {
                _selecionadoCarrier.IniciarMissaoCompleta(pontoAlvo);
            }
        }
        
        _selecionadoCarrier = null;
        _menuCarrierAtivo = false;
        esperandoCliqueMassa = false;
    }

    private void ProcessarOrdemAviaoCarrier()
    {
        if (_selecionadoCarrier == null || !_selecionadoCarrier.aguardandoCliqueRadar)
        {
            _rotaPatrulhaAviaoCarrier.Clear();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelarModoAviaoCarrier();
            return;
        }

        if (_modoOrdemAviao == 1)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                EncerrarModoAviaoCarrier();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace) && _rotaPatrulhaAviaoCarrier.Count > 0)
            {
                _rotaPatrulhaAviaoCarrier.RemoveAt(_rotaPatrulhaAviaoCarrier.Count - 1);
                if (_rotaPatrulhaAviaoCarrier.Count <= 1)
                {
                    ControleUnidade controleSelecionado = _selecionadoCarrier.GetComponent<ControleUnidade>();
                    if (controleSelecionado != null)
                    {
                        controleSelecionado.EmitirOrdemParar();
                    }
                    LimparLinhaPatrulhaAviaoCarrier();
                }
                else
                {
                    ControleUnidade controleSelecionado = _selecionadoCarrier.GetComponent<ControleUnidade>();
                    if (controleSelecionado != null)
                    {
                        controleSelecionado.EmitirOrdemPatrulha(new List<Vector3>(_rotaPatrulhaAviaoCarrier));
                    }
                    AtualizarLinhaPatrulhaAviaoCarrier(_rotaPatrulhaAviaoCarrier);
                }
                return;
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            ProcessarCliqueOrdemRadar();
        }
    }

    private void CancelarModoAviaoCarrier()
    {
        if (_selecionadoCarrier != null)
        {
            _selecionadoCarrier.aguardandoCliqueRadar = false;
        }

        _modoOrdemAviao = 0;
        _rotaPatrulhaAviaoCarrier.Clear();
        LimparLinhaPatrulhaAviaoCarrier();
        _selecionadoCarrier = null;
        esperandoCliqueMassa = false;
        _menuCarrierAtivo = false;
        AtualizarModoInteracaoManualAeroporto();
    }

    private void EncerrarModoAviaoCarrier()
    {
        if (_selecionadoCarrier != null)
        {
            _selecionadoCarrier.aguardandoCliqueRadar = false;
        }

        _modoOrdemAviao = 0;
        _rotaPatrulhaAviaoCarrier.Clear();
        _selecionadoCarrier = null;
        esperandoCliqueMassa = false;
        _menuCarrierAtivo = false;
        AtualizarModoInteracaoManualAeroporto();
    }

    private void AtualizarLinhaPatrulhaAviaoCarrier(IList<Vector3> pontos)
    {
        if (pontos == null || pontos.Count <= 1)
        {
            LimparLinhaPatrulhaAviaoCarrier();
            return;
        }

        if (_linhaPatrulhaAviaoCarrier == null)
        {
            GameObject obj = new GameObject("Carrier_Air_Patrol_Route");
            obj.transform.SetParent(transform, false);
            _linhaPatrulhaAviaoCarrier = obj.AddComponent<LineRenderer>();
            _linhaPatrulhaAviaoCarrier.material = new Material(Shader.Find("Sprites/Default"));
            _linhaPatrulhaAviaoCarrier.startWidth = 1.25f;
            _linhaPatrulhaAviaoCarrier.endWidth = 1.25f;
            _linhaPatrulhaAviaoCarrier.startColor = new Color(0.1f, 0.95f, 1f, 0.85f);
            _linhaPatrulhaAviaoCarrier.endColor = new Color(0.1f, 0.95f, 1f, 0.85f);
            _linhaPatrulhaAviaoCarrier.useWorldSpace = true;
        }

        _linhaPatrulhaAviaoCarrier.positionCount = pontos.Count;
        for (int i = 0; i < pontos.Count; i++)
        {
            Vector3 ponto = pontos[i];
            ponto.y = Mathf.Max(12f, ponto.y);
            _linhaPatrulhaAviaoCarrier.SetPosition(i, ponto);
        }
    }

    private void LimparLinhaPatrulhaAviaoCarrier()
    {
        if (_linhaPatrulhaAviaoCarrier != null)
        {
            _linhaPatrulhaAviaoCarrier.positionCount = 0;
        }
    }

    private bool TryResolverPontoMapaCarrier(out Vector3 pontoAlvo)
    {
        pontoAlvo = Vector3.zero;
        if (_cameraPrincipal == null) _cameraPrincipal = Camera.main;
        if (_cameraPrincipal == null) return false;

        Ray raio = _cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(raio, out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            pontoAlvo = hit.point;
            return true;
        }

        UnityEngine.Plane plano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
        if (plano.Raycast(raio, out float distancia))
        {
            pontoAlvo = raio.GetPoint(distancia);
            return true;
        }

        return false;
    }

    private void IniciarModoHelicopteroCarrier(Helicoptero heli, ModoOrdemHelicopteroCarrier modo)
    {
        if (heli == null) return;

        helicopteroSelecionadoParaMissao = heli;
        _selecionadoCarrier = null;
        _modoOrdemHelicopteroCarrier = modo;
        _rotaPatrulhaHelicopteroCarrier.Clear();
        _menuCarrierAtivo = false;
        AtualizarModoInteracaoManualAeroporto();
    }

    private void CancelarModoHelicopteroCarrier()
    {
        _rotaPatrulhaHelicopteroCarrier.Clear();
        _modoOrdemHelicopteroCarrier = ModoOrdemHelicopteroCarrier.Nenhum;
        AtualizarModoInteracaoManualAeroporto();
    }

    private void EncerrarModoHelicopteroCarrier()
    {
        _rotaPatrulhaHelicopteroCarrier.Clear();
        _modoOrdemHelicopteroCarrier = ModoOrdemHelicopteroCarrier.Nenhum;
        helicopteroSelecionadoParaMissao = null;
        AtualizarModoInteracaoManualAeroporto();
    }

    private void ProcessarOrdemHelicopteroCarrier()
    {
        if (helicopteroSelecionadoParaMissao == null)
        {
            CancelarModoHelicopteroCarrier();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EncerrarModoHelicopteroCarrier();
            return;
        }

        if (_modoOrdemHelicopteroCarrier == ModoOrdemHelicopteroCarrier.Patrulha)
        {
            if (Input.GetMouseButtonDown(0))
            {
                CancelarModoHelicopteroCarrier();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                EncerrarModoHelicopteroCarrier();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace) && _rotaPatrulhaHelicopteroCarrier.Count > 0)
            {
                _rotaPatrulhaHelicopteroCarrier.RemoveAt(_rotaPatrulhaHelicopteroCarrier.Count - 1);
                if (_rotaPatrulhaHelicopteroCarrier.Count > 0)
                {
                    helicopteroSelecionadoParaMissao.IniciarPatrulhaAeroporto(_rotaPatrulhaHelicopteroCarrier);
                }
                else
                {
                    helicopteroSelecionadoParaMissao.CancelarMissaoAeroporto();
                }
                return;
            }
        }

        if (!Input.GetMouseButtonDown(1)) return;
        if (GestorMenusExclusivos.CliqueBloqueadoPelaUI()) return;
        if (!TryResolverPontoMapaCarrier(out Vector3 pontoAlvo)) return;

        if (_modoOrdemHelicopteroCarrier == ModoOrdemHelicopteroCarrier.Patrulha)
        {
            _rotaPatrulhaHelicopteroCarrier.Add(pontoAlvo);
            helicopteroSelecionadoParaMissao.IniciarPatrulhaAeroporto(_rotaPatrulhaHelicopteroCarrier);
            CriarSinalizador(pontoAlvo, helicopteroSelecionadoParaMissao);
            return;
        }

        if (_modoOrdemHelicopteroCarrier == ModoOrdemHelicopteroCarrier.Reconhecimento)
        {
            helicopteroSelecionadoParaMissao.IniciarReconhecimentoAeroporto(pontoAlvo);
        }
        else if (_modoOrdemHelicopteroCarrier == ModoOrdemHelicopteroCarrier.AtaqueLocal)
        {
            helicopteroSelecionadoParaMissao.IniciarAtaqueLocalAeroporto(pontoAlvo);
        }

        CriarSinalizador(pontoAlvo, helicopteroSelecionadoParaMissao);
        EncerrarModoHelicopteroCarrier();
    }

    private void DesenharPainelHelicopteroCarrier()
    {
        if (helicopteroSelecionadoParaMissao == null) return;
        if (!helicopterosDoAeroporto.Contains(helicopteroSelecionadoParaMissao)) return;
        if (!HelicopteroPertenceAEstaBase(helicopteroSelecionadoParaMissao)) return;

        GUILayout.Space(8);
        GUILayout.BeginVertical("box");
        string nomeHeli = CompactarTextoMenu(helicopteroSelecionadoParaMissao.ObterRotuloExibicao(), 28);
        GUILayout.Label($"<b>ORDENS DO HELICÓPTERO: 🚁 {nomeHeli}</b>");
        GUILayout.Label($"<color=cyan>{helicopteroSelecionadoParaMissao.ObterEstadoOperacionalAeroporto()}</color>");

        if (_modoOrdemHelicopteroCarrier != ModoOrdemHelicopteroCarrier.Nenhum)
        {
            if (_modoOrdemHelicopteroCarrier == ModoOrdemHelicopteroCarrier.Patrulha)
            {
                GUILayout.Label($"<color=yellow>PATRULHA ATIVA: clique direito adiciona pontos ({_rotaPatrulhaHelicopteroCarrier.Count}). ENTER encerra, BACKSPACE desfaz, ESC cancela.</color>");
            }
            else
            {
                string textoModo = _modoOrdemHelicopteroCarrier == ModoOrdemHelicopteroCarrier.Reconhecimento ? "RECONHECIMENTO" : "ATAQUE LOCAL";
                GUILayout.Label($"<color=yellow>{textoModo} ATIVO: clique direito no mapa. ESC cancela.</color>");
            }

            if (GUILayout.Button("❌ Cancelar Ordem Helicóptero", GUILayout.Height(24)))
            {
                EncerrarModoHelicopteroCarrier();
            }
        }
        else
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("👁️ Reconhecimento", GUILayout.Height(28)))
            {
                IniciarModoHelicopteroCarrier(helicopteroSelecionadoParaMissao, ModoOrdemHelicopteroCarrier.Reconhecimento);
            }
            if (GUILayout.Button("🛡️ Patrulha", GUILayout.Height(28)))
            {
                IniciarModoHelicopteroCarrier(helicopteroSelecionadoParaMissao, ModoOrdemHelicopteroCarrier.Patrulha);
            }
            if (GUILayout.Button("💥 Ataque local", GUILayout.Height(28)))
            {
                IniciarModoHelicopteroCarrier(helicopteroSelecionadoParaMissao, ModoOrdemHelicopteroCarrier.AtaqueLocal);
            }
            GUILayout.EndHorizontal();

            if (!helicopteroSelecionadoParaMissao.EstaEstacionadoNoAeroporto())
            {
                if (GUILayout.Button("🔙 Retornar para vaga", GUILayout.Height(24)))
                {
                    ReceberHelicopteroNoCarrier(helicopteroSelecionadoParaMissao);
                }
            }
        }

        GUILayout.EndVertical();
    }

    public override bool PossuiOrdemManualAtiva()
    {
        if (base.PossuiOrdemManualAtiva())
        {
            return true;
        }

        bool aviaoAguardandoClique = _selecionadoCarrier != null && _selecionadoCarrier.aguardandoCliqueRadar;
        bool helicopteroAguardandoClique = _modoOrdemHelicopteroCarrier != ModoOrdemHelicopteroCarrier.Nenhum && helicopteroSelecionadoParaMissao != null;
        return aviaoAguardandoClique || helicopteroAguardandoClique;
    }

    protected override InteractionOwner ObterDonoInteracaoManual()
    {
        return InteractionOwner.CarrierOrder;
    }

    void IniciarRadar(int modo) 
    { 
        _modoOrdemAviao = modo;
        _rotaPatrulhaAviaoCarrier.Clear();
        _selecionadoCarrier.aguardandoCliqueRadar = true; 
        _menuCarrierAtivo = false; 
        AtualizarModoInteracaoManualAeroporto();
    }

    private Transform ObterVagaHelicopteroCarrier(Helicoptero heli = null)
    {
        if (heli != null)
        {
            Transform vagaAtual = heli.ObterVagaAeroporto();
            if (heli.EstaEstacionadoNoAeroporto() && vagaAtual != null && (vagaAtual == transform || vagaAtual.IsChildOf(transform)))
            {
                return vagaAtual;
            }
        }

        Transform vagaLivre = ObterPrimeiraVagaLivre();
        if (vagaLivre != null)
        {
            return vagaLivre;
        }

        return null;
    }

    private bool ReceberHelicopteroNoCarrier(Helicoptero heli)
    {
        if (heli == null)
        {
            return false;
        }

        Transform vaga = ObterVagaHelicopteroCarrier(heli);
        if (vaga == null)
        {
            return false;
        }

        int heliId = heli.GetInstanceID();
        if (_rotinasRecebimentoHeliCarrier.TryGetValue(heliId, out Coroutine rotinaAtual) && rotinaAtual != null)
        {
            StopCoroutine(rotinaAtual);
        }

        heli.VincularAoAeroporto(this, vaga);
        heli.CancelarMissaoAeroporto();
        Coroutine novaRotina = StartCoroutine(RotinaReceberHelicopteroNoCarrier(heli, vaga, heliId));
        _rotinasRecebimentoHeliCarrier[heliId] = novaRotina;

        if (!helicopterosDoAeroporto.Contains(heli))
        {
            helicopterosDoAeroporto.Add(heli);
        }

        helicopteroSelecionadoParaMissao = heli;
        _selecionadoCarrier = null;

        return true;
    }

    private IEnumerator RotinaReceberHelicopteroNoCarrier(Helicoptero heli, Transform vaga, int heliId)
    {
        if (heli == null || vaga == null)
        {
            _rotinasRecebimentoHeliCarrier.Remove(heliId);
            yield break;
        }

        heli.transform.SetParent(null, true);

        if (pontoAlinhamentoHelicoptero != null)
        {
            heli.Decolar(ObterDestinoAlinhamentoHelicoptero());
            yield return EsperarHelicopteroChegarAoPontoMovel(heli, pontoAlinhamentoHelicoptero, 20f, 5f);
        }

        if (heli != null && vaga != null)
        {
            heli.IniciarPousoEmVagaMovel(vaga);
        }

        float timeout = 45f;
        float tempo = 0f;
        while (tempo < timeout && heli != null && vaga != null)
        {
            tempo += Time.deltaTime;
            heli.AtualizarPousoEmVagaMovel(vaga);
            if (!heli.estaVoando && !heli.EstaEmPreparacaoDecolagem())
            {
                break;
            }

            yield return null;
        }

        if (heli != null && vaga != null && !heli.estaVoando)
        {
            heli.FixarEmVagaMovel(vaga, transform);
        }

        _rotinasRecebimentoHeliCarrier.Remove(heliId);
    }

    public void AcionarElevadorParaCima(ControleAviao av)
    {
        if (!_elevadorOcupado) StartCoroutine(RotinaElevadorSequencial(av, true));
    }

    public void MandarParaOHangar(ControleAviao av)
    {
        if (!_elevadorOcupado) StartCoroutine(RotinaElevadorSequencial(av, false));
    }

    IEnumerator RotinaElevadorSequencial(ControleAviao av, bool subir)
    {
        _elevadorOcupado = true;
        
        if (subir)
        {
            Transform v = ObterPrimeiraVagaLivre();
            if (v != null) 
            {
                ColocarAviaoInstantaneamenteNoPatio(av, v, true);
            }
            else
            {
                if (!avioesNoHangar.Contains(av)) avioesNoHangar.Add(av);
            }
        }
        else
        {
            GuardarAviaoNoHangarInstantaneo(av, true);

            if (av != null)
            {
                av.transform.SetParent(this.transform, false);
                av.transform.localPosition = localHangarInterno;
                av.transform.localRotation = Quaternion.identity;
            }
        }

        if (plataformaElevador != null)
        {
            plataformaElevador.localPosition = localConvesEntrada;
        }

        _elevadorOcupado = false;
        yield break;
    }

    IEnumerator MoverSuave(Transform o, Vector3 a, Vector3 b, float s, bool local = true)
    {
        float t = 0;
        while (t < 1f) 
        { 
            t += Time.deltaTime / s; 
            if (local) o.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t)); 
            else o.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t));
            yield return null; 
        }
    }

    private void PrepararEstilosUISeNecessario()
    {
        if (_skinCache == GUI.skin && _uiLinhaCompacta != null && _uiLabelCompacta != null && _uiLabelWrap != null)
        {
            return;
        }

        _skinCache = GUI.skin;

        _uiLinhaCompacta = new GUIStyle(GUI.skin.button);
        _uiLinhaCompacta.alignment = TextAnchor.MiddleLeft;
        _uiLinhaCompacta.wordWrap = false;
        _uiLinhaCompacta.clipping = TextClipping.Clip;
        _uiLinhaCompacta.fontSize = 10;
        _uiLinhaCompacta.padding = new RectOffset(6, 4, 2, 2);

        _uiLabelCompacta = new GUIStyle(GUI.skin.label);
        _uiLabelCompacta.richText = true;
        _uiLabelCompacta.wordWrap = false;
        _uiLabelCompacta.clipping = TextClipping.Clip;
        _uiLabelCompacta.fontSize = 10;

        _uiLabelWrap = new GUIStyle(GUI.skin.label);
        _uiLabelWrap.richText = true;
        _uiLabelWrap.wordWrap = true;
        _uiLabelWrap.fontSize = 11;
    }

    public override void GuardarNoHangarAutomatico(ControleAviao av)
    {
        if (av == null)
        {
            return;
        }

        av.aeroportoOrigem = this;

        Transform vagaVisivel = null;
        if (av.vagaRetorno != null && (av.vagaRetorno == transform || av.vagaRetorno.IsChildOf(transform)))
        {
            vagaVisivel = av.vagaRetorno;
        }
        else
        {
            vagaVisivel = ObterPrimeiraVagaLivre();
        }

        if (vagaVisivel != null)
        {
            ColocarAviaoInstantaneamenteNoPatio(av, vagaVisivel, true);
            av.transform.SetParent(this.transform, true);
            return;
        }

        avioesNoHangar.Remove(av);
        if (!avioesNoPatio.Contains(av))
        {
            avioesNoPatio.Add(av);
        }

        av.gameObject.SetActive(true);
        av.transform.SetParent(this.transform, true);
        av.aguardandoCliqueRadar = false;
        av.ordemParaRetorno = false;
        av.estaEmModoVooFisico = false;
        av.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
    }

    public override void RegistrarHelicopteroControlado(Helicoptero helicoptero)
    {
        base.RegistrarHelicopteroControlado(helicoptero);
        if (helicoptero == null || helicoptero.estaVoando || helicoptero.EstaEmPreparacaoDecolagem())
        {
            return;
        }

        if (!HelicopteroPertenceAEstaBase(helicoptero))
        {
            return;
        }

        Transform vaga = helicoptero.ObterVagaAeroporto();
        if (vaga != null)
        {
            helicoptero.FixarEmVagaMovel(vaga, transform);
        }
    }

    // ======================================================
    // MÉTODOS DE FILA DE DECOLAGEM E ANIMAÇÃO DA RAMPA
    // ======================================================
    public bool IsPreparaBusy(ControleAviao solicitante)
    {
        return _aviaoNoPrepara != null && _aviaoNoPrepara != solicitante;
    }

    public void ReservePrepara(ControleAviao solicitante)
    {
        _aviaoNoPrepara = solicitante;
    }

    public void ReleasePrepara(ControleAviao solicitante)
    {
        if (_aviaoNoPrepara == solicitante)
        {
            _aviaoNoPrepara = null;
        }
    }

    public void SubirRampa()
    {
        if (rampaDecolagem == null || EhCenaDeMenuAtiva()) return;
        if (_rotinaRampa != null) StopCoroutine(_rotinaRampa);
        
        Vector3 targetPos = _posicaoDefaultRampa;
        targetPos.y = -4f;
        
        Vector3 rot = _rotacaoDefaultRampa.eulerAngles;
        rot.x = 83f;
        Quaternion targetRot = Quaternion.Euler(rot);
        
        _rotinaRampa = StartCoroutine(RotinaAnimarRampa(targetPos, targetRot, 1.5f));
    }

    public void DescerRampa()
    {
        if (rampaDecolagem == null || EhCenaDeMenuAtiva()) return;
        if (_rotinaRampa != null) StopCoroutine(_rotinaRampa);
        
        Vector3 targetPos = _posicaoDefaultRampa;
        targetPos.y = -4.4f;
        
        Vector3 rot = _rotacaoDefaultRampa.eulerAngles;
        rot.x = 20f;
        Quaternion targetRot = Quaternion.Euler(rot);
        
        _rotinaRampa = StartCoroutine(RotinaAnimarRampa(targetPos, targetRot, 1.5f));
    }

    private IEnumerator RotinaAnimarRampa(Vector3 targetPos, Quaternion targetRot, float duracao)
    {
        Vector3 startPos = rampaDecolagem.localPosition;
        Quaternion startRot = rampaDecolagem.localRotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duracao;
            if (rampaDecolagem != null)
            {
                rampaDecolagem.localPosition = Vector3.Lerp(startPos, targetPos, t);
                rampaDecolagem.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            }
            yield return null;
        }
        if (rampaDecolagem != null)
        {
            rampaDecolagem.localPosition = targetPos;
            rampaDecolagem.localRotation = targetRot;
        }
    }
    private bool EhCenaDeMenuAtiva()
    {
        string nomeCena = SceneManager.GetActiveScene().name;
        return nomeCena == ConfiguracaoCenasJogo.CenaMenuPrincipalCanonica
            || nomeCena == ConfiguracaoCenasJogo.CenaMenuFallback;
    }
}
