using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.AI.IA03
{
    /// <summary>
    /// Índice em memória preenchido pelos próprios Creatys no seu ciclo de vida.
    /// Não procura objetos na cena durante a simulação.
    /// </summary>
    public static class RegistroCreatysEstrategicos
    {
        private static readonly List<CreatyEstrategico> registrados = new List<CreatyEstrategico>(128);
        private static readonly Dictionary<string, CreatyEstrategico> porId = new Dictionary<string, CreatyEstrategico>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> idsDuplicados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, Dictionary<IA03TipoCreaty, List<CreatyEstrategico>>> porPaisETipo
            = new Dictionary<int, Dictionary<IA03TipoCreaty, List<CreatyEstrategico>>>();

        public static int QuantidadeRegistrada => registrados.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReiniciarEstadoRuntime()
        {
            registrados.Clear();
            porId.Clear();
            idsDuplicados.Clear();
            porPaisETipo.Clear();
        }

        public static bool Registrar(CreatyEstrategico creaty)
        {
            if (creaty == null || registrados.Contains(creaty))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(creaty.Id))
            {
                Debug.LogWarning("Creaty estratégico sem ID: " + creaty.name, creaty);
                return false;
            }

            registrados.Add(creaty);
            AdicionarAoIndice(creaty);
            int duplicados = ReindexarId(creaty.Id);
            if (duplicados > 1)
            {
                Debug.LogError("ID de Creaty estratégico duplicado: " + creaty.Id, creaty);
            }

            return true;
        }

        public static void Remover(CreatyEstrategico creaty)
        {
            if (creaty == null)
            {
                return;
            }

            if (!registrados.Remove(creaty))
            {
                return;
            }

            RemoverDoIndice(creaty);
            if (!string.IsNullOrWhiteSpace(creaty.Id))
            {
                ReindexarId(creaty.Id);
            }
        }

        public static bool TryGetPorId(string id, out CreatyEstrategico creaty)
        {
            if (string.IsNullOrWhiteSpace(id) || idsDuplicados.Contains(id))
            {
                creaty = null;
                return false;
            }

            return porId.TryGetValue(id, out creaty) && creaty != null;
        }

        private static int ReindexarId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return 0;
            }

            CreatyEstrategico primeiro = null;
            int quantidade = 0;
            for (int i = 0; i < registrados.Count; i++)
            {
                CreatyEstrategico candidato = registrados[i];
                if (candidato == null || !string.Equals(candidato.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                primeiro = primeiro == null ? candidato : primeiro;
                quantidade++;
            }

            if (quantidade == 0)
            {
                porId.Remove(id);
                idsDuplicados.Remove(id);
            }
            else
            {
                porId[id] = primeiro;
                if (quantidade > 1)
                {
                    idsDuplicados.Add(id);
                }
                else
                {
                    idsDuplicados.Remove(id);
                }
            }

            return quantidade;
        }

        private static void AdicionarAoIndice(CreatyEstrategico creaty)
        {
            if (!porPaisETipo.TryGetValue(creaty.PaisProprietarioTeamId, out Dictionary<IA03TipoCreaty, List<CreatyEstrategico>> porTipo))
            {
                porTipo = new Dictionary<IA03TipoCreaty, List<CreatyEstrategico>>();
                porPaisETipo.Add(creaty.PaisProprietarioTeamId, porTipo);
            }

            if (!porTipo.TryGetValue(creaty.Tipo, out List<CreatyEstrategico> lista))
            {
                lista = new List<CreatyEstrategico>(8);
                porTipo.Add(creaty.Tipo, lista);
            }

            lista.Add(creaty);
        }

        private static void RemoverDoIndice(CreatyEstrategico creaty)
        {
            if (!porPaisETipo.TryGetValue(creaty.PaisProprietarioTeamId, out Dictionary<IA03TipoCreaty, List<CreatyEstrategico>> porTipo)
                || !porTipo.TryGetValue(creaty.Tipo, out List<CreatyEstrategico> lista))
            {
                return;
            }

            lista.Remove(creaty);
            if (lista.Count == 0)
            {
                porTipo.Remove(creaty.Tipo);
            }
            if (porTipo.Count == 0)
            {
                porPaisETipo.Remove(creaty.PaisProprietarioTeamId);
            }
        }

        public static void PreencherCandidatos(
            List<CreatyEstrategico> destino,
            int paisProprietarioTeamId,
            int paisAlvoTeamId,
            IA03TipoCreaty tipo,
            IA03NivelConflito nivel,
            IA03DominioEstrategico dominio)
        {
            if (destino == null)
            {
                return;
            }

            destino.Clear();
            if (!porPaisETipo.TryGetValue(paisProprietarioTeamId, out Dictionary<IA03TipoCreaty, List<CreatyEstrategico>> porTipo)
                || !porTipo.TryGetValue(tipo, out List<CreatyEstrategico> candidatos))
            {
                return;
            }

            for (int i = 0; i < candidatos.Count; i++)
            {
                CreatyEstrategico creaty = candidatos[i];
                if (creaty == null
                    || !creaty.Ativo
                    || idsDuplicados.Contains(creaty.Id)
                    || !creaty.PermiteAlvo(paisAlvoTeamId)
                    || creaty.NivelDeConflito != nivel
                    || (dominio != IA03DominioEstrategico.Combinado && creaty.Dominio != dominio && creaty.Dominio != IA03DominioEstrategico.Combinado))
                {
                    continue;
                }

                destino.Add(creaty);
            }
        }
    }
}
