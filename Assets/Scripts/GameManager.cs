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
        [SerializeField] private AudioClip _audioGameStart;
        [SerializeField] private AudioClip _audioNewQuestion;
        [SerializeField] private AudioClip _audioCorrect;
        [SerializeField] private AudioClip _audioWrong;
        [SerializeField] private AudioClip _audioWin;
        [SerializeField] private AudioClip _audioClick;

        [Header("Particle Systems")]
        [SerializeField] private ParticleSystem _particlesCorrect;
        [SerializeField] private ParticleSystem _particlesWrong;
        [SerializeField] private ParticleSystem _particlesNewQuestion;

        [Header("Dynamic Backgrounds")]
        [SerializeField] private Sprite[] _backgroundSprites;
        [SerializeField] private float _slideshowInterval = 6f;

        [Header("Anti-Copyright Settings")]
        [SerializeField] [Range(0.5f, 1.5f)] private float _audioPitch = 0.92f;

        private AudioSource _audioSource;
        private Coroutine _slideshowCoroutine;

        // ── Game state ──
        private int _currentStep;            // 0‑based ladder step
        private string _currentCategory;
        private string _currentLanguage = "EN";
        private QuestionEntry _currentQuestion;
        private bool _waitingForAnswer;      // prevents double‑clicks

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
            _audioSource.pitch = _audioPitch; // Apply anti-copyright pitch modification
        }

        private void Start()
        {
            // Build the entire UI programmatically
            _uiMgr.BuildUI();

            // Wire up constant button listeners
            WireButtons();
            
            // Wire language buttons
            _uiMgr.btnTurkish.onClick.AddListener(() => OnLanguageSelected("TR"));
            _uiMgr.btnEnglish.onClick.AddListener(() => OnLanguageSelected("EN"));

            _uiMgr.ShowLanguageScreen(true);
        }

        private void OnLanguageSelected(string language)
        {
            PlayClickSound();
            _currentLanguage = language;
            // The original JSON file 'questions' contains Turkish now, and 'questionsEN' contains English
            string fileName = (language == "TR") ? "questions" : "questionsEN";
            
            _questionMgr.LoadDatabase(fileName);

            if (!_questionMgr.IsReady)
            {
                Debug.LogError($"[GameManager] QuestionManager failed to load {fileName}!");
                return;
            }

            // Apply localized text to UI
            _uiMgr.ApplyLanguage(language);

            // Populate category buttons
            _uiMgr.PopulateCategoryButtons(
                _questionMgr.AvailableCategories,
                OnCategorySelected
            );

            _uiMgr.ShowCategoryScreen(true);
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

            PlayAudio(_audioGameStart);

            // Start Slideshow
            if (_slideshowCoroutine != null) StopCoroutine(_slideshowCoroutine);
            if (_backgroundSprites != null && _backgroundSprites.Length > 0)
                _slideshowCoroutine = StartCoroutine(SlideshowRoutine());

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

            PlayAudio(_audioNewQuestion);
            SpawnParticles(_particlesNewQuestion);

            _uiMgr.ShowQuestion(_currentQuestion, _currentStep, _currentLanguage);
            _uiMgr.UpdateLadder(_currentStep);
            RefreshLifelineButtons();
            _uiMgr.btnWalkAway.interactable = true;

            _waitingForAnswer = true;
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
                PlayAudio(_audioWrong);
                SpawnParticles(_particlesWrong);
            }
            else
            {
                PlayAudio(_audioCorrect);
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
                    PlayAudio(_audioWin);
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
                _uiMgr.UpdateBackground(_backgroundSprites[index]);
                index = (index + 1) % _backgroundSprites.Length;
                yield return new WaitForSeconds(_slideshowInterval);
            }
        }

        private void PlayClickSound()
        {
            PlayAudio(_audioClick);
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
