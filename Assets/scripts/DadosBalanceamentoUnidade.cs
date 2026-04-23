using UnityEngine;

[CreateAssetMenu(fileName = "NovoBalanceamentoUnidade", menuName = "Hegemonia/Balanceamento de Unidade")]
public class DadosBalanceamentoUnidade : ScriptableObject
{
    [Header("Leitura Tatica")]
    public string rotuloTipo = string.Empty;
    public string velocidadeExibida = string.Empty;
    public string blindagemExibida = string.Empty;
    public string poderOfensivoExibido = string.Empty;
    [TextArea] public string descricaoTatica = string.Empty;

    [Header("Governanca de Campo")]
    [Min(0)] public int limiteEmCampo = 0;
    [Range(0.5f, 5f)] public float pesoPerformance = 1f;
}
