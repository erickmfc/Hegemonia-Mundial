using System;

public enum EstadoRegistroMilitarIndividual
{
    EmFormacao,
    Ativo,
    Mobilizado,
    Reservista,
    Alistavel,
    Baixado
}

/// <summary>
/// Ficha persistente de um militar. Mantem identidade e carreira como dados,
/// sem criar um GameObject ou um ciclo Update para cada pessoa.
/// </summary>
[Serializable]
public sealed class RegistroMilitarIndividual
{
    public string id;
    public int teamId;
    public string nomeCompleto;
    public string nacionalidade;
    public int idade;
    public int forca;
    public string patente;
    public string especialidade;
    public float experiencia;
    public int diaRecrutamento;
    public int diaInicioServico;
    public int diasServico;
    public long salarioDiario;
    public EstadoRegistroMilitarIndividual estado;
    public string postoAtual;
    public string instalacaoId;
    public string unidadeId;
}
