using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(ControleAviao))]
public class ControleDroneHasaf : MonoBehaviour
{
    private ControleAviao controleAviao;
    private ControleUnidade controleUnidade;
    
    [Header("Mísseis")]
    public GameObject prefabMissil;
    public Transform[] pontosLancamento;
    public float tempoRecarga = 3f;
    private float cronometroRecarga = 0f;
    private int municaoAtual = 4;

    [Header("Patrulha e Seguir")]
    public float distanciaManterAlvo = 250f;
    public float raioPatrulha = 150f;
    private Transform alvoSeguir;

    void Start()
    {
        controleAviao = GetComponent<ControleAviao>();
        controleUnidade = GetComponent<ControleUnidade>();

        // 1. Remover Fumaça/Rastro para não atrapalhar a câmera
        RemoverFumaca();

        // Substituir o SistemaArmamentoHelice que só atira bala por este se existir
        var sistemaAntigo = GetComponent<SistemaArmamentoHelice>();
        if (sistemaAntigo != null)
        {
            sistemaAntigo.enabled = false;
        }
        
        // Ajuste de estabilidade e voo do ControleAviao
        controleAviao.raioOrbitaMissao = raioPatrulha;
        controleAviao.altitudeVoo = 90f; // Drone de vigia: altitude baixa e estável
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MissilePrefabAutoBinder.BindControleDroneHasaf(this);
    }

    [ContextMenu("Auto configurar missil")]
    private void AutoConfigurarMissilEditor()
    {
        MissilePrefabAutoBinder.BindControleDroneHasaf(this, true);
    }
#endif

    private void RemoverFumaca()
    {
        TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
        foreach (var t in trails)
        {
            t.enabled = false;
            Destroy(t);
        }

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in particles)
        {
            if (p.gameObject.name.ToLower().Contains("smoke") || 
                p.gameObject.name.ToLower().Contains("fuma") ||
                p.gameObject.name.ToLower().Contains("trail") ||
                p.gameObject.name.ToLower().Contains("rastro"))
            {
                p.Stop();
                p.gameObject.SetActive(false);
            }
        }
    }

    private static readonly List<IdentidadeUnidade> _bufferGlobaisDrone = new List<IdentidadeUnidade>(256);

    private Transform EscanearInimigoProximo(float raio)
    {
        IdentidadeUnidade meuId = GetComponent<IdentidadeUnidade>();
        int meuTime = meuId != null ? meuId.teamID : 1;

        _bufferGlobaisDrone.Clear();
        RegistroEntidadesJogo.FillUnidades(_bufferGlobaisDrone);

        Transform melhorTransform = null;
        float menorDistSqr = raio * raio;

        for (int i = 0; i < _bufferGlobaisDrone.Count; i++)
        {
            IdentidadeUnidade idAlvo = _bufferGlobaisDrone[i];
            if (idAlvo == null || idAlvo.teamID == meuTime) continue;
            if (!ControleSubmarino.PodeSerAlvoConvencional(idAlvo.transform)) continue;

            SistemaDeDanos alvoDanos = idAlvo.GetComponent<SistemaDeDanos>();
            if (alvoDanos == null || alvoDanos.vidaAtual <= 0) continue;

            float distSqr = (transform.position - idAlvo.transform.position).sqrMagnitude;
            if (distSqr < menorDistSqr)
            {
                menorDistSqr = distSqr;
                melhorTransform = idAlvo.transform;
            }
        }

        return melhorTransform;
    }

    void Update()
    {
        cronometroRecarga += Time.deltaTime;

        if (alvoSeguir != null)
        {
            // Seguir alvo
            if (alvoSeguir.gameObject.activeInHierarchy)
            {
                Vector3 alvoPlano = alvoSeguir.position;
                alvoPlano.y = transform.position.y;
                Vector3 direcao = (transform.position - alvoPlano).normalized;
                if (direcao.sqrMagnitude < 0.01f)
                {
                    direcao = -transform.forward;
                }
                controleAviao.alvoGPSVoo = alvoPlano + (direcao * distanciaManterAlvo);
            }
            else
            {
                alvoSeguir = null;
            }
        }
        else
        {
            // Autodisparo de patrulha contra o inimigo mais próximo em um raio de 600m
            Transform alvoInimigo = EscanearInimigoProximo(600f);
            if (alvoInimigo != null)
            {
                Vector3 alvoPlano = alvoInimigo.position;
                alvoPlano.y = transform.position.y;
                controleAviao.alvoGPSVoo = alvoPlano;
            }
        }
    }

    public void AtribuirAlvo(Transform novoAlvo)
    {
        alvoSeguir = novoAlvo;
        controleAviao.estadoAtual = ControleAviao.EstadoAviao.EmMissao;
        if (novoAlvo != null)
        {
            Vector3 foco = novoAlvo.position;
            foco.y = transform.position.y;
            controleAviao.alvoGPSVoo = foco;
        }
    }

    public void DispararMissil(Transform alvo)
    {
        if (cronometroRecarga >= tempoRecarga && municaoAtual > 0 && prefabMissil != null && pontosLancamento != null && pontosLancamento.Length > 0)
        {
            cronometroRecarga = 0f;
            municaoAtual--;
            
            int indiceCano = municaoAtual % pontosLancamento.Length;
            Transform saida = pontosLancamento[indiceCano];
            
            GameObject missilGO = Instantiate(prefabMissil, saida.position, saida.rotation);
            MisselTatico missil = missilGO.GetComponent<MisselTatico>();
            if (missil == null) missil = missilGO.AddComponent<MisselTatico>(); // Fallback
            
            missil.IniciarLancamento(alvo.position, alvo);
            
            // Notificar Menu Satélite
            if (MenuComandoController.Instancia != null)
            {
                MenuComandoController.Instancia.SendMessage("NotificarAtaqueDrone", "MÍSSIL LANÇADO CONTRA " + alvo.name, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
