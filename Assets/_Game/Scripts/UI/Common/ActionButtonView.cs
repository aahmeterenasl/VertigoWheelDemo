using System;
using UnityEngine;

namespace VertigoWheel.UI.Common
{
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class ActionButtonView : MonoBehaviour
    {
        [SerializeField]
        private UnityEngine.UI.Button _button;

        public event Action Clicked;

        public bool IsInteractable => _button != null && _button.IsInteractable();

        public void SetInteractable(bool interactable)
        {
            CacheButton();
            _button.interactable = interactable;
        }

        private void Awake() => CacheButton();

        private void OnEnable()
        {
            CacheButton();
            _button.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        private void OnValidate() => CacheButton();

        private void CacheButton()
        {
            if (_button == null)
            {
                _button = GetComponent<UnityEngine.UI.Button>();
            }
        }

        private void HandleClick()
        {
            if (isActiveAndEnabled && _button.IsInteractable())
            {
                Clicked?.Invoke();
            }
        }
    }
}
