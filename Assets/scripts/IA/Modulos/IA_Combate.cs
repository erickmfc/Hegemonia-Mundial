using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class IA_Combate : MonoBehaviour
{
    private IA_Comandante chefe;
    
    [Header("Inteligência IFF (Global)")]
    // A IA mantém uma lista de alvos conhecidos para ajudar unidades "cegas"
    public List<Transform> alvosDetectados = new List<Transform>();

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    /// <summary>
    /// Chamado por qualquer unidade da IA que avista um inimigo.
    /// Compartilha a informação com o resto do esquadrão.
    /// </summary>
    public void ReportarAvistamento(Transform inimigo)
    {
        if (!alvosDetectados.Contains(inimigo))
        {
            alvosDetectados.Add(inimigo);
            // Poderia acionar um alarme global ou redirecionar unidades próximas
        }
    }

    /// <summary>
    /// Limpa alvos mortos da memória da IA
    /// </summary>
    public void LimparMemoria()
    {
        alvosDetectados.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy);
    }
}
