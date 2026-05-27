using UnityEngine;

public class TripulacaoUnidade : MonoBehaviour
{
    public int tamanhoTripulacao = 1;
    public int teamID;
    private bool devolvida = false;

    public static TripulacaoUnidade Garantir(GameObject go)
    {
        if (go == null) return null;
        var ident = go.GetComponent<IdentidadeUnidade>();
        if (ident == null || ident.tipoUnidade == TipoUnidade.Estrutura) return null;

        var trip = go.GetComponent<TripulacaoUnidade>();
        if (trip == null)
        {
            trip = go.AddComponent<TripulacaoUnidade>();
        }

        trip.teamID = ident.teamID;
        
        switch (ident.tipoUnidade)
        {
            case TipoUnidade.Infantaria:
                trip.tamanhoTripulacao = 1;
                break;
            case TipoUnidade.Veiculo:
                trip.tamanhoTripulacao = 3;
                break;
            case TipoUnidade.Aereo:
                trip.tamanhoTripulacao = 2;
                break;
            case TipoUnidade.Naval:
                trip.tamanhoTripulacao = 10;
                break;
            default:
                trip.tamanhoTripulacao = 1;
                break;
        }

        ConsumirTripulacaoDoGoverno(ident.teamID, ident.tipoUnidade, trip.tamanhoTripulacao);

        return trip;
    }

    private static void ConsumirTripulacaoDoGoverno(int teamId, TipoUnidade tipo, int qtd)
    {
        if (SistemaGovernoMundial.Instancia == null) return;
        var pais = SistemaGovernoMundial.Instancia.ObterPais(teamId);
        if (pais == null) return;

        switch (tipo)
        {
            case TipoUnidade.Infantaria:
            case TipoUnidade.Veiculo:
                if (pais.ativosTerrestres < qtd)
                {
                    int necessarios = qtd - pais.ativosTerrestres;
                    int mob = Mathf.Min(necessarios, pais.reservasTerrestres);
                    pais.reservasTerrestres -= mob;
                    pais.ativosTerrestres += mob;
                }
                if (pais.ativosTerrestres < qtd)
                {
                    int necessarios = qtd - pais.ativosTerrestres;
                    int recrutados = Mathf.Min(necessarios, pais.alistaveis);
                    pais.alistaveis -= recrutados;
                    pais.ativosTerrestres += recrutados;
                }
                pais.ativosTerrestres = Mathf.Max(0, pais.ativosTerrestres - qtd);
                break;

            case TipoUnidade.Aereo:
                if (pais.ativosAereos < qtd)
                {
                    int necessarios = qtd - pais.ativosAereos;
                    int mob = Mathf.Min(necessarios, pais.reservasAereos);
                    pais.reservasAereos -= mob;
                    pais.ativosAereos += mob;
                }
                if (pais.ativosAereos < qtd)
                {
                    int necessarios = qtd - pais.ativosAereos;
                    int recrutados = Mathf.Min(necessarios, pais.alistaveis);
                    pais.alistaveis -= recrutados;
                    pais.ativosAereos += recrutados;
                }
                pais.ativosAereos = Mathf.Max(0, pais.ativosAereos - qtd);
                break;

            case TipoUnidade.Naval:
                if (pais.ativosMaritimos < qtd)
                {
                    int necessarios = qtd - pais.ativosMaritimos;
                    int mob = Mathf.Min(necessarios, pais.reservasMaritimas);
                    pais.reservasMaritimas -= mob;
                    pais.ativosMaritimos += mob;
                }
                if (pais.ativosMaritimos < qtd)
                {
                    int necessarios = qtd - pais.ativosMaritimos;
                    int recrutados = Mathf.Min(necessarios, pais.alistaveis);
                    pais.alistaveis -= recrutados;
                    pais.ativosMaritimos += recrutados;
                }
                pais.ativosMaritimos = Mathf.Max(0, pais.ativosMaritimos - qtd);
                break;
        }
    }

    private void OnDestroy()
    {
        DevolverTripulacaoAoGoverno();
    }

    public void DevolverTripulacaoAoGoverno()
    {
        if (devolvida) return;
        devolvida = true;

        if (SistemaGovernoMundial.Instancia == null) return;
        var pais = SistemaGovernoMundial.Instancia.ObterPais(teamID);
        if (pais == null) return;

        int sobreviventes = Mathf.CeilToInt(tamanhoTripulacao * 0.5f);
        pais.alistaveis += sobreviventes;
    }
}
