using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro somente de observabilidade da Carta Náutica. Não executa dano,
/// não cria mísseis e não substitui os controladores de combate existentes.
/// Os eventos são publicados pelos sistemas reais de lançamento e de dano.
/// </summary>
public static class CartaCombateRegistro
{
    [Serializable]
    public sealed class EventoCombate
    {
        public string id;
        public string horario;
        public string tipo;
        public string descricao;
        public string atacante;
        public string alvo;
        public string arma;
        public string resultado;
        public string missilId;
        public string idAlvo;
        public int equipeAtacante = -1;
        public int equipeAlvo = -1;
        public Vector3 posicao;
        public float momento;
    }

    private static readonly List<EventoCombate> eventos = new List<EventoCombate>(128);
    private static int sequencia;

    public static event Action<EventoCombate> EventoRegistrado;
    public static IReadOnlyList<EventoCombate> Eventos => eventos;

    public static void CopiarEventos(List<EventoCombate> destino)
    {
        if (destino == null) return;
        destino.Clear();
        destino.AddRange(eventos);
    }

    public static void RegistrarLancamento(MissileThreatTracker tracker)
    {
        if (tracker == null) return;

        string alvo = tracker.AlvoNome;
        if (string.IsNullOrWhiteSpace(alvo)) alvo = "coordenada " + FormatarPosicao(tracker.PontoAlvoConhecido);
        Adicionar(new EventoCombate
        {
            tipo = "LANÇAMENTO",
            descricao = tracker.NomeOrigem + " lançou " + ResolverNomeMissil(tracker) + " contra " + alvo,
            atacante = tracker.NomeOrigem,
            alvo = alvo,
            arma = ResolverNomeMissil(tracker),
            resultado = "LANÇADO",
            missilId = tracker.MissileId.ToString(),
            equipeAtacante = tracker.TeamOrigem,
            equipeAlvo = tracker.AlvoTeam,
            posicao = tracker.PontoLancamento
        });
    }

    public static void RegistrarMissilEncerrado(MissileThreatTracker tracker)
    {
        if (tracker == null) return;
        string alvo = tracker.AlvoNome;
        if (string.IsNullOrWhiteSpace(alvo)) alvo = "coordenada " + FormatarPosicao(tracker.PontoAlvoConhecido);
        Adicionar(new EventoCombate
        {
            tipo = "MISSIL ENCERRADO",
            descricao = ResolverNomeMissil(tracker) + " foi desativado/retirado do mundo real; resultado final não informado pelo controlador.",
            atacante = tracker.NomeOrigem,
            alvo = alvo,
            arma = ResolverNomeMissil(tracker),
            resultado = "DESATIVADO",
            missilId = tracker.MissileId.ToString(),
            equipeAtacante = tracker.TeamOrigem,
            equipeAlvo = tracker.AlvoTeam,
            posicao = tracker.RaizMissil != null ? tracker.RaizMissil.position : tracker.PontoLancamento
        });
    }

    public static void RegistrarUnidadeDestruida(SistemaDeDanos vitima, GameObject agressor)
    {
        if (vitima == null) return;

        IdentidadeUnidade alvoId = SistemaDeDanos.ResolverIdentidade(vitima);
        IdentidadeUnidade atacanteId = SistemaDeDanos.ResolverIdentidade(agressor != null ? agressor.transform : null);
        MissileThreatTracker tracker = agressor != null
            ? agressor.GetComponentInParent<MissileThreatTracker>()
            : null;
        string atacante = atacanteId != null
            ? atacanteId.name
            : tracker != null ? tracker.NomeOrigem : "DESCONHECIDO";
        string arma = tracker != null ? ResolverNomeMissil(tracker) : "ATAQUE NÃO INFORMADO";
        string alvo = alvoId != null ? alvoId.name : vitima.name;
        Adicionar(new EventoCombate
        {
            tipo = "UNIDADE DESTRUÍDA",
            descricao = alvo + " destruído; atacante: " + atacante,
            atacante = atacante,
            alvo = alvo,
            arma = arma,
            resultado = "DESTRUÍDA",
            idAlvo = alvoId != null ? ObterIdPersistente(alvoId.gameObject) : string.Empty,
            equipeAtacante = atacanteId != null ? atacanteId.teamID : tracker != null ? tracker.TeamOrigem : -1,
            equipeAlvo = alvoId != null ? alvoId.teamID : -1,
            posicao = vitima.transform.position
        });
    }

    private static void Adicionar(EventoCombate evento)
    {
        if (evento == null) return;
        evento.id = "combate-" + (++sequencia).ToString("000000");
        evento.horario = DateTime.Now.ToString("HH:mm:ss");
        evento.momento = Time.unscaledTime;
        eventos.Insert(0, evento);
        if (eventos.Count > 128) eventos.RemoveAt(eventos.Count - 1);
        EventoRegistrado?.Invoke(evento);
    }

    private static string ResolverNomeMissil(MissileThreatTracker tracker)
    {
        if (tracker == null || tracker.RaizMissil == null) return "MÍSSIL";
        return tracker.RaizMissil.name;
    }

    private static string ObterIdPersistente(GameObject objeto)
    {
        SaveableEntity saveable = objeto != null ? objeto.GetComponent<SaveableEntity>() : null;
        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.UniqueId)) return saveable.UniqueId;
        return objeto == null ? string.Empty : "runtime-" + objeto.GetInstanceID();
    }

    private static string FormatarPosicao(Vector3 posicao)
    {
        return "(" + posicao.x.ToString("0") + ", " + posicao.y.ToString("0") + ", " + posicao.z.ToString("0") + ")";
    }
}
