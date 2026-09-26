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
    public float raioPatrulha = 400f;
    [Min(0f)] public float altitudeMinimaVoo = 60f;
    [Min(0f)] public float altitudeCruzeiro = 90f;
    private Transform alvoSeguir;

    private void Awake()
    {
        ConfigurarVoo();
    }

    void Start()
    {
        if (controleAviao == null) controleAviao = GetComponent<ControleAviao>();
        if (controleUnidade == null) controleUnidade = GetComponent<ControleUnidade>();

        // 1. Remover Fumaça/Rastro para não atrapalhar a câmera
        RemoverFumaca();

        // Substituir o SistemaArmamentoHelice que só atira bala por este se existir
        var sistemaAntigo = GetComponent<SistemaArmamentoHelice>();
        if (sistemaAntigo != null)
        {
            sistemaAntigo.enabled = false;
        }
        
        ConfigurarVoo();
    }

    private void ConfigurarVoo()
    {
        if (controleAviao == null) controleAviao = GetComponent<ControleAviao>();
        if (controleAviao == null) return;

        // O piso geral dos aviões militares é 181 m; o HASAF usa cruzeiro
        // baixo e estável e mantém seu próprio limite de segurança.
        controleAviao.raioOrbitaMissao = raioPatrulha;
        controleAviao.altitudeVoo = Mathf.Max(altitudeMinimaVoo, altitudeCruzeiro);
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

        // A ordem oficial é a única autoridade sobre o deslocamento. O drone
        // pode continuar procurando alvos quando está ocioso/patrulhando, mas
        // não pode substituir um destino manual, uma perseguição ou um ataque
        // já emitido pelo ControleUnidade.
        bool ordemDeMovimentoAtiva = controleUnidade != null && controleUnidade.PossuiOrdemMovimentoAtiva;
        bool patrulhaOficialAtiva = controleUnidade != null
            && controleUnidade.OrdemAtual == OrdemControleUnidade.Patrulhando;
        bool deslocamentoManualAtivo = ordemDeMovimentoAtiva
            && controleUnidade != null
            && !controleUnidade.ModoCombateAtivo;

        if (patrulhaOficialAtiva || deslocamentoManualAtivo)
        {
            // Uma patrulha/ordem manual nova substitui uma perseguição antiga.
            // O executor oficial da aeronave continua sendo o único que escolhe
            // o próximo waypoint.
            alvoSeguir = null;
        }

        if (alvoSeguir != null && !patrulhaOficialAtiva && !deslocamentoManualAtivo)
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
        else if (!ordemDeMovimentoAtiva)
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
        // Alterar somente o enum deixava o ControleAviao com
        // estaEmModoVooFisico=false; o drone aparecia como "em missão", mas
        // o Update principal não movia mais o objeto.
        controleAviao.DefinirEstado(ControleAviao.EstadoAviao.EmMissao);
        controleAviao.estaEmModoVooFisico = true;
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
