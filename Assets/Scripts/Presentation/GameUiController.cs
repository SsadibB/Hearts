using System;
using DG.Tweening;
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

        CanvasGroup _leavePrompt;
        CanvasGroup _leaderboardBackdrop;
        bool _leaderboardOpen;

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
            if (leaveButton != null) leaveButton.onClick.AddListener(AskLeave);
            EnsureLeaderboardButton();
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

        public void AskLeave()
        {
            bool inMatch = (singlePlayer != null && singlePlayer.IsActive) || (session != null && session.IsRunning);
            if (!inMatch)
            {
                ReturnToMenu();
                return;
            }
            EnsureLeavePrompt();
            if (_leavePrompt == null)
            {
                ReturnToMenu();
                return;
            }
            _leavePrompt.gameObject.SetActive(true);
            _leavePrompt.transform.SetAsLastSibling();
            _leavePrompt.alpha = 1f;
            _leavePrompt.interactable = true;
            _leavePrompt.blocksRaycasts = true;
        }

        public void ReturnToMenu()
        {
            HideLeavePrompt();
            CloseLeaderboard();
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
            if (show)
            {
                RectTransform canvas = CanvasRect();
                if (canvas != null && passButton.transform.parent != canvas)
                    passButton.transform.SetParent(canvas, false);
                passButton.transform.SetAsLastSibling();
            }
            passButton.gameObject.SetActive(show);
            passButton.interactable = show;
            var image = passButton.GetComponent<Image>();
            if (image != null)
                image.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        public void HideCards(RectTransform cards)
        {
            if (cards != null)
                cards.gameObject.SetActive(false);
        }

        public void ShowLeaderboard(bool show)
        {
            if (show)
                OpenLeaderboard();
            else
                CloseLeaderboard();
        }

        public void OpenLeaderboard()
        {
            if (leaderboardPanel == null || _leaderboardOpen)
                return;
            EnsureLeaderboardBackdrop();
            _leaderboardOpen = true;
            if (_leaderboardBackdrop != null)
            {
                _leaderboardBackdrop.transform.SetAsLastSibling();
                _leaderboardBackdrop.gameObject.SetActive(true);
                _leaderboardBackdrop.alpha = 0f;
                _leaderboardBackdrop.interactable = true;
                _leaderboardBackdrop.blocksRaycasts = true;
            }
            leaderboardPanel.transform.SetAsLastSibling();
            leaderboardPanel.gameObject.SetActive(true);
            leaderboardPanel.alpha = 0f;
            leaderboardPanel.interactable = true;
            leaderboardPanel.blocksRaycasts = true;
            leaderboardPanel.transform.localScale = Vector3.one * 0.96f;
            if (animations != null)
            {
                if (_leaderboardBackdrop != null)
                    animations.Fade(_leaderboardBackdrop, 1f, true, null);
                animations.Fade(leaderboardPanel, 1f, true, null);
            }
            else
            {
                if (_leaderboardBackdrop != null)
                    SetGroup(_leaderboardBackdrop, true);
                SetGroup(leaderboardPanel, true);
            }
            leaderboardPanel.transform.DOKill(false);
            leaderboardPanel.transform.DOScale(1f, 0.28f).SetEase(Ease.OutCubic);
        }

        public void CloseLeaderboard()
        {
            CloseLeaderboard(null);
        }

        public void CloseLeaderboard(Action onClosed)
        {
            if (leaderboardPanel == null || (!_leaderboardOpen && !leaderboardPanel.gameObject.activeSelf))
            {
                onClosed?.Invoke();
                return;
            }
            _leaderboardOpen = false;
            leaderboardPanel.transform.DOKill(false);
            leaderboardPanel.transform.DOScale(0.96f, 0.28f).SetEase(Ease.InCubic);
            if (animations != null)
            {
                if (_leaderboardBackdrop != null)
                    animations.Fade(_leaderboardBackdrop, 0f, false, null);
                animations.Fade(leaderboardPanel, 0f, false, () =>
                {
                    if (!_leaderboardOpen && leaderboardPanel != null)
                        leaderboardPanel.transform.localScale = Vector3.one;
                    onClosed?.Invoke();
                });
                return;
            }
            if (_leaderboardBackdrop != null)
                SetGroup(_leaderboardBackdrop, false);
            SetGroup(leaderboardPanel, false);
            leaderboardPanel.transform.localScale = Vector3.one;
            onClosed?.Invoke();
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

        RectTransform CanvasRect()
        {
            if (gameTable != null)
                return gameTable.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (mainMenu != null)
                return mainMenu.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            return null;
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

        void EnsureLeaderboardButton()
        {
            if (gameTable == null)
                return;
            var existing = gameTable.transform.Find("LeaderboardButton");
            Button button;
            if (existing != null)
            {
                button = existing.GetComponent<Button>();
            }
            else
            {
                Font font = null;
                if (leaveButton != null)
                {
                    var sample = leaveButton.GetComponentInChildren<Text>();
                    if (sample != null)
                        font = sample.font;
                }
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                var go = new GameObject("LeaderboardButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(gameTable.transform, false);
                var rect = go.GetComponent<RectTransform>();
                var leaveRect = leaveButton != null ? leaveButton.GetComponent<RectTransform>() : null;
                if (leaveRect != null)
                {
                    rect.anchorMin = leaveRect.anchorMin;
                    rect.anchorMax = leaveRect.anchorMax;
                    rect.pivot = leaveRect.pivot;
                    rect.sizeDelta = new Vector2(168f, leaveRect.sizeDelta.y);
                    rect.anchoredPosition = leaveRect.anchoredPosition - new Vector2(leaveRect.sizeDelta.x * 0.5f + 96f, 0f);
                }
                else
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    rect.anchoredPosition = new Vector2(-180f, -16f);
                    rect.sizeDelta = new Vector2(168f, 44f);
                }
                go.GetComponent<Image>().color = new Color(0.2f, 0.36f, 0.52f, 1f);
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(go.transform, false);
                var labelRect = labelGo.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                var label = labelGo.GetComponent<Text>();
                label.font = font;
                label.text = "Leaderboard";
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.fontSize = 18;
                label.raycastTarget = false;
                button = go.GetComponent<Button>();
            }
            if (button != null)
                button.onClick.AddListener(OpenLeaderboard);
        }

        void EnsureLeaderboardBackdrop()
        {
            if (_leaderboardBackdrop != null || leaderboardPanel == null)
                return;
            RectTransform canvas = CanvasRect();
            if (canvas == null)
                return;
            var go = new GameObject("LeaderboardBackdrop", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            go.transform.SetParent(canvas, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            image.raycastTarget = true;
            go.GetComponent<Button>().onClick.AddListener(CloseLeaderboard);
            _leaderboardBackdrop = go.GetComponent<CanvasGroup>();
            _leaderboardBackdrop.alpha = 0f;
            _leaderboardBackdrop.interactable = false;
            _leaderboardBackdrop.blocksRaycasts = false;
            go.SetActive(false);
        }

        void EnsureLeavePrompt()
        {
            if (_leavePrompt != null)
                return;
            RectTransform canvas = CanvasRect();
            if (canvas == null)
                return;

            Font font = null;
            if (leaveButton != null)
            {
                var sample = leaveButton.GetComponentInChildren<Text>();
                if (sample != null)
                    font = sample.font;
            }
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("LeavePrompt", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvas, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            _leavePrompt = root.GetComponent<CanvasGroup>();

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 260f);
            panel.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.98f);

            CreatePromptLabel(panel.transform, "Leave the current game?", font, new Vector2(0f, 58f), new Vector2(500f, 80f), 30);
            Button yes = CreatePromptButton(panel.transform, "Yes", font, new Vector2(-120f, -55f));
            Button no = CreatePromptButton(panel.transform, "No", font, new Vector2(120f, -55f));
            yes.onClick.AddListener(() =>
            {
                HideLeavePrompt();
                ReturnToMenu();
            });
            no.onClick.AddListener(HideLeavePrompt);
            _leavePrompt.gameObject.SetActive(false);
        }

        void HideLeavePrompt()
        {
            if (_leavePrompt == null)
                return;
            _leavePrompt.alpha = 0f;
            _leavePrompt.interactable = false;
            _leavePrompt.blocksRaycasts = false;
            _leavePrompt.gameObject.SetActive(false);
        }

        static void CreatePromptLabel(Transform parent, string message, Font font, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject("Message", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = message;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
        }

        static Button CreatePromptButton(Transform parent, string label, Font font, Vector2 position)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180f, 64f);
            go.GetComponent<Image>().color = new Color(0.85f, 0.88f, 0.92f, 1f);
            var button = go.GetComponent<Button>();
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.1f, 0.12f, 0.16f, 1f);
            text.fontSize = 26;
            text.raycastTarget = false;
            return button;
        }
    }
}
