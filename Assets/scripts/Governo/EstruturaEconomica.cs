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

    [Header("Energia e Logistica")]
    public float energiaConsumida;
    public float energiaProduzida;
    public float combustivelConsumido;
    public int militaresNecessarios;

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

    private float timerUpdateEficiencia;

    private void Update()
    {
        if (empregosGerados <= 0 || status != StatusEstruturaEconomica.Ativa) return;

        timerUpdateEficiencia += Time.deltaTime;
        if (timerUpdateEficiencia >= 5f)
        {
            timerUpdateEficiencia = Random.Range(0f, 1f); // Stagger updates
            if (GerenciadorDivisaoTerritorial.Instancia != null)
            {
                float eficienciaMaoDeObra = GerenciadorDivisaoTerritorial.Instancia.ObterEficienciaMaoDeObraLocal(transform.position);
                eficiencia = Mathf.Clamp01(eficienciaMaoDeObra);
            }
        }
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
        if (chave.Contains("popular")) tipo = TipoEstruturaEconomica.CasaPopular;
        else if (chave.Contains("predio") || chave.Contains("residencial")) tipo = TipoEstruturaEconomica.PredioResidencial;
        else if (chave.Contains("shopping")) tipo = TipoEstruturaEconomica.Shopping;
        else if (chave.Contains("refinaria")) tipo = TipoEstruturaEconomica.Refinaria;
        else if (chave.Contains("nuclear")) tipo = TipoEstruturaEconomica.UsinaNuclear;
        else if (chave.Contains("hidreletrica")) tipo = TipoEstruturaEconomica.UsinaHidreletrica;
        else if (chave.Contains("solar")) tipo = TipoEstruturaEconomica.UsinaSolar;
        else if (chave.Contains("aeroporto")) tipo = TipoEstruturaEconomica.AeroportoCivil;
        else if (chave.Contains("porto")) tipo = TipoEstruturaEconomica.PortoComercial;
        // Legado
        else if (chave.Contains("casa") || chave.Contains("house") || chave.Contains("imovel")) tipo = TipoEstruturaEconomica.Casa;
        else if (chave.Contains("industria") || chave.Contains("fabrica") || chave.Contains("factory")) tipo = TipoEstruturaEconomica.Industria;
        else if (chave.Contains("petroleo") || chave.Contains("oil") || chave.Contains("plataforma")) tipo = TipoEstruturaEconomica.Petroleo;
        else if (chave.Contains("comercio") || chave.Contains("loja") || chave.Contains("shop")) tipo = TipoEstruturaEconomica.Comercio;
        else if (chave.Contains("farm") || chave.Contains("fazenda") || chave.Contains("comida")) tipo = TipoEstruturaEconomica.Farm;
        else if (chave.Contains("energia") || chave.Contains("power") || chave.Contains("usina")) tipo = TipoEstruturaEconomica.Energia;
    }

    public void AplicarPadraoPorTipo()
    {
        switch (tipo)
        {
            case TipoEstruturaEconomica.CasaPopular:
                if (capacidadePopulacional <= 0) capacidadePopulacional = 6;
                if (energiaConsumida <= 0f) energiaConsumida = 2f;
                if (empregosGerados <= 0) empregosGerados = 0;
                break;
            case TipoEstruturaEconomica.PredioResidencial:
                if (capacidadePopulacional <= 0) capacidadePopulacional = 60;
                if (energiaConsumida <= 0f) energiaConsumida = 12f;
                if (empregosGerados <= 0) empregosGerados = 8;
                break;
            case TipoEstruturaEconomica.ComercioPequeno:
                if (empregosGerados <= 0) empregosGerados = 12;
                if (energiaConsumida <= 0f) energiaConsumida = 8f;
                break;
            case TipoEstruturaEconomica.Shopping:
                if (empregosGerados <= 0) empregosGerados = 260;
                if (energiaConsumida <= 0f) energiaConsumida = 60f;
                if (militaresNecessarios <= 0) militaresNecessarios = 20;
                if (dinheiroGerado <= 0f) dinheiroGerado = 30f;
                break;
            case TipoEstruturaEconomica.IndustriaLeve:
                if (empregosGerados <= 0) empregosGerados = 180;
                if (energiaConsumida <= 0f) energiaConsumida = 80f;
                if (combustivelConsumido <= 0f) combustivelConsumido = 5f;
                if (industriaProduzida <= 0f) industriaProduzida = 15f;
                break;
            case TipoEstruturaEconomica.IndustriaPesada:
                if (empregosGerados <= 0) empregosGerados = 650;
                if (energiaConsumida <= 0f) energiaConsumida = 260f;
                if (combustivelConsumido <= 0f) combustivelConsumido = 18f;
                if (industriaProduzida <= 0f) industriaProduzida = 45f;
                break;
            case TipoEstruturaEconomica.Refinaria:
                if (empregosGerados <= 0) empregosGerados = 900;
                if (energiaConsumida <= 0f) energiaConsumida = 380f;
                if (petroleoProduzido <= 0f) petroleoProduzido = 120f;
                if (militaresNecessarios <= 0) militaresNecessarios = 80;
                break;
            case TipoEstruturaEconomica.PortoComercial:
                if (empregosGerados <= 0) empregosGerados = 420;
                if (energiaConsumida <= 0f) energiaConsumida = 180f;
                if (combustivelConsumido <= 0f) combustivelConsumido = 6f;
                if (militaresNecessarios <= 0) militaresNecessarios = 40;
                break;
            case TipoEstruturaEconomica.AeroportoCivil:
                if (empregosGerados <= 0) empregosGerados = 700;
                if (energiaConsumida <= 0f) energiaConsumida = 220f;
                if (combustivelConsumido <= 0f) combustivelConsumido = 15f;
                if (militaresNecessarios <= 0) militaresNecessarios = 60;
                break;
            case TipoEstruturaEconomica.UsinaTermicaPequena:
                if (energiaProduzida <= 0f) energiaProduzida = 120f;
                if (empregosGerados <= 0) empregosGerados = 120;
                if (combustivelConsumido <= 0f) combustivelConsumido = 8f;
                break;
            case TipoEstruturaEconomica.UsinaTermicaGrande:
                if (energiaProduzida <= 0f) energiaProduzida = 450f;
                if (empregosGerados <= 0) empregosGerados = 420;
                if (combustivelConsumido <= 0f) combustivelConsumido = 35f;
                break;
            case TipoEstruturaEconomica.UsinaNuclear:
                if (energiaProduzida <= 0f) energiaProduzida = 2200f;
                if (empregosGerados <= 0) empregosGerados = 2050; // 1800 + 250
                if (combustivelConsumido <= 0f) combustivelConsumido = 3f;
                if (militaresNecessarios <= 0) militaresNecessarios = 400;
                break;
            case TipoEstruturaEconomica.UsinaHidreletrica:
                if (energiaProduzida <= 0f) energiaProduzida = 1500f;
                if (empregosGerados <= 0) empregosGerados = 700;
                break;
            case TipoEstruturaEconomica.UsinaSolar:
                if (energiaProduzida <= 0f) energiaProduzida = 320f;
                if (empregosGerados <= 0) empregosGerados = 90;
                break;
            // BASES MILITARES
            case TipoEstruturaEconomica.BaseMilitarPequena:
                if (energiaConsumida <= 0f) energiaConsumida = 45f;
                if (militaresNecessarios <= 0) militaresNecessarios = 180;
                if (combustivelConsumido <= 0f) combustivelConsumido = 3f;
                break;
            case TipoEstruturaEconomica.BaseMilitarMedia:
                if (energiaConsumida <= 0f) energiaConsumida = 120f;
                if (militaresNecessarios <= 0) militaresNecessarios = 700;
                if (combustivelConsumido <= 0f) combustivelConsumido = 10f;
                break;
            case TipoEstruturaEconomica.GrandeBaseMilitar:
                if (energiaConsumida <= 0f) energiaConsumida = 300f;
                if (militaresNecessarios <= 0) militaresNecessarios = 2500;
                if (combustivelConsumido <= 0f) combustivelConsumido = 28f;
                break;
            case TipoEstruturaEconomica.BaseAerea:
                if (energiaConsumida <= 0f) energiaConsumida = 260f;
                if (militaresNecessarios <= 0) militaresNecessarios = 1800;
                if (empregosGerados <= 0) empregosGerados = 350; // Técnicos civis
                if (combustivelConsumido <= 0f) combustivelConsumido = 40f;
                break;
            case TipoEstruturaEconomica.BaseNaval:
                if (energiaConsumida <= 0f) energiaConsumida = 340f;
                if (militaresNecessarios <= 0) militaresNecessarios = 2200;
                if (empregosGerados <= 0) empregosGerados = 420; // Técnicos civis
                if (combustivelConsumido <= 0f) combustivelConsumido = 55f;
                break;
            // LEGADO
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
