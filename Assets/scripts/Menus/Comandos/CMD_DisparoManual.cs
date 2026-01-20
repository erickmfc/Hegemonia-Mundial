using UnityEngine;
using System.Collections.Generic;

namespace Hegemonia.Menus.Comandos
{
    [CreateAssetMenu(fileName = "CMD_DisparoManual", menuName = "Hegemonia/Comandos/Disparo Manual")]
    public class CMD_DisparoManual : ComandoMenu
    {
        public override void Executar(List<GameObject> unidadesSelecionadas)
        {
            foreach (var unidade in unidadesSelecionadas)
            {
                if (unidade == null) continue;

                // Tenta pegar o lançador
                LancadorMultiplo lancador = unidade.GetComponent<LancadorMultiplo>();
                
                if (lancador != null)
                {
                    lancador.DispararManual();
                }
                else
                {
                    // Tenta nos filhos
                    lancador = unidade.GetComponentInChildren<LancadorMultiplo>();
                    if(lancador != null) lancador.DispararManual();
                }
            }
        }
    }
}
