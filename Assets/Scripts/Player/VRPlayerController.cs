using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class VRPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed in meters per second.")]
    public float moveSpeed = 2.5f;

    [Tooltip("Gravity strength applied to the player.")]
    public float gravity = -9.81f;

    [Header("Mouse Look Settings")]
    [Tooltip("Mouse look sensitivity.")]
    public float mouseSensitivity = 0.1f;

    [Tooltip("Limit how far up/down the camera can rotate (degrees).")]
    public float verticalLookLimit = 80f;

    [Header("References")]
    [Tooltip("Drag your Main Camera here.")]
    public Transform vrCamera;

    private CharacterController characterController;
    private float verticalVelocity = 0f;
    private float cameraPitch = 0f;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (vrCamera == null && Camera.main != null)
        {
            vrCamera = Camera.main.transform;
        }

        // Start with the mouse locked to the center and hidden
        LockCursor();
    }

    void Update()
    {
        HandleCursorLockState();
        HandleMouseLook();
        MovePlayer();
    }

    private void HandleCursorLockState()
    {
        // 1. Press ESC -> Unlock cursor and stop mouse tracking
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        // 2. Click left mouse button -> Re-lock cursor and resume mouse tracking
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    private void HandleMouseLook()
    {
        // Only rotate the camera if mouse tracking is active (Cursor is locked)
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (Mouse.current == null || vrCamera == null) return;

        // Read mouse delta from New Input System
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        // 1. Rotate character body left/right (Yaw)
        transform.Rotate(Vector3.up * mouseDelta.x);

        // 2. Clamp and rotate camera up/down (Pitch)
        cameraPitch -= mouseDelta.y;
        cameraPitch = Mathf.Clamp(cameraPitch, -verticalLookLimit, verticalLookLimit);

        vrCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void MovePlayer()
    {
        Vector2 moveInput = Vector2.zero;

        // Keyboard controls
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
        }

        // Controller Thumbstick support
        if (Gamepad.current != null)
        {
            moveInput += Gamepad.current.leftStick.ReadValue();
        }

        // Calculate direction relative to player body/camera orientation
        Vector3 forward = vrCamera.forward;
        Vector3 right = vrCamera.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x);

        // Gravity check
        if (characterController.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}