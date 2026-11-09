using System;
using System.Collections.Generic;
using UnityEngine;

public static class SistemaMilitar
{
    public const int MetaEfetivoPadrao = 501;
    public const int RecrutamentoMilitarPadraoPorDia = 40;
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

        ProcessarEfetivoNacional(pais);

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

    public static void GarantirCadastroMilitar(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        if (pais.efetivoMilitar == null) pais.efetivoMilitar = new List<RegistroMilitarIndividual>();
        if (pais.registrosMilitaresSincronizados)
        {
            GarantirRegistrosAtivos(pais);
            return;
        }

        pais.registrosMilitaresAtivos = 0;
        pais.registrosMilitaresEmFormacao = 0;
        for (int i = 0; i < pais.efetivoMilitar.Count; i++)
        {
            RegistroMilitarIndividual registro = pais.efetivoMilitar[i];
            if (registro == null) continue;
            ContabilizarEstado(pais, registro.estado, 1);
            GarantirDadosRegistro(pais, registro);
        }

        pais.registrosMilitaresSincronizados = true;
        GarantirRegistrosAtivos(pais);
        VincularUnidadesExistentes(pais);
    }

    public static int ObterRegistrosEmFormacao(DadosPaisGoverno pais)
    {
        GarantirCadastroMilitar(pais);
        return pais != null ? Mathf.Max(0, pais.registrosMilitaresEmFormacao) : 0;
    }

    public static RegistroMilitarIndividual RegistrarRecrutaQuartel(
        DadosPaisGoverno pais,
        string id,
        string nomeCompleto,
        int teamId,
        string nacionalidade,
        int idade,
        int forca,
        string patente,
        string especialidade,
        float experiencia,
        int diaRecrutamento,
        int diaInicioServico,
        long salarioDiario,
        string postoAtual,
        string instalacaoId,
        bool emFormacao)
    {
        if (pais == null) return null;
        GarantirCadastroMilitar(pais);
        RegistroMilitarIndividual existente = EncontrarRegistro(pais, id);
        if (existente != null) return existente;

        {
            for (int i = 0; i < pais.efetivoMilitar.Count; i++)
            {
                RegistroMilitarIndividual legado = pais.efetivoMilitar[i];
                bool eReservaGenerica = legado != null
                    && legado.estado == EstadoRegistroMilitarIndividual.EmFormacao
                    && legado.instalacaoId != null
                    && legado.instalacaoId.StartsWith("reserva-nacional-", StringComparison.Ordinal);
                bool eAtivoGenerico = legado != null
                    && legado.estado == EstadoRegistroMilitarIndividual.Ativo
                    && string.IsNullOrEmpty(legado.unidadeId);
                if (legado == null || !string.IsNullOrEmpty(legado.unidadeId)
                    || (emFormacao ? !eReservaGenerica : !eAtivoGenerico)) continue;
                legado.id = id;
                legado.nomeCompleto = nomeCompleto;
                legado.nacionalidade = nacionalidade;
                legado.teamId = teamId > 0 ? teamId : pais.teamId;
                legado.idade = idade;
                legado.forca = forca;
                legado.patente = patente;
                legado.especialidade = especialidade;
                legado.experiencia = Mathf.Max(0f, experiencia);
                legado.diaRecrutamento = Mathf.Max(1, diaRecrutamento);
                legado.diaInicioServico = Mathf.Max(1, diaInicioServico > 0 ? diaInicioServico : diaRecrutamento);
                legado.salarioDiario = Math.Max(0L, salarioDiario);
                legado.postoAtual = postoAtual;
                legado.instalacaoId = instalacaoId;
                return legado;
            }
        }

        var registro = new RegistroMilitarIndividual
        {
            id = id,
            teamId = teamId > 0 ? teamId : pais.teamId,
            nomeCompleto = nomeCompleto,
            nacionalidade = nacionalidade,
            idade = idade,
            forca = forca,
            patente = patente,
            especialidade = especialidade,
            experiencia = Mathf.Max(0f, experiencia),
            diaRecrutamento = Mathf.Max(1, diaRecrutamento),
            diaInicioServico = Mathf.Max(0, diaInicioServico),
            salarioDiario = Math.Max(0L, salarioDiario),
            estado = emFormacao ? EstadoRegistroMilitarIndividual.EmFormacao : EstadoRegistroMilitarIndividual.Ativo,
            postoAtual = postoAtual,
            instalacaoId = instalacaoId
        };
        GarantirDadosRegistro(pais, registro);
        pais.efetivoMilitar.Add(registro);
        ContabilizarEstado(pais, registro.estado, 1);
        return registro;
    }

    public static void ConcluirFormacaoDeRecruta(DadosPaisGoverno pais, string id, int dia)
    {
        if (pais == null) return;
        GarantirCadastroMilitar(pais);
        RegistroMilitarIndividual registro = EncontrarRegistro(pais, id);
        if (registro == null || registro.estado != EstadoRegistroMilitarIndividual.EmFormacao) return;

        ContabilizarEstado(pais, registro.estado, -1);
        registro.estado = EstadoRegistroMilitarIndividual.Ativo;
        registro.patente = "Soldado";
        registro.diaInicioServico = Mathf.Max(1, dia);
        registro.postoAtual = "Reserva nacional";
        registro.experiencia = Mathf.Max(0.05f, registro.experiencia);
        ContabilizarEstado(pais, registro.estado, 1);
    }

    public static void VincularEfetivoAUnidade(DadosPaisGoverno pais, IdentidadeUnidade identidade)
    {
        if (pais == null || identidade == null || identidade.militaresConsumidos <= 0) return;
        GarantirCadastroMilitar(pais);
        GarantirRegistrosAtivos(pais);
        if (identidade.militaresVinculadosIds == null)
            identidade.militaresVinculadosIds = new List<string>(identidade.militaresConsumidos);
        else
            identidade.militaresVinculadosIds.Clear();

        SaveableEntity saveable = SaveableEntity.Garantir(identidade.gameObject);
        string unidadeId = saveable != null ? saveable.UniqueId : "runtime-" + identidade.GetInstanceID();
        for (int i = 0; i < pais.efetivoMilitar.Count && identidade.militaresVinculadosIds.Count < identidade.militaresConsumidos; i++)
        {
            RegistroMilitarIndividual registro = pais.efetivoMilitar[i];
            if (registro == null || registro.estado != EstadoRegistroMilitarIndividual.Ativo
                || !string.IsNullOrEmpty(registro.unidadeId)) continue;

            registro.estado = EstadoRegistroMilitarIndividual.Mobilizado;
            registro.unidadeId = unidadeId;
            registro.postoAtual = identidade.gameObject.name;
            identidade.militaresVinculadosIds.Add(registro.id);
        }
        for (int i = 0; i < identidade.militaresVinculadosIds.Count; i++)
        {
            RegistroMilitarIndividual registro = EncontrarRegistro(pais, identidade.militaresVinculadosIds[i]);
            if (registro != null) registro.estado = EstadoRegistroMilitarIndividual.Mobilizado;
        }
    }

    public static void RegistrarBaixas(DadosPaisGoverno pais, IdentidadeUnidade identidade, int quantidade)
    {
        if (pais == null || quantidade <= 0) return;
        GarantirCadastroMilitar(pais);
        int registradas = 0;
        List<string> ids = identidade != null ? identidade.militaresVinculadosIds : null;
        if (ids != null)
        {
            for (int i = 0; i < ids.Count && registradas < quantidade; i++)
            {
                RegistroMilitarIndividual registro = EncontrarRegistro(pais, ids[i]);
                if (registro == null || registro.estado == EstadoRegistroMilitarIndividual.Baixado) continue;
                ContabilizarEstado(pais, registro.estado, -1);
                registro.estado = EstadoRegistroMilitarIndividual.Baixado;
                registro.postoAtual = "Baixado em combate";
                registro.unidadeId = string.Empty;
                registradas++;
            }
        }

        for (int i = 0; i < pais.efetivoMilitar.Count && registradas < quantidade; i++)
        {
            RegistroMilitarIndividual registro = pais.efetivoMilitar[i];
            if (registro == null || (registro.estado != EstadoRegistroMilitarIndividual.Ativo
                                     && registro.estado != EstadoRegistroMilitarIndividual.Mobilizado)) continue;
            ContabilizarEstado(pais, registro.estado, -1);
            registro.estado = EstadoRegistroMilitarIndividual.Baixado;
            registro.postoAtual = "Baixado em combate";
            registro.unidadeId = string.Empty;
            registradas++;
        }

        if (identidade != null) identidade.militaresVinculadosIds?.Clear();
    }

    public static void LiberarVinculos(DadosPaisGoverno pais, IdentidadeUnidade identidade)
    {
        if (pais == null || identidade == null || identidade.militaresVinculadosIds == null) return;
        GarantirCadastroMilitar(pais);
        for (int i = 0; i < identidade.militaresVinculadosIds.Count; i++)
        {
            RegistroMilitarIndividual registro = EncontrarRegistro(pais, identidade.militaresVinculadosIds[i]);
            if (registro == null || registro.estado != EstadoRegistroMilitarIndividual.Mobilizado) continue;
            ContabilizarEstado(pais, registro.estado, -1);
            registro.estado = EstadoRegistroMilitarIndividual.Alistavel;
            registro.unidadeId = string.Empty;
            registro.postoAtual = "Reserva de mobilizacao";
            ContabilizarEstado(pais, registro.estado, 1);
        }
        identidade.militaresVinculadosIds.Clear();
    }

    private static void ProcessarEfetivoNacional(DadosPaisGoverno pais)
    {
        GarantirCadastroMilitar(pais);
        int dia = GerenciadorTempo.Instancia != null ? Mathf.Max(1, GerenciadorTempo.Instancia.totalDias) : 1;
        if (pais.ultimoDiaRecrutamentoMilitar == dia) return;

        GarantirRegistrosAtivos(pais);
        GarantirRegistrosEmFormacaoLegados(pais, dia);
        for (int i = 0; i < pais.efetivoMilitar.Count; i++)
        {
            RegistroMilitarIndividual registro = pais.efetivoMilitar[i];
            if (registro == null) continue;
            if (registro.estado == EstadoRegistroMilitarIndividual.EmFormacao
                && (string.IsNullOrWhiteSpace(registro.instalacaoId)
                    || registro.instalacaoId.StartsWith("reserva-nacional-", StringComparison.Ordinal))
                && dia > registro.diaRecrutamento)
            {
                ContabilizarEstado(pais, registro.estado, -1);
                registro.estado = EstadoRegistroMilitarIndividual.Ativo;
                registro.patente = "Soldado";
                registro.diaInicioServico = dia;
                registro.experiencia = Mathf.Max(0.05f, registro.experiencia);
                registro.postoAtual = "Reserva nacional";
                pais.reservistas = Mathf.Max(0, pais.reservistas - 1);
                pais.populacaoMilitarAtiva++;
                ContabilizarEstado(pais, registro.estado, 1);
            }
            if (registro.estado == EstadoRegistroMilitarIndividual.Ativo
                || registro.estado == EstadoRegistroMilitarIndividual.Mobilizado)
            {
                registro.diasServico = Mathf.Max(0, dia - registro.diaInicioServico);
            }
        }

        if (QuartelAdministracaoRuntime.ExisteQuartelAtivoParaEquipe(pais.teamId))
        {
            pais.ultimoDiaRecrutamentoMilitar = dia;
            return;
        }

        if (pais.metaEfetivoMilitar <= 0) pais.metaEfetivoMilitar = MetaEfetivoPadrao;
        if (pais.recrutamentoMilitarPorDia <= 0) pais.recrutamentoMilitarPorDia = RecrutamentoMilitarPadraoPorDia;
        int meta = Mathf.Max(1, pais.metaEfetivoMilitar);
        int emFormacao = Mathf.Max(pais.registrosMilitaresEmFormacao, Mathf.Max(0, pais.reservistas));
        int falta = Mathf.Max(0, meta - pais.populacaoMilitarAtiva - emFormacao);
        int tetoDiario = Mathf.Max(0, pais.recrutamentoMilitarPorDia);
        if (pais.orcamentoInvestimentoAtivo)
        {
            OrcamentoInvestimentoNacional.ProcessarDia(SistemaGovernoMundial.Instancia, pais, dia);
            tetoDiario = Mathf.Min(tetoDiario, Mathf.Max(0, OrcamentoInvestimentoNacional.ObterLimiteRecrutamentoForca(pais, QuartelForcaV2.Infantaria)));
        }

        int quantidade = Mathf.Min(tetoDiario, Mathf.Min(falta, Mathf.Max(0, pais.alistaveis)));
        for (int i = 0; i < quantidade; i++)
        {
            if (!OrcamentoInvestimentoNacional.ConsumirVerbaDeRecrutamento(
                pais, QuartelForcaV2.Infantaria, OrcamentoInvestimentoNacional.CustoRecrutamentoPorMilitar)) break;
            pais.alistaveis = Mathf.Max(0, pais.alistaveis - 1);
            pais.reservistas++;
            CriarRegistroAutomatico(pais, dia);
        }

        pais.ultimoDiaRecrutamentoMilitar = dia;
        pais.populacao = Mathf.Clamp(
            pais.populacaoCivil + pais.populacaoMilitarAtiva + pais.reservistas + pais.alistaveis,
            0,
            Mathf.Max(1, pais.populacaoMaxima));
    }

    private static void GarantirRegistrosAtivos(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        int vivos = pais.registrosMilitaresAtivos;
        int meta = Mathf.Max(0, pais.populacaoMilitarAtiva);
        while (vivos < meta)
        {
            RegistroMilitarIndividual registro = CriarRegistro(pais, EstadoRegistroMilitarIndividual.Ativo, QuartelForcaV2.Infantaria, "Reserva nacional", string.Empty, 1);
            registro.diaInicioServico = Mathf.Max(1, GerenciadorTempo.Instancia != null ? GerenciadorTempo.Instancia.totalDias : 1);
            vivos++;
        }
    }

    private static void VincularUnidadesExistentes(DadosPaisGoverno pais)
    {
        if (pais == null) return;
        RegistroEntidadesJogo.FillUnidades(unidadesBuffer);
        for (int i = 0; i < unidadesBuffer.Count; i++)
        {
            IdentidadeUnidade identidade = unidadesBuffer[i];
            if (identidade == null || identidade.teamID != pais.teamId || identidade.militaresConsumidos <= 0
                || (identidade.militaresVinculadosIds != null && identidade.militaresVinculadosIds.Count > 0)) continue;
            VincularEfetivoAUnidade(pais, identidade);
        }
    }

    private static void GarantirRegistrosEmFormacaoLegados(DadosPaisGoverno pais, int dia)
    {
        if (pais == null) return;
        int faltam = Mathf.Max(0, pais.reservistas - pais.registrosMilitaresEmFormacao);
        for (int i = 0; i < faltam; i++)
        {
            RegistroMilitarIndividual registro = CriarRegistro(
                pais,
                EstadoRegistroMilitarIndividual.EmFormacao,
                QuartelForcaV2.Infantaria,
                "Reserva nacional",
                "reserva-nacional-" + pais.teamId,
                dia);
            registro.patente = "Recruta";
        }
    }

    private static void CriarRegistroAutomatico(DadosPaisGoverno pais, int dia)
    {
        RegistroMilitarIndividual registro = CriarRegistro(
            pais,
            EstadoRegistroMilitarIndividual.EmFormacao,
            QuartelForcaV2.Infantaria,
            "Reserva nacional",
            "reserva-nacional-" + pais.teamId,
            dia);
        registro.patente = "Recruta";
        registro.diaInicioServico = 0;
    }

    private static RegistroMilitarIndividual CriarRegistro(
        DadosPaisGoverno pais,
        EstadoRegistroMilitarIndividual estado,
        QuartelForcaV2 forca,
        string posto,
        string instalacaoId,
        int dia)
    {
        var registro = new RegistroMilitarIndividual
        {
            id = "militar-" + pais.teamId + "-" + (++pais.sequenciaRegistroMilitar),
            teamId = pais.teamId,
            nomeCompleto = GeradorNomesBatismo.GerarNome(),
            nacionalidade = pais.nomePais,
            idade = UnityEngine.Random.Range(18, 36),
            forca = (int)forca,
            patente = estado == EstadoRegistroMilitarIndividual.EmFormacao ? "Recruta" : "Soldado",
            especialidade = forca == QuartelForcaV2.Veiculos ? "Tripulante de tanque" : "Infantaria",
            experiencia = 0f,
            diaRecrutamento = Mathf.Max(1, dia),
            diaInicioServico = estado == EstadoRegistroMilitarIndividual.Ativo ? Mathf.Max(1, dia) : 0,
            salarioDiario = 1L,
            estado = estado,
            postoAtual = posto,
            instalacaoId = instalacaoId
        };
        pais.efetivoMilitar.Add(registro);
        ContabilizarEstado(pais, estado, 1);
        return registro;
    }

    private static void GarantirDadosRegistro(DadosPaisGoverno pais, RegistroMilitarIndividual registro)
    {
        if (string.IsNullOrWhiteSpace(registro.id))
            registro.id = "militar-" + pais.teamId + "-" + (++pais.sequenciaRegistroMilitar);
        else
        {
            int separador = registro.id.LastIndexOf('-');
            if (separador >= 0 && int.TryParse(registro.id.Substring(separador + 1), out int sequencia))
                pais.sequenciaRegistroMilitar = Mathf.Max(pais.sequenciaRegistroMilitar, sequencia);
        }
        if (registro.teamId <= 0) registro.teamId = pais.teamId;
        if (string.IsNullOrWhiteSpace(registro.nomeCompleto)) registro.nomeCompleto = GeradorNomesBatismo.GerarNome();
        if (string.IsNullOrWhiteSpace(registro.nacionalidade)) registro.nacionalidade = pais.nomePais;
        if (registro.idade < 18) registro.idade = UnityEngine.Random.Range(18, 36);
        if (string.IsNullOrWhiteSpace(registro.patente))
            registro.patente = registro.estado == EstadoRegistroMilitarIndividual.EmFormacao ? "Recruta" : "Soldado";
        if (string.IsNullOrWhiteSpace(registro.especialidade)) registro.especialidade = "Infantaria";
        if (string.IsNullOrWhiteSpace(registro.postoAtual)) registro.postoAtual = "Reserva nacional";
    }

    private static RegistroMilitarIndividual EncontrarRegistro(DadosPaisGoverno pais, string id)
    {
        if (pais == null || pais.efetivoMilitar == null || string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < pais.efetivoMilitar.Count; i++)
        {
            RegistroMilitarIndividual registro = pais.efetivoMilitar[i];
            if (registro != null && registro.id == id) return registro;
        }
        return null;
    }

    private static void ContabilizarEstado(DadosPaisGoverno pais, EstadoRegistroMilitarIndividual estado, int delta)
    {
        if (pais == null) return;
        if (estado == EstadoRegistroMilitarIndividual.Ativo || estado == EstadoRegistroMilitarIndividual.Mobilizado)
            pais.registrosMilitaresAtivos = Mathf.Max(0, pais.registrosMilitaresAtivos + delta);
        else if (estado == EstadoRegistroMilitarIndividual.EmFormacao)
            pais.registrosMilitaresEmFormacao = Mathf.Max(0, pais.registrosMilitaresEmFormacao + delta);
    }
}
