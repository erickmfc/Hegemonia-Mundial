using UnityEngine;

namespace Hegemonia.AI.IA02
{
    /// <summary>Área marítima editável para a patrulha naval da IA02.</summary>
    public sealed class IA02NavalPatrolZone : MonoBehaviour
    {
        [SerializeField, Min(25f)] private float raio = 150f;
        [SerializeField, Min(1)] private int intervaloDias = 2;

        public int IntervaloDias => Mathf.Max(1, intervaloDias);

        public Vector3[] CriarRota(int indice)
        {
            float fase = (indice % 3) * 120f;
            Vector3 a = Quaternion.Euler(0f, fase, 0f) * Vector3.forward;
            Vector3 b = Quaternion.Euler(0f, fase + 120f, 0f) * Vector3.forward;
            Vector3 c = Quaternion.Euler(0f, fase + 240f, 0f) * Vector3.forward;
            return new[]
            {
                ResolverAgua(transform.position + a * (raio * 0.72f), a),
                ResolverAgua(transform.position + b * raio, b),
                ResolverAgua(transform.position + c * (raio * 0.72f), c)
            };
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
            Gizmos.DrawWireSphere(transform.position, raio);
        }
    }
}
