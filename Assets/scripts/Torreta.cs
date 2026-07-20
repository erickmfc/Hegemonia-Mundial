using UnityEngine;
using System.Collections.Generic;

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
    private int _meuTime;
    private Quaternion rotacaoInicialCabeca = Quaternion.identity;
    private Vector3 eulerRepousoCabeca;
    private static readonly List<IdentidadeUnidade> _bufferUnidades = new List<IdentidadeUnidade>(512);

    void Start()
    {
        _meuTime = GetComponentInParent<IdentidadeUnidade>()?.teamID ?? GetComponent<IdentidadeUnidade>()?.teamID ?? 1;
        if (cabecaGiro == null) cabecaGiro = transform;
        rotacaoInicialCabeca = cabecaGiro.localRotation;
        eulerRepousoCabeca = cabecaGiro.localEulerAngles;
        if (pontoTiro == null) pontoTiro = cabecaGiro;
        InvokeRepeating("AtualizarAlvo", 0f, 0.5f);
    }

    void AtualizarAlvo()
    {
        float alcanceSqr = alcance * alcance;
        float menorDistanciaSqr = float.PositiveInfinity;
        Transform melhor = null;

        RegistroEntidadesJogo.FillUnidades(_bufferUnidades);
        for (int i = 0; i < _bufferUnidades.Count; i++)
        {
            IdentidadeUnidade idAlvo = _bufferUnidades[i];
            if (idAlvo == null) continue;
            if (idAlvo.teamID == _meuTime) continue;

            SistemaDeDanos vida = idAlvo.GetComponent<SistemaDeDanos>();
            if (vida == null || vida.vidaAtual <= 0) continue;

            Transform alvo = vida.transform;
            float distSqr = (alvo.position - transform.position).sqrMagnitude;
            if (distSqr > alcanceSqr) continue;
            if (distSqr >= menorDistanciaSqr) continue;

            menorDistanciaSqr = distSqr;
            melhor = alvo;
        }

        alvoAtual = melhor;
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
        Vector3 direcao = alvoAtual.position - cabecaGiro.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.001f) return;

        if (cabecaGiro.parent != null)
        {
            Vector3 localDir = cabecaGiro.parent.InverseTransformDirection(direcao);
            float yaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            Quaternion olharPara = Quaternion.Euler(eulerRepousoCabeca.x, yaw, eulerRepousoCabeca.z);
            cabecaGiro.localRotation = Quaternion.Lerp(cabecaGiro.localRotation, olharPara, Time.deltaTime * velocidadeGiro);
        }
        else
        {
            // Sem pai, a torreta usa coordenadas de mundo. Usar o próprio
            // transform como referência fazia o yaw ser recalculado em cima
            // da rotação atual e podia causar giro estranho/deriva.
            float yawMundo = Mathf.Atan2(direcao.x, direcao.z) * Mathf.Rad2Deg;
            Quaternion olharParaMundo = Quaternion.Euler(eulerRepousoCabeca.x, yawMundo, eulerRepousoCabeca.z);
            cabecaGiro.rotation = Quaternion.Lerp(cabecaGiro.rotation, olharParaMundo, Time.deltaTime * velocidadeGiro);
        }

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

        GameObject bala = PoolDeObjetosCombate.Spawn(prefabProjetil, pontoTiro.position, pontoTiro.rotation);
        
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
