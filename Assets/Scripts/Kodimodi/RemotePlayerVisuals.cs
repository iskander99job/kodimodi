using Akila.FPSFramework;
using Kodimodi.Models;
using UnityEngine;

namespace Kodimodi
{
    public class RemotePlayerVisuals : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float positionLerp = 15f;
        [SerializeField] private float rotationLerp = 15f;

        private Damageable damageable;

        private Vector3 targetPos;
        private Quaternion targetRot;

        private bool initialized;
        private bool frozen;
        private bool dead;

        private Collider[] deathColliders;
        private bool[] colliderStates;
        private float[] layerWeights;

        private static readonly int HorizontalHash =
            Animator.StringToHash("Horizontal");

        private static readonly int VerticalHash =
            Animator.StringToHash("Vertical");

        private static readonly int InputMagnitudeHash =
            Animator.StringToHash("InputMagnitude");

        private static readonly int FireHash =
            Animator.StringToHash("Fire");

        private static readonly int IsDeadHash =
            Animator.StringToHash("IsDead");

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            damageable = GetComponent<Damageable>();

            targetPos = transform.position;
            targetRot = transform.rotation;
        }

        public void Freeze()
        {
            frozen = true;
            targetPos = transform.position;

            // Анимация смерти должна продолжать проигрываться.
            if (animator != null && !dead)
                animator.speed = 0f;
        }

        public void Shoot()
        {
            if (dead || frozen || animator == null)
                return;

            animator.SetBool(FireHash, true);
        }

        public void Apply(PlayerBodyPositionModel model)
        {
            if (dead || frozen)
                return;

            Vector3 newPos = new Vector3(
                model.Position.X,
                model.Position.Y,
                model.Position.Z);

            targetRot = Quaternion.Euler(
                0f, model.Rotation.Y, 0f);

            if (!initialized)
            {
                transform.position = newPos;
                transform.rotation = targetRot;
                initialized = true;
            }

            targetPos = newPos;

            if (animator == null)
                return;

            animator.SetFloat(HorizontalHash, model.H);
            animator.SetFloat(VerticalHash, model.V);

            animator.SetFloat(
                InputMagnitudeHash,
                Mathf.Clamp01(
                    new Vector2(model.H, model.V).magnitude));
        }

        private void Update()
        {
            if (!dead &&
                damageable != null &&
                damageable.health <= 0)
            {
                SetAlive(false);
            }

            if (dead || frozen || !initialized)
                return;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                Time.deltaTime * positionLerp);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * rotationLerp);
        }

        public void SetHealth(float value)
        {
            if (damageable != null)
                damageable.health = value;

            if (value <= 0)
                SetAlive(false);
        }

        public void SetAlive(bool alive)
        {
            if (alive)
            {
                if (dead)
                    RestoreAliveVisuals();

                return;
            }

            // Повторное сообщение не перезапускает смерть.
            if (dead)
                return;

            dead = true;

            targetPos = transform.position;
            targetRot = transform.rotation;

            // Модель остаётся видимой, коллайдеры отключаются.
            deathColliders =
                GetComponentsInChildren<Collider>(true);

            colliderStates = new bool[deathColliders.Length];

            for (int i = 0; i < deathColliders.Length; i++)
            {
                colliderStates[i] = deathColliders[i].enabled;
                deathColliders[i].enabled = false;
            }

            if (animator == null)
                return;

            animator.speed = 1f;

            animator.SetBool(FireHash, false);
            animator.SetFloat(HorizontalHash, 0);
            animator.SetFloat(VerticalHash, 0);
            animator.SetFloat(InputMagnitudeHash, 0);

            // AimOffsets и Firing не должны менять позу смерти.
            layerWeights = new float[animator.layerCount];

            for (int i = 1; i < animator.layerCount; i++)
            {
                layerWeights[i] = animator.GetLayerWeight(i);
                animator.SetLayerWeight(i, 0);
            }

            animator.SetBool(IsDeadHash, true);
        }

        private void RestoreAliveVisuals()
        {
            dead = false;
            frozen = false;

            if (deathColliders != null)
            {
                for (int i = 0; i < deathColliders.Length; i++)
                {
                    if (deathColliders[i] != null)
                    {
                        deathColliders[i].enabled =
                            colliderStates[i];
                    }
                }

                deathColliders = null;
                colliderStates = null;
            }

            if (animator == null)
                return;

            // Сбрасываем смерть, переходы и состояния всех слоёв.
            animator.Rebind();

            animator.speed = 1f;
            animator.SetBool(IsDeadHash, false);
            animator.SetBool(FireHash, false);

            animator.SetFloat(HorizontalHash, 0f);
            animator.SetFloat(VerticalHash, 0f);
            animator.SetFloat(InputMagnitudeHash, 0f);

// Возвращаем веса AimOffsets, Firing и других слоёв.
            if (layerWeights != null)
            {
                int count = Mathf.Min(
                    animator.layerCount, layerWeights.Length);

                for (int i = 1; i < count; i++)
                    animator.SetLayerWeight(i, layerWeights[i]);

                layerWeights = null;
            }

            animator.Play("Base Layer.Standing", 0, 0f);

// Сразу применяем живую позу, не ожидая следующего кадра.
            animator.Update(0f);
        }

        public void OnRemoteRespawn(Vector3 position)
        {
            transform.position = position;

            targetPos = position;
            targetRot = transform.rotation;
            initialized = true;

            if (damageable != null)
                damageable.ResetForRespawn();

            RestoreAliveVisuals();
        }
    }
}