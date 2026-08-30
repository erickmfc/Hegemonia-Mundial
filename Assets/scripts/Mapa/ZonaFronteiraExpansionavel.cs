using System;
using UnityEngine;

public enum EstadoZonaFronteiraExpansionavel
{
    Livre,
    Reservada,
    Ocupada,
    Bloqueada
}

/// <summary>
/// Parcela neutra de fronteira que pode ser reivindicada por uma fundação
/// territorial. A zona não substitui o GerenteDeTerritorio; ela registra a
/// oportunidade de expansão e deixa o marcador territorial existente assumir
/// a jurisdição depois da construção.
/// </summary>
[DisallowMultipleComponent]
public sealed class ZonaFronteiraExpansionavel : MonoBehaviour
{
    [Header("Identidade")]
    [SerializeField] private string idZona = "fronteira.expansao.01";
    [SerializeField] private string nomeZona = "Zona de expansão neutra";

    [Header("Área")]
    [SerializeField] private Vector2 tamanhoXZ = new Vector2(1600f, 1600f);
    [SerializeField] private float alturaColisao = 2f;

    [Header("Estado persistente da parcela")]
    [SerializeField] private EstadoZonaFronteiraExpansionavel estado = EstadoZonaFronteiraExpansionavel.Livre;
    [SerializeField] private int teamDono;
    [SerializeField] private string paisDonoId = string.Empty;
    [SerializeField] private string idReserva = string.Empty;

    public event Action<ZonaFronteiraExpansionavel> EstadoAlterado;

    public string IdZona => idZona;
    public string NomeZona => string.IsNullOrWhiteSpace(nomeZona) ? idZona : nomeZona;
    public Vector2 TamanhoXZ => tamanhoXZ;
    public EstadoZonaFronteiraExpansionavel Estado => estado;
    public int TeamDono => teamDono;
    public string PaisDonoId => paisDonoId;
    public string IdReserva => idReserva;
    public bool EstaLivre => estado == EstadoZonaFronteiraExpansionavel.Livre;

    public Bounds LimitesMundiais
    {
        get
        {
            return new Bounds(transform.position, new Vector3(
                Mathf.Max(1f, tamanhoXZ.x),
                Mathf.Max(0.1f, alturaColisao),
                Mathf.Max(1f, tamanhoXZ.y)));
        }
    }

    public void ConfigurarEditor(string novoId, string novoNome, Vector2 novoTamanho)
    {
        idZona = string.IsNullOrWhiteSpace(novoId) ? idZona : novoId;
        nomeZona = string.IsNullOrWhiteSpace(novoNome) ? nomeZona : novoNome;
        tamanhoXZ = new Vector2(Mathf.Max(10f, novoTamanho.x), Mathf.Max(10f, novoTamanho.y));
        estado = EstadoZonaFronteiraExpansionavel.Livre;
        teamDono = 0;
        paisDonoId = string.Empty;
        idReserva = string.Empty;
        AtualizarVisual();
    }

    public bool Contem(Vector3 pontoMundial, float margem = 0f)
    {
        Bounds limites = LimitesMundiais;
        float margemX = Mathf.Max(0f, margem);
        return pontoMundial.x >= limites.min.x + margemX
            && pontoMundial.x <= limites.max.x - margemX
            && pontoMundial.z >= limites.min.z + margemX
            && pontoMundial.z <= limites.max.z - margemX;
    }

    public bool PodeReservar(int teamId)
    {
        return teamId > 0 && estado == EstadoZonaFronteiraExpansionavel.Livre;
    }

    public bool TentarReservar(int teamId, string reservaId, out string motivo)
    {
        motivo = string.Empty;
        if (teamId <= 0)
        {
            motivo = "time inválido";
            return false;
        }

        if (estado != EstadoZonaFronteiraExpansionavel.Livre)
        {
            motivo = "a zona não está livre";
            return false;
        }

        estado = EstadoZonaFronteiraExpansionavel.Reservada;
        teamDono = teamId;
        idReserva = reservaId ?? string.Empty;
        AtualizarVisual();
        EstadoAlterado?.Invoke(this);
        return true;
    }

    public bool TentarReivindicar(int teamId, string paisId, out string motivo)
    {
        motivo = string.Empty;
        if (teamId <= 0)
        {
            motivo = "time inválido";
            return false;
        }

        if (estado == EstadoZonaFronteiraExpansionavel.Bloqueada)
        {
            motivo = "a zona está bloqueada";
            return false;
        }

        if (estado == EstadoZonaFronteiraExpansionavel.Ocupada && teamDono != teamId)
        {
            motivo = "a zona já pertence a outro time";
            return false;
        }

        if (estado == EstadoZonaFronteiraExpansionavel.Reservada && teamDono != teamId)
        {
            motivo = "a zona está reservada por outro time";
            return false;
        }

        estado = EstadoZonaFronteiraExpansionavel.Ocupada;
        teamDono = teamId;
        paisDonoId = paisId ?? string.Empty;
        idReserva = string.Empty;
        AtualizarVisual();
        EstadoAlterado?.Invoke(this);
        return true;
    }

    public void LiberarReserva(string reservaId)
    {
        if (estado != EstadoZonaFronteiraExpansionavel.Reservada) return;
        if (!string.IsNullOrEmpty(idReserva) && !string.Equals(idReserva, reservaId, StringComparison.Ordinal)) return;

        estado = EstadoZonaFronteiraExpansionavel.Livre;
        teamDono = 0;
        paisDonoId = string.Empty;
        idReserva = string.Empty;
        AtualizarVisual();
        EstadoAlterado?.Invoke(this);
    }

    public void AtualizarVisual()
    {
        LineRenderer linha = GetComponent<LineRenderer>();
        if (linha == null) return;

        Color cor;
        switch (estado)
        {
            case EstadoZonaFronteiraExpansionavel.Reservada:
                cor = new Color(1f, 0.78f, 0.12f, 0.9f);
                break;
            case EstadoZonaFronteiraExpansionavel.Ocupada:
                cor = CorDoTime(teamDono);
                break;
            case EstadoZonaFronteiraExpansionavel.Bloqueada:
                cor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
                break;
            default:
                cor = new Color(0.1f, 0.9f, 0.9f, 0.8f);
                break;
        }

        linha.startColor = cor;
        linha.endColor = cor;
    }

    private static Color CorDoTime(int teamId)
    {
        switch (teamId)
        {
            case 1: return new Color(0.1f, 0.45f, 1f, 0.9f);
            case 2: return new Color(1f, 0.15f, 0.15f, 0.9f);
            case 3: return new Color(0.2f, 0.9f, 0.25f, 0.9f);
            default: return new Color(0.85f, 0.7f, 0.2f, 0.9f);
        }
    }

    private void OnEnable()
    {
        AtualizarVisual();
        GerenciadorExpansaoFronteira.Instancia?.Registrar(this);
    }

    private void OnDisable()
    {
        GerenciadorExpansaoFronteira.Instancia?.Remover(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = estado == EstadoZonaFronteiraExpansionavel.Ocupada
            ? new Color(0.1f, 0.45f, 1f, 0.18f)
            : new Color(0.1f, 0.9f, 0.9f, 0.16f);
        Bounds limites = LimitesMundiais;
        Gizmos.DrawCube(limites.center, limites.size);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(limites.center, limites.size);
    }
}
