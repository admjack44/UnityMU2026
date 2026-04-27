using System.Text;
using System.Text.Json;
using MUServer.Core.Auth;
using MUServer.Core.Network;

namespace MUServer.GameServer.Network;

public sealed class MobileGamePacketDispatcher
{
    private readonly MUServer.Core.Auth.AuthService _authService;

    public MobileGamePacketDispatcher(MUServer.Core.Auth.AuthService authService)
    {
        _authService = authService;
    }

    public async Task<byte[]> DispatchAsync(MobilePacket packet, CancellationToken cancellationToken = default)
    {
        return packet.OpCode switch
        {
            MobileOpCode.LoginRequest => await HandleLoginAsync(packet, cancellationToken),

            _ => MobilePacketJson.Create(
                MobileOpCode.Error,
                LoginResponseDto.Fail("UNKNOWN_OPCODE", $"OpCode no manejado: {packet.OpCode}")
            )
        };
    }

    private async Task<byte[]> HandleLoginAsync(MobilePacket packet, CancellationToken cancellationToken)
    {
        try
        {
            string json = Encoding.UTF8.GetString(packet.Body);

            LoginRequestDto? request = JsonSerializer.Deserialize<LoginRequestDto>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            if (request is null)
            {
                return MobilePacketJson.Create(
                    MobileOpCode.LoginResponse,
                    LoginResponseDto.Fail("INVALID_JSON", "LoginRequest inválido.")
                );
            }

            LoginResponseDto response = await _authService.LoginAsync(request, cancellationToken);

            return MobilePacketJson.Create(MobileOpCode.LoginResponse, response);
        }
        catch (Exception ex)
        {
            return MobilePacketJson.Create(
                MobileOpCode.LoginResponse,
                LoginResponseDto.Fail("LOGIN_EXCEPTION", ex.Message)
            );
        }
    }
}