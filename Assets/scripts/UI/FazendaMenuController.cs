using UnityEngine;

/// <summary>
/// Compatibilidade para referências antigas. Fazendas são prédios passivos e
/// não possuem menu ou painel de controle.
/// </summary>
[DisallowMultipleComponent]
public sealed class FazendaMenuController : MonoBehaviour
{
    public static FazendaMenuController Instancia { get; private set; }
    public static bool EstaAberto => false;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    public static bool AbrirPara(Fazenda fazenda) => false;
    public static void FecharSeAbertoPara(Fazenda fazenda) { }
    public void FecharParaOutraInterface() { }
}
