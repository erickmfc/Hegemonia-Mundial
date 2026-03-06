using UnityEngine;

/// <summary>
/// O Consulado trata as solicitações de entrada de estrangeiros no "Corredor Nulo" ou nas fronteiras nacionaus.
/// Versão refatorada e limpa. Exclusivamente baseada em lógica de background, sem Menus visuais na tela.
/// </summary>
public class SistemaConsulado : MonoBehaviour
{
    public static SistemaConsulado Instancia;

    public enum PoliticaFronteira { SimAutomatico, NaoAutomatico }
    
    [Header("Diplomacia e Fronteiras")]
    public PoliticaFronteira politicaAtual = PoliticaFronteira.SimAutomatico; // Por padrão, libera a entrada

    void Awake()
    {
        // Padrão Singleton local para que o mundo possa achar a prefeitura do jogador facilmente
        if (Instancia == null) Instancia = this;
        else Destroy(this);
    }

    /// <summary>
    /// Invocada pelas unidades civis ou militares neutras quando tocam no Terreno do Jogador (TeamID 1).
    /// </summary>
    public bool SolicitarEntrada(ControleUnidade visitante)
    {
        if (visitante == null) return false;

        int teamVisitante = visitante.GetComponent<IdentidadeIA>()?.teamID ?? visitante.GetComponent<IdentidadeUnidade>()?.teamID ?? 0;
        string nomeCivil = visitante.gameObject.name.Replace("(Clone)", "").Trim();

        if (politicaAtual == PoliticaFronteira.SimAutomatico)
        {
            visitante.vistoAprovado = true;
            visitante.aguardandoVisto = false;
            Debug.Log($"<color=#00FF00>[Consulado]</color> Fronteiras Abertas. {nomeCivil} da Naçao {teamVisitante} entrou no país.");
            return true;
        }
        else // NaoAutomatico
        {
            Debug.Log($"<color=#FF0000>[Consulado]</color> Fronteiras Fechadas. {nomeCivil} da Naçao {teamVisitante} foi barrado sumariamente.");
            Destroy(visitante.gameObject); // Deleção imediata (deportado)
            return false;
        }
    }

    // Mantido apenas para não acusar erro (CS1061) no momento em que o "ComplexoGovernamental.cs" tentar chamar
    public void AlternarMenuGovernoGlobal()
    {
        Debug.Log("Você clicou na Prefeitura. (Interface gráfica removida a pedido).");
    }
}
