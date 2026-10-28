// Copy into Assets/Editor of an isolated Unity project; run ProjectDM.PickupFlightRegression.Run.
using System;
using UnityEditor;
using UnityEngine;

namespace ProjectDM
{
    public static class PickupFlightRegression
    {
        private static int checks;
        private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }

        public static void Run()
        {
            try
            {
                Vector2 start = new Vector2(400, 300);
                Vector2 testScreen = new Vector2(1280, 720);
                foreach (Vector2 end in new[] { new Vector2(1000, 40), new Vector2(160, 650), start })
                for (int seed = 1; seed <= 18; seed++)
                {
                    var route = PickupFlightPresentation.CreateRoute(seed);
                    Check(Vector2.Distance(PickupFlightPresentation.Sample(start, end, 0, route, testScreen), start) < .001f, "Start must match the collected position");
                    Check(Vector2.Distance(PickupFlightPresentation.Sample(start, end, 1, route, testScreen), end) < .001f, "Flight must end at the HUD target");
                    Vector2 previous = start;
                    for (int frame = 1; frame <= 40; frame++)
                    {
                        Vector2 point = PickupFlightPresentation.Sample(start, end, frame / 40f, route, testScreen);
                        Check(Vector2.Dot(point - previous, (end - start).normalized) >= -.001f, "Curved flights must keep moving forward without a backward launch or hold");
                        Check(point.x >= 0 && point.x <= testScreen.x && point.y >= 0 && point.y <= testScreen.y, "Curved flights must stay on screen");
                        previous = point;
                    }
                    if (end != start) Check(Vector2.Distance(start, PickupFlightPresentation.Sample(start, end, .01f, route, testScreen)) > 1, "No initial hesitation");
                }

                float shortest = float.MaxValue, longest = 0;
                bool smallArc = false, largeArc = false, sCurve = false;
                Vector2 routeEnd = new Vector2(1000, 40), normal = new Vector2(260, 600).normalized;
                for (int seed = 1; seed <= 30; seed++)
                {
                    var route = PickupFlightPresentation.CreateRoute(seed);
                    shortest = Mathf.Min(shortest, route.duration); longest = Mathf.Max(longest, route.duration);
                    float minSide = 0, maxSide = 0;
                    for (int frame = 1; frame < 40; frame++)
                    {
                        float side = Vector2.Dot(PickupFlightPresentation.Sample(start, routeEnd, frame / 40f, route, testScreen) - start, normal);
                        minSide = Mathf.Min(minSide, side); maxSide = Mathf.Max(maxSide, side);
                    }
                    float extent = Mathf.Max(maxSide, -minSide);
                    if (route.kind == 0 && extent > 20 && extent < 100) smallArc = true;
                    if (route.kind == 1 && extent > 100) largeArc = true;
                    if (route.kind == 2 && minSide < -5 && maxSide > 5) sCurve = true;
                }
                Check(smallArc && largeArc && sCurve, "Pickup groups must visibly mix small arcs, large arcs and S-shaped paths");
                Check(longest - shortest > .25f, "Flights must have enough timing variation to overtake one another");

                var cameraObject = new GameObject("pickup-flight-test-camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
                var settings = new GameSettings { gameplayCamera = camera };
                Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f);
                var flights = new PickupFlightPresentation(settings);
                Vector2 screen = new Vector2(Screen.width, Screen.height);
                Vector2 currency = RunHudView.CurrencyIconRect(screen).center, manaTarget = RunHudView.ManaTrackRect(screen).center;
                Vector2 experienceTarget = RunHudView.ExperienceTrackRect(screen).center;
                Check(!RunHudView.ExperienceTrackRect(screen).Overlaps(RunHudView.ManaTrackRect(screen)), "Experience and mana bars must have separate destinations");
                foreach (Vector2 viewport in new[] { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(640, 360), new Vector2(360, 640) })
                {
                    Rect rail = RunHudView.ExperienceTrackRect(viewport), badge = RunHudView.ExperienceBadgeRect(viewport);
                    Check(rail.x == 0 && rail.width == viewport.x && rail.height == 30 && rail.yMax == viewport.y, "Experience rail must sit flush against the full bottom edge");
                    Check(rail.Contains(badge.min) && rail.Contains(badge.max - Vector2.one * .01f), "Level text must stay inside the experience bar");
                    Check(Mathf.Abs(badge.center.x - viewport.x * .5f) < .01f && badge.x >= 0 && badge.xMax <= viewport.x, "Level plaque must stay centered and inside the screen");
                    Check(!badge.Overlaps(RunHudView.ManaTrackRect(viewport)), "Level plaque must not cover mana");
                    Rect portrait = RunHudView.PortraitHudRect(viewport), manaTrack = RunHudView.ManaTrackRect(viewport);
                    Check(portrait.yMax == rail.y && manaTrack.yMax == rail.y, "Portrait and mana must sit flush above experience");
                    Check(portrait.width == 176 && portrait.height == 176, "Portrait must be twice its original size");
                    Check(manaTrack.x == portrait.xMax + 12 && manaTrack.xMax <= viewport.x - 16, "Mana must fit to the right of the portrait");
                    Check(!portrait.Overlaps(manaTrack), "Portrait must not cover mana");
                }
                foreach (string type in new[] { "projectile", "flame", "frost", "lightning" })
                    Check(Resources.Load<Texture2D>("ProjectDM/UI/Portraits/" + type) != null, "Every mage must have a HUD portrait: " + type);
                flights.Show(Vector3.zero, null, PickupKind.Currency);
                flights.Show(Vector3.zero, sprite, PickupKind.Magnet);
                flights.Show(new Vector3(0, 0, -20), sprite, PickupKind.Currency);
                Check(flights.ActiveCount == 0, "Invalid or behind-camera sources must not create flights");
                for (int i = 0; i < 300; i++) flights.Show(Vector3.zero, sprite, i % 3 == 0 ? PickupKind.Currency : i % 3 == 1 ? PickupKind.Mana : PickupKind.Experience);
                Check(flights.ActiveCount == 300, "Bulk pickups must retain every visible flight");
                flights.Tick(.1f, screen, currency, manaTarget, experienceTarget);
                Check(flights.SparkCount > 0 && flights.CurrencyPulse == 0 && flights.ManaPulse == 0, "Trail sparkles must appear before arrivals");
                int count = flights.SparkCount;
                flights.Tick(0, screen, currency, manaTarget, experienceTarget);
                Check(count == flights.SparkCount && flights.ActiveCount == 300, "Pausing must freeze the presentation");
                flights.Tick(1, screen, currency, manaTarget, experienceTarget);
                Check(flights.ActiveCount == 0 && flights.CurrencyPulse == 1 && flights.ManaPulse == 1 && flights.ExperiencePulse == 1, "Arrival must pulse all three destinations");
                flights.Tick(.65f, screen, currency, manaTarget, experienceTarget);
                Check(flights.SparkCount == 0 && flights.ManaPulse < .001f, "Sparkles and pulses must finish");
                flights.Clear();

                flights.Show(Vector3.zero, sprite, PickupKind.Currency, 17);
                flights.Show(Vector3.zero, sprite, PickupKind.Mana, 5);
                flights.Show(Vector3.zero, sprite, PickupKind.Experience, 9);
                Check(flights.PendingCurrency == 17 && flights.PendingMana == 5 && flights.PendingExperience == 9, "Each visible flight must defer its HUD amount only");
                flights.Tick(.1f, screen, currency, manaTarget, experienceTarget);
                Check(flights.PendingCurrency == 17 && flights.PendingExperience == 9, "Amounts must remain pending before arrival");
                flights.Tick(1, screen, currency, manaTarget, experienceTarget);
                Check(flights.PendingMana == 5 && flights.PendingExperience == 9, "Arrival must begin filling rather than jumping immediately");
                flights.Tick(.14f, screen, currency, manaTarget, experienceTarget);
                Check(flights.PendingMana > 0 && flights.PendingMana < 5 && flights.PendingExperience > 0 && flights.PendingExperience < 9, "Arrived gains must fill gradually");
                flights.Tick(.2f, screen, currency, manaTarget, experienceTarget);
                Check(flights.PendingCurrency == 0 && flights.PendingMana == 0 && flights.PendingExperience == 0, "Arrival must release the matching HUD amounts");
                flights.Show(Vector3.zero, sprite, PickupKind.Currency, 4);
                flights.Clear();
                Check(flights.PendingCurrency == 0, "Screen changes must flush pending display amounts");

                foreach (float screenHeight in new[] { 540f, 720f, 1080f, 1440f, 2160f })
                    Check(PickupFlightPresentation.VisualSize(screenHeight) >= 32f, "Visible artwork must stay readable at every supported resolution");
                Check(PickupFlightPresentation.VisualSize(1080) == 38f, "Use the smaller artwork size while retaining cropped transparent margins");
                for (int seed = 1; seed <= 100; seed++)
                {
                    Check(PickupFlightPresentation.SparkOpacity(.2f, .4f, seed) > .3f, "Trail cores must remain visibly bright halfway through their lifetime");
                    Check(PickupFlightPresentation.SparkOpacity(.4f, .4f, seed) == 0, "Trail lights must finish fading out");
                }
                Check(PickupFlightPresentation.Opacity(.8f) == 1 && PickupFlightPresentation.Opacity(.94f) > 0 && PickupFlightPresentation.Opacity(.94f) < 1 && PickupFlightPresentation.Opacity(1) == 0,
                    "Maintain visibility until near arrival, then fade without shrinking");
                foreach (string path in new[] {
                    "Assets/GameContent/Art/Pixel32/CollectiblesAndProjectiles/Currency_01/Currency_01_1.png",
                    "Assets/GameContent/Art/Pixel32/CollectiblesAndProjectiles/Experience_01/Experience_01_0.png",
                    "Assets/GameContent/Art/Pixel32/CollectiblesAndProjectiles/ProjectDM_ManaPickup_v1/ProjectDM_ManaPickup_v1.png" })
                {
                    Sprite actual = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Rect uv = PickupFlightPresentation.ContentUv(actual);
                    Check(uv.width > 0 && uv.height > 0 && uv.width < .9f, "Use the actual coin/mana silhouette instead of its transparent margins");
                }

                var tables = GameTables.LoadResources(settings, false);
                var save = new SaveService(tables, new MemoryProgressStore(), true) { progress = new PrototypeSave(), metaCurrency = 0 };
                var progression = new ProgressionService(save, tables);
                var art = new WorldArtAssets { coinSprite = sprite };
                var stats = new PlayerStats(tables, progression);
                var world = new PlayerWorld(settings, tables, art, stats);
                var mana = new RunMana();
                var run = new RunController(settings, save, tables, progression, world, null, null, null, () => { }, () => { }, () => false, mana);
                var selection = new LevelUpController(tables, progression, run, null) { level = 2, experience = 3, choosingUpgrade = true };
                var hud = new RunHudView(settings, save, tables, run, null, null, selection, new UiGraphics(), null, art);
                hud.ShowPickupFlight(Vector3.zero, sprite, PickupKind.Experience, 5);
                hud.ExperienceDisplay(out int shownLevel, out double shownXp, out int shownThreshold);
                Check(shownLevel == 1 && shownXp == PrototypeRules.XpThreshold(tables.balance, 1) - 2,
                    "In-flight XP crossing a level must keep the previous HUD level and progress until arrival");
                hud.ResetPickupFeedback();
                hud.ExperienceDisplay(out shownLevel, out shownXp, out shownThreshold);
                Check(shownLevel == 2 && shownXp == 3, "Flushing flight display must reveal the actual XP and level");
                int callbacks = 0;
                float experience = 0;
                PickupKind lastKind = PickupKind.Currency;
                Sprite lastImage = null;
                var pickups = new PickupSystem(settings, save, tables, progression, art, world, run, null, amount => experience += amount, stats, null,
                    (position, image, kind, amount) => { callbacks++; lastKind = kind; lastImage = image; flights.Show(position, image, kind, amount); });
                var coin = new Pickup { kind = PickupKind.Currency, amount = 17, batchSprite = sprite };
                pickups.Collect(coin);
                Check(save.metaCurrency == 17 && run.runCurrency == 17 && flights.ActiveCount == 1, "Currency is granted before the animation arrives");
                pickups.Collect(coin);
                Check(save.metaCurrency == 17 && callbacks == 1, "Duplicate collection must not grant currency or animate twice");
                mana.Consume(12);
                pickups.Collect(new Pickup { kind = PickupKind.Mana, amount = 5, batchSprite = sprite });
                Check(mana.Current == 33 && callbacks == 2, "Mana must recover immediately");
                pickups.Collect(new Pickup { kind = PickupKind.Mana, amount = 100, batchSprite = sprite });
                Check(mana.Current == 40 && callbacks == 3, "Overflow mana must be clamped");
                pickups.Collect(new Pickup { kind = PickupKind.Mana, amount = 5, batchSprite = sprite });
                Check(callbacks == 3, "Full mana must not pretend to absorb another drop");
                art.runtimeAssets = new ProjectDMRuntimeAssets { ManaPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Prefabs/ManaPickup.prefab") };
                pickups.CreatePickup("Mana size check", Vector3.zero, null, PickupKind.Mana, 3, .28f);
                Pickup fieldMana = pickups.pickups[pickups.pickups.Count - 1];
                Rect manaUv = PickupFlightPresentation.ContentUv(fieldMana.batchSprite);
                float visibleHeight = fieldMana.batchSprite.bounds.size.y * manaUv.height * fieldMana.baseScale * camera.pixelHeight / (2 * camera.orthographicSize);
                Check(Mathf.Abs(visibleHeight - PickupFlightPresentation.VisualSize(Screen.height)) < .1f,
                    "Field mana artwork must match the flight artwork's visible screen size");
                mana.Consume(3);
                pickups.Collect(new Pickup { kind = PickupKind.Mana, amount = 3, batched = true });
                Check(mana.Current == 40 && callbacks == 4 && lastKind == PickupKind.Mana && lastImage != null && lastImage.name == "ProjectDM_ManaPickup_v1",
                    "Real batched mana pickups must pass their visible sprite to the mana flight");
                pickups.Collect(new Pickup { kind = PickupKind.Chest, amount = 9 });
                Check(save.metaCurrency == 26 && callbacks == 5, "Chest currency must use the currency destination");
                var xp = new Pickup { kind = PickupKind.Experience, amount = 4, batchSprite = sprite };
                pickups.Collect(xp);
                Check(experience == 4 && callbacks == 6 && lastKind == PickupKind.Experience, "Experience must award immediately and fly to its own UI");
                pickups.Collect(xp);
                Check(experience == 4 && callbacks == 6, "Experience must not award or animate twice");
                run.transition = new ScreenTransition();
                run.EndRun();
                Check(run.runEnded && !run.transition.Active && save.progress.completedRuns == 1, "Results must appear immediately without a screen fade");
                run.EndRun();
                Check(save.progress.completedRuns == 1, "Immediate results must still save once only");
                pickups.Collect(new Pickup { kind = PickupKind.Currency, amount = 10, batchSprite = sprite });
                Check(save.metaCurrency == 26 && callbacks == 6, "Ended runs must not award or animate late pickups");
                flights.Clear();
                Check(flights.ActiveCount == 0 && flights.SparkCount == 0 && flights.CurrencyPulse == 0 && save.metaCurrency == 26, "Screen reset clears visuals without touching rewards");
                run.ReturnToGrowthMap();
                Check(run.transition.Active && !run.showMetaTree, "Growth-map navigation must retain its fade");
                Time.timeScale = 1;
                pickups.Dispose();
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                Debug.Log($"PICKUP_FLIGHT_PASS {checks} checks: immediate forward flight, trails, mass pickups, arrival, pause, rewards and duplicate collection.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
