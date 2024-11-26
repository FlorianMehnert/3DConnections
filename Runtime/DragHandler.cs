using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CircleCollider2D))]

public class DragHandler : MonoBehaviour 
{

    private Vector3 _screenPoint;
    private Vector3 _offset;
    
    private void OnMouseDown()
    {
        if (Camera.main == null) return;
        _screenPoint = Camera.main.WorldToScreenPoint(gameObject.transform.position);

        _offset = gameObject.transform.position - Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, _screenPoint.z));
    }

    private void OnMouseDrag()
    {
        var curScreenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, _screenPoint.z);

        if (Camera.main == null) return;
        var curPosition = Camera.main.ScreenToWorldPoint(curScreenPoint) + _offset;
        transform.position = curPosition;
    }

}