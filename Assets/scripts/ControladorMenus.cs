using UnityEngine;

public class ControladorMenus : MonoBehaviour
{
    public GameObject janelaMercado; // Aqui vamos colocar sua janela azul

    // Essa função será chamada pelo BOTÃO MERCADO
    public void AbrirFecharMercado()
    {
        // Verifica se a janela está ativa ou não
        bool estaAberta = janelaMercado.activeSelf;

        // Inverte o estado (Se aberta -> fecha. Se fechada -> abre)
        janelaMercado.SetActive(!estaAberta);
    }
}
