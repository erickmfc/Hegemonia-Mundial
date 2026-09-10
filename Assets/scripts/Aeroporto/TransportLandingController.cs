using UnityEngine;

/// <summary>Reserva exclusiva da pista e controle de abortar arremetida.</summary>
[DisallowMultipleComponent]
public sealed class TransportLandingController : MonoBehaviour
{
    public MiniPistaLogistica pistaReservada;
    public int tentativasPouso;
    [Min(1)] public int maxTentativas = 3;

    public bool RunwayReserved => pistaReservada != null;

    public bool TryReserve(MiniPistaLogistica pista, C700TransporteAereo aviao)
    {
        if (pista == null || aviao == null) return false;
        if (!pista.TryReserve(aviao)) return false;
        pistaReservada = pista;
        return true;
    }

    public void Release()
    {
        if (pistaReservada != null) pistaReservada.Release(GetComponent<C700TransporteAereo>());
        pistaReservada = null;
    }

    public bool RegisterLandingAttempt()
    {
        tentativasPouso++;
        // A primeira falha consome a primeira tentativa. Só há nova
        // aproximação enquanto o limite total ainda não foi atingido.
        return tentativasPouso < Mathf.Max(1, maxTentativas);
    }

    public void ResetAttempts()
    {
        tentativasPouso = 0;
    }

    public void AbortLanding()
    {
        Release();
        ResetAttempts();
    }

    private void OnDisable()
    {
        Release();
    }
}
