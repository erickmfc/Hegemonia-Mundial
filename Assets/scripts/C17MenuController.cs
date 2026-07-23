using UnityEngine;
using Hegemonia.Menus;

namespace Hegemonia.Aeronaves.C17
{
    [RequireComponent(typeof(C17TransporteController))]
    public sealed class C17MenuController : MonoBehaviour
    {
        private C17TransporteController controlador;
        private bool aberto;

        private void Awake() => controlador = GetComponent<C17TransporteController>();

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.O) && controlador != null && controlador.EstaSelecionado()) AlternarMenuO();
        }

        public void AlternarMenuO()
        {
            aberto = !aberto;
            if (GerenteDeComandos.Instance == null) return;
            GerenteDeComandos.Instance.LimparComandos();
            if (!aberto) return;

            EstadoAviaoTransporte estado = controlador.EstadoAtual;
            if (estado == EstadoAviaoTransporte.Pousado || estado == EstadoAviaoTransporte.Estacionado)
            {
                GerenteDeComandos.Instance.RegistrarComando("Direcionar", controlador.ComandoZ_Direcionar, Color.cyan);
                GerenteDeComandos.Instance.RegistrarComando("Carregar tropas", controlador.Comando_EmbarcarTropas, Color.green);
                GerenteDeComandos.Instance.RegistrarComando("Carregar veiculos", controlador.Comando_EmbarcarVeiculos, Color.green);
                GerenteDeComandos.Instance.RegistrarComando("Consultar carga", controlador.ExibirStatusCarga, Color.gray);
            }
            else
            {
                GerenteDeComandos.Instance.RegistrarComando("Marcar pouso", controlador.IniciarModoMarcaacaoPouso, Color.yellow);
                GerenteDeComandos.Instance.RegistrarComando("Redirecionar", controlador.ComandoZ_Direcionar, Color.cyan);
                GerenteDeComandos.Instance.RegistrarComando("Voltar a base", controlador.ComandoZ_VoltarAeroporto, Color.blue);
                GerenteDeComandos.Instance.RegistrarComando("Consultar carga", controlador.ExibirStatusCarga, Color.gray);
            }
        }
    }
}
