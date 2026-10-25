using System;
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
    public int capacidadeMaxima = int.MaxValue;
    
    [Header("🌾 Alimentos")]
    public int alimentos = 0;
    public int alimentosMaximo = int.MaxValue;
    
    [Header("💧 Água")]
    public int agua = 0;
    public int aguaMaximo = int.MaxValue;
    
    [Header("⛽ Petróleo")]
    public int petroleo = 0;
    public int petroleoMaximo = int.MaxValue;
    
    [Header("💎 Minerais")]
    public int minerais = 0;
    public int mineraisMaximo = int.MaxValue;
    
    [Header("🔩 Aço/Metal")]
    public int metal = 0;
    public int metalMaximo = int.MaxValue;
    
    [Header("⚡ Energia Armazenada (Baterias)")]
    public int energia = 0;
    public int energiaMaximo = int.MaxValue;

    // ──────────────────────────────────────────────
    // ⛏️ MINÉRIOS EXTRAÍDOS (Sistema de Ordens)
    // ──────────────────────────────────────────────

    [Header("⛏️ Ferro (t))")]
    public float ferro = 0f;
    public float ferroMaximo = float.MaxValue;

    [Header("🟠 Cobre (t)")]
    public float cobre = 0f;
    public float cobreMaximo = float.MaxValue;

    [Header("🪶 Bauxita (t)")]
    public float bauxita = 0f;
    public float bauxitaMaximo = float.MaxValue;

    [Header("🔵 Titânio (t)")]
    public float titanio = 0f;
    public float titanioMaximo = float.MaxValue;

    [Header("☢️ Urânio (t)")]
    public float uranio = 0f;
    public float uranioMaximo = float.MaxValue;

    [Header("📊 Informações")]
    [TextArea(3, 5)]
    public string descricao = "Armazém de recursos civis do país";
    public string localizacao = "Base Principal";

    /// <summary>
    /// Retorna o espaço disponível total
    /// </summary>
    public int EspacoDisponivel()
    {
        return int.MaxValue;
    }

    // ──────────────────────────────────────────────
    // MÉTODOS PARA SISTEMA DE ORDENS DE EXTRAÇÃO
    // ──────────────────────────────────────────────

    /// <summary>
    /// Adiciona minério ao campo correspondente sem limite de armazenamento.
    /// Retorna a quantidade efetivamente adicionada, limitada apenas pela representação numérica.
    /// </summary>
    public float AdicionarMinerio(TipoRecursoExtracao tipo, float quantidade)
    {
        if (quantidade <= 0f || float.IsNaN(quantidade) || float.IsInfinity(quantidade)) return 0f;

        switch (tipo)
        {
            case TipoRecursoExtracao.Ferro:
                return AdicionarMinerioSemLimite(ref ferro, quantidade);

            case TipoRecursoExtracao.Cobre:
                return AdicionarMinerioSemLimite(ref cobre, quantidade);

            case TipoRecursoExtracao.Bauxita:
                return AdicionarMinerioSemLimite(ref bauxita, quantidade);

            case TipoRecursoExtracao.Titanio:
                return AdicionarMinerioSemLimite(ref titanio, quantidade);

            case TipoRecursoExtracao.Uranio:
                return AdicionarMinerioSemLimite(ref uranio, quantidade);
        }
        return 0f;
    }

    private static float AdicionarMinerioSemLimite(ref float estoque, float quantidade)
    {
        double total = Math.Max(0d, estoque) + quantidade;
        float adicionado = (float)Math.Min(float.MaxValue, total);
        adicionado = Mathf.Max(0f, adicionado - Mathf.Max(0f, estoque));
        estoque = Mathf.Min(float.MaxValue, Mathf.Max(0f, estoque) + adicionado);
        return adicionado;
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
        return 0f;
    }

    /// <summary>
    /// Tenta adicionar recursos ao armazém
    /// </summary>
    public bool AdicionarRecurso(TipoRecurso tipo, int quantidade)
    {
        if (quantidade <= 0) return false;

        switch (tipo)
        {
            case TipoRecurso.Alimentos:
                alimentos = AdicionarEstoqueSemLimite(alimentos, quantidade);
                return true;
                
            case TipoRecurso.Agua:
                agua = AdicionarEstoqueSemLimite(agua, quantidade);
                return true;
                
            case TipoRecurso.Petroleo:
                petroleo = AdicionarEstoqueSemLimite(petroleo, quantidade);
                return true;
                
            case TipoRecurso.Minerais:
                minerais = AdicionarEstoqueSemLimite(minerais, quantidade);
                return true;
                
            case TipoRecurso.Metal:
                metal = AdicionarEstoqueSemLimite(metal, quantidade);
                return true;
                
            case TipoRecurso.Energia:
                energia = AdicionarEstoqueSemLimite(energia, quantidade);
                return true;
        }
        return false;
    }

    private static int AdicionarEstoqueSemLimite(int estoque, int quantidade)
    {
        long total = Math.Max(0L, estoque) + quantidade;
        return (int)Math.Min(int.MaxValue, total);
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
