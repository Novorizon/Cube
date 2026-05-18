using UnityEngine;

public class VisualProjectileEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.15f;
    [SerializeField] private bool rotateToDirection = true;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private float timer;
    private bool playing;

    public float Duration
    {
        get
        {
            return duration;
        }
    }

    public void Play(Vector3 start, Vector3 end)
    {
        startPosition = start;
        endPosition = end;
        timer = 0f;
        playing = true;

        transform.position = startPosition;

        Vector3 direction = endPosition - startPosition;
        if (rotateToDirection && direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private void Update()
    {
        if (!playing)
        {
            return;
        }

        timer += Time.deltaTime;

        float t = duration <= 0f ? 1f : Mathf.Clamp01(timer / duration);
        float eased = 1f - Mathf.Pow(1f - t, 2f);

        transform.position = Vector3.Lerp(startPosition, endPosition, eased);

        if (t >= 1f)
        {
            playing = false;
            Destroy(gameObject, 0.15f);
        }
    }
}