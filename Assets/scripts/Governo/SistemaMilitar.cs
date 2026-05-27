using UnityEngine;
using System.Collections.Generic;

public static class SistemaMilitar
{
    private static List<IdentidadeUnidade> unidadesBuffer = new List<IdentidadeUnidade>();

    public static void Processar(DadosPaisGoverno pais, DadosEconomiaPais economia)
    {
        if (pais == null) return;

        // 1. Lógica de Alistamento
        float taxaAlistamento = Mathf.Lerp(0.01f, 0.25f, pais.felicidade / 100f);
        int limiteAlistaveis = Mathf.FloorToInt((pais.populacaoCivil + pais.alistaveis) * taxaAlistamento);
        
        if (pais.alistaveis < limiteAlistaveis)
        {
            int delta = Mathf.Min(pais.populacaoCivil, limiteAlistaveis - pais.alistaveis);
            int convertidoAgota = Mathf.Clamp(Mathf.CeilToInt(delta * 0.05f), 1, 500);
            if (convertidoAgota <= pais.populacaoCivil)
            {
                pais.populacaoCivil -= convertidoAgota;
                pais.alistaveis += convertidoAgota;
            }
        }
        else if (pais.alistaveis > limiteAlistaveis)
        {
            int delta = pais.alistaveis - limiteAlistaveis;
            int convertidoAgota = Mathf.Clamp(Mathf.CeilToInt(delta * 0.05f), 1, 500);
            pais.alistaveis -= convertidoAgota;
            pais.populacaoCivil += convertidoAgota;
        }

        // 2. Despesas de Manutenção (Salário)
        float despesaMilitar = (pais.populacaoMilitarAtiva * 1.0f) + (pais.reservistas * 0.5f);
        
        if (economia != null)
        {
            economia.custoManutencao += despesaMilitar;
        }
        else
        {
            pais.saldo -= Mathf.CeilToInt(despesaMilitar);
        }

        // 3. Consumo Logístico Contínuo das Unidades
        RegistroEntidadesJogo.FillUnidades(unidadesBuffer);
        float energiaUnidades = 0f;
        float combustivelUnidades = 0f;
        
        for (int i = 0; i < unidadesBuffer.Count; i++)
        {
            var u = unidadesBuffer[i];
            if (u != null && u.teamID == pais.teamId)
            {
                energiaUnidades += u.energiaConsumida;
                combustivelUnidades += u.combustivelPorHora;
            }
        }
        
        if (economia != null)
        {
            economia.energiaConsumida += energiaUnidades;
            economia.combustivelConsumido += combustivelUnidades;
        }
    }
}
