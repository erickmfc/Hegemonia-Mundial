using UnityEngine;
using TMPro;

namespace Hegemonia.UI
{
    public class SinalizadorTatico : MonoBehaviour
    {
        public string textoSinalizador = "★ PRIORITY TARGET ★";
        public Color corSinalizador = new Color(1f, 0.15f, 0.15f); // Vermelho neon militar
        public Vector3 offset = new Vector3(0f, 25f, 0f); // Altura acima do navio

        private GameObject _sinalizadorGO;
        private TextMeshPro _textMesh;
        private Camera _cameraCache;

        void Start()
        {
            _cameraCache = Camera.main;
            CriarSinalizador3D();
        }

        void CriarSinalizador3D()
        {
            // Criamos um GameObject filho para conter o sinalizador
            _sinalizadorGO = new GameObject("TacticalSignaler3D");
            _sinalizadorGO.transform.SetParent(transform, false);
            _sinalizadorGO.transform.localPosition = offset;

            // TextMeshPro 3D funciona nativamente no espaço 3D, sendo extremamente otimizado
            _textMesh = _sinalizadorGO.AddComponent<TextMeshPro>();
            _textMesh.text = textoSinalizador.ToUpper();
            _textMesh.fontSize = 24f;
            _textMesh.color = corSinalizador;
            _textMesh.alignment = TextAlignmentOptions.Center;
            _textMesh.fontStyle = FontStyles.Bold;
            
            // Opcional: Efeito extra de pixel para dar o visual de holograma / radar
            _textMesh.outlineWidth = 0.15f;
            _textMesh.outlineColor = new Color(0f, 0f, 0f, 1f);
        }

        void Update()
        {
            if (_sinalizadorGO == null) return;

            if (_cameraCache == null)
            {
                _cameraCache = Camera.main;
                if (_cameraCache == null) return;
            }

            // Garante que o texto sempre aponte para a câmera (Billboard)
            _sinalizadorGO.transform.LookAt(_sinalizadorGO.transform.position + _cameraCache.transform.rotation * Vector3.forward,
                                           _cameraCache.transform.rotation * Vector3.up);

            // Efeito de pulsação na escala (efeito de radar)
            float pulsoEscala = 1.0f + Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.15f;
            _sinalizadorGO.transform.localScale = new Vector3(pulsoEscala, pulsoEscala, pulsoEscala);

            // Efeito de oscilação do brilho (Alpha)
            float alpha = 0.6f + Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.4f;
            _textMesh.color = new Color(corSinalizador.r, corSinalizador.g, corSinalizador.b, alpha);
        }

        private void OnDestroy()
        {
            if (_sinalizadorGO != null)
            {
                Destroy(_sinalizadorGO);
            }
        }
    }
}
