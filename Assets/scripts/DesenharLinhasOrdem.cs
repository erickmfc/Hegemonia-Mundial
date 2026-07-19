using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class DesenharLinhasOrdem : MonoBehaviour
{
    private static int frameCliqueConsumido = -1;

    public static bool ConsumiuCliqueEsteFrame()
    {
        return frameCliqueConsumido == Time.frameCount;
    }

    private static void ConsumirCliqueNoFrame()
    {
        frameCliqueConsumido = Time.frameCount;
    }

    public LineRenderer lineRenderer;
    private GerenteSelecao gerenteSelecao;

    [Header("Marcadores")]
    public GameObject prefabMarcadorPatrulha;

    [Header("Estados Atuais")]
    public bool modoPatrulhaAtivo = false;
    public bool modoSeguirAtivo = false;
    public bool modoAtaqueAtivo = false;
    private float distanciaSeguimentoPadrao = 200f;

    // Overlay HUD para modo ataque/patrulha
    private GameObject _overlayHUDAtaque;
    private UnityEngine.UI.Text _overlayTextoAtaque;

    public List<Vector3> pontosPatrulha = new List<Vector3>();
    private readonly List<GameObject> _alvosModo = new List<GameObject>(32);

    void Start()
    {
        gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();
        GarantirLineRenderer();
    }

    void CriarLineRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.5f;
        lineRenderer.endWidth = 0.5f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(1f, 0.85f, 0f, 0.6f); // Amarelo/Dourado semi-transparente
        lineRenderer.endColor = new Color(1f, 0.85f, 0f, 0.6f);
        lineRenderer.positionCount = 0;
    }

    void GarantirLineRenderer()
    {
        if (lineRenderer == null)
        {
            CriarLineRenderer();
        }
    }

    public void IniciarModoPatrulha()
    {
        IniciarModoPatrulha(null);
    }

    public void IniciarModoPatrulha(List<GameObject> selecionadosSnapshot)
    {
        DefinirAlvosModo(selecionadosSnapshot);
        if (!ValidarAlvosModo())
        {
            Debug.LogWarning("MODO PATRULHA: nenhuma unidade valida selecionada.");
            return;
        }

        modoPatrulhaAtivo = true;
        modoSeguirAtivo = false;
        modoAtaqueAtivo = false;
        pontosPatrulha.Clear();
        GarantirLineRenderer();
        lineRenderer.positionCount = 0;
        InteractionModeService.Request(
            InteractionOwner.Patrol,
            new InteractionPolicy
            {
                bloqueiaSelecao = true,
                bloqueiaOrdemMundo = true,
                bloqueiaRotacaoCamera = false,
                consomeLMB = true,
                consomeRMB = true
            },
            "Patrulha em edição");
        MostrarOverlayHUD("🗺 MODO PATRULHA: Clique ESQUERDO ou DIREITO no mapa para marcar pontos. ENTER confirma. ESC cancela.", new Color(1f, 0.85f, 0f));
        DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Patrulha iniciada (alvos=" + _alvosModo.Count + ")");
        Debug.Log("MODO PATRULHA: Clique ESQUERDO ou DIREITO para marcar pontos de patrulha. ESC ou ENTER para finalizar.");
    }

    // ── MODO ATAQUE ──────────────────────────────────────────────────────────
    public void IniciarModoAtaque()
    {
        IniciarModoAtaque(null);
    }

    public void IniciarModoAtaque(List<GameObject> selecionadosSnapshot)
    {
        DefinirAlvosModo(selecionadosSnapshot);
        if (!ValidarAlvosModo())
        {
            Debug.LogWarning("MODO ATAQUE: nenhuma unidade valida selecionada.");
            return;
        }

        modoAtaqueAtivo = true;
        modoPatrulhaAtivo = false;
        modoSeguirAtivo = false;
        GarantirLineRenderer();
        lineRenderer.positionCount = 0;
        InteractionModeService.Request(
            InteractionOwner.Attack,
            new InteractionPolicy
            {
                bloqueiaSelecao = true,
                bloqueiaOrdemMundo = true,
                bloqueiaRotacaoCamera = false,
                consomeLMB = true,
                consomeRMB = true
            },
            "Ataque em edição");
        MostrarOverlayHUD("🎯 MODO ATAQUE: Clique no alvo ou área para atacar. ESC cancela.", new Color(1f, 0.2f, 0.1f));
        DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Ataque iniciado (alvos=" + _alvosModo.Count + ")");
        Debug.Log("MODO ATAQUE: Clique esquerdo/direito no alvo ou área para atacar.");
    }

    public void IniciarModoSeguir()
    {
        IniciarModoSeguir(null);
    }

    public void IniciarModoSeguir(List<GameObject> selecionadosSnapshot)
    {
        DefinirAlvosModo(selecionadosSnapshot);
        if (!ValidarAlvosModo())
        {
            Debug.LogWarning("MODO SEGUIR: nenhuma unidade valida selecionada.");
            return;
        }

        modoSeguirAtivo = true;
        modoPatrulhaAtivo = false;
        GarantirLineRenderer();
        lineRenderer.positionCount = 0;
        InteractionModeService.Request(
            InteractionOwner.Follow,
            new InteractionPolicy
            {
                bloqueiaSelecao = true,
                bloqueiaOrdemMundo = true,
                bloqueiaRotacaoCamera = true,
                consomeLMB = true,
                consomeRMB = true
            },
            "Seguir em edição");
        DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Seguir iniciado (alvos=" + _alvosModo.Count + ")");
        Debug.Log("MODO SEGUIR: clique com o botao direito em uma unidade aliada. ESC cancela.");
    }

    public void DefinirDistanciaSeguimento(float distancia)
    {
        distanciaSeguimentoPadrao = Mathf.Clamp(distancia, 25f, 10000f);
    }

    void Update()
    {
        if (!modoPatrulhaAtivo && !modoSeguirAtivo && !modoAtaqueAtivo)
        {
            return;
        }

        if (!ValidarAlvosModo())
        {
            DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Modo cancelado (alvos invalidos/vazios).");
            LimparTudo();
            return;
        }

        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // ── PATRULHA ──────────────────────
        if (modoPatrulhaAtivo)
        {
            if (Input.GetMouseButtonDown(0)) // Clique Esquerdo: Adiciona ponto
            {
                Vector3 pontoPatrulha;
                if (TryObterPontoPatrulha(out pontoPatrulha))
                {
                    ConsumirCliqueNoFrame();
                    pontosPatrulha.Add(pontoPatrulha);
                    AtualizarLinhaVisualPatrulha();
                    MostrarMarcadorPatrulha(pontoPatrulha);
                    // AplicarOrdemPatrulha() não é chamado aqui, para que eles só comecem ao confirmar.
                }
            }
            else if (Input.GetMouseButtonDown(1)) // Clique Direito: Confirma e Inicia a patrulha
            {
                if (pontosPatrulha.Count >= 1)
                {
                    AplicarOrdemPatrulha();
                    DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Patrulha confirmada (pontos=" + pontosPatrulha.Count + ", alvos=" + _alvosModo.Count + ")");
                    Debug.Log("Patrulha confirmada e iniciada via Botão Direito.");
                }
                else
                {
                    Debug.LogWarning("Cancelando patrulha (0 pontos).");
                }
                LimparTudo();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (pontosPatrulha.Count >= 1)
                {
                    AplicarOrdemPatrulha();
                }
                LimparTudo();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                LimparTudo();
                return;
            }
        }

        if (modoPatrulhaAtivo && Input.GetKeyDown(KeyCode.Backspace) && pontosPatrulha.Count > 0)
        {
            pontosPatrulha.RemoveAt(pontosPatrulha.Count - 1);
            AtualizarLinhaVisualPatrulha();
        }

        // ── SEGUIR ───────────────────────────────────────────────────────────
        if (modoSeguirAtivo && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            if (Camera.main == null)
            {
                return;
            }

            ConsumirCliqueNoFrame();
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                ControleUnidade alvo = hit.collider.GetComponentInParent<ControleUnidade>();
                if (alvo != null)
                {
                    AplicarOrdemSeguir(alvo.transform);
                    MostrarMarcadorPatrulha(alvo.transform.position, new Color(0.2f, 0.85f, 1f, 0.95f), 14f, 2.2f);
                    DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Seguir confirmado (alvos=" + _alvosModo.Count + ")");
                    Debug.Log("Ordem de SEGUIR confirmada.");
                    LimparTudo();
                }
                else
                {
                    Debug.LogWarning("Voce nao clicou em uma unidade para seguir.");
                }
            }
        }

        // ── ATAQUE: clique esquerdo/direito define alvo ou área ──────────────
        if (modoAtaqueAtivo && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
        {
            if (Camera.main == null) return;

            ConsumirCliqueNoFrame();
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            Vector3 pontoAtaque = Vector3.zero;
            ControleUnidade alvoUnidade = null;
            bool encontrou = false;

            if (Physics.Raycast(ray, out hit))
            {
                pontoAtaque = hit.point;
                encontrou = true;
                // Verifica se clicou em uma unidade (inimiga ou qualquer alvo)
                alvoUnidade = hit.collider.GetComponentInParent<ControleUnidade>();
            }
            else
            {
                // Fallback: plano do chão
                UnityEngine.Plane plano = new UnityEngine.Plane(Vector3.up, Vector3.zero);
                float dist;
                if (plano.Raycast(ray, out dist))
                {
                    pontoAtaque = ray.GetPoint(dist);
                    encontrou = true;
                }
            }

            if (encontrou)
            {
                AplicarOrdemAtaque(pontoAtaque, alvoUnidade != null ? alvoUnidade.transform : null);

                // Marcador visual no ataque removido para não aparecer no jogo 3D, conforme pedido do usuário
                DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode",
                    "Ataque confirmado em " + pontoAtaque + (alvoUnidade != null ? " (alvo=" + alvoUnidade.name + ")" : ""));
                Debug.Log("[ModoAtaque] Ordem de ataque emitida em " + pontoAtaque);
                LimparTudo();
            }
        }

        // ESC cancela qualquer modo
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (modoPatrulhaAtivo)
                DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Patrulha cancelada (esc).");
            else if (modoSeguirAtivo)
                DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Seguir cancelado (esc).");
            else if (modoAtaqueAtivo)
                DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Ataque cancelado (esc).");

            LimparTudo();
        }
    }

    void LimparTudo()
    {
        modoPatrulhaAtivo = false;
        modoSeguirAtivo = false;
        modoAtaqueAtivo = false;
        pontosPatrulha.Clear();
        _alvosModo.Clear();
        GarantirLineRenderer();
        lineRenderer.positionCount = 0;
        InteractionModeService.Release(InteractionOwner.Patrol);
        InteractionModeService.Release(InteractionOwner.Follow);
        InteractionModeService.Release(InteractionOwner.Attack);
        OcultarOverlayHUD();
    }

    // ── Overlay HUD ─────────────────────────────────────────────────────────
    void MostrarOverlayHUD(string mensagem, Color cor)
    {
        OcultarOverlayHUD();

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        _overlayHUDAtaque = new GameObject("_OverlayModoOrdem");
        _overlayHUDAtaque.transform.SetParent(canvas.transform, false);

        // Painel de fundo
        UnityEngine.UI.Image bg = _overlayHUDAtaque.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        RectTransform rt = _overlayHUDAtaque.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(760f, 54f);

        // Texto
        GameObject textoObj = new GameObject("_OverlayTexto");
        textoObj.transform.SetParent(_overlayHUDAtaque.transform, false);
        _overlayTextoAtaque = textoObj.AddComponent<UnityEngine.UI.Text>();
        _overlayTextoAtaque.text = mensagem;
        _overlayTextoAtaque.color = cor;
        _overlayTextoAtaque.fontSize = 18;
        _overlayTextoAtaque.fontStyle = FontStyle.Bold;
        _overlayTextoAtaque.alignment = TextAnchor.MiddleCenter;
        _overlayTextoAtaque.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform rtTxt = textoObj.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = new Vector2(8f, 4f);
        rtTxt.offsetMax = new Vector2(-8f, -4f);
    }

    void OcultarOverlayHUD()
    {
        if (_overlayHUDAtaque != null)
        {
            Destroy(_overlayHUDAtaque);
            _overlayHUDAtaque = null;
            _overlayTextoAtaque = null;
        }
    }

    private void DefinirAlvosModo(List<GameObject> selecionadosSnapshot)
    {
        _alvosModo.Clear();

        if (selecionadosSnapshot != null)
        {
            for (int i = 0; i < selecionadosSnapshot.Count; i++)
            {
                GameObject alvo = selecionadosSnapshot[i];
                if (alvo != null)
                {
                    _alvosModo.Add(alvo);
                }
            }

            return;
        }

        if (gerenteSelecao == null)
        {
            gerenteSelecao = FindFirstObjectByType<GerenteSelecao>();
        }

        if (gerenteSelecao == null)
        {
            return;
        }

        for (int i = 0; i < gerenteSelecao.unidadesSelecionadas.Count; i++)
        {
            ControleUnidade unidade = gerenteSelecao.unidadesSelecionadas[i];
            if (unidade != null)
            {
                _alvosModo.Add(unidade.gameObject);
            }
        }
    }

    private bool ValidarAlvosModo()
    {
        for (int i = _alvosModo.Count - 1; i >= 0; i--)
        {
            if (_alvosModo[i] == null)
            {
                _alvosModo.RemoveAt(i);
            }
        }

        return _alvosModo.Count > 0;
    }

    private Transform ObterTransformOrigemLinha()
    {
        for (int i = 0; i < _alvosModo.Count; i++)
        {
            GameObject alvo = _alvosModo[i];
            if (alvo != null)
            {
                return alvo.transform;
            }
        }

        return null;
    }

    // ── MÉTODOS AUXILIARES CHAMADOS PELO MENU DE COMANDO ─────────────────────
    public void AdicionarPontoPatrulhaDoMenu(Vector3 ponto)
    {
        if (!modoPatrulhaAtivo) return;
        pontosPatrulha.Add(ponto);
        GarantirLineRenderer();
        lineRenderer.positionCount = pontosPatrulha.Count;
        lineRenderer.SetPosition(pontosPatrulha.Count - 1, ponto);
        
        // Marcador visual
        MostrarMarcadorPatrulha(ponto, new Color(0.15f, 0.65f, 1f, 0.95f), 12f, 2.5f);
        Debug.Log("[ModoPatrulha] Ponto adicionado via menu: " + ponto);
    }

    public void ConfirmarPatrulhaDoMenu()
    {
        if (!modoPatrulhaAtivo) return;
        if (pontosPatrulha.Count >= 1)
        {
            AplicarOrdemPatrulha();
            DiagnosticoDesempenhoJogo.RegistrarEvento("InputMode", "Patrulha confirmada via menu.");
            LimparTudo();
        }
    }

    public bool AplicarOrdemSeguirDoMenu(Transform alvo)
    {
        return AplicarOrdemSeguirDoMenu(alvo, -1f);
    }

    public bool AplicarOrdemSeguirDoMenu(Transform alvo, float distanciaSeguimento)
    {
        if (!modoSeguirAtivo) return false;
        bool aplicado = AplicarOrdemSeguir(alvo, distanciaSeguimento);
        LimparTudo();
        return aplicado;
    }

    public void AplicarOrdemAtaqueDoMenu(Vector3 pontoAlvo, Transform transformAlvo)
    {
        if (!modoAtaqueAtivo) return;
        AplicarOrdemAtaque(pontoAlvo, transformAlvo);
        LimparTudo();
    }

    /// <summary>Cancela o modo patrulha ou seguir sem confirmar a ordem. Chamado pelo clique esquerdo.</summary>
    public void CancelarModo()
    {
        LimparTudo();
    }

    void MostrarMarcadorPatrulha(Vector3 pontoPatrulha)
    {
        // Cor Amarela/Dourada padrão para patrulha
        MostrarMarcadorPatrulha(pontoPatrulha, new Color(1f, 0.85f, 0f, 0.95f), 11f, 3f);
    }

    void MostrarMarcadorPatrulha(Vector3 pontoPatrulha, Color cor, float escala, float tempoVida)
    {
        // Prioridade 1: prefab dedicado neste componente
        if (prefabMarcadorPatrulha != null)
        {
            GameObject marcador = Instantiate(prefabMarcadorPatrulha, pontoPatrulha + Vector3.up * 0.1f, Quaternion.identity);
            marcador.transform.localScale = new Vector3(escala, escala, escala);
            // REMOVIDO: AplicarCorMarcador - Mantém as cores originais da animação amarela
            Destroy(marcador, tempoVida);
            return;
        }

        // Prioridade 2: prefab de patrulha configurado no GerenteSelecao
        if (gerenteSelecao != null && gerenteSelecao.prefabMarcadorPatrulha != null)
        {
            GameObject marcador = Instantiate(gerenteSelecao.prefabMarcadorPatrulha, pontoPatrulha + Vector3.up * 0.1f, Quaternion.identity);
            marcador.transform.localScale = new Vector3(escala * 1.5f, escala * 1.5f, escala * 1.5f);
            // REMOVIDO: AplicarCorMarcador - Mantém as cores originais da animação amarela
            Destroy(marcador, tempoVida);
            return;
        }

        // Prioridade 3: fallback geométrico (aqui sim pintamos de amarelo via script)
        CriarMarcadorFallback(pontoPatrulha, cor, escala, tempoVida);
    }

    void CriarMarcadorFallback(Vector3 pontoPatrulha, Color cor, float escala, float tempoVida)
    {
        GameObject raiz = new GameObject("MarcadorOrdemFallback");
        raiz.transform.position = pontoPatrulha + Vector3.up * 0.12f;

        GameObject disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disco.name = "Disco";
        disco.transform.SetParent(raiz.transform, false);
        disco.transform.localScale = new Vector3(escala * 0.09f, 0.03f, escala * 0.09f);
        Destroy(disco.GetComponent<Collider>());

        GameObject farol = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        farol.name = "Farol";
        farol.transform.SetParent(raiz.transform, false);
        farol.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        farol.transform.localScale = Vector3.one * Mathf.Max(0.6f, escala * 0.08f);
        Destroy(farol.GetComponent<Collider>());

        AplicarCorMarcador(raiz, cor);
        Destroy(raiz, tempoVida);
    }

    void AplicarCorMarcador(GameObject marcador, Color cor)
    {
        if (marcador == null)
        {
            return;
        }

        Renderer[] renderers = marcador.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendererAtual = renderers[i];
            if (rendererAtual == null)
            {
                continue;
            }

            // Usando Sprites/Default assegura que não fica com shader de erro rosa independente do URP
            Material materialInstancia = new Material(Shader.Find("Sprites/Default"));
            materialInstancia.color = cor;
            rendererAtual.material = materialInstancia;
            rendererAtual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    bool TryObterPontoPatrulha(out Vector3 ponto)
    {
        ponto = Vector3.zero;

        if (Camera.main == null)
        {
            return false;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            ponto = hit.point;
            return true;
        }

        if (!SelecaoContemUnidadeNaval())
        {
            return false;
        }

        UnityEngine.Plane planoAgua = new UnityEngine.Plane(Vector3.up, Vector3.zero);
        float distanciaPlano;
        if (!planoAgua.Raycast(ray, out distanciaPlano))
        {
            return false;
        }

        ponto = ray.GetPoint(distanciaPlano);
        return true;
    }

    bool SelecaoContemUnidadeNaval()
    {
        for (int i = 0; i < _alvosModo.Count; i++)
        {
            GameObject alvo = _alvosModo[i];
            if (alvo == null)
            {
                continue;
            }

            ControleUnidade unidade = alvo.GetComponent<ControleUnidade>();
            if (unidade != null && unidade.EhUnidadeNaval())
            {
                return true;
            }
        }

        if (gerenteSelecao != null)
        {
            for (int i = 0; i < gerenteSelecao.unidadesSelecionadas.Count; i++)
            {
                ControleUnidade unidade = gerenteSelecao.unidadesSelecionadas[i];
                if (unidade != null && unidade.EhUnidadeNaval())
                {
                    return true;
                }
            }
        }

        return false;
    }

    void AtualizarLinhaVisualPatrulha()
    {
        if (pontosPatrulha.Count == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        Transform origem = ObterTransformOrigemLinha();
        if (origem == null)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // Exibe loop completo: unidade → p0 → p1 → ... → pN → unidade (fecha o ciclo)
        const float alturaLinha = 3f;
        Vector3 posInicial = origem.position;
        posInicial.y += alturaLinha;

        int totalPontos = pontosPatrulha.Count + 2; // +1 início, +1 fecha o loop
        lineRenderer.positionCount = totalPontos;
        lineRenderer.SetPosition(0, posInicial);

        for (int i = 0; i < pontosPatrulha.Count; i++)
        {
            Vector3 p = pontosPatrulha[i];
            p.y += alturaLinha;
            lineRenderer.SetPosition(i + 1, p);
        }

        // Fecha o loop voltando ao ponto inicial (cria o circuito de patrulha visualmente)
        lineRenderer.SetPosition(totalPontos - 1, posInicial);
    }

    void AplicarOrdemPatrulha()
    {
        for (int i = 0; i < _alvosModo.Count; i++)
        {
            GameObject alvo = _alvosModo[i];
            if (alvo == null)
            {
                continue;
            }

            ControleUnidade unidade = alvo.GetComponent<ControleUnidade>();
            if (unidade != null)
            {
                unidade.EmitirOrdemPatrulha(pontosPatrulha);
                continue;
            }

            Helicoptero helicoptero = alvo.GetComponent<Helicoptero>();
            if (helicoptero != null)
            {
                helicoptero.IniciarPatrulhaAeroporto(new List<Vector3>(pontosPatrulha));
            }
        }
    }

    // ── ATAQUE ───────────────────────────────────────────────────────────────
    void AplicarOrdemAtaque(Vector3 pontoAlvo, Transform transformAlvo)
    {
        bool ataqueEmAlvoEspecifico = transformAlvo != null;

        for (int i = 0; i < _alvosModo.Count; i++)
        {
            GameObject alvo = _alvosModo[i];
            if (alvo == null) continue;

            // Helicóptero: voa até o alvo e atira automaticamente
            Helicoptero helicoptero = alvo.GetComponent<Helicoptero>();
            if (helicoptero != null)
            {
                ControleUnidade controleHelicoptero = alvo.GetComponent<ControleUnidade>();
                helicoptero.modoCombateAtivo = true;
                if (controleHelicoptero != null)
                {
                    controleHelicoptero.DefinirModoCombate(true);
                    controleHelicoptero.DefinirAlvoPrioritario(transformAlvo);
                }

                helicoptero.OrdenarAtaque(transformAlvo, pontoAlvo);
                continue;
            }

            // Avião bombardeiro: define alvo de solo
            AviaoBombardeiro bombardeiro = alvo.GetComponent<AviaoBombardeiro>();
            if (bombardeiro != null)
            {
                bombardeiro.modoDeAtaque = AviaoBombardeiro.ModoAtaque.AtaqueAoSolo;
                bombardeiro.alvoAreaSolo = pontoAlvo;
                ControleAviao controleAviao = alvo.GetComponent<ControleAviao>();
                if (controleAviao != null)
                {
                    controleAviao.alvoPrioritarioIA = ataqueEmAlvoEspecifico;
                    controleAviao.alvoEstrategico = ataqueEmAlvoEspecifico ? transformAlvo.position : pontoAlvo;
                    if (controleAviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                    {
                        controleAviao.IniciarMissaoCompleta(pontoAlvo);
                    }
                    else if (controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
                    {
                        controleAviao.alvoGPSVoo = pontoAlvo;
                        controleAviao.centroDaPatrulha = pontoAlvo;
                        controleAviao.ordemParaRetorno = false;
                    }
                }
                continue;
            }

            // Navio realista: avança para um ponto lateral na agua e dispara torpedos sem ir de frente
            ControleNavioRealista navioRealista = alvo.GetComponent<ControleNavioRealista>();
            if (navioRealista != null)
            {
                Vector3 referenciaNaval = transformAlvo != null ? transformAlvo.position : pontoAlvo;
                navioRealista.DefinirDestinoAtaqueLateral(referenciaNaval);
                continue;
            }

            // Qualquer unidade com ControleUnidade: move para o ponto de ataque
            ControleUnidade unidade = alvo.GetComponent<ControleUnidade>();
            if (unidade != null)
            {
                ControleAviao aviao = alvo.GetComponent<ControleAviao>();
                if (aviao != null)
                {
                    aviao.alvoPrioritarioIA = ataqueEmAlvoEspecifico;
                    unidade.DefinirModoCombate(true);
                    if (aviao.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio)
                    {
                        aviao.IniciarMissaoCompleta(pontoAlvo);
                    }
                    else if (aviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
                    {
                        aviao.alvoGPSVoo = pontoAlvo;
                        aviao.centroDaPatrulha = pontoAlvo;
                        aviao.alvoEstrategico = ataqueEmAlvoEspecifico ? transformAlvo.position : pontoAlvo;
                        aviao.ordemParaRetorno = false;
                    }
                    continue;
                }

                // Se tem alvo específico, força modo combate ativo e segue
                if (transformAlvo != null)
                {
                    unidade.DefinirModoCombate(true);
                    unidade.DefinirAlvoPrioritario(transformAlvo);
                    // Move para perto do alvo para atacar dentro do alcance
                    Vector3 direcaoAlvo = (pontoAlvo - unidade.transform.position).normalized;
                    float distanciaAtaque = 60f; // recua um pouco para estar em alcance
                    Vector3 posicaoAtaque = pontoAlvo - direcaoAlvo * distanciaAtaque;
                    unidade.EmitirOrdemMover(posicaoAtaque);
                }
                else
                {
                    // Ataque de área: move até o ponto diretamente
                    unidade.DefinirModoCombate(true);
                    unidade.EmitirOrdemMover(pontoAlvo);
                }
                continue;
            }
        }
    }

    bool AplicarOrdemSeguir(Transform alvo, float distanciaSeguimento = -1f)
    {
        if (distanciaSeguimento <= 0f)
        {
            distanciaSeguimento = distanciaSeguimentoPadrao;
        }

        ControleUnidade unidadeAlvo = alvo != null ? alvo.GetComponent<ControleUnidade>() : null;
        bool ordemEmitida = false;
        for (int i = 0; i < _alvosModo.Count; i++)
        {
            GameObject candidato = _alvosModo[i];
            if (candidato == null)
            {
                continue;
            }

            ControleUnidade unidade = candidato.GetComponent<ControleUnidade>();
            if (unidade == null || unidade.transform == alvo)
            {
                continue;
            }

            if (unidadeAlvo != null && !PodeSeguirAlvo(unidade, unidadeAlvo))
            {
                Debug.LogWarning($"[{unidade.name}] ignorou SEGUIR porque o alvo nao e aliado.");
                continue;
            }

            ordemEmitida |= unidade.EmitirOrdemSeguir(alvo, distanciaSeguimento);
        }

        return ordemEmitida;
    }

    bool PodeSeguirAlvo(ControleUnidade seguidor, ControleUnidade alvo)
    {
        if (seguidor == null || alvo == null)
        {
            return false;
        }

        IdentidadeUnidade idSeguidor = seguidor.GetComponent<IdentidadeUnidade>();
        IdentidadeUnidade idAlvo = alvo.GetComponent<IdentidadeUnidade>();
        if (idSeguidor == null || idAlvo == null)
        {
            return true;
        }

        return idSeguidor.teamID == idAlvo.teamID;
    }

}

public class ComportamentoPatrulhaUniversal : MonoBehaviour
{
    [SerializeField] private List<Vector3> pontos;
    [SerializeField] private int indiceAtual = 0;
    [SerializeField] private int indiceDesignado = -1;
    private float tempoUltimoComando = 0f;
    private ControleUnidade controle;
    private NavMeshAgent agente;
    private ControleNavioRealista navioRealista;
    private bool ehNaval;
    private bool ehAereo;

    public int IndiceAtual => indiceAtual;

    public IReadOnlyList<Vector3> ObterPontos()
    {
        return pontos ?? (IReadOnlyList<Vector3>)System.Array.Empty<Vector3>();
    }

    public void DefinirIndiceAtual(int indice)
    {
        if (pontos == null || pontos.Count == 0)
        {
            indiceAtual = 0;
            indiceDesignado = -1;
            return;
        }

        indiceAtual = Mathf.Clamp(indice, 0, pontos.Count - 1);
        indiceDesignado = -1;
    }

    public void Configurar(List<Vector3> novosPontos)
    {
        pontos = new List<Vector3>(novosPontos);
        indiceAtual = 0;
        indiceDesignado = -1;
        controle = GetComponent<ControleUnidade>();
        agente = GetComponent<NavMeshAgent>();
        navioRealista = GetComponent<ControleNavioRealista>();
        ehNaval = controle != null && controle.EhUnidadeNaval();
        ehAereo = controle != null && controle.DominioAtual == DominioControleUnidade.Aereo;
    }

    private void OnEnable()
    {
        if (controle == null) controle = GetComponent<ControleUnidade>();
        if (agente == null) agente = GetComponent<NavMeshAgent>();
        if (navioRealista == null) navioRealista = GetComponent<ControleNavioRealista>();
        ehNaval = controle != null && controle.EhUnidadeNaval();
        ehAereo = controle != null && controle.DominioAtual == DominioControleUnidade.Aereo;
    }

    void Update()
    {
        if (pontos == null || pontos.Count == 0 || controle == null)
        {
            return;
        }

        if (controle.OrdemAtual != OrdemControleUnidade.Patrulhando)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        Vector3 alvo = pontos[indiceAtual];
        
        // Verifica distância no plano 2D para evitar que subidas curtas façam o carro engasgar
        Vector3 posPlano = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 alvoPlano = new Vector3(alvo.x, 0, alvo.z);
        float distancia = Vector3.Distance(posPlano, alvoPlano);
        
        // Margem de chegada: navios com ControleNavioRealista usam distanciaChegada própria
        float margem;
        if (ehAereo)
        {
            margem = 45f;
        }
        else if (ehNaval && navioRealista != null)
        {
            margem = navioRealista.distanciaChegada + 5f;
        }
        else if (ehNaval)
        {
            margem = 25f;
        }
        else if (agente != null && agente.enabled && !agente.updatePosition)
        {
            margem = 15f;
        }
        else
        {
            margem = 6f;
        }

        if (distancia < margem)
        {
            indiceAtual++;
            if (indiceAtual >= pontos.Count)
            {
                indiceAtual = 0;
            }
            // Reseta para forçar emissão imediata pro novo ponto
            indiceDesignado = -1;
            return;
        }

        // Guarda Global: Se o destino já foi designado e a unidade já está navegando para ele,
        // NÃO re-emite o comando (isso evita o "hiccup" de recalcular path a cada 3 segundos).
        bool agenteTemRotaAtiva = ehNaval && navioRealista != null
            ? navioRealista.TemDestinoAtivo
            : agente != null && agente.enabled && agente.isOnNavMesh
              && (agente.hasPath || agente.pathPending);
        
        if (indiceDesignado == indiceAtual && agenteTemRotaAtiva)
        {
            return; 
        }

        // Intervalo de segurança (caso a unidade se perca ou pare por colisão externa)
        float intervaloSeguranca = ehNaval ? 1.5f : 2f;


        if (indiceDesignado != indiceAtual || !agenteTemRotaAtiva
            || Time.time - tempoUltimoComando > intervaloSeguranca)
        {
            tempoUltimoComando = Time.time;
            indiceDesignado = controle.EmitirOrdemMover(alvo, false) ? indiceAtual : -1;
        }
    }
}

public class ComportamentoSeguirUniversal : MonoBehaviour
{
    private Transform alvoSeguido;
    private ControleUnidade controle;
    private float distanciaIdeal = 45f;
    private float tempoProximaAtualizacao = 0f;
    private bool ehNaval;
    private bool ehAereo;
    private float intervaloAtualizacao = 0.5f;
    private float offsetLateralNaval = 0f;

    public Transform AlvoSeguido => alvoSeguido;

    public void Configurar(Transform novoAlvo, float distanciaDesejada = -1f)
    {
        alvoSeguido = novoAlvo;
        controle = GetComponent<ControleUnidade>();
        ehNaval = controle != null && controle.EhUnidadeNaval();
        ehAereo = controle != null && controle.DominioAtual == DominioControleUnidade.Aereo;
        distanciaIdeal = distanciaDesejada > 0f
            ? Mathf.Clamp(distanciaDesejada, 25f, 10000f)
            : (ehNaval ? 170f : (ehAereo ? (controle != null && controle.TemHelicopteroExterno ? 60f : 140f) : 45f));
        intervaloAtualizacao = ehNaval ? 0.2f : (ehAereo ? 0.25f : 0.5f);
        offsetLateralNaval = ehNaval ? (((GetInstanceID() & 1) == 0) ? 50f : -50f) : 0f;
    }

    void Update()
    {
        if (alvoSeguido == null || controle == null)
        {
            Destroy(this);
            return;
        }

        if (controle.OrdemAtual != OrdemControleUnidade.Seguindo)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        if (Time.time <= tempoProximaAtualizacao)
        {
            return;
        }

        if (ehNaval)
        {
            AtualizarSeguimentoNaval();
        }
        else if (ehAereo)
        {
            AtualizarSeguimentoAereo();
        }
        else
        {
            AtualizarSeguimentoPadrao();
        }

        tempoProximaAtualizacao = Time.time + intervaloAtualizacao;
    }

    void OnDestroy()
    {
        if (controle != null)
        {
            controle.RestaurarVelocidadeOriginal();
        }
    }

    void AtualizarSeguimentoPadrao()
    {
        Vector3 destinoEscolta = CalcularDestinoPadrao();
        float distanciaDestino = Vector3.Distance(transform.position, destinoEscolta);

        if (distanciaDestino > 15f)
        {
            controle.EmitirOrdemMover(destinoEscolta, false);
            AjustarVelocidadeSeguirBase();
        }
        else
        {
            AjustarVelocidadeSeguirBase();
        }
    }

    void AtualizarSeguimentoAereo()
    {
        ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
        float velocidadeLider = controleLider != null ? controleLider.ObterVelocidadeAtualReal() : 0f;

        Vector3 frenteLider = Flatten(alvoSeguido.forward);
        if (frenteLider.sqrMagnitude < 0.01f)
        {
            frenteLider = Flatten(alvoSeguido.position - transform.position);
            if (frenteLider.sqrMagnitude < 0.01f)
            {
                frenteLider = Vector3.forward;
            }
        }
        frenteLider.Normalize();

        float offsetLateral = controle != null && controle.TemHelicopteroExterno ? 18f : 55f;
        if ((GetInstanceID() & 1) != 0)
        {
            offsetLateral *= -1f;
        }

        float altitudeOffset = controle != null && controle.TemHelicopteroExterno ? 18f : 70f;
        Vector3 direitaLider = new Vector3(frenteLider.z, 0f, -frenteLider.x);
        Vector3 destinoEscolta = alvoSeguido.position - (frenteLider * distanciaIdeal) + (direitaLider * offsetLateral);
        destinoEscolta.y = Mathf.Max(alvoSeguido.position.y + altitudeOffset, transform.position.y);

        float distanciaAtual = Vector3.Distance(transform.position, destinoEscolta);
        if (distanciaAtual > 18f)
        {
            controle.EmitirOrdemMover(destinoEscolta, false);
        }

        if (controleLider != null)
        {
            if (velocidadeLider > 0.5f)
            {
                float multiplicador = controle.TemHelicopteroExterno ? 1.05f : 1.1f;
                controle.AplicarLimiteVelocidade(Mathf.Max(velocidadeLider * multiplicador, 3f));
            }
            else if (controle.TemHelicopteroExterno)
            {
                controle.AplicarLimiteVelocidade(3f);
            }
            else
            {
                controle.RestaurarVelocidadeOriginal();
            }
        }
    }

    void AtualizarSeguimentoNaval()
    {
        ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
        float velocidadeLider = controleLider != null ? controleLider.ObterVelocidadeAtualReal() : 0f;

        Vector3 frenteLider = alvoSeguido.forward;
        frenteLider.y = 0f;
        if (frenteLider.sqrMagnitude < 0.01f)
        {
            frenteLider = Flatten(alvoSeguido.position - transform.position);
            if (frenteLider.sqrMagnitude < 0.01f)
            {
                frenteLider = Vector3.forward;
            }
        }
        frenteLider.Normalize();

        Vector3 direitaLider = new Vector3(frenteLider.z, 0f, -frenteLider.x);
        Vector3 destinoEscolta = alvoSeguido.position - (frenteLider * distanciaIdeal) + (direitaLider * offsetLateralNaval);

        float distanciaLider = PlanarDistance(transform.position, alvoSeguido.position);
        float distanciaDestino = PlanarDistance(transform.position, destinoEscolta);
        bool estaNaFrente = Vector3.Dot(Flatten(transform.position - alvoSeguido.position), frenteLider) > 0f;

        if (estaNaFrente || distanciaLider < 75f)
        {
            float velocidadeFreio = velocidadeLider > 0.5f ? Mathf.Max(0.5f, velocidadeLider * 0.65f) : 0.1f;
            controle.AplicarLimiteVelocidade(velocidadeFreio);
            controle.EmitirOrdemMover(destinoEscolta, false);
            return;
        }

        if (distanciaDestino > 18f)
        {
            controle.EmitirOrdemMover(destinoEscolta, false);

            if (distanciaLider > 140f)
            {
                controle.RestaurarVelocidadeOriginal();
                return;
            }

            if (velocidadeLider > 0.5f)
            {
                float velocidadeEscolta = distanciaLider > 95f ? velocidadeLider * 1.05f : velocidadeLider;
                controle.AplicarLimiteVelocidade(velocidadeEscolta);
            }
            else
            {
                controle.AplicarLimiteVelocidade(Mathf.Clamp(distanciaDestino * 0.08f, 1.5f, 6f));
            }
            return;
        }

        if (velocidadeLider > 0.5f)
        {
            controle.AplicarLimiteVelocidade(velocidadeLider);
            controle.EmitirOrdemMover(destinoEscolta, false);
        }
        else
        {
            controle.AplicarLimiteVelocidade(0.1f);
            controle.EmitirOrdemMover(transform.position, false);
        }
    }

    private Vector3 CalcularDestinoPadrao()
    {
        Vector3 frente = Flatten(alvoSeguido.forward);
        if (frente.sqrMagnitude < 0.01f)
        {
            frente = Flatten(transform.position - alvoSeguido.position);
        }

        if (frente.sqrMagnitude < 0.01f)
        {
            frente = Vector3.forward;
        }

        frente.Normalize();
        return alvoSeguido.position - frente * distanciaIdeal;
    }

    private void AjustarVelocidadeSeguirBase()
    {
        ControleUnidade controleLider = alvoSeguido.GetComponent<ControleUnidade>();
        if (controleLider != null)
        {
            float velLider = controleLider.ObterVelocidadeAtualReal();
            if (velLider > 0.5f)
            {
                controle.AplicarLimiteVelocidade(velLider * 1.1f);
            }
            else
            {
                controle.RestaurarVelocidadeOriginal();
            }
        }
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(Flatten(a), Flatten(b));
    }

    static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
