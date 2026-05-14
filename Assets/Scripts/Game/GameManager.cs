using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BallShooter
{
    public class GameManager : MonoBehaviour
    {
        public PlayerHealth player;
        public WaveManager waves;
        public GameUI ui;

        float score;
        bool gameOver;

        public float Score => score;
        public bool IsGameOver => gameOver;

        void Start()
        {
            if (player != null) player.OnDeath += HandlePlayerDeath;
            // WaveManager.OnEnemyKilled now passes (score, deathPosition). GameManager
            // only needs the score; GameUI uses the position for the world popup.
            if (waves != null) waves.OnEnemyKilled += (score, _pos) => AddScore(score);
            if (ui != null) ui.UpdateScore(0);
        }

        void Update()
        {
            if (gameOver)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.rKey.wasPressedThisFrame)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }
        }

        void AddScore(float v)
        {
            score += v;
            if (ui != null) ui.UpdateScore(Mathf.RoundToInt(score));
        }

        void HandlePlayerDeath()
        {
            gameOver = true;
            if (waves != null) waves.StopAll();
            if (ui != null) ui.ShowGameOver(Mathf.RoundToInt(score), waves != null ? waves.Round : 0);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
