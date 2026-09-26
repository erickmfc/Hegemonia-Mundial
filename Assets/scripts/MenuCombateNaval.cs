using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Hegemonia.RTS;

/// <summary>
/// Pequeno painel runtime para as ordens de combate navais. O controlador
/// continua sendo dono das armas; esta classe apenas traduz a escolha do
/// jogador para as APIs já existentes.
/// </summary>
public sealed class MenuCombateNaval : MonoBehaviour
{
    public enum Modo { Passivo, Manual, Automatico }

    private static MenuCombateNaval host;
    private static ControleUnidade unidade;
    private static readonly Dictionary<int, Modo> modos = new Dictionary<int, Modo>();
    private static readonly Dictionary<int, bool> limitarAutomaticoPorUnidade = new Dictionary<int, bool>();
    private static readonly Dictionary<int, ControleUnidade> donosDosModos = new Dictionary<int, ControleUnidade>();
    private static readonly Dictionary<int, List<Transform>> alvosPorUnidade = new Dictionary<int, List<Transform>>();
    private static readonly List<Transform> alvos = new List<Transform>(32);
    private static readonly List<GerenciadorQuartel.ContatoMilitarQuartelV2> contatosE3 = new List<GerenciadorQuartel.ContatoMilitarQuartelV2>(128);
    private static readonly List<Transform> autorizados = new List<Transform>(8);
    private static float proximoScan;
    private static bool aguardandoPonto;
    private static bool painelIntegradoExpandido;
    private static GerenciadorQuartel quartel;
    private static Vector2 listaContatosScroll;
    private static string feedback = string.Empty;
    private static float feedbackAte;

    private Camera cameraPrincipal;

    public static void Alternar(ControleUnidade controle)
    {
        if (controle == null || !controle.EhUnidadeNaval()) return;
        GerenciadorPortaAvioes portaAvioes = controle.GetComponent<GerenciadorPortaAvioes>()
            ?? controle.GetComponentInChildren<GerenciadorPortaAvioes>(true);
        if (portaAvioes != null) return; // O ja abre o painel integrado do porta-avioes.

        if (host != null && unidade == controle)
        {
            Fechar();
            return;
        }

        Fechar();
        unidade = controle;
        quartel = ObterQuartel(controle);
        CarregarAlvosDaUnidade(controle);
        host = controle.GetComponent<MenuCombateNaval>();
        if (host == null) host = controle.gameObject.AddComponent<MenuCombateNaval>();
        host.enabled = true;
        host.cameraPrincipal = Camera.main;
        GarantirModoInicial(controle);
        AtualizarAlvos(true);
    }

    public static void DesenharPainelIntegrado(ControleUnidade controle)
    {
        if (controle == null) return;
        if (host != null && unidade != controle) Fechar();
        if (unidade != controle)
        {
            unidade = controle;
            quartel = ObterQuartel(controle);
            CarregarAlvosDaUnidade(controle);
            AtualizarAlvos(true);
        }
        DesenharControles(controle, false);
    }

    public static void ProcessarInputIntegrado(ControleUnidade controle)
    {
        if (controle == null) return;
        if (unidade != controle)
        {
            unidade = controle;
            quartel = ObterQuartel(controle);
            CarregarAlvosDaUnidade(controle);
            AtualizarAlvos(true);
        }
        if (aguardandoPonto && Input.GetMouseButtonDown(0)) ProcessarCliqueDeAlvo();
    }

    public static void CiclarModoLancador(ControleUnidade controle)
    {
        if (controle == null || Obter<LancadorNaval>(controle) == null) return;
        CiclarModoCombate(controle);
    }

    public static string CiclarModoCombate(ControleUnidade controle)
    {
        if (controle == null) return "PASSIVO";
        if (unidade != controle)
        {
            unidade = controle;
            CarregarAlvosDaUnidade(controle);
        }
        int id = controle.GetInstanceID();
        Modo atual = ObterModoAtual(controle);
        Modo proximo = (Modo)(((int)atual + 1) % 3);
        AplicarModo(controle, proximo);
        return proximo.ToString().ToUpperInvariant();
    }

    public static bool DefinirModoCombateHud(ControleUnidade controle, Modo modo, bool limitarAutomatico)
    {
        if (controle == null) return false;
        if (unidade != controle)
        {
            unidade = controle;
            CarregarAlvosDaUnidade(controle);
        }
        AplicarModo(controle, modo, limitarAutomatico);
        return true;
    }

    public static Modo ObterModoCombateHud(ControleUnidade controle)
    {
        if (controle == null) return Modo.Passivo;
        GarantirModoInicial(controle);
        return ObterModoAtual(controle);
    }

    public static bool ModoCombateHudLimitaAlvos(ControleUnidade controle)
    {
        return controle == null
            || !limitarAutomaticoPorUnidade.TryGetValue(controle.GetInstanceID(), out bool limitar)
            || limitar;
    }

    private static void Fechar()
    {
        aguardandoPonto = false;
        if (host != null)
        {
            MenuCombateNaval antigo = host;
            host = null;
            unidade = null;
            quartel = null;
            Destroy(antigo);
        }
    }

    private void Update()
    {
        if (host != this || unidade == null || !unidade.selecionado)
        {
            Fechar();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.O))
        {
            Fechar();
            return;
        }

        if (Time.unscaledTime >= proximoScan) AtualizarAlvos(false);
        if (aguardandoPonto && Input.GetMouseButtonDown(0))
            ProcessarCliqueDeAlvo();
    }

    private void OnGUI()
    {
        if (host != this || unidade == null) return;
        DesenharControles(unidade, true);
    }

    private static void DesenharControles(ControleUnidade controle, bool janela)
    {
        if (controle == null) return;
        GarantirModoInicial(controle);
        AtualizarAlvos(false);
        int id = controle.GetInstanceID();
        if (!modos.TryGetValue(id, out Modo modo)) modo = Modo.Automatico;

        if (janela) GUILayout.BeginArea(new Rect(20f, 90f, 360f, Mathf.Min(Screen.height - 110f, 570f)), GUI.skin.window);
        GUILayout.BeginVertical("box");
        GUILayout.BeginHorizontal();
        GUILayout.Label((janela ? "COMBATE NAVAL — " : "COMBATE — ") + controle.name, GUILayout.ExpandWidth(true));
        if (!janela && GUILayout.Button(painelIntegradoExpandido ? "Ocultar" : "Abrir", GUILayout.Width(68f)))
            painelIntegradoExpandido = !painelIntegradoExpandido;
        if (janela && GUILayout.Button("Fechar (O)", GUILayout.Width(90f))) Fechar();
        GUILayout.EndHorizontal();

        if (!janela && !painelIntegradoExpandido)
        {
            GUILayout.EndVertical();
            if (janela) GUILayout.EndArea();
            return;
        }

        GUILayout.BeginHorizontal();
        DesenharBotaoModo(controle, Modo.Passivo, "Pacífico", modo);
        DesenharBotaoModo(controle, Modo.Manual, "Manual", modo);
        DesenharBotaoModo(controle, Modo.Automatico, "Automático", modo);
        GUILayout.EndHorizontal();

        GUILayout.Label(modo == Modo.Passivo
            ? "Armamento inativo; ordens de movimento continuam disponíveis."
            : modo == Modo.Manual
                ? "Dispare por alvo ou escolha um ponto no mapa. Antiaérea ativa."
                : "O navio só engaja os alvos autorizados. Antiaérea ativa.");

        if (modo != Modo.Passivo)
        {
            GUILayout.Label("Contatos E-3 compartilhados com o Quartel:");
            if (quartel == null)
            {
                GUILayout.Label("Quartel desta equipe indisponível; o painel não busca alvos por outra lista.");
            }
            else if (contatosE3.Count == 0)
            {
                GUILayout.Label("Nenhum contato E-3 memorizado para esta equipe.");
            }

            listaContatosScroll = GUILayout.BeginScrollView(listaContatosScroll, GUILayout.Height(250f));
            for (int i = 0; i < contatosE3.Count; i++)
            {
                GerenciadorQuartel.ContatoMilitarQuartelV2 contato = contatosE3[i];
                if (contato == null) continue;
                Transform alvo = FindAlvoContacto(contato);
                float distancia = Vector3.Distance(controle.transform.position, contato.posicao);
                bool valido = contato.estado == "VALIDO" && Time.unscaledTime <= contato.validadeAte;
                bool autorizado = alvo != null && autorizados.Contains(alvo);
                float validadeRestante = Mathf.Max(0f, contato.validadeAte - Time.unscaledTime);
                GUILayout.BeginVertical("box");
                GUILayout.Label(contato.nome + " | " + contato.tipo + " | " + contato.estado);
                GUILayout.Label("X/Y/Z " + contato.posicao.ToString("F0") + " | " + distancia.ToString("F0") + " m"
                    + " | fonte " + contato.fonte + " (" + contato.transmissor + ")");
                GUILayout.Label("Contato há " + Mathf.Max(0f, Time.unscaledTime - contato.ultimaAtualizacao).ToString("0.0")
                    + " s | validade " + (contato.estado == "PERDIDO" ? "perdida" : validadeRestante.ToString("0.0") + " s"));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Selecionar", GUILayout.Width(72f)))
                {
                    if (quartel.SelecionarLancadorEContato(controle, contato.id))
                        SetFeedback("" + contato.nome + " selecionado no Quartel.");
                    else SetFeedback("Não foi possível selecionar este lançador/contato no Quartel.");
                }
                if (GUILayout.Button("Carta", GUILayout.Width(56f))) LocalizarNaCarta(contato);
                bool podeAtirar = modo == Modo.Manual || valido;
                GUI.enabled = podeAtirar;
                if (GUILayout.Button(modo == Modo.Manual ? "Atirar" : "Atirar auto", GUILayout.Width(78f)))
                {
                    if (quartel.TentarLancamentoRemoto(controle, contato.id, modo == Modo.Automatico, out string motivo))
                        SetFeedback("Disparo remoto autorizado contra " + contato.nome + ".");
                    else SetFeedback(motivo);
                }
                GUI.enabled = true;
                if (modo == Modo.Automatico)
                {
                    GUI.enabled = valido && alvo != null;
                    if (GUILayout.Button(autorizado ? "Retirar" : "Autorizar", GUILayout.Width(74f)))
                    {
                        if (autorizado) autorizados.Remove(alvo);
                        else AlternarAlvoAutorizado(controle, alvo);
                        SalvarAlvosDaUnidade(controle);
                        SincronizarAlvos(controle);
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();

            if (modo == Modo.Manual && (Obter<LancadorNaval>(controle) != null || Obter<ControleSubmarino>(controle) != null))
            {
                if (GUILayout.Button(aguardandoPonto ? "Clique num alvo/ponto no mapa..." : "Escolher ponto no mapa"))
                    aguardandoPonto = !aguardandoPonto;
            }
            GUILayout.Label("Vários alvos por navio: respeita as autorizações e a capacidade do lançador.");
        }

        if (!string.IsNullOrEmpty(feedback) && Time.unscaledTime < feedbackAte)
            GUILayout.Label(feedback);
        GUILayout.EndVertical();
        if (janela) GUILayout.EndArea();
    }

    private static void DesenharBotaoModo(ControleUnidade controle, Modo modo, string rotulo, Modo atual)
    {
        GUI.enabled = modo != atual;
        if (GUILayout.Button(rotulo, GUILayout.Height(28f))) AplicarModo(controle, modo);
        GUI.enabled = true;
    }

    private static void AplicarModo(ControleUnidade controle, Modo modo)
    {
        AplicarModo(controle, modo, true);
    }

    private static void AplicarModo(ControleUnidade controle, Modo modo, bool limitarAutomatico)
    {
        if (controle == null) return;
        int id = controle.GetInstanceID();
        modos[id] = modo;
        donosDosModos[id] = controle;
        bool filtrarAlvos = modo == Modo.Automatico && limitarAutomatico;
        limitarAutomaticoPorUnidade[id] = filtrarAlvos;
        bool ativo = modo != Modo.Passivo;
        controle.DefinirModoCombate(ativo);

        List<Transform> validos = modo == Modo.Automatico ? autorizados : new List<Transform>();
        LancadorNaval[] lancadores = controle.GetComponentsInChildren<LancadorNaval>(true);
        for (int i = 0; i < lancadores.Length; i++)
        {
            lancadores[i].DefinirAlvosAutorizados(validos, filtrarAlvos);
            lancadores[i].DefinirModoIA((LancadorNaval.ModoOperacao)modo, false);
        }

        ControleSubmarino[] submarinos = controle.GetComponentsInChildren<ControleSubmarino>(true);
        for (int i = 0; i < submarinos.Length; i++)
        {
            submarinos[i].DefinirAlvosAutorizados(validos, filtrarAlvos);
            submarinos[i].DefinirModoOperacao((ControleSubmarino.ModoOperacao)modo, false);
        }

        ControleTorreta[] torretas = controle.GetComponentsInChildren<ControleTorreta>(true);
        for (int i = 0; i < torretas.Length; i++)
            torretas[i].ConfigurarModoCombateNaval(ativo, modo == Modo.Manual, filtrarAlvos, validos);

        ControleTorretaModular[] modulares = controle.GetComponentsInChildren<ControleTorretaModular>(true);
        for (int i = 0; i < modulares.Length; i++)
            modulares[i].ConfigurarModoCombateNaval(ativo, modo == Modo.Manual, filtrarAlvos, validos);

        SistemaDeTiro[] armasDiretas = controle.GetComponentsInChildren<SistemaDeTiro>(true);
        for (int i = 0; i < armasDiretas.Length; i++)
            armasDiretas[i].ConfigurarModoCombateNaval(ativo, modo == Modo.Manual, filtrarAlvos, validos);

        SincronizarAlvos(controle);
        SetFeedback("Modo de combate: " + modo + ".");
    }

    private static void AlternarAlvoAutorizado(ControleUnidade controle, Transform alvo)
    {
        if (controle == null || alvo == null) return;
        if (autorizados.Contains(alvo)) autorizados.Remove(alvo);
        else
        {
            LancadorNaval lancador = Obter<LancadorNaval>(controle);
            bool suportaVarios = lancador != null && lancador.tirosPorSalva > 1;
            if (!suportaVarios) autorizados.Clear();
            autorizados.Add(alvo);
        }
        SalvarAlvosDaUnidade(controle);
        SincronizarAlvos(controle);
        if (modos.TryGetValue(controle.GetInstanceID(), out Modo modo) && modo == Modo.Automatico)
            AplicarModo(controle, modo, ModoCombateHudLimitaAlvos(controle));
    }

    private static void SincronizarAlvos(ControleUnidade controle)
    {
        if (controle == null) return;
        bool filtrarAlvos = ModoCombateHudLimitaAlvos(controle);
        LancadorNaval[] lancadores = controle.GetComponentsInChildren<LancadorNaval>(true);
        for (int i = 0; i < lancadores.Length; i++)
            lancadores[i].DefinirAlvosAutorizados(autorizados, filtrarAlvos);
        ControleSubmarino[] submarinos = controle.GetComponentsInChildren<ControleSubmarino>(true);
        for (int i = 0; i < submarinos.Length; i++)
            submarinos[i].DefinirAlvosAutorizados(autorizados, filtrarAlvos);
        ControleTorreta[] torretas = controle.GetComponentsInChildren<ControleTorreta>(true);
        for (int i = 0; i < torretas.Length; i++)
            torretas[i].DefinirAlvosAutorizados(autorizados, filtrarAlvos);
        ControleTorretaModular[] modulares = controle.GetComponentsInChildren<ControleTorretaModular>(true);
        for (int i = 0; i < modulares.Length; i++)
            modulares[i].DefinirAlvosAutorizados(autorizados, filtrarAlvos);
        SincronizarLancadoresAereosHud(controle, ObterModoCombateHud(controle) == Modo.Manual, filtrarAlvos);
        SistemaDeTiro[] sistemasDeTiro = controle.GetComponentsInChildren<SistemaDeTiro>(true);
        for (int i = 0; i < sistemasDeTiro.Length; i++)
            sistemasDeTiro[i].ConfigurarModoCombateNaval(
                controle.ModoCombateAtivo,
                ObterModoCombateHud(controle) == Modo.Manual,
                filtrarAlvos,
                autorizados);
    }

    private static void SincronizarLancadoresAereosHud(ControleUnidade controle, bool manual, bool filtrarAlvos)
    {
        if (controle == null) return;
        LancadorMisselCaca[] lancadores = controle.GetComponentsInChildren<LancadorMisselCaca>(true);
        for (int i = 0; i < lancadores.Length; i++)
            lancadores[i].DefinirPoliticaTaticaHud(manual, autorizados, filtrarAlvos);
    }

    private static void CarregarAlvosDaUnidade(ControleUnidade controle)
    {
        autorizados.Clear();
        if (controle == null) return;
        int id = controle.GetInstanceID();
        if (donosDosModos.TryGetValue(id, out ControleUnidade donoAnterior) && donoAnterior != controle)
        {
            modos.Remove(id);
            donosDosModos.Remove(id);
            alvosPorUnidade.Remove(id);
        }
        if (alvosPorUnidade.TryGetValue(id, out List<Transform> guardados))
            autorizados.AddRange(guardados.FindAll(t => t != null));
    }

    private static void SalvarAlvosDaUnidade(ControleUnidade controle)
    {
        if (controle == null) return;
        int id = controle.GetInstanceID();
        alvosPorUnidade[id] = new List<Transform>(autorizados);
        donosDosModos[id] = controle;
    }

    private static void GarantirModoInicial(ControleUnidade controle)
    {
        if (controle == null) return;
        int id = controle.GetInstanceID();
        if (donosDosModos.TryGetValue(id, out ControleUnidade donoAnterior) && donoAnterior != controle)
        {
            modos.Remove(id);
            donosDosModos.Remove(id);
            alvosPorUnidade.Remove(id);
        }
        if (!modos.ContainsKey(id)) modos[id] = ObterModoAtual(controle);
        donosDosModos[id] = controle;
    }

    private static Modo ObterModoAtual(ControleUnidade controle)
    {
        LancadorNaval lancador = Obter<LancadorNaval>(controle);
        if (lancador != null) return (Modo)lancador.modoAtual;
        ControleSubmarino submarino = Obter<ControleSubmarino>(controle);
        if (submarino != null) return (Modo)submarino.modoAtual;
        int id = controle.GetInstanceID();
        if (donosDosModos.TryGetValue(id, out ControleUnidade dono) && dono == controle
            && modos.TryGetValue(id, out Modo salvo)) return salvo;
        return controle.ModoCombateAtivo ? Modo.Automatico : Modo.Passivo;
    }

    private static void EngajarAlvo(ControleUnidade controle, Transform alvo)
    {
        if (controle == null || alvo == null) return;
        AplicarModo(controle, Modo.Manual);
        bool disparou = false;
        LancadorNaval lancador = Obter<LancadorNaval>(controle);
        if (lancador != null)
        {
            bool ok = lancador.TentarLancarCoordenado(alvo.position, alvo, false, out string motivo);
            disparou |= ok;
            if (!ok) SetFeedback("Lançador: " + motivo);
        }
        ControleSubmarino sub = Obter<ControleSubmarino>(controle);
        if (sub != null)
        {
            bool ok = sub.TentarLancarCoordenado(alvo.position, alvo, false, out string motivo);
            disparou |= ok;
            if (!ok) SetFeedback("Submarino: " + motivo);
        }
        ControleTorreta[] torretas = controle.GetComponentsInChildren<ControleTorreta>(true);
        for (int i = 0; i < torretas.Length; i++)
            if (torretas[i] != null && torretas[i].DefinirAlvoManual(alvo)) disparou = true;
        ControleTorretaModular[] modulares = controle.GetComponentsInChildren<ControleTorretaModular>(true);
        for (int i = 0; i < modulares.Length; i++)
            if (modulares[i] != null && modulares[i].DefinirAlvoManual(alvo)) disparou = true;
        SistemaDeTiro[] armasDiretas = controle.GetComponentsInChildren<SistemaDeTiro>(true);
        for (int i = 0; i < armasDiretas.Length; i++)
            if (armasDiretas[i] != null && armasDiretas[i].DefinirAlvoManual(alvo)) disparou = true;
        SetFeedback(disparou ? "Ordem manual enviada para " + alvo.name + "." : "Nenhuma arma disponível dentro do alcance.");
    }

    private static void ProcessarCliqueDeAlvo()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        Camera cameraPrincipal = Camera.main;
        if (cameraPrincipal == null) return;
        Ray raio = cameraPrincipal.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(raio, out RaycastHit hit, Mathf.Infinity))
        {
            SetFeedback("O clique não atingiu uma superfície do mapa.");
            aguardandoPonto = false;
            return;
        }
        IdentidadeUnidade idAlvo = hit.transform.GetComponentInParent<IdentidadeUnidade>();
        Transform alvo = idAlvo != null ? idAlvo.transform : null;
        if (alvo != null && alvos.Contains(alvo)) EngajarAlvo(unidade, alvo);
        else if (idAlvo == null) DispararEmPonto(hit.point);
        else SetFeedback("Alvo inválido, aliado ou fora do alcance.");
        aguardandoPonto = false;
    }

    private static void DispararEmPonto(Vector3 ponto)
    {
        bool disparou = false;
        LancadorNaval lancador = Obter<LancadorNaval>(unidade);
        if (lancador != null) disparou |= lancador.TentarLancarCoordenado(ponto, null, false, out string _);
        ControleSubmarino sub = Obter<ControleSubmarino>(unidade);
        if (sub != null) disparou |= sub.TentarLancarCoordenado(ponto, null, false, out string _);
        SetFeedback(disparou ? "Disparo manual enviado para o ponto marcado." : "Nenhum lançador manual aceitou o ponto.");
        aguardandoPonto = false;
    }

    private static void AtualizarAlvos(bool forcar)
    {
        if (unidade == null || (!forcar && Time.unscaledTime < proximoScan)) return;
        proximoScan = Time.unscaledTime + 0.5f;
        alvos.Clear();
        contatosE3.Clear();
        IdentidadeUnidade identidade = unidade.GetComponent<IdentidadeUnidade>()
            ?? unidade.GetComponentInParent<IdentidadeUnidade>()
            ?? unidade.GetComponentInChildren<IdentidadeUnidade>(true);
        int timeMeu = identidade != null ? identidade.teamID : 1;
        if (quartel == null || !quartel.isActiveAndEnabled || quartel.teamID != timeMeu)
            quartel = ObterQuartel(unidade);
        if (quartel != null)
        {
            quartel.AtualizarDadosLancamento(false);
            for (int i = 0; i < quartel.ContatosMilitares.Count; i++)
            {
                GerenciadorQuartel.ContatoMilitarQuartelV2 contato = quartel.ContatosMilitares[i];
                if (contato == null || !contato.inimigo) continue;
                contatosE3.Add(contato);
                Transform alvo = FindAlvoContacto(contato);
                if (contato.estado != "VALIDO" || Time.unscaledTime > contato.validadeAte
                    || alvo == null || !alvo.gameObject.activeInHierarchy
                    || !ControleSubmarino.PodeSerAlvoConvencional(alvo)) continue;

                float distancia = Vector3.Distance(unidade.transform.position, contato.posicao);
                if (!ArmaAlcanca(unidade, alvo, distancia)) continue;
                if (!alvos.Contains(alvo)) alvos.Add(alvo);
            }
        }

        alvos.Sort((a, b) => Vector3.Distance(unidade.transform.position, a.position).CompareTo(Vector3.Distance(unidade.transform.position, b.position)));
        int removidos = autorizados.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy || !alvos.Contains(t));
        if (removidos > 0)
        {
            SalvarAlvosDaUnidade(unidade);
            SincronizarAlvos(unidade);
        }
    }

    private static float ObterAlcance(ControleUnidade c)
    {
        float alcance = 0f;
        LancadorNaval l = Obter<LancadorNaval>(c);
        if (l != null && (l.municaoTotal > 0 || l.torpedosTotal > 0)) alcance = Mathf.Max(alcance, l.alcanceRadar);
        ControleSubmarino s = Obter<ControleSubmarino>(c);
        if (s != null && s.misseisDisponiveis > 0) alcance = Mathf.Max(alcance, s.alcanceMisseis);
        ControleTorreta[] t = c.GetComponentsInChildren<ControleTorreta>(true);
        for (int i = 0; i < t.Length; i++) if (t[i] != null && !t[i].EhAntiAereo) alcance = Mathf.Max(alcance, t[i].alcance);
        ControleTorretaModular[] m = c.GetComponentsInChildren<ControleTorretaModular>(true);
        for (int i = 0; i < m.Length; i++) if (m[i] != null && !m[i].EhAntiAereo) alcance = Mathf.Max(alcance, m[i].alcanceRadar);
        return alcance;
    }

    private static Transform FindAlvoContacto(GerenciadorQuartel.ContatoMilitarQuartelV2 contato)
    {
        return contato != null && contato.transformAlvo != null && contato.transformAlvo.gameObject.activeInHierarchy
            ? contato.transformAlvo
            : null;
    }

    private static GerenciadorQuartel ObterQuartel(ControleUnidade controle)
    {
        if (controle == null) return null;
        IdentidadeUnidade identidade = controle.GetComponent<IdentidadeUnidade>()
            ?? controle.GetComponentInParent<IdentidadeUnidade>()
            ?? controle.GetComponentInChildren<IdentidadeUnidade>(true);
        int equipe = identidade != null ? identidade.teamID : 1;
        GerenciadorQuartel[] quarteis = FindObjectsByType<GerenciadorQuartel>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        GerenciadorQuartel escolhido = null;
        float menorDistancia = float.PositiveInfinity;
        for (int i = 0; i < quarteis.Length; i++)
        {
            GerenciadorQuartel candidato = quarteis[i];
            if (candidato == null || !candidato.isActiveAndEnabled || candidato.teamID != equipe) continue;
            float distancia = (candidato.transform.position - controle.transform.position).sqrMagnitude;
            if (distancia >= menorDistancia) continue;
            menorDistancia = distancia;
            escolhido = candidato;
        }
        return escolhido;
    }

    private static void LocalizarNaCarta(GerenciadorQuartel.ContatoMilitarQuartelV2 contato)
    {
        if (contato == null || quartel == null) return;
        QuartelMenuUIController painel = quartel.GetComponent<QuartelMenuUIController>();
        bool localizado = painel != null && painel.LocalizarContatoNaCarta(contato.id, contato.posicao);
        SetFeedback(localizado ? "Contato centralizado na Carta Náutica." : "Não foi possível abrir a Carta Náutica deste Quartel.");
    }

    private static bool ArmaAlcanca(ControleUnidade c, Transform alvo, float distancia)
    {
        LancadorNaval l = Obter<LancadorNaval>(c);
        if (l != null && (l.municaoTotal > 0 || l.torpedosTotal > 0) && distancia <= l.alcanceRadar) return true;
        ControleSubmarino s = Obter<ControleSubmarino>(c);
        if (s != null && s.misseisDisponiveis > 0 && distancia <= s.alcanceMisseis) return true;
        ControleTorreta[] t = c.GetComponentsInChildren<ControleTorreta>(true);
        for (int i = 0; i < t.Length; i++) if (t[i] != null && !t[i].EhAntiAereo && distancia <= t[i].alcance) return true;
        ControleTorretaModular[] m = c.GetComponentsInChildren<ControleTorretaModular>(true);
        for (int i = 0; i < m.Length; i++) if (m[i] != null && !m[i].EhAntiAereo && distancia <= m[i].alcanceRadar) return true;
        return false;
    }

    private static T Obter<T>(ControleUnidade c) where T : Component
    {
        return c == null ? null : c.GetComponent<T>() ?? c.GetComponentInChildren<T>(true);
    }

    private static void SetFeedback(string texto)
    {
        feedback = texto;
        feedbackAte = Time.unscaledTime + 4f;
    }
}
