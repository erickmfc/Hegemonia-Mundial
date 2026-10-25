using UnityEngine;

/// <summary>
/// ScriptableObject que armazena dados de recursos militares
/// (Munição, Mísseis, Explosivos, Equipamento, etc.)
/// </summary>
[CreateAssetMenu(fileName = "Dados_Armazem_Militar", menuName = "Hegemonia/Armazéns/Armazém Militar")]
public class DadosArmazemMilitar : ScriptableObject
{
    [Header("📦 Capacidade do Armazém")]
    [Tooltip("Armazenamento militar ilimitado")]
    public int capacidadeMaxima = int.MaxValue;
    
    [Header("🔫 Munição Leve (Rifles, Pistolas)")]
    public int municaoLeve = 0;
    public int municaoLeveMaximo = int.MaxValue; // Unidades (balas)
    
    [Header("💣 Munição Pesada (Artilharia, Tanques)")]
    public int municaoPesada = 0;
    public int municaoPesadaMaximo = int.MaxValue; // Projéteis
    
    [Header("🚀 Mísseis")]
    public int misseis = 0;
    public int misseisMaximo = int.MaxValue;
    
    [Header("💥 Explosivos (C4, Granadas)")]
    public int explosivos = 0;
    public int explosivosMaximo = int.MaxValue;
    
    [Header("🎖️ Equipamento Militar")]
    public int equipamento = 0;
    public int equipamentoMaximo = int.MaxValue; // Coletes, capacetes, etc
    
    [Header("🛡️ Blindagem (Placas, Reforços)")]
    public int blindagem = 0;
    public int blindagemMaximo = int.MaxValue;

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
        return int.MaxValue;
    }

    /// <summary>
    /// Retorna o percentual de ocupação
    /// </summary>
    public float PercentualOcupacao()
    {
        return 0f;
    }

    /// <summary>
    /// Tenta adicionar recursos militares ao armazém
    /// </summary>
    public bool AdicionarRecursoMilitar(TipoRecursoMilitar tipo, int quantidade)
    {
        if (quantidade <= 0) return false;

        switch (tipo)
        {
            case TipoRecursoMilitar.MunicaoLeve:
                municaoLeve = AdicionarSemLimite(municaoLeve, quantidade);
                return true;
                
            case TipoRecursoMilitar.MunicaoPesada:
                municaoPesada = AdicionarSemLimite(municaoPesada, quantidade);
                return true;
                
            case TipoRecursoMilitar.Misseis:
                misseis = AdicionarSemLimite(misseis, quantidade);
                return true;
                
            case TipoRecursoMilitar.Explosivos:
                explosivos = AdicionarSemLimite(explosivos, quantidade);
                return true;
                
            case TipoRecursoMilitar.Equipamento:
                equipamento = AdicionarSemLimite(equipamento, quantidade);
                return true;
                
            case TipoRecursoMilitar.Blindagem:
                blindagem = AdicionarSemLimite(blindagem, quantidade);
                return true;
        }
        
        return false;
    }

    private static int AdicionarSemLimite(int estoque, int quantidade)
    {
        long total = (long)Mathf.Max(0, estoque) + quantidade;
        return total >= int.MaxValue ? int.MaxValue : (int)total;
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
