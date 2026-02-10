using UnityEngine;
using System.Collections.Generic;
using Hegemonia.Menus.Comandos; 
using Hegemonia.Units; 

// Implementação Concreta para uso em tempo de execução
public class ComandoLeopardRuntime : ComandoMenu
{
    private System.Action<List<GameObject>> acao;

    // Inicializa o ScriptableObject em memória
    public void Configurar(string titulo, System.Action<List<GameObject>> acaoExecutar)
    {
        this.tituloBotao = titulo;
        this.acao = acaoExecutar;
    }

    public override void Executar(List<GameObject> unidadesSelecionadas)
    {
        if (acao != null) acao.Invoke(unidadesSelecionadas);
    }
}

public class ComandosLeopard : UnidadeComandos 
{
    void Awake()
    {
        // Limpa a lista padrão do Inspector
        comandosDestaUnidade = new List<ComandoMenu>();

        // Cria os comandos em memória
        comandosDestaUnidade.Add(CriarComando("ATIVO 🚨", (objs) => DefinirModo(objs, true)));
        comandosDestaUnidade.Add(CriarComando("PASSIVO 💤", (objs) => DefinirModo(objs, false)));
    }

    ComandoLeopardRuntime CriarComando(string titulo, System.Action<List<GameObject>> acao)
    {
        // ScriptableObject.CreateInstance é o jeito certo de dar 'new' em ScriptableObjects
        var cmd = ScriptableObject.CreateInstance<ComandoLeopardRuntime>();
        cmd.Configurar(titulo, acao);
        return cmd;
    }

    void DefinirModo(List<GameObject> unidades, bool ativo)
    {
        foreach(var u in unidades)
        {
            var l = u.GetComponent<LancadorMLRS>();
            if(l != null) 
            {
                l.modoCombateAtivo = ativo;
                Debug.Log($"🐆 Leopard {u.name} agora está: {(ativo ? "ATIVO" : "PASSIVO")}");
            }
        }
    }
}
