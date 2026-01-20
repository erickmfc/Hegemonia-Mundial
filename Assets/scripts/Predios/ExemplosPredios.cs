using UnityEngine;

/// <summary>
/// Exemplo: Refinaria de Aço
/// Gera aço por segundo
/// </summary>
public class RefinariaAco : PredioRecursos
{
    void Start()
    {
        // Configuração padrão de uma refinaria
        producaoAco = 3f;         // +3 aço/s
        producaoDinheiro = -1f;   // Consome 1 dinheiro/s (custo operacional)
        
        delayInicial = 5f; // Demora 5s para começar a produzir
        
        base.Start();
    }
}

/// <summary>
/// Exemplo: Usina de Energia
/// Gera energia por segundo
/// </summary>
public class UsinaEnergia : PredioRecursos
{
    void Start()
    {
        producaoEnergia = 10f;    // +10 energia/s
        producaoPetroleo = -0.5f; // Consome 0.5 petróleo/s (combustível)
        
        base.Start();
    }
}

/// <summary>
/// Exemplo: Casa Residencial
/// Aumenta limite de população e gera pequena renda
/// </summary>
public class CasaResidencial : MonoBehaviour
{
    [Header("👥 Configurações")]
    public int aumentoLimitePopulacao = 10;
    public float rendaDinheiro = 1f; // Imposto

    private bool jaRegistrado = false;

    void Start()
    {
        GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
        if (recursos != null)
        {
            // Aumenta limite de população
            recursos.AumentarLimitePopulacao(aumentoLimitePopulacao);
            
            // Adiciona renda de impostos
            recursos.ModificarGanhos(multDinheiro: rendaDinheiro);
            
            jaRegistrado = true;
            
            Debug.Log($"🏠 Casa construída! População máxima +{aumentoLimitePopulacao}, Renda +${rendaDinheiro}/s");
        }
    }

    void OnDestroy()
    {
        if (jaRegistrado)
        {
            GerenciadorRecursos recursos = GerenciadorRecursos.Instancia;
            if (recursos != null)
            {
                // Remove benefícios ao destruir
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
    void Start()
    {
        producaoDinheiro = 20f;   // +20 dinheiro/s (juros)
        
        delayInicial = 10f; // Demora 10s para começar a render
        
        base.Start();
    }
}

/// <summary>
/// Exemplo: Poço de Petróleo
/// Gera petróleo mas consome energia
/// </summary>
public class PocoPetroleo : PredioRecursos
{
    void Start()
    {
        producaoPetroleo = 5f;    // +5 petróleo/s
        producaoEnergia = -2f;    // Consome 2 energia/s (bomba)
        
        base.Start();
    }
}
