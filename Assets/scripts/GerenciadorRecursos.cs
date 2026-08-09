using UnityEngine;
using System;
using Hegemonia.RTS;

/// <summary>
/// Sistema centralizado de gerenciamento de recursos do jogo.
/// Agora compatível com Plataforma Offshore e Sistema de Consumo.
/// </summary>
public class GerenciadorRecursos : MonoBehaviour
{
    public static GerenciadorRecursos Instancia { get; private set; }

    [Header("💰 Recursos Principais")]
    public long dinheiro = 5000L;
    public int petroleo = 500;
    public int aco = 300;
    public int populacaoAtual = 3200;
    public int populacaoMaxima = 3200;
    public int energia = 100;
    public int comida = 100;

    [Header("📈 Ganhos Passivos (Base)")]
    // O caixa começa sem renda artificial. A renda passa a vir do orçamento
    // nacional depois que houver atividade econômica real.
    public float dinheiroPorSegundo = 0f;
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
            if (SistemaSaveGame.Instancia != null
                && !SistemaSaveGame.Instancia.carregouDeSave
                && !SistemaSaveGame.Instancia.partidaNovaRecemIniciada)
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
        else if (SistemaSaveGame.Instancia != null && SistemaSaveGame.Instancia.partidaNovaRecemIniciada && SistemaSaveGame.Instancia.dadosAtuais != null)
        {
            dinheiro = SistemaSaveGame.Instancia.dadosAtuais.creditosJogador;
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
            dinheiro += (long)Math.Round(dinheiroPorSegundo, MidpointRounding.AwayFromZero);
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
    public void AdicionarRecurso(string tipo, long quantidade)
    {
        int quantidadeRecurso = quantidade > int.MaxValue ? int.MaxValue : quantidade < int.MinValue ? int.MinValue : (int)quantidade;
        switch (tipo)
        {
            case "Petroleo":
                petroleo += quantidadeRecurso;
                break;
            case "Dinheiro":
                dinheiro += quantidade;
                break;
            case "Aco":
                aco += quantidadeRecurso;
                break;
            case "Energia":
                energia += quantidadeRecurso;
                break;
            case "Comida":
                comida += quantidadeRecurso;
                break;
        }
        NotificarAtualizacao();
    }

    /// <summary>
    /// Usado pelo GestorDeConsumo para cobrar a conta
    /// </summary>
    public void RemoverRecurso(string tipo, long quantidade)
    {
        int quantidadeRecurso = quantidade > int.MaxValue ? int.MaxValue : quantidade < int.MinValue ? int.MinValue : (int)quantidade;
        switch (tipo)
        {
            case "Petroleo":
                petroleo -= quantidadeRecurso;
                break;
            case "Dinheiro":
                dinheiro -= quantidade;
                break;
            case "Aco":
                aco -= quantidadeRecurso;
                break;
            case "Energia":
                energia -= quantidadeRecurso;
                break;
            case "Comida":
                comida -= quantidadeRecurso;
                break;
        }

        ValidarLimites(); // Garante que não fique negativo
        NotificarAtualizacao();
    }

    // Garante que recursos não fiquem negativos
    void ValidarLimites()
    {
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
    public bool TentarGastar(long custoDinheiro = 0L, int custoPetroleo = 0, int custoAco = 0, int custoEnergia = 0)
    {
        RTSResourceLedgerService ledger = RTSResourceLedgerService.Instancia;
        if (ledger != null && !ledger.IsApplyingTransaction)
        {
            return ledger.TrySpendPlayer(new RTSResourceCost(custoDinheiro, custoPetroleo, custoAco, custoEnergia), "legacy spend");
        }

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

    public void AdicionarRecursos(long addDinheiro = 0L, int addPetroleo = 0, int addAco = 0, int addEnergia = 0)
    {
        RTSResourceLedgerService ledger = RTSResourceLedgerService.Instancia;
        if (ledger != null && !ledger.IsApplyingTransaction)
        {
            ledger.AddPlayer(new RTSResourceCost(addDinheiro, addPetroleo, addAco, addEnergia), "legacy add");
            return;
        }

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

    public void NotificarAtualizacao()
    {
        OnRecursosAtualizados?.Invoke();
    }

    // Compatibilidade Legada
    public bool TentarGastarDinheiro(long custo)
    {
        return TentarGastar(custoDinheiro: custo);
    }


}
