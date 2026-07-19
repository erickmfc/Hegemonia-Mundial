using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Registro central das nações presentes na partida. Ele só mantém referências
/// aos dados; a decisão econômica/militar continua pertencendo a cada país.
public sealed class RegistroNacoesGoverno : MonoBehaviour
{
    public static RegistroNacoesGoverno Instancia { get; private set; }
    private readonly Dictionary<int, DadosPaisGoverno> porId = new Dictionary<int, DadosPaisGoverno>();

    public IReadOnlyList<DadosPaisGoverno> Todos => porId.Values.Where(p => p != null).OrderBy(p => p.teamId).ToList();

    public static void GarantirInstancia()
    {
        if (Instancia != null) return;
        RegistroNacoesGoverno existente = FindFirstObjectByType<RegistroNacoesGoverno>();
        if (existente != null) { Instancia = existente; return; }
        GameObject go = new GameObject("RegistroNacoesGoverno_Runtime");
        Instancia = go.AddComponent<RegistroNacoesGoverno>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Registrar(DadosPaisGoverno pais)
    {
        if (pais != null && pais.teamId > 0) porId[pais.teamId] = pais;
    }

    public void Sincronizar(IEnumerable<DadosPaisGoverno> paises)
    {
        porId.Clear();
        if (paises == null) return;
        foreach (DadosPaisGoverno pais in paises) Registrar(pais);
    }
}
