using UnityEngine;

public class Edificio : MonoBehaviour
{
    [Header("Interface")]
    public GameObject menuDeConstrucao; // Arraste o Painel/Canvas que este prédio abre

    public void AoSelecionar()
    {
        if (menuDeConstrucao != null)
        {
            menuDeConstrucao.SetActive(true);
        }
    }

    public void AoDeselecionar()
    {
        if (menuDeConstrucao != null)
        {
            menuDeConstrucao.SetActive(false);
        }
    }
}
