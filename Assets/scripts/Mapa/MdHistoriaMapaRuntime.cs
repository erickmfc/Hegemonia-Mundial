using UnityEngine;

/// <summary>
/// Registro leve da área jogável da Md Historia.
/// Os bloqueios físicos são os BoxColliders gerados pelo utilitário Editor;
/// este componente apenas mantém os parâmetros e desenha a área no Editor.
/// </summary>
public sealed class MdHistoriaMapaRuntime : MonoBehaviour
{
    [SerializeField] private Bounds mapaBounds;
    [SerializeField] private float nivelAgua;
    [SerializeField] private float alturaParedao;

    public Bounds MapaBounds => mapaBounds;
    public float NivelAgua => nivelAgua;
    public float AlturaParedao => alturaParedao;

    public void Configurar(Bounds bounds, float waterLevel, float wallHeight)
    {
        mapaBounds = bounds;
        nivelAgua = waterLevel;
        alturaParedao = wallHeight;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.28f, 0.65f, 0.78f, 0.75f);
        Gizmos.DrawWireCube(mapaBounds.center, mapaBounds.size);
    }
}
