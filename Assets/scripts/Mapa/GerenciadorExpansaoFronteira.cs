using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridade única das parcelas neutras de fronteira. Não movimenta unidades
/// nem substitui GerenteDeTerritorio; apenas reserva e confirma a expansão.
/// </summary>
[DisallowMultipleComponent]
public sealed class GerenciadorExpansaoFronteira : MonoBehaviour
{
    public static GerenciadorExpansaoFronteira Instancia { get; private set; }

    [SerializeField] private List<ZonaFronteiraExpansionavel> zonas = new List<ZonaFronteiraExpansionavel>();
    private readonly Dictionary<string, ZonaFronteiraExpansionavel> porId = new Dictionary<string, ZonaFronteiraExpansionavel>(StringComparer.Ordinal);

    public event Action<ZonaFronteiraExpansionavel> ZonaReivindicada;
    public IReadOnlyList<ZonaFronteiraExpansionavel> Zonas => zonas;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        ReconstruirCache();
    }

    private void OnEnable()
    {
        if (Instancia == null) Instancia = this;
        ReconstruirCache();
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    public void ReconstruirCache()
    {
        zonas.Clear();
        porId.Clear();
        ZonaFronteiraExpansionavel[] encontradas = GetComponentsInChildren<ZonaFronteiraExpansionavel>(true);
        for (int i = 0; i < encontradas.Length; i++)
        {
            ZonaFronteiraExpansionavel zona = encontradas[i];
            if (zona == null || string.IsNullOrWhiteSpace(zona.IdZona)) continue;
            if (porId.ContainsKey(zona.IdZona))
            {
                Debug.LogError("[Fronteira] ID de zona duplicado: " + zona.IdZona, zona);
                continue;
            }

            porId.Add(zona.IdZona, zona);
            zonas.Add(zona);
        }
    }

    public void Registrar(ZonaFronteiraExpansionavel zona)
    {
        if (zona == null || string.IsNullOrWhiteSpace(zona.IdZona)) return;
        if (porId.TryGetValue(zona.IdZona, out ZonaFronteiraExpansionavel existente))
        {
            if (existente == zona) return;
            Debug.LogError("[Fronteira] ID de zona duplicado: " + zona.IdZona, zona);
            return;
        }

        porId.Add(zona.IdZona, zona);
        if (!zonas.Contains(zona)) zonas.Add(zona);
    }

    public void Remover(ZonaFronteiraExpansionavel zona)
    {
        if (zona == null) return;
        zonas.Remove(zona);
        if (!string.IsNullOrWhiteSpace(zona.IdZona)
            && porId.TryGetValue(zona.IdZona, out ZonaFronteiraExpansionavel existente)
            && existente == zona)
        {
            porId.Remove(zona.IdZona);
        }
    }

    public ZonaFronteiraExpansionavel EncontrarPorId(string idZona)
    {
        if (string.IsNullOrWhiteSpace(idZona)) return null;
        porId.TryGetValue(idZona, out ZonaFronteiraExpansionavel zona);
        return zona;
    }

    public ZonaFronteiraExpansionavel EncontrarNoPonto(Vector3 pontoMundial)
    {
        for (int i = 0; i < zonas.Count; i++)
        {
            ZonaFronteiraExpansionavel zona = zonas[i];
            if (zona != null && zona.Contem(pontoMundial)) return zona;
        }

        return null;
    }

    public bool TryReivindicarZona(string idZona, int teamId, string paisId, out string motivo)
    {
        ZonaFronteiraExpansionavel zona = EncontrarPorId(idZona);
        if (zona == null)
        {
            motivo = "zona de fronteira não encontrada";
            return false;
        }

        if (!zona.TentarReivindicar(teamId, paisId, out motivo)) return false;
        ZonaReivindicada?.Invoke(zona);
        return true;
    }

    public bool TryReivindicarNoPonto(Vector3 pontoMundial, int teamId, string paisId, out string motivo)
    {
        ZonaFronteiraExpansionavel zona = EncontrarNoPonto(pontoMundial);
        if (zona == null)
        {
            motivo = "o ponto não está em uma parcela de fronteira expansionável";
            return false;
        }

        return TryReivindicarZona(zona.IdZona, teamId, paisId, out motivo);
    }

    /// <summary>
    /// Chamado pelo Construtor somente depois que a fundação/bandeira foi
    /// instanciada. Assim, uma vaga de expansão não é marcada antes da compra.
    /// </summary>
    public bool NotificarConstrucao(GameObject edificacao, Vector3 pontoMundial)
    {
        ZonaFronteiraExpansionavel zona = EncontrarNoPonto(pontoMundial);
        if (zona == null || edificacao == null) return false;

        bool eFundacao = edificacao.GetComponentInChildren<MarcadorTerritorio>(true) != null
            || edificacao.GetComponentInChildren<ComplexoGovernamental>(true) != null
            || edificacao.name.IndexOf("bandeira", StringComparison.OrdinalIgnoreCase) >= 0
            || edificacao.name.IndexOf("flag", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!eFundacao) return false;

        IdentidadeUnidade identidade = edificacao.GetComponentInParent<IdentidadeUnidade>();
        int teamId = identidade != null && identidade.teamID > 0 ? identidade.teamID : 1;
        string paisId = identidade != null ? identidade.nomeDoPais : string.Empty;
        TryReivindicarZona(zona.IdZona, teamId, paisId, out _);
        return zona.TeamDono == teamId;
    }
}
