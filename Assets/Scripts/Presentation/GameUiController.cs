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

        PlayerSettingsData _settings;
        bool _tableShown;
        bool _gameFullscreen;
        Vector2 _fittedCanvas;

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
            if (leaveButton != null) leaveButton.onClick.AddListener(AskLeave);
            if (avatarButton != null) avatarButton.onClick.AddListener(NextAvatar);
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(_ => OnVolumeChanged());
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(_ => OnVolumeChanged());
        }

        void Update()
        {
            if (menuStatus != null && session != null && mainMenu != null && mainMenu.gameObject.activeSelf)
                menuStatus.text = session.LastMessage;
            FitChrome();
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

        void FitChrome()
        {
            RectTransform canvas = CanvasRect();
            if (canvas == null)
                return;
            Canvas.ForceUpdateCanvases();
            float width = canvas.rect.width;
            float height = canvas.rect.height;
            if (width < 80f || height < 80f)
                return;
            if (Mathf.Abs(_fittedCanvas.x - width) < 0.5f && Mathf.Abs(_fittedCanvas.y - height) < 0.5f)
                return;
            _fittedCanvas = new Vector2(width, height);

            ReadInsets(width, height, out float left, out float right, out float bottom, out float top);
            TableMetrics table = TableLayout.Compute(width, height, left, right, bottom, top);
            MenuMetrics menu = TableLayout.ComputeMenu(width, height, left, right, bottom, top);

            Place(passButton != null ? passButton.GetComponent<RectTransform>() : null, table.passCenter, table.passSize);
            Place(leaveButton != null ? leaveButton.GetComponent<RectTransform>() : null, table.leaveCenter, table.leaveSize);
            Place(FindRect(mainMenu, "Title"), menu.titleCenter, menu.titleSize);
            Place(avatarButton != null ? avatarButton.GetComponent<RectTransform>() : null, menu.avatarCenter, menu.avatarSize);
            Place(nameInput != null ? nameInput.GetComponent<RectTransform>() : null, menu.nameCenter, menu.nameSize);
            Place(singlePlayerButton != null ? singlePlayerButton.GetComponent<RectTransform>() : null, menu.singleCenter, menu.singleSize);
            Place(multiplayerButton != null ? multiplayerButton.GetComponent<RectTransform>() : null, menu.multiCenter, menu.multiSize);
            Place(settingsButton != null ? settingsButton.GetComponent<RectTransform>() : null, menu.settingsCenter, menu.settingsSize);
            Place(menuStatus != null ? menuStatus.rectTransform : null, menu.statusCenter, menu.statusSize);
            FitLabel(FindRect(mainMenu, "Title"), menu.titleFont, TextAnchor.MiddleCenter);
            FitChildren(singlePlayerButton != null ? singlePlayerButton.transform : null);
            FitChildren(multiplayerButton != null ? multiplayerButton.transform : null);
            FitChildren(settingsButton != null ? settingsButton.transform : null);
            FitChildren(nameInput != null ? nameInput.transform : null);

            FitPopup(multiplayerPanel, 640f, 420f, false);
            FitPopup(settingsPanel, 560f, 360f, false);
            FitPopup(leaderboardPanel, 760f, 520f, false);
            FitPopup(gameOverPanel, 900f, 120f, true);
            FitPopup(passingPanel, 220f, 180f, false);
        }

        RectTransform CanvasRect()
        {
            if (gameTable != null)
                return gameTable.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (mainMenu != null)
                return mainMenu.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            return null;
        }

        static void ReadInsets(float width, float height, out float left, out float right, out float bottom, out float top)
        {
            left = right = bottom = top = 0f;
            if (Screen.width < 8 || Screen.height < 8)
                return;
            Rect safe = Screen.safeArea;
            float scaleX = width / Screen.width;
            float scaleY = height / Screen.height;
            left = safe.xMin * scaleX;
            right = (Screen.width - safe.xMax) * scaleX;
            bottom = safe.yMin * scaleY;
            top = (Screen.height - safe.yMax) * scaleY;
        }

        static RectTransform FindRect(CanvasGroup group, string childName)
        {
            if (group == null)
                return null;
            Transform child = group.transform.Find(childName);
            return child as RectTransform;
        }

        static void Place(RectTransform rect, Vector2 center, Vector2 size)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        static void FitLabel(RectTransform rect, int fontSize, TextAnchor alignment)
        {
            if (rect == null)
                return;
            var text = rect.GetComponent<Text>();
            if (text == null)
                return;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
        }

        static void FitChildren(Transform root)
        {
            if (root == null)
                return;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null)
                    continue;
                if (child.name != "Label" && child.name != "Text" && child.name != "Placeholder")
                    continue;
                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.offsetMin = new Vector2(12f, 4f);
                child.offsetMax = new Vector2(-12f, -4f);
                var text = child.GetComponent<Text>();
                if (text == null)
                    continue;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 12;
                text.resizeTextMaxSize = 22;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        static void FitPopup(CanvasGroup group, float preferredWidth, float preferredHeight, bool pinTop)
        {
            if (group == null)
                return;
            var rect = group.GetComponent<RectTransform>();
            var canvas = group.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            float width = canvasRect != null ? canvasRect.rect.width : preferredWidth;
            float height = canvasRect != null ? canvasRect.rect.height : preferredHeight;
            float scale = TableLayout.PopupScale(width, height, preferredWidth, preferredHeight);
            rect.sizeDelta = new Vector2(preferredWidth, preferredHeight);
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.localRotation = Quaternion.identity;
            if (pinTop)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -12f);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
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
