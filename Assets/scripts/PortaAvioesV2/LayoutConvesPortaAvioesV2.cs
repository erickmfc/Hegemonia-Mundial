using System.Collections.Generic;
using UnityEngine;

public sealed class LayoutConvesPortaAvioesV2 : MonoBehaviour
{
    public Transform referenciaConves, pouso, taxi, vagasExternas, catapultas, elevadores, vagasInternas, pontosServico, voo, decolagem;
    public bool interiorHangarModelado;
    // No Enterprise o comprimento pode estar no X ou no Z dependendo da
    // orientação do modelo importado. O calibrador grava isso no prefab para
    // que o manager escolha a fila lateral correta sem depender de valores
    // absolutos.
    public bool eixoComprimentoEhX = true;
    // Quando ativo, as posições gravadas pelo calibrador do prefab são
    // preservadas durante AtualizarListas/Awake. Vagas novas ainda recebem a
    // configuração padrão para não quebrar porta-aviões criados em runtime.
    public bool layoutCalibradoManualmente;
    public List<Transform> pontosPouso = new List<Transform>();
    public List<Transform> pontosTaxi = new List<Transform>();
    public List<Transform> pontosVoo = new List<Transform>();
    public List<Transform> pontosDecolagem = new List<Transform>();
    public List<VagaPortaAvioesV2> vagasConves = new List<VagaPortaAvioesV2>();
    public List<VagaPortaAvioesV2> vagasHangar = new List<VagaPortaAvioesV2>();
    public List<Transform> elevadoresLista = new List<Transform>();
    public List<Transform> catapultasLista = new List<Transform>();
    public string[] UltimosErros { get; private set; } = new string[0];

    public bool VagaEstaNoLadoEsquerdo(VagaPortaAvioesV2 vaga)
    {
        if (vaga == null) return true;
        Vector3 posicao = vaga.transform.localPosition;
        return eixoComprimentoEhX ? posicao.z >= 0f : posicao.x < 0f;
    }

    [ContextMenu("Criar estrutura padrão")]
    public void CriarEstruturaPadrao()
    {
        pouso = CriarGrupo("Pouso"); taxi = CriarGrupo("Taxi"); vagasExternas = CriarGrupo("VagasExternas"); catapultas = CriarGrupo("Catapultas"); elevadores = CriarGrupo("Elevadores"); vagasInternas = CriarGrupo("VagasInternas"); pontosServico = CriarGrupo("PontosServico"); voo = CriarGrupo("Voo"); decolagem = CriarGrupo("Decolagem");
        if (referenciaConves == null) referenciaConves = CriarGrupo("ReferenciaConves");
        CriarPontos(pouso, new[] { "Espera_01", "Aproximacao_Longa", "Aproximacao_Media", "Aproximacao_Final", "Toque", "Fim_Frenagem", "Saida_Pista" });
        ConfigurarRotaPousoPadrao();
        CriarPontos(taxi, new[] { "Taxi_01", "Taxi_02", "Cruzamento_01", "Acesso_Vagas", "Acesso_Vagas_Esquerda", "Acesso_Vagas_Direita", "Cruzamento_Esquerda", "Cruzamento_Direita" });
        CriarVagasPadrao();
        CriarPontos(catapultas, new[] { "Catapulta_01/Fila", "Catapulta_01/Inicio", "Catapulta_01/Liberacao", "Catapulta_01/Subida" });
        CriarPontos(elevadores, new[] { "Elevador_01/Fila", "Elevador_01/Posicao_Conves", "Elevador_01/Posicao_Baixa", "Elevador_01/Saida_Hangar" });
        CriarPontos(vagasInternas, new[] { "Vaga_Hangar_01", "Vaga_Hangar_02" });
        CriarPontos(pontosServico, new[] { "Combustivel_01", "Combustivel_02", "Rearmamento_01" });
        CriarPontos(voo, new[] { "Circuito_01", "Afastamento_01", "Subida_Inicial", "Ponto_Missao" });
        CriarPontos(decolagem, new[] { "Fila", "Alinhamento", "Liberacao", "Subida_Inicial", "Saida_Voo" });
        AtualizarListas();
    }

    /// <summary>
    /// Cria o estacionamento persistente do porta-aviões: doze vagas de caça
    /// fora da faixa de taxi/pouso e três vagas de maior porte. As vagas do
    /// hangar possuem uma grade persistente de 60 vagas, sem reutilizar a
    /// posição da vaga externa durante o retorno do elevador.
    /// </summary>
    [ContextMenu("Criar 15 vagas de convés e 60 de hangar")]
    public void CriarVagasPadrao()
    {
        if (vagasExternas == null) vagasExternas = CriarGrupo("VagasExternas");
        if (vagasInternas == null) vagasInternas = CriarGrupo("VagasInternas");
        if (taxi == null) taxi = CriarGrupo("Taxi");

        CriarPontos(taxi, new[] { "Acesso_Vagas_Esquerda", "Acesso_Vagas_Direita", "Cruzamento_Esquerda", "Cruzamento_Direita" });
        ConfigurarPonto(taxi.Find("Acesso_Vagas_Esquerda"), new Vector3(-25f, .45f, 22f), 0f);
        ConfigurarPonto(taxi.Find("Acesso_Vagas_Direita"), new Vector3(-25f, .45f, -22f), 180f);
        ConfigurarPonto(taxi.Find("Cruzamento_Esquerda"), new Vector3(-80f, .45f, 22f), 0f);
        ConfigurarPonto(taxi.Find("Cruzamento_Direita"), new Vector3(-80f, .45f, -22f), 180f);

        VagaPadrao[] vagas =
        {
            new VagaPadrao("Vaga_Conves_01", new Vector3(-150f, .45f, 22f), 0f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_02", new Vector3(-110f, .45f, 22f), 0f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_03", new Vector3(-70f, .45f, 22f), 0f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_04", new Vector3(-30f, .45f, 22f), 0f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_05", new Vector3(10f, .45f, 22f), 0f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_06", new Vector3(50f, .45f, 22f), 0f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_07", new Vector3(-150f, .45f, -22f), 180f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_08", new Vector3(-110f, .45f, -22f), 180f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_09", new Vector3(-70f, .45f, -22f), 180f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_10", new Vector3(-30f, .45f, -22f), 180f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_11", new Vector3(10f, .45f, -22f), 180f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_12", new Vector3(50f, .45f, -22f), 180f, TipoAeronavePortaAvioesV2.Caca, 12f),
            new VagaPadrao("Vaga_Conves_13_Grande", new Vector3(120f, .45f, 20f), 0f, TipoAeronavePortaAvioesV2.Qualquer, 24f),
            new VagaPadrao("Vaga_Conves_14_Grande", new Vector3(155f, .45f, -20f), 180f, TipoAeronavePortaAvioesV2.Qualquer, 24f),
            new VagaPadrao("Vaga_Conves_15_Grande", new Vector3(180f, .45f, 0f), 90f, TipoAeronavePortaAvioesV2.Qualquer, 24f)
        };

        for (int i = 0; i < vagas.Length; i++)
        {
            CriarOuConfigurarVaga(vagasExternas, vagas[i], false);
        }
        CriarVagasHangar60(vagasInternas);
        AtualizarListas();
    }

    private void CriarVagasHangar60(Transform grupo)
    {
        if (grupo == null) return;

        const int colunas = 12;
        const int total = 60;
        const float espacamentoX = 28f;
        const float espacamentoZ = 14f;
        const float inicioX = -154f;
        const float inicioZ = -28f;

        for (int i = 0; i < total; i++)
        {
            int indice = i + 1;
            string nome = $"Vaga_Hangar_{indice:00}";
            Transform vaga = grupo.Find(nome);
            if (vaga == null && indice <= 15)
            {
                string nomeGrande = $"Vaga_Hangar_{indice:00}_Grande";
                vaga = grupo.Find(nomeGrande);
                if (vaga != null) vaga.name = nome;
            }
            bool vagaExistia = vaga != null;
            if (vaga == null)
            {
                vaga = new GameObject(nome).transform;
                vaga.SetParent(grupo, false);
            }

            int coluna = i % colunas;
            int linha = i / colunas;
            bool grande = indice >= 13 && indice <= 15;
            float x = inicioX + coluna * espacamentoX;
            float z = inicioZ + linha * espacamentoZ;
            if (grande)
            {
                // As três vagas maiores ficam no corredor lateral mais amplo
                // do hangar, sem coincidir com a grade das vagas de caça.
                x = -154f + (indice - 13) * 154f;
                // Mantém as vagas grandes no corredor lateral do hangar,
                // afastadas da última linha da grade de caças (z=28).
                z = 48f;
            }

            if (!layoutCalibradoManualmente || !vagaExistia)
                ConfigurarPonto(vaga, new Vector3(x, .45f, z), coluna < colunas / 2 ? 90f : -90f);
            VagaPortaAvioesV2 dados = vaga.GetComponent<VagaPortaAvioesV2>() ?? vaga.gameObject.AddComponent<VagaPortaAvioesV2>();
            dados.id = $"Hangar_{indice:00}";
            dados.tipoPermitido = grande ? TipoAeronavePortaAvioesV2.Qualquer : TipoAeronavePortaAvioesV2.Caca;
            dados.tamanhoMaximo = grande ? 24f : 12f;
            GarantirPontoFilho(vaga, "Entrada", new Vector3(0f, 0f, 12f));
            GarantirPontoFilho(vaga, "Parada", Vector3.zero);
        }
    }

    [ContextMenu("Validar layout")]
    public bool ValidarLayout()
    {
        var erros = new List<string>(); AtualizarListas();
        if (pouso == null || pontosPouso.Count < 5) erros.Add("Pouso precisa de pelo menos 5 pontos em sequência.");
        if (referenciaConves == null) erros.Add("ReferenciaConves ausente.");
        if (vagasConves.Count < 15) erros.Add("O convés precisa de pelo menos 15 vagas.");
        if (vagasHangar.Count < 60) erros.Add("O hangar precisa de pelo menos 60 vagas.");
        int vagasGrandes = 0;
        for (int i = 0; i < vagasConves.Count; i++) if (vagasConves[i] != null && vagasConves[i].tamanhoMaximo >= 20f) vagasGrandes++;
        if (vagasGrandes < 3) erros.Add("O convés precisa de pelo menos 3 vagas para aeronaves grandes.");
        if (elevadoresLista.Count > 0) foreach (var e in elevadoresLista) if (e == null || e.Find("Posicao_Conves") == null || e.Find("Posicao_Baixa") == null) erros.Add("Elevador sem posição superior/inferior.");
        var ids = new HashSet<string>(); foreach (var v in vagasConves) if (v != null && !ids.Add(v.id)) erros.Add("ID de vaga duplicado: " + v.id); foreach (var v in vagasHangar) if (v != null && !ids.Add(v.id)) erros.Add("ID de vaga duplicado: " + v.id);
        for (int i = 0; i < vagasConves.Count; i++)
        {
            VagaPortaAvioesV2 vaga = vagasConves[i];
            if (vaga == null) continue;
            // A pista ocupa o corredor central no eixo longitudinal detectado
            // pelo calibrador; não dependa de X/Z fixos do modelo importado.
            float lateral = eixoComprimentoEhX ? vaga.transform.localPosition.z : vaga.transform.localPosition.x;
            float comprimento = eixoComprimentoEhX ? vaga.transform.localPosition.x : vaga.transform.localPosition.z;
            bool foraDaFaixaPelaLateral = Mathf.Abs(lateral) >= 16f;
            bool foraDaFaixaPeloFundo = Mathf.Abs(comprimento) >= 35f;
            if (!foraDaFaixaPelaLateral && !foraDaFaixaPeloFundo) erros.Add("Vaga sobre a faixa de taxi/pouso: " + vaga.name);
            if (vaga.transform.Find("Entrada") == null || vaga.transform.Find("Parada") == null) erros.Add("Vaga sem Entrada/Parada: " + vaga.name);
            for (int j = i + 1; j < vagasConves.Count; j++) if (vagasConves[j] != null && Vector2.Distance(new Vector2(vaga.transform.localPosition.x, vaga.transform.localPosition.z), new Vector2(vagasConves[j].transform.localPosition.x, vagasConves[j].transform.localPosition.z)) < 10f) erros.Add("Vagas externas sobrepostas: " + vaga.name + " / " + vagasConves[j].name);
        }
        int vagasGrandesHangar = 0;
        for (int i = 0; i < vagasHangar.Count; i++)
        {
            VagaPortaAvioesV2 vaga = vagasHangar[i];
            if (vaga == null) continue;
            if (vaga.tamanhoMaximo >= 20f) vagasGrandesHangar++;
            if (vaga.transform.Find("Entrada") == null || vaga.transform.Find("Parada") == null) erros.Add("Vaga interna sem Entrada/Parada: " + vaga.name);
            for (int j = i + 1; j < vagasHangar.Count; j++) if (vagasHangar[j] != null && Vector2.Distance(new Vector2(vaga.transform.localPosition.x, vaga.transform.localPosition.z), new Vector2(vagasHangar[j].transform.localPosition.x, vagasHangar[j].transform.localPosition.z)) < 10f) erros.Add("Vagas internas sobrepostas: " + vaga.name + " / " + vagasHangar[j].name);
        }
        if (vagasGrandesHangar < 3) erros.Add("O hangar precisa de pelo menos 3 vagas para aeronaves grandes.");
        foreach (var t in pontosPouso) if (t != null && !t.IsChildOf(transform)) erros.Add("Ponto fora do porta-aviões: " + t.name);
        UltimosErros = erros.ToArray(); return erros.Count == 0;
    }

    [ContextMenu("Desenhar rotas")]
    public void DesenharRotas() { AtualizarListas(); }
    [ContextMenu("Testar reserva de vagas")]
    public void TestarReservaDeVagas() { AtualizarListas(); }
    [ContextMenu("Verificar sobreposição")]
    public void VerificarSobreposicao() { ValidarLayout(); }
    [ContextMenu("Listar pontos ausentes")]
    public void ListarPontosAusentes() { ValidarLayout(); }

    public void AtualizarListas()
    {
        pontosPouso = Filhos(pouso); pontosTaxi = Filhos(taxi); pontosVoo = Filhos(voo); pontosDecolagem = Filhos(decolagem); elevadoresLista = Filhos(elevadores); catapultasLista = Filhos(catapultas);
        foreach (var e in elevadoresLista)
        {
            if (e == null) continue;
            if (e.Find("Plataforma") == null) GarantirPontoFilho(e, "Plataforma", Vector3.zero);
            ElevadorPortaAvioesV2 elevador = e.GetComponent<ElevadorPortaAvioesV2>();
            if (elevador == null) elevador = e.gameObject.AddComponent<ElevadorPortaAvioesV2>();
            elevador.ConfigurarReferencias();
        }
        vagasConves = GarantirVagas(vagasExternas, false);
        if (vagasInternas != null) CriarVagasHangar60(vagasInternas);
        vagasHangar = GarantirVagas(vagasInternas, true);
    }
    private List<Transform> Filhos(Transform p) { var r = new List<Transform>(); if (p != null) foreach (Transform t in p) r.Add(t); return r; }
    private List<VagaPortaAvioesV2> GarantirVagas(Transform p, bool interna) { var r = new List<VagaPortaAvioesV2>(); if (p == null) return r; foreach (Transform t in p) { var v = t.GetComponent<VagaPortaAvioesV2>() ?? t.gameObject.AddComponent<VagaPortaAvioesV2>(); if (string.IsNullOrEmpty(v.id) || v.id == "Vaga") v.id = t.name; bool grande = t.name.Contains("_Grande"); if (!interna && grande) { v.tipoPermitido = TipoAeronavePortaAvioesV2.Qualquer; v.tamanhoMaximo = 24f; } else if (!grande && v.tamanhoMaximo <= 0f) { v.tipoPermitido = TipoAeronavePortaAvioesV2.Caca; v.tamanhoMaximo = 12f; } GarantirPontoFilho(t, "Entrada", new Vector3(0f, 0f, 12f)); GarantirPontoFilho(t, "Parada", Vector3.zero); r.Add(v); } return r; }
    private Transform CriarGrupo(string nome) { var t = transform.Find(nome); if (t != null) return t; var g = new GameObject(nome).transform; g.SetParent(transform, false); return g; }
    private void CriarPontos(Transform p, IEnumerable<string> nomes) { foreach (string n in nomes) { string[] partes = n.Split('/'); Transform atual = p; foreach (string parte in partes) { Transform f = atual.Find(parte); if (f == null) { f = new GameObject(parte).transform; f.SetParent(atual, false); } atual = f; } } }
    private void ConfigurarPonto(Transform ponto, Vector3 posicao, float yaw) { if (ponto == null) return; ponto.localPosition = posicao; ponto.localRotation = Quaternion.Euler(0f, yaw, 0f); }
    private void ConfigurarRotaPousoPadrao()
    {
        // O eixo X local é o comprimento do modelo do Enterprise. A
        // aproximação fica fora da proa/popa e entra em linha reta pela pista;
        // o eixo Z é reservado para a saída lateral do taxiamento.
        ConfigurarPonto(pouso.Find("Espera_01"), new Vector3(-280f, 35f, -8f), 90f);
        ConfigurarPonto(pouso.Find("Aproximacao_Longa"), new Vector3(-240f, 24f, -8f), 90f);
        ConfigurarPonto(pouso.Find("Aproximacao_Media"), new Vector3(-215f, 13f, -8f), 90f);
        ConfigurarPonto(pouso.Find("Aproximacao_Final"), new Vector3(-202f, 6f, -8f), 90f);
        ConfigurarPonto(pouso.Find("Toque"), new Vector3(-185f, .45f, -8f), 90f);
        ConfigurarPonto(pouso.Find("Fim_Frenagem"), new Vector3(-90f, .45f, -8f), 90f);
        ConfigurarPonto(pouso.Find("Saida_Pista"), new Vector3(-25f, .45f, 22f), 0f);
    }
    private void CriarOuConfigurarVaga(Transform grupo, VagaPadrao especificacao, bool interna)
    {
        Transform vaga = grupo.Find(especificacao.nome); if (vaga == null) { vaga = new GameObject(especificacao.nome).transform; vaga.SetParent(grupo, false); }
        ConfigurarPonto(vaga, especificacao.posicao, especificacao.yaw);
        VagaPortaAvioesV2 dados = vaga.GetComponent<VagaPortaAvioesV2>() ?? vaga.gameObject.AddComponent<VagaPortaAvioesV2>();
        dados.id = interna ? especificacao.id.Replace("Conves", "Hangar") : especificacao.id;
        dados.tipoPermitido = especificacao.tipo; dados.tamanhoMaximo = especificacao.tamanho;
        GarantirPontoFilho(vaga, "Entrada", new Vector3(0f, 0f, 12f)); GarantirPontoFilho(vaga, "Parada", Vector3.zero);
    }
    private void GarantirPontoFilho(Transform vaga, string nome, Vector3 posicao) { Transform ponto = vaga.Find(nome); if (ponto == null) { ponto = new GameObject(nome).transform; ponto.SetParent(vaga, false); } ponto.localPosition = posicao; ponto.localRotation = Quaternion.identity; }
    private sealed class VagaPadrao
    {
        public readonly string nome; public readonly string id; public readonly Vector3 posicao; public readonly float yaw; public readonly TipoAeronavePortaAvioesV2 tipo; public readonly float tamanho;
        public VagaPadrao(string nome, Vector3 posicao, float yaw, TipoAeronavePortaAvioesV2 tipo, float tamanho) { this.nome = nome; this.id = nome.Replace("Vaga_", ""); this.posicao = posicao; this.yaw = yaw; this.tipo = tipo; this.tamanho = tamanho; }
        public VagaPadrao ComoHangar() { return new VagaPadrao(nome.Replace("Conves", "Hangar"), posicao, yaw, tipo, tamanho); }
    }
    private void OnDrawGizmosSelected() { DesenharPouso(); Desenhar(taxi, Color.white); Desenhar(vagasExternas, Color.green); Desenhar(catapultas, Color.red); Desenhar(elevadores, new Color(.6f, 0, 1)); Desenhar(vagasInternas, new Color(1, .45f, 0)); Desenhar(pontosServico, Color.cyan); Desenhar(voo, new Color(.2f, .8f, 1f)); Desenhar(decolagem, Color.red); }
    private void DesenharPouso() { if (pouso == null) return; foreach (Transform t in pouso) { Gizmos.color = t.name == "Toque" || t.name == "Fim_Frenagem" ? Color.yellow : Color.blue; Gizmos.DrawSphere(t.position, .25f); Gizmos.DrawRay(t.position, t.forward * 1.5f); } }
    private void Desenhar(Transform grupo, Color cor) { if (grupo == null) return; Gizmos.color = cor; foreach (Transform t in grupo) { Gizmos.DrawSphere(t.position, .25f); Gizmos.DrawRay(t.position, t.forward * 1.5f); } }
}
