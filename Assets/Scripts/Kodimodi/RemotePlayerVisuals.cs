using Akila.FPSFramework;
using Kodimodi.Models;
using UnityEngine;
using System.Collections.Generic;

namespace Kodimodi
{
    public class RemotePlayerVisuals : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.05f)]
        private float interpolationDelay = 0.1f;

// Начальные значения для калибровки анимаций.
        [SerializeField, Min(0.1f)]
        private float walkSpeed = 2f;

        [SerializeField, Min(0.1f)]
        private float runSpeed = 5f;

// Более крупное перемещение считаем телепортом.
        [SerializeField, Min(0.1f)]
        private float teleportDistance = 8f;

        private struct Snapshot
        {
            public double time;
            public Vector3 position;
            public Quaternion rotation;
        }

        private readonly List<Snapshot> snapshots =
            new List<Snapshot>(32);

        private double clockOffset;
        private float lastArrival;

        private static readonly int LocomotionSpeedHash =
            Animator.StringToHash("LocomotionSpeed");

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
            snapshots.Clear();
            targetPos = transform.position;

            // Анимация смерти должна продолжать проигрываться.
            if (animator != null && !dead)
                animator.speed = 0f;
        }

        public void Shoot()
        {
            if (dead || frozen || animator == null)
                return;

            animator.SetTrigger(FireHash);
        }

public void Apply(PlayerBodyPositionModel model)
{
    if (dead || frozen)
        return;

    Vector3 position = new Vector3(
        model.Position.X,
        model.Position.Y,
        model.Position.Z);

    Quaternion rotation = Quaternion.Euler(
        0f, model.Rotation.Y, 0f);

    double now = Time.timeAsDouble;

    bool reset = !initialized;

    if (snapshots.Count > 0)
    {
        Snapshot last = snapshots[snapshots.Count - 1];

        reset |= Time.time - lastArrival > 0.5f ||
            Vector3.Distance(last.position, position) >
            teleportDistance;
    }

    if (reset)
    {
        snapshots.Clear();

        transform.SetPositionAndRotation(position, rotation);

        initialized = true;
        SetMovementAnimation(Vector3.zero);
    }

    // Часы игроков могут иметь разное начало отсчёта.
    // Переводим время отправителя в локальную временную шкалу.
    if (snapshots.Count == 0)
        clockOffset = now - model.SentAt;

    double sampleTime = model.SentAt + clockOffset;
    lastArrival = Time.time;

    if (snapshots.Count > 0 &&
        sampleTime <= snapshots[snapshots.Count - 1].time)
    {
        return;
    }

    snapshots.Add(new Snapshot
    {
        time = sampleTime,
        position = position,
        rotation = rotation
    });

    if (snapshots.Count > 64)
        snapshots.RemoveAt(0);
}

private void Update()
{
    if (!dead &&
        damageable != null &&
        damageable.health <= 0)
    {
        SetAlive(false);
    }

    if (dead || frozen || !initialized || snapshots.Count == 0)
        return;

    double renderTime =
        Time.timeAsDouble - interpolationDelay;

    while (snapshots.Count > 2 &&
           snapshots[1].time <= renderTime)
    {
        snapshots.RemoveAt(0);
    }

    Snapshot a = snapshots[0];
    Snapshot b = snapshots[snapshots.Count > 1 ? 1 : 0];

    double interval = b.time - a.time;

    float t = interval > 0
        ? Mathf.Clamp01(
            (float)((renderTime - a.time) / interval))
        : 0f;

    Vector3 previousPosition = transform.position;

    transform.SetPositionAndRotation(
        Vector3.Lerp(a.position, b.position, t),
        Quaternion.Slerp(a.rotation, b.rotation, t));

    Vector3 velocity = Time.deltaTime > 0f
        ? (transform.position - previousPosition) / Time.deltaTime
        : Vector3.zero;

    // Вертикальное движение не должно ускорять шаги.
    velocity.y = 0f;

    SetMovementAnimation(velocity);
}

private void SetMovementAnimation(Vector3 velocity)
{
    if (animator == null)
        return;

    float speed = velocity.magnitude;

    if (speed < 0.05f)
    {
        animator.SetFloat(HorizontalHash, 0f);
        animator.SetFloat(VerticalHash, 0f);
        animator.SetFloat(InputMagnitudeHash, 0f);
        animator.SetFloat(LocomotionSpeedHash, 1f);
        return;
    }

    Vector3 direction =
        transform.InverseTransformDirection(velocity / speed);

    // В существующем Blend Tree:
    // ходьба — координаты +/-1, бег — +/-2.
    // Диагональные точки тоже расположены по квадрату.
    direction /= Mathf.Max(
        Mathf.Abs(direction.x),
        Mathf.Abs(direction.z));

    float walk = Mathf.Max(0.1f, walkSpeed);
    float run = Mathf.Max(walk + 0.1f, runSpeed);

    float runBlend = Mathf.InverseLerp(walk, run, speed);
    float referenceSpeed = Mathf.Lerp(walk, run, runBlend);
    float ring = 1f + runBlend;

    animator.SetFloat(HorizontalHash, direction.x * ring);
    animator.SetFloat(VerticalHash, direction.z * ring);

    // Теперь это скорость в единицах Unity в секунду.
    animator.SetFloat(InputMagnitudeHash, speed);

    animator.SetFloat(
        LocomotionSpeedHash,
        Mathf.Clamp(speed / referenceSpeed, 0.05f, 3f));
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
            snapshots.Clear();
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

            animator.ResetTrigger(FireHash);
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
            snapshots.Clear();
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
            animator.SetFloat(LocomotionSpeedHash, 1f);
            animator.speed = 1f;
            animator.SetBool(IsDeadHash, false);
            animator.ResetTrigger(FireHash);

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