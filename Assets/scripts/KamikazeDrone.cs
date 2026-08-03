using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class KamikazeDrone : MonoBehaviour
{
    [Header("Configurações do Drone")]
    public float velocidadeBusca = 60f;
    public float velocidadeAtaque = 120f;
    public float raioExplosao = 15f;
    public float danoImpacto = 500f;
    
    [Header("Efeitos Visuais e Sonoros")]
    public GameObject efeitoExplosao;
    public float escalaEfeitoExplosao = 1f;
    public AudioClip somExplosao;
    [Range(0f, 1f)] public float volumeExplosao = 1f;

    [Header("Alvo")]
    public Transform alvoAtual;
    public bool kamikazeAtivo = false;

    private ControleAviao _controleAviao;
    private SistemaDeDanos _sistemaDanos;
    private bool _deteveAlvo = false;
    
    private GameObject marcadorArea;
    private bool marcadorCriado = false;

    void Start()
    {
        _controleAviao = GetComponent<ControleAviao>();
        _sistemaDanos = GetComponent<SistemaDeDanos>();

        // Drones kamikaze são mais lentos em voo normal mas rápidos no mergulho
        if (_controleAviao != null)
        {
            _controleAviao.velocidadeMaximaVoo = velocidadeBusca;
        }
    }

    void Update()
    {
        if (_controleAviao == null) return;

        // Só ativa a lógica de busca/ataque se estiver voando (ou indo pra missão)
        if (_controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao || 
            _controleAviao.estadoAtual == ControleAviao.EstadoAviao.Decolando)
        {
            if (!marcadorCriado && _controleAviao.alvoEstrategico != Vector3.zero)
            {
                CriarMarcadorDeArea(_controleAviao.alvoEstrategico);
                marcadorCriado = true;
            }

            // O Kamikaze só inicia as manobras e o mergulho de ataque quando já estiver no ar (EmMissao)
            if (_controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
            {
                if (!kamikazeAtivo)
                {
                    ProcurarAlvo();
                }
                else
                {
                    ExecutarAtaqueKamikaze();
                }
            }
        }
    }

    void CriarMarcadorDeArea(Vector3 pos)
    {
        marcadorArea = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(marcadorArea.GetComponent<Collider>());
        
        marcadorArea.transform.position = new Vector3(pos.x, pos.y + 0.1f, pos.z);
        marcadorArea.transform.localScale = new Vector3(raioExplosao * 2f, 0.05f, raioExplosao * 2f);
        
        Renderer rend = marcadorArea.GetComponent<Renderer>();
        if (rend != null)
        {
            // Usa shader default do Unity p/ suportar cor com alpha
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = new Color(1f, 0.1f, 0f, 0.2f);
        }
    }

    void OnDestroy()
    {
        if (marcadorArea != null) Destroy(marcadorArea);
    }

    void ProcurarAlvo()
    {
        // Verifica a distância horizontal (ignora altura neste momento) até o alvoEstrategico
        Vector3 diff = new Vector3(transform.position.x - _controleAviao.alvoEstrategico.x, 0, transform.position.z - _controleAviao.alvoEstrategico.z);
        
        // Se estiver num raio de 350m (distância boa para começar mergulho na velocidade de avião)
        if (diff.sqrMagnitude < 122500f) // 350 * 350 = 122500
        {
            kamikazeAtivo = true;
            _controleAviao.velocidadeMaximaVoo = velocidadeAtaque;
        }
        else if (_controleAviao.emAtaqueMergulho) // Fallback caso o mergulho seja forçado
        {
            kamikazeAtivo = true;
            _controleAviao.velocidadeMaximaVoo = velocidadeAtaque;
        }
    }

    void ExecutarAtaqueKamikaze()
    {
        // O ControleAviao já está movendo o drone para alvoGPSVoo
        // Verificamos a distância para detonar
        float distSqr = (transform.position - _controleAviao.alvoGPSVoo).sqrMagnitude;
        
        if (distSqr < 25f) // 5 metros de distância
        {
            Detonar();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Se bater em qualquer coisa enquanto estiver em missão, detona!
        if (_controleAviao != null && _controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao)
        {
            Detonar();
        }
    }

    public void Detonar()
    {
        if (_deteveAlvo) return;
        _deteveAlvo = true;

        Debug.Log($"[Kamikaze] Detonando drone em {transform.position}");

        if (somExplosao != null)
        {
            // Toca um som independente que continua rolando mesmo após o drone ser apagado
            AudioRuntime.PlayClipAtPoint(somExplosao, transform.position, volumeExplosao);
        }

        if (efeitoExplosao != null)
        {
            GameObject fx = Instantiate(efeitoExplosao, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * escalaEfeitoExplosao;
        }

        // Causa dano em área
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, raioExplosao);
        foreach (var hitCollider in hitColliders)
        {
            SistemaDeDanos sd = hitCollider.GetComponentInParent<SistemaDeDanos>();
            if (sd != null)
            {
                // Calcula dano baseado na distância (opcional)
                float dist = Vector3.Distance(transform.position, hitCollider.transform.position);
                float fatorDano = Mathf.Clamp01(1f - (dist / raioExplosao));
                sd.ReceberDano(danoImpacto * fatorDano);
            }
        }

        // O Drone se destrói
        Destroy(gameObject);
    }
}
