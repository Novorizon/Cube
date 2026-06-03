using UnityEngine;

namespace Game
{
    public class TowerRotateView : MonoBehaviour
    {
        [SerializeField] private float rotateSpeed = 45f;
        [SerializeField] private bool rotateAfterBuilt = true;

        private bool isRotating;

        public void StartRotate()
        {
            isRotating = true;
        }

        public void StopRotate()
        {
            isRotating = false;
        }

        public void SetRotateSpeed(float speed)
        {
            rotateSpeed = speed;
        }

        private void Start()
        {
            if (rotateAfterBuilt)
            {
                StartRotate();
            }
        }

        private void Update()
        {
            if (!isRotating)
            {
                return;
            }

            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);
        }
    }
}