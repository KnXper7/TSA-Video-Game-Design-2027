using System.Collections;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The crash site on the planet that looks like Earth. Objectives, in order:
    /// scan the air (oxygen stops draining once it's breathable), salvage parts, investigate the ruins,
    /// then follow a radio signal to a second rocket. Navigation 3+ marks salvage with beacons; Survival slows oxygen loss.
    /// </summary>
    public class LandingSiteSequence : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] OxygenSystem oxygen;
        [SerializeField] Interactable airScanner;
        [SerializeField] Interactable[] salvage;
        [SerializeField] GameObject[] salvageBeacons;
        [SerializeField] int navigationForBeacons = 3;
        [SerializeField] TriggerZone ruinsZone;
        [SerializeField] TriggerZone signalZone;
        [SerializeField] GameObject signalMarker;

        int salvaged;
        bool failed;

        void Start()
        {
            foreach (var beacon in salvageBeacons) beacon.SetActive(false);
            if (signalMarker != null) signalMarker.SetActive(false);
            signalZone.gameObject.SetActive(false);
            oxygen.Depleted += () => StartCoroutine(OutOfOxygen());
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var hud = HUD.Instance;
            var state = GameState.Instance;

            player.CanMove = false;
            hud.SetOxygen(oxygen.Normalized);
            hud.ShowOxygen(true);
            yield return hud.FadeTo(0f, 3f, Color.black);

            yield return Dialogue.Say(Speakers.ShipAI, "Hull integrity forty percent. We're on the ground. Mostly in one piece.");
            yield return Dialogue.Say(Speakers.Commander, "Mostly?");
            yield return Dialogue.Say(Speakers.ShipAI, "Your suit has limited oxygen. I suggest finding out if this air will kill you.");

            // 1. Air
            player.CanMove = true;
            oxygen.Draining = true;
            hud.SetObjective("Scan the atmosphere with the sensor kit");
            yield return WaitForUse(airScanner);

            oxygen.Draining = false;
            hud.ShowOxygen(false);
            state.SetFlag("site.air_breathable");
            state.AddXp(30, "Atmosphere scanned");
            yield return Dialogue.Say(Speakers.ShipAI, "Nitrogen seventy-eight percent. Oxygen twenty-one. Commander... it's the same air as Earth.");
            yield return Dialogue.Say(Speakers.Commander, "That's not possible.");
            yield return Dialogue.Say(Speakers.ShipAI, "And yet you may remove your helmet.");

            // 2. Salvage
            if (state.Check(Stat.Navigation, navigationForBeacons))
            {
                foreach (var beacon in salvageBeacons) beacon.SetActive(true);
                hud.ShowToast($"NAVIGATION {navigationForBeacons}: salvage marked on your scanner");
            }
            foreach (var part in salvage) part.Interacted += OnSalvaged;
            UpdateSalvageObjective();
            yield return new WaitUntil(() => salvaged >= salvage.Length);
            yield return Dialogue.Say(Speakers.ShipAI, "That's enough to patch the hull. Now, about those structures in the trees.");

            // 3. Ruins
            hud.SetObjective("Investigate the structures in the forest");
            bool reachedRuins = false;
            ruinsZone.Entered += () => reachedRuins = true;
            yield return new WaitUntil(() => reachedRuins);
            state.SetFlag("site.found_ruins");
            state.AddXp(40, "Discovery: alien ruins");
            yield return Dialogue.Say(Speakers.Commander, "These aren't natural. Someone built these. A long time ago.");
            yield return Dialogue.Say(Speakers.ShipAI, "The markings match nothing in my database. They look like... coordinates? I'm guessing.");

            // 4. Radio signal
            yield return new WaitForSeconds(1.5f);
            yield return Dialogue.Say(Speakers.ShipAI, "Commander, I'm picking up a radio signal. It's human. An old frequency. Very old.");
            hud.SetObjective("Follow the radio signal");
            signalZone.gameObject.SetActive(true);
            if (signalMarker != null) signalMarker.SetActive(true);
            bool reachedSignal = false;
            signalZone.Entered += () => reachedSignal = true;
            yield return new WaitUntil(() => reachedSignal);

            hud.SetObjective(null);
            player.CanMove = false;
            state.SetFlag("site.found_second_rocket");
            state.AddXp(50, "Discovery: a second rocket");
            yield return Dialogue.Say(Speakers.Commander, "Another rocket. That mission patch... that's from 2031.");
            yield return Dialogue.Say(Speakers.ShipAI, "Commander. There is someone inside.");

            yield return hud.FadeTo(1f, 2f, Color.black);
            yield return hud.ShowTitle("END OF INTRO", "Next: the other pilot", 3f);
        }

        IEnumerator WaitForUse(Interactable interactable)
        {
            bool used = false;
            interactable.Interacted += _ => used = true;
            yield return new WaitUntil(() => used);
        }

        void OnSalvaged(Interactable part)
        {
            salvaged++;
            GameState.Instance.AddXp(25, "Salvage recovered");
            int index = System.Array.IndexOf(salvage, part);
            if (index >= 0 && index < salvageBeacons.Length) salvageBeacons[index].SetActive(false);
            UpdateSalvageObjective();
        }

        void UpdateSalvageObjective()
        {
            if (salvaged < salvage.Length)
                HUD.Instance.SetObjective($"Salvage parts from the wreckage ({salvaged}/{salvage.Length})");
        }

        IEnumerator OutOfOxygen()
        {
            if (failed) yield break;
            failed = true;
            player.CanMove = false;
            yield return Dialogue.Say(Speakers.ShipAI, "Commander! Oxygen critical. Commander, do you read?");
            yield return HUD.Instance.FadeTo(1f, 2f, Color.black);
            yield return HUD.Instance.ShowTitle("OXYGEN DEPLETED", "Restarting from the crash site", 2f, new Color(1f, 0.4f, 0.35f));
            SceneFlow.Reload(0.1f);
        }
    }
}
