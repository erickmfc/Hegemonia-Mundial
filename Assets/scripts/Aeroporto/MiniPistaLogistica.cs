using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pista avançada exclusiva para o avião de transporte pesado.
/// Todos os pontos de pouso pertencem à pista; o C700 nunca cria um destino
/// de pouso aleatório por conta própria.
/// </summary>
[DisallowMultipleComponent]
public sealed class MiniPistaLogistica : MonoBehaviour
{
    private static readonly HashSet<MiniPistaLogistica> Registro = new HashSet<MiniPistaLogistica>();

    [Header("Identidade e operação")]
    public int teamId = 1;
    public bool operacional = true;
    public bool permitirTerritorioInimigo = false;
    public bool criarPontosPadraoSeAusentes = true;
    [Min(1)] public int capacidadeTropas = 1000;
    [Min(0)] public int tropasEsperando;
    [Min(50f)] public float raioAceitacaoDestino = 250f;

    [Header("Pontos fixos da pista")]
    public Transform approachPoint;
    public Transform landingPoint;
    public Transform runwayStart;
    public Transform runwayEnd;
    public Transform parkingPoint;
    public Transform takeoffPoint;
    public Transform exitPoint;
    public Transform troopBoardingPoint;
    public Transform troopUnloadPoint;
    public Transform goAroundPoint;

    private C700TransporteAereo reservaPista;
    private C700TransporteAereo estacionamentoOcupadoPor;

    public bool PistaValida
    {
        get
        {
            return isActiveAndEnabled
                && operacional
                && approachPoint != null
                && landingPoint != null
                && runwayStart != null
                && runwayEnd != null
                && parkingPoint != null
                && takeoffPoint != null
                && exitPoint != null
                && troopBoardingPoint != null
                && troopUnloadPoint != null
                && goAroundPoint != null;
        }
    }

    public bool RunwayReserved => reservaPista != null;
    public C700TransporteAereo RunwayReservedBy => reservaPista;
    public bool ParkingReserved => estacionamentoOcupadoPor != null;
    public C700TransporteAereo ParkingReservedBy => estacionamentoOcupadoPor;
    public int VagasTropas => Mathf.Max(0, capacidadeTropas - tropasEsperando);

    private void Awake()
    {
        if (criarPontosPadraoSeAusentes) GarantirPontosFixos();
    }

    private void OnEnable()
    {
        Registro.Add(this);
    }

    private void OnDisable()
    {
        Registro.Remove(this);
        reservaPista = null;
        estacionamentoOcupadoPor = null;
    }

    public bool TryReserve(C700TransporteAereo aviao)
    {
        if (aviao == null || !PistaValida) return false;
        if (reservaPista != null && reservaPista != aviao) return false;
        reservaPista = aviao;
        return true;
    }

    public void Release(C700TransporteAereo aviao)
    {
        if (aviao == null || reservaPista == aviao) reservaPista = null;
    }

    public bool TryReserveParking(C700TransporteAereo aviao)
    {
        if (aviao == null || !PistaValida) return false;
        if (estacionamentoOcupadoPor != null && estacionamentoOcupadoPor != aviao) return false;
        estacionamentoOcupadoPor = aviao;
        return true;
    }

    public void ReleaseParking(C700TransporteAereo aviao)
    {
        if (aviao == null || estacionamentoOcupadoPor == aviao) estacionamentoOcupadoPor = null;
    }

    public void ReleaseAll(C700TransporteAereo aviao)
    {
        Release(aviao);
        ReleaseParking(aviao);
    }

    public bool PodeReceber(C700TransporteAereo aviao, Vector3 origem)
    {
        if (aviao == null || !PistaValida) return false;
        if (RunwayReserved && reservaPista != aviao) return false;
        if (ParkingReserved && estacionamentoOcupadoPor != aviao) return false;
        return raioAceitacaoDestino <= 0f || Vector3.Distance(transform.position, origem) <= raioAceitacaoDestino;
    }

    public bool AdicionarTropas(int quantidade)
    {
        quantidade = Mathf.Max(0, quantidade);
        int aceito = Mathf.Min(quantidade, VagasTropas);
        tropasEsperando += aceito;
        return aceito == quantidade;
    }

    public int RetirarTropas(int quantidade)
    {
        int retirado = Mathf.Min(Mathf.Max(0, quantidade), tropasEsperando);
        tropasEsperando -= retirado;
        return retirado;
    }

    public Vector3 ObterPontoAproximacao()
    {
        return approachPoint != null ? approachPoint.position : transform.position;
    }

    public static MiniPistaLogistica LocalizarMaisProxima(Vector3 destino, float raio, int teamId, bool aceitarInimiga = false)
    {
        MiniPistaLogistica melhor = null;
        float melhorDistancia = float.MaxValue;
        float raioAceito = Mathf.Max(1f, raio);

        foreach (MiniPistaLogistica pista in Registro)
        {
            if (pista == null || !pista.PistaValida) continue;
            bool mesmaEquipe = pista.teamId == 0 || teamId == 0 || pista.teamId == teamId;
            if (!mesmaEquipe && !aceitarInimiga && !pista.permitirTerritorioInimigo) continue;

            float distancia = Vector3.Distance(pista.transform.position, destino);
            if (distancia > raioAceito || distancia >= melhorDistancia) continue;
            melhor = pista;
            melhorDistancia = distancia;
        }

        return melhor;
    }

    public static MiniPistaLogistica LocalizarPorNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return null;
        foreach (MiniPistaLogistica pista in Registro)
        {
            if (pista != null && string.Equals(pista.name, nome, System.StringComparison.OrdinalIgnoreCase)) return pista;
        }
        return null;
    }

    private void GarantirPontosFixos()
    {
        approachPoint = GarantirPonto(approachPoint, "ApproachPoint", new Vector3(0f, 80f, -250f));
        landingPoint = GarantirPonto(landingPoint, "LandingPoint", new Vector3(0f, 20f, -150f));
        runwayStart = GarantirPonto(runwayStart, "RunwayStart", new Vector3(0f, 0f, -100f));
        runwayEnd = GarantirPonto(runwayEnd, "RunwayEnd", new Vector3(0f, 0f, 100f));
        parkingPoint = GarantirPonto(parkingPoint, "ParkingPoint", new Vector3(65f, 0f, 115f));
        takeoffPoint = GarantirPonto(takeoffPoint, "TakeoffPoint", new Vector3(0f, 0f, -100f));
        exitPoint = GarantirPonto(exitPoint, "ExitPoint", new Vector3(0f, 80f, 260f));
        troopBoardingPoint = GarantirPonto(troopBoardingPoint, "TroopBoardingPoint", new Vector3(65f, 1f, 115f));
        troopUnloadPoint = GarantirPonto(troopUnloadPoint, "TroopUnloadPoint", new Vector3(65f, 1f, 115f));
        goAroundPoint = GarantirPonto(goAroundPoint, "GoAroundPoint", new Vector3(0f, 80f, -320f));
    }

    private Transform GarantirPonto(Transform ponto, string nome, Vector3 localPosition)
    {
        if (ponto != null) return ponto;
        Transform existente = transform.Find(nome);
        if (existente != null) return existente;
        GameObject criado = new GameObject(nome);
        criado.transform.SetParent(transform, false);
        criado.transform.localPosition = localPosition;
        criado.transform.localRotation = Quaternion.identity;
        return criado.transform;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = PistaValida ? Color.cyan : Color.red;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.2f, new Vector3(120f, 0.4f, 240f));
        DesenharPonto(approachPoint, Color.yellow);
        DesenharPonto(landingPoint, Color.white);
        DesenharPonto(runwayStart, Color.green);
        DesenharPonto(runwayEnd, Color.green);
        DesenharPonto(parkingPoint, Color.blue);
        DesenharPonto(troopBoardingPoint, Color.magenta);
        DesenharPonto(troopUnloadPoint, Color.magenta);
        DesenharPonto(goAroundPoint, Color.red);
    }

    private static void DesenharPonto(Transform ponto, Color cor)
    {
        if (ponto == null) return;
        Gizmos.color = cor;
        Gizmos.DrawSphere(ponto.position, 2f);
        Gizmos.DrawLine(ponto.position, ponto.position + ponto.forward * 12f);
    }
}

[System.Serializable]
public sealed class TransportMission
{
    public MiniPistaLogistica pickupPista;
    public MiniPistaLogistica deliveryPista;
    public bool carregarNaOrigem = true;
    public bool descarregarNoDestino = true;

    public TransportMission(MiniPistaLogistica pickup, MiniPistaLogistica delivery)
    {
        pickupPista = pickup;
        deliveryPista = delivery;
    }
}
