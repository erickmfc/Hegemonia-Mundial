using System;

[Serializable]
public class TransacaoMercado
{
    public string id;
    public int compradorTeamId;
    public int vendedorTeamId;
    public string itemId;
    public int quantidade;
    public int precoUnitario;
    public int total;
    public int frete;
    public string status;
    public bool compraDoJogador;
    public string mensagem;
}
