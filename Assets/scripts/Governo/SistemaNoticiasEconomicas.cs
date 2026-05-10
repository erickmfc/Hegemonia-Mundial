using UnityEngine;

public class SistemaNoticiasEconomicas : MonoBehaviour
{
    public static SistemaNoticiasEconomicas Instancia { get; private set; }

    public float intervaloNoticias = 24f;
    public float impactoRestante;
    public string noticiaAtual = string.Empty;

    private float proximoTick;

    private void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        impactoRestante = Mathf.MoveTowards(impactoRestante, 0f, Time.unscaledDeltaTime * 0.02f);
        if (Time.unscaledTime < proximoTick) return;
        proximoTick = Time.unscaledTime + Mathf.Max(8f, intervaloNoticias);
        AvaliarNoticias();
    }

    public float ModificadorEconomico
    {
        get { return impactoRestante; }
    }

    private void AvaliarNoticias()
    {
        SistemaGovernoMundial gov = SistemaGovernoMundial.Instancia;
        SistemaEconomiaImoveis economia = SistemaEconomiaImoveis.Instancia;
        if (gov == null || economia == null) return;

        foreach (DadosPaisGoverno pais in gov.Paises)
        {
            if (pais == null) continue;
            DadosEconomiaPais eco = economia.ObterEconomia(pais.teamId);
            if (eco == null) continue;

            if (eco.deficitEnergia > 2f)
            {
                Publicar(gov, "Crise de energia em " + pais.nomePais + ".", -0.20f);
                pais.estabilidade = Mathf.Clamp(pais.estabilidade - 3f, 0f, 100f);
                return;
            }

            if (eco.comidaProduzida > eco.populacaoTotal * 0.05f && eco.farms > 0)
            {
                Publicar(gov, "Safra recorde em " + pais.nomePais + ".", 0.14f);
                pais.estabilidade = Mathf.Clamp(pais.estabilidade + 1.5f, 0f, 100f);
                return;
            }

            if (eco.industriaProduzida > 12f && eco.deficitEnergia <= 0f)
            {
                Publicar(gov, "Boom industrial em " + pais.nomePais + ".", 0.12f);
                return;
            }

            if (pais.inflacao > 15f)
            {
                Publicar(gov, "Inflacao em alta em " + pais.nomePais + ".", -0.12f);
                return;
            }
        }
    }

    private void Publicar(SistemaGovernoMundial gov, string noticia, float impacto)
    {
        noticiaAtual = noticia;
        impactoRestante = Mathf.Clamp(impactoRestante + impacto, -0.35f, 0.35f);
        gov.RegistrarNoticia(noticia);
        SistemaMercadoGlobal.Instancia?.SimularMercado();
    }
}
