﻿/*
 * Created by Hugo Uchoas Borges <hugouchoas@outlook.com>
 */

using core;
using UnityEngine;
using UnityEngine.UI;

namespace leaderboard
{
    public class LeaderboardItem : ClickableComponent
    {
        [Header("Components")]
        [SerializeField] private Text _positionText;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _coinsText;
        [SerializeField] private Text _scoreText;

        [Header("Player Info")]
        [Space]
        [SerializeField] private string username;
        [SerializeField] private string userpass;
        [SerializeField] private int coins;
        [SerializeField] private int maxScore;
        [SerializeField] private int position;

        public string Name => username;
        public int Coins => coins;
        public int Score => maxScore;

        public void UpdateInfo(string username, string userpass, int coins, int maxScore, int position)
        {
            this.username = username;
            this.userpass = userpass;
            this.coins = coins;
            this.maxScore = maxScore;
            this.position = position;

            _nameText.text = username;
            _coinsText.text = coins.ToString();
            _scoreText.text = maxScore.ToString();
            _positionText.text = position.ToString();
        }
    }
}