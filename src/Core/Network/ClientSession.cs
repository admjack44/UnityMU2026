using MUServer.Core.Models;

namespace MUServer.Core.Network;

public sealed class ClientSession
{
    public string AccountName { get; set; } = string.Empty;

    public bool IsAuthenticated { get; set; }

    public Character? SelectedCharacter { get; set; }

    public int? PlayerId { get; set; }
}