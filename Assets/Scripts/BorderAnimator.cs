using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BorderAnimator : MonoBehaviour
{
    [SerializeField] private Sprite dotSprite;          // optional, plain square if empty
    [SerializeField] private int dotCount = 60;
    [SerializeField] private float dotSize = 16f;
    [SerializeField] private float speed = 250f;        // canvas units per second
    [SerializeField] private Color colorA = Color.yellow;
    [SerializeField] private Color colorB = Color.cyan;

    private RectTransform rectTransform;
    private readonly List<RectTransform> dots = new List<RectTransform>();
    private float travelled;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        for (int i = 0; i < dotCount; i++)
        {
            GameObject go = new GameObject("BorderDot_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(dotSize, dotSize);

            Image img = go.GetComponent<Image>();
            img.sprite = dotSprite;
            img.color = (i % 2 == 0) ? colorA : colorB;
            img.raycastTarget = false;

            dots.Add(rt);
        }
    }

    void Update()
    {
        float w = rectTransform.rect.width;
        float h = rectTransform.rect.height;
        float perimeter = 2f * (w + h);
        if (perimeter <= 0f) return;

        travelled = (travelled + speed * Time.deltaTime) % perimeter;

        for (int i = 0; i < dots.Count; i++)
        {
            float d = (travelled + i * (perimeter / dots.Count)) % perimeter;
            dots[i].anchoredPosition = PerimeterPoint(d, w, h);
        }
    }

    // Walks clockwise around the rectangle, starting at the top-left corner.
    private Vector2 PerimeterPoint(float d, float w, float h)
    {
        float hw = w / 2f, hh = h / 2f;

        if (d < w) return new Vector2(-hw + d, hh);
        d -= w;
        if (d < h) return new Vector2(hw, hh - d);
        d -= h;
        if (d < w) return new Vector2(hw - d, -hh);
        d -= w;
        return new Vector2(-hw, -hh + d);
    }
}