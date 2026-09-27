using System;
using System.Collections.Generic;
using UnityEngine;

public enum TipoRegiaoPolitica
{
    Terra,
    AguasTerritoriais
}

/// <summary>Estado político sem inferir neutralidade a partir de owner=0.</summary>
public enum EstadoTerritorial
{
    Undefined,
    CountryOwned,
    NeutralTerritory,
    UnownedTerritory,
    InternationalWaters
}

public enum FonteConsultaTerritorial
{
    Nenhuma,
    PoligonoPolitico,
    AguasInternacionais,
    Legado
}

/// <summary>Geometria política editável, independente dos Terrains físicos.</summary>
[Serializable]
public sealed class RegiaoPolitica
{
    public string territorioId;
    public string nome;
    [Tooltip("-1 significa que a associação ainda não foi definida no editor.")]
    public int ownerCountryTeamId = -1;
    public bool neutral;
    public bool capturable;
    public TipoRegiaoPolitica tipo = TipoRegiaoPolitica.Terra;
    public Color corMapa = Color.white;
    [Tooltip("Vértices UV da imagem de referência; a origem é o canto superior esquerdo.")]
    public List<Vector2> vertices = new List<Vector2>();

    public bool PossuiPoligono
    {
        get { return vertices != null && vertices.Count >= 3; }
    }
}

[Serializable]
public struct ResultadoConsultaTerritorio
{
    public bool encontrouRegiao;
    public string territorioId;
    public Vector3 worldPosition;
    public Vector2 mapPosition;
    public bool possuiMapPosition;
    public int ownerCountryTeamId;
    public bool neutral;
    public bool capturable;
    public TipoRegiaoPolitica tipo;
    public bool aguasInternacionais;
    public FonteConsultaTerritorial fonte;

    public EstadoTerritorial estado
    {
        get
        {
            if (!encontrouRegiao) return EstadoTerritorial.Undefined;
            if (aguasInternacionais) return EstadoTerritorial.InternationalWaters;
            if (neutral) return EstadoTerritorial.NeutralTerritory;
            return ownerCountryTeamId > 0 ? EstadoTerritorial.CountryOwned : EstadoTerritorial.UnownedTerritory;
        }
    }

    public static ResultadoConsultaTerritorio NaoDefinido
    {
        get { return new ResultadoConsultaTerritorio { ownerCountryTeamId = -1, fonte = FonteConsultaTerritorial.Nenhuma }; }
    }
}

[Serializable]
public sealed class SaveProprietarioTerritorio
{
    public string territorioId;
    public int ownerCountryTeamId;
    public bool neutral;
}
