using UnityEngine;
using UnityEngine.Events;

public class CreateAccountPopupUI : MonoBehaviour
{
    [Header("Document URLs")]
    public string termsUrl;
    public string personalDataUrl;

    [Header("Sign-in popup")]
    public GameObject signInPopup;

    [Header("Document events")]
    public UnityEvent onTermsRequested = new UnityEvent();
    public UnityEvent onPersonalDataRequested = new UnityEvent();

    [Header("Navigation")]
    public UnityEvent onBack = new UnityEvent();

    public void OpenTerms()
    {
        if (!string.IsNullOrWhiteSpace(termsUrl))
            Application.OpenURL(termsUrl);
        else
            onTermsRequested.Invoke();
    }

    public void OpenPersonalData()
    {
        if (!string.IsNullOrWhiteSpace(personalDataUrl))
            Application.OpenURL(personalDataUrl);
        else
            onPersonalDataRequested.Invoke();
    }

    public void Back()
    {
        gameObject.SetActive(false);

        if (signInPopup)
            signInPopup.SetActive(true);

        onBack.Invoke();
    }

    public void ClosePopup()
    {
        gameObject.SetActive(false);
    }
}