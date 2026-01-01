using UnityEngine;

// 1. O ESQUELETO DA FICHA TÉCNICA (ScriptableObject)
// Isso cria uma nova opção no menu do Unity: botão direito > Create > Hegemonia > Ficha de Construcao
[CreateAssetMenu(fileName = "NovaConstrucao", menuName = "Hegemonia/Ficha de Construcao")]
public class DadosConstrucao : ScriptableObject
{
    public enum CategoriaItem
    {
        Exercito,       // Tropas terrestres (antigo Militar)
        Marinha,        // Navios e unidades navais
        Aeronautica,    // Aviões e helicópteros
        Tecnologia,     // Pesquisas e upgrades (novo nome para Militar)
        Infraestrutura,
        Energia,
        Urbana
    }

    [Header("Informações Básicas")]
    public string nomeItem = "Nome da Unidade";
    [TextArea] public string descricao = "Descrição curta...";
    public Sprite icone; // A foto que vai no botão
    
    [Header("Técnico")]
    [Tooltip("Arraste aqui o objeto AZUL da pasta (Prefab), NÃO arraste da cena!")]
    public GameObject prefabDaUnidade; // O objeto 3D que vai ser construído
    public int preco = 100;
    
    [Header("Classificação")]
    public CategoriaItem categoria;
}