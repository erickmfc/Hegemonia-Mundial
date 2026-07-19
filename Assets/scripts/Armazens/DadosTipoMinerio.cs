using UnityEngine;

/// <summary>
/// ScriptableObject que define as propriedades de um tipo de minério extraível.
/// Crie um asset para cada minério via menu: Hegemonia/Extração/Tipo de Minério
/// </summary>
[CreateAssetMenu(fileName = "Minerio_Novo", menuName = "Hegemonia/Extração/Tipo de Minério")]
public class DadosTipoMinerio : ScriptableObject
{
    [Header("🪨 Identificação")]
    [Tooltip("Nome exibido na UI (ex: Ferro, Cobre, Urânio Bruto)")]
    public string nomeRecurso = "Ferro";

    [Tooltip("Ícone exibido na lista de ordens (opcional)")]
    public Sprite icone;

    [Tooltip("Tipo de recurso que será depositado no armazém ao concluir o ciclo")]
    public TipoRecursoExtracao tipoExtracao = TipoRecursoExtracao.Ferro;

    [Header("💰 Custo por Ciclo")]
    [Tooltip("Custo em dinheiro por ciclo de extração")]
    public int custoDinheiro = 400;

    [Tooltip("Custo em energia por ciclo de extração")]
    public int custoEnergia = 50;

    [Header("⏱️ Tempo de Ciclo")]
    [Tooltip("Duração do ciclo em dias de jogo")]
    public int duracaoEmDias = 1;

    [Header("⛏️ Produção Estimada")]
    [Tooltip("Produção mínima por ciclo (toneladas)")]
    public float producaoMinima = 5200f;

    [Tooltip("Produção máxima por ciclo (toneladas)")]
    public float producaoMaxima = 7800f;

    [Header("🔒 Restrições")]
    [Tooltip("Se marcado, a ordem começa como BLOQUEADA e exige autorização manual para iniciar")]
    public bool exigeAutorizacao = false;

    [Tooltip("Descrição da restrição exibida ao jogador (ex: 'Exige Tratado Nuclear')")]
    public string descricaoRestricao = "";

    /// <summary>
    /// Calcula uma produção aleatória dentro do intervalo configurado
    /// </summary>
    public float GerarProducaoAleatoria()
    {
        return Random.Range(producaoMinima, producaoMaxima);
    }

    /// <summary>
    /// Retorna o texto formatado da produção estimada para UI
    /// </summary>
    public string ProducaoEstimadaFormatada()
    {
        if (producaoMinima <= 0 && producaoMaxima <= 0)
            return "0";
        return $"{producaoMinima:N0}–{producaoMaxima:N0} t";
    }
}
