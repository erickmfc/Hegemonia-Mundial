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
    public int casas = 0;
    public int pesquisasMilitares = 0;

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
    public void RegistrarUnidade(TipoUnidade tipo, int teamID, GameObject go = null)
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

        // Contagem extra para energia/economia
        if (go != null)
        {
            if (EhCasa(go)) casas++;
            if (EhPesquisaMilitar(go)) pesquisasMilitares++;
        }

        OnCensoAtualizado?.Invoke();
    }

    /// <summary>
    /// Remove uma unidade do censo (morte/destruição).
    /// </summary>
    public void RemoverUnidade(TipoUnidade tipo, int teamID, GameObject go = null)
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

        // Remoção extra
        if (go != null)
        {
            if (EhCasa(go)) casas = Mathf.Max(0, casas - 1);
            if (EhPesquisaMilitar(go)) pesquisasMilitares = Mathf.Max(0, pesquisasMilitares - 1);
        }

        OnCensoAtualizado?.Invoke();
    }

    private bool EhCasa(GameObject go)
    {
        return go != null
            && (go.GetComponent<Imovel>() != null
            || go.GetComponentInChildren<Imovel>(true) != null);
    }

    private bool EhPesquisaMilitar(GameObject go)
    {
        if (go == null) return false;

        if (go.GetComponent("PesquisaMilitar") != null) return true;
        if (go.GetComponentInChildren(typeof(MonoBehaviour), true) != null)
        {
            MonoBehaviour[] componentes = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < componentes.Length; i++)
            {
                MonoBehaviour componente = componentes[i];
                if (componente != null && componente.GetType().Name.IndexOf("PesquisaMilitar", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        string nome = go.name ?? string.Empty;
        return nome.IndexOf("pesquisa", StringComparison.OrdinalIgnoreCase) >= 0
            && nome.IndexOf("militar", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
