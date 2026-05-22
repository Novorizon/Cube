using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Framework
{
    public sealed class GameInputManager : Singleton<GameInputManager>
    {
        private readonly Stack<InputMode> modeStack = new Stack<InputMode>();

        private GameInputActions controls;
        private InputMode currentMode = InputMode.None;
        private bool initialized;

        public InputMode CurrentMode
        {
            get
            {
                return currentMode;
            }
        }

        public Vector2 GameplayMove
        {
            get
            {
                if (!initialized)
                {
                    return Vector2.zero;
                }

                return controls.Gameplay.Move.ReadValue<Vector2>();
            }
        }

        public Vector2 GameplayLook
        {
            get
            {
                if (!initialized)
                {
                    return Vector2.zero;
                }

                return controls.Gameplay.Look.ReadValue<Vector2>();
            }
        }

        public Vector2 BuildMove
        {
            get
            {
                if (!initialized)
                {
                    return Vector2.zero;
                }

                return controls.Build.Move.ReadValue<Vector2>();
            }
        }

        public Vector2 BuildLook
        {
            get
            {
                if (!initialized)
                {
                    return Vector2.zero;
                }

                return controls.Build.Look.ReadValue<Vector2>();
            }
        }

        public Vector2 PointerPosition
        {
            get
            {
                if (!initialized)
                {
                    return Vector2.zero;
                }

                return controls.Common.PointerPosition.ReadValue<Vector2>();
            }
        }

        public Vector2 PointerDelta
        {
            get
            {
                if (!initialized)
                {
                    return Vector2.zero;
                }

                return controls.Common.PointerDelta.ReadValue<Vector2>();
            }
        }

        public Vector2 Scroll
        {
            get
            {
                if (!initialized)
                {
                    return Vector2.zero;
                }

                return controls.Common.Scroll.ReadValue<Vector2>();
            }
        }

        public bool SprintHeld
        {
            get
            {
                return initialized && controls.Gameplay.Sprint.IsPressed();
            }
        }

        public event Action<InputAction.CallbackContext> JumpStarted;
        public event Action<InputAction.CallbackContext> JumpCanceled;
        public event Action<InputAction.CallbackContext> GameplaySelectPerformed;
        public event Action<InputAction.CallbackContext> GameplayAttackCommandPerformed;
        public event Action<InputAction.CallbackContext> GameplayCancelPerformed;
        public event Action<InputAction.CallbackContext> InteractPerformed;
        public event Action<InputAction.CallbackContext> PausePerformed;

        public event Action<InputAction.CallbackContext> UISubmitPerformed;
        public event Action<InputAction.CallbackContext> UICancelPerformed;

        public event Action<InputAction.CallbackContext> BattleSelectPerformed;

        public event Action<InputAction.CallbackContext> BuildPlacePerformed;
        public event Action<InputAction.CallbackContext> BuildRemovePerformed;
        public event Action<InputAction.CallbackContext> BuildRotatePerformed;
        public event Action<InputAction.CallbackContext> BuildCancelPerformed;

        public event Action<InputAction.CallbackContext> DialogueContinuePerformed;
        public event Action<InputAction.CallbackContext> DialogueSkipPerformed;
        public event Action<InputAction.CallbackContext> DialogueCancelPerformed;

        public void Initialize(InputMode defaultMode = InputMode.Gameplay)
        {
            if (initialized)
            {
                return;
            }

            controls = new GameInputActions();
            SubscribeEvents();

            controls.Common.Enable();
            initialized = true;

            SetMode(defaultMode);
        }

        public void Release()
        {
            if (!initialized)
            {
                return;
            }

            DisableAllModes();

            if (controls != null)
            {
                controls.Common.Disable();
                UnsubscribeEvents();
                controls.Dispose();
                controls = null;
            }

            modeStack.Clear();
            currentMode = InputMode.None;
            initialized = false;

            JumpStarted = null;
            JumpCanceled = null;
            GameplaySelectPerformed = null;
            GameplayAttackCommandPerformed = null;
            GameplayCancelPerformed = null;
            InteractPerformed = null;
            PausePerformed = null;

            UISubmitPerformed = null;
            UICancelPerformed = null;

            BattleSelectPerformed = null;

            BuildPlacePerformed = null;
            BuildRemovePerformed = null;
            BuildRotatePerformed = null;
            BuildCancelPerformed = null;

            DialogueContinuePerformed = null;
            DialogueSkipPerformed = null;
            DialogueCancelPerformed = null;
        }

        public void SetMode(InputMode mode)
        {
            EnsureInitialized();

            DisableModeMaps();

            currentMode = mode;

            switch (mode)
            {
                case InputMode.Gameplay:
                    controls.Gameplay.Enable();
                    break;

                case InputMode.UI:
                    controls.UI.Enable();
                    break;

                case InputMode.Build:
                    controls.Build.Enable();
                    break;

                case InputMode.Dialogue:
                    controls.Dialogue.Enable();
                    break;

                case InputMode.Battle:
                    controls.Battle.Enable();
                    break;

                case InputMode.None:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public void PushMode(InputMode mode)
        {
            EnsureInitialized();

            modeStack.Push(currentMode);
            SetMode(mode);
        }

        public void PopMode()
        {
            EnsureInitialized();

            if (modeStack.Count == 0)
            {
                SetMode(InputMode.Gameplay);
                return;
            }

            SetMode(modeStack.Pop());
        }

        public void ClearModeStack()
        {
            modeStack.Clear();
        }

        public void DisableAllModes()
        {
            if (controls == null)
            {
                currentMode = InputMode.None;
                return;
            }

            DisableModeMaps();
            currentMode = InputMode.None;
        }

        private void DisableModeMaps()
        {
            controls.Gameplay.Disable();
            controls.UI.Disable();
            controls.Build.Disable();
            controls.Dialogue.Disable();
        }

        private void SubscribeEvents()
        {
            controls.Gameplay.Jump.started += OnJumpStarted;
            controls.Gameplay.Jump.canceled += OnJumpCanceled;
            controls.Gameplay.Select.performed += OnGameplaySelectPerformed;
            controls.Gameplay.AttackCommand.performed += OnGameplayAttackCommandPerformed;
            controls.Gameplay.Cancel.performed += OnGameplayCancelPerformed;
            controls.Gameplay.Interact.performed += OnInteractPerformed;

            controls.Common.Pause.performed += OnPausePerformed;

            controls.UI.Submit.performed += OnUISubmitPerformed;
            controls.UI.Cancel.performed += OnUICancelPerformed;

            controls.Battle.Select.performed += OnBattleSelectPerformed;
            controls.Battle.Place.performed += OnBuildPlacePerformed;
            controls.Battle.Remove.performed += OnBuildRemovePerformed;
            controls.Battle.Rotate.performed += OnBuildRotatePerformed;
            controls.Battle.Cancel.performed += OnBuildCancelPerformed;

            controls.Dialogue.Continue.performed += OnDialogueContinuePerformed;
            controls.Dialogue.Skip.performed += OnDialogueSkipPerformed;
            controls.Dialogue.Cancel.performed += OnDialogueCancelPerformed;
        }

        private void UnsubscribeEvents()
        {
            controls.Gameplay.Jump.started -= OnJumpStarted;
            controls.Gameplay.Jump.canceled -= OnJumpCanceled;
            controls.Gameplay.Select.performed -= OnGameplaySelectPerformed;
            controls.Gameplay.AttackCommand.performed -= OnGameplayAttackCommandPerformed;
            controls.Gameplay.Cancel.performed -= OnGameplayCancelPerformed;
            controls.Gameplay.Interact.performed -= OnInteractPerformed;

            controls.Common.Pause.performed -= OnPausePerformed;

            controls.UI.Submit.performed -= OnUISubmitPerformed;
            controls.UI.Cancel.performed -= OnUICancelPerformed;

            controls.Battle.Select.performed -= OnBattleSelectPerformed;
            controls.Battle.Place.performed -= OnBuildPlacePerformed;
            controls.Battle.Remove.performed -= OnBuildRemovePerformed;
            controls.Battle.Rotate.performed -= OnBuildRotatePerformed;
            controls.Battle.Cancel.performed -= OnBuildCancelPerformed;

            controls.Dialogue.Continue.performed -= OnDialogueContinuePerformed;
            controls.Dialogue.Skip.performed -= OnDialogueSkipPerformed;
            controls.Dialogue.Cancel.performed -= OnDialogueCancelPerformed;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("GameInputManager is not initialized. Call GameInputManager.Instance.Initialize() before using it.");
            }
        }

        private void OnJumpStarted(InputAction.CallbackContext context)
        {
            JumpStarted?.Invoke(context);
        }

        private void OnJumpCanceled(InputAction.CallbackContext context)
        {
            JumpCanceled?.Invoke(context);
        }

        private void OnGameplaySelectPerformed(InputAction.CallbackContext context)
        {
            GameplaySelectPerformed?.Invoke(context);
        }

        private void OnGameplayAttackCommandPerformed(InputAction.CallbackContext context)
        {
            GameplayAttackCommandPerformed?.Invoke(context);
        }

        private void OnGameplayCancelPerformed(InputAction.CallbackContext context)
        {
            GameplayCancelPerformed?.Invoke(context);
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            InteractPerformed?.Invoke(context);
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            PausePerformed?.Invoke(context);
        }

        private void OnUISubmitPerformed(InputAction.CallbackContext context)
        {
            UISubmitPerformed?.Invoke(context);
        }

        private void OnUICancelPerformed(InputAction.CallbackContext context)
        {
            UICancelPerformed?.Invoke(context);
        }

        private void OnBuildPlacePerformed(InputAction.CallbackContext context)
        {
            BuildPlacePerformed?.Invoke(context);
        }

        private void OnBuildRemovePerformed(InputAction.CallbackContext context)
        {
            BuildRemovePerformed?.Invoke(context);
        }

        private void OnBuildRotatePerformed(InputAction.CallbackContext context)
        {
            BuildRotatePerformed?.Invoke(context);
        }

        private void OnBuildCancelPerformed(InputAction.CallbackContext context)
        {
            BuildCancelPerformed?.Invoke(context);
        }

        private void OnDialogueContinuePerformed(InputAction.CallbackContext context)
        {
            DialogueContinuePerformed?.Invoke(context);
        }

        private void OnDialogueSkipPerformed(InputAction.CallbackContext context)
        {
            DialogueSkipPerformed?.Invoke(context);
        }

        private void OnDialogueCancelPerformed(InputAction.CallbackContext context)
        {
            DialogueCancelPerformed?.Invoke(context);
        }

        private void OnBattleSelectPerformed(InputAction.CallbackContext context)
        {
            BattleSelectPerformed?.Invoke(context);
        }

        public bool BuildPlaceHeld
        {
            get
            {
                return initialized && controls.Battle.Place.IsPressed();
            }
        }

        public bool BuildRemoveHeld
        {
            get
            {
                return initialized && controls.Battle.Remove.IsPressed();
            }
        }

        public bool BuildRotateHeld
        {
            get
            {
                return initialized && controls.Battle.Rotate.IsPressed();
            }
        }

        public bool BuildCancelHeld
        {
            get
            {
                return initialized && controls.Battle.Cancel.IsPressed();
            }
        }
    }
}