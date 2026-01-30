using UnityEngine;

/// <summary>
/// Exemplo: Refinaria de Aço
/// Gera aço por segundo
/// </summary>
public class RefinariaAco : PredioRecursos
{
    protected override void Start()
    {
        // ConfiguraÃ§Ã£o padrÃ£o de uma refinaria
        producaoAco = 3f;         // +3 aÃ§o/s
        producaoDinheiro = -1f;   // Consome 1 dinheiro/s (custo operacional)
        
        delayInicial = 5f; // Demora 5s para comeÃ§ar a produzir
        
        base.Start();
    }
}

/// <summary>
/// Exemplo: Usina de Energia
/// Gera energia por segundo
/// </summary>
public class UsinaEnergia : PredioRecursos
{
    protected override void Start()
    {
        producaoEnergia = 10f;    // +10 energia/s
        producaoPetroleo = -0.5f; // Consome 0.5 petrÃ³leo/s (combustÃ­vel)
        
        base.Start();
    }
}

/// <summary>
/// Exemplo: Casa Residencial
/// Aumenta limite de populaÃ§Ã£o e gera pequena renda
/// </summary>
public class CasaResidencial : MonoBehaviour
{
    [Header("ðŸ‘¥ ConfiguraÃ§Ãµes")]
    public int aumentoLimitePopulacao = 10;
    public float rendaDinheiro = 1f; // Imposto

    private bool jaRegistrado = false;

    void Start()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
        {
            // Aumenta limite de populaÃ§Ã£o
            recursos.AumentarLimitePopulacao(aumentoLimitePopulacao);
            
            // Adiciona renda de impostos
            recursos.ModificarGanhos(multDinheiro: rendaDinheiro);
            
            jaRegistrado = true;
            
            Debug.Log($"ðŸ  Casa construÃ­da! PopulaÃ§Ã£o mÃ¡xima +{aumentoLimitePopulacao}, Renda +${rendaDinheiro}/s");
        }
    }

    void OnDestroy()
    {
        if (jaRegistrado)
        {
            GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
            if (recursos != null)
            {
                // Remove benefÃ­cios ao destruir
                recursos.AumentarLimitePopulacao(-aumentoLimitePopulacao);
                recursos.ModificarGanhos(multDinheiro: -rendaDinheiro);
            }
        }
    }
}

/// <summary>
/// Exemplo: Banco
/// Gera muito dinheiro por segundo
/// </summary>
public class Banco : PredioRecursos
{
    protected override void Start()
    {
        producaoDinheiro = 20f;   // +20 dinheiro/s (juros)
        
        delayInicial = 10f; // Demora 10s para comeÃ§ar a render
        
        base.Start();
    }
}

/// <summary>
/// Exemplo: PoÃ§o de PetrÃ³leo
/// Gera petrÃ³leo mas consome energia
/// </summary>
public class PocoPetroleo : PredioRecursos
{
    protected override void Start()
    {
        producaoPetroleo = 5f;    // +5 petrÃ³leo/s
        producaoEnergia = -2f;    // Consome 2 energia/s (bomba)
        
        base.Start();
    }
}
