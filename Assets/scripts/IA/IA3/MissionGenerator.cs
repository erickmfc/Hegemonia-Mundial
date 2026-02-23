// SCRIPT GERADO PELO SUPERVISOR
// CONTEXTO: Criação dinâmica de 'ScriptMissao' (ScriptableObject)
// INTENÇÃO: "Construir 1 Estaleiro Naval, 3 Tendas (separadas), 3 Hangares (perto das tendas) e recrutar 1 Navio de Guerra."

using UnityEngine;
using System.Collections.Generic;

public class MissionGenerator : MonoBehaviour 
{
    [Header("Configuração de Prefabs (Arraste no Inspector)")]
    public GameObject prefabEstaleiro;
    public GameObject prefabTenda;
    public GameObject prefabHangar;
    public GameObject prefabNavioGuerra;

    // AUTO-START: Começa a gerar assim que dá Play
    // AUTO-START: Começa a gerar assim que dá Play
    void Start()
    {
        /*
        RecebedorIA recebedor = GetComponent<RecebedorIA>();
        if (recebedor != null)
        {
            Debug.Log("[MissionGenerator] Iniciando sequência de missões automáticas...");
            InjetarNoRecebedor(recebedor);
        }
        else
        {
            Debug.LogError("[MissionGenerator] ERRO: Não encontrei o componente 'RecebedorIA' no mesmo objeto!");
        }
        */
        Debug.Log("[MissionGenerator] Auto-start DESATIVADO para evitar conflito com CerebroIA.");
    }

    // Método principal que gera TODAS as missões em sequência
    public List<ScriptMissao> GerarPacoteMissaoCompleta() 
    {
        List<ScriptMissao> pacote = new List<ScriptMissao>();

        // 1. Estaleiro Naval
        ScriptMissao mEstaleiro = ScriptableObject.CreateInstance<ScriptMissao>();
        mEstaleiro.nomeDaMissao = "Construir Estaleiro Naval";
        mEstaleiro.solicitante = "Supervisor AI - Naval";
        mEstaleiro.tipoAcao = ActionType.ConstructBuilding; 
        mEstaleiro.prioridade = PriorityLevel.High;
        mEstaleiro.custoEstimado = 2500f;
        mEstaleiro.executarImediatamente = true;
        mEstaleiro.objetoAlvo = prefabEstaleiro; 
        // Posição: Deveria ser calculada, mas vamos deixar zero para o Arquiteto decidir ou setar aqui se soubermos a água
        mEstaleiro.posicaoAlvo = new Vector3(300, 0, 800); // Exemplo: Posição na água (fake)
        pacote.Add(mEstaleiro);

        // 2. Três Tendas (em lugares separados)
        for (int i = 0; i < 3; i++)
        {
            ScriptMissao mTenda = ScriptableObject.CreateInstance<ScriptMissao>();
            mTenda.nomeDaMissao = $"Construir Tenda {i+1}";
            mTenda.solicitante = "Supervisor AI - Base";
            mTenda.tipoAcao = ActionType.ConstructBuilding;
            mTenda.prioridade = PriorityLevel.Medium;
            mTenda.custoEstimado = 300f;
            mTenda.objetoAlvo = prefabTenda;
            // Espalhando as posições (X + 20 a cada iteração)
            mTenda.posicaoAlvo = new Vector3(320 + (i * 30), 0, 650); 
            pacote.Add(mTenda);
        }

        // 3. Três Hangares (perto das tendas)
        for (int i = 0; i < 3; i++)
        {
            ScriptMissao mHangar = ScriptableObject.CreateInstance<ScriptMissao>();
            mHangar.nomeDaMissao = $"Construir Hangar {i+1}";
            mHangar.solicitante = "Supervisor AI - Veículos";
            mHangar.tipoAcao = ActionType.ConstructBuilding;
            mHangar.prioridade = PriorityLevel.Medium;
            mHangar.custoEstimado = 500f; // Chute
            mHangar.objetoAlvo = prefabHangar;
            // Perto das tendas (Z + 20)
            mHangar.posicaoAlvo = new Vector3(320 + (i * 30), 0, 650 + 40); 
            pacote.Add(mHangar);
        }

        // 4. Recrutar Navio de Guerra (após o Estaleiro)
        ScriptMissao mNavio = ScriptableObject.CreateInstance<ScriptMissao>();
        mNavio.nomeDaMissao = "Recrutar Navio de Guerra";
        mNavio.solicitante = "Supervisor AI - Frota";
        mNavio.tipoAcao = ActionType.RecruitUnit; 
        mNavio.prioridade = PriorityLevel.High;
        mNavio.custoEstimado = 1500f; // Custo de um Destroyer/Corveta
        mNavio.objetoAlvo = prefabNavioGuerra;
        pacote.Add(mNavio);

        return pacote;
    }

    // Método para testar (Chame via botão ou Start)
    public void InjetarNoRecebedor(RecebedorIA recebedor)
    {
        if (recebedor == null) return;

        List<ScriptMissao> missoes = GerarPacoteMissaoCompleta();
        foreach (var m in missoes)
        {
            // Adiciona na lista do Recebedor (convertendo a lógica interna dele)
            // Como o Recebedor processa ScriptMissao na lista pública ou via método direto
            // Vamos usar o método direto para injetar runtime
            recebedor.ReceberPedido(
                m.solicitante,
                m.tipoAcao,
                m.posicaoAlvo,
                m.objetoAlvo,
                m.custoEstimado,
                m.prioridade
            );
        }
        Debug.Log($"[MissionGenerator] Injetadas {missoes.Count} missões no Recebedor!");
    }
}
