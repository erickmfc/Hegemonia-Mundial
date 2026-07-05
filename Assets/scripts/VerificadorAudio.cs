using UnityEngine;

[DisallowMultipleComponent]
public sealed class VerificadorAudio : MonoBehaviour
{
    [ContextMenu("Verificar sistema de audio")]
    public void VerificarSistemaAudio()
    {
#if UNITY_EDITOR
        AudioListener listener = GetComponent<AudioListener>();
        AudioSource[] fontesLocais = GetComponentsInChildren<AudioSource>(true);
        Debug.Log($"[Audio] Listener local: {(listener != null && listener.enabled ? "ativo" : "ausente/inativo")} | Fontes locais: {fontesLocais.Length}", this);
#endif
    }

    private void Awake()
    {
        enabled = false;
    }
}
