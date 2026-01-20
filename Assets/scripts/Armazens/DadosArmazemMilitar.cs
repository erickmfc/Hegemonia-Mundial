using UnityEngine;

/// <summary>
/// ScriptableObject que armazena dados de recursos militares
/// (Munição, Mísseis, Explosivos, Equipamento, etc.)
/// </summary>
[CreateAssetMenu(fileName = "Dados_Armazem_Militar", menuName = "Hegemonia/Armazéns/Armazém Militar")]
public class DadosArmazemMilitar : ScriptableObject
{
    [Header("📦 Capacidade do Armazém")]
    [Tooltip("Capacidade máxima de armazenamento militar")]
    public int capacidadeMaxima = 5000;
    
    [Header("🔫 Munição Leve (Rifles, Pistolas)")]
    public int municaoLeve = 0;
    public int municaoLeveMaximo = 10000; // Unidades (balas)
    
    [Header("💣 Munição Pesada (Artilharia, Tanques)")]
    public int municaoPesada = 0;
    public int municaoPesadaMaximo = 1000; // Projéteis
    
    [Header("🚀 Mísseis")]
    public int misseis = 0;
    public int misseisMaximo = 100;
    
    [Header("💥 Explosivos (C4, Granadas)")]
    public int explosivos = 0;
    public int explosivosMaximo = 500;
    
    [Header("🎖️ Equipamento Militar")]
    public int equipamento = 0;
    public int equipamentoMaximo = 1000; // Coletes, capacetes, etc
    
    [Header("🛡️ Blindagem (Placas, Reforços)")]
    public int blindagem = 0;
    public int blindagemMaximo = 200;

    [Header("📊 Informações")]
    [TextArea(3, 5)]
    public string descricao = "Armazém militar estratégico";
    public string localizacao = "Base Militar Principal";
    public int nivelSeguranca = 5; // 1-10

    /// <summary>
    /// Retorna o espaço disponível total
    /// </summary>
    public int EspacoDisponivel()
    {
        // Considera peso diferente para cada tipo
        int ocupado = (municaoLeve / 100) + municaoPesada + (misseis * 10) + 
                      explosivos + equipamento + (blindagem * 5);
        return capacidadeMaxima - ocupado;
    }

    /// <summary>
    /// Retorna o percentual de ocupação
    /// </summary>
    public float PercentualOcupacao()
    {
        int ocupado = (municaoLeve / 100) + municaoPesada + (misseis * 10) + 
                      explosivos + equipamento + (blindagem * 5);
        return (float)ocupado / capacidadeMaxima * 100f;
    }

    /// <summary>
    /// Tenta adicionar recursos militares ao armazém
    /// </summary>
    public bool AdicionarRecursoMilitar(TipoRecursoMilitar tipo, int quantidade)
    {
        switch (tipo)
        {
            case TipoRecursoMilitar.MunicaoLeve:
                if (municaoLeve + quantidade <= municaoLeveMaximo)
                {
                    municaoLeve += quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.MunicaoPesada:
                if (municaoPesada + quantidade <= municaoPesadaMaximo && EspacoDisponivel() >= quantidade)
                {
                    municaoPesada += quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.Misseis:
                if (misseis + quantidade <= misseisMaximo && EspacoDisponivel() >= (quantidade * 10))
                {
                    misseis += quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.Explosivos:
                if (explosivos + quantidade <= explosivosMaximo && EspacoDisponivel() >= quantidade)
                {
                    explosivos += quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.Equipamento:
                if (equipamento + quantidade <= equipamentoMaximo && EspacoDisponivel() >= quantidade)
                {
                    equipamento += quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.Blindagem:
                if (blindagem + quantidade <= blindagemMaximo && EspacoDisponivel() >= (quantidade * 5))
                {
                    blindagem += quantidade;
                    return true;
                }
                break;
        }
        
        Debug.LogWarning($"⚠️ Não foi possível adicionar {quantidade} de {tipo}. Armazém militar cheio ou limite atingido.");
        return false;
    }

    /// <summary>
    /// Tenta remover recursos militares do armazém
    /// </summary>
    public bool RemoverRecursoMilitar(TipoRecursoMilitar tipo, int quantidade)
    {
        switch (tipo)
        {
            case TipoRecursoMilitar.MunicaoLeve:
                if (municaoLeve >= quantidade)
                {
                    municaoLeve -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.MunicaoPesada:
                if (municaoPesada >= quantidade)
                {
                    municaoPesada -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.Misseis:
                if (misseis >= quantidade)
                {
                    misseis -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.Explosivos:
                if (explosivos >= quantidade)
                {
                    explosivos -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.Equipamento:
                if (equipamento >= quantidade)
                {
                    equipamento -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecursoMilitar.Blindagem:
                if (blindagem >= quantidade)
                {
                    blindagem -= quantidade;
                    return true;
                }
                break;
        }
        
        Debug.LogWarning($"❌ Não há {quantidade} de {tipo} disponível no armazém militar.");
        return false;
    }

    /// <summary>
    /// Consulta quantidade de um recurso militar
    /// </summary>
    public int ConsultarRecursoMilitar(TipoRecursoMilitar tipo)
    {
        switch (tipo)
        {
            case TipoRecursoMilitar.MunicaoLeve: return municaoLeve;
            case TipoRecursoMilitar.MunicaoPesada: return municaoPesada;
            case TipoRecursoMilitar.Misseis: return misseis;
            case TipoRecursoMilitar.Explosivos: return explosivos;
            case TipoRecursoMilitar.Equipamento: return equipamento;
            case TipoRecursoMilitar.Blindagem: return blindagem;
            default: return 0;
        }
    }

    /// <summary>
    /// Verifica se tem munição suficiente para equipar unidades
    /// </summary>
    public bool TemMunicaoParaUnidade(int quantidadeUnidades)
    {
        int municaoNecessaria = quantidadeUnidades * 30; // 30 balas por soldado
        return municaoLeve >= municaoNecessaria;
    }
}

/// <summary>
/// Tipos de recursos militares que podem ser armazenados
/// </summary>
public enum TipoRecursoMilitar
{
    MunicaoLeve,
    MunicaoPesada,
    Misseis,
    Explosivos,
    Equipamento,
    Blindagem
}
