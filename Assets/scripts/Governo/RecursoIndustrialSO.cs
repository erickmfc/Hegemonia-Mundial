using UnityEngine;

public enum CategoriaRecursoIndustrial
{
    MateriaPrima,
    Refinado,
    Estrategico,
    Componente,
    MilitarFuturo
}

public enum RaridadeRecursoIndustrial
{
    Comum,
    Incomum,
    Raro,
    MuitoRaro,
    Estrategico
}

[CreateAssetMenu(fileName = "RecursoIndustrial", menuName = "Hegemonia/Industrial/Recurso Industrial")]
public class RecursoIndustrialSO : ScriptableObject
{
    [Header("Identificação")]
    public string id = IndustriaIds.MinerioFerro;
    public string nome = "Minério de ferro";
    [TextArea(2, 4)] public string descricao = string.Empty;
    public CategoriaRecursoIndustrial categoria = CategoriaRecursoIndustrial.MateriaPrima;
    public string unidade = "t";

    [Header("Economia")]
    public int precoBase = 100;
    public RaridadeRecursoIndustrial raridade = RaridadeRecursoIndustrial.Comum;
    public bool estrategico;
    public bool podeComprar = true;
    public bool podeVender = true;
    public Sprite icone;
}
