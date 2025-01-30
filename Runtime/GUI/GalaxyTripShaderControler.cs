using UnityEngine;
using UnityEngine.UIElements;

[ExecuteInEditMode]
public class ScaleSphereWithOrthographicSize : MonoBehaviour
{
    public Camera orthographicCamera;
    public float baseOrthographicSize = 5f; // The orthographic size for which the sphere is designed

    private Vector3 _baseScale;
    public float scale;

    private void Start()
    {
        if (orthographicCamera == null)
        {
            Debug.LogError("Orthographic Camera is not assigned!");
            return;
        }

        // Store the initial scale of the sphere
        _baseScale = transform.localScale;

        UpdateSphereScale();
    }

    private void Update()
    {
        if (!orthographicCamera)
            return;

        // Update the sphere's scale if the orthographic size changes
        if (!Mathf.Approximately(orthographicCamera.orthographicSize, baseOrthographicSize))
        {
            UpdateSphereScale();
        }
    }

    private void UpdateSphereScale()
    {
        if (orthographicCamera == null)
            return;

        // Calculate the scale factor based on the current orthographic size
        float scaleFactor = orthographicCamera.orthographicSize / baseOrthographicSize;
        scaleFactor *= scale;

        // Scale the sphere uniformly
        transform.localScale.Set((_baseScale * scaleFactor).x, (_baseScale * scaleFactor).y, 0);
        transform.SetPositionAndRotation(new Vector3(0,0,(_baseScale * scaleFactor / 2).z), Quaternion.identity);
    }
}