using UnityEngine;

public class Torreta : MonoBehaviour
{
    [Header("Geral")]
    public float alcance = 15f;
    public float velocidadeGiro = 10f;
    public float cadenciaTiro = 1f; 
    private float contagemTiro = 0f;

    [Header("Peças")]
    public Transform cabecaGiro; 
    public Transform pontoTiro;  
    public GameObject prefabProjetil; 

    [Header("Radar")]
    public string tagInimigo = "Inimigo"; 
    private Transform alvoAtual;

    void Start()
    {
        InvokeRepeating("AtualizarAlvo", 0f, 0.5f);
    }

    void AtualizarAlvo()
    {
        GameObject[] inimigos = GameObject.FindGameObjectsWithTag(tagInimigo);
        float distanciaMaisCurta = Mathf.Infinity;
        GameObject inimigoMaisPerto = null;

        foreach (GameObject inimigo in inimigos)
        {
            float distanciaParaInimigo = Vector3.Distance(transform.position, inimigo.transform.position);
            if (distanciaParaInimigo < distanciaMaisCurta)
            {
                distanciaMaisCurta = distanciaParaInimigo;
                inimigoMaisPerto = inimigo;
            }
        }

        if (inimigoMaisPerto != null && distanciaMaisCurta <= alcance)
        {
            alvoAtual = inimigoMaisPerto.transform;
        }
        else
        {
            alvoAtual = null;
        }
    }

    void Update()
    {
        // --- PROTEÇÃO CONTRA ERRO DE "MISSING REFERENCE" ---
        if (alvoAtual == null) return;

        // Se o alvo morreu ou foi destruído, pare de olhar pra ele
        if (alvoAtual.gameObject == null) 
        {
            alvoAtual = null;
            return;
        }
        // ---------------------------------------------------

        // 1. MIRAR
        Vector3 direcao = alvoAtual.position - transform.position;
        Quaternion olharPara = Quaternion.LookRotation(direcao);
        
        Vector3 rotacao = Quaternion.Lerp(cabecaGiro.rotation, olharPara, Time.deltaTime * velocidadeGiro).eulerAngles;
        cabecaGiro.rotation = Quaternion.Euler(0f, rotacao.y, 0f); 

        // 2. ATIRAR (Só se estiver bem alinhado)
        // Calcula o ângulo ignorando a altura para evitar falhas se o alvo estiver num morro
        Vector3 dirPlana = direcao;
        dirPlana.y = 0;
        
        Vector3 cabecaPlana = cabecaGiro.forward;
        cabecaPlana.y = 0;

        float anguloParaAlvo = 999f;
        if (dirPlana != Vector3.zero && cabecaPlana != Vector3.zero)
        {
            anguloParaAlvo = Vector3.Angle(cabecaPlana.normalized, dirPlana.normalized);
        }
        
        if (contagemTiro <= 0f && anguloParaAlvo < 8f) // Só atira se a base apontou de fato
        {
            Atirar();
            contagemTiro = 1f / cadenciaTiro;
        }

        contagemTiro -= Time.deltaTime;
    }

    void Atirar()
    {
        // Proteção extra: Só atira se tiver munição carregada
        if(prefabProjetil == null) return;

        GameObject bala = Instantiate(prefabProjetil, pontoTiro.position, pontoTiro.rotation);
        
        // Verifica e adiciona componente Projetil se faltar
        Projetil scriptBala = bala.GetComponent<Projetil>();
        if (scriptBala == null) scriptBala = bala.AddComponent<Projetil>();

        // Define quem atirou (para não se auto-atacar)
        scriptBala.SetDono(transform.root.gameObject);
        
        if (alvoAtual != null)
        {
            // Calcula a direção FIXA do tiro (linha reta balística)
            // Mira no peito (+1m), não nos pés
            Vector3 alvoPos = alvoAtual.position + Vector3.up * 1.0f;
            Vector3 direcao = (alvoPos - pontoTiro.position).normalized;
            scriptBala.SetDirecao(direcao);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcance);
    }
}
