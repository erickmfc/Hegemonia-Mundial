using System;

namespace Hegemonia.AI.IA03
{
    // Números menores representam crises mais graves: N1 é guerra total e N4 é tensão.
    public enum IA03NivelConflito
    {
        Paz = 0,
        GuerraTotal = 1,
        ConflitoLimitado = 2,
        AvancoMilitar = 3,
        Tensao = 4
    }

    public enum IA03EstadoNacional
    {
        Paz,
        Tensao,
        Mobilizacao,
        ConflitoLimitado,
        GuerraTotal,
        Negociando,
        CessarFogo,
        Recuperacao
    }

    public enum IA03DominioEstrategico
    {
        Terrestre,
        Naval,
        Aereo,
        Combinado,
        Diplomatico
    }

    public enum IA03TipoCreaty
    {
        PatrulhaTerrestre,
        PatrulhaNaval,
        PatrulhaAerea,
        PatrulhaSubmarino,
        TensaoN4,
        AvancoN3,
        ConflitoN2,
        GuerraN1,
        ZonaPortaAvioes,
        EmbarqueTransporte,
        DesembarqueAnfibio,
        HelipontoDiplomatico,
        RecepcaoPresidencial,
        PontoGenerico
    }

    public enum IA03AlvoPreferencial
    {
        Nenhum,
        Aeroporto,
        Estaleiro,
        Radar,
        BaseMilitar,
        Infraestrutura,
        Cidade,
        DefesaAerea,
        Navios,
        Tropas
    }

    public enum IA03TipoOrdem
    {
        Mover,
        Atacar,
        Patrulhar
    }

    public enum IA03TipoMissao
    {
        Patrulha,
        Avanco,
        AtaqueLimitado,
        GuerraTotal,
        GrupoPortaAvioes,
        PatrulhaSubmarino,
        InvasaoAnfibia,
        VisitaPresidencial,
        DefesaDeObjetivo
    }

    public enum IA03CondicaoMissao
    {
        ChegarAoDestino,
        PermanecerNoDestino,
        DestruirAlvo,
        SobreviverAteOPrazo,
        ConfirmacaoExterna,
        CapturarTerritorio
    }

    public enum IA03NivelEconomico
    {
        MuitoFraco,
        Fraco,
        Medio,
        Forte,
        Potencia
    }
}
