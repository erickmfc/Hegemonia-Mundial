using UnityEngine;
using System;

/// <summary>
/// Sistema centralizado de gerenciamento de recursos do jogo.
/// Controla todos os recursos (dinheiro, petróleo, aço, população, etc.)
/// e calcula ganhos/gastos por segundo.
/// </summary>
public class GerenciadorRecursos : MonoBehaviour
{
    public static GerenciadorRecursos Instancia { get; private set; }

    [Header("💰 Recursos Principais")]
    public int dinheiro = 5000;
    public int petroleo = 500;
    public int aco = 300;
    public int populacaoAtual = 10;
    public int populacaoMaxima = 100;
    public int energia = 100;

    [Header("📈 Ganhos por Segundo")]
    public float dinheiroPorSegundo = 10f;
    public float petroleoPorSegundo = 2f;
    public float acoPorSegundo = 5f;
    public float energiaPorSegundo = 0f;

    [Header("⚙️ Configurações")]
    public bool ativarGanhosAutomaticos = true;
    
    // Eventos para notificar quando recursos mudarem
    public event Action OnRecursosAtualizados;

    private float tempoAcumulado = 0f;

    void Awake()
    {
        // Singleton pattern
        if (Instancia == null)
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (ativarGanhosAutomaticos)
        {
            ProcessarGanhosPorSegundo();
        }
    }

    /// <summary>
    /// Processa os ganhos automáticos de recursos a cada segundo
    /// </summary>
    void ProcessarGanhosPorSegundo()
    {
        tempoAcumulado += Time.deltaTime;
        
        if (tempoAcumulado >= 1f)
        {
            dinheiro += Mathf.RoundToInt(dinheiroPorSegundo);
            petroleo += Mathf.RoundToInt(petroleoPorSegundo);
            aco += Mathf.RoundToInt(acoPorSegundo);
            energia += Mathf.RoundToInt(energiaPorSegundo);
            
            // Garante que recursos não fiquem negativos
            dinheiro = Mathf.Max(0, dinheiro);
            petroleo = Mathf.Max(0, petroleo);
            aco = Mathf.Max(0, aco);
            energia = Mathf.Max(0, energia);
            
            tempoAcumulado = 0f;
            NotificarAtualizacao();
        }
    }

    /// <summary>
    /// Tenta gastar recursos. Retorna true se houver recursos suficientes.
    /// </summary>
    public bool TentarGastar(int custoDinheiro = 0, int custoPetroleo = 0, int custoAco = 0, int custoEnergia = 0)
    {
        // Verifica se tem recursos suficientes
        if (dinheiro >= custoDinheiro && 
            petroleo >= custoPetroleo && 
            aco >= custoAco && 
            energia >= custoEnergia)
        {
            dinheiro -= custoDinheiro;
            petroleo -= custoPetroleo;
            aco -= custoAco;
            energia -= custoEnergia;
            
            NotificarAtualizacao();
            return true;
        }
        
        Debug.LogWarning($"❌ Recursos insuficientes! Precisa: ${custoDinheiro}, ⛽{custoPetroleo}, 🔩{custoAco}, ⚡{custoEnergia}");
        return false;
    }

    /// <summary>
    /// Adiciona recursos (útil para capturas, bônus, etc.)
    /// </summary>
    public void AdicionarRecursos(int addDinheiro = 0, int addPetroleo = 0, int addAco = 0, int addEnergia = 0)
    {
        dinheiro += addDinheiro;
        petroleo += addPetroleo;
        aco += addAco;
        energia += addEnergia;
        
        NotificarAtualizacao();
    }

    /// <summary>
    /// Modifica os ganhos por segundo (útil para upgrades)
    /// </summary>
    public void ModificarGanhos(float multDinheiro = 0, float multPetroleo = 0, float multAco = 0, float multEnergia = 0)
    {
        dinheiroPorSegundo += multDinheiro;
        petroleoPorSegundo += multPetroleo;
        acoPorSegundo += multAco;
        energiaPorSegundo += multEnergia;
        
        NotificarAtualizacao();
    }

    /// <summary>
    /// Verifica se pode adicionar mais população
    /// </summary>
    public bool PodeAdicionarPopulacao(int quantidade)
    {
        return (populacaoAtual + quantidade) <= populacaoMaxima;
    }

    /// <summary>
    /// Adiciona população (ao criar unidades)
    /// </summary>
    public bool AdicionarPopulacao(int quantidade)
    {
        if (PodeAdicionarPopulacao(quantidade))
        {
            populacaoAtual += quantidade;
            NotificarAtualizacao();
            return true;
        }
        
        Debug.LogWarning($"❌ Limite de população atingido! ({populacaoAtual}/{populacaoMaxima})");
        return false;
    }

    /// <summary>
    /// Remove população (quando unidades morrem)
    /// </summary>
    public void RemoverPopulacao(int quantidade)
    {
        populacaoAtual -= quantidade;
        populacaoAtual = Mathf.Max(0, populacaoAtual);
        NotificarAtualizacao();
    }

    /// <summary>
    /// Aumenta o limite máximo de população
    /// </summary>
    public void AumentarLimitePopulacao(int quantidade)
    {
        populacaoMaxima += quantidade;
        NotificarAtualizacao();
    }

    /// <summary>
    /// Notifica todos os listeners que os recursos foram atualizados
    /// </summary>
    void NotificarAtualizacao()
    {
        OnRecursosAtualizados?.Invoke();
    }

    // ========== COMPATIBILIDADE COM GERENTE DE JOGO ANTIGO ==========
    
    /// <summary>
    /// Método de compatibilidade com o código antigo do GerenteDeJogo
    /// </summary>
    public bool TentarGastarDinheiro(int custo)
    {
        return TentarGastar(custoDinheiro: custo);
    }

    /// <summary>
    /// Propriedade de compatibilidade para dinheiroAtual
    /// </summary>
    public int dinheiroAtual
    {
        get { return dinheiro; }
        set { dinheiro = value; NotificarAtualizacao(); }
    }
}
