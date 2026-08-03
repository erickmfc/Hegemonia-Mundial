using System;
using UnityEngine;

/// <summary>
/// Representacao sem GameObject de uma unidade guardada em transporte.
/// Mantem o estado estrategico e materializa a unidade somente ao desembarcar.
/// </summary>
[Serializable]
public sealed class ManifestoCargaOperacional
{
    public SaveEntityData entidade;

    public string NomeExibicao => entidade != null ? entidade.nomeCena : "Carga";
    public TipoUnidade Tipo => entidade != null ? entidade.tipoUnidade : TipoUnidade.Infantaria;
    public int TeamId => entidade != null ? entidade.teamID : 0;

    public static ManifestoCargaOperacional Capturar(GameObject unidade)
    {
        if (unidade == null) return null;

        SaveableEntity saveable = SaveableEntity.Garantir(unidade);
        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>();
        SistemaDeDanos danos = unidade.GetComponent<SistemaDeDanos>();
        CombustivelUnidade combustivel = unidade.GetComponent<CombustivelUnidade>();
        ControleUnidade controle = unidade.GetComponent<ControleUnidade>();

        SaveEntityData data = new SaveEntityData
        {
            uniqueId = saveable.UniqueId,
            prefabKey = saveable.PrefabKey,
            nomeCena = unidade.name,
            ativo = true,
            posicao = new SaveVector3(unidade.transform.position),
            rotacao = new SaveQuaternion(unidade.transform.rotation),
            escala = new SaveVector3(unidade.transform.localScale),
            teamID = identidade != null ? identidade.teamID : 0,
            nomeDoPais = identidade != null ? identidade.nomeDoPais : string.Empty,
            tipoUnidade = identidade != null ? identidade.tipoUnidade : TipoUnidade.Infantaria,
            possuiVida = danos != null,
            vidaAtual = danos != null ? danos.vidaAtual : 0f,
            vidaMaxima = danos != null ? danos.vidaMaxima : 0f,
            possuiCombustivel = combustivel != null && combustivel.usaCombustivel,
            combustivelAtual = combustivel != null ? combustivel.combustivelAtual : 0f,
            capacidadeCombustivel = combustivel != null ? combustivel.capacidade : 0f,
            ordemAtual = controle != null ? controle.OrdemAtual : OrdemControleUnidade.Ociosa,
            modoCombateAtivo = controle == null || controle.ModoCombateAtivo
        };

        if (controle != null)
        {
            EstadoControleUnidadeSnapshot estado = controle.ObterEstadoControle();
            data.ordemAtual = estado.ordemAtual;
            data.modoCombateAtivo = estado.modoCombateAtivo;
            data.possuiDestino = estado.possuiDestinoOrdenado;
            data.ultimoDestino = new SaveVector3(estado.ultimoDestino);
        }

        return new ManifestoCargaOperacional { entidade = data };
    }

    public GameObject Materializar(Vector3 posicao, Quaternion rotacao)
    {
        if (entidade == null || SistemaSaveGame.Instancia == null)
        {
            return null;
        }

        return SistemaSaveGame.Instancia.MaterializarEntidadeOperacional(entidade, posicao, rotacao);
    }
}
