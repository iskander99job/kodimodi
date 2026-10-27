using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;

    // Кэшируем хэши параметров
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");
    private static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");
    private static readonly int FireHash = Animator.StringToHash("Fire");

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (animator == null) return;

        // 1. Получаем ввод (A/D, W/S)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // 2. Передаем направления в Animator
        animator.SetFloat(HorizontalHash, h);
        animator.SetFloat(VerticalHash, v);

        // 3. Вычисляем длину вектора для InputMagnitude
        Vector2 inputVector = new Vector2(h, v);
        float inputMagnitude = Mathf.Clamp01(inputVector.magnitude); 
        animator.SetFloat(InputMagnitudeHash, inputMagnitude);

        // 4. Обработка стрельбы (Space)
        // Используем GetKey, так как Fire - это Bool (стреляем, пока держим кнопку)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            animator.SetBool(FireHash, true);
        }
    }
}