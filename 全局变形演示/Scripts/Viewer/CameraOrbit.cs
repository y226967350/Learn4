using ObjViewer.Demo;
using UnityEngine;

namespace ObjViewer.Viewer
{
    /// <summary>
    /// 浏览模型的相机控制器：鼠标左键旋转、右键/中键平移、滚轮缩放。
    /// 挂到主摄像机上即可。
    /// </summary>
    public class CameraOrbit : MonoBehaviour
    {
        [Header("旋转")]
        public float rotateSpeed = 5f;

        [Header("平移")]
        public float panSpeed = 0.5f;

        [Header("缩放")]
        public float zoomSpeed = 2f;
        public float minDistance = 0.1f;
        public float maxDistance = 100f;

        [Header("目标")]
        public Transform target;

        private float yaw;
        private float pitch;
        private float distance = 5f;

        private Vector3 focusPoint = Vector3.zero;

        private void Start()
        {
            if (target != null)
            {
                focusPoint = target.position;
            }

            Vector3 offset = transform.position - focusPoint;
            distance = offset.magnitude;
            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(offset.y / Mathf.Max(distance, 0.001f)) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            // 鼠标位于演示面板或正在拖拽 FFD 控制点时，不要同时旋转/缩放相机。
            if (!GlobalDeformationDemo.ShouldBlockCamera)
            {
                HandleRotate();
                HandlePan();
                HandleZoom();
            }

            ApplyTransform();
        }

        private void HandleRotate()
        {
            if (Input.GetMouseButton(0))
            {
                yaw += Input.GetAxis("Mouse X") * rotateSpeed;
                pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
                pitch = Mathf.Clamp(pitch, -89f, 89f);
            }
        }

        private void HandlePan()
        {
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                float dx = -Input.GetAxis("Mouse X") * panSpeed * (distance * 0.001f + 0.1f);
                float dy = -Input.GetAxis("Mouse Y") * panSpeed * (distance * 0.001f + 0.1f);

                focusPoint += transform.right * dx;
                focusPoint += transform.up * dy;
            }
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                distance -= scroll * zoomSpeed * Mathf.Max(distance, 0.5f);
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }

        private void ApplyTransform()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = rotation;
            transform.position = focusPoint - rotation * Vector3.forward * distance;
        }

        /// <summary>把焦点对准指定物体的包围盒中心，方便浏览任意模型。</summary>
        public void FrameOn(Transform obj)
        {
            target = obj;
            if (obj == null)
            {
                return;
            }

            Renderer r = obj.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Bounds bounds = r.bounds;
                focusPoint = bounds.center;
                float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
                distance = radius * 2.4f;
                maxDistance = Mathf.Max(maxDistance, distance * 2f);
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
            else
            {
                focusPoint = obj.position;
                distance = 5f;
            }
        }
    }
}
