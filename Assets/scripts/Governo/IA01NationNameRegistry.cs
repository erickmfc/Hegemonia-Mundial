using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class IA01NationNameRegistry
{
    private static readonly string[] CountryNames =
    {
        "Valtoria", "Kerengard", "Norhavia", "Aurelmark", "Draskovia", "Solenthar", "Vestrand", "Calmyra",
        "Ostenbach", "Thelvara", "Ambrisk", "Nordheim Livre", "Cravenhold", "Ilharion", "Sentaria",
        "Ravenmoor", "Belquira", "Torvengard", "Meridian do Sul", "Wyndara", "Confederacao Solaris",
        "Reino de Altamira", "Liga de Norvengard", "Estado de Aurora", "Federacao de Arken", "Dominio de Vesper"
    };

    private static readonly string[] PresidentNames =
    {
        "Aldric Vosen", "Marlene Kastov", "Henrik Solari", "Ivara Denn", "Corwin Blaise",
        "Theodora Rennick", "Emeric Falkstead", "Nadia Corvain", "Lucian Marek", "Selene Hartwick",
        "Damir Kalen", "Yvonne Straka", "Helena Vasconcelos", "Artur Montenegro", "Mirela Duarte",
        "Caio Valente", "Sofia Amaral", "Dario Ferraz", "Livia Nogueira", "Henrique Saldanha"
    };

    public static void GarantirNomesUnicos(IList<DadosPaisGoverno> paises, int seed = 0)
    {
        if (paises == null) return;
        System.Random random = new System.Random(seed == 0 ? Environment.TickCount : seed);
        List<string> countries = CountryNames.OrderBy(x => random.Next()).ToList();
        List<string> presidents = PresidentNames.OrderBy(x => random.Next()).ToList();
        HashSet<string> usedCountries = new HashSet<string>(paises.Where(p => p != null && p.teamId > 0 && !string.IsNullOrWhiteSpace(p.nomePais)).Select(p => p.nomePais), StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedPresidents = new HashSet<string>(paises.Where(p => p != null && p.teamId > 0 && !string.IsNullOrWhiteSpace(p.nomePresidente)).Select(p => p.nomePresidente), StringComparer.OrdinalIgnoreCase);
        int index = 0;
        foreach (DadosPaisGoverno pais in paises.OrderBy(x => x != null ? x.teamId : int.MaxValue))
        {
            if (pais == null || pais.teamId <= 1) continue;
            bool duplicateCountry = string.IsNullOrWhiteSpace(pais.nomePais) || !usedCountries.Add(pais.nomePais);
            bool duplicatePresident = string.IsNullOrWhiteSpace(pais.nomePresidente) || !usedPresidents.Add(pais.nomePresidente);
            if (duplicateCountry)
            {
                string name;
                do { name = countries[index++ % countries.Count]; } while (usedCountries.Contains(name) && index < countries.Count * 2);
                pais.nomePais = name + (usedCountries.Contains(name) ? " " + pais.teamId : string.Empty);
                usedCountries.Add(pais.nomePais);
            }
            if (duplicatePresident)
            {
                string name;
                do { name = presidents[(index + pais.teamId) % presidents.Count]; } while (usedPresidents.Contains(name) && index < presidents.Count * 2);
                pais.nomePresidente = name;
                usedPresidents.Add(name);
            }
        }
    }

    public static void SortearNomesDePartida(IList<DadosPaisGoverno> paises, int seed)
    {
        if (paises == null) return;
        System.Random random = new System.Random(seed == 0 ? Environment.TickCount : seed);
        List<string> countries = CountryNames.OrderBy(x => random.Next()).ToList();
        List<string> presidents = PresidentNames.OrderBy(x => random.Next()).ToList();
        int index = 0;
        foreach (DadosPaisGoverno pais in paises.OrderBy(x => x != null ? x.teamId : int.MaxValue))
        {
            if (pais == null || pais.teamId <= 1) continue;
            bool generated = string.IsNullOrWhiteSpace(pais.nomePais) || CountryNames.Contains(pais.nomePais) || pais.nomePais.StartsWith("Pais IA", StringComparison.OrdinalIgnoreCase);
            if (!generated) continue;
            pais.nomePais = countries[index % countries.Count];
            pais.nomePresidente = presidents[(index + 3) % presidents.Count];
            index++;
        }
        GarantirNomesUnicos(paises, seed + 17);
        GarantirMoedasUnicas(paises);
    }

    public static void GarantirMoedasUnicas(IList<DadosPaisGoverno> paises)
    {
        if (paises == null) return;
        HashSet<string> usadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DadosPaisGoverno pais in paises.OrderBy(p => p != null ? p.teamId : int.MaxValue))
        {
            if (pais == null) continue;
            string baseNome = (pais.nomePais ?? "Nacao").Split(' ')[0].Trim();
            if (string.IsNullOrWhiteSpace(baseNome)) baseNome = "Nacao";
            string moeda = baseNome + "o";
            if (moeda.Length > 12) moeda = baseNome.Substring(0, Math.Min(9, baseNome.Length)) + "o";
            string original = moeda; int sufixo = 2;
            while (usadas.Contains(moeda)) moeda = original + sufixo++;
            if (pais.teamId > 1 || string.IsNullOrWhiteSpace(pais.nomeMoeda) || pais.nomeMoeda.StartsWith("Moeda ", StringComparison.OrdinalIgnoreCase))
                pais.nomeMoeda = moeda;
            if (pais.teamId > 0) usadas.Add(pais.nomeMoeda);
            pais.simboloMoeda = GerarSimbolo(pais.nomeMoeda);
        }
    }

    private static string GerarSimbolo(string nome)
    {
        string sigla = new string((nome ?? "DH").Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(sigla) ? "NAC$" : sigla + "$";
    }
}
