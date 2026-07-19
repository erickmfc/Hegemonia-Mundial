using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Perfil geológico único e permanente de um país.
/// Gerado uma única vez usando uma semente determinista (teamId).
/// Nunca muda após ser gerado, mesmo ao fechar e abrir o jogo.
/// </summary>
[Serializable]
public class PerfilMineralPais
{
    public int teamId;
    public bool perfilGerado = false;

    // Abundância de cada recurso mineral bruto neste país
    public AbundanciaMineralNivel ferro       = AbundanciaMineralNivel.Inexistente;
    public AbundanciaMineralNivel cobre       = AbundanciaMineralNivel.Inexistente;
    public AbundanciaMineralNivel bauxita     = AbundanciaMineralNivel.Inexistente;
    public AbundanciaMineralNivel titanio     = AbundanciaMineralNivel.Inexistente;
    public AbundanciaMineralNivel uranio      = AbundanciaMineralNivel.Inexistente;
    public float modificadorIndustrial = 1f;

    // Ordens de extração ativas (true = país está extraindo este recurso)
    public bool extraindoFerro    = false;
    public bool extraindoCobre    = false;
    public bool extraindoBauxita  = false;
    public bool extraindoTitanio  = false;
    public bool extraindoUranio   = false;

    // Fila de refino: quais materiais refinados estão sendo produzidos
    public bool refinandoAco                  = false;
    public bool refinandoCobreEletrolitico    = false;
    public bool refinandoDuraluminio          = false;
    public bool refinandoLigaTitanio          = false;
    public bool refinandoComponentes          = false;
    public bool refinandoUranioEnriquecido    = false;

    /// <summary>Obtém o nível de abundância para um recurso específico.</summary>
    public AbundanciaMineralNivel ObterAbundancia(RecursoMineral recurso)
    {
        switch (recurso)
        {
            case RecursoMineral.MinerioFerro:   return ferro;
            case RecursoMineral.MinerioCobre:   return cobre;
            case RecursoMineral.Bauxita:        return bauxita;
            case RecursoMineral.MinerioTitanio: return titanio;
            case RecursoMineral.UranioBruto:    return uranio;
            default: return AbundanciaMineralNivel.Inexistente;
        }
    }

    /// <summary>Verifica se há ordem de extração ativa para este recurso.</summary>
    public bool EstaExtraindo(RecursoMineral recurso)
    {
        switch (recurso)
        {
            case RecursoMineral.MinerioFerro:   return extraindoFerro;
            case RecursoMineral.MinerioCobre:   return extraindoCobre;
            case RecursoMineral.Bauxita:        return extraindoBauxita;
            case RecursoMineral.MinerioTitanio: return extraindoTitanio;
            case RecursoMineral.UranioBruto:    return extraindoUranio;
            default: return false;
        }
    }

    /// <summary>Ativa ou desativa a extração de um recurso.</summary>
    public void SetExtracao(RecursoMineral recurso, bool ativo)
    {
        switch (recurso)
        {
            case RecursoMineral.MinerioFerro:   extraindoFerro   = ativo; break;
            case RecursoMineral.MinerioCobre:   extraindoCobre   = ativo; break;
            case RecursoMineral.Bauxita:        extraindoBauxita = ativo; break;
            case RecursoMineral.MinerioTitanio: extraindoTitanio = ativo; break;
            case RecursoMineral.UranioBruto:    extraindoUranio  = ativo; break;
        }
    }

    /// <summary>Verifica se há ordem de refino ativa para este material.</summary>
    public bool EstaRefinando(MaterialRefinado material)
    {
        switch (material)
        {
            case MaterialRefinado.AcoEstrutural:         return refinandoAco;
            case MaterialRefinado.CobreEletrolitico:     return refinandoCobreEletrolitico;
            case MaterialRefinado.Duraluminio:           return refinandoDuraluminio;
            case MaterialRefinado.LigaTitanio:           return refinandoLigaTitanio;
            case MaterialRefinado.ComponentesEletronicos:return refinandoComponentes;
            case MaterialRefinado.UranioEnriquecido:     return refinandoUranioEnriquecido;
            default: return false;
        }
    }

    /// <summary>Ativa ou desativa o refino de um material.</summary>
    public void SetRefino(MaterialRefinado material, bool ativo)
    {
        switch (material)
        {
            case MaterialRefinado.AcoEstrutural:          refinandoAco                 = ativo; break;
            case MaterialRefinado.CobreEletrolitico:      refinandoCobreEletrolitico   = ativo; break;
            case MaterialRefinado.Duraluminio:            refinandoDuraluminio         = ativo; break;
            case MaterialRefinado.LigaTitanio:            refinandoLigaTitanio         = ativo; break;
            case MaterialRefinado.ComponentesEletronicos: refinandoComponentes         = ativo; break;
            case MaterialRefinado.UranioEnriquecido:      refinandoUranioEnriquecido   = ativo; break;
        }
    }

    /// <summary>
    /// Gera o perfil geológico do país usando uma semente determinista.
    /// Cada país começa com: 2 fortes, 2 médios, 1 baixo/escasso, urânio normalmente raro.
    /// Este método só deve ser chamado se perfilGerado == false.
    /// </summary>
    public void GerarPerfil(int teamId)
    {
        if (perfilGerado) return;

        this.teamId = teamId;
        System.Random rng = new System.Random(teamId * 999 + 17);

        // Pool de recursos não-urânio para distribuição
        RecursoMineral[] recursos = {
            RecursoMineral.MinerioFerro,
            RecursoMineral.MinerioCobre,
            RecursoMineral.Bauxita,
            RecursoMineral.MinerioTitanio
        };

        // Embaralha os recursos
        for (int i = recursos.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            RecursoMineral temp = recursos[i];
            recursos[i] = recursos[j];
            recursos[j] = temp;
        }

        // Distribui: 2 fortes, 2 médios, 1 baixo/escasso (o 5º recurso)
        AbundanciaMineralNivel[] nivelForte  = { AbundanciaMineralNivel.Abundante, AbundanciaMineralNivel.Alto };
        AbundanciaMineralNivel[] nivelMedio  = { AbundanciaMineralNivel.Medio, AbundanciaMineralNivel.Medio };
        AbundanciaMineralNivel[] nivelFraco  = { AbundanciaMineralNivel.Baixo, AbundanciaMineralNivel.Escasso };

        SetAbundancia(recursos[0], nivelForte[rng.Next(nivelForte.Length)]);
        SetAbundancia(recursos[1], nivelForte[rng.Next(nivelForte.Length)]);
        SetAbundancia(recursos[2], nivelMedio[rng.Next(nivelMedio.Length)]);
        SetAbundancia(recursos[3], nivelFraco[rng.Next(nivelFraco.Length)]);

        // Urânio: normalmente raro
        // ~60% Muito Escasso, ~25% Escasso, ~10% Baixo, ~4% Médio, ~1% Alto/Abundante
        int uranioRoll = rng.Next(100);
        if      (uranioRoll < 60) uranio = AbundanciaMineralNivel.MuitoEscasso;
        else if (uranioRoll < 85) uranio = AbundanciaMineralNivel.Escasso;
        else if (uranioRoll < 95) uranio = AbundanciaMineralNivel.Baixo;
        else if (uranioRoll < 99) uranio = AbundanciaMineralNivel.Medio;
        else                      uranio = AbundanciaMineralNivel.Alto;

        float sorteioIndustrial = 0.85f + (float)rng.NextDouble() * 0.25f;
        float bonusAbundancia = PesoAbundancia(ferro) + PesoAbundancia(cobre) + PesoAbundancia(bauxita) + PesoAbundancia(titanio) + (PesoAbundancia(uranio) * 0.5f);
        modificadorIndustrial = Mathf.Clamp(sorteioIndustrial + (bonusAbundancia * 0.015f), 0.75f, 1.25f);

        // Ativa a extração automaticamente para os recursos que existem
        foreach (RecursoMineral r in System.Enum.GetValues(typeof(RecursoMineral)))
        {
            if (ObterAbundancia(r) > AbundanciaMineralNivel.Inexistente)
                SetExtracao(r, true);
        }

        perfilGerado = true;

        Debug.Log($"[SistemaIndustrial] Perfil geológico gerado para team {teamId}: " +
                  $"Ferro={ferro} Cobre={cobre} Bauxita={bauxita} Titânio={titanio} Urânio={uranio} Mod={modificadorIndustrial:0.00}");
    }

    private float PesoAbundancia(AbundanciaMineralNivel nivel)
    {
        switch (nivel)
        {
            case AbundanciaMineralNivel.Abundante: return 6f;
            case AbundanciaMineralNivel.Alto: return 5f;
            case AbundanciaMineralNivel.Medio: return 4f;
            case AbundanciaMineralNivel.Baixo: return 3f;
            case AbundanciaMineralNivel.Escasso: return 2f;
            case AbundanciaMineralNivel.MuitoEscasso: return 1f;
            default: return 0f;
        }
    }

    private void SetAbundancia(RecursoMineral recurso, AbundanciaMineralNivel nivel)
    {
        switch (recurso)
        {
            case RecursoMineral.MinerioFerro:   ferro   = nivel; break;
            case RecursoMineral.MinerioCobre:   cobre   = nivel; break;
            case RecursoMineral.Bauxita:        bauxita = nivel; break;
            case RecursoMineral.MinerioTitanio: titanio = nivel; break;
            case RecursoMineral.UranioBruto:    uranio  = nivel; break;
        }
    }

    /// <summary>Retorna uma string descritiva do perfil para debug e UI.</summary>
    public string DescreverPerfil()
    {
        return $"Ferro: {DescreverNivel(ferro)} | Cobre: {DescreverNivel(cobre)} | " +
               $"Bauxita: {DescreverNivel(bauxita)} | Titânio: {DescreverNivel(titanio)} | " +
               $"Urânio: {DescreverNivel(uranio)}";
    }

    private string DescreverNivel(AbundanciaMineralNivel nivel)
    {
        switch (nivel)
        {
            case AbundanciaMineralNivel.Inexistente:   return "Inexistente";
            case AbundanciaMineralNivel.MuitoEscasso:  return "Muito Escasso";
            case AbundanciaMineralNivel.Escasso:       return "Escasso";
            case AbundanciaMineralNivel.Baixo:         return "Baixo";
            case AbundanciaMineralNivel.Medio:         return "Médio";
            case AbundanciaMineralNivel.Alto:          return "Alto";
            case AbundanciaMineralNivel.Abundante:     return "Abundante";
            default: return "?";
        }
    }
}
