using UnityEngine;

/// <summary>
/// ScriptableObject que armazena dados de recursos civis
/// (Alimentos, Água, Petróleo, Minerais, etc.)
/// </summary>
[CreateAssetMenu(fileName = "Dados_Armazem_Recursos", menuName = "Hegemonia/Armazéns/Armazém de Recursos")]
public class DadosArmazemRecursos : ScriptableObject
{
    [Header("📦 Capacidade do Armazém")]
    [Tooltip("Capacidade máxima de armazenamento")]
    public int capacidadeMaxima = 10000;
    
    [Header("🌾 Alimentos")]
    public int alimentos = 0;
    public int alimentosMaximo = 5000;
    
    [Header("💧 Água")]
    public int agua = 0;
    public int aguaMaximo = 5000;
    
    [Header("⛽ Petróleo")]
    public int petroleo = 0;
    public int petroleoMaximo = 3000;
    
    [Header("💎 Minerais")]
    public int minerais = 0;
    public int mineraisMaximo = 2000;
    
    [Header("🔩 Aço/Metal")]
    public int metal = 0;
    public int metalMaximo = 2000;
    
    [Header("⚡ Energia Armazenada (Baterias)")]
    public int energia = 0;
    public int energiaMaximo = 1000;

    [Header("📊 Informações")]
    [TextArea(3, 5)]
    public string descricao = "Armazém de recursos civis do país";
    public string localizacao = "Base Principal";

    /// <summary>
    /// Retorna o espaço disponível total
    /// </summary>
    public int EspacoDisponivel()
    {
        int ocupado = alimentos + agua + petroleo + minerais + metal + energia;
        return capacidadeMaxima - ocupado;
    }

    /// <summary>
    /// Retorna o percentual de ocupação
    /// </summary>
    public float PercentualOcupacao()
    {
        int ocupado = alimentos + agua + petroleo + minerais + metal + energia;
        return (float)ocupado / capacidadeMaxima * 100f;
    }

    /// <summary>
    /// Tenta adicionar recursos ao armazém
    /// </summary>
    public bool AdicionarRecurso(TipoRecurso tipo, int quantidade)
    {
        switch (tipo)
        {
            case TipoRecurso.Alimentos:
                if (alimentos + quantidade <= alimentosMaximo && EspacoDisponivel() >= quantidade)
                {
                    alimentos += quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Agua:
                if (agua + quantidade <= aguaMaximo && EspacoDisponivel() >= quantidade)
                {
                    agua += quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Petroleo:
                if (petroleo + quantidade <= petroleoMaximo && EspacoDisponivel() >= quantidade)
                {
                    petroleo += quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Minerais:
                if (minerais + quantidade <= mineraisMaximo && EspacoDisponivel() >= quantidade)
                {
                    minerais += quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Metal:
                if (metal + quantidade <= metalMaximo && EspacoDisponivel() >= quantidade)
                {
                    metal += quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Energia:
                if (energia + quantidade <= energiaMaximo && EspacoDisponivel() >= quantidade)
                {
                    energia += quantidade;
                    return true;
                }
                break;
        }
        
        Debug.LogWarning($"Não foi possível adicionar {quantidade} de {tipo}. Armazém cheio ou limite atingido.");
        return false;
    }

    /// <summary>
    /// Tenta remover recursos do armazém
    /// </summary>
    public bool RemoverRecurso(TipoRecurso tipo, int quantidade)
    {
        switch (tipo)
        {
            case TipoRecurso.Alimentos:
                if (alimentos >= quantidade)
                {
                    alimentos -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Agua:
                if (agua >= quantidade)
                {
                    agua -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Petroleo:
                if (petroleo >= quantidade)
                {
                    petroleo -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Minerais:
                if (minerais >= quantidade)
                {
                    minerais -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Metal:
                if (metal >= quantidade)
                {
                    metal -= quantidade;
                    return true;
                }
                break;
                
            case TipoRecurso.Energia:
                if (energia >= quantidade)
                {
                    energia -= quantidade;
                    return true;
                }
                break;
        }
        
        Debug.LogWarning($"Não há {quantidade} de {tipo} disponível no armazém.");
        return false;
    }

    /// <summary>
    /// Consulta quantidade de um recurso
    /// </summary>
    public int ConsultarRecurso(TipoRecurso tipo)
    {
        switch (tipo)
        {
            case TipoRecurso.Alimentos: return alimentos;
            case TipoRecurso.Agua: return agua;
            case TipoRecurso.Petroleo: return petroleo;
            case TipoRecurso.Minerais: return minerais;
            case TipoRecurso.Metal: return metal;
            case TipoRecurso.Energia: return energia;
            default: return 0;
        }
    }
}

/// <summary>
/// Tipos de recursos que podem ser armazenados
/// </summary>
public enum TipoRecurso
{
    Alimentos,
    Agua,
    Petroleo,
    Minerais,
    Metal,
    Energia
}
