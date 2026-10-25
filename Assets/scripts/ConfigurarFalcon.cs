using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Script DOUTOR FALCON - Repara completamente o helicóptero
/// 1. Remove NavMesh (proibido)
/// 2. Configura Física (Kinematic)
/// 3. Restaura Scripts de Voo e Seleção
/// 4. Reconecta Hélices
/// </summary>
public class ConfigurarFalcon : MonoBehaviour
{
    void Start()
    {
        RepararGeral();
    }

    // Botão direito no inspector para rodar manualmente também
    [ContextMenu("Rodar Reparo Agora")]
    public void RepararGeral()
    {
        Debug.Log($"🔧 [Doutor Falcon] Iniciando cirurgia no passaro: {name}...");

        // 1. REMOVER NAVMESH (Proibido)
        var nav = GetComponent<NavMeshAgent>();
        if (nav != null)
        {
            DestroyImmediate(nav);
            Debug.Log("   🗑️ NavMeshAgent REMOVIDO (Como solicitado).");
        }

        // 2. CONFIGURAR RIGIDBODY (Para não cair igual pedra)
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.useGravity = false;      // Nós simulamos gravidade, não a física
        rb.isKinematic = true;      // CRÍTICO: Impede que a física empurre ele pro chão
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Movimento suave
        Debug.Log("   ✅ Rigidbody Configurado (Kinematic=True, Gravity=False)");

        // 3. RESTAURAR HELICOPTER CONTROLLER (Cérebro)
        Helicoptero heli = GetComponent<Helicoptero>();
        if (heli == null)
        {
            // Adiciona o novo sistema unificado
            heli = gameObject.AddComponent<Helicoptero>();
            heli.velocidadeHelice = 1500f; // Falcon é rápido
            Debug.Log("   ✅ Adicionado: Helicoptero (Agora a hélice vai girar!)");
        }
        
        // Tenta achar as hélices automaticamente pelos nomes comuns
        if (heli.helicePrincipal == null)
        {
            // Procura em filhos profundos
            Transform acheiMain = BuscarFilho(transform, "MainPropeller") ?? BuscarFilho(transform, "Helice") ?? BuscarFilho(transform, "Propeller");
            if (acheiMain) 
            {
                heli.helicePrincipal = acheiMain;
                Debug.Log($"   🚁 Hélice Principal reconectada: {acheiMain.name}");
            }
        }
        if (heli.heliceTraseira == null)
        {
            Transform acheiBack = BuscarFilho(transform, "TailPropeller") ?? BuscarFilho(transform, "HeliceTraseira") ?? BuscarFilho(transform, "Rotor_Tail");
            if (acheiBack) 
            {
                heli.heliceTraseira = acheiBack;
                Debug.Log($"   🚁 Hélice Traseira reconectada: {acheiBack.name}");
            }
        }

        // 4. RESTAURAR CONTROLE UNIDADE (Para Selecionar)
        var controle = GetComponent<ControleUnidade>();
        if (controle == null)
        {
            controle = gameObject.AddComponent<ControleUnidade>();
            Debug.Log("   ✅ Adicionado: ControleUnidade (Agora dá para selcionar!)");
        }
        controle.tamanhoSelecao = 5f;

        // 5. RESTAURAR IDENTIDADE (Para ser do Time)
        var id = GetComponent<IdentidadeUnidade>();
        if (id == null)
        {
            id = gameObject.AddComponent<IdentidadeUnidade>();
            id.teamID = 1;
            id.tipoUnidade = TipoUnidade.Aereo;
            Debug.Log("   ✅ Adicionado: IdentidadeUnidade (RG do Time 1)");
        }

        // 6. Configura Voo Padrão
        heli.altitudeDeVoo = 30f;
        heli.altitudeDeVoo = 6f; // Altura segura para não nascer no chão

        Debug.Log("✨ [Doutor Falcon] REPARO CONCLUÍDO! O Pássaro deve voar agora! ✨");
    }

    // Função auxiliar recursiva para achar filhos perdidos
    Transform BuscarFilho(Transform pai, string parteDoNome)
    {
        foreach (Transform filho in pai)
        {
            if (filho.name.Contains(parteDoNome)) return filho;
            Transform neto = BuscarFilho(filho, parteDoNome);
            if (neto != null) return neto;
        }
        return null;
    }
}
