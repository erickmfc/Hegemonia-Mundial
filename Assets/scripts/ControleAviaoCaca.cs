using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SistemaDeDanos))]
public class ControleAviaoCaca : MonoBehaviour
{
    // ============================================
    // CONFIGURAÇÕES GERAIS
    // ============================================
    [Header("Status de Voo")]
    public float altitudeCruzeiro = 40f; 
    [Tooltip("Altura para recolher o trem de pouso e considerar 'Voando'.")]
    public float alturaDecolagem = 15f; 
    
    [Header("Velocidades")]
    public float velocidadeTaxi = 10f;       // No chão
    public float velocidadeCruzeiro = 40f;   // Voando normal (Passivo)
    public float velocidadeAtaque = 80f;     // Voando em combate (Ativo)
    public float velocidadeCurva = 1.0f;     // Agilidade da curva

    [Header("Combustão e Efeitos")]
    public List<ParticleSystem> fogoNosMotores; // Arraste os efeitos de fogo aqui
    public Light luzPosCombustao; // Opcional

    [Header("Trem de Pouso")]
    [Tooltip("Objetos das rodas que vão girar.")]
    public List<Transform> rodas; 
    [Tooltip("Eixo de rotação para recolher (X, Y ou Z). Geralmente X.")]
    public Vector3 eixoRecolher = Vector3.right; 
    public float anguloRecolher = 90f; 
    public float velocidadeTremPouso = 2f;

    // ============================================
    // ESTADOS INTERNOS
    // ============================================
    public enum EstadoVoo { NoChao, Decolando, Voando, Pousando }
    [SerializeField] // Para ver no inspector
    private EstadoVoo estadoAtual = EstadoVoo.NoChao;

    private float velocidadeAtual = 0f;
    private Vector3 destinoAtual;
    private bool temDestino = false;
    private Transform alvoAtaque = null;
    
    // Controle de Rodas
    private float fatorRodas = 0f; // 0 = Baixadas, 1 = Recolhidas
    private List<Quaternion> rotacoesOriginaisRodas = new List<Quaternion>();

    // Referências
    private ControleUnidade controleUnidade;
    private SistemaDeTiro sistemaTiro; // Para saber se tem alvo

    void Start()
    {
        controleUnidade = GetComponent<ControleUnidade>();
        sistemaTiro = GetComponentInChildren<SistemaDeTiro>();
        
        destinoAtual = transform.position;

        // Salva rotação original das rodas
        foreach(var roda in rodas)
        {
            if(roda != null) rotacoesOriginaisRodas.Add(roda.localRotation);
        }

        // Garante que começa desligado
        ControlarEfeitosMotor(false);
    }

    void Update()
    {
        // 1. GERENCIAMENTO DE ESTADO
        AtualizarLogicaEstado();

        // 2. MOVIMENTO FÍSICO
        MoverAviao();

        // 3. ANIMAÇÃO DO TREM DE POUSO
        AnimarTremDePouso();

        // 4. INPUT DE DECOLAGEM (Teste ou via Clique do ControleUnidade)
        if (estadoAtual == EstadoVoo.NoChao && temDestino)
        {
            float dist = Vector3.Distance(transform.position, destinoAtual);
            if (dist > 50f) // Só decola se o destino for longe
            {
                IniciarDecolagem();
            }
        }
    }

    // Chamado por scripts externos (ControleUnidade)
    public void DefinirDestino(Vector3 novoDestino)
    {
        destinoAtual = novoDestino;
        // Mantém a altura de voo no destino para evitar mergulhar no chão
        if (estadoAtual == EstadoVoo.Voando)
        {
            destinoAtual.y = altitudeCruzeiro;
        }
        temDestino = true;
    }

    void AtualizarLogicaEstado()
    {
        float alturaDoChao = transform.position.y; // Simplificado (assumindo chão em Y=0 ou usando Raycast)
        
        // Verifica se tem chuva de tiros ou inimigos
        bool emCombate = false;
        if (sistemaTiro != null && !sistemaTiro.modoPassivo)
        {
            // Tenta pegar o alvo privado do sistema de tiro via Reflection ou supõe lógica
            // Aqui vamos assumir que se não está passivo e tem munição, está "Ativo"
             emCombate = true; 
        }

        switch (estadoAtual)
        {
            case EstadoVoo.NoChao:
                velocidadeAtual = Mathf.Lerp(velocidadeAtual, 0f, Time.deltaTime);
                ControlarEfeitosMotor(false);
                break;

            case EstadoVoo.Decolando:
                // Sobe inclinado
                velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeCruzeiro, Time.deltaTime * 0.5f);
                ControlarEfeitosMotor(true);
                
                if (alturaDoChao > alturaDecolagem)
                {
                    estadoAtual = EstadoVoo.Voando;
                    Debug.Log("✈️ [F_C19] Decolagem concluída! Entrando em voo de cruzeiro.");
                }
                break;

            case EstadoVoo.Voando:
                // Lógica de Velocidade Variável
                float targetSpeed = emCombate ? velocidadeAtaque : velocidadeCruzeiro;
                velocidadeAtual = Mathf.Lerp(velocidadeAtual, targetSpeed, Time.deltaTime);
                ControlarEfeitosMotor(true);
                break;
                
            case EstadoVoo.Pousando:
                velocidadeAtual = Mathf.Lerp(velocidadeAtual, velocidadeTaxi, Time.deltaTime * 0.2f);
                ControlarEfeitosMotor(false); // Motor fraco no pouso
                
                if (alturaDoChao < 2f)
                {
                    estadoAtual = EstadoVoo.NoChao;
                    Debug.Log("🛬 [F_C19] Pouso confirmado.");
                }
                break;
        }
    }

    void MoverAviao()
    {
        if (estadoAtual == EstadoVoo.NoChao) return; // Parado ou taxiando manualmente (não implementado taxi complexo)

        // DIREÇÃO
        Vector3 direcao = transform.forward;
        
        if (estadoAtual == EstadoVoo.Voando || estadoAtual == EstadoVoo.Decolando)
        {
            // Se tem destino, vira para ele
            if (temDestino)
            {
                Vector3 vetorParaDestino = destinoAtual - transform.position;
                
                // Se chegou perto do destino e não tem ataque, CIRCULA!
                if (vetorParaDestino.magnitude < 100f && estadoAtual == EstadoVoo.Voando)
                {
                    // Lógica de Circular: Move o destino para a "direita" constantemente
                    destinoAtual = transform.position + (transform.right * 200f) + (transform.forward * 100f);
                    vetorParaDestino = destinoAtual - transform.position;
                }
                // Se está "Ativo" (Combate), voa direto!
                
                Quaternion rotacaoAlvo = Quaternion.LookRotation(vetorParaDestino);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotacaoAlvo, Time.deltaTime * velocidadeCurva);
            }
            
            // ALTURA
            // Ajuste suave de altura (Fly-by-wire)
            float alturaDesejada = (estadoAtual == EstadoVoo.Decolando) ? altitudeCruzeiro : altitudeCruzeiro;
            
            // Simples subida/descida no pitch
            float erroAltura = alturaDesejada - transform.position.y;
            Vector3 pos = transform.position;
            pos.y += erroAltura * Time.deltaTime * 0.5f; // Sobe suave
            transform.position = pos;
        }
        else if (estadoAtual == EstadoVoo.Pousando)
        {
            // Desce para o destino (pista)
            if (temDestino)
            {
                var dir = (destinoAtual - transform.position).normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime);
            }
            // Força descida
            transform.Translate(Vector3.down * 5f * Time.deltaTime, Space.World);
        }

        // APLICA MOVIMENTO FINAL (Sempre para frente!)
        transform.Translate(Vector3.forward * velocidadeAtual * Time.deltaTime);
        
        // BANKING (Inclina nas curvas)
        // Recalcula banking baseado na curva
        float curva = 0f; // ... logica de banking avançada opcional
    }

    void IniciarDecolagem()
    {
        if (estadoAtual != EstadoVoo.NoChao) return;
        
        Debug.Log("🛫 [F_C19] Iniciando sequência de decolagem...");
        estadoAtual = EstadoVoo.Decolando;
        
        // Define um destino longe e alto na frente se não tiver
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

    // ============================================
    // SISTEMAS AUXILIARES
    // ============================================

    void AnimarTremDePouso()
    {
        // Define o alvo (0 = Baixado, 1 = Recolhido)
        float meta = (estadoAtual == EstadoVoo.Voando || estadoAtual == EstadoVoo.Decolando) ? 1f : 0f;
        
        // Se já está decolando mas ainda baixo (<15m), mantém baixado? 
        // Não, o user disse "rodas recuar assim que voar". 
        // Vamos considerar estado 'Voando' como gatilho principal.
        if (estadoAtual == EstadoVoo.Decolando && transform.position.y < alturaDecolagem) meta = 0f;

        // Move fator
        fatorRodas = Mathf.MoveTowards(fatorRodas, meta, Time.deltaTime * velocidadeTremPouso);

        // Aplica rotação nas rodas
        for (int i = 0; i < rodas.Count; i++)
        {
            if (rodas[i] == null) continue;
            
            // Calcula rotação atual baseada no fator (Lerp)
            // 0 -> Rotação Original
            // 1 -> Rotação Original * 90 graus
            Quaternion rotOriginal = rotacoesOriginaisRodas[i];
            Quaternion rotRecolhida = rotOriginal * Quaternion.Euler(eixoRecolher * anguloRecolher);
            
            rodas[i].localRotation = Quaternion.Slerp(rotOriginal, rotRecolhida, fatorRodas);
        }
    }

    // --- MÉTODOS PÚBLICOS DE ESTADO ---
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
        foreach (var ps in fogoNosMotores)
        {
            if (ps == null) continue;
            if (ligado && !ps.isPlaying) ps.Play();
            if (!ligado && ps.isPlaying) ps.Stop();
        }
        
        if (luzPosCombustao != null)
        {
            luzPosCombustao.enabled = ligado;
        }
    }
    
    // Gizmos para debug
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destinoAtual);
        Gizmos.DrawWireSphere(destinoAtual, 2f);
    }
}
