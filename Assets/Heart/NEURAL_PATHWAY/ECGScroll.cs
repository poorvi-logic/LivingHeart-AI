using UnityEngine;
using UnityEngine.UI;

public class ECGScroll : MonoBehaviour
{
    public RawImage ecgImage;
    public float scrollSpeed = 0.6f;

    void Update()
    {
        // Scroll the texture from right to left continuously
        ecgImage.uvRect = new Rect(Time.time * scrollSpeed, 0, 1, 1);
    }

    public void SetWave(Texture texture, float speed)
    {
        ecgImage.texture = texture;
        scrollSpeed = speed;
    }
}