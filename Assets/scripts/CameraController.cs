using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraController : MonoBehaviour
{
    public static event Action<Vector3> CameraMudouArea;

    public float velocidade = 20f;
    public float velocidadeMenu = 5f;
    public float velocidadeZoom = 4000f;
    public float velocidadeRotacao = 100f;
    public float multiplicadorShift = 9.69f; // Velocidade triplicada (Antes 3.23)
    [Header("Visão da Câmera")]
    public float campoDeVisaoBase = 75f;
    public float campoDeVisaoMin = 65f;
    public float campoDeVisaoMax = 85f;
    public float alturaMinParaFov = 2f;
    public float alturaMaxParaFov = 8000f;

    [Header("Distância de renderização do mapa")]
    [Tooltip("Limite inferior evita que o terreno e a costa desapareçam na build.")]
    public float distanciaMinimaRender = 2500f;
    [Tooltip("Limite superior cobre o mapa grande sem deixar o recorte crescer indefinidamente.")]
    public float distanciaMaximaRender = 14000f;
    public float multiplicadorDistanciaRender = 6f;

    private float tempoShiftPressionado = 0f;
    private GerenteSelecao gerenteSelecaoCache;
    private float proximaBuscaGerenteSelecao = 0f;
    private Camera cameraPrincipal;
    private Vector3 ultimaAreaNotificada;
    private bool projecaoInicializada;
    private const float DistanciaMinimaNotificacaoSqr = 625f;
    private const float AlturaMinimaNotificacao = 5f;

    void Start()
    {
        cameraPrincipal = GetComponent<Camera>();
        multiplicadorShift = Mathf.Max(12f, multiplicadorShift);
        if (cameraPrincipal != null)
        {
            cameraPrincipal.fieldOfView = campoDeVisaoBase;
            // O mapa tem ilhas além do primeiro Terrain. Inicializar o recorte
            // aqui evita que a câmera comece vendo água e céu antes do primeiro
            // zoom/movimento do jogador.
            AtualizarProjecao(transform.position.y);
        }

        ultimaAreaNotificada = transform.position;
        CameraMudouArea?.Invoke(ultimaAreaNotificada);
    }

    void Update()
    {
        // Bloqueia movimento se estivermos digitando no menu
        if (UnityEngine.EventSystems.EventSystem.current != null 
            && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null
            && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>() != null)
        {
            return;
        }

        // Na cena de menu, a câmera fica parada para não atravessar o solo
        if (SceneManager.GetActiveScene().name == "Menu cena")
        {
            return;
        }
        
        bool menusAbertos = MenuConstrucao.EstaAberto || MenuPier.EstaAberto || Fazenda.QualquerFazendaAberta || (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto);

        long cameraTimingStart = DiagnosticoDesempenhoJogo.CapturaAtiva
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;

        // Força a substituição do Inspector se estiver salvo um valor muito baixo
        // O valor Ã© normalizado no Start/OnValidate para nÃ£o repetir esta escrita por frame.

        // --- 1. Controle de Velocidade (Speed Shift) ---
        float velAtual = velocidade;
        
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            tempoShiftPressionado += Time.deltaTime; // Conta tempo
            
            float multi = multiplicadorShift;
            // Se segurar por mais de 10 segundos, triplica a velocidade (Turbo Boost)
            if (tempoShiftPressionado > 10f)
            {
                multi *= 3f;
            }
            
            velAtual *= multi;
        }
        else
        {
            tempoShiftPressionado = 0f; // Reseta se soltar
        }

        Vector3 pos = transform.position;

        // --- 2. Movimento (W, A, S, D) Relativo à Câmera ---
        // Pegamos a direção "frente" e "direita" da câmera, mas zeramos o Y para não voar para o chão/céu
        bool moverW = !menusAbertos && Input.GetKey(KeyCode.W);
        bool moverS = !menusAbertos && Input.GetKey(KeyCode.S);
        bool moverD = !menusAbertos && Input.GetKey(KeyCode.D);
        bool moverA = !menusAbertos && Input.GetKey(KeyCode.A);
        bool moverCamera = moverW || moverS || moverD || moverA;
        if (moverCamera)
        {
            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = transform.right;
            right.y = 0;
            right.Normalize();

            if (moverW) pos += forward * velAtual * Time.deltaTime;
            if (moverS) pos -= forward * velAtual * Time.deltaTime;
            if (moverD) pos += right * velAtual * Time.deltaTime;
            if (moverA) pos -= right * velAtual * Time.deltaTime;
        }

        // --- 3. Zoom (Rodinha do Mouse e Teclado) ---
        float zoomInput = 0f;
        
        // Bloqueia Zoom se estiver sobre UI ou com Menus Abertos
        if (!menusAbertos)
        {
            UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
            bool mouseEmCimaDeUI = eventSystem != null && eventSystem.IsPointerOverGameObject();
            if (!mouseEmCimaDeUI)
            {
                zoomInput = Input.GetAxis("Mouse ScrollWheel");
            }
        }

        if (!menusAbertos)
        {
            // Teclas + e - (Teclado) com atalhos espelhados em Espaço/Ctrl.
            if (Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.Equals))
            {
                zoomInput += 0.03f; // Desce a camera
            }
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                zoomInput += 0.15f; // Ctrl desce MUITO mais rápido agora (Antes 0.03)
            }
            if (Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus))
            {
                zoomInput -= 0.08f; // Sobe a camera mais rápido (Antes 0.03)
            }
            if (Input.GetKey(KeyCode.Space))
            {
                zoomInput -= 0.15f; // Espaço sobe MUITO mais rápido agora (Antes 0.06)
            }
        }

        pos.y -= zoomInput * velocidadeZoom * Time.deltaTime;
        pos.y = Mathf.Clamp(pos.y, 2f, 8000f); // Teto aumentado para o atalho do Espaço

        float alturaAnterior = transform.position.y;
        bool posicaoMudou = pos != transform.position;
        if (posicaoMudou)
        {
            transform.position = pos;
            NotificarMudancaDeArea(pos);
        }

        if (cameraPrincipal != null
            && (!projecaoInicializada || (posicaoMudou && Mathf.Abs(pos.y - alturaAnterior) > 0.01f)))
        {
            AtualizarProjecao(pos.y);
            /*
            float tAltura = Mathf.InverseLerp(alturaMinParaFov, alturaMaxParaFov, pos.y);
            cameraPrincipal.fieldOfView = Mathf.Lerp(campoDeVisaoMin, campoDeVisaoMax, tAltura);
            
            // O mapa é maior que o valor padrão de 1000/1500 salvo na cena. Em
            // builds isso cortava terreno, costa e unidades à distância.
            float distanciaMinima = Mathf.Max(1000f, distanciaMinimaRender);
            float distanciaMaxima = Mathf.Max(distanciaMinima, distanciaMaximaRender);
            float multiplicador = Mathf.Max(1f, multiplicadorDistanciaRender);
            cameraPrincipal.farClipPlane = Mathf.Clamp(
                pos.y * multiplicador,
                distanciaMinima,
                distanciaMaxima);
            */
        }

        // --- 4. Rotação e Inclinação (Botão Direito, Meio ou Teclas Q/E) ---
        // --- 4. Rotação e Inclinação (Botão Direito, Meio ou Teclas Q/E) ---
        bool podeRotacionar = !menusAbertos;
        InteractionModeSnapshot snapshotInteracao = InteractionModeService.CurrentSnapshot();
        if (snapshotInteracao.Policy.bloqueiaRotacaoCamera)
        {
            podeRotacionar = false;
        }

        if (podeRotacionar)
        {
            if (!BloquearRotacaoPorMiraManual() && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
            {
                // Mouse X gira a câmera no eixo Y global (olhar para lados)
                float rotX = Input.GetAxis("Mouse X") * velocidadeRotacao * Time.deltaTime * 2f; // *2f para sensibilidade
                transform.Rotate(Vector3.up, rotX, Space.World);

                // Mouse Y inclina a câmera (olhar para cima/baixo)
                float rotY = Input.GetAxis("Mouse Y") * velocidadeRotacao * Time.deltaTime * 2f;
                // Inverter rotY se quiser "inverter eixo Y"
                transform.Rotate(Vector3.left, rotY, Space.Self);
            }
            else
            {
                // Teclas para rotacionar apenas no eixo Y
                if (Input.GetKey(KeyCode.Q)) transform.Rotate(Vector3.up, -velocidadeRotacao * Time.deltaTime, Space.World);
                if (Input.GetKey(KeyCode.E)) transform.Rotate(Vector3.up, velocidadeRotacao * Time.deltaTime, Space.World);
            }
        }

        if (cameraTimingStart != 0L)
        {
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - cameraTimingStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("camera_update_ms", elapsedMs);
        }
    }

    private void AtualizarProjecao(float altura)
    {
        if (cameraPrincipal == null)
        {
            return;
        }

        float tAltura = Mathf.InverseLerp(alturaMinParaFov, alturaMaxParaFov, altura);
        cameraPrincipal.fieldOfView = Mathf.Lerp(campoDeVisaoMin, campoDeVisaoMax, tAltura);

        float distanciaMinima = Mathf.Max(1000f, distanciaMinimaRender);
        float distanciaMaximaConfigurada = Mathf.Max(distanciaMinima, distanciaMaximaRender);
        float multiplicador = Mathf.Max(1f, multiplicadorDistanciaRender);
        float distanciaPorAltura = altura * multiplicador;
        float distanciaDasSuperficies = CalcularRecorteDasSuperficies();
        // A configuração histórica de 14 km não pode cortar um tile ativo.
        // O recorte continua limitado pelo mapa real, sem alterar a posição
        // da câmera ou a estratégia de navegação.
        float distanciaMaxima = Mathf.Max(distanciaMaximaConfigurada, distanciaDasSuperficies);
        cameraPrincipal.farClipPlane = Mathf.Clamp(
            Mathf.Max(distanciaPorAltura, distanciaDasSuperficies),
            distanciaMinima,
            distanciaMaxima);
        projecaoInicializada = true;
    }

    private float CalcularRecorteDasSuperficies()
    {
        if (cameraPrincipal == null)
        {
            return 0f;
        }

        float necessario = 0f;
        Terrain[] terrenos = FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Terrain terreno in terrenos)
        {
            // Todos os terrenos ativos da cena canônica fazem parte do mapa
            // visível. O inicializador de superfícies mantém inclusive os
            // terrenos auxiliares disponíveis para renderização; ignorá-los
            // aqui reduz o far clip e faz a área da direita desaparecer.
            if (terreno == null || !terreno.gameObject.activeInHierarchy || !terreno.enabled)
            {
                continue;
            }

            TerrainData dados = terreno.terrainData;
            if (dados == null)
            {
                continue;
            }

            Vector3 escala = terreno.transform.lossyScale;
            Vector3 tamanho = Vector3.Scale(dados.size, new Vector3(Mathf.Abs(escala.x), Mathf.Abs(escala.y), Mathf.Abs(escala.z)));
            Vector3 centro = terreno.GetPosition() + tamanho * 0.5f;
            float raio = tamanho.magnitude * 0.5f;
            necessario = Mathf.Max(necessario, Vector3.Distance(cameraPrincipal.transform.position, centro) + raio + 500f);
        }

        return necessario;
    }

    private static bool EhTerrenoAuxiliarInimigo(Terrain terreno)
    {
        string nome = terreno != null ? terreno.gameObject.name.ToLowerInvariant() : string.Empty;
        return nome.Contains("mapa inimigo") || nome.Contains("mapa_inimigo") || nome.Contains("enemy map");
    }

    private void OnValidate()
    {
        multiplicadorShift = Mathf.Max(12f, multiplicadorShift);
    }

    private void NotificarMudancaDeArea(Vector3 posicao)
    {
        Vector2 deslocamentoPlano = new Vector2(posicao.x - ultimaAreaNotificada.x, posicao.z - ultimaAreaNotificada.z);
        if (deslocamentoPlano.sqrMagnitude < DistanciaMinimaNotificacaoSqr
            && Mathf.Abs(posicao.y - ultimaAreaNotificada.y) < AlturaMinimaNotificacao)
        {
            return;
        }

        ultimaAreaNotificada = posicao;
        long notifyStart = DiagnosticoDesempenhoJogo.CapturaAtiva
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        CameraMudouArea?.Invoke(posicao);
        if (notifyStart != 0L)
        {
            float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - notifyStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            DiagnosticoDesempenhoJogo.RegistrarMetricaTempo("camera_area_notify_ms", elapsedMs);
            DiagnosticoDesempenhoJogo.IncrementarContadorMetrica("camera_area_notifications");
        }
    }

    /// <summary>
    /// Centraliza a camera principal em um alvo selecionado pelo menu de
    /// seguimento. Mantem a altura atual e ajusta a mira para o objeto, sem
    /// depender da camera HUD/render texture.
    /// </summary>
    public void FocarEm(Vector3 alvo, bool mirarNoAlvo = true)
    {
        Vector3 posicao = transform.position;
        posicao.x = alvo.x;
        posicao.z = alvo.z;
        transform.position = posicao;

        if (mirarNoAlvo)
        {
            Vector3 direcao = alvo - transform.position;
            if (direcao.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direcao.normalized, Vector3.up);
            }
        }

        NotificarMudancaDeArea(transform.position);
        if (cameraPrincipal != null)
        {
            AtualizarProjecao(transform.position.y);
        }
    }

    GerenteSelecao ObterGerenteSelecao()
    {
        if (gerenteSelecaoCache == null && Time.time >= proximaBuscaGerenteSelecao)
        {
            gerenteSelecaoCache = FindFirstObjectByType<GerenteSelecao>();
            proximaBuscaGerenteSelecao = Time.time + 1f;
        }

        return gerenteSelecaoCache;
    }

    private bool BloquearRotacaoPorMiraManual()
    {
        GerenteSelecao gerente = ObterGerenteSelecao();
        if (gerente == null || gerente.unidadesSelecionadas == null)
        {
            return false;
        }

        for (int i = 0; i < gerente.unidadesSelecionadas.Count; i++)
        {
            ControleUnidade unidade = gerente.unidadesSelecionadas[i];
            if (unidade == null)
            {
                continue;
            }

            ControleSubmarino submarino = unidade.GetComponent<ControleSubmarino>();
            if (submarino != null && submarino.EmModoManualDisparo())
            {
                return true;
            }

            LancadorNaval lancadorNaval = unidade.GetComponentInChildren<LancadorNaval>(true);
            if (lancadorNaval != null && lancadorNaval.modoAtual == LancadorNaval.ModoOperacao.Manual)
            {
                return true;
            }
        }

        return false;
    }
}
