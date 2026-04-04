using System.Collections.Generic;
using UnityEngine;

public static class RegistroEntidadesJogo
{
    private static readonly HashSet<IdentidadeUnidade> Unidades = new HashSet<IdentidadeUnidade>();
    private static readonly HashSet<ControleUnidade> ControlesUnidade = new HashSet<ControleUnidade>();
    private static readonly HashSet<IdentidadeNaval> Navios = new HashSet<IdentidadeNaval>();
    private static readonly HashSet<Imovel> Imoveis = new HashSet<Imovel>();
    private static readonly HashSet<GerenciadorAeroporto> Aeroportos = new HashSet<GerenciadorAeroporto>();
    private static readonly HashSet<ControleAviao> Avioes = new HashSet<ControleAviao>();
    private static readonly HashSet<PierMarinha> Piers = new HashSet<PierMarinha>();
    private static readonly HashSet<Fabrica> Fabricas = new HashSet<Fabrica>();
    private static readonly HashSet<Estaleiro> Estaleiros = new HashSet<Estaleiro>();
    private static readonly HashSet<Heliporto> Heliportos = new HashSet<Heliporto>();

    public static void Register(IdentidadeUnidade unidade)
    {
        if (unidade != null)
        {
            Unidades.Add(unidade);
        }
    }

    public static void Unregister(IdentidadeUnidade unidade)
    {
        if (unidade != null)
        {
            Unidades.Remove(unidade);
        }
    }

    public static void Register(ControleUnidade unidade)
    {
        if (unidade != null)
        {
            ControlesUnidade.Add(unidade);
        }
    }

    public static void Unregister(ControleUnidade unidade)
    {
        if (unidade != null)
        {
            ControlesUnidade.Remove(unidade);
        }
    }

    public static void Register(IdentidadeNaval navio)
    {
        if (navio != null)
        {
            Navios.Add(navio);
        }
    }

    public static void Unregister(IdentidadeNaval navio)
    {
        if (navio != null)
        {
            Navios.Remove(navio);
        }
    }

    public static void Register(Imovel imovel)
    {
        if (imovel != null)
        {
            Imoveis.Add(imovel);
        }
    }

    public static void Unregister(Imovel imovel)
    {
        if (imovel != null)
        {
            Imoveis.Remove(imovel);
        }
    }

    public static void Register(GerenciadorAeroporto aeroporto)
    {
        if (aeroporto != null)
        {
            Aeroportos.Add(aeroporto);
        }
    }

    public static void Unregister(GerenciadorAeroporto aeroporto)
    {
        if (aeroporto != null)
        {
            Aeroportos.Remove(aeroporto);
        }
    }

    public static void Register(ControleAviao aviao)
    {
        if (aviao != null)
        {
            Avioes.Add(aviao);
        }
    }

    public static void Unregister(ControleAviao aviao)
    {
        if (aviao != null)
        {
            Avioes.Remove(aviao);
        }
    }

    public static void Register(PierMarinha pier)
    {
        if (pier != null)
        {
            Piers.Add(pier);
        }
    }

    public static void Unregister(PierMarinha pier)
    {
        if (pier != null)
        {
            Piers.Remove(pier);
        }
    }

    public static void Register(Fabrica fabrica)
    {
        if (fabrica != null)
        {
            Fabricas.Add(fabrica);
        }
    }

    public static void Unregister(Fabrica fabrica)
    {
        if (fabrica != null)
        {
            Fabricas.Remove(fabrica);
        }
    }

    public static void Register(Estaleiro estaleiro)
    {
        if (estaleiro != null)
        {
            Estaleiros.Add(estaleiro);
        }
    }

    public static void Unregister(Estaleiro estaleiro)
    {
        if (estaleiro != null)
        {
            Estaleiros.Remove(estaleiro);
        }
    }

    public static void Register(Heliporto heliporto)
    {
        if (heliporto != null)
        {
            Heliportos.Add(heliporto);
        }
    }

    public static void Unregister(Heliporto heliporto)
    {
        if (heliporto != null)
        {
            Heliportos.Remove(heliporto);
        }
    }

    public static void FillUnidades(List<IdentidadeUnidade> destino)
    {
        Fill(Unidades, destino);
    }

    public static void FillControlesUnidade(List<ControleUnidade> destino)
    {
        Fill(ControlesUnidade, destino);
    }

    public static void FillNavios(List<IdentidadeNaval> destino)
    {
        Fill(Navios, destino);
    }

    public static void FillImoveis(List<Imovel> destino)
    {
        Fill(Imoveis, destino);
    }

    public static void FillAeroportos(List<GerenciadorAeroporto> destino)
    {
        Fill(Aeroportos, destino);
    }

    public static void FillPiers(List<PierMarinha> destino)
    {
        Fill(Piers, destino);
    }

    public static void FillFabricas(List<Fabrica> destino)
    {
        Fill(Fabricas, destino);
    }

    public static void FillEstaleiros(List<Estaleiro> destino)
    {
        Fill(Estaleiros, destino);
    }

    public static void FillHeliportos(List<Heliporto> destino)
    {
        Fill(Heliportos, destino);
    }

    public static void FillAvioes(List<ControleAviao> destino)
    {
        Fill(Avioes, destino);
    }

    public static PierMarinha GetPrimeiroPier()
    {
        return GetPrimeiroValido(Piers);
    }

    public static GerenciadorAeroporto GetPrimeiroAeroporto()
    {
        return GetPrimeiroValido(Aeroportos);
    }

    private static void Fill<T>(HashSet<T> origem, List<T> destino) where T : Object
    {
        if (destino == null)
        {
            return;
        }

        destino.Clear();
        foreach (T item in origem)
        {
            if (item != null)
            {
                destino.Add(item);
            }
        }
    }

    private static T GetPrimeiroValido<T>(HashSet<T> origem) where T : Object
    {
        foreach (T item in origem)
        {
            if (item != null)
            {
                return item;
            }
        }

        return null;
    }
}
