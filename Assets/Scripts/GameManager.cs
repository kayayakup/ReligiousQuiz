using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager — the central orchestrator.
/// 
/// Attach this script to an empty GameObject in your scene.
/// On Awake it creates all other managers (QuestionManager, LifelineManager, UIManager),
/// builds the UI programmatically, and wires up every button.
///
/// Game flow:
///   1. Category selection → player chooses a topic.
///   2. Questions are loaded & filtered → 15 questions picked by difficulty.
///   3. Player answers questions, uses lifelines, or walks away.
///   4. Wrong answer → game over (player keeps safe‑haven prize).
///   5. All 15 correct → player wins $1,000,000.
/// </summary>
namespace MillionaireGame
{
    public class GameManager : MonoBehaviour
    {
        // ── Manager references (created at runtime) ──
        private QuestionManager _questionMgr;
        private LifelineManager _lifelineMgr;
        private UIManager _uiMgr;

        // ── Audio & Effects (Assign in Inspector) ──
        [Header("Audio Clips")]
        public AudioClip audioGameStart;
        public AudioClip audioNewQuestion;
        public AudioClip audioCorrect;
        public AudioClip audioWrong;
        public AudioClip audioWin;
        public AudioClip audioClick;
        public AudioClip audioLifeline5050;
        public AudioClip audioLifelineAudience;
        public AudioClip audioLifelinePhone;
        public AudioClip audioBackground;

        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem _particlesCorrect;
        [SerializeField] private ParticleSystem _particlesWrong;
        [SerializeField] private ParticleSystem _particlesNewQuestion;

        [Header("Dynamic Backgrounds")]
        [SerializeField] private Sprite[] _backgroundSprites;
        [SerializeField] private float _slideshowInterval = 10f;

        [Header("Anti-Copyright Settings")]
        [SerializeField] [Range(0.5f, 1.5f)] private float _audioPitch = 0.92f;

        [Header("UI Customization")]
        [SerializeField] private Sprite _settingsButtonSprite;

        private AudioSource _audioSource;
        private AudioSource _musicSource;
        private Coroutine _slideshowCoroutine;

        private const string PREF_LANGUAGE = "SelectedLanguage";
        private const string PREF_MUSIC_VOL = "MusicVolume";
        private const string PREF_SFX_VOL = "SFXVolume";
        private const string PREF_MUTE = "MuteAll";

        // ── Game state ──
        private int _currentStep;            // 0‑based ladder step
        private string _currentCategory;
        private string _currentLanguage = "EN";
        private QuestionEntry _currentQuestion;
        private bool _waitingForAnswer;      // prevents double‑clicks
        private float _timer;
        private bool _timerActive;

        // ═══════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ═══════════════════════════════════════════════

        private void Awake()
        {
            // Create manager components on this same GameObject
            _questionMgr = gameObject.AddComponent<QuestionManager>();
            _lifelineMgr = gameObject.AddComponent<LifelineManager>();
            _uiMgr       = gameObject.AddComponent<UIManager>();

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.pitch = _audioPitch;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
        }

        private void Start()
        {
            // Build the entire UI programmatically
            _uiMgr.BuildUI();

            // Apply custom settings sprite if assigned
            if (_settingsButtonSprite != null)
                _uiMgr.SetSettingsButtonSprite(_settingsButtonSprite);

            // Wire up constant button listeners
            WireButtons();

            // Start background slideshow immediately (first image shown at once, then every 10s)
            if (_backgroundSprites != null && _backgroundSprites.Length > 0)
                _slideshowCoroutine = StartCoroutine(SlideshowRoutine());

            // Check if language was previously selected
            if (PlayerPrefs.HasKey(PREF_LANGUAGE))
            {
                // Returning user – skip language screen, go straight to categories
                string savedLang = PlayerPrefs.GetString(PREF_LANGUAGE, "EN");
                _uiMgr.SetSettingsButtonVisible(true);   // show gear immediately
                ApplyLanguageAndShowCategories(savedLang);
            }
            else
            {
                // First launch – show language selection (gear hidden until language chosen)
                _uiMgr.btnTurkish.onClick.AddListener(() => OnFirstLanguageSelected("TR"));
                _uiMgr.btnEnglish.onClick.AddListener(() => OnFirstLanguageSelected("EN"));
                _uiMgr.ShowLanguageScreen(true);
            }

            // Initialize background music
            if (audioBackground != null)
            {
                _musicSource.clip = audioBackground;
                _musicSource.Play();
            }

            // Load and apply audio settings
            LoadAudioSettings();
        }

        private void LoadAudioSettings()
        {
            float musicVol = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.7f);
            float sfxVol = PlayerPrefs.GetFloat(PREF_SFX_VOL, 1f);
            bool isMuted = PlayerPrefs.GetInt(PREF_MUTE, 0) == 1;

            _uiMgr.musicVolumeSlider.SetValueWithoutNotify(musicVol);
            _uiMgr.sfxVolumeSlider.SetValueWithoutNotify(sfxVol);
            _uiMgr.muteToggle.SetIsOnWithoutNotify(isMuted);

            ApplyAudioSettings(musicVol, sfxVol, isMuted);
        }

        private void ApplyAudioSettings(float musicVol, float sfxVol, bool muted)
        {
            _musicSource.volume = muted ? 0 : musicVol;
            _audioSource.volume = muted ? 0 : sfxVol;
        }

        private void OnMusicVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat(PREF_MUSIC_VOL, val);
            ApplyAudioSettings(val, _uiMgr.sfxVolumeSlider.value, _uiMgr.muteToggle.isOn);
        }

        private void OnSFXVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat(PREF_SFX_VOL, val);
            ApplyAudioSettings(_uiMgr.musicVolumeSlider.value, val, _uiMgr.muteToggle.isOn);
        }

        private void OnMuteToggled(bool muted)
        {
            PlayerPrefs.SetInt(PREF_MUTE, muted ? 1 : 0);
            ApplyAudioSettings(_uiMgr.musicVolumeSlider.value, _uiMgr.sfxVolumeSlider.value, muted);
        }

        /// <summary>Called only on first launch from language panel buttons.</summary>
        private void OnFirstLanguageSelected(string language)
        {
            PlayClickSound();
            PlayerPrefs.SetString(PREF_LANGUAGE, language);
            PlayerPrefs.Save();
            _uiMgr.SetSettingsButtonVisible(true);   // reveal gear after first-time selection
            ApplyLanguageAndShowCategories(language);
        }

        /// <summary>Loads the question database, applies UI language, and shows category screen.</summary>
        private void ApplyLanguageAndShowCategories(string language)
        {
            _currentLanguage = language;
            string fileName = (language == "TR") ? "questions" : "questionsEN";

            _questionMgr.LoadDatabase(fileName);

            if (!_questionMgr.IsReady)
            {
                Debug.LogError($"[GameManager] QuestionManager failed to load {fileName}!");
                return;
            }

            // Apply localized text to UI
            _uiMgr.ApplyLanguage(language);

            // Sync dropdown to current language (0 = TR, 1 = EN)
            _uiMgr.languageDropdown.SetValueWithoutNotify(language == "TR" ? 0 : 1);

            // Populate category buttons
            _uiMgr.PopulateCategoryButtons(
                _questionMgr.AvailableCategories,
                OnCategorySelected
            );

            _uiMgr.ShowCategoryScreen(true);
        }

        private void Update()
        {
            if (_timerActive)
            {
                _timer -= Time.deltaTime;
                _uiMgr.UpdateTimerUI(_timer, true);

                if (_timer <= 0)
                {
                    OnTimeOut();
                }
            }
        }

        private void OnTimeOut()
        {
            _timerActive = false;
            _uiMgr.UpdateTimerUI(0, false);
            _uiMgr.DisableAnswerButtons();
            _uiMgr.btnWalkAway.interactable = false;

            PlayAudio(audioWrong);

            bool isTurk = (_currentLanguage == "TR");
            string title = isTurk ? "Süre Doldu!" : "Time's Up!";
            string msg = isTurk ? "Soruyu cevaplamak için süreniz doldu." : "You ran out of time to answer the question.";
            string guaranteed = MoneyLadder.GetGuaranteedPrize(_currentStep);
            string dropMsg = isTurk ? "Şu seviyeye düştünüz:" : "You drop to the guaranteed level:";

            _uiMgr.ShowResult(title, $"{msg}\n\n{dropMsg} {guaranteed}");
        }

        /// <summary>Called when language is changed via settings dropdown.</summary>
        private void OnLanguageChangedFromSettings(int index)
        {
            string lang = index == 0 ? "TR" : "EN";
            if (lang == _currentLanguage) return;

            PlayerPrefs.SetString(PREF_LANGUAGE, lang);
            PlayerPrefs.Save();

            ApplyLanguageAndShowCategories(lang);
            _uiMgr.HideSettingsPanel();
        }

        // ═══════════════════════════════════════════════
        //  BUTTON WIRING
        // ═══════════════════════════════════════════════

        private void WireButtons()
        {
            // Answer buttons
            for (int i = 0; i < 4; i++)
            {
                int idx = i; // capture for closure
                _uiMgr.answerButtons[i].onClick.AddListener(() => { PlayClickSound(); OnAnswerClicked(idx); });
            }

            // Lifeline buttons
            _uiMgr.btnFiftyFifty.onClick.AddListener(() => { PlayClickSound(); OnFiftyFifty(); });
            _uiMgr.btnAskAudience.onClick.AddListener(() => { PlayClickSound(); OnAskAudience(); });
            _uiMgr.btnPhoneFriend.onClick.AddListener(() => { PlayClickSound(); OnPhoneFriend(); });

            // Audience / phone close buttons
            _uiMgr.audienceCloseButton.onClick.AddListener(() => { PlayClickSound(); _uiMgr.HideAudiencePanel(); });
            _uiMgr.phoneCloseButton.onClick.AddListener(() => { PlayClickSound(); _uiMgr.HidePhonePanel(); });

            // Walk away
            _uiMgr.btnWalkAway.onClick.AddListener(() => { PlayClickSound(); OnWalkAway(); });

            // Result → Main menu
            _uiMgr.resultMenuButton.onClick.AddListener(() => { PlayClickSound(); ReturnToMenu(); });

            // Persistent settings gear button (canvas-level, always visible after language chosen)
            _uiMgr.btnSettings.onClick.AddListener(() => { PlayClickSound(); _uiMgr.ShowSettingsPanel(); });
            _uiMgr.settingsCloseButton.onClick.AddListener(() => { PlayClickSound(); _uiMgr.HideSettingsPanel(); });
            _uiMgr.languageDropdown.onValueChanged.AddListener(OnLanguageChangedFromSettings);

            _uiMgr.musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            _uiMgr.sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            _uiMgr.muteToggle.onValueChanged.AddListener(OnMuteToggled);
        }

        // ═══════════════════════════════════════════════
        //  CATEGORY SELECTION
        // ═══════════════════════════════════════════════

        private void OnCategorySelected(string category)
        {
            PlayClickSound();
            _currentCategory = category;
            Debug.Log($"[GameManager] Category selected: {category}");

            bool success = _questionMgr.PrepareQuestions(category);
            if (!success)
            {
                Debug.LogError($"[GameManager] Could not prepare questions for '{category}'.");
                return;
            }

            // Reset lifelines
            _lifelineMgr.ResetLifelines();

            // Switch to game panel
            _uiMgr.ShowCategoryScreen(false);

            PlayAudio(audioGameStart);

            // Start at step 0
            _currentStep = 0;
            ShowCurrentQuestion();
        }

        // ═══════════════════════════════════════════════
        //  QUESTION DISPLAY
        // ═══════════════════════════════════════════════

        private void ShowCurrentQuestion()
        {
            _currentQuestion = _questionMgr.GetQuestion(_currentStep);
            if (_currentQuestion == null)
            {
                Debug.LogError("[GameManager] No question available for step " + _currentStep);
                return;
            }

            PlayAudio(audioNewQuestion);
            SpawnParticles(_particlesNewQuestion);

            _uiMgr.ShowQuestion(_currentQuestion, _currentStep, _currentLanguage);
            _uiMgr.UpdateLadder(_currentStep);
            RefreshLifelineButtons();
            _uiMgr.btnWalkAway.interactable = true;

            _waitingForAnswer = true;

            // Start timer if before second safe haven
            if (_currentStep <= MoneyLadder.SafeHaven2)
            {
                _timer = 60f + (_currentStep * 10f); // 60s for first, increases by 10s each step
                _timerActive = true;
                _uiMgr.UpdateTimerUI(_timer, true);
            }
            else
            {
                _timerActive = false;
                _uiMgr.UpdateTimerUI(0, false);
            }
        }

        private void RefreshLifelineButtons()
        {
            _uiMgr.UpdateLifelineButtons(
                _lifelineMgr.FiftyFiftyAvailable,
                _lifelineMgr.AskAudienceAvailable,
                _lifelineMgr.PhoneAvailable
            );
        }

        // ═══════════════════════════════════════════════
        //  ANSWER HANDLING
        // ═══════════════════════════════════════════════

        private void OnAnswerClicked(int index)
        {
            if (!_waitingForAnswer) return;
            _waitingForAnswer = false;
            _timerActive = false;
            _uiMgr.UpdateTimerUI(0, false);

            // Disable all buttons immediately
            _uiMgr.DisableAnswerButtons();
            _uiMgr.btnWalkAway.interactable = false;

            bool correct = (index == _currentQuestion.correctAnswerIndex);

            // Highlight the chosen answer
            _uiMgr.HighlightAnswer(index, correct);

            if (!correct)
            {
                // Also show the correct one
                _uiMgr.ShowCorrectAnswer(_currentQuestion.correctAnswerIndex);
                PlayAudio(audioWrong);
                SpawnParticles(_particlesWrong);
            }
            else
            {
                PlayAudio(audioCorrect);
                SpawnParticles(_particlesCorrect);
            }

            // Short delay before proceeding
            StartCoroutine(ProcessAnswerAfterDelay(correct, index));
        }

        private IEnumerator ProcessAnswerAfterDelay(bool correct, int chosenIndex)
        {
            yield return new WaitForSeconds(1.5f);

            if (correct)
            {
                _currentStep++;

                if (_currentStep >= MoneyLadder.TotalSteps)
                {
                    PlayAudio(audioWin);
                    bool isTurk = (_currentLanguage == "TR");
                    string congrat = isTurk ? "🎉 Tebrikler!" : "🎉 Congratulations!";
                    string text = isTurk ? $"Tüm 15 soruyu doğru cevapladınız!\n\n{MoneyLadder.PrizeLabels[MoneyLadder.TotalSteps - 1]} kazandınız!" : $"You answered all 15 questions correctly!\n\nYou won {MoneyLadder.PrizeLabels[MoneyLadder.TotalSteps - 1]}!";
                    // 🎉 WINNER!
                    _uiMgr.ShowResult(
                        congrat,
                        text
                    );
                }
                else
                {
                    // Next question
                    ShowCurrentQuestion();
                }
            }
            else
            {
                // WRONG – game over, drop to safe haven
                string guaranteed = MoneyLadder.GetGuaranteedPrize(_currentStep);
                string wonLabel = _currentStep > 0 ? MoneyLadder.PrizeLabels[_currentStep - 1] : "$0";

                bool isTurk = (_currentLanguage == "TR");
                string title = isTurk ? "Yanlış Cevap!" : "Wrong Answer!";
                string TheCorrectAnswerWas = isTurk ? "Doğru cevap şuydu:" : "The correct answer was:";
                string YouDropTo = isTurk ? "Şu seviyeye düştünüz:" : "You drop to the guaranteed level:";

                _uiMgr.ShowResult(
                    title,
                    $"{TheCorrectAnswerWas}\n{_currentQuestion.answers[_currentQuestion.correctAnswerIndex]}\n\n" +
                    $"{YouDropTo} {guaranteed}"
                );
            }
        }

        // ═══════════════════════════════════════════════
        //  WALK AWAY
        // ═══════════════════════════════════════════════

        private void OnWalkAway()
        {
            if (!_waitingForAnswer) return;
            _waitingForAnswer = false;
            _timerActive = false;
            _uiMgr.UpdateTimerUI(0, false);

            string prize = _currentStep > 0
                ? MoneyLadder.PrizeLabels[_currentStep - 1]
                : "$0";

            // On step 0, walking away means $0.
            // On step 1+, they keep the previous step's prize.
            // But the current step is 0-based: so if you're on step 0, you haven't won anything yet.
            // If you're on step 1, you've answered step 0 correctly (=$100).
            string currentPrize = MoneyLadder.PrizeLabels[_currentStep];

            bool isTurk = (_currentLanguage == "TR");
            string title = isTurk ? "Çekildiniz" : "You Walked Away";
            string text = isTurk ? $"{currentPrize} ile ayrılmayı seçtiniz.\n\nTebrikler!" : $"You chose to leave with {currentPrize}.\n\nWell played!";

            _uiMgr.ShowResult(
                title,
                text
            );
        }

        // ═══════════════════════════════════════════════
        //  LIFELINES
        // ═══════════════════════════════════════════════

        private void OnFiftyFifty()
        {
            if (!_waitingForAnswer || !_lifelineMgr.FiftyFiftyAvailable) return;

            List<int> keepIndices = _lifelineMgr.UseFiftyFifty(_currentQuestion);
            if (keepIndices != null)
            {
                PlayAudio(audioLifeline5050);
                _uiMgr.ApplyFiftyFifty(keepIndices);
            }

            RefreshLifelineButtons();
        }

        private void OnAskAudience()
        {
            if (!_waitingForAnswer || !_lifelineMgr.AskAudienceAvailable) return;

            float[] results = _lifelineMgr.UseAskAudience(_currentQuestion);
            if (results != null)
            {
                PlayAudio(audioLifelineAudience);
                _uiMgr.ShowAudienceResults(results);
            }

            RefreshLifelineButtons();
        }

        private void OnPhoneFriend()
        {
            if (!_waitingForAnswer || !_lifelineMgr.PhoneAvailable) return;

            string friendSays = _lifelineMgr.UsePhoneFriend(_currentQuestion);
            if (friendSays != null)
            {
                PlayAudio(audioLifelinePhone);
                _uiMgr.ShowPhoneFriend(friendSays);
            }

            RefreshLifelineButtons();
        }

        // ═══════════════════════════════════════════════
        //  RETURN TO MENU / RESTART
        // ═══════════════════════════════════════════════

        private void ReturnToMenu()
        {
            _uiMgr.HideResult();
            _uiMgr.ShowCategoryScreen(true);
        }
        // ═══════════════════════════════════════════════
        //  AUDIO & PARTICLES
        // ═══════════════════════════════════════════════

        private IEnumerator SlideshowRoutine()
        {
            int index = 0;
            while (true)
            {
                // Show the current slide immediately, then wait before switching
                _uiMgr.UpdateBackground(_backgroundSprites[index]);
                index = (index + 1) % _backgroundSprites.Length;
                yield return new WaitForSeconds(_slideshowInterval > 0f ? _slideshowInterval : 10f);
            }
        }

        private void PlayClickSound()
        {
            PlayAudio(audioClick);
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }

        private void SpawnParticles(ParticleSystem prefab)
        {
            if (prefab != null)
            {
                ParticleSystem ps = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                ps.Play();
                Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax + 1f);
            }
        }
    }
}
