using UnityEngine;
using System.Collections.Generic;

namespace Hegemonia.Menus.Comandos
{
    [CreateAssetMenu(fileName = "ComandoPassivo", menuName = "Hegemonia/Comandos/Passivo")]
    public class ComandoPassivo : ComandoMenu
    {
        public override void Executar(List<GameObject> unidades)
        {
            foreach (GameObject unidade in unidades)
            {
                if (unidade == null) continue;

                ControleUnidade controle = unidade.GetComponent<ControleUnidade>();
                if (controle != null && controle.DefinirModoCombate(false))
                {
                    Debug.Log($"[ComandoPassivo] {unidade.name}: modo passivo ativado");
                }
                else
                {
                    Debug.LogWarning($"[ComandoPassivo] {unidade.name} nao possui sistema de combate configurado");
                }
            }
        }
    }
}
