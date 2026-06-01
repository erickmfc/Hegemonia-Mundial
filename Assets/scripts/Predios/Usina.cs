using UnityEngine;

public enum TipoUsina { Solar, Nuclear }

/// <summary>
/// Usina de Energia genérica (Solar e Nuclear).
/// Gera energia para o país e possui custos operacionais e de mão de obra específicos.
/// </summary>
public class Usina : PredioRecursos
{
    [Header("⚡ Tipo de Usina")]
    public TipoUsina tipoUsina = TipoUsina.Solar;

    [Header("⚙️ Configurações")]
    [Tooltip("Eficiência base da usina")]
    public float eficienciaBase = 1.0f;

    protected override void Start()
    {
        // Configura os valores de produção baseados no tipo de usina
        if (tipoUsina == TipoUsina.Solar)
        {
            producaoEnergia = 320f * eficienciaBase;
            producaoDinheiro = -10f; // Custo de manutenção solar
        }
        else if (tipoUsina == TipoUsina.Nuclear)
        {
            producaoEnergia = 2200f * eficienciaBase;
            producaoDinheiro = -100f; // Custo de manutenção nuclear
        }

        // Integração completa com o sistema de cidade (EstruturaEconomica)
        EstruturaEconomica eco = GetComponent<EstruturaEconomica>();
        if (eco == null)
        {
            eco = gameObject.AddComponent<EstruturaEconomica>();
        }

        eco.tipo = (tipoUsina == TipoUsina.Solar) ? TipoEstruturaEconomica.UsinaSolar : TipoEstruturaEconomica.UsinaNuclear;
        eco.energiaProduzida = producaoEnergia;
        eco.dinheiroGerado = producaoDinheiro;
        
        if (tipoUsina == TipoUsina.Nuclear)
        {
            eco.empregosGerados = 2050;
            eco.combustivelConsumido = 3f;
            eco.militaresNecessarios = 400;
        }
        else
        {
            eco.empregosGerados = 90;
            eco.combustivelConsumido = 0f;
            eco.militaresNecessarios = 0;
        }
        
        eco.InferirTeamId();

        // Chama o Start da base para ativar a produção no GerenciadorRecursos
        base.Start();
    }
}
