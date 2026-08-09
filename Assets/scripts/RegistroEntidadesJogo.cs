using System.Collections.Generic;
using UnityEngine;

public static class RegistroEntidadesJogo
{
    private sealed class EntitySet<T> : HashSet<T>, System.Collections.ICollection where T : Object
    {
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public void CopyTo(System.Array array, int index)
        {
            int destino = index;
            foreach (T item in this)
            {
                array.SetValue(item, destino++);
            }
        }
    }

    public static event System.Action EntidadesAlteradas;

    private static readonly HashSet<IdentidadeUnidade> Unidades = new HashSet<IdentidadeUnidade>();
    private static readonly HashSet<ControleUnidade> ControlesUnidade = new HashSet<ControleUnidade>();
    private static readonly HashSet<IdentidadeNaval> Navios = new HashSet<IdentidadeNaval>();
    private static readonly HashSet<IdentidadeIA> IdentidadesIA = new HashSet<IdentidadeIA>();
    private static readonly HashSet<Imovel> Imoveis = new HashSet<Imovel>();
    private static readonly HashSet<GerenciadorAeroporto> Aeroportos = new HashSet<GerenciadorAeroporto>();
    private static readonly EntitySet<ControleAviao> Avioes = new EntitySet<ControleAviao>();
    private static readonly EntitySet<Helicoptero> Helicopteros = new EntitySet<Helicoptero>();
    private static readonly HashSet<PierMarinha> Piers = new HashSet<PierMarinha>();
    private static readonly HashSet<Fabrica> Fabricas = new HashSet<Fabrica>();
    private static readonly HashSet<Estaleiro> Estaleiros = new HashSet<Estaleiro>();
    private static readonly HashSet<Heliporto> Heliportos = new HashSet<Heliporto>();

    public static void Register(IdentidadeUnidade unidade)
    {
        if (unidade != null && Unidades.Add(unidade))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(IdentidadeUnidade unidade)
    {
        if (unidade != null && Unidades.Remove(unidade))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(ControleUnidade unidade)
    {
        if (unidade != null && ControlesUnidade.Add(unidade))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(ControleUnidade unidade)
    {
        if (unidade != null && ControlesUnidade.Remove(unidade))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(IdentidadeIA identidade)
    {
        if (identidade != null && IdentidadesIA.Add(identidade))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(IdentidadeIA identidade)
    {
        if (identidade != null && IdentidadesIA.Remove(identidade))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(IdentidadeNaval navio)
    {
        if (navio != null && Navios.Add(navio))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(IdentidadeNaval navio)
    {
        if (navio != null && Navios.Remove(navio))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(Imovel imovel)
    {
        if (imovel != null && Imoveis.Add(imovel))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(Imovel imovel)
    {
        if (imovel != null && Imoveis.Remove(imovel))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(GerenciadorAeroporto aeroporto)
    {
        if (aeroporto != null && Aeroportos.Add(aeroporto))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(GerenciadorAeroporto aeroporto)
    {
        if (aeroporto != null && Aeroportos.Remove(aeroporto))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(ControleAviao aviao)
    {
        if (aviao != null && Avioes.Add(aviao))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(ControleAviao aviao)
    {
        if (aviao != null && Avioes.Remove(aviao))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(Helicoptero helicoptero)
    {
        if (helicoptero != null && Helicopteros.Add(helicoptero))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(Helicoptero helicoptero)
    {
        if (helicoptero != null && Helicopteros.Remove(helicoptero))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(PierMarinha pier)
    {
        if (pier != null && Piers.Add(pier))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(PierMarinha pier)
    {
        if (pier != null && Piers.Remove(pier))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(Fabrica fabrica)
    {
        if (fabrica != null && Fabricas.Add(fabrica))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(Fabrica fabrica)
    {
        if (fabrica != null && Fabricas.Remove(fabrica))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(Estaleiro estaleiro)
    {
        if (estaleiro != null && Estaleiros.Add(estaleiro))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(Estaleiro estaleiro)
    {
        if (estaleiro != null && Estaleiros.Remove(estaleiro))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Register(Heliporto heliporto)
    {
        if (heliporto != null && Heliportos.Add(heliporto))
        {
            EntidadesAlteradas?.Invoke();
        }
    }

    public static void Unregister(Heliporto heliporto)
    {
        if (heliporto != null && Heliportos.Remove(heliporto))
        {
            EntidadesAlteradas?.Invoke();
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

    public static void FillIdentidadesIA(List<IdentidadeIA> destino)
    {
        Fill(IdentidadesIA, destino);
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

    public static void FillHelicopteros(List<Helicoptero> destino)
    {
        Fill(Helicopteros, destino);
    }

    public static PierMarinha GetPrimeiroPier()
    {
        return GetPrimeiroValido(Piers);
    }

    public static GerenciadorAeroporto GetPrimeiroAeroporto()
    {
        return GetPrimeiroValido(Aeroportos);
    }

    public static Helicoptero GetPrimeiroHelicoptero()
    {
        return GetPrimeiroValido(Helicopteros);
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
