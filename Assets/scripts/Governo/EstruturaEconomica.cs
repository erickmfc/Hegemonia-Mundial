using UnityEngine;

public class EstruturaEconomica : MonoBehaviour
{
    [Header("Identidade")]
    public TipoEstruturaEconomica tipo = TipoEstruturaEconomica.Casa;
    public int teamId = 1;

    [Header("Populacao e Trabalho")]
    public int capacidadePopulacional;
    public int populacaoAtual;
    public int empregosGerados;

    [Header("Energia")]
    public float energiaConsumida;
    public float energiaProduzida;

    [Header("Producao")]
    public float comidaProduzida;
    public float petroleoProduzido;
    public float industriaProduzida;
    public float dinheiroGerado;

    [Header("Estado")]
    public StatusEstruturaEconomica status = StatusEstruturaEconomica.Ativa;
    [Range(0f, 1f)] public float eficiencia = 1f;

    public bool Ativa
    {
        get { return isActiveAndEnabled && status == StatusEstruturaEconomica.Ativa && eficiencia > 0f; }
    }

    private void Reset()
    {
        InferirTipoPorTagOuNome();
        AplicarPadraoPorTipo();
        InferirTeamId();
    }

    private void OnValidate()
    {
        eficiencia = Mathf.Clamp01(eficiencia);
        capacidadePopulacional = Mathf.Max(0, capacidadePopulacional);
        populacaoAtual = Mathf.Clamp(populacaoAtual, 0, capacidadePopulacional > 0 ? capacidadePopulacional : int.MaxValue);
        empregosGerados = Mathf.Max(0, empregosGerados);
    }

    private void OnEnable()
    {
        SistemaEconomiaImoveis.Register(this);
    }

    private void OnDisable()
    {
        SistemaEconomiaImoveis.Unregister(this);
    }

    public void InferirTeamId()
    {
        IdentidadeUnidade identidade = GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null && identidade.teamID > 0)
        {
            teamId = identidade.teamID;
            return;
        }

        IdentidadeIA identidadeIA = GetComponentInParent<IdentidadeIA>();
        if (identidadeIA != null && identidadeIA.teamID > 0)
        {
            teamId = identidadeIA.teamID;
            return;
        }

        IA_Comandante comandante = GetComponentInParent<IA_Comandante>();
        if (comandante != null && comandante.TeamID > 0)
        {
            teamId = comandante.TeamID;
            return;
        }

        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        teamId = gov != null ? gov.teamJogador : Mathf.Max(1, teamId);
    }

    public void InferirTipoPorTagOuNome()
    {
        string chave = (tag + " " + name).ToLowerInvariant();
        if (chave.Contains("casa") || chave.Contains("house") || chave.Contains("imovel")) tipo = TipoEstruturaEconomica.Casa;
        if (chave.Contains("industria") || chave.Contains("fabrica") || chave.Contains("factory")) tipo = TipoEstruturaEconomica.Industria;
        if (chave.Contains("petroleo") || chave.Contains("oil") || chave.Contains("plataforma")) tipo = TipoEstruturaEconomica.Petroleo;
        if (chave.Contains("comercio") || chave.Contains("loja") || chave.Contains("shop")) tipo = TipoEstruturaEconomica.Comercio;
        if (chave.Contains("farm") || chave.Contains("fazenda") || chave.Contains("comida")) tipo = TipoEstruturaEconomica.Farm;
        if (chave.Contains("energia") || chave.Contains("power") || chave.Contains("usina")) tipo = TipoEstruturaEconomica.Energia;
    }

    public void AplicarPadraoPorTipo()
    {
        switch (tipo)
        {
            case TipoEstruturaEconomica.Casa:
                if (capacidadePopulacional <= 0) capacidadePopulacional = 10;
                if (dinheiroGerado <= 0f) dinheiroGerado = 0.5f;
                if (energiaConsumida <= 0f) energiaConsumida = 0.3f;
                break;
            case TipoEstruturaEconomica.Industria:
                if (empregosGerados <= 0) empregosGerados = 24;
                if (industriaProduzida <= 0f) industriaProduzida = 5f;
                if (dinheiroGerado <= 0f) dinheiroGerado = 7f;
                if (energiaConsumida <= 0f) energiaConsumida = 3f;
                break;
            case TipoEstruturaEconomica.Petroleo:
                if (empregosGerados <= 0) empregosGerados = 12;
                if (petroleoProduzido <= 0f) petroleoProduzido = 4f;
                if (dinheiroGerado <= 0f) dinheiroGerado = 5f;
                if (energiaConsumida <= 0f) energiaConsumida = 1.5f;
                break;
            case TipoEstruturaEconomica.Comercio:
                if (empregosGerados <= 0) empregosGerados = 10;
                if (dinheiroGerado <= 0f) dinheiroGerado = 6f;
                if (energiaConsumida <= 0f) energiaConsumida = 1f;
                break;
            case TipoEstruturaEconomica.Farm:
                if (empregosGerados <= 0) empregosGerados = 10;
                if (comidaProduzida <= 0f) comidaProduzida = 5f;
                if (energiaConsumida <= 0f) energiaConsumida = 0.7f;
                break;
            case TipoEstruturaEconomica.Energia:
                if (empregosGerados <= 0) empregosGerados = 8;
                if (energiaProduzida <= 0f) energiaProduzida = 8f;
                if (dinheiroGerado <= 0f) dinheiroGerado = 2f;
                break;
        }
    }
}
