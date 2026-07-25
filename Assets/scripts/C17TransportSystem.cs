using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.Aeronaves.C17
{
    public sealed class C17TransportSystem : MonoBehaviour
    {
        [SerializeField, Min(1)] private int capacidadeSoldados = 70;
        [SerializeField, Min(0)] private int capacidadeVeiculos = 3;
        [SerializeField] private Transform pontoEntradaTropas;
        [SerializeField] private Transform pontoEntradaVeiculos;
        [SerializeField] private Transform pontoSaidaTropas;
        [SerializeField] private Transform pontoSaidaVeiculos;
        [SerializeField, Min(1f)] private float raioBusca = 60f;
        [SerializeField, Min(1f)] private float distanciaEntrada = 4.5f;
        [SerializeField] private LayerMask camadaAgua;
        [SerializeField] private LayerMask camadaObstaculos;

        private readonly List<GameObject> tropas = new List<GameObject>();
        private readonly List<GameObject> veiculos = new List<GameObject>();

        public int CapacidadeSoldados => capacidadeSoldados;
        public int CapacidadeVeiculos => capacidadeVeiculos;
        public int TropasEmbarcadasCount => tropas.Count;
        public int VeiculosEmbarcadosCount => veiculos.Count;

        public void IniciarEmbarqueTropas(int teamId) => Embarcar(teamId, true);
        public void IniciarEmbarqueVeiculos(int teamId) => Embarcar(teamId, false);
        public void IniciarEmbarqueTodos(int teamId)
        {
            Embarcar(teamId, true);
            Embarcar(teamId, false);
        }

        private void Embarcar(int teamId, bool apenasTropas)
        {
            int vagas = apenasTropas ? capacidadeSoldados - tropas.Count : capacidadeVeiculos - veiculos.Count;
            if (vagas <= 0) return;
            Transform origem = apenasTropas ? pontoEntradaTropas : pontoEntradaVeiculos;
            Vector3 centro = origem != null ? origem.position : transform.position - transform.forward * distanciaEntrada;
            Collider[] encontrados = Physics.OverlapSphere(centro, raioBusca, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < encontrados.Length && vagas > 0; i++)
            {
                GameObject obj = encontrados[i].GetComponentInParent<ControleUnidade>()?.gameObject;
                if (obj == null || obj == gameObject || tropas.Contains(obj) || veiculos.Contains(obj)) continue;
                IdentidadeUnidade identidade = obj.GetComponent<IdentidadeUnidade>();
                if (identidade != null && identidade.teamID != teamId) continue;
                C17TransporteController outroC17 = obj.GetComponent<C17TransporteController>();
                if (outroC17 != null) continue;
                if (!apenasTropas && obj.GetComponent<MovimentoRealTerrestre>() == null && !obj.name.ToLowerInvariant().Contains("veiculo")) continue;
                if (apenasTropas && obj.GetComponent<MovimentoRealTerrestre>() != null) continue;
                obj.transform.SetParent(transform, true);
                obj.SetActive(false);
                if (apenasTropas) tropas.Add(obj); else veiculos.Add(obj);
                vagas--;
            }
        }

        public bool ExecutarDesembarque()
        {
            bool alterou = DesembarcarLista(tropas, pontoSaidaTropas, 0);
            alterou |= DesembarcarLista(veiculos, pontoSaidaVeiculos, 20);
            return alterou;
        }

        private bool DesembarcarLista(List<GameObject> lista, Transform ponto, int deslocamento)
        {
            bool alterou = false;
            Vector3 centro = ponto != null ? ponto.position : transform.position - transform.forward * 8f;
            for (int i = lista.Count - 1; i >= 0; i--)
            {
                GameObject obj = lista[i];
                if (obj == null) { lista.RemoveAt(i); continue; }
                Vector3 candidato = centro + transform.right * ((i + deslocamento) % 4 - 1.5f) * 4f + transform.forward * ((i + deslocamento) / 4) * 4f;
                if (Physics.Raycast(candidato + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (camadaAgua.value != 0 && ((1 << hit.collider.gameObject.layer) & camadaAgua.value) != 0) continue;
                    if (camadaObstaculos.value != 0 && Physics.CheckSphere(hit.point + Vector3.up, 1.5f, camadaObstaculos)) continue;
                    obj.transform.SetParent(null, true);
                    obj.transform.SetPositionAndRotation(hit.point + Vector3.up * 0.2f, transform.rotation);
                    obj.SetActive(true);
                    lista.RemoveAt(i);
                    alterou = true;
                }
            }
            return alterou;
        }
    }
}
