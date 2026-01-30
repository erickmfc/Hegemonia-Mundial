using UnityEngine;
using UnityEngine.UI;

public class ComandoAviaoUI : MonoBehaviour
{
    private TorreDeControle torre;
    private ControleAviaoCaca aviaoAlvo;

    [Header("Botões Internos")]
    public Button btnPousar;
    public Button btnPatrulhar;
    public Button btnSelecionar;

    public void Configurar(TorreDeControle torreRef, ControleAviaoCaca aviaoRef)
    {
        torre = torreRef;
        aviaoAlvo = aviaoRef;

        // Limpa listeners antigos
        if(btnPousar) btnPousar.onClick.RemoveAllListeners();
        if(btnPatrulhar) btnPatrulhar.onClick.RemoveAllListeners();
        if(btnSelecionar) btnSelecionar.onClick.RemoveAllListeners();

        // Adiciona novos
        if(btnPousar) btnPousar.onClick.AddListener(() => torre.OrdenarPouso(aviaoAlvo));
        if(btnPatrulhar) btnPatrulhar.onClick.AddListener(() => torre.OrdenarPatrulha(aviaoAlvo));
        if(btnSelecionar) btnSelecionar.onClick.AddListener(() => torre.SelecionarAviao(aviaoAlvo));
    }
}
