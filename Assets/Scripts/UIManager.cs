using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;

/// <summary>
/// Builds the entire UI programmatically (no manual scene setup needed)
/// and drives all visual updates cleanly with PrimeTween.
/// Updated for Portrait layout (1080x1920) and smooth animations.
/// </summary>
namespace MillionaireGame
{
    public class UIManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  PUBLIC REFERENCES
        // ══════════════════════════════════════════════
        public Canvas mainCanvas;
        private Image _bgImg;
        private Sprite _roundedSprite;

        // ── Language selection screen ──
        public GameObject languagePanel;
        public Button btnTurkish;
        public Button btnEnglish;
        public TextMeshProUGUI languageTitle; // Fixed type

        // ── Category selection screen ──
        public GameObject categoryPanel;
        public TextMeshProUGUI categoryTitle;
        public TextMeshProUGUI categorySubtitle;
        public List<Button> categoryButtons = new List<Button>();

        // ── Game screen ──
        public GameObject gamePanel;
        public TextMeshProUGUI questionNumberText;
        public TextMeshProUGUI questionText;
        public Button[] answerButtons = new Button[4];
        public TextMeshProUGUI[] answerLabels = new TextMeshProUGUI[4];
        public Image[] answerBackgrounds = new Image[4];

        // Lifeline buttons
        public Button btnFiftyFifty;
        public Button btnAskAudience;
        public Button btnPhoneFriend;
        public TextMeshProUGUI lblFiftyFifty;
        public TextMeshProUGUI lblAskAudience;
        public TextMeshProUGUI lblPhoneFriend;

        // Walk‑away
        public Button btnWalkAway;

        // Money ladder
        public GameObject ladderArea;
        public TextMeshProUGUI[] ladderLabels;
        public Image[] ladderBackgrounds;
        private RectTransform[] _ladderRowRTs;

        // ── Audience result panel ──
        public GameObject audiencePanel;
        public Slider[] audienceSliders = new Slider[4];
        public TextMeshProUGUI[] audiencePercentLabels = new TextMeshProUGUI[4];
        public TextMeshProUGUI[] audienceLetterLabels = new TextMeshProUGUI[4];
        public Button audienceCloseButton;

        // ── Phone a Friend panel ──
        public GameObject phonePanel;
        public TextMeshProUGUI phoneFriendText;
        public Button phoneCloseButton;

        // ── Result panel (win / lose / walk away) ──
        public GameObject resultPanel;
        public TextMeshProUGUI resultTitle;
        public TextMeshProUGUI resultMessage;
        public Button resultMenuButton;

        // ── Colors ──
        private readonly Color32 _bgDark       = new Color32(5, 5, 20, 255);
        private readonly Color32 _panelBg      = new Color32(15, 15, 60, 255); // Opaque
        private readonly Color32 _accentGold   = new Color32(255, 200, 50, 255);
        private readonly Color32 _btnNormal    = new Color32(25, 45, 100, 255);
        private readonly Color32 _btnHover     = new Color32(40, 70, 140, 255);
        private readonly Color32 _btnCorrect   = new Color32(40, 190, 70, 255);
        private readonly Color32 _btnWrong     = new Color32(210, 50, 50, 255);
        private readonly Color32 _btnDisabled  = new Color32(60, 60, 80, 255);
        private readonly Color32 _ladderNormal = new Color32(20, 30, 80, 255);
        private readonly Color32 _ladderActive = new Color32(255, 180, 0, 255);
        private readonly Color32 _ladderSafe   = new Color32(80, 180, 255, 255);
        private readonly Color32 _white        = new Color32(255, 255, 255, 255);
        private readonly Color32 _borderColor  = new Color32(100, 100, 250, 180);

        // ══════════════════════════════════════════════
        //  BUILD UI 
        // ══════════════════════════════════════════════
        public void BuildUI()
        {
            // ── Canvas ──
            GameObject canvasGO = new GameObject("MainCanvas");
            mainCanvas = canvasGO.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // PORTRAIT Update
            scaler.matchWidthOrHeight = 0f; // Match Width

            canvasGO.AddComponent<GraphicRaycaster>();

            // Background image on canvas
            _bgImg = canvasGO.AddComponent<Image>();
            _bgImg.color = _bgDark;

            // Add dynamic floating shapes background effect
            canvasGO.AddComponent<FloatingShapesEffect>();

            // Generate a rounded sprite at runtime for "border radius"
            _roundedSprite = CreateRoundedSprite(40, 15);

            // ── Event System ──
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // ── Build panels ──
            BuildLanguagePanel(canvasGO.transform);
            BuildCategoryPanel(canvasGO.transform);
            BuildGamePanel(canvasGO.transform);
            BuildAudiencePanel(canvasGO.transform);
            BuildPhonePanel(canvasGO.transform);
            BuildResultPanel(canvasGO.transform);

            // Hide all initially except language
            languagePanel.SetActive(true);
            categoryPanel.SetActive(false);
            gamePanel.SetActive(false);
            audiencePanel.SetActive(false);
            phonePanel.SetActive(false);
            resultPanel.SetActive(false);
            
            ShowPanel(languagePanel);
        }

        public void UpdateBackground(Sprite bg)
        {
            if (bg != null && _bgImg != null)
            {
                _bgImg.sprite = bg;
                // Fade effect
                Tween.Color(_bgImg, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 75f / 255f), 1f);
            }
        }

        // ─────────────────────────────────────────────
        //  ANIMATION HELPERS
        // ─────────────────────────────────────────────
        private void ShowPanel(GameObject panel)
        {
            panel.SetActive(true);
            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panel.AddComponent<CanvasGroup>();
            
            canvasGroup.alpha = 0f;
            Tween.Alpha(canvasGroup, 1f, 0.4f, Ease.OutQuad);
            
            var rt = panel.GetComponent<RectTransform>();
            rt.localScale = Vector3.one * 0.95f;
            Tween.Scale(rt, Vector3.one, 0.4f, Ease.OutBack);
        }

        private void HidePanel(GameObject panel)
        {
            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                Tween.Alpha(canvasGroup, 0f, 0.3f, Ease.InQuad).OnComplete(panel, p => p.SetActive(false));
            }
            else
            {
                panel.SetActive(false);
            }
        }

        private void AnimateButtonPress(Button btn)
        {
            var rt = btn.GetComponent<RectTransform>();
            Tween.Scale(rt, Vector3.one * 0.9f, 0.1f).OnComplete(rt, target => Tween.Scale(target, Vector3.one, 0.1f));
        }

        // ─────────────────────────────────────────────
        //  PANEL BUILDERS
        // ─────────────────────────────────────────────
        private void BuildLanguagePanel(Transform parent)
        {
            languagePanel = CreatePanel(parent, "LanguagePanel", Vector2.zero, new Vector2(900, 600));

            languageTitle = CreateTMP(languagePanel.transform, "LanguageTitle", "Select Language\nDil Seçin", 46, TextAlignmentOptions.Center, new Vector2(0, 150), new Vector2(800, 150));
            languageTitle.color = _accentGold;
            languageTitle.fontStyle = FontStyles.Bold;

            btnTurkish = CreateButton(languagePanel.transform, "BtnTurkish", "Türkçe", new Vector2(0, -30), new Vector2(500, 100), 38);
            btnEnglish = CreateButton(languagePanel.transform, "BtnEnglish", "English", new Vector2(0, -180), new Vector2(500, 100), 38);
        }

        private void BuildCategoryPanel(Transform parent)
        {
            categoryPanel = CreatePanel(parent, "CategoryPanel", Vector2.zero, new Vector2(950, 1600));

            categoryTitle = CreateTMP(categoryPanel.transform, "CategoryTitle", "Choose a Category", 48, TextAlignmentOptions.Center, new Vector2(0, 680), new Vector2(800, 80));
            categoryTitle.color = _accentGold;
            categoryTitle.fontStyle = FontStyles.Bold;

            categorySubtitle = CreateTMP(categoryPanel.transform, "Subtitle", "Religious Who Wants to Be a Millionaire?", 32, TextAlignmentOptions.Center, new Vector2(0, 600), new Vector2(800, 80));
            categorySubtitle.color = _white;
            categorySubtitle.fontStyle = FontStyles.Italic;
        }

        public void PopulateCategoryButtons(List<string> categories, System.Action<string> onClick)
        {
            foreach (var btn in categoryButtons) if (btn != null) Destroy(btn.gameObject);
            categoryButtons.Clear();

            float startY = 480f;
            float spacing = 130f;

            for (int i = 0; i < categories.Count; i++)
            {
                string cat = categories[i];
                float yPos = startY - i * spacing;

                var btn = CreateButton(categoryPanel.transform, $"CatBtn_{cat}", cat, new Vector2(0, yPos), new Vector2(700, 100), 34);
                btn.onClick.AddListener(() => {
                    AnimateButtonPress(btn);
                    onClick(cat);
                });
                categoryButtons.Add(btn);
            }
        }

        private void BuildGamePanel(Transform parent)
        {
            gamePanel = CreatePanel(parent, "GamePanel", Vector2.zero, new Vector2(1080, 1920));
            gamePanel.GetComponent<Image>().color = new Color(0, 0, 0, 0); // transparent container

            // ── Top: Lifeline buttons ──
            float lifeY = 820f;
            btnFiftyFifty = CreateButton(gamePanel.transform, "Btn5050", "50:50", new Vector2(-320, lifeY), new Vector2(280, 100), 36);
            lblFiftyFifty = btnFiftyFifty.GetComponentInChildren<TextMeshProUGUI>();

            btnAskAudience = CreateButton(gamePanel.transform, "BtnAudience", "Ask Aud", new Vector2(0, lifeY), new Vector2(300, 100), 36);
            lblAskAudience = btnAskAudience.GetComponentInChildren<TextMeshProUGUI>();

            btnPhoneFriend = CreateButton(gamePanel.transform, "BtnPhone", "Phone", new Vector2(320, lifeY), new Vector2(280, 100), 36);
            lblPhoneFriend = btnPhoneFriend.GetComponentInChildren<TextMeshProUGUI>();

            // ── Mid-Top: Money Ladder ──
            ladderArea = CreatePanel(gamePanel.transform, "LadderArea", new Vector2(0, 440), new Vector2(850, 700));
            ladderArea.GetComponent<Image>().color = new Color32(10, 10, 50, 255);

            var ladderTitle = CreateTMP(ladderArea.transform, "LadderTitle", "Prize Ladder", 32, TextAlignmentOptions.Center, new Vector2(0, 330), new Vector2(800, 45));
            ladderTitle.color = _accentGold;
            ladderTitle.fontStyle = FontStyles.Bold;

            int steps = MoneyLadder.TotalSteps;
            ladderLabels = new TextMeshProUGUI[steps];
            ladderBackgrounds = new Image[steps];
            _ladderRowRTs = new RectTransform[steps];

            for (int i = steps - 1; i >= 0; i--)
            {
                int displayRow = steps - 1 - i;
                float yPos = 265f - displayRow * 42f;

                var rowGO = new GameObject($"LadderRow_{i}");
                rowGO.transform.SetParent(ladderArea.transform, false);
                var rowRT = rowGO.AddComponent<RectTransform>();
                rowRT.anchoredPosition = new Vector2(0, yPos);
                rowRT.sizeDelta = new Vector2(800, 40);
                _ladderRowRTs[i] = rowRT;

                var rowImg = rowGO.AddComponent<Image>();
                rowImg.color = _ladderNormal;
                ladderBackgrounds[i] = rowImg;

                if (i == MoneyLadder.SafeHaven1 || i == MoneyLadder.SafeHaven2)
                    rowImg.color = _ladderSafe;

                string prefix = (i + 1).ToString().PadLeft(2) + ". ";
                ladderLabels[i] = CreateTMP(rowGO.transform, $"LadderLbl_{i}", prefix + MoneyLadder.PrizeLabels[i], 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(750, 40));
                ladderLabels[i].color = _white;
            }

            // ── Middle: Question Text ──
            questionNumberText = CreateTMP(gamePanel.transform, "QuestionNumber", "Question 1 / 15", 34, TextAlignmentOptions.Center, new Vector2(0, 40), new Vector2(800, 50));
            questionNumberText.color = _accentGold;

            var qBg = CreatePanel(gamePanel.transform, "QuestionBg", new Vector2(0, -110), new Vector2(980, 220));
            qBg.GetComponent<Image>().color = _panelBg;

            questionText = CreateTMP(qBg.transform, "QuestionText", "Question goes here?", 38, TextAlignmentOptions.Center, Vector2.zero, new Vector2(930, 180));
            questionText.color = _white;

            // ── Bottom: Answer buttons (1x4 vertically) ──
            string[] labels = { "A", "B", "C", "D" };
            for (int i = 0; i < 4; i++)
            {
                float aY = -310 - i * 115;
                var btn = CreateButton(gamePanel.transform, $"AnswerBtn_{labels[i]}", $"{labels[i]}: Answer", new Vector2(0, aY), new Vector2(920, 100), 34);
                
                var btnRT = btn.GetComponent<RectTransform>();
                answerBackgrounds[i] = btn.GetComponent<Image>();
                answerButtons[i] = btn;
                answerLabels[i] = btn.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                answerLabels[i].alignment = TextAlignmentOptions.Left;
                // Indent text slightly
                answerLabels[i].rectTransform.anchoredPosition = new Vector2(20, 0);
                answerLabels[i].rectTransform.sizeDelta = new Vector2(880, 90);
                
                int index = i;
                btn.onClick.AddListener(() => AnimateButtonPress(answerButtons[index]));
            }

            // Walk away button
            btnWalkAway = CreateButton(gamePanel.transform, "BtnWalkAway", "Walk Away", new Vector2(0, -800), new Vector2(350, 80), 30);
            btnWalkAway.GetComponent<Image>().color = new Color32(180, 50, 50, 255);
        }

        private void BuildAudiencePanel(Transform parent)
        {
            audiencePanel = CreatePanel(parent, "AudiencePanel", Vector2.zero, new Vector2(800, 700));
            
            CreateTMP(audiencePanel.transform, "AudTitle", "Audience Results", 40, TextAlignmentOptions.Center, new Vector2(0, 280), new Vector2(700, 60)).color = _accentGold;

            string[] letters = { "A", "B", "C", "D" };
            for (int i = 0; i < 4; i++)
            {
                float xPos = -240 + i * 160;

                audienceLetterLabels[i] = CreateTMP(audiencePanel.transform, $"AudLetter_{i}", letters[i], 36, TextAlignmentOptions.Center, new Vector2(xPos, -240), new Vector2(100, 50));
                
                var sliderGO = new GameObject($"AudSlider_{i}");
                sliderGO.transform.SetParent(audiencePanel.transform, false);
                var sliderRT = sliderGO.AddComponent<RectTransform>();
                sliderRT.anchoredPosition = new Vector2(xPos, 0);
                sliderRT.sizeDelta = new Vector2(100, 380);

                var bgGO = new GameObject("Background");
                bgGO.transform.SetParent(sliderGO.transform, false);
                var bgRT = bgGO.AddComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
                bgRT.sizeDelta = Vector2.zero; bgGO.AddComponent<Image>().color = new Color32(40, 40, 80, 255);

                var fillAreaGO = new GameObject("FillArea");
                fillAreaGO.transform.SetParent(sliderGO.transform, false);
                var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
                fillAreaRT.anchorMin = Vector2.zero; fillAreaRT.anchorMax = Vector2.one;
                fillAreaRT.sizeDelta = Vector2.zero; 

                var fillGO = new GameObject("Fill");
                fillGO.transform.SetParent(fillAreaGO.transform, false);
                var fillRT = fillGO.AddComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
                fillRT.sizeDelta = Vector2.zero; fillGO.AddComponent<Image>().color = _accentGold;

                var slider = sliderGO.AddComponent<Slider>();
                slider.direction = Slider.Direction.BottomToTop;
                slider.minValue = 0; slider.maxValue = 100;
                slider.interactable = false;
                slider.fillRect = fillRT;
                audienceSliders[i] = slider;

                audiencePercentLabels[i] = CreateTMP(audiencePanel.transform, $"AudPct_{i}", "0%", 30, TextAlignmentOptions.Center, new Vector2(xPos, 220), new Vector2(120, 40));
            }

            audienceCloseButton = CreateButton(audiencePanel.transform, "AudClose", "OK", new Vector2(0, -300), new Vector2(250, 70), 32);
        }

        private void BuildPhonePanel(Transform parent)
        {
            phonePanel = CreatePanel(parent, "PhonePanel", Vector2.zero, new Vector2(800, 500));

            CreateTMP(phonePanel.transform, "PhoneTitle", "📞 Phone a Friend", 40, TextAlignmentOptions.Center, new Vector2(0, 180), new Vector2(700, 60)).color = _accentGold;

            phoneFriendText = CreateTMP(phonePanel.transform, "PhoneFriendText", "", 34, TextAlignmentOptions.Center, new Vector2(0, 20), new Vector2(750, 200));
            
            phoneCloseButton = CreateButton(phonePanel.transform, "PhoneClose", "OK", new Vector2(0, -180), new Vector2(250, 70), 32);
        }

        private void BuildResultPanel(Transform parent)
        {
            resultPanel = CreatePanel(parent, "ResultPanel", Vector2.zero, new Vector2(900, 700));

            resultTitle = CreateTMP(resultPanel.transform, "ResultTitle", "Game Over", 55, TextAlignmentOptions.Center, new Vector2(0, 250), new Vector2(800, 80));
            resultTitle.color = _accentGold;
            resultTitle.fontStyle = FontStyles.Bold;

            resultMessage = CreateTMP(resultPanel.transform, "ResultMsg", "", 40, TextAlignmentOptions.Center, new Vector2(0, 60), new Vector2(800, 250));
            
            resultMenuButton = CreateButton(resultPanel.transform, "ResultMenuBtn", "Main Menu", new Vector2(0, -220), new Vector2(400, 100), 36);
        }

        // ══════════════════════════════════════════════
        //  UI UPDATE METHODS
        // ══════════════════════════════════════════════
        public void ApplyLanguage(string language)
        {
            bool isTurk = (language == "TR");

            categoryTitle.text = isTurk ? "Bir Kategori Seçin" : "Choose a Category";
            categorySubtitle.text = isTurk ? "Dini Kim Milyoner Olmak İster?" : "Religious Who Wants to Be a Millionaire?";

            lblFiftyFifty.text = isTurk ? "Yarı Yarıya" : "50:50";
            lblAskAudience.text = isTurk ? "Seyirci" : "Ask Aud";
            lblPhoneFriend.text = isTurk ? "Telefon" : "Phone";
            btnWalkAway.GetComponentInChildren<TextMeshProUGUI>().text = isTurk ? "Çekil" : "Walk Away";

            audienceCloseButton.GetComponentInChildren<TextMeshProUGUI>().text = isTurk ? "Tamam" : "OK";
            phoneCloseButton.GetComponentInChildren<TextMeshProUGUI>().text = isTurk ? "Tamam" : "OK";
            resultMenuButton.GetComponentInChildren<TextMeshProUGUI>().text = isTurk ? "Ana Menü" : "Main Menu";
        }

        public void ShowLanguageScreen(bool show)
        {
            if(show) ShowPanel(languagePanel); else HidePanel(languagePanel);
        }

        public void ShowCategoryScreen(bool show)
        {
            if(show) 
            {
                HidePanel(languagePanel);
                ShowPanel(categoryPanel);
            }
            else 
            {
                HidePanel(categoryPanel);
            }

            if(show) HidePanel(gamePanel); else ShowPanel(gamePanel);
            
            if(show)
            {
                HidePanel(resultPanel);
                HidePanel(audiencePanel);
                HidePanel(phonePanel);
            }
        }

        public void ShowQuestion(QuestionEntry q, int stepIndex, string language = "EN")
        {
            bool isTurk = (language == "TR");
            questionNumberText.text = $"{(isTurk ? "Soru" : "Question")} {stepIndex + 1} / {MoneyLadder.TotalSteps}";
            questionText.text = q.questionText;

            // Fade in Question text
            Tween.Alpha(questionText, 0f, 1f, 0.5f);

            string[] prefixes = { "A: ", "B: ", "C: ", "D: " };
            for (int i = 0; i < 4; i++)
            {
                answerButtons[i].gameObject.SetActive(true);
                answerButtons[i].interactable = true;
                answerLabels[i].text = prefixes[i] + q.answers[i];
                answerBackgrounds[i].color = _btnNormal;
                
                // Animation for Answer Buttons sliding in
                var rt = answerButtons[i].GetComponent<RectTransform>();
                var origPos = rt.anchoredPosition;
                rt.anchoredPosition = origPos + new Vector2(0, -60);
                var cg = answerButtons[i].gameObject.GetComponent<CanvasGroup>();
                if(cg == null) cg = answerButtons[i].gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                
                Tween.UIAnchoredPosition(rt, origPos, 0.3f, Ease.OutBack, startDelay: i * 0.1f);
                Tween.Alpha(cg, 1f, 0.3f, Ease.Linear, startDelay: i * 0.1f);
            }
        }

        public void HighlightAnswer(int index, bool correct)
        {
            var targetColor = correct ? _btnCorrect : _btnWrong;
            Tween.Color(answerBackgrounds[index], (Color)targetColor, 0.3f);
            
            if (correct)
            {
                Tween.Scale(answerButtons[index].transform, Vector3.one * 1.05f, 0.2f, cycles: 4, cycleMode: CycleMode.Yoyo);
            }
            else
            {
                Tween.LocalPositionX(answerButtons[index].transform, endValue: 15f, duration: 0.05f, cycles: 6, cycleMode: CycleMode.Yoyo);
            }
        }

        public void ShowCorrectAnswer(int correctIndex)
        {
            Tween.Color(answerBackgrounds[correctIndex], (Color)_btnCorrect, 0.3f, startDelay: 0.5f);
        }

        public void DisableAnswerButtons()
        {
            for (int i = 0; i < 4; i++) answerButtons[i].interactable = false;
        }

        public void ApplyFiftyFifty(List<int> keepIndices)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!keepIndices.Contains(i))
                {
                    var btnCG = answerButtons[i].GetComponent<CanvasGroup>();
                    Tween.Alpha(btnCG, 0f, 0.4f).OnComplete(answerButtons[i], b => b.gameObject.SetActive(false));
                }
            }
        }

        public void ShowAudienceResults(float[] percentages)
        {
            ShowPanel(audiencePanel);
            for (int i = 0; i < 4; i++)
            {
                int idx = i; // local copy for closure
                Tween.Custom(0f, percentages[idx], 1.5f, onValueChange: (val) => {
                    audienceSliders[idx].value = val;
                    audiencePercentLabels[idx].text = $"{Mathf.RoundToInt(val)}%";
                }, Ease.OutCubic);
            }
        }

        public void HideAudiencePanel() { AnimateButtonPress(audienceCloseButton); HidePanel(audiencePanel); }

        public void ShowPhoneFriend(string friendSays)
        {
            ShowPanel(phonePanel);
            phoneFriendText.text = friendSays;
            Tween.Alpha(phoneFriendText, 0f, 1f, 0.5f);
        }

        public void HidePhonePanel() { AnimateButtonPress(phoneCloseButton); HidePanel(phonePanel); }

        public void UpdateLifelineButtons(bool fifty, bool audience, bool phone)
        {
            btnFiftyFifty.interactable = fifty;
            btnAskAudience.interactable = audience;
            btnPhoneFriend.interactable = phone;

            SetButtonUsedVisual(btnFiftyFifty, lblFiftyFifty, fifty);
            SetButtonUsedVisual(btnAskAudience, lblAskAudience, audience);
            SetButtonUsedVisual(btnPhoneFriend, lblPhoneFriend, phone);
        }

        private void SetButtonUsedVisual(Button btn, TextMeshProUGUI label, bool available)
        {
            var img = btn.GetComponent<Image>();
            Tween.Color(img, available ? (Color)_btnNormal : (Color)_btnDisabled, 0.3f);
            Tween.Alpha(label, available ? 1f : 0.4f, 0.3f);
        }

        public void UpdateLadder(int currentStep)
        {
            for (int i = 0; i < MoneyLadder.TotalSteps; i++)
            {
                if (i == currentStep)
                {
                    Tween.Color(ladderBackgrounds[i], (Color)_ladderActive, 0.4f);
                    ladderLabels[i].fontStyle = FontStyles.Bold;
                    ladderLabels[i].color = Color.black;
                    Tween.Scale(_ladderRowRTs[i], Vector3.one * 1.05f, 0.5f, cycles: -1, cycleMode: CycleMode.Yoyo);
                }
                else if (i == MoneyLadder.SafeHaven1 || i == MoneyLadder.SafeHaven2)
                {
                    Tween.StopAll(_ladderRowRTs[i]);
                    _ladderRowRTs[i].localScale = Vector3.one;
                    ladderBackgrounds[i].color = _ladderSafe;
                    ladderLabels[i].fontStyle = FontStyles.Normal;
                    ladderLabels[i].color = _white;
                }
                else
                {
                    Tween.StopAll(_ladderRowRTs[i]);
                    _ladderRowRTs[i].localScale = Vector3.one;
                    ladderBackgrounds[i].color = i < currentStep ? new Color32(40, 80, 40, 150) : _ladderNormal; // slight green for passed
                    ladderLabels[i].fontStyle = FontStyles.Normal;
                    ladderLabels[i].color = _white;
                }
            }
        }

        public void ShowResult(string title, string message)
        {
            ShowPanel(resultPanel);
            resultTitle.text = title;
            resultMessage.text = message;

            DisableAnswerButtons();
            btnFiftyFifty.interactable = false;
            btnAskAudience.interactable = false;
            btnPhoneFriend.interactable = false;
            btnWalkAway.interactable = false;
        }

        public void HideResult() { AnimateButtonPress(resultMenuButton); HidePanel(resultPanel); }

        // ══════════════════════════════════════════════
        //  HELPER FACTORY METHODS
        // ══════════════════════════════════════════════
        private GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = _panelBg;
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = _borderColor;
            outline.effectDistance = new Vector2(2, -2);
            
            return go;
        }

        private TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.color = _white;
            return tmp;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, float fontSize = 28)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = _btnNormal;
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.7f, 0.85f, 1f);
            colors.pressedColor = new Color(0.5f, 0.65f, 0.9f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.55f);
            btn.colors = colors;
            btn.targetGraphic = img;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);

            var tmp = CreateTMP(go.transform, "Label", label, fontSize, TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(20, 20));
            tmp.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Generates a simple rounded square sprite programmatically to provide "border radius".
        /// </summary>
        private Sprite CreateRoundedSprite(int size, int radius)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] cols = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Min(x, size - x - 1);
                    float dy = Mathf.Min(y, size - y - 1);
                    bool inside = true;

                    if (dx < radius && dy < radius)
                    {
                        float d = Mathf.Sqrt(Mathf.Pow(radius - dx, 2) + Mathf.Pow(radius - dy, 2));
                        if (d > radius) inside = false;
                    }

                    cols[y * size + x] = inside ? Color.white : Color.clear;
                }
            }

            tex.SetPixels(cols);
            tex.Apply();
            
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
