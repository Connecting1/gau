using UnityEngine;

/// <summary>
/// 모바일 터치 기반 오빗 카메라 컨트롤러
/// 1-finger: 회전
/// 2-finger pinch: 줌
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = Vector3.zero;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private float maxVerticalAngle = 80f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private float initialDistance = 10f;

    [Header("Smoothing")]
    [SerializeField] private float smoothing = 5f;

    private float currentDistance;
    private float targetDistance;
    private float currentRotationX = 0f;
    private float currentRotationY = 0f;
    private float targetRotationX = 0f;
    private float targetRotationY = 0f;

    private Vector2 previousTouchPosition;
    private float previousPinchDistance;
    private bool isRotating = false;
    private bool isPinching = false;

    void Start()
    {
        // 초기 거리 설정
        currentDistance = initialDistance;
        targetDistance = initialDistance;

        // 타겟이 없으면 빈 GameObject 생성
        if (target == null)
        {
            GameObject targetObj = new GameObject("CameraTarget");
            target = targetObj.transform;
            target.position = Vector3.zero;
        }

        // 초기 카메라 위치 설정
        UpdateCameraPosition();
    }

    void LateUpdate()
    {
        HandleInput();
        SmoothCamera();
    }

    /// <summary>
    /// 입력 처리 (터치 및 마우스)
    /// </summary>
    private void HandleInput()
    {
        #if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
        #else
        HandleTouchInput();
        #endif
    }

    /// <summary>
    /// 마우스 입력 처리 (에디터 테스트용)
    /// </summary>
    private void HandleMouseInput()
    {
        // 회전 (좌클릭 드래그)
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            targetRotationX -= mouseY * rotationSpeed;
            targetRotationY += mouseX * rotationSpeed;

            targetRotationX = Mathf.Clamp(targetRotationX, minVerticalAngle, maxVerticalAngle);
        }

        // 줌 (마우스 휠)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetDistance -= scroll * zoomSpeed * 10f;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
    }

    /// <summary>
    /// 터치 입력 처리 (모바일)
    /// </summary>
    private void HandleTouchInput()
    {
        int touchCount = Input.touchCount;

        if (touchCount == 0)
        {
            isRotating = false;
            isPinching = false;
            return;
        }

        // 1-finger: 회전
        if (touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                isRotating = true;
                previousTouchPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Moved && isRotating)
            {
                Vector2 delta = touch.position - previousTouchPosition;

                targetRotationX -= delta.y * rotationSpeed * 0.1f;
                targetRotationY += delta.x * rotationSpeed * 0.1f;

                targetRotationX = Mathf.Clamp(targetRotationX, minVerticalAngle, maxVerticalAngle);

                previousTouchPosition = touch.position;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isRotating = false;
            }

            isPinching = false;
        }
        // 2-finger: 줌
        else if (touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                isPinching = true;
                previousPinchDistance = Vector2.Distance(touch0.position, touch1.position);
            }
            else if ((touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved) && isPinching)
            {
                float currentPinchDistance = Vector2.Distance(touch0.position, touch1.position);
                float deltaPinch = currentPinchDistance - previousPinchDistance;

                targetDistance -= deltaPinch * zoomSpeed * 0.01f;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

                previousPinchDistance = currentPinchDistance;
            }
            else if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended)
            {
                isPinching = false;
            }

            isRotating = false;
        }
    }

    /// <summary>
    /// 카메라를 부드럽게 이동
    /// </summary>
    private void SmoothCamera()
    {
        // 회전 값 스무딩
        currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, Time.deltaTime * smoothing);
        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, Time.deltaTime * smoothing);

        // 거리 스무딩
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * smoothing);

        // 카메라 위치 업데이트
        UpdateCameraPosition();
    }

    /// <summary>
    /// 실제 카메라 위치 계산 및 적용
    /// </summary>
    private void UpdateCameraPosition()
    {
        if (target == null) return;

        Quaternion rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);
        Vector3 direction = rotation * Vector3.back;
        Vector3 targetPosition = target.position + targetOffset;

        transform.position = targetPosition + direction * currentDistance;
        transform.LookAt(targetPosition);
    }

    /// <summary>
    /// 타겟 설정
    /// </summary>
    public void SetTarget(Transform newTarget, Vector3 offset = default)
    {
        target = newTarget;
        targetOffset = offset;
    }

    /// <summary>
    /// 카메라를 특정 위치로 리셋
    /// </summary>
    public void ResetCamera(Vector3? position = null, float? distance = null)
    {
        if (position.HasValue && target != null)
        {
            target.position = position.Value;
        }

        if (distance.HasValue)
        {
            currentDistance = distance.Value;
            targetDistance = distance.Value;
        }

        currentRotationX = 0f;
        currentRotationY = 0f;
        targetRotationX = 0f;
        targetRotationY = 0f;

        UpdateCameraPosition();
    }

    /// <summary>
    /// 줌 레벨 설정
    /// </summary>
    public void SetZoomDistance(float distance)
    {
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    }
}
