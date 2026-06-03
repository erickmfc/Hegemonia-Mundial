using UnityEngine;

namespace Hegemonia.UI
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instancia { get; private set; }

        private float _shakeDuration = 0f;
        private float _shakeAmount = 0.5f;
        private Vector3 _appliedOffset = Vector3.zero;

        private void Awake()
        {
            if (Instancia == null)
            {
                Instancia = this;
            }
            else if (Instancia != this)
            {
                Destroy(this);
            }
        }

        /// <summary>
        /// Dispara o tremor da câmera.
        /// </summary>
        /// <param name="duracao">Tempo em segundos que a tela irá tremer.</param>
        /// <param name="forca">Amplitude do tremor.</param>
        public void Sacudir(float duracao, float forca = 0.5f)
        {
            _shakeDuration = duracao;
            _shakeAmount = forca;
        }

        private void LateUpdate()
        {
            // 1. Limpa o offset do frame anterior antes que a câmera acumule desvios.
            // Isso funciona mesmo que o CameraController tenha calculado a posição base baseando-se no frame anterior.
            if (_appliedOffset != Vector3.zero)
            {
                transform.position -= _appliedOffset;
                _appliedOffset = Vector3.zero;
            }

            // 2. Se a sacudida estiver ativa, calcula um novo offset randômico e aplica
            if (_shakeDuration > 0f)
            {
                _appliedOffset = Random.insideUnitSphere * _shakeAmount;
                // Mantém o desvio no plano horizontal e vertical (X, Y, Z)
                transform.position += _appliedOffset;

                _shakeDuration -= Time.deltaTime;
            }
        }
    }
}
