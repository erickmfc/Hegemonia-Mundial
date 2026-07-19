using UnityEngine;

namespace Hegemonia.AI.IA01
{
    /// <summary>Área marítima editável para a patrulha naval da IA01.</summary>
    public sealed class IA01NavalPatrolZone : MonoBehaviour
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
                transform.position + a * (raio * 0.72f),
                transform.position + b * raio,
                transform.position + c * (raio * 0.72f)
            };
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, raio);
        }
    }
}
