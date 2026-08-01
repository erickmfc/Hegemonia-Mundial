using UnityEngine;

public static class ConfiguracaoCenasJogo
{
    public const string CenaMenuPrincipalCanonica = "Menu cena";
    public const string CenaMenuFallback = "MenuPrincipal";
    public const string CenaCampanhaCanonica = "cena19)";
    public const string CenaTutorialCanonica = "teste";

    private static readonly string[] aliasesMenuPrincipal =
    {
        CenaMenuPrincipalCanonica,
        "Assets/_Recovery/Menu/Menu cena.unity",
        CenaMenuFallback,
        "Assets/Scenes/MenuPrincipal.unity"
    };

    private static readonly string[] aliasesCampanha =
    {
        CenaCampanhaCanonica,
        "Assets/_Recovery/cena19).unity",
        "Assets/_Recovery/0 (9).unity",
        "Assets/Scenes/SampleScene.unity"
    };

    private static readonly string[] aliasesTutorial =
    {
        CenaTutorialCanonica,
        "Assets/_Recovery/teste.unity",
        "Assets/Scenes/Tutorial Coast Scene Final.unity",
        "tutorial",
        "Assets/_Recovery/Tutorial/tutorial.unity"
    };

    public static bool EhCenaDeMenu(string nomeCena)
    {
        return nomeCena == CenaMenuPrincipalCanonica || nomeCena == CenaMenuFallback;
    }

    public static string ResolverCenaMenuPrincipal()
    {
        return ResolverPrimeiraCenaCarregavel(aliasesMenuPrincipal);
    }

    public static string ResolverCenaCampanhaPadrao()
    {
        return ResolverPrimeiraCenaCarregavel(aliasesCampanha);
    }

    public static string ResolverCenaTutorial()
    {
        return ResolverPrimeiraCenaCarregavel(aliasesTutorial);
    }

    public static bool CenaExiste(string nomeOuCaminho)
    {
        return !string.IsNullOrWhiteSpace(nomeOuCaminho)
               && Application.CanStreamedLevelBeLoaded(nomeOuCaminho);
    }

    private static string ResolverPrimeiraCenaCarregavel(string[] aliases)
    {
        for (int i = 0; i < aliases.Length; i++)
        {
            string alias = aliases[i];
            if (CenaExiste(alias))
            {
                return alias;
            }
        }

        return aliases.Length > 0 ? aliases[0] : string.Empty;
    }
}
