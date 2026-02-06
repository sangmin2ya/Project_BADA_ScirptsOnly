using UnityEngine;
using Mirror;

public class GameStart : NetworkBehaviour
{
    [SerializeField] DummyShipController _dummyShipController;
    [SerializeField] private OceanType _oceanType;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isServer) return;

        if (other.CompareTag("Player"))
        {
            _dummyShipController.StartGameSession(_oceanType);
        }
    }
}