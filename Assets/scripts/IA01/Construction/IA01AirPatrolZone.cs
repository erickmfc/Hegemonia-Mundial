using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>
    /// Create editável que define a área de reconhecimento aéreo da IA01.
    /// A ordem é entregue pelo mesmo ControleUnidade usado pelo jogador.
    /// </summary>
    public sealed class IA01AirPatrolZone : MonoBehaviour
    {
        [SerializeField, Min(80f)] private float raio = 260f;
        [SerializeField, Min(40f)] private float altitude = 120f;
        [SerializeField, Min(1)] private int intervaloDias = 1;

        public int IntervaloDias => Mathf.Max(1, intervaloDias);

        public Vector3[] CriarRota(int indice)
        {
            float fase = (indice % 3) * 120f;
            Vector3 a = Quaternion.Euler(0f, fase, 0f) * Vector3.forward;
            Vector3 b = Quaternion.Euler(0f, fase + 120f, 0f) * Vector3.forward;
            Vector3 c = Quaternion.Euler(0f, fase + 240f, 0f) * Vector3.forward;
            return new[]
            {
                Ajustar(transform.position + a * (raio * 0.72f)),
                Ajustar(transform.position + b * raio),
                Ajustar(transform.position + c * (raio * 0.72f))
            };
        }

        private Vector3 Ajustar(Vector3 ponto)
        {
            ponto.y = Mathf.Max(60f, altitude);
            return ponto;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, raio);
            Gizmos.DrawLine(transform.position + Vector3.up * altitude, transform.position + Vector3.up * (altitude + 30f));
        }
    }
}
