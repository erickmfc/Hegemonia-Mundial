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

                // Tenta encontrar ControleTorreta (usado em navios, helicópteros, torres)
                ControleTorreta[] torretas = unidade.GetComponentsInChildren<ControleTorreta>();
                
                if (torretas != null && torretas.Length > 0)
                {
                    foreach (var torreta in torretas)
                    {
                        torreta.modoPassivo = false;
                        Debug.Log($"✅ {unidade.name}: MODO ATIVO ativado (atacará automaticamente)");
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
