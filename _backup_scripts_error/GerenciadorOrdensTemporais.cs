using UnityEngine;
using System;
using System.Collections.Generic;

public enum OrdemTemporalTipo
{
    TreinamentoMilitares,
    TreinamentoNaval,
    TreinamentoAereo,
    AtivacaoReserva
}

[System.Serializable]
public class OrdemTemporal
{
    public int teamId;
    public OrdemTemporalTipo tipo;
    public int quantidade;
    public float tempoRestante;
    public float tempoTotal;
}

public class GerenciadorOrdensTemporais : MonoBehaviour
{
    public static GerenciadorOrdensTemporais Instancia { get; private set; }

    public List<OrdemTemporal> ordensAtivas = new List<OrdemTemporal>();

    private void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (GerenciadorTempo.Instancia != null)
            GerenciadorTempo.Instancia.OnDataAlterada += ProcessarDiaCalendario;
    }

    private void OnDisable()
    {
        if (GerenciadorTempo.Instancia != null)
            GerenciadorTempo.Instancia.OnDataAlterada -= ProcessarDiaCalendario;
    }

    private void ProcessarDiaCalendario()
    {
        // Cada chamada = 1 dia do jogo decorrido (via GerenciadorTempo)
        for (int i = ordensAtivas.Count - 1; i >= 0; i--)
        {
            var ordem = ordensAtivas[i];
            ordem.tempoRestante -= 1f;
            if (ordem.tempoRestante <= 0f)
            {
                FinalizarOrdem(ordem);
                ordensAtivas.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Adiciona uma ordem temporal à fila de processamento.
    /// </summary>
    /// <param name="duracaoDias">Duração em DIAS do jogo (não segundos de máquina).</param>
    public void AdicionarOrdem(int teamId, OrdemTemporalTipo tipo, int quantidade, float duracaoDias)
    {
        ordensAtivas.Add(new OrdemTemporal
        {
            teamId = teamId,
            tipo = tipo,
            quantidade = quantidade,
            tempoRestante = duracaoDias,
            tempoTotal = duracaoDias
        });
    }

    public int ObterOrdensEmAndamento(int teamId, OrdemTemporalTipo tipo)
    {
        int total = 0;
        foreach (var o in ordensAtivas)
        {
            if (o.teamId == teamId && o.tipo == tipo)
            {
                total += o.quantidade;
            }
        }
        return total;
    }

    private void FinalizarOrdem(OrdemTemporal ordem)
    {
        DadosPaisGoverno pais = ConectorGoverno.ObterPais(ordem.teamId);
        if (pais == null && SistemaGovernoMundial.Instancia != null)
        {
            pais = SistemaGovernoMundial.Instancia.ObterPais(ordem.teamId);
        }

        if (pais != null)
        {
            if (ordem.tipo == OrdemTemporalTipo.TreinamentoMilitares)
            {
                pais.reservasTerrestres += ordem.quantidade;
                if (SistemaGovernoMundial.Instancia != null)
                    SistemaGovernoMundial.Instancia.RegistrarNoticia($"{pais.nomePais}: Treinamento terrestre concluído (+{ordem.quantidade} reservas).");
            }
            else if (ordem.tipo == OrdemTemporalTipo.TreinamentoNaval)
            {
                pais.reservasMaritimas += ordem.quantidade;
                if (SistemaGovernoMundial.Instancia != null)
                    SistemaGovernoMundial.Instancia.RegistrarNoticia($"{pais.nomePais}: Treinamento naval concluído (+{ordem.quantidade} reservas).");
            }
            else if (ordem.tipo == OrdemTemporalTipo.TreinamentoAereo)
            {
                pais.reservasAereos += ordem.quantidade;
                if (SistemaGovernoMundial.Instancia != null)
                    SistemaGovernoMundial.Instancia.RegistrarNoticia($"{pais.nomePais}: Treinamento aéreo concluído (+{ordem.quantidade} reservas).");
            }
            else if (ordem.tipo == OrdemTemporalTipo.AtivacaoReserva)
            {
                // A ativação da reserva será resolvida por quem solicitou, consumindo a quantidade das reservas.
                // Aqui apenas logamos.
                if (SistemaGovernoMundial.Instancia != null)
                    SistemaGovernoMundial.Instancia.RegistrarNoticia($"{pais.nomePais}: Esquadrão mobilizado e pronto para combate.");
            }
            
            SistemaGovernoMundial.Instancia?.NotificarGovernoAtualizado();
        }
    }

    public bool ExisteCentroTreinamento(int teamId)
    {
        // Usa cache global do SistemaGovernoMundial para evitar FindObjectsByType repetitivo
        if (SistemaGovernoMundial.Instancia != null)
            return SistemaGovernoMundial.TemCentroTreinamentoCache(teamId);

        // Fallback: só varre se o cache não estiver disponível
        GerenciadorQuartel[] quarteis = FindObjectsByType<GerenciadorQuartel>(FindObjectsSortMode.None);
        foreach (var q in quarteis)
        {
            if (q == null) continue;
            var id = q.GetComponent<IdentidadeUnidade>();
            if (id == null) id = q.GetComponentInParent<IdentidadeUnidade>();
            if (id != null && id.teamID == teamId && q.gameObject.activeInHierarchy)
            {
                var dmg = q.GetComponent<SistemaDeDanos>();
                if (dmg == null || dmg.vidaAtual > 0)
                    return true;
            }
        }

        Fabrica[] fabricas = FindObjectsByType<Fabrica>(FindObjectsSortMode.None);
        foreach (var f in fabricas)
        {
            if (f == null) continue;
            var id = f.GetComponent<IdentidadeUnidade>();
            if (id == null) id = f.GetComponentInParent<IdentidadeUnidade>();
            if (id != null && id.teamID == teamId && f.gameObject.activeInHierarchy)
            {
                var dmg = f.GetComponent<SistemaDeDanos>();
                if (dmg == null || dmg.vidaAtual > 0)
                    return true;
            }
        }

        return false;
    }
}
