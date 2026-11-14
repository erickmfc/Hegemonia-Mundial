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
        GameObject prefabMarcador = PoolDeObjetosCombate.ResolverMarcadorPatrulhaAerea();
        if (prefabMarcador == null)
        {
            return;
        }

        marcadorArea = PoolDeObjetosCombate.Spawn(prefabMarcador, new Vector3(pos.x, pos.y + 0.1f, pos.z), Quaternion.identity);
        if (marcadorArea == null)
        {
            return;
        }

        float escala = Mathf.Max(12f, raioExplosao * 0.8f);
        marcadorArea.transform.localScale = new Vector3(escala, escala, escala);

        ParticleSystem[] particulas = marcadorArea.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particulas.Length; i++)
        {
            ParticleSystem ps = particulas[i];
            if (ps == null) continue;
            ps.Clear(true);
            ps.Play(true);
        }
    }

    void OnDestroy()
    {
        if (marcadorArea != null)
        {
            PoolDeObjetosCombate.Release(marcadorArea);
            marcadorArea = null;
        }
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
                // O próprio drone é a origem da explosão; informar a autoria
                // evita inferir um agressor varrendo unidades próximas.
                sd.ReceberDano(danoImpacto * fatorDano, gameObject);
            }
        }

        // O Drone se destrói
        Destroy(gameObject);
    }
}
