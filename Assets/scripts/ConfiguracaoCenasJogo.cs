using UnityEngine;
using System.Linq;

public static class ConfiguracaoCenasJogo
{
    public const string CenaMenuPrincipalCanonica = "Menu cena";
    public const string CenaMenuFallback = "MenuPrincipal";
    public const string CenaCampanhaCanonica = "cena19)";
    // Esta e a cena de trabalho da campanha. Ela e a mesma que o projeto
    // carrega no Play e na build, evitando que ajustes sejam feitos em uma
    // copia que nunca entra no jogo.
    public const string CaminhoCenaCampanhaCanonica = "Assets/Scenes/cena19).unity";
    // A cena oficial do Tutorial e a cena historica do projeto.
    // cena19) continua sendo a campanha e permanece disponivel separadamente.
    public const string CenaTutorialCanonica = "Md Historia";
    public const string CaminhoCenaTutorialCanonica = "Assets/_Recovery/Md Historia.unity";

    private static readonly string[] aliasesMenuPrincipal =
    {
        CenaMenuPrincipalCanonica,
        "Assets/_Recovery/Menu/Menu cena.unity",
        CenaMenuFallback,
        "Assets/Scenes/MenuPrincipal.unity"
    };

    private static readonly string[] aliasesCampanhaLegada =
    {
        "Assets/Scenes/cena19).unity",
        "Assets/Scenes/SampleScene.unity"
    };

    private static readonly string[] aliasesTutorial =
    {
        CenaTutorialCanonica,
        CaminhoCenaTutorialCanonica,
        "demo1",
        "Assets/_Recovery/demo1.unity",
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
        return CenaCampanhaCanonica;
    }

    public static string NormalizarCenaCampanha(string nomeOuCaminho, string fallback)
    {
        if (string.IsNullOrWhiteSpace(nomeOuCaminho))
        {
            return fallback;
        }

        string valor = nomeOuCaminho.Trim().Replace('\\', '/');
        if (valor == CenaCampanhaCanonica
            || valor == CaminhoCenaCampanhaCanonica
            || aliasesCampanhaLegada.Contains(valor))
        {
            return CenaCampanhaCanonica;
        }

        return CenaExiste(valor) ? valor : fallback;
    }

    public static bool EhCenaCampanhaLegada(string nomeOuCaminho)
    {
        if (string.IsNullOrWhiteSpace(nomeOuCaminho))
        {
            return false;
        }

        string valor = nomeOuCaminho.Trim().Replace('\\', '/');
        return aliasesCampanhaLegada.Contains(valor);
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
