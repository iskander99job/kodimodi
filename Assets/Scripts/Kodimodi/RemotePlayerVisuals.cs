using Kodimodi.Models;
using UnityEngine;

namespace Kodimodi
{
    public class RemotePlayerVisuals : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float positionLerp = 15f;
        [SerializeField] private float rotationLerp = 15f;

        private Vector3 targetPos;
        private Quaternion targetRot;
        private Vector3 lastPacketPos; // запоминаем позицию из прошлого пакета
        private bool initialized;
        private bool prevJumping;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
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

            Debug.Log(isMoving);
            animator.SetBool("IsMoving", isMoving);

            if (model.IsJumping && !prevJumping)
                animator.SetTrigger("Jump");

            prevJumping = model.IsJumping;
        }

        private void Update()
        {
            if (!initialized) return;

            // Плавно двигаемся к цели
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerp);
        }
    }
}