using UnityEngine;
using System.Collections.Generic;

namespace Hegemonia.Menus.Comandos
{
    [CreateAssetMenu(fileName = "CMD_Seguir", menuName = "Hegemonia/Comandos/Seguir")]
    public class ComandoSeguir : ComandoMenu
    {
        public override void Executar(List<GameObject> unidadesSelecionadas)
        {
            if (unidadesSelecionadas == null || unidadesSelecionadas.Count == 0)
            {
                Debug.LogWarning("Seguir ignorado: nenhuma unidade selecionada.");
                return;
            }

            DesenharLinhasOrdem desenhador = Object.FindFirstObjectByType<DesenharLinhasOrdem>();
            if (desenhador == null)
            {
                Debug.LogWarning("DesenharLinhasOrdem nao encontrado na cena. Nao foi possivel entrar no modo seguir.");
                return;
            }

            desenhador.IniciarModoSeguir(unidadesSelecionadas);
        }
    }
}

