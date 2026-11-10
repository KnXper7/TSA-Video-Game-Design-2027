using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TSAGame
{
    /// <summary>Hover and focus effect for the main menu buttons: the accent bar grows, the label slides and brightens.</summary>
    public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public RectTransform accent;
        public RectTransform label;
        public Image fill;
        public Text text;
        public Text chevron;

        bool hovered, focused;
        float amount;
        float labelX;

        void Start() => labelX = label.anchoredPosition.x;

        public void OnPointerEnter(PointerEventData e) { hovered = true; AudioManager.Play(Sfx.Tick, 0.5f); }
        public void OnPointerExit(PointerEventData e) => hovered = false;
        public void OnSelect(BaseEventData e) => focused = true;
        public void OnDeselect(BaseEventData e) => focused = false;

        void Update()
        {
            amount = Mathf.MoveTowards(amount, hovered || focused ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            float k = Mathf.SmoothStep(0f, 1f, amount);
            accent.sizeDelta = new Vector2(Mathf.Lerp(4f, 14f, k), accent.sizeDelta.y);
            label.anchoredPosition = new Vector2(labelX + 14f * k, label.anchoredPosition.y);
            fill.color = Color.Lerp(new Color(0.04f, 0.07f, 0.11f, 0.72f), new Color(0.1f, 0.22f, 0.34f, 0.92f), k);
            text.color = Color.Lerp(new Color(0.82f, 0.88f, 0.95f), Color.white, k);
            chevron.color = new Color(0.55f, 0.85f, 1f, k);
        }
    }
}
