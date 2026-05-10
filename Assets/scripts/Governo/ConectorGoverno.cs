public static class ConectorGoverno
{
    public static DadosPaisGoverno ObterPais(int teamId)
    {
        SistemaGovernoMundial.GarantirInstancia();
        return SistemaGovernoMundial.Instancia != null ? SistemaGovernoMundial.Instancia.ObterPais(teamId) : null;
    }

    public static void NotificarGuerra(int teamId)
    {
        SistemaGovernoMundial.GarantirInstancia();
        SistemaGovernoMundial.Instancia?.NotificarGuerra(teamId);
    }

    public static void AlterarEmprego(int teamId, float valor)
    {
        SistemaGovernoMundial.GarantirInstancia();
        SistemaGovernoMundial.Instancia?.AlterarEmprego(teamId, valor);
    }

    public static void AlterarMoradia(int teamId, float valor)
    {
        SistemaGovernoMundial.GarantirInstancia();
        SistemaGovernoMundial.Instancia?.AlterarMoradia(teamId, valor);
    }

    public static bool RegistrarCompraMercado(int compradorTeamId, int vendedorTeamId, string itemId, int quantidade)
    {
        SistemaGovernoMundial.GarantirInstancia();
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (mercado == null) return false;
        return mercado.Comprar(compradorTeamId, vendedorTeamId, itemId, quantidade, out _);
    }

    public static bool RegistrarVendaMercado(int vendedorTeamId, int compradorTeamId, string itemId, int quantidade)
    {
        SistemaGovernoMundial.GarantirInstancia();
        SistemaMercadoGlobal mercado = SistemaMercadoGlobal.Instancia;
        if (mercado == null) return false;
        return mercado.Vender(vendedorTeamId, compradorTeamId, itemId, quantidade, out _);
    }
}
