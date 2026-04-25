using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private MUClient client;
    [SerializeField] private float unityScale = 10f;

    private byte serverX;
    private byte serverY;

    public void SetPosition(byte x, byte y)
    {
        serverX = x;
        serverY = y;

        ApplyPosition();

        Debug.Log($"Player posicionado -> Server:{x},{y}");
    }

    private void Update()
    {
        if (client == null)
            return;

        if (Input.GetKeyDown(KeyCode.W))
            RequestMove(0, 1);

        if (Input.GetKeyDown(KeyCode.S))
            RequestMove(0, -1);

        if (Input.GetKeyDown(KeyCode.A))
            RequestMove(-1, 0);

        if (Input.GetKeyDown(KeyCode.D))
            RequestMove(1, 0);
    }

    private void RequestMove(int dx, int dy)
    {
        int nextX = Mathf.Clamp(serverX + dx, 0, 255);
        int nextY = Mathf.Clamp(serverY + dy, 0, 255);

        Debug.Log($"Move enviado -> X:{nextX} Y:{nextY}");

        client.SendMove((byte)nextX, (byte)nextY);
    }

    // 🔥 ESTE MÉTODO LO LLAMA EL SERVER RESPONSE
    public void ConfirmMove(byte x, byte y)
    {
        serverX = x;
        serverY = y;

        ApplyPosition();

        Debug.Log($"Move confirmado -> X:{x} Y:{y}");
    }

    private void ApplyPosition()
    {
        transform.position = new Vector3(
            serverX / unityScale,
            0.5f,
            serverY / unityScale
        );
    }
}