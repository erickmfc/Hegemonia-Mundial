using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(ControleAviao))]
[RequireComponent(typeof(ControleUnidade))]
[RequireComponent(typeof(SistemaDeDanos))]
public class LancadorMisselCaca : MonoBehaviour
{
    [Header("Configuração de Munição e Vida")]
    public int municaoAtual = 2;    
    public int municaoMaxima = 4;
    
    [Header("Configuração de Lançamento")]
    public Transform[] pontosDeSaida; 
    public GameObject missilCacaPrefab; 
    public float tempoRecarga = 2.0f;
    public float raioDeDeteccao = 600f; // Área de identificação do Radar
    
    // Estado Temporário
    private float cronometroRecarga = 0f;
    private int indiceCano = 0;
    private ControleUnidade unidadeBase;
    private ControleAviao vooModerno;
    private SistemaDeDanos sistemaDanos;
    private int meuTime;

    // Patrulha e Comportamento
    public bool modoPassivo = false; // Controlado pelo MenuComportamento
    private Vector3 pontoPatrulha;
    private bool voltandoParaBase = false;

    // Detecção
    public class AlvoDetectado 
    {
        public Transform transform;
        public string nome;
        public float distancia;
    }
    private List<AlvoDetectado> inimigosNaArea = new List<AlvoDetectado>();
    private float tempoUltimoScan = 0f;

    // Interface
    private Vector2 scrollPosition;
    private bool radarMinimizado = false;
    private bool radarFechado = false;
    private bool ultimoEstadoRadar = false;

    void Start()
    {
        unidadeBase = GetComponent<ControleUnidade>();
        vooModerno = GetComponent<ControleAviao>();
        sistemaDanos = GetComponent<SistemaDeDanos>();
        
        // Padrão 1 (Player) caso o avião não tenha tag especificada
        meuTime = GetComponent<IdentidadeIA>()?.teamID ?? GetComponent<IdentidadeUnidade>()?.teamID ?? 1;
    }

    void Update()
    {
        if (cronometroRecarga > 0) cronometroRecarga -= Time.deltaTime;

        // 1. Deteccão de Inimigos (Radar) - Apenas a cada 1 segundo pra não pesar a RAM
        if (Time.time > tempoUltimoScan + 1.0f)
        {
            tempoUltimoScan = Time.time;
            EscanearArea();
            
            // --- LÓGICA DE PATRULHA AUTOMÁTICA ---
            ProcessarPatrulhaAutomatica();
        }

        // 2. Sistema de Recarga Automática na Base
        if (vooModerno.estadoAtual == ControleAviao.EstadoAviao.ProntoNoPatio || vooModerno.estadoAtual == ControleAviao.EstadoAviao.ReservaHangar)
        {
            // O avião está pousado (voltou para o aeroporto ou base). Ele recarrega mísseis e vida instantaneamente.
            if (municaoAtual < municaoMaxima)
            {
                municaoAtual = municaoMaxima;
                indiceCano = 0; // Reseta o cano de disparo
                
                // Reativa a malha (mesh) dos "mísseis em reserva" para mostrar que recarregou
                if (pontosDeSaida != null && pontosDeSaida.Length > 0)
                {
                    foreach (Transform tf in pontosDeSaida)
                    {
                        if (tf != null)
                        {
                            Renderer[] renderers = tf.GetComponentsInChildren<Renderer>();
                            foreach(var r in renderers)
                            {
                                r.enabled = true;
                            }
                        }
                    }
                }
                
                Debug.Log($"✈️ [Base] {gameObject.name} recarregou mísseis no Hangar/Porta-Aviões!");

                // Se estava voltando para a base sozinho, ele decola de volta pra patrulha
                if (voltandoParaBase)
                {
                    voltandoParaBase = false;
                    
                    // Esperar um pouco antes de decolar (só pra não dar soco instantâneo)
                    if (vooModerno.aeroportoOrigem != null)
                    {
                        vooModerno.IniciarMissaoCompleta(pontoPatrulha);
                        Debug.Log($"✈️ [Base] {gameObject.name} voltando para a patrulha automática!");
                    }
                }
            }
            if (sistemaDanos.vidaAtual < sistemaDanos.vidaMaxima)
            {
                sistemaDanos.Reparar(sistemaDanos.vidaMaxima);
                Debug.Log($"✈️ [Base] {gameObject.name} foi totalmente reparado!");
            }
        }
    }

    void ProcessarPatrulhaAutomatica()
    {
        // Se estiver no chão, não tenta atirar
        if (vooModerno.estadoAtual != ControleAviao.EstadoAviao.EmMissao) return;

        // Se a missão era voltar pra base, foca nisso e ignora inimigos
        if (voltandoParaBase) return;

        // Se estiver ativo (não passivo) e tem mísseis
        if (!modoPassivo)
        {
            if (municaoAtual > 0 && cronometroRecarga <= 0 && inimigosNaArea.Count > 0)
            {
                // Pega o inimigo mais próximo e atira! (O inimigosNaArea já é ordenado por distância via EscanearArea)
                var alvo = inimigosNaArea[0];
                if (alvo != null && alvo.transform != null)
                {
                    Disparar(alvo.transform);
                }
            }

            // Volta para a base pegar munição se ficar vazio e não estiver passivo
            if (municaoAtual <= 0)
            {
                if (vooModerno.aeroportoOrigem != null)
                {
                    pontoPatrulha = vooModerno.alvoGPSVoo; // Grava onde ele estava fazendo a ronda
                    voltandoParaBase = true;
                    vooModerno.ComandoRetornarBase();
                    Debug.Log($"✈️ [Radar] {gameObject.name} sem munição! Retornando para a base via Aeroporto.");
                }
            }
        }
    }

    void EscanearArea()
    {
        inimigosNaArea.Clear();
        Collider[] hits = Physics.OverlapSphere(transform.position, raioDeDeteccao);

        foreach (var col in hits)
        {
            var idIA = col.GetComponentInParent<IdentidadeIA>();
            var idUnidade = col.GetComponentInParent<IdentidadeUnidade>();
            
            int teamDele = idIA?.teamID ?? idUnidade?.teamID ?? -1;

            // Considera inimigo se for de outro time OU não tiver time (-1, ex: cubo de teste)
            if (teamDele != meuTime)
            {
                var alvoDanos = col.GetComponentInParent<SistemaDeDanos>();
                if (alvoDanos != null && alvoDanos.vidaAtual > 0)
                {
                    Transform alvoTransform = alvoDanos.transform;
                    
                    // Procura se já não listamos esse inimigo (visto que ele pode ter múltiplos colliders)
                    bool jaAdicionado = false;
                    foreach(var a in inimigosNaArea) {
                        if (a.transform == alvoTransform) {
                            jaAdicionado = true;
                            break;
                        }
                    }

                    if (!jaAdicionado)
                    {
                        AlvoDetectado novo = new AlvoDetectado();
                        novo.transform = alvoTransform;
                        novo.nome = alvoTransform.name.Replace("(Clone)", ""); 
                        novo.distancia = Vector3.Distance(transform.position, alvoTransform.position);
                        inimigosNaArea.Add(novo);
                    }
                }
            }
        }
        
        // Ordena por distância (Mais próximos primeiro)
        inimigosNaArea.Sort((a, b) => a.distancia.CompareTo(b.distancia));
    }

    void Disparar(Transform alvo)
    {
        if (municaoAtual <= 0 || missilCacaPrefab == null || alvo == null) return;

        Transform saida = transform;
        if (pontosDeSaida != null && pontosDeSaida.Length > 0)
        {
            saida = pontosDeSaida[indiceCano];
            
            // Oculta a malha (mesh) do "míssil em reserva" que está na asa do avião, 
            // para mostrar que o míssil foi fisicamente gasto.
            Renderer[] renderers = saida.GetComponentsInChildren<Renderer>();
            foreach(var r in renderers)
            {
                r.enabled = false;
            }

            indiceCano = (indiceCano + 1) % pontosDeSaida.Length;
        }

        GameObject missil = Instantiate(missilCacaPrefab, saida.position, saida.rotation);
        
        MisselCaca scriptVoo = missil.GetComponent<MisselCaca>();
        if (scriptVoo != null)
        {
            Vector3 velAtual = GetComponent<Rigidbody>() != null ? GetComponent<Rigidbody>().linearVelocity : (transform.forward * 40f); 
            scriptVoo.IniciarAtaque(alvo.position, velAtual, alvo); // passa o transform pra seguir
        }

        municaoAtual--;
        cronometroRecarga = tempoRecarga;
        
        AudioSource audio = GetComponent<AudioSource>();
        if(audio != null) audio.Play();
    }

    void OnGUI()
    {
        bool radarAtivoVisualmente = false;

        // 1ª Forma: Selecionado diretamente via clique do mouse (RTS)
        if (unidadeBase != null && unidadeBase.selecionado) radarAtivoVisualmente = true;

        // 2ª Forma: Selecionado na lista do Aeroporto
        ControleAviao aviaoGeral = GetComponent<ControleAviao>();
        if (aviaoGeral != null && aviaoGeral.aeroportoOrigem != null)
        {
            if (aviaoGeral.aeroportoOrigem.aviaoSelecionadoParaMissao == aviaoGeral) radarAtivoVisualmente = true;
        }

        // Reseta o estado fechar/minimizar se abriu de novo
        if (radarAtivoVisualmente && !ultimoEstadoRadar)
        {
            radarFechado = false;
        }
        ultimoEstadoRadar = radarAtivoVisualmente;

        if (!radarAtivoVisualmente || radarFechado) return;
        
        // E só se tivermos detectado pelo menos 1 inimigo (Radar Acionado)
        if (inimigosNaArea.Count == 0) return;

        // Aumentando 10% do tamanho
        float largura = 385;
        float altura = 385;
        // Posicione o visual à direita da tela, para não conflitar com a UI inferior de construção
        float x = Screen.width - largura - 20; 
        float y = (Screen.height - altura) / 2; 

        if (radarMinimizado)
        {
            GUI.Box(new Rect(x, y, largura, 30), "📡 RADAR: ALVOS DETECTADOS");
            if (GUI.Button(new Rect(x + largura - 50, y + 5, 20, 20), "▼")) radarMinimizado = false;
            if (GUI.Button(new Rect(x + largura - 25, y + 5, 20, 20), "X")) radarFechado = true;
            return;
        }

        GUI.Box(new Rect(x, y, largura, altura), "📡 RADAR: ALVOS DETECTADOS");
        
        // Botões de minimizar e fechar
        if (GUI.Button(new Rect(x + largura - 50, y + 5, 20, 20), "▲")) radarMinimizado = true;
        if (GUI.Button(new Rect(x + largura - 25, y + 5, 20, 20), "X")) radarFechado = true;

        GUI.Label(new Rect(x + 15, y + 30, 200, 20), $"<color=yellow>Mísseis Restantes: {municaoAtual} / {municaoMaxima}</color>");

        if (municaoAtual <= 0)
        {
            GUI.Label(new Rect(x + 15, y + 50, 320, 20), "<color=red>AERONAVE SEM MÍSSEIS - RETORNE À BASE!</color>");
        }

        GUI.Label(new Rect(x + 15, y + 75, 200, 20), $"Hostis na Área: {inimigosNaArea.Count}");

        // Lista de Inimigos (ScrollView)
        scrollPosition = GUI.BeginScrollView(
            new Rect(x + 10, y + 100, largura - 20, altura - 110), 
            scrollPosition, 
            new Rect(0, 0, largura - 40, inimigosNaArea.Count * 45)
        );

        for (int i = 0; i < inimigosNaArea.Count; i++)
        {
            var alvo = inimigosNaArea[i];
            
            // É possível que o alvo tenha sido destruído neste frame
            if (alvo.transform == null) continue;

            float slotY = i * 45;
            
            GUI.Label(new Rect(5, slotY, 150, 20), $"<b>{alvo.nome}</b>");
            GUI.Label(new Rect(5, slotY + 20, 100, 20), $"{alvo.distancia:F0}m");

            if (GUI.Button(new Rect(140, slotY + 5, 80, 30), "SEGUIR"))
            {
                // Ordena o jato voar para as costas do inimigo ou interceptar
                if (vooModerno != null) vooModerno.alvoGPSVoo = alvo.transform.position;
            }

            // Desabilita o botão atacar se estiver no cooldown ou sem bala
            GUI.enabled = (municaoAtual > 0 && cronometroRecarga <= 0);
            if (GUI.Button(new Rect(225, slotY + 5, 80, 30), "ATACAR"))
            {
                Disparar(alvo.transform);
            }
            GUI.enabled = true; // Volta ao normal
        }

        GUI.EndScrollView();
    }
}
