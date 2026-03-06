using UnityEngine;
using System.Collections;

/// <summary>
/// Sistema que cobra a "conta" de manutenção das unidades e prédios.
/// Consome recursos periodicamente baseado no Censo Imperial.
/// </summary>
public class GestorDeConsumo : MonoBehaviour
{
    public static GestorDeConsumo Instancia { get; private set; }

    [Header("Custos de Manutenção (Por Unidade/s)")]
    // Quanto cada soldado custa por segundo
    public int custoInfantariaDinheiro = 1; 
    
    // Quanto cada veículo custa por segundo
    public int custoVeiculoPetroleo = 2;
    public int custoVeiculoPeca = 1; // Aço/Peça

    // Quanto cada navio custa
    public int custoNavalPetroleo = 5;
    public int custoNavalDinheiro = 10;

    // Quanto cada aeronave custa
    public int custoAereoPetroleo = 8;
    public int custoAereoDinheiro = 5;

    // Quanto cada prédio custa
    public int custoEstruturaEnergia = 2;

    [Header("Status (Apenas Leitura)")]
    public int totalConsumoDinheiro;
    public int totalConsumoPetroleo;
    public int totalConsumoAco;
    public int totalConsumoEnergia;

    private float timer = 0f;

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
        transform.SetParent(null); // Garante que seja Root GameObject
        DontDestroyOnLoad(gameObject); // Persiste entre cenas
    }

    void Update()
    {
        // Cobra a cada 1 segundo
        timer += Time.deltaTime;
        if (timer >= 1.0f)
        {
            CalcularECobrarManutencao();
            timer = 0f;
        }
    }

    void CalcularECobrarManutencao()
    {
        if (CensoImperial.Instancia == null || GerenciadorRecursos.Instancia == null) return;

        var censo = CensoImperial.Instancia;
        var banco = GerenciadorRecursos.Instancia;

        // 1. Zera contadores
        totalConsumoDinheiro = 0;
        totalConsumoPetroleo = 0;
        totalConsumoAco = 0;
        totalConsumoEnergia = 0;

        // 2. Calcula baseados na quantidade (Censo)
        
        // Infantaria: Gasta Dinheiro (Salário/Comida)
        totalConsumoDinheiro += censo.infantaria * custoInfantariaDinheiro;

        // Veículos: Gastam Petróleo e Peças
        totalConsumoPetroleo += censo.veiculos * custoVeiculoPetroleo;
        totalConsumoAco += censo.veiculos * custoVeiculoPeca;

        // Naval: Gasta muito Petróleo e Dinheiro
        totalConsumoPetroleo += censo.naval * custoNavalPetroleo;
        totalConsumoDinheiro += censo.naval * custoNavalDinheiro;

        // Aéreo: Gasta muito Petróleo
        totalConsumoPetroleo += censo.aereo * custoAereoPetroleo;
        totalConsumoDinheiro += censo.aereo * custoAereoDinheiro;

        // Estruturas: Gastam Energia
        totalConsumoEnergia += censo.estruturas * custoEstruturaEnergia;

        // 3. Aplica a cobrança (Remove do banco)
        if (totalConsumoDinheiro > 0) banco.RemoverRecurso("Dinheiro", totalConsumoDinheiro);
        if (totalConsumoPetroleo > 0) banco.RemoverRecurso("Petroleo", totalConsumoPetroleo);
        if (totalConsumoAco > 0) banco.RemoverRecurso("Aco", totalConsumoAco);
        if (totalConsumoEnergia > 0) banco.RemoverRecurso("Energia", totalConsumoEnergia);

        // Debug (só se tiver consumo relevante)
        if (totalConsumoDinheiro + totalConsumoPetroleo > 10)
        {
            // Debug.Log($"[CONSUMO] Manutenção Cobrada: ${totalConsumoDinheiro}, ⛽{totalConsumoPetroleo}, 🔩{totalConsumoAco}, ⚡{totalConsumoEnergia}");
        }
    }
}
