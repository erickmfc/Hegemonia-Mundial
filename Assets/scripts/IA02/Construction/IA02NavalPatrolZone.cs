using UnityEngine;

namespace Hegemonia.AI.IA02
{
    /// <summary>Área marítima editável para a patrulha naval da IA02.</summary>
    public sealed class IA02NavalPatrolZone : MonoBehaviour
    {
        public enum TipoSetor { CostaAguasTerritoriais, InfraestruturaEstrategica, FronteiraMarAberto }

        [SerializeField, Min(25f)] private float raio = 150f;
        [SerializeField, Min(1)] private int intervaloDias = 2;
        [SerializeField] private TipoSetor tipoSetor = TipoSetor.CostaAguasTerritoriais;
        [SerializeField, Min(0)] private int naviosDesejados = 1;

        public int IntervaloDias => Mathf.Max(1, intervaloDias);
        public TipoSetor Setor => tipoSetor;
        public int NaviosDesejados => Mathf.Max(0, naviosDesejados);
        public float Raio => Mathf.Max(25f, raio);

        private void Awake()
        {
            if (tipoSetor == TipoSetor.CostaAguasTerritoriais)
            {
                if (name.EndsWith("B")) tipoSetor = TipoSetor.InfraestruturaEstrategica;
                else if (name.EndsWith("C")) tipoSetor = TipoSetor.FronteiraMarAberto;
            }
        }

        public Vector3[] CriarRota(int indice)
        {
            float fase = ((indice % 12) * 29f + (int)tipoSetor * 17f) * Mathf.Deg2Rad;
            float variacao = 1f + ((indice % 5) - 2) * 0.09f;
            Vector3[] rota = new Vector3[5];
            for (int i = 0; i < rota.Length; i++)
            {
                float angulo = fase + i * (360f / rota.Length) * Mathf.Deg2Rad;
                float distancia = Raio * (0.58f + ((i + indice) % 3) * 0.16f) * variacao;
                Vector3 direcao = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
                rota[i] = ResolverAgua(transform.position + direcao * distancia, direcao);
            }
            return rota;
        }

        public bool TryCriarRota(int indice, out Vector3[] rota)
        {
            rota = CriarRota(indice);
            if (rota == null || rota.Length < 2) return false;
            for (int i = 0; i < rota.Length; i++)
            {
                if (!NavalPlacementResolver.IsWaterAtPosition(rota[i]))
                {
                    rota = null;
                    return false;
                }
            }
            return true;
        }

        public Vector3[] CriarRotaResposta(Vector3 ultimaPosicaoConhecida, float raioInvestigacao, int indice)
        {
            float raioResposta = Mathf.Clamp(Mathf.Max(60f, raioInvestigacao * 0.12f), 80f, Raio * 0.72f);
            float fase = (indice % 8) * 45f * Mathf.Deg2Rad;
            Vector3[] rota = new Vector3[4];
            for (int i = 0; i < rota.Length; i++)
            {
                float angulo = fase + i * Mathf.PI * 0.5f;
                Vector3 direcao = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
                rota[i] = ResolverAgua(ultimaPosicaoConhecida + direcao * raioResposta, direcao);
            }
            return rota;
        }

        private static Vector3 ResolverAgua(Vector3 candidato, Vector3 direcao)
        {
            if (NavalPlacementResolver.IsWaterAtPosition(candidato))
            {
                candidato.y = NavalPlacementResolver.ResolveSeaLevel();
                return candidato;
            }

            if (NavalPlacementResolver.TryResolveWaterSpawn(candidato, direcao, 0f, 220f, out Vector3 agua, out _, out _))
            {
                return agua;
            }

            candidato.y = NavalPlacementResolver.ResolveSeaLevel();
            return candidato;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, Raio);
        }
    }
}
