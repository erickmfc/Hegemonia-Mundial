using UnityEngine;

public sealed class AdaptadorMenuPortaAvioesV2 : MonoBehaviour
{
    public GerenciadorOperacoesPortaAvioesV2 gerenciador;
    public ControleAviao aeronaveSelecionada;
    public Vector3 destinoMissao;
    public string Status => aeronaveSelecionada == null || gerenciador == null ? "Nenhuma aeronave" : CriarStatus(gerenciador.Registrar(aeronaveSelecionada));
    public bool TrySolicitarPouso() => gerenciador != null && gerenciador.TrySolicitarPouso(aeronaveSelecionada);
    public bool TrySolicitarReabastecimento() => gerenciador != null && gerenciador.TrySolicitarReabastecimento(aeronaveSelecionada);
    public bool TryEnviarParaHangarInterno() => gerenciador != null && gerenciador.TryEnviarParaHangarInterno(aeronaveSelecionada);
    public bool TryTrazerParaConves() => gerenciador != null && gerenciador.TryTrazerParaConves(aeronaveSelecionada);
    public bool TrySolicitarDecolagem() => gerenciador != null && gerenciador.TrySolicitarDecolagem(aeronaveSelecionada, destinoMissao);
    public bool TrySolicitarPatrulha() => gerenciador != null && gerenciador.TrySolicitarPatrulha(aeronaveSelecionada, destinoMissao);
    public bool TryCancelarOperacao() => gerenciador != null && gerenciador.TryCancelarOperacao(aeronaveSelecionada);
    private string CriarStatus(AeronaveEmbarcadaV2 a) { if (a == null) return "Aeronave ausente"; var r = a.Registro; return $"{r.id} | {r.estado} | Vaga {r.vagaOcupada} | Combustível {r.combustivel:0.0} | {r.operacaoAtual} | {r.motivoFalha}"; }
}
