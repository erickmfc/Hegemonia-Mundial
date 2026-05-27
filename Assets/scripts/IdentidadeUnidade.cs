using UnityEngine;
using Hegemonia.AI.BrainMaster;

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
    
    [Header("Logistica (GDD)")]
    public int militaresConsumidos;
    public float combustivelPorHora;
    public float energiaConsumida;

    void Start()
    {
        // Registra-se no Censo ao nascer
        if (CensoImperial.Instancia != null)
        {
            CensoImperial.Instancia.RegistrarUnidade(tipoUnidade, teamID, gameObject);
        }
    }

    void OnEnable()
    {
        RegistroEntidadesJogo.Register(this);
        IA_WorldState.Register(this);
    }

    void OnDisable()
    {
        RegistroEntidadesJogo.Unregister(this);
        IA_WorldState.Unregister(this);
        IA_WorldState.InvalidateStructureCache(gameObject.GetInstanceID());
    }

    void OnDestroy()
    {
        // Remove-se do Censo ao morrer
        RegistroEntidadesJogo.Unregister(this);
        IA_WorldState.Unregister(this);
        IA_WorldState.InvalidateStructureCache(gameObject.GetInstanceID());

        if (CensoImperial.Instancia != null)
        {
            CensoImperial.Instancia.RemoverUnidade(tipoUnidade, teamID, gameObject);
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
