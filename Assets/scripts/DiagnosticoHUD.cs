using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Script simples para mostrar na tela o que está faltando.
/// Se o HUD sumiu, arraste este script para a Câmera Principal.
/// </summary>
public class DiagnosticoHUD : MonoBehaviour
{
    [SerializeField] private bool habilitadoEmRuntime = false;
    private GerenciadorRecursos gerenciador;
    private PainelRecursos painel;
    private MenuConstrucao menuC;
    private UnityEngine.EventSystems.EventSystem[] eventSystems;

    void Start()
    {
        if (!habilitadoEmRuntime)
        {
            return;
        }

        gerenciador = FindFirstObjectByType<GerenciadorRecursos>();
        painel = FindFirstObjectByType<PainelRecursos>();
        menuC = FindFirstObjectByType<MenuConstrucao>();
        eventSystems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        
        Debug.Log("🔍 --- DIAGNÓSTICO DO SISTEMA ---");
        Debug.Log($"GerenciadorRecursos: {(gerenciador != null ? "✅ OK" : "❌ FALTA")}");
        Debug.Log($"PainelRecursos: {(painel != null ? "✅ OK" : "❌ FALTA")}");
        Debug.Log($"MenuConstrucao ('C'): {(menuC != null ? "✅ OK" : "❌ FALTA")}");
        Debug.Log($"EventSystems: {eventSystems.Length} encontrados (Ideal: 1)");
    }

    void OnGUI()
    {
        if (!habilitadoEmRuntime)
        {
            return;
        }

        GUI.skin.label.fontSize = 20;
        float y = 10;

        if (gerenciador == null)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(10, y, 800, 30), "❌ GerenciadorRecursos NÃO ENCONTRADO!");
            y += 30;
        }

        if (painel == null)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(10, y, 800, 30), "❌ PainelRecursos (HUD) NÃO ENCONTRADO!");
            y += 30;
        }

        if (menuC == null)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(10, y, 800, 30), "❌ Script 'MenuConstrucao' NÃO ENCONTRADO! (Por isso o 'C' não funciona)");
            y += 30;
        }

        if (eventSystems != null && eventSystems.Length > 1)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(10, y, 800, 30), $"⚠️ ALERTA: {eventSystems.Length} EventSystems detectados! Delete os extras.");
            y += 30;
        }
        else if (eventSystems == null || eventSystems.Length == 0)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(10, y, 800, 30), "❌ NENHUM EventSystem! UI não vai clicar.");
            y += 30;
        }

        if (gerenciador == null || painel == null)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(10, y + 10, 800, 30), "👉 Use o script 'CriadorHUDRecursos' para consertar HUD.");
        }
    }

    public void SetRuntimeVisible(bool ativo)
    {
        habilitadoEmRuntime = ativo;
        enabled = ativo;
    }
}
