using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CounterController : MonoBehaviour
{
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Button incrementButton;
    [SerializeField] private Button decrementButton;

    private int count;

    private void OnEnable()
    {
        incrementButton.onClick.AddListener(Increment);
        decrementButton.onClick.AddListener(Decrement);
        UpdateText();
    }

    private void OnDisable()
    {
        incrementButton.onClick.RemoveListener(Increment);
        decrementButton.onClick.RemoveListener(Decrement);
    }

    private void Increment()
    {
        count++;
        UpdateText();
    }

    private void Decrement()
    {
        count--;
        UpdateText();
    }

    private void UpdateText()
    {
        counterText.text = count.ToString();
    }
}