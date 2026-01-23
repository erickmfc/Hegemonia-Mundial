using UnityEngine;

[System.Serializable]
public class IA_Economia : MonoBehaviour
{
    [Header("Matemática Financeira")]
    public IA_Comandante chefe;
    
    // Prioridades de Gasto (0 a 100)
    public int prioridadeRecursos = 80; // Petróleo e Aço (Refinarias/Minas)
    public int prioridadeDefesa = 60;   // Torretas
    public int prioridadeExercito = 50; // Tropas

    [Header("Limites")]
    public float reservaMinima = 500f; // Dinheiro que nunca gastamos (emergência)

    public void Inicializar(IA_Comandante comandante)
    {
        chefe = comandante;
    }

    public void ProcessarEconomia()
    {
        // 1. Verifica se precisamos de mais renda
        // Lógica simples: Se a renda for baixa, aumenta prioridade de recursos

        // 2. Verifica se o armazém tá cheio (Simulação)
        // Se cheio, foca em gastar em exército
    }

    // Função para aprovar gastos solicitados pelos outros "cérebros"
    public bool SolicitarVerba(float valor, int nivelPrioridade)
    {
        if (chefe.dinheiro - valor < reservaMinima) return false;
        
        // Se tiver prioridade, gasta
        chefe.GastarDinheiro(valor);
        return true;
    }
}
