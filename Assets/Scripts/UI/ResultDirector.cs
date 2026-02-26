using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace StellarRhythm.UI
{
    /// <summary>
    /// リザルトシーンのコントローラ。
    /// ボタン 1 つで選曲シーンへ戻るだけのプレースホルダ実装。
    /// </summary>
    public class ResultDirector : MonoBehaviour
    {
        [SerializeField] Button _backButton;
        [SerializeField] string _songSelectSceneName = "SongSelectScene";

        void Start()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(OnBackClicked);
        }

        void OnBackClicked()
        {
            SceneManager.LoadScene(_songSelectSceneName);
        }
    }
}
