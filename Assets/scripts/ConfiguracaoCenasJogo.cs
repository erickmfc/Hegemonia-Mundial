using UnityEngine;
using System.Linq;

public static class ConfiguracaoCenasJogo
{
    // As cenas editaveis do jogo ficam centralizadas em Assets/_Recovery.
    // Estes nomes/caminhos sao a fonte oficial usada pelo menu e pelos testes.
    public const string CenaMenuPrincipalCanonica = "Cena menu P";
    public const string CenaMenuFallback = "MenuPrincipal";
    public const string CenaCampanhaCanonica = "cena19)";
    public const string CaminhoCenaMenuPrincipalCanonica = "Assets/_Recovery/Cena menu P.unity";
    public const string CaminhoCenaCampanhaCanonica = "Assets/_Recovery/cena19).unity";
    public const string CenaEscaramucaCanonica = "Md Historia";
    public const string CaminhoCenaEscaramucaCanonica = "Assets/_Recovery/Md Historia.unity";
    public const string CenaTutorialCanonica = "Tutorial";
    public const string CaminhoCenaTutorialCanonica = "Assets/_Recovery/Tutorial.unity";

    public const string CenaAno1Oficial = "Ano1";
    public const string CaminhoCenaAno1Oficial = "Assets/_Recovery/Ano1.unity";
    public const string CenaDemo1Oficial = "demo1";
    public const string CaminhoCenaDemo1Oficial = "Assets/_Recovery/demo1.unity";
    public const string CenaTesteOficial = "teste";
    public const string CaminhoCenaTesteOficial = "Assets/_Recovery/teste.unity";

    private static readonly string[] aliasesMenuPrincipal =
    {
        CenaMenuPrincipalCanonica,
        CaminhoCenaMenuPrincipalCanonica
    };

    private static readonly string[] aliasesCampanhaLegada =
    {
        CenaCampanhaCanonica,
        CaminhoCenaCampanhaCanonica,
        "Assets/Scenes/cena19).unity",
        "Assets/Scenes/SampleScene.unity"
    };

    private static readonly string[] aliasesTutorial =
    {
        CenaTutorialCanonica,
        CaminhoCenaTutorialCanonica
    };

    private static readonly string[] aliasesEscaramuca =
    {
        CenaEscaramucaCanonica,
        CaminhoCenaEscaramucaCanonica
    };

    private static readonly string[] cenasOficiais =
    {
        CaminhoCenaAno1Oficial,
        CaminhoCenaMenuPrincipalCanonica,
        CaminhoCenaCampanhaCanonica,
        CaminhoCenaEscaramucaCanonica,
        CaminhoCenaDemo1Oficial,
        CaminhoCenaTutorialCanonica,
        CaminhoCenaTesteOficial
    };

    public static bool EhCenaDeMenu(string nomeCena)
    {
        return nomeCena == CenaMenuPrincipalCanonica;
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

    public static string ResolverCenaEscaramuca()
    {
        return ResolverPrimeiraCenaCarregavel(aliasesEscaramuca);
    }

    public static bool EhCenaOficial(string nomeOuCaminho)
    {
        if (string.IsNullOrWhiteSpace(nomeOuCaminho))
        {
            return false;
        }

        string valor = nomeOuCaminho.Trim().Replace('\\', '/');
        for (int i = 0; i < cenasOficiais.Length; i++)
        {
            string caminho = cenasOficiais[i];
            string nome = System.IO.Path.GetFileNameWithoutExtension(caminho);
            if (string.Equals(valor, caminho, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(valor, nome, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
