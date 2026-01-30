using UnityEngine;

/// <summary>
/// Visual de mira circular no mouse quando o submarino está em modo ataque
/// </summary>
public class VisualMiraSubmarino : MonoBehaviour
{
    public static VisualMiraSubmarino Instancia;
    
    [Header("Configuração Visual")]
    public Color corMira = new Color(1f, 0f, 0f, 0.5f); // Vermelho semi-transparente
    public float raioCirculo = 50f; // Raio em pixels
    public float espessuraLinha = 2f;
    public int segmentos = 50;
    
    private bool modoAtivo = false;
    private Vector2 posicaoMouse;
    
    void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void AtivarMira()
    {
        modoAtivo = true;
    }
    
    public void DesativarMira()
    {
        modoAtivo = false;
    }
    
    void Update()
    {
        if (modoAtivo)
        {
            posicaoMouse = Input.mousePosition;
        }
    }
    
    void OnGUI()
    {
        if (!modoAtivo) return;
        
        // Desenha círculo de mira
        DesenharCirculo(posicaoMouse, raioCirculo, corMira);
        
        // Desenha cruz no centro
        DesenharCruz(posicaoMouse, 10f, corMira);
        
        // Texto informativo
        GUIStyle estilo = new GUIStyle(GUI.skin.label);
        estilo.fontSize = 14;
        estilo.fontStyle = FontStyle.Bold;
        estilo.normal.textColor = Color.white;
        
        Vector2 posTexto = new Vector2(posicaoMouse.x + raioCirculo + 10, Screen.height - posicaoMouse.y - 10);
        GUI.Label(new Rect(posTexto.x, posTexto.y, 200, 30), "BOTÃO DIREITO = DISPARAR", estilo);
    }
    
    void DesenharCirculo(Vector2 centro, float raio, Color cor)
    {
        // Converte coordenadas (GUI usa Y invertido)
        centro.y = Screen.height - centro.y;
        
        Texture2D textura = new Texture2D(1, 1);
        textura.SetPixel(0, 0, cor);
        textura.Apply();
        
        GUI.color = cor;
        
        float angleStep = 360f / segmentos;
        Vector2 prevPoint = centro + new Vector2(raio, 0);
        
        for (int i = 1; i <= segmentos; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector2 newPoint = centro + new Vector2(Mathf.Cos(angle) * raio, Mathf.Sin(angle) * raio);
            
            DesenharLinha(prevPoint, newPoint, espessuraLinha, textura);
            prevPoint = newPoint;
        }
        
        GUI.color = Color.white;
    }
    
    void DesenharCruz(Vector2 centro, float tamanho, Color cor)
    {
        centro.y = Screen.height - centro.y;
        
        Texture2D textura = new Texture2D(1, 1);
        textura.SetPixel(0, 0, cor);
        textura.Apply();
        
        GUI.color = cor;
        
        // Linha horizontal
        DesenharLinha(centro - new Vector2(tamanho, 0), centro + new Vector2(tamanho, 0), espessuraLinha, textura);
        
        // Linha vertical
        DesenharLinha(centro - new Vector2(0, tamanho), centro + new Vector2(0, tamanho), espessuraLinha, textura);
        
        GUI.color = Color.white;
    }
    
    void DesenharLinha(Vector2 pontoA, Vector2 pontoB, float espessura, Texture2D textura)
    {
        Vector2 direcao = pontoB - pontoA;
        float distancia = direcao.magnitude;
        float angulo = Mathf.Atan2(direcao.y, direcao.x) * Mathf.Rad2Deg;
        
        GUIUtility.RotateAroundPivot(angulo, pontoA);
        GUI.DrawTexture(new Rect(pontoA.x, pontoA.y - espessura / 2, distancia, espessura), textura);
        GUIUtility.RotateAroundPivot(-angulo, pontoA);
    }
}
