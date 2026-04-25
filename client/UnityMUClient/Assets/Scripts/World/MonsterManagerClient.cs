using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client-side visual manager for monsters spawned by the GameServer.
/// The server remains authoritative; this class only renders and updates visuals.
/// </summary>
public sealed class MonsterManagerClient : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private Transform monsterRoot;
    [SerializeField] private float unityScale = 10f;
    [SerializeField] private Vector3 defaultScale = new(0.8f, 1f, 0.8f);

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private readonly Dictionary<byte, MonsterView> monsters = new();

    private void Awake()
    {
        if (monsterRoot == null)
        {
            GameObject root = new("Monsters");
            root.transform.SetParent(transform);
            monsterRoot = root.transform;
        }
    }

    public void SpawnMonster(byte monsterId, byte monsterClass, byte mapId, byte x, byte y)
    {
        Vector3 worldPosition = ToUnityPosition(x, y);

        if (monsters.TryGetValue(monsterId, out MonsterView existing))
        {
            existing.UpdateState(monsterClass, mapId, x, y, worldPosition);
            Log($"Monster actualizado -> Id:{monsterId} Class:{monsterClass} Map:{mapId} Pos:{x},{y}");
            return;
        }

        GameObject monsterObject = CreateMonsterObject(monsterId, monsterClass, worldPosition);

        MonsterView view = monsterObject.GetComponent<MonsterView>();
        if (view == null)
        {
            view = monsterObject.AddComponent<MonsterView>();
        }

        view.Initialize(monsterId, monsterClass, mapId, x, y, worldPosition);
        monsters.Add(monsterId, view);

        Log($"Monster spawn visual -> Id:{monsterId} Class:{monsterClass} Map:{mapId} Pos:{x},{y}");
    }

    public void MoveMonster(byte monsterId, byte x, byte y)
    {
        if (!monsters.TryGetValue(monsterId, out MonsterView monster))
        {
            Log($"MoveMonster ignorado: monster no existe Id:{monsterId}");
            return;
        }

        monster.SetServerPosition(x, y, ToUnityPosition(x, y));
    }

    public void ApplyDamage(byte monsterId, int damage, int remainingHp)
    {
        if (!monsters.TryGetValue(monsterId, out MonsterView monster))
        {
            Log($"ApplyDamage ignorado: monster no existe Id:{monsterId}");
            return;
        }

        monster.ApplyDamage(damage, remainingHp);
        Log($"Monster hit -> Id:{monsterId} Damage:{damage} HP:{remainingHp}");
    }

    public void DespawnMonster(byte monsterId)
    {
        if (!monsters.TryGetValue(monsterId, out MonsterView monster))
        {
            return;
        }

        monsters.Remove(monsterId);

        if (monster != null)
        {
            Destroy(monster.gameObject);
        }

        Log($"Monster despawn visual -> Id:{monsterId}");
    }

    public void ClearAll()
    {
        foreach (MonsterView monster in monsters.Values)
        {
            if (monster != null)
            {
                Destroy(monster.gameObject);
            }
        }

        monsters.Clear();
        Log("Todos los monstruos visuales fueron limpiados.");
    }

    public bool TryGetMonster(byte monsterId, out MonsterView monster)
    {
        return monsters.TryGetValue(monsterId, out monster);
    }

    public bool HasMonster(byte monsterId)
    {
        return monsters.ContainsKey(monsterId);
    }

    private GameObject CreateMonsterObject(byte monsterId, byte monsterClass, Vector3 position)
    {
        GameObject monsterObject;

        if (monsterPrefab != null)
        {
            monsterObject = Instantiate(monsterPrefab, position, Quaternion.identity, monsterRoot);
        }
        else
        {
            monsterObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monsterObject.transform.SetParent(monsterRoot);
            monsterObject.transform.position = position;
            monsterObject.transform.localScale = defaultScale;
        }

        monsterObject.name = $"Monster_{monsterId}_Class_{monsterClass}";
        return monsterObject;
    }

    private Vector3 ToUnityPosition(byte x, byte y)
    {
        return new Vector3(x / unityScale, 0.5f, y / unityScale);
    }

    private void Log(string message)
    {
        if (verboseLogs)
        {
            Debug.Log(message);
        }
    }
}

/// <summary>
/// Lightweight visual state component for a monster instance.
/// It stores server identity and position separately from Unity transform.
/// </summary>
public sealed class MonsterView : MonoBehaviour
{
    public byte MonsterId { get; private set; }
    public byte MonsterClass { get; private set; }
    public byte MapId { get; private set; }
    public byte ServerX { get; private set; }
    public byte ServerY { get; private set; }
    public int CurrentHp { get; private set; }

    [SerializeField] private float moveSmoothness = 12f;

    private Vector3 targetPosition;

    public void Initialize(byte monsterId, byte monsterClass, byte mapId, byte x, byte y, Vector3 worldPosition)
    {
        MonsterId = monsterId;
        MonsterClass = monsterClass;
        MapId = mapId;
        ServerX = x;
        ServerY = y;
        targetPosition = worldPosition;
        transform.position = worldPosition;
    }

    public void UpdateState(byte monsterClass, byte mapId, byte x, byte y, Vector3 worldPosition)
    {
        MonsterClass = monsterClass;
        MapId = mapId;
        SetServerPosition(x, y, worldPosition);
    }

    public void SetServerPosition(byte x, byte y, Vector3 worldPosition)
    {
        ServerX = x;
        ServerY = y;
        targetPosition = worldPosition;
    }

    public void ApplyDamage(int damage, int remainingHp)
    {
        CurrentHp = remainingHp;
        // Visual damage feedback can be added here later: floating text, flash, hit animation.
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * moveSmoothness
        );
    }
}
