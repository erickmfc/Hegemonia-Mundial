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

    [Header("Classificação Militar")]
    public TipoUnidade tipoUnidade = TipoUnidade.Infantaria;

    void Start()
    {
        // Registra-se no Censo ao nascer
        if (CensoImperial.Instancia != null)
        {
            CensoImperial.Instancia.RegistrarUnidade(tipoUnidade, teamID);
        }
    }

    void OnDestroy()
    {
        // Remove-se do Censo ao morrer
        if (CensoImperial.Instancia != null)
        {
            CensoImperial.Instancia.RemoverUnidade(tipoUnidade, teamID);
        }
    }
}

public enum TipoUnidade
{
    Infantaria, // Soldados
    Veiculo,    // Tanques, Caminhões
    Naval,      // Navios, Submarinos
    Aereo,      // Aviões, Helicópteros
    Estrutura   // Prédios, Defesas
}
