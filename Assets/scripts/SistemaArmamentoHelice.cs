using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ControleAviao))]
public class SistemaArmamentoHelice : MonoBehaviour
{
    [Header("=== ANIMAÇÃO DA HÉLICE ===")]
    [Tooltip("Arraste o objeto 'Helice' aqui.")]
    public Transform helice;
    public float velocidadeGiroVoo = 2500f;
    public float velocidadeGiroChao = 800f;
    [Tooltip("Eixo de rotação da hélice (Geralmente Z ou Y).")]
    public Vector3 eixoGiro = Vector3.forward;

    [Header("=== COMBATE FRONTAL ===")]
    [Tooltip("Prefab do projetil traçante (Tiro genérico).")]
    public GameObject prefabProjetilTrassante;
    
    [Tooltip("Arraste os objetos 'Tiro' que ficam dentro das asas aqui.")]
    public Transform[] canosDeTiro; 
    
    public float cadenciaDeTiro = 0.12f; 
    public float raioDeVisao = 600f; // Aumentei um pouco para ele ver de mais longe
    
    [Header("=== MUNIÇÃO E RECARGA ===")]
    public int cartuchoMaximo = 100;
    public float tempoRecarga = 5f;
    private int balasAtuais;
    private bool recarregando = false;
    private float cronometroTiro = 0f;

    // --- VARIÁVEIS DE CONTROLE INTERNO ---
    private ControleAviao controleAviao;
    private int meuTime = 1;
    private float cronometroScan = 0f;
    private Transform alvoAtualGuardado;
    private readonly List<Transform> bufferPercepcaoTatica = new List<Transform>(12);

    // === NOVA MÁQUINA DE ESTADOS DO SUPER TUCANO ===
    private enum EstadoCombate { Patrulha, Afastamento, Mergulho_Subir, Mergulho_Atacar, Evasao }
    private EstadoCombate estadoAtualAtaque = EstadoCombate.Patrulha;
    
    private Vector3 pontoManobraFixo;
    private float cronometroEvasao = 0f;

    void Start()
    {
        balasAtuais = cartuchoMaximo;
        controleAviao = GetComponent<ControleAviao>();
        PoolDeObjetosCombate.Prewarm(prefabProjetilTrassante, Mathf.Clamp(canosDeTiro != null ? canosDeTiro.Length * 2 : 4, 4, 10));
        
        IdentidadeUnidade id = GetComponent<IdentidadeUnidade>();
        if (id != null) meuTime = id.teamID;

        // AUTO-DETECÇÃO DA HÉLICE
        if (helice == null)
        {
            Transform[] filhos = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in filhos)
            {
                if (t.name.ToLower().Contains("helice") || t.name.ToLower().Contains("propeller"))
                {
                    helice = t;
                    break;
                }
            }
        }

        // AUTO-DETECÇÃO DOS CANOS DE TIRO
        if (canosDeTiro == null || canosDeTiro.Length == 0)
        {
            List<Transform> canos = new List<Transform>();
            Transform[] filhos = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in filhos)
            {
                if (t.name.ToLower().StartsWith("tiro")) canos.Add(t);
            }
            canosDeTiro = canos.ToArray();
        }
    }

    void Update()
    {
        // 1. ANIMAÇÃO DA HÉLICE (Sempre roda)
        if (helice != null)
        {
            bool noAr = (controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao || 
                         controleAviao.estadoAtual == ControleAviao.EstadoAviao.Decolando);
            float velReal = noAr ? velocidadeGiroVoo : velocidadeGiroChao;
            helice.Rotate(eixoGiro * velReal * Time.deltaTime, Space.Self);
        }

        // Se não estiver voando em missão, não faz lógica de tiro
        if (controleAviao.estadoAtual != ControleAviao.EstadoAviao.EmMissao) return;

        // Controle de recarga
        if (recarregando)
        {
            cronometroTiro -= Time.deltaTime;
            if (cronometroTiro <= 0)
            {
                recarregando = false;
                balasAtuais = cartuchoMaximo;
            }
        }
        else if (cronometroTiro > 0)
        {
            cronometroTiro -= Time.deltaTime;
        }

        // 2. RADAR: Escaneia inimigos 4 vezes por segundo
        cronometroScan -= Time.deltaTime;
        if (cronometroScan <= 0f)
        {
            EscanearAreaAoRedorLento();
            cronometroScan = 0.25f; 
        }

        // 3. EXECUTA A MANOBRA DO SUPER TUCANO
        ExecutarManobrasDeCombate();
    }

    void EscanearAreaAoRedorLento()
    {
        // Se já temos um alvo e ele está vivo, não precisa procurar outro agora
        if (alvoAtualGuardado != null && alvoAtualGuardado.gameObject.activeInHierarchy)
        {
            if (!ControleSubmarino.PodeSerAlvoConvencional(alvoAtualGuardado))
            {
                alvoAtualGuardado = null;
            }
            else
            {
                SistemaDeDanos vidaAtual = alvoAtualGuardado.GetComponentInParent<SistemaDeDanos>();
                if (vidaAtual != null && vidaAtual.vidaAtual > 0) return;
            }
        }

        Transform alvoMaisProximoScanner = null;
        float menorDistanciaSqr = Mathf.Infinity;

        InfraPerformanceGameplay.ObterInimigosProximos(transform.position, raioDeVisao, meuTime, bufferPercepcaoTatica, 12);

        for (int i = 0; i < bufferPercepcaoTatica.Count; i++)
        {
            Transform candidato = bufferPercepcaoTatica[i];
            if (candidato == null)
            {
                continue;
            }

            SistemaDeDanos vida = candidato.GetComponentInParent<SistemaDeDanos>();
            if (vida != null && vida.vidaAtual > 0)
            {
                if (!ControleSubmarino.PodeSerAlvoConvencional(vida.transform)) continue;

                float distSqr = (transform.position - vida.transform.position).sqrMagnitude;
                if (distSqr < menorDistanciaSqr)
                {
                    menorDistanciaSqr = distSqr;
                    alvoMaisProximoScanner = vida.transform;
                }
            }
        }

        if (alvoMaisProximoScanner != alvoAtualGuardado)
        {
            alvoAtualGuardado = alvoMaisProximoScanner;
            if (alvoAtualGuardado != null)
            {
                // Sempre reinicia a sequencia quando um novo alvo aparece para evitar ficar preso em patrulha.
                estadoAtualAtaque = EstadoCombate.Patrulha;
                cronometroEvasao = 0f;
            }
        }
    }

    void ExecutarManobrasDeCombate()
    {
        if (alvoAtualGuardado != null && !ControleSubmarino.PodeSerAlvoConvencional(alvoAtualGuardado))
        {
            alvoAtualGuardado = null;
        }

        if (alvoAtualGuardado == null) 
        {
            estadoAtualAtaque = EstadoCombate.Patrulha;
            if (controleAviao != null) controleAviao.alvoPrioritarioIA = false;
            return;
        }

        controleAviao.alvoPrioritarioIA = true;
        Vector3 posicaoAlvo = alvoAtualGuardado.position;
        float distanciaDoAlvo = Vector3.Distance(transform.position, posicaoAlvo);
        float alturaDoAviao = transform.position.y;

        switch (estadoAtualAtaque)
        {
            case EstadoCombate.Patrulha:
                estadoAtualAtaque = EstadoCombate.Afastamento;
                CalcularPontoAfastamento(posicaoAlvo);
                break;

            case EstadoCombate.Afastamento:
                controleAviao.alvoGPSVoo = pontoManobraFixo;
                float distManobra = Vector3.Distance(transform.position, pontoManobraFixo);
                if (distManobra < 100f || (distanciaDoAlvo > 450f && alturaDoAviao > 150f))
                {
                    estadoAtualAtaque = EstadoCombate.Mergulho_Subir;
                }
                break;

            case EstadoCombate.Mergulho_Subir:
                // Sobe muito e ganha distância para o ataque Thunderbolt
                Vector3 pontoCeu = transform.position + transform.forward * 100f;
                pontoCeu.y = 200f; // Teto de ataque
                controleAviao.alvoGPSVoo = pontoCeu;

                if (alturaDoAviao >= 180f || distanciaDoAlvo > 600f)
                {
                    estadoAtualAtaque = EstadoCombate.Mergulho_Atacar;
                }
                break;

            case EstadoCombate.Mergulho_Atacar:
                Vector3 direcaoMergulho = (posicaoAlvo - transform.position).normalized;
                Vector3 pontoChao = posicaoAlvo + (direcaoMergulho * 60f); 
                pontoChao.y = 10f; 
                controleAviao.alvoGPSVoo = pontoChao;

                float anguloFrontal = Vector3.Angle(transform.forward, direcaoMergulho);
                if (anguloFrontal <= 25f && cronometroTiro <= 0f && distanciaDoAlvo < 900f && !recarregando)
                {
                    AtirarMetralhadora(posicaoAlvo);
                }

                if (distanciaDoAlvo < 120f || recarregando || alturaDoAviao < 35f)
                {
                    cronometroEvasao = 4f;
                    CalcularPontoEvasao(posicaoAlvo);
                    estadoAtualAtaque = EstadoCombate.Evasao;
                }
                break;

            case EstadoCombate.Evasao:
                controleAviao.alvoGPSVoo = pontoManobraFixo;
                cronometroEvasao -= Time.deltaTime;
                if (cronometroEvasao <= 0f || alturaDoAviao > 180f)
                {
                    estadoAtualAtaque = EstadoCombate.Afastamento; 
                    CalcularPontoAfastamento(posicaoAlvo);
                }
                break;
        }
    }

    void CalcularPontoAfastamento(Vector3 alvo)
    {
        // Vai para longe (600 metros na direção contrária de onde está olhando) e alto
        Vector3 direcaoTras = -transform.forward;
        direcaoTras.y = 0;
        
        // Pega um ponto de curva para não virar no próprio eixo de forma esquisita
        Vector3 lateral = transform.right * (Random.value > 0.5f ? 1f : -1f); 
        Vector3 direcaoCurva = (direcaoTras + lateral).normalized;

        pontoManobraFixo = transform.position + (direcaoCurva * 600f);
        pontoManobraFixo.y = 200f; // Sobe lá no alto para o mergulho
    }

    void CalcularPontoEvasao(Vector3 alvo)
    {
        // Continua indo pra frente, mas puxa com tudo pra cima
        Vector3 direcaoFrente = transform.forward;
        direcaoFrente.y = 0;

        pontoManobraFixo = transform.position + (direcaoFrente * 400f);
        pontoManobraFixo.y = 250f; // Altura de escape
    }

    void AtirarMetralhadora(Vector3 posicaoAlvoChao)
    {
        if (prefabProjetilTrassante == null || canosDeTiro.Length == 0) return;

        foreach (Transform cano in canosDeTiro)
        {
            if (cano == null) continue;
            
            Vector3 rajadaDir = (posicaoAlvoChao - cano.position).normalized;
            Vector3 direcaoFinal = Vector3.Lerp(cano.forward, rajadaDir, 0.90f).normalized;

            direcaoFinal += new Vector3(Random.Range(-0.015f, 0.015f), Random.Range(-0.02f, 0.02f), Random.Range(-0.015f, 0.015f));
            direcaoFinal.Normalize();

            GameObject bala = PoolDeObjetosCombate.Spawn(prefabProjetilTrassante, cano.position, Quaternion.LookRotation(direcaoFinal));
            Projetil scriptProj = bala.GetComponent<Projetil>();
            
            if (scriptProj != null)
            {
                scriptProj.SetDono(this.gameObject);
                scriptProj.SetDirecao(direcaoFinal);
            }
        }

        balasAtuais -= canosDeTiro.Length;
        if (balasAtuais <= 0)
        {
            recarregando = true;
            cronometroTiro = tempoRecarga; 
        }
        else
        {
            cronometroTiro = cadenciaDeTiro;
        }
        
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null) 
        {
            audio.pitch = Random.Range(0.9f, 1.1f);
            audio.Play();
        }
    }
}
