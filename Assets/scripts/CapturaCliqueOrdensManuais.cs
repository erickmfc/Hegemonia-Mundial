using UnityEngine;

public static class CapturaCliqueOrdensManuais
{
    public static bool EstaAtiva()
    {
        InteractionModeSnapshot snapshot = InteractionModeService.CurrentSnapshot();
        if (snapshot.Owner != InteractionOwner.None && snapshot.Owner != InteractionOwner.SelectionBox)
        {
            return true;
        }

        if (DesenharLinhasOrdem.ConsumiuCliqueEsteFrame())
        {
            return true;
        }

        return false;
    }
}
