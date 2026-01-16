using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton global que mantém registro de todos os helicópteros do país.
/// Permite que heliportos consultem quais helicópteros estão disponíveis.
/// </summary>
public class GerenciadorHelicopteros : MonoBehaviour
{
    // --- SINGLETON ---
    public static GerenciadorHelicopteros Instancia { get; private set; }

    [Header("Registro de Helicópteros")]
    [Tooltip("Lista de todos os helicópteros registrados no país")]
    public List<Helicoptero> helicopterosRegistrados = new List<Helicoptero>();

    [Header("Debug")]
    public bool mostrarDebug = false;

    void Awake()
    {
        // Singleton pattern
        if (Instancia == null)
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Registra um helicóptero no sistema global.
    /// Chamado automaticamente pelo script Helicoptero no Start().
    /// </summary>
    public void RegistrarHelicoptero(Helicoptero heli)
    {
        if (heli != null && !helicopterosRegistrados.Contains(heli))
        {
            helicopterosRegistrados.Add(heli);
            if (mostrarDebug)
                Debug.Log($"[GerenciadorHelicopteros] Helicóptero '{heli.nomeHelicoptero}' registrado. Total: {helicopterosRegistrados.Count}");
        }
    }

    /// <summary>
    /// Remove um helicóptero do registro (quando destruído).
    /// </summary>
    public void RemoverHelicoptero(Helicoptero heli)
    {
        if (heli != null && helicopterosRegistrados.Contains(heli))
        {
            helicopterosRegistrados.Remove(heli);
            if (mostrarDebug)
                Debug.Log($"[GerenciadorHelicopteros] Helicóptero '{heli.nomeHelicoptero}' removido. Restantes: {helicopterosRegistrados.Count}");
        }
    }

    /// <summary>
    /// Retorna lista de helicópteros disponíveis (que não estão em missão ou ocupados).
    /// </summary>
    public List<Helicoptero> ObterHelicopterosDisponiveis()
    {
        List<Helicoptero> disponiveis = new List<Helicoptero>();

        foreach (Helicoptero heli in helicopterosRegistrados)
        {
            if (heli != null && heli.EstaDisponivel())
            {
                disponiveis.Add(heli);
            }
        }

        return disponiveis;
    }

    /// <summary>
    /// Retorna TODOS os helicópteros (disponíveis ou não).
    /// </summary>
    public List<Helicoptero> ObterTodosHelicopteros()
    {
        // Limpa entradas nulas (helicópteros destruídos)
        helicopterosRegistrados.RemoveAll(h => h == null);
        return new List<Helicoptero>(helicopterosRegistrados);
    }

    /// <summary>
    /// Verifica se existe pelo menos um heliporto construído.
    /// Usado para condicionar a compra de helicópteros no menu.
    /// </summary>
    public bool ExisteHeliporto()
    {
        Heliporto[] heliportos = FindObjectsOfType<Heliporto>();
        return heliportos != null && heliportos.Length > 0;
    }

    /// <summary>
    /// Retorna a quantidade de heliportos existentes.
    /// </summary>
    public int QuantidadeHeliportos()
    {
        Heliporto[] heliportos = FindObjectsOfType<Heliporto>();
        return heliportos != null ? heliportos.Length : 0;
    }
}
