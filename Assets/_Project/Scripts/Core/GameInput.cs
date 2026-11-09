using UnityEngine.InputSystem;

namespace TSAGame
{
    /// <summary>
    /// Shared input actions for keyboard/mouse and gamepad, defined in code so no asset wiring is needed.
    /// </summary>
    public static class GameInput
    {
        public static readonly InputAction Move;
        public static readonly InputAction LookMouse;
        public static readonly InputAction LookStick;
        public static readonly InputAction Interact;
        public static readonly InputAction Jump;
        public static readonly InputAction Sprint;
        public static readonly InputAction Skip;
        public static readonly InputAction Choice1;
        public static readonly InputAction Choice2;
        public static readonly InputAction Choice3;

        static GameInput()
        {
            Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            LookMouse = new InputAction("LookMouse", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            LookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType: "Vector2");

            Interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonWest");

            Jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");

            Sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Skip = new InputAction("Skip", InputActionType.Button, "<Keyboard>/escape");
            Skip.AddBinding("<Gamepad>/start");

            Choice1 = new InputAction("Choice1", InputActionType.Button, "<Keyboard>/1");
            Choice2 = new InputAction("Choice2", InputActionType.Button, "<Keyboard>/2");
            Choice3 = new InputAction("Choice3", InputActionType.Button, "<Keyboard>/3");

            foreach (var action in new[] { Move, LookMouse, LookStick, Interact, Jump, Sprint, Skip, Choice1, Choice2, Choice3 })
                action.Enable();
        }
    }
}
