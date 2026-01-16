using UnityEngine;
using System.Collections.Generic;
using Hegemonia.Menus.Comandos;

namespace Hegemonia.Units
{
    // Coloque este script no Prefab da Unidade (Helicóptero, Tanque, etc)
    // Aqui você define quais botões vão aparecer quando clicar nele!
    public class UnidadeComandos : MonoBehaviour
    {
        [Header("Botões do Menu")]
        [Tooltip("Arraste aqui os arquivos de Comando (ex: CMD_Patrulhar)")]
        public List<ComandoMenu> comandosDestaUnidade = new List<ComandoMenu>();
    }
}
