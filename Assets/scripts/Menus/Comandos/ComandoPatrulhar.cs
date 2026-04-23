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
            DesenharLinhasOrdem desenhador = Object.FindFirstObjectByType<DesenharLinhasOrdem>();
            if (desenhador == null)
            {
                Debug.LogWarning("DesenharLinhasOrdem nao encontrado na cena. Nao foi possivel entrar no modo patrulha.");
                return;
            }

            desenhador.IniciarModoPatrulha();
        }
    }
}
