using UnityEngine;

public class Vida : MonoBehaviour
{
    [Header("Configuração de Vida")]
    public int vidaMaxima = 100;
    public int vidaAtual;

    [Header("Efeitos Visuais")]
    public GameObject efeitoDano; // Partículas ou efeito ao receber dano
    public GameObject efeitoMorte; // Explosão ou efeito ao morrer

    [Header("Configuração de Morte")]
    public float tempoAteDestruir = 2f; // Tempo até destruir o GameObject após morrer
    public bool desativarColliderAoMorrer = true;
    
    private bool estaMorto = false;

    void Start()
    {
        // Inicializa com vida cheia
        vidaAtual = vidaMaxima;
    }

    public void ReceberDano(int quantidade)
    {
        if (estaMorto) return; // Já está morto, ignora dano

        vidaAtual -= quantidade;
        Debug.Log($"{gameObject.name} recebeu {quantidade} de dano! Vida: {vidaAtual}/{vidaMaxima}");

        // Efeito visual de dano
        if (efeitoDano != null)
        {
            Instantiate(efeitoDano, transform.position, Quaternion.identity);
        }

        // Verifica se morreu
        if (vidaAtual <= 0)
        {
            Morrer();
        }
    }

    public void Curar(int quantidade)
    {
        if (estaMorto) return;

        vidaAtual += quantidade;
        if (vidaAtual > vidaMaxima)
        {
            vidaAtual = vidaMaxima;
        }
        Debug.Log($"{gameObject.name} foi curado! Vida: {vidaAtual}/{vidaMaxima}");
    }

    void Morrer()
    {
        if (estaMorto) return; // Evita chamar Morrer múltiplas vezes
        
        estaMorto = true;
        vidaAtual = 0;

        Debug.Log($"{gameObject.name} foi destruído!");

        // Efeito visual de morte (explosão)
        if (efeitoMorte != null)
        {
            Instantiate(efeitoMorte, transform.position, Quaternion.identity);
        }

        // Desativa collider para não bloquear projéteis
        if (desativarColliderAoMorrer)
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        // Desativa scripts de IA/controle
        DesativarScripts();

        // Destrói o objeto após um tempo
        Destroy(gameObject, tempoAteDestruir);
    }

    void DesativarScripts()
    {
        // Desativa NavMeshAgent para parar movimento
        UnityEngine.AI.NavMeshAgent agente = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agente != null) agente.enabled = false;

        // Desativa Animator para parar animações (ou ativar animação de morte)
        Animator anim = GetComponent<Animator>();
        if (anim != null)
        {
            // Opcional: Ativar trigger de morte se tiver animação
            // anim.SetTrigger("Morrer");
            // Por enquanto, apenas desativa
            anim.enabled = false;
        }

        // Desativa ControleUnidade
        ControleUnidade controle = GetComponent<ControleUnidade>();
        if (controle != null) controle.enabled = false;

        // Desativa helicóptero se for um
        VooHelicoptero heli = GetComponent<VooHelicoptero>();
        if (heli != null) heli.enabled = false;
    }

    // Método para verificar se está vivo (útil para outros scripts)
    public bool EstaVivo()
    {
        return !estaMorto && vidaAtual > 0;
    }

    // Método para obter a porcentagem de vida (útil para barras de vida)
    public float PorcentagemVida()
    {
        return (float)vidaAtual / (float)vidaMaxima;
    }
}
