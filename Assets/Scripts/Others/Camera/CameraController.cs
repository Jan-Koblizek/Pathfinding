using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    private float movementSpeed;
    [SerializeField]
    private float stoppingSpeed;
    private float movementX = 0.0f;
    private float movementY = 0.0f;
    private Camera cam;
    private float higherYbound;
    private float lowerYbound;
    private float higherXbound;
    private float lowerXbound;

    private float previousScrollDelta = 0.0f;
    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
    }

    public void UpdateBounds()
    {
        lowerXbound = cam.orthographicSize * ((float)Screen.width / Screen.height);
        higherXbound = (Map.instance.passabilityMap.GetLength(0)) - cam.orthographicSize * ((float)Screen.width / Screen.height);
        lowerYbound = cam.orthographicSize;
        higherYbound = (Map.instance.passabilityMap.GetLength(1)) - cam.orthographicSize;
    }


    // Update is called once per frame
    void Update()
    {
        float scrollDelta;
        if (Input.mouseScrollDelta.y > 0 || (previousScrollDelta > 0 && !(Input.mouseScrollDelta.y < 0)))
        {
            scrollDelta = Mathf.Max(Input.mouseScrollDelta.y, previousScrollDelta * 5.0f * (0.2f - Time.deltaTime) - 0.05f);
        }
        else
        {
            scrollDelta = Mathf.Min(Input.mouseScrollDelta.y, previousScrollDelta * 5.0f * (0.2f - Time.deltaTime) + 0.05f);
        }
        previousScrollDelta = scrollDelta;
        if ((cam.orthographicSize < 100 && cam.orthographicSize > 8) || (cam.orthographicSize >= 100 && scrollDelta > 0.0) || (cam.orthographicSize <= 8 && scrollDelta < 0.0))
        {
            cam.orthographicSize -= (cam.orthographicSize + 10) * scrollDelta * Time.deltaTime;
        }
        UpdateBounds(); //the camera bounds may have changed because of zooming

        Vector2 mousePos = (Input.mousePosition / new Vector2(Screen.width, Screen.height));
        //Debug.Log(mousePos.y);
        if ((movementX >= -stoppingSpeed) && (mousePos.x > 0.998f))
        {
            movementX += 2 * movementSpeed * Time.deltaTime + movementX * Time.deltaTime;
        }
        else if ((movementX <= stoppingSpeed) && (mousePos.x < 0.002f))
        {
            movementX -= 2 * movementSpeed * Time.deltaTime - movementX * Time.deltaTime;
        }
        else
        {
            movementX = movementX * 2 * (0.5f - Time.deltaTime);
            if (movementX < 0.0)
            {
                movementX = Mathf.Clamp(movementX + stoppingSpeed, -movementSpeed, 0.0f);
            }
            else
            {
                movementX = Mathf.Clamp(movementX - stoppingSpeed, 0.0f, movementSpeed);
            }
        }

        if ((movementY >= -stoppingSpeed) && (mousePos.y > 0.998f))
        {
            movementY += 2 * movementSpeed * Time.deltaTime + movementY * Time.deltaTime;
        }
        else if ((movementY <= stoppingSpeed) && (mousePos.y < 0.002f))
        {
            movementY -= 2 * movementSpeed * Time.deltaTime - movementY * Time.deltaTime;
        }
        else
        {
            movementY = movementY * 2 * (0.5f - Time.deltaTime);
            if (movementY < 0.0)
            {
                movementY = Mathf.Clamp(movementY + stoppingSpeed, -movementSpeed, 0.0f);
            }
            else
            {
                movementY = Mathf.Clamp(movementY - stoppingSpeed, 0.0f, movementSpeed);
            }
        }

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            movementX = -movementSpeed;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            movementX = movementSpeed;
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            movementY = movementSpeed;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            movementY = -movementSpeed;
        }


        movementX = Mathf.Clamp(movementX, -movementSpeed, movementSpeed);
        movementY = Mathf.Clamp(movementY, -movementSpeed, movementSpeed);
        transform.position = transform.position + (new Vector3(movementX, movementY, 0)) * Time.deltaTime;
        if (transform.position.x > higherXbound)
        {
            transform.position = new Vector3(higherXbound, transform.position.y, transform.position.z);
            movementX = 0;
        }
        if (transform.position.x < lowerXbound)
        {
            transform.position = new Vector3(lowerXbound, transform.position.y, transform.position.z);
            movementX = 0;
        }

        if (transform.position.y > higherYbound)
        {
            transform.position = new Vector3(transform.position.x, higherYbound, transform.position.z);
            movementY = 0;
        }
        if (transform.position.y < lowerYbound)
        {
            transform.position = new Vector3(transform.position.x, lowerYbound, transform.position.z);
            movementY = 0;
        }
    }
}