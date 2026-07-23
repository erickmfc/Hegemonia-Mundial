using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hegemonia.Aeronaves.C17
{
    /// <summary>
    /// Controlador exclusivo do C-17.
    /// A aeronave nunca sobe na vaga: no solo ela apenas percorre os waypoints da
    /// pista. A subida so comeca depois do ultimo waypoint de decolagem.
    /// </summary>
    [RequireComponent(typeof(C17FlightController))]
    [RequireComponent(typeof(C17LandingController))]
    [RequireComponent(typeof(C17TransportSystem))]
    public sealed class C17TransporteController : MonoBehaviour
    {
        [Header("Rota e velocidades")]
        [SerializeField, Min(1f)] private float velocidadeTaxi = 10f;
        [SerializeField, Min(5f)] private float velocidadePista = 62f;
        [SerializeField, Min(5f)] private float velocidadeCruzeiro = 105f;
        [SerializeField, Min(20f)] private float altitudeCruzeiro = 300f;
        [SerializeField, Min(1f)] private float aceleracaoPista = 22f;
        [SerializeField, Min(1f)] private float taxaCurvaSolo = 140f;
        [SerializeField, Min(1f)] private float taxaCurvaVoo = 55f;
        [SerializeField, Min(1f)] private float taxaSubida = 28f;
        [SerializeField, Min(1f)] private float taxaDescida = 20f;
        [SerializeField, Min(1f)] private float toleranciaWaypoint = 7f;
        [SerializeField, Min(10f)] private float distanciaExtraPista = 100f;

        [Header("Dependencias")]
        [SerializeField] private GerenciadorAeroporto aeroportoBase;

        private readonly List<Transform> rotaDecolagem = new List<Transform>();
        private C17FlightController voo;
        private C17LandingController pouso;
        private C17TransportSystem transporte;
        private ControleAviao controleAviao;
        private ControleUnidade controleUnidade;
        private Rigidbody corpo;
        private Coroutine rotina;
        private EstadoAviaoTransporte estadoAtual = EstadoAviaoTransporte.Estacionado;
        private Vector3 destino;
        private float velocidadeAtual;
        private float alturaDoSolo;
        private int operacao;

        public EstadoAviaoTransporte EstadoAtual => estadoAtual;
        public Vector3 PontoDestinoNavegacao => destino;
        public bool PossuiDestinoValido { get; private set; }

        private void Awake()
        {
            voo = GetComponent<C17FlightController>();
            pouso = GetComponent<C17LandingController>();
            transporte = GetComponent<C17TransportSystem>();
            controleAviao = GetComponent<ControleAviao>();
            controleUnidade = GetComponent<ControleUnidade>();
            corpo = GetComponent<Rigidbody>();

            // Esta versao e a unica dona da transformacao do C-17.
            C17MotionController movimentoAntigo = GetComponent<C17MotionController>();
            if (movimentoAntigo != null) movimentoAntigo.enabled = false;
            if (corpo != null)
            {
                corpo.isKinematic = true;
                corpo.useGravity = false;
                corpo.linearVelocity = Vector3.zero;
                corpo.angularVelocity = Vector3.zero;
            }
        }

        private void Start()
        {
            if (aeroportoBase == null && controleAviao != null) aeroportoBase = controleAviao.aeroportoOrigem;
            if (aeroportoBase == null) aeroportoBase = BuscarAeroportoAliadoMaisProximo();
            alturaDoSolo = CalcularAlturaDoSolo();
            DefinirEstado(EstadoAviaoTransporte.Estacionado, "aguardando ordem");
        }

        public void DirecionarParaPonto(Vector3 ponto)
        {
            if (!PontoValido(ponto)) return;
            destino = ponto;
            PossuiDestinoValido = true;
            IniciarRotina(NoSolo() ? RotinaDecolagem(ponto) : RotinaVooAte(ponto));
        }

        public bool IniciarModoMarcaacaoPouso(Vector3 inicio)
        {
            if (!PontoValido(inicio) || pouso == null) return false;
            Vector3 direcao = Plano(inicio - transform.position);
            if (direcao.sqrMagnitude < 0.01f) direcao = Plano(transform.forward);
            if (!pouso.TentarCriarAreaPouso(inicio, direcao.normalized, out AreaPousoSinalizada area, out string motivo))
            {
                Debug.LogWarning($"[C17] Pouso recusado: {motivo}");
                return false;
            }

            pouso.DefinirAreaPousoConfirmada(area);
            destino = area.PontoToqueSolo;
            PossuiDestinoValido = true;
            IniciarRotina(NoSolo() ? RotinaDecolagem(area.PontoEntradaAproximacao, area) : RotinaPouso(area));
            return true;
        }

        public void IniciarModoMarcaacaoPouso()
        {
            if (Camera.main == null) return;
            Ray raio = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(raio, out RaycastHit hit, 100000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                IniciarModoMarcaacaoPouso(hit.point);
        }

        public void ComandoZ_Direcionar() => PrepararClique();
        public void ComandoZ_CarregarTransporte() => PrepararClique();

        public void ComandoZ_VoltarAeroporto()
        {
            GerenciadorAeroporto baseDestino = BuscarAeroportoAliadoMaisProximo();
            if (baseDestino == null) return;
            aeroportoBase = baseDestino;
            if (baseDestino.waypointsDecida != null && baseDestino.waypointsDecida.Count >= 2)
            {
                Transform entrada = baseDestino.waypointsDecida[0];
                Transform proximo = baseDestino.waypointsDecida[1];
                if (entrada != null && proximo != null && pouso.TentarCriarAreaPouso(entrada.position, Plano(proximo.position - entrada.position).normalized, out AreaPousoSinalizada area, out _))
                {
                    pouso.DefinirAreaPousoConfirmada(area);
                    IniciarRotina(NoSolo() ? RotinaDecolagem(area.PontoEntradaAproximacao, area) : RotinaPouso(area));
                    return;
                }
            }
            DirecionarParaPonto(baseDestino.transform.position);
        }

        public void CancelarOrdemExterna()
        {
            operacao++;
            if (rotina != null) StopCoroutine(rotina);
            rotina = null;
            velocidadeAtual = 0f;
            PossuiDestinoValido = false;
            DefinirEstado(EstadoAviaoTransporte.Estacionado, "ordem cancelada");
        }

        public void MudarEstado(EstadoAviaoTransporte novoEstado)
        {
            if (novoEstado == EstadoAviaoTransporte.Estacionado || novoEstado == EstadoAviaoTransporte.Pousado)
                CancelarOrdemExterna();
        }

        public bool EstaSelecionado() => controleUnidade != null && controleUnidade.selecionado;

        private IEnumerator RotinaDecolagem(Vector3 destinoFinal, AreaPousoSinalizada pousoAoChegar = null)
        {
            if (!CarregarRotaDecolagem())
            {
                DefinirEstado(EstadoAviaoTransporte.SemRota, "aeroporto sem waypoints de decolagem");
                yield break;
            }

            operacao++;
            int minhaOperacao = operacao;
            if (transform.parent != null) transform.SetParent(null, true);
            velocidadeAtual = 0f;
            DefinirEstado(EstadoAviaoTransporte.TaxiandoParaPista, "seguindo waypoints da pista");

            for (int i = 0; i < rotaDecolagem.Count; i++)
            {
                Transform waypoint = rotaDecolagem[i];
                if (waypoint == null) continue;
                yield return MoverNoSolo(waypoint.position, velocidadeTaxi, minhaOperacao);
                if (minhaOperacao != operacao) yield break;
            }

            Vector3 inicio = rotaDecolagem[rotaDecolagem.Count - 1].position;
            Vector3 anterior = rotaDecolagem[Mathf.Max(0, rotaDecolagem.Count - 2)].position;
            Vector3 eixoPista = Plano(inicio - anterior);
            if (eixoPista.sqrMagnitude < 0.01f) eixoPista = Plano(transform.forward);
            eixoPista.Normalize();

            DefinirEstado(EstadoAviaoTransporte.CorridaDecolagem, "ultimo waypoint alcancado");
            yield return CorridaDeDecolagem(eixoPista, minhaOperacao);
            if (minhaOperacao != operacao) yield break;

            DefinirEstado(EstadoAviaoTransporte.Subindo, "pista liberada");
            Vector3 pontoSubida = transform.position + eixoPista * 500f;
            pontoSubida.y = Mathf.Max(altitudeCruzeiro, alturaDoSolo + 80f);
            yield return MoverNoAr(pontoSubida, velocidadeCruzeiro, minhaOperacao, true);
            if (minhaOperacao != operacao) yield break;

            if (pousoAoChegar != null) yield return RotinaPouso(pousoAoChegar);
            else yield return RotinaVooAte(destinoFinal);
        }

        private IEnumerator CorridaDeDecolagem(Vector3 eixoPista, int minhaOperacao)
        {
            float percorrido = 0f;
            Quaternion alinhado = Quaternion.LookRotation(eixoPista, Vector3.up);
            while (percorrido < distanciaExtraPista)
            {
                if (minhaOperacao != operacao) yield break;
                float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, alinhado, taxaCurvaSolo * dt);
                velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadePista, aceleracaoPista * dt);
                float passo = velocidadeAtual * dt;
                Vector3 proxima = transform.position + eixoPista * passo;
                proxima.y = AlturaSoloEm(proxima) + alturaDoSolo;
                transform.position = proxima;
                percorrido += passo;
                voo.AtualizarVisual(0f, 0f, dt);
                yield return null;
            }
        }

        private IEnumerator RotinaVooAte(Vector3 ponto)
        {
            operacao++;
            int minhaOperacao = operacao;
            DefinirEstado(EstadoAviaoTransporte.Navegando, "destino recebido");
            Vector3 alvo = ponto;
            alvo.y = Mathf.Max(altitudeCruzeiro, AlturaSoloEm(alvo) + 80f);
            yield return MoverNoAr(alvo, velocidadeCruzeiro, minhaOperacao, true);
            if (minhaOperacao != operacao) yield break;
            DefinirEstado(EstadoAviaoTransporte.OrbitandoDestino, "destino alcancado");
            while (minhaOperacao == operacao)
            {
                Vector3 orbita = alvo + new Vector3(Mathf.Sin(Time.time * 0.18f), 0f, Mathf.Cos(Time.time * 0.18f)) * 230f;
                yield return MoverNoAr(orbita, velocidadeCruzeiro * 0.7f, minhaOperacao, false, 25f);
            }
        }

        private IEnumerator RotinaPouso(AreaPousoSinalizada area)
        {
            if (area == null || !area.EhValida)
            {
                DefinirEstado(EstadoAviaoTransporte.Arremetendo, "area de pouso invalida");
                yield break;
            }

            operacao++;
            int minhaOperacao = operacao;
            DefinirEstado(EstadoAviaoTransporte.PreparandoAproximacao, "aproximando da pista");
            Vector3 entrada = area.PontoEntradaAproximacao;
            entrada.y = Mathf.Max(alturaDoSolo + 90f, area.PontoToqueSolo.y + 90f);
            yield return MoverNoAr(entrada, velocidadeCruzeiro * 0.72f, minhaOperacao, false);
            if (minhaOperacao != operacao) yield break;

            DefinirEstado(EstadoAviaoTransporte.Descendo, "alinhado com a pista");
            Vector3 toque = area.PontoToqueSolo + area.DirecaoPista * 8f;
            toque.y = AlturaSoloEm(toque) + alturaDoSolo;
            yield return MoverNoAr(toque, velocidadePista * 0.7f, minhaOperacao, false, toleranciaWaypoint);
            if (minhaOperacao != operacao) yield break;

            DefinirEstado(EstadoAviaoTransporte.Freando, "toque na pista");
            yield return MoverNoSolo(area.PontoParadaSolo, velocidadeTaxi, minhaOperacao);
            if (minhaOperacao != operacao) yield break;
            velocidadeAtual = 0f;
            PossuiDestinoValido = false;
            DefinirEstado(EstadoAviaoTransporte.Pousado, "parado no fim da pista");
        }

        private IEnumerator MoverNoSolo(Vector3 ponto, float velocidadeAlvo, int minhaOperacao)
        {
            while (DistanciaPlano(transform.position, ponto) > toleranciaWaypoint)
            {
                if (minhaOperacao != operacao) yield break;
                float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
                Vector3 direcao = Plano(ponto - transform.position);
                if (direcao.sqrMagnitude < 0.001f) break;
                Quaternion alvo = Quaternion.LookRotation(direcao.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, taxaCurvaSolo * dt);
                velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeAlvo, aceleracaoPista * dt);
                Vector3 proxima = transform.position + transform.forward * velocidadeAtual * dt;
                proxima.y = AlturaSoloEm(proxima) + alturaDoSolo;
                transform.position = proxima;
                voo.AtualizarVisual(Vector3.SignedAngle(transform.forward, direcao, Vector3.up), 0f, dt);
                yield return null;
            }
        }

        private IEnumerator MoverNoAr(Vector3 ponto, float velocidadeAlvo, int minhaOperacao, bool subindo, float tolerancia = 18f)
        {
            while (Vector3.Distance(transform.position, ponto) > tolerancia)
            {
                if (minhaOperacao != operacao) yield break;
                float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
                Vector3 horizontal = Plano(ponto - transform.position);
                if (horizontal.sqrMagnitude < 0.001f) horizontal = Plano(transform.forward);
                Quaternion alvo = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, taxaCurvaVoo * dt);
                velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeAlvo, aceleracaoPista * dt);
                float vertical = Mathf.Clamp(ponto.y - transform.position.y, -taxaDescida, subindo ? taxaSubida : taxaDescida);
                Vector3 proxima = transform.position + transform.forward * velocidadeAtual * dt + Vector3.up * vertical * dt;
                transform.position = proxima;
                voo.AtualizarVisual(Vector3.SignedAngle(transform.forward, horizontal, Vector3.up), vertical, dt);
                yield return null;
            }
        }

        private bool CarregarRotaDecolagem()
        {
            rotaDecolagem.Clear();
            if (aeroportoBase == null && controleAviao != null) aeroportoBase = controleAviao.aeroportoOrigem;
            if (aeroportoBase == null) aeroportoBase = BuscarAeroportoAliadoMaisProximo();
            if (aeroportoBase == null || aeroportoBase.waypointsDecolagem == null) return false;
            for (int i = 0; i < aeroportoBase.waypointsDecolagem.Count; i++)
            {
                Transform ponto = aeroportoBase.waypointsDecolagem[i];
                if (ponto != null) rotaDecolagem.Add(ponto);
            }
            return rotaDecolagem.Count >= 2;
        }

        private void IniciarRotina(IEnumerator novaRotina)
        {
            if (rotina != null) StopCoroutine(rotina);
            rotina = StartCoroutine(novaRotina);
        }

        private void PrepararClique()
        {
            if (controleAviao == null) return;
            controleAviao.aguardandoCliqueRadar = true;
            GerenciadorAeroporto aeroporto = controleAviao.aeroportoOrigem != null ? controleAviao.aeroportoOrigem : aeroportoBase;
            if (aeroporto != null) aeroporto.aviaoSelecionadoParaMissao = controleAviao;
        }

        private void DefinirEstado(EstadoAviaoTransporte novoEstado, string motivo)
        {
            estadoAtual = novoEstado;
            if (controleAviao != null)
            {
                controleAviao.estadoAtual = NoSolo()
                    ? ControleAviao.EstadoAviao.ProntoNoPatio
                    : ControleAviao.EstadoAviao.EmMissao;
            }
            Debug.Log($"[C17 NOVO] {novoEstado}: {motivo}");
        }

        private bool NoSolo()
        {
            return estadoAtual == EstadoAviaoTransporte.Estacionado || estadoAtual == EstadoAviaoTransporte.Pousado || estadoAtual == EstadoAviaoTransporte.AguardandoDestino;
        }

        public GerenciadorAeroporto BuscarAeroportoAliadoMaisProximo()
        {
            GerenciadorAeroporto[] aeroportos = Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
            GerenciadorAeroporto resultado = null;
            float menorDistancia = float.MaxValue;
            int meuTime = ObterTime();
            for (int i = 0; i < aeroportos.Length; i++)
            {
                GerenciadorAeroporto aeroporto = aeroportos[i];
                if (aeroporto == null) continue;
                IdentidadeUnidade identidade = aeroporto.GetComponent<IdentidadeUnidade>();
                if (identidade != null && identidade.teamID != 0 && identidade.teamID != meuTime) continue;
                float distancia = (aeroporto.transform.position - transform.position).sqrMagnitude;
                if (distancia < menorDistancia) { menorDistancia = distancia; resultado = aeroporto; }
            }
            return resultado;
        }

        public void Comando_EmbarcarTropas() => ExecutarCarga(true, false);
        public void Comando_EmbarcarVeiculos() => ExecutarCarga(false, false);
        public void Comando_EmbarcarTodos() => ExecutarCarga(true, true);
        public void Comando_Desembarcar()
        {
            if (!NoSolo() || transporte == null) return;
            DefinirEstado(EstadoAviaoTransporte.Desembarcando, "desembarque");
            transporte.ExecutarDesembarque();
            DefinirEstado(EstadoAviaoTransporte.Pousado, "desembarque concluido");
        }

        private void ExecutarCarga(bool tropas, bool todos)
        {
            if (!NoSolo() || transporte == null) return;
            DefinirEstado(EstadoAviaoTransporte.Embarcando, "embarque");
            if (todos) transporte.IniciarEmbarqueTodos(ObterTime());
            else if (tropas) transporte.IniciarEmbarqueTropas(ObterTime());
            else transporte.IniciarEmbarqueVeiculos(ObterTime());
            DefinirEstado(EstadoAviaoTransporte.Pousado, "embarque concluido");
        }

        public void ExibirStatusCarga()
        {
            if (transporte != null) Debug.Log($"[C17] Carga: {transporte.TropasEmbarcadasCount}/{transporte.CapacidadeSoldados} tropas | {transporte.VeiculosEmbarcadosCount}/{transporte.CapacidadeVeiculos} veiculos.");
        }

        private float CalcularAlturaDoSolo()
        {
            Collider colisor = GetComponent<Collider>();
            if (colisor == null) return 0.25f;
            return Mathf.Clamp(transform.position.y - colisor.bounds.min.y + 0.05f, 0.05f, 4f);
        }

        private float AlturaSoloEm(Vector3 posicao)
        {
            if (Physics.Raycast(posicao + Vector3.up * 800f, Vector3.down, out RaycastHit hit, 1600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return posicao.y - alturaDoSolo;
        }

        private int ObterTime()
        {
            IdentidadeUnidade identidade = GetComponent<IdentidadeUnidade>();
            return identidade != null ? identidade.teamID : 1;
        }

        private static Vector3 Plano(Vector3 valor) { valor.y = 0f; return valor; }
        private static float DistanciaPlano(Vector3 a, Vector3 b) => Plano(a - b).magnitude;
        private static bool PontoValido(Vector3 ponto) => !float.IsNaN(ponto.x) && !float.IsNaN(ponto.y) && !float.IsNaN(ponto.z) && !float.IsInfinity(ponto.x) && !float.IsInfinity(ponto.y) && !float.IsInfinity(ponto.z);
    }
}
