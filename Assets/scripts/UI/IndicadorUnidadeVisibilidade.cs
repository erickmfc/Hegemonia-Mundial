using UnityEngine;

/// <summary>
/// Regra unica para impedir que indicadores de unidades atravessem menus e HUDs.
/// </summary>
public static class IndicadorUnidadeVisibilidade
{
    public const float RaioMaximoCombustivelBaixo = 400f;

    public static bool ExisteMenuOuModoDeInterfaceAberto
    {
        get
        {
            if (MenuConstrucao.EstaAberto
                || MenuGoverno.EstaAberto
                || MenuMisseis.EstaAberto
                || MenuPier.EstaAberto
                || MapaGeralController.EstaAberto
                || Fazenda.QualquerFazendaAberta
                || FazendaMenuController.EstaAberto
                || FabricaMineriosMenuController.EstaAberto
                || AudioSettingsPanelUI.EstaAberto
                || GestorMenusExclusivos.TemMenuAtivo)
            {
                return true;
            }

            if (MenuComandoController.Instancia != null && MenuComandoController.Instancia.MenuAberto)
            {
                return true;
            }

            InteractionModeSnapshot snapshot = InteractionModeService.CurrentSnapshot();
            return snapshot.HasOwner;
        }
    }

    public static bool EstaDentroDoRaioDaCamera(Transform alvo, Camera camera, float raio)
    {
        if (alvo == null || camera == null)
        {
            return false;
        }

        float raioSeguro = Mathf.Max(0f, raio);
        return (alvo.position - camera.transform.position).sqrMagnitude <= raioSeguro * raioSeguro;
    }
}
