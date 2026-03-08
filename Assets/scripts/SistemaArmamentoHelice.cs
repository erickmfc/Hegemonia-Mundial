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
    public float raioDeVisao = 500f;
    
    [Tooltip("Margem de erro na mira. Só atira se o inimigo estiver na frente do avião.")]
    public float coneDeTiroGraus = 12f; 
    
    [Header("=== MUNIÇÃO E RECARGA ===")]
    public int cartuchoMaximo = 100;
    public float tempoRecarga = 5f;
    private int balasAtuais;
    private bool recarregando = false;

    private float cronometroTiro = 0f;
    private ControleAviao controleAviao;
    private int meuTime = 1;

    // --- NOVA OTIMIZAÇÃO DE RADAR ---
    private float cronometroScan = 0f;
    private Transform alvoAtualGuardado;

    void Start()
    {
        balasAtuais = cartuchoMaximo;
        controleAviao = GetComponent<ControleAviao>();
        
        IdentidadeUnidade id = GetComponent<IdentidadeUnidade>();
        if (id != null) meuTime = id.teamID;

        // AUTO-DETECÇÃO DA HÉLICE (Para facilitar caso não consiga arrastar no Inspector)
        if (helice == null)
        {
            Transform[] filhos = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in filhos)
            {
                if (t.name.ToLower().Contains("helice") || t.name.ToLower().Contains("propeller"))
                {
                    helice = t;
                    Debug.Log($"[ArmamentoHelice] Hélice auto-detectada com sucesso: {t.name}");
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
                // Busca objetos chamados "Tiro" ou "Tiro (1)" que você criou
                if (t.name.ToLower().StartsWith("tiro")) 
                {
                    canos.Add(t);
                }
            }
            canosDeTiro = canos.ToArray();
            Debug.Log($"[ArmamentoHelice] Mapeados {canosDeTiro.Length} pontos de tiro na asa.");
        }
    }

    void Update()
    {
        // 1. ANIMAÇÃO DA HÉLICE
        if (helice != null)
        {
            // Mais rápido no ar, mais lento taxiando/parado
            bool noAr = (controleAviao.estadoAtual == ControleAviao.EstadoAviao.EmMissao || 
                         controleAviao.estadoAtual == ControleAviao.EstadoAviao.Decolando);
                         
            float velReal = noAr ? velocidadeGiroVoo : velocidadeGiroChao;
            helice.Rotate(eixoGiro * velReal * Time.deltaTime, Space.Self);
        }

        // 2. LÓGICA DE DETECÇÃO E ATAQUE
        if (controleAviao.estadoAtual != ControleAviao.EstadoAviao.EmMissao) return; // Só ataca no ar

        if (recarregando)
        {
            cronometroTiro -= Time.deltaTime;
            if (cronometroTiro <= 0)
            {
                recarregando = false;
                balasAtuais = cartuchoMaximo;
            }
            return; // Ele só sobrevoa em paz enquanto troca o cartucho!
        }

        if (cronometroTiro > 0) cronometroTiro -= Time.deltaTime;

        // 3. OTIMIZAÇÃO: Varredura leve (Não usa física em todos os frames)
        cronometroScan -= Time.deltaTime;
        if (cronometroScan <= 0f)
        {
            EscanearAreaAoRedorLento();
            cronometroScan = 0.25f; // Scaneia o mundo só 4 vezes por segundo (Super Leve)
        }

        ProcessarAtaqueEmTempoReal();
    }

    void EscanearAreaAoRedorLento()
    {
        Collider[] vizinhos = Physics.OverlapSphere(transform.position, raioDeVisao);
        Transform alvoMaisProximoScanner = null;
        float menorDistancia = Mathf.Infinity;

        foreach (var col in vizinhos)
        {
            IdentidadeUnidade id = col.GetComponentInParent<IdentidadeUnidade>();
            
            if (id != null && id.teamID != meuTime && id.teamID != 0) 
            {
                SistemaDeDanos vida = col.GetComponentInParent<SistemaDeDanos>();
                if (vida != null && vida.vidaAtual > 0)
                {
                    float dist = Vector3.Distance(transform.position, vida.transform.position);
                    if (dist < menorDistancia)
                    {
                        menorDistancia = dist;
                        alvoMaisProximoScanner = vida.transform;
                    }
                }
            }
        }
        alvoAtualGuardado = alvoMaisProximoScanner;
    }

    void ProcessarAtaqueEmTempoReal()
    {
        // Limpeza de segurança caso o alvo tenha morrido explodido!
        if (alvoAtualGuardado == null || !alvoAtualGuardado.gameObject.activeInHierarchy) return;

        Vector3 alvoPosition = alvoAtualGuardado.position;
        Vector3 diferenca = alvoPosition - transform.position;
        float distanciaDoAlvo = diferenca.magnitude;

        // 1. EFEITO THUNDERBOLT (Mergulhar, desacelerar e passar rasgando por cima em linha reta!)
        if (distanciaDoAlvo < 350f) 
        {
            Vector3 direcaoRasa = diferenca; direcaoRasa.y = 0; direcaoRasa.Normalize();
            Vector3 rasantePontoFinal = alvoPosition + (direcaoRasa * 450f);
            rasantePontoFinal.y = 20f; 
            
            controleAviao.ForcarAtaqueMergulho(rasantePontoFinal);
        }
        else if (!controleAviao.emAtaqueMergulho)
        {
            controleAviao.alvoGPSVoo = alvoPosition;
        }

        // 2. MIRA FRONTAL MAIS PERMISSIVA (Qualquer coisa na reta leva bala)
        float angulo = Vector3.Angle(transform.forward, diferenca.normalized);

        if (angulo <= 25f && cronometroTiro <= 0f)
        {
            AtirarMetralhadora(alvoPosition);
        }
    }

    void AtirarMetralhadora(Vector3 posicaoAlvoChao)
    {
        if (prefabProjetilTrassante == null || canosDeTiro.Length == 0) return;

        foreach (Transform cano in canosDeTiro)
        {
            if (cano == null) continue;
            
            // GAU-8 AUTO-AIM: Evita que o tiro voe reto pro céu caso o caça levante o nariz:
            // Ele calcula um vetor magnético jogando as traçantes com força para baixo no chão onde o alvo está!
            Vector3 rajadaDir = (posicaoAlvoChao - cano.position).normalized;
            
            // Mistura 85% para o alvo da terra e 15% para o nariz físico
            Vector3 direcaoFinal = Vector3.Lerp(cano.forward, rajadaDir, 0.85f).normalized;

            // Micro-vibração da arma estilo A-10 Thunderbolt
            direcaoFinal += new Vector3(Random.Range(-0.015f, 0.015f), Random.Range(-0.02f, 0.02f), Random.Range(-0.015f, 0.015f));
            direcaoFinal.Normalize();

            // Cria a bala trassante
            GameObject bala = Instantiate(prefabProjetilTrassante, cano.position, Quaternion.LookRotation(direcaoFinal));
            Projetil scriptProj = bala.GetComponent<Projetil>();
            
            if (scriptProj != null)
            {
                scriptProj.SetDono(this.gameObject);
                scriptProj.SetDirecao(direcaoFinal); // Assegura que o tiro fure a terra!
            }
        }

        balasAtuais -= canosDeTiro.Length;
        if (balasAtuais <= 0)
        {
            // Fica sem atirar pelo tempo de recarga inteiro
            recarregando = true;
            cronometroTiro = tempoRecarga; 
        }
        else
        {
            cronometroTiro = cadenciaDeTiro;
        }
        
        // Toca som de metralhadora contínua se houver
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null) 
        {
            audio.pitch = Random.Range(0.9f, 1.1f); // Variação leve para som de rajada
            audio.Play();
        }
    }
}
