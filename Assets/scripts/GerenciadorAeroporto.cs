using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GerenciadorAeroporto : MonoBehaviour
{
    [Header("Hierarquia do Aeroporto (Vincular do Inspector)")]
    [Tooltip("Grupo pai contendo as marcações 'Parada' a 'Parada 4'")]
    public Transform patio;
    
    [Tooltip("Grupo contendo 'Preparacao' e 'Pronto'")]
    public Transform hangarAviao;
    
    [Tooltip("Grupo de waypoints de decolagem: alinhamento -> decolagem -> voo...")]
    public Transform decolagem;
    
    [Tooltip("Grupo de waypoints de pouso OBRIGATÓRIOS (Decida)")]
    public Transform decida;

    [Header("Gestão de Frota e Status")]
    public List<ControleAviao> avioesNoPatio = new List<ControleAviao>();
    public List<ControleAviao> avioesNoHangar = new List<ControleAviao>();
    public List<C700TransporteAereo> transportesC700NoPatio = new List<C700TransporteAereo>();

    [Header("Drone Kamikaze")]
    public GameObject prefabDroneKamikaze;
    public int precoDroneKamikaze = 1500;

    [Header("Interface (UI)")]
    public GameObject menuAeroportoUI;
    
    private bool menuAtivo = false;
    private int abaAtual = 0; 
    private Vector2 scrollPosFrota;
    private Vector2 scrollPosHangar;
    private Vector2 scrollPosC700;
    [HideInInspector] public ControleAviao aviaoSelecionadoParaMissao;
    [HideInInspector] public C700TransporteAereo c700SelecionadoParaMissao;

    // Listas internas de Waypoints lidas no Awake
    [HideInInspector] public List<Transform> waypointsPatio = new List<Transform>();
    [HideInInspector] public Transform wpPreparacao;
    [HideInInspector] public Transform wpPronto;
    [HideInInspector] public List<Transform> waypointsDecolagem = new List<Transform>();
    [HideInInspector] public List<Transform> waypointsDecida = new List<Transform>();
    
    [HideInInspector] public Transform wpAndadar;
    [HideInInspector] public Transform wpAnalise;

    [HideInInspector] public bool esperandoCliqueMassa = false;
    [HideInInspector] public int qtdMassaDrone = 1;

    // --- CACHE DE COMPONENTES (Evita GetComponent repetido) ---
    protected IdentidadeUnidade _identidadeCacheada;
    protected bool _identidadeVerificada = false;

    // --- CACHE PARA OnGUI (Evita alocações repetidas) ---
    private readonly HashSet<Transform> _vagasOcupadas = new HashSet<Transform>();
    private Camera cameraPrincipal;

    protected virtual void Awake()
    {
        // Cache da identidade do aeroporto
        _identidadeCacheada = GetComponent<IdentidadeUnidade>();
        _identidadeVerificada = true;

        if (patio != null)
        {
            foreach (Transform filho in patio)
                if (filho.name.ToLower().Contains("parada")) waypointsPatio.Add(filho);
        }

        if (hangarAviao != null)
        {
            wpPreparacao = hangarAviao.Find("Preparacao");
            wpPronto = hangarAviao.Find("Pronto");
        }

        if (decolagem != null)
        {
            foreach (Transform filho in decolagem) waypointsDecolagem.Add(filho);
        }

        // --- SISTEMA DE EMERGÊNCIA: AUTO-GERAÇÃO DE VAGAS ---
        if (waypointsPatio.Count == 0)
        {
            Debug.LogWarning("[Aeroporto] Nenhuma vaga de pátio encontrada no Prefab! Gerando vagas automáticas...");
            const int numVagas = 6;
            float anguloStep = 360f / numVagas * Mathf.Deg2Rad;
            for (int i = 0; i < numVagas; i++)
            {
                GameObject vagaAuto = new GameObject($"Vaga_Auto_{i}");
                vagaAuto.transform.SetParent(this.transform);
                float ang = i * anguloStep;
                vagaAuto.transform.localPosition = new Vector3(Mathf.Cos(ang) * 40f, 0, Mathf.Sin(ang) * 40f);
                waypointsPatio.Add(vagaAuto.transform);
            }
        }

        if (decida != null)
        {
            foreach (Transform filho in decida) waypointsDecida.Add(filho);
            // Como o objeto no Unity está do inicio (Freiada) ao fim (Alinhando)
            // e o avião entra pelo Alinhando, invertemos a lista inteira!
            waypointsDecida.Reverse();
        }

        // Tenta achar Andadar e Analise (em qualquer lugar dentro do Aeroporto)
        Transform[] todasAsTags = GetComponentsInChildren<Transform>(true);
        for (int i = 0, count = todasAsTags.Length; i < count; i++)
        {
            string nome = todasAsTags[i].name.ToLower();
            if (nome == "andadar") wpAndadar = todasAsTags[i];
            else if (nome == "analise") wpAnalise = todasAsTags[i];
            // Sai mais cedo se já encontrou ambos
            if (wpAndadar != null && wpAnalise != null) break;
        }
    }

    protected virtual void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
    }

    protected virtual void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    protected virtual void OnDestroy()
    {
        RegistroEntidadesJogo.Unregister(this);
    }

    void Start()
    {
        // Inicia o serviço de reparação automática
        StartCoroutine(ManutencaoDeFrota());
    }

    private IEnumerator ManutencaoDeFrota()
    {
        WaitForSeconds espera = new WaitForSeconds(2.0f); // Reutiliza o objeto de espera
        while (true)
        {
            yield return espera;

            // Repara quem está no pátio
            for (int i = avioesNoPatio.Count - 1; i >= 0; i--)
            {
                ControleAviao a = avioesNoPatio[i];
                if (a == null) { avioesNoPatio.RemoveAt(i); continue; }
                if (a.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) continue;

                SistemaDeDanos sd = a.GetComponent<SistemaDeDanos>();
                if (sd != null && sd.vidaAtual < sd.vidaMaxima) 
                    sd.Reparar(sd.vidaMaxima * 0.05f); // 5% de vida no pátio
            }

            // No hangar o reparo é prioritário
            for (int i = avioesNoHangar.Count - 1; i >= 0; i--)
            {
                ControleAviao h = avioesNoHangar[i];
                if (h == null) { avioesNoHangar.RemoveAt(i); continue; }

                SistemaDeDanos sd = h.GetComponent<SistemaDeDanos>();
                if (sd != null && sd.vidaAtual < sd.vidaMaxima) 
                    sd.Reparar(sd.vidaMaxima * 0.10f); // 10% de vida no hangar
            }
        }
    }

    void Update()
    {
        if (cameraPrincipal == null) cameraPrincipal = Camera.main;

        if (Construtor.EmModoConstrucaoAtivo)
        {
            if (menuAtivo || aviaoSelecionadoParaMissao != null || c700SelecionadoParaMissao != null)
            {
                CancelarInteracaoPorConstrucao();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Alpha7))
        {
            if (!_identidadeVerificada) { _identidadeCacheada = GetComponent<IdentidadeUnidade>(); _identidadeVerificada = true; }
            if (_identidadeCacheada != null && _identidadeCacheada.teamID != 1 && _identidadeCacheada.teamID != 0) return;

            menuAtivo = !menuAtivo;
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(menuAtivo);
            Debug.Log("[Aeroporto] Centro de Controle " + (menuAtivo ? "ABERTO" : "FECHADO"));
        }

        if (c700SelecionadoParaMissao != null)
        {
            if (c700SelecionadoParaMissao.AguardandoDestinoAereo && Input.GetMouseButtonDown(1))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

                if (cameraPrincipal == null) return;
                Ray rC700 = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
                Vector3 pontoAlvoC700 = Vector3.zero;

                if (Physics.Raycast(rC700, out RaycastHit hitC700, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    pontoAlvoC700 = hitC700.point;
                }
                else
                {
                    UnityEngine.Plane plano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
                    if (plano.Raycast(rC700, out float distC700))
                    {
                        pontoAlvoC700 = rC700.GetPoint(distC700);
                    }
                }

                if (pontoAlvoC700 != Vector3.zero)
                {
                    c700SelecionadoParaMissao.ReceberOrdemMover(pontoAlvoC700);
                    CriarSinalizador(pontoAlvoC700, c700SelecionadoParaMissao);
                    c700SelecionadoParaMissao = null;
                }
            }
        }

        if (aviaoSelecionadoParaMissao == null) return;

        bool esperandoAutorizacao = aviaoSelecionadoParaMissao.aguardandoCliqueRadar;
        bool emVooConstante = (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.EmMissao);

        if (!esperandoAutorizacao && !emVooConstante) return;
        if (!Input.GetMouseButtonDown(1)) return;
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        if (cameraPrincipal == null) return;
        Ray r = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        Vector3 pontoAlvo = Vector3.zero;

        if (Physics.Raycast(r, out RaycastHit hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            pontoAlvo = hit.point;
        }
        else
        {
            UnityEngine.Plane marPlano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
            float distanciaIntersecao;
            if (marPlano.Raycast(r, out distanciaIntersecao))
            {
                pontoAlvo = r.GetPoint(distanciaIntersecao);
            }
            else
            {
                return;
            }
        }

        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;

        if (esperandoCliqueMassa)
        {
            esperandoCliqueMassa = false;
            StartCoroutine(RotinaLancarMissaoEmMassa(pontoAlvo, qtdMassaDrone));
        }
        else if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
        {
            aviaoSelecionadoParaMissao.IniciarMissaoCompleta(pontoAlvo);
            Debug.Log($"[Aeroporto] Coordenadas recebidas! {aviaoSelecionadoParaMissao.gameObject.name} decolando para: {pontoAlvo}");
        }
        else
        {
            aviaoSelecionadoParaMissao.centroDaPatrulha = pontoAlvo;
            aviaoSelecionadoParaMissao.alvoGPSVoo = pontoAlvo;
            CacaVooRealista cv = aviaoSelecionadoParaMissao.GetComponent<CacaVooRealista>();
            if (cv != null) cv.alvoGPS = pontoAlvo;
            Debug.Log($"[Aeroporto] Rota Alterada! {aviaoSelecionadoParaMissao.gameObject.name} mudando curso para: {pontoAlvo}");
        }

        CriarSinalizador(pontoAlvo, aviaoSelecionadoParaMissao);
        aviaoSelecionadoParaMissao = null;
    }

    public void CancelarInteracaoPorConstrucao()
    {
        if (aviaoSelecionadoParaMissao != null)
        {
            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
        }

        if (c700SelecionadoParaMissao != null)
        {
            c700SelecionadoParaMissao.CancelarModoAereo();
        }

        aviaoSelecionadoParaMissao = null;
        c700SelecionadoParaMissao = null;
        menuAtivo = false;
        esperandoCliqueMassa = false;

        if (menuAeroportoUI != null)
        {
            menuAeroportoUI.SetActive(false);
        }
    }

    private void CriarSinalizador(Vector3 pos, Component aviao)
    {
        // Cria um feixe/pilar de cristal alto indicando o ponto ordenado
        GameObject sinal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(sinal.GetComponent<Collider>()); // Remover colisão para não ferrar física
        sinal.transform.position = pos + new Vector3(0, 50f, 0); 
        sinal.transform.localScale = new Vector3(4f, 100f, 4f); // Cilindro gigante visível de longe
        
        // Pinta da cor do esquadrão ou de Turquesa 
        Color c = new Color(0, 1, 1, 0.4f);
        if (aviao != null)
        {
            ControleAviaoCaca cc = aviao.GetComponent<ControleAviaoCaca>();
            if (cc != null) c = new Color(cc.corIdentificacao.r, cc.corIdentificacao.g, cc.corIdentificacao.b, 0.4f);
        }
        
        Renderer rend = sinal.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = c;
        }

        // Animação e Fade suave
        StartCoroutine(AnimarSinalizador(sinal, c));
    }

    private IEnumerator AnimarSinalizador(GameObject sinal, Color baseColor)
    {
        Renderer rend = sinal.GetComponent<Renderer>();
        float t = 0;
        const float duracao = 3.5f;
        while (t < duracao)
        {
            if (sinal == null) break;
            t += Time.deltaTime;
            sinal.transform.Rotate(0, 180f * Time.deltaTime, 0); // Gira o pilar loucamente
            
            // Pisca e apaga usando Fade no shader default alpha
            if (rend != null && rend.material != null)
            {
                baseColor.a = Mathf.Lerp(0.5f, 0f, t / duracao);
                rend.material.color = baseColor;
            }
            yield return null;
        }
        if (sinal != null) Destroy(sinal);
    }

    public void ComprarAviao(GameObject prefabDeAeronave)
    {
        Vector3 posSpawn = (wpPreparacao != null) ? wpPreparacao.position : transform.position;
        GameObject aeronaveNascente = Instantiate(prefabDeAeronave, posSpawn, Quaternion.identity);

        // --- SISTEMA DE IDENTIDADE (HERANÇA DO AEROPORTO) ---
        if (!_identidadeVerificada) { _identidadeCacheada = GetComponent<IdentidadeUnidade>(); _identidadeVerificada = true; }
        
        IdentidadeUnidade idAviao = aeronaveNascente.GetComponent<IdentidadeUnidade>();
        if (idAviao == null) idAviao = aeronaveNascente.AddComponent<IdentidadeUnidade>();
        
        if (_identidadeCacheada != null)
        {
            idAviao.teamID = _identidadeCacheada.teamID;
            idAviao.nomeDoPais = _identidadeCacheada.nomeDoPais;

            // Se pertencer à IA (Time 2 ou maior), empurra o avião pra mente dela
            if (idAviao.teamID > 1) 
            {
                // Busca o general correto que comanda este time específico
                IA_General_Pro gen = IA_ComandanteRegistry.GetGeneralByTeam(idAviao.teamID);
                if (gen != null)
                {
                    gen.RegistrarUnidade(aeronaveNascente);
                }

                DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("spawn_registrations");
            }
        }

        C700TransporteAereo c700 = aeronaveNascente.GetComponent<C700TransporteAereo>();
        if (c700 != null)
        {
            c700.DefinirAeroportoOrigem(this);
            StartCoroutine(RotinaRecebimentoC700(c700));
            return;
        }

        ControleAviao controleDaNave = aeronaveNascente.GetComponent<ControleAviao>();
        if (controleDaNave == null) controleDaNave = aeronaveNascente.AddComponent<ControleAviao>();

        controleDaNave.aeroportoOrigem = this;
        StartCoroutine(RotinaRecebimento(controleDaNave));
    }

    private IEnumerator RotinaRecebimento(ControleAviao aviao)
    {
        // Vai devagarzinho do Hangar até a frente do Hangar
        if (wpPronto != null && aviao != null)
        {
            yield return StartCoroutine(aviao.MoverInterpolado(Vector3.zero, aviao.velocidadeSolo, false, wpPronto));
        }

        if (aviao == null) yield break;

        Transform vagaDesignada = ObterPrimeiraVagaLivre();
        if (vagaDesignada != null)
        {
            aviao.vagaRetorno = vagaDesignada;
            if (!avioesNoPatio.Contains(aviao)) avioesNoPatio.Add(aviao);
            
            // Vai devagarzinho pra Vaga do Pátio
            yield return StartCoroutine(aviao.MoverInterpolado(Vector3.zero, aviao.velocidadeSolo, false, vagaDesignada));
            
            if (aviao != null) aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        }
        else
        {
            // Se não achou vaga (pátio lotado ou bloqueado), manda pro hangar
            if (!avioesNoHangar.Contains(aviao)) avioesNoHangar.Add(aviao);
            aviao.estadoAtual = ControleAviao.EstadoAviao.ReservaHangar;
            aviao.gameObject.SetActive(false); 
        }
    }

    private IEnumerator RotinaRecebimentoC700(C700TransporteAereo aviao)
    {
        if (aviao == null)
        {
            yield break;
        }

        if (wpPronto != null)
        {
            yield return StartCoroutine(aviao.TaxiarAteTransform(wpPronto));
        }

        if (aviao == null)
        {
            yield break;
        }

        Transform paradaGrande = ObterParadaGrandePreferencial(false);
        if (paradaGrande == null)
        {
            paradaGrande = ObterPrimeiraVagaLivre();
        }
        if (paradaGrande == null)
        {
            paradaGrande = ObterParadaGrandePreferencial(true);
        }

        if (paradaGrande != null)
        {
            aviao.RegistrarPontoEstacionamento(paradaGrande);
            if (!transportesC700NoPatio.Contains(aviao)) transportesC700NoPatio.Add(aviao);
            yield return StartCoroutine(aviao.TaxiarAteTransform(paradaGrande));
            aviao.FinalizarPosicionamentoNoPatio(paradaGrande);
        }
        else
        {
            aviao.transform.position = (wpPronto != null) ? wpPronto.position : transform.position;
            aviao.transform.rotation = (wpPronto != null) ? wpPronto.rotation : transform.rotation;
            if (!transportesC700NoPatio.Contains(aviao)) transportesC700NoPatio.Add(aviao);
            aviao.FinalizarPosicionamentoNoPatio((wpPronto != null) ? wpPronto : transform);
        }
    }

    public IEnumerator RotinaLancarMissaoEmMassa(Vector3 alvo, int quantidade)
    {
        int lancados = 0;
        
        while (lancados < quantidade)
        {
            ControleAviao proximo = avioesNoPatio.Find(a => a != null && a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio && a.GetComponent<KamikazeDrone>() != null);
            
            if (proximo == null)
            {
                proximo = avioesNoHangar.Find(a => a != null && a.GetComponent<KamikazeDrone>() != null);
                if (proximo != null)
                {
                    avioesNoHangar.Remove(proximo);
                    avioesNoPatio.Add(proximo);
                    proximo.gameObject.SetActive(true);
                    
                    if (wpPronto != null)
                    {
                        proximo.transform.position = wpPronto.position;
                        proximo.transform.rotation = wpPronto.rotation;
                    }
                    else
                    {
                        proximo.transform.position = transform.position;
                    }
                    proximo.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
                }
            }

            if (proximo == null)
            {
                Debug.LogWarning("[Ataque Massa] Sem kamikazes suficientes na reserva ou pátio!");
                break;
            }

            proximo.IniciarMissaoCompleta(alvo);
            lancados++;
            
            if (lancados < quantidade) yield return new WaitForSeconds(5f);
        }
    }

    public Transform ObterPrimeiraVagaLivre()
    {
        if (waypointsPatio == null || waypointsPatio.Count == 0) return null;

        _vagasOcupadas.Clear();
        for (int i = avioesNoPatio.Count - 1; i >= 0; i--)
        {
            ControleAviao av = avioesNoPatio[i];
            if (av == null) { avioesNoPatio.RemoveAt(i); continue; }
            if (av.vagaRetorno != null) _vagasOcupadas.Add(av.vagaRetorno);
        }

        for (int i = 0, count = waypointsPatio.Count; i < count; i++)
        {
            Transform wp = waypointsPatio[i];
            if (wp == null || _vagasOcupadas.Contains(wp)) continue;

            bool ocupadoPorC700 = false;
            Collider[] hits = Physics.OverlapSphere(wp.position, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h] == null) continue;

                C700TransporteAereo transporte = hits[h].GetComponentInParent<C700TransporteAereo>();
                if (transporte != null)
                {
                    ocupadoPorC700 = true;
                    break;
                }
            }

            if (!ocupadoPorC700) return wp;
        }
        return null;
    }

    public Transform ObterParadaGrandePreferencial(bool aceitarOcupada = false)
    {
        Transform encontrada = null;

        if (waypointsPatio != null)
        {
            for (int i = 0; i < waypointsPatio.Count; i++)
            {
                Transform wp = waypointsPatio[i];
                if (wp != null && wp.name.ToLowerInvariant().Contains("parada_grande"))
                {
                    encontrada = wp;
                    break;
                }
            }
        }

        if (encontrada == null)
        {
            Transform[] filhos = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < filhos.Length; i++)
            {
                if (filhos[i] != null && filhos[i].name.ToLowerInvariant().Contains("parada_grande"))
                {
                    encontrada = filhos[i];
                    break;
                }
            }
        }

        if (encontrada == null)
        {
            return null;
        }

        if (aceitarOcupada)
        {
            return encontrada;
        }

        Collider[] hits = Physics.OverlapSphere(encontrada.position, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            C700TransporteAereo transporte = hits[i].GetComponentInParent<C700TransporteAereo>();
            if (transporte != null)
            {
                return null;
            }
        }

        return encontrada;
    }

    // --- HELPER: Extrai texto formatado de um avião para OnGUI (evita código repetido) ---
    private string ObterInfoAviao(Component aviao, out string corCristal, out string vidaStr)
    {
        corCristal = "white";
        vidaStr = "";
        
        if (aviao == null) { return ""; }

        ControleAviaoCaca cacaScript = aviao.GetComponent<ControleAviaoCaca>();
        if (cacaScript != null) corCristal = "#" + ColorUtility.ToHtmlStringRGB(cacaScript.corIdentificacao);
        
        string nomeLimpo = aviao.gameObject.name.Replace("(Clone)", "").Trim();

        SistemaDeDanos danos = aviao.GetComponent<SistemaDeDanos>();
        if (danos != null && danos.vidaMaxima > 0)
        {
            int pct = Mathf.RoundToInt((danos.vidaAtual / danos.vidaMaxima) * 100f);
            string corVid = (pct > 50) ? "white" : (pct > 25 ? "yellow" : "red");
            vidaStr = $" (<color={corVid}>{pct}%</color>)";
        }

        return nomeLimpo;
    }

    void OnGUI()
    {
        if (Construtor.EmModoConstrucaoAtivo) return;
        if (!menuAtivo) return;
        if (menuAeroportoUI != null && menuAeroportoUI.activeInHierarchy) return;

        // --- SISTEMA DE FAXINA (Fix para os fantasmas no pátio) ---
        avioesNoPatio.RemoveAll(a => a == null);
        avioesNoHangar.RemoveAll(a => a == null);
        transportesC700NoPatio.RemoveAll(a => a == null);

        float xMenu = (Screen.width / 2f) - 350f - (Screen.width * 0.1f);
        if (xMenu < 10f) xMenu = 10f;
        
        Rect telaDeMenu = new Rect(xMenu, Screen.height / 2f - 300f, 700f, 600f);
        GUI.Box(telaDeMenu, "CENTRO DE CONTROLE TÁTICO & AEROPORTO");

        // --- BOTÃO DE FECHAR (X) ---
        if (GUI.Button(new Rect(telaDeMenu.xMax - 35, telaDeMenu.y + 5, 30, 25), "<b>X</b>"))
        {
            menuAtivo = false;
            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
        }

        GUILayout.BeginArea(new Rect(telaDeMenu.x + 15, telaDeMenu.y + 35, telaDeMenu.width - 30, telaDeMenu.height - 45));
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("✈️ Aba Comercial", GUILayout.Height(35))) { abaAtual = 0; aviaoSelecionadoParaMissao = null; }
        if (GUILayout.Button("🎖️ Aba Militar", GUILayout.Height(35))) { abaAtual = 1; aviaoSelecionadoParaMissao = null; }
        GUILayout.EndHorizontal();

        GUILayout.Space(25);

        if (abaAtual == 0)
        {
            GUILayout.Label("<size=18><b>OPERAÇÕES COMERCIAIS / LOGÍSTICA</b></size>");
            if(GUILayout.Button("[TESTE] Comprar Avião e Mandar pro Pátio", GUILayout.Height(40)))
            {
                GameObject fakeObj = GameObject.CreatePrimitive(PrimitiveType.Cube); 
                fakeObj.name = "Caça_Comprado";
                fakeObj.transform.localScale = new Vector3(3, 1, 3);
                ComprarAviao(fakeObj);
            }
        }
        else if (abaAtual == 1)
        {
            DesenharAbaMilitar();
        }
        GUILayout.EndArea();
    }

    private void DesenharAbaMilitar()
    {
        GUILayout.Label("<size=18><b>FROTA AÉREA E TÁTICA</b></size>");
        
        // Botão de compra para o Drone Kamikaze
        if (prefabDroneKamikaze != null)
        {
            if (GUILayout.Button($"🧨 COMPRAR DRONE KAMIKAZE (${precoDroneKamikaze})", GUILayout.Height(40)))
            {
                if (GerenciadorRecursos.Instancia != null && GerenciadorRecursos.Instancia.dinheiro >= precoDroneKamikaze)
                {
                    GerenciadorRecursos.Instancia.dinheiro -= precoDroneKamikaze;
                    ComprarAviao(prefabDroneKamikaze);
                }
                else
                {
                    Debug.LogWarning("[Aeroporto] Dinheiro insuficiente para Drone Kamikaze!");
                }
            }
        }
        else
        {
            GUI.enabled = false;
            GUILayout.Button("🧨 DRONE KAMIKAZE (Prefab não vinculado)", GUILayout.Height(40));
            GUI.enabled = true;
        }

        GUILayout.BeginHorizontal();

        // === COLUNA ESQUERDA: FROTA ATIVA ===
        GUILayout.BeginVertical("box", GUILayout.Width(320));
        GUILayout.Label($"<b>FROTA ATIVA ({avioesNoPatio.Count + transportesC700NoPatio.Count})</b>");
        
        scrollPosFrota = GUILayout.BeginScrollView(scrollPosFrota, GUILayout.Height(200));
        for (int i = 0, count = avioesNoPatio.Count; i < count; i++)
        {
            ControleAviao a = avioesNoPatio[i];
            if (a == null) continue;
            
            string nomeLimpo = ObterInfoAviao(a, out string corCristal, out string vidaStr);
            string corEst = (a.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio) ? "green" : "red";

            if (GUILayout.Button($"<color={corCristal}>■</color> ✈️ {nomeLimpo}{vidaStr} [<color={corEst}>{a.estadoAtual}</color>]", GUILayout.Height(30)))
                aviaoSelecionadoParaMissao = a;
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // === COLUNA DIREITA: HANGAR ===
        GUILayout.BeginVertical("box", GUILayout.Width(320));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>HANGAR ({avioesNoHangar.Count})</b>");
        if (GUILayout.Button("Lib. Todos", GUILayout.Width(75), GUILayout.Height(20)))
        {
            LiberarTodosDoHangar();
        }
        GUILayout.EndHorizontal();

        scrollPosHangar = GUILayout.BeginScrollView(scrollPosHangar, GUILayout.Height(200));
        for (int i = avioesNoHangar.Count - 1; i >= 0; i--)
        {
            ControleAviao h = avioesNoHangar[i];
            if (h == null) continue;
            
            string nomeLimpo = ObterInfoAviao(h, out string corCristal, out string vidaStr);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={corCristal}>■</color> 🔒 {nomeLimpo}{vidaStr}", GUILayout.Width(170));
            
            if (aviaoSelecionadoParaMissao != null && avioesNoPatio.Contains(aviaoSelecionadoParaMissao))
            {
                if (GUILayout.Button("⮂ TROCAR", GUILayout.Height(25)))
                {
                    TrocarAvioesLogicaGeral(h, aviaoSelecionadoParaMissao);
                    aviaoSelecionadoParaMissao = null; 
                    GUILayout.EndHorizontal();
                    break; 
                }
            }
            else
            {
                if (ObterPrimeiraVagaLivre() != null)
                {
                    if (GUILayout.Button("▶ LIBERAR", GUILayout.Height(25)))
                    {
                        LiberarAviaoParaPatio(h);
                        GUILayout.EndHorizontal();
                        break;
                    }
                }
                else 
                {
                    GUI.enabled = false;
                    GUILayout.Button("Pátio L.(X)", GUILayout.Height(25));
                    GUI.enabled = true;
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(20);
        
        // === PAINEL DE ORDENS DO AVIÃO SELECIONADO ===
        if (aviaoSelecionadoParaMissao != null && avioesNoPatio.Contains(aviaoSelecionadoParaMissao))
        {
            string nomeLimpo = ObterInfoAviao(aviaoSelecionadoParaMissao, out string corCristal, out string vidaStr);
            GUILayout.Label($"<b>PAINEL DE ORDENS: <color={corCristal}>■</color> {nomeLimpo}{vidaStr}</b>");
            
            if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
            {
                if (aviaoSelecionadoParaMissao.aeroportoOrigem != this && aviaoSelecionadoParaMissao.aeroportoOrigem != null)
                {
                    GUILayout.Label($"<color=orange>✈️ Estacionado em outra base/navio: {aviaoSelecionadoParaMissao.aeroportoOrigem.name.Replace("(Clone)","")}</color>");
                    if (GUILayout.Button("🔙 REQUISITAR RETORNO IMEDIATO", GUILayout.Height(50)))
                    {
                        aviaoSelecionadoParaMissao.aeroportoOrigem = this;
                        aviaoSelecionadoParaMissao.IniciarMissaoCompleta(transform.position);
                        aviaoSelecionadoParaMissao = null;
                        menuAtivo = false;
                        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                    }
                }
                else if (aviaoSelecionadoParaMissao.aguardandoCliqueRadar)
                {
                    GUILayout.Label("<color=yellow>⚠️ MODO ALVO ATIVO! Feche o Menu e Clique no mapa com o Botão Direito.</color>");
                    if (Input.GetMouseButtonDown(1)) // Botão direito do mouse
                    {
                        if (cameraPrincipal == null) cameraPrincipal = Camera.main;
                        if (cameraPrincipal == null) return;
                        Ray r = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
                        Vector3 pontoAlvo = Vector3.zero;

                        // Tenta Raycast físico (para pegar unidades ou terra)
                        if (Physics.Raycast(r, out RaycastHit hit))
                        {
                            pontoAlvo = hit.point;
                        }
                        else
                        {
                            UnityEngine.Plane marPlano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
                            float dist;
                            if (marPlano.Raycast(r, out dist)) pontoAlvo = r.GetPoint(dist);
                        }

                        if (pontoAlvo != Vector3.zero)
                        {
                            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
                            
                            if (esperandoCliqueMassa)
                            {
                                esperandoCliqueMassa = false;
                                StartCoroutine(RotinaLancarMissaoEmMassa(pontoAlvo, qtdMassaDrone));
                            }
                            else
                            {
                                aviaoSelecionadoParaMissao.IniciarMissaoCompleta(pontoAlvo);
                            }
                            
                            menuAtivo = false; 
                            if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                        }
                    }
                    if (GUILayout.Button("❌ Cancelar Ordem", GUILayout.Height(30)))
                    {
                        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = false;
                        esperandoCliqueMassa = false;
                    }
                }
                    bool isKamikaze = aviaoSelecionadoParaMissao.GetComponent<KamikazeDrone>() != null;
                    bool isBombardeiro = aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>() != null;

                    if (isKamikaze)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"<b>Qtd. P/ Ataque:</b> {qtdMassaDrone}");
                        if (GUILayout.Button("-", GUILayout.Width(35), GUILayout.Height(30))) qtdMassaDrone = Mathf.Max(1, qtdMassaDrone - 1);
                        if (GUILayout.Button("+", GUILayout.Width(35), GUILayout.Height(30))) qtdMassaDrone++;
                        if (GUILayout.Button("Todos", GUILayout.Width(60), GUILayout.Height(30))) 
                        {
                            int totais = 0;
                            foreach(var a in avioesNoPatio) if (a != null && a.GetComponent<KamikazeDrone>() != null) totais++;
                            foreach(var a in avioesNoHangar) if (a != null && a.GetComponent<KamikazeDrone>() != null) totais++;
                            qtdMassaDrone = totais;
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("🚀 ATAQUE EM MASSA", GUILayout.Height(40))) 
                        {
                            aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
                            esperandoCliqueMassa = true;
                        }
                        if (GUILayout.Button("💣 Ataque Solo", GUILayout.Height(40))) 
                        {
                            ExecutarModoRadar(false);
                            esperandoCliqueMassa = false;
                        }
                        GUILayout.EndHorizontal();
                    }
                    else if (isBombardeiro)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("💣 Ataque Área (Tapete)", GUILayout.Height(40))) 
                        {
                            aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.AtaqueAoSolo;
                            ExecutarModoRadar(false);
                        }
                        if (GUILayout.Button("🛡️ Radar (Móvel)", GUILayout.Height(40))) 
                        {
                            aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.Patrulha;
                            ExecutarModoRadar(false);
                        }
                        if (GUILayout.Button("🚀 Ataque em Massa", GUILayout.Height(40))) 
                        {
                            aviaoSelecionadoParaMissao.GetComponent<AviaoBombardeiro>().modoDeAtaque = AviaoBombardeiro.ModoAtaque.AtaqueEmMassa;
                            ExecutarModoRadar(false);
                        }
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("👁️ Reconhecimento", GUILayout.Height(40))) ExecutarModoRadar(true);
                        if (GUILayout.Button("🛡️ Patrulha Aérea", GUILayout.Height(40))) ExecutarModoRadar(false);
                        if (GUILayout.Button("💣 Ataque Solo", GUILayout.Height(40))) ExecutarModoRadar(false);
                        GUILayout.EndHorizontal();
                    }
            }
            else if (aviaoSelecionadoParaMissao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
            {
                GUILayout.Label("<color=cyan>Aeronave civil/militar operando no espaço aéreo.</color>");
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("🎯 ALTERAR ALVO/DESTINO", GUILayout.Height(50))) 
                {
                    ExecutarModoRadar(false);
                }

                if (GUILayout.Button("🔙 ABORTAR E RETORNAR À BASE", GUILayout.Height(50)))
                {
                    aviaoSelecionadoParaMissao.ComandoRetornarBase();
                    aviaoSelecionadoParaMissao = null;
                    menuAtivo = false;
                    if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                 GUILayout.Label($"<color=orange>Aeronave em trânsito: {aviaoSelecionadoParaMissao.estadoAtual}...</color>");
                 GUI.enabled = false;
                 GUILayout.Button("Aguarde a manobra de pista...", GUILayout.Height(40));
                 GUI.enabled = true;
            }
        }

        DesenharPainelC700();
    }

    private void DesenharPainelC700()
    {
        if (transportesC700NoPatio.Count == 0)
        {
            return;
        }

        if (c700SelecionadoParaMissao == null && transportesC700NoPatio.Count == 1)
        {
            c700SelecionadoParaMissao = transportesC700NoPatio[0];
        }

        GUILayout.Space(12);
        GUILayout.BeginVertical("box");
        GUILayout.Label("<b>TRANSPORTE C700</b>");
        scrollPosC700 = GUILayout.BeginScrollView(scrollPosC700, GUILayout.Height(300));

        for (int i = 0; i < transportesC700NoPatio.Count; i++)
        {
            C700TransporteAereo transporte = transportesC700NoPatio[i];
            if (transporte == null) continue;

            string nomeLimpo = ObterInfoAviao(transporte, out string corCristal, out string vidaStr);
            string corEstado = transporte.EstaNoSolo ? "green" : "orange";
            if (GUILayout.Button($"<color={corCristal}>■</color> {nomeLimpo}{vidaStr} [<color={corEstado}>{transporte.estadoAtual}</color>]", GUILayout.Height(28)))
            {
                c700SelecionadoParaMissao = transporte;
                aviaoSelecionadoParaMissao = null;
            }
        }

        if (c700SelecionadoParaMissao != null && transportesC700NoPatio.Contains(c700SelecionadoParaMissao))
        {
            GUILayout.Space(8);
            string nomeSelecionado = ObterInfoAviao(c700SelecionadoParaMissao, out string corCristalSelecionado, out string vidaSelecionada);
            GUILayout.Label($"<b>SELECIONADO:</b> <color={corCristalSelecionado}>■</color> {nomeSelecionado}{vidaSelecionada}");

            GUILayout.Label($"Estado: {c700SelecionadoParaMissao.estadoAtual}");
            GUILayout.Label($"Carga real: {c700SelecionadoParaMissao.QuantidadeCargaAtual}/{c700SelecionadoParaMissao.CapacidadeCargaAtual} | Manifesto: {c700SelecionadoParaMissao.QuantidadeManifestoTotal}");

            if (c700SelecionadoParaMissao.TemDestinoVisual)
            {
                Vector3 destinoAtual = c700SelecionadoParaMissao.DestinoVisualAtual;
                GUILayout.Label($"Destino: X {destinoAtual.x:0} / Z {destinoAtual.z:0}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Selecionar no mapa", GUILayout.Height(30)))
            {
                SelecionarTransporteNoMapa(c700SelecionadoParaMissao);
            }

            GUI.enabled = c700SelecionadoParaMissao.EstaNoSolo;
            if (GUILayout.Button("Puxar tropas", GUILayout.Height(30)))
            {
                c700SelecionadoParaMissao.PuxarUnidadesProximas();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (c700SelecionadoParaMissao.EstaNoSolo)
            {
                if (c700SelecionadoParaMissao.AguardandoDestinoAereo)
                {
                    GUILayout.Label("<color=yellow>MODO AEREO ATIVO. FECHE O MENU E CLIQUE COM O BOTAO DIREITO NO MAPA.</color>");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Cancelar ordem", GUILayout.Height(34)))
                    {
                        c700SelecionadoParaMissao.CancelarModoAereo();
                        c700SelecionadoParaMissao = null;
                    }
                    if (GUILayout.Button("Fechar menu", GUILayout.Height(34)))
                    {
                        menuAtivo = false;
                        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    if (GUILayout.Button("Preparar decolagem / destino", GUILayout.Height(40)))
                    {
                        c700SelecionadoParaMissao.PrepararMissaoAerea();
                        SelecionarTransporteNoMapa(c700SelecionadoParaMissao);
                        menuAtivo = false;
                        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                    }
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Desembarcar carga", GUILayout.Height(34)))
                {
                    c700SelecionadoParaMissao.DesembarcarTudo();
                }
                if (GUILayout.Button("Desembarcar manifesto", GUILayout.Height(34)))
                {
                    c700SelecionadoParaMissao.DesembarcarManifestoConfigurado();
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Limpar manifesto", GUILayout.Height(30)))
                {
                    c700SelecionadoParaMissao.LimparManifestoConfigurado();
                }
            }
            else
            {
                GUILayout.Label("<color=cyan>C700 em voo ou manobra.</color>");
                if (GUILayout.Button("Mandar retornar", GUILayout.Height(40)))
                {
                    c700SelecionadoParaMissao.OrdenarRetornoAoAeroporto();
                    c700SelecionadoParaMissao = null;
                    menuAtivo = false;
                    if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
                }
            }

            DesenharManifestoC700(c700SelecionadoParaMissao);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DesenharManifestoC700(C700TransporteAereo transporte)
    {
        if (transporte == null || transporte.ManifestoConfigurado == null || transporte.ManifestoConfigurado.Count == 0)
        {
            return;
        }

        GUILayout.Space(8);
        GUILayout.Label("<b>MANIFESTO CONFIGURAVEL</b>");

        for (int i = 0; i < transporte.ManifestoConfigurado.Count; i++)
        {
            C700TransporteAereo.EntradaManifesto entrada = transporte.ManifestoConfigurado[i];
            if (entrada == null)
            {
                continue;
            }

            int ajusteRapido = Mathf.Max(1, entrada.ajusteRapido);
            int ajustePesado = Mathf.Max(ajusteRapido, entrada.ajustePesado);

            GUILayout.BeginVertical("box");
            GUILayout.Label($"{entrada.nome}: {entrada.quantidade}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-" + ajustePesado, GUILayout.Height(24)))
            {
                transporte.AjustarManifesto(i, -ajustePesado);
            }
            if (GUILayout.Button("-" + ajusteRapido, GUILayout.Height(24)))
            {
                transporte.AjustarManifesto(i, -ajusteRapido);
            }
            if (GUILayout.Button("+" + ajusteRapido, GUILayout.Height(24)))
            {
                transporte.AjustarManifesto(i, ajusteRapido);
            }
            if (GUILayout.Button("+" + ajustePesado, GUILayout.Height(24)))
            {
                transporte.AjustarManifesto(i, ajustePesado);
            }
            GUILayout.EndHorizontal();

            if (entrada.prefabDesembarque == null)
            {
                GUILayout.Label("Prefab de desembarque nao configurado.");
            }
            GUILayout.EndVertical();
        }
    }

    private void SelecionarTransporteNoMapa(C700TransporteAereo transporte)
    {
        if (transporte == null)
        {
            return;
        }

        ControleUnidade controle = transporte.GetComponent<ControleUnidade>();
        if (controle == null)
        {
            return;
        }

        GerenteSelecao gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();
        if (gerenteSelecao != null)
        {
            gerenteSelecao.DeselecionarTudo();
            if (!gerenteSelecao.unidadesSelecionadas.Contains(controle))
            {
                gerenteSelecao.unidadesSelecionadas.Add(controle);
            }
        }

        controle.DefinirSelecao(true);
    }

    private void ExecutarModoRadar(bool deveSerPassivo)
    {
        if (aviaoSelecionadoParaMissao == null) return;
        
        // Tenta forçar o modo no script de Missil, caso exista
        LancadorMisselCaca missilScript = aviaoSelecionadoParaMissao.GetComponent<LancadorMisselCaca>();
        if (missilScript != null)
        {
            missilScript.modoPassivo = deveSerPassivo;
            Debug.Log($"[Aeroporto] Modo Passivo definido como: {deveSerPassivo}");
        }

        aviaoSelecionadoParaMissao.aguardandoCliqueRadar = true;
        menuAtivo = false;
        if (menuAeroportoUI != null) menuAeroportoUI.SetActive(false);
        Debug.Log($"[Aeroporto] Modo Missão Ativado. Fechando painel. Dê a ordem com o clique Direito!");
    }

    private void TrocarAvioesLogicaGeral(ControleAviao modeloSubsaturado, ControleAviao hangarASeAfastar)
    {
        if (hangarASeAfastar.estadoAtual != ControleAviao.EstadoAviao.ProntoNoPatio) return;

        Transform vagaOcupadaLivre = hangarASeAfastar.vagaRetorno;
        Vector3 xyzVaga = hangarASeAfastar.transform.position;
        Quaternion anguloVaga = hangarASeAfastar.transform.rotation;

        avioesNoPatio.Remove(hangarASeAfastar);
        avioesNoHangar.Remove(modeloSubsaturado);

        GuardarAviaoNoHangarInstantaneo(hangarASeAfastar, false);

        modeloSubsaturado.gameObject.SetActive(true);
        modeloSubsaturado.transform.position = xyzVaga;
        modeloSubsaturado.transform.rotation = anguloVaga;
        modeloSubsaturado.vagaRetorno = vagaOcupadaLivre; 
        modeloSubsaturado.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;
        avioesNoPatio.Add(modeloSubsaturado);
    }

    private void LiberarAviaoParaPatio(ControleAviao aviaoDoHangar)
    {
        Transform vagaDesignada = ObterPrimeiraVagaLivre();
        if (vagaDesignada == null) return;
        
        ColocarAviaoInstantaneamenteNoPatio(aviaoDoHangar, vagaDesignada, true);
    }
    
    public void LiberarTodosDoHangar()
    {
        // Copia a lista para evitar modificação simultânea no foreach
        List<ControleAviao> copiaHangar = new List<ControleAviao>(avioesNoHangar);
        for (int i = 0, count = copiaHangar.Count; i < count; i++)
        {
            if (ObterPrimeiraVagaLivre() == null) break; // Pátio lotado
            if (copiaHangar[i] != null) LiberarAviaoParaPatio(copiaHangar[i]);
        }
    }
    
    private IEnumerator TrazerAviaoParaPatio(ControleAviao aviao, Transform vaga)
    {
        if (aviao == null) yield break;
        ColocarAviaoInstantaneamenteNoPatio(aviao, vaga, true);
        yield break;
    }

    public virtual void GuardarNoHangarAutomatico(ControleAviao aviao)
    {
        GuardarAviaoNoHangarInstantaneo(aviao, true);
    }

    protected void GuardarAviaoNoHangarInstantaneo(ControleAviao aviao, bool removerDoPatio)
    {
        if (aviao == null) return;

        if (removerDoPatio)
        {
            avioesNoPatio.Remove(aviao);
        }

        if (!avioesNoHangar.Contains(aviao)) avioesNoHangar.Add(aviao);

        aviao.aguardandoCliqueRadar = false;
        aviao.ordemParaRetorno = false;
        aviao.estaEmModoVooFisico = false;
        aviao.estadoAtual = ControleAviao.EstadoAviao.ReservaHangar;
        aviao.vagaRetorno = null;
        aviao.transform.SetParent(transform, true);

        if (wpPreparacao != null)
        {
            aviao.transform.position = wpPreparacao.position;
            aviao.transform.rotation = wpPreparacao.rotation;
        }
        else if (hangarAviao != null)
        {
            aviao.transform.position = hangarAviao.position;
            aviao.transform.rotation = hangarAviao.rotation;
        }

        aviao.gameObject.SetActive(false);
    }

    protected bool ColocarAviaoInstantaneamenteNoPatio(ControleAviao aviao, Transform vaga, bool removerDoHangar)
    {
        if (aviao == null || vaga == null) return false;

        if (removerDoHangar)
        {
            avioesNoHangar.Remove(aviao);
        }

        aviao.gameObject.SetActive(true);
        aviao.transform.SetParent(transform, true);
        aviao.transform.position = vaga.position;
        aviao.transform.rotation = vaga.rotation;
        aviao.vagaRetorno = vaga;
        aviao.aguardandoCliqueRadar = false;
        aviao.ordemParaRetorno = false;
        aviao.estaEmModoVooFisico = false;
        aviao.estadoAtual = ControleAviao.EstadoAviao.ProntoNoPatio;

        if (!avioesNoPatio.Contains(aviao)) avioesNoPatio.Add(aviao);
        return true;
    }
}
