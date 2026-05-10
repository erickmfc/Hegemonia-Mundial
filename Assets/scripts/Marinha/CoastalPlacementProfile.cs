using UnityEngine;

[System.Serializable]
public class CoastalPlacementProfile
{
    public float offsetAguaFrente = 35f;
    public float offsetTerraTras = -15f;
    public float raioMinimoSonda = 8f;
    public float raioMaximoSonda = 180f;
    public float empurraoPreview = 14f;
    public float empurraoCommit = 18f;
    public bool usarValidacaoRapidaNoPreview = true;

    public static CoastalPlacementProfile CriarPadrao()
    {
        return new CoastalPlacementProfile();
    }
}
