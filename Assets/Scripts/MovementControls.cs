using UnityEngine;
using UnityEngine.InputSystem;

public class MovementControls : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    public float moveSpeed = 10f;
    public InputActionReference lookAction;

    float xRotation = 0f;
    float yRotation = 0f;
    float rotationClamp = 30f;

    void Start()
    {
        // Lock the cursor to the center of the screen
        Cursor.lockState = CursorLockMode.Locked;
    }

void Update()
    {

        Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
        float lookX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float lookY = lookInput.y * mouseSensitivity * Time.deltaTime;

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -rotationClamp, rotationClamp);
        yRotation += lookX;

        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDirection = transform.forward * vertical + transform.right * horizontal;
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

    }
}
