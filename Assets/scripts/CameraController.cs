using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float velocidade = 20f;
    public float velocidadeZoom = 4000f;
    public float velocidadeRotacao = 100f;
    public float multiplicadorShift = 2.5f;

    void Update()
    {
        // --- 1. Controle de Velocidade (Speed Shift) ---
        float velAtual = velocidade;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            velAtual *= multiplicadorShift;
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
        float zoomInput = Input.GetAxis("Mouse ScrollWheel");

        // Teclas + e - (Teclado Numérico e Alfanumérico)
        // Adiciona um valor constante por frame enquanto a tecla é segurada
        if (Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.Equals))
        {
            zoomInput += 0.03f; // Ajuste este valor para controlar a velocidade do teclado
        }
        if (Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.Minus))
        {
            zoomInput -= 0.03f;
        }

        pos.y -= zoomInput * velocidadeZoom * Time.deltaTime;
        pos.y = Mathf.Clamp(pos.y, 2f, 150f); // Aumentei o limite superior de 70 para 150 para permitir ver mais do mapa

        transform.position = pos;

        // --- 4. Rotação e Inclinação (Botão Direito, Meio ou Teclas Q/E) ---
        // --- 4. Rotação e Inclinação (Botão Direito, Meio ou Teclas Q/E) ---
        bool podeRotacionar = true;

        // Se estiver segurando o Direito, verifica se tem unidades selecionadas (para não conflitar com Mover)
        if (Input.GetMouseButton(1))
        {
            var gerenteSel = FindFirstObjectByType<GerenteSelecao>();
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
}

