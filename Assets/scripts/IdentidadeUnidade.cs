using UnityEngine;

public class IdentidadeUnidade : MonoBehaviour
{
    [Header("Identificação Global")]
    // ID 1 = Jogador (Sua Nação)
    // ID 2 = Inimigo (Nação Rival)
    // ID 0 = Neutro
    public int teamID = 1; 

    [Header("Dados do País")]
    public string nomeDoPais = "Hegemonia";

    // Função para aplicar a estabilidade Antygaviti que usamos no projeto
    public float antygavitiEstabilidade = 5f; 
}
