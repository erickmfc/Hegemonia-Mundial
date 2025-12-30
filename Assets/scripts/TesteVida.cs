using UnityEngine;

/// <summary>
/// Script de teste para demonstrar o sistema de vida e dano
/// Adicione este script em qualquer unidade para testar no Editor
/// </summary>
public class TesteVida : MonoBehaviour
{
    [Header("Testes de Dano")]
    [Tooltip("Tecla para causar dano pequeno")]
    public KeyCode teclaDanoPequeno = KeyCode.Alpha1;
    
    [Tooltip("Tecla para causar dano médio")]
    public KeyCode teclaDanoMedio = KeyCode.Alpha2;
    
    [Tooltip("Tecla para causar dano grande")]
    public KeyCode teclaDanoGrande = KeyCode.Alpha3;
    
    [Header("Testes de Cura")]
    [Tooltip("Tecla para curar")]
    public KeyCode teclaCurar = KeyCode.H;
    
    [Header("Configuração")]
    public int danoPequeno = 10;
    public int danoMedio = 25;
    public int danoGrande = 50;
    public int quantidadeCura = 30;
    
    private Vida scriptVida;

    void Start()
    {
        scriptVida = GetComponent<Vida>();
        
        if (scriptVida == null)
        {
            Debug.LogError($"⚠️ TesteVida: {gameObject.name} não tem componente Vida!");
            enabled = false;
            return;
        }
        
        Debug.Log($"🧪 TESTE DE VIDA ATIVADO em {gameObject.name}");
        Debug.Log($"Pressione {teclaDanoPequeno} para dano pequeno ({danoPequeno})");
        Debug.Log($"Pressione {teclaDanoMedio} para dano médio ({danoMedio})");
        Debug.Log($"Pressione {teclaDanoGrande} para dano grande ({danoGrande})");
        Debug.Log($"Pressione {teclaCurar} para curar ({quantidadeCura})");
    }

    void Update()
    {
        if (scriptVida == null || !scriptVida.EstaVivo())
        {
            return;
        }

        // Testes de dano
        if (Input.GetKeyDown(teclaDanoPequeno))
        {
            scriptVida.ReceberDano(danoPequeno);
            Debug.Log($"💥 Dano pequeno aplicado! Vida: {scriptVida.vidaAtual}/{scriptVida.vidaMaxima}");
        }
        
        if (Input.GetKeyDown(teclaDanoMedio))
        {
            scriptVida.ReceberDano(danoMedio);
            Debug.Log($"💥💥 Dano médio aplicado! Vida: {scriptVida.vidaAtual}/{scriptVida.vidaMaxima}");
        }
        
        if (Input.GetKeyDown(teclaDanoGrande))
        {
            scriptVida.ReceberDano(danoGrande);
            Debug.Log($"💥💥💥 Dano grande aplicado! Vida: {scriptVida.vidaAtual}/{scriptVida.vidaMaxima}");
        }
        
        // Teste de cura
        if (Input.GetKeyDown(teclaCurar))
        {
            scriptVida.Curar(quantidadeCura);
            Debug.Log($"💚 Curado! Vida: {scriptVida.vidaAtual}/{scriptVida.vidaMaxima}");
        }
    }

    // Desenha informações na tela (útil para debug)
    void OnGUI()
    {
        if (scriptVida == null || !scriptVida.EstaVivo()) return;
        
        // Mostra informações na tela
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        
        string info = $"{gameObject.name}\n";
        info += $"Vida: {scriptVida.vidaAtual}/{scriptVida.vidaMaxima}\n";
        info += $"Porcentagem: {(scriptVida.PorcentagemVida() * 100f):F1}%\n";
        info += $"\n[{teclaDanoPequeno}] Dano {danoPequeno}";
        info += $"\n[{teclaDanoMedio}] Dano {danoMedio}";
        info += $"\n[{teclaDanoGrande}] Dano {danoGrande}";
        info += $"\n[{teclaCurar}] Curar {quantidadeCura}";
        
        GUI.Label(new Rect(10, 10, 300, 200), info, style);
    }
}
