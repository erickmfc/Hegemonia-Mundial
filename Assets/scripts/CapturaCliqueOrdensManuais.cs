using UnityEngine;

public static class CapturaCliqueOrdensManuais
{
    public static bool EstaAtiva()
    {
        if (DesenharLinhasOrdem.ConsumiuCliqueEsteFrame())
        {
            return true;
        }

        GerenciadorAeroporto[] aeroportos = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
        for (int i = 0; i < aeroportos.Length; i++)
        {
            GerenciadorAeroporto aeroporto = aeroportos[i];
            if (aeroporto != null && aeroporto.PossuiOrdemManualAtiva())
            {
                return true;
            }
        }

        NavioTransporteTropas[] navios = Object.FindObjectsByType<NavioTransporteTropas>(FindObjectsSortMode.None);
        for (int i = 0; i < navios.Length; i++)
        {
            NavioTransporteTropas navio = navios[i];
            if (navio != null && navio.PossuiOrdemManualAtiva())
            {
                return true;
            }
        }

        DesenharLinhasOrdem[] desenhadores = Object.FindObjectsByType<DesenharLinhasOrdem>(FindObjectsSortMode.None);
        for (int i = 0; i < desenhadores.Length; i++)
        {
            DesenharLinhasOrdem desenhador = desenhadores[i];
            if (desenhador != null && (desenhador.modoPatrulhaAtivo || desenhador.modoSeguirAtivo))
            {
                return true;
            }
        }

        return false;
    }
}
