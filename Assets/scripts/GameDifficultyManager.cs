using System;
using UnityEngine;

public enum DificuldadeJogo
{
    Facil,
    Normal,
    Dificil,
    Imperial
}

public sealed class PerfilDificuldadeJogo
{
    public readonly DificuldadeJogo Dificuldade;
    public readonly string Codigo;
    public readonly string ChaveNome;
    public readonly float MultiplicadorEconomiaIA;
    public readonly float MultiplicadorMetasIA;
    public readonly float MultiplicadorContraJogador;
    public readonly float MultiplicadorOrcamentoIA;
    public readonly float MultiplicadorEngajamentoIA;
    public readonly float MultiplicadorCooldownProducaoIA;
    public readonly int BonusComandosIA;
    public readonly int BonusModulosPorFrameIA;
    public readonly int BonusHeavySlotsIA;
    public readonly int MetaEstabilidadeMinutos;

    public PerfilDificuldadeJogo(
        DificuldadeJogo dificuldade,
        string codigo,
        string chaveNome,
        float multiplicadorEconomiaIA,
        float multiplicadorMetasIA,
        float multiplicadorContraJogador,
        float multiplicadorOrcamentoIA,
        float multiplicadorEngajamentoIA,
        float multiplicadorCooldownProducaoIA,
        int bonusComandosIA,
        int bonusModulosPorFrameIA,
        int bonusHeavySlotsIA,
        int metaEstabilidadeMinutos)
    {
        Dificuldade = dificuldade;
        Codigo = codigo;
        ChaveNome = chaveNome;
        MultiplicadorEconomiaIA = multiplicadorEconomiaIA;
        MultiplicadorMetasIA = multiplicadorMetasIA;
        MultiplicadorContraJogador = multiplicadorContraJogador;
        MultiplicadorOrcamentoIA = multiplicadorOrcamentoIA;
        MultiplicadorEngajamentoIA = multiplicadorEngajamentoIA;
        MultiplicadorCooldownProducaoIA = multiplicadorCooldownProducaoIA;
        BonusComandosIA = bonusComandosIA;
        BonusModulosPorFrameIA = bonusModulosPorFrameIA;
        BonusHeavySlotsIA = bonusHeavySlotsIA;
        MetaEstabilidadeMinutos = metaEstabilidadeMinutos;
    }

    public int AjustarComandos(int baseValue)
    {
        return Mathf.Clamp(baseValue + BonusComandosIA, 1, 12);
    }

    public int AjustarModulosPorFrame(int baseValue)
    {
        return Mathf.Clamp(baseValue + BonusModulosPorFrameIA, 1, 8);
    }

    public int AjustarHeavySlots(int baseValue)
    {
        return Mathf.Clamp(baseValue + BonusHeavySlotsIA, 0, 4);
    }

    public int AjustarMeta(int baseValue, int minimo = 0)
    {
        return Mathf.Max(minimo, Mathf.RoundToInt(Mathf.Max(0, baseValue) * MultiplicadorMetasIA));
    }

    public int AjustarMetaContraJogador(int quantidadeJogador, float margemBase, int minimo = 0)
    {
        float margem = Mathf.Max(1f, margemBase) * Mathf.Max(0.5f, MultiplicadorContraJogador);
        return Mathf.Max(minimo, Mathf.CeilToInt(Mathf.Max(0, quantidadeJogador) * margem));
    }
}

[DefaultExecutionOrder(-10045)]
public sealed class GameDifficultyManager : MonoBehaviour
{
    private const string PlayerPrefsKey = "hegemonia.dificuldade";
    private static GameDifficultyManager instancia;

    private static readonly PerfilDificuldadeJogo PerfilFacil = new PerfilDificuldadeJogo(
        DificuldadeJogo.Facil, "facil", "difficulty.easy",
        0.78f, 0.72f, 0.75f, 0.72f, 0.70f, 1.35f, -1, -1, -1, 30);

    private static readonly PerfilDificuldadeJogo PerfilNormal = new PerfilDificuldadeJogo(
        DificuldadeJogo.Normal, "normal", "difficulty.normal",
        1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 0, 0, 0, 30);

    private static readonly PerfilDificuldadeJogo PerfilDificil = new PerfilDificuldadeJogo(
        DificuldadeJogo.Dificil, "dificil", "difficulty.hard",
        1.12f, 1.18f, 1.06f, 1.08f, 1.18f, 0.86f, 1, 0, 0, 45);

    private static readonly PerfilDificuldadeJogo PerfilImperial = new PerfilDificuldadeJogo(
        DificuldadeJogo.Imperial, "imperial", "difficulty.imperial",
        1.28f, 1.38f, 1.16f, 1.15f, 1.35f, 0.72f, 2, 1, 1, 60);

    public static GameDifficultyManager Instancia
    {
        get
        {
            GarantirInstancia();
            return instancia;
        }
    }

    public static DificuldadeJogo DificuldadeAtual => Instancia.dificuldadeAtual;
    public static PerfilDificuldadeJogo PerfilAtual => ObterPerfil(DificuldadeAtual);
    public static event Action DificuldadeAlterada;

    [SerializeField] private DificuldadeJogo dificuldadeAtual = DificuldadeJogo.Normal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void GarantirInstancia()
    {
        if (instancia != null)
        {
            return;
        }

        GameDifficultyManager existente = FindFirstObjectByType<GameDifficultyManager>();
        if (existente != null)
        {
            instancia = existente;
            instancia.Inicializar();
            return;
        }

        GameObject obj = new GameObject("GameDifficultyManager");
        instancia = obj.AddComponent<GameDifficultyManager>();
        instancia.Inicializar();
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        Inicializar();
    }

    private void Inicializar()
    {
        DontDestroyOnLoad(gameObject);
        AplicarCodigo(PlayerPrefs.GetString(PlayerPrefsKey, PerfilNormal.Codigo), false);
    }

    public string ObterCodigoDificuldade()
    {
        return ObterPerfil(dificuldadeAtual).Codigo;
    }

    public string NomeDificuldadeAtual()
    {
        PerfilDificuldadeJogo perfil = ObterPerfil(dificuldadeAtual);
        string fallback;
        switch (dificuldadeAtual)
        {
            case DificuldadeJogo.Facil:
                fallback = "Facil";
                break;
            case DificuldadeJogo.Dificil:
                fallback = "Dificil";
                break;
            case DificuldadeJogo.Imperial:
                fallback = "Imperial";
                break;
            default:
                fallback = "Normal";
                break;
        }

        return LocalizationManager.T(perfil.ChaveNome, fallback);
    }

    public void ProximaDificuldade()
    {
        switch (dificuldadeAtual)
        {
            case DificuldadeJogo.Facil:
                AplicarCodigo(PerfilNormal.Codigo);
                break;
            case DificuldadeJogo.Normal:
                AplicarCodigo(PerfilDificil.Codigo);
                break;
            case DificuldadeJogo.Dificil:
                AplicarCodigo(PerfilImperial.Codigo);
                break;
            default:
                AplicarCodigo(PerfilFacil.Codigo);
                break;
        }
    }

    public void AplicarCodigo(string codigo)
    {
        AplicarCodigo(codigo, true);
    }

    private void AplicarCodigo(string codigo, bool notificar)
    {
        DificuldadeJogo nova = CodigoParaDificuldade(codigo);
        string novoCodigo = ObterPerfil(nova).Codigo;
        if (dificuldadeAtual == nova && PlayerPrefs.GetString(PlayerPrefsKey, string.Empty) == novoCodigo)
        {
            return;
        }

        dificuldadeAtual = nova;
        PlayerPrefs.SetString(PlayerPrefsKey, novoCodigo);
        PlayerPrefs.Save();
        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("dificuldade", novoCodigo);
        DiagnosticoDesempenhoJogo.RegistrarEvento("Dificuldade", "Dificuldade alterada para " + novoCodigo);

        if (notificar)
        {
            DificuldadeAlterada?.Invoke();
        }
    }

    public static PerfilDificuldadeJogo ObterPerfil(DificuldadeJogo dificuldade)
    {
        switch (dificuldade)
        {
            case DificuldadeJogo.Facil:
                return PerfilFacil;
            case DificuldadeJogo.Dificil:
                return PerfilDificil;
            case DificuldadeJogo.Imperial:
                return PerfilImperial;
            default:
                return PerfilNormal;
        }
    }

    public static DificuldadeJogo CodigoParaDificuldade(string codigo)
    {
        if (string.Equals(codigo, "facil", StringComparison.OrdinalIgnoreCase)
            || string.Equals(codigo, "easy", StringComparison.OrdinalIgnoreCase))
        {
            return DificuldadeJogo.Facil;
        }

        if (string.Equals(codigo, "dificil", StringComparison.OrdinalIgnoreCase)
            || string.Equals(codigo, "hard", StringComparison.OrdinalIgnoreCase))
        {
            return DificuldadeJogo.Dificil;
        }

        if (string.Equals(codigo, "imperial", StringComparison.OrdinalIgnoreCase)
            || string.Equals(codigo, "empire", StringComparison.OrdinalIgnoreCase))
        {
            return DificuldadeJogo.Imperial;
        }

        return DificuldadeJogo.Normal;
    }
}
