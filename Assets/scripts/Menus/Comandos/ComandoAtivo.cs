using UnityEngine;
using System.Collections.Generic;

namespace Hegemonia.Menus.Comandos
{
    [CreateAssetMenu(fileName = "ComandoAtivo", menuName = "Hegemonia/Comandos/Ativo")]
    public class ComandoAtivo : ComandoMenu
    {
        public override void Executar(List<GameObject> unidades)
        {
            foreach (GameObject unidade in unidades)
            {
                if (unidade == null) continue;

                ControleUnidade controle = unidade.GetComponent<ControleUnidade>();
                if (controle != null && controle.DefinirModoCombate(true))
                {
                    Debug.Log($"[ComandoAtivo] {unidade.name}: modo ativo ativado");
                }
                else
                {
                    Debug.LogWarning($"[ComandoAtivo] {unidade.name} nao possui sistema de combate configurado");
                }
            }
        }
    }
}
