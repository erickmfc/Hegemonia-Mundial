using UnityEngine;
using System.Collections.Generic;

namespace Hegemonia.Menus.Comandos
{
    [CreateAssetMenu(fileName = "CMD_Patrulhar", menuName = "Hegemonia/Comandos/Patrulhar")]
    public class ComandoPatrulhar : ComandoMenu
    {
        public override void Executar(List<GameObject> unidadesSelecionadas)
        {
            Debug.Log($"Iniciando Protocolo de Patrulha em {unidadesSelecionadas.Count} unidades.");

            foreach(var unit in unidadesSelecionadas)
            {
                // Verifica se já tem o script para não duplicar
                ComportamentoPatrulha patrulha = unit.GetComponent<ComportamentoPatrulha>();
                if (patrulha == null)
                {
                    patrulha = unit.AddComponent<ComportamentoPatrulha>();
                }
                
                // Reinicia a patrulha (redefine centro etc)
                patrulha.enabled = true;
            }
        }
    }
}
