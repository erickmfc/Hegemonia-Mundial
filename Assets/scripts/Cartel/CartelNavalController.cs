using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hegemonia.Cartel
{
    [Serializable]
    public sealed class ContatoCartelNaval
    {
        public string IdContato;
        public string TipoAlvo;
        public int TimeAlvo;
        public Vector3 PosicaoUnity;
        public Vector3 Direcao;
        public float Velocidade;
        public string UnidadeTransmissora;
        public float PrimeiraDeteccao;
        public float UltimaDeteccao;
        public bool Valido;
        public GameObject Alvo;
    }

    /// <summary>
    /// Autoridade exclusiva do Cartel Naval. Não cria unidades terrestres,
    /// não executa patrulha global e não usa a autoridade do CartelAIController
    /// para comandar seus barcos.
    /// </summary>
    [AddComponentMenu("Hegemonia/Cartel Naval/Controlador Naval")]
    public sealed class CartelNavalController : MonoBehaviour
    {
        [Header("Identidade")]
        [Min(1)] public int CartelTeamId = 9;
        public string NomeCartel = "Cartel Naval";
        public bool IniciarAutomaticamente = true;
        [Tooltip("Deixe o controlador antigo disponível para fallback; nesta cena ele pode ficar inativo.")]
        public bool SistemaAntigoContinuaDisponivel = true;
        public bool DesativarControladorAntigoNestaCena = true;
        public CartelAIController ControladorLegado;

        [Header("Ritmo otimizado")]
        [Min(0.25f)] public float IntervaloDecisao = 1.5f;
        [Min(2f)] public float IntervaloVarredura = 12f;
        [Min(1)] public int DiaAtivacao = 1;

        [Header("Frota naval")]
        public GameObject PrefabNavio;
        [Min(0)] public int NaviosIniciais = 2;
        [Min(1)] public int MaxNavios = 12;
        [Min(1)] public int NaviosPorOnda = 2;
        public List<GameObject> NaviosAtivos = new List<GameObject>(12);

        [Header("Radar do Cartel")]
        [Min(50f)] public float RaioDeteccao = 1400f;
        [Min(0.05f)] public float LimiarMovimentoRadar = 0.6f;
        [Min(1)] public int DiasParaReaquisiçãoEmMovimento = 2;
        [Min(1)] public int ValidadeContatoDias = 4;

        [Header("Reposição aleatória em dias")]
        [Tooltip("Dias de espera possíveis depois que a frota inteira for destruída.")]
        public int[] AtrasosRespawnDias = { 2, 5, 9 };
        [Min(0)] public int ReforcosPendentes;
        [Min(0)] public int OndaAtual;
        [SerializeField] private int sementeAleatoria = 90127;

        [Header("Ataque naval")]
        public bool RoubarCombustivel = true;
        public bool AtacarComDano = true;
        [Min(1)] public int DrenoCombustivelPorSegundo = 500;
        [Min(0.5f)] public float IntervaloAtaque = 4f;
        [Min(0f)] public float DanoPorAtaque = 25f;
        [Min(10f)] public float DistanciaAtaque = 70f;

        [Header("Diagnóstico")]
        public CartelControllerState Estado = CartelControllerState.Disabled;
        [TextArea(2, 5)] public string StatusDebug = string.Empty;
        public int ContatosAtivos;
        public int PetroleirosAtacados;
        public int PlataformasAtacadas;

        private readonly List<CartelNavalCrate> crates = new List<CartelNavalCrate>(32);
        private readonly List<CartelNavalCrate> rota = new List<CartelNavalCrate>(8);
        private readonly List<NavioPetroleiro> petroleiros = new List<NavioPetroleiro>(32);
        private readonly List<PlataformaOffshore> plataformas = new List<PlataformaOffshore>(32);
        private readonly Dictionary<string, ContatoCartelNaval> contatos = new Dictionary<string, ContatoCartelNaval>(64);
        private readonly Dictionary<int, GameObject> alvosPorBarco = new Dictionary<int, GameObject>(16);
        private readonly Dictionary<int, float> proximoAtaquePorBarco = new Dictionary<int, float>(16);
        private readonly Dictionary<int, float> proximoComandoPorBarco = new Dictionary<int, float>(16);
        private readonly Dictionary<int, Vector3> destinoPorBarco = new Dictionary<int, Vector3>(16);
        private readonly HashSet<int> mortesProcessadas = new HashSet<int>();
        private readonly Collider[] bufferDeteccao = new Collider[256];
        private System.Random aleatorio;
        private float proximaDecisao;
        private float proximaVarredura;
        private int proximoDiaRespawn = -1;
        private bool inicializado;
        private bool esperandoRespawn;

        public IReadOnlyList<CartelNavalCrate> Crates { get { return crates; } }
        public IReadOnlyDictionary<string, ContatoCartelNaval> Contatos { get { return contatos; } }

        private void OnEnable()
        {
            SistemaDeDanos.OnMorteGlobal += AoMorrerUnidade;
        }

        private void OnDisable()
        {
            SistemaDeDanos.OnMorteGlobal -= AoMorrerUnidade;
        }

        private void Start()
        {
            if (IniciarAutomaticamente && !EhCenaDeMenu()) Inicializar();
        }

        private void Update()
        {
            if (!IniciarAutomaticamente || EhCenaDeMenu()) return;
            if (!inicializado) Inicializar();
            if (!inicializado) return;

            if (Time.unscaledTime >= proximaVarredura)
            {
                proximaVarredura = Time.unscaledTime + Mathf.Max(2f, IntervaloVarredura);
                AtualizarAlvosCache();
                AtualizarDeteccaoLocal();
                AtualizarRadar();
            }

            if (Time.unscaledTime >= proximaDecisao)
            {
                proximaDecisao = Time.unscaledTime + Mathf.Max(0.25f, IntervaloDecisao);
                ProcessarRespawn();
                ProcessarFrota();
            }
        }

        public void Inicializar()
        {
            if (inicializado) return;
            aleatorio = new System.Random(sementeAleatoria);
            CarregarCrates();
            CarregarNaviosExistentes();

            if (DesativarControladorAntigoNestaCena && ControladorLegado != null)
            {
                ControladorLegado.enabled = false;
            }

            if (PrefabNavio == null)
            {
                StatusDebug = "Cartel Naval aguardando PrefabNavio.";
                Estado = CartelControllerState.WaitingForManualCreate;
                return;
            }

            inicializado = true;
            Estado = CartelControllerState.Operational;
            GarantirQuantidadeInicial();
            StatusDebug = "Cartel Naval operacional: somente barcos, patrulha e emboscada.";
        }

        private void CarregarCrates()
        {
            crates.Clear();
            rota.Clear();
            List<CartelNavalCrate> encontrados = CartelNavalCrate.GetAll(true);
            for (int i = 0; i < encontrados.Count; i++)
            {
                CartelNavalCrate crate = encontrados[i];
                if (crate == null) continue;
                crates.Add(crate);
                if (crate.IsUsable() && IsPontoDeRota(crate.Tipo)) InserirRotaOrdenada(crate);
            }
        }

        private void InserirRotaOrdenada(CartelNavalCrate crate)
        {
            int indice = rota.Count;
            for (int i = 0; i < rota.Count; i++)
            {
                if (crate.SequenciaRota < rota[i].SequenciaRota)
                {
                    indice = i;
                    break;
                }
            }
            rota.Insert(indice, crate);
        }

        private void CarregarNaviosExistentes()
        {
            for (int i = NaviosAtivos.Count - 1; i >= 0; i--)
            {
                if (NaviosAtivos[i] == null) NaviosAtivos.RemoveAt(i);
            }

            CartelNavalUnidade[] existentes = GetComponentsInChildren<CartelNavalUnidade>(true);
            for (int i = 0; i < existentes.Length; i++) AdicionarNavio(existentes[i].gameObject);
        }

        private void GarantirQuantidadeInicial()
        {
            int dia = ObterDiaAtual();
            if (dia < Mathf.Max(1, DiaAtivacao)) return;
            int alvo = Mathf.Min(Mathf.Max(0, NaviosIniciais), Mathf.Max(1, MaxNavios));
            while (NaviosAtivos.Count < alvo) SpawnarNavio();
        }

        private void ProcessarFrota()
        {
            for (int i = NaviosAtivos.Count - 1; i >= 0; i--)
            {
                GameObject barco = NaviosAtivos[i];
                if (barco == null || !barco.activeInHierarchy)
                {
                    NaviosAtivos.RemoveAt(i);
                    continue;
                }

                CartelNavalUnidade unidade = barco.GetComponent<CartelNavalUnidade>();
                if (unidade == null) unidade = barco.AddComponent<CartelNavalUnidade>();
                unidade.Controlador = this;

                GameObject alvo = EscolherAlvo(barco);
                if (alvo != null)
                {
                    alvosPorBarco[barco.GetInstanceID()] = alvo;
                    unidade.AlvoAdquirido = true;
                    Vector3 ponto = CalcularPontoDeAproximacao(alvo, barco.transform.position);
                    EnviarDestino(barco, unidade, ponto, "alvo");

                    if (DistanciaHorizontal(barco.transform.position, alvo.transform.position) <= DistanciaAtaque)
                    {
                        AtacarAlvo(barco, alvo, unidade);
                    }
                }
                else
                {
                    unidade.AlvoAdquirido = false;
                    GameObject alvoAnterior;
                    if (alvosPorBarco.TryGetValue(barco.GetInstanceID(), out alvoAnterior)) alvosPorBarco.Remove(barco.GetInstanceID());
                    EnviarParaPatrulha(barco, unidade);
                }
            }

            ContatosAtivos = 0;
            foreach (KeyValuePair<string, ContatoCartelNaval> contato in contatos)
            {
                if (contato.Value != null && contato.Value.Valido) ContatosAtivos++;
            }
        }

        private void EnviarParaPatrulha(GameObject barco, CartelNavalUnidade unidade)
        {
            if (rota.Count == 0)
            {
                unidade.EstadoOperacional = "Sem rota naval configurada";
                return;
            }

            if (unidade.RotaAtual == null || !unidade.RotaAtual.IsUsable() || unidade.RotaAtual.Contains(unidade.transform.position))
            {
                unidade.IndicePatrulha = (unidade.IndicePatrulha + 1) % rota.Count;
                unidade.RotaAtual = rota[unidade.IndicePatrulha];
            }
            EnviarDestino(barco, unidade, unidade.RotaAtual.Position, "patrulha");
            unidade.EstadoOperacional = "Patrulhando";
        }

        private void EnviarDestino(GameObject barco, CartelNavalUnidade unidade, Vector3 destino, string motivo)
        {
            if (unidade == null || unidade.Controlador != this) return;
            if (!NavalPlacementResolver.IsWaterAtPosition(destino))
            {
                unidade.EstadoOperacional = "Destino recusado: ponto fora da água";
                return;
            }

            int id = barco.GetInstanceID();
            Vector3 destinoAnterior;
            float proximo;
            bool mudou = !destinoPorBarco.TryGetValue(id, out destinoAnterior) || Vector3.Distance(destinoAnterior, destino) > 12f;
            bool liberado = !proximoComandoPorBarco.TryGetValue(id, out proximo) || Time.unscaledTime >= proximo;
            if (!mudou && !liberado) return;

            ControleNavioRealista navio = barco.GetComponent<ControleNavioRealista>();
            if (navio == null)
            {
                unidade.EstadoOperacional = "Sem ControleNavioRealista";
                return;
            }

            SaveableEntity saveable = SaveableEntity.Garantir(barco, "Cartel/Naval");
            string idOrdem = "cartel-naval/" + saveable.UniqueId + "/" + motivo;
            navio.DefinirDestino(destino, idOrdem);
            destinoPorBarco[id] = destino;
            proximoComandoPorBarco[id] = Time.unscaledTime + 1f;
        }

        private GameObject EscolherAlvo(GameObject barco)
        {
            float melhor = Mathf.Max(50f, RaioDeteccao) * Mathf.Max(50f, RaioDeteccao);
            GameObject escolhido = null;

            for (int i = 0; i < petroleiros.Count; i++)
            {
                NavioPetroleiro petroleiro = petroleiros[i];
                if (petroleiro == null || !petroleiro.gameObject.activeInHierarchy) continue;
                if (EhDoCartel(petroleiro.gameObject)) continue;
                float distancia = DistanciaHorizontalSqr(barco.transform.position, petroleiro.transform.position);
                if (distancia < melhor) { melhor = distancia; escolhido = petroleiro.gameObject; }
            }

            for (int i = 0; i < plataformas.Count; i++)
            {
                PlataformaOffshore plataforma = plataformas[i];
                if (plataforma == null || !plataforma.gameObject.activeInHierarchy) continue;
                if (EhDoCartel(plataforma.gameObject)) continue;
                float distancia = DistanciaHorizontalSqr(barco.transform.position, plataforma.transform.position);
                if (distancia < melhor) { melhor = distancia; escolhido = plataforma.gameObject; }
            }

            return escolhido;
        }

        private Vector3 CalcularPontoDeAproximacao(GameObject alvo, Vector3 origem)
        {
            Vector3 direcao = origem - alvo.transform.position;
            direcao.y = 0f;
            if (direcao.sqrMagnitude < 1f) direcao = -alvo.transform.forward;
            if (direcao.sqrMagnitude < 1f) direcao = Vector3.back;
            direcao.Normalize();
            Vector3 ponto = alvo.transform.position + direcao * Mathf.Max(35f, DistanciaAtaque * 0.65f);
            ponto.y = NavalPlacementResolver.ResolveSeaLevel();
            return ponto;
        }

        private void AtacarAlvo(GameObject barco, GameObject alvo, CartelNavalUnidade unidade)
        {
            int idBarco = barco.GetInstanceID();
            float proximo;
            if (proximoAtaquePorBarco.TryGetValue(idBarco, out proximo) && Time.unscaledTime < proximo) return;
            proximoAtaquePorBarco[idBarco] = Time.unscaledTime + Mathf.Max(0.5f, IntervaloAtaque);

            NavioPetroleiro petroleiro = alvo.GetComponent<NavioPetroleiro>();
            PlataformaOffshore plataforma = alvo.GetComponent<PlataformaOffshore>();
            if (petroleiro != null)
            {
                if (RoubarCombustivel)
                {
                    int drenado = Mathf.Min(petroleiro.petroleoCarregado, Mathf.Max(1, DrenoCombustivelPorSegundo));
                    petroleiro.petroleoCarregado -= drenado;
                }
                PetroleirosAtacados++;
            }
            else if (plataforma != null)
            {
                if (RoubarCombustivel) plataforma.DrenarPetroleo(Mathf.Max(1, DrenoCombustivelPorSegundo));
                PlataformasAtacadas++;
            }

            if (AtacarComDano && DanoPorAtaque > 0f)
            {
                SistemaDeDanos danos = EncontrarDanos(alvo);
                if (danos != null) danos.ReceberDano(DanoPorAtaque, barco);
            }

            unidade.EstadoOperacional = "Emboscando alvo naval";
            StatusDebug = NomeCartel + ": ataque naval ativo; sem unidades terrestres.";
        }

        private void AtualizarAlvosCache()
        {
            NavioPetroleiro[] novosPetroleiros = UnityEngine.Object.FindObjectsByType<NavioPetroleiro>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            PlataformaOffshore[] novasPlataformas = UnityEngine.Object.FindObjectsByType<PlataformaOffshore>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            SubstituirPetroleiros(novosPetroleiros);
            SubstituirPlataformas(novasPlataformas);
        }

        private void SubstituirPetroleiros(NavioPetroleiro[] novos)
        {
            petroleiros.Clear();
            for (int i = 0; i < novos.Length; i++) if (novos[i] != null) petroleiros.Add(novos[i]);
        }

        private void SubstituirPlataformas(PlataformaOffshore[] novas)
        {
            plataformas.Clear();
            for (int i = 0; i < novas.Length; i++) if (novas[i] != null) plataformas.Add(novas[i]);
        }

        private void AtualizarDeteccaoLocal()
        {
            int dia = ObterDiaAtual();
            for (int i = 0; i < NaviosAtivos.Count; i++)
            {
                GameObject barco = NaviosAtivos[i];
                if (barco == null || !barco.activeInHierarchy) continue;

                int encontrados = Physics.OverlapSphereNonAlloc(barco.transform.position, Mathf.Max(50f, RaioDeteccao), bufferDeteccao);
                for (int j = 0; j < encontrados; j++)
                {
                    Collider collider = bufferDeteccao[j];
                    if (collider == null) continue;
                    IdentidadeUnidade identidade = SistemaDeDanos.ResolverIdentidade(collider);
                    if (identidade == null || identidade.teamID == CartelTeamId) continue;
                    GameObject alvo = identidade.gameObject;
                    if (alvo == barco || !alvo.activeInHierarchy) continue;
                    RegistrarContato(alvo, barco, dia);
                }
            }

            List<string> expirados = new List<string>();
            float validade = Mathf.Max(1, ValidadeContatoDias) * ResolverDuracaoDia();
            foreach (KeyValuePair<string, ContatoCartelNaval> entrada in contatos)
            {
                if (entrada.Value == null || Time.unscaledTime - entrada.Value.UltimaDeteccao > validade)
                {
                    expirados.Add(entrada.Key);
                }
            }
            for (int i = 0; i < expirados.Count; i++) contatos.Remove(expirados[i]);
        }

        private void RegistrarContato(GameObject alvo, GameObject barco, int dia)
        {
            SaveableEntity saveable = SaveableEntity.Garantir(alvo);
            string id = saveable.UniqueId;
            ContatoCartelNaval contato;
            if (!contatos.TryGetValue(id, out contato) || contato == null)
            {
                contato = new ContatoCartelNaval { IdContato = id, PrimeiraDeteccao = Time.unscaledTime };
                contatos[id] = contato;
            }

            Vector3 anterior = contato.PosicaoUnity;
            contato.PosicaoUnity = alvo.transform.position;
            contato.Direcao = contato.PosicaoUnity - anterior;
            contato.Direcao.y = 0f;
            if (contato.Direcao.sqrMagnitude > 0.01f) contato.Direcao.Normalize();
            contato.Velocidade = alvo.GetComponent<ControleNavioRealista>() != null
                ? alvo.GetComponent<ControleNavioRealista>().VelocidadeAtual
                : 0f;
            IdentidadeUnidade identidade = SistemaDeDanos.ResolverIdentidade(alvo.transform);
            contato.TipoAlvo = identidade != null ? identidade.tipoUnidade.ToString() : "Unidade";
            contato.TimeAlvo = identidade != null ? identidade.teamID : 0;
            contato.UnidadeTransmissora = barco.name;
            contato.UltimaDeteccao = Time.unscaledTime;
            contato.Valido = true;
            contato.Alvo = alvo;
        }

        private void AtualizarRadar()
        {
            int dia = ObterDiaAtual();
            for (int i = 0; i < NaviosAtivos.Count; i++)
            {
                GameObject barco = NaviosAtivos[i];
                if (barco == null) continue;
                CartelNavalUnidade unidade = barco.GetComponent<CartelNavalUnidade>();
                ControleNavioRealista navio = barco.GetComponent<ControleNavioRealista>();
                if (unidade != null)
                {
                    unidade.AtualizarRadar(dia, LimiarMovimentoRadar, DiasParaReaquisiçãoEmMovimento, navio != null ? navio.VelocidadeAtual : 0f);
                }
            }
        }

        private void ProcessarRespawn()
        {
            int dia = ObterDiaAtual();
            if (ReforcosPendentes > 0 && NaviosAtivos.Count < Mathf.Max(1, MaxNavios))
            {
                if (SpawnarNavio()) ReforcosPendentes--;
            }

            if (esperandoRespawn && NaviosAtivos.Count == 0 && dia >= proximoDiaRespawn)
            {
                esperandoRespawn = false;
                OndaAtual++;
                int quantidade = Mathf.Min(Mathf.Max(1, NaviosPorOnda), Mathf.Max(1, MaxNavios));
                for (int i = 0; i < quantidade; i++) SpawnarNavio();
                StatusDebug = "Nova onda naval do Cartel criada no dia " + dia + ".";
            }
        }

        private bool SpawnarNavio()
        {
            if (PrefabNavio == null || NaviosAtivos.Count >= Mathf.Max(1, MaxNavios)) return false;
            CartelNavalCrate crate = EscolherPontoDeSpawn(NaviosAtivos.Count);
            Vector3 posicao = crate != null ? crate.Position : transform.position;
            if (!TentarEncontrarAgua(ref posicao)) return false;

            Quaternion rotacao = crate != null ? crate.transform.rotation : transform.rotation;
            GameObject barco = Instantiate(PrefabNavio, posicao, rotacao);
            barco.name = NomeCartel + " - Patrulha " + (NaviosAtivos.Count + 1).ToString("00");

            SaveableEntity saveable = SaveableEntity.Garantir(barco, "Cartel/Naval");
            IdentidadeUnidade identidade = barco.GetComponent<IdentidadeUnidade>();
            if (identidade == null) identidade = barco.AddComponent<IdentidadeUnidade>();
            identidade.teamID = CartelTeamId;
            identidade.nomeDoPais = NomeCartel;
            identidade.tipoUnidade = TipoUnidade.Naval;
            identidade.nomeDeBatismo = barco.name;

            CartelNavalUnidade unidade = barco.GetComponent<CartelNavalUnidade>();
            if (unidade == null) unidade = barco.AddComponent<CartelNavalUnidade>();
            unidade.Controlador = this;
            unidade.IndicePatrulha = NaviosAtivos.Count % Mathf.Max(1, rota.Count);
            unidade.RotaAtual = rota.Count > 0 ? rota[unidade.IndicePatrulha] : null;
            unidade.EstadoOperacional = "Entrando na patrulha naval";
            AdicionarNavio(barco);
            return true;
        }

        private void AdicionarNavio(GameObject barco)
        {
            if (barco != null && !NaviosAtivos.Contains(barco)) NaviosAtivos.Add(barco);
        }

        private CartelNavalCrate EscolherPontoDeSpawn(int indice)
        {
            TipoCreateNavalCartel tipo = indice % 2 == 0 ? TipoCreateNavalCartel.SpawnNavio01 : TipoCreateNavalCartel.SpawnNavio02;
            for (int i = 0; i < crates.Count; i++) if (crates[i] != null && crates[i].Tipo == tipo && crates[i].IsUsable()) return crates[i];
            for (int i = 0; i < crates.Count; i++) if (crates[i] != null && crates[i].Tipo == TipoCreateNavalCartel.BaseNaval && crates[i].IsUsable()) return crates[i];
            return rota.Count > 0 ? rota[0] : null;
        }

        private bool TentarEncontrarAgua(ref Vector3 posicao)
        {
            posicao.y = NavalPlacementResolver.ResolveSeaLevel();
            if (NavalPlacementResolver.IsWaterAtPosition(posicao)) return true;
            Vector3[] deslocamentos = { Vector3.forward * 80f, Vector3.back * 80f, Vector3.right * 80f, Vector3.left * 80f, Vector3.forward * 160f, Vector3.back * 160f };
            for (int i = 0; i < deslocamentos.Length; i++)
            {
                Vector3 tentativa = posicao + deslocamentos[i];
                tentativa.y = posicao.y;
                if (NavalPlacementResolver.IsWaterAtPosition(tentativa)) { posicao = tentativa; return true; }
            }
            return false;
        }

        private void AoMorrerUnidade(SistemaDeDanos danos, GameObject agressor)
        {
            if (danos == null) return;
            int idMorte = danos.gameObject.GetInstanceID();
            if (!mortesProcessadas.Add(idMorte)) return;

            CartelNavalUnidade barco = EncontrarBarco(danos);
            if (barco != null)
            {
                NaviosAtivos.Remove(barco.gameObject);
                alvosPorBarco.Remove(barco.gameObject.GetInstanceID());
                if (NaviosAtivos.Count == 0)
                {
                    esperandoRespawn = true;
                    proximoDiaRespawn = ObterDiaAtual() + EscolherAtrasoRespawn();
                }
                return;
            }

            CartelNavalUnidade agressorNaval = agressor != null ? agressor.GetComponentInParent<CartelNavalUnidade>() : null;
            if (agressorNaval != null)
            {
                NavioPetroleiro petroleiro = danos.GetComponentInParent<NavioPetroleiro>();
                PlataformaOffshore plataforma = danos.GetComponentInParent<PlataformaOffshore>();
                if (petroleiro != null || plataforma != null) ReforcosPendentes = Mathf.Min(ReforcosPendentes + 1, Mathf.Max(0, MaxNavios - NaviosAtivos.Count));
            }
        }

        private CartelNavalUnidade EncontrarBarco(SistemaDeDanos danos)
        {
            for (int i = 0; i < NaviosAtivos.Count; i++)
            {
                GameObject barco = NaviosAtivos[i];
                if (barco == null) continue;
                if (danos.transform == barco.transform || danos.transform.IsChildOf(barco.transform)) return barco.GetComponent<CartelNavalUnidade>();
            }
            return null;
        }

        private int EscolherAtrasoRespawn()
        {
            if (AtrasosRespawnDias == null || AtrasosRespawnDias.Length == 0) return 2;
            int indice = aleatorio != null ? aleatorio.Next(0, AtrasosRespawnDias.Length) : 0;
            return Mathf.Max(1, AtrasosRespawnDias[indice]);
        }

        private bool EhDoCartel(GameObject objeto)
        {
            IdentidadeUnidade identidade = SistemaDeDanos.ResolverIdentidade(objeto != null ? objeto.transform : null);
            return identidade != null && identidade.teamID == CartelTeamId;
        }

        private SistemaDeDanos EncontrarDanos(GameObject objeto)
        {
            if (objeto == null) return null;
            SistemaDeDanos danos = objeto.GetComponent<SistemaDeDanos>();
            if (danos != null) return danos;
            danos = objeto.GetComponentInChildren<SistemaDeDanos>(true);
            if (danos != null) return danos;
            return objeto.GetComponentInParent<SistemaDeDanos>();
        }

        private int ObterDiaAtual()
        {
            return GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1;
        }

        private float ResolverDuracaoDia()
        {
            return GerenciadorTempo.Instancia != null ? Mathf.Max(1f, GerenciadorTempo.Instancia.duracaoDiaSegundos) : 30f;
        }

        private bool EhCenaDeMenu()
        {
            string nome = SceneManager.GetActiveScene().name.ToLowerInvariant();
            return nome.Contains("menu") || nome.Contains("loading") || nome.Contains("tutorial");
        }

        private static bool IsPontoDeRota(TipoCreateNavalCartel tipo)
        {
            return tipo == TipoCreateNavalCartel.Rota01 || tipo == TipoCreateNavalCartel.Rota02 || tipo == TipoCreateNavalCartel.Rota03 || tipo == TipoCreateNavalCartel.Rota04;
        }

        private static float DistanciaHorizontalSqr(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return (a - b).sqrMagnitude;
        }

        private static float DistanciaHorizontal(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(DistanciaHorizontalSqr(a, b));
        }
    }
}
