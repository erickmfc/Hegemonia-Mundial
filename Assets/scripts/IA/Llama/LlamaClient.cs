using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Diagnostics;

namespace Hegemonia.AI.Llama
{
    [Serializable]
    public class OllamaRequest
    {
        public string model;
        public string prompt;
        public bool stream;
        public string format;
    }

    [Serializable]
    public class OllamaResponse
    {
        public string model;
        public string response;
        public bool done;
    }

    [Serializable]
    public class OllamaPullRequest
    {
        public string name;
        public bool stream;
    }

    [Serializable]
    public class OllamaPullResponse
    {
        public string status;
        public string digest;
        public long total;
        public long completed;
    }

    public class OllamaStreamHandler : DownloadHandlerScript
    {
        private Action<string> onChunkReceived;
        private string buffer = "";
        
        public OllamaStreamHandler(Action<string> onChunk) : base(new byte[16384]) 
        {
            this.onChunkReceived = onChunk;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0) return false;
            string text = Encoding.UTF8.GetString(data, 0, dataLength);
            buffer += text;
            
            int lastNewline = buffer.LastIndexOf('\n');
            if (lastNewline >= 0)
            {
                string completeLines = buffer.Substring(0, lastNewline);
                buffer = buffer.Substring(lastNewline + 1);
                onChunkReceived?.Invoke(completeLines);
            }
            return true;
        }
    }

    public class LlamaClient : MonoBehaviour
    {
        [Header("Configurações do Ollama")]
        public string ollamaUrl = "http://localhost:11434/api/generate";
        public string modeloAtivo = "qwen2.5:1.5b";
        public AnimacaoIA_Terminal animacaoTerminal;
        
        [Tooltip("Define se a IA deve forçar a saída a ser um JSON estruturado.")]
        public bool forcarFormatoJson = true;

        public static LlamaClient Instancia { get; private set; }

        private void Awake()
        {
            if (Instancia == null)
            {
                Instancia = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // Integração Llama/Ollama desativada. Mantemos o componente inerte para compatibilidade.
            enabled = false;
        }

        private void LigarMotorOllama()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/c ollama serve";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden; 
            startInfo.CreateNoWindow = true;
            Process.Start(startInfo);
        }

        IEnumerator AguardarIAFicarPronta()
        {
            // --- ETAPA 1: Aguardar Servidor Ollama Iniciar ---
            bool online = false;
            string urlCheck = "http://127.0.0.1:11434/";

            if (animacaoTerminal != null)
                animacaoTerminal.AtualizarProgresso("Conectando ao Servidor Ollama...", 0, 0);

            while (!online)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(urlCheck))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        online = true;
                        UnityEngine.Debug.Log("[LlamaClient] Servidor Neural Online!");
                    }
                }
                if (!online)
                {
                    if (animacaoTerminal != null)
                        animacaoTerminal.AtualizarProgresso("Tentando conectar ao Ollama...", 0, 0);
                    yield return new WaitForSeconds(2f);
                }
            }

            // --- ETAPA 2: Verificar e Baixar o Modelo ---
            if (animacaoTerminal != null)
                animacaoTerminal.AtualizarProgresso("PROCURANDO DIRETRIZ NEURAL...", 0, 0);

            string urlPull = "http://127.0.0.1:11434/api/pull";
            OllamaPullRequest pullReq = new OllamaPullRequest { name = modeloAtivo, stream = true };
            string jsonPull = JsonUtility.ToJson(pullReq);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPull);

            using (UnityWebRequest request = new UnityWebRequest(urlPull, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new OllamaStreamHandler((textoCompleto) => {
                    AtualizarProgressoDownload(textoCompleto);
                });
                request.SetRequestHeader("Content-Type", "application/json");

                UnityWebRequestAsyncOperation asyncOp = request.SendWebRequest();

                while (!asyncOp.isDone)
                {
                    yield return new WaitForSeconds(0.2f);
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.Log("[LlamaClient] Modelo verificado/baixado com sucesso.");
                }
                else
                {
                    UnityEngine.Debug.LogError("[LlamaClient] Erro ao baixar modelo via API: " + request.error);
                }
            }

            // --- ETAPA 3: Pré-carregar o modelo na GPU/VRAM ---
            if (animacaoTerminal != null)
                animacaoTerminal.AtualizarProgresso("GRAVANDO MATRIZ NEURAL...", 0, 0);

            string urlGen = "http://127.0.0.1:11434/api/generate";
            OllamaRequest genReq = new OllamaRequest { model = modeloAtivo, prompt = "", stream = false };
            string jsonGen = JsonUtility.ToJson(genReq);
            byte[] bodyRawGen = Encoding.UTF8.GetBytes(jsonGen);

            using (UnityWebRequest request = new UnityWebRequest(urlGen, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRawGen);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.Log("[LlamaClient] Motor Neural totalmente carregado na GPU.");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[LlamaClient] Falha ao pré-carregar modelo: " + request.error);
                }
            }

            // --- ETAPA 4: Concluir e Ocultar Animação ---
            if (animacaoTerminal != null)
            {
                animacaoTerminal.AtualizarProgresso("SINCRONIZAÇÃO COMPLETA!", 0, 0);
                yield return new WaitForSeconds(1f);
                animacaoTerminal.OcultarAnimacao();
            }
        }

        private void AtualizarProgressoDownload(string rawText)
        {
            if (animacaoTerminal == null) return;

            string[] lines = rawText.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    OllamaPullResponse pullResp = JsonUtility.FromJson<OllamaPullResponse>(line);
                    if (pullResp != null && !string.IsNullOrEmpty(pullResp.status))
                    {
                        string statusTraduzido = TraduzirStatusOllama(pullResp.status);
                        animacaoTerminal.AtualizarProgresso(statusTraduzido, pullResp.completed, pullResp.total);
                        break;
                    }
                }
                catch
                {
                    // Ignora JSON incompleto nas pontas do buffer
                }
            }
        }

        private string TraduzirStatusOllama(string status)
        {
            if (status.StartsWith("pulling manifest")) return "PROCURANDO DIRETRIZ NEURAL...";
            if (status.StartsWith("downloading")) return "BAIXANDO BANCO DE DADOS ESTRATÉGICO...";
            if (status.StartsWith("verifying")) return "VERIFICANDO ASSINATURA CRIPTOGRÁFICA...";
            if (status.StartsWith("writing")) return "GRAVANDO MATRIZ NEURAL...";
            if (status.StartsWith("success")) return "SINCRONIZAÇÃO COMPLETA!";
            return status.ToUpper();
        }

        /// <summary>
        /// Envia um prompt para o Llama e retorna um JSON estruturado.
        /// </summary>
        public void EnviarPrompt(string systemPrompt, string gameStatePrompt, Action<string> onSucesso, Action<string> onErro)
        {
            string promptCompleto = $"{systemPrompt}\n\n[DADOS DA NAÇÃO]\n{gameStatePrompt}";
            StartCoroutine(RequisicaoOllama(promptCompleto, onSucesso, onErro));
        }

        private IEnumerator RequisicaoOllama(string prompt, Action<string> onSucesso, Action<string> onErro)
        {
            OllamaRequest reqData = new OllamaRequest
            {
                model = modeloAtivo,
                prompt = prompt,
                stream = false
            };

            if (forcarFormatoJson)
            {
                reqData.format = "json";
            }

            string jsonRequestBody = JsonUtility.ToJson(reqData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequestBody);

            using (UnityWebRequest request = new UnityWebRequest(ollamaUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    UnityEngine.Debug.LogWarning("[LlamaClient] Erro ao contatar Ollama (serviço offline): " + request.error);
                    onErro?.Invoke(request.error);
                }
                else
                {
                    string jsonResponse = request.downloadHandler.text;
                    try
                    {
                        OllamaResponse respostaOllama = JsonUtility.FromJson<OllamaResponse>(jsonResponse);
                        onSucesso?.Invoke(respostaOllama.response);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("[LlamaClient] Erro ao parsear resposta do Ollama: " + ex.Message);
                        onErro?.Invoke("Erro de parse: " + ex.Message);
                    }
                }
            }
        }
    }
}
