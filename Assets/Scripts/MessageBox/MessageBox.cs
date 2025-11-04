using System;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace.MessageBox
{
    public class MessageBox : MonoBehaviour
    {
        [SerializeField] private Button _okButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _contentText;

        public Action OnOkClick;

        public void Setup(string title, string content, Action onOkClick = null)
        {
            _titleText.text = title;
            _contentText.text = content;
            OnOkClick = onOkClick;
            _okButton.onClick.AddListener(() => OnOkClick?.Invoke());
            transform.gameObject.SetActive(true);
        }

        private void Start()
        {
            _okButton.onClick.AddListener(() => transform.gameObject.SetActive(false));
            _cancelButton.onClick.AddListener(() => transform.gameObject.SetActive(false));
        }
    }
}