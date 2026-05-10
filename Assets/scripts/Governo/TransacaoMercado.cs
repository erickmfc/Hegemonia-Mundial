using System;

[Serializable]
public class TransacaoMercado
{
    public int compradorTeamId;
    public int vendedorTeamId;
    public string itemId;
    public int quantidade;
    public int precoUnitario;
    public int total;
    public bool compraDoJogador;
    public string mensagem;
}
