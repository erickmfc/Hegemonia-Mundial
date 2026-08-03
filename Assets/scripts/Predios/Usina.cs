using UnityEngine;

public enum TipoUsina { Solar, Nuclear, Carvao }

/// <summary>
/// Usina de Energia genérica (Solar e Nuclear).
/// Gera energia para o país e possui custos operacionais e de mão de obra específicos.
/// </summary>
public class Usina : MonoBehaviour
{
    [Header("⚡ Tipo de Usina")]
    public TipoUsina tipoUsina = TipoUsina.Solar;

    [Header("⚙️ Configurações")]
    [Tooltip("Eficiência base da usina")]
    public float eficienciaBase = 1.0f;

    void Start()
    {
        float producaoEnergia = 0f;
        float producaoDinheiro = 0f;

        // Configura os valores de produção baseados no tipo de usina
        if (tipoUsina == TipoUsina.Solar)
        {
            // Solar foi reduzida em 50%; carvão usa 1,5x esta produção.
            producaoEnergia = 160f * eficienciaBase;
            producaoDinheiro = -10f; // Custo de manutenção solar
        }
        else if (tipoUsina == TipoUsina.Nuclear)
        {
            producaoEnergia = 2200f * eficienciaBase;
            producaoDinheiro = -100f; // Custo de manutenção nuclear
        }
        else if (tipoUsina == TipoUsina.Carvao)
        {
            producaoEnergia = 240f * eficienciaBase;
            producaoDinheiro = -180f; // Combustível, filtros, cinzas e controle ambiental
        }

        // Integração completa com o sistema de cidade (EstruturaEconomica)
        EstruturaEconomica eco = GetComponent<EstruturaEconomica>();
        if (eco == null)
        {
            eco = gameObject.AddComponent<EstruturaEconomica>();
        }

        eco.tipo = tipoUsina == TipoUsina.Solar
            ? TipoEstruturaEconomica.UsinaSolar
            : (tipoUsina == TipoUsina.Carvao ? TipoEstruturaEconomica.UsinaCarvao : TipoEstruturaEconomica.UsinaNuclear);
        eco.energiaProduzida = producaoEnergia;
        eco.dinheiroGerado = producaoDinheiro;
        
        if (tipoUsina == TipoUsina.Nuclear)
        {
            eco.empregosGerados = 2050;
            eco.combustivelConsumido = 3f;
            eco.militaresNecessarios = 400;
        }
        else if (tipoUsina == TipoUsina.Carvao)
        {
            eco.empregosGerados = 300;
            eco.combustivelConsumido = 42f;
            eco.militaresNecessarios = 0;
        }
        else
        {
            eco.empregosGerados = 90;
            eco.combustivelConsumido = 0f;
            eco.militaresNecessarios = 0;
        }
        
        eco.InferirTeamId();
    }
}
