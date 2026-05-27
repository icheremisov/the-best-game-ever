using UnityEngine;
using UnityEngine.InputSystem;

namespace Mimic.Input
{
    public class InputBridge : MonoBehaviour
    {
        public InputActionReference RotateCWAction;  // E or wheel down
        public InputActionReference RotateCCWAction; // Q or wheel up

        private void OnEnable()
        {
            if (RotateCWAction != null) RotateCWAction.action.performed += OnRotateCW;
            if (RotateCCWAction != null) RotateCCWAction.action.performed += OnRotateCCW;
            RotateCWAction?.action.Enable();
            RotateCCWAction?.action.Enable();
        }

        private void OnDisable()
        {
            if (RotateCWAction != null) RotateCWAction.action.performed -= OnRotateCW;
            if (RotateCCWAction != null) RotateCCWAction.action.performed -= OnRotateCCW;
        }

        private void OnRotateCW(InputAction.CallbackContext _)
        {
            var held = DragController.Instance?.Held;
            if (held != null) held.Rotate(clockwise: true);
        }

        private void OnRotateCCW(InputAction.CallbackContext _)
        {
            var held = DragController.Instance?.Held;
            if (held != null) held.Rotate(clockwise: false);
        }
    }
}
