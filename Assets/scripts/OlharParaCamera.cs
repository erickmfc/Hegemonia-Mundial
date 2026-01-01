using UnityEngine;

public class OlharParaCamera : MonoBehaviour
{
    public Transform cameraDoJogo;
    
    void Start()
    {
        // Se não foi definida, pega a câmera principal automaticamente
        if (cameraDoJogo == null)
        {
            if (Camera.main != null)
            {
                cameraDoJogo = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("OlharParaCamera: Nenhuma câmera encontrada!");
            }
        }
    }

    void LateUpdate() // LateUpdate acontece depois que tudo se moveu
    {
        if (cameraDoJogo == null) return;
        
        // Faz a barra olhar na mesma direção que a câmera está olhando
        transform.LookAt(transform.position + cameraDoJogo.forward);
    }
}
