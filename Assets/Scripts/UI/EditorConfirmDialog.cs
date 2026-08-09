using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EditorConfirmDialog : MonoBehaviour
{
    private TMP_Text messageText;
    private Action confirmed;

    public void Initialize(
        TMP_Text message,
        Button confirmButton,
        Button cancelButton)
    {
        messageText = message;
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
        gameObject.SetActive(false);
    }

    public void Show(string message, Action onConfirmed)
    {
        messageText.text = message;
        confirmed = onConfirmed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    private void Confirm()
    {
        Action action = confirmed;
        confirmed = null;
        gameObject.SetActive(false);
        action?.Invoke();
    }

    private void Cancel()
    {
        confirmed = null;
        gameObject.SetActive(false);
    }
}
