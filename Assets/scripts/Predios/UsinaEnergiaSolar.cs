using UnityEngine;

/// <summary>
/// Usina de Painel de Energia (Solar).
/// Gera energia limpa, mas possui custo de manutenção.
/// </summary>
public class UsinaEnergiaSolar : PredioRecursos
{
    [Header("☀️ Configurações Solares")]
    [Tooltip("Eficiência base da usina solar")]
    public float eficienciaBase = 1.0f;

    protected override void Start()
    {
        // Configura os valores de produção herdados de PredioRecursos
        // Estes valores serão usados pelo GerenciadorRecursos automaticamente
        producaoEnergia = 15f * eficienciaBase;
        producaoDinheiro = -3f; // Custo de manutenção (negativo gera gasto)

        // Integração completa com o sistema de cidade (EstruturaEconomica)
        EstruturaEconomica eco = GetComponent<EstruturaEconomica>();
        if (eco == null)
        {
            eco = gameObject.AddComponent<EstruturaEconomica>();
        }

        eco.tipo = TipoEstruturaEconomica.UsinaSolar;
        eco.energiaProduzida = producaoEnergia;
        eco.dinheiroGerado = producaoDinheiro;
        eco.InferirTeamId();

        // Chama o Start da base para ativar a produção no GerenciadorRecursos
        base.Start();
    }

    // A lógica de produção por segundo e remoção ao destruir já é tratada pela classe base PredioRecursos.
}
