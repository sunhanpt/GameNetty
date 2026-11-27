using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private Run journeyCoinRun;
        private long journeyCoinBalance;
        private RectTransform journeyCoinLayer;
        private Coroutine journeyCoinAnimation;

        private void RefreshJourneyCoins()
        {
            var run = game.Run;
            if (run == null || game.Phase is Phase.Camp or Phase.Result)
            {
                journeyCoinRun = null;
                ClearJourneyCoins();
                return;
            }

            long balance = (long)run.gold + run.milestoneGold;
            if (journeyCoinRun != run)
            {
                ClearJourneyCoins();
                journeyCoinRun = run;
                journeyCoinBalance = balance;
                return;
            }

            long previous = journeyCoinBalance;
            journeyCoinBalance = balance;
            if (balance != previous) ClearJourneyCoins();
            if (balance > previous)
                journeyCoinAnimation = StartCoroutine(AnimateJourneyCoins(previous, balance));
            if (journeyCoinLayer != null) journeyCoinLayer.SetAsLastSibling();
        }

        private IEnumerator AnimateJourneyCoins(long previous, long balance)
        {
            const float spreadTime = .30f;
            const float flightTime = .30f;
            const float stagger = .04f;
            const float holdTime = 1f;
            const float slideTime = .23f;
            long reward = balance - previous;
            int count = Mathf.Clamp((int)(reward / 20), 3, 10);

            journeyCoinLayer = LoadView("JourneyCoinReward", portrait);
            var counter = MountPart(journeyCoinLayer, "GoldRewards/AnimateJourneyCoins/counter");
            counter.name = "Journey gold counter";
            counter.GetComponent<Image>().sprite = world.PillSprite();
            var target = InstantiateUi("Widgets/JourneyCoinTarget", counter);
            var value = MountText(counter, "GoldRewards/AnimateJourneyCoins/value", value: previous.ToString());

            var tokens = new RectTransform[count];
            for (int i = 0; i < count; i++)
                tokens[i] = InstantiateUi("Widgets/JourneyCoinToken", journeyCoinLayer);

            float lastArrival = spreadTime + flightTime + (count - 1) * stagger;
            int shownArrivals = 0;
            float displayedAmountAt = -1;
            for (float elapsed = 0; elapsed < lastArrival + holdTime + slideTime;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .06f))
            {
                float enter = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / slideTime));
                float exit = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lastArrival + holdTime, lastArrival + holdTime + slideTime, elapsed));
                counter.anchoredPosition = Vector2.right * (counter.rect.width + 10) * (1 - enter + exit);
                var destination = (Vector2)journeyCoinLayer.InverseTransformPoint(target.position);
                var origin = new Vector2(journeyCoinLayer.rect.width * .045f, journeyCoinLayer.rect.height * .037f);
                float spread = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / spreadTime));
                int arrivals = 0;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 2.399963f;
                    var release = origin + new Vector2(Mathf.Cos(angle) * 38, Mathf.Sin(angle) * 43);
                    float flight = Mathf.Clamp01((elapsed - spreadTime - i * stagger) / flightTime);
                    var token = tokens[i];
                    if (flight >= 1)
                    {
                        if (token.gameObject.activeSelf) token.gameObject.SetActive(false);
                        arrivals++;
                        continue;
                    }
                    var position = Vector2.Lerp(Vector2.Lerp(origin, release, spread), destination, flight * flight);
                    position.y += Mathf.Sin(flight * Mathf.PI) * 22;
                    token.anchoredPosition = position;
                    token.localScale = Vector3.one * (spread * Mathf.Lerp(1.05f, .54f, flight));
                }
                if (shownArrivals != arrivals)
                {
                    shownArrivals = arrivals;
                    value.text = (previous + reward * arrivals / count).ToString();
                    displayedAmountAt = elapsed;
                }
                float pulse = Mathf.InverseLerp(displayedAmountAt, displayedAmountAt + .18f, elapsed);
                target.localScale = Vector3.one * (1 + .20f * Mathf.Sin(pulse * Mathf.PI));
                yield return null;
            }

            journeyCoinAnimation = null;
            ClearJourneyCoins();
        }

        private void ClearJourneyCoins()
        {
            if (journeyCoinAnimation != null)
            {
                StopCoroutine(journeyCoinAnimation);
                journeyCoinAnimation = null;
            }
            if (journeyCoinLayer == null) return;
            journeyCoinLayer.gameObject.SetActive(false);
            Destroy(journeyCoinLayer.gameObject);
            journeyCoinLayer = null;
        }
    }
}
