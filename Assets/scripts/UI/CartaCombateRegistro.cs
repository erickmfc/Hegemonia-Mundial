using System;
using System.Collections.Generic;
using UnityEngine;
using Hegemonia.AI.BrainMaster;

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
        public TipoUnidade tipoUnidadeAlvo;
        public bool alvoEhEstrutura;
        public bool custoReposicaoConhecido;
        public long custoReposicaoEstimado;
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
        long custoReposicaoEstimado;
        bool custoReposicaoConhecido = TentarResolverCustoReposicao(alvoId, out custoReposicaoEstimado);
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
            tipoUnidadeAlvo = alvoId != null ? alvoId.tipoUnidade : TipoUnidade.Estrutura,
            alvoEhEstrutura = vitima.ehEstrutura,
            custoReposicaoConhecido = custoReposicaoConhecido,
            custoReposicaoEstimado = custoReposicaoEstimado,
            posicao = vitima.transform.position
        });
    }

    private static bool TentarResolverCustoReposicao(IdentidadeUnidade alvo, out long custo)
    {
        custo = 0L;
        if (alvo == null || alvo.gameObject == null)
        {
            return false;
        }

        IA_ConstructionMetadata metadata = alvo.GetComponent<IA_ConstructionMetadata>();
        if (metadata == null)
        {
            metadata = alvo.GetComponentInChildren<IA_ConstructionMetadata>(true);
        }

        if (metadata != null && metadata.EstimatedReplacementCost > 0L)
        {
            custo = metadata.EstimatedReplacementCost;
            return true;
        }

        List<DadosConstrucao> catalogo = MenuConstrucao.catalogoGlobal;
        if (catalogo == null || catalogo.Count == 0)
        {
            return false;
        }

        string itemId = metadata != null ? metadata.ItemId : string.Empty;
        string nomePrefab = metadata != null ? metadata.SourcePrefabName : string.Empty;
        if (string.IsNullOrWhiteSpace(nomePrefab))
        {
            nomePrefab = RemoverSufixoClone(alvo.gameObject.name);
        }

        bool encontrou = false;
        long custoEncontrado = 0L;
        for (int i = 0; i < catalogo.Count; i++)
        {
            DadosConstrucao ficha = catalogo[i];
            if (ficha == null)
            {
                continue;
            }

            bool correspondeId = !string.IsNullOrWhiteSpace(itemId)
                && string.Equals(ficha.GetStableId(), itemId, StringComparison.OrdinalIgnoreCase);
            GameObject prefab;
            bool correspondePrefab = !string.IsNullOrWhiteSpace(nomePrefab)
                && ficha.TryGetPrefabBasico(out prefab)
                && prefab != null
                && string.Equals(prefab.name, nomePrefab, StringComparison.Ordinal);
            if (!correspondeId && !correspondePrefab)
            {
                continue;
            }

            long custoFicha = Math.Max(0L, ficha.ObterPrecoEfetivo());
            if (custoFicha <= 0L)
            {
                continue;
            }

            if (encontrou && custoFicha != custoEncontrado)
            {
                // Nomes/IDs ambíguos não são suficientes para atribuir valor.
                custo = 0L;
                return false;
            }

            encontrou = true;
            custoEncontrado = custoFicha;
        }

        if (!encontrou)
        {
            return false;
        }

        custo = custoEncontrado;
        return true;
    }

    private static string RemoverSufixoClone(string nome)
    {
        const string cloneSuffix = "(Clone)";
        if (string.IsNullOrWhiteSpace(nome))
        {
            return string.Empty;
        }

        string valor = nome.Trim();
        if (valor.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            valor = valor.Substring(0, valor.Length - cloneSuffix.Length).TrimEnd();
        }

        return valor;
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
