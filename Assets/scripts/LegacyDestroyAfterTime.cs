using UnityEngine;

/// <summary>
/// Compatibilidade para o componente legado usado pelos efeitos importados do
/// pacote PolygonWar. Os prefabs serializam apenas TimeToDestroy; manter o
/// mesmo campo permite reimportar os assets sem perder sua limpeza automática.
/// </summary>
public sealed class LegacyDestroyAfterTime : MonoBehaviour
{
    public float TimeToDestroy = 1f;

    private void OnEnable()
    {
        CancelInvoke(nameof(Destruir));
        Invoke(nameof(Destruir), Mathf.Max(0.01f, TimeToDestroy));
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(Destruir));
    }

    private void Destruir()
    {
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }
}
