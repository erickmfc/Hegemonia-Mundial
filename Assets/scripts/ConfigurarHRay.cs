using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Script DOUTOR H-RAY - Repara o helicóptero H_Ray
/// 1. Adiciona HelicopterController (Cérebro de voo)
/// 2. Configura Física (Kinematic)
/// 3. Conecta as Hélices especificas do H_Ray
/// </summary>
public class ConfigurarHRay : MonoBehaviour
{
    void Start()
    {
        RepararGeral();
    }

    [ContextMenu("Rodar Reparo H_Ray")]
    public void RepararGeral()
    {
        Debug.Log($"🔧 [Doutor H-Ray] Iniciando configuração no: {name}...");

        // 1. REMOVER NAVMESH 
        var nav = GetComponent<NavMeshAgent>();
        if (nav != null)
        {
            DestroyImmediate(nav);
            Debug.Log("   🗑️ NavMeshAgent REMOVIDO.");
        }

        // 2. CONFIGURAR RIGIDBODY 
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        
        rb.useGravity = false; // Importante
        rb.isKinematic = true; // Importante
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        Debug.Log("   ✅ Rigidbody Configurado (Kinematic=True)");

        // 3. ADICIONAR HELICOPTERO (O QUE FALTAVA!)
        var heli = GetComponent<Helicoptero>();
        if (heli == null)
        {
            // Adiciona o novo sistema unificado
            heli = gameObject.AddComponent<Helicoptero>();
            heli.velocidadeHelice = 1200f;
            Debug.Log("   ✅ Adicionado: Helicoptero (Agora voa de verdade!)");
        }

        // 4. CONECTAR HÉLICES (Nomes específicos do H_Ray)
        // Na imagem vi: Chopper_02_MainPropel / Chopper_02_SubPropel
        if (heli.helicePrincipal == null)
        {
            Transform main = BuscarFilho(transform, "_MainPropel");
            if (main) heli.helicePrincipal = main;
        }
        if (heli.heliceTraseira == null)
        {
            Transform sub = BuscarFilho(transform, "_SubPropel");
            if (sub) heli.heliceTraseira = sub;
        }

        // 5. AJUSTAR IDENTIDADE
        var id = GetComponent<IdentidadeUnidade>();
        if (id == null) id = gameObject.AddComponent<IdentidadeUnidade>();
        id.tipoUnidade = TipoUnidade.Aereo; // Era Infantaria, corrigido para Aereo
        id.teamID = 1;
        id.nomeDoPais = "Hegemonia";

        // 6. AJUSTAR CONTROLE UNIDADE (Seleção)
        var ctrl = GetComponent<ControleUnidade>();
        if (ctrl == null) ctrl = gameObject.AddComponent<ControleUnidade>();
        ctrl.tamanhoSelecao = 5f;

        // 7. Configura Voo
        heli.altitudeDeVoo = 15f;
        heli.altitudeDeVoo = 15f;

        Debug.Log("✨ [Doutor H-Ray] REPARO CONCLUÍDO! ✨");
    }

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
