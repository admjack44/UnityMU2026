using Microsoft.Extensions.Logging;
using MUServer.Core.Combat;
using MUServer.Core.Models;

namespace MUServer.Core.Services;

/// <summary>
/// Servicio responsable de operaciones básicas de combate.
/// Actualmente soporta combate contra un dummy de entrenamiento.
/// </summary>
public sealed class CombatService
{
    private readonly ILogger _logger;
    private readonly DummyTarget _trainingDummy = new();

    public CombatService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta un ataque contra el dummy de entrenamiento.
    /// Si el dummy estaba muerto, se reinicia automáticamente.
    /// </summary>
    public AttackResult AttackTrainingDummy(Character attacker)
    {
        if (attacker is null)
        {
            return AttackResult.Empty;
        }

        EnsureDummyIsAlive();

        int damage = CalculateBaseDamage(attacker);

        _trainingDummy.CurrentHp = Math.Max(0, _trainingDummy.CurrentHp - damage);

        _logger.LogInformation(
            "Combat => Player:{Player} Target:{Target} Damage:{Damage} RemainingHp:{RemainingHp}",
            attacker.Name,
            _trainingDummy.Name,
            damage,
            _trainingDummy.CurrentHp);

        return new AttackResult(
            Damage: ToByte(damage),
            RemainingHp: ToByte(_trainingDummy.CurrentHp));
    }

    /// <summary>
    /// Expone el estado actual del dummy para debug o tests.
    /// </summary>
    public byte GetDummyCurrentHp()
    {
        return ToByte(_trainingDummy.CurrentHp);
    }

    /// <summary>
    /// Reinicia manualmente el dummy a vida completa.
    /// </summary>
    public void ResetDummy()
    {
        _trainingDummy.CurrentHp = _trainingDummy.MaxHp;

        _logger.LogInformation(
            "Combat => Dummy reset. Target:{Target} Hp:{Hp}",
            _trainingDummy.Name,
            _trainingDummy.CurrentHp);
    }

    private void EnsureDummyIsAlive()
    {
        if (_trainingDummy.IsAlive)
        {
            return;
        }

        _trainingDummy.CurrentHp = _trainingDummy.MaxHp;

        _logger.LogInformation(
            "Combat => Dummy auto-respawn. Target:{Target} Hp:{Hp}",
            _trainingDummy.Name,
            _trainingDummy.CurrentHp);
    }

    private static int CalculateBaseDamage(Character attacker)
    {
        int damage = attacker.Strength + (attacker.Agility / 4);
        return Math.Max(1, damage);
    }

    private static byte ToByte(int value)
    {
        return (byte)Math.Clamp(value, 0, 255);
    }

    public readonly record struct AttackResult(byte Damage, byte RemainingHp)
    {
        public static AttackResult Empty => new(0, 0);
    }
}