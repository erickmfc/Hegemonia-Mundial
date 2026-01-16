using UnityEngine;
using System.Collections.Generic;

namespace Hegemonia.Menus.Comandos
{
    [CreateAssetMenu(fileName = "CMD_Exemplo", menuName = "Hegemonia/Comandos/Exemplo")]
    public class ComandoExemplo : ComandoMenu
    {
        public override void Executar(List<GameObject> unidadesSelecionadas)
        {
            Debug.Log($"Executando comando EXEMPLO em {unidadesSelecionadas.Count} unidades.");
            foreach(var unit in unidadesSelecionadas)
            {
                // Lógica aqui...
            }
        }
    }
}
