using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Player/Character Input")]
    public class CharacterInput : MonoBehaviour
    {
        [Header("Toggles")]
        public bool allowInputWhilePaused;
        public bool toggleAim = false;
        public bool toggleCrouch = true;
        public bool toggleLean = false;
        public bool reverseSprintInput = false;
        public bool allowTacticalSprining = true;

        [Header("Sensitivity")]
        public float sensitivityOnMouse = 1;
        public float sensitivityOnGamepad = 1;

        public AnimationCurve fovToSensitivityCurve =
            new AnimationCurve(
                new Keyframe(0, 1),
                new Keyframe(1, 0));

        public bool isDynamicSensitivityEnabled = true;

        public Action onLeanRight;
        public Action onLeanLeft;

        public Controls controls;

        public CharacterManager characterManager { get; protected set; }
        public Camera mainCamera { get; protected set; }

        private CameraManager cameraManager;

        protected Vector2 moveInputRaw { get; set; }
        public virtual Vector2 MoveInput => moveInputRaw.normalized;
        public virtual Vector2 MoveInputRaw => moveInputRaw;

        protected Vector2 rawLookInput { get; set; }
        public virtual Vector2 RawLookInput => rawLookInput;

        protected Vector2 lookInput { get; set; }
        public virtual Vector2 LookInput => lookInput;

        protected bool sprintInput { get; set; }
        public virtual bool SprintInput => sprintInput;

        protected bool tacticalSprintInput { get; set; }
        public virtual bool TacticalSprintInput => tacticalSprintInput;

        protected bool rawTacticalSprintInput;
        public virtual bool RawTacticalSprintInput => rawTacticalSprintInput;

        protected bool jumpInput { get; set; }
        public virtual bool JumpInput => jumpInput;

        protected bool crouchInput { get; set; }
        public virtual bool CrouchInput => crouchInput;

        protected bool leanRightInput { get; set; }
        public virtual bool LeanRightInput => leanRightInput;

        protected bool leanLeftInput { get; set; }
        public virtual bool LeanLeftInput => leanLeftInput;

        protected Vector2 addedLookValue { get; set; }
        public virtual Vector2 AddedLookValue => addedLookValue;

        public bool VideoInputBlocked { get; set; }

        protected bool IsInputAllowed =>
            FPSFrameworkCore.IsActive &&
            FPSFrameworkCore.IsInputActive &&
            !VideoInputBlocked;

        public virtual bool item1 =>
            IsInputAllowed ? controls.Player.Item1.triggered : false;

        public virtual bool item2 =>
            IsInputAllowed ? controls.Player.Item2.triggered : false;

        public virtual bool item3 =>
            IsInputAllowed ? controls.Player.Item3.triggered : false;

        public virtual bool item4 =>
            IsInputAllowed ? controls.Player.Item4.triggered : false;

        public virtual bool item5 =>
            IsInputAllowed ? controls.Player.Item5.triggered : false;

        public virtual bool item6 =>
            IsInputAllowed ? controls.Player.Item6.triggered : false;

        public virtual bool item7 =>
            IsInputAllowed ? controls.Player.Item7.triggered : false;

        public virtual bool item8 =>
            IsInputAllowed ? controls.Player.Item8.triggered : false;

        public virtual bool item9 =>
            IsInputAllowed ? controls.Player.Item9.triggered : false;

        public virtual bool itemUp =>
            IsInputAllowed
                ? controls.Player.SwitchItem.ReadValue<float>() > 0
                : false;

        public virtual bool itemDown =>
            IsInputAllowed
                ? controls.Player.SwitchItem.ReadValue<float>() < 0
                : false;

        public virtual bool itemNext =>
            IsInputAllowed ? controls.Player.NextItem.triggered : false;

        public virtual bool itemPrevious =>
            IsInputAllowed ? controls.Player.PreviousItem.triggered : false;

        public virtual bool defaultItem =>
            IsInputAllowed ? controls.Player.DefaultItem.triggered : false;

        [HideInInspector]
        public bool rawSprintInput;

        private float lastSprintClickTime;

        public bool IsSprintingAtAll()
        {
            return tacticalSprintInput || sprintInput;
        }

        private void Awake()
        {
            controls = new Controls();

            FPSFrameworkCore.IsInputActive = true;
            FPSFrameworkCore.IsActive = true;
        }

        private void Start()
        {
            characterManager = GetComponent<CharacterManager>();
            cameraManager = this.DeepSearch<CameraManager>();
        }

        protected void Update()
        {
            if (!IsInputAllowed ||
                (!allowInputWhilePaused && FPSFrameworkCore.IsPaused))
            {
                moveInputRaw = Vector2.zero;
                rawLookInput = Vector2.zero;
                lookInput = Vector2.zero;

                sprintInput = false;
                tacticalSprintInput = false;
                rawTacticalSprintInput = false;
                jumpInput = false;

                leanRightInput = false;
                leanLeftInput = false;

                addedLookValue = Vector2.zero;
                rawSprintInput = false;

                return;
            }

            if (controls == null)
                return;

            moveInputRaw = controls.Player.Move.ReadValue<Vector2>();

            Vector2 rawLookUnscaled =
                controls.Player.Look.ReadValue<Vector2>();

            rawLookInput = 100 * new Vector2(
                rawLookUnscaled.x * FPSFrameworkCore.XSensitivityMultiplier,
                rawLookUnscaled.y * FPSFrameworkCore.YSensitivityMultiplier
            ) * FPSFrameworkCore.SensitivityMultiplier;

            if (MoveInput.y < 0)
            {
                sprintInput = false;
                tacticalSprintInput = false;
            }
            else
            {
                tacticalSprintInput =
                    MoveInput.y > 0 && allowTacticalSprining
                        ? rawTacticalSprintInput
                        : false;

                sprintInput = MoveInput.y > 0
                    ? rawSprintInput
                    : false;
            }

            rawSprintInput = reverseSprintInput
                ? !controls.Player.Sprint.IsPressed()
                : controls.Player.Sprint.IsPressed();

            if (tacticalSprintInput)
                sprintInput = false;

            controls.Player.TacticalSprint.HasDoupleClicked(
                ref rawTacticalSprintInput,
                ref lastSprintClickTime);

            jumpInput = controls.Player.Jump.triggered;

            mainCamera = Camera.main;

            float finalSensitivity = 1f;
            float deviceSenstivity = 1f;

            if (FPSFrameworkCore.GetActiveControlScheme() ==
                ControlScheme.Mouse)
            {
                deviceSenstivity = sensitivityOnMouse * 0.1f;
            }

            if (FPSFrameworkCore.GetActiveControlScheme() ==
                ControlScheme.Gamepad)
            {
                deviceSenstivity = sensitivityOnGamepad;
            }

            if (characterManager?.character != null)
            {
                float baseSensitivity = deviceSenstivity;

                if (mainCamera != null && isDynamicSensitivityEnabled)
                {
                    float defaultFov = cameraManager
                        ? cameraManager.defaultMainFOV
                        : 60f;

                    finalSensitivity = Mathf.Lerp(
                        baseSensitivity,
                        0,
                        fovToSensitivityCurve.Evaluate(
                            mainCamera.fieldOfView / defaultFov));
                }
                else
                {
                    finalSensitivity = baseSensitivity;
                }
            }

            if (FPSFrameworkCore.IsPaused)
                finalSensitivity = 0;

            Vector2 calculatedLook =
                addedLookValue + rawLookInput * finalSensitivity;

            lookInput = calculatedLook / 200f + addedLookValue;
            addedLookValue = Vector2.zero;

            if ((sprintInput || jumpInput) && crouchInput)
                crouchInput = false;
        }

        private void LateUpdate()
        {
            if ((leanRightInput && leanLeftInput) ||
                sprintInput ||
                tacticalSprintInput)
            {
                leanRightInput = false;
                leanLeftInput = false;
            }
        }

        protected void AddInputListner()
        {
            if (controls?.Player == null)
                return;

            controls.Player.Crouch.performed += _ =>
            {
                if (IsInputAllowed && !toggleCrouch)
                    crouchInput = true;
            };

            controls.Player.Crouch.canceled += _ =>
            {
                if (IsInputAllowed)
                    crouchInput = toggleCrouch ? !crouchInput : false;
            };

            controls.Player.LeanRight.performed += _ =>
            {
                if (IsInputAllowed && !toggleLean)
                    leanRightInput = true;
            };

            controls.Player.LeanRight.canceled += _ =>
            {
                if (!IsInputAllowed)
                    return;

                if (!toggleLean)
                {
                    leanRightInput = false;
                }
                else
                {
                    leanLeftInput = false;
                    leanRightInput = !leanRightInput;
                }
            };

            controls.Player.LeanLeft.performed += _ =>
            {
                if (IsInputAllowed && !toggleLean)
                    leanLeftInput = true;
            };

            controls.Player.LeanLeft.canceled += _ =>
            {
                if (!IsInputAllowed)
                    return;

                if (!toggleLean)
                {
                    leanLeftInput = false;
                }
                else
                {
                    leanRightInput = false;
                    leanLeftInput = !leanLeftInput;
                }
            };
        }

        public virtual void AddLookValue(Vector2 value)
        {
            if (IsInputAllowed)
                addedLookValue += value;
        }

        private void OnEnable()
        {
            controls?.Enable();
            AddInputListner();
        }

        private void OnDisable()
        {
            controls?.Disable();
        }

        private void OnDestroy()
        {
            controls?.Disable();
            controls?.Dispose();
        }
    }
}