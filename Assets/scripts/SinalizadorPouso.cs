using UnityEngine;

namespace Hegemonia.Aeronaves.C17
{
    /// <summary>
    /// Bastão sinalizador visual para delimitar e sinalizar a linha de pouso no solo.
    /// </summary>
    public class SinalizadorPouso : MonoBehaviour
    {
        [SerializeField] private Light luzSinalizadora;
        [SerializeField] private MeshRenderer rendererBastao;
        [SerializeField] private Color corValida = new Color(0f, 0.9f, 1f, 1f); // Ciano neon
        [SerializeField] private Color corInvalida = new Color(1f, 0.2f, 0.2f, 1f); // Vermelho

        private bool ehValida = true;

        private void Awake()
        {
            if (luzSinalizadora == null)
            {
                luzSinalizadora = GetComponentInChildren<Light>();
            }

            if (rendererBastao == null)
            {
                rendererBastao = GetComponent<MeshRenderer>();
            }
        }

        public void DefinirEstadoVisual(bool valida)
        {
            ehValida = valida;
            Color cor = valida ? corValida : corInvalida;

            if (luzSinalizadora != null)
            {
                luzSinalizadora.color = cor;
            }

            if (rendererBastao != null && rendererBastao.material != null)
            {
                rendererBastao.material.color = cor;
            }
        }

        public void Autodestruir(float atrasoSegundos)
        {
            Destroy(gameObject, atrasoSegundos);
        }
    }
}
