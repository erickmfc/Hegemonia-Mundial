using UnityEngine;
using System;

/// <summary>
/// Sistema central de contagem populacional e militar.
/// Mantém registro em tempo real de todas as unidades do jogador.
/// </summary>
public class CensoImperial : MonoBehaviour
{
    public static CensoImperial Instancia { get; private set; }

    [Header("📊 Contagem Militar (Jogador)")]
    public int totalUnidades = 0;
    public int infantaria = 0;
    public int veiculos = 0;
    public int naval = 0;
    public int aereo = 0;
    public int estruturas = 0;

    // Evento para atualizar UI sempre que houver mudança
    public event Action OnCensoAtualizado;

    void Awake()
    {
        if(Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
    }

    /// <summary>
    /// Registra uma nova unidade no censo do jogador.
    /// Chamado automaticamente pelo IdentidadeUnidade.
    /// </summary>
    public void RegistrarUnidade(TipoUnidade tipo, int teamID)
    {
        // Só conta unidades do jogador (Team ID 1)
        if(teamID != 1) return;

        totalUnidades++;

        switch(tipo)
        {
            case TipoUnidade.Infantaria: infantaria++; break;
            case TipoUnidade.Veiculo: veiculos++; break;
            case TipoUnidade.Naval: naval++; break;
            case TipoUnidade.Aereo: aereo++; break;
            case TipoUnidade.Estrutura: estruturas++; break;
        }

        OnCensoAtualizado?.Invoke();
    }

    /// <summary>
    /// Remove uma unidade do censo (morte/destruição).
    /// </summary>
    public void RemoverUnidade(TipoUnidade tipo, int teamID)
    {
        if(teamID != 1) return;

        totalUnidades--;
        if(totalUnidades < 0) totalUnidades = 0;

        switch(tipo)
        {
            case TipoUnidade.Infantaria: infantaria--; break;
            case TipoUnidade.Veiculo: veiculos--; break;
            case TipoUnidade.Naval: naval--; break;
            case TipoUnidade.Aereo: aereo--; break;
            case TipoUnidade.Estrutura: estruturas--; break;
        }

        OnCensoAtualizado?.Invoke();
    }
}
