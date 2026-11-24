using System.Collections.Generic;
using Akila.FPSFramework;
using Akila.FPSFramework.Animation;
using UnityEngine;
using UnityEngine.Events;

namespace Kodimodi
{
    [DefaultExecutionOrder(10000)]
    public class NetworkRespawn : MonoBehaviour
    {
        public Transform cameraRoot;
        public Camera mainCamera;

        [Min(0.05f)]
        public float bodyRadius = 0.18f;

        public float backwardSpeed = 0.6f;

        [Range(0, 90)]
        public float rollAngle = 80f;

        [Min(0.05f)]
        public float rollTime = 0.45f;

        [Range(0, 31)]
        public int physicsLayer = 0;

        private Damageable damageable;
        private CharacterController controller;
        private FirstPersonController movement;
        private Rigidbody deathBody;

        private readonly List<Behaviour> paused =
            new List<Behaviour>();

        private Vector3 savedLocalPosition;
        private Quaternion savedLocalRotation;

        private Vector3 eyeOffset;
        private Quaternion startRotation;
        private Quaternion endRotation;

        private Camera hiddenWeaponCamera;
        private bool weaponCameraWasEnabled;

        private float elapsed;
        private bool dead;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            controller = GetComponent<CharacterController>();
            movement = GetComponent<FirstPersonController>();

            damageable.useFirstPersonDeathView = true;

            if (damageable.onDeath == null)
                damageable.onDeath = new UnityEvent();
        }

        private void OnEnable()
        {
            damageable.onDeath.AddListener(OnDeath);
        }

        private void OnDisable()
        {
            damageable.onDeath.RemoveListener(OnDeath);
            RestoreView();
        }

        private void Pause(Behaviour component)
        {
            if (component == null || !component.enabled)
                return;

            paused.Add(component);
            component.enabled = false;
        }

        private void OnDeath()
        {
            if (dead)
                return;

            Vector3 initialVelocity = controller.velocity;

            FPSFrameworkCore.IsInputActive = false;
            movement.FreezeForDeath();

            Pause(GetComponent<NetworkPlayerState>());

            if (cameraRoot == null)
                cameraRoot = movement.cameraTransform;

            var manager =
                GetComponentInChildren<CameraManager>(true);

            if (mainCamera == null && manager != null)
                mainCamera = manager.mainCamera;

            if (cameraRoot == null ||
                cameraRoot == transform ||
                mainCamera == null ||
                !mainCamera.transform.IsChildOf(cameraRoot))
            {
                Debug.LogError(
                    "Укажи CameraRoot и Main Camera в NetworkRespawn.",
                    this);

                return;
            }

            hiddenWeaponCamera = manager != null
                ? manager.overlayCamera
                : null;

            if (hiddenWeaponCamera != null)
            {
                weaponCameraWasEnabled = hiddenWeaponCamera.enabled;
                hiddenWeaponCamera.enabled = false;
            }

            foreach (var animator in
                     cameraRoot.GetComponentsInChildren<
                         ProceduralAnimator>(true))
            {
                Pause(animator);
            }

            foreach (var shaker in
                     cameraRoot.GetComponentsInChildren<
                         CameraShaker>(true))
            {
                Pause(shaker);
            }

            savedLocalPosition = cameraRoot.localPosition;
            savedLocalRotation = cameraRoot.localRotation;

            startRotation = cameraRoot.rotation;

            endRotation = startRotation *
                Quaternion.Euler(-10f, 0, rollAngle);

            Vector3 eye = mainCamera.transform.position;

            eyeOffset = Quaternion.Inverse(startRotation) *
                (eye - cameraRoot.position);

            var bodyObject =
                new GameObject("Local Death Camera Physics");

            bodyObject.layer = physicsLayer;
            bodyObject.transform.position = eye;

            var sphere = bodyObject.AddComponent<SphereCollider>();

            float near = mainCamera.nearClipPlane;

            float h = near * Mathf.Tan(
                mainCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);

            float w = h * mainCamera.aspect;

            sphere.radius = Mathf.Max(
                bodyRadius,
                Mathf.Sqrt(near * near + h * h + w * w) + 0.02f);

            foreach (var ownCollider in
                     GetComponentsInChildren<Collider>(true))
            {
                Physics.IgnoreCollision(
                    sphere,
                    ownCollider,
                    true);
            }

            deathBody = bodyObject.AddComponent<Rigidbody>();

            deathBody.mass = 1f;
            deathBody.useGravity = true;
            deathBody.constraints = RigidbodyConstraints.FreezeRotation;
            deathBody.interpolation = RigidbodyInterpolation.Interpolate;

            deathBody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            Vector3 backward = -Vector3.ProjectOnPlane(
                mainCamera.transform.forward,
                Vector3.up).normalized;

            initialVelocity =
                Vector3.ClampMagnitude(initialVelocity, 15f) +
                backward * backwardSpeed;

#if UNITY_6000_0_OR_NEWER
            deathBody.linearDamping = 0.35f;
            deathBody.linearVelocity = initialVelocity;
#else
            deathBody.drag = 0.35f;
            deathBody.velocity = initialVelocity;
#endif

            elapsed = 0;
            dead = true;
        }

        private void LateUpdate()
        {
            if (!dead || deathBody == null)
                return;

            elapsed += Time.deltaTime;

            float t = 1f - Mathf.Exp(
                -elapsed / Mathf.Max(0.05f, rollTime));

            Quaternion rotation = Quaternion.Slerp(
                startRotation,
                endRotation,
                t);

            cameraRoot.SetPositionAndRotation(
                deathBody.transform.position - rotation * eyeOffset,
                rotation);
        }

        private void RestoreView()
        {
            if (hiddenWeaponCamera != null)
            {
                hiddenWeaponCamera.enabled = weaponCameraWasEnabled;
                hiddenWeaponCamera = null;
            }

            if (deathBody != null)
            {
                deathBody.gameObject.SetActive(false);
                Destroy(deathBody.gameObject);
                deathBody = null;
            }

            if (dead && cameraRoot != null)
            {
                cameraRoot.localPosition = savedLocalPosition;
                cameraRoot.localRotation = savedLocalRotation;
            }

            dead = false;

            foreach (var component in paused)
            {
                if (component != null)
                    component.enabled = true;
            }

            paused.Clear();
        }

        public void Respawn(Vector3 position)
        {
            var battle = BattleClient.Instance;

            if (battle != null &&
                (battle.IsMatchEnded || battle.IsDisconnected))
            {
                return;
            }

            RestoreView();

            bool wasEnabled = controller.enabled;

            controller.enabled = false;
            transform.position = position;
            controller.enabled = wasEnabled;

            movement.ResetAfterNetworkRespawn();
            damageable.ResetForRespawn();

            foreach (var shaker in GetComponentsInChildren<CameraShaker>(true))
                shaker.ResetShake();

            // Респавн не снимает блокировку из-за отсутствия видео.
            FPSFrameworkCore.IsInputActive =
                battle == null || battle.CanPlay;
        }
    }
}