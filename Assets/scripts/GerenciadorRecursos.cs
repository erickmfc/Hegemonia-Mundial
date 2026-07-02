using UnityEngine;
using System;

/// <summary>
/// Sistema centralizado de gerenciamento de recursos do jogo.
/// Agora compatível com Plataforma Offshore e Sistema de Consumo.
/// </summary>
public class GerenciadorRecursos : MonoBehaviour
{
    public static GerenciadorRecursos Instancia { get; private set; }

    [Header("💰 Recursos Principais")]
    public int dinheiro = 5000;
    public int petroleo = 500;
    public int aco = 300;
    public int populacaoAtual = 5000;
    public int populacaoMaxima = 5000;
    public int energia = 100;
    public int comida = 100;

    [Header("📈 Ganhos Passivos (Base)")]
    public float dinheiroPorSegundo = 10f;
    public float petroleoPorSegundo = 0f; // Zerado! Depende da Plataforma agora.
    public float acoPorSegundo = 5f;
    public float energiaPorSegundo = 0f;
    public float comidaPorSegundo = 0f;

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
            // CORREÇÃO: Se já existe um gerente (veio do Menu) mas estamos iniciando do zero (Jogo Novo/Tutorial),
            // fazemos com que o gerente antigo herde os valores que você digitou no Inspector DESTA cena atual.
            if (SistemaSaveGame.Instancia != null && !SistemaSaveGame.Instancia.carregouDeSave)
            {
                Instancia.dinheiro = this.dinheiro;
                Instancia.petroleo = this.petroleo;
                Instancia.aco = this.aco;
                Instancia.energia = this.energia;
                Instancia.comida = this.comida;
                Instancia.populacaoAtual = this.populacaoAtual;
                Instancia.populacaoMaxima = this.populacaoMaxima;
                
                // Sincroniza ganhos passivos também
                Instancia.dinheiroPorSegundo = this.dinheiroPorSegundo;
                Instancia.acoPorSegundo = this.acoPorSegundo;
                
                Instancia.NotificarAtualizacao();
            }

            Destroy(gameObject);
            return;
        }

        // CORREÇÃO: Zera ganhos passivos que agora dependem de logística
        petroleoPorSegundo = 0f; 
    }

    void Start()
    {
        if (SistemaSaveGame.Instancia != null && SistemaSaveGame.Instancia.carregouDeSave && SistemaSaveGame.Instancia.dadosAtuais != null)
        {
            dinheiro = SistemaSaveGame.Instancia.dadosAtuais.creditosJogador;
            petroleo = SistemaSaveGame.Instancia.dadosAtuais.petroleoJogador;
            aco = SistemaSaveGame.Instancia.dadosAtuais.acoJogador;
            energia = SistemaSaveGame.Instancia.dadosAtuais.energiaJogador;
            comida = SistemaSaveGame.Instancia.dadosAtuais.comidaJogador;
            NotificarAtualizacao();
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
            comida += Mathf.RoundToInt(comidaPorSegundo);
            
            ValidarLimites();
            
            tempoAcumulado = 0f;
            NotificarAtualizacao();
        }
    }

    // ==============================================================================
    // 🆕 NOVOS MÉTODOS PARA A PLATAFORMA E SISTEMA DE CONSUMO
    // ==============================================================================

    /// <summary>
    /// Usado pela Plataforma Offshore para injetar recursos
    /// </summary>
    public void AdicionarRecurso(string tipo, int quantidade)
    {
        switch (tipo)
        {
            case "Petroleo":
                petroleo += quantidade;
                break;
            case "Dinheiro":
                dinheiro += quantidade;
                break;
            case "Aco":
                aco += quantidade;
                break;
            case "Energia":
                energia += quantidade;
                break;
            case "Comida":
                comida += quantidade;
                break;
        }
        NotificarAtualizacao();
    }

    /// <summary>
    /// Usado pelo GestorDeConsumo para cobrar a conta
    /// </summary>
    public void RemoverRecurso(string tipo, int quantidade)
    {
        switch (tipo)
        {
            case "Petroleo":
                petroleo -= quantidade;
                break;
            case "Dinheiro":
                dinheiro -= quantidade;
                break;
            case "Aco":
                aco -= quantidade;
                break;
            case "Energia":
                energia -= quantidade;
                break;
            case "Comida":
                comida -= quantidade;
                break;
        }

        ValidarLimites(); // Garante que não fique negativo
        NotificarAtualizacao();
    }

    // Garante que recursos não fiquem negativos
    void ValidarLimites()
    {
        dinheiro = Mathf.Max(0, dinheiro);
        petroleo = Mathf.Max(0, petroleo);
        aco = Mathf.Max(0, aco);
        energia = Mathf.Max(0, energia);
        comida = Mathf.Max(0, comida);
    }

    // ==============================================================================
    // FIM DOS NOVOS MÉTODOS
    // ==============================================================================

    /// <summary>
    /// Tenta gastar recursos. Retorna true se houver recursos suficientes.
    /// </summary>
    public bool TentarGastar(int custoDinheiro = 0, int custoPetroleo = 0, int custoAco = 0, int custoEnergia = 0)
    {
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
        
        Debug.LogWarning($"[RECURSOS] Insuficiente! Precisa: ${custoDinheiro}, P:{custoPetroleo}, A:{custoAco}, E:{custoEnergia}");
        return false;
    }

    public void AdicionarRecursos(int addDinheiro = 0, int addPetroleo = 0, int addAco = 0, int addEnergia = 0)
    {
        dinheiro += addDinheiro;
        petroleo += addPetroleo;
        aco += addAco;
        energia += addEnergia;
        NotificarAtualizacao();
    }

    public void ModificarGanhos(float multDinheiro = 0, float multPetroleo = 0, float multAco = 0, float multEnergia = 0, float multComida = 0)
    {
        dinheiroPorSegundo += multDinheiro;
        petroleoPorSegundo += multPetroleo;
        acoPorSegundo += multAco;
        energiaPorSegundo += multEnergia;
        comidaPorSegundo += multComida;
        NotificarAtualizacao();
    }

    public bool PodeAdicionarPopulacao(int quantidade)
    {
        return (populacaoAtual + quantidade) <= populacaoMaxima;
    }

    public bool AdicionarPopulacao(int quantidade)
    {
        if (PodeAdicionarPopulacao(quantidade))
        {
            populacaoAtual += quantidade;
            NotificarAtualizacao();
            return true;
        }
        return false;
    }

    public void RemoverPopulacao(int quantidade)
    {
        populacaoAtual -= quantidade;
        populacaoAtual = Mathf.Max(0, populacaoAtual);
        NotificarAtualizacao();
    }

    public void AumentarLimitePopulacao(int quantidade)
    {
        populacaoMaxima += quantidade;
        NotificarAtualizacao();
    }

    void NotificarAtualizacao()
    {
        OnRecursosAtualizados?.Invoke();
    }

    // Compatibilidade Legada
    public bool TentarGastarDinheiro(int custo)
    {
        return TentarGastar(custoDinheiro: custo);
    }


}