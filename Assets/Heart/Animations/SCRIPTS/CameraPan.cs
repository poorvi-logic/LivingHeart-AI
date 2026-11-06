using UnityEngine;

public class CameraPan : MonoBehaviour
{
    public Transform target;
    public float panSpeed = 0.5f;

    void Update()
    {
        if (Input.GetMouseButton(2))   // middle mouse hold
        {
            float moveX = -Input.GetAxis("Mouse X") * panSpeed;
            float moveY = -Input.GetAxis("Mouse Y") * panSpeed;

            transform.Translate(moveX, moveY, 0);
            target.Translate(moveX, moveY, 0);
        }
    }
}