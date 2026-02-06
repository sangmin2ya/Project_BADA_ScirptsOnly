using UnityEngine;

public class RigidbodyController : MonoBehaviour
{
    private Rigidbody2D rigidbody2D;
    // Start is called before the first frame update
    void Start()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.W))
        {
            rigidbody2D.AddForce(Vector2.up * Time.deltaTime, ForceMode2D.Impulse);
        }
        if (Input.GetKey(KeyCode.S))
        {
            rigidbody2D.AddForce(Vector2.down * Time.deltaTime, ForceMode2D.Impulse);
        }
        if (Input.GetKey(KeyCode.A))
        {
            rigidbody2D.AddForce(Vector2.left * Time.deltaTime, ForceMode2D.Impulse);
        }
        if (Input.GetKey(KeyCode.D))
        {
            rigidbody2D.AddForce(Vector2.right * Time.deltaTime, ForceMode2D.Impulse);
        }
    }
}
