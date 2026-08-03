using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Fonte unica dos valores economicos definidos para a campanha.
/// Os assets antigos continuam carregando, mas os sistemas consultam esta tabela
/// por ID/nome normalizado para evitar depender de precos simbolicos legados.
/// </summary>
public static class ValoresDefinitivosHegemonia
{
    private static readonly CultureInfo CulturaMonetaria = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Dictionary<string, long> Precos = CriarPrecos();
    private static readonly Dictionary<string, long> PrecosMercado = CriarPrecosMercado();
    private static readonly Dictionary<string, long> CustosSementes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
    {
        { "milho", 50000L }, { "batata", 40000L }, { "feijao", 35000L },
        { "trigo", 65000L }, { "arroz", 70000L }, { "cana de acucar", 100000L },
        { "algodao", 120000L }, { "soja", 150000L }, { "cafe", 250000L },
        { "cacau", 350000L }
    };

    public static long DinheiroInicial(DificuldadeJogo dificuldade)
    {
        switch (dificuldade)
        {
            case DificuldadeJogo.Facil: return 120000000000L;
            case DificuldadeJogo.Dificil: return 35000000000L;
            default: return 70000000000L;
        }
    }

    public static float MultiplicadorReceita(DificuldadeJogo dificuldade)
    {
        switch (dificuldade)
        {
            case DificuldadeJogo.Facil: return 1.25f;
            case DificuldadeJogo.Dificil: return 0.80f;
            default: return 1f;
        }
    }

    public static float MultiplicadorManutencao(DificuldadeJogo dificuldade)
    {
        switch (dificuldade)
        {
            case DificuldadeJogo.Facil: return 0.80f;
            case DificuldadeJogo.Dificil: return 1.20f;
            default: return 1f;
        }
    }

    public static long ObterPreco(DadosConstrucao ficha)
    {
        if (ficha == null) return 0L;
        long valor;
        if (TryObterPreco(ficha.itemId, ficha.nomeItem, out valor)) return valor;
        return ficha.precoDefinitivo > 0L ? ficha.precoDefinitivo : Math.Max(0L, ficha.preco);
    }

    public static bool TryObterPreco(string itemId, string nome, out long preco)
    {
        preco = 0L;
        string id = Normalizar(itemId);
        string nomeNormalizado = Normalizar(nome);
        if (!string.IsNullOrEmpty(id) && Precos.TryGetValue(id, out preco)) return true;
        if (!string.IsNullOrEmpty(nomeNormalizado) && Precos.TryGetValue(nomeNormalizado, out preco)) return true;

        foreach (KeyValuePair<string, long> item in Precos)
        {
            if ((!string.IsNullOrEmpty(id) && id.Contains(item.Key))
                || (!string.IsNullOrEmpty(nomeNormalizado) && nomeNormalizado.Contains(item.Key)))
            {
                preco = item.Value;
                return true;
            }
        }
        return false;
    }

    public static bool TryObterPrecoMercado(string id, out long preco)
    {
        return PrecosMercado.TryGetValue(Normalizar(id), out preco);
    }

    public static long ObterCustoSemente(string nome, long valorLegado)
    {
        long valor;
        return CustosSementes.TryGetValue(Normalizar(nome), out valor) ? valor : valorLegado;
    }

    public static long ObterPrecoMercadoOu(long valorLegado, string id)
    {
        long valor;
        return TryObterPrecoMercado(id, out valor) ? valor : valorLegado;
    }

    public static long ObterManutencaoPorDia(string itemId, string nome, long valorLegado = 0L)
    {
        string chave = Normalizar(string.IsNullOrWhiteSpace(itemId) ? nome : itemId);
        if (chave.Contains("soldado") || chave.Contains("infantaria") || chave.Contains("fuzileiro")) return 120L;
        if (chave.Contains("caminhao") || chave.Contains("truck") || chave.Contains("combustivel")) return 2000L;
        if (chave.Contains("hamer") || chave.Contains("leve")) return 3000L;
        if (chave.Contains("ares ar") || chave.Contains("hg ar") || chave.Contains("antiaerea") || chave.Contains("aa")) return 80000L;
        if (chave.Contains("artilharia")) return 35000L;
        if (chave.Contains("tank") || chave.Contains("leonc") || chave.Contains("arthur")) return 60000L;
        if (chave.Contains("blindado")) return 25000L;
        if (chave.Contains("drone")) return chave.Contains("hasaf") || chave.Contains("veloster") ? 50000L : 15000L;
        if (chave.Contains("c17") || chave.Contains("c700")) return 350000L;
        if (chave.Contains("bombardeiro")) return 450000L;
        if (chave.Contains("caca") || chave.Contains("su11") || chave.Contains("su20")) return 220000L;
        if (chave.Contains("aviao") || chave.Contains("helicoptero") || chave.Contains("heli")) return 80000L;
        if (chave.Contains("aeroporto comercial")) return 1000000L;
        if (chave.Contains("aeroporto") || chave.Contains("base aerea")) return 1500000L;
        if (chave.Contains("porta avioes") || chave.Contains("nav global")) return 4000000L;
        if (chave.Contains("submarino") || chave.Contains("leviathan")) return 1500000L;
        if (chave.Contains("desembarque") || chave.Contains("nav tropa") || chave.Contains("des wall")) return 600000L;
        if (chave.Contains("destrier") || chave.Contains("ironclad")) return 900000L;
        if (chave.Contains("cruzador") || chave.Contains("liberty") || chave.Contains("dominion") || chave.Contains("wraith")) return 1200000L;
        if (chave.Contains("fragata") || chave.Contains("f200")) return 500000L;
        if (chave.Contains("corveta")) return 250000L;
        if (chave.Contains("patrulha") || chave.Contains("vigia") || chave.Contains("think")) return 80000L;
        if (chave.Contains("petroleiro")) return 180000L;
        if (chave.Contains("carga") || chave.Contains("transporte")) return 120000L;
        if (chave.Contains("estaleiro")) return 2500000L;
        if (chave.Contains("shopping")) return 250000L;
        if (chave.Contains("fazenda") || chave.Contains("farm")) return 12000L;
        if (chave.Contains("armazem")) return chave.Contains("militar") ? 100000L : 15000L;
        if (chave.Contains("fabrica")) return 100000L;
        if (chave.Contains("industria leve")) return 150000L;
        if (chave.Contains("industria pesada")) return 400000L;
        if (chave.Contains("porto comercial")) return 700000L;
        if (chave.Contains("base militar pequena")) return 500000L;
        if (chave.Contains("base militar media")) return 1200000L;
        if (chave.Contains("grande base")) return 3000000L;
        if (chave.Contains("base naval")) return 2000000L;
        if (chave.Contains("usina solar")) return 60000L;
        if (chave.Contains("usina carvao")) return 650000L;
        if (chave.Contains("usina termica pequena")) return 180000L;
        if (chave.Contains("usina termica grande")) return 700000L;
        if (chave.Contains("hidreletrica")) return 800000L;
        if (chave.Contains("nuclear")) return 2500000L;
        if (chave.Contains("plataforma")) return 1000000L;
        if (chave.Contains("refinaria")) return 1200000L;
        if (chave.Contains("comercio pequeno")) return 5000L;
        return Math.Max(0L, valorLegado);
    }

    public static string FormatarDinheiro(long valor)
    {
        string sinal = valor < 0 ? "-" : string.Empty;
        decimal absoluto = Math.Abs((decimal)valor);
        if (absoluto >= 1000000000m) return sinal + "$" + (absoluto / 1000000000m).ToString("0.##", CulturaMonetaria) + " bi";
        if (absoluto >= 1000000m) return sinal + "$" + (absoluto / 1000000m).ToString("0.##", CulturaMonetaria) + " mi";
        return sinal + "$" + absoluto.ToString("N0", CulturaMonetaria);
    }

    public static string Normalizar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        string decomposed = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(decomposed.Length);
        for (int i = 0; i < decomposed.Length; i++)
        {
            char c = decomposed[i];
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    private static Dictionary<string, long> CriarPrecos()
    {
        return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "ares ar", 120000000L }, { "artilharia", 8000000L }, { "nav yuza", 1800000000L },
            { "quartel general", 750000000L }, { "caminhao de transporte", 300000L }, { "hack", 4500000L },
            { "hamer carro", 250000L }, { "leonc1", 12000000L }, { "hg ar", 7000000L },
            { "tank antigravity", 45000000L }, { "tank arthur", 14000000L }, { "tank c1 camuflado", 13000000L },
            { "tank c1 verde", 11000000L }, { "tank codex", 20000000L }, { "tank south", 18000000L },
            { "tank ubu", 10000000L }, { "track combustivel", 450000L },
            { "estaleiro naval", 8000000000L }, { "petroleiro", 150000000L }, { "navio de carga", 90000000L },
            { "marinha", 3500000000L }, { "corveta fortaleza", 350000000L }, { "des wall", 1500000000L },
            { "f200", 1200000000L }, { "uss leviathan", 4000000000L }, { "nav abastecimento", 650000000L },
            { "uss ironclad", 2700000000L }, { "liberty prime", 4500000000L }, { "barco ww transporte", 120000000L },
            { "corveta sam", 450000000L }, { "nav tropa", 1200000000L }, { "uss vindicator", 3600000000L },
            { "nav vigia", 80000000L }, { "nav think", 180000000L }, { "nav global", 13000000000L },
            { "uss arrowhead", 2400000000L }, { "uss dominion", 4200000000L }, { "uss mako", 2900000000L },
            { "uss wraith", 4000000000L },
            { "aeroporto militar", 2500000000L }, { "aeroporto comercial", 4000000000L }, { "a 20", 45000000L }, { "a20", 45000000L },
            { "b260", 160000000L }, { "c17", 280000000L }, { "c700 transporte", 220000000L }, { "c700", 220000000L },
            { "dh hasaf", 25000000L }, { "vans falcon", 95000000L }, { "flow", 80000000L },
            { "garcia g15", 90000000L }, { "g 18m", 110000000L }, { "su11", 75000000L },
            { "su20", 150000000L }, { "super tuk", 18000000L }, { "supra", 85000000L },
            { "drone veloster", 12000000L }, { "vap drone", 4000000L },
            { "heliporto", 60000000L }, { "fabrica", 250000000L }, { "farm", 8000000L },
            { "rua reta", 5000000L }, { "lancador de misseis", 200000000L }, { "lancador icbm", 650000000L },
            { "muro lateral", 3000000L }, { "armazem", 40000000L }, { "armazem militar", 100000000L },
            { "centro de construcao", 80000000L }, { "porto comercial", 1500000000L }, { "centro de distribuicao", 120000000L },
            { "comercio pequeno", 3000000L }, { "shopping center", 180000000L }, { "industria leve", 90000000L },
            { "industria pesada", 450000000L }, { "refinaria", 2500000000L },
            { "energia", 150000000L }, { "usina de carvao", 1200000000L }, { "usina carvao", 1200000000L }, { "usina grande", 3000000000L },
            { "plataforma de petroleo", 2000000000L }, { "usina nuclear", 9000000000L },
            { "usina termica pequena", 300000000L }, { "usina termica grande", 1200000000L },
            { "usina hidreletrica", 4000000000L }, { "usina solar", 180000000L },
            { "casa", 250000L }, { "predio medio", 180000000L }, { "predio medio residencial", 180000000L }, { "vilage medio", 400000000L },
            { "predio hard", 1200000000L }, { "fronteira", 120000000L }, { "prefeitura", 300000000L },
            { "base militar pequena", 450000000L }, { "base militar media", 1200000000L },
            { "grande base militar", 3500000000L }, { "base aerea", 2500000000L }, { "base naval", 3500000000L }
        };
    }

    private static Dictionary<string, long> CriarPrecosMercado()
    {
        return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "comida", 450L }, { "milho", 220L }, { "batata", 180L }, { "feijao", 650L },
            { "trigo", 250L }, { "arroz", 400L }, { "cana de acucar", 55L }, { "algodao", 1800L },
            { "soja", 500L }, { "cafe", 4500L }, { "cacau", 7000L }, { "petroleo", 80L },
            { "aco", 700L }, { "energia", 70L }, { "armamentos", 50000L }, { "uranio", 150000L }
        };
    }
}
