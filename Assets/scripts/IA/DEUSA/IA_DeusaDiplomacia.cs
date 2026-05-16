namespace Hegemonia.AI.DEUSA
{
    public sealed class IA_DeusaDiplomacia
    {
        private float _nextSancaoTime;

        public string UltimoResumo { get; private set; } = "diplomacia aguardando";

        public void Atualizar(IA_DeusaGovernoBridge governoBridge, IA_DeusaConfig config, DadosPaisGoverno pais, DeusaEstagio estagio, float now)
        {
            if (governoBridge == null || config == null || pais == null)
            {
                return;
            }

            if (!config.permitirSancoes || estagio < DeusaEstagio.TensaoGeopolitica)
            {
                UltimoResumo = "sancoes bloqueadas ou estagio insuficiente";
                return;
            }

            if (now < _nextSancaoTime || pais.rivalTeamId <= 0)
            {
                UltimoResumo = "monitorando rival " + pais.rivalTeamId;
                return;
            }

            string mensagem;
            if (governoBridge.TentarAplicarSancaoDireta(pais.teamId, pais.rivalTeamId, out mensagem))
            {
                UltimoResumo = mensagem;
                _nextSancaoTime = now + 45f;
                return;
            }

            UltimoResumo = mensagem;
            _nextSancaoTime = now + 15f;
        }
    }
}
