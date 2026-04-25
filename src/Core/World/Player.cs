using System.Net.Sockets;
using MUServer.Core.Models;

namespace MUServer.Core.World;

/// <summary>
/// Representa una instancia activa de un jugador dentro del mundo.
/// Contiene estado runtime (posición, conexión, combate).
/// </summary>
public sealed class Player
{
    // =========================
    // 🆔 IDENTIDAD
    // =========================
    public int PlayerId { get; init; }

    public string AccountName { get; init; } = string.Empty;

    public Character Character { get; init; } = new();

    // =========================
    // 🌍 ESTADO EN EL MUNDO
    // =========================
    public byte CurrentMapId { get; private set; }

    public byte X { get; private set; }

    public byte Y { get; private set; }

    public byte Direction { get; private set; }

    public bool IsOnline { get; private set; } = true;

    // =========================
    // 🔌 CONEXIÓN
    // =========================
    public NetworkStream? Stream { get; set; }

    // =========================
    // ⚔ COMBATE
    // =========================
    public DateTime LastAttackTimeUtc { get; set; } = DateTime.MinValue;

    // =========================
    // 🧠 ESTADO DERIVADO
    // =========================
    public bool IsAlive => Character is not null && Character.CurrentLife > 0;
    public bool IsDead => !IsAlive;

    // =========================
    // 🚶 MOVIMIENTO
    // =========================
    public void SetPosition(byte mapId, byte x, byte y, byte direction = 0)
    {
        CurrentMapId = mapId;
        X = x;
        Y = y;
        Direction = direction;
    }

    public void MoveTo(byte x, byte y)
    {
        X = x;
        Y = y;
    }

    // =========================
    // ⚔ COMBATE UTILIDADES
    // =========================
    public bool CanAttack(int cooldownMs)
    {
        var now = DateTime.UtcNow;

        if ((now - LastAttackTimeUtc).TotalMilliseconds < cooldownMs)
        {
            return false;
        }

        LastAttackTimeUtc = now;
        return true;
    }

    // =========================
    // 💀 VIDA / MUERTE
    // =========================
    public void Kill()
    {
        Character.CurrentLife = 0;
    }

    public void Revive(byte mapId, byte x, byte y)
    {
        Character.CurrentLife = Character.MaxLife;
        SetPosition(mapId, x, y);
    }

    // =========================
    // 🔌 CONEXIÓN
    // =========================
    public void Disconnect()
    {
        IsOnline = false;
        Stream = null;
    }
}