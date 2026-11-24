using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Rotation Settings (Right Mouse)")]
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float minPitch = -89f;
    [SerializeField] private float maxPitch = 89f;

    [Header("Movement Settings")]
    [SerializeField] private float normalSpeed = 15f;
    [SerializeField] private float boostMultiplier = 1.5f; // 按住Shift的加速倍率
    [SerializeField] private float minSpeed = 2f;          // 滚轮能调的最低速度
    [SerializeField] private float maxSpeed = 100f;        // 滚轮能调的最高速度
    [SerializeField] private float speedScrollStep = 5f;   // 滚一格滚轮改变的速度值
    [SerializeField] private bool moveOnXZPlane = true;    // WASD平移是否锁定在水平面

    [Header("Height Limit")]
    [Tooltip("摄像机允许的最低世界 Y。设成 0 就是贴地，默认 1 避免贴面时看不到格子")]
    [SerializeField] private float minHeight = 1f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private float currentSpeed = 0f; // 实时记录当前的基础速度

    private void Start()
    {
        // 初始化角度和速度
        Vector3 angles = transform.eulerAngles;
        rotationX = angles.y;
        rotationY = angles.x;
        currentSpeed = normalSpeed;

        ClampHeight();
    }

    private void Update()
    {
        // 核心逻辑：只有长按鼠标右键时，才允许旋转视角、WASD移动和滚轮调速
        if (Input.GetMouseButton(1))
        {
            HandleRotation();
            HandleSpeedScroll();
            HandleMovement();
        }

        // 不论是否在移动，都不允许低于 minHeight
        ClampHeight();
    }

    private void ClampHeight()
    {
        Vector3 position = transform.position;
        if (position.y >= minHeight) return;

        position.y = minHeight;
        transform.position = position;
    }

    private void HandleRotation()
    {
        rotationX += Input.GetAxis("Mouse X") * mouseSensitivity;
        rotationY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        rotationY = Mathf.Clamp(rotationY, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
    }

    private void HandleSpeedScroll()
    {
        // 读取滚轮输入，向前滚是正数，向后滚是负数
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            currentSpeed += scroll * speedScrollStep;
            currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);
            // 如果想在控制台看到速度变化，可以取消下面这行的注释
            // Debug.Log("Current Speed: " + currentSpeed);
        }
    }

    private void HandleMovement()
    {
        // 1. 获取 WASD 输入
        float moveX = Input.GetAxisRaw("Horizontal"); // A / D
        float moveZ = Input.GetAxisRaw("Vertical");   // W / S

        // 2. 获取 Q / E 升降输入 (Q 降，E 升)
        float moveY = 0f;
        if (Input.GetKey(KeyCode.E)) moveY = 1f;
        if (Input.GetKey(KeyCode.Q)) moveY = -1f;

        if (moveX == 0 && moveZ == 0 && moveY == 0) return;

        // 3. 获取方向向量
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        if (moveOnXZPlane)
        {
            // 压平移动方向，防止按 W 的时候往地底下钻
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
        }

        // 4. 组合最终移动向量
        Vector3 moveDir = (forward * moveZ + right * moveX + Vector3.up * moveY).normalized;

        // 5. 计算最终速度 (按住 Shift 时，速度乘以倍率)
        float finalSpeed = currentSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            finalSpeed *= boostMultiplier;
        }

        // 6. 移动摄像机
        transform.position += moveDir * finalSpeed * Time.deltaTime;
    }
}