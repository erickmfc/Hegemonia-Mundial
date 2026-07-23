using UnityEngine;

namespace Hegemonia.Aeronaves.C17
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class C17MotionController : MonoBehaviour
    {
        private Rigidbody corpo;
        private Vector3 velocidadeSolicitada;
        private Vector3 velocidadeAplicada;
        private Quaternion rotacaoSolicitada;
        private bool possuiComando;

        public Vector3 VelocidadeAtual { get; private set; }
        public float VelocidadeEscalar => VelocidadeAtual.magnitude;
        public Vector3 PosicaoAtual => corpo != null ? corpo.position : transform.position;
        public Quaternion RotacaoAtual => corpo != null ? corpo.rotation : transform.rotation;

        private void Awake()
        {
            corpo = GetComponent<Rigidbody>();
            corpo.isKinematic = true;
            corpo.useGravity = false;
            corpo.interpolation = RigidbodyInterpolation.Interpolate;
            corpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rotacaoSolicitada = corpo.rotation;
        }

        private void FixedUpdate()
        {
            if (!possuiComando)
            {
                velocidadeAplicada = Vector3.MoveTowards(velocidadeAplicada, Vector3.zero, 80f * Mathf.Max(Time.fixedDeltaTime, 0.0001f));
                VelocidadeAtual = velocidadeAplicada;
                return;
            }

            corpo.MoveRotation(rotacaoSolicitada);
            float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            float taxa = velocidadeSolicitada.sqrMagnitude < velocidadeAplicada.sqrMagnitude ? 70f : 38f;
            velocidadeAplicada = Vector3.MoveTowards(velocidadeAplicada, velocidadeSolicitada, taxa * dt);
            corpo.MovePosition(corpo.position + velocidadeAplicada * dt);
            VelocidadeAtual = velocidadeAplicada;
        }

        public void DefinirMovimento(Vector3 velocidade, Quaternion rotacao)
        {
            velocidadeSolicitada = velocidade;
            rotacaoSolicitada = rotacao;
            possuiComando = true;
        }

        public void Parar()
        {
            velocidadeSolicitada = Vector3.zero;
            velocidadeAplicada = Vector3.zero;
            rotacaoSolicitada = corpo != null ? corpo.rotation : transform.rotation;
            possuiComando = true;
        }

        public void CancelarMovimento()
        {
            velocidadeSolicitada = Vector3.zero;
            velocidadeAplicada = Vector3.zero;
            VelocidadeAtual = Vector3.zero;
            possuiComando = false;
        }
    }
}
