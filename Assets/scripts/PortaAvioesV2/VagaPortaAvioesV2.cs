using UnityEngine;

/// <summary>
/// Estado persistente de uma vaga. Mantido em um arquivo próprio para que o
/// Unity serialize VagaPortaAvioesV2 como um tipo distinto do layout.
/// </summary>
public sealed class VagaPortaAvioesV2 : MonoBehaviour
{
    public string id = "Vaga";
    public TipoAeronavePortaAvioesV2 tipoPermitido = TipoAeronavePortaAvioesV2.Qualquer;
    public float tamanhoMaximo = 12f;
    public EstadoVagaPortaAvioesV2 estado = EstadoVagaPortaAvioesV2.Livre;
    public string aeronaveReservadaId;
    public string aeronaveOcupanteId;

    public bool Reservar(string aeronaveId)
    {
        if (estado != EstadoVagaPortaAvioesV2.Livre || string.IsNullOrEmpty(aeronaveId)) return false;
        estado = EstadoVagaPortaAvioesV2.Reservada;
        aeronaveReservadaId = aeronaveId;
        return true;
    }

    public bool Ocupar(string aeronaveId)
    {
        if (aeronaveReservadaId != aeronaveId && estado != EstadoVagaPortaAvioesV2.Livre) return false;
        estado = EstadoVagaPortaAvioesV2.Ocupada;
        aeronaveOcupanteId = aeronaveId;
        aeronaveReservadaId = string.Empty;
        return true;
    }

    public void Liberar(string aeronaveId)
    {
        if (aeronaveReservadaId == aeronaveId) aeronaveReservadaId = string.Empty;
        if (aeronaveOcupanteId == aeronaveId) aeronaveOcupanteId = string.Empty;
        if (string.IsNullOrEmpty(aeronaveReservadaId) && string.IsNullOrEmpty(aeronaveOcupanteId) && estado != EstadoVagaPortaAvioesV2.Bloqueada)
            estado = EstadoVagaPortaAvioesV2.Livre;
    }
}
