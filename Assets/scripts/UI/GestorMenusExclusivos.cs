using UnityEngine;
using UnityEngine.EventSystems;

public static class GestorMenusExclusivos
{
    private static Object donoMenuAtivo;
    private static Object donoAreaBloqueio;
    private static Rect areaBloqueioAtual;
    private static int ultimoFrameArea = -1;

    public static void Abrir(Object dono)
    {
        if (dono == null) return;
        donoMenuAtivo = dono;
    }

    public static void Fechar(Object dono)
    {
        if (dono == null) return;

        if (donoMenuAtivo == dono)
        {
            donoMenuAtivo = null;
        }

        if (donoAreaBloqueio == dono)
        {
            donoAreaBloqueio = null;
            areaBloqueioAtual = default;
            ultimoFrameArea = -1;
        }
    }

    public static bool EstaAtivo(Object dono)
    {
        return dono != null && donoMenuAtivo == dono;
    }

    public static void RegistrarAreaBloqueio(Object dono, Rect area)
    {
        if (!EstaAtivo(dono))
        {
            return;
        }

        donoAreaBloqueio = dono;
        areaBloqueioAtual = area;
        ultimoFrameArea = Time.frameCount;
    }

    public static bool CliqueBloqueadoPelaUI()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        if (donoMenuAtivo == null || donoAreaBloqueio == null || ultimoFrameArea < 0)
        {
            return false;
        }

        if (ultimoFrameArea != Time.frameCount && ultimoFrameArea != Time.frameCount - 1)
        {
            return false;
        }

        Vector3 mouse = Input.mousePosition;
        Vector2 mouseGui = new Vector2(mouse.x, Screen.height - mouse.y);
        return areaBloqueioAtual.Contains(mouseGui);
    }
}
