using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapaTerritorial", menuName = "Hegemonia/Mapa Territorial")]
public sealed class DadosMapaTerritorial : ScriptableObject
{
    public const string NomeResource = "MapaTerritorialInicial";
    [SerializeField] private Texture2D imagemReferencia;
    [SerializeField] private List<RegiaoPolitica> regioes = new List<RegiaoPolitica>();
    [SerializeField, Min(8)] private int colunasIndice = 64;
    [SerializeField, Min(8)] private int linhasIndice = 36;

    [NonSerialized] private Dictionary<int, List<int>> indiceEspacial;
    [NonSerialized] private bool indiceSujo = true;

    public Texture2D ImagemReferencia { get { return imagemReferencia; } set { imagemReferencia = value; } }
    public List<RegiaoPolitica> Regioes { get { if (regioes == null) regioes = new List<RegiaoPolitica>(); return regioes; } }

    public RegiaoPolitica EncontrarRegiao(string territorioId)
    {
        if (string.IsNullOrEmpty(territorioId)) return null;
        for (int i = 0; i < Regioes.Count; i++)
            if (Regioes[i] != null && Regioes[i].territorioId == territorioId) return Regioes[i];
        return null;
    }

    public ResultadoConsultaTerritorio ConsultarUv(Vector2 uv)
    {
        GarantirIndice();
        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);
        int x = Mathf.Min(colunasIndice - 1, Mathf.FloorToInt(uv.x * colunasIndice));
        int y = Mathf.Min(linhasIndice - 1, Mathf.FloorToInt(uv.y * linhasIndice));
        int chave = y * colunasIndice + x;
        if (!indiceEspacial.TryGetValue(chave, out List<int> candidatos)) return ResultadoConsultaTerritorio.NaoDefinido;

        int melhorIndice = -1;
        int melhorRank = int.MinValue;
        float menorArea = float.MaxValue;
        for (int i = 0; i < candidatos.Count; i++)
        {
            int indice = candidatos[i];
            RegiaoPolitica regiao = Regioes[indice];
            if (regiao == null || !regiao.PossuiPoligono || !PontoDentroOuNaBorda(uv, regiao.vertices)) continue;

            // Em áreas desenhadas sobrepostas, a terra é mais específica que a faixa marítima.
            int rank = regiao.tipo == TipoRegiaoPolitica.Terra ? 2 : 1;
            float area = CalcularAreaCaixa(regiao.vertices);
            if (rank > melhorRank || (rank == melhorRank && area < menorArea))
            {
                melhorIndice = indice;
                melhorRank = rank;
                menorArea = area;
            }
        }

        if (melhorIndice < 0) return ResultadoConsultaTerritorio.NaoDefinido;
        RegiaoPolitica escolhida = Regioes[melhorIndice];
        return new ResultadoConsultaTerritorio
        {
            encontrouRegiao = true,
            territorioId = escolhida.territorioId,
            ownerCountryTeamId = escolhida.ownerCountryTeamId,
            neutral = escolhida.neutral,
            capturable = escolhida.capturable,
            tipo = escolhida.tipo
        };
    }

    public void InvalidarIndice()
    {
        indiceSujo = true;
        indiceEspacial = null;
    }

    private void GarantirIndice()
    {
        if (!indiceSujo && indiceEspacial != null) return;
        colunasIndice = Mathf.Max(8, colunasIndice);
        linhasIndice = Mathf.Max(8, linhasIndice);
        indiceEspacial = new Dictionary<int, List<int>>();
        for (int i = 0; i < Regioes.Count; i++)
        {
            RegiaoPolitica regiao = Regioes[i];
            if (regiao == null || !regiao.PossuiPoligono) continue;
            Vector2 min = regiao.vertices[0];
            Vector2 max = min;
            for (int v = 1; v < regiao.vertices.Count; v++)
            {
                min = Vector2.Min(min, regiao.vertices[v]);
                max = Vector2.Max(max, regiao.vertices[v]);
            }
            int x0 = Mathf.Clamp(Mathf.FloorToInt(min.x * colunasIndice), 0, colunasIndice - 1);
            int x1 = Mathf.Clamp(Mathf.FloorToInt(max.x * colunasIndice), 0, colunasIndice - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(min.y * linhasIndice), 0, linhasIndice - 1);
            int y1 = Mathf.Clamp(Mathf.FloorToInt(max.y * linhasIndice), 0, linhasIndice - 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int chave = y * colunasIndice + x;
                if (!indiceEspacial.TryGetValue(chave, out List<int> lista))
                    indiceEspacial.Add(chave, lista = new List<int>(2));
                lista.Add(i);
            }
        }
        indiceSujo = false;
    }

    public static bool PontoDentroOuNaBorda(Vector2 ponto, IList<Vector2> poligono)
    {
        if (poligono == null || poligono.Count < 3) return false;
        bool dentro = false;
        for (int i = 0, j = poligono.Count - 1; i < poligono.Count; j = i++)
        {
            Vector2 a = poligono[j];
            Vector2 b = poligono[i];
            if (DistanciaSegmentoSqr(ponto, a, b) <= 0.000001f) return true;
            bool cruza = (a.y > ponto.y) != (b.y > ponto.y)
                && ponto.x < (b.x - a.x) * (ponto.y - a.y) / (b.y - a.y) + a.x;
            if (cruza) dentro = !dentro;
        }
        return dentro;
    }

    private static float DistanciaSegmentoSqr(Vector2 ponto, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float comprimento = ab.sqrMagnitude;
        if (comprimento <= Mathf.Epsilon) return (ponto - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(ponto - a, ab) / comprimento);
        return (ponto - (a + ab * t)).sqrMagnitude;
    }

    private static float CalcularAreaCaixa(IList<Vector2> vertices)
    {
        Vector2 min = vertices[0];
        Vector2 max = min;
        for (int i = 1; i < vertices.Count; i++) { min = Vector2.Min(min, vertices[i]); max = Vector2.Max(max, vertices[i]); }
        return Mathf.Max(0.000001f, (max.x - min.x) * (max.y - min.y));
    }

    /// <summary>
    /// Semeia regiões distintas a partir dos contornos da imagem fase.png.
    /// Cores não definem país: as cinco associações ficam em -1 para edição explícita.
    /// </summary>
    public static DadosMapaTerritorial CriarModeloFase(Texture2D imagem)
    {
        DadosMapaTerritorial mapa = CreateInstance<DadosMapaTerritorial>();
        mapa.imagemReferencia = imagem;
        mapa.colunasIndice = 64;
        mapa.linhasIndice = 36;
        mapa.regioes = new List<RegiaoPolitica>();

        Color[] cores =
        {
            new Color(0.94f, 0.55f, 0.18f, 0.95f), new Color(0.25f, 0.72f, 0.86f, 0.95f),
            new Color(0.82f, 0.35f, 0.38f, 0.95f), new Color(0.66f, 0.55f, 0.88f, 0.95f),
            new Color(0.42f, 0.78f, 0.45f, 0.95f), new Color(0.91f, 0.77f, 0.23f, 0.95f)
        };
        mapa.regioes.Add(NovaRegiao("terra-noroeste-oeste", "Noroeste oeste", TipoRegiaoPolitica.Terra, cores[0],
            P(60, 61), P(72, 47), P(105, 39), P(150, 39), P(184, 43), P(210, 50), P(225, 64), P(233, 77), P(226, 87), P(230, 98), P(251, 119), P(277, 127), P(254, 140), P(233, 148), P(208, 145), P(187, 139), P(163, 139), P(140, 143), P(112, 151), P(83, 151), P(67, 141), P(65, 127), P(78, 112), P(89, 99), P(87, 89), P(72, 82), P(63, 73)));
        mapa.regioes.Add(NovaRegiao("terra-noroeste-leste", "Noroeste leste", TipoRegiaoPolitica.Terra, cores[1],
            P(207, 20), P(257, 13), P(296, 14), P(329, 4), P(365, 3), P(385, 12), P(414, 20), P(455, 20), P(477, 31), P(479, 42), P(462, 57), P(438, 70), P(416, 77), P(384, 75), P(361, 76), P(360, 91), P(360, 108), P(349, 119), P(333, 131), P(312, 137), P(288, 136), P(271, 130), P(252, 120), P(238, 110), P(232, 96), P(221, 88), P(208, 75), P(205, 62), P(219, 51), P(208, 37)));
        mapa.regioes.Add(NovaRegiao("terra-nordeste-norte", "Nordeste norte", TipoRegiaoPolitica.Terra, cores[2],
            P(884, 113), P(900, 110), P(914, 97), P(923, 75), P(939, 55), P(958, 47), P(980, 48), P(998, 56), P(1015, 58), P(1037, 43), P(1062, 39), P(1088, 41), P(1110, 50), P(1138, 60), P(1141, 257), P(1116, 267), P(1083, 267), P(1053, 260), P(1023, 259), P(1005, 253), P(1002, 227), P(994, 206), P(991, 187), P(982, 173), P(965, 163), P(943, 155), P(920, 147), P(900, 135), P(888, 124)));
        mapa.regioes.Add(NovaRegiao("terra-nordeste-sul", "Nordeste sul", TipoRegiaoPolitica.Terra, cores[3],
            P(1004, 254), P(1030, 261), P(1060, 270), P(1095, 271), P(1139, 266), P(1138, 325), P(1137, 350), P(1128, 363), P(1128, 397), P(1131, 424), P(1126, 449), P(1114, 465), P(1098, 471), P(1086, 460), P(1093, 440), P(1098, 413), P(1091, 389), P(1080, 373), P(1067, 362), P(1055, 346), P(1045, 332), P(1047, 304), P(1031, 293), P(1013, 286)));
        mapa.regioes.Add(NovaRegiao("terra-sudoeste", "Sudoeste", TipoRegiaoPolitica.Terra, cores[4],
            P(102, 513), P(97, 501), P(104, 485), P(121, 474), P(153, 464), P(188, 448), P(227, 433), P(270, 421), P(319, 414), P(360, 410), P(389, 418), P(421, 425), P(439, 416), P(455, 395), P(475, 379), P(491, 374), P(503, 380), P(506, 394), P(506, 415), P(497, 437), P(498, 464), P(511, 480), P(531, 495), P(526, 511), P(505, 523), P(489, 541), P(467, 560), P(446, 579), P(422, 594), P(397, 602), P(364, 602), P(324, 601), P(292, 605), P(263, 614), P(239, 606), P(225, 590), P(208, 579), P(183, 578), P(160, 584), P(139, 581), P(120, 566), P(112, 543)));
        mapa.regioes.Add(NovaRegiao("terra-sudeste", "Sudeste", TipoRegiaoPolitica.Terra, cores[5],
            P(946, 504), P(961, 497), P(981, 495), P(1004, 497), P(1026, 494), P(1049, 494), P(1049, 522), P(1055, 537), P(1033, 541), P(1021, 553), P(1035, 566), P(1041, 579), P(1036, 588), P(1019, 587), P(1004, 585), P(995, 578), P(982, 568), P(967, 568), P(955, 561), P(950, 546), P(947, 529)));
        mapa.regioes.Add(NovaRegiao("ilha-central", "Ilha central neutra", TipoRegiaoPolitica.Terra, new Color(0.85f, 0.85f, 0.85f, 0.95f),
            P(640, 263), P(650, 259), P(677, 257), P(697, 249), P(719, 248), P(738, 244), P(728, 257), P(727, 271), P(712, 276), P(727, 286), P(738, 301), P(724, 302), P(701, 295), P(681, 291), P(674, 300), P(646, 303), P(645, 285)));
        RegiaoPolitica ilha = mapa.regioes[mapa.regioes.Count - 1];
        ilha.ownerCountryTeamId = 0;
        ilha.neutral = true;
        ilha.capturable = true;

        Color corMar = new Color(1f, 0.40f, 0.68f, 0.9f);
        mapa.regioes.Add(NovaRegiao("aguas-noroeste-oeste", "Águas noroeste oeste", TipoRegiaoPolitica.AguasTerritoriais, corMar,
            P(36, 39), P(54, 31), P(72, 19), P(103, 13), P(169, 13), P(204, 15), P(214, 22), P(205, 34), P(207, 52), P(218, 70), P(238, 90), P(266, 105), P(307, 125), P(311, 136), P(296, 151), P(267, 160), P(248, 174), P(219, 178), P(45, 177), P(42, 155), P(43, 129), P(49, 107), P(44, 88), P(35, 66)));
        mapa.regioes.Add(NovaRegiao("aguas-noroeste-leste", "Águas noroeste leste", TipoRegiaoPolitica.AguasTerritoriais, corMar,
            P(202, 20), P(231, 16), P(267, 13), P(301, 12), P(328, 6), P(369, 0), P(482, 0), P(501, 11), P(512, 25), P(512, 47), P(514, 66), P(503, 83), P(480, 91), P(457, 101), P(435, 114), P(413, 126), P(390, 137), P(365, 137), P(337, 135), P(316, 130), P(298, 123), P(272, 110), P(246, 95), P(226, 79), P(211, 64), P(202, 45)));
        mapa.regioes.Add(NovaRegiao("aguas-nordeste", "Águas nordeste", TipoRegiaoPolitica.AguasTerritoriais, corMar,
            P(853, 54), P(858, 38), P(876, 25), P(902, 17), P(931, 10), P(971, 8), P(1054, 9), P(1086, 9), P(1115, 17), P(1142, 28), P(1152, 44), P(1152, 448), P(1142, 469), P(1123, 483), P(1091, 486), P(1067, 479), P(1049, 465), P(1061, 450), P(1076, 439), P(1082, 422), P(1084, 402), P(1078, 386), P(1069, 374), P(1059, 363), P(1048, 351), P(1044, 334), P(1046, 311), P(1041, 288), P(1028, 272), P(1008, 259), P(987, 248), P(973, 228), P(972, 208), P(965, 189), P(951, 174), P(931, 160), P(906, 151), P(885, 138), P(870, 121), P(856, 99)));
        mapa.regioes.Add(NovaRegiao("aguas-sudoeste", "Águas sudoeste", TipoRegiaoPolitica.AguasTerritoriais, corMar,
            P(80, 492), P(88, 469), P(110, 453), P(140, 443), P(167, 432), P(205, 418), P(246, 407), P(280, 399), P(335, 396), P(383, 389), P(420, 376), P(450, 373), P(474, 375), P(497, 389), P(512, 400), P(531, 396), P(545, 403), P(554, 415), P(555, 438), P(544, 464), P(539, 487), P(542, 502), P(564, 518), P(554, 538), P(530, 555), P(513, 574), P(489, 587), P(477, 606), P(454, 614), P(408, 615), P(356, 617), P(291, 616), P(271, 607), P(254, 603), P(236, 612), P(216, 616), P(196, 609), P(177, 601), P(158, 603), P(137, 602), P(117, 590), P(101, 573), P(91, 550), P(82, 526)));
        mapa.regioes.Add(NovaRegiao("aguas-sudeste", "Águas sudeste", TipoRegiaoPolitica.AguasTerritoriais, corMar,
            P(925, 458), P(950, 460), P(975, 460), P(1004, 460), P(1037, 460), P(1059, 466), P(1074, 480), P(1070, 506), P(1064, 522), P(1063, 542), P(1072, 559), P(1075, 580), P(1073, 600), P(1065, 611), P(1046, 622), P(1026, 629), P(1000, 628), P(977, 625), P(952, 619), P(928, 611), P(907, 600), P(891, 584), P(884, 563), P(884, 541), P(891, 519), P(902, 498), P(921, 480)));

        mapa.InvalidarIndice();
        return mapa;
    }

    private static RegiaoPolitica NovaRegiao(string id, string nome, TipoRegiaoPolitica tipo, Color cor, params Vector2[] vertices)
    {
        RegiaoPolitica regiao = new RegiaoPolitica
        {
            territorioId = id,
            nome = nome,
            ownerCountryTeamId = -1,
            neutral = false,
            capturable = false,
            tipo = tipo,
            corMapa = cor,
            vertices = new List<Vector2>(vertices)
        };
        return regiao;
    }

    private static Vector2 P(float x, float y)
    {
        return new Vector2(Mathf.Clamp01(x / 1152f), Mathf.Clamp01(y / 648f));
    }
}
