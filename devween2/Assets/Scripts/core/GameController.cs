﻿/*
 * Created by Hugo Uchoas Borges <hugouchoas@outlook.com>
 */

using leaderboard;
using UnityEngine;
using util.google;
using System;
using google;
using System.Collections.Generic;
using System.Linq;
using util;
using DG.Tweening;
using UnityEngine.UI;
using monster;
using System.Collections;
using core.ui;

namespace core
{
    [RequireComponent(typeof(AudioSource))]
    public class GameController : Singleton<GameController>
    {
        [Header("Network")]
        [SerializeField] private Loader mGoogleLoader;
        [SerializeField] private float mFetchServerEverySeconds;
        private Coroutine loadFromGoogle = null;

        [Header("Menu Items")]
        [Space]
        [SerializeField] private ClickableComponent mDoorClickable;
        [SerializeField] private ClickableComponent mInfoFrameClickable;
        [SerializeField] private ClickableComponent mRankingClickable;
        [SerializeField] private GameObject mLogo;

        [Header("Game Round")]
        [Space]
        [SerializeField] private RoundUIComponent roundUI;
        private int _roundScore;
        private int _roundCandy;

        [Header("Game Round Values")]
        [Space]
        [SerializeField] [Range(1, 50)] private int mBaseScorePrize = 5;
        [SerializeField] [Range(1, 50)] private int mBaseCandyPrize = 5;

        [Header("Multiplier")]
        [Space]
        [SerializeField] private int _maxMultiplier = 8;
        [SerializeField] private int _currentMultiplier = 1;
        [SerializeField] private int _currentMultiplierZoneCount = 0;

        [Space]
        [SerializeField] [Range(1f, 10f)] private float mStartRoundTime;
        [SerializeField] [Range(0f, 1f)] private float mRoundTimeDecrease;
        [SerializeField] [Range(0.1f, 5f)] private float mMinRoundTime;
        [SerializeField] private float _currentRoundTime;

        [Header("Game Controllers")]
        [Space]
        [SerializeField] private MonsterManager mMonsterManager;
        [SerializeField] private LeaderboardPoolController mLeaderboardPoolController;

        [Header("Login-out Menus")]
        [Space]
        [SerializeField] private LoginUIComponent loginUI;
        [SerializeField] private LogoutUIComponent logoutUI;

        [Header("User Info")]
        [Space]
        [SerializeField] private PlayerInfoUIComponent mLoggedUserUI;
        [SerializeField] private Player mLoggedUser;

        [Header("Leaderboard Challenge")]
        [Space]
        [SerializeField] private LeaderboardChallengeUIComponent leaderboardChallengeUI;

        [Header("Timer Component")]
        [Space]
        [SerializeField] private TimerUIComponent timerUI;

        [Header("Audio files")]
        [Space]
        [SerializeField] private AudioClip[] audioClips;
        private Dictionary<string, AudioClip> _audioClipsDict;
        private AudioSource _audioSource;
        [SerializeField] private AudioSource additionalAudioSource;

        /// <summary>
        /// First call in the ENTIRE game
        /// </summary>
        private void Start()
        {
            PlayAudio("menu_bg", true);
            LoadFromGoogle(() =>
            {
                mDoorClickable.onPointerDown.AddListener(Play);
                SetTouchActive(true);

                mMonsterManager.onGameOver = GameOver;
                mMonsterManager.onNextRound = NextRound;
                mMonsterManager.Init();

                loginUI.SetActive(true);
                loginUI.mLoginButton.onClick.AddListener(Login);
            });
        }

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioClipsDict = new Dictionary<string, AudioClip>();
            foreach (var audioClip in audioClips)
            {
                _audioClipsDict.Add(audioClip.name, audioClip);
            }
        }


        // ----------------------------------------------------------------------------------
        // ========================== Login ============================
        // ----------------------------------------------------------------------------------

        private void Login()
        {
            // Remove login button listener
            loginUI.mLoginButton.onClick.RemoveAllListeners();

            List<FormEntry> formEntries = new List<FormEntry>()
            {
                new FormEntry(SendForm.Instance.kNameEntry, loginUI.mNameInput.text),     // Name
                new FormEntry(SendForm.Instance.kPasswordEntry, loginUI.mPasswordInput.text), // Password
            };

            if (SendForm.Instance.CheckValidEntry(formEntries.ToArray()))
            {
                mLoggedUser = new Player(loginUI.mNameInput.text, loginUI.mPasswordInput.text, 0, 0);

                var currentUserEntry = GetEntryByName(loginUI.mNameInput.text);
                if (currentUserEntry.HasValue)
                {
                    mLoggedUser.coins = currentUserEntry.Value.coins;
                    mLoggedUser.score = currentUserEntry.Value.score;
                }

                UpdateUIPlayerInfo();

                loginUI.mLoginFeedback.text = "<color=\"green\">SUCESSO</color>";

                // DELAY then hide login
                StartCoroutine(DelayCall(() => HideLoginPanel(), 2));
            }
            else
            {
                loginUI.mLoginFeedback.text = "<color=\"red\">ERRO</color>";

                // Add Login Listener back
                loginUI.mLoginButton.onClick.AddListener(Login);
            }
        }

        private void Logout()
        {
            logoutUI.mLogoutButton.onClick.RemoveAllListeners();
            loginUI.SetActive(true);
            logoutUI.SetActive(false);
            mLoggedUserUI.SetActive(false);

            mLoggedUser = null;

            loginUI.mLoginButton.onClick.AddListener(Login);
        }

        private void HideLoginPanel()
        {
            GameDebug.Log("Hiding Login Panel", util.LogType.Transition);
            loginUI.mLoginFeedback.text = "";
            loginUI.SetActive(false);
            logoutUI.SetActive(true);
            logoutUI.mLogoutButton.onClick.AddListener(Logout);

            // PlayerInfoUI
            mLoggedUserUI.SetActive(true);
        }


        // ----------------------------------------------------------------------------------
        // ========================== Game Flow ============================
        // ----------------------------------------------------------------------------------

        private void SetTouchActive(bool active)
        {
            mDoorClickable.touchable = active;
            mInfoFrameClickable.touchable = active;
            mRankingClickable.touchable = active;
        }

        private void Play()
        {
            GameDebug.Log("Starting Game...", util.LogType.Transition);
            PlayAudio("single_click");
            _currentRoundTime = mStartRoundTime;
            _currentMultiplierZoneCount = 0;

            // Reset Multiplier
            _currentMultiplierZoneCount = 0;
            _currentMultiplier = 1;
            timerUI.multiplierText.text = $"x{_currentMultiplier}";
            timerUI.multiplierText.fontSize = 30 + 10 * _currentMultiplier;

            // Remove click Listeners
            SetTouchActive(false);

            _roundScore = 0;
            _roundCandy = 0;

            UpdateRoundValues();

            // Remove LeaderBoards + GameInfoPanel + Logo
            mLogo.SetActive(false);
            mRankingClickable.transform.DOMoveX(mRankingClickable.transform.position.x - 400, 0.2f);
            mInfoFrameClickable.transform.DOMoveX(mInfoFrameClickable.transform.position.x + 400, 0.2f);

            // Show round values on screen
            roundUI.mRoundValuesPanel.transform.DOLocalMoveY(0, 1);

            // Zoom in the Door
            mDoorClickable.transform.DOScale(2f, 1f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
            {
                StartRound();
            });
        }

        private void GameOver()
        {
            GameDebug.Log("GameOver...", util.LogType.Transition);
            PlayAudio("choose_wrong");
            mMonsterManager.SetTouchActive(false);
            timerUI.StopTimer();

            // Wrong animation
            roundUI.SetCorrect(false, () =>
            {
                // Closing the door
                // TODO: Closing the door animation
                mDoorClickable.GetComponent<Image>()
                    .DOFade(1, 0.5f)
                    .OnComplete(() =>
                    {
                        // Hide the monsters
                        mMonsterManager.SetMonstersAlpha(0);

                        // Hide the message
                        roundUI.mRoundMessageSpr.alpha = 0;

                        // Hide the roundFeedback
                        roundUI.SetFeedbackActive(false);

                        // Zoom out the Door
                        mDoorClickable.transform.DOScale(1f, 1f)
                                .SetEase(Ease.OutQuad)
                                .OnComplete(() =>
                                {
                                    // Remove RoundValuesPanel from screen
                                    roundUI.mRoundValuesPanel.transform.DOLocalMoveY(200, 0.5f);

                                    // Add LeaderBoards + GameInfoPanel + Logo
                                    mLogo.SetActive(true);
                                    mRankingClickable.transform.DOMoveX(mRankingClickable.transform.position.x + 400, 0.2f);
                                    mInfoFrameClickable.transform.DOMoveX(mInfoFrameClickable.transform.position.x - 400, 0.2f);
                                    SetTouchActive(true);

                                    UpdatePlayerInfo();
                                    SendValuesToServer();
                                });
                    });
            });
        }

        private void UpdateUIPlayerInfo()
        {
            // Update LoggedUser UI info
            mLoggedUserUI.playerName.text = mLoggedUser.name;
            mLoggedUserUI.playerCoins.text = mLoggedUser.coins.ToString();
            mLoggedUserUI.playerScore.text = mLoggedUser.score.ToString();
        }

        private void UpdatePlayerInfo()
        {
            mLoggedUser.coins += _roundCandy;
            mLoggedUser.score = Math.Max(_roundScore, mLoggedUser.score);

            string target = leaderboardChallengeUI.GetTargetName();
            if (!string.IsNullOrEmpty(target))
            {
                SheetEntry? targetPlayer = GetEntryByName(target);
                if (!targetPlayer.HasValue)
                {
                    GameDebug.LogError($"Could not find target: {target}", util.LogType.Leaderboard);
                    return;
                }

                int betCoins = leaderboardChallengeUI.GetBetCoins();
                int targetCoins;

                if (_roundScore > leaderboardChallengeUI.GetBetScore())
                {
                    GameDebug.Log($"----> CHALLENGE WON<-----", util.LogType.Leaderboard);

                    // Update loggedUser coins
                    mLoggedUser.coins += betCoins;

                    targetCoins = targetPlayer.Value.coins - betCoins;
                    targetCoins = Math.Max(0, targetCoins);
                }
                else
                {
                    GameDebug.Log($"----> CHALLENGE LOST<-----", util.LogType.Leaderboard);

                    // Update loggedUser coins
                    mLoggedUser.coins -= betCoins;
                    mLoggedUser.coins = Math.Max(0, mLoggedUser.coins);

                    targetCoins = targetPlayer.Value.coins + betCoins;
                }

                // Update Target Coins
                List<FormEntry> formEntries = new List<FormEntry>()
                    {
                        new FormEntry(SendForm.Instance.kNameEntry, targetPlayer.Value.name),     // Name
                        new FormEntry(SendForm.Instance.kPasswordEntry, targetPlayer.Value.password), // Password
                
                        new FormEntry(SendForm.Instance.kScoreEntry, targetPlayer.Value.score.ToString()),    // Score
                        new FormEntry(SendForm.Instance.kCoinsEntry, targetCoins.ToString()),    // Coins
                    };

                // Send the new Score then load all data from GoogleDocs again
                SendForm.Instance.Send(() => LoadFromGoogle(), formEntries.ToArray());

                // Reset the challenge values
                leaderboardChallengeUI.SetActive(false);
            }

            UpdateUIPlayerInfo();
        }

        private void SendValuesToServer()
        {
            if (mLoggedUser == null)
            {
                GameDebug.LogError("User not logged... Not sending any data to server", util.LogType.Round);
                LoadFromGoogle();
                return;
            }

            List<FormEntry> formEntries = new List<FormEntry>()
            {
                new FormEntry(SendForm.Instance.kNameEntry, mLoggedUser.name),     // Name
                new FormEntry(SendForm.Instance.kPasswordEntry, mLoggedUser.password), // Password
                
                new FormEntry(SendForm.Instance.kScoreEntry, mLoggedUser.score.ToString()),    // Score
                new FormEntry(SendForm.Instance.kCoinsEntry, mLoggedUser.coins.ToString()),    // Coins
            };

            // Send the new Score then load all data from GoogleDocs again
            SendForm.Instance.Send(() => LoadFromGoogle(), formEntries.ToArray());
        }

        // ----------------------------------------------------------------------------------
        // ========================== Round Specifics ============================
        // ----------------------------------------------------------------------------------

        private void UpdateRoundValues()
        {
            roundUI.mRoundScoreText.text = _roundScore.ToString();
            roundUI.mRoundCandyText.text = _roundCandy.ToString();
        }

        private void StartRound()
        {
            mMonsterManager.PrepareRound();

            Monster selectedMonster = mMonsterManager.GetSelectedMonster();
            roundUI.mRoundMessage.text = $"Selecione {selectedMonster.name}";

            roundUI.mRoundMessageSpr
            .DOFade(1f, 1f)
            .OnComplete(() =>
            {
                roundUI.SetFeedbackActive(true);
                // TODO: OpenDoor Animation (then delay)
                PlayAudio("door_open");
                mDoorClickable.GetComponent<Image>()
                .DOFade(0, 0.5f)
                .OnComplete(() =>
                {
                    timerUI.StartTimer(_currentRoundTime, GameOver);
                    GameDebug.Log("Starting Round...", util.LogType.Transition);
                    mMonsterManager.StartRound();
                });
            });
        }

        private void NextRound()
        {
            GameDebug.Log("Next Round...", util.LogType.Transition);
            PlayAudio("choose_correct");
            _currentRoundTime = Math.Max(mMinRoundTime, _currentRoundTime - mRoundTimeDecrease);

            // Increment score/candy then update screen values
            _roundCandy += mBaseCandyPrize;
            _roundScore += mBaseScorePrize * _currentMultiplier;

            timerUI.StopTimer((bool hasMultiplier) =>
            {
                if (!hasMultiplier)
                {
                    _currentMultiplierZoneCount = 0;
                    _currentMultiplier = 1;
                }
                else
                {
                    _currentMultiplierZoneCount++;
                    if (_currentMultiplier < _maxMultiplier && _currentMultiplierZoneCount >= _currentMultiplier * 2)
                    {
                        _currentMultiplier *= 2;
                        _currentMultiplierZoneCount = 0;
                    }
                }
            });

            timerUI.multiplierText.text = $"x{_currentMultiplier}";
            timerUI.multiplierText.fontSize = 30 + 10 * _currentMultiplier;

            UpdateRoundValues();

            // Correct animation
            roundUI.SetCorrect(true, () =>
            {
                // Closing the Door
                // TODO: Closing the door animation
                PlayAudio("door_close");
                mDoorClickable.GetComponent<Image>()
                    .DOFade(1, 0.5f)
                    .OnComplete(() =>
                    {
                        GameDebug.Log("Starting Next Round...", util.LogType.Transition);
                        StartRound();
                    });
            });
        }


        // ----------------------------------------------------------------------------------
        // ========================== Audio Playback ============================
        // ----------------------------------------------------------------------------------

        public void PlayAudio(string audioName, bool background = false)
        {
            var audioSource = background ? _audioSource : additionalAudioSource;
            audioSource.clip = _audioClipsDict[audioName];
            audioSource.loop = background;
            audioSource.Play();
        }


        // ----------------------------------------------------------------------------------
        // ========================== Server Communication ============================
        // ----------------------------------------------------------------------------------


        public SheetEntry? GetEntryByName(string name)
        {
            return mGoogleLoader.GetEntryByName(name);
        }

        public void LoadFromGoogle(Action callback = null)
        {
            // Load form from Google Drive
            mGoogleLoader.Load((entries) =>
            {
                mLeaderboardPoolController.DestroyAll();

                // Remove duplicated entries (keep newer one)
                List<SheetEntry> singleEntriesList = new List<SheetEntry>();
                foreach (var entry in entries.Reverse())
                {
                    if (singleEntriesList.Where(e => e.name == entry.name).ToArray().Length == 0)
                        singleEntriesList.Add(entry);
                }
                // Remove also zero score\candy entries
                singleEntriesList = singleEntriesList.Where(e => e.coins > 0 && e.score > 0).ToList();

                SheetEntry[] singleEntries = singleEntriesList.ToArray();

                // Sort
                Array.Sort<SheetEntry>(singleEntries,
                    (x, y) =>
                    {
                        var score = x.score.CompareTo(y.score);
                        if (score == 0)
                        {
                            var coins = x.coins.CompareTo(y.coins);
                            if (coins == 0)
                                return x.name.CompareTo(y.name);
                            return coins;
                        }
                        return score;
                    });
                for (int i = 0; i < singleEntries.Length; i++)
                {
                    // Instantiate leaderboard items
                    var entry = singleEntries[i];
                    LeaderboardItem leaderboardItem = mLeaderboardPoolController.Spawn();
                    leaderboardItem.onPointerDown.AddListener(() => LeaderboardItemSelected(leaderboardItem));
                    leaderboardItem.UpdateInfo(entry.name, entry.password, entry.coins, entry.score, singleEntries.Length - i);
                }

                callback?.Invoke();

                // Remove LoadFromGoogle delayCall
                if (loadFromGoogle != null)
                {
                    StopCoroutine(loadFromGoogle);
                    loadFromGoogle = null;
                }

                // Adds a Server fetch delayed call
                loadFromGoogle = StartCoroutine(DelayCall(() => LoadFromGoogle(null), mFetchServerEverySeconds));
            });
        }

        private void LeaderboardItemSelected(LeaderboardItem item)
        {
            if (mLoggedUser == null || string.IsNullOrEmpty(mLoggedUser.name))
            {
                GameDebug.Log($"USER NOT LOGGED --> Could not open Challenge", util.LogType.Leaderboard);
                return;
            }

            if (mLoggedUser.name == item.Name)
            {
                GameDebug.Log($"You CANNOT bet yourself", util.LogType.Leaderboard);
                return;
            }

            GameDebug.Log($"Leaderboard selected: {item.Name}", util.LogType.Leaderboard);
            leaderboardChallengeUI.SetActive(true);
            leaderboardChallengeUI.betButton.onClick.AddListener(Play);
            leaderboardChallengeUI.SetTarget(item.Name, Math.Min(item.Coins, mLoggedUser.coins), item.Score);
        }

        private IEnumerator DelayCall(Action call, float delay)
        {
            GameDebug.Log($"DelayCall ==> {call.Method.Name}", util.LogType.Thread);
            yield return new WaitForSeconds(delay);
            call.Invoke();
        }
    }

    [Serializable]
    public class Player
    {
        public string name;
        public string password;

        public int score;
        public int coins;

        public Player(string name, string password, int score, int coins)
        {
            this.name = name;
            this.password = password;
            this.score = score;
            this.coins = coins;
        }
    }
}