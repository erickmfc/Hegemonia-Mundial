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

                // Tenta encontrar ControleTorreta (usado em navios, helicópteros, torres)
                ControleTorreta[] torretas = unidade.GetComponentsInChildren<ControleTorreta>();
                
                if (torretas != null && torretas.Length > 0)
                {
                    foreach (var torreta in torretas)
                    {
                        torreta.modoPassivo = true;
                        Debug.Log($"✅ {unidade.name}: MODO PASSIVO ativado (não atacará automaticamente)");
                    }
                }
                else
                {
                    Debug.LogWarning($"⚠️ {unidade.name} não possui sistema de ataque automático (ControleTorreta)");
                }
            }
        }
    }
}
