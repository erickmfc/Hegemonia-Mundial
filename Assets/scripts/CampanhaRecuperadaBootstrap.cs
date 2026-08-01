using UnityEngine;

/// <summary>
/// Ponto de entrada seguro para a cena de campanha recuperada.
/// O conteudo de jogo e adicionado gradualmente para evitar reintroduzir
/// dados serializados corrompidos das cenas de recuperacao.
/// </summary>
public class CampanhaRecuperadaBootstrap : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("[Campanha] Cena de campanha recuperada iniciada com sucesso.");
    }
}
