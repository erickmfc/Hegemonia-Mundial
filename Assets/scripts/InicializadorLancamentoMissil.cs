using UnityEngine;

/// <summary>
/// Ponto único de compatibilidade para os prefabs de armamento existentes.
///
/// Há muitos lançadores legados no projeto e cada um conhecia apenas alguns
/// componentes de míssil. Quando um prefab diferente era usado, ele era
/// criado e a munição era consumida, mas nenhum controlador recebia o alvo.
/// Este despachante não cria um novo sistema de voo: apenas chama a API já
/// existente do componente que está no prefab.
/// </summary>
public static class InicializadorLancamentoMissil
{
    public static bool Inicializar(
        GameObject missil,
        Vector3 destino,
        Transform alvoMovel,
        Component origem,
        Transform lancador,
        GameObject dono,
        bool lancamentoSubmerso = false,
        Vector3 velocidadeInicial = default)
    {
        if (missil == null)
        {
            return false;
        }

        Vector3 alvoFinal = alvoMovel != null ? alvoMovel.position : destino;
        GameObject donoFinal = dono != null ? dono : (origem != null ? origem.gameObject : null);

        // O componente estratégico configura o próprio tracker durante o
        // lançamento; os demais são registrados no final deste método.
        MisselEstrategicoLongoAlcance estrategico = missil.GetComponent<MisselEstrategicoLongoAlcance>();
        if (estrategico != null)
        {
            estrategico.IniciarLancamento(alvoFinal, false, origem, alvoMovel);
            return true;
        }

        MisselNaval naval = missil.GetComponent<MisselNaval>();
        if (naval != null)
        {
            naval.IniciarAtaque(alvoFinal, alvoMovel, lancador);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        Torpedo torpedo = missil.GetComponent<Torpedo>();
        if (torpedo != null)
        {
            if (alvoMovel != null) torpedo.DefinirAlvo(alvoMovel);
            else torpedo.DefinirAlvo(destino);

            IdentidadeUnidade identidade = origem != null
                ? origem.GetComponentInParent<IdentidadeUnidade>()
                : null;
            torpedo.DefinirLancador(lancador, identidade != null ? identidade.teamID : -1);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        MisselSubmarino submarino = missil.GetComponent<MisselSubmarino>();
        if (submarino != null)
        {
            submarino.IniciarLancamento(alvoFinal, lancamentoSubmerso, alvoMovel);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        MisselCaca caca = missil.GetComponent<MisselCaca>();
        if (caca != null)
        {
            Vector3 velocidade = velocidadeInicial;
            if (velocidade.sqrMagnitude < 0.01f && lancador != null)
            {
                Rigidbody corpoLancador = lancador.GetComponentInParent<Rigidbody>();
                if (corpoLancador != null) velocidade = corpoLancador.linearVelocity;
            }

            if (velocidade.sqrMagnitude < 0.01f && lancador != null)
            {
                velocidade = lancador.forward * 40f;
            }

            caca.IniciarAtaque(alvoFinal, velocidade, alvoMovel);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        MisselBombardeiro bombardeiro = missil.GetComponent<MisselBombardeiro>();
        if (bombardeiro != null)
        {
            if (alvoMovel != null) bombardeiro.IniciarLancamentoRastreado(alvoMovel, donoFinal);
            else bombardeiro.IniciarLancamento(destino, donoFinal);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        MisselLeopardAutomatico leopard = missil.GetComponent<MisselLeopardAutomatico>();
        if (leopard != null && alvoMovel != null)
        {
            leopard.DefinirAlvo(alvoMovel);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        MissilTeleguiado teleguiado = missil.GetComponent<MissilTeleguiado>();
        if (teleguiado != null)
        {
            if (alvoMovel != null) teleguiado.DefinirAlvo(alvoMovel);
            else teleguiado.DefinirAlvo(destino);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        MisselTatico tatico = missil.GetComponent<MisselTatico>();
        if (tatico != null)
        {
            tatico.IniciarLancamento(alvoFinal, alvoMovel);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        MisselICBM icbm = missil.GetComponent<MisselICBM>();
        if (icbm != null)
        {
            icbm.IniciarLancamento(alvoFinal, alvoMovel);
            Registrar(missil, origem, alvoFinal, alvoMovel);
            return true;
        }

        // Compatibilidade final para prefabs antigos de projétil. Ele segue
        // em direção ao ponto solicitado e não procura outro alvo por conta
        // própria, preservando o destino do comando original.
        Projetil projetil = missil.GetComponent<Projetil>();
        if (projetil == null) projetil = missil.AddComponent<Projetil>();
        projetil.SetDono(donoFinal);

        Vector3 pontoDeMira = alvoMovel != null
            ? GuidagemAlvoMovel.ObterPontoDeMira(alvoMovel, missil.transform.position, MissileThreatTracker.EstimarVelocidade(missil))
            : destino;
        Vector3 direcao = pontoDeMira - missil.transform.position;
        if (direcao.sqrMagnitude > 0.0001f) projetil.SetDirecao(direcao.normalized);
        Registrar(missil, origem, alvoFinal, alvoMovel);
        return true;
    }

    private static void Registrar(GameObject missil, Component origem, Vector3 destino, Transform alvoMovel)
    {
        MissileThreatTracker.RegistrarLancamento(
            missil,
            origem,
            destino,
            alvoMovel,
            MissileThreatTracker.EstimarVelocidade(missil));
    }
}
