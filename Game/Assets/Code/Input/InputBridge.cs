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
            // Route through DragController so the pickOffset is remapped — otherwise
            // the shape would rotate around its bottom-left pivot, not the grabbed cell.
            DragController.Instance?.RotateHeld(clockwise: true);
        }

        private void OnRotateCCW(InputAction.CallbackContext _)
        {
            DragController.Instance?.RotateHeld(clockwise: false);
        }
    }
}
