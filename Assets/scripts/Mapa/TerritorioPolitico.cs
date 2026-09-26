using System;
using System.Collections.Generic;
using UnityEngine;

public enum TipoRegiaoPolitica
{
    Terra,
    AguasTerritoriais
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
    public int ownerCountryTeamId;
    public bool neutral;
    public bool capturable;
    public TipoRegiaoPolitica tipo;
    public bool aguasInternacionais;

    public static ResultadoConsultaTerritorio NaoDefinido
    {
        get { return new ResultadoConsultaTerritorio { ownerCountryTeamId = -1 }; }
    }
}

[Serializable]
public sealed class SaveProprietarioTerritorio
{
    public string territorioId;
    public int ownerCountryTeamId;
}
