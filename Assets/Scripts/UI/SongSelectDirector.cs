using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StellarRhythm.Core;

namespace StellarRhythm.UI
{
    /// <summary>
    /// 選曲シーンのコントローラ。
    /// _songs[i] に対応する _songButtons[i] がクリックされると
    /// GameContext に選曲データをセットして GameScene へ遷移する。
    /// </summary>
    public class SongSelectDirector : MonoBehaviour
    {
        [Header("Song List")]
        [Tooltip("選択可能な SongEntry アセットの配列")]
        [SerializeField] SongEntry[] _songs;

        [Tooltip("_songs[i] と 1:1 対応するボタン")]
        [SerializeField] Button[] _songButtons;

        [Header("Scene Names")]
        [SerializeField] string _gameSceneName = "GameScene";

        // ================================================================

        void Start()
        {
            int count = Mathf.Min(_songs.Length, _songButtons.Length);
            for (int i = 0; i < count; i++)
            {
                int idx = i; // ラムダキャプチャ用
                _songButtons[i].onClick.AddListener(() => OnSongSelected(idx));
            }
        }

        void OnSongSelected(int index)
        {
            if (index < 0 || index >= _songs.Length || _songs[index] == null)
            {
                Debug.LogWarning($"[SongSelectDirector] songs[{index}] が未設定です。");
                return;
            }

            GameContext.SelectedSong = _songs[index];
            SceneManager.LoadScene(_gameSceneName);
        }
    }
}
