using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Hegemonia.AI.Llama
{
    [Serializable]
    public class LlamaAcaoResposta
    {
        public string acao;
        public string justificativa;
        public int alvo;
        public string recurso;
        public string oferta;
        public string pedido;
    }

    [RequireComponent(typeof(IA_Comandante))]
    [RequireComponent(typeof(IA_Arquiteto_Pro))]
    public class IA_LlamaGovernante : MonoBehaviour
    {
        private IA_Comandante chefe;
        private IA_Arquiteto_Pro arquiteto;
        private LlamaClient llamaClient;

        [Header("Configurações do Cérebro LLM")]
        public bool habilitarLlama = false;
        public float intervaloDeDecisaoSegundos = 60f;
        public int idNacao = 2; // O ID desta nação. O jogador humano é 1.

        private float tempoUltimaDecisao = 0f;
        private bool aguardandoResposta = false;

        private string promptDeSistema = @"Você é o Supremo Comandante da sua nação.
Responda APENAS com um objeto JSON válido. Nenhuma palavra extra, sem saudações.

Ações válidas que você pode escolher:
{
  ""acao"": ""focar_pesquisa"",
  ""alvo"": 0,
  ""recurso"": ""tecnologia_extracao"",
  ""justificativa"": ""Nossos estoques de petroleo estao criticos.""
}
Para diplomacia/ataque direcionado:
{
  ""acao"": ""aplicar_sancao"",
  ""alvo"": 1,
  ""recurso"": ""petroleo"",
  ""justificativa"": ""Eles sao uma ameaca militar.""
}";

        void Start()
        {
            chefe = GetComponent<IA_Comandante>();
            arquiteto = GetComponent<IA_Arquiteto_Pro>();
            
            if (chefe != null)
            {
                chefe.controlePorLLM = false;
            }

            // A integração Llama/Ollama foi desativada: a IA segue 100% local.
            enabled = false;
        }

        void Update()
        {
            if (!habilitarLlama || aguardandoResposta) return;

            if (Time.time - tempoUltimaDecisao >= intervaloDeDecisaoSegundos)
            {
                PedirInstrucaoAoLlama();
            }
        }

        private void PedirInstrucaoAoLlama()
        {
            aguardandoResposta = true;
            tempoUltimaDecisao = Time.time;

            string estadoDaNacao = ColetarEstadoDaNacao();
            
            Debug.Log("[IA Llama/Qwen] Enviando estado da Nação para o Ollama analisar...\n" + estadoDaNacao);

            llamaClient.EnviarPrompt(promptDeSistema, estadoDaNacao, AoReceberInstrucao, AoFalhar);
        }

        private string ColetarEstadoDaNacao()
        {
            // Puxa informações vitais da base militar
            int dinheiroAtual = (int)chefe.dinheiro;
            
            int avioesNoPatio = 0;
            var aeroportos = UnityEngine.Object.FindObjectsByType<GerenciadorAeroporto>(FindObjectsSortMode.None);
            foreach(var aero in aeroportos)
            {
                var id = aero.GetComponent<IdentidadeUnidade>();
                if (id != null && id.teamID == chefe.identidade.teamID)
                {
                    avioesNoPatio += aero.avioesNoPatio.Count;
                }
            }

            // Conta frotas do inimigo para gerar pressão no LLM
            int tropasInimigas = 0;
            int tropasInimigasProximas = 0;
            var todasUnidades = UnityEngine.Object.FindObjectsByType<IdentidadeUnidade>(FindObjectsSortMode.None);
            foreach(var u in todasUnidades)
            {
                if (u.teamID == 1 && (u.tipoUnidade == TipoUnidade.Infantaria || u.tipoUnidade == TipoUnidade.Veiculo)) // O time 1 é o jogador humano
                {
                    tropasInimigas++;
                    
                    // Verifica se está muito próximo das minhas bases (tensão de fronteira)
                    foreach(var baseIA in chefe.minhasBases)
                    {
                        if (baseIA != null && Vector3.Distance(u.transform.position, baseIA.transform.position) < 250f)
                        {
                            tropasInimigasProximas++;
                            break;
                        }
                    }
                }
            }

            // Puxa estoques do governo para a nação inimiga controlada pela IA
            string nomeDaNacao = "Nação Desconhecida";
            string perfil = "Neutro";
            float estabilidade = 50f;
            int rivalId = 1;
            string nomeRival = "Republica Atlas";
            int relacaoComRival = 0;
            
            int dinheiroAtualGov = 0;
            int petroleoAtualGov = 0;
            string statusPetroleo = "NORMAL";
            
            if (SistemaGovernoMundial.Instancia != null)
            {
                var meuPais = SistemaGovernoMundial.Instancia.ObterPais(idNacao);
                if (meuPais != null)
                {
                    nomeDaNacao = meuPais.nomePais;
                    perfil = meuPais.perfilIA.ToString();
                    estabilidade = meuPais.estabilidade;
                    rivalId = meuPais.rivalTeamId > 0 ? meuPais.rivalTeamId : 1;
                    dinheiroAtualGov = meuPais.saldo;
                    petroleoAtualGov = meuPais.petroleo;
                    
                    // Se o petróleo estiver crítico (< 300)
                    if (petroleoAtualGov < 300) statusPetroleo = "CRITICO_BAIXO";
                    
                    var rival = SistemaGovernoMundial.Instancia.ObterPais(rivalId);
                    if (rival != null) nomeRival = rival.nomePais;
                    
                    var rel = SistemaGovernoMundial.Instancia.ObterRelacao(idNacao, rivalId);
                    relacaoComRival = rel != null ? rel.valor : 0;
                }
            }

            string estado = $@"Você é a {nomeDaNacao} (Team {idNacao}), perfil: {perfil}. Sua estabilidade é de {estabilidade:F0}%.
{{
  ""minha_nacao"": {idNacao},
  ""recursos"": {{
    ""dinheiro"": {dinheiroAtualGov},
    ""petroleo"": {petroleoAtualGov},
    ""status_petroleo"": ""{statusPetroleo}""
  }}
}}
A {nomeRival} (Team {rivalId}) está com forças na fronteira. Relacionamento com {nomeRival}: {relacaoComRival}. Qual é a sua ordem de Estado?";

            return estado;
        }

        private string LimparRespostaJson(string rawJson)
        {
            if (string.IsNullOrEmpty(rawJson)) return "";

            string limpo = rawJson.Trim();

            // Remove blocos de código markdown
            if (limpo.StartsWith("```json"))
            {
                limpo = limpo.Substring(7);
                if (limpo.EndsWith("```"))
                {
                    limpo = limpo.Substring(0, limpo.Length - 3);
                }
            }
            else if (limpo.StartsWith("```"))
            {
                limpo = limpo.Substring(3);
                if (limpo.EndsWith("```"))
                {
                    limpo = limpo.Substring(0, limpo.Length - 3);
                }
            }

            limpo = limpo.Trim();
            
            // Extrai o objeto JSON delimitado por chaves
            int indexAbertura = limpo.IndexOf('{');
            int indexFechamento = limpo.LastIndexOf('}');
            
            if (indexAbertura >= 0 && indexFechamento > indexAbertura)
            {
                limpo = limpo.Substring(indexAbertura, indexFechamento - indexAbertura + 1);
            }

            return limpo;
        }

        private void AoReceberInstrucao(string respostaJson)
        {
            aguardandoResposta = false;
            
            string jsonLimpo = LimparRespostaJson(respostaJson);

            try
            {
                LlamaAcaoResposta acaoLLM = JsonUtility.FromJson<LlamaAcaoResposta>(jsonLimpo);
                
                string detalhesAlvo = acaoLLM.alvo > 0 ? $" -> Alvo: Nação {acaoLLM.alvo}" : "";
                Debug.Log($"<color=#32CD32>[IA Qwen Decidiu]</color> Ação: <b>{acaoLLM.acao}</b>{detalhesAlvo}\nJustificativa: {acaoLLM.justificativa}");
                
                ExecutarAcao(acaoLLM);
            }
            catch (Exception ex)
            {
                Debug.LogError("[IA Qwen] Erro ao parsear JSON da IA. Resposta crua:\n" + respostaJson + "\nErro: " + ex.Message);
            }
        }

        private void AoFalhar(string erro)
        {
            aguardandoResposta = false;
            Debug.LogWarning("[IA Qwen] A IA não pôde processar o cenário via LLM (serviço offline). Mantendo autonomia padrão. Detalhes: " + erro);
        }

        private void ExecutarAcao(LlamaAcaoResposta acaoInst)
        {
            string cmdBaixo = acaoInst.acao.ToLower();

            if (cmdBaixo.Contains("focar_economia"))
            {
                chefe.dinheiro += 500;
                Debug.Log("Llama/Qwen ativou o modo focar_economia.");
            }
            else if (cmdBaixo.Contains("pesquisar_extracao") || cmdBaixo.Contains("focar_pesquisa"))
            {
                var gov = SistemaGovernoMundial.Instancia;
                if (gov != null)
                {
                    gov.DefinirPlanoEstrategico(idNacao, "Pesquisa Extração");
                    gov.AjustarImposto(idNacao, "moradia", -1); // Reduz impostos civis para manter estabilidade
                    
                    Debug.Log($"[IA Qwen] Nação {idNacao} ativou a PESQUISA DE EXTRAÇÃO! Aguardando 60s...");
                    StartCoroutine(ProcessarPesquisaExtracao());
                }
            }
            else if (cmdBaixo.Contains("sancao"))
            {
                var gov = SistemaGovernoMundial.Instancia;
                if (gov != null && acaoInst.alvo > 0)
                {
                    gov.AplicarSancao(acaoInst.alvo);
                    Debug.Log($"[IA Qwen Diplomacia] Nação {idNacao} aplicou SANÇÕES na Nação {acaoInst.alvo}!");
                }
            }
            else if (cmdBaixo.Contains("propor_pacto_defensivo"))
            {
                var gov = SistemaGovernoMundial.Instancia;
                if (gov != null && acaoInst.alvo > 0)
                {
                    gov.ProporPactoDefensivo(acaoInst.alvo);
                    Debug.Log($"[IA Qwen Diplomacia] Nação {idNacao} propôs Pacto Defensivo com Nação {acaoInst.alvo}!");
                }
            }
            else if (cmdBaixo.Contains("invasao_anfibia_combinada"))
            {
                Debug.Log($"[IA Qwen/BrainMaster] Iniciando DOUTRINA DE CHOQUE! Invasão anfíbia na Nação {acaoInst.alvo} delegada ao BrainMaster!");
                var brain = GetComponent<Hegemonia.AI.BrainMaster.IA_BrainMaster>();
                if (brain != null)
                {
                    brain.DefinirDiretrizSuprema("invasao_anfibia_combinada", acaoInst.alvo);
                }
                else
                {
                    Debug.LogError("IA_BrainMaster não encontrado na IA Governante!");
                }
            }
            else
            {
                Debug.Log("Llama/Qwen escolheu aguardar ou comando não reconhecido: " + cmdBaixo);
            }
        }

        private System.Collections.IEnumerator ProcessarPesquisaExtracao()
        {
            yield return new WaitForSeconds(60f);
            var gov = SistemaGovernoMundial.Instancia;
            if (gov != null)
            {
                var meuPais = gov.ObterPais(idNacao);
                if (meuPais != null)
                {
                    meuPais.tecnologiaExtracaoConcluida = true;
                    gov.RegistrarNoticia($"{meuPais.nomePais} dominou a Tecnologia de Extração Avancada!");
                    Debug.Log($"[IA Qwen] Nação {idNacao} CONCLUIU a pesquisa de Extração! Bônus de recursos passivos ativado.");
                }
            }
        }
    }
}
