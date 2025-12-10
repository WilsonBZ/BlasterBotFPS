using UnityEngine;
using UnityEngine.UI;

public class UIInfiniteScroll : MonoBehaviour
{
    public RawImage image;
    public float scrollSpeedX = 0.1f;
    public float scrollSpeedY = 0f;

    private void Update()
    {
        Rect rect = image.uvRect;
        rect.x += scrollSpeedX * Time.deltaTime;
        rect.y += scrollSpeedY * Time.deltaTime;
        image.uvRect = rect;
    }
}
