using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

    [Header("=== PONTOS DE TAXI EXCLUSIVOS ===")]
    public Transform waypointSaidaPista;
    public Transform waypointEntradaElevador;
    public Transform grupoParadas; // NOVO: Grupo com os pontos "parada" do convés
    
    private bool _menuCarrierAtivo = false;
    private bool _elevadorOcupado = false;
    private ControleAviao _selecionadoCarrier;
    private int _modoOrdemAviao = 0; // 0=Ataque/Recon, 1=Patrulha, 2=Seguir
    private Vector2 _scrollCarrier;
    private IdentidadeUnidade _idCarrier;
    private ControleUnidade _controleUnidade;

    [Header("=== RADAR DE CONTROLE AÉREO ===")]
    public float raioRadarResgate = 1500f; // Aumentei um pouco o alcance para facilitar
    private List<ControleAviao> _avioesProximosNoAr = new List<ControleAviao>();
    private float _tempoProximoScan = 0f;

    protected override void Awake()
    {
        base.Awake(); // Inicializa lógica base do Aeroporto
        
        _idCarrier = GetComponent<IdentidadeUnidade>();
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
        }

        // 3. Mapeia Pouso Revertido (Maior para o Menor: 4 -> 3 -> 2 -> 1 -> rampa)
        Transform pistaParaUsar = (grupoPistaLanding != null) ? grupoPistaLanding : decida;
        if (pistaParaUsar != null)
        {
            List<Transform> listaTemp = new List<Transform>();
            foreach (Transform t in pistaParaUsar) listaTemp.Add(t);
            
            // Ordenação Inteligente: 
            // 1. Pega os que tem números e ordena decrescente (4, 3, 2, 1)
            // 2. Coloca os sem número por último (aceleracao, rampa)
            waypointsDecida = listaTemp.OrderByDescending(t => {
                string n = t.name;
                if (n.Contains("(") && n.Contains(")")) {
                    string numStr = n.Substring(n.IndexOf('(') + 1, n.IndexOf(')') - n.IndexOf('(') - 1);
                    if (int.TryParse(numStr, out int val)) return val + 100; // Prioridade para numerados
                }
                if (n.ToLower().Contains("aceleracao")) return 50;
                return 0; // rampa e outros
            }).ToList();
            
            
            Debug.Log($"[Porta-Aviões] Sequência de Pouso: {string.Join(" -> ", waypointsDecida.Select(w => w.name))}");

            // --- NOVO: PONTO DE APROXIMAÇÃO REALISTA ---
            // Cria um ponto 600m atrás do navio para o avião alinhar antes de pousar
            if (waypointsDecida.Count > 0)
            {
                GameObject approach = new GameObject("Ponto_Aproximacao_Navio");
                approach.transform.SetParent(this.transform);
                
                // Calcula direção da pista baseada nos dois primeiros pontos ou no forward do navio
                Vector3 direcaoPista = (waypointsDecida.Count > 1) ? 
                    (waypointsDecida[0].position - waypointsDecida[1].position).normalized : transform.forward;
                
                // Coloca o ponto de entrada 600 metros atrás
                approach.transform.position = waypointsDecida[0].position + direcaoPista * 600f + Vector3.up * 50f;
                waypointsDecida.Insert(0, approach.transform);
            }
        }
        
        // 4. Fallbacks de Taxiamento
        if (wpAndadar == null) wpAndadar = (waypointSaidaPista != null) ? waypointSaidaPista : transform;
        // Removido wpAnalise automático para não forçar parada no elevador antes do pátio
        if (wpAnalise == null && waypointEntradaElevador != null) wpAnalise = null; 
    }

    new void Update()
    {
        // 1. Rotação da Antena
        if (antenaRotativa != null)
            antenaRotativa.Rotate(Vector3.up * velGiroAntena * Time.deltaTime);

        // ==========================================
        // 2. SISTEMA DA TECLA 'O' COM TRAVA DE SEGURANÇA (CORRIGIDO)
        // ==========================================
        if (Input.GetKeyDown(KeyCode.O))
        {
            // SÓ ABRE SE O NAVIO ESTIVER SELECIONADO PELO JOGADOR
            if (_controleUnidade != null && !_controleUnidade.selecionado)
            {
                return; // Ignora se não estiver selecionado
            }

            Debug.Log("[Porta-Aviões] Você apertou a tecla O no navio selecionado!");
            
            // Tenta achar a Identidade caso tenha falhado no Awake
            if (_idCarrier == null) _idCarrier = GetComponent<IdentidadeUnidade>();

            if (_idCarrier != null)
            {
                if (_idCarrier.teamID == 1) 
                {
                    _menuCarrierAtivo = !_menuCarrierAtivo; // Abre/Fecha normal
                }
                else
                {
                    Debug.LogWarning("[Porta-Aviões] ATENÇÃO: O Team ID do navio não é 1! O valor atual é: " + _idCarrier.teamID + ". Estou forçando o menu a abrir para você testar.");
                    _menuCarrierAtivo = !_menuCarrierAtivo; // Força abrir
                }
            }
            else
            {
                Debug.LogWarning("[Porta-Aviões] ERRO: Falta o script 'IdentidadeUnidade' no Porta-Aviões! Forçando o menu a abrir.");
                _menuCarrierAtivo = !_menuCarrierAtivo; // Força abrir
            }
        }

        // ==========================================
        // 3. SISTEMA DE CLIQUE COM "RAIO LASER" MOUSE ESQUERDO
        // ==========================================
        if (Input.GetMouseButtonDown(0)) 
        {
            if (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Ray raioCamera = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(raioCamera, out RaycastHit hit))
                {
                    if (hit.transform == this.transform || hit.transform.IsChildOf(this.transform))
                    {
                        if (!_identidadeVerificada) { _identidadeCacheada = GetComponent<IdentidadeUnidade>(); _identidadeVerificada = true; }
                        if (_identidadeCacheada != null && _identidadeCacheada.teamID != 1 && _identidadeCacheada.teamID != 0) 
                        {
                            Debug.LogWarning("[Porta-Aviões] Navio inimigo. Acesso Negado!");
                            return; 
                        }

                        Debug.Log("[Porta-Aviões] Você clicou no navio!");
                        _menuCarrierAtivo = true; // Abre o menu ao clicar
                    }
                }
            }
        }

        // 4. Scan de Aviões Próximos no Céu
        if (_menuCarrierAtivo && Time.time > _tempoProximoScan)
        {
            EscanearAvioesNoAr();
            _tempoProximoScan = Time.time + 2f;
        }

        // 5. Radar de Destino (Para mandar atacar) com Mouse Direito
        if (_selecionadoCarrier != null && _selecionadoCarrier.aguardandoCliqueRadar && !_menuCarrierAtivo)
        {
            if (Input.GetMouseButtonDown(1)) 
            {
                ProcessarCliqueOrdemRadar();
            }
        }

        // 6. SISTEMA DE "CONVÈS ADERENTE" (Parenting)
        // Garante que aviões no navio se movam JUNTO com o navio
        GerenciarParentescoAeronaves();
    }

    void GerenciarParentescoAeronaves()
    {
        // Limpa listas de nulos primeiro
        avioesNoPatio.RemoveAll(a => a == null);
        avioesNoHangar.RemoveAll(a => a == null);

        // Processa aviões no pátio
        foreach (var av in avioesNoPatio)
        {
            if (av == null) continue;
            // Se estiver no chão/pátio, gruda no navio
            if (av.transform.parent != this.transform && 
                (av.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio || 
                 av.estadoAtual == ControleAviao.EstadoAviao.Taxiando ||
                 av.estadoAtual == ControleAviao.EstadoAviao.RetornandoPraVaga))
            {
                av.transform.SetParent(this.transform, true);
            }
            // Se começou a voar, solta do navio
            else if (av.transform.parent == this.transform && 
                    (av.estadoAtual == ControleAviao.EstadoAviao.EmMissao || 
                     av.estadoAtual == ControleAviao.EstadoAviao.Decolando))
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
    }

    void EscanearAvioesNoAr()
    {
        _avioesProximosNoAr.Clear();
        var todosAvioes = Object.FindObjectsByType<ControleAviao>(FindObjectsSortMode.None);
        
        IdentidadeUnidade meuId = GetComponent<IdentidadeUnidade>();
        int meuTime = (meuId != null) ? meuId.teamID : 1;

        foreach (var av in todosAvioes)
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

    new void OnGUI()
    {
        if (!_menuCarrierAtivo) return;
        
        // 1. LIMPEZA DE LISTAS
        avioesNoPatio.RemoveAll(a => a == null);
        avioesNoHangar.RemoveAll(a => a == null);

        // 2. POSICIONAMENTO (20% para a esquerda do canto direito)
        float menuWidth = 380f;
        float menuHeight = 700f; 
        
        // offsetX movido 7% mais para a direita (De 0.20 -> 0.13, agora está mais perto do canto direito)
        float offsetX = Screen.width * 0.13f; 
        
        // Offset Y movido 8% para cima (- Screen.height * 0.08f)
        float offsetY = Screen.height / 2f - (menuHeight / 2f) - (Screen.height * 0.08f);
        
        Rect areaMenu = new Rect(Screen.width - menuWidth - 40f - offsetX, offsetY, menuWidth, menuHeight);
        
        GUI.Box(areaMenu, "<b>⚓ COMANDO DE OPERAÇÕES NAVAIS</b>");

        GUILayout.BeginArea(new Rect(areaMenu.x + 10, areaMenu.y + 25, areaMenu.width - 20, areaMenu.height - 35));
        
        GUILayout.Label($"<b>⚓ Status do Navio:</b> <color=cyan>{(_idCarrier != null ? _idCarrier.nomeDoPais : "USS-Carrier")}</color>");
        GUILayout.Space(5);
        
        if (_avioesProximosNoAr.Count > 0)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<color=cyan><b>📡 RADAR: AVIÕES NO CÉU (ALCANCE)</b></color>");
            
            for (int i = 0; i < _avioesProximosNoAr.Count; i++)
            {
                var av = _avioesProximosNoAr[i];
                if (av == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"✈️ {av.name.Replace("(Clone)","")}", GUILayout.Width(130));
                
                if (GUILayout.Button("⬇️ AUTORIZAR POUSO", GUILayout.Height(25)))
                {
                    av.aeroportoOrigem = this;
                    av.ComandoRetornarBase();
                    _avioesProximosNoAr.RemoveAt(i);
                    break;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

            GUILayout.Label("<color=lime><b>📦 CONVÉS (PRONTO PARA DECOLAR)</b></color>");
            _scrollCarrier = GUILayout.BeginScrollView(_scrollCarrier, GUILayout.Height(120));
            
            for (int i = 0; i < avioesNoPatio.Count; i++)
            {
                var av = avioesNoPatio[i];
                if (av == null) continue;
                string pfx = (av == _selecionadoCarrier) ? "► " : "";
                if (GUILayout.Button($"{pfx}✈️ {av.name.Replace("(Clone)","")}", GUILayout.Height(28))) _selecionadoCarrier = av;
            }
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.Label("<color=orange><b>🛠️ HANGAR INTERNO (RESERVAS)</b></color>");
            
            bool vagaDisponivel = ObterPrimeiraVagaLivre() != null;
            
            for (int i = 0; i < avioesNoHangar.Count; i++)
            {
                var av = avioesNoHangar[i];
                if (av == null) continue;
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"🔒 {av.name.Replace("(Clone)","")}", GUILayout.Width(180));
                
                if (vagaDisponivel)
                {
                    GUI.enabled = !_elevadorOcupado;
                    if (GUILayout.Button("⬆️ ELEVADOR", GUILayout.Width(130))) StartCoroutine(RotinaElevadorSequencial(av, true));
                    GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button("PÁTIO LOTADO", GUILayout.Width(130));
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }

        GUILayout.Space(10);

        if (_selecionadoCarrier != null)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>AERONAVE:</b> <color=yellow>{_selecionadoCarrier.name.Replace("(Clone)","")}</color>");
            
            SistemaDeDanos vidaAviao = _selecionadoCarrier.GetComponent<SistemaDeDanos>();
            if (vidaAviao != null)
            {
                float hpAtual = vidaAviao.vidaAtual;
                float hpMax = vidaAviao.vidaMaxima;
                string corHp = (hpAtual < (hpMax / 2f)) ? "red" : "lime";
                
                GUILayout.Label($"🛡️ <b>Integridade:</b> <color={corHp}>{hpAtual.ToString("F0")} / {hpMax.ToString("F0")}</color>");
                GUILayout.Label($"⛽ <b>Combustível/Munição:</b> <color=lime>Cheio</color>");

                if (hpAtual < hpMax && (avioesNoPatio.Contains(_selecionadoCarrier) || avioesNoHangar.Contains(_selecionadoCarrier)))
                {
                    if (GUILayout.Button("🔧 REPARAR E REABASTECER", GUILayout.Height(30)))
                    {
                        vidaAviao.vidaAtual = vidaAviao.vidaMaxima;
                        Debug.Log("[Porta-Aviões] Avião totalmente reparado!");
                    }
                }
            }

            GUILayout.Space(5);

            if (_selecionadoCarrier.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                if (_selecionadoCarrier.aguardandoCliqueRadar)
                {
                    string infoModo = _modoOrdemAviao == 0 ? "ALVO (ATAQUE/RECON)" : (_modoOrdemAviao == 1 ? "PATRULHA (CRIAR ROTA)" : "SEGUIR (CLIQUE NUM ALIADO)");
                    GUILayout.Label($"<color=yellow>⚠️ MODO {infoModo} ATIVO! Clique no mapa com o Botão Direito.</color>");
                    
                    if (Input.GetMouseButtonDown(1)) 
                    {
                        ProcessarCliqueOrdemRadar();
                    }
                    if (GUILayout.Button("❌ CANCELAR ORDEM", GUILayout.Height(30))) _selecionadoCarrier.aguardandoCliqueRadar = false;
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("🛫 DECOLAR AO ATAQUE", GUILayout.Height(40))) IniciarRadar(0);
                    if (GUILayout.Button("📡 DECOLAR RECON.", GUILayout.Height(40))) IniciarRadar(0); // Opcionalmente, pode forçar radar passivo
                    GUILayout.EndHorizontal();
                    
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("🛡️ DECOLAR PATRULHA", GUILayout.Height(35))) IniciarRadar(1);
                    if (GUILayout.Button("👥 DECOLAR SEGUIR", GUILayout.Height(35))) IniciarRadar(2);
                    GUILayout.EndHorizontal();
                }
            }
            else if (_selecionadoCarrier.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
            {
                if (GUILayout.Button("🔙 ABORTAR E POUSAR", GUILayout.Height(40))) 
                { 
                    _selecionadoCarrier.ComandoRetornarBase(); 
                    _selecionadoCarrier = null; 
                }
            }
            
            if (avioesNoPatio.Contains(_selecionadoCarrier))
            {
                if (!_elevadorOcupado)
                {
                    if (GUILayout.Button("⬇️ DESCER PARA HANGAR", GUILayout.Height(30))) 
                    {
                        StartCoroutine(RotinaElevadorSequencial(_selecionadoCarrier, false));
                        _selecionadoCarrier = null;
                    }
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button("ELEVADOR EM USO", GUILayout.Height(30));
                    GUI.enabled = true;
                }
            }
            GUILayout.EndVertical();
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("FECHAR TELA (Clique no Navio ou aperte O)", GUILayout.Height(30))) _menuCarrierAtivo = false;
        GUILayout.EndArea();
    }

    private void ProcessarCliqueOrdemRadar()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        
        Ray r = Camera.main.ScreenPointToRay(Input.mousePosition);
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
                    if (_selecionadoCarrier.GetComponent<ComportamentoPatrulhaUniversal>()) Destroy(_selecionadoCarrier.GetComponent<ComportamentoPatrulhaUniversal>());
                    var seg = _selecionadoCarrier.gameObject.AddComponent<ComportamentoSeguirUniversal>();
                    seg.Configurar(alvoSeguir.transform);
                    Debug.Log("🎯 Avião designado para Escolta/Seguir!");
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
            if (_selecionadoCarrier.GetComponent<ComportamentoSeguirUniversal>()) Destroy(_selecionadoCarrier.GetComponent<ComportamentoSeguirUniversal>());
            var pat = _selecionadoCarrier.gameObject.AddComponent<ComportamentoPatrulhaUniversal>();
            
            // Assegura que ambos os pontos (navio e alvo) têm a mesma altura para o avião não tentar mergulhar
            Vector3 pt1 = transform.position;
            pt1.y = 80f;
            Vector3 pt2 = pontoAlvo;
            pt2.y = 80f;
            
            List<Vector3> pts = new List<Vector3>() { pt1, pt2 };
            pat.Configurar(pts);
            
            // 🔴 CORREÇÃO IMPORTANTE: Ativar a renderização da linha verde para a patrulha
            DesenharLinhasOrdem linhas = FindFirstObjectByType<DesenharLinhasOrdem>();
            if (linhas != null)
            {
                linhas.lineRenderer.positionCount = 2;
                linhas.lineRenderer.SetPosition(0, pt1);
                linhas.lineRenderer.SetPosition(1, pt2);
            }
            
            Debug.Log("🛡️ Avião designado para rota de Patrulha (Do Navio até o Ponto)!");
        }

        _selecionadoCarrier.aguardandoCliqueRadar = false;
        
        // Dispara o avião pro ar
        if (pontoAlvo != Vector3.zero) _selecionadoCarrier.IniciarMissaoCompleta(pontoAlvo);
        
        _selecionadoCarrier = null;
        _menuCarrierAtivo = false;
    }

    void IniciarRadar(int modo) 
    { 
        _modoOrdemAviao = modo;
        _selecionadoCarrier.aguardandoCliqueRadar = true; 
        _menuCarrierAtivo = false; 
    }

    IEnumerator RotinaElevadorSequencial(ControleAviao av, bool subir)
    {
        _elevadorOcupado = true;
        
        if (subir)
        {
            avioesNoHangar.Remove(av);
            
            // 1. O elevador desce do convés até o hangar interno para buscar o avião
            yield return StartCoroutine(MoverSuave(plataformaElevador, localConvesEntrada, localHangarFrente, 2.5f));
            yield return StartCoroutine(MoverSuave(plataformaElevador, localHangarFrente, localHangarInterno, 2.5f));
            
            av.gameObject.SetActive(true);
            
            // 2. O avião estaciona no elevador
            av.transform.SetParent(plataformaElevador);
            av.transform.localPosition = Vector3.zero;
            av.transform.localRotation = Quaternion.identity;

            // 3. Elevador sobe com o avião: Hangar -> Frente -> Convés
            yield return StartCoroutine(MoverSuave(plataformaElevador, localHangarInterno, localHangarFrente, 2.5f));
            yield return StartCoroutine(MoverSuave(plataformaElevador, localHangarFrente, localConvesEntrada, 3.5f));

            av.transform.SetParent(this.transform); // Gruda no navio
            
            Transform v = ObterPrimeiraVagaLivre();
            if (v != null) 
            {
                av.vagaRetorno = v;
                if (!avioesNoPatio.Contains(av)) avioesNoPatio.Add(av);
                yield return StartCoroutine(av.MoverInterpolado(Vector3.zero, av.velocidadeSolo, true, v));
            }
            else
            {
                if (!avioesNoPatio.Contains(av)) avioesNoPatio.Add(av);
            }
            av.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        }
        else
        {
            avioesNoPatio.Remove(av);
            
            // 1. Garante que o elevador está paradinho no convés
            plataformaElevador.localPosition = localConvesEntrada;
            
            // 2. Pontinho guia para o avião taxiar e subir no elevador
            GameObject tempAlvo = new GameObject("TempAlvoElevador");
            tempAlvo.transform.SetParent(this.transform, false);
            tempAlvo.transform.localPosition = localConvesEntrada;
            
            yield return StartCoroutine(av.MoverInterpolado(Vector3.zero, av.velocidadeSolo, true, tempAlvo.transform));
            Destroy(tempAlvo);
            
            // 3. Centraliza perfeito no elevador e desce
            av.transform.SetParent(plataformaElevador);
            av.transform.localPosition = Vector3.zero;
            av.transform.localRotation = Quaternion.identity;
            av.vagaRetorno = null;

            // 4. Elevador desce as 3 etapas
            yield return StartCoroutine(MoverSuave(plataformaElevador, localConvesEntrada, localHangarFrente, 3.5f));
            yield return StartCoroutine(MoverSuave(plataformaElevador, localHangarFrente, localHangarInterno, 2.5f));
            
            // 5. Avião guardado, tira ele da memória ativa e do visual
            av.transform.SetParent(this.transform);
            av.gameObject.SetActive(false);
            avioesNoHangar.Add(av);
            
            // 6. Elevador retorna VAZIO para fechar o buraco do convés
            yield return StartCoroutine(MoverSuave(plataformaElevador, localHangarInterno, localHangarFrente, 2.5f));
            yield return StartCoroutine(MoverSuave(plataformaElevador, localHangarFrente, localConvesEntrada, 3.5f));
        }
        _elevadorOcupado = false;
    }

    // Sobrecarga do MoverSuave para aceitar local ou global
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

    public override void GuardarNoHangarAutomatico(ControleAviao av)
    {
        base.GuardarNoHangarAutomatico(av);
        if (av != null) av.transform.SetParent(this.transform);
    }
}