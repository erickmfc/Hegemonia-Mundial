using UnityEngine;
using System.Collections.Generic;

namespace Hegemonia.Menus.Comandos
{
    // Base para todos os comandos de menu
    public abstract class ComandoMenu : ScriptableObject
    {
        [Header("Informações do Comando")]
        public string tituloBotao = "Novo Comando";
        [TextArea] public string descricao = "O que este comando faz?";

        // Função principal que cada script vai implementar
        public abstract void Executar(List<GameObject> unidadesSelecionadas);

        // Utilitário para pegar o componente certo
        protected T GetScript<T>(GameObject obj) where T : Component
        {
            return obj.GetComponent<T>();
        }
    }
}
