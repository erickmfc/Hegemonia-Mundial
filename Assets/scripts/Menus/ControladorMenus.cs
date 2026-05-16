using UnityEngine;

public class ControladorMenus : MonoBehaviour
{
    public GameObject janelaMercado; // Aqui vamos colocar sua janela azul

    private void Update()
    {
        if (janelaMercado != null && janelaMercado.activeSelf && !GestorMenusExclusivos.EstaAtivo(this))
        {
            janelaMercado.SetActive(false);
        }
    }

    // Essa função será chamada pelo BOTÃO MERCADO
    public void AbrirFecharMercado()
    {
        // Verifica se a janela está ativa ou não
        bool estaAberta = janelaMercado.activeSelf;

        // Inverte o estado (Se aberta -> fecha. Se fechada -> abre)
        bool novoEstado = !estaAberta;
        if (novoEstado) GestorMenusExclusivos.Abrir(this);
        else GestorMenusExclusivos.Fechar(this);
        janelaMercado.SetActive(novoEstado);
    }
}
