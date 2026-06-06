using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ControleAviaoComercial : ControleAviao
{
    public enum TipoPropulsao { Turbina, Helice }

    [Header("=== PROPULSÃO COMERCIAL ===")]
    public TipoPropulsao tipoPropulsao = TipoPropulsao.Turbina;

    [Tooltip("Hélices que devem girar (se tipo for Helice)")]
    public List<Transform> helices = new List<Transform>();
    public Vector3 eixoGiroHelice = Vector3.forward;
    public float velocidadeMaxGiroHelice = 1500f;

    [Tooltip("Turbinas/Partículas de jato (se tipo for Turbina)")]
    public List<ParticleSystem> rastroTurbinas = new List<ParticleSystem>();
    public List<Light> luzesTurbina = new List<Light>();

    [Header("=== PARÂMETROS REALISTAS COMERCIAIS ===")]
    [Tooltip("Suavidade da aceleração (inércia do motor/aeronave)")]
    public float inerciaAceleracao = 1.5f;
    [Tooltip("Altitude padrão para aviões comerciais (cruzeiro mais alto e estável)")]
    public float altitudeCruzeiroComercial = 90f;
    [Tooltip("Ângulo máximo de curva para aviões comerciais (curvas mais suaves, ex: 25 graus)")]
    public float asaBankingComercial = 25f;
    [Tooltip("Pitch máximo na decolagem/subida (subida suave e elegante, ex: 12 graus)")]
    public float arfagemPitchComercial = 12f;

    [Header("=== ÁUDIO COMERCIAL ===")]
    [Tooltip("Som do motor de turbina ou hélice comercial")]
    public AudioSource somMotorComercial;

    private float velocidadeGiroHeliceAtual = 0f;

    [Header("=== INFORMAÇÕES DE VOO ===")]
    public string nomeCompanhia = "Independente";
    public string nomeDestinoIA = "";
    public int passagensVendidas = 0;
    private bool destinoCalculado = false;
    private float timerVooExterior = 0f;

    protected override void Start()
    {
        base.Start();

        // Configura parâmetros básicos do avião para um comportamento comercial estável
        velocidadeSolo = Mathf.Max(velocidadeSolo, 10f);
        asaBankingMaximo = asaBankingComercial;
        arfagemPitchMaxima = arfagemPitchComercial;

        // Se o som do motor comercial não estiver configurado, tenta buscar um na hierarquia
        if (somMotorComercial == null)
        {
            somMotorComercial = GetComponent<AudioSource>();
        }
        if (somMotorComercial == null)
        {
            somMotorComercial = GetComponentInChildren<AudioSource>();
        }

        if (somMotorComercial != null)
        {
            somMotorComercial.loop = true;
            somMotorComercial.spatialBlend = 1f; // 3D Sound
        }
    }

    private void DefinirDestinoComercial()
    {
        if (destinoCalculado) return;
        destinoCalculado = true;

        if (GerenciadorDivisaoTerritorial.Instancia != null && aeroportoOrigem != null)
        {
            IdentidadeUnidade identidadeAeroporto = aeroportoOrigem.GetComponent<IdentidadeUnidade>();
            int meuTeam = identidadeAeroporto != null ? identidadeAeroporto.teamID : 1;

            List<CidadeEstado> destinosPossiveis = new List<CidadeEstado>();
            foreach(var cid in GerenciadorDivisaoTerritorial.Instancia.cidades)
            {
                if (cid.temAeroporto && cid.teamID != meuTeam)
                {
                    destinosPossiveis.Add(cid);
                }
            }

            if (destinosPossiveis.Count > 0)
            {
                var cidadeDestino = destinosPossiveis[Random.Range(0, destinosPossiveis.Count)];
                nomeDestinoIA = cidadeDestino.nome;
                alvoGPSVoo = (cidadeDestino.marcador != null) ? cidadeDestino.marcador.transform.position : Vector3.zero;
                alvoGPSVoo.y = altitudeCruzeiroComercial;
                return;
            }
        }
        
        // Se não achou aeroporto inimigo ou AI, voa para o Exterior (borda do mapa)
        nomeDestinoIA = "Exterior";
        Vector2 direcaoExt = Random.insideUnitCircle.normalized * 5000f; // Bem longe
        alvoGPSVoo = new Vector3(transform.position.x + direcaoExt.x, altitudeCruzeiroComercial, transform.position.z + direcaoExt.y);
    }

    protected override void Update()
    {
        // Define o destino comercial assim que sai do chão e entra em missão
        if (!destinoCalculado && (estadoAtual == EstadoAviao.EmMissao || estadoAtual == EstadoAviao.Decolando))
        {
            DefinirDestinoComercial();
        }

        // 1. Executa a física e a navegação padrão de ControleAviao
        base.Update();

        // 2. Executa a animação e efeitos visuais/sonoros comerciais de acordo com o estado do avião
        AtualizarEfeitosMotores();
        
        // 3. Remove o avião se chegar ao exterior (para poupar memória)
        if (destinoCalculado && nomeDestinoIA == "Exterior" && estadoAtual == EstadoAviao.EmMissao)
        {
            float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(alvoGPSVoo.x, 0, alvoGPSVoo.z));
            if (dist < 300f)
            {
                Destroy(gameObject);
            }
        }
    }

    protected override void ManobraVooRealista(float multDano = 1f)
    {
        // Redefinimos a lógica de manobra de voo para que seja adequada a uma aeronave comercial de grande porte:
        // - Curvas mais suaves (taxa de giro menor)
        // - Altitude de cruzeiro comercial mais alta
        // - Inércia de voo
        
        float dt = Time.deltaTime;
        Vector3 retaAteAlvo = alvoGPSVoo - transform.position;
        float anguloPressaoLateralY = 0f;

        // Sobrescreve a altitude desejada do alvo para a altitude de cruzeiro comercial quando em missão/voo
        if (estadoAtual == EstadoAviao.EmMissao && alvoGPSVoo.y < altitudeCruzeiroComercial)
        {
            alvoGPSVoo.y = altitudeCruzeiroComercial;
            retaAteAlvo = alvoGPSVoo - transform.position;
        }

        if (retaAteAlvo.sqrMagnitude > 0.1f)
        {
            Vector3 upRef = Mathf.Abs(Vector3.Dot(retaAteAlvo.normalized, Vector3.up)) > 0.99f ? transform.up : Vector3.up;
            Quaternion olharMundoDesejado = Quaternion.LookRotation(retaAteAlvo, upRef);
            anguloPressaoLateralY = Vector3.SignedAngle(transform.forward, retaAteAlvo, Vector3.up);

            // Aviões comerciais fazem curvas mais suaves (taxa de giro reduzida em relação a caças)
            float taxaGiroSuave = taxaDeGiroLeme * 0.45f;
            transform.rotation = Quaternion.Slerp(transform.rotation, olharMundoDesejado, (taxaGiroSuave / 15f) * dt);
        }

        float multiplicadorPatrulha = 1f;
        if (estadoAtual == EstadoAviao.EmMissao)
        {
            // Voo de cruzeiro estável, sem oscilações bruscas
            multiplicadorPatrulha = 0.85f;
        }
        else if (estadoAtual == EstadoAviao.Pousando)
        {
            if (aeroportoOrigem != null && aeroportoOrigem.waypointsDecida != null && aeroportoOrigem.waypointsDecida.Count > 1)
            {
                float distToTouchdown = Vector3.Distance(transform.position, aeroportoOrigem.waypointsDecida[1].position);
                if (distToTouchdown > 200f)
                    multiplicadorPatrulha = 0.9f; 
                else
                    multiplicadorPatrulha = Mathf.Lerp(multiplicadorPatrulha, 0.35f, Time.deltaTime * 1.5f); // Descida suave
            }
            else
            {
                multiplicadorPatrulha = 0.4f;
            }
        }

        // Simula inércia na velocidade final
        float velAlvo = (velocidadeMaximaVoo * multiplicadorVelocidadeTurbo * multiplicadorPatrulha) * multDano;
        Vector3 novaPos = transform.position + transform.forward * (velAlvo * dt);

        // Limite de segurança de voo mínimo (Y de voo comercial mínimo)
        if (novaPos.y < 25f)
        {
            novaPos.y = 25f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, transform.eulerAngles.y, 0), 20f * dt);
        }

        // Proteção de borda do mapa
        if (Mathf.Abs(novaPos.x) > 10000f || Mathf.Abs(novaPos.z) > 10000f)
        {
            Vector3 centroDoMap = new Vector3(0, novaPos.y, 0);
            alvoGPSVoo = centroDoMap;
            Quaternion freioDeOuro = Quaternion.LookRotation((centroDoMap - transform.position).normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, freioDeOuro, 50f * dt);
            novaPos = transform.position + transform.forward * (velocidadeMaximaVoo * 0.5f * dt);
        }

        transform.position = novaPos;

        // Animação de Banking (inclinação de asas) e Pitch (arfagem) comerciais muito mais suaves
        if (modeloMecanicoVisual != null)
        {
            float inclinacaoAlvoZ = Mathf.Clamp(anguloPressaoLateralY * -1.8f, -asaBankingComercial, asaBankingComercial);
            float inclinacaoAlvoX = Mathf.Clamp(retaAteAlvo.y * -2.0f, -arfagemPitchComercial, arfagemPitchComercial);
            giroLateralRoll = Mathf.Lerp(giroLateralRoll, inclinacaoAlvoZ, dt * 2.5f); // Transições mais amortecidas
            empinadaPitch = Mathf.Lerp(empinadaPitch, inclinacaoAlvoX, dt * 2.5f);
            modeloMecanicoVisual.localRotation = Quaternion.Euler(empinadaPitch, 0f, giroLateralRoll);
        }
    }

    private void AtualizarEfeitosMotores()
    {
        float dt = Time.deltaTime;
        
        // Motores ligam a partir do táxi
        bool motoresLigados = estadoAtual != EstadoAviao.ReservaHangar;

        if (motoresLigados)
        {
            // Determina a velocidade de rotação com base no estado de voo
            float metaGiro = 0f;
            if (estadoAtual == EstadoAviao.Taxiando || estadoAtual == EstadoAviao.ProntoNoPatio || estadoAtual == EstadoAviao.RetornandoPraVaga)
            {
                metaGiro = velocidadeMaxGiroHelice * 0.25f; // Rotação lenta/idle no chão
            }
            else
            {
                metaGiro = velocidadeMaxGiroHelice; // Rotação máxima na decolagem/voo
            }

            velocidadeGiroHeliceAtual = Mathf.Lerp(velocidadeGiroHeliceAtual, metaGiro, dt * inerciaAceleracao);

            // Gira as hélices (se configurado como hélice)
            if (tipoPropulsao == TipoPropulsao.Helice)
            {
                for (int i = 0; i < helices.Count; i++)
                {
                    if (helices[i] != null)
                    {
                        helices[i].Rotate(eixoGiroHelice * (velocidadeGiroHeliceAtual * dt), Space.Self);
                    }
                }
            }

            // Controla partículas e luzes de turbina (se configurado como turbina)
            bool usaTurbina = (tipoPropulsao == TipoPropulsao.Turbina);
            for (int i = 0; i < rastroTurbinas.Count; i++)
            {
                if (rastroTurbinas[i] != null)
                {
                    if (usaTurbina && !rastroTurbinas[i].isPlaying) rastroTurbinas[i].Play();
                    else if (!usaTurbina && rastroTurbinas[i].isPlaying) rastroTurbinas[i].Stop();
                }
            }
            for (int i = 0; i < luzesTurbina.Count; i++)
            {
                if (luzesTurbina[i] != null)
                {
                    luzesTurbina[i].enabled = usaTurbina;
                }
            }

            // Som do motor com volume e pitch proporcionais à aceleração
            if (somMotorComercial != null)
            {
                if (!somMotorComercial.isPlaying) somMotorComercial.Play();
                
                float pctVel = velocidadeGiroHeliceAtual / velocidadeMaxGiroHelice;
                somMotorComercial.volume = Mathf.Lerp(0.3f, 1.0f, pctVel);
                somMotorComercial.pitch = Mathf.Lerp(0.7f, 1.3f, pctVel);
            }
        }
        else
        {
            // Motores desligados: desacelera e desliga efeitos
            velocidadeGiroHeliceAtual = Mathf.Lerp(velocidadeGiroHeliceAtual, 0f, dt * inerciaAceleracao * 0.5f);

            if (tipoPropulsao == TipoPropulsao.Helice)
            {
                for (int i = 0; i < helices.Count; i++)
                {
                    if (helices[i] != null && velocidadeGiroHeliceAtual > 0.05f)
                    {
                        helices[i].Rotate(eixoGiroHelice * (velocidadeGiroHeliceAtual * dt), Space.Self);
                    }
                }
            }

            for (int i = 0; i < rastroTurbinas.Count; i++)
            {
                if (rastroTurbinas[i] != null && rastroTurbinas[i].isPlaying) rastroTurbinas[i].Stop();
            }
            for (int i = 0; i < luzesTurbina.Count; i++)
            {
                if (luzesTurbina[i] != null) luzesTurbina[i].enabled = false;
            }

            if (somMotorComercial != null && somMotorComercial.isPlaying)
            {
                somMotorComercial.volume = Mathf.Lerp(somMotorComercial.volume, 0f, dt * 2f);
                if (somMotorComercial.volume <= 0.02f) somMotorComercial.Stop();
            }
        }
    }
}
