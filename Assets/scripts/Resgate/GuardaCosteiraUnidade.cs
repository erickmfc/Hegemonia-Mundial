using UnityEngine;

public enum EstadoGuardaCosteira
{
    Disponivel,
    Designada,
    ACaminho,
    Buscando,
    Retornando,
    Desembarcando,
    Danificada,
    Indisponivel
}

/// <summary>
/// Ciclo comum das duas unidades. O componente não move a unidade: cada classe
/// filha chama o controlador de voo ou naval que já existe no projeto.
/// </summary>
public abstract class GuardaCosteiraUnidade : MonoBehaviour
{
    [Header("Identidade")]
    [SerializeField] protected int teamId = 1;
    [SerializeField] protected int nationId;
    [SerializeField] protected string rescueUnitId = string.Empty;

    [Header("Busca e salvamento")]
    [SerializeField, Min(1)] protected int capacidadeResgate = 15;
    [SerializeField, Min(1)] protected int tripulacao = 5;
    [SerializeField, Min(1f)] protected float tempoBuscaSegundos = 20f;
    [SerializeField, Min(1f)] protected float areaBusca = 50f;
    [SerializeField, Min(0.1f)] protected float intervaloBuscaSegundos = 0.75f;
    [SerializeField, Min(1f)] protected float distanciaChegada = 25f;

    protected SistemaDeDanos danos;
    private float cronometroBusca;
    private float proximaBusca;

    public int TeamId => teamId;
    public int NationId => nationId;
    public string RescueUnitId => string.IsNullOrWhiteSpace(rescueUnitId) ? name + "-" + GetInstanceID() : rescueUnitId;
    public int CapacidadeResgate => Mathf.Max(1, capacidadeResgate);
    public int Tripulacao => Mathf.Max(1, tripulacao);
    public float TempoBuscaSegundos => Mathf.Max(1f, tempoBuscaSegundos);
    public float AreaBusca => Mathf.Max(1f, areaBusca);
    public int PessoasEmbarcadas { get; private set; }
    public int TotalHistoricoResgates { get; private set; }
    public string IncidentIdAtual { get; private set; } = string.Empty;
    public EstadoGuardaCosteira Estado { get; private set; } = EstadoGuardaCosteira.Disponivel;
    public bool PodeSerDespachada => Estado == EstadoGuardaCosteira.Disponivel && PodeOperarBase();

    protected abstract bool PodeOperarBase();
    protected abstract bool NavegarAte(Vector3 destino);
    protected abstract void RetornarParaBase();
    protected abstract bool EstaNaBase();
    protected abstract bool DeveAbortarPorDano(SistemaDeDanos sistema);

    protected virtual void Awake()
    {
        if (string.IsNullOrWhiteSpace(rescueUnitId)) rescueUnitId = name + "-" + GetInstanceID();
        danos = GetComponent<SistemaDeDanos>() ?? GetComponentInParent<SistemaDeDanos>();
        if (danos != null)
        {
            danos.OnDano -= AoReceberDano;
            danos.OnDano += AoReceberDano;
        }
        if (nationId <= 0)
        {
            IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>() ?? GetComponentInParent<IdentidadeUnidade>();
            if (identidade != null) nationId = identidade.teamID;
        }
    }

    protected virtual void OnEnable()
    {
        SistemaGuardaCosteira.GarantirInstancia();
        if (SistemaGuardaCosteira.Instancia != null) SistemaGuardaCosteira.Instancia.RegistrarUnidade(this);
    }

    protected virtual void OnDisable()
    {
        if (SistemaGuardaCosteira.Instancia != null) SistemaGuardaCosteira.Instancia.RemoverUnidade(this);
        if (danos != null) danos.OnDano -= AoReceberDano;
    }

    protected virtual void Update()
    {
        if (string.IsNullOrEmpty(IncidentIdAtual) || SistemaGuardaCosteira.Instancia == null) return;
        RescueIncident incidente = SistemaGuardaCosteira.Instancia.ObterIncidente(IncidentIdAtual);
        if (incidente == null) { EncerrarMissao(); return; }

        if (Estado == EstadoGuardaCosteira.Designada || Estado == EstadoGuardaCosteira.ACaminho)
        {
            Estado = EstadoGuardaCosteira.ACaminho;
            if (Vector3.Distance(transform.position, incidente.Position) <= Mathf.Max(2f, distanciaChegada))
            {
                Estado = EstadoGuardaCosteira.Buscando;
                cronometroBusca = 0f;
                proximaBusca = 0f;
            }
            return;
        }

        if (Estado == EstadoGuardaCosteira.Buscando)
        {
            if (!incidente.IsActiveAt(DiaAtual()) || incidente.AvailableForReservation(DiaAtual()) <= 0 || cronometroBusca >= TempoBuscaSegundos)
            {
                IniciarRetorno();
                return;
            }
            cronometroBusca += Time.deltaTime;
            if (Time.unscaledTime >= proximaBusca)
            {
                proximaBusca = Time.unscaledTime + Mathf.Max(0.15f, intervaloBuscaSegundos);
                int restante = Mathf.Max(0, CapacidadeResgate - PessoasEmbarcadas);
                int esperado = Mathf.Max(1, Mathf.CeilToInt(CapacidadeResgate * intervaloBuscaSegundos / TempoBuscaSegundos));
                int quantidade = Mathf.Clamp(Mathf.RoundToInt(esperado * Random.Range(0.65f, 1.35f)), 1, restante);
                int resgatado = SistemaGuardaCosteira.Instancia.ReservarEPegar(IncidentIdAtual, this, quantidade);
                PessoasEmbarcadas += resgatado;
                TotalHistoricoResgates += resgatado;
                if (PessoasEmbarcadas >= CapacidadeResgate || incidente.AvailableForReservation(DiaAtual()) <= 0)
                    IniciarRetorno();
            }
            return;
        }

        if (Estado == EstadoGuardaCosteira.Retornando && EstaNaBase())
        {
            Estado = EstadoGuardaCosteira.Desembarcando;
            if (PessoasEmbarcadas > 0)
            {
                int desembarcados = SistemaGuardaCosteira.Instancia.Desembarcar(IncidentIdAtual, this, PessoasEmbarcadas);
                PessoasEmbarcadas -= desembarcados;
            }
            SistemaGuardaCosteira.Instancia.LiberarUnidade(this);
            IncidentIdAtual = string.Empty;
            Estado = EstadoGuardaCosteira.Disponivel;
        }
    }

    public bool DesignarResgate(RescueIncident incidente)
    {
        if (incidente == null || !PodeSerDespachada) return false;
        if (!NavegarAte(incidente.Position)) return false;
        IncidentIdAtual = incidente.IncidentId;
        Estado = EstadoGuardaCosteira.Designada;
        return true;
    }

    public void AbortarResgate(string motivo = "DANO CONFIRMADO")
    {
        if (string.IsNullOrEmpty(IncidentIdAtual)) return;
        Estado = EstadoGuardaCosteira.Retornando;
        RetornarParaBase();
    }

    public void LimparPessoasEmbarcadas()
    {
        PessoasEmbarcadas = 0;
    }

    protected void IniciarRetorno()
    {
        if (Estado == EstadoGuardaCosteira.Retornando) return;
        Estado = EstadoGuardaCosteira.Retornando;
        RetornarParaBase();
    }

    private void EncerrarMissao()
    {
        IncidentIdAtual = string.Empty;
        Estado = EstadoGuardaCosteira.Disponivel;
    }

    private void AoReceberDano()
    {
        if (!string.IsNullOrEmpty(IncidentIdAtual) && DeveAbortarPorDano(danos))
            AbortarResgate();
    }

    private static int DiaAtual()
    {
        return GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
    }
}
