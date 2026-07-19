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

    // ──────────────────────────────────────────────
    // ⛏️ MINÉRIOS EXTRAÍDOS (Sistema de Ordens)
    // ──────────────────────────────────────────────

    [Header("⛏️ Ferro (t))")]
    public float ferro = 0f;
    public float ferroMaximo = 100000f;

    [Header("🟠 Cobre (t)")]
    public float cobre = 0f;
    public float cobreMaximo = 50000f;

    [Header("🪶 Bauxita (t)")]
    public float bauxita = 0f;
    public float bauxitaMaximo = 50000f;

    [Header("🔵 Titânio (t)")]
    public float titanio = 0f;
    public float titanioMaximo = 30000f;

    [Header("☢️ Urânio (t)")]
    public float uranio = 0f;
    public float uranioMaximo = 10000f;

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

    // ──────────────────────────────────────────────
    // MÉTODOS PARA SISTEMA DE ORDENS DE EXTRAÇÃO
    // ──────────────────────────────────────────────

    /// <summary>
    /// Adiciona minério ao campo correspondente, respeitando o limite máximo.
    /// Retorna a quantidade efetivamente adicionada (pode ser menor se o armazém estiver quase cheio).
    /// </summary>
    public float AdicionarMinerio(TipoRecursoExtracao tipo, float quantidade)
    {
        if (quantidade <= 0f) return 0f;

        switch (tipo)
        {
            case TipoRecursoExtracao.Ferro:
                float addFerro = Mathf.Min(quantidade, ferroMaximo - ferro);
                ferro += addFerro;
                if (addFerro < quantidade)
                    Debug.LogWarning($"[Armazém] Armazém de Ferro cheio! Perdeu {quantidade - addFerro:N0} t.");
                return addFerro;

            case TipoRecursoExtracao.Cobre:
                float addCobre = Mathf.Min(quantidade, cobreMaximo - cobre);
                cobre += addCobre;
                if (addCobre < quantidade)
                    Debug.LogWarning($"[Armazém] Armazém de Cobre cheio! Perdeu {quantidade - addCobre:N0} t.");
                return addCobre;

            case TipoRecursoExtracao.Bauxita:
                float addBauxita = Mathf.Min(quantidade, bauxitaMaximo - bauxita);
                bauxita += addBauxita;
                if (addBauxita < quantidade)
                    Debug.LogWarning($"[Armazém] Armazém de Bauxita cheio! Perdeu {quantidade - addBauxita:N0} t.");
                return addBauxita;

            case TipoRecursoExtracao.Titanio:
                float addTitanio = Mathf.Min(quantidade, titanioMaximo - titanio);
                titanio += addTitanio;
                if (addTitanio < quantidade)
                    Debug.LogWarning($"[Armazém] Armazém de Titânio cheio! Perdeu {quantidade - addTitanio:N0} t.");
                return addTitanio;

            case TipoRecursoExtracao.Uranio:
                float addUranio = Mathf.Min(quantidade, uranioMaximo - uranio);
                uranio += addUranio;
                if (addUranio < quantidade)
                    Debug.LogWarning($"[Armazém] Armazém de Urânio cheio! Perdeu {quantidade - addUranio:N0} t.");
                return addUranio;
        }
        return 0f;
    }

    /// <summary>
    /// Consulta o estoque atual de um minério (em toneladas).
    /// </summary>
    public float ConsultarMinerio(TipoRecursoExtracao tipo)
    {
        switch (tipo)
        {
            case TipoRecursoExtracao.Ferro:    return ferro;
            case TipoRecursoExtracao.Cobre:    return cobre;
            case TipoRecursoExtracao.Bauxita:  return bauxita;
            case TipoRecursoExtracao.Titanio:  return titanio;
            case TipoRecursoExtracao.Uranio:   return uranio;
            default: return 0f;
        }
    }

    /// <summary>
    /// Remove minério do armazém. Retorna true se havia quantidade suficiente.
    /// </summary>
    public bool RemoverMinerio(TipoRecursoExtracao tipo, float quantidade)
    {
        switch (tipo)
        {
            case TipoRecursoExtracao.Ferro:
                if (ferro >= quantidade) { ferro -= quantidade; return true; } break;
            case TipoRecursoExtracao.Cobre:
                if (cobre >= quantidade) { cobre -= quantidade; return true; } break;
            case TipoRecursoExtracao.Bauxita:
                if (bauxita >= quantidade) { bauxita -= quantidade; return true; } break;
            case TipoRecursoExtracao.Titanio:
                if (titanio >= quantidade) { titanio -= quantidade; return true; } break;
            case TipoRecursoExtracao.Uranio:
                if (uranio >= quantidade) { uranio -= quantidade; return true; } break;
        }
        Debug.LogWarning($"[Armazém] Não há {quantidade:N0} t de {tipo} disponível.");
        return false;
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
        
        // Removido para evitar spam no console
        // Debug.LogWarning($"Não foi possível adicionar {quantidade} de {tipo}. Armazém cheio ou limite atingido.");
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
