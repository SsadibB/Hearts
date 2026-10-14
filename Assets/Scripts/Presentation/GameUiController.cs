using Hearts.Gameplay;
using Hearts.Networking;
using Hearts.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace Hearts.Presentation
{
    public class GameUiController : MonoBehaviour
    {
        [SerializeField] CanvasGroup mainMenu;
        [SerializeField] CanvasGroup gameTable;
        [SerializeField] CanvasGroup passingPanel;
        [SerializeField] CanvasGroup leaderboardPanel;
        [SerializeField] CanvasGroup gameOverPanel;
        [SerializeField] CanvasGroup settingsPanel;
        [SerializeField] CardAnimationController animations;
        [SerializeField] HeartsSession session;
        [SerializeField] SinglePlayerMatch singlePlayer;
        [SerializeField] CanvasGroup multiplayerPanel;
        [SerializeField] InputField nameInput;
        [SerializeField] InputField roomInput;
        [SerializeField] Text menuStatus;
        [SerializeField] Text gameOverText;
        [SerializeField] Button passButton;
        [SerializeField] Button nextRoundButton;
        [SerializeField] Button hostButton;
        [SerializeField] Button joinButton;
        [SerializeField] Button singlePlayerButton;
        [SerializeField] Button multiplayerButton;
        [SerializeField] Button multiplayerBackButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button closeSettingsButton;
        [SerializeField] Button leaveButton;
        [SerializeField] Button avatarButton;
        [SerializeField] Image avatarPreview;
        [SerializeField] Sprite[] avatars;
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider sfxSlider;

        PlayerSettingsData _settings;
        bool _tableShown;
        bool _gameFullscreen;

        public Button PassButton => passButton;
        public Button NextRoundButton => nextRoundButton;

        void Awake()
        {
            _settings = SaveManager.Load();
            if (nameInput != null)
                nameInput.text = _settings.DisplayName;
            if (roomInput != null && string.IsNullOrEmpty(roomInput.text))
                roomInput.text = "Hearts";
            if (masterSlider != null)
                masterSlider.value = _settings.MasterVolume;
            if (sfxSlider != null)
                sfxSlider.value = _settings.SfxVolume;
            ShowAvatar();
            PlacePassArrow();
            SetGroup(mainMenu, true);
            SetGroup(gameTable, false);
            SetGroup(passingPanel, false);
            SetGroup(leaderboardPanel, false);
            SetGroup(gameOverPanel, false);
            SetGroup(settingsPanel, false);
            SetGroup(multiplayerPanel, false);
            if (passButton != null)
                passButton.gameObject.SetActive(false);
            if (hostButton != null) hostButton.onClick.AddListener(HostRoom);
            if (joinButton != null) joinButton.onClick.AddListener(JoinRoom);
            if (singlePlayerButton != null) singlePlayerButton.onClick.AddListener(StartSinglePlayer);
            if (multiplayerButton != null) multiplayerButton.onClick.AddListener(OpenMultiplayer);
            if (multiplayerBackButton != null) multiplayerBackButton.onClick.AddListener(CloseMultiplayer);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);
            if (leaveButton != null) leaveButton.onClick.AddListener(LeaveRoom);
            if (avatarButton != null) avatarButton.onClick.AddListener(NextAvatar);
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(_ => OnVolumeChanged());
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(_ => OnVolumeChanged());
        }

        void Update()
        {
            if (menuStatus != null && session != null && mainMenu != null && mainMenu.gameObject.activeSelf)
                menuStatus.text = session.LastMessage;
        }

        public void StartSinglePlayer()
        {
            if (session != null && session.IsRunning)
            {
                if (menuStatus != null)
                    menuStatus.text = "Leave the online room first.";
                return;
            }
            if (singlePlayer == null)
                return;
            StoreProfile();
            EnterGameDisplay();
            singlePlayer.Begin(_settings.DisplayName, _settings.AvatarIndex);
        }

        public void OpenMultiplayer() => SetGroup(multiplayerPanel, true);

        public void CloseMultiplayer() => SetGroup(multiplayerPanel, false);

        public void HostRoom()
        {
            StoreProfile();
            EnterGameDisplay();
            session.StartHost(roomInput != null ? roomInput.text : "Hearts");
        }

        public void JoinRoom()
        {
            StoreProfile();
            EnterGameDisplay();
            session.StartClient(roomInput != null ? roomInput.text : "Hearts");
        }

        public void LeaveRoom() => ReturnToMenu();

        public void ReturnToMenu()
        {
            if (singlePlayer != null && singlePlayer.IsActive)
                singlePlayer.StopMatch();
            if (session != null)
                session.Leave();
            HideGameOver();
            ShowTable(false);
            SetGroup(multiplayerPanel, false);
            ExitGameDisplay();
        }

        public void OpenSettings() => SetGroup(settingsPanel, true);

        public void CloseSettings()
        {
            StoreProfile();
            SetGroup(settingsPanel, false);
        }

        public void NextAvatar()
        {
            _settings.AvatarIndex = (_settings.AvatarIndex + 1) % Mathf.Max(1, avatars.Length);
            ShowAvatar();
            SaveManager.Save(_settings);
            if (session != null)
                session.SendLocalProfile();
        }

        public void OnVolumeChanged()
        {
            if (masterSlider != null)
                _settings.MasterVolume = masterSlider.value;
            if (sfxSlider != null)
                _settings.SfxVolume = sfxSlider.value;
            SaveManager.Save(_settings);
        }

        public void ShowTable(bool show)
        {
            if (_tableShown == show)
                return;
            _tableShown = show;
            if (animations != null)
            {
                animations.Fade(mainMenu, show ? 0f : 1f, false, null);
                animations.Fade(gameTable, show ? 1f : 0f, true, null);
            }
            else
            {
                SetGroup(mainMenu, !show);
                SetGroup(gameTable, show);
            }
        }

        public void ShowPassing(bool show)
        {
            SetGroup(passingPanel, false);
            ShowPassArrow(show, false);
        }

        public void ShowPassArrow(bool show, bool ready)
        {
            if (passButton == null)
                return;
            passButton.gameObject.SetActive(show);
            passButton.interactable = show;
            var image = passButton.GetComponent<Image>();
            if (image != null)
                image.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            if (show)
                passButton.transform.SetAsLastSibling();
        }

        public void HideCards(RectTransform cards)
        {
            if (cards != null)
                cards.gameObject.SetActive(false);
        }

        public void ShowLeaderboard(bool show)
        {
            SetGroup(leaderboardPanel, show);
        }

        public void ShowGameOver(string message)
        {
            if (gameOverText != null)
                gameOverText.text = message;
            SetGroup(gameOverPanel, true);
        }

        public void HideGameOver()
        {
            SetGroup(gameOverPanel, false);
        }

        public Sprite Avatar(int index)
        {
            if (avatars == null || avatars.Length == 0)
                return null;
            return avatars[Mathf.Clamp(index, 0, avatars.Length - 1)];
        }

        void StoreProfile()
        {
            if (nameInput != null)
                _settings.DisplayName = nameInput.text;
            SaveManager.Save(_settings);
            if (session != null)
                session.SendLocalProfile();
        }

        void ShowAvatar()
        {
            if (avatarPreview != null)
                avatarPreview.sprite = Avatar(_settings.AvatarIndex);
        }

        void EnterGameDisplay()
        {
            if (_gameFullscreen)
                return;
            _gameFullscreen = true;
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }

        void ExitGameDisplay()
        {
            if (!_gameFullscreen)
                return;
            _gameFullscreen = false;
            Screen.fullScreen = false;
        }

        void PlacePassArrow()
        {
            if (passButton == null)
                return;
            var canvas = gameTable != null ? gameTable.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
                passButton.transform.SetParent(canvas.transform, false);
            var rect = passButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(128f, 128f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        static void SetGroup(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.gameObject.SetActive(visible);
        }
    }
}
