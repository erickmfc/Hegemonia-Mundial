using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TorretaAntiaerea : MonoBehaviour
{
    [Header("Configurações do Radar (Aéreo)")]
    [Tooltip("Distância máxima que a torreta consegue enxergar as ameaças aéreas")]
    public float alcanceArea = 150f;
    
    [Tooltip("Altura mínima no eixo Y para considerar que o alvo está voando (evita atirar em tropas no chão)")]
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

    // Variáveis internas state
    private Transform alvoAtual;
    private IdentidadeUnidade minhaIdentidade;
    private bool atirando = false;
    private AudioSource audioSource;
    private int indexPontoDisparo = 0;
    private Collider[] _bufferOverlaps = new Collider[96];
    private static readonly List<IdentidadeUnidade> unidadesRegistroRadar = new List<IdentidadeUnidade>(256);
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
        // Procura ou cria a identidade do time para não atirar nos próprios aviões
        minhaIdentidade = GetComponentInParent<IdentidadeUnidade>();
        if (minhaIdentidade == null)
        {
            minhaIdentidade = gameObject.AddComponent<IdentidadeUnidade>();
            minhaIdentidade.teamID = 1; // 1 = Time do Jogador por padrão
        }

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
        AudioRuntime.ConfigurarFonteDeArmamento(audioSource);

        // Inicia varredura do céu a cada 0.5s para economizar processamento
        InvokeRepeating("ProcurarAlvoAereo", Random.Range(0f, 0.5f), 0.5f);
    }

    void Update()
    {
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
        // Se já tem um alvo válido dentro da área, mantém ele
        if (alvoAtual != null && alvoAtual.gameObject.activeInHierarchy)
        {
            if (Vector3.Distance(transform.position, alvoAtual.position) <= alcanceArea)
                return; 
        }

        alvoAtual = null;

        // NonAlloc: evita alocacao/GC quando houver muitos coliders (ex.: misseis no ar).
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, alcanceArea, _bufferOverlaps, ~0, QueryTriggerInteraction.UseGlobal);
        if (hitCount >= _bufferOverlaps.Length)
        {
            // Cresce o buffer (sem fazer isso todo frame) caso a area esteja muito densa.
            _bufferOverlaps = new Collider[Mathf.Min(_bufferOverlaps.Length * 2, 1024)];
            hitCount = Physics.OverlapSphereNonAlloc(transform.position, alcanceArea, _bufferOverlaps, ~0, QueryTriggerInteraction.UseGlobal);
        }

        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _bufferOverlaps[i];
            if (hit == null) continue;

            // Ignora projeteis/misseis para nao inflar o radar antiaereo.
            if (hit.GetComponentInParent<Projetil>() != null) continue;
            if (hit.GetComponentInParent<MisselCaca>() != null) continue;
            if (hit.GetComponentInParent<MisselNaval>() != null) continue;
            if (hit.GetComponentInParent<MisselSubmarino>() != null) continue;
            if (hit.GetComponentInParent<MissilTeleguiado>() != null) continue;

            // Busca IdentidadeUnidade (Componente que define time)
            IdentidadeUnidade idAlvo = hit.GetComponent<IdentidadeUnidade>();
            if (idAlvo == null) idAlvo = hit.GetComponentInParent<IdentidadeUnidade>();

            // Filtro Principal: É uma aeronave? 
            bool ehAereo = hit.GetComponentInParent<Helicoptero>() != null || 
                           hit.GetComponentInParent<ControleAviao>() != null ||
                           (idAlvo != null && idAlvo.tipoUnidade == TipoUnidade.Aereo);

            if (!ehAereo)
            {
                string nm = hit.name;
                ehAereo = nm.IndexOf("aviao", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          nm.IndexOf("heli", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          nm.IndexOf("caca", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          nm.IndexOf("caça", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          nm.IndexOf("jato", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          nm.IndexOf("drone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          nm.IndexOf("vap", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          nm.IndexOf("bombard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                          nm.IndexOf("b260", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (!ehAereo) continue; // Pula unidades terrestres e prédios altos

            // Filtro Secundário: Fogo Amigo
            if (idAlvo != null && idAlvo.teamID != minhaIdentidade.teamID && idAlvo.teamID != 0)
            {
                Transform alvoPrincipal = ResolverTransformPrincipal(hit.transform);
                float dist = Vector3.Distance(transform.position, alvoPrincipal.position);
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    melhorAlvo = alvoPrincipal;
                }
            }
        }

        if (melhorAlvo == null)
        {
            melhorAlvo = ProcurarAlvoAereoNoRegistroGlobal();
        }

        if (melhorAlvo != null)
        {
            alvoAtual = melhorAlvo;
        }
    }

    private Transform ProcurarAlvoAereoNoRegistroGlobal()
    {
        RegistroEntidadesJogo.FillUnidades(unidadesRegistroRadar);
        float alcanceSqr = alcanceArea * alcanceArea;
        float menorDistancia = Mathf.Infinity;
        Transform melhorAlvo = null;

        for (int i = 0; i < unidadesRegistroRadar.Count; i++)
        {
            IdentidadeUnidade idAlvo = unidadesRegistroRadar[i];
            if (idAlvo == null || !idAlvo.gameObject.activeInHierarchy) continue;
            if (idAlvo.teamID == 0 || idAlvo.teamID == minhaIdentidade.teamID) continue;

            Transform alvoPrincipal = ResolverTransformPrincipal(idAlvo.transform);
            if (alvoPrincipal == null || alvoPrincipal.root == transform.root) continue;

            bool ehAereo = idAlvo.tipoUnidade == TipoUnidade.Aereo
                || alvoPrincipal.position.y >= alturaMinimaVoo
                || alvoPrincipal.GetComponentInParent<Helicoptero>() != null
                || alvoPrincipal.GetComponentInParent<ControleAviao>() != null
                || alvoPrincipal.GetComponentInParent<ControleAviaoCaca>() != null
                || alvoPrincipal.GetComponentInParent<C700TransporteAereo>() != null;

            if (!ehAereo) continue;

            float distSqr = (alvoPrincipal.position - transform.position).sqrMagnitude;
            if (distSqr > alcanceSqr || distSqr >= menorDistancia) continue;

            menorDistancia = distSqr;
            melhorAlvo = alvoPrincipal;
        }

        unidadesRegistroRadar.Clear();
        return melhorAlvo;
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
            RaycastHit hit;
            // Um raio curto (35m) do cano pra frente pra ver se bate no nosso casco/heliponto
            if (Physics.Raycast(pontosDeDisparo[0].position, pontosDeDisparo[0].forward, out hit, 35f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                // Se a bala for bater num objeto com a mesma RAIZ que a torreta (o nosso navio)
                if (hit.transform.root == transform.root)
                {
                    return false; // Trava o tiro
                }
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

            DispararMunicoes();
            disparosFeitos++;
            
            yield return new WaitForSeconds(tempoEntreTiros);
        }

        yield return new WaitForSeconds(tempoPausaRajada);
        atirando = false;
    }

    void DispararMunicoes()
    {
        if (prefabProjetil == null) return;

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
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, alcanceArea);
    }
}
