using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float velocidade = 20f;
    public float velocidadeZoom = 4000f;
    public float velocidadeRotacao = 100f;
    public float multiplicadorShift = 9.69f; // Velocidade triplicada (Antes 3.23)
    [Header("Visão da Câmera")]
    public float campoDeVisaoBase = 75f;
    public float campoDeVisaoMin = 65f;
    public float campoDeVisaoMax = 85f;
    public float alturaMinParaFov = 2f;
    public float alturaMaxParaFov = 2500f;

    private float tempoShiftPressionado = 0f;
    private GerenteSelecao gerenteSelecaoCache;
    private float proximaBuscaGerenteSelecao = 0f;
    private Camera cameraPrincipal;

    void Start()
    {
        cameraPrincipal = GetComponent<Camera>();
        if (cameraPrincipal != null)
        {
            cameraPrincipal.fieldOfView = campoDeVisaoBase;
        }
    }

    void Update()
    {
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

        if (Input.GetKey("w")) pos += forward * velAtual * Time.deltaTime;
        if (Input.GetKey("s")) pos -= forward * velAtual * Time.deltaTime;
        if (Input.GetKey("d")) pos += right * velAtual * Time.deltaTime;
        if (Input.GetKey("a")) pos -= right * velAtual * Time.deltaTime;

        // --- 3. Zoom (Rodinha do Mouse) ---
        // --- 3. Zoom (Rodinha do Mouse e Teclado) ---
        float zoomInput = 0f;
        
        // Bloqueia Zoom se estiver sobre UI ou com Menus Abertos
        bool mouseEmCimaDeUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        bool menusAbertos = MenuConstrucao.EstaAberto || MenuPier.EstaAberto;

        if (!mouseEmCimaDeUI && !menusAbertos)
        {
            zoomInput = Input.GetAxis("Mouse ScrollWheel");
        }

        // Teclas + e - (Teclado) com atalhos espelhados em Espaço/Ctrl.
        if (Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            zoomInput += 0.03f; // Desce a camera
        }
        if (Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus))
        {
            zoomInput -= 0.08f; // Sobe a camera mais rápido (Antes 0.03)
        }
        if (Input.GetKey(KeyCode.Space))
        {
            zoomInput -= 0.15f; // Espaço sobe MUITO mais rápido agora (Antes 0.06)
        }

        pos.y -= zoomInput * velocidadeZoom * Time.deltaTime;
        pos.y = Mathf.Clamp(pos.y, 2f, 2500f); // Teto aumentado para o atalho do Espaço

        transform.position = pos;

        if (cameraPrincipal == null)
        {
            cameraPrincipal = GetComponent<Camera>();
        }

        if (cameraPrincipal != null)
        {
            float tAltura = Mathf.InverseLerp(alturaMinParaFov, alturaMaxParaFov, pos.y);
            cameraPrincipal.fieldOfView = Mathf.Lerp(campoDeVisaoMin, campoDeVisaoMax, tAltura);
        }

        // --- 4. Rotação e Inclinação (Botão Direito, Meio ou Teclas Q/E) ---
        // --- 4. Rotação e Inclinação (Botão Direito, Meio ou Teclas Q/E) ---
        bool podeRotacionar = true;

        // Se estiver segurando o Direito, verifica se tem unidades selecionadas (para não conflitar com Mover)
        if (Input.GetMouseButton(1))
        {
            var gerenteSel = ObterGerenteSelecao();
            if (gerenteSel != null && gerenteSel.unidadesSelecionadas.Count > 0)
            {
                podeRotacionar = false; 
            }
        }

        if (podeRotacionar && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
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

    GerenteSelecao ObterGerenteSelecao()
    {
        if (gerenteSelecaoCache == null && Time.time >= proximaBuscaGerenteSelecao)
        {
            gerenteSelecaoCache = FindFirstObjectByType<GerenteSelecao>();
            proximaBuscaGerenteSelecao = Time.time + 1f;
        }

        return gerenteSelecaoCache;
    }
}
