using System.Linq;
using Akila.FPSFramework;
using Kodimodi.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kodimodi.UI
{
    public sealed class GameHUDView : MonoBehaviour
    {
        private TMP_Text score;
        private TMP_Text healthCurrent;
        private TMP_Text healthMaximum;
        private TMP_Text ammoCurrent;
        private TMP_Text ammoReserve;
        private TMP_Text reloadHint;

        private Image healthFill;

        private GameObject boundPlayer;
        private Damageable damageable;
        private Firearm firearm;

        private float nextWeaponSearch;
        private bool initialized;

        private void Initialize()
        {
            if (initialized)
                return;

            score = FindText("Score/Value");

            healthCurrent = FindText("Health/Current");
            healthMaximum = FindText("Health/Maximum");

            ammoCurrent = FindText("Ammo/Current");
            ammoReserve = FindText("Ammo/Reserve");
            reloadHint = FindText("Ammo/ReloadHint");

            var fill = transform.Find(
                "Health/BarBackground/BarFill");

            if (fill != null)
                healthFill = fill.GetComponent<Image>();

            initialized = true;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void Render(
            GameObject player,
            ScoreboardModel board,
            int localId)
        {
            Initialize();

            if (player == null)
                return;

            if (boundPlayer != player)
            {
                boundPlayer = player;
                damageable = player.GetComponent<Damageable>();

                firearm = null;
                nextWeaponSearch = 0f;
            }

            UpdateScore(board);
            UpdateHealth();
            UpdateWeapon();
        }

        private void UpdateScore(ScoreboardModel board)
        {
            if (board == null)
            {
                SetText(score, "— / —");
                return;
            }

            int leaderKills = board.Players?
                .Where(p => p != null)
                .Select(p => p.Kills)
                .DefaultIfEmpty(0)
                .Max() ?? 0;

            SetText(score, $"{leaderKills} / {board.KillLimit}");
        }

        private void UpdateHealth()
        {
            if (damageable == null)
            {
                SetText(healthCurrent, "—");
                SetText(healthMaximum, "/ —");

                if (healthFill != null)
                    healthFill.fillAmount = 0f;

                return;
            }

            float maximum = Mathf.Max(0f, damageable.maxHealth);
            float current = Mathf.Clamp(
                damageable.health, 0f, maximum);

            SetText(
                healthCurrent,
                Mathf.CeilToInt(current).ToString());

            SetText(
                healthMaximum,
                $"/ {Mathf.CeilToInt(maximum)}");

            if (healthFill != null)
            {
                healthFill.fillAmount = maximum > 0f
                    ? current / maximum
                    : 0f;
            }
        }

        private void UpdateWeapon()
        {
            // Повторный поиск учитывает смену и появление оружия.
            if (Time.unscaledTime >= nextWeaponSearch)
            {
                nextWeaponSearch = Time.unscaledTime + 0.2f;

                firearm = boundPlayer
                    .GetComponentsInChildren<Firearm>(true)
                    .FirstOrDefault(IsEquipped);
            }

            if (!IsEquipped(firearm))
            {
                SetText(ammoCurrent, "—");
                SetText(ammoReserve, "/ —");

                if (reloadHint != null)
                    reloadHint.gameObject.SetActive(false);

                return;
            }

            SetText(
                ammoCurrent,
                Mathf.Max(0, firearm.remainingAmmoCount).ToString());

            int reserve = firearm.ammoProfile != null
                ? firearm.ammoProfile.count
                : firearm.remainingAmmoTypeCount;

            SetText(
                ammoReserve,
                $"/ {Mathf.Max(0, reserve)}");

            if (reloadHint != null)
            {
                reloadHint.gameObject.SetActive(true);

                SetText(
                    reloadHint,
                    firearm.isReloading
                        ? "Перезарядка…"
                        : "R — перезарядка");
            }
        }

        private static bool IsEquipped(Firearm value)
        {
            return value != null &&
                   value.gameObject.activeInHierarchy &&
                   value.isActive;
        }

        private TMP_Text FindText(string path)
        {
            var child = transform.Find(path);

            return child != null
                ? child.GetComponent<TMP_Text>()
                : null;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null && target.text != value)
                target.text = value;
        }
    }
}