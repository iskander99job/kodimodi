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
        private Vector3 lastPacketPos; // запоминаем позицию из прошлого пакета
        private bool initialized;
        private bool prevJumping;
        private bool frozen;
        
        private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
        private static readonly int VerticalHash = Animator.StringToHash("Vertical");
        private static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");
        private static readonly int FireHash = Animator.StringToHash("Fire");

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            damageable = GetComponent<Damageable>();
        }

        public void Freeze()
        {
            frozen = true;
            targetPos = transform.position;

            if (animator != null)
                animator.speed = 0f;
        }
        
        public void Shoot()
        {
            animator.SetBool(FireHash, true);
        }
        
        public void Apply(PlayerBodyPositionModel model)
        {
            Vector3 newPos = new Vector3(model.Position.X, model.Position.Y, model.Position.Z);
            targetRot = Quaternion.Euler(0f, model.Rotation.Y, 0f);

            if (!initialized)
            {
                transform.position = newPos;
                transform.rotation = targetRot;
                lastPacketPos = newPos;
                initialized = true;
            }

            // --- ВЫЧИСЛЕНИЕ ДВИЖЕНИЯ ---
            // Смотрим, насколько сдвинулась цель с прошлого раза
            float distance = Vector3.Distance(newPos, lastPacketPos);
            
            // Если сдвинулись больше чем на 1 сантиметр (защита от микродрожания)
            bool isMoving = distance > 0.01f;
            lastPacketPos = newPos; // обновляем для следующего кадра

            targetPos = newPos;

            // --- АНИМАЦИИ ---
            if (animator == null) return;
            
            // 2. Передаем направления в Animator
            animator.SetFloat(HorizontalHash, model.H);
            animator.SetFloat(VerticalHash, model.V);

            // 3. Вычисляем длину вектора для InputMagnitude
            Vector2 inputVector = new Vector2(model.H, model.V);
            float inputMagnitude = Mathf.Clamp01(inputVector.magnitude); 
            animator.SetFloat(InputMagnitudeHash, inputMagnitude);

            // if (model.IsJumping && !prevJumping)
            //     animator.SetTrigger("Jump");
            //
            // prevJumping = model.IsJumping;
        }

        private void Update()
        {
            if (frozen || !initialized) return;

            // Плавно двигаемся к цели
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerp);
        }
        
        public void SetHealth(float value)
        {
            if (damageable != null) damageable.health = value;
        }

        // мёртвый игрок исчезает: нет модели и нет коллайдеров, пули летят сквозь
        public void SetAlive(bool alive)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = alive;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = alive;
        }

        public void OnRemoteRespawn(Vector3 position)
        {
            transform.position = position;   // сразу, без плавного движения через карту
            targetPos = position;
            lastPacketPos = position;
            initialized = true;

            if (damageable != null) damageable.ResetForRespawn();
            SetAlive(true);
        }
    }
}