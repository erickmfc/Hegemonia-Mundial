using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TorretaAntiaerea : MonoBehaviour
{
    [Header("Postura de Combate")]
    [Tooltip("Desativa disparo e radar quando a unidade está em postura pacífica.")]
    public bool modoPassivo;
    [Header("Configurações do Radar (Aéreo)")]
    [Tooltip("Distância máxima que a torreta consegue enxergar as ameaças aéreas")]
    public float alcanceArea = 150f;
    
    [Tooltip("Mantido para compatibilidade com prefabs antigos; a altura do terreno não define um alvo como aeronave.")]
    public float alturaMinimaVoo = 5f;

    [Header("Articulações da Torreta")]
    [Tooltip("Peça que gira 360 graus horizontalmente (Esquerda/Direita)")]
    public Transform baseGiratoria; 
    
    [Tooltip("Peça que sobe e desce para mirar no céu (Apontamento vertical)")]
    public Transform canoElevacao; 
    
    [Tooltip("Locais de onde os tiros vão sair (Pode ser 1 cano, 2 canos, 4 canos...)")]
    public Transform[] pontosDeDisparo;
    
    [Header("Limites de Rotação (Anti-Clipping)")]
    public bool limitarInclinacao = true;
    [Tooltip("Elevação mínima (graus). Zero impede atirar abaixo da linha do horizonte.")]
    public float elevacaoMinima = 0f; 
    public float elevacaoMaxima = 85f;
    
    public bool limitarRotacaoY = false;
    public float anguloMinimoY = -160f;
    public float anguloMaximoY = 160f;

    [Header("Segurança de Tiro (Fogo Amigo)")]
    [Tooltip("Impede disparo se o cano estiver mirando numa parte do próprio veículo")]
    public bool checarLinhaDeVisao = true;
    
    [Header("Controle de Disparo")]
    [Tooltip("Quantidade de tiros consecutivos a cada rajada")]
    public int quantidadeDeDisparo = 10;
    
    [Tooltip("Tiros por segundo (Cadência/Rate of Fire)")]
    public float tirosPorSegundo = 5f;
    
    [Tooltip("Tempo de pausa/recarga entre as rajadas")]
    public float tempoPausaRajada = 2f;
    
    [Header("Munição e Visual")]
    public GameObject prefabProjetil;
    public float velocidadeProjetil = 200f;
    public AudioClip somDisparo;

    [Header("Economia da Municao")]
    [Tooltip("No Ares_Ar, cada disparo compra um cartucho diretamente do saldo do pais.")]
    public bool cobrarCadaDisparo = true;
    [Tooltip("Quantidade de cartuchos no carregador antes da pausa de reabastecimento.")]
    public int capacidadeCartucho = 10;
    [Tooltip("Pausa necessaria para reabastecer o carregador do Ares_Ar.")]
    public float tempoReabastecimento = 8f;
    public string idMunicao = "municao_ares_ar";

    // Variáveis internas state
    private Transform alvoAtual;
    private IdentidadeUnidade minhaIdentidade;
    private bool atirando = false;
    private bool reabastecendo = false;
    private float contadorReabastecimento;
    private int cartuchosAtuais;
    private bool ehAresAr;
    private float proximoAvisoSemSaldo;
    private AudioSource audioSource;
    private int indexPontoDisparo = 0;
    private static readonly List<IdentidadeUnidade> unidadesRegistroRadar = new List<IdentidadeUnidade>(256);
    private static int versaoRegistroRadar = -1;
    private static readonly RaycastHit[] _linhaDeVisaoHits = new RaycastHit[32];
    private Quaternion rotacaoInicialBaseGiratoria = Quaternion.identity;
    private Quaternion rotacaoInicialCanoElevacao = Quaternion.identity;
    private Vector3 eulerRepousoBaseGiratoria;
    private Vector3 eulerRepousoCanoElevacao;

    Transform ResolverTransformPrincipal(Transform alvo)
    {
        if (alvo == null) return null;

        SistemaDeDanos vida = alvo.GetComponentInParent<SistemaDeDanos>();
        if (vida != null) return vida.transform;

        ControleAviao aviao = alvo.GetComponentInParent<ControleAviao>();
        if (aviao != null) return aviao.transform;

        Helicoptero helicoptero = alvo.GetComponentInParent<Helicoptero>();
        if (helicoptero != null) return helicoptero.transform;

        IdentidadeUnidade identidade = alvo.GetComponentInParent<IdentidadeUnidade>();
        if (identidade != null) return identidade.transform;

        return alvo.root != null ? alvo.root : alvo;
    }

    void Start()
    {
        ehAresAr = SistemaGastosMilitares.EhAresAr(gameObject.name + " " + transform.root.name);
        if (ehAresAr)
        {
            SistemaGastosMilitares.GarantirInstancia();
            DefinicaoMunicaoMilitar definicao = SistemaGastosMilitares.Instancia != null
                ? SistemaGastosMilitares.Instancia.ObterMunicao(idMunicao)
                : null;
            if (definicao != null)
            {
                capacidadeCartucho = Mathf.Max(1, definicao.capacidadeCartucho);
                tempoReabastecimento = Mathf.Max(0.1f, definicao.tempoReabastecimento);
            }
            cartuchosAtuais = Mathf.Max(1, capacidadeCartucho);
        }
        SincronizarIdentidadePropria();

        if (baseGiratoria != null)
        {
            rotacaoInicialBaseGiratoria = baseGiratoria.localRotation;
            eulerRepousoBaseGiratoria = baseGiratoria.localEulerAngles;
        }
        if (canoElevacao != null)
        {
            rotacaoInicialCanoElevacao = canoElevacao.localRotation;
            eulerRepousoCanoElevacao = canoElevacao.localEulerAngles;
        }

        // Prepara sistema de som
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        AudioRuntime.ConfigurarFonteDeTiro(audioSource);

        // Inicia varredura do céu a cada 0.5s para economizar processamento
        InvokeRepeating("ProcurarAlvoAereo", Random.Range(0f, 0.5f), 0.5f);
    }

    void Update()
    {
        if (modoPassivo)
        {
            alvoAtual = null;
            return;
        }

        if (reabastecendo)
        {
            contadorReabastecimento -= Time.deltaTime;
            if (contadorReabastecimento <= 0f)
            {
                reabastecendo = false;
                cartuchosAtuais = Mathf.Max(1, capacidadeCartucho);
                contadorReabastecimento = 0f;
            }
            return;
        }

        if (alvoAtual != null)
        {
            // Se o alvo morreu ou fugiu
            if (!alvoAtual.gameObject.activeInHierarchy || Vector3.Distance(transform.position, alvoAtual.position) > alcanceArea + 10f)
            {
                alvoAtual = null;
                return;
            }

            // Gira fisicamente a torreta
            MirarNoAlvo();

            // Lógica de Atirar
            if (!atirando)
            {
                if (MirouComAcerto())
                {
                    StartCoroutine(RotinaDeDisparo());
                }
            }
        }
        else
        {
            // Ocioso: Gira a base 360 graus lentamente "vigiando o céu"
            if (baseGiratoria != null)
            {
                baseGiratoria.Rotate(0, 15f * Time.deltaTime, 0, Space.Self);
            }
        }
    }

    void ProcurarAlvoAereo()
    {
        if (modoPassivo)
        {
            alvoAtual = null;
            return;
        }

        // Se já tem um alvo válido dentro da área, mantém ele
        if (alvoAtual != null && alvoAtual.gameObject.activeInHierarchy)
        {
            if (Vector3.Distance(transform.position, alvoAtual.position) <= alcanceArea)
            {
                ReportarContatoAereo(alvoAtual);
                return;
            }
        }

        alvoAtual = null;

        // O Ares tem alcance de vários quilômetros. Um OverlapSphere desse
        // tamanho vasculhava prédios, projéteis e colisores sem identidade em
        // cada bateria. O registro de unidades já contém o time e o tipo do
        // alvo, e evita esse custo de física sem perder o alcance vertical.
        alvoAtual = ProcurarAlvoAereoNoRegistroGlobal();
        ReportarContatoAereo(alvoAtual);
    }

    public void DefinirModoAtivo(bool ativo)
    {
        SincronizarIdentidadePropria();
        modoPassivo = !ativo;
        if (modoPassivo) alvoAtual = null;
    }

    private Transform ProcurarAlvoAereoNoRegistroGlobal()
    {
        SincronizarIdentidadePropria();
        if (minhaIdentidade == null || minhaIdentidade.teamID <= 0)
        {
            return null;
        }

        AtualizarCacheAlvosAereos();
        float alcanceSqr = alcanceArea * alcanceArea;
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        for (int i = 0; i < unidadesRegistroRadar.Count; i++)
        {
            IdentidadeUnidade idAlvo = unidadesRegistroRadar[i];
            if (idAlvo == null || !idAlvo.gameObject.activeInHierarchy) continue;
            if (!ContextoTerritorialDiplomatico.PodeDispararPorHostilidadeOuGuerra(minhaIdentidade.teamID, idAlvo)) continue;

            Vector3 identidadePosicao = idAlvo.transform.position;
            float distanciaIdentidadeSqr = (identidadePosicao - transform.position).sqrMagnitude;
            if (distanciaIdentidadeSqr > alcanceSqr || distanciaIdentidadeSqr >= menorDistancia) continue;

            Transform alvoPrincipal = ResolverTransformPrincipal(idAlvo.transform);
            if (alvoPrincipal == null || alvoPrincipal.root == transform.root) continue;

            float distSqr = (alvoPrincipal.position - transform.position).sqrMagnitude;
            if (distSqr > alcanceSqr || distSqr >= menorDistancia) continue;

            menorDistancia = distSqr;
            melhorAlvo = alvoPrincipal;
        }

        return melhorAlvo;
    }

    private void ReportarContatoAereo(Transform alvo)
    {
        if (alvo == null || minhaIdentidade == null || minhaIdentidade.teamID <= 0)
            return;

        Hegemonia.RTS.RTSVisibilityService visibilidade = Hegemonia.RTS.RTSVisibilityService.Instancia;
        IdentidadeUnidade identidadeAlvo = ResolverIdentidadeAlvo(alvo);
        if (visibilidade != null && identidadeAlvo != null)
        {
            visibilidade.ReportContact(
                minhaIdentidade.teamID,
                identidadeAlvo,
                Hegemonia.RTS.RTSDetectionSource.Radar);
        }
    }

    private static void AtualizarCacheAlvosAereos()
    {
        int versaoAtual = RegistroEntidadesJogo.Version;
        if (versaoRegistroRadar == versaoAtual)
            return;

        RegistroEntidadesJogo.FillUnidades(unidadesRegistroRadar);
        for (int i = unidadesRegistroRadar.Count - 1; i >= 0; i--)
        {
            IdentidadeUnidade identidade = unidadesRegistroRadar[i];
            if (identidade == null || !EhIdentidadeAerea(identidade))
                unidadesRegistroRadar.RemoveAt(i);
        }

        versaoRegistroRadar = versaoAtual;
    }

    private static bool EhIdentidadeAerea(IdentidadeUnidade identidade)
    {
        if (identidade == null) return false;
        if (identidade.tipoUnidade == TipoUnidade.Aereo) return true;

        Transform alvo = identidade.transform;
        if (alvo.GetComponentInParent<Helicoptero>() != null
            || alvo.GetComponentInParent<ControleAviao>() != null
            || alvo.GetComponentInParent<ControleAviaoCaca>() != null
            || alvo.GetComponentInParent<AviaoBombardeiro>() != null
            || alvo.GetComponentInParent<C700TransporteAereo>() != null)
        {
            return true;
        }

        string nome = alvo.name;
        return nome.IndexOf("aviao", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("avião", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("heli", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("caca", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("caça", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("jato", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("drone", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("bombard", System.StringComparison.OrdinalIgnoreCase) >= 0
            || nome.IndexOf("b260", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SincronizarIdentidadePropria()
    {
        IdentidadeUnidade identidadeRaiz = transform.root != null
            ? transform.root.GetComponent<IdentidadeUnidade>()
            : null;
        minhaIdentidade = identidadeRaiz != null
            ? identidadeRaiz
            : GetComponentInParent<IdentidadeUnidade>();

        IdentidadeIA identidadeIA = GetComponentInParent<IdentidadeIA>()
            ?? GetComponentInChildren<IdentidadeIA>(true);
        if (minhaIdentidade == null)
        {
            minhaIdentidade = gameObject.AddComponent<IdentidadeUnidade>();
        }

        if (identidadeIA != null && identidadeIA.teamID > 0)
        {
            minhaIdentidade.teamID = identidadeIA.teamID;
        }
    }

    void MirarNoAlvo()
    {
        // 1. Gira a base horizontalmente com limites locais
        if (baseGiratoria != null)
        {
            Vector3 direcaoBase = alvoAtual.position - baseGiratoria.position;
            Transform referenciaBase = baseGiratoria.parent != null ? baseGiratoria.parent : transform;
            Vector3 localDir = referenciaBase.InverseTransformDirection(direcaoBase);
            
            float anguloY = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            if (limitarRotacaoY) anguloY = Mathf.Clamp(anguloY, anguloMinimoY, anguloMaximoY);
            
            // Mantém a inclinação/roll authored do suporte e altera somente o yaw.
            Quaternion rotacaoAlvoBase = Quaternion.Euler(eulerRepousoBaseGiratoria.x, anguloY, eulerRepousoBaseGiratoria.z);
            baseGiratoria.localRotation = Quaternion.Slerp(baseGiratoria.localRotation, rotacaoAlvoBase, Time.deltaTime * 40f);
        }

        // 2. Gira o cano verticalmente com limites locais para não cruzar o convés
        if (canoElevacao != null)
        {
            Vector3 direcaoCano = alvoAtual.position - canoElevacao.position;
            Transform refCano = canoElevacao.parent != null ? canoElevacao.parent : canoElevacao;
            Vector3 localDirCano = refCano.InverseTransformDirection(direcaoCano);
            
            float distanciaPlana = new Vector2(localDirCano.x, localDirCano.z).magnitude;
            float giroPitch = -Mathf.Atan2(localDirCano.y, distanciaPlana) * Mathf.Rad2Deg;
            
            if (limitarInclinacao) giroPitch = Mathf.Clamp(giroPitch, -elevacaoMaxima, -elevacaoMinima);
            
            Quaternion rotacaoAlvoCano = Quaternion.Euler(giroPitch, eulerRepousoCanoElevacao.y, eulerRepousoCanoElevacao.z);
            canoElevacao.localRotation = Quaternion.Slerp(canoElevacao.localRotation, rotacaoAlvoCano, Time.deltaTime * 40f);
        }
    }

    bool MirouComAcerto()
    {
        if (alvoAtual == null) return false;
        
        // 1. Verifica se a base horizontal chegou o suficiente na rotação
        if (baseGiratoria != null)
        {
            Vector3 direcaoPlanaAoAlvo = (alvoAtual.position - baseGiratoria.position);
            direcaoPlanaAoAlvo.y = 0;
            if (direcaoPlanaAoAlvo != Vector3.zero)
            {
                direcaoPlanaAoAlvo.Normalize();
                Vector3 baseForwardPlano = baseGiratoria.forward;
                baseForwardPlano.y = 0;
                baseForwardPlano.Normalize();
                
                // Tolerância gigantesca (45 GRAUS) para jatos velozes não ficarem fora da zona
                if (Vector3.Angle(baseForwardPlano, direcaoPlanaAoAlvo) > 45f) return false;
            }
        }
        
        // 2. Verifica se o cano vertical levantou o suficiente
        if (canoElevacao != null)
        {
            Vector3 direcaoCanoIdeal = (alvoAtual.position - canoElevacao.position).normalized;
            // Tolerância gigante no cano também (45 graus)
            if (Vector3.Angle(canoElevacao.forward, direcaoCanoIdeal) > 45f) return false;
        }

        // 3. Segurança: Não atirar através do próprio navio!
        if (checarLinhaDeVisao && pontosDeDisparo != null && pontosDeDisparo.Length > 0 && pontosDeDisparo[0] != null)
        {
            // O cano pode começar dentro do collider da própria unidade. Ignorar
            // esses hits evita que a malha do veículo trave todos os disparos;
            // obstáculos externos no corredor curto continuam bloqueando o tiro.
            int hitCount = Physics.RaycastNonAlloc(
                pontosDeDisparo[0].position,
                pontosDeDisparo[0].forward,
                _linhaDeVisaoHits,
                35f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = _linhaDeVisaoHits[i].collider;
                if (collider == null || collider.transform.root == transform.root)
                {
                    continue;
                }

                return false;
            }
        }

        return true;
    }

    IEnumerator RotinaDeDisparo()
    {
        atirando = true;
        
        float tempoEntreTiros = 1f / tirosPorSegundo;
        int disparosFeitos = 0;

        while (disparosFeitos < quantidadeDeDisparo && alvoAtual != null)
        {
            while (alvoAtual != null && !MirouComAcerto())
            {
                yield return null;
            }
            
            if (alvoAtual == null) break;

            if (!DispararMunicoes())
                break;
            disparosFeitos++;

            if (ehAresAr && cartuchosAtuais <= 0)
            {
                IniciarReabastecimento();
                break;
            }
            
            yield return new WaitForSeconds(tempoEntreTiros);
        }

        yield return new WaitForSeconds(tempoPausaRajada);
        atirando = false;
    }

    bool DispararMunicoes()
    {
        if (prefabProjetil == null) return false;

        if (ehAresAr && cobrarCadaDisparo)
        {
            int teamId = minhaIdentidade != null ? Mathf.Max(1, minhaIdentidade.teamID) : 1;
            string mensagem = "Sistema de gastos militares indisponivel.";
            bool pagamentoAprovado = false;
            if (SistemaGastosMilitares.Instancia == null)
                SistemaGastosMilitares.GarantirInstancia();
            if (SistemaGastosMilitares.Instancia != null)
            {
                pagamentoAprovado = SistemaGastosMilitares.Instancia.TentarPagarDisparo(teamId, idMunicao, transform.root.name, out mensagem);
            }
            if (!pagamentoAprovado)
            {
                if (Time.unscaledTime >= proximoAvisoSemSaldo)
                {
                    Debug.LogWarning("[Ares_Ar] " + mensagem + " Disparo pausado ate haver saldo.", this);
                    proximoAvisoSemSaldo = Time.unscaledTime + 2f;
                }
                return false;
            }
            cartuchosAtuais = Mathf.Max(0, cartuchosAtuais - 1);
        }

        Transform pontoSaida = transform;
        if (pontosDeDisparo != null && pontosDeDisparo.Length > 0)
        {
            if (pontosDeDisparo[indexPontoDisparo] != null)
            {
                pontoSaida = pontosDeDisparo[indexPontoDisparo];
            }
            indexPontoDisparo = (indexPontoDisparo + 1) % pontosDeDisparo.Length; 
        }

        GameObject bala = PoolDeObjetosCombate.Spawn(prefabProjetil, pontoSaida.position, pontoSaida.rotation);
        
        Projetil p = bala.GetComponent<Projetil>();
        if (p == null) p = bala.AddComponent<Projetil>();

        alvoAtual = ResolverTransformPrincipal(alvoAtual);
        p.SetDono(transform.root.gameObject);
        p.velocidade = velocidadeProjetil;
        
        if (alvoAtual != null)
        {
            Vector3 direcao = (alvoAtual.position - pontoSaida.position).normalized;
            p.SetDirecao(direcao);
            
            p.SetAlvo(alvoAtual);
            p.curvaDePerseguicao = 90f; 
        }
        else
        {
            p.SetDirecao(pontoSaida.forward);
        }

        if (somDisparo != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(somDisparo, 0.8f);
        }

        return true;
    }

    private void IniciarReabastecimento()
    {
        if (!ehAresAr) return;
        reabastecendo = true;
        contadorReabastecimento = Mathf.Max(0.1f, tempoReabastecimento);
        Debug.Log("[Ares_Ar] Carregador vazio. Reabastecendo por " + contadorReabastecimento.ToString("0.0") + "s.", this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, alcanceArea);
    }
}
