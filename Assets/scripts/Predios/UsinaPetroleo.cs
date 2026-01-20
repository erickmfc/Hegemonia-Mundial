using UnityEngine;

/// <summary>
/// Exemplo: Usina de Petróleo
/// Adicione este script aos prefabs de usinas de petróleo
/// </summary>
public class UsinaPetroleo : PredioRecursos
{
    [Header("⛽ Configurações da Usina")]
    [Tooltip("Nível atual da usina (1, 2, 3...)")]
    public int nivel = 1;

    void Start()
    {
        // Configura produção baseada no nível
        ConfigurarProducaoPorNivel();
        
        // Chama o Start da classe pai
        base.Start();
    }

    void ConfigurarProducaoPorNivel()
    {
        switch (nivel)
        {
            case 1:
                producaoPetroleo = 2f;  // Nível 1: +2 petróleo/s
                break;
            case 2:
                producaoPetroleo = 5f;  // Nível 2: +5 petróleo/s
                break;
            case 3:
                producaoPetroleo = 10f; // Nível 3: +10 petróleo/s
                break;
            default:
                producaoPetroleo = nivel * 2f; // Níveis maiores
                break;
        }

        Debug.Log($"⛽ Usina de Petróleo Nível {nivel} configurada: +{producaoPetroleo}/s");
    }

    /// <summary>
    /// Faz upgrade da usina para o próximo nível
    /// </summary>
    public void FazerUpgrade()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        
        // Custo do upgrade (aumenta por nível)
        int custoDinheiro = nivel * 500;
        int custoAco = nivel * 50;

        if (recursos.TentarGastar(custoDinheiro: custoDinheiro, custoAco: custoAco))
        {
            nivel++;
            ConfigurarProducaoPorNivel();
            
            // Reinicia produção com novos valores
            if (estaProduzindo)
            {
                DesativarProducao();
                AtivarProducao();
            }

            Debug.Log($"⬆️ Usina de Petróleo upgradada para Nível {nivel}!");
        }
        else
        {
            Debug.Log($"❌ Recursos insuficientes para upgrade! Precisa: ${custoDinheiro} e 🔩{custoAco}");
        }
    }
}
