using UnityEngine;

/// <summary>
/// Unidade rápida da Guarda Costeira. O voo e o retorno pertencem ao
/// Helicoptero/GerenciadorAeroporto existentes.
/// </summary>
[RequireComponent(typeof(Helicoptero))]
[RequireComponent(typeof(SistemaDeDanos))]
[RequireComponent(typeof(IdentidadeUnidade))]
[RequireComponent(typeof(CombustivelUnidade))]
[RequireComponent(typeof(ControleUnidade))]
public sealed class UH60GuardaCosteira : GuardaCosteiraUnidade
{
    // Configurado no prefab; o voo continua delegado ao Helicoptero existente.
    private Helicoptero helicoptero;
    private GerenciadorAeroporto aeroporto;

    protected override void Awake()
    {
        capacidadeResgate = 15;
        tripulacao = 5;
        tempoBuscaSegundos = 20f;
        areaBusca = 50f;
        base.Awake();
        helicoptero = GetComponent<Helicoptero>() ?? GetComponentInParent<Helicoptero>();
        aeroporto = EncontrarAeroporto();
    }

    protected override bool PodeOperarBase()
    {
        return helicoptero != null && helicoptero.TemOrigemAeroportoRegistrada();
    }

    protected override bool NavegarAte(Vector3 destino)
    {
        if (!PodeOperarBase() || !helicoptero.EstaDisponivel()) return false;

        ControleUnidade controle = GetComponent<ControleUnidade>()
            ?? GetComponentInParent<ControleUnidade>();
        if (controle != null)
        {
            return controle.EmitirOrdemMover(destino, true);
        }

        // Compatibilidade para o prefab antigo que ainda não possui a
        // autoridade central de ordens.
        helicoptero.Decolar(destino);
        return true;
    }

    protected override void RetornarParaBase()
    {
        if (helicoptero != null) helicoptero.RetornarParaVagaAeroporto();
    }

    protected override bool EstaNaBase()
    {
        return helicoptero != null && helicoptero.EstaEstacionadoNoAeroporto();
    }

    protected override bool DeveAbortarPorDano(SistemaDeDanos sistema)
    {
        return sistema != null && sistema.vidaAtual < sistema.vidaMaxima;
    }

    private GerenciadorAeroporto EncontrarAeroporto()
    {
        GerenciadorAeroporto[] aeroportos = FindObjectsByType<GerenciadorAeroporto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < aeroportos.Length; i++)
        {
            if (aeroportos[i] != null && aeroportos[i].helicopterosDoAeroporto.Contains(helicoptero)) return aeroportos[i];
        }
        return null;
    }
}
