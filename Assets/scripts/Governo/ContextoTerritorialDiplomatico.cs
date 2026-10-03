using System;
using System.Collections.Generic;
using UnityEngine;
using Hegemonia.RTS;

public enum ModoOperacionalTerritorial
{
    Ativo,
    Passivo,
    Patrulha,
    Seguir
}

public enum AcaoRegrasEngajamento
{
    Ignorar,
    Acompanhar,
    Identificar,
    Interceptar,
    Alertar,
    Atacar
}

public struct ContextoPresencaTerritorial
{
    public int paisDaUnidade;
    public int paisDoTerritorio;
    public string territorioId;
    public bool territorioConhecido;
    public bool passagemAutorizada;
    public bool alianca;
    public bool emGuerra;
    public bool violacaoTerritorial;
    public bool zonaDesmilitarizada;
    public bool violacaoDesmilitarizacao;
    public AcaoRegrasEngajamento acao;
}

/// <summary>
/// Resolve a camada política entre detecção e combate. Não substitui radar,
/// identidade de equipe, nem o teste de guerra já usado pelo armamento.
/// </summary>
public static class ContextoTerritorialDiplomatico
{
    public static event Action<IdentidadeUnidade, ContextoPresencaTerritorial> OnViolacaoTerritorial;
    public static event Action<IdentidadeUnidade, ResultadoConsultaTerritorio> OnTerritoryEntered;
    public static event Action<IdentidadeUnidade, ResultadoConsultaTerritorio> OnTerritoryExited;
    public static event Action<IdentidadeUnidade, ResultadoConsultaTerritorio> OnTerritoryChanged;

    private static readonly Dictionary<int, string> violacaoAtualPorUnidade = new Dictionary<int, string>();
    private static readonly Dictionary<int, UnidadeTerritorioConhecido> territorioConhecidoPorUnidade = new Dictionary<int, UnidadeTerritorioConhecido>();
    private static GerenteDeTerritorio gerenteObservado;

    private sealed class UnidadeTerritorioConhecido
    {
        public IdentidadeUnidade unidade;
        public ResultadoConsultaTerritorio territorio;
    }

    public static bool DefinirPermissaoDePassagem(int teamOrigem, int teamDestino, MeioPassagemTerritorial meio, bool permitida)
    {
        if (teamOrigem <= 0 || teamDestino <= 0 || teamOrigem == teamDestino) return false;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return false;
        RelacaoPaisGoverno relacao = governo.ObterRelacao(teamOrigem, teamDestino);
        if (relacao == null) return false;
        switch (meio)
        {
            case MeioPassagemTerritorial.Terrestre: relacao.passagemTerrestrePermitida = permitida; break;
            case MeioPassagemTerritorial.Aereo: relacao.passagemAereaPermitida = permitida; break;
            case MeioPassagemTerritorial.Naval: relacao.passagemNavalPermitida = permitida; break;
            default: return false;
        }
        governo.NotificarGovernoAtualizado();
        return true;
    }

    public static bool DefinirPermissaoTemporaria(int teamOrigem, int teamDestino, TimeSpan duracao)
    {
        if (teamOrigem <= 0 || teamDestino <= 0 || teamOrigem == teamDestino || duracao <= TimeSpan.Zero) return false;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return false;
        RelacaoPaisGoverno relacao = governo.ObterRelacao(teamOrigem, teamDestino);
        if (relacao == null) return false;
        relacao.passagemTemporariaAteUtcTicks = DateTime.UtcNow.Add(duracao).Ticks;
        governo.NotificarGovernoAtualizado();
        return true;
    }

    public static bool PassagemPermitida(int teamOrigem, int teamDestino, MeioPassagemTerritorial meio)
    {
        if (teamOrigem <= 0 || teamDestino <= 0 || teamOrigem == teamDestino) return teamOrigem == teamDestino;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        RelacaoPaisGoverno relacao = governo != null ? governo.ObterRelacao(teamOrigem, teamDestino) : null;
        if (relacao == null) return false;
        if (relacao.pactoMilitar || relacao.PosturaDe(teamOrigem) == PosturaRelacaoPais.Amigo) return true;
        if (relacao.passagemTemporariaAteUtcTicks > DateTime.UtcNow.Ticks) return true;
        switch (meio)
        {
            case MeioPassagemTerritorial.Terrestre: return relacao.passagemTerrestrePermitida;
            case MeioPassagemTerritorial.Aereo: return relacao.passagemAereaPermitida;
            case MeioPassagemTerritorial.Naval: return relacao.passagemNavalPermitida;
            default: return false;
        }
    }

    public static ContextoPresencaTerritorial AvaliarPresenca(
        IdentidadeUnidade unidade,
        MeioPassagemTerritorial meio,
        ModoOperacionalTerritorial modo,
        bool radarDetectou = true,
        bool defesaPropria = false)
    {
        ContextoPresencaTerritorial contexto = new ContextoPresencaTerritorial
        {
            paisDaUnidade = unidade != null ? unidade.teamID : 0,
            paisDoTerritorio = -1,
            territorioId = string.Empty,
            acao = AcaoRegrasEngajamento.Ignorar
        };
        if (unidade == null || !radarDetectou) return contexto;

        GerenteDeTerritorio gerente = GerenteDeTerritorio.Instancia;
        if (gerente == null) return contexto;
        ResultadoConsultaTerritorio territorio = AtualizarTerritorioUnidade(unidade);
        if (!territorio.encontrouRegiao) return contexto;

        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (territorio.tipo == TipoRegiaoPolitica.Terra
            && governo != null
            && governo.EstaRegiaoDesmilitarizada(unidade.teamID, territorio.territorioId))
        {
            RemoverViolacaoAtual(unidade);
            contexto.territorioConhecido = true;
            contexto.territorioId = territorio.territorioId;
            contexto.paisDoTerritorio = territorio.ownerCountryTeamId;
            contexto.zonaDesmilitarizada = true;
            contexto.violacaoDesmilitarizacao = true;
            contexto.acao = AcaoRegrasEngajamento.Alertar;
            return contexto;
        }

        contexto.territorioConhecido = true;
        contexto.territorioId = territorio.territorioId;
        contexto.paisDoTerritorio = territorio.ownerCountryTeamId;
        if (territorio.aguasInternacionais || territorio.neutral || territorio.ownerCountryTeamId <= 0)
        {
            RemoverViolacaoAtual(unidade);
            contexto.acao = modo == ModoOperacionalTerritorial.Seguir ? AcaoRegrasEngajamento.Acompanhar : AcaoRegrasEngajamento.Identificar;
            return contexto;
        }

        if (territorio.ownerCountryTeamId == unidade.teamID)
        {
            RemoverViolacaoAtual(unidade);
            contexto.acao = modo == ModoOperacionalTerritorial.Seguir ? AcaoRegrasEngajamento.Acompanhar : AcaoRegrasEngajamento.Identificar;
            return contexto;
        }

        RelacaoPaisGoverno relacao = governo != null && unidade.teamID > 0
            ? governo.ObterRelacao(unidade.teamID, territorio.ownerCountryTeamId)
            : null;
        contexto.alianca = relacao != null && (relacao.pactoMilitar || relacao.PosturaDe(unidade.teamID) == PosturaRelacaoPais.Amigo);
        contexto.emGuerra = relacao != null
            && !relacao.cessarFogoAtivo
            && (relacao.guerraDeclarada || RTSVisibilityService.TeamsAtWar(unidade.teamID, territorio.ownerCountryTeamId));
        contexto.passagemAutorizada = contexto.alianca || PassagemPermitida(unidade.teamID, territorio.ownerCountryTeamId, meio);
        contexto.violacaoTerritorial = !contexto.passagemAutorizada && !contexto.alianca;

        if (!contexto.violacaoTerritorial)
        {
            RemoverViolacaoAtual(unidade);
            contexto.acao = modo == ModoOperacionalTerritorial.Seguir ? AcaoRegrasEngajamento.Acompanhar : AcaoRegrasEngajamento.Identificar;
            return contexto;
        }

        RegistrarNovaViolacao(unidade, contexto);
        // A violação produz alerta/interceptação contextual; não inicia fogo por si só.
        if (defesaPropria && contexto.emGuerra) contexto.acao = AcaoRegrasEngajamento.Atacar;
        else if (modo == ModoOperacionalTerritorial.Seguir) contexto.acao = AcaoRegrasEngajamento.Acompanhar;
        else if (modo == ModoOperacionalTerritorial.Ativo && contexto.emGuerra) contexto.acao = AcaoRegrasEngajamento.Interceptar;
        else contexto.acao = AcaoRegrasEngajamento.Alertar;
        return contexto;
    }

    /// <summary>
    /// Chamado quando uma unidade muda de posição de forma relevante; mantém
    /// o território anterior e publica eventos sem exigir consulta em Update.
    /// </summary>
    public static ResultadoConsultaTerritorio AtualizarTerritorioUnidade(IdentidadeUnidade unidade)
    {
        if (unidade == null || GerenteDeTerritorio.Instancia == null) return ResultadoConsultaTerritorio.NaoDefinido;
        GarantirAssinaturaTerritorio();
        ResultadoConsultaTerritorio atual = GerenteDeTerritorio.Instancia.ObterTerritorioNaPosicao(unidade.transform.position);
        int id = unidade.GetInstanceID();
        if (!territorioConhecidoPorUnidade.TryGetValue(id, out UnidadeTerritorioConhecido anterior))
        {
            territorioConhecidoPorUnidade[id] = new UnidadeTerritorioConhecido { unidade = unidade, territorio = atual };
            if (atual.encontrouRegiao) OnTerritoryEntered?.Invoke(unidade, atual);
            return atual;
        }

        bool mudouId = anterior.territorio.territorioId != atual.territorioId
            || anterior.territorio.encontrouRegiao != atual.encontrouRegiao;
        bool mudouDono = anterior.territorio.ownerCountryTeamId != atual.ownerCountryTeamId
            || anterior.territorio.neutral != atual.neutral;
        if (mudouId)
        {
            if (anterior.territorio.encontrouRegiao) OnTerritoryExited?.Invoke(unidade, anterior.territorio);
            if (atual.encontrouRegiao) OnTerritoryEntered?.Invoke(unidade, atual);
        }
        if (mudouId || mudouDono) OnTerritoryChanged?.Invoke(unidade, atual);
        anterior.territorio = atual;
        return atual;
    }

    /// <summary>Gate curto para armas que já exigem guerra; radar e aquisição não usam este método.</summary>
    public static bool PodeDispararEmGuerra(int teamAtacante, int teamAlvo)
    {
        if (!RTSVisibilityService.TeamsAtWar(teamAtacante, teamAlvo)) return false;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo == null) return true;
        RelacaoPaisGoverno relacao = governo.ObterRelacao(teamAtacante, teamAlvo);
        return relacao == null || !relacao.cessarFogoAtivo;
    }

    public static bool PodeDispararEmGuerra(int teamAtacante, IdentidadeUnidade alvo)
    {
        if (alvo == null) return false;
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        if (governo != null
            && governo.EstaPosicaoDesmilitarizada(alvo.teamID, alvo.transform.position))
        {
            return false;
        }

        MeioPassagemTerritorial meio = InferirMeio(alvo);
        ContextoPresencaTerritorial contexto = AvaliarPresenca(
            alvo,
            meio,
            ModoOperacionalTerritorial.Ativo,
            true,
            false);
        // A entrada sem passagem gera contexto/alerta, mas só a guerra já
        // autorizada pode permitir o disparo automático por este gate legado.
        return contexto.paisDaUnidade == alvo.teamID && PodeDispararEmGuerra(teamAtacante, alvo.teamID);
    }

    public static bool DestinoProibidoPorDesmilitarizacao(int teamId, Vector3 destino)
    {
        SistemaGovernoMundial governo = SistemaGovernoMundial.Instancia;
        return governo != null && governo.EstaPosicaoDesmilitarizada(teamId, destino);
    }

    private static MeioPassagemTerritorial InferirMeio(IdentidadeUnidade unidade)
    {
        if (unidade.GetComponentInParent<IdentidadeNaval>() != null
            || unidade.GetComponentInParent<ControleNavioRealista>() != null
            || unidade.GetComponentInParent<ControleSubmarino>() != null)
            return MeioPassagemTerritorial.Naval;
        if (unidade.tipoUnidade == TipoUnidade.Aereo
            || unidade.GetComponentInParent<ControleAviao>() != null
            || unidade.GetComponentInParent<ControleAviaoCaca>() != null
            || unidade.GetComponentInParent<Helicoptero>() != null)
            return MeioPassagemTerritorial.Aereo;
        return MeioPassagemTerritorial.Terrestre;
    }

    private static void RegistrarNovaViolacao(IdentidadeUnidade unidade, ContextoPresencaTerritorial contexto)
    {
        int id = unidade.GetInstanceID();
        string chave = contexto.paisDoTerritorio + ":" + contexto.territorioId;
        if (violacaoAtualPorUnidade.TryGetValue(id, out string anterior) && anterior == chave) return;
        violacaoAtualPorUnidade[id] = chave;
        OnViolacaoTerritorial?.Invoke(unidade, contexto);
    }

    private static void RemoverViolacaoAtual(IdentidadeUnidade unidade)
    {
        if (unidade != null) violacaoAtualPorUnidade.Remove(unidade.GetInstanceID());
    }

    private static void GarantirAssinaturaTerritorio()
    {
        GerenteDeTerritorio atual = GerenteDeTerritorio.Instancia;
        if (gerenteObservado == atual) return;
        if (gerenteObservado != null) gerenteObservado.OnTerritoryOwnerChanged -= AoMudarDonoTerritorial;
        territorioConhecidoPorUnidade.Clear();
        violacaoAtualPorUnidade.Clear();
        gerenteObservado = atual;
        if (gerenteObservado != null) gerenteObservado.OnTerritoryOwnerChanged += AoMudarDonoTerritorial;
    }

    private static void AoMudarDonoTerritorial(string territorioId, int donoAnterior, int novoDono)
    {
        List<int> remover = null;
        foreach (KeyValuePair<int, UnidadeTerritorioConhecido> par in territorioConhecidoPorUnidade)
        {
            UnidadeTerritorioConhecido conhecido = par.Value;
            if (conhecido == null || conhecido.unidade == null)
            {
                if (remover == null) remover = new List<int>();
                remover.Add(par.Key);
                continue;
            }
            if (conhecido.territorio.territorioId != territorioId) continue;
            ResultadoConsultaTerritorio atualizado = conhecido.territorio;
            atualizado.ownerCountryTeamId = novoDono;
            atualizado.neutral = false;
            conhecido.territorio = atualizado;
            OnTerritoryChanged?.Invoke(conhecido.unidade, atualizado);
        }
        if (remover != null) for (int i = 0; i < remover.Count; i++) territorioConhecidoPorUnidade.Remove(remover[i]);
    }
}
