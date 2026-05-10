using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10010)]
public sealed class AuditoriaConteudoJogo : MonoBehaviour
{
    private static AuditoriaConteudoJogo instancia;

    private readonly HashSet<int> fichasAuditadas = new HashSet<int>();
    private readonly List<DadosConstrucao> fichas = new List<DadosConstrucao>(256);
    private readonly StringBuilder resumoBuilder = new StringBuilder(256);
    private Coroutine rotinaAuditoria;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void GarantirInstancia()
    {
        if (instancia != null)
        {
            return;
        }

        AuditoriaConteudoJogo existente = FindFirstObjectByType<AuditoriaConteudoJogo>();
        if (existente != null)
        {
            instancia = existente;
            DontDestroyOnLoad(existente.gameObject);
            existente.AgendarAuditoria();
            return;
        }

        GameObject obj = new GameObject("AuditoriaConteudoJogo");
        instancia = obj.AddComponent<AuditoriaConteudoJogo>();
        DontDestroyOnLoad(obj);
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AgendarAuditoria();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AgendarAuditoria();
    }

    private void AgendarAuditoria()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        if (rotinaAuditoria != null)
        {
            StopCoroutine(rotinaAuditoria);
        }

        rotinaAuditoria = StartCoroutine(RodarAuditoriaAtrasada());
    }

    private IEnumerator RodarAuditoriaAtrasada()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        rotinaAuditoria = null;
        RodarAuditoria();
    }

    private void RodarAuditoria()
    {
        fichas.Clear();
        fichasAuditadas.Clear();
        ColetarFichas();

        int erros = 0;
        int avisos = 0;
        int eventosEmitidos = 0;
        const int limiteEventos = 24;

        for (int i = 0; i < fichas.Count; i++)
        {
            DadosConstrucao ficha = fichas[i];
            if (ficha == null)
            {
                erros++;
                Emitir("ERRO", "Ficha nula no catalogo", ref eventosEmitidos, limiteEventos);
                continue;
            }

            AuditarFicha(ficha, ref erros, ref avisos, ref eventosEmitidos, limiteEventos);
        }

        resumoBuilder.Length = 0;
        resumoBuilder.Append("fichas=").Append(fichas.Count)
            .Append(" erros=").Append(erros)
            .Append(" avisos=").Append(avisos)
            .Append(" cena=").Append(SceneManager.GetActiveScene().name);

        string resumo = resumoBuilder.ToString();
        DiagnosticoDesempenhoJogo.RegistrarTextoMetrica("auditoria_conteudo", resumo);
        DiagnosticoDesempenhoJogo.RegistrarEvento("AuditoriaConteudo", resumo);
    }

    private void ColetarFichas()
    {
        if (MenuConstrucao.catalogoGlobal != null)
        {
            for (int i = 0; i < MenuConstrucao.catalogoGlobal.Count; i++)
            {
                AdicionarFicha(MenuConstrucao.catalogoGlobal[i]);
            }
        }

        DadosConstrucao[] fallback = Resources.FindObjectsOfTypeAll<DadosConstrucao>();
        for (int i = 0; i < fallback.Length; i++)
        {
            AdicionarFicha(fallback[i]);
        }
    }

    private void AdicionarFicha(DadosConstrucao ficha)
    {
        if (ficha == null)
        {
            fichas.Add(null);
            return;
        }

        int id = ficha.GetInstanceID();
        if (fichasAuditadas.Add(id))
        {
            fichas.Add(ficha);
        }
    }

    private void AuditarFicha(DadosConstrucao ficha, ref int erros, ref int avisos, ref int eventosEmitidos, int limiteEventos)
    {
        string nome = string.IsNullOrWhiteSpace(ficha.nomeItem) ? ficha.name : ficha.nomeItem;
        GameObject prefab = ficha.prefabDaUnidade;

        if (string.IsNullOrWhiteSpace(ficha.nomeItem))
        {
            avisos++;
            Emitir("AVISO", "Ficha sem nome: " + ficha.name, ref eventosEmitidos, limiteEventos);
        }

        if (ficha.preco < 0)
        {
            erros++;
            Emitir("ERRO", nome + ": preco negativo (" + ficha.preco + ")", ref eventosEmitidos, limiteEventos);
        }

        if (prefab == null)
        {
            erros++;
            Emitir("ERRO", nome + ": prefab ausente", ref eventosEmitidos, limiteEventos);
            return;
        }

        bool materialPersistente = ficha.categoria != DadosConstrucao.CategoriaItem.Tecnologia;
        bool unidadeCombate = EhCategoriaMilitar(ficha.categoria) && !EhLogisticaOuEstrutura(nome, prefab.name);

        if (materialPersistente && prefab.GetComponentInChildren<Collider>(true) == null)
        {
            avisos++;
            Emitir("AVISO", nome + ": prefab sem collider", ref eventosEmitidos, limiteEventos);
        }

        if (materialPersistente && prefab.GetComponentInChildren<SaveableEntity>(true) == null)
        {
            avisos++;
            Emitir("AVISO", nome + ": prefab sem SaveableEntity para save completo", ref eventosEmitidos, limiteEventos);
        }

        if (EhCategoriaMilitar(ficha.categoria) && prefab.GetComponentInChildren<IdentidadeUnidade>(true) == null)
        {
            avisos++;
            Emitir("AVISO", nome + ": unidade militar sem IdentidadeUnidade", ref eventosEmitidos, limiteEventos);
        }

        if (materialPersistente && prefab.GetComponentInChildren<SistemaDeDanos>(true) == null)
        {
            avisos++;
            Emitir("AVISO", nome + ": prefab sem SistemaDeDanos", ref eventosEmitidos, limiteEventos);
        }

        if (unidadeCombate && !TemComponenteDeArma(prefab))
        {
            avisos++;
            Emitir("AVISO", nome + ": unidade de combate sem arma/sistema de tiro detectado", ref eventosEmitidos, limiteEventos);
        }

        string suspeitaCategoria = DetectarCategoriaSuspeita(ficha, prefab);
        if (!string.IsNullOrEmpty(suspeitaCategoria))
        {
            avisos++;
            Emitir("AVISO", nome + ": categoria suspeita - " + suspeitaCategoria, ref eventosEmitidos, limiteEventos);
        }
    }

    private bool EhCategoriaMilitar(DadosConstrucao.CategoriaItem categoria)
    {
        return categoria == DadosConstrucao.CategoriaItem.Exercito
               || categoria == DadosConstrucao.CategoriaItem.Marinha
               || categoria == DadosConstrucao.CategoriaItem.Aeronautica;
    }

    private bool EhLogisticaOuEstrutura(string nomeItem, string nomePrefab)
    {
        string texto = Normalizar(nomeItem + " " + nomePrefab);
        return texto.Contains("petroleiro")
               || texto.Contains("petrolifero")
               || texto.Contains("tanker")
               || texto.Contains("transporte")
               || texto.Contains("aeroporto")
               || texto.Contains("heliporto")
               || texto.Contains("estaleiro")
               || texto.Contains("pier")
               || texto.Contains("plataforma")
               || texto.Contains("prefeitura")
               || texto.Contains("quartel")
               || texto.Contains("fabrica");
    }

    private bool TemComponenteDeArma(GameObject prefab)
    {
        MonoBehaviour[] componentes = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < componentes.Length; i++)
        {
            MonoBehaviour componente = componentes[i];
            if (componente == null)
            {
                continue;
            }

            string tipo = componente.GetType().Name.ToLowerInvariant();
            if (tipo.Contains("tiro")
                || tipo.Contains("torreta")
                || tipo.Contains("missil")
                || tipo.Contains("arma")
                || tipo.Contains("canhao")
                || tipo.Contains("torpedo")
                || tipo.Contains("bombardeiro"))
            {
                return true;
            }
        }

        return false;
    }

    private string DetectarCategoriaSuspeita(DadosConstrucao ficha, GameObject prefab)
    {
        string texto = Normalizar(ficha.nomeItem + " " + prefab.name);
        if ((texto.Contains("navio") || texto.Contains("submarino") || texto.Contains("fragata") || texto.Contains("corveta") || texto.Contains("petroleiro"))
            && ficha.categoria != DadosConstrucao.CategoriaItem.Marinha)
        {
            return "parece naval, mas categoria e " + ficha.categoria;
        }

        if ((texto.Contains("aviao") || texto.Contains("caca") || texto.Contains("helicoptero") || texto.Contains("helice"))
            && ficha.categoria != DadosConstrucao.CategoriaItem.Aeronautica)
        {
            return "parece aereo, mas categoria e " + ficha.categoria;
        }

        if ((texto.Contains("soldado") || texto.Contains("tanque") || texto.Contains("blindado") || texto.Contains("infantaria"))
            && ficha.categoria != DadosConstrucao.CategoriaItem.Exercito)
        {
            return "parece terrestre, mas categoria e " + ficha.categoria;
        }

        return string.Empty;
    }

    private string Normalizar(string texto)
    {
        return string.IsNullOrEmpty(texto) ? string.Empty : texto.ToLowerInvariant();
    }

    private void Emitir(string nivel, string mensagem, ref int eventosEmitidos, int limiteEventos)
    {
        string linha = nivel + ": " + mensagem;
        if (eventosEmitidos < limiteEventos)
        {
            eventosEmitidos++;
            DiagnosticoDesempenhoJogo.RegistrarEvento("AuditoriaConteudo", linha);
        }

        if (nivel == "ERRO")
        {
            Debug.LogError("[AuditoriaConteudo] " + mensagem, this);
        }
        else
        {
            Debug.LogWarning("[AuditoriaConteudo] " + mensagem, this);
        }
    }
}
