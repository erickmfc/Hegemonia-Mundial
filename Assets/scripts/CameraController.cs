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
    private const float DistanciaMinimaNotificacaoSqr = 625f;
    private const float AlturaMinimaNotificacao = 5f;

    void Start()
    {
        cameraPrincipal = GetComponent<Camera>();
        if (cameraPrincipal != null)
        {
            cameraPrincipal.fieldOfView = campoDeVisaoBase;
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

        // Força a substituição do Inspector se estiver salvo um valor muito baixo
        if (multiplicadorShift < 12f) multiplicadorShift = 12f;

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
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();
        Vector3 right = transform.right;
        right.y = 0;
        right.Normalize();

        if (!menusAbertos)
        {
            if (Input.GetKey("w")) pos += forward * velAtual * Time.deltaTime;
            if (Input.GetKey("s")) pos -= forward * velAtual * Time.deltaTime;
            if (Input.GetKey("d")) pos += right * velAtual * Time.deltaTime;
            if (Input.GetKey("a")) pos -= right * velAtual * Time.deltaTime;
        }

        // --- 3. Zoom (Rodinha do Mouse e Teclado) ---
        float zoomInput = 0f;
        
        // Bloqueia Zoom se estiver sobre UI ou com Menus Abertos
        bool mouseEmCimaDeUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (!mouseEmCimaDeUI && !menusAbertos)
        {
            zoomInput = Input.GetAxis("Mouse ScrollWheel");
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

        transform.position = pos;
        NotificarMudancaDeArea(pos);

        if (cameraPrincipal == null)
        {
            cameraPrincipal = GetComponent<Camera>();
        }

        if (cameraPrincipal != null)
        {
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
                if (Input.GetKey("q")) transform.Rotate(Vector3.up, -velocidadeRotacao * Time.deltaTime, Space.World);
                if (Input.GetKey("e")) transform.Rotate(Vector3.up, velocidadeRotacao * Time.deltaTime, Space.World);
            }
        }
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
        CameraMudouArea?.Invoke(posicao);
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
