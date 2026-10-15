using Hearts.Core;
using Hearts.Gameplay;
using Hearts.Networking;
using Hearts.Presentation;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hearts.EditorTools
{
    public static class HeartsSceneBuilder
    {
        [MenuItem("Hearts/Build Game Scene")]
        public static void Build()
        {
            var existing = GameObject.Find("HeartsRoot");
            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject("HeartsRoot");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var network = new GameObject("NetworkManager");
            network.transform.SetParent(root.transform, false);
            var runner = network.AddComponent<NetworkRunner>();
            var sceneManager = network.AddComponent<NetworkSceneManagerDefault>();
            var session = network.AddComponent<HeartsSession>();

            var matchGo = new GameObject("Match");
            matchGo.transform.SetParent(root.transform, false);
            matchGo.AddComponent<NetworkObject>();
            var match = matchGo.AddComponent<NetworkHeartsMatch>();

            var seats = new NetworkHeartsPlayer[4];
            for (int i = 0; i < 4; i++)
            {
                var seatGo = new GameObject("Seat" + i);
                seatGo.transform.SetParent(root.transform, false);
                seatGo.AddComponent<NetworkObject>();
                seats[i] = seatGo.AddComponent<NetworkHeartsPlayer>();
                SetInt(seats[i], "seatIndex", i);
            }

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(root.transform, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var mainMenu = Panel(canvasGo.transform, "MainMenuPanel", Vector2.zero, Vector2.one, new Color(0.05f, 0.12f, 0.1f, 0.96f));
            Label(mainMenu, "Title", "Hearts", 64, TextAnchor.UpperCenter, font, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(600, 80));
            var nameInput = Input(mainMenu, "NameInput", "Player", font, new Vector2(0, 90));
            var singleButton = Button(mainMenu, "SinglePlayerButton", "Single Player", font, new Vector2(0, -20), new Vector2(420, 64));
            var multiButton = Button(mainMenu, "MultiplayerButton", "Multiplayer", font, new Vector2(0, -110), new Vector2(420, 64));
            var settingsButton = Button(mainMenu, "SettingsButton", "Settings", font, new Vector2(0, -200), new Vector2(420, 56));
            var avatarButton = Button(mainMenu, "AvatarButton", "Avatar", font, new Vector2(0, 200), new Vector2(96, 96));
            var avatarImage = avatarButton.GetComponent<Image>();
            var menuStatus = Label(mainMenu, "Status", "Choose a game.", 22, TextAnchor.MiddleCenter, font, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(800, 40));

            var multiplayer = Panel(mainMenu, "MultiplayerPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.06f, 0.1f, 0.12f, 0.98f));
            multiplayer.sizeDelta = new Vector2(640, 420);
            Label(multiplayer, "Title", "Multiplayer", 36, TextAnchor.UpperCenter, font, new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(400, 50));
            var roomInput = Input(multiplayer, "RoomInput", "Hearts", font, new Vector2(0, 40));
            var hostButton = Button(multiplayer, "HostButton", "Host", font, new Vector2(-120, -50), new Vector2(200, 56));
            var joinButton = Button(multiplayer, "JoinButton", "Join", font, new Vector2(120, -50), new Vector2(200, 56));
            var multiBack = Button(multiplayer, "BackButton", "Back", font, new Vector2(0, -140), new Vector2(200, 48));
            multiplayer.GetComponent<CanvasGroup>().alpha = 0f;
            multiplayer.gameObject.SetActive(false);

            var gameTable = Panel(canvasGo.transform, "GameTable", Vector2.zero, Vector2.one, new Color(0.04f, 0.28f, 0.16f, 1f));
            var background = Create("TableBackground", gameTable, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            background.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.22f, 0.12f, 1f);
            var trick = Create("CenterTrickArea", gameTable, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(460, 280));
            var top = Hud(gameTable, "PlayerArea_Top", font, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(460, 130));
            var left = Hud(gameTable, "PlayerArea_Left", font, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16, 20), new Vector2(300, 150));
            var right = Hud(gameTable, "PlayerArea_Right", font, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16, 20), new Vector2(300, 150));
            var bottom = Hud(gameTable, "PlayerArea_Bottom", font, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 230), new Vector2(520, 120));
            var hand = Create("LocalPlayerHand", gameTable, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(1400, 190));
            var deck = Create("DeckPoint", gameTable, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(20, 20));
            var tableStatus = Label(gameTable, "TableStatus", "", 24, TextAnchor.MiddleCenter, font, new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(700, 40));
            var leave = Button(gameTable, "LeaveButton", "Leave", font, new Vector2(-16, -16), new Vector2(140, 44));
            var leaveRect = leave.GetComponent<RectTransform>();
            leaveRect.anchorMin = new Vector2(1f, 1f);
            leaveRect.anchorMax = new Vector2(1f, 1f);
            leaveRect.pivot = new Vector2(1f, 1f);
            leaveRect.anchoredPosition = new Vector2(-16f, -16f);

            var passing = Panel(canvasGo.transform, "CardPassingPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Color(0f, 0f, 0f, 0.35f));
            var passingRect = passing.GetComponent<RectTransform>();
            passingRect.anchorMin = new Vector2(1f, 0f);
            passingRect.anchorMax = new Vector2(1f, 0f);
            passingRect.pivot = new Vector2(1f, 0f);
            passingRect.anchoredPosition = new Vector2(-16f, 16f);
            passingRect.sizeDelta = new Vector2(220, 180);
            Label(passing, "PassHint", "Select 3 cards", 20, TextAnchor.UpperCenter, font, new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(200, 60));
            passing.gameObject.SetActive(false);

            var leaderboard = Panel(canvasGo.transform, "LeaderboardPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.05f, 0.08f, 0.1f, 0.94f));
            var boardRect = leaderboard.GetComponent<RectTransform>();
            boardRect.sizeDelta = new Vector2(760, 520);
            var boardTitle = Label(leaderboard, "Title", "Round scores", 36, TextAnchor.UpperCenter, font, new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(700, 50));
            var rowAvatars = new Image[4];
            var rowNames = new Text[4];
            var rowRound = new Text[4];
            var rowTotal = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                var row = Create("Row" + i, leaderboard, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90 - i * 80), new Vector2(700, 70));
                row.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
                var avatar = Create("Avatar", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(56, 56));
                rowAvatars[i] = avatar.gameObject.AddComponent<Image>();
                rowNames[i] = Label(row, "Name", "Player", 24, TextAnchor.MiddleLeft, font, new Vector2(0f, 0.5f), new Vector2(160, 0), new Vector2(220, 40));
                rowRound[i] = Label(row, "Round", "0", 24, TextAnchor.MiddleCenter, font, new Vector2(1f, 0.5f), new Vector2(-220, 0), new Vector2(100, 40));
                rowTotal[i] = Label(row, "Total", "0", 24, TextAnchor.MiddleCenter, font, new Vector2(1f, 0.5f), new Vector2(-80, 0), new Vector2(100, 40));
            }
            var nextRound = Button(leaderboard, "NextRoundButton", "Next round", font, new Vector2(0, -230), new Vector2(240, 52));
            var boardView = leaderboard.gameObject.AddComponent<LeaderboardView>();
            leaderboard.gameObject.SetActive(false);

            var gameOver = Panel(canvasGo.transform, "GameOverPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Color(0.25f, 0.08f, 0.08f, 0.95f));
            var overRect = gameOver.GetComponent<RectTransform>();
            overRect.pivot = new Vector2(0.5f, 1f);
            overRect.anchoredPosition = new Vector2(0, -20);
            overRect.sizeDelta = new Vector2(900, 120);
            gameOver.gameObject.SetActive(false);
            var overText = Label(gameOver, "Result", "Match over", 28, TextAnchor.MiddleCenter, font, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(860, 70));
            var menuButton = Button(gameOver, "MenuButton", "Menu", font, new Vector2(360, 0), new Vector2(140, 44));

            var settings = Panel(canvasGo.transform, "SettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.08f, 0.09f, 0.12f, 0.96f));
            settings.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 360);
            Label(settings, "Title", "Settings", 36, TextAnchor.UpperCenter, font, new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(400, 50));
            var master = Slider(settings, "MasterVolume", "Master", font, -20);
            var sfx = Slider(settings, "SfxVolume", "Effects", font, -90);
            var closeSettings = Button(settings, "CloseSettingsButton", "Close", font, new Vector2(0, -140), new Vector2(200, 48));
            settings.gameObject.SetActive(false);
            gameTable.gameObject.SetActive(false);

            var cardsRoot = Create("Cards", canvasGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var back = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cards/Card_Back.png");
            var cardViews = new CardView[52];
            for (int i = 0; i < 52; i++)
            {
                var card = CardId.FromIndex(i);
                var rect = Create("Card_" + i.ToString("00"), cardsRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 154));
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = back;
                image.preserveAspect = true;
                var view = rect.gameObject.AddComponent<CardView>();
                var face = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cards/" + CardId.SpriteFileName(card));
                SetRef(view, "faceImage", image);
                SetRef(view, "faceSprite", face);
                SetRef(view, "backSprite", back);
                SetInt(view, "cardIndex", i);
                cardViews[i] = view;
            }
            cardsRoot.gameObject.SetActive(false);
            var arrow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Arrow.png");
            var passButton = Button(canvasGo.transform, "PassButton", "", font, Vector2.zero, new Vector2(128, 128));
            passButton.GetComponent<Image>().sprite = arrow;
            passButton.GetComponent<Image>().color = Color.white;
            passButton.gameObject.SetActive(false);

            var avatars = LoadAvatars();
            if (avatars.Length > 0 && avatarImage != null)
                avatarImage.sprite = avatars[0];

            var animation = root.AddComponent<CardAnimationController>();
            var solo = root.AddComponent<SinglePlayerMatch>();
            var networkTable = root.AddComponent<NetworkTableSource>();
            var ui = root.AddComponent<GameUiController>();
            var presenter = root.AddComponent<HeartsTablePresenter>();
            var huds = new PlayerHudView[]
            {
                bottom.GetComponent<PlayerHudView>(),
                left.GetComponent<PlayerHudView>(),
                top.GetComponent<PlayerHudView>(),
                right.GetComponent<PlayerHudView>()
            };

            SetRef(session, "runner", runner);
            SetRef(session, "sceneManager", sceneManager);
            SetRef(session, "match", match);
            SetArray(session, "seats", seats);
            SetArray(match, "seats", seats);
            SetRef(networkTable, "match", match);
            SetRef(networkTable, "session", session);

            SetRef(ui, "mainMenu", mainMenu.GetComponent<CanvasGroup>());
            SetRef(ui, "gameTable", gameTable.GetComponent<CanvasGroup>());
            SetRef(ui, "passingPanel", passing.GetComponent<CanvasGroup>());
            SetRef(ui, "leaderboardPanel", leaderboard.GetComponent<CanvasGroup>());
            SetRef(ui, "gameOverPanel", gameOver.GetComponent<CanvasGroup>());
            SetRef(ui, "settingsPanel", settings.GetComponent<CanvasGroup>());
            SetRef(ui, "animations", animation);
            SetRef(ui, "session", session);
            SetRef(ui, "singlePlayer", solo);
            SetRef(ui, "multiplayerPanel", multiplayer.GetComponent<CanvasGroup>());
            SetRef(ui, "nameInput", nameInput);
            SetRef(ui, "roomInput", roomInput);
            SetRef(ui, "menuStatus", menuStatus);
            SetRef(ui, "gameOverText", overText);
            SetRef(ui, "passButton", passButton);
            SetRef(ui, "nextRoundButton", nextRound);
            SetRef(ui, "hostButton", hostButton);
            SetRef(ui, "joinButton", joinButton);
            SetRef(ui, "singlePlayerButton", singleButton);
            SetRef(ui, "multiplayerButton", multiButton);
            SetRef(ui, "multiplayerBackButton", multiBack);
            SetRef(ui, "settingsButton", settingsButton);
            SetRef(ui, "closeSettingsButton", closeSettings);
            SetRef(ui, "leaveButton", leave);
            SetRef(ui, "avatarButton", avatarButton);
            SetRef(ui, "avatarPreview", avatarImage);
            SetArray(ui, "avatars", avatars);
            SetRef(ui, "masterSlider", master);
            SetRef(ui, "sfxSlider", sfx);

            SetArray(boardView, "avatars", rowAvatars);
            SetArray(boardView, "names", rowNames);
            SetArray(boardView, "roundScores", rowRound);
            SetArray(boardView, "totalScores", rowTotal);
            SetRef(boardView, "titleText", boardTitle);

            SetArray(presenter, "cards", cardViews);
            SetArray(presenter, "screenHuds", huds);
            SetArray(presenter, "screenAreas", new RectTransform[] { bottom, left, top, right });
            SetRef(presenter, "handArea", hand);
            SetRef(presenter, "trickArea", trick);
            SetRef(presenter, "deckPoint", deck);
            SetRef(presenter, "cardsRoot", cardsRoot);
            SetRef(presenter, "animations", animation);
            SetRef(presenter, "singlePlayer", solo);
            SetRef(presenter, "networkTable", networkTable);
            SetRef(presenter, "ui", ui);
            SetRef(presenter, "leaderboard", boardView);
            SetRef(presenter, "tableStatus", tableStatus);

            menuButton.onClick.AddListener(ui.ReturnToMenu);
            var scene = root.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Hearts scene built with 52 cards in " + scene.path);
        }

        static Sprite[] LoadAvatars()
        {
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 1; i <= 9; i++)
                list.Add(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Avatars/" + i + ".png"));
            for (int i = 1; i <= 4; i++)
                list.Add(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Avatars/Avatar" + i + ".png"));
            return list.ToArray();
        }

        static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rect = Create(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.gameObject.AddComponent<Image>().color = color;
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            return rect;
        }

        static RectTransform Hud(Transform parent, string name, Font font, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = Create(name, parent, anchor, anchor, pivot, position, size);
            rect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);
            var avatar = Create("Avatar", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12, 0), new Vector2(72, 72));
            var avatarImage = avatar.gameObject.AddComponent<Image>();
            avatarImage.color = Color.white;
            var turn = Create("TurnIndicator", rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12, -12), new Vector2(18, 18));
            var turnImage = turn.gameObject.AddComponent<Image>();
            turnImage.color = new Color(1f, 1f, 1f, 0.25f);
            var nameText = Label(rect, "Name", "Waiting", 22, TextAnchor.MiddleLeft, font, new Vector2(0f, 1f), new Vector2(180, -28), new Vector2(220, 30));
            var cardsText = Label(rect, "Cards", "0 cards", 18, TextAnchor.MiddleLeft, font, new Vector2(0f, 0f), new Vector2(180, 36), new Vector2(160, 28));
            var statusText = Label(rect, "Status", "", 16, TextAnchor.MiddleLeft, font, new Vector2(0f, 0f), new Vector2(180, 12), new Vector2(180, 24));
            var hud = rect.gameObject.AddComponent<PlayerHudView>();
            SetRef(hud, "avatarImage", avatarImage);
            SetRef(hud, "nameText", nameText);
            SetRef(hud, "cardsText", cardsText);
            SetRef(hud, "statusText", statusText);
            SetRef(hud, "turnImage", turnImage);
            return rect;
        }

        static RectTransform Create(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        static Text Label(Transform parent, string name, string value, int size, TextAnchor anchor, Font font, Vector2 anchorPoint, Vector2 position, Vector2 box)
        {
            var rect = Create(name, parent, anchorPoint, anchorPoint, new Vector2(0.5f, 0.5f), position, box);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button Button(Transform parent, string name, string value, Font font, Vector2 position, Vector2 size)
        {
            var rect = Create(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.75f, 0.62f, 0.28f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            if (!string.IsNullOrEmpty(value))
                Label(rect, "Label", value, 22, TextAnchor.MiddleCenter, font, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return button;
        }

        static InputField Input(Transform parent, string name, string value, Font font, Vector2 position)
        {
            var rect = Create(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(420, 48));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.92f);
            var field = rect.gameObject.AddComponent<InputField>();
            var text = Label(rect, "Text", value, 22, TextAnchor.MiddleLeft, font, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 40));
            text.color = Color.black;
            var placeholder = Label(rect, "Placeholder", value, 22, TextAnchor.MiddleLeft, font, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 40));
            placeholder.color = new Color(0f, 0f, 0f, 0.35f);
            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = value;
            return field;
        }

        static Slider Slider(Transform parent, string name, string caption, Font font, float y)
        {
            Label(parent, name + "Label", caption, 20, TextAnchor.MiddleLeft, font, new Vector2(0.5f, 0.5f), new Vector2(-180, y), new Vector2(140, 30));
            var rect = Create(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, y), new Vector2(280, 30));
            var slider = rect.gameObject.AddComponent<Slider>();
            var background = Create("Background", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            background.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);
            var fillArea = Create("Fill Area", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20, 0));
            var fill = Create("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.75f, 0.62f, 0.28f, 1f);
            var handle = Create("Handle", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 30));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
