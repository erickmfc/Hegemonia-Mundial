using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SistemaDeDanos))]
public class ControleAviaoCaca : MonoBehaviour
{
    [Header("Status de Voo")]
    public float altitudeCruzeiro = 40f; 
    [Tooltip("Altura para recolher o trem de pouso e considerar 'Voando'.")]
    public float alturaDecolagem = 15f; 
    
    [Header("Velocidades")]
    public float velocidadeTaxi = 10f;
    public float velocidadeCruzeiro = 40f;
    public float velocidadeAtaque = 80f;
    public float velocidadeCurva = 1.0f;

    [Header("Combustão e Efeitos")]
    public List<ParticleSystem> fogoNosMotores;
    public Light luzPosCombustao;

    [Header("Trem de Pouso")]
    [Tooltip("Objetos das rodas que vão girar.")]
    public List<Transform> rodas; 
    [Tooltip("Eixo de rotação para recolher (X, Y ou Z). Geralmente X.")]
    public Vector3 eixoRecolher = Vector3.right; 
    public float anguloRecolher = 90f; 
    public float velocidadeTremPouso = 2f;

    [Header("Efeitos Sonoros")]
    [Tooltip("Som reproduzido quando o avião passa perto da câmera num rasante (Flyby).")]
    public AudioClip somPassagem;
    [Tooltip("Distância máxima da câmera para tocar o som de passagem.")]
    public float distanciaAtivacaoSom = 100f;
    private AudioSource audioSourcePassagem;
    private Transform cameraTransform;
    private float proximoSomPassagem = 0f;
    private Vector3 ultimaPosicaoSomPassagem;

    public enum EstadoVoo { NoChao, Decolando, Voando, Pousando }
    [SerializeField]
    private EstadoVoo estadoAtual = EstadoVoo.NoChao;

    private float velocidadeAtual = 0f;
    public float VelocidadeAtual => velocidadeAtual;
    private Vector3 destinoAtual;
    public Vector3 DestinoAtual => destinoAtual;
    private bool temDestino = false;
    
    private float fatorRodas = 0f;
    private List<Quaternion> rotacoesOriginaisRodas = new List<Quaternion>();

    // --- CACHE DE COMPONENTES (evita GetComponent repetido no Update) ---
    private ControleUnidade _controleUnidade;
    private SistemaDeTiro _sistemaTiro;
    private ControleAviao _controleAviaoModerno;
    private bool _temControleModerno = false;

    // --- IDENTIFICAÇÃO (CRISTAL) ---
    public Color corIdentificacao;
    private GameObject cristalIdentificacao;

    // --- CACHE: distância ao quadrado para flyby (evita sqrt) ---
    private float _distAtivacaoSomSqr;
    private SistemaDeDanos _sistemaDanos;
    private float _tempoBoostDefensivoAte = 0f;

    [Header("Comportamento de Velocidade")]
    [Tooltip("Distância até o destino a partir da qual o caça começa a reduzir para cruzeiro.")]
    public float distanciaTransicaoCruzeiro = 140f;
    [Tooltip("Tempo em segundos usando velocidade máxima após receber dano.")]
    public float duracaoBoostDefensivo = 4f;

    void Start()
    {
        _controleUnidade = GetComponent<ControleUnidade>();
        _sistemaTiro = GetComponentInChildren<SistemaDeTiro>();
        _sistemaDanos = GetComponent<SistemaDeDanos>();
        _controleAviaoModerno = GetComponent<ControleAviao>();
        _temControleModerno = (_controleAviaoModerno != null);

        if (_sistemaDanos != null)
        {
            _sistemaDanos.OnDano += RegistrarDanoRecebido;
        }
        
        destinoAtual = transform.position;

        // Pré-calcula distâncias ao quadrado (evita sqrt no Update)
        _distAtivacaoSomSqr = distanciaAtivacaoSom * distanciaAtivacaoSom;

        // --- GERAR CRISTAL ÚNICO ---
        corIdentificacao = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
        cristalIdentificacao = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(cristalIdentificacao.GetComponent<Collider>());
        cristalIdentificacao.name = "CristalIdentificacao";
        cristalIdentificacao.transform.SetParent(this.transform);
        cristalIdentificacao.transform.localPosition = new Vector3(0, 2.5f, 0);
        cristalIdentificacao.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        cristalIdentificacao.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        
        Renderer rend = cristalIdentificacao.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = corIdentificacao;
        }

        // Salva rotação original das rodas
        for (int i = 0, count = rodas.Count; i < count; i++)
        {
            if (rodas[i] != null) rotacoesOriginaisRodas.Add(rodas[i].localRotation);
        }

        ControlarEfeitosMotor(false);

        // Inicializa som de passagem
        if (Camera.main != null) cameraTransform = Camera.main.transform;
        
        GameObject objSom = new GameObject("SomPassagem");
        objSom.transform.SetParent(transform);
        objSom.transform.localPosition = Vector3.zero;
        audioSourcePassagem = objSom.AddComponent<AudioSource>();
        audioSourcePassagem.spatialBlend = 1f;
        audioSourcePassagem.minDistance = 9f;
        audioSourcePassagem.maxDistance = 150f;
        audioSourcePassagem.playOnAwake = false;
        audioSourcePassagem.clip = somPassagem;
        audioSourcePassagem.loop = false;
        ultimaPosicaoSomPassagem = transform.position;
    }

    void OnDestroy()
    {
        if (_sistemaDanos != null)
        {
            _sistemaDanos.OnDano -= RegistrarDanoRecebido;
        }
    }

    void Update()
    {
        // Animação do Cristal
        if (cristalIdentificacao != null)
            cristalIdentificacao.transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World);


        // Se o ControleAviao (Moderno) existir, desliga TUDO do script velho — o moderno assume
        if (_temControleModerno) return;

        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        AtualizarLogicaEstado();
        MoverAviao();
        AnimarTremDePouso();
        AtualizarSomPassagem();

        // Decolagem automática se tem destino longe
        if (estadoAtual == EstadoVoo.NoChao && temDestino)
        {
            float distSqr = (transform.position - destinoAtual).sqrMagnitude;
            if (distSqr > 2500f) // 50² = 2500
                IniciarDecolagem();
        }

    }

    public void DefinirDestino(Vector3 novoDestino)
    {
        if (!CombustivelUnidade.PodeOperarObjeto(gameObject))
        {
            PararPorFaltaDeCombustivel();
            return;
        }

        ControleUnidade controleUnidade = GetComponent<ControleUnidade>()
            ?? GetComponentInParent<ControleUnidade>();
        bool ordemCentralAtiva = controleUnidade != null
            && controleUnidade.PossuiOrdemMovimentoAtiva
            && controleUnidade.OrdemMovimentoAtual != null
            && controleUnidade.OrdemMovimentoAtual.Dono == nameof(ControleUnidade)
            && Vector3.Distance(controleUnidade.OrdemMovimentoAtual.Destino, novoDestino) <= 0.01f;
        if (!ordemCentralAtiva && controleUnidade != null)
        {
            string idOrdem = controleUnidade.ObterOuCriarIdOrdemMovimento(
                "aereo",
                novoDestino,
                TipoOrdemMovimento.Aerea);
            if (!controleUnidade.RegistrarOrdemMovimentoExterna(
                    idOrdem,
                    nameof(ControleAviaoCaca),
                    novoDestino,
                    TipoOrdemMovimento.Aerea))
            {
                return;
            }
        }

        destinoAtual = novoDestino;
        if (estadoAtual == EstadoVoo.Voando)
            destinoAtual.y = altitudeCruzeiro;
        temDestino = true;
    }

    public void PararPorFaltaDeCombustivel()
    {
        if (estadoAtual != EstadoVoo.NoChao && transform.position.y > 5f)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            SistemaDeDanos danos = GetComponent<SistemaDeDanos>();
            FalhaAereaFisica.Ativar(gameObject, rb, Mathf.Max(velocidadeCruzeiro, velocidadeAtaque) * 0.8f, 4.5f, false, danos);
            temDestino = false;
            ControlarEfeitosMotor(false);
            return;
        }

        temDestino = false;
        velocidadeAtual = 0f;
        ControlarEfeitosMotor(false);
    }

    void AtualizarLogicaEstado()
    {
        float alturaDoChao = transform.position.y;
        float dt = Time.deltaTime;
        
        switch (estadoAtual)
        {
            case EstadoVoo.NoChao:
                if (alturaDoChao > alturaDecolagem + 5f)
                {
                    estadoAtual = EstadoVoo.Voando;
                    break;
                }
                velocidadeAtual = Mathf.Lerp(velocidadeAtual, 0f, dt);
                ControlarEfeitosMotor(false);
                break;

            case EstadoVoo.Decolando:
                velocidadeAtual = Mathf.Lerp(velocidadeAtual, ObterVelocidadeAlvoVoo(), dt * 0.5f);
                ControlarEfeitosMotor(true);
                if (alturaDoChao > alturaDecolagem)
                {
                    estadoAtual = EstadoVoo.Voando;
                    Debug.Log("✈️ [F_C19] Decolagem concluída! Entrando em voo de cruzeiro.");
                }
                break;

            case EstadoVoo.Voando:
                velocidadeAtual = Mathf.Lerp(velocidadeAtual, ObterVelocidadeAlvoVoo(), dt);
                ControlarEfeitosMotor(true);
                break;
                
            case EstadoVoo.Pousando:
                velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeTaxi, dt * 0.2f);
                ControlarEfeitosMotor(false);
                if (alturaDoChao < 2f)
                {
                    estadoAtual = EstadoVoo.NoChao;
                    Debug.Log("🛬 [F_C19] Pouso confirmado.");
                }
                break;
        }
    }

    private float ObterVelocidadeAlvoVoo()
    {
        if (Time.time < _tempoBoostDefensivoAte)
        {
            return velocidadeAtaque;
        }

        bool emCombate = (_sistemaTiro != null && !_sistemaTiro.modoPassivo);
        if (emCombate)
        {
            return velocidadeAtaque;
        }

        if (temDestino)
        {
            float distDestino = Vector3.Distance(transform.position, destinoAtual);
            if (distDestino > distanciaTransicaoCruzeiro)
            {
                return velocidadeAtaque;
            }
        }

        return velocidadeCruzeiro;
    }

    private void RegistrarDanoRecebido()
    {
        _tempoBoostDefensivoAte = Time.time + duracaoBoostDefensivo;
    }

    void MoverAviao()
    {
        if (estadoAtual == EstadoVoo.NoChao) return;

        float dt = Time.deltaTime;

        if (estadoAtual == EstadoVoo.Voando || estadoAtual == EstadoVoo.Decolando)
        {
            if (temDestino)
            {
                Vector3 vetorParaDestino = destinoAtual - transform.position;
                
                if (vetorParaDestino.sqrMagnitude < 10000f && estadoAtual == EstadoVoo.Voando) // 100² = 10000
                {
                    destinoAtual = transform.position + (transform.right * 200f) + (transform.forward * 100f);
                    vetorParaDestino = destinoAtual - transform.position;
                }
                
                if (vetorParaDestino.sqrMagnitude > 0.1f)
                {
                    Vector3 upRef = Mathf.Abs(Vector3.Dot(vetorParaDestino.normalized, Vector3.up)) > 0.99f ? transform.up : Vector3.up;
                    Quaternion rotacaoAlvo = Quaternion.LookRotation(vetorParaDestino, upRef);
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, dt * velocidadeCurva);
                }
            }
            
            // Ajuste suave de altura (Fly-by-wire)
            float erroAltura = altitudeCruzeiro - transform.position.y;
            Vector3 pos = transform.position;
            pos.y += erroAltura * dt * 0.5f;
            transform.position = pos;
        }
        else if (estadoAtual == EstadoVoo.Pousando)
        {
            if (temDestino)
            {
                Vector3 dir = (destinoAtual - transform.position);
                if (dir.sqrMagnitude > 0.1f)
                {
                    Vector3 upRef = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f ? transform.up : Vector3.up;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, upRef), dt);
                }
            }
            transform.Translate(Vector3.down * 5f * dt, Space.World);
        }

        transform.Translate(Vector3.forward * velocidadeAtual * dt);
    }

    void IniciarDecolagem()
    {
        if (estadoAtual != EstadoVoo.NoChao) return;
        
        Debug.Log("🛫 [F_C19] Iniciando sequência de decolagem...");
        estadoAtual = EstadoVoo.Decolando;
        
        if (!temDestino)
        {
            destinoAtual = transform.position + transform.forward * 1000f;
            destinoAtual.y = altitudeCruzeiro;
            temDestino = true;
        }
    }

    public void SolicitarPouso(Vector3 pistaPosicao)
    {
        Debug.Log("🛬 [F_C19] Recebido comando de pouso.");
        estadoAtual = EstadoVoo.Pousando;
        destinoAtual = pistaPosicao;
        temDestino = true;
    }

    void AtualizarSomPassagem()
    {
        if (somPassagem == null || audioSourcePassagem == null)
        {
            return;
        }

        AtualizarReferenciaOuvinte();
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 deslocamento = transform.position - ultimaPosicaoSomPassagem;
        float velocidadeAproximada = deslocamento.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        ultimaPosicaoSomPassagem = transform.position;

        float distCamSqr = (transform.position - cameraTransform.position).sqrMagnitude;
        bool pertoDaCamera = distCamSqr < _distAtivacaoSomSqr;
        bool emMovimentoRapido = velocidadeAproximada > 18f;
        bool emAltitudeDeVoo = transform.position.y > 8f;

        if (pertoDaCamera && emMovimentoRapido && emAltitudeDeVoo && Time.time >= proximoSomPassagem)
        {
            TocarSomPassagem();
            proximoSomPassagem = Time.time + 1.6f;
        }
    }

    private void AtualizarReferenciaOuvinte()
    {
        if (cameraTransform != null)
        {
            return;
        }

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            return;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam != null && cam.enabled)
            {
                cameraTransform = cam.transform;
                return;
            }
        }

        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener != null)
        {
            cameraTransform = listener.transform;
        }
    }

    void TocarSomPassagem()
    {
        if (audioSourcePassagem == null || somPassagem == null) return;
        audioSourcePassagem.Stop();
        audioSourcePassagem.spatialBlend = 1f;
        audioSourcePassagem.PlayOneShot(somPassagem, 1f);
    }

    void AnimarTremDePouso()
    {
        float meta = (estadoAtual == EstadoVoo.Voando || estadoAtual == EstadoVoo.Decolando) ? 1f : 0f;
        if (estadoAtual == EstadoVoo.Decolando && transform.position.y < alturaDecolagem) meta = 0f;

        fatorRodas = Mathf.MoveTowards(fatorRodas, meta, Time.deltaTime * velocidadeTremPouso);

        for (int i = 0, count = rodas.Count; i < count; i++)
        {
            if (rodas[i] == null || i >= rotacoesOriginaisRodas.Count) continue;
            
            Quaternion rotOriginal = rotacoesOriginaisRodas[i];
            Quaternion rotRecolhida = rotOriginal * Quaternion.Euler(eixoRecolher * anguloRecolher);
            rodas[i].localRotation = Quaternion.Slerp(rotOriginal, rotRecolhida, fatorRodas);
        }
    }

    public string ObterEstadoTexto()
    {
        switch (estadoAtual)
        {
            case EstadoVoo.NoChao: return "No Chão";
            case EstadoVoo.Decolando: return "Decolando";
            case EstadoVoo.Voando: return "Em Voo";
            case EstadoVoo.Pousando: return "Pousando";
            default: return "Desconhecido";
        }
    }

    void ControlarEfeitosMotor(bool ligado)
    {
        for (int i = 0, count = fogoNosMotores.Count; i < count; i++)
        {
            ParticleSystem ps = fogoNosMotores[i];
            if (ps == null) continue;
            if (ligado && !ps.isPlaying) ps.Play();
            else if (!ligado && ps.isPlaying) ps.Stop();
        }
        
        if (luzPosCombustao != null)
            luzPosCombustao.enabled = ligado;
    }
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destinoAtual);
        Gizmos.DrawWireSphere(destinoAtual, 2f);
    }
}
