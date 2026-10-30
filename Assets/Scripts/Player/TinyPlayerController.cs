using UnityEngine;
using UnityEngine.InputSystem;

namespace HouseCreators
{
    // Состояния из машины состояний (ЛР1). Interaction / UseItem добавятся вместе с системой взаимодействия.
    public enum PlayerState { Stand, Walk, Running, Jumping, Crouch }

    // Простой контроллер от первого лица для уменьшенного учёного.
    // WASD — ходьба, Ctrl — бег, Space — прыжок, Shift — присед, Esc — освободить курсор.
    [RequireComponent(typeof(CharacterController))]
    public class TinyPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 6f;
        public float runSpeed = 10f;
        public float crouchSpeed = 3f;
        public float jumpHeight = 2.2f;
        public float gravity = -25f;
        public float airAcceleration = 20f;

        [Header("Body")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.0f;
        public float crouchLerpSpeed = 10f;

        [Header("Camera")]
        public Transform cameraPivot;
        public float mouseSensitivity = 0.1f;
        public float standEyeHeight = 1.6f;
        public float crouchEyeHeight = 0.85f;

        [Header("Respawn")]
        public float killY = -20f;

        public PlayerState State { get; private set; }

        CharacterController controller;
        Vector3 velocity;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float pitch;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            LockCursor(true);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) LockCursor(true);

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            }

            Vector2 input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));

            bool wantsCrouch = keyboard.leftShiftKey.isPressed;
            bool crouching = UpdateCrouch(wantsCrouch);
            bool running = keyboard.leftCtrlKey.isPressed && !crouching;
            bool grounded = controller.isGrounded;

            float speed = crouching ? crouchSpeed : running ? runSpeed : walkSpeed;
            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            wish *= speed;

            if (grounded)
            {
                velocity.x = wish.x;
                velocity.z = wish.z;
                if (velocity.y < 0f) velocity.y = -2f;
                if (keyboard.spaceKey.wasPressedThisFrame && !crouching)
                    velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
            }
            else
            {
                Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), wish, airAcceleration * Time.deltaTime);
                velocity.x = horizontal.x;
                velocity.z = horizontal.z;
            }

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            if (!controller.isGrounded) State = PlayerState.Jumping;
            else if (crouching) State = PlayerState.Crouch;
            else if (input.sqrMagnitude < 0.01f) State = PlayerState.Stand;
            else State = running ? PlayerState.Running : PlayerState.Walk;

            if (cameraPivot != null)
            {
                float eye = Mathf.Lerp(crouchEyeHeight, standEyeHeight, Mathf.InverseLerp(crouchHeight, standHeight, controller.height));
                cameraPivot.localPosition = new Vector3(0f, eye, 0f);
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (transform.position.y < killY) Respawn();
        }

        // Возвращает true, пока персонаж присевший (в том числе если встать мешает потолок).
        bool UpdateCrouch(bool wantsCrouch)
        {
            float target = wantsCrouch ? crouchHeight : standHeight;
            if (!wantsCrouch && controller.height < standHeight - 0.01f)
            {
                float radius = controller.radius * 0.9f;
                Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
                Vector3 top = transform.position + Vector3.up * (standHeight - radius);
                int mask = ~(1 << gameObject.layer);
                if (Physics.CheckCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore))
                    target = controller.height;
            }

            controller.height = Mathf.MoveTowards(controller.height, target, crouchLerpSpeed * Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            return controller.height < standHeight - 0.05f;
        }

        public void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            velocity = Vector3.zero;
            controller.enabled = true;
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
