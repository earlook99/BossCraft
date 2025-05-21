using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using GameSystem;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace UI
{
    /// <summary>
    /// Represents a single button in the action menu UI.
    /// Handles its display and click events.
    /// </summary>
    public class ActionButtonUI : MonoBehaviour
    {
        [SerializeField] private Button _button; // Reference to the UI Button component
        [SerializeField] private TextMeshProUGUI _buttonText; // Reference to the TextMeshProUGUI for button text

        private int _actionIndex; // The index associated with this action (e.g., move index, item index)

        /// <summary>
        /// Event triggered when this button is clicked. Passes the action index.
        /// </summary>
        public event Action<int> OnButtonClicked;

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Ensures button reference and subscribes to the click event.
        /// </summary>
        private void Awake()
        {
            // If not connected in Inspector, find with GetComponent
            if (_button is null)
            {
                _button = GetComponent<Button>();
            }
                
            if (_button != null) // Ensure button is not null before adding listener
            {
                _button.onClick.AddListener(HandleClick);
            }
        }

        /// <summary>
        /// Sets up the button with display text and an action index.
        /// </summary>
        /// <param name="text">The text to display on the button.</param>
        /// <param name="actionIndex">The index associated with this button's action.</param>
        public void Setup(string text, int actionIndex)
        {
            if (_buttonText is not null)
            {
                _buttonText.text = text;
            }
            
            _actionIndex = actionIndex;
        }

        /// <summary>
        /// Handles the button click event, invoking the <see cref="OnButtonClicked"/> event.
        /// </summary>
        private void HandleClick()
        {
            OnButtonClicked?.Invoke(_actionIndex);
        }

        /// <summary>
        /// Called when the MonoBehaviour will be destroyed.
        /// Unsubscribes from the button click event to prevent memory leaks.
        /// </summary>
        private void OnDestroy()
        {
            // Unsubscribe event
            if (_button is not null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }
    }
}